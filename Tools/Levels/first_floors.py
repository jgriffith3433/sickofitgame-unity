#!/usr/bin/env python3
"""
The first four floors teach one threat each (Docs/LevelDesign.md "The first four floors: teach, then finale").

  1000 The Landing   sinking stones   999 Only 999 To Go   rising lava   998 The Spill   the cinder   997 Imp Run   the sack imps' chains

Patches Floor1000-997.json in place (idempotent; rules named "first4: ..." are replaced, inserted steps carry a `floors:<n>:` signal):
  * TEACH  a `teachGlow` (Camera anchor, inside the playfield, above the touch stick) 1.8 s long + the threat's cue sound + signals
           floors:<n>:glow / floors:<n>:cue, 1.2 s before the threat first acts, so it is read before it is met.
  * FINALE its last section: `floors:<n>:finale`, the push track, a shake and a screen-wide tint of six flash pulses (~15 s), the threat harder.
  * PAYOFF a stinger (`floors:<n>:payoff`) as the landing opens the Shaft (before the Grudge pick).
Run it AFTER long_floors.py / cracked_floors.py / companions.py / overlord_cameos.py (they rewrite floors):
  python3 Tools/Levels/first_floors.py            write
  python3 Tools/Levels/first_floors.py --check    no Unity: every floor has teach, finale and payoff (exit 1 otherwise)
"""
import json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
LEV = os.path.join(ROOT, 'Assets', 'GameConfig', 'levels')
PUSH = 'Audio/Music/wastes_push.ogg'

FLOORS = {
    1000: dict(thing='sinking stones', start='slope', wait=1.5, glow=[1.4, 1.0], cue='lava_bubble', finale='bank',
               tint='#FF8A3D40', sting='sting_rank_b'),
    999: dict(thing='rising lava', start=None, wait=1.2, glow=[0.0, 1.2], cue='magma_rumble', finale='chase',
              tint='#FF3A2440', sting='sting_rank_a'),
    998: dict(thing='the cinder', start='river', wait=3.0, glow=[-1.4, 1.2], cue='slag_drip', finale='spire',
              tint='#FF6A1E40', sting='sting_rank_a'),
    997: dict(thing="the sack imps' chains", start='ledges', wait=2.0, glow=[1.0, 1.4], cue='chain_cut', finale='liftStop',
              tint='#FFD27A3C', sting='sting_rank_s'),
}
PULSES = 6


def path(n):
    return os.path.join(LEV, f'Floor{n}.json')


def teach_steps(n, c):
    return [
        {"type": "spawn", "entity": "teachGlow", "anchor": "Camera", "at": c['glow'], "id": f"teach{n}", "facePlayer": False},
        {"type": "playSound", "sound": c['cue'], "volume": 1.0},
        {"type": "emit", "signal": f"floors:{n}:glow"},
        {"type": "emit", "signal": f"floors:{n}:cue"},
        {"type": "wait", "seconds": c['wait'] if c['start'] is None else 1.2},
    ]


def finale_steps(n, c):
    s = [{"type": "emit", "signal": f"floors:{n}:finale"},
         {"type": "playMusic", "clip": PUSH, "volume": 0.9},
         {"type": "cameraShake", "intensity": 0.18, "duration": 0.6}]
    for i in range(PULSES):
        s.append({"type": "flash", "color": c['tint'], "duration": 2.6})
        if i == 0:
            s.append({"type": "emit", "signal": f"floors:{n}:tint"})
        s.append({"type": "wait", "seconds": 2.5})
    return s


def patch(n):
    c = FLOORS[n]
    d = json.load(open(path(n)))
    s = d['script']
    rules = [r for r in s['rules'] if not r['name'].startswith('first4:')]
    # TEACH
    if c['start'] is None:
        for r in rules:
            if r['name'] == 'the lava starts to climb' and 'floors:999:glow' not in json.dumps(r):
                steps = r['do'][0]['steps']
                steps[:] = [x for x in steps if x.get('type') != 'playMusic']   # the push track is the finale's now
                for x in steps:
                    if x.get('type') == 'setCounter' and x.get('name') == 'lavaSpeed':
                        x['value'] = 0.5                                         # slow while it is learned
                steps[:0] = teach_steps(n, c)
    else:
        rules.append({"name": f"first4: teach {c['thing']}", "when": {"type": "sectionReached", "section": c['start']}, "maxTimes": 1,
                      "do": [{"type": "runSequence", "steps": [{"type": "wait", "seconds": c['wait']}] + teach_steps(n, c)}]})
    # FINALE
    do = [{"type": "runSequence", "steps": finale_steps(n, c)}]
    if n == 999:
        do.append({"type": "setCounter", "name": "lavaSpeed", "value": 0.9})
    if n == 998:
        do.append({"type": "runSequence", "steps": sum(([{"type": "spawnWave", "entity": "cinder", "count": 2, "interval": 0.8, "anchor": "Camera",
                      "areaCenter": [0.0, 2.2], "areaSize": [14.0, 1.5], "tags": ["wave"]}, {"type": "wait", "seconds": 4.2}] for _ in range(4)), [])})
    rules.append({"name": f"first4: finale {c['thing']}", "when": {"type": "sectionReached", "section": c['finale']}, "maxTimes": 1, "do": do})
    s['rules'] = rules
    # PAYOFF: before the Grudge pick
    for r in rules:
        if r['name'] == 'the landing' and f'floors:{n}:payoff' not in json.dumps(r):
            steps = r['do'][0]['steps']
            i = next(k for k, x in enumerate(steps) if x.get('type') == 'showText')
            steps[i + 1:i + 1] = [{"type": "playSound", "sound": c['sting'], "volume": 1.0}, {"type": "emit", "signal": f"floors:{n}:payoff"}]
    # 1000's finale is the stones at full intensity: the bank's plain ledges become stones (the last ledge stays solid)
    if n == 1000:
        for p in s['world']['path']:
            if p['name'] == 'bank':
                k = 7
                for sp in p['spawns']:
                    if sp['entity'] == 'ledge' and 'id' not in sp:
                        sp['entity'] = 'sinkingStone'
                        sp['id'] = f'stone{k}'
                        sp['scale'] = [0.9, 1]
                        k += 1
    json.dump(d, open(path(n), 'w'), indent=2)
    open(path(n), 'a').write('\n')


def check():
    bad = 0
    for n, c in FLOORS.items():
        t = json.dumps(json.load(open(path(n))))
        for key in ('glow', 'cue', 'finale', 'tint', 'payoff'):
            if f'floors:{n}:{key}' not in t:
                print(f'Floor {n}: missing floors:{n}:{key}'); bad += 1
    print('ok' if not bad else f'{bad} problem(s)')
    return bad


if __name__ == '__main__':
    if '--check' in sys.argv:
        sys.exit(1 if check() else 0)
    for n in FLOORS:
        patch(n)
    sys.exit(1 if check() else 0)
