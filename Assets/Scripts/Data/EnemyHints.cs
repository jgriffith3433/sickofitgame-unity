using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// The enemy types the hero has already said his piece about (Docs/Story.md, "The hero sizes them up"): the
    /// <c>hintOnce</c> action adds an enemy's display name the first time that type spawns where he can hear. Kept
    /// for the save like <see cref="ShortcutProgress"/>: a death, a rewind or a new run from a Waystone does not
    /// clear it; only New Game does.
    /// </summary>
    public static class EnemyHints
    {
        const string Key = "Enemies_Seen";

        public static List<string> All()
        {
            var ids = new List<string>();
            foreach (string part in PlayerPrefs.GetString(Key, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                ids.Add(part);
            return ids;
        }

        public static bool Has(string name) => All().Contains(Clean(name));

        /// <summary>Marks a type as seen and saves at once. True if it was new.</summary>
        public static bool Add(string name)
        {
            string clean = Clean(name);
            var ids = All();
            if (clean.Length == 0 || ids.Contains(clean))
                return false;
            ids.Add(clean);
            PlayerPrefs.SetString(Key, string.Join(";", ids));
            PlayerPrefs.Save();
            return true;
        }

        public static void Reset() => PlayerPrefs.DeleteKey(Key);

        static string Clean(string name) => (name ?? "").Replace(";", "").Trim();
    }
}
