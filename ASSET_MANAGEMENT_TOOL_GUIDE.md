# Asset Management & WebGL Optimizer Tool

## Overview

A comprehensive Unity Editor tool designed specifically for WebGL game optimization, with a focus on iOS Safari compatibility and memory management. This tool helps you scan, analyze, and optimize all assets in your project while providing automated WebGL platform settings recommendations.

## Accessing the Tool

**Menu Location**: `Tools > Asset Management & WebGL Optimizer`

The window opens with four main tabs:
1. **Asset Scanner** - Scan and browse all project assets
2. **Batch Settings** - Apply import settings to multiple assets at once
3. **WebGL Optimizer** - Analyze and optimize WebGL platform settings
4. **Memory Analysis** - Analyze project size by category

---

## Tab 1: Asset Scanner

### Features

**Scan All Assets**
- Click "Scan All Assets" to index all files in your project
- Shows asset name, type, file size, and current import settings
- Results are cached until cleared

**Filtering & Sorting**
- **Search**: Filter by asset name
- **Type Filter**: Show only specific asset types (Texture, Audio, Video, etc.)
- **Sort Options**: 
  - Size (Descending/Ascending)
  - Name (Ascending/Descending)
  - Type (Ascending)

**Selection**
- Check individual assets or use "Select All" to select all filtered assets
- Selected assets carry over to the Batch Settings tab
- Click on asset names to highlight them in the Project window

### Common Workflows

**Find Large JPG Files (Video Frames)**
1. Click "Scan All Assets"
2. Set Type Filter to "Texture"
3. Set Sort to "Size Descending"
4. Search for ".jpg" or specific frame folder names
5. Select the assets you want to optimize

---

## Tab 2: Batch Settings

### Texture Import Settings

Apply consistent import settings to multiple selected assets:

**Individual Settings**
- **Compression**: Uncompressed, Compressed, CompressedHQ, CompressedLQ
- **Max Size**: 32 to 8192 pixels
- **Format**: Automatic or specific formats
- **Generate Mipmaps**: Enable/disable mipmap generation
- **Read/Write Enabled**: Allow CPU access (usually disable for performance)

### Quick Presets for WebGL

**High Quality** (Max 2048, Normal Compression)
- Good balance for hero assets and large backgrounds
- Maintains visual quality while optimizing size

**Medium Quality** (Max 1024, High Compression)
- Recommended for most sprite frame sequences
- Significant size reduction with acceptable quality
- **Best for your JPG video frames!**

**Low Quality** (Max 512, Max Compression)
- Maximum size optimization
- Good for small UI elements and background details

### Quick Selection Tools

**Select All JPG/JPEG Files**
- Instantly selects all JPG/JPEG files in the project
- Perfect for optimizing video frame sequences

**Select All PNG Files**
- Select all PNG textures
- Note: PNG is less efficient than JPG for photos

**Select Large Assets (>500KB or >1MB)**
- Find assets that contribute most to build size
- Prioritize optimization efforts on these files

### Recommended Workflow for Your JPG Frames

1. Go to Asset Scanner tab and click "Scan All Assets"
2. Go to Batch Settings tab
3. Click "Select All JPG/JPEG Files in Project"
4. Click "Medium Quality" preset button
5. Review settings (Max 1024, High Compression)
6. Click "Apply Settings to Selected Assets"
7. Wait for processing - this will reimport all selected textures

**Expected Results**: 
- Significantly reduced memory usage
- Faster loading times on WebGL
- Better iOS Safari compatibility

---

## Tab 3: WebGL Optimizer

### Current Settings Analysis

Automatically analyzes your current WebGL platform settings and shows:
- ✅ **Green**: Setting is optimal
- ⚠️ **Yellow**: Setting can be improved

**Analyzed Settings**:
- **Compression Format**: Brotli recommended (best compression, Safari compatible)
- **Exception Support**: ExplicitlyThrownOnly recommended (smaller builds)
- **Memory Size**: 512-768MB recommended (iOS Safari limit ~500MB)
- **Code Optimization**: OptimizeSize recommended (smaller WebGL builds)
- **Managed Stripping Level**: High/Medium recommended (removes unused code)
- **Data Caching**: Enabled recommended (faster subsequent loads)
- **Addressables**: Status check

### Quick Apply Settings

**Apply Recommended Settings** (iOS Safari Optimized)
- Brotli Compression
- Exception Support: Explicitly Thrown Only
- Memory: 512MB (safe for iOS Safari)
- Code Optimization: Size
- Stripping: High
- Data Caching: Enabled

**Apply Performance Settings** (Maximum Compression)
- Similar to recommended but more aggressive
- Memory: 768MB
- Exception Support: None (smallest build size)
- May break some debugging features

### Individual Setting Controls

Apply specific settings one at a time:
- Set Compression to Brotli
- Set Exception Support to Explicitly Thrown Only
- Set Memory Size to 512MB
- Enable Data Caching
- Optimize Color Space for WebGL (Linear)
- Disable Auto Graphics API (Use WebGL 2.0)

### Build Size Optimization

- **Set High Stripping Level**: Removes unused Unity engine code
- **Optimize IL2CPP Code Generation**: Reduces generated code size

---

## Tab 4: Memory Analysis

### Features

**Analyze Project Assets**
- Categorizes all assets by type
- Shows total size per category
- Displays percentage breakdown
- Visual progress bars for easy comparison

**Categories Tracked**:
- JPG/JPEG images
- PNG images
- Audio files
- Video files
- 3D Models
- Scripts
- Scenes, Prefabs, Materials
- Other assets

### Recommendations

The tool automatically provides warnings if:
- JPG files exceed 50MB total
- PNG files exceed 50MB total
- Total project size exceeds 500MB (iOS Safari concern)

### Understanding the Results

**Example Output**:
```
Total Project Size: 245.67 MB

Size by Category:
JPG/JPEG    189.23 MB  77.05%  ████████████████████
PNG          32.45 MB  13.21%  ███
Audio        15.67 MB   6.38%  █
Scripts       4.32 MB   1.76%  
Other         4.00 MB   1.60%  
```

This shows that JPG files are the majority of your project - optimizing these will have the biggest impact.

---

## Recommended Workflows

### Initial Project Optimization

1. **Tab 4**: Run Memory Analysis to understand your project composition
2. **Tab 1**: Scan All Assets
3. **Tab 2**: Select All JPG/JPEG Files
4. **Tab 2**: Apply "Medium Quality" preset
5. **Tab 3**: Apply "Recommended Settings (iOS Safari Optimized)"
6. Build and test on target devices

### Ongoing Asset Management

**Before Adding New Video Frame Sequences**:
1. Extract frames using FFmpeg Frame Extractor
2. Use Asset Scanner to view new frame sizes
3. Apply Medium or Low Quality preset before first build
4. Verify memory usage stays under 400MB in runtime

**Monthly Checkup**:
1. Run Memory Analysis
2. Check for new large assets (>1MB)
3. Review WebGL settings status
4. Optimize any new assets

### iOS Safari Optimization Priority

1. ✅ **Most Important**: Keep total runtime memory < 400MB
   - Use Addressables to load assets progressively
   - Release assets when switching scenes
   
2. ✅ **Very Important**: Compress JPG frame sequences
   - Use "Medium Quality" preset (Max 1024)
   - This is your largest asset category
   
3. ✅ **Important**: Use recommended WebGL settings
   - 512MB memory limit
   - Brotli compression
   - High stripping level

---

## Technical Details

### Texture Compression Modes

- **Uncompressed**: Highest quality, largest file size
- **Compressed**: Standard compression, good balance
- **CompressedHQ**: Higher quality compression, larger than standard
- **CompressedLQ**: Lower quality, maximum compression

### WebGL Memory Considerations

**iOS Safari Memory Limits**:
- Total available: ~500MB
- Unity engine overhead: ~50-100MB
- Your available asset budget: ~300-400MB

**Memory = File Size × Format Multiplier**:
- JPG compressed at 1024×768 ≈ 100KB file, ~3MB in memory
- Multiple frame sequences add up quickly!

### Addressables Integration

This tool complements your existing Addressables system:
- Assets can be optimized before adding to Addressables groups
- Smaller assets = faster download times
- Reduced memory per asset = more assets can be loaded simultaneously

---

## Tips & Best Practices

### For Your Sprite Frame Player

Your project uses JPG sequences for video playback. Here's how to optimize:

1. **Extract frames at lower resolution** (FFmpeg setting)
   - Instead of 1920×1080, try 1280×720 or 960×540
   
2. **Apply Medium Quality preset** after extraction
   - This further reduces size while maintaining quality
   
3. **Load frames via Addressables** (you already do this!)
   - Progressive loading prevents memory spikes
   
4. **Use AddressablesOperationManager.ReleaseSceneAssets()**
   - Release frame sequences when switching scenes

### Texture Size Guidelines for WebGL

- **HD Video Frames**: 1024×768 or less
- **Background Images**: 2048×2048 maximum
- **UI Elements**: 512×512 or less
- **Character Sprites**: 512×512 typical

### When to Rescan

Rescan after:
- Importing new assets
- Applying import settings
- Adding new video frame sequences
- Major asset restructuring

---

## Troubleshooting

### "No Assets Scanned" Warning

**Solution**: Go to Asset Scanner tab and click "Scan All Assets" first

### Assets Not Updating After Apply Settings

**Solution**: 
- Wait for Unity to finish reimporting (check bottom-right progress bar)
- Click "Scan All Assets" again to refresh file sizes

### WebGL Settings Don't Apply

**Solution**: 
- Ensure WebGL platform is installed (File > Build Settings > WebGL)
- Some settings require Unity restart to take effect

### Memory Still Too High

**Solutions**:
1. Check runtime memory, not file sizes (use AddressablesOperationManager.GetMemoryInfo())
2. Ensure old scene assets are released (SetCurrentScene before loading new scene)
3. Reduce texture max size further (512 instead of 1024)
4. Reduce frame extraction FPS (fewer frames = less memory)

---

## Integration with Your Existing Systems

### Works With

✅ **AddressablesOperationManager**: Optimizes assets before they're loaded  
✅ **SpriteFramePlayer**: Reduces memory per frame sequence  
✅ **Memory Management System**: Complements release/cleanup strategies  
✅ **FFmpeg Frame Extractor**: Post-process frames after extraction  

### Example Combined Workflow

```
1. FFmpeg Frame Extractor: Extract frames from MP4
   ↓
2. Asset Management Tool: Compress frames to 1024px
   ↓
3. Addressables: Assign frames to label group
   ↓
4. SpriteFramePlayer: Load and play frames via Addressables
   ↓
5. AddressablesOperationManager: Release frames on scene change
```

---

## Quick Reference

### Keyboard Shortcuts
- None (uses standard Unity Editor interactions)

### Button Locations
- **Main Menu**: Tools > Asset Management & WebGL Optimizer
- **Project Settings**: Tab 3 has quick links

### Default Presets

| Preset | Max Size | Compression | Best For |
|--------|----------|-------------|----------|
| High Quality | 2048 | Compressed | Hero assets |
| Medium Quality | 1024 | CompressedHQ | Video frames (recommended) |
| Low Quality | 512 | CompressedLQ | Small UI elements |

---

## Support & Further Reading

### Related Documentation
- `MEMORY_MANAGEMENT_GUIDE.md` - Memory system architecture
- `ADDRESSABLES_SYSTEM_OVERVIEW.md` - Addressables usage
- `Assets/Scripts/VideoFrameExtractor/README.md` - Frame extraction

### Unity Documentation
- [WebGL Player Settings](https://docs.unity3d.com/Manual/class-PlayerSettingsWebGL.html)
- [Texture Import Settings](https://docs.unity3d.com/Manual/class-TextureImporter.html)
- [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest)

---

**Created for sickofitgame-unity WebGL Project**  
**Version**: 1.0  
**Last Updated**: February 2026
