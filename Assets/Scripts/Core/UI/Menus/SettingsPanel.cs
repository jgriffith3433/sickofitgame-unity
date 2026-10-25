using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Audio;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Menus
{
    /// <summary>
    /// The settings overlay (sound on/off, volumes, screen shake, hints), shared by the main menu and the pause menu.
    /// Changes save and apply immediately (<see cref="GameSettings"/>). Escape or Done closes it.
    /// </summary>
    public static class SettingsPanel
    {
        const float Step = 0.1f;

        public static GameObject Show(MenuCanvas menu, Action onClosed)
        {
            var theme = menu.Theme;
            var texts = theme.texts ?? new MenuTexts();
            var previousBack = menu.Back;

            var overlay = Overlay.Create(menu, "Settings", 0.75f);
            var inner = overlay.Inner;

            var panel = MenuGraphics.NewRect("Panel", inner);
            MenuGraphics.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(980f, 880f));
            MenuGraphics.Surface(panel, theme.card.surface);
            menu.AddMotion(panel.gameObject, null, theme.entrance, 1.5f);
            // Portrait: a taller panel, so the rows can grow to the touch size without squeezing.
            MenuResponsive.Track(panel, MenuPlacement.At(0.5f, 0.5f, 0f, -10f, 980f, 880f), menu.Responsive.screens?.settingsPanel,
                MenuElementKind.Container, menu.Responsive);

            menu.AddText(panel, "Title", texts.settingsTitle, theme.title, new Vector2(0.5f, 1f), new Vector2(0f, -78f),
                new Vector2(900f, 130f), out _, Mathf.Min(theme.title.fontSize, 96f), menu.TitleFont, null, null, -2f);

            var rows = MenuGraphics.NewRect("Rows", panel);
            rows.anchorMin = new Vector2(0f, 0f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.offsetMin = new Vector2(60f, 150f);
            rows.offsetMax = new Vector2(-60f, -160f);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var step = theme.smallButton.Clone();
            step.size = new Vector2(78f, 70f);
            step.surface = CloneSurface(step.surface, MenuShape.Circle);
            var toggle = theme.smallButton.Clone();
            toggle.size = new Vector2(220f, 70f);

            ToggleRow(menu, rows, texts.sound, texts, toggle, () => GameSettings.SoundOn, v => GameSettings.SoundOn = v);
            VolumeRow(menu, rows, texts.masterVolume, step, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            VolumeRow(menu, rows, texts.musicVolume, step, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
            VolumeRow(menu, rows, texts.effectsVolume, step, () => GameSettings.SfxVolume, v => { GameSettings.SfxVolume = v; Sfx.Play(Sfx.Events.uiClick); });
            VolumeRow(menu, rows, texts.voiceVolume, step, () => GameSettings.VoiceVolume, v => GameSettings.VoiceVolume = v);
            ToggleRow(menu, rows, texts.screenShake, texts, toggle, () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v);
            ToggleRow(menu, rows, texts.hints, texts, toggle, () => GameSettings.Hints, v => GameSettings.Hints = v);

            void Close()
            {
                if (overlay.Root == null)
                    return;
                Sfx.Play(Sfx.Events.uiBack);
                menu.Back = previousBack;
                Overlay.Close(overlay.Root);
                onClosed?.Invoke();
            }

            var done = theme.button.Clone();
            done.size = new Vector2(340f, 92f);
            var access = theme.button.Clone();
            access.size = new Vector2(440f, 92f);
            menu.AddButtonAt(new Vector2(0.5f, 0f), new Vector2(-215f, 84f), texts.accessibility, access, () => ShowAccessibility(menu), panel);
            menu.AddButtonAt(new Vector2(0.5f, 0f), new Vector2(260f, 84f), texts.done, done, Close, panel);
            menu.Back = Close;
            return overlay.Root;
        }

        /// <summary>Settings > Accessibility: the toggles that make Hell easier to read and gentler on the eyes. Saved at once (<see cref="GameSettings"/>).</summary>
        public static GameObject ShowAccessibility(MenuCanvas menu)
        {
            var theme = menu.Theme;
            var texts = theme.texts ?? new MenuTexts();
            var previousBack = menu.Back;

            var overlay = Overlay.Create(menu, "Accessibility", 0.8f);
            var panel = MenuGraphics.NewRect("Panel", overlay.Inner);
            MenuGraphics.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(980f, 880f));
            MenuGraphics.Surface(panel, theme.card.surface);
            menu.AddMotion(panel.gameObject, null, theme.entrance, 1.5f);
            MenuResponsive.Track(panel, MenuPlacement.At(0.5f, 0.5f, 0f, -10f, 980f, 880f), menu.Responsive.screens?.settingsPanel,
                MenuElementKind.Container, menu.Responsive);
            menu.AddText(panel, "Title", texts.accessibilityTitle, theme.title, new Vector2(0.5f, 1f), new Vector2(0f, -78f),
                new Vector2(900f, 130f), out _, Mathf.Min(theme.title.fontSize, 96f), menu.TitleFont, null, null, -2f);

            var rows = MenuGraphics.NewRect("Rows", panel);
            rows.anchorMin = new Vector2(0f, 0f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.offsetMin = new Vector2(60f, 150f);
            rows.offsetMax = new Vector2(-60f, -160f);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var toggle = theme.smallButton.Clone();
            toggle.size = new Vector2(220f, 70f);
            ToggleRow(menu, rows, texts.bulletOutlines, texts, toggle, () => GameSettings.BulletOutlines, v => GameSettings.BulletOutlines = v);
            ToggleRow(menu, rows, texts.highContrastBullets, texts, toggle, () => GameSettings.HighContrastBullets, v => GameSettings.HighContrastBullets = v);
            ToggleRow(menu, rows, texts.reducedFlash, texts, toggle, () => GameSettings.ReducedFlash, v => GameSettings.ReducedFlash = v);
            ToggleRow(menu, rows, texts.reducedShake, texts, toggle, () => GameSettings.ReducedShake, v => GameSettings.ReducedShake = v);
            ToggleRow(menu, rows, texts.hitboxDot, texts, toggle, () => GameSettings.HitboxDot, v => GameSettings.HitboxDot = v);
            ToggleRow(menu, rows, texts.aimAssist, texts, toggle, () => GameSettings.AimAssist, v => GameSettings.AimAssist = v);

            void Close()
            {
                if (overlay.Root == null)
                    return;
                Sfx.Play(Sfx.Events.uiBack);
                menu.Back = previousBack;
                Overlay.Close(overlay.Root);
            }

            var done = theme.button.Clone();
            done.size = new Vector2(340f, 92f);
            menu.AddButtonAt(new Vector2(0.5f, 0f), new Vector2(0f, 84f), texts.done, done, Close, panel);
            menu.Back = Close;
            return overlay.Root;
        }

        /// <summary>The New Game card: four difficulties, each with what it changes. Picking one saves it (<see cref="Difficulty"/>) and calls onPicked.</summary>
        public static GameObject ShowDifficulty(MenuCanvas menu, Action<DifficultyLevel> onPicked, Action onBack)
        {
            var theme = menu.Theme;
            var texts = theme.texts ?? new MenuTexts();
            var previousBack = menu.Back;

            var overlay = Overlay.Create(menu, "Difficulty", 0.8f);
            var panel = MenuGraphics.NewRect("Panel", overlay.Inner);
            MenuGraphics.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(980f, 880f));
            MenuGraphics.Surface(panel, theme.card.surface);
            menu.AddMotion(panel.gameObject, null, theme.entrance, 1.5f);
            MenuResponsive.Track(panel, MenuPlacement.At(0.5f, 0.5f, 0f, -10f, 980f, 880f), menu.Responsive.screens?.settingsPanel,
                MenuElementKind.Container, menu.Responsive);
            menu.AddText(panel, "Title", texts.difficultyTitle, theme.title, new Vector2(0.5f, 1f), new Vector2(0f, -78f),
                new Vector2(900f, 130f), out _, Mathf.Min(theme.title.fontSize, 96f), menu.TitleFont, null, null, -2f);

            var rows = MenuGraphics.NewRect("Rows", panel);
            rows.anchorMin = new Vector2(0f, 0f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.offsetMin = new Vector2(60f, 150f);
            rows.offsetMax = new Vector2(-60f, -160f);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var pick = theme.button.Clone();
            pick.size = new Vector2(330f, 84f);
            bool chosen = false;
            foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
            {
                var mods = Difficulty.Of(level);
                var row = Row(menu, rows, mods.name);
                // The row's own label cell is the button's neighbour: a name button, then what it changes.
                Destroy(row.Find("Label").gameObject);
                var captured = level;
                menu.AddButton(row, mods.name, pick, () =>
                {
                    if (chosen)
                        return;
                    chosen = true;
                    Difficulty.Current = captured;
                    if (captured == DifficultyLevel.Assist)
                        GameSettings.AimAssist = true; // the Assist card says so
                    menu.Back = previousBack;
                    Overlay.Close(overlay.Root);
                    onPicked?.Invoke(captured);
                });
                var blurb = Cell(menu, row, "Blurb", mods.blurb, theme.body, 520f, TextAlignmentOptions.MidlineLeft);
                blurb.textWrappingMode = TextWrappingModes.Normal;
                blurb.fontSize = 26f;
            }

            void Close()
            {
                if (overlay.Root == null || chosen)
                    return;
                Sfx.Play(Sfx.Events.uiBack);
                menu.Back = previousBack;
                Overlay.Close(overlay.Root);
                onBack?.Invoke();
            }

            var back = theme.button.Clone();
            back.size = new Vector2(340f, 92f);
            menu.AddButtonAt(new Vector2(0.5f, 0f), new Vector2(0f, 84f), texts.difficultyBack, back, Close, panel);
            menu.Back = Close;
            return overlay.Root;
        }

        static void Destroy(GameObject go) => UnityEngine.Object.Destroy(go);

        static MenuSurfaceStyle CloneSurface(MenuSurfaceStyle source, MenuShape shape)
        {
            return new MenuSurfaceStyle
            {
                shape = shape,
                sprite = source.sprite,
                cornerRadius = source.cornerRadius,
                fill = source.fill,
                borderColor = source.borderColor,
                borderWidth = source.borderWidth,
                shadowColor = source.shadowColor,
                shadowOffset = source.shadowOffset,
                padding = new Vector2(4f, 4f),
            };
        }

        static RectTransform Row(MenuCanvas menu, RectTransform rows, string label)
        {
            var row = MenuGraphics.NewRect(label, rows);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 72f;
            // As tall as its buttons on a touch layout (they grow to the touch size).
            MenuResponsive.TrackInGroup(row, element, new Vector2(0f, 72f), MenuElementKind.Container, menu.Responsive, touch: true);
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 18f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;
            Cell(menu, row, "Label", label, menu.Theme.heading, 380f, TextAlignmentOptions.MidlineLeft);
            return row;
        }

        static TextMeshProUGUI Cell(MenuCanvas menu, RectTransform row, string name, string value, MenuTextStyle style, float width,
            TextAlignmentOptions alignment)
        {
            var slot = MenuGraphics.NewRect(name, row);
            var element = slot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.preferredHeight = 70f;
            var text = MenuGraphics.Text(slot, "Text", value, style, menu.Font, Mathf.Min(style.fontSize, 40f));
            text.alignment = alignment;
            return text;
        }

        static void VolumeRow(MenuCanvas menu, RectTransform rows, string label, MenuButtonStyle step, Func<float> get, Action<float> set)
        {
            var row = Row(menu, rows, label);
            TextMeshProUGUI value = null;
            void Refresh() => MenuGraphics.SetText(value, Mathf.RoundToInt(get() * 100f) + "%");
            menu.AddButton(row, "-", step, () => { set(Mathf.Round((get() - Step) * 10f) / 10f); Refresh(); });
            value = Cell(menu, row, "Value", "", menu.Theme.heading, 150f, TextAlignmentOptions.Center);
            menu.AddButton(row, "+", step, () => { set(Mathf.Round((get() + Step) * 10f) / 10f); Refresh(); });
            Refresh();
        }

        static void ToggleRow(MenuCanvas menu, RectTransform rows, string label, MenuTexts texts, MenuButtonStyle style, Func<bool> get, Action<bool> set)
        {
            var row = Row(menu, rows, label);
            MenuButton button = null;
            button = menu.AddButton(row, get() ? texts.on : texts.off, style, () =>
            {
                set(!get());
                button.SetText(get() ? texts.on : texts.off);
            });
        }
    }

    /// <summary>A full-screen dimmed layer over a menu canvas with its own safe area, for popups.</summary>
    public sealed class Overlay
    {
        public GameObject Root;
        public RectTransform Inner;

        public static Overlay Create(MenuCanvas menu, string name, float dim)
        {
            menu.ResetEntranceCascade();
            var image = MenuGraphics.NewImage(menu.transform, name, new Color(0.07f, 0.04f, 0.09f, dim));
            image.raycastTarget = true; // blocks clicks to whatever is underneath
            image.gameObject.AddComponent<MenuLayer>();
            var inner = MenuGraphics.NewRect("SafeArea", image.transform);
            MenuGraphics.Fill(inner);
            inner.gameObject.AddComponent<MenuSafeArea>();
            PanelAnim.Add(image.gameObject, inner);
            return new Overlay { Root = image.gameObject, Inner = inner };
        }

        /// <summary>Closes an overlay with a quick fade (it is destroyed when the fade ends).</summary>
        public static void Close(GameObject root) => PanelAnim.CloseAndDestroy(root);
    }
}
