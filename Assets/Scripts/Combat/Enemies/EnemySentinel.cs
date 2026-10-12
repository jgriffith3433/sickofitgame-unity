using UnityEngine;
using SickOfIt.Combat.Damage;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Data.Config;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// A stationary enemy that switches between standing and crouching based on player distance.
    ///
    /// Distance zones (closest to farthest):
    ///   1. <= standingAttackRange   : StandingAttack (melee)
    ///   2. <= crouchThreshold       : StandingIdle   (no attack)
    ///   3. <= crouchingAttackRange  : CrouchingIdle  (sweet spot - safe zone)
    ///   4.  > crouchingAttackRange  : CrouchingAttack (laser)
    ///
    /// Animator Controller setup:
    ///   Parameters: IsCrouching (bool), IsAttacking (bool)
    ///
    ///   Transitions FROM StandingIdle:
    ///     -> StandingAttack    : IsAttacking = true
    ///     -> StandingToCrouch  : IsCrouching = true
    ///
    ///   Transitions FROM CrouchingIdle:
    ///     -> CrouchingAttack   : IsAttacking = true
    ///     -> CrouchToStanding  : IsCrouching = false
    ///
    ///   One-shot states (Has Exit Time, no condition needed, loop = false):
    ///     StandingAttack   -> StandingIdle
    ///     StandingToCrouch -> CrouchingIdle
    ///     CrouchingAttack  -> CrouchingIdle
    ///     CrouchToStanding -> StandingIdle
    ///
    ///   Idle states loop.
    ///
    /// Animation events (mid-clip only, 2 total):
    ///   StandingAttack  - hit frame  : OnStandingAttackFrame  (on AnimationEventRelaySentinel)
    ///   CrouchingAttack - fire frame : OnCrouchAttackFrame    (on AnimationEventRelaySentinel)
    /// </summary>
    public class EnemySentinel : EnemyBase
    {
        // ── Inspector ────────────────────────────────────────────────────────────

        [Header("Sentinel Ranges")]
        [Tooltip("Distance at or below which the standing melee attack triggers.")]
        [SerializeField] private float standingAttackRange = 1.5f;
        [Tooltip("Distance above which the sentinel transitions to a crouching posture.")]
        [SerializeField] private float crouchThreshold = 5f;
        [Tooltip("Distance above which the crouching laser attack triggers.")]
        [SerializeField] private float crouchingAttackRange = 8f;

        [Header("Sentinel Attack Settings")]
        [SerializeField] private int standingAttackDamage = 2;

        [Header("Laser Projectile")]
        [SerializeField] private ObjectPool laserPool;
        [SerializeField] private float laserSpeed = 10f;
        [SerializeField] private Transform leftProjectileSpawn;
        [SerializeField] private Transform rightProjectileSpawn;
        [Tooltip("How long fired lasers live")]
        [SerializeField] private float laserLifetime = 5f;
        [Tooltip("Damage dealt by fired lasers")]
        [SerializeField] private int laserDamage = 1;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Facing")]
        [SerializeField] private bool facePlayer = true;

        // ── Animator parameter hashes ─────────────────────────────────────────────

        private static readonly int ParamIsCrouching = Animator.StringToHash("IsCrouching");
        private static readonly int ParamIsAttacking  = Animator.StringToHash("IsAttacking");

        // ── Private state ─────────────────────────────────────────────────────────


        // ── Unity lifecycle ───────────────────────────────────────────────────────

        public override void InitializeFromConfig(EnemyTypeConfig config, EnemySpawnConfig spawn)
        {
            base.InitializeFromConfig(config, spawn);
            standingAttackDamage = config.contactDamage;
            standingAttackRange = config.attackRange;
            laserSpeed = config.laserSpeed;
            crouchThreshold = config.crouchThreshold;
            crouchingAttackRange = config.crouchingAttackRange;
            facePlayer = config.faceMovementDirection;
            laserLifetime = config.projectileLifetime;
            laserDamage = config.projectileDamage;
        }

        /// <summary>Set at runtime by LevelRuntime after spawning.</summary>
        public void SetLaserPool(ObjectPool pool) => laserPool = pool;

        protected override void Awake()
        {
            base.Awake();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        // ── Core behavior ─────────────────────────────────────────────────────────

        protected override void UpdateBehavior()
        {
            if (player == null)
                return;

            UpdateFacing();

            float distance = Vector2.Distance(transform.position, player.position);
            bool shouldCrouch = distance > crouchThreshold;

            // Attack whenever the player is in the appropriate zone.
            // The Animator's Has Exit Time ensures each attack clip plays fully before re-evaluating.
            bool isAttacking = (!shouldCrouch && distance <= standingAttackRange)
                            || (shouldCrouch && distance > crouchingAttackRange);

            // Hand off to the Animator Controller - it handles all transitions
            animator.SetBool(ParamIsCrouching, shouldCrouch);
            animator.SetBool(ParamIsAttacking, isAttacking);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void UpdateFacing()
        {
            if (!facePlayer || spriteRenderer == null || player == null)
                return;

            spriteRenderer.flipX = player.position.x > transform.position.x;
        }

        private void DealMeleeDamage()
        {
            if (player == null || playerDamageable == null)
                return;

            if (Vector2.Distance(transform.position, player.position) <= standingAttackRange)
                playerDamageable.TakeDamage(standingAttackDamage);
        }

        private void FireLaser()
        {
            if (laserPool == null)
                return;

            bool facingLeft = spriteRenderer != null && !spriteRenderer.flipX;
            Transform spawnPoint = facingLeft ? leftProjectileSpawn : rightProjectileSpawn;

            if (spawnPoint == null)
            {
                Debug.LogWarning($"[EnemySentinel] No projectile spawn point for {(facingLeft ? "left" : "right")} side.");
                return;
            }

            Vector2 direction;
            if (player == null)
            {
                direction = facingLeft ? Vector2.left : Vector2.right;
            }
            else
            {
                direction = PlayerAimPoint() - (Vector2)spawnPoint.position;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = facingLeft ? Vector2.left : Vector2.right;
                else
                    direction.Normalize();
            }
            GameObject laser = laserPool.Get(spawnPoint.position, Quaternion.identity);

            // Use cached components — no per-fire GetComponent
            Rigidbody2D laserRb = laserPool.GetCachedRb(laser);
            if (laserRb != null)
                laserRb.linearVelocity = direction * laserSpeed;

            laserPool.CaptureLaunchState(laser);

            DamageDealer damageDealer = laser.GetComponent<DamageDealer>();
            if (damageDealer != null)
            {
                damageDealer.SetTargetTags("Player");
                damageDealer.SetDamage(laserDamage);
            }

            // ProjectileLifetime is already on the prefab, just set time (cached)
            var lifetime = laserPool.GetCachedLifetime(laser);
            if (lifetime != null)
                lifetime.SetLifetime(laserLifetime);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────────

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, standingAttackRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, crouchThreshold);

            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, crouchingAttackRange);
        }

        // ── Animation event hooks ─────────────────────────────────────────────────

        /// <summary>Called by AnimationEventRelaySentinel on the hit frame of StandingAttack.</summary>
        public void OnStandingAttackFrame() => DealMeleeDamage();

        /// <summary>Called by AnimationEventRelaySentinel on the fire frame of CrouchingAttack.</summary>
        public void OnCrouchAttackFrame() => FireLaser();
    }
}
