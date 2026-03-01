using UnityEngine;
using SickOfIt.Combat.Health;
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

        [Header("Cached References")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Rigidbody2D rb;
        [SerializeField] protected EnemyHealth enemyHealth;
        [SerializeField] protected AudioSource audioSource;

        protected Transform player;
        protected IDamageable playerDamageable;

        /// <summary>
        /// Initialize from template + spawn config. Called by LevelRuntime after instantiation.
        /// </summary>
        public virtual void InitializeFromConfig(EnemyTemplate template, EnemySpawnConfig spawn)
        {
            moveSpeed = spawn.speedOverride > 0 ? spawn.speedOverride : template.defaultMoveSpeed;
            contactDamage = template.contactDamage;
            detectionRange = spawn.detectionRangeOverride > 0 ? spawn.detectionRangeOverride : template.detectionRange;

            if (template.hurtSounds != null && template.hurtSounds.Length > 0)
                hurtSounds = template.hurtSounds;
            if (template.deathSounds != null && template.deathSounds.Length > 0)
                deathSounds = template.deathSounds;
            hurtSoundHealthThreshold = template.hurtSoundHealthThreshold;
            pitchVariation = template.pitchVariation;

            if (template.pointsPickupPrefab != null)
                pointsPickupPrefab = template.pointsPickupPrefab;
            dropChance = template.dropChance;
            dropPointsValue = template.dropPointsValue;
            dropCount = template.dropCount;
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
            // Re-find player when reactivated (rewind may reactivate enemies)
            if (player == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    player = playerObj.transform;
                    playerDamageable = playerObj.GetComponentInChildren<IDamageable>(true);
                }
            }
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
            if (pointsPickupPrefab == null || dropChance <= 0f) return;

            for (int i = 0; i < dropCount; i++)
            {
                if (Random.value > dropChance) continue;

                var drop = Instantiate(pointsPickupPrefab, transform.position, Quaternion.identity);
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
