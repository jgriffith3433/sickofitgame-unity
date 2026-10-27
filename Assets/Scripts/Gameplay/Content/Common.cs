using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;
using SickOfIt.Gameplay;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>
    /// Places things just outside the camera's view ("view edge plus margin") so they are never visible the moment they exist.
    /// Built on <c>GameplayWorld.View</c>, so it holds in landscape and portrait. Travel and Scroll floors ride barriers 0.5 units
    /// outside the view's sides: a walker placed beyond one could never get in, so only kinematic fliers use the sides; everything
    /// else drops in from above the view.
    /// </summary>
    public static class ViewEdge
    {
        /// <summary>Margin (world units) that gives rain falling 3.2/s about 1.5 s of warning.</summary>
        public const float DefaultMargin = 5f;
        /// <summary>How far in from the view's sides a walker's x is kept, inside the Travel/Scroll barriers.</summary>
        const float SidePad = 0.6f;

        /// <summary>The point above the view, over x, 'margin' units above its top edge.</summary>
        public static Vector2 Above(Rect view, float x, float margin)
        {
            float pad = Mathf.Min(SidePad, view.width * 0.25f);
            return new Vector2(Mathf.Clamp(x, view.xMin + pad, view.xMax - pad), view.yMax + margin);
        }

        /// <summary>
        /// Moves a wanted spot (world position) out past the nearest view edge by 'margin'. Vertical edges (and the bottom, which is
        /// lava or floor) become the top; a side is used only by kinematic fliers.
        /// </summary>
        public static Vector2 Place(GameplayWorld world, EntityDef def, Vector2 wanted, float margin)
        {
            Rect view = world.View;
            Vector2 c = view.center;
            float nx = (wanted.x - c.x) / Mathf.Max(0.01f, view.width * 0.5f);
            float ny = (wanted.y - c.y) / Mathf.Max(0.01f, view.height * 0.5f);
            bool flier = def != null && def.body != null && def.body.kind == BodyKind.Kinematic;
            if (flier && Mathf.Abs(nx) > Mathf.Abs(ny))
            {
                float x = nx >= 0f ? view.xMax + margin : view.xMin - margin;
                return new Vector2(x, Mathf.Clamp(wanted.y, view.yMin + 0.5f, view.yMax - 0.5f));
            }
            return Above(view, wanted.x, margin);
        }

        /// <summary>The same, looking the entity up by id.</summary>
        public static Vector2 Place(GameplayWorld world, string entityId, Vector2 wanted, float margin)
        {
            EntityDef def = null;
            if (!string.IsNullOrEmpty(entityId))
                world.Content.Entities.TryGetValue(entityId, out def);
            return Place(world, def, wanted, margin);
        }
    }

    [Doc("Which side something is on. Entities hurt entities of other teams; Neutral hurts and is hurt by no one unless an ability says so.")]
    public enum Team
    {
        Enemy,
        Player,
        Neutral,
    }

    [Doc("A number on an entity that modifiers and status effects can change.")]
    public enum Stat
    {
        [Doc("Multiplies movement speed.")] MoveSpeed,
        [Doc("Multiplies damage dealt.")] Damage,
        [Doc("Multiplies damage taken (0 = immune).")] DamageTaken,
        [Doc("Multiplies how often attacks happen (2 = twice as often).")] AttackSpeed,
        [Doc("Multiplies projectile speed.")] ProjectileSpeed,
        [Doc("Multiplies maximum health (applies when spawned).")] MaxHealth,
        [Doc("Multiplies sprite and collider size (applies when spawned).")] Size,
    }

    [Serializable]
    [Doc("Changes a stat: value * multiply + add.")]
    public class StatModifierDef
    {
        public Stat stat;
        [Doc("Multiplier (1 = unchanged).")] public float multiply = 1f;
        [Doc("Added after multiplying.")] public float add;
        [Doc("Added to 'multiply' for each previous repeat of the rule that applied this (wave scaling): 0.2 makes wave 3 get multiply + 0.4.")]
        public float multiplyPerRepeat;

        public float MultiplyFor(int repeat) => multiply + multiplyPerRepeat * repeat;
    }

    [Serializable]
    [Doc("Picks entities. Set exactly one of: player, self, id, tag, entity, all.")]
    public class TargetSelector
    {
        [Doc("The player.")] public bool player;
        [Doc("The entity running this (only inside entity abilities, states, and onDeath).")] public bool self;
        [Doc("The entity spawned with this instance id.")] public string id = "";
        [Doc("Every live entity with this tag.")] public string tag = "";
        [EntityRef, Doc("Every live entity of this entity type.")] public string entity = "";
        [Doc("Every live entity except the player.")] public bool all;

        public static TargetSelector Self() => new TargetSelector { self = true };
        public static TargetSelector Player() => new TargetSelector { player = true };

        public int SetCount()
        {
            int n = 0;
            if (player) n++;
            if (self) n++;
            if (!string.IsNullOrEmpty(id)) n++;
            if (!string.IsNullOrEmpty(tag)) n++;
            if (!string.IsNullOrEmpty(entity)) n++;
            if (all) n++;
            return n;
        }

        public override string ToString()
        {
            if (player) return "player";
            if (self) return "self";
            if (!string.IsNullOrEmpty(id)) return "id " + id;
            if (!string.IsNullOrEmpty(tag)) return "tag " + tag;
            if (!string.IsNullOrEmpty(entity)) return "entity " + entity;
            return all ? "all" : "nothing";
        }
    }

    [Doc("Where a position is measured from.")]
    public enum Anchor
    {
        [Doc("World coordinates.")] World,
        [Doc("Relative to the entity running the action.")] Self,
        [Doc("Relative to the player.")] Player,
        [Doc("Relative to the camera center.")] Camera,
    }

    [Doc("How a scripted move speeds up and slows down over its duration.")]
    public enum Ease
    {
        [Doc("Constant speed.")] Linear,
        [Doc("Starts slow, ends fast (being sucked in).")] In,
        [Doc("Starts fast, ends slow (settling).")] Out,
        [Doc("Slow at both ends (a camera move).")] InOut,
    }

    public static class Easing
    {
        /// <summary>Eased progress for t in 0..1.</summary>
        public static float Apply(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.In: return t * t;
                case Ease.Out: return 1f - (1f - t) * (1f - t);
                case Ease.InOut: return t * t * (3f - 2f * t);
                default: return t;
            }
        }
    }

    [Doc("Which way the player faces.")]
    public enum FaceDirection
    {
        Left,
        Right,
    }

    [Doc("What a fade does.")]
    public enum FadeTo
    {
        [Doc("Fades the screen to 'color' (black) and holds it there.")] Black,
        [Doc("Fades back to the game.")] Clear,
    }

    // ─── Reference markers (checked by the content validator) ─────────────

    /// <summary>The string names an entity in content/entities.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class EntityRefAttribute : Attribute { }

    /// <summary>The string names a sound in audio.json.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class SoundRefAttribute : Attribute { }

    /// <summary>The string names a sequence in content/sequences.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class SequenceRefAttribute : Attribute { }

    /// <summary>The string names a status effect in content/effects.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class EffectRefAttribute : Attribute { }

    /// <summary>The string names a phase of the current level script.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class PhaseRefAttribute : Attribute { }

    /// <summary>The string names an animation state of the entity's animator.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class AnimStateRefAttribute : Attribute { }

    /// <summary>The string names a state of the entity's state machine.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class EntityStateRefAttribute : Attribute { }

    /// <summary>The field is required (an empty string or null is an error).</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class RequiredAttribute : Attribute { }
}
