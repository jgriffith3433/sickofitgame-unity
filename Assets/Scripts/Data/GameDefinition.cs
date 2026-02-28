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

        [Header("Enemy Templates")]
        public List<EnemyTemplate> enemyTemplates = new List<EnemyTemplate>();

        [Header("Global Settings")]
        public float defaultLevelDuration = 60f;
        public int startingLevel = 1;
        public bool debugMode;

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
        /// Get an enemy template by type.
        /// </summary>
        public EnemyTemplate GetEnemyTemplate(EnemyType type)
        {
            for (int i = 0; i < enemyTemplates.Count; i++)
            {
                if (enemyTemplates[i] != null && enemyTemplates[i].type == type)
                    return enemyTemplates[i];
            }
            Debug.LogError($"[GameDefinition] Enemy template for type '{type}' not found");
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

                if (level.backgrounds.Count == 0)
                    issues.Add($"{prefix}: No backgrounds assigned");

                if (level.music == null)
                    issues.Add($"{prefix}: No music assigned");

                if (level.enemies.Count == 0)
                    issues.Add($"{prefix}: No enemies (is this intentional?)");

                if (level.duration <= 0)
                    issues.Add($"{prefix}: Duration is 0 or negative");

                // Validate enemy types have templates
                for (int j = 0; j < level.enemies.Count; j++)
                {
                    var enemy = level.enemies[j];
                    if (GetEnemyTemplate(enemy.type) == null)
                        issues.Add($"{prefix}: Enemy #{j + 1} uses type '{enemy.type}' but no template exists");
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

            // Validate enemy templates
            for (int i = 0; i < enemyTemplates.Count; i++)
            {
                if (enemyTemplates[i] == null)
                {
                    issues.Add($"Enemy template #{i + 1}: NULL reference");
                    continue;
                }
                if (enemyTemplates[i].prefab == null)
                    issues.Add($"Enemy template '{enemyTemplates[i].displayName}': No prefab assigned");
            }

            // Player validation
            if (player.baseMaxHealth <= 0)
                issues.Add("Player: Base max health is 0 or negative");

            return issues;
        }

        // =========================================================================
        // SINGLETON ACCESS (for runtime)
        // =========================================================================

        private static GameDefinition _instance;

        /// <summary>
        /// Runtime accessor. Loads from Resources/GameDefinition.
        /// </summary>
        public static GameDefinition Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameDefinition>("GameDefinition");
                    if (_instance == null)
                        Debug.LogError("[GameDefinition] GameDefinition.asset not found in Resources!");
                }
                return _instance;
            }
        }
    }
}
