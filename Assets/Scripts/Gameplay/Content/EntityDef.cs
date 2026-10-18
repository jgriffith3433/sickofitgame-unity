using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>
    /// content/entities/{id}.json. Anything that exists in a level: enemies, bosses, projectiles, hazards.
    /// </summary>
    [Serializable]
    [Doc("Anything that exists in a level: an enemy, a boss, a projectile, a hazard. File: content/entities/{id}.json. " +
         "Use \"extends\": \"otherId\" to start from another entity and change only some fields (objects merge, lists replace).")]
    public class EntityDef
    {
        [NonSerialized] public string id;

        [Doc("Which side it is on.")] public Team team = Team.Enemy;
        [Doc("Starting health. 0 = can't be damaged (most projectiles and hazards).")] public int health = 3;
        [Doc("Seconds it can't be hurt again after a hit.")] public float invincibilityTime;
        [Doc("Base movement speed (world units per second) used by movement abilities that don't set their own.")] public float moveSpeed = 2f;
        [Doc("Labels for selecting it from scripts, e.g. \"boss\", \"wave\".")] public List<string> tags = new List<string>();
        [Doc("The player's shots can hit it. Only applies to team Enemy with health above 0.")] public bool shootable = true;
        [Doc("Abilities only run while the player is closer than this (0 = always).")] public float activationRange;

        public BodyDef body = new BodyDef();
        public VisualDef visual = new VisualDef();
        public HealthBarDef healthBar = new HealthBarDef();
        public SoundsDef sounds = new SoundsDef();

        [Doc("Abilities active in every state.")]
        public List<Ability> abilities = new List<Ability>();

        [Doc("Optional state machine. Each state adds its own abilities; transitions move between states. The first state is where it starts, unless initialStates is set.")]
        public List<StateDef> states = new List<StateDef>();

        [EntityStateRef, Doc("Pick the starting state at random from these (deterministic per spawn).")]
        public List<string> initialStates = new List<string>();

        [Doc("Actions run when it spawns (self = this entity).")]
        public List<GameAction> onSpawn = new List<GameAction>();

        [Doc("Actions run when it dies from damage (self = this entity, still at its death position).")]
        public List<GameAction> onDeath = new List<GameAction>();

        [Doc("Entities dropped when it is killed (rewind orbs, power-ups), each rolled separately.")]
        public List<DropDef> drops = new List<DropDef>();

        [Doc("Points dropped on death.")]
        public LootDef loot = new LootDef();
    }

    [Doc("How the body moves and collides.")]
    public enum BodyKind
    {
        [Doc("Moved only by its abilities; ignores gravity and pushes (enemies, most projectiles).")] Kinematic,
        [Doc("Moved by physics: falls with gravity, bounces off ground.")] Dynamic,
    }

    [Doc("Collider shape.")]
    public enum ShapeKind
    {
        Circle,
        Box,
    }

    [Serializable]
    [Doc("Physics body and collider.")]
    public class BodyDef
    {
        public BodyKind kind = BodyKind.Kinematic;
        public ShapeKind shape = ShapeKind.Circle;
        [Doc("Circle radius, in the entity's local units (scaled with the visual scale, as before).")] public float radius = 1f;
        [Doc("Box size, in local units.")] public Vector2 size = new Vector2(1f, 1f);
        public Vector2 offset;
        [Doc("Blocks the player and other solid bodies. False = passes through and only detects overlaps (projectiles, hazards, auras).")]
        public bool solid = true;
        [Doc("Gravity multiplier for Dynamic bodies.")] public float gravity = 1f;
        [Doc("Never rotates.")] public bool freezeRotation = true;
        [Doc("Turns to face its velocity (arrows, fireballs).")] public bool rotateToVelocity;
    }

    [Serializable]
    [Doc("How it looks. Without art it is drawn as a colored placeholder with its id, so gameplay can be built before the art exists.")]
    public class VisualDef
    {
        [Doc("Scale applied to the whole entity (sprite and collider).")] public float scale = 1f;
        [Doc("A single still sprite, used when there is no animator.")] public Sprite sprite;
        [Doc("Animation graph: states play sprite-sheet clips; parameters and transitions switch between them, like Unity's Animator.")]
        public AnimatorDef animator = new AnimatorDef();
        [Doc("Tint multiplied into the sprite.")] public Color color = Color.white;
        [Doc("Placeholder color when there is no art.")] public Color placeholderColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        [Doc("Placeholder diameter in world units.")] public float placeholderSize = 0.6f;
        [Doc("Show the id on the placeholder.")] public bool placeholderLabel = true;
        [Doc("The art faces left when not flipped (most sprites face right).")] public bool facesLeft;
        [Doc("Draw order band.")] public SpriteLayer layer = SpriteLayer.Player;
        [Doc("Draw order within the band (-1 = the standard character order).")] public int sortingOrder = -1;

        public int SortingOrder => sortingOrder >= 0 ? sortingOrder : SpriteLayers.Character;
        [Doc("Flash white when hurt.")] public bool hurtFlash = true;
        [Doc("Color of the bits that fly off when it's hit or dies. Transparent = cream.")] public Color fxColor = Color.clear;
        [Doc("Pop into bits when it dies (enemies with health).")] public bool deathPop = true;
        [Doc("Death clip: squash-and-stretch pop (0 = none, 0.3 = a clear pop), then it shrinks and floats up as it fades.")] public float deathPunch;
        [Doc("Death clip: a bright flash of fxColor at the moment of death.")] public bool deathFlash;
        [Doc("Death clip: how many bits fly off (0 = the default 6).")] public int deathBits;
        [Doc("Death clip: seconds the last frame takes to fade out (0 = the default 0.25).")] public float deathFade;
    }

    [Serializable]
    [Doc("Animation graph, same shape as the generator's AnimationConfig: parameters, states, transitions, defaultStateName.")]
    public class AnimatorDef
    {
        public List<AnimParameterDef> parameters = new List<AnimParameterDef>();
        public List<AnimStateDef> states = new List<AnimStateDef>();
        public List<AnimTransitionDef> transitions = new List<AnimTransitionDef>();
        [AnimStateRef] public string defaultStateName = "";

        public bool IsEmpty => states == null || states.Count == 0;
    }

    [Doc("Animator parameter type.")]
    public enum AnimParameterKind
    {
        Float,
        Int,
        Bool,
        Trigger,
    }

    [Serializable]
    public class AnimParameterDef
    {
        [Required] public string name = "";
        public AnimParameterKind type = AnimParameterKind.Bool;
        public float defaultValue;
    }

    [Serializable]
    [Doc("One animation clip made from a sprite sheet.")]
    public class AnimStateDef
    {
        [Required] public string stateName = "";
        [Doc("Sprite sheet texture (sliced into sprites). Frames play in sprite-name order.")] public Texture2D spriteSheet;
        [Doc("Only sprites whose names start with this.")] public string spriteNameFilter = "";
        [Doc("Skip this many frames (after the name filter), e.g. to play only part of a long sheet.")] public int firstFrame;
        [Doc("How many frames to play from firstFrame. 0 = through the last frame.")] public int frameCount;
        public int fps = 12;
        public bool loop = true;
        public float speed = 1f;
        [Doc("Events fired when a frame is reached; abilities react to them by name (e.g. a shot on the frame the arm swings).")]
        public List<AnimEventDef> events = new List<AnimEventDef>();
    }

    [Serializable]
    public class AnimEventDef
    {
        public int frame;
        [Required, Doc("Event name abilities listen for, e.g. \"fire\".")] public string functionName = "";
    }

    [Serializable]
    [Doc("Switches animation state when its conditions are all true (and the exit time is reached, if hasExitTime). fromState \"Any\" applies from every state.")]
    public class AnimTransitionDef
    {
        [Required] public string fromState = "";
        [Required, AnimStateRef] public string toState = "";
        public bool hasExitTime;
        [Doc("Fraction of the clip that must have played (1 = the end).")] public float exitTime = 1f;
        public List<AnimConditionDef> conditions = new List<AnimConditionDef>();
    }

    [Doc("How an animator condition compares its parameter.")]
    public enum AnimConditionMode
    {
        If,
        IfNot,
        Greater,
        Less,
        Equals,
        NotEqual,
    }

    [Serializable]
    public class AnimConditionDef
    {
        [Required] public string parameterName = "";
        public AnimConditionMode mode = AnimConditionMode.If;
        public float threshold;
    }

    [Serializable]
    [Doc("Floating health bar.")]
    public class HealthBarDef
    {
        public bool show = true;
        [Doc("Sit just above the top of the entity's body (collider); 'offset' then nudges it from there. Off = 'offset' is from the entity's position.")]
        public bool aboveBody = true;
        [Doc("Offset in world units (from the body's top with aboveBody, else from the entity's position).")] public Vector2 offset = new Vector2(0f, 0.1f);
        [Doc("Width in world units.")] public float width = 0.42f;
        public float height = 0.055f;
        public Color color = new Color(1f, 0.36f, 0.23f, 1f);
        public Color trackColor = new Color(0.12f, 0.07f, 0.13f, 0.8f);
    }

    [Serializable]
    public class SoundsDef
    {
        public List<AudioClip> spawn = new List<AudioClip>();
        public List<AudioClip> hurt = new List<AudioClip>();
        public List<AudioClip> death = new List<AudioClip>();
        [Doc("Hurt sounds only play at or below this fraction of health.")] public float hurtThreshold = 1f;
        [Doc("audio.json sound for hits, used when 'hurt' has no clips. Empty = the game's enemyHurt sound (enemies only).")]
        [SoundRef] public string hurtSound = "";
        [Doc("audio.json sound for deaths, used when 'death' has no clips. Empty = the game's enemyDie sound (enemies only).")]
        [SoundRef] public string deathSound = "";
        public float pitchVariation = 0.1f;
        public float volume = 1f;
    }

    [Serializable]
    [Doc("Point pickups dropped on death.")]
    public class LootDef
    {
        [Doc("Chance for each drop, 0-1.")] public float chance;
        public int count = 1;
        [Doc("Diamonds per successful drop (each is its own pickup).")] public int value = 1;
        [EntityRef, Doc("The pickup entity dropped. Empty = the \"diamond\" entity (or the old generated diamond prefab if there's no such entity).")]
        public string entity = "";
    }

    [Serializable]
    [Doc("A state of the entity's state machine.")]
    public class StateDef
    {
        [Required] public string name = "";
        [Doc("Abilities active only in this state (added to the entity's always-on abilities).")]
        public List<Ability> abilities = new List<Ability>();
        [Doc("Actions run on entering the state (self = this entity).")] public List<GameAction> onEnter = new List<GameAction>();
        [Doc("Actions run on leaving the state.")] public List<GameAction> onExit = new List<GameAction>();
        [AnimStateRef, Doc("Animation state to play on entering, if any.")] public string animation = "";
        [Doc("Checked in order every tick; the first that triggers wins.")] public List<TransitionDef> transitions = new List<TransitionDef>();
    }

    [Serializable]
    public class TransitionDef
    {
        [Required, EntityStateRef] public string to = "";
        [Doc("When to switch. Time-based triggers (after, every, randomAfter) count from entering the state.")]
        public Trigger when;
    }
}
