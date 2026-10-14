using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
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

    // ─── Reference markers (checked by the content validator) ─────────────

    /// <summary>The string names an entity in content/entities.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class EntityRefAttribute : Attribute { }

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
