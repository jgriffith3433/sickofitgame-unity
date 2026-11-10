using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The style meter (Docs/Story.md, "Style"): the tier's name and the streak over a thin bar, low on the screen above
    /// the progress bar, shown only while the meter is up. Presentation only: it reads <see cref="StyleSystem"/>.
    /// </summary>
    public sealed partial class GameplayHud
    {
        TextMeshProUGUI styleLabel;
        RectTransform styleSlot, styleFill;
        Image styleFillImage;
        float stylePunchAt = -99f;
        string shownStyle = "";

        /// <summary>The meter's text as the player sees it (the playtest reads it), and whether it is on screen.</summary>
        public string StyleText => styleSlot != null && styleSlot.gameObject.activeSelf ? styleLabel.text : "";
        public float StyleBarFraction => styleFill != null && styleSlot.gameObject.activeSelf ? styleFill.anchorMax.x : 0f;
        /// <summary>How many grazes made the meter pop (the playtest reads it).</summary>
        public int StylePunches { get; private set; }

        void BuildStyle()
        {
            var land = MenuPlacement.At(0.5f, 0f, 0f, 96f, 700f, 64f);
            var port = MenuPlacement.At(0.5f, 0f, 0f, 380f, 900f, 64f);
            styleLabel = menu.AddText(menu.Root, "Style", "", theme.caption, land.anchor, land.offset, land.size, out _, 40f, null, theme.entrance);
            styleLabel.enableVertexGradient = false;
            if (styleLabel.isActiveAndEnabled)
                HudFloaters.ApplyTextOutlineAndShadow(styleLabel);
            styleSlot = (RectTransform)styleLabel.transform.parent;
            MenuResponsive.Track(styleSlot, land, port, MenuElementKind.Text, menu.Responsive);

            var track = MenuGraphics.NewImage(styleSlot, "StyleTrack", new Color(0.12f, 0.07f, 0.13f, 0.8f));
            track.raycastTarget = false;
            var tr = track.rectTransform;
            tr.anchorMin = new Vector2(0.1f, 0f);
            tr.anchorMax = new Vector2(0.9f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.sizeDelta = new Vector2(0f, 12f);
            tr.anchoredPosition = Vector2.zero;
            styleFillImage = MenuGraphics.NewImage(tr, "StyleFill", Color.white);
            styleFillImage.raycastTarget = false;
            styleFill = styleFillImage.rectTransform;
            styleFill.anchorMin = Vector2.zero;
            styleFill.anchorMax = new Vector2(0f, 1f);
            styleFill.offsetMin = styleFill.offsetMax = Vector2.zero;
            styleSlot.gameObject.SetActive(false);
        }

        /// <summary>A graze: the meter pops.</summary>
        public void StylePunch()
        {
            stylePunchAt = Time.unscaledTime;
            StylePunches++;
        }

        /// <summary>A payout rises off the meter ("HOT!  +1♦  +1s").</summary>
        public void StyleFloat(string text, Color color)
        {
            if (styleSlot != null && !styleSlot.gameObject.activeSelf)
                styleSlot.gameObject.SetActive(true);
            floaters?.Spawn(styleSlot != null ? styleSlot : progressRoot, text, color, 52f);
        }

        void UpdateStyle()
        {
            var style = world.Style;
            if (styleLabel == null || style == null)
                return;
            bool show = style.Active && style.Meter > 0.5f && !ResultsOpen && !(world.PlayerController != null && world.PlayerController.Suspended);
            if (styleSlot.gameObject.activeSelf != show)
                styleSlot.gameObject.SetActive(show);
            if (!show)
                return;
            if (styleLabel.isActiveAndEnabled && styleLabel.outlineWidth < 0.3f)
                HudFloaters.ApplyTextOutlineAndShadow(styleLabel);
            var color = HudFloaters.EnsureReadableColor(style.Tier > 0 ? style.TierColor : new Color(1f, 0.95f, 0.82f));
            string text = style.Tier > 0 ? style.TierName : "STYLE";
            if (style.Streak > 1)
                text += $"  x{style.Streak}";
            if (text != shownStyle)
            {
                shownStyle = text;
                styleLabel.text = text;
            }
            styleLabel.color = color;
            styleFillImage.color = color;
            styleFill.anchorMax = new Vector2(style.Fraction, 1f);
            float t = (Time.unscaledTime - stylePunchAt) / 0.25f;
            float punch = t < 1f ? 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) : 1f;
            styleLabel.rectTransform.localScale = new Vector3(punch, punch, 1f);
        }
    }
}
