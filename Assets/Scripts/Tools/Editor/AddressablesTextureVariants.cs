using System;
using System.Collections.Generic;
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
    /// Both passes cap every texture at <see cref="MaxTextureSize"/> (Docs/BuildSize.md). Bundles without a texture (sound,
    /// scenes, scripts) come out byte for byte the same in both passes: the ASTC folder drops its copy and lists the name in
    /// <see cref="SharedListPath"/>, which the player build carries (TextureVariantShared.txt) so phones fetch those from
    /// ServerData/WebGL.
    /// </summary>
    public static class AddressablesTextureVariants
    {
        /// <summary>
        /// The largest side of any texture in either variant. Phones had it first (the hero's 3624x4080 sheets were 6 MB each
        /// as ASTC, and iOS Safari reloads the page past its memory limit); desktop too since 2026-11-08: its uncapped DXT
        /// sheets were 740 MB of the 1.27 GB build, and a 1080p screen shows no more of a 2048 sheet than a phone does.
        /// </summary>
        public const int MaxTextureSize = 2048;
        public const int PhoneMaxTextureSize = MaxTextureSize;

        /// <summary>The bundles both variants share (one file name a line), written by <see cref="Build"/>.</summary>
        public static string SharedListPath => Path.Combine(ServerDataRoot(), TextureVariantRedirect.SharedListName + ".txt");

        /// <summary>
        /// Keeps the texture cap on while the player builds (dispose after): the DXT pass imported every texture at
        /// <see cref="MaxTextureSize"/>, and a player build without the cap would import them all again at full size.
        /// </summary>
        public static IDisposable CapTextures() => new TextureCapScope();

        sealed class TextureCapScope : IDisposable
        {
            readonly int before = EditorUserBuildSettings.overrideMaxTextureSize;
            public TextureCapScope() => EditorUserBuildSettings.overrideMaxTextureSize = MaxTextureSize;
            public void Dispose() => EditorUserBuildSettings.overrideMaxTextureSize = before;
        }

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
                if (File.Exists(SharedListPath))
                    File.Delete(SharedListPath);

                // Every texture at most MaxTextureSize in both passes (the DXT pass's imports are the ones the player
                // build reuses: it runs under CapTextures, so nothing imports again at full size in between).
                log($"Building ASTC Addressables (phones, textures at most {MaxTextureSize})...");
                EditorUserBuildSettings.overrideMaxTextureSize = MaxTextureSize;
                BuildPass(settings, WebGLTextureSubtarget.ASTC);
                Directory.Move(remote, mobile);
                CopyBundles(LocalBuildPath(settings), Path.Combine(mobile, TextureVariantRedirect.LocalSubfolder));
                log("  " + mobile + ": " + FolderMegabytes(mobile) + " MB");

                log($"Building DXT Addressables (desktop, textures at most {MaxTextureSize})...");
                BuildPass(settings, WebGLTextureSubtarget.DXT);
                log("  " + remote + ": " + FolderMegabytes(remote) + " MB");

                DropSharedCopies(remote, LocalBuildPath(settings), mobile, log);
            }
            finally
            {
                EditorUserBuildSettings.overrideMaxTextureSize = 0;
                if (EditorUserBuildSettings.webGLBuildSubtarget != original)
                    EditorUserBuildSettings.webGLBuildSubtarget = original;
            }
        }

        /// <summary>
        /// Deletes every bundle of the ASTC build (remote, and its copy of the local ones) that is byte for byte the same file
        /// as the DXT build's (remote with remote, local with local) and writes their names to <see cref="SharedListPath"/>.
        /// </summary>
        static void DropSharedCopies(string desktopRemote, string desktopLocal, string mobile, Action<string> log)
        {
            var shared = new List<string>();
            long bytes = 0;
            void Compare(string mobileFolder, string desktopFolder)
            {
                if (!Directory.Exists(mobileFolder) || !Directory.Exists(desktopFolder))
                    return;
                foreach (string file in Directory.GetFiles(mobileFolder, "*.bundle"))
                {
                    string name = Path.GetFileName(file);
                    string twin = Path.Combine(desktopFolder, name);
                    if (!File.Exists(twin) || !SameBytes(file, twin))
                        continue;
                    bytes += new FileInfo(file).Length;
                    File.Delete(file);
                    shared.Add(name);
                }
            }
            Compare(mobile, desktopRemote);
            Compare(Path.Combine(mobile, TextureVariantRedirect.LocalSubfolder), desktopLocal);
            shared.Sort(StringComparer.Ordinal);
            File.WriteAllText(SharedListPath, string.Join("\n", shared));
            log($"  Shared: {shared.Count} bundles ({bytes / (1024 * 1024)} MB: sound, scenes, scripts) are the same in both builds; " +
                $"phones fetch them from {TextureVariantRedirect.DesktopFolder}, so {TextureVariantRedirect.MobileFolder} holds no copy. " +
                $"{mobile}: {FolderMegabytes(mobile)} MB left.");
        }

        static bool SameBytes(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length)
                return false;
            using (var sa = fa.OpenRead())
            using (var sb = fb.OpenRead())
            {
                var ba = new byte[1 << 16];
                var bb = new byte[1 << 16];
                while (true)
                {
                    int na = sa.Read(ba, 0, ba.Length);
                    if (na == 0)
                        return true;
                    int got = 0;
                    while (got < na)
                    {
                        int nb = sb.Read(bb, got, na - got);
                        if (nb == 0)
                            return false;
                        got += nb;
                    }
                    for (int i = 0; i < na; i++)
                    {
                        if (ba[i] != bb[i])
                            return false;
                    }
                }
            }
        }

        static string ServerDataRoot()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return Path.GetDirectoryName(Path.GetFullPath(settings.RemoteCatalogBuildPath.GetValue(settings)));
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
