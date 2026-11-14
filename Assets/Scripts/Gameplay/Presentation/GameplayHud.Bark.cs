using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Input;
using SickOfIt.Core.UI.Menus;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The hero's bark caption (<see cref="HeroBarks"/>, Docs/Story.md "The hero talks back"): a small one-line bubble above his head for
    /// 1.5 s. It sits inside the safe area and is moved off the hero himself, the rewind button, the rewind hint and the touch stick's zone
    /// (above him first, then beside him, then below). Presentation only: real time, nothing saved.
    /// </summary>
    public sealed partial class GameplayHud
    {
        /// <summary>The smallest a bark caption is drawn, in screen pixels.</summary>
        public const float BarkMinPixels = 17f;

        RectTransform barkRect;
        TextMeshProUGUI barkText;
        float barkUntil = -1f;
        string barkShown = "";
        Rect barkHeroRect;

        /// <summary>The bark caption on screen now ("" when none).</summary>
        public string BarkShown => barkRect != null && barkRect.gameObject.activeSelf ? barkShown : "";

        /// <summary>Shows a caption above the hero until the real time <paramref name="untilUnscaled"/>.</summary>
        public void ShowBark(string text, Color color, float untilUnscaled)
        {
            if (barkRect == null)
                return;
            barkShown = text ?? "";
            barkUntil = untilUnscaled;
            barkText.text = barkShown;
            barkText.color = color;
            barkRect.gameObject.SetActive(!string.IsNullOrEmpty(barkShown));
            PlaceBark();
        }

        public void HideBark()
        {
            barkUntil = -1f;
            if (barkRect != null)
                barkRect.gameObject.SetActive(false);
        }

        /// <summary>The bubble's and the hero's rectangles in screen pixels (the playtest's phone check).</summary>
        public bool TryGetBarkScreenRects(out Rect bubble, out Rect hero)
        {
            bubble = hero = Rect.zero;
            if (barkRect == null || !barkRect.gameObject.activeSelf)
                return false;
            bubble = ScreenRectOf(barkRect);
            hero = barkHeroRect;
            return true;
        }

        /// <summary>The rectangles a bark must stay off, in screen pixels: the hero is added by the caller.</summary>
        public void BarkForbiddenRects(System.Collections.Generic.List<Rect> into)
        {
            var rewind = RewindButtonScreenRect;
            if (rewind.width > 1f)
                into.Add(rewind);
            var hint = RewindHintScreenRect;
            if (hint.width > 1f)
                into.Add(hint);
            if (TouchStickShown)
            {
                var zone = TouchStickZoneScreenRect;
                if (zone.width > 1f)
                    into.Add(zone);
            }
        }

        void BuildBark()
        {
            barkRect = MenuGraphics.NewRect("Bark", menu.Root);
            barkRect.anchorMin = barkRect.anchorMax = new Vector2(0.5f, 0.5f);
            barkRect.pivot = new Vector2(0.5f, 0f);
            var back = barkRect.gameObject.AddComponent<Image>();
            back.color = new Color(0.1f, 0.06f, 0.12f, 0.88f);
            back.raycastTarget = false;
            var slot = MenuGraphics.NewRect("Text", barkRect);
            slot.anchorMin = Vector2.zero;
            slot.anchorMax = Vector2.one;
            slot.offsetMin = new Vector2(10f, 5f);
            slot.offsetMax = new Vector2(-10f, -5f);
            barkText = MenuGraphics.Text(slot, "Label", "", theme.subtitle, menu.Font, 0f, false);
            barkText.alignment = TextAlignmentOptions.Center;
            barkText.raycastTarget = false;
            barkRect.gameObject.SetActive(false);
        }

        void UpdateBark()
        {
            if (barkRect == null || !barkRect.gameObject.activeSelf)
                return;
            if (Time.unscaledTime >= barkUntil)
                HideBark();
            else
                PlaceBark();
        }

        static Rect Inflate(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        void PlaceBark()
        {
            var hero = PlayerController.Active;
            var cam = world != null ? world.Camera : null;
            var canvas = menu.GetComponent<Canvas>();
            var root = menu.Root;
            if (hero == null || cam == null)
                return;
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            barkText.fontSize = Mathf.Max(34f, BarkMinPixels / scale);
            float maxWidth = root.rect.width * 0.72f;
            Vector2 size = barkText.GetPreferredValues(barkShown, maxWidth - 20f, 0f);
            barkRect.sizeDelta = new Vector2(Mathf.Min(maxWidth, size.x + 20f), size.y + 10f);
            float w = barkRect.sizeDelta.x * scale, h = barkRect.sizeDelta.y * scale;

            // The hero's body on screen.
            var sr = hero.GetComponentInChildren<SpriteRenderer>();
            Bounds b = sr != null ? sr.bounds : new Bounds(hero.transform.position + Vector3.up * 0.8f, new Vector3(0.8f, 1.6f, 0f));
            Vector2 lo = cam.WorldToScreenPoint(b.min), hi = cam.WorldToScreenPoint(b.max);
            barkHeroRect = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));

            var area = ScreenRectOf(root);
            var avoid = new System.Collections.Generic.List<Rect>(4);
            avoid.Add(Inflate(barkHeroRect, 4f));
            BarkForbiddenRects(avoid);
            float gap = 8f, pad = 6f;
            float cx = barkHeroRect.center.x;
            var tries = new[]
            {
                new Rect(cx - w * 0.5f, barkHeroRect.yMax + gap, w, h),                                  // above
                new Rect(barkHeroRect.xMax + gap, barkHeroRect.yMax - h, w, h),                          // right of him
                new Rect(barkHeroRect.xMin - gap - w, barkHeroRect.yMax - h, w, h),                      // left of him
                new Rect(cx - w * 0.5f, barkHeroRect.yMax + gap + h + 4f, w, h),                         // a little higher
                new Rect(cx - w * 0.5f, barkHeroRect.yMin - gap - h, w, h),                              // below him
            };
            Rect best = tries[0];
            bool found = false;
            foreach (var t in tries)
            {
                var c = t;
                c.x = Mathf.Clamp(c.x, area.xMin + pad, Mathf.Max(area.xMin + pad, area.xMax - pad - c.width));
                c.y = Mathf.Clamp(c.y, area.yMin + pad, Mathf.Max(area.yMin + pad, area.yMax - pad - c.height));
                bool clear = true;
                foreach (var a in avoid)
                    if (a.Overlaps(c)) { clear = false; break; }
                if (clear) { best = c; found = true; break; }
                if (!found) best = c;
            }

            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 anchorScreen = new Vector2(best.center.x, best.yMin);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, anchorScreen, uiCam, out Vector2 local))
                return;
            barkRect.anchoredPosition = local - root.rect.center;
        }
    }
}
