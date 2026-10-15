using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Debug = UnityEngine.Debug;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Headless build methods for CLI / batch mode invocation.
    /// Called via: Unity.exe -batchmode -executeMethod SickOfIt.Tools.Editor.ClawBuildScript.BuildWebGL
    ///
    /// Environment variables:
    ///   BUILD_VERSION  — version number (e.g. "97"). Auto-detects if not set.
    ///   WEBSITE_PATH   — path to sickofitgame-website. Defaults to C:\Projects\sickofitgame-website.
    ///
    /// Exit codes: 0 = success, 1 = failure
    /// </summary>
    public static class ClawBuildScript
    {
        private const string DefaultWebsitePath = @"C:\Projects\sickofitgame-website";
        private const string DefaultUnityPath = @"C:\Projects\sickofitgame-unity";

        /// <summary>
        /// Full pipeline: addressables → version folder → WebGL build → version replace → done.
        /// Mirrors GameDeployWindow's "Full Deploy" but headless.
        /// </summary>
        public static void FullDeploy()
        {
            try
            {
                string websitePath = GetEnv("WEBSITE_PATH", DefaultWebsitePath);
                string gameFolder = Path.Combine(websitePath, "Game");
                string templateFolder = Path.Combine(gameFolder, "v0000000");
                int currentVersion = DetectVersion(gameFolder);
                int newVersion = currentVersion + 1;
                string buildVersionOverride = GetEnv("BUILD_VERSION", null);
                if (buildVersionOverride != null)
                    newVersion = int.Parse(buildVersionOverride);

                ////Debug.Log($"[ClawBuild] Starting full deploy: v{currentVersion} → v{newVersion}");

                // Steps 1-2: Build Addressables, once per texture format (ServerData/WebGL and WebGL_ASTC)
                AddressablesTextureVariants.Build(message => Debug.Log("[ClawBuild] " + message));

                // Step 3: Create version folder from template
                string newVersionFolder = Path.Combine(gameFolder, $"v{newVersion}");
                if (Directory.Exists(newVersionFolder))
                    Directory.Delete(newVersionFolder, true);
                CopyDirectory(templateFolder, newVersionFolder);
                ////Debug.Log($"[ClawBuild] Created {newVersionFolder}");

                // Step 4: Delete old version folder
                if (currentVersion >= 0)
                {
                    string oldFolder = Path.Combine(gameFolder, $"v{currentVersion}");
                    if (Directory.Exists(oldFolder))
                    {
                        Directory.Delete(oldFolder, true);
                        ////Debug.Log($"[ClawBuild] Deleted v{currentVersion}");
                    }
                }

                // Step 5: Copy addressables to new version
                AddressablesTextureVariants.CopyToServerData(Path.Combine(newVersionFolder, "ServerData"),
                    message => Debug.Log("[ClawBuild] " + message));
                ////Debug.Log("[ClawBuild] Copied addressables");

                // Step 6: Build WebGL
                string buildPath = Path.Combine(newVersionFolder, "SickOfIt");
                BuildWebGLTo(buildPath);

                // Step 7: Find & replace version in website
                if (currentVersion >= 0)
                {
                    ReplaceVersionInWebsite(websitePath, $"v{currentVersion}", $"v{newVersion}");
                }

                ////Debug.Log($"[ClawBuild] Full deploy complete! Version: v{newVersion}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClawBuild] FAILED: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Build WebGL only (no addressables, no version management).
        /// Output goes to Game/v{version}/SickOfIt in the website project.
        /// </summary>
        public static void BuildWebGL()
        {
            try
            {
                string websitePath = GetEnv("WEBSITE_PATH", DefaultWebsitePath);
                string gameFolder = Path.Combine(websitePath, "Game");
                int version = DetectVersion(gameFolder);
                if (version < 0) version = 1;

                string buildPath = Path.Combine(gameFolder, $"v{version}", "SickOfIt");
                BuildWebGLTo(buildPath);
                ////Debug.Log($"[ClawBuild] WebGL build complete: {buildPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClawBuild] FAILED: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Build addressables only (DXT and ASTC variants).
        /// </summary>
        public static void BuildAddressables()
        {
            try
            {
                AddressablesTextureVariants.Build(message => Debug.Log("[ClawBuild] " + message));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClawBuild] FAILED: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.Exit(1);
            }
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static void BuildWebGLTo(string buildPath)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new Exception("No scenes enabled in Build Settings.");

            ////Debug.Log($"[ClawBuild] Building {scenes.Length} scene(s) to {buildPath}...");

            if (Directory.Exists(buildPath))
                Directory.Delete(buildPath, true);
            Directory.CreateDirectory(buildPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception($"WebGL build failed: {report.summary.result}");

            RaiseUnityCacheLimit(buildPath);
        }

        /// <summary>
        /// Unity's WebGL loader caps its browser cache at 1 GiB and evicts older
        /// bundles on every startup. This build is larger than that, so raise the
        /// cap in the generated loader. There is no player setting for it.
        /// </summary>
        public static void RaiseUnityCacheLimit(string buildPath)
        {
            const string oldLimit = "MaximumCacheSize=1073741824";
            const string newLimit = "MaximumCacheSize=4294967296";
            string loaderDir = Path.Combine(buildPath, "Build");
            if (!Directory.Exists(loaderDir))
                return;

            foreach (string file in Directory.GetFiles(loaderDir, "*.loader.js"))
            {
                string content = File.ReadAllText(file);
                if (!content.Contains(oldLimit))
                    continue;
                File.WriteAllText(file, content.Replace(oldLimit, newLimit));
            }
        }

        private static int DetectVersion(string gameFolder)
        {
            if (!Directory.Exists(gameFolder)) return -1;

            int max = -1;
            foreach (var dir in Directory.GetDirectories(gameFolder))
            {
                string name = Path.GetFileName(dir);
                if (name == "v0000000") continue;
                var match = Regex.Match(name, @"^v(\d+)$");
                if (match.Success)
                {
                    int ver = int.Parse(match.Groups[1].Value);
                    if (ver > max) max = ver;
                }
            }
            return max;
        }

        private static void ReplaceVersionInWebsite(string websitePath, string oldVer, string newVer)
        {
            ////Debug.Log($"[ClawBuild] Replacing '{oldVer}' → '{newVer}' in website...");

            string[] extensions = { ".html", ".htm", ".js", ".css", ".json", ".yml", ".yaml",
                                    ".bat", ".sh", ".md", ".txt", ".conf", ".cfg", ".env",
                                    ".xml", ".toml", ".ini" };

            var files = Directory.GetFiles(websitePath, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string rel = f.Substring(websitePath.Length);
                    if (rel.Contains(@"\.git\") || rel.Contains(@"\node_modules\")) return false;
                    if (rel.Contains(@"\Game\v") && !rel.Contains(@"\Game\v0000000")) return false;
                    return extensions.Contains(Path.GetExtension(f).ToLowerInvariant());
                });

            int fileCount = 0;
            foreach (string file in files)
            {
                string content = File.ReadAllText(file);
                if (content.Contains(oldVer))
                {
                    File.WriteAllText(file, content.Replace(oldVer, newVer));
                    fileCount++;
                }
            }
            ////Debug.Log($"[ClawBuild] Replaced in {fileCount} file(s)");
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        private static string GetEnv(string name, string defaultValue)
        {
            string val = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(val) ? defaultValue : val;
        }
    }
}
