using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>One of Rook's Shaft portrait loops (idle, talk, scratch).</summary>
    [Serializable]
    public class RookClip
    {
        [Tooltip("idle, talk or scratch.")]
        public string name = "idle";
        [Tooltip("The sheet (keyed). Empty = the amber placeholder figure.")]
        public Sprite sheet;
        public int columns = 4;
        public int rows = 2;
        [Tooltip("Frames used from the first cell. 0 = every cell.")]
        public int frames = 0;
        public float fps = 10f;
    }

    /// <summary>A Shaft line Rook says, keyed to a rewind-count band.</summary>
    [Serializable]
    public class RookLine
    {
        [Tooltip("Lowest rewind count this line may play for (inclusive).")]
        public int minRewinds;
        [Tooltip("Highest rewind count this line may play for (inclusive). -1 = no upper bound.")]
        public int maxRewinds = -1;
        [Tooltip("audio.json sound id (voice rook).")]
        public string sound = "";
        [Tooltip("The caption (about 40 characters).")]
        public string text = "";
        [Tooltip("Portrait loop while he says it: talk or scratch.")]
        public string clip = "talk";
    }

    /// <summary>
    /// Rook on the Shaft (Docs/Story.md "The Climbers"): chalk tally wall of the hero's real rewind count and one
    /// voiced line per visit by band (ui/rook.json). Presentation only.
    /// </summary>
    [Serializable]
    public class RookConfig
    {
        [Tooltip("Off = no Rook on the Shaft.")]
        public bool enabled = true;
        public string displayName = "ROOK";
        public string speaker = "Rook";
        [Tooltip("Seconds after his line before the Shaft finishes (Continue already up).")]
        public float pause = 0.25f;
        [Tooltip("A line without a clip yet is shown this long per character (at least 1.4 s).")]
        public float secondsPerCharacter = 0.055f;
        public Color frame = new Color(0.45f, 0.22f, 0.08f, 1f);
        public Color body = new Color(0.95f, 0.66f, 0.23f, 1f);
        public Color chalk = new Color(0.92f, 0.9f, 0.82f, 0.95f);
        public Color wall = new Color(0.18f, 0.12f, 0.1f, 0.95f);
        [Tooltip("audio.json id played when a new mark scratches in.")]
        public string scratchSound = "sfx_rook_chalk";
        [Tooltip("Portrait loops (idle, talk, scratch).")]
        public List<RookClip> clips = new List<RookClip>();
        [Tooltip("Sixteen lines across the four rewind bands.")]
        public List<RookLine> lines = new List<RookLine>();

        [Header("Placement")]
        [Tooltip("His portrait + tally wall on the Shaft (landscape). At least 64 px tall on a phone.")]
        public MenuPlacement shaftPortrait = MenuPlacement.At(1f, 0f, -170f, 210f, 280f, 280f);
        public MenuPlacement shaftPortraitPortrait = MenuPlacement.At(0f, 1f, 150f, -840f, 240f, 240f);
        public MenuPlacement subtitle = MenuPlacement.At(0.72f, 0.2f, 0f, 0f, 1000f, 90f);
        public MenuPlacement subtitlePortrait = MenuPlacement.At(0.5f, 0f, 0f, 700f, 1000f, 100f);
    }
}
