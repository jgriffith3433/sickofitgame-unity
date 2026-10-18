using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The world below a floor with a path (Docs/LevelDesign.md: "the world falling away below"): a sky gradient, the lava
    /// sea far below that drops away slowly as the hero climbs, parallax layers of spires, smoke and floating islands, a heat
    /// shimmer over the sea and embers drifting up. Drawn from the Circle's <see cref="SkyDef"/>. Presentation only: it
    /// reads the camera and never touches the simulation, and its drift and embers run on real time.
    /// </summary>
    public sealed class FloorBackdrop : MonoBehaviour
    {
        const int SkyOrder = -320, SeaOrder = -300, LayerOrder = -290, EmberOrder = -200;

        GameplayWorld world;
        SkyDef sky;
        Vector2 origin;

        SpriteRenderer skyFill;
        SpriteRenderer seaFill;
        SpriteRenderer seaGlow;
        SpriteRenderer seaLine;
        bool seaPainted;
        readonly List<SpriteRenderer> haze = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> embers = new List<SpriteRenderer>();
        readonly Dictionary<long, SpriteRenderer> items = new Dictionary<long, SpriteRenderer>();
        readonly HashSet<long> wanted = new HashSet<long>();
        readonly List<long> scratch = new List<long>();
        readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        /// <summary>Where the far sea's surface is on screen now (world y), for the playtest.</summary>
        public float SeaTop { get; private set; }
        /// <summary>The far sea is painted art (the Circle's seaArt), not the gradient.</summary>
        public bool SeaPainted => seaPainted;
        /// <summary>Painted sky layers and bands on screen now (sprites with art, for the playtest).</summary>
        public int PaintedCount
        {
            get
            {
                int n = 0;
                foreach (var pair in items)
                    if (pair.Value != null && sky.layers[(int)(pair.Key >> 40)].sprite != null)
                        n++;
                return n;
            }
        }
        /// <summary>Parallax shapes on screen now.</summary>
        public int ItemCount => items.Count;
        public int LayerCount => sky != null && sky.layers != null ? sky.layers.Count : 0;

        /// <summary>World position of the first visible shape of a layer and its layer index (the playtest measures parallax with it).</summary>
        public bool TryGetItem(int layer, out Vector2 position, out long key)
        {
            foreach (var pair in items)
            {
                if ((int)(pair.Key >> 40) == layer && pair.Value != null)
                {
                    position = pair.Value.transform.position;
                    key = pair.Key;
                    return true;
                }
            }
            position = default;
            key = 0;
            return false;
        }

        public bool TryGetItem(long key, out Vector2 position)
        {
            if (items.TryGetValue(key, out var sr) && sr != null)
            {
                position = sr.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        public static FloorBackdrop Create(GameplayWorld world, SkyDef sky, Vector2 origin)
        {
            var go = new GameObject("FloorBackdrop");
            go.transform.SetParent(world.transform, false);
            var backdrop = go.AddComponent<FloorBackdrop>();
            backdrop.world = world;
            backdrop.sky = sky;
            backdrop.origin = origin;
            backdrop.Build();
            return backdrop;
        }

        void Build()
        {
            skyFill = Quad("Sky", Gradient(sky.top, sky.glow), SkyOrder);
            seaFill = Quad("FarSea", Gradient(sky.sea, Color.Lerp(sky.sea, Color.black, 0.55f)), SeaOrder);
            seaGlow = Quad("FarSeaGlow", Gradient(new Color(sky.sea.r, sky.sea.g, sky.sea.b, 0f), new Color(sky.sea.r, sky.sea.g, sky.sea.b, 0.55f)), SeaOrder - 1);
            seaLine = Quad("FarSeaLine", EntityFactory.WhiteSquare, SeaOrder + 1);
            seaLine.color = Color.Lerp(sky.sea, Color.white, 0.45f);
            for (int i = 0; i < 3; i++)
            {
                var h = Quad("Shimmer" + i, EntityFactory.WhiteSquare, SeaOrder + 2);
                h.color = new Color(1f, 0.6f, 0.3f, 0.08f - i * 0.02f);
                haze.Add(h);
            }
            for (int i = 0; i < Mathf.Max(0, sky.embers); i++)
            {
                var e = Quad("Ember" + i, EntityFactory.WhiteSquare, EmberOrder);
                embers.Add(e);
            }
            LateUpdate();
        }

        SpriteRenderer Quad(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            SpriteLayers.Apply(sr, SpriteLayer.Background2, order);
            return sr;
        }

        /// <summary>A one-unit square fading from 'top' to 'bottom' (vertical gradient).</summary>
        static Sprite Gradient(Color top, Color bottom)
        {
            const int h = 64;
            var tex = new Texture2D(1, h) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < h; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.DontSave;
            // 1 pixel wide at 1 pixel per unit: one unit wide, 'h' tall; Place() scales it.
            return sprite;
        }

        static void Place(SpriteRenderer sr, Vector2 center, Vector2 size)
        {
            Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            sr.transform.position = new Vector3(center.x, center.y, sr.transform.position.z);
            sr.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, native.x), size.y / Mathf.Max(0.0001f, native.y), 1f);
        }

        void LateUpdate()
        {
            if (world == null || sky == null || world.Camera == null)
                return;
            Vector2 cam = world.CameraCenter;
            float h = world.Camera.orthographicSize;
            float w = h * world.Camera.aspect;
            float t = Time.time;

            Place(skyFill, cam, new Vector2(w * 2f + 2f, h * 2f + 2f));

            // The far sea drops away as he climbs (seaParallax), but its horizon never quite leaves the bottom of the screen.
            float seaTop = origin.y + sky.seaLevel + (cam.y - origin.y) * Mathf.Clamp01(sky.seaParallax);
            seaTop = Mathf.Max(seaTop, cam.y - h + Mathf.Clamp01(sky.seaMinOnScreen) * 2f * h);
            SeaTop = seaTop;
            float below = Mathf.Max(0.5f, seaTop - (cam.y - h) + 1f);
            Place(seaFill, new Vector2(cam.x, seaTop - below * 0.5f), new Vector2(w * 2f + 2f, below));
            Place(seaGlow, new Vector2(cam.x, seaTop + 1.4f), new Vector2(w * 2f + 2f, 2.8f));
            Place(seaLine, new Vector2(cam.x, seaTop), new Vector2(w * 2f + 2f, 0.05f + 0.02f * Mathf.Sin(t * 2.3f)));
            for (int i = 0; i < haze.Count; i++)
            {
                float wobble = Mathf.Sin(t * (1.7f + i * 0.6f) + i) * 0.3f * sky.shimmer;
                Place(haze[i], new Vector2(cam.x + wobble, seaTop + 0.3f + i * 0.4f), new Vector2(w * 2f + 2f, 0.3f + 0.1f * Mathf.Sin(t * 2.9f + i) * sky.shimmer));
            }

            PaintedSea(cam, w, h, seaTop, below, t);
            Bands(cam, w, h, seaTop, t);
            Layers(cam, w, h, seaTop, t);
            Embers(cam, w, h, t);
        }

        const int MaxSeaRows = 6;

        /// <summary>The painted far sea: rows of its loop from the horizon to the bottom of the screen (the gradient stays under it).</summary>
        void PaintedSea(Vector2 cam, float w, float h, float seaTop, float below, float t)
        {
            var art = sky.seaArt;
            var frames = FloorArt.Frames(art, world);
            float row = Mathf.Max(0.5f, sky.seaArtHeight);
            int rows = frames.Length > 0 ? Mathf.Min(MaxSeaRows, Mathf.CeilToInt(below / row)) : 0;
            seaPainted = rows > 0;
            // The painted sea replaces the gradient fill (its glow and horizon line stay over it).
            seaFill.enabled = !seaPainted;
            for (int i = 0; i < MaxSeaRows; i++)
            {
                if (i >= rows)
                {
                    FloorArt.Hide(transform, "SeaRow" + i);
                    continue;
                }
                float y = seaTop - row * (i + 0.5f);
                Across(art, frames, "SeaRow" + i, cam, w, y, row, sky.seaParallax, t, SeaOrder);
            }
        }

        void Bands(Vector2 cam, float w, float h, float seaTop, float t)
        {
            var bands = sky.bands;
            for (int i = 0; bands != null && i < bands.Count; i++)
            {
                var band = bands[i];
                if (band == null)
                    continue;
                float y = band.anchor == SkyBandAnchor.Top ? cam.y + h - band.offset : seaTop + band.offset;
                Across(band.art, FloorArt.Frames(band.art, world), "Band" + i, cam, w, y, band.height, band.parallax, t, LayerOrder + band.order);
            }
        }

        /// <summary>A seamless strip across the whole screen at height y, moving across by 'parallax' as the camera travels.</summary>
        void Across(ArtLoopDef art, Sprite[] frames, string name, Vector2 cam, float w, float y, float height, float parallax, float t, int order)
        {
            var frame = FloorArt.Frame(frames, art != null ? art.fps : 0f, t);
            if (frame == null || height <= 0f)
            {
                FloorArt.Hide(transform, name);
                return;
            }
            Vector2 native = frame.bounds.size;
            float tile = height * native.x / Mathf.Max(0.0001f, native.y);
            // Its tiles sit at origin + (camera travelled) * parallax, repeated every tile.
            float anchorX = cam.x - (cam.x - origin.x) * (1f - Mathf.Clamp01(parallax));
            float edge = cam.x - w - tile;
            float left = edge - Mathf.Repeat(edge - anchorX, tile);
            FloorArt.Strip(transform, name, art, frames, new Vector2(left, y), new Vector2(cam.x + w + tile, y), height, t, SpriteLayer.Background2, order, false);
        }

        void Layers(Vector2 cam, float w, float h, float seaTop, float t)
        {
            scratch.Clear();
            scratch.AddRange(items.Keys);
            wanted.Clear();
            var layers = sky.layers;
            for (int l = 0; layers != null && l < layers.Count; l++)
            {
                var layer = layers[l];
                if (layer == null)
                    continue;
                Vector2 spacing = new Vector2(Mathf.Max(0.5f, layer.spacing.x), Mathf.Max(0.5f, layer.spacing.y));
                Vector2 p = new Vector2(Mathf.Clamp01(layer.parallax.x), Mathf.Clamp01(layer.parallax.y));
                Vector2 travelled = cam - origin;
                // A shape at layer position q is drawn at origin + q + travelled * parallax (+ drift).
                float drift = layer.drift * t;
                Vector2 centre = travelled - Vector2.Scale(travelled, p) - new Vector2(drift, 0f);
                float reach = Mathf.Max(layer.size.x, layer.size.y) * (1f + layer.sizeVariation) + 1f;
                int x0 = Mathf.FloorToInt((centre.x - w - reach) / spacing.x), x1 = Mathf.FloorToInt((centre.x + w + reach) / spacing.x);
                int y0 = 0, y1 = 0;
                if (!layer.onSea)
                {
                    y0 = Mathf.FloorToInt((centre.y - h - reach) / spacing.y);
                    y1 = Mathf.FloorToInt((centre.y + h + reach) / spacing.y);
                }
                for (int cx = x0; cx <= x1; cx++)
                {
                    for (int cy = y0; cy <= y1; cy++)
                    {
                        var random = DeterministicRandom.Derive(world.Seed ^ 0x5C1Eu, (uint)(l * 7919 + cx * 131 + cy * 524287));
                        if (random.Value() >= layer.chance)
                            continue;
                        float qx = (cx + 0.5f + random.Range(-0.4f, 0.4f)) * spacing.x;
                        float qy = (cy + 0.5f + random.Range(-0.4f, 0.4f)) * spacing.y;
                        float x = origin.x + qx + travelled.x * p.x + drift;
                        float y = layer.onSea ? seaTop - 0.1f : origin.y + qy + travelled.y * p.y;
                        float sizeW = layer.size.x * (1f + random.Range(-layer.sizeVariation, layer.sizeVariation));
                        float sizeH = layer.size.y * (1f + random.Range(-layer.sizeVariation, layer.sizeVariation));
                        // Off screen, or under the far sea (only spires stand in it).
                        if (Mathf.Abs(x - cam.x) > w + reach || (!layer.onSea && (Mathf.Abs(y - cam.y) > h + reach || y < seaTop + 0.5f)))
                            continue;
                        long key = ((long)l << 40) | ((long)(cx & 0xFFFFF) << 20) | (long)(cy & 0xFFFFF);
                        wanted.Add(key);
                        if (!items.TryGetValue(key, out var sr))
                        {
                            sr = Take(layer);
                            items[key] = sr;
                        }
                        bool flip = random.Value() < 0.5f;
                        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
                        sr.transform.localScale = new Vector3(sizeW / Mathf.Max(0.01f, native.x) * (flip ? -1f : 1f),
                            sizeH / Mathf.Max(0.01f, native.y) * (layer.hanging ? -1f : 1f), 1f);
                        sr.transform.position = new Vector3(x, y, sr.transform.position.z);
                    }
                }
            }
            foreach (var key in scratch)
            {
                if (wanted.Contains(key))
                    continue;
                var sr = items[key];
                items.Remove(key);
                if (sr != null)
                {
                    sr.gameObject.SetActive(false);
                    spare.Push(sr);
                }
            }
        }

        SpriteRenderer Take(SkyLayerDef layer)
        {
            SpriteRenderer sr = null;
            while (sr == null && spare.Count > 0)
                sr = spare.Pop();
            if (sr == null)
                sr = Quad("SkyShape", null, LayerOrder);
            sr.gameObject.SetActive(true);
            sr.gameObject.name = string.IsNullOrEmpty(layer.name) ? "SkyShape" : layer.name;
            sr.sprite = layer.sprite != null ? layer.sprite : StageTrack.PropShapes.Get(layer.shape);
            sr.color = layer.color;
            SpriteLayers.Apply(sr, SpriteLayer.Background2, LayerOrder + layer.order);
            return sr;
        }

        void Embers(Vector2 cam, float w, float h, float t)
        {
            for (int i = 0; i < embers.Count; i++)
            {
                // Each ember has its own lane, speed and sway, from its index (cosmetic, real time).
                float a = Hash(i * 3 + 1), b = Hash(i * 3 + 2), c = Hash(i * 3 + 3);
                float rise = Mathf.Repeat(t * (0.05f + 0.07f * b) + c, 1f);
                float x = cam.x + (a * 2f - 1f) * (w + 0.5f) + Mathf.Sin(t * (0.6f + b) + i) * 0.35f;
                float y = cam.y - h + rise * (h * 2f + 0.5f);
                var sr = embers[i];
                var col = sky.emberColor;
                col.a *= Mathf.Clamp01(rise * 4f) * Mathf.Clamp01((1f - rise) * 3f) * (0.6f + 0.4f * Mathf.Sin(t * 7f + i));
                sr.color = col;
                Place(sr, new Vector2(x, y), Vector2.one * (0.04f + 0.05f * c));
            }
        }

        static float Hash(int n)
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }
}
