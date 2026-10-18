using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// What the Shaft (Hell's map) remembers between screens: the floor the hero's marker was last shown on, and
    /// whether the first plunge from the Meadow has been seen (after that the Shaft can be skipped). The deepest floor
    /// reached is derived from the completed levels' floors. New Game resets both.
    /// </summary>
    public static class ShaftProgress
    {
        const string FloorKey = "Shaft_Floor";
        const string PlungedKey = "Shaft_Plunged";

        /// <summary>The floor the marker was last shown on; 0 = still in the Meadow (no Shaft yet).</summary>
        public static int Floor
        {
            get => PlayerPrefs.GetInt(FloorKey, 0);
            set
            {
                PlayerPrefs.SetInt(FloorKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>The first plunge has been seen: the Shaft can be skipped from now on.</summary>
        public static bool Plunged
        {
            get => PlayerPrefs.GetInt(PlungedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PlungedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(FloorKey);
            PlayerPrefs.DeleteKey(PlungedKey);
        }
    }
}
