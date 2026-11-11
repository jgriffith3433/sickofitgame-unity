using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A level's analytics (AGENTS.md "Analytics"), mapped from the game's own telemetry stream
    /// (<see cref="GameplayTelemetry.Recorded"/>: levelStart, floor, damage, signal, phase, shortcut, upgradeOffer,
    /// upgradePick) plus the moments telemetry doesn't see (the rewind's length, a death that stood, the boss bar, the
    /// results, leaving). Sends level_start/level_end, death (with what killed him), feature "rewind" (how far back),
    /// choice (Grudges, shortcuts), progress (each new floor climbed), the custom "boss" event (start, each phase
    /// signal, win/lose) and the boss's feedback card on the results. Rewind replays the same future, so every
    /// one-off event is sent once per attempt. Presentation only: it reads sim state, never changes it.
    /// </summary>
    public static class PlayAnalytics
    {
        /// <summary>The whole climb, Floor 1,000 up to the Overlord's on Floor 1 (progress steps are floors climbed).</summary>
        const int BottomFloor = 1000;

        sealed class Play
        {
            public GameplayWorld World;
            public int Level;
            public string Name = "";
            public bool Open;
            public int StartFloor;
            public int BestFloor;
            public int Rewinds;
            public float Rewound;
            public float RewindFrom = -1f;
            public bool RewindFromDeath;
            public string LastHitBy = "";
            public string Boss = "";
            public float BossSince;
            public bool BossWon;
            public bool BossOver;
            public GameClock Clock;
            public readonly HashSet<string> Once = new HashSet<string>();
        }

        static Play current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            current = null;
            GameplayTelemetry.Recorded -= OnRecorded;
            GameplayTelemetry.Recorded += OnRecorded;
        }

        static void OnRecorded(GameplayTelemetry log, GameplayTelemetry.Event e)
        {
            var world = log != null ? log.World : null;
            if (world == null)
                return;
            if (e.Kind == "levelStart")
            {
                Open(world);
                return;
            }
            var play = current;
            if (play == null || play.World != world || !play.Open)
                return;
            switch (e.Kind)
            {
                case "floor":
                    Floor(play, e.Value);
                    break;
                case "damage":
                    if (e.Subject == "player" && !string.IsNullOrEmpty(e.Detail))
                        play.LastHitBy = e.Detail;
                    break;
                case "phase":
                    AnalyticsManager.SetContext("phase", e.Subject);
                    break;
                case "signal":
                    if (!string.IsNullOrEmpty(play.Boss) && !play.BossOver && play.Once.Add("signal:" + e.Subject))
                        Boss(play, "phase", new Dictionary<string, object> { { "phase", e.Subject } });
                    break;
                case "shortcut":
                case "shortcut-declined":
                    if (play.Once.Add("shortcut:" + e.Subject))
                        AnalyticsManager.Choice("shortcut:" + e.Subject, e.Kind == "shortcut" ? "taken" : "climb",
                            new Dictionary<string, object> { { "floor", world.Stage.Floor }, { "level", play.Level } });
                    break;
                case "upgradePick":
                    if (play.Once.Add("pick:" + e.Detail))
                    {
                        AnalyticsManager.Choice("grudge", e.Subject, new Dictionary<string, object>
                        {
                            { "offered", e.Detail }, { "slot", e.Value + 1 }, { "level", play.Level }, { "floor", world.Stage.Floor },
                        });
                        AnalyticsManager.SetContext("grudges", Grudges().Count);
                    }
                    break;
            }
        }

        // ─── The level ─────────────────────────────────────────────────────

        static void Open(GameplayWorld world)
        {
            if (current != null && current.Open && current.World != world)
                Close(current, "quit");

            var play = current = new Play { World = world, Level = world.LevelNumber, Open = true };
            var config = GameDefinition.Instance != null ? GameDefinition.Instance.GetLevel(world.LevelNumber) : null;
            play.Name = config != null && !string.IsNullOrEmpty(config.displayName) ? config.displayName : world.LevelName;
            // The floor counter is set just after levelStart (StageProgress.Start), so the start floor is the script's.
            var stage = world.Stage;
            play.StartFloor = stage != null && stage.HasFloors ? stage.Floors.start : 0;
            play.BestFloor = play.StartFloor;
            string circle = CircleName(world, play.StartFloor);
            string mode = Mode(world);
            var grudges = Grudges();

            AnalyticsManager.SetContext(new Dictionary<string, object>
            {
                { "level_name", play.Name }, { "mode", mode }, { "floor", play.StartFloor > 0 ? (object)play.StartFloor : null },
                { "circle", circle }, { "grudges", grudges.Count }, { "phase", null }, { "boss", null },
            });
            AnalyticsManager.Screen("level");
            AnalyticsManager.CloseAsk();
            var props = new Dictionary<string, object>
            {
                { "name", play.Name }, { "mode", mode }, { "run", WaystoneProgress.RunsEnded + 1 },
                { "diamonds", RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0 },
            };
            if (play.StartFloor > 0)
            {
                props["floor"] = play.StartFloor;
                if (stage.Floors.end > 0) props["floor_end"] = stage.Floors.end;
            }
            if (circle != null) props["circle"] = circle;
            if (grudges.Count > 0) props["grudges"] = string.Join(",", grudges.Select(kv => kv.Value > 1 ? kv.Key + "x" + kv.Value : kv.Key));
            AnalyticsManager.LevelStart(play.Level, props);

            play.Clock = GameClock.Instance;
            if (play.Clock != null)
            {
                play.Clock.OnRewindStarted += OnRewindStarted;
                play.Clock.OnRewindStopped += OnRewindStopped;
            }
        }

        static void Close(Play play, string result)
        {
            if (play == null || !play.Open)
                return;
            play.Open = false;
            if (play.Clock != null)
            {
                play.Clock.OnRewindStarted -= OnRewindStarted;
                play.Clock.OnRewindStopped -= OnRewindStopped;
            }
            var world = play.World;
            var props = new Dictionary<string, object>
            {
                { "name", play.Name }, { "rewinds", play.Rewinds }, { "rewound", Round(play.Rewound) },
            };
            if (world != null)
            {
                props["game_time"] = Round(world.Now);
                props["kills"] = (int)world.GetCounter("kills");
                props["hits"] = (int)world.GetCounter("playerHits");
                if (world.Director != null && !string.IsNullOrEmpty(world.Director.PhaseName))
                    props["phase"] = world.Director.PhaseName;
            }
            if (play.StartFloor > 0)
            {
                props["floor"] = play.BestFloor;
                props["floors_climbed"] = play.StartFloor - play.BestFloor;
            }
            if (!string.IsNullOrEmpty(play.Boss))
                props["boss"] = play.Boss;
            AnalyticsManager.LevelEnd(play.Level, result, props);
            AnalyticsManager.SetContext(new Dictionary<string, object> { { "phase", null }, { "boss", null } });
        }

        /// <summary>The level was cleared (its results are final).</summary>
        public static void Completed(GameplayWorld world)
        {
            var play = For(world);
            if (play != null)
                Close(play, "complete");
        }

        /// <summary>The pause menu's Restart ("restarted") or Quit to menu ("quit").</summary>
        public static void Left(GameplayWorld world, string outcome)
        {
            var play = For(world);
            if (play != null)
                Close(play, outcome == "restarted" ? "restart" : "quit");
        }

        /// <summary>The level's scene is going away; a level still open (a script's exit, a reload) was left.</summary>
        public static void Unloaded(GameplayWorld world)
        {
            var play = For(world);
            if (play != null)
                Close(play, "quit");
        }

        /// <summary>A death that stood (the undo window ran out): what killed him, where, then the level's end.</summary>
        public static void Died(GameplayWorld world)
        {
            var play = For(world);
            if (play == null)
                return;
            var props = new Dictionary<string, object> { { "level", play.Level }, { "t", Round(world.Now) } };
            if (play.StartFloor > 0) props["floor"] = world.Stage.Floor;
            if (world.Player != null)
            {
                props["x"] = Round(world.Player.Position.x);
                props["y"] = Round(world.Player.Position.y);
            }
            if (world.Director != null && !string.IsNullOrEmpty(world.Director.PhaseName)) props["phase"] = world.Director.PhaseName;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget != null) props["juice"] = Round(budget.Juice);
            if (!string.IsNullOrEmpty(play.Boss)) props["boss"] = play.Boss;
            SickOfIt.Core.Progression.NextGoal.LastCause = play.LastHitBy ?? "";
            SickOfIt.Core.Progression.NextGoal.LastBoss = play.Boss ?? "";
            AnalyticsManager.Death(play.Level, string.IsNullOrEmpty(play.LastHitBy) ? "unknown" : play.LastHitBy, props);
            if (!string.IsNullOrEmpty(play.Boss) && !play.BossOver)
            {
                play.BossOver = true;
                Boss(play, "lose", null);
            }
            Close(play, "death");
        }

        static void Floor(Play play, int floor)
        {
            if (play.StartFloor <= 0 || floor <= 0)
                return;
            AnalyticsManager.SetContext("floor", floor);
            // Each floor higher than any reached this attempt (rewinds and repeated floors don't count twice).
            if (floor >= play.BestFloor)
                return;
            play.BestFloor = floor;
            string circle = CircleName(play.World, floor);
            AnalyticsManager.SetContext("circle", circle);
            AnalyticsManager.Progress(BottomFloor + 1 - floor, BottomFloor, "Floor " + floor,
                new Dictionary<string, object> { { "floor", floor }, { "level", play.Level } });
        }

        // ─── Rewind ────────────────────────────────────────────────────────

        static void OnRewindStarted()
        {
            var play = current;
            if (play == null || !play.Open || play.World == null)
                return;
            play.RewindFrom = play.World.Now;
            play.RewindFromDeath = play.World.Player != null && play.World.Player.Health <= 0;
        }

        static void OnRewindStopped()
        {
            var play = current;
            if (play == null || !play.Open || play.World == null || play.RewindFrom < 0f)
                return;
            float back = Mathf.Max(0f, play.RewindFrom - play.World.Now);
            play.RewindFrom = -1f;
            play.Rewinds++;
            play.Rewound += back;
            var props = new Dictionary<string, object> { { "seconds", Round(back) }, { "level", play.Level }, { "t", Round(play.World.Now) } };
            if (play.RewindFromDeath) props["undo_death"] = true;
            if (play.StartFloor > 0) props["floor"] = play.World.Stage.Floor;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget != null) props["juice"] = Round(budget.Juice);
            AnalyticsManager.Feature("rewind", props);
        }

        // ─── Bosses ────────────────────────────────────────────────────────

        /// <summary>The boss bar came up (GameplayHud.ShowBossBar): the fight starts.</summary>
        public static void BossShown(GameplayWorld world, string label, string id)
        {
            var play = For(world);
            string boss = !string.IsNullOrEmpty(id) ? id : label;
            if (play == null || string.IsNullOrEmpty(boss) || !play.Once.Add("boss:" + boss))
                return;
            play.Boss = boss;
            play.BossSince = world.Now;
            play.BossOver = false;
            AnalyticsManager.SetContext("boss", boss);
            Boss(play, "start", new Dictionary<string, object> { { "label", label ?? "" } });
        }

        /// <summary>The boss died (the bar went away with him).</summary>
        public static void BossDefeated(GameplayWorld world)
        {
            var play = For(world);
            if (play == null || string.IsNullOrEmpty(play.Boss) || play.BossOver || !play.Once.Add("bossWin"))
                return;
            play.BossOver = true;
            play.BossWon = true;
            Boss(play, "win", null);
        }

        static void Boss(Play play, string action, Dictionary<string, object> extra)
        {
            var props = new Dictionary<string, object>
            {
                { "boss", play.Boss }, { "action", action }, { "level", play.Level },
                { "seconds", Round(play.World != null ? play.World.Now - play.BossSince : 0f) },
            };
            if (play.World != null && play.World.Player != null) props["hero_health"] = play.World.Player.Health;
            if (extra != null)
                foreach (var kv in extra) props[kv.Key] = kv.Value;
            AnalyticsManager.Track("boss", props);
        }

        /// <summary>The results are up (a pause): after a boss win, the stars card.</summary>
        public static void ResultsShown(GameplayWorld world)
        {
            AnalyticsManager.Screen("results");
            var play = current != null && current.World == world ? current : null;
            if (play == null || !play.BossWon)
                return;
            string label = !string.IsNullOrEmpty(play.Boss) ? Title(play.Boss) : "that";
            AnalyticsManager.Ask("boss-" + play.Boss, $"{label}. How was that fight?", $"boss {play.Boss}, level {play.Level}", stars: true, comment: true);
        }

        // ─── Helpers ───────────────────────────────────────────────────────

        static Play For(GameplayWorld world)
        {
            var play = current;
            return play != null && world != null && play.World == world && play.Open ? play : null;
        }

        static string Mode(GameplayWorld world) =>
            world.LevelName != null && world.LevelName.StartsWith("Arcade", System.StringComparison.Ordinal) ? "arcade" : "story";

        static string CircleName(GameplayWorld world, int floor)
        {
            var circle = world.Stage != null ? world.Stage.Circle : null;
            if (circle == null && floor > 0 && world.Content != null)
                circle = world.Content.Circles.Values.FirstOrDefault(c => c != null && floor <= c.firstFloor && floor >= c.lastFloor);
            if (circle == null)
                return null;
            return !string.IsNullOrEmpty(circle.displayName) ? circle.displayName : circle.id;
        }

        static Dictionary<string, int> Grudges()
        {
            try { return UpgradeProgress.Owned(); }
            catch { return new Dictionary<string, int>(); }
        }

        static string Title(string id) => string.IsNullOrEmpty(id) ? id
            : System.Text.RegularExpressions.Regex.Replace(char.ToUpperInvariant(id[0]) + id.Substring(1), "(?<=[a-z])(?=[A-Z])", " ");

        static float Round(float v) => Mathf.Round(v * 10f) / 10f;
    }
}
