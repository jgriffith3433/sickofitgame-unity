using UnityEngine;

namespace SickOfIt.Combat.Enemies
{
    /// <summary>
    /// Enemy that chases the player
    /// </summary>
    public class EnemyChaser : EnemyBase
    {
        [Header("Chaser Settings")]
        [SerializeField] private float stopDistance = 0.5f; // How close before stopping
        [SerializeField] private bool faceMovementDirection = true;

        protected override void UpdateBehavior()
        {
            if (player == null)
                return;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // Only move if not too close
            if (distanceToPlayer > stopDistance)
            {
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
        }
    }
}
