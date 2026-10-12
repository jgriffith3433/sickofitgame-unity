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
