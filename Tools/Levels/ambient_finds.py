#!/usr/bin/env python3
"""
Small finds on the long floors (Docs/Story.md "Stop floors", the last paragraph): one damned soul hovering over a ledge and two diamond glints on
others, per floor of Circle X and Circle IX that has a way up of plain ledges (a floor with a boss, an arena, a side door or no plain ledge is left alone).
They sit on the route's own ledges (spawned in the same path section as the ledge they sit over, so they stream in with it) and need no new rule:
a glint pays one diamond, a soul within 1.6 units of the hero pays one rewind orb (damnedSoul's own react). A floor that already has a soul gets
only the glints. Safe to run again (every spawn carries the id prefix "find_" and is replaced); run it after long_floors.py / cracked_floors.py /
elites.py / bystanders.py, which rewrite their floors.

  python3 Tools/Levels/ambient_finds.py            write them
  python3 Tools/Levels/ambient_finds.py --check    nothing to write (exit 1 if a floor would change)
"""
import json, os, random, sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
LEV = os.path.join(ROOT, 'Assets', 'GameConfig', 'levels')
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from artkeep import guard  # noqa: E402

PREFIX = 'find_'
STATIC = ('ledge', 'roadSlab')               # footholds that stay where they are
SIDE_HOSTS = {910, 909, 908, 907, 906, 905, 904, 903}
BOSSES = {1000, 991, 951, 901, 891, 851, 801, 802}


def floors():
    game = json.load(open(os.path.join(ROOT, 'Assets', 'GameConfig', 'game.json'), encoding='utf-8'))['levels']
    for name in game:
        if not name.startswith('Floor'):
            continue
        n = int(name[5:])
        if n in BOSSES or n in SIDE_HOSTS or not (801 <= n <= 1000):
            continue
        yield n


def plan(n):
    path = os.path.join(LEV, f'Floor{n}.json')
    d = json.load(open(path, encoding='utf-8'))
    s = d['script']
    secs = s['world']['path']
    for sec in secs:
        sec['spawns'] = [sp for sp in sec['spawns'] if not str(sp.get('id', '')).startswith(PREFIX)]
    text = json.dumps(s)
    if 'stop:' in text or '"bossBar"' in text or 'midBoss' in text:
        return path, d, None
    spots = [(i, sp) for i, sec in enumerate(secs) if i >= 1 and sec['direction'] != 'Arena' for sp in sec['spawns'] if sp['entity'] in STATIC]
    if len(spots) < 6:
        return path, d, None
    rng = random.Random(n * 7919)
    pick = rng.sample(range(len(spots)), 3)
    has_soul = '"damnedSoul"' in text or 'Soul"' in text
    wanted = [('diamondGlint', 'find_glint0', 1.0), ('diamondGlint', 'find_glint1', 1.0)] + ([] if has_soul else [('damnedSoul', 'find_soul0', 1.1)])
    for (ent, sid, dy), k in zip(wanted, pick):
        i, host = spots[k]
        secs[i]['spawns'].append({'entity': ent, 'at': [round(host['at'][0], 2), round(host['at'][1] + dy, 2)], 'id': sid})
    return path, d, True


def main(check):
    changed = 0
    for n in floors():
        path, d, ok = plan(n)
        if not ok:
            continue
        data = guard(path, d)
        if data is None:
            continue
        new = json.dumps(data, indent=2, ensure_ascii=False) + '\n'
        old = open(path, encoding='utf-8', newline='').read()
        if new != old:
            changed += 1
            if not check:
                with open(path, 'w', encoding='utf-8', newline='\n') as f:
                    f.write(new)
    print(('would change' if check else 'wrote'), changed, 'floors')
    return 1 if (check and changed) else 0


if __name__ == '__main__':
    sys.exit(main('--check' in sys.argv))
