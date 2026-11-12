# Place ambience: Meadow, Shaft, Floors 1000–997

## What was wrong
Only Level 1 and Floor 1000 had ambient beds (synth stand-ins). Floors 999–997 and the Shaft were silent. No positional prop one-shots. Eyes closed, you could not tell which floor you were on.

## What changed
- **7 Ambience-bus beds:** meadow calm/burning, shaft, floors 1000–997 (`Tools/Audio/floor_ambience.py` + `synth_core_sfx.py`).
- **12 positional one-shots** on props via new `AmbientEmitters` + `LevelConfig.ambientEmitters` (4–12 s gaps, stereo pan by screen x, distance falloff; phone cap = half of 8).
- Shaft plays `ambience_shaft` while open (`ShaftConfig.ambientSound`).
- Playtest **`floor-ambience`** (also in `.hawkes/scenario.txt`).
- World bible + SoundAudit updated.

## Gen / GPU
Budget $0.28 approved, but **org credits are 1** (audio.sfx-open needs 8/job) and the AWS fleet has no keys. Queues refused. **Synth stand-ins ship**; `.hawkes/pending-gpu.txt` lists the Gen replacements. GPU spent: **$0**.

## Checks
- `python3 -I Tools/Audio/floor_ambience.py --check` — pass
- Unity compile not run here (no editor in this sandbox)
- Conflicts: none

## Fun reviewer (self-pass)
**Verdict: thin → living on the sound axis for these places.** Beds + breathing prop one-shots give each floor a distinct air. Gaps: Gen clips still stand-ins; Meadow visual village life (hen/mill art) still missing (out of scope); later floors beyond 997 still bare.

## Reviewer (self-pass)
**Verdict: merge** with pending-gpu note. Criteria 1–2 and 4 are wired; criterion 3 (audio.analyze loop) waits on Gen takes.
