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
    /// The touch move stick: a floating ring in the free space below the playfield on a phone held upright (the lower left
    /// in landscape). A finger that goes down in its zone and drags left or right steers the hero through
    /// <c>PlayerController.TouchMove</c>, the same path the keys and the optional pad take; a flick up jumps. Fingers come
    /// from the Input System's touch events (<c>EnhancedTouch</c> finger down / move / up), not from polling. It shows on
    /// touch devices only (<see cref="TouchUiOn"/>), never on desktop. A first-run hint under it and by the rewind button
    /// ("Drag to move", "Hold to rewind") goes away the first time each is used and is remembered. The rewind button is
    /// grown to at least 48 px here. Presentation only.
    /// </summary>
    public sealed partial class GameplayHud
    {
        const string StickMovedKey = "TouchStickMoved";
        const string StickRewoundKey = "TouchStickRewound";
        const float MinRewindPixels = 48f;

        /// <summary>The playtest's way of saying "pretend this is a phone" (a desktop has none of the touch controls).</summary>
        public static bool TouchUiForced;

        /// <summary>The touch controls belong here: a phone or tablet, or the playtest's touch scenarios.</summary>
        public static bool TouchUiOn => Application.isMobilePlatform || TouchUiForced;

        RectTransform stickZoneRect, stickRingRect, stickKnobRect;
        Image stickRing, stickKnob;
        RectTransform stickMoveHintRoot, stickRewindHintRoot;
        Finger stickFinger;
        Vector2 stickStart;
        float stickAxis, stickRadius;
        bool stickJumped, stickBuilt, stickShown;
        bool stickMovedBefore, stickRewoundBefore;

        /// <summary>The stick is on screen (the playtest checks it on a phone, and that it is not on desktop).</summary>
        public bool TouchStickShown => stickShown && stickZoneRect != null && stickZoneRect.gameObject.activeInHierarchy;

        /// <summary>The stick's ring, in screen pixels (the playtest checks it sits outside the playfield and drags inside it).</summary>
        public Rect TouchStickScreenRect => stickRingRect != null ? ScreenRect(stickRingRect) : default;

        /// <summary>The rewind button on screen, in pixels.</summary>
        public Rect RewindButtonScreenRect => rewindRoot != null ? ScreenRect(rewindRoot) : default;

        /// <summary>The stick's current axis, -1..1.</summary>
        public float TouchStickAxis => stickAxis;

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

            // The zone takes the finger; the ring is only where it rests. The zone is bigger than the ring.
            stickZoneRect = MenuGraphics.NewRect("TouchStickZone", menu.Root);
            stickZoneRect.gameObject.SetActive(false);
            MenuResponsive.Track(stickZoneRect,
                MenuPlacement.At(0f, 0f, 250f, 250f, 520f, 520f),
                MenuPlacement.At(0f, 0f, 270f, 190f, 540f, 400f), MenuElementKind.Container, menu.Responsive);

            stickRingRect = MenuGraphics.NewRect("Ring", stickZoneRect);
            MenuGraphics.Place(stickRingRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
            stickRing = stickRingRect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(stickRing, MenuShape.Circle, 0f);
            stickRing.color = new Color(1f, 0.96f, 0.84f, 0.34f);
            stickRing.raycastTarget = false;
            var inner = MenuGraphics.NewRect("Fill", stickRingRect);
            MenuGraphics.Fill(inner, 8f);
            var innerImage = inner.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(innerImage, MenuShape.Circle, 0f);
            innerImage.color = new Color(0.12f, 0.07f, 0.13f, 0.5f);
            innerImage.raycastTarget = false;

            stickKnobRect = MenuGraphics.NewRect("Knob", stickRingRect);
            MenuGraphics.Place(stickKnobRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            stickKnob = stickKnobRect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(stickKnob, MenuShape.Circle, 0f);
            stickKnob.color = new Color(1f, 0.96f, 0.84f, 0.8f);
            stickKnob.raycastTarget = false;

            // Left and right arrowheads on the ring say which way it goes.
            foreach (var (name, glyph, x) in new[] { ("ArrowL", "◄", -108f), ("ArrowR", "►", 108f) })
            {
                var rect = MenuGraphics.NewRect(name, stickRingRect);
                MenuGraphics.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(60f, 60f));
                var arrow = MenuGraphics.Text(rect, "Text", glyph, theme.chipText, menu.Font, 40f);
                arrow.raycastTarget = false;
            }

            stickMoveHintRoot = MakeStickHint("TouchMoveHint", "DRAG TO MOVE · FLICK UP TO JUMP",
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
            stickStart = position;
            stickJumped = false;
            stickRadius = Mathf.Max(24f, ScreenRect(stickRingRect).width * 0.5f);
            // The ring floats to where the thumb landed, kept inside the zone.
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
                stickRingRect.anchoredPosition = Vector2.zero;
                stickKnobRect.anchoredPosition = Vector2.zero;
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
