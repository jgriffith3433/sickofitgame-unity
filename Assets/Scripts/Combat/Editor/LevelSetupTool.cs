using UnityEngine;
using UnityEditor;

/// <summary>
/// Quick tool to add spawners and setup combat in your levels
/// Window > Combat System > Level Setup Tool
/// </summary>
public class LevelSetupTool : EditorWindow
{
    private GameObject spawnerPrefab;
    private GameObject winTriggerPrefab;
    private int spawnerCount = 3;
    private float spread = 5f;
    
    [MenuItem("Window/Combat System/Level Setup Tool")]
    public static void ShowWindow()
    {
        GetWindow<LevelSetupTool>("Level Setup");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Level Combat Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox("This tool helps you quickly add combat elements to your level.", MessageType.Info);
        EditorGUILayout.Space();
        
        // Prefab references
        GUILayout.Label("Prefab References", EditorStyles.boldLabel);
        spawnerPrefab = (GameObject)EditorGUILayout.ObjectField("Spawner Prefab", spawnerPrefab, typeof(GameObject), false);
        winTriggerPrefab = (GameObject)EditorGUILayout.ObjectField("Win Trigger Prefab", winTriggerPrefab, typeof(GameObject), false);
        
        EditorGUILayout.Space();
        
        // Spawner settings
        GUILayout.Label("Spawner Setup", EditorStyles.boldLabel);
        spawnerCount = EditorGUILayout.IntSlider("Number of Spawners", spawnerCount, 1, 10);
        spread = EditorGUILayout.Slider("Spread Distance", spread, 1f, 20f);
        
        EditorGUILayout.Space();
        
        // Quick setup button
        if (GUILayout.Button("Auto-Setup Level", GUILayout.Height(40)))
        {
            AutoSetupLevel();
        }
        
        EditorGUILayout.Space();
        
        // Manual setup buttons
        GUILayout.Label("Manual Setup", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Add Player Health Component"))
        {
            AddPlayerHealthComponents();
        }
        
        if (GUILayout.Button("Add WinTriggerSpawner to Scene"))
        {
            AddWinTriggerSpawner();
        }
        
        if (GUILayout.Button("Create Spawner at (0,0,0)"))
        {
            CreateSpawnerAtOrigin();
        }
        
        EditorGUILayout.Space();
        
        // Instructions
        EditorGUILayout.HelpBox(
            "Quick Setup:\n" +
            "1. Load prefabs in the fields above\n" +
            "2. Click 'Auto-Setup Level' to add all combat elements\n\n" +
            "Manual Setup:\n" +
            "- Add Player Health Component: Adds health to player\n" +
            "- Add WinTriggerSpawner: Required for win condition\n" +
            "- Create Spawner: Adds a spawner at origin (move it manually)",
            MessageType.None
        );
    }
    
    private void AutoSetupLevel()
    {
        if (spawnerPrefab == null || winTriggerPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign spawner and win trigger prefabs first!", "OK");
            return;
        }
        
        // Add player health
        AddPlayerHealthComponents();
        
        // Add win trigger spawner
        GameObject winSpawner = AddWinTriggerSpawner();
        
        // Create spawners in a circle
        for (int i = 0; i < spawnerCount; i++)
        {
            float angle = (360f / spawnerCount) * i;
            float radian = angle * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                Mathf.Cos(radian) * spread,
                Mathf.Sin(radian) * spread,
                0
            );
            
            GameObject spawner = (GameObject)PrefabUtility.InstantiatePrefab(spawnerPrefab);
            spawner.transform.position = position;
            spawner.name = $"Spawner_{i + 1}";
            
            Undo.RegisterCreatedObjectUndo(spawner, "Create Spawner");
        }
        
        EditorUtility.DisplayDialog("Success", 
            $"Level setup complete!\n\n" +
            $"Added:\n" +
            $"- Player health components\n" +
            $"- {spawnerCount} spawners\n" +
            $"- Win trigger spawner\n\n" +
            $"Don't forget to set up Health UI in your Canvas!",
            "OK");
    }
    
    private void AddPlayerHealthComponents()
    {
        var allObjects = Object.FindObjectsOfType<GameObject>(true);
        GameObject player = System.Array.Find(allObjects, obj => obj.CompareTag("Player"));
        if (player == null)
        {
            EditorUtility.DisplayDialog("Error", "No GameObject with 'Player' tag found!", "OK");
            return;
        }
        
        // Add HealthSystem if not present
        if (player.GetComponent<HealthSystem>() == null)
        {
            Undo.AddComponent<HealthSystem>(player);
        }
        
        // Add PlayerHealth if not present
        if (player.GetComponent<PlayerHealth>() == null)
        {
            Undo.AddComponent<PlayerHealth>(player);
        }
        
        Debug.Log("Player health components added!");
    }
    
    private GameObject AddWinTriggerSpawner()
    {
        // Check if one already exists
        WinTriggerSpawner existing = FindFirstObjectByType<WinTriggerSpawner>();
        if (existing != null)
        {
            Debug.Log("WinTriggerSpawner already exists in scene.");
            return existing.gameObject;
        }
        
        GameObject spawnerObj = new GameObject("WinTriggerSpawner");
        WinTriggerSpawner spawner = spawnerObj.AddComponent<WinTriggerSpawner>();
        
        // Set win trigger prefab
        if (winTriggerPrefab != null)
        {
            SerializedObject so = new SerializedObject(spawner);
            so.FindProperty("winTriggerPrefab").objectReferenceValue = winTriggerPrefab;
            so.ApplyModifiedProperties();
        }
        
        Undo.RegisterCreatedObjectUndo(spawnerObj, "Create WinTriggerSpawner");
        Debug.Log("WinTriggerSpawner added to scene!");
        
        return spawnerObj;
    }
    
    private void CreateSpawnerAtOrigin()
    {
        if (spawnerPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a spawner prefab first!", "OK");
            return;
        }
        
        GameObject spawner = (GameObject)PrefabUtility.InstantiatePrefab(spawnerPrefab);
        spawner.transform.position = Vector3.zero;
        
        Undo.RegisterCreatedObjectUndo(spawner, "Create Spawner");
        Selection.activeGameObject = spawner;
        
        Debug.Log("Spawner created at origin. Move it to desired position!");
    }
}
