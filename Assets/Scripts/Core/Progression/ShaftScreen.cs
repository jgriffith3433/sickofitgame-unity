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
    /// and after every level with a floor range (the hero's marker climbs to the level's last floor), and after a death
    /// that ends a run (the marker drops back to the Waystone, <see cref="RunFlow"/>). Waystones are marked on the
    /// map, lit once reached. Continue goes on;
    /// once Yooba has met the hero, Visit Yooba beside it opens his shop over the Shaft and Back returns here.
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
            /// <summary>No climb: Yooba says hello where the marker is (after the video he turns up in, Video3).</summary>
            Meet,
            /// <summary>The run is over: the marker falls from where the hero died back to the Waystone (after the death scene).</summary>
            Drop,
        }

        /// <summary>The Shaft on screen now, or null.</summary>
        static ShaftScreen current;
        public static ShaftScreen Current
        {
            get => current;
            private set
            {
                current = value;
                SceneTransitionManager.ShaftCovering = value != null;
            }
        }

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
        /// <summary>Yooba's portrait on this Shaft, or null (before he has met the hero).</summary>
        public YoobaPortrait Yooba => yooba;
        /// <summary>Waystones marked on the map, and how many of them are lit (reached).</summary>
        public int WaystonesShown { get; private set; }
        public int WaystonesLit { get; private set; }
        /// <summary>Drop: the first run Hell has ended (the announcer explains Waystones; it can't be skipped).</summary>
        public bool FirstDrop => firstDrop;
        /// <summary>Drop: the level the next run starts on.</summary>
        public int RestartLevel { get; private set; }
        /// <summary>Drop: the line under the map once it is over ("Next run: Floors 1000-991: The Landing").</summary>
        public string RestartText => restartText ?? "";
        /// <summary>Visit Yooba, beside Continue once he has met the hero, or null.</summary>
        public MenuButton YoobaButton => yoobaButton;
        public MenuButton ContinueButton => continueButton;
        /// <summary>Yooba's shop is open over the Shaft (Visit Yooba).</summary>
        public bool ShopOpen => shopOpen;
        /// <summary>Climb: a shortcut taken on the level (Docs/Story.md, "Shortcuts"): the floor it starts from and the one it lets him out on, else 0.</summary>
        public int ShortcutFrom => shortcutFrom;
        public int ShortcutTo => shortcutTo;
        /// <summary>The marker has jumped the shortcut (the dotted line is drawn up to ShortcutTo).</summary>
        public bool ShortcutJumped { get; private set; }
        /// <summary>Dots drawn for shortcuts on this map (the one jumped now and the ones taken before).</summary>
        public int ShortcutDots { get; private set; }

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
            public string WakeVoice;
            public Image Flash;
            /// <summary>Darkness over a Circle still asleep (only on the climb that wakes it).</summary>
            public Image Dim;
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
        MenuButton yoobaButton;
        YoobaPortrait yooba;
        bool shopOpen;
        bool firstDrop;
        string restartText;
        readonly List<Image> waystoneGlows = new List<Image>();
        float keysFrom;
        readonly List<Band> bands = new List<Band>();
        readonly List<Behaviour> hidden = new List<Behaviour>();
        Action next;
        bool preview;
        bool skip;
        bool continued;
        float openedAt;
        float cameraFloor;
        float markerFloor;
        /// <summary>Where the marker is across the shaft (0.5 = the middle; a shortcut's jump swings it out through a crack).</summary>
        float markerAcross = 0.5f;
        int shortcutFrom;
        int shortcutTo;
        static int pendingShortcutFrom;
        static int pendingShortcutTo;
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
                // Yooba's hello, the first time the video he turns up in leads on to a level.
                var yoobaConfig = definition.yooba;
                if (yoobaConfig != null && yoobaConfig.enabled && stop.Video == yoobaConfig.helloAfterVideo && stop.NextLevel > 0 && !YoobaProgress.Met)
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

            if (stop.Kind == ShaftStopKind.AfterDeath)
            {
                // The run is over: from the floor he died on back down to the Waystone; Continue starts the next run.
                if (stop.Waystone <= 0)
                    return false;
                var drop = Open(Mode.Drop, stop.FromFloor > 0 ? stop.FromFloor : stop.Waystone, stop.Waystone, next, false, WaystoneProgress.RunsEnded <= 1);
                drop.SetRestart(stop.NextLevel);
                return true;
            }

            LevelFloors(stop.Level, out int start, out int end);
            if (end <= 0)
                return false;
            // From where the marker was, if that is below this level's end and inside its range; else the level's start.
            int saved = ShaftProgress.Floor;
            int from = saved > end && saved <= Mathf.Max(start, end) ? saved : start > end ? start : end;
            // A shortcut taken on the level: after the climb the marker jumps on up a dotted line to where it let him out.
            pendingShortcutFrom = stop.ShortcutTo > 0 ? (stop.ShortcutFrom > 0 ? stop.ShortcutFrom : end) : 0;
            pendingShortcutTo = stop.ShortcutTo > 0 && stop.ShortcutTo < end ? stop.ShortcutTo : 0;
            // A Warden's floor (Floor 901: it starts and ends on its Waystone) lights the Waystone and wakes the Circle above.
            pendingWardenFloor = start == end && RunFlow.IsWaystone(end) ? end : 0;
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

        /// <summary>Yooba's hello at 'floor' without the story flow (playtest setup shortcut). Continue closes it; nothing is saved.</summary>
        /// <summary>A climb to 'toFloor' and a shortcut's jump on to 'jumpTo' without the story flow (playtest setup shortcut). Nothing is saved.</summary>
        public static ShaftScreen PreviewShortcut(int fromFloor, int toFloor, int jumpTo, Action closed = null)
        {
            pendingShortcutFrom = toFloor;
            pendingShortcutTo = jumpTo > 0 && jumpTo < toFloor ? jumpTo : 0;
            return Open(Mode.Climb, fromFloor, toFloor, closed, true);
        }

        public static ShaftScreen PreviewMeet(int floor, Action closed = null)
        {
            return Open(Mode.Meet, floor, floor, closed, true);
        }

        /// <summary>
        /// The drop back to a Waystone without the story flow (playtest setup shortcut): the marker falls from 'fromFloor'
        /// to 'waystone' with the first death's lines if 'first'. Continue closes it; nothing is saved.
        /// </summary>
        public static ShaftScreen PreviewDrop(int fromFloor, int waystone, int restartLevel, bool first, Action closed = null)
        {
            var screen = Open(Mode.Drop, fromFloor, waystone, closed, true, first);
            screen.SetRestart(restartLevel);
            return screen;
        }

        void SetRestart(int level)
        {
            RestartLevel = level;
            var stage = level > 0 && GameDefinition.Instance != null ? GameDefinition.Instance.GetLevel(level) : null;
            string name = stage != null ? (!string.IsNullOrEmpty(stage.displayName) ? stage.displayName : stage.levelName) : "";
            restartText = string.IsNullOrEmpty(name) ? "" : string.Format(CultureInfo.InvariantCulture, texts.shaftNextRunFormat, name);
        }

        static ShaftScreen Open(Mode mode, int from, int to, Action next, bool preview, bool firstDrop = false)
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
            screen.firstDrop = mode == Mode.Drop && firstDrop;
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
            shortcutFrom = mode == Mode.Climb && pendingShortcutTo > 0 ? pendingShortcutFrom : 0;
            shortcutTo = mode == Mode.Climb ? pendingShortcutTo : 0;
            if (!isPreview)
            {
                AnalyticsManager.Screen("shaft");
                AnalyticsManager.Track("shaft", new Dictionary<string, object> { { "kind", mode.ToString().ToLowerInvariant() }, { "from", from }, { "to", to } });
            }
            pendingShortcutFrom = pendingShortcutTo = 0;
            wardenFloor = mode == Mode.Climb && !isPreview ? pendingWardenFloor : 0;
            pendingWardenFloor = 0;
            next = onContinue;
            preview = isPreview;
            // First plunge is skippable from its first frame (Docs/Story.md "First five minutes"); the first Waystone drop still plays out.
            Skippable = mode == Mode.Plunge || (ShaftProgress.Plunged && !firstDrop);
            openedAt = Time.unscaledTime;
            menu.Back = null;
            // A scene that replaces the current one (Continue's, or any other) takes the Shaft with it.
            SceneManager.sceneLoaded += OnSceneLoaded;

            HideTheRest();
            ResolveBands();
            Build();
            // The Shaft's own air (rushing wind, chains, the band far above) on the Ambience bus.
            if (!string.IsNullOrEmpty(config.ambientSound))
                AmbienceBed.PlaySound(config.ambientSound, 1.2f);
            cameraFloor = markerFloor = from;
            SetCounter(from);
            UpdateInfo(from);
            Report("open", $"{mode.ToString().ToLowerInvariant()} from {from} to {to}" + (shortcutTo > 0 ? $", then a shortcut from {shortcutFrom} to {shortcutTo}" : "")
                           + (preview ? " (preview)" : "") + (Skippable ? ", skippable" : ""));
            if (!string.IsNullOrEmpty(config.ambientSound))
                Report("ambience", config.ambientSound);
            StartCoroutine(mode == Mode.Plunge ? PlungeRoutine() : mode == Mode.Meet ? MeetRoutine() : mode == Mode.Drop ? DropRoutine() : ClimbRoutine());
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
                    WakeVoice = band.wakeVoice,
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
            BuildWaystones();
            foreach (var band in bands)
                BuildWarden(band);
            BuildMarker();
            Report("waystones", $"{WaystonesShown} Waystones on the map (FLOORS {string.Join(", ", RunFlow.Waystones())}), {WaystonesLit} lit; "
                                + $"highest reached: {(WaystoneProgress.Highest > 0 ? "FLOOR " + Number(WaystoneProgress.Highest) : "none yet")}");
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
            subtitle.textWrappingMode = TextWrappingModes.Normal;
            subtitle.transform.parent.gameObject.SetActive(false);
            skipHint = Line("SkipHint", texts.shaftSkipHint, theme.caption, 26f,
                MenuPlacement.At(config.continueButton.anchor.x, config.continueButton.anchor.y, config.continueButton.offset.x, config.continueButton.offset.y, 600f, 50f),
                MenuPlacement.At(config.continueButtonPortrait.anchor.x, config.continueButtonPortrait.anchor.y, config.continueButtonPortrait.offset.x, config.continueButtonPortrait.offset.y, 900f, 50f),
                out _);
            skipHint.enableVertexGradient = false;
            skipHint.color = theme.muted;
            skipHint.transform.parent.gameObject.SetActive(Skippable);

            // Yooba, once he has met the hero (and for his hello), left of the shaft window (portrait: on its corner).
            var yoobaConfig = GameDefinition.Instance != null ? GameDefinition.Instance.yooba : null;
            if (yoobaConfig != null && yoobaConfig.enabled && (Kind == Mode.Meet || (Kind == Mode.Climb || Kind == Mode.Drop) && YoobaProgress.Met))
                yooba = YoobaPortrait.Create(menu, stage, yoobaConfig, yoobaConfig.shaftPortrait, yoobaConfig.shaftPortraitPortrait);

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
            band.Dim = Strip("Asleep", new Color(0f, 0f, 0f, 0f), 0f, height, 0f, 1f, fill.transform);
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

            // The shortcuts he has taken: a dotted jump up through the crack, drawn for good (the one jumped now draws as he goes).
            foreach (var taken in ShortcutProgress.All())
            {
                if (taken.FromFloor <= taken.ToFloor || (shortcutTo > 0 && taken.ToFloor == shortcutTo))
                    continue;
                foreach (var dot in ShortcutPath(taken.FromFloor, taken.ToFloor))
                    ShortcutDot(dot);
            }
        }

        /// <summary>The side of the shaft a shortcut leaves through: the wall of the crack hinted nearest to it (left 0.14, right 0.86), or the middle for a gap.</summary>
        float ShortcutSide(int fromFloor, int toFloor)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < config.shortcutHints.Count; i++)
            {
                int floor = config.shortcutHints[i];
                float distance = floor <= fromFloor && floor >= toFloor ? 0f : Mathf.Min(Mathf.Abs(floor - fromFloor), Mathf.Abs(floor - toFloor));
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            if (best < 0 || best % 2 != 0)
                return 0.75f;
            return best % 4 == 0 ? 0.14f : 0.86f;
        }

        /// <summary>A point on a shortcut's jump: t 0 = where he took it (the middle of the shaft), out to the crack's wall and back in at 'toFloor'.</summary>
        Vector2 ShortcutPoint(int fromFloor, int toFloor, float side, float t)
        {
            float across = Mathf.Lerp(0.5f, side, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
            return new Vector2(across, Mathf.Lerp(fromFloor, toFloor, t));
        }

        List<Vector2> ShortcutPath(int fromFloor, int toFloor)
        {
            float side = ShortcutSide(fromFloor, toFloor);
            int count = Mathf.Clamp(Mathf.Abs(fromFloor - toFloor) / 2, 8, 40);
            var dots = new List<Vector2>();
            for (int i = 1; i < count; i++)
                dots.Add(ShortcutPoint(fromFloor, toFloor, side, i / (float)count));
            return dots;
        }

        void ShortcutDot(Vector2 point)
        {
            Dot("ShortcutDot", config.shortcutPath, point.x, Y(point.y), new Vector2(10f, 10f));
            ShortcutDots++;
        }

        /// <summary>
        /// The Waystones (Docs/Story.md, "Waystones and runs"): a standing stone on the left wall at the Landing and at
        /// the top of every Circle, with a line across the shaft and its name. Lit (glowing) once reached: the highest
        /// Waystone reached and every one below it; dark stone above.
        /// </summary>
        void BuildWaystones()
        {
            int highest = WaystoneProgress.Highest;
            if (Kind == Mode.Drop && (highest <= 0 || ToFloor < highest))
                highest = ToFloor;
            foreach (int floor in RunFlow.Waystones())
            {
                if (floor <= 0 || floor > Bottom)
                    continue;
                bool lit = highest > 0 && floor >= highest;
                var color = lit ? config.waystoneLit : config.waystoneDark;
                // At the bottom the stone stands on the lava's edge, inside the shaft; elsewhere on the Circle's top floor.
                float y = floor >= Bottom ? Y(Bottom) - 22f : Y(floor) - 4f;
                Strip("WaystoneLine", new Color(color.r, color.g, color.b, lit ? 0.6f : 0.3f), y + 18f, 3f, 0.12f, 0.88f);
                var glow = Dot("WaystoneGlow", new Color(color.r, color.g, color.b, lit ? 0.45f : 0f), 0.165f, y, new Vector2(70f, 70f));
                if (lit)
                    waystoneGlows.Add(glow);
                Dot("Waystone", color, 0.165f, y, new Vector2(30f, 40f), MenuShape.Rounded);
                Dot("WaystoneCap", Color.Lerp(color, Color.black, 0.35f), 0.165f, y - 20f, new Vector2(38f, 8f), MenuShape.Rounded);
                Label(content, "WaystoneName", texts.shaftWaystone, menu.Theme.caption, 18f, 0.2f, 0.42f, y - 12f, 26f,
                    new Color(color.r, color.g, color.b, lit ? 1f : 0.7f));
                WaystonesShown++;
                if (lit)
                    WaystonesLit++;
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
        static int pendingWardenFloor;
        int wardenFloor;

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
            // First five minutes: the whole plunge (fall + lines) is at most 8 s, and a tap skips from frame one.
            const float PlungeBudget = 8f;
            float started = Time.unscaledTime;
            int bottom = ToFloor;
            yield return Wait(0.15f);
            Sfx.Play(config.fallSound);
            PlayMusic(config.plungeMusic, "the plunge's riser and slam");
            Report("fall", $"from FLOOR 0 down to FLOOR {Number(bottom)}");
            float duration = Mathf.Min(Mathf.Max(0.5f, config.plungeSeconds), 2.8f);
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
                yield return Wait(0.25f);
            }

            if (!preview)
            {
                ShaftProgress.Floor = bottom;
                ShaftProgress.Plunged = true;
            }

            if (!skip)
            {
                // Hell says hello. The hero doesn't. Cap so the whole plunge stays inside the budget.
                float left = PlungeBudget - (Time.unscaledTime - started);
                Sfx.Play(config.welcomeVoice);
                ShowSubtitle(texts.shaftWelcome);
                Report("voice", $"announcer: {texts.shaftWelcome} ({config.welcomeVoice}{(Sfx.Has(config.welcomeVoice) ? "" : ", no clip")})");
                float welcome = Sfx.LengthOf(config.welcomeVoice);
                float welcomeWait = Mathf.Min((welcome > 0f ? welcome : 2.2f) + 0.15f, Mathf.Max(0.4f, left * 0.6f));
                yield return Wait(welcomeWait);
            }
            if (!skip)
            {
                float left = PlungeBudget - (Time.unscaledTime - started);
                Sfx.Play(config.heroVoice);
                ShowSubtitle(texts.shaftHero);
                Report("voice", $"hero: {texts.shaftHero} ({config.heroVoice}{(Sfx.Has(config.heroVoice) ? "" : ", no clip")})");
                float hero = Sfx.LengthOf(config.heroVoice);
                float heroWait = Mathf.Min((hero > 0f ? hero : 1.0f) + 0.15f, Mathf.Max(0.3f, left));
                yield return Wait(heroWait);
            }
            Report("plunge", $"done in {Time.unscaledTime - started:0.00}s (skip={skip})");
            Finish();
        }

        /// <summary>
        /// The Circle that wakes up on this climb: the one above a Waystone the climb has just reached at the top of a
        /// Circle (Floor 901 when the Magma King falls: Circle IX). Null on every other climb and on previews.
        /// </summary>
        Band WakingBand()
        {
            if (Kind != Mode.Climb || preview || ToFloor == config.bottomFloor || !RunFlow.IsWaystone(ToFloor))
                return null;
            // Only the Warden's own floor wakes it: the marker standing on the Waystone (901 to 901) after his fight,
            // not the climb up to it from the floor below (902 to 901).
            if (wardenFloor != ToFloor)
                return null;
            return BandAt(ToFloor - 1);
        }

        /// <summary>The Circle above wakes up: its band brightens out of the dark, flashes, and the announcer calls it.</summary>
        IEnumerator WakeBand(Band band)
        {
            string text = string.Format(CultureInfo.InvariantCulture, texts.shaftWakeFormat ?? "", band.Numeral, (band.Name ?? "").ToUpperInvariant());
            Report("waystone-lit", $"the Waystone at FLOOR {Number(ToFloor)} glows ({(WaystoneProgress.Highest > 0 && WaystoneProgress.Highest <= ToFloor ? "saved: the highest reached" : "NOT saved as reached")})");
            Report("band-wakes", $"Circle {band.Numeral}, {band.Name} ({band.First}-{band.Last}) wakes above the Waystone at FLOOR {Number(ToFloor)}; next Warden {band.Warden}");
            if (!string.IsNullOrEmpty(band.WakeVoice))
            {
                Sfx.Play(band.WakeVoice);
                Report("voice", $"announcer: {text} ({band.WakeVoice}{(Sfx.Has(band.WakeVoice) ? "" : ", no clip")})");
            }
            ShowSubtitle(text);
            if (band.Flash != null)
                StartCoroutine(FlashBand(band.Flash));
            float fade = 1.4f;
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / fade)
            {
                if (band.Dim != null)
                    band.Dim.color = new Color(0f, 0f, 0f, 0.75f * (1f - SmoothStep(Mathf.Clamp01(t))));
                yield return null;
            }
            if (band.Dim != null)
                band.Dim.color = new Color(0f, 0f, 0f, 0f);
            float voice = string.IsNullOrEmpty(band.WakeVoice) ? 0f : Sfx.LengthOf(band.WakeVoice);
            yield return Wait(Mathf.Max(0.6f, voice - fade + 0.3f));
        }

        IEnumerator ClimbRoutine()
        {
            int from = FromFloor, to = ToFloor;
            // A Circle about to wake starts the climb asleep: dark over its painting.
            var waking = WakingBand();
            if (waking != null && waking.Dim != null)
                waking.Dim.color = new Color(0f, 0f, 0f, 0.75f);
            yield return Wait(Mathf.Max(0f, config.climbDelay));
            int floors = Mathf.Abs(from - to);
            float duration = Mathf.Clamp(floors * Mathf.Max(0.01f, config.climbSecondsPerFloor), 0.4f, Mathf.Max(0.4f, config.climbMaxSeconds));
            float t = 0f;
            int shown = from;
            if (floors > 0)
            {
                PlayMusic(config.climbMusic, "the climb's rising sting");
                Report("climb-start", $"from FLOOR {Number(from)} to FLOOR {Number(to)}");
            }
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
            if (shortcutTo > 0)
            {
                yield return JumpShortcut(shortcutFrom > 0 ? shortcutFrom : to, shortcutTo);
                to = shortcutTo;
                ToFloor = to;
            }
            if (!preview)
            {
                ShaftProgress.Floor = to;
                ShaftProgress.Plunged = true;
            }
            Report("climb", $"from FLOOR {Number(from)} to FLOOR {Number(to)}: {FloorsLeftText}; {NextWardenText}");
            if (waking != null)
            {
                if (skip && waking.Dim != null)
                    waking.Dim.color = new Color(0f, 0f, 0f, 0f);
                yield return Wait(0.3f);
                yield return WakeBand(waking);
            }
            if (yooba != null && !skip)
            {
                // One line from Yooba about what just happened.
                var warden = NextWardenAbove(to);
                var line = YoobaPortrait.PickBetweenFloors(from, to, warden != null ? warden.Last : 0);
                if (line != null)
                {
                    yield return Wait(0.3f);
                    yield return yooba.Say(line, ShowSubtitle, () => skip);
                }
                Report("yooba", line != null ? $"between floors: {line.moment} ({line.sound})" : "between floors: nothing to say");
                if (!preview)
                    YoobaPortrait.Heard(line);
            }
            Finish();
        }

        /// <summary>
        /// A shortcut (Docs/Story.md, "Shortcuts"): the marker leaves the shaft through the crack it hinted at and
        /// jumps on up a dotted line, the counter racing, to the floor the shortcut let him out on.
        /// </summary>
        IEnumerator JumpShortcut(int from, int to)
        {
            Report("shortcut-start", $"from FLOOR {Number(from)} to FLOOR {Number(to)}");
            if (!skip)
                yield return Wait(0.35f);
            var dots = ShortcutPath(from, to);
            float side = ShortcutSide(from, to);
            int drawn = 0;
            float duration = Mathf.Max(0.4f, config.shortcutSeconds);
            float t = 0f;
            int shown = from;
            if (!string.IsNullOrEmpty(config.shortcutSound))
                Sfx.Play(config.shortcutSound);
            while (t < 1f && !skip)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
                float eased = SmoothStep(t);
                var point = ShortcutPoint(from, to, side, eased);
                markerAcross = point.x;
                markerFloor = cameraFloor = point.y;
                while (drawn < dots.Count && (drawn + 1f) / (dots.Count + 1f) <= eased)
                    ShortcutDot(dots[drawn++]);
                int now = Mathf.RoundToInt(point.y);
                if (now != shown)
                {
                    shown = now;
                    SetCounter(now);
                }
                yield return null;
            }
            while (drawn < dots.Count)
                ShortcutDot(dots[drawn++]);
            markerAcross = 0.5f;
            markerFloor = cameraFloor = to;
            SetCounter(to);
            UpdateInfo(to);
            ShortcutJumped = true;
            if (counterMotion != null && !skip)
                StartCoroutine(Punch(1.3f));
            Report("shortcut", $"jumped from FLOOR {Number(from)} to FLOOR {Number(to)} ({Mathf.Abs(from - to)} floors skipped), a dotted line of {dots.Count} through the "
                               + (side < 0.5f ? "left wall's crack" : side > 0.8f ? "right wall's crack" : "gap") + $": {FloorsLeftText}");
        }

        /// <summary>One of the Shaft's music cues (an audio.json sound, played over the map like its effects).</summary>
        void PlayMusic(string sound, string what)
        {
            if (string.IsNullOrEmpty(sound))
                return;
            Sfx.Play(sound);
            float length = Sfx.LengthOf(sound);
            Report("music", $"{what} ({sound}{(Sfx.Has(sound) ? $", {length:0.0} s" : ", no clip")})");
        }

        IEnumerator MeetRoutine()
        {
            markerFloor = cameraFloor = ToFloor;
            SetCounter(ToFloor);
            UpdateInfo(ToFloor);
            yield return Wait(0.6f);
            var lines = YoobaPortrait.HelloLines();
            Report("yooba", $"hello at FLOOR {Number(ToFloor)}: {lines.Count} lines");
            foreach (var line in lines)
            {
                if (yooba == null || skip)
                    break;
                yield return yooba.Say(line, ShowSubtitle, () => skip);
            }
            if (!preview)
            {
                YoobaProgress.Met = true;
                YoobaPortrait.Heard(null);
            }
            Finish();
        }

        /// <summary>
        /// The run is over: a beat on the floor he died on, then the marker falls (faster and faster) back to the
        /// Waystone, the slam, and Hell's announcer: the first time the rule (*"Hell doesn't let you die. It lets you
        /// do it again."*, and the hero's answer), after that a short jeer. Under the map: where the next run starts.
        /// </summary>
        IEnumerator DropRoutine()
        {
            int from = FromFloor, to = ToFloor;
            markerFloor = cameraFloor = from;
            SetCounter(from);
            UpdateInfo(from);
            yield return Wait(0.8f);
            int floors = Mathf.Abs(to - from);
            if (floors > 0)
            {
                Sfx.Play(config.fallSound);
                Report("drop-start", $"from FLOOR {Number(from)} back down to the Waystone on FLOOR {Number(to)}");
                float duration = Mathf.Clamp(floors * Mathf.Max(0.005f, config.dropSecondsPerFloor), 0.6f, Mathf.Max(0.6f, config.dropMaxSeconds));
                float t = 0f;
                int shown = from;
                while (t < 1f && !skip)
                {
                    t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
                    float floor = Mathf.Lerp(from, to, t * t);
                    markerFloor = cameraFloor = floor;
                    int now = Mathf.RoundToInt(floor);
                    if (now != shown)
                    {
                        shown = now;
                        SetCounter(now);
                    }
                    yield return null;
                }
            }
            markerFloor = cameraFloor = to;
            SetCounter(to);
            UpdateInfo(to);
            Sfx.Play(config.slamSound);
            if (!skip)
                StartCoroutine(Slam());
            if (!preview)
                ShaftProgress.Floor = to;
            Report("drop", $"from FLOOR {Number(from)} to the Waystone on FLOOR {Number(to)}: {FloorsLeftText}; {NextWardenText}; next run: level {RestartLevel}");
            if (!skip)
                yield return Wait(0.6f);

            var lines = firstDrop
                ? new[] { ("announcer", config.firstDropVoice, texts.shaftFirstDrop), ("hero", config.firstDropHeroVoice, texts.shaftFirstDropHero) }
                : new[] { ("announcer", config.dropVoice, texts.shaftDrop) };
            foreach (var (who, sound, text) in lines)
            {
                if (skip)
                    break;
                Sfx.Play(sound);
                ShowSubtitle(text);
                Report("voice", $"{who}: {text} ({sound}{(Sfx.Has(sound) ? "" : ", no clip")})");
                float length = Sfx.LengthOf(sound);
                yield return Wait((length > 0f ? length : 2.5f) + 0.3f);
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
            // Yooba, once he has met the hero, keeps his shop open between floors: Visit Yooba stands beside Continue.
            bool shop = yooba != null;
            var landscape = shop ? config.continueBesideYooba : config.continueButton;
            var portrait = shop ? config.continueBesideYoobaPortrait : config.continueButtonPortrait;
            var place = MenuScreen.Portrait && portrait.use ? portrait : landscape;
            if (shop)
            {
                var style = menu.Theme.backButton.Clone();
                if (config.yoobaButton.size.x > 0f && config.yoobaButton.size.y > 0f)
                    style.size = config.yoobaButton.size;
                yoobaButton = menu.AddButtonAt(config.yoobaButton.anchor, config.yoobaButton.offset, texts.shaftVisitYooba, style,
                    VisitYooba, stage, config.yoobaButtonPortrait);
            }
            continueButton = menu.AddButtonAt(landscape.anchor, landscape.offset, texts.continueText, menu.Theme.button,
                Continue, stage, portrait);
            var events = EventSystem.current;
            if (events != null && continueButton != null)
                events.SetSelectedGameObject(continueButton.gameObject);
            Ready = true;
            // After a drop: where the next run starts, under the map.
            if (Kind == Mode.Drop && !string.IsNullOrEmpty(restartText))
                ShowSubtitle(restartText);
            Report("ready", $"FLOOR {Number(ShownFloor)}; Continue is up" + (shop ? ", Visit Yooba beside it" : "") + (place == portrait ? " (portrait)" : ""));
        }

        // ─── Yooba's shop ─────────────────────────────────────────────

        /// <summary>Visit Yooba: his shop opens over the Shaft; Back brings the Shaft back with Continue up.</summary>
        void VisitYooba()
        {
            if (!Ready || continued || shopOpen)
                return;
            shopOpen = true;
            SetShown(false);
            Report("yooba-shop", $"Visit Yooba at FLOOR {Number(ShownFloor)}: his shop opens over the Shaft");
            AnalyticsManager.Choice("shaft", "visit_yooba");
            UpgradeShopManager.OpenOverShaft(BackFromShop);
        }

        void BackFromShop()
        {
            if (this == null)
                return;
            shopOpen = false;
            // The key that closed the shop (Escape) must not also press Continue.
            keysFrom = Time.unscaledTime + 0.35f;
            SetShown(true);
            var events = EventSystem.current;
            if (events != null && continueButton != null)
                events.SetSelectedGameObject(continueButton.gameObject);
            Report("yooba-shop", $"back on the Shaft at FLOOR {Number(ShownFloor)}; Continue is up");
        }

        /// <summary>The Shaft's own canvas on or off (off under Yooba's shop, so nothing under it can be pressed).</summary>
        void SetShown(bool shown)
        {
            if (menu == null)
                return;
            var canvas = menu.GetComponent<Canvas>();
            if (canvas != null)
                canvas.enabled = shown;
            var raycaster = menu.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = shown;
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
            float stone = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 2.4f);
            foreach (var glow in waystoneGlows)
            {
                if (glow != null)
                    glow.rectTransform.localScale = new Vector3(stone, stone, 1f);
            }
            stage.offsetMin = stage.offsetMax = shake;

            if (continued || shopOpen || Time.unscaledTime < keysFrom || Time.unscaledTime - openedAt < 0.35f)
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
            markerRect.anchoredPosition = new Vector2(content.rect.width * (markerAcross - 0.5f), -(markerFloor <= 0f ? Meadow - 40f : Y(markerFloor)));
        }

        // ─── Leaving ──────────────────────────────────────────────────

        void Continue()
        {
            if (!Ready || continued || shopOpen)
                return;
            continued = true;
            if (continueButton != null)
                continueButton.interactable = false;
            if (yoobaButton != null)
                yoobaButton.interactable = false;
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
            if (shopOpen && UpgradeShopManager.OverShaft != null)
                UpgradeShopManager.OverShaft.CloseOverlay(false);
            shopOpen = false;
            // Let the next level's bed take over; if we go to a menu, silence the Shaft bed.
            if (!string.IsNullOrEmpty(config != null ? config.ambientSound : null)
                && AmbienceBed.Current != null
                && AmbienceBed.Current.IndexOf("shaft", System.StringComparison.OrdinalIgnoreCase) >= 0)
                AmbienceBed.Stop();
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
