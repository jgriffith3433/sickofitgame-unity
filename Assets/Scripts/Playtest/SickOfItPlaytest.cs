using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using Object = UnityEngine.Object;
using PlaytestBot = SickOfIt.Gameplay.PlaytestBot;

namespace SickOfIt.Playtest
{
    /// <summary>
    /// Sick Of It's side of the Hawkes playtest contract v1 (C:/Projects/hawkesstudio/docs/playtest.md).
    /// Registers the scenarios at startup in every build (they cost nothing until one starts) and plays them with
    /// <see cref="PlaytestBot"/> through virtual devices (<see cref="PlaytestInput"/>): keys, sticks and mouse clicks
    /// on the real menus. In a WebGL build the browser starts them (window.hawkesPlaytest, or ?playtest=smoke);
    /// in the editor and the PlayMode playtests, call <c>HawkesPlaytest.StartScenario("level-3")</c>.
    ///
    /// Scenarios: smoke (menu, New Game, level 1 for ~30 s, rewind, pause), level-N (starts level N directly,
    /// a setup shortcut), full (New Game and every level in order through the real flow, ~10 min), menus (level
    /// select, videos, shop and settings open and close).
    /// </summary>
    public static class SickOfItPlaytest
    {
        public const string Product = "sickofit";

        /// <summary>
        /// The setup shortcut level-N scenarios (and the full run's fallback) use to open a level. Returns the load
        /// operation, or null. The PlayMode playtests replace it with the editor's scene loader.
        /// </summary>
        public static Func<int, AsyncOperation> LoadLevel = DefaultLoadLevel;

        static AsyncOperation DefaultLoadLevel(int number)
        {
            SceneTransitionManager.Instance.LoadLevel(number);
            return null;
        }

        /// <summary>Real seconds the game clock may stand still mid-level (not paused) before it counts as a softlock.</summary>
        public static float SoftlockSeconds = 15f;

        /// <summary>The level-N scenario's last result, for the PlayMode playtest report.</summary>
        public static string LastLevelOutcome { get; private set; }

        static Session active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Register()
        {
            HawkesPlaytest.Register(Product, Scenarios(), Bot, IsReady, StopActive);
            HawkesPlaytest.Finished -= OnFinished;
            HawkesPlaytest.Finished += OnFinished;
        }

        public static PlaytestScenario[] Scenarios()
        {
            var list = new List<PlaytestScenario>
            {
                new PlaytestScenario("smoke", "Boot, main menu, New Game, level 1 for 30 s with a rewind and a pause", 55, "smoke"),
            };
            var definition = GameDefinition.Instance;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
            {
                var level = definition.levels[n - 1];
                string name = level != null && !string.IsNullOrEmpty(level.displayName) ? level.displayName : level?.levelName;
                float seconds = Mathf.Max(30f, level != null ? level.duration : 60f) + 30f;
                list.Add(new PlaytestScenario("level-" + n, $"Level {n} ({name}), started directly (setup shortcut)", seconds, "full", "level"));
            }
            list.Add(new PlaytestScenario("full", "New Game, then every level in order through the real flow, as far as the bot gets", 600, "full"));
            list.Add(new PlaytestScenario("menus", "Level select, videos, shop and settings each open and close", 60, "menus"));
            return list.ToArray();
        }

        static bool IsReady()
        {
            if (GameDefinition.Instance == null)
                return false;
            // A test that loads levels itself starts from an empty scene.
            if (LoadLevel != DefaultLoadLevel)
                return true;
            return Object.FindAnyObjectByType<MainMenuManager>() != null || GameplayWorld.Instance != null
                   || Object.FindAnyObjectByType<MenuCanvas>() != null;
        }

        static void StopActive()
        {
            active?.Dispose();
            active = null;
        }

        static void OnFinished(PlaytestRun run)
        {
            if (active != null && active.Run == run)
            {
                active.Close();
                StopActive();
            }
        }

        static IEnumerator Bot(string id, PlaytestOptions options, PlaytestRun run)
        {
            StopActive();
            var session = active = new Session(run, options);
            run.Note("waiting for the game to load");
            yield return run.Until(IsReady, 90f, "the game to load");
            if (run.Done)
                yield break;
            session.CheckContent();

            if (id == "smoke")
                yield return session.Smoke();
            else if (id == "full")
                yield return session.Full();
            else if (id == "menus")
                yield return session.Menus();
            else if (id.StartsWith("level-", StringComparison.Ordinal) && int.TryParse(id.Substring(6), out int n))
                yield return session.LevelScenario(n);
            else
            {
                run.Problem(PlaytestSeverity.Bug, "unknown scenario " + id);
                run.Finish(PlaytestOutcome.Error);
            }
        }

        /// <summary>One run: the virtual devices, the bot, and everything it watches (logs, telemetry, fps).</summary>
        sealed class Session
        {
            public readonly PlaytestRun Run;
            readonly PlaytestOptions options;
            readonly PlaytestInput input;
            readonly FpsSampler fps;
            readonly int diamondsAtStart;
            PlaytestBot bot;
            string lastOutcome;
            int deaths;
            int botRewinds;

            public Session(PlaytestRun run, PlaytestOptions options)
            {
                Run = run;
                this.options = options;
                input = PlaytestInput.Create();
                var go = new GameObject("PlaytestFps") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                fps = go.AddComponent<FpsSampler>();
                diamondsAtStart = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
                Application.logMessageReceived += OnLog;
                GameplayTelemetry.Recorded += OnTelemetry;
                GameplayTelemetry.Finished += OnLevelLog;
            }

            float Speed => Mathf.Clamp(options.speed, 0.25f, 4f);

            /// <summary>Writes the final metrics, then finishes (the report the browser reads is taken at Finish).</summary>
            void End(PlaytestOutcome outcome)
            {
                Close();
                Run.Finish(outcome);
            }

            /// <summary>Final metrics (also on a finish the package makes: stuck, error).</summary>
            public void Close()
            {
                if (fps != null && fps.Samples > 0)
                {
                    Run.Metric("fps", fps.Average);
                    Run.Metric("fpsWorst", fps.Worst);
                }
                Run.Metric("deaths", deaths);
                if (RogueliteManager.Instance != null)
                    Run.Metric("diamonds", RogueliteManager.Instance.GetPoints() - diamondsAtStart);
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
                GameplayTelemetry.Recorded -= OnTelemetry;
                GameplayTelemetry.Finished -= OnLevelLog;
                if (bot != null)
                    Object.Destroy(bot);
                input.Dispose();
                if (fps != null)
                    Object.Destroy(fps.gameObject);
                Time.timeScale = 1f;
            }

            // ─── What the game can see ───────────────────────────────────

            public void CheckContent()
            {
                var content = SickOfIt.Gameplay.GameContent.Current;
                if (content != null)
                {
                    int warnings = 0;
                    foreach (var issue in content.Issues)
                    {
                        if (issue.Severity == SickOfIt.Json.JsonIssueSeverity.Error)
                            Run.Problem(PlaytestSeverity.Bug, "GameConfig error: " + issue);
                        else if (warnings++ < 10)
                            Run.Problem(PlaytestSeverity.Warning, "GameConfig warning: " + issue);
                    }
                }
                if (RemoteContent.State == RemoteContent.Status.Failed)
                    Run.Problem(PlaytestSeverity.Warning, "remote content: " + RemoteContent.Message);
            }

            static readonly Regex SoundId = new Regex("\"([^\"]+)\"");

            void OnLog(string message, string stack, LogType type)
            {
                if (Run.Done || type != LogType.Warning || !message.StartsWith("[Sfx]", StringComparison.Ordinal))
                    return;
                var match = SoundId.Match(message);
                string id = match.Success ? match.Groups[1].Value : message;
                if (message.Contains("has no clip"))
                    Run.Problem(PlaytestSeverity.Polish, $"missing sound: \"{id}\" has no clip (not generated)");
                else
                    Run.Problem(PlaytestSeverity.Warning, $"missing sound: \"{id}\" is not in audio.json");
            }

            void OnTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (Run.Done)
                    return;
                string level = "level " + log.World.LevelNumber;
                switch (e.Kind)
                {
                    case "phase":
                        Run.Event("phase", $"{level}: {e.Subject}");
                        break;
                    case "error":
                        Run.Problem(PlaytestSeverity.Bug, $"{level} script error in {e.Subject}: {e.Detail}");
                        break;
                    case "upgrade":
                        Run.Event("upgrade", $"{level}: {e.Subject} -> {e.Value}");
                        break;
                    case "milestone":
                        Run.Event("milestone", $"{level}: {e.Subject}");
                        break;
                    case "floor":
                        Run.Metric("floor", e.Value);
                        break;
                }
            }

            void OnLevelLog(GameplayTelemetry log, GameplayTelemetry.Summary s)
            {
                if (Run.Done)
                    return;
                string prefix = "level" + s.levelNumber;
                Run.Event("level-end", $"level {s.levelNumber}: {s.outcome} after {s.duration:0.0} s, {s.kills}/{s.spawned} kills, " +
                                       $"hp {s.playerHealthEnd}/{s.playerMaxHealth}, phase {s.lastPhase}");
                Run.Add("kills", s.kills);
                Run.Add("spawned", s.spawned);
                Run.Add("playerHits", s.playerHits);
                Run.Add("rewinds", s.rewinds);
                Run.Add("rewoundSeconds", s.rewoundSeconds);
                Run.Metric(prefix + ".seconds", s.duration);
                Run.Metric(prefix + ".kills", s.kills);
                Run.Metric(prefix + ".hp", s.playerHealthEnd);
                Run.Add(prefix + ".attempts");
                int total = 0;
                foreach (var pair in s.damageTakenBySource)
                {
                    Run.Add("damageTaken." + pair.Key, pair.Value);
                    total += pair.Value;
                }
                Run.Add("damageTaken", total);
                if (RogueliteManager.Instance != null)
                    Run.Metric("diamonds", RogueliteManager.Instance.GetPoints() - diamondsAtStart);
            }

            // ─── Scenarios ───────────────────────────────────────────────

            public IEnumerator LevelScenario(int n)
            {
                var definition = GameDefinition.Instance;
                if (n < 1 || n > definition.TotalLevels)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"there is no level {n}");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                LastLevelOutcome = null;
                Run.Event("shortcut", $"load level {n} directly");
                var load = LoadLevel(n);
                if (load != null)
                    yield return load;
                yield return PlayLevel(n, 0f);
                LastLevelOutcome = lastOutcome;
                if (Run.Done)
                    yield break;
                Run.Progress(1f);
                End(lastOutcome == "completed" ? PlaytestOutcome.Won
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Done);
            }

            public IEnumerator Smoke()
            {
                var labels = GameDefinition.Instance.mainMenuConfig.labels;
                var texts = GameDefinition.Instance.menuTheme.texts;
                yield return ToMainMenu();
                if (Run.Done) yield break;
                Run.Progress(0.1f);

                yield return ClickUntil(labels.newGame, () => Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(1), 30f);
                if (Run.Done) yield break;
                yield return ReachLevel(1);
                if (Run.Done) yield break;
                Run.Progress(0.3f);

                // Play about 30 s: rewind once at ~10 s, pause and unpause at ~20 s.
                bool rewound = false, paused = false;
                yield return PlayLevel(1, 30f, now =>
                {
                    if (!rewound && now > 10f && bot != null && bot.RewindNow(1.5f))
                    {
                        rewound = true;
                        Run.Event("rewind", "smoke: hold R for 1.5 s");
                    }
                    if (!paused && now > 20f)
                    {
                        paused = true;
                        return PauseAndResume();
                    }
                    return null;
                }, 0.3f, 0.9f);
                if (Run.Done) yield break;
                if (!rewound)
                    Run.Problem(PlaytestSeverity.Warning, "smoke: the bot never got to rewind (no juice, or the level ended first)");

                // Back out to the main menu through the pause menu.
                if (lastOutcome == "time up" && GameplayWorld.Instance != null)
                {
                    yield return Tap(Key.P);
                    yield return ClickUntil(texts.quitToMenu, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 20f);
                    if (Run.Done) yield break;
                    Run.Scene("Main Menu");
                }
                Run.Progress(1f);
                End(lastOutcome == "died" ? PlaytestOutcome.Lost : PlaytestOutcome.Done);
            }

            IEnumerator PauseAndResume()
            {
                var hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                if (hud == null || hud.PickerOpen || hud.ResultsOpen)
                    yield break;
                yield return Tap(Key.P);
                yield return WaitReal(1f);
                hud = GameplayWorld.Instance != null ? GameplayWorld.Instance.Hud : null;
                if (hud == null)
                    yield break;
                if (!hud.Paused)
                {
                    Run.Problem(PlaytestSeverity.Bug, "pressing P didn't pause the level");
                    yield break;
                }
                Run.Event("pause", "P");
                yield return Tap(Key.P);
                yield return WaitReal(0.5f);
                if (hud != null && hud.Paused)
                    Run.Problem(PlaytestSeverity.Bug, "pressing P again didn't resume the level");
                else
                    Run.Event("resume", "P");
            }

            public IEnumerator Full()
            {
                var definition = GameDefinition.Instance;
                var labels = definition.mainMenuConfig.labels;
                var texts = definition.menuTheme.texts;
                int levels = definition.TotalLevels;
                float deadline = Time.realtimeSinceStartup + 600f;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return ClickUntil(labels.newGame, () => Object.FindAnyObjectByType<VideoRuntime>() != null || LevelRunning(1), 30f);
                if (Run.Done) yield break;

                int completed = 0;
                for (int n = 1; n <= levels; n++)
                {
                    int attempts = 0;
                    while (true)
                    {
                        yield return ReachLevel(n);
                        if (Run.Done) yield break;
                        attempts++;
                        float left = deadline - Time.realtimeSinceStartup;
                        if (left < 5f)
                        {
                            Run.Event("time-up", $"stopped at level {n}");
                            End(PlaytestOutcome.Done);
                            yield break;
                        }
                        float from = (float)(n - 1) / levels, to = (float)n / levels;
                        yield return PlayLevel(n, 0f, null, from, to, left, waitOutDeath: true);
                        if (Run.Done) yield break;
                        if (lastOutcome == "completed")
                            break;
                        if (lastOutcome == "died" && attempts < 3)
                        {
                            // The game's own way back: the death scene, then the level again.
                            Run.Event("retry", $"level {n}, attempt {attempts + 1}");
                            continue;
                        }
                        Run.Event("stopped", $"level {n}: {lastOutcome} after {attempts} attempt(s)");
                        Run.Metric("levelsCompleted", completed);
                        End(lastOutcome == "died" ? PlaytestOutcome.Lost
                            : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
                        yield break;
                    }
                    completed++;
                    Run.Metric("levelsCompleted", completed);
                    if (n == levels)
                        break;
                    // Results screen, then Continue (the game goes on to the next video or level).
                    yield return Run.Until(() => GameplayWorld.Instance == null || GameplayWorld.Instance.Hud == null || GameplayWorld.Instance.Hud.ResultsOpen,
                        30f, $"the level {n} results screen");
                    if (Run.Done) yield break;
                    if (GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.ResultsOpen)
                        yield return ClickUntil(texts.continueText, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.ResultsOpen, 20f);
                    if (Run.Done) yield break;
                }
                Run.Progress(1f);
                End(PlaytestOutcome.Won);
            }

            /// <summary>Gets from wherever the game went (video, death scene, menus) into level n.</summary>
            IEnumerator ReachLevel(int n)
            {
                float end = Time.realtimeSinceStartup + 90f;
                bool shortcut = false;
                float videoSeenAt = -1f;
                while (!Run.Done && !LevelRunning(n))
                {
                    if (Time.realtimeSinceStartup > end)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"never reached level {n} (stuck in {SceneName()})");
                        End(PlaytestOutcome.Stuck);
                        yield break;
                    }
                    bool inVideo = Object.FindAnyObjectByType<VideoRuntime>() != null;
                    bool inDeath = Object.FindAnyObjectByType<DeathRuntime>() != null;
                    if (inVideo || inDeath)
                    {
                        Run.Scene(inVideo ? "Video" : "Death");
                        if (videoSeenAt < 0f)
                            videoSeenAt = Time.realtimeSinceStartup;
                        // Watch a moment (for the recording), then skip like an impatient player.
                        if (Time.realtimeSinceStartup - videoSeenAt > 2f)
                        {
                            yield return Tap(Key.Escape);
                            Run.Event("skip", inVideo ? "video" : "death animation");
                            videoSeenAt = Time.realtimeSinceStartup + 3f;
                        }
                    }
                    else if (!shortcut && (Object.FindAnyObjectByType<LevelSelectionManager>() != null
                                           || Object.FindAnyObjectByType<MainMenuManager>() != null))
                    {
                        // The flow ended in a menu (no next video): open the level directly.
                        shortcut = true;
                        Run.Event("shortcut", $"the game went to {SceneName()}; loading level {n} directly");
                        var load = LoadLevel(n);
                        if (load != null)
                            yield return load;
                    }
                    yield return null;
                }
            }

            public IEnumerator Menus()
            {
                var definition = GameDefinition.Instance;
                var main = definition.mainMenuConfig;
                var labels = main.labels;
                yield return ToMainMenu();
                if (Run.Done) yield break;

                var screens = new List<(string label, bool shown, Func<bool> open, string back, string name)>
                {
                    (labels.levelSelect, main.showLevelSelectButton, () => Object.FindAnyObjectByType<LevelSelectionManager>() != null,
                        definition.levelSelectionConfig.layout.backText, "Level Select"),
                    (labels.videos, main.showVideosButton, () => Object.FindAnyObjectByType<VideoSelectionManager>() != null,
                        definition.videoSelectionConfig.layout.backText, "Video Select"),
                    (labels.shop, main.showUpgradeShopButton, () => Object.FindAnyObjectByType<UpgradeShopManager>() != null,
                        definition.upgradeShopConfig.layout.backText, "Shop"),
                };
                int step = 0, steps = screens.Count + 1;
                foreach (var screen in screens)
                {
                    step++;
                    if (!screen.shown)
                    {
                        Run.Event("menu-skipped", screen.name + " (hidden by mainMenu.json)");
                        continue;
                    }
                    yield return ClickUntil(screen.label, screen.open, 20f);
                    if (Run.Done) yield break;
                    Run.Scene(screen.name);
                    Run.Event("menu-open", screen.name);
                    yield return WaitReal(1.5f);
                    if (CountButtons() < 2)
                        Run.Problem(PlaytestSeverity.Warning, $"{screen.name} shows {CountButtons()} button(s)");
                    yield return ClickUntil(screen.back, () => Object.FindAnyObjectByType<MainMenuManager>() != null && !screen.open(), 20f);
                    if (Run.Done) yield break;
                    Run.Scene("Main Menu");
                    Run.Event("menu-close", screen.name);
                    Run.Progress((float)step / steps);
                    yield return WaitReal(1f);
                }

                if (main.showSettingsButton)
                {
                    string done = definition.menuTheme.texts.done;
                    yield return ClickUntil(labels.settings, () => FindButton(done) != null, 15f);
                    if (Run.Done) yield break;
                    Run.Event("menu-open", "Settings");
                    yield return WaitReal(1.5f);
                    yield return ClickUntil(done, () => FindButton(done) == null, 15f);
                    if (Run.Done) yield break;
                    Run.Event("menu-close", "Settings");
                }
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            // ─── Playing a level ─────────────────────────────────────────

            static bool LevelRunning(int n)
            {
                var world = GameplayWorld.Instance;
                return world != null && world.LevelNumber == n && world.Player != null
                       && GameClock.Instance != null && GameClock.Instance.Time > 0f;
            }

            /// <summary>
            /// Plays level n with the bot until it is completed, the player dies, the level's time runs out, or
            /// 'playSeconds' of game time pass (0 = no limit). Sets lastOutcome: completed, died, timeout, time up,
            /// softlock, scene changed. 'during' may return a coroutine to run mid-level (pause checks). With
            /// 'waitOutDeath' a death plays out like a player's: the bot may undo it with a rewind, else the game
            /// leaves for the death scene; otherwise the level ends at the fatal hit.
            /// </summary>
            IEnumerator PlayLevel(int n, float playSeconds, Func<float, IEnumerator> during = null,
                float progressFrom = 0f, float progressTo = 1f, float realBudget = float.MaxValue, bool waitOutDeath = false)
            {
                lastOutcome = "not started";
                Run.Scene("Level " + n);
                yield return Run.Until(() => LevelRunning(n), 45f, $"level {n} to start");
                if (Run.Done)
                    yield break;

                var world = GameplayWorld.Instance;
                var config = GameDefinition.Instance.GetLevel(n);
                if (bot != null)
                    Object.Destroy(bot);
                bot = PlaytestBot.Attach(world, (uint)(1000 * options.seed + n), input);
                Time.timeScale = Speed;
                Run.Event("level-start", $"level {n}: {config.levelName}");

                var health = world.Player.GetComponentInChildren<PlayerHealth>();
                float limit = Mathf.Max(30f, config.duration) + 10f;
                if (playSeconds > 0f)
                    limit = Mathf.Min(limit, playSeconds);
                float started = Time.realtimeSinceStartup;
                float realLimit = Mathf.Min(limit / Speed + 60f, realBudget);
                float lastClock = -1f, clockMovedAt = started, nextScan = 0f, shownProgress = -1f;
                string outcome = "timeout";
                bool dying = false;
                while (!Run.Done)
                {
                    if (world == null) { outcome = dying ? "died" : "scene changed"; break; }
                    var clock = GameClock.Instance;
                    float now = clock != null ? clock.Time : 0f;
                    if (world.Director != null && world.Director.Completed) { outcome = "completed"; break; }
                    if (health != null && !health.IsAlive)
                    {
                        if (!dying)
                        {
                            dying = true;
                            deaths++;
                            Run.Metric("deaths", deaths);
                            Run.Event("death", $"level {n} at {now:0.0} s");
                        }
                        if (!waitOutDeath) { outcome = "died"; break; }
                    }
                    else if (dying)
                    {
                        dying = false;
                        Run.Event("death-undone", $"level {n}: rewound out of it");
                    }
                    if (now >= limit) { outcome = playSeconds > 0f && playSeconds < Mathf.Max(30f, config.duration) + 10f ? "time up" : "timeout"; break; }
                    if (Time.realtimeSinceStartup - started > realLimit) { outcome = "timeout"; break; }

                    // Softlock: the clock stands still while nothing (pause, picker, results, rewind) holds it.
                    var hud = world.Hud;
                    bool held = dying || hud != null && (hud.Paused || hud.PickerOpen || hud.ResultsOpen) || (clock != null && clock.IsRewinding);
                    if (held || Mathf.Abs(now - lastClock) > 0.0001f)
                    {
                        lastClock = now;
                        clockMovedAt = Time.realtimeSinceStartup;
                    }
                    else if (Time.realtimeSinceStartup - clockMovedAt > SoftlockSeconds)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"softlock: level {n}'s clock stopped at {now:0.0} s (phase \"{world.Director?.PhaseName}\")");
                        outcome = "softlock";
                        break;
                    }

                    if (Time.realtimeSinceStartup >= nextScan)
                    {
                        nextScan = Time.realtimeSinceStartup + 0.5f;
                        ScanPlaceholders(world);
                        Run.Note($"level {n} {world.Director?.PhaseName} t={now:0}");
                    }
                    float progress = Mathf.Lerp(progressFrom, progressTo, Mathf.Clamp01(now / limit));
                    if (progress - shownProgress >= 0.01f)
                    {
                        shownProgress = progress;
                        Run.Progress(progress);
                    }

                    var extra = during?.Invoke(now);
                    if (extra != null)
                        yield return extra;
                    else
                        yield return null;
                }

                if (bot != null)
                {
                    botRewinds += bot.RewindsStarted;
                    Run.Metric("botRewinds", botRewinds);
                    Object.Destroy(bot);
                }
                bot = null;
                input.ReleaseAll();
                Time.timeScale = 1f;
                // Ends the level's log (the level may already have: complete, died). Writes Logs/Playtest in the editor.
                if (world != null && world.Telemetry != null)
                    world.Telemetry.Finish(outcome);
                lastOutcome = outcome;
            }

            readonly HashSet<string> placeholderIds = new HashSet<string>();

            void ScanPlaceholders(GameplayWorld world)
            {
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || placeholderIds.Contains(e.DefId) || e.transform.Find("Placeholder") == null)
                        continue;
                    placeholderIds.Add(e.DefId);
                    Run.Problem(PlaytestSeverity.Polish, $"placeholder art: \"{e.DefId}\" is drawn as a labeled circle (no visual.animator)");
                }
            }

            // ─── Menus and keys through the virtual devices ──────────────

            IEnumerator ToMainMenu()
            {
                if (Object.FindAnyObjectByType<MainMenuManager>() == null)
                {
                    Run.Event("shortcut", "load the main menu");
                    if (GameplayWorld.Instance != null)
                        GameplayWorld.Instance.Telemetry?.Finish("playtest left");
                    Time.timeScale = 1f;
                    SceneTransitionManager.Instance.LoadMainMenu();
                }
                yield return Run.Until(() => Object.FindAnyObjectByType<MainMenuManager>() != null && CountButtons() > 0, 45f, "the main menu");
                if (Run.Done)
                    yield break;
                Run.Scene("Main Menu");
                yield return WaitReal(1.5f);
            }

            IEnumerator Tap(Key key)
            {
                input.SetKey(key, true);
                yield return null;
                yield return null;
                input.SetKey(key, false);
                yield return null;
            }

            IEnumerator WaitReal(float seconds)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < end)
                    yield return null;
            }

            /// <summary>Clicks the button labeled 'text' with the virtual mouse until 'effect' is true (one retry).</summary>
            IEnumerator ClickUntil(string text, Func<bool> effect, float timeout)
            {
                for (int attempt = 0; attempt < 2 && !Run.Done; attempt++)
                {
                    MenuButton button = null;
                    yield return Run.Until(() => (button = FindButton(text)) != null && button.IsInteractable(), timeout, $"the \"{text}\" button");
                    if (Run.Done)
                        yield break;
                    yield return WaitReal(0.4f); // entrance animation
                    button = FindButton(text);
                    if (button != null)
                    {
                        input.MoveMouse(ScreenPoint(button));
                        yield return null;
                        yield return null;
                        input.SetMouseButton(true);
                        yield return null;
                        yield return null;
                        input.SetMouseButton(false);
                        Run.Event("click", text);
                    }
                    float end = Time.realtimeSinceStartup + 3f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        if (Safe(effect))
                            yield break;
                        yield return null;
                    }
                }
                yield return Run.Until(effect, timeout, $"clicking \"{text}\" to do something");
            }

            static bool Safe(Func<bool> test)
            {
                try { return test(); }
                catch { return false; }
            }

            static Vector2 ScreenPoint(Component button)
            {
                var rect = (RectTransform)button.transform;
                var canvas = button.GetComponentInParent<Canvas>();
                canvas = canvas != null ? canvas.rootCanvas : null;
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            }

            static readonly Regex Tags = new Regex("<[^>]*>");

            static MenuButton FindButton(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return null;
                MenuButton partial = null;
                foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                {
                    if (button == null || !button.isActiveAndEnabled || button.Label == null)
                        continue;
                    string label = Tags.Replace(button.Label.text ?? "", "").Trim();
                    if (string.Equals(label, text.Trim(), StringComparison.OrdinalIgnoreCase))
                        return button;
                    if (partial == null && label.IndexOf(text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        partial = button;
                }
                return partial;
            }

            static int CountButtons()
            {
                int count = 0;
                foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                    if (button != null && button.isActiveAndEnabled)
                        count++;
                return count;
            }

            static string SceneName()
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                return scene.IsValid() ? scene.name : "?";
            }
        }

        /// <summary>Frames per second, sampled every second in real time.</summary>
        sealed class FpsSampler : MonoBehaviour
        {
            int frames;
            float since;
            double sum;
            public int Samples { get; private set; }
            public float Worst { get; private set; } = float.MaxValue;
            public float Average => Samples > 0 ? (float)(sum / Samples) : 0f;

            void Update()
            {
                frames++;
                since += Time.unscaledDeltaTime;
                if (since < 1f)
                    return;
                float value = frames / since;
                frames = 0;
                since = 0f;
                // The first seconds include loading; skip them.
                if (Time.realtimeSinceStartup < 5f)
                    return;
                sum += value;
                Samples++;
                Worst = Mathf.Min(Worst, value);
            }
        }
    }
}
