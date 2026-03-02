# Current State — Updated (Post Full Generation Pipeline)

## What Works ✅

### Data Layer
- **GameDefinition.cs** — Root SO with all config types, validation, singleton access, **GetProjectileConfig(id)** accessor
- **All config types** — LevelConfig, VideoConfig, DeathConfig, EnemySpawnConfig, EnemyTemplate, PlayerConfig, PlatformConfig, TimelineConfig, ProjectileConfig, PickupConfig, **AnimationConfig**
- **GameDefinition.asset** — Lives at `Assets/Resources/GameDefinition.asset`, loadable at runtime
- **Enemy Templates** — 4 SO assets at `Assets/Resources/EnemyTemplates/` (Chaser, Fighter, Sentinel, Shooter) — each an `EnemyType` enum value
- **GameDefinition now has UI/Camera prefab slots** — `levelUIPrefab`, `videoUIPrefab`, `deathUIPrefab`, `levelCameraPrefab`, `videoCameraPrefab`, `deathCameraPrefab`
- **GameDefinition has Rewind Settings** — `rewindRecordInterval`, `rewindMaxHistorySeconds`
- **ProjectileConfigs** — Define projectile types (Player, Enemy, Laser) for generation, with poolSize, **spriteSheet**, **animationConfig**
- **PickupConfig** — Defines Diamond prefab (sprite, physics, pool, bob animation, lifetime), **spriteSheet**, **animationConfig**
- **PlayerConfig** — movement, combat, rotation, drop-through, shadow (`shadowMaxHeight/MinScale/RaycastDistance`, `shadowSpriteSheet`, `shadowAnimationFps`), input (`inputDeadzone`, `mobileDropThreshold`, `jumpDetectVelocityThreshold`), invincibility, rewind, **`animationConfig`** (auto-populated by generator if null)
- **EnemyTemplate** — all base stats, physics, combat, invincibility, health bar, behavior, plus `projectileLifetime`, `projectileDamage`, `firePointOffset`, **`animationConfig`** (auto-populated by generator if null based on `EnemyType`)
- **AnimationConfig system** — 6 serializable types: AnimationConfig, AnimatorParameterConfig, AnimationStateConfig, AnimationEventConfig, AnimationTransitionConfig, TransitionConditionConfig. Plus enums: AnimatorParameterType, ConditionMode.
- **No bypass fields** — removed `animatorController`, `defaultSprite`, `shadowAnimatorController`. Config is the ONLY source of truth. Generators always generate.
- **Config-driven scene layout** — `groundY`, `groundColliderSize`, `wallX`, `wallColliderSize`, `levelCameraOrthoSize`, `cinematicCameraOrthoSize` on GameDefinition

### Animation Generation
- **AnimationGenerator.cs** — Core engine that produces `.anim` clips + `.controller` files from AnimationConfig
  - `Generate()`: Full pipeline — loads sprites per state (with name filter), creates clips with keyframes + events, builds controller with parameters/states/transitions/conditions
  - `GenerateSimpleIdle()`: Fallback for single looping clip when no AnimationConfig exists but spriteSheet is set
  - `DeleteFolder()`: Cleanup before regeneration (idempotent)
- **AnimationConfigPopulator.cs** — Internal utility (NOT a menu item) called automatically by EnemyPrefabGenerator and PlayerPrefabGenerator. Auto-populates AnimationConfig based on EnemyType when config is null.
- **No manual .controller or .anim files** — ALL animation assets are generated. `ProjectGenerator` Phase 1 wipes the entire `Assets/Animations/` tree.
- **Runtime parameter mapping** (verified by audit):
  - Player: OnGround(bool), Velocity_X(float), Velocity_Z(float), Jump(trigger), Fire(trigger)
  - EnemyFighter: IsWalking(bool), IsAttacking(bool), event: OnAttackFrame
  - EnemySentinel: IsCrouching(bool), IsAttacking(bool), events: OnStandingAttackFrame, OnCrouchAttackFrame
  - EnemyShooter: IsAttacking(bool), event: FireProjectile
  - EnemyChaser: No parameters (simple idle loop)

### Prefab Generation
- **ONE BUTTON: `SickOfIt → Generate Project`** — the ONLY menu item for generation. Does everything:
  - Phase 1: Cleanup (delete ALL generated prefabs, animations, old scenes)
  - Phase 2: Generate prefabs (Projectiles → Pickups → Platforms → Player → Enemies)
  - Phase 3: Generate scenes (Video, Level, Death)
  - Phase 4: Configure Addressables
- **ProjectilePrefabGenerator.cs** — Generates `Projectile_*.prefab` with DamageDealer, targetTags, ProjectileLifetime wired from config, animation from config/spriteSheet. **Wildcard cleanup** (no hardcoded names).
- **PickupPrefabGenerator.cs** — Generates `Diamond.prefab` at `Assets/Prefabs/` with rewind components, animation from config/spriteSheet
- **PlatformPrefabGenerator.cs** — Generates `Platform.prefab` with BoxCollider2D, PlatformEffector2D, DropThroughPlatform
- **PlayerPrefabGenerator.cs** — Generates `Character.prefab` with full config wiring. **Auto-populates AnimationConfig** if null via AnimationConfigPopulator.
- **EnemyPrefabGenerator.cs** — Generates `Enemy_*.prefab` with full config wiring. **Auto-populates AnimationConfig** per EnemyType if null via AnimationConfigPopulator.

### Scene Generation
- **LevelSceneGenerator.cs** — Creates fully-wired level scenes with GameClock pre-attached, RewindTimeline (config-driven recordInterval/maxHistorySeconds), RewindController (config-driven rewindSpeed/minimumTimeBeforeRewind), projectile pools, **pickup pool** (from PickupConfig.poolSize), Character prefab, LevelRuntime, Music, MobileController, HealthUI
- **VideoSceneGenerator.cs** — Creates video scenes with VideoRuntime, VideoPlayer, Audio, config camera
- **DeathSceneGenerator.cs** — Creates death scenes with DeathRuntime, DeathAnimation, **AudioSource wired to DeathRuntime**, config camera
- **All generators wire serialized references** via `SerializedObject` — no runtime discovery needed

### Object Pool System
- **ObjectPool.cs** — MonoBehaviour-based generic pool with **per-instance component caching** (Rigidbody2D, ProjectileLifetime cached at creation, no GetComponent during gameplay). **SendMessage("SetOwnerPool") on creation** for cross-assembly DamageDealer wiring.
- **4 pools per level scene** — Player projectiles, Enemy projectiles, Lasers, **Pickups (Diamond)**
- **Rewind integration** — pools self-register with RewindTimeline. On rewind start/stop, ReturnAllPools() bulk-returns all active projectiles
- **GetCachedRb() / GetCachedLifetime()** — consumers (EnemyShooter, EnemySentinel) use cached refs instead of per-fire GetComponent
- **PointsPickup** — pool-based: EnemyBase uses `pickupPool.Get()`, `ReturnOrDestroy()` on collection/despawn, `ResetState()` on pool reuse

### Rewind System (Component-Based)
- **RewindTimeline** — Per-scene singleton, config-driven `recordInterval` and `maxHistorySeconds` wired by LevelSceneGenerator
- **BaseRewindable<T>** — Abstract MonoBehaviour base with CircularBuffer<TSnapshot>, binary-search restore. **`RecordState` is now `virtual`**, subclasses use `override` (not `new`). **Start() retry** ensures registration even if RewindTimeline wasn't ready in Awake.
- **5 rewindable components**: RewindableTransform, RewindableRigidbody2D, RewindableGameObject, RewindableHealth, RewindableAnimator
- **RewindableAnimator** — **Pre-allocated arrays** for float/int/bool snapshots (eliminates GC pressure during recording)
- **All generated prefabs have rewind components** — enemies, player, pickups, projectiles
- **Pool-based projectile rewind** — ReturnAllPools() on rewind, no per-projectile tracking

### Runtime
- **LevelRuntime** — Config-driven completion via `config.completionDelay`. SetPools wires enemy/laser/pickup pools. GameClock setup prefers pre-attached component. **Win zone reads `winZoneCenter`/`winZoneHalfSize` from per-level config.**
- **ExperienceRunner** — All coroutines use **GameClock.DeltaTime**. **Wave-spawned enemies receive pickup pool** via `SetPickupPool()`. **Caches pickup pool** for distribution.
- **EnemyHealth** — **SetHealth() now fires `onHealthChanged` event and `UpdateHealthBar()`** (was silent before)
- **GlitchEffect** — Flash timing uses **GameClock.DeltaTime** instead of WaitForSeconds — pauses during rewind
- **All enemy types** apply full config from template: faceMovementDirection, projectileLifetime/Damage
- **PlayerController** — `inputDeadzone` and `mobileDropThreshold` are now **serialized fields** (were hardcoded constants), wired from config by generator
- **VideoSelectionManager** — reads `VideoConfig.displayName` for button text
- **GameProgressManager** — reads `VideoConfig.alwaysUnlocked` instead of hardcoded `videoNumber == 1`
- **RogueliteManager** — `ResetAllRogueliteData()` uses `GameDefinition.TotalLevels` instead of hardcoded 100
- **DeathRuntime** — uses scene-generated AudioSource (falls back to creating one if missing)

### Combat
- **4 enemy types** with fully generated prefabs: Chaser, Fighter, Sentinel, Shooter
- **EnemyShooter** — projectileLifetime/projectileDamage from template, uses ObjectPool cached refs
- **EnemySentinel** — laserLifetime/laserDamage from template, uses ObjectPool cached refs
- **EnemyBase** — `SetPickupPool()` receives pickup pool from LevelRuntime, `SpawnPointDrops()` uses pool.Get() when available
- **DamageDealer** — pool-aware, correct `damageAmount` field wired by generator. **`ownerPool` wired via SendMessage** at pool creation time.

### Anti-Pattern Scorecard
- **0 remaining** FindObjectOfType/FindGameObjectWithTag **in gameplay scene code**
- **3 remaining** in non-gameplay code: GlobalTapJump (2x, MobileController prefab), MainMenuManager (1x, menu scene) — **DEFERRED**
- **0 remaining** per-fire GetComponent on pooled objects (all cached by ObjectPool)
- **0 remaining** runtime AddComponent for ProjectileLifetime (pre-attached by generator)
- **Shared static white sprite** for enemy health bars (no more per-instance Texture2D)
- **0 dead config fields** — cleaned up: removed `startingLevel`, `debugMode`, `levelBounds`, `hasTwoStageSequence`, `firstSequenceDuration`
- **0 dead code** — cleaned up: removed `SetupTimelines()`, `ShowSceneTransitionUI()`, `SetGameObjectsActive()`, `Pause()`/`Resume()` on ExperienceRunner, `DeathSceneToReload` PlayerPrefs writes

## What Needs Work ⚠️

### GameDefinition.asset Needs Sprite Sheet Assignments
- **Assign sprite sheets in Inspector** — player `spriteSheet`, enemy template `spriteSheet` fields (already partially done), `Diamond.png` on PickupConfig, `Fireball.png` on Enemy/Laser projectile configs
- **Then run `SickOfIt → Generate Project`** — one button generates EVERYTHING: prefabs, animations, scenes, addressables
- **UI/Camera prefab slots** need to be assigned (fallback cameras work)
- **VideoConfig** entries need `displayName` and `alwaysUnlocked` values set

### Known Deferrals
- **AnalyticsManager** hardcodes 4 levels/4 videos — constrained by WebGL JS DllImport
- **Enemy AI state** (nextStateChangeTime, lastAttackTime) not rewind-aware — low gameplay impact
- **Score/points rewind** — game design decision needed (reset on rewind?)
- **GlobalTapJump** FindObjectsByType — lives on manual MobileController prefab
- **MainMenuManager** FindObjectOfType — menu scenes are manual
- **BaseRewindable.bufferCapacity** (600) hardcoded independently from RewindTimeline settings — could derive from config

### Video Playback
- **SpriteFramePlayer may not be playing frames** — verify Update loop advances `localTime` and calls `ShowFrame()`

## What Was Removed 🗑️

### Previous Refactors
- All PlayableDirectors and Timeline assets
- All individual scenes (Video1-4, Level1-4, Death1-3, Animation)
- Old auto-bootstrap `[RuntimeInitializeOnLoadMethod]` pattern
- **TopDownController.cs** — DELETED, replaced by PlayerController.cs
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
