#!/usr/bin/env python3
"""The adaptive score's cue table (Docs/Story.md "Music", docs/world-bible.md): three loops per floor (explore, fight, finale)
at one tempo and key, for the Meadow and Floors 1000-997. Edit THIS table, never the levels' `score` blocks.

    python3 Tools/Audio/adaptive_score.py --manifest   write the 15 cues into music_manifest.json as 'to-make' (or 'exists' once the .ogg is there)
    python3 Tools/Audio/adaptive_score.py --wire       write each floor's `score` block (levels/<file>.json) for every floor whose three clips exist
    python3 Tools/Audio/adaptive_score.py --check      the table, the manifest and the levels agree (no Unity)

Generation (Hawkes Gen audio.music then audio.music-loop, checked with audio.analyze loop:true): see music_manifest.json
`adaptiveScore`. Clips are Vorbis, streamed (loadType 1, m_02cb77364146); a floor's score loads with that floor's bundle.
"""
import json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MANIFEST = os.path.join(ROOT, 'Tools', 'Audio', 'music_manifest.json')
LEVELS = os.path.join(ROOT, 'Assets', 'GameConfig', 'levels')

COMMON = "90s arcade video game soundtrack, instrumental, "
ROLES = {
    'explore': "sparse, the floor's motif alone over a soft bed, no drums or only a light pulse, room to breathe, loopable, no intro",
    'fight': "full drum kit with double kicks, distorted guitar chugs, choir stabs, the motif driving hard, same tempo and key as the explore loop, loopable, no intro",
    'finale': "the set piece: the motif at its biggest, a rising choir and brass or organ, drums doubled, a climbing build that peaks, distinct from the fight loop, same tempo and key, loopable, no intro",
}

FLOORS = [
    # id, level file, floor, bpm, key, character (docs/world-bible.md), finale signal
    ('meadow', 'Level1', 'Meadow', 112, 'D minor', "pastoral folk (plucked strings, a wooden flute, a hand drum) turning frantic as the sky opens", 'portal:telegraph'),
    ('f1000', 'Floor1000', '1000', 120, 'A minor', "sulphur and brass: low brass swells, a bowed metal drone, creaking stone percussion", 'floors:1000:finale'),
    ('f999', 'Floor999', '999', 124, 'E minor', "furnace industrial: pounding forge hammers, hissing steam, grinding gears, a detuned bass pulse", 'floors:999:finale'),
    ('f998', 'Floor998', '998', 108, 'F# minor', "bone-percussion and choir: rattling bones, hollow drums, a low whispered choir", 'floors:998:finale'),
    ('f997', 'Floor997', '997', 116, 'C minor', "the Overlord's organ theme: a pipe organ, slow menacing chords, a tolling bell", 'floors:997:finale'),
]


def clip(fid, role):
    return f"Assets/Audio/Music/score_{fid}_{role}.ogg"


def tracks():
    out = []
    for fid, lv, floor, bpm, key, character, sig in FLOORS:
        for role, desc in ROLES.items():
            out.append({
                "id": f"score_{fid}_{role}", "circle": floor, "role": "score", "bpm": bpm, "key": key, "timesignature": "4",
                "prompt": COMMON + f"{character}, {desc}", "seconds": 32, "takes": 1,
                "out": clip(fid, role), "standIn": "Audio/Music/meadow_calm.ogg" if fid == 'meadow' else "Audio/Music/wastes_loop.ogg",
                "status": "exists" if os.path.exists(os.path.join(ROOT, clip(fid, role))) else "to-make",
                "used": f"StageMusic score ({lv} `score.{role}`)",
                "note": "audio.music then audio.music-loop, audio.analyze loop:true; whole bars (32 s = 14-15 bars), same length for the floor's three loops; Vorbis, streamed (loadType 1)."})
    return out


def manifest():
    m = json.load(open(MANIFEST))
    m['tracks'] = [t for t in m['tracks'] if not t['id'].startswith('score_')] + tracks()
    json.dump(m, open(MANIFEST, 'w'), indent=2); open(MANIFEST, 'a').write("\n")


def wire():
    for fid, lv, floor, bpm, key, character, sig in FLOORS:
        if not all(os.path.exists(os.path.join(ROOT, clip(fid, r))) for r in ROLES):
            print(f"{lv}: clips not made yet, not wired"); continue
        p = os.path.join(LEVELS, lv + '.json')
        d = json.load(open(p))
        d['score'] = {"explore": clip(fid, 'explore').replace('Assets/', ''), "fight": clip(fid, 'fight').replace('Assets/', ''),
                      "finale": clip(fid, 'finale').replace('Assets/', ''), "bpm": bpm, "volume": 0.9, "finaleSignal": sig, "fightHoldSeconds": 1.0}
        json.dump(d, open(p, 'w'), indent=2); open(p, 'a').write("\n")
        print(f"{lv}: wired")


def check():
    bad = 0
    ids = {t['id'] for t in json.load(open(MANIFEST))['tracks']}
    for t in tracks():
        if t['id'] not in ids:
            print("manifest lacks", t['id']); bad += 1
    for fid, lv, *_ in FLOORS:
        d = json.load(open(os.path.join(LEVELS, lv + '.json')))
        s = d.get('score')
        if s:
            for r in ROLES:
                if not os.path.exists(os.path.join(ROOT, clip(fid, r))):
                    print(lv, "score names a missing clip", r); bad += 1
    print("adaptive_score: OK" if not bad else f"{bad} problems")
    return bad


if __name__ == '__main__':
    a = sys.argv[1:]
    if '--manifest' in a: manifest()
    elif '--wire' in a: wire()
    else: sys.exit(1 if check() else 0)
