# Enemy ink: launcherThief and cinderThatch fully inked (die clip landed)

**Done**
- Judged and baked the launcherThief **Die** (`j_b00c3a95acae`, `a_7899998226888672`, ~$0.10): yellow flash from the launcher, then a soot-and-ember cloud; imp stays on-model and is not a chest cannon. Weak pass (he keeps running through the burst instead of falling), noted in `Assets/Art/EnemyInk/JUDGE.md`. 19 frames at 12 fps, non-looping, same pivot and `ppu` 79.9 as his other clips (`Tools/Art/bake_jobs/enemyInkThiefDie.json`, `Tools/Art/bake_clips.mjs`). The entity's `Die` state already pointed at the file at 12 fps, so no config change.
- Earlier in this change (commit d3c1963): launcherThief idle/move/windup/attack/hurt and cinderThatch windup/attack baked from Gen clips, `FireProjectile` on the lunge frame, glow wind-ups of at least 0.3 s.
- Every enemy on the Meadow and Floors 1000-997 now has 5 inked animations plus a hooked wind-up.

**Not done**
- No Unity editor in this worktree: config validate/import and the `enemy-ink` playtest were not run. The die change is a sheet swap with a sidecar in the existing format.
- The PR's side-by-side screenshots with the hero need the beta `enemy-ink` run (scenario lists all seven enemies, phone device).

**GPU spend** about $0.30 in total of the $0.86 (seven earlier jobs ~$0.20, die clip ~$0.10).

**Weak spots**: thief hurt drifts a little off-model, thatch attack is a hop with an angry face more than a lunge, thief death doesn't collapse.
