using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Runs an <see cref="AnimatorDef"/>: plays sprite-sheet clips, evaluates parameter transitions
    /// (with exit times), and reports frame events. It is driven by game time and its whole state is
    /// saved for rewind, unlike Unity's Animator.
    /// </summary>
    public sealed class EntityAnimation
    {
        sealed class Clip
        {
            public string Name;
            public Sprite[] Frames;
            public float Fps;
            public bool Loop;
            public AnimEventDef[] Events;
        }

        sealed class Transition
        {
            public int From;   // -1 = any state
            public int To;
            public bool HasExitTime;
            public float ExitTime;
            public AnimConditionDef[] Conditions;
        }

        readonly Clip[] clips;
        readonly Transition[] transitions;
        readonly Dictionary<string, int> clipIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<string, int> paramIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly AnimParameterKind[] paramKinds;
        readonly float[] paramDefaults;
        readonly float[] parameters;
        readonly int defaultClip;
        readonly SpriteRenderer renderer;
        readonly Action<string> onEvent;

        // Pulsed bools are true for exactly one tick.
        readonly List<int> pulses = new List<int>();

        int current = -1;
        float stateTime;
        int lastFrameShown = -1;
        int framesProcessed;

        public bool HasClips => clips.Length > 0;
        public string CurrentState => current >= 0 ? clips[current].Name : "";

        /// <summary>The current clip doesn't loop and has played to its last frame.</summary>
        public bool IsDone
        {
            get
            {
                if (current < 0) return true;
                var clip = clips[current];
                return !clip.Loop && clip.Frames.Length > 0 && stateTime * clip.Fps >= clip.Frames.Length;
            }
        }

        public EntityAnimation(AnimatorDef def, ISpriteSheetSource sheets, SpriteRenderer renderer, Action<string> onEvent)
        {
            this.renderer = renderer;
            this.onEvent = onEvent;

            var states = def?.states ?? new List<AnimStateDef>();
            clips = new Clip[states.Count];
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                clips[i] = new Clip
                {
                    Name = s.stateName,
                    Frames = AnimationStateConfig.SelectFrames(Frames(sheets, s.spriteSheet, s.spriteNameFilter), s.firstFrame, s.frameCount).ToArray(),
                    Fps = Mathf.Max(0.01f, s.fps * (s.speed <= 0f ? 1f : s.speed)),
                    Loop = s.loop,
                    Events = s.events?.ToArray() ?? Array.Empty<AnimEventDef>(),
                };
                clipIndex[s.stateName] = i;
            }

            var ps = def?.parameters ?? new List<AnimParameterDef>();
            paramKinds = new AnimParameterKind[ps.Count];
            paramDefaults = new float[ps.Count];
            parameters = new float[ps.Count];
            for (int i = 0; i < ps.Count; i++)
            {
                paramIndex[ps[i].name] = i;
                paramKinds[i] = ps[i].type;
                paramDefaults[i] = ps[i].defaultValue;
            }

            var ts = def?.transitions ?? new List<AnimTransitionDef>();
            var list = new List<Transition>();
            foreach (var t in ts)
            {
                bool any = t.fromState == "Any" || t.fromState == "AnyState";
                if (!clipIndex.TryGetValue(t.toState, out int to)) continue;
                int from = -1;
                if (!any && !clipIndex.TryGetValue(t.fromState, out from)) continue;
                list.Add(new Transition { From = any ? -1 : from, To = to, HasExitTime = t.hasExitTime, ExitTime = t.exitTime, Conditions = t.conditions?.ToArray() ?? Array.Empty<AnimConditionDef>() });
            }
            transitions = list.ToArray();

            defaultClip = 0;
            if (def != null && !string.IsNullOrEmpty(def.defaultStateName) && clipIndex.TryGetValue(def.defaultStateName, out int d))
                defaultClip = d;
        }

        static Sprite[] Frames(ISpriteSheetSource sheets, Texture2D sheet, string filter)
        {
            if (sheet == null || sheets == null)
                return Array.Empty<Sprite>();
            var all = sheets.GetSprites(sheet);
            if (string.IsNullOrEmpty(filter))
                return all;
            var filtered = new List<Sprite>();
            foreach (var s in all)
            {
                if (s != null && s.name.StartsWith(filter, StringComparison.Ordinal))
                    filtered.Add(s);
            }
            return filtered.ToArray();
        }

        public void Reset()
        {
            Array.Copy(paramDefaults, parameters, parameters.Length);
            pulses.Clear();
            current = -1;
            if (clips.Length > 0)
                Enter(defaultClip);
        }

        // ─── Parameters ───────────────────────────────────────────────

        public bool HasParameter(string name) => paramIndex.ContainsKey(name);

        public void SetBool(string name, bool value) => SetValue(name, value ? 1f : 0f);

        public void SetValue(string name, float value)
        {
            if (paramIndex.TryGetValue(name, out int i))
                parameters[i] = paramKinds[i] == AnimParameterKind.Int ? Mathf.Round(value) : value;
        }

        public void SetTrigger(string name) => SetValue(name, 1f);

        /// <summary>A trigger, or a bool that is true for this tick only (how the old enemy scripts started attacks).</summary>
        public void Pulse(string name)
        {
            if (!paramIndex.TryGetValue(name, out int i))
                return;
            parameters[i] = 1f;
            if (paramKinds[i] == AnimParameterKind.Bool)
                pulses.Add(i);
        }

        public void Play(string state)
        {
            if (clipIndex.TryGetValue(state, out int i))
                Enter(i);
        }

        // ─── Playback ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (current < 0)
                return;

            stateTime += dt;
            FireEvents();
            EvaluateTransitions();
            ShowFrame();

            foreach (int p in pulses)
                parameters[p] = 0f;
            pulses.Clear();
        }

        void Enter(int index)
        {
            current = index;
            stateTime = 0f;
            framesProcessed = 0;
            lastFrameShown = -1;
            ShowFrame();
            FireEvents();
        }

        void FireEvents()
        {
            var clip = clips[current];
            int count = clip.Frames.Length;
            if (count == 0)
                return;
            // Every frame index reached since the last tick, across loops; capped to stop runaway catch-up.
            int reached = Mathf.FloorToInt(stateTime * clip.Fps) + 1;
            if (!clip.Loop)
                reached = Mathf.Min(reached, count);
            int guard = 0;
            while (framesProcessed < reached && guard++ < 64)
            {
                int frame = framesProcessed % count;
                foreach (var e in clip.Events)
                {
                    if (e.frame == frame)
                        onEvent?.Invoke(e.functionName);
                }
                framesProcessed++;
            }
            framesProcessed = Mathf.Max(framesProcessed, reached);
        }

        void EvaluateTransitions()
        {
            var clip = clips[current];
            float normalized = clip.Frames.Length > 0 ? stateTime * clip.Fps / clip.Frames.Length : 1f;
            foreach (var t in transitions)
            {
                if (t.From != current && t.From != -1)
                    continue;
                if (t.From == -1 && t.To == current)
                    continue;
                if (t.HasExitTime && normalized < t.ExitTime)
                    continue;
                if (!ConditionsMet(t))
                    continue;
                ConsumeTriggers(t);
                Enter(t.To);
                return;
            }
        }

        bool ConditionsMet(Transition t)
        {
            foreach (var c in t.Conditions)
            {
                if (!paramIndex.TryGetValue(c.parameterName, out int i))
                    return false;
                float v = parameters[i];
                switch (c.mode)
                {
                    case AnimConditionMode.If: if (v < 0.5f) return false; break;
                    case AnimConditionMode.IfNot: if (v >= 0.5f) return false; break;
                    case AnimConditionMode.Greater: if (!(v > c.threshold)) return false; break;
                    case AnimConditionMode.Less: if (!(v < c.threshold)) return false; break;
                    case AnimConditionMode.Equals: if (Mathf.Round(v) != Mathf.Round(c.threshold)) return false; break;
                    case AnimConditionMode.NotEqual: if (Mathf.Round(v) == Mathf.Round(c.threshold)) return false; break;
                }
            }
            return true;
        }

        void ConsumeTriggers(Transition t)
        {
            foreach (var c in t.Conditions)
            {
                if (paramIndex.TryGetValue(c.parameterName, out int i) && paramKinds[i] == AnimParameterKind.Trigger)
                    parameters[i] = 0f;
            }
        }

        void ShowFrame()
        {
            if (renderer == null || current < 0)
                return;
            var clip = clips[current];
            int count = clip.Frames.Length;
            if (count == 0)
                return;
            int frame = Mathf.FloorToInt(stateTime * clip.Fps);
            frame = clip.Loop ? ((frame % count) + count) % count : Mathf.Clamp(frame, 0, count - 1);
            if (frame == lastFrameShown)
                return;
            lastFrameShown = frame;
            renderer.sprite = clip.Frames[frame];
        }

        /// <summary>A state's frames and frame rate, for playing it outside the animator (the death effect plays "Die").</summary>
        public bool TryGetClip(string state, out Sprite[] frames, out float fps)
        {
            frames = null;
            fps = 0f;
            if (!clipIndex.TryGetValue(state, out int i) || clips[i].Frames.Length == 0)
                return false;
            frames = clips[i].Frames;
            fps = clips[i].Fps;
            return true;
        }

        /// <summary>The first frame of the default clip, for the template's initial look.</summary>
        public Sprite FirstSprite => clips.Length > 0 && clips[defaultClip].Frames.Length > 0 ? clips[defaultClip].Frames[0] : null;

        // ─── Rewind ───────────────────────────────────────────────────

        public void SaveState(StateWriter writer)
        {
            writer.Int(current);
            writer.Float(stateTime);
            writer.Int(framesProcessed);
            writer.Int(parameters.Length);
            foreach (float p in parameters)
                writer.Float(p);
        }

        public void LoadState(StateReader reader)
        {
            current = reader.Int();
            stateTime = reader.Float();
            framesProcessed = reader.Int();
            int n = reader.Int();
            for (int i = 0; i < n; i++)
            {
                float v = reader.Float();
                if (i < parameters.Length)
                    parameters[i] = v;
            }
            pulses.Clear();
            lastFrameShown = -1;
            ShowFrame();
        }
    }

    /// <summary>Gives the sprites cut from a sprite sheet texture, in name order.</summary>
    public interface ISpriteSheetSource
    {
        Sprite[] GetSprites(Texture2D sheet);
    }
}
