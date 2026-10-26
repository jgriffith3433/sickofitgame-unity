#!/usr/bin/env python3
"""
Circle VIII (the Rune Bridges): writes its entities, its floors' level files and its audio.json lines from the tables below.

  python3 Tools/Levels/rune_floors.py            (rewrites Assets/GameConfig/content/entities/rune*.json, floorLanding8xx/79x,
                                                  levels/Floor800..Floor792.json and the vo_f8xx entries of audio.json)

The floors are the long bullet-hell recipe (Docs/LevelDesign.md "A long bullet-hell floor"): ~300 s, an opening, five set
pieces (one per path section, each a phase with its own idea), a landing gated until the floor has had its minutes. Edit the
tables, run it, then `python3 Tools/Levels/floor_check.py Floor800 ...`. Everything it writes is plain data; the file is the
source of the numbers, the JSON is what the game reads.
"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from artkeep import guard  # noqa: E402  (generated art is kept: Tools/Levels/artkeep.py)

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'GameConfig')
ENT = os.path.join(ROOT, 'content', 'entities')
LEV = os.path.join(ROOT, 'levels')

PLAYER_VOICE = 'DzkD7I4tf5i3KSaB4cz5'
ANNOUNCER_VOICE = '1xynpTfy7I462pPrU2kL'

BASE_Y = -1.2          # the bridge's height (top of a span is a little above)
PITCH = 3.8            # span centre to span centre (a span is 2.4 wide: a 1.4 gap, inside the bot's and a player's jump)
WIDTH = 1.2            # scale of a 2.0 ledge body -> 2.4 wide
GOLD = '#8FE3FF'       # the Circle's banner colour: rune cyan


def dump(path, data):
    data = guard(path, data)
    if data is None:
        return
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write('\n')


# ---------------------------------------------------------------- entities

def span(extends='ledge', **rune):
    ab = {'type': 'runeSpan'}
    ab.update(rune)
    return {
        'schemaVersion': 1, 'extends': extends, 'tags': ['foothold', 'runeSpan'],
        'visual': {'color': '#FFFFFF', 'placeholderColor': '#9FD8FF'},
        'abilities': [ab],
    }


HUM = 'rune_hum'
ENTITIES = {
    # The bridge pieces: a rune lit on a beat. runeSpan = a wave along the bridge (each span lights a moment after the one before).
    'runeSpan': span(period=9.0, hum=1.0, lit=5.5, dim=1.0, phasePerUnit=0.044, humSound=HUM),
    # The first floor's: waits for him (stays lit while he stands on it) and lights slowly.
    'runeSpanHold': span(period=10.0, hum=1.0, lit=6.5, dim=1.0, phasePerUnit=0.03, holdWhileStoodOn=True, humSound=HUM),
    # All of a row on one beat: cross while it is lit.
    'runeSpanUnison': span(period=9.0, hum=1.0, lit=5.5, dim=1.0, phasePerUnit=0.0, humSound=HUM),
    # Out of step with runeSpanUnison by a third of the beat.
    'runeSpanLate': span(period=9.0, hum=1.0, lit=5.5, dim=1.0, phase=0.33, phasePerUnit=0.0, humSound=HUM),
    # A quicker beat (6 s), lit for 3.5.
    'runeSpanQuick': span(period=6.0, hum=1.0, lit=3.5, dim=1.0, phasePerUnit=0.06, humSound=HUM),
    # Follows a Watcher's gaze instead of a beat.
    'runeSpanGaze': span(gazeTag='watcher', gazeRadius=2.6, humRadius=5.2, humSound=HUM),
    # A pier: the bridge's own stone, always there.
    'runePillar': {'schemaVersion': 1, 'extends': 'ledge', 'tags': ['foothold', 'runePillar'],
                   'visual': {'color': '#A9BFDA', 'placeholderColor': '#6F8CB0'}},
    # Cold, not hot: a rune shard. Dies on a lit span or a pier, passes through a dark span.
    'runeBolt': {'schemaVersion': 1, 'extends': 'fireball',
                 'visual': {'color': '#8FE3FF', 'scale': 0.12},
                 'abilities': [{'type': 'projectile', 'damage': 1, 'dieOnSolid': True},
                               {'type': 'lifetime', 'seconds': 6}]},
}

# The Watcher: a floating eye that guards a bridge. Lid shut (shots are blocked from every side), it counts; lid open it fires
# a slow aimed fan and can be shot. "Shoot as it opens."
WATCHER_STATES = [
    {'name': 'shut', 'animation': 'Idle',  # lid shut (art/watcher.json idle)
     'abilities': [{'type': 'frontShield', 'arc': 180, 'blockSound': 'chain_cut', 'color': '#BFEFFF'}],
     'transitions': [{'to': 'open', 'when': {'type': 'after', 'seconds': 2.6}}]},
    {'name': 'open', 'animation': 'Attack',  # lid open, firing
     'abilities': [{'type': 'shoot', 'projectile': 'runeBolt', 'pattern': {'type': 'spread', 'count': 3, 'angle': 30},
                    'speed': 2.9, 'aim': 'Player', 'interval': 1.4, 'initialDelay': 0.5, 'damage': 1}],
     'transitions': [{'to': 'shut', 'when': {'type': 'after', 'seconds': 2.2}}]},
]


def watcher(patrol=False):
    w = {
        'schemaVersion': 1, 'extends': 'chaser', 'health': 6, 'moveSpeed': 0.0, 'activationRange': 14.0,
        'tags': ['watcher'],
        'body': {'radius': 2.6},
        'visual': {'scale': 0.2, 'color': '#CFF4FF', 'fxColor': '#7FD8FF',
                   'animator': {'parameters': [], 'states': [{'stateName': 'Gaze', 'spriteSheet': 'Images/Fireball.png', 'fps': 8, 'loop': True}],
                                'transitions': [], 'defaultStateName': 'Gaze'}},
        'loot': {'chance': 0.5, 'count': 1, 'value': 1},
        'abilities': [{'type': 'contactDamage', 'damage': 1}] + ([{'type': 'patrol', 'speed': 1.0, 'distance': 5.0, 'pause': 1.2}] if patrol
                                                                 else [{'type': 'faceTarget'}, {'type': 'hover', 'speed': 0.0, 'aroundSpawn': True, 'bobHeight': 0.12, 'bobSpeed': 1.6}]),
        'states': WATCHER_STATES,
        'onDeath': [],
    }
    return w


ENTITIES['watcher'] = watcher()
ENTITIES['watcherPatrol'] = watcher(patrol=True)

# ---------------------------------------------------------------- pieces and floors

# A piece = one 30-unit path section and the phase that runs while it is on screen.
#   kind     the span entity the piece's bridge is made of
#   layout   the heights along the bridge (line | zig | wave)
#   pillar   a pier every Nth element
#   rain     (interval, ramp) of runeBolt rain from the top; aimed = interval of an aimed fan from the right edge
#   waves    [(entity, count, every s, startAfter s, (areaCenter y, area height))] from the right of the screen
LAYOUTS = {'line': [0.0], 'zig': [0.0, 0.5, 0.0, -0.4], 'wave': [0.0, 0.4, 0.8, 0.4, 0.0, -0.3]}


def piece(name, banner, kind, layout='line', pillar=5, rain=None, aimed=None, waves=(), mix=None, gaze=False):
    return dict(name=name, banner=banner, kind=kind, layout=layout, pillar=pillar, rain=rain, aimed=aimed, waves=list(waves), mix=mix, gaze=gaze)


FLOORS = [
    dict(n=800, name='The Bridge Foot', title='FLOOR 800: THE RUNE BRIDGES', title_say='[booming] Floor eight hundred! [pause] [excited] THE RUNE BRIDGES!',
         words='BRIDGE', opening=[],  # nothing else: the span alone
         h1=('The rune says BRIDGE. ...It is a bridge.', '[deadpan] The rune says BRIDGE. [pause] It is a bridge.'),
         h2=('It left. Then it came back. Fine.', '[deadpan] It left. [pause] Then it came back. [sighs] Fine.'),
         land=('Learnt that. Didn\'t love it.', '[deadpan] Learnt that. [pause] Didn\'t love it.'),
         next_say='[shouting] Floor seven ninety-nine! [pause] Two in a row!', next_title='FLOOR 799: TWO IN A ROW',
         pieces=[
             piece('lit', 'ONE LIT SPAN!', 'runeSpanHold', 'line', pillar=2),
             piece('hum', 'THE HUM!', 'runeSpanHold', 'line', pillar=3),
             piece('beat', 'THE BEAT!', 'runeSpanHold', 'zig', pillar=4, rain=(2.6, 0.008)),
             piece('across', 'ACROSS!', 'runeSpan', 'zig', pillar=5, rain=(1.9, 0.01), aimed=3.6),
             piece('longBridge', 'THE LONG BRIDGE!', 'runeSpanUnison', 'wave', pillar=6, rain=(1.4, 0.012), aimed=2.8, mix='runeSpanLate'),
         ]),
    dict(n=799, name='Two In A Row', title='FLOOR 799: TWO IN A ROW', title_say='[shouting] Floor seven ninety-nine! [pause] Two in a row!',
         words='STILL THE BRIDGE', opening=['imp'],
         h1=('Count to six. Jump on five.', '[deadpan] Count to six. [pause] Jump on five.'),
         h2=('THIS IS THE BRIDGE. Yes. I know.', '[deadpan] This is the bridge. [pause] [sighs] Yes. I know.'),
         land=('Two in a row. I\'ll take it.', '[deadpan] Two in a row. [pause] I\'ll take it.'),
         next_say='[shouting] Floor seven ninety-eight! [pause] Three in time!', next_title='FLOOR 798: THREE IN TIME',
         pieces=[
             piece('pair', 'TWO IN A ROW!', 'runeSpanUnison', 'line', pillar=3, rain=(2.6, 0.008)),
             piece('wave', 'THE WAVE!', 'runeSpan', 'zig', pillar=4, rain=(2.2, 0.01), waves=[('imp', 1, 12, 4, (1.0, 2.4))]),
             piece('breather', 'A BREATHER... (ALMOST)', 'runeSpanHold', 'line', pillar=3, rain=(2.4, 0.008), waves=[('imp', 1, 14, 3, (1.2, 2.2))]),
             piece('quick', 'QUICKER!', 'runeSpanQuick', 'zig', pillar=5, rain=(1.8, 0.01), aimed=3.6, waves=[('imp', 2, 11, 3, (1.0, 2.4))]),
             piece('crowd', 'THE ROW GETS BUSY!', 'runeSpan', 'wave', pillar=6, rain=(1.4, 0.012), aimed=2.8, waves=[('imp', 2, 8, 2, (1.0, 2.6))]),
         ]),
    dict(n=798, name='Three In Time', title='FLOOR 798: THREE IN TIME', title_say='[shouting] Floor seven ninety-eight! [pause] Three in time!',
         words='BRIDGE (AHEAD)', opening=['ricochetImp'],
         h1=('That one bounced. Off a bridge. Sure.', '[deadpan] That one bounced. [pause] Off a bridge. [pause] Sure.'),
         h2=('STILL THE BRIDGE. Good to know.', '[deadpan] Still the bridge. [pause] Good to know.'),
         land=('Fewer bridges would\'ve been fine.', '[deadpan] Fewer bridges would\'ve been fine.'),
         next_say='[shouting] Floor seven ninety-seven! [pause] The first Watcher!', next_title='FLOOR 797: THE FIRST WATCHER',
         pieces=[
             piece('three', 'THREE IN TIME!', 'runeSpan', 'line', pillar=4, rain=(2.6, 0.008)),
             piece('bounce', 'OFF THE BRIDGE!', 'runeSpan', 'zig', pillar=4, rain=(2.2, 0.01), waves=[('ricochetImp', 1, 12, 4, (1.0, 2.4))]),
             piece('unison', 'ALL TOGETHER!', 'runeSpanUnison', 'line', pillar=3, rain=(2.2, 0.01), waves=[('ricochetImp', 1, 11, 3, (1.0, 2.4)), ('imp', 1, 14, 6, (1.2, 2.0))]),
             piece('ricochet', 'RICOCHET ROW!', 'runeSpan', 'wave', pillar=5, rain=(1.8, 0.011), aimed=3.4, waves=[('ricochetImp', 2, 10, 3, (1.0, 2.6))]),
             piece('hall', 'THE HALL OF BOUNCES!', 'runeSpanQuick', 'zig', pillar=5, rain=(1.4, 0.012), aimed=2.8, waves=[('ricochetImp', 2, 8, 2, (1.0, 2.6)), ('imp', 1, 12, 5, (1.4, 2.0))]),
         ]),
    dict(n=797, name='The First Watcher', title='FLOOR 797: THE FIRST WATCHER', title_say='[shouting] Floor seven ninety-seven! [pause] The first WATCHER!',
         words='THIS IS THE BRIDGE', opening=['watcher'],
         h1=('It\'s just looking. Why is it looking?', '[deadpan] It\'s just looking. [pause] Why is it looking?'),
         h2=('Please stop looking at me.', '[deadpan] [sighs] Please stop looking at me.'),
         land=('Watched me the whole way. Never waved.', '[deadpan] Watched me the whole way. [pause] Never waved.'),
         next_say='[shouting] Floor seven ninety-six! [pause] Out of step!', next_title='FLOOR 796: OUT OF STEP',
         pieces=[
             piece('eye', 'IT BLINKED!', 'runeSpan', 'line', pillar=4, rain=(2.8, 0.008)),
             piece('lid', 'SHOOT AS IT OPENS!', 'runeSpan', 'zig', pillar=4, rain=(2.4, 0.01), waves=[('watcher', 1, 14, 3, (1.4, 2.0))]),
             piece('gaze', 'THE WATCHERS WATCH!', 'runeSpanUnison', 'line', pillar=4, rain=(2.2, 0.01), waves=[('watcher', 1, 12, 3, (1.4, 2.0)), ('imp', 1, 14, 6, (1.0, 2.4))]),
             piece('crossing', 'CROSSING UNDER GAZE!', 'runeSpan', 'wave', pillar=5, rain=(1.8, 0.011), aimed=3.4, waves=[('watcher', 2, 12, 3, (1.4, 2.2))]),
             piece('stare', 'THE STARING CONTEST!', 'runeSpanQuick', 'zig', pillar=5, rain=(1.5, 0.012), aimed=2.8, waves=[('watcher', 2, 9, 2, (1.4, 2.2)), ('imp', 1, 12, 5, (1.0, 2.4))]),
         ]),
    dict(n=796, name='Out Of Step', title='FLOOR 796: OUT OF STEP', title_say='[shouting] Floor seven ninety-six! [pause] Out of step!',
         words='BRIDGE (BEHIND)', opening=['bulwarkImp'],
         h1=('Wait for the hum. Hate the hum.', '[deadpan] Wait for the hum. [pause] I hate the hum.'),
         h2=('Shield on the front. Of course.', '[deadpan] Shield on the front. [pause] Of course.'),
         land=('Half of it was in the wrong place.', '[deadpan] Half of it was in the wrong place.'),
         next_say='[shouting] Floor seven ninety-five! [pause] The stagger!', next_title='FLOOR 795: THE STAGGER',
         pieces=[
             piece('late', 'ONE BEHIND!', 'runeSpanUnison', 'line', pillar=3, rain=(2.6, 0.008), mix='runeSpanLate'),
             piece('stagger', 'OUT OF STEP!', 'runeSpanUnison', 'zig', pillar=4, rain=(2.2, 0.01), waves=[('bulwarkImp', 1, 12, 4, (1.0, 2.4))], mix='runeSpanLate'),
             piece('lids', 'LIDS AND SHIELDS!', 'runeSpan', 'wave', pillar=4, rain=(2.0, 0.01), waves=[('bulwarkImp', 1, 11, 3, (1.0, 2.4)), ('watcher', 1, 14, 6, (1.4, 2.0))]),
             piece('offbeat', 'OFF THE BEAT!', 'runeSpanUnison', 'zig', pillar=5, rain=(1.7, 0.011), aimed=3.4, waves=[('bulwarkImp', 2, 10, 3, (1.0, 2.6))], mix='runeSpanLate'),
             piece('reel', 'THE REEL!', 'runeSpanQuick', 'wave', pillar=5, rain=(1.4, 0.012), aimed=2.8, waves=[('bulwarkImp', 1, 9, 2, (1.0, 2.6)), ('watcher', 1, 11, 5, (1.4, 2.0))]),
         ]),
    dict(n=795, name='The Stagger', title='FLOOR 795: THE STAGGER', title_say='[shouting] Floor seven ninety-five! [pause] The STAGGER!',
         words='STILL BRIDGE', opening=['blinkImp'],
         h1=('It blinks and it\'s right there. Everything here blinks.', '[deadpan] It blinks and it\'s right there. [pause] Everything here blinks.'),
         h2=('Bridge. Imp. Bridge. Imp.', '[deadpan] Bridge. [pause] Imp. [pause] Bridge. [pause] Imp.'),
         land=('Still the bridge. Still not funny.', '[deadpan] Still the bridge. [pause] Still not funny.'),
         next_say='[shouting] Floor seven ninety-four! [pause] The hum and the glow!', next_title='FLOOR 794: THE HUM AND THE GLOW',
         pieces=[
             piece('blink', 'IT BLINKED AGAIN!', 'runeSpan', 'line', pillar=4, rain=(2.6, 0.008)),
             piece('near', 'RIGHT BEHIND YOU!', 'runeSpan', 'zig', pillar=4, rain=(2.2, 0.01), waves=[('blinkImp', 1, 13, 4, (1.0, 2.4))]),
             piece('pairs', 'TWO AT A TIME!', 'runeSpanUnison', 'line', pillar=4, rain=(2.0, 0.01), waves=[('blinkImp', 1, 11, 3, (1.0, 2.4)), ('bulwarkImp', 1, 14, 6, (1.0, 2.4))], mix='runeSpanLate'),
             piece('flicker', 'FLICKER!', 'runeSpanQuick', 'wave', pillar=5, rain=(1.7, 0.011), aimed=3.4, waves=[('blinkImp', 2, 10, 3, (1.0, 2.6))]),
             piece('mirror', 'NOW YOU SEE IT!', 'runeSpan', 'zig', pillar=5, rain=(1.4, 0.012), aimed=2.8, waves=[('blinkImp', 2, 8, 2, (1.0, 2.6)), ('watcher', 1, 12, 5, (1.4, 2.0))]),
         ]),
    dict(n=794, name='The Hum And The Glow', title='FLOOR 794: THE HUM AND THE GLOW', title_say='[shouting] Floor seven ninety-four! [pause] The hum and the GLOW!',
         words='BRIDGE, LIT', opening=['leadImp'],
         h1=('That one leads its shots. Show-off.', '[deadpan] That one leads its shots. [pause] Show-off.'),
         h2=('Hum, glow, gone. Like every plan I\'ve had.', '[deadpan] Hum. Glow. Gone. [pause] Like every plan I\'ve had.'),
         land=('Led by a bridge. Fine.', '[deadpan] Led by a bridge. [pause] Fine.'),
         next_say='[shouting] Floor seven ninety-three! [pause] Glance away!', next_title='FLOOR 793: GLANCE AWAY',
         pieces=[
             piece('lead', 'IT LEADS YOU!', 'runeSpan', 'line', pillar=4, rain=(2.6, 0.008)),
             piece('aim', 'WHERE YOU WILL BE!', 'runeSpan', 'zig', pillar=4, rain=(2.2, 0.01), waves=[('leadImp', 1, 13, 4, (1.0, 2.4))]),
             piece('glow', 'GLOW!', 'runeSpanQuick', 'line', pillar=4, rain=(2.0, 0.01), waves=[('leadImp', 1, 11, 3, (1.0, 2.4)), ('imp', 1, 14, 6, (1.2, 2.0))]),
             piece('hum', 'THE HUM!', 'runeSpanUnison', 'wave', pillar=5, rain=(1.7, 0.011), aimed=3.4, waves=[('leadImp', 2, 10, 3, (1.0, 2.6))], mix='runeSpanLate'),
             piece('gone', 'GONE!', 'runeSpan', 'zig', pillar=5, rain=(1.4, 0.012), aimed=2.8, waves=[('leadImp', 2, 8, 2, (1.0, 2.6)), ('watcher', 1, 12, 5, (1.4, 2.0))]),
         ]),
    dict(n=793, name='Glance Away', title='FLOOR 793: GLANCE AWAY', title_say='[shouting] Floor seven ninety-three! [pause] Glance away!',
         words='BRIDGE (WATCHED)', opening=['watcherPatrol'],
         h1=('It lights the bridge where it looks. So I\'ll look somewhere else.', '[deadpan] It lights the bridge where it looks. [pause] So I\'ll look somewhere else.'),
         h2=('Please stop looking at me. Look at the bridge.', '[deadpan] Please stop looking at me. [pause] Look at the bridge.'),
         land=('The bridge goes where it looks. Noted.', '[deadpan] The bridge goes where it looks. [pause] Noted.'),
         next_say='[shouting] Floor seven ninety-two! [pause] Still the bridge!', next_title='FLOOR 792: STILL THE BRIDGE',
         pieces=[
             piece('follow', 'IT FOLLOWS THE EYE!', 'runeSpanGaze', 'line', pillar=3, rain=(2.8, 0.008), gaze=True),
             piece('eyes', 'TWO EYES!', 'runeSpanGaze', 'zig', pillar=3, rain=(2.4, 0.01), waves=[('imp', 1, 13, 4, (1.0, 2.4))], gaze=True),
             piece('steps', 'WHERE IT LOOKS!', 'runeSpanGaze', 'line', pillar=4, rain=(2.0, 0.01), waves=[('bulwarkImp', 1, 12, 3, (1.0, 2.4))], gaze=True),
             piece('turn', 'IT LOOKS AWAY!', 'runeSpan', 'wave', pillar=5, rain=(1.7, 0.011), aimed=3.4, waves=[('watcher', 1, 11, 3, (1.4, 2.0)), ('imp', 1, 12, 5, (1.0, 2.4))]),
             piece('watched', 'WATCHED THE WHOLE WAY!', 'runeSpanGaze', 'zig', pillar=5, rain=(1.4, 0.012), aimed=2.8, waves=[('blinkImp', 1, 10, 3, (1.0, 2.6)), ('imp', 1, 11, 5, (1.2, 2.2))], gaze=True),
         ]),
    dict(n=792, name='Still The Bridge', title='FLOOR 792: STILL THE BRIDGE', title_say='[shouting] Floor seven ninety-two! [pause] Still the bridge!',
         words='BRIDGE. MORE BRIDGE.', opening=[],
         h1=('Where it looks, there\'s a bridge. Where I look, there\'s a problem.', '[deadpan] Where it looks, there\'s a bridge. [pause] Where I look, there\'s a problem.'),
         h2=('Big arch up ahead. Bigger than the bridge.', '[deadpan] Big arch up ahead. [pause] Bigger than the bridge.'),
         land=('Something\'s holding all this up.', '[deadpan] Something\'s holding all this up.'),
         next_say='[shouting] Floor seven ninety-one! [pause] The Keystone!', next_title='FLOOR 791: THE KEYSTONE',
         pieces=[
             piece('arch', 'THE ARCH!', 'runeSpan', 'wave', pillar=4, rain=(2.4, 0.01), waves=[('imp', 1, 12, 4, (1.0, 2.4))]),
             piece('quickArch', 'ARCH, QUICKLY!', 'runeSpanQuick', 'zig', pillar=4, rain=(2.0, 0.01), waves=[('ricochetImp', 1, 12, 3, (1.0, 2.4)), ('watcher', 1, 14, 6, (1.4, 2.0))]),
             piece('stagger', 'EVERYTHING OUT OF STEP!', 'runeSpanUnison', 'zig', pillar=4, rain=(1.8, 0.011), aimed=3.6, waves=[('bulwarkImp', 1, 11, 3, (1.0, 2.4)), ('leadImp', 1, 13, 5, (1.2, 2.2))], mix='runeSpanLate'),
             piece('eyes', 'ALL THE EYES!', 'runeSpanGaze', 'line', pillar=4, rain=(1.6, 0.012), aimed=3.0, waves=[('watcher', 1, 10, 3, (1.4, 2.0)), ('blinkImp', 1, 12, 5, (1.0, 2.4))], gaze=True),
             piece('span', 'THE LAST SPAN!', 'runeSpan', 'wave', pillar=6, rain=(1.3, 0.013), aimed=2.6, waves=[('ricochetImp', 1, 9, 2, (1.0, 2.6)), ('leadImp', 1, 11, 4, (1.2, 2.2)), ('watcher', 1, 13, 6, (1.4, 2.0))]),
         ]),
]


EXTRA_AUDIO = [
    {'id': 'rune_hum', 'kind': 'Effect', 'prompt': 'A soft low crystalline hum swelling quickly, a rune lighting up, glassy and cold, a gentle rising tone. One-shot, dry, no music.',
     'durationSeconds': 1.0, 'promptInfluence': 0.6, 'loop': False, 'voice': '', 'voiceId': '', 'clip': 'Audio/Generated/rune_hum.mp3',
     'generatedFrom': 'effect|1|0.6|False|A soft low crystalline hum swelling quickly, a rune lighting up, glassy and cold, a gentle rising tone. One-shot, dry, no music.', 'volume': 0.5, 'pitchVariation': 0.05, 'maxVoices': 2, 'cooldown': 0.3},
]


# ---------------------------------------------------------------- level builder

SEC_FOOT, SEC_PIECE, SEC_FAR = 12.0, 30.0, 12.0


def sp(entity, x, y, **kw):
    d = {'entity': entity, 'at': [round(x, 2), round(y, 2)]}
    d.update(kw)
    return d


def build_floor(f, position):
    n = f['n']
    pieces = f['pieces']
    sections = []
    # Camera x at the start of each section.
    starts = [0.0, SEC_FOOT]
    for i in range(1, len(pieces)):
        starts.append(starts[-1] + SEC_PIECE)
    far_start = starts[-1] + SEC_PIECE
    end_x = far_start + SEC_FAR
    secs = [('foot', SEC_FOOT, 0.6, 0.0)] + [(p['name'], SEC_PIECE, 0.55, starts[i + 1]) for i, p in enumerate(pieces)] + [('far', SEC_FAR, 0.6, far_start)]
    secs[0] = ('foot', SEC_FOOT, 0.6, 0.0)

    land_x = end_x + 0.4
    # The bridge's elements: x positions from the start ledge on, one pitch apart, the last a gap from the landing.
    xs = []
    x = 1.0
    last = land_x - 3.0 - 1.4 - 1.2          # the last span's centre: its right edge a 1.4 gap from the landing's left edge
    while x <= last + 0.01:
        xs.append(round(x, 2))
        x += PITCH
    # Spread so the last lands exactly: stretch the pitch a hair.
    if len(xs) > 1:
        step = (last - xs[0]) / (len(xs) - 1)
        step = min(step, PITCH + 0.15)
        xs = [round(xs[0] + step * k, 2) for k in range(len(xs))]

    def section_of(x):
        # The section whose camera range is [start + 2 .. end + 2) holds it (spawned one section ahead).
        for j in range(len(secs) - 1, -1, -1):
            if x >= secs[j][3] + 2.0 - 0.01 - (2.0 if j == 0 else 0.0):
                return j
        return 0

    spawns = [[] for _ in secs]
    spawns[0].append(sp('ledge', -3.6, BASE_Y - 0.25, id='start', scale=[2.0, 1]))
    elem = []
    for k, x in enumerate(xs):
        j = section_of(x)
        pj = j - 1 if 1 <= j <= len(pieces) else None
        p = pieces[pj] if pj is not None else None
        if p is None:
            kind, layout, pillar = ('runeSpanHold' if n == 800 else 'runeSpan'), 'line', 2
        else:
            kind, layout, pillar = p['kind'], p['layout'], p['pillar']
            if p['mix'] and k % 2 == 1:
                kind = p['mix']
        ys = LAYOUTS[layout]
        y = BASE_Y + ys[k % len(ys)]
        is_pillar = (k % pillar == 0) or k == len(xs) - 1 or k == 0
        e = 'runePillar' if is_pillar else kind
        spawns[j].append(sp(e, x, y, scale=[WIDTH, 1]))
        elem.append((x, y))
    # A gaze piece's bridge follows Watchers that patrol above it.
    for i, p in enumerate(pieces):
        if p['gaze']:
            for dx in (7.0, 21.0):
                spawns[i + 1].append(sp('watcherPatrol', starts[i + 1] + dx, 0.9))
    # The landing, and the enemy that opens the floor.
    spawns[-1].append(sp('floorLanding%d' % n, land_x, BASE_Y, id='landing'))
    opening = f['opening']
    for i, e in enumerate(opening):
        spawns[0].append(sp(e, 8.0 + 2.5 * i, 0.9, id='opening%d' % i, onEnter=True))

    path = []
    for j, (name, length, auto, _start) in enumerate(secs):
        path.append({'name': name, 'direction': 'Right', 'length': length, 'turn': 2.0, 'spawns': spawns[j], 'autoSpeed': auto})

    hero = lambda text, snd, wait=0.0: {'type': 'runSequence', 'steps': ([{'type': 'wait', 'seconds': wait}] if wait else []) +
                                        [{'type': 'showText', 'text': text, 'duration': 3.0, 'style': 'Subtitle', 'sound': snd}]}
    rules = [
        {'name': 'hero: first line', 'when': {'type': 'atTime', 'seconds': 14.0}, 'maxTimes': 1,
         'do': [hero(f['h1'][0], 'vo_f%d_a' % n)]},
        {'name': 'hero: second line', 'when': {'type': 'sectionReached', 'section': pieces[1]['name']}, 'maxTimes': 1,
         'do': [hero(f['h2'][0], 'vo_f%d_b' % n, 6.0)]},
    ]
    for p in pieces:
        rules.append({'name': 'to ' + p['name'], 'when': {'type': 'sectionReached', 'section': p['name']}, 'maxTimes': 1,
                      'do': [{'type': 'goToPhase', 'phase': p['name']}]})
    rules.append({'name': 'the bridge has had its minutes', 'when': {'type': 'atTime', 'seconds': 270.0}, 'maxTimes': 1,
                  'do': [{'type': 'setCounter', 'name': 'finale', 'value': 1.0}]})
    rules.append({
        'name': 'the landing',
        'when': {'type': 'allOf', 'triggers': [{'type': 'playerOn', 'target': {'id': 'landing'}}, {'type': 'counter', 'name': 'finale', 'atLeast': 1.0}]},
        'maxTimes': 1,
        'do': [{'type': 'runSequence', 'steps': [
            {'type': 'setFloor', 'floor': n - 1, 'skipMilestones': True},
            {'type': 'showText', 'text': f['land'][0], 'duration': 3.0, 'style': 'Subtitle', 'sound': 'vo_f%d_c' % n},
            {'type': 'wait', 'seconds': 2.4},
            {'type': 'showText', 'text': f['next_title'], 'duration': 3.2, 'style': 'Banner', 'sound': 'vo_f%d_clear' % n, 'color': GOLD},
            {'type': 'wait', 'seconds': 2.6},
            {'type': 'offerUpgrades', 'title': 'Pick a grudge'},
            {'type': 'setCounter', 'name': 'cleared', 'value': 1},
        ]}]})

    phases = [{'name': 'ledge', 'onEnter': [{'type': 'runSequence', 'steps': [
        {'type': 'showText', 'text': f['title'], 'duration': 3.5, 'style': 'Banner', 'sound': 'vo_f%d_title' % n, 'color': GOLD}]}]}]
    for i, p in enumerate(pieces):
        on = [{'type': 'emit', 'signal': 'setpiece:' + p['name']},
              {'type': 'showText', 'text': p['banner'], 'duration': 2.4, 'style': 'Banner', 'color': GOLD}]
        if p['rain']:
            interval, ramp = p['rain']
            on.append({'type': 'startHazard', 'id': 'rain', 'projectile': 'runeBolt', 'pattern': {'type': 'rain', 'count': 1}, 'speed': 3.0,
                       'interval': interval, 'initialDelay': 1.5, 'rampPerSecond': ramp})
        if p['aimed']:
            on.append({'type': 'startHazard', 'id': 'aimed', 'projectile': 'runeBolt', 'pattern': {'type': 'spread', 'count': 3, 'angle': 34},
                       'aim': 'Player', 'origin': [8.0, 0.5], 'anchor': 'Camera', 'speed': 3.0, 'interval': p['aimed'], 'initialDelay': 2.0})
        prules = []
        for (ent, count, every, after, (cy, ch)) in p['waves']:
            prules.append({'name': '%s: %s' % (p['name'], ent),
                           'when': {'type': 'every', 'seconds': float(every), 'startAfter': float(after), 'maxTimes': 0},
                           'do': [{'type': 'spawnWave', 'entity': ent, 'count': count, 'interval': 0.9, 'anchor': 'Camera',
                                   'areaCenter': [4.0, cy], 'areaSize': [1.2, ch], 'tags': ['wave']}]})
        ph = {'name': p['name'], 'onEnter': on}
        if prules:
            ph['rules'] = prules
        phases.append(ph)

    return {
        'schemaVersion': 1, 'levelName': 'Floor%d' % n, 'displayName': 'Floor %d: %s' % (n, f['name']), 'levelNumber': position,
        'thumbnail': 'Videos/Video2_FirstFrame.png', 'backgrounds': [], 'backgroundLayers': [],
        'music': 'Audio/Music/wastes_loop.ogg', 'musicVolume': 0.85, 'platforms': [], 'placedSprites': [], 'enemies': [],
        'playerSpawnPosition': [-4.0, 0.0], 'deathConfigName': 'Death1', 'duration': 420.0, 'completionDelay': 2.0,
        'nextVideoNumber': 0, 'nextLevelNumber': 0 if n == FLOORS[-1]['n'] else position + 1, 'returnToMainMenuOnComplete': False,
        'script': {
            'seed': n, 'length': 300,
            'floors': {'start': n, 'end': n - 1, 'unitsPerFloor': 0.0, 'circle': 'runeBridges', 'signs': False},
            'world': {'camera': 'Travel', 'travelLead': 1.2, 'followSharpness': 4.0, 'holdForPushes': False, 'path': path},
            'objective': {'complete': {'type': 'counter', 'name': 'cleared', 'atLeast': 1}, 'text': 'Cross'},
            'counters': [{'name': 'finale', 'value': 0.0}],
            'rules': rules, 'phases': phases,
        },
    }


def landing(f):
    return {'schemaVersion': 1, 'extends': 'floorLanding',
            'abilities': [{'type': 'carved', 'text': str(f['n']), 'words': f['words'], 'color': '#BFEFFF', 'size': 0.6, 'above': -1.1, 'onFace': True}]}


def audio_entries(f):
    n = f['n']
    rows = [('title', f['title_say'], 'announcer'), ('a', f['h1'][1], 'player'), ('b', f['h2'][1], 'player'),
            ('c', f['land'][1], 'player'), ('clear', f['next_say'], 'announcer')]
    out = []
    for suffix, say, voice in rows:
        vid = PLAYER_VOICE if voice == 'player' else ANNOUNCER_VOICE
        out.append({'id': 'vo_f%d_%s' % (n, suffix), 'kind': 'Voice', 'prompt': say, 'durationSeconds': 0, 'promptInfluence': 0.3, 'loop': False,
                    'voice': voice, 'voiceId': '', 'clip': 'Audio/Generated/vo_f%d_%s.mp3' % (n, suffix),
                    'generatedFrom': 'voice|%s|eleven_v3|0.5|1|%s' % (vid, say), 'volume': 1, 'pitchVariation': 0, 'maxVoices': 1, 'cooldown': 0.5})
    return out


def patch_audio():
    path = os.path.join(ROOT, 'audio.json')
    text = open(path, encoding='utf-8').read()
    have = set(json.loads(text)['sounds'] and [x['id'] for x in json.loads(text)['sounds']])
    rows = []
    for f in FLOORS:
        rows += audio_entries(f)
    rows += EXTRA_AUDIO
    new = [r for r in rows if r['id'] not in have]
    if not new:
        return 0
    blob = ',\n'.join('    ' + json.dumps(r, indent=2, ensure_ascii=False).replace('\n', '\n    ') for r in new)
    # Inside the file, after the Spire Fiend's last line (not at the end: other branches append there).
    anchor = '"id": "vo_f802_clear"'
    i = text.index(anchor)
    j = text.index('\n    },\n', i) + len('\n    },\n')
    text = text[:j] + blob + ',\n' + text[j:]
    open(path, 'w', encoding='utf-8', newline='\n').write(text)
    return len(new)


def patch_game(first_position):
    path = os.path.join(ROOT, 'game.json')
    text = open(path, encoding='utf-8').read()
    d = json.loads(text)
    names = ['Floor%d' % f['n'] for f in FLOORS]
    if names[0] in d['levels']:
        return
    d['levels'] = d['levels'] + names
    assert len(d['levels']) - len(names) + 1 == first_position, 'first_position must be the 1-based position of Floor800 in game.json'
    # Keep the file's own one-line list.
    line = '  "levels": [' + ', '.join('"%s"' % n for n in d['levels']) + '],'
    import re
    text = re.sub(r'  "levels": \[[^\]]*\],', lambda m: line, text, count=1)
    open(path, 'w', encoding='utf-8', newline='\n').write(text)


def main(first_position):
    for name, data in ENTITIES.items():
        dump(os.path.join(ENT, name + '.json'), data)
    for i, f in enumerate(FLOORS):
        dump(os.path.join(ENT, 'floorLanding%d.json' % f['n']), landing(f))
        dump(os.path.join(LEV, 'Floor%d.json' % f['n']), build_floor(f, first_position + i))
    print('wrote %d floors, %d entities, %d audio lines' % (len(FLOORS), len(ENTITIES), patch_audio()))
    patch_game(first_position)


if __name__ == '__main__':
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 80)
