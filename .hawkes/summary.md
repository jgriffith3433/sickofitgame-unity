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

## Merge with origin/beta (Chapter One payoff, #455 and others)
Conflicted: `.hawkes/learned.md`, `.hawkes/scenario.txt`, `.hawkes/summary.md`, `.hawkes/try.md`. All four are per-change notes, no game files conflicted.
- `learned.md`: kept both sides' lines (my tool/GPU notes, plus the other change's `canon: Chapter One close` line).
- `scenario.txt`: this change's `enemy-ink` + `{"device":"phone"}` kept; the file holds one scenario, so the other change's `chapter-one` is not listed here (beta runs it from its own merged change).
- `try.md`: my steps kept, the other change's Chapter One step appended.
- `summary.md`: my summary kept; the other change's summary is below.
- No Unity here, so config validate/import and playtests were not run after the merge. Checked: no conflict markers left.

---
# Chapter One payoff, end of Floor 997

Last step done: the Gen output is in and wired.

- Overlord lens exchange on Floor 997 and the CHAPTER ONE card (time, deaths, rewinds, diamonds, a voiced hook, Continue of at least 48 px) were committed earlier, with the seen-flag and Skip. Story text is in `Docs/Story.md` ("Chapter One closes on Floor 997").
- **Voice (Gen job `j_ad4c415941d4`, audio.dialogue, one take, 18.9 s, $0.06):** the six lines were made as one exchange so the two voices react to each other, then split into stems. The segment timings are sensible: 2-4 s per line, hero lines shorter than the Overlord's. I imported each stem to `Assets/Audio/Generated/vo_ch1_*.mp3` and set `clip` and `generatedFrom` in `audio.json`.
- **Sting (Gen job `j_e12c9c974e58`, audio.music, 10 s):** imported as `music_chapter_one_sting.mp3` and set in `audio.json`.
- I removed all seven ids from `Tools/Audio/pending_clips.json`. `sound_audit.py --check` shows nothing for `ch1` or the sting. The remaining errors are older: missing ambience beds and `vo_stop853_pitch1`.
- `.meta` files are new, with fresh GUIDs, copied from the existing voice import settings.
- **Not listened to:** I have no way to play audio here, so the clips are judged from the job's timings and transcripts only. Listen for the Overlord's bored read and the sting's brass-to-major resolve on beta.
- **Not run:** no Unity import or playtest (Studio runs those). Scenario for beta: `chapter-one`, run at 390x844 touch too.
- GPU spend is about $0.06 for the dialogue plus the sting, within the $0.47 budget; the sting's cost isn't in the job output I read, so check `hawkesgen_jobs` for the exact total.
- No merge conflicts.
