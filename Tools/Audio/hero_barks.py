#!/usr/bin/env python3
"""The hero's barks: the table of 45 lines (11 events), written into audio.json and Assets/Scripts/Gameplay/Presentation/HeroBarkLines.cs.

Docs/Story.md "The hero talks back". Edit TABLE here, never the generated parts:
  python3 Tools/Audio/hero_barks.py --write     rewrite the audio.json entries (voice `player`) and HeroBarkLines.cs
  python3 Tools/Audio/hero_barks.py --meta      write a .meta for every clip that exists (mono 22 kHz Vorbis 0.35)
  python3 Tools/Audio/hero_barks.py --jobs      print the Hawkes Gen audio.voice jobs (id, text) still to make
  python3 Tools/Audio/hero_barks.py --check     table sane, audio.json and the C# file in step, a clip for every line (no Unity)
A line's prompt is its subtitle (delivery tags in [] are stripped by Sfx.SpokenText): deadpan, exhausted, annoyed or smug.
"""
import json
import os
import re
import sys
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
AUDIO = os.path.join(ROOT, "Assets/GameConfig/audio.json")
CS = os.path.join(ROOT, "Assets/Scripts/Gameplay/Presentation/HeroBarkLines.cs")
CLIPS = os.path.join(ROOT, "Assets/Audio/Generated")
VOICE_ID = "DzkD7I4tf5i3KSaB4cz5"  # audio.json voices[player]; every bark uses it

# event key -> (priority, lines). Priority: higher wins when two events land on the same frame.
TABLE = {
    "undo": (9, [
        "[smug] Nope. Not today.",
        "[smug] Dead? Hardly. Rewinding.",
        "[annoyed] Let's pretend that never happened.",
        "[smug] Take-backs. Best invention ever.",
        "[exhausted] Again. Fine. Again.",
    ]),
    "lastHeart": (8, [
        "[exhausted] One heart. Great. Love that for me.",
        "[annoyed] That's the last heart. Noted.",
        "[deadpan] One heart left. No pressure.",
        "[exhausted] I'm held together by spite.",
        "[deadpan] Hearts: one. Spirits: lower.",
    ]),
    "graze5": (7, [
        "[smug] Five. I'm basically a professional.",
        "[deadpan] Five near misses. This is a skill now.",
        "[exhausted] Five. Put that on my resume.",
        "[smug] They keep missing. I keep counting.",
    ]),
    "kill5": (6, [
        "[smug] That's five. Anyone else?",
        "[deadpan] Five down. Zero feelings.",
        "[exhausted] Five. Was that supposed to be hard?",
        "[annoyed] Five. They just keep coming. Rude.",
    ]),
    "dashThrough": (6, [
        "[smug] Straight through. Like it wasn't there.",
        "[deadpan] Bullet? What bullet?",
        "[smug] Too slow, pal.",
        "[deadpan] Dodged. Don't clap. It's weird.",
    ]),
    "overlord": (5, [
        "[annoyed] Oh, he's watching. Fantastic.",
        "[deadpan] Hi, boss. Yes, still climbing.",
        "[smug] Look at him. Getting nervous.",
        "[annoyed] Stare all you want. I'm busy.",
    ]),
    "healed": (5, [
        "[deadpan] Oh. I feel slightly less terrible.",
        "[smug] Healing. Take that, Hell.",
        "[exhausted] A free heart? Suspicious.",
        "[deadpan] Good as new. Which isn't saying much.",
    ]),
    "shop": (4, [
        "[smug] Worth every diamond. Probably.",
        "[deadpan] Slime-made. Hope it holds.",
        "[smug] Shopping. My only hobby.",
    ]),
    "firstGraze": (3, [
        "[smug] Oh, it missed. Cute.",
        "[deadpan] That was inches. Cool. Cool cool.",
        "[annoyed] Personal space. Ever heard of it?",
        "[smug] Felt the breeze. Thanks.",
    ]),
    "floor": (2, [
        "[exhausted] Another floor. Yay.",
        "[deadpan] Next floor. Looks exactly the same.",
        "[annoyed] Still not the top, is it.",
        "[exhausted] Up we go. Don't tell me how many are left.",
    ]),
    "idle": (1, [
        "[exhausted] I could just stand here. Forever.",
        "[deadpan] Still here. Still sick of it.",
        "[annoyed] Is anyone coming, or is this the plan?",
        "[deadpan] Great view. Terrible everything else.",
    ]),
}


def lines():
    """(id, event, text-with-tags) for every line, in table order."""
    out = []
    for event, (_, texts) in TABLE.items():
        for i, text in enumerate(texts, 1):
            out.append((f"vo_hero_bark_{event}_{i}", event, text))
    return out


def plain(text):
    return re.sub(r"\s+", " ", re.sub(r"\[[^\]]*\]", "", text)).strip()


def entry_text(sid, text):
    key = f"voice|{VOICE_ID}|eleven_v3|0.5|1|{text}"
    clip = f"Audio/Generated/{sid}.mp3"
    has = os.path.exists(os.path.join(CLIPS, sid + ".mp3"))
    return (
        "    {\n"
        f'      "id": {json.dumps(sid)},\n'
        '      "kind": "Voice",\n'
        f'      "prompt": {json.dumps(text, ensure_ascii=False)},\n'
        '      "durationSeconds": 0,\n'
        '      "promptInfluence": 0.3,\n'
        '      "loop": false,\n'
        '      "voice": "player",\n'
        '      "voiceId": "",\n'
        f'      "clip": {json.dumps(clip) if has else "null"},\n'
        f'      "generatedFrom": {json.dumps(key if has else "", ensure_ascii=False)},\n'
        '      "volume": 1,\n'
        '      "pitchVariation": 0,\n'
        '      "maxVoices": 1,\n'
        '      "cooldown": 0.5\n'
        "    }"
    )


def write_audio():
    src = open(AUDIO, encoding="utf-8").read()
    # drop any earlier blocks of ours, then put the new ones right after vo_meadow_intro.
    src = re.sub(r'    \{\n      "id": "vo_hero_bark_[^"]+",.*?\n    \},?\n', "", src, flags=re.S)
    anchor = '    {\n      "id": "vo_meadow_imps"'
    at = src.index(anchor)
    block = ",\n".join(entry_text(sid, text) for sid, _, text in lines()) + ",\n"
    src = src[:at] + block + src[at:]
    json.loads(src)
    open(AUDIO, "w", encoding="utf-8", newline="\n").write(src)


def write_cs():
    rows = []
    for event, (prio, texts) in TABLE.items():
        ids = ", ".join(f'"vo_hero_bark_{event}_{i}"' for i in range(1, len(texts) + 1))
        rows.append(f'            {{ "{event}", new Bark {{ Priority = {prio}, Lines = new[] {{ {ids} }} }} }},')
    body = "\n".join(rows)
    open(CS, "w", encoding="utf-8", newline="\n").write(f"""// <auto-generated> by Tools/Audio/hero_barks.py: edit its table, never this file. </auto-generated>
namespace SickOfIt.Gameplay
{{
    /// <summary>The hero's {len(lines())} barks (Docs/Story.md "The hero talks back"): event key to a priority and its lines (audio.json voices of `player`).</summary>
    public static class HeroBarkLines
    {{
        public sealed class Bark
        {{
            /// <summary>Higher wins when two events land in the same frame.</summary>
            public int Priority;
            public string[] Lines;
        }}

        public static readonly System.Collections.Generic.Dictionary<string, Bark> Table = new System.Collections.Generic.Dictionary<string, Bark>
        {{
{body}
        }};
    }}
}}
""")


META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 2
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.35
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_meta():
    n = 0
    for sid, _, _ in lines():
        mp3 = os.path.join(CLIPS, sid + ".mp3")
        meta = mp3 + ".meta"
        if os.path.exists(mp3) and not os.path.exists(meta):
            open(meta, "w", newline="\n").write(META.format(guid=uuid.uuid4().hex))
            n += 1
    print(f"{n} .meta written")


def check():
    errors = []
    all_lines = lines()
    if len(all_lines) != 45:
        errors.append(f"{len(all_lines)} lines, expected 45")
    for event, (_, texts) in TABLE.items():
        if not 3 <= len(texts) <= 5:
            errors.append(f"{event}: {len(texts)} lines, 3-5 wanted")
    seen = set()
    for sid, event, text in all_lines:
        p = plain(text)
        if p in seen:
            errors.append(f"{sid}: duplicate words")
        seen.add(p)
        if len(p) > 52:
            errors.append(f"{sid}: {len(p)} characters, a one-line bubble wants 52 at most")
        if not re.match(r"^\[(deadpan|exhausted|annoyed|smug)\]", text):
            errors.append(f"{sid}: no direction tag")
    audio = json.load(open(AUDIO, encoding="utf-8"))
    by_id = {s["id"]: s for s in audio["sounds"]}
    for sid, event, text in all_lines:
        s = by_id.get(sid)
        if not s:
            errors.append(f"{sid}: not in audio.json (run --write)")
            continue
        if s["prompt"] != text or s["voice"] != "player" or s["kind"] != "Voice":
            errors.append(f"{sid}: audio.json differs from the table (run --write)")
        if not s.get("clip"):
            errors.append(f"{sid}: no clip yet")
        elif not os.path.exists(os.path.join(CLIPS, sid + ".mp3")):
            errors.append(f"{sid}: clip file missing")
        elif not os.path.exists(os.path.join(CLIPS, sid + ".mp3.meta")):
            errors.append(f"{sid}: no .meta")
    cs = open(CS, encoding="utf-8").read() if os.path.exists(CS) else ""
    for sid, _, _ in all_lines:
        if f'"{sid}"' not in cs:
            errors.append(f"{sid}: not in HeroBarkLines.cs (run --write)")
    pending = json.load(open(os.path.join(ROOT, "Tools/Audio/pending_clips.json")))["ids"]
    for sid, _, _ in all_lines:
        if sid in pending:
            errors.append(f"{sid}: still in pending_clips.json")
    for e in errors:
        print("ERROR", e)
    print("hero_barks: OK" if not errors else f"hero_barks: {len(errors)} problems")
    return 1 if errors else 0


def main():
    arg = sys.argv[1] if len(sys.argv) > 1 else "--check"
    if arg == "--write":
        write_audio()
        write_cs()
        print("written")
    elif arg == "--meta":
        write_meta()
    elif arg == "--jobs":
        for sid, _, text in lines():
            if not os.path.exists(os.path.join(CLIPS, sid + ".mp3")):
                print(f"{sid}\t{text}")
    elif arg == "--check":
        sys.exit(check())
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
