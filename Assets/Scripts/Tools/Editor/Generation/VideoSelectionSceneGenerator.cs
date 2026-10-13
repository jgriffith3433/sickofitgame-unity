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
    /// Generates VideoSelection.unity from SelectionSceneConfig.
    /// </summary>
    public static class VideoSelectionSceneGenerator
    {
        const string OutputPath = "Assets/Scenes/VideoSelection.unity";
        const string VideoButtonPath = "Assets/Prefabs/(Prb)ButtonVideo.prefab";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[VideoSelectionSceneGen] GameDefinition is null.");
                return;
            }

            var config = gd.videoSelectionConfig ?? new SelectionSceneConfig();
            var shell = MenuShell.Build(
                gd,
                config.baseConfig,
                "VideoSelectPanel",
                string.IsNullOrEmpty(config.title) ? "Select Video" : config.title,
                "VideoButtonContainer",
                config.buttonSpacing,
                config.buttonSize);
            if (shell == null)
                return;

            var backPrefab = config.backButtonPrefab != null
                ? config.backButtonPrefab
                : gd.mainMenuConfig != null ? gd.mainMenuConfig.buttonPrefab : null;
            var back = MenuShell.AddOvalButton(shell.SafeArea, backPrefab, "(Prb)ButtonBack", "Back", config.buttonSize);
            MenuShell.PlaceCornerButton(back, Vector2.zero, Vector2.zero, new Vector2(60f, 50f));

            var videoButton = config.itemButtonPrefab != null
                ? config.itemButtonPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(VideoButtonPath);

            var manager = new GameObject("Video Selection Manager").AddComponent<VideoSelectionManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("videoButtonContainer").objectReferenceValue = shell.Container;
            so.FindProperty("videoButtonPrefab").objectReferenceValue = videoButton;
            if (back != null)
                so.FindProperty("backButton").objectReferenceValue = back.GetComponent<MobileControllerFramework.Button>();
            so.FindProperty("sceneLoadingManager").objectReferenceValue = shell.Loading;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), OutputPath);
        }
    }
}
#endif
