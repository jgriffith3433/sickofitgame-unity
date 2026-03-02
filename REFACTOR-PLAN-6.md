# REFACTOR-PLAN-6: Full Config-Driven Generation

## Status: ✅ COMPLETE

## Goal
Make EVERYTHING config-driven + generator-generated. After this refactor:
- **Zero manual scene editing** — all 7 scenes (MainMenu, LevelSelection, VideoSelection, UpgradeShop, Video, Level, Death) are generated
- **Zero manual prefab editing** — all prefabs are generated (Player, Enemies, Projectiles, Pickups, Platforms, Buttons)
- **One button** — `SickOfIt → Generate Project` regenerates the entire game from config

---

## Phase 1: Fix PlayerPrefabGenerator Missing Wiring ✅

**Problem:** PlayerController has unwired fields:
- `groundLayer` / `platformLayer` — not set (critical for gameplay)
- `projectileSpawnPoint` — not created
- `InputActionReference` fields — not wired to InputSystem_Actions.inputactions

**Solution:**
```csharp
// In PlayerPrefabGenerator, add after current PlayerController wiring:

// 1. Create projectileSpawnPoint child
var spawnPoint = new GameObject("ProjectileSpawnPoint");
spawnPoint.transform.SetParent(root.transform);
spawnPoint.transform.localPosition = new Vector3(0.5f, 0f, 0f);
pcSo.FindProperty("projectileSpawnPoint").objectReferenceValue = spawnPoint.transform;

// 2. Set LayerMasks
pcSo.FindProperty("groundLayer").intValue = LayerMask.GetMask("Ground", "Default");
pcSo.FindProperty("platformLayer").intValue = LayerMask.GetMask("Platform");

// 3. Wire InputActionReferences from InputSystem_Actions.inputactions
var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
if (inputAsset != null)
{
    var playerMap = inputAsset.FindActionMap("Player");
    if (playerMap != null)
    {
        pcSo.FindProperty("moveAction").objectReferenceValue = GetActionReference(inputAsset, "Player/Move");
        pcSo.FindProperty("jumpAction").objectReferenceValue = GetActionReference(inputAsset, "Player/Jump");
        pcSo.FindProperty("fireAction").objectReferenceValue = GetActionReference(inputAsset, "Player/Fire");
        pcSo.FindProperty("lookAction").objectReferenceValue = GetActionReference(inputAsset, "Player/Look");
    }
}
```

---

## Phase 2: Create UI Config Types

### 2.1 UISceneConfig (base for all UI scenes)
```csharp
[Serializable]
public class UISceneConfig
{
    [Header("Camera")]
    public float cameraOrthoSize = 5f;
    public Color cameraBackgroundColor = Color.black;
    
    [Header("Canvas")]
    public Vector2 referenceResolution = new Vector2(1920, 1080);
    
    [Header("Background")]
    public Sprite backgroundSprite;
    
    [Header("Lighting")]
    public bool useDirectionalLight = true;
    public Color lightColor = Color.white;
    public float lightIntensity = 1f;
}
```

### 2.2 MainMenuConfig
```csharp
[Serializable]
public class MainMenuConfig : UISceneConfig
{
    [Header("Buttons")]
    public GameObject buttonPrefab; // Base button template
    public bool showContinueButton = true;
    public bool showNewGameButton = true;
    public bool showLevelSelectButton = true;
    public bool showVideosButton = true;
    public bool showUpgradeShopButton = true;
    public bool showQuitButton = true;
    
    [Header("Button Layout")]
    public float buttonSpacing = 50f;
    
    [Header("Roguelite UI")]
    public bool showCumulativeHealth = true;
    public bool showPointsDisplay = true;
    
    [Header("Bundle Download")]
    public bool showBundleDownloadUI = true;
    
    [Header("Transition")]
    public Sprite idleHouseSprite;  // House visual
    public Sprite explosionSprite;  // Explosion transition
    public float hideDelay = 1.5f;
    public float transitionDelay = 1f;
}
```

### 2.3 SelectionSceneConfig (shared for Level/Video selection)
```csharp
[Serializable]
public class SelectionSceneConfig : UISceneConfig
{
    [Header("Button Container")]
    public float buttonSpacing = 20f;
    
    [Header("Back Button")]
    public GameObject backButtonPrefab;
    
    [Header("Roguelite UI")]
    public bool showCumulativeHealth = true;
}
```

### 2.4 UpgradeShopConfig
```csharp
[Serializable]
public class UpgradeShopConfig : UISceneConfig
{
    [Header("Upgrades")]
    public UpgradeDefinition[] upgrades;
    
    [Header("Debug")]
    public bool showDebugAddPoints = true;
    public int debugPointsAmount = 500;
}

[Serializable]
public class UpgradeDefinition
{
    public string id;
    public string title;
    public string description;
    public int[] costsPerLevel; // Cost at each upgrade level
    public int maxLevel;
}
```

---

## Phase 3: Create Button Prefab Generators

### 3.1 ButtonPrefabGenerator
Generates base button prefab with:
- RectTransform (configurable size)
- MobileControllerFramework.Button component
- CanvasGroup
- Text (TMP) child
- Animator (optional)

### 3.2 LevelButtonPrefabGenerator  
Generates (Prb)ButtonLevel.prefab with:
- LevelButton component
- Level text, CompletedIndicator, LockedIndicator, HealthInfoText children

### 3.3 VideoButtonPrefabGenerator
Generates (Prb)ButtonVideo.prefab with:
- VideoButton component
- Video name text, LockedIndicator, optional thumbnail

---

## Phase 4: Create Scene Generators

### 4.1 MainMenuSceneGenerator
Creates MainMenu.unity with:
- Camera (wired)
- EventSystem + InputSystemUIInputModule
- Canvas with SafeAreaPanel, button container
- MainMenuManager (all fields wired)
- GameProgressManager, SceneTransitionManager references
- BundleDownloadManager + BundleDownloadUI
- House visual hierarchy (idleObjects)
- Explosion animation hierarchy (transitionObjects)
- All buttons instantiated and wired

### 4.2 LevelSelectionSceneGenerator
Creates LevelSelection.unity with:
- Same base structure (Camera, EventSystem, Canvas)
- LevelSelectionManager (all fields wired)
- levelButtonPrefab reference to generated (Prb)ButtonLevel
- Back button

### 4.3 VideoSelectionSceneGenerator
Creates VideoSelection.unity with:
- Same base structure
- VideoSelectionManager (all fields wired)
- videoButtonPrefab reference to generated (Prb)ButtonVideo
- Back button

### 4.4 UpgradeShopSceneGenerator
Creates UpgradeShop.unity with:
- Same base structure
- UpgradeShopManager (all fields wired)
- Dynamic upgrade UI creation from config

---

## Phase 5: Update GameDefinition

Add new config sections:
```csharp
[Header("UI Scenes")]
public MainMenuConfig mainMenuConfig;
public SelectionSceneConfig levelSelectionConfig;
public SelectionSceneConfig videoSelectionConfig;
public UpgradeShopConfig upgradeShopConfig;
```

---

## Phase 6: Update ProjectGenerator

Current order:
1. Phase 1: Cleanup
2. Phase 2: Generate prefabs (Projectiles → Pickups → Platforms → Player → Enemies)
3. Phase 3: Generate scenes (Video, Level, Death)
4. Phase 4: Addressables

New order:
1. Phase 1: Cleanup (ALL generated assets)
2. Phase 2: Generate button prefabs (Base, Level, Video)
3. Phase 3: Generate entity prefabs (Projectiles → Pickups → Platforms → Player → Enemies)
4. Phase 4: Generate gameplay scenes (Video, Level, Death)
5. Phase 5: Generate UI scenes (MainMenu, LevelSelection, VideoSelection, UpgradeShop)
6. Phase 6: Configure Addressables

---

## Phase 7: Cleanup

Delete:
- `Assets/Scripts/Tools/Editor/ConfigWiringTool.cs`
- All manual scenes (will be regenerated)
- All manual button prefabs (will be regenerated)

Keep:
- `MobileController.prefab` — complex reusable prefab (can be made config-driven later)
- `LoadingScreen.prefab` — simple prefab (reference in config)
- `Camera.prefab` (if exists) — or generate inline

---

## Execution Order

1. **Phase 1: PlayerPrefabGenerator fixes** — Add missing wiring (groundLayer, platformLayer, projectileSpawnPoint, InputActions)
2. **Phase 2: Config types** — Create all new config classes in SickOfIt.Data
3. **Phase 3: Button generators** — Create ButtonPrefabGenerator, LevelButtonPrefabGenerator, VideoButtonPrefabGenerator
4. **Phase 4: Scene generators** — Create MainMenuSceneGenerator, LevelSelectionSceneGenerator, VideoSelectionSceneGenerator, UpgradeShopSceneGenerator
5. **Phase 5: Update GameDefinition** — Add UI config fields
6. **Phase 6: Update ProjectGenerator** — Call all new generators in correct order
7. **Phase 7: Update GameDefinition.asset** — Populate config values from existing scenes
8. **Phase 8: Delete manual assets** — Remove old scenes, old button prefabs, ConfigWiringTool

---

## Files to Create

### Config Types (in Assets/Scripts/Data/Config/)
- [x] UISceneConfigs.cs (all config types in one file)

### Generators (in Assets/Scripts/Tools/Editor/Generation/)
- [x] UISceneGeneratorBase.cs (shared utilities)
- [x] MainMenuSceneGenerator.cs
- [x] LevelSelectionSceneGenerator.cs
- [x] VideoSelectionSceneGenerator.cs
- [x] UpgradeShopSceneGenerator.cs

### Updates
- [x] PlayerPrefabGenerator.cs — Added projectileSpawnPoint, groundLayer, platformLayer, InputActions
- [x] GameDefinition.cs — Added mainMenuConfig, levelSelectionConfig, videoSelectionConfig, upgradeShopConfig
- [x] ProjectGenerator.cs — Calls all 4 UI scene generators

### Deletes (completed)
- [x] ConfigWiringTool.cs — Deleted

---

## Risk Mitigation

1. **Backup current scenes** before deletion — can restore if generation fails
2. **Generate side-by-side first** — create new scenes with "_generated" suffix, verify, then replace
3. **Keep MobileController.prefab manual** — it's complex, can be made config-driven in a future pass
4. **Incremental testing** — regenerate and test after each phase
