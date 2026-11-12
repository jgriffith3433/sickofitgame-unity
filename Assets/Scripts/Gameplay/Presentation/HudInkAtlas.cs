using UnityEngine;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Inked HUD atlas (Assets/Resources/UI/HudInkAtlas): vessel, cracked vessel, reel, gem and dial as 128 px
    /// tiles. When the Gen sheet is missing, procedural stand-ins match the art bible's outline (#140B0D) and
    /// ember/brass colours so the HUD stays playable. Presentation only.
    /// </summary>
    public static class HudInkAtlas
    {
        public const int Tile = 128;
        const string ResourcePath = "UI/HudInkAtlas";

        static Sprite vesselFull, vesselCracked, vesselEmpty, reel, gem, dialFace, dialNeedle;
        static bool tried;

        public static Sprite VesselFull { get { Load(); return vesselFull; } }
        public static Sprite VesselCracked { get { Load(); return vesselCracked; } }
        public static Sprite VesselEmpty { get { Load(); return vesselEmpty; } }
        public static Sprite Reel { get { Load(); return reel; } }
        public static Sprite Gem { get { Load(); return gem; } }
        public static Sprite DialFace { get { Load(); return dialFace; } }
        public static Sprite DialNeedle { get { Load(); return dialNeedle; } }

        /// <summary>True when the Gen-made atlas texture is loaded (stand-ins leave this false).</summary>
        public static bool HasBakedArt { get; private set; }

        static void Load()
        {
            if (tried) return;
            tried = true;
            var tex = Resources.Load<Texture2D>(ResourcePath);
            if (tex != null && tex.width >= Tile * 3 && tex.height >= Tile)
            {
                HasBakedArt = true;
                // Prefer a 6-wide strip along the bottom (Gen min height is 256, so a 768x256 sheet is common).
                if (tex.width >= Tile * 6)
                {
                    vesselFull = Slice(tex, 0, 0);
                    vesselCracked = Slice(tex, 1, 0);
                    vesselEmpty = Slice(tex, 2, 0);
                    reel = Slice(tex, 3, 0);
                    gem = Slice(tex, 4, 0);
                    dialFace = Slice(tex, 5, 0);
                    dialNeedle = dialFace;
                }
                else
                {
                    // 3-wide grid: bottom row reel/gem/dial, top row vessels (Unity y=0 is bottom).
                    vesselFull = Slice(tex, 0, tex.height >= Tile * 2 ? 1 : 0);
                    vesselCracked = Slice(tex, 1, tex.height >= Tile * 2 ? 1 : 0);
                    vesselEmpty = Slice(tex, 2, tex.height >= Tile * 2 ? 1 : 0);
                    reel = Slice(tex, 0, 0);
                    gem = Slice(tex, 1, 0);
                    dialFace = Slice(tex, 2, 0);
                    dialNeedle = dialFace;
                }
                return;
            }

            HasBakedArt = false;
            vesselFull = DrawVessel(full: true, cracked: false);
            vesselCracked = DrawVessel(full: false, cracked: true);
            vesselEmpty = DrawVessel(full: false, cracked: false);
            reel = DrawReel();
            gem = DrawGem();
            dialFace = DrawDialFace();
            dialNeedle = DrawDialNeedle();
        }

        static Sprite Slice(Texture2D tex, int col, int row)
        {
            var s = Sprite.Create(tex, new Rect(col * Tile, row * Tile, Tile, Tile), new Vector2(0.5f, 0.5f), Tile);
            s.name = $"HudInk_{col}_{row}";
            return s;
        }

        static readonly Color32 Ink = new Color32(0x14, 0x0B, 0x0D, 255);
        static readonly Color32 Ember = new Color32(0xFF, 0x5B, 0x3A, 255);
        static readonly Color32 EmberHot = new Color32(0xFF, 0x8A, 0x1F, 255);
        static readonly Color32 EmberDark = new Color32(0x7A, 0x1F, 0x2B, 255);
        static readonly Color32 Brass = new Color32(0xC8, 0x9A, 0x3A, 255);
        static readonly Color32 BrassDark = new Color32(0x8A, 0x5A, 0x2B, 255);
        static readonly Color32 Teal = new Color32(0x4C, 0xE0, 0xD2, 255);
        static readonly Color32 GemCore = new Color32(0x7A, 0xE0, 0xFF, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        static Sprite DrawVessel(bool full, bool cracked)
        {
            int n = Tile;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            float cx = n * 0.5f, cy = n * 0.48f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float vx = (x + 0.5f - cx) / (n * 0.32f);
                float vy = (y + 0.5f - cy) / (n * 0.36f);
                float d = vx * vx + vy * vy;
                Color32 c = Clear;
                if (d < 1.05f)
                {
                    bool edge = d > 0.82f;
                    if (edge) c = Ink;
                    else if (full) c = y > cy + n * 0.05f ? EmberHot : Ember;
                    else if (cracked) c = EmberDark;
                    else c = new Color32(0x2A, 0x16, 0x18, 220);
                }
                if (cracked && d < 0.95f)
                {
                    float crack = Mathf.Abs((x - cx) - (y - cy) * 0.4f);
                    if (crack < 1.6f) c = Ink;
                    crack = Mathf.Abs((x - cx) * 0.5f + (y - cy) - n * 0.1f);
                    if (crack < 1.2f && y < cy) c = Ink;
                }
                if (full && d > 0.7f && d < 0.82f) c = EmberHot;
                px[y * n + x] = c;
            }
            return Bake(tex, px, "vessel_" + (full ? "full" : cracked ? "crack" : "empty"));
        }

        static Sprite DrawReel()
        {
            int n = Tile;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Dist(x + 0.5f, y + 0.5f, c, c);
                Color32 col = Clear;
                if (d < n * 0.46f)
                {
                    col = d > n * 0.42f ? Ink : Brass;
                    float a = Mathf.Atan2(y + 0.5f - c, x + 0.5f - c);
                    float spoke = Mathf.Abs(Mathf.Sin(a * 3f));
                    if (spoke < 0.18f && d > n * 0.12f && d < n * 0.4f) col = Ink;
                    if (d < n * 0.1f) col = BrassDark;
                    if (d < n * 0.05f) col = Teal;
                    if (d > n * 0.28f && d < n * 0.36f) col = Teal;
                }
                px[y * n + x] = col;
            }
            return Bake(tex, px, "reel");
        }

        static Sprite DrawGem()
        {
            int n = Tile;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            float cx = n * 0.5f, cy = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - cx) / (n * 0.28f);
                float dy = Mathf.Abs(y + 0.5f - cy) / (n * 0.36f);
                float d = dx + dy;
                Color32 col = Clear;
                if (d < 1.05f)
                {
                    col = d > 0.88f ? Ink : (y > cy ? GemCore : new Color32(0x3A, 0x90, 0xC0, 255));
                    if (Mathf.Abs(x + 0.5f - cx) < 1.4f && d < 0.85f)
                        col = Color32.Lerp(col, new Color32(255, 255, 255, 255), 0.45f);
                }
                px[y * n + x] = col;
            }
            return Bake(tex, px, "gem");
        }

        static Sprite DrawDialFace()
        {
            int n = Tile;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Dist(x + 0.5f, y + 0.5f, c, c);
                Color32 col = Clear;
                if (d < n * 0.46f)
                {
                    col = d > n * 0.42f ? Ink : BrassDark;
                    if (d < n * 0.38f) col = new Color32(0x2A, 0x18, 0x12, 240);
                    float a = Mathf.Atan2(y + 0.5f - c, x + 0.5f - c);
                    float tick = Mathf.Abs(Mathf.Repeat(a + Mathf.PI, Mathf.PI / 5f) - Mathf.PI / 10f);
                    if (tick < 0.04f && d > n * 0.28f && d < n * 0.4f) col = Brass;
                    if (d < n * 0.06f) col = Brass;
                }
                px[y * n + x] = col;
            }
            return Bake(tex, px, "dialFace");
        }

        static Sprite DrawDialNeedle()
        {
            int n = Tile;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - c);
                float dy = y + 0.5f - c;
                Color32 col = Clear;
                if (dx < 2.2f && dy > 0f && dy < n * 0.38f) col = EmberHot;
                if (dx < 3.2f && dy > 0f && dy < n * 0.38f && col.a == 0) col = Ink;
                if (Dist(x + 0.5f, y + 0.5f, c, c) < 4f) col = Brass;
                px[y * n + x] = col;
            }
            return Bake(tex, px, "dialNeedle");
        }

        static float Dist(float x, float y, float cx, float cy) =>
            Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

        static Texture2D NewTex(int n) =>
            new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "HudInkStandIn",
            };

        static Sprite Bake(Texture2D tex, Color32[] px, string name)
        {
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Tile);
            s.name = name;
            return s;
        }
    }
}
