using SickOfIt.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;
using SickOfIt.Core.Flow;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// THE level runtime. Reads LevelConfig from GameDefinition and dynamically builds
    /// the entire level: enemies, platforms, audio, timeline, completion flow.
    /// 
    /// This is the ONLY script needed on a level scene. No LevelManager, no baked-in enemies.
    /// 
    /// Auto-bootstraps via [RuntimeInitializeOnLoadMethod] — attaches itself to any level scene.
    /// Can also be placed manually on a GameObject in the scene.
    /// </summary>
    public class LevelRuntime : MonoBehaviour
    {
        #region Static Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name != "Level")
                return;

            if (Object.FindAnyObjectByType<LevelRuntime>() != null)
                return;

            // Create a fresh runtime object — no dependency on LevelManager
            var go = new GameObject("LevelRuntime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<LevelRuntime>();
            Debug.Log($"[LevelRuntime] Auto-bootstrapped in scene '{scene.name}'");
        }

        #endregion

        #region Config

        [Header("Level Identity")]
        [SerializeField] private int levelNumber = 1;

        // Runtime state
        private LevelConfig config;
        private GameDefinition gameDefinition;
        private Dictionary<EnemyType, EnemyTemplate> templateCache;
        private bool levelCompleted;
        private List<Coroutine> spawnCoroutines = new List<Coroutine>();

        // Auto-discovered scene references
        private PlayerHealth playerHealth;
        private AudioSource musicSource;
        private Transform enemyContainer;
        private Transform platformContainer;

        // Game systems
        private GameClock gameClock;
        private ExperienceRunner experienceRunner;
        private StateRecorder stateRecorder;
        private RewindController rewindController;

        // Scene transition UI
        private List<GameObject> objectsToActivateOnTransition = new List<GameObject>();
        private List<GameObject> objectsToDeactivateOnTransition = new List<GameObject>();

        #endregion

        #region Public API

        public int LevelNumber => levelNumber;
        public LevelConfig Config => config;
        public void SetLevelNumber(int number) => levelNumber = number;

        #endregion

        #region Lifecycle

        private void Start()
        {
            // Read level number from PlayerPrefs (set by SceneTransitionManager.LoadLevel)
            if (PlayerPrefs.HasKey("CurrentLevelNumber"))
                levelNumber = PlayerPrefs.GetInt("CurrentLevelNumber", levelNumber);

            gameDefinition = GameDefinition.Instance;
            if (gameDefinition == null)
            {
                Debug.LogError("[LevelRuntime] GameDefinition not found in Resources!");
                return;
            }

            config = gameDefinition.GetLevel(levelNumber);
            if (config == null)
            {
                Debug.LogError($"[LevelRuntime] No LevelConfig for level {levelNumber}!");
                return;
            }

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

            // Discover what exists in the scene
            AutoDiscoverReferences();

            // Build level content
            SetupContainers();
            SetupGameClock();
            SetupAudio();
            SetupTimelines();
            StartExperience();
            SetupRewind();
            SpawnEnemies();
            SpawnPlatforms();
            SpawnBarriers();

            // Player init
            if (playerHealth != null)
            {
                playerHealth.SetDeathSceneName(config.deathConfigName);
                playerHealth.InitializeForLevel(levelNumber);
            }
            AnalyticsManager.Instance?.TrackLevelStart(levelNumber);

            Debug.Log($"[LevelRuntime] Level {levelNumber} '{config.displayName}' — {config.EnemyCount} enemies, {config.platforms?.Count ?? 0} platforms, {config.duration}s");
        }

        private void OnDestroy()
        {
            foreach (var co in spawnCoroutines)
            {
                if (co != null) StopCoroutine(co);
            }
        }

        #endregion

        #region Auto-Discovery

        private void AutoDiscoverReferences()
        {
            playerHealth = Object.FindAnyObjectByType<PlayerHealth>();

            // Spawn the Character prefab if no player exists in the scene
            if (playerHealth == null)
            {
                Debug.Log("[LevelRuntime] No PlayerHealth found in scene, attempting to spawn Character prefab...");
                var characterPrefab = gameDefinition?.player?.playerPrefab;
                if (characterPrefab != null)
                {
                    var spawnPos = Vector3.zero;
                    var spawnPoint = GameObject.Find("PlayerSpawn");
                    if (spawnPoint != null)
                        spawnPos = spawnPoint.transform.position;

                    var player = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
                    player.name = "Character";
                    playerHealth = player.GetComponent<PlayerHealth>();
                    Debug.Log($"[LevelRuntime] Spawned Character prefab at {spawnPos} (has PlayerHealth: {playerHealth != null})");
                }
                else
                {
                    Debug.LogWarning($"[LevelRuntime] No playerPrefab assigned in GameDefinition.player! (gameDefinition null: {gameDefinition == null}, player null: {gameDefinition?.player == null})");
                }
            }
            else
            {
                Debug.Log($"[LevelRuntime] Found existing PlayerHealth on '{playerHealth.gameObject.name}'");
            }

            // Find music source — prefer one tagged or named "Music", fallback to any AudioSource
            var allAudio = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var src in allAudio)
            {
                if (src.gameObject.name.Contains("Music"))
                {
                    musicSource = src;
                    break;
                }
            }
            if (musicSource == null && allAudio.Length > 0)
                musicSource = allAudio[0];

            // No more PlayableDirector discovery — all timeline events
            // are handled by ExperienceRunner from config.
        }

        #endregion

        #region Setup

        private void SetupGameClock()
        {
            gameClock = gameObject.AddComponent<GameClock>();
        }

        private void SetupRewind()
        {
            stateRecorder = gameObject.AddComponent<StateRecorder>();
            rewindController = gameObject.AddComponent<RewindController>();
        }

        private void SetupContainers()
        {
            // Create containers for spawned content
            var enemyGo = new GameObject("Enemies");
            enemyGo.transform.SetParent(transform);
            enemyContainer = enemyGo.transform;

            var platformGo = new GameObject("Platforms");
            platformGo.transform.SetParent(transform);
            platformContainer = platformGo.transform;
        }

        private void SetupAudio()
        {
            if (config.music == null) return;

            // Create AudioSource if none exists
            if (musicSource == null)
            {
                var audioGo = new GameObject("LevelMusic");
                audioGo.transform.SetParent(transform);
                musicSource = audioGo.AddComponent<AudioSource>();
            }

            musicSource.clip = config.music;
            musicSource.volume = config.musicVolume;
            musicSource.time = config.musicStartTime;
            musicSource.loop = true;
            musicSource.Play();
        }

        private void SetupTimelines()
        {
            // All timeline events are now handled by ExperienceRunner.
            // Two-stage sequences are defined as experience events in config.
        }

        private void StartExperience()
        {
            if (config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            experienceRunner = gameObject.AddComponent<ExperienceRunner>();
            experienceRunner.Run(config.timeline, templateCache);
            Debug.Log($"[LevelRuntime] Experience started: {config.timeline.events.Count} events");
        }

        #endregion

        #region Enemy Spawning

        private void SpawnEnemies()
        {
            if (config.enemies == null) return;

            foreach (var spawn in config.enemies)
            {
                if (!templateCache.TryGetValue(spawn.type, out var template))
                {
                    Debug.LogWarning($"[LevelRuntime] No EnemyTemplate for {spawn.type}");
                    continue;
                }
                if (template.prefab == null)
                {
                    Debug.LogWarning($"[LevelRuntime] EnemyTemplate '{template.displayName}' has no prefab!");
                    continue;
                }

                if (spawn.activeAtStart && spawn.spawnTime <= 0f)
                    SpawnEnemy(template, spawn);
                else
                    spawnCoroutines.Add(StartCoroutine(DelayedSpawn(template, spawn)));
            }
        }

        private IEnumerator DelayedSpawn(EnemyTemplate template, EnemySpawnConfig spawn)
        {
            if (spawn.spawnTime > 0f)
                yield return new WaitForSeconds(spawn.spawnTime);
            SpawnEnemy(template, spawn);
        }

        private void SpawnEnemy(EnemyTemplate template, EnemySpawnConfig spawn)
        {
            var enemy = Instantiate(template.prefab, new Vector3(spawn.position.x, spawn.position.y, 0f), Quaternion.identity, enemyContainer);
            enemy.name = $"{template.displayName}_{config.enemies.IndexOf(spawn)}";

            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null)
                enemyBase.InitializeFromConfig(template, spawn);

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

        #region Platform Spawning

        private void SpawnPlatforms()
        {
            if (config.platforms == null || config.platforms.Count == 0) return;

            foreach (var platConfig in config.platforms)
            {
                SpawnPlatform(platConfig);
            }
        }

        private void SpawnPlatform(PlatformConfig platConfig)
        {
            if (platConfig.prefab != null)
            {
                var plat = Instantiate(platConfig.prefab, new Vector3(platConfig.position.x, platConfig.position.y, 0f), Quaternion.identity, platformContainer);
                plat.name = $"Platform_{config.platforms.IndexOf(platConfig)}";

                if (platConfig.scale != Vector2.zero)
                    plat.transform.localScale = new Vector3(platConfig.scale.x, platConfig.scale.y, 1f);
            }
            else
            {
                // Create a basic platform from scratch
                var go = new GameObject($"Platform_{config.platforms.IndexOf(platConfig)}");
                go.transform.SetParent(platformContainer);
                go.transform.position = new Vector3(platConfig.position.x, platConfig.position.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.4f, 0.4f, 0.4f);

                var col = go.AddComponent<BoxCollider2D>();
                float width = platConfig.scale.x > 0 ? platConfig.scale.x : platConfig.width;
                float height = 0.3f;
                col.size = new Vector2(width, height);
                go.transform.localScale = new Vector3(width, height, 1f);

                // Set platform layer if it exists
                int platLayer = LayerMask.NameToLayer("Platform");
                if (platLayer >= 0)
                    go.layer = platLayer;
            }
        }

        #endregion

        #region Barrier Spawning

        private void SpawnBarriers()
        {
            if (!config.generateBarriers) return;

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[LevelRuntime] No main camera found, cannot generate barriers");
                return;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float thickness = config.barrierThickness;

            var barrierContainer = new GameObject("Barriers");
            barrierContainer.transform.SetParent(transform);

            int boundaryLayer = LayerMask.NameToLayer("Boundary");

            // Left barrier
            CreateBarrier(barrierContainer.transform, "Barrier_Left",
                new Vector3(-halfWidth - thickness * 0.5f, 0f, 0f),
                new Vector2(thickness, halfHeight * 2f + thickness * 2f),
                boundaryLayer);

            // Right barrier
            CreateBarrier(barrierContainer.transform, "Barrier_Right",
                new Vector3(halfWidth + thickness * 0.5f, 0f, 0f),
                new Vector2(thickness, halfHeight * 2f + thickness * 2f),
                boundaryLayer);

            // Top barrier
            if (config.barrierOnTop)
            {
                CreateBarrier(barrierContainer.transform, "Barrier_Top",
                    new Vector3(0f, halfHeight + thickness * 0.5f, 0f),
                    new Vector2(halfWidth * 2f + thickness * 2f, thickness),
                    boundaryLayer);
            }

            // Bottom barrier
            if (config.barrierOnBottom)
            {
                CreateBarrier(barrierContainer.transform, "Barrier_Bottom",
                    new Vector3(0f, -halfHeight - thickness * 0.5f, 0f),
                    new Vector2(halfWidth * 2f + thickness * 2f, thickness),
                    boundaryLayer);
            }

            Debug.Log($"[LevelRuntime] Spawned barriers (viewport: {halfWidth * 2:F1}x{halfHeight * 2:F1})");
        }

        private static void CreateBarrier(Transform parent, string name, Vector3 position, Vector2 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            if (layer >= 0) go.layer = layer;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        #endregion

        #region Level Completion

        public void CompleteLevel()
        {
            if (levelCompleted) return;
            levelCompleted = true;

            GameProgressManager.Instance.SetLevelCompleted(levelNumber);
            AnalyticsManager.Instance?.TrackLevelComplete(levelNumber);
            GameProgressManager.Instance.UnlockVideo(levelNumber + 1);

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

        // Two-stage sequences are now handled by ExperienceRunner events.
        // Use GameEventType.ActivateObject/DeactivateObject events in the timeline config.

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
