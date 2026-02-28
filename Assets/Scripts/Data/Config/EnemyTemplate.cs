using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// ScriptableObject defining an enemy type.
    /// One per enemy archetype (Chaser, Fighter, Sentinel, Shooter).
    /// EnemySpawnConfig references these by EnemyType enum.
    /// EnemyBase reads stats from this at spawn time.
    /// </summary>
    [CreateAssetMenu(menuName = "Sick Of It/Enemy Template")]
    public class EnemyTemplate : ScriptableObject
    {
        [Header("Identity")]
        public EnemyType type;
        public string displayName;
        public GameObject prefab;

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
        public GameObject pointsPickupPrefab;
        [Range(0f, 1f)]
        public float dropChance = 0.35f;
        public int dropPointsValue = 1;
        public int dropCount = 1;

        [Header("Shooter Settings")]
        public float fireRate = 1f;
        public float projectileSpeed = 5f;
        public string projectileAddress = "Projectile_Enemy";

        [Header("Fighter/Sentinel Settings")]
        public float attackRange = 2f;
        public float attackCooldown = 1f;
    }
}
