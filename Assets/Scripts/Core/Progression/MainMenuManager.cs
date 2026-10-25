using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Main Menu scene. The title, buttons, stickers and stats are built at runtime from
    /// mainMenuConfig.layout/labels and ui/theme.json (see <see cref="MenuCanvas"/>).
    /// No PlayableDirectors — transitions use simple coroutine-based fades.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Scene Loading")]
        [SerializeField] private SceneLoadingManager sceneLoadingManager;

        [Header("Bundle Download Manager")]
        [SerializeField] private BundleDownloadManager bundleDownloadManager;

        [Header("Transition")]
        [Tooltip("How long to wait before hiding the objects (in seconds)")]
        [SerializeField] private float hideDelay = 1.5f;
        [Tooltip("GameObjects to hide after Continue/New Game is pressed")]
        [SerializeField] private List<GameObject> objectsToHideOnStart;
        [Tooltip("Optional: idle animation objects to deactivate on transition")]
        [SerializeField] private List<GameObject> idleObjects;
        [Tooltip("Optional: transition animation objects to activate on transition")]
        [SerializeField] private List<GameObject> transitionObjects;
        [Tooltip("Delay before executing scene load after transition starts")]
        [SerializeField] private float transitionDelay = 1f;

        [Header("Explode Transition")]
        [Tooltip("House-explosion video. Played to the end before the next scene loads.")]
        [SerializeField] private GameObject explodeRoot;
        [SerializeField] private SpriteFramesData explodeFrames;
        [SerializeField] private AudioClip explodeAudio;

        private bool isTransitioning = false;
        private MenuCanvas menu;

        private void Start()
        {
            // Analytics: the game is playable once the main menu is up.
            AnalyticsManager.Ready();
            AnalyticsManager.Screen("menu");
            AnalyticsManager.SetContext(new System.Collections.Generic.Dictionary<string, object>
                { { "level", null }, { "level_name", null }, { "floor", null }, { "phase", null }, { "boss", null }, { "video", null } });
            GameProgressManager.Instance.InitializeNewPlayer();

            if (explodeRoot != null)
                explodeRoot.SetActive(false);

            ApplyMenuConfig();
            BuildMenu();
            SetupBundleDownloadManager();
        }

        private void ApplyMenuConfig()
        {
            var config = GameDefinition.Instance != null ? GameDefinition.Instance.mainMenuConfig : null;
            if (config == null)
                return;

            hideDelay = config.hideDelay;
            transitionDelay = config.transitionDelay;
            PlacedSpriteBuilder.Bind(config.placedSprites);

            if (!config.placeCharacter)
                return;

            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null || transforms[i].name != "Character")
                    continue;
                var position = transforms[i].localPosition;
                position.x = config.characterPosition.x;
                position.y = config.characterPosition.y;
                transforms[i].localPosition = position;
                break;
            }
        }

        private void BuildMenu()
        {
            var gd = GameDefinition.Instance;
            var config = gd != null ? gd.mainMenuConfig : new MainMenuConfig();
            var labels = config.labels ?? new MainMenuLabels();
            menu = MenuCanvas.Create("MainMenuUI", gd != null ? gd.menuTheme : null, config.layout, gameObject);
            var list = menu.AddList(config.layout.list, menu.Theme.button.size);

            // Continue: the current run, where it left off. New Run: a fresh run from the highest Waystone reached
            // (Docs/Story.md, "Waystones and runs"). New Game: everything from the Meadow again.
            int currentLevel = RunFlow.ContinueLevel();
            int totalLevels = GameProgressManager.Instance.GetTotalLevels();
            if (config.showContinueButton && currentLevel <= totalLevels)
                Add(list, string.Format(labels.continueGame, currentLevel), OnContinueClicked);
            int waystone = WaystoneProgress.Highest;
            if (waystone > 0 && RunFlow.LevelFrom(waystone) > 0)
                Add(list, string.Format(System.Globalization.CultureInfo.InvariantCulture, labels.newRun,
                    waystone.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), OnNewRunClicked);
            if (config.showNewGameButton)
                Add(list, labels.newGame, OnNewGameClicked);
            if (config.showLevelSelectButton)
                Add(list, labels.levelSelect, OnLevelSelectClicked);
            if (config.showVideosButton)
                Add(list, labels.videos, OnVideosClicked);
            if (config.showUpgradeShopButton)
                Add(list, labels.shop, OnUpgradeShopClicked);
            var codex = gd != null ? gd.codex : null;
            if (codex != null && codex.enabled && codex.showOnMainMenu)
                Add(list, codex.buttonLabel, () =>
                {
                    Picked("codex");
                    SickOfIt.Gameplay.CodexPanel.Show(menu, () => AnalyticsManager.Screen("menu"));
                    AnalyticsManager.Screen("codex");
                });
            if (config.showSettingsButton)
                Add(list, labels.settings, () =>
                {
                    Picked("settings");
                    SettingsPanel.Show(menu, () => AnalyticsManager.Screen("menu"));
                    AnalyticsManager.Screen("settings");
                });
            // Right after a rage quit from a death, the quit button dares you to do it again (even in a browser,
            // where it can only ask you to close the tab). Otherwise quitting is hidden in browsers.
            bool rageQuitAgain = PlayerPrefs.GetInt(DeathRuntime.RageQuitKey, 0) == 1;
            if (rageQuitAgain)
            {
                PlayerPrefs.DeleteKey(DeathRuntime.RageQuitKey);
                PlayerPrefs.Save();
            }
            if (config.showQuitButton && (rageQuitAgain || Application.platform != RuntimePlatform.WebGLPlayer))
                Add(list, rageQuitAgain ? labels.rageQuitAgain : labels.quit, OnQuitClicked);

            if (config.showCumulativeHealth)
            {
                int maxHealth = RogueliteManager.Instance.GetMaxHealth();
                int health = RogueliteManager.Instance.GetCumulativeHealth(totalLevels);
                menu.AddChip(string.Format(menu.Theme.healthFormat, health, maxHealth));
            }
            if (config.showPointsDisplay)
                menu.AddChip(string.Format(menu.Theme.pointsFormat, RogueliteManager.Instance.GetPoints()));
        }

        /// <summary>Analytics: which main menu button was pressed.</summary>
        private static void Picked(string option) => AnalyticsManager.Choice("main_menu", option);

        private void Add(RectTransform list, string text, Action onClick)
        {
            menu.AddButton(list, text, menu.Theme.button, onClick);
        }

        private void OnContinueClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            int level = RunFlow.ContinueLevel();
            Picked("continue");
            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(level);
                else
                    SceneTransitionManager.Instance.LoadLevel(level);
            }));
        }

        /// <summary>A new run from the highest Waystone: the current run's Grudges go; diamonds and Yooba's goods stay.</summary>
        private void OnNewRunClicked()
        {
            if (isTransitioning) return;
            int level = RunFlow.NewRun();
            if (level <= 0) return;
            isTransitioning = true;
            Picked("new_run");

            if (bundleDownloadManager != null)
                bundleDownloadManager.StartDownloadForLevel(level);
            else if (BundleDownloadManager.Instance != null)
                BundleDownloadManager.Instance.StartDownloadForLevel(level);

            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(level);
                else
                    SceneTransitionManager.Instance.LoadLevel(level);
            }));
        }

        private void OnNewGameClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            Picked("new_game");
            RunFlow.Abandon();
            RogueliteManager.Instance.ResetAllRogueliteData();
            GameProgressManager.Instance.InitializeNewPlayer();
            PlayerPrefs.SetInt("Level_1_Visited", 1);
            PlayerPrefs.SetInt("ReturnToLevel", 1);
            PlayerPrefs.Save();

            if (bundleDownloadManager != null)
                bundleDownloadManager.StartDownloadForLevel(1);
            else if (BundleDownloadManager.Instance != null)
                BundleDownloadManager.Instance.StartDownloadForLevel(1);

            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadVideo(1);
                else
                    SceneTransitionManager.Instance.LoadVideo(1);
            }));
        }

        /// <summary>
        /// Play the house explosion through to the end, then execute the scene load.
        /// </summary>
        private IEnumerator TransitionThenExecute(Action onComplete)
        {
            if (menu != null)
                menu.PlayExit(0.35f);

            if (explodeRoot != null)
                explodeRoot.SetActive(true);

            SpriteFramePlayer player = null;
            if (explodeRoot != null && explodeFrames != null)
            {
                player = explodeRoot.GetComponent<SpriteFramePlayer>();
                if (player == null)
                    player = explodeRoot.AddComponent<SpriteFramePlayer>();

                var renderer = explodeRoot.GetComponent<SpriteRenderer>();
                if (renderer != null)
                    SpriteLayers.Apply(renderer, SpriteLayer.Foreground2);

                player.UseGameClock(false);
                player.Initialize(explodeFrames, () => player.Play());
                player.SetLoop(false);
            }

            // The hero and the house stay on screen until the explosion can play: its frames can take seconds to
            // load on WebGL, and hiding them first left an empty meadow (and no hero) while the level loaded.
            if (player != null)
                yield return WaitForExplodeReady(player);

            if (idleObjects != null)
            {
                foreach (var obj in idleObjects)
                    if (obj != null) obj.SetActive(false);
            }

            if (transitionObjects != null)
            {
                foreach (var obj in transitionObjects)
                    if (obj != null) obj.SetActive(true);
            }

            if (objectsToHideOnStart != null)
            {
                foreach (var obj in objectsToHideOnStart)
                    if (obj != null) obj.SetActive(false);
            }

            var source = explodeRoot != null ? explodeRoot.GetComponent<AudioSource>() : null;
            if (source != null && explodeAudio != null)
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.loop = false;
                source.clip = explodeAudio;
                source.volume = 1f;
                if (explodeAudio.loadState != AudioDataLoadState.Loaded)
                    explodeAudio.LoadAudioData();
                source.Play();
            }

            if (player != null)
                yield return PlayExplodeUntilDone(player);
            else if (transitionDelay > 0)
                yield return new WaitForSeconds(transitionDelay);

            onComplete?.Invoke();
        }

        private IEnumerator WaitForExplodeReady(SpriteFramePlayer player)
        {
            float waited = 0f;
            while (player != null && !player.IsReady && waited < 8f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator PlayExplodeUntilDone(SpriteFramePlayer player)
        {
            yield return WaitForExplodeReady(player);

            float duration = player != null ? player.Duration : 0f;
            if (explodeAudio != null)
                duration = Mathf.Max(duration, explodeAudio.length);
            if (duration <= 0f)
                duration = 4f;

            float played = 0f;
            while (played < duration + 0.15f)
            {
                played += Time.deltaTime;
                bool framesDone = player != null && player.IsReady && player.TotalFrames > 0 && player.Progress >= 0.99f;
                bool audioDone = explodeAudio == null || played >= explodeAudio.length - 0.05f;
                if (framesDone && audioDone)
                    break;
                yield return null;
            }
        }

        private void OnLevelSelectClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Picked("level_select");
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadLevelSelection();
        }

        private void OnVideosClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Picked("videos");
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadVideoSelection();
        }

        private void OnQuitClicked()
        {
            Picked("quit");
            SceneTransitionManager.Instance.QuitGame();
            if (Application.platform == RuntimePlatform.WebGLPlayer && menu != null && !quitHintShown)
            {
                // Browsers usually refuse to let a page close its own tab.
                quitHintShown = true;
                var labels = GameDefinition.Instance != null ? GameDefinition.Instance.mainMenuConfig.labels : new MainMenuLabels();
                menu.AddText(menu.Root, "QuitHint", labels.browserQuitHint, menu.Theme.subtitle, new Vector2(0.5f, 0.08f), Vector2.zero,
                    new Vector2(1500f, 70f), out _, 34f, null, menu.Theme.entrance);
            }
        }

        private bool quitHintShown;

        private void OnUpgradeShopClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Picked("shop");
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadUpgradeShop();
        }

        private void SetupBundleDownloadManager()
        {
            if (bundleDownloadManager == null)
                bundleDownloadManager = FindObjectOfType<BundleDownloadManager>();

            if (bundleDownloadManager != null)
            {
                if (bundleDownloadManager.IsDownloading)
                    DisableAllButtons();
                bundleDownloadManager.OnDownloadComplete.AddListener(OnBundleDownloadComplete);
            }
        }

        private void OnBundleDownloadComplete() => EnableAllButtons();

        private void DisableAllButtons()
        {
            if (menu != null)
                menu.SetInteractable(false);
        }

        private void EnableAllButtons()
        {
            if (menu != null && !isTransitioning)
                menu.SetInteractable(true);
        }
    }
}
