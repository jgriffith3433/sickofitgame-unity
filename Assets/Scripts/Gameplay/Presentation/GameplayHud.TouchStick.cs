using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SickOfIt.Core.Input;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Finger = UnityEngine.InputSystem.EnhancedTouch.Finger;
using EnhancedTouchSupport = UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The touch move stick: in portrait it rests in the free control strip below the playfield; in landscape it is a
    /// floating stick that only lights up where the thumb lands in the bottom-left zone. Idle is a faint 30% outline
    /// that fades while untouched so bullets behind it stay readable. A finger that goes down in its zone and drags
    /// left or right steers the hero through <c>PlayerController.TouchMove</c>; a flick up jumps. Fingers come from
    /// EnhancedTouch events. It shows on touch devices only (<see cref="TouchUiOn"/>), never on desktop. First-run
    /// hints go away the first time each control is used. The rewind button is grown to at least 48 px. Presentation only.
    /// </summary>
    public sealed partial class GameplayHud
    {
        const string StickMovedKey = "TouchStickMoved";
        const string StickRewoundKey = "TouchStickRewound";
        const float MinRewindPixels = 88f;
        /// <summary>Idle outline alpha (30%): faint enough that shots behind the landscape stick stay readable.</summary>
        const float IdleOutlineAlpha = 0.3f;
        /// <summary>How fast the stick fades between idle and held.</summary>
        const float StickFadeSpeed = 8f;

        /// <summary>The playtest's way of saying "pretend this is a phone" (a desktop has none of the touch controls).</summary>
        public static bool TouchUiForced;

        /// <summary>The touch controls belong here: a phone or tablet (an iPad in Safari too: TouchDevice), or the playtest's touch scenarios.</summary>
        public static bool TouchUiOn => SickOfIt.Core.Device.TouchDevice.IsPrimary || TouchUiForced;

        RectTransform stickZoneRect, stickRingRect, stickKnobRect;
        Image stickRing, stickKnob, stickFill;
        TextMeshProUGUI stickArrowL, stickArrowR;
        RectTransform stickMoveHintRoot, stickRewindHintRoot;
        Finger stickFinger;
        Vector2 stickStart;
        float stickAxis, stickRadius;
        bool stickJumped, stickBuilt, stickShown;
        bool stickMovedBefore, stickRewoundBefore;
        float stickVisualAlpha = IdleOutlineAlpha;

        /// <summary>The stick is on screen (the playtest checks it on a phone, and that it is not on desktop).</summary>
        public bool TouchStickShown => stickShown && stickZoneRect != null && stickZoneRect.gameObject.activeInHierarchy;

        /// <summary>The stick's ring, in screen pixels (the playtest checks it sits outside the playfield and drags inside it).</summary>
        public Rect TouchStickScreenRect => stickRingRect != null ? ScreenRect(stickRingRect) : default;

        /// <summary>The stick's touch zone, in screen pixels (portrait: the control strip; landscape: the bottom-left float zone).</summary>
        public Rect TouchStickZoneScreenRect => stickZoneRect != null ? ScreenRect(stickZoneRect) : default;

        /// <summary>The rewind button on screen, in pixels.</summary>
        public Rect RewindButtonScreenRect => rewindRoot != null ? ScreenRect(rewindRoot) : default;

        /// <summary>The stick's current axis, -1..1.</summary>
        public float TouchStickAxis => stickAxis;

        /// <summary>True while a finger is holding the stick (the playtest checks landscape float-under-thumb).</summary>
        public bool TouchStickHeld => stickFinger != null;

        /// <summary>Current drawn alpha of the stick ring (idle ~0.3 or lower; held ~1).</summary>
        public float TouchStickAlpha => stickVisualAlpha;

        /// <summary>The first-run hints still showing.</summary>
        public bool TouchHintsShown =>
            stickMoveHintRoot != null && stickMoveHintRoot.gameObject.activeSelf
            || stickRewindHintRoot != null && stickRewindHintRoot.gameObject.activeSelf;

        /// <summary>Forgets the first-run hints (a playtest's fresh start).</summary>
        public static void ResetTouchHints()
        {
            PlayerPrefs.DeleteKey(StickMovedKey);
            PlayerPrefs.DeleteKey(StickRewoundKey);
        }

        void BuildTouchStick()
        {
            if (!TouchUiOn)
                return;
            stickBuilt = true;
            stickMovedBefore = PlayerPrefs.GetInt(StickMovedKey, 0) > 0;
            stickRewoundBefore = PlayerPrefs.GetInt(StickRewoundKey, 0) > 0;
            stickVisualAlpha = IdleOutlineAlpha;

            // Landscape: bottom-left float zone over the playfield. Portrait: lower strip below the playfield (the playfield ends where the strip starts: AspectRatioController.PortraitControlStripLayoutPx).
            stickZoneRect = MenuGraphics.NewRect("TouchStickZone", menu.Root);
            stickZoneRect.gameObject.SetActive(false);
            MenuResponsive.Track(stickZoneRect,
                MenuPlacement.At(0f, 0f, 250f, 250f, 520f, 520f),
                MenuPlacement.At(0f, 0f, 270f, 190f, 540f, 400f), MenuElementKind.Container, menu.Responsive);

            stickRingRect = MenuGraphics.NewRect("Ring", stickZoneRect);
            MenuGraphics.Place(stickRingRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
            stickRing = stickRingRect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(stickRing, MenuShape.Circle, 0f);
            stickRing.color = new Color(1f, 0.96f, 0.84f, IdleOutlineAlpha);
            stickRing.raycastTarget = false;
            // Thin fill inset so idle reads as an outline (the fill stays nearly transparent until held).
            var inner = MenuGraphics.NewRect("Fill", stickRingRect);
            MenuGraphics.Fill(inner, 14f);
            stickFill = inner.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(stickFill, MenuShape.Circle, 0f);
            stickFill.color = new Color(0.12f, 0.07f, 0.13f, 0f);
            stickFill.raycastTarget = false;

            stickKnobRect = MenuGraphics.NewRect("Knob", stickRingRect);
            MenuGraphics.Place(stickKnobRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            stickKnob = stickKnobRect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(stickKnob, MenuShape.Circle, 0f);
            stickKnob.color = new Color(1f, 0.96f, 0.84f, 0f);
            stickKnob.raycastTarget = false;

            // Left and right arrowheads on the ring say which way it goes (hidden while idle).
            foreach (var (name, glyph, x) in new[] { ("ArrowL", "◄", -108f), ("ArrowR", "►", 108f) })
            {
                var rect = MenuGraphics.NewRect(name, stickRingRect);
                MenuGraphics.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(60f, 60f));
                var arrow = MenuGraphics.Text(rect, "Text", glyph, theme.chipText, menu.Font, 40f);
                arrow.raycastTarget = false;
                arrow.color = new Color(arrow.color.r, arrow.color.g, arrow.color.b, 0f);
                if (name == "ArrowL") stickArrowL = arrow;
                else stickArrowR = arrow;
            }

            stickMoveHintRoot = MakeStickHint("TouchMoveHint", "DRAG TO MOVE",
                MenuPlacement.At(0f, 0f, 270f, 470f, 620f, 56f), MenuPlacement.At(0f, 0f, 300f, 420f, 600f, 56f));
            stickRewindHintRoot = MakeStickHint("TouchRewindHint", "HOLD TO REWIND",
                MenuPlacement.At(1f, 1f, -400f, -214f, 420f, 56f), MenuPlacement.At(1f, 0f, -330f, 280f, 420f, 56f));

            rewindButton.Pressed += OnStickRewindPressed;
            EnhancedTouchSupport.Enable();
            ETouch.onFingerDown += OnStickFingerDown;
            ETouch.onFingerMove += OnStickFingerMove;
            ETouch.onFingerUp += OnStickFingerUp;
        }

        RectTransform MakeStickHint(string name, string text, MenuPlacement landscape, MenuPlacement portrait)
        {
            var root = MenuGraphics.NewRect(name, menu.Root);
            root.gameObject.SetActive(false);
            var label = MenuGraphics.Text(root, "Text", text, theme.chipText, menu.Font, 32f);
            label.raycastTarget = false;
            MenuResponsive.Track(root, landscape, portrait, MenuElementKind.Text, menu.Responsive);
            return root;
        }

        void TeardownTouchStick()
        {
            if (!stickBuilt)
                return;
            stickBuilt = false;
            ETouch.onFingerDown -= OnStickFingerDown;
            ETouch.onFingerMove -= OnStickFingerMove;
            ETouch.onFingerUp -= OnStickFingerUp;
            EnhancedTouchSupport.Disable();
            TouchStickZone.Zone = null;
            if (rewindButton != null)
                rewindButton.Pressed -= OnStickRewindPressed;
            var controller = world != null ? world.PlayerController : null;
            if (controller != null && stickFinger != null)
                controller.TouchMove = 0f;
        }

        void OnStickRewindPressed()
        {
            if (stickRewoundBefore)
                return;
            stickRewoundBefore = true;
            PlayerPrefs.SetInt(StickRewoundKey, 1);
        }

        bool StickCanSteer()
        {
            var controller = world != null ? world.PlayerController : null;
            return stickShown && controller != null && !controller.Suspended && !controller.InputLocked && !Paused && !Blocked;
        }

        void OnStickFingerDown(Finger finger)
        {
            if (stickFinger != null || !StickCanSteer())
                return;
            var position = finger.currentTouch.screenPosition;
            if (!TouchStickZone.Contains(position) || MenuHit.OnButton(position))
                return;
            stickFinger = finger;
            stickJumped = false;
            // Snap to the held look before measuring radius so drag distance matches the full ring.
            ApplyStickFade(1f);
            stickRadius = Mathf.Max(24f, ScreenRect(stickRingRect).width * 0.5f);
            // The ring floats to where the thumb landed, kept inside the zone (landscape floating stick).
            var zone = TouchStickZone.Zone.Value;
            var rest = new Vector2(Mathf.Clamp(position.x, zone.xMin + stickRadius, zone.xMax - stickRadius),
                Mathf.Clamp(position.y, zone.yMin + stickRadius, zone.yMax - stickRadius));
            stickStart = rest;
            MoveStick(position - rest, rest);
        }

        void OnStickFingerMove(Finger finger)
        {
            if (finger != stickFinger)
                return;
            if (!StickCanSteer())
            {
                ReleaseStick();
                return;
            }
            MoveStick(finger.currentTouch.screenPosition - stickStart, stickStart);
        }

        void OnStickFingerUp(Finger finger)
        {
            if (finger == stickFinger)
                ReleaseStick();
        }

        void MoveStick(Vector2 drag, Vector2 center)
        {
            stickAxis = TouchStickZone.Axis(drag.x, stickRadius);
            if (stickAxis != 0f && !stickMovedBefore)
            {
                stickMovedBefore = true;
                PlayerPrefs.SetInt(StickMovedKey, 1);
            }
            if (TouchStickZone.IsJumpFlick(drag.y, stickRadius))
            {
                if (!stickJumped)
                {
                    stickJumped = true;
                    var controller = world.PlayerController;
                    if (controller != null)
                        controller.RequestJump();
                }
            }
            else if (drag.y < stickRadius * 0.4f)
                stickJumped = false;
            SetStickVisual(drag, center);
        }

        void SetStickVisual(Vector2 drag, Vector2 center)
        {
            // Ring and knob in the zone's own space (screen pixels -> layout units through the ring's scale).
            var scale = stickRingRect.lossyScale.x > 0f ? stickRingRect.lossyScale.x : 1f;
            var zone = ScreenRect(stickZoneRect);
            var local = (center - (Vector2)zone.center) / scale;
            stickRingRect.anchoredPosition = local;
            var knob = Vector2.ClampMagnitude(drag, stickRadius) / scale;
            stickKnobRect.anchoredPosition = knob;
        }

        void ReleaseStick()
        {
            stickFinger = null;
            stickAxis = 0f;
            stickJumped = false;
            if (stickRingRect != null)
            {
                // Portrait: snap the ring back to the resting spot in the control strip. Landscape: leave it where
                // the thumb was and let the fade hide it so the next touch can float in under a new thumb.
                if (MenuScreen.Portrait)
                {
                    stickRingRect.anchoredPosition = Vector2.zero;
                    stickKnobRect.anchoredPosition = Vector2.zero;
                }
                else if (stickKnobRect != null)
                    stickKnobRect.anchoredPosition = Vector2.zero;
            }
        }

        void ApplyStickFade(float alpha)
        {
            // The staged volley is never under the float stick: it hides while the first rewind is taught (portrait's ring is below the playfield).
            if (SickOfIt.Gameplay.Content.FirstRewindStage.Pulsing && !MenuScreen.Portrait)
                alpha = 0f;
            stickVisualAlpha = alpha;
            if (stickRing != null)
                stickRing.color = new Color(1f, 0.96f, 0.84f, alpha);
            // Fill and knob only while held, so idle is a faint outline (see-through disk, no dark pad).
            float held = Mathf.Clamp01((alpha - IdleOutlineAlpha) / Mathf.Max(0.01f, 1f - IdleOutlineAlpha));
            if (stickFill != null)
                stickFill.color = new Color(0.12f, 0.07f, 0.13f, 0.55f * held);
            if (stickKnob != null)
                stickKnob.color = new Color(1f, 0.96f, 0.84f, 0.85f * held);
            // Portrait: the strip always shows its two sides (◄ left, ► right) so the zones read at a glance.
            float arrowAlpha = Mathf.Max(held, MenuScreen.Portrait ? 0.55f : 0f);
            if (stickArrowL != null)
            {
                var c = stickArrowL.color;
                stickArrowL.color = new Color(c.r, c.g, c.b, arrowAlpha);
            }
            if (stickArrowR != null)
            {
                var c = stickArrowR.color;
                stickArrowR.color = new Color(c.r, c.g, c.b, arrowAlpha);
            }
            // Idle ring is smaller; it grows to full size under the thumb.
            if (stickRingRect != null)
            {
                float scale = Mathf.Lerp(0.72f, 1f, held);
                stickRingRect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        void UpdateTouchStick()
        {
            if (!stickBuilt)
                return;
            var controller = world.PlayerController;
            bool playing = controller != null && !controller.Suspended && !controller.InputLocked && !Paused && !Blocked;
            bool show = playing && !GameSettings.TouchButtons;
            if (stickZoneRect.gameObject.activeSelf != show)
                stickZoneRect.gameObject.SetActive(show);
            stickShown = show;
            TouchStickZone.Zone = show ? (Rect?)ScreenRect(stickZoneRect) : null;
            if (!show && stickFinger != null)
                ReleaseStick();
            if (show && controller != null)
                controller.TouchMove = stickAxis;

            if (show)
            {
                bool portrait = MenuScreen.Portrait;
                bool held = stickFinger != null;
                // Portrait: a steady 30% outline in the control strip. Landscape: fade toward invisible while
                // untouched so the bottom-left playfield stays clear of a solid ring over bullets.
                float target = held ? 1f : (portrait ? IdleOutlineAlpha : 0f);
                float next = Mathf.MoveTowards(stickVisualAlpha, target, StickFadeSpeed * Time.unscaledDeltaTime);
                ApplyStickFade(next);
            }

            // A thumb-sized rewind button: at least 48 px on screen however small the phone.
            if (rewindRoot != null && rewindRoot.gameObject.activeInHierarchy)
            {
                float now = ScreenRect(rewindRoot).height;
                float scale = rewindRoot.localScale.y;
                float baseHeight = scale > 0f ? now / scale : now;
                if (baseHeight > 0f)
                {
                    float want = Mathf.Max(1f, MinRewindPixels * 1.1f / baseHeight);
                    if (Mathf.Abs(want - scale) > 0.01f)
                        rewindRoot.localScale = new Vector3(want, want, 1f);
                }
            }

            // First-run hints: each goes the first time that control is used, and stays gone.
            bool moveHint = show && !stickMovedBefore;
            bool rewindHint = show && !stickRewoundBefore;
            if (stickMoveHintRoot.gameObject.activeSelf != moveHint)
                stickMoveHintRoot.gameObject.SetActive(moveHint);
            if (stickRewindHintRoot.gameObject.activeSelf != rewindHint)
                stickRewindHintRoot.gameObject.SetActive(rewindHint);
        }
    }
}
