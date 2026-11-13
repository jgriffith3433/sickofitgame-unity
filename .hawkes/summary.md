# Summary

**Build fix (beta web 140, "Game config import FAILED").** The importer's `ValidateAudio` (GameConfigImporter.cs) treats an Effect sound's `durationSeconds` outside 0.5–30 s (0 = let ElevenLabs decide) as an **error**, and six sounds broke that rule:
- `player_jump` 0.45, `player_land` 0.45, `hit_pop` 0.45, `graze_tick` 0.4: all set to 0.5 s.
- `menu_theme` 120 and `menu_theme_loop` 90 (no clip yet): both set to 30 s.

The four short core verbs are written by `Tools/Audio/core_sfx.py`, so I changed its `GEN` table too (only the seconds), or the next run would write the error back. Their clips and `generatedFrom` are untouched, so the clips you hear are the same. The Sound Generator will see the four as out of date (they were made at 0.45/0.4 s) and remake them only when someone asks it to.

**How I looked.** I mirrored the importer's audio rules in Python (duplicate ids, voices, stability, enum names, unknown fields, durations, clip paths checked against git's index because this checkout is partial). The durations were the only failures. I also checked:
- every asset path in GameConfig against the index: none missing;
- every `[SoundRef]`/`[EntityRef]`/… id in content and level scripts: only false positives from my rough scan;
- the fields added by the latest GameConfig commits (#403–#413) against the C# classes: all exist.

**What I could not check.** Unity isn't available here, so I could not run the import. The build log names only Logs/GameConfig.log, so if the importer finds another error my static checks don't cover, it will show on the rebuild. I did not say the build is fixed: only that rebuild proves it.

**Clarity task.** Already on beta and unchanged here:
- #354: hearts and the rewind prompt only on Floor 1000; the dash and weapon chips come later.
- theme.json `hud.*.rewindHint`: the "Hold R to undo!" hint is placed away from the rewind button.
- #430: the `floors` playtest checks both ("floor 1000 shows no dash or weapon chip", and "floor 1000 hint and rewind button stay apart" measured from the real screen rects).

The scenario is `floors`; its portrait run switches the touch UI on.

**Overlap.** c_97bf9e351465 is fixing the same build failure. If it lands the same values, the merge is identical lines; if it chose other in-range values, keep either one. I touched only `Assets/GameConfig/audio.json` (6 numbers) and `Tools/Audio/core_sfx.py` (4 numbers). No merge conflicts.

**GPU estimate:** none needed. No generation was done or is required.
