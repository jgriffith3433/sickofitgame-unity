using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Which weapons (content/weapons) the hero holds beside the rocket launcher, their ammo, and which one is
    /// equipped. Saved in PlayerPrefs and cleared with the Grudges when a run ends and on New Game. The level's
    /// PlayerWeapons reads it when the level starts and writes it as he picks up, loses and switches weapons.
    /// </summary>
    public static class WeaponProgress
    {
        const string Key = "GameplayWeapons";
        const string EquippedKey = "GameplayWeaponEquipped";

        /// <summary>The held weapons other than the launcher, in the order he got them, with their ammo (0 for a weapon without).</summary>
        public static List<KeyValuePair<string, int>> Owned()
        {
            var owned = new List<KeyValuePair<string, int>>();
            string saved = PlayerPrefs.GetString(Key, "");
            foreach (string part in saved.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = part.LastIndexOf(':');
                if (colon > 0 && int.TryParse(part.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ammo))
                    owned.Add(new KeyValuePair<string, int>(part.Substring(0, colon), Math.Max(0, ammo)));
            }
            return owned;
        }

        /// <summary>The equipped weapon's id, or "" for the launcher.</summary>
        public static string Equipped() => PlayerPrefs.GetString(EquippedKey, "");

        public static bool Has(string id)
        {
            foreach (var pair in Owned())
                if (pair.Key == id)
                    return true;
            return false;
        }

        /// <summary>Stores the held weapons (not the launcher) and the equipped one. 'flush' also writes the prefs to disk.</summary>
        public static void Store(IList<KeyValuePair<string, int>> owned, string equipped, bool flush)
        {
            var parts = new List<string>(owned.Count);
            foreach (var pair in owned)
                parts.Add(pair.Key + ":" + pair.Value.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(Key, string.Join(";", parts));
            PlayerPrefs.SetString(EquippedKey, equipped ?? "");
            if (flush)
                PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(EquippedKey);
            PlayerPrefs.Save();
        }
    }
}
