# The hero talks back: 45 voiced barks over 11 events

**What was missing:** the hero only spoke on scripted beats. Nothing answered what the player had just done.

**What I made**
- **45 lines, 11 events, 3-5 each** (`undo` 5, `lastHeart` 5, `graze5` 4, `kill5` 4, `dashThrough` 4, `overlord` 4, `healed` 4, `firstGraze` 4, `floor` 4, `idle` 4, `shop` 3), each with a direction tag (deadpan, exhausted, annoyed or smug). The table is `Tools/Audio/hero_barks.py`. It writes the `audio.json` entries (voice `player`, ElevenLabs `DzkD7I4tf5i3KSaB4cz5`, `eleven_v3`, the same recipe as his other lines) and `HeroBarkLines.cs`. `--check` runs without Unity.
- **Voices:** 45 `audio.voice` clips made through Gen with his existing voice (one test line first, no payment error). Spend about $0.52 of the $0.64 budget (45 jobs at $0.006-0.016; one call timed out and was retried). They are in `Assets/Audio/Generated/vo_hero_bark_*.mp3`, each with a hand-written `.meta` (new GUID, mono 22 kHz Vorbis 0.35). `pending_clips.json` is untouched; no bark is in it.
- **`HeroBarks`** (Gameplay/Presentation, created by the HUD next to the Overlord's taunts) reads the telemetry (`graze`, `death`, `playerHit`, `move dash`, `taunt`, `spawn overlordFar*`, `floor`) and polls hearts, the dash and idleness. Rules:
  - the highest priority wins when two events land in one frame;
  - a 15 s global cooldown (the shop's too);
  - no line twice per session;
  - never over another voice (new `Sfx.VoiceBusy`): a bark waits 1-8 s if the event has patience, else it is dropped, and the undone death queues behind the voice so it still answers at once;
  - no barks in scenes, beats, locked input or while dying (except the undone death);
  - voice goes through `Sfx`, so it is already on the Voice bus.
- **The caption** (`GameplayHud.Bark.cs`) is a one-line bubble above the hero for 1.5 s, starting on the frame the voice starts. It is placed inside the safe area and moved off the hero, the rewind button, the rewind hint and the touch-stick zone (above, beside, a little higher, then below).
- **Yooba's shop:** after a buy the hero answers once Yooba has finished his line. The caption shows where Yooba's subtitle is (`UpgradeShopManager.HeroShopBark`).
- **Playtest `hero-barks`** (`.hawkes/scenario.txt`, `SickOfItPlaytest.Barks.cs`):
  - plays Level 1 and Floors 1000-998 with the bot (graze distance widened, as the `style` scenario does), then dies on 998 once the cooldown is open and rewinds the death (R, or a finger on the HUD button on touch);
  - checks 6+ different barks from 4+ events, no repeat, 15 s between barks, the caption about 1.5 s, a graze streak of 5 and the undone death each barking within 1 s;
  - on touch (390x844) checks the bubble stays on screen and off the hero, rewind button and stick.
- Docs: Story.md "The hero talks back", world-bible (Hero + What happens), AGENTS.md bullet. Existing level-beat lines are untouched.

**Not verified here:** no Unity or dotnet on this machine, so **Unity compile not run here**. I read the C# against the signatures it calls (`Entity.GapTo`, `FindAbility`, `PlayerHealth`, `GameplayHud` rect getters) and ran `hero_barks.py --check` (OK). `sound_audit.py` only fails on ambience `.ogg` files the sparse checkout lacks. The fun-reviewer was not asked. Nothing was played, so the bubble placement and the 1 s timings are untested.

**Judgment calls for J**
- The 15 s cooldown also gates the undone death, so a death rewound within 15 s of another bark stays silent (acceptance criterion 2 is strict). The scenario waits for the cooldown before dying.
- The first graze usually lands a few seconds after the arrival bark, so a streak of 5 soon after may be muted by the cooldown. The scenario counts only streaks with the cooldown open, and warns if none happened.
- "Overlord sighted" listens to the existing `taunt` telemetry and `overlordFar*` spawns, once a floor. I did not see k_6ec95a098f2e's code; if it adds a new sighting event, add it to `HeroBarks.OnRecorded`.
- Conflicts: none. I touched `Sfx.cs` (+5 lines), `GameplayHud.cs` (+4), `UpgradeShopManager.cs`, `AGENTS.md` and `Story.md` (a new section), and inserted my `audio.json` entries after `vo_meadow_intro`, not at the end, to stay clear of the portraits change (c_32567712b7c7), which touches those files too.

## Merge with origin/beta (enemy ink and Chapter One payoff)
Conflicted: `.hawkes/scenario.txt`, `.hawkes/summary.md`, `.hawkes/try.md`. All three are per-change notes; no game file conflicted (beta's changes are enemy art, `launcherThief.json` and bake tooling, none of which this change touches).
- `scenario.txt`: the file holds one scenario. Kept this change's `hero-barks` and added line 2 `{"device":"phone"}` (beta's own device line; `hero-barks` also checks the touch layout at 390x844). Beta's `enemy-ink` is not listed here; it runs from its own merged change.
- `summary.md`: this change's summary kept; beta's enemy-ink summary appended below.
- `try.md`: my steps kept, beta's steps appended.
- No Unity here, so config validate/import and playtests were not run after the merge. Checked: no conflict markers left.

---
# From beta (already merged, not this change)
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
