using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The climb leaves a trace (docs/world-bible.md "The climb leaves a trace"): every death scorches the stone where the hero
    /// fell and every rewind scratches a cyan seam where time was cut, kept per floor in PlayerPrefs (cleared by New Game), and
    /// drawn again whenever he comes back to that floor. Presentation only: world sprites, nothing in the simulation, nothing
    /// rewound (a death undone by a rewind still happened, so its scorch stays).
    /// </summary>
    public sealed class ClimbTrace : MonoBehaviour
    {
        const string Prefix = "Trace_F";
        const int MaxPerFloor = 14;
        const float MinGap = 0.9f;

        public enum Mark { Scorch = 0, Seam = 1 }
        struct Entry { public float X, Y; public Mark Kind; }

        static bool hooked;
        static ClimbTrace current;
        static Sprite scorchSprite, seamSprite;

        readonly List<Entry> entries = new List<Entry>();
        readonly List<SpriteRenderer> drawn = new List<SpriteRenderer>();
        GameplayWorld world;
        GameClock clock;
        int floor;

        /// <summary>Marks drawn on this floor (the playtest reads these).</summary>
        public static int Drawn => current != null ? current.drawn.Count : 0;
        public static int DeathMarks { get; private set; }
        public static int RewindMarks { get; private set; }
        /// <summary>Marks saved for a floor, all kinds.</summary>
        public static int Saved(int floorNumber) => Load(floorNumber).Count;

        public static void Attach(GameplayWorld owner)
        {
            if (owner == null)
                return;
            var t = owner.gameObject.AddComponent<ClimbTrace>();
            t.world = owner;
            if (!hooked)
            {
                hooked = true;
                PlayerHealth.HeroDied += OnHeroDied;
                SickOfIt.Data.ClimbTraceStore.Cleared += () => DeathMarks = RewindMarks = 0;
            }
        }

        public static void Reset()
        {
            SickOfIt.Data.ClimbTraceStore.Reset();
        }

        void Start()
        {
            current = this;
            floor = CurrentFloor(world);
            entries.AddRange(Load(floor));
            foreach (var e in entries)
                Draw(e);
            clock = GameClock.Instance;
            if (clock != null)
                clock.OnRewindStarted += OnRewind;
        }

        void OnDestroy()
        {
            if (clock != null)
                clock.OnRewindStarted -= OnRewind;
            if (current == this)
                current = null;
        }

        static int CurrentFloor(GameplayWorld w) => w != null && w.Stage != null ? w.Stage.Floor : 0;

        static void OnHeroDied(Vector2 at)
        {
            if (current != null)
            {
                DeathMarks++;
                current.Add(at, Mark.Scorch);
            }
        }

        void OnRewind()
        {
            var hero = world != null ? world.PlayerController : null;
            if (hero == null)
                return;
            RewindMarks++;
            Add(hero.transform.position, Mark.Seam);
        }

        void Add(Vector2 at, Mark kind)
        {
            if (floor <= 0)
                return;
            foreach (var e in entries)
                if (e.Kind == kind && Mathf.Abs(e.X - at.x) < MinGap && Mathf.Abs(e.Y - at.y) < MinGap)
                    return;
            if (entries.Count >= MaxPerFloor)
            {
                entries.RemoveAt(0);
                if (drawn.Count > 0)
                {
                    if (drawn[0] != null)
                        Destroy(drawn[0].gameObject);
                    drawn.RemoveAt(0);
                }
            }
            var entry = new Entry { X = at.x, Y = at.y, Kind = kind };
            entries.Add(entry);
            Draw(entry);
            Save(floor, entries);
        }

        void Draw(Entry e)
        {
            var go = new GameObject(e.Kind == Mark.Scorch ? "TraceScorch" : "TraceSeam");
            go.transform.SetParent(transform, false);
            // Lies on the stone under the hero's feet, the scorch a little to the side of true centre so repeats do not stack.
            float jitter = (Mathf.Abs(Mathf.Sin(e.X * 12.9f + e.Y * 78.2f)) - 0.5f) * 0.4f;
            go.transform.position = new Vector3(e.X + jitter, e.Y - 0.45f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1;
            if (e.Kind == Mark.Scorch)
            {
                sr.sprite = ScorchSprite();
                sr.color = new Color(1f, 1f, 1f, 0.85f);
                go.transform.localScale = new Vector3(1.6f, 0.5f, 1f);
            }
            else
            {
                sr.sprite = SeamSprite();
                sr.color = new Color(0.45f, 0.95f, 1f, 0.7f);
                go.transform.localScale = new Vector3(1.1f, 1f, 1f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, jitter * 40f);
            }
            drawn.Add(sr);
        }

        static Sprite ScorchSprite()
        {
            if (scorchSprite != null)
                return scorchSprite;
            const int n = 64;
            var tex = NewTex(n, "TraceScorch");
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n * 0.5f) / (n * 0.5f), dy = (y - n * 0.5f) / (n * 0.5f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ragged = 0.78f + 0.2f * Mathf.Sin(Mathf.Atan2(dy, dx) * 7f + 1.3f);
                    float a = Mathf.Clamp01((ragged - r) / 0.35f);
                    float core = Mathf.Clamp01(1f - r / 0.4f);
                    byte c = (byte)(14 + 40 * (1f - core));
                    px[y * n + x] = new Color32(c, (byte)(c * 0.7f), (byte)(c * 0.8f), (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return scorchSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
        }

        static Sprite SeamSprite()
        {
            if (seamSprite != null)
                return seamSprite;
            const int w = 64, h = 16;
            var tex = NewTex(w, "TraceSeam");
            tex.Reinitialize(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float ax = 1f - Mathf.Abs(x - w * 0.5f) / (w * 0.5f);
                    float line = Mathf.Clamp01(1f - Mathf.Abs(y - h * 0.5f + Mathf.Sin(x * 0.4f) * 1.5f) / 2f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(line * ax * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return seamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
        }

        static Texture2D NewTex(int n, string name) =>
            new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = name };

        // ─── The save: "x,y,kind;x,y,kind" per floor, and a list of floors so New Game can clear them ───

        static List<Entry> Load(int f)
        {
            var list = new List<Entry>();
            string s = PlayerPrefs.GetString(Prefix + f, "");
            if (string.IsNullOrEmpty(s))
                return list;
            foreach (var part in s.Split(';'))
            {
                var p = part.Split(',');
                if (p.Length == 3
                    && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                    && int.TryParse(p[2], out int k))
                    list.Add(new Entry { X = x, Y = y, Kind = (Mark)k });
            }
            return list;
        }

        static void Save(int f, List<Entry> list)
        {
            var sb = new StringBuilder();
            foreach (var e in list)
            {
                if (sb.Length > 0)
                    sb.Append(';');
                sb.Append(e.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                  .Append(e.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append((int)e.Kind);
            }
            PlayerPrefs.SetString(Prefix + f, sb.ToString());
            var floors = new List<string>(PlayerPrefs.GetString(Prefix + "List", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            if (!floors.Contains(f.ToString()))
            {
                floors.Add(f.ToString());
                PlayerPrefs.SetString(Prefix + "List", string.Join(",", floors));
            }
            PlayerPrefs.Save();
        }

        static IEnumerable<string> KnownFloors() =>
            PlayerPrefs.GetString(Prefix + "List", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
    }
}
