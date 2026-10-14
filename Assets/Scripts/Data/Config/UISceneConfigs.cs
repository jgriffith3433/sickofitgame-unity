using System;
using System.Collections.Generic;
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

        [Header("World")]
        [Tooltip("Backdrop sprite placed under the camera. Used by the selection and shop scenes.")]
        public Sprite groundSprite;
        public Vector2 groundPosition = new Vector2(1.03f, 0f);
        public Vector3 cameraPosition = new Vector3(0f, -0.23f, -10f);
    }

    /// <summary>
    /// Configuration for MainMenu scene generation.
    /// </summary>
    [Serializable]
    public class MainMenuConfig
    {
        [Header("Base Settings")]
        public UISceneConfig baseConfig = new UISceneConfig();
        
        [Header("Button Visibility")]
        public bool showContinueButton = true;
        public bool showNewGameButton = true;
        public bool showLevelSelectButton = true;
        public bool showVideosButton = true;
        public bool showUpgradeShopButton = true;
        public bool showSettingsButton = true;
        public bool showQuitButton = true;
        
        [Header("Roguelite UI")]
        public bool showCumulativeHealth = true;
        public bool showPointsDisplay = true;

        [Header("Look")]
        [Tooltip("Title, buttons, stickers and backdrop, built at runtime (style comes from ui/theme.json).")]
        public MenuLayoutConfig layout = new MenuLayoutConfig();
        public MainMenuLabels labels = new MainMenuLabels();
        
        [Header("Bundle Download UI")]
        public bool showBundleDownloadUI = true;
        
        [Header("Transition Visuals")]
        public Sprite idleHouseSprite;
        public Sprite explosionSprite;
        public AudioClip explodeAudio;
        public float hideDelay = 1.5f;
        public float transitionDelay = 1f;

        [Header("Scene Layout")]
        [Tooltip("Background, house, explosion, and any other menu art.")]
        public List<PlacedSpriteConfig> placedSprites = new List<PlacedSpriteConfig>();
        public bool placeCharacter = true;
        public Vector2 characterPosition = new Vector2(-4.21f, -3.14f);
        public bool characterCanShoot;

        [Tooltip("Placed sprite the hut colliders are parented under.")]
        public string houseSpriteName = "MainMenu_LeftPartOfHouse";
        public Vector3 houseBodyPosition = new Vector3(0.73f, 0.27f, 2f);
        public Vector3 houseBodyScale = new Vector3(0.25f, 0.25f, 0.25f);
        public List<MenuBoxConfig> houseColliders = new List<MenuBoxConfig>();
        public List<MenuBoxConfig> screenBounds = new List<MenuBoxConfig>();
    }

    /// <summary>
    /// A box collider placed by the main menu generator.
    /// </summary>
    [Serializable]
    public class MenuBoxConfig
    {
        public string name = "Box";
        public Vector2 position;
        public float rotation;
        public Vector2 size = Vector2.one;
        public Vector2 offset;
    }

    /// <summary>
    /// Configuration for Level/Video selection scenes.
    /// </summary>
    [Serializable]
    public class SelectionSceneConfig
    {
        [Header("Base Settings")]
        public UISceneConfig baseConfig = new UISceneConfig();

        [Header("Roguelite UI")]
        public bool showCumulativeHealth = true;

        [Header("Look")]
        [Tooltip("Title, item grid, stickers and backdrop, built at runtime (style comes from ui/theme.json).")]
        public MenuLayoutConfig layout = new MenuLayoutConfig();
        [Tooltip("Small text above each item's name. {0} = its number.")]
        public string captionFormat = "{0}";
        public string playText = "PLAY";
        public string lockedText = "LOCKED";
        public string clearedText = "CLEARED";
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
        [Tooltip("Used only when PlayerConfig.availableUpgrades is empty. The player list is the catalog.")]
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
        
        [Header("Debug")]
        public bool showDebugAddPoints = true;
        public int debugPointsAmount = 500;

        [Header("Look")]
        [Tooltip("Title, upgrade cards, stickers and backdrop, built at runtime (style comes from ui/theme.json).")]
        public MenuLayoutConfig layout = new MenuLayoutConfig();
        [Tooltip("{0} = cost.")]
        public string buyFormat = "BUY ♦ {0}";
        public string maxedText = "MAXED OUT";
        public string comingSoonText = "COMING SOON";
    }
}
