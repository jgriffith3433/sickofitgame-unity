# Sick Of It — Agent Guide

Unity 6.3 (6000.3.7f1) 2D side-view arcade bullet-dodger with a rewind mechanic and a comedic tone, shipped to WebGL.
Read `ARCHITECTURE.md` for the runtime and generator design and `CONVENTIONS.md` for C# rules. This file covers how to
change the game. (`CLAUDE.md` just imports this file; edit this one.)

**Gameplay, UI and audio requests** ("make the next level", "enemies come in waves", "make chasers explode", "restyle
the menu", "add a voice line", "add a shop upgrade"): read [Docs/GameplayCookbook.md](Docs/GameplayCookbook.md) for
recipes and [Docs/GameplayReference.md](Docs/GameplayReference.md) (generated) for every content field. Most requests
are JSON-only edits; no C#, no Generate Project.

**Story first.** [Docs/Story.md](Docs/Story.md) is the game's design: the 1,000-floor climb, the cast, which enemies
live on which floors and why, how rewind/upgrades/diamonds fit the story, and the pacing rules for levels. [Docs/LevelDesign.md](Docs/LevelDesign.md)
is how a floor is built (one level = one floor, climbing up from the lava sea; the toolkit; the route through Circle X
with its shortcuts and floor bosses; a page per played floor): build floors from it. Every new
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
- **Yooba** (`ui/yooba.json`, `YoobaPortrait`): the village slime's talking portrait (idle/talk/laugh/cough sheets, a shape
  placeholder until painted) and lines on the Shaft (his hello after Video3, one reaction per climb) and in the shop. Lines
  are audio.json voices `yooba` (hero replies `player`); which line plays is `moment` + `floor` (Docs/Story.md, "Yooba between floors").
- **Shortcuts** (Docs/Story.md, "Shortcuts"): a choice point in a stage (`offerShortcut`: keep climbing or the shortcut,
  its cost a Grudge shown on the card, never how far it goes) and the `shortcutChosen` trigger to branch on. Taken, the
  stage ends on the shortcut's `toFloor` with no Grudge pick, leads to its `nextLevel` (past the skipped stages), and the
  Shaft jumps a dotted line up through a hinted crack (`ShortcutProgress`). Circle X's: the Sack Lift (Floor 991, a 20-diamond fare, `costDiamonds`) and the King's Chimney (Floor 951,
  a risk: Floor 910 reads `shortcutTaken`); Circle IX's Old Stair (`levels/Level14.json`, level 62, on Circle IX's long way), 871 to 841, level 66
  (`Level18.json`) next; Circle IX's played route has the Spared Demon (Floor 891, a Grudge, to 860) and the Hollow Spire
  (Floor 851, a risk: Floor 810 reads `shortcutTaken`). Cookbook "A shortcut".
- **Waystones and runs** (Docs/Story.md): a death on a floor stage ends the run: the death screen, then the Shaft drops the
  marker back to the highest Waystone (the Landing, Floor 1,000, then each Circle's top once its Warden is beaten) and the
  run restarts there; Grudges reset, diamonds and Yooba's goods stay. Main menu: Continue (the run) / New Run (the
  Waystone). `RunFlow`, `WaystoneProgress`; cookbook "Waystones and runs".
- **Diamonds** drop from enemy `loot` as the `diamond` entity and are spent in Yooba's shop (main menu, or **Visit Yooba** on
  the Shaft: `UpgradeShopManager.OpenOverShaft`, an overlay; Back returns to the Shaft). **Shop upgrades** are
  `player.json` `availableUpgrades` with an `effect` (MaxHealth, ExtraJump, MoveSpeed, FireRate, Damage, RewindJuice,
  Magnet); `RogueliteManager` stores levels, `UpgradeSystem.ApplyShop` applies them each level. Between-level picks are
  `content/upgrades`.
- **Aim cursor**: in a level the mouse cursor is a crosshair (`Gameplay/Presentation/AimCursor.cs`, drawn at runtime
  in theme.json `hud` colors: `marker` ring and fangs, `healthFill` dot on the hotspot, `worldBarOutline` rim; 32x32,
  `Cursor.SetCursor`, a CSS cursor in WebGL). The pause, death and results screens, the Grudge picker, HUD buttons and
  every menu outside a level keep the system arrow (`GameplayHud.UpdateCursor`).
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
- **Climb art** (the world below the lava, stones, rafts, lifts, chains, lava, the landing; Docs/LevelDesign.md "The art
  of the climb") is `Assets/Art/Climb/`, baked by `Tools/Art/climb_art.py` from `Tools/Art/climb/climb.json` (which
  Hawkes Gen takes, keying, fitting each platform's walkable top to its body). Strips (chains, lava surface/falls, the
  far sea, sky bands) are `ArtLoopDef` fields (`FloorArt`); stones switch animator states on `Sinking`/`Cracked`/`Falling`.
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

## Analytics (Hawkes Analytics)

Hawkes Gen's player analytics (`hawkesgen/docs/analytics.md`) is the only tracker; never add another (Umami is gone).
- **The bridge** is Gen's `com.hawkes.analytics`, **embedded** in `Packages/com.hawkes.analytics` because Gen's copy
  doesn't compile (raw U+2028/U+2029 in `HawkesAnalytics.cs` char literals; fixed here as `'\u2028'`/`'\u2029'`). When
  Gen ships the fix, delete the folder and go back to `"com.hawkes.analytics": "file:C:/Projects/hawkesgen/clients/unity/com.hawkes.analytics"`.
  It loads the SDK into the page itself (product `sickofit`), so the website needs no change. Editor and non-WebGL send nothing.
- **`AnalyticsManager`** (Core, static) is the only caller of the bridge: init, `window.HAWKES_BUILD` (version =
  `Application.version` + `+v<n>` from `Game/v<n>/`, commit and date from `Resources/HawkesBuildInfo.json`, which
  `Tools/Editor/AnalyticsBuildInfo.cs` writes for a player build and removes after; gitignored), the feedback card in the
  game's colors (`Plugins/WebGL/SickOfItAnalytics.jslib`: `ask` with a theme, `AskOpen`, `CloseAsk`, the playtest's
  debug-log readout) and `Recent`/`Sent` (every call, in the editor too).
- **The level's events come from the telemetry** (`Gameplay/Presentation/PlayAnalytics.cs` on `GameplayTelemetry.Recorded`):
  `levelStart` → context (level, level_name, mode story/arcade, floor, circle, grudges, phase, boss) + `level_start` (start
  floor from `floors.start`: the counter is set just after); `floor` → `progress` per new floor (step = 1001 − floor, of
  1000); `damage` on the player → the next death's `cause`; `signal` during a boss → custom `boss` phase; `upgradeOffer`/
  `upgradePick` (UpgradeSystem) → `choice` grudge (offered, taken); `shortcut`/`shortcut-declined` → `choice` shortcut.
  Direct hooks: `GameClock` rewind start/stop → `feature` rewind (seconds back, `undo_death`); `PlayerHealth.DeathStood`
  (the undo window ran out) → `death` + `level_end` death; `CommitCompletion` → `level_end` complete; pause Restart/Quit
  and the scene unloading → restart/quit; the boss bar (`ShowBossBar`/its target dying) → `boss` start/win/lose and a stars
  card on the results. One-off events are sent once per attempt (rewind replays the same future).
- **Elsewhere:** `RunFlow` (`run_start` on a session's first floor stage, `run_end` lose on a death that ends the run,
  quit on New Run/New Game; `progress` + `checkpoint` per Waystone), `VideoRuntime` (`video` play/complete/skip),
  menus (`screen`: menu, settings, level_select, video_select, shop, shaft, level, pause, results, death, video;
  `choice` main_menu / level_select / video_select / shaft), the shop (`shop_purchase`), the death screen (`rage_quit`,
  and from the 3rd death on a level in a session the thumbs card "Too hard? Not fun?"; the screen waits for it up to
  15 s, Escape goes on; a level start closes any open card, since it keeps the keyboard).
- **Rules:** no per-frame events, nothing personal, presentation only. A new moment: prefer the telemetry (add a
  `Record` kind and map it in PlayAnalytics) over a parallel hook. Check with the `analytics` playtest (it turns on the
  SDK's debug log, dies 3 times on Level 1 with a rewind, checks the card and that level_start/death/rewind/level_end
  went out with the build and env) or `?hawkes-analytics-debug=1` on any page.

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
- **Every build imports the GameConfig, always.** The committed `GameDefinition.asset` is an old compile (an LFS
  file nobody re-commits), so a build that skipped the import would ship old levels, scenes and clips with new code
  and art (beta v139). The deploy's Step 0 and the player build hook run `GameConfigSync.ImportForBuild`: a full
  import, never skipped as "unchanged", then a check that the asset matches the JSON; anything wrong stops the build.
  A good build log has `Game config verified for the build ... fingerprint <hash>`; no such line, no current data.
  To check an agent worktree the same way: `-executeMethod SickOfIt.Tools.Editor.GameConfig.GameConfigCommands.ImportForBuild`.

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
  the aim crosshair checked before, during and after the pause (`check` events `cursor <beat>`), quit to menu), `level-<n>` per level in game.json (loads the level directly, a setup shortcut; `level-1` also checks its
  ending, the portal scene, beat by beat: Grudge pick first, beam telegraphed and hitting on the background's fire hole (world point from the background layer), portal open on it (a `check` with its world x; screen captures before and after: the background's fire found, then the portal drawn over it),
  move/jump/fire/R pressed and ignored, the walk, every line's subtitle and voice, the turn, the pull-in, the slime,
  the scorch, the fade, no damage; then Continue on the results and Video2 must start; `check`, `line`, `signal` events;
  a floor (one level = one floor, Levels 2-6: Floors 1000-996) is checked like a stage, with a "FLOOR N: PLACE" banner, the
  counter ticking to the next floor on its landing and the floor's own new idea (1000 sinking stones, no foothold under a
  still lava (`lava-over-foothold` events), the jump from stone5 onto the bank and the landing reached (`foothold` events, the
  way he climbed), 999 the rising lava
  and its wave, 998 the village's wreckage and cinders, 997 sack imps on the hauling chains and an empty chain ridden,
  996 lava falls and geysers; `FinishFloorIdea`); a floor stage, Level 7 on, is checked as it plays: its "FLOORS N-M: PLACE" banner and hero lines with their voices,
  the floor counter start → end, its carved `floors.sign`, at most one new enemy type and the first one alone, a 3+ s
  breather, ≤ 3 enemies at once, a repeated floor on two signs (The Loop), 60-120 s, the first phase's lines (title,
  a milestone like Level 7's Floor 950) before the first enemy, every scripted line shown with its voice, a
  `floors.door` standing at the top with a voice other than the hero's or the announcer's heard before it (Level 10:
  the Magma King's door), the Grudge; then the Shaft's climb and what comes next; `check`, `banner`, `line` (with the
  time and speaker), `new-enemy`, `breather`, `sign`, `door` events; a Warden's arena (a floor stage on a Static
  screen, `IsArena`: Floor 901, the Slag Throne, level 31) skips the breather and takes 60-240 s, and adds the boss checks: the
  voiced intro (the throne rises, "FLOOR 901! THE WARDEN OF THE WASTES!", his taunt, the hero's line, the boss bar
  before the fight), the phases in order from the fight's `emit` signals (`mkIntro`, `mkFight`, `mkStand`,
  `mkPhase2`, `mkEnrage`, `mkFlood`, `mkDefeat`, `waystoneLit`) with his health at the stand (≤ half) and the enrage
  (≤ a third), imp guards only in phase 2, every attack's `spawn` 1+ s after its warning glow (`ArenaTelegraphs`), the
  defeat (slow motion, his last line, "HAS FALLEN"), the counter at 901 and "WAYSTONE LIT: FLOOR 901"; then the
  Shaft's Waystone saved and Circle IX waking (`climbWaystone`, `climbWakes`); `boss`, `signal`, `telegraph`,
  `attack`, `adds` events, plus the magma-king watch's checks), `full` (New Game, then
  every level in order through the real flow: results → Continue → the Shaft (Continue) → video skipped with Escape; up to 3 tries per level;
  ~10 min), `all-levels` (every level on the path in game.json's order: Level 1 then each `FloorN` level up to the first
  level that isn't one (the long way's old stages and the arcade levels aren't on it), each opened directly (setup shortcut:
  no videos or Shaft, and a level the bot can't clear never stops the run), played with a time limit (160 s of game time, a
  floor ending in 1 250 s) and once more after a death; a `level-result` event per level (CLEARED, DIED with where (phase,
  floor, place, path section) and by what (the last `damage` source on the hero), or STUCK with the stuck event's place,
  path section and the bot's ClimbTarget), then a `summary` event, one `table` event per row and the same markdown table in
  the report's `log`; metrics `path.cleared`/`died`/`stuck`, `level<n>.cleared`; ~2-3 h), `floors-A-B` (the same run over
  one stretch of the path, each ending on a floor boss or where a shortcut jumps: `floors-1000-991`, `floors-960-951`,
  `floors-910-901`, `floors-900-891`, `floors-860-851`, `floors-810-802`; the ids come from game.json, so new floors make
  new ones; the package drops unknown option keys, so a range is an id, not an option), `story-path` (New Game along the story path, each step a `check` event: Video1, Level 1, Video2, Level 2
  "Floor 1000: The Landing" with no other level between, its floor counter 1000 → 999, Video3 (Yooba) watched 8 s,
  Level 3 (Floor 999) opening on the 999 milestone, Levels 4-11 (Floors 998-991, the Imp Foreman), the Sack Lift (its
  fare topped up, a setup shortcut) to Levels 12-21 (Floors 960-951, the Pile Keeper), the King's Chimney to Levels 22-30
  (Floors 910-902, the Heat Stairs) and Level 31 (Floor 901, the Slag Throne), one after another with no video between,
  each with the stage checks below (the old 10-floor stages, levels 67-74, never played), then the
  level select, whose cards must list the story levels first and in order; ~70 min; the Shaft after Video2 and after each floor stage is checked too), `wastes-climb` (the Heat Stairs:
  Level 22, Floor 910, loaded directly, then Levels 23-31 through the real flow with the stage checks on each: Level 30
  (Floor 902, The Door) ends at the Magma King's door with his knock heard from the other side, Level 31 (Floor 901) is
  his arena with the boss checks, then the level select; ~30 min), `landing-stretch` (the first floors, one level per floor: Level 2, Floor 1000,
  loaded directly, then Levels 3-6 through the real flow, Video3 and Yooba's hello skipped after Floor 1000; each floor
  with the stage checks and its own idea, the Shaft climbing exactly one floor after each (`climbOneFloor`), then Floor
  996 leading on to Level 7; ~15 min; Docs/LevelDesign.md), `foreman-stretch` (Floors 995-991: Level 7, The Loop,
  loaded directly, then Levels 8-11 through the real flow, each with the stage checks and its own idea: 995 loop stones
  sinking by themselves in the same order lap after lap with the second lap called out, 994 both chain lifts ridden while
  imps cut the chains, 993 the throwing imps with the camera held on the hollows and the sleeper falling, 992 crumbling
  stones and the collapsing bridge, 991 the Imp Foreman's climbing arena with the floor-boss checks: voiced intro, boss
  bar, the fight's signals in order (`fmIntro`, `fmFight`, `fmClimb`, `fmPhase2`, `fmDefeat`, `fmPile`), every whip
  crack and sack ember 1+ s after its glow, at most 3 enemies, the defeat in slow motion with 20+ diamonds, 60-180 s;
  `check`, `signal`, `attack`, `boss` events; ~18 min), `circle-x-route` (Circle X's played route: first the long
  way, Floor 991 (level 11) loaded directly and the Sack Lift declined (the bot presses 1): the choice within ~10 s of the
  Foreman's defeat or the run stops there (`level11.exit`), the counter on 990, the Grudge pick, the Shaft, level 67
  starting on Floor 990 (`level11.longWay.*`; the Shaft's save put back); then Floor 991 loaded directly again with the
  Sack Lift's 20-diamond fare topped up (setup shortcut), then the real flow to Floor 901,
  the bot taking every shortcut: the Sack Lift's choice with its fare, paid, the counter jumping 990 to 960, the Shaft's
  dotted jump, Floors 960-951 (the Pile Keeper on 951), the King's Chimney (950 to 910, Floor 910 harder for it: the
  King's "Came up my chimney" line), Floors 910-902 and the Magma King on 901, his defeat lighting the Waystone; the old
  10-floor stages, levels 67-74, never played past level 67's start; `check` events `levelN.shortcut.*`; ~65 min), `shaft` (Video2 loaded directly with
  the Shaft's save reset, skipped: the first plunge past the 10 Circles to FLOOR 1,000, the welcome lines, Continue to Level 2, the
  bot plays it, then the marker's climb to 999 with floors left and the next Warden, Continue on; layout audited on both; ~8 min),
  `music` (Hell's soundtrack, Docs/Story.md "Music": the `shaft` run listening to the music, then Level 31 (Floor 901) loaded directly and fought
  until the enrage: the plunge's riser and slam, the Ember Wastes loop on Level 2 and the push music taking over in a generated push (Floor 1000 has
  no push: that check needs a generated stage),
  the climb's sting, the Magma King's theme when the fight starts and the enrage variant after `mkEnrage`; `music` events (every
  switch, from `StageMusic` telemetry and the Shaft's reports) and `check` events `music <beat>`; ~15 min),
  `menus` (level select, videos, Yooba's shop, settings open and close, then a Shaft preview with Yooba met: Visit
  Yooba opens his shop over it, Back returns to the Shaft), `layout` (main menu, level select, settings,
  a Shaft preview with Visit Yooba beside Continue, Yooba's shop over it, level 1's HUD, pause and its settings, the death scene: no button overlaps another or a HUD panel, leaves the screen or
  is under the touch size; meant for device "phone", 390x844), `analytics` (Hawkes Analytics: the SDK's debug log on, Level 1 loaded directly, R held once, then the bot dies three times;
each death screen checked, the third with the "Too hard? Not fun?" card on the page and the screen held; then level_start,
death with a cause, feature rewind and level_end checked as sent with the build version and env; `check` and `analytics`
events; ~5 min; AGENTS.md "Analytics"), `restart` (the pause menu's Restart Level: Level 2, Floor 1000, loaded directly
and recorded 1 s in, the bot plays a few seconds, P, Restart Level; 1 s into the restarted level there must be one Level
scene and a new world, and the clock, the hero's place, his hearts, the floor counter/distance and the first moving pieces
must match the fresh load; `check` and `snapshot` events; ~1 min. A same-scene reload goes through
`AddressablesSceneLoader`'s reload path: old copy unloaded first, behind an empty holder scene), `imp-death` (level 1 until the bot has killed three
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
  as long as its soundtrack; `check` and `frame` events, `video.caption@<t>` metrics), `yooba` (Yooba, the village slime: his hello on the Shaft after Video3 with his talking portrait, every hello line voiced and the hero's reply, Continue to Level 3; then Shaft previews where he reacts to Floor 950, a death and the Magma King's door; then the shop: welcome, can't afford, buy, goodbye; `check` and `yooba` events; ~4 min), `yooba-shop` (Yooba's shop from the Shaft:
  the Shaft after Video3 with his hello skipped, **Visit Yooba** beside Continue opens his shop over the Shaft (animated portrait,
  voiced welcome, slime-made goods), can't afford, buy (diamonds spent, level saved), Back returns to the Shaft, Continue starts
  Level 3 with the goods applied, the main menu's Visit Yooba shows them still bought, a climb preview shows the button again;
  `check` events `shop <beat>`; ~3 min), `waystone` (Waystones and runs: a save with the Landing's Waystone, Yooba met and
  one of his goods bought (setup shortcut), Level 3 (Floor 999) loaded directly and cleared by the bot with its Grudge pick, the climb to
  Level 4 (Floor 998), where the bot plays to lose (`PlaytestBot.reckless`: walks into the shots) until Hell kills it; the death screen
  ("YOU OBVIOUSLY DIED", the announcer's gloat), the Shaft's drop from the floor he died on to the Waystone at FLOOR 1,000 with
  the Waystones marked, the first death's voiced line and the hero's answer, not skippable; Grudges gone, diamonds and goods
  kept; Continue restarts the run at Level 2; the main menu shows Continue and New Run, and New Run starts Level 2; `check`
  events `waystone <beat>`; ~6 min; a death in `full`/`story-path`/`wastes-climb` also drops back, and their retries reopen
  the level directly), `cracked-road-route` (Circle IX's played route, Docs/LevelDesign.md section 6: Level 31 (Floor 901, the Magma King) loaded directly, then the real flow
  through Floors 900-891, the Road Breaker, the Spared Demon (890 to 860), Floors 860-851, the Carver, the Hollow Spire (850 to 810) and Floors 810-802, ending at
  the Spire Fiend's arena entrance with the counter on 801, then the level select; the stage checks on every floor, 4 enemies allowed in Circle IX; ~75 min),
  `shortcut` (Circle IX's long-way shortcut: Level 62, The Old Stair (`Level14.json`), loaded directly with one Grudge in hand (setup shortcut);
  the bot plays to the collapsed stairwell at the top and presses 2 on its choice: the choice with its cost on screen,
  the Grudge paid, the hero's line and the announcer's reaction, the counter jumping 871 to 841 with no Grudge pick; the
  Shaft climbs to 871 and jumps a dotted line to 841 (`climbShortcut14`); Continue starts Level 66 (`Level18.json`, Floors 840-831),
  skipping 35-37, played with the stage checks; `check` events `shortcut <beat>`, `shortcut-offer`/`shortcut-taken`; ~6 min),
  `climb` (the floor toolkit on a test floor: `content/scenes/climbTest.json` on Level 2's stage, setup shortcut; the bot
  climbs it through real input: the path's climb, sideways run and chase, climb again, the lava sea and parallax, a
  stone sinking and rising back, a crumbling stone falling, a raft drifting and carrying him, a chain swing, the lava
  wave chasing him, a hold of R bringing a sunk stone back up, the carved landing ending the level; `check`,
  `section`, `floorkit` events; ~3 min; Docs/LevelDesign.md "A floor's path"), `one-way` (one-way platforms:
  `content/scenes/oneWayTest.json` on Level 2's stage, setup shortcut; the bot jumps up through ledges and a stone hung
  straight over each other and lands on top, then drops down through the shelf holding S and through a sinking stone
  holding the gamepad's left stick down (its next foothold right under them), and holds down on the carved landing,
  which stays solid; `PlayerController.JumpedUpThrough`/`DroppedThrough`; `check` and `one-way` events; ~2 min; the
  move touch pulled down takes the same path but the bot has no touchscreen),
  `portal-audio` (on the main menu, plays
the prologue portal scene's sounds in scene order, every take, then the Meadow's lines; a sound without a clip is a bug), `site` (the website around the game: each page opened
  over the game checks its "Powered by HAWKES GEN" link, the landing page's Hawkes Biomedical link, and that the game
  page has neither; then the lore (the Descent, whose floor counter must read 1,000 at the bottom, and the new lore
  pages) and the engine post, linked from the blog and Development; on every page no link or button overlaps another
  or leaves the screen and nothing scrolls sideways (run it on device "phone"); `check` and `site-layout` events;
  `Plugins/WebGL/HawkesSite.jslib`).
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

**The website's lore and blog posts** live here too, in `Tools/Web/site/` (it mirrors the website's paths:
`blog/lore.html`, `blog/lore/**`, `blog/posts/the-engine.html`). The deploy (step 7d, `SiteContent`, before the
credit) copies every file there over a fresh copy of the website, so they reach the beta site with the build and go
live with the game; `site.json` inserts snippets (a post's card on the blog index and Development, a note on an old
post) into pages this repo doesn't own, between `<!-- Hawkes site: <id> -->` comments. Edit the pages here, never in
the website (the next deploy overwrites them). The lore is the Story told for visitors: change `Docs/Story.md` first,
then the page; Wardens the game hasn't revealed stay ??? (Docs/Story.md, "On the website"). `blog/lore.html` is "the
Descent" (`blog/lore/assets/descent.css`/`.js`: floor counter, Circle bands, embers, sound only after a tap, reduced
motion respected); its art is the game's own, made small by `Tools/Web/site-images.sh`. Check it without Unity:
`node Tools/Web/site-check.mjs <website checkout> [--install <dir>]` (links, anchors, inserts, banned words; `--install`
builds a local copy to look at). Never start a browser on the agents' server: J asks for the `site` playtest.

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
