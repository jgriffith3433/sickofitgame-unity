using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    // Menus (main menu, level/video selection, shop) are built at runtime from these settings, so restyling
    // or rearranging them is a JSON edit (ui/theme.json for the look, each screen's "layout" for placement).
    // Positions use anchors: [0,0] is the bottom-left of the screen, [1,1] the top-right; offsets are in
    // reference pixels (1920x1080) from that anchor. The whole menu scales to fit any screen.

    public enum MenuShape
    {
        [Tooltip("Rectangle with rounded corners (cornerRadius).")] Rounded,
        [Tooltip("Fully rounded ends.")] Pill,
        [Tooltip("Sharp corners.")] Rectangle,
        Circle,
        [Tooltip("Comic starburst.")] Burst,
        [Tooltip("The 'sprite' field, 9-sliced if it has borders.")] Sprite,
        [Tooltip("Nothing drawn (text only).")] None,
    }

    public enum MenuMotionType { None, Bob, Pulse, Wobble, Sway, Spin, Jitter }

    public enum MenuEntranceType { None, Fade, Pop, SlideLeft, SlideRight, SlideUp, SlideDown, Drop }

    public enum MenuListDirection { Vertical, Horizontal, Grid }

    public enum MenuPattern { None, Stripes, Dots, Checker }

    public enum MenuTextAlign { Center, Left, Right }

    [Serializable]
    public class MenuMotion
    {
        public MenuMotionType type = MenuMotionType.None;
        [Tooltip("Bob/Jitter: pixels. Wobble/Sway: degrees. Pulse: scale change (0.05 = 5%). Spin: degrees per second.")]
        public float amount = 8f;
        [Tooltip("Cycles per second (Spin ignores it).")]
        public float speed = 1f;
        [Tooltip("Offset in cycles, so neighbours don't move in lockstep.")]
        public float phase;
    }

    [Serializable]
    public class MenuEntrance
    {
        public MenuEntranceType type = MenuEntranceType.Pop;
        public float duration = 0.35f;
        [Tooltip("Seconds before the first element appears.")]
        public float delay = 0.05f;
        [Tooltip("Extra delay per element, so lists cascade in.")]
        public float stagger = 0.06f;
        [Tooltip("How far slides start from, in pixels.")]
        public float distance = 400f;
    }

    [Serializable]
    public class MenuTextStyle
    {
        public float fontSize = 40f;
        public Color color = Color.white;
        [Tooltip("Vertical gradient. Leave both transparent for a flat color.")]
        public Color gradientTop = Color.clear;
        public Color gradientBottom = Color.clear;
        public Color outlineColor = new Color(0f, 0f, 0f, 0.85f);
        [Tooltip("0 = no outline, 0.3 = very thick.")]
        public float outlineWidth = 0.15f;
        [Tooltip("Drop shadow drawn behind the text. Transparent = none.")]
        public Color shadowColor = Color.clear;
        public Vector2 shadowOffset = new Vector2(4f, -4f);
        public bool bold = true;
        public bool italic;
        public bool uppercase;
        public float characterSpacing;
        public float lineSpacing;
        public MenuTextAlign align = MenuTextAlign.Center;
    }

    [Serializable]
    public class MenuSurfaceStyle
    {
        public MenuShape shape = MenuShape.Rounded;
        [Tooltip("Used when shape is Sprite.")]
        public Sprite sprite;
        public float cornerRadius = 28f;
        public Color fill = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        public Color borderColor = Color.clear;
        public float borderWidth;
        [Tooltip("Transparent = no shadow.")]
        public Color shadowColor = new Color(0f, 0f, 0f, 0.45f);
        public Vector2 shadowOffset = new Vector2(0f, -8f);
        [Tooltip("Space between the edge and the content, in pixels.")]
        public Vector2 padding = new Vector2(24f, 16f);
    }

    [Serializable]
    public class MenuButtonStyle
    {
        public Vector2 size = new Vector2(460f, 110f);
        public MenuSurfaceStyle surface = new MenuSurfaceStyle();
        public MenuTextStyle label = new MenuTextStyle();
        [Tooltip("Fill while hovered or selected. Transparent = a brighter version of the fill.")]
        public Color hoverFill = Color.clear;
        [Tooltip("Fill while held down. Transparent = a darker version of the fill.")]
        public Color pressedFill = Color.clear;
        public Color disabledFill = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        [Tooltip("Label color while hovered. Transparent = unchanged.")]
        public Color hoverLabelColor = Color.clear;
        public float hoverScale = 1.08f;
        public float pressScale = 0.93f;
        [Tooltip("Degrees it rotates toward while hovered.")]
        public float hoverRotation = -3f;
        [Tooltip("Resting rotation in degrees.")]
        public float tilt;
        [Tooltip("Flip the tilt on every other button for a hand-placed look.")]
        public bool alternateTilt;
        [Tooltip("How quickly it reacts to hover/press (higher = snappier).")]
        public float responsiveness = 18f;
        public MenuMotion idle = new MenuMotion();

        /// <summary>A copy to tweak (size, colors) without changing the theme. Nested styles are shared.</summary>
        public MenuButtonStyle Clone() => (MenuButtonStyle)MemberwiseClone();
    }

    [Serializable]
    public class MenuBackdrop
    {
        [Tooltip("Full-screen picture behind the menu, cropped to fill.")]
        public Sprite image;
        public Color imageTint = Color.white;
        [Tooltip("Color laid over the image. Transparent = none.")]
        public Color tint = Color.clear;
        public MenuPattern pattern = MenuPattern.None;
        public Color patternColor = new Color(1f, 1f, 1f, 0.06f);
        [Tooltip("Pattern tile size in pixels.")]
        public float patternScale = 90f;
        [Tooltip("Pixels per second the pattern drifts.")]
        public Vector2 patternScroll = new Vector2(30f, -15f);
        [Tooltip("Darkened edges. Transparent = none.")]
        public Color vignette = Color.clear;
    }

    /// <summary>
    /// Where an element goes on one screen shape. Portrait placements are in the portrait layout space
    /// (theme.json responsive.portraitReferenceResolution, 1080 wide). Whatever the placement, an element is kept
    /// inside the safe area and shrunk to fit it, so nothing leaves the screen.
    /// </summary>
    [Serializable]
    public class MenuPlacement
    {
        [Tooltip("Use this placement. Off = fall back (in portrait: the landscape placement, kept on screen).")]
        public bool use;
        [Tooltip("Leave the element out on this screen shape (decorations, stickers).")]
        public bool hide;
        public Vector2 anchor = new Vector2(0.5f, 0.5f);
        public Vector2 offset;
        [Tooltip("Size in pixels. Zero = the element's own size.")]
        public Vector2 size;

        public static MenuPlacement At(float anchorX, float anchorY, float offsetX, float offsetY, float width = 0f, float height = 0f) =>
            new MenuPlacement
            {
                use = true,
                anchor = new Vector2(anchorX, anchorY),
                offset = new Vector2(offsetX, offsetY),
                size = new Vector2(width, height),
            };
    }

    /// <summary>A list's portrait version: where it goes, and how it stacks or wraps on a tall screen.</summary>
    [Serializable]
    public class MenuPortraitList
    {
        [Tooltip("Use this in portrait. Off = the landscape list, kept on screen.")]
        public bool use;
        public Vector2 anchor = new Vector2(0.5f, 0.45f);
        public Vector2 offset;
        [Tooltip("Area in pixels. Zero = the landscape size (shrunk to fit).")]
        public Vector2 size;
        public MenuListDirection direction = MenuListDirection.Vertical;
        [Tooltip("Grid only: columns. 0 = as many as fit.")]
        public int columns;
        public Vector2 spacing = new Vector2(28f, 22f);
        [Tooltip("Item size in pixels. Zero = the landscape item size.")]
        public Vector2 itemSize;

        public static MenuPortraitList At(float anchorX, float anchorY, float width, float height, MenuListDirection direction,
            float itemWidth, float itemHeight, float spacing, int columns = 0) =>
            new MenuPortraitList
            {
                use = true,
                anchor = new Vector2(anchorX, anchorY),
                size = new Vector2(width, height),
                direction = direction,
                itemSize = new Vector2(itemWidth, itemHeight),
                spacing = new Vector2(spacing, spacing),
                columns = columns,
            };
    }

    [Serializable]
    public class MenuLabel
    {
        public bool show = true;
        public string text = "";
        public Vector2 anchor = new Vector2(0.5f, 0.85f);
        public Vector2 offset;
        public float rotation;
        [Tooltip("0 = the theme style's size.")]
        public float fontSize;
        public MenuMotion motion = new MenuMotion();
        [Tooltip("Where it goes on a portrait screen (size is ignored: the text sets it).")]
        public MenuPlacement portrait = new MenuPlacement();
    }

    [Serializable]
    public class MenuList
    {
        public Vector2 anchor = new Vector2(0.5f, 0.45f);
        public Vector2 offset;
        [Tooltip("Area the items are laid out in, in pixels. Content that doesn't fit scrolls.")]
        public Vector2 size = new Vector2(1500f, 640f);
        public MenuListDirection direction = MenuListDirection.Vertical;
        [Tooltip("Grid only: columns. 0 = as many as fit.")]
        public int columns;
        public Vector2 spacing = new Vector2(28f, 22f);
        [Tooltip("Item size in pixels. Zero = the button style's size.")]
        public Vector2 itemSize;
        [Tooltip("The list on a portrait screen: stack or wrap instead of shrinking.")]
        public MenuPortraitList portrait = new MenuPortraitList();
    }

    [Serializable]
    public class MenuDecoration
    {
        [Tooltip("Text to show. Leave empty for a plain shape or sprite.")]
        public string text = "";
        [Tooltip("Theme text style: title, subtitle, heading, body, caption.")]
        public string style = "caption";
        public float fontSize;
        public Color textColor = Color.clear;
        [Tooltip("Shape drawn behind the text (Burst makes a comic sticker).")]
        public MenuShape shape = MenuShape.None;
        public Sprite sprite;
        public Color fill = new Color(1f, 0.8f, 0.2f, 1f);
        public Color borderColor = Color.clear;
        public float borderWidth;
        [Tooltip("Size in pixels. Zero = fit the text.")]
        public Vector2 size;
        public Vector2 anchor = new Vector2(0.5f, 0.5f);
        public Vector2 offset;
        public float rotation;
        public MenuMotion motion = new MenuMotion();
        [Tooltip("Where it goes on a portrait screen ('hide' leaves it out there).")]
        public MenuPlacement portrait = new MenuPlacement();
    }

    [Serializable]
    public class MenuLayoutConfig
    {
        public MenuBackdrop backdrop = new MenuBackdrop();
        public MenuLabel title = new MenuLabel();
        public MenuLabel subtitle = new MenuLabel { anchor = new Vector2(0.5f, 0.76f) };
        public MenuList list = new MenuList();
        [Tooltip("Back button position (selection screens and shop).")]
        public Vector2 backAnchor = new Vector2(0f, 0f);
        public Vector2 backOffset = new Vector2(150f, 90f);
        public string backText = "Back";
        [Tooltip("Where the health/diamond chips go.")]
        public Vector2 statsAnchor = new Vector2(1f, 1f);
        public Vector2 statsOffset = new Vector2(-40f, -40f);
        public List<MenuDecoration> decorations = new List<MenuDecoration>();
        [Tooltip("Back button on a portrait screen (size is the button's).")]
        public MenuPlacement portraitBack = new MenuPlacement();
        [Tooltip("Health/diamond chips on a portrait screen.")]
        public MenuPlacement portraitStats = new MenuPlacement();
    }

    /// <summary>Where each HUD element goes on one screen shape (see <see cref="MenuPlacement"/>).</summary>
    [Serializable]
    public class MenuHudLayout
    {
        [Tooltip("Player health; size x = the bar's width. Landscape: off = hud.healthAnchor/healthOffset/healthWidth.")]
        public MenuPlacement health = new MenuPlacement();
        [Tooltip("Diamond counter (its corner sits on the anchor).")]
        public MenuPlacement diamonds = new MenuPlacement();
        [Tooltip("Pause button.")]
        public MenuPlacement pause = new MenuPlacement();
        [Tooltip("Hold-to-rewind button.")]
        public MenuPlacement rewind = new MenuPlacement();
        [Tooltip("FLOOR readout on floor stages (its top edge sits on the anchor).")]
        public MenuPlacement floor = new MenuPlacement();
        public MenuPlacement bossBar = new MenuPlacement();
        [Tooltip("Boss bar on stages that also show the floor readout.")]
        public MenuPlacement bossBarWithFloor = new MenuPlacement();
        public MenuPlacement objective = new MenuPlacement();
        public MenuPlacement banner = new MenuPlacement();
        public MenuPlacement subtitle = new MenuPlacement();
        [Tooltip("The big '<< Rewinding' while rewinding.")]
        public MenuPlacement rewinding = new MenuPlacement();
        [Tooltip("'Ouch! Hold R to undo that.'")]
        public MenuPlacement rewindHint = new MenuPlacement();
        [Tooltip("Level progress / rewind juice bar.")]
        public MenuPlacement progressBar = new MenuPlacement();

        /// <summary>The desktop/landscape HUD, as it always looked (1920x1080 space).</summary>
        public static MenuHudLayout Landscape() => new MenuHudLayout
        {
            diamonds = MenuPlacement.At(1f, 1f, -140f, -48f),
            pause = MenuPlacement.At(1f, 1f, -80f, -78f, 88f, 88f),
            rewind = MenuPlacement.At(1f, 1f, -112f, -214f, 124f, 124f),
            floor = MenuPlacement.At(0.5f, 1f, 0f, -40f),
            bossBar = MenuPlacement.At(0.5f, 1f, 0f, -112f, 780f, 54f),
            bossBarWithFloor = MenuPlacement.At(0.5f, 1f, 0f, -184f, 780f, 54f),
            objective = MenuPlacement.At(0f, 1f, 436f, -150f, 800f, 44f),
            banner = MenuPlacement.At(0.5f, 0.52f, 0f, 0f, 1100f, 150f),
            subtitle = MenuPlacement.At(0.5f, 0.2f, 0f, 0f, 1600f, 90f),
            rewinding = MenuPlacement.At(0.5f, 0.8f, 0f, 0f, 1200f, 130f),
            rewindHint = MenuPlacement.At(1f, 1f, -520f, -300f, 760f, 70f),
            progressBar = MenuPlacement.At(0.5f, 0f, 0f, 42f, 1500f, 30f),
        };

        /// <summary>
        /// A phone held upright (1080 wide): the game is a 16:9 band in the middle, so the readouts stack in the
        /// space above it and the thumb controls (rewind, the progress bar) sit in the space below it.
        /// </summary>
        public static MenuHudLayout Portrait() => new MenuHudLayout
        {
            health = MenuPlacement.At(0f, 1f, 24f, -24f, 330f),
            diamonds = MenuPlacement.At(1f, 1f, -176f, -40f),
            pause = MenuPlacement.At(1f, 1f, -90f, -86f, 124f, 124f),
            rewind = MenuPlacement.At(1f, 0f, -130f, 160f, 180f, 180f),
            floor = MenuPlacement.At(0.5f, 1f, 0f, -176f),
            bossBar = MenuPlacement.At(0.5f, 1f, 0f, -430f, 960f, 54f),
            bossBarWithFloor = MenuPlacement.At(0.5f, 1f, 0f, -430f, 960f, 54f),
            objective = MenuPlacement.At(0.5f, 1f, 0f, -300f, 1000f, 48f),
            banner = MenuPlacement.At(0.5f, 0.52f, 0f, 0f, 1000f, 150f),
            subtitle = MenuPlacement.At(0.5f, 0.3f, 0f, 0f, 1000f, 150f),
            rewinding = MenuPlacement.At(0.5f, 1f, 0f, -620f, 1000f, 130f),
            rewindHint = MenuPlacement.At(0.5f, 1f, 0f, -530f, 1000f, 70f),
            progressBar = MenuPlacement.At(0.5f, 0f, 0f, 320f, 1000f, 30f),
        };
    }

    /// <summary>Portrait versions of the in-level screens (pause, settings, results, upgrade picker, death scene).</summary>
    [Serializable]
    public class MenuPortraitScreens
    {
        [Tooltip("Pause menu buttons.")]
        public MenuPortraitList pauseList = MenuPortraitList.At(0.5f, 0.38f, 900f, 720f, MenuListDirection.Vertical, 760f, 0f, 18f);
        [Tooltip("The settings panel.")]
        public MenuPlacement settingsPanel = MenuPlacement.At(0.5f, 0.5f, 0f, 0f, 1032f, 1500f);
        [Tooltip("Upgrade cards between levels: they wrap into a grid.")]
        public MenuPortraitList upgradePicker = MenuPortraitList.At(0.5f, 0.45f, 1032f, 1240f, MenuListDirection.Grid, 440f, 480f, 28f, 2);
        [Tooltip("Results: the stat rows (label at anchor x 'labelX', value at 'valueX', from 'statsTop' down by 'statsStep').")]
        public float resultsLabelX = 0.3f;
        public float resultsValueX = 0.74f;
        public float resultsStatsTop = 0.72f;
        public float resultsStatsStep = 0.055f;
        public MenuPlacement resultsGrade = MenuPlacement.At(0.5f, 0.37f, 0f, 0f, 330f, 330f);
        public MenuPlacement resultsQuip = MenuPlacement.At(0.5f, 0.22f, 0f, 0f, 1000f, 80f);
        public MenuPlacement resultsContinue = MenuPlacement.At(0.5f, 0.09f, 0f, 0f, 520f, 124f);
        [Tooltip("The death scene's Rage Quit (top center, clear of the rewind/skip row at the bottom).")]
        public MenuPlacement rageQuit = MenuPlacement.At(0.5f, 1f, 0f, -90f, 400f, 124f);
    }

    /// <summary>
    /// How every menu and the HUD adapt to the screen: one layout system. Layouts are written in a fixed space
    /// sized on the screen's short side (1920x1080 landscape, 1080x1920 portrait), so a button is the same
    /// physical size on any phone; everything stays inside the device safe area; on a portrait screen elements
    /// take their portrait placement and lists stack or wrap; and buttons there are never smaller than a thumb.
    /// </summary>
    [Serializable]
    public class MenuResponsiveStyle
    {
        [Tooltip("Layout space on a portrait screen; its width is the short side, like 1080 in 1920x1080.")]
        public Vector2 portraitReferenceResolution = new Vector2(1080f, 1920f);
        [Tooltip("Smallest button in layout pixels on the short side's scale: 124 of 1080 is about 44 pt on a 390 pt wide phone.")]
        public float minTouchSize = 124f;
        [Tooltip("Also grow buttons to minTouchSize in landscape (off keeps the landscape look).")]
        public bool touchSizeInLandscape;
        [Tooltip("Gap kept between elements and the safe area's edge in portrait, in pixels.")]
        public float portraitMargin = 24f;
        [Tooltip("Extra room above the home bar, as a fraction of the screen height.")]
        public float bottomMargin = 0.03f;
        [Tooltip("On the web: room for the page's beta/build badge at the top, as a fraction of the screen height.")]
        public float webTopMargin = 0.1f;
        public float webTopMarginPortrait = 0.065f;
        [Tooltip("Pause, settings, results, upgrade picker and death scene in portrait.")]
        public MenuPortraitScreens screens = new MenuPortraitScreens();
    }

    /// <summary>In-level HUD: player health, diamonds, boss bar, level progress / rewind bar, enemy health bars.</summary>
    [Serializable]
    public class MenuHudStyle
    {
        [Tooltip("Where the player's health sits (pivot = anchor, so it grows inward).")]
        public Vector2 healthAnchor = new Vector2(0f, 1f);
        public Vector2 healthOffset = new Vector2(36f, -30f);
        [Tooltip("Width of the health bar panel next to the heart.")]
        public float healthWidth = 440f;
        public Color healthFill = new Color(1f, 0.36f, 0.23f, 1f);
        [Tooltip("Fill color at low health (the heart also throbs).")]
        public Color lowHealthFill = new Color(1f, 0.18f, 0.18f, 1f);
        [Tooltip("At or below this fraction of max health counts as low.")]
        public float lowHealthFraction = 0.34f;
        [Tooltip("Health just lost: flashes, then drains away.")]
        public Color healthLost = new Color(1f, 0.96f, 0.84f, 1f);
        public Color barTrack = new Color(0.12f, 0.07f, 0.13f, 0.85f);
        [Tooltip("Health drawn as one segment per hit point up to this many; above it, one smooth bar.")]
        public int maxSegments = 12;

        public Color bossFill = new Color(0.72f, 0.25f, 1f, 1f);
        public Color progressPlayed = new Color(1f, 0.96f, 0.84f, 0.95f);
        public Color rewind = new Color(0.18f, 0.77f, 0.71f, 1f);
        public Color rewindEmpty = new Color(1f, 0.36f, 0.23f, 1f);
        public Color marker = new Color(1f, 0.79f, 0.24f, 1f);
        [Tooltip("Dark edge around enemy health bars in the world.")]
        public Color worldBarOutline = new Color(0.12f, 0.07f, 0.13f, 1f);

        [Tooltip("Where the HUD elements go on a landscape screen (desktop, a phone on its side).")]
        public MenuHudLayout landscape = MenuHudLayout.Landscape();
        [Tooltip("Where they go on a phone held upright.")]
        public MenuHudLayout portrait = MenuHudLayout.Portrait();
    }

    /// <summary>A results-screen grade: the best one whose minScore the run reaches.</summary>
    [Serializable]
    public class MenuGrade
    {
        public string grade = "C";
        [Tooltip("Score out of 100 needed (100 minus points lost to hits and rewinds).")]
        public int minScore;
        public string quip = "";
        public Color color = Color.white;
    }

    /// <summary>Words on the in-level screens (pause, settings, results, death).</summary>
    [Serializable]
    public class MenuTexts
    {
        public string pauseTitle = "Paused";
        public string pauseSubtitle = "Take a breath. The enemies will wait. Probably.";
        public string resume = "Resume";
        public string restart = "Restart Level";
        public string settings = "Settings";
        public string quitToMenu = "Quit to Menu";

        public string settingsTitle = "Settings";
        public string sound = "Sound";
        public string masterVolume = "Volume";
        public string musicVolume = "Music";
        public string effectsVolume = "Effects";
        public string voiceVolume = "Voices";
        public string screenShake = "Screen Shake";
        public string hints = "Hints";
        public string on = "On";
        public string off = "Off";
        public string done = "Done";

        public string resultsTitle = "Level Clear!";
        public string resultsTime = "Time";
        public string resultsKills = "Kills";
        public string resultsHits = "Hits Taken";
        public string resultsRewinds = "Rewinds";
        public string resultsDiamonds = "Diamonds";
        public string continueText = "Continue";

        public string deathTitle = "You Died";
        public string deathRewind = "Hold R to undo it!";
        public string deathRewindTouch = "Hold ◄◄ to undo it!";
        public string deathNoJuice = "No rewind juice left...";
        [Tooltip("Button on the death scene that goes to the main menu.")]
        public string rageQuit = "Rage Quit";

        public string rewindHint = "Ouch! Hold R to undo that.";
        public string rewindHintTouch = "Ouch! Hold ◄◄ to undo that.";
        [Tooltip("Under the HUD's rewind button while a scripted scene has locked rewind (lockRewind).")]
        public string rewindLocked = "Not now";

        [Tooltip("The HUD's floor readout on stages with a floor counter; {0} is the floor number.")]
        public string floorFormat = "FLOOR {0}";
    }

    /// <summary>The shared look of every menu: fonts, text styles, buttons, cards, motion.</summary>
    [Serializable]
    public class MenuTheme
    {
        [Tooltip("Font file (.ttf/.otf) for everything. Empty = the TextMesh Pro default.")]
        public Font font;
        [Tooltip("Font for titles. Empty = 'font'.")]
        public Font titleFont;
        [Tooltip("Size of the layout space in pixels; the menu scales to fit the screen.")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Portrait layout space, touch sizes, safe-area margins and the in-level screens' portrait layouts.")]
        public MenuResponsiveStyle responsive = new MenuResponsiveStyle();

        public MenuTextStyle title = new MenuTextStyle { fontSize = 150f, outlineWidth = 0.25f };
        public MenuTextStyle subtitle = new MenuTextStyle { fontSize = 44f };
        public MenuTextStyle heading = new MenuTextStyle { fontSize = 40f };
        public MenuTextStyle body = new MenuTextStyle { fontSize = 28f, bold = false, outlineWidth = 0f };
        public MenuTextStyle caption = new MenuTextStyle { fontSize = 26f };

        public MenuButtonStyle button = new MenuButtonStyle();
        public MenuButtonStyle backButton = new MenuButtonStyle { size = new Vector2(240f, 90f) };
        public MenuButtonStyle smallButton = new MenuButtonStyle { size = new Vector2(260f, 80f) };
        [Tooltip("Level, video and shop cards.")]
        public MenuButtonStyle card = new MenuButtonStyle { size = new Vector2(420f, 330f), hoverScale = 1.05f };
        public MenuSurfaceStyle chip = new MenuSurfaceStyle { shape = MenuShape.Pill };
        public MenuTextStyle chipText = new MenuTextStyle { fontSize = 34f };

        public Color accent = new Color(1f, 0.8f, 0.2f, 1f);
        public Color positive = new Color(0.45f, 1f, 0.5f, 1f);
        public Color muted = new Color(1f, 1f, 1f, 0.55f);
        public Color lockedTint = new Color(0.35f, 0.35f, 0.4f, 1f);

        [Tooltip("How buttons, cards and stickers appear.")]
        public MenuEntrance entrance = new MenuEntrance();
        [Tooltip("How titles appear.")]
        public MenuEntrance titleEntrance = new MenuEntrance { type = MenuEntranceType.Drop, duration = 0.8f, distance = 320f };
        public AudioClip hoverSound;
        public AudioClip clickSound;
        [Range(0f, 1f)] public float soundVolume = 0.6f;

        public MenuHudStyle hud = new MenuHudStyle();
        public MenuTexts texts = new MenuTexts();
        [Tooltip("Results-screen grades, any order.")]
        public List<MenuGrade> grades = new List<MenuGrade>();

        [Tooltip("{0} = health, {1} = max health.")]
        public string healthFormat = "♥ {0}/{1}";
        [Tooltip("{0} = diamonds.")]
        public string pointsFormat = "♦ {0}";
    }

    [Serializable]
    public class MainMenuLabels
    {
        [Tooltip("{0} = the level it continues from.")]
        public string continueGame = "Continue (Level {0})";
        public string newGame = "New Game";
        public string levelSelect = "Level Select";
        public string videos = "Videos";
        public string shop = "Shop";
        public string settings = "Settings";
        public string quit = "Quit";
        [Tooltip("The quit button right after rage-quitting from a death.")]
        public string rageQuitAgain = "Rage Quit Again";
        [Tooltip("Shown when quitting can't close a browser tab.")]
        public string browserQuitHint = "Browsers won't let us close the tab. Do it yourself. We'll wait.";
    }
}
