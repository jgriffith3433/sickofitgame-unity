> **Historical (March 2026).** This describes the project before the JSON config, data-driven gameplay, runtime
> menus/HUD and ElevenLabs audio. For how things work now, read [AGENTS.md](AGENTS.md) and [ARCHITECTURE.md](ARCHITECTURE.md).

# Current State — Updated (Post Cleanup + SFX + Background System)

## What Works ✅

### Data Layer
- **GameDefinition.cs** — Root SO with all config types, validation, singleton access, **GetProjectileConfig(id)** accessor
- **All config types** — LevelConfig, VideoConfig, DeathConfig, EnemySpawnConfig, EnemyTypeConfig, PlayerConfig, PlatformConfig, TimelineConfig, ProjectileConfig, PickupConfig, **AnimationConfig**, **BackgroundLayerConfig**
- **GameDefinition.asset** — Lives at `Assets/Resources/GameDefinition.asset`, loadable at runtime
- **Enemy Templates** — 4 SO assets at `Assets/Resources/EnemyTypeConfigs/` (Chaser, Fighter, Sentinel, Shooter) — each an `EnemyType` enum value
- **GameDefinition now has UI/Camera prefab slots** — `levelUIPrefab`, `videoUIPrefab`, `deathUIPrefab`, `levelCameraPrefab`, `videoCameraPrefab`, `deathCameraPrefab`
- **GameDefinition has Rewind Settings** — `rewindRecordInterval`, `rewindMaxHistorySeconds`
- **ProjectileConfigs** — Define projectile types (Player, Enemy, Laser) for generation, with poolSize, **spriteSheet**, **animationConfig**, **SFX** (`fireSound`, `hitSound`, `expireSound`)
- **PickupConfig** — Defines Diamond prefab (sprite, physics, pool, bob animation, lifetime), **spriteSheet**, **animationConfig**, **SFX** (`collectSound`, `spawnSound`)
- **PlayerConfig** — movement, combat, rotation, drop-through, shadow, input, invincibility, rewind, **`animationConfig`**, **SFX** (`hurtSounds[]`, `deathSounds[]`, `jumpSound`, `fireSound`, `landSound`, `pitchVariation`)
- **EnemyTypeConfig** — all base stats, physics, combat, invincibility, health bar, behavior, `firePointOffset`, **`animationConfig`** (auto-populated by generator if null)
- **LevelConfig** — **`backgroundLayers` list** (BackgroundLayerConfig: animated SpriteFramesData, static Sprite, or solid Color with parallax/sorting/scale), **level audio** (`ambientSound`, `ambientVolume`, `levelCompleteSound`)
- **BackgroundLayerConfig** — New strongly-typed config for level backgrounds (replaces legacy `backgrounds` list). Supports animated frames, static sprites, solid colors, parallax, tiling.
- **AnimationConfig system** — 6 serializable types for full declarative animation setup
- **No bypass fields** — Config is the ONLY source of truth. Generators always generate.

### Animation Generation
- **AnimationGenerator.cs** — Core engine that produces `.anim` clips + `.controller` files from AnimationConfig
- **AnimationConfigPopulator.cs** — Auto-populates AnimationConfig based on EnemyType when config is null
- **No manual .controller or .anim files** — ALL animation assets are generated

### Prefab Generation
- **ONE BUTTON: `SickOfIt → Generate Project`** — the ONLY menu item for generation
- **GeneratorHelper.cs** — Shared utility class used by ALL generators:
  - `GetFirstSprite()` / `GetAllSprites()` — sprite sheet helpers
  - `GenerateAnimation()` — unified animation generation (config → controller)
  - `AddRewindComponents()` — standard rewind component attachment with optional rb/animator/health wiring
  - `SetLayer()` / `SetTag()` — safe layer/tag with validation
  - `SetFloat/Int/Bool/Ref/Vector2/Color()` — safe SerializedObject property setters
  - `SavePrefab()` — save + cleanup in one call
  - `AddAudioSource()` — configured AudioSource creation
  - `CreatePool()` — ObjectPool setup
- **All 5 generators use GeneratorHelper** — eliminated duplicate sprite detection, animation generation, rewind wiring, layer/tag setting, and save patterns
- **PlayerPrefabGenerator** — Wires AudioSource + all SFX clips (hurt, death, jump, fire, land, pitchVariation)
- **EnemyPrefabGenerator** — Generates all enemy types with health bars, fire points, animation events
- **ProjectilePrefabGenerator** — Wires `hitSound` from ProjectileConfig onto DamageDealer
- **PickupPrefabGenerator** — Wires AudioSource + `collectSound`/`spawnSound` from PickupConfig

### Scene Generation
- **LevelSceneGenerator.cs** — Creates fully-wired level scenes with GameClock, RewindTimeline, RewindController, pools, Character, LevelRuntime, Music, MobileController, HealthUI, **backgroundsContainer**, **RewindableScore**, **RewindableAudioSource**
- **VideoSceneGenerator.cs** / **DeathSceneGenerator.cs** — Full scene generation with config wiring

### Object Pool System
- **ObjectPool.cs** — MonoBehaviour-based generic pool with per-instance component caching
- **IPoolReturnable interface** — Replaces fragile `SendMessage("SetOwnerPool")`. Implemented by `DamageDealer`, `ProjectileLifetime`, `PointsPickup`.
- **4 pools per level scene** — Player projectiles, Enemy projectiles, Lasers, Pickups
- **Rewind integration** — ReturnAllPools() on rewind start

### Rewind System (Component-Based)
- **RewindTimeline** — Per-scene singleton, config-driven `recordInterval` and `maxHistorySeconds`
- **BaseRewindable<T>** — Abstract base with CircularBuffer<TSnapshot>, binary-search restore
- **7 rewindable components**: RewindableTransform, RewindableRigidbody2D, RewindableGameObject, RewindableHealth, RewindableAnimator, **RewindableScore**, **RewindableAudioSource**
- **RewindableScore** — Records/restores player points total so collected diamonds revert on rewind
- **RewindableAudioSource** — Records music playback position, restores on rewind stop
- **All generated prefabs have rewind components** — enemies, player, pickups, projectiles

### SFX System (Config-Driven)
- **Player sounds** — Jump, fire, land, hurt, death (via PlayerController AudioSource)
- **Projectile sounds** — Hit sound (via DamageDealer AudioSource.PlayClipAtPoint)
- **Pickup sounds** — Collect sound (PlayClipAtPoint), spawn sound (PlayOneShot)
- **All wired by generators** from configs — no manual AudioSource setup needed
- **PlaySFX() / PlayRandomSFX()** helper methods on PlayerController with pitch variation

### Background System (Config-Driven)
- **BackgroundLayerConfig** — Supports animated (SpriteFramesData), static sprite, or solid color
- **LevelRuntime.SetupBackgrounds()** — Reads `config.backgroundLayers` at startup, creates all background GOs
- **Camera-relative positioning** — Backgrounds auto-position relative to camera, scale to viewport
- **Parallax-ready** — `parallaxFactor` field on each layer (parallax movement not yet implemented)

### Analytics
- **AnalyticsManager** — Now fully scalable using generic `trackEvent(string)` JS interop
- **Backward compatible** — Still calls `window.trackLevel1Start()` etc. via dynamic function name
- **No per-level/video DllImport** — Single generic function handles any number of levels/videos
- **Application quit guard** — Prevents singleton creation during editor play mode stop

### Runtime
- **LevelRuntime** — Config-driven: completion, audio, backgrounds, enemies, platforms, barriers, win zone
- **ExperienceRunner** — All coroutines use **GameClock.DeltaTime**. **Static cached Texture2D** for flash screen (no leak).
- **GlitchEffect** — **Cached renderers array** (no GetComponentsInChildren per frame). GameClock-aware flash timing.
- **PlayerController** — SFX wired, pool-cached `GetCachedRb()`/`GetCachedLifetime()` for projectile firing
- **Singleton leak guards** — RogueliteManager, GameProgressManager, AnalyticsManager all have `_applicationIsQuitting` guards

### Anti-Pattern Scorecard
- **0 remaining** FindObjectOfType/FindGameObjectWithTag **in gameplay scene code**
- **3 remaining** in non-gameplay code: GlobalTapJump (2x, MobileController prefab), MainMenuManager (1x, menu scene) — **DEFERRED**
- **0 remaining** per-fire GetComponent on pooled objects (all cached by ObjectPool)
- **0 remaining** SendMessage usage (replaced by IPoolReturnable interface)
- **0 remaining** per-frame GetComponentsInChildren (GlitchEffect now caches)
- **0 remaining** Texture2D leaks (ExperienceRunner flash screen now static cached)
- **0 remaining** runtime AddComponent for ProjectileLifetime (pre-attached by generator)

## What Needs Work ⚠️

### GameDefinition.asset Needs Assignments
- **Assign sprite sheets** — player, enemies, projectiles, pickups
- **Assign SFX AudioClips** — player hurt/death/jump/fire/land sounds, projectile hit sounds, pickup collect/spawn sounds
- **Assign backgroundLayers** — per-level background layers (SpriteFramesData or static sprites)
- **Then run `SickOfIt → Generate Project`**

### Known Deferrals
- **Parallax movement** — `parallaxFactor` field exists on BackgroundLayerConfig but no ParallaxController component yet
- **Enemy AI state** (nextStateChangeTime, lastAttackTime) not rewind-aware — low gameplay impact
- **GlobalTapJump** FindObjectsByType — lives on manual MobileController prefab
- **MainMenuManager** FindObjectOfType — menu scenes are manual
- **BaseRewindable.bufferCapacity** (600) hardcoded independently from RewindTimeline settings
- **LevelRuntime god-class** — could be decomposed into smaller systems

### Video Playback
- **SpriteFramePlayer may not be playing frames** — verify Update loop advances `localTime` and calls `ShowFrame()`

## What Was Removed 🗑️

### Previous Refactors
- All PlayableDirectors and Timeline assets
- All individual scenes (Video1-4, Level1-4, Death1-3, Animation)
- Old auto-bootstrap `[RuntimeInitializeOnLoadMethod]` pattern
- **TopDownController.cs** — DELETED, replaced by PlayerController.cs

### Cleanup Session Fixes
- **AnalyticsManager** — Replaced 20+ hardcoded DllImport functions with 2 generic ones (371→120 lines)
- **ObjectPool** — Replaced `SendMessage("SetOwnerPool")` with `IPoolReturnable` interface
- **GlitchEffect** — Replaced per-frame `GetComponentsInChildren` with Awake-cached array
- **ExperienceRunner** — Replaced per-flash `new Texture2D()` with static cached texture
- **All generators** — Refactored to use `GeneratorHelper` shared utilities
- **ClawCharacterPipeline.cs** — replaced by individual PrefabGenerators
- **StateRecorder.cs** — replaced by component-based RewindTimeline
- **RewindableProjectile.cs** — replaced by ObjectPool bulk-return
- **LevelWinTrigger.cs** — merged into LevelRuntime bounds check
- **AddressableGroupGenerator.cs** — stale, used wrong paths
- Manual prefabs (Character, Enemy_*, Projectile_*) — now all GENERATED

### Deep Audit Cleanup (This Session)
- **38 manual animation files** (.controller + .anim) from Assets/Animations/ — ALL replaced by AnimationGenerator
- **4 dead prefabs** — Shadow.prefab, HealthBarContainer.prefab, HealthUI.prefab, Heart.prefab (all unreferenced)
- **5 dead config fields** — `startingLevel`, `debugMode`, `levelBounds`, `hasTwoStageSequence`, `firstSequenceDuration`
- **Dead code** — `SetupTimelines()`, `ShowSceneTransitionUI()`, `SetGameObjectsActive()`, `Pause()`/`Resume()` on ExperienceRunner, `DeathSceneToReload` PlayerPrefs writes, `objectsToActivateOnTransition`/`objectsToDeactivateOnTransition` lists
- **Dead UI** — `hasTwoStageSequence`/`firstSequenceDuration` fields removed from GameOverviewWindow

### Bugs Fixed (This Session)
| # | Bug | Fix |
|---|-----|-----|
| 1 | DamageDealer.ownerPool never set — pool leak on hit | SendMessage in ObjectPool.CreateInstance |
| 2 | BaseRewindable `new` vs `override` — RecordState dispatch broken | Made RecordState virtual + override |
| 3 | BaseRewindable no Start() retry — registration race | Added protected virtual Start() with retry |
| 4 | Win zone not per-level at runtime | Read from config in LevelRuntime.Start |
| 5 | EnemyHealth.SetHealth doesn't fire events | Added onHealthChanged + UpdateHealthBar |
| 6 | EnemyPrefabGenerator invincibilityTimer field name | Changed to "invincibilityDuration" |
| 7 | Wave enemies missing SetPickupPool | Added SetPickupPool in HandleSpawnEnemyWave |
| 8 | ProjectileLifetime.lifetime not wired | Wire via SerializedObject in generator |
| 9 | PlayerPrefabGenerator maxAirJumps not wired | Wire from config.maxExtraJumps |
| 10 | RewindableAnimator GC pressure | Pre-allocated arrays in CacheParameterInfo |
