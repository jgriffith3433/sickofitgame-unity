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

        public string rewindHint = "Ouch! Hold R to undo that.";
        public string rewindHintTouch = "Ouch! Hold ◄◄ to undo that.";
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
    }
}
