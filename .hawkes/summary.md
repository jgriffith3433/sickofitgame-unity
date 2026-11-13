# Level-clear celebration (k_859dbae5fd3c)

**State found:** #412 had already put a tally, a rank stamp and Yooba's verdict text on the results screen, but none of its sounds had clips (the 8 `vo_yooba_rank_*`, `ui_rank_stamp`, `ui_new_best` were on `pending_clips.json`), the stamp was a drawn starburst, there was no NEW BEST banner, no diamond fly-in and no portrait. It also showed 7 rows, where the brief wants 5. The same rank could repeat its line, because the alternation was shared across ranks.

**Changed**
- Sounds (Hawkes Gen, ElevenLabs, Yooba's existing voice `EdDvd8GSdCyZwUcsPawi`, `eleven_v3`): the 8 verdicts (2 per rank), `ui_rank_stamp`, `ui_new_best`, and a new `ui_tally_tick`. They are wired in `audio.json` and removed from `pending_clips.json`.
- Art: an inked stamp seal (image.generate + cutout) at `Assets/Art/Icons/ResultsStamp.png`, wired as theme `screens.resultsStamp`. The letter is drawn over its blank centre.
- `GameplayHud.Screens.cs`: 5 rows (time, clean kills, grazes, rewinds, diamonds), 0.3 s apart, a tick each; the stamp slams at about 1.9 s; the verdict text is the quip; Continue is 150 units (54 px at 390x844); a tap skips to the end.
- `GameplayHud.Results.cs` (new): 0.2 s shake, 36 UI embers, NEW BEST banner slam (only when `StyleRanks.Record` says a record fell), earned diamonds flying into a total chip that ticks up, `PickVerdict` (per-rank alternation: a replay of the same rank never repeats), phone helpers.
- `Core/Progression/ResultsYooba.cs` (new) and `NextGoalBridge.BuildResultsYooba`: Yooba's portrait beside the stamp, speaking his verdict (placeholder slime until his painted loops land).
- Portrait layout: stats top 0.745 step 0.045, stamp at 0.415 with Yooba left and the banner right, the next-goal card moved to y 0.17 (`NextGoal.cs`, one number).
- Playtest `level-clear` (`SickOfItPlaytest.LevelClear.cs`, run on device phone): two clears by the bot, every tap a finger. Checks: the 8 clips exist, row order, 5 ticks 0.3 s apart, the stamp within 3 s with embers and its sound, the verdict heard and matching the rank, no repeat on a replay of the same rank, tap-to-end within 0.3 s, Continue 48+ px, every word inside the safe area.
- Docs: Story.md, world-bible beat 18, AGENTS.md.

**Not verified:** no Unity here, so "Unity compile not run here". I read the C# for CS0102/missing usings and found none. The playtest has not been run, and nothing was seen on screen, so the portrait layout numbers at 390x844 (Yooba left, banner right, total chip between rows and stamp) are calculated, not looked at. Please check them on beta. The fun-reviewer was not run. Verdict lines carry ElevenLabs tags in `audio.json`; the subtitle strips them.

**Open:** NEW BEST covers the rank record only (there is no best-time record). Yooba's painted portrait loops (k_00e37bc679ba) use whatever `ui/yooba.json` has.

**Gen spend:** about $0.17 of $0.39 (8 voice lines $0.14, 3 sfx $0.02, stamp art $0.007). No GPU work left, no pending-gpu. Conflicts: none.
