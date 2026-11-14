using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>One expression of a speaker's portrait: the closed-mouth painting and the same one with the mouth open.</summary>
    [Serializable]
    public class PortraitFrame
    {
        [Tooltip("neutral, smug (angry), laughing or shocked.")]
        public string mood = "neutral";
        [Tooltip("The painting, mouth closed (keyed, transparent background).")]
        public Sprite closed;
        [Tooltip("The same painting with the mouth open: the flap while the voice is loud.")]
        public Sprite open;
    }

    /// <summary>One speaking character's portrait set.</summary>
    [Serializable]
    public class PortraitCharacter
    {
        [Tooltip("hero, overlord, yooba, rook or wick.")]
        public string id = "";
        [Tooltip("The audio.json voice whose lines show this portrait (player, overlord, yooba, rook, wick).")]
        public string voice = "";
        [Tooltip("Shown on the name chip.")]
        public string displayName = "";
        [Tooltip("The card's rim, the chip and the stand-in's body.")]
        public Color color = new Color(1f, 0.54f, 0.12f, 1f);
        [Tooltip("The expressions painted so far. A mood with no painting falls back to neutral; none at all draws the stand-in bust.")]
        public List<PortraitFrame> frames = new List<PortraitFrame>();
    }

    /// <summary>
    /// The painted speaker portraits (Docs/Story.md "Portraits", ui/portraits.json): whenever a voiced line of one of these
    /// characters plays, its portrait slides in at a screen edge, shows the line's mood and flaps its mouth with the clip's
    /// loudness. Loaded with the late core scope. Presentation only.
    /// </summary>
    [Serializable]
    public class PortraitsConfig
    {
        public bool enabled = true;
        [Tooltip("Seconds the card takes to slide in (the brief says 0.15 at the most).")]
        public float slideSeconds = 0.12f;
        [Tooltip("Seconds the card stays after its line ends before it slides away.")]
        public float holdSeconds = 0.6f;
        [Tooltip("Seconds the card takes to slide away.")]
        public float slideOutSeconds = 0.15f;
        [Tooltip("The card's height as a fraction of the screen's short side.")]
        public float heightFraction = 0.3f;
        [Tooltip("Smallest and largest card height, in screen pixels (the phone check wants 72+).")]
        public float minHeight = 96f;
        public float maxHeight = 240f;
        [Tooltip("Card centre height on screen (0 bottom, 1 top), landscape and portrait: above the touch stick, under the top HUD.")]
        public float heightLandscape = 0.62f;
        public float heightPortrait = 0.70f;
        public List<PortraitCharacter> characters = new List<PortraitCharacter>();
    }
}
