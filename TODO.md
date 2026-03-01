# TODO — Priority Order

## 🔴 Critical (Game Won't Work Without These)

### 1. Fix ProjectGenerator to Use Scene Generators
The current `ProjectGenerator.cs` creates minimal empty scenes. The individual generators (`LevelSceneGenerator`, `VideoSceneGenerator`, `DeathSceneGenerator`) create fully-wired scenes with serialized references. **Reconcile these.**

**Recommended approach:**
- Update `ProjectGenerator.GenerateProject()` to call the individual generators
- Pass `"Video"` as the scene name (not per-config names like "Video1")
- Since there's only 1 scene per type, just generate from the first config entry OR generate a generic shell that works for any config
- The generators should save to `Assets/Scenes/Video.unity`, `Assets/Scenes/Level.unity`, `Assets/Scenes/Death.unity`
- Then call `AddressableGroupGenerator.UpdateAddressableGroups(gd)` to set up Addressables

**OR** keep 3 simple scenes but make sure Runtimes can fully self-wire at runtime (current approach has gaps).

### 2. Fix Video Playback
VideoRuntime calls `spritePlayer.Play()` but video doesn't play. Debug steps:
1. Check Unity console for `[VideoRuntime]` logs — does it find the config? Does SpriteFramePlayer initialize?
2. Check if `SpriteFramePlayer.Update()` is advancing `localTime` when `useGameClock = false`
3. Check if Addressable sprite chunks are loading (look for `[AddressablesOp]` logs)
4. Check if `ShowFrame()` is being called and updating the SpriteRenderer
5. The `useGameClock` field is currently `false` — make sure the non-GameClock path in Update works

### 3. Fix GameBuilder.cs Compile Error
`GameBuilder.cs` line 40 references `UnifiedGameBuilder` which was deleted. Either:
- Delete the entire `GameBuilder.cs` (if ProjectGenerator replaces it)
- Or remove the `UnifiedGameBuilder` reference and keep it as legacy

### 4. Fix DeathRuntime.ReloadLevel()
Current code parses "Level" + number from scene name, but the scene is just "Level". Fix:
```csharp
private void ReloadLevel()
{
    if (PlayerPrefs.HasKey("CurrentLevelNumber"))
    {
        int levelNum = PlayerPrefs.GetInt("CurrentLevelNumber");
        SceneTransitionManager.Instance.LoadLevel(levelNum);
    }
    else
    {
        SceneTransitionManager.Instance.LoadMainMenu();
    }
}
```

## 🟡 Important (Game Partially Broken)

### 5. Wire GameDefinition Asset
In Unity Inspector, open `Assets/Resources/GameDefinition.asset` and verify:
- [ ] `player.playerPrefab` → `Assets/Prefabs/Character.prefab`
- [ ] `levelUIPrefab` → `Assets/Prefabs/MobileController.prefab` (or a level-specific UI prefab)
- [ ] `videoUIPrefab` → A skip-button-only UI prefab (create if needed)
- [ ] `deathUIPrefab` → A skip-button-only UI prefab (create if needed)
- [ ] `enemyTemplates` list has all 4 templates from `Assets/Resources/EnemyTemplates/`
- [ ] Each `LevelConfig` entry has enemies, music, duration configured
- [ ] Each `VideoConfig` entry has `spriteFramesData` assigned (SpriteFramesData ScriptableObjects)
- [ ] Each `DeathConfig` entry has `deathName` set (referenced by `LevelConfig.deathConfigName`)

### 6. Level Player Spawning
LevelRuntime tries to spawn from `PlayerConfig.playerPrefab` if `characterObject` isn't wired. Make sure:
- The Character prefab has `PlayerHealth`, `TopDownController`, `Rigidbody2D`
- The `GameInputManager` on MobileController can bind to `TopDownController`
- If using the generator, it instantiates the prefab and wires it via `SerializedObject`

### 7. Level Completion Flow
`LevelWinTrigger` calls `LevelRuntime.CompleteLevel()`. Verify:
- The WinTrigger is placed at the right edge of the level
- The player collider triggers it
- `CompleteLevel()` saves progress and loads next video/level

## 🟢 Nice to Have

### 8. Remove Debug Logging
VideoRuntime has verbose debug logging (every 60 frames). Remove or gate behind `GameDefinition.debugMode` before shipping.

### 9. Clean Up Stale Files
- Delete `Assets/Scripts/Tools/Editor/SetupAddressables.cs`
- Delete or update `GameBuilder.cs` 
- Review `Assets/Scripts/Tools/Editor/GameOverview/GameOverviewWindow.cs` — may need updating

### 10. SpriteFramePlayer GameClock Integration
`useGameClock` is currently `false`. For rewind to work on videos, it needs to use GameClock. But first get basic playback working with `Time.deltaTime`, then switch to GameClock.

### 11. ExperienceRunner Event Types
ExperienceRunner supports 24 event types. Make sure the TimelineConfig events in GameDefinition use valid event types. Check `ExperienceRunner.cs` for the full enum.

### 12. Barrier Generation
LevelRuntime generates barriers from camera viewport. Verify:
- `config.generateBarriers` is true in LevelConfig
- Camera exists before barriers are spawned
- Boundary layer exists (`LayerMask.NameToLayer("Boundary")`)

## Architecture Decisions to Make

### Single Scene vs Per-Config Scenes
**Current state**: Two conflicting approaches exist:
1. `ProjectGenerator` creates 3 generic scenes (Video, Level, Death)
2. Individual generators create per-config scenes (Video1, Level1, etc.) with full wiring

**Option A**: Keep 3 generic scenes, make Runtimes fully self-sufficient (current direction but incomplete)
**Option B**: Generate per-config scenes with full wiring, update Addressables to map each one

**Recommendation**: Option A is cleaner long-term. Fix the Runtimes to be fully self-sufficient:
- VideoRuntime already works mostly (just needs SpriteFramePlayer fix)
- LevelRuntime already spawns character from config if not wired
- DeathRuntime already creates fallback objects

The generators are still useful for producing richer scenes when you want editor-time wiring (e.g., camera prefabs, UI prefabs). But the Runtimes should work even in a bare scene.
