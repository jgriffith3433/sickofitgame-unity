# Painted parallax backdrops for the Meadow and Floors 1000-997

**All backdrop art in this change is a procedural STAND-IN, not painted art.** Gen is out of credits, so I made 21 seamless
512x512 PNGs with a script (`Assets/Art/Backdrops/`: sky, far, mid, near per level, plus a Meadow raid sky). They are
placeholders: flat silhouettes and gradients. Painted art swaps in by file name (list in `.hawkes/pending-gpu.txt`, project
`org_eb0d5c82e8da8c2d.sickofit`), with no config or code change.

## What changed
- Level 1 (Meadow) and Floors 1000-997: the baked video-frame backgrounds (`spriteFramesData`) are gone. Each has 4 `staticSprite`
  layers, tiled horizontally, parallax 0.1 / 0.25 / 0.5 / 0.8, with a slow drift on each.
- New `BackdropDrift` (Gameplay/Presentation) and three `BackgroundLayerConfig` fields (`driftSpeed`, `raidSprite`, `raidTint`).
  Drift and the Meadow's sky swap at the `imps` phase (the raid) are functions of the game clock, so rewind plays them back.
- `LevelRuntime.cs` and `BackgroundLayerConfig.cs`: narrow additions only (kept small for c_ce98c0a4001b). `SickOfItPlaytest.cs`: the
  scenario registration, plus a fallback for the portal scene's fire-hole probe, which read the old Level 1 video layer (it now uses the
  portal's camera-anchored spot).
- Playtest `backdrop-layers` (device "phone" 390x844, touch): per level checks >=4 layers with distinct factors, no video background,
  no layer smaller than the view, fps >=30, and the Meadow sky swap.
- Docs: world-bible "Backdrops", art-bible "Backdrop layers".

## Sizes (acceptance 3)
The old `Videos/Level1_Background*.asset` and `Video2.asset` sheets are not in this sparse checkout, so before-sizes could not be
measured. New PNGs total ~297 KB (e.g. Meadow sky 27 KB, far 4 KB, mid 4 KB, near 3 KB; floor sky 22-27 KB, far 16-39 KB, mid 3-11 KB,
near 3-6 KB). Per-level bundle sizes need the build's `build-size.json`; baked footage sheets were far larger, but that is unverified.
Floors 996+ still use the Circle's `Video2` backdrop (emberWastes.json untouched).

## Not run
No Unity compile, tests, build or playtest (none available here). The C# was re-read for errors only.

## Self-review (reviewer + fun-reviewer rules, read by me, not a separate agent)
- Gaps: art is stand-in (acceptance 1 is met structurally, not visually); acceptance 3 and 4 are unmeasured until the playtest/build run.
- Fun: backdrops move and the raid changes the sky, but the painted look needs real art; no new interactions were in scope.
