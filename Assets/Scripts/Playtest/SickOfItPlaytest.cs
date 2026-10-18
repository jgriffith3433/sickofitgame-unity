using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    /// select, videos, shop and settings open and close; settings turns Sound off and on and steps Music), site (the
    /// website pages around the game show "Powered by HAWKES GEN", the landing page links Hawkes Biomedical, and the
    /// game page itself has neither), layout (every screen's buttons on screen, apart and thumb-sized: main menu,
    /// level select, settings, the level HUD, pause and its settings, the death scene; meant for device "phone").
    /// Every scenario starts by pressing the page's "Tap to play" sound prompt (<see cref="PageSound"/>).
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

        /// <summary>The level the imp-death scenario plays: Level 1's "imps" phase brings four, one at a time first.</summary>
        public const int ImpLevel = 1;

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
            list.Add(new PlaytestScenario("imp-death", $"Level {ImpLevel}'s imps, started directly (setup shortcut): the bot kills them and each imp's Die clip " +
                "must play where it died; the first deaths run in slow motion so the clip can be seen", 90, "level"));
            list.Add(new PlaytestScenario("full", "New Game, then every level in order through the real flow, as far as the bot gets", 600, "full"));
            list.Add(new PlaytestScenario("menus", "Tap to play, then level select, videos, shop and settings (Sound off/on, Music -/+) each open and close", 70, "menus"));
            list.Add(new PlaytestScenario("layout", "Phone layout check (run with device \"phone\", 390x844): on the main menu, level select, settings, " +
                "level 1's HUD, the pause menu and its settings, and the death scene (loaded directly, a setup shortcut), no button overlaps another " +
                "or a HUD panel, none leaves the screen, and every button is thumb-sized", 120, "menus"));
            list.Add(new PlaytestScenario("site", "The game's main menu (no credit on the game page), then the landing page, blog and sub pages each opened over it: Powered by HAWKES GEN on every one, Hawkes Biomedical on the landing page", 60, "menus"));
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
            yield return session.TapSoundPrompt();
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
            else if (id == "site")
                yield return session.Site();
            else if (id == "layout")
                yield return session.Layout();
            else if (id == "imp-death")
                yield return session.ImpDeath();
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
            int impDeaths;
            bool impSlowMotion;
            float slowMotionUntil;

            /// <summary>imp-death: how many deaths play in slow motion, and how slow.</summary>
            const int ImpSlowMotionDeaths = 3;
            const float ImpSlowMotion = 0.3f;

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
                SitePages.Close();
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
                    case "death" when e.Subject == "imp" && e.Detail == "Damage":
                        CheckImpDeath(log.World.FindBySerial(e.Value), level);
                        break;
                    case "floor":
                        Run.Metric("floor", e.Value);
                        break;
                }
            }

            /// <summary>
            /// An imp was killed: its "Die" clip must be playing where it died (Entity.Die plays it before the death is
            /// recorded). In the imp-death scenario the first few play in slow motion so a recording shows the frames.
            /// </summary>
            void CheckImpDeath(Entity imp, string level)
            {
                impDeaths++;
                Run.Metric("impDeaths", impDeaths);
                if (imp == null || imp.Animation == null || !imp.Animation.TryGetClip(Entity.DieState, out var frames, out float fps)
                    || frames == null || frames.Length == 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"{level}: an imp died without a Die clip (no death frames, only the pop)");
                    return;
                }
                bool playing = false;
                foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (r.name == "Flipbook" && r.gameObject.activeInHierarchy && Array.IndexOf(frames, r.sprite) >= 0)
                    {
                        playing = true;
                        break;
                    }
                }
                float fade = imp.Def.visual.deathFade > 0f ? imp.Def.visual.deathFade : 0.25f;
                float seconds = frames.Length / Mathf.Max(0.01f, fps) + fade;
                string sheet = frames[0] != null && frames[0].texture != null ? frames[0].texture.name : "?";
                Run.Metric("impDieFrames", frames.Length);
                Run.Metric("impDieSeconds", seconds);
                Run.Event("imp-death", $"{level}: an imp died: Die clip {sheet}, {frames.Length} frames at {fps:0} fps " +
                                       $"({seconds:0.00} s with the fade){(playing ? "" : ", NOT playing")}");
                if (!playing)
                    Run.Problem(PlaytestSeverity.Bug, $"{level}: an imp died but its Die clip ({sheet}) isn't playing where it died");
                else if (impSlowMotion && impDeaths <= ImpSlowMotionDeaths && Time.timeScale > 0f)
                {
                    // Presentation pacing only (Time.timeScale), like the speed option: the simulation is untouched.
                    Time.timeScale = Speed * ImpSlowMotion;
                    slowMotionUntil = Time.realtimeSinceStartup + seconds / (Speed * ImpSlowMotion) + 0.2f;
                    Run.Event("slow-motion", $"imp death {impDeaths} at {ImpSlowMotion:0.0}x");
                }
            }

            void EndSlowMotion()
            {
                if (slowMotionUntil <= 0f)
                    return;
                slowMotionUntil = 0f;
                if (Time.timeScale > 0f)
                    Time.timeScale = Speed;
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

            /// <summary>
            /// Plays Level 1 (where the imps arrive) until the bot has killed ImpSlowMotionDeaths imps and their Die
            /// clips have played, or the level ends. Every kill is checked by CheckImpDeath.
            /// </summary>
            public IEnumerator ImpDeath()
            {
                impSlowMotion = true;
                Run.Event("shortcut", $"load level {ImpLevel} directly");
                var load = LoadLevel(ImpLevel);
                if (load != null)
                    yield return load;
                yield return PlayLevel(ImpLevel, 0f, until: () => impDeaths >= ImpSlowMotionDeaths && slowMotionUntil <= 0f);
                if (Run.Done)
                    yield break;
                Run.Metric("impDeaths", impDeaths);
                if (impDeaths == 0)
                    Run.Problem(PlaytestSeverity.Warning, $"imp-death: the bot killed no imp (level {lastOutcome}), so no Die clip was seen");
                Run.Progress(1f);
                End(impDeaths == 0 ? PlaytestOutcome.Done
                    : lastOutcome == "died" ? PlaytestOutcome.Lost
                    : lastOutcome == "softlock" ? PlaytestOutcome.Stuck
                    : PlaytestOutcome.Won);
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
                string done = GameDefinition.Instance.menuTheme.texts.done;
                yield return ClickUntil(GameDefinition.Instance.menuTheme.texts.settings, () => FindButton(done) != null, 10f);
                if (Run.Done) yield break;
                Run.Event("menu-open", "Settings (from pause)");
                yield return WaitReal(1f);
                yield return ClickUntil(done, () => FindButton(done) == null, 10f);
                if (Run.Done) yield break;
                Run.Event("menu-close", "Settings (from pause)");
                yield return WaitReal(0.5f);
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
                    Run.Scene("Settings");
                    Run.Event("menu-open", "Settings");
                    yield return WaitReal(1f);
                    yield return SoundSettings();
                    if (Run.Done) yield break;
                    yield return ClickUntil(done, () => FindButton(done) == null, 15f);
                    if (Run.Done) yield break;
                    Run.Scene("Main Menu");
                    Run.Event("menu-close", "Settings");
                }
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            /// <summary>
            /// The responsive layout on every main screen: each one is opened the way a player opens it (clicks and
            /// taps on the real buttons), then <see cref="MenuLayoutAudit"/> checks what is on screen. A level and the
            /// death scene are loaded directly (setup shortcuts). Each overlap, off-screen or too-small button is a bug.
            /// </summary>
            public IEnumerator Layout()
            {
                var definition = GameDefinition.Instance;
                var main = definition.mainMenuConfig;
                var labels = main.labels;
                var texts = definition.menuTheme.texts;
                string done = texts.done;
                Run.Metric("screenWidth", Screen.width);
                Run.Metric("screenHeight", Screen.height);
                Run.Event("layout-screen", $"{Screen.width}x{Screen.height} {(MenuScreen.Portrait ? "portrait" : "landscape")}, safe area {Screen.safeArea}, " +
                                           $"touch size {MenuScreen.MinTouchPixels(definition.menuTheme.responsive):0} px" +
                                           (MenuScreen.Portrait ? "" : " (landscape: run it with device \"phone\" for the portrait layout)"));
                const int steps = 7;
                int step = 0;

                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return Audit("Main Menu", ++step, steps);

                if (main.showLevelSelectButton)
                {
                    yield return ClickUntil(labels.levelSelect, () => Object.FindAnyObjectByType<LevelSelectionManager>() != null, 20f);
                    if (Run.Done) yield break;
                    yield return Audit("Level Select", ++step, steps);
                    yield return ClickUntil(definition.levelSelectionConfig.layout.backText,
                        () => Object.FindAnyObjectByType<MainMenuManager>() != null && Object.FindAnyObjectByType<LevelSelectionManager>() == null, 20f);
                    if (Run.Done) yield break;
                    yield return WaitReal(1f);
                }
                if (main.showSettingsButton)
                {
                    yield return ClickUntil(labels.settings, () => FindButton(done) != null, 15f);
                    if (Run.Done) yield break;
                    yield return Audit("Settings", ++step, steps);
                    yield return ClickUntil(done, () => FindButton(done) == null, 15f);
                    if (Run.Done) yield break;
                }

                Run.Event("shortcut", "load level 1 directly");
                var load = LoadLevel(1);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => LevelRunning(1), 45f, "level 1 to start");
                if (Run.Done) yield break;
                yield return Audit("Level 1 HUD", ++step, steps, 2.5f);

                // Pause with the HUD's own button (the one a thumb presses), then its settings.
                yield return ClickUntil("II", () => GameplayWorld.Instance != null && GameplayWorld.Instance.Hud != null && GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;
                yield return Audit("Pause", ++step, steps);
                yield return ClickUntil(texts.settings, () => FindButton(done) != null, 10f);
                if (Run.Done) yield break;
                yield return Audit("Pause Settings", ++step, steps);
                yield return ClickUntil(done, () => FindButton(done) == null, 10f);
                if (Run.Done) yield break;
                yield return ClickUntil(texts.resume, () => GameplayWorld.Instance == null || !GameplayWorld.Instance.Hud.Paused, 10f);
                if (Run.Done) yield break;

                string death = definition.deaths.Count > 0 && !string.IsNullOrEmpty(definition.deaths[0].deathName) ? definition.deaths[0].deathName : "Death1";
                Run.Event("shortcut", $"load the death scene ({death}) directly");
                GameplayWorld.Instance?.Telemetry?.Finish("playtest left");
                Time.timeScale = 1f;
                SceneTransitionManager.Instance.LoadDeathScene(death);
                yield return Run.Until(() => Object.FindAnyObjectByType<DeathRuntime>() != null && FindButton(texts.rageQuit) != null, 45f, "the death scene");
                if (Run.Done) yield break;
                yield return Audit("Death", ++step, steps, 1.5f);
                yield return ClickUntil(texts.rageQuit, () => Object.FindAnyObjectByType<MainMenuManager>() != null, 30f);
                if (Run.Done) yield break;
                Run.Scene("Main Menu");

                Run.Metric("layoutProblems", layoutProblems);
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            int layoutProblems;

            /// <summary>Lets the screen settle (entrances), then checks its layout and reports each problem once per screen.</summary>
            IEnumerator Audit(string screen, int step, int steps, float settle = 1.5f)
            {
                Run.Scene(screen);
                yield return WaitReal(settle);
                // Park the virtual mouse in a corner so no hovered button is mid-spring.
                input.MoveMouse(new Vector2(1f, 1f));
                yield return WaitReal(0.4f);
                if (Run.Done)
                    yield break;
                var result = MenuLayoutAudit.Check(GameDefinition.Instance.menuTheme.responsive);
                int buttons = 0;
                foreach (var box in result.Boxes)
                    if (box.Kind == MenuElementKind.Button)
                        buttons++;
                foreach (var problem in result.Problems)
                    Run.Problem(PlaytestSeverity.Bug, $"layout ({screen}, {Screen.width}x{Screen.height}): {problem}");
                foreach (var warning in result.Warnings)
                    Run.Problem(PlaytestSeverity.Polish, $"layout ({screen}, {Screen.width}x{Screen.height}): {warning}");
                layoutProblems += result.Problems.Count;
                Run.Metric("layout." + screen.Replace(" ", "") + ".problems", result.Problems.Count);
                Run.Event("layout", $"{screen}: {buttons} buttons and {result.Boxes.Count - buttons} panels/texts checked, " +
                                    (result.Problems.Count == 0 ? "no overlaps, all on screen and thumb-sized" : $"{result.Problems.Count} problem(s)"));
                Run.Progress((float)step / (steps + 1));
            }

            /// <summary>
            /// The website around the game: the game page has no credit (the game screen is unchanged), then each
            /// site page opens in a frame over the game, scrolled to its "Powered by HAWKES GEN" mark (ringed for the
            /// recording), and the landing page's Hawkes Biomedical link is checked too. The pages are opened by
            /// address, the way a visitor follows the site's links; outside a WebGL build there is no site to check.
            /// </summary>
            public IEnumerator Site()
            {
                yield return ToMainMenu();
                if (Run.Done) yield break;
                if (!SitePages.Available)
                {
                    Run.Event("site", "no website here (editor or a test): nothing to check");
                    Run.Progress(1f);
                    End(PlaytestOutcome.Done);
                    yield break;
                }

                int gameCredits = SitePages.GamePageCredits();
                Run.Event("game-page", gameCredits == 0 ? "the game page has no credit mark (unchanged)" : $"the game page has {gameCredits} credit element(s)");
                if (gameCredits > 0)
                    Run.Problem(PlaytestSeverity.Bug, $"the game page itself shows the Hawkes credit ({gameCredits} element(s)); it belongs on the site pages only");
                yield return WaitReal(1.5f);

                int passed = 0;
                for (int i = 0; i < SitePages.Pages.Length && !Run.Done; i++)
                {
                    var (path, name, landing) = SitePages.Pages[i];
                    Run.Scene("Site: " + name);
                    SitePages.Open(path);
                    string json = "";
                    yield return Run.Until(() => (json = SitePages.Result()).Length > 0, 25f, $"the {name} page to load");
                    if (Run.Done) break;
                    var check = JsonUtility.FromJson<SitePages.Check>(json);
                    bool ok = true;
                    if (!string.IsNullOrEmpty(check.error))
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"site page {name} (/{path}): {check.error}");
                        ok = false;
                    }
                    else
                    {
                        ok &= Expect(name, path, "Powered by HAWKES GEN", "https://hawkesgen.com", check.gen, "powered by hawkes gen");
                        if (landing)
                            ok &= Expect(name, path, "Hawkes Biomedical", "https://hawkesbiomedical.com", check.parent, "hawkes biomedical");
                    }
                    if (ok)
                        passed++;
                    Run.Progress((i + 1f) / (SitePages.Pages.Length + 1));
                    yield return WaitReal(2.5f); // on screen for the recording
                }
                SitePages.Close();
                if (Run.Done) yield break;
                Run.Metric("sitePagesChecked", SitePages.Pages.Length);
                Run.Metric("sitePagesWithCredit", passed);
                Run.Scene("Main Menu");
                Run.Event("site-done", $"{passed}/{SitePages.Pages.Length} site pages show the credit; back to the game");
                yield return WaitReal(1.5f);
                Run.Progress(1f);
                End(PlaytestOutcome.Done);
            }

            bool Expect(string page, string path, string what, string url, SitePages.Link link, string text)
            {
                if (link == null || string.IsNullOrEmpty(link.href))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}) has no \"{what}\" link to {url}");
                    return false;
                }
                bool good = link.visible && (link.text ?? "").ToLowerInvariant().Contains(text);
                Run.Event("site-link", $"{page}: \"{link.text}\" -> {link.href}{(link.visible ? "" : " (not visible)")}");
                if (!good)
                    Run.Problem(PlaytestSeverity.Bug, $"site page {page} (/{path}): the {url} link reads \"{link.text}\"{(link.visible ? "" : " and isn't visible")}");
                return good;
            }

            /// <summary>
            /// Presses the page's "Tap to play" prompt, so the run isn't recorded behind it, and logs what the
            /// page says about audio. The page can only be pressed from script: a browser unlocks audio for a
            /// real tap only, so "audio" stays "suspended" unless the runner clicked the page itself.
            /// </summary>
            public IEnumerator TapSoundPrompt()
            {
                string before = PageSound.State();
                if (before == null)
                {
                    Run.Event("tap-prompt", "no page prompt here (editor or a page without hawkes-sound.js)");
                    yield break;
                }
                if (before.Length == 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, "the game page has no tap-to-play sound prompt (hawkes-sound.js not loaded)");
                    yield break;
                }
                bool tapped = PageSound.Tap();
                yield return WaitReal(0.5f);
                string after = PageSound.State();
                Run.Event("tap-prompt", (tapped ? "pressed the prompt: " : "the prompt was already gone: ") + after);
                Run.Metric("soundPrompt", tapped ? 1f : 0f);
                if (after != null && after.Contains("\"prompt\":\"shown\""))
                    Run.Problem(PlaytestSeverity.Bug, "the tap-to-play prompt stayed up after it was pressed: " + after);
            }

            /// <summary>In the open settings: Sound off and on again, Music one step down and up, checking each applies.</summary>
            IEnumerator SoundSettings()
            {
                var texts = GameDefinition.Instance.menuTheme.texts;
                bool wasOn = GameSettings.SoundOn;
                float music = GameSettings.MusicVolume;

                yield return ClickIn(texts.sound, wasOn ? texts.on : texts.off, () => GameSettings.SoundOn != wasOn);
                if (Run.Done) yield break;
                bool muted = !GameSettings.SoundOn && AudioListener.volume == 0f;
                Run.Event("settings", $"Sound {(GameSettings.SoundOn ? "on" : "off")}, listener volume {AudioListener.volume:0.##}, {GameSettings.SoundKey} = {GameSettings.SoundJson}");
                if (wasOn && !muted)
                    Run.Problem(PlaytestSeverity.Bug, $"Sound Off didn't mute the game (listener volume {AudioListener.volume:0.##})");
                yield return WaitReal(0.5f);
                yield return ClickIn(texts.sound, GameSettings.SoundOn ? texts.on : texts.off, () => GameSettings.SoundOn == wasOn);
                if (Run.Done) yield break;
                Run.Event("settings", $"Sound {(GameSettings.SoundOn ? "on" : "off")}, listener volume {AudioListener.volume:0.##}");

                string step = music > 0.05f ? "-" : "+";
                yield return ClickIn(texts.musicVolume, step, () => !Mathf.Approximately(GameSettings.MusicVolume, music));
                if (Run.Done) yield break;
                Run.Event("settings", $"Music {music:0.#} -> {GameSettings.MusicVolume:0.#}, {GameSettings.SoundKey} = {GameSettings.SoundJson}");
                float stepped = GameSettings.MusicVolume;
                yield return WaitReal(0.5f);
                yield return ClickIn(texts.musicVolume, step == "-" ? "+" : "-", () => !Mathf.Approximately(GameSettings.MusicVolume, stepped));
                if (Run.Done) yield break;
                Run.Event("settings", $"Music back to {GameSettings.MusicVolume:0.#}");
                Run.Metric("soundSettings", 1f);
            }

            /// <summary>Clicks the button labeled 'text' in the settings row 'row' until 'effect' is true.</summary>
            IEnumerator ClickIn(string row, string text, Func<bool> effect)
            {
                MenuButton button = null;
                yield return Run.Until(() => (button = FindButtonInRow(row, text)) != null, 10f, $"the \"{text}\" button for {row}");
                if (Run.Done)
                    yield break;
                // The panel springs in when it opens, so a button's place moves for a moment: let it settle, then
                // aim at the button where it is now, and click again if the first click found it moving.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    yield return WaitReal(0.6f);
                    if (Run.Done)
                        yield break;
                    button = FindButtonInRow(row, text) ?? button;
                    input.MoveMouse(ScreenPoint(button));
                    yield return null;
                    yield return null;
                    input.SetMouseButton(true);
                    yield return null;
                    yield return null;
                    input.SetMouseButton(false);
                    Run.Event("click", $"{row}: {text}");
                    float end = Time.realtimeSinceStartup + 1.5f;
                    while (Time.realtimeSinceStartup < end && !Safe(effect))
                        yield return null;
                    if (Safe(effect))
                        yield break;
                }
                yield return Run.Until(effect, 3f, $"clicking {row} \"{text}\" to change the setting");
            }

            static MenuButton FindButtonInRow(string row, string text)
            {
                foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                {
                    if (button == null || !button.isActiveAndEnabled || button.Label == null)
                        continue;
                    // A settings row is named after its label; its buttons sit a level or two below it.
                    var parent = button.transform.parent;
                    for (int up = 0; up < 3 && parent != null && parent.name != row; up++)
                        parent = parent.parent;
                    if (parent == null || parent.name != row)
                        continue;
                    if (string.Equals(Tags.Replace(button.Label.text ?? "", "").Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                        return button;
                }
                return null;
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
                float progressFrom = 0f, float progressTo = 1f, float realBudget = float.MaxValue, bool waitOutDeath = false,
                Func<bool> until = null)
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
                    if (slowMotionUntil > 0f && Time.realtimeSinceStartup >= slowMotionUntil)
                        EndSlowMotion();
                    if (until != null && until()) { outcome = "checked"; break; }
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
                slowMotionUntil = 0f;
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
                    if (attempt == 0)
                        yield return Run.Until(() => (button = FindButton(text)) != null && button.IsInteractable(), timeout, $"the \"{text}\" button");
                    else
                    {
                        // The first click may still be working (New Game plays the house explosion before the video
                        // loads, and the menu animates out): if the button is gone, wait for the effect, don't fail.
                        float gone = Time.realtimeSinceStartup + 2f;
                        while (!Run.Done && Time.realtimeSinceStartup < gone && !((button = FindButton(text)) != null && button.IsInteractable()))
                            yield return null;
                        if (button == null || !button.IsInteractable())
                            break;
                    }
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

        /// <summary>
        /// The game page's tap-to-play sound prompt (Tools/Web/hawkes-sound.js, through Plugins/WebGL/HawkesSound.jslib).
        /// State() is the page's JSON, "" on a page without the prompt, null outside a WebGL build.
        /// </summary>
        static class PageSound
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            [DllImport("__Internal")]
            static extern int HawkesSoundTap();

            [DllImport("__Internal")]
            static extern string HawkesSoundState();

            public static bool Tap() => HawkesSoundTap() != 0;
            public static string State() => HawkesSoundState() ?? "";
#else
            public static bool Tap() => false;
            public static string State() => null;
#endif
        }

        /// <summary>
        /// The website pages around the game (Plugins/WebGL/HawkesSite.jslib), opened in a frame over the game page.
        /// The credit on them is installed by the deploy (GameDeployWindow step 7c).
        /// </summary>
        static class SitePages
        {
            /// <summary>Paths from the site root, a name for the timeline, and whether it's the landing page.</summary>
            public static readonly (string path, string name, bool landing)[] Pages =
            {
                ("", "Landing", true),
                ("blog/index.html", "Blog", false),
                ("blog/lore.html", "Lore", false),
                ("blog/lore/characters/overlord.html", "Lore: The Overlord", false),
                ("blog/posts/diamonds.html", "Blog post: Diamonds", false),
                ("blog/posts/resend-saved-my-sanity.html", "Blog post without a footer", false),
                ("offline.html", "Offline", false),
            };

            [Serializable]
            public class Link
            {
                public string href;
                public string text;
                public bool visible;
            }

            [Serializable]
            public class Check
            {
                public string path;
                public string url;
                public int status;
                public string error;
                public string title;
                public Link gen;
                public Link parent;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            [DllImport("__Internal")]
            static extern void HawkesSiteOpen(string path);

            [DllImport("__Internal")]
            static extern string HawkesSiteResult();

            [DllImport("__Internal")]
            static extern void HawkesSiteClose();

            [DllImport("__Internal")]
            static extern int HawkesSiteGamePageCredits();

            public static bool Available => true;
            public static void Open(string path) => HawkesSiteOpen(path);
            public static string Result() => HawkesSiteResult() ?? "";
            public static void Close() => HawkesSiteClose();
            public static int GamePageCredits() => HawkesSiteGamePageCredits();
#else
            public static bool Available => false;
            public static void Open(string path) { }
            public static string Result() => "";
            public static void Close() { }
            public static int GamePageCredits() => 0;
#endif
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
