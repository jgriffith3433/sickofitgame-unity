using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>What a laid-out element is, for <see cref="MenuLayoutAudit"/>.</summary>
    public enum MenuElementKind
    {
        /// <summary>Something to press: never overlaps another, never off screen, never smaller than a thumb.</summary>
        Button,
        /// <summary>A readout or bar (health, diamonds, progress): never overlaps a button or another panel.</summary>
        Panel,
        /// <summary>Words: kept on screen, may sit over other things (banners).</summary>
        Text,
        /// <summary>An area that holds other elements (a list): not checked itself.</summary>
        Container,
    }

    /// <summary>The screen's shape and the touch size, in one place for every menu and the HUD.</summary>
    public static class MenuScreen
    {
        /// <summary>A phone held upright (or any screen taller than wide).</summary>
        public static bool Portrait => Screen.height > Screen.width;

        /// <summary>Buttons get the minimum touch size: always in portrait, in landscape only if the theme asks.</summary>
        public static bool TouchLayout(MenuResponsiveStyle style) => Portrait || (style != null && style.touchSizeInLandscape);

        /// <summary>The short side of the layout space (1080 in 1920x1080 and in 1080x1920).</summary>
        public static float ShortReference(MenuResponsiveStyle style)
        {
            var portrait = style != null ? style.portraitReferenceResolution : new Vector2(1080f, 1920f);
            float side = Mathf.Min(portrait.x, portrait.y);
            return side > 0f ? side : 1080f;
        }

        /// <summary>The smallest button, in screen pixels (the theme's minTouchSize on the short side's scale).</summary>
        public static float MinTouchPixels(MenuResponsiveStyle style)
        {
            float size = style != null ? style.minTouchSize : 124f;
            return Mathf.Min(Screen.width, Screen.height) * size / ShortReference(style);
        }
    }

    /// <summary>
    /// Places one menu or HUD element for the screen's shape: its landscape or portrait <see cref="MenuPlacement"/>,
    /// shrunk to fit and kept inside its parent (the safe area, an overlay, a panel), and grown to the minimum touch
    /// size if it is a button on a touch layout. Re-applies itself when the screen turns or the parent resizes.
    /// Elements in a layout group (settings rows) only get their size, through their <see cref="LayoutElement"/>.
    /// </summary>
    public sealed class MenuResponsive : MonoBehaviour
    {
        /// <summary>Every enabled element, for the layout audit.</summary>
        public static readonly List<MenuResponsive> Active = new List<MenuResponsive>();

        public MenuPlacement Landscape;
        public MenuPlacement Portrait;
        public MenuElementKind Kind = MenuElementKind.Panel;
        /// <summary>The pivot sits on the anchor, so the element grows inward from its corner (chips, health).</summary>
        public bool PivotAtAnchor;
        /// <summary>Off when a ContentSizeFitter sizes the element (chips): it is only positioned.</summary>
        public bool ControlsSize = true;
        /// <summary>Grow to the minimum touch size on touch layouts.</summary>
        public bool Touch;
        /// <summary>In a layout group: the size goes through this element and the group positions it.</summary>
        public LayoutElement Element;

        MenuResponsiveStyle style;
        RectTransform rect;
        bool appliedPortrait;
        bool applied;
        Vector2 parentSize;
        Vector2 ownSize;
        bool hidden;

        public static MenuResponsive Track(RectTransform rect, MenuPlacement landscape, MenuPlacement portrait, MenuElementKind kind,
            MenuResponsiveStyle style, bool pivotAtAnchor = false, bool controlsSize = true)
        {
            if (!rect.TryGetComponent<MenuResponsive>(out var responsive))
                responsive = rect.gameObject.AddComponent<MenuResponsive>();
            responsive.Landscape = landscape ?? new MenuPlacement();
            responsive.Portrait = portrait;
            responsive.Kind = kind;
            responsive.style = style;
            responsive.PivotAtAnchor = pivotAtAnchor;
            responsive.ControlsSize = controlsSize;
            responsive.Touch = kind == MenuElementKind.Button;
            responsive.Apply();
            return responsive;
        }

        /// <summary>
        /// Tracks an element in a layout group: its size (and touch size) only. A size axis of 0 or less is left to
        /// the group. 'touch' grows it to the touch size (default: buttons do).
        /// </summary>
        public static MenuResponsive TrackInGroup(RectTransform rect, LayoutElement element, Vector2 size, MenuElementKind kind, MenuResponsiveStyle style,
            bool? touch = null)
        {
            if (!rect.TryGetComponent<MenuResponsive>(out var responsive))
                responsive = rect.gameObject.AddComponent<MenuResponsive>();
            responsive.Landscape = new MenuPlacement { use = true, size = size };
            responsive.Kind = kind;
            responsive.style = style;
            responsive.Element = element;
            responsive.Touch = touch ?? kind == MenuElementKind.Button;
            responsive.Apply();
            return responsive;
        }

        /// <summary>A placement from the old anchor/offset/size triple (the landscape layouts in code and JSON).</summary>
        public static MenuPlacement From(Vector2 anchor, Vector2 offset, Vector2 size) =>
            new MenuPlacement { use = true, anchor = anchor, offset = offset, size = size };

        void Awake() => rect = (RectTransform)transform;

        void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
            Apply();
        }

        void OnDisable() => Active.Remove(this);

        void LateUpdate()
        {
            var parent = rect.parent as RectTransform;
            Vector2 size = parent != null ? parent.rect.size : Vector2.zero;
            if (!applied || appliedPortrait != MenuScreen.Portrait || size != parentSize || (!ControlsSize && rect.rect.size != ownSize))
                Apply();
        }

        /// <summary>The placement for the current screen shape.</summary>
        public MenuPlacement Current =>
            MenuScreen.Portrait && Portrait != null && Portrait.use ? Portrait : Landscape;

        public void Apply()
        {
            // AddComponent runs OnEnable before Track has filled the placements in.
            if (Landscape == null)
                return;
            if (rect == null)
                rect = (RectTransform)transform;
            if (style == null)
            {
                var canvas = GetComponentInParent<MenuCanvas>(true);
                style = canvas != null && canvas.Theme != null ? canvas.Theme.responsive : null;
            }
            style ??= new MenuResponsiveStyle();
            bool portrait = MenuScreen.Portrait;
            applied = true;
            appliedPortrait = portrait;
            var parent = rect.parent as RectTransform;
            parentSize = parent != null ? parent.rect.size : Vector2.zero;

            SetHidden(portrait && Portrait != null && Portrait.hide);
            var place = Current;
            Vector2 size = place.size.x > 0f || place.size.y > 0f ? place.size : Landscape.size;
            if (size.x <= 0f) size.x = Landscape.size.x;
            if (size.y <= 0f) size.y = Landscape.size.y;
            if (Element != null)
            {
                // Only the axes it sizes (a settings row's width is the panel's).
                bool grow = Touch && MenuScreen.TouchLayout(style);
                if (size.x > 0f)
                    Element.minWidth = Element.preferredWidth = grow ? Mathf.Max(size.x, style.minTouchSize) : size.x;
                if (size.y > 0f)
                    Element.minHeight = Element.preferredHeight = grow ? Mathf.Max(size.y, style.minTouchSize) : size.y;
                rect.sizeDelta = new Vector2(Element.preferredWidth > 0f ? Element.preferredWidth : rect.sizeDelta.x,
                    Element.preferredHeight > 0f ? Element.preferredHeight : rect.sizeDelta.y);
                return;
            }
            if (Touch && MenuScreen.TouchLayout(style))
                size = Vector2.Max(size, Vector2.one * style.minTouchSize);

            float margin = portrait ? Mathf.Max(0f, style.portraitMargin) : 0f;
            Vector2 room = parentSize - Vector2.one * (2f * margin);
            rect.anchorMin = rect.anchorMax = place.anchor;
            rect.pivot = PivotAtAnchor ? place.anchor : new Vector2(0.5f, 0.5f);
            if (ControlsSize)
            {
                // Shrink to fit (text then auto-sizes down); a touch target never shrinks below the touch size.
                float floor = Touch && MenuScreen.TouchLayout(style) ? style.minTouchSize : 0f;
                if (room.x > 0f) size.x = Mathf.Max(Mathf.Min(size.x, room.x), Mathf.Min(floor, room.x));
                if (room.y > 0f) size.y = Mathf.Max(Mathf.Min(size.y, room.y), Mathf.Min(floor, room.y));
                rect.sizeDelta = size;
            }
            Vector2 actual = ControlsSize ? size : rect.rect.size;
            ownSize = rect.rect.size;

            Vector2 position = place.offset;
            if (parentSize.x > 0f && parentSize.y > 0f)
            {
                for (int axis = 0; axis < 2; axis++)
                {
                    float extent = parentSize[axis];
                    float start = place.anchor[axis] * extent + position[axis] - rect.pivot[axis] * actual[axis];
                    float low = margin, high = extent - margin - actual[axis];
                    start = high < low ? (extent - actual[axis]) * 0.5f : Mathf.Clamp(start, low, high);
                    position[axis] = start - place.anchor[axis] * extent + rect.pivot[axis] * actual[axis];
                }
            }
            rect.anchoredPosition = position;
            if (TryGetComponent<MenuMotionDriver>(out var motion))
                motion.RestPosition = position;
        }

        void SetHidden(bool hide)
        {
            if (hide == hidden)
                return;
            hidden = hide;
            // The slot stays active (so it can come back when the screen turns); its contents go.
            foreach (Transform child in transform)
                child.gameObject.SetActive(!hide);
        }

        public bool Hidden => hidden;
    }

    /// <summary>
    /// A menu list's arrangement for the screen's shape: on a portrait screen it takes its
    /// <see cref="MenuPortraitList"/> (a column instead of a row, a narrower grid), its items grow to the touch size,
    /// and a grid's column count follows the width. Rebuilt when the screen turns.
    /// </summary>
    public sealed class MenuListLayout : MonoBehaviour
    {
        public MenuList Landscape;
        public MenuPortraitList Portrait;
        public Vector2 DefaultItemSize;
        public RectTransform Content;
        public ScrollRect Scroll;
        public MenuListMarker Marker;
        public MenuResponsiveStyle Style;

        public const float Pad = 36f; // room for hover scale and shadows inside the clipping viewport

        bool applied;
        bool appliedPortrait;
        Vector2 areaSize;
        MenuListDirection? direction;

        bool UsePortrait => MenuScreen.Portrait && Portrait != null && Portrait.use;

        void LateUpdate()
        {
            var size = ((RectTransform)transform).rect.size;
            if (!applied || appliedPortrait != MenuScreen.Portrait || size != areaSize)
                Apply();
        }

        /// <summary>The item size for the current screen shape (what new items are sized to).</summary>
        public Vector2 ItemSize()
        {
            Vector2 item = Landscape.itemSize.sqrMagnitude > 0f ? Landscape.itemSize : DefaultItemSize;
            if (UsePortrait)
            {
                if (Portrait.itemSize.x > 0f) item.x = Portrait.itemSize.x;
                if (Portrait.itemSize.y > 0f) item.y = Portrait.itemSize.y;
            }
            var style = Style ?? new MenuResponsiveStyle();
            if (MenuScreen.TouchLayout(style))
                item = Vector2.Max(item, Vector2.one * style.minTouchSize);
            var area = ((RectTransform)transform).rect.size;
            // Never wider than the list itself (a column of buttons on a narrow screen).
            if (area.x > Pad * 2f && Direction != MenuListDirection.Horizontal)
                item.x = Mathf.Min(item.x, area.x - Pad * 2f);
            return item;
        }

        MenuListDirection Direction => UsePortrait ? Portrait.direction : Landscape.direction;

        public void Apply()
        {
            applied = true;
            appliedPortrait = MenuScreen.Portrait;
            if (TryGetComponent<MenuResponsive>(out var placement))
                placement.Apply();
            areaSize = ((RectTransform)transform).rect.size;

            var dir = Direction;
            Vector2 spacing = UsePortrait ? Portrait.spacing : Landscape.spacing;
            int columns = UsePortrait ? Portrait.columns : Landscape.columns;
            Vector2 item = ItemSize();
            Marker.ItemSize = item;
            Configure(dir, spacing, item, columns);

            foreach (Transform child in Content)
            {
                if (!child.TryGetComponent<LayoutElement>(out var element))
                    continue;
                element.preferredWidth = element.minWidth = item.x;
                element.preferredHeight = element.minHeight = item.y;
                ((RectTransform)child).sizeDelta = item;
            }
        }

        void Configure(MenuListDirection dir, Vector2 spacing, Vector2 item, int columns)
        {
            var padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
            bool horizontal = dir == MenuListDirection.Horizontal;
            if (direction != dir)
            {
                // One layout group per object: swap it when the arrangement changes.
                if (Content.TryGetComponent<LayoutGroup>(out var old))
                    DestroyImmediate(old);
                if (dir == MenuListDirection.Grid)
                    Content.gameObject.AddComponent<GridLayoutGroup>();
                else if (horizontal)
                    Content.gameObject.AddComponent<HorizontalLayoutGroup>();
                else
                    Content.gameObject.AddComponent<VerticalLayoutGroup>();
                direction = dir;
                Content.anchoredPosition = Vector2.zero;
            }

            var fitter = Content.GetComponent<ContentSizeFitter>();
            if (dir == MenuListDirection.Grid)
            {
                Content.anchorMin = new Vector2(0f, 0.5f);
                Content.anchorMax = new Vector2(1f, 0.5f);
                Content.pivot = new Vector2(0.5f, 0.5f);
                Content.sizeDelta = new Vector2(0f, Content.sizeDelta.y);
                var grid = Content.GetComponent<GridLayoutGroup>();
                grid.cellSize = item;
                grid.spacing = spacing;
                grid.padding = padding;
                grid.childAlignment = TextAnchor.MiddleCenter;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                float width = areaSize.x > 0f ? areaSize.x : Landscape.size.x;
                int fit = Mathf.Max(1, Mathf.FloorToInt((width - Pad * 2f + spacing.x) / (item.x + spacing.x)));
                grid.constraintCount = columns > 0 ? Mathf.Min(columns, fit) : fit;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                Content.anchorMin = Content.anchorMax = Content.pivot = new Vector2(0.5f, 0.5f);
                var group = Content.GetComponent<HorizontalOrVerticalLayoutGroup>();
                group.spacing = horizontal ? spacing.x : spacing.y;
                group.padding = padding;
                group.childAlignment = TextAnchor.MiddleCenter;
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = group.childForceExpandHeight = false;
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            Scroll.horizontal = horizontal;
            Scroll.vertical = !horizontal;
        }
    }

    /// <summary>
    /// Checks what is on screen right now: every button and HUD panel on screen and inside the safe area, no two
    /// overlapping, and buttons at least the touch size on a touch layout. Used by the "layout" playtest.
    /// Only the top-most popup of each canvas is checked (what it covers can't be pressed anyway).
    /// </summary>
    public static class MenuLayoutAudit
    {
        public sealed class Box
        {
            public string Name;
            public MenuElementKind Kind;
            public Rect Screen;
            public Transform Transform;
            public bool Touch;
        }

        public sealed class Result
        {
            public readonly List<Box> Boxes = new List<Box>();
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }

        static readonly Regex Tags = new Regex("<[^>]*>");

        public static Result Check(MenuResponsiveStyle style)
        {
            style ??= new MenuResponsiveStyle();
            var result = new Result();
            var seen = new HashSet<Transform>();

            foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
            {
                if (button == null || !button.isActiveAndEnabled || !Visible(button.transform))
                    continue;
                // Measure the slot (the button's visual springs and tilts inside it).
                var slot = button.transform.parent != null ? button.transform.parent : button.transform;
                string label = button.Label != null ? Tags.Replace(button.Label.text ?? "", "").Trim() : "";
                if (string.IsNullOrEmpty(label))
                {
                    var any = button.GetComponentInChildren<TextMeshProUGUI>();
                    label = any != null ? Tags.Replace(any.text ?? "", "").Trim() : "";
                }
                Add(result, seen, slot, $"button \"{(label.Length > 0 ? label : slot.name)}\"", MenuElementKind.Button, true);
            }
            foreach (var button in Object.FindObjectsByType<MobileControllerFramework.Button>(FindObjectsSortMode.None))
            {
                if (button == null || !button.isActiveAndEnabled || !Visible(button.transform))
                    continue;
                Add(result, seen, button.transform, $"button \"{button.name}\"", MenuElementKind.Button, true);
            }
            foreach (var element in MenuResponsive.Active)
            {
                if (element == null || !element.isActiveAndEnabled || element.Hidden || element.Kind == MenuElementKind.Container
                    || element.Kind == MenuElementKind.Button || !Visible(element.transform))
                    continue;
                Add(result, seen, element.transform, $"{element.Kind.ToString().ToLowerInvariant()} \"{element.name}\"", element.Kind, false);
            }

            var screen = new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);
            var safe = UnityEngine.Screen.safeArea;
            float minTouch = MenuScreen.MinTouchPixels(style) * 0.9f;
            bool touchLayout = MenuScreen.TouchLayout(style);
            const float slack = 2f;
            foreach (var box in result.Boxes)
            {
                var r = box.Screen;
                if (r.xMin < screen.xMin - slack || r.yMin < screen.yMin - slack || r.xMax > screen.xMax + slack || r.yMax > screen.yMax + slack)
                    result.Problems.Add($"{box.Name} leaves the screen ({Describe(r)} on a {screen.width:0}x{screen.height:0} screen)");
                else if (r.xMin < safe.xMin - slack || r.yMin < safe.yMin - slack || r.xMax > safe.xMax + slack || r.yMax > safe.yMax + slack)
                    result.Warnings.Add($"{box.Name} reaches outside the safe area ({Describe(r)}, safe area {Describe(safe)})");
                if (box.Touch && touchLayout && Mathf.Min(r.width, r.height) < minTouch)
                    result.Problems.Add($"{box.Name} is smaller than a thumb ({r.width:0}x{r.height:0} px, needs {minTouch / 0.9f:0} px)");
            }
            for (int i = 0; i < result.Boxes.Count; i++)
            {
                for (int j = i + 1; j < result.Boxes.Count; j++)
                {
                    var a = result.Boxes[i];
                    var b = result.Boxes[j];
                    if (a.Kind == MenuElementKind.Text || b.Kind == MenuElementKind.Text)
                        continue;
                    if (a.Transform.IsChildOf(b.Transform) || b.Transform.IsChildOf(a.Transform))
                        continue;
                    float w = Mathf.Min(a.Screen.xMax, b.Screen.xMax) - Mathf.Max(a.Screen.xMin, b.Screen.xMin);
                    float h = Mathf.Min(a.Screen.yMax, b.Screen.yMax) - Mathf.Max(a.Screen.yMin, b.Screen.yMin);
                    if (w > 3f && h > 3f)
                        result.Problems.Add($"{a.Name} overlaps {b.Name} ({w:0}x{h:0} px)");
                }
            }
            return result;
        }

        static string Describe(Rect r) => $"x {r.xMin:0}..{r.xMax:0}, y {r.yMin:0}..{r.yMax:0}";

        static void Add(Result result, HashSet<Transform> seen, Transform target, string name, MenuElementKind kind, bool touch)
        {
            if (!seen.Add(target) || !(target is RectTransform rect))
                return;
            if (!ScreenRect(rect, out var screen))
                return;
            // Inside a scrolling list only the visible part counts; a fully scrolled-away item isn't on screen.
            var mask = rect.GetComponentInParent<RectMask2D>();
            if (mask != null && mask.transform != rect && ScreenRect((RectTransform)mask.transform, out var clip, true))
            {
                float xMin = Mathf.Max(screen.xMin, clip.xMin), yMin = Mathf.Max(screen.yMin, clip.yMin);
                float xMax = Mathf.Min(screen.xMax, clip.xMax), yMax = Mathf.Min(screen.yMax, clip.yMax);
                if (xMax - xMin < 4f || yMax - yMin < 4f)
                    return;
                bool clipped = xMin > screen.xMin + 1f || yMin > screen.yMin + 1f || xMax < screen.xMax - 1f || yMax < screen.yMax - 1f;
                screen = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                if (clipped)
                    touch = false; // partly scrolled out: its size on screen isn't its real size
            }
            result.Boxes.Add(new Box { Name = name, Kind = kind, Screen = screen, Transform = rect, Touch = touch });
        }

        /// <summary>The element's rest rectangle in screen pixels, ignoring its own animated scale and tilt.</summary>
        static bool ScreenRect(RectTransform rect, out Rect screen, bool includeOwnScale = false)
        {
            screen = default;
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
                return false;
            canvas = canvas.rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var local = rect.rect;
            var parent = rect.parent;
            Vector3 origin = rect.localPosition;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            var corners = new[] { new Vector2(local.xMin, local.yMin), new Vector2(local.xMin, local.yMax), new Vector2(local.xMax, local.yMin), new Vector2(local.xMax, local.yMax) };
            foreach (var corner in corners)
            {
                Vector3 world = includeOwnScale || parent == null
                    ? rect.TransformPoint(corner)
                    : parent.TransformPoint(origin + (Vector3)corner);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, world);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            screen = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return screen.width > 0.5f && screen.height > 0.5f;
        }

        /// <summary>Drawn, and not under a popup that covers it.</summary>
        static bool Visible(Transform t)
        {
            var groups = t.GetComponentsInParent<CanvasGroup>();
            foreach (var group in groups)
            {
                if (group.alpha < 0.05f)
                    return false;
                if (group.ignoreParentGroups)
                    break;
            }
            var canvas = t.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled)
                return false;
            var top = TopLayer(canvas.rootCanvas.transform);
            return top == null || t.IsChildOf(top);
        }

        /// <summary>The last opened popup in a canvas (pause, settings, results, picker), or null.</summary>
        static Transform TopLayer(Transform canvas)
        {
            Transform top = null;
            foreach (var layer in canvas.GetComponentsInChildren<MenuLayer>())
            {
                if (layer.isActiveAndEnabled && layer.Blocks && (top == null || layer.Opened >= top.GetComponent<MenuLayer>().Opened))
                    top = layer.transform;
            }
            return top;
        }
    }

    /// <summary>Whether a touch lands on an on-screen button (so the touch controls under it leave it alone).</summary>
    public static class MenuHit
    {
        static readonly List<UnityEngine.EventSystems.RaycastResult> hits = new List<UnityEngine.EventSystems.RaycastResult>();

        public static bool OnButton(Vector2 screenPosition)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null)
                return false;
            hits.Clear();
            events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = screenPosition }, hits);
            foreach (var hit in hits)
            {
                if (hit.gameObject != null && hit.gameObject.GetComponentInParent<MenuButton>() != null)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Marks a popup layer (pause, settings, results, picker) that covers the rest of its canvas.</summary>
    public sealed class MenuLayer : MonoBehaviour
    {
        static int count;
        public int Opened;
        /// <summary>False for layers that don't take input (the in-level death countdown).</summary>
        public bool Blocks = true;

        void Awake() => Opened = ++count;
    }
}
