using UnityEngine;

namespace MobileControllerFramework
{
    /// <summary>
    /// Camera follow script with a dead zone for top-down perspective.
    /// Camera only moves when the target exits the safe area in the center of the screen.
    /// Supports smooth zoom-out that lerps the camera to center (0,0,-10) at full zoom.
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

        [Header("Zoom")]
        [Tooltip("Minimum camera size (fully zoomed in, follows target with bounds)")]
        public float minZoomSize = 1.7f;

        [Tooltip("Maximum camera size (fully zoomed out, camera centers at 0,0,-10)")]
        public float maxZoomSize = 3.6f;

        [Tooltip("How fast the camera zooms in/out (units per second)")]
        public float zoomSpeed = 1f;

        [Tooltip("Automatically start zooming out when the game starts")]
        public bool autoStartZoom = false;

        [Header("Debug")]
        [Tooltip("Show the dead zone area in the Scene view")]
        public bool showDebugGizmos = true;

        private Vector3 velocity = Vector3.zero;
        private Camera cam;

        /// <summary>
        /// True when the camera is actively zooming out, false when zooming back in.
        /// </summary>
        [HideInInspector]
        public bool isZoomingOut = false;

        /// <summary>
        /// The center position the camera lerps towards when fully zoomed out.
        /// </summary>
        private static readonly Vector3 ZoomOutCenter = new Vector3(0f, 0f, -10f);

        private void Start()
        {
            cam = GetComponent<Camera>();
            
            if (target == null)
            {
                // Try to find the player by tag
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                    Debug.Log("CameraFollow: Automatically found target - " + target.name);
                }
                else
                {
                    Debug.LogWarning("CameraFollow: No target assigned and couldn't find GameObject tagged 'Player'!");
                }
            }

            if (autoStartZoom)
            {
                isZoomingOut = true;
            }
        }

        private void LateUpdate()
        {
            if (target == null || cam == null) return;

            // --- Zoom ---
            float targetSize = isZoomingOut ? maxZoomSize : minZoomSize;
            cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, targetSize, zoomSpeed * Time.deltaTime);

            // Zoom factor: 0 = fully zoomed in, 1 = fully zoomed out
            float zoomRange = maxZoomSize - minZoomSize;
            float zoomT = zoomRange > 0f
                ? Mathf.Clamp01((cam.orthographicSize - minZoomSize) / zoomRange)
                : 0f;

            // --- Follow position (dead zone logic) ---
            Vector3 currentPos = transform.position;
            Vector3 targetPos = target.position;
            
            // Calculate dead zone boundaries (in world space)
            float halfDeadZoneWidth = deadZoneWidth * 0.5f;
            float halfDeadZoneHeight = deadZoneHeight * 0.5f;

            // Calculate how far the target is from the camera center
            float offsetX = targetPos.x - currentPos.x;
            float offsetY = targetPos.y - currentPos.y;

            // Desired camera position (starts at current position)
            Vector3 followPosition = currentPos;

            // Only move camera if target is outside the dead zone
            if (Mathf.Abs(offsetX) > halfDeadZoneWidth)
            {
                // Target is outside horizontal dead zone
                float targetX = targetPos.x - Mathf.Sign(offsetX) * halfDeadZoneWidth;
                followPosition.x = targetX;
            }

            if (Mathf.Abs(offsetY) > halfDeadZoneHeight)
            {
                // Target is outside vertical dead zone
                float targetY = targetPos.y - Mathf.Sign(offsetY) * halfDeadZoneHeight;
                followPosition.y = targetY;
            }

            // Apply bounds if enabled
            if (useBounds)
            {
                followPosition.x = Mathf.Clamp(followPosition.x, minBounds.x, maxBounds.x);
                followPosition.y = Mathf.Clamp(followPosition.y, minBounds.y, maxBounds.y);
            }

            // Keep the Z position unchanged
            followPosition.z = currentPos.z;

            // --- Lerp between follow position and zoom-out center based on zoom factor ---
            Vector3 desiredPosition = Vector3.Lerp(followPosition, ZoomOutCenter, zoomT);

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

        /// <summary>
        /// Start zooming out towards the full-screen view.
        /// </summary>
        public void ZoomOut()
        {
            isZoomingOut = true;
        }

        /// <summary>
        /// Start zooming back in to follow the target.
        /// </summary>
        public void ZoomIn()
        {
            isZoomingOut = false;
        }

        /// <summary>
        /// Toggle between zoomed-in and zoomed-out states.
        /// </summary>
        public void ToggleZoom()
        {
            isZoomingOut = !isZoomingOut;
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
