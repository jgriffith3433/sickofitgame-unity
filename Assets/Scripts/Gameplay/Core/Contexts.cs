using UnityEngine;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>Deterministic random numbers from whoever owns the current decision (an entity or the level director).</summary>
    public interface IRandomSource
    {
        float Value();
        float Range(float min, float max);
        int Range(int min, int max);
    }

    /// <summary>What a trigger can see when it is checked.</summary>
    public sealed class TriggerContext
    {
        public GameplayWorld World;
        /// <summary>The entity whose state machine or ability is checking, or null for level rules.</summary>
        public Entity Self;
        public LevelDirector Director;
        /// <summary>Game time (seconds since the level started).</summary>
        public float Now;
        /// <summary>When the current phase (or entity state) began.</summary>
        public float ScopeStart;
        public IRandomSource Random;
    }

    /// <summary>What an action can use when it runs.</summary>
    public sealed class ActionContext
    {
        public GameplayWorld World;
        /// <summary>The entity running the action (ability, state, onSpawn/onDeath), or null for level rules.</summary>
        public Entity Self;
        public LevelDirector Director;
        /// <summary>How many times the rule that ran this had fired before (0 the first time). Drives wave scaling.</summary>
        public int Repeat;
        /// <summary>The phase that owns effects started by this action, or null when they belong to the whole level.</summary>
        public string Scope;
        public IRandomSource Random;
        public float Now;

        public ActionContext With(Entity self)
        {
            return new ActionContext { World = World, Self = self, Director = Director, Repeat = Repeat, Scope = Scope, Random = Random, Now = Now };
        }

        /// <summary>Where "self" is: the entity, or the player when there is none.</summary>
        public Vector2 SelfPosition => Self != null ? Self.Position : World.Player != null ? World.Player.Position : Vector2.zero;

        public Vector2 Resolve(Vector2 position, Anchor anchor)
        {
            switch (anchor)
            {
                case Anchor.Self: return SelfPosition + position;
                case Anchor.Player: return (World.Player != null ? World.Player.Position : Vector2.zero) + position;
                case Anchor.Camera: return World.CameraCenter + position;
                default: return position;
            }
        }
    }
}
