using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The weapon button: the equipped weapon's name (and its ammo) in a pause-button-sized pill under the rewind button, thumb-sized
    /// in portrait. Pressing it (mouse or touch) equips the next weapon he holds; Q / the mouse wheel / the
    /// gamepad do the same. Presentation only: it reads the weapons' saved state and asks them to switch.
    /// </summary>
    public sealed partial class GameplayHud
    {
        MenuButton weaponButton;
        RectTransform weaponRoot;
        Image weaponIcon;

        /// <summary>The weapon icon the HUD chip shows now (content/weapons "icon"), or null.</summary>
        public Sprite WeaponIcon => weaponIcon != null && weaponIcon.enabled ? weaponIcon.sprite : null;
        string shownWeapon = "", shownWeaponId = "";
        int shownAmmo = -2, shownHeld = -1;
        bool shownWeaponInteractable;
        float weaponSwapAt = -99f;
        Vector2 weaponLabelBase;

        const float WeaponSwapSeconds = 0.4f;
        /// <summary>How many times the chip played its switch animation, and how long ago the last one began (the playtest reads both).</summary>
        public int WeaponSwaps { get; private set; }
        public float WeaponSwapAge => Time.unscaledTime - weaponSwapAt;

        /// <summary>The weapon chip's current text (the playtest reads what the player sees).</summary>
        public string WeaponText => weaponButton != null ? weaponButton.Label.text : "";
        /// <summary>The weapon button is on screen.</summary>
        public bool WeaponButtonShown => weaponRoot != null && weaponRoot.gameObject.activeInHierarchy;
        /// <summary>The weapon button's rectangle in screen pixels (the playtest clicks its middle).</summary>
        public Rect WeaponButtonScreenRect
        {
            get
            {
                if (weaponRoot == null)
                    return Rect.zero;
                var corners = new Vector3[4];
                weaponRoot.GetWorldCorners(corners);
                var canvas = menu.GetComponent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
        }

        void BuildWeapon()
        {
            var place = Landscape.weapon;
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
                    fontSize = 24f,
                    color = new Color(1f, 0.96f, 0.84f, 1f),
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
            weaponButton = menu.AddButtonAt(place.anchor, place.offset, " ", buttonStyle, null); // non-empty: MenuButton makes no Label for ""
            Lay(weaponButton.transform.parent, l => l.weapon, MenuElementKind.Button);
            weaponButton.Clicked += OnWeaponButton;
            // Out of keyboard/gamepad navigation, like the rewind button: arrow keys never select it mid-fight.
            weaponButton.navigation = new Navigation { mode = Navigation.Mode.None };
            weaponRoot = (RectTransform)weaponButton.transform.parent;
            // The weapon's painted icon at the chip's left (its pickup art); the name sits beside it.
            var iconRect = MenuGraphics.NewRect("Icon", weaponButton.transform);
            iconRect.anchorMin = new Vector2(0f, 0.08f);
            iconRect.anchorMax = new Vector2(0f, 0.92f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(place.size.y * 0.84f, 0f);
            weaponIcon = iconRect.gameObject.AddComponent<Image>();
            weaponIcon.preserveAspect = true;
            weaponIcon.raycastTarget = false;
            weaponIcon.enabled = false;
            weaponRoot.gameObject.SetActive(false);
        }

        void OnWeaponButton()
        {
            var weapons = world.Weapons;
            var controller = world.PlayerController;
            if (weapons == null || controller == null || controller.InputLocked || Paused || Blocked)
                return;
            weapons.Cycle(1, world.Now);
        }

        void UpdateWeapon()
        {
            if (weaponButton == null)
                return;
            var weapons = world.Weapons;
            var def = weapons?.CurrentDef;
            bool show = def != null && world.PlayerController != null && !world.PlayerController.Suspended;
            if (weaponRoot.gameObject.activeSelf != show)
                weaponRoot.gameObject.SetActive(show);
            if (!show)
                return;

            bool canSwitch = weapons.HeldCount > 1 && !weapons.Swinging(world.Now);
            if (canSwitch != shownWeaponInteractable)
            {
                weaponButton.interactable = canSwitch;
                shownWeaponInteractable = canSwitch;
            }
            // Rebuilt only when what it shows changes (this runs every frame).
            int ammo = weapons.CurrentUsesAmmo ? weapons.CurrentAmmo : -1;
            if (weapons.CurrentId != shownWeaponId && shownWeaponId != "")
            {
                weaponLabelBase = weaponButton.Label.rectTransform.anchoredPosition;
                weaponSwapAt = Time.unscaledTime;
                WeaponSwaps++;
            }
            AnimateWeaponSwap();
            if (weapons.CurrentId == shownWeaponId && ammo == shownAmmo && weapons.HeldCount == shownHeld)
                return;
            shownWeaponId = weapons.CurrentId;
            shownAmmo = ammo;
            shownHeld = weapons.HeldCount;
            string text = def.displayName.ToUpperInvariant();
            if (ammo >= 0)
                text += $"\n{ammo}/{weapons.CurrentCapacity}";
            shownWeapon = text;
            weaponButton.SetText(text);
            weaponButton.Label.enableVertexGradient = false;
            weaponButton.Label.enableAutoSizing = true;
            weaponButton.Label.fontSizeMin = 10f;
            weaponButton.Label.fontSizeMax = 26f;
            weaponButton.Label.color = Color.Lerp(def.color, Color.white, 0.35f);
            weaponIcon.sprite = def.icon;
            weaponIcon.enabled = def.icon != null;
            var labelRect = weaponButton.Label.rectTransform;
            var chip = ((RectTransform)weaponButton.transform).rect;
            var iconRect = weaponIcon.rectTransform;
            if (def.icon != null && chip.width < chip.height * 1.6f)
            {
                // A square chip (portrait): the icon on top, the name under it in words, never a letter stack beside it.
                iconRect.anchorMin = new Vector2(0.5f, 0.42f);
                iconRect.anchorMax = new Vector2(0.5f, 0.94f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(chip.width * 0.6f, 0f);
                labelRect.offsetMin = new Vector2(4f, 4f);
                labelRect.offsetMax = new Vector2(-4f, -chip.height * 0.58f);
                weaponButton.Label.fontSizeMin = 8f;
                weaponButton.Label.fontSizeMax = 18f;
            }
            else
                labelRect.offsetMin = new Vector2(def.icon != null ? Landscape.weapon.size.y * 0.84f + 8f : 0f, labelRect.offsetMin.y);
        }

        /// <summary>The new weapon's name rises into the chip from below with a punch of size and a flash; presentation only.</summary>
        void AnimateWeaponSwap()
        {
            var label = weaponButton.Label;
            float t = (Time.unscaledTime - weaponSwapAt) / WeaponSwapSeconds;
            if (t >= 1f)
            {
                if (label.rectTransform.localScale.x != 1f)
                {
                    label.rectTransform.localScale = Vector3.one;
                    label.rectTransform.anchoredPosition = weaponLabelBase;
                    label.alpha = 1f;
                }
                return;
            }
            float smooth = 1f - (1f - t) * (1f - t) * (1f - t);
            float punch = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t * 1.4f) * Mathf.PI) ;
            label.rectTransform.localScale = new Vector3(punch, punch, 1f);
            label.rectTransform.anchoredPosition = weaponLabelBase + new Vector2(0f, -26f * (1f - smooth));
            label.alpha = Mathf.Clamp01(t * 4f);
        }
    }
}
