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
        private bool coverPortrait;
        private bool portraitCoverActive;
        private float portraitWidthShare = 1f;
        private float portraitFocusX;
        private Transform portraitFocus;
        private Vector3 landscapePosition;
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
        /// and shows more height at the same width, instead of a 16:9 strip in the middle. The menu, videos and the death scene use <see cref="CoverPortrait"/>.
        /// </summary>
        public static void FillPortrait(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent(out AspectRatioController controller) || !controller.enabled)
                return;
            controller.fillPortrait = true;
            controller.coverPortrait = false;
            controller.landscapeOrthoSize = camera.orthographic ? camera.orthographicSize : 0f;
            controller.UpdateCameraRect();
        }

        /// <summary>
        /// The main menu, videos and the death scene call this once their camera is placed. On a portrait screen the
        /// camera then fills the screen instead of a 16:9 strip between black bars. <paramref name="widthShare"/> is the
        /// share of the landscape picture's width still in view: 1 keeps all of it (the bands above and below are the
        /// film's own <see cref="PortraitBackdrop"/>), 0 is "cover" (the full picture height, the sides cropped). The
        /// view is centred on <paramref name="focus"/> when given (it follows it), else on <paramref name="focusX"/>,
        /// and never leaves the picture. Landscape is unchanged. Levels use <see cref="FillPortrait"/> instead.
        /// </summary>
        public static void CoverPortrait(Camera camera, float widthShare, float focusX, Transform focus = null)
        {
            if (camera == null || !camera.orthographic || !camera.TryGetComponent(out AspectRatioController controller) || !controller.enabled)
                return;
            if (!controller.coverPortrait)
            {
                // Remember the landscape framing once; a second call must not take the portrait framing as its base.
                controller.landscapeOrthoSize = camera.orthographicSize;
                controller.landscapePosition = camera.transform.position;
            }
            controller.coverPortrait = true;
            controller.fillPortrait = false;
            controller.portraitWidthShare = Mathf.Clamp01(widthShare);
            controller.portraitFocusX = focusX;
            controller.portraitFocus = focus;
            controller.UpdateCameraRect();
        }

        /// <summary>True while a portrait screen shows the full-screen framing <see cref="CoverPortrait"/> set (the playtest reads it).</summary>
        public bool PortraitCoverActive => portraitCoverActive;

        private void ApplyPortraitCover(float targetAspect, float screenAspect)
        {
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            // The share of the picture width in view sets the size; never closer than the full picture height.
            float ortho = landscapeOrthoSize * targetAspect * Mathf.Max(portraitWidthShare, 0.01f) / screenAspect;
            cam.orthographicSize = Mathf.Max(ortho, landscapeOrthoSize);
            portraitCoverActive = true;
            PlacePortraitCamera(snap: true);
        }

        private void PlacePortraitCamera(bool snap)
        {
            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float pictureHalfWidth = landscapeOrthoSize * (targetAspectWidth / targetAspectHeight);
            float viewHalfWidth = cam.orthographicSize * screenAspect;
            float room = Mathf.Max(0f, pictureHalfWidth - viewHalfWidth);
            float focus = portraitFocus != null ? portraitFocus.position.x : portraitFocusX;
            float x = landscapePosition.x + Mathf.Clamp(focus - landscapePosition.x, -room, room);
            var position = cam.transform.position;
            position.x = snap ? x : Mathf.Lerp(position.x, x, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            position.y = landscapePosition.y;
            cam.transform.position = position;
        }

        // Follows the focus (the menu's hero) while a portrait screen crops the picture. No work in landscape.
        private void LateUpdate()
        {
            if (portraitCoverActive && portraitFocus != null)
                PlacePortraitCamera(snap: false);
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
            else if (coverPortrait && cam.orthographic && landscapeOrthoSize > 0f && screenAspect < targetAspect)
            {
                // Portrait menu, video or death scene: full screen, no letterbox (CoverPortrait).
                ApplyPortraitCover(targetAspect, screenAspect);
            }
            else if (fitMode == FitMode.FitInside)
            {
                LeavePortraitCover();
                FitInsideScreen(targetAspect, screenAspect);
            }
            else
            {
                LeavePortraitCover();
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

        // Back to landscape after a portrait cover: the camera's own size and place again.
        private void LeavePortraitCover()
        {
            if (!portraitCoverActive)
                return;
            portraitCoverActive = false;
            cam.orthographicSize = landscapeOrthoSize;
            cam.transform.position = landscapePosition;
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
