using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using MobileControllerFramework;

/// <summary>
/// Custom editor for LevelManager to provide context menu tools
/// </summary>
[CustomEditor(typeof(LevelManager))]
public class LevelManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Scan for Loading Components"))
        {
            ScanForLoadingComponents();
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "LOADING SYSTEM:\n" +
            "• Initial Loading: For addressable assets at scene start\n" +
            "• Scene Transition: For loading UI when level completes\n\n" +
            "Add loading screens, player controls, and UI to the appropriate lists.",
            MessageType.Info
        );
    }
    
    /// <summary>
    /// Context menu item to scan for loading components in the scene
    /// Right-click the LevelManager component in the Inspector to use this
    /// </summary>
    [ContextMenu("Scan for Loading Components")]
    private void ScanForLoadingComponents()
    {
        LevelManager levelManager = (LevelManager)target;
        
        // Use SerializedObject for proper undo/redo support
        SerializedObject so = new SerializedObject(levelManager);
        
        // Find properties
        SerializedProperty backgroundPlayersProperty = so.FindProperty("backgroundPlayers");
        SerializedProperty musicCyclerProperty = so.FindProperty("musicCycler");
        
        // Scan for SpriteFramePlayer components in the scene
        SpriteFramePlayer[] foundPlayers = FindObjectsOfType<SpriteFramePlayer>();
        
        // Clear the list and add found components
        backgroundPlayersProperty.ClearArray();
        
        int playerCount = 0;
        foreach (var player in foundPlayers)
        {
            backgroundPlayersProperty.InsertArrayElementAtIndex(playerCount);
            backgroundPlayersProperty.GetArrayElementAtIndex(playerCount).objectReferenceValue = player;
            playerCount++;
        }
        
        // Scan for MusicCycler component in the scene
        MusicCycler foundMusicCycler = FindObjectOfType<MusicCycler>();
        if (foundMusicCycler != null)
        {
            musicCyclerProperty.objectReferenceValue = foundMusicCycler;
        }
        
        // Apply changes
        so.ApplyModifiedProperties();
        
        // Log results
        Debug.Log($"<b>LevelManager Scan Complete:</b>\n" +
                  $"- Found {playerCount} SpriteFramePlayer component(s)\n" +
                  $"- Found MusicCycler: {(foundMusicCycler != null ? "Yes" : "No")}\n" +
                  $"Don't forget to add GameObjects to 'Objects To Activate After Loading' list!");
        
        // Mark scene as dirty so Unity knows to save changes
        EditorUtility.SetDirty(levelManager);
    }
}
