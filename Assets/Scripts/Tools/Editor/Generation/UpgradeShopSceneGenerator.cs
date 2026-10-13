#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Core.Progression;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generates UpgradeShop.unity from UpgradeShopConfig.
    /// The player upgrade list is the catalog. The shop config list is the fallback.
    /// </summary>
    public static class UpgradeShopSceneGenerator
    {
        const string OutputPath = "Assets/Scenes/UpgradeShop.unity";

        public static void Generate(GameDefinition gd)
        {
            if (gd == null)
            {
                Debug.LogError("[UpgradeShopSceneGen] GameDefinition is null.");
                return;
            }

            var config = gd.upgradeShopConfig ?? new UpgradeShopConfig();
            var shell = MenuShell.Build(
                gd,
                config.baseConfig,
                "UpgradeShopPanel",
                string.IsNullOrEmpty(config.title) ? "Upgrade Shop" : config.title,
                "ShopContainer",
                config.buttonSpacing,
                config.buttonSize);
            if (shell == null)
                return;

            var buttonPrefab = config.buyButtonPrefab != null
                ? config.buyButtonPrefab
                : config.backButtonPrefab != null
                    ? config.backButtonPrefab
                    : gd.mainMenuConfig != null ? gd.mainMenuConfig.buttonPrefab : null;

            TextMeshProUGUI extraJumpTitle = null;
            TextMeshProUGUI extraJumpDescription = null;
            TextMeshProUGUI extraJumpCost = null;
            TextMeshProUGUI extraJumpStatus = null;
            GameObject buyExtraJump = null;

            foreach (var upgrade in Catalog(gd, config))
            {
                if (upgrade == null)
                    continue;

                var title = MenuShell.CreateText(shell.Container, upgrade.id + "TitleText", upgrade.title, 32f);
                var description = MenuShell.CreateText(shell.Container, upgrade.id + "DescriptionText", upgrade.description, 24f);
                int cost = upgrade.costsPerLevel != null && upgrade.costsPerLevel.Length > 0 ? upgrade.costsPerLevel[0] : 0;
                var costText = MenuShell.CreateText(shell.Container, upgrade.id + "CostText", "Cost: " + cost, 24f);
                var status = MenuShell.CreateText(shell.Container, upgrade.id + "StatusText", "Level: 0 / " + upgrade.maxLevel, 24f);
                var buy = MenuShell.AddOvalButton(shell.Container, buttonPrefab, "Buy" + upgrade.id + "Button", "Buy " + upgrade.title, config.buttonSize);

                if (upgrade.id == "extraJump")
                {
                    extraJumpTitle = title;
                    extraJumpDescription = description;
                    extraJumpCost = costText;
                    extraJumpStatus = status;
                    buyExtraJump = buy;
                }
            }

            var back = MenuShell.AddOvalButton(shell.SafeArea, config.backButtonPrefab != null ? config.backButtonPrefab : buttonPrefab, "(Prb)ButtonBack", "Back", config.buttonSize);
            MenuShell.PlaceCornerButton(back, Vector2.zero, Vector2.zero, new Vector2(60f, 50f));

            GameObject debug = null;
            if (config.showDebugAddPoints)
            {
                debug = MenuShell.AddOvalButton(shell.SafeArea, buttonPrefab, "AddPointsDebugButton", "+" + config.debugPointsAmount + " Points", config.buttonSize);
                MenuShell.PlaceCornerButton(debug, Vector2.one, Vector2.one, new Vector2(-40f, -40f));
            }

            var manager = new GameObject("Upgrade Shop Manager").AddComponent<UpgradeShopManager>();
            var so = new SerializedObject(manager);
            if (shell.Points != null)
                so.FindProperty("pointsText").objectReferenceValue = shell.Points;
            if (extraJumpTitle != null)
                so.FindProperty("extraJumpTitleText").objectReferenceValue = extraJumpTitle;
            if (extraJumpDescription != null)
                so.FindProperty("extraJumpDescriptionText").objectReferenceValue = extraJumpDescription;
            if (extraJumpCost != null)
                so.FindProperty("extraJumpCostText").objectReferenceValue = extraJumpCost;
            if (extraJumpStatus != null)
                so.FindProperty("extraJumpStatusText").objectReferenceValue = extraJumpStatus;
            if (buyExtraJump != null)
                so.FindProperty("buyExtraJumpButton").objectReferenceValue = buyExtraJump.GetComponent<MobileControllerFramework.Button>();
            if (shell.Health != null)
                so.FindProperty("cumulativeHealthText").objectReferenceValue = shell.Health;
            if (back != null)
                so.FindProperty("backButton").objectReferenceValue = back.GetComponent<MobileControllerFramework.Button>();
            if (debug != null)
            {
                so.FindProperty("addPointsDebugButton").objectReferenceValue = debug.GetComponent<MobileControllerFramework.Button>();
                so.FindProperty("debugPointsAmount").intValue = config.debugPointsAmount;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), OutputPath);
        }

        static IList<UpgradeDefinition> Catalog(GameDefinition gd, UpgradeShopConfig config)
        {
            if (gd.player != null && gd.player.availableUpgrades != null && gd.player.availableUpgrades.Count > 0)
                return gd.player.availableUpgrades;
            return config.upgrades;
        }
    }
}
#endif
