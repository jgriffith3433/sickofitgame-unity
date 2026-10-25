using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Waystones and runs (Docs/Story.md, "Waystones and runs"). Hell doesn't let the hero die; it sends him back to
    /// repeat the floors. A Waystone is where a run starts: the Landing at the bottom (Floor 1,000) and the top floor
    /// of every Circle once its Warden is beaten (901, 801, ... 101: the Shaft's bands, ui/shaft.json). Dying on a
    /// floor stage ends the run: the death scene, then the Shaft drops the hero's marker back to the highest Waystone
    /// reached and the run restarts with the level that starts there. Grudges and the health carried between levels
    /// go with the run; diamonds and Yooba's goods stay. The Meadow and the arcade levels have no floors, so a death
    /// there just plays the level again. Saved in <see cref="WaystoneProgress"/>.
    /// </summary>
    public static class RunFlow
    {
        /// <summary>The Waystone floors from the bottom up: the bottom of Hell (the Landing), then each Circle's top floor under the Overlord's.</summary>
        public static List<int> Waystones()
        {
            var config = GameDefinition.Instance != null ? GameDefinition.Instance.shaft : null;
            int bottom = config != null ? Mathf.Max(1, config.bottomFloor) : 1000;
            int top = config != null ? config.topFloor : 1;
            var list = new List<int> { bottom };
            if (config != null && config.bands != null)
            {
                foreach (var band in config.bands)
                {
                    if (band != null && band.lastFloor > top && band.lastFloor < bottom && !list.Contains(band.lastFloor))
                        list.Add(band.lastFloor);
                }
            }
            list.Sort((a, b) => b.CompareTo(a));
            return list;
        }

        public static bool IsWaystone(int floor) => floor > 0 && Waystones().Contains(floor);

        /// <summary>Where a run starts now: the highest Waystone reached, or the Landing before any.</summary>
        public static int CurrentWaystone
        {
            get
            {
                int saved = WaystoneProgress.Highest;
                return saved > 0 ? saved : Waystones()[0];
            }
        }

        /// <summary>The level a run starting at a Waystone plays first: the stage whose floors start there (the Landing) or just above it (900 above 901), never the Warden's floor that lit it; 0 if none is built yet.</summary>
        public static int LevelFrom(int waystone)
        {
            var definition = GameDefinition.Instance;
            if (definition == null || waystone <= 0)
                return 0;
            for (int n = 1; n <= definition.TotalLevels; n++)
            {
                ShaftScreen.LevelFloors(n, out int start, out int end);
                // Not the Warden's own floor (Floor 901 starts and ends on its Waystone): the run starts above it.
                if (end > 0 && end != waystone && (start == waystone || start == waystone - 1))
                    return n;
            }
            return 0;
        }

        /// <summary>A level started. A floor stage is where the run is now (main menu Continue); one that starts on a Waystone (the Landing) reaches it.</summary>
        public static void LevelStarted(int level)
        {
            if (ReplayModes.IsReplayLevel(level))
                return;
            ShaftScreen.LevelFloors(level, out int start, out int end);
            if (start <= 0 || end <= 0)
                return;
            WaystoneProgress.RunLevel = level;
            WaystoneProgress.DiedOnFloor = 0;
            if (!runOpen)
            {
                runOpen = true;
                runStartedAt = Time.realtimeSinceStartup;
                runFromFloor = start;
                AnalyticsManager.RunStart(new Dictionary<string, object>
                {
                    { "run", WaystoneProgress.RunsEnded + 1 }, { "mode", "story" }, { "level", level }, { "floor", start },
                    { "waystone", CurrentWaystone },
                });
            }
            // A Warden's floor (start = end = its Waystone) lights it when he is beaten, not when the fight starts.
            if (IsWaystone(start) && end < start && WaystoneProgress.Reach(start))
            {
                Debug.Log($"[Runs] Waystone reached: FLOOR {start} (level {level} starts on it)");
                WaystoneReached(start, level);
            }
        }

        // ─── Analytics (AGENTS.md "Analytics"): a run starts on its first floor stage this session, ends on a death ───
        static bool runOpen;
        static float runStartedAt;
        static int runFromFloor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => runOpen = false;

        static void CloseRun(string result, int level, int floor)
        {
            if (!runOpen && result != "lose")
                return;
            var props = new Dictionary<string, object> { { "run", WaystoneProgress.RunsEnded + (result == "lose" ? 0 : 1) }, { "mode", "story" }, { "level", level } };
            if (floor > 0) props["floor"] = floor;
            if (runOpen)
            {
                props["duration"] = Mathf.Round(Time.realtimeSinceStartup - runStartedAt);
                if (floor > 0 && runFromFloor > 0) props["floors_climbed"] = Mathf.Max(0, runFromFloor - floor);
            }
            runOpen = false;
            AnalyticsManager.RunEnd(result, props);
        }

        /// <summary>New Game over a run: it is dropped (analytics run_end quit).</summary>
        public static void Abandon() => CloseRun("quit", WaystoneProgress.RunLevel, 0);

        static void WaystoneReached(int floor, int level)
        {
            var props = new Dictionary<string, object> { { "floor", floor }, { "level", level }, { "waystone", true } };
            AnalyticsManager.Progress(1001 - floor, 1000, "Waystone " + floor, props);
            AnalyticsManager.Track("checkpoint", new Dictionary<string, object> { { "step", 1001 - floor }, { "label", "Waystone " + floor }, { "floor", floor } });
        }

        /// <summary>A level was cleared: the run goes on from 'next' (0 = nothing on the path); a stage that ends on a Waystone (its Warden beaten) reaches it.</summary>
        public static void LevelCleared(int level, int next)
        {
            if (ReplayModes.IsReplayLevel(level))
                return;
            if (next > 0)
                WaystoneProgress.RunLevel = next;
            ShaftScreen.LevelFloors(level, out int start, out int end);
            // The Warden's floor lights it (Floor 901: start = end); the floor below that only climbs up to it (902 to 901) doesn't.
            if (end > 0 && IsWaystone(end) && start != end + 1 && WaystoneProgress.Reach(end))
            {
                Debug.Log($"[Runs] Waystone reached: FLOOR {end} (level {level} cleared)");
                WaystoneReached(end, level);
            }
        }

        /// <summary>The floor the hero is on as he dies (the drop starts there). Presentation memory only.</summary>
        public static void HeroDied(int floor)
        {
            if (floor > 0)
                WaystoneProgress.DiedOnFloor = floor;
        }

        /// <summary>
        /// The death scene of a death on 'level': on a floor stage the run is over. Grudges and carried health go, the
        /// run is set to restart at the Waystone, and 'stop' is the Shaft's drop. False (nothing changed) on a level
        /// without floors: it is just played again.
        /// </summary>
        public static bool EndRun(int level, out ShaftStop stop)
        {
            stop = default;
            // Endless Floor and the Daily Challenge are not the climb: a death there plays them again.
            if (ReplayModes.IsReplayLevel(level))
                return false;
            ShaftScreen.LevelFloors(level, out int start, out int end);
            if (start <= 0 || end <= 0)
                return false;
            int waystone = CurrentWaystone;
            int restart = LevelFrom(waystone);
            if (restart <= 0)
            {
                Debug.LogWarning($"[Runs] No level starts at the Waystone on FLOOR {waystone}; the run restarts on level {level}.");
                restart = level;
            }
            int high = Mathf.Max(start, end), low = Mathf.Min(start, end);
            int died = WaystoneProgress.DiedOnFloor;
            int from = died >= low && died <= high ? died : start;
            WaystoneProgress.DiedOnFloor = 0;

            RogueliteManager.Instance?.EndRun();
            WaystoneProgress.RunsEnded++;
            CloseRun("lose", level, from);
            WaystoneProgress.RunLevel = restart;
            Debug.Log($"[Runs] Run {WaystoneProgress.RunsEnded} ended on level {level} at FLOOR {from}: Grudges reset, back to the Waystone on FLOOR {waystone}, restarting on level {restart}");
            stop = new ShaftStop { Kind = ShaftStopKind.AfterDeath, Level = level, NextLevel = restart, FromFloor = from, Waystone = waystone };
            return true;
        }

        /// <summary>
        /// Main menu New Run: the current run is dropped (its Grudges and carried health go) and a new one starts at
        /// the highest Waystone. Returns the level to load, or 0 when no Waystone has been reached yet.
        /// </summary>
        public static int NewRun()
        {
            if (WaystoneProgress.Highest <= 0)
                return 0;
            int level = LevelFrom(WaystoneProgress.Highest);
            if (level <= 0)
                return 0;
            CloseRun("quit", WaystoneProgress.RunLevel, 0);
            RogueliteManager.Instance?.EndRun();
            WaystoneProgress.RunLevel = level;
            Debug.Log($"[Runs] New Run from the Waystone on FLOOR {WaystoneProgress.Highest}: level {level}");
            return level;
        }

        /// <summary>
        /// Main menu New Game+ (Docs/Story.md, "Again"): the climb starts again at the Landing with the next cycle's harder Hell. Returns the level to
        /// load, or 0 when it isn't offered yet.
        /// </summary>
        public static int NewGamePlus()
        {
            if (!ReplayModes.NewGamePlusOpen)
                return 0;
            int level = LevelFrom(Waystones()[0]);
            if (level <= 0)
                return 0;
            CloseRun("quit", WaystoneProgress.RunLevel, 0);
            RogueliteManager.Instance?.StartNewGamePlus();
            ReplayModes.StartNextCycle();
            WaystoneProgress.RunLevel = level;
            Debug.Log($"[Runs] New Game+ cycle {ReplayModes.Cycle}: level {level}, weapons and goods kept");
            return level;
        }

        /// <summary>The level main menu Continue opens: the current run's, else the highest level reached.</summary>
        public static int ContinueLevel()
        {
            var definition = GameDefinition.Instance;
            int run = WaystoneProgress.RunLevel;
            if (run > 0 && definition != null && run <= definition.TotalLevels)
                return run;
            return GameProgressManager.Instance != null ? GameProgressManager.Instance.GetCurrentLevel() : 1;
        }
    }
}
