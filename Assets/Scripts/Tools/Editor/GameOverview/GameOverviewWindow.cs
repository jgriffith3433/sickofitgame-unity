#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.GameOverview
{
    /// <summary>
    /// The master game development window.
    /// Shows the entire game at a glance — levels, videos, deaths, enemies, player.
    /// Click to drill down. This IS the game editor.
    /// </summary>
    public class GameOverviewWindow : EditorWindow
    {
        private GameDefinition gameDefinition;
        private SerializedObject serializedGameDef;
        private Vector2 scrollPosition;
        private int selectedTab;
        private int selectedLevelIndex = -1;
        private int selectedEnemyIndex = -1;
        private List<string> validationIssues = new List<string>();

        private static readonly string[] TabNames = { "Overview", "Levels", "Videos", "Deaths", "Enemies", "Player", "Validation" };
        private static readonly Color HeaderColor = new Color(0.2f, 0.6f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.8f, 0.2f);
        private static readonly Color ErrorColor = new Color(1f, 0.3f, 0.3f);
        private static readonly Color SuccessColor = new Color(0.3f, 1f, 0.3f);

        [MenuItem("Tools/Sick Of It/Game Overview %#g", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<GameOverviewWindow>("Sick Of It — Game Overview");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            FindGameDefinition();
        }

        private void FindGameDefinition()
        {
            // Try to find existing GameDefinition asset
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                gameDefinition = AssetDatabase.LoadAssetAtPath<GameDefinition>(path);
                if (gameDefinition != null)
                    serializedGameDef = new SerializedObject(gameDefinition);
            }
        }

        private void OnGUI()
        {
            if (gameDefinition == null)
            {
                DrawNoDefinition();
                return;
            }

            // Refresh serialized object
            serializedGameDef.Update();

            // Header
            DrawHeader();

            // Tab bar
            selectedTab = GUILayout.Toolbar(selectedTab, TabNames, GUILayout.Height(30));
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawLevelsTab(); break;
                case 2: DrawVideosTab(); break;
                case 3: DrawDeathsTab(); break;
                case 4: DrawEnemiesTab(); break;
                case 5: DrawPlayerTab(); break;
                case 6: DrawValidationTab(); break;
            }

            EditorGUILayout.EndScrollView();

            serializedGameDef.ApplyModifiedProperties();
        }

        // =========================================================================
        // NO DEFINITION
        // =========================================================================

        private void DrawNoDefinition()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("No Game Definition Found", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Create a GameDefinition asset to define your game.\n\n" +
                "This is THE source of truth — every level, enemy, video, and death scene.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Create Game Definition", GUILayout.Height(40)))
            {
                var asset = CreateInstance<GameDefinition>();
                string dir = "Assets/Resources";
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateAsset(asset, $"{dir}/GameDefinition.asset");
                AssetDatabase.SaveAssets();
                gameDefinition = asset;
                serializedGameDef = new SerializedObject(gameDefinition);
                EditorGUIUtility.PingObject(asset);
            }
        }

        // =========================================================================
        // HEADER
        // =========================================================================

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUI.color = HeaderColor;
            EditorGUILayout.LabelField("SICK OF IT", style, GUILayout.Width(100));
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(
                $"{gameDefinition.TotalLevels} Levels | {gameDefinition.TotalVideos} Videos | {gameDefinition.TotalDeaths} Deaths | {gameDefinition.enemyTemplates.Count} Enemy Types",
                EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
            {
                validationIssues = gameDefinition.Validate();
                if (validationIssues.Count == 0)
                    Debug.Log("[GameOverview] ✅ Validation passed — no issues found");
                else
                    Debug.LogWarning($"[GameOverview] ⚠️ {validationIssues.Count} issue(s) found");
                selectedTab = 6;
            }

            if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton))
            {
                Selection.activeObject = gameDefinition;
                EditorGUIUtility.PingObject(gameDefinition);
            }

            EditorGUILayout.EndHorizontal();
        }

        // =========================================================================
        // OVERVIEW TAB
        // =========================================================================

        private void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Game At A Glance", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Level cards
            EditorGUILayout.LabelField("LEVELS", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < gameDefinition.levels.Count; i++)
            {
                var level = gameDefinition.levels[i];
                DrawLevelCard(level, i);
            }
            if (GUILayout.Button("+ Add\nLevel", GUILayout.Width(80), GUILayout.Height(80)))
            {
                AddNewLevel();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);

            // Videos row
            EditorGUILayout.LabelField("VIDEOS", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < gameDefinition.videos.Count; i++)
            {
                var video = gameDefinition.videos[i];
                if (GUILayout.Button($"Video {video.videoNumber}\n{video.videoName}", GUILayout.Width(100), GUILayout.Height(50)))
                {
                    selectedTab = 2;
                }
            }
            if (GUILayout.Button("+ Add", GUILayout.Width(60), GUILayout.Height(50)))
            {
                AddNewVideo();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);

            // Deaths + Enemies side by side
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("DEATHS", EditorStyles.boldLabel);
            for (int i = 0; i < gameDefinition.deaths.Count; i++)
            {
                EditorGUILayout.LabelField($"  {gameDefinition.deaths[i].deathName}");
            }
            if (GUILayout.Button("+ Add Death", GUILayout.Width(100)))
            {
                AddNewDeath();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("ENEMY TYPES", EditorStyles.boldLabel);
            for (int i = 0; i < gameDefinition.enemyTemplates.Count; i++)
            {
                var template = gameDefinition.enemyTemplates[i];
                if (template != null)
                    EditorGUILayout.LabelField($"  {template.displayName} ({template.type})");
            }
            if (GUILayout.Button("+ Add Enemy Type", GUILayout.Width(130)))
            {
                selectedTab = 4;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Player summary
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("PLAYER", EditorStyles.boldLabel);
            var p = gameDefinition.player;
            EditorGUILayout.LabelField($"  HP: {p.baseMaxHealth} | Speed: {p.moveSpeed} | Jump: {p.jumpForce} | Shooting: {(p.enableShooting ? "Yes" : "No")}");
        }

        private void DrawLevelCard(LevelConfig level, int index)
        {
            var bgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.4f);

            EditorGUILayout.BeginVertical("box", GUILayout.Width(120), GUILayout.Height(80));
            GUI.backgroundColor = bgColor;

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField($"Level {level.levelNumber}", titleStyle);
            EditorGUILayout.LabelField($"{level.EnemyCount} enemies", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{level.duration}s", EditorStyles.centeredGreyMiniLabel);

            if (GUILayout.Button("Edit", GUILayout.Height(20)))
            {
                selectedTab = 1;
                selectedLevelIndex = index;
            }

            EditorGUILayout.EndVertical();
        }

        // =========================================================================
        // LEVELS TAB
        // =========================================================================

        private void DrawLevelsTab()
        {
            if (selectedLevelIndex >= 0 && selectedLevelIndex < gameDefinition.levels.Count)
            {
                DrawLevelEditor(selectedLevelIndex);
                return;
            }

            EditorGUILayout.LabelField("All Levels", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            for (int i = 0; i < gameDefinition.levels.Count; i++)
            {
                var level = gameDefinition.levels[i];
                EditorGUILayout.BeginHorizontal("box");

                EditorGUILayout.LabelField($"Level {level.levelNumber}: {level.displayName}", EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField($"{level.EnemyCount} enemies", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{level.duration}s", GUILayout.Width(50));
                EditorGUILayout.LabelField(level.music != null ? level.music.name : "(no music)", GUILayout.Width(150));

                if (GUILayout.Button("Edit", GUILayout.Width(60)))
                {
                    selectedLevelIndex = i;
                }
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    if (EditorUtility.DisplayDialog("Delete Level", $"Delete Level {level.levelNumber}?", "Delete", "Cancel"))
                    {
                        Undo.RecordObject(gameDefinition, "Delete Level");
                        gameDefinition.levels.RemoveAt(i);
                        EditorUtility.SetDirty(gameDefinition);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("+ Add Level", GUILayout.Height(30)))
            {
                AddNewLevel();
            }
        }

        private void DrawLevelEditor(int index)
        {
            var level = gameDefinition.levels[index];

            // Back button
            if (GUILayout.Button("← Back to Level List", GUILayout.Width(150)))
            {
                selectedLevelIndex = -1;
                return;
            }

            EditorGUILayout.Space(5);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            EditorGUILayout.LabelField($"Level {level.levelNumber} — {level.displayName}", titleStyle);
            EditorGUILayout.Space(10);

            // Use SerializedProperty for undo support
            var levelsProp = serializedGameDef.FindProperty("levels");
            var levelProp = levelsProp.GetArrayElementAtIndex(index);

            // Identity
            EditorGUILayout.LabelField("IDENTITY", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("levelName"));
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("displayName"));
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("levelNumber"));

            EditorGUILayout.Space(10);

            // Visuals
            EditorGUILayout.LabelField("VISUALS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("backgrounds"), true);
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("ambientColor"));

            EditorGUILayout.Space(10);

            // Audio
            EditorGUILayout.LabelField("AUDIO", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("music"));
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("musicStartTime"));
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("musicVolume"));

            EditorGUILayout.Space(10);

            // Flow
            EditorGUILayout.LabelField("FLOW", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("deathConfigName"));
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("duration"));
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("hasTwoStageSequence"));
            if (level.hasTwoStageSequence)
                EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("firstSequenceDuration"));
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("nextVideoNumber"));

            EditorGUILayout.Space(10);

            // Enemies
            EditorGUILayout.LabelField($"ENEMIES ({level.EnemyCount})", EditorStyles.boldLabel);
            var enemiesProp = levelProp.FindPropertyRelative("enemies");

            for (int i = 0; i < level.enemies.Count; i++)
            {
                var enemy = level.enemies[i];
                var enemyProp = enemiesProp.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"#{i + 1} {enemy.type}", EditorStyles.boldLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField($"@ ({enemy.position.x:F1}, {enemy.position.y:F1})", GUILayout.Width(100));

                if (i > 0 && GUILayout.Button("↑", GUILayout.Width(25)))
                {
                    Undo.RecordObject(gameDefinition, "Move Enemy Up");
                    (gameDefinition.levels[index].enemies[i], gameDefinition.levels[index].enemies[i - 1]) =
                        (gameDefinition.levels[index].enemies[i - 1], gameDefinition.levels[index].enemies[i]);
                    EditorUtility.SetDirty(gameDefinition);
                }
                if (i < level.enemies.Count - 1 && GUILayout.Button("↓", GUILayout.Width(25)))
                {
                    Undo.RecordObject(gameDefinition, "Move Enemy Down");
                    (gameDefinition.levels[index].enemies[i], gameDefinition.levels[index].enemies[i + 1]) =
                        (gameDefinition.levels[index].enemies[i + 1], gameDefinition.levels[index].enemies[i]);
                    EditorUtility.SetDirty(gameDefinition);
                }
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    Undo.RecordObject(gameDefinition, "Remove Enemy");
                    gameDefinition.levels[index].enemies.RemoveAt(i);
                    EditorUtility.SetDirty(gameDefinition);
                    break;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("type"));
                EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("position"));
                EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("spawnTime"));
                EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("activeAtStart"));

                // Show overrides
                EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("healthOverride"));
                EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("speedOverride"));

                if (enemy.type == EnemyType.Shooter)
                {
                    EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("fireRateOverride"));
                    EditorGUILayout.PropertyField(enemyProp.FindPropertyRelative("shootDirection"));
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (GUILayout.Button("+ Add Enemy", GUILayout.Height(25)))
            {
                Undo.RecordObject(gameDefinition, "Add Enemy");
                gameDefinition.levels[index].enemies.Add(new EnemySpawnConfig
                {
                    type = EnemyType.Chaser,
                    activeAtStart = true
                });
                EditorUtility.SetDirty(gameDefinition);
            }

            EditorGUILayout.Space(10);

            // Platforms
            EditorGUILayout.LabelField($"PLATFORMS ({level.platforms.Count})", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("platforms"), true);

            EditorGUILayout.Space(10);

            // Timeline
            EditorGUILayout.LabelField("TIMELINE", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelProp.FindPropertyRelative("timeline"), true);
        }

        // =========================================================================
        // VIDEOS TAB
        // =========================================================================

        private void DrawVideosTab()
        {
            EditorGUILayout.LabelField("Videos / Cutscenes", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var videosProp = serializedGameDef.FindProperty("videos");
            for (int i = 0; i < gameDefinition.videos.Count; i++)
            {
                var video = gameDefinition.videos[i];
                var videoProp = videosProp.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Video {video.videoNumber}: {video.videoName}", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("videoName"));
                EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("videoNumber"));
                EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("displayName"));
                EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("spriteFramesData"));
                EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("audio"));
                EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("afterLevel"));
                EditorGUILayout.PropertyField(videoProp.FindPropertyRelative("nextLevel"));

                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("Delete Video", $"Delete {video.videoName}?", "Delete", "Cancel"))
                    {
                        Undo.RecordObject(gameDefinition, "Delete Video");
                        gameDefinition.videos.RemoveAt(i);
                        EditorUtility.SetDirty(gameDefinition);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("+ Add Video", GUILayout.Height(30)))
            {
                AddNewVideo();
            }
        }

        // =========================================================================
        // DEATHS TAB
        // =========================================================================

        private void DrawDeathsTab()
        {
            EditorGUILayout.LabelField("Death Scenes", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var deathsProp = serializedGameDef.FindProperty("deaths");
            for (int i = 0; i < gameDefinition.deaths.Count; i++)
            {
                var death = gameDefinition.deaths[i];
                var deathProp = deathsProp.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(death.deathName, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(deathProp.FindPropertyRelative("deathName"));
                EditorGUILayout.PropertyField(deathProp.FindPropertyRelative("displayName"));
                EditorGUILayout.PropertyField(deathProp.FindPropertyRelative("spriteFramesData"));
                EditorGUILayout.PropertyField(deathProp.FindPropertyRelative("audio"));

                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("Delete Death", $"Delete {death.deathName}?", "Delete", "Cancel"))
                    {
                        Undo.RecordObject(gameDefinition, "Delete Death");
                        gameDefinition.deaths.RemoveAt(i);
                        EditorUtility.SetDirty(gameDefinition);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("+ Add Death Scene", GUILayout.Height(30)))
            {
                AddNewDeath();
            }
        }

        // =========================================================================
        // ENEMIES TAB
        // =========================================================================

        private void DrawEnemiesTab()
        {
            EditorGUILayout.LabelField("Enemy Templates", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each template defines an enemy type. Levels reference these by type.\n" +
                "Change a template's stats and ALL levels using that type are affected.",
                MessageType.Info);
            EditorGUILayout.Space(5);

            var templatesProp = serializedGameDef.FindProperty("enemyTemplates");
            for (int i = 0; i < gameDefinition.enemyTemplates.Count; i++)
            {
                var template = gameDefinition.enemyTemplates[i];
                if (template == null) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{template.displayName} ({template.type})", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                var templateObj = new SerializedObject(template);
                templateObj.Update();

                EditorGUILayout.PropertyField(templateObj.FindProperty("displayName"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("type"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("prefab"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("defaultHealth"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("defaultMoveSpeed"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("contactDamage"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("detectionRange"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("dropChance"));
                EditorGUILayout.PropertyField(templateObj.FindProperty("dropPointsValue"));

                if (template.type == EnemyType.Shooter)
                {
                    EditorGUILayout.PropertyField(templateObj.FindProperty("fireRate"));
                    EditorGUILayout.PropertyField(templateObj.FindProperty("projectileSpeed"));
                }
                if (template.type == EnemyType.Fighter || template.type == EnemyType.Sentinel)
                {
                    EditorGUILayout.PropertyField(templateObj.FindProperty("attackRange"));
                    EditorGUILayout.PropertyField(templateObj.FindProperty("attackCooldown"));
                }

                templateObj.ApplyModifiedProperties();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Add Enemy Template", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Enemy Template", GUILayout.Height(30)))
            {
                var template = CreateInstance<EnemyTemplate>();
                template.displayName = "New Enemy";
                template.type = EnemyType.Chaser;

                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Enemy Template", "NewEnemy", "asset",
                    "Save the enemy template", "Assets/Resources");

                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(template, path);
                    AssetDatabase.SaveAssets();

                    Undo.RecordObject(gameDefinition, "Add Enemy Template");
                    gameDefinition.enemyTemplates.Add(template);
                    EditorUtility.SetDirty(gameDefinition);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // =========================================================================
        // PLAYER TAB
        // =========================================================================

        private void DrawPlayerTab()
        {
            EditorGUILayout.LabelField("Player Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var playerProp = serializedGameDef.FindProperty("player");

            EditorGUILayout.LabelField("MOVEMENT", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("moveSpeed"));
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("jumpForce"));
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("gravityScale"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("COMBAT", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("enableShooting"));
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("fireRate"));
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("projectileSpeed"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("ROGUELITE", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("baseMaxHealth"));
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("maxExtraJumps"));
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("extraJumpCost"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UPGRADES", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playerProp.FindPropertyRelative("availableUpgrades"), true);
        }

        // =========================================================================
        // VALIDATION TAB
        // =========================================================================

        private void DrawValidationTab()
        {
            EditorGUILayout.LabelField("Game Validation", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (GUILayout.Button("Run Validation", GUILayout.Height(30)))
            {
                validationIssues = gameDefinition.Validate();
            }

            EditorGUILayout.Space(10);

            if (validationIssues.Count == 0)
            {
                GUI.color = SuccessColor;
                EditorGUILayout.LabelField("✅ No issues found!", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = WarningColor;
                EditorGUILayout.LabelField($"⚠️ {validationIssues.Count} issue(s) found:", EditorStyles.boldLabel);
                GUI.color = Color.white;

                for (int i = 0; i < validationIssues.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    GUI.color = ErrorColor;
                    EditorGUILayout.LabelField("●", GUILayout.Width(15));
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField(validationIssues[i]);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private void AddNewLevel()
        {
            Undo.RecordObject(gameDefinition, "Add Level");
            int num = gameDefinition.levels.Count + 1;
            gameDefinition.levels.Add(new LevelConfig
            {
                levelName = $"Level{num}",
                displayName = $"Level {num}",
                levelNumber = num,
                duration = gameDefinition.defaultLevelDuration,
                deathConfigName = "Death1"
            });
            EditorUtility.SetDirty(gameDefinition);
        }

        private void AddNewVideo()
        {
            Undo.RecordObject(gameDefinition, "Add Video");
            int num = gameDefinition.videos.Count + 1;
            gameDefinition.videos.Add(new VideoConfig
            {
                videoName = $"Video{num}",
                videoNumber = num,
                displayName = $"Video {num}"
            });
            EditorUtility.SetDirty(gameDefinition);
        }

        private void AddNewDeath()
        {
            Undo.RecordObject(gameDefinition, "Add Death");
            int num = gameDefinition.deaths.Count + 1;
            gameDefinition.deaths.Add(new DeathConfig
            {
                deathName = $"Death{num}",
                displayName = $"Death {num}"
            });
            EditorUtility.SetDirty(gameDefinition);
        }
    }
}
#endif
