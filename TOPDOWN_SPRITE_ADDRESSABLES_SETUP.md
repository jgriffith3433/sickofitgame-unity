# Setup Guide: TopDownController & SpriteFramePlayer with Addressables

This guide shows you exactly how to convert your TopDownController and SpriteFramePlayer to use Addressables.

## 📋 Summary

**What needs to be done:**
1. ✅ Replace old TopDownController with new Addressables version
2. ✅ Replace old SpriteFramePlayer with new Addressables version  
3. ✅ Mark projectile prefab as Addressable
4. ✅ Mark sprite PNG frames as Addressable with labels
5. ✅ Test everything works

---

## Part 1: TopDownController (Projectile Shooting)

### Step 1: Replace the Script

**Option A - In-Place Replacement (Easier):**
1. Open your existing TopDownController.cs
2. Copy the entire contents from `TopDownController_Addressables.cs`
3. Paste over your existing file
4. Save

**Option B - Keep Both Versions:**
1. Rename your current script to `TopDownController_OLD.cs`
2. Rename `TopDownController_Addressables.cs` to `TopDownController.cs`
3. Unity will automatically update references

### Step 2: Update Your Player GameObject

1. Select your player GameObject in the scene (the one with TopDownController)
2. Look at the TopDownController component in Inspector
3. You'll see a **new field**: "Projectile Address"
4. **IMPORTANT**: Note the name of your old `Projectile Prefab` (if you had one assigned)
5. Enter that name in the "Projectile Address" field (e.g., "Projectile_Player")

### Step 3: Mark Projectile Prefab as Addressable

1. **Find your projectile prefab** in the Project window (usually in Assets/Prefabs/)
2. **Select it**
3. In the Inspector, **check the "Addressable" checkbox** at the top
4. **Set the address** to match what you entered in Step 2 (e.g., "Projectile_Player")

Example:
```
Prefab Name: PlayerProjectile.prefab
Addressable: ✓ Checked
Address: Projectile_Player
```

### Step 4: Test It!

1. Enter Play Mode
2. Try shooting
3. Projectiles should spawn instantly (they're preloaded in Awake)

**If projectiles don't spawn:**
- Check Console for errors
- Verify the address matches exactly (case-sensitive!)
- Make sure you built Addressables (Window → Addressables → Groups → Build)

---

## Part 2: SpriteFramePlayer (PNG Frame Animation)

### Step 1: Replace the Script

Same as TopDownController - either replace in-place or keep both versions.

### Step 2: Organize Your Sprite Frames

Your PNG frames need to be in a specific structure. Let's say you have video frames:

```
Assets/Videos/MyVideo_frames/
  ├── frame_0001.png
  ├── frame_0002.png
  ├── frame_0003.png
  └── ... (more frames)
```

### Step 3: Mark All Frames as Addressable with a Label

**Quick Method (Select All):**
1. Select ALL your frame PNGs (frame_0001 through frame_XXXX)
2. In Inspector, check "Addressable" for all of them
3. **IMPORTANT**: Assign the same **Label** to all frames

**How to add a label:**
1. With all frames selected and Addressable checked
2. Look in the Inspector for "Labels" dropdown
3. Click it and select "Create New Label"
4. Name it something descriptive like: `Video1_Frames` or `Character_Walk_Frames`
5. Make sure ALL frames have this label

**Example for multiple animations:**
```
Video 1 Frames → Label: "Video1_Frames"
Video 2 Frames → Label: "Video2_Frames"
Character Walk → Label: "Character_Walk"
Character Idle → Label: "Character_Idle"
```

### Step 4: Configure SpriteFramePlayer in Scene

1. Select your GameObject with SpriteFramePlayer
2. Look at the component in Inspector
3. Set "Frames Label" to match the label you created (e.g., "Video1_Frames")
4. Check "Preload On Awake" to load frames immediately ✓
5. Check "Play On Load" if you want automatic playback ✓

### Step 5: Test It!

1. Enter Play Mode
2. Frames should load automatically
3. Animation should play smoothly

**Check Console for:**
- "Loading sprite frames with label: Video1_Frames"
- "Loaded X frames for: Video1_Frames"

**If frames don't load:**
- Verify ALL frames have the Addressable checkbox checked
- Verify ALL frames have the SAME label
- Verify the label matches exactly in the Inspector
- Build Addressables first!

---

## Important: Build Addressables Before Testing!

Every time you mark new assets as Addressable or change labels:

1. Open: **Window → Asset Management → Addressables → Groups**
2. Click: **Build → New Build → Default Build Script**
3. Wait for it to finish
4. Now test in Play Mode

---

## Troubleshooting

### "Failed to load projectile prefab"
- ❌ Projectile prefab not marked as Addressable
- ❌ Address doesn't match what's in TopDownController
- ❌ Didn't build Addressables

### "Failed to load frames with label"
- ❌ Frames not marked as Addressable
- ❌ Frames missing the label
- ❌ Label name doesn't match what's in SpriteFramePlayer
- ❌ Didn't build Addressables

### "Projectile prefab not loaded yet"
- ⏳ Prefab is still loading (should only happen for a split second)
- If it persists, check that PreloadProjectilePrefab() is being called

### Frames play but in wrong order
- Order is alphabetical by sprite name
- Make sure your frames are named: frame_0001, frame_0002, etc.
- Leading zeros matter! (frame_1, frame_10 sorts wrong)

---

## Performance Tips

### For TopDownController:
- ✅ Projectile is preloaded in Awake() - instant firing!
- ✅ Only loads once, cached for entire level
- ✅ No stutter when first shot fires

### For SpriteFramePlayer:
- ✅ Set "Preload On Awake" to true for smooth playback
- ✅ Frames load once at scene start
- ✅ Use labels to load multiple animations efficiently
- ✅ All frames of one animation go in one Addressable group

---

## Advanced: Organizing Addressables Groups

For better organization:

1. Open Addressables Groups window
2. Create a group: **Right-click → Create New Group → Packed Assets**
3. Name it "Level1_Projectiles" or "Level1_Animations"
4. Drag your projectile/frames into that group

This keeps level-specific assets together and loads them as a bundle.

**Example structure:**
```
Groups:
├── Shared (materials, common sprites)
├── Level1_Assets
│   ├── Projectile_Player
│   └── Video1_Frames (all frame sprites)
├── Level2_Assets
│   ├── Projectile_PowerUp
│   └── Video2_Frames
```

---

## Quick Reference

### TopDownController Setup:
1. Replace script file
2. Set "Projectile Address" in Inspector
3. Mark prefab as Addressable with matching address
4. Build Addressables
5. Test!

### SpriteFramePlayer Setup:
1. Replace script file
2. Select all frame PNGs
3. Mark as Addressable
4. Assign same Label to all
5. Set "Frames Label" in Inspector to match
6. Build Addressables
7. Test!

---

## Verification Checklist

Before building your game, verify:

✅ TopDownController uses new script  
✅ Projectile prefab is Addressable with correct address  
✅ All sprite frames are Addressable  
✅ All frames in an animation have the same label  
✅ SpriteFramePlayer "Frames Label" matches the label  
✅ Addressables are built (Window → Addressables → Groups → Build)  
✅ Both work in Play Mode  

---

**You're done!** Your gameplay assets now load on-demand for optimized WebGL builds! 🎮
