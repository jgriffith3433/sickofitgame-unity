using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// A Circle's air (Docs/Story.md "Hell's air"): depth layers at their own parallax (through the <see cref="ParallaxController"/>), weather
    /// (ash, sparks, cinders, runes, grit) and far-off life (flyers, walkers, chases), all from the Circle's <see cref="AmbienceDef"/>.
    /// Presentation only: it reads the camera and the floor counter and never touches the simulation, so it needs no rewind. Everything is
    /// pooled at build time: a fixed set of sprite renderers, moved in one LateUpdate, nothing allocated while it runs.
    /// The frame-time budget: at most <see cref="MaxBits"/> weather bits and <see cref="MaxLife"/> actors, half the bits on phones; the
    /// <c>ambience</c> playtest measures the frame time with it on and off.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class CircleAmbience : MonoBehaviour
    {
        /// <summary>Most weather bits alive over all a Circle's weather.</summary>
        public const int MaxBits = 150;
        /// <summary>Most background actors at once.</summary>
        public const int MaxLife = 8;
        const int DepthBudget = 90;
        const int WeatherOrder = -30, LifeOrder = -40;

        /// <summary>The share of the weather drawn (1 = all, 0.5 = half on phones). Set 0 with <see cref="Enabled"/> off for a baseline.</summary>
        public static float Quality = Application.isMobilePlatform ? 0.5f : 1f;
        /// <summary>Everything off (the playtest's baseline frame time).</summary>
        public static bool Enabled = true;
        /// <summary>The weather bits (ash, sparks, cinders): off for players (J, 2026-10-26: far too many particles); the ambience playtest turns them on to check them.</summary>
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

        GameplayWorld world;
        CircleDef circle;
        Camera cam;
        Vector2 camHome;
        float clock;
        float gateAt;
        System.Random random;
        Transform weatherRoot, lifeRoot, depthRoot;

        readonly List<Bit> bits = new List<Bit>();
        readonly Actor[] actors = new Actor[MaxLife];
        readonly Actor[] freeActors = new Actor[2];
        readonly List<LifeTimer> timers = new List<LifeTimer>();
        readonly List<ParallaxController.Entry> depthEntries = new List<ParallaxController.Entry>();
        readonly Dictionary<WeatherKind, int> activeByKind = new Dictionary<WeatherKind, int>();
        readonly HashSet<string> lifeSeen = new HashSet<string>();
        int depthShapes;
        int lifeAppearances;

        /// <summary>The Circle this air belongs to.</summary>
        public CircleDef Circle => circle;
        /// <summary>Weather bits drawn right now.</summary>
        public int ActiveBits { get; private set; }
        /// <summary>Weather bits built (the pool).</summary>
        public int BitCount => bits.Count;
        /// <summary>Depth rows and the silhouettes in them.</summary>
        public int DepthLayers => depthEntries.Count;
        public int DepthShapes => depthShapes;
        public IReadOnlyList<ParallaxController.Entry> DepthEntries => depthEntries;
        /// <summary>Background actors on screen now / appearances so far / the names of the kinds seen.</summary>
        public int ActiveLife { get; private set; }
        public int LifeAppearances => lifeAppearances;
        public IReadOnlyCollection<string> LifeSeen => lifeSeen;
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

        void Build(AmbienceDef def)
        {
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            depthRoot = new GameObject("Depth").transform;
            depthRoot.SetParent(transform, false);
            weatherRoot = new GameObject("Weather").transform;
            weatherRoot.SetParent(transform, false);
            lifeRoot = new GameObject("Life").transform;
            lifeRoot.SetParent(transform, false);

            for (int i = 0; def.depth != null && i < def.depth.Count; i++)
                if (def.depth[i] != null)
                    BuildDepth(def.depth[i], i, halfW, halfH);

            int budget = MaxBits;
            for (int i = 0; def.weather != null && i < def.weather.Count && budget > 0; i++)
            {
                var w = def.weather[i];
                if (w == null || w.count <= 0)
                    continue;
                int n = Mathf.Min(budget, w.count);
                budget -= n;
                var sprite = AmbienceSprites.Bit(w.kind);
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

            for (int i = 0; def.life != null && i < def.life.Count; i++)
            {
                if (def.life[i] == null)
                    continue;
                timers.Add(new LifeTimer { Def = def.life[i], Next = FirstDelay(def.life[i]) });
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
            RefreshGate();
            LateUpdate();
        }

        float FirstDelay(AmbientLifeDef def)
        {
            // The first appearance comes early, so a Circle's life shows within a few seconds of arriving.
            float lo = Mathf.Max(0.5f, def.interval.x * 0.2f), hi = Mathf.Max(lo + 0.5f, def.interval.x * 0.5f);
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
                if (ActiveBits > 0 || ActiveLife > 0)
                    HideAll();
                SetDepthVisible(false);
                return;
            }
            SetDepthVisible(true);

            clock += Time.deltaTime;
            if (clock >= gateAt)
                RefreshGate();

            Vector2 camPos = cam.transform.position;
            Vector2 delta = camPos - camHome;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            UpdateWeather(camPos, delta, halfW, halfH);
            UpdateLife(camPos, delta, halfW, halfH);
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
            activeByKind.Clear();
            ActiveBits = 0;
            ActiveLife = 0;
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
                bool on = Weather && FloorOpen(bit.Def.fromFloor, floor) && bit.Rank < Mathf.CeilToInt(bit.Def.count * Mathf.Clamp01(Quality));
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
                float dirY = d.kind == WeatherKind.Sparks || d.kind == WeatherKind.Runes ? 1f : -1f;
                float flick = 1f;
                float alpha = d.color.a;
                float spin = 0f;
                switch (d.kind)
                {
                    case WeatherKind.Sparks:
                        flick = 0.5f + 0.5f * Mathf.Sin(t * 14f + bit.Phase);
                        alpha *= flick;
                        speed *= 0.7f + 0.6f * flick;
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
                        spin = t * 120f * (bit.SpeedT - 0.5f);
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
                tr.localScale = new Vector3(size, size * (d.kind == WeatherKind.Sparks ? 1.6f : 1f), 1f);
                bit.Renderer.color = c;
            }
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
