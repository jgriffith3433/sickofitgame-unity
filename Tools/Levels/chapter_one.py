#!/usr/bin/env python3
"""
Chapter One payoff on Floor 997 (Docs/Story.md "Chapter One closes on Floor 997").

After the Imp Run finale / landing, a playable Overlord face-off (BeatDirector find, three lens phrases,
voiced exchange, escape up the Shaft), then a CHAPTER ONE card. Flag chapter1.seen offers Skip on replay.

  python3 Tools/Levels/chapter_one.py            write
  python3 Tools/Levels/chapter_one.py --check    no Unity: Floor997 has chapterOne phase + signals

Run AFTER long_floors.py / first_floors.py / overlord_cameos.py / companions.py (they rewrite floors).
"""
import json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, 'Assets', 'GameConfig', 'levels', 'Floor997.json')
FLAG = 'chapter1.seen'
STING = 'music_chapter_one_sting'
OVL_COLOR = '#C9A0FF'

LINES = [
    ('vo_ch1_ovl_caught', 'Mm. ... Caught up.', OVL_COLOR, 'ovlChapter'),
    ('vo_ch1_hero_looking', "Been looking for you.", None, 'player'),
    ('vo_ch1_ovl_watch', 'Mm. ... Watch.', OVL_COLOR, 'ovlChapter'),
    ('vo_ch1_hero_always', 'I always do.', None, 'player'),
    ('vo_ch1_ovl_far', 'Mm. ... Not far enough.', OVL_COLOR, 'ovlChapter'),
]
HOOK = ('vo_ch1_hero_daylight', "Daylight's still up there. Somewhere.")


def load():
    with open(PATH) as f:
        return json.load(f)


def save(d):
    with open(PATH, 'w') as f:
        json.dump(d, f, indent=2)
        f.write('\n')


def say(sound, text, color, actor, wait=2.8):
    step = {
        'type': 'say',
        'actor': actor,
        'text': text,
        'sound': sound,
        'minSeconds': 0.8,
        'pad': 0.15,
    }
    if color:
        step['color'] = color
    return [
        step,
        {'type': 'wait', 'seconds': wait},
    ]


def find_beat_on_skip():
    return [
        {'type': 'spawn', 'entity': 'overlordChapter', 'id': 'ovlChapter', 'anchor': 'Camera',
         'at': [1.4, 1.55], 'facePlayer': False},
        {'type': 'emit', 'signal': 'chapter1:found'},
    ]


def encounter_steps():
    """Beat find + three lens phrases + lines, then escape phase. ~28 s of playable dodge time after unlock."""
    return [
        {'type': 'beatStart', 'id': 'chapterOneFind', 'maxSeconds': 4.5, 'unlockAtEnd': True,
         'onSkip': find_beat_on_skip()},
        {'type': 'wait', 'seconds': 0.35},
        {'type': 'cameraFocus', 'target': 'player', 'at': [1.2, 0.8], 'size': 5.2, 'seconds': 0.55},
        {'type': 'spawn', 'entity': 'overlordChapter', 'id': 'ovlChapter', 'anchor': 'Camera',
         'at': [1.4, 1.55], 'facePlayer': False},
        {'type': 'emit', 'signal': 'chapter1:found'},
        {'type': 'cameraShake', 'intensity': 0.12, 'duration': 0.35},
        {'type': 'playSound', 'sound': 'overlord_beam_charge', 'volume': 0.85},
        {'type': 'say', 'actor': 'ovlChapter', 'text': 'Mm. ... Caught up.', 'sound': 'vo_ch1_ovl_caught',
         'color': OVL_COLOR, 'minSeconds': 0.9, 'pad': 0.1},
        {'type': 'beatEnd'},
        {'type': 'cameraReset', 'duration': 0.4},
        {'type': 'emit', 'signal': 'chapter1:phrase1'},
        {'type': 'say', 'actor': 'player', 'text': 'Been looking for you.', 'sound': 'vo_ch1_hero_looking',
         'minSeconds': 0.7, 'pad': 0.1},
        {'type': 'wait', 'seconds': 5.5},
        {'type': 'despawn', 'target': {'id': 'ovlChapter'}},
        {'type': 'spawn', 'entity': 'overlordChapterP2', 'id': 'ovlChapter', 'anchor': 'Camera',
         'at': [1.4, 1.55], 'facePlayer': False},
        {'type': 'emit', 'signal': 'chapter1:phrase2'},
        {'type': 'playEntityAnimation', 'target': {'id': 'ovlChapter'}, 'animation': 'Point'},
        {'type': 'say', 'actor': 'ovlChapter', 'text': 'Mm. ... Watch.', 'sound': 'vo_ch1_ovl_watch',
         'color': OVL_COLOR, 'minSeconds': 0.8, 'pad': 0.1},
        {'type': 'say', 'actor': 'player', 'text': 'I always do.', 'sound': 'vo_ch1_hero_always',
         'minSeconds': 0.6, 'pad': 0.1},
        {'type': 'wait', 'seconds': 6.0},
        {'type': 'despawn', 'target': {'id': 'ovlChapter'}},
        {'type': 'spawn', 'entity': 'overlordChapterP3', 'id': 'ovlChapter', 'anchor': 'Camera',
         'at': [1.4, 1.55], 'facePlayer': False},
        {'type': 'emit', 'signal': 'chapter1:phrase3'},
        {'type': 'say', 'actor': 'ovlChapter', 'text': 'Mm. ... Not far enough.', 'sound': 'vo_ch1_ovl_far',
         'color': OVL_COLOR, 'minSeconds': 0.9, 'pad': 0.1},
        {'type': 'wait', 'seconds': 5.0},
        {'type': 'goToPhase', 'phase': 'chapterOneEscape'},
    ]


def chapter_one_phase():
    """Setup, then offerChapterSkip (first play runs onWatch at once; replay offers Skip)."""
    on_enter = [
        {'type': 'lockTravel'},
        {'type': 'stopHazard', 'id': 'rain'},
        {'type': 'stopHazard', 'id': 'aimed'},
        # Keep chain/imp ambience; only clear the fight waves so the ledge still feels like Imp Run.
        {'type': 'despawn', 'target': {'tag': 'wave'}},
        {'type': 'despawn', 'target': {'tag': 'brute'}},
        {'type': 'emit', 'signal': 'chapter1:start'},
        {'type': 'playSound', 'sound': 'amb_chain_rattle', 'volume': 0.55},
        {'type': 'offerChapterSkip', 'title': 'CHAPTER ONE',
         'watchLabel': 'Face him again', 'skipLabel': 'Skip',
         'onSkip': [
             {'type': 'emit', 'signal': 'chapter1:skipped'},
             {'type': 'goToPhase', 'phase': 'chapterOneCard'},
         ],
         'onWatch': [
             {'type': 'runSequence', 'steps': encounter_steps()},
         ]},
    ]
    rules = [
        {
            'name': 'chapter1: death-rewind shows his back',
            'when': {'type': 'heroRewound'},
            'maxTimes': 3,
            'do': [
                {'type': 'playEntityAnimation', 'target': {'id': 'ovlChapter'}, 'animation': 'Flee'},
                {'type': 'emit', 'signal': 'chapter1:back'},
            ],
        },
    ]
    return {'name': 'chapterOne', 'onEnter': on_enter, 'rules': rules}


def escape_phase():
    return {
        'name': 'chapterOneEscape',
        'onEnter': [
            {'type': 'runSequence', 'steps': [
                {'type': 'emit', 'signal': 'chapter1:escape'},
                {'type': 'playEntityAnimation', 'target': {'id': 'ovlChapter'}, 'animation': 'Laugh'},
                {'type': 'playSound', 'sound': 'vo_ovl_t_higher', 'volume': 0.7},
                {'type': 'cameraShake', 'intensity': 0.2, 'duration': 0.55},
                {'type': 'wait', 'seconds': 0.7},
                {'type': 'playEntityAnimation', 'target': {'id': 'ovlChapter'}, 'animation': 'Flee'},
                {'type': 'wait', 'seconds': 1.1},
                {'type': 'despawn', 'target': {'id': 'ovlChapter'}},
                {'type': 'playSound', 'sound': STING, 'volume': 1.0},
                {'type': 'emit', 'signal': 'chapter1:sting'},
                {'type': 'wait', 'seconds': 0.9},
                {'type': 'goToPhase', 'phase': 'chapterOneCard'},
            ]},
        ],
        'rules': [],
    }


def card_phase():
    # CHAPTER ONE card first (showChapterCard); Continue then Grudge + clear. chapterOne counter also
    # makes the normal results screen a no-op if it would open again.
    return {
        'name': 'chapterOneCard',
        'onEnter': [
            {'type': 'lockTravel'},
            {'type': 'setFlag', 'flag': FLAG},
            {'type': 'setCounter', 'name': 'chapterOne', 'value': 1},
            {'type': 'emit', 'signal': 'chapter1:card'},
            {'type': 'emit', 'signal': 'floors:997:payoff'},
            {'type': 'showChapterCard',
             'title': 'CHAPTER ONE',
             'hookSound': HOOK[0],
             'hookText': HOOK[1]},
        ],
        'rules': [],
    }


def patch_landing(rules):
    """Landing starts the chapter phase instead of clearing at once."""
    out = []
    for r in rules:
        if r.get('name') == 'the landing':
            r = {
                'name': 'the landing',
                'when': {'type': 'playerOn', 'target': {'id': 'landing'}},
                'maxTimes': 1,
                'do': [
                    {'type': 'setFloor', 'floor': 996, 'skipMilestones': True},
                    {'type': 'setCounter', 'name': 'lavaSpeed', 'value': 0.0},
                    {'type': 'goToPhase', 'phase': 'chapterOne'},
                ],
            }
        if isinstance(r.get('name'), str) and r['name'].startswith('chapter1:'):
            continue
        out.append(r)
    return out


def strip_old_phases(phases):
    return [p for p in phases if p.get('name') not in ('chapterOne', 'chapterOneEscape', 'chapterOneCard')]


def write():
    d = load()
    s = d['script']
    s['rules'] = patch_landing(s['rules'])
    phases = strip_old_phases(s['phases'])
    # Insert chapter phases after liftStop.
    insert_at = next((i + 1 for i, p in enumerate(phases) if p.get('name') == 'liftStop'), len(phases))
    for phase in (chapter_one_phase(), escape_phase(), card_phase()):
        phases.insert(insert_at, phase)
        insert_at += 1
    s['phases'] = phases
    # Counter for the card / results branch.
    counters = s.setdefault('counters', [])
    if not any(c.get('name') == 'chapterOne' for c in counters):
        counters.append({'name': 'chapterOne', 'value': 0.0})
    save(d)
    print('Floor997: chapterOne phases written')


def check():
    d = load()
    s = d['script']
    blob = json.dumps(s)
    ok = True
    for need in ('chapterOne', 'chapter1:start', 'chapter1:phrase1', 'chapter1:phrase3',
                 'chapter1:escape', 'chapter1:card', 'offerChapterSkip', 'showChapterCard', FLAG,
                 'vo_ch1_ovl_caught'):
        if need not in blob:
            print(f'MISSING {need}')
            ok = False
    landing = next((r for r in s['rules'] if r.get('name') == 'the landing'), None)
    if not landing or 'chapterOne' not in json.dumps(landing):
        print('MISSING landing -> chapterOne')
        ok = False
    names = [p.get('name') for p in s['phases']]
    for n in ('chapterOne', 'chapterOneEscape', 'chapterOneCard'):
        if n not in names:
            print(f'MISSING phase {n}')
            ok = False
    if ok:
        print('chapter_one: up to date')
    return 0 if ok else 1


if __name__ == '__main__':
    if '--check' in sys.argv:
        sys.exit(check())
    write()
    sys.exit(check())
