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
    /// Counts the bundle URLs the page asks for until the main menu is up, by group (the file name before "_assets_"/"_scenes_"),
    /// so the phone-load playtest can say what the ~50 bundles before the menu are. Counting only: nothing reads it in play.
    /// </summary>
    public static class BundleFetchLog
    {
        static readonly System.Collections.Generic.SortedDictionary<string, int> perGroup = new System.Collections.Generic.SortedDictionary<string, int>(System.StringComparer.Ordinal);
        static bool frozen;
        static int resolving;

        /// <summary>Resolving a bundle's URL (a catalog lookup, nothing fetched) must not count as a fetch.</summary>
        public static void BeginResolve() => resolving++;
        public static void EndResolve() { if (resolving > 0) resolving--; }

        /// <summary>Called by the menu once it is up: later fetches are not "before the menu".</summary>
        public static void Freeze() => frozen = true;

        public static void Note(string id)
        {
            if (frozen || resolving > 0 || string.IsNullOrEmpty(id))
                return;
            int bundle = id.IndexOf(".bundle", System.StringComparison.OrdinalIgnoreCase);
            if (bundle < 0)
                return;
            int slash = id.LastIndexOf('/', bundle);
            string name = id.Substring(slash + 1, bundle - slash - 1);
            int cut = name.IndexOf("_assets_", System.StringComparison.Ordinal);
            if (cut < 0)
                cut = name.IndexOf("_scenes_", System.StringComparison.Ordinal);
            if (cut > 0)
                name = name.Substring(0, cut);
            perGroup.TryGetValue(name, out int n);
            perGroup[name] = n + 1;
        }

        /// <summary>"group x2, group x1 ..." and the total, for the playtest.</summary>
        public static string Describe(out int total)
        {
            total = 0;
            var text = new System.Text.StringBuilder();
            foreach (var pair in perGroup)
            {
                total += pair.Value;
                if (text.Length > 0)
                    text.Append(", ");
                text.Append(pair.Key).Append(" x").Append(pair.Value);
            }
            return text.ToString();
        }
    }
}
