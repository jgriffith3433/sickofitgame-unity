using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>What falls, rises or floats through a Circle's air.</summary>
    [Doc("A kind of weather. It sets how the bits move and look; the numbers on the weather def tune it.")]
    public enum WeatherKind
    {
        [Doc("Grey flakes sinking slowly and swaying. The Wastes' ash.")] Ash,
        [Doc("Tiny bright sparks flicking upward, flickering. Fresh from the lava.")] Sparks,
        [Doc("Heavier glowing cinders falling and turning, brightening and dimming.")] Cinders,
        [Doc("Glyphs from the walls floating up, pulsing. The Rune Bridges' air.")] Runes,
        [Doc("Grit and pebbles shaken loose, blown sideways while they fall. The Cracked Road's dust.")] Grit,
        [Doc("Soft meadow pollen drifting and swaying.")] Pollen,
        [Doc("Wind streaks or thin debris rushing past (negative parallax reads close).")] Debris,
        [Doc("Soft steam or smoke puffs rising and thinning.")] Steam,
        [Doc("Fine dark dust (obsidian, furnace grit) drifting sideways.")] Dust,
        [Doc("Pale soul wisps drifting and pulsing.")] Soul,
    }

    /// <summary>A timed background beat that fires on its own (geyser, steam vent, birds scattering).</summary>
    [Doc("What a timed ambient event does on screen.")]
    public enum AmbientEventKind
    {
        [Doc("A background lava geyser: a rising column, a glow, optional sound.")] Geyser,
        [Doc("A steam vent puff that blooms and fades.")] SteamVent,
        [Doc("Chunks of debris falling through the view.")] DebrisFall,
        [Doc("A flock of birds scattering off screen.")] BirdsScatter,
        [Doc("A smoke plume rising (Meadow raid, burning houses).")] SmokePlume,
        [Doc("A soft glow pulse on the far ground (heat flash).")] GlowPulse,
    }

    [Doc("How a bit of background life moves.")]
    public enum LifeBehavior
    {
        [Doc("Crosses the sky at a height, bobbing as it flaps.")] Flyer,
        [Doc("Walks along a far ledge at a fixed height.")] Walker,
        [Doc("A far-off chase: a small figure runs and a bigger one runs after it. Two shapes per appearance, the second behind.")] Chase,
    }

    [Doc("The built-in silhouettes for background life (drawn tinted, no art needed).")]
    public enum LifeShape
    {
        [Doc("A flat V of wings, a flier.")] Wing,
        [Doc("A small upright figure, arms out: a soul or a running imp.")] Figure,
        [Doc("A hulking figure with horns: a demon on the march.")] Horned,
        [Doc("A round glow with a tail: a will-o'-the-wisp.")] Wisp,
    }

    [Serializable]
    [Doc("A Circle's air: depth layers (silhouettes at their own parallax), weather (bits of ash, spark, cinder, rune or grit in the air) and background " +
         "life (distant demons, flying things, far-off chases). Drawn on floor stages in the Circle, behind the action. Presentation only: it never " +
         "touches the simulation and needs no rewind. Keep it cheap: weather bits are capped (see Docs/GameplayCookbook.md, \"Weather and life\").")]
    public class AmbienceDef
    {
        [Doc("Silhouettes in rows at their own depth; each row repeats around the camera so it covers the screen however far the stage goes. " +
             "Put a foreground silhouette row on Foreground1 at alpha ≤ 0.6 so chains/grass pass in front without hiding bullets.")]
        public List<DepthLayerDef> depth = new List<DepthLayerDef>();
        [Doc("Bits in the air.")] public List<WeatherDef> weather = new List<WeatherDef>();
        [Doc("Things far away: flyers, walkers, chases.")] public List<AmbientLifeDef> life = new List<AmbientLifeDef>();
        [Doc("Timed background beats (geyser, steam vent, birds scattering) that fire every interval seconds so the screen breathes on its own.")]
        public List<AmbientEventDef> events = new List<AmbientEventDef>();
        [Doc("True = draw weather bits for players on this pack (the Meadow and Floors 1000-997 living air). Circle defaults stay false so older floors keep the quiet air J asked for.")]
        public bool liveWeather;
        [Doc("Heat shimmer strength over lava (0 = off). Soft distortion bars; always off on the phone tier.")]
        public float heatShimmer;

        public bool IsEmpty => (depth == null || depth.Count == 0) && (weather == null || weather.Count == 0)
            && (life == null || life.Count == 0) && (events == null || events.Count == 0);
    }

    [Serializable]
    [Doc("A timed ambient event: a geyser, steam vent, falling debris, birds scattering or a smoke plume. Fires every 'interval' seconds " +
         "(and optionally when 'onSignal' is emitted). Presentation only; never hurts the hero. Keep one every ~10-15 s so each screen breathes.")]
    public class AmbientEventDef
    {
        [Doc("For reports and playtest events.")] public string name = "";
        public AmbientEventKind kind = AmbientEventKind.Geyser;
        [Doc("Seconds between firings: [min, max].")] public Vector2 interval = new Vector2(10f, 15f);
        [Doc("Optional audio.json sound id played when it fires.")] public string sound = "";
        public Color color = new Color(1f, 0.45f, 0.15f, 0.7f);
        [Doc("World size of the effect.")] public float size = 1.6f;
        [Doc("How long the visual lasts.")] public float duration = 1.4f;
        [Doc("How much it follows the camera (far = near 1).")] public float parallax = 0.88f;
        [Doc("Also fire when this signal is emitted (e.g. meadow:raid for birds scattering). Empty = timer only.")]
        public string onSignal = "";
        [Doc("True = never fire on the timer; only on onSignal (Meadow raid smoke/birds).")] public bool signalOnly;
        [Doc("Skip this event on the phone tier (heat flashes and heavy debris).")] public bool phoneOff;
    }

    [Serializable]
    [Doc("A row of silhouettes at one depth, moved by the ParallaxController. 'parallax' is how much it follows the camera: 1 = stuck to the screen, " +
         "0 = fixed in the world, 0.8 = far away and drifting by slowly. Draws behind the player.")]
    public class DepthLayerDef
    {
        [Doc("For reports.")] public string name = "";
        [Doc("Art (optional). Without it 'shape' is drawn.")] public Sprite sprite;
        public PropShape shape = PropShape.Spire;
        public Color color = new Color(0.08f, 0.03f, 0.04f, 0.8f);
        [Doc("Width and height in world units.")] public Vector2 size = new Vector2(1.6f, 3f);
        [Doc("Random size variation as a fraction (0.3 = 70% to 130%).")] public float sizeVariation = 0.3f;
        [Doc("How much it follows the camera, across and up: [x, y].")] public Vector2 parallax = new Vector2(0.8f, 0.8f);
        [Doc("Distance between two silhouettes: [across, up]. Up 0 = a single row; above 0 = rows repeating upward as well (Up-travel stages).")]
        public Vector2 spacing = new Vector2(3f, 0f);
        [Doc("Chance (0-1) each slot has a silhouette.")] public float chance = 0.8f;
        [Doc("World y of the silhouettes' base on screen when the stage starts (the ground is around -2.5).")] public float baseY = -2.5f;
        [Doc("Drawn upside down under baseY (stalactites).")] public bool hanging;
        [Doc("Draw band: Background1 is furthest back; the ParallaxController's rows sit in Background2 by default.")] public SpriteLayer layer = SpriteLayer.Background2;
        [Doc("Order inside the band (negative = further back).")] public int sortingOrder = -50;
    }

    [Serializable]
    [Doc("Bits of one kind of weather. They drift through a box around the camera and wrap, so a few cover the whole screen. 'count' is how many " +
         "are alive at once (on phones half are drawn); the total over a Circle is capped.")]
    public class WeatherDef
    {
        [Doc("For reports.")] public string name = "";
        public WeatherKind kind = WeatherKind.Ash;
        [Doc("Bits alive at once.")] public int count = 30;
        public Color color = new Color(0.7f, 0.65f, 0.6f, 0.6f);
        [Doc("Bit size: [min, max] world units.")] public Vector2 size = new Vector2(0.04f, 0.09f);
        [Doc("Speed along its kind's way (down for ash, cinders, grit, pollen, dust, debris, soul; up for sparks, runes, steam): [min, max] units per second.")] public Vector2 speed = new Vector2(0.3f, 0.7f);
        [Doc("Steady sideways wind, units per second (negative = left).")] public float wind;
        [Doc("How far it sways side to side (units).")] public float sway = 0.3f;
        [Doc("How much it follows the camera (1 = stuck to the screen, 0 = fixed in the world, negative = close and rushing past).")] public float parallax = 0.6f;
        [Doc("Only at or above this floor (counting down: 990 means from floor 990 up). 0 = always.")] public int fromFloor;
        [Doc("Draw in front of the player instead of behind (a few big close bits). Keep alpha low so bullets stay readable.")] public bool inFront;
        [Doc("Optional painted particle sprite (Hawkes Gen). Empty = the built-in shape for 'kind'.")] public Sprite sprite;
    }

    [Serializable]
    [Doc("Something alive far away: a demon on the march, a flying thing, a small soul chased by a big one. Appears every 'interval' seconds, crosses the " +
         "view at its depth and goes. A cosmetic: nothing it does matters to the fight.")]
    public class AmbientLifeDef
    {
        [Doc("For reports.")] public string name = "";
        public LifeBehavior behavior = LifeBehavior.Flyer;
        public LifeShape shape = LifeShape.Wing;
        [Doc("Art (optional). Without it 'shape' is drawn.")] public Sprite sprite;
        public Color color = new Color(0.05f, 0.02f, 0.03f, 0.75f);
        [Doc("Height in world units (a chase's pursuer is 1.6 times this).")] public float size = 0.5f;
        [Doc("Speed in world units per second (the chase's pursuer is a little faster).")] public float speed = 1.6f;
        [Doc("Height on screen as a fraction of the view from the bottom: [low, high].")] public Vector2 height = new Vector2(0.55f, 0.85f);
        [Doc("Seconds between appearances: [min, max].")] public Vector2 interval = new Vector2(8f, 16f);
        [Doc("How much it follows the camera (far away = near 1: it hardly moves with the stage).")] public float parallax = 0.85f;
        [Doc("Only at or above this floor (counting down). 0 = always.")] public int fromFloor;
    }
}
