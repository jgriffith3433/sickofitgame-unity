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
    /// Auto-bootstraps on any scene starting with "Death".
    /// </summary>
    public class DeathRuntime : MonoBehaviour
    {
        #region Static Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name != "Death")
                return;

            if (Object.FindAnyObjectByType<DeathRuntime>() != null)
                return;

            var go = new GameObject("DeathRuntime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<DeathRuntime>();
            Debug.Log($"[DeathRuntime] Auto-bootstrapped in scene '{scene.name}'");
        }

        #endregion

        [Header("Settings")]
        [SerializeField] private float delayBeforeReload = 1f;

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
            else
            {
                var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (sceneName.StartsWith("Death"))
                    deathName = sceneName;
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
                skipButton.TapEndEvent.AddListener(SkipDeathAnimation);

            // Setup SpriteFramePlayer if we have sprite frames data
            if (config?.spriteFramesData != null)
            {
                var framesData = config.spriteFramesData as SpriteFramesData;
                if (framesData != null)
                {
                    var deathObj = GameObject.Find("DeathAnimation");
                    if (deathObj == null)
                    {
                        deathObj = new GameObject("DeathAnimation");
                        deathObj.transform.SetParent(transform);
                        var sr = deathObj.AddComponent<SpriteRenderer>();
                        sr.sortingOrder = -10;
                    }

                    spritePlayer = deathObj.GetComponent<SpriteFramePlayer>();
                    if (spritePlayer == null)
                        spritePlayer = deathObj.AddComponent<SpriteFramePlayer>();

                    spritePlayer.Initialize(framesData);
                }
            }

            // Setup audio
            if (config?.audio != null)
            {
                var audioGo = new GameObject("DeathAudio");
                audioGo.transform.SetParent(transform);
                var audioSource = audioGo.AddComponent<AudioSource>();
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
            if (PlayerPrefs.HasKey("DeathSceneToReload"))
            {
                string sceneName = PlayerPrefs.GetString("DeathSceneToReload");
                PlayerPrefs.DeleteKey("DeathSceneToReload");
                PlayerPrefs.Save();

                if (sceneName.StartsWith("Level"))
                {
                    string numberStr = sceneName.Replace("Level", "");
                    if (int.TryParse(numberStr, out int levelNum))
                    {
                        SceneTransitionManager.Instance.LoadLevel(levelNum);
                        return;
                    }
                }

                SceneTransitionManager.Instance.LoadMainMenu();
            }
            else
            {
                SceneTransitionManager.Instance.LoadMainMenu();
            }
        }
    }
}
