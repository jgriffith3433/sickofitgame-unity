using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// ScriptableObject defining an enemy type.
    /// One per enemy archetype (Chaser, Fighter, Sentinel, Shooter).
    /// EnemySpawnConfig references these by EnemyType enum.
    /// EnemyBase reads stats from this at spawn time.
    /// EnemyPrefabGenerator uses this to create enemy prefabs.
    /// </summary>
    [CreateAssetMenu(menuName = "Sick Of It/Enemy Template")]
    public class EnemyTemplate : ScriptableObject
    {
        [Header("Identity")]
        public EnemyType type;
        public string displayName;
        [Tooltip("Generated prefab (set by EnemyPrefabGenerator)")]
        public GameObject prefab;

        [Header("Sprite/Animation (for generator)")]
        [Tooltip("Sprite sheet texture (sliced in Multiple mode). Used as fallback for states that don't specify their own sheet.")]
        public Texture2D spriteSheet;
        [Tooltip("Animation frames per second")]
        public int animationFps = 8;

        [Header("Animation Config (for generator)")]
        [Tooltip("Full animation setup: states, transitions, parameters, events. If set, the generator builds a multi-state animator. If null, falls back to a single idle clip from spriteSheet.")]
        public AnimationConfig animationConfig;

        [Header("Base Stats")]
        public int defaultHealth = 3;
        public float defaultMoveSpeed = 2f;
        public int contactDamage = 1;
        public float detectionRange = 10f;

        [Header("Audio")]
        public AudioClip[] hurtSounds;
        public AudioClip[] deathSounds;
        [Range(0f, 1f)]
        public float hurtSoundHealthThreshold = 1f;
        [Range(0f, 0.3f)]
        public float pitchVariation = 0.1f;

        [Header("Drops")]
        [Range(0f, 1f)]
        public float dropChance = 0.35f;
        public int dropPointsValue = 1;
        public int dropCount = 1;

        [Header("Physics (for generator)")]
        [Tooltip("Collider radius for the generated prefab")]
        public float colliderRadius = 0.4f;
        [Tooltip("Rigidbody gravity scale (0 for floating enemies)")]
        public float gravityScale = 0f;
        [Tooltip("Rigidbody constraints")]
        public RigidbodyConstraints2D constraints = RigidbodyConstraints2D.FreezeRotation;

        [Header("Combat - Invincibility")]
        [Tooltip("Whether this enemy has invincibility frames after damage")]
        public bool hasInvincibilityFrames = false;
        public float invincibilityDuration = 0.5f;

        [Header("Behavior")]
        [Tooltip("Whether the enemy flips to face movement/player direction")]
        public bool faceMovementDirection = true;

        [Header("Health Bar")]
        public Vector2 healthBarOffset = new Vector2(0f, 0.6f);
        public Vector2 healthBarSize = new Vector2(35f, 3f);
        public Color healthBarColor = Color.red;

        [Header("Chaser Settings")]
        public float stopDistance = 0.5f;

        [Header("Shooter Settings")]
        public float fireRate = 1f;
        public float projectileSpeed = 5f;
        [Tooltip("How long enemy projectiles live before returning to pool")]
        public float projectileLifetime = 5f;
        [Tooltip("Damage dealt by enemy projectiles")]
        public int projectileDamage = 1;
        [Tooltip("Offset from center for fire point children (Shooter/Sentinel)")]
        public Vector2 firePointOffset = new Vector2(0.3f, 0f);

        [Header("Fighter/Sentinel Settings")]
        public float attackRange = 2f;
        public float attackCooldown = 1f;

        [Header("Fighter Behavior")]
        public float minStateChangeTime = 5f;
        public float maxStateChangeTime = 10f;

        [Header("Sentinel Settings")]
        public float laserSpeed = 10f;
        public float crouchThreshold = 5f;
        public float crouchingAttackRange = 8f;
    }
}
