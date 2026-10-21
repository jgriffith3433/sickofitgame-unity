using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// Shapes, fonts and text styling for runtime menus. Shapes are drawn into small textures once and cached,
    /// so menus look finished without any UI art.
    /// </summary>
    public static class MenuGraphics
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<Font, TMP_FontAsset> fonts = new Dictionary<Font, TMP_FontAsset>();

        // ─── Shapes ───────────────────────────────────────────────────

        /// <summary>The sprite for a shape. Rounded shapes are 9-sliced so any size keeps crisp corners.</summary>
        public static Sprite Shape(MenuShape shape, float cornerRadius, Sprite custom = null)
        {
            switch (shape)
            {
                case MenuShape.Sprite: return custom;
                case MenuShape.Rectangle: return RoundedRect(0);
                case MenuShape.Pill: return RoundedRect(64);
                case MenuShape.Circle: return Circle();
                case MenuShape.Burst: return Burst(14);
                case MenuShape.None: return null;
                default: return RoundedRect(Mathf.Clamp(Mathf.RoundToInt(cornerRadius), 0, 64));
            }
        }

        /// <summary>Applies a shape to an image: sliced for rounded shapes, simple otherwise.</summary>
        public static void Apply(Image image, MenuShape shape, float cornerRadius, Sprite custom = null)
        {
            var sprite = Shape(shape, cornerRadius, custom);
            image.sprite = sprite;
            image.enabled = sprite != null;
            bool sliced = sprite != null && sprite.border.sqrMagnitude > 0f;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            // Corner textures are drawn one texel per reference pixel; a pill's corners follow its height.
            if (shape == MenuShape.Pill && image.GetComponent<MenuPill>() == null)
                image.gameObject.AddComponent<MenuPill>();
        }

        static Sprite RoundedRect(int radius)
        {
            string key = "rect" + radius;
            if (sprites.TryGetValue(key, out var cached))
                return cached;
            int r = Mathf.Max(radius, 1);
            int size = r * 2 + 4;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = radius == 0 ? 0f : Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            float border = r + 1;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = key;
            return sprites[key] = sprite;
        }

        static Sprite Circle()
        {
            if (sprites.TryGetValue("circle", out var cached))
                return cached;
            const int size = 128;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) - (c - 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return sprites["circle"] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite Burst(int points)
        {
            string key = "burst" + points;
            if (sprites.TryGetValue(key, out var cached))
                return cached;
            const int size = 192;
            const int samples = 3;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float c = size * 0.5f;
            float outer = c - 2f;
            float inner = outer * 0.74f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = x + (sx + 0.5f) / samples - c;
                            float py = y + (sy + 0.5f) / samples - c;
                            float angle = Mathf.Atan2(py, px);
                            // Distance to the star's edge along this angle: alternate outer and inner points.
                            float t = Mathf.Repeat(angle / (Mathf.PI * 2f) * points, 1f);
                            float edge = Mathf.Lerp(outer, inner, 1f - Mathf.Abs(t * 2f - 1f));
                            if (Mathf.Sqrt(px * px + py * py) <= edge)
                                inside++;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return sprites[key] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A soft dark edge, transparent in the middle.</summary>
        public static Sprite Vignette()
        {
            if (sprites.TryGetValue("vignette", out var cached))
                return cached;
            const int size = 128;
            var tex = NewTexture(size, size);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return sprites["vignette"] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A tileable pattern texture (white on transparent; tint it with the image color).</summary>
        public static Texture2D Pattern(MenuPattern pattern)
        {
            string key = "pattern" + pattern;
            if (sprites.TryGetValue(key, out var cached))
                return cached.texture;
            const int size = 64;
            var tex = NewTexture(size, size);
            tex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a;
                    switch (pattern)
                    {
                        case MenuPattern.Stripes:
                            a = Mathf.Repeat(x + y, size) < size / 2 ? 1f : 0f;
                            break;
                        case MenuPattern.Dots:
                            a = Mathf.Clamp01(size * 0.18f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)));
                            break;
                        case MenuPattern.Checker:
                            a = (x < size / 2) ^ (y < size / 2) ? 1f : 0f;
                            break;
                        default:
                            a = 0f;
                            break;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            sprites[key] = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.zero, 100f);
            return tex;
        }

        static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }

        // ─── Text ─────────────────────────────────────────────────────

        public static TMP_FontAsset FontAsset(Font font)
        {
            if (font == null)
                return TMP_Settings.defaultFontAsset;
            if (fonts.TryGetValue(font, out var asset) && asset != null)
                return asset;
            asset = TMP_FontAsset.CreateFontAsset(font);
            if (asset == null)
            {
                Debug.LogWarning($"[Menu] Couldn't make a TextMesh Pro font from '{font.name}'; using the default.");
                asset = TMP_Settings.defaultFontAsset;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                // Characters the font lacks (♥ ♦ ...) fall back to the default font.
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            }
            return fonts[font] = asset;
        }

        /// <summary>Styles a text: font, size, color/gradient, outline, spacing, case, alignment.</summary>
        public static void Style(TextMeshProUGUI text, MenuTextStyle style, TMP_FontAsset font, float sizeOverride = 0f, bool autoSize = true)
        {
            if (font != null)
                text.font = font;
            text.fontSize = sizeOverride > 0f ? sizeOverride : style.fontSize;
            text.enableAutoSizing = autoSize;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(text.fontSize, Mathf.Max(12f, text.fontSize * 0.45f));
            text.color = Color.white;
            bool gradient = style.gradientTop.a > 0f || style.gradientBottom.a > 0f;
            text.enableVertexGradient = gradient;
            if (gradient)
                text.colorGradient = new VertexGradient(style.gradientTop, style.gradientTop, style.gradientBottom, style.gradientBottom);
            else
                text.color = style.color;
            text.fontStyle = (style.bold ? FontStyles.Bold : 0) | (style.italic ? FontStyles.Italic : 0) | (style.uppercase ? FontStyles.UpperCase : 0);
            text.characterSpacing = style.characterSpacing;
            text.lineSpacing = style.lineSpacing;
            text.alignment = style.align == MenuTextAlign.Left ? TextAlignmentOptions.MidlineLeft
                : style.align == MenuTextAlign.Right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            if (style.outlineWidth > 0f && style.outlineColor.a > 0f)
            {
                text.outlineWidth = style.outlineWidth;
                text.outlineColor = style.outlineColor;
            }
        }

        /// <summary>Creates a styled text filling its parent, with an optional drop shadow copy behind it.</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string value, MenuTextStyle style, TMP_FontAsset font, float sizeOverride = 0f, bool autoSize = true)
        {
            MenuTextShadow shadowLink = null;
            if (style.shadowColor.a > 0f)
            {
                var shadow = NewText(parent, name + "Shadow");
                Style(shadow, style, font, sizeOverride, autoSize);
                shadow.enableVertexGradient = false;
                shadow.color = style.shadowColor;
                shadow.outlineColor = style.shadowColor;
                shadow.text = value;
                var rect = shadow.rectTransform;
                rect.offsetMin += style.shadowOffset;
                rect.offsetMax += style.shadowOffset;
                shadowLink = shadow.gameObject.AddComponent<MenuTextShadow>();
                shadowLink.Shadow = shadow;
                shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            var text = NewText(parent, name);
            Style(text, style, font, sizeOverride, autoSize);
            text.text = value;
            if (shadowLink != null)
            {
                shadowLink.Source = text;
                shadowLink.Sync();
            }
            return text;
        }

        static TextMeshProUGUI NewText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go.AddComponent<TextMeshProUGUI>();
        }

        /// <summary>Sets a text and its shadow copy (if any) together.</summary>
        public static void SetText(TextMeshProUGUI text, string value)
        {
            if (text == null)
                return;
            text.text = value;
            var shadow = text.transform.parent != null ? text.transform.parent.Find(text.name + "Shadow") : null;
            if (shadow == null)
                return;
            if (shadow.TryGetComponent<MenuTextShadow>(out var link))
            {
                link.Source = text;
                link.Sync();
            }
            else if (shadow.TryGetComponent<TextMeshProUGUI>(out var copy))
            {
                copy.text = value;
            }
        }

        // ─── Layout helpers ───────────────────────────────────────────

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Fill(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        public static Image NewImage(Transform parent, string name, Color color)
        {
            var rect = NewRect(name, parent);
            Fill(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        /// <summary>Shadow + border + fill for a surface style, filling the parent. Returns the fill image.</summary>
        public static Image Surface(Transform parent, MenuSurfaceStyle style, Color? fillOverride = null)
        {
            if (style.shape == MenuShape.None)
                return null;
            if (style.shadowColor.a > 0f)
            {
                var shadow = NewImage(parent, "Shadow", style.shadowColor);
                Apply(shadow, style.shape, style.cornerRadius, style.sprite);
                shadow.rectTransform.offsetMin = style.shadowOffset;
                shadow.rectTransform.offsetMax = style.shadowOffset;
            }
            if (style.borderWidth > 0f && style.borderColor.a > 0f)
            {
                var border = NewImage(parent, "Border", style.borderColor);
                Apply(border, style.shape, style.cornerRadius, style.sprite);
            }
            var fill = NewImage(parent, "Fill", fillOverride ?? style.fill);
            Apply(fill, style.shape, Mathf.Max(0f, style.cornerRadius - style.borderWidth), style.sprite);
            if (style.borderWidth > 0f && style.borderColor.a > 0f)
                Fill(fill.rectTransform, style.borderWidth);
            return fill;
        }

        /// <summary>Shows a picture cropped to fill its rect, like CSS background-size: cover.</summary>
        public static void Cover(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            if (!image.TryGetComponent<AspectRatioFitter>(out var fitter))
                fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
        }
    }

    /// <summary>Marks a text as another text's drop shadow and keeps its words and layout settings in sync.</summary>
    public sealed class MenuTextShadow : MonoBehaviour
    {
        public TextMeshProUGUI Source;
        public TextMeshProUGUI Shadow;

        void OnEnable() => Sync();
        void LateUpdate() => Sync();

        public void Sync()
        {
            if (Source == null)
                return;
            if (Shadow == null && !TryGetComponent(out Shadow))
                return;
            if (Shadow.text != Source.text)
                Shadow.text = Source.text;
            if (Shadow.font != Source.font)
                Shadow.font = Source.font;
            if (Shadow.fontStyle != Source.fontStyle)
                Shadow.fontStyle = Source.fontStyle;
            if (Shadow.enableAutoSizing != Source.enableAutoSizing)
                Shadow.enableAutoSizing = Source.enableAutoSizing;
            if (Shadow.fontSizeMin != Source.fontSizeMin)
                Shadow.fontSizeMin = Source.fontSizeMin;
            if (Shadow.fontSizeMax != Source.fontSizeMax)
                Shadow.fontSizeMax = Source.fontSizeMax;
            if (!Source.enableAutoSizing && Shadow.fontSize != Source.fontSize)
                Shadow.fontSize = Source.fontSize;
            if (Shadow.characterSpacing != Source.characterSpacing)
                Shadow.characterSpacing = Source.characterSpacing;
            if (Shadow.lineSpacing != Source.lineSpacing)
                Shadow.lineSpacing = Source.lineSpacing;
            if (Shadow.alignment != Source.alignment)
                Shadow.alignment = Source.alignment;
            if (Shadow.textWrappingMode != Source.textWrappingMode)
                Shadow.textWrappingMode = Source.textWrappingMode;
            if (Shadow.overflowMode != Source.overflowMode)
                Shadow.overflowMode = Source.overflowMode;
            if (Shadow.margin != Source.margin)
                Shadow.margin = Source.margin;
        }
    }

    /// <summary>Gives a text an outline once it's enabled (TextMesh Pro can't outline a text that was never enabled).</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class MenuThemedOutline : MonoBehaviour
    {
        float width;
        Color color;

        public void Set(float outlineWidth, Color outlineColor)
        {
            width = outlineWidth;
            color = outlineColor;
            if (isActiveAndEnabled)
                Apply();
        }

        void OnEnable() => Apply();

        void Apply()
        {
            var text = GetComponent<TextMeshProUGUI>();
            if (text == null || text.font == null || width <= 0f)
                return;
            text.outlineWidth = width;
            text.outlineColor = color;
        }
    }

    /// <summary>Keeps a sliced pill's ends fully round whatever its height.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class MenuPill : MonoBehaviour
    {
        void OnEnable() => Refresh();
        void OnRectTransformDimensionsChange() => Refresh();

        void Refresh()
        {
            var image = GetComponent<Image>();
            var rect = (RectTransform)transform;
            if (image == null || image.sprite == null)
                return;
            float radius = Mathf.Max(1f, Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f);
            float border = image.sprite.border.x;
            image.pixelsPerUnitMultiplier = border / radius;
        }
    }
}
