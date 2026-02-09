# WebGL Build Size Optimization - Quick Reference

## Summary

✅ **System now supports Addressables for optimized WebGL builds**  
✅ **Initial load: ~1-10 MB** (just main menu)  
✅ **Levels load on-demand** (5-20 MB each)  
✅ **Automatic browser caching**  
✅ **Background preloading** for instant level transitions

---

## What Was Added

### Core Systems:
1. **AddressablesSceneLoader.cs** - Handles on-demand scene loading
2. **AddressablesAssetLoader.cs** - Handles runtime prefab/asset loading (NEW!)
3. **AddressablePrefabReference.cs** - Drop-in replacement for prefab references (NEW!)
4. **SceneDownloadPrompt.cs** - (Optional) Shows download size/progress to players
5. **SceneTransitionManager.cs** - Updated to support both modes

### Documentation:
- **ADDRESSABLES_SETUP.md** - Scene loading setup
- **ADDRESSABLES_ASSETS.md** - Runtime asset loading guide (NEW!)
- **WEBGL_OPTIMIZATION.md** - This file

---

## Important: Two Types of Addressables

### 1. Scenes (Levels)
**What:** Entire game scenes  
**Setup:** Mark scene as Addressable  
**Loading:** Use `AddressablesSceneLoader`  
**Guide:** [ADDRESSABLES_SETUP.md](ADDRESSABLES_SETUP.md)

### 2. Runtime Assets (Prefabs, Materials, etc.)
**What:** Assets you spawn/load during gameplay  
**Setup:** Mark individual assets as Addressable  
**Loading:** Use `AddressablesAssetLoader`  
**Guide:** [ADDRESSABLES_ASSETS.md](ADDRESSABLES_ASSETS.md)

**Both are needed for optimal WebGL builds!**

---

## Quick Setup (5 minutes)

### 1. Install Addressables
- Window → Package Manager → Add by name: `com.unity.addressables`

### 2. Mark Scenes as Addressable
- Select all scenes EXCEPT MainMenu
- Check "Addressable" in Inspector

### 3. Mark Runtime Assets as Addressable
- Select prefabs you spawn at runtime (enemies, powerups, etc.)
- Check "Addressable" in Inspector
- See [ADDRESSABLES_ASSETS.md](ADDRESSABLES_ASSETS.md) for details

### 4. Remove Scenes from Build
- File → Build Settings
- Remove all scenes except MainMenu

### 5. Enable in SceneTransitionManager
- Find SceneTransitionManager in your scene
- Check "Use Addressables" ✓

### 6. Build Addressables
- Window → Asset Management → Addressables → Groups
- Build → New Build → Default Build Script

### 7. Build WebGL
- File → Build Settings → WebGL → Build

Done! Your initial build is now tiny.

---

## Results You Can Expect

### Before:
```
Initial Download: 150 MB
Load Time: 60 seconds
```

### After:
```
Initial Download: 5 MB
Load Time: 3 seconds
Level 1 Download: 10 MB (first time only)
Level 1 Load: 5 seconds (instant after cached)
```

---

## Bonus Features

### Preload Next Level (Already Implemented!)
The LevelManager automatically downloads the next level in the background while you play. This means:
- Player starts Level 1
- Level 2 downloads in background
- When Level 1 completes, Level 2 loads instantly!

### Show Download Size (Optional)
Add SceneDownloadPrompt to level selection to show download sizes:
```csharp
SceneDownloadPrompt prompt = GetComponent<SceneDownloadPrompt>();
prompt.ShowDownloadPrompt("Level5", 
    onConfirm: () => LoadLevel(5),
    onCancel: () => ReturnToMenu()
);
```

### Load Runtime Assets On-Demand
Spawn prefabs without bloating your initial build:
```csharp
// Old way: [SerializeField] private GameObject enemyPrefab;
// New way:
AddressablesAssetLoader.Instance.InstantiatePrefab("Enemy_Basic", (enemy) =>
{
    // Enemy spawned!
});
```
See [ADDRESSABLES_ASSETS.md](ADDRESSABLES_ASSETS.md) for 12+ examples!

---

## Production Deployment

### Option 1: Same Server (Simple)
1. Build Addressables
2. Build WebGL
3. Upload everything to your web host
4. Works automatically!

### Option 2: CDN (Best Performance)
1. Upload `ServerData/WebGL/` to CDN (e.g., AWS CloudFront)
2. Configure Addressables Load Path to CDN URL
3. Rebuild Addressables
4. Build WebGL and upload to game server
5. Assets load from CDN, game from your server

CDN gives you:
- ✅ Faster downloads worldwide
- ✅ Reduced server costs
- ✅ Better scalability
- ✅ Automatic compression/optimization

---

## Testing Addressables Locally

Use a local web server (WebGL won't work with file:// protocol):

**Python:**
```bash
cd Build
python -m http.server 8000
# Visit: http://localhost:8000
```

**Node.js:**
```bash
npx http-server Build -p 8000
```

**Unity:**
Just use "Build and Run" - Unity creates a local server automatically

---

## Switching Between Modes

You can toggle between traditional and Addressables loading:

**Traditional (All scenes in build):**
- SceneTransitionManager: Uncheck "Use Addressables"
- Scenes in Build Settings: Add all scenes
- Larger build, simpler setup

**Addressables (On-demand loading):**
- SceneTransitionManager: Check "Use Addressables"  
- Scenes in Build Settings: Only MainMenu
- Smaller build, requires Addressables setup

Both work with the same game code!

---

## Common Questions

**Q: Do I need to rebuild Addressables every time?**
A: Only when you change scenes/assets. Regular code changes don't require Addressables rebuild.

**Q: Can I use this for standalone builds too?**
A: Yes! Addressables work on all platforms. Great for reducing download sizes everywhere.

**Q: What if a player has slow internet?**
A: Show download size with SceneDownloadPrompt. They'll see "Download 10 MB?" before loading.

**Q: Are downloaded assets stored permanently?**
A: Browser cache decides. Usually persist until cache is cleared. You can detect and re-download if needed.

**Q: Does this work offline after first download?**
A: Yes! Once cached, scenes load offline from browser cache.

---

## Further Optimization

Once Addressables is working, you can:

1. **Compress textures more** (ETC2 for Android, PVRTC for iOS, DXT for Desktop/WebGL)
2. **Use texture atlases** to reduce draw calls
3. **Split large assets** into separate Addressables groups
4. **Profile with Addressables Analyze** tool (Window → Asset Management → Addressables → Analyze)
5. **Use asset bundle compression** (LZMA or LZ4)
6. **Lazy-load audio** - music/sfx on-demand
7. **Lazy-load models** - load character skins separately

---

## Monitoring

Add analytics to track:
- Initial load time
- Per-level download times
- Cache hit rates
- Average player session size

This helps optimize which content to bundle vs. load separately.

---

**Your game now loads lightning-fast! ⚡**

See [ADDRESSABLES_SETUP.md](ADDRESSABLES_SETUP.md) for detailed instructions.
