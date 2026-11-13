using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The death scene's backdrop (Docs/Story.md "How he dies"): the same cracked-lava, ember palette as the menu's
    /// diorama and the floors, replacing the old photographic fire-vortex film. An ash sky, a cracked basalt floor with
    /// glowing seams, and a vortex of embers that spirals in over a pulsing glow. Presentation only: tiny generated
    /// textures, unscaled time, nothing allocated per frame after construct, nothing saved or rewound.
    /// <see cref="Progress"/> reaches 1 after <see cref="Seconds"/>, which is what ends the scene.
    /// </summary>
    public sealed class DeathEmberBackdrop : MonoBehaviour
    {
        public const float Seconds = 6f;
        const int EmberCount = 56;
        const float Wide = 60f;

        static readonly Color Ash = new Color(0.03f, 0.015f, 0.02f);
        static readonly Color Ember = new Color(0.36f, 0.08f, 0.02f);
        static readonly Color Lava = new Color(1f, 0.45f, 0.1f);
        static readonly Color Hot = new Color(1f, 0.72f, 0.25f);

        Transform[] bits;
        SpriteRenderer[] bitRenderers;
        float[] phase, lane;
        SpriteRenderer glow;
        Texture2D sky, floor, disc;
        float startedAt;

        /// <summary>0 at the start, 1 once the scene has played its length.</summary>
        public float Progress => Mathf.Clamp01((Time.unscaledTime - startedAt) / Seconds);

        public static DeathEmberBackdrop Create(Transform parent)
        {
            var go = new GameObject("DeathEmberBackdrop");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<DeathEmberBackdrop>();
            b.Construct();
            return b;
        }

        void Construct()
        {
            startedAt = Time.unscaledTime;
            sky = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                sky.SetPixel(0, y, Color.Lerp(Ember, Ash, y / 63f));
            sky.Apply();
            var skySprite = Sprite.Create(sky, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 1f);
            Quad("Sky", skySprite, Color.white, new Vector2(0f, 0f), new Vector2(Wide, Wide / 64f), 0);

            floor = PaintFloor();
            var floorSprite = Sprite.Create(floor, new Rect(0, 0, floor.width, floor.height), new Vector2(0.5f, 0.5f), floor.height / 4f);
            Quad("Floor", floorSprite, Color.white, new Vector2(0f, -3.4f), new Vector2(3f, 1f), 1);

            disc = PaintDisc();
            var discSprite = Sprite.Create(disc, new Rect(0, 0, disc.width, disc.height), new Vector2(0.5f, 0.5f), disc.width / 6f);
            glow = Quad("Glow", discSprite, new Color(Lava.r, Lava.g, Lava.b, 0.6f), new Vector2(0f, 0.2f), Vector2.one, 2);

            var dot = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            bits = new Transform[EmberCount];
            bitRenderers = new SpriteRenderer[EmberCount];
            phase = new float[EmberCount];
            lane = new float[EmberCount];
            for (int i = 0; i < EmberCount; i++)
            {
                float h = Frac(Mathf.Sin(i * 12.9898f) * 43758.5453f);
                float h2 = Frac(Mathf.Sin(i * 78.233f) * 12543.311f);
                phase[i] = h * Mathf.PI * 2f;
                lane[i] = h2;
                var r = Quad("Ember" + i, dot, Hot, Vector2.zero, Vector2.one * Mathf.Lerp(0.08f, 0.2f, h2), 3);
                bits[i] = r.transform;
                bitRenderers[i] = r;
            }
            Tick();
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        SpriteRenderer Quad(string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            SpriteLayers.Apply(r, SpriteLayer.Background1, order);
            return r;
        }

        /// <summary>Dark basalt with glowing seams: ridged noise, bright where it is thin.</summary>
        static Texture2D PaintFloor()
        {
            const int W = 192, H = 64;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.07f + 3.1f, y * 0.11f + 7.7f);
                    float seam = 1f - Mathf.Clamp01(Mathf.Abs(n - 0.5f) * 11f);
                    float fade = Mathf.Clamp01((H - y) / 10f); // the top edge melts into the sky
                    Color rock = Color.Lerp(Ash, Ember * 0.7f, 0.25f + 0.2f * Mathf.PerlinNoise(x * 0.2f, y * 0.25f));
                    Color c = Color.Lerp(rock, Color.Lerp(Lava, Hot, seam * seam), seam);
                    c = Color.Lerp(rock, c, fade);
                    c.a = y >= H - 3 ? 0f : 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static Texture2D PaintDisc()
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f));
                    float a = Mathf.Pow(1f - d, 2f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        void Update() => Tick();

        void Tick()
        {
            float t = Time.unscaledTime - startedAt;
            float p = Mathf.Clamp01(t / Seconds);
            float pulse = 1f + 0.12f * Mathf.Sin(t * 5f);
            float g = Mathf.Lerp(0.8f, 3.4f, Mathf.SmoothStep(0f, 1f, p)) * pulse;
            glow.transform.localScale = new Vector3(g, g, 1f);
            glow.color = new Color(Lava.r, Lava.g, Lava.b, Mathf.Lerp(0.35f, 0.75f, p));
            for (int i = 0; i < EmberCount; i++)
            {
                // Each ember rides a spiral that tightens upward; its lane staggers where it is in the loop.
                float u = Frac(t * (0.18f + 0.1f * lane[i]) + lane[i]);
                float radius = Mathf.Lerp(3.6f, 0.15f, u) * (0.8f + 0.5f * lane[i]);
                float ang = phase[i] + t * 2.2f + u * 9f;
                bits[i].localPosition = new Vector3(Mathf.Cos(ang) * radius, 0.2f + u * 3.2f + Mathf.Sin(ang) * radius * 0.25f - 1.3f * (1f - u), 0f);
                float a = Mathf.Sin(u * Mathf.PI);
                bitRenderers[i].color = new Color(Color.Lerp(Lava, Hot, u).r, Color.Lerp(Lava, Hot, u).g, Color.Lerp(Lava, Hot, u).b, a);
            }
        }

        void OnDestroy()
        {
            if (sky != null) Destroy(sky);
            if (floor != null) Destroy(floor);
            if (disc != null) Destroy(disc);
        }
    }
}
