# Sick Of It — Architecture

## Core Philosophy

**The game IS the config.** One ScriptableObject (`GameDefinition` at `Assets/Resources/GameDefinition.asset`) defines every level, video, death scene, enemy, projectile, and player setting. Unity scenes AND prefabs are OUTPUT generated from this config. You edit the definition, press one button (`SickOfIt → Generate Project`), the entire game regenerates.

**The definition is authored as JSON** in `Assets/GameConfig/` and compiled into `GameDefinition.asset` automatically (see CLAUDE.md "The game is defined in JSON").

## The Generation Pipeline

```
GameDefinition.asset (THE source of truth)
       ↓
ProjectGenerator.cs ("SickOfIt → Generate Project")
       │
       ├── Phase 1: Cleanup
       │   └── Delete all generated prefabs and scenes
       │
       ├── Phase 2: Prefab Generation (order matters)
       │   ├── ProjectilePrefabGenerator → Projectile_Player, Projectile_Enemy, Projectile_Laser
       │   ├── PlayerPrefabGenerator     → Character.prefab
       │   └── EnemyPrefabGenerator      → Enemy_Chaser, Enemy_Fighter, Enemy_Sentinel, Enemy_Shooter
       │
       ├── Phase 3: Scene Generation
       │   ├── VideoSceneGenerator       → Video.unity
       │   ├── LevelSceneGenerator       → Level.unity
       │   └── DeathSceneGenerator       → Death.unity
       │
       └── Phase 4: Addressables Setup
```

## What Gets Generated

### Prefabs (Phase 2)
| Generator | Output | Components Added |
|-----------|--------|------------------|
| ProjectilePrefabGenerator | `Projectile_*.prefab` | SpriteRenderer, Rigidbody2D, CircleCollider2D, DamageDealer |
| PlayerPrefabGenerator | `Character.prefab` | PlayerController, PlayerHealth, Rigidbody2D, ColliderCircle2D, Graphics+Shadow children, Rewind components |
| EnemyPrefabGenerator | `Enemy_*.prefab` | SpriteRenderer, Animator, Rigidbody2D, CircleCollider2D, EnemyHealth, EnemyBase subclass, Rewind components |

All prefabs include **rewind components**: RewindableTransform, RewindableRigidbody2D, RewindableGameObject, RewindableHealth.

### Scenes (Phase 3)
Each scene has:
1. **SceneRoot** — has `SceneContentController` for Addressable loading state
2. **Content** — parent for all visible content
3. **Camera** — from GameDefinition camera prefab, or fallback orthographic
4. **Runtime Component** — `VideoRuntime`, `LevelRuntime`, or `DeathRuntime` with serialized refs wired via `SerializedObject`
5. **UI** — from GameDefinition UI prefab (MobileController for levels, skip button for videos/deaths)

### Level Scene Specifics
- **Character** — instantiated from generated `PlayerConfig.playerPrefab`
- **Ground/Walls** — `BoxCollider2D` boundaries based on `LevelConfig.levelBounds`
- **Win Condition** — All enemies spawned and killed (tracked by LevelRuntime counters)
- **Music** — `AudioSource` with `LevelConfig.music`
- **Projectile Pools** — `ObjectPool` components: PlayerProjectilePool, EnemyProjectilePool, LaserPool
- **LevelRuntime** — wired to character, spawn point, music source, pools, HealthUI, mobile controller
- **RewindTimeline + RewindController** — component-based rewind system (see Rewind System section)

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
  4. Spawns enemies from config.enemies + EnemyTypeConfigs
  5. Spawns platforms from config.platforms
  6. Spawns barriers from camera viewport
  7. Starts ExperienceRunner for timeline events
  8. Sets up GameClock, RewindTimeline, RewindController
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
| `SickOfIt.Core.Progression` | `Scripts/Core/Progression/` | **VideoRuntime, LevelRuntime, DeathRuntime**, MainMenuManager, ExperienceRunner, RewindController |
| `SickOfIt.Core.Addressables` | `Scripts/Core/Addressables/` | AddressablesSceneLoader, AddressablesOperationManager |
| `SickOfIt.Core.Flow` | `Scripts/Core/Flow/` | SceneTransitionManager, SceneContentController, SceneLoadingManager |
| `SickOfIt.Combat` | `Scripts/Combat/` | EnemyBase subclasses, health systems, projectiles, pickups |
| `SickOfIt.Tools.Editor` | `Scripts/Tools/Editor/` | ProjectGenerator, scene generators, build scripts, MCP tools |
| `SickOfIt.VideoFrameExtractor.Runtime` | `Scripts/VideoFrameExtractor/Runtime/` | SpriteFramePlayer, SpriteFramesData |
| `SickOfIt.Json` | `Scripts/Json/` | Unity-compatible JSON serializer (strict reading, `"type"` polymorphism, raw JSON fields) |
| `SickOfIt.Gameplay` | `Scripts/Gameplay/` | Data-driven gameplay: content model, entities, level director, HUD, telemetry, playtest bot |

## Gameplay Layer (data-driven)

Levels with a `script` (levels/{name}.json) and everything in `Assets/GameConfig/content/` run on this layer;
levels without a script still use LevelRuntime's enemy list and the `Combat/Enemies` classes.
Authoring guide: CLAUDE.md, Docs/GameplayCookbook.md, Docs/GameplayReference.md (generated).

```
LevelRuntime ──creates──▶ GameplayWorld (per level, FixedUpdate)
                            ├── LevelDirector  phases · rules (trigger → actions) · sequences · hazards · objective
                            ├── Entity × N     pooled; abilities + optional state machine + EntityAnimation
                            ├── Entity(player) wraps PlayerController/PlayerHealth for statuses, modifiers, targeting
                            ├── GameplayHud    banner/subtitle/objective/boss bar/flash
                            ├── WorldEffects   camera (static/follow/scroll) · shake · slow motion · audio
                            └── GameplayTelemetry → Logs/Playtest/*.jsonl + *.summary.json
GameContent (parsed JSON, from GameDefinition.content + level scripts, or a remote bundle)
```

- **Content model** (`Gameplay/Content/`): `EntityDef`, `Ability`, `Trigger`, `GameAction`, `Pattern`, `LevelScript`,
  `SequenceDef`, `StatusEffectDef`. Polymorphic classes are picked by `"type"` (`[JsonType]`). Abilities and triggers are
  cloned per use; their private fields are runtime state.
- **Determinism and rewind**: the world ticks the director, then entities in spawn order. Every decision draws from a
  seeded `DeterministicRandom` (per entity, derived from the level seed and spawn serial). Entity, director, world, HUD,
  and camera state are snapshotted by `SimHistory` (an `IRewindable`); positions by the existing Rewindable* components.
  Entities spawned after a rewind's branch point are reclaimed by their `ObjectPool` and re-simulated, so the replayed
  future matches. Physics itself isn't bit-exact across a replay; decisions are.
- **Assets at runtime**: content refers to assets by path; the importer records them in `GameDefinition.assetRegistry`,
  which `RegistryAssetResolver` reads. `link.xml` keeps the reflection-created types from being stripped in WebGL builds.
- **Validation**: `GameConfigImporter` parses content and scripts with line numbers, `ContentValidator` checks references
  ([EntityRef], [PhaseRef], ...), and errors block import, Generate, Play, and builds.

## Key Classes

### Config Layer (`SickOfIt.Data`)
- **GameDefinition** — Root SO, singleton via `Resources.Load`. Has lists of levels, videos, deaths, enemy templates, player config. Also holds UI/camera prefab refs for scene generation.
- **LevelConfig** — Level name, enemies, platforms, barriers, music, duration, death config name, timeline events
- **VideoConfig** — Video name/number, spriteFramesData, audio, afterLevel, nextLevel, returnToMainMenu
- **DeathConfig** — Death name, spriteFramesData, audio, delayBeforeReload
- **EnemyTypeConfig** — SO per enemy type (Chaser/Fighter/Sentinel/Shooter). Prefab, default stats, sounds, drops
- **EnemySpawnConfig** — Per-spawn: type, position, spawnTime, stat overrides
- **PlayerConfig** — Player prefab, movement params, combat params, roguelite settings, upgrades
- **PlatformConfig** — Type, prefab, position, scale, width
- **TimelineConfig** — List of timed events for ExperienceRunner

### Runtime Layer
- **VideoRuntime** — Reads VideoConfig, drives SpriteFramePlayer, handles skip/completion flow
- **LevelRuntime** — Reads LevelConfig, spawns everything dynamically, manages completion/death flow
- **DeathRuntime** — Reads DeathConfig, plays death animation, reloads level after delay
- **GameClock** — Per-scene time that can go forward/backward. Singleton (`GameClock.Instance`)
- **ExperienceRunner** — Fires timed events (spawn enemy, activate object, play sound, etc.) driven by GameClock. Player/music refs injected by LevelRuntime.
- **RewindTimeline** — Per-scene singleton. Registers all IRewindable components, 2-phase restore (GameObjects first), pool-based projectile cleanup
- **RewindController** — Triggers rewind on GameClock; RewindTimeline restores component snapshots
- **ObjectPool** — MonoBehaviour-based reusable object pool. Self-registers with RewindTimeline. ReturnAll() on rewind.
- **PlayerController** — 2D player controller (renamed from TopDownController), GameClock-aware firing, uses ObjectPool for projectiles
- **SpriteFramePlayer** — Loads sprite sheet chunks via Addressables, renders frames. Supports GameClock or Time.deltaTime

### Scene Loading
- **SceneTransitionManager** — DontDestroyOnLoad singleton. Sets PlayerPrefs, loads scenes via Addressables or SceneManager
- **AddressablesSceneLoader** — Loads Addressable scenes by key
- **AddressablesOperationManager** — Manages Addressable asset loading with memory cleanup
- **SceneContentController** — Shows/hides content root while Addressables load

### Combat (`SickOfIt.Combat`)
- **EnemyBase** — Base class, `InitializeFromConfig(EnemyTypeConfig, EnemySpawnConfig)`, `SetPlayer(Transform, IDamageable)` for injected player ref
- **EnemyChaser** — Follows player
- **EnemyFighter** — Alternates between approach and attack states
- **EnemySentinel** — Stationary, crouches/shoots lasers from ObjectPool
- **EnemyShooter** — Shoots projectiles from ObjectPool (configurable direction)
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
| RewindTimeline | Per-scene | `RewindTimeline.Instance` |

## Rewind System (Component-Based)

The rewind system is fully decentralized. Each rewindable aspect of an object is a separate component that records its own snapshots into a circular buffer. `RewindTimeline` coordinates recording and restoring across all registered components.

### Architecture

```
GameClock (time source)
    ↓ events: OnTick, OnRewindStart, OnRewindStop
RewindTimeline (coordinator)
    ↓ calls RecordAll() / RestoreAll()
    ↓ calls ReturnAllPools() on rewind start/stop
IRewindable components (self-contained state capture)
    ├── RewindableTransform   — position, rotation, scale
    ├── RewindableRigidbody2D — velocity, body type
    ├── RewindableGameObject  — activeSelf (change-driven)
    ├── RewindableHealth      — HP + invincibility (event-driven)
    └── RewindableAnimator    — state hash, params
ObjectPool (projectile lifecycle)
    ├── Get(pos, rot)   — activates pooled object
    ├── Return(obj)     — deactivates, re-parents
    └── ReturnAll()     — bulk return on rewind
```

### Key Design Decisions

1. **Awake/OnDestroy registration** (not OnEnable/OnDisable) — dead enemies stay registered so RewindableGameObject can reactivate them
2. **2-phase RestoreAll** — RewindableGameObject runs first (reactivates objects), then all other components restore their state
3. **Pre-existence default** — if rewinding to a time before an object was first recorded, `ApplyPreExistenceDefault()` deactivates it
4. **Change-driven recording** — RewindableGameObject and RewindableHealth only record when state actually changes (with safety-net ticks)
5. **No scene-wide lookups** — all components self-register with `RewindTimeline.Instance`
6. **Pool-based projectiles** — `ObjectPool` manages projectile lifecycle; `ReturnAll()` on rewind instead of Destroy

### Adding Rewind to a New Object

Place these components on the prefab:
- `RewindableTransform` — always (position tracking)
- `RewindableRigidbody2D` — if it has a Rigidbody2D
- `RewindableGameObject` — if it can be deactivated/destroyed
- `RewindableHealth` — if it has PlayerHealth or EnemyHealth
- `RewindableAnimator` — if it has an Animator
- `RewindableProjectile` + `ProjectileLifetime` — for short-lived projectiles (pool-aware)

### Object Pool System

`ObjectPool` is a MonoBehaviour-based generic pool. Created by LevelSceneGenerator, wired as serialized fields.

```
LevelSceneGenerator creates:
    ├── PlayerProjectilePool  (Projectile_Player, size=10)
    ├── EnemyProjectilePool   (Projectile_Enemy, size=20)
    └── LaserPool             (Projectile_Laser, size=5)

Wiring:
    LevelRuntime holds pool refs → distributes to enemies at spawn time
    PlayerController.SetProjectilePool() ← called by LevelRuntime
    EnemyShooter.SetProjectilePool()   ← called by LevelRuntime.SpawnEnemy()
    EnemySentinel.SetLaserPool()       ← called by LevelRuntime.SpawnEnemy()

Rewind integration:
    ObjectPool.Awake() → RewindTimeline.RegisterPool(this)
    RewindTimeline.OnRewindStarted → ReturnAllPools() → each pool.ReturnAll()
    ProjectileLifetime returns to pool instead of Destroy()
```

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
Located at `Assets/Resources/EnemyTypeConfigs/`:
- `Chaser.asset` — follows player, melee
- `Fighter.asset` — alternates approach/attack states
- `Sentinel.asset` — stationary, ranged (laser)
- `Shooter.asset` — shoots projectiles (configurable direction)
