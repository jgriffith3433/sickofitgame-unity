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

        Texture2D ramp, white, basaltTex, lavaTex;
        Sprite rampSprite, whiteSprite, basaltSprite, lavaSprite;
        Transform[] embers;
        SpriteRenderer[] emberRenderers;
        SpriteRenderer seam, surface, glow, seamSpark;
        Transform overlordRoot;
        SpriteRenderer[] overlordParts;
        Camera cam;
        float baseOrtho = 3.6f;
        float nextOverlordAt;
        float overlordStartedAt = -1f;
        bool overlordGoingRight = true;
        float openedAt;
        float seamFlickerUntil = -1f;
        float lastSeamFlickerAt = -99f;
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

        /// <summary>True once the basalt ledge + lava strip textures were painted (playtest / fun-reviewer).</summary>
        public bool PaintedDressing { get; private set; }

        /// <summary>How many times the cyan seam sparked under the hero (playtest).</summary>
        public int SeamFlickers { get; private set; }

        /// <summary>World X the Overlord is climbing toward (&gt;0 right); used so the hero faces him.</summary>
        public float OverlordFaceDir => overlordGoingRight ? 1f : -1f;

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
            basaltSprite = PaintBasalt();
            lavaSprite = PaintLava();
            PaintedDressing = basaltSprite != null && lavaSprite != null;

            const float W = 60f; // wide enough for any aspect the portrait cover can ask for
            Quad("Sky", rampSprite, Color.white, new Vector2(0f, 2f), new Vector2(W, 40f), -30, true);
            // Far cliff teeth so the ledge reads as a place, not a flat band (fun-reviewer: painted dressing).
            BuildFarCliffs(W);
            glow = Quad("Glow", whiteSprite, new Color(Lava.r, Lava.g, Lava.b, 0.18f), new Vector2(0f, LedgeTop + 1.2f), new Vector2(W * 0.55f, 6f), -25, false);
            Quad("FarSea", lavaSprite != null ? lavaSprite : whiteSprite, Color.white, new Vector2(0f, LedgeTop - 0.15f), new Vector2(W, 2.8f), -20, false);
            surface = Quad("SeaLine", whiteSprite, Hot, new Vector2(0f, LedgeTop + 0.04f), new Vector2(W, 0.06f), -19, false);
            Quad("Ledge", basaltSprite != null ? basaltSprite : whiteSprite, Color.white, new Vector2(0f, LedgeTop - 1.4f), new Vector2(W, 2.8f), -10, false);
            // Darker basalt lip under the walkable top so feet read against rock, not lava.
            Quad("LedgeLip", whiteSprite, new Color(0.06f, 0.03f, 0.05f, 1f), new Vector2(0f, LedgeTop - 0.18f), new Vector2(W, 0.22f), -9, false);
            seam = Quad("RewindSeam", whiteSprite, Cyan, new Vector2(0f, LedgeTop - 0.02f), new Vector2(W, 0.035f), -8, false);
            seamSpark = Quad("SeamSpark", whiteSprite, new Color(Cyan.r, Cyan.g, Cyan.b, 0f), new Vector2(0f, LedgeTop + 0.12f), new Vector2(0.35f, 0.35f), -7, false);

            embers = new Transform[EmberCount];
            emberRenderers = new SpriteRenderer[EmberCount];
            for (int i = 0; i < EmberCount; i++)
            {
                bool cyan = i % 9 == 0;
                float sz = 0.05f + 0.03f * (i % 3);
                var r = Quad("Ember" + i, whiteSprite, cyan ? Cyan : Lava, Vector2.zero, new Vector2(sz, sz), -6, false);
                embers[i] = r.transform;
                emberRenderers[i] = r;
            }

            BuildOverlordSilhouette();
            // First glimpse soon enough that a 25 s sit always sees one, then every 15–20 s.
            nextOverlordAt = openedAt + 4.5f;
            Tick(0f);
        }

        Sprite PaintBasalt()
        {
            const int W = 128, H = 48;
            basaltTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "MenuBasalt",
            };
            var ink = new Color(0.05f, 0.02f, 0.04f, 1f);
            var mid = new Color(0.12f, 0.06f, 0.08f, 1f);
            var rim = new Color(0.22f, 0.1f, 0.09f, 1f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float ny = y / (float)(H - 1);
                float grain = Mathf.PerlinNoise(x * 0.11f, y * 0.17f);
                float crack = Mathf.PerlinNoise(x * 0.35f + 9f, y * 0.08f);
                var c = Color.Lerp(ink, mid, 0.35f + 0.5f * grain);
                if (ny > 0.82f)
                    c = Color.Lerp(c, rim, (ny - 0.82f) / 0.18f * 0.7f);
                if (crack > 0.72f)
                    c = Color.Lerp(c, ink, 0.55f);
                // Ember wash near the lava edge (bottom of the strip).
                if (ny < 0.18f)
                    c = Color.Lerp(c, Ember, 0.25f * (1f - ny / 0.18f));
                basaltTex.SetPixel(x, y, c);
            }
            basaltTex.Apply(false, true);
            return Sprite.Create(basaltTex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), H);
        }

        Sprite PaintLava()
        {
            const int W = 128, H = 32;
            lavaTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "MenuLava",
            };
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float ny = y / (float)(H - 1);
                float swirl = Mathf.PerlinNoise(x * 0.07f, y * 0.14f + 3f);
                var c = Color.Lerp(Ember, Lava, 0.35f + 0.55f * swirl);
                if (ny > 0.7f)
                    c = Color.Lerp(c, Hot, (ny - 0.7f) / 0.3f * 0.65f);
                if (ny < 0.2f)
                    c = Color.Lerp(c, Ash, 0.35f * (1f - ny / 0.2f));
                c.a = 0.92f;
                lavaTex.SetPixel(x, y, c);
            }
            lavaTex.Apply(false, true);
            return Sprite.Create(lavaTex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), H);
        }

        void BuildFarCliffs(float _)
        {
            // Jagged dark silhouettes above the far lava — landmark depth without loading climb sheets.
            float[] xs = { -18f, -11f, -4f, 5f, 12f, 19f };
            float[] hs = { 3.2f, 4.1f, 2.6f, 3.8f, 2.9f, 3.5f };
            for (int i = 0; i < xs.Length; i++)
            {
                float h = hs[i];
                Quad("Cliff" + i, whiteSprite, new Color(0.02f, 0.01f, 0.02f, 0.85f),
                    new Vector2(xs[i], LedgeTop + 1.1f + h * 0.5f), new Vector2(2.2f + (i % 3) * 0.4f, h), -28, false);
                // Horn tip
                Quad("CliffTip" + i, whiteSprite, new Color(0.015f, 0.008f, 0.02f, 0.9f),
                    new Vector2(xs[i] + 0.3f, LedgeTop + 1.1f + h + 0.35f), new Vector2(0.45f, 0.7f), -27, false);
            }
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

        /// <summary>Diegetic ledge verb: stand on the cyan rewind seam and it brightens + sparks a whisper-flicker.</summary>
        void TickSeamWithHero(float t)
        {
            if (seam == null) return;
            float heroX = 0f;
            bool near = false;
            if (MainMenuManager.ActiveHeroX(out heroX))
                near = Mathf.Abs(heroX) < 6f;
            var c = Cyan;
            float pulse = 0.55f + 0.25f * Mathf.Sin(t * 2.4f);
            bool flickering = t < seamFlickerUntil;
            if (near && !flickering && t - lastSeamFlickerAt > 4f)
            {
                // Stand near the seam: a tiny cyan rewind whisper (diegetic verb beyond walk/click).
                lastSeamFlickerAt = t;
                seamFlickerUntil = t + 0.45f;
                SeamFlickers++;
                if (seamSpark != null)
                    seamSpark.transform.localPosition = new Vector3(heroX, LedgeTop + 0.12f, 0f);
            }
            c.a = flickering ? 1f : (near ? Mathf.Lerp(0.85f, 1f, pulse) : 0.55f + 0.2f * Mathf.Sin(t * 1.1f));
            seam.color = c;
            if (seam.transform != null)
            {
                float h = flickering ? 0.08f : (near ? 0.055f : 0.035f);
                var s = seam.transform.localScale;
                seam.transform.localScale = new Vector3(s.x, h, s.z);
            }
            if (seamSpark != null)
            {
                float u = flickering ? 1f - (seamFlickerUntil - t) / 0.45f : 1f;
                var sc = Cyan;
                sc.a = flickering ? Mathf.Sin(u * Mathf.PI) * 0.95f : 0f;
                seamSpark.color = sc;
                if (flickering)
                {
                    float s = 0.25f + 0.35f * Mathf.Sin(u * Mathf.PI);
                    seamSpark.transform.localScale = new Vector3(s, s, 1f);
                }
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
                // Dedicated low hum for the silhouette (fun-reviewer: not a reused magma rumble).
                if (SickOfIt.Core.Audio.Sfx.ClipOf("menu_overlord_hum") != null)
                    SickOfIt.Core.Audio.Sfx.Play("menu_overlord_hum");
                else
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
            if (basaltSprite != null) Destroy(basaltSprite);
            if (lavaSprite != null) Destroy(lavaSprite);
            if (ramp != null) Destroy(ramp);
            if (basaltTex != null) Destroy(basaltTex);
            if (lavaTex != null) Destroy(lavaTex);
        }
    }
}
