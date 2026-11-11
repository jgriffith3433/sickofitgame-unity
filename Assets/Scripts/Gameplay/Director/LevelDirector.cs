using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>What the director asks of the level scene: finishing, failing, and leaving the level.</summary>
    public interface ILevelHost
    {
        void CompleteLevel();
        /// <summary>A rewind went back to before the level was completed.</summary>
        void CancelCompletion();
        void FailLevel();
        void GoToLevel(int levelNumber);
        void PlayVideo(int videoNumber);
        void ReturnToMenu();
    }

    /// <summary>
    /// Runs a <see cref="LevelScript"/>: phases, rules, sequences, hazards, staggered waves, spawn modifiers,
    /// and the objective. Its state is saved with the world's for rewind; anything it starts refers back to
    /// the action that started it by index, so a snapshot holds only numbers and strings.
    /// </summary>
    public sealed partial class LevelDirector : IRandomSource
    {
        sealed class RuleRuntime
        {
            public RuleDef Def;
            public Trigger Trigger;
            public bool WasTrue;
            public int Fired;
        }

        sealed class PhaseRuntime
        {
            public PhaseDef Def;
            public RuleRuntime[] Rules;
        }

        struct SequenceRun
        {
            public int Action;      // RunSequenceAction index
            public int Step;
            public float WaitUntil;
            public string Scope;
            public int SelfSerial;  // -1 = none
            public int Repeat;
        }

        struct HazardRun
        {
            public int Action;      // StartHazardAction index
            public float StartedAt;
            public float NextAt;
            public float EndAt;     // +inf = until stopped
            public int Volley;
            public string Scope;
        }

        struct WaveRun
        {
            public int Action;      // SpawnWaveAction index
            public int Spawned;
            public float NextAt;
            public int Repeat;
            public string Scope;
        }

        struct SpawnModifierRun
        {
            public int Action;      // ModifierAction index
            public int Repeat;
            public float Until;
            public string Scope;
        }

        readonly GameplayWorld world;
        readonly LevelScript script;
        public ILevelHost Host { get; }

        readonly RuleRuntime[] globalRules;
        readonly PhaseRuntime[] phases;
        readonly Trigger completeTrigger;
        readonly Trigger failTrigger;
        readonly List<GameAction> actions = new List<GameAction>();
        readonly Dictionary<GameAction, int> actionIndex = new Dictionary<GameAction, int>(ReferenceComparer.Instance);
        readonly TriggerContext triggers = new TriggerContext();

        // ─── Saved state ──────────────────────────────────────────────
        DeterministicRandom random;
        int phase = -1;
        float phaseStart;
        float levelStart;
        bool completed;
        bool failed;
        string skipPhase = "";
        string skipSignal = "";
        readonly List<SequenceRun> sequences = new List<SequenceRun>();
        readonly List<HazardRun> hazards = new List<HazardRun>();
        readonly List<WaveRun> waves = new List<WaveRun>();
        readonly List<SpawnModifierRun> spawnModifiers = new List<SpawnModifierRun>();

        int pendingPhase = -1;
        bool insideTick;

        /// <summary>The beat running now (Docs/beats.md). Presentation-side state, not saved: a beat locks rewind.</summary>
        public readonly BeatRuntime Beats = new BeatRuntime();

        public string PhaseName => phase >= 0 && phase < phases.Length ? phases[phase].Def.name : "";
        public bool Completed => completed;
        public LevelScript Script => script;

        public LevelDirector(GameplayWorld world, LevelScript script, ILevelHost host)
        {
            this.world = world;
            this.script = script;
            Host = host;
            random = DeterministicRandom.Derive(world.Seed, 0xD1EC7u);

            globalRules = MakeRules(script.rules);
            phases = new PhaseRuntime[script.phases.Count];
            for (int i = 0; i < phases.Length; i++)
                phases[i] = new PhaseRuntime { Def = script.phases[i], Rules = MakeRules(script.phases[i].rules) };
            completeTrigger = script.objective?.complete?.Fresh();
            failTrigger = script.objective?.fail?.Fresh();

            IndexActions(world.Content);
        }

        static RuleRuntime[] MakeRules(List<RuleDef> rules)
        {
            var result = new RuleRuntime[rules.Count];
            for (int i = 0; i < rules.Count; i++)
                result[i] = new RuleRuntime { Def = rules[i], Trigger = rules[i].when?.Fresh() };
            return result;
        }

        /// <summary>Numbers every action that can start something long-running, so snapshots can refer to them.</summary>
        void IndexActions(GameContent content)
        {
            var found = new List<GameAction>();
            ContentWalker.CollectActions(script, found);
            foreach (var id in SortedKeys(content.Sequences))
                ContentWalker.CollectActions(content.Sequences[id], found);
            foreach (var id in SortedKeys(content.Entities))
                ContentWalker.CollectActions(content.Entities[id], found);
            foreach (var id in SortedKeys(content.Circles))
                ContentWalker.CollectActions(content.Circles[id], found);
            foreach (var a in found)
            {
                if (!actionIndex.ContainsKey(a))
                {
                    actionIndex[a] = actions.Count;
                    actions.Add(a);
                }
            }
        }

        static List<string> SortedKeys<T>(Dictionary<string, T> dictionary)
        {
            var keys = new List<string>(dictionary.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        /// <summary>A stable number for an action (used to key once-per-level rewards).</summary>
        public int IndexOfAction(GameAction action) => IndexOf(action);

        int IndexOf(GameAction action)
        {
            if (actionIndex.TryGetValue(action, out int i))
                return i;
            // Actions created at runtime (rare) are appended; they survive rewinds within this level.
            actionIndex[action] = actions.Count;
            actions.Add(action);
            return actions.Count - 1;
        }

        // =====================================================================
        // RUN
        // =====================================================================

        public void Start(float now)
        {
            levelStart = now;
            foreach (var c in script.counters)
                world.SetCounter(c.name, c.value);
            if (!string.IsNullOrEmpty(script.objective?.text))
                world.Hud.SetObjective(script.objective.text);

            UpdateTriggerContext(now, now);
            foreach (var rule in globalRules)
                rule.Trigger?.Reset(triggers);
            completeTrigger?.Reset(triggers);
            failTrigger?.Reset(triggers);

            if (phases.Length > 0)
                EnterPhase(0, now);
        }

        void UpdateTriggerContext(float now, float scopeStart)
        {
            triggers.World = world;
            triggers.Self = null;
            triggers.Director = this;
            triggers.Now = now;
            triggers.ScopeStart = scopeStart;
            triggers.Random = this;
        }

        public void Tick(float now)
        {
            insideTick = true;
            try
            {
                if (!completed && !failed)
                    CheckObjective(now);

                UpdateTriggerContext(now, levelStart);
                RunRules(globalRules, now, null);

                if (pendingPhase < 0 && phase >= 0)
                {
                    UpdateTriggerContext(now, phaseStart);
                    RunRules(phases[phase].Rules, now, PhaseName);
                }
                ApplyPendingPhase(now);

                AdvanceSequences(now);
                ApplyPendingPhase(now);
                PollSkip(now);
                PollBeat(now);
                FireHazards(now);
                AdvanceWaves(now);
                ExpireSpawnModifiers(now);
                TickGenerator(now);
            }
            finally
            {
                insideTick = false;
            }
        }

        /// <summary>Arms allowSkip: a tap or key jumps to phase and/or emits signal.</summary>
        public void ArmSkip(string phaseName, string signal)
        {
            skipPhase = phaseName ?? "";
            skipSignal = signal ?? "";
        }

        public void ClearSkip()
        {
            skipPhase = "";
            skipSignal = "";
        }

        void PollSkip(float now)
        {
            if (string.IsNullOrEmpty(skipPhase) && string.IsNullOrEmpty(skipSignal))
                return;
            if (!SkipPressed())
                return;
            string toPhase = skipPhase;
            string signal = skipSignal;
            ClearSkip();
            sequences.Clear();
            world.Telemetry.Record("cutscene", "skip", 0, string.IsNullOrEmpty(toPhase) ? signal : toPhase);
            if (!string.IsNullOrEmpty(signal))
                world.Emit(signal);
            if (!string.IsNullOrEmpty(toPhase))
                GoToPhase(toPhase);
            ApplyPendingPhase(now);
        }

        // ─── Beats (Docs/beats.md) ────────────────────────────────────

        public void BeginBeat(BeatStartAction action, ActionContext context)
        {
            if (Beats.Active)
                EndBeat(false, context.Now);
            Beats.Active = true;
            Beats.Id = action.id;
            Beats.Replay = BeatRuntime.HasSeen(action.id);
            Beats.StartedAt = context.Now;
            Beats.MaxSeconds = action.maxSeconds;
            Beats.UnlockAtEnd = action.unlockAtEnd;
            Beats.SkipPhase = action.skipPhase ?? "";
            Beats.OnSkip = action.onSkip;
            world.SetInputLocked(float.PositiveInfinity);
            world.SetRewindLocked(true);
            world.Hud.SetLetterbox(true, BeatLetterbox(Beats.Replay));
            world.Telemetry.Record("beat", "start", Beats.Replay ? 1 : 0, action.id);
            world.Emit("beat:" + action.id + ":start");
        }

        /// <summary>Presses in the first moments of a beat are the fight's last clicks, not a skip.</summary>
        const float BeatGrace = 0.4f;

        static float BeatLetterbox(bool replay) => GameplayHud.LetterboxSeconds * (replay ? BeatRuntime.ReplayScale : 1f);

        /// <summary>Ends the running beat: bars out, marked seen, input back if it asked for that.</summary>
        public void EndBeat(bool skipped, float now)
        {
            if (!Beats.Active)
                return;
            string id = Beats.Id;
            BeatRuntime.MarkSeen(id);
            Beats.Active = false;
            world.Hud.SetLetterbox(false, GameplayHud.LetterboxSeconds);
            world.Hud.HideBubble();
            if (Beats.UnlockAtEnd)
            {
                world.SetInputLocked(0f);
                world.SetRewindLocked(false);
            }
            world.Telemetry.Record("beat", skipped ? "skipped" : "end", Mathf.RoundToInt((now - Beats.StartedAt) * 100f), id);
            world.Emit("beat:" + id + ":end");
        }

        void PollBeat(float now)
        {
            if (!Beats.Active)
                return;
            bool pressed = world.Hud.TakeBeatPress() && now - Beats.StartedAt > BeatGrace;
            bool late = Beats.MaxSeconds > 0f && now - Beats.StartedAt >= Beats.MaxSeconds;
            if (!pressed && !late)
                return;
            var onSkip = Beats.OnSkip;
            string toPhase = Beats.SkipPhase;
            sequences.Clear();
            ClearSkip();
            var context = Context(now, null, 0, null);
            for (int i = 0; i < onSkip.Count; i++)
                world.RunAction(onSkip[i], context);
            EndBeat(true, now);
            if (!string.IsNullOrEmpty(toPhase))
                GoToPhase(toPhase);
            ApplyPendingPhase(now);
        }

        static bool SkipPressed()
        {
            // Escape / Enter / a tap or click: not WASD or Space (the bot keeps those pressed through the scene).
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
                               || kb.numpadEnterKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            if (pad != null && pad.startButton.wasPressedThisFrame)
                return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            return false;
        }

        void CheckObjective(float now)
        {
            UpdateTriggerContext(now, levelStart);
            if (completeTrigger != null && completeTrigger.Check(triggers))
                Complete();
            else if (failTrigger != null && failTrigger.Check(triggers))
                Fail();
        }

        void RunRules(RuleRuntime[] rules, float now, string scope)
        {
            foreach (var rule in rules)
            {
                if (rule.Trigger == null)
                    continue;
                if (rule.Def.maxTimes > 0 && rule.Fired >= rule.Def.maxTimes)
                    continue;
                bool value = rule.Trigger.Check(triggers);
                bool fire = rule.Trigger.EdgeTriggered ? value && !rule.WasTrue : value;
                rule.WasTrue = value;
                if (!fire)
                    continue;

                var context = Context(now, null, rule.Fired, scope);
                rule.Fired++;
                world.Telemetry.Record("rule", string.IsNullOrEmpty(rule.Def.name) ? scope ?? "global" : rule.Def.name, rule.Fired);
                world.RunActions(rule.Def.@do, context);
                if (pendingPhase >= 0 && scope != null)
                    return;
            }
        }

        ActionContext Context(float now, Entity self, int repeat, string scope)
        {
            return new ActionContext
            {
                World = world,
                Self = self,
                Director = this,
                Repeat = repeat,
                Scope = scope,
                Random = self != null ? self.Random : this,
                Now = now,
            };
        }

        // ─── Phases ───────────────────────────────────────────────────

        public void GoToPhase(string name)
        {
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].Def.name != name)
                    continue;
                pendingPhase = i;
                if (!insideTick)
                    ApplyPendingPhase(world.Now);
                return;
            }
            Debug.LogError($"[LevelDirector] No phase \"{name}\".");
        }

        void ApplyPendingPhase(float now)
        {
            while (pendingPhase >= 0)
            {
                int next = pendingPhase;
                pendingPhase = -1;
                EnterPhase(next, now);
            }
        }

        void EnterPhase(int index, float now)
        {
            if (phase >= 0)
            {
                string oldScope = PhaseName;
                sequences.RemoveAll(s => s.Scope == oldScope);
                hazards.RemoveAll(h => h.Scope == oldScope);
                waves.RemoveAll(w => w.Scope == oldScope);
                spawnModifiers.RemoveAll(m => m.Scope == oldScope);
                StopGeneratorInScope(oldScope);
                world.RunActions(phases[phase].Def.onExit, Context(now, null, 0, null));
            }

            phase = index;
            phaseStart = now;
            world.Telemetry.Record("phase", PhaseName, index);
            UpdateTriggerContext(now, now);
            foreach (var rule in phases[index].Rules)
            {
                rule.Trigger?.Reset(triggers);
                rule.WasTrue = false;
                rule.Fired = 0;
            }
            world.RunActions(phases[index].Def.onEnter, Context(now, null, 0, PhaseName));
        }

        // ─── Objective ────────────────────────────────────────────────

        public void Complete()
        {
            if (completed || failed)
                return;
            completed = true;
            world.Telemetry.Record("complete", world.LevelName, 0);
            Host?.CompleteLevel();
        }

        public void Fail()
        {
            if (completed || failed)
                return;
            failed = true;
            world.Telemetry.Record("fail", world.LevelName, 0);
            Host?.FailLevel();
        }

        // ─── Sequences ────────────────────────────────────────────────

        public void StartSequence(RunSequenceAction action, ActionContext context)
        {
            if (!string.IsNullOrEmpty(action.sequence) && !world.Content.Sequences.ContainsKey(action.sequence))
            {
                Debug.LogError($"[LevelDirector] No sequence \"{action.sequence}\".");
                return;
            }
            var run = new SequenceRun
            {
                Action = IndexOf(action),
                Step = 0,
                WaitUntil = context.Now,
                Scope = action.persist ? null : context.Scope,
                SelfSerial = context.Self != null ? context.Self.Serial : -1,
                Repeat = context.Repeat,
            };
            sequences.Add(run);
            // Run the first steps right away, so a sequence started this tick acts this tick.
            AdvanceSequence(sequences.Count - 1, context.Now);
        }

        List<GameAction> StepsOf(RunSequenceAction action)
        {
            if (!string.IsNullOrEmpty(action.sequence) && world.Content.Sequences.TryGetValue(action.sequence, out var def))
                return def.steps;
            return action.steps;
        }

        void AdvanceSequences(float now)
        {
            for (int i = 0; i < sequences.Count; i++)
            {
                if (AdvanceSequence(i, now))
                {
                    sequences.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>Runs due steps. Returns true when the sequence has finished.</summary>
        bool AdvanceSequence(int index, float now)
        {
            var run = sequences[index];
            var steps = StepsOf((RunSequenceAction)actions[run.Action]);
            Entity self = run.SelfSerial >= 0 ? world.FindBySerial(run.SelfSerial) : null;
            int guard = 0;
            while (run.Step < steps.Count && now >= run.WaitUntil && guard++ < 256)
            {
                var step = steps[run.Step++];
                if (step is WaitAction wait)
                {
                    run.WaitUntil = now + Mathf.Max(0f, wait.seconds) * Beats.Scale;
                    continue;
                }
                sequences[index] = run;
                var stepContext = Context(now, self, run.Repeat, run.Scope);
                world.RunAction(step, stepContext);
                if (index >= sequences.Count)
                    return false;
                run = sequences[index];
                if (step is ITimedStep timed)
                    run.WaitUntil = Mathf.Max(run.WaitUntil, now + timed.Hold(stepContext));
            }
            sequences[index] = run;
            return run.Step >= steps.Count && now >= run.WaitUntil;
        }

        // ─── Hazards ──────────────────────────────────────────────────

        public void StartHazard(StartHazardAction action, ActionContext context)
        {
            StopHazard(action.id);
            hazards.Add(new HazardRun
            {
                Action = IndexOf(action),
                StartedAt = context.Now,
                NextAt = context.Now + action.initialDelay,
                EndAt = action.duration > 0f ? context.Now + action.duration : float.PositiveInfinity,
                Volley = 0,
                Scope = action.persist ? null : context.Scope,
            });
        }

        public void StopHazard(string id)
        {
            hazards.RemoveAll(h => ((StartHazardAction)actions[h.Action]).id == id);
        }

        void FireHazards(float now)
        {
            for (int i = 0; i < hazards.Count; i++)
            {
                var h = hazards[i];
                var action = (StartHazardAction)actions[h.Action];
                if (now >= h.EndAt)
                {
                    hazards.RemoveAt(i);
                    i--;
                    continue;
                }
                int guard = 0;
                while (now >= h.NextAt && guard++ < 16)
                {
                    var context = Context(now, null, 0, h.Scope);
                    Vector2 origin = context.Resolve(action.origin, action.anchor);
                    Emission.Fire(world, null, action.projectile, action.pattern, action.aim, action.direction, origin,
                        action.speed, action.damage, h.Volley++, this);
                    float rate = 1f / Mathf.Max(0.01f, action.interval) + action.rampPerSecond * (now - h.StartedAt);
                    h.NextAt += 1f / Mathf.Max(0.05f, rate);
                }
                hazards[i] = h;
            }
        }

        // ─── Staggered waves ──────────────────────────────────────────

        public void StartWave(SpawnWaveAction action, ActionContext context)
        {
            waves.Add(new WaveRun { Action = IndexOf(action), Spawned = 0, NextAt = context.Now, Repeat = context.Repeat, Scope = context.Scope });
            AdvanceWaves(context.Now);
        }

        void AdvanceWaves(float now)
        {
            for (int i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                var action = (SpawnWaveAction)actions[w.Action];
                while (w.Spawned < action.count && now >= w.NextAt)
                {
                    action.SpawnOne(Context(now, null, w.Repeat, w.Scope), w.Spawned);
                    w.Spawned++;
                    w.NextAt += action.interval;
                }
                if (w.Spawned >= action.count)
                {
                    waves.RemoveAt(i);
                    i--;
                }
                else
                {
                    waves[i] = w;
                }
            }
        }

        // ─── Spawn modifiers ──────────────────────────────────────────

        public void AddSpawnModifier(ModifierAction action, int repeat, float until, string scope)
        {
            spawnModifiers.Add(new SpawnModifierRun { Action = IndexOf(action), Repeat = repeat, Until = until, Scope = scope });
        }

        void ExpireSpawnModifiers(float now)
        {
            spawnModifiers.RemoveAll(m => now >= m.Until);
        }

        /// <summary>Adds active "includeFuture" modifiers that select this entity type to its spawn options.</summary>
        public void CollectSpawnModifiers(EntityDef def, SpawnOptions options)
        {
            foreach (var run in spawnModifiers)
            {
                var action = (ModifierAction)actions[run.Action];
                var s = action.target;
                bool match = s.all
                    || (!string.IsNullOrEmpty(s.entity) && s.entity == def.id)
                    || (!string.IsNullOrEmpty(s.tag) && (def.tags.Contains(s.tag) || options.Tags.Contains(s.tag)))
                    || (!string.IsNullOrEmpty(s.id) && s.id == options.InstanceId);
                if (!match)
                    continue;
                foreach (var m in action.modifiers)
                    options.Modifiers.Add((m.stat, m.MultiplyFor(run.Repeat), m.add));
            }
        }

        // =====================================================================
        // IRandomSource
        // =====================================================================

        public float Value() => random.Value();
        public float Range(float min, float max) => random.Range(min, max);
        public int Range(int min, int max) => random.Range(min, max);

        // =====================================================================
        // REWIND
        // =====================================================================

        public void SaveState(StateWriter w)
        {
            w.UInt(random.State);
            w.Int(phase);
            w.Float(phaseStart);
            w.Float(levelStart);
            w.Bool(completed);
            w.Bool(failed);
            w.String(skipPhase);
            w.String(skipSignal);

            SaveRules(w, globalRules);
            foreach (var p in phases)
                SaveRules(w, p.Rules);
            completeTrigger?.SaveState(w);
            failTrigger?.SaveState(w);

            w.Int(sequences.Count);
            foreach (var s in sequences)
            {
                w.Int(s.Action); w.Int(s.Step); w.Float(s.WaitUntil); w.String(s.Scope); w.Int(s.SelfSerial); w.Int(s.Repeat);
            }
            w.Int(hazards.Count);
            foreach (var h in hazards)
            {
                w.Int(h.Action); w.Float(h.StartedAt); w.Float(h.NextAt); w.Float(h.EndAt); w.Int(h.Volley); w.String(h.Scope);
            }
            w.Int(waves.Count);
            foreach (var x in waves)
            {
                w.Int(x.Action); w.Int(x.Spawned); w.Float(x.NextAt); w.Int(x.Repeat); w.String(x.Scope);
            }
            w.Int(spawnModifiers.Count);
            foreach (var m in spawnModifiers)
            {
                w.Int(m.Action); w.Int(m.Repeat); w.Float(m.Until); w.String(m.Scope);
            }
            SaveGenerator(w);
        }

        static void SaveRules(StateWriter w, RuleRuntime[] rules)
        {
            foreach (var r in rules)
            {
                w.Bool(r.WasTrue);
                w.Int(r.Fired);
                r.Trigger?.SaveState(w);
            }
        }

        public void LoadState(StateReader r)
        {
            bool wasCompleted = completed;
            random.State = r.UInt();
            phase = r.Int();
            phaseStart = r.Float();
            levelStart = r.Float();
            completed = r.Bool();
            failed = r.Bool();
            skipPhase = r.String();
            skipSignal = r.String();

            LoadRules(r, globalRules);
            foreach (var p in phases)
                LoadRules(r, p.Rules);
            completeTrigger?.LoadState(r);
            failTrigger?.LoadState(r);

            sequences.Clear();
            int n = r.Int();
            for (int i = 0; i < n; i++)
                sequences.Add(new SequenceRun { Action = r.Int(), Step = r.Int(), WaitUntil = r.Float(), Scope = r.String(), SelfSerial = r.Int(), Repeat = r.Int() });
            hazards.Clear();
            n = r.Int();
            for (int i = 0; i < n; i++)
                hazards.Add(new HazardRun { Action = r.Int(), StartedAt = r.Float(), NextAt = r.Float(), EndAt = r.Float(), Volley = r.Int(), Scope = r.String() });
            waves.Clear();
            n = r.Int();
            for (int i = 0; i < n; i++)
                waves.Add(new WaveRun { Action = r.Int(), Spawned = r.Int(), NextAt = r.Float(), Repeat = r.Int(), Scope = r.String() });
            spawnModifiers.Clear();
            n = r.Int();
            for (int i = 0; i < n; i++)
                spawnModifiers.Add(new SpawnModifierRun { Action = r.Int(), Repeat = r.Int(), Until = r.Float(), Scope = r.String() });
            LoadGenerator(r);
            pendingPhase = -1;

            if (wasCompleted && !completed)
                Host?.CancelCompletion();
        }

        static void LoadRules(StateReader r, RuleRuntime[] rules)
        {
            foreach (var rule in rules)
            {
                rule.WasTrue = r.Bool();
                rule.Fired = r.Int();
                rule.Trigger?.LoadState(r);
            }
        }

        sealed class ReferenceComparer : IEqualityComparer<GameAction>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(GameAction a, GameAction b) => ReferenceEquals(a, b);
            public int GetHashCode(GameAction a) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a);
        }
    }
}
