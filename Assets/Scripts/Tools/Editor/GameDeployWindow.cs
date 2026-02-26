using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Debug = UnityEngine.Debug;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Editor window that automates the full game update and deployment pipeline:
    /// delete server data, build addressables, version bump, copy output,
    /// build WebGL, find-replace version strings, and launch dev server.
    /// </summary>
    public class GameDeployWindow : EditorWindow
    {
        // Paths
        private const string UnityProjectPath = @"C:\Projects\sickofitgame-unity";
        private const string WebsiteProjectPath = @"C:\Projects\sickofitgame-website";
        private const string ServerDataWebGL = UnityProjectPath + @"\ServerData\WebGL";
        private const string GameFolder = WebsiteProjectPath + @"\Game";
        private const string TemplateFolder = GameFolder + @"\v0000000";
        private const string AutoStartBat = WebsiteProjectPath + @"\auto-start.bat";

        // State
        private int detectedVersion = -1;
        private int newVersion = -1;
        private Vector2 scrollPos;
        private string statusLog = "";
        private bool isBusy;

        // Step tracking
        private int currentStep;
        private int totalSteps = 8;

        [MenuItem("Tools/Game Deploy")]
        public static void ShowWindow()
        {
            var window = GetWindow<GameDeployWindow>("Game Deploy");
            window.minSize = new Vector2(500, 600);
            window.RefreshVersionInfo();
        }

        [MenuItem("Tools/Run Full Deploy")]
        public static void RunFullDeployFromMenu()
        {
            var window = GetWindow<GameDeployWindow>("Game Deploy");
            window.RefreshVersionInfo();
            window.RunFullDeploy();
        }

        private void OnEnable()
        {
            RefreshVersionInfo();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.LabelField("Sick Of It - Game Deploy", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Version info
            EditorGUILayout.LabelField("Version Info", EditorStyles.boldLabel);

            if (detectedVersion >= 0)
            {
                EditorGUILayout.HelpBox(
                    $"Current version: v{detectedVersion}\n" +
                    $"New version:     v{newVersion}",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No existing version folders found.\nNew version will be v1.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Refresh Version Info"))
                RefreshVersionInfo();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
            DrawReadOnlyField("Unity Project", UnityProjectPath);
            DrawReadOnlyField("Website Project", WebsiteProjectPath);
            DrawReadOnlyField("Server Data", ServerDataWebGL);
            DrawReadOnlyField("Template", TemplateFolder);

            EditorGUILayout.Space(10);

            // Individual step buttons
            EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(isBusy))
            {
                if (GUILayout.Button("1. Delete Server Data"))
                    RunStep(() => Step_DeleteServerData());

                if (GUILayout.Button("2. Build Addressables"))
                    RunStep(() => Step_BuildAddressables());

                if (GUILayout.Button("3. Create New Version Folder"))
                    RunStep(() => Step_CreateVersionFolder());

                if (GUILayout.Button("4. Delete Old Version Folder"))
                    RunStep(() => Step_DeleteOldVersionFolder());

                if (GUILayout.Button("5. Copy Addressables to New Version"))
                    RunStep(() => Step_CopyAddressables());

                if (GUILayout.Button("6. Build WebGL"))
                    RunStep(() => Step_BuildWebGL());

                if (GUILayout.Button("7. Find & Replace Version in Website"))
                    RunStep(() => Step_FindReplaceVersion());

                if (GUILayout.Button("8. Run Dev Server & Open Browser"))
                    RunStep(() => Step_RunDevServer());
            }

            EditorGUILayout.Space(10);

            // Full deploy button
            EditorGUILayout.LabelField("Full Pipeline", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(isBusy))
            {
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button("Run Full Deploy", GUILayout.Height(40)))
                {
                    if (EditorUtility.DisplayDialog("Full Deploy",
                        $"This will:\n" +
                        $"1. Delete ServerData\\WebGL\n" +
                        $"2. Build Addressables\n" +
                        $"3. Create v{newVersion} from template\n" +
                        $"4. Delete v{detectedVersion}\n" +
                        $"5. Copy addressables to v{newVersion}\n" +
                        $"6. Build WebGL to v{newVersion}\\SickOfIt\n" +
                        $"7. Replace v{detectedVersion} → v{newVersion} in website\n" +
                        $"8. Run auto-start.bat & open browser\n\n" +
                        $"Continue?", "Deploy", "Cancel"))
                    {
                        RunFullDeploy();
                    }
                }
                GUI.backgroundColor = originalColor;
            }

            // Status log
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(statusLog, GUILayout.ExpandHeight(true));

            if (GUILayout.Button("Clear Log"))
                statusLog = "";

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────
        // Version detection
        // ─────────────────────────────────────────────

        private void RefreshVersionInfo()
        {
            detectedVersion = -1;
            newVersion = 1;

            if (!Directory.Exists(GameFolder))
            {
                Log("WARNING: Game folder not found: " + GameFolder);
                return;
            }

            var dirs = Directory.GetDirectories(GameFolder);
            int maxVersion = -1;

            foreach (var dir in dirs)
            {
                string name = Path.GetFileName(dir);
                if (name == "v0000000") continue;

                var match = Regex.Match(name, @"^v(\d+)$");
                if (match.Success)
                {
                    int ver = int.Parse(match.Groups[1].Value);
                    if (ver > maxVersion)
                        maxVersion = ver;
                }
            }

            if (maxVersion >= 0)
            {
                detectedVersion = maxVersion;
                newVersion = maxVersion + 1;
            }

            Repaint();
        }

        // ─────────────────────────────────────────────
        // Pipeline steps
        // ─────────────────────────────────────────────

        private void Step_DeleteServerData()
        {
            Log("Step 1: Deleting ServerData\\WebGL...");

            if (Directory.Exists(ServerDataWebGL))
            {
                Directory.Delete(ServerDataWebGL, true);
                Log("  Deleted: " + ServerDataWebGL);
            }
            else
            {
                Log("  Already clean (folder not found).");
            }
        }

        private void Step_BuildAddressables()
        {
            Log("Step 2: Building Addressables...");

            AddressableAssetSettings.CleanPlayerContent(
                AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Log("  ERROR: Addressables build failed: " + result.Error);
                throw new Exception("Addressables build failed: " + result.Error);
            }

            Log("  Addressables build succeeded. Duration: " + result.Duration + "s");
        }

        private void Step_CreateVersionFolder()
        {
            RefreshVersionInfo();
            string newVersionFolder = Path.Combine(GameFolder, $"v{newVersion}");

            Log($"Step 3: Creating v{newVersion} from template...");

            if (!Directory.Exists(TemplateFolder))
            {
                Log("  ERROR: Template folder not found: " + TemplateFolder);
                throw new Exception("Template folder not found: " + TemplateFolder);
            }

            if (Directory.Exists(newVersionFolder))
            {
                Log($"  WARNING: v{newVersion} already exists. Deleting it first...");
                Directory.Delete(newVersionFolder, true);
            }

            CopyDirectoryRecursive(TemplateFolder, newVersionFolder);
            Log($"  Created: {newVersionFolder}");
        }

        private void Step_DeleteOldVersionFolder()
        {
            if (detectedVersion < 0)
            {
                Log("Step 4: No old version to delete. Skipping.");
                return;
            }

            string oldVersionFolder = Path.Combine(GameFolder, $"v{detectedVersion}");
            Log($"Step 4: Deleting old version folder v{detectedVersion}...");

            if (Directory.Exists(oldVersionFolder))
            {
                Directory.Delete(oldVersionFolder, true);
                Log($"  Deleted: {oldVersionFolder}");
            }
            else
            {
                Log($"  Folder not found (already deleted?): {oldVersionFolder}");
            }
        }

        private void Step_CopyAddressables()
        {
            string destWebGL = Path.Combine(GameFolder, $"v{newVersion}", "ServerData", "WebGL");

            Log($"Step 5: Copying addressables to v{newVersion}...");

            if (!Directory.Exists(ServerDataWebGL))
            {
                Log("  ERROR: Addressables output not found: " + ServerDataWebGL);
                throw new Exception("Addressables output not found. Did you build addressables first?");
            }

            // Ensure destination parent exists
            Directory.CreateDirectory(Path.GetDirectoryName(destWebGL));

            if (Directory.Exists(destWebGL))
                Directory.Delete(destWebGL, true);

            CopyDirectoryRecursive(ServerDataWebGL, destWebGL);
            Log($"  Copied to: {destWebGL}");
        }

        private void Step_BuildWebGL()
        {
            string buildPath = Path.Combine(GameFolder, $"v{newVersion}", "SickOfIt");

            Log($"Step 6: Building WebGL to {buildPath}...");

            // Get all enabled scenes from build settings
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Log("  ERROR: No scenes enabled in Build Settings.");
                throw new Exception("No scenes enabled in Build Settings.");
            }

            Log($"  Building {scenes.Length} scene(s)...");

            // Clean the output folder
            if (Directory.Exists(buildPath))
                Directory.Delete(buildPath, true);
            Directory.CreateDirectory(buildPath);

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildOptions);

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Log($"  ERROR: WebGL build failed. Result: {report.summary.result}");
                throw new Exception($"WebGL build failed: {report.summary.result}");
            }

            Log($"  WebGL build succeeded! Size: {report.summary.totalSize / (1024 * 1024)}MB, " +
                $"Duration: {report.summary.totalTime.TotalSeconds:F1}s");
        }

        private void Step_FindReplaceVersion()
        {
            if (detectedVersion < 0)
            {
                Log("Step 7: No old version to replace. Skipping.");
                return;
            }

            string oldVersionStr = $"v{detectedVersion}";
            string newVersionStr = $"v{newVersion}";

            Log($"Step 7: Replacing '{oldVersionStr}' → '{newVersionStr}' in website project...");

            int fileCount = 0;
            int replaceCount = 0;

            // Process text files in the root directory of the website project
            string[] extensions = { ".html", ".htm", ".js", ".css", ".json", ".yml", ".yaml",
                                    ".bat", ".sh", ".md", ".txt", ".conf", ".cfg", ".env",
                                    ".xml", ".toml", ".ini" };

            var files = Directory.GetFiles(WebsiteProjectPath, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    // Skip .git folder, node_modules, and the Game version folders themselves
                    string rel = f.Substring(WebsiteProjectPath.Length);
                    if (rel.Contains(@"\.git\") || rel.Contains(@"\node_modules\")) return false;
                    if (rel.Contains(@"\Game\v") && !rel.Contains(@"\Game\v0000000")) return false;

                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return extensions.Contains(ext);
                })
                .ToArray();

            foreach (string file in files)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    if (content.Contains(oldVersionStr))
                    {
                        string updated = content.Replace(oldVersionStr, newVersionStr);
                        File.WriteAllText(file, updated);
                        int count = CountOccurrences(content, oldVersionStr);
                        replaceCount += count;
                        fileCount++;
                        string relativePath = file.Substring(WebsiteProjectPath.Length + 1);
                        Log($"  {relativePath} ({count} replacement(s))");
                    }
                }
                catch (Exception ex)
                {
                    Log($"  WARNING: Could not process {file}: {ex.Message}");
                }
            }

            Log($"  Done. {replaceCount} replacement(s) across {fileCount} file(s).");
        }

        private void Step_RunDevServer()
        {
            Log("Step 8: Running auto-start.bat and opening browser...");

            if (!File.Exists(AutoStartBat))
            {
                Log("  ERROR: auto-start.bat not found: " + AutoStartBat);
                throw new Exception("auto-start.bat not found: " + AutoStartBat);
            }

            // Launch auto-start.bat in a new cmd window
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{AutoStartBat}\"",
                WorkingDirectory = WebsiteProjectPath,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            Process.Start(startInfo);
            Log("  Started auto-start.bat");

            // Wait a moment then open browser
            EditorApplication.delayCall += () =>
            {
                // Give Docker a few seconds to start
                EditorApplication.delayCall += () =>
                {
                    Application.OpenURL("http://localhost/Game");
                    Log("  Opened browser at http://localhost/Game");
                };
            };
        }

        // ─────────────────────────────────────────────
        // Full deploy pipeline
        // ─────────────────────────────────────────────

        internal void RunFullDeploy()
        {
            isBusy = true;
            currentStep = 0;
            statusLog = "";

            Log("═══════════════════════════════════════");
            Log("  FULL DEPLOY STARTED");
            Log($"  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log("═══════════════════════════════════════");
            Log("");

            try
            {
                Step_DeleteServerData();
                Log("");

                Step_BuildAddressables();
                Log("");

                Step_CreateVersionFolder();
                Log("");

                Step_DeleteOldVersionFolder();
                Log("");

                Step_CopyAddressables();
                Log("");

                Step_BuildWebGL();
                Log("");

                Step_FindReplaceVersion();
                Log("");

                Step_RunDevServer();

                Log("");
                Log("═══════════════════════════════════════");
                Log("  DEPLOY COMPLETE!");
                Log($"  Version: v{newVersion}");
                Log("═══════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Log("");
                Log("═══════════════════════════════════════");
                Log("  DEPLOY FAILED");
                Log($"  Error: {ex.Message}");
                Log("═══════════════════════════════════════");
                Debug.LogError($"[GameDeploy] Deploy failed: {ex}");
            }
            finally
            {
                isBusy = false;
                Repaint();
            }
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private void RunStep(Action action)
        {
            isBusy = true;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Debug.LogError($"[GameDeploy] {ex}");
            }
            finally
            {
                isBusy = false;
                Repaint();
            }
        }

        private static void CopyDirectoryRecursive(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destDir);
            }
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
            {
                count++;
                i += pattern.Length;
            }
            return count;
        }

        private void Log(string message)
        {
            statusLog += message + "\n";
            Debug.Log("[GameDeploy] " + message);
            Repaint();
        }

        private static void DrawReadOnlyField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }
    }
}
