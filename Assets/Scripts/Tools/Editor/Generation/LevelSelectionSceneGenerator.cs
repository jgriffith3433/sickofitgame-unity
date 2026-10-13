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
    /// Generates LevelSelection.unity from SelectionSceneConfig.
    /// </summary>
    public static class LevelSelectionSceneGenerator
    {
        const string OutputPath = "Assets/Scenes/LevelSelection.unity";
        const string LevelButtonPath = "Assets/Prefabs/(Prb)ButtonLevel.prefab";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[LevelSelectionSceneGen] GameDefinition is null.");
                return;
            }

            var config = gd.levelSelectionConfig ?? new SelectionSceneConfig();
            var shell = MenuShell.Build(
                gd,
                config.baseConfig,
                "LevelSelectPanel",
                string.IsNullOrEmpty(config.title) ? "Select Level" : config.title,
                "LevelButtonContainer",
                config.buttonSpacing,
                config.buttonSize);
            if (shell == null)
                return;

            var backPrefab = config.backButtonPrefab != null
                ? config.backButtonPrefab
                : gd.mainMenuConfig != null ? gd.mainMenuConfig.buttonPrefab : null;
            var back = MenuShell.AddOvalButton(shell.SafeArea, backPrefab, "(Prb)ButtonBack", "Back", config.buttonSize);
            MenuShell.PlaceCornerButton(back, Vector2.zero, Vector2.zero, new Vector2(60f, 50f));

            var levelButton = config.itemButtonPrefab != null
                ? config.itemButtonPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(LevelButtonPath);

            var manager = new GameObject("Level Selection Manager").AddComponent<LevelSelectionManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("levelButtonContainer").objectReferenceValue = shell.Container;
            so.FindProperty("levelButtonPrefab").objectReferenceValue = levelButton;
            if (back != null)
                so.FindProperty("backButton").objectReferenceValue = back.GetComponent<MobileControllerFramework.Button>();
            if (config.showCumulativeHealth && shell.Health != null)
                so.FindProperty("cumulativeHealthText").objectReferenceValue = shell.Health;
            so.FindProperty("sceneLoadingManager").objectReferenceValue = shell.Loading;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), OutputPath);
        }
    }
}
#endif
