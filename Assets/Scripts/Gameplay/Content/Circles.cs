using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>The string names a Circle of Hell in content/circles.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class CircleRefAttribute : Attribute { }

    /// <summary>content/circles/{id}.json</summary>
    [Serializable]
    [Doc("A Circle of Hell: a run of floors with one biome, one family of enemies and one Warden (see Docs/Story.md). " +
         "Travel stages in the circle take their ground, platforms, passing props, backdrop and generated floors (generateFloors) from it. " +
         "File: content/circles/{id}.json.")]
    public class CircleDef
    {
        [NonSerialized] public string id;

        [Doc("Name shown to the player, e.g. \"The Ember Wastes\".")] public string displayName = "";
        [Doc("Bottom floor of the circle (where the climb through it starts), e.g. 1000.")] public int firstFloor = 1000;
        [Doc("Top floor (the Warden's), e.g. 901. Difficulty goes from 'easiest' at firstFloor to 'hardest' here.")] public int lastFloor = 901;
        [Doc("Backdrop for stages in this circle whose level file has no backgroundLayers (same fields as a level's backgroundLayers).")]
        public List<BackgroundLayerConfig> backgroundLayers = new List<BackgroundLayerConfig>();
        public TrackDef track = new TrackDef();
        [Doc("Scenery that passes by on travel stages: spires, rocks, chains. Drawn only; never collides.")] public List<PropDef> props = new List<PropDef>();
        [Doc("Who the generator sends, picked by weight. Use fromFloor to introduce a new enemy partway up (one new enemy at a time).")]
        public List<CircleEnemyDef> enemies = new List<CircleEnemyDef>();
        [Doc("Hazards the generator can use instead of an enemy push. Each is telegraphed first.")] public List<CircleHazardDef> hazards = new List<CircleHazardDef>();
        [Doc("Generator settings at the circle's first (bottom) floor.")] public DifficultyDef easiest = new DifficultyDef();
        [Doc("Generator settings at its last (top) floor; floors in between are interpolated.")] public DifficultyDef hardest = new DifficultyDef();
        [Doc("The Circle's push music: on floor stages here it takes over from the stage's music while a generated push (enemies or a " +
             "hazard) is on, and the stage's music comes back a moment after it ends. Same tempo as the stages' music, so the beat carries " +
             "over. null = the stage's music plays throughout.")]
        public AudioClip pushMusic;
        [Doc("Volume of pushMusic (0-1, times the player's Music setting).")] public float pushMusicVolume = 1f;
        [Doc("The Circle's intensity layer (Docs/Story.md, \"Music\"): a stem the same length and tempo as the stage's music, played in step with it and faded in as enemies and " +
             "bullets crowd the screen, out again as it empties. Only over the stage's own music: a push track or a script's playMusic (a Warden's theme) is intense already. null = no layer.")]
        public AudioClip intensityMusic;
        [Doc("Volume of intensityMusic at full intensity (0-1, times the player's Music setting).")] public float intensityMusicVolume = 0.8f;
        [Doc("What the layer counts: each live enemy is worth this much.")] public float intensityEnemyWeight = 1f;
        [Doc("What the layer counts: each live hostile shot (a projectile) is worth this much.")] public float intensityBulletWeight = 0.35f;
        [Doc("Crowding (enemies x weight + shots x weight) at which the layer starts to fade in.")] public float intensityLow = 3f;
        [Doc("Crowding at which the layer is fully in.")] public float intensityHigh = 14f;
        [Doc("Lines that play when the floor counter reaches a floor in this circle (any stage), e.g. 999: \"Only nine hundred ninety-nine to go.\"")]
        public List<MilestoneDef> milestones = new List<MilestoneDef>();
        [Doc("Floors with a path in this Circle: the world below them (the lava sea, parallax layers, embers, shimmer).")]
        public SkyDef sky = new SkyDef();
        [Doc("The Circle's air: depth layers at their own parallax, weather (ash, sparks, cinders, runes, grit) and distant life (demons, flyers, far-off chases). " +
             "Drawn on every floor stage in the Circle. Empty = clean air.")]
        public AmbienceDef ambience = new AmbienceDef();
        [Doc("Colour of the rim drawn round every hostile shot so it reads against this Circle's backgrounds (alpha 0 = a pale hot rim). Pale for dark " +
             "Circles, ink for bright ones. Docs/Story.md, \"Readability is fairness\".")]
        public Color shotRim;
        [Doc("Pieces of floor the generator fills a path's 'generate' sections with (a climb of sinking stones, a raft ride, a chase).")]
        public List<PathChunkDef> chunks = new List<PathChunkDef>();

        /// <summary>0 at firstFloor, 1 at lastFloor.</summary>
        public float Progress(int floor)
        {
            if (firstFloor == lastFloor)
                return 0f;
            return Mathf.Clamp01((float)(firstFloor - floor) / (firstFloor - lastFloor));
        }

        public DifficultyDef DifficultyAt(int floor) => DifficultyDef.Lerp(easiest, hardest, Progress(floor));
    }

    [Serializable]
    [Doc("The ground and platforms of a travel stage. The ground runs the whole way; platforms are streamed in ahead and removed behind, " +
         "laid out from the level seed (same seed, same platforms).")]
    public class TrackDef
    {
        [Doc("Height of the top of the ground (world units).")] public float groundY = -2.5f;
        public Color groundColor = new Color(0.12f, 0.06f, 0.05f, 1f);
        [Doc("Thin strip along the top of the ground (alpha 0 = none).")] public Color groundEdgeColor = new Color(1f, 0.45f, 0.15f, 1f);
        [Doc("Length of one streamed segment along the travel direction. Up: the height between ledges.")] public float segmentLength = 6f;
        [Doc("Chance (0-1) that a segment has a platform.")] public float platformChance = 0.5f;
        [Doc("Platform height above the ground: [min, max].")] public Vector2 platformHeight = new Vector2(1.1f, 1.8f);
        [Doc("Platform width: [min, max].")] public Vector2 platformWidth = new Vector2(1.4f, 2.6f);
        [Doc("Platform tint (alpha 0 = the game's platform color).")] public Color platformColor = new Color(0f, 0f, 0f, 0f);
        [Doc("Distance from the start kept flat, so the opening has room to move.")] public float clearStart = 8f;
    }

    [Doc("A built-in silhouette for props without a sprite.")]
    public enum PropShape
    {
        [Doc("A tall pointed rock.")] Spire,
        [Doc("A low lumpy boulder.")] Rock,
        [Doc("A plain rectangle.")] Block,
        [Doc("A flat glowing patch on the ground (lava cracks, puddles).")] Glow,
    }

    [Serializable]
    [Doc("A kind of scenery repeated along a travel stage. 'parallax' sets its depth: 0 moves with the ground, 0.8 is far away and drifts by slowly, " +
         "negative is close to the camera and rushes past in front.")]
    public class PropDef
    {
        [Doc("For reports.")] public string name = "";
        [Doc("Art (optional). Without it the prop is drawn as 'shape' in 'color'.")] public Sprite sprite;
        public PropShape shape = PropShape.Spire;
        public Color color = new Color(0.08f, 0.04f, 0.05f, 1f);
        [Doc("Width and height in world units.")] public Vector2 size = new Vector2(1f, 2.5f);
        [Doc("Random size variation as a fraction (0.3 = 70% to 130%).")] public float sizeVariation = 0.3f;
        [Doc("Where its base sits across the travel direction: the world y for Right travel, the x offset from the camera for Up travel.")] public float across = -2.5f;
        [Doc("Depth: 0 = with the ground, 1 = stuck to the sky, negative = in front of the player.")] public float parallax = 0.5f;
        [Doc("Average distance between two of them.")] public float spacing = 4f;
        [Doc("Chance (0-1) each slot gets one.")] public float chance = 0.8f;
        [Doc("Draw band: Background2 behind the player, Foreground1 in front.")] public SpriteLayer layer = SpriteLayer.Background2;
        public int sortingOrder;
    }

    [Serializable]
    [Doc("An enemy the floor generator can send.")]
    public class CircleEnemyDef
    {
        [Required, EntityRef] public string entity = "";
        [Doc("How often it is picked relative to the others.")] public float weight = 1f;
        [Doc("It only appears once the floor counter is at or above this floor (counting down: 990 means from floor 990 up). 0 = from the start.")]
        public int fromFloor;
        [Doc("Spawn area center, measured from the camera (x is ahead of it on Right travel).")] public Vector2 areaCenter = new Vector2(3.6f, -1.2f);
        [Doc("Spawn area size; each spawn picks a point inside it.")] public Vector2 areaSize = new Vector2(1.2f, 2f);
    }

    [Serializable]
    [Doc("A hazard the floor generator can run instead of an enemy push: a warning (the telegraph), then its actions.")]
    public class CircleHazardDef
    {
        [Doc("For reports.")] public string name = "";
        public float weight = 1f;
        [Doc("Only at or above this floor (counting down). 0 = from the start.")] public int fromFloor;
        [Doc("Warning text shown when it starts (subtitle).")] public string warning = "";
        [SoundRef, Doc("Sound played with the warning.")] public string warningSound = "";
        [Doc("Seconds between the warning and the actions (at least 1, so it is fair).")] public float telegraph = 1.2f;
        [Doc("What happens, usually a startHazard with a duration.")] public List<GameAction> actions = new List<GameAction>();
        [Doc("Seconds the push lasts before the next breather.")] public float duration = 6f;
    }

    [Serializable]
    [Doc("How hard the generated floors are. A circle sets one for its bottom floor and one for its top floor.")]
    public class DifficultyDef
    {
        [Doc("Most generated enemies alive at once.")] public int maxAlive = 2;
        [Doc("Enemies in one push.")] public int pushSize = 2;
        [Doc("Seconds between spawns inside a push.")] public float spawnInterval = 1.2f;
        [Doc("Quiet seconds between pushes.")] public float breather = 4f;
        [Doc("Chance (0-1) a push is a hazard instead of enemies.")] public float hazardChance = 0.2f;
        [Doc("Multiplies generated enemies' health.")] public float healthMultiplier = 1f;
        [Doc("A push ends after this many seconds even if enemies are left (they keep fighting).")] public float pushTimeout = 25f;

        public static DifficultyDef Lerp(DifficultyDef a, DifficultyDef b, float t)
        {
            return new DifficultyDef
            {
                maxAlive = Mathf.RoundToInt(Mathf.Lerp(a.maxAlive, b.maxAlive, t)),
                pushSize = Mathf.RoundToInt(Mathf.Lerp(a.pushSize, b.pushSize, t)),
                spawnInterval = Mathf.Lerp(a.spawnInterval, b.spawnInterval, t),
                breather = Mathf.Lerp(a.breather, b.breather, t),
                hazardChance = Mathf.Lerp(a.hazardChance, b.hazardChance, t),
                healthMultiplier = Mathf.Lerp(a.healthMultiplier, b.healthMultiplier, t),
                pushTimeout = Mathf.Lerp(a.pushTimeout, b.pushTimeout, t),
            };
        }
    }

    [Serializable]
    [Doc("A line for reaching a floor.")]
    public class MilestoneDef
    {
        public int floor;
        [Required] public string text = "";
        [SoundRef, Doc("Voice line or sting played with it.")] public string sound = "";
        public TextStyle style = TextStyle.Banner;
        public Color color = new Color(1f, 0.82f, 0.48f, 1f);
        [Doc("Seconds on screen.")] public float duration = 2.5f;
    }

    // =====================================================================
    // LEVEL SCRIPT: FLOORS
    // =====================================================================

    [Serializable]
    [Doc("The floor counter: which floors of Hell a stage covers. Floors count down as the hero climbs (1000 is the bottom, 1 the top). " +
         "The counter is the script counter \"floor\"; the HUD shows it (ui/theme.json texts.floorFormat) and the progress bar measures floors.")]
    public class FloorsDef
    {
        [Doc("Floor the stage starts on, e.g. 1000. 0 = no floor counter.")] public int start;
        [Doc("Floor the stage ends on, e.g. 976 (the progress bar runs from start to here). 0 = no end.")] public int end;
        [Doc("Travel stages: world units travelled per floor. The counter goes up a floor each time the camera covers this distance, " +
             "and a floor sign stands where it happens. 0 = only actions (setFloor, climbFloors) change the floor.")]
        public float unitsPerFloor;
        [CircleRef, Doc("The Circle of Hell (content/circles) the stage is in: track, props, backdrop, generated floors and milestone lines.")]
        public string circle = "";
        [Doc("Paint floor numbers into the world as signs on travel stages.")] public bool signs = true;
        [Doc("Travel stages: words carved on a big sign at the start of the stage (the place's own sign, e.g. \"IMPS ONLY\"). '' = none.")]
        public string sign = "";
        [Doc("Travel stages: words carved over a big door standing at the top of the stage, where the camera stops (the Heat Stairs end at " +
             "the Magma King's door, \"THE SLAG THRONE\"). '' = none.")]
        public string door = "";
        [Doc("Floors Hell makes the hero walk twice (The Loop): each one takes two floors' distance, its number stays on the counter " +
             "and two signs show it. Add unitsPerFloor per repeat to travelLength.")]
        public List<int> repeat = new List<int>();

        /// <summary>The floor the counter shows after travelling 'steps' floors' distance from the start (repeats counted).</summary>
        public int FloorAfter(int steps)
        {
            int floor = start, shown = 1;
            for (int k = 1; k <= steps; k++)
            {
                if (repeat != null && repeat.Contains(floor) && shown < 2)
                {
                    shown++;
                }
                else
                {
                    floor--;
                    shown = 1;
                }
            }
            return floor;
        }

        /// <summary>Floors' distance from start to end, repeats included (the travelLength a stage needs, over unitsPerFloor).</summary>
        public int Steps => start > 0 && end > 0 && end < start ? start - end + RepeatsBetween() : 0;

        int RepeatsBetween()
        {
            int count = 0;
            if (repeat == null)
                return 0;
            foreach (int f in repeat)
                if (f <= start && f > end)
                    count++;
            return count;
        }
    }

    [Doc("Which way a travel stage goes.")]
    public enum TravelDirection
    {
        [Doc("Walk to the right.")] Right,
        [Doc("Climb upward on ledges.")] Up,
    }
}
