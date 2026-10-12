using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data.Config;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Enemy that walks to the player and performs melee attacks.
    /// Randomly switches between idle and walk, always attacks when in range.
    ///
    /// Animator Controller setup:
    ///   Parameters: IsWalking (bool), IsAttacking (bool)
    ///
    ///   Transitions:
    ///     Idle   -> Walk   : IsWalking = true
    ///     Walk   -> Idle   : IsWalking = false
    ///     Any    -> Attack : IsAttacking = true
    ///     Attack -> Idle   : Has Exit Time, IsAttacking = false
    ///     Attack -> Walk   : Has Exit Time, IsAttacking = false, IsWalking = true
    ///
    /// Animation events:
    ///   Attack clip - hit frame : OnAttackFrame  (on AnimationEventRelayFighter)
    /// </summary>
    public class EnemyFighter : EnemyBase
    {
        [Header("Fighter Settings")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private int attackDamage = 2;
        [SerializeField] private float minStateChangeTime = 5f;
        [SerializeField] private float maxStateChangeTime = 10f;
        [SerializeField] private float attackCooldown;
        [SerializeField] private bool faceMovementDirection = true;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        // â”€â”€ Animator parameter hashes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static readonly int ParamIsWalking   = Animator.StringToHash("IsWalking");
        private static readonly int ParamIsAttacking = Animator.StringToHash("IsAttacking");

        // â”€â”€ Private state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool isWalking = false;
        private float nextStateChangeTime;
        private float attackReadyTime;
        private float attackUntil;

        public override void InitializeFromConfig(EnemyTypeConfig config, EnemySpawnConfig spawn)
        {
            base.InitializeFromConfig(config, spawn);
            attackRange = config.attackRange;
            attackDamage = config.contactDamage;
            minStateChangeTime = config.minStateChangeTime;
            maxStateChangeTime = config.maxStateChangeTime;
            attackCooldown = config.attackCooldown;
            faceMovementDirection = config.faceMovementDirection;
        }

        protected override void Awake()
        {
            base.Awake();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            // Start in a random state
            isWalking = Random.value > 0.5f;
            SetRandomStateChangeTime();
        }

        protected override void UpdateBehavior()
        {
            if (player == null)
                return;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            bool inRange = distanceToPlayer <= attackRange;
            float now = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
            bool isAttacking;
            if (attackCooldown <= 0f)
            {
                isAttacking = inRange;
            }
            else
            {
                if (inRange && now >= attackReadyTime)
                {
                    attackUntil = now + Mathf.Min(0.35f, attackCooldown);
                    attackReadyTime = now + attackCooldown;
                }
                isAttacking = now < attackUntil;
            }

            if (!isAttacking)
            {
                // Randomly switch between idle and walk
                float currentTime = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
                if (currentTime >= nextStateChangeTime)
                {
                    isWalking = !isWalking;
                    SetRandomStateChangeTime();
                }

                if (isWalking)
                    MoveTowardsPlayer();
            }

            animator.SetBool(ParamIsWalking, isWalking && !isAttacking);
            animator.SetBool(ParamIsAttacking, isAttacking);
        }

        private void MoveTowardsPlayer()
        {
            if (player == null)
                return;

            Vector2 playerPos2D = new Vector2(player.position.x, player.position.y);
            Vector2 currentPos2D = new Vector2(transform.position.x, transform.position.y);
            Vector2 direction = (playerPos2D - currentPos2D).normalized;
            Vector2 newPosition = Vector2.MoveTowards(
                currentPos2D,
                playerPos2D,
                moveSpeed * (GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime)
            );

            rb.MovePosition(newPosition);

            if (faceMovementDirection && spriteRenderer != null)
            {
                if (direction.x > 0)
                    spriteRenderer.flipX = false;
                else if (direction.x < 0)
                    spriteRenderer.flipX = true;
            }
        }

        private void DealDamageToPlayer()
        {
            if (player == null || playerDamageable == null)
                return;

            if (Vector2.Distance(transform.position, player.position) <= attackRange)
                playerDamageable.TakeDamage(attackDamage);
        }

        private void SetRandomStateChangeTime()
        {
            float currentTime = GameClock.Instance != null ? GameClock.Instance.Time : Time.time;
            nextStateChangeTime = currentTime + Random.Range(minStateChangeTime, maxStateChangeTime);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        /// <summary>Called by AnimationEventRelayFighter on the hit frame of the Attack clip.</summary>
        public void OnAttackFrame() => DealDamageToPlayer();
    }
}
