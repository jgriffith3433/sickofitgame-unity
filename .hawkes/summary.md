# Enemy ink: launcherThief and cinderThatch motion baked; thief death clip still rendering

**Done**
- Judged the six finished clips on mid frames and baked the passes (all through `Tools/Art/bake_clips.mjs`, new: ffmpeg + node, since this worktree has no Pillow/numpy/DoD checkout for `art.mjs --build`; keys by greenness so cyan glow stays and the paler green box in some thatch frames goes).
  - launcherThief: idle and move (the same run loop, replacing the old painterly flying sheets), windup (cyan exhaust glow on every frame, loop), attack (glow, then the lunge; `FireProjectile` moved from frame 2 to frame 5 so the shot leaves on the lunge), hurt.
  - cinderThatch: windup (limbless, cyan flame core; replaces the held still) and attack (limbless).
- `launcherThief` Windup state now 12 fps. Cell sizes and `ppu` (79.9 thief, 85.0 thatch) keep each figure the on-screen size of the held sheet it replaces; every clip of an entity shares one pivot.
- Dead pose judged (soot-and-ember burst, not a chest cannon: weak pass), framed to 640 green (`a_83e43c82bd4a6028`).

**Not done**
- launcherThief **Die** sheet: the die i2v `j_b00c3a95acae` was queued (cold RunPod start, several minutes) and is parked in `.hawkes/waiting-jobs.txt`. Until it lands the thief dies on the old painterly die sheet (`.hawkes/pending-gpu.txt`).
- Couldn't run Unity here (no editor in the worktree): the config validate/import and the `enemy-ink` playtest have not been run. The entity edit is two numbers in `launcherThief.json`; sheets' sidecars follow the existing format.
- The PR's side-by-side screenshots with the hero need the beta `enemy-ink` run (scenario already lists all seven enemies, phone device).

**GPU spend** about $0.20 for the seven finished jobs (`hawkesgen_jobs`), die clip about $0.04 more; well inside $0.86.

**Weak spots** (see Assets/Art/EnemyInk/JUDGE.md): thief hurt clip drifts a bit off-model (baked from frame 6 on), thatch attack is a hop with an angry face more than a lunge, the dead pose is mid-burst.
