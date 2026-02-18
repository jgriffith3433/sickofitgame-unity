using UnityEngine;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Triggers instant death when the player enters this trigger zone.
    /// Attach to a GameObject with a Collider2D set to "Is Trigger".
    /// Examples: bottomless pits, hazards, kill zones, etc.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InstantDeathTrigger : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Optional: The cause of death for analytics tracking (e.g., 'fall', 'hazard', 'spike')")]
        [SerializeField] private string deathCause = "hazard";

        [Header("Debug")]
        [Tooltip("Visualize the trigger zone in the editor")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);

        private void Awake()
        {
            // Ensure this has a trigger collider
            Collider2D col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[InstantDeathTrigger] Collider on '{gameObject.name}' is not set to trigger. Setting to trigger now.");
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Only trigger on player
            if (!other.CompareTag("Player"))
                return;

            // Get the player's health system
            HealthSystem playerHealth = other.GetComponent<HealthSystem>();
            if (playerHealth == null)
            {
                Debug.LogError($"[InstantDeathTrigger] Player '{other.name}' does not have a HealthSystem component!");
                return;
            }

            // Only damage if player is alive
            if (!playerHealth.IsAlive)
                return;

            Debug.Log($"[InstantDeathTrigger] Player entered death trigger '{gameObject.name}'. Triggering instant death.");

            // Track the death cause if analytics is available
            if (!string.IsNullOrEmpty(deathCause))
            {
                SickOfIt.Core.AnalyticsManager.Instance.TrackPlayerDeath(deathCause);
            }

            // Deal lethal damage (9999 should be enough to kill any player)
            playerHealth.TakeDamage(9999);
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo)
                return;

            Collider2D col = GetComponent<Collider2D>();
            if (col == null)
                return;

            Gizmos.color = gizmoColor;

            // Draw based on collider type
            if (col is BoxCollider2D boxCol)
            {
                Matrix4x4 originalMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawCube(boxCol.offset, boxCol.size);
                Gizmos.matrix = originalMatrix;
            }
            else if (col is CircleCollider2D circleCol)
            {
                float radius = circleCol.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                Vector3 center = transform.position + (Vector3)circleCol.offset;
                Gizmos.DrawSphere(center, radius);
            }
            else if (col is PolygonCollider2D polyCol)
            {
                // Draw polygon outline
                for (int i = 0; i < polyCol.points.Length; i++)
                {
                    Vector2 p1 = transform.TransformPoint(polyCol.points[i]);
                    Vector2 p2 = transform.TransformPoint(polyCol.points[(i + 1) % polyCol.points.Length]);
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }
    }
}
