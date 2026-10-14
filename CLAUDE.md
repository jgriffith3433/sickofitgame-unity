# Sick Of It — Agent Guide

Unity 6.3 (6000.3.7f1) 2D side-view action game, shipped to WebGL. Read `ARCHITECTURE.md` for the runtime and
generator design and `CONVENTIONS.md` for C# rules. This file covers how to change the game.

**Gameplay requests** ("make the next level", "enemies come in waves", "make chasers explode"): read
[Docs/GameplayCookbook.md](Docs/GameplayCookbook.md) for recipes and [Docs/GameplayReference.md](Docs/GameplayReference.md)
(generated) for every field. Most requests are JSON-only edits; no C#, no Generate Project.

## The game is defined in JSON

`Assets/GameConfig/` is the **source of truth** for the whole game. It is compiled into
`Assets/Resources/GameDefinition.asset`, which the generators and the runtime read. Never hand-edit
`GameDefinition.asset`, generated prefabs, or generated scenes: they are overwritten.

```
Assets/GameConfig/
  game.json                 globals (camera, ground, rewind, UI/camera prefabs, ...) and the ordered
                            lists of item files: "levels": ["Level1", "Level2"] -> levels/Level1.json, ...
  player.json, pickup.json  one file per large section
  audio.json                every sound effect and voice line (ElevenLabs prompts) + which plays for each game event
  ui/*.json                 menus: theme.json (the look of every menu) + mainMenu, levelSelection,
                            videoSelection, upgradeShop (each has a "layout": backdrop, title, list, stickers)
  levels/ videos/ deaths/   one file per item (a level's gameplay is its "script" field)
  enemies/ projectiles/     legacy enemy/projectile configs (used by the generators and levels without a script)
  content/entities/         gameplay entities: enemies, bosses, projectiles, hazards (id = file name)
  content/sequences/        reusable action lists (boss intros, cutscene beats)
  content/effects/          status effects (burning, slow, stun, shield)
  content/upgrades/         player upgrades offered between levels (saved; reset on New Game)
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
audio.json with ElevenLabs (needs `ELEVENLABS_API_KEY`; never put the key in the repo). Batch mode fails with "another Unity
instance is running" while the editor is open.

- **Tests:** `-runTests -testPlatform EditMode` and `-runTests -testPlatform PlayMode` (add `-testResults <file>`).
- **Playtest every level with a bot** (real Level scene, ~1 min per level; omit `-nographics`):
  `-runTests -testPlatform PlayMode -testCategory Playtest -testResults Logs/Playtest/results.xml`, then read
  `Logs/Playtest/report.md` and the per-level `*.summary.json` / `*.jsonl` (kills, damage taken by source, phases, outcome).
  Every real play in the editor also writes a log there.
- **Compile check without Unity:** `dotnet build` an SDK-style csproj that reuses the HintPaths from the
  Unity-generated `*.csproj` files (they reference the editor's DLLs and `Library/ScriptAssemblies`).

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
