using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>
    /// "When" something happens. Used by level rules (fires the rule's actions) and by entity state
    /// transitions. Each use gets its own copy, so a trigger can keep state (a timer, a random roll).
    ///
    /// Rules fire on the rising edge: when the trigger becomes true, not every tick it stays true.
    /// Repeating triggers (every, signal) fire each time they report true.
    /// </summary>
    [Serializable, JsonPolymorphic]
    [Doc("When something happens. In a rule it fires the actions once each time it becomes true; in a state transition it switches state.")]
    public abstract class Trigger
    {
        /// <summary>False for triggers that report each occurrence (every, signal) instead of a condition.</summary>
        public virtual bool EdgeTriggered => true;

        /// <summary>A copy with its own state, for one use.</summary>
        public Trigger Fresh()
        {
            var copy = (Trigger)MemberwiseClone();
            copy.FreshChildren();
            return copy;
        }

        protected virtual void FreshChildren() { }

        /// <summary>The phase or state this trigger belongs to just started.</summary>
        public virtual void Reset(TriggerContext context) { }

        public abstract bool Check(TriggerContext context);

        public virtual void SaveState(StateWriter writer) { }
        public virtual void LoadState(StateReader reader) { }

        public virtual void CollectChildren(List<Trigger> into) { }
    }

    [Serializable, JsonType("phaseStarted")]
    [Doc("The phase (or state) just started. Fires once per entry.")]
    public sealed class PhaseStartedTrigger : Trigger
    {
        [NonSerialized] bool pending;

        public override void Reset(TriggerContext context) => pending = true;

        public override bool Check(TriggerContext context)
        {
            if (!pending)
                return false;
            pending = false;
            return true;
        }

        public override void SaveState(StateWriter writer) => writer.Bool(pending);
        public override void LoadState(StateReader reader) => pending = reader.Bool();
    }

    [Serializable, JsonType("after")]
    [Doc("This many seconds after the phase (or state) started.")]
    public sealed class AfterTrigger : Trigger
    {
        public float seconds;
        public override bool Check(TriggerContext context) => context.Now - context.ScopeStart >= seconds;
    }

    [Serializable, JsonType("atTime")]
    [Doc("This many seconds after the level started (absolute level time).")]
    public sealed class AtTimeTrigger : Trigger
    {
        public float seconds;
        public override bool Check(TriggerContext context) => context.Now >= seconds;
    }

    [Serializable, JsonType("every")]
    [Doc("Repeatedly: first after 'startAfter' seconds, then every 'seconds'. 'maxTimes' 0 = forever.")]
    public sealed class EveryTrigger : Trigger
    {
        public float seconds = 1f;
        public float startAfter;
        public int maxTimes;

        [NonSerialized] float next;
        [NonSerialized] int fired;

        public override bool EdgeTriggered => false;

        public override void Reset(TriggerContext context)
        {
            next = context.ScopeStart + startAfter;
            fired = 0;
        }

        public override bool Check(TriggerContext context)
        {
            if (maxTimes > 0 && fired >= maxTimes)
                return false;
            if (context.Now < next)
                return false;
            next += Mathf.Max(0.01f, seconds);
            fired++;
            return true;
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Float(next);
            writer.Int(fired);
        }

        public override void LoadState(StateReader reader)
        {
            next = reader.Float();
            fired = reader.Int();
        }
    }

    [Serializable, JsonType("randomAfter")]
    [Doc("A random time between min and max seconds after the phase (or state) started. Rolled on entry, deterministic.")]
    public sealed class RandomAfterTrigger : Trigger
    {
        public float min = 1f;
        public float max = 2f;

        [NonSerialized] float threshold;

        public override void Reset(TriggerContext context) => threshold = context.Random.Range(min, max);
        public override bool Check(TriggerContext context) => context.Now - context.ScopeStart >= threshold;
        public override void SaveState(StateWriter writer) => writer.Float(threshold);
        public override void LoadState(StateReader reader) => threshold = reader.Float();
    }

    [Serializable, JsonType("signal")]
    [Doc("Another rule, sequence, or entity emitted this signal (action 'emit').")]
    public sealed class SignalTrigger : Trigger
    {
        [Required] public string name = "";
        public override bool EdgeTriggered => false;
        public override bool Check(TriggerContext context) => context.World.HasSignal(name);
    }

    [Serializable, JsonType("counter")]
    [Doc("A counter is at least 'atLeast' (and below 'below', if set). Built-in counters: kills, kills:{entityId}, spawned, spawned:{entityId}, playerHits, floor (the floor counter), distance (travel stages).")]
    public sealed class CounterTrigger : Trigger
    {
        [Required] public string name = "";
        public float atLeast = 1f;
        [Doc("Optional upper bound (exclusive). 0 = none.")] public float below;

        public override bool Check(TriggerContext context)
        {
            float value = context.World.GetCounter(name);
            return value >= atLeast && (below <= 0f || value < below);
        }
    }

    [Serializable, JsonType("allDead")]
    [Doc("Everything the target selects is dead, after at least one of them was alive (so it doesn't fire before they spawn).")]
    public sealed class AllDeadTrigger : Trigger
    {
        public TargetSelector target = new TargetSelector();

        [NonSerialized] bool seenAlive;

        public override void Reset(TriggerContext context) => seenAlive = false;

        public override bool Check(TriggerContext context)
        {
            bool anyAlive = context.World.AnyAlive(target, context.Self);
            if (anyAlive)
                seenAlive = true;
            return seenAlive && !anyAlive;
        }

        public override void SaveState(StateWriter writer) => writer.Bool(seenAlive);
        public override void LoadState(StateReader reader) => seenAlive = reader.Bool();
    }

    [Serializable, JsonType("healthBelow")]
    [Doc("A target's health is below a fraction of its maximum (e.g. 0.5), or below 'amount' if set. Defaults to self inside entities.")]
    public sealed class HealthBelowTrigger : Trigger
    {
        public TargetSelector target = new TargetSelector();
        public float fraction = 0.5f;
        [Doc("Use an absolute amount instead of a fraction (0 = use fraction).")] public int amount;

        public override bool Check(TriggerContext context)
        {
            var selector = target.SetCount() == 0 ? TargetSelector.Self() : target;
            return context.World.AnyMatches(selector, context.Self, e =>
                e.IsAlive && (amount > 0 ? e.Health < amount : e.Health < e.MaxHealth * fraction));
        }
    }

    [Serializable, JsonType("distanceToPlayer")]
    [Doc("Inside an entity: the player is closer than 'below' and/or farther than 'above' (0 = no limit).")]
    public sealed class DistanceToPlayerTrigger : Trigger
    {
        public float below;
        public float above;

        public override bool Check(TriggerContext context)
        {
            var player = context.World.Player;
            if (context.Self == null || player == null)
                return false;
            float distance = Vector2.Distance(context.Self.Position, player.Position);
            return (below <= 0f || distance < below) && (above <= 0f || distance > above);
        }
    }

    [Serializable, JsonType("playerInZone")]
    [Doc("The player is inside a box: 'center' and 'size' in world units.")]
    public sealed class PlayerInZoneTrigger : Trigger
    {
        public Vector2 center;
        public Vector2 size = new Vector2(1f, 1f);

        public override bool Check(TriggerContext context)
        {
            var player = context.World.Player;
            if (player == null)
                return false;
            Vector2 p = player.Position;
            return Mathf.Abs(p.x - center.x) <= size.x * 0.5f && Mathf.Abs(p.y - center.y) <= size.y * 0.5f;
        }
    }

    [Serializable, JsonType("animationDone")]
    [Doc("Inside an entity: the current non-looping animation has played to the end.")]
    public sealed class AnimationDoneTrigger : Trigger
    {
        public override bool Check(TriggerContext context) => context.Self != null && context.Self.Animation.IsDone;
    }

    [Serializable, JsonType("always")]
    [Doc("Always true. Useful for a transition that should happen immediately.")]
    public sealed class AlwaysTrigger : Trigger
    {
        public override bool Check(TriggerContext context) => true;
    }

    [Serializable, JsonType("chance")]
    [Doc("Checked every 'every' seconds; true with this probability (0-1) each check. Deterministic.")]
    public sealed class ChanceTrigger : Trigger
    {
        public float probability = 0.5f;
        public float every = 1f;

        [NonSerialized] float next;

        public override bool EdgeTriggered => false;
        public override void Reset(TriggerContext context) => next = context.ScopeStart + every;

        public override bool Check(TriggerContext context)
        {
            if (context.Now < next)
                return false;
            next += Mathf.Max(0.01f, every);
            return context.Random.Value() < probability;
        }

        public override void SaveState(StateWriter writer) => writer.Float(next);
        public override void LoadState(StateReader reader) => next = reader.Float();
    }

    [Serializable, JsonType("anyOf")]
    [Doc("True when any of the listed triggers is true.")]
    public sealed class AnyOfTrigger : Trigger
    {
        public List<Trigger> triggers = new List<Trigger>();

        protected override void FreshChildren()
        {
            triggers = Copy(triggers);
        }

        public override void Reset(TriggerContext context)
        {
            foreach (var t in triggers) t?.Reset(context);
        }

        public override bool Check(TriggerContext context)
        {
            bool any = false;
            // Every child is checked so stateful children (every, chance) advance consistently.
            foreach (var t in triggers)
            {
                if (t != null && t.Check(context))
                    any = true;
            }
            return any;
        }

        public override void SaveState(StateWriter writer) { foreach (var t in triggers) t?.SaveState(writer); }
        public override void LoadState(StateReader reader) { foreach (var t in triggers) t?.LoadState(reader); }
        public override void CollectChildren(List<Trigger> into) => into.AddRange(triggers);

        internal static List<Trigger> Copy(List<Trigger> source)
        {
            var copy = new List<Trigger>(source.Count);
            foreach (var t in source)
                copy.Add(t?.Fresh());
            return copy;
        }
    }

    [Serializable, JsonType("allOf")]
    [Doc("True when all of the listed triggers are true.")]
    public sealed class AllOfTrigger : Trigger
    {
        public List<Trigger> triggers = new List<Trigger>();

        protected override void FreshChildren()
        {
            triggers = AnyOfTrigger.Copy(triggers);
        }

        public override void Reset(TriggerContext context)
        {
            foreach (var t in triggers) t?.Reset(context);
        }

        public override bool Check(TriggerContext context)
        {
            bool all = triggers.Count > 0;
            foreach (var t in triggers)
            {
                if (t == null || !t.Check(context))
                    all = false;
            }
            return all;
        }

        public override void SaveState(StateWriter writer) { foreach (var t in triggers) t?.SaveState(writer); }
        public override void LoadState(StateReader reader) { foreach (var t in triggers) t?.LoadState(reader); }
        public override void CollectChildren(List<Trigger> into) => into.AddRange(triggers);
    }

    [Serializable, JsonType("not")]
    [Doc("True when the inner trigger is false.")]
    public sealed class NotTrigger : Trigger
    {
        public Trigger trigger;

        protected override void FreshChildren()
        {
            trigger = trigger?.Fresh();
        }

        public override void Reset(TriggerContext context) => trigger?.Reset(context);
        public override bool Check(TriggerContext context) => trigger == null || !trigger.Check(context);
        public override void SaveState(StateWriter writer) => trigger?.SaveState(writer);
        public override void LoadState(StateReader reader) => trigger?.LoadState(reader);

        public override void CollectChildren(List<Trigger> into)
        {
            if (trigger != null)
                into.Add(trigger);
        }
    }
}
