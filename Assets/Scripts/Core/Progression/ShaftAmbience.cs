using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Audio;
using SickOfIt.Gameplay;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The Shaft's living air (Docs/Story.md "Hell's air"): wind streaks rushing past, falling debris, and a faint
    /// foreground chain silhouette. UI-only, pooled Images under the shaft window; presentation never touches the sim.
    /// Phone tier halves the particle counts. Playtest <c>living-air</c> reads <see cref="ActiveKinds"/> / <see cref="EventFirings"/>.
    /// </summary>
    public sealed class ShaftAmbience : MonoBehaviour
    {
        const int MaxBits = 48;
        const int MaxEvents = 4;

        struct Bit
        {
            public RectTransform Rect;
            public Image Image;
            public float U, V, Speed, Size, Phase;
            public int Kind; // 0 wind, 1 debris, 2 ash
        }

        struct EventFx
        {
            public RectTransform Rect;
            public Image Image;
            public float Age, Duration, X, Speed;
            public bool Active;
        }

        readonly List<Bit> bits = new List<Bit>();
        readonly EventFx[] events = new EventFx[MaxEvents];
        float clock;
        float nextDebris;
        int eventFirings;
        RectTransform root;
        readonly HashSet<string> kindsSeen = new HashSet<string>();

        public int ActiveBits { get; private set; }
        public int ActiveKindCount => kindsSeen.Count;
        public int EventFirings => eventFirings;
        public int ForegroundLayers { get; private set; }
        public static ShaftAmbience Current { get; private set; }

        public static ShaftAmbience Attach(RectTransform window, float quality = 1f)
        {
            if (window == null)
                return null;
            var existing = window.GetComponent<ShaftAmbience>();
            if (existing != null)
            {
                existing.Rebuild(quality);
                return existing;
            }
            var amb = window.gameObject.AddComponent<ShaftAmbience>();
            amb.Rebuild(quality);
            Current = amb;
            return amb;
        }

        void Rebuild(float quality)
        {
            quality = Mathf.Clamp01(quality);
            foreach (Transform child in transform)
            {
                if (child.name == "ShaftAir")
                    Destroy(child.gameObject);
            }
            bits.Clear();
            kindsSeen.Clear();
            eventFirings = 0;
            ActiveBits = 0;
            ForegroundLayers = 0;

            root = new GameObject("ShaftAir", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            // Don't steal clicks from Continue / Visit Yooba.
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            int wind = Mathf.Max(4, Mathf.RoundToInt(14 * quality));
            int debris = Mathf.Max(3, Mathf.RoundToInt(10 * quality));
            int ash = Mathf.Max(3, Mathf.RoundToInt(12 * quality));
            AddBits(0, wind, new Color(0.85f, 0.9f, 1f, 0.22f), 18f, 4f);
            AddBits(1, debris, new Color(0.45f, 0.32f, 0.28f, 0.45f), 10f, 8f);
            AddBits(2, ash, new Color(0.7f, 0.65f, 0.6f, 0.35f), 6f, 5f);
            kindsSeen.Add("wind");
            kindsSeen.Add("debris");
            kindsSeen.Add("ash");
            ActiveBits = bits.Count;

            // Foreground chain silhouettes (alpha ≤ 0.6) along the sides.
            for (int i = 0; i < 2; i++)
            {
                var chain = MakeImage("Chain" + i, new Color(0.08f, 0.05f, 0.06f, 0.45f));
                var rt = chain.rectTransform;
                rt.anchorMin = new Vector2(i == 0 ? 0.02f : 0.92f, 0.05f);
                rt.anchorMax = new Vector2(i == 0 ? 0.08f : 0.98f, 0.95f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                ForegroundLayers++;
            }

            for (int i = 0; i < events.Length; i++)
            {
                var img = MakeImage("DebrisEvent" + i, new Color(0.5f, 0.35f, 0.28f, 0.55f));
                img.gameObject.SetActive(false);
                events[i] = new EventFx { Rect = img.rectTransform, Image = img };
            }
            nextDebris = 2.5f + Random.value * 3f;
            clock = 0f;
        }

        void AddBits(int kind, int count, Color color, float speed, float size)
        {
            for (int i = 0; i < count; i++)
            {
                var img = MakeImage(kind == 0 ? "Wind" : kind == 1 ? "Debris" : "Ash", color);
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(kind == 0 ? size * 3f : size, kind == 0 ? size * 0.35f : size);
                bits.Add(new Bit
                {
                    Rect = rt,
                    Image = img,
                    U = Random.value,
                    V = Random.value,
                    Speed = speed * (0.7f + Random.value * 0.6f),
                    Size = size,
                    Phase = Random.value * 6.28f,
                    Kind = kind,
                });
            }
        }

        Image MakeImage(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            // Shaped weather bits (Gen Resources/Ambience when present, else procedural Soft shapes) beat a flat white square.
            if (name.StartsWith("Debris") || name == "Debris")
                img.sprite = AmbienceSprites.Bit(SickOfIt.Gameplay.Content.WeatherKind.Debris);
            else if (name == "Ash")
                img.sprite = AmbienceSprites.Bit(SickOfIt.Gameplay.Content.WeatherKind.Ash);
            else if (name == "Wind")
                img.sprite = AmbienceSprites.Bit(SickOfIt.Gameplay.Content.WeatherKind.Debris);
            else
                img.sprite = EntityFactory.WhiteSquare;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        void LateUpdate()
        {
            if (root == null)
                return;
            clock += Time.unscaledDeltaTime;
            float dt = Time.unscaledDeltaTime;
            var parent = root.parent as RectTransform;
            float w = parent != null ? parent.rect.width : 400f;
            float h = parent != null ? parent.rect.height : 700f;

            for (int i = 0; i < bits.Count; i++)
            {
                var b = bits[i];
                float x, y;
                if (b.Kind == 0)
                {
                    // Wind streaks rush downward past the view.
                    x = (b.U - 0.5f) * w * 0.9f + Mathf.Sin(clock * 1.3f + b.Phase) * 8f;
                    y = Mathf.Repeat(b.V * h + clock * b.Speed, h + 40f) - h * 0.5f - 20f;
                    // Flip: rushing past = from top to bottom visually while climbing feels upward; fall downward.
                    y = -y;
                }
                else if (b.Kind == 1)
                {
                    x = (b.U - 0.5f) * w * 0.85f + Mathf.Sin(clock * 2f + b.Phase) * 12f;
                    y = h * 0.55f - Mathf.Repeat(b.V * (h + 80f) + clock * b.Speed, h + 80f);
                }
                else
                {
                    x = (b.U - 0.5f) * w * 0.9f + Mathf.Sin(clock * 0.8f + b.Phase) * 18f;
                    y = h * 0.55f - Mathf.Repeat(b.V * (h + 60f) + clock * b.Speed * 0.45f, h + 60f);
                }
                b.Rect.anchoredPosition = new Vector2(x, y);
            }

            nextDebris -= dt;
            if (nextDebris <= 0f)
            {
                FireDebris(w, h);
                nextDebris = 10f + Random.value * 5f; // every 10-15 s
            }

            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                if (!e.Active)
                    continue;
                e.Age += dt;
                if (e.Age >= e.Duration)
                {
                    e.Active = false;
                    e.Image.gameObject.SetActive(false);
                    events[i] = e;
                    continue;
                }
                float u = e.Age / e.Duration;
                e.Rect.anchoredPosition = new Vector2(e.X, h * 0.55f - u * (h + 60f));
                var c = e.Image.color;
                c.a = 0.55f * (1f - u);
                e.Image.color = c;
                events[i] = e;
            }
        }

        void FireDebris(float w, float h)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Active)
                    continue;
                var e = events[i];
                e.Active = true;
                e.Age = 0f;
                e.Duration = 1.6f;
                e.X = (Random.value - 0.5f) * w * 0.7f;
                e.Image.gameObject.SetActive(true);
                e.Rect.sizeDelta = new Vector2(14f + Random.value * 10f, 10f + Random.value * 8f);
                events[i] = e;
                eventFirings++;
                Sfx.Play("amb_chain_rattle", 0.45f);
                return;
            }
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }
    }
}
