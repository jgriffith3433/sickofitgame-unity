using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Base class for all enemy types
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(AudioSource))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Enemy Settings")]
        [SerializeField] protected float moveSpeed = 2f;
        [SerializeField] protected int contactDamage = 1;

        [Header("Detection")]
        [SerializeField] protected float detectionRange = 10f;
        [SerializeField] protected LayerMask playerLayer;

        [Header("Audio")]
        [SerializeField] private AudioClip[] hurtSounds;
        [SerializeField] private AudioClip[] deathSounds;
        [SerializeField] [Range(0f, 1f)] [Tooltip("Play hurt sounds only when health is below this percentage (0 = always, 1 = never)")]
        private float hurtSoundHealthThreshold = 1f;
        [SerializeField] [Range(0f, 0.3f)] [Tooltip("Random pitch variation for audio variety")]
        private float pitchVariation = 0.1f;

        [Header("Point Drops")]
        [Tooltip("Prefab of the PointsPickup to spawn on death. Leave null to disable drops.")]
        [SerializeField] private GameObject pointsPickupPrefab;
        [Tooltip("Chance (0-1) that a point pickup drops on death")]
        [SerializeField] [Range(0f, 1f)] private float dropChance = 0.35f;
        [Tooltip("Points value of each dropped pickup")]
        [SerializeField] private int dropPointsValue = 1;
        [Tooltip("How many pickups to drop (each rolls independently)")]
        [SerializeField] private int dropCount = 1;

        [SerializeField] protected SpriteRenderer spriteRenderer;
        protected Transform player;
        protected IDamageable playerDamageable;
        protected Rigidbody2D rb;
        protected EnemyHealth enemyHealth;
        protected AudioSource audioSource;

        /// <summary>
        /// Initialize this enemy from a template + spawn config.
        /// Called by LevelRuntime after instantiation.
        /// Override fields (> 0) replace template defaults.
        /// </summary>
        public virtual void InitializeFromConfig(EnemyTemplate template, EnemySpawnConfig spawn)
        {
            moveSpeed = spawn.speedOverride > 0 ? spawn.speedOverride : template.defaultMoveSpeed;
            contactDamage = template.contactDamage;
            detectionRange = spawn.detectionRangeOverride > 0 ? spawn.detectionRangeOverride : template.detectionRange;

            // Audio
            if (template.hurtSounds != null && template.hurtSounds.Length > 0)
                hurtSounds = template.hurtSounds;
            if (template.deathSounds != null && template.deathSounds.Length > 0)
                deathSounds = template.deathSounds;
            hurtSoundHealthThreshold = template.hurtSoundHealthThreshold;
            pitchVariation = template.pitchVariation;

            // Drops
            if (template.pointsPickupPrefab != null)
                pointsPickupPrefab = template.pointsPickupPrefab;
            dropChance = template.dropChance;
            dropPointsValue = template.dropPointsValue;
            dropCount = template.dropCount;
        }

        protected virtual void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            rb = GetComponent<Rigidbody2D>();
            enemyHealth = GetComponent<EnemyHealth>();
            audioSource = GetComponent<AudioSource>();

            // Setup rigidbody for 2D movement
            rb.gravityScale = 0; // Top-down style, no gravity
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Subscribe to health events
            if (enemyHealth != null)
            {
                enemyHealth.onDamaged.AddListener(OnDamaged);
                enemyHealth.onDeath.AddListener(OnDeath);
            }

            // Find player (including inactive)
            var allObjects = FindObjectsOfType<GameObject>(true);
            var playerObj = System.Array.Find(allObjects, obj => obj.CompareTag("Player"));
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerDamageable = playerObj.GetComponentInChildren<IDamageable>(true);
            }
        }

        protected virtual void OnDestroy()
        {
            // Unsubscribe from health events
            if (enemyHealth != null)
            {
                enemyHealth.onDamaged.RemoveListener(OnDamaged);
                enemyHealth.onDeath.RemoveListener(OnDeath);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (player == null || !enemyHealth.IsAlive)
                return;

            // Check if player is in range
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRange)
            {
                UpdateBehavior();
            }
        }

        /// <summary>
        /// Override this for specific enemy behavior
        /// </summary>
        protected abstract void UpdateBehavior();

        /// <summary>
        /// Deal damage to player on contact
        /// </summary>
        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player"))
            {
                IDamageable target = playerDamageable;
                if (target == null)
                    target = collision.gameObject.GetComponentInChildren<IDamageable>(true);

                if (target != null)
                    target.TakeDamage(contactDamage);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            // Draw detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        /// <summary>
        /// Called when enemy takes damage
        /// </summary>
        private void OnDamaged()
        {
            // Check if health is below threshold
            float healthPercent = (float)enemyHealth.CurrentHealth / enemyHealth.MaxHealth;
            if (healthPercent <= hurtSoundHealthThreshold)
            {
                PlayRandomSound(hurtSounds);
            }
        }

        /// <summary>
        /// Called when enemy dies
        /// </summary>
        private void OnDeath()
        {
            PlayRandomSound(deathSounds);

            // Roll for point drops
            SpawnPointDrops();
        }

        /// <summary>
        /// Spawn point pickup(s) at the enemy's position based on drop chance.
        /// </summary>
        private void SpawnPointDrops()
        {
            if (pointsPickupPrefab == null || dropChance <= 0f)
                return;

            for (int i = 0; i < dropCount; i++)
            {
                if (Random.value <= dropChance)
                {
                    GameObject drop = Instantiate(pointsPickupPrefab, transform.position, Quaternion.identity);
                    var pickup = drop.GetComponent<PointsPickup>();
                    if (pickup != null)
                    {
                        pickup.SetPointsValue(dropPointsValue);
                        pickup.SetSettledY(transform.position.y);
                    }
                }
            }
        }

        /// <summary>
        /// Play a random audio clip from the provided array with pitch variation
        /// </summary>
        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || audioSource == null)
                return;

            // Pick a random clip
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
                return;

            // Apply pitch variation
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            
            // Play the sound
            audioSource.PlayOneShot(clip);
        }
    }
}
