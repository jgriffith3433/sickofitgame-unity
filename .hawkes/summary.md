# Enemy ink — paused until Gen fleet works

## J's call
Answered **Pause until Gen fleet works** (no close). Holding this change open; not shipping a “done” close on stills-only thief/thatch motion.

## Status
- Gen fleet still broken: `hawkesgen_fleet` → servers `[]`, error AWS keys missing in `hawkesgen/.env`.
- Prior requeues failed instantly (No GPU, $0). Org credits were also under the video.i2v floor (40).
- **No new Gen jobs this pass** (per pause + memory: don’t requeue while keys are missing).
- Held Windup/Attack sheets + entity wiring from `08d5614` still ship. Scenario `enemy-ink` (phone). Remaining GPU work listed in `.hawkes/pending-gpu.txt`.
- Unity compile not run here.

## Still to make (when fleet works)
1. launcherThief dead pose redo + die i2v + bake
2. launcherThief idle/windup/attack/hurt i2v + bake over held sheets
3. cinderThatch limbless windup/attack i2v + bake (never `a_f5d9ad823922f967`)

Est. ~$0.32 of remaining budget once GPU + credits are available.

## Fun review (prior pass, still stands)
**Verdict: thin** — Floor 998 theft beat + roster wind-ups live; held stills weaken the Cuphead bounce until motion lands.

## Conflicts
None.
