# Enemy ink — judged poses, held sheets, i2v queued

## Wrong
`launcherThief` Windup/Attack still pointed at Move; `cinderThatch` windup/attack needed limbless motion after bird-leg i2v fails. Prior Gen pose batch finished this resume.

## Judged (poses)
| Pose | Asset | Verdict |
|---|---|---|
| thief stand | `a_35b1d63f3bd6a42f` | **PASS** — imp + launcher sack silhouette |
| thief windup | `a_63f46cfb1eb5804b` | **PASS** — chamber/exhaust glow telegraph |
| thief attack | `a_ee2ba6e3ad30e7df` | **PASS** — lunge + sack/launcher |
| thief struck | `a_0de3446e74a7d32e` | **PASS** |
| thief dead | `a_0396dffd2fa61f26` | **FAIL** — chest-cannon firing a horned sack (not death). Rejected → `_rejected/launcherThief_dead_chestcannon.png` |
| thatch windup limbless | `a_eb77a64dd91899f6` | **PASS** — no legs/arms |

Pose GPU spend so far **~$0.21** of **$0.86**.

## Done this pass
- Free `image.frame` ×5 → framed thief stand/windup/attack/struck + thatch windup (`a_ceb84e1b929c3e44`, `a_3b1a590b412a0ef4`, `a_b9165821785cd2b2`, `a_6aaca8f81a5afad0`, `a_af6820a9ba56e675`).
- **Held** Windup/Attack sheets for thief (keyed stills, ≤2048); entity Windup/Attack no longer use Move.
- Replaced thatch Windup held sheet from limbless framed pose.
- Queued GPU (`wait_for_server`): dead redo + thief idle/windup/attack/hurt i2v + thatch windup/attack i2v. First queue failed (no GPU / no AWS keys, $0); **requeued** — ids in `.hawkes/waiting-jobs.txt`. Est. **~$0.35** more (fits budget); die i2v after dead PASS.
- Playtest scenario remains `enemy-ink` + phone device.

## Fun review
**thin** on the ink arena alone (QA lineup), but Floor 998 glow-before-snatch + thief station keep the climb beat. Gaps for next resume: motion bake replacing held sheets; dead/die; hero-side screenshot for PR.

## Conflicts
None with other agents. Left unrelated `videos/hello-hole` LFS dirty files unstaged (sparse/LFS churn).

## Checks
Entity JSON parse OK. Unity compile not run here. GPU jobs queued, not waited.
