using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>What a card shows. Everything is optional.</summary>
    public sealed class MenuCardData
    {
        public Sprite Picture;
        /// <summary>Keep the picture area even without a picture (so a grid of cards lines up).</summary>
        public bool PictureArea;
        public string Badge;
        /// <summary>An icon drawn in a round frame in the card's top corner (a Grudge's or a good's art).</summary>
        public Sprite Icon;
        public string Caption;
        public string Title;
        public string Description;
        public string Status;
        public Color? StatusColor;
        /// <summary>Greyed out, not clickable, with this text stamped across it.</summary>
        public string LockedStamp;
    }

    /// <summary>A built card, for updating it later (the shop after a purchase).</summary>
    public sealed class MenuCardView
    {
        public MenuButton Button;
        public TextMeshProUGUI Caption;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Description;
        public TextMeshProUGUI Status;

        public void SetStatus(string text, Color color)
        {
            if (Status == null)
                return;
            MenuGraphics.SetText(Status, text);
            Status.enableVertexGradient = false;
            Status.color = color;
        }

        public void SetCaption(string text) => MenuGraphics.SetText(Caption, text);
    }

    /// <summary>
    /// A clickable card for the level select, video select and shop: optional picture on top, a number badge,
    /// a small caption, a title, a description, and a status line ("PLAY", "CLEARED ♥♥♡", "BUY ♦ 200").
    /// </summary>
    public static class MenuCard
    {
        public static MenuCardView Add(MenuCanvas menu, RectTransform list, MenuCardData data, Action onClick, MenuButtonStyle styleOverride = null)
        {
            var theme = menu.Theme;
            var cardStyle = styleOverride ?? theme.card;
            bool locked = !string.IsNullOrEmpty(data.LockedStamp);
            var card = menu.AddButton(list, null, cardStyle, onClick);
            var content = card.Content;
            bool picture = data.Picture != null || data.PictureArea;

            if (picture)
            {
                var frame = MenuGraphics.NewRect("Picture", content);
                Anchor(frame, 0f, 0.4f, 1f, 1f);
                var maskImage = frame.gameObject.AddComponent<Image>();
                MenuGraphics.Apply(maskImage, MenuShape.Rounded, Mathf.Max(8f, theme.card.surface.cornerRadius - 8f));
                maskImage.raycastTarget = false;
                frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                if (data.Picture != null)
                {
                    var image = MenuGraphics.NewImage(frame, "Image", locked ? theme.lockedTint : Color.white);
                    image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    MenuGraphics.Cover(image, data.Picture);
                }
                else
                {
                    MenuGraphics.NewImage(frame, "Empty", new Color(0f, 0f, 0f, 0.35f));
                    var mark = MenuGraphics.Text(frame, "Mark", "?", theme.title, menu.TitleFont, theme.title.fontSize * 0.6f);
                    mark.enableVertexGradient = false;
                    mark.color = new Color(1f, 1f, 1f, 0.25f);
                }
            }

            float captionTop = picture ? 0.4f : 1f;
            float titleTop = picture ? 0.3f : 0.8f;
            float titleBottom = picture ? 0.13f : 0.58f;
            var view = new MenuCardView { Button = card };
            view.Caption = AddText(menu, content, "Caption", data.Caption, theme.caption, theme.muted, 0f, titleTop - 0.01f, 1f, captionTop);
            view.Title = AddText(menu, content, "Title", data.Title, theme.heading, null, 0f, titleBottom, 1f, titleTop);
            if (!picture)
                view.Description = AddText(menu, content, "Description", data.Description, theme.body, null, 0f, 0.2f, 1f, 0.58f);
            view.Status = AddText(menu, content, "Status", data.Status, theme.caption, data.StatusColor ?? theme.accent, 0f, 0f, 1f, picture ? 0.13f : 0.2f);

            if (!string.IsNullOrEmpty(data.Badge))
            {
                var badge = MenuGraphics.NewRect("Badge", card.transform);
                badge.anchorMin = badge.anchorMax = new Vector2(0f, 1f);
                badge.anchoredPosition = new Vector2(18f, -18f);
                badge.sizeDelta = new Vector2(88f, 88f);
                MenuGraphics.Surface(badge, new MenuSurfaceStyle
                {
                    shape = MenuShape.Circle,
                    fill = theme.accent,
                    borderColor = theme.card.surface.borderColor,
                    borderWidth = Mathf.Min(theme.card.surface.borderWidth, 6f),
                    shadowColor = new Color(0f, 0f, 0f, 0.4f),
                    shadowOffset = new Vector2(3f, -4f),
                });
                var number = MenuGraphics.Text(badge, "Number", data.Badge, theme.heading, menu.Font);
                number.enableVertexGradient = false;
                number.color = theme.card.surface.borderColor.a > 0f ? theme.card.surface.borderColor : Color.black;
                number.outlineWidth = 0f;
            }

            if (data.Icon != null)
            {
                var frame = MenuGraphics.NewRect("Icon", card.transform);
                frame.anchorMin = frame.anchorMax = new Vector2(1f, 1f);
                frame.anchoredPosition = new Vector2(-62f, -62f);
                frame.sizeDelta = new Vector2(104f, 104f);
                var icon = MenuGraphics.NewImage(frame, "Art", locked ? theme.lockedTint : Color.white);
                icon.sprite = data.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.rectTransform.anchorMin = Vector2.zero;
                icon.rectTransform.anchorMax = Vector2.one;
                icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            }

            if (locked)
            {
                card.interactable = false;
                var stamp = MenuGraphics.NewRect("Locked", card.transform);
                stamp.anchorMin = stamp.anchorMax = new Vector2(0.5f, picture ? 0.68f : 0.5f);
                stamp.sizeDelta = new Vector2(theme.card.size.x * 0.7f, 76f);
                stamp.localRotation = Quaternion.Euler(0f, 0f, 8f);
                MenuGraphics.Surface(stamp, new MenuSurfaceStyle
                {
                    shape = MenuShape.Rounded,
                    cornerRadius = 14f,
                    fill = new Color(0.1f, 0.1f, 0.12f, 0.92f),
                    borderColor = theme.muted,
                    borderWidth = 4f,
                    shadowColor = Color.clear,
                });
                var text = MenuGraphics.Text(stamp, "Text", data.LockedStamp, theme.caption, menu.Font);
                text.enableVertexGradient = false;
                text.color = Color.white;
            }
            return view;
        }

        static TextMeshProUGUI AddText(MenuCanvas menu, RectTransform parent, string name, string value, MenuTextStyle style, Color? color,
            float xMin, float yMin, float xMax, float yMax)
        {
            if (value == null)
                return null;
            var rect = MenuGraphics.NewRect(name, parent);
            Anchor(rect, xMin, yMin, xMax, yMax);
            var text = MenuGraphics.Text(rect, "Text", value, style, menu.Font);
            if (color.HasValue)
            {
                text.enableVertexGradient = false;
                text.color = color.Value;
            }
            return text;
        }

        static void Anchor(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
