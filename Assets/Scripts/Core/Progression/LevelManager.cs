using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;
using SickOfIt.Core.Flow;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Base manager for individual level scenes.
    /// Handles scene transitions and multi-stage sequences.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Level Settings")]
        [SerializeField] private int levelNumber = 1;

        [Header("Death Scene")]
        [SerializeField] private string deathSceneName = "Death1";

        [Header("Backgrounds")]
        [Tooltip("Background PlayableDirectors")]
        [SerializeField] private List<PlayableDirector> backgroundDirectors = new List<PlayableDirector>();

        [Header("Scene Transition UI")]
        [Tooltip("Activated when transitioning to another scene (e.g. 'Loading...' overlay)")]
        [SerializeField] private List<GameObject> objectsToActivateOnSceneTransition = new List<GameObject>();
        [Tooltip("Deactivated when transitioning (e.g. player controls, game HUD)")]
        [SerializeField] private List<GameObject> objectsToDeactivateOnSceneTransition = new List<GameObject>();

        [Header("Two-Stage Sequence")]
        [SerializeField] private bool enableTwoStageSequence;
        [SerializeField] private PlayableDirector firstSequenceDirector;
        [SerializeField] private PlayableDirector secondSequenceDirector;
        [SerializeField] private List<GameObject> objectsToActivateAfterFirstSequence = new List<GameObject>();
        [SerializeField] private List<GameObject> objectsToDeactivateAfterFirstSequence = new List<GameObject>();
        [Tooltip("Optional button to skip the first sequence")]
        [SerializeField] private MobileControllerFramework.Button skipFirstSequenceButton;
        [Header("Level Completion")]
        [Tooltip("When this director finishes playing, the level is completed.")]
        [SerializeField] private PlayableDirector levelCompletionDirector;

        [Header("Player")]
        [Tooltip("Reference to the PlayerHealth component in this level scene.")]
        [SerializeField] private SickOfIt.Combat.Health.PlayerHealth playerHealth;

        private bool levelCompleted;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Resolve serialized references
            if (enableTwoStageSequence)
            {
                if (firstSequenceDirector != null)
                {
                    firstSequenceDirector.stopped += OnFirstDirectorStopped;
                }

                // Wire up skip button if provided
                if (skipFirstSequenceButton != null)
                {
                    skipFirstSequenceButton.TapEndEvent.AddListener(SkipFirstSequence);
                }
            }

            if (levelCompletionDirector != null)
                levelCompletionDirector.stopped += OnLevelCompletionDirectorStopped;

            playerHealth?.InitializeForLevel(levelNumber);
            AnalyticsManager.Instance.TrackLevelStart(levelNumber);
        }

        private void OnDestroy()
        {
            if (levelCompletionDirector != null)
                levelCompletionDirector.stopped -= OnLevelCompletionDirectorStopped;
            if (firstSequenceDirector != null)
                firstSequenceDirector.stopped -= OnFirstDirectorStopped;
            if (skipFirstSequenceButton != null)
                skipFirstSequenceButton.TapEndEvent.RemoveListener(SkipFirstSequence);
        }

        #endregion



        #region Level Completion

        private void OnLevelCompletionDirectorStopped(PlayableDirector director)
        {
            CompleteLevel();
        }

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

            // Save the player's current health for the roguelite system
            SavePlayerHealthForRoguelite();
        }

        /// <summary>
        /// Save the player's current health to RogueliteManager when completing a level.
        /// </summary>
        private void SavePlayerHealthForRoguelite()
        {
            if (playerHealth != null)
            {
                RogueliteManager.Instance.SaveLevelEndHealth(levelNumber, playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
            else
            {
                Debug.LogWarning($"[LevelManager] Could not save roguelite health — PlayerHealth not found.");
            }
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

        #region Sequence Callbacks

        private void OnFirstDirectorStopped(PlayableDirector director)
        {
            OnFirstSequenceComplete();
        }

        private void OnFirstSequenceComplete()
        {
            if (!enableTwoStageSequence)
                return;

            // Hide first sequence director
            if (firstSequenceDirector != null)
                firstSequenceDirector.gameObject.SetActive(false);

            SetGameObjectsActive(objectsToDeactivateAfterFirstSequence, false);
            SetGameObjectsActive(objectsToActivateAfterFirstSequence, true);

            if (firstSequenceDirector != null)
                firstSequenceDirector.gameObject.SetActive(true);

            if (secondSequenceDirector != null)
            {
                secondSequenceDirector.time = 0;
                secondSequenceDirector.Play();
            }

            // Hide skip button after first sequence completes
            if (skipFirstSequenceButton != null)
                skipFirstSequenceButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Skip the first sequence and immediately trigger the second sequence.
        /// Called by the skip button.
        /// </summary>
        public void SkipFirstSequence()
        {
            if (!enableTwoStageSequence || firstSequenceDirector == null)
                return;

            // Skip to end — the director's stopped event will trigger OnFirstSequenceComplete
            firstSequenceDirector.time = firstSequenceDirector.duration;
            firstSequenceDirector.Evaluate();
            firstSequenceDirector.Stop();
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
