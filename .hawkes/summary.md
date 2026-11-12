# Rewind look + hit feel (Sands of Time) — merge with beta

## What was wrong
Rewind still read as a tape glitch (scanlines / cyan-magenta over the frame) instead of time pouring backwards. Hits used a long red wash and shake without a clear hero-sprite flash or shove, so bullets could disappear under feedback.

## What I changed (this task)
- **Story / bible / metrics / lighting:** canon for desat+ghosts+reverse streaks+gold rim+snap-back; hero flash ≤0.15 s and knockback ≥0.3; shake cap 0.3 s.
- **`RewindFx`:** outer-8% gold strips + optional rim scanlines + sand/ember motes; measured `PlayfieldGlitchCoverage`; 0.2 s snap-back; teal HUD wash softened.
- **`RewindWorldLook` (new):** world desat to ~20% (hero + bullets stay coloured), 8/4 hero ghosts, reverse bullet streaks; release ripple via `FeelVfx.RewindRipple`.
- **Hit feel:** `ApplyHitPush` (≥0.3 u, saved in `MoveState`/`HeroMoves`); white `SpriteFeel` flash 0.12 s on the hero; red edge vignette pulse; `Shake` capped at 0.3 s.
- **Phone:** rewind button fill ring drains while held; playtest holds it with `TouchDown` when touch UI is on.
- **Playtest `rewind-fx`:** hard checks for desat, ≥4 ghosts, ≥1 streak, playfield glitch, flash, knockback, ring drain, fps ≥30.

## Brought in from beta (other change)
Rook/Wick real-art prep (blocked on Gen org credits): `art/rook.json` Scratch/Talk, `art/wick.json` LightLamp/Talk, `.hawkes/pending-gpu.txt`, Yooba sheet PNG refreshes, living-air / ambience particle tooling. Gameplay locals already on beta; Gen sheets still pending credits.

## Conflicts (merge `origin/beta`)
Only `.hawkes` data files conflicted; no binaries, scenes or prefabs.

| File | Resolution |
|---|---|
| `.hawkes/learned.md` | Kept **all** bullets from both sides (rewind/hit canon+conventions + Rook/Wick Gen credits lessons and canon voice ids). |
| `.hawkes/scenario.txt` | Kept **this** change’s `rewind-fx` (acceptance criteria). Beta’s `climber-locals` + phone options stay the scenario for the already-merged Rook/Wick change; noted in try.md so neither side is dropped. |
| `.hawkes/summary.md` | Combined: this rewind summary + beta’s Rook/Wick blocked-on-credits note + this Conflicts table. |
| `.hawkes/try.md` | Kept rewind try steps first; appended the Shaft Rook / Floor 999 Wick steps from beta. |

## Checks
- `/opt/unity-check/unity-check` on rewind C# — clean (pre-merge); merge touched no C# from this task.
- Confirmed `RewindFx.cs` / `RewindWorldLook.cs` still present after merge; no conflict markers left in `.hawkes/`.
- Unity editor compile not run here (no editor in this sandbox).
- Browser / Studio playtest not started (per brief); scenario named in `.hawkes/scenario.txt`.

## Reviews
- **fun-reviewer:** thin (not demo). Soul of the rewind look is there; asked for deeper world-reacts (neighbours/lava reversing) — out of this task’s acceptance criteria, noted for later.
- **reviewer:** merge after revise (streak hard-check, real glitch probe, touch hold). Softened leftover teal wash.

## GPU
$0 spent on rewind (procedural motes + hero-sprite ghosts). Rook/Wick Gen still blocked on org credits (see beta’s pending-gpu).
