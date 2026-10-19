#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Tells Hawkes Analytics which build the players are on (AGENTS.md "Analytics"): before a player build it writes
    /// Assets/Resources/HawkesBuildInfo.json ({ version, commit, date }: the player version, the commit checked out,
    /// the build time), which AnalyticsManager hands the page as window.HAWKES_BUILD (plus "+v&lt;n&gt;" from the
    /// Game/v&lt;n&gt;/ folder it is loaded from). The file is removed after the build and never committed (.gitignore).
    /// </summary>
    sealed class AnalyticsBuildInfo : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string AssetPath = "Assets/Resources/HawkesBuildInfo.json";

        public int callbackOrder => -900;

        [Serializable]
        class Info { public string version; public string commit; public string date; }

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                var info = new Info
                {
                    version = PlayerSettings.bundleVersion,
                    commit = Commit(),
                    date = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                };
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                File.WriteAllText(AssetPath, JsonUtility.ToJson(info));
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[Analytics] Build info for Hawkes Analytics: version {info.version}, commit {(string.IsNullOrEmpty(info.commit) ? "(unknown)" : info.commit)}, {info.date}");
            }
            catch (Exception e)
            {
                // Never stops a build: the bridge then reports Application.version alone.
                Debug.Log("[Analytics] No build info written: " + e.Message);
            }
        }

        public void OnPostprocessBuild(BuildReport report) => Remove();

        /// <summary>Removes the file (after the build, and on the next editor start if a failed build left it).</summary>
        [InitializeOnLoadMethod]
        static void Remove()
        {
            if (!File.Exists(AssetPath))
                return;
            if (BuildPipeline.isBuildingPlayer)
                return;
            AssetDatabase.DeleteAsset(AssetPath);
        }

        /// <summary>The commit checked out: GIT_COMMIT / BUILD_COMMIT from the build server, else git.</summary>
        static string Commit()
        {
            foreach (var name in new[] { "BUILD_COMMIT", "GIT_COMMIT" })
            {
                string value = Environment.GetEnvironmentVariable(name);
                if (IsHash(value))
                    return value.Trim().Substring(0, Math.Min(12, value.Trim().Length));
            }
            try
            {
                var start = new ProcessStartInfo("git", "rev-parse --short=12 HEAD")
                {
                    WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var git = Process.Start(start))
                {
                    string output = git.StandardOutput.ReadToEnd().Trim();
                    git.WaitForExit(5000);
                    return IsHash(output) ? output : "";
                }
            }
            catch
            {
                return "";
            }
        }

        static bool IsHash(string value) => !string.IsNullOrEmpty(value) && Regex.IsMatch(value.Trim(), "^[0-9a-fA-F]{4,40}$");
    }
}
#endif
