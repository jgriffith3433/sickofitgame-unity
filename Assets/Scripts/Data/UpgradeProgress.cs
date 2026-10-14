using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Which gameplay upgrades the player owns, and at what level. Saved in PlayerPrefs and cleared on New Game
    /// (RogueliteManager.ResetAllRogueliteData). Upgrades are defined in content/upgrades.
    /// </summary>
    public static class UpgradeProgress
    {
        const string Key = "GameplayUpgrades";

        public static Dictionary<string, int> Owned()
        {
            var owned = new Dictionary<string, int>(StringComparer.Ordinal);
            string saved = PlayerPrefs.GetString(Key, "");
            foreach (string part in saved.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = part.LastIndexOf(':');
                if (colon > 0 && int.TryParse(part.Substring(colon + 1), out int level) && level > 0)
                    owned[part.Substring(0, colon)] = level;
            }
            return owned;
        }

        public static int LevelOf(string id) => Owned().TryGetValue(id, out int level) ? level : 0;

        public static void Add(string id)
        {
            var owned = Owned();
            owned[id] = (owned.TryGetValue(id, out int level) ? level : 0) + 1;
            Save(owned);
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        static void Save(Dictionary<string, int> owned)
        {
            PlayerPrefs.SetString(Key, string.Join(";", owned.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}:{p.Value}")));
            PlayerPrefs.Save();
        }
    }
}
