#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.MCP
{
    /// <summary>
    /// Editor menu items that Claw can invoke via MCP execute_menu_item.
    /// Each menu item operates on GameDefinition.asset and logs JSON results.
    /// 
    /// Usage from Claw:
    ///   mcporter call unity execute_menu_item menuPath="Tools/Claw/Get Game Stats"
    ///   mcporter call unity execute_menu_item menuPath="Tools/Claw/Validate Game"
    /// </summary>
    public static class ClawGameTools
    {
        private static GameDefinition LoadGameDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length == 0)
            {
                Debug.LogError("[ClawTools] No GameDefinition found!");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void SaveAndRefresh(GameDefinition gd)
        {
            EditorUtility.SetDirty(gd);
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // READ TOOLS
        // =====================================================================

        [MenuItem("Tools/Claw/Get Game Stats")]
        public static void GetGameStats()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int totalEnemies = 0;
            float totalDuration = 0;
            foreach (var level in gd.levels)
            {
                totalEnemies += level.EnemyCount;
                totalDuration += level.duration;
            }

            Debug.Log($"[ClawTools] GAME STATS: " +
                $"Levels={gd.TotalLevels}, " +
                $"Videos={gd.TotalVideos}, " +
                $"Deaths={gd.TotalDeaths}, " +
                $"EnemyTemplates={gd.enemyTemplates.Count}, " +
                $"TotalEnemySpawns={totalEnemies}, " +
                $"TotalDuration={totalDuration}s, " +
                $"PlayerHP={gd.player.baseMaxHealth}, " +
                $"PlayerSpeed={gd.player.moveSpeed}");
        }

        [MenuItem("Tools/Claw/Get Level Summary")]
        public static void GetLevelSummary()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            for (int i = 0; i < gd.levels.Count; i++)
            {
                var lv = gd.levels[i];
                var enemyCounts = new Dictionary<EnemyType, int>();
                foreach (var e in lv.enemies)
                {
                    if (!enemyCounts.ContainsKey(e.type)) enemyCounts[e.type] = 0;
                    enemyCounts[e.type]++;
                }
                string enemies = string.Join(", ", enemyCounts.Select(kv => $"{kv.Value}x{kv.Key}"));
                Debug.Log($"[ClawTools] Level {i + 1} '{lv.displayName}': " +
                    $"{lv.EnemyCount} enemies [{enemies}], " +
                    $"duration={lv.duration}s, " +
                    $"death={lv.deathConfigName}, " +
                    $"music={lv.music?.name ?? "NONE"}, " +
                    $"backgrounds={lv.backgrounds.Count}, " +
                    $"twoStage={lv.hasTwoStageSequence}");
            }
        }

        [MenuItem("Tools/Claw/Get Enemy Templates")]
        public static void GetEnemyTemplates()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            foreach (var t in gd.enemyTemplates)
            {
                if (t == null) continue;
                Debug.Log($"[ClawTools] EnemyTemplate '{t.displayName}': " +
                    $"type={t.type}, " +
                    $"hp={t.defaultHealth}, " +
                    $"speed={t.defaultMoveSpeed}, " +
                    $"damage={t.contactDamage}, " +
                    $"detection={t.detectionRange}, " +
                    $"dropChance={t.dropChance}, " +
                    $"prefab={t.prefab?.name ?? "NONE"}, " +
                    $"attackRange={t.attackRange}, " +
                    $"fireRate={t.fireRate}");
            }
        }

        [MenuItem("Tools/Claw/Get Player Config")]
        public static void GetPlayerConfig()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            var p = gd.player;
            Debug.Log($"[ClawTools] PLAYER: " +
                $"hp={p.baseMaxHealth}, " +
                $"speed={p.moveSpeed}, " +
                $"jump={p.jumpForce}, " +
                $"gravity={p.gravityScale}, " +
                $"shooting={p.enableShooting}, " +
                $"fireRate={p.fireRate}, " +
                $"projectileSpeed={p.projectileSpeed}, " +
                $"upgrades={p.availableUpgrades.Count}");
        }

        // =====================================================================
        // VALIDATION
        // =====================================================================

        [MenuItem("Tools/Claw/Validate Game")]
        public static void ValidateGame()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            var issues = gd.Validate();
            if (issues.Count == 0)
            {
                Debug.Log("[ClawTools] VALIDATION PASSED: No issues found ✅");
            }
            else
            {
                Debug.LogWarning($"[ClawTools] VALIDATION: {issues.Count} issue(s) found:");
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"  ⚠️ {issue}");
                }
            }
        }

        // =====================================================================
        // LEVEL MODIFICATION TOOLS
        // =====================================================================

        [MenuItem("Tools/Claw/Add Enemy To Level 1/Chaser at Origin")]
        public static void AddChaserToLevel1() => AddEnemyToLevel(1, EnemyType.Chaser, Vector2.zero);

        [MenuItem("Tools/Claw/Add Enemy To Level 1/Fighter at Origin")]
        public static void AddFighterToLevel1() => AddEnemyToLevel(1, EnemyType.Fighter, Vector2.zero);

        [MenuItem("Tools/Claw/Add Enemy To Level 1/Shooter at Origin")]
        public static void AddShooterToLevel1() => AddEnemyToLevel(1, EnemyType.Shooter, Vector2.zero);

        [MenuItem("Tools/Claw/Add Enemy To Level 1/Sentinel at Origin")]
        public static void AddSentinelToLevel1() => AddEnemyToLevel(1, EnemyType.Sentinel, Vector2.zero);

        /// <summary>
        /// Add an enemy spawn to a level. Claw calls this programmatically.
        /// For custom positions, Claw edits the config directly via update_component on GameDefinition.
        /// </summary>
        public static void AddEnemyToLevel(int levelNumber, EnemyType type, Vector2 position, 
            float spawnTime = 0f, int healthOverride = 0, float speedOverride = 0f)
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            var level = gd.GetLevel(levelNumber);
            if (level == null) return;

            var spawn = new EnemySpawnConfig
            {
                type = type,
                position = position,
                spawnTime = spawnTime,
                activeAtStart = spawnTime <= 0f,
                healthOverride = healthOverride,
                speedOverride = speedOverride
            };

            level.enemies.Add(spawn);
            SaveAndRefresh(gd);

            Debug.Log($"[ClawTools] Added {type} to Level {levelNumber} at ({position.x}, {position.y}), " +
                $"spawnTime={spawnTime}s. Total enemies: {level.EnemyCount}");
        }

        /// <summary>
        /// Remove the last enemy from a level.
        /// </summary>
        public static void RemoveLastEnemyFromLevel(int levelNumber)
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            var level = gd.GetLevel(levelNumber);
            if (level == null || level.enemies.Count == 0)
            {
                Debug.LogWarning($"[ClawTools] Level {levelNumber} has no enemies to remove.");
                return;
            }

            var removed = level.enemies[level.enemies.Count - 1];
            level.enemies.RemoveAt(level.enemies.Count - 1);
            SaveAndRefresh(gd);

            Debug.Log($"[ClawTools] Removed {removed.type} from Level {levelNumber}. Remaining: {level.EnemyCount}");
        }

        // =====================================================================
        // BULK OPERATIONS
        // =====================================================================

        [MenuItem("Tools/Claw/Clear All Enemies From All Levels")]
        public static void ClearAllEnemies()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int total = 0;
            foreach (var level in gd.levels)
            {
                total += level.enemies.Count;
                level.enemies.Clear();
            }
            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Cleared {total} enemies from all {gd.TotalLevels} levels.");
        }

        [MenuItem("Tools/Claw/Set Player Speed/1")]
        public static void SetPlayerSpeed1() => SetPlayerSpeed(1f);
        [MenuItem("Tools/Claw/Set Player Speed/2")]
        public static void SetPlayerSpeed2() => SetPlayerSpeed(2f);
        [MenuItem("Tools/Claw/Set Player Speed/3")]
        public static void SetPlayerSpeed3() => SetPlayerSpeed(3f);
        [MenuItem("Tools/Claw/Set Player Speed/5")]
        public static void SetPlayerSpeed5() => SetPlayerSpeed(5f);

        public static void SetPlayerSpeed(float speed)
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;
            gd.player.moveSpeed = speed;
            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Player speed set to {speed}");
        }

        public static void SetPlayerHealth(int hp)
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;
            gd.player.baseMaxHealth = hp;
            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Player base max health set to {hp}");
        }

        [MenuItem("Tools/Claw/Set Level Duration/Level 1 to 90s")]
        public static void SetLevel1Duration90() => SetLevelDuration(1, 90f);
        [MenuItem("Tools/Claw/Set Level Duration/Level 1 to 120s")]
        public static void SetLevel1Duration120() => SetLevelDuration(1, 120f);

        public static void SetLevelDuration(int levelNumber, float duration)
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;
            var level = gd.GetLevel(levelNumber);
            if (level == null) return;
            level.duration = duration;
            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Level {levelNumber} duration set to {duration}s");
        }

        // =====================================================================
        // ADD NEW LEVEL
        // =====================================================================

        [MenuItem("Tools/Claw/Add New Level")]
        public static void AddNewLevel()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int newNum = gd.TotalLevels + 1;
            var newLevel = new LevelConfig
            {
                levelName = $"Level{newNum}",
                displayName = $"Level {newNum}",
                levelNumber = newNum,
                duration = gd.defaultLevelDuration,
                deathConfigName = gd.deaths.Count > 0 ? gd.deaths[0].deathName : "Death1",
                enemies = new List<EnemySpawnConfig>(),
                platforms = new List<PlatformConfig>(),
                backgrounds = new List<ScriptableObject>(),
                timeline = new TimelineConfig()
            };

            gd.levels.Add(newLevel);
            SaveAndRefresh(gd);

            Debug.Log($"[ClawTools] Added Level {newNum} '{newLevel.displayName}'. Total levels: {gd.TotalLevels}");
        }

        [MenuItem("Tools/Claw/Remove Last Level")]
        public static void RemoveLastLevel()
        {
            var gd = LoadGameDefinition();
            if (gd == null || gd.TotalLevels == 0) return;

            var removed = gd.levels[gd.levels.Count - 1];
            gd.levels.RemoveAt(gd.levels.Count - 1);
            SaveAndRefresh(gd);

            Debug.Log($"[ClawTools] Removed Level '{removed.displayName}'. Remaining: {gd.TotalLevels}");
        }
    }
}
#endif
