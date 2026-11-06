using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>
    /// A level's gameplay, written in the level file's "script" field (levels/{name}.json).
    /// </summary>
    [Doc("Which replay mode a level is.")]
    public enum ReplayKind
    {
        [Doc("An ordinary level.")] None,
        [Doc("Endless Floor: the best floor climbed is saved.")] Endless,
        [Doc("Daily Challenge: the date is the seed, the twist of the day applies and scores go in a local table.")] Daily,
    }

    [Serializable]
    [Doc("A level's gameplay, written as the \"script\" object in levels/{name}.json. Rules fire actions when their trigger fires; " +
         "global rules run the whole level, phase rules only while their phase is active. The first phase starts the level.")]
    public class LevelScript
    {
        [Doc("Random seed (0 = derived from the level number). Same seed, same level, same choices.")] public int seed;
        [Doc("Replay modes (Docs/Story.md, \"Again\"): Endless Floor keeps the best floor climbed, the Daily Challenge takes its seed and twist from the date and keeps " +
             "a local score table. Neither ends a run (no Waystone, no Shaft).")] public ReplayKind replay = ReplayKind.None;
        public WorldDef world = new WorldDef();
        [Doc("Air for a level that is not on a Circle's floors (a meadow, a scene stage): depth layers, weather and distant life, as a Circle's 'ambience'. " +
             "Ignored when the level's Circle has its own. Empty = none.")] public AmbienceDef ambience = new AmbienceDef();
        [Doc("Colour of the rim drawn round every hostile shot so it reads against this level's background (alpha 0 = the Circle's, else a pale " +
             "hot rim). Pick a pale rim for dark backgrounds, an ink rim for bright ones.")] public Color shotRim;
        public FloorsDef floors = new FloorsDef();
        public ObjectiveDef objective = new ObjectiveDef();
        [Doc("Expected length in seconds, for the progress bar (0 = the level's duration).")] public float length;
        [Doc("Labeled ticks on the progress bar, e.g. where the boss arrives.")] public List<ProgressMarkerDef> markers = new List<ProgressMarkerDef>();
        [Doc("Counters set when the level starts.")] public List<CounterDef> counters = new List<CounterDef>();
        [Doc("Rules active for the whole level.")] public List<RuleDef> rules = new List<RuleDef>();
        public List<PhaseDef> phases = new List<PhaseDef>();
    }

    [Serializable]
    [Doc("A stage of the level, like 'waves' then 'boss'. Effects started in a phase (hazards, sequences, modifiers) end when it ends unless marked persist.")]
    public class PhaseDef
    {
        [Required] public string name = "";
        public List<GameAction> onEnter = new List<GameAction>();
        public List<GameAction> onExit = new List<GameAction>();
        public List<RuleDef> rules = new List<RuleDef>();
    }

    [Serializable]
    [Doc("When 'when' fires, run 'do'.")]
    public class RuleDef
    {
        [Doc("Optional name, for reports.")] public string name = "";
        public Trigger when;
        public List<GameAction> @do = new List<GameAction>();
        [Doc("Fire at most this many times (0 = no limit).")] public int maxTimes;
    }

    [Serializable]
    [Doc("How the level is won and lost.")]
    public class ObjectiveDef
    {
        [Doc("Win when this fires. Leave empty to win only through a completeLevel action.")] public Trigger complete;
        [Doc("Lose when this fires (in addition to the player dying).")] public Trigger fail;
        [Doc("Objective line shown in the corner.")] public string text = "";
    }

    [Serializable]
    public class CounterDef
    {
        [Required] public string name = "";
        public float value;
    }

    [Doc("How the camera behaves.")]
    public enum CameraMode
    {
        [Doc("Fixed on the level's starting view (the classic arena).")] Static,
        [Doc("Follows the player inside 'bounds'.")] Follow,
        [Doc("Moves at 'scrollSpeed'; the player is kept on screen.")] Scroll,
        [Doc("Travels through the stage in 'travelDirection': follows the player forward, never back. The ground runs along with it, " +
             "platforms and props stream in ahead and are removed behind. The counter \"distance\" is how far it has gone.")] Travel,
    }

    [Serializable]
    [Doc("The space the level happens in.")]
    public class WorldDef
    {
        public CameraMode camera = CameraMode.Static;
        [Doc("Follow/Scroll: the area the camera may show, as center and size. Zero size = unlimited.")] public Vector2 boundsCenter;
        public Vector2 boundsSize;
        [Doc("Scroll: camera velocity in world units per second.")] public Vector2 scrollSpeed = new Vector2(1f, 0f);
        [Doc("Follow: how quickly the camera catches up (higher = snappier).")] public float followSharpness = 5f;
        [Doc("Multiplies the player's gravity (0.5 = floaty).")] public float playerGravity = 1f;
        [Doc("Travel: which way the stage goes.")] public TravelDirection travelDirection = TravelDirection.Right;
        [Doc("Travel: how far ahead of the player the camera looks (world units).")] public float travelLead = 1.5f;
        [Doc("Travel: the camera also moves forward on its own at least this fast (0 = only as the player goes).")] public float travelSpeed;
        [Doc("Travel: the camera stops after this distance (0 = no end).")] public float travelLength;
        [Doc("Travel: the camera waits while enemies of a generated push are alive, so the player fights instead of running past.")] public bool holdForPushes = true;
        [Doc("Travel: scenery for this stage only (the village debris in The Spill), drawn with its Circle's props.")]
        public List<PropDef> props = new List<PropDef>();
        [Doc("Travel: a floor's path, section by section (climb up, run right or left, a diagonal, an arena), instead of one " +
             "travelDirection. The camera follows it forward only with smooth turns; each section puts its stones, rafts and hazards " +
             "on the floor (spawns). Floors with a path have no streamed ground: the hero starts on a spawned ledge above the lava. " +
             "The path ends where the last section ends (travelLength is ignored). See Docs/GameplayCookbook.md, \"A floor with a path\".")]
        public List<PathSectionDef> path = new List<PathSectionDef>();

        public bool HasPath => camera == CameraMode.Travel && path != null && path.Count > 0;
    }

    /// <summary>content/sequences/{id}.json</summary>
    [Serializable]
    [Doc("A reusable list of steps (actions and waits) such as a boss intro. File: content/sequences/{id}.json.")]
    public class SequenceDef
    {
        [NonSerialized] public string id;
        public List<GameAction> steps = new List<GameAction>();
    }

    [Doc("What happens when a status is applied again while it is active.")]
    public enum Stacking
    {
        [Doc("Restart its duration.")] Refresh,
        [Doc("Add a stack (up to maxStacks); damage and modifiers scale with stacks.")] Stack,
        [Doc("Keep the existing one.")] Ignore,
    }

    /// <summary>content/effects/{id}.json</summary>
    [Serializable]
    [Doc("A timed condition on an entity or the player: poison, burning, slow, stun, shield. File: content/effects/{id}.json.")]
    public class StatusEffectDef
    {
        [NonSerialized] public string id;
        public float duration = 3f;
        [Doc("Seconds between damage ticks.")] public float tickInterval = 0.5f;
        [Doc("Damage each tick (ignores hit invincibility).")] public int damagePerTick;
        public List<StatModifierDef> modifiers = new List<StatModifierDef>();
        [Doc("Can't move or attack.")] public bool stun;
        [Doc("Can't be damaged.")] public bool invulnerable;
        [Doc("Tint while active (alpha 0 = none).")] public Color tint = new Color(1f, 1f, 1f, 0f);
        public Stacking stacking = Stacking.Refresh;
        public int maxStacks = 3;
    }
}
