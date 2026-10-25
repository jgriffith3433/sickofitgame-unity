using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using SickOfIt.Core.Flow;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// A menu screen built at runtime from a <see cref="MenuTheme"/> (the look) and a <see cref="MenuLayoutConfig"/>
    /// (what goes where). The canvas scales a fixed reference space sized on the screen's short side (1920x1080 in
    /// landscape, theme responsive.portraitReferenceResolution on a phone held upright) and keeps inside the safe
    /// area. Every element it adds is a <see cref="MenuResponsive"/>: it takes its portrait placement on a tall
    /// screen, stays on screen, and buttons never get smaller than a thumb. Lists stack or wrap
    /// (<see cref="MenuListLayout"/>). Screens add their own buttons, cards and chips through the Add* methods.
    /// </summary>
    public sealed class MenuCanvas : MonoBehaviour
    {
        public MenuTheme Theme { get; private set; }
        public MenuLayoutConfig Layout { get; private set; }
        /// <summary>Everything inside the safe area.</summary>
        public RectTransform Root { get; private set; }
        public TMP_FontAsset Font { get; private set; }
        public TMP_FontAsset TitleFont { get; private set; }
        public MenuSounds Sounds { get; private set; }
        /// <summary>Called by Escape / gamepad B.</summary>
        public Action Back;

        readonly List<MenuButton> buttons = new List<MenuButton>();
        RectTransform stats;
        int entranceIndex;
        CanvasScaler scaler;
        bool? scaledPortrait;

        /// <summary>Touch sizes, margins and portrait layouts (theme.json "responsive").</summary>
        public MenuResponsiveStyle Responsive => Theme.responsive ?? (Theme.responsive = new MenuResponsiveStyle());

        /// <param name="owner">
        /// The object the menu belongs to. The canvas is put in its scene: scenes load additively, so the active scene
        /// at Start can be the one about to unload (which would take the menu with it).
        /// </param>
        public static MenuCanvas Create(string name, MenuTheme theme, MenuLayoutConfig layout, GameObject owner)
        {
            theme = theme ?? new MenuTheme();
            layout = layout ?? new MenuLayoutConfig();
            EnsureEventSystem();
            // A floor transition left the screen covered: every new menu or HUD opens it.
            ScreenTransition.Reveal();

            var go = new GameObject(name, typeof(RectTransform));
            if (owner != null && owner.scene.IsValid() && go.scene != owner.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, owner.scene);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Expand: the whole reference space always fits on screen; extra space is added around it.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();

            var menu = go.AddComponent<MenuCanvas>();
            menu.Theme = theme;
            menu.scaler = scaler;
            menu.ApplyScaler();
            menu.Layout = layout;
            menu.Font = MenuGraphics.FontAsset(theme.font);
            menu.TitleFont = theme.titleFont != null ? MenuGraphics.FontAsset(theme.titleFont) : menu.Font;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.ignoreListenerPause = true;
            menu.Sounds = new MenuSounds { Source = audio, Hover = theme.hoverSound, Click = theme.clickSound, Volume = theme.soundVolume };

            menu.BuildBackdrop(layout.backdrop);
            menu.Root = MenuGraphics.NewRect("SafeArea", go.transform);
            MenuGraphics.Fill(menu.Root);
            menu.Root.gameObject.AddComponent<MenuSafeArea>();

            menu.AddLabel("Title", layout.title, theme.title, menu.TitleFont, theme.titleEntrance);
            menu.AddLabel("Subtitle", layout.subtitle, theme.subtitle, menu.Font, theme.entrance);
            foreach (var decoration in layout.decorations)
                menu.AddDecoration(decoration);

            SceneLoadingManager.HideOnLoad(go);
            menu.SafeAdoptScene(go.scene);
            return menu;
        }

        /// <summary>
        /// Brings a scene's other UI (the generated loading screen, preload spinner, mobile controls' text) into
        /// this theme, and hides the old health/points readouts that menus and the HUD replace.
        /// </summary>
        /// <summary><see cref="AdoptScene"/>, but a failure only logs: restyling is cosmetic and must never stop a menu from building.</summary>
        public void SafeAdoptScene(UnityEngine.SceneManagement.Scene scene)
        {
            try
            {
                AdoptScene(scene);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void AdoptScene(UnityEngine.SceneManagement.Scene scene)
        {
            HideLegacyStats(scene);
            if (!scene.IsValid())
                return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text.GetComponentInParent<MenuCanvas>(true) != null)
                        continue;
                    // Keep each text's size, color and place; change the lettering to match. The outline needs the
                    // text's material, which a never-enabled text (the hidden loading screen) doesn't have yet, so it's
                    // applied when the text is enabled.
                    try
                    {
                        text.font = Font;
                        text.fontStyle |= FontStyles.Bold | FontStyles.UpperCase;
                        text.characterSpacing = Theme.caption.characterSpacing * 0.5f;
                        if (!text.TryGetComponent<MenuThemedOutline>(out var outline))
                            outline = text.gameObject.AddComponent<MenuThemedOutline>();
                        outline.Set(Mathf.Max(0.15f, Theme.heading.outlineWidth), Theme.title.outlineColor);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Menu] Couldn't restyle '{text.name}': {e.Message}");
                    }
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "LoadingScreen")
                        continue;
                    foreach (var image in t.GetComponentsInChildren<Image>(true))
                    {
                        if (image.name == "_backgroundImg")
                            image.color = Theme.title.outlineColor.a > 0f ? Theme.title.outlineColor : image.color;
                    }
                }
            }
        }

        /// <summary>The mobile UI prefab's own health/points readouts; menus show chips instead.</summary>
        static readonly string[] LegacyStats = { "CumulativeHealth", "PointsUI", "Points", "PointText" };

        public static void HideLegacyStats(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid())
                return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (Array.IndexOf(LegacyStats, t.name) >= 0 && t.GetComponentInParent<MenuCanvas>(true) == null)
                        t.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Landscape: the theme's reference space (1920x1080). Portrait: the portrait one (1080x1920), so on a phone
        /// the scale comes from the screen's width, its short side, and a button is the same size in both.
        /// </summary>
        void ApplyScaler()
        {
            bool portrait = MenuScreen.Portrait;
            if (scaler == null || scaledPortrait == portrait)
                return;
            scaledPortrait = portrait;
            Vector2 landscape = Theme.referenceResolution.x > 0f && Theme.referenceResolution.y > 0f
                ? Theme.referenceResolution : new Vector2(1920f, 1080f);
            Vector2 tall = Responsive.portraitReferenceResolution;
            scaler.referenceResolution = portrait && tall.x > 0f && tall.y > 0f ? tall : landscape;
        }

        /// <summary>Starts the entrance cascade over, so a popup opened later doesn't wait behind everything the canvas ever built.</summary>
        public void ResetEntranceCascade() => entranceIndex = 0;

        float NextDelay(MenuEntrance entrance) => entrance == null ? 0f : entrance.delay + entranceIndex++ * entrance.stagger;

        public MenuTextStyle StyleNamed(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "title": return Theme.title;
                case "subtitle": return Theme.subtitle;
                case "heading": return Theme.heading;
                case "body": return Theme.body;
                case "chip": return Theme.chipText;
                default: return Theme.caption;
            }
        }

        // ─── Backdrop ─────────────────────────────────────────────────

        void BuildBackdrop(MenuBackdrop backdrop)
        {
            var root = MenuGraphics.NewRect("Backdrop", transform);
            MenuGraphics.Fill(root);
            root.gameObject.AddComponent<RectMask2D>();
            if (backdrop.image != null)
            {
                var image = MenuGraphics.NewImage(root, "Image", backdrop.imageTint);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                MenuGraphics.Cover(image, backdrop.image);
            }
            if (backdrop.tint.a > 0f)
                MenuGraphics.NewImage(root, "Tint", backdrop.tint);
            if (backdrop.pattern != MenuPattern.None && backdrop.patternColor.a > 0f)
            {
                var patternRect = MenuGraphics.NewRect("Pattern", root);
                MenuGraphics.Fill(patternRect);
                var raw = patternRect.gameObject.AddComponent<RawImage>();
                raw.texture = MenuGraphics.Pattern(backdrop.pattern);
                raw.color = backdrop.patternColor;
                raw.raycastTarget = false;
                var scroller = patternRect.gameObject.AddComponent<MenuPatternScroller>();
                scroller.TileSize = Mathf.Max(8f, backdrop.patternScale);
                scroller.Scroll = backdrop.patternScroll;
            }
            if (backdrop.vignette.a > 0f)
            {
                var vignette = MenuGraphics.NewImage(root, "Vignette", backdrop.vignette);
                vignette.sprite = MenuGraphics.Vignette();
            }
        }

        // ─── Labels and decorations ───────────────────────────────────

        TextMeshProUGUI AddLabel(string name, MenuLabel label, MenuTextStyle style, TMP_FontAsset font, MenuEntrance entrance)
        {
            if (label == null || !label.show || string.IsNullOrEmpty(label.text))
                return null;
            var slot = MenuGraphics.NewRect(name, Root);
            // Sized to the words with horizontal breathing room; on a narrow screen the slot shrinks to fit and the words shrink with it.
            var text = MenuGraphics.Text(slot, "Text", label.text, style, font, label.fontSize, autoSize: true);
            text.margin = new Vector4(24f, 6f, 24f, 6f);
            if (!label.text.Contains("\n"))
                text.textWrappingMode = TextWrappingModes.NoWrap;
            MenuGraphics.SetText(text, label.text);
            Vector2 size = text.GetPreferredValues(label.text, Theme.referenceResolution.x * 0.86f, 0f);
            size += new Vector2(64f, 24f);
            MenuGraphics.Place(slot, label.anchor, label.offset, size);
            AddMotion(slot.gameObject, label.motion, entrance, label.rotation);
            MenuResponsive.Track(slot, MenuResponsive.From(label.anchor, label.offset, size), label.portrait, MenuElementKind.Text, Responsive);
            return text;
        }

        void AddDecoration(MenuDecoration decoration)
        {
            var slot = MenuGraphics.NewRect("Decoration", Root);
            var style = StyleNamed(decoration.style);
            Vector2 size = decoration.size;
            TextMeshProUGUI text = null;
            if (!string.IsNullOrEmpty(decoration.text))
            {
                text = MenuGraphics.Text(slot, "Text", decoration.text, style, Font, decoration.fontSize, autoSize: size.sqrMagnitude > 0f);
                if (decoration.textColor.a > 0f)
                {
                    text.enableVertexGradient = false;
                    text.color = decoration.textColor;
                }
                if (size.sqrMagnitude <= 0f)
                {
                    size = text.GetPreferredValues(decoration.text, 900f, 0f) + new Vector2(48f, 24f);
                    // Round shapes need room around the words.
                    if (decoration.shape == MenuShape.Burst || decoration.shape == MenuShape.Circle)
                        size = Vector2.one * Mathf.Max(size.x, size.y) * 1.25f;
                }
            }
            if (size.sqrMagnitude <= 0f)
                size = new Vector2(120f, 120f);

            if (decoration.shape != MenuShape.None)
            {
                var surface = new MenuSurfaceStyle
                {
                    shape = decoration.shape,
                    sprite = decoration.sprite,
                    fill = decoration.fill,
                    borderColor = decoration.borderColor,
                    borderWidth = decoration.borderWidth,
                    shadowColor = new Color(0f, 0f, 0f, 0.35f),
                    shadowOffset = new Vector2(6f, -6f),
                    cornerRadius = 20f,
                };
                var background = MenuGraphics.NewRect("Shape", slot);
                MenuGraphics.Fill(background);
                background.SetAsFirstSibling();
                MenuGraphics.Surface(background, surface);
            }
            MenuGraphics.Place(slot, decoration.anchor, decoration.offset, size);
            AddMotion(slot.gameObject, decoration.motion, Theme.entrance, decoration.rotation);
            MenuResponsive.Track(slot, MenuResponsive.From(decoration.anchor, decoration.offset, size), decoration.portrait,
                MenuElementKind.Text, Responsive);
        }

        /// <summary>
        /// A styled text placed at an anchor (fixed size box; long text shrinks to fit). Its slot carries a motion
        /// driver (entrance + idle), returned through 'motion' so callers can replay the entrance.
        /// </summary>
        public TextMeshProUGUI AddText(Transform parent, string name, string value, MenuTextStyle style, Vector2 anchor, Vector2 offset,
            Vector2 size, out MenuMotionDriver motion, float fontSize = 0f, TMP_FontAsset font = null, MenuEntrance entrance = null,
            MenuMotion idle = null, float rotation = 0f)
        {
            var slot = MenuGraphics.NewRect(name, parent != null ? parent : Root);
            MenuGraphics.Place(slot, anchor, offset, size);
            var text = MenuGraphics.Text(slot, "Text", value, style, font ?? Font, fontSize);
            motion = AddMotion(slot.gameObject, idle, entrance, rotation);
            MenuResponsive.Track(slot, MenuResponsive.From(anchor, offset, size), null, MenuElementKind.Text, Responsive);
            return text;
        }

        /// <summary>Gives a slot an entrance (taking the next cascade delay), idle motion and resting rotation.</summary>
        public MenuMotionDriver AddMotion(GameObject target, MenuMotion idle, MenuEntrance entrance, float rotation)
        {
            var driver = target.AddComponent<MenuMotionDriver>();
            driver.Idle = idle;
            driver.Entrance = entrance;
            driver.EntranceDelay = NextDelay(entrance);
            driver.BaseRotation = rotation;
            return driver;
        }

        // ─── Lists ────────────────────────────────────────────────────

        /// <summary>
        /// The area items are laid out in (column, row or grid; on a portrait screen the list's "portrait" version).
        /// Items that don't fit scroll with the wheel, a drag or the keyboard. Returns the container to pass to
        /// <see cref="AddButton"/>.
        /// </summary>
        public RectTransform AddList(MenuList list, Vector2 defaultItemSize, Transform parent = null)
        {
            var area = MenuGraphics.NewRect("List", parent != null ? parent : Root);
            MenuGraphics.Place(area, list.anchor, list.offset, list.size);
            var viewport = MenuGraphics.NewRect("Viewport", area);
            MenuGraphics.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear; // catches drags/wheel on empty space

            var content = MenuGraphics.NewRect("Items", viewport);
            content.gameObject.AddComponent<ContentSizeFitter>();

            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = true;
            area.gameObject.AddComponent<MenuScrollFollow>().Scroll = scroll;

            var marker = content.gameObject.AddComponent<MenuListMarker>();
            var portrait = list.portrait;
            MenuResponsive.Track(area, MenuResponsive.From(list.anchor, list.offset, list.size),
                portrait != null && portrait.use ? MenuResponsive.From(portrait.anchor, portrait.offset, portrait.size) : null,
                MenuElementKind.Container, Responsive);
            var layout = area.gameObject.AddComponent<MenuListLayout>();
            layout.Landscape = list;
            layout.Portrait = portrait;
            layout.DefaultItemSize = defaultItemSize;
            layout.Content = content;
            layout.Scroll = scroll;
            layout.Marker = marker;
            layout.Style = Responsive;
            marker.Layout = layout;
            layout.Apply();
            return content;
        }

        // ─── Buttons ──────────────────────────────────────────────────

        /// <summary>Adds a button to a list from <see cref="AddList"/> (or any parent with a layout group).</summary>
        public MenuButton AddButton(RectTransform list, string text, MenuButtonStyle style, Action onClick)
        {
            var marker = list.GetComponent<MenuListMarker>();
            Vector2 size = marker != null ? (marker.Layout != null ? marker.Layout.ItemSize() : marker.ItemSize) : style.size;
            var slot = MenuGraphics.NewRect("Item", list);
            slot.sizeDelta = size;
            var element = slot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = size.x;
            element.preferredHeight = element.minHeight = size.y;
            // A list sizes its own items; a button in any other group (a settings row) keeps the touch size itself.
            if (marker == null)
                MenuResponsive.TrackInGroup(slot, element, size, MenuElementKind.Button, Responsive);
            return Finish(MenuButton.Create(slot, style, text, Font, Theme.entrance, NextDelay(Theme.entrance), buttons.Count, Sounds), onClick);
        }

        /// <summary>
        /// Adds a free-standing button at an anchor (like the back button), or at 'portrait' on a portrait screen.
        /// It stays on screen and is at least the touch size there.
        /// </summary>
        public MenuButton AddButtonAt(Vector2 anchor, Vector2 offset, string text, MenuButtonStyle style, Action onClick, Transform parent = null,
            MenuPlacement portrait = null)
        {
            var slot = MenuGraphics.NewRect("Button", parent != null ? parent : Root);
            MenuGraphics.Place(slot, anchor, offset, style.size);
            MenuResponsive.Track(slot, MenuResponsive.From(anchor, offset, style.size), portrait, MenuElementKind.Button, Responsive);
            return Finish(MenuButton.Create(slot, style, text, Font, Theme.entrance, NextDelay(Theme.entrance), buttons.Count, Sounds), onClick);
        }

        public MenuButton AddBackButton(Action onClick)
        {
            Back = onClick;
            return AddButtonAt(Layout.backAnchor, Layout.backOffset, Layout.backText, Theme.backButton, onClick, null, Layout.portraitBack);
        }

        MenuButton Finish(MenuButton button, Action onClick)
        {
            if (onClick != null)
                button.Clicked += onClick;
            buttons.Add(button);
            return button;
        }

        public void SetInteractable(bool interactable)
        {
            foreach (var button in buttons)
            {
                if (button != null)
                    button.interactable = interactable;
            }
        }

        // ─── Stats chips ──────────────────────────────────────────────

        /// <summary>A pill showing a value (health, diamonds) in the stats corner. Returns its text to update.</summary>
        public TextMeshProUGUI AddChip(string value, Color? textColor = null)
        {
            if (stats == null)
            {
                stats = MenuGraphics.NewRect("Stats", Root);
                stats.anchorMin = stats.anchorMax = stats.pivot = Layout.statsAnchor;
                stats.anchoredPosition = Layout.statsOffset;
                MenuResponsive.Track(stats, MenuResponsive.From(Layout.statsAnchor, Layout.statsOffset, Vector2.zero), Layout.portraitStats,
                    MenuElementKind.Panel, Responsive, pivotAtAnchor: true, controlsSize: false);
                var row = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 18f;
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = false;
                var fitter = stats.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                AddMotion(stats.gameObject, null, Theme.entrance, 0f);
            }

            var chip = MenuGraphics.NewRect("Chip", stats);
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            var padding = Theme.chip.padding;
            layout.padding = new RectOffset((int)padding.x, (int)padding.x, (int)padding.y, (int)padding.y);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            MenuGraphics.Surface(chip, Theme.chip);
            var text = MenuGraphics.Text(chip, "Value", value, Theme.chipText, Font, 0f, autoSize: false);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (textColor.HasValue)
            {
                text.enableVertexGradient = false;
                text.color = textColor.Value;
            }
            return text;
        }

        // ─── Leaving ──────────────────────────────────────────────────

        /// <summary>Animates everything out (reverse entrance) over about 'duration' seconds.</summary>
        public void PlayExit(float duration = 0.3f)
        {
            SetInteractable(false);
            var drivers = GetComponentsInChildren<MenuMotionDriver>();
            for (int i = 0; i < drivers.Length; i++)
                drivers[i].Exit(duration, i * 0.02f);
        }

        // ─── Input ────────────────────────────────────────────────────

        void Update()
        {
            ApplyScaler();
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if (Back != null && ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                                 || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)))
            {
                var back = Back;
                back();
                return;
            }

            // Keyboard/gamepad users get a selection as soon as they start navigating.
            var events = EventSystem.current;
            if (events == null || (events.currentSelectedGameObject != null && events.currentSelectedGameObject.activeInHierarchy))
                return;
            bool navigate = (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                                                  || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                                                  || keyboard.tabKey.wasPressedThisFrame))
                            || (gamepad != null && (gamepad.dpad.ReadValue().sqrMagnitude > 0.25f || gamepad.leftStick.ReadValue().sqrMagnitude > 0.25f));
            if (!navigate)
                return;
            foreach (var button in buttons)
            {
                if (button != null && button.IsInteractable() && button.isActiveAndEnabled)
                {
                    events.SetSelectedGameObject(button.gameObject);
                    break;
                }
            }
        }

        static void EnsureEventSystem() => EventSystemGuard.Ensure();
    }

    /// <summary>Remembers a list's item size for buttons added to it.</summary>
    public sealed class MenuListMarker : MonoBehaviour
    {
        public Vector2 ItemSize;
        public MenuListLayout Layout;
    }

    /// <summary>Keeps the content inside the device's safe area (notches, rounded corners).</summary>
    public sealed class MenuSafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void Update()
        {
            var safe = Screen.safeArea;
            if (safe == applied && screen.x == Screen.width && screen.y == Screen.height)
                return;
            applied = safe;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;
            var rect = (RectTransform)transform;
            // Extra margins keep the HUD, timeline and corner buttons (Rage Quit) clear of the home bar at the bottom and,
            // on the web, of the beta/build badge the page draws over the top-left corner. Fractions, so any screen fits.
            var menu = GetComponentInParent<MenuCanvas>(true);
            var style = menu != null && menu.Theme != null ? menu.Responsive : new MenuResponsiveStyle();
            bool portrait = MenuScreen.Portrait;
            float bottom = style.bottomMargin;
            float top = Application.platform == RuntimePlatform.WebGLPlayer ? (portrait ? style.webTopMarginPortrait : style.webTopMargin) : 0f;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, Mathf.Min(safe.yMin / Screen.height + bottom, 0.5f));
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, Mathf.Max(safe.yMax / Screen.height - top, 0.5f));
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Tiles and drifts a backdrop pattern.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class MenuPatternScroller : MonoBehaviour
    {
        public float TileSize = 90f;
        public Vector2 Scroll;
        RawImage image;

        void Awake() => image = GetComponent<RawImage>();

        void Update()
        {
            var rect = ((RectTransform)transform).rect;
            Vector2 tiles = new Vector2(rect.width / TileSize, rect.height / TileSize);
            Vector2 offset = Scroll * (Time.unscaledTime / TileSize);
            image.uvRect = new Rect(-offset.x, -offset.y, tiles.x, tiles.y);
        }
    }

    /// <summary>Scrolls a list so the keyboard/gamepad selection stays visible.</summary>
    public sealed class MenuScrollFollow : MonoBehaviour
    {
        public ScrollRect Scroll;
        GameObject last;

        void LateUpdate()
        {
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (Scroll == null || selected == null || selected == last || !selected.transform.IsChildOf(Scroll.content))
            {
                last = selected;
                return;
            }
            last = selected;
            var viewport = Scroll.viewport;
            var target = (RectTransform)selected.transform;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = viewport.InverseTransformPoint(corners[0]);
            Vector2 max = viewport.InverseTransformPoint(corners[2]);
            var view = viewport.rect;
            Vector2 shift = Vector2.zero;
            if (max.y > view.yMax) shift.y = view.yMax - max.y;
            else if (min.y < view.yMin) shift.y = view.yMin - min.y;
            if (max.x > view.xMax) shift.x = view.xMax - max.x;
            else if (min.x < view.xMin) shift.x = view.xMin - min.x;
            Scroll.content.anchoredPosition += shift;
        }
    }
}
