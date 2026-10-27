#!/usr/bin/env python3
"""
Stop floors (Docs/Story.md "Stop floors"): four floors where nothing attacks and the hero has ~20 s of quiet with one thing to find.

  957 The Hot Bath     (Circle X)  a cooled spring, damned souls soaking, a soul's line, a heal
  959 Hell's Graffiti  (Circle X)  six carvings from earlier climbers, the last signed by Rook
  897 Rook's Camp      (Circle IX) Rook waits; the hero's reply is where he stands (sit = heal, walk on = diamonds)
  893 The Stuck Imp    (Circle IX) an imp repeats the same failed ember throw; funny on a rewind

They take the place of four long floors in their slots (same file, same level number, same nextLevelNumber), so the numbering does not shift.
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
    return {'type': 'playerInZone', 'center': [cx, cy], 'size': [w, h], 'anchor': 'World'}


def counter(name, at_least=1.0, below=None):
    t = {'type': 'counter', 'name': name, 'atLeast': at_least}
    if below is not None:
        t['below'] = below
    return t


def all_of(*ts):
    return {'type': 'allOf', 'triggers': list(ts)}


def spawn(entity, at, sid=None, **kw):
    s = {'type': 'spawn', 'entity': entity, 'anchor': 'World', 'at': list(at), 'facePlayer': False}
    if sid:
        s['id'] = sid
    s.update(kw)
    return s


# ------------------------------------------------------------------------------------------------ the shared shape
def ledge(at, sc=0.9, **kw):
    s = {'entity': kw.pop('entity', 'ledge'), 'at': list(at), 'scale': [sc, 1]}
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
        rule(f'the landing, Floor {floor}', all_of({'type': 'playerOn', 'target': {'id': 'landing'}}, counter('restOver')),
             seq(emit(f'stop:{key}:done'), {'type': 'setFloor', 'floor': nxt, 'skipMilestones': True},
                 {'type': 'setCounter', 'name': 'lavaSpeed', 'value': 0.0},
                 clear, wait(2.6), {'type': 'offerUpgrades', 'title': 'Pick a grudge'},
                 {'type': 'setCounter', 'name': 'cleared', 'value': 1.0})),
    ]
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
    return dict(floor=f, key='graffiti', name="Hell's Graffiti", circle='emberWastes', walk=walk, props=[], rules=rules,
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


FLOORS = [hot_bath, graffiti, camp, stuck_imp]


def main():
    write_content()
    for make in FLOORS:
        build_floor(make())
    write_audio()
    print('wrote stop floors 957, 959, 897, 893;', len(SOUNDS), 'line prompts')


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
