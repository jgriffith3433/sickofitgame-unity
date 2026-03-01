using SickOfIt.Core;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Combat.Enemies;
using SickOfIt.Combat.Health;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Records game state snapshots at regular intervals for rewind.
    /// 
    /// Tracked per snapshot:
    ///   - Player position, velocity, health
    ///   - All enemy positions, health, active state
    ///   - Spawned/destroyed objects
    ///   - Audio state (music position)
    ///   - Camera position
    /// 
    /// Rewind = restore snapshots in reverse order.
    /// Uses a ring buffer with configurable duration.
    /// 
    /// Attaches to GameClock — only records when time flows forward.
    /// </summary>
    public class StateRecorder : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Seconds between snapshots (lower = smoother rewind, more memory)")]
        [SerializeField] private float snapshotInterval = 0.1f;
        [Tooltip("Maximum seconds of rewind history")]
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

        // Active during rewind
        private bool isRestoring;

        #region Initialization

        private void Start()
        {
            int capacity = Mathf.CeilToInt(maxHistorySeconds / snapshotInterval);
            snapshots = new StateSnapshot[capacity];
            for (int i = 0; i < capacity; i++)
                snapshots[i] = new StateSnapshot();

            // If player refs weren't wired by generator, try to resolve from characterObject
            if (playerTransform == null && playerRb != null)
                playerTransform = playerRb.transform;

            // Subscribe to GameClock
            if (GameClock.Instance != null)
            {
                GameClock.Instance.OnTimeChanged += OnClockTick;
                GameClock.Instance.OnRewindStarted += OnRewindStarted;
                GameClock.Instance.OnRewindStopped += OnRewindStopped;
            }
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

        #endregion

        #region Recording

        private void OnClockTick(float time)
        {
            if (isRestoring) return;
            if (GameClock.Instance.IsRewinding) return;

            if (time - lastSnapshotTime >= snapshotInterval)
            {
                RecordSnapshot(time);
                lastSnapshotTime = time;
            }
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

            // Enemies
            snap.enemyStates.Clear();
            var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                var es = new EnemySnapshot
                {
                    instanceId = enemy.gameObject.GetInstanceID(),
                    position = enemy.transform.position,
                    isActive = enemy.gameObject.activeSelf,
                };

                var health = enemy.GetComponent<EnemyHealth>();
                if (health != null)
                {
                    es.health = health.CurrentHealth;
                    es.isAlive = health.IsAlive;
                }

                snap.enemyStates.Add(es);
            }

            // Advance write pointer
            writeIndex = (writeIndex + 1) % snapshots.Length;
            if (snapshotCount < snapshots.Length)
                snapshotCount++;
        }

        #endregion

        #region Rewind Restore

        private void OnRewindStarted()
        {
            isRestoring = true;

            // Disable player physics during rewind
            if (playerRb != null)
            {
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.linearVelocity = Vector2.zero;
            }
        }

        private void OnRewindStopped()
        {
            isRestoring = false;

            // Re-enable player physics
            if (playerRb != null)
                playerRb.bodyType = RigidbodyType2D.Dynamic;
        }

        private void Update()
        {
            if (!isRestoring || GameClock.Instance == null) return;

            float targetTime = GameClock.Instance.Time;
            RestoreToTime(targetTime);
        }

        /// <summary>
        /// Find the closest snapshot to the target time and restore it.
        /// </summary>
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
                // Optimization: if we've passed the target going backwards, stop
                if (snap.time < targetTime - snapshotInterval * 2)
                    break;
            }

            if (closest == null) return;

            // Restore player
            if (playerTransform != null)
                playerTransform.position = closest.playerPosition;

            // Restore camera
            if (mainCamera != null)
            {
                mainCamera.transform.position = closest.cameraPosition;
                if (mainCamera.orthographic && closest.cameraSize > 0)
                    mainCamera.orthographicSize = closest.cameraSize;
            }

            // Restore enemies
            var currentEnemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in currentEnemies)
            {
                int id = enemy.gameObject.GetInstanceID();
                EnemySnapshot found = null;
                foreach (var es in closest.enemyStates)
                {
                    if (es.instanceId == id)
                    {
                        found = es;
                        break;
                    }
                }

                if (found != null)
                {
                    enemy.transform.position = found.position;
                    enemy.gameObject.SetActive(found.isActive);

                    var health = enemy.GetComponent<EnemyHealth>();
                    if (health != null && found.health > 0)
                        health.SetHealth(found.health);
                }
            }

            // Trim snapshots ahead of current time (they're now "future")
            TrimSnapshotsAfter(targetTime);
        }

        private void TrimSnapshotsAfter(float time)
        {
            // Remove snapshots recorded after the current rewind position
            while (snapshotCount > 0)
            {
                int lastIdx = ((writeIndex - 1) % snapshots.Length + snapshots.Length) % snapshots.Length;
                if (snapshots[lastIdx].time > time + snapshotInterval)
                {
                    writeIndex = lastIdx;
                    snapshotCount--;
                }
                else
                {
                    break;
                }
            }
        }

        #endregion

        #region Snapshot Data Structures

        private class StateSnapshot
        {
            public float time;

            // Player
            public Vector3 playerPosition;
            public Vector2 playerVelocity;
            public int playerHealth;
            public int playerMaxHealth;

            // Camera
            public Vector3 cameraPosition;
            public float cameraSize;

            // Enemies
            public List<EnemySnapshot> enemyStates = new List<EnemySnapshot>();
        }

        private class EnemySnapshot
        {
            public int instanceId;
            public Vector3 position;
            public bool isActive;
            public int health;
            public bool isAlive;
        }

        #endregion
    }
}
