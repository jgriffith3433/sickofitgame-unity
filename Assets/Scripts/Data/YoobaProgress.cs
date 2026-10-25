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
        const string DeathsKey = "Yooba_Deaths";
        const string StockedKey = "Yooba_Stocked";

        /// <summary>How many times the hero has died in all (Yooba keeps count; the shop reacts to it).</summary>
        public static int Deaths
        {
            get => PlayerPrefs.GetInt(DeathsKey, 0);
            set
            {
                PlayerPrefs.SetInt(DeathsKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>The wares he has already pointed out on the shelf.</summary>
        public static bool HasShownWare(string id) => !string.IsNullOrEmpty(id) && Set(StockedKey).Contains(id);

        public static void MarkWareShown(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            var shown = Set(StockedKey);
            if (!shown.Add(id))
                return;
            PlayerPrefs.SetString(StockedKey, string.Join(",", shown));
            PlayerPrefs.Save();
        }

        static HashSet<string> Set(string key) =>
            new HashSet<string>(PlayerPrefs.GetString(key, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

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
            PlayerPrefs.DeleteKey(DeathsKey);
            PlayerPrefs.DeleteKey(StockedKey);
        }
    }
}
