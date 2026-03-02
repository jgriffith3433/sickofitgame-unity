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
    /// Generates LevelSelection.unity from SelectionSceneConfig.
    /// </summary>
    public static class LevelSelectionSceneGenerator
    {
        private const string OUTPUT_PATH = "Assets/Scenes/LevelSelection.unity";
        private const string LEVEL_BUTTON_PREFAB_PATH = "Assets/Prefabs/UI/ButtonLevel.prefab";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[LevelSelectionSceneGen] GameDefinition is null!");
                return;
            }

            var config = gd.levelSelectionConfig;

            // Create base scene infrastructure
            UISceneGeneratorBase.CreateBaseScene(OUTPUT_PATH, config.baseConfig, out var canvasGO, out var safeArea);

            // ─── Scene Loading Manager ────────────────────────────────────
            var sceneLoadingGO = new GameObject("Scene Loading Manager");
            var sceneLoadingManager = sceneLoadingGO.AddComponent<SceneLoadingManager>();

            // ─── Level Selection Manager ──────────────────────────────────
            var managerGO = new GameObject("Level Selection Manager");
            var levelSelectionManager = managerGO.AddComponent<LevelSelectionManager>();

            // ─── Create button container ──────────────────────────────────
            var buttonContainer = UISceneGeneratorBase.CreateButtonContainer(safeArea, "LevelButtonContainer", config.buttonSpacing);

            // ─── Create back button ───────────────────────────────────────
            var backButton = UISceneGeneratorBase.CreateSimpleButton(safeArea, "BackButton", "Back", new Vector2(150, 60));
            var backRect = backButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(100, -50);

            // ─── Cumulative health text ───────────────────────────────────
            TextMeshProUGUI cumulativeHealthText = null;
            if (config.showCumulativeHealth)
            {
                cumulativeHealthText = UISceneGeneratorBase.CreateText(
                    safeArea, "CumulativeHealthText", "♥: 0",
                    new Vector2(-800, 450), new Vector2(200, 50),
                    TextAlignmentOptions.Left, 32f);
            }

            // ─── Load or create level button prefab ───────────────────────
            var levelButtonPrefab = config.itemButtonPrefab;
            if (levelButtonPrefab == null)
                levelButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LEVEL_BUTTON_PREFAB_PATH);

            // ─── Wire LevelSelectionManager ───────────────────────────────
            var lsmSo = new SerializedObject(levelSelectionManager);
            lsmSo.FindProperty("levelButtonContainer").objectReferenceValue = buttonContainer;
            lsmSo.FindProperty("levelButtonPrefab").objectReferenceValue = levelButtonPrefab;
            lsmSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<MobileControllerFramework.Button>();
            if (cumulativeHealthText != null)
                lsmSo.FindProperty("cumulativeHealthText").objectReferenceValue = cumulativeHealthText;
            lsmSo.FindProperty("sceneLoadingManager").objectReferenceValue = sceneLoadingManager;
            lsmSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── Save scene ───────────────────────────────────────────────
            UISceneGeneratorBase.SaveScene(OUTPUT_PATH);

            Debug.Log($"[LevelSelectionSceneGen] Created: {OUTPUT_PATH}");
        }
    }
}
#endif
