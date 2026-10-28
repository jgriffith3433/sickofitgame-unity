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
        // This project is where Unity has it open; the website is HAWKES_SITE_PATH / WEBSITE_PATH when a build server
        // sets them (Hawkes Gen's Linux build container), else the sickofitgamewebsite folder beside this project.
        private static readonly string UnityProjectPath = Path.GetDirectoryName(Application.dataPath) ?? "";
        private static readonly string WebsiteProjectPath = Path.GetFullPath(
            Environment.GetEnvironmentVariable("HAWKES_SITE_PATH") is { Length: > 0 } site ? site
            : Environment.GetEnvironmentVariable("WEBSITE_PATH") is { Length: > 0 } web ? web
            : Path.Combine(UnityProjectPath, "..", "sickofitgamewebsite")).TrimEnd('\\', '/');
        private static readonly string ServerDataWebGL = Path.Combine(UnityProjectPath, "ServerData", "WebGL");
        private static readonly string GameFolder = Path.Combine(WebsiteProjectPath, "Game");
        private static readonly string TemplateFolder = Path.Combine(GameFolder, "v0000000");
        private static readonly string AutoStartBat = Path.Combine(WebsiteProjectPath, "auto-start.bat");

        // State
        private int detectedVersion = -1;
        private int newVersion = -1;
        private Vector2 scrollPos;
        private string statusLog = "";
        private bool isBusy;
        // Batch mode (FullDeployBatch): no window; every log line goes to the Unity log.
        private bool batch;

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

        /// <summary>
        /// Run Full Deploy without a window, for the build server:
        /// Unity.exe -batchmode -projectPath C:/Projects/sickofitgame-unity -executeMethod SickOfIt.Tools.Editor.GameDeployWindow.FullDeployBatch
        /// Same steps (Addressables, a new Game/v&lt;n&gt;, the loader and page update, removing the old version) except
        /// starting the local dev server. BUILD_VERSION (environment) picks the version number instead of latest + 1.
        /// Exits 0 on success, 1 on failure.
        /// </summary>
        public static void FullDeployBatch()
        {
            var deploy = CreateInstance<GameDeployWindow>();
            deploy.batch = true;
            int code = 1;
            try
            {
                deploy.RefreshVersionInfo();
                deploy.RunPipeline(startDevServer: false);
                code = 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameDeploy] Deploy failed: {ex}");
            }
            finally
            {
                DestroyImmediate(deploy);
            }
            EditorApplication.Exit(code);
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

                if (GUILayout.Button("7b. Install the Tap-to-Play Sound Prompt"))
                    RunStep(() => Step_InstallSoundPrompt());

                if (GUILayout.Button("7d. Install the Site Content (lore pages, blog posts)"))
                    RunStep(() => Step_InstallSiteContent());

                if (GUILayout.Button("7c. Install the Powered by HAWKES GEN Mark on the Site"))
                    RunStep(() => Step_InstallCredit());

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
                        $"7b. Install the tap-to-play sound prompt in Game\\index.html\n" +
                        $"7d. Install the site content from Tools\\Web\\site (lore, blog posts)\n" +
                        $"7c. Put Powered by HAWKES GEN on every site page (not the game)\n" +
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

            string overrideVersion = batch ? Environment.GetEnvironmentVariable("BUILD_VERSION") : null;
            if (!string.IsNullOrEmpty(overrideVersion))
            {
                if (!int.TryParse(overrideVersion.TrimStart('v', 'V'), out int forced) || forced < 1)
                    throw new Exception("BUILD_VERSION must be a positive number, got: " + overrideVersion);
                newVersion = forced;
                Log($"BUILD_VERSION={forced}: building v{forced} (latest existing: v{detectedVersion}).");
            }

            if (!batch)
                Repaint();
        }

        // ─────────────────────────────────────────────
        // Pipeline steps
        // ─────────────────────────────────────────────

        private void Step_ImportGameConfig()
        {
            Log("Step 0: Importing the game config (Assets/GameConfig -> GameDefinition.asset)...");
            // Always a full import: the committed GameDefinition.asset is an old compile, and the build must never ship it.
            GameConfig.GameConfigSync.ImportForBuild("deploy");
            Log($"  Game config imported and verified (fingerprint {GameConfig.GameConfigSync.Fingerprint()}).");
        }

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

        private void Step_SplitPlayerData()
        {
            Log("Step 1b: Moving GameDefinition and its art and sound out of the player data into Addressables...");
            PlayerDataSplit.Begin(Log);
        }

        private void Step_CheckFileSizes()
        {
            Log($"Step 6b: Checking v{newVersion}'s file sizes (GitHub refuses files over 100 MB)...");
            BuildOutputGuard.Check(Path.Combine(GameFolder, $"v{newVersion}"), Log);
        }

        private void Step_BuildAddressables()
        {
            Log("Step 2: Building Addressables (DXT for desktop, ASTC for phones)...");
            AddressablesTextureVariants.Build(Log);
            Log("  Addressables build succeeded.");
        }

        private void Step_CreateVersionFolder()
        {
            RefreshVersionInfo();
            string newVersionFolder = Path.Combine(GameFolder, $"v{newVersion}");

            Log($"Step 3: Creating v{newVersion} from template...");

            // The template is an empty folder, and git can't store empty folders: a fresh clone of the website (the
            // build server's copy) doesn't have it. Create it rather than fail.
            if (!Directory.Exists(TemplateFolder))
            {
                Log("  Template folder not found (a fresh checkout): creating an empty " + TemplateFolder);
                Directory.CreateDirectory(TemplateFolder);
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
            if (detectedVersion < 0 || detectedVersion == newVersion)
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
            Log($"Step 5: Copying addressables to v{newVersion}...");
            AddressablesTextureVariants.CopyToServerData(Path.Combine(GameFolder, $"v{newVersion}", "ServerData"), Log);
        }

        private void Step_BuildWebGL()
        {
            string buildPath = Path.Combine(GameFolder, $"v{newVersion}", "SickOfIt");

            Log($"Step 6: Building WebGL to {buildPath}...");

            // Get all enabled scenes from build settings
            string[] scenes = PlayerDataSplit.PlayerScenes(EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray());

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

            ClawBuildScript.RaiseUnityCacheLimit(buildPath);

            Log($"  WebGL build succeeded! Size: {report.summary.totalSize / (1024 * 1024)}MB, " +
                $"Duration: {report.summary.totalTime.TotalSeconds:F1}s");
        }

        private void Step_FindReplaceVersion()
        {
            if (detectedVersion < 0 || detectedVersion == newVersion)
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
                    string rel = f.Substring(WebsiteProjectPath.Length).Replace('\\', '/');
                    if (rel.Contains("/.git/") || rel.Contains("/node_modules/")) return false;
                    if (rel.Contains("/Game/v") && !rel.Contains("/Game/v0000000")) return false;

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

        /// <summary>
        /// The Hawkes standard sound prompt (shared memory m_d00535b22659): copies Tools/Web/hawkes-sound.js from this
        /// project to Game/v&lt;n&gt;/ and makes sure Game/index.html loads it first in its &lt;head&gt; (before Unity's
        /// loader, which it has to wrap). The script lives here so a build of this repo always ships it; the website
        /// only holds the copy.
        /// </summary>
        private void Step_InstallSoundPrompt()
        {
            string page = Path.Combine(GameFolder, "index.html");
            if (!File.Exists(page))
                throw new Exception("Game page not found: " + page);
            string html = File.ReadAllText(page);

            // The build just made v<newVersion>; run on its own (the button), the page's current version.
            int version = newVersion;
            if (!Directory.Exists(Path.Combine(GameFolder, $"v{version}")))
            {
                var current = Regex.Match(html, @"gameVersion\s*=\s*""v(\d+)""");
                version = current.Success ? int.Parse(current.Groups[1].Value) : -1;
            }
            Log($"Step 7b: Installing the tap-to-play sound prompt (v{version})...");
            if (version < 0 || !Directory.Exists(Path.Combine(GameFolder, $"v{version}")))
                throw new Exception($"No Game/v{version} folder to install the sound prompt into.");

            string source = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Tools", "Web", SoundPromptFile);
            if (!File.Exists(source))
                throw new Exception("Sound prompt script not found: " + source);
            string target = Path.Combine(GameFolder, $"v{version}", SoundPromptFile);
            File.Copy(source, target, true);
            Log($"  Copied {SoundPromptFile} to v{version}");

            string updated = InstallSoundPromptTag(html, version);
            if (updated == null)
                throw new Exception("Game/index.html has no <head> to put the sound prompt in.");
            if (updated != html)
            {
                File.WriteAllText(page, updated);
                Log("  Game/index.html now loads v" + version + "/" + SoundPromptFile);
            }
            else
            {
                Log("  Game/index.html already loads it.");
            }
        }

        private const string SoundPromptFile = "hawkes-sound.js";

        /// <summary>
        /// The page with exactly one &lt;script src="v&lt;n&gt;/hawkes-sound.js"&gt; as the first thing in &lt;head&gt;
        /// (an existing tag is moved and repointed), or null without a &lt;head&gt;.
        /// </summary>
        internal static string InstallSoundPromptTag(string html, int version)
        {
            string newline = html.Contains("\r\n") ? "\r\n" : "\n";
            string tag = $"<script src=\"v{version}/{SoundPromptFile}\"></script>";
            string comment = "<!-- Hawkes sound prompt: installed by the deploy from sickofitgame-unity Tools/Web; must load before Unity's loader -->";
            string without = Regex.Replace(html, @"[ \t]*<script[^>]*hawkes-sound\.js[^>]*>\s*</script>[ \t]*\r?\n?", "");
            without = Regex.Replace(without, @"[ \t]*<!-- Hawkes sound prompt:[^>]*-->[ \t]*\r?\n?", "");
            var head = Regex.Match(without, @"<head[^>]*>", RegexOptions.IgnoreCase);
            if (!head.Success)
                return null;
            int at = head.Index + head.Length;
            string insert = newline + "    " + comment + newline + "    " + tag;
            return without.Substring(0, at) + insert + without.Substring(at);
        }

        /// <summary>
        /// The website content this repo owns (Tools/Web/site: the lore pages, the descent's art and script, blog
        /// posts and their cards on the blog's lists), copied into the website so it ships with this build. Runs
        /// before step 7c, which then puts the credit on the new pages too. See <see cref="SiteContent"/>.
        /// </summary>
        private void Step_InstallSiteContent()
        {
            Log("Step 7d: Installing the site content (Tools/Web/site)...");
            int missed = SiteContent.Install(SiteContent.SourceFolder, WebsiteProjectPath, Log);
            Log(missed == 0 ? "  Done." : $"  Done, {missed} insert(s) skipped (see the warnings above).");
        }

        // ─────────────────────────────────────────────
        // Hawkes credit (Powered by HAWKES GEN, and the parent company on the landing page)
        // ─────────────────────────────────────────────

        private const string HawkesGenUrl = "https://hawkesgen.com";
        private const string ParentUrl = "https://hawkesbiomedical.com";

        /// <summary>
        /// Puts the small "Powered by HAWKES GEN" mark (linking to hawkesgen.com) on every page of the website except
        /// the game (Game/), and on the landing page also the parent company link (hawkesbiomedical.com). It sits in
        /// the page's footer when it has one, else at the bottom of the page (pinned on the one-screen landing and
        /// offline pages). The mark is generated here, so a build of this repo always ships it; the website only
        /// holds the copy. Re-running replaces it.
        /// </summary>
        private void Step_InstallCredit()
        {
            Log("Step 7c: Installing the Powered by HAWKES GEN mark on the website's pages...");
            int changed = 0, already = 0;
            foreach (string file in Directory.GetFiles(WebsiteProjectPath, "*.html", SearchOption.AllDirectories))
            {
                string rel = file.Substring(WebsiteProjectPath.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (!IsSitePage(rel))
                    continue;
                string html = File.ReadAllText(file);
                string updated = InstallCredit(html, CreditPlacementFor(rel));
                if (updated == null)
                {
                    Log("  WARNING: no </body> in " + rel + ", skipped");
                    continue;
                }
                if (updated == html)
                {
                    already++;
                    continue;
                }
                File.WriteAllText(file, updated);
                changed++;
                Log("  " + rel);
            }
            Log($"  Done. {changed} page(s) updated, {already} already had it.");
        }

        /// <summary>Every website page but the game itself (Game/, its version folders) and repo plumbing.</summary>
        public static bool IsSitePage(string relativePath)
        {
            string rel = relativePath.Replace('\\', '/');
            if (rel.StartsWith("Game/", StringComparison.OrdinalIgnoreCase))
                return false;
            if (rel.StartsWith(".git/") || rel.Contains("node_modules/"))
                return false;
            return rel.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
        }

        public enum CreditPlacement
        {
            /// <summary>Inside the page's last &lt;footer&gt; (the blog's pages).</summary>
            Footer,
            /// <summary>A footer bar of its own at the end of the page.</summary>
            Bar,
            /// <summary>Pinned to the bottom of the screen (one-screen pages: the landing page, offline).</summary>
            Pinned,
            /// <summary>Pinned, with the parent company link too (the landing page).</summary>
            Landing,
        }

        public static CreditPlacement CreditPlacementFor(string relativePath)
        {
            string rel = relativePath.Replace('\\', '/');
            if (rel == "index.html")
                return CreditPlacement.Landing;
            if (rel == "offline.html")
                return CreditPlacement.Pinned;
            return CreditPlacement.Footer; // InstallCredit falls back to Bar without a <footer>
        }

        private const string CreditStart = "<!-- Hawkes credit: installed by the deploy from sickofitgame-unity (GameDeployWindow); edit it there -->";
        private const string CreditEnd = "<!-- /Hawkes credit -->";

        /// <summary>
        /// The page with exactly one Hawkes credit block (an existing one is replaced), or null without a &lt;/body&gt;.
        /// </summary>
        public static string InstallCredit(string html, CreditPlacement placement)
        {
            string newline = html.Contains("\r\n") ? "\r\n" : "\n";
            string without = Regex.Replace(html, @"[ \t]*<!-- Hawkes credit:.*?<!-- /Hawkes credit -->[ \t]*\r?\n?", "", RegexOptions.Singleline);

            int footer = without.LastIndexOf("</footer>", StringComparison.OrdinalIgnoreCase);
            if (placement == CreditPlacement.Footer && footer < 0)
                placement = CreditPlacement.Bar;
            int at = placement == CreditPlacement.Footer ? footer : without.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (at < 0)
                return null;

            // On its own lines just before the closing tag, indented one step inside it.
            int lineStart = at > 0 ? without.LastIndexOf('\n', at - 1) + 1 : 0;
            string indent = without.Substring(lineStart, at - lineStart);
            string block = string.Join(newline, CreditLines(placement, (indent.Trim().Length > 0 ? "" : indent) + "  ")) + newline;
            if (indent.Trim().Length > 0)
                return without.Substring(0, at) + newline + block + without.Substring(at);
            return without.Substring(0, lineStart) + block + without.Substring(lineStart);
        }

        private static string[] CreditLines(CreditPlacement placement, string pad)
        {
            string gen = $"<a href=\"{HawkesGenUrl}\" target=\"_blank\" rel=\"noopener\" data-hawkes-credit=\"gen\">Powered by <strong>HAWKES GEN</strong></a>";
            string parent = $"<a href=\"{ParentUrl}\" target=\"_blank\" rel=\"noopener\" data-hawkes-credit=\"parent\">A <strong>HAWKES BIOMEDICAL</strong> company</a>";
            const string style =
                "<style>" +
                ".hawkes-credit{margin:.5rem 0 0;font:12px/1.5 Arial,sans-serif;letter-spacing:.03em;text-align:center;color:rgba(255,255,255,.45)}" +
                ".hawkes-credit a{display:inline-block;white-space:nowrap;color:rgba(255,255,255,.6);text-decoration:none;transition:color .2s,text-shadow .2s}" +
                ".hawkes-credit a:hover,.hawkes-credit a:focus{color:#fff;text-shadow:0 0 12px rgba(74,144,226,.8)}" +
                ".hawkes-credit strong{font-weight:700;letter-spacing:.14em;color:rgba(255,255,255,.85)}" +
                ".hawkes-credit-sep{margin:0 .6em}" +
                ".hawkes-credit-bar{margin:0;padding:1.5rem 1rem;background:#000;border-top:1px solid rgba(13,110,253,.25)}" +
                ".hawkes-credit-pinned{position:fixed;left:0;right:0;bottom:0;margin:0;padding:12px 16px calc(12px + env(safe-area-inset-bottom));z-index:2100}" +
                "@media (max-width:480px){.hawkes-credit-sep{display:none}.hawkes-credit-pinned a{display:block;margin-top:2px}}" +
                "</style>";
            switch (placement)
            {
                case CreditPlacement.Landing:
                    return new[]
                    {
                        pad + CreditStart, pad + style,
                        pad + $"<div class=\"hawkes-credit hawkes-credit-pinned\">{parent}<span class=\"hawkes-credit-sep\">&middot;</span>{gen}</div>",
                        pad + CreditEnd,
                    };
                case CreditPlacement.Pinned:
                    return new[] { pad + CreditStart, pad + style, pad + $"<div class=\"hawkes-credit hawkes-credit-pinned\">{gen}</div>", pad + CreditEnd };
                case CreditPlacement.Bar:
                    return new[] { pad + CreditStart, pad + style, pad + $"<footer class=\"hawkes-credit hawkes-credit-bar\">{gen}</footer>", pad + CreditEnd };
                default:
                    return new[] { pad + CreditStart, pad + style, pad + $"<p class=\"hawkes-credit\">{gen}</p>", pad + CreditEnd };
            }
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
            try
            {
                RunPipeline(startDevServer: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameDeploy] Deploy failed: {ex}");
            }
            finally
            {
                isBusy = false;
                Repaint();
            }
        }

        /// <summary>Every deploy step in order; logs and rethrows the first failure.</summary>
        private void RunPipeline(bool startDevServer)
        {
            Log("═══════════════════════════════════════");
            Log("  FULL DEPLOY STARTED");
            Log($"  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log("═══════════════════════════════════════");
            Log("");

            try
            {
                Step_ImportGameConfig();
                Log("");

                Step_DeleteServerData();
                Log("");

                try
                {
                    Step_SplitPlayerData();
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
                }
                finally
                {
                    PlayerDataSplit.End(Log);
                }

                Step_CheckFileSizes();
                Log("");

                Step_FindReplaceVersion();
                Log("");

                Step_InstallSoundPrompt();
                Log("");

                // Before the credit, so the new pages get it too.
                Step_InstallSiteContent();
                Log("");

                Step_InstallCredit();
                Log("");

                if (startDevServer)
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
                throw;
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
            if (batch)
            {
                Debug.Log("[GameDeploy] " + message);
                return;
            }
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
