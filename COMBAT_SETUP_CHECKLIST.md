# Combat System - Setup Checklist ✅

## 📋 Complete Setup Checklist

Follow this checklist to set up combat in all 4 levels. Takes about 10-15 minutes per level.

---

## ✅ Phase 1: One-Time Setup (Do Once)

### Step 1.1: Create All Prefabs
- [ ] Open Unity
- [ ] Go to **Window > Combat System > Setup Wizard**
- [ ] Click **"Create All Prefabs"**
- [ ] Verify prefabs created in `Assets/Prefabs/Combat/`:
  - [ ] Enemy_Chaser.prefab (red cube)
  - [ ] Enemy_Shooter.prefab (cyan sphere)
  - [ ] Enemy_Spawner.prefab (yellow cube)
  - [ ] WinTrigger.prefab (green cube)
  - [ ] Projectile_Enemy.prefab (red sphere) - Create via Quick Fixes

### Step 1.2: Create Tags
- [ ] Go to **Window > Combat System > Quick Fixes**
- [ ] Click **"Create Required Tags"**
- [ ] Verify tags exist: Enemy, Finish

### Step 1.3: Setup Player Projectile
- [ ] Find your player projectile prefab
  - Search for "Projectile_Player" in Project window
  - Or check Addressables groups
- [ ] Select the projectile prefab
- [ ] Go to **Window > Combat System > Quick Fixes**
- [ ] Click **"Add DamageDealer to Selected Prefab"**
- [ ] Verify settings:
  - [ ] Damage Amount: 1
  - [ ] Destroy On Hit: ✅
  - [ ] Target Tags: "Enemy"

### Step 1.4: Setup Enemy Projectile
- [ ] Go to **Window > Combat System > Quick Fixes**
- [ ] Click **"Create Enemy Projectile Prefab"**
- [ ] Select the created `Projectile_Enemy.prefab`
- [ ] In Inspector, check **"Addressable"**
- [ ] Set Address to: **Projectile_Enemy**
- [ ] Click **"Build > New Build > Default Build Script"** (Addressables window)

---

## ✅ Phase 2: Level Setup (Do For Each Level)

### Step 2.1: Open Level Scene
- [ ] Open `Level1.unity` (or Level2, 3, 4)

### Step 2.2: Add Combat to Level
- [ ] Go to **Window > Combat System > Level Setup Tool**
- [ ] Drag prefabs:
  - [ ] Spawner Prefab: `Enemy_Spawner`
  - [ ] Win Trigger Prefab: `WinTrigger`
- [ ] Set **Number of Spawners**: 3-5
- [ ] Set **Spread Distance**: 5-10
- [ ] Click **"Auto-Setup Level"**

### Step 2.3: Verify Player Health
- [ ] Select the Player GameObject
- [ ] Verify components exist:
  - [ ] HealthSystem (with Max Health = 3)
  - [ ] PlayerHealth

### Step 2.4: Configure Spawners
For each spawner in the scene:
- [ ] Select spawner
- [ ] Set **Enemy Prefab**: `Enemy_Chaser` or `Enemy_Shooter`
- [ ] Set **Spawn Interval**: 3 seconds
- [ ] Set **Max Enemies**: 5
- [ ] Set **Spawn Radius**: 1
- [ ] Position spawner in level (move it where you want)

### Step 2.5: Verify Win Trigger Spawner
- [ ] Find **WinTriggerSpawner** GameObject in scene
- [ ] Verify **Win Trigger Prefab** is assigned
- [ ] Set spawn position (optional)

### Step 2.6: Add Health UI
- [ ] Find your **Canvas** in the scene
- [ ] Right-click Canvas → **UI > Horizontal Layout Group**
- [ ] Name it: `HealthContainer`
- [ ] Position in top-left corner
- [ ] Create 3 child **UI > Image** objects
  - [ ] Name: Heart_1, Heart_2, Heart_3
  - [ ] Make them white squares (or use heart sprites)
  - [ ] Size: 50x50 each
- [ ] Create empty GameObject: `HealthUI_Manager`
- [ ] Add Component: **HealthUI**
- [ ] Assign:
  - [ ] Hearts Container: `HealthContainer`
  - [ ] Heart Prefab: `Heart_1`
  - [ ] Auto Find Player: ✅

### Step 2.7: Test Level (Optional)
- [ ] Press Play
- [ ] Verify:
  - [ ] Health UI shows in top-left
  - [ ] Spawners spawn enemies
  - [ ] Enemies chase/shoot you
  - [ ] Player takes damage when hit
  - [ ] Player can shoot and kill enemies
  - [ ] Destroying all spawners spawns win trigger
  - [ ] Touching win trigger completes level

---

## ✅ Phase 3: Polish & Balance (Optional)

### Visual Effects
For each spawner/enemy, adjust **GlitchEffect** component:
- [ ] Flicker Speed: 10-30
- [ ] Enable Color Glitch: ✅
- [ ] Glitch Colors: Cyan + Magenta (or custom)
- [ ] Enable Jitter: ✅ (optional)

### Difficulty Tuning
#### Easy (Level 1)
- [ ] 2-3 spawners
- [ ] Only Chaser enemies
- [ ] Spawner Health: 3
- [ ] Spawn Interval: 5 seconds

#### Medium (Level 2)
- [ ] 3-4 spawners
- [ ] Mix Chasers and Shooters
- [ ] Spawner Health: 5
- [ ] Spawn Interval: 3 seconds

#### Hard (Level 3)
- [ ] 4-5 spawners
- [ ] Mostly Shooters
- [ ] Spawner Health: 7
- [ ] Spawn Interval: 2 seconds

#### Boss (Level 4)
- [ ] 5+ spawners
- [ ] All enemy types
- [ ] Spawner Health: 10
- [ ] Spawn Interval: 1.5 seconds

---

## ✅ Phase 4: Testing & Debugging

### Add Debug Tools (Temporary)
- [ ] Add empty GameObject: `CombatDebugger`
- [ ] Add Component: **CombatDebugHUD**
- [ ] Add Component: **CombatTester**
- [ ] Press Play
- [ ] Press F1 to toggle debug HUD
- [ ] Use number keys 1-5 to test combat

### Debug Controls
- `1` - Damage player
- `2` - Heal player
- `3` - Damage nearest enemy
- `4` - Kill all enemies
- `5` - Spawn win trigger

### Checklist
- [ ] Player takes damage from enemies
- [ ] Player shoots and kills enemies
- [ ] Spawners spawn enemies
- [ ] Destroying all spawners shows win trigger
- [ ] Win trigger completes level
- [ ] Health UI updates correctly
- [ ] Game restarts when player dies

---

## ✅ Phase 5: Final Touches

### Remove Debug Tools
- [ ] Delete `CombatDebugger` GameObject (or disable it)

### Save & Build
- [ ] Save scene (Ctrl+S)
- [ ] Repeat Phase 2 for remaining levels
- [ ] Test all 4 levels in order

### Addressables Build
- [ ] Window > Asset Management > Addressables > Groups
- [ ] Build > New Build > Default Build Script
- [ ] Verify all addressables loaded:
  - [ ] Projectile_Player
  - [ ] Projectile_Enemy
  - [ ] All level assets

---

## 🚨 Troubleshooting Checklist

### "Player bullets don't damage enemies"
- [ ] Player projectile has DamageDealer component
- [ ] DamageDealer targets "Enemy" tag
- [ ] Enemies are tagged "Enemy"
- [ ] Player projectile has Collider2D (is trigger)

### "Enemy bullets don't damage player"
- [ ] Enemy projectile has DamageDealer component
- [ ] DamageDealer targets "Player" tag
- [ ] Player is tagged "Player"
- [ ] Projectile_Enemy is in Addressables with correct key

### "Enemies don't spawn"
- [ ] Spawner has Enemy Prefab assigned
- [ ] Spawner has HealthSystem component
- [ ] Spawner is not destroyed
- [ ] Check Console for errors

### "Win trigger doesn't appear"
- [ ] WinTriggerSpawner component in scene
- [ ] Win Trigger Prefab assigned to spawner
- [ ] All spawners must be destroyed first
- [ ] Check Console: "Win trigger spawned!"

### "Health UI doesn't show"
- [ ] HealthUI component on GameObject in Canvas
- [ ] Hearts Container assigned
- [ ] Auto Find Player enabled
- [ ] Player has HealthSystem component

### "Game doesn't restart when player dies"
- [ ] Player has PlayerHealth component
- [ ] LevelManager component in scene
- [ ] Restart Level On Death enabled

---

## 📊 Progress Tracker

### Level 1: □ Complete
- [ ] Spawners added
- [ ] Enemy types configured
- [ ] Health UI working
- [ ] Win trigger works
- [ ] Balanced & tested

### Level 2: □ Complete
- [ ] Spawners added
- [ ] Enemy types configured
- [ ] Health UI working
- [ ] Win trigger works
- [ ] Balanced & tested

### Level 3: □ Complete
- [ ] Spawners added
- [ ] Enemy types configured
- [ ] Health UI working
- [ ] Win trigger works
- [ ] Balanced & tested

### Level 4: □ Complete
- [ ] Spawners added
- [ ] Enemy types configured
- [ ] Health UI working
- [ ] Win trigger works
- [ ] Balanced & tested

---

## 🎉 You're Done!

All 4 levels now have:
- ✅ Enemy combat
- ✅ Player health system
- ✅ Spawn nodes to destroy
- ✅ Win condition
- ✅ Glitch visual effects
- ✅ Full integration with existing game

**Estimated total time:** 45-60 minutes for all 4 levels

Good luck and have fun! 🎮
