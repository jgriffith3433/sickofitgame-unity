using UnityEngine;
using SickOfIt.Combat.Health;

namespace SickOfIt.Combat.Damage
{
    /// <summary>
    /// Deals damage on collision with objects that implement IDamageable
    /// (PlayerHealth, EnemyHealth). Use this on projectiles, enemies, etc.
    /// Pool-aware: returns to ObjectPool on hit instead of Destroy.
    /// </summary>
    public class DamageDealer : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int damageAmount = 1;
        [SerializeField] private bool destroyOnHit = true;

        [Header("Target Filtering")]
        [Tooltip("What tags can this damage? Default for player projectiles: Enemy")]
        [SerializeField] private string[] targetTags = { "Enemy" };
        [SerializeField] private bool canDamageMultipleTargets = false;

        private bool hasDealtDamage = false;
        private SickOfIt.Core.ObjectPool ownerPool;

        /// <summary>
        /// Set the pool this projectile belongs to. Called by ObjectPool or generator.
        /// </summary>
        public void SetOwnerPool(SickOfIt.Core.ObjectPool pool)
        {
            ownerPool = pool;
        }

        private void OnEnable()
        {
            // Reset damage state when re-used from pool
            hasDealtDamage = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Skip if already dealt damage and can't damage multiple
            if (hasDealtDamage && !canDamageMultipleTargets)
                return;

            // Check if target has correct tag
            bool isValidTarget = false;
            foreach (string tag in targetTags)
            {
                if (other.CompareTag(tag))
                {
                    isValidTarget = true;
                    break;
                }
            }

            if (!isValidTarget)
                return;

            // Try to deal damage via IDamageable (works for both PlayerHealth and EnemyHealth)
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                damageable.TakeDamage(damageAmount);
                hasDealtDamage = true;

                if (destroyOnHit)
                {
                    // Return to pool if available, otherwise destroy
                    if (ownerPool != null)
                        ownerPool.Return(gameObject);
                    else
                        Destroy(gameObject);
                }
            }
        }

        /// <summary>
        /// Set damage amount at runtime (useful for different enemy types)
        /// </summary>
        public void SetDamage(int damage)
        {
            damageAmount = damage;
        }

        /// <summary>
        /// Set what this can damage
        /// </summary>
        public void SetTargetTags(params string[] tags)
        {
            targetTags = tags;
        }
    }
}
