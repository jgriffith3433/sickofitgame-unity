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
using SickOfIt.VideoFrameExtractor.Runtime;

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
        [SerializeField] private Transform backgroundsContainer;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private HealthUI healthUI;

        [Header("Projectile Pools (wired by generator)")]
        [SerializeField] private ObjectPool playerProjectilePool;
        [SerializeField] private ObjectPool enemyProjectilePool;
        [SerializeField] private ObjectPool laserPool;

        [Header("Pickup Pool (wired by generator)")]
        [SerializeField] private ObjectPool pickupPool;

        [Header("Win Zone")]
        [SerializeField] private Vector2 winZoneCenter = new Vector2(6.5f, 0f);
        [SerializeField] private Vector2 winZoneHalfSize = new Vector2(0.5f, 4f);

        // Runtime state
        private LevelConfig config;
        private GameDefinition gameDefinition;
        private Dictionary<EnemyType, EnemyTypeConfig> templateCache;
        private bool levelCompleted;
        private bool[] enemyHasSpawned;
        private int totalEnemiesToSpawn;
        private int enemiesSpawned;
        private int enemiesKilled;

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
        private readonly List<SpriteFramePlayer> backgroundPlayers = new List<SpriteFramePlayer>();
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

            // Apply per-level config
            winZoneCenter = config.winZoneCenter;
            winZoneHalfSize = config.winZoneHalfSize;

            // Count total enemies for the kill-all win condition
            totalEnemiesToSpawn = config.enemies != null ? config.enemies.Count : 0;
            enemiesSpawned = 0;
            enemiesKilled = 0;

            // Cache enemy types
            templateCache = new Dictionary<EnemyType, EnemyTypeConfig>();
            if (gameDefinition.enemyTypes != null)
            {
                foreach (var enemyConfig in gameDefinition.enemyTypes)
                {
                    if (enemyConfig != null && !templateCache.ContainsKey(enemyConfig.type))
                        templateCache[enemyConfig.type] = enemyConfig;
                }
            }

            // Resolve scene references (from serialized fields or fallback spawn)
            ResolveReferences();

            // Build level content
            SetupContainers();
            SetupGameClock();
            SetupAudio();
            SetupBackgrounds();
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
            StartCoroutine(PlaybackStartGate.Hold(gameClock, backgroundPlayers, musicSource, StartLevelMusic));

            //Debug.Log($"[LevelRuntime] Level {levelNumber} '{config.displayName}' — {config.EnemyCount} enemies, {config.platforms?.Count ?? 0} platforms, {config.duration}s");
        }

        private void Update()
        {
            TrySpawnDueEnemies();
            CheckWinZone();

            // A kill rewound back to dead is counted during the scrub, and the
            // clear itself waits until time is running forward again.
            if (!levelCompleted &&
                (GameClock.Instance == null || !GameClock.Instance.IsRewinding) &&
                KillsSatisfied())
                CompleteLevel();
        }

        #endregion

        #region Scene References

        private void ResolveReferences()
        {
            // Use serialized character reference, or spawn from prefab as fallback
            if (characterObject != null)
            {
                playerHealth = characterObject.GetComponentInChildren<PlayerHealth>();
                //Debug.Log($"[LevelRuntime] Using serialized Character ref (has PlayerHealth: {playerHealth != null})");
            }
            else
            {
                //Debug.Log("[LevelRuntime] No characterObject assigned, spawning Character prefab...");
                var characterPrefab = gameDefinition?.player?.playerPrefab;
                if (characterPrefab != null)
                {
                    var spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
                    characterObject = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
                    characterObject.name = "Character";
                    playerHealth = characterObject.GetComponentInChildren<PlayerHealth>();
                    //Debug.Log($"[LevelRuntime] Spawned Character prefab at {spawnPos} (has PlayerHealth: {playerHealth != null})");
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

            // SceneContentController hides Content in Awake and shows it later.
            // Play() on a source whose parent is inactive is ignored, so wait.
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.dopplerLevel = 0f;
            musicSource.clip = config.music;
            musicSource.volume = config.musicVolume;
            musicSource.loop = true;
            if (musicSource.GetComponent<RewindableAudioSource>() == null)
                musicSource.gameObject.AddComponent<RewindableAudioSource>();
            if (musicSource.clip != null && musicSource.clip.loadState == AudioDataLoadState.Unloaded)
                musicSource.clip.LoadAudioData();
        }

        private void StartLevelMusic()
        {
            if (experienceRunner != null)
                experienceRunner.SetPlaybackHeld(false);

            if (musicSource == null || musicSource.clip == null)
                return;
            if (!musicSource.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[LevelRuntime] Music source never became active, so playback was skipped.");
                return;
            }

            EnsureAudioListener();
            musicSource.enabled = true;
            musicSource.Play();
            if (config != null && config.musicStartTime > 0f && musicSource.isPlaying)
                musicSource.time = config.musicStartTime;
        }

        private static void EnsureAudioListener()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
                    return;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var listener = cam.GetComponent<AudioListener>();
                if (listener == null)
                    cam.gameObject.AddComponent<AudioListener>();
                else
                    listener.enabled = true;
                return;
            }

            var go = new GameObject("AudioListener");
            go.AddComponent<AudioListener>();
        }

        private void SetupBackgrounds()
        {
            var layers = ResolveBackgroundLayers();
            if (layers.Count == 0)
                return;

            // Create container if generator didn't wire one
            Transform container = backgroundsContainer;
            if (container == null)
            {
                var go = new GameObject("Backgrounds");
                go.transform.SetParent(transform);
                container = go.transform;
            }

            var cam = Camera.main;

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!layer.HasContent) continue;

                var layerGo = new GameObject(string.IsNullOrEmpty(layer.name) ? $"BG_{i}" : layer.name);
                layerGo.transform.SetParent(container);

                var sr = layerGo.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(sr, layer.layer, layer.sortingOrder);

                // Position offset (centered on camera if available)
                Vector3 basePos = cam != null
                    ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f)
                    : Vector3.zero;
                layerGo.transform.position = basePos + new Vector3(
                    layer.offset.x, layer.offset.y, SpriteLayers.Depth(layer.layer));

                if (layer.IsAnimated)
                {
                    // Animated background using SpriteFramePlayer
                    var framesData = layer.spriteFramesData as SpriteFramesData;
                    if (framesData != null)
                    {
                        // Scale to fill camera view
                        if (cam != null && cam.orthographic)
                        {
                            float camHeight = cam.orthographicSize * 2f;
                            float spriteHeight = framesData.frameHeight / framesData.spritePixelsPerUnit;
                            float scale = camHeight / spriteHeight;
                            layerGo.transform.localScale = new Vector3(
                                scale * layer.scale.x, scale * layer.scale.y, 1f);
                        }

                        var player = layerGo.AddComponent<SpriteFramePlayer>();
                        backgroundPlayers.Add(player);
                        player.Initialize(framesData, () =>
                        {
                            player.UseGameClock(true);
                            player.SetLoop(true);
                            player.Play();
                        });
                        player.UseGameClock(true);
                        player.SetLoop(true);
                    }
                    else
                    {
                        Debug.LogWarning($"[LevelRuntime] Background layer '{layer.name}' spriteFramesData is not a SpriteFramesData");
                    }
                }
                else if (layer.staticSprite != null)
                {
                    // Static sprite background
                    sr.sprite = layer.staticSprite;
                    layerGo.transform.localScale = new Vector3(layer.scale.x, layer.scale.y, 1f);
                }
                else if (layer.solidColor.a > 0)
                {
                    // Solid color — create a 1x1 white texture, scale to fill viewport
                    sr.color = layer.solidColor;
                    if (cam != null && cam.orthographic)
                    {
                        float camHeight = cam.orthographicSize * 2f;
                        float camWidth = camHeight * cam.aspect;
                        layerGo.transform.localScale = new Vector3(
                            camWidth * layer.scale.x, camHeight * layer.scale.y, 1f);
                    }
                }
            }

            //Debug.Log($"[LevelRuntime] Setup {layers.Count} background layers");
        }

        /// <summary>
        /// backgroundLayers is the newer list. Existing levels still store their
        /// videos on the legacy backgrounds list, which was being ignored.
        /// </summary>
        private List<BackgroundLayerConfig> ResolveBackgroundLayers()
        {
            if (config.backgroundLayers != null && config.backgroundLayers.Count > 0)
                return config.backgroundLayers;

            var layers = new List<BackgroundLayerConfig>();
            if (config.backgrounds == null)
                return layers;

            for (int i = 0; i < config.backgrounds.Count; i++)
            {
                var frames = config.backgrounds[i] as SpriteFramesData;
                if (frames == null)
                    continue;

                layers.Add(new BackgroundLayerConfig
                {
                    name = string.IsNullOrEmpty(frames.videoName) ? $"BG_{i}" : frames.videoName,
                    spriteFramesData = frames,
                    layer = i == 0 ? SpriteLayer.Background1 : SpriteLayer.Background2,
                    sortingOrder = i == 0 ? 0 : i - 1,
                    scale = Vector2.one
                });
            }

            return layers;
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
            //Debug.Log($"[LevelRuntime] Experience started: {config.timeline.events.Count} events");
        }

        #endregion

        #region Enemy Spawning

        private void SpawnEnemies()
        {
            if (config.enemies == null) return;
            enemyHasSpawned = new bool[config.enemies.Count];
            TrySpawnDueEnemies();
        }

        /// <summary>
        /// Spawn from the game clock, not a real-time delay. Rewinding before a
        /// spawn and playing forward crosses spawnTime again and creates the enemy
        /// if it does not already exist. Already-spawned enemies are shown again
        /// by RewindableGameObject.
        /// </summary>
        private void TrySpawnDueEnemies()
        {
            if (config?.enemies == null || enemyHasSpawned == null) return;
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding) return;

            float time = GameClock.Instance != null ? GameClock.Instance.Time : 0f;

            for (int i = 0; i < config.enemies.Count; i++)
            {
                if (enemyHasSpawned[i]) continue;

                var spawn = config.enemies[i];
                if (time + 0.0001f < spawn.spawnTime) continue;
                if (!templateCache.TryGetValue(spawn.type, out var template))
                {
                    Debug.LogWarning($"[LevelRuntime] No EnemyTypeConfig for {spawn.type}");
                    enemyHasSpawned[i] = true;
                    continue;
                }
                if (template.prefab == null)
                {
                    Debug.LogWarning($"[LevelRuntime] EnemyTypeConfig '{template.displayName}' has no prefab!");
                    enemyHasSpawned[i] = true;
                    continue;
                }

                enemyHasSpawned[i] = true;
                SpawnEnemy(template, spawn);
            }
        }

        private void SpawnEnemy(EnemyTypeConfig template, EnemySpawnConfig spawn)
        {
            var enemy = Instantiate(template.prefab, new Vector3(spawn.position.x, spawn.position.y, 0f), Quaternion.identity, enemyContainer);
            enemy.name = $"{template.displayName}_{config.enemies.IndexOf(spawn)}";

            var enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyBase != null)
                enemyBase.InitializeFromConfig(template, spawn);

            // Subscribe to death event for win condition tracking
            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                int hp = spawn.healthOverride > 0 ? spawn.healthOverride : template.defaultHealth;
                health.SetMaxHealth(hp);
                health.onDefeated.AddListener(OnEnemyDied);
                health.onRevived.AddListener(OnEnemyRevived);
            }

            enemiesSpawned++;

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

            //Debug.Log($"[LevelRuntime] Spawned barriers (viewport: {halfWidth * 2:F1}x{halfHeight * 2:F1})");
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
            if (characterObject == null) return;

            bool inside = IsPlayerInWinZone();
            if (levelCompleted)
            {
                if (completionCommitted) return;
                if (!inside && !KillsSatisfied())
                    CancelCompletion();
                return;
            }

            if (GameClock.Instance != null && GameClock.Instance.IsRewinding) return;
            if (inside)
                CompleteLevel();
        }

        private bool IsPlayerInWinZone()
        {
            Vector2 playerPos = characterObject.transform.position;
            return Mathf.Abs(playerPos.x - winZoneCenter.x) <= winZoneHalfSize.x &&
                   Mathf.Abs(playerPos.y - winZoneCenter.y) <= winZoneHalfSize.y;
        }

        private bool KillsSatisfied()
        {
            return totalEnemiesToSpawn > 0 &&
                   enemiesKilled >= totalEnemiesToSpawn &&
                   enemiesSpawned >= totalEnemiesToSpawn;
        }

        private void OnEnemyDied()
        {
            enemiesKilled++;
            //Debug.Log($"[LevelRuntime] Enemy killed ({enemiesKilled}/{totalEnemiesToSpawn})");

            if (GameClock.Instance != null && GameClock.Instance.IsRewinding) return;
            if (KillsSatisfied())
                CompleteLevel();
        }

        private void OnEnemyRevived()
        {
            enemiesKilled = Mathf.Max(0, enemiesKilled - 1);
            if (!levelCompleted || completionCommitted) return;
            if (KillsSatisfied()) return;
            if (characterObject != null && IsPlayerInWinZone()) return;
            CancelCompletion();
        }

        private bool completionCommitted;

        public void CompleteLevel()
        {
            if (levelCompleted) return;
            levelCompleted = true;

            // Progress is written when the level actually changes. Rewind during
            // the delay can still undo the clear.
            if (config.returnToMainMenuOnComplete)
                Invoke(nameof(FinishAndReturnToMainMenu), config.completionDelay);
            else
                Invoke(nameof(ProceedToNextLevel), config.completionDelay);
        }

        private void CancelCompletion()
        {
            if (completionCommitted) return;
            levelCompleted = false;
            CancelInvoke(nameof(ProceedToNextLevel));
            CancelInvoke(nameof(FinishAndReturnToMainMenu));
        }

        private void CommitCompletion()
        {
            if (completionCommitted) return;
            completionCommitted = true;

            GameProgressManager.Instance.SetLevelCompleted(levelNumber);
            AnalyticsManager.Instance?.TrackLevelComplete(levelNumber);

            if (playerHealth != null)
                RogueliteManager.Instance.SaveLevelEndHealth(levelNumber, playerHealth.CurrentHealth, playerHealth.MaxHealth);

            if (config != null && !config.returnToMainMenuOnComplete)
                GameProgressManager.Instance.UnlockVideo(levelNumber + 1);
        }

        private void FinishAndReturnToMainMenu()
        {
            CommitCompletion();
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        private void ProceedToNextLevel()
        {
            CommitCompletion();

            if (config.nextVideoNumber > 0)
            {
                var nextVideo = gameDefinition != null ? gameDefinition.GetVideo(config.nextVideoNumber) : null;
                var frames = nextVideo != null ? nextVideo.spriteFramesData as SpriteFramesData : null;
                if (frames != null && !frames.HasBuiltFrames)
                {
                    Debug.LogWarning($"[LevelRuntime] Video {config.nextVideoNumber} is not built yet. Returning to the main menu.");
                    SceneTransitionManager.Instance.LoadMainMenu();
                    return;
                }

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
