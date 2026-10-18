using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Hell's shortcuts the hero has taken (Docs/Story.md, "Shortcuts"): which one, and the floors it jumped (from the
    /// floor he took it on up to the floor it let him out on). The Shaft draws each one as a dotted jump up the wall.
    /// Kept for the save, like the Shaft's marker; New Game forgets them.
    /// </summary>
    public static class ShortcutProgress
    {
        const string Key = "Shortcuts_Taken";

        public struct Taken
        {
            public string Id;
            public int FromFloor;
            public int ToFloor;
        }

        public static List<Taken> All()
        {
            var list = new List<Taken>();
            foreach (string part in PlayerPrefs.GetString(Key, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = part.Split(':');
                if (fields.Length == 3 && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)
                    && int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int to))
                    list.Add(new Taken { Id = fields[0], FromFloor = from, ToFloor = to });
            }
            return list;
        }

        public static bool WasTaken(string id) => All().Exists(t => t.Id == id);

        /// <summary>Remembers a shortcut taken from 'fromFloor' to 'toFloor' (once per id; taking it again moves it).</summary>
        public static void Record(string id, int fromFloor, int toFloor)
        {
            if (string.IsNullOrEmpty(id))
                return;
            var list = All();
            list.RemoveAll(t => t.Id == id);
            list.Add(new Taken { Id = id.Replace(":", "").Replace(";", ""), FromFloor = fromFloor, ToFloor = toFloor });
            var parts = new List<string>();
            foreach (var t in list)
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}", t.Id, t.FromFloor, t.ToFloor));
            PlayerPrefs.SetString(Key, string.Join(";", parts));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
        }
    }
}
