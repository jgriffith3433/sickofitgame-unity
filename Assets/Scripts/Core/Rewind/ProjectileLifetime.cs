using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Returns the owning GameObject to its ObjectPool after a configurable lifetime
    /// using GameClock time. Replaces Destroy(obj, lifetime) calls.
    /// 
    /// Pauses during rewind. Does not tick when GameClock is paused or rewinding.
    /// </summary>
    public class ProjectileLifetime : MonoBehaviour, IPoolReturnable
    {
        [SerializeField] private float lifetime = 5f;

        private ObjectPool ownerPool;
        private float spawnTime;
        private bool initialized;

        /// <summary>
        /// Set the lifetime externally (e.g., from the firing code).
        /// Must be called before the first Update tick if overriding the default.
        /// </summary>
        public void SetLifetime(float seconds)
        {
            lifetime = seconds;
        }

        /// <summary>
        /// Set the pool this projectile belongs to. Called by ObjectPool on creation.
        /// </summary>
        public void SetOwnerPool(ObjectPool pool)
        {
            ownerPool = pool;
        }

        /// <summary>
        /// Reset internal state when the object is re-used from the pool.
        /// </summary>
        public void ResetLifetime()
        {
            initialized = false;
        }

        private void OnEnable()
        {
            if (!initialized && GameClock.Instance != null)
            {
                spawnTime = GameClock.Instance.Time;
                initialized = true;
            }
        }

        private void Update()
        {
            if (GameClock.Instance == null) return;
            if (GameClock.Instance.IsPaused || GameClock.Instance.IsRewinding) return;

            if (!initialized)
            {
                spawnTime = GameClock.Instance.Time;
                initialized = true;
            }

            float elapsed = GameClock.Instance.Time - spawnTime;
            if (elapsed >= lifetime)
            {
                if (ownerPool != null)
                    ownerPool.Return(gameObject);
                else
                    Destroy(gameObject); // fallback if no pool
            }
        }
    }
}
