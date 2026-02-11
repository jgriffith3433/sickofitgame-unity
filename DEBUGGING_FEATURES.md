# Debugging Features Guide

## Overview
This guide covers all the debugging tools available for troubleshooting the loading issue on mobile (particularly iOS).

---

## 1. Mobile Debug Console

**Purpose**: View real-time logs directly on your device without external tools.

### Controls:
- **Mobile**: Triple-tap anywhere on screen to toggle console
- **Editor**: Press `P` key to toggle console

### Features:
- Shows all Unity Debug.Log, Debug.LogWarning, Debug.LogError messages
- Auto-scrolls to bottom when new messages arrive
- "Copy All Logs" button - copies entire log to clipboard for sharing
- Plain text output (no color tags)

### Setup:
1. Ensure `MobileDebugConsole.cs` exists in `Assets/Scripts/`
2. It auto-initializes on game start - no manual setup needed
3. Build and deploy to test device

---

## 2. Mobile Diagnostics

**Purpose**: Automatically runs system checks on startup to verify everything is configured correctly.

### What it checks:
- Addressables initialization
- Scene managers (AddressablesOperationManager, SceneLoadingManager, etc.)
- PreloadManager status
- PlayerPrefs save data
- Network connectivity
- Memory usage
- Platform information

### Output Format:
```
[OK] Addressables initialized
[WARN] SceneLoadingManager not found
[ERROR] Network unreachable
```

### Setup:
- Automatically runs on game start
- No manual setup needed
- View output in Mobile Debug Console (triple-tap/P key)

---

## 3. Preload Status Indicator

**Purpose**: Shows what Addressables operations are currently running in the background.

### Controls:
- **Toggle**: Press `O` key (for "Operations") to show/hide
- **Auto-hide**: Automatically hides when no operations are running

### Display Location:
- Top-right corner of screen
- Semi-transparent black background panel
- Shows operation names and progress percentages

### What it shows:
```
[Preload Status]

Preloading: Songs
Loading Scene: Video1
  Progress: 45.2%
```

### Setup:
1. Ensure `PreloadStatusIndicator.cs` exists in `Assets/Scripts/`
2. It auto-initializes on game start
3. Press `O` to toggle visibility

---

## 4. Loading Progress Display

**Purpose**: Shows the actual loading percentage on the loading screen.

### What changed:
- Previously, the loading screen showed a spinner but no percentage
- Now, the TextMeshProUGUI child of the loading screen shows: `"Loading... 45%"`
- Updates in real-time as the scene loads

### How it works:
- `AddressablesSceneLoader` polls progress from `AddressablesOperationManager`
- Updates the TextMeshProUGUI every frame with current progress
- Progress goes from 0% to 100%

### Setup:
- No manual setup needed
- Ensure your loading screen prefab has a TextMeshProUGUI component as a child

---

## 5. Comprehensive Logging

**Purpose**: Detailed logs throughout the entire loading pipeline to trace where it gets stuck.

### Log Tags:
- `[MOBILE DEBUG]` - Debug console status
- `[MobileDiagnostics]` - System checks
- `[AddressablesSceneLoader]` - Scene loading flow
- `[AddressablesOperationManager]` - Addressables operations tracking
- `[SceneLoadingManager]` - Loading UI activation/deactivation
- `[PreloadStatusIndicator]` - Status indicator visibility changes

### Key logs to watch:
1. **Scene load starts**: `"[AddressablesSceneLoader] Loading scene: <name>"`
2. **Progress updates**: `"[AddressablesSceneLoader] Progress: X%"` (every 10%)
3. **Loading UI activation**: `"[SceneLoadingManager] Activating loading UI"`
4. **Timeout detection**: `"[AddressablesOperationManager] Operation timed out after 60s"`
5. **Scene load complete**: `"[AddressablesSceneLoader] Scene loaded successfully"`

---

## Testing Workflow

### In Unity Editor:
1. Press `P` to open debug console
2. Press `O` to show preload status indicator
3. Click "New Game" button
4. Watch logs in console to see loading flow
5. Check if progress percentage updates
6. Note where it stops if it gets stuck

### On iOS Device:
1. Build to WebGL
2. Deploy to hosting service
3. Open on iOS device
4. Triple-tap screen to open debug console
5. Tap "New Game"
6. Watch logs to see where loading stops
7. Use "Copy All Logs" button to share logs

### What to look for if stuck:
- Last log message before freeze
- Progress percentage it reached
- Whether timeout triggered (60s for scenes, 90s for labels)
- Whether loading UI activated correctly
- Whether scene name is correct in logs

---

## Troubleshooting

### Console won't open:
- Ensure `MobileDebugConsole.cs` is in `Assets/Scripts/`
- Check Editor Console for initialization errors
- Try changing `InEditor` flag if testing in editor

### No logs appearing:
- Check if `MobileDiagnostics.cs` ran (should see diagnostics on startup)
- Verify Debug.Log statements aren't stripped in build settings
- Check Unity Player Settings → Other Settings → Stack Trace Log Type

### Progress not updating:
- Ensure loading screen prefab has TextMeshProUGUI child
- Check `AddressablesSceneLoader.UpdateLoadingProgress()` is being called
- Verify `GetCurrentSceneLoadProgress()` returns valid progress (0-1)

### Preload indicator not showing:
- Press `O` key to toggle visibility
- Ensure operations are actually running (check `GetActiveOperations()`)
- Check if `PreloadStatusIndicator` initialized in logs

---

## File Locations

```
Assets/Scripts/
├── MobileDebugConsole.cs         # On-screen console
├── MobileDiagnostics.cs          # Auto diagnostics  
├── PreloadStatusIndicator.cs     # Preload status display
├── AddressablesSceneLoader.cs    # Scene loading (modified)
├── AddressablesOperationManager.cs  # Operation tracking (modified)
└── SceneLoadingManager.cs        # Loading UI manager (modified)
```

---

## Quick Reference

| Feature | Mobile Control | Editor Control | Purpose |
|---------|---------------|----------------|---------|
| Debug Console | Triple-tap | `P` key | View logs |
| Preload Indicator | Auto-shows | `O` key | Show operations |
| Progress Display | Auto-shows | Auto-shows | Loading % |
| Copy Logs | Tap button | Click button | Share logs |

---

## Next Steps

1. Test in Unity Editor first (press `P` and `O`)
2. Click "New Game" and watch logs
3. If it works in editor, build to WebGL
4. Test on iOS device (triple-tap for console)
5. If stuck, copy logs and review where it stopped
6. Check last progress percentage reached
7. Look for timeout messages or error logs
