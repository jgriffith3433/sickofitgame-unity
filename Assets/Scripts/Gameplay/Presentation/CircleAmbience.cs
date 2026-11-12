using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A Circle's air (Docs/Story.md "Hell's air"): depth layers at their own parallax (through the <see cref="ParallaxController"/>), weather
    /// (ash, sparks, cinders, runes, grit, pollen, debris, steam, dust, soul), far-off life, and timed ambient events (geysers, steam vents,
    /// birds scattering), all from the Circle's or level's <see cref="AmbienceDef"/>. Presentation only: it reads the camera and the floor
    /// counter and never touches the simulation, so it needs no rewind. Everything is pooled at build time: a fixed set of sprite renderers,
    /// moved in one LateUpdate, nothing allocated while it runs. Living packs set <see cref="AmbienceDef.liveWeather"/> so players see
    /// particles on the Meadow and Floors 1000-997; Circle defaults keep quiet air. Phone tier: half the bits, no heat shimmer.
    /// The frame-time budget: at most <see cref="MaxBits"/> weather bits and <see cref="MaxLife"/> actors; the <c>ambience</c> and
    /// <c>living-air</c> playtests measure them.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class CircleAmbience : MonoBehaviour
    {
        /// <summary>Most weather bits alive over all a Circle's weather.</summary>
        public const int MaxBits = 150;
        /// <summary>Most background actors at once.</summary>
        public const int MaxLife = 8;
        /// <summary>Most timed ambient events running at once.</summary>
        public const int MaxEvents = 6;
        const int DepthBudget = 90;
        const int WeatherOrder = -30, LifeOrder = -40, EventOrder = -25, ShimmerOrder = -20;

        /// <summary>The share of the weather drawn (1 = all, 0.5 = half on phones). Set 0 with <see cref="Enabled"/> off for a baseline.</summary>
        public static float Quality = Application.isMobilePlatform ? 0.5f : 1f;
        /// <summary>Everything off (the playtest's baseline frame time).</summary>
        public static bool Enabled = true;
        /// <summary>Force weather bits on (the ambience playtest). Living packs also draw weather via <see cref="AmbienceDef.liveWeather"/>.</summary>
        public static bool Weather = false;

        sealed class Bit
        {
            public SpriteRenderer Renderer;
            public WeatherDef Def;
            public float U, V, Phase, SizeT, SpeedT;
            public int Rank;
            public bool Active;
        }

        sealed class Actor
        {
            public SpriteRenderer Renderer;
            public AmbientLifeDef Def;
            public float Age, Dir, StartX, Y, SizeMul, SpeedMul, Delay, Phase, Duration;
            public bool Active;
        }

        sealed class LifeTimer
        {
            public AmbientLifeDef Def;
            public float Next;
        }

        sealed class EventTimer
        {
            public AmbientEventDef Def;
            public float Next;
        }

        sealed class EventFx
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer Glow;
            public AmbientEventDef Def;
            public float Age, Duration, X, Y, Parallax, Size;
            public bool Active;
        }

        GameplayWorld world;
        CircleDef circle;
        AmbienceDef def;
        Camera cam;
        Vector2 camHome;
        float clock;
        float gateAt;
        System.Random random;
        Transform weatherRoot, lifeRoot, depthRoot, eventRoot;
        bool liveWeather;
        float heatShimmer;
        readonly List<SpriteRenderer> shimmerBars = new List<SpriteRenderer>();
        int foregroundLayers;

        readonly List<Bit> bits = new List<Bit>();
        readonly Actor[] actors = new Actor[MaxLife];
        readonly Actor[] freeActors = new Actor[2];
        readonly List<LifeTimer> timers = new List<LifeTimer>();
        readonly List<EventTimer> eventTimers = new List<EventTimer>();
        readonly EventFx[] eventFx = new EventFx[MaxEvents];
        readonly List<ParallaxController.Entry> depthEntries = new List<ParallaxController.Entry>();
        readonly Dictionary<WeatherKind, int> activeByKind = new Dictionary<WeatherKind, int>();
        readonly HashSet<string> lifeSeen = new HashSet<string>();
        readonly HashSet<string> eventsSeen = new HashSet<string>();
        readonly HashSet<string> signalsSeen = new HashSet<string>();
        int depthShapes;
        int lifeAppearances;
        int eventFirings;

        /// <summary>The Circle this air belongs to.</summary>
        public CircleDef Circle => circle;
        /// <summary>Weather bits drawn right now.</summary>
        public int ActiveBits { get; private set; }
        /// <summary>Weather bits built (the pool).</summary>
        public int BitCount => bits.Count;
        /// <summary>Distinct weather kinds with at least one bit drawn.</summary>
        public int ActiveKindCount
        {
            get
            {
                int n = 0;
                foreach (var pair in activeByKind)
                    if (pair.Value > 0) n++;
                return n;
            }
        }
        /// <summary>Depth rows and the silhouettes in them.</summary>
        public int DepthLayers => depthEntries.Count;
        public int DepthShapes => depthShapes;
        /// <summary>Foreground silhouette rows (Foreground1/2) drawn over the play area.</summary>
        public int ForegroundLayers => foregroundLayers;
        public IReadOnlyList<ParallaxController.Entry> DepthEntries => depthEntries;
        /// <summary>Background actors on screen now / appearances so far / the names of the kinds seen.</summary>
        public int ActiveLife { get; private set; }
        public int LifeAppearances => lifeAppearances;
        public IReadOnlyCollection<string> LifeSeen => lifeSeen;
        /// <summary>Timed ambient events fired so far / kinds seen / active FX.</summary>
        public int EventFirings => eventFirings;
        public IReadOnlyCollection<string> EventsSeen => eventsSeen;
        public int ActiveEvents { get; private set; }
        /// <summary>Heat shimmer bars are up (false on phone or when strength is 0).</summary>
        public bool HeatShimmerOn => shimmerBars.Count > 0 && shimmerBars[0] != null && shimmerBars[0].enabled;
        /// <summary>Bits drawn now of one kind of weather.</summary>
        public int ActiveOf(WeatherKind kind) => activeByKind.TryGetValue(kind, out int n) ? n : 0;
        /// <summary>The kinds of weather in this Circle's air, drawn now.</summary>
        public IEnumerable<WeatherKind> ActiveKinds
        {
            get
            {
                foreach (var pair in activeByKind)
                    if (pair.Value > 0)
                        yield return pair.Key;
            }
        }

        public static CircleAmbience Current { get; private set; }

        public static CircleAmbience Create(GameplayWorld world, CircleDef circle)
        {
            if (world == null || world.Camera == null || circle == null || circle.ambience == null || circle.ambience.IsEmpty)
                return null;
            var go = new GameObject("CircleAmbience");
            go.transform.SetParent(world.transform, false);
            var ambience = go.AddComponent<CircleAmbience>();
            ambience.world = world;
            ambience.circle = circle;
            ambience.cam = world.Camera;
            ambience.camHome = world.Camera.transform.position;
            ambience.random = new System.Random((int)(world.Seed & 0x7FFFFFFF) ^ 0x1A1B);
            ambience.Build(circle.ambience);
            Current = ambience;
            return ambience;
        }

        void Build(AmbienceDef source)
        {
            def = source;
            liveWeather = source.liveWeather;
            heatShimmer = Mathf.Max(0f, source.heatShimmer);
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            depthRoot = new GameObject("Depth").transform;
            depthRoot.SetParent(transform, false);
            weatherRoot = new GameObject("Weather").transform;
            weatherRoot.SetParent(transform, false);
            lifeRoot = new GameObject("Life").transform;
            lifeRoot.SetParent(transform, false);
            eventRoot = new GameObject("Events").transform;
            eventRoot.SetParent(transform, false);

            for (int i = 0; source.depth != null && i < source.depth.Count; i++)
                if (source.depth[i] != null)
                    BuildDepth(source.depth[i], i, halfW, halfH);

            int budget = MaxBits;
            for (int i = 0; source.weather != null && i < source.weather.Count && budget > 0; i++)
            {
                var w = source.weather[i];
                if (w == null || w.count <= 0)
                    continue;
                int n = Mathf.Min(budget, w.count);
                budget -= n;
                var sprite = AmbienceSprites.BitOr(w.kind, w.sprite);
                for (int b = 0; b < n; b++)
                {
                    var go = new GameObject(w.kind.ToString());
                    go.transform.SetParent(weatherRoot, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    SpriteLayers.Apply(sr, w.inFront ? SpriteLayer.Foreground1 : SpriteLayer.Background2, w.inFront ? 60 : WeatherOrder);
                    go.SetActive(false);
                    int seed = i * 9973 + b * 31;
                    bits.Add(new Bit
                    {
                        Renderer = sr,
                        Def = w,
                        U = Hash(seed + 1),
                        V = Hash(seed + 2),
                        Phase = Hash(seed + 3) * 6.2832f,
                        SizeT = Hash(seed + 4),
                        SpeedT = Hash(seed + 5),
                        Rank = b,
                    });
                }
            }

            for (int i = 0; source.life != null && i < source.life.Count; i++)
            {
                if (source.life[i] == null)
                    continue;
                timers.Add(new LifeTimer { Def = source.life[i], Next = FirstDelay(source.life[i]) });
            }
            for (int i = 0; i < actors.Length && timers.Count > 0; i++)
            {
                var go = new GameObject("Actor");
                go.transform.SetParent(lifeRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(sr, SpriteLayer.Background2, LifeOrder);
                go.SetActive(false);
                actors[i] = new Actor { Renderer = sr };
            }

            for (int i = 0; source.events != null && i < source.events.Count; i++)
            {
                if (source.events[i] == null)
                    continue;
                eventTimers.Add(new EventTimer { Def = source.events[i], Next = FirstEventDelay(source.events[i]) });
            }
            for (int i = 0; i < eventFx.Length && eventTimers.Count > 0; i++)
            {
                var go = new GameObject("Event");
                go.transform.SetParent(eventRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(sr, SpriteLayer.Background2, EventOrder);
                go.SetActive(false);
                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(go.transform, false);
                var glow = glowGo.AddComponent<SpriteRenderer>();
                glow.sprite = AmbienceSprites.Bit(WeatherKind.Steam);
                SpriteLayers.Apply(glow, SpriteLayer.Background2, EventOrder - 1);
                glowGo.SetActive(false);
                eventFx[i] = new EventFx { Renderer = sr, Glow = glow };
            }

            // Heat shimmer: soft wobbling bars over lava. Phone tier skips it (brief: no heat distortion on phone).
            if (heatShimmer > 0.01f && Quality >= 0.99f && !Application.isMobilePlatform)
            {
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject("Shimmer" + i);
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = AmbienceSprites.Bit(WeatherKind.Steam);
                    sr.color = new Color(1f, 0.55f, 0.25f, 0.07f - i * 0.015f);
                    SpriteLayers.Apply(sr, SpriteLayer.Background2, ShimmerOrder + i);
                    shimmerBars.Add(sr);
                }
            }

            RefreshGate();
            LateUpdate();
        }

        float FirstDelay(AmbientLifeDef lifeDef)
        {
            // The first appearance comes early, so a Circle's life shows within a few seconds of arriving.
            float lo = Mathf.Max(0.5f, lifeDef.interval.x * 0.2f), hi = Mathf.Max(lo + 0.5f, lifeDef.interval.x * 0.5f);
            return Mathf.Lerp(lo, hi, (float)random.NextDouble());
        }

        float FirstEventDelay(AmbientEventDef e)
        {
            float lo = Mathf.Max(0.8f, e.interval.x * 0.15f), hi = Mathf.Max(lo + 0.4f, e.interval.x * 0.45f);
            return Mathf.Lerp(lo, hi, (float)random.NextDouble());
        }

        // ─── Depth layers ─────────────────────────────────────────────

        void BuildDepth(DepthLayerDef layer, int index, float halfW, float halfH)
        {
            float sx = Mathf.Max(0.5f, layer.spacing.x);
            float sy = layer.spacing.y > 0.01f ? Mathf.Max(0.5f, layer.spacing.y) : 0f;
            float reach = Mathf.Max(layer.size.x, layer.size.y) * (1f + Mathf.Max(0f, layer.sizeVariation)) + 1f;
            float localBase = layer.baseY - camHome.y;
            int n = Mathf.CeilToInt((halfW * 2f + reach * 2f) / sx);
            int m = sy > 0f ? Mathf.CeilToInt((halfH * 2f + (reach + Mathf.Abs(localBase)) * 2f) / sy) : 0;
            while ((2 * n + 1) * (2 * m + 1) > DepthBudget && (n > 2 || m > 1))
            {
                if (m > 1)
                    m--;
                else
                    n--;
            }

            if (layer.layer == SpriteLayer.Foreground1 || layer.layer == SpriteLayer.Foreground2)
                foregroundLayers++;
            var root = new GameObject("Depth_" + (string.IsNullOrEmpty(layer.name) ? index.ToString() : layer.name));
            root.transform.SetParent(depthRoot, false);
            root.transform.position = new Vector3(camHome.x, camHome.y, 0f);
            var sprite = layer.sprite != null ? layer.sprite : StageTrack.PropShapes.Get(layer.shape);
            Vector2 native = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            for (int i = -n; i <= n; i++)
            {
                for (int j = -m; j <= m; j++)
                {
                    // The same slot every period, so the row repeats seamlessly as it wraps.
                    int si = ((i % Mathf.Max(1, n)) + Mathf.Max(1, n)) % Mathf.Max(1, n);
                    int sj = m > 0 ? ((j % m) + m) % m : 0;
                    int seed = index * 7919 + si * 131 + sj * 524287;
                    if (Hash(seed) >= layer.chance)
                        continue;
                    var go = new GameObject("Shape");
                    go.transform.SetParent(root.transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = layer.color;
                    SpriteLayers.Apply(sr, layer.layer, layer.sortingOrder + index);
                    float w = layer.size.x * (1f + (Hash(seed + 1) * 2f - 1f) * layer.sizeVariation);
                    float h = layer.size.y * (1f + (Hash(seed + 2) * 2f - 1f) * layer.sizeVariation);
                    bool flip = Hash(seed + 3) < 0.5f;
                    go.transform.localScale = new Vector3(w / Mathf.Max(0.01f, native.x) * (flip ? -1f : 1f),
                        h / Mathf.Max(0.01f, native.y) * (layer.hanging ? -1f : 1f), 1f);
                    float z = go.transform.localPosition.z;
                    go.transform.localPosition = new Vector3(i * sx + (Hash(seed + 4) - 0.5f) * sx * 0.6f, localBase + j * sy, z);
                    depthShapes++;
                }
            }
            var parallax = ParallaxController.Obtain(cam);
            if (parallax != null)
            {
                var entry = parallax.Add(root.name, root.transform, layer.parallax,
                    new Vector2(Mathf.Max(1, n) * sx, m > 0 ? m * sy : 0f));
                depthEntries.Add(entry);
            }
        }

        // ─── Weather and life ─────────────────────────────────────────

        void LateUpdate()
        {
            if (cam == null || world == null)
                return;
            if (!Enabled || Quality <= 0f)
            {
                if (ActiveBits > 0 || ActiveLife > 0 || ActiveEvents > 0)
                    HideAll();
                SetDepthVisible(false);
                SetShimmerVisible(false);
                return;
            }
            SetDepthVisible(true);
            SetShimmerVisible(true);

            // Weather and far-off life hold still while time runs backward: they are only atmosphere, and the
            // frame is better spent on the rewind.
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;

            clock += Time.deltaTime;
            if (clock >= gateAt)
                RefreshGate();

            Vector2 camPos = cam.transform.position;
            Vector2 delta = camPos - camHome;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            UpdateWeather(camPos, delta, halfW, halfH);
            UpdateLife(camPos, delta, halfW, halfH);
            UpdateEvents(camPos, delta, halfW, halfH);
            UpdateShimmer(camPos, halfW, halfH);
        }

        void SetShimmerVisible(bool on)
        {
            for (int i = 0; i < shimmerBars.Count; i++)
                if (shimmerBars[i] != null)
                    shimmerBars[i].enabled = on;
        }

        void UpdateShimmer(Vector2 camPos, float halfW, float halfH)
        {
            if (shimmerBars.Count == 0)
                return;
            float t = clock;
            for (int i = 0; i < shimmerBars.Count; i++)
            {
                var sr = shimmerBars[i];
                if (sr == null) continue;
                float wobble = Mathf.Sin(t * (1.7f + i * 0.6f) + i) * 0.35f * heatShimmer;
                float y = camPos.y - halfH * 0.55f + i * 0.35f;
                sr.transform.position = new Vector3(camPos.x + wobble, y, sr.transform.position.z);
                float native = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
                float w = halfW * 2.2f, h = 0.28f + 0.08f * Mathf.Sin(t * 2.9f + i) * heatShimmer;
                sr.transform.localScale = new Vector3(w / Mathf.Max(0.01f, native), h / Mathf.Max(0.01f, native), 1f);
            }
        }

        bool depthVisible = true;

        void SetDepthVisible(bool on)
        {
            if (on == depthVisible || depthRoot == null)
                return;
            depthVisible = on;
            depthRoot.gameObject.SetActive(on);
        }

        void HideAll()
        {
            foreach (var bit in bits)
            {
                bit.Active = false;
                bit.Renderer.gameObject.SetActive(false);
            }
            foreach (var a in actors)
            {
                if (a == null)
                    continue;
                a.Active = false;
                a.Renderer.gameObject.SetActive(false);
            }
            foreach (var fx in eventFx)
            {
                if (fx == null) continue;
                fx.Active = false;
                fx.Renderer.gameObject.SetActive(false);
                if (fx.Glow != null) fx.Glow.gameObject.SetActive(false);
            }
            activeByKind.Clear();
            ActiveBits = 0;
            ActiveLife = 0;
            ActiveEvents = 0;
        }

        int Floor => world.Stage != null && world.Stage.HasFloors ? world.Stage.Floor : 0;

        static bool FloorOpen(int fromFloor, int floor) => fromFloor <= 0 || floor <= 0 || floor <= fromFloor;

        /// <summary>Which bits are drawn: the first share of each weather def (by Quality), and only on floors the def is for. Twice a second.</summary>
        void RefreshGate()
        {
            gateAt = clock + 0.5f;
            int floor = Floor;
            ActiveBits = 0;
            activeByKind.Clear();
            foreach (var bit in bits)
            {
                // Living packs (Meadow, Floors 1000-997) draw weather for players; Circles stay quiet unless the playtest forces Weather on.
                bool on = (liveWeather || Weather) && FloorOpen(bit.Def.fromFloor, floor) && bit.Rank < Mathf.CeilToInt(bit.Def.count * Mathf.Clamp01(Quality));
                if (on != bit.Active)
                {
                    bit.Active = on;
                    bit.Renderer.gameObject.SetActive(on);
                }
                if (on)
                {
                    ActiveBits++;
                    activeByKind.TryGetValue(bit.Def.kind, out int n);
                    activeByKind[bit.Def.kind] = n + 1;
                }
            }
        }

        void UpdateWeather(Vector2 camPos, Vector2 delta, float halfW, float halfH)
        {
            float bw = halfW * 2f + 2f, bh = halfH * 2f + 2f;
            float t = clock;
            for (int i = 0; i < bits.Count; i++)
            {
                var bit = bits[i];
                if (!bit.Active)
                    continue;
                var d = bit.Def;
                float speed = Mathf.Lerp(d.speed.x, d.speed.y, bit.SpeedT);
                float size = Mathf.Lerp(d.size.x, d.size.y, bit.SizeT);
                float sway = Mathf.Sin(t * (0.5f + bit.SpeedT) + bit.Phase) * d.sway;
                bool rises = d.kind == WeatherKind.Sparks || d.kind == WeatherKind.Runes || d.kind == WeatherKind.Steam;
                float dirY = rises ? 1f : -1f;
                float flick = 1f;
                float alpha = d.color.a;
                float spin = 0f;
                float stretchY = 1f;
                switch (d.kind)
                {
                    case WeatherKind.Sparks:
                        flick = 0.5f + 0.5f * Mathf.Sin(t * 14f + bit.Phase);
                        alpha *= flick;
                        speed *= 0.7f + 0.6f * flick;
                        stretchY = 1.6f;
                        break;
                    case WeatherKind.Cinders:
                        alpha *= 0.65f + 0.35f * Mathf.Sin(t * 3f + bit.Phase);
                        spin = t * (40f + 80f * bit.SpeedT) + bit.Phase * 57f;
                        break;
                    case WeatherKind.Runes:
                        alpha *= 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 1.8f + bit.Phase));
                        spin = Mathf.Sin(t * 0.6f + bit.Phase) * 18f;
                        break;
                    case WeatherKind.Grit:
                    case WeatherKind.Dust:
                        spin = t * 120f * (bit.SpeedT - 0.5f);
                        break;
                    case WeatherKind.Pollen:
                        alpha *= 0.55f + 0.45f * Mathf.Sin(t * 1.2f + bit.Phase);
                        sway *= 1.4f;
                        break;
                    case WeatherKind.Debris:
                        stretchY = 2.4f;
                        spin = t * 90f * (bit.SpeedT - 0.5f);
                        break;
                    case WeatherKind.Steam:
                        alpha *= 0.4f + 0.4f * Mathf.Sin(t * 2.1f + bit.Phase);
                        stretchY = 1.8f;
                        break;
                    case WeatherKind.Soul:
                        alpha *= 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f + bit.Phase));
                        sway *= 1.2f;
                        break;
                }
                // Position in the layer's own space, then shifted by the share of the camera's travel it does not follow, and wrapped
                // into a box around the camera (the same rule as the ParallaxController's entries).
                float qx = bit.U * bw + d.wind * t + sway - delta.x * (1f - d.parallax);
                float qy = bit.V * bh + dirY * speed * t - delta.y * (1f - d.parallax);
                float rx = Mathf.Repeat(qx + bw * 0.5f, bw) - bw * 0.5f;
                float ry = Mathf.Repeat(qy + bh * 0.5f, bh) - bh * 0.5f;
                float edge = Mathf.Clamp01((bw * 0.5f - Mathf.Abs(rx)) / (bw * 0.08f)) * Mathf.Clamp01((bh * 0.5f - Mathf.Abs(ry)) / (bh * 0.08f));
                var c = d.color;
                c.a = alpha * edge;
                var tr = bit.Renderer.transform;
                tr.position = new Vector3(camPos.x + rx, camPos.y + ry, tr.position.z);
                tr.rotation = Quaternion.Euler(0f, 0f, spin);
                tr.localScale = new Vector3(size, size * stretchY, 1f);
                bit.Renderer.color = c;
            }
        }

        void UpdateEvents(Vector2 camPos, Vector2 delta, float halfW, float halfH)
        {
            if (eventTimers.Count == 0)
                return;
            float dt = Time.deltaTime;
            bool phone = Application.isMobilePlatform || Quality < 0.99f;

            // Signal-triggered events (meadow:raid → birds scatter / smoke).
            for (int i = 0; i < eventTimers.Count; i++)
            {
                var e = eventTimers[i].Def;
                if (e == null || string.IsNullOrEmpty(e.onSignal))
                    continue;
                if (signalsSeen.Contains(e.onSignal))
                    continue;
                if (world.HasSignal(e.onSignal))
                {
                    signalsSeen.Add(e.onSignal);
                    if (!(phone && e.phoneOff))
                        FireEvent(e, halfW, halfH);
                }
            }

            for (int i = 0; i < eventTimers.Count; i++)
            {
                var timer = eventTimers[i];
                if (timer.Def.signalOnly)
                    continue; // Meadow raid smoke/birds: only onSignal.
                timer.Next -= dt;
                if (timer.Next > 0f)
                    continue;
                if (!(phone && timer.Def.phoneOff))
                    FireEvent(timer.Def, halfW, halfH);
                timer.Next = Mathf.Lerp(timer.Def.interval.x, Mathf.Max(timer.Def.interval.x, timer.Def.interval.y), (float)random.NextDouble());
            }

            int active = 0;
            for (int i = 0; i < eventFx.Length; i++)
            {
                var fx = eventFx[i];
                if (fx == null || !fx.Active)
                    continue;
                fx.Age += dt;
                if (fx.Age >= fx.Duration)
                {
                    fx.Active = false;
                    fx.Renderer.gameObject.SetActive(false);
                    if (fx.Glow != null) fx.Glow.gameObject.SetActive(false);
                    continue;
                }
                active++;
                float u = fx.Age / Mathf.Max(0.01f, fx.Duration);
                float f = fx.Parallax;
                float qx = fx.X - delta.x * (1f - f);
                float qy = fx.Y - delta.y * (1f - f);
                float rise = 0f, scale = fx.Size, alpha = fx.Def.color.a;
                switch (fx.Def.kind)
                {
                    case AmbientEventKind.Geyser:
                        rise = u * 2.4f * fx.Size;
                        scale = fx.Size * (0.4f + 1.4f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.2f)));
                        alpha *= 1f - u;
                        break;
                    case AmbientEventKind.SteamVent:
                        rise = u * 1.6f;
                        scale = fx.Size * (0.6f + u * 1.8f);
                        alpha *= (1f - u) * 0.7f;
                        break;
                    case AmbientEventKind.DebrisFall:
                        rise = -u * 3.5f;
                        scale = fx.Size * (1f - 0.3f * u);
                        alpha *= 1f - u * 0.8f;
                        break;
                    case AmbientEventKind.BirdsScatter:
                        qx += (u * 6f) * (fx.X >= 0f ? 1f : -1f);
                        rise = u * 2.2f;
                        scale = fx.Size * (1f + 0.3f * Mathf.Sin(u * 18f));
                        alpha *= 1f - u;
                        break;
                    case AmbientEventKind.SmokePlume:
                        rise = u * 2.8f;
                        scale = fx.Size * (0.8f + u * 2f);
                        alpha *= (1f - u) * 0.55f;
                        break;
                    default:
                        rise = 0f;
                        alpha *= 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI);
                        scale = fx.Size * (1f + 0.4f * Mathf.Sin(u * Mathf.PI));
                        break;
                }
                var tr = fx.Renderer.transform;
                tr.position = new Vector3(camPos.x + qx, camPos.y + qy + rise, tr.position.z);
                float native = fx.Renderer.sprite != null ? fx.Renderer.sprite.bounds.size.x : 1f;
                tr.localScale = new Vector3(scale / Mathf.Max(0.01f, native), scale * 1.3f / Mathf.Max(0.01f, native), 1f);
                var c = fx.Def.color;
                c.a = alpha;
                fx.Renderer.color = c;
                if (fx.Glow != null && fx.Glow.gameObject.activeSelf)
                {
                    fx.Glow.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
                    var g = fx.Def.color;
                    g.a = alpha * 0.45f;
                    fx.Glow.color = g;
                }
            }
            ActiveEvents = active;
        }

        void FireEvent(AmbientEventDef e, float halfW, float halfH)
        {
            EventFx slot = null;
            for (int i = 0; i < eventFx.Length; i++)
            {
                if (eventFx[i] != null && !eventFx[i].Active)
                {
                    slot = eventFx[i];
                    break;
                }
            }
            if (slot == null)
                return;

            slot.Def = e;
            slot.Active = true;
            slot.Age = 0f;
            slot.Duration = Mathf.Max(0.4f, e.duration);
            slot.Parallax = e.parallax;
            slot.Size = Mathf.Max(0.2f, e.size);
            slot.X = (float)(random.NextDouble() * 2.0 - 1.0) * halfW * 0.7f;
            float yFrac = e.kind == AmbientEventKind.BirdsScatter ? 0.65f + (float)random.NextDouble() * 0.25f
                : e.kind == AmbientEventKind.DebrisFall ? 0.85f
                : 0.08f + (float)random.NextDouble() * 0.25f;
            slot.Y = Mathf.Lerp(-halfH, halfH, yFrac);

            WeatherKind look = WeatherKind.Steam;
            switch (e.kind)
            {
                case AmbientEventKind.Geyser: look = WeatherKind.Sparks; break;
                case AmbientEventKind.SteamVent:
                case AmbientEventKind.SmokePlume: look = WeatherKind.Steam; break;
                case AmbientEventKind.DebrisFall: look = WeatherKind.Debris; break;
                case AmbientEventKind.BirdsScatter: look = WeatherKind.Soul; break;
                default: look = WeatherKind.Cinders; break;
            }
            slot.Renderer.sprite = AmbienceSprites.Bit(look);
            slot.Renderer.color = new Color(e.color.r, e.color.g, e.color.b, 0f);
            slot.Renderer.gameObject.SetActive(true);
            bool glow = e.kind == AmbientEventKind.Geyser || e.kind == AmbientEventKind.GlowPulse;
            if (slot.Glow != null)
                slot.Glow.gameObject.SetActive(glow);

            if (!string.IsNullOrEmpty(e.sound))
                SickOfIt.Core.Audio.Sfx.Play(e.sound);

            eventFirings++;
            eventsSeen.Add(string.IsNullOrEmpty(e.name) ? e.kind.ToString() : e.name);
            world.Telemetry?.Record("ambience-event", string.IsNullOrEmpty(e.name) ? e.kind.ToString() : e.name, eventFirings);
        }

        void UpdateLife(Vector2 camPos, Vector2 delta, float halfW, float halfH)
        {
            if (timers.Count == 0)
                return;
            float dt = Time.deltaTime;
            int floor = Floor;
            for (int i = 0; i < timers.Count; i++)
            {
                var timer = timers[i];
                timer.Next -= dt;
                if (timer.Next > 0f)
                    continue;
                if (FloorOpen(timer.Def.fromFloor, floor) && Quality >= 0.5f)
                    Appear(timer.Def, halfW, halfH);
                timer.Next = Mathf.Lerp(timer.Def.interval.x, Mathf.Max(timer.Def.interval.x, timer.Def.interval.y), (float)random.NextDouble());
            }

            int active = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                var a = actors[i];
                if (a == null || !a.Active)
                    continue;
                a.Age += dt;
                float age = a.Age - a.Delay;
                if (a.Age >= a.Duration)
                {
                    a.Active = false;
                    a.Renderer.gameObject.SetActive(false);
                    continue;
                }
                var d = a.Def;
                active++;
                if (age < 0f)
                {
                    a.Renderer.enabled = false;
                    continue;
                }
                a.Renderer.enabled = true;
                float f = d.parallax;
                float qx = a.StartX + a.Dir * d.speed * a.SpeedMul * age - delta.x * (1f - f);
                float qy = a.Y - delta.y * (1f - f);
                float size = d.size * a.SizeMul;
                float bob = 0f, flap = 1f;
                switch (d.behavior)
                {
                    case LifeBehavior.Flyer:
                        bob = Mathf.Sin(age * 1.6f + a.Phase) * 0.35f;
                        flap = 1f + 0.35f * Mathf.Sin(age * 9f + a.Phase);
                        break;
                    case LifeBehavior.Walker:
                        bob = Mathf.Abs(Mathf.Sin(age * 5f + a.Phase)) * 0.05f * size;
                        break;
                    default:
                        // Running: a hop in every stride; the big one hops slower.
                        bob = Mathf.Abs(Mathf.Sin(age * (a.SizeMul > 1f ? 4.2f : 6.5f) + a.Phase)) * 0.12f * size;
                        break;
                }
                var tr = a.Renderer.transform;
                tr.position = new Vector3(camPos.x + qx, camPos.y + qy + bob, tr.position.z);
                tr.localScale = new Vector3(size * (a.Dir < 0f ? -1f : 1f), size * flap, 1f);
                float fadeIn = Mathf.Clamp01(age / 0.6f), fadeOut = Mathf.Clamp01((a.Duration - a.Age) / 0.6f);
                var c = d.color;
                c.a *= Mathf.Min(fadeIn, fadeOut);
                a.Renderer.color = c;
            }
            ActiveLife = active;
        }

        void Appear(AmbientLifeDef def, float halfW, float halfH)
        {
            // A Chase is two shapes, the second a stride behind; a painted chase sprite already holds both, so it is one.
            int members = def.behavior == LifeBehavior.Chase && def.sprite == null ? 2 : 1;
            var free = freeActors;
            int found = 0;
            for (int i = 0; i < actors.Length && found < members; i++)
            {
                if (actors[i] != null && !actors[i].Active)
                    free[found++] = actors[i];
            }
            if (found < members)
                return;

            float dir = random.NextDouble() < 0.5 ? -1f : 1f;
            float frac = Mathf.Lerp(def.height.x, def.height.y, (float)random.NextDouble());
            float y = Mathf.Lerp(-halfH, halfH, Mathf.Clamp01(frac));
            float gap = members > 1 ? Mathf.Max(2f, def.size * 5f) : 0f;
            float travel = halfW * 2f + def.size * 4f + gap + 4f;
            var sprite = def.sprite != null ? def.sprite : AmbienceSprites.Life(def.shape);
            for (int m = 0; m < members; m++)
            {
                var a = free[m];
                bool chaser = m == 1;
                a.Def = def;
                a.Active = true;
                a.Age = 0f;
                a.Dir = dir;
                a.Y = y;
                a.SizeMul = chaser ? 1.6f : 1f;
                a.SpeedMul = chaser ? 1.12f : 1f;
                a.Phase = (float)random.NextDouble() * 6.28f;
                // Both start off screen on the same side; the chaser a stride behind.
                a.StartX = -dir * (halfW + def.size * 2f + (chaser ? gap : 0f));
                a.Delay = 0f;
                a.Duration = travel / (def.speed * a.SpeedMul) + 1f;
                a.Renderer.sprite = sprite;
                a.Renderer.color = new Color(def.color.r, def.color.g, def.color.b, 0f);
                a.Renderer.gameObject.SetActive(true);
            }
            lifeAppearances++;
            lifeSeen.Add(string.IsNullOrEmpty(def.name) ? def.behavior + ":" + def.shape : def.name);
        }

        static float Hash(int n)
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return (x & 0xFFFFFF) / 16777216f;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }
    }
}
