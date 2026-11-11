using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// What a beat (Docs/beats.md) shows on the HUD: the letterbox bars (slide in over 0.3 s, drawn inside the safe area)
    /// and one speech bubble with a short caption above whoever is speaking. Presentation only: unscaled time, nothing
    /// here is saved (a beat locks rewind, so nothing ever rewinds into one).
    /// </summary>
    public sealed partial class GameplayHud
    {
        /// <summary>Seconds the bars take to slide in or out.</summary>
        public const float LetterboxSeconds = 0.3f;
        /// <summary>The smallest a caption is drawn, in screen pixels.</summary>
        public const float BubbleMinPixels = 20f;

        RectTransform barTop, barBottom, bubbleRect;
        TextMeshProUGUI bubbleText;
        Image bubbleBack;
        float letterK, letterTarget;
        float letterSeconds = LetterboxSeconds;
        int bubbleSerial = -1;
        Vector2 bubblePoint;
        float bubbleUntil = -1f;
        string bubbleShown = "";

        /// <summary>How far the bars are in, 0 (out) to 1 (in).</summary>
        public float LetterboxAmount => letterK;
        /// <summary>The bars are wanted in (a beat is running).</summary>
        public bool LetterboxOn => letterTarget > 0f;
        /// <summary>The caption in the speech bubble now ("" when none).</summary>
        public string BubbleShown => bubbleRect != null && bubbleRect.gameObject.activeSelf ? bubbleShown : "";

        /// <summary>Slides the letterbox bars in or out. While they are in, the rewind scanlines and colour split stay off the play area.</summary>
        public void SetLetterbox(bool on, float seconds)
        {
            letterTarget = on ? 1f : 0f;
            letterSeconds = Mathf.Max(0.01f, seconds);
            RewindFx.BeatHidden = on;
        }

        /// <summary>Puts a one-line caption in a bubble above an entity (by serial) or a world point.</summary>
        public void ShowBubble(string text, Color color, int entitySerial, Vector2 point, float until)
        {
            if (bubbleRect == null)
                return;
            bubbleShown = text ?? "";
            bubbleSerial = entitySerial;
            bubblePoint = point;
            bubbleUntil = until;
            bubbleText.text = bubbleShown;
            bubbleText.color = color;
            bubbleRect.gameObject.SetActive(!string.IsNullOrEmpty(bubbleShown));
            PlaceBubble();
        }

        public void HideBubble()
        {
            bubbleUntil = -1f;
            if (bubbleRect != null)
                bubbleRect.gameObject.SetActive(false);
        }

        /// <summary>The bars' rectangles in screen pixels (top, bottom), for the playtest's safe-area check.</summary>
        public bool TryGetLetterboxScreenRects(out Rect top, out Rect bottom)
        {
            top = bottom = Rect.zero;
            if (barTop == null || barBottom == null || !barTop.gameObject.activeSelf)
                return false;
            top = ScreenRectOf(barTop);
            bottom = ScreenRectOf(barBottom);
            return true;
        }

        /// <summary>The bubble's rectangle in screen pixels and its caption's size in screen pixels.</summary>
        public bool TryGetBubbleScreenRect(out Rect rect, out float fontPixels)
        {
            rect = Rect.zero;
            fontPixels = 0f;
            if (bubbleRect == null || !bubbleRect.gameObject.activeSelf)
                return false;
            rect = ScreenRectOf(bubbleRect);
            var canvas = menu.GetComponent<Canvas>();
            fontPixels = bubbleText.fontSize * (canvas != null ? canvas.scaleFactor : 1f);
            return true;
        }

        Rect ScreenRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var canvas = menu.GetComponent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        void BuildBeat()
        {
            RewindFx.BeatHidden = false;
            var root = menu.Root;
            barTop = NewBar(root, "LetterboxTop", true);
            barBottom = NewBar(root, "LetterboxBottom", false);

            bubbleRect = MenuGraphics.NewRect("Bubble", root);
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0f);
            bubbleBack = bubbleRect.gameObject.AddComponent<Image>();
            bubbleBack.color = new Color(0.1f, 0.06f, 0.12f, 0.9f);
            bubbleBack.raycastTarget = false;
            var textSlot = MenuGraphics.NewRect("Text", bubbleRect);
            textSlot.anchorMin = Vector2.zero;
            textSlot.anchorMax = Vector2.one;
            textSlot.offsetMin = new Vector2(14f, 8f);
            textSlot.offsetMax = new Vector2(-14f, -8f);
            bubbleText = MenuGraphics.Text(textSlot, "Label", "", theme.subtitle, menu.Font, 0f, false);
            bubbleText.alignment = TextAlignmentOptions.Center;
            bubbleText.raycastTarget = false;
            bubbleRect.gameObject.SetActive(false);
        }

        static RectTransform NewBar(RectTransform parent, string name, bool top)
        {
            var bar = MenuGraphics.NewRect(name, parent);
            bar.anchorMin = new Vector2(0f, top ? 1f : 0f);
            bar.anchorMax = new Vector2(1f, top ? 1f : 0f);
            bar.pivot = new Vector2(0.5f, top ? 1f : 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = Vector2.zero;
            var image = bar.gameObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            bar.gameObject.SetActive(false);
            return bar;
        }

        bool beatPress;

        /// <summary>True once per press: a tap, click, key or pad button that went down since the last call (latched every
        /// rendered frame here, because the director ticks in FixedUpdate and would miss a press on a frame with no step).</summary>
        public bool TakeBeatPress()
        {
            bool pressed = beatPress;
            beatPress = false;
            return pressed;
        }

        static bool AnyPressThisFrame()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
                return true;
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                                || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                                || pad.startButton.wasPressedThisFrame))
                return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            var mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        }

        void UpdateBeat()
        {
            if (barTop == null)
                return;
            if (letterTarget > 0f && AnyPressThisFrame())
                beatPress = true;
            else if (letterTarget <= 0f)
                beatPress = false;
            letterK = Mathf.MoveTowards(letterK, letterTarget, Time.unscaledDeltaTime / letterSeconds);
            bool shown = letterK > 0.001f;
            if (barTop.gameObject.activeSelf != shown)
            {
                barTop.gameObject.SetActive(shown);
                barBottom.gameObject.SetActive(shown);
            }
            if (shown)
            {
                // A tenth of the safe area's height each (a little less on a tall portrait screen), sliding in by growing.
                float full = menu.Root.rect.height * (menu.Root.rect.height > menu.Root.rect.width ? 0.07f : 0.11f);
                float h = full * Mathf.SmoothStep(0f, 1f, letterK);
                barTop.sizeDelta = new Vector2(0f, h);
                barBottom.sizeDelta = new Vector2(0f, h);
            }
            if (bubbleRect.gameObject.activeSelf)
            {
                if (world == null || world.Now >= bubbleUntil)
                    HideBubble();
                else
                    PlaceBubble();
            }
        }

        void PlaceBubble()
        {
            var root = menu.Root;
            var canvas = menu.GetComponent<Canvas>();
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            bubbleText.fontSize = Mathf.Max(40f, BubbleMinPixels / scale);
            float maxWidth = root.rect.width * 0.9f;
            Vector2 size = bubbleText.GetPreferredValues(bubbleShown, maxWidth - 28f, 0f);
            bubbleRect.sizeDelta = new Vector2(Mathf.Min(maxWidth, size.x + 28f), size.y + 16f);

            Vector2 at = bubblePoint;
            if (bubbleSerial >= 0 && world != null)
            {
                var e = world.FindBySerial(bubbleSerial);
                if (e != null && e.IsAlive)
                    at = (Vector2)e.Position + new Vector2(0f, 1.1f);
            }
            var cam = world != null ? world.Camera : null;
            if (cam == null)
                return;
            Vector3 sp = cam.WorldToScreenPoint(at);
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, uiCam, out Vector2 local))
                return;
            Vector2 p = local - root.rect.center;
            float halfW = root.rect.width * 0.5f - bubbleRect.sizeDelta.x * 0.5f - 6f;
            float bar = root.rect.height * (root.rect.height > root.rect.width ? 0.07f : 0.11f) * letterTarget;
            float minY = -root.rect.height * 0.5f + bar + 8f;
            float maxY = root.rect.height * 0.5f - bar - bubbleRect.sizeDelta.y - 8f;
            p.x = Mathf.Clamp(p.x, -Mathf.Max(0f, halfW), Mathf.Max(0f, halfW));
            p.y = Mathf.Clamp(p.y, minY, Mathf.Max(minY, maxY));
            bubbleRect.anchoredPosition = p;
        }
    }
}
