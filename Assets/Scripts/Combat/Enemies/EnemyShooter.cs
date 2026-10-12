using System.Collections;
using UnityEngine;
using SickOfIt.Combat.Damage;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Data.Config;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Enemy that shoots projectiles at the player
    /// </summary>
    public class EnemyShooter : EnemyBase
    {
        [Header("Shooter Settings")]
        [SerializeField] private ObjectPool projectilePool;
        [SerializeField] private float projectileSpeed = 5f;
        [SerializeField] private Transform leftFirePoint;
        [SerializeField] private Transform rightFirePoint;
        [SerializeField] private bool flipToFacePlayer = true;

        [Header("Fixed Direction Shooting")]
        [Tooltip("When enabled, shoots in a fixed direction instead of targeting the player.")]
        [SerializeField] private bool shootInFixedDirection = false;
        [Tooltip("The direction to shoot in when 'Shoot In Fixed Direction' is enabled. Does not need to be normalized.")]
        [SerializeField] private Vector2 fixedShootDirection = Vector2.right;

        [Header("Attack")]
        [SerializeField] private float attackInterval = 2f;
        [Tooltip("Delay before the first attack after the enemy spawns.")]
        [SerializeField] private float initialAttackDelay = 5f;
        [Tooltip("How long fired projectiles live")]
        [SerializeField] private float projectileLifetime = 5f;
        [Tooltip("Damage dealt by fired projectiles")]
        [SerializeField] private int projectileDamage = 1;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        private float lastAttackTime;

        private static readonly int ParamIsAttacking = Animator.StringToHash("IsAttacking");

        public override void InitializeFromConfig(EnemyTypeConfig config, EnemySpawnConfig spawn)
        {
            base.InitializeFromConfig(config, spawn);
            projectileSpeed = config.projectileSpeed;
            attackInterval = spawn.fireRateOverride > 0 ? spawn.fireRateOverride : config.fireRate;
            initialAttackDelay = spawn.initialAttackDelay;
            flipToFacePlayer = config.faceMovementDirection;
            projectileLifetime = config.projectileLifetime;
            projectileDamage = config.projectileDamage;

            // Apply shoot direction
            if (spawn.shootDirection != ShootDirection.AtPlayer)
            {
                shootInFixedDirection = true;
                fixedShootDirection = spawn.shootDirection == ShootDirection.Left ? Vector2.left : Vector2.right;
            }
        }

        /// <summary>Set at runtime by LevelRuntime after spawning.</summary>
        public void SetProjectilePool(ObjectPool pool) => projectilePool = pool;

        protected override void Awake()
        {
            base.Awake();

            // Shooter enemies should not move
            rb.constraints = RigidbodyConstraints2D.FreezeAll;

            // Apply initial attack delay so the first shot isn't immediate
            float currentTime = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
            lastAttackTime = currentTime - attackInterval + initialAttackDelay;

            // Load projectile prefab
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        protected override void UpdateBehavior()
        {
            if (player == null)
            {
                if (shootInFixedDirection && flipToFacePlayer && spriteRenderer != null)
                    spriteRenderer.flipX = fixedShootDirection.x < 0f;
                TryTriggerAttack();
                return;
            }

            if (flipToFacePlayer && spriteRenderer != null)
                spriteRenderer.flipX = player.position.x < transform.position.x;

            TryTriggerAttack();
        }

        private void TryTriggerAttack()
        {
            if (animator == null)
                return;

            float currentTime = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
            if (currentTime >= lastAttackTime + attackInterval)
            {
                lastAttackTime = currentTime;
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
            
            if (projectilePool == null || firePoint == null)
                return;

            // Leave from the body, not the cell pivot. The pivot sits under the drawn character.
            Vector2 origin = firePoint.position;
            if (spriteRenderer != null)
            {
                Bounds body = spriteRenderer.bounds;
                origin.y = body.center.y + body.extents.y * 0.25f;
            }

            Vector2 direction = ShotDirection(origin);
            if (direction.sqrMagnitude < 0.0001f)
                return;

            // Get projectile from pool
            GameObject projectile = projectilePool.Get(origin, Quaternion.identity);

            // Set projectile velocity (cached — no per-fire GetComponent)
            Rigidbody2D projectileRb = projectilePool.GetCachedRb(projectile);
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = direction * projectileSpeed;
            }

            projectilePool.CaptureLaunchState(projectile);

            // Make sure it can damage player
            DamageDealer damageDealer = projectile.GetComponent<DamageDealer>();
            if (damageDealer != null)
            {
                damageDealer.SetTargetTags("Player");
                damageDealer.SetDamage(projectileDamage);
            }

            // ProjectileLifetime is already on the prefab, just set time (cached)
            var lifetime = projectilePool.GetCachedLifetime(projectile);
            if (lifetime != null)
                lifetime.SetLifetime(projectileLifetime);
        }

        private Vector2 ShotDirection(Vector2 origin)
        {
            if (player != null)
            {
                Vector2 toPlayer = PlayerAimPoint() - origin;
                if (toPlayer.sqrMagnitude > 0.0001f)
                    return toPlayer.normalized;
            }

            if (shootInFixedDirection && fixedShootDirection.sqrMagnitude > 0.0001f)
                return fixedShootDirection.normalized;

            return Vector2.right;
        }

        /// <summary>
        /// Public method to be called by animation events (via AnimationEventRelayShooter)
        /// </summary>
        public void FireProjectile()
        {
            if (projectilePool != null)
            {
                Shoot();
            }
        }
    }
}
