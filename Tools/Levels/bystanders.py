#!/usr/bin/env python3
"""
Bystanders on the floors (Docs/Story.md "Bystanders on the floors"): writes the five bystander entities, their voice
prompts in audio.json, and one or two spawns in each listed floor's first phase. Safe to run again (spawns carry the
id prefix "by_" and are replaced). Run it after long_floors.py / cracked_floors.py, which rewrite their floors.

  python3 Tools/Levels/bystanders.py
"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from artkeep import guard  # noqa: E402  (generated art is kept: Tools/Levels/artkeep.py)

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'GameConfig')
SOUL = "#D2DAE4"

def line(text, dur, sid):
    return {"type": "showText", "text": text, "duration": dur, "style": "Subtitle", "color": SOUL}

def face():
    return {"type": "faceEntity", "target": "self", "toward": "player"}

def hiccup():
    return {"type": "playEntityAnimation", "target": "self", "state": "Hiccup"}

def step(x, speed, keep=True):
    return {"type": "walkEntityTo", "target": "self", "anchor": "Self", "x": x, "speed": speed, "keepFacing": keep}

def emit(sig):
    return {"type": "emit", "signal": sig}

def react(when, do, times=1):
    return {"type": "react", "when": when, "maxTimes": times, "do": do}

NEAR = lambda d: {"type": "distanceToPlayer", "below": d}
SHOT = lambda d: {"type": "witness", "what": "ShotPassed", "within": d}
HURT = lambda d: {"type": "witness", "what": "HeroHurt", "within": d}

IDS = {'soul': 'ysEGb2K9bN5ooRlOwDTl', 'hawker': 'tXVpIRlCXsO2DDDgyZ4P', 'heckler': 'gX7swrVG71qk3uWMh8RU'}  # cast through Hawkes Gen audio.voice-design/save

# id -> (lines, abilities). Lines are [text, seconds, voice-id, voice prompt].
LINES = {
  "hawker_pitch":  ("Cinders! Fresh cinders! ...Fresh cinders!", 2.6, "[loudly, sing-song, flat on the repeat] Cinders! Fresh cinders! [pause, flatter] Fresh cinders!"),
  "hawker_sample": ("Free sample. It's the same cinder. ...Same cinder.", 2.8, "[offering, flat] Free sample. It's the same cinder. [pause] Same cinder."),
  "hawker_flee":   ("Not the stall! ...Not the stall!", 2.2, "[alarmed] Not the stall! [pause, flatter] Not the stall!"),
  "hawker_hurt":   ("No refunds. ...No refunds.", 2.2, "[shrugs, flat] No refunds. [pause, flatter] No refunds."),
  "heckler_jeer":  ("Is that all? ...Is that all?", 2.2, "[sneering, drawn out] Is that all? [pause, flatter] Is that all?"),
  "heckler_shot":  ("Hey! Aim at someone else! ...Someone else!", 2.8, "[indignant] Hey! Aim at someone else! [pause, flatter] Someone else!"),
  "heckler_hurt":  ("Ha! Do it again. ...Do it again.", 2.4, "[delighted cackle, flat on the repeat] Ha! Do it again. [pause] Do it again."),
  "giver_hand":    ("Here. It's not mine. ...It's not mine.", 2.6, "[mumbling, offering] Here. It's not mine. [pause, flatter] It's not mine."),
  "hider_whisper": ("Shh. ...Shh.", 1.6, "[whispering] Shh. [pause] Shh."),
  "hider_gone":    ("I'm not here. ...I'm not here.", 2.4, "[whispering, terrified] I'm not here. [pause, flatter] I'm not here."),
  "cheer_go":      ("Go on up. ...Go on up.", 2.2, "[encouraging, flat on the repeat] Go on up. [pause] Go on up."),
  "cheer_again":   ("Again! ...Again!", 1.8, "[excited, then flat] Again! [pause] Again!"),
}

def L(key):
    t, d, _ = LINES[key]
    return {"type": "showText", "text": t, "duration": d, "style": "Subtitle", "color": SOUL, "sound": "vo_by_" + key}

ENTITIES = {
  "hawkerSoul": [
    react(NEAR(4.5), [face(), L("hawker_pitch"), emit("by:hawker:pitch")]),
    react(NEAR(2.6), [L("hawker_sample"), {"type": "spawn", "entity": "diamond", "anchor": "Self", "at": [-0.6, 0.45], "facePlayer": False}, emit("by:hawker:sample")]),
    react(SHOT(1.8), [hiccup(), step(3, 4), L("hawker_flee"), emit("by:hawker:flee")]),
    react(HURT(6), [face(), L("hawker_hurt"), emit("by:hawker:hurt")]),
  ],
  "hecklerSoul": [
    react(NEAR(5), [face(), L("heckler_jeer"), emit("by:heckler:jeer")]),
    react(SHOT(0.9), [hiccup(), step(0.6, 3), L("heckler_shot"), emit("by:heckler:stray")]),
    react(SHOT(0.9), [hiccup(), step(0.6, 3)], 0),
    react(HURT(6), [face(), L("heckler_hurt"), emit("by:heckler:laugh")]),
  ],
  "giverSoul": [
    react(NEAR(3.2), [face(), L("giver_hand"), hiccup(),
        {"type": "spawn", "entity": "rewindOrb", "anchor": "Self", "at": [-0.7, 0.4], "facePlayer": False}, emit("by:giver:hand")]),
  ],
  "hidingSoul": [
    react(NEAR(5), [{"type": "faceEntity", "target": "self", "awayFrom": "player"}, L("hider_whisper"), emit("by:hider:whisper")]),
    react(HURT(8), [{"type": "faceEntity", "target": "self", "awayFrom": "player"}, step(2.5, 3, False), L("hider_gone"), emit("by:hider:hide")]),
    react(SHOT(3), [{"type": "faceEntity", "target": "self", "awayFrom": "player"}, step(2.5, 3, False), emit("by:hider:duck")]),
  ],
  "cheerSoul": [
    react(NEAR(4.5), [face(), L("cheer_go"), emit("by:cheer:go")]),
    react(SHOT(2.5), [hiccup(), L("cheer_again"), emit("by:cheer:again")]),
    react(SHOT(2.5), [hiccup()], 0),
  ],
}

# floor -> bystander types, nearest first
FLOORS = {
  999: ["hawkerSoul"], 997: ["hecklerSoul"], 996: ["cheerSoul"], 995: ["hidingSoul"], 994: ["giverSoul"],
  993: ["hawkerSoul", "hecklerSoul"], 992: ["hidingSoul"], 960: ["giverSoul", "cheerSoul"], 958: ["hecklerSoul"],
  955: ["hawkerSoul"], 953: ["cheerSoul", "hidingSoul"], 910: ["hecklerSoul"], 908: ["giverSoul"], 905: ["hawkerSoul", "cheerSoul"],
  904: ["hidingSoul"], 900: ["hecklerSoul", "giverSoul"], 896: ["cheerSoul"], 894: ["hawkerSoul"], 860: ["hidingSoul", "hecklerSoul"],
  854: ["cheerSoul", "hawkerSoul"],
}

def write(path, data):
    data = guard(os.path.join(ROOT, path), data)
    if data is None:
        return
    with open(os.path.join(ROOT, path), 'w', newline='\n') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")

def main():
    for name, abil in ENTITIES.items():
        write(f"content/entities/{name}.json", {"schemaVersion": 1, "extends": "damnedSoul", "tags": ["damned", "bystander"], "abilities": abil})
    # voices and sounds
    ap = os.path.join(ROOT, 'audio.json')
    a = json.load(open(ap))
    voices = {
      "soul": "A damned soul: thin, grey, tired, flat, mumbling; says everything twice, the second time flatter, like a loop. Close-mic studio recording, American accent.",
      "hawker": "A damned soul selling cinders from a tray: hoarse, sing-song, loud, worn out by ten thousand years of the same pitch. Close-mic studio recording.",
      "heckler": "A damned soul in the cheap seats of Hell: nasal, sneering, delighted with himself, drawn-out vowels. Close-mic studio recording.",
    }
    have = {v['name'] for v in a['voices']}
    tmpl = next(v for v in a['voices'] if v['name'] == 'yooba')
    for n, d in voices.items():
        if n not in have:
            v = dict(tmpl); v.update({"name": n, "description": d, "voiceId": IDS[n], "speed": 1, "stability": 0.5, "sampleText": LINES["hawker_pitch"][2] if n == "hawker" else LINES["heckler_jeer"][2] if n == "heckler" else LINES["giver_hand"][2]})
            a['voices'].append(v)
    snd = next(s for s in a['sounds'] if s['id'] == 'vo_f996_title')
    a['sounds'] = [s for s in a['sounds'] if not s['id'].startswith('vo_by_')]
    vmap = {"hawker": "hawker", "heckler": "heckler"}
    for key, (_, _, prompt) in LINES.items():
        s = dict(snd); s.update({"id": "vo_by_" + key, "prompt": prompt, "voice": vmap.get(key.split('_')[0], "soul"), "clip": f"Audio/Generated/vo_by_{key}.mp3",
                  "generatedFrom": f"voice|{IDS[vmap.get(key.split('_')[0], 'soul')]}|eleven_v3|0.5|1|{prompt}", "volume": 0.9})
        a['sounds'].append(s)
    write('audio.json', a)
    # floors
    for f, types in FLOORS.items():
        p = os.path.join(ROOT, f'levels/Floor{f}.json')
        d = json.load(open(p))
        ph = d['script']['phases'][0]
        ph['onEnter'] = [x for x in ph['onEnter'] if not str(x.get('id', '')).startswith('by_')]
        for i, t in enumerate(types):
            ph['onEnter'].append({"type": "spawn", "entity": t, "id": "by_" + t.replace("Soul", ""), "anchor": "Player",
                                  "at": [6.5 + 4.5 * i, 0.35 + 0.3 * i], "facePlayer": False})
        with open(p, 'w', newline='\n') as fh:
            json.dump(d, fh, indent=2, ensure_ascii=False); fh.write("\n")

main()
