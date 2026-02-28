using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using SickOfIt.Combat.Damage;
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
        [SerializeField] private string laserProjectileAddress = "Projectile_Laser";
        [SerializeField] private float laserSpeed = 10f;
        [SerializeField] private Transform leftProjectileSpawn;
        [SerializeField] private Transform rightProjectileSpawn;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Facing")]
        [SerializeField] private bool facePlayer = true;

        // ── Animator parameter hashes ─────────────────────────────────────────────

        private static readonly int ParamIsCrouching = Animator.StringToHash("IsCrouching");
        private static readonly int ParamIsAttacking  = Animator.StringToHash("IsAttacking");

        // ── Private state ─────────────────────────────────────────────────────────

        private GameObject cachedLaserPrefab;
        private bool isLaserReady = false;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        public override void InitializeFromConfig(EnemyTemplate template, EnemySpawnConfig spawn)
        {
            base.InitializeFromConfig(template, spawn);
            standingAttackDamage = template.contactDamage;
            standingAttackRange = template.attackRange;
        }

        protected override void Awake()
        {
            base.Awake();

            rb.constraints = RigidbodyConstraints2D.FreezeAll;

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            LoadLaserPrefab();
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
            if (!isLaserReady || cachedLaserPrefab == null)
                return;

            bool facingLeft = spriteRenderer != null && !spriteRenderer.flipX;
            Transform spawnPoint = facingLeft ? leftProjectileSpawn : rightProjectileSpawn;

            if (spawnPoint == null)
            {
                Debug.LogWarning($"[EnemySentinel] No projectile spawn point for {(facingLeft ? "left" : "right")} side.");
                return;
            }

            Vector2 direction = facingLeft ? Vector2.left : Vector2.right;
            GameObject laser = Instantiate(cachedLaserPrefab, spawnPoint.position, Quaternion.identity);

            Rigidbody2D laserRb = laser.GetComponent<Rigidbody2D>();
            if (laserRb != null)
                laserRb.linearVelocity = direction * laserSpeed;

            DamageDealer damageDealer = laser.GetComponent<DamageDealer>();
            if (damageDealer != null)
                damageDealer.SetTargetTags("Player");

            Destroy(laser, 5f);
        }

        private void LoadLaserPrefab()
        {
            Addressables.LoadAssetAsync<GameObject>(laserProjectileAddress).Completed +=
                (AsyncOperationHandle<GameObject> handle) =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        cachedLaserPrefab = handle.Result;
                        isLaserReady = true;
                    }
                    else
                    {
                        Debug.LogError($"[EnemySentinel] Failed to load laser prefab: '{laserProjectileAddress}'");
                    }
                };
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
