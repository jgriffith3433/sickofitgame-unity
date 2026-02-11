# Memory Management Testing Checklist

## Pre-Test Setup

### Unity Editor
- [ ] Build Addressables content
- [ ] Open MainMenu scene
- [ ] Press `P` to verify debug console works
- [ ] Press `O` to verify memory monitor works

### Build Settings
- [ ] WebGL platform selected
- [ ] Development Build enabled (for testing)
- [ ] Addressables set to "Use Existing Build"

---

## Editor Testing

### Test 1: Memory Monitor Visibility
- [ ] Press `O` key
- [ ] Memory Status panel appears in top-right
- [ ] Shows: Total Alloc, Reserved, Mono, Platform Limit
- [ ] Updates every 0.5 seconds
- [ ] Press `O` again to hide

### Test 2: Debug Console
- [ ] Press `P` key
- [ ] Console opens with existing logs
- [ ] Shows MobileDiagnostics startup checks
- [ ] Copy button works
- [ ] Press `P` again to close

### Test 3: Scene Transition - New Game
**Steps**:
1. Press `O` to show memory monitor
2. Press `P` to show console
3. Note starting memory (should be ~50-100MB)
4. Click "New Game"
5. Watch console logs

**Expected Console Logs**:
```
[SceneTransitionManager] LoadVideo(1) called
[SCENE TRANSITION] MainMenu -> Video1
[AddressablesSceneLoader] Loading scene: Video1
[Memory Status] shows memory increase as Video1_Assets load
```

**Expected Memory**:
- Before: ~50-100MB
- After Video1 loads: ~150-200MB
- Should NOT show MainMenu still loaded

### Test 4: Scene Transition - Video to Level
**Steps**:
1. Let Video1 play (or skip it)
2. Transition to Level1
3. Watch console logs
4. Watch memory monitor

**Expected Console Logs**:
```
[SCENE TRANSITION] Video1 -> Level1
[MEMORY] Releasing assets for scene: Video1
[MEMORY] Released label from memory: Video1_Assets
[MEMORY] Completed releasing assets for: Video1
[AddressablesSceneLoader] Loading scene: Level1
```

**Expected Memory**:
- Before transition: ~150-200MB (Video1 loaded)
- During transition: Brief drop as Video1 releases
- After Level1 loads: ~150-250MB
- Video1_Assets should be GONE

### Test 5: Multiple Transitions
**Steps**:
1. Play through: MainMenu → Video1 → Level1 → Level2 → Level3
2. Monitor memory at each transition
3. Check console logs for cleanup

**Expected Pattern**:
```
MainMenu (50MB)
  → Video1 loads (+100MB) = 150MB
  → Video1 releases, Level1 loads = 200MB
  → Level1 releases, Level2 loads = 200MB
  → Level2 releases, Level3 loads = 200MB
```

**Memory should NEVER exceed 300MB**

### Test 6: Manual Memory Check
**Steps**:
1. Open console with `P`
2. Load Level1
3. Run this in console (if you have a debug command system):
```csharp
AddressablesOperationManager.Instance.IsLabelLoaded("Level1_Assets")
// Should return: true

AddressablesOperationManager.Instance.IsLabelLoaded("Video1_Assets")
// Should return: false (was released)
```

---

## WebGL Testing (Local)

### Build Steps
1. Build → WebGL
2. Build And Run (starts local server)
3. Open in browser
4. Press `O` for memory monitor
5. Right-click console → Inspect (to see Unity logs)

### Test 1: Desktop Browser
- [ ] Chrome: Works smoothly
- [ ] Firefox: Works smoothly
- [ ] Safari: Works smoothly
- [ ] Memory stays under 300MB
- [ ] No warnings/errors in browser console

### Test 2: Transitions
- [ ] Play through Video1 → Level1 → Level2
- [ ] Check browser's Task Manager for memory usage
- [ ] Should see memory drop after each transition
- [ ] No memory warnings

---

## iOS Safari Testing (Critical)

### Deploy Steps
1. Build WebGL (Release mode)
2. Upload to hosting (e.g., GitHub Pages, Netlify, etc.)
3. Open URL on iOS device in Safari
4. Triple-tap to open debug console

### Test 1: Basic Load
- [ ] Game loads without errors
- [ ] Triple-tap opens console
- [ ] Console shows diagnostic logs
- [ ] Tap "O" would show memory monitor (if keyboard accessible)

### Test 2: Single Level Playthrough
**Steps**:
1. Triple-tap for console
2. Click "New Game"
3. Watch console for cleanup logs
4. Play through Video1
5. Play through Level1

**Success Criteria**:
- [ ] No page reloads
- [ ] Console shows cleanup logs: `[MEMORY] Releasing assets for scene: Video1`
- [ ] Game runs smoothly
- [ ] No Safari warnings

### Test 3: Extended Playthrough
**Steps**:
1. Clear Safari cache first (Settings > Safari > Clear History)
2. Open game fresh
3. Play through: Video1 → Level1 → Level2 → Level3 → Level4
4. Take breaks between levels to see memory monitor

**Success Criteria**:
- [ ] **NO PAGE RELOADS** (this was the main issue)
- [ ] No "A problem repeatedly occurred" errors
- [ ] Smooth transitions between scenes
- [ ] Memory Monitor (if visible) shows stable memory usage

### Test 4: Stress Test
**Steps**:
1. Rapidly transition: MainMenu → Level1 → MainMenu → Level2 → MainMenu → Level3
2. Force memory pressure by loading different scenes
3. Open other Safari tabs in background

**Success Criteria**:
- [ ] No crashes
- [ ] No page reloads
- [ ] Console shows cleanup happening
- [ ] Game remains playable

### Test 5: Long Session
**Steps**:
1. Play through 5+ levels
2. Return to main menu
3. Start new game
4. Play through again

**Success Criteria**:
- [ ] Memory doesn't accumulate over time
- [ ] No progressive slowdown
- [ ] No crashes after extended play

---

## Debugging Failed Tests

### If Memory Keeps Growing:
1. Check console logs - are cleanup logs appearing?
2. Verify cleanup code runs:
   ```
   Look for: "[MEMORY] Releasing assets for scene: X"
   ```
3. If NOT appearing:
   - Check SceneTransitionManager calls `SetCurrentScene()`
   - Verify scene name matches pattern (Level1, Video1, etc.)

### If Page Still Reloads:
1. Check how much memory before reload:
   - Look at memory monitor last value
   - iOS Safari limit is ~300-500MB
2. If over 300MB:
   - Assets aren't being released properly
   - Check if label names match (Level1_Assets, etc.)
3. If under 300MB:
   - Might be different issue (memory spike, JS error, etc.)

### If Labels Don't Release:
1. Verify Addressables setup:
   - Assets are tagged with "Level1_Assets" label
   - Label name matches scene name + "_Assets"
2. Check ReleaseLabel logs:
   ```
   Should see: "[MEMORY] Released label from memory: Level1_Assets"
   ```
3. If not releasing:
   - Check loadedLabels dictionary contains the label
   - Verify label name exactly matches

---

## Performance Metrics

### Target Memory Usage (WebGL/iOS):
- **MainMenu**: 50-100MB
- **Video Scene**: 150-200MB
- **Level Scene**: 150-250MB
- **Peak (during transition)**: < 300MB
- **Never exceed**: 300MB (iOS Safari limit)

### Target Load Times:
- **First Load**: 5-15 seconds (download + load)
- **Cached Load**: 1-3 seconds (load from cache)
- **Scene Transition**: 2-5 seconds (with loading screen)

### Target Cleanup Times:
- **ReleaseSceneAssets**: < 500ms
- **GC.Collect**: < 200ms (during transition, not noticeable)

---

## Sign-Off Checklist

### Before Declaring Victory:
- [ ] Editor tests all pass
- [ ] WebGL local tests all pass
- [ ] iOS Safari basic test passes (no reload)
- [ ] iOS Safari extended test passes (5+ levels)
- [ ] iOS Safari stress test passes
- [ ] Memory monitor shows stable usage
- [ ] Console logs show cleanup happening
- [ ] No errors in browser console
- [ ] Performance feels smooth

### Verification on Multiple iOS Devices:
- [ ] iPhone (newest iOS)
- [ ] iPhone (older iOS if possible)
- [ ] iPad (if available)

---

## Rollback Plan

### If Tests Fail:
1. **Revert changes**:
   ```bash
   git checkout HEAD~1 AddressablesOperationManager.cs
   git checkout HEAD~1 SceneTransitionManager.cs
   git checkout HEAD~1 MainMenuPreloader.cs
   git checkout HEAD~1 VideoManager.cs
   git checkout HEAD~1 PreloadStatusIndicator.cs
   ```

2. **Identify which change caused issue**:
   - Test with only cleanup (AddressablesOperationManager + SceneTransitionManager)
   - Test with only disabled preloading
   - Test with only memory monitor

3. **Incremental fix**:
   - Apply changes one at a time
   - Test after each change
   - Keep what works, fix what doesn't

---

## Success Criteria Summary

✅ **Primary Goal**: No page reloads on iOS Safari
✅ **Secondary Goal**: Memory stays under 250MB
✅ **Tertiary Goal**: Smooth gameplay experience

**If ALL these are met, the refactor is successful!**
