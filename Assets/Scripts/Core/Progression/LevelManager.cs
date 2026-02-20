using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core.Addressables;
using SickOfIt.Core.Flow;
using SickOfIt.Combat.Enemies.Spawning;
using SickOfIt.VideoFrameExtractor.Runtime;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Base manager for individual level scenes.
    /// Handles asset loading, spawner lifecycle, scene transitions, and multi-stage sequences.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Level Settings")]
        [SerializeField] private int levelNumber = 1;

        [Header("Death Scene")]
        [SerializeField] private string deathSceneName = "Death1";

        [Header("Asset Loading")]
        [Tooltip("Background sprite players that must finish loading before gameplay begins")]
        [SerializeField] private List<MonoBehaviour> backgroundPlayerBehaviours = new List<MonoBehaviour>();
        [Tooltip("Activated once all assets are loaded (start deactivated)")]
        [SerializeField] private List<GameObject> objectsToActivateAfterLoading = new List<GameObject>();
        [Tooltip("Deactivated once all assets are loaded (e.g. loading indicators)")]
        [SerializeField] private List<GameObject> objectsToDeactivateAfterLoading = new List<GameObject>();

        [Header("Scene Transition UI")]
        [Tooltip("Activated when transitioning to another scene (e.g. 'Loading...' overlay)")]
        [SerializeField] private List<GameObject> objectsToActivateOnSceneTransition = new List<GameObject>();
        [Tooltip("Deactivated when transitioning (e.g. player controls, game HUD)")]
        [SerializeField] private List<GameObject> objectsToDeactivateOnSceneTransition = new List<GameObject>();

        [Header("Spawner Control")]
        [Tooltip("SpriteFramePlayer whose completion kills all spawners")]
        [SerializeField] private MonoBehaviour spawnerKillTriggerPlayer;
        [SerializeField] private bool autoKillSpawnersOnComplete;

        [Header("Two-Stage Sequence")]
        [SerializeField] private bool enableTwoStageSequence;
        [SerializeField] private MonoBehaviour firstSequencePlayer;
        [SerializeField] private MonoBehaviour secondSequencePlayer;
        [SerializeField] private List<GameObject> objectsToActivateAfterFirstSequence = new List<GameObject>();
        [SerializeField] private List<GameObject> objectsToDeactivateAfterFirstSequence = new List<GameObject>();
        [Tooltip("Optional button to skip the first sequence")]
        [SerializeField] private MobileControllerFramework.Button skipFirstSequenceButton;

        [Header("Debug")]
        [SerializeField] private bool isLoading;
        [SerializeField] private bool allAssetsLoaded;
        private bool levelCompleted;

        #endregion

        #region Private State

        private readonly List<IFrameSequencePlayer> backgroundPlayers = new List<IFrameSequencePlayer>();
        private IFrameSequencePlayer triggerPlayer;
        private IFrameSequencePlayer firstPlayer;
        private IFrameSequencePlayer secondPlayer;
        private List<EnemySpawner> levelSpawners = new List<EnemySpawner>();

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Resolve MonoBehaviour references to interfaces
            foreach (var behaviour in backgroundPlayerBehaviours)
            {
                if (behaviour is IFrameSequencePlayer player)
                    backgroundPlayers.Add(player);
            }

            if (spawnerKillTriggerPlayer is IFrameSequencePlayer trigger)
            {
                triggerPlayer = trigger;
                triggerPlayer.PlaybackCompleted += OnSpawnerKillTriggerComplete;
            }

            if (enableTwoStageSequence)
            {
                if (firstSequencePlayer is IFrameSequencePlayer first)
                {
                    firstPlayer = first;
                    firstPlayer.PlaybackCompleted += OnFirstSequenceComplete;
                }
                if (secondSequencePlayer is IFrameSequencePlayer second)
                    secondPlayer = second;

                // Wire up skip button if provided
                if (skipFirstSequenceButton != null)
                {
                    skipFirstSequenceButton.TapEndEvent.AddListener(SkipFirstSequence);
                }
            }

            if (autoKillSpawnersOnComplete)
                levelSpawners.AddRange(FindObjectsOfType<EnemySpawner>(true));

            // Begin loading or activate immediately
            if (backgroundPlayers.Count > 0)
            {
                isLoading = true;
                StartCoroutine(WaitForAssetsToLoad());
            }
            else
            {
                ActivateGameObjects();
            }
        }

        private void OnDestroy()
        {
            if (triggerPlayer != null)
                triggerPlayer.PlaybackCompleted -= OnSpawnerKillTriggerComplete;
            if (firstPlayer != null)
                firstPlayer.PlaybackCompleted -= OnFirstSequenceComplete;
            if (skipFirstSequenceButton != null)
                skipFirstSequenceButton.TapEndEvent.RemoveListener(SkipFirstSequence);
        }

        #endregion

        #region Asset Loading

        private IEnumerator WaitForAssetsToLoad()
        {
            while (!AreAllAssetsLoaded())
                yield return null;

            allAssetsLoaded = true;
            isLoading = false;

            SetGameObjectsActive(objectsToDeactivateAfterLoading, false);
            ActivateGameObjects();
        }

        private bool AreAllAssetsLoaded()
        {
            foreach (var player in backgroundPlayers)
            {
                if (player != null && !player.IsLoaded)
                    return false;
            }
            return true;
        }

        private void ActivateGameObjects()
        {
            SetGameObjectsActive(objectsToActivateAfterLoading, true);
            AnalyticsManager.Instance.TrackLevelStart(levelNumber);
        }

        #endregion

        #region Level Completion

        public void CompleteLevel()
        {
            if (levelCompleted) return;
            levelCompleted = true;
            MarkLevelComplete();
            Invoke(nameof(ProceedToNextLevel), 2f);
        }

        public void CompleteLevelAndReturnToMainMenu()
        {
            if (levelCompleted) return;
            levelCompleted = true;
            MarkLevelComplete();
            Invoke(nameof(ReturnToMainMenu), 2f);
        }

        private void MarkLevelComplete()
        {
            GameProgressManager.Instance.SetLevelCompleted(levelNumber);
            AnalyticsManager.Instance.TrackLevelComplete(levelNumber);
            GameProgressManager.Instance.UnlockVideo(levelNumber + 1);
        }

        private void ProceedToNextLevel()
        {
            int nextLevel = levelNumber + 1;
            ShowSceneTransitionUI();

            if (GameProgressManager.Instance.IsVideoUnlocked(nextLevel))
            {
                PlayerPrefs.SetInt("ReturnToLevel", nextLevel);
                PlayerPrefs.Save();
                SceneTransitionManager.Instance.LoadVideo(nextLevel);
            }
            else
            {
                SceneTransitionManager.Instance.LoadLevelSelection();
            }
        }

        #endregion

        #region Navigation

        public void ReturnToLevelSelect()
        {
            ShowSceneTransitionUI();
            SceneTransitionManager.Instance.LoadLevelSelection();
        }

        public void ReturnToMainMenu()
        {
            ShowSceneTransitionUI();
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        public void RestartLevel()
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            PlayerPrefs.SetString("DeathSceneToReload", currentScene.name);
            PlayerPrefs.Save();

            ShowSceneTransitionUI();

            if (!string.IsNullOrEmpty(deathSceneName))
            {
                SceneTransitionManager.Instance.LoadDeathScene(deathSceneName);
            }
            else
            {
                Debug.LogWarning("[LevelManager] No death scene configured, loading level directly");
                SceneTransitionManager.Instance.LoadLevel(levelNumber);
            }
        }

        public void ShowSceneTransitionUI()
        {
            SetGameObjectsActive(objectsToActivateOnSceneTransition, true);
            SetGameObjectsActive(objectsToDeactivateOnSceneTransition, false);
        }

        #endregion

        #region Spawner & Sequence Callbacks

        private void OnSpawnerKillTriggerComplete()
        {
            if (!autoKillSpawnersOnComplete)
                return;

            foreach (var spawner in levelSpawners)
            {
                if (spawner != null)
                    spawner.KillSpawner();
            }
        }

        private void OnFirstSequenceComplete()
        {
            if (!enableTwoStageSequence)
                return;

            // Disable first player's SpriteRenderer before enabling the second one
            firstPlayer?.SetSpriteRendererEnabled(false);

            SetGameObjectsActive(objectsToDeactivateAfterFirstSequence, false);
            SetGameObjectsActive(objectsToActivateAfterFirstSequence, true);

            firstPlayer?.SetSpriteRendererEnabled(true);

            if (secondPlayer != null)
            {
                if (secondPlayer.IsLoaded)
                {
                    secondPlayer.Play();
                }
                else
                {
                    Debug.LogWarning("[LevelManager] Second sequence player is not loaded yet — waiting for it...");
                    StartCoroutine(WaitForSecondPlayerAndPlay());
                }
            }

            // Hide skip button after first sequence completes
            if (skipFirstSequenceButton != null)
                skipFirstSequenceButton.gameObject.SetActive(false);
        }

        private IEnumerator WaitForSecondPlayerAndPlay()
        {
            while (secondPlayer != null && !secondPlayer.IsLoaded)
                yield return null;

            if (secondPlayer != null)
            {
                Debug.Log("[LevelManager] Second sequence player loaded — starting playback.");
                secondPlayer.Play();
            }
        }

        /// <summary>
        /// Skip the first sequence and immediately trigger the second sequence.
        /// Called by the skip button.
        /// </summary>
        public void SkipFirstSequence()
        {
            if (!enableTwoStageSequence || firstPlayer == null)
                return;

            // Skip to end will trigger OnFirstSequenceComplete via the PlaybackCompleted event
            firstPlayer.SkipToEnd();
        }

        #endregion

        #region Helpers

        private static void SetGameObjectsActive(List<GameObject> objects, bool active)
        {
            if (objects == null) return;
            foreach (var obj in objects)
            {
                if (obj != null)
                    obj.SetActive(active);
            }
        }

        #endregion
    }
}
