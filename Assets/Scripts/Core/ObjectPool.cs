using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Core
{
    /// <summary>
    /// Reusable object pool. Created by scene generators with a prefab and initial size.
    /// Consumers (PlayerController, EnemyShooter, EnemySentinel) hold a serialized reference.
    ///
    /// Integrates with the rewind system — RewindTimeline calls ReturnAll() on rewind
    /// instead of destroying tracked projectiles.
    ///
    /// On Get():
    ///   - Activates a pooled instance, positions it, resets its Rigidbody2D
    ///   - Sets ProjectileLifetime.ownerPool so it knows where to return
    ///
    /// On Return():
    ///   - Deactivates and re-parents to the hidden container
    ///
    /// Self-registers with RewindTimeline so it can be bulk-returned on rewind.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private int initialSize = 10;

        private Transform poolContainer;
        private readonly Queue<GameObject> available = new Queue<GameObject>();
        private readonly List<GameObject> active = new List<GameObject>();

        // Per-instance component cache — avoids GetComponent on every Get()
        private readonly Dictionary<GameObject, Rigidbody2D> cachedRb = new Dictionary<GameObject, Rigidbody2D>();
        private readonly Dictionary<GameObject, Rewind.ProjectileLifetime> cachedLifetime = new Dictionary<GameObject, Rewind.ProjectileLifetime>();

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
                available.Enqueue(obj);
            }
        }

        private GameObject CreateInstance()
        {
            var obj = Instantiate(prefab, poolContainer);
            obj.name = $"{prefab.name}_{available.Count + active.Count}";
            obj.SetActive(false);

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

            // Wire DamageDealer so hit-projectiles return to pool instead of Destroy
            // Use SendMessage to avoid cross-assembly dependency (DamageDealer is in SickOfIt.Combat)
            obj.SendMessage("SetOwnerPool", this, SendMessageOptions.DontRequireReceiver);

            return obj;
        }

        /// <summary>
        /// Get an object from the pool. Activates it at the given position/rotation.
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj;

            if (available.Count > 0)
            {
                obj = available.Dequeue();
            }
            else
            {
                // Pool exhausted — grow by one
                Debug.LogWarning($"[ObjectPool] '{gameObject.name}' exhausted, growing. Consider increasing initialSize.");
                obj = CreateInstance();
            }

            // Re-parent to scene root (out of the hidden container)
            obj.transform.SetParent(null);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            // Reset Rigidbody2D state (from cache — no GetComponent)
            if (cachedRb.TryGetValue(obj, out var rb) && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // Reset ProjectileLifetime (from cache — no GetComponent)
            if (cachedLifetime.TryGetValue(obj, out var lifetime) && lifetime != null)
                lifetime.ResetLifetime();

            active.Add(obj);
            return obj;
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
        /// Return an object to the pool. Deactivates it and re-parents to the container.
        /// </summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            active.Remove(obj);
            obj.SetActive(false);
            obj.transform.SetParent(poolContainer);
            available.Enqueue(obj);
        }

        /// <summary>
        /// Return ALL active objects to the pool. Called by RewindTimeline on rewind start/stop.
        /// </summary>
        public void ReturnAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var obj = active[i];
                if (obj != null)
                {
                    obj.SetActive(false);
                    obj.transform.SetParent(poolContainer);
                    available.Enqueue(obj);
                }
            }
            active.Clear();
        }
    }
}
