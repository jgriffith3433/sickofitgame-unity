using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// How hits, shots and Hell's fire feel (game.json "feel"; Docs/Story.md "The feel of a hit"). All presentation: the hit
    /// freeze slows Unity's clock for a few real milliseconds (every simulation tick stays the same), the flash, squash and
    /// kick are drawn by a sprite shader, and the glows are additive sprites. Nothing here is saved or rewound.
    /// </summary>
    [Serializable]
    public class FeelConfig
    {
        [Tooltip("Off = none of it (the flash falls back to the old tint).")]
        public bool enabled = true;
        public HitFreezeFeel freeze = new HitFreezeFeel();
        public HitFlashFeel hit = new HitFlashFeel();
        public FireKickFeel fire = new FireKickFeel();
        public GlowFeel glow = new GlowFeel();
    }

    /// <summary>The hit-freeze: a few real milliseconds where the world stands still on a strong hit.</summary>
    [Serializable]
    public class HitFreezeFeel
    {
        public bool enabled = true;
        [Tooltip("Unity's time scale during a freeze (0.02 = nearly still). The simulation runs the same ticks, only later.")]
        public float timeScale = 0.02f;
        [Tooltip("A hit dealing at least this much damage is strong (the fists' hook, the whip's last lash, a crit).")]
        public int strongDamage = 2;
        [Tooltip("On a boss a hit must deal this much to be strong (every upgraded shot would freeze it otherwise).")]
        public int bossStrongDamage = 6;
        [Tooltip("Real seconds a strong hit freezes.")]
        public float strongSeconds = 0.06f;
        [Tooltip("Killing an enemy with at least this much health freezes (an imp has 2).")]
        public int killMinHealth = 2;
        public float killSeconds = 0.045f;
        [Tooltip("A boss's killing blow.")]
        public float bossKillSeconds = 0.22f;
        [Tooltip("An explosion (explodeNear) going off.")]
        public float explosionSeconds = 0.07f;
        [Tooltip("The hero getting hurt.")]
        public float heroHurtSeconds = 0.08f;
        [Tooltip("At least this long (real seconds) between two freezes, so a crowd never stutters.")]
        public float cooldownSeconds = 0.3f;
    }

    /// <summary>An enemy hit: a white flash and a squash, pushed away from the blow.</summary>
    [Serializable]
    public class HitFlashFeel
    {
        public bool enabled = true;
        public Color flashColor = Color.white;
        [Tooltip("Real seconds the flash lasts (full for the first half, then it fades).")]
        public float flashSeconds = 0.09f;
        [Tooltip("How far the flash goes to its color (1 = a solid silhouette).")]
        public float flashStrength = 0.85f;
        [Tooltip("Squash on a hit: 0.2 = 20% wider and shorter, then it springs back.")]
        public float squash = 0.2f;
        [Tooltip("A strong hit squashes this much more.")]
        public float strongSquashScale = 1.6f;
        [Tooltip("Bosses squash this much of it (they are big).")]
        public float bossSquashScale = 0.35f;
        public float squashSeconds = 0.14f;
        [Tooltip("World units the sprite is pushed away from the blow (the body stays where the simulation has it).")]
        public float recoilDistance = 0.08f;
    }

    /// <summary>The hero's recoil on every shot: the sprite kicks back from the aim and squashes a little.</summary>
    [Serializable]
    public class FireKickFeel
    {
        public bool enabled = true;
        [Tooltip("World units the hero's sprite kicks back from his aim.")]
        public float kickDistance = 0.06f;
        [Tooltip("Squash along the kick (0.06 = 6%).")]
        public float squash = 0.06f;
        public float seconds = 0.08f;
        [Tooltip("A melee swing kicks this much of it (forward, not back).")]
        public float meleeScale = 0.5f;
    }

    public enum GlowShape
    {
        /// <summary>A soft round halo around the body.</summary>
        Round,
        /// <summary>A band of light along the top edge of the body (a lava sea lighting what is above it).</summary>
        Top,
    }

    /// <summary>Which entities glow and how. The first rule that matches an entity wins.</summary>
    [Serializable]
    public class GlowRule
    {
        public string name = "";
        [Tooltip("Matches when the entity id contains one of these (any case).")]
        public List<string> ids = new List<string>();
        [Tooltip("Matches when the entity has one of these tags.")]
        public List<string> tags = new List<string>();
        [Tooltip("Matches when one of its animator's sprite sheets is named with one of these (\"Fireball\").")]
        public List<string> sheets = new List<string>();
        [Tooltip("Ids containing one of these never match this rule.")]
        public List<string> exceptIds = new List<string>();
        public GlowShape shape = GlowShape.Round;
        public Color color = new Color(1f, 0.55f, 0.2f, 1f);
        [Tooltip("Take the entity's own fxColor (or its tint) instead of the rule's color, when it has one.")]
        public bool entityColor = true;
        [Tooltip("Round: the halo's size as a multiple of the body. Top: the band's height in world units.")]
        public float size = 2.4f;
        [Tooltip("World units added to a round halo's size.")]
        public float padding = 0.2f;
        public float minSize = 0.4f;
        public float maxSize = 30f;
        public float alpha = 0.55f;
        [Tooltip("0 = steady, 0.3 = flickers 30% like fire.")]
        public float flicker = 0.25f;
        public float flickerSpeed = 9f;
        [Tooltip("Draw order against the entity's own (-1 = just under it).")]
        public int orderOffset = -1;
    }

    /// <summary>Additive glows: fire shots, lava, explosions. Pooled sprites sharing one material, capped.</summary>
    [Serializable]
    public class GlowFeel
    {
        public bool enabled = true;
        [Tooltip("Most glows drawn at once (followers and bursts together); the rest wait for a free one.")]
        public int maxGlows = 48;
        [Tooltip("Multiplies every glow's brightness.")]
        public float intensity = 1f;
        [Tooltip("The hero's own shots (the launcher's rockets) glow too.")]
        public bool heroShots = true;
        public Color heroShotColor = new Color(1f, 0.75f, 0.35f, 1f);
        public float heroShotSize = 0.9f;
        public float heroShotAlpha = 0.45f;
        [Tooltip("An enemy's death pop leaves a glow of its fxColor this many times its size.")]
        public float deathGlowScale = 2.6f;
        public float deathGlowAlpha = 0.6f;
        public float deathGlowSeconds = 0.3f;
        [Tooltip("An explosion's glow, as a multiple of its blast radius.")]
        public float explosionGlowScale = 3.4f;
        public float explosionGlowAlpha = 0.85f;
        public float explosionGlowSeconds = 0.45f;
        [Tooltip("The muzzle flash's glow (world units; 0 = none).")]
        public float muzzleGlowSize = 0.7f;
        public float muzzleGlowAlpha = 0.35f;
        public List<GlowRule> rules = new List<GlowRule>();
    }
}
