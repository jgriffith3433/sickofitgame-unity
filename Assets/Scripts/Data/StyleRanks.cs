using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// The best rank earned on each floor stage (Docs/Story.md, "Style"), by level number. Kept for the save like
    /// <see cref="EnemyHints"/>: deaths and new runs keep it; New Game clears it.
    /// </summary>
    public static class StyleRanks
    {
        const string Key = "Style_Ranks";

        public struct Best
        {
            public int Level;
            public string Letter;
            public int Points;
        }

        public static List<Best> All()
        {
            var list = new List<Best>();
            foreach (string part in PlayerPrefs.GetString(Key, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(':');
                if (f.Length == 3 && int.TryParse(f[0], out int level) && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                    list.Add(new Best { Level = level, Letter = f[1], Points = points });
            }
            return list;
        }

        public static bool TryGet(int level, out Best best)
        {
            foreach (var b in All())
            {
                if (b.Level == level)
                {
                    best = b;
                    return true;
                }
            }
            best = default;
            return false;
        }

        /// <summary>Saves the rank if it beats the floor's best. True when it is a new best (the first rank counts).</summary>
        public static bool Record(int level, string letter, int points)
        {
            var all = All();
            int at = all.FindIndex(b => b.Level == level);
            if (at >= 0 && all[at].Points >= points)
                return false;
            var entry = new Best { Level = level, Letter = letter, Points = points };
            if (at >= 0)
                all[at] = entry;
            else
                all.Add(entry);
            var parts = new List<string>();
            foreach (var b in all)
                parts.Add($"{b.Level}:{b.Letter}:{b.Points.ToString(CultureInfo.InvariantCulture)}");
            PlayerPrefs.SetString(Key, string.Join(";", parts));
            PlayerPrefs.Save();
            return true;
        }

        public static void Reset() => PlayerPrefs.DeleteKey(Key);
    }
}
