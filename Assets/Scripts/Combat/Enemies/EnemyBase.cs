using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Base class for all enemy types.
    /// RequireComponent ensures rb, health, audio exist — cached in Awake via serialized refs or GetComponent once.
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

        [Header("Audio")]
        [SerializeField] private AudioClip[] hurtSounds;
        [SerializeField] private AudioClip[] deathSounds;
        [SerializeField] [Range(0f, 1f)] private float hurtSoundHealthThreshold = 1f;
        [SerializeField] [Range(0f, 0.3f)] private float pitchVariation = 0.1f;

        [Header("Point Drops")]
        [SerializeField] private GameObject pointsPickupPrefab;
        [SerializeField] [Range(0f, 1f)] private float dropChance = 0.35f;
        [SerializeField] private int dropPointsValue = 1;
        [SerializeField] private int dropCount = 1;

        private ObjectPool pickupPool;

        [Header("Cached References")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Rigidbody2D rb;
        [SerializeField] protected EnemyHealth enemyHealth;
        [SerializeField] protected AudioSource audioSource;

        protected Transform player;
        protected IDamageable playerDamageable;

        /// <summary>
        /// Initialize from config + spawn config. Called by LevelRuntime after instantiation.
        /// </summary>
        public virtual void InitializeFromConfig(EnemyTypeConfig config, EnemySpawnConfig spawn)
        {
            moveSpeed = spawn.speedOverride > 0 ? spawn.speedOverride : config.defaultMoveSpeed;
            contactDamage = config.contactDamage;
            detectionRange = spawn.detectionRangeOverride > 0 ? spawn.detectionRangeOverride : config.detectionRange;

            if (config.hurtSounds != null && config.hurtSounds.Length > 0)
                hurtSounds = config.hurtSounds;
            if (config.deathSounds != null && config.deathSounds.Length > 0)
                deathSounds = config.deathSounds;
            hurtSoundHealthThreshold = config.hurtSoundHealthThreshold;
            pitchVariation = config.pitchVariation;

            // Pickup prefab comes from GameDefinition (generated), not from config
            pointsPickupPrefab = GameDefinition.Instance.pickupPrefab;
            dropChance = config.dropChance;
            dropPointsValue = config.dropPointsValue;
            dropCount = config.dropCount;
        }

        /// <summary>
        /// Set the player reference. Called by LevelRuntime after spawning.
        /// Avoids FindObjectOfType at runtime.
        /// </summary>
        public void SetPlayer(Transform playerTransform, IDamageable damageable)
        {
            player = playerTransform;
            playerDamageable = damageable;
        }

        /// <summary>
        /// Set the pickup pool. Called by LevelRuntime after spawning.
        /// When set, SpawnPointDrops uses pool.Get() instead of Instantiate.
        /// </summary>
        public void SetPickupPool(ObjectPool pool)
        {
            pickupPool = pool;
        }

        protected virtual void Awake()
        {
            // Cache required components (serialized refs preferred, fallback to GetComponent once)
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (enemyHealth != null)
            {
                enemyHealth.onDamaged.AddListener(OnDamaged);
                enemyHealth.onDeath.AddListener(OnDeath);
            }
        }

        protected virtual void OnEnable()
        {
            // Player ref is set via SetPlayer() by LevelRuntime.
            // No runtime lookup needed.
        }

        protected virtual void OnDestroy()
        {
            if (enemyHealth != null)
            {
                enemyHealth.onDamaged.RemoveListener(OnDamaged);
                enemyHealth.onDeath.RemoveListener(OnDeath);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (SickOfIt.Core.GameClock.Instance != null && SickOfIt.Core.GameClock.Instance.IsRewinding)
                return;

            if (player == null || !enemyHealth.IsAlive)
                return;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRange)
                UpdateBehavior();
        }

        protected abstract void UpdateBehavior();

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.CompareTag("Player")) return;

            // Use cached ref first, fall back only if null
            var target = playerDamageable;
            target?.TakeDamage(contactDamage);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        private void OnDamaged()
        {
            float healthPercent = (float)enemyHealth.CurrentHealth / enemyHealth.MaxHealth;
            if (healthPercent <= hurtSoundHealthThreshold)
                PlayRandomSound(hurtSounds);
        }

        private void OnDeath()
        {
            PlayRandomSound(deathSounds);
            SpawnPointDrops();
        }

        private void SpawnPointDrops()
        {
            if (dropChance <= 0f) return;
            if (pickupPool == null && pointsPickupPrefab == null) return;

            for (int i = 0; i < dropCount; i++)
            {
                if (Random.value > dropChance) continue;

                GameObject drop;
                if (pickupPool != null)
                    drop = pickupPool.Get(transform.position, Quaternion.identity);
                else
                    drop = Instantiate(pointsPickupPrefab, transform.position, Quaternion.identity);

                if (drop.TryGetComponent<PointsPickup>(out var pickup))
                {
                    pickup.SetPointsValue(dropPointsValue);
                    pickup.SetSettledY(transform.position.y);
                }
            }
        }

        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || audioSource == null) return;

            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip);
        }
    }
}
