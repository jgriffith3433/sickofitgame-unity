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
    /// Over it: "YOU OBVIOUSLY DIED" and Hell's announcer gloating (DeathConfig title and gloats). A death on a floor
    /// stage ends the run (<see cref="RunFlow.EndRun"/>, as soon as the scene starts, so a Rage Quit can't keep it):
    /// when the animation is over the Shaft drops the hero back to the Waystone and the next run starts there.
    /// Anywhere else (the Meadow, the arcade levels) the level is played again.
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
        private int levelNumber;
        private bool runEnded;
        private ShaftStop runStop;
        private DeathLine gloat;

        /// <summary>The death scene on screen now, or null.</summary>
        public static DeathRuntime Current { get; private set; }
        /// <summary>The title over the death animation ("YOU OBVIOUSLY DIED"), as shown.</summary>
        public string TitleText => titleText != null ? titleText.text : "";
        /// <summary>The announcer's gloat on this death (its subtitle and audio.json voice), or null.</summary>
        public DeathLine Gloat => gloat;
        /// <summary>This death ended the run: the Shaft's drop to the Waystone comes next instead of the level again.</summary>
        public bool RunEnded => runEnded;
        public ShaftStop RunStop => runStop;

        private TMPro.TextMeshProUGUI titleText;
        private TMPro.TextMeshProUGUI gloatText;

        /// <summary>Which gloat comes next (they are said in turn, one per death).</summary>
        private const string GloatTurnKey = "Death_GloatTurn";

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

            Current = this;
            // Hell ends the run here: the death stands once its scene starts.
            levelNumber = PlayerPrefs.GetInt("CurrentLevelNumber", 1);
            runEnded = RunFlow.EndRun(levelNumber, out runStop);
            AskWhyAfterDeaths();

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

        /// <summary>Deaths on one level (this session) before the death screen asks "Too hard? Not fun?".</summary>
        public const int AskAfterDeaths = 3;
        /// <summary>The longest the death screen waits for an open feedback card before going on (real seconds).</summary>
        const float AskHoldSeconds = 15f;

        /// <summary>This death screen showed the feedback card (the third death or more on the level, once per level and session).</summary>
        public bool AskedFeedback { get; private set; }
        public int DeathsOnLevel { get; private set; }
        private bool skipHold;

        /// <summary>Analytics: the death screen, and on the third death in a level the thumbs card (Hawkes Analytics' ask).</summary>
        private void AskWhyAfterDeaths()
        {
            AnalyticsManager.Screen("death");
            DeathsOnLevel = AnalyticsManager.DeathsIn(levelNumber);
            if (DeathsOnLevel < AskAfterDeaths)
                return;
            AskedFeedback = true;
            int floor = runStop.FromFloor;
            string where = floor > 0 ? $"level {levelNumber}, floor {floor}" : $"level {levelNumber}";
            AnalyticsManager.Ask("deaths-level-" + levelNumber, "Too hard? Not fun?", $"death {DeathsOnLevel} on {where}", stars: false, comment: true, placeholder: "What got you? (optional)");
            Debug.Log($"[Death] Death {DeathsOnLevel} on level {levelNumber}: the feedback card asks \"Too hard? Not fun?\"");
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
            BuildWords(menu);
        }

        /// <summary>"YOU OBVIOUSLY DIED" at the top, and under it the announcer's gloat once the animation starts.</summary>
        private void BuildWords(SickOfIt.Core.UI.Menus.MenuCanvas menu)
        {
            var theme = menu.Theme;
            string title = config != null && !string.IsNullOrEmpty(config.title) ? config.title : "You Obviously Died";
            titleText = AddLine(menu, "Title", title, theme.title, 130f,
                MenuPlacement.At(0.5f, 0.87f, 0f, 0f, 1500f, 170f), MenuPlacement.At(0.5f, 1f, 0f, -270f, 1000f, 150f), menu.TitleFont,
                new SickOfIt.Data.Config.MenuMotion { type = SickOfIt.Data.Config.MenuMotionType.Jitter, amount = 5f, speed = 1f }, -3f);
            gloatText = AddLine(menu, "Gloat", "", theme.subtitle, 46f,
                MenuPlacement.At(0.5f, 0.75f, 0f, 0f, 1500f, 80f), MenuPlacement.At(0.5f, 1f, 0f, -390f, 1000f, 90f), null, null, 0f);
            gloatText.transform.parent.gameObject.SetActive(false);

            if (config != null && config.gloats != null && config.gloats.Count > 0)
            {
                int turn = PlayerPrefs.GetInt(GloatTurnKey, 0);
                gloat = config.gloats[Mathf.Abs(turn) % config.gloats.Count];
                PlayerPrefs.SetInt(GloatTurnKey, turn + 1);
                PlayerPrefs.Save();
            }
            Debug.Log($"[Death] \"{title}\"" + (gloat != null ? $", the announcer: \"{gloat.text}\" ({gloat.sound})" : "")
                      + (runEnded ? $"; the run is over: back to the Waystone on FLOOR {runStop.Waystone}, then level {runStop.NextLevel}" : $"; level {levelNumber} again"));
        }

        private static TMPro.TextMeshProUGUI AddLine(SickOfIt.Core.UI.Menus.MenuCanvas menu, string name, string value, SickOfIt.Data.Config.MenuTextStyle style,
            float size, MenuPlacement landscape, MenuPlacement portrait, TMPro.TMP_FontAsset font, SickOfIt.Data.Config.MenuMotion idle, float rotation)
        {
            var text = menu.AddText(menu.Root, name, value, style, landscape.anchor, landscape.offset, landscape.size, out _, size, font, null, idle, rotation);
            text.fontStyle |= TMPro.FontStyles.UpperCase;
            text.enableAutoSizing = true;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            SickOfIt.Core.UI.Menus.MenuResponsive.Track((RectTransform)text.transform.parent, landscape, portrait,
                SickOfIt.Core.UI.Menus.MenuElementKind.Text, menu.Responsive);
            return text;
        }

        /// <summary>The announcer gloats as the animation starts (over its soundtrack; Sfx plays outside the scene's clock).</summary>
        private void PlayGloat()
        {
            if (gloat == null || gloatText == null)
                return;
            gloatText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(gloat.text));
            SickOfIt.Core.UI.Menus.MenuGraphics.SetText(gloatText, gloat.text ?? "");
            if (!string.IsNullOrEmpty(gloat.sound))
                SickOfIt.Core.Audio.Sfx.Play(gloat.sound);
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
            AnalyticsManager.Track("rage_quit", new Dictionary<string, object> { { "level", levelNumber }, { "deaths", DeathsOnLevel } });
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
            PlayGloat();

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
            if (deathCompleted)
            {
                // Waiting on the feedback card: Escape goes on without it.
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    skipHold = true;
                return;
            }

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
            if (Current == this)
                Current = null;
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
            if (deathCompleted)
            {
                skipHold = true;
                return;
            }
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
            // The feedback card is the player's for a moment (never carried into play: it keeps the keyboard).
            float until = Time.unscaledTime + AskHoldSeconds;
            while (AskedFeedback && !skipHold && Time.unscaledTime < until && AnalyticsManager.AskOpen)
                yield return null;
            AnalyticsManager.CloseAsk();
            ReloadLevel();
        }

        private void ReloadLevel()
        {
            if (!runEnded)
            {
                SceneTransitionManager.Instance.LoadLevel(levelNumber);
                return;
            }
            // The Shaft: the hero's marker drops back to the Waystone; its Continue starts the next run there.
            int restart = runStop.NextLevel;
            ShaftHook.Run(runStop, () => SceneTransitionManager.Instance.LoadLevel(restart));
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
