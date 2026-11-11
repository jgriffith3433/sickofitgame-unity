# Beats: short framed in-engine scenes

A **beat** is a story moment told with the engine: letterbox bars, a camera move, voiced lines in speech bubbles, a
shake. Beats are level-script commands (`Assets/GameConfig/levels/*.json`, `content/sequences`, `content/scenes`), so
every story task builds them from JSON. Code: `Gameplay/Content/Beats.cs` (commands), `LevelDirector` (`Beats`,
`BeginBeat`, `EndBeat`, `PollBeat`), `GameplayHud.Beat.cs` (bars and bubble).

Rules a beat keeps (the player goal: *never trapped, one tap skips*):

- **Any tap, click, key or pad button during a beat ends it** (after a 0.4 s grace: the fight's last clicks are not a
  skip). The beat's `onSkip` list runs at once (the end state: positions, flags, spawned actors) and the bars slide out;
  the end state is on screen within a frame, always inside 0.5 s of the press. `maxSeconds` (default 10) ends a beat the
  same way if it runs long (a hero who walks slowly, a clip that stalls).
- **Input outside beats stays live.** `beatStart` locks input and rewind; `beatEnd` (or a skip) gives them back when
  `unlockAtEnd` is true. Leave it false only when the level ends right after (Level 1's portal).
- **Seen-flag per beat.** When a beat ends (played or skipped) its `id` is remembered for the session. Playing it again
  runs in *replay mode*: every `wait`, `cameraFocus` move and bar slide takes 40% of its time and `say` shows its
  caption for 0.45 s without repeating the clip: well over 40% shorter. (`BeatRuntime.ForgetAll()` is the playtest's reset.)
- **No tape look in the play area** while a beat runs: the rewind scanlines, colour split and tracking bar stay off
  (`RewindFx.BeatHidden`).
- **Phone:** the bars sit inside the safe area (they are children of the HUD's `SafeArea` and slide in by growing),
  captions are drawn at 20+ screen px (`GameplayHud.BubbleMinPixels`, scaled by the canvas) and stay on screen.
- Scene clips hold their pose until `Idle` is asked for (convention): end a clip with `playPlayerAnimation Idle`.
- Tell the beat with events and voice first; the bubble is one short line (about 40 characters), never a paragraph.

## The commands

| Command | What it does |
|---|---|
| `beatStart` | `id`, `maxSeconds` (10), `onSkip` (instant actions, no waits), `skipPhase`, `unlockAtEnd` (true). Locks input and rewind, bars in over 0.3 s, arms the skip. Emits `beat:<id>:start`. |
| `beatEnd` | Ends it the normal way: bars out, seen-flag set, input back if asked. Emits `beat:<id>:end`. |
| `letterbox` | `on`, `seconds` (0.3). Bars in or out by hand (held across two beats). |
| `cameraFocus` | Pan and/or zoom the Static camera: `target` (an actor id, `player` too; `at` is then an offset) or `at` + `anchor`; `size` (0 = keep zoom); `keepPosition` (zoom only); `seconds`, `ease`. Cut short in replays. `cameraReset` returns. |
| `say` | An actor speaks: `actor` (id or `player`; or no actor and a point `at`/`anchor`), `text` (one short line), `sound` (an audio.json voice), `color`. Plays the clip, shows the bubble above the speaker, **holds its sequence until the clip ends** (`minSeconds`, `pad`). Put it in its own `runSequence` to let it overlap the action. |
| `cameraShake` | Existing: `intensity`, `duration`. |
| `slowMotion` | Existing slow-mo: `scale`, `duration` (real seconds). |
| `wait` | Existing, inside a sequence; shortened in a replay. |
| `playPlayerAnimation`, `walkPlayerTo`, `facePlayer`, `pullPlayerInto`, `walkEntityTo`, `playEntityAnimation`, `spawn`, `playSound`, `flash`, `fade` | Existing scene commands (Docs/GameplayCookbook.md "A scene with the hero"). |

## Example (the bench scene, `content/scenes/beatTest.json`)

```json
{ "type": "beatStart", "id": "beatTest", "maxSeconds": 8, "unlockAtEnd": true,
  "onSkip": [ { "type": "setCounter", "name": "beatTestDone", "value": 1 } ] },
{ "type": "runSequence", "steps": [
  { "type": "wait", "seconds": 0.4 },
  { "type": "cameraFocus", "target": "player", "at": [0, 0.5], "size": 2.0, "seconds": 0.5 },
  { "type": "say", "actor": "player", "text": "That's a hole.", "sound": "vo_portal_hole_1" },
  { "type": "cameraShake", "intensity": 0.1, "duration": 0.3 },
  { "type": "say", "at": [3, 1.2], "anchor": "Player", "text": "Hello?", "sound": "vo_portal_hello_1", "color": "#FF9A5C" },
  { "type": "cameraReset", "duration": 0.5 },
  { "type": "beatEnd" } ] }
```

The `onSkip` list is where you write the end state: whatever the beat would have left behind. If a spawn may or may not
have happened yet, branch on a counter you set when you spawned it (Level 1: `portalUp`).

## Level 1's portal (the real one)

Phases `portalBeam` to `portalClose` in `levels/Level1.json`: `beatStart level1Portal` (skip -> `portalClose`, hero
pulled in and portal up), the laser and the Overlord's *"Mm. ... Stay."* in a bubble from above, the push-in on the hole,
the walk, *"That's a hole."*, *"Hello?"*, the hole's echo (a parallel sequence), *"Nope."*, *"Oh, come ON-"* and the pull
in, Yooba's slurp, the close, `beatEnd` and the fade: about 9 s from the first frame. The old signals (`portal:*`) are
unchanged. Playtest `beat-director` plays it uncut, again, skipped by one tap, and the bench scene (run it at 390x844).

## Adding a beat

1. Write it as one `beatStart` plus a `runSequence` (steps are timed with `wait` and `say` holds).
2. Write `onSkip` as the end state; make `maxSeconds` your real budget plus 0.5 s.
3. Keep voice lines in audio.json (`vo_*`, with the same words in `prompt` as the caption).
4. Add the beat to a playtest: tap during it, assert the end state inside 0.5 s.
