using SickOfIt.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

            // Setup GameClock for this video. RewindTimeline applies the audio scrub.
            gameClock = gameObject.AddComponent<GameClock>();
            gameObject.AddComponent<SickOfIt.Core.Rewind.RewindTimeline>();

            EnsurePlaybackCamera();
            EnsureEventSystem();
            AutoDiscoverReferences();
            SetupPlayback();
            StartExperience();
        }

        private void AutoDiscoverReferences()
        {
            // The shared mobile UI has no object named "skip". The circular control
            // is (Prb)ButtonRewind, so use that when a skip button is absent.
            if (uiRoot == null) return;

            var buttons = uiRoot.GetComponentsInChildren<MobileControllerFramework.Button>(true);
            MobileControllerFramework.Button rewindFallback = null;
            foreach (var btn in buttons)
            {
                string name = btn.gameObject.name.ToLowerInvariant();
                if (name.Contains("skip"))
                {
                    skipButton = btn;
                    break;
                }
                if (rewindFallback == null && name.Contains("rewind"))
                    rewindFallback = btn;
            }

            if (skipButton == null)
                skipButton = rewindFallback;
        }

        /// <summary>
        /// Video.unity has no camera. The mobile canvas is Screen Space - Camera
        /// with no world camera, so pointer clicks never reach the skip button,
        /// and there is no AudioListener once the menu scene unloads.
        /// </summary>
        private void EnsurePlaybackCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("VideoCamera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                go.transform.position = new Vector3(0f, 0f, -10f);
            }

            if (cam.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].renderMode == RenderMode.ScreenSpaceCamera && canvases[i].worldCamera == null)
                    canvases[i].worldCamera = cam;
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void SetupPlayback()
        {
            if (skipButton != null)
            {
                skipButton.TapStartEvent.AddListener(OnRewindButtonDown);
                skipButton.TapEndEvent.AddListener(OnRewindButtonUp);
            }

            // Setup SpriteFramePlayer if we have sprite frames data
            var framesData = config.spriteFramesData as SpriteFramesData;
            if (framesData != null && !framesData.HasBuiltFrames)
            {
                Debug.LogWarning($"[VideoRuntime] Video {videoNumber} has no built sprite sheets. Returning to the main menu.");
                SceneTransitionManager.Instance.LoadMainMenu();
                return;
            }
            else if (framesData != null)
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
                    spritePlayer.UseGameClock(true);
                    spritePlayer.Play();
                });
                spritePlayer.UseGameClock(true);
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
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
                audioSource.dopplerLevel = 0f;
                audioSource.loop = false;
                audioSource.clip = config.audio;
                audioSource.volume = 1f;
                if (audioSource.GetComponent<RewindableAudioSource>() == null)
                    audioSource.gameObject.AddComponent<RewindableAudioSource>();
                StartCoroutine(PlayClipWhenAudible(audioSource));
            }
        }

        private static IEnumerator PlayClipWhenAudible(AudioSource source)
        {
            var clip = source != null ? source.clip : null;
            if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();

            float waited = 0f;
            while (source != null && !source.gameObject.activeInHierarchy && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (source == null || !source.gameObject.activeInHierarchy)
                yield break;

            source.enabled = true;
            source.Play();
            ////Debug.Log($"[VideoRuntime] Audio '{(clip != null ? clip.name : "none")}' playing={source.isPlaying}");
        }

        private void StartExperience()
        {
            if (config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            var runner = gameObject.AddComponent<ExperienceRunner>();
            runner.Run(config.timeline);
        }

        private bool rewindButtonHeld;
        private float rewindPressedAt;

        private void OnRewindButtonDown()
        {
            rewindButtonHeld = true;
            rewindPressedAt = Time.unscaledTime;
        }

        private void OnRewindButtonUp()
        {
            rewindButtonHeld = false;
            bool wasRewinding = gameClock != null && gameClock.IsRewinding;
            if (wasRewinding)
                gameClock.StopRewind();
            else
                SkipVideo();
        }

        private void Update()
        {
            if (videoCompleted) return;

            if (rewindButtonHeld && gameClock != null && !gameClock.IsRewinding &&
                Time.unscaledTime - rewindPressedAt >= 0.22f && gameClock.Time > 0.05f)
                gameClock.StartRewind(3f);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SkipVideo();
                return;
            }

            if (gameClock != null && gameClock.IsRewinding) return;

            if (spritePlayer != null && spritePlayer.IsReady && spritePlayer.Progress >= 0.99f)
            {
                OnVideoComplete();
            }
        }

        private void OnDestroy()
        {
            if (skipButton != null)
            {
                skipButton.TapStartEvent.RemoveListener(OnRewindButtonDown);
                skipButton.TapEndEvent.RemoveListener(OnRewindButtonUp);
            }
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
