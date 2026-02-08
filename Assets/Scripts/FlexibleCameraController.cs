using UnityEngine;

/// <summary>
/// Alternative approach: Adjusts camera orthographic size to show more/less content
/// based on screen aspect ratio. No letterboxing, but visible content varies.
/// Use this if you want to utilize full screen space on all devices.
/// </summary>
[RequireComponent(typeof(Camera))]
public class FlexibleCameraController : MonoBehaviour
{
    [Header("Target Resolution")]
    [Tooltip("The resolution you designed your game for")]
    public Vector2 targetResolution = new Vector2(1920, 1080);

    [Header("Camera Settings")]
    [Tooltip("Base orthographic size at target aspect ratio")]
    public float baseOrthographicSize = 5f;

    [Tooltip("How to adapt to different aspect ratios")]
    public AdaptMode adaptMode = AdaptMode.FitWidth;

    public enum AdaptMode
    {
        FitWidth,   // Always show same width, height varies
        FitHeight,  // Always show same height, width varies
        Expand      // Show more content on larger screens (recommended for 2D)
    }

    [Header("Constraints")]
    [Tooltip("Minimum orthographic size (prevents showing too little)")]
    public float minOrthographicSize = 3f;
    
    [Tooltip("Maximum orthographic size (prevents showing too much)")]
    public float maxOrthographicSize = 10f;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showSafeBounds = true;

    private Camera cam;
    private float lastScreenWidth;
    private float lastScreenHeight;
    private float currentOrthoSize;

    void Awake()
    {
        cam = GetComponent<Camera>();
        
        if (cam == null)
        {
            Debug.LogError("FlexibleCameraController: No Camera component found!");
            enabled = false;
            return;
        }

        if (!cam.orthographic)
        {
            Debug.LogWarning("FlexibleCameraController: Camera is not orthographic! This script is designed for 2D orthographic cameras.");
        }

        UpdateCameraSize();
    }

    void Update()
    {
        // Update if screen size changed
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            UpdateCameraSize();
        }
    }

    void UpdateCameraSize()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float targetAspect = targetResolution.x / targetResolution.y;
        float screenAspect = (float)Screen.width / Screen.height;

        switch (adaptMode)
        {
            case AdaptMode.FitWidth:
                FitWidth(targetAspect, screenAspect);
                break;
            
            case AdaptMode.FitHeight:
                FitHeight(targetAspect, screenAspect);
                break;
            
            case AdaptMode.Expand:
                Expand(targetAspect, screenAspect);
                break;
        }

        // Apply constraints
        currentOrthoSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
        cam.orthographicSize = currentOrthoSize;

        if (showDebugInfo)
        {
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.nearClipPlane));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.nearClipPlane));
            float visibleWidth = topRight.x - bottomLeft.x;
            float visibleHeight = topRight.y - bottomLeft.y;

            Debug.Log($"FlexibleCamera: Screen={Screen.width}x{Screen.height} (AR: {screenAspect:F2}), " +
                      $"OrthoSize={currentOrthoSize:F2}, Visible={visibleWidth:F1}x{visibleHeight:F1} units");
        }
    }

    void FitWidth(float targetAspect, float screenAspect)
    {
        // Always show the same width, adjust height based on aspect ratio
        // Taller screens see more vertically, wider screens see less
        
        if (screenAspect < targetAspect)
        {
            // Screen is taller than target - increase ortho size to maintain width
            float multiplier = targetAspect / screenAspect;
            cam.orthographicSize = baseOrthographicSize * multiplier;
        }
        else
        {
            // Screen is wider than or equal to target
            cam.orthographicSize = baseOrthographicSize;
        }
    }

    void FitHeight(float targetAspect, float screenAspect)
    {
        // Always show the same height, adjust width based on aspect ratio
        // This is the default behavior of orthographic cameras
        cam.orthographicSize = baseOrthographicSize;
    }

    void Expand(float targetAspect, float screenAspect)
    {
        // Show more content on larger screens (recommended for most 2D games)
        // This is a balanced approach that doesn't cut off content
        
        if (screenAspect < targetAspect)
        {
            // Screen is taller - show more vertically
            float heightRatio = targetAspect / screenAspect;
            cam.orthographicSize = baseOrthographicSize * Mathf.Lerp(1f, heightRatio, 0.5f);
        }
        else
        {
            // Screen is wider - show more horizontally (automatically handled by aspect)
            cam.orthographicSize = baseOrthographicSize;
        }
    }

    // Get the visible world bounds
    public Rect GetVisibleBounds()
    {
        if (cam == null) return new Rect();

        float height = 2f * cam.orthographicSize;
        float width = height * cam.aspect;

        Vector2 center = transform.position;
        return new Rect(center.x - width / 2f, center.y - height / 2f, width, height);
    }

    // Get the target aspect ratio bounds (safe zone)
    public Rect GetSafeAspectBounds()
    {
        if (cam == null) return new Rect();

        float targetAspect = targetResolution.x / targetResolution.y;
        float screenAspect = cam.aspect;

        float height = 2f * baseOrthographicSize;
        float width = height * targetAspect;

        Vector2 center = transform.position;
        return new Rect(center.x - width / 2f, center.y - height / 2f, width, height);
    }

    void OnDrawGizmos()
    {
        if (!showSafeBounds || cam == null) return;

        // Draw visible bounds in cyan
        Rect visible = GetVisibleBounds();
        Gizmos.color = Color.cyan;
        DrawRectGizmo(visible);

        // Draw safe bounds in yellow
        Rect safe = GetSafeAspectBounds();
        Gizmos.color = Color.yellow;
        DrawRectGizmo(safe);
    }

    void DrawRectGizmo(Rect rect)
    {
        Vector3 bottomLeft = new Vector3(rect.xMin, rect.yMin, 0);
        Vector3 bottomRight = new Vector3(rect.xMax, rect.yMin, 0);
        Vector3 topLeft = new Vector3(rect.xMin, rect.yMax, 0);
        Vector3 topRight = new Vector3(rect.xMax, rect.yMax, 0);

        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }

    void OnValidate()
    {
        if (Application.isPlaying && cam != null)
        {
            UpdateCameraSize();
        }
    }

    // Helper method to check if a world position is in the safe area
    public bool IsInSafeArea(Vector3 worldPosition)
    {
        Rect safeBounds = GetSafeAspectBounds();
        return safeBounds.Contains(new Vector2(worldPosition.x, worldPosition.y));
    }
}
