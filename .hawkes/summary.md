# Enemy ink — still paused (fleet AWS keys missing)

## J's call (holds)
**Pause until Gen fleet works** — do not requeue launcherThief / cinderThatch i2v while `hawkesgen_fleet` shows AWS keys missing / `servers: []`. Held Windup/Attack sheets + `.hawkes/pending-gpu.txt` stay; resume when fleet can launch and org credits cover `video.i2v` (≥ 40).

## This pass (`c_8ec024977cdc`)
- Rechecked fleet: error `AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY are not in hawkesgen/.env`; `servers: []`; no requeue; $0 GPU spend.
- No gameplay/art edits — beta already has the pause merge (#395 / prior `c_666db16040a6`): held thief Windup/Attack, limbless thatch windup hold, Floor 998 glow-before-snatch, playtest `enemy-ink` (phone).
- Refreshed `.hawkes/pending-gpu.txt`, `.hawkes/waiting-jobs.txt` (no live job ids), scenario / try / learned for Studio’s resume gate.

## Still to make (when fleet works)
1. launcherThief dead pose redo + die i2v + bake (never chest-cannon `a_0396dffd2fa61f26`)
2. launcherThief idle/windup/attack/hurt i2v + bake over held sheets (framed seeds in pending-gpu)
3. cinderThatch limbless windup/attack i2v + bake (never `a_f5d9ad823922f967` / bird-leg seeds)
4. Floor score loops remain credit-blocked in pending-gpu (other item; not this GPU budget)

Est. ~$0.32 of the approved $0.86 once GPU + credits exist.

## Conflicts
None this pass (no merge; working tree was clean from beta).

## Checks
- `hawkesgen_fleet` — blocked (documented above).
- Unity compile not run here (no editor; no C# changed).
- Scenario still `enemy-ink` + `{"device":"phone"}`.

## Fun review
**Verdict: thin** (unchanged) — early-floor roster + Floor 998 theft beat read as a world beat; held stills for thief/thatch wind-up/attack still weaken the Cuphead bounce until motion lands after the fleet pause.
Full 3 / Breathing 3 / Interactions 3 / Story-as-experience 3 / Soul 4.
Gaps when resumed: (1) thief motion sheets over held Windup/Attack, (2) thatch limbless windup/attack motion, (3) thief Die from a non-chest-cannon dead pose.
