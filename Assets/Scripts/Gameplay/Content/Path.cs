using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // A FLOOR'S PATH (Docs/LevelDesign.md, "How a floor reads")
    // =====================================================================

    [Doc("Which way a section of a floor's path goes.")]
    public enum PathDirection
    {
        [Doc("Climb straight up (a lava fall, a chain shaft).")] Up,
        [Doc("Run to the right (a lava river, a chase).")] Right,
        [Doc("Run to the left.")] Left,
        [Doc("Up and to the right at 45 degrees (a slope of floes).")] UpRight,
        [Doc("Up and to the left at 45 degrees.")] UpLeft,
        [Doc("A locked screen (an arena, a floor boss's tier): the camera holds until a nextSection action.")] Arena,
    }

    [Doc("Where a path spawn's 'at' is measured from.")]
    public enum PathAnchor
    {
        [Doc("From the path's origin (where the camera starts): one coordinate system for the whole floor.")] Path,
        [Doc("From the start of its own section (sections can be reordered or reused as chunks).")] Section,
        [Doc("World coordinates.")] World,
    }

    [Serializable]
    [Doc("Something a path section puts on the floor: a stone, a raft, a chain, a hazard, an enemy. Spawned when the section before " +
         "it starts (so it is there before the hero sees it), or when its own section starts with onEnter (a chase's lava wave).")]
    public class PathSpawnDef
    {
        [Required, EntityRef] public string entity = "";
        [Doc("Position, measured from 'anchor'.")] public Vector2 at;
        public PathAnchor anchor = PathAnchor.Path;
        [Doc("Instance id for targeting it later (scripts, the playtest).")] public string id = "";
        public List<string> tags = new List<string>();
        [Doc("Spawn when its own section starts instead of one section ahead.")] public bool onEnter;
        [Doc("Face this way: 1 right, -1 left, 0 = toward the player.")] public float facing;
    }

    [Serializable]
    [Doc("One stretch of a floor's path: the camera moves along 'direction' for 'length' world units, forward only, following the hero " +
         "with a look-ahead, or at least at autoSpeed (chases, raft rides). Turns between sections are rounded off over 'turn' units. " +
         "Entering a section emits the signal \"section:<name>\".")]
    public class PathSectionDef
    {
        [Doc("Name, for the section signal, sectionReached and reports.")] public string name = "";
        public PathDirection direction = PathDirection.Up;
        [Doc("How far the camera goes along this section (world units). Arena: ignored.")] public float length = 8f;
        [Doc("The camera moves forward at least this fast (world units per second): a chase or a raft ride. 0 = only as the hero goes.")]
        public float autoSpeed;
        [Doc("Look-ahead in the travel direction (world units). -1 = the world's travelLead.")] public float lead = -1f;
        [Doc("Distance over which the turn into the next section is rounded off.")] public float turn = 2f;
        [Doc("Generated sections: replace this one with this many chunks from the Circle's 'chunks' (seeded by the level seed and the floor).")]
        public int generate;
        [Doc("What the section puts on the floor.")] public List<PathSpawnDef> spawns = new List<PathSpawnDef>();

        public Vector2 Axis
        {
            get
            {
                switch (direction)
                {
                    case PathDirection.Up: return Vector2.up;
                    case PathDirection.Right: return Vector2.right;
                    case PathDirection.Left: return Vector2.left;
                    case PathDirection.UpRight: return new Vector2(1f, 1f).normalized;
                    case PathDirection.UpLeft: return new Vector2(-1f, 1f).normalized;
                    default: return Vector2.zero;
                }
            }
        }

        public float Length => direction == PathDirection.Arena ? 0f : Mathf.Max(0f, length);

        /// <summary>A copy with its own spawn list (generated chunks are placed more than once).</summary>
        public PathSectionDef Copy()
        {
            var copy = (PathSectionDef)MemberwiseClone();
            copy.spawns = spawns != null ? new List<PathSpawnDef>(spawns) : new List<PathSpawnDef>();
            return copy;
        }
    }

    [Serializable]
    [Doc("A piece of floor the generator can use (content/circles 'chunks'): a section with its stones, rafts and hazards, picked by " +
         "weight once the floor counter is at or above fromFloor.")]
    public class PathChunkDef
    {
        [Doc("For reports.")] public string name = "";
        public float weight = 1f;
        [Doc("Only at or above this floor (counting down). 0 = from the start.")] public int fromFloor;
        [Doc("The section it adds (its spawns are usually anchored to the Section).")] public PathSectionDef section = new PathSectionDef();
    }

    // =====================================================================
    // ABOVE THE LAVA: the backdrop of a path floor
    // =====================================================================

    [Serializable]
    [Doc("The world falling away below a floor of the Wastes: a sky gradient, the lava sea far below with its glow, parallax layers of " +
         "spires, smoke and floating islands, embers drifting up and a heat shimmer over the sea. Drawn only; nothing in it collides. " +
         "Used by floors with a path (world.path) in this Circle.")]
    public class SkyDef
    {
        [Doc("Draw it (floors with a path in this Circle).")] public bool enabled;
        [Doc("Colour at the top of the screen.")] public Color top = new Color(0.07f, 0.02f, 0.03f, 1f);
        [Doc("Colour just above the lava sea (the glow).")] public Color glow = new Color(0.55f, 0.13f, 0.04f, 1f);
        [Doc("The lava sea's colour.")] public Color sea = new Color(1f, 0.42f, 0.1f, 1f);
        [Doc("World y of the far lava sea's surface where the camera starts, measured from the path origin.")] public float seaLevel = -5f;
        [Doc("How much the far sea follows the camera up (1 = stuck to the screen, 0 = fixed in the world). 0.9 = it drops away slowly as he climbs.")]
        public float seaParallax = 0.9f;
        [Doc("Lowest the sea's horizon gets on screen, as a fraction of the screen height from the bottom (it never quite leaves).")]
        public float seaMinOnScreen = 0.06f;
        public List<SkyLayerDef> layers = new List<SkyLayerDef>();
        [Doc("Embers drifting up across the screen at once.")] public int embers = 28;
        public Color emberColor = new Color(1f, 0.6f, 0.2f, 0.85f);
        [Doc("Heat shimmer over the far sea: how strongly its haze bands wobble (0 = none).")] public float shimmer = 0.6f;
    }

    [Serializable]
    [Doc("A parallax layer of the sky: shapes repeated on a grid, moving with the camera by 'parallax' (1 = stuck to the screen, far away; " +
         "0 = fixed in the world). 'band' limits it to heights around the far sea (spires) or lets it fill the sky (islands, smoke).")]
    public class SkyLayerDef
    {
        [Doc("For reports.")] public string name = "";
        [Doc("Art (optional). Without it the shape below is drawn.")] public Sprite sprite;
        public PropShape shape = PropShape.Spire;
        public Color color = new Color(0.08f, 0.03f, 0.04f, 1f);
        [Doc("Width and height in world units.")] public Vector2 size = new Vector2(1.2f, 3f);
        [Doc("Random size variation (0.3 = 70% to 130%).")] public float sizeVariation = 0.3f;
        [Doc("How much it follows the camera, across and up: [x, y].")] public Vector2 parallax = new Vector2(0.8f, 0.85f);
        [Doc("Grid cell size [across, up]: the average distance between two shapes.")] public Vector2 spacing = new Vector2(3f, 3f);
        [Doc("Chance (0-1) that a cell has one.")] public float chance = 0.7f;
        [Doc("Stand on the far sea's surface (spires) instead of floating anywhere in the sky.")] public bool onSea;
        [Doc("Floating: drawn upside down under its top (an island of rock with its roots hanging).")] public bool hanging;
        [Doc("Sideways drift in world units per second (smoke).")] public float drift;
        [Doc("Draw order inside the sky (higher is nearer).")] public int order;
    }

    // =====================================================================
    // SCRIPT: actions and triggers for paths
    // =====================================================================

    [Serializable, JsonType("nextSection")]
    [Doc("Floors with a path: lets the camera leave an Arena section (the fight on this tier is over).")]
    public sealed class NextSectionAction : GameAction
    {
        public override void Run(ActionContext context) => context.World.Effects.ReleaseSection();
    }

    [Serializable, JsonType("sectionReached")]
    [Doc("Floors with a path: the camera has reached the section named 'section' (or one after it).")]
    public sealed class SectionReachedTrigger : Trigger
    {
        [Required] public string section = "";
        public override bool Check(TriggerContext context) => context.World.Effects.SectionReached(section);
    }

    [Serializable, JsonType("playerOn")]
    [Doc("The hero is standing on an entity picked by 'target' (a landing, a raft, a stone).")]
    public sealed class PlayerOnTrigger : Trigger
    {
        public TargetSelector target = new TargetSelector();

        public override bool Check(TriggerContext context)
        {
            var world = context.World;
            return world.AnyMatches(target, context.Self, e => !e.IsPlayer && world.PlayerStandsOn(e));
        }
    }
}
