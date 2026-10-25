using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Data
{
    /// <summary>
    /// THE source of truth for the entire game.
    /// Every level, enemy, video, death scene, and player setting is defined here.
    /// Unity scenes are OUTPUT generated from this config.
    /// 
    /// Create one: Assets → Create → Sick Of It → Game Definition
    /// </summary>
    [CreateAssetMenu(fileName = "GameDefinition", menuName = "Sick Of It/Game Definition", order = 0)]
    public class GameDefinition : ScriptableObject
    {
        [Header("Player")]
        public PlayerConfig player = new PlayerConfig();

        [Header("Levels")]
        public List<LevelConfig> levels = new List<LevelConfig>();

        [Header("Videos")]
        public List<VideoConfig> videos = new List<VideoConfig>();

        [Header("Deaths")]
        public List<DeathConfig> deaths = new List<DeathConfig>();

        [Header("Enemy Types")]
        public List<EnemyTypeConfig> enemyTypes = new List<EnemyTypeConfig>();

        [Header("Projectile Configs")]
        [Tooltip("Projectile definitions (Player, Enemy, Laser). Used by ProjectilePrefabGenerator.")]
        public List<ProjectileConfig> projectileConfigs = new List<ProjectileConfig>();

        [Header("Pickup Config")]
        [Tooltip("Configuration for point drop pickups (Diamond)")]
        public PickupConfig pickupConfig = new PickupConfig();

        [Header("Platform Defaults")]
        public float platformColliderHeight = 0.1f;

        [Header("Presentation")]
        [Tooltip("Sorting-band spacing, draw order inside the Player band, and the platform color.")]
        public PresentationConfig presentation = new PresentationConfig();

        [Header("Character Art")]
        [Tooltip("mp4 folders and sprite-sheet packing used by Regenerate Player/Enemy Sprites.")]
        public CharacterArtConfig characterArt = new CharacterArtConfig();

        [Header("Source Media")]
        [Tooltip("Authoring folders for videos, object clips, and still images. Generate Project lists what is missing.")]
        public SourceMediaConfig sourceMedia = new SourceMediaConfig();

        [Header("Camera Defaults")]
        [Tooltip("Orthographic size for level camera")]
        public float levelCameraOrthoSize = 2.8f;
        [Tooltip("Orthographic size for video/death cameras")]
        public float cinematicCameraOrthoSize = 2.8f;
        public Color cinematicCameraBackground = Color.black;

        [Header("Level Scene Layout")]
        [Tooltip("Y position of the ground collider")]
        public float groundY = -3.5f;
        [Tooltip("Size of the ground collider")]
        public Vector2 groundColliderSize = new Vector2(14f, 1f);
        [Tooltip("X distance of boundary walls from center")]
        public float wallX = 7f;
        [Tooltip("Size of each boundary wall collider")]
        public Vector2 wallColliderSize = new Vector2(1f, 8f);

        [Header("Global Settings")]
        public float defaultLevelDuration = 60f;

        [Header("Rewind Settings")]
        [Tooltip("How often rewind state is recorded (seconds). Lower = more precise, more memory.")]
        public float rewindRecordInterval = 0.05f;
        [Tooltip("Maximum seconds of rewind history to keep.")]
        public float rewindMaxHistorySeconds = 30f;
        [Tooltip("How much rewinding the player gets per level, and how it refills.")]
        public RewindBudgetConfig rewindBudget = new RewindBudgetConfig();

        [Header("Scene UI Prefabs")]
        [Tooltip("Full MobileController UI prefab for level scenes (movement, jump, shoot, etc.)")]
        public GameObject levelUIPrefab;
        [Tooltip("Minimal UI prefab for video scenes (skip button only)")]
        public GameObject videoUIPrefab;
        [Tooltip("Minimal UI prefab for death scenes (skip button only)")]
        public GameObject deathUIPrefab;

        [Header("Scene Camera Prefabs")]
        [Tooltip("Camera prefab for level scenes")]
        public GameObject levelCameraPrefab;
        [Tooltip("Camera prefab for video scenes")]
        public GameObject videoCameraPrefab;
        [Tooltip("Camera prefab for death scenes")]
        public GameObject deathCameraPrefab;

        [Header("UI Scene Configs")]
        [Tooltip("Configuration for MainMenu scene generation")]
        public MainMenuConfig mainMenuConfig = new MainMenuConfig();
        [Tooltip("Configuration for LevelSelection scene generation")]
        public SelectionSceneConfig levelSelectionConfig = new SelectionSceneConfig();
        [Tooltip("Configuration for VideoSelection scene generation")]
        public SelectionSceneConfig videoSelectionConfig = new SelectionSceneConfig();
        [Tooltip("Configuration for UpgradeShop scene generation")]
        public UpgradeShopConfig upgradeShopConfig = new UpgradeShopConfig();
        [Tooltip("The Shaft, Hell's map between videos and levels (ui/shaft.json)")]
        public ShaftConfig shaft = new ShaftConfig();
        [Tooltip("Yooba's talking portrait and lines on the Shaft and in the shop (ui/yooba.json)")]
        public YoobaConfig yooba = new YoobaConfig();
        [Tooltip("The Codex: what the hero has seen, from the main and pause menus (ui/codex.json)")]
        public CodexConfig codex = new CodexConfig();
        [Tooltip("Shared look of all menus (ui/theme.json)")]
        public MenuTheme menuTheme = new MenuTheme();
        [Tooltip("Sounds (generated with ElevenLabs) and which plays for each game event (audio.json)")]
        public AudioConfig audio = new AudioConfig();

        [Header("Gameplay Content")]
        [Tooltip("Entities, sequences, and status effects, parsed at runtime. Authored in Assets/GameConfig/content/.")]
        public List<ContentFile> content = new List<ContentFile>();
        [Tooltip("Optional URL of a content bundle that replaces the content and level scripts above at startup, so they can change without a rebuild.")]
        public string remoteContentUrl = "";

        [Tooltip("Assets used by gameplay content, by id. Built by the config importer — DO NOT EDIT.")]
        public List<AssetRegistryEntry> assetRegistry = new List<AssetRegistryEntry>();

        [Header("Generated Prefab References (set by generators — DO NOT EDIT)")]
        [Tooltip("Set by PickupPrefabGenerator")]
        public GameObject pickupPrefab;
        [Tooltip("Set by PlatformPrefabGenerator")]
        public GameObject platformPrefab;

        // =========================================================================
        // ACCESSORS
        // =========================================================================

        public int TotalLevels => levels.Count;
        public int TotalVideos => videos.Count;
        public int TotalDeaths => deaths.Count;

        /// <summary>
        /// Get a level config by 1-indexed level number.
        /// </summary>
        public LevelConfig GetLevel(int levelNumber)
        {
            if (levelNumber < 1 || levelNumber > levels.Count)
            {
                Debug.LogError($"[GameDefinition] Level {levelNumber} out of range (1-{levels.Count})");
                return null;
            }
            return levels[levelNumber - 1];
        }

        /// <summary>
        /// Get a video config by 1-indexed video number.
        /// </summary>
        public VideoConfig GetVideo(int videoNumber)
        {
            if (videoNumber < 1 || videoNumber > videos.Count)
            {
                Debug.LogError($"[GameDefinition] Video {videoNumber} out of range (1-{videos.Count})");
                return null;
            }
            return videos[videoNumber - 1];
        }

        /// <summary>
        /// Get a death config by name.
        /// </summary>
        public DeathConfig GetDeath(string deathName)
        {
            for (int i = 0; i < deaths.Count; i++)
            {
                if (deaths[i].deathName == deathName)
                    return deaths[i];
            }
            Debug.LogError($"[GameDefinition] Death config '{deathName}' not found");
            return null;
        }

        /// <summary>
        /// Get an enemy type config by type enum.
        /// </summary>
        public EnemyTypeConfig GetEnemyType(EnemyType type)
        {
            for (int i = 0; i < enemyTypes.Count; i++)
            {
                if (enemyTypes[i] != null && enemyTypes[i].type == type)
                    return enemyTypes[i];
            }
            Debug.LogError($"[GameDefinition] Enemy type config for '{type}' not found");
            return null;
        }

        /// <summary>
        /// Get the video that plays after a specific level (if any).
        /// </summary>
        public VideoConfig GetVideoAfterLevel(int levelNumber)
        {
            for (int i = 0; i < videos.Count; i++)
            {
                if (videos[i].afterLevel == levelNumber)
                    return videos[i];
            }
            return null;
        }

        // =========================================================================
        // VALIDATION
        // =========================================================================

        /// <summary>
        /// Validate the entire game definition. Returns a list of issues.
        /// </summary>
        public List<string> Validate()
        {
            var issues = new List<string>();

            if (levels.Count == 0)
                issues.Add("No levels defined");

            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                string prefix = $"Level {i + 1} ({level.levelName})";

                if (string.IsNullOrEmpty(level.levelName))
                    issues.Add($"{prefix}: Missing level name");

                if (level.backgrounds.Count == 0 && level.backgroundLayers.Count == 0)
                    issues.Add($"{prefix}: No backgrounds assigned (use backgrounds or backgroundLayers)");

                if (level.music == null)
                    issues.Add($"{prefix}: No music assigned");

                if (level.enemies.Count == 0)
                    issues.Add($"{prefix}: No enemies (is this intentional?)");

                if (level.duration <= 0)
                    issues.Add($"{prefix}: Duration is 0 or negative");

                // Validate enemy types have configs
                for (int j = 0; j < level.enemies.Count; j++)
                {
                    var enemy = level.enemies[j];
                    if (GetEnemyType(enemy.type) == null)
                        issues.Add($"{prefix}: Enemy #{j + 1} uses type '{enemy.type}' but no config exists");
                }

                // Validate death config exists
                if (!string.IsNullOrEmpty(level.deathConfigName) && GetDeath(level.deathConfigName) == null)
                    issues.Add($"{prefix}: References death config '{level.deathConfigName}' which doesn't exist");
            }

            for (int i = 0; i < videos.Count; i++)
            {
                var video = videos[i];
                if (video.spriteFramesData == null)
                    issues.Add($"Video {i + 1} ({video.videoName}): No sprite frames data");
            }

            for (int i = 0; i < deaths.Count; i++)
            {
                var death = deaths[i];
                if (string.IsNullOrEmpty(death.deathName))
                    issues.Add($"Death #{i + 1}: Missing death name");
            }

            // Validate enemy type configs
            for (int i = 0; i < enemyTypes.Count; i++)
            {
                if (enemyTypes[i] == null)
                {
                    issues.Add($"Enemy type #{i + 1}: NULL reference");
                    continue;
                }
                if (enemyTypes[i].prefab == null)
                    issues.Add($"Enemy type '{enemyTypes[i].displayName}': No prefab assigned");
            }

            // Timeline overlays (keyframed pictures over a video, death or level)
            for (int i = 0; i < levels.Count; i++)
                AddOverlayIssues(issues, $"Level {i + 1} ({levels[i].levelName})", levels[i].timeline);
            for (int i = 0; i < videos.Count; i++)
                AddOverlayIssues(issues, $"Video {i + 1} ({videos[i].videoName})", videos[i].timeline);
            for (int i = 0; i < deaths.Count; i++)
                AddOverlayIssues(issues, $"Death #{i + 1} ({deaths[i].deathName})", deaths[i].timeline);

            // Player validation
            if (player.baseMaxHealth <= 0)
                issues.Add("Player: Base max health is 0 or negative");

            return issues;
        }

        static void AddOverlayIssues(List<string> issues, string prefix, TimelineConfig timeline)
        {
            if (timeline == null || timeline.overlays == null)
                return;
            foreach (var overlay in timeline.overlays)
            {
                if (overlay == null)
                {
                    issues.Add($"{prefix}: timeline.overlays has an empty entry");
                    continue;
                }
                foreach (string problem in overlay.Problems())
                    issues.Add($"{prefix}: timeline {problem}");
            }
        }

        // =========================================================================
        // ACCESSORS
        // =========================================================================

        /// <summary>
        /// The sprites cut from a sheet, in name order, from the asset registry (a texture's sprites can't be
        /// listed at runtime otherwise). Empty if no config uses the sheet.
        /// </summary>
        public Sprite[] SpritesOf(Texture2D sheet)
        {
            if (sheet != null && assetRegistry != null)
            {
                for (int i = 0; i < assetRegistry.Count; i++)
                {
                    var entry = assetRegistry[i];
                    if (entry != null && entry.asset == sheet && entry.sprites != null)
                        return entry.sprites;
                }
            }
            return System.Array.Empty<Sprite>();
        }

        /// <summary>
        /// Find a projectile config by its ID string (e.g. "Player", "Enemy", "Laser").
        /// </summary>
        public ProjectileConfig GetProjectileConfig(string id)
        {
            for (int i = 0; i < projectileConfigs.Count; i++)
            {
                if (projectileConfigs[i].id == id)
                    return projectileConfigs[i];
            }
            return null;
        }

        // =========================================================================
        // SINGLETON ACCESS (for runtime)
        // =========================================================================

        private static GameDefinition _instance;
        private static System.Action whenReady;

        /// <summary>Its Addressables address in a player build, where it isn't in Resources (see GameDefinitionBoot).</summary>
        public const string Address = "GameDefinition";

        /// <summary>
        /// True while a player build loads the definition from its Addressables bundle (the Boot scene). Until
        /// <see cref="Provide"/>, <see cref="Instance"/> is null without an error.
        /// </summary>
        public static bool Loading { get; set; }

        /// <summary>
        /// Runtime accessor. The editor and tests load it from Resources/GameDefinition; a player build gets it from
        /// its Addressables bundle at boot (<see cref="Provide"/>), so its art and sound stay out of the player data.
        /// </summary>
        public static GameDefinition Instance
        {
            get
            {
                if (_instance == null && !Loading)
                {
                    _instance = Resources.Load<GameDefinition>("GameDefinition");
#if UNITY_EDITOR
                    // During a deploy's player build it sits outside Resources for a while (PlayerDataSplit).
                    if (_instance == null && !Application.isPlaying)
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDefinition>("Assets/GameDefinitionBuild/GameDefinition.asset");
#endif
                    if (_instance == null)
                        Debug.LogError("[GameDefinition] GameDefinition.asset not found in Resources!");
                }
                return _instance;
            }
        }

        /// <summary>The boot loader hands over the definition it loaded; everything waiting in <see cref="WhenReady"/> runs.</summary>
        public static void Provide(GameDefinition definition)
        {
            _instance = definition;
            Loading = false;
            var waiting = whenReady;
            whenReady = null;
            waiting?.Invoke();
        }

        /// <summary>Runs <paramref name="action"/> now if the definition is there, or as soon as the boot has loaded it.</summary>
        public static void WhenReady(System.Action action)
        {
            if (action == null)
                return;
            if (Loading)
                whenReady += action;
            else
                action();
        }
    }
}
