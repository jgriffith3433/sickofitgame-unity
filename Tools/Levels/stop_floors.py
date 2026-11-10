#!/usr/bin/env python3
"""
Stop floors (Docs/Story.md "Stop floors"): eight floors where nothing attacks and the hero has ~20 s of quiet with one thing to find.

  957 The Hot Bath     (Circle X)  a cooled spring, damned souls soaking, a soul's line, a heal
  959 Hell's Graffiti  (Circle X)  six carvings from earlier climbers, the last signed by Rook
  897 Rook's Camp      (Circle IX) Rook waits; the hero's reply is where he stands (sit = heal, walk on = diamonds)
  893 The Stuck Imp    (Circle IX) an imp repeats the same failed ember throw; funny on a rewind
  952 The Waystone Shrine (Circle X) a stone that mends: rest for a heart, or fill every heart for a Grudge (offerShortcut)
  856 The Sleeping Demon (Circle IX) stealth: a demon asleep under the road; a shot passing or a landing on it wakes it; tiptoe past for diamonds
  853 The Damned Parade (Circle IX) souls repeat a village fair; on the third loop one turns to watch the hero
  809 The Loop Room    (Circle IX) the road dips through a room; a soul there replays your last jump after a rewind

They take the place of eight long floors in their slots (same file, same level number, same nextLevelNumber), so the numbering does not shift.
long_floors.py and cracked_floors.py skip them (STOP_FLOORS there). Writes the levels, their landings' carved words, the content they use and
audio.json prompts for the lines. The clips are not made (no voice budget yet): ids in Tools/Audio/pending_clips.json, and the showTexts carry no
`sound` until WIRE_SOUNDS is True (run `python3 Tools/Levels/stop_floors.py --wire` after the clips exist).

  python3 Tools/Levels/stop_floors.py [--wire]       write the floors
  python3 Tools/Levels/stop_floors.py --check        no Unity: every floor is quiet, reachable, its lines have prompts (exit 1 on a problem)
"""
import json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
CFG = os.path.join(ROOT, 'Assets', 'GameConfig')
LEV = os.path.join(CFG, 'levels')
ENT = os.path.join(CFG, 'content', 'entities')
AUDIO = os.path.join(CFG, 'audio.json')
PENDING = os.path.join(ROOT, 'Tools', 'Audio', 'pending_clips.json')

WIRE_SOUNDS = '--wire' in sys.argv
GOLD = '#FFD27A'
SOUL = '#B9C4D0'
ROOKC = '#F2A93B'
CALM = 20.0                  # seconds of quiet before the landing opens
WALK_Y = 1.65
STEP = 1.8                   # a ledge at scale 0.9 is 1.8 wide: contiguous

VOICE_OF = {'hero': 'player', 'announcer': 'announcer', 'soul': 'soul', 'rook': 'rook'}
SOUNDS = []                  # (id, speaker, prompt)


def load(path):
    with open(path, encoding='utf-8') as f:
        return json.load(f)


def rounded(o):
    if isinstance(o, float):
        return round(o, 3)
    if isinstance(o, list):
        return [rounded(v) for v in o]
    if isinstance(o, dict):
        return {k: rounded(v) for k, v in o.items()}
    return o


def dump(path, data):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write('\n')


# ------------------------------------------------------------------------------------------------ steps
def say(floor, tag, who, text, prompt, seconds, color=None):
    """A showText; the line's audio.json prompt is registered. Subtitles carry the speaker for souls and Rook, as the climbers' scenes do."""
    sid = f'vo_stop{floor}_{tag}'
    SOUNDS.append((sid, who, prompt))
    shown = {'soul': 'Soul: ', 'rook': 'Rook: '}.get(who, '') + text
    step = {'type': 'showText', 'text': shown, 'duration': seconds, 'style': 'Subtitle'}
    if WIRE_SOUNDS:
        step['sound'] = sid
    if color:
        step['color'] = color
    return step


def banner(floor, tag, text, prompt, seconds=2.6):
    sid = f'vo_stop{floor}_{tag}'
    SOUNDS.append((sid, 'announcer', prompt))
    step = {'type': 'showText', 'text': text, 'duration': seconds, 'style': 'Banner', 'color': GOLD}
    if WIRE_SOUNDS:
        step['sound'] = sid
    return step


def wait(s):
    return {'type': 'wait', 'seconds': s}


def emit(sig):
    return {'type': 'emit', 'signal': sig}


def seq(*steps):
    return {'type': 'runSequence', 'steps': list(steps)}


def rule(name, when, *do, max_times=1):
    return {'name': name, 'when': when, 'maxTimes': max_times, 'do': list(do)}


def section(name):
    return {'type': 'sectionReached', 'section': name}


def zone(cx, cy, w, h):
    return {'type': 'playerInZone', 'center': [round(cx, 2), round(cy, 2)], 'size': [round(w, 2), round(h, 2)], 'anchor': 'World'}


def counter(name, at_least=1.0, below=None):
    t = {'type': 'counter', 'name': name, 'atLeast': at_least}
    if below is not None:
        t['below'] = below
    return t


def all_of(*ts):
    return {'type': 'allOf', 'triggers': list(ts)}


def spawn(entity, at, sid=None, **kw):
    s = {'type': 'spawn', 'entity': entity, 'anchor': 'World', 'at': [round(v, 2) for v in at], 'facePlayer': False}
    if sid:
        s['id'] = sid
    s.update(kw)
    return s


# ------------------------------------------------------------------------------------------------ the shared shape
def ledge(at, sc=0.9, **kw):
    s = {'entity': kw.pop('entity', 'ledge'), 'at': [round(v, 2) for v in at], 'scale': [sc, 1]}
    s.update(kw)
    return s


def walkway(x0, count, extra=None, y=WALK_Y):
    """Contiguous ledges from x0, `count` of them; `extra` maps an index to an entity name (a carving in the row)."""
    extra = extra or {}
    return [ledge((x0 + i * STEP, y), entity=extra.get(i, 'ledge')) for i in range(count)]


def build_floor(spec):
    floor, nxt = spec['floor'], spec['floor'] - 1
    path_file = os.path.join(LEV, f'Floor{floor}.json')
    d = load(path_file)
    walk = spec['walk']
    last_x = max(s['at'][0] for s in walk)
    stop_len = round(last_x - 3.0, 2)
    landing = [last_x + 0.9 + 3.0, WALK_Y + 1.4]
    world = {
        'camera': 'Travel', 'travelLead': 1.2, 'followSharpness': 4.0, 'holdForPushes': False,
        'path': [
            {'name': 'ledges', 'direction': 'Up', 'length': 1.45, 'turn': 2.0, 'spawns': [
                {'entity': 'lavaSea', 'at': [0.0, -5.6], 'id': 'lavaSea'},
                {'entity': 'ledge', 'at': [-3.6, -2.45], 'id': 'start', 'scale': [2.0, 1]},
                {'entity': 'ledge', 'at': [-1.2, -1.15], 'scale': [0.9, 1]},
                {'entity': 'ledge', 'at': [-3.6, 0.15], 'scale': [1.0, 1]}]},
            {'name': 'step', 'direction': 'Up', 'length': 1.5, 'turn': 2.0, 'spawns': []},
            {'name': 'stop', 'direction': 'Right', 'length': stop_len, 'turn': 2.0, 'spawns': walk + spec.get('props', [])},
            {'name': 'landing', 'direction': 'Right', 'length': 5.0, 'turn': 2.0,
             'spawns': [{'entity': f'floorLanding{floor}', 'at': landing, 'id': 'landing'}]},
        ]}
    key = spec['key']
    title = banner(floor, 'title', f'FLOOR {floor}: {spec["name"].upper()}', spec['titlePrompt'])
    clear = banner(floor, 'clear', spec['clear'], spec['clearPrompt'], 2.4)
    rules = [
        rule(f'stop floor: the quiet, Floor {floor}', section('stop'),
             seq(emit(f'stop:{key}:enter'), wait(CALM), {'type': 'setCounter', 'name': 'restOver', 'value': 1.0}, emit(f'stop:{key}:calm'))),
        rule(f'stop floor: not yet, Floor {floor}', all_of({'type': 'playerOn', 'target': {'id': 'landing'}}, counter('restOver', 0.0, 1.0)),
             seq(say(floor, 'early', 'hero', "I'm not in a hurry.", '[flat] I\'m not in a hurry.', 2.2))),
    ] + spec['rules'] + [
    ]
    guard = spec.get('noPickWhen')       # a trigger that skips the Grudge pick (the shrine's fill paid a Grudge already)
    on_landing = all_of({'type': 'playerOn', 'target': {'id': 'landing'}}, counter('restOver'))
    land_steps = [emit(f'stop:{key}:done'), {'type': 'setFloor', 'floor': nxt, 'skipMilestones': True},
                  {'type': 'setCounter', 'name': 'lavaSpeed', 'value': 0.0}, clear, wait(2.6)]
    done_steps = [{'type': 'setCounter', 'name': 'cleared', 'value': 1.0}]
    if guard:
        rules.append(rule(f'the landing, Floor {floor}', all_of(on_landing, {'type': 'not', 'trigger': guard}),
                          seq(*land_steps, {'type': 'offerUpgrades', 'title': 'Pick a grudge'}, *done_steps)))
        rules.append(rule(f'the landing, Floor {floor}, no pick', all_of(on_landing, guard), seq(*land_steps, *done_steps)))
    else:
        rules.append(rule(f'the landing, Floor {floor}', on_landing,
                          seq(*land_steps, {'type': 'offerUpgrades', 'title': 'Pick a grudge'}, *done_steps)))
    d['displayName'] = f'Floor {floor}: {spec["name"]}'
    d['duration'] = 240.0
    d['script'] = {
        'seed': floor, 'length': 60,
        'floors': {'start': floor, 'end': nxt, 'unitsPerFloor': 0.0, 'circle': spec['circle'], 'signs': False},
        'world': world,
        'objective': {'complete': {'type': 'counter', 'name': 'cleared', 'atLeast': 1}, 'text': 'Climb'},
        'counters': [{'name': 'lavaSpeed', 'value': 0.0}, {'name': 'lavaMax', 'value': 0.0}, {'name': 'restOver', 'value': 0.0}] + spec.get('counters', []),
        'rules': rules,
        'phases': [{'name': 'ledge', 'onEnter': [seq(title)]}],
    }
    d['script'] = rounded(d['script'])
    dump(path_file, d)
    # the landing's carved words
    lf = os.path.join(ENT, f'floorLanding{floor}.json')
    e = load(lf)
    e['abilities'][0]['words'] = spec['name'].upper()
    dump(lf, e)


# ------------------------------------------------------------------------------------------------ 957 The Hot Bath
def hot_bath():
    f = 957
    walk = walkway(-2.4, 8)
    pool_x = 5.4
    z = zone(pool_x, 2.3, 4.6, 2.6)
    props = [{'entity': 'bathPool', 'at': [pool_x, 1.98], 'id': 'pool'}]
    for i, (dx, dy) in enumerate([(-1.1, 0.5), (0.0, 0.7), (1.2, 0.45)]):
        props.append({'entity': 'damnedSoul', 'at': [pool_x + dx, 1.98 + dy], 'id': f'soaker{i}'})
    rules = [
        rule('the bath: first look', section('stop'), seq(wait(1.8), say(f, 'quiet', 'hero', "It's quiet. I don't like it.", "[flat] It's quiet. [beat] I don't like it.", 2.6))),
        rule('the bath: the soul speaks, the hero soaks', z,
             seq(emit('stop:hotBath:discovery'), wait(0.5),
                 say(f, 'careful', 'soul', "Careful. It's the one thing down here that isn't trying to burn you.", "[whispering] Careful. [beat] It's the one thing down here that isn't trying to burn you.", 4.2, SOUL),
                 wait(4.4),
                 say(f, 'puddle', 'hero', "It's a puddle.", '[deadpan] It\'s a puddle.', 2.0),
                 wait(2.3),
                 say(f, 'spring', 'soul', "It's a spring. It cooled a century ago. ...Nobody told the Overlord.", "[whispering] It's a spring. It cooled a century ago. [pause] Nobody told the Overlord.", 4.4, SOUL),
                 wait(4.7),
                 {'type': 'heal', 'target': {'player': True}, 'amount': 1},
                 {'type': 'popText', 'text': '+1 heart', 'color': '#7FE0D0', 'at': [pool_x, 3.4]},
                 emit('stop:hotBath:healed'),
                 wait(1.0),
                 say(f, 'helped', 'hero', 'Fine. That helped.', '[sighs] Fine. [beat] That helped.', 2.4))),
        rule('the bath: a soul asks for nothing', all_of(z, {'type': 'counter', 'name': 'restOver'}),
             seq(say(f, 'stay', 'soul', "Stay as long as you like. Everyone does.", "[whispering] Stay as long as you like. [beat] Everyone does.", 3.4, SOUL))),
    ]
    return dict(floor=f, key='hotBath', name='The Hot Bath', circle='emberWastes', walk=walk, props=props, rules=rules,
                titlePrompt='[shouting] Floor nine fifty-seven! [pause] The Hot Bath!', clear='THE HOT BATH: SOAKED',
                clearPrompt='[shouting] The Hot Bath... SOAKED!')


# ------------------------------------------------------------------------------------------------ 959 Hell's Graffiti
GRAFFITI = [
    ('WENT UP.', 'CAME DOWN.', None),
    ("DON'T TRUST", 'THE FLOOR.', None),
    ('ONE MORE', 'FLOOR. ONE.', ('third', 'Everyone says one.', '[flat] Everyone says one.', 2.4)),
    ('THE LAVA IS', 'NOT A SHORTCUT.', None),
    ('SOMEONE KEEPS', 'SIGNING MY NAME.', None),
    ('ROOK WAS HERE.', 'SECOND.', ('rook', 'Rook.', '[sighs] Rook.', 1.8)),
]


def graffiti():
    f = 959
    carve = {}
    for i in range(6):
        carve[i * 2 + 1] = f'graffiti{i + 1}'
    walk = walkway(-2.4, 13, carve)
    rules = []
    for i, (a, b, react) in enumerate(GRAFFITI):
        x = -2.4 + (i * 2 + 1) * STEP
        steps = []
        if i == 0:
            steps.append(emit('stop:graffiti:discovery'))
        steps.append(emit(f'stop:graffiti:read{i + 1}'))
        if react:
            steps += [wait(0.6), say(f, react[0], 'hero', react[1], react[2], react[3])]
        if i == 5:
            steps += [wait(1.2), spawn('diamond', (x, WALK_Y + 1.6)), emit('stop:graffiti:diamond')]
        rules.append(rule(f'graffiti {i + 1}', zone(x, WALK_Y + 1.0, 3.0, 2.2), seq(*steps)))
    rules.insert(0, rule('graffiti: first look', section('stop'),
                         seq(wait(1.6), say(f, 'walls', 'hero', 'Somebody had time.', '[flat] Somebody had time.', 2.4))))
    # the living bar: souls and a fire along the wall, a one-shot every ~4 s, a gap probe signal every 4 s
    props = [{'entity': 'campFire', 'at': [-2.4 + 2 * STEP, WALK_Y + 0.5], 'id': 'wallFire'}]
    for i, dx in enumerate([3, 6, 9, 11]):
        props.append({'entity': 'damnedSoul', 'at': [-2.4 + dx * STEP + 0.4, WALK_Y + 1.0 + 0.1 * (i % 2)], 'id': f'reader{i}'})
    beats = []
    for i in range(30):
        beats += [wait(4.0), {'type': 'playSound', 'sound': ('lava_bubble', 'lava_burst')[i % 2]}, emit('stop:graffiti:life')]
    rules.append(rule('graffiti: ambient bed', section('stop'), seq(*beats)))
    rules.append(rule('graffiti: the last carving shakes', zone(-2.4 + 11 * STEP, WALK_Y + 1.0, 3.0, 2.2),
                      seq(wait(0.3), {'type': 'cameraShake', 'intensity': 0.1, 'duration': 0.6},
                          {'type': 'playSound', 'sound': 'vo_mk_chuckle'}, emit('stop:graffiti:beat'))))
    return dict(floor=f, key='graffiti', name="Hell's Graffiti", circle='emberWastes', walk=walk, props=props, rules=rules,
                titlePrompt="[shouting] Floor nine fifty-nine! [pause] Hell's Graffiti!", clear="HELL'S GRAFFITI: READ",
                clearPrompt="[shouting] Hell's Graffiti... READ!")


# ------------------------------------------------------------------------------------------------ 897 Rook's Camp
def camp():
    f = 897
    walk = walkway(-2.4, 8)
    camp_x = 4.4
    props = [
        {'entity': 'ledge', 'at': [camp_x, 0.15], 'id': 'campLedge', 'scale': [1.6, 1]},
        {'entity': 'campFire', 'at': [camp_x - 0.9, 0.58], 'id': 'campFire'},
    ]
    last_x = max(s['at'][0] for s in walk)
    sit = zone(camp_x, 0.9, 3.2, 1.4)
    go = zone(last_x - 1.0, WALK_Y + 1.2, 3.0, 2.2)
    open_ = counter('campOpen')
    unanswered = counter('campReply', 0.0, 1.0)
    rules = [
        rule('camp: Rook waits', section('stop'),
             seq(spawn('rook', (camp_x + 0.6, 0.95), 'rook'), emit('stop:camp:discovery'), wait(1.5),
                 say(f, 'fire', 'rook', 'I made a fire! Out of fire. ...It was quicker.', 'I made a fire! [beat] Out of fire. [pause] It was quicker.', 4.4, ROOKC),
                 wait(4.7),
                 say(f, 'sit', 'rook', "Sit. Or don't. I won't say anything. ...Sit?", "Sit. Or don't. I won't say anything. [pause] Sit?", 3.8, ROOKC),
                 wait(3.6), {'type': 'setCounter', 'name': 'campOpen', 'value': 1.0}, emit('stop:camp:asked'))),
        rule('camp: sit', all_of(sit, open_, unanswered),
             seq({'type': 'setCounter', 'name': 'campReply', 'value': 1.0}, emit('stop:camp:reply:sit'),
                 say(f, 'five', 'hero', 'Five minutes.', '[sighs] Five minutes.', 2.0),
                 wait(2.3),
                 say(f, 'win', 'rook', "Five minutes! I'm winning. ...Of what, I'll work out later.", "Five minutes! I'm winning. [pause] Of what, I'll work out later.", 4.6, ROOKC),
                 {'type': 'heal', 'target': {'player': True}, 'amount': 1},
                 {'type': 'popText', 'text': '+1 heart', 'color': '#7FE0D0', 'at': [camp_x, 2.4]},
                 {'type': 'setFlag', 'flag': 'rook.camp'}, {'type': 'setFlag', 'flag': 'rook.sat'},
                 emit('stop:camp:done:sit'))),
        rule('camp: walk on', all_of(go, open_, unanswered),
             seq({'type': 'setCounter', 'name': 'campReply', 'value': 1.0}, emit('stop:camp:reply:walkOn'),
                 say(f, 'cant', 'hero', "Can't. Up.", "[flat] Can't. [beat] Up.", 1.8),
                 wait(2.1),
                 say(f, 'behind', 'rook', "Right. Up. ...I'm right behind you.", "Right. Up. [pause] I'm right behind you.", 3.4, ROOKC),
                 spawn('diamond', (last_x - 0.6, WALK_Y + 1.2)), spawn('diamond', (last_x - 0.1, WALK_Y + 1.2)),
                 {'type': 'setFlag', 'flag': 'rook.camp'},
                 emit('stop:camp:done:walkOn'))),
    ]
    return dict(floor=f, key='camp', name="Rook's Camp", circle='crackedRoad', walk=walk, props=props, rules=rules,
                counters=[{'name': 'campOpen', 'value': 0.0}, {'name': 'campReply', 'value': 0.0}],
                titlePrompt="[shouting] Floor eight ninety-seven! [pause] Rook's Camp!", clear="ROOK'S CAMP: LEFT BEHIND",
                clearPrompt="[shouting] Rook's Camp... LEFT BEHIND!")


# ------------------------------------------------------------------------------------------------ 893 The Stuck Imp
def stuck_imp():
    f = 893
    walk = walkway(-2.4, 8)
    last_x = max(s['at'][0] for s in walk)
    imp_at = [last_x + 3.4, -0.25]
    props = [
        {'entity': 'ledge', 'at': [imp_at[0], imp_at[1] - 0.45], 'id': 'impLedge', 'scale': [1.4, 1]},
        {'entity': 'stuckImp', 'at': imp_at, 'id': 'stuck'},
    ]
    thrown = 'spawned:stuckEmber'
    rules = [
        rule('stuck imp: first look', section('stop'),
             seq(wait(1.5), say(f, 'down', 'hero', 'An imp. Down there.', '[flat] An imp. [beat] Down there.', 2.4))),
        rule('stuck imp: it throws', counter(thrown, 1.0), emit('stop:stuckImp:discovery')),
        rule('stuck imp: same throw', counter(thrown, 2.0),
             seq(say(f, 'same', 'hero', 'Same throw.', '[deadpan] Same throw.', 1.8))),
        rule('stuck imp: same miss', counter(thrown, 4.0),
             seq(say(f, 'miss', 'hero', 'Same miss.', '[deadpan] Same miss.', 1.8))),
        rule('stuck imp: rewound, still short', all_of(counter(thrown, 1.0), {'type': 'heroRewound', 'within': 4.0}),
             seq(wait(3.0), emit('stop:stuckImp:rewound'),
                 say(f, 'short', 'hero', 'Rewound it. Still short.', '[amused, deadpan] Rewound it. [beat] Still short.', 2.8)), max_times=2),
        rule('stuck imp: for the record', counter(thrown, 7.0),
             seq(say(f, 'feel', 'hero', "I'd feel bad. Almost.", "[sighs] I'd feel bad. [beat] Almost.", 2.6))),
    ]
    return dict(floor=f, key='stuckImp', name='The Stuck Imp', circle='crackedRoad', walk=walk, props=props, rules=rules,
                titlePrompt='[shouting] Floor eight ninety-three! [pause] The Stuck Imp!', clear='THE STUCK IMP: STILL SHORT',
                clearPrompt='[shouting] The Stuck Imp... STILL SHORT!')


# ------------------------------------------------------------------------------------------------ 952 The Waystone Shrine
def next_level(floor):
    return load(os.path.join(LEV, f'Floor{floor}.json'))['nextLevelNumber']


def shrine():
    f = 952
    walk = walkway(-2.4, 8)
    stone_x = -2.4 + 4 * STEP
    props = [{'entity': 'shrineStone', 'at': [stone_x, 1.98], 'id': 'shrineStone'},
             {'entity': 'damnedSoul', 'at': [stone_x + 1.6, 2.5], 'id': 'keeper'}]
    z = zone(stone_x, 2.3, 3.6, 2.6)
    offer = {'type': 'offerShortcut', 'id': 'waystoneShrine', 'prompt': 'The Waystone Shrine', 'title': 'Fill every heart',
             'description': 'Kneel at the stone and it mends all of you. It keeps a Grudge for the trouble.',
             'stayTitle': 'Just rest', 'stayDescription': 'A heart, and a minute of quiet. You keep your Grudges.',
             'costGrudges': 1, 'costText': 'Costs a Grudge: {0}', 'cantPayText': 'NO GRUDGE TO GIVE',
             'toFloor': f - 1, 'nextLevel': next_level(f), 'color': '#9CE8D8'}
    taken = {'type': 'shortcutChosen', 'shortcut': 'waystoneShrine', 'taken': True}
    left = {'type': 'shortcutChosen', 'shortcut': 'waystoneShrine', 'taken': False}
    rules = [
        rule('the shrine: first look', section('stop'), seq(wait(1.8), say(f, 'stone', 'hero', 'A shrine. In Hell. Sure.', '[flat] A shrine. [beat] In Hell. [beat] Sure.', 2.6))),
        rule('the shrine: the keeper offers', z,
             seq(emit('stop:shrine:discovery'), wait(0.5),
                 say(f, 'kneel', 'soul', "Kneel. Resting is free. ...Mending costs.", "[whispering] Kneel. [beat] Resting is free. [pause] Mending costs.", 4.0, SOUL),
                 wait(4.3), offer)),
        rule('the shrine: rest', left,
             seq({'type': 'heal', 'target': {'player': True}, 'amount': 1},
                 {'type': 'popText', 'text': '+1 heart', 'color': '#7FE0D0', 'at': [round(stone_x, 2), 3.4]},
                 emit('stop:shrine:rest'),
                 say(f, 'rest', 'hero', 'A minute of nothing. Fine.', '[sighs] A minute of nothing. [beat] Fine.', 2.6))),
        rule('the shrine: fill', taken,
             seq({'type': 'heal', 'target': {'player': True}, 'amount': 99},
                 {'type': 'popText', 'text': 'EVERY HEART', 'color': '#9CE8D8', 'at': [round(stone_x, 2), 3.4]},
                 emit('stop:shrine:filled'),
                 say(f, 'whole', 'soul', "All of you. Back. ...Hell keeps the receipt.", "[whispering] All of you. [beat] Back. [pause] Hell keeps the receipt.", 4.0, SOUL),
                 wait(4.3),
                 say(f, 'new', 'hero', 'Good as new. Worse, but new.', '[deadpan] Good as new. [beat] Worse, but new.', 2.8))),
    ]
    return dict(floor=f, key='shrine', name='The Waystone Shrine', circle='emberWastes', walk=walk, props=props, rules=rules, noPickWhen=taken,
                titlePrompt='[shouting] Floor nine fifty-two! [pause] The Waystone Shrine!', clear='THE WAYSTONE SHRINE: KNELT',
                clearPrompt='[shouting] The Waystone Shrine... KNELT!')


# ------------------------------------------------------------------------------------------------ 856 The Sleeping Demon
def sleeping_demon():
    f = 856
    walk = walkway(-2.4, 10)
    demon_x = -2.4 + 4 * STEP
    props = [
        {'entity': 'ledge', 'at': [demon_x, 0.05], 'id': 'demonLedge', 'scale': [2.0, 1]},
        {'entity': 'sleepingDemon', 'at': [demon_x, 0.62], 'id': 'demon'},
    ]
    near = zone(demon_x, 2.3, 5.4, 2.6)
    under = zone(demon_x, 0.95, 2.0, 1.0)             # his back: the ledge top, where a drop from the road lands
    past_x = demon_x + 4.0
    past = zone(past_x, 2.3, 2.4, 2.6)
    quiet = all_of(counter('demonEver', 0.0, 1.0), counter('demonShot', 0.0, 1.0))
    rules = [
        rule('demon: first look', section('stop'), seq(wait(1.6), say(f, 'big', 'hero', 'Big. Asleep. Keep it that way.', '[low, flat] Big. [beat] Asleep. [beat] Keep it that way.', 3.0))),
        rule('demon: asleep', near,
             seq(emit('stop:sleepingDemon:discovery'), {'type': 'popText', 'text': 'zzz', 'color': '#B9C4D0', 'at': [round(demon_x, 2), 1.5]}, wait(0.8),
                 say(f, 'snore', 'hero', 'It snores in time with the lava.', '[whispering, amused] It snores in time with the lava.', 3.2))),
        rule('demon: landed on', under, seq({'type': 'setCounter', 'name': 'demonLanded', 'value': 1.0}, {'type': 'setCounter', 'name': 'demonEver', 'value': 1.0},
                                            emit('stop:sleepingDemon:landed'))),
        rule('demon: a shot passed', {'type': 'witness', 'what': 'ShotPassed', 'within': 3.0, 'watcher': 'demon'},
             seq({'type': 'setCounter', 'name': 'demonShot', 'value': 1.0}, emit('stop:sleepingDemon:shot'))),
        rule('demon: tiptoed past', all_of(past, quiet),
             seq(emit('stop:sleepingDemon:stealth'), wait(0.4),
                 say(f, 'past', 'hero', "Didn't even breathe.", "[whispering, pleased] Didn't even breathe.", 2.4),
                 spawn('diamond', (past_x - 0.5, WALK_Y + 1.5)), spawn('diamond', (past_x + 0.1, WALK_Y + 1.5)), spawn('diamond', (past_x + 0.7, WALK_Y + 1.5)),
                 {'type': 'popText', 'text': 'QUIET', 'color': GOLD, 'at': [round(past_x, 2), 3.6]})),
        rule('demon: woken', {'type': 'signal', 'name': 'stop:sleepingDemon:woken'},
             seq(wait(0.6), say(f, 'mine', 'hero', "Well. That one's on me.", "[sighs] Well. [beat] That one's on me.", 2.6))),
    ]
    return dict(floor=f, key='sleepingDemon', name='The Sleeping Demon', circle='crackedRoad', walk=walk, props=props, rules=rules,
                counters=[{'name': 'demonLanded', 'value': 0.0}, {'name': 'demonEver', 'value': 0.0}, {'name': 'demonShot', 'value': 0.0}],
                titlePrompt='[shouting] Floor eight fifty-six! [pause] The Sleeping Demon!', clear='THE SLEEPING DEMON: STILL ASLEEP',
                clearPrompt='[shouting] The Sleeping Demon... STILL ASLEEP!')


# ------------------------------------------------------------------------------------------------ 853 The Damned Parade
FAIR_LEFT = [5.0, 5.6, 6.2]          # where the three walkers start and come back to
FAIR_RIGHT = [9.2, 8.6, 8.0]


def parade():
    f = 853
    walk = walkway(-2.4, 10)
    stall_x = 1.4
    soul_y = WALK_Y + 0.55
    props = [{'entity': 'fairStall', 'at': [stall_x, WALK_Y + 0.33], 'id': 'stall'},
             {'entity': 'paradeSoul', 'at': [stall_x + 1.1, soul_y], 'id': 'hawk'}]
    for i, x in enumerate(FAIR_LEFT):
        props.append({'entity': 'paradeSoul', 'at': [x, soul_y], 'id': f'walker{i}'})

    def go(side, speed=1.7):
        return [{'type': 'walkEntityTo', 'target': f'walker{i}', 'anchor': 'World', 'x': (FAIR_RIGHT if side == 'right' else FAIR_LEFT)[i], 'speed': speed} for i in range(3)]

    pitch = lambda n: say(f, f'pitch{n}', 'soul', 'Step right up! ...Step right up!', '[loudly, sing-song, flat on the repeat] Step right up! [pause, flatter] Step right up!', 1.9, SOUL)
    steps = [wait(1.0)]
    for loop in (1, 2):
        steps += go('right') + [wait(2.6), pitch(loop)] + go('left') + [wait(2.6)]
    steps += go('right') + [wait(2.4), pitch(3)]
    steps += [{'type': 'faceEntity', 'target': 'walker1', 'toward': 'player'}, wait(0.9), emit('stop:parade:discovery'),
              say(f, 'line', 'soul', "...You're not in the line.", "[quietly, surprised] You're not in the line.", 2.6, SOUL), wait(2.9),
              say(f, 'leaving', 'hero', "I'm leaving.", "[flat] I'm leaving.", 1.8), wait(2.1),
              say(f, 'last', 'soul', "That's what the last one said.", "[whispering] That's what the last one said.", 3.0, SOUL), wait(2.6),
              {'type': 'spawn', 'entity': 'rewindOrb', 'anchor': 'Player', 'at': [0.8, 0.9], 'facePlayer': False}, emit('stop:parade:gift')]
    steps += go('left')
    rules = [
        rule('parade: first look', section('stop'), seq(wait(2.0), say(f, 'fair', 'hero', 'A fair. Down here.', '[flat] A fair. [beat] Down here.', 2.4))),
        rule('parade: the fair, three times', section('stop'), seq(*steps)),
        rule('parade: the same line', zone(stall_x + 3.0, 2.3, 4.0, 2.6),
             seq(emit('stop:parade:seen'), wait(1.2), say(f, 'again', 'hero', 'Same as last time. Exactly.', '[deadpan] Same as last time. [beat] Exactly.', 2.8))),
    ]
    return dict(floor=f, key='parade', name='The Damned Parade', circle='crackedRoad', walk=walk, props=props, rules=rules,
                titlePrompt='[shouting] Floor eight fifty-three! [pause] The Damned Parade!', clear='THE DAMNED PARADE: WATCHED',
                clearPrompt='[shouting] The Damned Parade... WATCHED!')


# ------------------------------------------------------------------------------------------------ 809 The Loop Room
def loop_room():
    f = 809
    low = WALK_Y - 1.3
    xs = [-2.4 + i * STEP for i in range(10)]
    walk = [ledge((x, WALK_Y if i < 3 or i > 6 else low)) for i, x in enumerate(xs)]
    room_l, room_r = xs[3] - 0.9, xs[6] + 0.9
    room_x, room_w = (room_l + room_r) / 2, room_r - room_l
    props = [{'entity': 'loopSign', 'at': [room_x, low + 0.33], 'id': 'sign'},
             {'entity': 'paradeSoul', 'at': [room_x + 1.0, low + 0.55], 'id': 'roomSoul'}]
    in_room = zone(room_x, low + 1.6, room_w, 3.6)
    in_air = zone(room_x, low + 2.2, room_w, 1.6)         # above where he stands: he jumped
    echo = all_of({'type': 'heroRewound', 'within': 3.0}, in_room, {'type': 'heroState', 'requires': ['flag:loopRoom.jumped']})
    rules = [
        rule('room: first look', section('stop'), seq(wait(1.6), say(f, 'dip', 'hero', 'The road goes down. Of course it does.', '[flat] The road goes down. [beat] Of course it does.', 3.0))),
        rule('room: the soul', in_room,
             seq(emit('stop:loopRoom:discovery'), wait(0.4),
                 say(f, 'room', 'soul', "Jump. Then rewind. ...I'll do it for you.", "[whispering] Jump. [beat] Then rewind. [pause] I'll do it for you.", 4.0, SOUL))),
        rule('room: he jumped', in_air, seq({'type': 'setFlag', 'flag': 'loopRoom.jumped'}, emit('stop:loopRoom:jumped'))),
        rule('room: the echo', echo,
             seq({'type': 'spawn', 'entity': 'echoSoul', 'anchor': 'Player', 'at': [0.0, -0.2], 'facePlayer': False, 'id': 'echo'},
                 emit('stop:loopRoom:echo'), wait(1.4),
                 say(f, 'got', 'soul', "I've got that one.", "[pleased, whispering] I've got that one.", 2.2, SOUL), wait(2.4),
                 say(f, 'worse', 'hero', 'It jumps worse than me.', '[deadpan] It jumps worse than me.', 2.4),
                 spawn('diamond', (room_x, low + 1.4))), max_times=3),
    ]
    return dict(floor=f, key='loopRoom', name='The Loop Room', circle='crackedRoad', walk=walk, props=props, rules=rules,
                titlePrompt='[shouting] Floor eight oh nine! [pause] The Loop Room!', clear='THE LOOP ROOM: AGAIN',
                clearPrompt='[shouting] The Loop Room... AGAIN!')


# ------------------------------------------------------------------------------------------------ content
def entity(name, body):
    dump(os.path.join(ENT, name + '.json'), {'schemaVersion': 1, **body})
    meta = os.path.join(ENT, name + '.json.meta')
    if not os.path.exists(meta):
        guid = __import__('hashlib').md5(('sickofit-stop-' + name).encode()).hexdigest()
        with open(meta, 'w', newline='\n') as f:
            f.write(f'fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def write_content():
    pool = {'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['bath'],
            'body': {'shape': 'Box', 'size': [3.6, 0.5], 'solid': False},
            'visual': {'scale': 1.0, 'placeholderColor': '#6FD0C8B0', 'placeholderShape': 'Box', 'placeholderLabel': False, 'layer': 'Player',
                       'sortingOrder': -1, 'hurtFlash': False, 'deathPop': False, 'color': '#FFFFFF', 'facesLeft': False},
            'healthBar': {'show': False},
            'abilities': [{'type': 'carved', 'text': 'SOAK.', 'words': 'NO SPLASHING.', 'color': '#BFEFE8', 'size': 0.26, 'above': 1.15, 'onFace': False}]}
    entity('bathPool', pool)
    fire = {'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['camp'],
            'body': {'shape': 'Box', 'size': [0.5, 0.4], 'solid': False},
            'visual': {'scale': 1.0, 'placeholderColor': '#FF8A2AE0', 'placeholderShape': 'Box', 'placeholderLabel': False, 'layer': 'Player',
                       'sortingOrder': 0, 'hurtFlash': False, 'deathPop': False, 'color': '#FFFFFF', 'facesLeft': False},
            'healthBar': {'show': False}, 'abilities': []}
    entity('campFire', fire)
    for i, (a, b, _) in enumerate(GRAFFITI):
        entity(f'graffiti{i + 1}', {'extends': 'loopCarving', 'abilities': [
            {'type': 'carved', 'text': a, 'words': b, 'color': '#E0A060', 'size': 0.26, 'above': 0.95, 'onFace': False}]})
    # The stuck imp: an imp that can't be hurt, drops nothing and throws one ember in one direction. The ember dies after 0.8 s, 2.4 units out.
    entity('stuckEmber', {'extends': 'projectileEnemy', 'abilities': [
        {'type': 'projectile', 'damage': 1, 'hitSound': None}, {'type': 'lifetime', 'seconds': 0.8}]})
    entity('stuckImp', {'extends': 'imp', 'displayName': 'Stuck imp', 'team': 'Neutral', 'health': 0, 'shootable': False, 'onSpawn': [],
                        'hint': 'Harmless. Same throw, every time.', 'note': 'Throws one ember and it never gets anywhere. Neither does he.',
                        'hintVoice': '', 'tags': ['stuckImp'], 'loot': {'chance': 0.0, 'count': 0, 'value': 0}, 'drops': [],
                        'abilities': [{'type': 'faceTarget'},
                                      {'type': 'shoot', 'projectile': 'stuckEmber', 'speed': 3.0, 'aim': 'Direction', 'direction': [-1.0, 0.3],
                                       'muzzle': [-0.1, 0.1], 'interval': 3.2, 'initialDelay': 2.0, 'animTrigger': 'IsAttacking',
                                       'fireOnEvent': 'FireProjectile', 'damage': 1}]})


    # 952: the stone that mends
    entity('shrineStone', {'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['shrine'],
                           'body': {'shape': 'Box', 'size': [0.9, 1.1], 'solid': False},
                           'visual': {'scale': 1.0, 'placeholderColor': '#9CE8D8D0', 'placeholderShape': 'Box', 'placeholderLabel': False, 'layer': 'Player',
                                      'sortingOrder': -1, 'hurtFlash': False, 'deathPop': False, 'color': '#FFFFFF', 'facesLeft': False},
                           'healthBar': {'show': False},
                           'abilities': [{'type': 'carved', 'text': 'REST.', 'words': 'OR PAY.', 'color': '#BFEFE8', 'size': 0.26, 'above': 1.0, 'onFace': False}]})
    # 853 / 809: a damned soul that only stands and is walked by the floor's script (no hover, no orb of its own)
    soul = load(os.path.join(ENT, 'damnedSoul.json'))
    entity('paradeSoul', {'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['damned', 'parade'], 'body': soul['body'], 'visual': soul['visual'],
                          'healthBar': {'show': False}, 'abilities': [{'type': 'lifetime', 'seconds': 600}]})
    echo_visual = json.loads(json.dumps(soul['visual']))
    echo_visual['color'] = '#BFE8FFB0'
    entity('echoSoul', {'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['damned', 'echo'], 'body': soul['body'], 'visual': echo_visual,
                        'healthBar': {'show': False},
                        'abilities': [{'type': 'mover', 'waypoints': [[0.3, 1.0], [0.6, 1.45], [0.9, 1.0], [1.2, 0.0]], 'speed': 4.5, 'pause': 0.0, 'mode': 'PingPong'},
                                      {'type': 'lifetime', 'seconds': 2.6}]})
    entity('fairStall', {'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['fair'],
                         'body': {'shape': 'Box', 'size': [1.6, 0.9], 'solid': False},
                         'visual': {'scale': 1.0, 'placeholderColor': '#E0A060C8', 'placeholderShape': 'Box', 'placeholderLabel': False, 'layer': 'Player',
                                    'sortingOrder': -1, 'hurtFlash': False, 'deathPop': False, 'color': '#FFFFFF', 'facesLeft': False},
                         'healthBar': {'show': False},
                         'abilities': [{'type': 'carved', 'text': 'FAIR.', 'words': 'AGAIN.', 'color': '#E0A060', 'size': 0.26, 'above': 0.95, 'onFace': False}]})
    entity('loopSign', {'team': 'Neutral', 'health': 0, 'shootable': False, 'tags': ['loopRoom'],
                        'body': {'shape': 'Box', 'size': [0.6, 0.6], 'solid': False},
                        'visual': {'scale': 1.0, 'placeholderColor': '#BFE8FF90', 'placeholderShape': 'Box', 'placeholderLabel': False, 'layer': 'Player',
                                   'sortingOrder': -1, 'hurtFlash': False, 'deathPop': False, 'color': '#FFFFFF', 'facesLeft': False},
                        'healthBar': {'show': False},
                        'abilities': [{'type': 'carved', 'text': 'THE LOOP ROOM.', 'words': 'JUMP. REWIND.', 'color': '#BFE8FF', 'size': 0.26, 'above': 1.0, 'onFace': False}]})
    # 856: a demon asleep. Neutral and not shootable (the bot and the hero's shots pass through); a shot passing near, or a landing on his back (the floor's
    # counter), wakes him. Awake he gives a 1.4 s warning, fires slow spreads at the hero for 8 s, then lies back down.
    entity('demonEmber', {'extends': 'projectileEnemy', 'abilities': [{'type': 'projectile', 'damage': 1, 'hitSound': None}, {'type': 'lifetime', 'seconds': 3.2}]})
    entity('sleepingDemon', {
        'extends': 'imp', 'displayName': 'Sleeping demon', 'team': 'Neutral', 'health': 0, 'shootable': False, 'onSpawn': [],
        'hint': 'Asleep. A shot past it or a landing on it wakes it. Walk.', 'note': 'Do not shoot the sleeping demon. I will say it once more, quietly.',
        'hintVoice': '', 'tags': ['sleepingDemon'], 'loot': {'chance': 0.0, 'count': 0, 'value': 0}, 'drops': [],
        'abilities': [{'type': 'faceTarget'}],
        'states': [
            {'name': 'asleep', 'transitions': [{'to': 'awake', 'when': {'type': 'anyOf', 'triggers': [
                {'type': 'witness', 'what': 'ShotPassed', 'within': 3.0}, {'type': 'counter', 'name': 'demonLanded', 'atLeast': 1.0}]}}]},
            {'name': 'awake',
             'onEnter': [{'type': 'setCounter', 'name': 'demonLanded', 'value': 0.0}, {'type': 'emit', 'signal': 'stop:sleepingDemon:woken'},
                         {'type': 'popText', 'text': '!', 'color': '#FF8A3D', 'at': [0.0, 1.2], 'anchor': 'Self', 'size': 1.4},
                         {'type': 'showText', 'text': 'Mmh. ...Mm?', 'duration': 1.6, 'style': 'Subtitle', 'color': '#FF9A5C'}],
             'abilities': [{'type': 'shoot', 'projectile': 'demonEmber', 'pattern': {'type': 'spread', 'count': 3, 'angle': 50}, 'speed': 2.6, 'aim': 'Player',
                            'interval': 2.2, 'initialDelay': 1.4, 'damage': 1}],
             'transitions': [{'to': 'asleep', 'when': {'type': 'after', 'seconds': 8.0}}]}]})


def write_audio():
    audio = load(AUDIO)
    have = {s['id']: s for s in audio['sounds']}
    template = have['vo_f957_nice']
    pending = []
    for sid, who, prompt in SOUNDS:
        pending.append(sid)
        if sid in have:
            have[sid]['prompt'] = prompt
            continue
        s = {k: template[k] for k in template if k not in ('generatedFrom',)}
        s.update({'id': sid, 'kind': 'Voice', 'prompt': prompt, 'voice': VOICE_OF[who], 'clip': ''})
        # before the climbers' block: companions.py rewrites its sounds to the end of the list and --check wants the file as it would write it
        at = next((i for i, x in enumerate(audio['sounds']) if x['id'].startswith(('vo_rook', 'vo_wick', 'vo_pointer', 'vo_hero_cmp_'))), len(audio['sounds']))
        audio['sounds'].insert(at, s)
        have[sid] = s
    dump(AUDIO, audio)
    made = {sid for sid in pending if have.get(sid, {}).get('clip')}
    with open(PENDING, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'comment': 'Voice lines whose prompts are in audio.json but whose clips are not made yet (no voice budget). sound_audit.py and SoundClipTests '
                              'skip them; once a clip exists the id is dropped here by `stop_floors.py --wire`.',
                   'ids': sorted(set(pending) - made)}, f, indent=2)
        f.write('\n')


FLOORS = [hot_bath, graffiti, camp, stuck_imp, shrine, sleeping_demon, parade, loop_room]


def main():
    write_content()
    for make in FLOORS:
        build_floor(make())
    write_audio()
    print('wrote stop floors', ', '.join(str(m()['floor']) for m in FLOORS), ';', len(SOUNDS), 'line prompts')


def check():
    """Static checks, no Unity: each floor is quiet (no hazard, no enemy but the stuck imp, no rising lava), holds the hero CALM seconds, has a discovery
    signal and a landing, and every line of it has a prompt in audio.json (a clip or a pending_clips.json entry)."""
    problems = []
    audio = {s['id']: s for s in load(AUDIO)['sounds']}
    pending = set(load(PENDING)['ids'])
    for make in FLOORS:
        spec = make()
        n = spec['floor']
        d = load(os.path.join(LEV, f'Floor{n}.json'))
        text = json.dumps(d['script'])
        s = d['script']
        for bad in ('startHazard', 'spawnWave', 'generateFloors'):
            if f'"{bad}"' in text:
                problems.append(f'Floor{n}: uses {bad}')
        for c in s['counters']:
            if c['name'] in ('lavaSpeed', 'lavaMax') and c['value'] != 0:
                problems.append(f'Floor{n}: {c["name"]} starts at {c["value"]}')
        for sec in s['world']['path']:
            if 'autoSpeed' in sec:
                problems.append(f'Floor{n}: section {sec["name"]} auto-scrolls')
            for sp in sec['spawns']:
                path = os.path.join(ENT, sp['entity'] + '.json')
                if not os.path.exists(path):
                    problems.append(f'Floor{n}: entity {sp["entity"]} missing')
                    continue
                e = load(path)
                if e.get('team') == 'Enemy' or sp['entity'] in ('imp', 'cinder', 'cinderSpark'):
                    problems.append(f'Floor{n}: hostile {sp["entity"]} on the floor')
        for r in s['rules']:
            for key in ('every', 'randomAfter'):
                if key in json.dumps(r['when']):
                    problems.append(f'Floor{n}: rule {r["name"]} repeats')
        if f'stop:{spec["key"]}:discovery' not in text:
            problems.append(f'Floor{n}: no discovery signal')
        if f'"seconds": {CALM}' not in text:
            problems.append(f'Floor{n}: the quiet is not {CALM} s')
        if 'restOver' not in text or '"id": "landing"' not in text:
            problems.append(f'Floor{n}: the landing is not gated')
        for sid in sorted(set(__import__('re').findall(r'vo_stop%d_\w+' % n, json.dumps(d)))):
            if sid not in audio:
                problems.append(f'{sid}: wired but not in audio.json')
    for sid, a in audio.items():
        if sid.startswith('vo_stop') and not a.get('prompt'):
            problems.append(f'{sid}: no prompt')
        elif sid.startswith('vo_stop') and not a.get('clip') and sid not in pending:
            problems.append(f'{sid}: no clip and not in pending_clips.json')
    for p in problems:
        print('STOP FLOORS:', p)
    print(f'{len(FLOORS)} stop floors, {sum(1 for s in audio if s.startswith("vo_stop"))} line prompts, {len(problems)} problems')
    return 1 if problems else 0


if __name__ == '__main__':
    if '--check' in sys.argv:
        sys.exit(check())
    main()
