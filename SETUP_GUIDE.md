# 🚀 Quick Setup Guide - Mobile Loading Debug Fix

## What I've Done

I've added comprehensive mobile debugging tools to help diagnose and fix your "stuck on Loading" issue on iOS.

---

## 🆕 New Files Created

### 1. **MobileDebugConsole.cs**
- On-screen debug console that shows all Unity logs
- **How to use:** Triple-tap anywhere in your game to toggle visibility
- Logs appear at the bottom half of the screen
- No Mac or external tools needed!

### 2. **MobileDiagnostics.cs**
- Runs automatically when your game starts
- Checks for common mobile WebGL issues:
  - Addressables setup
  - Network connectivity
  - System resources
  - Manager initialization
- All findings are logged to console

### 3. **iOS_Debug_Guide.md**
- Complete guide on viewing logs on iOS without a Mac
- Multiple solutions from easiest to most advanced
- Troubleshooting checklist

### 4. **WebGL_Template_With_Debug.html**
- Optional HTML template with Eruda mobile console built-in
- Shows logs in a floating panel
- Can replace your default WebGL template

---

## 📝 Files Modified (Enhanced Logging)

### 1. **AddressablesOperationManager.cs**
- Added `[MOBILE DEBUG]` logs for scene and label loading
- Progress tracking (shows percentage)
- 60-second timeout detection for scenes
- 90-second timeout detection for labels
- Detailed error reporting with full exception details

### 2. **MainMenuManager.cs**
- Logs when "New Game" is clicked
- Shows which loading path is taken

### 3. **SceneLoadingManager.cs**
- Logs UI activation
- Shows which GameObjects are activated/deactivated

### 4. **SceneTransitionManager.cs**
- Logs scene name and loading method
- Shows whether Addressables or Unity scene loading is used

---

## ✅ What to Do Next

### Step 1: Build Your Game
1. Open Unity
2. Make sure all new scripts are in your project (they should be in Assets/Scripts/)
3. Go to **File > Build Settings**
4. Select **WebGL** platform
5. Click **Build** or **Build and Run**

### Step 2: Test on iOS

#### Option A: Use Built-in Console (RECOMMENDED)
1. Open your WebGL game on your iPhone/iPad
2. **Triple-tap** anywhere on the screen
3. You'll see the debug console appear at the bottom
4. Click "New Game"
5. Watch the logs scroll

#### Option B: Use Safari Console
1. On iOS: **Settings > Safari > Advanced > Enable Web Inspector**
2. Open your game in Safari
3. Tap URL bar > Scroll down > Tap **"<>"** icon
4. Tap **Console**
5. Click "New Game" in your game
6. View logs

### Step 3: Look for These Logs

When you click "New Game", you should see:

```
[MOBILE DEBUG] New Game button clicked!
[MOBILE DEBUG] Attempting to load Video1...
[MOBILE DEBUG] SceneLoadingManager.LoadVideo(1) called
[MOBILE DEBUG] Starting scene load: 'Video1'...
[MOBILE DEBUG] Platform: WebGLPlayer, Internet: ReachableViaLocalAreaNetwork
[MOBILE DEBUG] Scene load progress: 10.0%
[MOBILE DEBUG] Scene load progress: 25.0%
...
```

### Step 4: Identify the Problem

#### If you see:
❌ **"Scene load timeout after 60s!"**
- **Problem:** Scene is taking too long to download
- **Fix:** Your Video1 bundle is too large or network is slow
- **Solution:** Reduce bundle size or increase timeout in AddressablesOperationManager.cs

❌ **"Failed to load scene 'Video1'"**
- **Problem:** Scene not found in Addressables
- **Fix:** Check Addressables Groups window
- **Solution:** Make sure Video1 scene is added to an Addressable group

❌ **"Internet: NotReachable"**
- **Problem:** No network connection
- **Fix:** Check iOS network settings for the browser

❌ **"Scene load progress: 0.0%" (stuck at 0%)**
- **Problem:** Download not starting
- **Fix:** Check that Addressables remote URL is accessible
- **Solution:** Test your hosting URL in a browser

✅ **"Successfully loaded scene: Video1"**
- Scene loaded correctly! Issue is elsewhere.

---

## 🔧 Common Fixes

### Fix 1: Addressables Not Set Up
```
Window > Asset Management > Addressables > Groups
- Find Video1 scene
- Make sure it's in a group (e.g., "Video1_Assets")
- Set Build & Load Paths to "Remote"
- Build > New Build > Default Build Script
```

### Fix 2: Bundle Too Large
Check the size of your Video1 bundle:
```
- Find ServerData/WebGL/StandaloneWindows64/ (or similar)
- Check .bundle file sizes
- If over 50MB, consider compressing or splitting content
```

### Fix 3: Disable Addressables for Testing
```csharp
// In Unity Editor, find SceneTransitionManager GameObject
// Uncheck "Use Addressables"
// Add Video1 to Build Settings > Scenes In Build
// Rebuild and test
```

### Fix 4: Increase Timeout
If your network is slow but loading does eventually complete:

Edit `AddressablesOperationManager.cs`:
```csharp
// Line ~230 (in LoadSceneAsync)
float timeout = 60f; // Change to 120f or 180f

// Line ~195 (in LoadLabelAsync)
float timeout = 90f; // Change to 180f or 300f
```

---

## 📊 Diagnostics Report

When your game starts, you'll also see a diagnostics report showing:
```
========================================
🔍 MOBILE DIAGNOSTICS REPORT
========================================
Platform: WebGLPlayer
Device: iPhone 14
Screen: 1170x2532 @ 460dpi
Internet: ReachableViaLocalAreaNetwork
✅ Video1_Assets is cached locally
✅ SceneTransitionManager found
✅ AddressablesSceneLoader found
========================================
```

This helps identify issues before they happen!

---

## 🆘 Still Not Working?

If you still see infinite loading after implementing these changes:

1. **Screenshot the MobileDebugConsole** (triple-tap to show)
2. **Note the last log message** before it freezes
3. **Check for red error messages**
4. **Share the logs** and I can help diagnose

---

## 💡 Pro Tips

1. **Keep MobileDebugConsole enabled** during testing
2. **Test on WiFi first** then cellular to isolate network issues
3. **Check browser console** in addition to Unity logs
4. **Screenshots are your friend** - capture logs for review
5. **Test locally** before deploying to server

---

## 🎯 Expected Behavior

With these changes, your loading should:

1. Show loading UI immediately when "New Game" is clicked
2. Log progress every 10% (10%, 20%, 30%, etc.)
3. Complete in under 60 seconds (or show timeout error)
4. Either load Video1 scene successfully OR show a clear error message

No more silent failures! 🎉

---

## 📁 File Locations

All files are in your Unity project:
```
Assets/Scripts/
  ├── MobileDebugConsole.cs          [NEW]
  ├── MobileDiagnostics.cs           [NEW]
  ├── AddressablesOperationManager.cs [MODIFIED]
  ├── MainMenuManager.cs              [MODIFIED]
  ├── SceneLoadingManager.cs          [MODIFIED]
  └── SceneTransitionManager.cs       [MODIFIED]

Root/
  ├── iOS_Debug_Guide.md             [NEW]
  └── WebGL_Template_With_Debug.html [NEW - Optional]
```

---

## ✨ Quick Test Procedure

1. ✅ Build WebGL
2. ✅ Open on iOS device
3. ✅ Triple-tap screen → See console
4. ✅ Read diagnostics report
5. ✅ Click "New Game"
6. ✅ Watch logs scroll
7. ✅ Identify where it stops
8. ✅ Apply appropriate fix

Good luck! With these tools, you'll be able to see exactly what's happening on your iOS device! 🚀
