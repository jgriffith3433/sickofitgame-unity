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
