#!/usr/bin/env python3
"""
The Climbers (Docs/Story.md "The Climbers"): Rook the rival, Wick (Circle X's guide) and Pointer (Circle IX's guide), fourteen scenes on Circle X's and
Circle IX's floors. Each scene is one rule in the floor's own script (a sequence: the climber appears ahead, speaks, BRANCHES on the hero with the `heroState`
trigger, pays a small reward once, writes a flag the later scenes read, walks on).

Writes: the three climber entities, one rule per scene in the floors, Tools/Audio/companion_voices.json (voice recipes, priced, not made: the lines are subtitles
until the clips exist) and the art specs. Idempotent. Run it after long_floors.py / cracked_floors.py / elites.py / bystanders.py / overlord_cameos.py.

    python3 Tools/Levels/companions.py          # write
    python3 Tools/Levels/companions.py --check  # fail if the files differ from what the table makes
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from artkeep import guard  # noqa: E402  (generated art is kept: Tools/Levels/artkeep.py)

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CFG = os.path.join(ROOT, "Assets", "GameConfig")
check_only = "--check" in sys.argv
changed = []
PREFIX = "companion:"


def load(path, base=CFG):
    with open(os.path.join(base, path), encoding="utf-8") as f:
        return json.load(f)


def save(path, data, base=CFG):
    full = os.path.join(base, path)
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


# ─── who ───────────────────────────────────────────────────────────────────────────────────────────────
WHO = {
    "rook": dict(name="Rook", color="#F2A93B", scale=1.15,
                 voice="Bright, boastful, a little too loud: a game-show host who is not winning. Cracks on the number two. Close-mic studio recording, American accent.",
                 look="a tall lean soul in a patched mountaineer's coat and a scarf that burns without burning down, one glove, a coil of rope that goes nowhere, a medal on a ribbon around his neck that says 2, always mid-stretch",
                 height=1.9),
    "wick": dict(name="Wick", color="#BFE6C8", scale=0.8,
                 voice="A soft wheeze, very kind, in no hurry; every sentence sounds like a match that might not catch. Hums between lines. Close-mic studio recording.",
                 look="a small bent soul with a candle for a head that is always lit and never shorter, holding a long pole with a wick on the end, pale green flame, a worn hood",
                 height=1.1),
    "pointer": dict(name="Pointer", color="#C9A27A", scale=1.2,
                    voice="Gruff, flat, as few words as possible, directions only; the only soul in Hell annoyed at being right. Close-mic studio recording.",
                    look="a soul fused to a road sign: planks nailed to a body, one arm a long signpost arm held straight out with a hand at the end, a name board on his chest that reads UP, dusty tan wood",
                    height=1.8),
}

VOICE_IDS = {"rook": "tyqG7NyoJ3HhzdkhAsW7", "wick": "nXyPsMINc4YNXaqnmYmR", "pointer": "2y1BMYkEPUuMF8p3lf6w"}  # designed with audio.voice-design, saved with audio.voice-save
HERO_VOICE_ID = "DzkD7I4tf5i3KSaB4cz5"  # audio.json voice `player`
SILENT = {"..."}  # a beat of silence: subtitle only


def slug(text):
    return re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")


# ─── the table ─────────────────────────────────────────────────────────────────────────────────────────
# Line: ("c", text)  the climber says it;  ("h", text)  the hero answers.
# Reward: None, "diamond", "diamond2", "diamond3", "orb", "orb2", "heart", "heart+diamond2", "diamond3+orb"
# Branch: (label, condition (heroState fields), lines, reward); the last branch has condition None (the default). Order = priority.
# `intro` runs first, `outro` runs after the branch's own lines; `flag` is written once the branch is chosen.
H = lambda t: ("h", t)
SCENES = [
    # ── Rook, the rival ──
    dict(who="rook", floor=996, section="foot", flag="rook.met",
         intro=[("c", "First! ...Oh. You're a person."), H("I'm just going up."), ("c", "Going up. Racing. Same thing.")],
         branches=[("rich", dict(diamondsAtLeast=10), [("c", "Look at the pockets on you. Cheating."), H("It's called picking things up.")], None),
                   ("poor", None, [("c", "Slow and poor. I like it. For the effort."), H("Thanks. I think.")], "diamond")]),
    dict(who="rook", floor=992, section="wall", flag="rook.dared",
         intro=[("c", "Race you to the bridge. Go!"), H("No."), ("c", "Too late. I'm winning.")],
         branches=[("hurt", dict(heartsAtMost=1), [("c", "You're one hit from the floor. Even I won't race that. Take the ember off my scarf."), H("Appreciated.")], "heart"),
                   ("race", None, [("c", "Third floor in a row I'm second! ...Is the bridge supposed to do that?"), H("Yes.")], "diamond")]),
    dict(who="rook", floor=955, section="falls", flag="rook.asked",
         intro=[("c", "Psst. How does the R thing work? Asking for a friend."), H("Who's the friend?"), ("c", "...Me.")],
         branches=[("dead", dict(deathsAtLeast=3), [("c", "You've died more than I have, and you're still ahead?"), H("Hell's weird.")], "orb"),
                   ("grudges", dict(grudgesAtLeast=2), [("c", "You collect those Grudges like medals."), H("They collect me.")], "orb"),
                   ("plain", None, [("c", "Show me. Slowly."), H("Hold R."), ("c", "I'm holding it."), H("It's not on you.")], "orb")],
         outro=[("c", "Found this. It's yours.")]),
    dict(who="rook", floor=899, section="wall", flag="rook.sideways",
         intro=[("c", "I tried the R thing. ...It doesn't go backward for me. It goes sideways."), H("That's not how it works.")],
         branches=[("asked", dict(requires=["flag:rook.asked"]), [("c", "You said hold R. I held R. It's the annoyance, isn't it. You have to bring your own."), H("Yeah.")], "diamond2"),
                   ("skipped", None, [("c", "I heard there's a button. Where's the button?"), H("R."), ("c", "R. ...R!")], "diamond")]),
    dict(who="rook", floor=858, section="foot", flag="rook.second",
         intro=[("c", "I'm first! I'm first! ...The road tipped. You're first."), H("It tips.")],
         branches=[("hurt", dict(heartsAtMost=1), [("c", "You're hurt. I'll say where it tips. Don't make it a thing."), H("Noted.")], "heart"),
                   ("rich", dict(diamondsAtLeast=30), [("c", "Spend those. First place is heavy to carry."), H("It's diamonds."), ("c", "It's heavy!")], "orb"),
                   ("plain", None, [("c", "Second. ...Second. It's a number, at least."), H("It's a good number.")], "orb")]),
    dict(who="rook", floor=808, section="foot", flag="rook.done",
         intro=[("c", "Don't look. I'm having a moment."), H("You okay?"), ("c", "I don't need to be first. ...I need to see you be.")],
         branches=[("kind", dict(requires=["flag:rook.second"]), [("c", "You said second's a good number. It's the best one. You get to watch.")], "diamond3+orb"),
                   ("dead", dict(deathsAtLeast=5), [("c", "Die less. It's embarrassing for both of us.")], "diamond3+orb"),
                   ("plain", None, [("c", "Don't die. I'd have to carry on.")], "diamond3+orb")],
         outro=[("c", "Take the rope. It goes where you go."), H("It goes nowhere."), ("c", "That's where I was going.")]),
    # ── Wick, Circle X's guide ──
    dict(who="wick", floor=999, section="ledges", flag="wick.met",
         intro=[("c", "Oh. Someone. ...Mind the dark. I'll light it.")],
         branches=[("hurt", dict(heartsAtMost=1), [("c", "You're running on a wick. Here. Stand by the lamp."), H("Thanks.")], "heart"),
                   ("plain", None, [("c", "The lava rises here. Don't stand still."), H("I wasn't going to.")], "orb")]),
    dict(who="wick", floor=993, section="ledges", flag="wick.lit",
         intro=[("c", "They throw things here. ...Don't catch them."), H("Wasn't planning to.")],
         branches=[("dead", dict(deathsAtLeast=3), [("c", "You've been up here before. ...So have I.")], "orb2"),
                   ("rook", dict(requires=["flag:rook.met"]), [("c", "A loud one went by. He says he's winning."), H("He's second."), ("c", "He said that too.")], "orb"),
                   ("plain", None, [("c", "Light's on. Go quick.")], "orb")]),
    dict(who="wick", floor=960, section="stones", flag=None,
         intro=[("c", "It keeps going out. ...I'm not crying, it's the smoke.")],
         branches=[("rich", dict(diamondsAtLeast=20), [("c", "Could you spare a diamond for the oil? ...No. Keep it. I'll owe you.")], "orb2"),
                   ("grudges", dict(grudgesAtLeast=2), [("c", "Hold on to those Grudges. They get heavy.")], "orb"),
                   ("plain", None, [("c", "Try the left stone. It holds."), H("Thanks.")], "orb")]),
    dict(who="wick", floor=902, section="stairs", flag="wick.rested",
         intro=[("c", "The last lamp. ...I can stop now. Can I stop now?"), H("Stop. Sit.")],
         branches=[("hurt", dict(heartsAtMost=1), [("c", "Oh, you're hurt. One more match, then.")], "heart+diamond2"),
                   ("friends", dict(requires=["flag:wick.lit", "flag:rook.asked"]), [("c", "You talk to everyone, don't you."), H("They talk to me.")], "diamond3"),
                   ("plain", None, [("c", "Go quiet. He hears everything.")], "diamond3")],
         outro=[("c", "It stayed lit. ...It stayed lit.")]),
    # ── Pointer, Circle IX's guide ──
    dict(who="pointer", floor=900, section="rise", flag="pointer.met",
         intro=[("c", "Left."), H("You're a sign."), ("c", "I'm a person. ...Your other left.")],
         branches=[("hurt", dict(heartsAtMost=1), [("c", "Up. Fast. Please.")], "orb"),
                   ("plain", None, [("c", "There."), H("Thanks.")], "diamond")]),
    dict(who="pointer", floor=895, section="road", flag="pointer.right",
         intro=[("c", "Up."), ("c", "Not that up."), H("Which up?"), ("c", "The one that holds.")],
         branches=[("dead", dict(deathsAtLeast=2), [("c", "I've pointed you here before.")], "orb"),
                   ("rook", dict(requires=["flag:rook.sideways"]), [("c", "A man went sideways. I don't recommend it.")], "orb"),
                   ("plain", None, [("c", "That one.")], "orb")]),
    dict(who="pointer", floor=860, section="crack", flag=None,
         intro=[("c", "Don't look at the statue."), H("Why?"), ("c", "It looks back.")],
         branches=[("grudges", dict(grudgesAtLeast=2), [("c", "You carry a lot of promises.")], "diamond2"),
                   ("wick", dict(requires=["flag:wick.rested"]), [("c", "Small glowing one sent regards."), H("He stopped?"), ("c", "Sat down.")], "diamond2"),
                   ("plain", None, [("c", "Keep moving.")], "diamond")]),
    dict(who="pointer", floor=810, section="stair", flag="pointer.up",
         intro=[("c", "..."), ("c", "That's all I know. ...Up."), H("Up's enough.")],
         branches=[("hurt", dict(heartsAtMost=1), [("c", "Sit. A heart is on the house. ...I'm a sign. It's the house.")], "heart+diamond2"),
                   ("both", dict(requires=["flag:pointer.right", "flag:rook.second"]), [("c", "I was right about all of you.")], "diamond2"),
                   ("plain", None, [("c", "Don't thank me. I'm a sign.")], "diamond2")]),
]
OFFSET = [6.0, 0.35]  # ahead of where he starts, as the bystanders stand


def sid(sc):
    return f"{sc['who']}{sc['floor']}"


def secs(text):
    return round(max(1.8, 1.0 + 0.065 * len(text)), 1)


# ─── actions ───────────────────────────────────────────────────────────────────────────────────────────

def wait(s):
    return {"type": "wait", "seconds": round(s, 2)}


def emit(sc, what):
    return {"type": "emit", "signal": f"cmp:{sc['who']}:{sc['floor']}:{what}"}


def talk(sc, who_key, text, tag=None):
    if who_key == "h":
        show = {"type": "showText", "text": text, "duration": secs(text), "style": "Subtitle"}
        if text not in SILENT:
            show["sound"] = f"vo_hero_cmp_{slug(text)}"
        return [show, wait(secs(text) + 0.3)]
    w = WHO[sc["who"]]
    line = f"{w['name']}: {text}"
    show = {"type": "showText", "text": line, "duration": secs(line), "style": "Subtitle", "color": w["color"]}
    if text not in SILENT:
        show["sound"] = f"vo_{sid(sc)}_{tag}"
    return [show, wait(secs(line) + 0.3)]


def spawn(entity, at):
    return {"type": "spawn", "entity": entity, "anchor": "Player", "at": at, "facePlayer": False}


def reward(code, sc):
    steps = []
    if not code:
        return steps
    for part in code.split("+"):
        if part.startswith("diamond"):
            n = int(part[7:] or 1)
            for i in range(n):
                steps.append(spawn("diamond", [3.0 + 0.6 * i, 0.9]))
        elif part.startswith("orb"):
            n = int(part[3:] or 1)
            for i in range(n):
                steps.append(spawn("rewindOrb", [3.0 + 0.6 * i, 1.3]))
        elif part == "heart":
            steps.append({"type": "heal", "target": {"player": True}, "amount": 1})
            steps.append({"type": "popText", "text": "+1 heart", "color": [1.0, 0.45, 0.5, 1.0], "at": [0.0, 1.2], "anchor": "Player"})
        else:
            raise ValueError(code)
    steps.append(emit(sc, "reward:" + code))
    return steps


def branch_seq(sc, label, lines, rw):
    steps = [emit(sc, "branch:" + label)]
    for i, (k, t) in enumerate(lines):
        steps += talk(sc, k, t, f"{label}{i}")
    for i, (k, t) in enumerate(sc.get("outro", [])):
        steps += talk(sc, k, t, f"outro{i}")
    steps += reward(rw, sc)
    if sc.get("flag"):
        steps.append({"type": "setFlag", "flag": sc["flag"]})
    # "done" goes out as soon as the reward and flag are paid: the walk away is only dressing, and a floor that ends
    # or kills the hero in its last seconds must not leave a finished scene looking unfinished.
    steps += [emit(sc, "done"),
              {"type": "walkEntityTo", "target": "cmp", "x": 14.0, "relativeTo": "player", "speed": 3.0},
              wait(3.0), {"type": "despawn", "target": {"id": "cmp"}}]
    return {"type": "runSequence", "steps": steps}


def chain(sc, branches):
    """if / else-if / else, each arm starting the branch's own sequence (a wait only works at the top of a sequence)."""
    label, cond, lines, rw = branches[0]
    seq = branch_seq(sc, label, lines, rw)
    if cond is None:
        return seq
    return {"type": "if", "condition": dict(type="heroState", **cond), "then": [seq], "else": [chain(sc, branches[1:])]}


def build_scene(sc):
    steps = [wait(1.0), spawn_climber(sc), emit(sc, "enter"), wait(1.0)]
    for i, (k, t) in enumerate(sc["intro"]):
        steps += talk(sc, k, t, f"intro{i}")
    steps.append(chain(sc, sc["branches"]))
    return [{"name": f"{PREFIX} {sc['who']}, Floor {sc['floor']}", "when": {"type": "sectionReached", "section": sc["section"]}, "maxTimes": 1,
             "do": [{"type": "runSequence", "steps": steps}]}]


def spawn_climber(sc):
    return {"type": "spawn", "entity": sc["who"], "id": "cmp", "anchor": "Player", "at": OFFSET, "facePlayer": False}


def build_floor(floor, scenes):
    name = f"Floor{floor}"
    lv = load(f"levels/{name}.json")
    s = lv["script"]
    sections = [p["name"] for p in s["world"].get("path", [])]
    rules = []
    for sc in scenes:
        if sc["section"] not in sections:
            raise SystemExit(f"{name} has no section {sc['section']}")
        rules += build_scene(sc)
    s["rules"] = [r for r in s["rules"] if not r["name"].startswith(PREFIX)] + rules
    save(f"levels/{name}.json", lv)


def build_entities():
    for key, w in WHO.items():
        save(f"content/entities/{key}.json", {
            "schemaVersion": 1, "extends": "damnedSoul", "tags": ["damned", "companion", key],
            "visual": {"scale": w["scale"], "color": w["color"], "placeholderColor": w["color"], "placeholderSize": 0.7 * w["scale"], "placeholderLabel": True}})


def build_art():
    for key, w in WHO.items():
        save(f"art/{key}.json", {
            "schemaVersion": 1, "entity": key, "name": w["name"], "description": w["look"], "height": w["height"],
            "poses": {"stand": "standing in profile facing right, relaxed", "talk": "in profile facing right, mid-sentence, one hand raised", "walk": "mid-stride in profile facing right", "react": "in profile facing right, startled, leaning back, both hands up"},
            "clips": {"idle": {"from": "stand", "action": "breathes slowly, shifts weight", "seconds": 3},
                      "talk": {"from": "talk", "action": "speaks with small gestures", "seconds": 2.5},
                      "walk": {"from": "walk", "action": "walks away unhurried", "seconds": 2.5},
                      "react": {"from": "react", "action": "flinches back, then settles and nods", "seconds": 1.5, "loop": False}},
            "takes": {"pose": 3, "clip": 3}})


def build_voices():
    lines = []
    for sc in SCENES:
        w = WHO[sc["who"]]
        all_lines = [(f"intro{i}", t) for i, (k, t) in enumerate(sc["intro"]) if k == "c"]
        for label, cond, bl, rw in sc["branches"]:
            all_lines += [(f"{label}{i}", t) for i, (k, t) in enumerate(bl) if k == "c"]
        all_lines += [(f"outro{i}", t) for i, (k, t) in enumerate(sc.get("outro", [])) if k == "c"]
        for tag, t in all_lines:
            lines.append({"id": f"vo_{sid(sc)}_{tag}", "voice": sc["who"], "subtitle": t, "prompt": t})
    heroes = []
    for sc in SCENES:
        for label, cond, bl, rw in sc["branches"]:
            pass
    save("Tools/Audio/companion_voices.json", {
        "note": "Voice recipes and lines of the Climbers (Docs/Story.md \"The Climbers\"). The voices are designed and saved in Hawkes Gen, the lines are audio.json sounds (written by build_audio below) with clips in Audio/Generated; the scenes play them through showText `sound`.",
        "voices": {k: {"name": w["name"], "description": w["voice"]} for k, w in WHO.items()},
        "lines": lines,
        "heroLines": sorted({t for sc in SCENES for k, t in sc["intro"] if k == "h"} | {t for sc in SCENES for b in sc["branches"] for k, t in b[2] if k == "h"} |
                            {t for sc in SCENES for k, t in sc.get("outro", []) if k == "h"})}, base=ROOT)


def sound_entry(sid_, prompt, voice, voice_id, speed):
    return {"id": sid_, "kind": "Voice", "prompt": prompt, "durationSeconds": 0, "promptInfluence": 0.3, "loop": False, "voice": voice, "voiceId": "",
            "clip": f"Audio/Generated/{sid_}.mp3", "generatedFrom": f"voice|{voice_id}|eleven_v3|0.5|{speed}|{prompt}",
            "volume": 1, "pitchVariation": 0, "maxVoices": 1, "cooldown": 0.5}


def build_audio():
    """The climbers' voices and every line they say (and the hero's answers) as audio.json sounds, clips made through Hawkes Gen."""
    audio = load("audio.json")
    have = {v["name"] for v in audio["voices"]}
    for key, w in WHO.items():
        if key not in have:
            audio["voices"].append({"name": key, "description": w["voice"], "sampleText": SAMPLE[key], "voiceId": VOICE_IDS[key], "model": "eleven_v3", "stability": 0.5, "speed": 1})
    wanted = []
    for ln in json.load(open(os.path.join(ROOT, "Tools/Audio/companion_voices.json"), encoding="utf-8"))["lines"]:
        if ln["prompt"] not in SILENT:
            wanted.append(sound_entry(ln["id"], ln["prompt"], ln["voice"], VOICE_IDS[ln["voice"]], 1))
    for t in sorted({t for sc in SCENES for k, t in sc["intro"] if k == "h"} | {t for sc in SCENES for b in sc["branches"] for k, t in b[2] if k == "h"} |
                    {t for sc in SCENES for k, t in sc.get("outro", []) if k == "h"}):
        wanted.append(sound_entry(f"vo_hero_cmp_{slug(t)}", "[deadpan] " + t, "player", HERO_VOICE_ID, 1))
    ids = {w["id"] for w in wanted}
    sounds = [x for x in audio["sounds"] if x["id"] not in ids] + wanted
    audio["sounds"] = sounds
    save("audio.json", audio)
    return ids


SAMPLE = {"rook": "First! ...Oh. You're a person. Going up. Racing. Same thing.",
          "wick": "Oh. Someone. ...Mind the dark. I'll light it.",
          "pointer": "Up. Not that up. The one that holds."}

# ─── run ───────────────────────────────────────────────────────────────────────────────────────────────
build_entities()
by_floor = {}
for sc in SCENES:
    by_floor.setdefault(sc["floor"], []).append(sc)
for floor, scs in by_floor.items():
    build_floor(floor, scs)
build_art()
build_voices()
build_audio()

problems = []
for who in WHO:
    mine = [sc for sc in SCENES if sc["who"] == who]
    if len({sc["floor"] for sc in mine}) < 3:
        problems.append(f"{who} appears on fewer than three floors")
    if not any(len(sc["branches"]) >= 2 for sc in mine):
        problems.append(f"{who} never branches")
for sc in SCENES:
    if sc["branches"][-1][1] is not None:
        problems.append(f"Floor {sc['floor']}: the last branch must be the default")
    if not (801 <= sc["floor"] <= 1000):
        problems.append(f"Floor {sc['floor']} is not in Circle X or IX")
    if sc["who"] == "pointer" and sc["floor"] > 900 or sc["who"] == "wick" and sc["floor"] < 901:
        problems.append(f"{sc['who']} is on the wrong Circle at Floor {sc['floor']}")
if problems:
    print("problems:", "; ".join(problems))
    sys.exit(1)
if check_only and changed:
    print("out of date:", ", ".join(changed))
    sys.exit(1)
print(("would change: " if check_only else "wrote: ") + (", ".join(changed) if changed else "nothing"))
