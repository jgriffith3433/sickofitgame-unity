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
