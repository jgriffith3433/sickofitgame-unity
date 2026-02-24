using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using SickOfIt.Combat.Damage;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Enemy that shoots projectiles at the player
    /// </summary>
    public class EnemyShooter : EnemyBase
    {
        [Header("Shooter Settings")]
        [SerializeField] private string projectileAddress = "Projectile_Enemy";
        [SerializeField] private float projectileSpeed = 5f;
        [SerializeField] private Transform leftFirePoint;
        [SerializeField] private Transform rightFirePoint;
        [SerializeField] private bool flipToFacePlayer = true;

        [Header("Fixed Direction Shooting")]
        [Tooltip("When enabled, shoots in a fixed direction instead of targeting the player.")]
        [SerializeField] private bool shootInFixedDirection = false;
        [Tooltip("The direction to shoot in when 'Shoot In Fixed Direction' is enabled. Does not need to be normalized.")]
        [SerializeField] private Vector2 fixedShootDirection = Vector2.right;

        private GameObject cachedProjectilePrefab;
        private bool isProjectileReady = false;

        [Header("Attack")]
        [SerializeField] private float attackInterval = 2f;
        [Tooltip("Delay before the first attack after the enemy spawns.")]
        [SerializeField] private float initialAttackDelay = 5f;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        private float lastAttackTime;

        private static readonly int ParamIsAttacking = Animator.StringToHash("IsAttacking");

        protected override void Awake()
        {
            base.Awake();

            // Shooter enemies should not move
            rb.constraints = RigidbodyConstraints2D.FreezeAll;

            // Apply initial attack delay so the first shot isn't immediate
            lastAttackTime = Time.time - attackInterval + initialAttackDelay;

            // Load projectile prefab
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            LoadProjectilePrefab();
        }

        private void LoadProjectilePrefab()
        {
            Addressables.LoadAssetAsync<GameObject>(projectileAddress).Completed += (AsyncOperationHandle<GameObject> handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    cachedProjectilePrefab = handle.Result;
                    isProjectileReady = true;
                }
                else
                {
                    Debug.LogError($"Failed to load projectile: {projectileAddress}");
                }
            };
        }

        protected override void UpdateBehavior()
        {
            if (shootInFixedDirection)
            {
                if (flipToFacePlayer)
                {
                    // Flip sprite to match the fixed shoot direction
                    spriteRenderer.flipX = fixedShootDirection.x < 0f;
                }
                // Even when shooting in a fixed direction, attacker should periodically enter Attack state
                TryTriggerAttack();
                return;
            }

            if (player == null)
                return;

            if (flipToFacePlayer)
            {
                // Flip sprite to face player
                if (player.position.x < transform.position.x)
                    spriteRenderer.flipX = true;
                else
                    spriteRenderer.flipX = false;
            }

            // If player is present, periodically trigger attack animation
            TryTriggerAttack();
        }

        private void TryTriggerAttack()
        {
            if (animator == null)
                return;

            if (Time.time >= lastAttackTime + attackInterval)
            {
                lastAttackTime = Time.time;
                animator.SetBool(ParamIsAttacking, true);
                StartCoroutine(ClearAttackBoolNextFrame());
            }
        }

        private IEnumerator ClearAttackBoolNextFrame()
        {
            yield return null;
            if (animator != null)
                animator.SetBool(ParamIsAttacking, false);
        }

        private void Shoot()
        {
            // Choose fire point based on facing direction
            Transform firePoint = spriteRenderer.flipX ? leftFirePoint : rightFirePoint;
            
            if (cachedProjectilePrefab == null || firePoint == null)
                return;

            // Calculate shoot direction
            Vector2 direction;
            if (shootInFixedDirection)
            {
                direction = fixedShootDirection.normalized;
                if (direction == Vector2.zero)
                    direction = Vector2.right;
            }
            else
            {
                if (player == null)
                    return;

                // Calculate direction to player (ignoring Z for 2D)
                Vector2 playerPos2D = new Vector2(player.position.x, player.position.y);
                Vector2 firePointPos2D = new Vector2(firePoint.position.x, firePoint.position.y);
                direction = (playerPos2D - firePointPos2D).normalized;
            }

            // Spawn projectile
            GameObject projectile = Instantiate(cachedProjectilePrefab, firePoint.position, Quaternion.identity);

            // Set projectile velocity
            Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = direction * projectileSpeed;
            }

            // Make sure it can damage player
            DamageDealer damageDealer = projectile.GetComponent<DamageDealer>();
            if (damageDealer != null)
            {
                damageDealer.SetTargetTags("Player");
            }

            // Auto-destroy after a while
            Destroy(projectile, 5f);
        }

        /// <summary>
        /// Public method to be called by animation events (via AnimationEventRelayShooter)
        /// </summary>
        /// <param name="spawnPoint">Optional spawn location. If null, uses the default firePoint.</param>
        public void FireProjectile()
        {
            if (isProjectileReady)
            {
                Shoot();
            }
        }
    }
}
