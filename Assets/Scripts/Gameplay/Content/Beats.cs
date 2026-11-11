using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>A sequence step that holds the sequence for a time of its own (a spoken line waits for its clip).</summary>
    public interface ITimedStep
    {
        float Hold(ActionContext context);
    }

    /// <summary>
    /// The state of the beat running now (a short framed in-engine scene, Docs/beats.md). Owned by the LevelDirector.
    /// Not saved: a beat locks rewind, and a rewound level never lands inside one.
    /// </summary>
    public sealed class BeatRuntime
    {
        /// <summary>Beats finished (played or skipped) in this session; a second play runs in replay mode.</summary>
        static readonly HashSet<string> Seen = new HashSet<string>();

        /// <summary>Replay mode: waits and camera moves take this fraction of their time, and spoken clips are not repeated.</summary>
        public const float ReplayScale = 0.4f;

        public bool Active;
        public string Id = "";
        public bool Replay;
        public float StartedAt;
        public float MaxSeconds;
        public bool UnlockAtEnd;
        public string SkipPhase = "";
        public List<GameAction> OnSkip = new List<GameAction>();

        /// <summary>1 normally, ReplayScale while a seen beat replays.</summary>
        public float Scale => Active && Replay ? ReplayScale : 1f;

        public static bool HasSeen(string id) => Seen.Contains(id ?? "");
        public static void MarkSeen(string id) { if (!string.IsNullOrEmpty(id)) Seen.Add(id); }
        public static void ForgetAll() => Seen.Clear();
    }

    [Serializable, JsonType("beatStart")]
    [Doc("Starts a beat, a short framed in-engine scene (Docs/beats.md): locks input and rewind, slides the letterbox bars in over 0.3 s " +
         "and arms the skip: any tap, click or key runs 'onSkip' (the end state, instantly) within a frame and ends the beat. " +
         "A beat seen before in this session replays shortened (waits x0.4, no repeated voice). Emits beat:<id>:start.")]
    public sealed class BeatStartAction : GameAction
    {
        [Required, Doc("Names the beat (its seen-flag and its signals).")] public string id = "";
        [Doc("Hard limit in seconds: a beat that is still running then is ended as if skipped.")] public float maxSeconds = 10f;
        [Doc("The end state as instant actions (positions, flags, spawns): run when a press or the hard limit ends the beat. " +
             "No waits and no runSequence in here.")] public List<GameAction> onSkip = new List<GameAction>();
        [PhaseRef, Doc("Phase entered after onSkip (empty = stay).")] public string skipPhase = "";
        [Doc("Hand input and rewind back when the beat ends (leave off when the level ends right after it).")] public bool unlockAtEnd = true;

        public override void Run(ActionContext context) => context.Director.BeginBeat(this, context);
        public override void CollectChildren(List<GameAction> into) => into.AddRange(onSkip);
    }

    [Serializable, JsonType("beatEnd")]
    [Doc("Ends the running beat the normal way: bars slide out, the beat is marked seen, input comes back if the beat asked for it. " +
         "Emits beat:<id>:end.")]
    public sealed class BeatEndAction : GameAction
    {
        public override void Run(ActionContext context) => context.Director.EndBeat(false, context.Now);
    }

    [Serializable, JsonType("letterbox")]
    [Doc("Slides the cinematic bars in or out (default 0.3 s), inside the safe area. beatStart / beatEnd do this by themselves; " +
         "use it to bring the bars in earlier or hold them across two beats.")]
    public sealed class LetterboxAction : GameAction
    {
        [Doc("true = bars in, false = out.")] public bool on = true;
        public float seconds = 0.3f;
        public override void Run(ActionContext context) => context.World.Hud.SetLetterbox(on, seconds * context.Director.Beats.Scale);
    }

    [Serializable, JsonType("cameraFocus")]
    [Doc("Beat camera: moves (pan) and/or zooms the Static camera to a world point or an actor (an instance id, 'player' too), then holds. " +
         "Shortened in a replay. cameraReset (or the level) takes it back.")]
    public sealed class CameraFocusAction : GameAction
    {
        [Doc("Instance id of an actor to focus on (empty = the point 'at'). 'at' is then an offset from the actor.")] public string target = "";
        [Doc("The point, measured from 'anchor'.")] public Vector2 at;
        public Anchor anchor = Anchor.World;
        [Doc("Half the visible height (the level's is 2.8; smaller = closer). 0 = keep the current zoom (a pure pan).")] public float size;
        [Doc("true = zoom only, stay where the camera is.")] public bool keepPosition;
        public float seconds = 0.6f;
        public Ease ease = Ease.InOut;

        public override void Run(ActionContext context)
        {
            Vector2 center = context.World.Effects.CameraBase;
            if (!keepPosition)
            {
                if (!string.IsNullOrEmpty(target))
                {
                    if (!SceneTargets.TryFind(context, target, out var entity))
                        return;
                    center = entity.Position + at;
                }
                else
                {
                    center = context.Resolve(at, anchor);
                }
            }
            SceneCamera.Move(context, center, size, seconds * context.Director.Beats.Scale, ease);
        }
    }

    [Serializable, JsonType("say")]
    [Doc("Beat line: an actor speaks. Plays the voiced clip, shows a speech bubble with one short caption above the speaker " +
         "(an instance id, 'player' too; or a point when no actor is named) and holds the sequence until the clip ends. " +
         "In a replay the clip is not repeated and the caption is brief.")]
    public sealed class SayAction : GameAction, ITimedStep
    {
        [Doc("Who speaks: instance id or 'player'. Empty = the bubble sits on the point 'at'.")] public string actor = "";
        public Vector2 at;
        public Anchor anchor = Anchor.World;
        [Required, Doc("One short line (about 40 characters at most).")] public string text = "";
        [SoundRef, Doc("audio.json sound spoken with it.")] public string sound = "";
        public Color color = Color.white;
        [Doc("The least the line holds the sequence, seconds.")] public float minSeconds = 0.7f;
        [Doc("Extra seconds after the clip.")] public float pad = 0.15f;

        public override void Run(ActionContext context)
        {
            bool replay = context.Director.Beats.Active && context.Director.Beats.Replay;
            int serial = -1;
            Vector2 point = Vector2.zero;
            if (!string.IsNullOrEmpty(actor))
            {
                if (SceneTargets.TryFind(context, actor, out var entity))
                    serial = entity.Serial;
            }
            else
            {
                point = context.Resolve(at, anchor) + new Vector2(0f, 0.4f);
            }
            float shown = Hold(context);
            context.World.Hud.ShowBubble(text, color, serial, point, context.Now + shown + 0.6f);
            if (!replay)
                SickOfIt.Core.Audio.Sfx.Play(sound);
            // The playtest's "text only" count: a caption with a voice (Value 1) or without.
            context.World.Telemetry.Record("caption", text, string.IsNullOrEmpty(sound) ? 0 : 1, "Bubble|" + sound);
        }

        public float Hold(ActionContext context)
        {
            bool replay = context.Director.Beats.Active && context.Director.Beats.Replay;
            if (replay)
                return 0.45f;
            return Mathf.Max(minSeconds, SickOfIt.Core.Audio.Sfx.LengthOf(sound) + pad);
        }
    }
}
