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
        private const float AudioDriftSeconds = 0.3f;
        // A picture that is not ready by then is a black screen: go on to the next scene instead.
        private const float PictureReadySeconds = 1.75f;
        private const float StartSeconds = 4f;
        private const float InputGraceSeconds = 0.2f;
        private float startedAt;
        // The intro (Video1) brings the first level's bundles in while it plays, so a skip only activates the level.
        private const int IntroVideo = 1;
        private int preloadedLevel;
        /// <summary>Playtest: real seconds from the tap that skipped the intro to its next scene being asked for, and whether it was the fallback menu.</summary>
        public static float LastSkipAt { get; private set; } = -1f;
        public static string LastSkipTarget { get; private set; } = "";

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

            // R still held from the level counts as a hold, not a new press.
            rewindKeyHeld = Keyboard.current != null && Keyboard.current.rKey.isPressed;

            EnsurePlaybackCamera();
            EnsureEventSystem();
            AutoDiscoverReferences();
            SetupPlayback();
            StartExperience();
            if (!leavingScene)
            {
                startedAt = Time.unscaledTime;
                StartPreload();
                StartCoroutine(PlaybackStartGate.Hold(gameClock, playbackPlayers, playbackAudio, BeginVideoPlayback));
                // One-shot timers (not polling): the sheets or the soundtrack never arrived -> no black wait.
                Invoke(nameof(GiveUpIfPictureNotReady), PictureReadySeconds);
                Invoke(nameof(GiveUpIfNotStarted), StartSeconds);
            }
        }

        /// <summary>The level this video leads to, or -1 for the main menu. Reads the return key without clearing it.</summary>
        private int PeekNextLevel()
        {
            if (config.returnToMainMenu) return -1;
            if (PlayerPrefs.HasKey("ReturnToLevel")) return PlayerPrefs.GetInt("ReturnToLevel");
            return config.nextLevel > 0 ? config.nextLevel : videoNumber;
        }

        private void StartPreload()
        {
            if (videoNumber != IntroVideo) return;
            int next = PeekNextLevel();
            if (next <= 0) return;
            preloadedLevel = next;
            SceneTransitionManager.Instance.PreloadLevel(next);
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
            TransportButtonLayout.Apply(gameObject, rewindButton, skipButton);
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
                    TrackVideo("play");
                    spritePlayer.UseGameClock(true);
                    spritePlayer.Play();
                });
                spritePlayer.UseGameClock(true);
            }
            else
            {
                Debug.LogWarning("[VideoRuntime] No SpriteFramesData found in config! Video will not play.");
                // No frames — just proceed after a delay
                TrackVideo("play");
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

            // A clip that won't start (the browser is out of media players, say) must never hold the video up.
            try
            {
                playbackAudio.enabled = true;
                playbackAudio.Play();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[VideoRuntime] Video {videoNumber}'s soundtrack could not start: {e.Message}");
            }
        }

        /// <summary>
        /// Stops the soundtrack and lets go of its clip. On WebGL a playing clip holds a browser media player
        /// (about 75 allowed at once); a video that ends or is skipped must give it back before the next scene.
        /// </summary>
        private void ReleaseAudio()
        {
            if (playbackAudio == null) return;
            playbackAudio.Stop();
            playbackAudio.clip = null;
        }

        private void StartExperience()
        {
            if (config.timeline == null || !config.timeline.HasContent)
                return;

            experienceRunner = gameObject.AddComponent<ExperienceRunner>();
            experienceRunner.Run(config.timeline);
        }

        private bool rewindButtonHeld;
        private bool rewindKeyHeld;
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

        private void GiveUpIfPictureNotReady()
        {
            if (videoCompleted || playbackBegun) return;
            if (spritePlayer != null && spritePlayer.IsReady) return;
            Debug.LogWarning($"[VideoRuntime] Video {videoNumber}'s picture was not ready in {PictureReadySeconds}s; moving on.");
            TrackVideo("skip");
            OnVideoComplete();
        }

        private void GiveUpIfNotStarted()
        {
            if (videoCompleted || playbackBegun) return;
            Debug.LogWarning($"[VideoRuntime] Video {videoNumber} did not start in {StartSeconds}s; moving on.");
            TrackVideo("skip");
            OnVideoComplete();
        }

        /// <summary>The first tap, click or key press anywhere skips. The rewind key and the rewind button keep their job.</summary>
        private bool SkipPressed()
        {
            if (Time.unscaledTime - startedAt < InputGraceSeconds) return false;

            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame && !kb.rKey.isPressed)
                return true;

            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
                return true;

            Vector2 at = default;
            bool pressed = false;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                pressed = true;
                at = touch.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pressed = true;
                at = Mouse.current.position.ReadValue();
            }
            if (!pressed) return false;

            // A press that lands on the rewind button starts a hold; its release still skips when it is the only button.
            if (rewindButton != null && !rewindAlsoSkips)
            {
                var rect = (RectTransform)rewindButton.transform;
                var canvas = rect.GetComponentInParent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, at, cam)) return false;
            }
            return !rewindAlsoSkips || rewindButton == null || !OverRewind(at);
        }

        private bool OverRewind(Vector2 at)
        {
            var rect = (RectTransform)rewindButton.transform;
            var canvas = rect.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, at, cam);
        }

        private void Update()
        {
            if (videoCompleted) return;

            if (SkipPressed())
            {
                if (gameClock != null && gameClock.IsRewinding)
                    gameClock.StopRewind();
                SkipVideo();
                return;
            }

            if (rewindButtonHeld && gameClock != null && !gameClock.IsRewinding &&
                Time.unscaledTime - rewindPressedAt >= RewindHoldSeconds() && gameClock.Time > 0.05f)
                gameClock.StartRewind(RewindSpeed());

            // Hold R to rewind, as in a level (the on-screen ◄◄ button does the same).
            bool rewindKey = Keyboard.current != null && Keyboard.current.rKey.isPressed;
            if (rewindKey && !rewindKeyHeld && gameClock != null && !gameClock.IsRewinding && gameClock.Time > 0.05f)
                gameClock.StartRewind(RewindSpeed());
            else if (!rewindKey && rewindKeyHeld && !rewindButtonHeld && gameClock != null && gameClock.IsRewinding)
                gameClock.StopRewind();
            rewindKeyHeld = rewindKey;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SkipVideo();
                return;
            }

            if (gameClock != null && gameClock.IsRewinding) return;

            KeepAudioInSync();

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

        /// <summary>
        /// The picture follows the game clock but the sound runs on the browser's audio clock. A hidden tab
        /// stops the frame loop (the picture waits) while the audio keeps going, or the audio context is
        /// suspended while the picture goes on, so they drift. Pull the audio back to the clock when they differ.
        /// </summary>
        private void KeepAudioInSync()
        {
            if (!playbackBegun || playbackAudio == null || playbackAudio.clip == null || gameClock == null) return;
            if (!playbackAudio.gameObject.activeInHierarchy) return;

            float length = playbackAudio.clip.length;
            float clock = gameClock.Time;
            if (clock < 0f || clock >= length - 0.05f) return;

            bool stopped = !playbackAudio.isPlaying;
            if (!stopped && Mathf.Abs(playbackAudio.time - clock) <= AudioDriftSeconds) return;

            Debug.Log($"[VideoRuntime] Audio {(stopped ? "stopped" : "drifted")} (audio {playbackAudio.time:F2}s, video {clock:F2}s); re-syncing.");
            playbackAudio.time = clock;
            if (stopped) playbackAudio.Play();
        }

        private void OnDestroy()
        {
            ReleaseAudio();
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

        /// <summary>Analytics: this story video played, finished or was skipped, and how far in.</summary>
        private void TrackVideo(string action)
        {
            if (action == "play")
            {
                AnalyticsManager.Screen("video");
                AnalyticsManager.SetContext("video", "Video" + videoNumber);
            }
            AnalyticsManager.Video("Video" + videoNumber, action, gameClock != null ? gameClock.Time : 0f);
            if (action != "play")
                AnalyticsManager.SetContext("video", null);
        }

        public void SkipVideo()
        {
            if (videoCompleted) return;
            videoWasSkipped = true;
            TrackVideo("skip");
            // Release sprite resources immediately to free memory before scene transition
            if (spritePlayer != null)
                spritePlayer.Release();
            OnVideoComplete();
        }

        private void OnVideoComplete()
        {
            if (videoCompleted) return;
            videoCompleted = true;
            ReleaseAudio();

            if (!videoWasSkipped)
                TrackVideo("complete");

            // Skipped (or given up on) before the level's bundles are in: go to the main menu, which is already in the
            // build, rather than hold a black loading screen for seconds.
            if ((videoWasSkipped || !playbackBegun) && preloadedLevel > 0 && !SceneTransitionManager.Instance.LevelAssetsReady(preloadedLevel))
            {
                Debug.LogWarning($"[VideoRuntime] Level {preloadedLevel} is not loaded yet; the main menu instead of a loading screen.");
                SceneTransitionManager.Instance.CancelPreload();
                LastSkipAt = Time.realtimeSinceStartup;
                LastSkipTarget = "MainMenu";
                SceneTransitionManager.Instance.LoadMainMenu();
                return;
            }
            if (videoNumber == IntroVideo)
            {
                LastSkipAt = Time.realtimeSinceStartup;
                LastSkipTarget = config.returnToMainMenu ? "MainMenu" : "Level";
            }

            if (config.returnToMainMenu)
            {
                SceneTransitionManager.Instance.LoadMainMenu();
                return;
            }

            int levelToLoad;
            if (PlayerPrefs.HasKey("ReturnToLevel"))
            {
                levelToLoad = PlayerPrefs.GetInt("ReturnToLevel");
                PlayerPrefs.DeleteKey("ReturnToLevel");
                PlayerPrefs.Save();

                if (levelToLoad == -1)
                {
                    SceneTransitionManager.Instance.LoadMainMenu();
                    return;
                }
            }
            else if (config.nextLevel > 0)
            {
                levelToLoad = config.nextLevel;
            }
            else
            {
                levelToLoad = videoNumber;
            }

            // The Shaft (Hell's map) comes up between a video and the level after it (after Video2: the plunge).
            ShaftHook.Run(new ShaftStop { Kind = ShaftStopKind.AfterVideo, Video = videoNumber, NextLevel = levelToLoad },
                () => SceneTransitionManager.Instance.LoadLevel(levelToLoad));
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
