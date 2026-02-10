# Addressables Preloading - Quick Reference

## 🎯 The Problem You Had

**"Already cached" but still loading slowly?**

The old system only cached the **scene container**, not the **assets inside**.

```
❌ OLD: CacheSceneAssets("Level2")
   → Only downloads Level2.unity bundle
   → Assets inside Level2 NOT preloaded
   → Still loads sprites/audio at runtime = SLOW

✅ NEW: DownloadGroup("Level2_Group")
   → Downloads scene + all assets in group
   → Everything cached for instant loading
   → No runtime loading = FAST
```

---

## 📦 Core Concepts

### Download vs Preload

| Operation | Disk Cache | RAM | Speed | Memory |
|-----------|----------|-----|-------|--------|
| **Download** | ✅ Yes | ❌ No | Fast | Low |
| **Preload** | ✅ Yes | ✅ Yes | Instant | High |

**Use Download for:** Next level during gameplay (won't use memory)
**Use Preload for:** Imminent content (instant access, uses memory)

---

## 🚀 Quick Start (5 Minutes)

### 1. Create Groups in Unity
**Window → Asset Management → Addressables → Groups**

Create these groups:
- `Level1_Group` → Add Level1.unity + all Level1 assets
- `Level2_Group` → Add Level2.unity + all Level2 assets
- `Video1_Group` → Add Video1.unity + all Video1 assets

### 2. Add PreloadManager to MainMenu
- Create empty GameObject
- Add `PreloadManager` component
- Check "Show Debug Logs"

### 3. Configure Preload Rules (Inspector)
```
📋 Preload Config #1:
Scene Name: Level1
Groups To Download: Video1_Group, Level2_Group

📋 Preload Config #2:
Scene Name: Video1
Groups To Download: Level2_Group

📋 Preload Config #3:
Scene Name: MainMenu
Groups To Download: Level1_Group
```

### 4. Build Addressables
**Groups window → Build → New Build → Default Build Script**

### 5. Test!
Play your game and watch Console for:
```
[PreloadManager] Downloading group 'Level2_Group': 15.43 MB
[PreloadManager] Successfully cached Level2_Group
```

---

## 💻 Code Usage

### Automatic (Recommended)
```csharp
// In LevelManager.cs or any manager script:
PreloadManager.Instance.PreloadForScene("Level1");
// Uses Inspector configuration automatically
```

### Manual Download (Disk Cache)
```csharp
// Download a group (no RAM usage)
PreloadManager.Instance.DownloadGroup("Level2_Group", (success) => {
    Debug.Log("Level 2 ready!");
});

// Download multiple groups
PreloadManager.Instance.DownloadGroups(
    new List<string> { "Level2_Group", "Level3_Group" },
    (success) => Debug.Log("Levels ready!")
);
```

### Manual Preload (Into RAM)
```csharp
// Preload into RAM (instant access)
PreloadManager.Instance.PreloadGroup("BossAssets_Group", (success) => {
    // Now boss spawns instantly!
    SpawnBoss();
});
```

### Check Size First
```csharp
PreloadManager.Instance.GetGroupDownloadSize("Level5_Group", (bytes) => {
    float mb = bytes / 1024f / 1024f;
    if (mb > 50) {
        ShowWarning($"Large download: {mb:F1} MB");
    }
});
```

### Release Memory
```csharp
// Release one group
PreloadManager.Instance.ReleaseGroup("Level1_Group");

// Release all (keeps disk cache)
PreloadManager.Instance.ReleaseAll();
```

---

## 🎮 Common Patterns

### Pattern: Main Menu
```csharp
void Start() {
    // Preload player's current level in background
    int level = GameProgressManager.Instance.GetCurrentLevel();
    PreloadManager.Instance.DownloadGroup($"Level{level}_Group");
}
```

### Pattern: Level to Video Transition
```csharp
// In Level1:
void Start() {
    // Downloads Video1 while playing Level1
    PreloadManager.Instance.DownloadGroup("Video1_Group");
}

// Later when win condition met:
void OnLevelComplete() {
    // Video1 already cached, loads instantly!
    SceneTransitionManager.Instance.LoadVideo(1);
}
```

### Pattern: Video to Next Level
```csharp
// In Video1 (VideoManager):
void Start() {
    // Downloads Level2 while video plays
    PreloadManager.Instance.DownloadGroup("Level2_Group");
}

void OnVideoEnd() {
    // Level2 already cached, loads instantly!
    SceneTransitionManager.Instance.LoadLevel(2);
}
```

### Pattern: Boss Fight
```csharp
void OnBossTriggerEnter() {
    // Preload boss into RAM for instant spawn
    PreloadManager.Instance.PreloadGroup("BossAssets_Group", (success) => {
        ShowBossIntro();
        SpawnBoss(); // Instant!
    });
}
```

---

## 🐛 Troubleshooting

### Still saying "already cached"?
✅ **This is GOOD now!** It means it was downloaded before.

### Still loading slowly?
❌ Check these:
1. Is your scene in a **group**? (not Default Local Group)
2. Are all scene's assets in that **same group**?
3. Did you **build Addressables**? (Groups → Build → New Build)
4. Are you calling `DownloadGroup()` not `CacheSceneAssets()`?

### High memory usage?
```csharp
// ❌ Don't preload everything
PreloadManager.Instance.PreloadGroup("HugeLevel_Group"); // Uses lots of RAM

// ✅ Just download
PreloadManager.Instance.DownloadGroup("HugeLevel_Group"); // No RAM usage
```

### Not preloading at all?
1. Check PreloadManager has "Show Debug Logs" enabled
2. Look for `[PreloadManager]` messages in Console
3. Verify group names match exactly (case-sensitive!)
4. Make sure PreloadManager GameObject is in scene

---

## 📊 Expected Results

### Before:
- Level load: **5-10 seconds** ⏱️
- "Already cached" but **still loading** 😕
- Download every asset at runtime 📶

### After:
- Level load: **< 1 second** ⚡
- "Already cached" and **actually cached** 😊
- Assets loaded from disk cache 💾

---

## 🎯 Your Specific Setup

### Level1 → Video1 → Level2
```
MainMenu (starts):
  ↓ Downloads: Level1_Group
Level1 (starts):
  ↓ Downloads: Video1_Group, Level2_Group
Player beats Level1:
  ↓ Loads Video1 (instant - already cached!)
Video1 (plays):
  ↓ Downloads: Level2_Group (if not cached)
  ↓ (Downloads during video playback)
Video1 ends:
  ↓ Loads Level2 (instant - already cached!)
Level2 (starts):
  ↓ Downloads: Video2_Group, Level3_Group
...and so on
```

### MainMenu Continue/New Game
```csharp
// MainMenuPreloader.cs handles this automatically:

Continue Button:
  → Gets current level from save
  → Downloads that level's group
  → Loads when ready

New Game Button:
  → Downloads Level1_Group
  → Loads when ready
```

---

## 🔑 Key Differences

| Old System | New System |
|-----------|-----------|
| `CacheSceneAssets("Level2")` | `DownloadGroup("Level2_Group")` |
| Scene only | Scene + all assets |
| Hard-coded | Inspector config |
| No groups | ✅ Group support |
| No RAM preload | ✅ Download vs Preload |
| Unclear state | ✅ Debug logs |

---

## 📝 Checklist

Before testing:
- [ ] Created groups (Level1_Group, Level2_Group, etc.)
- [ ] Added all assets to their groups
- [ ] Added PreloadManager to MainMenu
- [ ] Configured preload rules in Inspector
- [ ] Built Addressables (Groups → Build)
- [ ] Enabled "Show Debug Logs" on PreloadManager
- [ ] Updated LevelManager to use `PreloadForScene()`
- [ ] (Optional) Added MainMenuPreloader
- [ ] (Optional) Updated VideoManager with video number

After testing:
- [ ] See `[PreloadManager]` logs in Console
- [ ] Levels load quickly after preloading
- [ ] No "loading" delays after download
- [ ] Memory usage is acceptable

---

## 🎉 You're Done!

Your game now has:
- ✅ True preloading (not just scene bundles)
- ✅ Group-based organization
- ✅ Smart background downloading
- ✅ Instant level transitions
- ✅ Configurable preload rules
- ✅ Download vs RAM choice
- ✅ Proper dependency handling

**No more "already cached" confusion!** 🚀

---

## Need Help?

1. **Check Console** - Enable "Show Debug Logs" on PreloadManager
2. **Check Groups** - Window → Addressables → Groups
3. **Build Addressables** - Groups → Build → New Build
4. **Test Debug UI** - Add `PreloadDebugUI.cs` to test manually
5. **Read Full Guide** - See `ADDRESSABLES_PRELOADING_GUIDE.md`

Press **F1** in game with PreloadDebugUI to test preloading manually!
