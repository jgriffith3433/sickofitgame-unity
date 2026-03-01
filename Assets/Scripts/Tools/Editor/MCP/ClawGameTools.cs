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

        // =====================================================================
        // EXPERIENCE / TIMELINE TOOLS
        // =====================================================================

        [MenuItem("Tools/Claw/Get Level Events")]
        public static void GetLevelEvents()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            for (int i = 0; i < gd.levels.Count; i++)
            {
                var level = gd.levels[i];
                int eventCount = level.timeline?.events?.Count ?? 0;
                if (eventCount == 0)
                {
                    Debug.Log($"[ClawTools] Level {i + 1}: No experience events");
                    continue;
                }

                Debug.Log($"[ClawTools] Level {i + 1}: {eventCount} events:");
                foreach (var evt in level.timeline.events)
                {
                    string detail = evt.type switch
                    {
                        GameEventType.PlaySFX => $"clip={evt.audioClip?.name ?? "null"}, vol={evt.volume}",
                        GameEventType.PlayMusic => $"clip={evt.audioClip?.name ?? "null"}, vol={evt.volume}",
                        GameEventType.CameraShake => $"intensity={evt.shakeIntensity}, dur={evt.duration}s",
                        GameEventType.CameraZoom => $"zoom={evt.zoomTarget}, dur={evt.duration}s",
                        GameEventType.SpawnEnemyWave => $"{evt.enemyCount}x {evt.enemyType} at ({evt.spawnPosition.x},{evt.spawnPosition.y})",
                        GameEventType.ActivateObject => $"path={evt.targetPath}",
                        GameEventType.DeactivateObject => $"path={evt.targetPath}",
                        GameEventType.SlowMotion => $"scale={evt.timeScale}, dur={evt.duration}s",
                        GameEventType.ScreenFlash => $"color={evt.color}, dur={evt.duration}s",
                        _ => ""
                    };
                    Debug.Log($"  t={evt.time:F1}s [{evt.type}] {evt.label} {detail}");
                }
            }
        }

        /// <summary>
        /// Add a SFX event to a level's timeline.
        /// Environment vars: LEVEL_NUMBER, EVENT_TIME, CLIP_NAME (AudioClip asset name), VOLUME (optional)
        /// </summary>
        [MenuItem("Tools/Claw/Add SFX Event To Level")]
        public static void AddSFXEventToLevel()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int levelNum = int.TryParse(System.Environment.GetEnvironmentVariable("LEVEL_NUMBER"), out var ln) ? ln : 1;
            float time = float.TryParse(System.Environment.GetEnvironmentVariable("EVENT_TIME"), out var t) ? t : 0f;
            string clipName = System.Environment.GetEnvironmentVariable("CLIP_NAME") ?? "";
            float volume = float.TryParse(System.Environment.GetEnvironmentVariable("VOLUME"), out var v) ? v : 1f;
            string label = System.Environment.GetEnvironmentVariable("EVENT_LABEL") ?? "";

            var level = gd.GetLevel(levelNum);
            if (level == null) return;

            // Find audio clip by name
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(clipName))
            {
                string[] guids = AssetDatabase.FindAssets($"t:AudioClip {clipName}");
                if (guids.Length > 0)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (level.timeline == null)
                level.timeline = new TimelineConfig();

            level.timeline.events.Add(new GameEvent
            {
                time = time,
                type = GameEventType.PlaySFX,
                audioClip = clip,
                volume = volume,
                label = label
            });

            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Added SFX '{clipName}' to Level {levelNum} at t={time}s (vol={volume})");
        }

        /// <summary>
        /// Add a camera shake event.
        /// Environment vars: LEVEL_NUMBER, EVENT_TIME, SHAKE_INTENSITY (default 0.3), SHAKE_DURATION (default 0.3)
        /// </summary>
        [MenuItem("Tools/Claw/Add Camera Shake To Level")]
        public static void AddCameraShakeToLevel()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int levelNum = int.TryParse(System.Environment.GetEnvironmentVariable("LEVEL_NUMBER"), out var ln) ? ln : 1;
            float time = float.TryParse(System.Environment.GetEnvironmentVariable("EVENT_TIME"), out var t) ? t : 0f;
            float intensity = float.TryParse(System.Environment.GetEnvironmentVariable("SHAKE_INTENSITY"), out var si) ? si : 0.3f;
            float duration = float.TryParse(System.Environment.GetEnvironmentVariable("SHAKE_DURATION"), out var sd) ? sd : 0.3f;
            string label = System.Environment.GetEnvironmentVariable("EVENT_LABEL") ?? "";

            var level = gd.GetLevel(levelNum);
            if (level == null) return;

            if (level.timeline == null)
                level.timeline = new TimelineConfig();

            level.timeline.events.Add(new GameEvent
            {
                time = time,
                type = GameEventType.CameraShake,
                shakeIntensity = intensity,
                duration = duration,
                label = label
            });

            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Added CameraShake to Level {levelNum} at t={time}s (intensity={intensity}, dur={duration}s)");
        }

        /// <summary>
        /// Add an enemy wave spawn event.
        /// Environment vars: LEVEL_NUMBER, EVENT_TIME, ENEMY_TYPE, ENEMY_COUNT, SPAWN_X, SPAWN_Y
        /// </summary>
        [MenuItem("Tools/Claw/Add Enemy Wave Event To Level")]
        public static void AddEnemyWaveEventToLevel()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int levelNum = int.TryParse(System.Environment.GetEnvironmentVariable("LEVEL_NUMBER"), out var ln) ? ln : 1;
            float time = float.TryParse(System.Environment.GetEnvironmentVariable("EVENT_TIME"), out var t) ? t : 0f;
            string typeStr = System.Environment.GetEnvironmentVariable("ENEMY_TYPE") ?? "Chaser";
            int count = int.TryParse(System.Environment.GetEnvironmentVariable("ENEMY_COUNT"), out var c) ? c : 1;
            float x = float.TryParse(System.Environment.GetEnvironmentVariable("SPAWN_X"), out var sx) ? sx : 5f;
            float y = float.TryParse(System.Environment.GetEnvironmentVariable("SPAWN_Y"), out var sy) ? sy : 0f;
            string label = System.Environment.GetEnvironmentVariable("EVENT_LABEL") ?? "";

            if (!System.Enum.TryParse<EnemyType>(typeStr, true, out var enemyType))
            {
                Debug.LogError($"[ClawTools] Invalid ENEMY_TYPE: {typeStr}");
                return;
            }

            var level = gd.GetLevel(levelNum);
            if (level == null) return;

            if (level.timeline == null)
                level.timeline = new TimelineConfig();

            level.timeline.events.Add(new GameEvent
            {
                time = time,
                type = GameEventType.SpawnEnemyWave,
                enemyType = enemyType,
                enemyCount = count,
                spawnPosition = new Vector2(x, y),
                label = label
            });

            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Added {count}x {enemyType} wave to Level {levelNum} at t={time}s ({x},{y})");
        }

        /// <summary>
        /// Clear all experience events from a level.
        /// Environment var: LEVEL_NUMBER
        /// </summary>
        [MenuItem("Tools/Claw/Clear Level Events")]
        public static void ClearLevelEvents()
        {
            var gd = LoadGameDefinition();
            if (gd == null) return;

            int levelNum = int.TryParse(System.Environment.GetEnvironmentVariable("LEVEL_NUMBER"), out var ln) ? ln : 1;
            var level = gd.GetLevel(levelNum);
            if (level == null) return;

            int count = level.timeline?.events?.Count ?? 0;
            if (level.timeline != null)
                level.timeline.events.Clear();

            SaveAndRefresh(gd);
            Debug.Log($"[ClawTools] Cleared {count} events from Level {levelNum}");
        }

        // =====================================================================
        // BUILD TOOLS
        // =====================================================================

        [MenuItem("Tools/Claw/Build All Scenes")]
        public static void BuildAllScenes()
        {
            Generation.GameBuilder.BuildAllScenes();
        }

        [MenuItem("Tools/Claw/Build Level Scenes")]
        public static void BuildLevelScenes()
        {
            Generation.GameBuilder.BuildLevelScenesOnly();
        }

        [MenuItem("Tools/Claw/Build Video Scenes")]
        public static void BuildVideoScenes()
        {
            Generation.GameBuilder.BuildVideoScenesOnly();
        }

        [MenuItem("Tools/Claw/Build Death Scenes")]
        public static void BuildDeathScenes()
        {
            Generation.GameBuilder.BuildDeathScenesOnly();
        }

        // =====================================================================
        // ENEMY PREFAB GENERATION
        // =====================================================================

        /// <summary>
        /// Generate an enemy prefab from a sprite sheet.
        /// Set environment variables before calling:
        ///   ENEMY_NAME, ENEMY_TYPE (Chaser/Fighter/Sentinel/Shooter), SPRITE_SHEET_PATH
        ///   Optional: ENEMY_HEALTH, ENEMY_SPEED, ENEMY_DAMAGE, ENEMY_FPS
        /// </summary>
        [MenuItem("Tools/Claw/Generate Enemy From Sprite Sheet")]
        public static void GenerateEnemyFromSpriteSheet()
        {
            string name = System.Environment.GetEnvironmentVariable("ENEMY_NAME") ?? "NewEnemy";
            string typeStr = System.Environment.GetEnvironmentVariable("ENEMY_TYPE") ?? "Chaser";
            string sheetPath = System.Environment.GetEnvironmentVariable("SPRITE_SHEET_PATH") ?? "";
            int health = int.TryParse(System.Environment.GetEnvironmentVariable("ENEMY_HEALTH"), out var h) ? h : 3;
            float speed = float.TryParse(System.Environment.GetEnvironmentVariable("ENEMY_SPEED"), out var s) ? s : 2f;
            int damage = int.TryParse(System.Environment.GetEnvironmentVariable("ENEMY_DAMAGE"), out var d) ? d : 1;
            int fps = int.TryParse(System.Environment.GetEnvironmentVariable("ENEMY_FPS"), out var f) ? f : 8;

            if (string.IsNullOrEmpty(sheetPath))
            {
                Debug.LogError("[ClawGameTools] SPRITE_SHEET_PATH not set!");
                return;
            }

            if (!System.Enum.TryParse<EnemyType>(typeStr, true, out var enemyType))
            {
                Debug.LogError($"[ClawGameTools] Invalid ENEMY_TYPE: {typeStr}");
                return;
            }

            var (prefab, template) = Generation.EnemyPrefabGenerator.GenerateWithTemplate(
                name, enemyType, sheetPath, health, speed, damage, fps);

            if (prefab != null && template != null)
            {
                // Auto-add to GameDefinition
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameDefinition");
                if (guids.Length > 0)
                {
                    var gd = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDefinition>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                    if (gd != null)
                    {
                        gd.enemyTemplates.Add(template);
                        UnityEditor.EditorUtility.SetDirty(gd);
                        UnityEditor.AssetDatabase.SaveAssets();
                        Debug.Log($"[ClawGameTools] ✅ Added {name} to GameDefinition.enemyTemplates");
                    }
                }
            }
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
