using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core;
using SickOfIt.Core.Device;
using SickOfIt.Core.Input;
using SickOfIt.Data;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Hand-made flipbook VFX for every combat verb (Docs/Story.md "The feel of a hit", Dead Cells-style bursts).
    /// One atlas (Resources/Effects/FeelVfxAtlas) of 12 rows × 10 frames at 128 px; Gen art replaces the procedural
    /// stand-ins. Spawned from a <see cref="VfxPool"/> (prewarmed, never Instantiate during play). Presentation only.
    /// </summary>
    public static class FeelVfx
    {
        public const int FrameCount = 10;
        public const int Cell = 128;
        public const int KindCount = 12;
        public const float DiamondFlySeconds = 0.6f;

        /// <summary>One flipbook recipe in the atlas (row order is fixed; Gen sheets must match).</summary>
        public enum Kind
        {
            Muzzle = 0,
            Impact = 1,
            Hit = 2,
            DeathImp = 3,
            DeathCinder = 4,
            DeathSack = 5,
            Diamond = 6,
            HeroHurt = 7,
            HeroDeath = 8,
            Dash = 9,
            Graze = 10,
            RewindRipple = 11,
        }

        /// <summary>Raised when a flipbook starts (kind name, world position). Playtest listens.</summary>
        public static event Action<string, Vector2> Played;

        /// <summary>Diamonds that finished flying to the HUD counter this session.</summary>
        public static int DiamondsFlown { get; private set; }

        /// <summary>Live flipbooks right now (for the burst fps check).</summary>
        public static int ActiveCount => VfxPool.Active;

        /// <summary>True when the Gen atlas texture is loaded (stand-ins leave this false).</summary>
        public static bool HasBakedArt => FeelVfxAtlas.HasBakedArt;

        static bool dashedHooked;
        static GameClock rewindClock;

        /// <summary>Warm the pool and listen for dash / rewind. Safe to call every level start.</summary>
        public static void Ensure()
        {
            VfxPool.Warm();
            DiamondFlyer.Warm();
            if (!dashedHooked)
            {
                dashedHooked = true;
                PlayerController.Dashed += OnDashed;
            }
            BindRewindClock(GameClock.Instance);
        }

        /// <summary>Call when a new GameClock appears (each level).</summary>
        public static void BindRewindClock(GameClock clock)
        {
            if (rewindClock == clock)
                return;
            if (rewindClock != null)
                rewindClock.OnRewindStarted -= OnRewindStarted;
            rewindClock = clock;
            if (rewindClock != null)
                rewindClock.OnRewindStarted += OnRewindStarted;
        }

        static void OnDashed(PlayerController who)
        {
            if (who == null)
                return;
            Play(Kind.Dash, who.transform.position, size: 1.1f, tint: new Color(0.55f, 0.85f, 1f, 0.85f));
        }

        static void OnRewindStarted()
        {
            var world = GameplayWorld.Instance;
            var hero = world != null ? world.PlayerController : null;
            Vector2 at = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            Play(Kind.RewindRipple, at, size: 2.2f, tint: new Color(0.45f, 0.95f, 1f, 0.9f));
        }

        /// <summary>Play a flipbook at a world point. Phone tier draws smaller and the pool caps lower.</summary>
        public static void Play(Kind kind, Vector2 at, float size = 1f, Color? tint = null, float fps = 18f)
        {
            Ensure();
            if (GameSettings.ReducedFlash && (kind == Kind.HeroHurt || kind == Kind.HeroDeath || kind == Kind.DeathImp
                || kind == Kind.DeathCinder || kind == Kind.DeathSack))
                size *= 0.65f;
            if (TouchDevice.IsPrimary)
                size *= 0.85f;
            var frames = FeelVfxAtlas.Frames(kind);
            if (frames == null || frames.Length == 0)
                return;
            Color c = tint ?? Color.white;
            if (GameSettings.ReducedFlash)
                c.a *= 0.55f;
            if (!VfxPool.Play(frames, at, size, c, fps))
                return;
            Played?.Invoke(kind.ToString(), at);
        }

        /// <summary>Death burst for an enemy family (imp soot, cinder ash, sack-imp / Floor 997 threats).</summary>
        public static void DeathBurst(string defId, Vector2 at, float radius)
        {
            Kind kind = DeathKindOf(defId);
            float size = Mathf.Clamp(radius * 3.2f, 0.9f, 2.4f);
            Play(kind, at, size);
        }

        public static Kind DeathKindOf(string defId)
        {
            if (string.IsNullOrEmpty(defId))
                return Kind.DeathImp;
            string id = defId.ToLowerInvariant();
            if (id.Contains("cinder") || id.Contains("thatch") || id.Contains("spark"))
                return Kind.DeathCinder;
            if (id.Contains("sack") || id.Contains("impSack") || id.Contains("launcherthief") || id.Contains("thief"))
                return Kind.DeathSack;
            return Kind.DeathImp;
        }

        /// <summary>A diamond picked up: sparkle at the pickup, then an arc to the HUD gem within 0.6 s.</summary>
        public static void DiamondPickup(Vector2 worldAt)
        {
            Play(Kind.Diamond, worldAt, size: 0.85f);
            DiamondFlyer.Fly(worldAt);
        }

        /// <summary>Playtest / debug: reset counters between scenarios.</summary>
        public static void ResetCounters()
        {
            DiamondsFlown = 0;
            VfxPool.ResetSeen();
        }

        internal static void NoteDiamondFlown() => DiamondsFlown++;

        /// <summary>Kinds seen this level (playtest).</summary>
        public static IReadOnlyCollection<string> Seen => VfxPool.SeenKinds;
    }

    /// <summary>Slices or builds the 12×10 Feel VFX atlas.</summary>
    public static class FeelVfxAtlas
    {
        const string ResourcePath = "Effects/FeelVfxAtlas";
        static Sprite[][] rows;
        static bool tried;
        public static bool HasBakedArt { get; private set; }

        public static Sprite[] Frames(FeelVfx.Kind kind)
        {
            Load();
            int i = (int)kind;
            return rows != null && i >= 0 && i < rows.Length ? rows[i] : null;
        }

        static void Load()
        {
            if (tried)
                return;
            tried = true;
            rows = new Sprite[FeelVfx.KindCount][];
            var tex = Resources.Load<Texture2D>(ResourcePath);
            if (tex != null && tex.width >= FeelVfx.Cell * FeelVfx.FrameCount && tex.height >= FeelVfx.Cell * FeelVfx.KindCount)
            {
                HasBakedArt = true;
                for (int k = 0; k < FeelVfx.KindCount; k++)
                {
                    rows[k] = new Sprite[FeelVfx.FrameCount];
                    // Unity y=0 is bottom: row 0 (Muzzle) sits at the top of the sheet.
                    int rowFromBottom = FeelVfx.KindCount - 1 - k;
                    for (int f = 0; f < FeelVfx.FrameCount; f++)
                    {
                        var s = Sprite.Create(tex,
                            new Rect(f * FeelVfx.Cell, rowFromBottom * FeelVfx.Cell, FeelVfx.Cell, FeelVfx.Cell),
                            new Vector2(0.5f, 0.5f), FeelVfx.Cell);
                        s.name = $"FeelVfx_{(FeelVfx.Kind)k}_{f:00}";
                        rows[k][f] = s;
                    }
                }
                return;
            }

            HasBakedArt = false;
            for (int k = 0; k < FeelVfx.KindCount; k++)
                rows[k] = BuildStandIn((FeelVfx.Kind)k);
        }

        // ─── Procedural stand-ins (inked arcade bursts until Gen sheets land) ───

        static readonly Color32 Ink = new Color32(0x14, 0x0B, 0x0D, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        static Sprite[] BuildStandIn(FeelVfx.Kind kind)
        {
            var frames = new Sprite[FeelVfx.FrameCount];
            for (int f = 0; f < FeelVfx.FrameCount; f++)
            {
                float t = f / (float)(FeelVfx.FrameCount - 1);
                frames[f] = BakeFrame(kind, t, f);
            }
            return frames;
        }

        static Sprite BakeFrame(FeelVfx.Kind kind, float t, int f)
        {
            int n = FeelVfx.Cell;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"FeelVfxStandIn_{kind}",
            };
            var px = new Color32[n * n];
            float cx = n * 0.5f, cy = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = Sample(kind, t, (x + 0.5f - cx) / (n * 0.5f), (y + 0.5f - cy) / (n * 0.5f), f);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            s.name = $"FeelVfx_{kind}_{f:00}";
            return s;
        }

        static Color32 Sample(FeelVfx.Kind kind, float t, float nx, float ny, int f)
        {
            float r = Mathf.Sqrt(nx * nx + ny * ny);
            float a = Mathf.Atan2(ny, nx);
            switch (kind)
            {
                case FeelVfx.Kind.Muzzle:
                    return RingBurst(r, a, t, Hot(1f - t * 0.5f), 0.15f + 0.55f * t, 0.08f);
                case FeelVfx.Kind.Impact:
                    return StarBurst(r, a, t, new Color32(255, 230, 140, 255), 6);
                case FeelVfx.Kind.Hit:
                    return FlashRing(r, t, new Color32(255, 255, 255, 255), new Color32(255, 120, 60, 255));
                case FeelVfx.Kind.DeathImp:
                    return Puff(r, a, t, new Color32(40, 30, 28, 255), new Color32(255, 140, 40, 255), soot: true);
                case FeelVfx.Kind.DeathCinder:
                    return Shatter(r, a, t, new Color32(180, 160, 140, 255), new Color32(255, 200, 120, 255));
                case FeelVfx.Kind.DeathSack:
                    return Puff(r, a, t, new Color32(60, 40, 30, 255), new Color32(255, 210, 80, 255), soot: true);
                case FeelVfx.Kind.Diamond:
                    return Sparkle(r, a, t, new Color32(120, 220, 255, 255));
                case FeelVfx.Kind.HeroHurt:
                    return FlashRing(r, t, new Color32(255, 80, 80, 255), new Color32(255, 200, 180, 255));
                case FeelVfx.Kind.HeroDeath:
                    return RingBurst(r, a, t, new Color32(255, 60, 40, 255), 0.2f + 0.9f * t, 0.12f);
                case FeelVfx.Kind.Dash:
                    return Streak(nx, ny, t, new Color32(100, 200, 255, 200));
                case FeelVfx.Kind.Graze:
                    return Streak(nx, ny, t, new Color32(255, 240, 160, 220));
                case FeelVfx.Kind.RewindRipple:
                    return Ripple(r, t, new Color32(80, 230, 255, 255));
                default:
                    return Clear;
            }
        }

        static Color32 Hot(float k) => Color32.Lerp(new Color32(255, 80, 20, 255), new Color32(255, 255, 220, 255), k);

        static Color32 RingBurst(float r, float a, float t, Color32 core, float radius, float thick)
        {
            float fade = 1f - t;
            float d = Mathf.Abs(r - radius);
            if (d < thick)
            {
                byte al = (byte)Mathf.Clamp(Mathf.RoundToInt(255 * fade * (1f - d / thick)), 0, 255);
                return new Color32(core.r, core.g, core.b, al);
            }
            if (r < radius * 0.35f * (1f - t * 0.5f))
            {
                byte al = (byte)Mathf.Clamp(Mathf.RoundToInt(200 * fade), 0, 255);
                return new Color32(255, 255, 240, al);
            }
            // Embers
            float spoke = Mathf.Abs(Mathf.Sin(a * 5f + t * 6f));
            if (spoke > 0.92f && r < radius + 0.2f && r > radius * 0.4f)
                return new Color32(Ink.r, Ink.g, Ink.b, (byte)(180 * fade));
            return Clear;
        }

        static Color32 StarBurst(float r, float a, float t, Color32 col, int points)
        {
            float fade = 1f - t * 0.85f;
            float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * points * 0.5f)), 2.5f);
            float reach = (0.25f + 0.75f * t) * (0.45f + 0.55f * spike);
            if (r < reach)
            {
                byte al = (byte)Mathf.Clamp(Mathf.RoundToInt(255 * fade * (1f - r / Mathf.Max(0.01f, reach))), 0, 255);
                return new Color32(col.r, col.g, col.b, al);
            }
            return Clear;
        }

        static Color32 FlashRing(float r, float t, Color32 inner, Color32 outer)
        {
            float fade = 1f - t;
            float rad = 0.2f + 0.7f * t;
            if (r < rad * 0.4f)
                return new Color32(inner.r, inner.g, inner.b, (byte)(220 * fade));
            if (r < rad)
            {
                float k = (r - rad * 0.4f) / (rad * 0.6f);
                var c = Color32.Lerp(inner, outer, k);
                return new Color32(c.r, c.g, c.b, (byte)(200 * fade * (1f - k)));
            }
            return Clear;
        }

        static Color32 Puff(float r, float a, float t, Color32 soot, Color32 ember, bool soot)
        {
            float fade = 1f - t * 0.7f;
            float rad = 0.3f + 0.7f * t;
            float wobble = 0.08f * Mathf.Sin(a * 4f + t * 8f);
            if (r < rad + wobble)
            {
                float k = r / Mathf.Max(0.01f, rad);
                var c = k > 0.55f ? ember : soot;
                byte al = (byte)Mathf.Clamp(Mathf.RoundToInt(230 * fade * (1f - k * 0.7f)), 0, 255);
                if (soot && k > 0.75f && Mathf.Abs(Mathf.Sin(a * 7f)) > 0.85f)
                    c = ember;
                return new Color32(c.r, c.g, c.b, al);
            }
            return Clear;
        }

        static Color32 Shatter(float r, float a, float t, Color32 ash, Color32 spark)
        {
            float fade = 1f - t;
            float shard = Mathf.Abs(Mathf.Cos(a * 4f + fHash(a) * 2f));
            float reach = (0.2f + 0.9f * t) * (0.5f + 0.5f * shard);
            if (r < reach && shard > 0.35f)
            {
                var c = shard > 0.75f ? spark : ash;
                return new Color32(c.r, c.g, c.b, (byte)(210 * fade));
            }
            if (r < 0.2f * (1f - t))
                return new Color32(255, 240, 200, (byte)(180 * fade));
            return Clear;
        }

        static Color32 Sparkle(float r, float a, float t, Color32 gem)
        {
            float fade = 1f - t * 0.5f;
            float pulse = 0.15f + 0.25f * Mathf.Sin(t * Mathf.PI);
            if (r < pulse)
                return new Color32(255, 255, 255, (byte)(230 * fade));
            float arm = Mathf.Abs(Mathf.Cos(a * 2f));
            if (arm > 0.9f && r < 0.2f + 0.7f * t)
                return new Color32(gem.r, gem.g, gem.b, (byte)(200 * fade));
            if (r < 0.55f * (1f - t * 0.3f))
                return new Color32(gem.r, gem.g, gem.b, (byte)(160 * fade * (1f - r)));
            return Clear;
        }

        static Color32 Streak(float nx, float ny, float t, Color32 col)
        {
            float fade = 1f - t;
            // Horizontal smear fading back.
            float along = (nx + 1f) * 0.5f;
            float across = Mathf.Abs(ny);
            if (across < 0.18f + 0.1f * (1f - along) && along > t * 0.3f)
            {
                byte al = (byte)Mathf.Clamp(Mathf.RoundToInt(220 * fade * (1f - across / 0.28f) * along), 0, 255);
                return new Color32(col.r, col.g, col.b, al);
            }
            return Clear;
        }

        static Color32 Ripple(float r, float t, Color32 col)
        {
            float fade = 1f - t;
            float rad = 0.15f + 0.95f * t;
            float d = Mathf.Abs(r - rad);
            if (d < 0.1f)
                return new Color32(col.r, col.g, col.b, (byte)(220 * fade * (1f - d / 0.1f)));
            float rad2 = rad * 0.55f;
            d = Mathf.Abs(r - rad2);
            if (d < 0.07f)
                return new Color32(col.r, col.g, col.b, (byte)(140 * fade));
            return Clear;
        }

        static float fHash(float a) => Mathf.Abs(Mathf.Sin(a * 12.9898f) * 43758.5453f) % 1f;
    }

    /// <summary>Pooled flipbook sprites. Prewarmed; never Instantiate during play.</summary>
    public sealed class VfxPool : MonoBehaviour
    {
        const int DesktopCap = 64;
        const int PhoneCap = 32;
        static VfxPool instance;
        static readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        struct Slot
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public float Age, Fps, Size;
            public Color Tint;
            public bool Active;
            public string Kind;
        }

        Slot[] slots;
        int next;

        public static int Active
        {
            get
            {
                if (instance == null || instance.slots == null)
                    return 0;
                int n = 0;
                for (int i = 0; i < instance.slots.Length; i++)
                    if (instance.slots[i].Active)
                        n++;
                return n;
            }
        }

        public static IReadOnlyCollection<string> SeenKinds => seen;

        public static void ResetSeen() => seen.Clear();

        public static void Warm()
        {
            if (instance != null)
                return;
            if (!Application.isPlaying)
                return;
            var go = new GameObject("[FeelVfx]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<VfxPool>();
            instance.Build();
        }

        void Build()
        {
            int cap = TouchDevice.IsPrimary ? PhoneCap : DesktopCap;
            slots = new Slot[cap];
            for (int i = 0; i < cap; i++)
            {
                var child = new GameObject("Vfx");
                child.transform.SetParent(transform, false);
                var sr = child.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(sr, SpriteLayer.Player, 420);
                child.SetActive(false);
                slots[i] = new Slot { Renderer = sr };
            }
        }

        public static bool Play(Sprite[] frames, Vector2 at, float size, Color tint, float fps)
        {
            Warm();
            if (instance == null || frames == null || frames.Length == 0)
                return false;
            int cap = instance.slots.Length;
            // Prefer a free slot; otherwise steal the oldest.
            int pick = -1;
            for (int i = 0; i < cap; i++)
            {
                int idx = (instance.next + i) % cap;
                if (!instance.slots[idx].Active)
                {
                    pick = idx;
                    break;
                }
            }
            if (pick < 0)
                pick = instance.next % cap;
            instance.next = (pick + 1) % cap;

            ref var s = ref instance.slots[pick];
            s.Frames = frames;
            s.Age = 0f;
            s.Fps = Mathf.Max(1f, fps);
            s.Size = size;
            s.Tint = tint;
            s.Active = true;
            s.Kind = frames[0] != null ? frames[0].name : "vfx";
            int us = s.Kind.IndexOf('_');
            if (us > 0 && s.Kind.StartsWith("FeelVfx_", StringComparison.Ordinal))
            {
                int us2 = s.Kind.IndexOf('_', us + 1);
                s.Kind = us2 > us ? s.Kind.Substring(0, us2) : s.Kind;
            }
            seen.Add(StripFrame(s.Kind));
            s.Renderer.sprite = frames[0];
            s.Renderer.color = tint;
            s.Renderer.transform.position = new Vector3(at.x, at.y, -0.5f);
            float world = size;
            float spriteW = frames[0] != null ? Mathf.Max(0.01f, frames[0].bounds.size.x) : 1f;
            float scale = world / spriteW;
            s.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            s.Renderer.gameObject.SetActive(true);
            return true;
        }

        static string StripFrame(string name)
        {
            // FeelVfx_Muzzle_00 → Muzzle
            if (name != null && name.StartsWith("FeelVfx_", StringComparison.Ordinal))
            {
                string rest = name.Substring(8);
                int us = rest.LastIndexOf('_');
                if (us > 0 && us < rest.Length - 1 && char.IsDigit(rest[us + 1]))
                    return rest.Substring(0, us);
                return rest;
            }
            return name ?? "";
        }

        void Update()
        {
            if (slots == null)
                return;
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            for (int i = 0; i < slots.Length; i++)
            {
                ref var s = ref slots[i];
                if (!s.Active)
                    continue;
                s.Age += dt;
                float length = s.Frames.Length / s.Fps;
                if (s.Age >= length)
                {
                    s.Active = false;
                    s.Renderer.gameObject.SetActive(false);
                    continue;
                }
                int frame = Mathf.Min(s.Frames.Length - 1, Mathf.FloorToInt(s.Age * s.Fps));
                s.Renderer.sprite = s.Frames[frame];
                var c = s.Tint;
                float fade = s.Age > length * 0.7f ? 1f - (s.Age - length * 0.7f) / (length * 0.3f) : 1f;
                c.a *= fade;
                s.Renderer.color = c;
            }
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }

    /// <summary>A diamond gem that arcs from the pickup to the HUD counter within <see cref="FeelVfx.DiamondFlySeconds"/>.</summary>
    public sealed class DiamondFlyer : MonoBehaviour
    {
        static DiamondFlyer runner;
        static readonly List<Fly> live = new List<Fly>(16);
        static Sprite gemSprite;
        static Canvas canvas;

        struct Fly
        {
            public RectTransform Rect;
            public Image Image;
            public Vector3 StartScreen;
            public Vector3 EndScreen;
            public float Age;
            public bool Active;
        }

        public static void Fly(Vector2 worldAt)
        {
            Ensure();
            var cam = Camera.main;
            if (cam == null || canvas == null)
            {
                FeelVfx.NoteDiamondFlown();
                return;
            }
            Vector3 start = cam.WorldToScreenPoint(new Vector3(worldAt.x, worldAt.y, 0f));
            Vector3 end = GemScreenTarget();
            int pick = -1;
            for (int i = 0; i < live.Count; i++)
            {
                if (!live[i].Active)
                {
                    pick = i;
                    break;
                }
            }
            if (pick < 0)
            {
                // Pool full: steal the oldest (no Instantiate in play).
                pick = 0;
                float oldest = live[0].Age;
                for (int i = 1; i < live.Count; i++)
                {
                    if (live[i].Age > oldest)
                    {
                        oldest = live[i].Age;
                        pick = i;
                    }
                }
            }
            var slot = live[pick];
            slot.StartScreen = start;
            slot.EndScreen = end;
            slot.Age = 0f;
            slot.Active = true;
            if (slot.Image != null)
            {
                slot.Image.sprite = gemSprite;
                slot.Image.color = Color.white;
            }
            slot.Rect.gameObject.SetActive(true);
            slot.Rect.position = start;
            live[pick] = slot;
        }

        static Vector3 GemScreenTarget()
        {
            var world = GameplayWorld.Instance;
            var hud = world != null ? world.Hud : null;
            if (hud != null)
            {
                var r = hud.GemScreenRect;
                if (r.width > 1f)
                    return new Vector3(r.center.x, r.center.y, 0f);
            }
            // Top-right fallback.
            return new Vector3(Screen.width - 48f, Screen.height - 36f, 0f);
        }

        /// <summary>Prewarm the flyer pool (call from FeelVfx.Ensure). Never Instantiate during play.</summary>
        public static void Warm() => Ensure();

        static void Ensure()
        {
            if (runner != null)
                return;
            var go = new GameObject("[DiamondFlyer]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<DiamondFlyer>();
            gemSprite = HudInkAtlas.Gem;
            if (gemSprite == null)
            {
                // Tiny white diamond stand-in.
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var px = new Color32[64];
                for (int i = 0; i < 64; i++)
                    px[i] = new Color32(120, 220, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                gemSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            }
            var canvasGo = new GameObject("DiamondFlyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(go.transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Prewarm flyers so play never Instantiates under load.
            for (int i = 0; i < 16; i++)
            {
                var child = new GameObject("DiamondFly", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(canvas.transform, false);
                var rt = child.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(28f, 28f);
                var img = child.GetComponent<Image>();
                img.raycastTarget = false;
                img.sprite = gemSprite;
                child.SetActive(false);
                live.Add(new Fly { Rect = rt, Image = img, Active = false });
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < live.Count; i++)
            {
                var f = live[i];
                if (!f.Active)
                    continue;
                f.Age += dt;
                float u = Mathf.Clamp01(f.Age / FeelVfx.DiamondFlySeconds);
                // Arc: rise then settle into the counter.
                Vector3 flat = Vector3.Lerp(f.StartScreen, f.EndScreen, u * u);
                float lift = Mathf.Sin(u * Mathf.PI) * 80f;
                flat.y += lift;
                f.Rect.position = flat;
                float scale = Mathf.Lerp(1.15f, 0.55f, u);
                f.Rect.localScale = new Vector3(scale, scale, 1f);
                if (u >= 1f)
                {
                    f.Active = false;
                    f.Rect.gameObject.SetActive(false);
                    FeelVfx.NoteDiamondFlown();
                }
                live[i] = f;
            }
        }
    }
}
