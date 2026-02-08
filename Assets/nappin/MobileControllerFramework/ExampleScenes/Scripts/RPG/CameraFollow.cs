using UnityEngine;

namespace MobileControllerFramework
{
    /// <summary>
    /// Camera follow script with a dead zone for top-down perspective.
    /// Camera only moves when the target exits the safe area in the center of the screen.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform to follow (usually the player)")]
        public Transform target;

        [Header("Dead Zone")]
        [Tooltip("Width of the safe area where camera doesn't move")]
        public float deadZoneWidth = 2f;
        
        [Tooltip("Height of the safe area where camera doesn't move")]
        public float deadZoneHeight = 1.5f;

        [Header("Movement")]
        [Tooltip("How smoothly the camera follows (lower = smoother, 0 = instant)")]
        [Range(0f, 1f)]
        public float smoothSpeed = 0.125f;

        [Header("Constraints")]
        [Tooltip("Enable to clamp camera position within bounds")]
        public bool useBounds = false;
        
        [Tooltip("Minimum world position for camera")]
        public Vector2 minBounds = new Vector2(-50, -50);
        
        [Tooltip("Maximum world position for camera")]
        public Vector2 maxBounds = new Vector2(50, 50);

        [Header("Debug")]
        [Tooltip("Show the dead zone area in the Scene view")]
        public bool showDebugGizmos = true;

        private Vector3 velocity = Vector3.zero;
        private Camera cam;

        private void Start()
        {
            cam = GetComponent<Camera>();
            
            if (target == null)
            {
                // Try to find the TopDownController automatically
                TopDownController controller = FindFirstObjectByType<TopDownController>();
                if (controller != null)
                {
                    target = controller.transform;
                    Debug.Log("CameraFollow: Automatically found target - " + target.name);
                }
                else
                {
                    Debug.LogWarning("CameraFollow: No target assigned and couldn't find TopDownController!");
                }
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Calculate the offset between camera and target
            Vector3 currentPos = transform.position;
            Vector3 targetPos = target.position;
            
            // Calculate dead zone boundaries (in world space)
            float halfDeadZoneWidth = deadZoneWidth * 0.5f;
            float halfDeadZoneHeight = deadZoneHeight * 0.5f;

            // Calculate how far the target is from the camera center
            float offsetX = targetPos.x - currentPos.x;
            float offsetY = targetPos.y - currentPos.y;

            // Desired camera position (starts at current position)
            Vector3 desiredPosition = currentPos;

            // Only move camera if target is outside the dead zone
            if (Mathf.Abs(offsetX) > halfDeadZoneWidth)
            {
                // Target is outside horizontal dead zone
                float targetX = targetPos.x - Mathf.Sign(offsetX) * halfDeadZoneWidth;
                desiredPosition.x = targetX;
            }

            if (Mathf.Abs(offsetY) > halfDeadZoneHeight)
            {
                // Target is outside vertical dead zone
                float targetY = targetPos.y - Mathf.Sign(offsetY) * halfDeadZoneHeight;
                desiredPosition.y = targetY;
            }

            // Apply bounds if enabled
            if (useBounds)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
            }

            // Keep the Z position unchanged (important for 2D/top-down)
            desiredPosition.z = currentPos.z;

            // Smooth camera movement
            if (smoothSpeed > 0)
            {
                transform.position = Vector3.SmoothDamp(currentPos, desiredPosition, ref velocity, smoothSpeed);
            }
            else
            {
                // Instant movement
                transform.position = desiredPosition;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw the dead zone box in the Scene view
            Gizmos.color = Color.yellow;
            Vector3 center = transform.position;
            Vector3 size = new Vector3(deadZoneWidth, deadZoneHeight, 0.1f);
            Gizmos.DrawWireCube(center, size);

            // Draw bounds if enabled
            if (useBounds)
            {
                Gizmos.color = Color.red;
                Vector3 boundsCenter = new Vector3(
                    (minBounds.x + maxBounds.x) * 0.5f,
                    (minBounds.y + maxBounds.y) * 0.5f,
                    transform.position.z
                );
                Vector3 boundsSize = new Vector3(
                    maxBounds.x - minBounds.x,
                    maxBounds.y - minBounds.y,
                    0.1f
                );
                Gizmos.DrawWireCube(boundsCenter, boundsSize);
            }

            // Draw line to target
            if (target != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }
}
