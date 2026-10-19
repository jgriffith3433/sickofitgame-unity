"""Shared geometry for floor_check.py: loads a level's path, sections and spawned pieces with their boxes."""
import json, os, re

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'GameConfig')
ENT = os.path.join(ROOT, 'content', 'entities')
_cache = {}

def load_json(path):
    with open(path, encoding='utf-8') as f:
        text = f.read()
    text = re.sub(r'^\s*//.*$', '', text, flags=re.M)
    return json.loads(text)

def entity(name):
    """The entity with its 'extends' chain merged (child fields win; abilities of the child replace by type)."""
    if name in _cache:
        return _cache[name]
    path = os.path.join(ENT, name + '.json')
    if not os.path.exists(path):
        _cache[name] = None
        return None
    d = load_json(path)
    base = entity(d['extends']) if d.get('extends') else None
    if base:
        merged = json.loads(json.dumps(base))
        for k, v in d.items():
            if k == 'abilities':
                byType = {a.get('type'): a for a in merged.get('abilities', [])}
                for a in v:
                    byType[a.get('type')] = a
                merged['abilities'] = list(byType.values())
            elif k == 'body' and v:
                merged['body'] = {**merged.get('body', {}), **v}
            elif v is not None:
                merged[k] = v
        d = merged
    _cache[name] = d
    return d

def ability(e, kind):
    for a in (e or {}).get('abilities', []) or []:
        if a.get('type') == kind:
            return a
    return None

AXIS = {'Up': (0, 1), 'Right': (1, 0), 'Left': (-1, 0), 'UpRight': (0.7071, 0.7071), 'UpLeft': (-0.7071, 0.7071), 'Arena': (0, 0)}

class Piece:
    def __init__(self, sec, spawn, e):
        self.sec = sec
        self.spawn = spawn
        self.name = spawn['entity']
        self.id = spawn.get('id', '')
        self.e = e or {}
        self.at = tuple(spawn['at'])
        body = self.e.get('body') or {}
        sx, sy = (spawn.get('scale') or [1, 1])
        if body.get('shape') == 'Circle' or (body.get('radius') and not body.get('size')):
            r = body.get('radius', 0.5)
            size = (2 * r * sx, 2 * r * sy)
        else:
            s = body.get('size') or [1, 1]
            size = (s[0] * sx, s[1] * sy)
        off = body.get('offset') or [0, 0]
        cx, cy = self.at[0] + off[0] * sx, self.at[1] + off[1] * sy
        self.box = (cx - size[0] / 2, cy - size[1] / 2, cx + size[0] / 2, cy + size[1] / 2)
        self.tags = set(self.e.get('tags') or [])
        self.oneWay = bool(body.get('oneWay'))
        self.onEnter = bool(spawn.get('onEnter'))

    @property
    def top(self):
        return self.box[3]

    def __repr__(self):
        b = self.box
        return f"{self.name}{('#' + self.id) if self.id else ''}[x {b[0]:.2f}..{b[2]:.2f}, top {b[3]:.2f}]"

    def has(self, kind):
        return ability(self.e, kind) is not None

    @property
    def foothold(self):
        return 'foothold' in self.tags

    @property
    def lava(self):
        return 'lava' in self.tags

def level(name):
    d = load_json(os.path.join(ROOT, 'levels', name + '.json'))
    script = d.get('script') or {}
    world = script.get('world') or {}
    path = world.get('path') or []
    sections = []
    at = (0.0, 0.0)
    for i, s in enumerate(path):
        ax = AXIS[s.get('direction', 'Up')]
        length = 0 if s.get('direction') == 'Arena' else s.get('length', 8)
        end = (at[0] + ax[0] * length, at[1] + ax[1] * length)
        sec = {'i': i, 'name': s.get('name', ''), 'dir': s.get('direction', 'Up'), 'start': at, 'end': end,
               'length': length, 'turn': s.get('turn', 2), 'auto': s.get('autoSpeed', 0), 'lead': s.get('lead', -1), 'pieces': []}
        for sp in s.get('spawns', []) or []:
            anchor = sp.get('anchor', 'Path')
            spc = dict(sp)
            if anchor == 'Section':
                spc['at'] = [at[0] + sp['at'][0], at[1] + sp['at'][1]]
            sec['pieces'].append(Piece(i, spc, entity(sp['entity'])))
        sections.append(sec)
        at = end
    return d, script, world, sections
