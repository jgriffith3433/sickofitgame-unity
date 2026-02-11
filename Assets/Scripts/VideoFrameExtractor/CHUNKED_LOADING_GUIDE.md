# Chunked Sprite Frame Loading System

## Overview
The SpriteFramePlayer uses a chunked loading system to play long sprite frame videos without loading all frames into memory at once. It progressively loads chunks during playback and caches them in the browser.

**All videos must now use SpriteFramesData** - this is the only supported loading method.

## How It Works

1. **Chunks**: Video frames are split into chunks (e.g., 250 frames each)
2. **Sliding Window**: During playback, current + next chunk stay in RAM (~340MB)
3. **Browser Caching**: Preloading caches all chunks, then maintains 2-chunk sliding window
4. **Instant Transitions**: Next chunk is already in RAM when needed (no loading delays)

## Setup Guide

### Step 1: Create SpriteFramesData Asset

1. In Unity: **Right-click in Project → Create → Video → Sprite Frames Data**
2. Name it appropriately (e.g., "Video3" or "Level1_Background1")
   - **Important**: The asset name will be used as the label prefix!
3. Fill in basic info:
   - Video Name: "Video3"
   - Playback FPS: 30
   - Loop: false/true
   - Audio Label: "Video3_Audio" (if you have audio)

### Step 2: Auto-Chunk Your Frames

1. Open **Tools → Sprite Frames Manager**
2. Drag your SpriteFramesData asset into the "Sprite Frames Data" slot
3. Select ALL frames in Project window (Ctrl+A in the frames folder)
4. Set "Frames Per Chunk" to **250** (recommended for ~170MB per chunk)
5. Click **"Auto-Chunk Selected Frames"**

If your asset is named "Video3", this creates chunks like:
- Part1: frames 0-249 → Label: "Video3_Part1"
- Part2: frames 250-499 → Label: "Video3_Part2"
- etc.

If named "Level1_Background1":
- Label: "Level1_Background1_Part1"
- Label: "Level1_Background1_Part2"
- etc.

### Step 3: Apply Addressable Labels

1. Make sure frames are still selected in Project window
2. In the Sprite Frames Manager, click **"Apply Addressable Labels to Frames"**
3. This automatically assigns chunk labels to each frame in Addressables

### Step 4: Setup SpriteFramePlayer Component

1. Select your Video GameObject in the Scene
2. In SpriteFramePlayer component:
   - Drag your **SpriteFramesData** asset into the "Sprite Frames Data" slot (REQUIRED)
   - Set "Preload On Awake" to **true** (recommended)
   - Set "Play On Load" based on your needs:
     - **true**: Auto-plays after preloading completes
     - **false**: Call Play() manually (e.g., from VideoManager)

**Note**: SpriteFramesData is now REQUIRED. Preload Frame Threshold is no longer used (sliding window handles transitions).

### Step 5: Preloading (Optional but Recommended)

To preload Video3 during Level2 (for browser caching):

```csharp
public class Level2Manager : MonoBehaviour
{
    private void Start()
    {
        // Find Video3's SpriteFramePlayer (it doesn't need to be active)
        var video3Player = FindObjectOfType<SpriteFramePlayer>(true);
        
        // Preload all chunks (loads and unloads for caching)
        video3Player.PreloadAllChunks((success) =>
        {
            if (success)
                Debug.Log("Video3 chunks cached!");
        });
    }
}
```

## Memory Comparison

**Video3 (1135 frames, 38 seconds):**

| Method | Memory Usage |
|--------|-------------|
| All frames at once | ~638MB ❌ Crashes iOS |
| Chunked (250 frames/chunk) | ~340MB (2 chunks in RAM) ✅ iOS-safe |
| With preloading | First 2 chunks in RAM + rest cached ✅ |

**Why 2 chunks (sliding window)?**
- Chunk transitions are **instant** (next chunk already in RAM)
- When switching from chunk N to N+1:
  - Chunk N+1 already in RAM (loaded during previous switch)
  - Chunk N+2 loads immediately
  - Chunk N-1 unloads to free memory
- Memory stays constant at ~340MB
- No network delays during playback

## How VideoManager Uses It

Your existing VideoManager automatically works with the chunked loading system:

```csharp
// VideoManager just checks IsLoaded
if (videoPlayer.IsLoaded) 
{
    // ALL chunks are cached + first chunk is in RAM + audio loaded
    videoPlayer.Play();
}
```

**IsLoaded behavior:**
- `IsLoaded = false` until `PreloadAllChunks()` completes
- `PreloadAllChunks()` caches all chunks in browser, keeps first 2 in RAM (chunks 0, 1)
- Once complete, `IsLoaded = true` - ready for instant playback
- During playback, sliding window maintains current + next chunk in RAM
- Video will NOT start until preloading completes (guaranteed stutter-free)

### Preloading Example

```csharp
public class LevelManager : MonoBehaviour
{
    [SerializeField] private SpriteFramePlayer video3Player;
    
    private void Start()
    {
        Debug.Log("Preloading Video3 chunks...");
        video3Player.PreloadAllChunks((success) =>
        {
            if (success)
            {
                Debug.Log($"Video3 ready! All chunks cached: {video3Player.AreAllChunksPreloaded()}");
                Debug.Log($"IsLoaded: {video3Player.IsLoaded}"); // true
            }
        });
    }
}
```

## Architecture Notes

### Centralized Addressables Management

All addressables loading goes through `AddressablesOperationManager`:
- **Deduplication**: Prevents duplicate concurrent loads
- **Progress tracking**: Consistent progress reporting
- **Error handling**: Centralized logging and error management
- **Handle management**: Proper handle storage for cleanup

When loading chunks, the system uses `LoadLabelWithHandle<Sprite>()` which:
1. Checks if the label is already being loaded (deduplication)
2. Loads the assets via Addressables
3. Returns both the handle (for cleanup) and the results
4. Tracks progress and logs consistently

### IsLoaded Logic

```
IsLoaded = allChunksPreloaded && framesLoaded && (!hasAudio || audioLoaded)

Where:
- allChunksPreloaded = true after PreloadAllChunks() completes
- framesLoaded = true when first chunk is loaded
- audioLoaded = true when audio is loaded (if needed)
```

This ensures videos are only marked "ready" when:
1. All chunks are downloaded and cached in browser
2. First TWO chunks (0, 1) are in RAM for immediate playback
3. Audio is loaded (if needed)

Result: **Zero stuttering** during playback:
- Chunk 0 → 1: Instant (both already in RAM)
- Chunk 1 → 2: Instant (2 loads when switching to 1)
- Chunk 2 → 3: Instant (3 loads when switching to 2)
- Sliding window keeps next chunk always ready

## Troubleshooting

### "CRITICAL: Chunk X not in sliding window!"
- This means PreloadAllChunks() didn't complete successfully
- Check that `Preload On Awake` is enabled
- Ensure VideoManager waits for `IsLoaded == true` before calling `Play()`
- Check browser console for network errors during preload

### Stuttering during chunk transitions
- Should NOT happen with sliding window (next chunk always in RAM)
- If it does, check the CRITICAL error above
- Verify Addressables bundles built correctly

### Still using too much memory
- Current: ~340MB for 2 chunks (Video3)
- This is the minimum for smooth playback (current + next)
- To reduce: Make chunks smaller (200 frames = ~272MB for 2 chunks)
- Or apply lower texture maxSize (512 instead of 1024)

## Quick Setup Checklist

1. ✅ Create SpriteFramesData asset
2. ✅ Select all video frames in Project window
3. ✅ Tools → Sprite Frames Manager → Auto-Chunk (250 frames)
4. ✅ Apply Addressable Labels
5. ✅ Drag SpriteFramesData into SpriteFramePlayer component
6. ✅ Done!

Now Video3 uses ~170MB per chunk instead of 638MB total!
