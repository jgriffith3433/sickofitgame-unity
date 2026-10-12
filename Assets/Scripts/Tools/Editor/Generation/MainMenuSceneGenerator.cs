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
    /// Generates MainMenu.unity from MainMenuConfig.
    /// </summary>
    public static class MainMenuSceneGenerator
    {
        private const string OUTPUT_PATH = "Assets/Scenes/MainMenu.unity";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[MainMenuSceneGen] GameDefinition is null!");
                return;
            }

            var config = gd.mainMenuConfig;

            // Create base scene infrastructure
            UISceneGeneratorBase.CreateBaseScene(OUTPUT_PATH, config.baseConfig, out var canvasGO, out var safeArea);

            // ─── Game Managers ────────────────────────────────────────────
            var managersGO = new GameObject("Game Managers");
            managersGO.AddComponent<GameProgressManager>();
            managersGO.AddComponent<SceneTransitionManager>();

            // ─── Roguelite Manager ────────────────────────────────────────
            var rogueliteGO = new GameObject("Roguelite Manager");
            rogueliteGO.AddComponent<RogueliteManager>();

            // ─── Scene Loading Manager ────────────────────────────────────
            var sceneLoadingGO = new GameObject("Scene Loading Manager");
            var sceneLoadingManager = sceneLoadingGO.AddComponent<SceneLoadingManager>();

            // ─── Main Menu Manager ────────────────────────────────────────
            var mainMenuManager = canvasGO.AddComponent<MainMenuManager>();

            // ─── Create button container ──────────────────────────────────
            var buttonContainer = UISceneGeneratorBase.CreateButtonContainer(safeArea, "MenuButtons", config.buttonSpacing);

            // ─── Create buttons ───────────────────────────────────────────
            GameObject continueButton = null, newGameButton = null, levelSelectButton = null;
            GameObject videosButton = null, upgradeShopButton = null, quitButton = null;
            TextMeshProUGUI continueButtonText = null;

            if (config.showContinueButton)
            {
                continueButton = UISceneGeneratorBase.CreateSimpleButton(buttonContainer, "ContinueButton", "Continue", config.buttonSize);
                var textChild = continueButton.transform.Find("Text");
                if (textChild != null)
                    continueButtonText = textChild.GetComponent<TextMeshProUGUI>();
            }

            if (config.showNewGameButton)
                newGameButton = UISceneGeneratorBase.CreateSimpleButton(buttonContainer, "NewGameButton", "New Game", config.buttonSize);

            if (config.showLevelSelectButton)
                levelSelectButton = UISceneGeneratorBase.CreateSimpleButton(buttonContainer, "LevelSelectButton", "Level Select", config.buttonSize);

            if (config.showVideosButton)
                videosButton = UISceneGeneratorBase.CreateSimpleButton(buttonContainer, "VideosButton", "Videos", config.buttonSize);

            if (config.showUpgradeShopButton)
                upgradeShopButton = UISceneGeneratorBase.CreateSimpleButton(buttonContainer, "UpgradeShopButton", "Upgrade Shop", config.buttonSize);

            if (config.showQuitButton)
                quitButton = UISceneGeneratorBase.CreateSimpleButton(buttonContainer, "QuitButton", "Quit", config.buttonSize);

            // ─── Roguelite UI texts ───────────────────────────────────────
            TextMeshProUGUI cumulativeHealthText = null, pointsText = null;

            if (config.showCumulativeHealth)
            {
                cumulativeHealthText = UISceneGeneratorBase.CreateText(
                    safeArea, "CumulativeHealthText", "♥: 0",
                    new Vector2(-800, 450), new Vector2(200, 50),
                    TextAlignmentOptions.Left, 32f);
            }

            if (config.showPointsDisplay)
            {
                pointsText = UISceneGeneratorBase.CreateText(
                    safeArea, "PointsText", "Points: 0",
                    new Vector2(-800, 400), new Vector2(200, 50),
                    TextAlignmentOptions.Left, 28f);
            }

            // ─── Transition objects ───────────────────────────────────────
            var idleObjects = new System.Collections.Generic.List<GameObject>();
            var transitionObjects = new System.Collections.Generic.List<GameObject>();

            // House idle object (if sprite provided)
            if (config.idleHouseSprite != null)
            {
                var houseGO = new GameObject("House");
                var sr = houseGO.AddComponent<SpriteRenderer>();
                sr.sprite = config.idleHouseSprite;
                SpriteLayers.Apply(sr, SpriteLayer.Foreground1);
                idleObjects.Add(houseGO);
            }

            // Explosion transition object (if sprite provided)
            if (config.explosionSprite != null)
            {
                var explodeGO = new GameObject("Explosion");
                var sr = explodeGO.AddComponent<SpriteRenderer>();
                sr.sprite = config.explosionSprite;
                SpriteLayers.Apply(sr, SpriteLayer.Foreground2);
                explodeGO.SetActive(false);
                transitionObjects.Add(explodeGO);
            }

            // ─── Wire MainMenuManager ─────────────────────────────────────
            var mmSo = new SerializedObject(mainMenuManager);

            if (continueButton != null)
                mmSo.FindProperty("continueButton").objectReferenceValue = continueButton.GetComponent<MobileControllerFramework.Button>();
            if (newGameButton != null)
                mmSo.FindProperty("newGameButton").objectReferenceValue = newGameButton.GetComponent<MobileControllerFramework.Button>();
            if (levelSelectButton != null)
                mmSo.FindProperty("levelSelectButton").objectReferenceValue = levelSelectButton.GetComponent<MobileControllerFramework.Button>();
            if (videosButton != null)
                mmSo.FindProperty("videosButton").objectReferenceValue = videosButton.GetComponent<MobileControllerFramework.Button>();
            if (upgradeShopButton != null)
                mmSo.FindProperty("upgradeShopButton").objectReferenceValue = upgradeShopButton.GetComponent<MobileControllerFramework.Button>();
            if (quitButton != null)
                mmSo.FindProperty("quitButton").objectReferenceValue = quitButton.GetComponent<MobileControllerFramework.Button>();

            if (continueButtonText != null)
                mmSo.FindProperty("continueButtonText").objectReferenceValue = continueButtonText;
            if (cumulativeHealthText != null)
                mmSo.FindProperty("cumulativeHealthText").objectReferenceValue = cumulativeHealthText;
            if (pointsText != null)
                mmSo.FindProperty("pointsText").objectReferenceValue = pointsText;

            mmSo.FindProperty("sceneLoadingManager").objectReferenceValue = sceneLoadingManager;
            mmSo.FindProperty("hideDelay").floatValue = config.hideDelay;
            mmSo.FindProperty("transitionDelay").floatValue = config.transitionDelay;

            // Wire object lists
            var hideListProp = mmSo.FindProperty("objectsToHideOnStart");
            hideListProp.ClearArray();
            foreach (var obj in idleObjects)
            {
                hideListProp.InsertArrayElementAtIndex(hideListProp.arraySize);
                hideListProp.GetArrayElementAtIndex(hideListProp.arraySize - 1).objectReferenceValue = obj;
            }

            var idleListProp = mmSo.FindProperty("idleObjects");
            idleListProp.ClearArray();
            foreach (var obj in idleObjects)
            {
                idleListProp.InsertArrayElementAtIndex(idleListProp.arraySize);
                idleListProp.GetArrayElementAtIndex(idleListProp.arraySize - 1).objectReferenceValue = obj;
            }

            var transListProp = mmSo.FindProperty("transitionObjects");
            transListProp.ClearArray();
            foreach (var obj in transitionObjects)
            {
                transListProp.InsertArrayElementAtIndex(transListProp.arraySize);
                transListProp.GetArrayElementAtIndex(transListProp.arraySize - 1).objectReferenceValue = obj;
            }

            mmSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── Save scene ───────────────────────────────────────────────
            UISceneGeneratorBase.SaveScene(OUTPUT_PATH);

            ////Debug.Log($"[MainMenuSceneGen] Created: {OUTPUT_PATH}");
        }
    }
}
#endif
