#!/usr/bin/env python3
"""
Circle IX's heat pass: the authored floors 900-892, 860-852 and 810-802 become long bullet-hell floors (~285 s), hotter than
Circle X's (Docs/LevelDesign.md "A long bullet-hell floor", point 10). The floor boss floors (891, 851, 801) are not touched.

Each floor keeps its opening (everything before its `drift`: its idea, its first beats, its lines), loses its drift and landing,
and gets a riser plus five set pieces (a run of 28 units or a climb of 8), then the landing, gated on the `finale` counter. Pieces are
built from Circle IX's roster: the Brimstone family on the road (demons, whelps, lobbers on perches, nests, vents), the trick imps
(ricochet, blink, lead) and the beam lens, the hauling route's guards and swing imps, and mixed waves of cinders, sparks and brutes.
Climbs alternate between ladders (Floors 899 and up take one a floor) and stepping slabs.

  python3 Tools/Levels/cracked_floors.py [900 899 ...]     (no args: every floor below)
  python3 Tools/Levels/floor_check.py Floor900 ...         (footholds, reach, ladders)

The openings are read from git (BASE), so the script can be run again after editing the tables: the files it writes are plain data.
"""
import json, math, os, re, subprocess, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import long_floors as lf
from long_floors import sp, dump, end_of, walk, wave, TOP, LOW, GOLD, hz_aimed, hz_drip, hz_rain

ROOT = lf.ROOT
LEV = lf.LEV
BASE = 'fe62d171'          # the commit whose Floor<n>.json files are the openings (the 90 s stages)
FINALE_AT = 255.0
ORDER = list(range(900, 891, -1)) + list(range(860, 851, -1)) + list(range(810, 801, -1))
TIER0 = 15                 # Circle X has 15 long floors; Circle IX continues the ramp where it left off
ENEMIES = {'imp', 'impGuard', 'impSwing', 'impSwingB', 'impCutter', 'brimstoneDemon', 'demonWhelp', 'demonLobber', 'whelpNest',
           'ricochetImp', 'blinkImp', 'leadImp', 'bulwarkImp', 'beamLens'}
FOOT = {'R': ('roadSlab', 1.2), 'S': ('settleSlab', 1.2), 'P': ('splitSlab', 1.2)}
VENTS = ['brimstoneVent', 'brimstoneVentB', 'brimstoneVentC', 'brimstoneVentD']
TRICKS = ['ricochetImp', 'blinkImp', 'leadImp']


def original(n):
    txt = subprocess.check_output(['git', 'show', f'{BASE}:Assets/GameConfig/levels/Floor{n}.json'], cwd=ROOT).decode('utf-8')
    return json.loads(txt)


# ------------------------------------------------------------------ the floors: foothold cycle, what stands on the road
# (D demon, W whelp, L lobber on a perch, N nest, V vent, T trick imp, G guard, X swing imp, E beam lens: the first of the list
# arrive first, the lens only from the 4th piece) and the hazards from above (C cinders, S sparks, B brutes, R rain, A aimed
# fans, M a mixed wave, H spire shards).

SPEC = {
    900: ('RSRR', 'DDWDV', 'CRAMSB', ['THE ROAD SPLITS!', 'DEMONS IN THE CRACKS!', 'KEEP TO THE ROAD!', 'THE RIFT LOOKS UP!', 'NINE HUNDRED, THE HARD WAY!']),
    899: ('PRPR', 'WDWDT', 'CSRAMB', ['SPLIT ENDS!', 'THE SEAMS GO!', "DON'T STAND STILL!", 'ROAD IN PIECES!', 'THE LAST OF THE ROAD!']),
    898: ('RSRR', 'WDLDT', 'HCHSAMB', ['SHARD FALL!', 'THE SPIRES SHED!', 'UNDER THE SPIRES!', 'A HAIL OF SHARDS!', 'THE LAST SHARD!']),
    897: ('RSRR', 'WWDWN', 'CSMRAB', ['THE WHELPS!', 'SMALL AND ANGRY!', 'BITING DISTANCE!', 'THE LITTER!', 'THE BIGGEST WHELP!']),
    896: ('RRSR', 'VVDVW', 'CRSAMB', ['THE VENTS!', 'COUGHING ROAD!', 'VENT ROW!', 'EVERYTHING BREATHES!', 'THE BIG BREATH!']),
    895: ('SRSR', 'DWVDT', 'CMSRAB', ['THE HEAVE!', 'THE ROAD SHOVES!', 'UP AND DOWN!', 'SHOVE AFTER SHOVE!', 'THE LAST HEAVE!']),
    894: ('RRSR', 'LLDLT', 'CHARMSB', ['THE LOBBERS!', 'LOOK UP!', 'LUMPS FROM ABOVE!', 'LOBBER ROW!', 'THE LAST LOB!']),
    893: ('RSRP', 'DWTDL', 'CRMASB', ['SEESAW ROAD!', 'TIPPING POINT!', 'KEEP YOUR BALANCE!', 'THE LONG TIP!', 'OFF THE SEESAW!']),
    892: ('PRPR', 'DDDWT', 'CAMRSB', ['DEMON ROW!', 'THE ROW MARCHES!', 'DEMONS ON DEMONS!', 'NOWHERE TO STAND!', 'THE END OF THE ROW!']),
    860: ('RRSR', 'NDWDT', 'CMRASB', ["THE CARVER'S YARD!", 'CARVED STONE!', 'CHISEL WORK!', 'THE YARD FILLS!', 'HIS LAST CARVING!']),
    859: ('RRSR', 'XXGDT', 'CRAMSB', ['THE HOIST!', 'UP THE CHAINS!', 'SACKS ON SWINGS!', 'HOIST AND HAUL!', 'THE LAST HOIST!']),
    858: ('PRPR', 'WDTWL', 'HCMRAS', ['THE LEAN!', 'IT TILTS!', 'TOPPLING!', 'RUN FOR IT!', 'DOWN IT GOES!']),
    857: ('RSRR', 'VVWDG', 'CRSMAB', ['SEAM FALLS!', 'BEHIND THE FALLS!', 'THE SEAMS POUR!', 'A WALL OF FIRE!', 'THROUGH THE LAST FALL!']),
    856: ('RSRS', 'NNWDT', 'CSMRAB', ['THE NEST!', 'SPAWNING GROUND!', 'THE LITTER GROWS!', 'NEST UPON NEST!', 'SHUT THE LAST ONE!']),
    855: ('RSRS', 'TWDTL', 'CRSAMB', ['YOU ARE HERE!', 'AGAIN, YOU ARE HERE!', 'THE SIGN KNOWS!', 'LAP AFTER LAP!', 'STILL HERE!']),
    854: ('RRPR', 'DLWTG', 'HCRMAS', ['THE QUARRY!', 'THE CUT FACE!', 'DUST AND STONE!', 'THE DEEP CUT!', 'OUT OF THE QUARRY!']),
    853: ('RSRS', 'WVDTL', 'CRMSAB', ['DUST!', 'CHOKING ROAD!', 'A COUGH OF EMBERS!', 'THE DUST STORM!', 'THROUGH THE HAZE!']),
    852: ('RRSR', 'DGTLE', 'CAMRSB', ['THE GALLERY!', 'THE STATUES WATCH!', 'EYES FOLLOW!', 'THE GALLERY FILLS!', 'THE LAST EXHIBIT!']),
    810: ('RRSR', 'DWLTV', 'HCMRAS', ["THE NEEDLE'S FOOT!", 'UP THE STAIR!', 'THE SPIRE HUMS!', 'SHARDS AND SPARKS!', 'THE LAST FLIGHT!']),
    809: ('RSRR', 'VTDLW', 'CRAMSB', ['ROUND THE SPIRE!', 'HOT AIR AGAIN!', 'THE TALL COLUMN!', 'SPIRALLING UP!', 'THE LONG DRAFT UP!']),
    808: ('SRSR', 'DWTVL', 'CMRASB', ['THE CREEPING STAIR!', 'THE RIFT RISES!', 'KEEP MOVING!', 'STAIRS GO DOWN!', 'THE LAST STEP!']),
    807: ('RRSR', 'GTDWL', 'CRAMSB', ['THE CHOIR!', 'THE FIRST VERSE!', 'ALL VOICES!', 'THE CHORUS!', 'THE LAST NOTE!']),
    806: ('PRSR', 'DLWTV', 'HCMRAS', ['THE OVERHANG!', 'IT SHEDS!', 'UNDER THE LIP!', 'THE BIG ONE!', 'OUT FROM UNDER!']),
    805: ('PRPR', 'WDTLN', 'CRMSAB', ['THE BREAKAWAY!', 'IT GOES!', 'RUN AND JUMP!', 'STAIRS IN PIECES!', 'THE LAST BREAK!']),
    804: ('RSRR', 'DTGWL', 'CAMRSB', ['THE TURN!', 'ROUND THE BEND!', 'HOLD THE LINE!', 'ONE MORE TURN!', 'OUT OF THE TURN!']),
    803: ('RPRP', 'DWVLT', 'CMRASH', ['THE LAST HEAVE!', 'SHOVE AND SHOVE!', 'HEAVE-HO!', 'THE ROAD BUCKLES!', 'THE LAST ONE. REALLY.']),
    802: ('RSRR', 'DLTWE', 'HCRAMSB', ['THE SPIRE DOOR!', 'TALLY MARKS!', 'THE DOOR WATCHES!', 'ALMOST AT THE DOOR!', 'KNOCK KNOCK!']),
}

# the kind of each piece: a run (28 units) or a climb (8): three rotations, the finale always a run
KINDS = [['run', 'climb', 'run', 'run', 'run'], ['run', 'run', 'climb', 'run', 'run'], ['run', 'climb', 'run', 'climb', 'run']]
LADDER_PIECE = 1         # the first climb piece of a floor is a ladder (the riser is steps)

# where the opening's last foothold is, when the last spawn of its last section is not it: (x, top) in the path's frame
FOOT_OVERRIDE = {}


# ------------------------------------------------------------------ hazards by strength (hotter than Circle X's)

def h_cinders(s, piece, flavor=None):
    return [wave('cinder', 3 if s < 5.2 else 4, max(4.2, 8.2 - 0.65 * s), 2.0, TOP, 'wave', name=f'{piece}: cinders')]


def h_sparks(s, piece, flavor=None):
    return [wave('cinderSpark', 5 if s < 5.0 else 6, max(5.0, 11.0 - 0.9 * s), 3.5, LOW, 'wave', 0.25, name=f'{piece}: sparks from the Rift')]


def h_brutes(s, piece, flavor=None):
    return [wave('cinderBrute', 1 if s < 5.4 else 2, max(9.0, 19.0 - 1.5 * s), 5.0, TOP, 'brute', 1.2, name=f'{piece}: a brute for the fists')]


def h_mixed(s, piece, flavor=None):
    """A mixed wave: a cinder pair, a brute and a burst of sparks arrive together every so often."""
    return [{'name': f'{piece}: a mixed wave', 'when': {'type': 'every', 'seconds': round(max(9.0, 17.0 - 1.1 * s), 1), 'startAfter': 4.0, 'maxTimes': 0},
             'do': [{'type': 'spawnWave', 'entity': 'cinder', 'count': 2, 'interval': 0.6, 'anchor': 'Camera', 'areaCenter': TOP[0], 'areaSize': TOP[1], 'tags': ['wave']},
                    {'type': 'spawnWave', 'entity': 'cinderBrute', 'count': 1, 'interval': 0.6, 'anchor': 'Camera', 'areaCenter': TOP[0], 'areaSize': TOP[1], 'tags': ['brute']},
                    {'type': 'spawnWave', 'entity': 'cinderSpark', 'count': 3 if s < 5.0 else 4, 'interval': 0.25, 'anchor': 'Camera', 'areaCenter': LOW[0], 'areaSize': LOW[1], 'tags': ['wave']}]}]


def hz_shards(s):
    return {'type': 'startHazard', 'id': 'shards', 'projectile': 'spireShard', 'pattern': {'type': 'rain', 'count': 1 if s < 4.6 else 2, 'topInset': 0.25},
            'speed': 0.0, 'interval': round(max(0.9, 2.3 - 0.2 * s), 2), 'initialDelay': 1.5}


def hz_rain_hot(s):
    h = hz_rain(s)
    h['pattern']['count'] = 2 if s < 5.2 else 3
    h['interval'] = round(max(0.9, 2.0 - 0.18 * s), 2)
    return h


def hz_aimed_hot(s):
    h = hz_aimed(s)
    h['pattern'] = {'type': 'spread', 'count': 4, 'angle': 40}
    h['interval'] = round(max(2.8, 4.4 - 0.3 * s), 2)
    h['speed'] = 3.2
    return h


def estimate(srcs, s, placed):
    tot = placed
    for c in srcs:
        if c == 'C': tot += (3 if s < 5.2 else 4) * 6.0 / max(4.2, 8.2 - 0.65 * s)
        if c == 'R': tot += (2 if s < 5.2 else 3) * 1.5 / max(0.9, 2.0 - 0.18 * s)
        if c == 'S': tot += (5 if s < 5.0 else 6) * 2.0 / max(5.0, 11.0 - 0.9 * s)
        if c == 'A': tot += 4 * 3.0 / max(2.8, 4.4 - 0.3 * s)
        if c == 'B': tot += (1 if s < 5.4 else 2) * 10.0 / max(9.0, 19.0 - 1.5 * s)
        if c == 'M': tot += (3 + 1 + (3 if s < 5.0 else 4)) * 4.0 / max(9.0, 17.0 - 1.1 * s)
        if c == 'H': tot += (1 if s < 4.6 else 2) * 1.5 / max(0.9, 2.3 - 0.2 * s)
    return tot


def hazard_for(c, s, piece, flavor):
    """('rules'|'hazards', [...]) for one source letter."""
    if c == 'C': return 'rules', h_cinders(s, piece)
    if c == 'S': return 'rules', h_sparks(s, piece)
    if c == 'B': return 'rules', h_brutes(s, piece)
    if c == 'M': return 'rules', h_mixed(s, piece)
    if c == 'R': return 'hazards', [hz_rain_hot(s)]
    if c == 'A': return 'hazards', [hz_aimed_hot(s)]
    if c == 'H': return 'hazards', [hz_shards(s)]
    raise KeyError(c)


# ------------------------------------------------------------------ what stands on the road

def placed(kind, x, y, i, k, n):
    """The spawns of one loadout letter on the slab centred at (x, y) (section coordinates)."""
    if kind == 'D':
        return [sp('brimstoneDemon', x, y + 0.7, None, onEnter=True)]
    if kind == 'W':
        return [sp('demonWhelp', x - 1.0, y + 0.65, None, onEnter=True), sp('demonWhelp', x + 1.0, y + 0.65, None, onEnter=True)][:1 + (i >= 3)]
    if kind == 'L':
        return [sp('roadPerch', x, y + 2.7, 0.9), sp('demonLobber', x, y + 3.4, None, onEnter=True)]
    if kind == 'N':
        return [sp('whelpNest', x, y + 0.5, None, onEnter=True)]
    if kind == 'V':
        return [sp(VENTS[k % 4], x + (-0.6 if k % 2 else 0.6), y + 0.25)]
    if kind == 'T':
        return [sp(TRICKS[(k + i) % 3], x, y + 3.0, None, onEnter=True)]
    if kind == 'G':
        return [sp('impGuard', x, y + 3.1, None, onEnter=True)]
    if kind == 'X':
        return [sp('impSwingB' if k % 2 else 'impSwing', x + 2.0, y + 0.25 + 2.5, None, onEnter=True)]
    if kind == 'E':
        return [sp('beamLens', x, y + 3.4, None, onEnter=True)]
    raise KeyError(kind)


def loadout(letters, i, tier):
    """The letters standing in piece i: 2 on the first, 5 (6 on the later floors) on the last; the most alive at once stays under 16."""
    n = [2, 2, 3, 4, 5][i] + (1 if tier >= 18 and i >= 3 else 0)
    pool = [c for c in letters if not (c == 'E' and i < 3)]
    out = []
    j = 0
    while len(out) < n:
        out.append(pool[j % len(pool)])
        j += 1
    return out


# ------------------------------------------------------------------ the path

def run_piece(n, tier, i, last, foot, letters):
    """A Right section: slabs every 4 units (a zig up and down), the loadout on the slabs, the last one the foot (or the landing's)."""
    cyc = foot
    heights = [0.0, 0.6, 0.0, 0.5]
    cnt = 6 if last else 7
    spawns = []
    lo = loadout(letters, i, tier)
    ks = [3, 5, 2, 6, 4, 1, 7]
    slots = {}
    for c, k in zip(lo, ks * 2):
        slots.setdefault(k, []).append(c)
    prev_kind, prev_y = 'R', -1.2
    for k in range(1, cnt + 1):
        x = 4.0 * k
        y = -1.2 + heights[(k - 1) % 4]
        kind = 'R' if k == cnt and not last else cyc[(k + i) % len(cyc)]
        if k == cnt and not last:
            y = -1.2
        elif prev_kind in 'SP' and y > prev_y:
            y = prev_y             # a slab that settles or splits under him is never followed by a rise
        if k == 1 and y > -1.2:
            y = -1.2
        ent, scale = FOOT[kind]
        spawns.append(sp(ent, x, y, scale))
        prev_kind, prev_y = kind, y
        if k < cnt or last:
            for c in slots.get(k, []):
                spawns.extend(placed(c, x, y, i, k, cnt))
    if last:
        length = 30.0
    else:
        length = 28.0
        # the 7th slab stands at 28 (the foot of the next section); loadout letters that landed there go back one slab
    return spawns, length


def climb_piece(n, tier, i, foot, letters, ladder, L=8.0):
    """An Up section from a hero on a foot at x 0, top -0.95: slabs zigging up to a foot slab at (0, L-1.2), or a ladder to it."""
    foot_top = -0.95
    ftop = L - 1.2 + 0.25
    spawns = []
    lo = [c for c in loadout(letters, i, tier) if c in 'TGXDLW'][:2 + (i >= 3)]
    if ladder:
        h = ftop - foot_top
        spawns.append({'entity': 'ladder', 'at': [0.3, round((ftop + foot_top) / 2, 3)], 'anchor': 'Section', 'scale': [1.0, round(h, 3)], 'id': f'ladder{n}'})
        spawns.append(sp('roadSlab', 0.0, L - 1.2, 1.2))
        mids = [(-3.0, 2.6), (3.0, 4.4), (-3.0, 6.0)]
    else:
        step = 1.35
        k_n = max(1, math.ceil((ftop - foot_top) / step))
        mids = []
        for k in range(1, k_n + 1):
            y = foot_top + (ftop - foot_top) * k / k_n
            x = 0.0 if (k_n - k) % 2 == 0 else -2.4
            last = k == k_n
            ent, scale = ('roadSlab', 1.2) if last else FOOT[foot[(k + i) % len(foot)] if foot[(k + i) % len(foot)] != 'P' else 'R']
            spawns.append(sp(ent, x, y - 0.25, scale))
            if not last and k % 2 == 0:
                mids.append((x + (3.0 if x < 1.0 else -3.0), y + 0.4))
    for c, (mx, my) in zip(lo, mids):
        ent = {'G': 'impGuard', 'X': 'impSwing', 'T': TRICKS[i % 3], 'D': 'impGuard', 'L': TRICKS[(i + 1) % 3], 'W': 'imp'}[c]
        spawns.append(sp(ent, mx, my + 0.9, None, onEnter=True))
    return spawns, L


def riser(L, foot_top, foot_x, foot):
    """The first climb: steps from the opening's last foothold up clear of the opening, ending on a foot slab at (0, L-1.2)."""
    ftop = L - 1.2 + 0.25
    n = max(1, math.ceil((ftop - foot_top) / 1.35))
    out = []
    for k in range(1, n + 1):
        y = foot_top + (ftop - foot_top) * k / n
        x = 0.0 if (n - k) % 2 == 0 else (2.4 if foot_x >= 0 else -2.4)
        last = k == n
        ent, scale = ('roadSlab', 1.2) if last else ('roadSlab', 0.9)
        out.append(sp(ent, x, y - 0.25, scale))
    return out


PLATFORM = {'roadSlab', 'settleSlab', 'splitSlab', 'driftSlab', 'driftSlabLeft', 'risingSlab', 'risingSlabTall', 'seesawSlab', 'seesawSlabB', 'roadCreep',
            'roadCreepRight', 'ledge', 'sinkingStone', 'crumblingStone', 'debrisRaft', 'chainLift'}


def last_foot(kept, ex, ey):
    """The opening's last foothold in the path's frame (x, top): the platform that is furthest along its last section."""
    for sec in reversed(kept):
        cands = [q for q in sec['spawns'] if q['entity'] in PLATFORM and q.get('anchor', 'Path') == 'Path']
        if cands:
            d = sec['direction']
            key = (lambda q: (q['at'][1], q['at'][0])) if d in ('Up', 'UpRight', 'UpLeft') else (lambda q: (q['at'][0],)) if d == 'Right' else (lambda q: (-q['at'][0],))
            q = max(cands, key=key)
            return q['at'][0], q['at'][1] + 0.25
    return ex, ey - 0.95


def old_ceiling(kept, ex, ey):
    top = ey
    for s in kept:
        for q in s['spawns']:
            if q['entity'] == 'lavaSea' or q.get('anchor', 'Path') != 'Path':
                continue
            if q['at'][0] >= ex - 6.0:
                top = max(top, q['at'][1] + 1.5)
    return top


def build(n, report):
    foot, letters, srcs_order, banners = SPEC[n]
    tier = TIER0 + ORDER.index(n)
    idx = ORDER.index(n)
    d = original(n)
    script = d['script']
    path = script['world']['path']
    names = [p['name'] for p in path]
    assert 'drift' in names, (n, names)
    cut = names.index('drift')
    kept = path[:cut]
    tail = path[cut:]
    # the landing (and what stands with it: Floor 802's tally soul) comes from the old last section
    land_sec = tail[-1]
    land = next(q for q in land_sec['spawns'] if q.get('id') == 'landing')
    extras = [q for q in land_sec['spawns'] if q is not land]
    ex, ey = end_of(kept)
    lx, ltop = FOOT_OVERRIDE.get(n) or last_foot(kept, ex, ey)
    ceiling = old_ceiling(kept, ex, ey)
    L = 5.6
    while ey + L - 1.2 < ceiling + 1.0:
        L += 0.4
    L = round(L, 1)
    rs = {'name': 'riser', 'direction': 'Up', 'length': L, 'turn': 2.0, 'lead': 0.4, 'autoSpeed': 0.36,
          'spawns': riser(L, ltop - ey, lx - ex, foot)}
    sections = kept + [rs]

    kinds = KINDS[idx % 3]
    s0 = 2.8 + 0.03 * idx
    step = 0.55
    phases = list(script['phases'])
    rules = [r for r in script['rules'] if not any(x.get('type') == 'sectionReached' and x.get('section') in ('drift', 'driftClimb', 'landing')
                                                    for x in walk(r['when']))]
    new_rules = []
    est = []
    first_climb = True
    for i in range(5):
        kind = kinds[i]
        banner = banners[i]
        pname = re.sub(r'[^A-Za-z0-9]', '', banner.title())[:26] + f'{i + 1}'
        last = i == 4
        s = s0 + i * step
        k_src = min(len(srcs_order), [2, 3, 3, 4, 4][i])
        srcs = srcs_order[:k_src]
        lo_n = len(loadout(letters, i, tier))
        est.append((banner, round(estimate(srcs, s, lo_n * 0.4), 1)))
        if kind == 'run':
            spawns, length = run_piece(n, tier, i, last, foot, letters)
            if last:
                lp = dict(land)
                lp['at'] = [29.4, -0.4]
                lp['anchor'] = 'Section'
                spawns.append(lp)
                for q in extras:
                    e = dict(q)
                    e['at'] = [round(29.4 + q['at'][0] - land['at'][0], 3), round(-0.4 + q['at'][1] - land['at'][1], 3)]
                    e['anchor'] = 'Section'
                    spawns.append(e)
            sec = {'name': pname, 'direction': 'Right', 'length': length, 'turn': 2.0, 'autoSpeed': 0.55, 'spawns': spawns}
        else:
            ladder = first_climb
            first_climb = False
            spawns, length = climb_piece(n, tier, i, foot, letters, ladder)
            sec = {'name': pname, 'direction': 'Up', 'length': length, 'turn': 2.0, 'lead': 0.4, 'autoSpeed': 0.36, 'spawns': spawns}
        for q in sec['spawns']:
            if q['entity'] in ENEMIES:
                q.setdefault('tags', []).append(f'pc{i}')
        sections.append(sec)
        gone = [{'type': 'despawn', 'target': {'tag': f'pc{i - 1}'}}] if i > 0 else []
        new_rules.append({'name': f'to {pname}', 'when': {'type': 'sectionReached', 'section': pname}, 'maxTimes': 1,
                          'do': gone + [{'type': 'goToPhase', 'phase': pname}]})
        on_enter = [{'type': 'emit', 'signal': 'setpiece:' + pname},
                    {'type': 'showText', 'text': banner, 'duration': 2.4, 'style': 'Banner', 'color': GOLD}]
        if i == 1:
            on_enter.append({'type': 'playMusic', 'clip': 'Audio/Music/wastes_push.ogg', 'volume': 0.85})
        if last:
            on_enter.append({'type': 'playMusic', 'clip': 'Audio/Music/wastes_loop.ogg', 'volume': 0.85})
        prules = []
        for c in srcs:
            kindr, items = hazard_for(c, s, banner, None)
            if kindr == 'hazards':
                on_enter.extend(items)
            else:
                prules.extend(items)
        phases.append({'name': pname, 'onEnter': on_enter, 'rules': prules})

    script['world']['path'] = sections
    script['phases'] = phases

    # the landing waits for the floor's minutes
    for r in rules:
        if r.get('name') == 'the landing':
            r['when'] = {'type': 'allOf', 'triggers': [r['when'], {'type': 'counter', 'name': 'finale', 'atLeast': 1.0}]}
    have = {c['name'] for c in script['counters']}
    for c in ('heat', 'finale'):
        if c not in have:
            script['counters'].append({'name': c, 'value': 0.0})
    new_rules.append({'name': "the opening's rift stops", 'when': {'type': 'sectionReached', 'section': 'riser'}, 'maxTimes': 1,
                      'do': [{'type': 'setCounter', 'name': 'lavaSpeed', 'value': 0.0}]})
    new_rules.append({'name': 'the floor has had its minutes', 'when': {'type': 'atTime', 'seconds': FINALE_AT}, 'maxTimes': 1,
                      'do': [{'type': 'setCounter', 'name': 'finale', 'value': 1.0}]})
    new_rules.append({'name': 'heat: the floor gets hotter', 'when': {'type': 'every', 'seconds': 20.0, 'startAfter': 40.0, 'maxTimes': 12},
                      'do': [{'type': 'addCounter', 'name': 'heat', 'amount': 1.0}]})
    for h in (2, 4, 6, 8, 10):
        new_rules.append({'name': f'heat {h}: the cinders hurry', 'when': {'type': 'counter', 'name': 'heat', 'atLeast': float(h)}, 'maxTimes': 1,
                          'do': [{'type': 'modifier', 'target': {'entity': 'cinder'}, 'modifiers': [{'stat': 'MoveSpeed', 'multiply': 1.1}], 'includeFuture': True}]})
    script['rules'] = rules + new_rules
    script['length'] = 285
    d['duration'] = 400.0
    report.append((n, L, est, (lx, ltop), (ex, ey)))
    return d


def main(argv):
    wanted = [int(a) for a in argv] or ORDER
    report = []
    for n in wanted:
        d = build(n, report)
        dump(os.path.join(LEV, f'Floor{n}.json'), d)
    for n, L, est, foot, end in report:
        print(n, 'riser', L, 'foot', tuple(round(v, 2) for v in foot), 'end', tuple(round(v, 2) for v in end), 'est alive:', ' '.join(f'{e}' for p, e in est))


if __name__ == '__main__':
    main(sys.argv[1:])
