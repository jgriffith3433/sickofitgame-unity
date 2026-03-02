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
    /// Generates UpgradeShop.unity from UpgradeShopConfig.
    /// </summary>
    public static class UpgradeShopSceneGenerator
    {
        private const string OUTPUT_PATH = "Assets/Scenes/UpgradeShop.unity";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[UpgradeShopSceneGen] GameDefinition is null!");
                return;
            }

            var config = gd.upgradeShopConfig;

            // Create base scene infrastructure
            UISceneGeneratorBase.CreateBaseScene(OUTPUT_PATH, config.baseConfig, out var canvasGO, out var safeArea);

            // ─── Upgrade Shop Manager ─────────────────────────────────────
            var managerGO = new GameObject("Upgrade Shop Manager");
            var upgradeShopManager = managerGO.AddComponent<UpgradeShopManager>();

            // ─── Points text ──────────────────────────────────────────────
            var pointsText = UISceneGeneratorBase.CreateText(
                safeArea, "PointsText", "Points: 0",
                new Vector2(0, 400), new Vector2(300, 50),
                TextAlignmentOptions.Center, 36f);

            // ─── Cumulative health text ───────────────────────────────────
            var cumulativeHealthText = UISceneGeneratorBase.CreateText(
                safeArea, "CumulativeHealthText", "♥: 0",
                new Vector2(-800, 450), new Vector2(200, 50),
                TextAlignmentOptions.Left, 32f);

            // ─── Shop container ───────────────────────────────────────────
            var shopContainer = UISceneGeneratorBase.CreateButtonContainer(safeArea, "ShopContainer", 20f);

            // ─── Create upgrade UI for each upgrade definition ────────────
            TextMeshProUGUI extraJumpTitleText = null;
            TextMeshProUGUI extraJumpDescriptionText = null;
            TextMeshProUGUI extraJumpStatusText = null;
            GameObject buyExtraJumpButton = null;

            foreach (var upgrade in config.upgrades)
            {
                // Title
                var titleText = UISceneGeneratorBase.CreateText(
                    shopContainer, $"{upgrade.id}TitleText", upgrade.title,
                    Vector2.zero, new Vector2(300, 40),
                    TextAlignmentOptions.Center, 28f);

                // Description
                var descText = UISceneGeneratorBase.CreateText(
                    shopContainer, $"{upgrade.id}DescriptionText", upgrade.description,
                    Vector2.zero, new Vector2(300, 30),
                    TextAlignmentOptions.Center, 18f);

                // Status
                var statusText = UISceneGeneratorBase.CreateText(
                    shopContainer, $"{upgrade.id}StatusText", "Level: 0 / " + upgrade.maxLevel,
                    Vector2.zero, new Vector2(300, 30),
                    TextAlignmentOptions.Center, 18f);

                // Buy button
                var buyButton = UISceneGeneratorBase.CreateSimpleButton(
                    shopContainer, $"Buy{upgrade.id}Button", $"Buy ({upgrade.costsPerLevel[0]} pts)",
                    new Vector2(200, 50));

                // Track references for ExtraJump (the default upgrade)
                if (upgrade.id == "extraJump")
                {
                    extraJumpTitleText = titleText;
                    extraJumpDescriptionText = descText;
                    extraJumpStatusText = statusText;
                    buyExtraJumpButton = buyButton;
                }
            }

            // ─── Create back button ───────────────────────────────────────
            var backButton = UISceneGeneratorBase.CreateSimpleButton(safeArea, "BackButton", "Back", new Vector2(150, 60));
            var backRect = backButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.anchoredPosition = new Vector2(100, -50);

            // ─── Debug add points button (conditional) ────────────────────
            GameObject addPointsDebugButton = null;
            if (config.showDebugAddPoints)
            {
                addPointsDebugButton = UISceneGeneratorBase.CreateSimpleButton(safeArea, "AddPointsDebugButton", "+500 Points", new Vector2(150, 40));
                var debugRect = addPointsDebugButton.GetComponent<RectTransform>();
                debugRect.anchorMin = new Vector2(1, 1);
                debugRect.anchorMax = new Vector2(1, 1);
                debugRect.anchoredPosition = new Vector2(-100, -50);
            }

            // ─── Wire UpgradeShopManager ──────────────────────────────────
            var usmSo = new SerializedObject(upgradeShopManager);
            usmSo.FindProperty("pointsText").objectReferenceValue = pointsText;
            usmSo.FindProperty("cumulativeHealthText").objectReferenceValue = cumulativeHealthText;
            usmSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<MobileControllerFramework.Button>();

            if (extraJumpTitleText != null)
                usmSo.FindProperty("extraJumpTitleText").objectReferenceValue = extraJumpTitleText;
            if (extraJumpDescriptionText != null)
                usmSo.FindProperty("extraJumpDescriptionText").objectReferenceValue = extraJumpDescriptionText;
            if (extraJumpStatusText != null)
                usmSo.FindProperty("extraJumpStatusText").objectReferenceValue = extraJumpStatusText;
            if (buyExtraJumpButton != null)
                usmSo.FindProperty("buyExtraJumpButton").objectReferenceValue = buyExtraJumpButton.GetComponent<MobileControllerFramework.Button>();

            if (addPointsDebugButton != null)
            {
                usmSo.FindProperty("addPointsDebugButton").objectReferenceValue = addPointsDebugButton.GetComponent<MobileControllerFramework.Button>();
                usmSo.FindProperty("debugPointsAmount").intValue = config.debugPointsAmount;
            }

            usmSo.ApplyModifiedPropertiesWithoutUndo();

            // ─── Save scene ───────────────────────────────────────────────
            UISceneGeneratorBase.SaveScene(OUTPUT_PATH);

            Debug.Log($"[UpgradeShopSceneGen] Created: {OUTPUT_PATH}");
        }
    }
}
#endif
