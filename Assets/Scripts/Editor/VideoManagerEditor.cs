using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for VideoManager to provide context menu tools
/// </summary>
[CustomEditor(typeof(VideoManager))]
public class VideoManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Scan for Video Player"))
        {
            ScanForVideoPlayer();
        }
    }
    
    /// <summary>
    /// Context menu item to scan for SpriteFramePlayer in the scene
    /// Right-click the VideoManager component in the Inspector to use this
    /// </summary>
    [ContextMenu("Scan for Video Player")]
    private void ScanForVideoPlayer()
    {
        VideoManager videoManager = (VideoManager)target;
        
        // Use SerializedObject for proper undo/redo support
        SerializedObject so = new SerializedObject(videoManager);
        
        // Find property
        SerializedProperty videoPlayerProperty = so.FindProperty("videoPlayer");
        
        // Scan for SpriteFramePlayer component in the scene
        SpriteFramePlayer foundPlayer = FindObjectOfType<SpriteFramePlayer>();
        
        if (foundPlayer != null)
        {
            videoPlayerProperty.objectReferenceValue = foundPlayer;
        }
        
        // Apply changes
        so.ApplyModifiedProperties();
        
        // Log results
        Debug.Log($"<b>VideoManager Scan Complete:</b>\n" +
                  $"- Found SpriteFramePlayer: {(foundPlayer != null ? foundPlayer.name : "No")}\n" +
                  $"Don't forget to add GameObjects to 'Objects To Activate/Deactivate After Loading' lists!");
        
        // Mark scene as dirty so Unity knows to save changes
        EditorUtility.SetDirty(videoManager);
    }
}
