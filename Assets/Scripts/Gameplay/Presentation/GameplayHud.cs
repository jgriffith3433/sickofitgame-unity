using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Screen overlay for levels, drawn in the menu theme (ui/theme.json, including its "hud" section): the player's
    /// health, diamonds, banner and subtitle text, the objective line, a boss health bar, the level progress /
    /// rewind bar, the upgrade picker and a full-screen flash. What the script shows (texts, boss bar) is part of
    /// the saved simulation state, so it rewinds too; the animations are cosmetic.
    /// </summary>
    public sealed partial class GameplayHud : MonoBehaviour
    {
        MenuCanvas menu;
        MenuTheme theme;
        MenuHudStyle style;
        GameplayWorld world;

        TextMeshProUGUI banner;
        MenuMotionDriver bannerMotion;
        TextMeshProUGUI subtitle;
        MenuMotionDriver subtitleMotion;
        TextMeshProUGUI objective;
        RectTransform objectiveBackdrop;
        RectTransform bossRoot;
        MenuBar bossBar;
        TextMeshProUGUI bossLabel;
        Image flash;

        RectTransform healthRoot;
        MenuBar healthBar;
        TextMeshProUGUI healthCount;
        RectTransform heart;
        TextMeshProUGUI diamonds;
        RectTransform diamondsRoot;
        int shownPoints = -1;
        float diamondsPopAt = -99f;
        NumberTicker diamondsTicker;
        HudFloaters floaters;
        int shownHealth = -1;
        float healPopAt = -99f;
        float shownJuice = -1f;
        SickOfIt.Core.Rewind.RewindBudget rewardBudget;
        float pendingReward;
        /// <summary>How many named juice rewards (CLEAN KILL, SURVIVED) the HUD has floated; the rewind-juice playtest reads it.</summary>
        public int RewardsShown { get; private set; }
        public string LastRewardShown { get; private set; } = "";
        string pendingRewardLabel;
        float juicePopAt = -99f;

        string shownBanner = "", shownSubtitle = "";

        // ─── Saved state ──────────────────────────────────────────────
        string bannerText = "";
        Color bannerColor = Color.white;
        float bannerUntil;
        string subtitleText = "";
        Color subtitleColor = Color.white;
        float subtitleUntil;
        string objectiveText = "";
        readonly TargetSelector bossTarget = new TargetSelector();
        string bossName = "";
        bool bossVisible;

        float flashEndsAt;
        float flashDuration;
        Color flashColor;

        // The script's fade (fade action): game time, so it is saved and rewinds with the scene.
        Image fade;
        Color fadeColor = Color.black;
        float fadeFrom, fadeTo;
        float fadeStart, fadeDuration;

        public static GameplayHud Create(GameplayWorld world)
        {
            var theme = GameDefinition.Instance != null ? GameDefinition.Instance.menuTheme : new MenuTheme();
            var layout = new MenuLayoutConfig();
            layout.title.show = false;
            layout.subtitle.show = false;
            var menu = MenuCanvas.Create("GameplayHud", theme, layout, world.gameObject);
            menu.GetComponent<Canvas>().sortingOrder = 50;
            menu.transform.SetParent(world.transform, false);
            menu.SafeAdoptScene(world.gameObject.scene);
            HideLegacyRewindButton(world.gameObject.scene);

            var hud = menu.gameObject.AddComponent<GameplayHud>();
            hud.world = world;
            hud.menu = menu;
            hud.theme = menu.Theme;
            hud.style = menu.Theme.hud ?? new MenuHudStyle();
            hud.Build();
            return hud;
        }

        void Build()
        {
            flash = MenuGraphics.NewImage(transform, "Flash", new Color(1f, 1f, 1f, 0f));
            flash.transform.SetAsFirstSibling();
            // Under the HUD's own widgets, so lines and the pause menu still show over a black screen.
            fade = MenuGraphics.NewImage(transform, "Fade", Color.clear);
            fade.raycastTarget = false;
            fade.transform.SetAsFirstSibling();

            BuildHealth();
            BuildDiamonds();
            BuildRewind();
            BuildWeapon();
            BuildDash();
            BuildStyle();

            var place = Landscape.objective;
            objective = menu.AddText(menu.Root, "Objective", "", theme.caption, place.anchor, place.offset,
                place.size, out _, 0f, null, theme.entrance);
            var objectiveSlot = (RectTransform)objective.transform.parent;
            objectiveBackdrop = MenuGraphics.NewRect("Backdrop", objectiveSlot);
            objectiveBackdrop.SetAsFirstSibling();
            MenuGraphics.Surface(objectiveBackdrop, new MenuSurfaceStyle
            {
                shape = MenuShape.Pill,
                fill = new Color(0.12f, 0.07f, 0.13f, 0.78f),
                borderColor = new Color(0.12f, 0.07f, 0.13f, 0.92f),
                borderWidth = 3f,
                shadowColor = new Color(0f, 0f, 0f, 0.4f),
                shadowOffset = new Vector2(0f, -3f),
            });
            objectiveBackdrop.gameObject.SetActive(false);
            objective.margin = new Vector4(18f, 0f, 18f, 0f);
            Lay(objectiveSlot, l => l.objective, MenuElementKind.Text);
            // Left-aligned under the health bar in landscape; centred in its own row on a portrait screen.
            objective.alignment = MenuScreen.Portrait ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineLeft;

            place = Landscape.banner;
            banner = menu.AddText(menu.Root, "Banner", "", theme.title, place.anchor, place.offset,
                place.size, out bannerMotion, Mathf.Min(theme.title.fontSize, 96f), menu.TitleFont, theme.titleEntrance,
                new MenuMotion { type = MenuMotionType.Wobble, amount = 0.6f, speed = 0.4f }, -1f);
            Lay(banner.transform.parent, l => l.banner, MenuElementKind.Text);
            // Stay in the middle band, clear of the HUD row (objective, diamonds, pause, rewind) and the Overlord.
            banner.fontSizeMin = 36f;
            place = Landscape.subtitle;
            subtitle = menu.AddText(menu.Root, "Subtitle", "", theme.subtitle, place.anchor, place.offset,
                place.size, out subtitleMotion, 0f, null, theme.entrance);
            Lay(subtitle.transform.parent, l => l.subtitle, MenuElementKind.Text);

            BuildBossBar();
            BuildScreens();
            // Last, so "+3" rises over everything else on the HUD.
            floaters = HudFloaters.Create(menu.Root, menu.Font);
        }

        // ─── Layout: theme.json hud.landscape / hud.portrait ──────────

        static readonly MenuHudLayout DefaultLandscape = MenuHudLayout.Landscape();
        static readonly MenuHudLayout DefaultPortrait = MenuHudLayout.Portrait();

        /// <summary>The landscape placements, each falling back to the built-in one when the theme leaves it off.</summary>
        MenuHudLayout Landscape => landscapeLayout ??= Merge(style.landscape, DefaultLandscape);
        MenuHudLayout landscapeLayout;

        static MenuHudLayout Merge(MenuHudLayout layout, MenuHudLayout fallback)
        {
            if (layout == null)
                return fallback;
            MenuPlacement Or(MenuPlacement p, MenuPlacement f) => p != null && p.use ? p : f;
            return new MenuHudLayout
            {
                health = Or(layout.health, fallback.health),
                diamonds = Or(layout.diamonds, fallback.diamonds),
                pause = Or(layout.pause, fallback.pause),
                rewind = Or(layout.rewind, fallback.rewind),
                floor = Or(layout.floor, fallback.floor),
                bossBar = Or(layout.bossBar, fallback.bossBar),
                bossBarWithFloor = Or(layout.bossBarWithFloor, fallback.bossBarWithFloor),
                objective = Or(layout.objective, fallback.objective),
                banner = Or(layout.banner, fallback.banner),
                subtitle = Or(layout.subtitle, fallback.subtitle),
                rewinding = Or(layout.rewinding, fallback.rewinding),
                rewindHint = Or(layout.rewindHint, fallback.rewindHint),
                weapon = Or(layout.weapon, fallback.weapon),
                dash = Or(layout.dash, fallback.dash),
                progressBar = Or(layout.progressBar, fallback.progressBar),
            };
        }

        /// <summary>
        /// Puts a HUD element at its landscape placement, or its portrait one on a phone held upright (a portrait
        /// placement left off keeps the landscape one, fitted to the screen).
        /// </summary>
        MenuResponsive Lay(Transform target, System.Func<MenuHudLayout, MenuPlacement> pick, MenuElementKind kind,
            bool pivotAtAnchor = false, bool controlsSize = true, Vector2? landscapeSize = null, Vector2? portraitSize = null)
        {
            var landscape = Clone(pick(Landscape), landscapeSize);
            var portrait = style.portrait != null ? pick(style.portrait) : pick(DefaultPortrait);
            portrait = portrait != null && portrait.use ? Clone(portrait, portraitSize) : null;
            return MenuResponsive.Track((RectTransform)target, landscape, portrait, kind, menu.Responsive, pivotAtAnchor, controlsSize);
        }

        static MenuPlacement Clone(MenuPlacement p, Vector2? size) => new MenuPlacement
        {
            use = true,
            hide = p.hide,
            anchor = p.anchor,
            offset = p.offset,
            size = size ?? p.size,
        };

        // ─── Player health (top-left) and diamonds (top-right) ────────

        const float HeartSize = 104f;

        /// <summary>The health block's size for a bar of 'width' (the heart overlaps the bar's panel).</summary>
        static Vector2 HealthSize(float width) => new Vector2(HeartSize * 0.6f + width, HeartSize);

        void BuildHealth()
        {
            healthRoot = MenuGraphics.NewRect("PlayerHealth", menu.Root);
            healthRoot.anchorMin = healthRoot.anchorMax = healthRoot.pivot = style.healthAnchor;
            healthRoot.anchoredPosition = style.healthOffset;
            const float heartSize = HeartSize;
            const float panelHeight = 66f;
            healthRoot.sizeDelta = HealthSize(style.healthWidth);

            // Panel behind the bar, tucked under the heart.
            var panel = MenuGraphics.NewRect("Panel", healthRoot);
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(heartSize * 0.6f, -panelHeight * 0.5f);
            panel.offsetMax = new Vector2(0f, panelHeight * 0.5f);
            MenuGraphics.Surface(panel, theme.chip);

            healthBar = MenuBar.Create(panel, "Bar");
            var barRect = (RectTransform)healthBar.transform;
            MenuGraphics.Fill(barRect);
            barRect.offsetMin = new Vector2(heartSize * 0.45f, 14f);
            barRect.offsetMax = new Vector2(-104f, -14f);
            healthBar.Fill = style.healthFill;
            healthBar.LowFill = style.lowHealthFill;
            healthBar.Lost = style.healthLost;
            healthBar.Track = style.barTrack;
            healthBar.LowFraction = style.lowHealthFraction;
            healthBar.MaxSegments = style.maxSegments;

            var countSlot = MenuGraphics.NewRect("Count", panel);
            countSlot.anchorMin = new Vector2(1f, 0f);
            countSlot.anchorMax = new Vector2(1f, 1f);
            countSlot.pivot = new Vector2(1f, 0.5f);
            countSlot.sizeDelta = new Vector2(100f, 0f);
            countSlot.anchoredPosition = new Vector2(-8f, 0f);
            healthCount = MenuGraphics.Text(countSlot, "Text", "", theme.chipText, menu.Font);

            heart = MenuGraphics.NewRect("Heart", healthRoot);
            heart.anchorMin = heart.anchorMax = new Vector2(0f, 0.5f);
            heart.pivot = new Vector2(0f, 0.5f);
            heart.sizeDelta = new Vector2(heartSize, heartSize);
            MenuGraphics.Surface(heart, new MenuSurfaceStyle
            {
                shape = MenuShape.Circle,
                fill = style.healthFill,
                borderColor = theme.chip.borderColor.a > 0f ? theme.chip.borderColor : Color.white,
                borderWidth = 6f,
                shadowColor = new Color(0f, 0f, 0f, 0.45f),
                shadowOffset = new Vector2(0f, -6f),
            });
            var symbol = MenuGraphics.Text(heart, "Symbol", "♥", theme.heading, menu.Font, heartSize * 0.62f);
            symbol.enableVertexGradient = false;
            symbol.color = theme.title.outlineColor.a > 0f ? new Color(1f, 0.96f, 0.84f, 1f) : Color.white;
            symbol.rectTransform.offsetMin = new Vector2(0f, 4f);

            // The motion driver owns the slot's position, so hits shake the panel inside it.
            menu.AddMotion(healthRoot.gameObject, null, theme.entrance, 0f);

            // Landscape: hud.healthAnchor/healthOffset/healthWidth, unless hud.landscape.health is set.
            var landscape = style.landscape != null && style.landscape.health != null && style.landscape.health.use
                ? style.landscape.health
                : MenuPlacement.At(style.healthAnchor.x, style.healthAnchor.y, style.healthOffset.x, style.healthOffset.y, style.healthWidth);
            var portrait = style.portrait != null ? style.portrait.health : DefaultPortrait.health;
            MenuResponsive.Track(healthRoot, Clone(landscape, HealthSize(landscape.size.x > 0f ? landscape.size.x : style.healthWidth)),
                portrait != null && portrait.use ? Clone(portrait, HealthSize(portrait.size.x > 0f ? portrait.size.x : style.healthWidth)) : null,
                MenuElementKind.Panel, menu.Responsive, pivotAtAnchor: true);
        }

        // ─── Rewind: a labeled hold button (top-right), a rewinding overlay, and a hint on the first hit ───

        MenuButton rewindButton;
        RectTransform rewindRoot;
        TextMeshProUGUI rewindKey;
        Image rewindTint;
        TextMeshProUGUI rewindingText;
        TextMeshProUGUI rewindHint;
        MenuMotionDriver rewindHintMotion;
        float hintShownAt = -99f;
        bool wasRewinding;
        bool hintShown;
        int lastHealth = -1;

        static string RewindKeyText => Application.isMobilePlatform ? "Hold: rewind" : "Hold R: rewind";

        void BuildRewind()
        {
            // Overlay while rewinding: a teal vignette wash and a big pulsing "<<".
            rewindTint = MenuGraphics.NewImage(transform, "RewindTint", Color.clear);
            rewindTint.sprite = MenuGraphics.Vignette();
            rewindTint.transform.SetSiblingIndex(flash.transform.GetSiblingIndex() + 1);
            var place = Landscape.rewinding;
            rewindingText = menu.AddText(menu.Root, "Rewinding", "\u25C4\u25C4  Rewinding", theme.title, place.anchor,
                place.offset, place.size, out _, Mathf.Min(theme.title.fontSize, 84f), menu.TitleFont, null,
                new MenuMotion { type = MenuMotionType.Pulse, amount = 0.05f, speed = 2.5f });
            Lay(rewindingText.transform.parent, l => l.rewinding, MenuElementKind.Text);
            rewindingText.enableVertexGradient = false;
            rewindingText.color = style.rewind;
            rewindingText.transform.parent.gameObject.SetActive(false);

            var ink = theme.title.outlineColor;
            place = Landscape.rewind;
            var buttonStyle = new MenuButtonStyle
            {
                size = place.size,
                surface = new MenuSurfaceStyle
                {
                    shape = MenuShape.Circle,
                    fill = style.rewind,
                    borderColor = ink,
                    borderWidth = 7f,
                    shadowColor = ink,
                    shadowOffset = new Vector2(0f, -8f),
                    padding = new Vector2(8f, 8f),
                },
                label = new MenuTextStyle
                {
                    fontSize = 46f,
                    color = new Color(1f, 0.96f, 0.84f, 1f),
                    outlineColor = ink,
                    outlineWidth = 0.25f,
                    bold = true,
                },
                hoverFill = theme.accent,
                pressedFill = Color.Lerp(style.rewind, Color.black, 0.3f),
                disabledFill = new Color(0.35f, 0.32f, 0.36f, 0.9f),
                hoverScale = 1.1f,
                pressScale = 0.9f,
                hoverRotation = 8f,
            };
            rewindButton = menu.AddButtonAt(place.anchor, place.offset, "\u25C4\u25C4", buttonStyle, null);
            Lay(rewindButton.transform.parent, l => l.rewind, MenuElementKind.Button);
            rewindButton.Pressed += SickOfIt.Core.Rewind.RewindInput.Begin;
            rewindButton.Released += SickOfIt.Core.Rewind.RewindInput.End;
            // Keep it out of keyboard/gamepad navigation, so arrow keys never select it mid-fight.
            rewindButton.navigation = new Navigation { mode = Navigation.Mode.None };
            rewindRoot = (RectTransform)rewindButton.transform.parent;

            var keySlot = MenuGraphics.NewRect("Key", rewindRoot);
            keySlot.anchorMin = keySlot.anchorMax = new Vector2(1f, 0f);
            keySlot.pivot = new Vector2(1f, 1f);
            keySlot.sizeDelta = new Vector2(248f, 40f);
            keySlot.anchoredPosition = new Vector2(16f, -10f);
            MenuGraphics.Surface(keySlot, new MenuSurfaceStyle
            {
                shape = MenuShape.Pill,
                fill = new Color(0.12f, 0.07f, 0.13f, 0.78f),
                borderColor = new Color(0.12f, 0.07f, 0.13f, 0.92f),
                borderWidth = 3f,
                shadowColor = new Color(0f, 0f, 0f, 0.4f),
                shadowOffset = new Vector2(0f, -3f),
            });
            rewindKey = MenuGraphics.Text(keySlot, "Text", RewindKeyText, theme.caption, menu.Font, 22f);
            rewindKey.rectTransform.offsetMin = new Vector2(14f, 2f);
            rewindKey.rectTransform.offsetMax = new Vector2(-14f, -2f);

            // Taught once per level: the first time you get hurt with juice to spend.
            string hint = Application.isMobilePlatform ? Texts.rewindHintTouch : Texts.rewindHint;
            place = Landscape.rewindHint;
            rewindHint = menu.AddText(menu.Root, "RewindHint", hint, theme.subtitle, place.anchor, place.offset,
                place.size, out rewindHintMotion, 34f, null, theme.entrance,
                new MenuMotion { type = MenuMotionType.Bob, amount = 5f, speed = 1.5f }, -3f);
            Lay(rewindHint.transform.parent, l => l.rewindHint, MenuElementKind.Text);
            rewindHint.enableVertexGradient = false;
            rewindHint.color = style.rewind;
            rewindHint.transform.parent.gameObject.SetActive(false);
        }

        void UpdateRewind()
        {
            if (rewindButton == null)
                return;
            var clock = SickOfIt.Core.GameClock.Instance;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            bool rewinding = clock != null && clock.IsRewinding;
            bool canRewind = SickOfIt.Core.Rewind.RewindInput.Available;
            bool hasJuice = budget == null || budget.CanRewind;
            bool locked = SickOfIt.Core.Rewind.RewindInput.Locked;

            if (rewindRoot.gameObject.activeSelf != canRewind)
                rewindRoot.gameObject.SetActive(canRewind);
            if (!rewinding)
                rewindButton.interactable = hasJuice && !locked;
            MenuGraphics.SetText(rewindKey, locked ? Texts.rewindLocked : hasJuice ? RewindKeyText : "Out of juice");
            rewindKey.enableVertexGradient = false;
            rewindKey.color = hasJuice && !locked ? new Color(1f, 0.96f, 0.84f, 1f) : style.rewindEmpty;

            if (rewinding != wasRewinding)
            {
                Sfx.Play(rewinding ? Sfx.Events.rewindStart : Sfx.Events.rewindStop);
                if (!rewinding && !hasJuice)
                    Sfx.Play(Sfx.Events.rewindEmpty);
                wasRewinding = rewinding;
            }

            float wash = rewinding ? 0.55f + 0.1f * Mathf.Sin(Time.unscaledTime * 10f) : 0f;
            var tint = style.rewind;
            rewindTint.color = new Color(tint.r, tint.g, tint.b, Mathf.MoveTowards(rewindTint.color.a, wash, Time.unscaledDeltaTime * 4f));
            var rewindingSlot = rewindingText.transform.parent.gameObject;
            if (rewindingSlot.activeSelf != rewinding)
                rewindingSlot.SetActive(rewinding);

            // First hit with juice to spend: show the hint for a few seconds, gone once they rewind.
            var player = world != null ? world.Player : null;
            int health = player != null ? player.Health : -1;
            var hintSlot = rewindHint.transform.parent.gameObject;
            if (!hintShown && GameSettings.Hints && lastHealth >= 0 && health >= 0 && health < lastHealth && canRewind && hasJuice && !rewinding)
            {
                hintShown = true;
                hintShownAt = Time.unscaledTime;
                hintSlot.SetActive(true);
                rewindHintMotion.Restart();
            }
            lastHealth = health;
            if (hintSlot.activeSelf && (rewinding || Time.unscaledTime - hintShownAt > 4.5f))
                hintSlot.SetActive(false);
        }

        /// <summary>The mobile UI prefab's own small rewind button sat top-left, under the health bar; the HUD has its own.</summary>
        static void HideLegacyRewindButton(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid())
                return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("ButtonRewind", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && t.GetComponentInParent<MenuCanvas>(true) == null)
                        t.gameObject.SetActive(false);
                }
            }
        }

        void BuildDiamonds()
        {
            diamondsRoot = MenuGraphics.NewRect("Diamonds", menu.Root);
            var place = Landscape.diamonds;
            diamondsRoot.anchorMin = diamondsRoot.anchorMax = diamondsRoot.pivot = place.anchor;
            diamondsRoot.anchoredPosition = place.offset; // right of it: the pause button
            var row = diamondsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            var padding = theme.chip.padding;
            row.padding = new RectOffset((int)padding.x, (int)padding.x, (int)padding.y, (int)padding.y);
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fitter = diamondsRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            MenuGraphics.Surface(diamondsRoot, theme.chip);
            diamonds = MenuGraphics.Text(diamondsRoot, "Value", "", theme.chipText, menu.Font, 0f, autoSize: false);
            diamonds.textWrappingMode = TextWrappingModes.NoWrap;
            diamonds.enableVertexGradient = false;
            diamonds.color = theme.accent;
            diamondsTicker = NumberTicker.On(diamonds, v => string.Format(theme.pointsFormat, v));
            menu.AddMotion(diamondsRoot.gameObject, null, theme.entrance, 0f);
            Lay(diamondsRoot, l => l.diamonds, MenuElementKind.Panel, pivotAtAnchor: true, controlsSize: false);
        }

        void UpdateHealthAndDiamonds()
        {
            var player = world != null ? world.Player : null;
            if (player != null && healthBar != null)
            {
                int max = player.MaxHealth;
                int health = Mathf.Clamp(player.Health, 0, Mathf.Max(0, max));
                healthBar.Set(health, max);
                MenuGraphics.SetText(healthCount, $"{health}/{max}");

                float now = Time.unscaledTime;
                // Shake the panel when hit; throb the heart at low health.
                float hit = healthBar.SinceHit;
                Vector2 shake = hit < 0.3f
                    ? new Vector2(Mathf.Sin(now * 90f), Mathf.Cos(now * 77f)) * (14f * (1f - hit / 0.3f))
                    : Vector2.zero;
                foreach (Transform child in healthRoot)
                {
                    if (child.name == "Panel")
                        ((RectTransform)child).anchoredPosition = shake;
                }
                bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
                if (shownHealth >= 0 && health > shownHealth && !rewinding)
                {
                    healPopAt = now;
                    floaters?.Spawn(heart, $"+{health - shownHealth}", style.healthFill, 52f);
                }
                shownHealth = health;
                float beat = healthBar.IsLow ? 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(now * 9f)) : 1f;
                if (hit < 0.25f)
                    beat = 1.25f - hit;
                float healAge = now - healPopAt;
                if (healAge < 0.45f)
                    beat *= 1f + 0.35f * Mathf.Sin(healAge / 0.45f * Mathf.PI);
                heart.localScale = new Vector3(beat, beat, 1f);
            }

            var roguelite = RogueliteManager.Instance;
            if (roguelite != null && diamonds != null)
            {
                int points = roguelite.GetPoints();
                if (points != shownPoints)
                {
                    if (shownPoints >= 0 && points > shownPoints)
                    {
                        diamondsPopAt = Time.unscaledTime;
                        Sfx.Play(Sfx.Events.pickupDiamond);
                        floaters?.Spawn(diamondsRoot, $"+{points - shownPoints}", theme.accent);
                    }
                    if (shownPoints < 0)
                        diamondsTicker.Snap(points);
                    else
                        diamondsTicker.CountTo(points, 0.4f);
                    shownPoints = points;
                }
                float age = Time.unscaledTime - diamondsPopAt;
                float pop = age < 0.3f ? 1f + 0.25f * Mathf.Sin(age / 0.3f * Mathf.PI) : 1f;
                diamonds.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
        }

        // ─── Boss bar (top-center) ────────────────────────────────────

        void BuildBossBar()
        {
            bossRoot = MenuGraphics.NewRect("BossBar", menu.Root);
            var place = Landscape.bossBar;
            MenuGraphics.Place(bossRoot, place.anchor, place.offset, place.size);
            MenuGraphics.Surface(bossRoot, theme.chip);
            bossBar = MenuBar.Create(bossRoot, "Bar");
            var barRect = (RectTransform)bossBar.transform;
            MenuGraphics.Fill(barRect, 12f);
            bossBar.Fill = style.bossFill;
            bossBar.LowFill = style.lowHealthFill;
            bossBar.Lost = style.healthLost;
            bossBar.Track = style.barTrack;
            bossBar.LowFraction = 0.25f;
            bossBar.MaxSegments = 0; // always one smooth bar

            var labelSlot = MenuGraphics.NewRect("Label", bossRoot);
            labelSlot.anchorMin = new Vector2(0f, 1f);
            labelSlot.anchorMax = new Vector2(1f, 1f);
            labelSlot.pivot = new Vector2(0.5f, 0f);
            labelSlot.sizeDelta = new Vector2(0f, 56f);
            labelSlot.anchoredPosition = new Vector2(0f, 2f);
            bossLabel = MenuGraphics.Text(labelSlot, "Text", "", theme.heading, menu.TitleFont);
            menu.AddMotion(bossRoot.gameObject, null, theme.entrance, 0f);
            Lay(bossRoot, l => l.bossBar, MenuElementKind.Panel);
            bossRoot.gameObject.SetActive(false);
        }

        // ─── API ──────────────────────────────────────────────────────

        public void ShowText(string text, TextStyle textStyle, Color color, float until)
        {
            if (textStyle == TextStyle.Banner)
            {
                bannerText = text ?? "";
                bannerColor = color;
                bannerUntil = until;
            }
            else
            {
                subtitleText = text ?? "";
                subtitleColor = color;
                subtitleUntil = until;
            }
            Refresh(world.Now);
        }

        public void SetObjective(string text)
        {
            objectiveText = text ?? "";
            Refresh(world.Now);
        }

        public void ShowBossBar(TargetSelector target, string label)
        {
            bossTarget.player = target.player;
            bossTarget.self = false;
            bossTarget.id = target.id;
            bossTarget.tag = target.tag;
            bossTarget.entity = target.entity;
            bossTarget.all = target.all;
            bossName = label ?? "";
            bossVisible = true;
            Refresh(world.Now);
            string bossId = null;
            foreach (var e in world.Live)
            {
                if (e != null && e.IsAlive && !e.IsPlayer && GameplayWorld.Matches(e, bossTarget, null))
                {
                    bossId = e.DefId;
                    break;
                }
            }
            PlayAnalytics.BossShown(world, bossName, bossId);
        }

        public void HideBossBar()
        {
            bossVisible = false;
            Refresh(world.Now);
        }

        public void OnEntityDied(Entity entity)
        {
            if (bossVisible && GameplayWorld.Matches(entity, bossTarget, null) && !world.AnyAlive(bossTarget, null))
            {
                bossVisible = false;
                if (!entity.IsPlayer)
                    PlayAnalytics.BossDefeated(world);
            }
        }

        /// <summary>The opacity of the last full-screen flash started, after the accessibility setting (the playtest reads it).</summary>
        public float LastFlashAlpha => flashColor.a;

        public void Flash(Color color, float duration)
        {
            if (SickOfIt.Data.GameSettings.ReducedFlash)
                color.a *= 0.25f;
            flashColor = color;
            flashDuration = Mathf.Max(0.01f, duration);
            flashEndsAt = Time.unscaledTime + flashDuration;
        }

        /// <summary>How covered the screen is by the script's fade, 0 (clear) to 1 (held black).</summary>
        public float FadeAlpha => FadeAt(world != null ? world.Now : 0f);

        /// <summary>The subtitle on screen now ("" when none), for the playtest's checks of scene lines.</summary>
        public string SubtitleShown => world != null && world.Now < subtitleUntil ? subtitleText : "";

        /// <summary>The boss bar's label while the bar is on screen ("" when it isn't), for the playtest's checks of a boss intro.</summary>
        public string BossBarShown => bossRoot != null && bossRoot.gameObject.activeSelf ? bossName : "";

        /// <summary>The banner on screen now ("" when none), for the playtest's checks of stage titles.</summary>
        public string BannerShown => world != null && world.Now < bannerUntil ? bannerText : "";

        /// <summary>Fades the screen to 'color' (alpha 1, held) or back to clear over 'duration' seconds of game time.</summary>
        public void Fade(bool toColor, Color color, float duration, float now)
        {
            fadeFrom = FadeAt(now);
            fadeTo = toColor ? 1f : 0f;
            if (toColor)
                fadeColor = color;
            fadeStart = now;
            fadeDuration = Mathf.Max(0f, duration);
            UpdateFade();
        }

        float FadeAt(float now)
        {
            float t = fadeDuration > 0f ? Mathf.Clamp01((now - fadeStart) / fadeDuration) : 1f;
            return Mathf.Lerp(fadeFrom, fadeTo, t);
        }

        void UpdateFade()
        {
            if (fade == null || world == null)
                return;
            var c = fadeColor;
            c.a = FadeAlpha;
            fade.color = c;
        }

        public void Tick(float now) => Refresh(now);

        void Update()
        {
            if (flash == null)
                return;
            float left = flashEndsAt - Time.unscaledTime;
            var c = flashColor;
            c.a = left > 0f ? flashColor.a * (left / flashDuration) : 0f;
            flash.color = c;
            UpdateFade();
            UpdateHealthAndDiamonds();
            UpdateProgressBar();
            UpdateBossIntro();
            UpdateRewind();
            UpdateWeapon();
            UpdateDash();
            UpdateStyle();
            UpdateScreens();
            UpdatePickerKeys();
            UpdateCursor();
        }

        /// <summary>The aim crosshair while playing; the arrow on the pause, death and results screens, the Grudge
        /// picker and over HUD buttons (the pause and rewind buttons), where you click rather than shoot.</summary>
        void UpdateCursor()
        {
            AimCursor.Show(!Paused && !Blocked && !AimCursor.PointerOverUi, style);
        }

        // ─── Progress bar: how far through the level, and how far back you can rewind ───

        RectTransform progressRoot;
        RectTransform playedFill;
        RectTransform rewindTail;
        Image rewindImage;
        RectTransform playhead;
        TextMeshProUGUI rewindLabel;

        // ─── Floor readout (top-center): FLOOR 973 ───

        RectTransform floorRoot;
        TextMeshProUGUI floorText;
        int shownFloor = -1;
        float floorPopAt = -99f;

        void BuildFloor()
        {
            if (world.Stage == null || !world.Stage.HasFloors)
                return;
            floorRoot = MenuGraphics.NewRect("Floor", menu.Root);
            var place = Landscape.floor;
            floorRoot.anchorMin = floorRoot.anchorMax = floorRoot.pivot = place.anchor;
            floorRoot.anchoredPosition = place.offset;
            var row = floorRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            var padding = theme.chip.padding;
            row.padding = new RectOffset((int)padding.x + 12, (int)padding.x + 12, (int)padding.y, (int)padding.y);
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fitter = floorRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            MenuGraphics.Surface(floorRoot, theme.chip);
            floorText = MenuGraphics.Text(floorRoot, "Value", "", theme.chipText, menu.TitleFont, 0f, autoSize: false);
            floorText.textWrappingMode = TextWrappingModes.NoWrap;
            floorText.enableVertexGradient = false;
            floorText.color = theme.accent;
            menu.AddMotion(floorRoot.gameObject, null, theme.entrance, 0f);
            Lay(floorRoot, l => l.floor, MenuElementKind.Panel, pivotAtAnchor: true, controlsSize: false);
            // The boss bar normally sits top-center too; move it under the readout.
            if (bossRoot != null)
                Lay(bossRoot, l => l.bossBarWithFloor, MenuElementKind.Panel);
        }

        void UpdateFloor()
        {
            if (floorText == null)
                return;
            int floor = world.Stage.Floor;
            if (floor != shownFloor)
            {
                if (shownFloor >= 0)
                {
                    floorPopAt = Time.unscaledTime;
                    // The floor changed: a sweep of lava light up the screen (not while rewinding, when floors tick back).
                    if (GameClock.Instance == null || !GameClock.Instance.IsRewinding)
                        ScreenTransition.Sweep();
                }
                shownFloor = floor;
                MenuGraphics.SetText(floorText, string.Format(Texts.floorFormat ?? "FLOOR {0}", floor));
            }
            // A little punch when a floor ticks over (cosmetic, real time).
            float t = Time.unscaledTime - floorPopAt;
            float scale = t < 0.35f ? 1f + 0.22f * Mathf.Sin(t / 0.35f * Mathf.PI) : 1f;
            floorText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Where the progress bar's playhead is: floors climbed on a floor stage, otherwise level time.</summary>
        float ProgressOf(float seconds, int floor)
        {
            var stage = world.Stage;
            if (stage != null && stage.FloorProgress >= 0f && floor > 0)
                return Mathf.Clamp01((float)(stage.Floors.start - floor) / (stage.Floors.start - stage.Floors.end));
            return Mathf.Clamp01(seconds / Mathf.Max(1f, world.LevelLength));
        }

        public void BuildProgressBar()
        {
            BuildFloor();

            progressRoot = MenuGraphics.NewRect("ProgressBar", menu.Root);
            var place = Landscape.progressBar;
            MenuGraphics.Place(progressRoot, place.anchor, place.offset, place.size);
            MenuGraphics.Surface(progressRoot, new MenuSurfaceStyle
            {
                shape = MenuShape.Pill,
                fill = style.barTrack,
                borderColor = theme.chip.borderColor,
                borderWidth = 4f,
                shadowColor = new Color(0f, 0f, 0f, 0.4f),
                shadowOffset = new Vector2(0f, -4f),
            });
            var inner = MenuGraphics.NewRect("Inner", progressRoot);
            MenuGraphics.Fill(inner, 7f);

            playedFill = Bar(inner, "Played", style.progressPlayed).rectTransform;
            rewindImage = Bar(inner, "Rewind", style.rewind);
            rewindTail = rewindImage.rectTransform;

            foreach (var marker in world.Director.Script.markers)
            {
                float x = ProgressOf(marker.seconds, marker.floor);
                var tick = MenuGraphics.NewRect("Marker", inner);
                tick.anchorMin = tick.anchorMax = new Vector2(x, 0.5f);
                tick.sizeDelta = new Vector2(34f, 34f);
                MenuGraphics.Surface(tick, new MenuSurfaceStyle
                {
                    shape = MenuShape.Burst,
                    fill = style.marker,
                    borderColor = theme.title.outlineColor,
                    borderWidth = 3f,
                    shadowColor = Color.clear,
                });
                if (string.IsNullOrEmpty(marker.label))
                    continue;
                var labelSlot = MenuGraphics.NewRect("MarkerLabel", inner);
                labelSlot.anchorMin = labelSlot.anchorMax = new Vector2(x, 1f);
                labelSlot.pivot = new Vector2(0.5f, 0f);
                labelSlot.sizeDelta = new Vector2(260f, 34f);
                labelSlot.anchoredPosition = new Vector2(0f, 18f);
                var label = MenuGraphics.Text(labelSlot, "Text", marker.label, theme.caption, menu.Font, 22f);
                label.enableVertexGradient = false;
                label.color = style.marker;
            }

            playhead = MenuGraphics.NewRect("Playhead", inner);
            playhead.anchorMin = playhead.anchorMax = new Vector2(0f, 0.5f);
            playhead.sizeDelta = new Vector2(30f, 30f);
            MenuGraphics.Surface(playhead, new MenuSurfaceStyle
            {
                shape = MenuShape.Circle,
                fill = Color.white,
                borderColor = theme.title.outlineColor,
                borderWidth = 4f,
                shadowColor = Color.clear,
            });

            var rewindSlot = MenuGraphics.NewRect("RewindLabel", progressRoot);
            rewindSlot.anchorMin = rewindSlot.anchorMax = new Vector2(1f, 1f);
            rewindSlot.pivot = new Vector2(1f, 0f);
            rewindSlot.sizeDelta = new Vector2(340f, 36f);
            rewindSlot.anchoredPosition = new Vector2(-6f, 10f);
            rewindLabel = MenuGraphics.Text(rewindSlot, "Text", "", theme.caption, menu.Font, 24f);
            rewindLabel.alignment = TextAlignmentOptions.MidlineRight;
            menu.AddMotion(progressRoot.gameObject, null, theme.entrance, 0f);
            Lay(progressRoot, l => l.progressBar, MenuElementKind.Panel);
        }

        static Image Bar(RectTransform parent, string name, Color color)
        {
            var image = MenuGraphics.NewImage(parent, name, color);
            MenuGraphics.Apply(image, MenuShape.Pill, 0f);
            image.rectTransform.anchorMax = new Vector2(0f, 1f);
            return image;
        }

        void UpdateProgressBar()
        {
            if (progressRoot == null || world == null)
                return;
            UpdateFloor();
            float length = Mathf.Max(1f, world.LevelLength);
            float floorProgress = world.Stage != null ? world.Stage.FloorProgress : -1f;
            float progress = floorProgress >= 0f ? floorProgress : Mathf.Clamp01(world.Now / length);
            if (world.Director != null && !world.Director.Completed)
                progress = Mathf.Min(progress, 0.995f);
            playedFill.anchorMax = new Vector2(progress, 1f);
            playedFill.gameObject.SetActive(progress > 0.01f);
            playhead.anchorMin = playhead.anchorMax = new Vector2(progress, 0.5f);

            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            bool limited = budget != null;
            rewindTail.gameObject.SetActive(limited);
            if (!limited)
            {
                MenuGraphics.SetText(rewindLabel, "");
                return;
            }
            // The tail is how far back the juice reaches. On a floor stage, time becomes distance at the average pace so far.
            float tail = floorProgress >= 0f
                ? progress * Mathf.Clamp01(budget.Juice / Mathf.Max(1f, world.Now))
                : Mathf.Min(progress, budget.Juice / length);
            rewindTail.anchorMin = new Vector2(progress - tail, 0f);
            rewindTail.anchorMax = new Vector2(progress, 1f);
            rewindTail.gameObject.SetActive(tail > 0.005f);
            bool rewindingNow = GameClock.Instance != null && GameClock.Instance.IsRewinding;
            if (rewardBudget != budget)
            {
                if (rewardBudget != null)
                    rewardBudget.Rewarded -= OnJuiceReward;
                rewardBudget = budget;
                rewardBudget.Rewarded += OnJuiceReward;
            }
            if (pendingReward > 0f)
            {
                // A reward names itself ("CLEAN KILL +0.5s"); the plain "+Ns" pop below then skips it.
                juicePopAt = Time.unscaledTime;
                floaters?.Spawn((RectTransform)rewindLabel.transform.parent, $"{pendingRewardLabel} +{pendingReward:0.#}s", style.rewind, 40f);
                shownJuice = budget.Juice;
                RewardsShown++;
                LastRewardShown = $"{pendingRewardLabel} +{pendingReward:0.#}s";
                pendingReward = 0f;
            }
            if (shownJuice >= 0f && budget.Juice > shownJuice + 0.4f && !rewindingNow)
            {
                juicePopAt = Time.unscaledTime;
                floaters?.Spawn((RectTransform)rewindLabel.transform.parent, $"+{budget.Juice - shownJuice:0.#}s", style.rewind, 40f);
            }
            shownJuice = budget.Juice;
            float juiceAge = Time.unscaledTime - juicePopAt;
            float juiceScale = juiceAge < 0.4f ? 1f + 0.3f * Mathf.Sin(juiceAge / 0.4f * Mathf.PI) : 1f;
            rewindLabel.rectTransform.localScale = new Vector3(juiceScale, juiceScale, 1f);
            bool empty = !budget.CanRewind;
            MenuGraphics.SetText(rewindLabel, empty ? "Rewind empty" : $"Rewind {budget.Juice:0.0}s");
            rewindLabel.enableVertexGradient = false;
            rewindLabel.color = empty ? style.rewindEmpty : style.rewind;
        }

        // ─── Upgrade picker (pauses the game until a card is picked) ───

        GameObject pickerRoot;
        System.Action<int> onPick;
        int pickerCount;
        HashSet<int> pickerLocked;
        bool pickerIsShortcut;
        float timeScaleBeforePicker = 1f;

        public bool PickerOpen => pickerRoot != null;

        public void ShowUpgradePicker(string title, List<UpgradeDef> options, System.Action<int> picked)
        {
            var cards = new List<MenuCardData>();
            var colors = new List<Color>();
            for (int i = 0; i < options.Count; i++)
            {
                var def = options[i];
                int level = UpgradeProgress.LevelOf(def.id);
                cards.Add(new MenuCardData
                {
                    Badge = (i + 1).ToString(),
                    Caption = (def.iconArt != null || string.IsNullOrEmpty(def.icon) ? "" : def.icon + "  ") + $"Level {level + 1}/{def.maxLevel}",
                    Icon = def.iconArt,
                    Title = def.title,
                    Description = def.description,
                    Status = "Take it",
                    StatusColor = def.color,
                });
                colors.Add(def.color);
            }
            ShowPicker(title, "Click a card or press its number", cards, colors, picked, null);
        }

        /// <summary>A shortcut (Docs/Story.md, "Shortcuts") is being offered: the picker's cards are "keep climbing" and the shortcut.</summary>
        public bool ShortcutOpen => PickerOpen && pickerIsShortcut;

        /// <summary>The cost line on the shortcut's card, as shown (the playtest reads it).</summary>
        public string ShortcutCostShown { get; private set; } = "";

        /// <summary>The title of the shortcut's choice, as shown (the bot tells a door in the wall from the route's own shortcuts by it).</summary>
        public string ShortcutPromptShown { get; private set; } = "";

        /// <summary>
        /// A shortcut's choice point, in the picker's panel (pauses the game the same way): card 1 keeps climbing,
        /// card 2 takes the shortcut, with its cost on it ('lockedStamp' greys it out when it can't be paid).
        /// </summary>
        public void ShowShortcutChoice(string title, MenuCardData stay, MenuCardData take, Color takeColor, string costShown, string lockedStamp, System.Action<int> picked)
        {
            if (PickerOpen)
                return;
            take.LockedStamp = lockedStamp;
            ShortcutCostShown = costShown ?? "";
            ShortcutPromptShown = title ?? "";
            var locked = string.IsNullOrEmpty(lockedStamp) ? null : new HashSet<int> { 1 };
            ShowPicker(title, "Click a card or press its number", new List<MenuCardData> { stay, take },
                new List<Color> { theme.muted, takeColor }, picked, locked);
            pickerIsShortcut = PickerOpen;
        }

        void ShowPicker(string title, string hint, List<MenuCardData> cards, List<Color> colors, System.Action<int> picked, HashSet<int> locked)
        {
            if (PickerOpen || cards.Count == 0)
                return;
            onPick = picked;
            pickerCount = cards.Count;
            pickerLocked = locked;
            pickerIsShortcut = false;
            Sfx.Play(Sfx.Events.upgradeOffer);
            timeScaleBeforePicker = Time.timeScale;
            Time.timeScale = 0f;

            var dim = MenuGraphics.NewImage(transform, "UpgradePicker", new Color(0.07f, 0.04f, 0.09f, 0.82f));
            dim.raycastTarget = true;
            dim.gameObject.AddComponent<MenuLayer>();
            pickerRoot = dim.gameObject;
            var inner = MenuGraphics.NewRect("SafeArea", dim.transform);
            MenuGraphics.Fill(inner);
            inner.gameObject.AddComponent<MenuSafeArea>();
            PanelAnim.Add(pickerRoot, inner);

            menu.AddText(inner, "Title", title, theme.title, new Vector2(0.5f, 0.84f), Vector2.zero, new Vector2(1700f, 170f),
                out _, Mathf.Min(theme.title.fontSize, 110f), menu.TitleFont, theme.titleEntrance,
                new MenuMotion { type = MenuMotionType.Wobble, amount = 1.2f, speed = 0.4f }, -2f);
            menu.AddText(inner, "Hint", hint, theme.caption, new Vector2(0.5f, 0.12f), Vector2.zero,
                new Vector2(1200f, 50f), out _, 0f, null, theme.entrance);

            var list = menu.AddList(new MenuList
            {
                anchor = new Vector2(0.5f, 0.46f),
                size = new Vector2(1800f, 560f),
                direction = MenuListDirection.Horizontal,
                spacing = new Vector2(36f, 0f),
                itemSize = new Vector2(400f, 440f),
                portrait = menu.Responsive.screens?.upgradePicker,
            }, theme.card.size, inner);

            for (int i = 0; i < cards.Count; i++)
            {
                int index = i;
                var view = MenuCard.Add(menu, list, cards[i], () => Choose(index));
                if (view.Title != null && i < colors.Count)
                {
                    view.Title.enableVertexGradient = false;
                    view.Title.color = Color.Lerp(colors[i], Color.white, 0.25f);
                }
            }
        }

        public void Choose(int index)
        {
            if (!PickerOpen || index < 0 || index >= pickerCount || (pickerLocked != null && pickerLocked.Contains(index)))
                return;
            PanelAnim.CloseAndDestroy(pickerRoot);
            pickerRoot = null;
            pickerLocked = null;
            pickerIsShortcut = false;
            Sfx.Play(Sfx.Events.upgradePick);
            Time.timeScale = timeScaleBeforePicker <= 0f ? 1f : timeScaleBeforePicker;
            var callback = onPick;
            onPick = null;
            callback?.Invoke(index);
        }

        void UpdatePickerKeys()
        {
            if (!PickerOpen)
                return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) Choose(3);
        }

        void OnJuiceReward(float seconds, string label)
        {
            pendingReward += seconds;
            pendingRewardLabel = label;
        }

        void OnDestroy()
        {
            if (rewardBudget != null)
                rewardBudget.Rewarded -= OnJuiceReward;
            // Never leave the game frozen behind us (picker, pause or results open when the level unloads).
            if (PickerOpen || Paused || resultsRoot != null)
                Time.timeScale = 1f;
            if (Paused || resultsRoot != null)
                GameClock.Instance?.Resume();
            // Menus and videos keep the system arrow.
            AimCursor.Reset(style);
        }

        void Refresh(float now)
        {
            if (banner == null)
                return;
            string bannerNow = now < bannerUntil ? bannerText : "";
            if (bannerNow != shownBanner)
            {
                shownBanner = bannerNow;
                MenuGraphics.SetText(banner, bannerNow);
                // The script picks a color per banner; keep the theme's gradient look in that color.
                banner.enableVertexGradient = true;
                var top = Color.Lerp(bannerColor, Color.white, 0.45f);
                banner.colorGradient = new VertexGradient(top, top, bannerColor, bannerColor);
                if (bannerNow.Length > 0)
                {
                    bannerMotion.Restart();
                    Sfx.Play(Sfx.Events.bannerShow);
                }
            }
            string subtitleNow = now < subtitleUntil ? subtitleText : "";
            if (subtitleNow != shownSubtitle)
            {
                shownSubtitle = subtitleNow;
                MenuGraphics.SetText(subtitle, subtitleNow);
                subtitle.enableVertexGradient = false;
                subtitle.color = subtitleColor == Color.white ? theme.subtitle.color : subtitleColor;
                if (subtitleNow.Length > 0)
                    subtitleMotion.Restart();
            }
            MenuGraphics.SetText(objective, objectiveText);
            if (objectiveBackdrop != null)
            {
                bool hasObjective = !string.IsNullOrEmpty(objectiveText);
                if (objectiveBackdrop.gameObject.activeSelf != hasObjective)
                    objectiveBackdrop.gameObject.SetActive(hasObjective);
                if (hasObjective)
                {
                    bool portrait = MenuScreen.Portrait;
                    objective.alignment = portrait ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineLeft;
                    var slot = (RectTransform)objective.transform.parent;
                    float maxWidth = slot.rect.width > 0f ? slot.rect.width : 740f;
                    Vector2 pref = objective.GetPreferredValues(objectiveText, maxWidth - 36f, 48f);
                    float width = Mathf.Clamp(pref.x + 36f, 140f, maxWidth);
                    objectiveBackdrop.anchorMin = objectiveBackdrop.anchorMax = objectiveBackdrop.pivot =
                        new Vector2(portrait ? 0.5f : 0f, 0.5f);
                    objectiveBackdrop.anchoredPosition = Vector2.zero;
                    objectiveBackdrop.sizeDelta = new Vector2(width, 44f);
                }
            }

            Entity boss = null;
            if (bossVisible)
            {
                foreach (var e in world.Live)
                {
                    if (e.IsAlive && GameplayWorld.Matches(e, bossTarget, null))
                    {
                        boss = e;
                        break;
                    }
                }
                if (boss == null && bossTarget.player)
                    boss = world.Player;
            }
            if (boss != null && !bossRoot.gameObject.activeSelf)
            {
                Sfx.Play(Sfx.Events.bossAppear);
                bossIntroAt = Time.unscaledTime;
                BossIntros++;
                Flash(new Color(1f, 0.85f, 0.6f, 0.35f), 0.4f);
            }
            bossRoot.gameObject.SetActive(boss != null);
            if (boss != null)
            {
                bossHealth = boss.Health;
                bossMaxHealth = boss.MaxHealth;
                MenuGraphics.SetText(bossLabel, bossName);
                // Set at once when the intro is over (this also runs while the game is paused for a rewind);
                // during it, UpdateBossIntro fills the bar.
                if (Time.unscaledTime - bossIntroAt >= BossIntroSeconds)
                    SetBossBar();
            }
        }

        // ─── Boss intro: the name drops in, the bar fills from empty ──

        /// <summary>How long the boss bar's intro takes (name in, bar filling).</summary>
        public const float BossIntroSeconds = 1.3f;
        float bossIntroAt = -99f;
        int bossHealth, bossMaxHealth, bossShownValue;

        /// <summary>How many boss bars have been introduced (the playtest reads it).</summary>
        public static int BossIntros { get; private set; }
        /// <summary>Seconds since the boss bar appeared (large when no bar is up).</summary>
        public float BossIntroAge => bossRoot != null && bossRoot.gameObject.activeSelf ? Time.unscaledTime - bossIntroAt : 999f;
        /// <summary>How full the boss bar draws right now, 0 to 1 (it climbs during the intro).</summary>
        public float BossBarFill => bossMaxHealth > 0 ? Mathf.Clamp01((float)bossShownValue / bossMaxHealth) : 0f;
        /// <summary>The boss bar's rectangle in screen pixels (the playtest checks it is on screen).</summary>
        public Rect BossBarScreenRect => ScreenRect(bossRoot);

        static Rect ScreenRect(RectTransform rect)
        {
            if (rect == null)
                return Rect.zero;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        void SetBossBar()
        {
            bossShownValue = bossHealth;
            bossBar.Set(bossHealth, bossMaxHealth);
        }

        void UpdateBossIntro()
        {
            if (bossRoot == null || !bossRoot.gameObject.activeSelf)
                return;
            float t = (Time.unscaledTime - bossIntroAt) / BossIntroSeconds;
            if (t >= 1f)
            {
                if (bossLabel.rectTransform.localScale.x != 1f)
                    bossLabel.rectTransform.localScale = Vector3.one;
                bossLabel.alpha = 1f;
                if (bossShownValue != bossHealth)
                    SetBossBar();
                return;
            }
            // The name first: it lands from large with a small overshoot, the bar waits, then fills.
            float nameT = Mathf.Clamp01(t / 0.4f);
            float c1 = 1.70158f, c3 = c1 + 1f;
            float back = 1f + c3 * Mathf.Pow(nameT - 1f, 3f) + c1 * Mathf.Pow(nameT - 1f, 2f);
            float s = Mathf.LerpUnclamped(2f, 1f, back);
            bossLabel.rectTransform.localScale = new Vector3(s, s, 1f);
            bossLabel.alpha = Mathf.Clamp01(nameT * 3f);
            float fillT = Mathf.Clamp01((t - 0.35f) / 0.55f);
            float eased = 1f - (1f - fillT) * (1f - fillT);
            bossShownValue = Mathf.Min(bossHealth, Mathf.RoundToInt(bossMaxHealth * eased));
            bossBar.Set(bossShownValue, bossMaxHealth);
        }

        // ─── Rewind ───────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.String(bannerText); w.Float(bannerUntil); SaveColor(w, bannerColor);
            w.String(subtitleText); w.Float(subtitleUntil); SaveColor(w, subtitleColor);
            w.String(objectiveText);
            w.Bool(bossVisible);
            w.String(bossName);
            w.Bool(bossTarget.player);
            w.Bool(bossTarget.all);
            w.String(bossTarget.id);
            w.String(bossTarget.tag);
            w.String(bossTarget.entity);
            SaveColor(w, fadeColor);
            w.Float(fadeFrom); w.Float(fadeTo);
            w.Float(fadeStart); w.Float(fadeDuration);
        }

        public void LoadState(StateReader r)
        {
            bannerText = r.String(); bannerUntil = r.Float(); bannerColor = LoadColor(r);
            subtitleText = r.String(); subtitleUntil = r.Float(); subtitleColor = LoadColor(r);
            objectiveText = r.String();
            bossVisible = r.Bool();
            bossName = r.String();
            bossTarget.player = r.Bool();
            bossTarget.all = r.Bool();
            bossTarget.id = r.String();
            bossTarget.tag = r.String();
            bossTarget.entity = r.String();
            fadeColor = LoadColor(r);
            fadeFrom = r.Float(); fadeTo = r.Float();
            fadeStart = r.Float(); fadeDuration = r.Float();
            UpdateFade();
            Refresh(world.Now);
        }

        static void SaveColor(StateWriter w, Color c)
        {
            w.Float(c.r); w.Float(c.g); w.Float(c.b); w.Float(c.a);
        }

        static Color LoadColor(StateReader r) => new Color(r.Float(), r.Float(), r.Float(), r.Float());
    }
}
