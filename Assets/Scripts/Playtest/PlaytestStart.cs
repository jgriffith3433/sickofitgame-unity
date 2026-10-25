using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Progression;
using SickOfIt.Data;

namespace SickOfIt.Playtest
{
    /// <summary>
    /// Options with a start point, for the editor and tests: <c>HawkesPlaytest.StartScenario("start-at", new PlaytestStartOptions { at = "905" })</c>.
    /// In a build the page brings it (the contract's optional <c>at</c>, or <c>?at=</c> next to <c>?playtest=</c>).
    /// </summary>
    public sealed class PlaytestStartOptions : PlaytestOptions
    {
        public string at;
    }

    /// <summary>
    /// Start points (AGENTS.md "Playtests", "Start points"): a playtest-only way to start at any floor. <c>at</c> names a floor
    /// ("905", "Floor 905") or a level ("level 31", "L31", "Level14"); the scenario loads that level directly (a setup shortcut) after
    /// setting the save the way a player arriving there on the played route would have it (<see cref="PrepareStart"/>).
    /// Everything comes from the data: the levels and their floors from game.json and the level scripts, the Waystones from
    /// ui/shaft.json, the shortcuts from the route. Players never reach it: it runs only inside a playtest scenario.
    ///
    /// The all-levels run also comes in parts (<see cref="Chunks"/>, ids all-levels-1..N) that cover it exactly and can run side by side.
    /// </summary>
    public static partial class SickOfItPlaytest
    {
        /// <summary>start-at: the level or floor in <c>at</c> (else the Landing), played with the level-N checks.</summary>
        public const string StartAtId = "start-at";
        const string StartAtDefault = "1000";
        const float StartAtSeconds = 420f;

        /// <summary>all-levels-K: the all-levels run in parts.</summary>
        public const string ChunkPrefix = "all-levels-";
        /// <summary>A part of the off-path levels is at most this many minutes of estimated play, so a part fits the runner's hour with its video.</summary>
        const float ChunkMinutes = 30f;

        // ─── Where a run starts ─────────────────────────────────────────

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void SickOfItPlaytestWatchStart();

        [DllImport("__Internal")]
        static extern string SickOfItPlaytestStartAt(string id);
#endif

        /// <summary>Called once the bridge is installed: the page keeps the <c>at</c> that start() was given (the package passes on only seed and speed).</summary>
        static void WatchStartPoint()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { SickOfItPlaytestWatchStart(); }
            catch (Exception e) { Debug.LogWarning("[Playtest] couldn't watch start() for 'at': " + e.Message); }
#endif
        }

        /// <summary>The start point asked for with scenario 'id': the editor's <see cref="PlaytestStartOptions"/>, else the page's <c>at</c>; null if none.</summary>
        public static string RequestedAt(string id, PlaytestOptions options)
        {
            if (options is PlaytestStartOptions start && !string.IsNullOrWhiteSpace(start.at))
                return start.at.Trim();
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string at = SickOfItPlaytestStartAt(id);
                if (!string.IsNullOrWhiteSpace(at))
                    return at.Trim();
            }
            catch (Exception e) { Debug.LogWarning("[Playtest] couldn't read 'at': " + e.Message); }
#endif
            return null;
        }

        static readonly Regex LevelAt = new Regex(@"^(?:level|lvl|l)[\s\-_]*(\d+)$", RegexOptions.IgnoreCase);
        static readonly Regex FloorAt = new Regex(@"^(?:floor|f)?[\s\-_]*(\d+)$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The level <c>at</c> names, or 0 with the reason in 'what'. A floor ("905", "Floor 905") is the played route's level for it:
        /// the one-floor level "FloorN" on the path, else the level whose floors start there, else the stage whose floors hold it (an old
        /// 10-floor stage starts at its first floor). A level: "level 31", "L31", or a level file's name ("Level14", "Floor905").
        /// A bare number that is no floor but a level number (1-99) is that level.
        /// </summary>
        public static int ResolveAt(string at, out string what)
        {
            what = "";
            var definition = GameDefinition.Instance;
            if (definition == null || string.IsNullOrWhiteSpace(at))
            {
                what = "no start point";
                return 0;
            }
            string s = at.Trim().Replace(",", "");
            int total = definition.TotalLevels;

            // A level file's name, as game.json lists it.
            for (int n = 1; n <= total; n++)
            {
                var level = definition.GetLevel(n);
                if (level != null && string.Equals(level.levelName, s, StringComparison.OrdinalIgnoreCase))
                {
                    what = $"level {n} ({level.levelName})";
                    return n;
                }
            }
            var byLevel = LevelAt.Match(s);
            if (byLevel.Success)
            {
                int n = int.Parse(byLevel.Groups[1].Value);
                if (n >= 1 && n <= total)
                {
                    what = $"level {n}";
                    return n;
                }
                what = $"there is no level {n} (game.json has {total})";
                return 0;
            }
            var byFloor = FloorAt.Match(s);
            if (!byFloor.Success)
            {
                what = $"\"{at}\" is neither a floor (\"905\", \"Floor 905\") nor a level (\"level 31\", \"Level14\")";
                return 0;
            }
            int floor = int.Parse(byFloor.Groups[1].Value);
            int found = FloorLevel(floor);
            if (found > 0)
            {
                ShaftScreen.LevelFloors(found, out int start, out _);
                what = start == floor ? $"Floor {floor} (level {found})" : $"Floor {floor} is inside level {found}, which starts on Floor {start}";
                return found;
            }
            if (floor >= 1 && floor <= total && !Regex.IsMatch(s, "^f", RegexOptions.IgnoreCase))
            {
                what = $"level {floor} (no Floor {floor} is built)";
                return floor;
            }
            what = $"no level is built for Floor {floor}";
            return 0;
        }

        /// <summary>The played route's level for a floor (see <see cref="ResolveAt"/>), or 0.</summary>
        static int FloorLevel(int floor)
        {
            if (floor <= 0)
                return 0;
            foreach (int n in PathLevels())
                if (PathFloor(n) == floor)
                    return n;
            var all = AllGameLevels();
            foreach (int n in all)
            {
                ShaftScreen.LevelFloors(n, out int start, out _);
                if (start == floor)
                    return n;
            }
            foreach (int n in all)
            {
                ShaftScreen.LevelFloors(n, out int start, out int end);
                if (start > 0 && end > 0 && floor <= start && floor >= end)
                    return n;
            }
            return 0;
        }

        /// <summary>
        /// The save a player arriving at level 'n' on the played route would have (a setup shortcut, playtest only), set before the
        /// level loads: the Shaft plunged and its marker on the level's first floor, the Waystones lit below it (the Landing, then
        /// each Circle's top whose Warden is under him: Floor 900 has 901 lit, 901 itself not yet), the run on this level, the route's
        /// shortcuts already taken recorded for the Shaft and the one that leads here as his way in (Floor 910 after the King's
        /// Chimney reads shortcutTaken), Yooba met past the Landing. 'newRun': also a fresh run, as a Waystone start gives (no Grudges,
        /// full health; diamonds and Yooba's goods kept). The Meadow (Level 1) is a New Game's first level and keeps the save as it is
        /// (its run ends in the portal and the first plunge). Returns what it set, for the event.
        /// </summary>
        public static string PrepareStart(int n, bool newRun)
        {
            var definition = GameDefinition.Instance;
            if (definition == null || n < 1 || n > definition.TotalLevels)
                return "nothing (no such level)";
            var said = new List<string>();
            if (newRun && RogueliteManager.Instance != null)
            {
                RogueliteManager.Instance.EndRun();
                said.Add("a fresh run (no Grudges, full health)");
            }
            string moves = TakeBackLockedMoves(n);
            if (moves != null)
                said.Add(moves);
            if (n == 1)
            {
                said.Add("the Meadow: the save left as it is");
                return string.Join("; ", said);
            }

            ShaftScreen.LevelFloors(n, out int start, out int end);
            ShaftProgress.Plunged = true;
            if (start > 0)
            {
                ShaftProgress.Floor = start;

                // Waystones: exactly the ones under him, so a death drops to the right one (a browser's old save may have more).
                int runsEnded = WaystoneProgress.RunsEnded;
                WaystoneProgress.Reset();
                WaystoneProgress.RunsEnded = runsEnded;
                int lit = 0;
                foreach (int waystone in RunFlow.Waystones())
                {
                    if (waystone > start && WaystoneProgress.Reach(waystone))
                        lit = waystone;
                }
                WaystoneProgress.RunLevel = n;
                said.Add($"the Shaft's marker on FLOOR {start}");
                said.Add(lit > 0 ? $"the Waystone at FLOOR {lit} lit" : "no Waystone yet (he reaches the Landing's as it starts)");
            }
            else
                said.Add("no floors (an arcade level): the Shaft and the Waystones left as they are");

            // The route's shortcuts on the way here (the path takes them), and the one he came in by.
            ShortcutProgress.Reset();
            string arrival = null;
            var path = PathLevels();
            bool onPath = path.Contains(n);
            var taken = new List<string>();
            foreach (var shortcut in RouteShortcuts)
            {
                if (!onPath || shortcut.next > n || !path.Contains(shortcut.next))
                    continue;
                ShaftScreen.LevelFloors(shortcut.level, out _, out int from);
                ShortcutProgress.Record(shortcut.id, from, shortcut.toFloor);
                taken.Add($"{shortcut.id} {from}->{shortcut.toFloor}");
                if (shortcut.next == n)
                    arrival = shortcut.id;
            }
            ShortcutProgress.SetArrival(arrival, n);
            said.Add(taken.Count > 0 ? "shortcuts taken: " + string.Join(", ", taken) : "no shortcuts taken");
            if (arrival != null)
                said.Add($"came in by {arrival} (shortcutTaken)");

            // Yooba says hello on the Shaft after the Landing's video; any later level has met him.
            bool met = n > LandingLevel;
            YoobaProgress.Met = met;
            YoobaProgress.HeroDied = false;
            said.Add(met ? "Yooba met" : "Yooba not met yet");
            return string.Join("; ", said);
        }

        /// <summary>
        /// The dash and the wall-jump are the story's to give (player.json moves.*.unlockWhen "level:K": K cleared). A local browser or
        /// editor save keeps what an earlier scenario (moves, wall-jump, hero-anim) unlocked, so a bot warped to Floor 1000 or 998
        /// clung to the ledges' corners and hung there. Takes back the clears of level n and later, and the flags they gave, so a
        /// floor starts with the moves a player arriving there has (Yooba's dash is bought, so it comes back by itself).
        /// </summary>
        static string TakeBackLockedMoves(int n)
        {
            var moves = GameDefinition.Instance != null && GameDefinition.Instance.player != null ? GameDefinition.Instance.player.moves : null;
            if (moves == null || GameProgressManager.Instance == null)
                return null;
            var taken = new List<string>();
            void Take(string flag, List<string> when)
            {
                bool locked = false;
                if (when != null)
                {
                    foreach (string requirement in when)
                    {
                        string[] parts = requirement.Split(':');
                        if (parts.Length == 2 && parts[0] == "level" && int.TryParse(parts[1], out int level) && level >= n)
                        {
                            PlayerPrefs.DeleteKey($"Level_{level}_Completed");
                            locked = true;
                        }
                    }
                }
                if (locked && UnlockFlags.Has(flag))
                {
                    UnlockFlags.Remove(flag);
                    taken.Add(flag);
                }
            }
            Take(string.IsNullOrEmpty(moves.dash.unlockFlag) ? SickOfIt.Gameplay.HeroMoves.DashFlag : moves.dash.unlockFlag, moves.dash.unlockWhen);
            Take(string.IsNullOrEmpty(moves.wallJump.unlockFlag) ? SickOfIt.Gameplay.HeroMoves.WallJumpFlag : moves.wallJump.unlockFlag, moves.wallJump.unlockWhen);
            PlayerPrefs.Save();
            return taken.Count > 0 ? "moves not his yet on this floor, taken back: " + string.Join(", ", taken) : null;
        }

        /// <summary>
        /// Trims a run's levels to start at <c>at</c>: from that level on, if it is in the list. Returns false (with the reason) if
        /// <c>at</c> names no level or one the run doesn't play.
        /// </summary>
        static bool StartAtIn(List<int> levels, string at, out List<int> from, out string what)
        {
            from = levels;
            int n = ResolveAt(at, out what);
            if (n <= 0)
                return false;
            int i = levels.IndexOf(n);
            if (i < 0)
            {
                what += $": not in this run ({PathRange(levels)})";
                return false;
            }
            from = levels.GetRange(i, levels.Count - i);
            return true;
        }

        // ─── all-levels in parts ────────────────────────────────────────

        /// <summary>
        /// The all-levels run in parts, in its order, that together play every level once: Level 1 with the path's first stretch
        /// (the Meadow and Floors 1000-991), then each later stretch of the path (960-951, 910-901 with the Magma King, then the Cracked
        /// Road's 900-891, 860-851, 810-801: each ends on a floor boss or Warden or where a shortcut jumps, <see cref="PathStretches"/>),
        /// then the levels after the path (the long way's old stages and the arcade levels) in parts of at most ChunkMinutes of
        /// estimated play. All from game.json, so new floors land in a part by themselves.
        /// </summary>
        public static List<(string title, List<int> levels)> Chunks()
        {
            var chunks = new List<(string, List<int>)>();
            var path = PathLevels();
            var stretches = PathStretches();
            var covered = new HashSet<int>();
            for (int i = 0; i < stretches.Count; i++)
            {
                var stretch = stretches[i];
                var levels = path.FindAll(n => n >= stretch.from && n <= stretch.to || i == 0 && n < stretch.from);
                if (levels.Count == 0)
                    continue;
                levels.ForEach(n => covered.Add(n));
                string floors = $"Floors {PathFloor(stretch.from)}-{PathFloor(stretch.to)}";
                chunks.Add((levels[0] < stretch.from ? $"Level 1 (the Meadow) and {floors}" : floors, levels));
            }
            // Anything on the path no stretch holds (none today) goes with the levels after it.
            // In even parts: as few as fit ChunkMinutes each, then the levels shared out evenly between them.
            var rest = AllGameLevels().FindAll(n => !covered.Contains(n));
            float total = 0f;
            foreach (int n in rest)
                total += LevelMinutes(n);
            int parts = Mathf.Max(1, Mathf.CeilToInt(total / ChunkMinutes));
            float share = total / parts;
            var part = new List<int>();
            float minutes = 0f;
            int made = 0;
            foreach (int n in rest)
            {
                float estimate = LevelMinutes(n);
                if (part.Count > 0 && minutes + estimate / 2f > share && made < parts - 1)
                {
                    chunks.Add(($"after the path: {PathRange(part)}", part));
                    made++;
                    part = new List<int>();
                    minutes = 0f;
                }
                part.Add(n);
                minutes += estimate;
            }
            if (part.Count > 0)
                chunks.Add(($"after the path: {PathRange(part)}", part));
            return chunks;
        }

        /// <summary>A level's estimated real minutes in the run: its time limit and its load and results.</summary>
        static float LevelMinutes(int n) => (PathLevelSeconds(n) + 45f) / 60f;

        static string ChunkId(int index) => ChunkPrefix + (index + 1);

        /// <summary>The part an id names (all-levels-K), or false.</summary>
        static bool TryChunk(string id, out (string title, List<int> levels) chunk, out int index)
        {
            chunk = default;
            index = -1;
            if (id == null || !id.StartsWith(ChunkPrefix, StringComparison.Ordinal) || !int.TryParse(id.Substring(ChunkPrefix.Length), out int k))
                return false;
            var chunks = Chunks();
            if (k < 1 || k > chunks.Count)
                return false;
            index = k - 1;
            chunk = chunks[index];
            return true;
        }

        /// <summary>The start-at scenario and the all-levels parts, for the scenario list.</summary>
        static void AddStartScenarios(List<PlaytestScenario> list, string pathHow)
        {
            list.Add(new PlaytestScenario(StartAtId, "Start at any floor (a setup shortcut, playtest only): the floor or level in the contract's " +
                "optional \"at\" (\"905\", \"Floor 905\", \"level 31\", \"Level14\"; also ?at= next to ?playtest= on the page), the Landing (Floor 1000) " +
                "without one, loaded straight away with the save a player arriving there would have (the Shaft's marker on that floor, the Waystones " +
                "under it lit, the route's shortcuts taken and the one he came in by, Yooba met past the Landing, a fresh run: no Grudges, full health), " +
                "then played like level-N: its stage checks, the results, the Shaft's climb and what comes next. A start event says what it set. " +
                "Options: at", StartAtSeconds, "level", "start"));
            var chunks = Chunks();
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                list.Add(new PlaytestScenario(ChunkId(i), $"all-levels, part {i + 1} of {chunks.Count}: {chunk.title} ({PathRange(chunk.levels)}); the parts " +
                    "together play every level once and can run side by side. It warps to its first floor (the start-at save, a fresh run), then " +
                    "each level the same way (the save that level expects, the run's Grudges kept), and logs a cleared, died or stuck event per floor " +
                    "with the all-levels table; \"at\" starts it later in the part: " + pathHow, PathRunSeconds(chunk.levels), "full", "levels", "chunk"));
            }
        }

        /// <summary>The start-at, all-levels part and at-aware runs; false if 'id' is none of them.</summary>
        static bool TryStartRun(Session session, string id, string at, out System.Collections.IEnumerator run)
        {
            run = null;
            if (id == StartAtId)
            {
                run = session.StartAt(at ?? StartAtDefault, at == null);
                return true;
            }
            if (TryChunk(id, out var chunk, out int index))
            {
                run = session.AllLevels(chunk.levels, $"all-levels part {index + 1} ({chunk.title})", checkEvery: true, at: at);
                return true;
            }
            return false;
        }

        sealed partial class Session
        {
            /// <summary>The first level AllLevels opened (0 before): only that one starts a fresh run.</summary>
            int preparedFirst;

            /// <summary>The start-at save for level n, logged as a start event.</summary>
            public void Prepare(int n, bool newRun)
            {
                string set = PrepareStart(n, newRun);
                Run.Event("start-point", $"level {n}{(PathFloor(n) > 0 ? $" (Floor {PathFloor(n)})" : "")}: {set}");
            }

            /// <summary>start-at: resolve, set the save, then the level-N run.</summary>
            public System.Collections.IEnumerator StartAt(string at, bool byDefault)
            {
                int n = ResolveAt(at, out string what);
                Run.Event("start-at", $"at \"{at}\"{(byDefault ? " (no at given: the Landing)" : "")}: {(n > 0 ? what : "no start: " + what)}");
                if (n <= 0)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"start-at: {what}");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                Run.Metric("startAt.level", n);
                if (PathFloor(n) > 0)
                    Run.Metric("startAt.floor", PathFloor(n));
                Prepare(n, true);
                yield return LevelScenario(n);
            }

            /// <summary>
            /// wastes-climb, landing-stretch, foreman-stretch: warp to their first floor (or the one in "at", inside their range) with the
            /// save it expects, then the real flow on to their last, as before. "at" = "905" plays Floors 905-901 of the Heat Stairs.
            /// </summary>
            public System.Collections.IEnumerator ClimbFromStart(int first, int last, float seconds, string at)
            {
                if (at != null)
                {
                    int n = ResolveAt(at, out string what);
                    if (n < first || n > last)
                    {
                        Run.Problem(PlaytestSeverity.Bug, $"start point \"{at}\": {(n > 0 ? what + $", not in this run (levels {first}-{last})" : what)}");
                        End(PlaytestOutcome.Error);
                        yield break;
                    }
                    Run.Event("start-at", $"from {what} (levels {n}-{last} of {first}-{last})");
                    first = n;
                }
                Prepare(first, true);
                yield return WastesClimb(first, last, seconds);
            }

            /// <summary>
            /// AllLevels' hook before it loads level n directly: the save that level expects; the run's first level is also a fresh run
            /// (the warp to the part's first floor), later ones keep the Grudges the bot picked on the way, as a player's run would.
            /// </summary>
            void PrepareOpen(int n)
            {
                Prepare(n, preparedFirst == 0);
                if (preparedFirst == 0)
                    preparedFirst = n;
            }
        }
    }
}
