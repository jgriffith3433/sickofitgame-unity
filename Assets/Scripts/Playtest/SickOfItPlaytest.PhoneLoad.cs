using System.Collections;
using System.Runtime.InteropServices;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.SceneManagement;
using SickOfIt.Core.Addressables;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;
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
        const float PhoneLoadSeconds = 150f;
        const float PhoneLoadMenuBudget = 20f;
        const float PhoneLoadMinFps = 30f;
        const int PhoneLoadMenuBundleBudget = 40;
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

            /// <summary>
            /// Opens like a player on a phone (no input until the menu is up), then reads what the page and the boot did,
            /// then plays Level 1 (loaded directly: setup shortcut) with the bot for 30 s while counting frames.
            /// </summary>
            public IEnumerator PhoneLoadRun()
            {
                Run.Scene("Loading");
                Run.Event("device", $"{Screen.width}x{Screen.height}, mobile platform {Application.isMobilePlatform}, " +
                                    $"texture variant {(TextureVariantRedirect.UsingMobileVariant ? "ASTC (phones)" : "DXT (desktop)")}");

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
                    Run.Metric("load.menuFiles", info.files);
                    Run.Metric("load.menuBundles", info.bundles);
                    if (info.sized > 0)
                        Run.Metric("load.menuMB", (float)(info.bytes / 1000000.0));
                    // beta v27 fetched 50 bundles before the menu, ~20 of them the hero's and the legacy enemies' sheets (GameDefinition's
                    // prefabs pulled them in). Only meaningful on a build that splits its art out (the editor has no bundles).
                    if (GameAssets.Managed)
                        LoadCheck("bundles before the menu", info.bundles <= PhoneLoadMenuBundleBudget,
                            $"{info.bundles} bundles ({Megabytes(info.bundleBytes)}{(info.sized > 0 ? "" : ", sizes hidden by the host")}) were fetched before the menu, budget {PhoneLoadMenuBundleBudget} (beta v27: 50)");
                }
                if (GameAssets.Managed)
                {
                    Run.Metric("load.menuBundlesHeld", GameAssets.LoadedCount);
                    LoadCheck("hero art waits for a level", !GameAssets.Holds(GameAssets.Hero),
                        $"the hero's sheets and the legacy enemy art are not held at the menu ({GameAssets.Describe()})");
                }
                Run.Progress(0.3f);

                // 4. Frame rate on the menu.
                float menuFps = 0f, menuWorst = 0f;
                yield return SampleFps(5f, null, (a, w) => { menuFps = a; menuWorst = w; });
                Run.Metric("load.menuFps", menuFps);
                LoadCheck("menu fps", menuFps >= PhoneLoadMinFps, $"{menuFps:0} fps on the main menu (worst second {menuWorst:0})");
                if (Run.Done) yield break;

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
                }

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
