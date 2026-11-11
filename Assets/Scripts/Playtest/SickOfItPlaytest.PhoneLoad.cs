using System.Collections;
using System.Runtime.InteropServices;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.SceneManagement;
using SickOfIt.Core.Addressables;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using TMPro;
using Object = UnityEngine.Object;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The phone-load scenario (Docs/BuildSize.md): how fast and how light the game opens. The page's loading bar and the
        /// boot's bar (GameDefinitionBoot) both fill with progress, the main menu is up within 20 s of the page opening, what
        /// was downloaded by then, and the frame rate on the menu and in Level 1 (30+ fps). Run it with device "phone".
        /// </summary>
        public const string PhoneLoadId = "phone-load";
        const float PhoneLoadSeconds = 300f;
        const float PhoneLoadMenuBudget = 20f;
        const float PhoneLoadMinFps = 30f;
        // The menu needs the core art and sound (2-3 shared bundles), the Boot dependencies (1) and GameDefinition (1): a dozen is generous.
        const int PhoneLoadMenuBundleBudget = 12;
        const float PhoneLoadPlaySeconds = 30f;

        /// <summary>
        /// The page's clock and fetches (Plugins/WebGL/SickOfItLoad.jslib), and when the main menu first came up, recorded
        /// as it happens in every build (a scenario starts only after the boot, too late to see it).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void WatchPageLoad() => PageLoad.Watch();

        static class PageLoad
        {
            /// <summary>The main menu's first appearance: real seconds since the player started, and since the page opened (-1 = not yet / unknown).</summary>
            public static float MenuAtReal = -1f;
            public static float MenuAtPage = -1f;

#if UNITY_WEBGL && !UNITY_EDITOR
            [DllImport("__Internal")] static extern void SoiLoadWatch();
            [DllImport("__Internal")] static extern float SoiLoadPageSeconds();
            [DllImport("__Internal")] static extern string SoiLoadPageInfo(float untilSeconds);

            public static float PageSeconds() => SoiLoadPageSeconds();
            public static string Info(float untilSeconds) => SoiLoadPageInfo(untilSeconds) ?? "";
            static void StartWatching() => SoiLoadWatch();
#else
            public static float PageSeconds() => -1f;
            public static string Info(float untilSeconds) => "";
            static void StartWatching() { }
#endif

            public static void Watch()
            {
                try { StartWatching(); }
                catch (System.Exception e) { Debug.Log("[PageLoad] no page timing: " + e.Message); }
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (MenuAtReal >= 0f || Object.FindAnyObjectByType<MainMenuManager>() == null)
                    return;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                MenuAtReal = Time.realtimeSinceStartup;
                MenuAtPage = PageSeconds();
                Debug.Log($"[PageLoad] The main menu is up {(MenuAtPage >= 0f ? MenuAtPage.ToString("0.0") + " s after the page opened, " : "")}" +
                          $"{MenuAtReal:0.0} s after the player started.");
            }
        }

        [System.Serializable]
        sealed class PageLoadInfo
        {
            public string bar;
            public string barWidth;
            public bool barHidden;
            public int files;
            public int sized;
            public double bytes;
            public double player;
            public int playerFiles;
            public int bundles;
            public double bundleBytes;
            public int catalogs;
            public int astc;
            public string error;

            public static PageLoadInfo Read(float untilSeconds)
            {
                string json = PageLoad.Info(untilSeconds);
                if (string.IsNullOrEmpty(json))
                    return null;
                try { return JsonUtility.FromJson<PageLoadInfo>(json); }
                catch { return null; }
            }
        }

        sealed partial class Session
        {
            int phoneLoadFailed;

            void LoadCheck(string beat, bool ok, string detail)
            {
                if (ok) { Run.Event("check", $"phone-load {beat}: ok ({detail})"); return; }
                phoneLoadFailed++;
                Run.Event("check", $"phone-load {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"phone-load {beat}: {detail}");
            }

            static string Megabytes(double bytes) => (bytes / 1000000.0).ToString("0.0") + " MB";

            /// <summary>
            /// Why the main menu's hero is not on screen (null = he is): the deploy takes the scene's copy out of the player build
            /// (PlayerDataSplit.StripMenuHero: it was 22.8 MB of SickOfIt.data) and MainMenuManager.SpawnMenuHero puts one back at runtime,
            /// so a sprite drawn inside the camera's view is the proof nothing removed him for good.
            /// </summary>
            static string MenuHeroProblem()
            {
                var menu = Object.FindAnyObjectByType<MainMenuManager>();
                if (menu == null)
                    return "no main menu";
                var sr = menu.MenuHeroRenderer();
                if (sr == null)
                    return "no hero object on the menu";
                if (!sr.enabled || !sr.gameObject.activeInHierarchy)
                    return "his renderer is switched off";
                if (sr.sprite == null)
                    return "his renderer draws no sprite";
                var camera = Camera.main;
                if (camera == null)
                    return "no camera";
                var view = camera.WorldToViewportPoint(sr.bounds.center);
                if (view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f)
                    return $"he is off screen (viewport {view.x:0.00}, {view.y:0.00})";
                return null;
            }

            /// <summary>Frames per second over 'seconds' of real time while 'during' runs: (average, worst one-second window).</summary>
            IEnumerator SampleFps(float seconds, IEnumerator during, System.Action<float, float> result)
            {
                var go = new GameObject("PhoneLoadFps");
                var sampler = go.AddComponent<FpsSampler>();
                int frames0 = Time.frameCount;
                float start = Time.realtimeSinceStartup;
                if (during != null)
                    yield return during;
                else
                    yield return WaitReal(seconds);
                float took = Mathf.Max(0.001f, Time.realtimeSinceStartup - start);
                float average = (Time.frameCount - frames0) / took;
                float worst = sampler.Samples > 0 ? sampler.Worst : average;
                Object.Destroy(go);
                result(average, worst);
            }


            const int PressRuns = 3;
            const float PressWaitSeconds = 5f;
            const float PressBudgetSeconds = 3f;

            /// <summary>Digits-and-percent or "LOADING" text on screen right now (the menu's old indicator, or a bare loading bar).</summary>
            static string LoadingTextOnScreen()
            {
                foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                {
                    if (text == null || !text.isActiveAndEnabled || string.IsNullOrEmpty(text.text))
                        continue;
                    if (text.canvasRenderer != null && text.canvasRenderer.GetAlpha() <= 0.05f)
                        continue;
                    string t = text.text;
                    if (t.Contains("%") || t.IndexOf("LOADING", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t;
                }
                return null;
            }

            static bool AcceptingInput(int level)
            {
                var world = GameplayWorld.Instance;
                return LevelRunning(level) && !(world.Now < world.InputLockedUntil);
            }

            IEnumerator PhonePressRuns()
            {
                var labels = GameDefinition.Instance.mainMenuConfig.labels;
                var seconds = new System.Collections.Generic.List<float>();
                for (int run = 0; run < PressRuns && !Run.Done; run++)
                {
                    if (run > 0)
                    {
                        yield return ToMainMenu();
                        if (Run.Done) yield break;
                    }
                    // Buttons up: the moment the menu offers New Game.
                    yield return Run.Until(() =>
                    {
                        if (FindButton(NewGameLabel(labels)) == null)
                            Object.FindAnyObjectByType<MainMenuManager>()?.ShowPageHolding(NewGameLabel(labels));
                        var b = FindButton(NewGameLabel(labels));
                        return b != null && b.IsInteractable();
                    }, 60f, "the New Game button");
                    if (Run.Done) yield break;
                    float buttonsAt = Time.realtimeSinceStartup;
                    float glintStart = BundleDownloadUI.GlintShownAt;

                    // How long the menu's glint stays up after the buttons (it is a glint, never a percentage).
                    string stray = null;
                    while (!Run.Done && Time.realtimeSinceStartup - buttonsAt < PressWaitSeconds)
                    {
                        stray = stray ?? LoadingTextOnScreen();
                        yield return null;
                    }
                    if (Run.Done) yield break;
                    stray = stray ?? LoadingTextOnScreen();
                    float glintUp = BundleDownloadUI.GlintVisible ? -1f
                        : (BundleDownloadUI.GlintGoneAt > 0f ? BundleDownloadUI.GlintGoneAt - buttonsAt : 0f);
                    Run.Event("load", $"press run {run + 1}: menu glint {(BundleDownloadUI.GlintVisible ? "still up" : $"gone {glintUp:0.0} s after the buttons")} at 5 s, warm active {GameAssets.WarmActive}");
                    LoadCheck($"menu shows no percentage (run {run + 1})", stray == null,
                        stray == null ? "no \"%\" or LOADING text with the buttons up" : $"the menu showed \"{stray}\"");

                    // Press New Game (a real click) and time it to Level 1 taking input.
                    var button = FindButton(NewGameLabel(labels));
                    int coversBefore = ScreenTransition.Covers;
                    float pressed = Time.realtimeSinceStartup;
                    yield return ClickButton(button, "New Game (phone-load press " + (run + 1) + ")");
                    bool sawBar = false, bareBar = false, covered = false;
                    string barLabel = "";
                    float barProgressMax = 0f;
                    while (!Run.Done && !AcceptingInput(1) && Time.realtimeSinceStartup - pressed < 90f)
                    {
                        covered |= ScreenTransition.Covered;
                        if (ScreenTransition.LoadingVisible)
                        {
                            sawBar = true;
                            barLabel = ScreenTransition.LoadingLabel;
                            barProgressMax = Mathf.Max(barProgressMax, ScreenTransition.LoadingHeroAt);
                            if (barLabel.Contains("%") || barLabel.IndexOf("LOADING", System.StringComparison.OrdinalIgnoreCase) >= 0 || barLabel != ScreenTransition.LoadingWords)
                                bareBar = true;
                        }
                        yield return null;
                    }
                    float took = Time.realtimeSinceStartup - pressed;
                    if (Run.Done) yield break;
                    seconds.Add(took);
                    Run.Metric($"load.pressToPlaySeconds.run{run + 1}", took);
                    Run.Event("load", $"press run {run + 1}: New Game to Level 1 taking input in {took:0.0} s; " +
                                      $"cover {covered}, transition bar {(sawBar ? $"shown (\"{barLabel}\", hero walked to {barProgressMax * 100f:0}%)" : "never needed")}");
                    LoadCheck($"themed transition (run {run + 1})", !bareBar,
                        bareBar ? $"a bare loading bar showed (\"{barLabel}\")" : (sawBar ? $"the hero stepping off with progress, \"{barLabel}\"" : "the level was in before any bar was needed"));
                    Run.Progress(0.3f + 0.15f * (run + 1) / PressRuns);
                    // Let the level breathe, then leave it for the next press (the first run's save stays: New Game resets it again).
                    yield return WaitReal(1.5f);
                }
                if (seconds.Count == 0)
                    yield break;
                seconds.Sort();
                float median = seconds[seconds.Count / 2];
                Run.Metric("load.pressToPlaySeconds", median);
                LoadCheck("press to play", median <= PressBudgetSeconds,
                    $"median {median:0.0} s of {seconds.Count} runs ({string.Join(", ", seconds.ConvertAll(x => x.ToString("0.0")))}) from New Game, pressed after {PressWaitSeconds:0} s on the menu, to Level 1 accepting input; budget {PressBudgetSeconds:0} s");
            }

            /// <summary>
            /// Opens like a player on a phone (no input until the menu is up), then reads what the page and the boot did,
            /// then plays Level 1 (loaded directly: setup shortcut) with the bot for 30 s while counting frames.
            /// </summary>
            public IEnumerator PhoneLoadRun()
            {
                Run.Scene("Loading");
                Run.Event("device", $"{Screen.width}x{Screen.height}, mobile platform {Application.isMobilePlatform}, " +
                                    $"texture variant {(TextureVariantRedirect.UsingMobileVariant ? "ASTC (phones)" : "DXT (desktop)")}");

                Run.Metric("load.viewportShortSide", Mathf.Min(Screen.width, Screen.height));
                if (Mathf.Min(Screen.width, Screen.height) > 600)
                    Run.Event("check", $"phone-load device: NOT A PHONE ({Screen.width}x{Screen.height}); the budget is for 390x844 (run it as phone-load@local:phone)");

                // 1. The menu comes up by itself (the boot opens it); the hook above saw the moment.
                yield return Run.Until(() => PageLoad.MenuAtReal >= 0f || Object.FindAnyObjectByType<MainMenuManager>() != null, 90f, "the main menu to open by itself");
                if (Run.Done) yield break;
                if (PageLoad.MenuAtReal < 0f)
                {
                    PageLoad.MenuAtReal = Time.realtimeSinceStartup;
                    PageLoad.MenuAtPage = PageLoad.PageSeconds();
                }
                yield return ToMainMenu();
                if (Run.Done) yield break;

                float menuPage = PageLoad.MenuAtPage;
                float menuReal = PageLoad.MenuAtReal;
                bool pageClock = menuPage >= 0f;
                float menuAt = pageClock ? menuPage : menuReal;
                float playerStart = pageClock ? menuPage - menuReal : -1f; // page seconds when the player's clock started
                float bootStart = GameDefinitionBoot.StartedAt, bootData = GameDefinitionBoot.DefinitionAt, bootReady = GameDefinitionBoot.ReadyAt;
                string boot = bootReady > 0f
                    ? $"game data {bootData - bootStart:0.0} s, core art and sound {bootReady - bootData:0.0} s, menu {menuReal - bootReady:0.0} s after"
                    : "no boot scene (GameDefinition in the player)";
                Run.Event("load", (pageClock ? $"page open -> player started {playerStart:0.0} s (download, unpack, compile); " : "") +
                                  $"player -> boot {bootStart:0.0} s; {boot}; main menu at {menuAt:0.0} s");
                Run.Metric("load.menuSeconds", menuAt);
                if (pageClock)
                    Run.Metric("load.playerStartSeconds", playerStart);
                if (bootReady > 0f)
                {
                    Run.Metric("load.bootDataSeconds", bootData - bootStart);
                    Run.Metric("load.bootCoreSeconds", bootReady - bootData);
                    if (GameDefinitionBoot.InitAt > 0f)
                    {
                        Run.Metric("load.bootCatalogSeconds", GameDefinitionBoot.InitAt - bootStart);
                        Run.Event("load", $"Addressables init and catalog {GameDefinitionBoot.InitAt - bootStart:0.0} s of the {bootData - bootStart:0.0} s game-data step");
                    }
                }
                LoadCheck("menu in time", menuAt <= PhoneLoadMenuBudget,
                    $"the main menu was up {menuAt:0.0} s after the {(pageClock ? "page opened" : "player started (no page clock here)")}, budget {PhoneLoadMenuBudget:0} s");

                // 2. The loading bars: the page's (the download) and the boot's (the game data and core bundles).
                var info = PageLoadInfo.Read(menuAt > 0f && pageClock ? menuAt : 0f);
                if (info != null && !string.IsNullOrEmpty(info.bar))
                    LoadCheck("page bar", info.barWidth == "100%" && info.barHidden,
                        $"the page's loading bar {info.bar} filled to {(string.IsNullOrEmpty(info.barWidth) ? "?" : info.barWidth)} and {(info.barHidden ? "went away" : "is still up")}");
                else
                    Run.Event("check", "phone-load page bar: not checked (no page bar found: the editor, or a page without one)");
                if (bootStart > 0f)
                    LoadCheck("boot bar", GameDefinitionBoot.ProgressSteps >= 2 && GameDefinitionBoot.Progress >= 0.999f,
                        $"the boot's bar moved {GameDefinitionBoot.ProgressSteps} times to {GameDefinitionBoot.Progress * 100f:0}% with its percentage over {bootReady - bootStart:0.0} s");
                else
                    Run.Event("check", "phone-load boot bar: not checked (no boot scene in this build)");

                // 3. What the page fetched before the menu.
                if (info != null)
                {
                    string sizes = info.sized > 0 ? $"{Megabytes(info.bytes)} in {info.sized} measured" : "sizes hidden by the host";
                    Run.Event("download", $"before the menu: {info.files} files ({sizes}); player {info.playerFiles} files {Megabytes(info.player)}; " +
                                          $"{info.bundles} bundles {Megabytes(info.bundleBytes)} ({info.astc} from WebGL_ASTC); {info.catalogs} catalog files" +
                                          (string.IsNullOrEmpty(info.error) ? "" : "; " + info.error));
                    string groups = BundleFetchLog.Describe(out int requested);
                    if (requested > 0)
                    {
                        Run.Event("download", $"bundle requests before the menu by group ({requested}): {groups}");
                        Run.Metric("load.menuBundleRequests", requested);
                    }
                    Run.Metric("load.menuFiles", info.files);
                    Run.Metric("load.menuBundles", info.bundles);
                    if (info.sized > 0)
                        Run.Metric("load.menuMB", (float)(info.bytes / 1000000.0));
                    // beta v27 fetched 50 bundles before the menu, ~20 of them the hero's and the legacy enemies' sheets (GameDefinition's
                    // prefabs pulled them in). Only meaningful on a build that splits its art out (the editor has no bundles).
                    // The failing check carries the group names, so the finding that comes out of a red run says what the bundles are.
                    if (GameAssets.Managed)
                        LoadCheck("bundles before the menu", info.bundles <= PhoneLoadMenuBundleBudget,
                            $"{info.bundles} bundles ({Megabytes(info.bundleBytes)}{(info.sized > 0 ? "" : ", sizes hidden by the host")}) were fetched before the menu, budget {PhoneLoadMenuBundleBudget} (beta v27: 50); by group ({requested}): {(string.IsNullOrEmpty(groups) ? "none logged" : groups)}");
                }
                if (GameAssets.Managed)
                {
                    Run.Metric("load.menuBundlesHeld", GameAssets.LoadedCount);
                    LoadCheck("hero art waits for a level", !GameAssets.Holds(GameAssets.Hero),
                        $"the hero's whole scope (every state's sheets) and the legacy enemy art are not held at the menu, only the menu hero's own prefab ({GameAssets.Describe()})");
                }
                Run.Progress(0.3f);

                // 4. Frame rate on the menu.
                float menuFps = 0f, menuWorst = 0f;
                yield return SampleFps(5f, null, (a, w) => { menuFps = a; menuWorst = w; });
                Run.Metric("load.menuFps", menuFps);
                LoadCheck("menu fps", menuFps >= PhoneLoadMinFps, $"{menuFps:0} fps on the main menu (worst second {menuWorst:0})");
                if (Run.Done) yield break;

                // 4a. The hero is on the menu: drawn at once from the core Idle sheet, then the real, playable one once his bundles are in.
                float heroAsked = Time.realtimeSinceStartup;
                string heroProblem = MenuHeroProblem();
                while (heroProblem != null && !Run.Done && Time.realtimeSinceStartup - heroAsked < 20f)
                {
                    yield return null;
                    heroProblem = MenuHeroProblem();
                }
                if (Run.Done) yield break;
                Run.Metric("load.menuHeroSeconds", Time.realtimeSinceStartup - heroAsked);
                LoadCheck("hero on the menu", heroProblem == null,
                    heroProblem == null ? $"his sprite is drawn on the menu {Time.realtimeSinceStartup - heroAsked:0.0} s after the buttons" : heroProblem);

                // The first screen is the lava world (art cohesion): the lava backdrop is up, the meadow video and house are off.
                var lava = GameObject.Find(SickOfIt.Core.Progression.MenuLavaBackdrop.Name);
                var meadow = GameObject.Find("MainMenu_Background1");
                LoadCheck("menu is the lava world", lava != null && meadow == null,
                    lava == null ? "the lava backdrop is missing from the menu" : meadow != null ? "the meadow video is still drawn" : "lava backdrop up, no meadow");

                // 4b. The art for the screens after the menu (select cards, Shaft, Yooba, shop) arrives behind it, not before it.
                if (GameAssets.Managed && GameAssets.Knows(GameAssets.LateCore))
                {
                    float lateAsked = Time.realtimeSinceStartup;
                    yield return Run.Until(() => GameAssets.LateReady, 90f, "the late core art to load behind the menu");
                    if (Run.Done) yield break;
                    float lateSeconds = Time.realtimeSinceStartup - menuReal;
                    Run.Metric("load.lateCoreSeconds", lateSeconds);
                    Run.Event("load", $"late core art in {lateSeconds:0.0} s after the menu came up ({GameAssets.Describe()})");
                    LoadCheck("late core behind the menu", GameAssets.LateReady, $"the Shaft, Yooba, shop and select-card art loaded {lateSeconds:0.0} s after the menu, not before it");
                    // Its downloads and texture uploads must not run through the menu scene's own load (the boot's 2 s timer did).
                    if (GameAssets.LateStartedAt > 0f)
                    {
                        float lateStart = GameAssets.LateStartedAt - menuReal;
                        Run.Metric("load.lateStartAfterMenuSeconds", lateStart);
                        LoadCheck("late core starts after the menu", lateStart >= 0f, $"the late core's downloads began {lateStart:0.0} s {(lateStart >= 0f ? "after" : "BEFORE")} the main menu came up");
                    }
                }

                // 4c. Idle on the menu, the hero's and Level 1's bundles are fetched behind it, so Continue only has to load them.
                if (GameAssets.Managed && GameAssets.Knows(GameAssets.LateCore))
                {
                    yield return Run.Until(() => GameAssets.WarmDone, 60f, "the hero and level bundles to be fetched behind the menu");
                    if (Run.Done) yield break;
                    Run.Metric("load.warmSeconds", GameAssets.WarmSeconds);
                    LoadCheck("level bundles warmed behind the menu", GameAssets.WarmDone, $"the hero and Level 1 bundles were fetched in {GameAssets.WarmSeconds:0.0} s while the menu idled");
                }

                // 4c2. By now his bundles have been fetched behind the menu: the stand-in has given way to the playable hero.
                yield return Run.Until(() => Object.FindAnyObjectByType<SickOfIt.Core.Input.PlayerController>() != null, 60f, "the menu's playable hero");
                if (Run.Done) yield break;
                LoadCheck("playable hero on the menu", MenuHeroProblem() == null && Object.FindAnyObjectByType<SickOfIt.Core.Input.PlayerController>() != null,
                    $"the real hero took his place {Time.realtimeSinceStartup - menuReal:0.0} s after the menu came up ({MenuHeroProblem() ?? "on screen"})");
                Run.Metric("load.menuHeroPlayableSeconds", Time.realtimeSinceStartup - menuReal);

                // 4d. A player's Start: after 5 s on the menu, New Game to Level 1 accepting input, three times (median), with the
                // menu's indicator gone and a themed transition (never a bare LOADING bar).
                yield return PhonePressRuns();
                if (Run.Done) yield break;

                // 5. Level 1 (setup shortcut: loaded directly), its bundles loaded on demand, then 30 s of play by the bot.
                float levelAsked = Time.realtimeSinceStartup;
                SceneTransitionManager.Instance.LoadLevel(1);
                yield return Run.Until(() => LevelRunning(1), 60f, "level 1 to start");
                if (Run.Done) yield break;
                float levelLoad = Time.realtimeSinceStartup - levelAsked;
                Run.Metric("load.level1Seconds", levelLoad);
                Run.Event("load", $"level 1 started {levelLoad:0.0} s after it was asked for");
                Run.Progress(0.5f);

                float levelFps = 0f, levelWorst = 0f;
                yield return SampleFps(PhoneLoadPlaySeconds, PlayLevel(1, PhoneLoadPlaySeconds, progressFrom: 0.5f, progressTo: 0.95f),
                    (a, w) => { levelFps = a; levelWorst = w; });
                if (Run.Done) yield break;
                Run.Metric("load.levelFps", levelFps);
                Run.Metric("load.levelFpsWorst", levelWorst);
                LoadCheck("level fps", levelFps >= PhoneLoadMinFps, $"{levelFps:0} fps over Level 1's play (worst second {levelWorst:0}, {lastOutcome})");
                if (levelWorst < 20f)
                    Run.Problem(PlaytestSeverity.Warning, $"phone-load: a second of Level 1 dropped to {levelWorst:0} fps");

                var after = PageLoadInfo.Read(0f);
                if (after != null)
                    Run.Event("download", $"by the end: {after.files} files, {after.bundles} bundles" +
                                          (after.sized > 0 ? $", {Megabytes(after.bytes)} measured" : ""));
                Run.Progress(1f);
                End(phoneLoadFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
