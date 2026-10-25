using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The dash button: a pill under the weapon chip (a thumb-sized button above the rewind button in portrait), shown once the
    /// dash is his. A tap (mouse or touch) dashes the way he faces or moves; Shift / the gamepad's B do the same. It says
    /// READY, or the seconds until the next dash. Presentation only: it reads the controller and asks it to dash.
    /// </summary>
    public sealed partial class GameplayHud
    {
        MenuButton dashButton;
        RectTransform dashRoot;
        string shownDash = "";

        static string DashKeyText => Application.isMobilePlatform ? "Tap: dash" : "Shift: dash";

        /// <summary>The dash chip's text (the playtest reads what the player sees).</summary>
        public string DashText => dashButton != null ? dashButton.Label.text : "";
        /// <summary>The dash button is on screen.</summary>
        public bool DashButtonShown => dashRoot != null && dashRoot.gameObject.activeInHierarchy;
        /// <summary>The dash button's rectangle in screen pixels (the playtest clicks its middle).</summary>
        public Rect DashButtonScreenRect
        {
            get
            {
                if (dashRoot == null)
                    return Rect.zero;
                var corners = new Vector3[4];
                dashRoot.GetWorldCorners(corners);
                var canvas = menu.GetComponent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
        }

        void BuildDash()
        {
            var place = Landscape.dash;
            var ink = new Color(0.12f, 0.07f, 0.13f, 0.92f);
            var buttonStyle = new MenuButtonStyle
            {
                size = place.size,
                surface = new MenuSurfaceStyle
                {
                    shape = MenuShape.Pill,
                    fill = new Color(0.12f, 0.07f, 0.13f, 0.82f),
                    borderColor = ink,
                    borderWidth = 4f,
                    shadowColor = new Color(0f, 0f, 0f, 0.4f),
                    shadowOffset = new Vector2(0f, -4f),
                    padding = new Vector2(10f, 6f),
                },
                label = new MenuTextStyle
                {
                    fontSize = 34f,
                    color = new Color(0.75f, 1f, 0.85f, 1f),
                    outlineColor = ink,
                    outlineWidth = 0.2f,
                    bold = true,
                },
                hoverFill = new Color(0.25f, 0.14f, 0.24f, 0.92f),
                pressedFill = new Color(0.07f, 0.04f, 0.08f, 0.95f),
                disabledFill = new Color(0.12f, 0.07f, 0.13f, 0.82f),
                hoverScale = 1.06f,
                pressScale = 0.94f,
                hoverRotation = 0f,
            };
            dashButton = menu.AddButtonAt(place.anchor, place.offset, "", buttonStyle, null);
            Lay(dashButton.transform.parent, l => l.dash, MenuElementKind.Button);
            dashButton.Clicked += OnDashButton;
            // Out of keyboard/gamepad navigation, like the rewind and weapon buttons.
            dashButton.navigation = new Navigation { mode = Navigation.Mode.None };
            dashRoot = (RectTransform)dashButton.transform.parent;
            dashRoot.gameObject.SetActive(false);
        }

        void OnDashButton()
        {
            var controller = world.PlayerController;
            if (controller == null || controller.InputLocked || Paused || Blocked)
                return;
            controller.RequestDash();
        }

        void UpdateDash()
        {
            if (dashButton == null)
                return;
            var controller = world.PlayerController;
            bool show = controller != null && controller.DashUnlocked && !controller.Suspended;
            if (dashRoot.gameObject.activeSelf != show)
                dashRoot.gameObject.SetActive(show);
            if (!show)
                return;
            // Rebuilt only when what it shows changes (this runs every frame): tenths of a second while cooling down.
            float left = controller.DashCooldownLeft;
            string text = left > 0f ? $"DASH  {left:0.0}" : $"DASH  READY\n<size=60%>{DashKeyText}</size>";
            if (text == shownDash)
                return;
            shownDash = text;
            dashButton.SetText(text);
            dashButton.Label.enableVertexGradient = false;
            dashButton.Label.color = left > 0f ? new Color(0.6f, 0.6f, 0.65f, 1f) : new Color(0.75f, 1f, 0.85f, 1f);
        }
    }
}
