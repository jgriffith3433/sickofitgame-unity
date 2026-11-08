using UnityEngine;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Puts the video/death rewind and skip buttons at the bottom centre, inside the safe area. The beta and build
    /// badge the site draws over the page's top-left corner (and the notch) used to cover them. On a touch layout
    /// (a phone held upright) they are at least the theme's touch size (ui/theme.json responsive.minTouchSize).
    /// Presentation only.
    /// </summary>
    public sealed class TransportButtonLayout : MonoBehaviour
    {
        /// <summary>The smallest transport button, in CSS pixels (the page's pixels, not the canvas's): 48 is the touch floor, 52 leaves a margin.</summary>
        public const float MinButtonCssPixels = 56f;
        MobileControllerFramework.Button rewind;
        MobileControllerFramework.Button skip;
        Vector2Int screen;
        Vector2 rewindSize, skipSize;
        float scale;

        /// <summary>
        /// Device pixels per CSS pixel. On a WebGL page Unity's Screen.dpi is 96 x devicePixelRatio; elsewhere (or when it
        /// reports nothing) the canvas is taken as 1:1. A phone at 2x or 3x drew a "56 px" button only 28 or 19 CSS px tall.
        /// </summary>
        public static float DevicePixelRatio()
        {
            float dpi = Screen.dpi;
            if (Application.platform != RuntimePlatform.WebGLPlayer || dpi <= 0f) return 1f;
            return Mathf.Clamp(dpi / 96f, 1f, 4f);
        }

        /// <summary>A height in screen (backbuffer) pixels as CSS pixels.</summary>
        public static float ToCssPixels(float screenPixels) => screenPixels / DevicePixelRatio();

        public static void Apply(GameObject host, MobileControllerFramework.Button rewind, MobileControllerFramework.Button skip)
        {
            if (rewind == null) return;
            var layout = host.AddComponent<TransportButtonLayout>();
            layout.rewind = rewind;
            layout.skip = skip == rewind ? null : skip;
            layout.rewindSize = ((RectTransform)rewind.transform).rect.size;
            if (layout.skip != null)
                layout.skipSize = ((RectTransform)layout.skip.transform).rect.size;
        }

        void LateUpdate()
        {
            // The canvas scaler settles after the first frame (or a resize), so its scale is part of "changed" too:
            // sizing against a stale scale left the button at ~40 px on a phone.
            var scaler = rewind.GetComponentInParent<Canvas>();
            float currentScale = scaler != null ? scaler.rootCanvas.scaleFactor : 1f;
            if (screen.x == Screen.width && screen.y == Screen.height && Mathf.Approximately(scale, currentScale)) return;
            screen = new Vector2Int(Screen.width, Screen.height);
            scale = currentScale;
            if (screen.x <= 0 || screen.y <= 0) return;

            var rewindRect = (RectTransform)rewind.transform;
            var skipRect = skip != null ? (RectTransform)skip.transform : null;
            var parent = rewindRect.parent as RectTransform;
            if (parent == null) return;

            var gd = SickOfIt.Data.GameDefinition.Instance;
            var style = gd != null && gd.menuTheme != null ? gd.menuTheme.responsive : null;
            var canvas = rewindRect.GetComponentInParent<Canvas>();
            float pixelsPerUnit = canvas != null && canvas.rootCanvas.scaleFactor > 0f ? canvas.rootCanvas.scaleFactor : 1f;
            // Always at least MinButtonCssPixels on the page (the intro's skip button was ~40 px on a phone), more on a touch layout.
            float touchPixels = SickOfIt.Core.UI.Menus.MenuScreen.TouchLayout(style)
                ? SickOfIt.Core.UI.Menus.MenuScreen.MinTouchPixels(style) : 0f;
            float touch = Mathf.Max(touchPixels, MinButtonCssPixels * DevicePixelRatio()) / pixelsPerUnit;
            Size(rewindRect, rewindSize, touch);
            if (skipRect != null)
                Size(skipRect, skipSize, touch);

            // Lift the row clear of the bottom edge by the safe-area inset plus a margin (more in portrait).
            float inset = Screen.safeArea.yMin / screen.y;
            float lift = screen.y > screen.x ? 0.09f : 0.06f;
            float y = Mathf.Clamp01(inset + lift);
            float gap = Mathf.Max(rewindRect.rect.width, 80f) * 0.6f + 8f;

            // The parent is not always the whole screen, so anchor 0.5 of it can sit off the screen's centre: shift
            // the row by however far the canvas centre is from the parent's.
            float shift = 0f;
            if (canvas != null && canvas.rootCanvas.transform is RectTransform root)
                shift = parent.InverseTransformPoint(root.TransformPoint(root.rect.center)).x - parent.rect.center.x;

            Place(rewindRect, 0.5f, y, shift + (skipRect != null ? -gap : 0f));
            if (skipRect != null)
                Place(skipRect, 0.5f, y, shift + gap);

            // Measure what actually reached the screen (the canvas scaler, its camera and the page's pixel ratio all
            // sit between a rect's size and its pixels) and grow any button still under the floor. The phone run
            // measured 44.8 CSS px for a button sized to 52.
            float wanted = MinButtonCssPixels * DevicePixelRatio();
            Correct(rewindRect, canvas, wanted);
            if (skipRect != null)
                Correct(skipRect, canvas, wanted);
        }

        static float ScreenHeight(RectTransform rect, Canvas canvas)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return Mathf.Abs(RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y - RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y);
        }

        static void Correct(RectTransform rect, Canvas canvas, float wantedScreenPixels)
        {
            for (int i = 0; i < 2; i++)
            {
                float h = ScreenHeight(rect, canvas);
                if (h <= 1f || h >= wantedScreenPixels) return;
                float grow = wantedScreenPixels / h * 1.02f;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.rect.width * grow);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.rect.height * grow);
            }
        }

        static void Size(RectTransform rect, Vector2 size, float touch)
        {
            // A rect not laid out yet reads 0x0: size it square rather than leave the tiny prefab icon.
            if (size.x <= 0f || size.y <= 0f)
                size = new Vector2(touch, touch);
            float grow = Mathf.Max(1f, touch / Mathf.Min(size.x, size.y)); // keep the button's shape
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * grow);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * grow);
            // The glyph is a child image that kept its small prefab size inside the grown button: make it fill it.
            foreach (var image in rect.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.gameObject.name != "_iconImg") continue;
                var icon = image.rectTransform;
                icon.anchorMin = Vector2.zero;
                icon.anchorMax = Vector2.one;
                icon.offsetMin = Vector2.zero;
                icon.offsetMax = Vector2.zero;
            }
        }

        static void Place(RectTransform rect, float x, float y, float offsetX)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.anchoredPosition = new Vector2(offsetX, 0f);
        }
    }
}
