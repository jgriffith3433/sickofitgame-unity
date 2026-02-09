using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for SceneLoadingManager
/// </summary>
[CustomEditor(typeof(SceneLoadingManager))]
public class SceneLoadingManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "How to use:\n\n" +
            "1. Add loading indicators to 'Objects To Activate On Load'\n" +
            "2. Add menu UI elements to 'Objects To Deactivate On Load'\n" +
            "3. Call SceneLoadingManager methods instead of SceneTransitionManager directly:\n" +
            "   - LoadLevel(int)\n" +
            "   - LoadVideo(int)\n" +
            "   - NewGame()\n" +
            "   - ContinueGame()\n\n" +
            "Or call SceneLoadingManager.ShowLoadingUI() before any SceneTransitionManager call.",
            MessageType.Info
        );
    }
}
