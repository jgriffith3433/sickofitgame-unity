using UnityEngine;
using UnityEngine.Playables;
using System.Collections;
using MobileControllerFramework;

namespace SickOfIt.Core.Flow
{
    /// <summary>
    /// Manager for death scenes. Handles death animation playback and automatically
    /// reloads the level after animation completes.
    /// </summary>
    public class DeathManager : MonoBehaviour
    {
        [Header("Playback")]
        [Tooltip("PlayableDirector for the death animation")]
        [SerializeField] private PlayableDirector deathDirector;

        [Header("Skip Settings")]
        [Tooltip("Optional skip button using MobileControllerFramework")]
        [SerializeField] private MobileControllerFramework.Button skipButton;

        [Header("Playback Settings")]
        [Tooltip("How long to wait after death animation finishes before reloading level")]
        [SerializeField] private float delayBeforeReload = 1f;

        private bool animationComplete;

        private void Awake()
        {

            // Subscribe to skip button
            if (skipButton != null)
            {
                skipButton.TapEndEvent.AddListener(SkipDeathAnimation);
            }

            // Subscribe to director completion callback
            if (deathDirector != null)
            {
                deathDirector.stopped += OnDirectorStopped;

                // Play the death animation
                deathDirector.time = 0;
                deathDirector.Play();
            }
            else
            {
                // No death player, just reload
                StartCoroutine(ReloadLevelAfterDelay());
            }
        }

        private void OnDestroy()
        {
            if (deathDirector != null)
                deathDirector.stopped -= OnDirectorStopped;

            if (skipButton != null)
                skipButton.TapEndEvent.RemoveListener(SkipDeathAnimation);
        }

        private void OnDirectorStopped(PlayableDirector director)
        {
            OnDeathAnimationComplete();
        }

        /// <summary>
        /// Skip the currently playing death animation
        /// </summary>
        public void SkipDeathAnimation()
        {
            if (deathDirector != null)
            {
                Debug.Log("DeathManager: Death animation skipped by player");
                deathDirector.time = deathDirector.duration;
                deathDirector.Evaluate();
                deathDirector.Stop();
            }
        }

        /// <summary>
        /// Called when death animation completes (via callback)
        /// </summary>
        private void OnDeathAnimationComplete()
        {
            animationComplete = true;

            // Wait a moment before reloading
            StartCoroutine(ReloadLevelAfterDelay());
        }

        /// <summary>
        /// Reload after a delay (fallback if no animation)
        /// </summary>
        private IEnumerator ReloadLevelAfterDelay()
        {
            yield return new WaitForSeconds(delayBeforeReload);
            ReloadLevel();
        }

        /// <summary>
        /// Reload the level that the player died in
        /// </summary>
        private void ReloadLevel()
        {
            // Check if there's a level stored to reload
            if (PlayerPrefs.HasKey("DeathSceneToReload"))
            {
                string sceneName = PlayerPrefs.GetString("DeathSceneToReload");
                PlayerPrefs.DeleteKey("DeathSceneToReload");
                PlayerPrefs.Save();

                // Load the scene (which will unload Death1 and activate the preloaded level)
                if (sceneName.StartsWith("Level"))
                {
                    // Extract level number and load via SceneTransitionManager
                    string numberStr = sceneName.Replace("Level", "");
                    if (int.TryParse(numberStr, out int levelNum))
                    {
                        SceneTransitionManager.Instance.LoadLevel(levelNum);
                    }
                    else
                    {
                        Debug.LogError($"[DeathManager] Could not parse level number from '{sceneName}'");
                        SceneTransitionManager.Instance.LoadMainMenu();
                    }
                }
                else
                {
                    // Generic scene load
                    SceneTransitionManager.Instance.LoadDeathScene(sceneName); // Reusing LoadDeathScene for any scene name
                }
            }
            else
            {
                Debug.LogWarning("[DeathManager] No scene to reload, returning to main menu");
                SceneTransitionManager.Instance.LoadMainMenu();
            }
        }

    }
}
