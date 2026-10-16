#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SickOfIt.Combat.Health;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Playtest;
using Hawkes.Playtest;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>
    /// Plays every level in the real Level scene with the playtest bot (the Hawkes playtest's "level-N" scenario,
    /// shared with WebGL builds; see SickOfIt.Playtest) and writes what happened to
    /// Logs/Playtest/report.md (plus a summary and event log per level). Excluded from normal runs:
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testCategory Playtest -testResults Logs/Playtest/results.xml
    /// A test fails only if the level throws errors or never starts; whether the bot wins is reported, not asserted.
    /// </summary>
    [Category("Playtest")]
    public class LevelPlaytests
    {
        const string ReportPath = "Logs/Playtest/report.md";
        const float SpeedUp = 2f;
        static readonly List<string> Rows = new List<string>();

        readonly List<string> errors = new List<string>();

        // Saved player state that a run changes: upgrades, max health, and health carried between levels.
        static readonly List<(string key, string text, int number, bool isText)> savedPrefs = new List<(string, string, int, bool)>();

        static IEnumerable<(string key, bool isText)> RunKeys()
        {
            yield return ("GameplayUpgrades", true);
            yield return ("Rogue_MaxHealth", false);
            foreach (int level in Levels())
            {
                yield return ($"Rogue_Level_{level}_EndHealth", false);
                yield return ($"Rogue_Level_{level}_MaxHealth", false);
            }
        }

        /// <summary>Play the levels as one fresh run (full health, no upgrades; picks carry over). The player's own save is restored afterwards.</summary>
        [OneTimeSetUp]
        public void StartRun()
        {
            savedPrefs.Clear();
            foreach (var (key, isText) in RunKeys())
            {
                if (PlayerPrefs.HasKey(key))
                    savedPrefs.Add((key, isText ? PlayerPrefs.GetString(key) : null, isText ? 0 : PlayerPrefs.GetInt(key), isText));
                PlayerPrefs.DeleteKey(key);
            }
            Rows.Clear();
        }

        [OneTimeTearDown]
        public void EndRun()
        {
            foreach (var (key, _) in RunKeys())
                PlayerPrefs.DeleteKey(key);
            foreach (var (key, text, number, isText) in savedPrefs)
            {
                if (isText)
                    PlayerPrefs.SetString(key, text);
                else
                    PlayerPrefs.SetInt(key, number);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Leave nothing of the level behind: its scene (clock, rewind timeline) would keep ticking into whichever
        /// test fixture runs next.
        /// </summary>
        // Batch mode never focuses the Game view, and by default the editor drops keyboard and mouse input that
        // isn't aimed at it, so the bot's virtual keyboard would go unheard. Let all input through while testing.
        InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
        InputSettings.BackgroundBehavior savedBackground;

        [SetUp]
        public void LetInputThrough()
        {
            savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        }

        [TearDown]
        public void RestoreInput()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
            InputSystem.settings.backgroundBehavior = savedBackground;
        }

        [UnityTearDown]
        public IEnumerator LeaveLevel()
        {
            Time.timeScale = 1f;
            var empty = SceneManager.CreateScene("AfterPlaytest" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add($"{type}: {message.Split('\n')[0]}");
        }

        public static IEnumerable<int> Levels()
        {
            var definition = GameDefinition.Instance;
            return Enumerable.Range(1, definition != null ? definition.levels.Count : 0);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator PlayLevel([ValueSource(nameof(Levels))] int levelNumber)
        {
            LogAssert.ignoreFailingMessages = true;
            errors.Clear();
            Application.logMessageReceived += OnLog;
            try
            {
                var config = GameDefinition.Instance.GetLevel(levelNumber);
                GameplayTelemetry.WriteFiles = true;
                GameplayTelemetry.Folder = "Logs/Playtest";

                // The same scenario the WebGL build runs for the Hawkes playtest ("level-N"), with the editor's
                // scene loader as its setup shortcut. The bot plays through virtual input devices.
                SickOfItPlaytest.Register();
                SickOfItPlaytest.LoadLevel = LoadLevelLikeTheBuild;
                GameplayTelemetry.Summary s = null;
                void Keep(GameplayTelemetry log, GameplayTelemetry.Summary summary) => s = s ?? summary;
                GameplayTelemetry.Finished += Keep;
                PlaytestRun run;
                try
                {
                    run = HawkesPlaytest.StartScenario("level-" + levelNumber, new PlaytestOptions { seed = 1, speed = SpeedUp });
                    while (!run.Done)
                        yield return null;
                }
                finally
                {
                    GameplayTelemetry.Finished -= Keep;
                }
                File.WriteAllText($"Logs/Playtest/level-{levelNumber}.report.json", HawkesPlaytest.ReportJson(run));

                Assert.That(s, Is.Not.Null, $"Level {levelNumber} never started its gameplay world ({run.Outcome}). Errors:\n{string.Join("\n", errors)}");
                string outcome = SickOfItPlaytest.LastLevelOutcome ?? run.Outcome.ToString().ToLowerInvariant();
                var world = GameplayWorld.Instance;
                string upgrades = string.Join(" ", UpgradeProgress.Owned().Select(p => $"{p.Key}:{p.Value}"));
                Rows.Add($"| {levelNumber} | {config.levelName} | {outcome} | {s?.duration:0.0} / {world?.LevelLength:0} | {s?.kills}/{s?.spawned} | " +
                         $"{s?.playerHealthEnd}/{s?.playerMaxHealth} | {Damage(s)} | {s?.lastPhase} | {Floor(s)} | {upgrades} | {errors.Count} |");
                WriteReport();
                Assert.That(errors, Is.Empty, $"Level {levelNumber} logged errors:\n{string.Join("\n", errors.Take(20))}");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                HawkesPlaytest.Stop();
                Time.timeScale = 1f;
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>
        /// Opens a level the way the build's loader (AddressablesSceneLoader) does: additively, while the previous
        /// scene and its own MainCamera are still loaded; that scene is unloaded and the level's content shown once
        /// the load is done. A single-scene load hid code that grabbed the outgoing scene's camera.
        /// </summary>
        static AsyncOperation LoadLevelLikeTheBuild(int n)
        {
            PlayerPrefs.SetInt("CurrentLevelNumber", n);
            var previous = SceneManager.CreateScene("PreviousScene" + Time.frameCount);
            var camera = new GameObject("PreviousCamera") { tag = "MainCamera" };
            camera.AddComponent<Camera>().orthographic = true;
            SceneManager.MoveGameObjectToScene(camera, previous);
            SceneManager.SetActiveScene(previous);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != previous && scene.isLoaded)
                    SceneManager.UnloadSceneAsync(scene);
            }

            const string levelPath = "Assets/Scenes/Level.unity";
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(levelPath, new LoadSceneParameters(LoadSceneMode.Additive));
            load.completed += _ =>
            {
                var level = SceneManager.GetSceneByPath(levelPath);
                if (level.IsValid() && level.isLoaded)
                {
                    SceneManager.SetActiveScene(level);
                    foreach (var root in level.GetRootGameObjects())
                    {
                        var content = root.GetComponentInChildren<SickOfIt.Core.Flow.SceneContentController>(true);
                        if (content != null)
                        {
                            content.ActivateScene();
                            break;
                        }
                    }
                }
                SceneManager.UnloadSceneAsync(previous);
            };
            return load;
        }

        static string Floor(GameplayTelemetry.Summary s)
        {
            if (s == null || (s.floorEnd == 0 && s.distance <= 0f))
                return "-";
            return s.floorEnd > 0 ? $"{s.floorEnd} ({s.distance:0} u)" : $"{s.distance:0} u";
        }

        static string Damage(GameplayTelemetry.Summary s)
        {
            if (s == null || s.damageTakenBySource.Count == 0)
                return "-";
            return string.Join(", ", s.damageTakenBySource.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}"));
        }

        static void WriteReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Playtest report");
            sb.AppendLine();
            sb.AppendLine($"Bot playtests at {SpeedUp}x speed. Per-level event logs and summaries are next to this file.");
            sb.AppendLine();
            sb.AppendLine("| # | Level | Outcome | Time / length (s) | Kills/Spawned | Player HP | Damage taken by source | Last phase | Floor reached | Upgrades after | Errors |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var row in Rows)
                sb.AppendLine(row);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
        }
    }
}
#endif
