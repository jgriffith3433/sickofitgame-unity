using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Named things the hero has unlocked (a weapon, a story beat): a set of ids saved in PlayerPrefs, kept through
    /// runs and cleared on New Game. Shop goods and Grudges gate on them with <c>requires</c> (see <see cref="Requirements"/>).
    /// A save from before this existed has no key: no flags, and nothing it already owns is touched.
    /// </summary>
    public static class UnlockFlags
    {
        const string Key = "UnlockFlags";

        public static HashSet<string> All()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in PlayerPrefs.GetString(Key, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                set.Add(id);
            return set;
        }

        public static bool Has(string id) => All().Contains(id);

        /// <summary>Unlocks something (a weapon: <c>Set("weapon:fists")</c>). Safe to call twice.</summary>
        public static void Set(string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOf(';') >= 0)
                return;
            var set = All();
            if (!set.Add(id))
                return;
            PlayerPrefs.SetString(Key, string.Join(";", set.OrderBy(s => s, StringComparer.Ordinal)));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
