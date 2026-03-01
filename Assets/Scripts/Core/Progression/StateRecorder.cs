using SickOfIt.Core;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Damage;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Records game state snapshots for rewind. Ring buffer, configurable duration.
    /// 
    /// Tracks: player position/velocity/health, enemy positions/health/active state, camera.
    /// Uses cached references — no GetComponent in hot paths.
    /// Only records when GameClock flows forward.
    /// </summary>
    public class StateRecorder : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float snapshotInterval = 0.1f;
        [SerializeField] private float maxHistorySeconds = 10f;

        [Header("Scene References (wired by generator)")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Rigidbody2D playerRb;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Camera mainCamera;

        // Ring buffer
        private StateSnapshot[] snapshots;
        private int writeIndex;
        private int snapshotCount;
        private float lastSnapshotTime;
        private bool isRestoring;
        private bool subscribedToGameClock;

        // Enemy tracking — cached refs, no GetComponent during record/restore
        private readonly List<TrackedEnemy> trackedEnemies = new List<TrackedEnemy>();

        private struct TrackedEnemy
        {
            public int instanceId;
            public EnemyBase enemy;
            public EnemyHealth health;
            public Rigidbody2D rb;
            public RigidbodyType2D originalBodyType;
        }

        private void Start()
        {
            int capacity = Mathf.CeilToInt(maxHistorySeconds / snapshotInterval);
            snapshots = new StateSnapshot[capacity];
            for (int i = 0; i < capacity; i++)
                snapshots[i] = new StateSnapshot();

            if (playerTransform == null && playerRb != null)
                playerTransform = playerRb.transform;

            TrySubscribeToGameClock();
        }

        private void TrySubscribeToGameClock()
        {
            if (subscribedToGameClock || GameClock.Instance == null) return;
            GameClock.Instance.OnTimeChanged += OnClockTick;
            GameClock.Instance.OnRewindStarted += OnRewindStarted;
            GameClock.Instance.OnRewindStopped += OnRewindStopped;
            subscribedToGameClock = true;
        }

        private void OnDestroy()
        {
            if (GameClock.Instance != null)
            {
                GameClock.Instance.OnTimeChanged -= OnClockTick;
                GameClock.Instance.OnRewindStarted -= OnRewindStarted;
                GameClock.Instance.OnRewindStopped -= OnRewindStopped;
            }
        }

        /// <summary>
        /// Register an enemy with all refs cached upfront. Call when spawning.
        /// </summary>
        public void RegisterEnemy(EnemyBase enemy)
        {
            int id = enemy.gameObject.GetInstanceID();

            // Check if already tracked
            for (int i = 0; i < trackedEnemies.Count; i++)
                if (trackedEnemies[i].instanceId == id) return;

            trackedEnemies.Add(new TrackedEnemy
            {
                instanceId = id,
                enemy = enemy,
                health = enemy.GetComponent<EnemyHealth>(),   // Cached ONCE at registration
                rb = enemy.GetComponent<Rigidbody2D>(),       // Cached ONCE at registration
                originalBodyType = enemy.GetComponent<Rigidbody2D>()?.bodyType ?? RigidbodyType2D.Dynamic
            });
        }

        #region Recording

        private void Update()
        {
            TrySubscribeToGameClock();

            if (!isRestoring || GameClock.Instance == null) return;
            RestoreToTime(GameClock.Instance.Time);
        }

        private void OnClockTick(float time)
        {
            if (isRestoring || GameClock.Instance.IsRewinding) return;
            if (time - lastSnapshotTime < snapshotInterval) return;

            RecordSnapshot(time);
            lastSnapshotTime = time;
        }

        private void RecordSnapshot(float time)
        {
            var snap = snapshots[writeIndex];
            snap.time = time;

            // Player
            if (playerTransform != null)
            {
                snap.playerPosition = playerTransform.position;
                snap.playerVelocity = playerRb != null ? playerRb.linearVelocity : Vector2.zero;
            }
            if (playerHealth != null)
            {
                snap.playerHealth = playerHealth.CurrentHealth;
                snap.playerMaxHealth = playerHealth.MaxHealth;
            }

            // Camera
            if (mainCamera != null)
            {
                snap.cameraPosition = mainCamera.transform.position;
                snap.cameraSize = mainCamera.orthographic ? mainCamera.orthographicSize : 0f;
            }

            // Enemies — all from cached list, no FindObjectsByType
            snap.enemyStates.Clear();
            for (int i = 0; i < trackedEnemies.Count; i++)
            {
                var te = trackedEnemies[i];
                if (te.enemy == null) continue;

                snap.enemyStates.Add(new EnemySnapshot
                {
                    instanceId = te.instanceId,
                    position = te.enemy.transform.position,
                    isActive = te.enemy.gameObject.activeSelf,
                    health = te.health != null ? te.health.CurrentHealth : 0,
                    maxHealth = te.health != null ? te.health.MaxHealth : 0
                });
            }

            writeIndex = (writeIndex + 1) % snapshots.Length;
            if (snapshotCount < snapshots.Length)
                snapshotCount++;
        }

        #endregion

        #region Rewind

        private void OnRewindStarted()
        {
            isRestoring = true;

            // Freeze player
            if (playerRb != null)
            {
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.linearVelocity = Vector2.zero;
            }

            // Freeze all enemies
            for (int i = 0; i < trackedEnemies.Count; i++)
            {
                var rb = trackedEnemies[i].rb;
                if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            }

            DestroyProjectiles();
        }

        private void OnRewindStopped()
        {
            isRestoring = false;

            // Restore player physics
            if (playerRb != null)
                playerRb.bodyType = RigidbodyType2D.Dynamic;

            // Restore enemy physics (only active ones)
            for (int i = 0; i < trackedEnemies.Count; i++)
            {
                var te = trackedEnemies[i];
                if (te.rb != null && te.enemy != null && te.enemy.gameObject.activeSelf)
                    te.rb.bodyType = te.originalBodyType;
            }

            DestroyProjectiles();
        }

        private void RestoreToTime(float targetTime)
        {
            if (snapshotCount == 0) return;

            // Find closest snapshot
            StateSnapshot closest = null;
            float closestDelta = float.MaxValue;

            for (int i = 0; i < snapshotCount; i++)
            {
                int idx = ((writeIndex - 1 - i) % snapshots.Length + snapshots.Length) % snapshots.Length;
                var snap = snapshots[idx];
                float delta = Mathf.Abs(snap.time - targetTime);
                if (delta < closestDelta)
                {
                    closestDelta = delta;
                    closest = snap;
                }
                if (snap.time < targetTime - snapshotInterval * 2)
                    break;
            }

            if (closest == null) return;

            // Restore player
            if (playerTransform != null)
                playerTransform.position = closest.playerPosition;
            if (playerHealth != null && closest.playerHealth > 0)
                playerHealth.SetHealth(closest.playerHealth);

            // Restore camera
            if (mainCamera != null)
            {
                mainCamera.transform.position = closest.cameraPosition;
                if (mainCamera.orthographic && closest.cameraSize > 0)
                    mainCamera.orthographicSize = closest.cameraSize;
            }

            // Restore enemies — use cached tracked list, match by instanceId
            for (int e = 0; e < closest.enemyStates.Count; e++)
            {
                var es = closest.enemyStates[e];
                TrackedEnemy? matched = null;

                for (int t = 0; t < trackedEnemies.Count; t++)
                {
                    if (trackedEnemies[t].instanceId == es.instanceId)
                    {
                        matched = trackedEnemies[t];
                        break;
                    }
                }

                if (matched == null || matched.Value.enemy == null) continue;

                var te = matched.Value;
                var go = te.enemy.gameObject;

                // Activate/deactivate
                if (es.isActive && !go.activeSelf) go.SetActive(true);
                else if (!es.isActive && go.activeSelf) go.SetActive(false);

                if (es.isActive)
                {
                    te.enemy.transform.position = es.position;
                    if (te.health != null && es.health > 0)
                        te.health.SetHealth(es.health);
                }
            }

            TrimSnapshotsAfter(targetTime);
        }

        private void TrimSnapshotsAfter(float time)
        {
            while (snapshotCount > 0)
            {
                int lastIdx = ((writeIndex - 1) % snapshots.Length + snapshots.Length) % snapshots.Length;
                if (snapshots[lastIdx].time > time + snapshotInterval)
                {
                    writeIndex = lastIdx;
                    snapshotCount--;
                }
                else break;
            }
        }

        private void DestroyProjectiles()
        {
            var damageDealers = Object.FindObjectsByType<DamageDealer>(FindObjectsSortMode.None);
            foreach (var dd in damageDealers)
            {
                if (dd.GetComponent<EnemyBase>() != null) continue;
                if (dd.GetComponent<PlayerHealth>() != null) continue;
                Destroy(dd.gameObject);
            }
        }

        #endregion

        #region Data

        private class StateSnapshot
        {
            public float time;
            public Vector3 playerPosition;
            public Vector2 playerVelocity;
            public int playerHealth;
            public int playerMaxHealth;
            public Vector3 cameraPosition;
            public float cameraSize;
            public readonly List<EnemySnapshot> enemyStates = new List<EnemySnapshot>();
        }

        private struct EnemySnapshot
        {
            public int instanceId;
            public Vector3 position;
            public bool isActive;
            public int health;
            public int maxHealth;
        }

        #endregion
    }
}
