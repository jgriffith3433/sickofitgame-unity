# Rook on the Shaft + Wick’s lamp walk (Floor 999)

## What was wrong
Players never met Hell’s locals the way the brief asks: Rook had floor scenes but no Shaft chalk wall of the real rewind count, and Wick’s Floor 999 beat was talk-only — he didn’t walk ahead lighting lamps while input stayed live.

## What changed
- **Story / bible:** Shaft Rook tally + Floor 999 lamp walk documented (`Docs/Story.md`, `docs/world-bible.md`). Kept canon Rook/Wick voice ids (bright/boastful + soft wheeze); did **not** redesign voices to “gravelly”.
- **Rook on every Shaft stop:** `ui/rook.json`, `RookConfig`, `RookShaft` (tally wall = `Achievements.Get("rewinds")`, bundles of five, live scratch when the count rises, 16 lines by band 0 / 1–5 / 6–20 / 20+, never the same twice). Wired into `ShaftScreen`, `GameDefinition`, `GameConfigLayout`, `PlayerDataManifest` (`rook.` → `core.late`).
- **Wick on Floor 999:** `companions.py` `build_wick999` — three `wickLamp` → `wickLampLit` with flash, `teachGlow`, pop “lit”, `sfx_wick_lamp`, `say` bubbles (input live). Entities `wickLamp` / `wickLampLit`.
- **Playtest:** `climber-locals` (`SickOfItPlaytest.ClimberLocals.cs`); `.hawkes/scenario.txt`.
- **Audio:** 16 Rook Shaft lines + chalk/lamp SFX entries in `audio.json`; Floor 999 lamp lines via companions. **Clips not generated** — Gen org credits: `audio.voice` needs 8, 1 left (see `.hawkes/stuck.md`). Ids in `pending_clips.json`.
- **Art:** placeholders + existing Idle/Walk sheet.json; GPU sheets listed in `.hawkes/pending-gpu.txt` (budget $0.88 unused after voice wall).

## Fun-reviewer
Verdict: **thin**. Fixed what we could without credits (bigger lamp placeholders, teachGlow + pop on light). Left: land voice/SFX clips; optional wall-figure Rook; real lamp/LightLamp art.

## Checks
- `python3 Tools/Levels/companions.py` (Floor 999 + voices table)
- Unity compile **not run here** (no editor in this worktree)
- Voice Gen: one ElevenLabs test → credits wall → stopped

## Conflicts
None with `c_e7b101d18752` (living floors / CircleAmbience); no shared files touched.

## What’s left
1. Gen credits → make clips → import → clear `pending_clips` / delete `stuck.md` when unblocked  
2. GPU art in `pending-gpu.txt` within the $0.88 budget once a server is free  
3. Beta playtest `climber-locals` (and phone 390x844 for ≥64 px / captions)
