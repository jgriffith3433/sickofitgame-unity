using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Data.Config;

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

        Image cover, band, barFill;
        TextMeshProUGUI label, loadingText;
        CanvasGroup labelGroup, loadingGroup;
        float alpha, from, target, startAt, duration = 0.01f;
        float bandAt = -99f;
        // Assets coming in behind the cover (GameAssets): a bar and a percentage, and the cover stays shut while they move.
        float loadingAt = -99f, loadingShown, loadingProgress;

        /// <summary>The last loading progress shown (0..1; the playtest reads it).</summary>
        public static float LoadingProgress => instance != null ? instance.loadingProgress : 0f;

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

            var loading = MenuGraphics.NewRect("Loading", transform);
            MenuGraphics.Place(loading, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(640f, 80f));
            loadingGroup = loading.gameObject.AddComponent<CanvasGroup>();
            loadingGroup.alpha = 0f;
            loadingGroup.blocksRaycasts = false;
            var track = MenuGraphics.NewImage(loading, "Track", new Color(1f, 0.93f, 0.8f, 0.18f));
            track.raycastTarget = false;
            MenuGraphics.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(640f, 12f));
            barFill = MenuGraphics.NewImage(track.rectTransform, "Fill", new Color(1f, 0.5f, 0.15f, 1f));
            barFill.raycastTarget = false;
            var fill = barFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            loadingText = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            loadingText.transform.SetParent(loading, false);
            MenuGraphics.Place(loadingText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(640f, 44f));
            loadingText.alignment = TextAlignmentOptions.Midline;
            loadingText.fontSize = 34f;
            loadingText.fontStyle = FontStyles.Bold;
            loadingText.color = new Color(1f, 0.93f, 0.8f, 0.85f);
            loadingText.raycastTarget = false;
        }

        /// <summary>
        /// Something is loading behind the cover (a floor's art and sound: GameAssets): closes the cover if it is open and
        /// shows a bar at <paramref name="progress"/> (0..1) with the percentage. Call it while loading; the bar goes once
        /// the calls stop, and the cover doesn't open by itself while they come.
        /// </summary>
        public static void Loading(float progress)
        {
            var t = Instance();
            if (t.target < 0.5f)
                Cover();
            t.loadingAt = Time.unscaledTime;
            t.loadingProgress = Mathf.Clamp01(progress);
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
            bool loading = now - loadingAt < 0.75f;
            if (target > 0.5f && now - Mathf.Max(startAt, loadingAt) > 8f && !loading)
                Go(0f, RevealSeconds);
            float t = Mathf.Clamp01((now - startAt) / duration);
            alpha = Mathf.Lerp(from, target, t * t * (3f - 2f * t));
            cover.color = new Color(0.04f, 0.02f, 0.05f, alpha);
            // The label shows while the screen is mostly black, and goes with it.
            labelGroup.alpha = Mathf.Clamp01((alpha - 0.5f) * 2f);
            // The bar eases toward the progress (bundles arrive in steps) and fades in after a moment (a quick load shows none).
            loadingShown = Mathf.MoveTowards(loadingShown, loadingProgress, Time.unscaledDeltaTime * 1.5f);
            barFill.rectTransform.anchorMax = new Vector2(loadingShown, 1f);
            loadingText.text = $"LOADING  {Mathf.RoundToInt(loadingShown * 100f)}%";
            float wantLoading = loading && now - startAt > 0.3f ? labelGroup.alpha : 0f;
            loadingGroup.alpha = Mathf.MoveTowards(loadingGroup.alpha, wantLoading, Time.unscaledDeltaTime * 4f);
            if (!loading && loadingGroup.alpha <= 0f)
                loadingShown = loadingProgress = 0f;

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
    /// Short-lived words that rise from a HUD element and fade ("+3", "CLEAN KILL +0.5s"): pickup and bonus feedback.
    /// Pooled by recycling, stacked into separate slots so successive popups never overlap, and backed by a semi-opaque
    /// dark plum pill with a warm-grey rim so they read on bright sky and black slag alike.
    /// </summary>
    public sealed class HudFloaters : MonoBehaviour
    {
        const int MaxAlive = 8;
        public const float Life = 1.5f;
        /// <summary>Full opacity for at least 0.6 s before fading; the scale-pop eases from larger to normal over PopSeconds.</summary>
        public const float HoldSeconds = 0.9f;
        public const float PopSeconds = 0.3f;
        const float PeakPop = 1.32f;
        const float PillAlpha = 0.85f;
        const float PeakPillAlpha = 0.92f;

        // Warm-grey / dark-plum subtitle palette family (theme.json subtitle + Docs/Story.md beat warm-grey #C9B8A6).
        public static readonly Color PillInk = new Color(0.08f, 0.04f, 0.1f, 1f);
        public static readonly Color PillRimColor = new Color(0.788f, 0.722f, 0.651f, 1f);
        static readonly Color SubtitleCream = new Color(1f, 0.957f, 0.839f, 1f);

        static HudFloaters current;

        /// <summary>How many floaters were spawned (the playtest reads it) and the last one's text.</summary>
        public static int Spawned { get; private set; }
        public static string Last { get; private set; } = "";
        /// <summary>Whether the last floater was spawned with a pill behind it, and the alpha that pill settles to during hold.</summary>
        public static bool LastHadPill { get; private set; }
        public static float LastPillAlpha => PillAlpha;
        /// <summary>The stacking slot of the last popup and the highest slot in use at once (the playtest checks that popups in a row never share one).</summary>
        public static int LastSlot { get; private set; }
        public static int MaxSlotUsed { get; private set; }
        /// <summary>Live scale and opacity of the most recently spawned floater (0 when recycled), read by the playtest.</summary>
        public static float LastCurrentScale { get; private set; }
        public static float LastCurrentAlpha { get; private set; }

        sealed class Floater
        {
            public RectTransform Root;
            public Image Rim;
            public Image Pill;
            public TextMeshProUGUI Text;
            public Vector3 Start;
            public Vector2 PillSize;
            public float At;
            public float Direction;
            public int Slot;
            public int SpawnIndex;
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
            current = floaters;
            return floaters;
        }

        void OnEnable() => current = this;

        void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        static float SrgbLuminance(Color c)
        {
            float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(Mathf.Clamp01(c.r)) + 0.7152f * Lin(Mathf.Clamp01(c.g)) + 0.0722f * Lin(Mathf.Clamp01(c.b));
        }

        static float WcagContrast(Color a, Color b)
        {
            float la = SrgbLuminance(a), lb = SrgbLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        /// <summary>
        /// Checks 'color' against the semi-opaque dark plum pill composited over the brightest sky (pure white) and
        /// darkest floor (pure black), lifting dark hues toward warm subtitle cream (#FFF4D6) so contrast is >= 5.5:1.
        /// </summary>
        public static Color EnsureReadableColor(Color color)
        {
            var c = new Color(color.r, color.g, color.b, 1f);
            var pillOnBrightSky = Color.Lerp(Color.white, PillInk, PillAlpha);
            const float minContrast = 5.5f;
            if (WcagContrast(c, pillOnBrightSky) >= minContrast)
                return c;
            for (int step = 1; step <= 20; step++)
            {
                var lifted = Color.Lerp(c, SubtitleCream, step / 20f);
                if (WcagContrast(lifted, pillOnBrightSky) >= minContrast)
                    return lifted;
            }
            return SubtitleCream;
        }

        public static void ApplyTextOutlineAndShadow(TMP_Text text)
        {
            if (text == null)
                return;
            text.outlineWidth = 0.32f;
            text.outlineColor = PillInk;
            var mat = text.fontMaterial;
            if (mat != null)
            {
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.88f));
                mat.SetFloat("_UnderlayOffsetX", 0.6f);
                mat.SetFloat("_UnderlayOffsetY", -0.6f);
                mat.SetFloat("_UnderlaySoftness", 0.15f);
            }
        }

        /// <summary>A floater clear of 'from', rising (or stepping down near the top edge) in its own stack slot.</summary>
        public void Spawn(RectTransform from, string value, Color color, float size = 46f)
        {
            if (from == null || !from.gameObject.activeInHierarchy || string.IsNullOrEmpty(value))
                return;
            current = this;
            if (alive.Count >= MaxAlive)
                Recycle(alive[0]);
            var f = spare.Count > 0 ? spare.Pop() : Make();
            f.Root.gameObject.SetActive(true);
            if (f.Text.isActiveAndEnabled && f.Text.outlineWidth < 0.3f)
                ApplyTextOutlineAndShadow(f.Text);
            f.Text.text = value;
            f.Text.fontSize = size;
            f.Text.ForceMeshUpdate();

            float padX = size * 0.48f, padY = size * 0.22f;
            var pillSize = new Vector2(f.Text.preferredWidth + padX * 2f, size * 1.12f + padY * 2f);
            f.PillSize = pillSize;
            f.Root.sizeDelta = pillSize;
            f.Rim.rectTransform.sizeDelta = pillSize + new Vector2(5f, 5f);
            f.Pill.rectTransform.sizeDelta = pillSize;
            f.Color = EnsureReadableColor(color);

            float scale = parent != null ? Mathf.Max(0.01f, parent.lossyScale.y) : 1f;
            Vector3 basePos = from.TransformPoint(from.rect.center);
            float marginX = (pillSize.x + 6f) * 0.5f * PeakPop * scale + 8f;
            if (Screen.width > marginX * 2f)
                basePos.x = Mathf.Clamp(basePos.x, marginX, Screen.width - marginX);

            // Top-row HUD chips (heart, diamonds) step downward so stacks stay on screen; bottom readouts rise upward.
            float dir = basePos.y > Screen.height * 0.72f ? -1f : 1f;
            float anchorHalfH = from.rect.height * 0.5f * scale;
            basePos.y += dir * (anchorHalfH + pillSize.y * 0.58f * scale + 6f);

            // Successive popups in the same horizontal column take the lowest free slot, so they never overlap.
            int slot = 0;
            while (SlotTaken(slot, basePos.x, pillSize.x, scale))
                slot++;
            f.Slot = slot;
            f.Direction = dir;
            LastSlot = slot;
            MaxSlotUsed = Mathf.Max(MaxSlotUsed, slot);

            float stepPx = (pillSize.y * 1.28f + 8f) * scale;
            f.Start = basePos + new Vector3(0f, dir * slot * stepPx, 0f);
            float marginY = (pillSize.y + 6f) * 0.5f * PeakPop * scale + 8f;
            if (Screen.height > marginY * 2f)
                f.Start.y = Mathf.Clamp(f.Start.y, marginY, Screen.height - marginY);

            f.At = Time.unscaledTime;
            Spawned++;
            f.SpawnIndex = Spawned;
            f.Root.position = f.Start;
            f.Root.localScale = new Vector3(PeakPop, PeakPop, 1f);
            var c = f.Color;
            c.a = 1f;
            f.Text.color = c;
            f.Pill.color = new Color(PillInk.r, PillInk.g, PillInk.b, PeakPillAlpha);
            f.Rim.color = new Color(PillRimColor.r, PillRimColor.g, PillRimColor.b, 0.42f);
            alive.Add(f);
            SeparateLive(scale);

            Last = value;
            LastCurrentScale = PeakPop;
            LastCurrentAlpha = 1f;
            LastHadPill = f.Pill != null && f.Pill.rectTransform.sizeDelta.x > f.Text.preferredWidth;
        }

        bool SlotTaken(int slot, float x, float widthRef, float scale)
        {
            for (int i = 0; i < alive.Count; i++)
            {
                var a = alive[i];
                if (a.Slot != slot)
                    continue;
                float minSepX = (a.PillSize.x + widthRef) * 0.55f * scale + 8f;
                if (Mathf.Abs(a.Start.x - x) < minSepX)
                    return true;
            }
            return false;
        }

        Floater Make()
        {
            var go = new GameObject("Floater", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(300f, 70f);

            var rim = new GameObject("Rim", typeof(RectTransform)).AddComponent<Image>();
            rim.transform.SetParent(root, false);
            rim.raycastTarget = false;
            MenuGraphics.Apply(rim, MenuShape.Pill, 0f);
            rim.color = Color.clear;

            var pill = new GameObject("Pill", typeof(RectTransform)).AddComponent<Image>();
            pill.transform.SetParent(root, false);
            pill.raycastTarget = false;
            MenuGraphics.Apply(pill, MenuShape.Pill, 0f);
            pill.color = Color.clear;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(root, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.alignment = TextAlignmentOptions.Midline;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            if (text.isActiveAndEnabled)
                ApplyTextOutlineAndShadow(text);
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            return new Floater { Root = root, Rim = rim, Pill = pill, Text = text };
        }

        void Recycle(Floater f)
        {
            alive.Remove(f);
            if (f.SpawnIndex == Spawned)
            {
                LastCurrentAlpha = 0f;
                LastCurrentScale = 0f;
            }
            f.Root.gameObject.SetActive(false);
            spare.Push(f);
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void Update()
        {
            float scale = parent != null ? Mathf.Max(0.01f, parent.lossyScale.y) : 1f;
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var f = alive[i];
                float age = Time.unscaledTime - f.At;
                if (age >= Life)
                {
                    Recycle(f);
                    continue;
                }
                float t = age / Life;
                float rise = 1f - (1f - t) * (1f - t);
                // Larger size for the first ~0.3 s: quick scale-pop to 1.32x, then smooth ease back to 1.0x.
                float pop = age < 0.05f
                    ? Mathf.Lerp(0.78f, PeakPop, age / 0.05f)
                    : age < PopSeconds
                        ? Mathf.Lerp(PeakPop, 1f, Smooth01((age - 0.05f) / (PopSeconds - 0.05f)))
                        : 1f;
                var rect = f.Root;
                rect.localScale = new Vector3(pop, pop, 1f);

                Vector3 pos = f.Start + new Vector3(0f, f.Direction * rise * 42f * scale, 0f);
                float halfW = (f.PillSize.x + 6f) * 0.5f * pop * scale + 6f;
                float halfH = (f.PillSize.y + 6f) * 0.5f * pop * scale + 6f;
                if (Screen.width > halfW * 2f)
                    pos.x = Mathf.Clamp(pos.x, halfW, Screen.width - halfW);
                if (Screen.height > halfH * 2f)
                    pos.y = Mathf.Clamp(pos.y, halfH, Screen.height - halfH);
                rect.position = pos;

                // Full opacity for HoldSeconds (0.9 s >= 0.6 s), then smooth fade.
                var c = f.Color;
                c.a = age <= HoldSeconds ? 1f : 1f - Smooth01((age - HoldSeconds) / (Life - HoldSeconds));
                f.Text.color = c;
                float pillA = age < PopSeconds ? Mathf.Lerp(PeakPillAlpha, PillAlpha, age / PopSeconds) : PillAlpha;
                f.Pill.color = new Color(PillInk.r, PillInk.g, PillInk.b, pillA * c.a);
                f.Rim.color = new Color(PillRimColor.r, PillRimColor.g, PillRimColor.b, 0.42f * c.a);

                if (f.SpawnIndex == Spawned)
                {
                    LastCurrentScale = pop;
                    LastCurrentAlpha = c.a;
                }
            }
            if (alive.Count > 1)
                SeparateLive(scale);
        }

        /// <summary>Keeps every pair of live popups separated in screen space even while rising at different ages.</summary>
        void SeparateLive(float scale)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < alive.Count; i++)
                {
                    var a = alive[i];
                    float aScale = a.Root.localScale.y;
                    float aHalfW = (a.PillSize.x + 6f) * 0.5f * aScale * scale;
                    float aHalfH = (a.PillSize.y + 6f) * 0.5f * aScale * scale;
                    Vector3 aPos = a.Root.position;
                    for (int j = i + 1; j < alive.Count; j++)
                    {
                        var b = alive[j];
                        float bScale = b.Root.localScale.y;
                        float bHalfW = (b.PillSize.x + 6f) * 0.5f * bScale * scale;
                        float bHalfH = (b.PillSize.y + 6f) * 0.5f * bScale * scale;
                        Vector3 bPos = b.Root.position;
                        if (Mathf.Abs(aPos.x - bPos.x) >= aHalfW + bHalfW + 4f)
                            continue;
                        float minGap = aHalfH + bHalfH + 6f * scale;
                        float dy = bPos.y - aPos.y;
                        if (Mathf.Abs(dy) >= minGap)
                            continue;
                        float pushDir = Mathf.Abs(dy) > 0.5f ? Mathf.Sign(dy) : b.Direction;
                        float nextY = aPos.y + pushDir * minGap;
                        if (nextY + bHalfH + 4f > Screen.height || nextY - bHalfH - 4f < 0f)
                            nextY = aPos.y - pushDir * minGap;
                        bPos.y = Mathf.Clamp(nextY, bHalfH + 4f, Mathf.Max(bHalfH + 4f, Screen.height - bHalfH - 4f));
                        b.Root.position = bPos;
                    }
                }
            }
        }

        static Rect PillScreenRect(Floater f)
        {
            var corners = new Vector3[4];
            f.Rim.rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                Mathf.Min(corners[0].x, corners[2].x),
                Mathf.Min(corners[0].y, corners[2].y),
                Mathf.Max(corners[0].x, corners[2].x),
                Mathf.Max(corners[0].y, corners[2].y));
        }

        /// <summary>Fills 'rects' with the screen-space bounds of every active popup (the playtest checks non-overlap).</summary>
        public static int GetActiveScreenRects(List<Rect> rects)
        {
            rects?.Clear();
            if (current == null)
                return 0;
            for (int i = 0; i < current.alive.Count; i++)
            {
                var f = current.alive[i];
                if (f.Root != null && f.Root.gameObject.activeInHierarchy)
                    rects?.Add(PillScreenRect(f));
            }
            return current.alive.Count;
        }

        /// <summary>Screen-space bounds and text colour of the most recently spawned live popup (for contrast capture checks).</summary>
        public static bool TryGetLastScreenRect(out Rect rect, out Color textColor)
        {
            rect = default;
            textColor = Color.clear;
            if (current == null || current.alive.Count == 0)
                return false;
            var f = current.alive[current.alive.Count - 1];
            if (f.Root == null || !f.Root.gameObject.activeInHierarchy)
                return false;
            rect = PillScreenRect(f);
            textColor = f.Text.color;
            return rect.width > 1f && rect.height > 1f;
        }
    }
}
