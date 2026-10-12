#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;
using SickOfIt.Core.Flow;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates VideoSelection.unity from SelectionSceneConfig.
    /// </summary>
    public static class VideoSelectionSceneGenerator
    {
        private const string OUTPUT_PATH = "Assets/Scenes/VideoSelection.unity";
        private const string VIDEO_BUTTON_PREFAB_PATH = "Assets/Prefabs/UI/ButtonVideo.prefab";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[VideoSelectionSceneGen] GameDefinition is null!");
                return;
            }

            var config = gd.videoSelectionConfig;

            // Create base scene infrastructure
            UISceneGeneratorBase.CreateBaseScene(OUTPUT_PATH, config.baseConfig, out var canvasGO, out var safeArea);

            // ─── Scene Loading Manager ────────────────────────────────────
            var sceneLoadingGO = new GameObject("Scene Loading Manager");
            var sceneLoadingManager = sceneLoadingGO.AddComponent<SceneLoadingManager>();

            // ─── Video Selection Manager ──────────────────────────────────
            var managerGO = new GameObject("Video Selection Manager");
            var videoSelectionManager = managerGO.AddComponent<VideoSelectionManager>();

            // ─── Create button container ──────────────────────────────────
            var buttonContainer = UISceneGeneratorBase.CreateButtonContainer(safeArea, "VideoButtonContainer", config.buttonSpacing);

            // ─── Create back button ───────────────────────────────────────
            var backButton = UISceneGeneratorBase.CreateSimpleButton(safeArea, "BackButton", "Back", new Vector2(150, 60));
            var backRect = backButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(100, -50);

            // ─── Load or create video button prefab ───────────────────────
            var videoButtonPrefab = config.itemButtonPrefab;
            if (videoButtonPrefab == null)
                videoButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VIDEO_BUTTON_PREFAB_PATH);

            // ─── Wire VideoSelectionManager ───────────────────────────────
            var vsmSo = new SerializedObject(videoSelectionManager);
            vsmSo.FindProperty("videoButtonContainer").objectReferenceValue = buttonContainer;
            vsmSo.FindProperty("videoButtonPrefab").objectReferenceValue = videoButtonPrefab;
            vsmSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<MobileControllerFramework.Button>();
            vsmSo.FindProperty("sceneLoadingManager").objectReferenceValue = sceneLoadingManager;
            vsmSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── Save scene ───────────────────────────────────────────────
            UISceneGeneratorBase.SaveScene(OUTPUT_PATH);

            ////Debug.Log($"[VideoSelectionSceneGen] Created: {OUTPUT_PATH}");
        }
    }
}
#endif
