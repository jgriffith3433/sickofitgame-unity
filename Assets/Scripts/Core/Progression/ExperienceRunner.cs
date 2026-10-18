using SickOfIt.Core;
using SickOfIt.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Runs the game experience — processes timed GameEvents from TimelineConfig.
    /// This is the "director" that makes levels feel alive: sound effects, camera shakes,
    /// enemy waves, visual effects, music changes, all driven by config.
    /// 
    /// Attach to a scene or let LevelRuntime/VideoRuntime/DeathRuntime create one.
    /// 
    /// Design:
    ///   - Events are sorted by time and processed via coroutine
    ///   - Each event type has a dedicated handler
    ///   - Supports pause/resume and time-scale changes
    ///   - Camera effects use the scene's main camera
    ///   - Audio uses a pooled AudioSource system for SFX
    /// </summary>
    public class ExperienceRunner : MonoBehaviour
    {
        #region Config

        [Header("Settings")]
        [SerializeField] private int sfxPoolSize = 8;

        // Runtime state
        private TimelineConfig config;
        private List<GameEvent> sortedEvents;
        private int nextEventIndex;
        private float elapsedTime;
        private bool isRunning;
        private bool isPaused;

        // References
        private Camera mainCamera;
        private Transform cameraTransform;
        private Vector3 cameraOriginalPosition;
        private float cameraOriginalSize;
        private AudioSource musicSource;

        // SFX pool
        private AudioSource[] sfxPool;
        private int sfxPoolIndex;

        // Enemy spawning (delegate to LevelRuntime if available)
        private LevelRuntime levelRuntime;
        private Dictionary<EnemyType, EnemyTypeConfig> templateCache;

        // Injected references (set via SetPlayerRefs / SetMusicSource / SetPools)
        private PlayerHealth cachedPlayerHealth;
        private PlayerController cachedPlayerController;
        private Transform cachedPlayerTransform;
        private IDamageable cachedPlayerDamageable;
        private ObjectPool cachedEnemyProjectilePool;
        private ObjectPool cachedLaserPool;
        private ObjectPool cachedPickupPool;

        // Active coroutines for cleanup
        private List<Coroutine> activeCoroutines = new List<Coroutine>();

        // Camera shake state
        private Coroutine shakeCoroutine;
        private Vector3 shakeOffset;

        #endregion

        #region Public API

        /// <summary>
        /// Inject player references so ExperienceRunner never needs FindAnyObjectByType.
        /// Called by LevelRuntime after setting up the character.
        /// </summary>
        public void SetPlayerRefs(PlayerHealth health, PlayerController controller)
        {
            cachedPlayerHealth = health;
            cachedPlayerController = controller;
            if (controller != null)
            {
                cachedPlayerTransform = controller.transform;
                cachedPlayerDamageable = health;
            }
        }

        /// <summary>
        /// Inject projectile pools so timeline-spawned enemies can shoot.
        /// Called by LevelRuntime after setting up pools.
        /// </summary>
        public void SetPools(ObjectPool enemyProjectilePool, ObjectPool laserPool, ObjectPool pickupPool = null)
        {
            cachedEnemyProjectilePool = enemyProjectilePool;
            cachedLaserPool = laserPool;
            cachedPickupPool = pickupPool;
        }

        /// <summary>
        /// Inject the music AudioSource so we don't scan the scene.
        /// Called by LevelRuntime after setting up audio.
        /// </summary>
        public void SetMusicSource(AudioSource source)
        {
            musicSource = source;
        }

        /// <summary>The level's own camera (Camera.main can still be the previous scene's while this one loads).</summary>
        public void SetCamera(Camera camera)
        {
            mainCamera = camera;
        }

        /// <summary>
        /// Initialize and start running the experience.
        /// </summary>
        public void Run(TimelineConfig timelineConfig, Dictionary<EnemyType, EnemyTypeConfig> templates = null)
        {
            config = timelineConfig;
            templateCache = templates;

            // Overlays sample the game clock themselves, so they need no events and no hold.
            if (config != null && config.overlays != null && config.overlays.Count > 0)
                gameObject.AddComponent<TimelineOverlayPlayer>().Play(config.overlays);

            if (config == null || config.events == null || config.events.Count == 0)
            {
                ////Debug.Log("[ExperienceRunner] No events to process");
                return;
            }

            // Sort events by time
            sortedEvents = new List<GameEvent>(config.events);
            sortedEvents.Sort((a, b) => a.time.CompareTo(b.time));

            // Setup
            SetupReferences();
            SetupSFXPool();

            // Start processing. Callers hold playback until videos and audio
            // are ready, so time-zero events do not fire during that pause.
            nextEventIndex = 0;
            elapsedTime = 0f;
            isRunning = true;
            isPaused = true;
        }

        /// <summary>
        /// Keep timeline events from firing until scene playback is released.
        /// </summary>
        public void SetPlaybackHeld(bool held)
        {
            isPaused = held;
        }

        public void Stop()
        {
            isRunning = false;
            foreach (var co in activeCoroutines)
            {
                if (co != null) StopCoroutine(co);
            }
            activeCoroutines.Clear();

            // Restore camera
            if (cameraTransform != null)
            {
                cameraTransform.position = cameraOriginalPosition;
                if (mainCamera != null && mainCamera.orthographic)
                    mainCamera.orthographicSize = cameraOriginalSize;
            }

            // Restore time scale (only events change it; an overlay-only timeline leaves it alone)
            if (sortedEvents != null)
                Time.timeScale = 1f;
        }

        public float ElapsedTime => elapsedTime;
        public bool IsRunning => isRunning;

        #endregion

        #region Setup

        private void SetupReferences()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
                cameraOriginalPosition = cameraTransform.position;
                cameraOriginalSize = mainCamera.orthographic ? mainCamera.orthographicSize : 0f;
            }

            levelRuntime = GetComponent<LevelRuntime>();

            // musicSource is injected via SetMusicSource().
            // Fallback: search children only (no scene-wide scan).
            if (musicSource == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.Contains("Music"))
                    {
                        musicSource = child.GetComponent<AudioSource>();
                        if (musicSource != null) break;
                    }
                }
            }
        }

        private void SetupSFXPool()
        {
            var poolGo = new GameObject("SFX Pool");
            poolGo.transform.SetParent(transform);
            sfxPool = new AudioSource[sfxPoolSize];
            for (int i = 0; i < sfxPoolSize; i++)
            {
                sfxPool[i] = poolGo.AddComponent<AudioSource>();
                sfxPool[i].playOnAwake = false;
            }
            sfxPoolIndex = 0;
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!isRunning || isPaused || sortedEvents == null) return;

            // Use GameClock if available, otherwise track our own time
            float previousTime = elapsedTime;
            if (GameClock.Instance != null)
                elapsedTime = GameClock.Instance.Time;
            else
                elapsedTime += Time.deltaTime;

            bool goingForward = elapsedTime >= previousTime;
            bool isRewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;

            if (goingForward)
            {
                // Forward: process events that should have fired
                while (nextEventIndex < sortedEvents.Count && sortedEvents[nextEventIndex].time <= elapsedTime)
                {
                    ProcessEvent(sortedEvents[nextEventIndex]);
                    nextEventIndex++;
                }
            }
            else
            {
                // Rewinding: events still fire FORWARD when the clock crosses their time going backwards.
                // This means audio/visual events play naturally (not reversed) during rewind.
                // We walk backwards through events and fire them as we cross their timestamps.
                while (nextEventIndex > 0 && sortedEvents[nextEventIndex - 1].time >= elapsedTime)
                {
                    nextEventIndex--;
                    var evt = sortedEvents[nextEventIndex];

                    // During rewind, only fire "presentation" events (audio, visual).
                    // Skip state-changing events (spawns, activations) — RewindTimeline handles those.
                    if (IsRewindSafeEvent(evt.type))
                        ProcessEvent(evt);
                }
            }

            // Apply camera shake offset
            if (cameraTransform != null && shakeOffset != Vector3.zero)
            {
                cameraTransform.position = cameraOriginalPosition + shakeOffset;
            }

            // Check if we're done
            if (config.duration > 0 && elapsedTime >= config.duration && nextEventIndex >= sortedEvents.Count)
            {
                isRunning = false;
                ////Debug.Log("[ExperienceRunner] Complete");
            }
        }

        private void OnDestroy()
        {
            Stop();
        }

        #endregion

        #region Event Processing

        private void ProcessEvent(GameEvent evt)
        {
            switch (evt.type)
            {
                // Audio
                case GameEventType.PlaySFX:         HandlePlaySFX(evt);         break;
                case GameEventType.PlayMusic:        HandlePlayMusic(evt);       break;
                case GameEventType.StopMusic:        HandleStopMusic(evt);       break;
                case GameEventType.SetMusicVolume:   HandleSetMusicVolume(evt);  break;

                // Camera
                case GameEventType.CameraShake:      HandleCameraShake(evt);     break;
                case GameEventType.CameraZoom:       HandleCameraZoom(evt);      break;
                case GameEventType.CameraMove:       HandleCameraMove(evt);      break;

                // Visual
                case GameEventType.ScreenFlash:      HandleScreenFlash(evt);     break;
                case GameEventType.SetAmbientColor:  HandleSetAmbientColor(evt); break;

                // Objects
                case GameEventType.ActivateObject:   HandleActivateObject(evt, true);  break;
                case GameEventType.DeactivateObject: HandleActivateObject(evt, false); break;
                case GameEventType.SpawnPrefab:      HandleSpawnPrefab(evt);     break;

                // Enemies
                case GameEventType.SpawnEnemyWave:   HandleSpawnEnemyWave(evt);  break;
                case GameEventType.ActivateEnemy:    HandleActivateEnemy(evt);   break;

                // Sprite Frames
                case GameEventType.PlaySpriteSequence: HandlePlaySpriteSequence(evt); break;

                // Time
                case GameEventType.SlowMotion:       HandleSlowMotion(evt);      break;

                // Player
                case GameEventType.SetPlayerInvincible:  HandlePlayerInvincible(evt); break;
                case GameEventType.DisablePlayerInput:   HandlePlayerInput(evt, false); break;
                case GameEventType.EnablePlayerInput:    HandlePlayerInput(evt, true);  break;

                default:
                    Debug.LogWarning($"[ExperienceRunner] Unhandled event type: {evt.type} at t={evt.time}s");
                    break;
            }
        }

        #endregion

        #region Rewind Safety

        /// <summary>
        /// During rewind, only "presentation" events fire — things that produce audio/visual output
        /// but don't change game state. State changes (enemy spawns, object activation) are handled
        /// by RewindTimeline restoring snapshots.
        /// </summary>
        private static bool IsRewindSafeEvent(GameEventType type)
        {
            return type switch
            {
                GameEventType.PlaySFX => true,
                GameEventType.CameraShake => true,
                GameEventType.ScreenFlash => true,
                // Music, zooms, and state changes are NOT safe during rewind
                _ => false
            };
        }

        #endregion

        #region Audio Handlers

        private void HandlePlaySFX(GameEvent evt)
        {
            if (evt.audioClip == null) return;

            var source = GetNextSFXSource();
            source.clip = evt.audioClip;
            source.volume = evt.volume;
            source.pitch = 1f + (evt.pitchVariation > 0 ? Random.Range(-evt.pitchVariation, evt.pitchVariation) : 0f);

            // Spatial audio if position is set
            if (evt.audioPosition != Vector2.zero)
            {
                source.transform.position = new Vector3(evt.audioPosition.x, evt.audioPosition.y, 0);
                source.spatialBlend = 0.5f;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            source.PlayOneShot(evt.audioClip, evt.volume);
        }

        private void HandlePlayMusic(GameEvent evt)
        {
            if (musicSource == null)
            {
                var go = new GameObject("Music");
                go.transform.SetParent(transform);
                musicSource = go.AddComponent<AudioSource>();
                musicSource.loop = true;
            }

            if (evt.audioClip != null)
            {
                musicSource.clip = evt.audioClip;
                musicSource.volume = evt.volume;
                musicSource.Play();
            }
        }

        private void HandleStopMusic(GameEvent evt)
        {
            if (musicSource != null)
            {
                if (evt.duration > 0)
                    activeCoroutines.Add(StartCoroutine(FadeMusicOut(evt.duration)));
                else
                    musicSource.Stop();
            }
        }

        private void HandleSetMusicVolume(GameEvent evt)
        {
            if (musicSource != null)
            {
                if (evt.duration > 0)
                    activeCoroutines.Add(StartCoroutine(FadeMusicVolume(evt.volume, evt.duration)));
                else
                    musicSource.volume = evt.volume;
            }
        }

        private IEnumerator FadeMusicOut(float duration)
        {
            if (musicSource == null) yield break;
            float startVol = musicSource.volume;
            float t = 0;
            while (t < duration)
            {
                t += GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            musicSource.Stop();
            musicSource.volume = startVol;
        }

        private IEnumerator FadeMusicVolume(float targetVol, float duration)
        {
            if (musicSource == null) yield break;
            float startVol = musicSource.volume;
            float t = 0;
            while (t < duration)
            {
                t += GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVol, targetVol, t / duration);
                yield return null;
            }
            musicSource.volume = targetVol;
        }

        private AudioSource GetNextSFXSource()
        {
            var source = sfxPool[sfxPoolIndex];
            sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
            return source;
        }

        #endregion

        #region Camera Handlers

        private void HandleCameraShake(GameEvent evt)
        {
            if (!SickOfIt.Data.GameSettings.ScreenShake)
                return;
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeCamera(evt.shakeIntensity, evt.duration > 0 ? evt.duration : 0.3f));
        }

        private IEnumerator ShakeCamera(float intensity, float duration)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                shakeOffset = new Vector3(x, y, 0);
                elapsed += GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime;
                yield return null;
            }
            shakeOffset = Vector3.zero;
            if (cameraTransform != null)
                cameraTransform.position = cameraOriginalPosition;
        }

        private void HandleCameraZoom(GameEvent evt)
        {
            if (mainCamera != null && mainCamera.orthographic)
            {
                if (evt.duration > 0)
                    activeCoroutines.Add(StartCoroutine(ZoomCamera(evt.zoomTarget, evt.duration)));
                else
                    mainCamera.orthographicSize = evt.zoomTarget;
            }
        }

        private IEnumerator ZoomCamera(float target, float duration)
        {
            float start = mainCamera.orthographicSize;
            float t = 0;
            while (t < duration)
            {
                t += GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime;
                mainCamera.orthographicSize = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            mainCamera.orthographicSize = target;
        }

        private void HandleCameraMove(GameEvent evt)
        {
            if (cameraTransform == null) return;
            Vector3 target = new Vector3(evt.cameraTargetPosition.x, evt.cameraTargetPosition.y, cameraTransform.position.z);

            if (evt.duration > 0)
                activeCoroutines.Add(StartCoroutine(MoveCamera(target, evt.duration)));
            else
            {
                cameraTransform.position = target;
                cameraOriginalPosition = target;
            }
        }

        private IEnumerator MoveCamera(Vector3 target, float duration)
        {
            Vector3 start = cameraTransform.position;
            float t = 0;
            while (t < duration)
            {
                t += GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime;
                cameraTransform.position = Vector3.Lerp(start, target, t / duration);
                yield return null;
            }
            cameraTransform.position = target;
            cameraOriginalPosition = target;
        }

        #endregion

        #region Visual Handlers

        private void HandleScreenFlash(GameEvent evt)
        {
            activeCoroutines.Add(StartCoroutine(FlashScreen(evt.color, evt.duration > 0 ? evt.duration : 0.2f)));
        }

        private IEnumerator FlashScreen(Color color, float duration)
        {
            // Create a full-screen flash using a temporary SpriteRenderer
            var go = new GameObject("ScreenFlash");
            go.transform.SetParent(transform);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = color;
            SpriteLayers.Apply(sr, SpriteLayer.Foreground2, SpriteLayers.Overlay);
            if (mainCamera != null)
            {
                go.transform.position = new Vector3(
                    mainCamera.transform.position.x,
                    mainCamera.transform.position.y,
                    SpriteLayers.Depth(SpriteLayer.Foreground2));
            }

            // Reuse a cached 1x1 white texture (avoids Texture2D leak per flash)
            if (_cachedFlashTexture == null)
            {
                _cachedFlashTexture = new Texture2D(1, 1);
                _cachedFlashTexture.SetPixel(0, 0, Color.white);
                _cachedFlashTexture.Apply();
            }
            sr.sprite = Sprite.Create(_cachedFlashTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            go.transform.localScale = new Vector3(100, 100, 1);

            // Fade out
            float t = 0;
            Color startColor = color;
            Color endColor = new Color(color.r, color.g, color.b, 0);
            while (t < duration)
            {
                t += GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime;
                sr.color = Color.Lerp(startColor, endColor, t / duration);
                yield return null;
            }

            Destroy(go);
        }

        private static Texture2D _cachedFlashTexture;

        private void HandleSetAmbientColor(GameEvent evt)
        {
            RenderSettings.ambientLight = evt.color;
            if (mainCamera != null)
                mainCamera.backgroundColor = evt.color;
        }

        #endregion

        #region Object Handlers

        private void HandleActivateObject(GameEvent evt, bool active)
        {
            if (string.IsNullOrEmpty(evt.targetPath)) return;

            var go = GameObject.Find(evt.targetPath);
            if (go != null)
                go.SetActive(active);
            else
                Debug.LogWarning($"[ExperienceRunner] Object not found: '{evt.targetPath}'");
        }

        private void HandleSpawnPrefab(GameEvent evt)
        {
            if (evt.prefab == null) return;

            var pos = new Vector3(evt.spawnPosition.x, evt.spawnPosition.y, 0);
            var rot = Quaternion.Euler(0, 0, evt.spawnRotation);
            var spawned = Instantiate(evt.prefab, pos, rot);

            if (evt.duration > 0)
                Destroy(spawned, evt.duration);
        }

        #endregion

        #region Enemy Handlers

        private void HandleSpawnEnemyWave(GameEvent evt)
        {
            if (templateCache == null || !templateCache.TryGetValue(evt.enemyType, out var template))
            {
                Debug.LogWarning($"[ExperienceRunner] No template for enemy type: {evt.enemyType}");
                return;
            }

            if (template.prefab == null)
            {
                Debug.LogWarning($"[ExperienceRunner] Enemy template '{template.displayName}' has no prefab");
                return;
            }

            for (int i = 0; i < evt.enemyCount; i++)
            {
                Vector2 pos = (evt.enemyPositions != null && i < evt.enemyPositions.Count)
                    ? evt.enemyPositions[i]
                    : evt.spawnPosition + new Vector2(i * 1.5f, 0);

                var enemy = Instantiate(template.prefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
                enemy.name = $"{template.displayName}_wave_{i}";

                var enemyBase = enemy.GetComponent<EnemyBase>();
                if (enemyBase != null)
                {
                    var spawn = new EnemySpawnConfig
                    {
                        type = evt.enemyType,
                        position = pos,
                        activeAtStart = true
                    };
                    enemyBase.InitializeFromConfig(template, spawn);

                    // Wire player refs so enemies can track and attack the player
                    if (cachedPlayerTransform != null)
                        enemyBase.SetPlayer(cachedPlayerTransform, cachedPlayerDamageable);

                    // Wire projectile pools so shooters/sentinels can fire
                    if (enemyBase is EnemyShooter shooter && cachedEnemyProjectilePool != null)
                        shooter.SetProjectilePool(cachedEnemyProjectilePool);
                    else if (enemyBase is EnemySentinel sentinel && cachedLaserPool != null)
                        sentinel.SetLaserPool(cachedLaserPool);
                }

                // Wire pickup pool for point drops
                if (enemyBase != null && cachedPickupPool != null)
                    enemyBase.SetPickupPool(cachedPickupPool);

                var health = enemy.GetComponent<EnemyHealth>();
                if (health != null)
                    health.SetMaxHealth(template.defaultHealth);
            }

            ////Debug.Log($"[Experience] Spawned {evt.enemyCount}x {evt.enemyType} at t={evt.time:F1}s");
        }

        private void HandleActivateEnemy(GameEvent evt)
        {
            if (string.IsNullOrEmpty(evt.targetPath)) return;

            var go = GameObject.Find(evt.targetPath);
            if (go != null)
            {
                go.SetActive(true);
            }
        }

        #endregion

        #region Sprite Frame Handlers

        private void HandlePlaySpriteSequence(GameEvent evt)
        {
            if (evt.spriteFramesData == null) return;

            var framesData = evt.spriteFramesData as SpriteFramesData;
            if (framesData == null)
            {
                Debug.LogWarning("[ExperienceRunner] spriteFramesData is not a SpriteFramesData ScriptableObject");
                return;
            }

            // Find or create a SpriteFramePlayer for this target
            GameObject targetGo = null;
            if (!string.IsNullOrEmpty(evt.targetPath))
                targetGo = GameObject.Find(evt.targetPath);

            if (targetGo == null)
            {
                // Create a new background layer
                targetGo = new GameObject($"SpriteLayer_{framesData.videoName}");
                targetGo.transform.SetParent(transform);

                var sr = targetGo.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(sr, SpriteLayer.Background2);

                // Scale to fill camera view
                if (mainCamera != null && mainCamera.orthographic)
                {
                    float camHeight = mainCamera.orthographicSize * 2f;
                    float camWidth = camHeight * mainCamera.aspect;
                    float spriteHeight = framesData.frameHeight / framesData.FramePixelsPerUnit;
                    float scale = camHeight / spriteHeight;
                    targetGo.transform.localScale = new Vector3(scale, scale, 1f);
                }

                float depth = SpriteLayers.Depth(SpriteLayer.Background2);
                targetGo.transform.position = mainCamera != null
                    ? new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, depth)
                    : new Vector3(0f, 0f, depth);
            }

            var player = targetGo.GetComponent<SpriteFramePlayer>();
            if (player == null)
                player = targetGo.AddComponent<SpriteFramePlayer>();

            player.Initialize(framesData);
            player.UseGameClock(GameClock.Instance != null);
            player.Play();
        }

        #endregion

        #region Time / Player Handlers

        private void HandleSlowMotion(GameEvent evt)
        {
            Time.timeScale = evt.timeScale;
            if (evt.duration > 0)
                activeCoroutines.Add(StartCoroutine(RestoreTimeScale(evt.duration)));
        }

        private IEnumerator RestoreTimeScale(float afterRealSeconds)
        {
            yield return new WaitForSecondsRealtime(afterRealSeconds);
            Time.timeScale = 1f;
        }

        private void HandlePlayerInvincible(GameEvent evt)
        {
            if (cachedPlayerHealth != null && evt.duration > 0)
            {
                cachedPlayerHealth.SetInvincibilityTimer(evt.duration);
                ////Debug.Log($"[Experience] Player invincible for {evt.duration}s");
            }
        }

        private void HandlePlayerInput(GameEvent evt, bool enable)
        {
            if (cachedPlayerController != null)
                cachedPlayerController.enabled = enable;
        }

        #endregion
    }
}
