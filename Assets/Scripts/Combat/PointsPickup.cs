using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;

namespace SickOfIt.Combat
{
    /// <summary>
    /// A collectable pickup that awards points when the player touches it.
    /// Spawned by enemies on death. Uses a trigger collider.
    /// Supports pool-based lifecycle: SetPool() on creation, ReturnToPool() on collection/despawn.
    /// Implements IPoolReturnable so ObjectPool can wire the pool reference directly.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PointsPickup : MonoBehaviour, SickOfIt.Core.IPoolReturnable
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

        [Header("Audio")]
        [SerializeField] private AudioClip collectSound;
        [SerializeField] private AudioClip spawnSound;
        [SerializeField] private AudioSource audioSource;

        [Header("Visual")]
        [Tooltip("Optional: Bobbing amplitude once settled")]
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobSpeed = 3f;

        private float settledY;
        private float bornAt;
        private Vector3 origin;
        private float driftX;
        private bool motionReady;
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

        /// <summary>
        /// IPoolReturnable implementation — ObjectPool calls this on creation.
        /// </summary>
        public void SetOwnerPool(ObjectPool p)
        {
            pool = p;
        }

        [SerializeField] private Collider2D col;

        private void OnEnable()
        {
            // Rewind turns the diamond back on. Keep the original toss.
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;

            ResetState();
        }

        private void ResetState()
        {
            // Make sure collider is a trigger
            if (col == null) col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;

            origin = transform.position;
            settledY = origin.y;
            driftX = Random.Range(-spreadRange, spreadRange);
            bornAt = GameClock.Instance != null ? GameClock.Instance.Time : 0f;
            motionReady = true;

            // Play spawn SFX
            if (spawnSound != null && audioSource != null)
                audioSource.PlayOneShot(spawnSound);
        }

        private void Update()
        {
            // Respect GameClock — freeze during pause/rewind
            var clock = GameClock.Instance;
            float dt = clock != null ? clock.DeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            if (!motionReady)
                ResetState();

            float age = clock != null ? clock.Time - bornAt : 0f;

            // Self-destruct / return to pool after lifetime
            if (lifetime > 0 && age >= lifetime)
            {
                ReturnOrDestroy();
                return;
            }

            // Position is a function of age, so rewind lands on the same toss.
            float g = Mathf.Max(0.01f, gravity);
            float settleAge = 2f * popForce / g;
            float xOffset = driftX * Mathf.Min(age, settleAge);
            float yOffset;

            if (age >= settleAge)
            {
                float bob = bobAmplitude > 0f ? Mathf.Sin((age - settleAge) * bobSpeed) * bobAmplitude : 0f;
                yOffset = (settledY - origin.y) + bob;
            }
            else
            {
                yOffset = popForce * age - 0.5f * g * age * age;
            }

            transform.position = origin + new Vector3(xOffset, yOffset, 0f);
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
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;

            if (!other.CompareTag("Player"))
                return;

            // Award points
            Vector2 at = transform.position;
            RogueliteManager.Instance.AddPoints(pointsValue);
            SickOfIt.Gameplay.FeelVfx.DiamondPickup(at);

            // Play collect SFX (PlayOneShot so it finishes even after return to pool)
            if (collectSound != null && audioSource != null)
                AudioSource.PlayClipAtPoint(collectSound, transform.position);

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
