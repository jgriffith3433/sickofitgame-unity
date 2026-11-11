using System;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// First-run helpers (Docs/Story.md "First five minutes"): New Game starts at Normal with no difficulty card;
    /// after three deaths inside five real minutes the death screen offers Assist once. Cleared on New Game.
    /// </summary>
    public static class FtueProgress
    {
        const string StartedAtKey = "Ftue_StartedAtUtc";
        const string DeathsKey = "Ftue_SessionDeaths";
        const string AssistOfferedKey = "Ftue_AssistOffered";
        public const float AssistWindowSeconds = 300f;
        public const int AssistAfterDeaths = 3;

        /// <summary>
        /// Optional difficulty for the next New Game (playtest / Settings pick). Null = Normal. Cleared when BeginNewGame runs.
        /// </summary>
        public static DifficultyLevel? PendingDifficulty;

        /// <summary>Call from New Game: starts at PendingDifficulty or Normal; the five-minute Assist window starts; Assist offer is fresh.</summary>
        public static void BeginNewGame()
        {
            var level = PendingDifficulty ?? DifficultyLevel.Normal;
            PendingDifficulty = null;
            Difficulty.Current = level;
            if (level == DifficultyLevel.Assist)
                GameSettings.AimAssist = true;
            PlayerPrefs.SetString(StartedAtKey, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.SetInt(DeathsKey, 0);
            PlayerPrefs.DeleteKey(AssistOfferedKey);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(StartedAtKey);
            PlayerPrefs.DeleteKey(DeathsKey);
            PlayerPrefs.DeleteKey(AssistOfferedKey);
        }

        /// <summary>A death that stood (the death screen opened). Returns the session death count after this one.</summary>
        public static int NoteDeath()
        {
            int n = PlayerPrefs.GetInt(DeathsKey, 0) + 1;
            PlayerPrefs.SetInt(DeathsKey, n);
            PlayerPrefs.Save();
            return n;
        }

        public static int SessionDeaths => PlayerPrefs.GetInt(DeathsKey, 0);

        public static bool AssistOffered
        {
            get => PlayerPrefs.GetInt(AssistOfferedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(AssistOfferedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True when the death screen should show Try Assist: three deaths inside five minutes, not yet offered, not already on Assist.</summary>
        public static bool ShouldOfferAssist()
        {
            if (AssistOffered || Difficulty.Current == DifficultyLevel.Assist)
                return false;
            if (SessionDeaths < AssistAfterDeaths)
                return false;
            return SecondsSinceNewGame() <= AssistWindowSeconds;
        }

        public static float SecondsSinceNewGame()
        {
            string raw = PlayerPrefs.GetString(StartedAtKey, "");
            if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, out long ticks))
                return float.PositiveInfinity;
            return (float)(DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>Takes the Assist offer: saves Assist, turns aim assist on, and never offers again this save.</summary>
        public static void TakeAssist()
        {
            Difficulty.Current = DifficultyLevel.Assist;
            GameSettings.AimAssist = true;
            AssistOffered = true;
        }
    }
}
