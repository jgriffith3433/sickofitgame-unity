using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The main menu's living lava diorama (Docs/Story.md "The main menu is a living diorama"): ash-to-ember sky, lava
    /// sea, basalt ledge with a cyan rewind seam, drifting embers, a slow camera breathe, and the Overlord's silhouette
    /// climbing past far above every 15–20 s. Presentation only: tiny generated textures, unscaled clock, no allocation
    /// per frame after construct, nothing saved or rewound. Replaces the old meadow video and house.
    /// </summary>
    public sealed class MenuLavaBackdrop : MonoBehaviour
    {
        public const string Name = "MenuLavaBackdrop";
        /// <summary>The ground the hero stands on (config.characterPosition.y is his feet's place; the ledge top sits just under).</summary>
        const float LedgeTop = -3.3f;
        const int EmberCount = 36;
        const float BreathSeconds = 20f;
        const float BreathScale = 0.03f; // ±3% ortho push
        const float OverlordMinGap = 15f;
        const float OverlordMaxGap = 20f;
        const float OverlordCrossSeconds = 5.5f;

        static readonly Color Ash = new Color(0.03f, 0.015f, 0.02f);
        static readonly Color Ember = new Color(0.36f, 0.08f, 0.02f);
        static readonly Color Lava = new Color(1f, 0.45f, 0.1f);
        static readonly Color Hot = new Color(1f, 0.72f, 0.25f);
        static readonly Color Cyan = new Color(0.30f, 0.88f, 0.82f); // theme hud.rewind #4CE0D2

        Texture2D ramp, white;
        Sprite rampSprite, whiteSprite;
        Transform[] embers;
        SpriteRenderer[] emberRenderers;
        SpriteRenderer seam, surface, glow;
        Transform overlordRoot;
        SpriteRenderer[] overlordParts;
        Camera cam;
        float baseOrtho = 3.6f;
        float nextOverlordAt;
        float overlordStartedAt = -1f;
        bool overlordGoingRight = true;
        float openedAt;
        /// <summary>Raised (presentation) when the Overlord silhouette starts a climb past; MainMenuManager sighs the hero.</summary>
        public static System.Action OverlordAppeared;

        /// <summary>The live backdrop, if the menu built one.</summary>
        public static MenuLavaBackdrop Active { get; private set; }

        /// <summary>How many times the Overlord's silhouette finished a climb past (playtest).</summary>
        public int OverlordPasses { get; private set; }

        /// <summary>Unscaled time of the last finished Overlord pass, or -1.</summary>
        public float LastOverlordPassAt { get; private set; } = -1f;

        /// <summary>True while embers are ticking (always, once built).</summary>
        public bool EmbersDrifting => embers != null && embers.Length > 0;

        /// <summary>0…1 breath phase (playtest / metrics).</summary>
        public float CameraBreath01 { get; private set; }

        /// <summary>True while the silhouette is on screen climbing.</summary>
        public bool OverlordVisible => overlordStartedAt >= 0f;

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
            {
                var found = existing.GetComponent<MenuLavaBackdrop>();
                Active = found;
                return found;
            }
            var go = new GameObject(Name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            var b = go.AddComponent<MenuLavaBackdrop>();
            b.Construct();
            Active = b;
            return b;
        }

        void Construct()
        {
            openedAt = Time.unscaledTime;
            cam = Camera.main;
            if (cam != null && cam.orthographic)
                baseOrtho = cam.orthographicSize;

            ramp = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                ramp.SetPixel(0, y, Color.Lerp(Ember, Ash, y / 63f)); // bottom = ember, top = ash
            ramp.Apply();
            white = Texture2D.whiteTexture;
            rampSprite = Sprite.Create(ramp, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);

            const float W = 60f; // wide enough for any aspect the portrait cover can ask for
            Quad("Sky", rampSprite, Color.white, new Vector2(0f, 2f), new Vector2(W, 40f), -30, true);
            glow = Quad("Glow", whiteSprite, new Color(Lava.r, Lava.g, Lava.b, 0.18f), new Vector2(0f, LedgeTop + 1.2f), new Vector2(W * 0.55f, 6f), -25, false);
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

            BuildOverlordSilhouette();
            // First glimpse soon enough that a 25 s sit always sees one, then every 15–20 s.
            nextOverlordAt = openedAt + 4.5f;
            Tick(0f);
        }

        void BuildOverlordSilhouette()
        {
            overlordRoot = new GameObject("OverlordSilhouette").transform;
            overlordRoot.SetParent(transform, false);
            overlordRoot.localPosition = new Vector3(-18f, LedgeTop + 7.2f, 0f);
            // Black obsidian with long horns and red eyes (canon m_d34d91cf19c3), drawn as flat quads — a silhouette
            // only; the far-ledge sheets stay out of the menu's load.
            overlordParts = new[]
            {
                QuadOn(overlordRoot, "Cloak", whiteSprite, new Color(0.02f, 0.01f, 0.03f, 0.92f), new Vector2(0f, 0.15f), new Vector2(0.95f, 1.55f), -6),
                QuadOn(overlordRoot, "Head", whiteSprite, new Color(0.04f, 0.02f, 0.05f, 0.95f), new Vector2(0f, 1.05f), new Vector2(0.55f, 0.45f), -5),
                QuadOn(overlordRoot, "HornL", whiteSprite, new Color(0.02f, 0.01f, 0.03f, 0.95f), new Vector2(-0.28f, 1.45f), new Vector2(0.12f, 0.55f), -5),
                QuadOn(overlordRoot, "HornR", whiteSprite, new Color(0.02f, 0.01f, 0.03f, 0.95f), new Vector2(0.28f, 1.45f), new Vector2(0.12f, 0.55f), -5),
                QuadOn(overlordRoot, "EyeL", whiteSprite, new Color(0.85f, 0.08f, 0.08f, 0.9f), new Vector2(-0.12f, 1.08f), new Vector2(0.08f, 0.06f), -4),
                QuadOn(overlordRoot, "EyeR", whiteSprite, new Color(0.85f, 0.08f, 0.08f, 0.9f), new Vector2(0.12f, 1.08f), new Vector2(0.08f, 0.06f), -4),
            };
            SetOverlordVisible(false);
        }

        SpriteRenderer QuadOn(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            SpriteLayers.Apply(r, SpriteLayer.Background1, order);
            return r;
        }

        void SetOverlordVisible(bool on)
        {
            if (overlordRoot == null) return;
            overlordRoot.gameObject.SetActive(on);
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
            if (glow != null)
            {
                var c = glow.color;
                c.a = 0.12f + 0.08f * (0.5f + 0.5f * Mathf.Sin(t * 0.7f));
                glow.color = c;
            }

            BreathCamera(t);
            TickOverlord(t);
            TickSeamWithHero(t);
        }

        /// <summary>Second ledge verb: stand on the cyan rewind seam and it brightens (fun-reviewer: more than click + walk).</summary>
        void TickSeamWithHero(float t)
        {
            if (seam == null) return;
            float heroX = 0f;
            bool near = false;
            var menu = MainMenuManager.ActiveHeroX(out heroX);
            if (menu)
            {
                // Ledge top is under his feet; horizontal near the camera centre / hero.
                near = Mathf.Abs(heroX) < 6f;
            }
            var c = Cyan;
            float pulse = 0.55f + 0.25f * Mathf.Sin(t * 2.4f);
            c.a = near ? Mathf.Lerp(0.85f, 1f, pulse) : 0.55f + 0.2f * Mathf.Sin(t * 1.1f);
            seam.color = c;
            // Slightly taller when stood on.
            if (seam.transform != null)
            {
                float h = near ? 0.055f : 0.035f;
                var s = seam.transform.localScale;
                seam.transform.localScale = new Vector3(s.x, h, s.z);
            }
        }

        void BreathCamera(float t)
        {
            if (cam == null)
                cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return;
            // Slow push in 3% over 20 s and back (Hades-style breathe).
            float phase = ((t - openedAt) / BreathSeconds) * Mathf.PI * 2f;
            CameraBreath01 = 0.5f + 0.5f * Mathf.Sin(phase);
            float scale = 1f - BreathScale * Mathf.Sin(phase); // push in = smaller ortho
            cam.orthographicSize = baseOrtho * scale;
        }

        void TickOverlord(float t)
        {
            if (overlordRoot == null) return;
            if (overlordStartedAt < 0f)
            {
                if (t < nextOverlordAt) return;
                overlordGoingRight = !overlordGoingRight;
                overlordStartedAt = t;
                SetOverlordVisible(true);
                // Low rising hum as he crosses (bible: silhouette silent but for a hum).
                SickOfIt.Core.Audio.Sfx.Play("magma_rumble");
                OverlordAppeared?.Invoke();
            }

            float u = Mathf.Clamp01((t - overlordStartedAt) / OverlordCrossSeconds);
            // Ease in/out so he doesn't pop; climbs slightly upward as he crosses.
            float eased = u * u * (3f - 2f * u);
            float x0 = overlordGoingRight ? -11f : 11f;
            float x1 = overlordGoingRight ? 11f : -11f;
            float y = LedgeTop + 6.8f + eased * 1.4f + Mathf.Sin(eased * Mathf.PI) * 0.15f;
            overlordRoot.localPosition = new Vector3(Mathf.Lerp(x0, x1, eased), y, 0f);
            overlordRoot.localScale = new Vector3(overlordGoingRight ? 1.15f : -1.15f, 1.15f, 1f);

            // Soft fade at the edges.
            float a = eased < 0.12f ? eased / 0.12f : (eased > 0.88f ? (1f - eased) / 0.12f : 1f);
            if (overlordParts != null)
            {
                for (int i = 0; i < overlordParts.Length; i++)
                {
                    if (overlordParts[i] == null) continue;
                    var c = overlordParts[i].color;
                    // Eyes stay a touch brighter.
                    bool eye = overlordParts[i].name.StartsWith("Eye");
                    c.a = a * (eye ? 0.95f : 0.9f);
                    overlordParts[i].color = c;
                }
            }

            if (u < 1f) return;
            OverlordPasses++;
            LastOverlordPassAt = t;
            overlordStartedAt = -1f;
            SetOverlordVisible(false);
            float gap = OverlordMinGap + (OverlordMaxGap - OverlordMinGap) * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 0.17f)));
            nextOverlordAt = t + gap;
        }

        void OnDestroy()
        {
            if (Active == this)
                Active = null;
            if (cam != null && cam.orthographic)
                cam.orthographicSize = baseOrtho;
            if (rampSprite != null) Destroy(rampSprite);
            if (whiteSprite != null) Destroy(whiteSprite);
            if (ramp != null) Destroy(ramp);
        }
    }
}
