# Addressables System Refactoring - February 2026

## Summary

Completely refactored the Addressables management system to leverage Unity's built-in caching instead of manual RAM tracking. This simplifies the codebase and eliminates the risk of pink shader bugs caused by premature asset release.

## Key Changes

### Philosophy Shift

**OLD APPROACH (Manual Caching):**
- Manually tracked all AsyncOperationHandles in RAM
- Attempted to cache loaded assets in dictionaries
- Provided manual Release methods that could break shared dependencies
- Complex handle management and tracking

**NEW APPROACH (Unity-Managed Caching):**
- Let Unity's Addressables system handle all caching automatically
- Browser caches downloaded bundles on disk (persistent)
- Unity's GC manages asset lifecycle (safe)
- Only prevent duplicate concurrent loads (deduplication)
- No manual release = no pink shaders

---

## Files Changed

### 1. **AddressablesOperationManager.cs** (Major Refactor)

**Removed:**
- `allLoadedHandles` list - no longer tracking handles for caching
- `loadedLabelNames` HashSet - Unity tracks what's loaded
- `sceneAssetMap` - dangerous scene-based cleanup removed
- `autoReleaseSceneAssets` flag - no auto-cleanup
- `ReleaseAll()` method - dangerous, unnecessary
- `ReleaseLabel()` method - dangerous, unnecessary
- `ReleaseSceneAssets()` method - dangerous, causes pink shaders
- `DeepCleanup()` method - dangerous Resources.UnloadUnusedAssets
- `IsLabelLoaded()` method - not reliable without handle tracking

**Kept:**
- Deduplication logic (prevents duplicate concurrent loads)
- Progress tracking for UI (GetCurrentSceneLoadProgress)
- Memory info methods (GetMemoryInfo)
- Scene transition tracking (SetCurrentScene, CompleteSceneTransition) - for logging only
- Active operations list (for debugging UI)

**Why:**
- Unity's Addressables already caches downloaded bundles on disk
- Unity's GC naturally unloads unused assets
- Manual release is dangerous (can release shared dependencies)
- Simplified from ~640 lines to ~280 lines

---

### 2. **AddressablesAssetLoader.cs** (Simplified)

**Removed:**
- `loadedAssets` Dictionary - duplicate tracking, unnecessary
- `ReleaseAsset()` method - dangerous
- `ReleaseAllAssets()` method - dangerous
- `OnDestroy()` cleanup logic - causes pink shaders

**Changed:**
- `LoadAssetAsync()` - removed local cache update logic
- `PreloadAsset()` - simplified, no cache checks

**Why:**
- Local caching duplicates Unity's Addressables cache
- Manual release methods are too dangerous to expose
- Simpler code is more maintainable

---

### 3. **PreloadManager.cs** (Simplified)

**Removed:**
- `ReleaseLabel()` method
- `ReleaseAll()` method
- `OnDestroy()` cleanup logic

**Why:**
- These methods were wrappers around dangerous operations
- Users shouldn't manually release assets
- Unity handles cleanup automatically

---

### 4. **AddressablesExamples.cs** (Updated)

**Changed:**
- Example 8 now explains Unity's automatic cleanup instead of calling ReleaseAllAssets

---

## What Unity Addressables Already Does

### Disk Caching (Browser Cache)
- Downloaded asset bundles are cached on disk by the browser
- Subsequent loads use the cached bundle (no re-download)
- Persists across sessions
- **You get this for free - no code needed**

### Memory Management
- Unity loads assets into memory when requested
- Unity's GC automatically unloads assets when no references exist
- Shared dependencies (materials, shaders) are reference-counted
- **You get this for free - no code needed**

### What We Actually Need
- **Deduplication**: Prevent multiple concurrent loads of the same asset
  - **We handle this** in AddressablesOperationManager
- **Progress Tracking**: Show download/load progress in UI
  - **We handle this** with GetCurrentSceneLoadProgress
- **Memory Monitoring**: Track current RAM usage
  - **We expose this** with GetMemoryInfo

---

## Migration Guide

### If You Were Using IsLabelLoaded()

**OLD CODE:**
```csharp
if (AddressablesOperationManager.Instance.IsLabelLoaded("Level1_Assets"))
{
    // Assets are loaded
}
```

**NEW APPROACH:**
```csharp
// Just load it - Unity will use the cached version if already loaded
AddressablesOperationManager.Instance.LoadLabel("Level1_Assets", (success) =>
{
    // Assets are ready (cached or freshly loaded)
});
```

**WHY:** Unity's Addressables automatically uses cached assets. No need to check.

---

### If You Were Using ReleaseLabel() or ReleaseAll()

**OLD CODE:**
```csharp
// Dangerous! Can release shared dependencies
AddressablesOperationManager.Instance.ReleaseLabel("Level1_Assets");
AddressablesOperationManager.Instance.ReleaseAll();
```

**NEW APPROACH:**
```csharp
// Do nothing! Unity's GC handles cleanup automatically
// Assets are unloaded when no longer referenced
```

**WHY:** Manual release is **extremely dangerous**:
- Can release shared shaders/materials → pink materials
- Can release Resources that multiple scenes depend on
- Unity's GC does this safely without breaking dependencies

---

### If You Were Using ReleaseSceneAssets()

**OLD CODE:**
```csharp
// Dangerous! Causes pink shaders
AddressablesOperationManager.Instance.ReleaseSceneAssets("Level1");
```

**NEW APPROACH:**
```csharp
// Do nothing! Let Unity's GC handle it
// When a scene unloads, Unity automatically marks its assets for cleanup
```

---

## Memory Management Strategy

### iOS Safari WebGL (300-500MB Limit)

**How We Stay Under Limit:**

1. **Browser Caches Bundles on Disk** (doesn't count toward RAM)
   - Downloaded bundles are cached by browser
   - Only RAM-loaded assets count toward 300-500MB limit

2. **Scene Transitions Naturally Clean Up**
   - When a scene unloads, its GameObjects are destroyed
   - Assets with no references are marked for GC
   - Unity automatically unloads unreferenced assets

3. **Shared Assets Are Safe**
   - Materials in Resources folders are persistent
   - Shaders are shared across scenes
   - Unity's reference counting prevents premature unload

4. **Monitor, Don't Micromanage**
   - Use `GetMemoryInfo()` to track RAM usage
   - If approaching limit, load smaller scenes/assets
   - Trust Unity's GC for cleanup

---

## Benefits of This Refactor

### 1. **No More Pink Shader Bugs**
- We don't manually release anything
- Unity's reference counting prevents shared dependency issues
- Materials and shaders stay loaded as long as needed

### 2. **Simpler Codebase**
- Removed ~360 lines of complex caching logic
- No more handle tracking confusion
- Clear separation: we do deduplication, Unity does caching

### 3. **Better Memory Behavior**
- Unity's GC is smarter than our manual tracking
- Assets unload naturally when scenes change
- Less risk of memory leaks from forgotten handles

### 4. **Maintainability**
- Less code = fewer bugs
- Clear comments explain what Unity handles vs what we handle
- Easier to onboard new developers

---

## Testing Checklist

After this refactor, verify:

### Basic Loading
- [ ] Labels load successfully (sprites, audio, etc.)
- [ ] Scenes load successfully
- [ ] No duplicate concurrent loads (deduplication works)

### Scene Transitions
- [ ] MainMenu → LevelSelection → Level1 (no pink shaders)
- [ ] Level1 → Video1 → Level2 (no pink shaders)
- [ ] Materials and shaders render correctly across all scenes

### Memory Behavior (iOS Safari)
- [ ] Build to WebGL, test on iPhone
- [ ] RAM stays under 400MB
- [ ] No crashes during extended play session
- [ ] Memory usage is similar or better than before refactor

### UI/UX
- [ ] Loading progress bars update correctly
- [ ] PreloadStatusIndicator shows accurate memory info
- [ ] No regressions in load times

---

## Documentation That Needs Updating

The following files reference removed methods and need updating:

1. **MEMORY_MANAGEMENT_GUIDE.md**
   - Remove references to ReleaseAll, ReleaseLabel, ReleaseSceneAssets
   - Update with new philosophy (Unity-managed caching)

2. **TESTING_CHECKLIST.md**
   - Remove IsLabelLoaded() checks
   - Update with new testing approach

3. **iOS_Debug_Guide.md** (check if affected)

4. **SETUP_GUIDE.md** (check if affected)

---

## Technical Details

### Why Manual Handle Tracking Failed

**The Problem:**
```csharp
// OLD CODE - Tracking handles manually
allLoadedHandles.Add(loadHandle);  // We stored this

// Later, in ReleaseAll():
Addressables.Release(loadHandle);  // Releases EVERYTHING this handle references
```

**What Went Wrong:**
- A single handle might reference a shader used by 5 other scenes
- Releasing it breaks those other scenes → pink shaders
- We can't track which handles are "shared" vs "scene-specific"

**Unity's Solution:**
- Unity tracks **reference counts** for every asset
- Assets are only unloaded when ref count = 0
- We don't need to manage this - Unity does it correctly

---

### Why Deduplication Still Matters

**The Problem We Solve:**
```csharp
// User loads Level1_Assets 3 times in quick succession
PreloadManager.Instance.PreloadLabel("Level1_Assets", callback1);
PreloadManager.Instance.PreloadLabel("Level1_Assets", callback2);
PreloadManager.Instance.PreloadLabel("Level1_Assets", callback3);
```

**Without Deduplication:**
- 3 simultaneous Addressables operations
- Downloads bundle 3 times (wastes bandwidth)
- Loads assets 3 times (wastes RAM temporarily)

**With Deduplication:**
- 1 Addressables operation
- Queue callbacks 2 and 3
- All callbacks fire when operation completes

**Result:** Faster, more efficient, less RAM churn

---

## Rollback Plan

If this refactor causes issues:

```bash
# Restore old versions
git checkout HEAD~1 Assets/Scripts/AddressablesOperationManager.cs
git checkout HEAD~1 Assets/Scripts/AddressablesAssetLoader.cs
git checkout HEAD~1 Assets/Scripts/PreloadManager.cs
```

However, consider whether the issue is:
1. A bug in the refactor → fix the bug
2. Fundamental misunderstanding → probably still a bug in old code

The old code's manual release logic **was the cause** of pink shaders.

---

## Conclusion

This refactor simplifies the codebase by trusting Unity's Addressables system to do what it's designed to do: manage asset caching and lifecycle. We focus on what Unity doesn't provide (deduplication) and let Unity handle what it does best (caching, reference counting, GC).

**Result:** Simpler, safer, more maintainable code.
