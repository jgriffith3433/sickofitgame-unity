---
name: soi-voice-music
description: Write and generate Sick Of It voice lines, sound effects and music through Hawkes Gen, as audio.json data. Use for "lines for Floors 975-951", "a death sound for <enemy>", "the announcer should call out floor 900", "music for <Circle>".
---

# Voice, sound and music for Sick Of It

Every sound is an entry in `Assets/GameConfig/audio.json` (a prompt) and a generated file; never a hand-placed clip.
The recipe and prompting rules are `Docs/GameplayCookbook.md` "Sound effects and voice lines": read it first.

## Writing

- The hero is a sarcastic nobody who is sick of everything; the announcer is Hell's arcade voice. Lines are short,
  dry, and written for `eleven_v3` delivery tags (`[sighs]`, `[deadpan]`, `[shouting]`). Story.md is the canon:
  floors count down, Hell repeats, slimes are friends, **no work or office jokes**.
- Ids: `vo_<level or circle>_<what>` for lines, `<enemy>_hurt` / `<enemy>_die` for enemy sounds; hook them up in
  `events`, the entity's `sounds`, a script's `playSound`, or a `showText`'s `sound`.
- Effects: one physical sound, plainly ("crumbling into ash: a dry crackle and a soft hiss, close-up, no music"),
  0.5–30 s.

## Generating (API cost, no GPU)

- `-executeMethod SickOfIt.Tools.Editor.Audio.SoundGeneratorWindow.GenerateSounds -sounds id1,id2` in batch mode
  (AGENTS.md "Checking your changes"), or the Sound Generator window. Only missing or outdated sounds are
  made, and a sound Hawkes Gen already made with the same settings comes back from its library for free.
- Where Unity can't run (a server agent), generate with `hawkesgen_run` (`audio.voice` / `audio.sfx`, project
  `sickofit`, the voice id from `audio.json` `voices`) into `Assets/Audio/Generated/<id>.mp3` and set the entry's `clip`;
  the Sound Generator will then see it as current only if `generatedFrom` matches, so prefer the batch command when you can.
- About $0.01–0.03 a line or effect: say the count in your summary. Music (ACE-Step on the GPU) is priced with
  `hawkesgen_estimate` and needs J's OK like any GPU batch.

## Check

Validate (`Logs/GameConfig.log`: sound ids are checked on import), then the level's bot playtest (`level-<n>`): the
report lists missing sounds from `[Sfx]` warnings as problems; criteria: no missing sounds, the new lines play at their
beats (subtitles show in the footage).
