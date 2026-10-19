using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The mouse cursor while a level plays: a gold ring with four fangs biting at a red dot, drawn in the HUD's
    /// colours (theme.json hud: marker, healthFill, worldBarOutline) with a dark rim so it reads on lava and sky
    /// alike. The hotspot is the dot, so the shot goes where the dot is. Menus, the pause and death screens, the
    /// Grudge picker and HUD buttons keep the system arrow. Presentation only: it never touches the simulation, and
    /// the playtest bot aims through the virtual mouse whatever the cursor looks like.
    /// </summary>
    public static class AimCursor
    {
        // 32x32 is the size every browser takes for a CSS cursor (WebGL turns this texture into one).
        const int Size = 32;
        static readonly Vector2 Hotspot = new Vector2(16f, 16f);

        static Texture2D texture;
        static bool showing;

        /// <summary>True while the crosshair is the cursor (the playtest checks it around the pause).</summary>
        public static bool Showing => showing;

        /// <summary>The mouse is over a UI element that takes clicks (a HUD button, a card, a menu).</summary>
        public static bool PointerOverUi
        {
            get
            {
                var events = UnityEngine.EventSystems.EventSystem.current;
                return events != null && events.IsPointerOverGameObject();
            }
        }

        /// <summary>Shows the crosshair (true) or the system arrow (false); only calls Unity when it changes.</summary>
        public static void Show(bool crosshair, MenuHudStyle style)
        {
            if (crosshair == showing)
                return;
            showing = crosshair;
            if (crosshair)
            {
                if (texture == null)
                    texture = Draw(style ?? new MenuHudStyle());
                Cursor.SetCursor(texture, Hotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        /// <summary>Back to the system arrow (the level is closing).</summary>
        public static void Reset(MenuHudStyle style) => Show(false, style);

        static Texture2D Draw(MenuHudStyle style)
        {
            var gold = Opaque(style.marker);
            var red = Opaque(style.healthFill);
            var rim = Opaque(style.worldBarOutline);

            // Created at runtime, so it stays readable (Cursor.SetCursor needs that) and uncompressed.
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "AimCursor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            // Texture rows count from the bottom, the hotspot from the top: pixel (16, 16) from the top-left
            // is centred at (16.5, 15.5) here.
            var centre = new Vector2(Hotspot.x + 0.5f, Size - Hotspot.y - 0.5f);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - centre;
                    float shape = Mathf.Min(Ring(p), Fangs(p));
                    float dot = p.magnitude - 1.6f;
                    float d = Mathf.Min(shape, dot);

                    float fill = Coverage(d);
                    float outline = Coverage(d - 1.5f);
                    var colour = Color.Lerp(gold, red, Coverage(dot));
                    colour = Color.Lerp(rim, colour, fill);
                    colour.a = outline;
                    pixels[y * Size + x] = colour;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Signed distance to the ring (radius 8.5, 2.4 px thick).</summary>
        static float Ring(Vector2 p) => Mathf.Abs(p.magnitude - 8.5f) - 1.2f;

        /// <summary>Signed distance to the four fangs: wedges from outside the ring narrowing toward the dot.</summary>
        static float Fangs(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var axis = i switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
                float along = Vector2.Dot(p, axis);
                float across = Mathf.Abs(p.x * axis.y - p.y * axis.x);
                const float inner = 4.5f, outer = 13.5f;
                float width = Mathf.Lerp(0.3f, 1.8f, Mathf.InverseLerp(inner, outer, along));
                float d = Mathf.Max(across - width, Mathf.Max(inner - along, along - outer));
                best = Mathf.Min(best, d);
            }
            return best;
        }

        /// <summary>How much of a pixel a shape covers, from its signed distance (a 1 px soft edge).</summary>
        static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        static Color Opaque(Color c)
        {
            c.a = 1f;
            return c;
        }
    }
}
