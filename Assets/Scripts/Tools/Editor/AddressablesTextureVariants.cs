using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using SickOfIt.Core.Addressables;
using Debug = UnityEngine.Debug;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Builds the Addressables content once per texture format. Desktop browsers sample DXT; phones only ASTC/ETC2,
    /// and WebGL decompresses an unsupported sheet to RGBA32 (8x the memory). The ASTC pass runs first and is moved to
    /// ServerData/WebGL_ASTC (with its local bundles in "local"); the DXT pass runs last so ServerData/WebGL and the
    /// local content the player build bundles are the desktop ones. TextureVariantRedirect picks the folder at runtime.
    /// Switching the subtarget reimports every texture once; later switches come from the import cache.
    /// </summary>
    public static class AddressablesTextureVariants
    {
        /// <summary>The largest side of any texture in the phones' (ASTC) content; desktop keeps each texture's own size.</summary>
        public const int PhoneMaxTextureSize = 2048;

        /// <summary>Batch mode: -executeMethod SickOfIt.Tools.Editor.AddressablesTextureVariants.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build(Debug.Log);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build(Action<string> log)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new Exception("Addressables settings are missing.");

            string remote = Path.GetFullPath(settings.RemoteCatalogBuildPath.GetValue(settings));
            string mobile = Path.Combine(Path.GetDirectoryName(remote), TextureVariantRedirect.MobileFolder);
            var original = EditorUserBuildSettings.webGLBuildSubtarget;
            try
            {
                DeleteFolder(remote);
                DeleteFolder(mobile);

                // Phones also get every texture at most PhoneMaxTextureSize: the hero's 3624x4080 sheets were 6 MB each
                // as ASTC (and the same again as bundle data in the WebGL heap), and iOS Safari reloads the page past its limit.
                log($"Building ASTC Addressables (phones, textures at most {PhoneMaxTextureSize})...");
                EditorUserBuildSettings.overrideMaxTextureSize = PhoneMaxTextureSize;
                try
                {
                    BuildPass(settings, WebGLTextureSubtarget.ASTC);
                }
                finally
                {
                    EditorUserBuildSettings.overrideMaxTextureSize = 0;
                    AssetDatabase.Refresh();
                }
                Directory.Move(remote, mobile);
                CopyBundles(LocalBuildPath(settings), Path.Combine(mobile, TextureVariantRedirect.LocalSubfolder));
                log("  " + mobile + ": " + FolderMegabytes(mobile) + " MB");

                log("Building DXT Addressables (desktop)...");
                BuildPass(settings, WebGLTextureSubtarget.DXT);
                log("  " + remote + ": " + FolderMegabytes(remote) + " MB");
            }
            finally
            {
                if (EditorUserBuildSettings.webGLBuildSubtarget != original)
                    EditorUserBuildSettings.webGLBuildSubtarget = original;
            }
        }

        /// <summary>Copies every variant folder (WebGL, WebGL_ASTC) next to each other under destServerData.</summary>
        public static void CopyToServerData(string destServerData, Action<string> log)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            string remote = Path.GetFullPath(settings.RemoteCatalogBuildPath.GetValue(settings));
            string root = Path.GetDirectoryName(remote);
            foreach (string folder in new[] { TextureVariantRedirect.DesktopFolder, TextureVariantRedirect.MobileFolder })
            {
                string source = Path.Combine(root, folder);
                if (!Directory.Exists(source))
                    throw new Exception("Addressables output not found: " + source + ". Build Addressables first.");

                string dest = Path.Combine(destServerData, folder);
                DeleteFolder(dest);
                CopyDirectory(source, dest);
                log("  Copied to: " + dest);
            }
        }

        static void BuildPass(AddressableAssetSettings settings, WebGLTextureSubtarget subtarget)
        {
            if (EditorUserBuildSettings.webGLBuildSubtarget != subtarget)
                EditorUserBuildSettings.webGLBuildSubtarget = subtarget;
            AssetDatabase.Refresh(); // (the subtarget or the size override changed: textures import again, or come from the import cache)

            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
                throw new Exception("Addressables " + subtarget + " build failed: " + result.Error);
        }

        static string LocalBuildPath(AddressableAssetSettings settings)
        {
            var profiles = settings.profileSettings;
            string raw = profiles.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kLocalBuildPath);
            return Path.GetFullPath(profiles.EvaluateString(settings.activeProfileId, raw));
        }

        static void CopyBundles(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            if (!Directory.Exists(source))
                return;
            foreach (string file in Directory.GetFiles(source, "*.bundle"))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        }

        static void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        static long FolderMegabytes(string path)
        {
            long bytes = 0;
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                bytes += new FileInfo(file).Length;
            return bytes / (1024 * 1024);
        }
    }
}
