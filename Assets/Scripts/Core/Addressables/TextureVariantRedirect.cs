using UnityEngine;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// The Addressables content is built twice: DXT for desktop browsers in ServerData/WebGL and ASTC for phones in
    /// ServerData/WebGL_ASTC (see AddressablesTextureVariants). Phones can't sample DXT, so WebGL would decompress every
    /// 2048 sprite sheet to RGBA32 (16 MB instead of 1-2 MB). On a browser without S3TC but with ASTC, every catalog
    /// and bundle URL is pointed at the ASTC folder. Local bundles of the ASTC build live in its "local" subfolder,
    /// because the player's StreamingAssets only holds the DXT ones.
    /// Add ?tex=astc or ?tex=dxt to the page URL to force a variant.
    /// </summary>
    public static class TextureVariantRedirect
    {
        public const string DesktopFolder = "WebGL";
        public const string MobileFolder = "WebGL_ASTC";
        public const string LocalSubfolder = "local";

        private static bool installed;

        public static bool UsingMobileVariant { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (installed)
                return;
            installed = true;

            UsingMobileVariant = ChooseMobileVariant();
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                Debug.Log("[TextureVariant] " + (UsingMobileVariant ? "ASTC" : "DXT") + " content" +
                          " (DXT1 " + SystemInfo.SupportsTextureFormat(TextureFormat.DXT1) +
                          ", ASTC " + SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_8x8) + ")");
            if (!UsingMobileVariant)
                return;

            string remoteFrom = "/ServerData/" + DesktopFolder + "/";
            string remoteTo = "/ServerData/" + MobileFolder + "/";
            string localFrom = AddressablesAPI.RuntimePath + "/" + DesktopFolder + "/";
            string localTo = Application.streamingAssetsPath + "/../../ServerData/" + MobileFolder + "/" + LocalSubfolder + "/";

            var previous = AddressablesAPI.ResourceManager.InternalIdTransformFunc;
            AddressablesAPI.ResourceManager.InternalIdTransformFunc = location =>
            {
                string raw = previous != null ? previous(location) : location.InternalId;
                string id = AddressablesAPI.ResolveInternalId(raw);
                if (string.IsNullOrEmpty(id))
                    return id;

                int remote = id.IndexOf(remoteFrom, System.StringComparison.Ordinal);
                if (remote >= 0)
                    return id.Substring(0, remote) + remoteTo + id.Substring(remote + remoteFrom.Length);

                if (id.StartsWith(localFrom, System.StringComparison.Ordinal) &&
                    id.EndsWith(".bundle", System.StringComparison.Ordinal))
                    return localTo + id.Substring(localFrom.Length);

                return id;
            };
        }

        private static bool ChooseMobileVariant()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string url = Application.absoluteURL ?? "";
            if (url.Contains("tex=astc"))
                return true;
            if (url.Contains("tex=dxt"))
                return false;
            return !SystemInfo.SupportsTextureFormat(TextureFormat.DXT1) &&
                   SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_8x8);
#else
            return false;
#endif
        }
    }
}
