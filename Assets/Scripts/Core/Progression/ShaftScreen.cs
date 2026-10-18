using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SickOfIt.Core.Audio;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// The Shaft, Hell's map (Docs/Story.md, "The Shaft"): a tall cross-section with the Meadow (FLOOR 0) at the top,
    /// the 10 Circles stacked below it in their biome colours and FLOOR 1,000 at the bottom. <see cref="ShaftHook"/>
    /// brings it up after the plunge video (the fall from the Meadow to the bottom, the slam, the announcer's welcome)
    /// and after every level with a floor range (the hero's marker climbs to the level's last floor). Continue goes on.
    /// Built at runtime over the scene that just ended (like every menu), from ui/shaft.json and theme.json texts;
    /// shapes are painted placeholders until the Shaft art replaces them. Presentation only: it reads progress and
    /// remembers where the marker was (<see cref="ShaftProgress"/>), never touching the game itself.
    /// </summary>
    public sealed class ShaftScreen : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>From the Meadow down the whole Shaft to the bottom (after the plunge video).</summary>
            Plunge,
            /// <summary>The marker climbs from where it was to the level's last floor.</summary>
            Climb,
            /// <summary>No climb: Gloob says hello where the marker is (after the video he turns up in, Video3).</summary>
            Meet,
        }

        /// <summary>The Shaft on screen now, or null.</summary>
        public static ShaftScreen Current { get; private set; }

        /// <summary>What the Shaft did, for the playtest and the log: (kind, detail), e.g. ("band", "...") or ("climb", "from 1000 to 991").</summary>
        public static event Action<string, string> Reported;

        public Mode Kind { get; private set; }
        public int FromFloor { get; private set; }
        public int ToFloor { get; private set; }
        /// <summary>The floor on the big counter right now.</summary>
        public int ShownFloor { get; private set; }
        /// <summary>The animation is over and Continue is up.</summary>
        public bool Ready { get; private set; }
        /// <summary>Taps and keys skip the animation (every Shaft after the first one).</summary>
        public bool Skippable { get; private set; }
        /// <summary>Circle bands the plunge fell past.</summary>
        public int BandsPassed { get; private set; }
        public int BandCount => bands.Count;
        public string FloorsLeftText => floorsLeft != null ? floorsLeft.text : "";
        public string NextWardenText => nextWarden != null ? nextWarden.text : "";
        public string CircleText => circleLine != null ? circleLine.text : "";
        public string SubtitleText => subtitle != null && subtitle.gameObject.activeInHierarchy ? subtitle.text : "";
        /// <summary>Circle bands drawn with their painted art (the rest are colour placeholders).</summary>
        public int PaintedBands { get; private set; }
        /// <summary>Wardens drawn from their art: as a dark silhouette (not met yet) and in colour (met).</summary>
        public int WardenSilhouettes { get; private set; }
        public int WardenPortraits { get; private set; }
        /// <summary>The Meadow and the hero's marker are drawn from their art.</summary>
        public bool PaintedMeadow { get; private set; }
        public bool PaintedMarker { get; private set; }
        /// <summary>Continue was pressed: the next scene is loading under it.</summary>
        public bool Continued => continued;
        /// <summary>A preview (playtest setup shortcut): Continue only closes it; nothing is saved.</summary>
        public bool IsPreview => preview;
        /// <summary>Gloob's portrait on this Shaft, or null (before he has met the hero).</summary>
        public GloobPortrait Gloob => gloob;

        /// <summary>One Circle as drawn: ui/shaft.json, with content/circles' name and floors where the circle exists.</summary>
        sealed class Band
        {
            public string Numeral;
            public string Name;
            public int First;
            public int Last;
            public Color Color;
            public string Warden;
            public Sprite Art;
            public Sprite WardenArt;
            public Image Flash;
        }

        ShaftConfig config;
        MenuTexts texts;
        MenuCanvas menu;
        RectTransform stage;
        RectTransform viewport;
        RectTransform content;
        RectTransform markerRect;
        Image markerGlow;
        Image flash;
        TextMeshProUGUI counter;
        MenuMotionDriver counterMotion;
        TextMeshProUGUI circleLine;
        TextMeshProUGUI floorsLeft;
        TextMeshProUGUI nextWarden;
        TextMeshProUGUI subtitle;
        TextMeshProUGUI skipHint;
        MenuButton continueButton;
        GloobPortrait gloob;
        readonly List<Band> bands = new List<Band>();
        readonly List<Behaviour> hidden = new List<Behaviour>();
        Action next;
        bool preview;
        bool skip;
        bool continued;
        float openedAt;
        float cameraFloor;
        float markerFloor;
        float contentHeight;
        Vector2 shake;

        // ─── The hook ─────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() => ShaftHook.Show = Show;

        /// <summary>
        /// ShaftHook's handler: the plunge after the plunge video when a level comes next, the climb after a level
        /// that has a floor range. Anything else (the Meadow, the arcade levels, other videos) goes straight on.
        /// </summary>
        static bool Show(ShaftStop stop, Action next)
        {
            var definition = GameDefinition.Instance;
            var config = definition != null ? definition.shaft : null;
            if (config == null || !config.enabled)
                return false;

            if (stop.Kind == ShaftStopKind.AfterVideo)
            {
                // Gloob's hello, the first time the video he turns up in leads on to a level.
                var gloobConfig = definition.gloob;
                if (gloobConfig != null && gloobConfig.enabled && stop.Video == gloobConfig.helloAfterVideo && stop.NextLevel > 0 && !GloobProgress.Met)
                {
                    int here = ShaftProgress.Floor > 0 ? ShaftProgress.Floor : config.bottomFloor;
                    Open(Mode.Meet, here, here, next, false);
                    return true;
                }
                if (stop.Video != config.plungeAfterVideo || stop.NextLevel <= 0)
                    return false;
                Open(Mode.Plunge, 0, config.bottomFloor, next, false);
                return true;
            }

            LevelFloors(stop.Level, out int start, out int end);
            if (end <= 0)
                return false;
            // From where the marker was, if that is below this level's end and inside its range; else the level's start.
            int saved = ShaftProgress.Floor;
            int from = saved > end && saved <= Mathf.Max(start, end) ? saved : start > end ? start : end;
            Open(Mode.Climb, from, end, next, false);
            return true;
        }

        /// <summary>
        /// Shows the Shaft over the current screen without the story flow (playtest setup shortcut for the menus and
        /// layout checks): from 0 it plunges, otherwise the marker climbs. Continue closes it; nothing is saved.
        /// </summary>
        public static ShaftScreen Preview(int fromFloor, int toFloor, Action closed = null)
        {
            return Open(fromFloor <= 0 ? Mode.Plunge : Mode.Climb, Mathf.Max(0, fromFloor), toFloor, closed, true);
        }

        /// <summary>Gloob's hello at 'floor' without the story flow (playtest setup shortcut). Continue closes it; nothing is saved.</summary>
        public static ShaftScreen PreviewMeet(int floor, Action closed = null)
        {
            return Open(Mode.Meet, floor, floor, closed, true);
        }

        static ShaftScreen Open(Mode mode, int from, int to, Action next, bool preview)
        {
            if (Current != null)
                Current.Close();
            var definition = GameDefinition.Instance;
            var layout = new MenuLayoutConfig();
            layout.title.show = false;
            layout.subtitle.show = false;
            layout.backdrop.tint = definition.shaft.backdrop;
            layout.backdrop.vignette = new Color(0f, 0f, 0f, 0.6f);
            var menu = MenuCanvas.Create("ShaftUI", definition.menuTheme, layout, null);
            menu.GetComponent<Canvas>().sortingOrder = 60;
            // It stays up until the next scene has loaded, then goes (Continue starts that load).
            DontDestroyOnLoad(menu.gameObject);
            var screen = menu.gameObject.AddComponent<ShaftScreen>();
            screen.Begin(menu, definition, mode, from, to, next, preview);
            return screen;
        }

        // ─── Building ─────────────────────────────────────────────────

        void Begin(MenuCanvas canvas, GameDefinition definition, Mode mode, int from, int to, Action onContinue, bool isPreview)
        {
            Current = this;
            menu = canvas;
            config = definition.shaft;
            texts = definition.menuTheme.texts ?? new MenuTexts();
            Kind = mode;
            FromFloor = from;
            ToFloor = to;
            next = onContinue;
            preview = isPreview;
            Skippable = ShaftProgress.Plunged;
            openedAt = Time.unscaledTime;
            menu.Back = null;
            // A scene that replaces the current one (Continue's, or any other) takes the Shaft with it.
            SceneManager.sceneLoaded += OnSceneLoaded;

            HideTheRest();
            ResolveBands();
            Build();
            cameraFloor = markerFloor = from;
            SetCounter(from);
            UpdateInfo(from);
            Report("open", $"{mode.ToString().ToLowerInvariant()} from {from} to {to}" + (preview ? " (preview)" : "") + (Skippable ? ", skippable" : ""));
            StartCoroutine(mode == Mode.Plunge ? PlungeRoutine() : mode == Mode.Meet ? MeetRoutine() : ClimbRoutine());
        }

        /// <summary>
        /// The screen that ended (results, the video's buttons, the HUD) goes quiet under the Shaft: its canvases are
        /// switched off so nothing under it can be pressed, and the level's own keys (pause) stop listening.
        /// The story flow leaves that scene anyway; a preview puts them back when it closes.
        /// </summary>
        void HideTheRest()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.enabled || !canvas.isRootCanvas || canvas.gameObject == gameObject)
                    continue;
                canvas.enabled = false;
                hidden.Add(canvas);
            }
            foreach (var other in FindObjectsByType<MenuCanvas>(FindObjectsSortMode.None))
            {
                if (other == null || other == menu || !other.enabled)
                    continue;
                other.enabled = false;
                hidden.Add(other);
            }
            foreach (var hud in FindObjectsByType<GameplayHud>(FindObjectsSortMode.None))
            {
                if (hud == null || !hud.enabled)
                    continue;
                hud.enabled = false;
                hidden.Add(hud);
            }
            if (preview)
                return;
            // The video's soundtrack or the level's music would play on under the map. Sounds and voices (Sfx) live
            // in DontDestroyOnLoad, like this screen, and keep playing.
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source != null && source.isPlaying && source.gameObject.scene != gameObject.scene)
                    source.Stop();
            }
        }

        void ResolveBands()
        {
            bands.Clear();
            Dictionary<string, SickOfIt.Gameplay.Content.CircleDef> circles = null;
            try
            {
                circles = GameContent.Current != null ? GameContent.Current.Circles : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Shaft] Couldn't read content/circles: " + e.Message);
            }
            foreach (var band in config.bands)
            {
                if (band == null)
                    continue;
                var view = new Band
                {
                    Numeral = band.numeral,
                    Name = band.name,
                    First = band.firstFloor,
                    Last = band.lastFloor,
                    Color = band.color,
                    Warden = band.warden,
                    Art = band.art,
                    WardenArt = band.wardenArt,
                };
                if (!string.IsNullOrEmpty(band.circle) && circles != null && circles.TryGetValue(band.circle, out var circle) && circle != null)
                {
                    if (!string.IsNullOrEmpty(circle.displayName))
                        view.Name = circle.displayName;
                    view.First = Mathf.Max(circle.firstFloor, circle.lastFloor);
                    view.Last = Mathf.Min(circle.firstFloor, circle.lastFloor);
                }
                bands.Add(view);
            }
        }

        float Ppf => Mathf.Max(1f, config.pixelsPerFloor);
        float Meadow => Mathf.Max(120f, config.meadowHeight);
        int Bottom => Mathf.Max(1, config.bottomFloor);

        /// <summary>Distance from the top of the shaft to a floor (floor 0 is the Meadow's ground).</summary>
        float Y(float floor) => Meadow + floor * Ppf;

        void Build()
        {
            var theme = menu.Theme;
            stage = MenuGraphics.NewRect("Stage", menu.Root);
            MenuGraphics.Fill(stage);

            // The shaft window: a panel that clips the very tall shaft inside it.
            viewport = MenuGraphics.NewRect("Shaft", stage);
            MenuGraphics.Place(viewport, config.shaft.anchor, config.shaft.offset, config.shaft.size);
            MenuResponsive.Track(viewport, config.shaft, config.shaftPortrait, MenuElementKind.Panel, menu.Responsive);
            var frame = MenuGraphics.NewImage(viewport, "Frame", theme.title.outlineColor.a > 0f ? theme.title.outlineColor : Color.black);
            MenuGraphics.Apply(frame, MenuShape.Rounded, 24f);
            frame.rectTransform.offsetMin = new Vector2(-10f, -10f);
            frame.rectTransform.offsetMax = new Vector2(10f, 10f);
            var window = MenuGraphics.NewRect("Window", viewport);
            MenuGraphics.Fill(window);
            window.gameObject.AddComponent<RectMask2D>();
            MenuGraphics.NewImage(window, "Dark", config.wall);

            content = MenuGraphics.NewRect("Content", window);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            contentHeight = Y(Bottom) + 160f;
            content.sizeDelta = new Vector2(0f, contentHeight);

            BuildMeadow();
            foreach (var band in bands)
                BuildBand(band);
            BuildBottom();
            BuildWalls();
            BuildTicks();
            BuildHints();
            foreach (var band in bands)
                BuildWarden(band);
            BuildMarker();
            Report("art", $"{PaintedBands} of {bands.Count} Circle bands painted, meadow {(PaintedMeadow ? "painted" : "placeholder")}, "
                          + $"Wardens: {WardenSilhouettes} dark silhouettes, {WardenPortraits} met, {bands.Count - WardenSilhouettes - WardenPortraits} placeholder; "
                          + $"hero marker {(PaintedMarker ? "painted" : "placeholder")}");

            // The readouts beside (landscape) or under (portrait) the shaft.
            counter = Line("Counter", "", theme.title, 120f, config.counter, config.counterPortrait, out counterMotion);
            circleLine = Line("Circle", "", theme.heading, 46f, config.circleLine, config.circleLinePortrait, out _);
            floorsLeft = Line("FloorsLeft", "", theme.subtitle, 42f, config.floorsLeft, config.floorsLeftPortrait, out _);
            nextWarden = Line("NextWarden", "", theme.subtitle, 42f, config.nextWarden, config.nextWardenPortrait, out _);
            nextWarden.enableVertexGradient = false;
            nextWarden.color = theme.accent;
            subtitle = Line("Subtitle", "", theme.subtitle, 40f, config.subtitle, config.subtitlePortrait, out _);
            subtitle.fontStyle &= ~FontStyles.UpperCase;
            subtitle.transform.parent.gameObject.SetActive(false);
            skipHint = Line("SkipHint", texts.shaftSkipHint, theme.caption, 26f,
                MenuPlacement.At(config.continueButton.anchor.x, config.continueButton.anchor.y, config.continueButton.offset.x, config.continueButton.offset.y, 600f, 50f),
                MenuPlacement.At(config.continueButtonPortrait.anchor.x, config.continueButtonPortrait.anchor.y, config.continueButtonPortrait.offset.x, config.continueButtonPortrait.offset.y, 900f, 50f),
                out _);
            skipHint.enableVertexGradient = false;
            skipHint.color = theme.muted;
            skipHint.transform.parent.gameObject.SetActive(Skippable);

            // Gloob, once he has met the hero (and for his hello), left of the shaft window (portrait: on its corner).
            var gloobConfig = GameDefinition.Instance != null ? GameDefinition.Instance.gloob : null;
            if (gloobConfig != null && gloobConfig.enabled && (Kind == Mode.Meet || Kind == Mode.Climb && GloobProgress.Met))
                gloob = GloobPortrait.Create(menu, stage, gloobConfig, gloobConfig.shaftPortrait, gloobConfig.shaftPortraitPortrait);

            flash = MenuGraphics.NewImage(stage, "Flash", new Color(1f, 0.95f, 0.85f, 0f));
            flash.rectTransform.offsetMin = new Vector2(-400f, -400f);
            flash.rectTransform.offsetMax = new Vector2(400f, 400f);
        }

        TextMeshProUGUI Line(string name, string value, MenuTextStyle style, float size, MenuPlacement landscape, MenuPlacement portrait, out MenuMotionDriver motion)
        {
            var text = menu.AddText(stage, name, value, style, landscape.anchor, landscape.offset, landscape.size, out motion, size);
            text.fontStyle |= FontStyles.UpperCase;
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            MenuResponsive.Track((RectTransform)text.transform.parent, landscape, portrait, MenuElementKind.Text, menu.Responsive);
            return text;
        }

        /// <summary>A strip across the shaft from 'top' (pixels from the top of the shaft) down 'height', between x0 and x1 (0-1).</summary>
        Image Strip(string name, Color color, float top, float height, float x0 = 0f, float x1 = 1f, Transform parent = null)
        {
            var rect = MenuGraphics.NewRect(name, parent != null ? parent : content);
            rect.anchorMin = new Vector2(x0, 1f);
            rect.anchorMax = new Vector2(x1, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A shape centred at x (0-1) and 'y' pixels from the top.</summary>
        Image Dot(string name, Color color, float x, float y, Vector2 size, MenuShape shape = MenuShape.Circle)
        {
            var rect = MenuGraphics.NewRect(name, content);
            rect.anchorMin = rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, -y);
            var image = rect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(image, shape, 12f);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TextMeshProUGUI Label(Transform parent, string name, string value, MenuTextStyle style, float size, float x0, float x1, float top, float height, Color? color = null)
        {
            var rect = MenuGraphics.NewRect(name, parent);
            rect.anchorMin = new Vector2(x0, 1f);
            rect.anchorMax = new Vector2(x1, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
            var text = MenuGraphics.Text(rect, "Text", value, style, menu.Font, size);
            text.fontStyle |= FontStyles.UpperCase;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (color.HasValue)
            {
                text.enableVertexGradient = false;
                text.color = color.Value;
            }
            return text;
        }

        /// <summary>
        /// A painted picture filling 'parent' the way a background does: scaled to cover it, centred, the overflow
        /// clipped (the shaft is 560 wide in landscape and 1000 in portrait, so the crop changes with the screen).
        /// </summary>
        Image Cover(Transform parent, string name, Sprite sprite)
        {
            var clip = MenuGraphics.NewRect(name, parent);
            MenuGraphics.Fill(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            var rect = MenuGraphics.NewRect("Art", clip);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            return image;
        }

        void BuildMeadow()
        {
            float grass = 70f;
            if (config.meadowArt != null)
            {
                // The painted Meadow has its own sky, grass and burned hole; the daylight still falls down the shaft.
                var meadow = Strip("Meadow", config.sky, 0f, Meadow);
                Cover(meadow.transform, "Painting", config.meadowArt);
                Strip("Daylight", new Color(1f, 0.95f, 0.7f, 0.14f), Meadow, 420f, 0.38f, 0.62f);
                PaintedMeadow = true;
                var sign = new Color(1f, 1f, 1f, 0.95f);
                Strip("NameBack", new Color(0f, 0f, 0f, 0.35f), 34f, 110f, 0.1f, 0.9f);
                Label(content, "MeadowName", texts.shaftMeadow, menu.Theme.heading, 40f, 0.05f, 0.95f, 40f, 60f, sign);
                Label(content, "MeadowFloor", string.Format(CultureInfo.InvariantCulture, texts.shaftFloorFormat, 0), menu.Theme.caption, 28f,
                    0.05f, 0.95f, 104f, 40f, sign);
                return;
            }
            Strip("Sky", config.sky, 0f, Meadow - grass);
            Strip("SkyGlow", new Color(1f, 0.97f, 0.8f, 0.35f), Meadow - grass - 90f, 90f);
            Strip("Grass", config.grass, Meadow - grass, grass);
            Strip("Soil", new Color(0.3f, 0.2f, 0.12f, 1f), Meadow - 18f, 18f);
            // The hole the portal burned, and the daylight that falls a little way down it.
            Strip("Hole", new Color(0.06f, 0.02f, 0.02f, 1f), Meadow - grass + 20f, grass - 20f, 0.4f, 0.6f);
            Strip("HoleRim", new Color(1f, 0.45f, 0.12f, 0.9f), Meadow - grass + 14f, 8f, 0.38f, 0.62f);
            Strip("Daylight", new Color(1f, 0.95f, 0.7f, 0.12f), Meadow, 420f, 0.4f, 0.6f);
            Label(content, "MeadowName", texts.shaftMeadow, menu.Theme.heading, 40f, 0.05f, 0.95f, 40f, 60f, new Color(0.15f, 0.25f, 0.1f, 1f));
            Label(content, "MeadowFloor", string.Format(CultureInfo.InvariantCulture, texts.shaftFloorFormat, 0), menu.Theme.caption, 28f,
                0.05f, 0.95f, 104f, 40f, new Color(0.15f, 0.25f, 0.1f, 1f));
        }

        void BuildBand(Band band)
        {
            float top = Y(band.Last - 1);
            float height = Mathf.Max(Ppf, Y(band.First) - top);
            var fill = Strip("Band " + band.Numeral, band.Color, top, height);
            if (band.Art != null)
            {
                // The Circle's painting, a little darker at the bottom where it meets the Circle below.
                Cover(fill.transform, "Painting", band.Art);
                Strip("Shade", new Color(0f, 0f, 0f, 0.22f), height * 0.8f, height * 0.2f, 0f, 1f, fill.transform);
                PaintedBands++;
            }
            else
            {
                // A darker lower half: depth, for a Circle without its painting.
                Strip("Shade", new Color(0f, 0f, 0f, 0.28f), height * 0.45f, height * 0.55f, 0f, 1f, fill.transform);
            }
            // A bright seam in the biome's colour at the top: where one Circle stacks on the next.
            Strip("Seam", Color.Lerp(band.Color, Color.white, 0.45f), 0f, 6f, 0f, 1f, fill.transform);
            band.Flash = Strip("Flash", new Color(1f, 1f, 1f, 0f), 0f, height, 0f, 1f, fill.transform);
            string name = string.Format(CultureInfo.InvariantCulture, texts.shaftCircleFormat, band.Numeral, band.Name,
                band.First.ToString("N0", CultureInfo.InvariantCulture), band.Last.ToString("N0", CultureInfo.InvariantCulture));
            if (band.Art != null)
                Strip("NameBack", new Color(0f, 0f, 0f, 0.5f), height * 0.5f - 24f, 48f, 0.12f, 0.88f, fill.transform);
            Label(fill.transform, "Name", name, menu.Theme.caption, 26f, 0.14f, 0.86f, height * 0.5f - 20f, 40f, new Color(1f, 1f, 1f, band.Art != null ? 0.95f : 0.8f));
        }

        void BuildBottom()
        {
            float top = Y(Bottom);
            Strip("Lava", new Color(1f, 0.42f, 0.12f, 1f), top, 160f);
            Strip("LavaGlow", new Color(1f, 0.8f, 0.3f, 0.6f), top, 10f);
            Label(content, "BottomFloor", string.Format(CultureInfo.InvariantCulture, texts.shaftFloorFormat, Bottom.ToString("N0", CultureInfo.InvariantCulture)),
                menu.Theme.caption, 28f, 0.05f, 0.95f, top + 60f, 40f, new Color(0.3f, 0.05f, 0.02f, 1f));
        }

        void BuildWalls()
        {
            float top = Meadow;
            float height = Y(Bottom) - Meadow;
            // Over painted bands the walls only darken the edges, so the paintings read across the whole shaft.
            var wall = PaintedBands > 0 ? config.wallOverArt : config.wall;
            Strip("WallLeft", wall, top, height, 0f, 0.11f);
            Strip("WallRight", wall, top, height, 0.89f, 1f);
            Strip("EdgeLeft", new Color(0f, 0f, 0f, 0.5f), top, height, 0.11f, 0.12f);
            Strip("EdgeRight", new Color(0f, 0f, 0f, 0.5f), top, height, 0.88f, 0.89f);
        }

        /// <summary>A tick every 10 floors (one level), a long one and the number at every Circle's edge.</summary>
        void BuildTicks()
        {
            var tick = new Color(1f, 0.85f, 0.6f, 0.35f);
            var big = new Color(1f, 0.85f, 0.6f, 0.8f);
            for (int floor = 10; floor <= Bottom; floor += 10)
            {
                bool hundred = floor % 100 == 0;
                Strip("Tick", hundred ? big : tick, Y(floor) - 1f, hundred ? 4f : 2f, hundred ? 0.8f : 0.845f, 0.89f);
                if (hundred && floor < Bottom)
                    Label(content, "Number", floor.ToString(CultureInfo.InvariantCulture), menu.Theme.caption, 20f, 0.89f, 1f, Y(floor) - 12f, 24f, big);
            }
        }

        /// <summary>Hell's shortcuts, hinted and not explained: a crack in the wall or a dotted gap between two floors.</summary>
        void BuildHints()
        {
            for (int i = 0; i < config.shortcutHints.Count; i++)
            {
                int floor = config.shortcutHints[i];
                if (floor <= 0 || floor >= Bottom)
                    continue;
                if (i % 2 == 0)
                {
                    bool left = i % 4 == 0;
                    for (int s = 0; s < 3; s++)
                    {
                        var crack = Dot("Crack", config.crack, left ? 0.135f + s * 0.012f : 0.865f - s * 0.012f, Y(floor) + s * 14f, new Vector2(5f, 30f), MenuShape.Rectangle);
                        crack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (left ? 1f : -1f) * (s % 2 == 0 ? 28f : -24f));
                    }
                }
                else
                {
                    for (int d = 0; d < 7; d++)
                        Dot("Gap", config.crack, 0.2f + d * 0.1f, Y(floor), new Vector2(9f, 9f));
                }
            }
        }

        /// <summary>The Warden's mark at the top of its Circle: a dark silhouette and ??? until the hero has beaten it.</summary>
        void BuildWarden(Band band)
        {
            bool met = WardenMet(band);
            float y = Y(band.Last) - Ppf * 0.5f + 46f;
            if (band.WardenArt != null)
            {
                // The Warden's own figure: a black shape against its Circle's glow until the hero has beaten it.
                float size = Mathf.Max(40f, config.wardenSize);
                y = Y(band.Last) - Ppf * 0.5f + size * 0.5f + 8f;
                var halo = Dot("WardenGlow", Color.Lerp(band.Color, Color.white, 0.35f), 0.5f, y, new Vector2(size * 1.15f, size * 1.15f));
                halo.color = new Color(halo.color.r, halo.color.g, halo.color.b, met ? 0.45f : 0.8f);
                var figure = Dot("Warden", met ? Color.white : config.wardenUnmet, 0.5f, y, new Vector2(size, size), MenuShape.Rectangle);
                figure.sprite = band.WardenArt;
                figure.enabled = true;
                figure.type = Image.Type.Simple;
                figure.preserveAspect = true;
                if (met)
                    WardenPortraits++;
                else
                    WardenSilhouettes++;
                Label(content, "WardenName", met ? band.Warden : texts.shaftUnknownWarden, menu.Theme.caption, met ? 24f : 34f, 0.13f, 0.87f, y + size * 0.5f + 2f, 36f,
                    met ? Color.white : new Color(1f, 1f, 1f, 0.9f));
                return;
            }
            var glow = Dot("WardenGlow", new Color(band.Color.r, band.Color.g, band.Color.b, 0.6f), 0.5f, y, new Vector2(110f, 110f));
            glow.color = Color.Lerp(band.Color, Color.white, 0.25f);
            Dot("Warden", met ? Color.Lerp(band.Color, Color.white, 0.55f) : new Color(0.03f, 0.01f, 0.02f, 1f), 0.5f, y, new Vector2(84f, 84f));
            Dot("Horns", met ? Color.Lerp(band.Color, Color.white, 0.55f) : new Color(0.03f, 0.01f, 0.02f, 1f), 0.5f, y - 44f, new Vector2(70f, 18f), MenuShape.Burst);
            Label(content, "WardenName", met ? band.Warden : texts.shaftUnknownWarden, menu.Theme.caption, met ? 24f : 34f, 0.13f, 0.87f, y + 46f, 36f,
                met ? Color.white : new Color(1f, 1f, 1f, 0.85f));
        }

        void BuildMarker()
        {
            markerRect = MenuGraphics.NewRect("Hero", content);
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(44f, 44f);
            markerGlow = NewCircle(markerRect, "Glow", new Color(config.marker.r, config.marker.g, config.marker.b, 0.35f), 96f);
            if (config.markerArt != null)
            {
                // The hero himself, small: his helmet and the rocket launcher on his shoulder, over a warm glow.
                float height = Mathf.Max(24f, config.markerSize);
                var sprite = config.markerArt;
                float width = sprite.rect.height > 0f ? height * sprite.rect.width / sprite.rect.height : height;
                markerGlow.rectTransform.sizeDelta = new Vector2(height * 1.6f, height * 1.6f);
                var rect = MenuGraphics.NewRect("Hero", markerRect);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width, height);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                PaintedMarker = true;
                return;
            }
            NewCircle(markerRect, "Ring", Color.white, 44f);
            NewCircle(markerRect, "Dot", config.marker, 32f);
        }

        Image NewCircle(Transform parent, string name, Color color, float size)
        {
            var rect = MenuGraphics.NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(image, MenuShape.Circle, 0f);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // ─── What it reads ────────────────────────────────────────────

        /// <summary>A level's floor range from its script (content level scripts: floors.start / floors.end), 0 if none.</summary>
        public static void LevelFloors(int levelNumber, out int start, out int end)
        {
            start = end = 0;
            var definition = GameDefinition.Instance;
            var level = definition != null && levelNumber > 0 ? definition.GetLevel(levelNumber) : null;
            if (level == null || string.IsNullOrEmpty(level.levelName))
                return;
            try
            {
                var content = GameContent.Current;
                if (content != null && content.LevelScripts.TryGetValue(level.levelName, out var script) && script != null && script.floors != null)
                {
                    start = script.floors.start;
                    end = script.floors.end;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shaft] Couldn't read level {levelNumber}'s floors: {e.Message}");
            }
        }

        /// <summary>The deepest floor the hero has climbed to: the lowest end floor of the cleared levels, or where the marker was.</summary>
        public static int HighestFloorReached()
        {
            int best = ShaftProgress.Floor;
            var definition = GameDefinition.Instance;
            var progress = GameProgressManager.Instance;
            if (definition == null || progress == null)
                return best;
            for (int n = 1; n <= definition.TotalLevels; n++)
            {
                if (!progress.IsLevelCompleted(n))
                    continue;
                LevelFloors(n, out _, out int end);
                if (end > 0 && (best <= 0 || end < best))
                    best = end;
            }
            return best;
        }

        bool WardenMet(Band band)
        {
            int reached = HighestFloorReached();
            if (Kind == Mode.Climb && !preview)
                reached = reached <= 0 ? ToFloor : Mathf.Min(reached, ToFloor);
            return reached > 0 && reached <= band.Last;
        }

        Band BandAt(int floor)
        {
            foreach (var band in bands)
            {
                if (floor <= band.First && floor >= band.Last)
                    return band;
            }
            return null;
        }

        /// <summary>The Warden still above 'floor': the one at the top of its Circle, or the next Circle's once that one is reached.</summary>
        Band NextWardenAbove(int floor)
        {
            Band best = null;
            foreach (var band in bands)
            {
                if (band.Last < floor && (best == null || band.Last > best.Last))
                    best = band;
            }
            return best;
        }

        // ─── Showing it ───────────────────────────────────────────────

        static string Number(int floor) => floor.ToString("N0", CultureInfo.InvariantCulture);

        void SetCounter(int floor)
        {
            ShownFloor = floor;
            MenuGraphics.SetText(counter, string.Format(CultureInfo.InvariantCulture, texts.shaftFloorFormat, Number(floor)));
            var band = BandAt(floor);
            MenuGraphics.SetText(circleLine, floor <= 0 || band == null ? texts.shaftMeadow
                : string.Format(CultureInfo.InvariantCulture, texts.shaftInCircleFormat, band.Numeral, band.Name));
        }

        void UpdateInfo(int floor)
        {
            bool inHell = floor > 0;
            floorsLeft.transform.parent.gameObject.SetActive(inHell);
            nextWarden.transform.parent.gameObject.SetActive(inHell);
            if (!inHell)
                return;
            int top = Mathf.Max(0, config.topFloor);
            MenuGraphics.SetText(floorsLeft, string.Format(CultureInfo.InvariantCulture, texts.shaftFloorsLeftFormat, Number(Mathf.Max(0, floor - top))));
            var warden = NextWardenAbove(floor);
            string name = warden == null ? "" : !string.IsNullOrEmpty(warden.Warden) ? warden.Warden : texts.shaftUnknownWarden;
            MenuGraphics.SetText(nextWarden, warden == null ? "" : string.Format(CultureInfo.InvariantCulture, texts.shaftNextWardenFormat, name, Number(floor - warden.Last)));
        }

        void ShowSubtitle(string line)
        {
            var slot = subtitle.transform.parent.gameObject;
            slot.SetActive(!string.IsNullOrEmpty(line));
            MenuGraphics.SetText(subtitle, line ?? "");
        }

        void Report(string kind, string detail)
        {
            Debug.Log($"[Shaft] {kind}: {detail}");
            try
            {
                Reported?.Invoke(kind, detail);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ─── The animations ───────────────────────────────────────────

        IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && !skip)
                yield return null;
        }

        static float SmoothStep(float t) => t * t * (3f - 2f * t);

        IEnumerator PlungeRoutine()
        {
            int bottom = ToFloor;
            yield return Wait(0.7f);
            Sfx.Play(config.fallSound);
            Report("fall", $"from FLOOR 0 down to FLOOR {Number(bottom)}");
            float duration = Mathf.Max(0.5f, config.plungeSeconds);
            float t = 0f;
            int shown = 0;
            while (t < 1f && !skip)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
                // Falling: slow off the edge, faster and faster to the bottom.
                float floor = bottom * t * t;
                markerFloor = cameraFloor = floor;
                int now = Mathf.FloorToInt(floor);
                foreach (var band in bands)
                {
                    // Entering a Circle from above, at its top floor.
                    if (shown < band.Last && now >= band.Last)
                    {
                        BandsPassed++;
                        Report("band", $"fell past Circle {band.Numeral}, {band.Name} ({band.First}-{band.Last}), Warden {(WardenMet(band) ? band.Warden : texts.shaftUnknownWarden)}");
                        Sfx.Play(config.tickSound);
                        if (band.Flash != null)
                            StartCoroutine(FlashBand(band.Flash));
                    }
                }
                if (now != shown)
                {
                    shown = now;
                    SetCounter(now);
                }
                yield return null;
            }
            if (skip)
            {
                foreach (var band in bands)
                    if (shown < band.Last)
                        BandsPassed++;
            }

            // The bottom.
            markerFloor = cameraFloor = bottom;
            SetCounter(bottom);
            UpdateInfo(bottom);
            Sfx.Play(config.slamSound);
            Report("slam", $"landed on FLOOR {Number(bottom)} after falling past {BandsPassed} of {bands.Count} Circles");
            if (!skip)
            {
                StartCoroutine(Slam());
                yield return Wait(0.6f);
            }

            if (!preview)
            {
                ShaftProgress.Floor = bottom;
                ShaftProgress.Plunged = true;
            }

            if (!skip)
            {
                // Hell says hello. The hero doesn't.
                Sfx.Play(config.welcomeVoice);
                ShowSubtitle(texts.shaftWelcome);
                Report("voice", $"announcer: {texts.shaftWelcome} ({config.welcomeVoice}{(Sfx.Has(config.welcomeVoice) ? "" : ", no clip")})");
                float welcome = Sfx.LengthOf(config.welcomeVoice);
                yield return Wait((welcome > 0f ? welcome : 3.5f) + 0.25f);
            }
            if (!skip)
            {
                Sfx.Play(config.heroVoice);
                ShowSubtitle(texts.shaftHero);
                Report("voice", $"hero: {texts.shaftHero} ({config.heroVoice}{(Sfx.Has(config.heroVoice) ? "" : ", no clip")})");
                float hero = Sfx.LengthOf(config.heroVoice);
                yield return Wait((hero > 0f ? hero : 1.4f) + 0.3f);
            }
            Finish();
        }

        IEnumerator ClimbRoutine()
        {
            int from = FromFloor, to = ToFloor;
            yield return Wait(Mathf.Max(0f, config.climbDelay));
            int floors = Mathf.Abs(from - to);
            float duration = Mathf.Clamp(floors * Mathf.Max(0.01f, config.climbSecondsPerFloor), 0.4f, Mathf.Max(0.4f, config.climbMaxSeconds));
            float t = 0f;
            int shown = from;
            if (floors > 0)
                Report("climb-start", $"from FLOOR {Number(from)} to FLOOR {Number(to)}");
            while (floors > 0 && t < 1f && !skip)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
                float floor = Mathf.Lerp(from, to, SmoothStep(t));
                markerFloor = cameraFloor = floor;
                int now = Mathf.RoundToInt(floor);
                if (now != shown)
                {
                    shown = now;
                    SetCounter(now);
                    Sfx.Play(config.tickSound);
                }
                yield return null;
            }
            markerFloor = cameraFloor = to;
            SetCounter(to);
            UpdateInfo(to);
            if (counterMotion != null && !skip)
                StartCoroutine(Punch(1.18f));
            if (!preview)
            {
                ShaftProgress.Floor = to;
                ShaftProgress.Plunged = true;
            }
            Report("climb", $"from FLOOR {Number(from)} to FLOOR {Number(to)}: {FloorsLeftText}; {NextWardenText}");
            if (gloob != null && !skip)
            {
                // One line from Gloob about what just happened.
                var warden = NextWardenAbove(to);
                var line = GloobPortrait.PickBetweenFloors(from, to, warden != null ? warden.Last : 0);
                if (line != null)
                {
                    yield return Wait(0.3f);
                    yield return gloob.Say(line, ShowSubtitle, () => skip);
                }
                Report("gloob", line != null ? $"between floors: {line.moment} ({line.sound})" : "between floors: nothing to say");
                if (!preview)
                    GloobPortrait.Heard(line);
            }
            Finish();
        }

        IEnumerator MeetRoutine()
        {
            markerFloor = cameraFloor = ToFloor;
            SetCounter(ToFloor);
            UpdateInfo(ToFloor);
            yield return Wait(0.6f);
            var lines = GloobPortrait.HelloLines();
            Report("gloob", $"hello at FLOOR {Number(ToFloor)}: {lines.Count} lines");
            foreach (var line in lines)
            {
                if (gloob == null || skip)
                    break;
                yield return gloob.Say(line, ShowSubtitle, () => skip);
            }
            if (!preview)
            {
                GloobProgress.Met = true;
                GloobPortrait.Heard(null);
            }
            Finish();
        }

        IEnumerator FlashBand(Image image)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                if (image == null)
                    yield break;
                image.color = new Color(1f, 1f, 1f, 0.35f * (1f - t));
                yield return null;
            }
            if (image != null)
                image.color = new Color(1f, 1f, 1f, 0f);
        }

        IEnumerator Slam()
        {
            StartCoroutine(Punch(1.4f));
            bool shaking = GameSettings.ScreenShake;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
            {
                float left = 1f - t;
                flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0.75f * left * left);
                shake = shaking ? UnityEngine.Random.insideUnitCircle * config.slamShake * left * left : Vector2.zero;
                yield return null;
            }
            flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0f);
            shake = Vector2.zero;
        }

        IEnumerator Punch(float size)
        {
            if (counterMotion == null)
                yield break;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                counterMotion.ExtraScale = Mathf.Lerp(size, 1f, SmoothStep(t));
                yield return null;
            }
            counterMotion.ExtraScale = 1f;
        }

        void Finish()
        {
            if (Ready)
                return;
            ShowSubtitle(null);
            skipHint.transform.parent.gameObject.SetActive(false);
            var place = MenuScreen.Portrait && config.continueButtonPortrait.use ? config.continueButtonPortrait : config.continueButton;
            continueButton = menu.AddButtonAt(config.continueButton.anchor, config.continueButton.offset, texts.continueText, menu.Theme.button,
                Continue, stage, config.continueButtonPortrait);
            var events = EventSystem.current;
            if (events != null && continueButton != null)
                events.SetSelectedGameObject(continueButton.gameObject);
            Ready = true;
            Report("ready", $"FLOOR {Number(ShownFloor)}; Continue is up" + (place == config.continueButtonPortrait ? " (portrait)" : ""));
        }

        // ─── Every frame ──────────────────────────────────────────────

        void Update()
        {
            if (content == null)
                return;
            ApplyCamera();
            if (markerGlow != null)
            {
                float pulse = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 5f);
                markerGlow.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
            stage.offsetMin = stage.offsetMax = shake;

            if (continued || Time.unscaledTime - openedAt < 0.35f)
                return;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool key = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                                            || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                       || gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
            bool tap = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                       || Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            if (!Ready)
            {
                if (Skippable && (key || tap) && !skip)
                {
                    skip = true;
                    Report("skip", $"skipped at FLOOR {Number(ShownFloor)}");
                }
                return;
            }
            // Taps go through the Continue button; keys and the gamepad continue from anywhere.
            if (key)
                Continue();
        }

        void ApplyCamera()
        {
            float view = ((RectTransform)content.parent).rect.height;
            float centre = Y(cameraFloor);
            // On the Meadow the camera keeps the sky in view; lower down the marker sits a little above the middle.
            float scroll = Mathf.Clamp(centre - view * 0.55f, 0f, Mathf.Max(0f, contentHeight - view));
            content.anchoredPosition = new Vector2(0f, scroll);
            markerRect.anchoredPosition = new Vector2(0f, -(markerFloor <= 0f ? Meadow - 40f : Y(markerFloor)));
        }

        // ─── Leaving ──────────────────────────────────────────────────

        void Continue()
        {
            if (!Ready || continued)
                return;
            continued = true;
            if (continueButton != null)
                continueButton.interactable = false;
            Report("continue", $"from FLOOR {Number(ShownFloor)}" + (preview ? " (preview closes)" : ""));
            var then = next;
            next = null;
            if (preview)
            {
                Close();
                then?.Invoke();
                return;
            }
            // Stay up over the old scene until the next one has loaded (Continue starts that load).
            StartCoroutine(CloseAfter(30f));
            try
            {
                then?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Close();
            }
        }

        /// <summary>A fallback if the next scene never loads (real time: the level may have stopped the clock).</summary>
        IEnumerator CloseAfter(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
                yield return null;
            Close();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single || continued)
                Close();
        }

        /// <summary>Takes the Shaft off the screen (a preview puts back what it covered).</summary>
        public void Close()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (preview)
            {
                foreach (var behaviour in hidden)
                {
                    if (behaviour != null)
                        behaviour.enabled = true;
                }
            }
            hidden.Clear();
            if (Current == this)
                Current = null;
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Current == this)
                Current = null;
        }
    }
}
