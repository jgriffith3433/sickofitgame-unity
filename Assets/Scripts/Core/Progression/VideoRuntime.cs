using SickOfIt.Core;
using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Config-driven video/cutscene runtime. Reads VideoConfig from GameDefinition
    /// and drives playback via SpriteFramePlayer (no PlayableDirector).
    /// 
    /// Added directly to generated scenes by ProjectGenerator / VideoSceneGenerator.
    /// ONE generic video scene handles all videos — the config defines everything.
    /// </summary>
    public class VideoRuntime : MonoBehaviour
    {
        [Header("Video Identity")]
        [SerializeField] private int videoNumber = 1;

        [Header("Scene References (wired by generator)")]
        [SerializeField] private GameObject videoPlayerObject;
        [SerializeField] private GameObject audioObject;
        [SerializeField] private GameObject uiRoot;

        private GameClock gameClock;
        private SpriteFramePlayer spritePlayer;
        private MobileControllerFramework.Button skipButton;
        private VideoConfig config;
        private bool videoWasSkipped;
        private bool videoCompleted;

        private void Start()
        {
            if (PlayerPrefs.HasKey("CurrentVideoNumber"))
            {
                videoNumber = PlayerPrefs.GetInt("CurrentVideoNumber", videoNumber);
            }
            else
            {
                Debug.LogWarning($"[VideoRuntime] No CurrentVideoNumber in PlayerPrefs, using default: {videoNumber}");
            }

            var gd = GameDefinition.Instance;
            if (gd == null)
            {
                Debug.LogError("[VideoRuntime] GameDefinition not found!");
                return;
            }

            config = gd.GetVideo(videoNumber);
            if (config == null)
            {
                Debug.LogError($"[VideoRuntime] No VideoConfig for video {videoNumber}!");
                return;
            }

            // Setup GameClock for this video
            gameClock = gameObject.AddComponent<GameClock>();

            AutoDiscoverReferences();
            SetupPlayback();
            StartExperience();
        }

        private void AutoDiscoverReferences()
        {
            // Find skip button from serialized UI reference
            if (uiRoot != null)
            {
                var buttons = uiRoot.GetComponentsInChildren<MobileControllerFramework.Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn.gameObject.name.ToLower().Contains("skip"))
                    {
                        skipButton = btn;
                        break;
                    }
                }
            }
        }

        private void SetupPlayback()
        {
            if (skipButton != null)
                skipButton.TapEndEvent.AddListener(SkipVideo);

            // Setup SpriteFramePlayer if we have sprite frames data
            var framesData = config.spriteFramesData as SpriteFramesData;
            if (framesData != null)
            {
                // Use serialized reference or create fallback
                if (videoPlayerObject == null)
                {
                    videoPlayerObject = new GameObject("VideoPlayer");
                    videoPlayerObject.transform.SetParent(transform);
                    videoPlayerObject.transform.position = Vector3.zero;
                    var sr = videoPlayerObject.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 0;
                    sr.color = Color.white;
                }

                spritePlayer = videoPlayerObject.GetComponent<SpriteFramePlayer>();
                if (spritePlayer == null)
                    spritePlayer = videoPlayerObject.AddComponent<SpriteFramePlayer>();

                spritePlayer.Initialize(framesData, () =>
                {
                    AnalyticsManager.Instance?.TrackVideoStart(videoNumber);
                    spritePlayer.Play();
                });
            }
            else
            {
                Debug.LogWarning("[VideoRuntime] No SpriteFramesData found in config! Video will not play.");
                // No frames — just proceed after a delay
                AnalyticsManager.Instance?.TrackVideoStart(videoNumber);
                Invoke(nameof(OnVideoComplete), 1f);
            }

            // Setup audio
            if (config.audio != null)
            {
                if (audioObject == null)
                {
                    audioObject = new GameObject("Audio");
                    audioObject.transform.SetParent(transform);
                }
                var audioSource = audioObject.GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = audioObject.AddComponent<AudioSource>();
                audioSource.clip = config.audio;
                audioSource.Play();
            }
        }

        private void StartExperience()
        {
            if (config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            var runner = gameObject.AddComponent<ExperienceRunner>();
            runner.Run(config.timeline);
        }

        private void Update()
        {
            if (videoCompleted) return;

            if (spritePlayer != null && spritePlayer.IsReady && spritePlayer.Progress >= 0.99f)
            {
                OnVideoComplete();
            }
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.TapEndEvent.RemoveListener(SkipVideo);
        }

        public void SkipVideo()
        {
            if (videoCompleted) return;
            videoWasSkipped = true;
            AnalyticsManager.Instance?.TrackVideoSkip(videoNumber);
            // Release sprite resources immediately to free memory before scene transition
            if (spritePlayer != null)
                spritePlayer.Release();
            OnVideoComplete();
        }

        private void OnVideoComplete()
        {
            if (videoCompleted) return;
            videoCompleted = true;

            if (!videoWasSkipped)
                AnalyticsManager.Instance?.TrackVideoComplete(videoNumber);

            if (config.returnToMainMenu)
            {
                SceneTransitionManager.Instance.LoadMainMenu();
                return;
            }

            if (PlayerPrefs.HasKey("ReturnToLevel"))
            {
                int levelToLoad = PlayerPrefs.GetInt("ReturnToLevel");
                PlayerPrefs.DeleteKey("ReturnToLevel");
                PlayerPrefs.Save();

                if (levelToLoad == -1)
                    SceneTransitionManager.Instance.LoadMainMenu();
                else
                    SceneTransitionManager.Instance.LoadLevel(levelToLoad);
            }
            else if (config.nextLevel > 0)
            {
                SceneTransitionManager.Instance.LoadLevel(config.nextLevel);
            }
            else
            {
                SceneTransitionManager.Instance.LoadLevel(videoNumber);
            }
        }
    }
}
