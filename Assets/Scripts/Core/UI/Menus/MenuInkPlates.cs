using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Data.Config;
using Object = UnityEngine.Object;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// Illustrated main-menu button plates (normal / hover / press) as 9-sliced sprites. Builds inked basalt-and-ember
    /// plates in code so the menu never waits on Gen; when Gen plates land in Resources/UI/MenuButtonPlate*.png they
    /// replace the stand-ins on the next menu open. Presentation only.
    /// </summary>
    public static class MenuInkPlates
    {
        const string ResourcesNormal = "UI/MenuButtonPlate";
        const string ResourcesHover = "UI/MenuButtonPlateHover";
        const string ResourcesPressed = "UI/MenuButtonPlatePressed";
        const int TexW = 128;
        const int TexH = 64;
        const int Border = 18;

        static Sprite normal, hover, pressed;
        static bool built;

        public static Sprite Normal => Ensure().normal;
        public static Sprite Hover => Ensure().hover;
        public static Sprite Pressed => Ensure().pressed;
        public static bool Ready => Normal != null;

        /// <summary>True when the live sprites came from Resources (Gen-imported), not the procedural stand-in.</summary>
        public static bool FromGen { get; private set; }

        static (Sprite normal, Sprite hover, Sprite pressed) Ensure()
        {
            if (built)
                return (normal, hover, pressed);
            built = true;
            // Prefer Gen plates when Studio has imported them (studio_gen_import → Resources/UI/).
            // Prefer Gen plates (Resources/UI/*.png). Accept either a Sprite import or a Texture2D.
            normal = LoadPlate(ResourcesNormal, "MenuButtonPlate");
            if (normal != null)
            {
                FromGen = true;
                hover = LoadPlate(ResourcesHover, "MenuButtonPlateHover") ?? normal;
                pressed = LoadPlate(ResourcesPressed, "MenuButtonPlatePressed") ?? normal;
                return (normal, hover, pressed);
            }

            FromGen = false;
            normal = MakePlate("MenuInkPlate", new Color(0.92f, 0.32f, 0.18f), new Color(1f, 0.72f, 0.28f), false, false);
            hover = MakePlate("MenuInkPlateHover", new Color(1f, 0.78f, 0.28f), new Color(1f, 0.92f, 0.55f), true, false);
            pressed = MakePlate("MenuInkPlatePressed", new Color(0.72f, 0.18f, 0.12f), new Color(0.95f, 0.45f, 0.18f), false, true);
            return (normal, hover, pressed);
        }

        /// <summary>Applies the illustrated plates to a theme button style (shape Sprite + hover/press sprites).</summary>
        public static void ApplyTo(MenuButtonStyle style)
        {
            if (style == null) return;
            Ensure();
            if (normal == null) return;
            style.surface.shape = MenuShape.Sprite;
            style.surface.sprite = normal;
            style.surface.fill = Color.white;
            style.surface.borderWidth = 0f;
            style.surface.shadowColor = new Color(0.07f, 0.04f, 0.08f, 0.55f);
            style.surface.shadowOffset = new Vector2(0f, -8f);
            style.hoverSprite = hover;
            style.pressedSprite = pressed;
            // Keep readable label contrast on the inked plate.
            if (style.hoverFill.a < 0.01f)
                style.hoverFill = new Color(1f, 1f, 1f, 0.15f);
            if (style.pressedFill.a < 0.01f)
                style.pressedFill = new Color(0.7f, 0.7f, 0.7f, 0.35f);
        }

        static Sprite LoadPlate(string resourcesPath, string name)
        {
            var asSprite = Resources.Load<Sprite>(resourcesPath);
            if (asSprite != null)
            {
                // Re-slice so 9-slice borders exist even if the importer left them empty.
                if (asSprite.border.sqrMagnitude > 0f)
                    return asSprite;
                var tex = asSprite.texture;
                if (tex != null)
                    return Slice(tex, name);
                return asSprite;
            }
            var asTex = Resources.Load<Texture2D>(resourcesPath);
            return asTex != null ? Slice(asTex, name) : null;
        }

        static Sprite Slice(Texture2D tex, string name)
        {
            if (tex == null) return null;
            int b = Mathf.Min(Border, tex.width / 4, tex.height / 4);
            // Readable copy: imported textures are often non-readable in player builds.
            Texture2D readable = tex;
            if (!tex.isReadable)
            {
                readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                try
                {
                    var tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(tex, tmp);
                    var prev = RenderTexture.active;
                    RenderTexture.active = tmp;
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readable.Apply(false, false);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(tmp);
                }
                catch
                {
                    Object.Destroy(readable);
                    return null;
                }
            }
            var sprite = Sprite.Create(readable, new Rect(0, 0, readable.width, readable.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = name;
            return sprite;
        }

        static Sprite MakePlate(string name, Color fill, Color rim, bool bright, bool pressedIn)
        {
            var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var ink = new Color(0.08f, 0.04f, 0.07f, 1f);
            var cyan = new Color(0.30f, 0.88f, 0.82f, 1f);
            float inset = pressedIn ? 3f : 2f;
            for (int y = 0; y < TexH; y++)
            for (int x = 0; x < TexW; x++)
            {
                float nx = x / (float)(TexW - 1);
                float ny = y / (float)(TexH - 1);
                bool edge = x < 3 || y < 3 || x >= TexW - 3 || y >= TexH - 3;
                bool inner = x >= inset && y >= inset && x < TexW - inset && y < TexH - inset;
                Color c;
                if (edge)
                    c = ink;
                else if (!inner)
                    c = Color.Lerp(ink, rim, 0.55f);
                else
                {
                    // Basalt grain + ember wash.
                    float grain = Mathf.PerlinNoise(x * 0.18f, y * 0.22f);
                    c = Color.Lerp(fill, rim, 0.15f + 0.25f * grain);
                    if (bright)
                        c = Color.Lerp(c, Color.white, 0.12f);
                    // Cyan rewind seam along the bottom inside the ink rim.
                    if (y >= 5 && y <= 8)
                        c = Color.Lerp(c, cyan, 0.75f);
                    // Soft top highlight.
                    if (y > TexH - 10)
                        c = Color.Lerp(c, Color.white, 0.08f * ((y - (TexH - 10)) / 9f));
                }
                // Rounded corners: punch transparent outside a soft radius so the 9-slice looks plate-like.
                float cx = Mathf.Min(x, TexW - 1 - x);
                float cy = Mathf.Min(y, TexH - 1 - y);
                if (cx < 10 && cy < 10)
                {
                    float d = Vector2.Distance(new Vector2(10 - cx, 10 - cy), Vector2.zero);
                    if (d > 10f)
                        c.a = 0f;
                    else if (d > 8f)
                        c.a *= 1f - (d - 8f) / 2f;
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply(false, true);
            return Slice(tex, name);
        }

        /// <summary>Swaps a button fill's sprite for the plate that matches its selection state.</summary>
        public static void ApplyState(Image fill, SelectionStateProxy state)
        {
            if (fill == null || !Ready) return;
            switch (state)
            {
                case SelectionStateProxy.Hover:
                    fill.sprite = Hover;
                    break;
                case SelectionStateProxy.Pressed:
                    fill.sprite = Pressed;
                    break;
                default:
                    fill.sprite = Normal;
                    break;
            }
            fill.type = Image.Type.Sliced;
            fill.color = Color.white;
        }

        public enum SelectionStateProxy { Normal, Hover, Pressed, Disabled }
    }
}
