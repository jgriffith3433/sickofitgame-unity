using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Data
{
    /// <summary>
    /// The achievements' memory (Docs/Story.md, "Achievements"): saved counters (<c>stat=n</c>), the order achievements were unlocked in,
    /// the unique keys behind "how many different ..." counters, and the title the hero wears. The list itself is ui/codex.json
    /// (<see cref="CodexConfig.achievements"/>). Kept in PlayerPrefs like <see cref="CodexProgress"/>: deaths, runs and rewinds
    /// keep it, only New Game clears it. <c>AchievementTracker</c> feeds the counters from play; anything that changes one goes
    /// through <see cref="Bump"/>, <see cref="SetMax"/> or <see cref="AddUnique"/>, which unlock what the counter now reaches.
    /// </summary>
    public static class Achievements
    {
        const string StatsKey = "Achv_Stats";
        const string UnlockedKey = "Achv_Unlocked";
        const string UniqueKey = "Achv_Unique";
        const string TitleKey = "Achv_Title";

        /// <summary>An achievement was just unlocked (the toast listens). Raised once per achievement per save.</summary>
        public static event Action<AchievementDef> Unlocked;
        /// <summary>The title he wears changed.</summary>
        public static event Action TitleChanged;

        static Dictionary<string, int> stats;
        static List<string> unlocked;
        static HashSet<string> unique;
        static float lastSave = -10f;

        public static IReadOnlyList<AchievementDef> Defs
        {
            get
            {
                var gd = GameDefinition.Instance;
                return gd != null && gd.codex != null && gd.codex.achievements != null ? gd.codex.achievements : (IReadOnlyList<AchievementDef>)Array.Empty<AchievementDef>();
            }
        }

        static void Load()
        {
            if (stats != null)
                return;
            stats = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string part in PlayerPrefs.GetString(StatsKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.LastIndexOf('=');
                if (eq > 0 && int.TryParse(part.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    stats[part.Substring(0, eq)] = n;
            }
            unlocked = new List<string>(PlayerPrefs.GetString(UnlockedKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            unique = new HashSet<string>(PlayerPrefs.GetString(UniqueKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        }

        static string Clean(string s) => (s ?? "").Replace(";", "").Replace("=", "").Trim();

        static void Write(bool force)
        {
            PlayerPrefs.SetString(StatsKey, string.Join(";", stats.Select(p => p.Key + "=" + p.Value.ToString(CultureInfo.InvariantCulture))));
            PlayerPrefs.SetString(UnlockedKey, string.Join(";", unlocked));
            PlayerPrefs.SetString(UniqueKey, string.Join(";", unique));
            // A busy floor changes counters all the time: write to the browser's storage at most every few seconds (and on an unlock).
            if (force || Time.realtimeSinceStartup - lastSave >= 3f)
                Flush();
        }

        /// <summary>Writes any counter not yet saved.</summary>
        public static void Flush()
        {
            lastSave = Time.realtimeSinceStartup;
            PlayerPrefs.Save();
        }

        public static int Get(string stat)
        {
            Load();
            stats.TryGetValue(Clean(stat), out int n);
            return n;
        }

        public static bool IsUnlocked(string id)
        {
            Load();
            return unlocked.Contains(id);
        }

        public static int UnlockedCount
        {
            get
            {
                Load();
                int n = 0;
                foreach (var d in Defs)
                    if (d != null && unlocked.Contains(d.id))
                        n++;
                return n;
            }
        }

        /// <summary>Unlocked ids, oldest first.</summary>
        public static IReadOnlyList<string> UnlockedIds
        {
            get
            {
                Load();
                return unlocked;
            }
        }

        public static AchievementDef Find(string id)
        {
            foreach (var d in Defs)
                if (d != null && d.id == id)
                    return d;
            return null;
        }

        /// <summary>How far an achievement is: the counter, capped at its target.</summary>
        public static int Progress(AchievementDef def) => def == null ? 0 : Mathf.Min(def.target, Get(def.stat));

        /// <summary>Adds to a counter.</summary>
        public static void Bump(string stat, int amount = 1)
        {
            if (amount == 0 || string.IsNullOrEmpty(stat))
                return;
            Load();
            string key = Clean(stat);
            stats.TryGetValue(key, out int n);
            stats[key] = n + amount;
            Check(key);
        }

        /// <summary>Raises a counter to at least this value (a best, like the floors climbed).</summary>
        public static void SetMax(string stat, int value)
        {
            if (string.IsNullOrEmpty(stat))
                return;
            Load();
            string key = Clean(stat);
            stats.TryGetValue(key, out int n);
            if (value <= n)
                return;
            stats[key] = value;
            Check(key);
        }

        /// <summary>Counts a thing once (a weapon, a secret room, a kind of enemy): the counter is the number of different keys seen.</summary>
        public static bool AddUnique(string stat, string key)
        {
            if (string.IsNullOrEmpty(stat) || string.IsNullOrEmpty(key))
                return false;
            Load();
            if (!unique.Add(Clean(stat) + "|" + Clean(key)))
                return false;
            Bump(stat, 1);
            return true;
        }

        static void Check(string stat)
        {
            bool any = false;
            foreach (var d in Defs)
            {
                if (d == null || d.stat != stat || unlocked.Contains(d.id) || Get(stat) < d.target)
                    continue;
                unlocked.Add(d.id);
                any = true;
                try { Unlocked?.Invoke(d); }
                catch (Exception e) { Debug.LogException(e); }
            }
            Write(any);
        }

        // ─── The title ─────────────────────────────────────────────────────

        /// <summary>The id of the achievement whose title he wears ("" = none).</summary>
        public static string WornId => PlayerPrefs.GetString(TitleKey, "");

        /// <summary>The title he wears, or "".</summary>
        public static string WornTitle
        {
            get
            {
                var d = Find(WornId);
                return d != null && IsUnlocked(d.id) ? d.title : "";
            }
        }

        /// <summary>Wears (or, worn already, takes off) the title of an unlocked achievement. False if it is locked.</summary>
        public static bool Wear(string id)
        {
            if (WornId == id)
            {
                PlayerPrefs.DeleteKey(TitleKey);
            }
            else
            {
                var d = Find(id);
                if (d == null || !IsUnlocked(id) || string.IsNullOrEmpty(d.title))
                    return false;
                PlayerPrefs.SetString(TitleKey, id);
            }
            Flush();
            TitleChanged?.Invoke();
            return true;
        }

        // ─── Resets ────────────────────────────────────────────────────────

        /// <summary>New Game: every counter, achievement and title goes.</summary>
        public static void Reset()
        {
            stats = null;
            unlocked = null;
            unique = null;
            PlayerPrefs.DeleteKey(StatsKey);
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(UniqueKey);
            PlayerPrefs.DeleteKey(TitleKey);
            TitleChanged?.Invoke();
        }

        /// <summary>Drops the in-memory copy (tests and the playtest that write PlayerPrefs directly).</summary>
        public static void Reload()
        {
            stats = null;
            unlocked = null;
            unique = null;
        }
    }
}
