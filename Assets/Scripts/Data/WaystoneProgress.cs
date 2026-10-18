using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Waystones and runs (Docs/Story.md, "Waystones and runs"): the highest Waystone the hero has reached (the
    /// Landing at Floor 1,000, then the top of each Circle once its Warden is beaten: 901, 801, ... 101), the level
    /// the current run goes on from, and how many runs Hell has ended. Saved in PlayerPrefs; New Game resets it
    /// (RogueliteManager.ResetAllRogueliteData). Which floors are Waystones is the Shaft's config (RunFlow).
    /// </summary>
    public static class WaystoneProgress
    {
        const string HighestKey = "Waystone_Highest";
        const string RunLevelKey = "Run_Level";
        const string RunsEndedKey = "Run_Ended";
        const string DiedOnFloorKey = "Run_DiedOnFloor";

        /// <summary>The highest Waystone reached (the lowest floor number), or 0 before the Landing.</summary>
        public static int Highest
        {
            get => PlayerPrefs.GetInt(HighestKey, 0);
            private set
            {
                PlayerPrefs.SetInt(HighestKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Saves a Waystone if it is higher up (a lower floor) than the one saved. Returns true if it was new.</summary>
        public static bool Reach(int floor)
        {
            if (floor <= 0)
                return false;
            int saved = Highest;
            if (saved > 0 && saved <= floor)
                return false;
            Highest = floor;
            return true;
        }

        /// <summary>The level the current run goes on from (main menu Continue); 0 = no run saved yet.</summary>
        public static int RunLevel
        {
            get => PlayerPrefs.GetInt(RunLevelKey, 0);
            set
            {
                PlayerPrefs.SetInt(RunLevelKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>How many runs Hell has ended (deaths that sent the hero back to a Waystone). 1 = the first.</summary>
        public static int RunsEnded
        {
            get => PlayerPrefs.GetInt(RunsEndedKey, 0);
            set
            {
                PlayerPrefs.SetInt(RunsEndedKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>The floor the hero was on when he last died on a floor stage (where the Shaft's drop starts); 0 = unknown.</summary>
        public static int DiedOnFloor
        {
            get => PlayerPrefs.GetInt(DiedOnFloorKey, 0);
            set
            {
                PlayerPrefs.SetInt(DiedOnFloorKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(HighestKey);
            PlayerPrefs.DeleteKey(RunLevelKey);
            PlayerPrefs.DeleteKey(RunsEndedKey);
            PlayerPrefs.DeleteKey(DiedOnFloorKey);
        }
    }
}
