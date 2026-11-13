# Feel VFX kit: the twelve Hawkes Gen flipbooks are in

**Done**
- All 12 flipbooks are Hawkes Gen art, packed into `Assets/Resources/Effects/FeelVfxAtlas.png` (1280x1536, 10 frames x 12 rows, 128 px cells, ~2.1 MB): muzzle, bullet impact, enemy hit, death bursts for the imp (soot + embers), the cinder (ash shatter) and the sack imps / Floor 999-997 threats, diamond sparkle, hero hurt, hero death explosion, dash afterimage, graze streak, rewind ripple. `FeelVfx` (already pooled through `VfxPool`, no Instantiate in play, phone sizes smaller, diamonds on a 0.6 s arc to the HUD gem) now draws them instead of the stand-ins; `FeelVfxAtlas.HasBakedArt` is true.
- Pipeline per effect: `image.generate` keyframe -> `video.i2v` (640 px, 33 frames) -> `sprite.sheet` (256 px cells). The model's "green" is never exactly 00ff00 (the keyframes came out 27ce4c, 49c971 ...), so each sheet is keyed on its own measured background colour; the keying is clean. Bursts that were over by frame 6 were re-cut with 14-20 frames and the first 10 taken, so all 10 cells are used.
- `Tools/Art/fx/feel_vfx_pack.py` was rewritten to need only ffmpeg: scales to 128, fades the diamond / dash / impact tails (the Wan clips end holding the gem and the arrow), makes the rewind ripple light so the cyan tint shows on dark ground.
- Docs: `docs/art-bible.md` section 8 and `Docs/Story.md` updated. Scenario: `vfx-kit` (`.hawkes/scenario.txt`), unchanged from the earlier part; it now reports `vfx.hasBakedArt` 1.

**GPU spend** (Gen project `org_eb0d5c82e8da8c2d.sickofit`): 12 keyframes ~$0.05, 12 video.i2v ~$0.50, sprite.sheet free. About $0.55 of the $0.59.

**Not done / honest notes**
- Not run: Unity, the playtests or a build (not allowed here); the atlas has never been seen in the engine. Judged only on contact sheets over a dark ground.
- The atlas loads through `Resources.Load`, not the `core.late` Addressables scope: that scope is fed by GameDefinition references, so it needs a new config field plus a prefix in `PlayerDataManifest`. At ~2.1 MB it is small next to the 20 MB pre-gameplay budget, but moving it to core.late is the left-over.
- The dash is a blue arrow shape and the diamond keys to a deeper blue than the cyan asked for (key edge); the graze streak is a thin line. All read, none are as rich as the bursts; a second Gen pass could improve them.
- Not committed: no merge conflicts; the keyframes/strips are scratch in `.hawkes/` (not for the PR).
