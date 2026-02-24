using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using TMPro;
using MobileControllerFramework;
using SickOfIt.Core.Flow;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Main Menu scene
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
        [Tooltip("Reference to the BundleDownloadManager to disable buttons during bundle downloads")]
        [SerializeField] private BundleDownloadManager bundleDownloadManager;

        [Header("Transition Animation")]
        [Tooltip("The PlayableDirector to deactivate when Continue/New Game is pressed (e.g. idle menu animation)")]
        [SerializeField] private PlayableDirector idleDirector;
        [Tooltip("The PlayableDirector to play as a transition before loading the next scene")]
        [SerializeField] private PlayableDirector transitionDirector;

        [Header("Delayed Hide")]
        [Tooltip("How long to wait before hiding the objects (in seconds)")]
        [SerializeField] private float hideDelay = 1.5f;
        [Tooltip("GameObjects to hide after Continue/New Game is pressed")]
        [SerializeField] private List<GameObject> objectsToHideOnStart;

        /// <summary>
        /// Prevents multiple button presses from firing duplicate scene load requests.
        /// </summary>
        private bool isTransitioning = false;

        private void Awake()
        {
        }

        private void Start()
        {
            // Track game start when main menu loads
            SickOfIt.Core.AnalyticsManager.Instance.TrackGameStart();

            // Initialize game progress for first-time players
            GameProgressManager.Instance.InitializeNewPlayer();

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
            if (continueButton != null)
            {
                int currentLevel = GameProgressManager.Instance.GetCurrentLevel();
                int totalLevels = GameProgressManager.Instance.GetTotalLevels();

                // Hide continue button if player has beaten the game
                if (currentLevel > totalLevels)
                {
                    continueButton.gameObject.SetActive(false);
                    Debug.Log("[MainMenu] Player has beaten all levels - Continue button hidden");
                    return;
                }

                // Show button and update text
                continueButton.gameObject.SetActive(true);

                // Update button text if available
                if (continueButtonText != null)
                {
                    continueButtonText.text = $"Continue (Level {currentLevel})";
                }
            }
        }

        /// <summary>
        /// Update the roguelite UI elements (cumulative health, points).
        /// </summary>
        private void UpdateRogueliteUI()
        {
            int totalLevels = GameProgressManager.Instance.GetTotalLevels();
            int maxHealth = RogueliteManager.Instance.GetMaxHealth();
            int cumulativeHealth = RogueliteManager.Instance.GetCumulativeHealth(totalLevels);

            if (cumulativeHealthText != null)
            {
                cumulativeHealthText.text = $"Yooba's Health: {cumulativeHealth} / {maxHealth}";
            }

            if (pointsText != null)
            {
                pointsText.text = $"Diamonds: {RogueliteManager.Instance.GetPoints()}";
            }
        }

        private void OnContinueClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            StartCoroutine(DelayedHideObjects(hideDelay));
            StartCoroutine(PlayTransitionThenExecute(() =>
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

            Debug.Log("[MOBILE DEBUG] New Game button clicked!");

            // Reset all progress and start from level 1
            RogueliteManager.Instance.ResetAllRogueliteData();
            GameProgressManager.Instance.InitializeNewPlayer();

            // Mark Level1 as visited to prevent auto-play loop
            PlayerPrefs.SetInt("Level_1_Visited", 1);

            // Play Video1 first, then load Level1
            PlayerPrefs.SetInt("ReturnToLevel", 1);
            PlayerPrefs.Save();

            Debug.Log("[MOBILE DEBUG] Attempting to load Video1...");
            Debug.Log($"[MOBILE DEBUG] SceneLoadingManager present: {sceneLoadingManager != null}");

            StartCoroutine(DelayedHideObjects(hideDelay));
            StartCoroutine(PlayTransitionThenExecute(() =>
            {
                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadVideo(1);
                else
                    SceneTransitionManager.Instance.LoadVideo(1);
            }));
        }

        /// <summary>
        /// Waits for the specified delay then deactivates all GameObjects in the objectsToHideOnStart list.
        /// </summary>
        private IEnumerator DelayedHideObjects(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (objectsToHideOnStart != null)
            {
                foreach (var obj in objectsToHideOnStart)
                {
                    if (obj != null)
                        obj.SetActive(false);
                }
                Debug.Log($"[MainMenu] Hidden {objectsToHideOnStart.Count} objects after {delay}s delay");
            }
        }

        /// <summary>
        /// Deactivates the idle SpriteFramePlayer, plays the transition via PlayableDirector,
        /// waits for it to finish, then invokes the provided action.
        /// Falls back to invoking the action immediately if the transition player is not assigned.
        /// </summary>
        private IEnumerator PlayTransitionThenExecute(Action onComplete)
        {
            // Deactivate the idle director
            if (idleDirector != null)
            {
                idleDirector.gameObject.SetActive(false);
                Debug.Log("[MainMenu] Deactivated idle PlayableDirector");
            }

            // If no transition director is assigned, just proceed immediately
            if (transitionDirector == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            // Make sure the transition director's GameObject is active
            transitionDirector.gameObject.SetActive(true);

            // Subscribe to director completion and play
            bool finished = false;
            void OnDirectorStopped(PlayableDirector d)
            {
                finished = true;
            }

            transitionDirector.stopped += OnDirectorStopped;
            transitionDirector.time = 0;
            transitionDirector.Play();
            Debug.Log("[MainMenu] Playing transition via PlayableDirector");

            // Wait for playback to complete
            while (!finished)
            {
                yield return null;
            }

            transitionDirector.stopped -= OnDirectorStopped;
            Debug.Log("[MainMenu] Transition finished, proceeding");

            onComplete?.Invoke();
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

        /// <summary>
        /// Setup bundle download manager to disable buttons during downloads
        /// </summary>
        private void SetupBundleDownloadManager()
        {
            if (bundleDownloadManager == null)
            {
                // Try to find it in the scene
                bundleDownloadManager = FindObjectOfType<BundleDownloadManager>();
            }

            if (bundleDownloadManager != null)
            {
                // Check if downloading
                if (bundleDownloadManager.IsDownloading)
                {
                    DisableAllButtons();
                }

                // Subscribe to download complete event
                bundleDownloadManager.OnDownloadComplete.AddListener(OnBundleDownloadComplete);
            }
        }

        /// <summary>
        /// Called when bundle downloads complete
        /// </summary>
        private void OnBundleDownloadComplete()
        {
            EnableAllButtons();
        }

        /// <summary>
        /// Disable all menu buttons
        /// </summary>
        private void DisableAllButtons()
        {
            if (continueButton != null) continueButton.Disable();
            if (newGameButton != null) newGameButton.Disable();
            if (levelSelectButton != null) levelSelectButton.Disable();
            if (videosButton != null) videosButton.Disable();
            if (quitButton != null) quitButton.Disable();
            if (upgradeShopButton != null) upgradeShopButton.Disable();
        }

        /// <summary>
        /// Enable all menu buttons (respecting continue button visibility)
        /// </summary>
        private void EnableAllButtons()
        {
            // Enable continue button only if it should be visible
            if (continueButton != null && continueButton.gameObject.activeSelf)
            {
                continueButton.Enable();
            }
            
            if (newGameButton != null) newGameButton.Enable();
            if (levelSelectButton != null) levelSelectButton.Enable();
            if (videosButton != null) videosButton.Enable();
            if (quitButton != null) quitButton.Enable();
            if (upgradeShopButton != null) upgradeShopButton.Enable();
        }
    }
}
