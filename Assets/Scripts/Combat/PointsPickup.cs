using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;

namespace SickOfIt.Combat
{
    /// <summary>
    /// A collectable pickup that awards points when the player touches it.
    /// Spawned by enemies on death. Uses a trigger collider.
    /// Supports pool-based lifecycle: SetPool() on creation, ReturnToPool() on collection/despawn.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PointsPickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [Tooltip("Points awarded when collected")]
        [SerializeField] private int pointsValue = 1;

        [Tooltip("How long the pickup stays before despawning (0 = forever)")]
        [SerializeField] private float lifetime = 1f;

        [Header("Movement")]
        [Tooltip("Initial upward pop force when spawned")]
        [SerializeField] private float popForce = 3f;

        [Tooltip("Gravity applied to the pickup after popping")]
        [SerializeField] private float gravity = 5f;

        [Tooltip("Horizontal spread range when spawned")]
        [SerializeField] private float spreadRange = 0.5f;

        [Header("Visual")]
        [Tooltip("Optional: Bobbing amplitude once settled")]
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobSpeed = 3f;

        private Vector2 velocity;
        private bool settled;
        private float settledY;
        private float settledTime;
        private float aliveTime;
        private ObjectPool pool;

        /// <summary>
        /// Set the points value at runtime (used when spawning from enemy drops).
        /// </summary>
        public void SetPointsValue(int value)
        {
            pointsValue = value;
        }

        /// <summary>
        /// Set the pool this pickup belongs to (called once at pool warmup via ObjectPool parent).
        /// When set, collection/despawn returns to pool instead of Destroy.
        /// </summary>
        public void SetPool(ObjectPool p)
        {
            pool = p;
        }

        [SerializeField] private Collider2D col;

        private void Start()
        {
            ResetState();
        }

        private void OnEnable()
        {
            // Reset state for pool reuse
            ResetState();
        }

        private void ResetState()
        {
            // Make sure collider is a trigger
            if (col == null) col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;

            // Apply initial pop velocity
            float randomX = Random.Range(-spreadRange, spreadRange);
            velocity = new Vector2(randomX, popForce);
            aliveTime = 0f;
            settled = false;
        }

        private void Update()
        {
            // Respect GameClock — freeze during pause/rewind
            var clock = GameClock.Instance;
            float dt = clock != null ? clock.DeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            aliveTime += dt;

            // Self-destruct / return to pool after lifetime
            if (lifetime > 0 && aliveTime >= lifetime)
            {
                ReturnOrDestroy();
                return;
            }

            if (!settled)
            {
                // Apply gravity
                velocity.y -= gravity * dt;
                transform.position += (Vector3)velocity * dt;

                // Settle when falling below spawn height
                if (velocity.y < 0 && transform.position.y <= settledY)
                {
                    settled = true;
                    Vector3 pos = transform.position;
                    pos.y = settledY;
                    transform.position = pos;
                    settledTime = aliveTime;
                }
            }
            else if (bobAmplitude > 0)
            {
                // Gentle bobbing once settled (uses alive time for consistency)
                float elapsed = aliveTime - settledTime;
                Vector3 pos = transform.position;
                pos.y = settledY + Mathf.Sin(elapsed * bobSpeed) * bobAmplitude;
                transform.position = pos;
            }
        }

        /// <summary>
        /// Set the initial Y position to settle at (call right after instantiation).
        /// </summary>
        public void SetSettledY(float y)
        {
            settledY = y;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            // Award points
            RogueliteManager.Instance.AddPoints(pointsValue);

            ReturnOrDestroy();
        }

        /// <summary>
        /// Return to pool if available, else Destroy.
        /// </summary>
        private void ReturnOrDestroy()
        {
            if (pool != null)
                pool.Return(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
