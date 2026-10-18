using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// What Yooba remembers (Docs/Story.md, "Yooba between floors"): whether he has met the hero, which of his
    /// between-floor lines he has said, and the news since the last Shaft (a death, the Grudges held). New Game
    /// resets it. Presentation only: nothing in the game reads it.
    /// </summary>
    public static class YoobaProgress
    {
        const string MetKey = "Yooba_Met";
        const string SaidKey = "Yooba_Said";
        const string DiedKey = "Yooba_HeroDied";
        const string GrudgesKey = "Yooba_Grudges";

        /// <summary>He has said hello (the Shaft after Video3).</summary>
        public static bool Met
        {
            get => PlayerPrefs.GetInt(MetKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MetKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The hero died since the last Shaft (set by the level, cleared when Yooba has had his say).</summary>
        public static bool HeroDied
        {
            get => PlayerPrefs.GetInt(DiedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(DiedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>How many Grudges the hero held at the last Shaft; -1 = not counted yet.</summary>
        public static int GrudgesSeen
        {
            get => PlayerPrefs.GetInt(GrudgesKey, -1);
            set
            {
                PlayerPrefs.SetInt(GrudgesKey, value);
                PlayerPrefs.Save();
            }
        }

        static HashSet<string> Said() =>
            new HashSet<string>(PlayerPrefs.GetString(SaidKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        public static bool HasSaid(string id) => !string.IsNullOrEmpty(id) && Said().Contains(id);

        public static void MarkSaid(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            var said = Said();
            if (!said.Add(id))
                return;
            PlayerPrefs.SetString(SaidKey, string.Join(",", said));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(MetKey);
            PlayerPrefs.DeleteKey(SaidKey);
            PlayerPrefs.DeleteKey(DiedKey);
            PlayerPrefs.DeleteKey(GrudgesKey);
        }
    }
}
