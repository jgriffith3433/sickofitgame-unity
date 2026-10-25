using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Fx
{
    /// <summary>
    /// The glow of fire, lava and explosions (game.json feel.glow): soft additive sprites, one shared material (Resources/Feel's
    /// SickOfIt/SpriteGlow) and one generated sprite per shape, so they batch; at most feel.glow.maxGlows drawn at once. A
    /// follower rides a transform for as long as its object is active (a rewind switching it back on brings its glow back); a
    /// burst flares and fades where something blew up. Presentation only, cheaper than lights on WebGL: no lighting pass, no
    /// render texture.
    /// </summary>
    public static class Glow
    {
        /// <summary>Glows on the screen now, bursts flared and followers registered since the scene opened (for the playtest).</summary>
        public static int Drawn => GlowRunner.Current != null ? GlowRunner.Current.DrawnNow : 0;
        public static int Bursts => GlowRunner.Current != null ? GlowRunner.Current.BurstCount : 0;
        public static int Followers => GlowRunner.Current != null ? GlowRunner.Current.FollowerCount : 0;
        /// <summary>The kinds of glow drawn since the scene opened ("lava", "fire", "explosion", ...).</summary>
        public static IEnumerable<string> KindsSeen => GlowRunner.Current != null ? GlowRunner.Current.Seen : System.Array.Empty<string>();

        static GlowFeel Config
        {
            get
            {
                var feel = Feel.Config;
                return feel.enabled && feel.glow != null && feel.glow.enabled ? feel.glow : null;
            }
        }

        /// <summary>
        /// Makes 'target' glow while it is active: a halo (Round) or a band along its top (Top) of 'size' world units, centred
        /// 'offset' from it, drawn in 'layer' at 'order'. Calling it again for the same target updates the glow.
        /// </summary>
        public static void Follow(Transform target, string kind, Vector2 offset, Vector2 size, Color color, float alpha, float flicker,
            float flickerSpeed, GlowShape shape, SpriteLayer layer, int order)
        {
            var cfg = Config;
            if (target == null || cfg == null || !Application.isPlaying)
                return;
            GlowRunner.Get()?.Follow(target, kind, offset, size, color, alpha * cfg.intensity, flicker, flickerSpeed, shape, layer, order);
        }

        /// <summary>A flare that grows a little and fades over 'seconds' (an explosion, a death pop, a muzzle flash).</summary>
        public static void Burst(Vector2 at, string kind, Color color, float size, float alpha, float seconds)
        {
            var cfg = Config;
            if (cfg == null || size <= 0f || seconds <= 0f || alpha <= 0f || !Application.isPlaying)
                return;
            GlowRunner.Get()?.Burst(at, kind, color, size, alpha * cfg.intensity, seconds);
        }
    }

    /// <summary>Owns the glow sprites for the current scene.</summary>
    public sealed class GlowRunner : MonoBehaviour
    {
        sealed class Follower
        {
            public Transform Target;
            public string Kind;
            public Vector2 Offset, Size;
            public Color Color;
            public float Alpha, Flicker, FlickerSpeed, Phase;
            public GlowShape Shape;
            public SpriteLayer Layer;
            public int Order;
        }

        struct Flare
        {
            public Vector2 At;
            public string Kind;
            public Color Color;
            public float Size, Alpha, Seconds, Age;
        }

        struct Slot
        {
            public SpriteRenderer Renderer;
            public SpriteLayer Layer;
            public int Order;
            public bool Shown;
            public bool Band;
        }

        public static GlowRunner Current { get; private set; }

        readonly List<Follower> followers = new List<Follower>();
        readonly Dictionary<Transform, Follower> byTarget = new Dictionary<Transform, Follower>();
        readonly List<Flare> flares = new List<Flare>();
        readonly HashSet<string> seen = new HashSet<string>();
        Slot[] slots = new Slot[0];
        Material material;
        Sprite round, band;
        Camera view;

        public int DrawnNow { get; private set; }
        public int BurstCount { get; private set; }
        public int FollowerCount => followers.Count;
        public IEnumerable<string> Seen => seen;

        public static GlowRunner Get()
        {
            if (Current != null)
                return Current;
            if (!Application.isPlaying)
                return null;
            Current = new GameObject("[Glow]").AddComponent<GlowRunner>();
            return Current;
        }

        void Awake()
        {
            var shader = Resources.Load<Shader>("Feel/SpriteGlow");
            if (shader != null && shader.isSupported)
                material = new Material(shader) { name = "SpriteGlow (runtime)", hideFlags = HideFlags.DontSave };
            else
                Debug.LogWarning("[Glow] Resources/Feel/SpriteGlow shader missing or unsupported: glows are drawn alpha-blended.");
            round = MakeSprite(false);
            band = MakeSprite(true);
        }

        /// <summary>A soft white disc (round) or a soft horizontal band (band), 1 x 1 world unit.</summary>
        static Sprite MakeSprite(bool isBand)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
                name = isBand ? "GlowBand" : "GlowRound",
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float a;
                    if (isBand)
                    {
                        // Brightest along the middle row, fading up and down; the ends fade in over the outer eighth.
                        float across = Mathf.Clamp01(1f - Mathf.Abs(v));
                        float ends = Mathf.Clamp01((1f - Mathf.Abs(u)) * 8f);
                        a = across * across * ends;
                    }
                    else
                    {
                        float d = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                        a = d * d * (3f - 2f * d) * d;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        public void Follow(Transform target, string kind, Vector2 offset, Vector2 size, Color color, float alpha, float flicker, float flickerSpeed,
            GlowShape shape, SpriteLayer layer, int order)
        {
            if (!byTarget.TryGetValue(target, out var f))
            {
                f = new Follower { Target = target, Phase = (target.GetInstanceID() & 1023) * 0.61f };
                byTarget[target] = f;
                followers.Add(f);
            }
            f.Kind = kind ?? "";
            f.Offset = offset;
            f.Size = size;
            f.Color = color;
            f.Alpha = alpha;
            f.Flicker = Mathf.Clamp01(flicker);
            f.FlickerSpeed = flickerSpeed;
            f.Shape = shape;
            f.Layer = layer;
            f.Order = order;
        }

        public void Burst(Vector2 at, string kind, Color color, float size, float alpha, float seconds)
        {
            // The oldest flare gives way when there are too many.
            if (flares.Count >= 32)
                flares.RemoveAt(0);
            flares.Add(new Flare { At = at, Kind = kind ?? "", Color = color, Size = size, Alpha = alpha, Seconds = seconds });
            BurstCount++;
        }

        void LateUpdate()
        {
            var feel = Feel.Config;
            var cfg = feel.enabled && feel.glow != null && feel.glow.enabled ? feel.glow : null;
            int max = cfg != null && Feel.Drawn ? Mathf.Clamp(cfg.maxGlows, 0, 256) : 0;
            if (slots.Length < max)
                System.Array.Resize(ref slots, max);

            float dt = Time.deltaTime;
            int n = 0;

            // Flares first: they are short and they are the bang.
            for (int i = flares.Count - 1; i >= 0; i--)
            {
                var flare = flares[i];
                flare.Age += dt;
                if (flare.Age >= flare.Seconds)
                {
                    flares.RemoveAt(i);
                    continue;
                }
                flares[i] = flare;
                if (n >= max)
                    continue;
                float t = flare.Age / flare.Seconds;
                float s = flare.Size * Mathf.Lerp(0.6f, 1.1f, 1f - (1f - t) * (1f - t));
                float a = flare.Alpha * (1f - t) * (1f - t);
                Show(ref n, flare.At, new Vector2(s, s), flare.Color, a, false, SpriteLayer.Player, SpriteLayers.Projectile + 1);
                seen.Add(flare.Kind);
            }

            // Followers on (or near) the screen.
            if (view == null || !view.isActiveAndEnabled)
                view = Camera.main;
            Vector2 center = view != null ? (Vector2)view.transform.position : Vector2.zero;
            float halfH = view != null && view.orthographic ? view.orthographicSize : 50f;
            float halfW = halfH * (view != null ? view.aspect : 1f);
            float time = Time.time;
            for (int i = followers.Count - 1; i >= 0; i--)
            {
                var f = followers[i];
                if (f.Target == null)
                {
                    followers.RemoveAt(i);
                    RemoveDeadTargets();
                    continue;
                }
                if (n >= max || !f.Target.gameObject.activeInHierarchy)
                    continue;
                Vector2 at = (Vector2)f.Target.position + f.Offset;
                if (Mathf.Abs(at.x - center.x) > halfW + f.Size.x * 0.5f || Mathf.Abs(at.y - center.y) > halfH + f.Size.y * 0.5f)
                    continue;
                float a = f.Alpha;
                if (f.Flicker > 0f)
                {
                    // Two waves out of step, so fire doesn't pulse like a metronome.
                    float w = 0.5f + 0.25f * Mathf.Sin(time * f.FlickerSpeed + f.Phase) + 0.25f * Mathf.Sin(time * f.FlickerSpeed * 2.3f + f.Phase * 1.7f);
                    a *= 1f - f.Flicker * w;
                }
                Show(ref n, at, f.Size, f.Color, a, f.Shape == GlowShape.Top, f.Layer, f.Order);
                seen.Add(f.Kind);
            }

            for (int i = n; i < slots.Length; i++)
            {
                if (slots[i].Shown)
                {
                    slots[i].Shown = false;
                    slots[i].Renderer.enabled = false;
                }
            }
            DrawnNow = n;
        }

        void Show(ref int n, Vector2 at, Vector2 size, Color color, float alpha, bool isBand, SpriteLayer layer, int order)
        {
            ref var slot = ref slots[n++];
            if (slot.Renderer == null)
            {
                var go = new GameObject("Glow");
                go.transform.SetParent(transform, false);
                slot.Renderer = go.AddComponent<SpriteRenderer>();
                if (material != null)
                    slot.Renderer.sharedMaterial = material;
                slot.Renderer.sprite = round;
                SpriteLayers.Apply(slot.Renderer, layer, order);
                slot.Layer = layer;
                slot.Order = order;
                slot.Band = false;
            }
            var r = slot.Renderer;
            if (slot.Layer != layer || slot.Order != order)
            {
                SpriteLayers.Apply(r, layer, order);
                slot.Layer = layer;
                slot.Order = order;
            }
            if (slot.Band != isBand)
            {
                r.sprite = isBand ? band : round;
                slot.Band = isBand;
            }
            if (!slot.Shown)
            {
                slot.Shown = true;
                r.enabled = true;
            }
            var t = r.transform;
            t.position = new Vector3(at.x, at.y, SpriteLayers.Depth(layer) - 0.05f);
            t.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
            color.a = Mathf.Clamp01(alpha);
            r.color = color;
        }

        readonly List<Transform> deadKeys = new List<Transform>();

        void RemoveDeadTargets()
        {
            deadKeys.Clear();
            foreach (var key in byTarget.Keys)
            {
                if (key == null)
                    deadKeys.Add(key);
            }
            foreach (var key in deadKeys)
                byTarget.Remove(key);
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (material != null)
                Destroy(material);
            if (round != null)
                Destroy(round.texture);
            if (band != null)
                Destroy(band.texture);
        }
    }
}
