using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// An arcade-style bar (player health, boss health). With few points it draws one chunky segment per point;
    /// otherwise one smooth bar. Lost value flashes and then drains away behind the fill, gained value pops in,
    /// and below the low threshold the fill switches color and throbs. Purely cosmetic: it shows whatever value
    /// it's given each frame, so rewinding just looks like healing.
    /// </summary>
    public sealed class MenuBar : MonoBehaviour
    {
        const float GhostDelay = 0.35f;
        const float GhostDrainPerSecond = 1.6f;
        const float FlashTime = 0.18f;

        public Color Fill = Color.red;
        public Color LowFill = Color.red;
        public Color Lost = Color.white;
        public Color Track = new Color(0f, 0f, 0f, 0.6f);
        public float LowFraction = 0.34f;
        public int MaxSegments = 12;
        public float Gap = 6f;

        /// <summary>Seconds since the value last dropped (for shake effects); large when it hasn't.</summary>
        public float SinceHit => Time.unscaledTime - hitAt;
        public bool IsLow { get; private set; }
        /// <summary>True while drawn as one segment per point (up to MaxSegments); a bigger maximum is one smooth bar.</summary>
        public bool Segmented => segmentsRoot != null;

        RectTransform segmentsRoot;
        readonly List<Segment> segments = new List<Segment>();
        Image smoothFill, smoothGhost;
        int current = -1, max = -1;
        float shownFraction = 1f, ghostFraction = 1f;
        float hitAt = -99f;

        sealed class Segment
        {
            public RectTransform Rect;
            public Image Fill;
            public float LostAt = -99f;
            public float GainedAt = -99f;
            public bool Full;
        }

        public static MenuBar Create(Transform parent, string name)
        {
            var rect = MenuGraphics.NewRect(name, parent);
            return rect.gameObject.AddComponent<MenuBar>();
        }

        /// <summary>Shows 'value' out of 'maximum'.</summary>
        public void Set(int value, int maximum)
        {
            maximum = Mathf.Max(0, maximum);
            value = Mathf.Clamp(value, 0, maximum);
            if (maximum != max)
                Rebuild(maximum);
            if (current >= 0 && value < current)
                hitAt = Time.unscaledTime;
            if (segmentsRoot != null)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];
                    bool full = i < value;
                    if (current >= 0 && segment.Full && !full)
                        segment.LostAt = Time.unscaledTime;
                    if (current >= 0 && !segment.Full && full)
                        segment.GainedAt = Time.unscaledTime;
                    segment.Full = full;
                }
            }
            current = value;
            shownFraction = maximum > 0 ? (float)value / maximum : 0f;
            if (shownFraction > ghostFraction)
                ghostFraction = shownFraction;
            IsLow = maximum > 0 && shownFraction <= LowFraction;
        }

        void Rebuild(int maximum)
        {
            max = maximum;
            segments.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            segmentsRoot = null;
            smoothFill = smoothGhost = null;

            if (maximum > 0 && maximum <= MaxSegments)
            {
                segmentsRoot = MenuGraphics.NewRect("Segments", transform);
                MenuGraphics.Fill(segmentsRoot);
                var row = segmentsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = Gap;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = true;
                for (int i = 0; i < maximum; i++)
                {
                    var slot = MenuGraphics.NewRect("Segment", segmentsRoot);
                    var back = MenuGraphics.NewImage(slot, "Track", Track);
                    MenuGraphics.Apply(back, MenuShape.Rounded, 8f);
                    var fill = MenuGraphics.NewImage(slot, "Fill", Fill);
                    MenuGraphics.Apply(fill, MenuShape.Rounded, 8f);
                    segments.Add(new Segment { Rect = (RectTransform)fill.transform, Fill = fill, Full = true });
                }
            }
            else
            {
                var back = MenuGraphics.NewImage(transform, "Track", Track);
                MenuGraphics.Apply(back, MenuShape.Rounded, 10f);
                smoothGhost = MenuGraphics.NewImage(transform, "Lost", Lost);
                MenuGraphics.Apply(smoothGhost, MenuShape.Rounded, 10f);
                smoothFill = MenuGraphics.NewImage(transform, "Fill", Fill);
                MenuGraphics.Apply(smoothFill, MenuShape.Rounded, 10f);
            }
            current = -1;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            float throb = IsLow ? 0.5f + 0.5f * Mathf.Sin(now * 9f) : 0f;
            Color fill = IsLow ? Color.Lerp(LowFill, Color.white, throb * 0.35f) : Fill;

            if (segmentsRoot != null)
            {
                foreach (var segment in segments)
                {
                    float lostAge = now - segment.LostAt;
                    float gainedAge = now - segment.GainedAt;
                    if (segment.Full)
                    {
                        segment.Fill.color = gainedAge < FlashTime ? Color.white : fill;
                        float pop = gainedAge < 0.3f ? 1f + 0.35f * Mathf.Sin(gainedAge / 0.3f * Mathf.PI) : 1f;
                        segment.Rect.localScale = new Vector3(pop, pop, 1f);
                    }
                    else if (lostAge < GhostDelay + 0.25f)
                    {
                        // Just lost: flash, then shrink away.
                        segment.Fill.color = lostAge < FlashTime ? Color.white : Lost;
                        float shrink = Mathf.Clamp01(1f - (lostAge - GhostDelay) / 0.25f);
                        segment.Rect.localScale = new Vector3(1f, shrink, 1f);
                    }
                    else
                    {
                        segment.Rect.localScale = new Vector3(1f, 0f, 1f);
                    }
                }
                return;
            }

            if (smoothFill == null)
                return;
            if (now - hitAt > GhostDelay)
                ghostFraction = Mathf.MoveTowards(ghostFraction, shownFraction, GhostDrainPerSecond * Time.unscaledDeltaTime);
            SetWidth(smoothFill, shownFraction);
            SetWidth(smoothGhost, ghostFraction);
            smoothFill.color = now - hitAt < FlashTime ? Color.white : fill;
        }

        static void SetWidth(Image image, float fraction)
        {
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.enabled = fraction > 0.001f;
        }
    }
}
