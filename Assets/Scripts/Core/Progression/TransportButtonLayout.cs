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
        const float MinButtonPixels = 56f;
        MobileControllerFramework.Button rewind;
        MobileControllerFramework.Button skip;
        Vector2Int screen;
        Vector2 rewindSize, skipSize;
        float scale;

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
            // Always at least MinButtonPixels on screen (the intro's skip button was ~40 px on a phone), more on a touch layout.
            float touchPixels = SickOfIt.Core.UI.Menus.MenuScreen.TouchLayout(style)
                ? SickOfIt.Core.UI.Menus.MenuScreen.MinTouchPixels(style) : 0f;
            float touch = Mathf.Max(touchPixels, MinButtonPixels) / pixelsPerUnit;
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
        }

        static void Size(RectTransform rect, Vector2 size, float touch)
        {
            if (size.x <= 0f || size.y <= 0f)
                return;
            float grow = Mathf.Max(1f, touch / Mathf.Min(size.x, size.y)); // keep the button's shape
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * grow);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * grow);
        }

        static void Place(RectTransform rect, float x, float y, float offsetX)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.anchoredPosition = new Vector2(offsetX, 0f);
        }
    }
}
