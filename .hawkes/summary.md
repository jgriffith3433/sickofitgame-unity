# Chapter One payoff at the end of Floor 997

## What was wrong
Floor 997’s Imp Run finale ended on a banner + Grudge with no first face-off. The Overlord stayed a far-ledge cameo; chapter one never closed.

## What changed (this task)
- **Story / bible:** `Docs/Story.md` “Chapter One closes on Floor 997”; `docs/world-bible.md` beat + Overlord moment; `Docs/LevelDesign.md` Floor 997 note; `docs/metrics.json` (feel floors).
- **Playable face-off:** `Tools/Levels/chapter_one.py` patches Floor 997 — landing → `chapterOne` (BeatDirector find, three escalating `laserSweep` phrases @ 0.5 s telegraph, five `say` lines, death-rewind Flee), `chapterOneEscape` (laugh + up), `chapterOneCard`.
- **Entities:** `overlordChapter` / `P2` / `P3` (taunt art + lens).
- **UI:** `showChapterCard` + `offerChapterSkip` (`chapter1.seen`); CHAPTER ONE card (time / deaths / rewinds / diamonds, hook VO, Continue ≥48 px design) **before** the Grudge; normal results skipped after.
- **Audio prompts** in `audio.json` (clips pending); listed in `pending_clips.json`.
- **Playtest:** `chapter-one` (`.hawkes/scenario.txt`).

## Merge with origin/beta (this pass)

Beta moved first with **VFX kit** (#402 / `63c6848`). Merged `origin/beta` and resolved:

| File | Result |
|---|---|
| `Assets/Scripts/Playtest/SickOfItPlaytest.cs` | **Auto-merged.** Kept both: `VfxKitId` registration/dispatch and `ChapterOneId` registration/dispatch. |
| `Docs/Story.md` | **Auto-merged.** Kept “The feel of a hit” flipbook section (beta) and “Chapter One closes on Floor 997” (ours). |
| `docs/world-bible.md` | **Auto-merged.** Kept Feel VFX bullet (beta) and Chapter One beat / Overlord arc+moment (ours). |
| `docs/metrics.json` | **Conflict (add/add).** Combined: our measured jump/feel numbers + notes from player.json; beta’s corridor aliases (`narrow`/`standard`/`wide`) and `screenShakeMs` / `diamondFlyMs` (600 for the VFX diamond arc). `hitStopMs` kept at 80 (matches `feel.freeze.heroHurtSeconds 0.08`). |

`.hawkes/*` from beta (`vfx-kit` scenario, Feel VFX pending-gpu/summary) were restored to this task’s chapter-one scenario, pending voice/sting GPU list, and this summary. Beta’s Feel VFX convention line kept in `learned.md`.

## Fun reviewer
**thin** — Full 3, Breathing 3, Interactions 2, Story-as-experience 3, Soul 4.
Gaps (biggest first): (1) wire `vo_ch1_*` + sting clips (blocked on Gen credits); (2) more Imp Run life on the landing during the duel; (3) stage a real ascent escape + a second verb beyond dodge.

## Stuck
Gen org credits: 1 left; `audio.voice` needs 8, `audio.dialogue-open` needs 16. See `.hawkes/stuck.md`. GPU $ budget ($0.47) would cover music + open dialogue once credits exist.

## Checks (after merge)
- `python3 Tools/Levels/chapter_one.py --check` — up to date
- JSON parse of Floor997 / audio / entities / theme / metrics — ok
- No conflict markers left in the four named files
- Unity compile not run here (no batch editor on this agent host)
