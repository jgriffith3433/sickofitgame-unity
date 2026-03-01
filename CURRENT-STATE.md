# Current State — March 1, 2026

## What Works ✅

### Data Layer
- **GameDefinition.cs** — Root SO with all config types, validation, singleton access
- **All config types** — LevelConfig, VideoConfig, DeathConfig, EnemySpawnConfig, EnemyTemplate, PlayerConfig, PlatformConfig, TimelineConfig
- **GameDefinition.asset** — Lives at `Assets/Resources/GameDefinition.asset`, loadable at runtime
- **Enemy Templates** — 4 SO assets at `Assets/Resources/EnemyTemplates/` (Chaser, Fighter, Sentinel, Shooter)
- **GameDefinition now has UI/Camera prefab slots** — `levelUIPrefab`, `videoUIPrefab`, `deathUIPrefab`, `levelCameraPrefab`, `videoCameraPrefab`, `deathCameraPrefab`

### Scene Generation
- **ProjectGenerator.cs** — Creates 3 simple scenes (Video, Level, Death) + sets up Addressables. Menu: `SickOfIt → Generate Project`
- **LevelSceneGenerator.cs** — Creates fully-wired level scenes with Character prefab, LevelRuntime, StateRecorder, RewindController, Music, MobileController, ground/walls/win trigger. Uses GameDefinition UI/camera prefabs.
- **VideoSceneGenerator.cs** — Creates video scenes with VideoRuntime, VideoPlayer, Audio. Uses GameDefinition UI/camera prefabs.
- **DeathSceneGenerator.cs** — Creates death scenes with DeathRuntime, DeathAnimation, Audio. Uses GameDefinition UI/camera prefabs.
- **AddressableGroupGenerator.cs** — Updates Addressable groups from GameDefinition
- **All generators wire serialized references** via `SerializedObject` — no runtime discovery needed

### Runtime
- **VideoRuntime** — Reads PlayerPrefs for video number, loads VideoConfig, initializes SpriteFramePlayer, handles skip/completion. References wired by generator.
- **LevelRuntime** — Reads PlayerPrefs for level number, loads LevelConfig, spawns enemies/platforms/barriers from config, sets up GameClock/StateRecorder/RewindController, handles completion/death flow. References wired by generator. Falls back to spawning Character prefab if not wired.
- **DeathRuntime** — Reads PlayerPrefs for death config name, loads DeathConfig, plays death animation, reloads level after delay. References wired by generator.
- **SceneTransitionManager** — Correctly routes to "Video", "Level", "Death" scenes via Addressables, setting PlayerPrefs before load
- **ExperienceRunner** — Fires timed events from TimelineConfig driven by GameClock
- **GameClock** — Per-scene singleton, forward/backward time, pause/resume/rewind
- **StateRecorder** — Ring buffer snapshots of player/enemy/camera state
- **RewindController** — Reverses GameClock, restores snapshots

### Scenes
- **7 scenes in project**: MainMenu, LevelSelection, VideoSelection, UpgradeShop, Video, Level, Death
- **4 menu scenes** in Build Settings (loaded directly)
- **3 game scenes** loaded via Addressables
- All old individual scenes (Video1-4, Level1-4, Death1-3, Animation) and folders (Deaths/, LevelS/, VideoS/) have been deleted

### Combat
- **4 enemy types** with prefabs and templates: Chaser, Fighter, Sentinel, Shooter
- **EnemyBase.InitializeFromConfig()** reads from EnemyTemplate + EnemySpawnConfig
- **AnimationEventRelay** forwards animation events from child Animators
- **Player prefab** at `Assets/Prefabs/Character.prefab`

## What Needs Work ⚠️

### Video Playback (HIGH PRIORITY)
- **SpriteFramePlayer may not be playing frames** — VideoRuntime initializes it and calls Play(), but the actual frame advancement might not work correctly
- **`useGameClock` is set to `false`** in SpriteFramePlayer — it should use `Time.deltaTime` to advance frames, but verify the Update loop is actually running and advancing `localTime`
- **Debug logs are in place** — VideoRuntime logs progress every 60 frames. Check console output to see if it's stuck at 0% or advancing
- **Possible issue**: SpriteFramePlayer's Update method may need `useGameClock = false` path to actually work (check that `localTime` advances and `ShowFrame()` is called)

### GameBuilder.cs is Stale
- **GameBuilder.cs** still references old `UnifiedGameBuilder` (compile error) — needs to be updated or deleted
- The old GameBuilder called individual scene generators but used a different folder structure
- **ProjectGenerator.cs** is the current entry point but it creates simple scenes, not the fully-wired ones from the individual generators
- **Decision needed**: Should `ProjectGenerator` call the individual generators (LevelSceneGenerator, etc.) instead of creating minimal scenes? The individual generators produce much richer scenes.

### Scene Generation Mismatch
- **ProjectGenerator** creates simple scenes (just camera + basic objects)
- **LevelSceneGenerator/VideoSceneGenerator/DeathSceneGenerator** create fully-wired scenes with serialized references
- These two approaches need to be reconciled — ideally ProjectGenerator should call the individual generators
- The individual generators save to subfolders (`Scenes/Levels/`, `Scenes/Videos/`, `Scenes/Deaths/`) which contradicts the "3 simple scenes" approach
- **Recommendation**: Update ProjectGenerator to call the generators but save to `Assets/Scenes/Video.unity`, `Assets/Scenes/Level.unity`, `Assets/Scenes/Death.unity`

### GameDefinition.asset Needs Population
- The GameDefinition asset exists but may not have all configs properly filled in
- **UI prefab slots** (`levelUIPrefab`, `videoUIPrefab`, `deathUIPrefab`) need to be assigned
- **Camera prefab slots** need to be assigned (or the fallback camera works fine)
- **PlayerConfig.playerPrefab** needs to point to `Assets/Prefabs/Character.prefab`
- Verify all 4 enemy templates are linked in `enemyTemplates` list
- Verify video configs have `spriteFramesData` assigned (SpriteFramesData ScriptableObjects)

### DeathRuntime Reload Logic
- `ReloadLevel()` tries to parse "Level" + number from `PlayerPrefs["DeathSceneToReload"]` — but since we only have one scene named "Level", this parsing won't work
- Should instead read `PlayerPrefs["CurrentLevelNumber"]` directly

### Leftover Files to Clean Up
- `Assets/Scripts/Tools/Editor/SetupAddressables.cs` — old, can delete
- `Assets/Scripts/Tools/Editor/ProjectGenerator.cs` — needs updating or replacement (see above)
- Verify no other stale editor scripts reference deleted types

## What Was Removed 🗑️
- All PlayableDirectors and Timeline assets
- All individual scenes (Video1-4, Level1-4, Death1-3, Animation)
- Scene folders (Deaths/, LevelS/, VideoS/)
- Old auto-bootstrap `[RuntimeInitializeOnLoadMethod]` pattern — replaced with generators wiring components directly into scenes
