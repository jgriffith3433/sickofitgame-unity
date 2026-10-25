using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// A number that counts up (or down) to its target instead of jumping: diamonds, the results' stats, the shop's
    /// purse. Unscaled time, so it runs on a paused results screen. Presentation only.
    /// </summary>
    public sealed class NumberTicker : MonoBehaviour
    {
        /// <summary>Every ticker that changed its target, for the playtest (<c>Retargets</c>) and how many are mid-count.</summary>
        public static int Retargets { get; private set; }
        public static int Active { get; private set; }

        TextMeshProUGUI text;
        Func<int, string> format = v => v.ToString();
        float from, shown, target;
        float startAt, duration;
        bool counting;

        public int Target => Mathf.RoundToInt(target);
        public int Shown => Mathf.RoundToInt(shown);
        public bool Counting => counting;

        public static NumberTicker On(TextMeshProUGUI text, Func<int, string> format = null)
        {
            if (!text.TryGetComponent(out NumberTicker ticker))
                ticker = text.gameObject.AddComponent<NumberTicker>();
            ticker.text = text;
            if (format != null)
                ticker.format = format;
            return ticker;
        }

        /// <summary>Jumps to 'value' with no count (the first value shown, or after a rewind).</summary>
        public void Snap(int value)
        {
            Stop();
            from = shown = target = value;
            Draw();
        }

        /// <summary>Counts from what is shown to 'value' over 'seconds' (longer jumps take a little longer, up to 'seconds' x 2), after 'delay'.</summary>
        public void CountTo(int value, float seconds = 0.6f, float delay = 0f)
        {
            if (Mathf.Approximately(value, target) && !counting)
                return;
            Retargets++;
            from = shown;
            target = value;
            duration = Mathf.Max(0.05f, seconds * Mathf.Lerp(1f, 2f, Mathf.Clamp01(Mathf.Abs(target - from) / 200f)));
            startAt = Time.unscaledTime + delay;
            if (!counting)
                Active++;
            counting = true;
        }

        void Update()
        {
            if (!counting)
                return;
            float t = Mathf.Clamp01((Time.unscaledTime - startAt) / duration);
            float eased = 1f - (1f - t) * (1f - t);
            shown = Mathf.Lerp(from, target, eased);
            if (t >= 1f)
            {
                shown = target;
                Stop();
            }
            Draw();
        }

        void Stop()
        {
            if (counting)
                Active = Mathf.Max(0, Active - 1);
            counting = false;
        }

        void OnDisable() => Stop();

        int drawn = int.MinValue;

        void Draw()
        {
            int value = Mathf.RoundToInt(shown);
            if (value == drawn || text == null)
                return;
            drawn = value;
            MenuGraphics.SetText(text, format(value));
        }
    }

    /// <summary>
    /// Opens a popup (pause, results, Codex, a picker) with a fade and a small rise, and closes it with a quick
    /// fade instead of vanishing. Unscaled time: these screens stop the game.
    /// </summary>
    public sealed class PanelAnim : MonoBehaviour
    {
        public const float OpenSeconds = 0.28f;
        public const float CloseSeconds = 0.16f;

        /// <summary>Counts for the playtest: how many panels opened / closed with the animation, and which one last.</summary>
        public static int Opened { get; private set; }
        public static int Closed { get; private set; }
        public static string LastOpened { get; private set; } = "";
        public static string LastClosed { get; private set; } = "";
        /// <summary>Panels still opening or closing (a layout audit waits for 0).</summary>
        public static int Animating { get; private set; }

        CanvasGroup group;
        RectTransform inner;
        float startedAt;
        float closeAt = -1f;
        bool openDone;

        public static PanelAnim Add(GameObject root, RectTransform inner)
        {
            var anim = root.AddComponent<PanelAnim>();
            anim.inner = inner;
            return anim;
        }

        void Awake()
        {
            if (!TryGetComponent(out group))
                group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            startedAt = Time.unscaledTime;
            openDone = false;
            Opened++;
            Animating++;
            LastOpened = name;
            Apply(0f);
        }

        void OnDisable()
        {
            if (!openDone)
                Animating = Mathf.Max(0, Animating - 1);
            if (closeAt >= 0f)
                Animating = Mathf.Max(0, Animating - 1);
        }

        /// <summary>Fades the panel out, then destroys it. Clicks stop reaching it at once.</summary>
        public static void CloseAndDestroy(GameObject root, float seconds = CloseSeconds)
        {
            if (root == null)
                return;
            if (!root.TryGetComponent(out PanelAnim anim) || !root.activeInHierarchy)
            {
                UnityEngine.Object.Destroy(root);
                return;
            }
            anim.BeginClose(seconds);
        }

        float closeSeconds;

        void BeginClose(float seconds)
        {
            if (closeAt >= 0f)
                return;
            closeSeconds = Mathf.Max(0.02f, seconds);
            closeAt = Time.unscaledTime;
            if (openDone)
                Animating++;
            group.blocksRaycasts = false;
            group.interactable = false;
            Closed++;
            LastClosed = name;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (closeAt >= 0f)
            {
                float c = Mathf.Clamp01((now - closeAt) / closeSeconds);
                Apply(1f - c);
                if (c >= 1f)
                {
                    Animating = Mathf.Max(0, Animating - 1);
                    closeAt = -1f;
                    openDone = true;
                    Destroy(gameObject);
                }
                return;
            }
            if (openDone)
                return;
            float t = Mathf.Clamp01((now - startedAt) / OpenSeconds);
            Apply(t);
            if (t >= 1f)
            {
                openDone = true;
                Animating = Mathf.Max(0, Animating - 1);
            }
        }

        void Apply(float t)
        {
            float smooth = 1f - (1f - t) * (1f - t) * (1f - t);
            group.alpha = smooth;
            if (inner != null)
            {
                float s = Mathf.Lerp(0.94f, 1f, smooth);
                inner.localScale = new Vector3(s, s, 1f);
            }
        }
    }

    /// <summary>
    /// The screen transition between floors: a black cover that closes before one floor ends and opens on the next
    /// (a lava-lit wipe with the floor's number on it), kept across scene loads. A new canvas that finds the screen
    /// still covered opens it, so no scene can leave the player on black. Presentation only.
    /// </summary>
    public sealed class ScreenTransition : MonoBehaviour
    {
        public const float CoverSeconds = 0.35f;
        public const float RevealSeconds = 0.55f;

        static ScreenTransition instance;

        /// <summary>The cover is closed (or closing) right now.</summary>
        public static bool Covered => instance != null && instance.target > 0.5f;
        /// <summary>How black the screen is, 0 to 1 (the playtest reads it).</summary>
        public static float Alpha => instance != null ? instance.alpha : 0f;
        public static int Covers { get; private set; }
        public static int Sweeps { get; private set; }
        public static int Reveals { get; private set; }
        public static string LastLabel { get; private set; } = "";

        Image cover, band;
        TextMeshProUGUI label;
        CanvasGroup labelGroup;
        float alpha, from, target, startAt, duration = 0.01f;
        float bandAt = -99f;

        static ScreenTransition Instance()
        {
            if (instance != null)
                return instance;
            var go = new GameObject("ScreenTransition", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            instance = go.AddComponent<ScreenTransition>();
            instance.Build();
            // A scene that finishes loading opens the screen the previous floor closed.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Reveal();
            return instance;
        }

        void Build()
        {
            cover = MenuGraphics.NewImage(transform, "Cover", new Color(0.04f, 0.02f, 0.05f, 0f));
            cover.raycastTarget = false;
            band = MenuGraphics.NewImage(transform, "Band", new Color(1f, 0.45f, 0.12f, 0f));
            band.raycastTarget = false;
            var slot = MenuGraphics.NewRect("Label", transform);
            MenuGraphics.Place(slot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 160f));
            labelGroup = slot.gameObject.AddComponent<CanvasGroup>();
            labelGroup.alpha = 0f;
            labelGroup.blocksRaycasts = false;
            label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(slot, false);
            MenuGraphics.Fill(label.rectTransform);
            label.alignment = TextAlignmentOptions.Midline;
            label.fontSize = 96f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(1f, 0.93f, 0.8f, 1f);
            label.raycastTarget = false;
        }

        /// <summary>Closes the cover over 'seconds' (black, with 'text' in the middle if given) and keeps it closed until <see cref="Reveal"/>.</summary>
        public static void Cover(string text = "", float seconds = CoverSeconds)
        {
            var t = Instance();
            if (t.target > 0.5f && string.IsNullOrEmpty(text))
                return;
            Covers++;
            LastLabel = text ?? "";
            t.label.text = text ?? "";
            t.Go(1f, seconds);
        }

        /// <summary>Opens the cover over 'seconds', if it is closed.</summary>
        public static void Reveal(float seconds = RevealSeconds)
        {
            if (instance == null || instance.target < 0.5f)
                return;
            Reveals++;
            instance.Go(0f, seconds);
        }

        /// <summary>A fast sweep of lava light up the screen, over the game: the floor changed. Does not block anything.</summary>
        public static void Sweep()
        {
            var t = Instance();
            t.bandAt = Time.unscaledTime;
            Sweeps++;
        }

        void Go(float to, float seconds)
        {
            from = alpha;
            target = to;
            startAt = Time.unscaledTime;
            duration = Mathf.Max(0.01f, seconds);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            // Never leave the player on black: a scene that never opened the cover gets it opened.
            if (target > 0.5f && now - startAt > 8f)
                Go(0f, RevealSeconds);
            float t = Mathf.Clamp01((now - startAt) / duration);
            alpha = Mathf.Lerp(from, target, t * t * (3f - 2f * t));
            cover.color = new Color(0.04f, 0.02f, 0.05f, alpha);
            // The label shows while the screen is mostly black, and goes with it.
            labelGroup.alpha = Mathf.Clamp01((alpha - 0.5f) * 2f);

            float age = now - bandAt;
            const float sweepSeconds = 0.5f;
            if (age >= 0f && age < sweepSeconds)
            {
                float p = age / sweepSeconds;
                var rect = band.rectTransform;
                // A horizontal bar of light travelling from the bottom of the screen to the top.
                rect.anchorMin = new Vector2(0f, Mathf.Clamp01(p * 1.3f - 0.3f));
                rect.anchorMax = new Vector2(1f, Mathf.Clamp01(p * 1.3f));
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                band.color = new Color(1f, 0.5f, 0.15f, 0.42f * Mathf.Sin(p * Mathf.PI));
            }
            else if (band.color.a > 0f)
            {
                band.color = new Color(1f, 0.5f, 0.15f, 0f);
            }
        }
    }

    /// <summary>
    /// Short-lived words that rise from a HUD element and fade ("+3", "+1.5s"): pickup feedback. Pooled by
    /// recycling, never more than a handful alive.
    /// </summary>
    public sealed class HudFloaters : MonoBehaviour
    {
        const int MaxAlive = 6;
        const float Life = 0.9f;

        /// <summary>How many floaters were spawned (the playtest reads it) and the last one's text.</summary>
        public static int Spawned { get; private set; }
        public static string Last { get; private set; } = "";

        sealed class Floater
        {
            public TextMeshProUGUI Text;
            public Vector3 Start;
            public float At;
            public Color Color;
        }

        readonly List<Floater> alive = new List<Floater>();
        readonly Stack<Floater> spare = new Stack<Floater>();
        Transform parent;
        TMP_FontAsset font;

        public static HudFloaters Create(Transform parent, TMP_FontAsset font)
        {
            var floaters = parent.gameObject.AddComponent<HudFloaters>();
            floaters.parent = parent;
            floaters.font = font;
            return floaters;
        }

        /// <summary>A floater at 'from's middle, rising from it.</summary>
        public void Spawn(RectTransform from, string value, Color color, float size = 46f)
        {
            if (from == null || !from.gameObject.activeInHierarchy)
                return;
            if (alive.Count >= MaxAlive)
                Recycle(alive[0]);
            var f = spare.Count > 0 ? spare.Pop() : Make();
            f.Text.gameObject.SetActive(true);
            f.Text.text = value;
            f.Text.fontSize = size;
            f.Color = color;
            f.Start = from.TransformPoint(from.rect.center);
            f.At = Time.unscaledTime;
            f.Text.rectTransform.position = f.Start;
            alive.Add(f);
            Spawned++;
            Last = value;
        }

        Floater Make()
        {
            var go = new GameObject("Floater", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.alignment = TextAlignmentOptions.Midline;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color(0.12f, 0.07f, 0.13f, 1f);
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(300f, 70f);
            return new Floater { Text = text };
        }

        void Recycle(Floater f)
        {
            alive.Remove(f);
            f.Text.gameObject.SetActive(false);
            spare.Push(f);
        }

        void Update()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var f = alive[i];
                float t = (Time.unscaledTime - f.At) / Life;
                if (t >= 1f)
                {
                    Recycle(f);
                    continue;
                }
                float rise = 1f - (1f - t) * (1f - t);
                float pop = t < 0.2f ? Mathf.Lerp(0.6f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 1f, (t - 0.2f) / 0.8f);
                var rect = f.Text.rectTransform;
                rect.position = f.Start + new Vector3(0f, rise * 70f * rect.lossyScale.y * 1f, 0f);
                rect.localScale = new Vector3(pop, pop, 1f);
                var c = f.Color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                f.Text.color = c;
            }
        }
    }
}
