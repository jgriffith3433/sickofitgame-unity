using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Core
{
    /// <summary>
    /// Reusable object pool. Created by scene generators with a prefab and initial size.
    /// Consumers (PlayerController, EnemyShooter, EnemySentinel) hold a serialized reference.
    ///
    /// Shots and diamonds keep their rewind history after they despawn. Rewind can
    /// show them again. A shot from a future you rewound away is reclaimed instead
    /// of playing itself over.
    ///
    /// On Get():
    ///   - Activates a pooled instance, positions it, resets its Rigidbody2D
    ///   - Starts a fresh rewind life
    ///
    /// On Return():
    ///   - Deactivates in place and holds the instance until it ages out of rewind
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private int initialSize = 10;

        private Transform poolContainer;
        private readonly List<GameObject> available = new List<GameObject>();
        private readonly List<GameObject> active = new List<GameObject>();
        private readonly List<GameObject> cooling = new List<GameObject>();

        // Per-instance component cache — avoids GetComponent on every Get()
        private readonly Dictionary<GameObject, Rigidbody2D> cachedRb = new Dictionary<GameObject, Rigidbody2D>();
        private readonly Dictionary<GameObject, Rewind.ProjectileLifetime> cachedLifetime = new Dictionary<GameObject, Rewind.ProjectileLifetime>();
        private readonly Dictionary<GameObject, Rewind.IRewindable[]> cachedRewind = new Dictionary<GameObject, Rewind.IRewindable[]>();
        private readonly Dictionary<GameObject, Rewind.RewindableGameObject> cachedPresence = new Dictionary<GameObject, Rewind.RewindableGameObject>();
        private readonly Dictionary<GameObject, float> heldSince = new Dictionary<GameObject, float>();

        public int ActiveCount => active.Count;
        public int AvailableCount => available.Count;

        private void Awake()
        {
            // Create a hidden container for inactive pool objects
            var containerGo = new GameObject($"{gameObject.name}_Container");
            containerGo.transform.SetParent(transform);
            containerGo.SetActive(false); // children are deactivated by parent
            poolContainer = containerGo.transform;

            if (prefab != null)
                Prewarm(initialSize);

            // Self-register with RewindTimeline
            if (Rewind.RewindTimeline.Instance != null)
                Rewind.RewindTimeline.Instance.RegisterPool(this);
        }

        private void Start()
        {
            // Retry registration if RewindTimeline wasn't ready in Awake
            if (Rewind.RewindTimeline.Instance != null)
                Rewind.RewindTimeline.Instance.RegisterPool(this);
        }

        private void Update()
        {
            if (cooling.Count == 0) return;
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding) return;

            float time = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
            float window = Rewind.RewindTimeline.Instance != null
                ? Rewind.RewindTimeline.Instance.MaxHistorySeconds
                : 30f;

            for (int i = cooling.Count - 1; i >= 0; i--)
            {
                GameObject obj = cooling[i];
                if (obj == null)
                {
                    cooling.RemoveAt(i);
                    continue;
                }

                if (obj.activeSelf)
                {
                    cooling.RemoveAt(i);
                    continue;
                }

                float since;
                if (!heldSince.TryGetValue(obj, out since))
                {
                    if (cachedPresence.TryGetValue(obj, out var presence) &&
                        presence != null && presence.HasRecordedBirth)
                        since = presence.BirthTime;
                    else
                        since = time - window;
                }

                if (time < since + window)
                    continue;

                cooling.RemoveAt(i);
                Retire(obj);
            }
        }

        private void OnDestroy()
        {
            if (Rewind.RewindTimeline.Instance != null)
                Rewind.RewindTimeline.Instance.UnregisterPool(this);
        }

        /// <summary>
        /// Pre-instantiate pool objects. Called once during Awake.
        /// </summary>
        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = CreateInstance();
                available.Add(obj);
            }
        }

        private GameObject CreateInstance()
        {
            var obj = Instantiate(prefab, poolContainer);
            obj.name = $"{prefab.name}_{available.Count + active.Count + cooling.Count}";
            obj.SetActive(false);

            EnsureRewind(obj);

            // Cache components at creation time (never GetComponent during gameplay)
            var rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null) cachedRb[obj] = rb;

            // ProjectileLifetime should be on the prefab (added by generator)
            var lifetime = obj.GetComponent<Rewind.ProjectileLifetime>();
            if (lifetime != null)
            {
                lifetime.SetOwnerPool(this);
                cachedLifetime[obj] = lifetime;
            }

            // Wire all IPoolReturnable components so they know which pool to return to
            foreach (var returnable in obj.GetComponents<IPoolReturnable>())
            {
                returnable.SetOwnerPool(this);
            }

            cachedRewind[obj] = obj.GetComponents<Rewind.IRewindable>();
            cachedPresence[obj] = obj.GetComponent<Rewind.RewindableGameObject>();
            return obj;
        }

        private void EnsureRewind(GameObject obj)
        {
            if (obj.GetComponent<Rewind.RewindableTransform>() == null)
                obj.AddComponent<Rewind.RewindableTransform>();

            var presence = obj.GetComponent<Rewind.RewindableGameObject>();
            if (presence == null)
                presence = obj.AddComponent<Rewind.RewindableGameObject>();
            presence.BindPool(this);

            if (obj.GetComponent<Rigidbody2D>() != null &&
                obj.GetComponent<Rewind.RewindableRigidbody2D>() == null)
                obj.AddComponent<Rewind.RewindableRigidbody2D>();

            if (obj.GetComponent<Animator>() != null &&
                obj.GetComponent<Rewind.RewindableAnimator>() == null)
                obj.AddComponent<Rewind.RewindableAnimator>();
        }

        /// <summary>
        /// Get an object from the pool. Activates it at the given position/rotation.
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj;

            if (available.Count > 0)
            {
                int last = available.Count - 1;
                obj = available[last];
                available.RemoveAt(last);
            }
            else
            {
                // Pool exhausted — grow by one. Cooling instances are still rewindable.
                Debug.LogWarning($"[ObjectPool] '{gameObject.name}' exhausted, growing. Consider increasing initialSize.");
                obj = CreateInstance();
            }

            ResetRewindLife(obj);

            // Re-parent to scene root (out of the hidden container)
            obj.transform.SetParent(null);
            obj.transform.SetPositionAndRotation(position, rotation);

            // Clear leftover velocity before the object wakes and records its birth.
            if (cachedRb.TryGetValue(obj, out var rb) && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (cachedLifetime.TryGetValue(obj, out var lifetime) && lifetime != null)
                lifetime.ResetLifetime();

            obj.SetActive(true);
            CaptureRewind(obj);
            active.Add(obj);
            return obj;
        }

        /// <summary>
        /// Record the shot after its launch velocity is applied.
        /// </summary>
        public void CaptureLaunchState(GameObject obj)
        {
            CaptureRewind(obj);
        }

        /// <summary>
        /// Get cached Rigidbody2D for a pooled object (avoids per-use GetComponent).
        /// Returns null if not cached or not present.
        /// </summary>
        public Rigidbody2D GetCachedRb(GameObject obj)
        {
            return cachedRb.TryGetValue(obj, out var rb) ? rb : null;
        }

        /// <summary>
        /// Get cached ProjectileLifetime for a pooled object (avoids per-use GetComponent).
        /// Returns null if not cached or not present.
        /// </summary>
        public Rewind.ProjectileLifetime GetCachedLifetime(GameObject obj)
        {
            return cachedLifetime.TryGetValue(obj, out var lt) ? lt : null;
        }

        /// <summary>
        /// Return an object to the pool. It stays in the scene, inactive, until
        /// rewind can no longer reach it.
        /// </summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding) return;
            if (cooling.Contains(obj) || available.Contains(obj)) return;

            active.Remove(obj);
            obj.SetActive(false);
            heldSince[obj] = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
            cooling.Add(obj);
        }

        /// <summary>
        /// Rewind turned this instance on or off. Keep it out of the free list
        /// while it still belongs to the timeline.
        /// </summary>
        public void SyncRewindPresence(GameObject obj, bool isActive)
        {
            if (obj == null) return;

            if (isActive)
            {
                available.Remove(obj);
                cooling.Remove(obj);
                if (!active.Contains(obj))
                    active.Add(obj);
                return;
            }

            active.Remove(obj);
            available.Remove(obj);
            if (!cooling.Contains(obj))
                cooling.Add(obj);
        }

        /// <summary>
        /// Drop instances whose whole life is after the rewind branch.
        /// They must not appear again when time plays forward.
        /// </summary>
        public void ReclaimDiscarded(float time)
        {
            ReclaimFrom(active, time);
            ReclaimFrom(cooling, time);
        }

        private void ReclaimFrom(List<GameObject> source, float time)
        {
            for (int i = source.Count - 1; i >= 0; i--)
            {
                GameObject obj = source[i];
                if (obj == null)
                {
                    source.RemoveAt(i);
                    continue;
                }

                if (!cachedPresence.TryGetValue(obj, out var presence) || presence == null)
                    continue;
                if (presence.ReplaysBirth || !presence.HasRecordedBirth)
                    continue;
                if (time + 0.0001f >= presence.BirthTime)
                    continue;

                source.RemoveAt(i);
                Retire(obj);
            }
        }

        private void Retire(GameObject obj)
        {
            active.Remove(obj);
            cooling.Remove(obj);
            heldSince.Remove(obj);
            ResetRewindLife(obj);
            obj.SetActive(false);
            obj.transform.SetParent(poolContainer);
            if (!available.Contains(obj))
                available.Add(obj);
        }

        private void ResetRewindLife(GameObject obj)
        {
            if (!cachedRewind.TryGetValue(obj, out var list) || list == null) return;
            for (int i = 0; i < list.Length; i++)
                list[i].BeginNewLife();
        }

        private void CaptureRewind(GameObject obj)
        {
            if (!cachedRewind.TryGetValue(obj, out var list) || list == null) return;
            for (int i = 0; i < list.Length; i++)
                list[i].CaptureBirth();
        }
    }
}
