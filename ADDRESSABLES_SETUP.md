# Addressables Setup for Fast WebGL Loading

This guide shows you how to set up Addressables to dramatically reduce your initial WebGL load time by loading levels on-demand.

> **Important:** Addressables only bundles assets IN the scene hierarchy. For prefabs you spawn at runtime, see [ADDRESSABLES_ASSETS.md](ADDRESSABLES_ASSETS.md) for how to load them properly.

## Benefits

✅ **Tiny Initial Download** - Only MainMenu + core systems (~1-5 MB)  
✅ **On-Demand Loading** - Levels download as needed  
✅ **Browser Caching** - Downloaded levels stay cached  
✅ **Remote Hosting** - Host assets on CDN for faster global delivery  
✅ **Background Preloading** - Download next level while playing  

## Step-by-Step Setup

### 1. Install Addressables Package

1. Open **Window → Package Manager**
2. Click the **+** button → **Add package by name**
3. Enter: `com.unity.addressables`
4. Click **Add**

### 2. Initialize Addressables

1. Open **Window → Asset Management → Addressables → Groups**
2. Click **Create Addressables Settings** (if prompted)
3. This creates the `AddressableAssetsData` folder in your project

### 3. Mark Scenes as Addressable

For each scene EXCEPT MainMenu (keep MainMenu in the build):

1. Select the scene in Project window
2. Check **Addressable** in the Inspector
3. The scene's address should match its name (e.g., "Level1", "Video0")

**Scenes to mark as Addressable:**
- ✅ Level1, Level2, Level3, etc.
- ✅ Video0, Video1, Video2, etc.
- ✅ LevelSelection
- ✅ VideoSelection
- ❌ MainMenu (keep this in build for fast startup)

### 4. Configure Addressables Groups

1. Open **Window → Asset Management → Addressables → Groups**
2. You'll see your scenes listed under "Default Local Group"

**For Optimal WebGL Performance:**

Create separate groups for different content types:

#### Create "Levels" Group:
- Right-click → **Create New Group → Packed Assets**
- Name it "Levels"
- Drag all Level scenes (Level1, Level2, etc.) into this group

#### Create "Videos" Group:
- Create another group named "Videos"  
- Drag all Video scenes into this group

#### Create "UI Scenes" Group:
- Create group named "UI_Scenes"
- Drag LevelSelection and VideoSelection here

### 5. Configure Build & Load Paths

For each group, click on the group name to see settings:

**For Local Testing:**
- **Build Path:** LocalBuildPath
- **Load Path:** LocalLoadPath

**For Production (Remote Hosting):**
- **Build Path:** RemoteBuildPath  
- **Load Path:** `https://yourcdn.com/WebGL/[BuildTarget]`

You can change this later when you're ready to host remotely.

### 6. Update Build Settings

1. Open **File → Build Settings**
2. **Remove** all scenes EXCEPT MainMenu from the build list
3. MainMenu should be the only scene at index 0
4. This drastically reduces your initial build size!

### 7. Configure SceneTransitionManager

1. Find the **SceneTransitionManager** in your MainMenu scene (or create a persistent one)
2. In the Inspector, check **Use Addressables** ✓

That's it! The system will now use Addressables automatically.

### 8. Build Addressables

Before building your game:

1. Open **Window → Asset Management → Addressables → Groups**
2. Click **Build → New Build → Default Build Script**
3. Wait for it to complete
4. This creates the asset bundles in `ServerData/` folder

### 9. Build Your Game

1. **File → Build Settings**
2. Select WebGL platform
3. Click **Build** or **Build and Run**

Your initial build will now be much smaller!

---

## Testing Locally

To test Addressables locally:

1. Build Addressables (step 8)
2. Build WebGL
3. Test in a local web server:
   ```
   python -m http.server 8000
   ```
4. Navigate to `http://localhost:8000`

Levels will load from your local `ServerData/` folder.

---

## Remote Hosting for Production

### Option 1: Host on CDN (Recommended)

1. Build Addressables with Remote path configured
2. Upload the `ServerData/WebGL/` folder to your CDN:
   - AWS S3 + CloudFront
   - Google Cloud Storage + CDN
   - Azure CDN
   - Cloudflare R2
   - Any CDN service

3. Update your Addressables group settings:
   - Load Path: `https://your-cdn-url.com/WebGL/[BuildTarget]`

4. Rebuild Addressables
5. Build WebGL game
6. Host the game build on your game server
7. Host the Addressables data on your CDN

### Option 2: Same Server as Game

1. After building Addressables, the `ServerData/` folder contains your assets
2. When you build WebGL, copy `ServerData/` to your web server
3. The game will load assets from the same domain

### CORS Configuration

If hosting on a separate CDN, configure CORS headers:

```
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET
Access-Control-Allow-Headers: Content-Type
```

---

## Advanced: Preloading During Gameplay

To download the next level while the player is playing (for instant transitions):

```csharp
// In your LevelManager or gameplay code:
void Start()
{
    // Preload next level in background
    int currentLevel = 1; // or get from LevelManager
    SceneTransitionManager.Instance.PreloadNextLevel(currentLevel);
}
```

This downloads Level 2 while the player is playing Level 1!

---

## Advanced: Show Download Progress

To show download size before loading:

```csharp
AddressablesSceneLoader.Instance.GetSceneDownloadSize("Level5", (size) => 
{
    if (size > 0)
    {
        float sizeMB = size / 1024f / 1024f;
        Debug.Log($"Will download {sizeMB:F2} MB");
        // Show user a message or progress bar
    }
    else
    {
        Debug.Log("Already cached!");
    }
});
```

---

## Expected Build Size Comparison

### Without Addressables:
- Initial Download: **50-200 MB** (all scenes + assets)
- Load Time: **30-120 seconds**

### With Addressables:
- Initial Download: **1-10 MB** (just MainMenu + core)
- Load Time: **2-5 seconds**
- Level Download: **5-20 MB per level** (on-demand)
- Level Load Time: **3-10 seconds** (first time, instant after cached)

---

## Troubleshooting

**"Scene not found" error:**
- Make sure you built Addressables before building the game
- Check scene is marked as Addressable with correct address
- Verify Load Path is correct

**Slow loading:**
- Check your internet connection
- Consider using a CDN for production
- Use preloading for next level during gameplay

**Scenes won't load in WebGL:**
- Open browser console for errors
- Check CORS headers if using remote hosting
- Verify ServerData folder is accessible via the Load Path URL

**Assets too large:**
- Compress textures more aggressively
- Use texture atlases
- Enable Unity's compression in Addressables settings

---

## Best Practices

### Group Organization:
- Group scenes by type (Levels, Videos, UI)
- Label scenes clearly
- Use descriptive addresses

### Asset Management:
- Put level-specific assets in the same bundle as the level
- Share common assets in a "Shared" group
- Don't make bundles too small (overhead) or too large (long downloads)

### Caching Strategy:
- Let browser cache assets automatically
- Clear cache with version updates
- Monitor cache size for mobile browsers

### Testing:
- Test with throttled network speeds
- Test cache behavior (load level, reload page, load again)
- Test with web server, not file:// protocol

---

## Next Steps

Once Addressables is working:

1. **Profile your build sizes** using Addressables Analyze tool
2. **Set up remote hosting** on a CDN for production
3. **Implement preloading** for smoother experience  
4. **Add loading screens** with progress bars
5. **Monitor download sizes** and optimize large assets

Your WebGL game will now load almost instantly! 🚀
