using UnityEngine;
using System.Text.RegularExpressions;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// GitHub Pages sends Cache-Control: max-age=600 and does not allow custom
    /// headers. Bundle URLs already contain a content hash. The catalog file name
    /// does not, so append the publish folder (v101, v102, ...) to that URL only.
    /// </summary>
    public static class CatalogCacheBuster
    {
        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (installed)
                return;
            installed = true;

            var previous = AddressablesAPI.ResourceManager.InternalIdTransformFunc;
            AddressablesAPI.ResourceManager.InternalIdTransformFunc = location =>
            {
                string raw = previous != null ? previous(location) : location.InternalId;
                string id = AddressablesAPI.ResolveInternalId(raw);
                BundleFetchLog.Note(id);
                if (string.IsNullOrEmpty(id) || id.IndexOf('?') >= 0)
                    return id;
                if (id.IndexOf("catalog_", System.StringComparison.Ordinal) < 0)
                    return id;

                string version = PublishVersion();
                if (string.IsNullOrEmpty(version))
                    return id;
                return id + "?v=" + version;
            };
        }

        /// <summary>
        /// The version folder in the WebGL streaming assets URL, for example v101.
        /// </summary>
        private static string PublishVersion()
        {
            string path = Application.streamingAssetsPath;
            if (string.IsNullOrEmpty(path))
                return null;

            Match match = Regex.Match(path, @"/v(\d+)/");
            if (!match.Success)
                return null;
            return "v" + match.Groups[1].Value;
        }
    }
}

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// Counts the bundles the page asks for until the main menu is up, by group (the file name before "_assets_"/"_scenes_"),
    /// so the phone-load playtest can say what the bundles before the menu are. Counting only: nothing reads it in play.
    /// A bundle is counted once however often Addressables passes its location through the id transform: the transform runs for every
    /// asset location that depends on a bundle (the menu hero's Idle sheet is one 4.5 MB bundle with ~90 sprite locations, which
    /// the first count of 76 "playerdata requests" really was), so "calls" is shown beside it.
    /// </summary>
    public static class BundleFetchLog
    {
        static readonly System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.HashSet<string>> perGroup =
            new System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.HashSet<string>>(System.StringComparer.Ordinal);
        static readonly System.Collections.Generic.Dictionary<string, int> calls = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
        static bool frozen;

        /// <summary>Called by the menu once it is up: later fetches are not "before the menu".</summary>
        public static void Freeze() => frozen = true;

        public static void Note(string id)
        {
            if (frozen || string.IsNullOrEmpty(id))
                return;
            int bundle = id.IndexOf(".bundle", System.StringComparison.OrdinalIgnoreCase);
            if (bundle < 0)
                return;
            int slash = id.LastIndexOf('/', bundle);
            string file = id.Substring(slash + 1, bundle - slash - 1);
            string name = file;
            // "playerdata_assets_core_art1_assets_all_<hash>": the group is what comes before the LAST "_assets_"/"_scenes_" marker, so
            // the core and per-asset groups of PlayerData_Assets_* stay apart (cutting at the first one lumped them all as "playerdata").
            int cut = name.LastIndexOf("_assets_", System.StringComparison.Ordinal);
            if (cut < 0)
                cut = name.LastIndexOf("_scenes_", System.StringComparison.Ordinal);
            if (cut > 0)
                name = name.Substring(0, cut);
            if (!perGroup.TryGetValue(name, out var files))
                perGroup[name] = files = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            files.Add(file);
            calls.TryGetValue(name, out int n);
            calls[name] = n + 1;
        }

        /// <summary>"group x2 (5 calls), group x1 ..." and the number of different bundles, for the playtest.</summary>
        public static string Describe(out int total)
        {
            total = 0;
            var text = new System.Text.StringBuilder();
            foreach (var pair in perGroup)
            {
                total += pair.Value.Count;
                if (text.Length > 0)
                    text.Append(", ");
                text.Append(pair.Key).Append(" x").Append(pair.Value.Count);
                if (calls.TryGetValue(pair.Key, out int n) && n != pair.Value.Count)
                    text.Append(" (").Append(n).Append(" calls)");
            }
            return text.ToString();
        }
    }
}
