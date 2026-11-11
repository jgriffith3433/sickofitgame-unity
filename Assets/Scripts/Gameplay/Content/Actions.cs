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
        [Doc("Place each spawn this far outside the view edge nearest its spot (areaCenter/points still pick which edge and where along it), so it is never visible when it appears. " +
             "Fliers enter from the side; walkers drop in from above the view, never beyond the Travel barriers. 0 = spawn where the spot says.")]
        public float edgeMargin;

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
            Vector2 at = context.Resolve(local, anchor);
            if (edgeMargin > 0f)
                at = ViewEdge.Place(context.World, entity, at, edgeMargin);
            context.World.Spawn(entity, at, options);
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

    [Serializable, JsonType("allowSkip")]
    [Doc("While armed, a tap or key (or Escape) ends the current sequences and jumps to 'phase' and/or emits 'signal'. " +
         "Use it on a lockInput scene so one press skips the rest (Level 1's portal). Cleared on unlockInput or when it fires.")]
    public sealed class AllowSkipAction : GameAction
    {
        [PhaseRef, Doc("Phase to enter when skipped (empty = stay).")] public string phase = "";
        [Doc("Signal emitted when skipped (empty = none).")] public string signal = "";
        public override void Run(ActionContext context) => context.Director.ArmSkip(phase, signal);
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
            // For the playtest's "text only" count: a caption with a voice (Value 1) or without.
            if (!string.IsNullOrEmpty(text))
                context.World.Telemetry.Record("caption", text, string.IsNullOrEmpty(sound) ? 0 : 1, style + "|" + sound);
        }
    }

    [Serializable, JsonType("hintOnce")]
    [Doc("The hero sizes up an enemy type the first time it spawns in a save: its displayName and hint as a subtitle (and its hintVoice), " +
         "once. Put it in an enemy's onSpawn (self) or name an 'entity' in a level rule. Remembered in the save (EnemyHints) so a death, " +
         "a rewind or a new run does not repeat it; New Game forgets. Silent, and not used up, during a scene, while input or rewind is " +
         "locked by a script, and while rewinding.")]
    public sealed class HintOnceAction : GameAction
    {
        [EntityRef, Doc("The entity type to talk about (empty = the entity running this).")] public string entity = "";
        [Doc("Seconds the subtitle stays up (at least as long as the voice line).")] public float duration = 3.5f;

        public override void Run(ActionContext context)
        {
            var world = context.World;
            EntityDef def = null;
            if (string.IsNullOrEmpty(entity))
                def = context.Self != null ? context.Self.Def : null;
            else
                world.Content.Entities.TryGetValue(entity, out def);
            if (def == null || string.IsNullOrEmpty(def.displayName))
                return;
            if (!string.IsNullOrEmpty(world.SceneId) || world.RewindLocked || context.Now < world.InputLockedUntil
                || (SickOfIt.Core.GameClock.Instance != null && SickOfIt.Core.GameClock.Instance.IsRewinding))
                return;
            if (!SickOfIt.Data.EnemyHints.Add(def.displayName))
                return;

            string text = string.IsNullOrEmpty(def.hint) ? def.displayName.ToUpperInvariant() : def.displayName.ToUpperInvariant() + ": " + def.hint;
            bool voiced = !string.IsNullOrEmpty(def.hintVoice) && SickOfIt.Core.Audio.Sfx.Has(def.hintVoice);
            float shown = voiced ? Mathf.Max(duration, SickOfIt.Core.Audio.Sfx.LengthOf(def.hintVoice) + 0.4f) : duration;
            world.Hud.ShowText(text, TextStyle.Subtitle, Color.white, context.Now + shown);
            if (voiced)
                SickOfIt.Core.Audio.Sfx.Play(def.hintVoice);
            world.Telemetry.Record("hint", def.displayName, 0, text);
        }
    }

    [Serializable, JsonType("storyBeat")]
    [Doc("A floor's arrival moment, lived once: runs 'then' (a shake, a stinger, a hero reaction, a voiced line, an event; put waits inside a runSequence) the first " +
         "time it is reached in a save and does nothing on a retry, a death, a rewind or a new run. Never locks input itself. Remembered in StoryBeats; New Game forgets. " +
         "Logs a 'storyBeat' telemetry event (subject: id, detail: 'event|voice') the playtest reads. Silent, and not used up, during a scene and while rewinding.")]
    public sealed class StoryBeatAction : GameAction
    {
        [Required, Doc("Unique id of the beat (the floor's).")] public string id = "";
        [Doc("What happens on screen, for the log (e.g. 'landingSlam').")] public string eventName = "";
        [SoundRef, Doc("The voiced line (audio.json id) said inside 'then', for the log.")] public string voice = "";
        [Doc("What the beat does.")] public List<GameAction> then = new List<GameAction>();

        public override void Run(ActionContext context)
        {
            var world = context.World;
            if (!string.IsNullOrEmpty(world.SceneId)
                || (SickOfIt.Core.GameClock.Instance != null && SickOfIt.Core.GameClock.Instance.IsRewinding))
                return;
            if (!SickOfIt.Data.StoryBeats.Add(id))
            {
                world.Telemetry.Record("storyBeatSkipped", id, 0, "already lived");
                return;
            }
            world.Telemetry.Record("storyBeat", id, 1, eventName + "|" + voice);
            world.RunActions(then, context);
        }

        public override void CollectChildren(List<GameAction> into) => into.AddRange(then);
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
    [Doc("Switches the level music to this track, from the top (a Warden's theme, its enrage). It stays until the next playMusic; " +
         "a rewind past it brings back the track before. Floor stages' push music doesn't override it.")]
    public sealed class PlayMusicAction : GameAction
    {
        public AudioClip clip;
        public float volume = 1f;
        public override void Run(ActionContext context) => context.World.Effects.PlayMusic(clip, volume);
    }

    [Serializable, JsonType("setAmbience")]
    [Doc("Cross-fades the level's ambient bed to this audio.json sound (kind Effect, loop true, bus Ambience): the Meadow's calm bed " +
         "to its burning one when the sky opens. Presentation only (the Ambience bus, the Effects volume): a rewind past it keeps the new bed.")]
    public sealed class SetAmbienceAction : GameAction
    {
        [SoundRef, Required, Doc("audio.json sound id of the looping bed.")] public string sound = "";
        [Doc("Seconds of cross-fade.")] public float fadeSeconds = 2f;
        public override void Run(ActionContext context) => SickOfIt.Core.Audio.AmbienceBed.PlaySound(sound, fadeSeconds);
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
        public override void Run(ActionContext context)
        {
            context.Director.ClearSkip();
            context.World.SetInputLocked(0f);
        }
    }

    // =====================================================================
    // SCENES: directing the hero (input and rewind usually locked)
    // =====================================================================

    /// <summary>Where an instance id ("id" given by spawn) is now, if that entity is alive.</summary>
    static class SceneTargets
    {
        public static bool TryFind(ActionContext context, string id, out Entity entity)
        {
            entity = null;
            if (string.IsNullOrEmpty(id))
                return false;
            if (id == "player")
            {
                entity = context.World.Player;
                return entity != null;
            }
            if (id == "self")
            {
                entity = context.Self;
                return entity != null;
            }
            foreach (var e in context.World.Live)
            {
                if (e.IsAlive && e.InstanceId == id)
                {
                    entity = e;
                    return true;
                }
            }
            if (context.World.Telemetry != null)
                context.World.Telemetry.Record("error", "scene target", 0, $"no live entity with id \"{id}\"");
            return false;
        }
    }

    [Serializable, JsonType("walkPlayerTo")]
    [Doc("Walks the player to a point by himself, with his own Walk clip and turning, even while input is locked (lockInput). " +
         "He stops there (or after 1.5 s without getting closer, e.g. against a wall) and 'emit' fires.")]
    public sealed class WalkPlayerToAction : GameAction
    {
        [Doc("Where to stop (world x), measured from 'anchor'; with 'target', an offset from that entity.")] public float x;
        public Anchor anchor = Anchor.World;
        [Doc("Instance id (spawn 'id') of an entity to walk up to, instead of a fixed x.")] public string target = "";
        [Doc("Fraction of his normal running speed (1 = full speed, 0.5 = a stroll).")] public float speed = 0.6f;
        [Doc("Signal emitted when he arrives.")] public string emit = "";

        public override void Run(ActionContext context)
        {
            float to;
            if (!string.IsNullOrEmpty(target))
            {
                if (!SceneTargets.TryFind(context, target, out var entity))
                    return;
                to = entity.Position.x + x;
            }
            else
            {
                to = context.Resolve(new Vector2(x, 0f), anchor).x;
            }
            context.World.Scripted.WalkTo(to, speed, emit, context.Now);
        }
    }

    [Serializable, JsonType("facePlayer")]
    [Doc("Turns the player left or right, or toward an entity. Moving or firing turns him again.")]
    public sealed class FacePlayerAction : GameAction
    {
        public FaceDirection direction = FaceDirection.Right;
        [Doc("Instance id of an entity to face instead of 'direction'.")] public string target = "";

        public override void Run(ActionContext context)
        {
            var controller = context.World.PlayerController;
            if (controller == null || context.World.Player == null)
                return;
            float dir = direction == FaceDirection.Right ? 1f : -1f;
            if (!string.IsNullOrEmpty(target))
            {
                if (!SceneTargets.TryFind(context, target, out var entity))
                    return;
                dir = entity.Position.x >= context.World.Player.Position.x ? 1f : -1f;
            }
            controller.Face(dir);
            context.World.Telemetry.Record("cutscene", "facePlayer", (int)dir, dir > 0f ? "right" : "left");
        }
    }

    [Serializable, JsonType("pullPlayerInto")]
    [Doc("Pulls the player into a point (a portal): gravity and collisions off, he slides into it spinning and shrinking, then " +
         "is gone (hidden, nothing can hit him) for the rest of the level. 'emit' fires when he disappears.")]
    public sealed class PullPlayerIntoAction : GameAction
    {
        [Doc("The point, measured from 'anchor' (used when 'target' is empty).")] public Vector2 at;
        public Anchor anchor = Anchor.World;
        [Doc("Instance id of an entity to be pulled into (followed if it moves).")] public string target = "";
        [Doc("Seconds from the first tug to gone.")] public float duration = 1.5f;
        [Doc("Degrees he turns on the way in (positive = clockwise; 720 = two turns).")] public float spin = 720f;
        [Doc("His size at the end, as a fraction of normal (0 = shrinks to nothing).")] public float endScale;
        [Doc("In = slow first tug, fast swallow.")] public Ease ease = Ease.In;
        [Doc("Signal emitted when he is gone.")] public string emit = "";

        public override void Run(ActionContext context)
        {
            Vector2 point;
            if (!string.IsNullOrEmpty(target))
            {
                if (!SceneTargets.TryFind(context, target, out var entity))
                    return;
                point = entity.Position;
            }
            else
            {
                point = context.Resolve(at, anchor);
            }
            context.World.Scripted.PullInto(target, point, duration, spin, endScale, ease, emit, context.Now);
        }
    }

    [Serializable, JsonType("playPlayerAnimation")]
    [Doc("Plays a state of the player's Animator by name (player.json animationConfig: Idle, Walk, Jump, Fall, Fire, and scene clips added there). " +
         "His own movement takes over again through the Animator's transitions.")]
    public sealed class PlayPlayerAnimationAction : GameAction
    {
        [Required] public string state = "";
        [Doc("Seconds to blend into it (0 = cut).")] public float crossFade;
        public override void Run(ActionContext context) => context.World.PlayPlayerAnimation(state, crossFade);
    }

    [Serializable, JsonType("lockRewind")]
    [Doc("No rewind until unlockRewind (a scene the hero can't undo): R and the HUD button do nothing, and the button says so.")]
    public sealed class LockRewindAction : GameAction
    {
        public override void Run(ActionContext context) => context.World.SetRewindLocked(true);
    }

    [Serializable, JsonType("unlockRewind")]
    [Doc("Allows rewinding again after lockRewind.")]
    public sealed class UnlockRewindAction : GameAction
    {
        public override void Run(ActionContext context) => context.World.SetRewindLocked(false);
    }

    [Serializable, JsonType("cameraTo")]
    [Doc("Moves the camera to a point (and zooms, with 'size') over 'duration' seconds. Static camera only (world.camera); cameraReset goes back.")]
    public sealed class CameraToAction : GameAction
    {
        [Doc("The new center, measured from 'anchor'.")] public Vector2 at;
        public Anchor anchor = Anchor.World;
        [Doc("Half the visible height in world units (the level's is 2.8; smaller = closer). 0 = keep the current zoom.")] public float size;
        public float duration = 1f;
        public Ease ease = Ease.InOut;
        public override void Run(ActionContext context) => SceneCamera.Move(context, context.Resolve(at, anchor), size, duration, ease);
    }

    [Serializable, JsonType("cameraZoom")]
    [Doc("Zooms the camera where it is to 'size' over 'duration' seconds. Static camera only; cameraReset goes back.")]
    public sealed class CameraZoomAction : GameAction
    {
        [Doc("Half the visible height in world units (the level's is 2.8; smaller = closer).")] public float size = 2f;
        public float duration = 1f;
        public Ease ease = Ease.InOut;
        public override void Run(ActionContext context) => SceneCamera.Move(context, context.World.Effects.CameraBase, size, duration, ease);
    }

    [Serializable, JsonType("cameraReset")]
    [Doc("Moves and zooms the camera back to the level's own view after cameraTo / cameraZoom.")]
    public sealed class CameraResetAction : GameAction
    {
        public float duration = 1f;
        public Ease ease = Ease.InOut;

        public override void Run(ActionContext context)
        {
            if (!context.World.Effects.ResetCamera(duration, ease, context.Now))
                SceneCamera.WarnNotStatic(context);
        }
    }

    static class SceneCamera
    {
        public static void Move(ActionContext context, Vector2 center, float size, float duration, Ease ease)
        {
            if (!context.World.Effects.MoveCamera(center, size, duration, ease, context.Now))
                WarnNotStatic(context);
        }

        public static void WarnNotStatic(ActionContext context)
        {
            context.World.Telemetry.Record("error", "camera", 0, $"camera actions need world.camera \"Static\" (this level is {context.World.Effects.Mode})");
        }
    }

    [Serializable, JsonType("fade")]
    [Doc("Fades the screen to black and holds it, or back to clear (to: Clear). Lines (showText) and the pause menu still show over it.")]
    public sealed class FadeAction : GameAction
    {
        public FadeTo to = FadeTo.Black;
        public float duration = 1f;
        [Doc("The color it fades to.")] public Color color = Color.black;

        public override void Run(ActionContext context)
        {
            context.World.Hud.Fade(to == FadeTo.Black, color, duration, context.Now);
            context.World.Telemetry.Record("cutscene", "fade", to == FadeTo.Black ? 1 : 0, to.ToString());
        }
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
    // SCENES: directing other entities (bystanders, enemies); the hero has his own actions above
    // =====================================================================

    [Serializable, JsonType("walkEntityTo")]
    [Doc("Walks any entity except the hero (for him: walkPlayerTo) sideways to a world x by itself, overriding its own movement abilities while it goes. " +
         "It faces the way it walks, stops there (or after 1.5 s without getting closer, e.g. against a wall) and 'emit' fires. Rewind-safe.")]
    public sealed class WalkEntityToAction : GameAction
    {
        [Required, Doc("Instance id (spawn 'id') of the entity; \"self\" inside an entity's own abilities.")] public string target = "";
        [Doc("Where to stop (world x), measured from 'anchor'; with 'relativeTo', an offset from that entity.")] public float x;
        public Anchor anchor = Anchor.World;
        [Doc("Instance id of an entity to walk to (\"player\" works): x is then an offset from it, negative = on its left.")] public string relativeTo = "";
        [Doc("World units per second (0 = the entity's own moveSpeed).")] public float speed = 1.5f;
        [Doc("Walk without turning to face the way it goes (backing away from something, or a flinch).")] public bool keepFacing;
        [Doc("Signal emitted when it arrives.")] public string emit = "";

        public override void Run(ActionContext context)
        {
            if (!SceneTargets.TryFind(context, target, out var entity))
                return;
            if (entity.IsPlayer)
            {
                context.World.Telemetry.Record("error", "walkEntityTo", 0, "the hero walks with walkPlayerTo");
                return;
            }
            float to;
            if (!string.IsNullOrEmpty(relativeTo))
            {
                if (!SceneTargets.TryFind(context, relativeTo, out var other))
                    return;
                to = other.Position.x + x;
            }
            else
            {
                to = context.Resolve(new Vector2(x, 0f), anchor).x;
            }
            entity.ScriptWalkTo(to, speed, emit, keepFacing);
            context.World.Telemetry.Record("cutscene", "walkEntityTo", Mathf.RoundToInt(to * 100f), target);
        }
    }

    [Serializable, JsonType("faceEntity")]
    [Doc("Turns any entity except the hero (for him: facePlayer) left or right, toward another entity or away from it. Its own abilities may turn it again (a hover or chase faces the hero).")]
    public sealed class FaceEntityAction : GameAction
    {
        [Required, Doc("Instance id (spawn 'id') of the entity; \"self\" inside an entity's own abilities.")] public string target = "";
        public FaceDirection direction = FaceDirection.Right;
        [Doc("Instance id of an entity to face (\"player\" works), instead of 'direction'.")] public string toward = "";
        [Doc("Instance id of an entity to turn away from (\"player\" works), instead of 'direction'.")] public string awayFrom = "";

        public override void Run(ActionContext context)
        {
            if (!SceneTargets.TryFind(context, target, out var entity))
                return;
            if (entity.IsPlayer)
            {
                context.World.Telemetry.Record("error", "faceEntity", 0, "the hero turns with facePlayer");
                return;
            }
            float dir = direction == FaceDirection.Right ? 1f : -1f;
            if (!string.IsNullOrEmpty(toward) || !string.IsNullOrEmpty(awayFrom))
            {
                bool away = string.IsNullOrEmpty(toward);
                if (!SceneTargets.TryFind(context, away ? awayFrom : toward, out var other))
                    return;
                dir = other.Position.x >= entity.Position.x ? 1f : -1f;
                if (away)
                    dir = -dir;
            }
            entity.SetFacing(dir);
            context.World.Telemetry.Record("cutscene", "faceEntity", (int)dir, target);
        }
    }

    [Serializable, JsonType("playEntityAnimation")]
    [Doc("Plays a state of an entity's animator by instance id (\"player\" works too: his Animator, see playPlayerAnimation). " +
         "A state the entity has no clip for is logged and skipped. The animator's own transitions take over when a non-looping clip ends.")]
    public sealed class PlayEntityAnimationAction : GameAction
    {
        [Required, Doc("Instance id (spawn 'id') of the entity; \"self\" inside an entity's own abilities.")] public string target = "";
        [Required, Doc("Animator state name, e.g. Hiccup.")] public string state = "";

        public override void Run(ActionContext context)
        {
            if (!SceneTargets.TryFind(context, target, out var entity))
                return;
            if (entity.IsPlayer)
            {
                context.World.PlayPlayerAnimation(state);
                return;
            }
            if (!entity.Animation.TryGetClip(state, out _, out _))
            {
                context.World.Telemetry.Record("error", "playEntityAnimation", 0, $"{target} has no animation state \"{state}\"");
                return;
            }
            entity.Animation.Play(state);
            entity.RecordNow();
            context.World.Telemetry.Record("cutscene", "playEntityAnimation", 0, $"{target}:{state}");
        }
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
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e =>
        {
            if (e.IsPlayer)
                context.World.PlayPlayerAnimation(state);
            else
                e.Animation.Play(state);
        });
    }

    [Serializable, JsonType("setAnimParam")]
    [Doc("Sets an animator parameter on entities (bools use value 0/1; triggers ignore value).")]
    public sealed class SetAnimParamAction : GameAction
    {
        public TargetSelector target = TargetSelector.Self();
        [Required] public string param = "";
        public float value = 1f;
        public override void Run(ActionContext context) => context.World.ForEachTarget(target, context.Self, e => e.Animation?.SetValue(param, value));
    }
}
