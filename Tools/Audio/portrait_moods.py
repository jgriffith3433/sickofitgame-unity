#!/usr/bin/env python3
"""Writes the `mood` field of every voiced line of the five portrait characters in Assets/GameConfig/audio.json
(hero = voice `player`, overlord, yooba, rook, wick), so the painted portrait (ui/portraits.json) shows the right expression.

The mood is one of neutral, smug (the angry/sly frame), laughing, shocked. It is read from the line's delivery tags and words
(the same reading Core/UI/Portraits/PortraitVoice.cs `PortraitMood.Infer` makes for a line left without a mood), then OVERRIDES
below set the lines a reader would play differently. Edit this table, never the JSON.

  python3 Tools/Audio/portrait_moods.py --apply   write the moods (text edit, the file's layout is kept)
  python3 Tools/Audio/portrait_moods.py --check   exit 1 if a line's mood is missing or stale, or a character has fewer than 4 moods
"""
import json, re, sys, collections
from pathlib import Path

AUDIO = Path(__file__).resolve().parents[2] / "Assets/GameConfig/audio.json"
VOICES = {"player": "hero", "overlord": "overlord", "yooba": "yooba", "rook": "rook", "wick": "wick"}
MOODS = ("neutral", "smug", "laughing", "shocked")

LAUGH = ("laugh", "chuckl", "giggl", "cackl", "snicker")
SHOCK = ("gasp", "shock", "surpris", "scream", "panic", "yelp", "alarm", "horrif", "disbelief", "urgent", "gulp", "nervous", "realising")
SMUG = ("smug", "angry", "snort", "scoff", "sneer", "annoy", "furious", "sarcastic", "proud", "unimpressed", "indignant", "suspicious", "shouting", "squinting", "dryly", "groan", "amused")

OVERRIDES = {
    # The Overlord: bored by default, a broad grin when he enjoys it, a start when the hero is right behind him.
    "vo_ovl_t_imp2": "laughing", "vo_ovl_t_bullet2": "laughing", "vo_ovl_t_fall2": "laughing", "vo_ovl_t_fin1": "laughing",
    "vo_ovl_t_streak1": "laughing", "vo_ovl_t_village": "smug", "vo_ovl_t_bullet3": "smug", "vo_ovl_t_streak3": "smug",
    "vo_ovl_t_bottom": "smug", "vo_ovl_t_hazard3": "smug", "vo_ovl_t_undo2": "smug", "vo_ovl_t_fin3": "smug", "vo_ovl_t_rew4": "smug",
    "vo_ch1_ovl_caught": "shocked", "vo_ovl_t_rew2": "shocked", "vo_ch1_ovl_far": "smug", "vo_ovl_997_up": "smug",
    # Wick: kind and patient; a stern frame for his warnings, a start at the lava, a glow when a lamp stays lit.
    "vo_wick999_warn0": "smug", "vo_wick999_warn2": "smug", "vo_wick999_lamp2": "shocked", "vo_wick999_plain0": "shocked",
    "vo_wick993_intro0": "shocked", "vo_wick902_plain0": "shocked", "vo_wick902_outro0": "laughing", "vo_wick993_rook0": "laughing",
    "vo_wick993_rook2": "laughing", "vo_wick960_intro0": "shocked", "vo_wick902_friends0": "laughing", "vo_wick960_grudges0": "smug",
    # Rook: vain and sly by default.
    "vo_stop897_fire": "laughing", "vo_stop897_win": "laughing", "vo_rook992_intro0": "laughing", "vo_rook858_rich2": "shocked",
    "vo_rook992_race0": "shocked", "vo_rook858_intro0": "shocked", "vo_rook996_intro0": "shocked", "vo_rook955_dead0": "shocked",
    "vo_rook899_skipped2": "shocked", "vo_rook955_intro0": "neutral", "vo_rook955_intro2": "neutral", "vo_rook808_intro0": "neutral",
    "vo_rook808_intro2": "neutral", "vo_rook808_kind0": "neutral", "vo_rook808_outro0": "neutral", "vo_rook808_outro2": "laughing",
    "vo_rook955_outro0": "neutral", "vo_rook899_intro0": "neutral", "vo_rook899_asked0": "neutral", "vo_stop897_sit": "neutral",
    "vo_stop897_behind": "neutral", "vo_rook996_intro2": "smug",
}
OVERRIDES.update({
    # Yooba: a giggle is a laugh; a salesman's pride and a bragging stock line is the cheeky frame; excited is just bright.
    "vo_yooba_weapon_pockets": "smug", "vo_yooba_welcome_king": "smug", "vo_yooba_stock_king": "smug", "vo_yooba_welcome_sale": "smug",
    "vo_yooba_welcome_customer": "smug", "vo_yooba_rank_s_1": "smug", "vo_yooba_buy_dip": "smug", "vo_yooba_buy_heels": "smug",
    "vo_yooba_loop": "smug", "vo_yooba_stock_thanks": "smug", "vo_yooba_quest_done1": "laughing", "vo_yooba_deaths_5": "shocked",
})
ROOK_SMUG = ("rook", "winning", "first", "cheat", "impressed", "medal", "second", "champion")


def infer(voice, sid, prompt):
    if sid in OVERRIDES:
        return OVERRIDES[sid]
    t = prompt.lower()
    tags = " ".join(re.findall(r"\[([^\]]*)\]", t))
    words = re.sub(r"\[[^\]]*\]", " ", t)
    if any(k in tags for k in LAUGH) or "hahaha" in words or "ha ha" in words:
        return "laughing"
    if any(k in tags for k in SHOCK) or "?!" in words or "!?" in words:
        return "shocked"
    if any(k in tags for k in SMUG):
        return "smug"
    if voice == "rook" and any(k in words for k in ROOK_SMUG):
        return "smug"
    if voice == "player" and words.strip().endswith("!"):
        return "smug"
    return "neutral"


def apply(check):
    lines = AUDIO.read_text(encoding="utf-8").split("\n")
    out, sid, prompt = [], "", ""
    stale, changed = 0, 0
    counts = collections.defaultdict(collections.Counter)
    i = 0
    while i < len(lines):
        line = lines[i]
        m = re.match(r'^(\s*)"id": (".*"),$', line)
        if m:
            sid = json.loads(m.group(2))
        m = re.match(r'^\s*"prompt": (".*"),$', line)
        if m:
            prompt = json.loads(m.group(1))
        m = re.match(r'^(\s*)"voice": "(\w+)",$', line)
        out.append(line)
        if m and m.group(2) in VOICES:
            indent, voice = m.group(1), m.group(2)
            mood = infer(voice, sid, prompt)
            counts[voice][mood] += 1
            want = f'{indent}"mood": "{mood}",'
            nxt = lines[i + 1] if i + 1 < len(lines) else ""
            if re.match(r'^\s*"mood": ', nxt):
                if nxt != want:
                    stale += 1
                out.append(want)
                i += 1
            else:
                stale += 1
                out.append(want)
        i += 1
    for voice, c in counts.items():
        print(f"{VOICES[voice]:9} " + "  ".join(f"{m} {c[m]}" for m in MOODS))
    poor = [VOICES[v] for v, c in counts.items() if sum(1 for m in MOODS if c[m] > 0) < 4]
    if poor:
        print("FAIL: fewer than 4 moods used by", ", ".join(poor))
    if check:
        if stale:
            print(f"FAIL: {stale} lines have a missing or stale mood (run --apply)")
        sys.exit(1 if stale or poor else 0)
    AUDIO.write_text("\n".join(out), encoding="utf-8")
    print(f"wrote {stale} moods")
    sys.exit(1 if poor else 0)


if __name__ == "__main__":
    if "--apply" in sys.argv:
        apply(False)
    elif "--check" in sys.argv:
        apply(True)
    else:
        print(__doc__)
