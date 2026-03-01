# Sick Of It — Architecture

## Core Philosophy

**The game IS the config.** One ScriptableObject (`GameDefinition` at `Assets/Resources/GameDefinition.asset`) defines every level, video, death scene, enemy, and player setting. Unity scenes are OUTPUT generated from this config. You edit the definition, press one button (`SickOfIt → Generate Project`), the game regenerates.

## How Scene Generation Works

The project uses **scene generators** that read from `GameDefinition` and produce fully-wired Unity scenes. The generators place Runtime components directly into the scenes with serialized references already connected — no auto-bootstrap magic, no runtime discovery hacks.

### The Generation Pipeline

```
GameDefinition.asset (THE source of truth)
       ↓
ProjectGenerator.cs ("SickOfIt → Generate Project")
       ↓ calls individual generators:
       ├── VideoSceneGenerator  → Video.unity  (or per-video scenes)
       ├── LevelSceneGenerator  → Level.unity  (or per-level scenes)
       ├── DeathSceneGenerator  → Death.unity  (or per-death scenes)
       └── AddressableGroupGenerator → Addressables config
```

### What Generators Produce

Each generator creates a scene with:
1. **SceneRoot** — has `SceneContentController` for Addressable loading state
2. **Content** — parent for all visible content
3. **Camera** — from GameDefinition camera prefab, or fallback orthographic
4. **Runtime Component** — `VideoRuntime`, `LevelRuntime`, or `DeathRuntime` with serialized refs wired via `SerializedObject`
5. **UI** — from GameDefinition UI prefab (MobileController for levels, skip button for videos/deaths)

### Level Scene Specifics
- **Character** — instantiated from `PlayerConfig.playerPrefab` (or `Assets/Prefabs/Character.prefab`)
- **Ground/Walls** — `BoxCollider2D` boundaries based on `LevelConfig.levelBounds`
- **WinTrigger** — `LevelWinTrigger` at the right edge
- **Music** — `AudioSource` with `LevelConfig.music`
- **LevelRuntime** — wired to character, spawn point, music source, mobile controller
- **StateRecorder + RewindController** — wired to player transform, rigidbody, health, camera

### Video Scene Specifics
- **VideoPlayer** — `SpriteRenderer` (SpriteFramePlayer added at runtime)
- **Audio** — `AudioSource` for video audio
- **VideoRuntime** — wired to videoPlayerObject, audioObject, uiRoot

### Death Scene Specifics
- **DeathAnimation** — `SpriteRenderer` (SpriteFramePlayer added at runtime)
- **DeathRuntime** — wired to deathAnimationObject, uiRoot

## Data Flow at Runtime

```
Player presses "New Game"
    ↓
MainMenuManager → SceneTransitionManager.LoadVideo(1)
    ↓
SceneTransitionManager:
  1. PlayerPrefs.SetInt("CurrentVideoNumber", 1)
  2. AddressablesSceneLoader.LoadScene("Video")
    ↓
Video.unity loads → VideoRuntime.Start()
    ↓
VideoRuntime:
  1. Reads PlayerPrefs["CurrentVideoNumber"] → 1
  2. Reads GameDefinition.GetVideo(1) → VideoConfig
  3. Finds videoPlayerObject (serialized ref)
  4. Adds SpriteFramePlayer, calls Initialize(config.spriteFramesData)
  5. SpriteFramePlayer loads sprite chunks via Addressables
  6. Calls spritePlayer.Play() when ready
    ↓
Video completes → SceneTransitionManager.LoadLevel(config.nextLevel)
    ↓
Level.unity loads → LevelRuntime.Start()
    ↓
LevelRuntime:
  1. Reads PlayerPrefs["CurrentLevelNumber"]
  2. Reads GameDefinition.GetLevel(N) → LevelConfig
  3. Uses serialized refs (character, music, etc.)
  4. Spawns enemies from config.enemies + enemyTemplates
  5. Spawns platforms from config.platforms
  6. Spawns barriers from camera viewport
  7. Starts ExperienceRunner for timeline events
  8. Sets up GameClock, StateRecorder, RewindController
    ↓
Player dies → SceneTransitionManager.LoadDeathScene(config.deathConfigName)
    ↓
Death.unity loads → DeathRuntime.Start()
    ↓
DeathRuntime:
  1. Reads PlayerPrefs["CurrentDeathConfig"]
  2. Reads GameDefinition.GetDeath(name)
  3. Plays death animation via SpriteFramePlayer
  4. After delay → reloads the level
```

## Assembly Structure

| Assembly | Path | Purpose |
|----------|------|---------|
| `SickOfIt.Data` | `Scripts/Data/` | GameDefinition, all config types, progression, roguelite |
| `SickOfIt.Core` | `Scripts/Core/` | GameClock, AnalyticsManager, input, device detection |
| `SickOfIt.Core.Progression` | `Scripts/Core/Progression/` | **VideoRuntime, LevelRuntime, DeathRuntime**, MainMenuManager, ExperienceRunner, StateRecorder, RewindController |
| `SickOfIt.Core.Addressables` | `Scripts/Core/Addressables/` | AddressablesSceneLoader, AddressablesOperationManager |
| `SickOfIt.Core.Flow` | `Scripts/Core/Flow/` | SceneTransitionManager, SceneContentController, SceneLoadingManager |
| `SickOfIt.Combat` | `Scripts/Combat/` | EnemyBase subclasses, health systems, projectiles, pickups |
| `SickOfIt.Tools.Editor` | `Scripts/Tools/Editor/` | ProjectGenerator, scene generators, build scripts, MCP tools |
| `SickOfIt.VideoFrameExtractor.Runtime` | `Scripts/VideoFrameExtractor/Runtime/` | SpriteFramePlayer, SpriteFramesData |

## Key Classes

### Config Layer (`SickOfIt.Data`)
- **GameDefinition** — Root SO, singleton via `Resources.Load`. Has lists of levels, videos, deaths, enemy templates, player config. Also holds UI/camera prefab refs for scene generation.
- **LevelConfig** — Level name, enemies, platforms, barriers, music, duration, death config name, timeline events
- **VideoConfig** — Video name/number, spriteFramesData, audio, afterLevel, nextLevel, returnToMainMenu
- **DeathConfig** — Death name, spriteFramesData, audio, delayBeforeReload
- **EnemyTemplate** — SO per enemy type (Chaser/Fighter/Sentinel/Shooter). Prefab, default stats, sounds, drops
- **EnemySpawnConfig** — Per-spawn: type, position, spawnTime, stat overrides
- **PlayerConfig** — Player prefab, movement params, combat params, roguelite settings, upgrades
- **PlatformConfig** — Type, prefab, position, scale, width
- **TimelineConfig** — List of timed events for ExperienceRunner

### Runtime Layer
- **VideoRuntime** — Reads VideoConfig, drives SpriteFramePlayer, handles skip/completion flow
- **LevelRuntime** — Reads LevelConfig, spawns everything dynamically, manages completion/death flow
- **DeathRuntime** — Reads DeathConfig, plays death animation, reloads level after delay
- **GameClock** — Per-scene time that can go forward/backward. Singleton (`GameClock.Instance`)
- **ExperienceRunner** — Fires timed events (spawn enemy, activate object, play sound, etc.) driven by GameClock
- **StateRecorder** — Ring buffer of game state snapshots for rewind
- **RewindController** — Triggers rewind on GameClock, restores StateRecorder snapshots
- **SpriteFramePlayer** — Loads sprite sheet chunks via Addressables, renders frames. Supports GameClock or Time.deltaTime

### Scene Loading
- **SceneTransitionManager** — DontDestroyOnLoad singleton. Sets PlayerPrefs, loads scenes via Addressables or SceneManager
- **AddressablesSceneLoader** — Loads Addressable scenes by key
- **AddressablesOperationManager** — Manages Addressable asset loading with memory cleanup
- **SceneContentController** — Shows/hides content root while Addressables load

### Combat (`SickOfIt.Combat`)
- **EnemyBase** — Base class, `InitializeFromConfig(EnemyTemplate, EnemySpawnConfig)`
- **EnemyChaser** — Follows player
- **EnemyFighter** — Alternates between approach and attack states
- **EnemySentinel** — Stationary, shoots at player
- **EnemyShooter** — Shoots in configured direction (at player, left, right)
- **PlayerHealth / EnemyHealth** — Health systems with damage, death, invulnerability frames
- **AnimationEventRelay** — Forwards animation events (like FireProjectile) from child Animator to parent EnemyShooter

## Singleton Patterns
| Class | Lifetime | Access |
|-------|----------|--------|
| GameDefinition | Persistent (Resources) | `GameDefinition.Instance` |
| SceneTransitionManager | DontDestroyOnLoad | `SceneTransitionManager.Instance` |
| GameProgressManager | DontDestroyOnLoad | `GameProgressManager.Instance` |
| RogueliteManager | DontDestroyOnLoad | `RogueliteManager.Instance` |
| AnalyticsManager | DontDestroyOnLoad | `AnalyticsManager.Instance` |
| GameClock | Per-scene | `GameClock.Instance` |

## Addressables

### Scene Addresses
| Address | Scene |
|---------|-------|
| `Video` | `Assets/Scenes/Video.unity` |
| `Level` | `Assets/Scenes/Level.unity` |
| `Death` | `Assets/Scenes/Death.unity` |

### Asset Labels
Sprite frame chunks use labels like `Video1_Part1`, `Video1_Part2`, `Level1_Background1`, etc.
SpriteFramePlayer loads them via `Addressables.LoadAssetsAsync<Texture2D>(label)`.

### Memory Management
- WebGL has tight memory limits — SpriteFramePlayer caps at 2 chunks on WebGL
- AddressablesOperationManager tracks loaded assets per scene and releases on scene change
- SpriteFramePlayer uses a sliding window: loads chunks ahead, evicts old ones

## Build & Deploy
- **Engine**: Unity 6000.3.7f1 (Unity 6.3)
- **Target**: WebGL
- **Build scripts**: `ClawBuildScript.cs` (headless CLI), `GameDeployWindow.cs` (editor GUI)
- **Website**: Docker (nginx:alpine) on EC2
- **Docker Hub**: `jgriffith3433/sickofitgame:latest`
- **Current deployed version**: v97
- **Version convention**: `Game/v{N}/` folder, find-replace all references on version bump

## Existing Prefabs
| Prefab | Path | Purpose |
|--------|------|---------|
| Character | `Prefabs/Character.prefab` | Player character |
| MobileController | `Prefabs/MobileController.prefab` | Full touch controls UI |
| Platform | `Prefabs/Platform.prefab` | Standard platform |
| Diamond | `Prefabs/Diamond.prefab` | Collectible pickup |
| Enemy_Chaser | `Prefabs/Combat/Enemy_Chaser.prefab` | Chaser enemy |
| Enemy_Fighter | `Prefabs/Combat/Enemy_Fighter.prefab` | Fighter enemy |
| Enemy_Sentinel | `Prefabs/Combat/Enemy_Sentinel.prefab` | Sentinel enemy |
| Enemy_Shooter | `Prefabs/Combat/Enemy_Shooter.prefab` | Shooter enemy (+ Left/Right variants) |
| Projectile_Enemy | `Prefabs/Combat/Projectile_Enemy.prefab` | Enemy projectile |
| Projectile_Player | `Prefabs/Combat/Projectile_Player.prefab` | Player projectile |
| Projectile_Laser | `Prefabs/Combat/Projectile_Laser.prefab` | Sentinel laser |

## Enemy Templates (ScriptableObjects)
Located at `Assets/Resources/EnemyTemplates/`:
- `Chaser.asset` — follows player, melee
- `Fighter.asset` — alternates approach/attack states
- `Sentinel.asset` — stationary, ranged (laser)
- `Shooter.asset` — shoots projectiles (configurable direction)
