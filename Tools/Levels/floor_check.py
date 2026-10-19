#!/usr/bin/env python3
"""
Static check of a path floor's footholds (no Unity needed): every jump within the hero's reach (and the bot's), every
foothold inside the screen while its section is on, every section able to end (hero + travelLead at its end), no lava
box over a foothold or a jump, every enemy of an Arena inside its held screen. Numbers from player.json (moveSpeed 2, jumpForce 9, gravityScale 1.8 -> apex 2.29) and
WorldEffects (ortho 2.8, 16:9, TravelEdge walls). Moving pieces are checked where they spawn.
Updraft columns are carriers (in from a foothold, out onto one up to the column's throw); a counterweight has gone down
(and its partner up) by 0.35 s of its speed when he jumps off it.

  python3 Tools/Levels/floor_check.py Floor999 Floor998 ...   (no args: Floors 999-991, 960-951 and 910-902)
"""
import math, sys
import floorgeo as g

G = 9.81 * 1.8
V = 9.0
SPEED = 2.0
R = 0.3            # hero collider radius
HALF_H = 2.8
HALF_W = 2.8 * 16 / 9
BOT_RISE = 1.9     # PlaytestBot.Climb ignores footholds higher than this
APEX = V * V / (2 * G)

def air_time(rise):
    """Seconds in the air from a jump to landing 'rise' higher (None when out of reach)."""
    d = V * V - 2 * G * rise
    if d < 0:
        return None
    return (V + math.sqrt(d)) / G

def gap(a, b):
    """Horizontal gap between two boxes (0 when they overlap in x)."""
    return max(0.0, max(b[0] - a[2], a[0] - b[2]))

class Chain:
    """A swing chain's handle: 'at' is the bottom of its arc, the pivot 'length' above it."""
    def __init__(self, p):
        a = g.ability(p.e, 'swingChain')
        self.p, self.sec, self.id, self.name = p, p.sec, p.id, p.name
        self.L, self.amp, self.period, self.hang = a.get('length', 2.6), math.radians(a.get('amplitude', 40)), a.get('period', 2.8), a.get('hang', 0.55)
        self.grab = a.get('grabRadius', 0.85) + 0.35
        self.at = p.at
        dx = self.L * math.sin(self.amp)
        self.box = (p.at[0] - dx, p.at[1], p.at[0] + dx, p.at[1] + self.L * (1 - math.cos(self.amp)))
        self.top = p.at[1] - 0.5
        self.onEnter = p.onEnter
        self.tags = set()
    carrier = True
    def __repr__(self):
        return f"{self.name}#{self.id}[handle x {self.box[0]:.2f}..{self.box[2]:.2f}, y {self.box[1]:.2f}..{self.box[3]:.2f}]"
    def vmax(self):
        return self.L * self.amp * 2 * math.pi / self.period

class Draft:
    """A heat updraft's column: inside it he is carried up at 'lift' until his body leaves its top (UpdraftAbility)."""
    carrier = True
    def __init__(self, p):
        a = g.ability(p.e, 'updraft')
        self.p, self.sec, self.id, self.name, self.e = p, p.sec, p.id, p.name, p.e
        self.box = p.box
        self.lift = a.get('lift', 5.0)
        self.onEnter = p.onEnter
        self.tags = set()
        self.at = p.at
    @property
    def top(self):
        return self.box[3]
    @property
    def apex(self):
        """His feet's highest point: out of the top at 'lift', then thrown on."""
        return self.box[3] + self.lift * self.lift / (2 * G)
    def has(self, kind):
        return False
    def __repr__(self):
        b = self.box
        return f"{self.name}{('#' + self.id) if self.id else ''}[column x {b[0]:.2f}..{b[2]:.2f}, y {b[1]:.2f}..{b[3]:.2f}]"

def cw_move(p):
    """How far a counterweight has gone down by the time he jumps off it (its partner up as far)."""
    cw = g.ability(p.e, 'counterweight') if hasattr(p, 'e') and not getattr(p, 'carrier', False) else None
    return (min(cw.get('drop', 2.4), cw.get('speed', 1.2) * 0.35), cw.get('partner', '')) if cw else (0.0, '')

def flight(x0, feet0, b, v0, way):
    """Hero leaves (x0, feet0) jumping with sideways momentum v0 (decaying 3/s) and runs 'way': lands on b?"""
    rise = b.top - feet0
    t = air_time(rise) if rise > 0 else air_time(0) + math.sqrt(2 * -rise / G)
    if t is None:
        return False, f"rise {rise:.2f} from the swing"
    tm = min(t, v0 / 3) if v0 > 0 else 0
    run = SPEED * t + v0 * tm - 1.5 * tm * tm
    xs = (x0 + way * run)
    lo, hi = min(x0, xs), max(x0, xs)
    ok = hi >= b.box[0] - R * 0.9 and lo <= b.box[2] + R * 0.9
    return ok, f"swing release at x {x0:.2f} (v {v0:.1f}) reaches x {xs:.2f}, rise {rise:+.2f}"

def reach(a, b, bot):
    """Can the hero standing on a jump onto b? Returns (ok, why)."""
    if isinstance(b, Chain):
        rise = b.box[1] - (a.top + R)          # handle above his centre when he leaves
        if rise > (2.6 if bot else APEX + b.grab):
            return False, f"handle {rise:.2f} above him"
        t = air_time(max(0.0, rise - b.grab)) or 0.5
        need = gap(a.box, b.box) - b.grab
        if need > SPEED * t:
            return False, f"handle {gap(a.box, b.box):.2f} away"
        return True, f"grab, handle {gap(a.box, b.box):.2f} away"
    if isinstance(b, Draft):
        # Into the column: walk off or jump from his foothold and have his body cross it on the way.
        if isinstance(a, Draft):
            return False, "column to column"
        if b.box[3] < a.top + 0.6:
            return False, f"column top {b.box[3]:.2f} under him"
        if b.box[1] > a.top + (1.2 if bot else APEX - 0.3):
            return False, f"column foot {b.box[1] - a.top:.2f} above him"
        gp = gap(a.box, b.box)
        if gp > (1.6 if bot else 2.0):
            return False, f"column {gp:.2f} away"
        return True, f"into the column, {gp:.2f} away"
    if isinstance(a, Draft):
        if isinstance(b, Chain):
            return False, "column to chain"
        # PlaytestBot leaves the column once the throw (lift at the top) carries his feet 0.25 over b.
        if b.top > a.apex - (0.25 if bot else 0.1):
            return False, f"{b.top - a.apex:+.2f} over the column's throw"
        if b.top < a.box[1] - 3.0:
            return False, "far under the column"
        fall = max(0.0, a.apex - b.top)
        t = a.lift / G + math.sqrt(2 * fall / G)
        gp = gap(a.box, b.box)
        if gp - R * 0.9 > SPEED * t * (0.8 if bot else 1.0):
            return False, f"gap {gp:.2f} from the column"
        return True, f"out of the column, gap {gp:.2f}"
    if isinstance(a, Chain):
        way = 1 if (b.box[0] + b.box[2]) / 2 > a.at[0] else -1
        body_feet = a.at[1] - a.hang - R
        # PlaytestBot lets go just past the bottom of the swing, at its fastest, like a player does.
        return flight(a.at[0], body_feet, b, a.vmax() * (0.95 if bot else 1.0), way)
    sink = g.ability(a.e, 'sink') if hasattr(a, 'e') else None
    sunk = min(sink.get('depth', 1.5), sink.get('sinkSpeed', 1.0) * 0.35) if sink and not sink.get('loopPeriod') else 0.0
    cw, partner = cw_move(a)
    sunk = max(sunk, cw)
    raised = cw if partner and getattr(b, 'id', '') == partner else 0.0
    rise = b.top + raised - (a.top - sunk)
    if rise > (BOT_RISE if bot else APEX - 0.15):
        return False, f"rise {rise:.2f}"
    if rise < -4.5:
        return False, f"drop {-rise:.2f}"
    t = air_time(rise) if rise > 0 else air_time(0) + math.sqrt(2 * -rise / G)
    run = SPEED * t
    gp = gap(a.box, b.box)
    need = gp - 2 * R * 0.9
    if bot and gp > 0:
        # PlaytestBot.Climb: a higher foothold within 1.3 of him: jumps where he stands; else from 0.4 inside his edge.
        if rise > 0.3 and gp - R * 0.9 < 1.3:
            need = min(gp - R * 0.9, 1.3) - R * 0.9
        else:
            need = gp + 0.4 - R * 0.9
    if need > run - (0.1 if bot else 0):
        return False, f"gap {gap(a.box, b.box):.2f} > {run + 2 * R * 0.9:.2f}"
    return True, f"rise {rise:+.2f} gap {gap(a.box, b.box):.2f}"

def camera_points(sections, i, steps=40):
    """The camera's positions while it is in section i (WorldEffects.PathPoint: turns rounded off)."""
    at = [0.0]
    for s in sections:
        at.append(at[-1] + s['length'])
    def straight(dist):
        k = len(sections) - 1
        for j, s in enumerate(sections):
            if dist < at[j] + s['length'] or j == len(sections) - 1:
                k = j
                break
        s = sections[k]
        ax = g.AXIS[s['dir']]
        t = min(max(dist - at[k], 0), s['length'])
        return (s['start'][0] + ax[0] * t, s['start'][1] + ax[1] * t)
    def point(dist):
        for c in range(1, len(sections)):
            a_, b_ = sections[c - 1], sections[c]
            r = min(max(0, a_.get('turn', 2)), a_['length'] * 0.5, b_['length'] * 0.5)
            corner = at[c]
            if r > 0.01 and abs(dist - corner) < r and g.AXIS[a_['dir']] != g.AXIS[b_['dir']]:
                A, B, K = straight(corner - r), straight(corner + r), b_['start']
                t = (dist - (corner - r)) / (2 * r)
                u = 1 - t
                return (u * u * A[0] + 2 * u * t * K[0] + t * t * B[0], u * u * A[1] + 2 * u * t * K[1] + t * t * B[1])
        return straight(dist)
    s = sections[i]
    return [point(at[i] + s['length'] * n / steps) for n in range(steps + 1)]

def window(sections, i):
    """x and y ranges of the hero's centre while the camera is in section i."""
    pts = camera_points(sections, i)
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return (min(xs) - HALF_W + R, max(xs) + HALF_W - R, min(ys) - HALF_H, max(ys) + HALF_H + APEX)

def check(name, verbose=False):
    d, script, world, sections = g.level(name)
    lead = world.get('travelLead', 1.2)
    out = []
    feet_all = []
    lavas = []
    for sec in sections:
        for p in sec['pieces']:
            if p.has('swingChain') and 'imp' not in p.name.lower() and not p.has('contactDamage'):
                feet_all.append(Chain(p))
            elif p.has('updraft'):
                feet_all.append(Draft(p))
            elif p.foothold and p.box[2] - p.box[0] > 0.4:
                feet_all.append(p)
                lift = g.ability(p.e, 'chainLift')
                if lift:
                    tr = list(lift.get('travel', [0, 3]))
                    if lift.get('cutterTag'):
                        tr[1] -= lift.get('maxCut', 2.0)   # the imps got every cut in
                    top = g.Piece(p.sec, {**p.spawn, 'at': [p.at[0] + tr[0], p.at[1] + tr[1]], 'id': (p.id or 'lift') + '@top'}, p.e)
                    top.liftOf = p
                    feet_all.append(top)
                raft = g.ability(p.e, 'raft')
                if raft and raft.get('range', 0) > 0:
                    cur = raft.get('current', [0.8, 0])
                    norm = math.hypot(cur[0], cur[1]) or 1
                    far = g.Piece(p.sec, {**p.spawn, 'at': [p.at[0] + cur[0] / norm * raft['range'], p.at[1] + cur[1] / norm * raft['range']],
                                          'id': (p.id or 'raft') + '@drift'}, p.e)
                    far.liftOf = p
                    feet_all.append(far)
            elif p.lava and p.name not in ('lavaSea',) and not p.has('surge'):
                lavas.append(p)
    start = next((p for p in feet_all if p.id == 'start'), feet_all[0] if feet_all else None)
    landing = next((p for p in feet_all if 'landing' in p.tags), None)
    # Reachable set, section by section: what's out during section i is every spawn of sections <= i+1.
    reached = {id(start): start} if start else {}
    for sec in sections:
        i = sec['i']
        x0, x1, y0, y1 = window(sections, i)
        out_now = [p for p in feet_all if p.sec <= i + 1 and not (p.onEnter and p.sec > i)]
        inside = [p for p in out_now if p.box[2] >= x0 - R * 0.9 and p.box[0] <= x1 + R * 0.9
                  and (p.box[1] <= y1 and p.top >= y0 if isinstance(p, Draft) else p.top <= y1 and p.top >= y0 - 1.0)]
        nxt = window(sections, i + 1) if i + 1 < len(sections) else None
        for p in feet_all:
            if nxt and p.box[2] >= nxt[0] - R * 0.9 and p.box[0] <= nxt[1] + R * 0.9:
                continue
            if p.sec == i and p not in inside and getattr(p, 'liftOf', None) is None:
                out.append(f"  ! {sec['name']}: {p} is outside the screen while its section is on (x {x0:.2f}..{x1:.2f})")
        changed = True
        while changed:
            changed = False
            for b in inside:
                if id(b) in reached:
                    continue
                if getattr(b, 'liftOf', None) is not None and id(b.liftOf) in reached:
                    reached[id(b)] = b
                    b.via = (b.liftOf, 'rides it')
                    changed = True
                    continue
                for a in list(reached.values()):
                    ok, why = reach(a, b, bot=True)
                    if ok:
                        reached[id(b)] = b
                        b.via = (a, why)
                        changed = True
                        break
        if sec['dir'] == 'Arena':
            # The camera holds on the arena's start: whoever the fight needs stands inside that screen.
            cx = sec['start'][0]
            for p in sec['pieces']:
                if p.e.get('team', 'Enemy') == 'Enemy' and (p.e.get('health') or 0) > 0 and not p.foothold \
                        and not (cx - HALF_W + 0.3 <= p.at[0] <= cx + HALF_W - 0.3):
                    out.append(f"  ! {sec['name']}: {p.name}#{p.id} at x {p.at[0]:.2f} is off the arena's held screen (x {cx - HALF_W:.2f}..{cx + HALF_W:.2f})")
            continue
        ax = g.AXIS[sec['dir']]
        sec_lead = sec['lead'] if sec['lead'] >= 0 else lead
        end_along = sec['end'][0] * ax[0] + sec['end'][1] * ax[1]
        def along(p):
            cx = min(max((sec['end'][0]), p.box[0] + R), p.box[2] - R) if ax[0] != 0 else (p.box[0] + p.box[2]) / 2
            return cx * ax[0] + (p.top + R) * ax[1]
        enders = [p for p in reached.values() if p in inside and not getattr(p, 'carrier', False) and along(p) + sec_lead >= end_along - 0.05]
        if not enders and sec['auto'] <= 0 and i < len(sections) - 1:
            best = max((round(along(p), 2) for p in reached.values() if p in inside and not getattr(p, 'carrier', False)), default=None)
            out.append(f"  ! {sec['name']}: can't end: the hero needs {end_along - sec_lead:.2f} along {sec['dir']}, best reachable foothold gives {best}")
    for p in feet_all:
        if id(p) not in reached and getattr(p, 'liftOf', None) is None:
            why = []
            for a in reached.values():
                ok, w = reach(a, p, bot=False)
                if ok:
                    why.append(f"a player can from {a} ({w})")
                    break
            best = min(reached.values(), key=lambda a: gap(a.box, p.box) + abs(p.top - a.top) + (0.5 if getattr(a, 'carrier', False) else 0)) if reached else None
            near = f"; nearest {best}: {reach(best, p, True)[1]}" if best else ""
            out.append(f"  ! unreachable for the bot: {p}{near}{'; ' + why[0] if why else ''}")
    # Lava over footholds and jumps.
    for lv in lavas:
        b = lv.box
        if lv.has('geyser'):
            a = g.ability(lv.e, 'geyser')
            hw = a.get('width', 0.9) / 2
            b = (lv.at[0] - hw, lv.at[1], lv.at[0] + hw, lv.at[1] + a.get('height', 3.2))
        for p in feet_all:
            if getattr(p, 'carrier', False):
                continue
            stand = (p.box[0], p.top, p.box[2], p.top + 1.0)
            if b[0] < stand[2] - 0.05 and b[2] > stand[0] + 0.05 and b[1] < stand[3] and b[3] > stand[1] + 0.05:
                kind = 'timed ' if (lv.has('geyser') or lv.has('lavaFall')) else ''
                out.append(f"  {'!' if not kind else '~'} {kind}lava {lv.name}{('#' + lv.id) if lv.id else ''} [x {b[0]:.2f}..{b[2]:.2f}, y {b[1]:.2f}..{b[3]:.2f}] over {p}")
    # Rising lava: the highest it gets.
    lmax = max([0] + [st.get('value', 0) for r in script.get('rules', []) for st in walk(r) if st.get('type') == 'setCounter' and st.get('name') == 'lavaMax'])
    sea = next((p for s in sections for p in s['pieces'] if p.name == 'lavaSea'), None)
    if sea and lmax:
        top = sea.top + lmax
        under = [p for p in feet_all if not getattr(p, 'carrier', False) and p.top < top + 0.05 and p.top > top - 3 and not p.has('sink')]
        out.append(f"  i lava rises to y {top:.2f}" + (f"; solid footholds under it: {under}" if under else ""))
    # The floor number carved into the landing's face must be on screen when he lands (the landing section's camera).
    if landing is not None:
        carved = g.ability(landing.e, 'carved')
        land_sec = sections[landing.sec]
        if carved and land_sec['dir'] in ('Right', 'Left'):
            text_y = landing.at[1] + carved.get('above', 0)
            low = land_sec['start'][1] - HALF_H + carved.get('size', 0.6) * 0.6
            if text_y < low:
                out.append(f"  ! landing: its carved number (y {text_y:.2f}) is below the screen's bottom (camera y "
                           f"{land_sec['start'][1]:.2f}): shorten the climb before it")
    if landing is None:
        out.append("  ! no landing")
    elif id(landing) not in reached:
        out.append("  ! the landing is unreachable for the bot")
    if verbose:
        for p in feet_all:
            via = getattr(p, 'via', None)
            out.append(f"    {p} <- {via[0] if via else '-'} {via[1] if via else ''}")
    return out

def walk(node):
    if isinstance(node, dict):
        yield node
        for v in node.values():
            yield from walk(v)
    elif isinstance(node, list):
        for v in node:
            yield from walk(v)

if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('-')]
    verbose = '-v' in sys.argv
    names = args or [f"Floor{n}" for n in list(range(999, 990, -1)) + list(range(960, 950, -1)) + list(range(910, 901, -1))]
    bad = 0
    for n in names:
        lines = check(n, verbose)
        print(f"{n}: {'ok' if not any(l.startswith('  !') for l in lines) else 'PROBLEMS'}")
        for l in lines:
            print(l)
        bad += any(l.startswith('  !') for l in lines)
    sys.exit(1 if bad else 0)
