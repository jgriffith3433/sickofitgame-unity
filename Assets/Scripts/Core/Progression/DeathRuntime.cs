using SickOfIt.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections;
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
        private SpriteFramePlayer spritePlayer;
        private MobileControllerFramework.Button skipButton;
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
        }

        private void AutoDiscoverReferences()
        {
            // Same mobile UI as the video scene: no object is named "skip".
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
            if (skipButton != null)
            {
                skipButton.TapStartEvent.AddListener(OnRewindButtonDown);
                skipButton.TapEndEvent.AddListener(OnRewindButtonUp);
            }

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
                        sr.sortingOrder = -10;
                    }

                    spritePlayer = deathAnimationObject.GetComponent<SpriteFramePlayer>();
                    if (spritePlayer == null)
                        spritePlayer = deathAnimationObject.AddComponent<SpriteFramePlayer>();

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
            {
                Debug.LogWarning("[DeathRuntime] Audio source never became active.");
                yield break;
            }

            source.enabled = true;
            source.Play();
        }

        private void StartExperience()
        {
            if (config == null || config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            var runner = gameObject.AddComponent<ExperienceRunner>();
            runner.Run(config.timeline);
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
            else
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
            if (skipButton != null)
            {
                skipButton.TapStartEvent.RemoveListener(OnRewindButtonDown);
                skipButton.TapEndEvent.RemoveListener(OnRewindButtonUp);
            }
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
