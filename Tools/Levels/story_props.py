#!/usr/bin/env python3
"""Tilesets and story props of the Meadow and Floors 1000-997 (Docs/Story.md "Hell's furniture", docs/world-bible.md).

Writes, from the tables below, never by hand:
  * content/entities/ledgeFloor1000|999|998|997.json: the floor's own ledge, three drawn variants (`Art/Tiles/<set>/LedgeA|B|C`)
    chosen by where it stands (`initialStateByPlace`, `Entity.PlaceHash`); the body is `ledge`'s, so no collider changes;
  * content/entities/sodStrip.json, sodEndL.json, sodEndR.json: the Meadow's ground lip (three strips by place, two ends);
  * content/entities/prop<Name>.json: each story prop (tag `storyProp`, band Background2, behind the hero, shots and ground
    sod): an idle clip and, for the ones that answer a shot passing or the hero close by, a React clip with a sound, then
    a Done pose (a sign that fell stays down) or a short rest and idle again;
  * the levels: the floors' `ledge` spawns become the floor's variant, each prop is a path spawn `sp_<n>` standing on the top of its
    ledge (same section and anchor as the ledge, so it streams in with it), the Meadow's sod and props are `spawn` actions at the
    start of its first phase.
Run it after every other floor writer (long_floors.py, first_floors.py, stop_floors.py, ambient_finds.py ...): it is idempotent and
puts back what a writer replaced. `--check` verifies, with no Unity: the files are current, every prop's art and sidecar exist, each
stands on its surface (gap and overlap 0, limit 2 px), fits the ledge it sits on, 8+ props per level with 2+ reacting, 3 tile variants
visible per level, every sound has a clip.

  python3 Tools/Levels/story_props.py          write
  python3 Tools/Levels/story_props.py --check  exit 1 when anything would change or is wrong
"""
import itertools
import json
import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'Art'))
import story_art  # noqa: E402

GC = os.path.join(ROOT, 'Assets', 'GameConfig')
ENT = os.path.join(GC, 'content', 'entities')
LEV = os.path.join(GC, 'levels')

LEDGE_TOP = 0.25      # ledge body: Box [2.0, 0.5], centred on its place
LANDING_TOP = 0.30    # floorLanding body: Box [6.0, 0.6]
GROUND_TOP = -2.5     # the Meadow's ground (groundY -3, collider 1 tall)
PROP_ORDER = 90       # inside Background2: above the backdrop layers, behind the Player band
ORDER = {'fenceA': 88}  # the fences stand a little behind the rest of the Meadow's props
PX = 1.0 / 202.0      # one texture pixel of a ledge, in world units (the "2 px" limit)

# ----------------------------------------------------------------------------------------------------------------------
# Reactions: prop -> (sound, volume, shot radius (0 = ignores shots), touch radius (0 = ignores him), 'done' = stays changed, shake)
# Sounds are audio.json ids that already have a clip.
# ----------------------------------------------------------------------------------------------------------------------
REACT = {
    'hen': ('amb_rooster_crow', 0.9, 1.6, 1.0, False, 0.0),
    'roosterPerch': ('amb_rooster_crow', 0.9, 1.6, 1.0, False, 0.0),
    'mill': ('amb_mill_creak', 1.0, 2.0, 0.0, True, 0.0),
    'cart': ('sack_spill', 0.9, 1.8, 1.2, True, 0.04),
    'hayBale': ('thatch_burst', 0.9, 1.4, 1.0, True, 0.0),
    'fenceA': ('hit_pop', 1.0, 1.5, 0.0, True, 0.0),
    'floorSign': ('door_knock', 0.8, 1.8, 0.0, True, 0.06),
    'numberDispenser': ('amb_ticket_bell', 1.0, 1.4, 1.0, True, 0.0),
    'serviceBell': ('amb_ticket_bell', 1.0, 1.2, 0.9, False, 0.0),
    'gibbet': ('amb_chain_rattle', 1.0, 1.8, 1.1, False, 0.0),
    'gaugePost': ('amb_steam_hiss', 0.9, 1.5, 0.0, True, 0.0),
    'valveWheel': ('amb_hammer_strike', 0.9, 1.3, 0.9, False, 0.0),
    'steamVent': ('amb_steam_hiss', 1.0, 1.4, 1.0, False, 0.0),
    'bonePile': ('amb_bone_clatter', 1.0, 1.4, 1.0, False, 0.0),
    'boneChime': ('amb_bone_chime', 1.0, 1.6, 1.0, False, 0.0),
    'hymnBoard': ('amb_bone_clatter', 0.9, 1.6, 0.0, False, 0.0),
    'bannerTall': ('whip_crack', 0.8, 1.8, 0.0, True, 0.0),
    'ovlPortrait': ('sf_crumble', 0.5, 1.8, 0.0, True, 0.08),
    'brazier': ('amb_torch_crackle', 1.0, 1.4, 1.0, False, 0.0),
}

# ----------------------------------------------------------------------------------------------------------------------
# Placements. Floors: (prop, section, ledge place [x, y] exactly as in the floor, dx from the ledge's middle).
# ----------------------------------------------------------------------------------------------------------------------
FLOORS = {
    1000: dict(tiles='Floor1000', props=[
        ('luggage', 'slope', (-3.6, -2.45), -1.2),
        ('floorSign', 'slope', (-3.6, -2.45), 1.0),
        ('hatShelf', 'slope', (1.2, 2.3), 0.0),
        ('numberDispenser', 'slope', (3.4, 6.2), -0.8),
        ('queueRope', 'slope', (3.4, 6.2), 0.6),
        ('serviceBell', 'drift', (21.4, 6.6), 0.0),
        ('cageStanding', 'bank', (18.9, 13.3), 0.0),
        ('welcomeDesk', 'top', (23.6, 14.4), -1.8),
        ('gibbet', 'top', (23.6, 14.4), 1.9),
    ]),
    999: dict(tiles='Floor999', props=[
        ('furnaceDoor', 'ledges', (-3.6, -2.45), -1.2),
        ('coalBucket', 'ledges', (-3.6, -2.45), 0.9),
        ('anvil', 'ledges', (1.5, 0.1), 0.0),
        ('valveWheel', 'shelf', (5.6, 2.6), 0.0),
        ('gaugePipe', 'shelf', (9.0, 2.6), 0.0),
        ('tallyWall', 'climb', (9.6, 5.2), 0.0),
        ('gaugePost', 'climb', (9.4, 10.4), 0.0),
        ('steamVent', 'chase', (12.8, 11.2), 0.0),
        ('furnacePipes', 'chase', (18.6, 10.9), 0.0),
    ]),
    998: dict(tiles='Floor998', props=[
        ('brokenPew', 'river', (-3.6, -1.45), -1.0),
        ('candleStubs', 'river', (-3.6, -1.45), 1.25),
        ('bonePile', 'procession', (23.7, -1.2), 0.0),
        ('stoneFont', 'emberfall', (50.7, -1.2), 0.0),
        ('boneHeap', 'emberfall', (56.7, -1.2), 0.0),
        ('hymnBoard', 'signpost', (68.7, -1.2), 0.0),
        ('pulpit', 'signpost', (74.7, -1.2), 0.0),
        ('boneChime', 'spire', (87.0, 6.8), 0.0),
        ('choirLoft', 'spire', (104.4, 7.6), -0.6),
    ]),
    997: dict(tiles='Floor997', props=[
        ('pedestal', 'ledges', (-3.6, -2.45), -1.2),
        ('halberds', 'ledges', (-3.6, -2.45), 1.0),
        ('bannerShort', 'ledges', (1.8, 0.3), 0.0),
        ('bannerTall', 'ledges', (6.0, 2.9), 0.0),
        ('throneCrate', 'gap', (13.0, 4.5), 0.0),
        ('ovlPortrait', 'gap2', (20.4, 5.9), 0.0),
        ('brazier', 'wall', (17.8, 9.9), 0.0),
        ('bannerStand', 'higher', (10.8, 10.2), 0.0),
        ('wallHook', 'higher', (5.6, 10.6), 0.0),
    ]),
}

# The Meadow (world coordinates; ground top -2.5; the view is about x -5 to 5).  (prop, x, sorting offset)
MEADOW = [
    ('fenceA', -2.7, 0), ('fenceA', 3.7, 0),
    ('mill', -4.3, 2), ('well', -2.2, 2), ('hen', -0.9, 3), ('cart', 0.7, 2), ('hayBale', 2.7, 3), ('scarecrow', 4.1, 2),
    ('roosterPerch', 5.1, 3),
]
SOD_X = [-6.0, -4.0, -2.0, 0.0, 2.0, 4.0, 6.0]   # one strip is 2.0 wide; the two ends sit at the walls

SODS = {'A': 'Art/Tiles/Meadow/SodStripA.png', 'B': 'Art/Tiles/Meadow/SodStripB.png', 'C': 'Art/Tiles/Meadow/SodStripC.png'}


def cap(name):
    return name[0].upper() + name[1:]


def prop_id(name):
    return 'prop' + cap(name)


def place_hash(x, y, count):
    """Mirror of Entity.PlaceHash (float32 places, quarter-unit cells, banker's rounding like Mathf.RoundToInt)."""
    import struct
    f32 = lambda v: struct.unpack('f', struct.pack('f', v))[0]
    cx = round(f32(f32(x) * 4.0))
    cy = round(f32(f32(y) * 4.0))
    m = 0xFFFFFFFF
    h = ((cx & m) * 73856093 & m) ^ ((cy & m) * 19349663 & m)
    h ^= h >> 13
    h = (h * 0x5BD1E995) & m
    h ^= h >> 15
    return h % count


def best_order(places, names):
    """The variant list (length 3-6, every variant in it) whose place-hash picks show the most distinct variants over the places
    (in the order given) and the fewest equal neighbours: a floor with five ledges still draws all three."""
    best = None
    for n in range(len(names), 7):
        for combo in itertools.product(names, repeat=n):
            if set(combo) != set(names):
                continue
            picks = [combo[place_hash(x, y, n)] for x, y in places]
            score = (len(set(picks)), -sum(1 for a, b in zip(picks, picks[1:]) if a == b), -n)
            if best is None or score > best[0]:
                best = (score, list(combo))
    return best[1]


# ----------------------------------------------------------------------------------------------------------------------
# Entities
# ----------------------------------------------------------------------------------------------------------------------
def jdump(obj):
    return json.dumps(obj, indent=2, ensure_ascii=False) + '\n'


def anim_state(name, sheet, fps=8, loop=True):
    return {'stateName': name, 'spriteSheet': sheet, 'fps': fps, 'loop': loop, 'events': []}


def base_visual(layer, order, color, animator):
    return {'scale': 1.0, 'placeholderColor': color, 'placeholderShape': 'Box', 'placeholderLabel': False, 'layer': layer,
            'sortingOrder': order, 'hurtFlash': False, 'deathPop': False, 'animator': animator, 'color': '#FFFFFF', 'facesLeft': False}


def ledge_entity(tiles, variants):
    ledge = json.load(open(os.path.join(ENT, 'ledge.json'), encoding='utf-8'))
    d = json.loads(json.dumps(ledge))
    d['visual']['animator'] = {'parameters': [], 'states': [anim_state(v, f'Art/Tiles/{tiles}/Ledge{v}.png', 12) for v in 'ABC'],
                               'transitions': [], 'defaultStateName': 'A'}
    d['states'] = [{'name': v, 'animation': v} for v in 'ABC']
    d['initialStates'] = variants
    d['initialStateByPlace'] = True
    return d


def sod_entity(variants=None, sheet=None):
    d = {'schemaVersion': 1, 'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['sod'],
         'body': {'shape': 'Box', 'size': [2.0, 0.1], 'solid': False}}
    if variants:
        anim = {'parameters': [], 'states': [anim_state(v, SODS[v], 12) for v in 'ABC'], 'transitions': [], 'defaultStateName': 'A'}
        d['states'] = [{'name': v, 'animation': v} for v in 'ABC']
        d['initialStates'] = variants
        d['initialStateByPlace'] = True
    else:
        anim = {'parameters': [], 'states': [anim_state('A', sheet, 12)], 'transitions': [], 'defaultStateName': 'A'}
    d['visual'] = base_visual('Player', 0, '#3F5A2A', anim)
    d['healthBar'] = {'show': False}
    d['abilities'] = []
    return d


def prop_entity(name, group):
    folder = os.path.join(story_art.PROPS, group)
    has = lambda clip: os.path.exists(os.path.join(folder, f'{name}_{clip}.png'))
    sound, vol, shot, touch, done, shake = REACT.get(name, (None, 0, 0, 0, False, 0))
    animator_states = [anim_state('Idle', f'Art/Props/{group}/{name}_idle.png', 8)]
    states = None
    if sound and has('react'):
        animator_states.append(anim_state('React', f'Art/Props/{group}/{name}_react.png', 12, loop=False))
        if done and has('done'):
            animator_states.append(anim_state('Done', f'Art/Props/{group}/{name}_done.png', 4))
        triggers = []
        if shot:
            triggers.append({'type': 'witness', 'what': 'ShotPassed', 'within': shot})
        if touch:
            triggers.append({'type': 'distanceToPlayer', 'below': touch})
        when = triggers[0] if len(triggers) == 1 else {'type': 'anyOf', 'triggers': triggers}
        on_enter = [{'type': 'playSound', 'sound': sound, 'volume': vol}]
        if shake:
            on_enter.append({'type': 'cameraShake', 'intensity': shake, 'duration': 0.2})
        on_enter.append({'type': 'emit', 'signal': f'prop:{name}:react'})
        after = 'done' if (done and has('done')) else 'rest'
        states = [{'name': 'idle', 'animation': 'Idle', 'transitions': [{'to': 'react', 'when': when}]},
                  {'name': 'react', 'animation': 'React', 'onEnter': on_enter, 'transitions': [{'to': after, 'when': {'type': 'animationDone'}}]}]
        if after == 'done':
            states.append({'name': 'done', 'animation': 'Done'})
        else:
            states.append({'name': 'rest', 'animation': 'Idle', 'transitions': [{'to': 'idle', 'when': {'type': 'after', 'seconds': 3.0}}]})
    d = {'schemaVersion': 1, 'team': 'Neutral', 'health': 0, 'shootable': False,
         'tags': ['storyProp'] + (['reacts'] if states else []),
         'body': {'shape': 'Box', 'size': [0.4, 0.2], 'solid': False},
         'visual': base_visual('Background2', ORDER.get(name, PROP_ORDER), '#6A5A4A', {'parameters': [], 'states': animator_states, 'transitions': [], 'defaultStateName': 'Idle'}),
         'healthBar': {'show': False}, 'abilities': []}
    if states:
        d['states'] = states
    return d


def prop_group(name):
    for g in os.listdir(story_art.PROPS):
        if os.path.exists(os.path.join(story_art.PROPS, g, f'{name}_idle.png')):
            return g
    raise SystemExit(f'no art for prop {name}')


def prop_size(name):
    group = prop_group(name)
    cw, ch = story_art.png_size(os.path.join(story_art.PROPS, group, f'{name}_idle.png'))
    if name == 'brazier':
        cw = 250
    h = story_art.PROP_HEIGHT[name]
    return cw / ch * h, h


def used_props():
    names = [p[0] for f in FLOORS.values() for p in f['props']] + [p[0] for p in MEADOW]
    return sorted(set(names))


def entity_files():
    files = {}
    for n, f in FLOORS.items():
        places = floor_ledge_places(n)
        files[f'ledgeFloor{n}'] = ledge_entity(f['tiles'], best_order(places, 'ABC'))
    sod_places = [(x, GROUND_TOP) for x in SOD_X[1:-1]]
    files['sodStrip'] = sod_entity(best_order(sod_places, 'ABC'))
    files['sodEndL'] = sod_entity(sheet='Art/Tiles/Meadow/SodEndL.png')
    files['sodEndR'] = sod_entity(sheet='Art/Tiles/Meadow/SodEndR.png')
    for name in used_props():
        files[prop_id(name)] = prop_entity(name, prop_group(name))
    return files


# ----------------------------------------------------------------------------------------------------------------------
# Levels
# ----------------------------------------------------------------------------------------------------------------------
def load_level(name):
    path = os.path.join(LEV, name + '.json')
    return path, json.load(open(path, encoding='utf-8'))


def is_surface(sp):
    return sp['entity'] == 'ledge' or sp['entity'].startswith('ledgeFloor') or sp['entity'].startswith('floorLanding')


def surface_top(sp):
    sy = sp.get('scale', [1, 1])[1]
    return (LANDING_TOP if sp['entity'].startswith('floorLanding') else LEDGE_TOP) * sy


def surface_half(sp):
    if sp['entity'].startswith('floorLanding'):
        return 3.0
    return 1.0 * sp.get('scale', [1, 1])[0]


def floor_ledge_places(n):
    _, d = load_level(f'Floor{n}')
    out = []
    for sec in d['script']['world']['path']:
        for sp in sec['spawns']:
            if sp['entity'] == 'ledge' or sp['entity'] == f'ledgeFloor{n}':
                out.append((sp['at'][0], sp['at'][1]))
    return out


def find_surface(sec, place):
    for sp in sec['spawns']:
        if is_surface(sp) and abs(sp['at'][0] - place[0]) < 0.011 and abs(sp['at'][1] - place[1]) < 0.011:
            return sp
    raise SystemExit(f"no ledge at {place} in section {sec['name']}")


def build_floor(n):
    path, d = load_level(f'Floor{n}')
    spec = FLOORS[n]
    secs = {s['name']: s for s in d['script']['world']['path']}
    for sec in secs.values():
        sec['spawns'] = [sp for sp in sec['spawns'] if not str(sp.get('id', '')).startswith('sp_')]
        for sp in sec['spawns']:
            if sp['entity'] == 'ledge':
                sp['entity'] = f'ledgeFloor{n}'
    for k, (name, section, place, dx) in enumerate(spec['props']):
        sec = secs[section]
        host = find_surface(sec, place)
        x = round(host['at'][0] + dx, 3)
        y = round(host['at'][1] + surface_top(host), 3)
        sp = {'entity': prop_id(name), 'at': [x, y], 'id': f'sp_{k + 1}'}
        if host.get('anchor'):
            sp['anchor'] = host['anchor']
        if host.get('onEnter'):
            sp['onEnter'] = True
        # Right after its ledge (and the props already there), so another writer that appends to the section (ambient_finds) never reorders it.
        at = sec['spawns'].index(host) + 1
        while at < len(sec['spawns']) and str(sec['spawns'][at].get('id', '')).startswith('sp_'):
            at += 1
        sec['spawns'].insert(at, sp)
    return path, d


def build_meadow():
    path, d = load_level('Level1')
    ph = d['script']['phases'][0]
    ph['onEnter'] = [a for a in ph['onEnter'] if not str(a.get('id', '')).startswith('sp_')]
    acts = []
    k = 0
    for i, x in enumerate(SOD_X):
        ent = 'sodEndL' if i == 0 else ('sodEndR' if i == len(SOD_X) - 1 else 'sodStrip')
        k += 1
        acts.append({'type': 'spawn', 'entity': ent, 'anchor': 'World', 'at': [x, GROUND_TOP], 'facePlayer': False, 'id': f'sp_{k}'})
    for name, x, off in MEADOW:
        k += 1
        acts.append({'type': 'spawn', 'entity': prop_id(name), 'anchor': 'World', 'at': [x, GROUND_TOP], 'facePlayer': False, 'id': f'sp_{k}'})
    at = 1 if ph['onEnter'] and ph['onEnter'][0].get('type') == 'objectiveText' else 0
    ph['onEnter'][at:at] = acts
    return path, d


def level_files():
    out = {}
    for n in FLOORS:
        p, d = build_floor(n)
        out[p] = jdump(d)
    p, d = build_meadow()
    out[p] = jdump(d)
    return out


# ----------------------------------------------------------------------------------------------------------------------
# Checks
# ----------------------------------------------------------------------------------------------------------------------
def problems():
    bad = []
    audio = json.load(open(os.path.join(GC, 'audio.json'), encoding='utf-8'))['sounds']
    clips = {(s['id'] if isinstance(s, dict) else k): s for k, s in (enumerate(audio) if isinstance(audio, list) else audio.items())}
    for name in used_props():
        w, h = prop_size(name)
        if name in REACT:
            if not os.path.exists(os.path.join(story_art.PROPS, prop_group(name), f'{name}_react.png')):
                bad.append(f'{name}: reacts but has no React sheet')
            snd = clips.get(REACT[name][0])
            if not snd or not snd.get('clip'):
                bad.append(f'{name}: sound {REACT[name][0]} has no clip')
    for n, spec in FLOORS.items():
        path, d = load_level(f'Floor{n}')
        secs = {s['name']: s for s in d['script']['world']['path']}
        props = spec['props']
        reacting = [p for p in props if p[0] in REACT]
        if len(props) < 8:
            bad.append(f'Floor {n}: {len(props)} props, 8 wanted')
        if len(reacting) < 2:
            bad.append(f'Floor {n}: {len(reacting)} reacting props, 2 wanted')
        places = floor_ledge_places(n)
        order = best_order(places, 'ABC')
        variants = set(order[place_hash(x, y, len(order))] for x, y in places)
        if len(variants) < 3:
            bad.append(f'Floor {n}: only {len(variants)} ledge variants show (of {len(places)} ledges)')
        for name, section, place, dx in props:
            host = find_surface(secs[section], place)
            w, _ = prop_size(name)
            if abs(dx) + w / 2 > surface_half(host) + 0.05:
                bad.append(f'Floor {n}: {name} ({w:.2f} wide at dx {dx}) overhangs its ledge (half width {surface_half(host):.2f})')
    return bad


def main(check):
    want = {}
    for name, d in entity_files().items():
        want[os.path.join(ENT, name + '.json')] = jdump(dict({'schemaVersion': 1}, **{k: v for k, v in d.items() if k != 'schemaVersion'}))
    want.update(level_files())
    stale = 0
    for path, text in sorted(want.items()):
        old = open(path, encoding='utf-8', newline='').read() if os.path.exists(path) else None
        if old == text:
            continue
        stale += 1
        if check:
            print('would change:', os.path.relpath(path, ROOT))
        else:
            with open(path, 'w', encoding='utf-8', newline='\n') as f:
                f.write(text)
    bad = problems()
    for b in bad:
        print('PROBLEM:', b)
    if check:
        print('story_props: OK' if not stale and not bad else f'story_props: {stale} files out of date, {len(bad)} problems')
        return 1 if stale or bad else 0
    print(f'story_props: wrote {stale} files, {len(bad)} problems')
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main('--check' in sys.argv))
