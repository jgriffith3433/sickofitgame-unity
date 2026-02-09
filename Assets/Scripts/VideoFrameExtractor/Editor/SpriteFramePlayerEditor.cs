#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SpriteFramePlayer))]
public class SpriteFramePlayerEditor : Editor
{
    private SpriteFramePlayer player;
    private SerializedProperty framesLabelProp;
    private SerializedProperty preloadOnAwakeProp;
    private SerializedProperty framesProp;
    private SerializedProperty isLoadedProp;

    private void OnEnable()
    {
        player = (SpriteFramePlayer)target;
        framesLabelProp = serializedObject.FindProperty("framesLabel");
        preloadOnAwakeProp = serializedObject.FindProperty("preloadOnAwake");
        framesProp = serializedObject.FindProperty("frames");
        isLoadedProp = serializedObject.FindProperty("isLoaded");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Addressables Info", EditorStyles.boldLabel);

        // Show loaded frame count
        int frameCount = framesProp.arraySize;
        bool isLoaded = isLoadedProp.boolValue;
        
        string status = isLoaded ? $"✓ Loaded: {frameCount} frames" : "Not loaded yet";
        MessageType messageType = isLoaded ? MessageType.Info : MessageType.Warning;
        
        EditorGUILayout.HelpBox(status, messageType);

        // Manual load button (if not preloading on awake)
        if (!Application.isPlaying && !preloadOnAwakeProp.boolValue)
        {
            EditorGUILayout.HelpBox("Preload On Awake is disabled. Frames will load on first Play() call.", MessageType.Info);
        }

        // Runtime controls
        if (Application.isPlaying)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Playback Controls (Runtime)", EditorStyles.boldLabel);

            // Manual load button if not loaded
            if (!isLoaded && GUILayout.Button("Load Frames Now", GUILayout.Height(30)))
            {
                player.LoadFrames();
            }

            if (isLoaded)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Play"))
                {
                    player.Play();
                }

                if (GUILayout.Button("Pause"))
                {
                    player.Pause();
                }

                if (GUILayout.Button("Stop"))
                {
                    player.Stop();
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                // Frame scrubber
                int totalFrames = player.GetTotalFrames();
                if (totalFrames > 0)
                {
                    int currentFrame = player.GetCurrentFrame();
                    int newFrame = EditorGUILayout.IntSlider("Scrub Frame", currentFrame, 0, totalFrames - 1);
                    
                    if (newFrame != currentFrame)
                    {
                        player.SetFrame(newFrame);
                    }

                    EditorGUILayout.LabelField($"Progress: {player.GetProgress() * 100:F1}%");
                    EditorGUILayout.LabelField($"Playing: {(player.IsPlaying() ? "Yes" : "No")}");
                }
            }
        }

        GUILayout.Space(10);

        // Updated help box for Addressables workflow
        EditorGUILayout.HelpBox(
            "ADDRESSABLES WORKFLOW:\n\n" +
            "1. Select your sprite frames in Project window\n" +
            "2. Check 'Addressable' on all frames\n" +
            "3. Assign them all the SAME label (e.g., 'Video1_Frames')\n" +
            "4. Set 'Frames Label' above to match that label\n" +
            "5. Build Addressables before testing\n\n" +
            "Frames will auto-load at runtime via Addressables!",
            MessageType.Info
        );
        
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
