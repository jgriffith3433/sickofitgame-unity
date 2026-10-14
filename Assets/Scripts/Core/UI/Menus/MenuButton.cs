using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// A styled menu button (or clickable card): springs bigger and tilts on hover, squashes when pressed,
    /// recolors its fill, and works with mouse, touch, keyboard and gamepad (it's a Selectable).
    /// </summary>
    public sealed class MenuButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public event Action Clicked;
        /// <summary>Pointer/touch went down on it (for hold-to-use buttons like rewind).</summary>
        public event Action Pressed;
        /// <summary>The press ended (released anywhere, or the button got disabled mid-press).</summary>
        public event Action Released;
        bool held;

        /// <summary>Where custom content (card text, pictures) goes, inside the padding.</summary>
        public RectTransform Content { get; private set; }
        public TextMeshProUGUI Label { get; private set; }
        public MenuMotionDriver Motion { get; private set; }

        MenuButtonStyle style;
        Image fill;
        Color normalFill, hoverFill, pressedFill;
        Color labelColor;
        bool labelGradient;
        AudioSource audioSource;
        AudioClip hoverSound, clickSound;
        float volume;

        float scale = 1f, rotation, targetScale = 1f, targetRotation;
        Color currentFill, targetFill;
        Color currentLabel, targetLabel;

        /// <summary>Builds the button's visuals under 'slot' (which a layout group may position).</summary>
        public static MenuButton Create(RectTransform slot, MenuButtonStyle style, string text, TMP_FontAsset font,
            MenuEntrance entrance, float entranceDelay, int index, MenuSounds sounds)
        {
            var visual = MenuGraphics.NewRect("Visual", slot);
            MenuGraphics.Fill(visual);
            var button = visual.gameObject.AddComponent<MenuButton>();
            button.Init(style, text, font, entrance, entranceDelay, index, sounds);
            return button;
        }

        void Init(MenuButtonStyle buttonStyle, string text, TMP_FontAsset font, MenuEntrance entrance, float entranceDelay, int index, MenuSounds sounds)
        {
            style = buttonStyle;
            transition = Transition.None;
            fill = MenuGraphics.Surface(transform, style.surface);
            if (fill == null)
            {
                // Text-only buttons still need something to catch the pointer.
                fill = MenuGraphics.NewImage(transform, "HitArea", Color.clear);
            }
            fill.raycastTarget = true;
            targetGraphic = fill;

            normalFill = fill.color;
            hoverFill = style.hoverFill.a > 0f ? style.hoverFill : Color.Lerp(normalFill, Color.white, 0.25f);
            pressedFill = style.pressedFill.a > 0f ? style.pressedFill : Color.Lerp(normalFill, Color.black, 0.25f);
            currentFill = targetFill = normalFill;

            Content = MenuGraphics.NewRect("Content", transform);
            MenuGraphics.Fill(Content);
            Content.offsetMin = new Vector2(style.surface.padding.x, style.surface.padding.y);
            Content.offsetMax = new Vector2(-style.surface.padding.x, -style.surface.padding.y);

            if (!string.IsNullOrEmpty(text))
            {
                Label = MenuGraphics.Text(Content, "Label", text, style.label, font);
                labelColor = style.label.color;
                labelGradient = Label.enableVertexGradient;
                currentLabel = targetLabel = labelColor;
            }

            Motion = gameObject.AddComponent<MenuMotionDriver>();
            Motion.Idle = style.idle;
            Motion.Entrance = entrance;
            Motion.EntranceDelay = entranceDelay;
            float tilt = style.alternateTilt && index % 2 == 1 ? -style.tilt : style.tilt;
            Motion.BaseRotation = tilt;

            audioSource = sounds?.Source;
            hoverSound = sounds?.Hover;
            clickSound = sounds?.Click;
            volume = sounds?.Volume ?? 1f;
            DoStateTransition(currentSelectionState, true);
        }

        /// <summary>Replaces the label text.</summary>
        public void SetText(string text) => MenuGraphics.SetText(Label, text);

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (style == null)
                return;
            switch (state)
            {
                case SelectionState.Highlighted:
                case SelectionState.Selected:
                    if (state == SelectionState.Highlighted && targetScale <= 1f && hoverSound != null && audioSource != null)
                        audioSource.PlayOneShot(hoverSound, volume * 0.6f);
                    targetScale = style.hoverScale;
                    targetRotation = style.hoverRotation;
                    targetFill = hoverFill;
                    targetLabel = style.hoverLabelColor.a > 0f ? style.hoverLabelColor : labelColor;
                    break;
                case SelectionState.Pressed:
                    targetScale = style.pressScale;
                    targetRotation = style.hoverRotation * 0.5f;
                    targetFill = pressedFill;
                    targetLabel = labelColor;
                    break;
                case SelectionState.Disabled:
                    targetScale = 1f;
                    targetRotation = 0f;
                    targetFill = style.disabledFill;
                    targetLabel = new Color(labelColor.r, labelColor.g, labelColor.b, labelColor.a * 0.5f);
                    break;
                default:
                    targetScale = 1f;
                    targetRotation = 0f;
                    targetFill = normalFill;
                    targetLabel = labelColor;
                    break;
            }
            if (instant)
            {
                scale = targetScale;
                rotation = targetRotation;
                currentFill = targetFill;
                currentLabel = targetLabel;
                ApplyVisuals();
            }
        }

        void Update()
        {
            if (style == null)
                return;
            float k = 1f - Mathf.Exp(-style.responsiveness * Time.unscaledDeltaTime);
            // A little overshoot on the way up makes the hover feel springy.
            scale = Mathf.Lerp(scale, targetScale + (targetScale > scale + 0.01f ? 0.02f : 0f), k);
            rotation = Mathf.Lerp(rotation, targetRotation, k);
            currentFill = Color.Lerp(currentFill, targetFill, k);
            currentLabel = Color.Lerp(currentLabel, targetLabel, k);
            ApplyVisuals();
        }

        void ApplyVisuals()
        {
            if (Motion != null)
            {
                Motion.ExtraScale = scale;
                Motion.ExtraRotation = rotation;
            }
            if (fill != null && fill.name != "HitArea")
                fill.color = currentFill;
            if (Label != null && !labelGradient)
                Label.color = currentLabel;
            else if (Label != null)
                Label.alpha = currentLabel.a;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Press();
        }

        public void OnSubmit(BaseEventData eventData) => Press();

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable())
                return;
            held = true;
            Pressed?.Invoke();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            EndHold();
        }

        protected override void OnDisable()
        {
            EndHold();
            base.OnDisable();
        }

        void EndHold()
        {
            if (!held)
                return;
            held = false;
            Released?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            // A mouse click selects the button; let go of that when the pointer leaves so it doesn't stay big.
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == gameObject)
                events.SetSelectedGameObject(null);
        }

        void Press()
        {
            if (!IsActive() || !IsInteractable())
                return;
            if (clickSound != null && audioSource != null)
                audioSource.PlayOneShot(clickSound, volume);
            scale = style.pressScale;
            Clicked?.Invoke();
        }
    }

    /// <summary>Where menu buttons play their hover/click sounds.</summary>
    public sealed class MenuSounds
    {
        public AudioSource Source;
        public AudioClip Hover;
        public AudioClip Click;
        public float Volume = 1f;
    }
}
