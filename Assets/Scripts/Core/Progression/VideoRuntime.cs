using SickOfIt.Core;
using System.Collections;
using System.Collections.Generic;
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
        private ExperienceRunner experienceRunner;
        private SpriteFramePlayer spritePlayer;
        private readonly List<SpriteFramePlayer> playbackPlayers = new List<SpriteFramePlayer>();
        private AudioSource playbackAudio;
        private bool leavingScene;
        private MobileControllerFramework.Button skipButton;
        private MobileControllerFramework.Button rewindButton;
        private bool rewindAlsoSkips;
        private VideoConfig config;
        private bool videoWasSkipped;
        private bool videoCompleted;
        private bool playbackBegun;
        private float lastClockTime = -1f;
        private float stalledSeconds;
        private const float StallSecondsBeforeAdvance = 6f;

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
            if (!leavingScene)
                StartCoroutine(PlaybackStartGate.Hold(gameClock, playbackPlayers, playbackAudio, BeginVideoPlayback));
        }

        private void AutoDiscoverReferences()
        {
            if (uiRoot == null) return;

            var buttons = uiRoot.GetComponentsInChildren<MobileControllerFramework.Button>(true);
            foreach (var btn in buttons)
            {
                string name = btn.gameObject.name.ToLowerInvariant();
                if (skipButton == null && name.Contains("skip"))
                    skipButton = btn;
                else if (rewindButton == null && name.Contains("rewind"))
                    rewindButton = btn;
            }

            // Older scenes have one control. A short tap skips and a hold rewinds.
            rewindAlsoSkips = skipButton == null;
            if (rewindAlsoSkips)
                skipButton = rewindButton;
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
                cam.orthographicSize = CinematicOrthoSize();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = CinematicBackground();
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

        private static void EnsureEventSystem() => SickOfIt.Core.UI.EventSystemGuard.Ensure();

        private void SetupPlayback()
        {
            if (rewindButton != null && !rewindAlsoSkips)
            {
                rewindButton.TapStartEvent.AddListener(OnRewindButtonDown);
                rewindButton.TapEndEvent.AddListener(OnRewindButtonUp);
            }
            else if (skipButton != null)
            {
                skipButton.TapStartEvent.AddListener(OnRewindButtonDown);
                skipButton.TapEndEvent.AddListener(OnRewindButtonUp);
            }

            if (skipButton != null && !rewindAlsoSkips)
                skipButton.TapEndEvent.AddListener(OnSkipButton);

            // Setup SpriteFramePlayer if we have sprite frames data
            var framesData = config.spriteFramesData as SpriteFramesData;
            if (framesData != null && !framesData.HasBuiltFrames)
            {
                Debug.LogWarning($"[VideoRuntime] Video {videoNumber} has no built sprite sheets. Returning to the main menu.");
                leavingScene = true;
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
                    var sr = videoPlayerObject.AddComponent<SpriteRenderer>();
                    SpriteLayers.Apply(sr, SpriteLayer.Background1);
                    videoPlayerObject.transform.position = new Vector3(0f, 0f, SpriteLayers.Depth(SpriteLayer.Background1));
                    sr.color = Color.white;
                }

                spritePlayer = videoPlayerObject.GetComponent<SpriteFramePlayer>();
                if (spritePlayer == null)
                    spritePlayer = videoPlayerObject.AddComponent<SpriteFramePlayer>();
                playbackPlayers.Add(spritePlayer);

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
                playbackAudio = audioSource;
            }
        }

        private void BeginVideoPlayback()
        {
            playbackBegun = true;
            if (experienceRunner != null)
                experienceRunner.SetPlaybackHeld(false);

            if (playbackAudio == null || playbackAudio.clip == null)
                return;
            if (!playbackAudio.gameObject.activeInHierarchy)
                return;

            playbackAudio.enabled = true;
            playbackAudio.Play();
        }

        private void StartExperience()
        {
            if (config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            experienceRunner = gameObject.AddComponent<ExperienceRunner>();
            experienceRunner.Run(config.timeline);
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
            else if (rewindAlsoSkips)
                SkipVideo();
        }

        private void OnSkipButton()
        {
            if (gameClock != null && gameClock.IsRewinding)
                gameClock.StopRewind();
            SkipVideo();
        }

        private void Update()
        {
            if (videoCompleted) return;

            if (rewindButtonHeld && gameClock != null && !gameClock.IsRewinding &&
                Time.unscaledTime - rewindPressedAt >= RewindHoldSeconds() && gameClock.Time > 0.05f)
                gameClock.StartRewind(RewindSpeed());

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SkipVideo();
                return;
            }

            if (gameClock != null && gameClock.IsRewinding) return;

            // Progress is frame/total and never reaches 1, so also test the last frame and the clock itself.
            if (spritePlayer != null && spritePlayer.IsReady &&
                (spritePlayer.Progress >= 0.99f || spritePlayer.CurrentFrame >= spritePlayer.TotalFrames - 1 ||
                 (gameClock != null && spritePlayer.Duration > 0f && gameClock.Time >= spritePlayer.Duration)))
            {
                OnVideoComplete();
                return;
            }

            // Watchdog: if the clock stops advancing once playback began (stalled load), move on
            // instead of leaving a black screen.
            if (playbackBegun && gameClock != null)
            {
                if (Mathf.Approximately(gameClock.Time, lastClockTime))
                    stalledSeconds += Time.unscaledDeltaTime;
                else
                    stalledSeconds = 0f;
                lastClockTime = gameClock.Time;

                if (stalledSeconds >= StallSecondsBeforeAdvance)
                {
                    Debug.LogWarning($"[VideoRuntime] Video {videoNumber} stalled at {gameClock.Time:F1}s; continuing.");
                    OnVideoComplete();
                }
            }
        }

        private void OnDestroy()
        {
            if (rewindButton != null && !rewindAlsoSkips)
            {
                rewindButton.TapStartEvent.RemoveListener(OnRewindButtonDown);
                rewindButton.TapEndEvent.RemoveListener(OnRewindButtonUp);
            }
            else if (skipButton != null)
            {
                skipButton.TapStartEvent.RemoveListener(OnRewindButtonDown);
                skipButton.TapEndEvent.RemoveListener(OnRewindButtonUp);
            }

            if (skipButton != null && !rewindAlsoSkips)
                skipButton.TapEndEvent.RemoveListener(OnSkipButton);
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

        static float CinematicOrthoSize()
        {
            var gd = GameDefinition.Instance;
            return gd != null && gd.cinematicCameraOrthoSize > 0f ? gd.cinematicCameraOrthoSize : 5f;
        }

        static Color CinematicBackground()
        {
            var gd = GameDefinition.Instance;
            return gd != null ? gd.cinematicCameraBackground : Color.black;
        }

        static float RewindHoldSeconds()
        {
            var player = GameDefinition.Instance != null ? GameDefinition.Instance.player : null;
            return player != null ? player.rewindHoldSeconds : 0.22f;
        }

        static float RewindSpeed()
        {
            var player = GameDefinition.Instance != null ? GameDefinition.Instance.player : null;
            return player != null && player.rewindSpeed > 0f ? player.rewindSpeed : 3f;
        }
    }
}
