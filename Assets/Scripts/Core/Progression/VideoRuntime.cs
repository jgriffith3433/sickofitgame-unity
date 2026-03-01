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
    /// Auto-bootstraps on any scene starting with "Video" (except VideoSelection).
    /// ONE generic video scene handles all videos — the config defines everything.
    /// </summary>
    public class VideoRuntime : MonoBehaviour
    {
        #region Static Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name != "Video")
                return;

            if (Object.FindAnyObjectByType<VideoRuntime>() != null)
                return;

            var go = new GameObject("VideoRuntime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<VideoRuntime>();
            Debug.Log($"[VideoRuntime] Auto-bootstrapped in scene '{scene.name}'");
        }

        #endregion

        [Header("Video Identity")]
        [SerializeField] private int videoNumber = 1;

        private GameClock gameClock;
        private SpriteFramePlayer spritePlayer;
        private MobileControllerFramework.Button skipButton;
        private VideoConfig config;
        private bool videoWasSkipped;
        private bool videoCompleted;

        private void Start()
        {
            Debug.Log($"[VideoRuntime] Starting in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");
            
            if (PlayerPrefs.HasKey("CurrentVideoNumber"))
            {
                videoNumber = PlayerPrefs.GetInt("CurrentVideoNumber", videoNumber);
                Debug.Log($"[VideoRuntime] Found CurrentVideoNumber: {videoNumber}");
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
            Debug.Log($"[VideoRuntime] Found GameDefinition with {gd.TotalVideos} videos");

            config = gd.GetVideo(videoNumber);
            if (config == null)
            {
                Debug.LogError($"[VideoRuntime] No VideoConfig for video {videoNumber}!");
                return;
            }
            Debug.Log($"[VideoRuntime] Found VideoConfig: '{config.displayName}'");

            // Setup GameClock for this video
            gameClock = gameObject.AddComponent<GameClock>();

            AutoDiscoverReferences();
            SetupPlayback();
            StartExperience();

            Debug.Log($"[VideoRuntime] Video {videoNumber} '{config.displayName}' loaded");
        }

        private void AutoDiscoverReferences()
        {
            // Find skip button
            var buttons = Object.FindObjectsByType<MobileControllerFramework.Button>(FindObjectsSortMode.None);
            foreach (var btn in buttons)
            {
                if (btn.gameObject.name.ToLower().Contains("skip"))
                {
                    skipButton = btn;
                    break;
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
                Debug.Log($"[VideoRuntime] Found SpriteFramesData: {framesData.videoName}");
                // Find or create a sprite renderer for video playback
                var videoObj = GameObject.Find("VideoPlayer");
                if (videoObj == null)
                {
                    videoObj = new GameObject("VideoPlayer");
                    videoObj.transform.SetParent(transform);
                    videoObj.transform.position = Vector3.zero; // Ensure it's at origin where camera can see it
                    var sr = videoObj.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 0; // Make sure it's visible (not behind other objects)
                    sr.color = Color.white; // Ensure it's not transparent
                }

                spritePlayer = videoObj.GetComponent<SpriteFramePlayer>();
                if (spritePlayer == null)
                    spritePlayer = videoObj.AddComponent<SpriteFramePlayer>();

                spritePlayer.Initialize(framesData, () =>
                {
                    Debug.Log($"[VideoRuntime] Sprite frames ready, starting playback");
                    Debug.Log($"[VideoRuntime] SpriteFramePlayer - IsReady: {spritePlayer.IsReady}, TotalFrames: {spritePlayer.TotalFrames}");
                    AnalyticsManager.Instance?.TrackVideoStart(videoNumber);
                    
                    // Force the sprite player to play
                    spritePlayer.Play();
                    Debug.Log("[VideoRuntime] Forced SpriteFramePlayer.Play()");
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
                var audioGo = GameObject.Find("Audio");
                if (audioGo == null)
                {
                    audioGo = new GameObject("Audio");
                    audioGo.transform.SetParent(transform);
                }
                var audioSource = audioGo.GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = audioGo.AddComponent<AudioSource>();
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

            // Debug sprite player status
            if (spritePlayer != null)
            {
                if (!spritePlayer.IsReady)
                {
                    Debug.Log($"[VideoRuntime] SpritePlayer not ready yet...");
                }
                else if (spritePlayer.Progress >= 0.99f)
                {
                    Debug.Log($"[VideoRuntime] Video playback complete!");
                    OnVideoComplete();
                }
                else if (Time.frameCount % 60 == 0) // Log every 60 frames
                {
                    Debug.Log($"[VideoRuntime] Playing... Progress: {spritePlayer.Progress:P1}, Frame: {spritePlayer.CurrentFrame}/{spritePlayer.TotalFrames}, IsPlaying: {spritePlayer.IsPlaying}");
                }
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
