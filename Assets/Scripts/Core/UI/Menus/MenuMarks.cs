using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// Small marks on a menu button's slot: the comic "NEW!" sticker (same burst, colours and pulse as the main menu's own stickers)
    /// and a pulsing frame ("glow") that points at one card. Presentation only, on unscaled time.
    /// </summary>
    public static class MenuMarks
    {
        public const string StickerName = "NewSticker";
        public const string GlowName = "Glow";

        /// <summary>A "NEW!" burst on the top-right corner of a button's slot (inside it, so a list's mask never clips it).</summary>
        public static RectTransform AddNewSticker(MenuCanvas menu, MenuButton button, string text = "NEW!")
        {
            SickOfIt.Core.Audio.UiSounds.Unlock();
            var slot = (RectTransform)button.transform.parent;
            var sticker = MenuGraphics.NewRect(StickerName, slot);
            sticker.anchorMin = sticker.anchorMax = new Vector2(1f, 1f);
            sticker.pivot = new Vector2(1f, 1f);
            float side = Mathf.Min(92f, slot.rect.height * 0.9f);
            if (side < 40f)
                side = 70f;
            sticker.sizeDelta = new Vector2(side * 1.15f, side);
            sticker.anchoredPosition = new Vector2(-6f, -2f);
            sticker.localRotation = Quaternion.Euler(0f, 0f, 12f);
            MenuGraphics.Surface(sticker, new MenuSurfaceStyle
            {
                shape = MenuShape.Burst,
                fill = new Color(1f, 0.788f, 0.235f, 1f),
                borderColor = new Color(0.118f, 0.071f, 0.125f, 1f),
                borderWidth = 4f,
                shadowColor = new Color(0f, 0f, 0f, 0.35f),
                shadowOffset = new Vector2(4f, -4f),
                cornerRadius = 20f,
            });
            var label = MenuGraphics.Text(sticker, "Text", text, menu.Theme.caption, menu.Font, 22f);
            label.enableVertexGradient = false;
            label.color = new Color(0.118f, 0.071f, 0.125f, 1f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            sticker.gameObject.AddComponent<MenuPulse>().Scale = 0.07f;
            NoRaycasts(sticker);
            return sticker;
        }

        /// <summary>A frame in the accent colour behind a card that breathes: "this one".</summary>
        public static RectTransform AddGlow(MenuCanvas menu, MenuButton card)
        {
            var slot = (RectTransform)card.transform.parent;
            var glow = MenuGraphics.NewRect(GlowName, slot);
            glow.SetAsFirstSibling();
            glow.anchorMin = Vector2.zero;
            glow.anchorMax = Vector2.one;
            glow.offsetMin = new Vector2(-12f, -12f);
            glow.offsetMax = new Vector2(12f, 12f);
            var image = glow.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(image, MenuShape.Rounded, menu.Theme.card.surface.cornerRadius + 10f);
            image.color = menu.Theme.accent;
            image.raycastTarget = false;
            glow.gameObject.AddComponent<MenuPulse>().GlowImage = image;
            return glow;
        }

        /// <summary>Takes a mark off a slot (a pressed button, a card that is no longer the pick).</summary>
        public static void Remove(Transform slot, string markName)
        {
            var mark = slot != null ? slot.Find(markName) : null;
            if (mark != null)
                Object.Destroy(mark.gameObject);
        }

        static void NoRaycasts(Transform root)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }

    /// <summary>Breathes a mark: scale for a sticker, alpha for a glow frame. Unscaled time, so a paused menu still moves.</summary>
    public sealed class MenuPulse : MonoBehaviour
    {
        public float Scale;
        public Image GlowImage;
        Color baseColor;

        void Awake() { }

        void Update()
        {
            float t = Time.unscaledTime;
            if (GlowImage != null)
            {
                if (baseColor.a <= 0f)
                    baseColor = GlowImage.color;
                var c = baseColor;
                c.a = 0.45f + 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 4.2f));
                GlowImage.color = c;
            }
            if (Scale > 0f)
                transform.localScale = Vector3.one * (1f + Scale * Mathf.Sin(t * 7f));
        }
    }
}
