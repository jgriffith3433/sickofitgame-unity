#!/usr/bin/env python3
"""
Circle VIII's lower half (Docs/Story.md "The Rune Bridges' lower half"): Floors 751-702 and the Bridge Wraith's Great Span (701).

  python3 Tools/Levels/rune_lower.py            write the entities, levels, audio.json rows and game.json entries
  python3 Tools/Levels/rune_lower.py --check    fail if the files differ from what the tables make (no Unity)

751 is the Lamplighter's floor (a floor boss, Travel camera held on an Arena section), 750..702 are short crossings (about 110 s, one
level = one floor: a foot, three set pieces, a far end), 701 is the Bridge Wraith's arena (a Static screen, like the Spire Fiend's).
The path floors are built with rune_floors.py's pieces and spans; edit the tables here, run it, then
`python3 Tools/Levels/floor_check.py Floor750 ...`. Everything written is plain data.
"""
import copy, json, os, random, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rune_floors as rf
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from artkeep import guard  # noqa: E402  (generated art is kept: Tools/Levels/artkeep.py)

CFG = rf.ROOT
ENT = rf.ENT
LEV = rf.LEV
CHECK = '--check' in sys.argv
changed = []


def write_json(path, data):
    data = guard(path, data)
    if data is None:
        return
    text = json.dumps(data, indent=2, ensure_ascii=False) + '\n'
    old = open(path, encoding='utf-8').read() if os.path.exists(path) else None
    if old == text:
        return
    changed.append(os.path.relpath(path, CFG))
    if not CHECK:
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)


def ent(name, data):
    write_json(os.path.join(ENT, name + '.json'), data)


GOLD = rf.GOLD
LAMP = '#BFEFFF'
WRAITH = '#CFE4FF'
LAMPLIGHTER_VOICE = '0XCO5EAvKdZyibxN5Wno'
WRAITH_VOICE = 'CnhuFxa0eyLaYlh9aGEa'

# ---------------------------------------------------------------- new path enemies (no new C#)

ENTITIES = {}

ENTITIES['afterimage'] = {
    'schemaVersion': 1, 'extends': 'chaser', 'displayName': 'Afterimage',
    'hint': 'A pale copy of you, slow. Shoot it from range.',
    'note': 'That is me. Three seconds ago. Looks bored. Accurate.',
    'hintVoice': 'vo_hero_hint_afterimage',
    'onSpawn': [{'type': 'hintOnce'}],
    'health': 2, 'moveSpeed': 1.5, 'activationRange': 12.0, 'tags': ['afterimage'],
    'body': {'shape': 'Circle', 'radius': 0.45, 'solid': False, 'gravity': 0.0},
    'visual': {'scale': 0.15, 'color': '#D8E8FF', 'fxColor': '#BFEFFF', 'placeholderColor': '#D8E8FF'},
    'loot': {'chance': 0.3, 'count': 1, 'value': 1},
    'abilities': [{'type': 'contactDamage', 'damage': 1, 'dieOnHit': True},
                  {'type': 'chase', 'speed': 1.5, 'stopDistance': 0.0, 'face': True, 'animBool': 'IsMoving'},
                  {'type': 'lifetime', 'seconds': 22}],
    'onDeath': [],
}

ENTITIES['runeHound'] = {
    'schemaVersion': 1, 'extends': 'slagHound', 'displayName': 'Rune hound',
    'hint': 'Asleep until you come near. Crouches, then lunges. Sidestep.',
    'note': 'A statue with opinions. Wakes up when I get close. Rude.',
    'hintVoice': 'vo_hero_hint_runeHound',
    'tags': ['runeHound'], 'activationRange': 6.5, 'health': 4,
    'visual': {'scale': 0.22, 'color': '#A9BFDA', 'fxColor': '#8FE3FF', 'placeholderColor': '#6F8CB0'},
}

ENTITIES['sleepwalker'] = {
    'schemaVersion': 1, 'extends': 'chaser', 'displayName': 'Sleepwalker',
    'hint': 'Harmless asleep. A shot wakes it. Wait for it to pass.',
    'note': 'Do not wake it. I repeat: do not wake it. ...Who is going to wake it.',
    'hintVoice': 'vo_hero_hint_sleepwalker',
    'onSpawn': [{'type': 'hintOnce'}],
    'health': 4, 'moveSpeed': 0.0, 'activationRange': 14.0, 'tags': ['sleepwalker'],
    'body': {'shape': 'Circle', 'radius': 0.5, 'solid': False, 'gravity': 0.0},
    'visual': {'scale': 0.18, 'color': '#B9C9E8', 'fxColor': '#8FE3FF', 'placeholderColor': '#B9C9E8'},
    'loot': {'chance': 0.4, 'count': 1, 'value': 1},
    'abilities': [{'type': 'contactDamage', 'damage': 1},
                  {'type': 'patrol', 'speed': 0.7, 'distance': 4.0, 'pause': 0.0, 'animBool': 'IsMoving'},
                  {'type': 'lifetime', 'seconds': 40}],
    'states': [
        {'name': 'asleep', 'transitions': [{'to': 'woken', 'when': {'type': 'healthBelow', 'fraction': 0.99}}]},
        {'name': 'woken',
         'abilities': [{'type': 'shoot', 'projectile': 'runeBolt', 'pattern': {'type': 'spread', 'count': 5, 'angle': 80},
                        'speed': 2.8, 'aim': 'Player', 'interval': 1.6, 'initialDelay': 0.4, 'damage': 1}]},
    ],
    'onDeath': [],
}

ENTITIES['lanternfly'] = {
    'schemaVersion': 1, 'extends': 'chaser', 'displayName': 'Lanternfly',
    'hint': 'Small, bright and fragile. One shot.',
    'note': 'A bug with a lantern. Of course there is.',
    'health': 1, 'moveSpeed': 1.15, 'activationRange': 14.0, 'tags': ['lanternfly'],
    'body': {'shape': 'Circle', 'radius': 0.3, 'solid': False, 'gravity': 0.0},
    'visual': {'scale': 0.1, 'color': '#7FD8FF', 'fxColor': '#7FD8FF', 'placeholderColor': '#7FD8FF'},
    'loot': {'chance': 0.0, 'count': 0, 'value': 0},
    'abilities': [{'type': 'contactDamage', 'damage': 1, 'dieOnHit': True},
                  {'type': 'chase', 'speed': 1.15, 'stopDistance': 0.0, 'face': True, 'animBool': 'IsMoving'},
                  {'type': 'lifetime', 'seconds': 16}],
    'onDeath': [],
}

# ---------------------------------------------------------------- the floors (names, words, the hero's line, who comes)

# n, name, carved words, hero line index, opening enemy, pool
POOLS = {
    'a': ['imp', 'ricochetImp'],
    'b': ['imp', 'bulwarkImp', 'blinkImp'],
    'c': ['ricochetImp', 'leadImp', 'watcher'],
    'd': ['blinkImp', 'bulwarkImp', 'watcher', 'afterimage'],
    'e': ['leadImp', 'watcher', 'afterimage', 'runeHound'],
    'f': ['bulwarkImp', 'blinkImp', 'runeHound', 'sleepwalker'],
    'g': ['leadImp', 'ricochetImp', 'runeHound', 'sleepwalker', 'afterimage'],
}
# (floor, name, carved words, hero line, pool, opening, span kinds of its pieces)
F = [
    (750, 'Halfway Across', 'BRIDGE. HALFWAY.', 0, 'a', ['imp'], 'runeSpan'),
    (749, 'The Lamps Are Out', 'BRIDGE (UNLIT)', 1, 'a', [], 'runeSpanUnison'),
    (748, 'Same Bridge, Again', 'BRIDGE (AGAIN)', 2, 'a', ['ricochetImp'], 'runeSpan'),
    (747, 'Mind The Gap', 'MIND THE GAP', 3, 'a', [], 'runeSpanLate'),
    (746, 'There Is No Gap', 'NO GAP (SORRY)', 4, 'b', ['bulwarkImp'], 'runeSpan'),
    (745, 'The Pale Follower', 'BRIDGE, WALKED', 5, 'b', ['afterimage'], 'runeSpan'),
    (744, 'Three Seconds Behind', 'BRIDGE (BEHIND YOU)', 6, 'b', [], 'runeSpanQuick'),
    (743, 'Walk It Twice', 'BRIDGE (TWICE)', 7, 'b', ['blinkImp'], 'runeSpanUnison'),
    (742, 'The Watchers Compare Notes', 'BRIDGE (NOTED)', 8, 'c', ['watcher'], 'runeSpanGaze'),
    (741, 'A Bridge With A View', 'BRIDGE (VIEW)', 9, 'c', [], 'runeSpan'),
    (740, 'Eyes Left', 'LEFT. NO, THE OTHER LEFT.', 10, 'c', ['watcher'], 'runeSpanGaze'),
    (739, 'Out Of Step Again', 'BRIDGE (OFF)', 11, 'c', ['leadImp'], 'runeSpanUnison'),
    (738, 'The Slow Hum', 'HUM (SLOW)', 12, 'c', [], 'runeSpanHold'),
    (737, 'Hum, Glow, Hum', 'HUM. GLOW. HUM.', 13, 'c', ['ricochetImp'], 'runeSpanQuick'),
    (736, 'Stone Ears', 'BRIDGE (LISTENING)', 14, 'd', [], 'runeSpan'),
    (735, 'The Statue Wakes', 'DOG (STONE)', 15, 'd', ['runeHound'], 'runeSpan'),
    (734, 'Sit', 'SIT', 0, 'd', [], 'runeSpanLate'),
    (733, 'Good Dog, Bad Bridge', 'BRIDGE (GOOD DOG)', 1, 'd', ['runeHound'], 'runeSpanUnison'),
    (732, 'Collar', 'COLLAR (RUNE)', 2, 'd', [], 'runeSpan'),
    (731, 'The Long Walk', 'BRIDGE. MORE BRIDGE.', 3, 'd', ['blinkImp'], 'runeSpanQuick'),
    (730, 'Under The Arch', 'ARCH (ABOVE)', 4, 'd', ['watcher'], 'runeSpanGaze'),
    (729, 'Written On The Wall', 'WALL (WRITTEN)', 5, 'e', [], 'runeSpan'),
    (728, 'Echo Of A Bridge', 'BRIDGE (ECHO)', 6, 'e', ['afterimage'], 'runeSpanUnison'),
    (727, 'Hold Still', 'STILL (BRIDGE)', 7, 'e', [], 'runeSpanHold'),
    (726, 'The Quiet Span', 'QUIET (BRIDGE)', 8, 'e', ['leadImp'], 'runeSpanLate'),
    (725, 'Do Not Wake It', 'DO NOT WAKE IT', 9, 'e', ['sleepwalker'], 'runeSpan'),
    (724, 'It Walks In Its Sleep', 'WALKING (ASLEEP)', 10, 'e', [], 'runeSpanQuick'),
    (723, 'One Way Only', 'ONE WAY', 11, 'e', ['sleepwalker'], 'runeSpanUnison'),
    (722, 'Pass In The Night', 'PASSING (QUIETLY)', 12, 'e', [], 'runeSpan'),
    (721, 'The Dark Between', 'BRIDGE (DARK)', 13, 'f', ['bulwarkImp'], 'runeSpanHold'),
    (720, 'Brighter Than It Was', 'BRIDGE (LIT)', 14, 'f', [], 'runeSpanGaze'),
    (719, 'The Eye And The Hound', 'EYE. HOUND. BRIDGE.', 15, 'f', ['runeHound'], 'runeSpan'),
    (718, 'Behind The Lamps', 'LAMPS (BEHIND)', 0, 'f', [], 'runeSpanLate'),
    (717, 'Everyone Is Asleep', 'ALL ASLEEP', 1, 'f', ['sleepwalker'], 'runeSpanUnison'),
    (716, 'Walking Wake', 'AWAKE (SOME)', 2, 'f', [], 'runeSpanQuick'),
    (715, 'The Three Beats', 'BEAT. BEAT. BEAT.', 3, 'f', ['afterimage'], 'runeSpan'),
    (714, 'Half The Hum', 'HUM (HALF)', 4, 'f', [], 'runeSpanGaze'),
    (713, 'Stone, Eye, Ghost', 'STONE. EYE. GHOST.', 5, 'g', ['runeHound'], 'runeSpanUnison'),
    (712, 'A Bridge Remembers', 'BRIDGE (REMEMBERED)', 6, 'g', [], 'runeSpan'),
    (711, 'Nearly Written', 'NEARLY WRITTEN', 7, 'g', ['blinkImp'], 'runeSpanLate'),
    (710, 'Scribbled Over', 'BRIDGE (SCRIBBLED)', 8, 'g', [], 'runeSpanQuick'),
    (709, 'Crossing Out', 'CROSSED OUT', 9, 'g', ['leadImp'], 'runeSpanUnison'),
    (708, 'The Handprint', 'UP', 10, 'g', [], 'runeSpan'),
    (707, 'Rubbed Out, Written Again', 'UP (AGAIN)', 11, 'g', ['sleepwalker'], 'runeSpanHold'),
    (706, 'He Was Trying Too', 'UP (STILL)', 12, 'g', [], 'runeSpanGaze'),
    (705, 'The Great Span Ahead', 'GREAT SPAN (AHEAD)', 13, 'g', ['watcher'], 'runeSpanQuick'),
    (704, 'The Lantern Is Lit', 'LANTERN (LIT)', 14, 'g', [], 'runeSpanUnison'),
    (703, 'Someone Is Excited', 'SOMEONE (EXCITED)', 15, 'g', ['afterimage'], 'runeSpan'),
    (702, 'Sorry, Sorry', 'SORRY. SORRY.', 0, 'g', [], 'runeSpanLate'),
]
PATH_FLOORS = [f[0] for f in F]

HERO = [
    ('The runes keep saying what they are. I respect that.', '[deadpan] The runes keep saying what they are. [pause] I respect that.'),
    ('Every light behind me, out. Thanks.', '[deadpan] Every light behind me, out. [pause] Thanks.'),
    ('Same bridge. Different bridge. Same.', '[deadpan] Same bridge. [pause] Different bridge. [pause] Same.'),
    ('There is a gap. They did say.', '[deadpan] There is a gap. [pause] They did say.'),
    ('Now there is no gap. Somehow worse.', '[deadpan] Now there is no gap. [pause] Somehow worse.'),
    ('That is me. From earlier. Looks bored. Accurate.', '[deadpan] That is me. [pause] From earlier. [pause] Looks bored. Accurate.'),
    ('Count to the hum. Jump on the glow.', '[deadpan] Count to the hum. [pause] Jump on the glow.'),
    ('Walk it twice. Hell loves a repeat.', '[deadpan] Walk it twice. [pause] Hell loves a repeat.'),
    ('Please stop looking at me. Look at each other.', '[deadpan] Please stop looking at me. [pause] Look at each other.'),
    ('Nice view. Of the thing that wants me dead.', '[deadpan] Nice view. [pause] Of the thing that wants me dead.'),
    ('Do not wake the statue. I repeat, the statue.', '[deadpan] Do not wake the statue. [pause] I repeat. The statue.'),
    ('Half of this is the wrong way round.', '[deadpan] Half of this is the wrong way round.'),
    ('Slow hum. Fast regret.', '[deadpan] Slow hum. [pause] Fast regret.'),
    ('Somebody wrote this bridge in a hurry.', '[deadpan] Somebody wrote this bridge in a hurry.'),
    ('It is just a bridge. Say it with me.', '[deadpan] It is just a bridge. [pause] Say it with me.'),
    ('Whoever lit this knew I was coming.', '[deadpan] Whoever lit this knew I was coming.'),
]
HERO_IDS = ['vo_lb_hero_%02d' % i for i in range(len(HERO))]

# ---------------------------------------------------------------- path floors

SEC_FOOT, SEC_PIECE, SEC_FAR = 6.0, 17.0, 6.2     # 6 + 3 x 17 + 6.2: the last span lands exactly on the 3.8 pitch
FINALE_AT = 88.0
LAYOUT_CYCLE = ['line', 'zig', 'wave']


def pieces_for(i, f):
    n, name, words, hero, pool, opening, kind = f
    rnd = random.Random(n)
    d = i / (len(F) - 1)           # 0 at 750, 1 at 702
    enemies = POOLS[pool]
    ps = []
    for k in range(3):
        ramp = (k + 1) / 3.0
        rain_i = round(2.8 - 1.5 * (0.5 * d + 0.5 * ramp), 2)
        waves = []
        if k >= 1 or pool != 'a':
            ent1 = enemies[rnd.randrange(len(enemies))]
            waves.append((ent1, 1 if d < 0.5 or k == 1 else 2, round(13 - 4 * d, 1), 3 + k, (1.2, 2.2)))
            if d > 0.4 and k == 2:
                ent2 = enemies[rnd.randrange(len(enemies))]
                if ent2 != ent1:
                    waves.append((ent2, 1, round(12 - 3 * d, 1), 6, (1.2, 2.2)))
        gaze = kind == 'runeSpanGaze' and k != 1
        if gaze:
            waves = []
        pk = kind if k != 1 or kind == 'runeSpanGaze' else 'runeSpan'
        mix = 'runeSpanLate' if (kind in ('runeSpanUnison', 'runeSpanLate') and k == 2) else None
        # Hold spans only on a piece's first half: a row that waits for the hero is the breather.
        if kind == 'runeSpanHold' and k == 2:
            pk = 'runeSpanQuick'
        # sleepwalker / hound / afterimage come in on the floor that introduces them, alone first (waves start late).
        piece_name = ['first', 'second', 'third'][k]
        ps.append(rf.piece(piece_name, ['THE FIRST SPAN!', 'THE SECOND SPAN!', 'THE LAST SPAN!'][k] if False else
                           '%s' % ['ACROSS!', 'AND THEN!', 'AND AGAIN!'][k],
                           pk, LAYOUT_CYCLE[(n + k) % 3], pillar=4 + (k % 2), rain=(rain_i, round(0.008 + 0.004 * d, 4)),
                           aimed=(None if k == 0 else round(3.6 - 0.8 * d, 2)), waves=waves, mix=mix, gaze=gaze))
    return ps


def floor_row(i, f):
    n, name, words, hero, pool, opening, kind = f
    nxt = F[i + 1][0] if i + 1 < len(F) else 701
    return dict(n=n, name=name, words=words, hero=hero, opening=opening, pieces=pieces_for(i, f), next=nxt)


def build_path_floor(i, f, position):
    r = floor_row(i, f)
    n = r['n']
    pieces = r['pieces']
    # rune_floors.build_floor with this module's section lengths and timing.
    saved = (rf.SEC_FOOT, rf.SEC_PIECE, rf.SEC_FAR)
    rf.SEC_FOOT, rf.SEC_PIECE, rf.SEC_FAR = SEC_FOOT, SEC_PIECE, SEC_FAR
    fl = dict(n=n, name=r['name'], title='FLOOR %d: %s' % (n, r['name'].upper()), words=r['words'], opening=r['opening'],
              h1=(HERO[r['hero']][0], HERO[r['hero']][1]), h2=('', ''), land=('', ''),
              next_title='FLOOR %d: %s' % (r['next'], next_name(r['next'])), pieces=pieces)
    saved_floors = rf.FLOORS
    rf.FLOORS = [dict(n=n)]            # so rf treats this as the last floor, then fix nextLevelNumber below
    try:
        lv = rf.build_floor(fl, position)
    finally:
        rf.SEC_FOOT, rf.SEC_PIECE, rf.SEC_FAR = saved
        rf.FLOORS = saved_floors
    s = lv['script']
    lv['nextLevelNumber'] = position + 1
    lv['duration'] = 240.0
    s['length'] = 110
    # Voices: the title by the announcer (vo_f<n>_title), one hero line from the shared pool; the second hero line and the landing line are dropped.
    s['rules'] = [x for x in s['rules'] if x['name'] != 'hero: second line']
    for x in s['rules']:
        if x['name'] == 'hero: first line':
            x['when'] = {'type': 'atTime', 'seconds': 12.0}
            x['do'] = [{'type': 'runSequence', 'steps': [{'type': 'showText', 'text': HERO[r['hero']][0], 'duration': 3.0, 'style': 'Subtitle', 'sound': HERO_IDS[r['hero']]}]}]
        if x['name'] == 'the bridge has had its minutes':
            x['when'] = {'type': 'atTime', 'seconds': FINALE_AT}
        if x['name'] == 'the landing':
            steps = x['do'][0]['steps']
            steps[:] = [st for st in steps if not (st.get('style') == 'Subtitle')]
            for st in steps:
                if st.get('style') == 'Banner':
                    st['sound'] = 'vo_f%d_title' % r['next']
                    st['text'] = fl['next_title']
            # wait after the landing banner stays
    lv['script']['floors']['circle'] = 'runeBridges'
    return lv


NAMES = {f[0]: f[1].upper() for f in F}
NAMES[701] = 'THE GREAT SPAN'
NAMES[751] = 'THE LAMPLIGHTER'


def next_name(n):
    return NAMES[n]


def landing_entity(r):
    return rf.landing(dict(n=r['n'], words=r['words']))


def say_floor(n):
    t = {7: 'seven'}
    ones = ['', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine']
    teens = ['ten', 'eleven', 'twelve', 'thirteen', 'fourteen', 'fifteen', 'sixteen', 'seventeen', 'eighteen', 'nineteen']
    tens = ['', '', 'twenty', 'thirty', 'forty', 'fifty']
    rest = n - 700
    if rest == 0:
        return 'seven hundred'
    if rest < 10:
        return 'seven oh ' + ones[rest]
    if rest < 20:
        return 'seven ' + teens[rest - 10]
    a, b = divmod(rest, 10)
    return 'seven ' + tens[a] + ('-' + ones[b] if b else '')


# ---------------------------------------------------------------- audio rows

def voice_row(sid, say, voice, voice_id, speed=1, stability=0.5, model='eleven_v3'):
    return {'id': sid, 'kind': 'Voice', 'prompt': say, 'durationSeconds': 0, 'promptInfluence': 0.3, 'loop': False,
            'voice': voice, 'voiceId': '', 'clip': 'Audio/Generated/%s.mp3' % sid,
            'generatedFrom': 'voice|%s|%s|%s|%s|%s' % (voice_id, model, stability, speed, say), 'volume': 1, 'pitchVariation': 0, 'maxVoices': 1, 'cooldown': 0.5}


def fx_row(sid, prompt, dur, vol=0.9, influence=0.5, voices=2, cooldown=0.3):
    return {'id': sid, 'kind': 'Effect', 'prompt': prompt, 'durationSeconds': dur, 'promptInfluence': influence, 'loop': False, 'voice': '', 'voiceId': '',
            'clip': 'Audio/Generated/%s.mp3' % sid, 'generatedFrom': 'effect|%s|%s|False|%s' % (dur, influence, prompt), 'volume': vol,
            'pitchVariation': 0.05, 'maxVoices': voices, 'cooldown': cooldown}


PL, AN = rf.PLAYER_VOICE, rf.ANNOUNCER_VOICE
P, A, W, L = ('player', PL, 1), ('announcer', AN, 1), ('bridgeWraith', WRAITH_VOICE, 0.9), ('lamplighter', LAMPLIGHTER_VOICE, 0.9)

# (id, spoken prompt with delivery tags, shown text, speaker)  -- the Lamplighter and the Wraith
BOSS_LINES = [
    ('vo_f751_title', '[shouting] Floor seven fifty-one! [pause] The LAMPLIGHTER!', 'FLOOR 751: THE LAMPLIGHTER', A),
    ('vo_ll_name', '[booming] THE LAMPLIGHTER!', 'THE LAMPLIGHTER', A),
    ('vo_ll_intro', '[muttering] One for the left. One for the right. [pause] Oh. You are on the bridge.', 'One for the left. One for the right. ...Oh. You are on the bridge.', L),
    ('vo_ll_hero_intro', "[deadpan] He's lighting the bridge. [pause] Under my feet.", "He's lighting the bridge. Under my feet.", P),
    ('vo_hero_hint_lamplighter', '[calm] The Lamplighter. His whole job is the flame on his pole. So I shoot the flame.', 'The Lamplighter. His whole job is the flame on his pole. So I shoot the flame.', P),
    ('vo_ll_hurt1', '[fussy] Careful. Careful with the pole.', 'Careful. Careful with the pole.', L),
    ('vo_ll_tier2', '[muttering] Two bridges tonight. Hold the eyes, please.', 'Two bridges tonight. Hold the eyes, please.', L),
    ('vo_ll_blind', '[alarmed] My flame! [pause] I cannot see the edge.', 'My flame! ...I cannot see the edge.', L),
    ('vo_ll_defeat', '[dying whisper] Oh. [pause] Is this the edge?', 'Oh. ...Is this the edge?', L),
    ('vo_ll_fallen', '[booming] THE LAMPLIGHTER HAS WALKED OFF THE BRIDGE!', 'THE LAMPLIGHTER HAS WALKED OFF THE BRIDGE!', A),
    ('vo_ll_hero_defeat', '[dry] Well. That is one way off a bridge.', 'Well. That is one way off a bridge.', P),
    ('vo_f701_title', '[shouting] Floor seven oh one! [pause] The GREAT SPAN!', 'FLOOR 701: THE GREAT SPAN', A),
    ('vo_br_warden', '[booming] FLOOR SEVEN OH ONE! [pause] THE WARDEN OF THE RUNE BRIDGES!', 'FLOOR 701! THE WARDEN OF THE RUNE BRIDGES!', A),
    ('vo_br_name', '[booming] THE BRIDGE WRAITH!', 'THE BRIDGE WRAITH', A),
    ('vo_br_intro', '[whispers] Oh. [pause] Oh, hello. Someone needs to cross. [pause] Sorry. It has been so long.', 'Oh. Oh, hello. Someone needs to cross. ...Sorry. It has been so long.', W),
    ('vo_br_hero_intro', "[deadpan] She's already sorry. That's worse.", "She's already sorry. That's worse.", P),
    ('vo_hero_hint_bridgeWraith', '[calm] The Bridge Wraith. The lantern opens when she lights something. So I shoot it then.', 'The Bridge Wraith. The lantern opens when she lights something. So I shoot it then.', P),
    ('vo_br_phase1', '[whispers] Mind the gap. [pause] There is not one. [pause] Sorry.', 'Mind the gap. ...There is not one. Sorry.', W),
    ('vo_br_hurt1', '[gasps] Oh! That was the lantern. Sorry. Sorry.', 'Oh! That was the lantern. Sorry. Sorry.', W),
    ('vo_br_snuff', '[whispers] I will put it out. It is the only way to do it right.', 'I will put it out. It is the only way to do it right.', W),
    ('vo_br_flies', '[whispers] Come along. Come along, little lights.', 'Come along. Come along, little lights.', W),
    ('vo_br_enrage', '[anxious whisper] Unwritten. Unwritten. Please, do not rush me. I will lose my place.', 'Unwritten. Unwritten. Please, do not rush me. I will lose my place.', W),
    ('vo_br_hero_field', "[deadpan] The bridge is unwriting itself. Behind me. [pause] Of course it is.", "The bridge is unwriting itself. Behind me. Of course it is.", P),
    ('vo_br_hurt2', '[trembling whisper] Please. I will write it wrong.', 'Please. I will write it wrong.', W),
    ('vo_br_defeat', '[relieved whisper] Oh. [pause] Thank you for crossing.', 'Oh. ...Thank you for crossing.', W),
    ('vo_br_fallen', '[booming] THE BRIDGE WRAITH HAS FALLEN!', 'THE BRIDGE WRAITH HAS FALLEN!', A),
    ('vo_br_hero_defeat', '[dry] She said thank you. Hell, first time.', 'She said thank you. Hell, first time.', P),
    ('vo_waystone_lit_701', '[booming] WAYSTONE LIT! [pause] FLOOR SEVEN HUNDRED ONE!', 'WAYSTONE LIT: FLOOR 701', A),
    ('vo_waystone_hero_701', '[dry] Good. [pause] Fewer bridges next time.', 'Good. Fewer bridges next time.', P),
    ('vo_shaft_wake_vii', '[booming] CIRCLE SEVEN! [pause] [excited] THE GLOOMWOOD!', 'CIRCLE SEVEN! THE GLOOMWOOD!', A),
    ('vo_hero_hint_afterimage', '[calm] That is me, three seconds ago. It is slow. I can shoot it.', 'That is me, three seconds ago. It is slow. I can shoot it.', P),
    ('vo_hero_hint_runeHound', '[calm] A stone hound. It wakes when I come near. Sidestep the lunge.', 'A stone hound. It wakes when I come near. Sidestep the lunge.', P),
    ('vo_hero_hint_sleepwalker', '[whispers] Asleep. A shot wakes it. So I do not shoot it.', 'Asleep. A shot wakes it. So I do not shoot it.', P),
]
TEXT = {b[0]: b[2] for b in BOSS_LINES}

EFFECTS = [
    fx_row('br_chime', 'A single soft glassy bell chime with a long cold echo tail, a lantern of runes being lifted. One-shot, dry, no music.', 2.0, 0.8),
    fx_row('bridgeWraith_hurt', 'A hooded wraith hit: a small breathy gasp and a thin glassy crack, hollow echo. One-shot, dry, no music. Very short, ends immediately.', 0.6, 0.7, 0.6, 4, 0.08),
    fx_row('bridgeWraith_die', 'A wraith fading out: a long relieved breath, glass bells ringing softly and dying away into silence. One-shot, no music.', 2.5, 0.9, 0.6, 2, 0.1),
    fx_row('lamplighter_die', 'A pole clattering down and a long fading whistle of someone falling off a very high bridge, then silence. One-shot, no music.', 2.0, 0.9, 0.6, 2, 0.1),
]


def all_audio_rows():
    rows = []
    for i, f in enumerate(F):
        n = f[0]
        rows.append(voice_row('vo_f%d_title' % n, '[shouting] Floor %s! [pause] %s!' % (say_floor(n), f[1]), *A[:1], A[1]))
    for sid, say in zip(HERO_IDS, HERO):
        rows.append(voice_row(sid, say[1], 'player', PL))
    for sid, say, text, (voice, vid, speed) in BOSS_LINES:
        rows.append(voice_row(sid, say, voice, vid, speed))
    rows += EFFECTS
    return rows


def patch_audio():
    path = os.path.join(CFG, 'audio.json')
    d = json.load(open(path, encoding='utf-8'))
    # voices
    names = {v['name'] for v in d['voices']}
    voices = [
        {'name': 'bridgeWraith', 'description': "The Bridge Wraith, Warden of Hell's Rune Bridges: a hooded wraith keeper of bridges, hushed, courteous and anxious; a dry hollow whisper with a long echo tail as if from the far end of a hall; politely worried, apologises before every terrible thing, a small breath-catch before she acts. Never raises her voice. Clean studio recording with a faint cold reverb.",
         'sampleText': 'Oh. Oh, hello. Sorry. Sorry. Someone needs to cross. I have not lit a rune in such a very long time. Mind the gap. There is no gap. Sorry.', 'voiceId': WRAITH_VOICE, 'model': 'eleven_v3', 'stability': 0.5, 'speed': 0.9},
        {'name': 'lamplighter', 'description': "The Lamplighter of Floor 751: a hunched old wraith carrier who lights runes on a long pole. Thin, creaky, muttering and fussy, speaks in short flat phrases, tired and dutiful.",
         'sampleText': 'One for the left. One for the right. Light them, put them out. That is the work. Mind the pole. Mind the flame.', 'voiceId': LAMPLIGHTER_VOICE, 'model': 'eleven_v3', 'stability': 0.5, 'speed': 0.9},
    ]
    for v in voices:
        if v['name'] not in names:
            i = next(k for k, x in enumerate(d['voices']) if x['name'] == 'spireFiend')
            d['voices'].insert(i + 1, v)
    have = {s['id'] for s in d['sounds']}
    new = [r for r in all_audio_rows() if r['id'] not in have]
    if new:
        i = next(k for k, s in enumerate(d['sounds']) if s['id'] == 'vo_f792_clear')
        d['sounds'][i + 1:i + 1] = new
    write_json(path, d)
    return len(new)


# ---------------------------------------------------------------- the bosses

GLOW_LAMP = 'brWarnLamp'


def shoot(projectile, muzzle, delay=0.2, aim='Down', speed=0.0, anim=None, **extra):
    a = {'type': 'shoot', 'projectile': projectile, 'speed': speed, 'aim': aim, 'muzzle': muzzle, 'interval': 99, 'initialDelay': delay}
    if anim:
        a['animTrigger'] = anim
    a.update(extra)
    return a


def spawners(mix, slots, interval=3.2, first=0.5):
    return [{'type': 'spawner', 'entity': e, 'interval': interval, 'initialDelay': round(first + i * 0.55, 2), 'maxAlive': cap,
             'offset': list(slots[i % len(slots)])} for i, (e, cap) in enumerate(mix)]


def go(to, seconds):
    return {'to': to, 'when': {'type': 'after', 'seconds': seconds}}


def below(to, fraction):
    return {'to': to, 'when': {'type': 'healthBelow', 'fraction': fraction}}


def state(name, abilities=None, then=None, on_enter=None, guards=None):
    s = {'name': name}
    if on_enter:
        s['onEnter'] = on_enter
    if abilities:
        s['abilities'] = abilities
    s['transitions'] = (guards or []) + (then or [])
    return s


def emit(sig):
    return {'type': 'emit', 'signal': sig}


def kills(types):
    return [{'type': 'kill', 'target': {'entity': t}} for t in types]


def say(sid, color, duration=3.0):
    return {'type': 'showText', 'text': TEXT[sid], 'style': 'Subtitle', 'duration': duration, 'color': color, 'sound': sid}


SHIELD = {'type': 'frontShield', 'arc': 200, 'blockSound': 'chain_cut', 'color': '#BFEFFF'}
HOVER = {'type': 'hover', 'speed': 0.0, 'aroundSpawn': True, 'bobHeight': 0.14, 'bobSpeed': 1.3}

# Where minions come in relative to the boss (the boss floats 1.4 above the Spire Fiend's place, so these sit 1.4 lower).
SLOTS = [(1.7, -2.3), (3.6, -3.0), (5.2, -1.8), (2.4, -0.5), (4.4, 0.0), (6.2, -3.2), (3.0, -4.0), (5.8, -0.4)]


def wraith_entity():
    slots = SLOTS
    wave = spawners([('imp', 3), ('ricochetImp', 3), ('blinkImp', 3)], slots)
    p2 = spawners([('lanternfly', 6), ('bulwarkImp', 6), ('leadImp', 6), ('afterimage', 6), ('watcher', 6)], slots)
    p3 = spawners([('afterimage', 5), ('blinkImp', 5), ('leadImp', 5), ('ricochetImp', 5)], slots, 3.0)
    last = spawners([('runeHound', 8), ('blinkImp', 8), ('leadImp', 8), ('afterimage', 8), ('ricochetImp', 8), ('bulwarkImp', 8)], slots, 2.8, 0.4)
    muz = [0.3, 0.3]

    def fan(count, angle):
        return [shoot(GLOW_LAMP, muz, 0.0), shoot('brLampBolt', muz, 1.2, 'Player', 3.4, pattern={'type': 'spread', 'count': count, 'angle': angle}, animTrigger='Lantern')]

    def lead(burst):
        return [shoot(GLOW_LAMP, muz, 0.0), shoot('brMarkBolt', muz, 1.2, 'Predictive', 5.6, burst=burst, burstInterval=0.3)]

    wall = lambda count: [shoot('brWarnWall', [4.2, -2.3], 0.0, 'Down', 0.0, None, pattern={'type': 'line', 'count': count, 'spacing': 0.8})]
    spot = [{'type': 'spawn', 'entity': 'brWarnRune', 'anchor': 'Player', 'at': [0.0, -0.3], 'facePlayer': False}]

    g1 = [below('snuffed', 0.5)]
    g1w = [below('snuffed', 0.5)]
    g2 = [below('unwriting', 0.334)]
    g3 = [below('brLast', 0.15)]
    st = []
    st.append(state('emerging', None, [go('waiting', 2.2)], [{'type': 'playSound', 'sound': 'br_chime'}]))
    st.append(state('waiting', None, [{'to': 'p1Fan', 'when': {'type': 'signal', 'name': 'brFight'}}]))
    # Phase 1, The Lantern: she lights something (open, can be shot), then the lantern shutters (shielded).
    p1 = [('p1Fan', fan(3, 40), 2.8, 'p1Rest', []), ('p1Rest', [SHIELD], 1.4, 'p1Rune', []), ('p1Rune', [], 2.6, 'p1Rest2', spot), ('p1Rest2', [SHIELD], 1.2, 'p1Fan', [])]
    for name, ab, secs, nxt, enter in p1:
        st.append(state(name, ab, [go(nxt, secs)], enter or None, [below('brWave', 0.75)] + g1))
    # The wave (75%): her carriers come.
    st.append(state('brWave', None, [go('waveFan', 0.5)], [emit('brWave'), {'type': 'playSound', 'sound': 'br_chime'}], g1w))
    wv = [('waveFan', fan(3, 44) + wave, 3.6, 'waveRest'), ('waveRest', [SHIELD] + wave, 1.2, 'waveLead'), ('waveLead', lead(2) + wave, 3.4, 'waveRest2'), ('waveRest2', [SHIELD] + wave, 1.2, 'waveFan')]
    for name, ab, secs, nxt in wv:
        st.append(state(name, ab, [go(nxt, secs)], None, g1w))
    # Phase 2, Snuffed (half health): she shuts the lantern; the Span goes dark except where he stands; her afterimages trace him.
    st.append(state('snuffed', None, [go('snuffedLights', 6.0)],
                    [emit('brSnuff')] + kills(['imp', 'ricochetImp', 'blinkImp']) + [say('vo_br_snuff', WRAITH, 3.4), {'type': 'cameraShake', 'intensity': 0.2, 'duration': 1.2}]))
    st.append(state('snuffedLights', None, [go('p2Fan', 0.5)], [say('vo_br_flies', WRAITH, 3.0), emit('brPhase2')]))
    p2s = [('p2Fan', fan(5, 60) + p2, 3.0, 'p2Rest'), ('p2Rest', [SHIELD] + p2, 1.2, 'p2Rune'), ('p2Rune', p2, 3.0, 'p2Lead'), ('p2Lead', lead(3) + p2, 3.6, 'p2Rest2'), ('p2Rest2', [SHIELD] + p2, 1.2, 'p2Fan')]
    for name, ab, secs, nxt in p2s:
        enter = spot if name == 'p2Rune' else None
        st.append(state(name, ab, [go(nxt, secs)], enter, g2))
    # Phase 3, Unwritten (a third): the bridges unwrite behind him on the beat; walls of runes fall.
    st.append(state('unwriting', None, [go('p3Wall', 5.0)],
                    [emit('brEnrage'), say('vo_br_enrage', WRAITH, 4.0), {'type': 'cameraShake', 'intensity': 0.3, 'duration': 1.6}] +
                    kills(['lanternfly', 'bulwarkImp', 'leadImp', 'afterimage', 'watcher'])))
    p3s = [('p3Wall', wall(9) + p3, 3.4, 'p3Fan'), ('p3Fan', fan(5, 70) + p3, 3.0, 'p3Rune'), ('p3Rune', p3, 2.8, 'p3Lead'), ('p3Lead', lead(3) + p3, 3.4, 'p3Wall')]
    for name, ab, secs, nxt in p3s:
        enter = spot if name == 'p3Rune' else ([emit('brUnwrite')] if name == 'p3Wall' else None)
        st.append(state(name, ab, [go(nxt, secs)], enter, g3))
    # Last stand (15%): everything at once.
    st.append(state('brLast', None, [go('lastFan', 0.6)], [emit('brLast'), {'type': 'playSound', 'sound': 'br_chime'}]))
    ls = [('lastFan', fan(5, 70) + last, 3.0, 'lastLead'), ('lastLead', lead(3) + last, 3.0, 'lastWall'), ('lastWall', wall(9) + last, 3.2, 'lastFan')]
    for name, ab, secs, nxt in ls:
        st.append(state(name, ab, [go(nxt, secs)]))
    return {
        'schemaVersion': 1, 'displayName': 'Bridge Wraith', 'hint': 'The lantern opens when she lights something. Shoot it then.',
        'note': 'She keeps every bridge. Apologises for the ones that kill me.', 'team': 'Enemy', 'health': 150, 'moveSpeed': 0, 'activationRange': 0,
        'tags': ['boss', 'bridgeWraith'], 'body': {'shape': 'Box', 'size': [1.2, 2.2], 'solid': True, 'gravity': 0},
        'visual': {'scale': 1, 'color': '#9FB4D8', 'fxColor': '#8FE3FF', 'placeholderSize': 2.2, 'deathFade': 1.4},
        'healthBar': {'show': False}, 'loot': {'chance': 1, 'count': 5, 'value': 2},
        'abilities': [{'type': 'faceTarget'}, {'type': 'contactDamage', 'damage': 1}, copy.deepcopy(HOVER),
                      {'type': 'react', 'when': {'type': 'healthBelow', 'fraction': 0.85}, 'do': [say('vo_br_hurt1', WRAITH)]},
                      {'type': 'react', 'when': {'type': 'healthBelow', 'fraction': 0.15}, 'do': [say('vo_br_hurt2', WRAITH)]}],
        'states': st,
        'onDeath': [say('vo_br_defeat', WRAITH, 4.5), {'type': 'playSound', 'sound': 'bridgeWraith_die'}, emit('brFallen')],
        'sounds': {'hurtSound': 'bridgeWraith_hurt', 'deathSound': 'bridgeWraith_die', 'attackSound': 'br_chime'},
        'hintVoice': 'vo_hero_hint_bridgeWraith',
    }


def lamplighter_entity():
    slots = [(2.0, -1.4), (4.0, -2.0), (5.6, -0.6), (3.0, 0.8), (4.8, 1.4)]
    tier2 = spawners([('watcher', 4), ('imp', 4), ('ricochetImp', 4), ('leadImp', 4)], slots, 3.4)
    blind = spawners([('blinkImp', 6), ('leadImp', 6), ('watcher', 6), ('imp', 6), ('afterimage', 6)], slots, 3.0)
    last = spawners([('blinkImp', 8), ('leadImp', 8), ('watcher', 8), ('imp', 8), ('afterimage', 8), ('ricochetImp', 8)], slots, 2.8, 0.4)
    muz = [0.4, 0.8]
    spot = [{'type': 'spawn', 'entity': 'llWarnRune', 'anchor': 'Player', 'at': [0.0, -0.3], 'facePlayer': False}]
    poles = lambda count, angle: [shoot(GLOW_LAMP, muz, 0.0), shoot('llFlameBolt', muz, 1.2, 'Player', 3.4, pattern={'type': 'spread', 'count': count, 'angle': angle})]
    lead = [shoot(GLOW_LAMP, muz, 0.0), shoot('llMarkBolt', muz, 1.2, 'Predictive', 5.4, burst=3, burstInterval=0.3)]
    st = [state('emerging', None, [go('waiting', 2.0)]),
          state('waiting', None, [{'to': 'p1Spot', 'when': {'type': 'signal', 'name': 'llFight'}}])]
    g1 = [below('llSecond', 0.66)]
    for name, ab, secs, nxt, enter in [('p1Spot', [], 2.6, 'p1Pole', spot), ('p1Pole', poles(3, 36), 2.8, 'p1Spot', None)]:
        st.append(state(name, ab, [go(nxt, secs)], enter, g1))
    st.append(state('llSecond', None, [go('p2Spot', 1.0)], [emit('llTier2'), say('vo_ll_tier2', LAMP, 3.2), {'type': 'cameraShake', 'intensity': 0.15, 'duration': 0.8}]))
    g2 = [below('llBlind', 0.33)]
    for name, ab, secs, nxt, enter in [('p2Spot', tier2, 2.8, 'p2Pole', spot), ('p2Pole', poles(5, 56) + tier2, 3.2, 'p2Lead', None), ('p2Lead', lead + tier2, 3.4, 'p2Spot', None)]:
        st.append(state(name, ab, [go(nxt, secs)], enter, g2))
    st.append(state('llBlind', None, [go('p3Spot', 3.0)],
                    [emit('llBlindSignal'), say('vo_ll_blind', LAMP, 3.2), {'type': 'cameraShake', 'intensity': 0.25, 'duration': 1.0}] +
                    kills(['watcher', 'imp', 'ricochetImp', 'leadImp'])))
    g3 = [below('llLast', 0.12)]
    for name, ab, secs, nxt, enter in [('p3Spot', blind, 2.4, 'p3Pole', spot), ('p3Pole', poles(5, 70) + blind, 3.0, 'p3Lead', None), ('p3Lead', lead + blind, 3.0, 'p3Spot', None)]:
        st.append(state(name, ab, [go(nxt, secs)], enter, g3))
    st.append(state('llLast', None, [go('lastPole', 0.6)], [emit('llLast')]))
    for name, ab, secs, nxt in [('lastPole', poles(5, 70) + last, 3.0, 'lastLead'), ('lastLead', lead + last, 3.0, 'lastPole')]:
        st.append(state(name, ab, [go(nxt, secs)]))
    return {
        'schemaVersion': 1, 'displayName': 'Lamplighter', 'hint': 'The flame on his pole is the whole job. Shoot it.',
        'note': 'Lights the bridge under me, puts it out behind. Fussy about the pole.', 'team': 'Enemy', 'health': 90, 'moveSpeed': 0, 'activationRange': 0,
        'tags': ['boss', 'lamplighter'], 'body': {'shape': 'Box', 'size': [1.1, 2.0], 'solid': True, 'gravity': 0},
        'visual': {'scale': 1, 'color': '#8E9BB8', 'fxColor': '#BFEFFF', 'placeholderSize': 2.0, 'deathFade': 1.2},
        'healthBar': {'show': False}, 'loot': {'chance': 1, 'count': 5, 'value': 2},
        'abilities': [{'type': 'faceTarget'}, {'type': 'contactDamage', 'damage': 1}, copy.deepcopy(HOVER),
                      {'type': 'react', 'when': {'type': 'healthBelow', 'fraction': 0.85}, 'do': [say('vo_ll_hurt1', LAMP)]}],
        'states': st,
        'onDeath': [say('vo_ll_defeat', LAMP, 4.0), {'type': 'playSound', 'sound': 'lamplighter_die'}, emit('llFallen')],
        'sounds': {'deathSound': 'lamplighter_die'}, 'hintVoice': 'vo_hero_hint_lamplighter',
    }


def glow(name, extends='mkWarnHand', color=LAMP, hit=None):
    d = {'schemaVersion': 1, 'extends': extends, 'visual': {'color': color}}
    if hit:
        d['onDeath'] = [{'type': 'spawn', 'entity': hit, 'anchor': 'Self', 'facePlayer': False}, {'type': 'playSound', 'sound': 'rune_hum'}]
    return d


def column(scale_h=3.4):
    return {'schemaVersion': 1, 'team': 'Enemy', 'health': 0, 'shootable': False,
            'body': {'shape': 'Box', 'size': [1.1, scale_h], 'offset': [0.0, scale_h / 2], 'solid': False},
            'healthBar': {'show': False},
            'visual': {'scale': 0.3, 'color': '#BFEFFF', 'fxColor': '#8FE3FF', 'placeholderColor': '#8FE3FF', 'placeholderShape': 'Box',
                       'placeholderLabel': False, 'sortingOrder': 4, 'hurtFlash': False, 'deathPop': False},
            'abilities': [{'type': 'contactDamage', 'damage': 1}, {'type': 'lifetime', 'seconds': 0.7}]}


def bolt(color):
    return {'schemaVersion': 1, 'extends': 'runeBolt', 'visual': {'color': color, 'scale': 0.13}}


BOSS_ENTITIES = {
    'bridgeWraith': wraith_entity(),
    'lamplighter': lamplighter_entity(),
    'brWarnLamp': glow('brWarnLamp'),
    'brWarnRune': glow('brWarnRune', 'fmWarnWhip', LAMP, 'brRuneColumn'),
    'brRuneColumn': column(),
    'brWarnWall': glow('brWarnWall', 'fmWarnWhip', '#8FE3FF', 'brWallHit'),
    'brWallHit': column(4.2),
    'brLampBolt': bolt('#BFEFFF'),
    'brMarkBolt': bolt('#E8F6FF'),
    'llWarnRune': glow('llWarnRune', 'fmWarnWhip', '#FFE9A8', 'llRuneColumn'),
    'llRuneColumn': column(2.6),
    'llFlameBolt': bolt('#FFD27A'),
    'llMarkBolt': bolt('#FFE9A8'),
}
for k in ('brWarnRune', 'brWarnWall', 'llWarnRune'):
    BOSS_ENTITIES[k]['visual']['scale'] = 0.14


# ---------------------------------------------------------------- the two boss levels

def load(path):
    return json.load(open(os.path.join(CFG, path), encoding='utf-8'))


def replace_all(obj, table):
    s = json.dumps(obj, ensure_ascii=False)
    for a, b in table:
        s = s.replace(a, b)
    return json.loads(s)


def wraith_level(position):
    lv = load('levels/Floor801.json')
    table = [('Floor801', 'Floor701'), ("FLOOR 801: THE SPIRE'S HEART", 'FLOOR 701: THE GREAT SPAN'), ("Floor 801: The Spire's Heart", 'Floor 701: The Great Span'),
             ('vo_f801_title', 'vo_f701_title'), ('"start": 801', '"start": 701'), ('"end": 801', '"end": 701'),
             ('crackedRoad', 'runeBridges'), ('Defeat the Spire Fiend', 'Defeat the Bridge Wraith'),
             ('vo_waystone_lit_801', 'vo_waystone_lit_701'), ('vo_waystone_hero_801', 'vo_waystone_hero_701'),
             ('WAYSTONE LIT: FLOOR 801', 'WAYSTONE LIT: FLOOR 701'), ('"spireFiend"', '"bridgeWraith"'),
             ('THE SPIRE FIEND HAS FALLEN!', 'THE BRIDGE WRAITH HAS FALLEN!'), ('sfIntro', 'brIntro'), ('sfFight', 'brFight'), ('sfDefeat', 'brDefeat'), ('sfWave', 'brWave'), ('sfLast', 'brLast'), ('sfField', 'brUnwrite'),
             ('vo_sf_', 'vo_br_'), ('vo_hero_hint_spireFiend', 'vo_hero_hint_bridgeWraith'), ('sf_hum', 'rune_hum'), ('sf_crumble', 'br_chime')]
    lv = replace_all(lv, table)
    lv['levelName'] = 'Floor701'
    lv['backgroundLayers'] = []
    lv['displayName'] = 'Floor 701: The Great Span'
    lv['levelNumber'] = position
    lv['nextLevelNumber'] = 0
    lv['duration'] = 700.0
    s = lv['script']
    phases = {p['name']: p for p in s['phases']}

    # intro
    intro = phases['intro']
    intro['onEnter'] = [a for a in intro['onEnter'] if not (a.get('type') == 'spawn' and a.get('entity') == 'lavaSeaHall')]
    for a in intro['onEnter']:
        if a.get('type') == 'showText':
            a['color'] = GOLD
    seq = intro['rules'][0]['do'][0]['steps']
    for st in seq:
        if st.get('type') == 'spawn' and st.get('id') == 'boss':
            st['at'] = [3.9, 0.2]
        if st.get('type') == 'showText':
            st['color'] = WRAITH
        if st.get('type') == 'flash':
            st['color'] = '#8FE3FF88'
        if st.get('type') == 'cameraTo':
            st['at'] = [2.4, 0.5]
    for st in seq:
        if st.get('text') == 'FLOOR 701! THE WARDEN OF THE ROAD!':
            st['text'] = 'FLOOR 701! THE WARDEN OF THE RUNE BRIDGES!'
        if st.get('text') == 'THE SPIRE FIEND':
            st['text'] = 'THE BRIDGE WRAITH'
            st['label'] = None
        if st.get('type') == 'bossBar':
            st['label'] = 'The Bridge Wraith'
        if st.get('style') == 'Subtitle' and st.get('sound') == 'vo_br_intro':
            st['text'] = TEXT['vo_br_intro']
        if st.get('sound') == 'vo_br_hero_intro':
            st['text'] = TEXT['vo_br_hero_intro']
    for st in seq:
        st.pop('label', None) if st.get('type') != 'bossBar' else None
    # the Great Span: a ring of bridges round her, lit on the beat (spans above the ground to hop onto)
    ring = []
    for i, (x, y) in enumerate([(-2.6, -0.7), (0.4, -0.2), (3.4, -0.7), (6.2, -0.2)]):
        ring.append({'type': 'spawn', 'entity': 'runeSpanUnison' if i % 2 == 0 else 'runeSpanLate', 'anchor': 'Camera', 'at': [x, y], 'id': 'span%d' % i})
    intro['onEnter'] = ring + intro['onEnter']

    # fight
    fight = phases['fight']
    for a in fight['onEnter']:
        if a.get('type') == 'showText':
            a['text'] = TEXT['vo_br_phase1']
            a['color'] = WRAITH
    for r in fight['rules']:
        if r['name'] == 'the first needle field':
            r['name'] = 'the first unwriting'
            for st in r['do'][0]['steps']:
                if st.get('type') == 'showText':
                    st['text'] = TEXT['vo_br_hero_field']
        if r['name'] == 'the hero sizes him up':
            for a in r['do']:
                a['text'] = TEXT['vo_hero_hint_bridgeWraith']
                a['duration'] = 4.4
    fight['rules'] += [
        {'name': 'snuffed: the Span goes dark', 'when': {'type': 'signal', 'name': 'brSnuff'}, 'maxTimes': 1,
         'do': [{'type': 'despawn', 'target': {'entity': 'runeSpanUnison'}}, {'type': 'despawn', 'target': {'entity': 'runeSpanLate'}},
                {'type': 'spawn', 'entity': 'runeSpanHold', 'anchor': 'Camera', 'at': [-2.2, -0.6]},
                {'type': 'spawn', 'entity': 'runeSpanHold', 'anchor': 'Camera', 'at': [1.2, -0.2]},
                {'type': 'spawn', 'entity': 'runeSpanHold', 'anchor': 'Camera', 'at': [4.4, -0.6]},
                {'type': 'cameraShake', 'intensity': 0.15, 'duration': 0.7}]},
        {'name': 'unwritten: the bridges go on the beat', 'when': {'type': 'signal', 'name': 'brEnrage'}, 'maxTimes': 1,
         'do': [{'type': 'despawn', 'target': {'entity': 'runeSpanHold'}},
                {'type': 'spawn', 'entity': 'runeSpanQuick', 'anchor': 'Camera', 'at': [-2.6, -0.7]},
                {'type': 'spawn', 'entity': 'runeSpanQuick', 'anchor': 'Camera', 'at': [0.6, -0.2]},
                {'type': 'spawn', 'entity': 'runeSpanQuick', 'anchor': 'Camera', 'at': [3.8, -0.7]}]},
    ]

    # defeat
    defeat = phases['defeat']
    for a in defeat['onEnter']:
        if a.get('type') == 'despawn':
            a['target'] = {'entity': 'lanternfly'}
        if a.get('type') == 'playSound':
            a['sound'] = 'bridgeWraith_die'
    for a in defeat['onEnter']:
        if a.get('type') == 'runSequence':
            for st in a['steps']:
                if st.get('type') == 'showText' and st.get('color'):
                    st['color'] = '#7CFF8A'
            a['steps'].insert(1, {'type': 'popText', 'text': 'THANK YOU', 'color': '#BFEFFF', 'anchor': 'Camera', 'at': [3.9, 1.2], 'size': 1.4})
            for st in a['steps']:
                if st.get('text') == 'He never got off that rock. I almost respect it.':
                    st['text'] = TEXT['vo_br_hero_defeat']
    wl = defeat['rules'][0]['do'][0]['steps']
    for st in wl:
        if st.get('sound') == 'vo_waystone_hero_701':
            st['text'] = TEXT['vo_waystone_hero_701']
        if st.get('type') == 'spawn' and st.get('entity') == 'waystoneGlow':
            st['at'] = [1.1, -0.3]
    # the stump is a span: the hero walks to x = 2.0 of the screen as in the Spire Fiend's hall
    s['counters'] = []
    return lv


def lamplighter_level(position):
    """Floor 751: a floor boss held on one Arena section of a bridge (a Travel camera, like the Pile Keeper's mound)."""
    spans = [rf.sp('ledge', -3.6, rf.BASE_Y - 0.25, id='start', scale=[2.0, 1]),
             rf.sp('runePillar', 3.2, rf.BASE_Y - 0.25, id='pier', scale=[3.4, 1]),
             rf.sp('runeSpanQuick', -0.4, rf.BASE_Y + 1.1, scale=[1.0, 1]),
             rf.sp('runeSpanLate', 2.6, rf.BASE_Y + 1.1, scale=[1.0, 1])]
    intro_steps = [
        {'type': 'playSound', 'sound': 'br_chime'},
        {'type': 'cameraShake', 'intensity': 0.12, 'duration': 1.6},
        {'type': 'spawn', 'entity': 'lamplighter', 'id': 'boss', 'anchor': 'Camera', 'at': [3.6, 0.3]},
        {'type': 'setState', 'target': {'self': False, 'id': 'boss'}, 'state': 'emerging'},
        {'type': 'emit', 'signal': 'llIntro'},
        {'type': 'wait', 'seconds': 2.0},
        {'type': 'showText', 'text': TEXT['vo_ll_name'], 'duration': 2.4, 'sound': 'vo_ll_name', 'color': LAMP},
        {'type': 'wait', 'seconds': 2.8},
        {'type': 'showText', 'text': TEXT['vo_ll_intro'], 'style': 'Subtitle', 'duration': 5.0, 'sound': 'vo_ll_intro', 'color': LAMP},
        {'type': 'wait', 'seconds': 5.4},
        {'type': 'showText', 'text': TEXT['vo_ll_hero_intro'], 'style': 'Subtitle', 'duration': 3.0, 'sound': 'vo_ll_hero_intro'},
        {'type': 'wait', 'seconds': 3.2},
        {'type': 'bossBar', 'target': {'id': 'boss'}, 'label': 'The Lamplighter'},
        {'type': 'goToPhase', 'phase': 'fight'},
    ]
    phases = [
        {'name': 'ledge', 'onEnter': [{'type': 'runSequence', 'steps': [
            {'type': 'showText', 'text': 'FLOOR 751: THE LAMPLIGHTER', 'duration': 3.0, 'style': 'Banner', 'sound': 'vo_f751_title', 'color': GOLD}]}],
         'rules': [{'name': 'the lamplighter comes', 'when': {'type': 'after', 'seconds': 4.0}, 'maxTimes': 1, 'do': [{'type': 'goToPhase', 'phase': 'intro'}]}]},
        {'name': 'intro', 'onEnter': [{'type': 'lockInput'}, {'type': 'lockRewind'},
                                      {'type': 'applyStatus', 'target': {'player': True}, 'effect': 'sceneUntouchable'}],
         'rules': [{'name': 'he lights the bridge', 'when': {'type': 'after', 'seconds': 0.4}, 'maxTimes': 1, 'do': [{'type': 'runSequence', 'steps': intro_steps}]}]},
        {'name': 'fight',
         'onEnter': [{'type': 'unlockInput'}, {'type': 'unlockRewind'}, {'type': 'removeStatus', 'target': {'player': True}, 'effect': 'sceneUntouchable'},
                     {'type': 'emit', 'signal': 'llFight'},
                     {'type': 'playMusic', 'clip': 'Audio/Music/wastes_push.ogg', 'volume': 0.85}],
         'rules': [
             {'name': 'he falls', 'when': {'type': 'allDead', 'target': {'id': 'boss'}}, 'maxTimes': 1, 'do': [{'type': 'goToPhase', 'phase': 'defeat'}]},
             {'name': 'the hero sizes him up', 'when': {'type': 'after', 'seconds': 8.0}, 'maxTimes': 1,
              'do': [{'type': 'showText', 'text': TEXT['vo_hero_hint_lamplighter'], 'duration': 4.0, 'style': 'Subtitle', 'sound': 'vo_hero_hint_lamplighter'}]},
             {'name': 'boss rework: second bridge', 'when': {'type': 'signal', 'name': 'llTier2'}, 'maxTimes': 1,
              'do': [{'type': 'cameraShake', 'intensity': 0.15, 'duration': 0.7}]},
             {'name': 'boss rework: blind', 'when': {'type': 'signal', 'name': 'llBlindSignal'}, 'maxTimes': 1,
              'do': [{'type': 'cameraShake', 'intensity': 0.3, 'duration': 0.7}]}]},
        {'name': 'defeat',
         'onEnter': [{'type': 'lockInput'}, {'type': 'lockRewind'}, {'type': 'applyStatus', 'target': {'player': True}, 'effect': 'sceneUntouchable'},
                     {'type': 'slowMotion', 'scale': 0.3, 'duration': 2.0}, {'type': 'cameraShake', 'intensity': 0.3, 'duration': 1.2},
                     {'type': 'emit', 'signal': 'llDefeat'},
                     {'type': 'runSequence', 'steps': [
                         {'type': 'wait', 'seconds': 4.5},
                         {'type': 'showText', 'text': TEXT['vo_ll_fallen'], 'duration': 3.4, 'style': 'Banner', 'sound': 'vo_ll_fallen', 'color': '#7CFF8A'},
                         {'type': 'wait', 'seconds': 3.6},
                         {'type': 'showText', 'text': TEXT['vo_ll_hero_defeat'], 'style': 'Subtitle', 'duration': 3.4, 'sound': 'vo_ll_hero_defeat'},
                         {'type': 'wait', 'seconds': 3.6},
                         {'type': 'hideBossBar'},
                         {'type': 'unlockInput'}, {'type': 'unlockRewind'}, {'type': 'removeStatus', 'target': {'player': True}, 'effect': 'sceneUntouchable'},
                         {'type': 'setFloor', 'floor': 750, 'skipMilestones': True},
                         {'type': 'showText', 'text': 'FLOOR 750: HALFWAY ACROSS', 'duration': 3.2, 'style': 'Banner', 'sound': 'vo_f750_title', 'color': GOLD},
                         {'type': 'wait', 'seconds': 3.0},
                         {'type': 'offerUpgrades', 'title': 'Pick a grudge'},
                         {'type': 'setCounter', 'name': 'cleared', 'value': 1}]}]},
    ]
    return {
        'schemaVersion': 1, 'levelName': 'Floor751', 'displayName': 'Floor 751: The Lamplighter', 'levelNumber': position,
        'thumbnail': 'Videos/Video2_FirstFrame.png', 'backgrounds': [], 'backgroundLayers': [],
        'music': 'Audio/Music/wastes_loop.ogg', 'musicVolume': 0.85, 'platforms': [], 'placedSprites': [], 'enemies': [],
        'playerSpawnPosition': [-4.0, 0.0], 'deathConfigName': 'Death1', 'duration': 600.0, 'completionDelay': 2.0,
        'nextVideoNumber': 0, 'nextLevelNumber': position + 1, 'returnToMainMenuOnComplete': False,
        'script': {'seed': 751, 'length': 150,
                   'floors': {'start': 751, 'end': 750, 'unitsPerFloor': 0.0, 'circle': 'runeBridges', 'signs': False},
                   'world': {'camera': 'Travel', 'travelLead': 1.2, 'followSharpness': 4.0, 'holdForPushes': False,
                             'path': [{'name': 'bridge', 'direction': 'Arena', 'length': 0.0, 'turn': 2.0, 'spawns': spans}]},
                   'objective': {'complete': {'type': 'counter', 'name': 'cleared', 'atLeast': 1}, 'text': 'Defeat the Lamplighter'},
                   'rules': [], 'phases': phases, 'counters': []},
    }


def patch_game(first_position):
    path = os.path.join(CFG, 'game.json')
    text = open(path, encoding='utf-8').read()
    d = json.loads(text)
    names = ['Floor751'] + ['Floor%d' % n for n in PATH_FLOORS] + ['Floor701']
    if names[0] in d['levels']:
        assert d['levels'].index(names[0]) + 1 == first_position, 'positions moved: pass the 1-based position of Floor751 in game.json'
        return
    assert len(d['levels']) + 1 == first_position, 'first_position must be the 1-based position Floor751 will take (the end of the list)'
    d['levels'] = d['levels'] + names
    line = '  "levels": [' + ', '.join('"%s"' % n for n in d['levels']) + '],'
    text = re.sub(r'  "levels": \[[^\]]*\],', lambda m: line, text, count=1)
    if not CHECK:
        open(path, 'w', encoding='utf-8', newline='\n').write(text)
    changed.append('game.json')


def main(first_position):
    for name, data in ENTITIES.items():
        ent(name, data)
    for name, data in BOSS_ENTITIES.items():
        ent(name, data)
    levels = [('Floor751', lamplighter_level(first_position))]
    for i, f in enumerate(F):
        r = floor_row(i, f)
        ent('floorLanding%d' % f[0], landing_entity(r))
        levels.append(('Floor%d' % f[0], build_path_floor(i, f, first_position + 1 + i)))
    levels.append(('Floor701', wraith_level(first_position + 1 + len(F))))
    for name, lv in levels:
        write_json(os.path.join(LEV, name + '.json'), lv)
    n = patch_audio()
    patch_game(first_position)
    print(('would change' if CHECK else 'changed'), len(changed), 'files;', n, 'new audio rows;', len(levels), 'levels')
    if CHECK and changed:
        print('\n'.join(changed[:20]))
        sys.exit(1)


if __name__ == '__main__':
    pos = [int(a) for a in sys.argv[1:] if a.isdigit()]
    main(pos[0] if pos else 99)
