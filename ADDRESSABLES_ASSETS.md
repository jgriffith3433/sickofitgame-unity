# Addressables Asset Management Guide

## Understanding How Addressables Bundles Assets

### Visual Example

```
Scene: Level1
├── Player (in hierarchy) ✅ Auto-bundled with scene
├── Ground (in hierarchy) ✅ Auto-bundled with scene
├── EnemySpawner (in hierarchy) ✅ Auto-bundled with scene
    └── Script references: enemyPrefab ❌ NOT bundled!
        
Assets/Prefabs/Enemy.prefab ❌ NOT bundled unless marked Addressable
```

**The Rule:** Addressables only bundles what's IN the scene. References to prefabs/assets outside the scene must be marked separately.

### What Gets Bundled Automatically

When you mark a **scene** as Addressable, Unity bundles:
- ✅ All GameObjects in the scene hierarchy
- ✅ Materials, textures, meshes assigned to scene objects
- ✅ Components and their serialized data
- ✅ Lighting data, nav meshes, etc.

### What Does NOT Get Bundled

- ❌ Prefabs you `Instantiate()` at runtime
- ❌ Assets loaded via `Resources.Load()`
- ❌ Assets referenced in scripts but not in the scene
- ❌ Dynamically loaded assets

**Solution:** Mark those runtime assets as Addressable too!

---

## Common Scenarios

### Scenario 1: Spawning Enemies at Runtime

**Old Way (Direct Reference):**
```csharp
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab; // ❌ Creates dependency
    
    void SpawnEnemy()
    {
        Instantiate(enemyPrefab);
    }
}
```

**Problem:** The `enemyPrefab` creates a dependency that pulls the prefab into EVERY scene's bundle.

**New Way (Addressables):**
```csharp
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private string enemyAddress = "Enemy_Basic"; // ✅ String reference
    
    void SpawnEnemy()
    {
        AddressablesAssetLoader.Instance.InstantiatePrefab(enemyAddress, (enemy) => 
        {
            // Enemy spawned!
        });
    }
}
```

**Setup:**
1. Find your enemy prefab in Project window
2. Check "Addressable" in Inspector
3. Set its address to "Enemy_Basic"

Now the enemy prefab loads on-demand, only when needed!

---

### Scenario 2: Shared Assets (Materials, Sprites)

**Problem:** If 5 levels all use the same material, it gets duplicated in each bundle.

**Solution:** Create a "Shared" group in Addressables

1. Window → Asset Management → Addressables → Groups
2. Create New Group → Name it "Shared"
3. Mark common assets as Addressable
4. Move them to the "Shared" group

Now the material downloads once and is shared across all levels!

---

### Scenario 3: Scriptable Objects (Config Data)

**Old Way:**
```csharp
[SerializeField] private LevelConfig levelConfig; // In Inspector
```

**New Way:**
```csharp
void Start()
{
    AddressablesAssetLoader.Instance.LoadAsset<LevelConfig>("Level1_Config", (config) =>
    {
        // Use config data
    });
}
```

**Setup:**
1. Mark your ScriptableObject as Addressable
2. Set its address (e.g., "Level1_Config")

---

### Scenario 4: Loading Multiple Related Assets

**Use Case:** Load all powerup prefabs at once

**Setup:**
1. Select all powerup prefabs
2. Make them Addressable
3. Assign the same label: "Powerup"

**Code:**
```csharp
AddressablesAssetLoader.Instance.LoadAssetsByLabel<GameObject>("Powerup", (prefabs) =>
{
    foreach (var prefab in prefabs)
    {
        Debug.Log($"Loaded powerup: {prefab.name}");
    }
});
```

---

## Step-by-Step: Making Assets Addressable

### For Individual Assets (Prefabs, Materials, etc.)

1. **Select asset** in Project window
2. **Check "Addressable"** in Inspector
3. **Set address** (optional - defaults to file name)
4. **Assign labels** (optional - for grouping)

### For Folders (Make All Contents Addressable)

1. **Select folder** in Project window
2. **Check "Addressable"**
3. All assets in folder become Addressable

### Organizing Assets

**Addressables Groups Window:**
- Window → Asset Management → Addressables → Groups
- Drag assets between groups
- Create groups for organization:
  - "Level1_Assets" - Only used by Level 1
  - "Level2_Assets" - Only used by Level 2
  - "Shared" - Used by multiple levels
  - "Enemies" - All enemy prefabs
  - "Powerups" - All powerup prefabs

---

## Best Practices

### 1. Use Labels for Categories
```
Label: "Enemy" - All enemy prefabs
Label: "Weapon" - All weapon prefabs
Label: "Effect" - All particle effects
Label: "Audio" - All sound effects
```

### 2. Preload Before You Need It
```csharp
void Start()
{
    // Preload wave 2 enemies during wave 1
    AddressablesAssetLoader.Instance.PreloadAsset<GameObject>("Enemy_Boss");
}
```

### 3. Clean Up When Done
```csharp
void OnDestroy()
{
    // Release assets when leaving a level
    AddressablesAssetLoader.Instance.ReleaseAllAssets();
}
```

### 4. Share Common Assets
Put frequently used assets in a "Shared" group:
- Common materials
- UI sprites
- Sound effects
- Character models

### 5. Group Assets by Usage
- **Level-specific group** - Only loaded with that level
- **Shared group** - Loaded once, used everywhere
- **On-demand group** - Loaded only when needed

---

## Migration Guide: Converting Existing Code

### Pattern 1: Direct Prefab References

**Before:**
```csharp
[SerializeField] private GameObject prefab;
Instantiate(prefab);
```

**After:**
```csharp
[SerializeField] private string prefabAddress = "MyPrefab";
AddressablesAssetLoader.Instance.InstantiatePrefab(prefabAddress, (obj) => {});
```

### Pattern 2: Resources.Load

**Before:**
```csharp
GameObject prefab = Resources.Load<GameObject>("Enemies/Boss");
Instantiate(prefab);
```

**After:**
```csharp
// Mark asset as Addressable with address "Enemies/Boss"
AddressablesAssetLoader.Instance.LoadPrefabThenInstantiate("Enemies/Boss", (obj) => {});
```

### Pattern 3: Accessing Materials/Sprites

**Before:**
```csharp
[SerializeField] private Material goldMaterial;
renderer.material = goldMaterial;
```

**After:**
```csharp
AddressablesAssetLoader.Instance.LoadAsset<Material>("Material_Gold", (mat) =>
{
    renderer.material = mat;
});
```

---

## Common Issues & Solutions

### Issue: "Asset not found"
**Solution:** 
- Check asset is marked as Addressable
- Verify address matches exactly (case-sensitive)
- Build Addressables before building game

### Issue: Asset loads slowly
**Solution:**
- Preload it earlier in the level
- Use labels to batch-load related assets
- Check network if using remote CDN

### Issue: Too many assets in one bundle
**Solution:**
- Split into multiple groups
- Use labels to organize better
- Don't put everything in one group

### Issue: Duplicate assets in multiple bundles
**Solution:**
- Move shared assets to a "Shared" group
- Check for circular dependencies
- Use Addressables Analyze tool

---

## Workflow Summary

1. **Identify runtime-loaded assets** (prefabs, materials, etc.)
2. **Mark them as Addressable** in Inspector
3. **Organize into groups** (Shared, Level1, Level2, etc.)
4. **Assign labels** for easy batch loading
5. **Update your code** to use AddressablesAssetLoader
6. **Build Addressables** before building game
7. **Test** - assets should load on-demand

---

## Performance Tips

### Preloading Strategy
```csharp
// At level start, preload everything you'll need
void Start()
{
    AddressablesAssetLoader.Instance.PreloadAssetsByLabel("Level1_Enemies");
    AddressablesAssetLoader.Instance.PreloadAssetsByLabel("Level1_Powerups");
}
```

### Memory Management
```csharp
// Release assets when transitioning
void OnLevelComplete()
{
    AddressablesAssetLoader.Instance.ReleaseAllAssets();
}
```

### Smart Loading
```csharp
// Load next wave's enemies while player fights current wave
void OnWaveComplete(int waveNumber)
{
    int nextWave = waveNumber + 1;
    AddressablesAssetLoader.Instance.PreloadAsset<GameObject>($"Enemy_Wave{nextWave}");
}
```

---

## Quick Reference

### Load and Spawn Prefab
```csharp
AddressablesAssetLoader.Instance.InstantiatePrefab("PrefabName", (obj) => {});
```

### Load Asset (No Spawn)
```csharp
AddressablesAssetLoader.Instance.LoadAsset<GameObject>("PrefabName", (prefab) => {});
```

### Load Multiple by Label
```csharp
AddressablesAssetLoader.Instance.LoadAssetsByLabel<GameObject>("LabelName", (list) => {});
```

### Preload
```csharp
AddressablesAssetLoader.Instance.PreloadAsset<GameObject>("PrefabName");
```

### Clean Up
```csharp
AddressablesAssetLoader.Instance.ReleaseAllAssets();
```

---

**Next:** See [AddressablesExamples.cs](AddressablesExamples.cs) for 12 practical code examples!
