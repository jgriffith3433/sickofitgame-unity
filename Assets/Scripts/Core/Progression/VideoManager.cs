using UnityEngine;
using UnityEngine.Playables;
using MobileControllerFramework;
using SickOfIt.Core.Flow;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manager for video scenes. Handles video playback and scene transitions.
    /// </summary>
    public class VideoManager : MonoBehaviour
    {
        [Header("Video Info")]
        [SerializeField] private int videoNumber = 1;

        [Header("Scene Transition")]
        [Tooltip("If enabled, returns to main menu after video instead of loading next level")]
        [SerializeField] private bool returnToMainMenu = false;

        [Header("Skip Settings")]
        [Tooltip("Optional skip button using MobileControllerFramework")]
        [SerializeField] private MobileControllerFramework.Button skipButton;

        [Header("Playback")]
        [Tooltip("PlayableDirector for the video")]
        [SerializeField] private PlayableDirector videoDirector;

        private bool videoWasSkipped = false;

        private void Awake()
        {

            // Subscribe to skip button
            if (skipButton != null)
            {
                skipButton.TapEndEvent.AddListener(SkipVideo);
            }

            // Subscribe to director completion callback
            if (videoDirector != null)
            {
                videoDirector.stopped += OnDirectorStopped;

                // Play the video
                videoDirector.time = 0;
                videoDirector.Play();

                SickOfIt.Core.AnalyticsManager.Instance.TrackVideoStart(videoNumber);
            }
        }

        private void OnDestroy()
        {
            if (videoDirector != null)
                videoDirector.stopped -= OnDirectorStopped;

            if (skipButton != null)
                skipButton.TapEndEvent.RemoveListener(SkipVideo);
        }

        private void OnDirectorStopped(PlayableDirector director)
        {
            OnVideoComplete();
        }

        /// <summary>
        /// Called when video playback completes
        /// </summary>
        private void OnVideoComplete()
        {
            // Track video completion (only if not skipped)
            if (!videoWasSkipped)
            {
                SickOfIt.Core.AnalyticsManager.Instance.TrackVideoComplete(videoNumber);
            }

            // Check if configured to return to main menu
            if (returnToMainMenu)
            {
                SceneTransitionManager.Instance.LoadMainMenu();
                return;
            }

            // Check if we should return to a specific level (set by main menu or video selection)
            if (PlayerPrefs.HasKey("ReturnToLevel"))
            {
                int levelToLoad = PlayerPrefs.GetInt("ReturnToLevel");
                PlayerPrefs.DeleteKey("ReturnToLevel");
                PlayerPrefs.Save();

                // Special case: -1 means return to main menu (from video gallery)
                if (levelToLoad == -1)
                {
                    SceneTransitionManager.Instance.LoadMainMenu();
                }
                else
                {
                    SceneTransitionManager.Instance.LoadLevel(levelToLoad);
                }
            }
            else
            {
                // Default behavior: Load the level matching this video number
                SceneTransitionManager.Instance.LoadLevel(videoNumber);
            }
        }

        /// <summary>
        /// Skip the currently playing video
        /// </summary>
        public void SkipVideo()
        {
            if (videoDirector != null)
            {
                Debug.Log("VideoManager: Video skipped by player");

                // Track video skip
                videoWasSkipped = true;
                SickOfIt.Core.AnalyticsManager.Instance.TrackVideoSkip(videoNumber);

                // Stop immediately without Evaluate(). Calling Evaluate() at
                // duration causes ProcessFrame to fire for the last video frame,
                // which triggers an async Addressable load for the final chunk.
                // That load can't be cancelled and its textures decompress into
                // WASM memory alongside the next scene's load, causing OOM on WebGL.
                videoDirector.Stop();
            }
        }

    }
}
