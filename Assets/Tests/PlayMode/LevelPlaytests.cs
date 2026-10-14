#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SickOfIt.Combat.Health;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>
    /// Plays every level in the real Level scene with the playtest bot and writes what happened to
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
                PlayerPrefs.SetInt("CurrentLevelNumber", levelNumber);

                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));

                float started = Time.realtimeSinceStartup;
                while ((GameplayWorld.Instance == null || SickOfIt.Core.GameClock.Instance == null || SickOfIt.Core.GameClock.Instance.Time <= 0f)
                       && Time.realtimeSinceStartup - started < 40f)
                    yield return null;
                var world = GameplayWorld.Instance;
                Assert.That(world, Is.Not.Null, $"Level {levelNumber} never started its gameplay world. Errors:\n{string.Join("\n", errors)}");

                PlaytestBot.Attach(world, (uint)(1000 + levelNumber));
                Time.timeScale = SpeedUp;

                var health = world.Player != null ? world.Player.GetComponentInChildren<PlayerHealth>() : null;
                float limit = Mathf.Max(30f, config.duration) + 10f;
                string outcome = "timeout";
                while (Time.realtimeSinceStartup - started < limit / SpeedUp + 60f)
                {
                    if (world == null) { outcome = "scene changed"; break; }
                    if (world.Director.Completed) { outcome = "completed"; break; }
                    if (health != null && !health.IsAlive) { outcome = "died"; break; }
                    if (SickOfIt.Core.GameClock.Instance != null && SickOfIt.Core.GameClock.Instance.Time > limit) break;
                    yield return null;
                }

                Time.timeScale = 1f;
                world?.Telemetry.Finish(outcome);
                var s = GameplayTelemetry.LastSummary;
                string upgrades = string.Join(" ", UpgradeProgress.Owned().Select(p => $"{p.Key}:{p.Value}"));
                Rows.Add($"| {levelNumber} | {config.levelName} | {outcome} | {s?.duration:0.0} / {world?.LevelLength:0} | {s?.kills}/{s?.spawned} | " +
                         $"{s?.playerHealthEnd}/{s?.playerMaxHealth} | {Damage(s)} | {s?.lastPhase} | {upgrades} | {errors.Count} |");
                WriteReport();
                Assert.That(errors, Is.Empty, $"Level {levelNumber} logged errors:\n{string.Join("\n", errors.Take(20))}");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Time.timeScale = 1f;
                LogAssert.ignoreFailingMessages = false;
            }
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
            sb.AppendLine("| # | Level | Outcome | Time / length (s) | Kills/Spawned | Player HP | Damage taken by source | Last phase | Upgrades after | Errors |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var row in Rows)
                sb.AppendLine(row);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
        }
    }
}
#endif
