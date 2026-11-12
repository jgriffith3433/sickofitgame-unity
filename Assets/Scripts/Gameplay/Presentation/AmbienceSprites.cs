using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The built-in art of a Circle's air: weather bits and background silhouettes drawn into small white textures once (tinted by the
    /// def's colour), so a Circle with no painted art still has weather and life. White so one sprite serves every colour.
    /// Gen-painted bits land in <c>Resources/Ambience/particle_*</c> (see Tools/Art/ambience_particles.py); when present they win over
    /// the procedural Soft() shapes. WeatherDef.sprite also wins via <see cref="BitOr"/>.
    /// </summary>
    internal static class AmbienceSprites
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> painted = new Dictionary<string, Sprite>();
        static bool paintedTried;
        static int paintedCount;

        /// <summary>How many Gen particle textures were found under Resources/Ambience (0 = stand-ins only).</summary>
        public static int PaintedBitCount
        {
            get
            {
                EnsurePainted();
                return paintedCount;
            }
        }

        public static bool HasPaintedArt => PaintedBitCount > 0;

        public static Sprite Bit(WeatherKind kind)
        {
            var fromGen = PaintedFor(kind);
            if (fromGen != null)
                return fromGen;
            switch (kind)
            {
                case WeatherKind.Sparks: return Make("spark", 24, (x, y) => Soft(Mathf.Pow(Mathf.Abs(x), 0.55f) + Mathf.Pow(Mathf.Abs(y), 0.55f), 1f, 0.35f));
                case WeatherKind.Cinders: return Make("cinder", 24, (x, y) => Soft(Mathf.Abs(x) * 1.1f + Mathf.Abs(y) * 1.35f, 0.95f, 0.2f));
                case WeatherKind.Runes: return Make("rune", 32, Rune);
                case WeatherKind.Grit: return Make("grit", 16, (x, y) => Soft(Mathf.Max(Mathf.Abs(x), Mathf.Abs(y * 1.2f)), 0.8f, 0.2f));
                case WeatherKind.Pollen: return Make("pollen", 20, (x, y) => Soft(Mathf.Sqrt(x * x + y * y * 1.4f), 0.85f, 0.7f));
                case WeatherKind.Debris: return Make("debris", 28, (x, y) => Soft(Mathf.Abs(x) * 0.4f + Mathf.Abs(y), 0.95f, 0.35f));
                case WeatherKind.Steam: return Make("steam", 32, (x, y) => Soft(Mathf.Sqrt(x * x * 0.7f + y * y), 1f, 0.85f));
                case WeatherKind.Dust: return Make("dust", 18, (x, y) => Soft(Mathf.Sqrt(x * x + y * y), 0.7f, 0.55f));
                case WeatherKind.Soul: return Make("soul", 28, (x, y) => Soft(Mathf.Sqrt(x * x * 0.6f + (y - 0.15f) * (y - 0.15f)), 0.55f, 0.5f));
                default: return Make("flake", 24, (x, y) => Soft(Mathf.Sqrt(x * x + y * y), 1f, 0.6f));
            }
        }

        /// <summary>Optional Gen-painted bit: WeatherDef.sprite wins, then Resources/Ambience, then the built-in shape.</summary>
        public static Sprite BitOr(WeatherKind kind, Sprite paintedSprite) => paintedSprite != null ? paintedSprite : Bit(kind);

        static string ResourceId(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Sparks: return "spark";
                case WeatherKind.Pollen: return "pollen";
                case WeatherKind.Debris: return "debris";
                case WeatherKind.Steam: return "steam";
                case WeatherKind.Dust: return "dust";
                case WeatherKind.Soul: return "soul";
                case WeatherKind.Cinders: return "spark"; // shared glow fleck until its own sheet lands
                case WeatherKind.Ash:
                case WeatherKind.Grit: return "dust";
                default: return null;
            }
        }

        static Sprite PaintedFor(WeatherKind kind)
        {
            EnsurePainted();
            string id = ResourceId(kind);
            if (id == null)
                return null;
            return painted.TryGetValue(id, out var s) ? s : null;
        }

        static void EnsurePainted()
        {
            if (paintedTried)
                return;
            paintedTried = true;
            paintedCount = 0;
            // Expected files (Tools/Art/ambience_particles.py): particle_pollen/spark/steam/dust/soul/debris.png
            string[] ids = { "pollen", "spark", "steam", "dust", "soul", "debris" };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                var tex = Resources.Load<Texture2D>("Ambience/particle_" + id);
                if (tex == null)
                    continue;
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                    Mathf.Max(tex.width, tex.height));
                sprite.name = "particle_" + id;
                painted[id] = sprite;
                paintedCount++;
            }
        }

        public static Sprite Life(LifeShape shape)
        {
            switch (shape)
            {
                case LifeShape.Figure: return Make("figure", 64, Figure);
                case LifeShape.Horned: return Make("horned", 64, Horned);
                case LifeShape.Wisp: return Make("wisp", 64, Wisp);
                default: return Make("wing", 64, Wing);
            }
        }

        /// <summary>1 inside 'edge' fading to 0 over 'soft' (a share of the edge).</summary>
        static float Soft(float d, float edge, float soft)
        {
            return Mathf.Clamp01((edge - d) / Mathf.Max(0.001f, edge * soft));
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by, float width)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / Mathf.Max(0.0001f, dx * dx + dy * dy));
            float px = ax + dx * t - x, py = ay + dy * t - y;
            return Soft(Mathf.Sqrt(px * px + py * py), width, 0.4f);
        }

        static float Disc(float x, float y, float cx, float cy, float r) => Soft(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)), r, 0.3f);

        static float Rune(float x, float y)
        {
            float a = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.82f) < 0.09f ? 1f : 0f;
            float b = Segment(x, y, 0f, -0.55f, 0f, 0.55f, 0.1f);
            float c = Segment(x, y, -0.35f, 0.2f, 0.35f, 0.2f, 0.09f);
            float d = Segment(x, y, 0f, -0.15f, 0.38f, -0.55f, 0.09f);
            return Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
        }

        static float Wing(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float line = 0.05f + 0.55f * Mathf.Pow(ax, 1.4f) - 0.25f * ax * ax * ax;
            float thick = 0.07f + 0.1f * (1f - ax * 0.7f);
            float wing = ax <= 1f ? Soft(Mathf.Abs(y - (line - 0.2f)), thick, 0.5f) : 0f;
            return Mathf.Max(wing, Disc(x, y, 0f, -0.12f, 0.17f));
        }

        static float Figure(float x, float y)
        {
            float head = Disc(x, y, 0f, 0.68f, 0.17f);
            float body = Segment(x, y, 0f, 0.5f, 0f, -0.15f, 0.13f);
            float arms = Mathf.Max(Segment(x, y, 0f, 0.4f, -0.5f, 0.1f, 0.07f), Segment(x, y, 0f, 0.4f, 0.5f, 0.2f, 0.07f));
            float legs = Mathf.Max(Segment(x, y, 0f, -0.15f, -0.3f, -0.9f, 0.08f), Segment(x, y, 0f, -0.15f, 0.28f, -0.9f, 0.08f));
            return Mathf.Max(Mathf.Max(head, body), Mathf.Max(arms, legs));
        }

        static float Horned(float x, float y)
        {
            float head = Disc(x, y, 0f, 0.55f, 0.2f);
            float horns = Mathf.Max(Segment(x, y, -0.12f, 0.68f, -0.35f, 0.98f, 0.06f), Segment(x, y, 0.12f, 0.68f, 0.35f, 0.98f, 0.06f));
            float torso = Segment(x, y, 0f, 0.38f, 0f, -0.2f, 0.34f);
            float arms = Mathf.Max(Segment(x, y, -0.3f, 0.25f, -0.62f, -0.2f, 0.1f), Segment(x, y, 0.3f, 0.25f, 0.62f, -0.2f, 0.1f));
            float legs = Mathf.Max(Segment(x, y, -0.15f, -0.2f, -0.28f, -0.95f, 0.13f), Segment(x, y, 0.15f, -0.2f, 0.28f, -0.95f, 0.13f));
            return Mathf.Max(Mathf.Max(head, horns), Mathf.Max(Mathf.Max(torso, arms), legs));
        }

        static float Wisp(float x, float y)
        {
            float core = Disc(x, y, 0.35f, 0f, 0.3f);
            float tail = x < 0.35f ? Soft(Mathf.Abs(y), 0.28f * Mathf.Clamp01((x + 1f) / 1.35f), 0.8f) : 0f;
            return Mathf.Max(core, tail * 0.7f);
        }

        /// <summary>Draws f(x, y) over -1..1 on both axes as alpha, white.</summary>
        static Sprite Make(string key, int size, Func<float, float, float> alpha)
        {
            if (cache.TryGetValue(key, out var sprite) && sprite != null)
                return sprite;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size * 2f - 1f, y = (py + 0.5f) / size * 2f - 1f;
                    pixels[py * size + px] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y)));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            // One unit square at this pixels-per-unit; the def's size scales it.
            sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.DontSave;
            cache[key] = sprite;
            return sprite;
        }
    }
}
