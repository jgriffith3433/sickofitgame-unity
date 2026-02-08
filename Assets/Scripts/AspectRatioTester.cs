using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Editor tool to test different aspect ratios in the Game view.
/// Add this to any GameObject in your scene to access the test buttons.
/// </summary>
[ExecuteInEditMode]
public class AspectRatioTester : MonoBehaviour
{
    [Header("Common Aspect Ratios to Test")]
    [Tooltip("Click these buttons in the Inspector to test different aspect ratios")]
    public bool testCurrentResolution;

    // This provides an easy way to test in the inspector
    [System.Serializable]
    public class AspectRatioPreset
    {
        public string name;
        public int width;
        public int height;

        public AspectRatioPreset(string name, int width, int height)
        {
            this.name = name;
            this.width = width;
            this.height = height;
        }
    }

    private void OnValidate()
    {
        if (testCurrentResolution && Application.isPlaying)
        {
            Debug.Log($"Current Resolution: {Screen.width}x{Screen.height} (Aspect: {(float)Screen.width / Screen.height:F2})");
            testCurrentResolution = false;
        }
    }
}

[CustomEditor(typeof(AspectRatioTester))]
public class AspectRatioTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AspectRatioTester tester = (AspectRatioTester)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Test Aspect Ratios", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Set Game View to 'Free Aspect' first, then use these buttons to test different resolutions.", MessageType.Info);

        EditorGUILayout.Space();

        // Desktop/Tablet ratios
        EditorGUILayout.LabelField("Desktop & Tablets:", EditorStyles.boldLabel);
        if (GUILayout.Button("16:9 - 1920x1080 (Target)"))
            SetResolution(1920, 1080);
        if (GUILayout.Button("16:10 - 1920x1200"))
            SetResolution(1920, 1200);
        if (GUILayout.Button("4:3 - 1024x768"))
            SetResolution(1024, 768);

        EditorGUILayout.Space();

        // Modern phone ratios
        EditorGUILayout.LabelField("Modern Smartphones:", EditorStyles.boldLabel);
        if (GUILayout.Button("19.5:9 - 1080x2340 (iPhone 12/13)"))
            SetResolution(1080, 2340);
        if (GUILayout.Button("20:9 - 1080x2400 (Samsung S21)"))
            SetResolution(1080, 2400);
        if (GUILayout.Button("19:9 - 1080x2160 (Pixel 5)"))
            SetResolution(1080, 2160);
        if (GUILayout.Button("18:9 - 1080x2160"))
            SetResolution(1080, 2160);

        EditorGUILayout.Space();

        // Older phones
        EditorGUILayout.LabelField("Older Devices:", EditorStyles.boldLabel);
        if (GUILayout.Button("16:9 - 1080x1920 (Standard Phone)"))
            SetResolution(1080, 1920);
        if (GUILayout.Button("16:9 - 750x1334 (iPhone 8)"))
            SetResolution(750, 1334);

        EditorGUILayout.Space();

        // Extreme cases
        EditorGUILayout.LabelField("Extreme Cases:", EditorStyles.boldLabel);
        if (GUILayout.Button("21:9 - 1080x2520 (Ultra Wide)"))
            SetResolution(1080, 2520);
        if (GUILayout.Button("1:1 - 1080x1080 (Square)"))
            SetResolution(1080, 1080);

        EditorGUILayout.Space();

        if (GUILayout.Button("Show Current Resolution", GUILayout.Height(30)))
        {
            var gameView = GetGameView();
            if (gameView != null)
            {
                var resolution = GetGameViewSize(gameView);
                float aspect = (float)resolution.x / resolution.y;
                Debug.Log($"<color=cyan>Current Game View: {resolution.x}x{resolution.y} (Aspect Ratio: {aspect:F2}:1)</color>");
                EditorUtility.DisplayDialog("Current Resolution", 
                    $"Width: {resolution.x}\nHeight: {resolution.y}\nAspect Ratio: {aspect:F2}:1", "OK");
            }
        }
    }

    private void SetResolution(int width, int height)
    {
        var gameView = GetGameView();
        if (gameView != null)
        {
            var sizeType = gameView.GetType();
            
            // Try to set Free Aspect first
            var currentSizeProp = sizeType.GetProperty("currentGameViewSize", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            
            // Set the size
            SetGameViewSize(gameView, width, height);
            
            float aspect = (float)width / height;
            Debug.Log($"<color=green>Game View set to: {width}x{height} (Aspect Ratio: {aspect:F2}:1)</color>");
        }
        else
        {
            Debug.LogWarning("Could not find Game View. Make sure the Game View is visible.");
        }
    }

    private EditorWindow GetGameView()
    {
        System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType != null)
        {
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            return gameView;
        }
        return null;
    }

    private void SetGameViewSize(EditorWindow gameView, int width, int height)
    {
        var sizeType = gameView.GetType();
        var areaField = sizeType.GetField("m_SizeType",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (areaField != null)
        {
            // 0 = Free Aspect, 1 = Standalone, etc.
            areaField.SetValue(gameView, 0); // Set to Free Aspect
        }

        // Set the actual size
        var targetSizeField = sizeType.GetField("m_TargetSize",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (targetSizeField != null)
        {
            targetSizeField.SetValue(gameView, new Vector2(width, height));
        }

        gameView.Repaint();
    }

    private Vector2Int GetGameViewSize(EditorWindow gameView)
    {
        var sizeType = gameView.GetType();
        var targetSizeField = sizeType.GetField("m_TargetSize",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (targetSizeField != null)
        {
            Vector2 size = (Vector2)targetSizeField.GetValue(gameView);
            return new Vector2Int((int)size.x, (int)size.y);
        }

        return new Vector2Int(Screen.width, Screen.height);
    }
}
#endif
