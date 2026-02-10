# Addressables Preloading Guide

## Understanding Download vs Preload

### Download (Disk Cache) ⬇️
- Downloads asset bundles from remote server to local cache
- **Does NOT load into RAM**
- Fast subsequent loads (no network needed)
- **No memory usage**
- **Use for:** Next level during gameplay, background preloading

### Preload (RAM) 🧠
- Downloads AND loads assets into memory
- **Instant access** (no loading time)
- **Uses RAM**
- **Use for:** Imminent content, frequently used assets

## Quick Setup

### 1. Organize Your Addressables into Groups

In Unity's Addressables Groups window (**Window → Asset Management → Addressables → Groups**):

**Create these groups:**
- `Level1_Group` - All assets for Level 1 scene
- `Level2_Group` - All assets for Level 2 scene  
- `Video1_Group` - All assets for Video 1 scene
- `Video2_Group` - All assets for Video 2 scene
- etc.

**What to include in each group:**
- The scene itself (e.g., Level1.unity)
- All sprites/textures used in that scene
- Audio files used in that scene
- Video files
- Prefabs spawned in that scene

### 2. Add PreloadManager to Your Scene

1. Create an empty GameObject in your **MainMenu** scene
2. Name it "PreloadManager"
3. Add the `PreloadManager` component
4. Check **Show Debug Logs** to see what's happening

### 3. Configure Preload Rules

In the PreloadManager Inspector, add preload configurations:

#### Example: Level 1 Configuration
```
Scene Name: Level1
Groups To Download: Video1_Group, Level2_Group
Groups To Preload: (empty unless needed)
```

This means:
- When in Level 1, it downloads Video 1 and Level 2 in the background
- They're cached on disk for instant loading later
- Not loaded into RAM yet (saves memory)

#### Example: Video 1 Configuration
```
Scene Name: Video1
Groups To Download: Level2_Group
Groups To Preload: (empty)
```

#### Example: Main Menu Configuration
```
Scene Name: MainMenu
Groups To Download: Level1_Group
Groups To Preload: (empty)
```

### 4. Add MainMenuPreloader (Optional but Recommended)

1. Add the `MainMenuPreloader` component to MainMenu
2. Check **Preload On Start**
3. Connect your Continue/New Game buttons to:
   - `OnContinueButtonPressed()`
   - `OnNewGameButtonPressed()`

This automatically preloads the correct level based on player's progress.

## Common Preload Patterns

### Pattern 1: Linear Level Progression
```
MainMenu → Downloads Level1_Group
Level1 → Downloads Video1_Group, Level2_Group
Video1 → Downloads Level2_Group (if not already cached)
Level2 → Downloads Video2_Group, Level3_Group
```

### Pattern 2: Hub-Based Game
```
MainMenu → Downloads Hub_Group
Hub → Downloads Level1_Group, Level2_Group, Level3_Group (player choice)
Level1 → Downloads Hub_Group (for return)
```

### Pattern 3: Video-Heavy Game
```
Level1 → Preloads Video1_Group (into RAM for instant playback)
Video1 → Downloads Level2_Group (background download during video)
```

## Manual Preloading (Advanced)

### From Any Script:

```csharp
// Download a group (to disk cache)
PreloadManager.Instance.DownloadGroup("Level2_Group", (success) => {
    if (success) Debug.Log("Level 2 ready!");
});

// Preload a group (into RAM)
PreloadManager.Instance.PreloadGroup("Video1_Group", (success) => {
    if (success) Debug.Log("Video 1 instantly available!");
});

// Download multiple groups at once
List<string> groups = new List<string> { "Level1_Group", "Level2_Group" };
PreloadManager.Instance.DownloadGroups(groups, null);

// Download just a scene (not a whole group)
PreloadManager.Instance.DownloadScene("Level3", null);

// Check download size before downloading
PreloadManager.Instance.GetGroupDownloadSize("Video1_Group", (bytes) => {
    float mb = bytes / 1024f / 1024f;
    Debug.Log($"Download size: {mb:F2} MB");
});

// Release a group from RAM (keeps disk cache)
PreloadManager.Instance.ReleaseGroup("Level1_Group");

// Release everything from RAM
PreloadManager.Instance.ReleaseAll();
```

## Understanding the "Already Cached" Message

**OLD BEHAVIOR (Your Current Issue):**
```csharp
// SceneTransitionManager.PreloadNextLevel() calls:
AddressablesSceneLoader.Instance.CacheSceneAssets("Level2");
// This only downloads the SCENE asset bundle, not its assets!
// Result: "Already cached" but loading is still slow
```

**NEW BEHAVIOR (PreloadManager):**
```csharp
PreloadManager.Instance.DownloadGroup("Level2_Group");
// Downloads the ENTIRE group including:
// - Scene asset bundle
// - All sprites/textures
// - All audio
// - All prefabs
// - All referenced assets
// Result: Everything is cached, truly fast loading
```

## Troubleshooting

### Still Says "Already Cached" But Slow Loading?

**Check these:**
1. Is your scene in a group? (Window → Addressables → Groups)
2. Are all assets used in that scene also in the group?
3. Are you calling `DownloadGroup()` not `DownloadScene()`?
4. Did you build Addressables? (Groups window → Build → New Build)

### Memory Usage Too High?

Use **Download** instead of **Preload**:
```csharp
// ❌ High memory usage
PreloadManager.Instance.PreloadGroup("Level2_Group");

// ✅ Low memory, still fast loading
PreloadManager.Instance.DownloadGroup("Level2_Group");
```

### Not Preloading Dependencies?

Make sure assets are marked as Addressable:
1. Select asset in Project window
2. Check **Addressable** in Inspector
3. Assign to the correct group

### Scene's Own Addressables Not Loading?

Example: Video1 scene references Video1.mp4 (which is addressable):

**Solution 1: Include in same group**
- Put Video1.mp4 in `Video1_Group` along with the scene

**Solution 2: Preload the asset manually**
```csharp
// In Video1 scene's Start() or Awake():
AddressablesAssetLoader.Instance.PreloadAsset<VideoClip>("Video1.mp4");
```

## Best Practices

### ✅ DO:
- Download next level during current level gameplay
- Group related assets together (scene + its assets)
- Use groups for easier management
- Download in background (async)
- Release old content when entering new sections

### ❌ DON'T:
- Preload everything at once (memory issues)
- Mix unrelated content in one group
- Forget to build Addressables before testing
- Block gameplay waiting for downloads

## Performance Tips

### For Slow Internet:
```csharp
// Show download size first
PreloadManager.Instance.GetGroupDownloadSize("Level2_Group", (bytes) => {
    if (bytes > 10 * 1024 * 1024) { // > 10 MB
        ShowDownloadPrompt($"Download {bytes / 1024f / 1024f:F1} MB?");
    } else {
        PreloadManager.Instance.DownloadGroup("Level2_Group");
    }
});
```

### For Fast Internet:
```csharp
// Aggressively preload multiple levels
PreloadManager.Instance.DownloadGroups(new List<string> {
    "Level1_Group",
    "Level2_Group", 
    "Level3_Group"
}, (success) => Debug.Log("First 3 levels ready!"));
```

### For Limited RAM (Mobile):
```csharp
// Only download, never preload
PreloadManager.Instance.DownloadGroup("NextLevel_Group");
// Load when needed:
SceneTransitionManager.Instance.LoadLevel(2);
```

### For High-End PC:
```csharp
// Preload into RAM for instant access
PreloadManager.Instance.PreloadGroup("NextLevel_Group");
// Instant loading:
SceneTransitionManager.Instance.LoadLevel(2);
```

## Example: Complete Level Flow

```csharp
// ===== MainMenu.cs =====
void Start() {
    // Determine which level to prep
    int level = GameProgressManager.Instance.GetCurrentLevel();
    PreloadManager.Instance.DownloadGroup($"Level{level}_Group");
}

// ===== Level1.cs (LevelManager) =====
void Start() {
    // Preload next video and level
    PreloadManager.Instance.PreloadForScene("Level1");
    // This uses the config you set in PreloadManager Inspector:
    // - Downloads Video1_Group (for upcoming video cutscene)
    // - Downloads Level2_Group (for next level)
}

// ===== Video1.cs =====
void Start() {
    // Download next level while video plays
    PreloadManager.Instance.DownloadGroup("Level2_Group");
}

void OnVideoEnd() {
    // Return to Level 1 or go to Level 2
    SceneTransitionManager.Instance.LoadLevel(2);
    // Loads instantly because we downloaded it!
}
```

## Migration from Old System

### Old Code (LevelManager.cs):
```csharp
if (preloadNextLevel) {
    SceneTransitionManager.Instance.PreloadNextLevel(levelNumber);
}
```

### New Code (LevelManager.cs):
```csharp
if (preloadNextLevel) {
    string currentSceneName = $"Level{levelNumber}";
    PreloadManager.Instance.PreloadForScene(currentSceneName);
}
```

The PreloadManager will use the configuration you set in the Inspector to download the correct groups!

---

## Summary

| Feature | Old System | New System |
|---------|-----------|------------|
| What's cached | Scene bundle only | Entire group (scene + assets) |
| Group support | ❌ No | ✅ Yes |
| Asset preloading | ❌ No | ✅ Yes |
| Download vs Load | Mixed | ✅ Separate |
| Nested dependencies | ❌ No | ✅ Yes |
| Multiple preloads | One at a time | ✅ Batch support |
| Configuration | Hard-coded | ✅ Inspector |

Now your addressables preloading will actually work properly! 🎉
