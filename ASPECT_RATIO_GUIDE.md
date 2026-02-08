# Aspect Ratio Handling Guide for 2D Unity Games

## The Problem
You're developing a 2D game targeting 16:9 (1920x1080) but need to support various phone aspect ratios:
- Standard phones: 16:9 (1080x1920)
- Modern phones: 18:9, 19:9, 19.5:9, 20:9 (very tall)
- Tablets: 4:3, 16:10
- Ultra-wide displays: 21:9

## Solutions Overview

### Option 1: Letterboxing/Pillarboxing (Recommended)
**What it does:** Adds black bars to maintain your 16:9 aspect ratio
**Pros:**
- Consistent gameplay experience across all devices
- No content gets cut off
- Easiest to design for
**Cons:**
- Black bars on some devices
- Doesn't use full screen space

### Option 2: Flexible Layout
**What it does:** Shows more content on taller/wider screens
**Pros:**
- Uses full screen on all devices
- No black bars
**Cons:**
- Need to design for variable visible area
- More complex to balance gameplay
- Need safe zones for important content

## Setup Instructions

### 1. Camera Setup (Letterboxing Approach)

1. **Add AspectRatioController to your Main Camera:**
   - Select your Main Camera in the hierarchy
   - Add Component → `AspectRatioController`
   - Set Target Aspect Width: `16`
   - Set Target Aspect Height: `9`
   - Set Fit Mode: `Fit Inside` (for letterboxing)

2. **Test Different Aspect Ratios:**
   - Create an empty GameObject in your scene
   - Add Component → `AspectRatioTester`
   - In the Inspector, use the buttons to test different resolutions
   - Make sure Game View is set to "Free Aspect"

### 2. UI Setup

For UI elements, you MUST configure your Canvas Scaler properly:

1. **Select your Canvas** in the hierarchy
2. **Canvas Scaler Component Settings:**
   - UI Scale Mode: `Scale With Screen Size`
   - Reference Resolution: `1920 x 1080` (your target resolution)
   - Screen Match Mode: `Match Width Or Height`
   - Match: `0.5` (balances between width and height)
     - Use 0 to prioritize width (for landscape games)
     - Use 1 to prioritize height (for portrait games)
     - Use 0.5 for balanced scaling

3. **Add SafeAreaHandler for Notch Support:**
   - Create a Panel as child of Canvas (name it "SafeAreaPanel")
   - Add Component → `SafeAreaHandler`
   - Put ALL your UI elements inside this SafeAreaPanel
   - Enable "Apply Safe Area" and "Show Debug"

### 3. Camera Size Adjustments

For 2D games, you may want to adjust the orthographic size based on aspect ratio:

```csharp
// In your camera script or AspectRatioController
float targetAspect = 16f / 9f;
float screenAspect = (float)Screen.width / Screen.height;

if (screenAspect < targetAspect)
{
    // Screen is taller than 16:9
    // Option: Increase orthographic size to show more vertical content
    cam.orthographicSize = baseOrthographicSize * (targetAspect / screenAspect);
}
```

### 4. Testing in Unity Editor

1. **Install Free Aspect:**
   - Game View → Aspect Dropdown → `Free Aspect`

2. **Use AspectRatioTester:**
   - Select the GameObject with AspectRatioTester
   - In Inspector, click different resolution buttons
   - Watch how your game adapts

3. **Test These Critical Aspect Ratios:**
   - 16:9 (1920x1080) - Your target
   - 19.5:9 (1080x2340) - iPhone 12/13
   - 20:9 (1080x2400) - Samsung Galaxy S21
   - 4:3 (1024x768) - Old tablets
   - 21:9 (1080x2520) - Ultra-wide

## WebGL Specific Considerations

### Canvas Setup in Your HTML

When embedding your WebGL build, use this template:

```html
<div id="unity-container" style="width: 100%; height: 100vh;">
    <canvas id="unity-canvas"></canvas>
</div>

<script>
    var canvas = document.querySelector("#unity-canvas");
    var container = document.querySelector("#unity-container");
    
    createUnityInstance(canvas, {
        dataUrl: "Build/Your.data",
        frameworkUrl: "Build/Your.framework.js",
        codeUrl: "Build/Your.wasm",
        streamingAssetsUrl: "StreamingAssets",
        companyName: "YourCompany",
        productName: "YourGame",
    }).then((unityInstance) => {
        // Unity instance loaded
        resizeCanvas();
    });
    
    function resizeCanvas() {
        var container = document.querySelector("#unity-container");
        var canvas = document.querySelector("#unity-canvas");
        
        // Get container size
        var containerWidth = container.clientWidth;
        var containerHeight = container.clientHeight;
        
        // Calculate 16:9 dimensions that fit in container
        var targetAspect = 16 / 9;
        var containerAspect = containerWidth / containerHeight;
        
        if (containerAspect > targetAspect) {
            // Container is wider - fit to height
            canvas.style.width = (containerHeight * targetAspect) + 'px';
            canvas.style.height = containerHeight + 'px';
        } else {
            // Container is taller - fit to width
            canvas.style.width = containerWidth + 'px';
            canvas.style.height = (containerWidth / targetAspect) + 'px';
        }
    }
    
    window.addEventListener('resize', resizeCanvas);
</script>
```

### Unity WebGL Player Settings

1. **Edit → Project Settings → Player → WebGL**
2. **Resolution and Presentation:**
   - Default Canvas Width: `1920`
   - Default Canvas Height: `1080`
   - WebGL Template: `Default` or custom template

## Design Best Practices

### 1. Safe Zone for Important Content
Keep critical UI elements and gameplay within a "safe zone":
- Assume 16:9 as your "safe zone"
- Any content outside this may be cropped on narrower screens
- Or hidden by letterbox bars on wider screens

### 2. Background Design
- Make your backgrounds extend beyond 16:9
- Use tiling or stretching for backgrounds
- Ensure backgrounds look good with black bars

### 3. UI Layout
- Anchor UI elements to screen edges using Unity's anchor system
- Use Canvas Scaler with Scale With Screen Size
- Test on multiple aspect ratios regularly

### 4. Gameplay Area
For gameplay-critical elements:
```csharp
// Get the safe gameplay bounds
var aspectController = Camera.main.GetComponent<AspectRatioController>();
Rect safeArea = aspectController.GetSafeAreaWorldBounds();

// Keep important gameplay elements within these bounds
```

## Troubleshooting

### Black bars appear in Unity Editor but not on phone
- This is correct! AspectRatioController is working
- The phone likely has a different aspect ratio than your Game View

### UI elements are cut off on some devices
- Check Canvas Scaler settings
- Make sure Match is set to 0.5
- Use SafeAreaHandler for devices with notches

### Game looks stretched
- Check Canvas Scaler: should be "Scale With Screen Size", NOT "Constant Physical Size"
- Verify AspectRatioController is attached to Main Camera

### Different content visible on different phones
- This is expected with Fit Outside mode
- Switch to Fit Inside for consistent content
- Or design game to work with variable visible area

## Quick Checklist

- [ ] AspectRatioController added to Main Camera
- [ ] AspectRatioTester added for testing
- [ ] Canvas Scaler configured (Scale With Screen Size, 1920x1080, Match 0.5)
- [ ] SafeAreaHandler added to UI root panel
- [ ] Tested with multiple aspect ratios using AspectRatioTester
- [ ] Background extends beyond 16:9
- [ ] Important UI elements in safe area
- [ ] Gameplay-critical elements within safe bounds

## Additional Resources

- Unity UI Anchors: https://docs.unity3d.com/Manual/UIBasicLayout.html
- Safe Area API: https://docs.unity3d.com/ScriptReference/Screen-safeArea.html
- Canvas Scaler: https://docs.unity3d.com/Manual/script-CanvasScaler.html
