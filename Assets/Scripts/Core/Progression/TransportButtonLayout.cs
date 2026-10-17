using UnityEngine;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Puts the video/death rewind and skip buttons at the bottom centre, inside the safe area. The beta and build
    /// badge the site draws over the page's top-left corner (and the notch) used to cover them. Presentation only.
    /// </summary>
    public sealed class TransportButtonLayout : MonoBehaviour
    {
        MobileControllerFramework.Button rewind;
        MobileControllerFramework.Button skip;
        Vector2Int screen;

        public static void Apply(GameObject host, MobileControllerFramework.Button rewind, MobileControllerFramework.Button skip)
        {
            if (rewind == null) return;
            var layout = host.AddComponent<TransportButtonLayout>();
            layout.rewind = rewind;
            layout.skip = skip == rewind ? null : skip;
        }

        void LateUpdate()
        {
            if (screen.x == Screen.width && screen.y == Screen.height) return;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0) return;

            var rewindRect = (RectTransform)rewind.transform;
            var skipRect = skip != null ? (RectTransform)skip.transform : null;
            var parent = rewindRect.parent as RectTransform;
            if (parent == null) return;

            // Lift the row clear of the bottom edge by the safe-area inset plus a margin (more in portrait).
            float inset = Screen.safeArea.yMin / screen.y;
            float lift = screen.y > screen.x ? 0.09f : 0.06f;
            float y = Mathf.Clamp01(inset + lift);
            float gap = Mathf.Max(rewindRect.rect.width, 80f) * 0.6f + 8f;

            Place(rewindRect, 0.5f, y, skipRect != null ? -gap : 0f);
            if (skipRect != null)
                Place(skipRect, 0.5f, y, gap);
        }

        static void Place(RectTransform rect, float x, float y, float offsetX)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.anchoredPosition = new Vector2(offsetX, 0f);
        }
    }
}
