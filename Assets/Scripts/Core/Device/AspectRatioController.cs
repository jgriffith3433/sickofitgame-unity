using UnityEngine;

namespace SickOfIt.Core.Device
{
    /// <summary>
    /// Manages camera viewport to maintain a target aspect ratio (e.g., 16:9)
    /// by adding letterboxing (black bars on top/bottom) or pillarboxing (black bars on sides)
    /// when the screen aspect ratio differs from the target.
    /// </summary>
    public class AspectRatioController : MonoBehaviour
    {
        [Header("Target Aspect Ratio")]
        [Tooltip("Target width of aspect ratio (e.g., 16 for 16:9)")]
        public float targetAspectWidth = 16f;

        [Tooltip("Target height of aspect ratio (e.g., 9 for 16:9)")]
        public float targetAspectHeight = 9f;

        [Header("Letterbox Settings")]
        [Tooltip("Color of the letterbox bars")]
        public Color letterboxColor = Color.black;

        [Header("Fit Mode")]
        [Tooltip("Fit Inside: Letterbox to fit entire game (recommended). Fit Outside: Crop edges to fill screen.")]
        public FitMode fitMode = FitMode.FitInside;

        public enum FitMode
        {
            FitInside,  // Letterbox/pillarbox to show all content
            FitOutside  // Crop edges to fill the screen
        }

        private Camera cam;
        private Camera clearCamera;
        private bool fillPortrait;
        private float landscapeOrthoSize;
        private float lastScreenWidth;
        private float lastScreenHeight;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogError("AspectRatioController: No Camera component found!");
                enabled = false;
                return;
            }

            // Camera.rect only clears its own rectangle. The skip/rewind UI sits in
            // the rest of the screen, so that area has to be wiped every frame too.
            EnsureClearCamera();
            EnsureResizeWatcher();
            UpdateCameraRect();
        }

        private void EnsureClearCamera()
        {
            var existing = transform.Find("BackgroundClear");
            if (existing != null)
                clearCamera = existing.GetComponent<Camera>();

            if (clearCamera == null)
            {
                var go = new GameObject("BackgroundClear");
                go.transform.SetParent(transform, false);
                clearCamera = go.AddComponent<Camera>();
            }

            clearCamera.orthographic = true;
            clearCamera.cullingMask = 0;
            clearCamera.clearFlags = CameraClearFlags.SolidColor;
            clearCamera.rect = new Rect(0f, 0f, 1f, 1f);
            clearCamera.allowHDR = cam.allowHDR;
            clearCamera.allowMSAA = cam.allowMSAA;
            clearCamera.depth = cam.depth - 1f;
        }

        /// <summary>Share of the screen height the playfield takes on a portrait phone; the rest is for the thumbs.</summary>
        public const float PortraitPlayfieldFraction = 0.6f;

        /// <summary>Set after every layout: the share of the screen the playfield covers (tests and the playtest read it).</summary>
        public Rect LastViewport => cam != null ? cam.rect : new Rect(0f, 0f, 1f, 1f);

        // No per-frame poll: a hidden overlay canvas resizes with the window, and its child tells us (a resize,
        // a rotation, a browser bar sliding away) through OnRectTransformDimensionsChange.
        private void EnsureResizeWatcher()
        {
            if (transform.Find("ScreenSizeWatcher") != null) return;
            var go = new GameObject("ScreenSizeWatcher", typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -32000;
            var child = new GameObject("Watch", typeof(RectTransform), typeof(ScreenSizeWatcher));
            child.transform.SetParent(go.transform, false);
            var rt = (RectTransform)child.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            child.GetComponent<ScreenSizeWatcher>().owner = this;
        }

        private sealed class ScreenSizeWatcher : MonoBehaviour
        {
            public AspectRatioController owner;
            private void OnRectTransformDimensionsChange()
            {
                if (owner != null && owner.isActiveAndEnabled
                    && (Screen.width != owner.lastScreenWidth || Screen.height != owner.lastScreenHeight))
                    owner.UpdateCameraRect();
            }
        }

        /// <summary>
        /// Levels call this before they size their backgrounds: on a portrait screen the camera then fills it (no letterbox)
        /// and shows more height at the same width, instead of a 16:9 strip in the middle. Videos and the death scene never call it.
        /// </summary>
        public static void FillPortrait(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent(out AspectRatioController controller) || !controller.enabled)
                return;
            controller.fillPortrait = true;
            controller.landscapeOrthoSize = camera.orthographic ? camera.orthographicSize : 0f;
            controller.UpdateCameraRect();
        }

        private void UpdateCameraRect()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            // Calculate target aspect ratio
            float targetAspect = targetAspectWidth / targetAspectHeight;

            // Calculate current screen aspect ratio
            float screenAspect = (float)Screen.width / Screen.height;

            if (fillPortrait && cam.orthographic && landscapeOrthoSize > 0f)
            {
                if (screenAspect < targetAspect)
                {
                    // Portrait: the playfield keeps its world width, spans the full screen width and sits at the top
                    // (below the notch); the lower part of the screen is left to the touch controls.
                    Rect safe = Screen.safeArea;
                    float top = Mathf.Clamp01(safe.yMax / Screen.height);
                    float height = Mathf.Min(PortraitPlayfieldFraction, top);
                    // Never shorter than the 16:9 strip it replaces.
                    height = Mathf.Max(height, Mathf.Min(top, screenAspect / targetAspect));
                    cam.rect = new Rect(0f, top - height, 1f, height);
                    float viewportAspect = screenAspect / height;
                    cam.orthographicSize = landscapeOrthoSize * targetAspect / viewportAspect;
                }
                else
                {
                    // Landscape and wider: fills the screen, no bars.
                    cam.rect = new Rect(0f, 0f, 1f, 1f);
                    cam.orthographicSize = landscapeOrthoSize;
                }
            }
            else if (fitMode == FitMode.FitInside)
            {
                FitInsideScreen(targetAspect, screenAspect);
            }
            else
            {
                FitOutsideScreen(targetAspect, screenAspect);
            }

            // Wipe the game view every frame. The full-screen camera behind it
            // wipes the bars the viewport no longer covers.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = letterboxColor;
            if (clearCamera != null)
            {
                clearCamera.backgroundColor = letterboxColor;
                clearCamera.depth = cam.depth - 1f;
                clearCamera.enabled = cam.rect.width < 0.999f || cam.rect.height < 0.999f;
            }
        }

        private void FitInsideScreen(float targetAspect, float screenAspect)
        {
            if (screenAspect > targetAspect)
            {
                // Screen is wider than target: add pillarboxing (vertical bars on sides)
                float viewportWidth = targetAspect / screenAspect;
                float viewportX = (1f - viewportWidth) / 2f;

                cam.rect = new Rect(viewportX, 0f, viewportWidth, 1f);

                // Pillarboxing applied
            }
            else if (screenAspect < targetAspect)
            {
                // Screen is taller than target: add letterboxing (horizontal bars on top/bottom)
                float viewportHeight = screenAspect / targetAspect;
                float viewportY = (1f - viewportHeight) / 2f;

                cam.rect = new Rect(0f, viewportY, 1f, viewportHeight);

                // Letterboxing applied
            }
            else
            {
                // Perfect match
                cam.rect = new Rect(0f, 0f, 1f, 1f);

                // Perfect aspect ratio match
            }
        }

        private void FitOutsideScreen(float targetAspect, float screenAspect)
        {
            // This mode crops the edges to fill the screen completely
            // Useful if you have extra space in your game design that can be safely cropped

            if (screenAspect > targetAspect)
            {
                // Screen is wider: show more width, maintain height
                float orthoSizeMultiplier = screenAspect / targetAspect;
                // You would adjust orthographic size here for 2D cameras
                // cam.orthographicSize *= orthoSizeMultiplier;
            }
            else if (screenAspect < targetAspect)
            {
                // Screen is taller: show more height, maintain width
                float orthoSizeMultiplier = targetAspect / screenAspect;
                // cam.orthographicSize *= orthoSizeMultiplier;
            }

            // For FitOutside, always use full screen
            cam.rect = new Rect(0f, 0f, 1f, 1f);

            Debug.LogWarning("AspectRatioController: FitOutside mode - edges may be cropped on different aspect ratios!");
        }

        // Public method to get the safe area in world coordinates
        public Rect GetSafeAreaWorldBounds()
        {
            if (cam == null) return new Rect();

            // Get viewport corners in world space
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.nearClipPlane));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.nearClipPlane));

            return new Rect(bottomLeft.x, bottomLeft.y, topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        }

        private void OnValidate()
        {
            // Update in editor when values change
            if (Application.isPlaying && cam != null)
            {
                UpdateCameraRect();
            }
        }
    }
}
