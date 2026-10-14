#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates VideoSelection.unity. The video cards are built at runtime by VideoSelectionManager from
    /// videoSelectionConfig.layout and ui/theme.json.
    /// </summary>
    public static class VideoSelectionSceneGenerator
    {
        const string OutputPath = "Assets/Scenes/VideoSelection.unity";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[VideoSelectionSceneGen] GameDefinition is null.");
                return;
            }

            var config = gd.videoSelectionConfig ?? new SelectionSceneConfig();
            var shell = MenuShell.Build(gd, config.baseConfig);
            if (shell == null)
                return;

            var manager = new GameObject("Video Selection Manager").AddComponent<VideoSelectionManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("sceneLoadingManager").objectReferenceValue = shell.Loading;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), OutputPath);
        }
    }
}
#endif
