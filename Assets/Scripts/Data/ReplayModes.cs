using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Data
{
    /// <summary>What a replay mode does to Hell on top of the floor's own tables: multipliers on enemies and the generator's pushes.</summary>
    public struct ReplayScale
    {
        public float Health, AttackSpeed, MoveSpeed, ProjectileSpeed, Size, BreatherScale;
        public int ExtraAlive, ExtraPush;

        public static ReplayScale None => new ReplayScale
        {
            Health = 1f, AttackSpeed = 1f, MoveSpeed = 1f, ProjectileSpeed = 1f, Size = 1f, BreatherScale = 1f,
        };

        /// <summary>True when it changes anything.</summary>
        public bool Active =>
            !Mathf.Approximately(Health, 1f) || !Mathf.Approximately(AttackSpeed, 1f) || !Mathf.Approximately(MoveSpeed, 1f)
            || !Mathf.Approximately(ProjectileSpeed, 1f) || !Mathf.Approximately(Size, 1f) || !Mathf.Approximately(BreatherScale, 1f)
            || ExtraAlive != 0 || ExtraPush != 0;
    }

    /// <summary>One score in the Daily Challenge's local table.</summary>
    public struct DailyScore
    {
        public string Date;
        public long Attempt;
        public int Score, Floors;
        public bool Cleared;
    }

    /// <summary>
    /// Endless Floor, the Daily Challenge and New Game Plus (Docs/Story.md, "Again"): what is saved (the best Endless floor, the Daily's
    /// local score table, the New Game+ cycle) and how they scale Hell. Kept in PlayerPrefs; the best floor and the table are the player's
    /// own and stay through New Game, the cycle is cleared by it.
    /// </summary>
    public static class ReplayModes
    {
        const string EndlessKey = "Replay_EndlessBest";
        const string TableKey = "Replay_DailyTable";
        const string CycleKey = "Replay_NgCycle";

        /// <summary>Playtest only: pretend it is this day (yyyyMMdd). Empty = the real one.</summary>
        public static string DateOverride = "";

        static ReplayConfig Config => GameDefinition.Instance != null ? GameDefinition.Instance.replay : null;

        // ─── Which level is which ─────────────────────────────────────

        public static string EndlessLevelName => Config != null ? Config.endlessLevel : "EndlessFloor";
        public static string DailyLevelName => Config != null ? Config.dailyLevel : "DailyFloor";

        /// <summary>The level number (position in game.json's list) of a level file's name, or 0.</summary>
        public static int LevelNumber(string levelName)
        {
            var definition = GameDefinition.Instance;
            if (definition == null || string.IsNullOrEmpty(levelName))
                return 0;
            for (int n = 1; n <= definition.TotalLevels; n++)
            {
                var level = definition.GetLevel(n);
                if (level != null && level.levelName == levelName)
                    return n;
            }
            return 0;
        }

        /// <summary>A replay level (Endless Floor or the Daily): a death there is not a run's end, no Waystone, no Shaft.</summary>
        public static bool IsReplayLevel(int level)
        {
            var definition = GameDefinition.Instance;
            var data = definition != null && level > 0 && level <= definition.TotalLevels ? definition.GetLevel(level) : null;
            return data != null && (data.levelName == EndlessLevelName || data.levelName == DailyLevelName);
        }

        // ─── Endless Floor ────────────────────────────────────────────

        /// <summary>The most floors climbed in one Endless Floor play (0 = none yet).</summary>
        public static int EndlessBest => PlayerPrefs.GetInt(EndlessKey, 0);

        /// <summary>Saves the floors climbed if it beats the best. True when it is a new best.</summary>
        public static bool RecordEndless(int floorsClimbed)
        {
            if (floorsClimbed <= EndlessBest)
                return false;
            PlayerPrefs.SetInt(EndlessKey, floorsClimbed);
            PlayerPrefs.Save();
            return true;
        }

        // ─── Daily Challenge ──────────────────────────────────────────

        /// <summary>Today's date as the Daily's key (yyyyMMdd, UTC, so everyone's day changes together).</summary>
        public static string TodayKey => string.IsNullOrEmpty(DateOverride) ? DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : DateOverride;

        /// <summary>A stable hash of a day (FNV-1a over its characters: the same on every machine, unlike string.GetHashCode).</summary>
        public static uint DailySeed(string dateKey)
        {
            uint h = 2166136261u;
            foreach (char c in dateKey ?? "")
            {
                h ^= c;
                h *= 16777619u;
            }
            return h == 0 ? 1u : h;
        }

        /// <summary>The day's twist (null when none are defined).</summary>
        public static ReplayModifier ModifierFor(string dateKey)
        {
            var config = Config;
            if (config == null || config.modifiers == null || config.modifiers.Count == 0)
                return null;
            return config.modifiers[(int)(DailySeed(dateKey) % (uint)config.modifiers.Count)];
        }

        /// <summary>"2026-10-25" from "20261025".</summary>
        public static string PrettyDate(string dateKey) =>
            dateKey != null && dateKey.Length == 8 ? dateKey.Substring(0, 4) + "-" + dateKey.Substring(4, 2) + "-" + dateKey.Substring(6, 2) : dateKey ?? "";

        public static int DailyScoreOf(int floors, int kills, int grazes, bool cleared)
        {
            var c = Config ?? new ReplayConfig();
            return floors * c.scorePerFloor + kills * c.scorePerKill + grazes * c.scorePerGraze + (cleared ? c.scoreCleared : 0);
        }

        /// <summary>Every saved score, newest day last.</summary>
        public static List<DailyScore> AllDaily()
        {
            var list = new List<DailyScore>();
            foreach (string part in PlayerPrefs.GetString(TableKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(':');
                if (f.Length == 5 && long.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long attempt)
                    && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score)
                    && int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int floors))
                    list.Add(new DailyScore { Date = f[0], Attempt = attempt, Score = score, Floors = floors, Cleared = f[4] == "1" });
            }
            return list;
        }

        /// <summary>The day's table, best first (at most the config's table size).</summary>
        public static List<DailyScore> DailyTable(string dateKey)
        {
            var rows = AllDaily().FindAll(s => s.Date == dateKey);
            rows.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Attempt.CompareTo(b.Attempt));
            int size = Config != null ? Mathf.Max(1, Config.tableSize) : 10;
            if (rows.Count > size)
                rows.RemoveRange(size, rows.Count - size);
            return rows;
        }

        /// <summary>
        /// Writes one play of the day's floor into the table. A play is one 'attempt' id: it is saved as it climbs (a death keeps what he
        /// reached) and updated as the score grows. Returns the play's place in the day's table (1 = first; 0 = not in it).
        /// </summary>
        public static int RecordDaily(string dateKey, long attempt, int score, int floors, bool cleared)
        {
            var all = AllDaily();
            int found = all.FindIndex(s => s.Date == dateKey && s.Attempt == attempt);
            var entry = new DailyScore { Date = dateKey, Attempt = attempt, Score = score, Floors = floors, Cleared = cleared };
            if (found >= 0)
            {
                // Only ever up: a rewind that takes a climb back doesn't take the score back.
                if (all[found].Score >= score && all[found].Cleared == cleared)
                    return PlaceOf(all, entry);
                all[found] = entry;
            }
            else
            {
                all.Add(entry);
            }
            Save(all);
            return PlaceOf(all, entry);
        }

        static int PlaceOf(List<DailyScore> all, DailyScore entry)
        {
            var table = DailyTable(entry.Date);
            for (int i = 0; i < table.Count; i++)
                if (table[i].Attempt == entry.Attempt)
                    return i + 1;
            return 0;
        }

        static void Save(List<DailyScore> all)
        {
            // Keep the newest days, and each day's best scores only.
            int days = Config != null ? Mathf.Max(1, Config.daysKept) : 14;
            var dates = new List<string>();
            foreach (var s in all)
                if (!dates.Contains(s.Date))
                    dates.Add(s.Date);
            dates.Sort(string.CompareOrdinal);
            var keep = new HashSet<string>();
            for (int i = Mathf.Max(0, dates.Count - days); i < dates.Count; i++)
                keep.Add(dates[i]);
            var parts = new List<string>();
            foreach (string date in dates)
            {
                if (!keep.Contains(date))
                    continue;
                var day = all.FindAll(s => s.Date == date);
                day.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Attempt.CompareTo(b.Attempt));
                int size = Config != null ? Mathf.Max(1, Config.tableSize) : 10;
                for (int i = 0; i < day.Count && i < size; i++)
                {
                    var s = day[i];
                    parts.Add(string.Join(":", s.Date, s.Attempt.ToString(CultureInfo.InvariantCulture), s.Score.ToString(CultureInfo.InvariantCulture),
                        s.Floors.ToString(CultureInfo.InvariantCulture), s.Cleared ? "1" : "0"));
                }
            }
            PlayerPrefs.SetString(TableKey, string.Join(";", parts));
            PlayerPrefs.Save();
        }

        /// <summary>Playtest setup: forgets the best floor, the Daily's table and the cycle.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(EndlessKey);
            PlayerPrefs.DeleteKey(TableKey);
            PlayerPrefs.DeleteKey(CycleKey);
            PlayerPrefs.Save();
        }

        // ─── New Game Plus ────────────────────────────────────────────

        /// <summary>0 = the first climb; 1 = New Game+; ... up to the config's top cycle.</summary>
        public static int Cycle => Mathf.Clamp(PlayerPrefs.GetInt(CycleKey, 0), 0, Config != null ? Config.ngPlusMaxCycle : 9);

        /// <summary>Whether New Game+ is offered: the Magma King has fallen (his Waystone is lit), or a cycle is already running.</summary>
        public static bool NewGamePlusOpen
        {
            get
            {
                var config = Config;
                if (config == null || !config.enabled)
                    return false;
                if (Cycle > 0)
                    return true;
                int highest = WaystoneProgress.Highest;
                return highest > 0 && highest <= config.ngPlusAfterWaystone;
            }
        }

        /// <summary>The cycle New Game+ would start (the next one, never past the top).</summary>
        public static int NextCycle => Mathf.Min(Cycle + 1, Config != null ? Config.ngPlusMaxCycle : 9);

        /// <summary>Starts the next cycle: the cycle counts up. (The run's own reset is RunFlow.NewGamePlus.)</summary>
        public static void StartNextCycle()
        {
            PlayerPrefs.SetInt(CycleKey, NextCycle);
            PlayerPrefs.Save();
        }

        /// <summary>New Game forgets the cycles.</summary>
        public static void ResetCycle()
        {
            PlayerPrefs.DeleteKey(CycleKey);
            PlayerPrefs.Save();
        }

        /// <summary>The scale of New Game+ cycle 'cycle' (1 = the first repeat).</summary>
        public static ReplayScale ScaleForCycle(int cycle)
        {
            var scale = ReplayScale.None;
            var config = Config;
            if (config == null || cycle <= 0)
                return scale;
            scale.Health = 1f + config.ngPlusHealth * cycle;
            scale.AttackSpeed = 1f + config.ngPlusAttackSpeed * cycle;
            scale.MoveSpeed = 1f + config.ngPlusMoveSpeed * cycle;
            int every = Mathf.Max(1, config.ngPlusCrowdEvery);
            scale.ExtraAlive = cycle / every;
            scale.ExtraPush = cycle / every;
            return scale;
        }

        /// <summary>The scale of a day's twist.</summary>
        public static ReplayScale ScaleForModifier(ReplayModifier m)
        {
            var scale = ReplayScale.None;
            if (m == null)
                return scale;
            scale.Health = m.enemyHealth;
            scale.AttackSpeed = m.enemyAttackSpeed;
            scale.MoveSpeed = m.enemyMoveSpeed;
            scale.ProjectileSpeed = m.enemyProjectileSpeed;
            scale.Size = m.enemySize;
            scale.BreatherScale = m.breatherScale;
            scale.ExtraAlive = m.extraAlive;
            scale.ExtraPush = m.extraPush;
            return scale;
        }
    }
}
