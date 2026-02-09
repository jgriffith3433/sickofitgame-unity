## Quick Visual Guide: Addressables Setup

### **Before (Old Way)**
```
TopDownController.cs
  ├─ [SerializeField] GameObject projectilePrefab ❌
  └─ Instantiate(projectilePrefab) 
       └─ Prefab bundled into EVERY scene = Large builds!

SpriteFramePlayer.cs  
  ├─ [SerializeField] List<Sprite> frames ❌
  └─ 50+ sprite references = All in build = Huge!
```

### **After (Addressables Way)**
```
TopDownController.cs
  ├─ [SerializeField] string projectileAddress = "Projectile_Player" ✅
  ├─ Preload in Awake() → cached
  └─ Instantiate(cachedProjectilePrefab)
       └─ Prefab downloads on-demand = Small builds!

SpriteFramePlayer.cs
  ├─ [SerializeField] string framesLabel = "Video1_Frames" ✅
  ├─ LoadAssetsByLabel("Video1_Frames") → loads all 50 sprites
  └─ Sprites cached in memory
       └─ Sprites download on-demand = Tiny initial load!
```

---

## What Happens at Runtime

### Scene Loads:
```
1. Scene starts loading
2. MainCamera ✅ loads (in scene)
3. Player GameObject ✅ loads (in scene) 
4. Ground ✅ loads (in scene)
5. TopDownController script ✅ loads (in scene)
   └─ But projectile prefab ❌ does NOT auto-load!
6. SpriteFramePlayer script ✅ loads (in scene)
   └─ But sprite frames ❌ do NOT auto-load!
```

### Your Scripts Run:
```
TopDownController.Awake():
  ├─ PreloadProjectilePrefab() called
  ├─ Downloads projectile from server (or cache)
  └─ cachedProjectilePrefab = loaded prefab ✅

SpriteFramePlayer.Awake():
  ├─ LoadFrames() called  
  ├─ Downloads all 50 sprites by label
  └─ frames list populated ✅

Player can now shoot ✅
Animation can now play ✅
```

---

## File Structure Example

### Your Project Folder:
```
Assets/
├── Prefabs/
│   └── PlayerProjectile.prefab
│       └── ✅ Addressable
│       └── Address: "Projectile_Player"
│
├── Videos/
│   └── Video1_frames/
│       ├── frame_0001.png ✅ Addressable, Label: "Video1_Frames"
│       ├── frame_0002.png ✅ Addressable, Label: "Video1_Frames"
│       ├── frame_0003.png ✅ Addressable, Label: "Video1_Frames"
│       └── ... (all frames same label)
│
└── Scenes/
    └── Level1.unity
        └── Player GameObject
            ├── TopDownController
            │   └── Projectile Address: "Projectile_Player"
            └── SpriteFramePlayer  
                └── Frames Label: "Video1_Frames"
```

### Addressables Groups Window:
```
Default Local Group
├── PlayerProjectile.prefab
│   Address: Projectile_Player
├── frame_0001.png
│   Labels: Video1_Frames
├── frame_0002.png
│   Labels: Video1_Frames
├── frame_0003.png
│   Labels: Video1_Frames
└── ... (all frames)
```

---

## The Magic of Labels

**Why use labels for sprites?**

Instead of:
```csharp
LoadAsset("frame_0001")
LoadAsset("frame_0002")
LoadAsset("frame_0003")
// ... 50 times!
```

You do:
```csharp
LoadAssetsByLabel("Video1_Frames")
// Loads ALL 50 at once! 🎉
```

---

## Checklist Format

### TopDownController Setup:
```
□ Copy TopDownController_Addressables.cs content to TopDownController.cs
□ Select Player GameObject
□ Set "Projectile Address" = "Projectile_Player"
□ Select projectile prefab in Project
□ Check "Addressable" ✓
□ Set Address = "Projectile_Player" (matches above!)
□ Build Addressables
□ Test in Play Mode - projectiles should fire!
```

### SpriteFramePlayer Setup:
```
□ Copy SpriteFramePlayer_Addressables.cs content to SpriteFramePlayer.cs  
□ Select ALL frame PNG files (frame_0001 through frame_XXXX)
□ Check "Addressable" ✓ on all
□ Create new Label: "Video1_Frames"
□ Assign this label to ALL selected frames
□ Select GameObject with SpriteFramePlayer
□ Set "Frames Label" = "Video1_Frames"
□ Check "Preload On Awake" ✓
□ Check "Play On Load" ✓
□ Build Addressables
□ Test in Play Mode - animation should play!
```

---

## Common Mistakes to Avoid

### ❌ Mistake 1: Forgetting to Build Addressables
**Symptom:** "Asset not found" errors  
**Fix:** Window → Addressables → Groups → Build

### ❌ Mistake 2: Address/Label Mismatch
**Symptom:** Assets don't load
**Fix:** 
- TopDownController "Projectile Address" must match prefab's Address EXACTLY
- SpriteFramePlayer "Frames Label" must match sprites' Label EXACTLY

### ❌ Mistake 3: Not All Frames Have Label
**Symptom:** Only some frames load, animation looks broken  
**Fix:** Select ALL frames, verify they ALL have the same label

### ❌ Mistake 4: Frames Named Wrong
**Symptom:** Frames play in wrong order  
**Fix:** Rename to: frame_0001, frame_0002, frame_0003 (leading zeros!)

### ❌ Mistake 5: Testing Without Preload
**Symptom:** First shot doesn't fire, or animation stutters
**Fix:** 
- TopDownController: Preload happens in Awake() automatically ✅
- SpriteFramePlayer: Set "Preload On Awake" = true ✅

---

**Still stuck?** Check [TOPDOWN_SPRITE_ADDRESSABLES_SETUP.md](TOPDOWN_SPRITE_ADDRESSABLES_SETUP.md) for detailed troubleshooting!
