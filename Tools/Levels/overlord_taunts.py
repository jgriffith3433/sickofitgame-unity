#!/usr/bin/env python3
"""
The Overlord's taunts on Floors 1000-997 (Docs/Story.md "The Overlord watches the climb"): one animated figure and 30 voiced lines, keyed to what the hero just did.

Writes (idempotent): the 30 `vo_ovl_t_*` voice lines in audio.json (voice `overlord`, the existing canon voice; clips are made through Hawkes Gen, ids without a
clip sit in Tools/Audio/pending_clips.json), Assets/Scripts/Gameplay/Presentation/OverlordTauntLines.cs (the table the director reads), the entities
`overlordTaunt` (four clips baked by Tools/Art/overlord_taunt_bake.py) and `overlordTauntFlee` (a copy of its animator that drifts up and out),
the art spec art/overlordTaunt.json and, on each of the four floors, one rule that names the entities so the level's bundle carries them.

    python3 Tools/Levels/overlord_taunts.py          # write
    python3 Tools/Levels/overlord_taunts.py --check  # fail if the files differ from what the table makes
"""
import json, os, sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CFG = os.path.join(ROOT, "Assets", "GameConfig")
check_only = "--check" in sys.argv
changed = []

# key -> list of (id suffix, subtitle, delivery tags). One line is spoken once per session. 4+12+4+4+3+3 = 30.
LINES = {
    "enter1000": [("bottom", "Mm. ... The bottom. Cozy, isn't it?", "[bored, velvety]")],
    "enter999": [("rising", "Mm. ... Rising, are we?", "[bored, amused]")],
    "enter998": [("village", "Mm. ... Your village is floating by.", "[bored, theatrical]")],
    "enter997": [("higher", "Mm. ... Higher. How tiresome.", "[bored, sighing]")],
    "deathImp": [("imp1", "Mm. ... An imp. Really?", "[bored]"), ("imp2", "Mm. ... My staff thanks you.", "[bored, amused]"), ("imp3", "Mm. ... Even the small ones.", "[bored]")],
    "deathBullet": [("bullet1", "Mm. ... You walked into that.", "[bored]"), ("bullet2", "Mm. ... It was slow, too.", "[bored, amused]"), ("bullet3", "Mm. ... Dodging is optional, I see.", "[bored, theatrical]")],
    "deathHazard": [("hazard1", "Mm. ... The floor is lava. Still.", "[bored]"), ("hazard2", "Mm. ... Hot, isn't it?", "[bored, amused]"), ("hazard3", "Mm. ... Hell does the work for me.", "[bored, velvety]")],
    "deathFall": [("fall1", "Mm. ... Down is that way.", "[bored]"), ("fall2", "Mm. ... Gravity sends its regards.", "[bored, amused]"), ("fall3", "Mm. ... The long way down, then.", "[bored]")],
    "rewind": [("rew1", "Mm. ... Again with the clock.", "[bored]"), ("rew2", "Mm. ... I remember that trick.", "[bored, quietly]"), ("rew3", "Mm. ... Backward. Of course.", "[bored]"), ("rew4", "Mm. ... Careful. It costs you.", "[bored, velvety]")],
    "undo": [("undo1", "Mm. ... Dead, then not. Tiresome.", "[bored, sighing]"), ("undo2", "Mm. ... You cannot undo me.", "[bored, velvety]"), ("undo3", "Mm. ... Fine. Die again.", "[bored]"), ("undo4", "Mm. ... I was nearly rid of you.", "[long, bored exhale]")],
    "streak": [("streak1", "Mm. ... Thirty seconds. Careful.", "[bored, amused]"), ("streak2", "Mm. ... Untouched. It won't last.", "[bored, velvety]"), ("streak3", "Mm. ... Pride comes before lava.", "[bored, theatrical]")],
    "finale": [("fin1", "Mm. ... Now it gets loud.", "[bored, amused]"), ("fin2", "Mm. ... Last stretch. Don't trip.", "[bored]"), ("fin3", "Mm. ... Let me help.", "[bored, velvety]")],
}
FLOORS = {1000: "Floor1000", 999: "Floor999", 998: "Floor998", 997: "Floor997"}
assert sum(len(v) for v in LINES.values()) == 30


def sid(suffix):
    return f"vo_ovl_t_{suffix}"


def jload(path, default=None):
    full = os.path.join(ROOT, path)
    if not os.path.exists(full):
        return default
    with open(full, encoding="utf-8") as f:
        return json.load(f)


def jsave(path, data, keep_existing=False):
    full = os.path.join(ROOT, path)
    if keep_existing and os.path.exists(full):
        return
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    if os.path.exists(full):
        with open(full, encoding="utf-8") as f:
            if f.read() == text:
                return
    changed.append(path)
    if not check_only:
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


def tsave(path, text):
    full = os.path.join(ROOT, path)
    if os.path.exists(full):
        with open(full, encoding="utf-8") as f:
            if f.read() == text:
                return
    changed.append(path)
    if not check_only:
        with open(full, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


def audio():
    path = "Assets/GameConfig/audio.json"
    d = jload(path)
    have = {s["id"]: s for s in d["sounds"]}
    voice = next(v for v in d["voices"] if v["name"] == "overlord")
    pending = []
    for key, rows in LINES.items():
        for suffix, text, tags in rows:
            i = sid(suffix)
            clip = f"Audio/Generated/{i}.mp3"
            made = os.path.exists(os.path.join(ROOT, "Assets", clip)) or (have.get(i, {}).get("clip") == clip and have[i].get("generatedFrom"))
            entry = have.get(i) or {"id": i, "kind": "Voice", "durationSeconds": 0, "promptInfluence": 0.3, "loop": False, "voice": "overlord", "voiceId": "",
                                    "clip": "", "volume": 1, "pitchVariation": 0, "maxVoices": 1, "cooldown": 0.5}
            entry["prompt"] = f"{tags} Mm. [long pause] {text[len('Mm. ... '):]}" if text.startswith("Mm. ... ") else f"{tags} {text}"
            if not made:
                entry["clip"] = ""
                pending.append(i)
            if i not in have:
                d["sounds"].append(entry)
                have[i] = entry
    jsave(path, d)
    pend = jload("Tools/Audio/pending_clips.json", {"comment": "", "ids": []})
    pend["ids"] = sorted(set(pend["ids"]) | set(pending))
    jsave("Tools/Audio/pending_clips.json", pend)


def lines_cs():
    out = ["// <auto-generated> by Tools/Levels/overlord_taunts.py: edit its table, never this file. </auto-generated>",
           "namespace SickOfIt.Gameplay", "{", "    /// <summary>The Overlord's 30 taunts on Floors 1000-997 (key to lines; the subtitle is the sound's prompt without its tags).</summary>",
           "    public static class OverlordTauntLines", "    {",
           "        public static readonly System.Collections.Generic.Dictionary<string, string[]> Table = new System.Collections.Generic.Dictionary<string, string[]>", "        {"]
    for key, rows in LINES.items():
        ids = ", ".join(f'"{sid(r[0])}"' for r in rows)
        out.append(f'            {{ "{key}", new[] {{ {ids} }} }},')
    out += ["        };", "    }", "}", ""]
    tsave("Assets/Scripts/Gameplay/Presentation/OverlordTauntLines.cs", "\n".join(out))


def entities():
    anchor = lambda drift, after: {"type": "screenAnchor", "offset": [0.7, 1.7], "drift": drift, "driftAfter": after}
    base = lambda: {"schemaVersion": 1, "team": "Neutral", "health": 0, "shootable": False, "tags": ["overlordCameo", "overlordTaunt"],
                    "body": {"radius": 0.3, "solid": False},
                    "visual": {"scale": 1.0, "placeholderColor": "#2A2030", "placeholderSize": 1.8, "placeholderLabel": False, "layer": "Background2",
                               "sortingOrder": 3, "hurtFlash": False, "deathPop": False, "facesLeft": False},
                    "healthBar": {"show": False}}
    # visual.animator (Idle, Laugh, Point, Flee) is written once by hand from Tools/Art/overlord_taunt_bake.py's sheets; keep what is there.
    # Flee: the 8-frame turn plays, then he rises out of the top at 4 units/s (a 390x844 view is ~16 units tall).
    cur = jload("Assets/GameConfig/content/entities/overlordTaunt.json")
    t = base()
    t["abilities"] = [anchor([0.0, 0.0], 0.0), {"type": "lifetime", "seconds": 4.0}]
    if cur:
        t["visual"] = cur["visual"]
    jsave("Assets/GameConfig/content/entities/overlordTaunt.json", t)
    f = base()
    f["abilities"] = [anchor([0.0, 4.0], 0.6), {"type": "lifetime", "seconds": 2.6}]
    f["visual"] = json.loads(json.dumps(t["visual"]))
    anim = f["visual"].get("animator")
    if anim:
        anim["defaultStateName"] = "Flee"
    jsave("Assets/GameConfig/content/entities/overlordTauntFlee.json", f)


def art():
    spec = {"schemaVersion": 1, "entity": "overlordTaunt", "name": "The Overlord, watching",
            "description": "the Overlord of Hell: a giant figure of black obsidian with a crown of long black horns, burning red eyes, broad shoulders under a heavy cloak of black stone, tired and velvety, lit by lava glow from below",
            "height": 1.8, "pixels": 300,
            "poses": {"stand": "standing in profile facing left, cloak hanging, one hand on a horn of his crown, looking down",
                      "laugh": "head thrown back laughing, shoulders raised, one hand on his chest",
                      "point": "one long arm stretched out pointing down-left, the other hand open"},
            "clips": {"idle": {"from": "stand", "action": "hovers, shoulders rising and falling slowly, red eyes glowing, the cloak stirring", "seconds": 2.5},
                      "laugh": {"from": "laugh", "loop": True, "action": "laughs silently, shoulders shaking", "seconds": 2.5},
                      "point": {"from": "point", "loop": True, "action": "points down at the hero and flicks his hand to throw", "seconds": 2.5},
                      "flee": {"from": "stand", "action": "turns his back and rises upward out of the top of the picture, cloak trailing", "seconds": 2.5}},
            "animator": {"parameters": [], "transitions": []}, "prefix": "OverlordTaunt", "takes": {"stand": 3, "pose": 2, "clip": 1}}
    jsave("Assets/GameConfig/art/overlordTaunt.json", spec, keep_existing=True)


def floors():
    for floor, name in FLOORS.items():
        path = f"Assets/GameConfig/levels/{name}.json"
        d = jload(path)
        rules = d["script"]["rules"]
        rules[:] = [r for r in rules if r.get("name") != f"overlord: watches, Floor {floor}"]
        rules.append({"name": f"overlord: watches, Floor {floor}", "when": {"type": "counter", "name": "ovlTauntNever", "atLeast": 999.0}, "maxTimes": 1,
                      "do": [{"type": "spawn", "entity": "overlordTaunt", "id": "ovlTaunt", "anchor": "Camera", "at": [0.7, 1.7], "facePlayer": False},
                             {"type": "spawn", "entity": "overlordTauntFlee", "id": "ovlTauntFlee", "anchor": "Camera", "at": [0.7, 1.7], "facePlayer": False}]})
        jsave(path, d)


audio(); lines_cs(); entities(); art(); floors()
if check_only and changed:
    print("out of date:", *changed, sep="\n  ")
    sys.exit(1)
print("changed:", *changed, sep="\n  ") if changed else print("up to date")
