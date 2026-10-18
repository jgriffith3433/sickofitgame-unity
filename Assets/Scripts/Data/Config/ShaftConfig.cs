using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// One Circle of Hell on the Shaft: a band of floors in its biome's colour, with its Warden's mark at the top
    /// (Docs/Story.md, "The Circles"). When content/circles has the circle, its displayName and floors are used.
    /// </summary>
    [Serializable]
    public class ShaftBand
    {
        [Tooltip("The Circle's id in content/circles (empty if it isn't built yet). Its displayName, firstFloor and lastFloor win over the fields below.")]
        public string circle = "";
        [Tooltip("Roman numeral shown on the band: X at the bottom, I at the top.")]
        public string numeral = "X";
        [Tooltip("Name shown on the band when content/circles has no such circle.")]
        public string name = "";
        [Tooltip("Bottom floor of the band (e.g. 1000).")]
        public int firstFloor = 1000;
        [Tooltip("Top floor of the band, where its Warden sits (e.g. 901).")]
        public int lastFloor = 901;
        [Tooltip("The biome's colour (placeholder painting until the Shaft art replaces it).")]
        public Color color = new Color(0.5f, 0.15f, 0.08f, 1f);
        [Tooltip("The Warden's name, shown once the hero has reached the top of this Circle; '???' until then.")]
        public string warden = "";
        [Tooltip("The Circle's painted band (Hawkes Gen, floor-backdrop preset; its top and bottom edges stack). It covers the band, cropped to fit. Empty = the colour above.")]
        public Sprite art;
        [Tooltip("The Warden's figure on a transparent background: drawn as a dark silhouette until met, in full colour after. Empty = a placeholder shape.")]
        public Sprite wardenArt;
        [Tooltip("audio.json id of the announcer calling this Circle when it wakes up: the climb that lights the Waystone at the top of the Circle below (Docs/Story.md, \"The Magma King's fight\"). Empty = it wakes without a voice.")]
        public string wakeVoice = "";
    }

    /// <summary>
    /// The Shaft, Hell's map (Docs/Story.md, "The Shaft"): the plunge from the Meadow to FLOOR 1,000 after the plunge
    /// video, the hero's marker climbing after every level with a floor range, and dropping back to the Waystone when a
    /// death ends a run (Waystones: the bottom floor and each band's lastFloor below topFloor). Presentation only: it reads
    /// progress, never changes the game. Words are in ui/theme.json texts (shaft*).
    /// </summary>
    [Serializable]
    public class ShaftConfig
    {
        [Tooltip("Off = the flow goes straight on (no Shaft).")]
        public bool enabled = true;
        [Tooltip("The video after which the Shaft plunges from the Meadow to the bottom (Video2: the fire tunnel).")]
        public int plungeAfterVideo = 2;
        [Tooltip("Where the plunge lands: the bottom of Hell.")]
        public int bottomFloor = 1000;
        [Tooltip("The top of Hell, the Overlord's floor ('floors left' counts to it).")]
        public int topFloor = 1;

        [Header("Timing (real seconds)")]
        [Tooltip("How long the fall from the Meadow to the bottom takes.")]
        public float plungeSeconds = 3.4f;
        [Tooltip("Seconds per floor while the marker climbs after a level.")]
        public float climbSecondsPerFloor = 0.16f;
        [Tooltip("The climb never takes longer than this.")]
        public float climbMaxSeconds = 3f;
        [Tooltip("Pause before the marker starts to climb.")]
        public float climbDelay = 0.6f;
        [Tooltip("Seconds per floor while the marker drops back to the Waystone after a run ends.")]
        public float dropSecondsPerFloor = 0.03f;
        [Tooltip("The drop never takes longer than this.")]
        public float dropMaxSeconds = 2.4f;
        [Tooltip("How hard the landing at the bottom shakes the screen, in pixels (Screen Shake setting off = none).")]
        public float slamShake = 34f;

        [Header("Look")]
        [Tooltip("Pixels per floor inside the shaft (10 floors = one level tick).")]
        public float pixelsPerFloor = 7f;
        [Tooltip("Height of the Meadow at the top (FLOOR 0).")]
        public float meadowHeight = 420f;
        public Color backdrop = new Color(0.05f, 0.025f, 0.04f, 1f);
        public Color wall = new Color(0.09f, 0.05f, 0.06f, 1f);
        public Color sky = new Color(0.55f, 0.78f, 0.95f, 1f);
        public Color grass = new Color(0.36f, 0.66f, 0.25f, 1f);
        public Color marker = new Color(1f, 0.85f, 0.3f, 1f);
        public Color crack = new Color(0f, 0f, 0f, 0.55f);
        [Tooltip("The Meadow at the top, with the burned hole and its daylight (cropped to fit). Empty = painted sky and grass.")]
        public Sprite meadowArt;
        [Tooltip("The hero's marker: his helmet and rocket launcher. Empty = a dot.")]
        public Sprite markerArt;
        [Tooltip("Height of the hero's marker art, in pixels.")]
        public float markerSize = 64f;
        [Tooltip("Height of a Warden's figure, in pixels.")]
        public float wardenSize = 150f;
        [Tooltip("A Warden not met yet: its figure tinted to this (a dark silhouette).")]
        public Color wardenUnmet = new Color(0.035f, 0.012f, 0.02f, 0.97f);
        [Tooltip("The shaft's side walls when the bands are painted (see-through, so the art shows behind them).")]
        public Color wallOverArt = new Color(0.05f, 0.025f, 0.03f, 0.55f);
        [Tooltip("A Waystone the hero has reached: its stone glows this colour.")]
        public Color waystoneLit = new Color(0.62f, 1f, 0.9f, 1f);
        [Tooltip("A Waystone not reached yet: dark stone.")]
        public Color waystoneDark = new Color(0.3f, 0.27f, 0.27f, 0.95f);
        [Tooltip("Floors where a crack in the wall or a dotted gap hints at a shortcut (not explained yet).")]
        public List<int> shortcutHints = new List<int>();
        [Tooltip("A shortcut the hero has taken: the dotted jump up the wall from the floor he took it on to the floor it let him out on.")]
        public Color shortcutPath = new Color(1f, 0.72f, 0.3f, 0.95f);
        [Tooltip("Seconds the marker takes to jump a shortcut.")]
        public float shortcutSeconds = 1.6f;

        [Header("Sounds (ids in audio.json)")]
        public string fallSound = "shaft_fall";
        public string slamSound = "shaft_slam";
        public string tickSound = "shaft_tick";
        [Tooltip("The whoosh as the marker jumps a shortcut.")]
        public string shortcutSound = "";
        [Tooltip("Music on the first plunge: a falling riser whose slam lands as the marker hits the bottom (it starts with fallSound, " +
                 "plungeSeconds before the slam).")]
        public string plungeMusic = "";
        [Tooltip("Music on every climb after a level: a short rising sting as the marker goes up.")]
        public string climbMusic = "";
        [Tooltip("The announcer at the bottom, after the plunge.")]
        public string welcomeVoice = "vo_shaft_welcome";
        [Tooltip("The hero's answer.")]
        public string heroVoice = "vo_shaft_great";
        [Tooltip("The announcer when Hell ends the first run (the drop back to the Waystone): the rule, in story terms.")]
        public string firstDropVoice = "vo_shaft_first_drop";
        [Tooltip("The hero's answer to it.")]
        public string firstDropHeroVoice = "vo_shaft_first_drop_hero";
        [Tooltip("The announcer on every drop after the first.")]
        public string dropVoice = "vo_shaft_drop";

        [Header("Circles, bottom (X) to top (I)")]
        public List<ShaftBand> bands = new List<ShaftBand>();

        [Header("Placement")]
        [Tooltip("The shaft window (landscape).")]
        public MenuPlacement shaft = MenuPlacement.At(0.3f, 0.5f, 0f, 0f, 560f, 900f);
        public MenuPlacement shaftPortrait = MenuPlacement.At(0.5f, 1f, 0f, -510f, 1000f, 900f);
        [Tooltip("The big FLOOR counter.")]
        public MenuPlacement counter = MenuPlacement.At(0.72f, 0.74f, 0f, 0f, 900f, 170f);
        public MenuPlacement counterPortrait = MenuPlacement.At(0.5f, 0f, 0f, 640f, 1000f, 150f);
        [Tooltip("The Circle the hero is in.")]
        public MenuPlacement circleLine = MenuPlacement.At(0.72f, 0.62f, 0f, 0f, 900f, 70f);
        public MenuPlacement circleLinePortrait = MenuPlacement.At(0.5f, 0f, 0f, 530f, 1000f, 60f);
        [Tooltip("Floors left to the top.")]
        public MenuPlacement floorsLeft = MenuPlacement.At(0.72f, 0.5f, 0f, 0f, 900f, 70f);
        public MenuPlacement floorsLeftPortrait = MenuPlacement.At(0.5f, 0f, 0f, 460f, 1000f, 60f);
        [Tooltip("The next Warden and how far up.")]
        public MenuPlacement nextWarden = MenuPlacement.At(0.72f, 0.41f, 0f, 0f, 900f, 70f);
        public MenuPlacement nextWardenPortrait = MenuPlacement.At(0.5f, 0f, 0f, 390f, 1000f, 60f);
        [Tooltip("Spoken lines as subtitles.")]
        public MenuPlacement subtitle = MenuPlacement.At(0.72f, 0.27f, 0f, 0f, 1000f, 110f);
        public MenuPlacement subtitlePortrait = MenuPlacement.At(0.5f, 0f, 0f, 760f, 1000f, 110f);
        public MenuPlacement continueButton = MenuPlacement.At(0.72f, 0.12f, 0f, 0f, 460f, 110f);
        public MenuPlacement continueButtonPortrait = MenuPlacement.At(0.5f, 0f, 0f, 220f, 520f, 124f);
        [Tooltip("Visit Yooba, next to Continue once Yooba has met the hero: opens his shop over the Shaft (size = the button's).")]
        public MenuPlacement yoobaButton = MenuPlacement.At(0.8f, 0.12f, -460f, 0f, 380f, 84f);
        public MenuPlacement yoobaButtonPortrait = MenuPlacement.At(0.5f, 0f, -250f, 220f, 440f, 124f);
        [Tooltip("Continue when Visit Yooba stands beside it (moved over to make room).")]
        public MenuPlacement continueBesideYooba = MenuPlacement.At(0.8f, 0.12f, 20f, 0f, 520f, 84f);
        public MenuPlacement continueBesideYoobaPortrait = MenuPlacement.At(0.5f, 0f, 260f, 220f, 480f, 124f);
    }
}
