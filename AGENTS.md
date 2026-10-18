# Sick Of It — Agent Guide

Unity 6.3 (6000.3.7f1) 2D side-view arcade bullet-dodger with a rewind mechanic and a comedic tone, shipped to WebGL.
Read `ARCHITECTURE.md` for the runtime and generator design and `CONVENTIONS.md` for C# rules. This file covers how to
change the game. (`CLAUDE.md` just imports this file; edit this one.)

**Gameplay, UI and audio requests** ("make the next level", "enemies come in waves", "make chasers explode", "restyle
the menu", "add a voice line", "add a shop upgrade"): read [Docs/GameplayCookbook.md](Docs/GameplayCookbook.md) for
recipes and [Docs/GameplayReference.md](Docs/GameplayReference.md) (generated) for every content field. Most requests
are JSON-only edits; no C#, no Generate Project.

**Story first.** [Docs/Story.md](Docs/Story.md) is the game's design: the 1,000-floor climb, the cast, which enemies
live on which floors and why, how rewind/upgrades/diamonds fit the story, and the pacing rules for levels. Every new
level, enemy, upgrade or voice line must fit it; if it doesn't yet, update the story first. No slime enemies (slimes
are villagers).

## The game is defined in JSON

`Assets/GameConfig/` is the **source of truth** for the whole game. It is compiled into
`Assets/Resources/GameDefinition.asset`, which the generators and the runtime read. Never hand-edit
`GameDefinition.asset`, generated prefabs, or generated scenes: they are overwritten.

```
Assets/GameConfig/
  game.json                 globals (camera, ground, rewind, UI/camera prefabs, ...) and the ordered
                            lists of item files: "levels": ["Level1", "Level2"] -> levels/Level1.json, ...
  player.json, pickup.json  one file per large section
  audio.json                every sound effect and voice line (prompts for Hawkes Gen's ElevenLabs tools) + which plays for each game event
  ui/*.json                 menus: theme.json (the look of every menu) + mainMenu, levelSelection,
                            videoSelection, upgradeShop (each has a "layout": backdrop, title, list, stickers)
  levels/ videos/ deaths/   one file per item (a level's gameplay is its "script" field)
  enemies/ projectiles/     legacy enemy/projectile configs (used by the generators and levels without a script)
  content/entities/         gameplay entities: enemies, bosses, projectiles, hazards (id = file name)
  content/sequences/        reusable action lists (boss intros, cutscene beats)
  content/effects/          status effects (burning, slow, stun, shield)
  content/upgrades/         player upgrades offered between levels (saved; reset on New Game)
  content/circles/          Circles of Hell: backdrop, track, props, enemy table, hazards, difficulty, milestones
                            (travel stages and the floor generator use them; see the cookbook)
  content/scenes/           scenes: a level script played on a level's stage instead of its own (the playtest's
                            cutscene scenario plays cutsceneTest on Level 1's; cookbook "A scene with the hero")
```

- **Order matters.** `GetLevel(n)` and `GetVideo(n)` use the position in game.json's list, not `levelNumber`.
- **Adding a level/video/death:** create the file *and* add its name to the list in game.json. Unlisted files are ignored (with a warning).
- **Adding content** (`content/**`): just create the file; no list.
- Every file starts with `"schemaVersion": 1`.
- Missing fields keep their defaults; unknown fields are errors. Exported files list every field. Comments are
  allowed but lost when the editor re-exports a file.

### Value formats

| C# type | JSON |
|---|---|
| enum | name: `"Chaser"`; `[Flags]` as `"FreezeRotation, FreezePositionX"` |
| Vector2/3 | `[x, y]` / `[x, y, z]` |
| Color | `"#RRGGBB"` / `"#RRGGBBAA"`, or `[r, g, b, a]` with 0–1 values |
| Rect | `{"x", "y", "width", "height"}` |
| asset (AudioClip, Sprite, prefab, SpriteFramesData, ...) | path under `Assets/`: `"Videos/Video1_Audio.mp3"`; a sprite sliced from a sheet: `"Images/Diamond.png#Diamond_0"`; or `null` |
| ability / trigger / action / pattern | object with `"type"`: `{ "type": "chase", "stopDistance": 0.5 }` |

## How gameplay runs

- `LevelRuntime` creates a `GameplayWorld` per level. If the level has a `script`, the `LevelDirector` runs it
  (phases, rules, hazards, sequences, objective) and the legacy `enemies` list and win rules are ignored.
- Entities are built at runtime from JSON (`Assets/Scripts/Gameplay/`), pooled, and ticked in spawn order from
  FixedUpdate. Art comes from `visual.animator` sprite sheets; without art they draw as labeled placeholders.
- **Rewind is automatic**: entity and director state is saved through `SimHistory`; random numbers come from seeded
  per-entity streams, so a rewind replays the same future. New C# abilities/triggers must save every runtime field in
  `SaveState`/`LoadState` (`SaveStateCoversEveryRuntimeField` checks this).
- Content can reference only assets that some config uses (the importer builds `GameDefinition.assetRegistry`).
- **Arcade loop**: rewind is limited by juice (`game.json` `rewindBudget`, `RewindBudget`), refilled by `rewindOrb` drops;
  the HUD progress bar shows level progress and juice; levels end with `offerUpgrades` (see the cookbook). Rewards are
  paid once per play via `GameplayWorld.TryGrantOnce`, never re-paid by a rewind.
- `GameDefinition.remoteContentUrl` (game.json `remoteContentUrl`) + **Build Remote Content Bundle** ships content and
  level-script changes without a rebuild.

## Menus, HUD, sound and feel (all config-driven)

- **Menus and HUD are built at runtime** by `MenuCanvas` (`Assets/Scripts/Core/UI/Menus/`): main menu, level/video
  select, shop, pause, settings, results, death overlay and the in-level HUD (health, diamonds, rewind button, boss bar,
  progress/rewind bar, upgrade picker). The look is `ui/theme.json` (styles, `hud` colors, every on-screen word in
  `texts`, results `grades`); placement is each screen's `layout`. One responsive system places all of it
  (`MenuResponsive`/`MenuListLayout`, numbers in theme.json `responsive` and `hud.landscape`/`hud.portrait`): scaled on
  the short side, kept in the safe area, portrait placements that stack or wrap, thumb-sized buttons in portrait
  (cookbook: "Phones, portrait and touch"). Place new UI through `MenuCanvas.Add*` or `MenuResponsive.Track`, never at
  raw pixel positions, or the `layout` playtest flags it. Generators only build the scene shell; restyling
  never needs Generate Project. Don't add prefab buttons to scenes.
- **Sound is `audio.json`**: every effect and voice line is a prompt plus a `clip` filled in by the generator
  (SickOfIt > Audio > Sound Generator, or the `GenerateSounds` batch command), which makes them through Hawkes Gen's
  `audio.sfx` / `audio.voice` (ElevenLabs). `events` maps game moments to
  sound ids. `voices` defines who speaks (`player` for subtitles, `announcer` for banners); lines use `eleven_v3` with
  delivery tags like `[sighs]`. A `showText` voices its line through `"sound"`. Prompting rules are in the cookbook;
  the API rejects effects under 0.5 s. This project holds no ElevenLabs key: Hawkes Gen does.
- **Diamonds** drop from enemy `loot` as the `diamond` entity and are spent in the shop. **Shop upgrades** are
  `player.json` `availableUpgrades` with an `effect` (MaxHealth, ExtraJump, MoveSpeed, FireRate, Damage, RewindJuice,
  Magnet); `RogueliteManager` stores levels, `UpgradeSystem.ApplyShop` applies them each level. Between-level picks are
  `content/upgrades`.
- **Feel**: hit pops/sparks/dust (`Core/Fx`), red flash + shake + hit-stop on damage, player blink during invincibility,
  pause (Esc/P/Start), settings (Sound on/off, volumes, shake, hints in `GameSettings`; Sound/Music/Effects are the
  Hawkes standard `hawkes.sound` JSON `{ on, music, sfx }` in PlayerPrefs), "undo your death" countdown
  (`player.deathRewindWindow`), Rage Quit on the death scene. All presentation; none of it touches the simulation.
  Rewind is **hold R** (`RewindController`) or the HUD's ◄◄ button (`RewindInput`); a script's `lockRewind` sets
  `RewindInput.Locked` (both refuse, the button says so, the bot doesn't try). The input asset also binds R to an
  unused "DashRight" action; don't give R another job.
- **Enemy art** comes from the art pipeline: a spec in `Assets/GameConfig/art/<id>.json` (description, poses, clips)
  and `node Tools/Art/art.mjs <id> --start`, which generates poses and clips on the ComfyUI GPU server, packs sheets
  into `Assets/Art/Enemies/` and writes the entity's `visual.animator`. Read [Docs/ArtPipeline.md](Docs/ArtPipeline.md).
- **Videos, deaths and level/menu backgrounds** are baked sprite-sheet sequences played by `SpriteFramePlayer`, not
  Unity video. Anything that moves over them (the hero falling down Video2's tunnel) is a keyframed
  `timeline.overlays` entry, sampled from the clock so rewind plays it backward (cookbook: "Something moving over a video").
  `playbackFPS` is both bake and playback rate (change it, then re-bake); sizes are fixed per frame,
  so never fix a video's size with the camera. Read [Docs/VideoPlayback.md](Docs/VideoPlayback.md) before touching them.
- **Presentation code must stay out of the simulation**: sounds, effects and UI may read sim state but never change it,
  and use `UnityEngine.Random`/unscaled time only for cosmetics.
- When the editor is open, Unity compiles on focus; check the console and `Logs/GameConfig.log`. Some Unity quirks bit
  this code before: use `TryGetComponent` (not `GetComponent() ?? AddComponent()`, which fails on Unity's fake-null),
  and don't set a TextMesh Pro outline on a text that has never been enabled. Scenes load additively, so runtime UI
  must be created in its owner's scene (`MenuCanvas.Create(..., owner)`), not the active one. There is exactly one
  EventSystem, owned by `EventSystemGuard` (it switches off the ones in scenes and the Camera prefab); call
  `EventSystemGuard.Ensure()` instead of creating one.

## Adding a new mechanic (C#)

When a request can't be expressed with the existing types, add one:
1. A class in `Assets/Scripts/Gameplay/Content/` deriving from `Ability`, `Trigger`, `GameAction`, or `Pattern`, with
   `[Serializable, JsonType("name")]` and a `[Doc("...")]` (a test requires it). Public fields are its JSON config.
2. Mark id fields with `[EntityRef]`, `[SequenceRef]`, `[EffectRef]`, `[PhaseRef]` so the validator checks them; `[Required]` for must-set fields.
3. Save every private runtime field in `SaveState`/`LoadState`. Use `Owner.Random` / `context.Random`, never `UnityEngine.Random`, for gameplay decisions.
4. Run the tests; the reference doc regenerates on the next import.

## Checking your changes

Every import/validate/export writes **`Logs/GameConfig.log`**: errors and warnings with file, line, JSON path,
and a suggested fix ("Did you mean ...?"). Read it after every change.

- **Editor open:** JSON edits are imported when Unity refreshes (on focus), and again before Generate, Play, and
  builds. An import with errors changes nothing, and Generate/Play/build refuse to run. In Play mode a successful
  import restarts the current level with the new content.
- **Editor closed:** run commands in batch mode (exit code 0 = OK, 1 = config errors, 2 = crash):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Projects/sickofitgame-unity" -logFile - -executeMethod SickOfIt.Tools.Editor.GameConfig.GameConfigCommands.Validate
```

Methods: `Validate` (check only), `Import` (JSON -> GameDefinition), `Export` (GameDefinition -> JSON),
`VerifyRoundTrip`, `GenerateReference`, `BuildContentBundle`, `ConvertLegacy`. Sounds:
`-executeMethod SickOfIt.Tools.Editor.Audio.SoundGeneratorWindow.GenerateSounds` makes every missing/outdated sound in
audio.json through Hawkes Gen (start it first: `C:\Projects\hawkesgen\auto-start.bat`); add `-sounds id1,id2` to make
just those (even if up to date). A sound whose exact settings Hawkes Gen already made is downloaded from its library
instead of generated again. Batch mode fails with "another Unity instance is running" while the editor is open.

- **A failed build server build:** the `FSBTool ERROR ... FailedDownload.wav` line it leads with is in every build
  (a sound inside the `com.unity.ai.generators` package) and never the cause. "The game config has errors" is: the
  job's Unity log has the real line right after `Game config import FAILED` (file + JSON path). The log's `Date:` line
  says when the build ran; check that against the last merge before fixing something beta already has.

- **Tests:** `-runTests -testPlatform EditMode` and `-runTests -testPlatform PlayMode` (add `-testResults <file>`).
- **Playtest every level with a bot** (real Level scene, ~1 min per level; omit `-nographics`):
  `-runTests -testPlatform PlayMode -testCategory Playtest -testResults Logs/Playtest/results.xml`, then read
  `Logs/Playtest/report.md` and the per-level `*.summary.json` / `*.jsonl` (kills, damage taken by source, phases, outcome).
  Every real play in the editor also writes a log there.
- **Compile check without Unity:** `dotnet build` an SDK-style csproj that reuses the HintPaths from the
  Unity-generated `*.csproj` files (they reference the editor's DLLs and `Library/ScriptAssemblies`).

## Playtests (Hawkes playtest contract v1)

Every build, WebGL included, exposes the bot to the Hawkes Studio daily playtest runner as `window.hawkesPlaytest`
(spec: `C:\Projects\hawkesstudio\docs\playtest.md`; package `com.hawkes.playtest` in `Packages/manifest.json`).
`Assets/Scripts/Playtest/SickOfItPlaytest.cs` registers the scenarios at startup; nothing runs until one starts.

- **Scenarios:** `smoke` (main menu, New Game, skip the video, level 1 for ~30 s with one rewind and a P pause/resume,
  quit to menu), `level-<n>` per level in game.json (loads the level directly, a setup shortcut; `level-1` also checks its
  ending, the portal scene, beat by beat: Grudge pick first, beam telegraphed and hitting beside the hero, portal open,
  move/jump/fire/R pressed and ignored, the walk, every line's subtitle and voice, the turn, the pull-in, the slime,
  the scorch, the fade, no damage; then Continue on the results and Video2 must start; `check`, `line`, `signal` events;
  a floor stage, Level 2 on, is checked as it plays: its "FLOORS N-M: PLACE" banner and hero lines with their voices,
  the floor counter start → end, its carved `floors.sign`, at most one new enemy type and the first one alone, a 3+ s
  breather, ≤ 3 enemies at once, a repeated floor on two signs (The Loop), 60-120 s, the first phase's lines (title,
  a milestone like Level 7's Floor 950) before the first enemy, every scripted line shown with its voice, a
  `floors.door` standing at the top with a voice other than the hero's or the announcer's heard before it (Level 10:
  the Magma King's door), the Grudge; then the Shaft's climb and what comes next; `check`, `banner`, `line` (with the
  time and speaker), `new-enemy`, `breather`, `sign`, `door` events), `full` (New Game, then
  every level in order through the real flow: results → Continue → the Shaft (Continue) → video skipped with Escape; up to 3 tries per level;
  ~10 min), `story-path` (New Game along the story path, each step a `check` event: Video1, Level 1, Video2, Level 2
  "Floors 1000-991: The Landing" with no other level between, its floor counter 1000 → 991 and the 999 milestone,
  Video3 (Gloob) watched 8 s, then Levels 3-10 (The Spill, Imp Run, The Slag Falls, The Loop, Halfway Basin, The Crust,
  The Sack Pile, The Heat Stairs) one after another with no video between, each with the stage checks below, then the
  level select, whose cards must list the story levels first and in order; ~35 min; the Shaft after Video2 and after each floor stage is checked too), `shaft` (Video2 loaded directly with
  the Shaft's save reset, skipped: the first plunge past the 10 Circles to FLOOR 1,000, the welcome lines, Continue to Level 2, the
  bot plays it, then the marker's climb to 991 with floors left and the next Warden, Continue on; layout audited on both; ~8 min),
  `menus` (level select, videos, shop, settings open and close, then a Shaft preview), `layout` (main menu, level select, settings,
  a Shaft preview, level 1's HUD, pause and its settings, the death scene: no button overlaps another or a HUD panel, leaves the screen or
  is under the touch size; meant for device "phone", 390x844), `imp-death` (level 1 until the bot has killed three
imps: each imp's Die clip must be playing where it died, and those three play at 0.3x so a recording shows the frames;
every other scenario checks imp kills the same way, without the slow motion), `cutscene` (the scene
  `content/scenes/cutsceneTest.json` on Level 1's stage: with input and rewind locked the hero walks to a point by
  himself and stops, turns, R does nothing, the camera zooms in and back, the screen fades to black and back, and he is
  pulled into a point spinning and shrinking until he is gone; each beat is a `check` event), `video-2` (Video2 loaded
  directly and watched, not skipped: the hero's `heroFall` overlay appears at the burning door, spins 3+ turns and shrinks
  into the tunnel and is gone by ~14.5 s; R held mid-spin turns it back; Escape skips; `overlay` and `check` events),
  `video-2-tunnel` (Video2 loaded directly and watched with no input from the burning door through the fire tunnel to the
  lava river, 10.6–17.5 s: at each mark a screen capture of the box the old burned-in "COMING SOON" caption covered must
  not be caption-coloured, the frame on screen must match the clock and the soundtrack the clock, and the clip must be
  as long as its soundtrack; `check` and `frame` events, `video.caption@<t>` metrics), `portal-audio` (on the main menu, plays
the prologue portal scene's sounds in scene order, every take, then the Meadow's lines; a sound without a clip is a bug), `site` (the website around the game: each page opened
  over the game checks its "Powered by HAWKES GEN" link, the landing page's Hawkes Biomedical link, and that the game
  page has neither; `Plugins/WebGL/HawkesSite.jslib`).
- **Start one:** in a browser `?playtest=smoke` (plus `&seed=`, `&speed=`), or `hawkesPlaytest.start("level-2")`; in the
  editor or a test, `HawkesPlaytest.StartScenario("level-2")`. Note: `smoke` and `full` press New Game, which resets
  the save in that browser.
- **The bot plays through real input.** `PlaytestBot` (Gameplay/Presentation) decides; `PlaytestInput` adds a virtual
  keyboard, gamepad and mouse to the Input System: A/D move, Space jump, gamepad right stick aims and fires, hold R
  rewinds, P pauses, 1 picks an upgrade, Escape skips videos, and menu buttons are clicked with the mouse. Never give
  it a shortcut that edits sim state. If a new control is added, bind it in the input asset and have the bot press it.
- **Report:** metrics (kills, `damageTaken.<source>`, deaths, rewinds, `botRewinds`, diamonds, `level<n>.seconds`,
  floor, fps); events (phases, level-start/level-end, death, rewind, pause, clicks); problems (placeholder art,
  missing sounds from `[Sfx]` warnings, softlocks when the game clock stalls 15 s, GameConfig issues in the loaded
  content, level-script errors, any logged error or exception).
- **PlayMode playtests** (`-testCategory Playtest`, above) run the same `level-<n>` scenario with the editor's scene
  loader (`SickOfItPlaytest.LoadLevel`) and also write `Logs/Playtest/level-<n>.report.json` (what the runner would get).
  The editor drops keyboard/mouse input when the Game view isn't focused (always, in batch mode), so the test sets
  `InputSystem.settings.editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` while it runs; starting a
  scenario by hand in the editor, keep the Game view focused.
- WebGL builds come from the build server; keep everything in `Assets/Scripts/Playtest/` runtime-only (no `UnityEditor`).

**The "Tap to play" sound prompt** (the Hawkes standard: every game page asks for one tap, which unlocks audio on
iOS/Android/desktop) is `Tools/Web/hawkes-sound.js`. Edit it here, not in the website: the deploy (step 7b) copies it
to `Game/v<n>/` and puts its `<script>` first in the website's `Game/index.html` `<head>`, because it must wrap
`window.AudioContext`/`Audio` before Unity's loader runs (Unity's `WEBAudio.audioContext` is private to the
framework). Never make an AudioContext for Unity or hand it one that is already running: Unity only marks its audio
as running when its own context *changes* to "running" (`onstatechange` in `_JS_Sound_Init`), so every sound would
stay queued and the game would be silent. Track the context Unity makes and resume it. Every playtest scenario presses it first (`hawkesSound.tap()` through `Plugins/WebGL/HawkesSound.jslib`);
a scripted press can't unlock real audio, so a playtest's "audio" state stays "suspended" unless the runner clicked.
Because it runs first, it also stops the website's "reload on `controllerchange`" the first time the site's service
worker claims a page that had none: on a first visit that claim can come a minute in and reloaded the player out of the
level (and killed the playtest run). Updates on an already-controlled page still reload.

**The "Powered by HAWKES GEN" mark** on every website page except the game (and the Hawkes Biomedical link on the
landing page) is written by the deploy too (step 7c, `GameDeployWindow.InstallCredit`), into a fresh copy of the
website's main, between `<!-- Hawkes credit: ... -->` comments. Change its words or look there, not in the website: an
edit in the website is replaced on the next deploy. New site pages get it automatically.

**Deploy in batch mode** (the build server): `-batchmode -projectPath C:/Projects/sickofitgame-unity -executeMethod
SickOfIt.Tools.Editor.GameDeployWindow.FullDeployBatch` runs Tools > Run Full Deploy without a window (Addressables, new
`sickofitgamewebsite/Game/v<n>`, WebGL build, version replaced in the site, old version removed; no dev server), logs
each step as `[GameDeploy]`, and exits 0/1. `BUILD_VERSION=<n>` forces the version number. Committing and pushing the
site is still a separate, asked-for step.

## How the sync works

`Assets/Scripts/Tools/Editor/GameConfig/GameConfigSync.cs`:
- JSON changed -> imported into GameDefinition.asset (content validated, asset registry rebuilt, reference doc regenerated).
- GameDefinition saved by an editor tool or the inspector (generators, sprite bakers, GameOverviewWindow) -> exported to JSON.
- Both changed -> the JSON wins and the report says so.

Serialization rules live in `Assets/Scripts/Json/` (runtime assembly `SickOfIt.Json`, no editor dependency) and
mirror Unity's: public or `[SerializeField]` fields, `[Serializable]` classes, `List<T>`/`T[]`, plus `"type"`-tagged
polymorphic content types. A field type the encoder doesn't support fails loudly.

## Changing the format

- Renaming/removing a config field, or changing what a value means, breaks existing JSON. Add a step to
  `GameConfigMigrations.Steps` and bump `CurrentVersion`; old files are upgraded on import and rewritten.
- Adding a field with a sensible default needs no migration.
- To split a new section into its own file or folder, edit `GameConfigLayout`.

## Rules

- Change gameplay through `Assets/GameConfig/`. Change *capabilities* (new mechanics) in C#, then expose them as config.
- After C# changes to generators, regenerate (`SickOfIt > Generate Project`) and check the console.
- Keep everything rewind-compatible (see "How gameplay runs").
- Don't touch `Library/`, `Temp/`, `obj/`. Keep `.meta` files with their assets.

## Generative AI: Hawkes Gen

Images, textures, video, 3D, music, voice, sound and Claude all go through **Hawkes Gen**
(`C:\Projects\hawkesgen`, dashboard http://127.0.0.1:8790; start it with `hawkesgen\auto-start.bat`). It holds
the API and AWS keys and runs the shared GPU fleet. This project never holds those keys.

- **Agents:** the `hawkesgen` MCP server is in this folder's `.mcp.json`. Use `hawkesgen_presets` (project
  `sickofit`) for the art direction, then `hawkesgen_run`.
- **Command line:** `C:\Projects\hawkesgen\hawkesgen.cmd run - --project sickofit --preset <name> --param prompt="..." --out <folder>`
- **Art direction:** `hawkesgen/presets/sickofit.json` (style line and named presets). Edit it there.
- **Unity:** Window > Hawkes Gen (package `com.hawkes.gen` in `Packages/manifest.json`) runs any tool or preset and imports the results into `Assets/HawkesGen/`.
- Enemy art: `Tools/Art/art.mjs` calls Hawkes Gen (Docs/ArtPipeline.md). Sounds in `audio.json`: the Sound Generator
  (`Assets/Scripts/Tools/Editor/Audio/`, `HawkesGenAudio.cs`) uses `audio.sfx`, `audio.voice`, `audio.voice-design` and
  `audio.voice-save` through `com.hawkes.gen`, and `recipes/check` to reuse files it already made.
- GPU tools cost money while a server runs. Ask J before starting servers or running big batches.

## Making content (agents)

Enemies, stages and lines are made end to end by agents with the skills in `.claude/skills/` (Claude Code loads them
by themselves): `soi-enemy` (story → entity → art spec → `Tools/Art/art.mjs` → a level that introduces it → validate →
bot playtest), `soi-level` (a stretch of floors in a Circle, travel script, beats, lines), `soi-voice-music`. Story.md
comes first, GPU work is priced (`hawkesgen_estimate`) and needs J's OK, and the content playtest is the level's
`level-<n>` scenario (`.hawkes/scenario.txt`).
