# Sprite Video Frame Player

A simple Unity system for extracting video frames and playing them back on a sprite at a desired FPS.

## 🎬 What This Does

1. **Extract Frames**: Uses FFmpeg to extract video frames from an MP4 file into individual PNG images
2. **Load Frames**: Loads all frames into memory when the game starts
3. **Play Back**: Plays the frame sequence on a SpriteRenderer at your desired FPS

## 📦 FFmpeg Installation (Required)

### Windows Installation (Easiest Method)

1. **Open PowerShell or Command Prompt as Administrator**
   - Press `Win + X` and select "Windows PowerShell (Admin)" or "Command Prompt (Admin)"

2. **Install using WinGet**:
   ```powershell
   winget install ffmpeg
   ```

3. **Restart Unity** after installation completes

### Alternative: Manual Installation

1. Download FFmpeg from: https://www.gyan.dev/ffmpeg/builds/
2. Extract to a location like `C:\ffmpeg`
3. Add `C:\ffmpeg\bin` to your System PATH:
   - Right-click "This PC" → Properties → Advanced System Settings
   - Click "Environment Variables"
   - Under "System variables", find "Path" and click "Edit"
   - Click "New" and add `C:\ffmpeg\bin`
   - Click OK and restart Unity

### Verify Installation

Open Command Prompt and type:
```
ffmpeg -version
```

If you see version information, FFmpeg is installed correctly!

## 🚀 Quick Start Guide

### Step 1: Extract Video Frames

1. In Unity, go to **Tools → Video Frame Extractor**
2. Drag your video clip (MP4) into the "Video Clip" field
3. Set your desired "Target FPS" (15-30 recommended)
   - Lower FPS = Less memory usage
   - Higher FPS = Smoother playback
4. Click **"Extract Frames"**
5. Wait for extraction to complete

Frames will be saved to: `Assets/YourVideoLocation/YourVideoName_frames/`

### Step 2: Set Up Sprite Player

1. Create a GameObject with a **SpriteRenderer** component
2. Add the **SpriteFramePlayer** component to it
3. In the Inspector:
   - Set **Frames Folder** to your extracted frames folder (e.g., `Assets/Videos/MyVideo_frames`)
   - Set **Playback FPS** to your desired playback speed (usually same as extraction FPS)
   - Enable **Play On Awake** if you want it to start automatically
   - Enable **Loop** if you want it to repeat
4. Click the **"Load Frames"** button (or it will auto-load on Play)

### Step 3: Run and Enjoy!

Press Play and watch your video play as sprite frames!

## 🎮 Runtime Controls

You can control the player via code:

```csharp
public class VideoController : MonoBehaviour
{
    private SpriteFramePlayer player;

    void Start()
    {
        player = GetComponent<SpriteFramePlayer>();
    }

    void Update()
    {
        // Play
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (player.IsPlaying())
                player.Pause();
            else
                player.Play();
        }

        // Stop
        if (Input.GetKeyDown(KeyCode.S))
        {
            player.Stop();
        }

        // Change speed
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            player.SetPlaybackFPS(60f);
        }

        // Jump to specific frame
        if (Input.GetKeyDown(KeyCode.R))
        {
            player.SetFrame(0); // Restart
        }
    }
}
```

## 📊 Memory Considerations

**Memory Formula**:
```
Total Memory = Frame Count × Frame Resolution × 4 bytes per pixel

Example (1920×1080, 30fps, 60 seconds):
= (60 seconds × 30 fps) × (1920 × 1080) × 4 bytes
= 1800 frames × 8.3 MB per frame
≈ 15 GB in memory
```

### Tips for Managing Memory:

- **Lower FPS**: Extract at 15-24 FPS instead of 30-60
- **Smaller Resolution**: Scale down your video before extracting
- **Shorter Clips**: Break long videos into multiple shorter segments
- **Stream Instead**: For very long videos, consider using Unity's VideoPlayer instead

## 🔧 API Reference

### SpriteFramePlayer Methods

| Method | Description |
|--------|-------------|
| `LoadFrames()` | Load all frames from the frames folder |
| `Play()` | Start playing the frame sequence |
| `Pause()` | Pause playback |
| `Stop()` | Stop and reset to first frame |
| `SetFrame(int index)` | Jump to a specific frame |
| `SetPlaybackFPS(float fps)` | Change playback speed |
| `GetTotalFrames()` | Get total number of loaded frames |
| `GetCurrentFrame()` | Get current frame index |
| `IsPlaying()` | Check if currently playing |
| `GetProgress()` | Get playback progress (0-1) |

## 📁 File Structure

```
Assets/
  Scripts/
    VideoFrameExtractor/
      Runtime/
        SpriteFramePlayer.cs        ← Main playback component
      Editor/
        FFmpegFrameExtractor.cs     ← Frame extraction window
        SpriteFramePlayerEditor.cs  ← Custom inspector
      README.md                      ← This file
  
  Videos/                            ← Your videos
    MyVideo.mp4
    MyVideo_frames/                  ← Auto-generated frames folder
      frame_00000.png
      frame_00001.png
      ...
```

## ⚠️ Common Issues

### "FFmpeg not found"
- Make sure FFmpeg is installed using `winget install ffmpeg`
- Restart Unity after installing FFmpeg
- Verify FFmpeg is in PATH by running `ffmpeg -version` in Command Prompt

### Frames not loading in build
- Frames must be in a `Resources` folder for builds
- Move your frames folder to `Assets/Resources/VideoFrames/`
- Update the frames folder path to `Assets/Resources/VideoFrames/YourVideo_frames`

### Out of memory
- Extract at lower FPS (15-20 instead of 30-60)
- Use smaller video resolution
- Break video into shorter clips

### Playback is choppy
- Make sure "Playback FPS" matches your extraction FPS
- Check if frame loading took too long (visible in Console)
- Lower the resolution if frames are very large

## 💡 Tips & Best Practices

✅ **DO:**
- Extract at 24-30 FPS for most use cases
- Use consistent naming for videos and frame folders
- Test with a short clip first to estimate memory usage
- Delete frame folders you no longer need

❌ **DON'T:**
- Extract 60+ second videos at 60 FPS (huge memory!)
- Mix frame formats (PNG/JPG) in the same folder
- Rename frame files manually (breaks sequential loading)
- Use this for videos longer than 2-3 minutes

## 🎯 Example Scenarios

### Music Video Background (60 seconds)
- Extract at **24 FPS**
- Resolution: **1280×720**
- Memory: ~4.5 GB
- ✅ Good for music rhythm games

### Short Loop Animation (5 seconds)
- Extract at **30 FPS**
- Resolution: **1920×1080**
- Memory: ~1.2 GB
- ✅ Perfect for menu backgrounds

### Long Cutscene (5 minutes)
- Extract at **15 FPS**
- Resolution: **1280×720**
- Memory: ~11 GB
- ⚠️ Consider Unity VideoPlayer instead

## 📝 License

Free to use in any project!

## 🤝 Credits

Based on FFmpeg frame extraction techniques.
