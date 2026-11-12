# Rewind look + hit feel (Sands of Time)

## What was wrong
Rewind still read as a tape glitch (scanlines / cyan-magenta over the frame) instead of time pouring backwards. Hits used a long red wash and shake without a clear hero-sprite flash or shove, so bullets could disappear under feedback.

## What I changed
- **Story / bible / metrics / lighting:** canon for desat+ghosts+reverse streaks+gold rim+snap-back; hero flash ≤0.15 s and knockback ≥0.3; shake cap 0.3 s.
- **`RewindFx`:** outer-8% gold strips + optional rim scanlines + sand/ember motes; measured `PlayfieldGlitchCoverage`; 0.2 s snap-back; teal HUD wash softened.
- **`RewindWorldLook` (new):** world desat to ~20% (hero + bullets stay coloured), 8/4 hero ghosts, reverse bullet streaks; release ripple via `FeelVfx.RewindRipple`.
- **Hit feel:** `ApplyHitPush` (≥0.3 u, saved in `MoveState`/`HeroMoves`); white `SpriteFeel` flash 0.12 s on the hero; red edge vignette pulse; `Shake` capped at 0.3 s.
- **Phone:** rewind button fill ring drains while held; playtest holds it with `TouchDown` when touch UI is on.
- **Playtest `rewind-fx`:** hard checks for desat, ≥4 ghosts, ≥1 streak, playfield glitch, flash, knockback, ring drain, fps ≥30.

## Checks
- `/opt/unity-check/unity-check` on changed C# — clean.
- Unity editor compile not run here (no editor in this sandbox).
- Browser / Studio playtest not started (per brief); scenario named in `.hawkes/scenario.txt`.

## Reviews
- **fun-reviewer:** thin (not demo). Soul of the rewind look is there; asked for deeper world-reacts (neighbours/lava reversing) — out of this task’s acceptance criteria, noted for later.
- **reviewer:** merge after revise (streak hard-check, real glitch probe, touch hold). Softened leftover teal wash.

## Conflicts
None. Did not touch `CircleAmbience` (parallel living-floors agent / already on beta).

## GPU
$0 spent. Procedural motes + hero-sprite ghosts; no Gen jobs.
