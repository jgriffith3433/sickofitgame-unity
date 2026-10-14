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
    /// Generates LevelSelection.unity. The level cards are built at runtime by LevelSelectionManager from
    /// levelSelectionConfig.layout and ui/theme.json.
    /// </summary>
    public static class LevelSelectionSceneGenerator
    {
        const string OutputPath = "Assets/Scenes/LevelSelection.unity";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[LevelSelectionSceneGen] GameDefinition is null.");
                return;
            }

            var config = gd.levelSelectionConfig ?? new SelectionSceneConfig();
            var shell = MenuShell.Build(gd, config.baseConfig);
            if (shell == null)
                return;

            var manager = new GameObject("Level Selection Manager").AddComponent<LevelSelectionManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("sceneLoadingManager").objectReferenceValue = shell.Loading;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), OutputPath);
        }
    }
}
#endif
