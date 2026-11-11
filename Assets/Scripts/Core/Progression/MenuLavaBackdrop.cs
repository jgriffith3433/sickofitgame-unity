using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The main menu's world, in the game's one palette (black, orange, cyan): an ash-black sky going to ember, a far
    /// lava sea, the basalt ledge the hero stands on with a cyan rewind seam, and embers rising. It replaces the old
    /// meadow video and house, so the first screen is the same Hell as Floor 1000. Presentation only: sprites built from
    /// tiny generated textures, positions from the clock, nothing saved or rewound, no per-frame allocation.
    /// </summary>
    public sealed class MenuLavaBackdrop : MonoBehaviour
    {
        public const string Name = "MenuLavaBackdrop";
        /// <summary>The ground the hero stands on (config.characterPosition.y is his feet's place; the ledge top sits just under).</summary>
        const float LedgeTop = -3.3f;
        const int EmberCount = 36;

        static readonly Color Ash = new Color(0.03f, 0.015f, 0.02f);
        static readonly Color Ember = new Color(0.36f, 0.08f, 0.02f);
        static readonly Color Lava = new Color(1f, 0.45f, 0.1f);
        static readonly Color Hot = new Color(1f, 0.72f, 0.25f);
        static readonly Color Cyan = new Color(0.30f, 0.88f, 0.82f); // theme hud.rewind #4CE0D2

        Texture2D ramp, white;
        Sprite rampSprite, whiteSprite;
        Transform[] embers;
        SpriteRenderer[] emberRenderers;
        SpriteRenderer seam, surface;

        /// <summary>Builds the backdrop under <paramref name="parent"/> (the scene's content root) and hides the meadow's objects.</summary>
        public static MenuLavaBackdrop Build(Transform parent)
        {
            // The meadow video and the house, whichever way the scene carries them.
            foreach (var name in new[] { "MainMenu_Background1", "MainMenu_LeftPartOfHouse" })
            {
                var old = GameObject.Find(name);
                if (old != null)
                    old.SetActive(false);
            }
            var existing = GameObject.Find(Name);
            if (existing != null)
                return existing.GetComponent<MenuLavaBackdrop>();
            var go = new GameObject(Name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            var b = go.AddComponent<MenuLavaBackdrop>();
            b.Construct();
            return b;
        }

        void Construct()
        {
            ramp = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                ramp.SetPixel(0, y, Color.Lerp(Ember, Ash, y / 63f)); // bottom = ember, top = ash
            ramp.Apply();
            white = Texture2D.whiteTexture;
            rampSprite = Sprite.Create(ramp, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);

            const float W = 60f; // wide enough for any aspect the portrait cover can ask for
            Quad("Sky", rampSprite, Color.white, new Vector2(0f, 2f), new Vector2(W, 40f), -30, true);
            Quad("FarSea", whiteSprite, new Color(Lava.r, Lava.g, Lava.b, 0.35f), new Vector2(0f, LedgeTop - 0.1f), new Vector2(W, 2.6f), -20, false);
            surface = Quad("SeaLine", whiteSprite, Hot, new Vector2(0f, LedgeTop + 0.04f), new Vector2(W, 0.05f), -19, false);
            Quad("Ledge", whiteSprite, Ash, new Vector2(0f, LedgeTop - 1.4f), new Vector2(W, 2.8f), -10, false);
            seam = Quad("RewindSeam", whiteSprite, Cyan, new Vector2(0f, LedgeTop - 0.02f), new Vector2(W, 0.035f), -9, false);

            embers = new Transform[EmberCount];
            emberRenderers = new SpriteRenderer[EmberCount];
            for (int i = 0; i < EmberCount; i++)
            {
                bool cyan = i % 9 == 0;
                float sz = 0.05f + 0.03f * (i % 3);
                var r = Quad("Ember" + i, whiteSprite, cyan ? Cyan : Lava, Vector2.zero, new Vector2(sz, sz), -8, false);
                embers[i] = r.transform;
                emberRenderers[i] = r;
            }
            Tick(0f);
        }

        SpriteRenderer Quad(string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, int order, bool ramped)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            // The ramp sprite is 1 x 64 units, a white sprite 1 x 1: scale each to the size asked for.
            go.transform.localScale = ramped ? new Vector3(size.x, size.y / 64f, 1f) : new Vector3(size.x, size.y, 1f);
            SpriteLayers.Apply(r, SpriteLayer.Background1, order);
            return r;
        }

        void Update() => Tick(Time.unscaledTime);

        void Tick(float t)
        {
            if (embers == null) return;
            const float span = 8f; // how far up an ember rises
            for (int i = 0; i < embers.Length; i++)
            {
                float ph = (t * (0.05f + 0.025f * (i % 5)) + i * 0.137f) % 1f;
                float x = (Mathf.Sin(i * 12.9898f) * 0.5f) * 16f + Mathf.Sin(t * 0.8f + i) * 0.15f;
                embers[i].localPosition = new Vector3(x, Mathf.Lerp(LedgeTop, LedgeTop + span, ph), 0f);
                var c = emberRenderers[i].color;
                c.a = (1f - ph) * 0.9f;
                emberRenderers[i].color = c;
            }
            if (surface != null)
            {
                var c = surface.color;
                c.a = 0.7f + 0.3f * Mathf.Sin(t * 2.2f);
                surface.color = c;
            }
        }

        void OnDestroy()
        {
            if (rampSprite != null) Destroy(rampSprite);
            if (whiteSprite != null) Destroy(whiteSprite);
            if (ramp != null) Destroy(ramp);
        }
    }
}
