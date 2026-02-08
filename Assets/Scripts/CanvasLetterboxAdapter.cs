using UnityEngine;

/// <summary>
/// Adapts a Screen Space - Overlay Canvas to respect letterboxing/pillarboxing
/// applied by AspectRatioController. Attach this to your Canvas.
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(RectTransform))]
public class CanvasLetterboxAdapter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera with AspectRatioController (usually Main Camera)")]
    public Camera targetCamera;

    [Header("Settings")]
    [Tooltip("Update every frame (enable if aspect ratio can change during gameplay)")]
    public bool continuousUpdate = true;

    [Tooltip("Show debug information")]
    public bool showDebug = false;

    private Canvas canvas;
    private RectTransform rectTransform;
    private Rect lastCameraRect;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        rectTransform = GetComponent<RectTransform>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            Debug.LogError("CanvasLetterboxAdapter: No target camera assigned and couldn't find Main Camera!");
            enabled = false;
            return;
        }

        // Ensure canvas is in Screen Space - Overlay mode
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogWarning("CanvasLetterboxAdapter: Canvas is not in Screen Space - Overlay mode. " +
                           "Consider using Screen Space - Camera instead, or this script will change it to Overlay.");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        UpdateCanvasRect();
    }

    private void Update()
    {
        if (continuousUpdate && targetCamera.rect != lastCameraRect)
        {
            UpdateCanvasRect();
        }
    }

    private void UpdateCanvasRect()
    {
        if (targetCamera == null) return;

        Rect cameraRect = targetCamera.rect;
        lastCameraRect = cameraRect;

        // Calculate the screen-space rect that matches the camera viewport
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        float canvasX = cameraRect.x * screenWidth;
        float canvasY = cameraRect.y * screenHeight;
        float canvasWidth = cameraRect.width * screenWidth;
        float canvasHeight = cameraRect.height * screenHeight;

        // Apply to RectTransform
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0, 0);
        
        rectTransform.anchoredPosition = new Vector2(canvasX, canvasY);
        rectTransform.sizeDelta = new Vector2(canvasWidth, canvasHeight);

        if (showDebug)
        {
            Debug.Log($"CanvasLetterboxAdapter: Camera rect={cameraRect}, " +
                     $"Canvas size={canvasWidth}x{canvasHeight} at ({canvasX},{canvasY})");
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && rectTransform != null)
        {
            UpdateCanvasRect();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Update Canvas Rect Now")]
    private void UpdateCanvasRectMenu()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        if (targetCamera == null)
            targetCamera = Camera.main;
        
        UpdateCanvasRect();
    }
#endif
}
