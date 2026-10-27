using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Incoming warnings (Docs/Story.md "Incoming warnings"): a quiet, see-through tick just inside the screen's edge where a hostile shot
    /// or creature that is off screen will come in within about 1.5 s, a little stronger as it nears. Crowds merge into a soft band, at
    /// most four show, nearest arrival first. Everything is worked out again every frame from the live entities (<see cref="IncomingSet"/>),
    /// so a rewind needs nothing saved: during a rewind the markers fade out and come back from the replayed state. Fades run on the
    /// game clock (frozen while paused, slowed in slow motion). A pool of Images, no camera, no shader, no allocation per frame.
    /// No sound, no flash, no text. Presentation only: it reads the world and changes nothing in it.
    /// </summary>
    public sealed partial class GameplayHud
    {
        sealed class IncomingSlot
        {
            public Image Image;
            public RectTransform Tr;
            public bool Active;
            public float Alpha;
            public float TargetAlpha;
            public EdgeSide Side;
            public float Along, SpanMin, SpanMax, Eta;
            public int Count;
            public bool Creature;
        }

        static Sprite incomingSprite;
        static readonly Dictionary<Content.EntityDef, byte> incomingKinds = new Dictionary<Content.EntityDef, byte>();
        const byte KindIgnore = 1, KindShot = 2, KindCreature = 3;

        readonly List<IncomingSlot> incomingSlots = new List<IncomingSlot>();
        readonly IncomingSet incoming = new IncomingSet();
        readonly List<Rect> keepClear = new List<Rect>();
        readonly Vector3[] cornerBuffer = new Vector3[4];
        RectTransform incomingParent;
        float keepClearAt = -1f;
        IncomingWarningsConfig incomingConfig;

        /// <summary>Markers that stand for something coming this frame (the playtest reads it; fading-out ones are not counted).</summary>
        public int IncomingActive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < incomingSlots.Count; i++)
                    if (incomingSlots[i].Active)
                        n++;
                return n;
            }
        }

        /// <summary>The markers' own state this frame (edge, place along it, seconds to arrival, drawn opacity); returns how many are active.</summary>
        public int IncomingState(List<IncomingMarker> markers, List<float> alphas)
        {
            markers?.Clear();
            alphas?.Clear();
            int n = 0;
            for (int i = 0; i < incomingSlots.Count; i++)
            {
                var s = incomingSlots[i];
                if (!s.Active)
                    continue;
                n++;
                markers?.Add(new IncomingMarker { Side = s.Side, Along = s.Along, SpanMin = s.SpanMin, SpanMax = s.SpanMax, Eta = s.Eta, Count = s.Count, Creature = s.Creature });
                alphas?.Add(s.Alpha);
            }
            return n;
        }

        /// <summary>The screen rectangles (pixels) of every marker that is drawn at all, including fading ones.</summary>
        public void IncomingScreenRects(List<Rect> rects)
        {
            rects.Clear();
            for (int i = 0; i < incomingSlots.Count; i++)
            {
                var s = incomingSlots[i];
                if (s.Alpha <= 0.01f)
                    continue;
                s.Tr.GetWorldCorners(cornerBuffer);
                var canvas = menu.GetComponent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[2]);
                Vector2 c = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[1]);
                Vector2 d = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[3]);
                float xMin = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)), xMax = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
                float yMin = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)), yMax = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
                rects.Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
            }
        }

        void BuildIncoming()
        {
            var def = GameDefinition.Instance;
            incomingConfig = def != null && def.incomingWarnings != null ? def.incomingWarnings : new IncomingWarningsConfig();
            incomingParent = (RectTransform)transform;
            int slots = Mathf.Max(1, incomingConfig.maxMarkers) + 2; // a few extra so ones fading out never take a new one's place
            for (int i = 0; i < slots; i++)
            {
                var image = MenuGraphics.NewImage(incomingParent, "Incoming" + i, new Color(1f, 1f, 1f, 0f));
                image.sprite = IncomingSprite;
                image.raycastTarget = false;
                image.enabled = false;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                // Under the HUD's own widgets (the safe-area root), over the flash and fade.
                rect.SetSiblingIndex(menu.Root != null ? menu.Root.GetSiblingIndex() : 2);
                incomingSlots.Add(new IncomingSlot { Image = image, Tr = rect });
            }
        }

        /// <summary>A soft tick: brightest at its top row (the screen's edge), fading inward, with rounded, fading ends.</summary>
        static Sprite IncomingSprite
        {
            get
            {
                if (incomingSprite != null)
                    return incomingSprite;
                const int w = 64, h = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "IncomingTick",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    float v = Mathf.SmoothStep(0f, 1f, (y + 0.5f) / h); // brightest at the top row (the last one), fading inward
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w * 2f - 1f; // -1..1 along the edge
                        float ends = 1f - Mathf.SmoothStep(0.35f, 1f, Mathf.Abs(u));
                        pixels[y * w + x] = new Color(1f, 1f, 1f, v * ends);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                incomingSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                incomingSprite.hideFlags = HideFlags.DontSave;
                return incomingSprite;
            }
        }

        static byte KindOf(Content.EntityDef def)
        {
            if (def == null)
                return KindIgnore;
            if (incomingKinds.TryGetValue(def, out byte known))
                return known;
            byte kind = KindIgnore;
            if (def.team == Content.Team.Enemy && def.abilities != null)
            {
                foreach (var ability in def.abilities)
                {
                    if (ability is Content.ProjectileAbility)
                    {
                        kind = KindShot;
                        break;
                    }
                    if (ability is Content.ContactDamageAbility || ability is Content.ShootAbility || ability is Content.LaserSweepAbility)
                        kind = KindCreature;
                }
            }
            incomingKinds[def] = kind;
            return kind;
        }

        /// <summary>The playtest runs the update again and again in one frame to count what it allocates.</summary>
        public void TickIncomingForTest() => UpdateIncoming();

        void UpdateIncoming()
        {
            if (incomingSlots.Count == 0 || world == null)
                return;
            // Paused: everything stays exactly as it is (the world is frozen too).
            if (Paused)
                return;
            var clock = GameClock.Instance;
            bool rewinding = clock != null && clock.IsRewinding;
            var cfg = incomingConfig;
            bool on = cfg.enabled && GameSettings.IncomingWarnings && !rewinding && !Blocked
                      && string.IsNullOrEmpty(world.SceneId) && world.Player != null && world.Player.IsAlive;

            incoming.Begin(world.View, cfg.lookaheadSeconds, cfg.mergeFraction, cfg.maxMarkers);
            if (on)
            {
                var live = world.Live;
                for (int i = 0; i < live.Count; i++)
                {
                    var e = live[i];
                    if (e == null || !e.IsAlive || e.Body == null || !e.gameObject.activeInHierarchy)
                        continue;
                    byte kind = KindOf(e.Def);
                    if (kind == KindIgnore)
                        continue;
                    incoming.Add(e.Position, e.Body.linearVelocity, kind == KindCreature);
                }
            }
            incoming.End();

            // Fade clock: the game's (slow motion scales it, a pause stops it); a rewind fades them out at real speed.
            float dt = rewinding ? Time.unscaledDeltaTime : clock != null ? clock.DeltaTime : Time.deltaTime;
            AssignIncoming(cfg);
            DrawIncoming(cfg, dt);
        }

        void AssignIncoming(IncomingWarningsConfig cfg)
        {
            for (int i = 0; i < incomingSlots.Count; i++)
                incomingSlots[i].Active = false;
            var markers = incoming.Markers;
            for (int m = 0; m < markers.Count; m++)
            {
                var marker = markers[m];
                IncomingSlot best = null;
                float bestDistance = 0.3f;
                // The same band keeps its slot from frame to frame: the nearest live slot on the same edge.
                for (int i = 0; i < incomingSlots.Count; i++)
                {
                    var s = incomingSlots[i];
                    if (s.Active || s.Alpha <= 0f || s.Side != marker.Side)
                        continue;
                    float d = Mathf.Abs(s.Along - marker.Along);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = s;
                    }
                }
                if (best == null)
                {
                    for (int i = 0; i < incomingSlots.Count; i++)
                    {
                        var s = incomingSlots[i];
                        if (!s.Active && (best == null || s.Alpha < best.Alpha))
                            best = s;
                    }
                }
                if (best == null)
                    break;
                if (best.Alpha <= 0f || best.Side != marker.Side)
                    best.Alpha = 0f; // a different edge fades in from nothing rather than sliding across the screen
                best.Active = true;
                best.Side = marker.Side;
                best.Along = marker.Along;
                best.SpanMin = marker.SpanMin;
                best.SpanMax = marker.SpanMax;
                best.Eta = marker.Eta;
                best.Count = marker.Count;
                best.Creature = marker.Creature;
                best.TargetAlpha = IncomingSet.AlphaFor(marker.Eta, cfg);
            }
        }

        void DrawIncoming(IncomingWarningsConfig cfg, float dt)
        {
            Rect canvas = incomingParent.rect;
            float shortSide = Mathf.Min(canvas.width, canvas.height);
            float depth = cfg.depth * shortSide, length = cfg.length * shortSide, inset = cfg.inset * shortSide;
            RefreshKeepClear(canvas);
            float fadeIn = cfg.nearAlpha / cfg.fadeInSeconds, fadeOut = cfg.nearAlpha / cfg.fadeOutSeconds;
            for (int i = 0; i < incomingSlots.Count; i++)
            {
                var s = incomingSlots[i];
                float target = s.Active ? s.TargetAlpha : 0f;
                s.Alpha = Mathf.MoveTowards(s.Alpha, target, (target > s.Alpha ? fadeIn : fadeOut) * dt);
                if (s.Alpha <= 0.002f && !s.Active)
                {
                    s.Alpha = 0f;
                    if (s.Image.enabled)
                        s.Image.enabled = false;
                    continue;
                }
                if (!s.Image.enabled)
                    s.Image.enabled = true;

                // Stronger as it nears: a little deeper, and a band is as long as what it stands for.
                float near = 1f - Mathf.Clamp01((s.Eta - cfg.nearSeconds) / Mathf.Max(0.01f, cfg.lookaheadSeconds - cfg.nearSeconds));
                bool horizontal = s.Side == EdgeSide.Top || s.Side == EdgeSide.Bottom;
                float edgeLength = horizontal ? canvas.width : canvas.height;
                float span = (s.SpanMax - s.SpanMin) * edgeLength;
                float thisLength = Mathf.Min(length + span, edgeLength * 0.8f);
                float thisDepth = depth * Mathf.Lerp(0.8f, 1.2f, near);
                float margin = thisLength * 0.5f + edgeLength * 0.04f;
                float along = PlaceAlong(s, canvas, horizontal, thisLength, thisDepth, inset, margin);

                Vector2 point, inward;
                switch (s.Side)
                {
                    case EdgeSide.Top: point = new Vector2(canvas.xMin + along, canvas.yMax); inward = new Vector2(0f, -1f); break;
                    case EdgeSide.Bottom: point = new Vector2(canvas.xMin + along, canvas.yMin); inward = new Vector2(0f, 1f); break;
                    case EdgeSide.Left: point = new Vector2(canvas.xMin, canvas.yMin + along); inward = new Vector2(1f, 0f); break;
                    default: point = new Vector2(canvas.xMax, canvas.yMin + along); inward = new Vector2(-1f, 0f); break;
                }
                Vector2 center = point + inward * (inset + thisDepth * 0.5f);
                s.Tr.sizeDelta = new Vector2(thisLength, thisDepth);
                s.Tr.anchoredPosition = center - canvas.min;
                s.Tr.localRotation = Quaternion.Euler(0f, 0f, s.Side == EdgeSide.Top ? 0f : s.Side == EdgeSide.Bottom ? 180f : s.Side == EdgeSide.Left ? 90f : -90f);
                Color tint = s.Creature ? cfg.creatureColor : cfg.shotColor;
                tint.a = s.Alpha;
                s.Image.color = tint;
            }
        }

        // Where on its edge the marker sits (canvas units from the edge's start): its place, kept off the corners and
        // nudged along the edge out of the way of any HUD widget (the health, the buttons, the touch pad).
        float PlaceAlong(IncomingSlot s, Rect canvas, bool horizontal, float length, float depth, float inset, float margin)
        {
            float edgeLength = horizontal ? canvas.width : canvas.height;
            float wanted = Mathf.Clamp(s.Along * edgeLength, margin, edgeLength - margin);
            if (keepClear.Count == 0)
                return wanted;
            float step = edgeLength * 0.06f;
            for (int k = 0; k <= 8; k++)
            {
                float offset = ((k + 1) / 2) * step * ((k & 1) == 1 ? 1f : -1f);
                float along = Mathf.Clamp(wanted + offset, margin, edgeLength - margin);
                if (!HitsHud(s.Side, canvas, along, length, depth, inset))
                    return along;
            }
            return wanted;
        }

        bool HitsHud(EdgeSide side, Rect canvas, float along, float length, float depth, float inset)
        {
            Rect r;
            switch (side)
            {
                case EdgeSide.Top: r = new Rect(canvas.xMin + along - length * 0.5f, canvas.yMax - inset - depth, length, depth); break;
                case EdgeSide.Bottom: r = new Rect(canvas.xMin + along - length * 0.5f, canvas.yMin + inset, length, depth); break;
                case EdgeSide.Left: r = new Rect(canvas.xMin + inset, canvas.yMin + along - length * 0.5f, depth, length); break;
                default: r = new Rect(canvas.xMax - inset - depth, canvas.yMin + along - length * 0.5f, depth, length); break;
            }
            for (int i = 0; i < keepClear.Count; i++)
                if (keepClear[i].Overlaps(r))
                    return true;
            return false;
        }

        /// <summary>The rectangles (canvas units) of the HUD's widgets, read a few times a second: health, buttons, the touch pad, the objective.</summary>
        void RefreshKeepClear(Rect canvas)
        {
            if (Time.unscaledTime < keepClearAt)
                return;
            keepClearAt = Time.unscaledTime + 0.25f;
            keepClear.Clear();
            var root = menu.Root;
            if (root == null)
                return;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;
                child.GetWorldCorners(cornerBuffer);
                Vector2 a = incomingParent.InverseTransformPoint(cornerBuffer[0]);
                Vector2 b = incomingParent.InverseTransformPoint(cornerBuffer[2]);
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                // A layer that covers the screen (a panel, the pause menu) is not a widget to stay clear of.
                if (r.width > canvas.width * 0.6f && r.height > canvas.height * 0.6f || r.width < 1f || r.height < 1f)
                    continue;
                keepClear.Add(r);
            }
        }
    }
}
