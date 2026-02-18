using UnityEngine;
using SickOfIt.Combat.Health;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Enemy that walks to the player and performs melee attacks
    /// Switches between idle and walk states randomly
    /// </summary>
    public class EnemyFighter : EnemyBase
    {
        [Header("Fighter Settings")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackCooldown = 2f;
        [SerializeField] private int attackDamage = 2;
        [SerializeField] private float minStateChangeTime = 5f;
        [SerializeField] private float maxStateChangeTime = 10f;
        [SerializeField] private bool faceMovementDirection = true;

        [Header("Animation - Separate Animators")]
        [SerializeField] private GameObject idleAnimator;
        [SerializeField] private GameObject walkAnimator;
        [SerializeField] private GameObject attackAnimator;
        
        // Animation parameter names
        private const string ANIM_IDLE = "Idle";
        private const string ANIM_WALK = "Walk";
        private const string ANIM_ATTACK = "Attack";

        // State management
        private enum MovementState { Idle, Walk }
        private MovementState currentState = MovementState.Idle;
        private float nextStateChangeTime;
        private float nextAttackTime;
        private bool isAttacking = false;
        private GameObject currentActiveAnimator;

        protected override void Awake()
        {
            base.Awake();

            // Disable all animators initially
            if (idleAnimator != null) idleAnimator.SetActive(false);
            if (walkAnimator != null) walkAnimator.SetActive(false);
            if (attackAnimator != null) attackAnimator.SetActive(false);

            // Start with a random state
            currentState = Random.value > 0.5f ? MovementState.Idle : MovementState.Walk;
            SetRandomStateChangeTime();
            
            // Enable the appropriate animator based on starting state
            ActivateAnimator(currentState == MovementState.Idle ? idleAnimator : walkAnimator);
        }

        protected override void UpdateBehavior()
        {
            if (player == null || isAttacking)
                return;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // Always attack if player is close enough and cooldown is ready
            if (distanceToPlayer <= attackRange && Time.time >= nextAttackTime)
            {
                PerformAttack();
                return;
            }

            // Switch between idle and walk states based on timer
            if (Time.time >= nextStateChangeTime)
            {
                SwitchMovementState();
            }

            // Movement and animation based on current state
            if (currentState == MovementState.Walk)
            {
                MoveTowardsPlayer();
                PlayAnimation(ANIM_WALK);
            }
            else
            {
                PlayAnimation(ANIM_IDLE);
            }
        }

        private void MoveTowardsPlayer()
        {
            if (player == null)
                return;

            // Move toward player (ignoring Z for 2D)
            Vector2 playerPos2D = new Vector2(player.position.x, player.position.y);
            Vector2 currentPos2D = new Vector2(transform.position.x, transform.position.y);
            Vector2 direction = (playerPos2D - currentPos2D).normalized;
            Vector2 newPosition = Vector2.MoveTowards(
                currentPos2D,
                playerPos2D,
                moveSpeed * Time.deltaTime
            );

            rb.MovePosition(newPosition);

            // Face the direction of movement using sprite flip (doesn't affect child objects like health bars)
            if (faceMovementDirection && spriteRenderer != null)
            {
                if (direction.x > 0)
                {
                    spriteRenderer.flipX = false;
                }
                else if (direction.x < 0)
                {
                    spriteRenderer.flipX = true;
                }
            }
        }

        private void PerformAttack()
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;
            PlayAnimation(ANIM_ATTACK);

            // Deal damage immediately (can be changed to use animation event if needed)
            DealDamageToPlayer();

            // Reset attack state after animation (approximate time, adjust as needed)
            Invoke(nameof(ResetAttackState), 1f);
        }

        private void DealDamageToPlayer()
        {
            if (player == null)
                return;

            // Check if player is still in range
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer <= attackRange)
            {
                HealthSystem playerHealth = player.GetComponent<HealthSystem>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                }
            }
        }

        private void ResetAttackState()
        {
            isAttacking = false;
        }

        private void SwitchMovementState()
        {
            // Toggle between idle and walk
            currentState = currentState == MovementState.Idle ? MovementState.Walk : MovementState.Idle;
            SetRandomStateChangeTime();
        }

        private void SetRandomStateChangeTime()
        {
            float randomTime = Random.Range(minStateChangeTime, maxStateChangeTime);
            nextStateChangeTime = Time.time + randomTime;
        }

        private void PlayAnimation(string animationName)
        {
            GameObject targetAnimator = null;

            // Determine which animator to activate
            switch (animationName)
            {
                case ANIM_IDLE:
                    targetAnimator = idleAnimator;
                    break;
                case ANIM_WALK:
                    targetAnimator = walkAnimator;
                    break;
                case ANIM_ATTACK:
                    targetAnimator = attackAnimator;
                    break;
            }

            // Only switch if different from current
            if (targetAnimator != null && targetAnimator != currentActiveAnimator)
            {
                ActivateAnimator(targetAnimator);
            }
        }

        private void ActivateAnimator(GameObject animatorToActivate)
        {
            if (animatorToActivate == null)
                return;

            // Deactivate all animators
            if (idleAnimator != null) idleAnimator.SetActive(false);
            if (walkAnimator != null) walkAnimator.SetActive(false);
            if (attackAnimator != null) attackAnimator.SetActive(false);

            // Activate the target animator
            animatorToActivate.SetActive(true);
            currentActiveAnimator = animatorToActivate;
            
            // Update the spriteRenderer reference to the active one (optional, for compatibility)
            SpriteRenderer activeSpriteRenderer = animatorToActivate.GetComponent<SpriteRenderer>();
            if (activeSpriteRenderer != null)
            {
                spriteRenderer = activeSpriteRenderer;
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // Draw attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        /// <summary>
        /// Public method that can be called by animation events to deal damage
        /// This allows for precise timing with the attack animation
        /// </summary>
        public void OnAttackFrame()
        {
            DealDamageToPlayer();
        }
    }
}
