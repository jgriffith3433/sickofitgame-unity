using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Per-floor colour grades and local lights (game.json "lighting"; Docs/lighting.md). Built-in renderer: a camera
    /// fullscreen grade pass plus additive light sprites over a multiply darkness layer. Presentation only.
    /// </summary>
    [Serializable]
    public class LightingConfig
    {
        public bool enabled = true;
        [Tooltip("How far a lava or torch light reaches platforms (world units).")]
        public float lightReach = 2f;
        [Tooltip("Phone: bloom at half resolution, no grain, at most this many lights.")]
        public LightingQualityTier quality = new LightingQualityTier();
        public List<LevelLightingProfile> profiles = new List<LevelLightingProfile>();
    }

    [Serializable]
    public class LightingQualityTier
    {
        public LightingQuality desktop = new LightingQuality { bloomHalfRes = false, grain = true, maxLights = 24 };
        public LightingQuality phone = new LightingQuality { bloomHalfRes = true, grain = false, maxLights = 8 };
        [Tooltip("Short side at or under this many CSS/screen pixels counts as phone when touch is primary.")]
        public float phoneShortSide = 500f;
    }

    [Serializable]
    public class LightingQuality
    {
        public bool bloomHalfRes = false;
        public bool grain = true;
        public int maxLights = 24;
    }

    /// <summary>One floor's mood of light (Docs/lighting.md grades).</summary>
    [Serializable]
    public class LevelLightingProfile
    {
        public string id = "";
        [Tooltip("Level positions in game.json (1 = Meadow, 2 = Floor 1000, ...).")]
        public List<int> levels = new List<int>();
        [Tooltip("Dominant grade colour multiply/tint.")]
        public Color grade = new Color(1f, 0.85f, 0.65f, 1f);
        [Tooltip("0 = none, 1 = full grade wash.")]
        public float gradeStrength = 0.35f;
        [Tooltip("Meadow only: grade after the sky opens (warm dusk → red).")]
        public Color raidGrade = new Color(0.95f, 0.35f, 0.22f, 1f);
        public float raidGradeStrength = 0.45f;
        [Tooltip("Multiply darkness over the playfield (local lights punch holes in it).")]
        public Color darkness = new Color(0.12f, 0.08f, 0.16f, 1f);
        public float darknessAlpha = 0.35f;
        public float bloomThreshold = 1f;
        public float bloomIntensity = 0.55f;
        public float vignette = 0.35f;
        public float grain = 0.04f;
        public Color rim = new Color(1f, 0.82f, 0.55f, 0.7f);
        public Color lavaLight = new Color(1f, 0.45f, 0.15f, 1f);
        public Color torchLight = new Color(1f, 0.7f, 0.35f, 1f);
        public Color bulletLight = new Color(1f, 0.85f, 0.45f, 1f);
        public float lavaFlicker = 0.22f;
        public float torchFlicker = 0.3f;
    }
}
