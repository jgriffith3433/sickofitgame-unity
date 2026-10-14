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
    /// Generates UpgradeShop.unity. The upgrade cards are built at runtime by UpgradeShopManager from
    /// upgradeShopConfig.layout and ui/theme.json (the player upgrade list is the catalog; the shop list is the fallback).
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
            var shell = MenuShell.Build(gd, config.baseConfig);
            if (shell == null)
                return;

            new GameObject("Upgrade Shop Manager").AddComponent<UpgradeShopManager>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), OutputPath);
        }
    }
}
#endif
