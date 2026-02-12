using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool to quickly set up the combat system
/// Window > Combat System > Setup Wizard
/// </summary>
public class CombatSystemSetup : EditorWindow
{
    private int playerMaxHealth = 3;
    private int chaserHealth = 2;
    private int shooterHealth = 3;
    private int spawnerHealth = 5;
    
    [MenuItem("Window/Combat System/Setup Wizard")]
    public static void ShowWindow()
    {
        GetWindow<CombatSystemSetup>("Combat Setup");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Combat System Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox("This wizard will help you set up the combat system for your game.", MessageType.Info);
        EditorGUILayout.Space();
        
        // Health settings
        GUILayout.Label("Health Settings", EditorStyles.boldLabel);
        playerMaxHealth = EditorGUILayout.IntField("Player Max Health", playerMaxHealth);
        chaserHealth = EditorGUILayout.IntField("Chaser Enemy Health", chaserHealth);
        shooterHealth = EditorGUILayout.IntField("Shooter Enemy Health", shooterHealth);
        spawnerHealth = EditorGUILayout.IntField("Spawner Health", spawnerHealth);
        
        EditorGUILayout.Space();
        
        // Create prefabs button
        if (GUILayout.Button("Create All Prefabs", GUILayout.Height(40)))
        {
            CreateAllPrefabs();
        }
        
        EditorGUILayout.Space();
        
        // Instructions
        GUILayout.Label("Instructions:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Click 'Create All Prefabs' to generate:\n" +
            "   - Enemy Chaser\n" +
            "   - Enemy Shooter\n" +
            "   - Enemy Spawner\n" +
            "   - Win Trigger\n" +
            "   - Health UI\n\n" +
            "2. Use the 'Level Setup Tool' to add spawners to your levels\n\n" +
            "3. Add WinTriggerSpawner component to a GameObject in your scene\n\n" +
            "4. Add HealthUI prefab to your Canvas",
            MessageType.None
        );
    }
    
    private void CreateAllPrefabs()
    {
        // Create prefabs folder if it doesn't exist
        string prefabPath = "Assets/Prefabs/Combat";
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            Directory.CreateDirectory(Application.dataPath + "/Prefabs/Combat");
            AssetDatabase.Refresh();
        }
        
        // Create enemy chaser
        CreateEnemyChaserPrefab(prefabPath);
        
        // Create enemy shooter
        CreateEnemyShooterPrefab(prefabPath);
        
        // Create spawner
        CreateSpawnerPrefab(prefabPath);
        
        // Create win trigger
        CreateWinTriggerPrefab(prefabPath);
        
        // Create health UI
        CreateHealthUIPrefab(prefabPath);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Success", "All combat prefabs created in " + prefabPath, "OK");
    }
    
    private void CreateEnemyChaserPrefab(string path)
    {
        GameObject chaser = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chaser.name = "Enemy_Chaser";
        chaser.tag = "Enemy";
        chaser.layer = LayerMask.NameToLayer("Default");
        
        // Make it 2D
        DestroyImmediate(chaser.GetComponent<BoxCollider>());
        BoxCollider2D collider = chaser.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.8f, 0.8f);
        
        // Add Rigidbody2D
        Rigidbody2D rb = chaser.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        
        // Add sprite renderer (change material to unlit)
        MeshRenderer renderer = chaser.GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.red;
        
        // Add health system
        HealthSystem health = chaser.AddComponent<HealthSystem>();
        health.GetType().GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(health, chaserHealth);
        health.GetType().GetField("destroyOnDeath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(health, true);
        
        // Add chaser AI
        chaser.AddComponent<EnemyChaser>();
        
        // Add glitch effect
        GlitchEffect glitch = chaser.AddComponent<GlitchEffect>();
        
        // Add damage dealer
        DamageDealer damageDealer = chaser.AddComponent<DamageDealer>();
        
        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(chaser, path + "/Enemy_Chaser.prefab");
        DestroyImmediate(chaser);
    }
    
    private void CreateEnemyShooterPrefab(string path)
    {
        GameObject shooter = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shooter.name = "Enemy_Shooter";
        shooter.tag = "Enemy";
        
        // Make it 2D
        DestroyImmediate(shooter.GetComponent<SphereCollider>());
        CircleCollider2D collider = shooter.AddComponent<CircleCollider2D>();
        collider.radius = 0.4f;
        
        // Add Rigidbody2D
        Rigidbody2D rb = shooter.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        
        // Change material
        MeshRenderer renderer = shooter.GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.cyan;
        
        // Add health system
        HealthSystem health = shooter.AddComponent<HealthSystem>();
        health.GetType().GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(health, shooterHealth);
        
        // Add shooter AI
        shooter.AddComponent<EnemyShooter>();
        
        // Add glitch effect
        shooter.AddComponent<GlitchEffect>();
        
        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(shooter, path + "/Enemy_Shooter.prefab");
        DestroyImmediate(shooter);
    }
    
    private void CreateSpawnerPrefab(string path)
    {
        GameObject spawner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spawner.name = "Enemy_Spawner";
        spawner.tag = "Enemy";
        
        // Scale it up
        spawner.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        
        // Make it 2D
        DestroyImmediate(spawner.GetComponent<BoxCollider>());
        BoxCollider2D collider = spawner.AddComponent<BoxCollider2D>();
        
        // Change material
        MeshRenderer renderer = spawner.GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.yellow;
        
        // Add health system
        HealthSystem health = spawner.AddComponent<HealthSystem>();
        health.GetType().GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(health, spawnerHealth);
        
        // Add spawner component
        spawner.AddComponent<EnemySpawner>();
        
        // Add glitch effect
        GlitchEffect glitch = spawner.AddComponent<GlitchEffect>();
        
        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(spawner, path + "/Enemy_Spawner.prefab");
        DestroyImmediate(spawner);
    }
    
    private void CreateWinTriggerPrefab(string path)
    {
        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = "WinTrigger";
        trigger.tag = "Finish";
        
        // Make it 2D and a trigger
        DestroyImmediate(trigger.GetComponent<BoxCollider>());
        BoxCollider2D collider = trigger.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        
        // Change material
        MeshRenderer renderer = trigger.GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = Color.green;
        
        // Add win trigger component
        LevelWinTrigger winScript = trigger.AddComponent<LevelWinTrigger>();
        
        // Add glitch effect
        GlitchEffect glitch = trigger.AddComponent<GlitchEffect>();
        
        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(trigger, path + "/WinTrigger.prefab");
        DestroyImmediate(trigger);
    }
    
    private void CreateHealthUIPrefab(string path)
    {
        // Create UI setup (this would need a Canvas in the scene)
        GameObject healthUI = new GameObject("HealthUI");
        
        // Add HealthUI component
        healthUI.AddComponent<HealthUI>();
        
        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(healthUI, path + "/HealthUI.prefab");
        DestroyImmediate(healthUI);
        
        Debug.Log("HealthUI prefab created. You'll need to manually set it up in a Canvas.");
    }
}
