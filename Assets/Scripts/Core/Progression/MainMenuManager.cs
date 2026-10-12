using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MobileControllerFramework;
using SickOfIt.Core.Flow;
using SickOfIt.Data;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Main Menu scene.
    /// No PlayableDirectors — transitions use simple coroutine-based fades.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private MobileControllerFramework.Button continueButton;
        [SerializeField] private MobileControllerFramework.Button newGameButton;
        [SerializeField] private MobileControllerFramework.Button levelSelectButton;
        [SerializeField] private MobileControllerFramework.Button videosButton;
        [SerializeField] private MobileControllerFramework.Button quitButton;
        [SerializeField] private MobileControllerFramework.Button upgradeShopButton;

        [Header("Optional UI Elements")]
        [SerializeField] private TextMeshProUGUI continueButtonText;

        [Header("Roguelite UI")]
        [Tooltip("Displays Yooba's cumulative health on the main menu.")]
        [SerializeField] private TextMeshProUGUI cumulativeHealthText;
        [Tooltip("Displays the player's current points.")]
        [SerializeField] private TextMeshProUGUI pointsText;

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

        private void Start()
        {
            SickOfIt.Core.AnalyticsManager.Instance.TrackGameStart();
            GameProgressManager.Instance.InitializeNewPlayer();

            if (explodeRoot != null)
                explodeRoot.SetActive(false);

            SetupButtons();
            UpdateContinueButton();
            UpdateRogueliteUI();
            SetupBundleDownloadManager();
        }

        private void SetupButtons()
        {
            if (continueButton != null)
                continueButton.TapEndEvent.AddListener(OnContinueClicked);
            if (newGameButton != null)
                newGameButton.TapEndEvent.AddListener(OnNewGameClicked);
            if (levelSelectButton != null)
                levelSelectButton.TapEndEvent.AddListener(OnLevelSelectClicked);
            if (videosButton != null)
                videosButton.TapEndEvent.AddListener(OnVideosClicked);
            if (quitButton != null)
                quitButton.TapEndEvent.AddListener(OnQuitClicked);
            if (upgradeShopButton != null)
                upgradeShopButton.TapEndEvent.AddListener(OnUpgradeShopClicked);
        }

        private void UpdateContinueButton()
        {
            if (continueButton == null) return;

            int currentLevel = GameProgressManager.Instance.GetCurrentLevel();
            int totalLevels = GameProgressManager.Instance.GetTotalLevels();

            if (currentLevel > totalLevels)
            {
                continueButton.gameObject.SetActive(false);
                return;
            }

            continueButton.gameObject.SetActive(true);
            if (continueButtonText != null)
                continueButtonText.text = $"Continue (Level {currentLevel})";
        }

        private void UpdateRogueliteUI()
        {
            int totalLevels = GameProgressManager.Instance.GetTotalLevels();
            int maxHealth = RogueliteManager.Instance.GetMaxHealth();
            int cumulativeHealth = RogueliteManager.Instance.GetCumulativeHealth(totalLevels);

            if (cumulativeHealthText != null)
                cumulativeHealthText.text = $"Yooba's Health: {cumulativeHealth} / {maxHealth}";
            if (pointsText != null)
                pointsText.text = $"Diamonds: {RogueliteManager.Instance.GetPoints()}";
        }

        private void OnContinueClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            StartCoroutine(TransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.ContinueGame();
                else
                    SceneTransitionManager.Instance.ContinueGame();
            }));
        }

        private void OnNewGameClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;

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

        private IEnumerator PlayExplodeUntilDone(SpriteFramePlayer player)
        {
            float waited = 0f;
            while (player != null && !player.IsReady && waited < 8f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

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
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadLevelSelection();
        }

        private void OnVideosClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadVideoSelection();
        }

        private void OnQuitClicked()
        {
            SceneTransitionManager.Instance.QuitGame();
        }

        private void OnUpgradeShopClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
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
            if (continueButton != null) continueButton.Disable();
            if (newGameButton != null) newGameButton.Disable();
            if (levelSelectButton != null) levelSelectButton.Disable();
            if (videosButton != null) videosButton.Disable();
            if (quitButton != null) quitButton.Disable();
            if (upgradeShopButton != null) upgradeShopButton.Disable();
        }

        private void EnableAllButtons()
        {
            if (continueButton != null && continueButton.gameObject.activeSelf) continueButton.Enable();
            if (newGameButton != null) newGameButton.Enable();
            if (levelSelectButton != null) levelSelectButton.Enable();
            if (videosButton != null) videosButton.Enable();
            if (quitButton != null) quitButton.Enable();
            if (upgradeShopButton != null) upgradeShopButton.Enable();
        }
    }
}
