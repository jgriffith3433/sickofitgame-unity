using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>A step of the style meter: reaching it pays once (Docs/Story.md, "Style").</summary>
    [Serializable]
    public class StyleTier
    {
        public string name = "";
        [Tooltip("Meter value (0-100) that reaches this tier.")]
        public float at = 25f;
        public int diamonds;
        [Tooltip("Rewind juice paid, in seconds.")]
        public float juice;
        public Color color = Color.white;
    }

    /// <summary>
    /// A line Hell's cabinet says: <c>on</c> is the moment (streak5, streak10, streak20, tier3, perfect, rankS); every sound
    /// in <c>sounds</c> plays, and the first one's words (its audio.json prompt) are the subtitle.
    /// </summary>
    [Serializable]
    public class StyleLine
    {
        public string on = "";
        public List<string> sounds = new List<string>();
        public Color color = new Color(1f, 0.85f, 0.25f);
    }

    [Serializable]
    public class StyleRank
    {
        public string letter = "C";
        public int minPoints;
    }

    /// <summary>Grazes, the style meter and the floor rank (game.json "style").</summary>
    [Serializable]
    public class StyleConfig
    {
        public bool enabled = true;
        [Tooltip("A hostile shot passing this close (world units between the bodies) without hitting is a graze.")]
        public float grazeDistance = 0.35f;
        [Tooltip("Sound id played on a graze; its pitch climbs with the streak.")]
        public string grazeSound = "graze_tick";
        public float meterMax = 100f;
        public float grazeGain = 12f;
        [Tooltip("Extra meter per step of the streak (the first graze of a streak has none).")]
        public float streakBonus = 2f;
        public int streakBonusCap = 8;
        [Tooltip("A graze this soon after the last one carries the streak on (seconds).")]
        public float streakWindow = 2.5f;
        [Tooltip("The meter starts to drain this long after its last gain (seconds).")]
        public float decayDelay = 1.5f;
        public float decayPerSecond = 14f;
        [Tooltip("Never say two lines closer than this (seconds).")]
        public float lineGapSeconds = 6f;
        public List<StyleTier> tiers = new List<StyleTier>
        {
            new StyleTier { name = "COOL", at = 25f, diamonds = 0, juice = 0.5f, color = new Color(0.3f, 0.85f, 1f) },
            new StyleTier { name = "HOT", at = 55f, diamonds = 1, juice = 1f, color = new Color(1f, 0.6f, 0.3f) },
            new StyleTier { name = "SICK OF IT", at = 85f, diamonds = 2, juice = 1.5f, color = new Color(1f, 0.85f, 0.25f) },
        };
        public List<StyleLine> lines = new List<StyleLine>
        {
            new StyleLine { on = "streak5", sounds = new List<string> { "vo_style_streak5", "vo_crowd_gasp" } },
            new StyleLine { on = "streak10", sounds = new List<string> { "vo_style_streak10", "vo_crowd_roar" } },
            new StyleLine { on = "streak20", sounds = new List<string> { "vo_style_streak20", "vo_crowd_roar" } },
            new StyleLine { on = "tier3", sounds = new List<string> { "vo_style_sick" } },
            new StyleLine { on = "perfect", sounds = new List<string> { "vo_style_perfect", "vo_crowd_cheer" } },
            new StyleLine { on = "rankS", sounds = new List<string> { "vo_style_ranks" } },
        };

        [Header("Floor rank")]
        [Tooltip("Par time = the stage's expected length x this.")]
        public float parFactor = 1f;
        public int parPoints = 30;
        public int noHitPoints = 30;
        public int grazePoints = 20;
        [Tooltip("Grazes that earn all the graze points.")]
        public int grazeGoal = 20;
        public int rewindPoints = 20;
        [Tooltip("Rewinds that still earn half the rewind points.")]
        public int rewindsForHalf = 2;
        public List<StyleRank> ranks = new List<StyleRank>
        {
            new StyleRank { letter = "S", minPoints = 90 },
            new StyleRank { letter = "A", minPoints = 70 },
            new StyleRank { letter = "B", minPoints = 45 },
            new StyleRank { letter = "C", minPoints = 0 },
        };
    }
}
