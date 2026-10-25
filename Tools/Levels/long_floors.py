#!/usr/bin/env python3
"""
Circle X's long bullet-hell floors: Floors 997-992 and 960-952 (Docs/LevelDesign.md "A long bullet-hell floor", Floor 998 is the
reference). Each floor keeps its own opening (the path the floor had as a 90 s stage: its idea, its new thing, its lines) and
gets a riser plus five set pieces after it, each a section and a phase with its own banner, then the landing gated on the
`finale` counter (a fast hero waits for the end).

  python3 Tools/Levels/long_floors.py [997 996 ...]     (no args: every floor below)
  python3 Tools/Levels/floor_check.py Floor997 ...      (footholds, reach, lava over footholds)

The opening is read from git (BASE) so the script can be run again after editing the tables: the files it writes are plain data and
what the game reads. The boss floors (991, 951) and Floors 1000/999/910 and below are not touched.
"""
import json, math, os, subprocess, sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
LEV = os.path.join(ROOT, 'Assets', 'GameConfig', 'levels')
ENT = os.path.join(ROOT, 'Assets', 'GameConfig', 'content', 'entities')
BASE = '4bfdbcdb'          # the commit whose Floor<n>.json files are the openings
GOLD = '#FFD27A'
FINALE_AT = 255.0          # seconds before the landing opens (Floor 998: 250)

ORDER = [997, 996, 995, 994, 993, 992, 960, 959, 958, 957, 956, 955, 954, 953, 952]
PITCH = 4.0
LAYOUTS = ['line', 'zig', 'zig', 'wave', 'zig']


def original(n):
    txt = subprocess.check_output(['git', 'show', f'{BASE}:Assets/GameConfig/levels/Floor{n}.json'], cwd=ROOT).decode('utf-8')
    return json.loads(txt)


def dump(path, data):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write('\n')


AXIS = {'Up': (0, 1), 'Right': (1, 0), 'Left': (-1, 0), 'UpRight': (0.7071, 0.7071), 'UpLeft': (-0.7071, 0.7071), 'Arena': (0, 0)}


def end_of(sections):
    x = y = 0.0
    for s in sections:
        ax = AXIS[s['direction']]
        L = 0 if s['direction'] == 'Arena' else s['length']
        x += ax[0] * L
        y += ax[1] * L
    return x, y


def sp(entity, x, y, scale=None, id='', onEnter=False, facing=None):
    d = {'entity': entity, 'at': [round(x, 3), round(y, 3)], 'anchor': 'Section'}
    if id:
        d['id'] = id
    if onEnter:
        d['onEnter'] = True
    if scale is not None and scale != 1.0:
        d['scale'] = [scale, 1]
    if facing is not None:
        d['facing'] = facing
    return d


# ------------------------------------------------------------------ hazards by strength

def hz_rain(s, proj='fireball'):
    count = 1 if s < 3.8 else 2
    return {'type': 'startHazard', 'id': 'rain', 'projectile': proj, 'pattern': {'type': 'rain', 'count': count},
            'speed': 3.2, 'interval': round(max(1.0, 2.1 - 0.2 * s), 2), 'initialDelay': 1.5, 'rampPerSecond': 0.01}


def hz_drip(s):
    return {'type': 'startHazard', 'id': 'drip', 'projectile': 'slagDrip', 'pattern': {'type': 'rain', 'count': 1 if s < 3.0 else 2, 'topInset': 0.25},
            'speed': 0.0, 'interval': round(max(0.9, 1.9 - 0.18 * s), 2), 'initialDelay': 1.5}


def hz_aimed(s):
    count = 3 if s < 4.3 else 4
    return {'type': 'startHazard', 'id': 'aimed', 'projectile': 'fireball',
            'pattern': {'type': 'spread', 'count': count, 'angle': 34 if count == 3 else 40},
            'aim': 'Player', 'origin': [8.0, 0.5], 'anchor': 'Camera', 'speed': 3.0 if s < 4.0 else 3.2,
            'interval': round(max(2.2, 3.8 - 0.3 * s), 2), 'initialDelay': 2.0}


def wave(entity, count, every, start, area, tag, interval=0.8, name=''):
    return {'name': name, 'when': {'type': 'every', 'seconds': round(every, 2), 'startAfter': start, 'maxTimes': 0},
            'do': [{'type': 'spawnWave', 'entity': entity, 'count': count, 'interval': interval, 'anchor': 'Camera',
                    'areaCenter': area[0], 'areaSize': area[1], 'tags': [tag]}]}


TOP = ([0.0, 2.2], [14.0, 1.5])
LOW = ([0.0, -2.5], [14.0, 0.0])


def src_C(s, piece, flavor):
    ent = flavor.get('cinder', 'cinder')
    return {'rules': [wave(ent, 2 if s < 3.6 else 3, max(5.0, 9.0 - 0.7 * s), 2.0, TOP, 'wave', name=f'{piece}: cinders')]}


def src_R(s, piece, flavor):
    return {'hazards': [hz_drip(s) if flavor.get('drip') else hz_rain(s)]}


def src_S(s, piece, flavor):
    return {'rules': [wave('cinderSpark', 4 if s < 4.0 else 5, max(6.0, 12.0 - s), 3.5, LOW, 'wave', 0.25, name=f'{piece}: sparks from the lava')]}


def src_A(s, piece, flavor):
    return {'hazards': [hz_aimed(s)]}


def src_B(s, piece, flavor):
    return {'rules': [wave('cinderBrute', 1, max(11.0, 21.0 - 1.6 * s), 5.0, TOP, 'brute', name=f'{piece}: a brute for the fists')]}


SRC = {'C': src_C, 'R': src_R, 'S': src_S, 'A': src_A, 'B': src_B}
ORDERS = ['CRSAB', 'CSRBA', 'CARSB', 'CRBSA']

# ------------------------------------------------------------------ the path

def climb(L, foot_top, foot_x, deco=None, plat=('ledge', 0.9), step=1.35):
    """A climb section (Up, L long) from a hero standing on a platform whose top is foot_top at x foot_x (both relative to the
    section start). Ledges zig between x 0 and x 2.4, ending on a foot ledge at (0, L-1.2) like Floor 998's jam. `plat` is one
    (entity, scale) or a list that alternates."""
    ftop = L - 1.2 + 0.25
    n = max(1, math.ceil((ftop - foot_top) / step))
    out = []
    for k in range(1, n + 1):
        y = foot_top + (ftop - foot_top) * k / n
        x = 0.0 if (n - k) % 2 == 0 else (2.4 if foot_x > 1.2 else -2.4)
        last = k == n
        if last:
            ent, scale = 'ledge', 1.2
        else:
            ent, scale = plat[k % len(plat)] if isinstance(plat, list) else plat
        out.append(sp(ent, x, y - 0.25, scale))
        if deco:
            out.extend(deco(k, n, x, y - 0.25, last))
    return out


def run_row(n, ents, pitch, layout, deco, landing=None, final_y=-1.2):
    """A Right section's footholds: n platforms at pitch, the last one the foot at (n*pitch, final_y) (the next section's x 0)."""
    heights = {'line': [0.0], 'low': [0.0, 0.25], 'zig': [0.0, 0.6], 'wave': [0.0, 0.5, 1.0, 0.5], 'stair': [0.0, 0.4, 0.8, 0.4]}[layout]
    out = []
    for k in range(1, n + 1):
        x = pitch * k
        y = -1.2 + heights[(k - 1) % len(heights)]
        if k == n and landing is None:
            y = final_y
        r = ents(k, n)
        if r:
            out.append(sp(r[0], x, y, r[1]))
        if deco:
            out.extend(deco(k, n, x, y))
    return out


def estimate(srcs, s):
    """A crude count of enemies and shots alive: the rain, fans, cinders, sparks and brutes of a piece."""
    tot = 0.0
    for c in srcs:
        if c == 'C': tot += (2 if s < 3.6 else 3) * 6.0 / max(5.0, 9.0 - 0.7 * s)
        if c == 'R': tot += (1 if s < 3.8 else 2) * 1.5 / max(1.0, 2.1 - 0.2 * s)
        if c == 'S': tot += (4 if s < 4.0 else 5) * 2.0 / max(6.0, 12.0 - s)
        if c == 'A': tot += (3 if s < 4.3 else 4) * 3.0 / max(2.2, 3.8 - 0.3 * s)
        if c == 'B': tot += 10.0 / max(11.0, 21.0 - 1.6 * s)
    return tot




# ------------------------------------------------------------------ foothold choices

def cycle(seq, foot=1.2):
    """The footholds of a run, by place in the row: seq[k % len(seq)]; the last one (the foot the next section starts from) is
    always a ledge."""
    def per_piece(i):
        def f(k, n):
            if k == n:
                return 'ledge', foot
            return seq[k % len(seq)]
        return f
    return per_piece


def hover(entity, xs, y, scale=None, onEnter=True):
    return [sp(entity, x, y, scale, onEnter=onEnter) for x in xs]


def make_pairs():
    """Counterweight pairs for Floor 960 (Floor 960's own are A, B and C): each pair names its partner."""
    base = json.load(open(os.path.join(ENT, 'counterweightAL.json'), encoding='utf-8'))
    for j in range(1, 4):
        for side, other, parent in (('L', 'R', 'counterweightLeft'), ('R', 'L', 'counterweightRight')):
            d = json.loads(json.dumps(base))
            d['extends'] = parent
            d['abilities'][0]['partner'] = f'cwP{j}{other}'
            dump(os.path.join(ENT, f'counterweightP{j}{side}.json'), d)


# ------------------------------------------------------------------ the units a floor repeats

def default_run(spec, i, last):
    ents = spec['ents'](i)
    deco = spec.get('deco')
    dd = (lambda k, n, x, y: deco(i, k, n, x, y)) if deco else None
    layout = spec.get('layouts', LAYOUTS)[i]
    if last:
        out = []
        hs = {'line': [0.0], 'low': [0.0, 0.25]}.get(layout, [0.0, 0.6])
        for k in range(1, 7):
            x = 4.0 * k
            y = -1.2 + hs[(k - 1) % len(hs)]
            ent, scale = ents(k, 6) or ('ledge', 1.2)
            out.append(sp(ent, x, y, scale))
            if dd:
                out.extend(dd(k, 6, x, y))
        return out, 30.0
    pitch = spec.get('pitch', PITCH)
    n = spec.get('runN', 7)
    return run_row(n, ents, pitch, layout, dd), pitch * n


def default_climb(spec, i):
    cdeco = spec.get('cdeco')
    L = spec.get('climbL', 8.0)
    return climb(L, -0.95, 0.0, cdeco and (lambda kk, nn, x, y, lastf: cdeco(i, kk, nn, x, y, lastf)), spec.get('cplat', ('ledge', 0.9))), L


def lift_climb(entity, travel, L, cutters=0):
    """A chain lift up a shaft: the lift beside the foot, the next foot at the top (Floor 994's lifts, Floor 956's)."""
    def f(spec, i):
        out = [sp(entity, 2.8, -1.2, None), sp('ledge', 0.0, L - 1.2, 1.2)]
        if cutters:
            out.extend(hover('impCutter', [-2.2, 3.8][:1 + (i >= 3)], L - 2.4))
        return out, L
    return f


def run_pairs(pair, deco=None):
    """Floor 960's pulley row: ledge, a counterweight pair (left rises as right drops), ledge ... the foot at 28."""
    def f(spec, i, last):
        layout_y = -1.2
        out = []
        if last:
            return default_run(spec, i, True)
        out.append(sp('ledge', 4.0, layout_y, 1.2))
        out.append(sp(f'counterweightP{pair}L', 6.3, layout_y + 0.0, 0.8, id=f'cwP{pair}L'))
        out.append(sp(f'counterweightP{pair}R', 8.5, layout_y + 0.5, 0.8, id=f'cwP{pair}R'))
        for x in (10.8, 14.4, 18.0, 21.6, 25.0, 28.0):
            out.append(sp('ledge', x, layout_y + (0.4 if x in (14.4, 21.6) else 0.0), 1.2))
        if deco:
            out.extend(deco(i))
        return out, 28.0
    return f


def run_drafts(spec, i, last):
    """Floor 957's hot-air row: ledge, a column of hot air, a ledge a step higher, a hop down ..."""
    if last:
        return default_run(spec, i, True)
    out = []
    y0 = -1.2
    for u in (0.0, 11.4):
        out.append(sp('ledge', 3.6 + u, y0, 0.9))
        out.append(sp('heatUpdraft', 6.0 + u, y0 + 0.15))
        out.append(sp('ledge', 8.4 + u, y0 + 1.0, 0.9))
        out.append(sp('ledge', 11.8 + u, y0, 1.2))
    out.append(sp('ledge', 25.6, y0, 1.2))
    out.append(sp('ledge', 28.0, y0, 1.2))
    return out, 28.0


def climb_chimney(spec, i):
    """Floor 957's chimney: a tall column of hot air beside the foot, a ledge where it throws him."""
    L = 6.6
    out = [sp('heatUpdraftTall', 2.4, 1.8), sp('ledge', 0.0, L - 1.2, 1.2)]
    out.append(sp('crumblingStone', -2.4, 2.6, 0.8))
    return out, L


def run_laps(spec, i, last):
    """Floor 995's laps: the same five stones, the rest with its geyser, the carving: again and again (Floor 995's lap)."""
    def lap(u):
        o = [sp('loopStone1', u + 0.4, -2.3, 0.9), sp('loopStone2', u + 2.6, -2.0, 0.9), sp('loopStone3', u + 4.8, -2.3, 0.9),
             sp('loopStone4', u + 7.0, -2.0, 0.9), sp('ledge', u + 9.6, -2.2, 1.4), sp('loopGeyser', u + 8.9, -1.95),
             sp('loopStone5', u + 12.4, -2.0, 0.9), sp('loopCarving', u + 14.6, -2.2, 1.4)]
        return o
    if last:
        out = lap(0.0) + [sp('ledge', 18.0, -1.2, 1.2), sp('ledge', 21.0, -0.9, 1.2), sp('ledge', 24.0, -0.6, 1.2)]
        return out, 30.0
    out = lap(0.0) + lap(17.0) + [sp('ledge', 34.0, -1.2, 1.2)]
    return out, 34.0


def run_crumble(spec, i, last):
    """Floor 992's bridge: a span of crumbling stones, a solid ledge now and then; the middle pieces have a gap with a chain."""
    if last:
        return default_run(spec, i, True)
    out = []
    gap = i in (1, 3)
    for k in range(1, 14):
        if gap and k in (9, 10):
            continue
        x = 2.0 * k
        if k % 5 == 0:
            out.append(sp('ledge', x, -1.2, 0.8))
        else:
            out.append(sp('crumblingStone', x, -1.2, 0.8))
    out.append(sp('ledge', 28.0, -1.2, 1.2))
    if gap:
        out.append(sp('swingChain', 19.0, -1.2 + 0.25 + 2.0))
    return out, 28.0


def run_shelves(spec, i, last):
    """Floor 955's shelves: a bank of ledges, the lava falls pouring on them in turn (a hop between the curtains)."""
    if last:
        out, L = default_run(spec, i, True)
        for k, ent in ((2, 'lavaFall955a'), (4, 'lavaFall955c')):
            out.append(sp(ent, 4.0 * k - 0.9, -1.2 + 1.6))
        return out, L
    out = []
    n = 15
    for k in range(1, n + 1):
        ent, scale = ('ledge', 0.9) if k < n else ('ledge', 1.2)
        out.append(sp(ent, 1.9 * k - 0.2 if k < n else 28.0, -1.2, scale))
    falls = ['lavaFall955a', 'lavaFall955b', 'lavaFall955c']
    count = min(7, 3 + i)
    xs = [5.2 + 3.4 * j for j in range(count)]
    for j, x in enumerate(xs):
        out.append(sp(falls[j % 3], x, -1.2 + 1.6))
    return out, 28.0


# ------------------------------------------------------------------ the decorations of each floor

def swing_deco(i, k, n, x, y):
    """997 and 960: sack imps on the hauling chains swing over the gaps (every second gap early, every gap late)."""
    every = 2 if i < 3 else 1
    if k % every == 0 and k != n:
        return [sp('impSwingB' if k % 2 else 'impSwing', x + 2.0, y + 0.25 + 2.5, None, onEnter=True)]
    return []


def swing_cdeco(i, k, n, x, y, last):
    if k % 2 == 0 and not last and i >= 0:
        return [sp('impSwing' if k % 4 else 'impSwingB', x - 3.0 if x < 1.0 else x + 3.0, y + 0.25 + 2.5, None, onEnter=True)]
    return []


def geyser_deco(i, k, n, x, y):
    """996: a vent on a ledge, every second ledge early and every ledge late; the falls behind."""
    out = []
    if k != n and (k % 2 == 0 or i >= 2):
        out.append(sp(['lavaGeyser', 'lavaGeyserB', 'lavaGeyserC'][k % 3], x + (-0.6 if k % 2 else 0.6), y + 0.25))
    if k % 3 == 0:
        out.append(sp('lavaFallScenery', x, y + 4.9))
    return out


def geyser_cdeco(i, k, n, x, y, last):
    if k % 2 == 0 and not last:
        return [sp(['lavaGeyser', 'lavaGeyserB', 'lavaGeyserC'][k % 3], x - 0.55, y + 0.25)]
    return []


PERCH = {0: (3,), 1: (3, 6), 2: (2, 5), 3: (2, 4, 6), 4: (2, 4, 5)}


def perch_deco(i, k, n, x, y):
    """993: the throwers' perches: imps above the row (one early, three late)."""
    if k in PERCH[i] and k != n:
        return [sp('imp', x, y + 0.25 + 2.6, None, onEnter=True)]
    return []


def perch_cdeco(i, k, n, x, y, last):
    if k == 3 and i >= 1 and not last:
        return [sp('imp', x + (3.0 if x < 1.0 else -3.0), y + 0.25 + 2.8, None, onEnter=True)]
    return []


def chain_deco(i, k, n, x, y):
    """994: where a ledge is missing, a swing chain hangs over the gap (every fourth, the cross)."""
    if k % 4 == 0 and k != n:
        return [sp('swingChain', x, y + 0.25 + 2.0)]
    return []


def chain_ents(i):
    def f(k, n):
        if k == n:
            return 'ledge', 1.2
        if k % 4 == 0:
            return None
        return ('sinkingStone', 0.9) if k % 3 == 0 else ('ledge', 1.2)
    return f


def guard_extra(counts):
    def f(i, kind, last):
        c = counts[i]
        xs = {1: [14.0], 2: [10.0, 20.0], 3: [8.0, 16.0, 24.0]}[c] if kind == 'run' else {1: [0.0], 2: [-2.6, 3.0], 3: [-2.6, 3.0, 0.0]}[c]
        y = 3.1 if kind == 'run' else 3.4
        return hover('impGuard', xs[:3], y)
    return f


def imp_extra(counts):
    def f(i, kind, last):
        c = counts[i]
        if not c:
            return []
        xs = {1: [14.0], 2: [10.0, 20.0]}[c] if kind == 'run' else {1: [-2.6], 2: [-2.6, 3.0]}[c]
        return hover('imp', xs, 3.0 if kind == 'run' else 3.4)
    return f


def loop_cdeco(i, k, n, x, y, last):
    """995 and 953: a geyser on the world's clock on a ledge of the climb."""
    if k % 2 == 0 and not last and i >= 0:
        return [sp('loopGeyser', x - 0.5, y + 0.25)]
    return []


def lap_deco(i, k, n, x, y):
    """953: a geyser on the clock of the whole floor (worldClock) on most ledges; the lava falls between."""
    out = []
    if k != n and k % 2 == 0:
        out.append(sp('loopGeyser', x + 0.5, y + 0.25))
    if k % 3 == 1 and k != 1:
        out.append(sp('lavaFall955a', x + 2.0, y + 1.6))
    return out


# ------------------------------------------------------------------ the floors

SPECS = {}

SPECS[997] = dict(
    keep=['ledges', 'gap', 'gap2', 'wall', 'higher'], last=(5.6, 10.85), landing='floorLanding997',
    kinds=['run', 'climb', 'run', 'run', 'run'],
    pieces=[('sackRoute', 'THE SACK ROUTE!'), ('highHaul', 'THE HIGH HAUL!'), ('diamondShower', 'DIAMOND SHOWER!'),
            ('chainsEverywhere', 'CHAINS EVERYWHERE!'), ('liftStop', 'THE LIFT STOP!')],
    ents=cycle([('ledge', 1.2)]), deco=swing_deco, cdeco=swing_cdeco,
)

SPECS[996] = dict(
    keep=['foot', 'run', 'beside'], last=(28.6, 8.55), landing='floorLanding996',
    kinds=['run', 'climb', 'run', 'climb', 'run'],
    pieces=[('ventRow', 'THE VENT ROW!'), ('upTheFalls', 'UP THE FALLS!'), ('sneezingFit', 'A SNEEZING FIT!'),
            ('orangeEverything', 'EVERYTHING IS ORANGE!'), ('theSpout', 'MIND THE SPOUT!')],
    ents=cycle([('ledge', 1.6)], foot=1.6), deco=geyser_deco, cdeco=geyser_cdeco, cplat=('ledge', 1.2), pitch=4.4, runN=6,
)

SPECS[995] = dict(
    layouts=['line', 'low', 'low', 'low', 'low'],
    keep=['lap1', 'lap2', 'wall'], last=(36.4, 5.85), landing='floorLanding995',
    kinds=['run', 'run', 'run', 'climb', 'run'],
    pieces=[('lapThree', 'LAP THREE!'), ('sameRock', 'STILL THE SAME ROCK!'), ('lapFive', 'LAP FIVE. AGAIN.'),
            ('loopGoesUp', 'THE LOOP GOES UP!'), ('lastLap', 'THE LAST LAP. MAYBE.')],
    ents=cycle([('ledge', 1.2), ('sinkingStone', 1.2), ('ledge', 1.2)]),
    runfns={0: run_laps, 2: run_laps, 4: run_laps}, cdeco=loop_cdeco, cplat=('ledge', 0.9),
)

SPECS[994] = dict(
    layouts=['line', 'low', 'low', 'low', 'low'],
    keep=['run', 'lift1', 'cross', 'lift2', 'landing'], last=(23.2, 11.3), landing='floorLanding994', fix_landing=('ledge', 2.0),
    kinds=['run', 'climb', 'run', 'climb', 'run'],
    pieces=[('theLongHaul', 'THE LONG HAUL!'), ('liftAndAnother', 'LIFT, AND ANOTHER!'), ('cutChains', 'CUT CHAINS!'),
            ('highShaft', 'THE HIGH SHAFT!'), ('keepsGoing', 'THE LIFT THAT KEEPS GOING!')],
    ents=chain_ents, deco=chain_deco, pitch=3.6, runN=8,
    climbfn=lift_climb('chainLift994a', 6.0, 6.0, cutters=1),
)

SPECS[993] = dict(
    layouts=['line', 'low', 'low', 'low', 'low'],
    keep=['ledges', 'slope', 'hollows', 'lift', 'landing'], last=(8.2, 13.15), landing='floorLanding993', fix_landing=('ledge', 2.0),
    kinds=['run', 'run', 'climb', 'run', 'run'],
    pieces=[('throwersLedge', "THE THROWERS' LEDGE!"), ('embersFromAbove', 'EMBERS FROM ABOVE!'), ('twoOnALedge', 'TWO ON A LEDGE!'),
            ('emberStairs', 'THE EMBER STAIRS!'), ('everyoneThrows', 'EVERYONE THROWS!')],
    ents=cycle([('ledge', 1.2), ('ledge', 1.2), ('sinkingStone', 1.2)]), deco=perch_deco, cdeco=perch_cdeco,
)

SPECS[992] = dict(
    keep=['wall', 'bridge', 'far'], last=(22.4, 10.45), landing='floorLanding992',
    kinds=['run', 'climb', 'run', 'run', 'run'],
    pieces=[('nothingHolds', 'NOTHING HOLDS!'), ('theWallCracks', 'THE WALL CRACKS!'), ('theLongBridge', 'THE LONG BRIDGE!'),
            ('stepFallRepeat', 'STEP, FALL, REPEAT!'), ('theLastSpan', 'THE LAST SPAN!')],
    ents=cycle([('crumblingStone', 1.2), ('ledge', 1.2), ('crumblingStone', 1.2)]), auto=0.9,
    runfns={0: run_crumble, 1: run_crumble, 2: run_crumble, 3: run_crumble}, cplat=[('crumblingStone', 0.9), ('ledge', 0.9)],
    extra=imp_extra([0, 0, 1, 1, 2]),
)

SPECS[960] = dict(
    keep=['stones', 'pulleys', 'lastPair'], last=(10.2, 7.1), landing='floorLanding960',
    kinds=['run', 'run', 'climb', 'run', 'run'],
    pieces=[('pulleyParade', 'PULLEY PARADE!'), ('heavyDiamonds', 'HEAVY DIAMONDS!'), ('upTheDepot', 'UP THE DEPOT!'),
            ('swingingSacks', 'SWINGING SACKS!'), ('lastPulley', 'THE LAST PULLEY!')],
    ents=cycle([('ledge', 1.2)]), deco=swing_deco, cdeco=swing_cdeco,
    runfns={0: run_pairs(1), 1: run_pairs(2), 3: run_pairs(3)},
)

SPECS[959] = dict(
    keep=['bank', 'river', 'steps', 'signRide'], last=(0.4, 4.3), landing='floorLanding959',
    kinds=['run', 'run', 'climb', 'run', 'run'],
    pieces=[('riverReturns', 'THE RIVER RETURNS!'), ('roofHopping', 'ROOF HOPPING!'), ('theBank', 'THE BANK!'),
            ('signComesBack', 'THE SIGN COMES BACK!'), ('stillFloating', 'STILL FLOATING!')],
    ents=cycle([('debrisRaftRoof', 1.2), ('debrisRaft', 1.2), ('debrisRaftHay', 1.2), ('ledge', 1.2)]), auto=0.8,
    cplat=('ledge', 1.2),
)

SPECS[958] = dict(
    layouts=['line', 'low', 'low', 'low', 'low'],
    keep=['slope', 'ride', 'bank'], last=(14.2, 6.5), landing='floorLanding958', flavor={'cinder': 'cinderThatch'},
    kinds=['run', 'climb', 'run', 'climb', 'run'],
    pieces=[('somebodysRoof', "SOMEBODY'S ROOF!"), ('hayBusters', 'HAY BUSTERS!'), ('thatchClimb', 'THATCH CLIMB!'),
            ('sparkShower', 'SPARK SHOWER!'), ('lastRoof', 'THE LAST ROOF!')],
    ents=cycle([('ledge', 1.2), ('debrisRaftHay', 1.2), ('sinkingStone', 1.2)]), auto=0.85, cplat=('ledge', 1.2),
)

SPECS[957] = dict(
    keep=['ledges', 'columns', 'chimney', 'landing'], last=(10.4, 9.0), landing='floorLanding957',
    kinds=['run', 'climb', 'run', 'climb', 'run'],
    pieces=[('upUpUp', 'UP, UP, UP!'), ('theChimney', 'THE CHIMNEY!'), ('hotAirHopping', 'HOT AIR HOPPING!'),
            ('chimneyAgain', 'ANOTHER CHIMNEY!'), ('theLongDraft', 'THE LONG DRAFT!')],
    ents=cycle([('ledge', 1.2)]),
    runfns={0: run_drafts, 2: run_drafts}, climbfn=climb_chimney, extra=imp_extra([0, 0, 1, 1, 2]),
)

SPECS[956] = dict(
    layouts=['line', 'low', 'low', 'low', 'low'],
    keep=['lift', 'ceiling', 'ledges'], last=(7.6, 5.1), landing='floorLanding956', flavor={'drip': True},
    kinds=['run', 'climb', 'run', 'run', 'run'],
    pieces=[('firstDrips', 'THE FIRST DRIPS!'), ('liftShaft', 'THE LIFT SHAFT!'), ('ceilingRun', 'CEILING RUN!'),
            ('dripDrop', 'DRIP DROP!'), ('theBigDrip', 'THE BIG DRIP!')],
    ents=cycle([('ledge', 1.2), ('sinkingStone', 1.2), ('sinkingStone', 1.2)]),
    climbfn=lift_climb('chainLift', 3.0, 4.0),
)

SPECS[955] = dict(
    keep=['falls', 'behind'], last=(7.4, 1.7), landing='floorLanding955',
    kinds=['run', 'run', 'climb', 'run', 'run'],
    pieces=[('theCurtain', 'THE CURTAIN!'), ('waitForIt', 'WAIT FOR IT!'), ('behindTheFalls', 'BEHIND THE FALLS!'),
            ('threeAtOnce', 'THREE FALLS AT ONCE!'), ('slippingThrough', 'SLIPPING THROUGH!')],
    ents=cycle([('ledge', 1.2)]),
    runfns={0: run_shelves, 1: run_shelves, 3: run_shelves, 4: run_shelves},
)

SPECS[954] = dict(
    layouts=['line', 'low', 'low', 'low', 'low'],
    keep=['weights', 'post', 'guardPost', 'border'], last=(9.8, 1.8), landing='floorLanding954',
    kinds=['run', 'climb', 'run', 'run', 'run'],
    pieces=[('borderPatrol', 'BORDER PATROL!'), ('guardsOnGuard', 'GUARDS ON GUARD!'), ('notCarrying', 'NOT CARRYING ANYTHING!'),
            ('helmetLedge', 'THE HELMET LEDGE!'), ('wholePost', 'THE WHOLE POST!')],
    ents=cycle([('ledge', 1.2), ('sinkingStone', 1.2), ('ledge', 1.2)]), extra=guard_extra([1, 1, 2, 2, 3]),
)

SPECS[953] = dict(
    keep=['lap1up', 'lap1run', 'lap1top', 'lap2up', 'lap2run'], last=(18.2, 6.2), landing='floorLanding953b',
    kinds=['run', 'run', 'climb', 'run', 'run'],
    pieces=[('againAgain', 'AGAIN, AGAIN!'), ('sameVent', 'SAME VENT, NEW LAP!'), ('lapSeven', 'LAP... SEVEN?'),
            ('geysersKnowYou', 'THE GEYSERS KNOW YOU!'), ('noMoreLaps', 'NO MORE LAPS. REALLY.')],
    ents=cycle([('ledge', 1.2)]), deco=lap_deco, cdeco=loop_cdeco, cplat=('ledge', 1.2),
)

SPECS[952] = dict(
    keep=['foot', 'raft', 'pileSide', 'line', 'top'], last=(7.0, 4.9), landing='floorLanding952',
    kinds=['run', 'climb', 'run', 'run', 'run'],
    pieces=[('pilesEdge', "THE PILE'S EDGE!"), ('pileOn', 'PILE-ON!'), ('guardsCountUp', 'GUARDS COUNT UP!'),
            ('pileSpills', 'THE PILE SPILLS!'), ('topOfThePile', 'THE TOP OF THE PILE!')],
    ents=cycle([('debrisRaftRoof', 1.2), ('ledge', 1.2), ('debrisRaft', 1.2)]), auto=0.8, extra=guard_extra([1, 1, 2, 2, 3]),
)


# ------------------------------------------------------------------ assembling a floor

def old_ceiling(kept, ex, ey):
    """The highest thing the opening left within reach of the new rows (x from ex - 6 on): the riser must lift the rows above it."""
    top = ey
    for s in kept:
        for q in s['spawns']:
            if q['entity'] == 'lavaSea' or q.get('anchor', 'Path') != 'Path':
                continue
            if q['at'][0] >= ex - 6.0:
                top = max(top, q['at'][1] + 1.5)
    return top


def walk(node):
    if isinstance(node, dict):
        yield node
        for v in node.values():
            yield from walk(v)
    elif isinstance(node, list):
        for v in node:
            yield from walk(v)


def build(n, report):
    spec = SPECS[n]
    d = original(n)
    script = d['script']
    path = script['world']['path']
    kept = [p for p in path if p['name'] in spec['keep']]
    assert len(kept) == len(spec['keep']), (n, [p['name'] for p in path])
    # the old landing goes (the new one ends the floor); a floor whose last foothold was its landing keeps a ledge there
    for s in kept:
        fixed = []
        for q in s['spawns']:
            if q.get('id') == 'landing':
                if spec.get('fix_landing'):
                    ent, scale = spec['fix_landing']
                    fixed.append({'entity': ent, 'at': q['at'], 'scale': [scale, 1]})
                continue
            fixed.append(q)
        s['spawns'] = fixed
    ex, ey = end_of(kept)
    tier = ORDER.index(n)
    flavor = spec.get('flavor', {})

    # the riser: lifts the rows clear of the opening
    lx, ltop = spec['last']
    ceiling = old_ceiling(kept, ex, ey)
    L = 5.6
    while ey + L - 1.2 < ceiling + 1.0:
        L += 0.4
    L = round(L, 1)
    riser = {'name': 'riser', 'direction': 'Up', 'length': L, 'turn': 2.0, 'lead': 0.4, 'autoSpeed': 0.36,
             'spawns': climb(L, ltop - ey, lx - ex, spec.get('cdeco') and (lambda kk, nn, x, y, lastf: spec['cdeco'](-1, kk, nn, x, y, lastf)),
                             spec.get('cplat', ('ledge', 0.9)))}
    sections = kept + [riser]

    # strengths: the first piece's s rises a little with the tier, each piece adds a step
    s0 = 1.0 + 0.09 * tier
    step = 0.9 - 0.012 * tier
    boost = 0 if tier < 5 else 1 if tier < 10 else 2
    order = ORDERS[tier % 4]
    names = spec['pieces']
    phases = list(script['phases'])
    rules = list(script['rules'])
    new_rules = []
    est = []
    for i in range(5):
        kind = spec['kinds'][i]
        pname, banner = names[i]
        last = i == 4
        s = s0 + i * step
        srcs = order[:min(5, i + 1 + boost)]
        est.append((pname, round(estimate(srcs, s), 1)))
        if kind == 'run':
            fn = spec.get('runfns', {}).get(i)
            if fn:
                spawns, length = fn(spec, i, last)
            else:
                spawns, length = default_run(spec, i, last)
            if last:
                assert length == 30.0
                spawns.append(sp(spec['landing'], 29.4, -0.4, None, 'landing'))
            sec = {'name': pname, 'direction': 'Right', 'length': length, 'turn': 2.0, 'autoSpeed': spec.get('auto', 0.55), 'spawns': spawns}
        else:
            fn = spec.get('climbfn') or default_climb
            spawns, length = fn(spec, i)
            sec = {'name': pname, 'direction': 'Up', 'length': length, 'turn': 2.0, 'lead': 0.4, 'autoSpeed': 0.36, 'spawns': spawns}
        if spec.get('extra'):
            sec['spawns'].extend(spec['extra'](i, kind, last))
        sections.append(sec)
        new_rules.append({'name': f'to {pname}', 'when': {'type': 'sectionReached', 'section': pname}, 'maxTimes': 1,
                          'do': [{'type': 'goToPhase', 'phase': pname}]})
        on_enter = [{'type': 'emit', 'signal': 'setpiece:' + pname},
                    {'type': 'showText', 'text': banner, 'duration': 2.4, 'style': 'Banner', 'color': GOLD}]
        prules = []
        for c in srcs:
            r = SRC[c](s, pname, flavor)
            on_enter.extend(r.get('hazards', []))
            prules.extend(r.get('rules', []))
        phases.append({'name': pname, 'onEnter': on_enter, 'rules': prules})

    script['world']['path'] = sections
    script['phases'] = phases

    # rules that waited for a section that is gone now wait for the last piece (994's launcher comes back at the end)
    have_sections = {s['name'] for s in sections}
    for r in rules:
        for node in walk(r.get('when')):
            if node.get('type') == 'sectionReached' and node.get('section') not in have_sections:
                print(f'  Floor {n}: rule "{r["name"]}" waited for the removed section {node["section"]}: now {names[4][0]}')
                node['section'] = names[4][0]

    # the landing waits for the floor's minutes; heat and finale counters (Floor 998's)
    for r in rules:
        if r.get('name') == 'the landing':
            r['when'] = {'type': 'allOf', 'triggers': [r['when'], {'type': 'counter', 'name': 'finale', 'atLeast': 1.0}]}
    have = {c['name'] for c in script['counters']}
    for c in ('heat', 'finale'):
        if c not in have:
            script['counters'].append({'name': c, 'value': 0.0})
    new_rules.append({'name': 'the opening\'s lava stops', 'when': {'type': 'sectionReached', 'section': 'riser'}, 'maxTimes': 1,
                      'do': [{'type': 'setCounter', 'name': 'lavaSpeed', 'value': 0.0}]})
    new_rules.append({'name': 'the floor has had its minutes', 'when': {'type': 'atTime', 'seconds': FINALE_AT}, 'maxTimes': 1,
                      'do': [{'type': 'setCounter', 'name': 'finale', 'value': 1.0}]})
    new_rules.append({'name': 'heat: the floor gets hotter', 'when': {'type': 'every', 'seconds': 20.0, 'startAfter': 40.0, 'maxTimes': 12},
                      'do': [{'type': 'addCounter', 'name': 'heat', 'amount': 1.0}]})
    for h in (3, 5, 7, 9, 11):
        new_rules.append({'name': f'heat {h}: the cinders hurry', 'when': {'type': 'counter', 'name': 'heat', 'atLeast': float(h)}, 'maxTimes': 1,
                          'do': [{'type': 'modifier', 'target': {'entity': 'cinder'}, 'modifiers': [{'stat': 'MoveSpeed', 'multiply': 1.1}], 'includeFuture': True}]})
    script['rules'] = rules + new_rules
    script['length'] = 285
    d['duration'] = 400.0
    report.append((n, L, est))
    return d


def main(argv):
    wanted = [int(a) for a in argv] or ORDER
    report = []
    make_pairs()
    for n in wanted:
        d = build(n, report)
        dump(os.path.join(LEV, f'Floor{n}.json'), d)
    for n, L, est in report:
        print(n, 'riser', L, 'est alive:', ' '.join(f'{p}={e}' for p, e in est))


if __name__ == '__main__':
    main(sys.argv[1:])
