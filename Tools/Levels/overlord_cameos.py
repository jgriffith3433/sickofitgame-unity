#!/usr/bin/env python3
"""
The Overlord on the far ledge (Docs/Story.md "The Overlord on the far ledge"): eight scripted glimpses of him on Circle X's and Circle IX's floors, each a
different interaction, each with his line and the hero's reply.

Writes: the Overlord's stand-in entities (one per scene, they differ in how they move on the screen), the lift, the bait diamond, the voice lines in
audio.json (clips are made through Hawkes Gen: the entries point at Audio/Generated/<id>.mp3), the art spec, and on each floor one rule (and, for the
rewind scene, a second) in the floor's own script. Idempotent: it rebuilds all of it from the table below. Run it after long_floors.py / cracked_floors.py /
elites.py, which rewrite their floors.

    python3 Tools/Levels/overlord_cameos.py          # write
    python3 Tools/Levels/overlord_cameos.py --check  # fail if the files differ from what the table makes (and check the placement)
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from artkeep import guard  # noqa: E402  (generated art is kept: Tools/Levels/artkeep.py)

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CFG = os.path.join(ROOT, "Assets", "GameConfig")
check_only = "--check" in sys.argv
changed = []

HERO = "DzkD7I4tf5i3KSaB4cz5"
OVERLORD = "A5KBJzxznyU8QeeRqyiY"
RED = "#E0605A"
PREFIX = "overlord:"


def load(path):
    with open(os.path.join(CFG, path), encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    full = os.path.join(CFG, path)
    data = guard(full, data)
    if data is None:
        return
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    if os.path.exists(full):
        with open(full, encoding="utf-8") as f:
            if f.read() == text:
                return
    changed.append(path)
    if not check_only:
        with open(full, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


# ─── the table ─────────────────────────────────────────────────────────────────────────────────────────
# floor, section the scene starts on, kind, Overlord's line (text, prompt tail, seconds), hero's reply (text, prompt, seconds), how his stand-in moves.
# Lines: the text is the subtitle; the voice prompt is the same words with delivery tags.
SCENES = [
    dict(floor=997, section="higher", kind="taunt", sound="up",
         ovl=("Mm. ... Up. They always try up.", "[bored] Mm. [long pause] Up. [pause] They always try up.", 3.6),
         hero=("Don't mind me.", "[deadpan] Don't mind me.", 2.0)),
    dict(floor=994, section="cross", kind="drop", sound="catch",
         ovl=("Mm. ... Catch.", "[bored] Mm. [long pause] Catch.", 2.8),
         hero=("Great. A present.", "[deadpan] Great. A present.", 2.2)),
    dict(floor=958, section="bank", kind="lift", sound="higher", drift=[0.0, 2.0], drift_after=3.6,
         ovl=("Mm. ... Higher.", "[bored] Mm. [long pause] Higher.", 2.8),
         hero=("He has a lift. Of course he has a lift.", "[deadpan] He has a lift. [pause] Of course he has a lift.", 3.2)),
    dict(floor=953, section="lap2up", kind="laser", sound="floor",
         ovl=("Mm. ... Floor.", "[bored] Mm. [long pause] Floor.", 2.6),
         hero=("Missed. Or he didn't care.", "[deadpan] Missed. [pause] Or he didn't care.", 2.6)),
    dict(floor=906, section="loop1", kind="steal", sound="mine",
         ovl=("Mm. ... Mine.", "[bored] Mm. [long pause] Mine.", 2.6),
         hero=("It was never yours either.", "[deadpan] It was never yours either.", 2.6)),
    dict(floor=896, section="slope", kind="rewind", sound="down", drift=[-0.8, 0.0], drift_after=1.0,
         ovl=("Mm. ... Down.", "[bored] Mm. [long pause] Down.", 2.6),
         hero=("Let's see what R does to him.", "[deadpan] Let's see what R does to him.", 3.0),
         again=("Hhh. ... Again.", "[long, bored exhale] Hhh. [pause] Again.", 3.0),
         hero2=("Yeah. Same here.", "[deadpan, quietly] Yeah. [pause] Same here.", 2.6)),
    dict(floor=857, section="behind", kind="summon", sound="go",
         ovl=("Mm. ... Go.", "[bored] Mm. [long pause] Go.", 2.6),
         hero=("He sends the help.", "[deadpan] He sends the help.", 2.4)),
    dict(floor=806, section="under", kind="drop-diamond", sound="heavy", drift=[-0.8, 0.0], drift_after=0.8,
         ovl=("Mm. ... Heavy.", "[bored] Mm. [long pause] Heavy.", 2.8),
         hero=("Not picking that up for him. ... Fine. I am.", "[deadpan] Not picking that up for him. [pause] Fine. I am.", 3.6)),
]
OFFSET = [2.8, 1.8]  # from the camera's centre: upper right, small and far


def sid_ovl(sc, tag=None):
    return f"vo_ovl_{sc['floor']}_{tag or sc['sound']}"


def sid_hero(sc, second=False):
    return f"vo_hero_ovl_{sc['floor']}" + ("_again" if second else "")


# ─── entities ──────────────────────────────────────────────────────────────────────────────────────────

def anchor(offset, drift, after):
    return {"type": "screenAnchor", "offset": offset, "drift": drift, "driftAfter": after}


def visual():
    # His own sheet (art/overlordFar.json, Tools/Art/art.mjs): one for every glimpse.
    return {"scale": 0.5, "placeholderColor": "#2A2030", "placeholderSize": 1.6, "placeholderLabel": False,
            "layer": "Background2", "sortingOrder": 3, "hurtFlash": False, "deathPop": False,
            "animator": {"parameters": [], "states": [{"stateName": "Idle", "spriteSheet": "Art/Enemies/OverlordFar/OverlordFar_idle.png", "fps": 12, "loop": True, "events": []}],
                         "transitions": [], "defaultStateName": "Idle"},
            "facesLeft": False}


def build_entities():
    for sc in SCENES:
        drift, after = sc.get("drift", [0.0, 0.0]), sc.get("drift_after", 0.0)
        start = list(OFFSET)
        if sc["kind"] == "drop-diamond" or sc["kind"] == "rewind":
            start = [3.8, 1.8]
        save(f"content/entities/overlordFar{sc['floor']}.json", {
            "schemaVersion": 1, "team": "Neutral", "health": 0, "shootable": False, "tags": ["overlordCameo"],
            "body": {"radius": 0.3, "solid": False}, "visual": visual(), "healthBar": {"show": False},
            "abilities": [anchor(start, drift, after)]})
        sc["start"] = start
    # the lift he rides on Floor 958: rises with him
    lift = next(s for s in SCENES if s["kind"] == "lift")
    save("content/entities/overlordLift958.json", {
        "schemaVersion": 1, "team": "Neutral", "health": 0, "shootable": False, "tags": ["overlordCameo"],
        "body": {"radius": 0.3, "solid": False},
        "visual": {"scale": 1.0, "color": "#FFFFFF", "placeholderColor": "#4A3A35", "placeholderShape": "Box", "placeholderSize": 1.2, "placeholderLabel": False,
                   "layer": "Background2", "sortingOrder": 2, "hurtFlash": False, "deathPop": False,
                   "animator": {"parameters": [], "states": [{"stateName": "Idle", "spriteSheet": "Art/Climb/ChainLift_idle.png", "fps": 12, "loop": True, "events": []}],
                                  "transitions": [], "defaultStateName": "Idle"}, "facesLeft": False},
        "healthBar": {"show": False},
        "abilities": [anchor([OFFSET[0], OFFSET[1] - 0.55], lift["drift"], lift["drift_after"])]})
    # the diamond he takes on Floor 906: his beam marks it, then strikes it
    d = load("content/entities/diamond.json")
    for a in d["abilities"]:
        if a["type"] == "lifetime":
            a["seconds"] = 1.9
    d["onSpawn"] = [{"type": "spawn", "entity": "overlordBeamGlow", "anchor": "Self", "at": [0.0, 0.0], "facePlayer": False}]
    d["onDeath"] = [{"type": "spawn", "entity": "overlordBeam", "anchor": "Self", "at": [0.0, 0.0], "facePlayer": False},
                    {"type": "spawn", "entity": "overlordBeamImpact", "anchor": "Self", "at": [0.0, 0.0], "facePlayer": False},
                    {"type": "playSound", "sound": "overlord_beam_strike"},
                    {"type": "cameraShake", "intensity": 0.15, "duration": 0.4}]
    save("content/entities/overlordBaitDiamond.json", d)


# ─── scenes ────────────────────────────────────────────────────────────────────────────────────────────

def wait(s):
    return {"type": "wait", "seconds": round(s, 2)}


def say(text, dur, sound, color=None):
    a = {"type": "showText", "text": text, "duration": dur, "style": "Subtitle", "sound": sound}
    if color:
        a["color"] = color
    return a


def emit(sc, what):
    return {"type": "emit", "signal": f"ovl:{sc['floor']}:{what}"}


def interaction(sc):
    """The steps between his line and the hero's reply: what makes each scene different."""
    k = sc["kind"]
    f = sc["floor"]
    if k == "taunt":
        return [wait(1.2), emit(sc, "taunt")]
    if k == "drop":
        return [{"type": "spawn", "entity": "mkWarnHand", "anchor": "Player", "at": [3.0, 0.2], "facePlayer": False},
                emit(sc, "warn"), wait(1.1),
                {"type": "spawn", "entity": "slag", "anchor": "Player", "at": [3.0, 3.6], "velocity": [0.0, -3.2], "facePlayer": False},
                emit(sc, "drop"), wait(1.8)]
    if k == "lift":
        return [wait(sc["drift_after"] - sc["ovl"][2] - 1.0 + 0.6), emit(sc, "lift"), wait(2.4)]
    if k == "laser":
        return [{"type": "playSound", "sound": "overlord_beam_charge"},
                {"type": "spawn", "entity": "overlordBeamGlow", "anchor": "Player", "at": [5.5, -0.4], "facePlayer": False},
                emit(sc, "warn"), wait(1.25),
                {"type": "flash", "color": "#FF5A3C", "duration": 0.3},
                {"type": "cameraShake", "intensity": 0.25, "duration": 0.7},
                {"type": "spawn", "entity": "overlordBeam", "anchor": "Player", "at": [5.5, -0.4], "facePlayer": False},
                {"type": "spawn", "entity": "overlordBeamImpact", "anchor": "Player", "at": [5.5, -0.4], "facePlayer": False},
                {"type": "playSound", "sound": "overlord_beam_strike"},
                emit(sc, "laser"), wait(1.2)]
    if k == "steal":
        return [{"type": "spawn", "entity": "overlordBaitDiamond", "anchor": "Player", "at": [3.5, 0.7], "facePlayer": False},
                emit(sc, "bait"), wait(2.2), emit(sc, "stolen")]
    if k == "rewind":
        return [wait(0.4), say(*sc["hero"][:1], sc["hero"][2], sid_hero(sc)), emit(sc, "walk"), wait(sc["hero"][2] + 4.0)]
    if k == "summon":
        return [{"type": "spawnWave", "entity": "demonWhelp", "count": 2, "interval": 0.7, "anchor": "Camera", "areaCenter": [1.5, 2.4], "areaSize": [4.0, 0.6]},
                emit(sc, "summon"), wait(1.2)]
    if k == "drop-diamond":
        return [wait(1.4),
                {"type": "spawn", "entity": "diamond", "anchor": "Player", "at": [3.5, 0.6], "facePlayer": False},
                {"type": "spawn", "entity": "diamondGlint", "anchor": "Player", "at": [3.5, 0.6], "facePlayer": False},
                emit(sc, "drop"), wait(1.4)]
    raise ValueError(k)


def build_scene(sc):
    f = sc["floor"]
    steps = [wait(1.0),
             {"type": "spawn", "entity": f"overlordFar{f}", "id": "ovl", "anchor": "Camera", "at": sc["start"], "facePlayer": False}]
    if sc["kind"] == "lift":
        steps.insert(1, {"type": "spawn", "entity": "overlordLift958", "id": "ovlLift", "anchor": "Camera", "at": [OFFSET[0], OFFSET[1] - 0.55], "facePlayer": False})
    steps += [{"type": "setCounter", "name": "ovlOn", "value": 1}, emit(sc, "enter"), wait(1.0),
              say(sc["ovl"][0], sc["ovl"][2], sid_ovl(sc), RED), emit(sc, "line"), wait(sc["ovl"][2] + 0.2)]
    steps += interaction(sc)
    if sc["kind"] != "rewind":
        steps.append(say(sc["hero"][0], sc["hero"][2], sid_hero(sc)))
        steps.append(wait(sc["hero"][2] + 0.4))
    steps += [{"type": "despawn", "target": {"id": "ovl"}}]
    if sc["kind"] == "lift":
        steps.append({"type": "despawn", "target": {"id": "ovlLift"}})
    steps += [{"type": "setCounter", "name": "ovlOn", "value": 0}, emit(sc, "done")]
    rules = [{"name": f"{PREFIX} {sc['kind']}, Floor {f}", "when": {"type": "sectionReached", "section": sc["section"]}, "maxTimes": 1,
              "do": [{"type": "runSequence", "steps": steps}]}]
    if sc["kind"] == "rewind":
        # the hero's rewind in the middle of the scene: it drags him back along his ledge, and he sees it
        rules.append({"name": f"{PREFIX} caught in the loop, Floor {f}",
                      "when": {"type": "allOf", "triggers": [{"type": "counter", "name": "ovlOn", "atLeast": 1.0}, {"type": "heroRewound", "within": 2.5}]},
                      "maxTimes": 3,
                      "do": [{"type": "runSequence", "steps": [
                          wait(0.4), say(sc["again"][0], sc["again"][2], sid_ovl(sc, "again"), RED), emit(sc, "rewound"), wait(sc["again"][2] + 0.2),
                          say(sc["hero2"][0], sc["hero2"][2], sid_hero(sc, True)), wait(sc["hero2"][2])]}]})
    return rules


def build_floor(sc):
    name = f"Floor{sc['floor']}"
    lv = load(f"levels/{name}.json")
    s = lv["script"]
    sections = [p["name"] for p in s["world"].get("path", [])]
    if sc["section"] not in sections:
        raise SystemExit(f"{name} has no section {sc['section']}")
    s["rules"] = [r for r in s["rules"] if not r["name"].startswith(PREFIX)] + build_scene(sc)
    if not any(c["name"] == "ovlOn" for c in s["counters"]):
        s["counters"].append({"name": "ovlOn", "value": 0.0})
    save(f"levels/{name}.json", lv)


# ─── audio.json ────────────────────────────────────────────────────────────────────────────────────────

def voices():
    for sc in SCENES:
        yield sid_ovl(sc), "overlord", OVERLORD, 0.85, sc["ovl"][1]
        yield sid_hero(sc), "player", HERO, 1, sc["hero"][1]
        if sc["kind"] == "rewind":
            yield sid_ovl(sc, "again"), "overlord", OVERLORD, 0.85, sc["again"][1]
            yield sid_hero(sc, True), "player", HERO, 1, sc["hero2"][1]


def build_audio():
    a = load("audio.json")
    ids = {s["id"]: i for i, s in enumerate(a["sounds"])}
    for sid, who, vid, speed, prompt in voices():
        entry = {"id": sid, "kind": "Voice", "prompt": prompt, "durationSeconds": 0, "promptInfluence": 0.3, "loop": False, "voice": who, "voiceId": "",
                 "clip": f"Audio/Generated/{sid}.mp3", "generatedFrom": f"voice|{vid}|eleven_v3|0.5|{speed}|{prompt}", "volume": 1, "pitchVariation": 0,
                 "maxVoices": 1, "cooldown": 0.5}
        if sid in ids:
            a["sounds"][ids[sid]] = entry
        else:
            a["sounds"].append(entry)
    save("audio.json", a)


# ─── art (spec only; GPU priced in the summary) ────────────────────────────────────────────────────────

def build_art():
    save("art/overlordFar.json", {
        "schemaVersion": 1, "entity": "overlordFar", "name": "The Overlord (far)",
        "description": "a giant obsidian titan seen from far away on a ledge, red glowing eyes, a crown of black horns, broad shoulders in a heavy cloak of black stone, "
                       "his back and one shoulder to the viewer, tired posture, no expression; a dark silhouette lit only by lava glow from below",
        "height": 1.6,
        "poses": {"stand": "standing on a ledge in profile looking up, one hand resting on a horn of his crown",
                  "walk": "mid-stride in profile, heavy sack over one shoulder",
                  "lift": "standing on a small chain lift platform, back to the viewer, looking up"},
        "clips": {"idle": {"from": "stand", "action": "breathes slowly, shoulders rising and falling, eyes glowing", "seconds": 3},
                  "walk": {"from": "walk", "action": "walks slowly along the ledge, unhurried", "seconds": 2.5}},
        "takes": {"pose": 3, "clip": 3}})


# ─── run ───────────────────────────────────────────────────────────────────────────────────────────────

build_entities()
for sc in SCENES:
    build_floor(sc)
build_audio()
build_art()

problems = []
if len({sc["floor"] for sc in SCENES}) < 8:
    problems.append("fewer than eight floors")
if len({sc["kind"] for sc in SCENES}) < 8:
    problems.append("two scenes share an interaction")
for sc in SCENES:
    if not (905 <= sc["floor"] <= 1000 or 801 <= sc["floor"] <= 900):
        problems.append(f"Floor {sc['floor']} is not in Circle X or IX")
if problems:
    print("problems:", "; ".join(problems))
    sys.exit(1)
if check_only and changed:
    print("out of date:", ", ".join(changed))
    sys.exit(1)
print(("would change: " if check_only else "wrote: ") + (", ".join(changed) if changed else "nothing"))
