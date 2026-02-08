# UI and Letterboxing - Complete Guide

## The Problem

When using `AspectRatioController` for letterboxing, your UI can look wrong because:

1. **AspectRatioController** adjusts the Camera viewport (creates black bars)
2. **UI Canvas** by default renders to full screen (ignores camera viewport)
3. Result: UI and game content have different aspect ratios!

## Visual Example

```
Without Fix:                    With Fix:
┌─────────────────────┐        ┌─────────────────────┐
│     BLACK BAR       │        │     BLACK BAR       │
├─────────────────────┤        ├─────────────────────┤
│                     │        │                     │
│   [UI] Game Content │        │  [UI+Game Content]  │
│                     │        │                     │
├─────────────────────┤        ├─────────────────────┤
│     BLACK BAR       │        │     BLACK BAR       │
└─────────────────────┘        └─────────────────────┘
UI in black bars! ✗           UI respects letterbox ✓
```

## Solutions Comparison

| Method | Complexity | Best For | Pros | Cons |
|--------|-----------|----------|------|------|
| **Screen Space - Camera** | Easy | Most games | Simple, automatic | Slight render overhead |
| **CanvasLetterboxAdapter** | Medium | Special cases | Keeps Overlay mode | Extra script |
| **SafeAreaHandler** | Easy | Mobile with notches | Handles notches too | Only for safe area |

## Solution 1: Screen Space - Camera (Recommended)

### Setup:

1. **Select your Canvas**
2. **Canvas Component Settings:**
   - Render Mode: `Screen Space - Camera`
   - Render Camera: [Drag your Main Camera here]
   - Plane Distance: `1`
3. **Canvas Scaler Settings:**
   - UI Scale Mode: `Scale With Screen Size`
   - Reference Resolution: `1920 x 1080`
   - Match: `0.5`

### Why This Works:
- Canvas now renders relative to the camera
- Automatically respects camera viewport/letterbox
- UI only appears in the visible game area

### When to Use:
- ✓ Standard 2D games
- ✓ When you need UI to perfectly match game content
- ✓ Most common use case

## Solution 2: CanvasLetterboxAdapter Script

For cases where you **must** keep Screen Space - Overlay mode:

### Setup:

1. **Select your Canvas**
2. **Canvas Component Settings:**
   - Render Mode: `Screen Space - Overlay`
3. **Add Component:** `CanvasLetterboxAdapter`
4. **CanvasLetterboxAdapter Settings:**
   - Target Camera: [Drag your Main Camera here]
   - Continuous Update: ✓ (enabled)
5. **Canvas Scaler Settings:**
   - UI Scale Mode: `Scale With Screen Size`
   - Reference Resolution: `1920 x 1080`
   - Match: `0.5`

### When to Use:
- ✓ Need overlay mode for specific rendering reasons
- ✓ Multiple canvases with different behaviors
- ✓ Advanced UI setups

## Solution 3: Combination for Mobile

For mobile games that need both letterboxing AND notch support:

### Setup:

1. **Canvas Setup:**
   - Render Mode: `Screen Space - Camera`
   - Render Camera: Main Camera (with AspectRatioController)
   - Canvas Scaler: Scale With Screen Size, 1920x1080, Match 0.5

2. **Create SafeAreaPanel:**
   - Right-click Canvas → UI → Panel
   - Name it "SafeAreaPanel"
   - RectTransform: Stretch to fill (Anchor Min: 0,0 / Max: 1,1)
   - Add Component: `SafeAreaHandler`

3. **Put UI inside SafeAreaPanel:**
   ```
   Canvas (Screen Space - Camera)
   └─ SafeAreaPanel (SafeAreaHandler)
      ├─ YourButton
      ├─ YourText
      └─ ... (all your UI)
   ```

### Why This Works:
- Screen Space - Camera → Respects letterbox
- SafeAreaHandler → Respects device notches
- Double protection for perfect UI on all devices!

### When to Use:
- ✓ Mobile games targeting iOS/Android
- ✓ Devices with notches (iPhone X+)
- ✓ Production-ready setup

## Common Issues & Fixes

### Issue: UI Appears in Black Bars

**Cause:** Canvas not respecting camera viewport

**Fix:** Use Screen Space - Camera mode or add CanvasLetterboxAdapter

### Issue: UI is Stretched/Squashed

**Cause:** Canvas Scaler not configured correctly

**Fix:** 
```
Canvas Scaler:
- UI Scale Mode: Scale With Screen Size ✓
- Reference Resolution: 1920 x 1080 ✓
- Match: 0.5 ✓
```

### Issue: UI Scale Different on Different Phones

**Cause:** Match value too far from 0.5

**Fix:** Set Match to `0.5` for balanced scaling

### Issue: UI Too Big/Small

**Cause:** Reference Resolution doesn't match your design

**Fix:** Set Reference Resolution to what you designed for (usually 1920x1080)

### Issue: UI Appears Behind Game Objects

**Cause (Screen Space - Camera):** Plane Distance too low

**Fix:** Increase Plane Distance or adjust Sorting Order

## Complete Setup Checklist

### Basic Setup (No Mobile, No Notches):
- [ ] AspectRatioController on Main Camera
- [ ] Canvas: Screen Space - Camera mode
- [ ] Canvas: Render Camera = Main Camera
- [ ] Canvas Scaler: Scale With Screen Size
- [ ] Canvas Scaler: Reference Resolution = 1920x1080
- [ ] Canvas Scaler: Match = 0.5

### Mobile Setup (With Notch Support):
- [ ] AspectRatioController on Main Camera
- [ ] Canvas: Screen Space - Camera mode
- [ ] Canvas: Render Camera = Main Camera
- [ ] Canvas Scaler: Scale With Screen Size (1920x1080, Match 0.5)
- [ ] SafeAreaPanel inside Canvas
- [ ] SafeAreaHandler on SafeAreaPanel
- [ ] All UI inside SafeAreaPanel

### Advanced Setup (Multiple Canvases):
- [ ] Gameplay Canvas: Screen Space - Camera
- [ ] HUD Canvas: Screen Space - Camera (higher Plane Distance)
- [ ] Popup/Menu Canvas: Screen Space - Overlay with CanvasLetterboxAdapter

## Testing Your Setup

1. **Add AspectRatioTester** to scene
2. **Enter Play Mode**
3. **Test these resolutions:**
   - 1920x1080 (16:9) - Should look perfect
   - 1080x2340 (19.5:9) - Should have letterbox bars, UI not in bars
   - 1024x768 (4:3) - Should have pillarbox bars, UI not in bars

4. **What to Look For:**
   - ✓ UI only appears in the game area (not in black bars)
   - ✓ UI elements maintain correct proportions
   - ✓ Button sizes look correct
   - ✓ Text reads clearly

## Quick Reference

**Need to fix UI appearing in letterbox bars?**
→ Canvas: Change to "Screen Space - Camera"

**Already using Screen Space - Camera but UI still wrong?**
→ Check Canvas Scaler settings (Scale With Screen Size, 1920x1080, Match 0.5)

**Need Screen Space - Overlay mode specifically?**
→ Add CanvasLetterboxAdapter script to Canvas

**Targeting mobile with notches?**
→ Use Screen Space - Camera + SafeAreaHandler combo

**Multiple canvases with different needs?**
→ Mix approaches: Camera for gameplay, Overlay+Adapter for popups
