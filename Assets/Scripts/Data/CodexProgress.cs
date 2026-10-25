using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// The Codex's memory (Docs/Story.md, "The Codex"): how many times the hero has met and killed each kind of creature,
    /// by display name. "Met" is shared with the first-sight hints: a type is known if <see cref="EnemyHints"/> has said its
    /// name or the tally says he has met it, so a type met in a scene (no subtitle) is still in the Codex. Kept in the save
    /// like <see cref="EnemyHints"/> (PlayerPrefs, not rewound; the world counts each spawn and kill once, even when a rewind
    /// plays it again); only New Game clears it.
    /// </summary>
    public static class CodexProgress
    {
        const string Key = "Codex_Tally";

        public struct Tally
        {
            public int Met;
            public int Killed;
        }

        static Dictionary<string, Tally> cache;

        static Dictionary<string, Tally> Load()
        {
            if (cache != null)
                return cache;
            cache = new Dictionary<string, Tally>(StringComparer.Ordinal);
            foreach (string part in PlayerPrefs.GetString(Key, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // name=met,killed
                int eq = part.LastIndexOf('=');
                if (eq <= 0)
                    continue;
                string[] nums = part.Substring(eq + 1).Split(',');
                if (nums.Length != 2
                    || !int.TryParse(nums[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int met)
                    || !int.TryParse(nums[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int killed))
                    continue;
                cache[part.Substring(0, eq)] = new Tally { Met = met, Killed = killed };
            }
            return cache;
        }

        static void Save()
        {
            var parts = new List<string>();
            foreach (var pair in Load())
                parts.Add(pair.Key + "=" + pair.Value.Met.ToString(CultureInfo.InvariantCulture) + "," + pair.Value.Killed.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(Key, string.Join(";", parts));
            // A busy floor spawns many creatures: write to the browser's storage at most every few seconds (Flush on the Codex).
            if (Time.realtimeSinceStartup - lastSave >= 3f)
                Flush();
        }

        static float lastSave = -10f;

        /// <summary>Writes any tally not yet saved.</summary>
        public static void Flush()
        {
            lastSave = Time.realtimeSinceStartup;
            PlayerPrefs.Save();
        }

        static string Clean(string name) => (name ?? "").Replace(";", "").Replace("=", "").Trim();

        public static Tally Of(string name)
        {
            Load().TryGetValue(Clean(name), out var tally);
            return tally;
        }

        /// <summary>The hero knows this type: he has said its name (the hint set) or met one.</summary>
        public static bool Seen(string name)
        {
            string clean = Clean(name);
            return clean.Length > 0 && (EnemyHints.Has(clean) || Of(clean).Met > 0);
        }

        /// <summary>One more of this type has shown up.</summary>
        public static void Meet(string name)
        {
            string clean = Clean(name);
            if (clean.Length == 0)
                return;
            var tally = Of(clean);
            tally.Met++;
            Load()[clean] = tally;
            Save();
        }

        /// <summary>One of this type was killed (it was also met, so it counts as met if the spawn was missed).</summary>
        public static void Kill(string name)
        {
            string clean = Clean(name);
            if (clean.Length == 0)
                return;
            var tally = Of(clean);
            tally.Killed++;
            if (tally.Met < tally.Killed)
                tally.Met = tally.Killed;
            Load()[clean] = tally;
            Save();
        }

        public static void Reset()
        {
            cache = null;
            PlayerPrefs.DeleteKey(Key);
        }

        /// <summary>Drops the in-memory copy (tests that write PlayerPrefs directly).</summary>
        public static void Reload() => cache = null;
    }
}
