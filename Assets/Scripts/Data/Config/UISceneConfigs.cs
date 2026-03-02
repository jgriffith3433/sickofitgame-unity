using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Base configuration for UI scenes (shared elements).
    /// </summary>
    [Serializable]
    public class UISceneConfig
    {
        [Header("Camera")]
        public float cameraOrthoSize = 5f;
        public Color cameraBackgroundColor = Color.black;
        
        [Header("Canvas")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        
        [Header("Background")]
        public Sprite backgroundSprite;
        
        [Header("Lighting")]
        public bool useDirectionalLight = true;
        public Color lightColor = Color.white;
        public float lightIntensity = 1f;
    }

    /// <summary>
    /// Configuration for MainMenu scene generation.
    /// </summary>
    [Serializable]
    public class MainMenuConfig
    {
        [Header("Base Settings")]
        public UISceneConfig baseConfig = new UISceneConfig();
        
        [Header("Button Prefabs")]
        [Tooltip("Base button prefab template (MobileControllerFramework.Button)")]
        public GameObject buttonPrefab;
        
        [Header("Button Visibility")]
        public bool showContinueButton = true;
        public bool showNewGameButton = true;
        public bool showLevelSelectButton = true;
        public bool showVideosButton = true;
        public bool showUpgradeShopButton = true;
        public bool showQuitButton = true;
        
        [Header("Button Layout")]
        public float buttonSpacing = 50f;
        public Vector2 buttonSize = new Vector2(300, 80);
        
        [Header("Roguelite UI")]
        public bool showCumulativeHealth = true;
        public bool showPointsDisplay = true;
        
        [Header("Bundle Download UI")]
        public bool showBundleDownloadUI = true;
        
        [Header("Transition Visuals")]
        public Sprite idleHouseSprite;
        public Sprite explosionSprite;
        public float hideDelay = 1.5f;
        public float transitionDelay = 1f;
    }

    /// <summary>
    /// Configuration for Level/Video selection scenes.
    /// </summary>
    [Serializable]
    public class SelectionSceneConfig
    {
        [Header("Base Settings")]
        public UISceneConfig baseConfig = new UISceneConfig();
        
        [Header("Button Container")]
        public float buttonSpacing = 20f;
        public Vector2 buttonSize = new Vector2(300, 100);
        
        [Header("Button Prefabs")]
        [Tooltip("Button prefab for selection items (LevelButton/VideoButton)")]
        public GameObject itemButtonPrefab;
        [Tooltip("Back button prefab")]
        public GameObject backButtonPrefab;
        
        [Header("Roguelite UI")]
        public bool showCumulativeHealth = true;
    }

    /// <summary>
    /// Configuration for UpgradeShop scene generation.
    /// </summary>
    [Serializable]
    public class UpgradeShopConfig
    {
        [Header("Base Settings")]
        public UISceneConfig baseConfig = new UISceneConfig();
        
        [Header("Upgrades")]
        public UpgradeDefinition[] upgrades = new UpgradeDefinition[]
        {
            new UpgradeDefinition
            {
                id = "extraJump",
                title = "Extra Jump",
                description = "Allows an additional jump in the air",
                costsPerLevel = new int[] { 100, 200, 400, 800 },
                maxLevel = 4
            }
        };
        
        [Header("Button Prefabs")]
        public GameObject backButtonPrefab;
        public GameObject buyButtonPrefab;
        
        [Header("Debug")]
        public bool showDebugAddPoints = true;
        public int debugPointsAmount = 500;
    }
}
