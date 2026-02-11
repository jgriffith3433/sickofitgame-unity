# Memory Management System - iOS Safari Fix

## Overview
This document explains the comprehensive memory management refactor that fixes the iOS Safari page reload issue caused by excessive memory usage.

---

## The Problem

**Symptom**: WebGL game reloads the page unexpectedly on iOS Safari, showing "A problem repeatedly occurred on https://sickofitgame.com/"

**Root Cause**: 
- Assets were never released from memory
- When loading Level1 → Video1 → Level2, ALL assets remained in RAM
- iOS Safari has strict memory limits (~300-500MB)
- Accumulated memory eventually triggered page reload

**Example Memory Growth**:
```
MainMenu (50MB base)
  → Load Level1_Assets (+150MB) = 200MB total
     → Load Video1_Assets (+100MB) = 300MB total ⚠️
        → Load Level2_Assets (+150MB) = 450MB total ⚠️⚠️
           → CRASH: Safari kills the page
```

---

## The Solution

### 1. **Automatic Scene Asset Cleanup**
**Location**: `AddressablesOperationManager.cs`

**Key Changes**:
- Track current scene name
- Before loading new scene, release previous scene's assets
- Clear all dictionaries (untyped, typed, and individual assets)

**New Methods**:
```csharp
// Call this before loading a new scene
SetCurrentScene(string sceneName)
  → Releases previous scene's assets automatically
  → Updates currentSceneName tracking

// Release all assets for a specific scene
ReleaseSceneAssets(string sceneName)
  → Releases "SceneName_Assets" label
  → Releases all sub-labels (e.g., Level1_Background1_Frames)
  → Clears from all dictionaries
  → Forces garbage collection

// Get memory info
GetMemoryInfo(out long totalMB, out long usedMB, out long monoUsedMB, out long monoHeapMB)
  → Uses Unity Profiler APIs
  → Returns current memory stats
```

**How It Works**:
```csharp
// When transitioning Level1 → Video1:
AddressablesOperationManager.Instance.SetCurrentScene("Video1");
  1. Detects previous scene was "Level1"
  2. Calls ReleaseSceneAssets("Level1")
  3. Releases "Level1_Assets", "Level1_Background1_Frames", "Level1_Music"
  4. Clears all references from dictionaries
  5. Calls GC.Collect() and Resources.UnloadUnusedAssets()
```

---

### 2. **Improved ReleaseLabel Method**
**Previously**: Only released untyped labels (missed Sprite/AudioClip loads)
**Now**: Releases ALL label types

```csharp
ReleaseLabel(string label)
  → Releases untyped label (loadedLabels)
  → Releases ALL typed variations (loadedTypedLabels)
     Example: "Level1_Background1_Frames<Sprite>"
  → Clears in-progress flags
  → Logs memory release
```

---

### 3. **Scene Transition Integration**
**Location**: `SceneTransitionManager.cs`

**Changes**:
```csharp
LoadLevel(int levelNumber)
  // OLD: Just loaded the scene
  AddressablesSceneLoader.Instance.LoadScene(sceneName);
  
  // NEW: Cleanup old scene first
  AddressablesOperationManager.Instance.SetCurrentScene(sceneName); // ← Triggers cleanup
  AddressablesSceneLoader.Instance.LoadScene(sceneName);

LoadVideo(int videoNumber)
  // Same pattern - cleanup before loading
  AddressablesOperationManager.Instance.SetCurrentScene(sceneName);
  AddressablesSceneLoader.Instance.LoadScene(sceneName);
```

---

### 4. **Disabled Aggressive Background Preloading**
**Problem**: Loading assets in background that might never be used

**Files Modified**:
- `MainMenuPreloader.cs` → Disabled `PreloadAppropriateLevel()`
- `VideoManager.cs` → Disabled `PreloadNextContentDuringVideo()`

**Reasoning**:
- Background preloading loaded Level1_Assets + Level2_Assets + Video1_Assets simultaneously
- User might never click Continue (wasted memory)
- Assets are now loaded on-demand when user clicks buttons
- Seamless transitions still work because download/load is fast

**Before**:
```
MainMenu loads → Immediately preload Level1_Assets in background
Video1 plays → Immediately preload Level2_Assets in background
Result: Multiple scenes' assets in RAM
```

**After**:
```
MainMenu loads → Nothing preloaded
User clicks → Load only what's needed
Scene transition → Auto-release old scene
Result: Only current scene's assets in RAM
```

---

### 5. **Memory Monitoring UI**
**Location**: `PreloadStatusIndicator.cs`

**New Display** (Press `O` key to show/hide):
```
[Memory Status]
Total Alloc: 180MB
Reserved: 220MB
Mono: 45MB / 60MB
Platform Limit: ~300-500MB

[Preload Status]
Loading: Video1
  Progress: 45.2%
```

**Memory Metrics**:
- **Total Alloc**: Total memory allocated by Unity
- **Reserved**: Memory reserved from OS (includes overhead)
- **Mono**: C# managed memory usage / heap size
- **Platform Limit**: Estimated limit for current platform (WebGL shows ~300-500MB for iOS Safari)

**Usage**:
- Always visible when enabled (even when idle)
- Updates every 0.5 seconds
- Shows active preload operations
- Press `O` to toggle visibility

---

## Asset Labeling Structure

**How Addressables are organized**:
```
Scene Address: "Level1" (scene itself)
Asset Label: "Level1_Assets" (all assets for Level1)

Sub-labels also tagged with "Level1_Assets":
  - "Level1_Background1_Frames"
  - "Level1_Background2_Frames"
  - "Level1_Music"
  
All belong to: "Level1_Group" (Addressables group)
```

**Cleanup Pattern**:
When releasing "Level1":
1. Release main label: `Level1_Assets`
2. Scan for sub-labels: `Level1_*`
3. Release all matches
4. Clear all dictionaries
5. Force GC

---

## Memory Flow Example

### Before Refactor:
```
MainMenu (50MB)
  → Click New Game
  → Preload Level1_Assets (150MB) = 200MB
  → Preload Video1_Assets (100MB) = 300MB
  → Load Video1 scene
  → During video, preload Level2_Assets (150MB) = 450MB
  → ⚠️ iOS Safari limit exceeded
  → 🔴 PAGE RELOAD
```

### After Refactor:
```
MainMenu (50MB)
  → Click New Game
  → Load Video1_Assets (100MB) = 150MB
  → Load Video1 scene
  → User watches video
  → Click to Level1
  → SetCurrentScene("Level1")
     → Release Video1_Assets (-100MB) = 50MB
     → GC.Collect()
  → Load Level1_Assets (150MB) = 200MB
  → Play Level1
  → Complete Level1
  → SetCurrentScene("Video2")
     → Release Level1_Assets (-150MB) = 50MB
     → GC.Collect()
  → Load Video2_Assets (100MB) = 150MB
  → ✅ Memory stays under limit
```

---

## Testing the Fix

### In Unity Editor:
1. Press `P` to open debug console
2. Press `O` to show memory monitor
3. Click "New Game"
4. Watch memory usage in monitor
5. Play through: Video1 → Level1 → Level2
6. **Expected**: Memory should drop after each scene transition
7. **Console logs should show**:
   ```
   [SCENE TRANSITION] Video1 -> Level1
   [MEMORY] Releasing assets for scene: Video1
   [MEMORY] Released label from memory: Video1_Assets
   [MEMORY] Completed releasing assets for: Video1
   ```

### On iOS Safari:
1. Deploy to WebGL
2. Open on iOS device
3. Triple-tap to open debug console
4. Play through multiple levels
5. **Expected**: NO PAGE RELOADS
6. Memory should stay around 150-200MB (was 400-500MB before)

---

## Component Reference Guide

### AddressablesOperationManager
**Purpose**: Central manager for ALL Addressables operations
**Key Dictionaries**:
- `loadedLabels` - Untyped label loads (Object)
- `loadedTypedLabels` - Typed label loads (Sprite, AudioClip, etc.)
- `loadedAssets` - Individual asset loads
- `sceneAssetMap` - Maps scene names to their asset labels (NEW)

**New Properties**:
- `currentSceneName` - Currently loaded scene
- `previousSceneName` - Previous scene (for cleanup)

**Critical Methods**:
- `SetCurrentScene()` - Call before loading new scene
- `ReleaseSceneAssets()` - Release specific scene's assets
- `ReleaseLabel()` - Release label (all types)
- `ReleaseAll()` - Clear everything (use carefully)
- `GetMemoryInfo()` - Get current memory stats

### SpriteFramePlayer
**How it loads assets**:
```csharp
LoadFrames()
  → AddressablesAssetLoader.LoadAssetsByLabel<Sprite>(framesLabel)
    → AddressablesOperationManager.LoadLabel<Sprite>(label)
      → Stores in loadedTypedLabels["Label<Sprite>"]
```

**Memory held**:
- `List<Sprite> frames` - Holds references to loaded sprites
- When scene unloads, Unity destroys component
- When label is released, Addressables releases sprite memory
- GC.Collect() cleans up remaining references

### MusicCycler
**Similar pattern to SpriteFramePlayer**:
- Loads AudioClips via Addressables
- Holds references in component
- Cleaned up on scene unload

---

## Important Notes

### Menu Scenes Are NOT Released
**Menu scenes are excluded from cleanup**:
- MainMenu
- LevelSelection
- VideoSelection

**Reason**: These are small scenes that should stay in memory for fast transitions

### When Cleanup Happens
**Automatic cleanup triggers**:
1. Before loading Level scene → Release previous Level/Video
2. Before loading Video scene → Release previous Level/Video
3. Menu scenes DON'T trigger cleanup

### Manual Cleanup
```csharp
// Release specific scene
AddressablesOperationManager.Instance.ReleaseSceneAssets("Level1");

// Release specific label
AddressablesOperationManager.Instance.ReleaseLabel("Level1_Assets");

// Nuclear option - release everything
AddressablesOperationManager.Instance.ReleaseAll();
```

---

## Debugging Memory Issues

### Check Memory Usage:
```csharp
AddressablesOperationManager.Instance.GetMemoryInfo(
    out long totalMB, 
    out long usedMB, 
    out long monoUsedMB, 
    out long monoHeapMB
);
Debug.Log($"Memory: {totalMB}MB allocated, {usedMB}MB reserved");
```

### Check What's Loaded:
```csharp
var ops = AddressablesOperationManager.Instance.GetActiveOperations();
foreach (var op in ops)
{
    Debug.Log($"Active: {op}");
}
```

### Check If Label Is Loaded:
```csharp
bool loaded = AddressablesOperationManager.Instance.IsLabelLoaded("Level1_Assets");
Debug.Log($"Level1_Assets loaded: {loaded}");
```

### Force Cleanup:
```csharp
// If you suspect memory leak
AddressablesOperationManager.Instance.ReleaseSceneAssets("Level1");
System.GC.Collect();
Resources.UnloadUnusedAssets();
```

---

## Performance Considerations

### Load Times
**Q**: Won't on-demand loading make transitions slower?
**A**: No, because:
- Assets are downloaded to cache (persistent)
- Only first load requires download
- Subsequent loads are instant from cache
- Scene transition overlay hides load time

### Seamless Transitions
**Q**: Can we still preload next level during gameplay?
**A**: Yes, but carefully:
```csharp
// During Level1 gameplay, when near end:
PreloadManager.Instance.PreloadLabel("Level2_Assets", (success) => {
    // Level2 ready, but Level1 is still in memory
    // Make sure to release Level1 before loading Level2 scene
});
```

### GC Performance
**Q**: Does GC.Collect() cause frame drops?
**A**: We call it during scene transitions when:
- Loading screen is visible
- User doesn't notice any stuttering
- Better than running out of memory

---

## Platform-Specific Behavior

### iOS Safari (WebGL)
- **Memory Limit**: ~300-500MB (varies by device)
- **Behavior**: Hard kills page when exceeded
- **Solution**: Keep memory under 250MB for safety margin

### Desktop (Standalone)
- **Memory Limit**: System RAM
- **Behavior**: No hard limits
- **Solution**: Still cleanup for good practice

### Android (WebGL)
- **Memory Limit**: Varies by device (usually 500MB-1GB)
- **Behavior**: Similar to iOS but more forgiving

---

## Migration Guide

### If You Add New Scenes:
1. Name scene: `LevelX` or `VideoX`
2. Create Addressables label: `LevelX_Assets` or `VideoX_Assets`
3. Tag all assets with that label
4. Group in: `LevelX_Group` or `VideoX_Group`
5. **No code changes needed** - automatic cleanup works by naming convention

### If You Add Background Preloading:
```csharp
// DON'T DO THIS (causes memory issues):
PreloadManager.Instance.PreloadLabel("Level1_Assets");
PreloadManager.Instance.PreloadLabel("Level2_Assets");
PreloadManager.Instance.PreloadLabel("Video1_Assets');

// DO THIS INSTEAD (load on-demand):
void OnButtonClick() {
    PreloadManager.Instance.PreloadLabel("Level1_Assets", (success) => {
        if (success) {
            SceneTransitionManager.Instance.LoadLevel(1);
        }
    });
}
```

---

## Quick Reference

### Show Memory Monitor:
- Press `O` key

### Show Debug Console:
- Editor: Press `P`
- Mobile: Triple-tap screen

### Manual Cleanup:
```csharp
AddressablesOperationManager.Instance.ReleaseSceneAssets("Level1");
```

### Check Memory:
```csharp
AddressablesOperationManager.Instance.GetMemoryInfo(out var t, out var u, out var m, out var h);
```

### Force Full Cleanup:
```csharp
AddressablesOperationManager.Instance.ReleaseAll();
System.GC.Collect();
Resources.UnloadUnusedAssets();
```

---

## Summary

**The Fix**:
1. ✅ Automatic scene asset cleanup on transition
2. ✅ Improved label release (handles all types)
3. ✅ Disabled aggressive background preloading
4. ✅ Memory monitoring UI
5. ✅ Comprehensive logging

**Expected Result**:
- Memory stays under 250MB on iOS Safari
- No more page reloads
- Seamless transitions still work
- Only current scene's assets in RAM

**Test Target**:
- Play through Level1 → Video1 → Level2 → Video2 → Level3
- Memory should stay stable around 150-200MB
- No page reloads on iOS Safari
