using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// The floor-arrival story beats the hero has already lived (Docs/Story.md, "Arrival beats"): the <c>storyBeat</c>
    /// action adds its id the first time it runs. Kept for the save like <see cref="EnemyHints"/>: a death, a retry,
    /// a rewind or a new run from a Waystone does not play the beat again; only New Game clears it.
    /// </summary>
    public static class StoryBeats
    {
        const string Key = "Story_Beats";

        public static List<string> All()
        {
            var ids = new List<string>();
            foreach (string part in PlayerPrefs.GetString(Key, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                ids.Add(part);
            return ids;
        }

        public static bool Has(string id) => All().Contains(Clean(id));

        /// <summary>Marks a beat as lived and saves at once. True if it was new.</summary>
        public static bool Add(string id)
        {
            string clean = Clean(id);
            var ids = All();
            if (clean.Length == 0 || ids.Contains(clean))
                return false;
            ids.Add(clean);
            PlayerPrefs.SetString(Key, string.Join(";", ids));
            PlayerPrefs.Save();
            return true;
        }

        public static void Reset() => PlayerPrefs.DeleteKey(Key);

        static string Clean(string id) => (id ?? "").Replace(";", "").Trim();
    }
}
