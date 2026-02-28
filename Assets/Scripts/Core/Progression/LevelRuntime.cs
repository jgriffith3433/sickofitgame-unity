using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;
using SickOfIt.Core.Flow;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Reads a LevelConfig from GameDefinition and dynamically spawns all content.
    /// Auto-bootstraps: when a level scene loads, this finds the LevelManager and attaches itself.
    /// No manual scene modification needed.
    /// 
    /// Flow:
    ///   1. [RuntimeInitializeOnLoadMethod] registers a sceneLoaded callback
    ///   2. On level scene load, creates a LevelRuntime on the LevelManager GameObject
    ///   3. LevelRuntime reads LevelConfig and spawns enemies/audio/flow
    /// </summary>
    public class LevelRuntime : MonoBehaviour
    {
        /// <summary>
        /// Auto-bootstrap: register for scene load events at runtime.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Only bootstrap on level scenes
            if (!scene.name.StartsWith("Level") || scene.name == "LevelSelection")
                return;

            // Check if a LevelRuntime already exists
            if (Object.FindAnyObjectByType<LevelRuntime>() != null)
                return;

            // Find the LevelManager and attach LevelRuntime
            var lm = Object.FindAnyObjectByType<LevelManager>();
            if (lm != null)
            {
                var runtime = lm.gameObject.AddComponent<LevelRuntime>();
                // Level number comes from PlayerPrefs (set by SceneTransitionManager)
                Debug.Log($"[LevelRuntime] Auto-bootstrapped on '{lm.gameObject.name}' in scene '{scene.name}'");
            }
        }

        [Header("Level Identity")]
        [Tooltip("Which level to load from GameDefinition. Set by SceneTransitionManager or via code.")]
        [SerializeField] private int levelNumber = 1;

        [Header("Scene References")]
        [SerializeField] private Transform enemyContainer;
        [SerializeField] private Transform platformContainer;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Scene Transition UI")]
        [SerializeField] private List<GameObject> objectsToActivateOnTransition = new List<GameObject>();
        [SerializeField] private List<GameObject> objectsToDeactivateOnTransition = new List<GameObject>();

        [Header("Timeline")]
        [SerializeField] private PlayableDirector levelCompletionDirector;
        [SerializeField] private PlayableDirector firstSequenceDirector;
        [SerializeField] private PlayableDirector secondSequenceDirector;
        [SerializeField] private MobileControllerFramework.Button skipFirstSequenceButton;
        [SerializeField] private List<GameObject> objectsToActivateAfterFirstSequence = new List<GameObject>();
        [SerializeField] private List<GameObject> objectsToDeactivateAfterFirstSequence = new List<GameObject>();

        // Runtime state
        private LevelConfig config;
        private GameDefinition gameDefinition;
        private Dictionary<EnemyType, EnemyTemplate> templateCache;
        private bool levelCompleted;
        private List<Coroutine> spawnCoroutines = new List<Coroutine>();

        /// <summary>
        /// Set the level number before Start() runs (e.g. from SceneTransitionManager).
        /// </summary>
        public void SetLevelNumber(int number) => levelNumber = number;
        public int LevelNumber => levelNumber;
        public LevelConfig Config => config;

        private void Start()
        {
            // Read level number from PlayerPrefs (set by SceneTransitionManager.LoadLevel)
            if (PlayerPrefs.HasKey("CurrentLevelNumber"))
            {
                levelNumber = PlayerPrefs.GetInt("CurrentLevelNumber", levelNumber);
            }

            gameDefinition = GameDefinition.Instance;
            if (gameDefinition == null)
            {
                Debug.LogError("[LevelRuntime] GameDefinition not found in Resources!");
                return;
            }

            // Find level config
            config = gameDefinition.GetLevel(levelNumber);
            if (config == null)
            {
                Debug.LogError($"[LevelRuntime] No LevelConfig found for level {levelNumber}!");
                return;
            }

            // Auto-discover scene references if not set
            AutoDiscoverReferences();

            // Cache enemy templates
            templateCache = new Dictionary<EnemyType, EnemyTemplate>();
            if (gameDefinition.enemyTemplates != null)
            {
                foreach (var template in gameDefinition.enemyTemplates)
                {
                    if (template != null && !templateCache.ContainsKey(template.type))
                        templateCache[template.type] = template;
                }
            }

            // Setup
            SetupAudio();
            SetupTimelines();
            SpawnEnemies();

            // Initialize player health for this level
            playerHealth?.InitializeForLevel(levelNumber);
            AnalyticsManager.Instance?.TrackLevelStart(levelNumber);

            Debug.Log($"[LevelRuntime] Level {levelNumber} '{config.displayName}' loaded — {config.EnemyCount} enemies, duration {config.duration}s");
        }

        /// <summary>
        /// Auto-discover references from the existing scene hierarchy.
        /// This lets LevelRuntime bootstrap without manual serialized field wiring.
        /// </summary>
        private void AutoDiscoverReferences()
        {
            if (playerHealth == null)
                playerHealth = Object.FindAnyObjectByType<PlayerHealth>();

            if (musicSource == null)
            {
                // Look for an AudioSource on this or nearby objects
                musicSource = GetComponentInChildren<AudioSource>();
                if (musicSource == null)
                    musicSource = Object.FindAnyObjectByType<AudioSource>();
            }

            // Try to find timeline directors from existing LevelManager
            var lm = GetComponent<LevelManager>();
            if (lm != null)
            {
                // Use reflection to grab LevelManager's existing timeline references
                // This bridges the old system without requiring scene changes
                var lmType = typeof(LevelManager);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                if (levelCompletionDirector == null)
                {
                    var field = lmType.GetField("levelCompletionDirector", flags);
                    if (field != null)
                        levelCompletionDirector = field.GetValue(lm) as UnityEngine.Playables.PlayableDirector;
                }

                if (firstSequenceDirector == null)
                {
                    var field = lmType.GetField("firstSequenceDirector", flags);
                    if (field != null)
                        firstSequenceDirector = field.GetValue(lm) as UnityEngine.Playables.PlayableDirector;
                }

                if (secondSequenceDirector == null)
                {
                    var field = lmType.GetField("secondSequenceDirector", flags);
                    if (field != null)
                        secondSequenceDirector = field.GetValue(lm) as UnityEngine.Playables.PlayableDirector;
                }
            }
        }

        private void OnDestroy()
        {
            // Stop all spawn coroutines
            foreach (var co in spawnCoroutines)
            {
                if (co != null) StopCoroutine(co);
            }

            if (levelCompletionDirector != null)
                levelCompletionDirector.stopped -= OnLevelCompletionDirectorStopped;
            if (firstSequenceDirector != null)
                firstSequenceDirector.stopped -= OnFirstDirectorStopped;
            if (skipFirstSequenceButton != null)
                skipFirstSequenceButton.TapEndEvent.RemoveListener(SkipFirstSequence);
        }

        #region Setup

        private void SetupAudio()
        {
            if (musicSource == null || config.music == null) return;

            musicSource.clip = config.music;
            musicSource.volume = config.musicVolume;
            musicSource.time = config.musicStartTime;
            musicSource.loop = true;
            musicSource.Play();
        }

        private void SetupTimelines()
        {
            if (config.hasTwoStageSequence)
            {
                if (firstSequenceDirector != null)
                    firstSequenceDirector.stopped += OnFirstDirectorStopped;

                if (skipFirstSequenceButton != null)
                    skipFirstSequenceButton.TapEndEvent.AddListener(SkipFirstSequence);
            }

            if (levelCompletionDirector != null)
                levelCompletionDirector.stopped += OnLevelCompletionDirectorStopped;
        }

        #endregion

        #region Enemy Spawning

        private void SpawnEnemies()
        {
            if (config.enemies == null) return;

            var container = enemyContainer != null ? enemyContainer : transform;

            foreach (var spawn in config.enemies)
            {
                if (!templateCache.TryGetValue(spawn.type, out var template))
                {
                    Debug.LogWarning($"[LevelRuntime] No EnemyTemplate found for type {spawn.type}");
                    continue;
                }

                if (template.prefab == null)
                {
                    Debug.LogWarning($"[LevelRuntime] EnemyTemplate '{template.displayName}' has no prefab!");
                    continue;
                }

                if (spawn.activeAtStart && spawn.spawnTime <= 0f)
                {
                    SpawnEnemy(template, spawn, container);
                }
                else
                {
                    var co = StartCoroutine(DelayedSpawn(template, spawn, container));
                    spawnCoroutines.Add(co);
                }
            }
        }

        private IEnumerator DelayedSpawn(EnemyTemplate template, EnemySpawnConfig spawn, Transform container)
        {
            if (spawn.spawnTime > 0f)
                yield return new WaitForSeconds(spawn.spawnTime);

            SpawnEnemy(template, spawn, container);
        }

        private void SpawnEnemy(EnemyTemplate template, EnemySpawnConfig spawn, Transform container)
        {
            var enemy = Instantiate(template.prefab, new Vector3(spawn.position.x, spawn.position.y, 0f), Quaternion.identity, container);
            enemy.name = $"{template.displayName}_{config.enemies.IndexOf(spawn)}";

            // Apply template stats via EnemyBase
            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null)
            {
                enemyBase.InitializeFromConfig(template, spawn);
            }

            // Apply health override
            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                int hp = spawn.healthOverride > 0 ? spawn.healthOverride : template.defaultHealth;
                health.SetMaxHealth(hp);
            }

            if (!spawn.activeAtStart)
                enemy.SetActive(false);
        }

        #endregion

        #region Level Completion

        private void OnLevelCompletionDirectorStopped(PlayableDirector director)
        {
            CompleteLevel();
        }

        public void CompleteLevel()
        {
            if (levelCompleted) return;
            levelCompleted = true;

            GameProgressManager.Instance.SetLevelCompleted(levelNumber);
            AnalyticsManager.Instance?.TrackLevelComplete(levelNumber);
            GameProgressManager.Instance.UnlockVideo(levelNumber + 1);

            // Save roguelite health
            if (playerHealth != null)
                RogueliteManager.Instance.SaveLevelEndHealth(levelNumber, playerHealth.CurrentHealth, playerHealth.MaxHealth);

            Invoke(nameof(ProceedToNextLevel), 2f);
        }

        public void CompleteLevelAndReturnToMainMenu()
        {
            if (levelCompleted) return;
            levelCompleted = true;

            GameProgressManager.Instance.SetLevelCompleted(levelNumber);
            AnalyticsManager.Instance?.TrackLevelComplete(levelNumber);

            if (playerHealth != null)
                RogueliteManager.Instance.SaveLevelEndHealth(levelNumber, playerHealth.CurrentHealth, playerHealth.MaxHealth);

            Invoke(nameof(ReturnToMainMenu), 2f);
        }

        private void ProceedToNextLevel()
        {
            ShowSceneTransitionUI();

            if (config.nextVideoNumber > 0)
            {
                PlayerPrefs.SetInt("ReturnToLevel", levelNumber + 1);
                PlayerPrefs.Save();
                SceneTransitionManager.Instance.LoadVideo(config.nextVideoNumber);
            }
            else
            {
                SceneTransitionManager.Instance.LoadLevelSelection();
            }
        }

        #endregion

        #region Death / Navigation

        public void RestartLevel()
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            PlayerPrefs.SetString("DeathSceneToReload", currentScene.name);
            PlayerPrefs.Save();

            ShowSceneTransitionUI();

            if (!string.IsNullOrEmpty(config.deathConfigName))
                SceneTransitionManager.Instance.LoadDeathScene(config.deathConfigName);
            else
                SceneTransitionManager.Instance.LoadLevel(levelNumber);
        }

        public void ReturnToLevelSelect()
        {
            ShowSceneTransitionUI();
            SceneTransitionManager.Instance.LoadLevelSelection();
        }

        public void ReturnToMainMenu()
        {
            ShowSceneTransitionUI();
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        public void ShowSceneTransitionUI()
        {
            SetGameObjectsActive(objectsToActivateOnTransition, true);
            SetGameObjectsActive(objectsToDeactivateOnTransition, false);
        }

        #endregion

        #region Two-Stage Sequence

        private void OnFirstDirectorStopped(PlayableDirector director)
        {
            if (!config.hasTwoStageSequence) return;

            if (firstSequenceDirector != null)
                firstSequenceDirector.gameObject.SetActive(false);

            SetGameObjectsActive(objectsToDeactivateAfterFirstSequence, false);
            SetGameObjectsActive(objectsToActivateAfterFirstSequence, true);

            if (firstSequenceDirector != null)
                firstSequenceDirector.gameObject.SetActive(true);

            if (secondSequenceDirector != null)
            {
                secondSequenceDirector.time = 0;
                secondSequenceDirector.Play();
            }

            if (skipFirstSequenceButton != null)
                skipFirstSequenceButton.gameObject.SetActive(false);
        }

        public void SkipFirstSequence()
        {
            if (!config.hasTwoStageSequence || firstSequenceDirector == null) return;

            firstSequenceDirector.time = firstSequenceDirector.duration;
            firstSequenceDirector.Evaluate();
            firstSequenceDirector.Stop();
        }

        #endregion

        #region Helpers

        private static void SetGameObjectsActive(List<GameObject> objects, bool active)
        {
            if (objects == null) return;
            foreach (var obj in objects)
            {
                if (obj != null) obj.SetActive(active);
            }
        }

        #endregion
    }
}
