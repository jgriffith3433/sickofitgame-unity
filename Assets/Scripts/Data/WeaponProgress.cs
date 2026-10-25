using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Which weapons (content/weapons) the hero holds beside the rocket launcher, their ammo, and which one is
    /// equipped, and which weapons a floor's scene has taken away for now (the launcher on Floor 998). Saved in
    /// PlayerPrefs and cleared with the Grudges when a run ends and on New Game. The level's PlayerWeapons reads it
    /// when the level starts and writes it as he picks up, loses and switches weapons. What a pickup has unlocked for
    /// good is <see cref="WeaponUnlocks"/>, which a run ending does not clear.
    /// </summary>
    public static class WeaponProgress
    {
        const string Key = "GameplayWeapons";
        const string EquippedKey = "GameplayWeaponEquipped";
        const string SuspendedKey = "GameplayWeaponsSuspended";

        /// <summary>Weapons (the launcher included) a scene has taken from him this run; they come back with giveWeapon or a pickup.</summary>
        public static List<string> Suspended()
        {
            var ids = new List<string>();
            foreach (string part in PlayerPrefs.GetString(SuspendedKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                ids.Add(part);
            return ids;
        }

        public static void StoreSuspended(IList<string> ids) => PlayerPrefs.SetString(SuspendedKey, string.Join(";", ids));

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
            PlayerPrefs.DeleteKey(SuspendedKey);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The weapons a pickup has unlocked for good (content/weapons, the ones with a pickup). Unlike
    /// <see cref="WeaponProgress"/> they are kept when he dies and the run starts again from a Waystone: every level
    /// starts with all of them in his hands. Only New Game clears them. The launcher is his from the start and is not listed.
    /// </summary>
    public static class WeaponUnlocks
    {
        const string Key = "GameplayWeaponUnlocks";

        /// <summary>The unlocked weapon ids, in the order they were found.</summary>
        public static List<string> All()
        {
            var ids = new List<string>();
            foreach (string part in PlayerPrefs.GetString(Key, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                ids.Add(part);
            return ids;
        }

        public static bool Has(string id) => All().Contains(id);

        /// <summary>Unlocks a weapon and saves at once. True if it was new.</summary>
        public static bool Add(string id)
        {
            var ids = All();
            if (ids.Contains(id))
                return false;
            ids.Add(id);
            PlayerPrefs.SetString(Key, string.Join(";", ids));
            PlayerPrefs.Save();
            return true;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
