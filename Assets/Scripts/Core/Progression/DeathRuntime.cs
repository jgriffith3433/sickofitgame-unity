using SickOfIt.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core.Flow;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Config-driven death scene runtime. Reads DeathConfig from GameDefinition
    /// and drives death animation via SpriteFramePlayer (no PlayableDirector).
    /// 
    /// Added directly to generated scenes by ProjectGenerator / DeathSceneGenerator.
    /// </summary>
    public class DeathRuntime : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float delayBeforeReload = 1f;

        [Header("Scene References (wired by generator)")]
        [SerializeField] private GameObject deathAnimationObject;
        [SerializeField] private GameObject uiRoot;
        [SerializeField] private AudioSource audioSource;

        private GameClock gameClock;
        private ExperienceRunner experienceRunner;
        private SpriteFramePlayer spritePlayer;
        private readonly List<SpriteFramePlayer> playbackPlayers = new List<SpriteFramePlayer>();
        private MobileControllerFramework.Button skipButton;
        private MobileControllerFramework.Button rewindButton;
        private bool rewindAlsoSkips;
        private bool rewindButtonHeld;
        private float rewindPressedAt;
        private DeathConfig config;
        private bool deathCompleted;

        private void Start()
        {
            string deathName = "Death1";
            if (PlayerPrefs.HasKey("CurrentDeathConfig"))
            {
                deathName = PlayerPrefs.GetString("CurrentDeathConfig", deathName);
                PlayerPrefs.DeleteKey("CurrentDeathConfig");
                PlayerPrefs.Save();
            }

            var gd = GameDefinition.Instance;
            if (gd != null)
            {
                config = gd.GetDeath(deathName);
                if (config != null)
                    delayBeforeReload = config.delayBeforeReload > 0 ? config.delayBeforeReload : delayBeforeReload;
            }

            gameClock = gameObject.AddComponent<GameClock>();
            gameObject.AddComponent<SickOfIt.Core.Rewind.RewindTimeline>();

            EnsurePlaybackCamera();
            EnsureEventSystem();
            AutoDiscoverReferences();
            SetupPlayback();
            StartExperience();
            StartCoroutine(PlaybackStartGate.Hold(gameClock, playbackPlayers, audioSource, BeginDeathPlayback));
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

            rewindAlsoSkips = skipButton == null;
            if (rewindAlsoSkips)
                skipButton = rewindButton;
        }

        private void EnsurePlaybackCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("DeathCamera");
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
            if (config?.spriteFramesData != null)
            {
                var framesData = config.spriteFramesData as SpriteFramesData;
                if (framesData != null)
                {
                    // Use serialized reference or create fallback
                    if (deathAnimationObject == null)
                    {
                        deathAnimationObject = new GameObject("DeathAnimation");
                        deathAnimationObject.transform.SetParent(transform);
                        var sr = deathAnimationObject.AddComponent<SpriteRenderer>();
                        SpriteLayers.Apply(sr, SpriteLayer.Background1);
                    }

                    spritePlayer = deathAnimationObject.GetComponent<SpriteFramePlayer>();
                    if (spritePlayer == null)
                        spritePlayer = deathAnimationObject.AddComponent<SpriteFramePlayer>();
                    playbackPlayers.Add(spritePlayer);

                    spritePlayer.Initialize(framesData, () =>
                    {
                        spritePlayer.UseGameClock(true);
                        spritePlayer.Play();
                    });
                    spritePlayer.UseGameClock(true);
                }
            }

            // Content is hidden until the scene activates, so Play() right here is ignored.
            if (config?.audio != null)
            {
                if (audioSource == null)
                {
                    var audioGo = new GameObject("DeathAudio");
                    audioGo.transform.SetParent(transform);
                    audioSource = audioGo.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
                audioSource.dopplerLevel = 0f;
                audioSource.loop = false;
                audioSource.clip = config.audio;
                audioSource.volume = 1f;
                if (audioSource.GetComponent<RewindableAudioSource>() == null)
                    audioSource.gameObject.AddComponent<RewindableAudioSource>();
            }
        }

        private void BeginDeathPlayback()
        {
            if (experienceRunner != null)
                experienceRunner.SetPlaybackHeld(false);

            if (audioSource == null || audioSource.clip == null)
                return;
            if (!audioSource.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[DeathRuntime] Audio source never became active.");
                return;
            }

            audioSource.enabled = true;
            audioSource.Play();
        }

        private void StartExperience()
        {
            if (config == null || config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            experienceRunner = gameObject.AddComponent<ExperienceRunner>();
            experienceRunner.Run(config.timeline);
        }

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
                SkipDeathAnimation();
        }

        private void OnSkipButton()
        {
            if (gameClock != null && gameClock.IsRewinding)
                gameClock.StopRewind();
            SkipDeathAnimation();
        }

        private void Update()
        {
            if (deathCompleted) return;

            if (rewindButtonHeld && gameClock != null && !gameClock.IsRewinding &&
                Time.unscaledTime - rewindPressedAt >= 0.22f && gameClock.Time > 0.05f)
                gameClock.StartRewind(3f);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SkipDeathAnimation();
                return;
            }

            if (gameClock != null && gameClock.IsRewinding) return;

            // Check if sprite playback is done
            if (spritePlayer != null && spritePlayer.IsReady && spritePlayer.Progress >= 0.99f)
            {
                OnDeathAnimationComplete();
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

        public void SkipDeathAnimation()
        {
            if (deathCompleted) return;
            if (spritePlayer != null)
                spritePlayer.Release();
            OnDeathAnimationComplete();
        }

        private void OnDeathAnimationComplete()
        {
            if (deathCompleted) return;
            deathCompleted = true;
            StartCoroutine(ReloadLevelAfterDelay());
        }

        private IEnumerator ReloadLevelAfterDelay()
        {
            yield return new WaitForSeconds(delayBeforeReload);
            ReloadLevel();
        }

        private void ReloadLevel()
        {
            // Read the level number directly — no scene name parsing needed
            int levelNumber = PlayerPrefs.GetInt("CurrentLevelNumber", 1);
            SceneTransitionManager.Instance.LoadLevel(levelNumber);
        }
    }
}
