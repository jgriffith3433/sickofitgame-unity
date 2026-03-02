using SickOfIt.Core;
using SickOfIt.Core.Rewind;
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
    /// Added directly to generated scenes by ProjectGenerator / LevelSceneGenerator.
    /// </summary>
    public class LevelRuntime : MonoBehaviour
    {
        #region Config

        [Header("Level Identity")]
        [SerializeField] private int levelNumber = 1;

        [Header("Scene References (wired by generator)")]
        [SerializeField] private GameObject characterObject;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private HealthUI healthUI;

        [Header("Win Zone (bounds check — no separate script needed)")]
        [SerializeField] private Vector2 winZoneCenter = new Vector2(6.5f, 0f);
        [SerializeField] private Vector2 winZoneHalfSize = new Vector2(0.5f, 4f);

        [Header("Projectile Pools (wired by generator)")]
        [SerializeField] private ObjectPool playerProjectilePool;
        [SerializeField] private ObjectPool enemyProjectilePool;
        [SerializeField] private ObjectPool laserPool;

        [Header("Pickup Pool (wired by generator)")]
        [SerializeField] private ObjectPool pickupPool;

        // Runtime state
        private LevelConfig config;
        private GameDefinition gameDefinition;
        private Dictionary<EnemyType, EnemyTemplate> templateCache;
        private bool levelCompleted;
        private List<Coroutine> spawnCoroutines = new List<Coroutine>();

        // Resolved scene references
        private PlayerHealth playerHealth;
        private Transform enemyContainer;
        private Transform platformContainer;

        #endregion

        #region Public API

        public int LevelNumber => levelNumber;
        public LevelConfig Config => config;
        public void SetLevelNumber(int number) => levelNumber = number;

        // Game systems
        private GameClock gameClock;
        private ExperienceRunner experienceRunner;
        private RewindTimeline rewindTimeline;
        private RewindController rewindController;

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

            // Apply per-level win zone from config
            winZoneCenter = config.winZoneCenter;
            winZoneHalfSize = config.winZoneHalfSize;

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

            // Resolve scene references (from serialized fields or fallback spawn)
            ResolveReferences();

            // Build level content
            SetupContainers();
            SetupGameClock();
            SetupAudio();
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

        private void Update()
        {
            CheckWinZone();
        }

        #endregion

        #region Scene References

        private void ResolveReferences()
        {
            // Use serialized character reference, or spawn from prefab as fallback
            if (characterObject != null)
            {
                playerHealth = characterObject.GetComponentInChildren<PlayerHealth>();
                Debug.Log($"[LevelRuntime] Using serialized Character ref (has PlayerHealth: {playerHealth != null})");
            }
            else
            {
                Debug.Log("[LevelRuntime] No characterObject assigned, spawning Character prefab...");
                var characterPrefab = gameDefinition?.player?.playerPrefab;
                if (characterPrefab != null)
                {
                    var spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
                    characterObject = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
                    characterObject.name = "Character";
                    playerHealth = characterObject.GetComponentInChildren<PlayerHealth>();
                    Debug.Log($"[LevelRuntime] Spawned Character prefab at {spawnPos} (has PlayerHealth: {playerHealth != null})");
                }
                else
                {
                    Debug.LogWarning("[LevelRuntime] No characterObject assigned and no playerPrefab in GameDefinition!");
                }
            }

            // Resolve PlayerHealth from character
            if (characterObject != null && playerHealth == null)
                playerHealth = characterObject.GetComponentInChildren<PlayerHealth>();

            // Wire player projectile pool
            if (characterObject != null && playerProjectilePool != null)
            {
                var pc = characterObject.GetComponentInChildren<SickOfIt.Core.Input.PlayerController>();
                if (pc != null)
                    pc.SetProjectilePool(playerProjectilePool);
            }

            // Wire HealthUI → PlayerHealth
            if (healthUI != null && playerHealth != null)
                healthUI.SetPlayerHealth(playerHealth);
        }

        #endregion

        #region Setup

        private void SetupGameClock()
        {
            // Prefer pre-attached component from generator, fallback to AddComponent
            gameClock = GetComponent<GameClock>();
            if (gameClock == null)
                gameClock = gameObject.AddComponent<GameClock>();
        }

        private void SetupRewind()
        {
            // Prefer components already on the GO (wired by generator), fallback to AddComponent
            rewindTimeline = GetComponent<RewindTimeline>();
            if (rewindTimeline == null)
                rewindTimeline = gameObject.AddComponent<RewindTimeline>();

            rewindController = GetComponent<RewindController>();
            if (rewindController == null)
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

        private void StartExperience()
        {
            if (config.timeline == null || config.timeline.events == null || config.timeline.events.Count == 0)
                return;

            experienceRunner = gameObject.AddComponent<ExperienceRunner>();

            // Inject references so ExperienceRunner never uses Find*
            var playerCtrl = characterObject != null
                ? characterObject.GetComponentInChildren<SickOfIt.Core.Input.PlayerController>()
                : null;
            experienceRunner.SetPlayerRefs(playerHealth, playerCtrl);
            if (musicSource != null)
                experienceRunner.SetMusicSource(musicSource);

            // Wire projectile pools so timeline-spawned enemies can shoot
            experienceRunner.SetPools(enemyProjectilePool, laserPool, pickupPool);

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

            // Inject player reference so enemies never need FindGameObjectWithTag
            if (enemyBase != null && characterObject != null)
            {
                var playerDamageable = characterObject.GetComponentInChildren<IDamageable>(true);
                enemyBase.SetPlayer(characterObject.transform, playerDamageable);
            }

            // Wire projectile pools to shooting enemies
            if (enemyBase is EnemyShooter shooter && enemyProjectilePool != null)
                shooter.SetProjectilePool(enemyProjectilePool);
            else if (enemyBase is EnemySentinel sentinel && laserPool != null)
                sentinel.SetLaserPool(laserPool);

            // Wire pickup pool for point drops
            if (enemyBase != null && pickupPool != null)
                enemyBase.SetPickupPool(pickupPool);

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
            // Use the generated platform prefab from GameDefinition
            var platformPrefab = GameDefinition.Instance.platformPrefab;
            
            if (platformPrefab != null)
            {
                var plat = Instantiate(platformPrefab, new Vector3(platConfig.position.x, platConfig.position.y, 0f), Quaternion.identity, platformContainer);
                plat.name = $"Platform_{config.platforms.IndexOf(platConfig)}";

                float width = platConfig.scale.x > 0 ? platConfig.scale.x : platConfig.width;
                float height = platConfig.scale.y > 0 ? platConfig.scale.y : 0.3f;
                plat.transform.localScale = new Vector3(width, height, 1f);

                // Wire DropThroughPlatform to PlayerController
                var dropThrough = plat.GetComponent<SickOfIt.Core.Input.DropThroughPlatform>();
                if (dropThrough != null)
                {
                    var playerController = characterObject?.GetComponent<SickOfIt.Core.Input.PlayerController>();
                    if (playerController != null)
                        dropThrough.SetPlayerController(playerController);
                }
            }
            else
            {
                // Fallback: create a basic platform from scratch
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

        private void CheckWinZone()
        {
            if (levelCompleted || characterObject == null) return;

            Vector2 playerPos = characterObject.transform.position;
            if (Mathf.Abs(playerPos.x - winZoneCenter.x) <= winZoneHalfSize.x &&
                Mathf.Abs(playerPos.y - winZoneCenter.y) <= winZoneHalfSize.y)
            {
                CompleteLevel();
            }
        }

        public void CompleteLevel()
        {
            if (levelCompleted) return;
            levelCompleted = true;

            GameProgressManager.Instance.SetLevelCompleted(levelNumber);
            AnalyticsManager.Instance?.TrackLevelComplete(levelNumber);

            if (playerHealth != null)
                RogueliteManager.Instance.SaveLevelEndHealth(levelNumber, playerHealth.CurrentHealth, playerHealth.MaxHealth);

            if (config.returnToMainMenuOnComplete)
            {
                Invoke(nameof(ReturnToMainMenu), config.completionDelay);
            }
            else
            {
                GameProgressManager.Instance.UnlockVideo(levelNumber + 1);
                Invoke(nameof(ProceedToNextLevel), config.completionDelay);
            }
        }

        private void ProceedToNextLevel()
        {
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

            if (!string.IsNullOrEmpty(config.deathConfigName))
                SceneTransitionManager.Instance.LoadDeathScene(config.deathConfigName);
            else
                SceneTransitionManager.Instance.LoadLevel(levelNumber);
        }

        public void ReturnToLevelSelect()
        {
            SceneTransitionManager.Instance.LoadLevelSelection();
        }

        public void ReturnToMainMenu()
        {
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        #endregion

        // Two-stage sequences are now handled by ExperienceRunner events.
        // Use GameEventType.ActivateObject/DeactivateObject events in the timeline config.
    }
}
