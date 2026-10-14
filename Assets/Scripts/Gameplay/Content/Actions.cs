using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>Something that happens. Run by level rules, sequences, and entity reactions.</summary>
    [Serializable, JsonPolymorphic]
    [Doc("Something that happens: spawn, change phase, show text, apply a status...")]
    public abstract class GameAction
    {
        public abstract void Run(ActionContext context);

        /// <summary>Nested action lists (for if/random), so the director can index every action.</summary>
        public virtual void CollectChildren(List<GameAction> into) { }
    }

    // =====================================================================
    // SPAWNING
    // =====================================================================

    [Serializable, JsonType("spawn")]
    [Doc("Spawns one or more entities.")]
    public sealed class SpawnAction : GameAction
    {
        [Required, EntityRef] public string entity = "";
        [Doc("Position, measured from 'anchor'.")] public Vector2 at;
        public Anchor anchor = Anchor.World;
        public int count = 1;
        [Doc("Offset between each of 'count' entities.")] public Vector2 spacing = new Vector2(0.5f, 0f);
        [Doc("Instance id for targeting it later (count must be 1).")] public string id = "";
        [Doc("Tags for targeting it later.")] public List<string> tags = new List<string>();
        [Doc("Overrides the entity's starting health (0 = keep).")] public int health;
        public List<StatModifierDef> modifiers = new List<StatModifierDef>();
        [Doc("Initial velocity.")] public Vector2 velocity;
        [Doc("Face the player when spawned.")] public bool facePlayer = true;

        public override void Run(ActionContext context)
        {
            Vector2 origin = context.Resolve(at, anchor);
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                var options = SpawnOptions.From(context, id, tags, health, modifiers);
                options.Velocity = velocity;
                options.FacePlayer = facePlayer;
                context.World.Spawn(entity, origin + spacing * i, options);
            }
        }
    }

    [Serializable, JsonType("spawnWave")]
    [Doc("Spawns 'count' entities spread over an area or a list of points, optionally one every 'interval' seconds. Use modifiers with multiplyPerRepeat to make each wave of an 'every' rule stronger.")]
    public sealed class SpawnWaveAction : GameAction
    {
        [Required, EntityRef] public string entity = "";
        public int count = 3;
        [Doc("Spawn points (measured from 'anchor'). If fewer than count, the rest spawn in the area.")] public List<Vector2> points = new List<Vector2>();
        [Doc("Center of the spawn area, measured from 'anchor'.")] public Vector2 areaCenter;
        [Doc("Size of the spawn area. Zero = all at areaCenter.")] public Vector2 areaSize = new Vector2(10f, 0f);
        public Anchor anchor = Anchor.Camera;
        [Doc("Seconds between each spawn (0 = all at once).")] public float interval;
        public List<string> tags = new List<string>();
        public int health;
        public List<StatModifierDef> modifiers = new List<StatModifierDef>();
        public bool facePlayer = true;

        public override void Run(ActionContext context)
        {
            if (interval > 0f && count > 1)
            {
                context.Director.StartWave(this, context);
                return;
            }
            for (int i = 0; i < count; i++)
                SpawnOne(context, i);
        }

        public void SpawnOne(ActionContext context, int index)
        {
            Vector2 local = index < points.Count
                ? points[index]
                : areaCenter + new Vector2(context.Random.Range(-0.5f, 0.5f) * areaSize.x, context.Random.Range(-0.5f, 0.5f) * areaSize.y);
            var options = SpawnOptions.From(context, "", tags, health, modifiers);
            options.FacePlayer = facePlayer;
            context.World.Spawn(entity, context.Resolve(local, anchor), options);
        }
    }

    [Serializable, JsonType("despawn")]
    [Doc("Removes entities without them dying (no onDeath, no loot, not counted as kills).")]
    public sealed class DespawnAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.Die(DeathCause.Despawn));
    }

    [Serializable, JsonType("kill")]
    [Doc("Kills entities (onDeath runs; counts as a kill).")]
    public sealed class KillAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.Die(DeathCause.Damage));
    }

    [Serializable, JsonType("damage")]
    [Doc("Damages entities (or the player).")]
    public sealed class DamageAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        public int amount = 1;
        [Doc("Ignore hit invincibility.")] public bool ignoreInvincibility;
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.Damage(amount, context.Self, ignoreInvincibility));
    }

    [Serializable, JsonType("heal")]
    [Doc("Restores health (up to the maximum).")]
    public sealed class HealAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        public int amount = 1;
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.Heal(amount));
    }

    // =====================================================================
    // STATUS AND STATS
    // =====================================================================

    [Serializable, JsonType("applyStatus")]
    [Doc("Applies a status effect (content/effects) to entities or the player.")]
    public sealed class ApplyStatusAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        [Required, EffectRef] public string effect = "";
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.ApplyStatus(effect, context.Self));
    }

    [Serializable, JsonType("removeStatus")]
    [Doc("Removes a status effect.")]
    public sealed class RemoveStatusAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        [Required, EffectRef] public string effect = "";
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.RemoveStatus(effect));
    }

    [Serializable, JsonType("modifier")]
    [Doc("Changes stats of entities. With 'includeFuture' it also applies to matching entities spawned later (until it expires or its phase ends).")]
    public sealed class ModifierAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        public List<StatModifierDef> modifiers = new List<StatModifierDef>();
        [Doc("Seconds it lasts. 0 = permanent.")] public float duration;
        [Doc("Also apply to matching entities spawned later.")] public bool includeFuture;

        public override void Run(ActionContext context)
        {
            float until = duration > 0f ? context.Now + duration : float.PositiveInfinity;
            int repeat = context.Repeat;
            context.World.ForEachTarget(target, context.Self, e =>
            {
                foreach (var m in modifiers)
                    e.AddModifier(m.stat, m.MultiplyFor(repeat), m.add, until);
            });
            if (includeFuture)
                context.Director.AddSpawnModifier(this, repeat, until, context.Scope);
        }
    }

    // =====================================================================
    // FLOW
    // =====================================================================

    [Serializable, JsonType("goToPhase")]
    [Doc("Ends the current phase (stopping its hazards, sequences, and timed modifiers) and starts another.")]
    public sealed class GoToPhaseAction : GameAction
    {
        [Required, PhaseRef] public string phase = "";
        public override void Run(ActionContext context) => context.Director.GoToPhase(phase);
    }

    [Serializable, JsonType("runSequence")]
    [Doc("Runs a list of actions in order, with 'wait' steps between them. Use a named sequence (content/sequences) or list the steps here.")]
    public sealed class RunSequenceAction : GameAction
    {
        [SequenceRef, Doc("Named sequence id.")] public string sequence = "";
        [Doc("Inline steps, used when 'sequence' is empty.")] public List<GameAction> steps = new List<GameAction>();
        [Doc("Keep running after the phase that started it ends.")] public bool persist;

        public override void Run(ActionContext context) => context.Director.StartSequence(this, context);
        public override void CollectChildren(List<GameAction> into) => into.AddRange(steps);
    }

    [Serializable, JsonType("wait")]
    [Doc("Inside a sequence: pause before the next step.")]
    public sealed class WaitAction : GameAction
    {
        public float seconds = 1f;
        public override void Run(ActionContext context) { }
    }

    [Serializable, JsonType("emit")]
    [Doc("Emits a signal that 'signal' triggers react to (next tick).")]
    public sealed class EmitAction : GameAction
    {
        [Required] public string signal = "";
        public override void Run(ActionContext context) => context.World.Emit(signal);
    }

    [Serializable, JsonType("setCounter")]
    [Doc("Sets a named counter.")]
    public sealed class SetCounterAction : GameAction
    {
        [Required] public string name = "";
        public float value;
        public override void Run(ActionContext context) => context.World.SetCounter(name, value);
    }

    [Serializable, JsonType("addCounter")]
    [Doc("Adds to a named counter (negative to subtract).")]
    public sealed class AddCounterAction : GameAction
    {
        [Required] public string name = "";
        public float amount = 1f;
        public override void Run(ActionContext context) => context.World.SetCounter(name, context.World.GetCounter(name) + amount);
    }

    [Serializable, JsonType("if")]
    [Doc("Runs 'then' if the condition is true right now, otherwise 'else'.")]
    public sealed class IfAction : GameAction
    {
        public Trigger condition;
        public List<GameAction> then = new List<GameAction>();
        public List<GameAction> @else = new List<GameAction>();

        public override void Run(ActionContext context)
        {
            var check = condition?.Fresh();
            var triggerContext = new TriggerContext
            {
                World = context.World, Self = context.Self, Director = context.Director,
                Now = context.Now, ScopeStart = context.Now, Random = context.Random,
            };
            check?.Reset(triggerContext);
            bool value = check != null && check.Check(triggerContext);
            context.World.RunActions(value ? then : @else, context);
        }

        public override void CollectChildren(List<GameAction> into)
        {
            into.AddRange(then);
            into.AddRange(@else);
        }
    }

    [Serializable]
    public class WeightedActions
    {
        public float weight = 1f;
        public List<GameAction> @do = new List<GameAction>();
    }

    [Serializable, JsonType("random")]
    [Doc("Runs one of the choices, picked by weight (deterministic).")]
    public sealed class RandomAction : GameAction
    {
        public List<WeightedActions> choices = new List<WeightedActions>();

        public override void Run(ActionContext context)
        {
            float total = 0f;
            foreach (var c in choices) total += Mathf.Max(0f, c.weight);
            if (total <= 0f)
                return;
            float roll = context.Random.Range(0f, total);
            foreach (var c in choices)
            {
                roll -= Mathf.Max(0f, c.weight);
                if (roll < 0f)
                {
                    context.World.RunActions(c.@do, context);
                    return;
                }
            }
            context.World.RunActions(choices[choices.Count - 1].@do, context);
        }

        public override void CollectChildren(List<GameAction> into)
        {
            foreach (var c in choices) into.AddRange(c.@do);
        }
    }

    // =====================================================================
    // HAZARDS
    // =====================================================================

    [Serializable, JsonType("startHazard")]
    [Doc("Starts firing projectile entities from the level itself (fire rain, turrets, sweeping lasers). Stops when its phase ends, after 'duration', or on stopHazard.")]
    public sealed class StartHazardAction : GameAction
    {
        [Required, Doc("Name for stopHazard.")] public string id = "";
        [Required, EntityRef] public string projectile = "";
        public Pattern pattern;
        public float speed = 4f;
        public Aim aim = Aim.Down;
        public Vector2 direction = Vector2.down;
        [Doc("Where shots come from, measured from 'anchor' (rain patterns use the top of the screen instead).")] public Vector2 origin;
        public Anchor anchor = Anchor.Camera;
        [Doc("Seconds between volleys.")] public float interval = 1f;
        public float initialDelay;
        [Doc("Seconds it runs (0 = until its phase ends or stopHazard).")] public float duration;
        [Doc("Keep running after its phase ends.")] public bool persist;
        [Doc("Damage per shot; 0 = the projectile's own.")] public int damage;
        [Doc("Volleys per second added each second it runs (it intensifies). Negative slows it down.")] public float rampPerSecond;

        public override void Run(ActionContext context) => context.Director.StartHazard(this, context);
    }

    [Serializable, JsonType("stopHazard")]
    [Doc("Stops a hazard started with startHazard.")]
    public sealed class StopHazardAction : GameAction
    {
        [Required] public string id = "";
        public override void Run(ActionContext context) => context.Director.StopHazard(id);
    }

    // =====================================================================
    // PRESENTATION
    // =====================================================================

    [Serializable, JsonType("showText")]
    [Doc("Shows text on screen.")]
    public sealed class ShowTextAction : GameAction
    {
        public string text = "";
        [Doc("Seconds (0 = until replaced or hideText).")] public float duration = 2f;
        public TextStyle style = TextStyle.Banner;
        public Color color = Color.white;
        [SoundRef, Doc("audio.json sound spoken (or played) with it, e.g. the player's voice line for this subtitle.")] public string sound = "";

        public override void Run(ActionContext context)
        {
            // A spoken line keeps its text up at least as long as it takes to say it.
            float shown = duration > 0f ? Mathf.Max(duration, SickOfIt.Core.Audio.Sfx.LengthOf(sound) + 0.4f) : 0f;
            context.World.Hud.ShowText(text, style, color, shown > 0f ? context.Now + shown : float.PositiveInfinity);
            SickOfIt.Core.Audio.Sfx.Play(sound);
        }
    }

    public enum TextStyle
    {
        [Doc("Big, centered.")] Banner,
        [Doc("Smaller, lower on screen.")] Subtitle,
    }

    [Serializable, JsonType("hideText")]
    public sealed class HideTextAction : GameAction
    {
        public TextStyle style = TextStyle.Banner;
        public override void Run(ActionContext context) => context.World.Hud.ShowText("", style, Color.white, 0f);
    }

    [Serializable, JsonType("objectiveText")]
    [Doc("Sets the objective line in the corner ('' hides it).")]
    public sealed class ObjectiveTextAction : GameAction
    {
        public string text = "";
        public override void Run(ActionContext context) => context.World.Hud.SetObjective(text);
    }

    [Serializable, JsonType("bossBar")]
    [Doc("Shows a big health bar for an entity.")]
    public sealed class BossBarAction : GameAction
    {
        public TargetSelector target = new TargetSelector();
        public string label = "";
        public override void Run(ActionContext context) => context.World.Hud.ShowBossBar(target, label);
    }

    [Serializable, JsonType("hideBossBar")]
    public sealed class HideBossBarAction : GameAction
    {
        public override void Run(ActionContext context) => context.World.Hud.HideBossBar();
    }

    [Serializable, JsonType("cameraShake")]
    public sealed class CameraShakeAction : GameAction
    {
        public float intensity = 0.2f;
        public float duration = 0.3f;
        public override void Run(ActionContext context) => context.World.Effects.Shake(intensity, duration);
    }

    [Serializable, JsonType("flash")]
    [Doc("Flashes the screen a color.")]
    public sealed class FlashAction : GameAction
    {
        public Color color = Color.white;
        public float duration = 0.2f;
        public override void Run(ActionContext context) => context.World.Effects.Flash(color, duration);
    }

    [Serializable, JsonType("slowMotion")]
    [Doc("Slows game time to 'scale' for 'duration' seconds of real time.")]
    public sealed class SlowMotionAction : GameAction
    {
        public float scale = 0.3f;
        public float duration = 1f;
        public override void Run(ActionContext context) => context.World.Effects.SlowMotion(scale, duration);
    }

    [Serializable, JsonType("playSound")]
    [Doc("Plays a sound: an audio.json sound id (announcer lines, stings) or a clip.")]
    public sealed class PlaySoundAction : GameAction
    {
        [SoundRef, Doc("audio.json sound id (e.g. an announcer line). Used when 'clip' isn't set.")] public string sound = "";
        public AudioClip clip;
        public float volume = 1f;

        public override void Run(ActionContext context)
        {
            if (clip != null)
                context.World.Effects.PlaySound(clip, volume);
            else
                SickOfIt.Core.Audio.Sfx.Play(sound, volume);
        }
    }

    [Serializable, JsonType("playMusic")]
    [Doc("Switches the level music.")]
    public sealed class PlayMusicAction : GameAction
    {
        public AudioClip clip;
        public float volume = 1f;
        public override void Run(ActionContext context) => context.World.Effects.PlayMusic(clip, volume);
    }

    [Serializable, JsonType("musicVolume")]
    public sealed class MusicVolumeAction : GameAction
    {
        public float volume = 1f;
        public override void Run(ActionContext context) => context.World.Effects.MusicVolume(volume);
    }

    // =====================================================================
    // PLAYER AND LEVEL
    // =====================================================================

    [Serializable, JsonType("playerInvincible")]
    public sealed class PlayerInvincibleAction : GameAction
    {
        public float seconds = 2f;
        public override void Run(ActionContext context) => context.World.Player?.MakeInvincible(seconds);
    }

    [Serializable, JsonType("lockInput")]
    [Doc("Stops the player from moving and shooting for 'seconds' (0 = until unlockInput).")]
    public sealed class LockInputAction : GameAction
    {
        public float seconds;
        public override void Run(ActionContext context) => context.World.SetInputLocked(seconds > 0f ? context.Now + seconds : float.PositiveInfinity);
    }

    [Serializable, JsonType("unlockInput")]
    public sealed class UnlockInputAction : GameAction
    {
        public override void Run(ActionContext context) => context.World.SetInputLocked(0f);
    }

    [Serializable, JsonType("completeLevel")]
    [Doc("Completes the level now (the level's completionDelay and next video still apply).")]
    public sealed class CompleteLevelAction : GameAction
    {
        public override void Run(ActionContext context) => context.Director.Complete();
    }

    [Serializable, JsonType("failLevel")]
    [Doc("Fails the level: plays the level's death scene.")]
    public sealed class FailLevelAction : GameAction
    {
        public override void Run(ActionContext context) => context.Director.Fail();
    }

    [Serializable, JsonType("goToLevel")]
    [Doc("Leaves for another level (by number) instead of the normal next step. For branching paths and secret exits.")]
    public sealed class GoToLevelAction : GameAction
    {
        public int level = 1;
        public override void Run(ActionContext context) => context.Director.Host?.GoToLevel(level);
    }

    [Serializable, JsonType("playVideo")]
    [Doc("Leaves for a video (by number).")]
    public sealed class PlayVideoAction : GameAction
    {
        public int video = 1;
        public override void Run(ActionContext context) => context.Director.Host?.PlayVideo(video);
    }

    [Serializable, JsonType("returnToMenu")]
    public sealed class ReturnToMenuAction : GameAction
    {
        public override void Run(ActionContext context) => context.Director.Host?.ReturnToMenu();
    }

    // =====================================================================
    // ENTITY CONTROL
    // =====================================================================

    [Serializable, JsonType("setState")]
    [Doc("Moves entities to a state of their state machine.")]
    public sealed class SetStateAction : GameAction
    {
        public TargetSelector target = TargetSelector.Self();
        [Required] public string state = "";
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.SetState(state));
    }

    [Serializable, JsonType("playAnimation")]
    [Doc("Plays an animation state on entities.")]
    public sealed class PlayAnimationAction : GameAction
    {
        public TargetSelector target = TargetSelector.Self();
        [Required] public string state = "";
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.Animation.Play(state));
    }

    [Serializable, JsonType("setAnimParam")]
    [Doc("Sets an animator parameter on entities (bools use value 0/1; triggers ignore value).")]
    public sealed class SetAnimParamAction : GameAction
    {
        public TargetSelector target = TargetSelector.Self();
        [Required] public string param = "";
        public float value = 1f;
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.Animation.SetValue(param, value));
    }
}
