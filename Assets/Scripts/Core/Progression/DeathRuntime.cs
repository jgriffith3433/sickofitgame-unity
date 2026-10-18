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
            BuildRageQuitButton();
        }

        /// <summary>Main menu flag: the next main menu offers "Rage Quit Again".</summary>
        public const string RageQuitKey = "RageQuitFromDeath";

        /// <summary>A corner button (out of the way of the death animation) that leaves for the main menu.</summary>
        private void BuildRageQuitButton()
        {
            var gd = GameDefinition.Instance;
            var theme = gd != null ? gd.menuTheme : new SickOfIt.Data.Config.MenuTheme();
            var layout = new SickOfIt.Data.Config.MenuLayoutConfig();
            layout.title.show = false;
            layout.subtitle.show = false;
            var menu = SickOfIt.Core.UI.Menus.MenuCanvas.Create("DeathUI", theme, layout, gameObject);
            var style = menu.Theme.button.Clone();
            style.size = new Vector2(340f, 92f);
            string label = menu.Theme.texts != null ? menu.Theme.texts.rageQuit : "Rage Quit";
            // Bottom-right in landscape; top centre on a phone held upright, clear of the rewind/skip row.
            var portrait = menu.Responsive.screens != null ? menu.Responsive.screens.rageQuit : null;
            menu.AddButtonAt(new Vector2(1f, 0f), new Vector2(-210f, 96f), label, style, OnRageQuit, null, portrait);
        }

        private void OnRageQuit()
        {
            if (rageQuitting)
                return;
            rageQuitting = true;
            deathCompleted = true; // no reload into the level afterwards
            StopAllCoroutines();
            if (gameClock != null && gameClock.IsRewinding)
                gameClock.StopRewind();
            PlayerPrefs.SetInt(RageQuitKey, 1);
            PlayerPrefs.Save();
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        private bool rageQuitting;

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
            TransportButtonLayout.Apply(gameObject, rewindButton, skipButton);
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
            if (config == null || config.timeline == null || !config.timeline.HasContent)
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
                Time.unscaledTime - rewindPressedAt >= RewindHoldSeconds() && gameClock.Time > 0.05f)
                gameClock.StartRewind(RewindSpeed());

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
