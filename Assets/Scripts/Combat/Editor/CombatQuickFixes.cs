using UnityEngine;
using UnityEditor;

/// <summary>
/// Quick tool to fix common combat system issues
/// Window > Combat System > Quick Fixes
/// </summary>
public class CombatQuickFixes : EditorWindow
{
    [MenuItem("Window/Combat System/Quick Fixes")]
    public static void ShowWindow()
    {
        GetWindow<CombatQuickFixes>("Combat Fixes");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Combat System Quick Fixes", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox("Use these tools to quickly fix common combat setup issues.", MessageType.Info);
        EditorGUILayout.Space();
        
        // Tag fixes
        GUILayout.Label("Tag Setup", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Create Required Tags"))
        {
            CreateRequiredTags();
        }
        
        EditorGUILayout.Space();
        
        // Player projectile fix
        GUILayout.Label("Player Projectiles", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Add DamageDealer to Selected Prefab"))
        {
            AddDamageDealerToSelection();
        }
        
        EditorGUILayout.HelpBox(
            "Select your player projectile prefab, then click the button above to add DamageDealer component.",
            MessageType.None
        );
        
        EditorGUILayout.Space();
        
        // Enemy projectile setup
        GUILayout.Label("Enemy Projectiles", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Create Enemy Projectile Prefab"))
        {
            CreateEnemyProjectilePrefab();
        }
        
        EditorGUILayout.HelpBox(
            "Creates a simple enemy projectile that damages the player. Make it addressable with key 'Projectile_Enemy'.",
            MessageType.None
        );
        
        EditorGUILayout.Space();
        
        // Layer setup
        GUILayout.Label("Physics Setup", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Setup Collision Layers"))
        {
            SetupCollisionLayers();
        }
        
        EditorGUILayout.HelpBox(
            "Configures physics layers so player projectiles don't hit player and enemy projectiles don't hit enemies.",
            MessageType.None
        );
    }
    
    private void CreateRequiredTags()
    {
        AddTag("Enemy");
        AddTag("Finish");
        
        EditorUtility.DisplayDialog("Success", "Required tags created!\n\n- Enemy\n- Finish", "OK");
    }
    
    private void AddTag(string tag)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        
        // Check if tag already exists
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals(tag))
            {
                return; // Tag already exists
            }
        }
        
        // Add tag
        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
        newTag.stringValue = tag;
        
        tagManager.ApplyModifiedProperties();
    }
    
    private void AddDamageDealerToSelection()
    {
        GameObject selected = Selection.activeGameObject;
        
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a GameObject first!", "OK");
            return;
        }
        
        // Check if it's a prefab
        bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(selected) || PrefabUtility.IsPartOfPrefabInstance(selected);
        
        GameObject target = selected;
        
        // If it's a prefab instance, get the prefab
        if (PrefabUtility.IsPartOfPrefabInstance(selected))
        {
            target = PrefabUtility.GetCorrespondingObjectFromSource(selected);
        }
        
        // Add DamageDealer if not present
        DamageDealer damageDealer = target.GetComponent<DamageDealer>();
        if (damageDealer == null)
        {
            damageDealer = target.AddComponent<DamageDealer>();
            
            // Configure for player projectile
            SerializedObject so = new SerializedObject(damageDealer);
            so.FindProperty("damageAmount").intValue = 1;
            so.FindProperty("destroyOnHit").boolValue = true;
            
            SerializedProperty targetTags = so.FindProperty("targetTags");
            targetTags.ClearArray();
            targetTags.InsertArrayElementAtIndex(0);
            targetTags.GetArrayElementAtIndex(0).stringValue = "Enemy";
            
            so.ApplyModifiedProperties();
            
            // Save prefab if it's a prefab
            if (isPrefab)
            {
                PrefabUtility.SavePrefabAsset(target);
            }
            
            EditorUtility.DisplayDialog("Success", "DamageDealer added to " + target.name + "!\n\nConfigured to damage enemies.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Already Exists", target.name + " already has a DamageDealer component.", "OK");
        }
    }
    
    private void CreateEnemyProjectilePrefab()
    {
        string path = "Assets/Prefabs/Combat";
        
        // Create a simple sphere projectile
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Projectile_Enemy";
        projectile.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        
        // Make it 2D
        DestroyImmediate(projectile.GetComponent<SphereCollider>());
        CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.15f;
        
        // Add Rigidbody2D
        Rigidbody2D rb = projectile.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        
        // Change material
        MeshRenderer renderer = projectile.GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.red;
        
        // Add DamageDealer
        DamageDealer damageDealer = projectile.AddComponent<DamageDealer>();
        SerializedObject so = new SerializedObject(damageDealer);
        so.FindProperty("damageAmount").intValue = 1;
        so.FindProperty("destroyOnHit").boolValue = true;
        
        SerializedProperty targetTags = so.FindProperty("targetTags");
        targetTags.ClearArray();
        targetTags.InsertArrayElementAtIndex(0);
        targetTags.GetArrayElementAtIndex(0).stringValue = "Player";
        
        so.ApplyModifiedProperties();
        
        // Add glitch effect
        projectile.AddComponent<GlitchEffect>();
        
        // Save prefab
        string prefabPath = path + "/Projectile_Enemy.prefab";
        PrefabUtility.SaveAsPrefabAsset(projectile, prefabPath);
        DestroyImmediate(projectile);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Success", 
            "Enemy projectile created at:\n" + prefabPath + "\n\n" +
            "IMPORTANT: Make this prefab Addressable!\n" +
            "1. Select the prefab\n" +
            "2. Check 'Addressable' in inspector\n" +
            "3. Set address to: Projectile_Enemy",
            "OK");
        
        // Select the created prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }
    
    private void SetupCollisionLayers()
    {
        EditorUtility.DisplayDialog("Manual Setup Required", 
            "To prevent friendly fire:\n\n" +
            "1. Go to Edit > Project Settings > Physics 2D\n" +
            "2. In the Layer Collision Matrix:\n" +
            "   - Uncheck Player × Player Projectiles\n" +
            "   - Uncheck Enemy × Enemy Projectiles\n\n" +
            "Or create custom layers:\n" +
            "- Player layer\n" +
            "- Enemy layer\n" +
            "- PlayerProjectile layer\n" +
            "- EnemyProjectile layer",
            "OK");
    }
}
