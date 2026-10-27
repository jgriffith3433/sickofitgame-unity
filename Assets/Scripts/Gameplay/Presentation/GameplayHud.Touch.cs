using UnityEngine;
using TMPro;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The touch move pad: left and right buttons held by the left thumb and a jump button above them, bottom-left, clear
    /// of the rewind and dash buttons on the right. Off unless Settings > Accessibility > Touch Buttons is on
    /// (<see cref="GameSettings.TouchButtons"/>), on phones too: the game is played by tapping and dragging. They steer the
    /// hero through <c>PlayerController.TouchMove</c>/<c>RequestJump</c>, the same path the keys take. A short hint names
    /// them on the first few levels they show. Presentation only.
    /// </summary>
    public sealed partial class GameplayHud
    {
        const string TouchHintKey = "TouchPadHints";
        const int TouchHintLevels = 3;
        const float TouchHintSeconds = 9f;

        MenuButton touchLeft, touchRight, touchJump;
        RectTransform touchLeftRoot, touchRightRoot, touchJumpRoot;
        TextMeshProUGUI touchHint;
        float touchBuiltAt;
        bool touchHintOn;
        int touchLeftHeld, touchRightHeld;


        /// <summary>The move pad is on screen (the playtest checks it on a phone).</summary>
        public bool TouchPadShown => touchLeftRoot != null && touchLeftRoot.gameObject.activeInHierarchy;

        MenuButton MakeTouchButton(MenuPlacement place, System.Func<MenuHudLayout, MenuPlacement> pick, string text, out RectTransform root)
        {
            var ink = new Color(0.12f, 0.07f, 0.13f, 0.92f);
            var style = new MenuButtonStyle
            {
                size = place.size,
                surface = new MenuSurfaceStyle
                {
                    shape = MenuShape.Pill,
                    fill = new Color(0.12f, 0.07f, 0.13f, 0.6f),
                    borderColor = new Color(1f, 0.96f, 0.84f, 0.7f),
                    borderWidth = 4f,
                    shadowColor = new Color(0f, 0f, 0f, 0.4f),
                    shadowOffset = new Vector2(0f, -4f),
                    padding = new Vector2(10f, 6f),
                },
                label = new MenuTextStyle
                {
                    fontSize = 56f,
                    color = new Color(1f, 0.96f, 0.84f, 1f),
                    outlineColor = ink,
                    outlineWidth = 0.2f,
                    bold = true,
                },
                hoverFill = new Color(0.12f, 0.07f, 0.13f, 0.6f),
                pressedFill = new Color(0.4f, 0.25f, 0.3f, 0.9f),
                disabledFill = new Color(0.12f, 0.07f, 0.13f, 0.6f),
                hoverScale = 1f,
                pressScale = 0.94f,
                hoverRotation = 0f,
            };
            var button = menu.AddButtonAt(place.anchor, place.offset, text, style, null);
            Lay(button.transform.parent, pick, MenuElementKind.Button);
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            root = (RectTransform)button.transform.parent;
            root.gameObject.SetActive(false);
            return button;
        }

        void BuildTouchPad()
        {
            int seen = PlayerPrefs.GetInt(TouchHintKey, 0);
            touchLeft = MakeTouchButton(Landscape.moveLeft, l => l.moveLeft, "◄", out touchLeftRoot);
            touchRight = MakeTouchButton(Landscape.moveRight, l => l.moveRight, "►", out touchRightRoot);
            touchJump = MakeTouchButton(Landscape.jump, l => l.jump, "JUMP", out touchJumpRoot);
            touchLeft.Pressed += () => touchLeftHeld = 1;
            touchLeft.Released += () => touchLeftHeld = 0;
            touchRight.Pressed += () => touchRightHeld = 1;
            touchRight.Released += () => touchRightHeld = 0;
            touchJump.Label.fontSize = 34f;
            touchJump.Pressed += OnTouchJump;

            // The hint sits just above the jump button until a few seconds pass; it shows on the first levels only.
            var hintRect = MenuGraphics.NewRect("TouchHint", menu.Root);
            hintRect.gameObject.SetActive(false);
            touchHint = MenuGraphics.Text(hintRect, "Text", "Hold ◄ ► to move · JUMP to jump · DASH to dash", theme.chipText, menu.Font, 34f);
            touchHint.raycastTarget = false;
            MenuResponsive.Track(hintRect, MenuPlacement.At(0f, 0f, 460f, 520f, 900f, 60f),
                MenuPlacement.At(0f, 0f, 540f, 560f, 1000f, 60f), MenuElementKind.Text, menu.Responsive);
            touchBuiltAt = Time.unscaledTime;
            touchHintOn = seen < TouchHintLevels;
            if (touchHintOn && GameSettings.TouchButtons)
                PlayerPrefs.SetInt(TouchHintKey, seen + 1);
        }

        void OnTouchJump()
        {
            var controller = world.PlayerController;
            if (controller == null || controller.InputLocked || Paused || Blocked)
                return;
            controller.RequestJump();
        }

        void UpdateTouchPad()
        {
            if (touchLeft == null)
                return;
            var controller = world.PlayerController;
            bool show = GameSettings.TouchButtons && controller != null && !controller.Suspended && !controller.InputLocked && !Paused && !Blocked;
            if (touchLeftRoot.gameObject.activeSelf != show)
            {
                touchLeftRoot.gameObject.SetActive(show);
                touchRightRoot.gameObject.SetActive(show);
                touchJumpRoot.gameObject.SetActive(show);
            }
            if (controller != null)
                controller.TouchMove = show ? touchRightHeld - touchLeftHeld : 0f;
            bool hint = show && touchHintOn && Time.unscaledTime - touchBuiltAt < TouchHintSeconds;
            if (touchHint.transform.parent.gameObject.activeSelf != hint)
                touchHint.transform.parent.gameObject.SetActive(hint);
        }
    }
}
