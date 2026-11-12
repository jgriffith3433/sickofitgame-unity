# Enemy ink — paused; merged origin/beta (adaptive score)

## J's call
**Pause until Gen fleet works** (no close). Holding this change open; not shipping a “done” close on stills-only thief/thatch motion.

## Status
- Gen fleet still broken: AWS keys missing in `hawkesgen/.env`; no requeue this pass.
- Held Windup/Attack sheets + entity wiring from `08d5614` still ship. Scenario `enemy-ink` (phone).
- Merged `origin/beta`: took Meadow Gen-looped score cues (`score_meadow_{explore,fight,finale}.ogg`) from the adaptive-score change; floor loops remain credit-blocked in pending-gpu.

## Still to make (when fleet works)
1. launcherThief dead pose redo + die i2v + bake
2. launcherThief idle/windup/attack/hurt i2v + bake over held sheets
3. cinderThatch limbless windup/attack i2v + bake (never `a_f5d9ad823922f967`)

Est. ~$0.32 of remaining budget once GPU + credits are available.

## Conflicts (merge origin/beta)
- `.hawkes/learned.md` — kept both: fleet-pause / No-GPU / i2v-credit lessons + enemy-ink canon; folded in adaptive-score credit lesson, Gen project-id convention, and score BPM canon (≤5 lines).
- `.hawkes/pending-gpu.txt` — kept enemy-ink pause + thief/thatch GPU list **and** beta’s 12 floor `audio.music-loop` credit-blocked rows.
- `.hawkes/scenario.txt` — kept **this** change’s `enemy-ink` + phone device (beta had `score-floors` for the already-merged adaptive-score PR).
- `.hawkes/summary.md` — rewritten for this merge (enemy-ink pause + conflict notes); beta’s adaptive-score summary intent preserved above.
- `.hawkes/try.md` — kept enemy-ink steps; appended a short note for Meadow score layers now on beta.
- `.hawkes/waiting-jobs.txt` — **modify/delete**: beta deleted (no live waits after Meadow loops); kept our PAUSED resume checklist (no live job ids).

## Checks
- `python3 Tools/Audio/adaptive_score.py --check` (after merge).
- Unity compile not run here (no editor).
- No GPU spend this pass.

## Fun review (prior pass, still stands)
**Verdict: thin** — Floor 998 theft beat + roster wind-ups live; held stills weaken the Cuphead bounce until motion lands.
