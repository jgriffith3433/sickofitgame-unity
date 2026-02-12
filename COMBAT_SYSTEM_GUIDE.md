# Combat System - Quick Start Guide

## 🎮 What You Just Got

A complete combat system with:
- ✅ Player health (3 HP by default)
- ✅ Two enemy types (Chaser & Shooter)
- ✅ Enemy spawners (destroy to win)
- ✅ Collision-based damage
- ✅ Glitch visual effects
- ✅ Editor tools for fast setup

---

## 📦 Step 1: Create All Prefabs (1 Minute)

1. In Unity, go to **Window > Combat System > Setup Wizard**
2. Set health values (or keep defaults):
   - Player Max Health: 3
   - Chaser Enemy Health: 2
   - Shooter Enemy Health: 3
   - Spawner Health: 5
3. Click **"Create All Prefabs"**
4. Done! Prefabs created in `Assets/Prefabs/Combat/`

**What you get:**
- `Enemy_Chaser.prefab` - Red cube that chases player
- `Enemy_Shooter.prefab` - Cyan sphere that shoots
- `Enemy_Spawner.prefab` - Yellow cube that spawns enemies
- `WinTrigger.prefab` - Green cube that ends level

---

## 🎪 Step 2: Setup Your Level (2 Minutes)

### Option A: Auto-Setup (Recommended)
1. Open any Level scene (Level1, Level2, etc.)
2. Go to **Window > Combat System > Level Setup Tool**
3. Drag prefabs from `Assets/Prefabs/Combat/` into the tool:
   - Spawner Prefab → `Enemy_Spawner`
   - Win Trigger Prefab → `WinTrigger`
4. Adjust settings:
   - Number of Spawners: 3-5
   - Spread Distance: 5-10
5. Click **"Auto-Setup Level"**
6. Done! Your level now has combat.

### Option B: Manual Setup
1. Click **"Add Player Health Component"** in the tool
2. Click **"Add WinTriggerSpawner to Scene"**
3. Manually drag `Enemy_Spawner` prefabs into the scene
4. Position them where you want

---

## 🎨 Step 3: Setup Health UI (2 Minutes)

### Create Health Hearts/Squares:

1. Find your **Canvas** in the scene
2. Right-click Canvas → **UI > Image** (create 3 times for 3 hearts)
3. Name them: `Heart_1`, `Heart_2`, `Heart_3`
4. Position them in top-left corner
5. Make them white squares or use heart sprites

### Add HealthUI Component:

1. Create empty GameObject in Canvas: `HealthUI`
2. Add Component: **HealthUI** script
3. Assign references:
   - **Hearts Container**: The parent of your heart images
   - **Heart Prefab**: One of your heart images (for reference)
   - Check **"Auto Find Player"**

---

## 🔫 Step 4: Setup Player Projectiles (Important!)

Your player can already shoot, but the bullets need to damage enemies:

1. Find your player projectile prefab (probably in Addressables)
2. Add Component: **DamageDealer**
3. Set properties:
   - **Damage Amount**: 1
   - **Destroy On Hit**: ✅ (checked)
   - **Target Tags**: Add "Enemy"
   - **Can Damage Multiple**: ❌ (unchecked)

**If you can't find the projectile:**
- Look in `Assets/Prefabs/` or search for "Projectile_Player"
- Or check your Addressables groups for "Projectile_Player"

---

## 🎯 Step 5: Configure Spawners

Click on any spawner in your scene and adjust:

### Enemy Spawner Settings:
- **Enemy Prefab**: Drag `Enemy_Chaser` or `Enemy_Shooter`
- **Spawn Interval**: 3 seconds (how often to spawn)
- **Max Enemies**: 5 (max alive at once)
- **Spawn Radius**: 1 (spawn area size)

### Spawner Health:
- **Max Health**: 5 hits to destroy
- **Destroy On Death**: ✅ (checked)

---

## 🎨 Customize Visual Effects

### Glitch Effect (Already on enemies & spawners)
Each enemy/spawner has a **GlitchEffect** component:
- **Enable Flicker**: Makes them transparent/opaque
- **Flicker Speed**: How fast they flicker
- **Enable Color Glitch**: Adds cyan/magenta colors
- **Enable Jitter**: Makes them shake slightly

### Make It Look More "Glitchy":
- Increase **Flicker Speed** to 20-30
- Enable **Position Jitter** with 0.05 amount
- Change **Glitch Colors** to neon colors

---

## 🎮 How It Works (Game Loop)

1. **Level starts** → Player has 3 HP
2. **Spawners** spawn enemies every few seconds
3. **Enemies** chase/shoot player
4. **Player** shoots enemies to kill them
5. **Destroy all spawners** → Win trigger appears
6. **Touch win trigger** → Level complete!

### Enemy Behavior:
- **Chaser** (Red cube): Moves toward player, damages on touch
- **Shooter** (Cyan sphere): Stays in place, shoots projectiles

### Damage:
- Player hit by enemy: -1 HP
- Player at 0 HP: Level restarts
- Enemy hit by player bullet: Takes damage based on health
- Spawner destroyed: All its enemies die too

---

## 🔧 Advanced: Create More Enemy Types

Want a third enemy type? Just:

1. Duplicate `Enemy_Chaser.prefab`
2. Rename to `Enemy_Custom.prefab`
3. Change the **EnemyChaser** script to **EnemyShooter**
4. Adjust settings (speed, health, etc.)
5. Change color/shape to distinguish it

---

## 🐛 Troubleshooting

### "Player bullets don't damage enemies"
- Add **DamageDealer** to player projectile (see Step 4)
- Make sure enemy tag is "Enemy"

### "Win trigger doesn't spawn"
- Check your scene has **WinTriggerSpawner** component
- Assign the **WinTrigger** prefab to it

### "Enemies don't spawn"
- Check spawner has **enemy prefab** assigned
- Make sure spawner has health and isn't destroyed

### "No health UI appears"
- Make sure HealthUI is in a Canvas
- Check "Auto Find Player" is enabled

### "Enemies shoot but bullets don't hurt player"
- Enemy projectiles need **DamageDealer** component
- Target Tags should be "Player"

---

## 📝 Tags You Need

Make sure these tags exist (should already):
- **Player** - On your player GameObject
- **Enemy** - On all enemy prefabs and spawners (auto-added by setup tool)
- **Finish** - On win trigger (auto-added)

---

## 🎯 Recommended Settings Per Level

### Level 1 (Easy):
- 2-3 spawners
- Only Chaser enemies
- Spawner Health: 3

### Level 2 (Medium):
- 3-4 spawners
- Mix of Chasers and Shooters
- Spawner Health: 5

### Level 3 (Hard):
- 4-5 spawners
- Mostly Shooters
- Spawner Health: 7

### Level 4 (Boss Level):
- 5+ spawners close together
- All enemy types
- Spawner Health: 10

---

## 🚀 You're Done!

Press Play and test your level. You should be able to:
- ✅ See health UI in top-left
- ✅ Take damage from enemies
- ✅ Shoot and destroy enemies
- ✅ Destroy all spawners
- ✅ Touch the win trigger to complete level

**The whole system is fully integrated** with your existing:
- Player controls (mobile + PC)
- Level progression system
- Video playback between levels
- Scene management

Just set it up once per level using the editor tools!

---

## 💡 Quick Tips

1. **Test one spawner first** - Make sure it works before adding more
2. **Adjust spawn rates** - Start slow (5 seconds) then speed up
3. **Place spawners strategically** - Make player move around the map
4. **Use different enemy types per spawner** - Variety is fun
5. **Add more health to player** - If it's too hard, give 5 HP instead of 3

Good luck! You've got a full combat system now. 🎮
