using SickOfIt.Core;
using UnityEngine;
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

        private SpriteFramePlayer spritePlayer;
        private MobileControllerFramework.Button skipButton;
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

            AutoDiscoverReferences();
            SetupPlayback();
            StartExperience();

            Debug.Log($"[DeathRuntime] Death '{deathName}' loaded");
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
                skipButton.TapEndEvent.AddListener(SkipDeathAnimation);

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

                    spritePlayer.Initialize(framesData);
                }
            }

            // Setup audio using scene-generated AudioSource
            if (config?.audio != null)
            {
                if (audioSource == null)
                {
                    var audioGo = new GameObject("DeathAudio");
                    audioGo.transform.SetParent(transform);
                    audioSource = audioGo.AddComponent<AudioSource>();
                }
                audioSource.clip = config.audio;
                audioSource.Play();
            }
        }

        private void StartExperience()
        {
            if (config == null || config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            var runner = gameObject.AddComponent<ExperienceRunner>();
            runner.Run(config.timeline);
        }

        private void Update()
        {
            if (deathCompleted) return;

            // Check if sprite playback is done
            if (spritePlayer != null && spritePlayer.IsReady && spritePlayer.Progress >= 0.99f)
            {
                OnDeathAnimationComplete();
            }
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.TapEndEvent.RemoveListener(SkipDeathAnimation);
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
