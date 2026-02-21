using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Combat
{
    /// <summary>
    /// A collectable pickup that awards points when the player touches it.
    /// Spawned by enemies on death. Uses a trigger collider.
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

        /// <summary>
        /// Set the points value at runtime (used when spawning from enemy drops).
        /// </summary>
        public void SetPointsValue(int value)
        {
            pointsValue = value;
        }

        private void Start()
        {
            // Make sure collider is a trigger
            var col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;

            // Apply initial pop velocity
            float randomX = Random.Range(-spreadRange, spreadRange);
            velocity = new Vector2(randomX, popForce);

            if (lifetime > 0)
                Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (!settled)
            {
                // Apply gravity
                velocity.y -= gravity * Time.deltaTime;
                transform.position += (Vector3)velocity * Time.deltaTime;

                // Settle when falling below spawn height
                if (velocity.y < 0 && transform.position.y <= settledY)
                {
                    settled = true;
                    Vector3 pos = transform.position;
                    pos.y = settledY;
                    transform.position = pos;
                    settledTime = Time.time;
                }
            }
            else if (bobAmplitude > 0)
            {
                // Gentle bobbing once settled
                float elapsed = Time.time - settledTime;
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

            Destroy(gameObject);
        }
    }
}
