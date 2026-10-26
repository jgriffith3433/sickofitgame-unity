using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Describes a complete animator setup: parameters, states, transitions, and events.
    /// Used by AnimationGenerator to produce .controller + .anim assets.
    /// 
    /// Attach to EnemyTypeConfig or PlayerConfig via animationConfig field.
    /// The generator reads this to build everything from sprite sheets.
    /// </summary>
    [Serializable]
    public class AnimationConfig
    {
        [Header("Parameters")]
        [Tooltip("Animator parameters (bool, float, int, trigger)")]
        public List<AnimatorParameterConfig> parameters = new List<AnimatorParameterConfig>();

        [Header("States")]
        [Tooltip("Each state maps to one .anim clip built from a sprite sheet region")]
        public List<AnimationStateConfig> states = new List<AnimationStateConfig>();

        [Header("Transitions")]
        [Tooltip("Transitions between states (condition-driven)")]
        public List<AnimationTransitionConfig> transitions = new List<AnimationTransitionConfig>();

        [Header("Default State")]
        [Tooltip("Name of the default state (entry point). Must match one of the states.")]
        public string defaultStateName = "Idle";
    }

    /// <summary>
    /// One animator parameter (bool, float, int, or trigger).
    /// </summary>
    [Serializable]
    public class AnimatorParameterConfig
    {
        public string name;
        public AnimatorParameterType type = AnimatorParameterType.Bool;
        [Tooltip("Default value for bool/int/float params")]
        public float defaultValue;
    }

    /// <summary>
    /// Parameter type — mirrors UnityEngine.AnimatorControllerParameterType.
    /// Defined separately to avoid editor-only dependency in data assembly.
    /// </summary>
    public enum AnimatorParameterType
    {
        Float = 1,
        Int = 3,
        Bool = 4,
        Trigger = 9
    }

    /// <summary>
    /// One animation state. Points to a sprite sheet (or region of one) and
    /// produces a single .anim clip with sprite-swap keyframes.
    /// </summary>
    [Serializable]
    public class AnimationStateConfig
    {
        [Header("Identity")]
        public string stateName;

        [Header("Sprite Source")]
        [Tooltip("Sprite sheet to slice for this state. If null, uses the parent config's spriteSheet.")]
        public Texture2D spriteSheet;
        [Tooltip("A sheet loaded on demand instead (path under Assets/, with spriteSheet null): kept out of the boot's load. Player states only (LazySheets: the weapon-grip sheets).")]
        public string lazySheet = "";
        [Tooltip("If the sheet has multiple rows, which sprites to use (by name prefix). Leave empty to use all.")]
        public string spriteNameFilter;
        [Tooltip("Skip this many frames (after the name filter), e.g. to play only part of a long sheet.")]
        public int firstFrame = 0;
        [Tooltip("How many frames to play from firstFrame. 0 = through the last frame.")]
        public int frameCount = 0;

        [Header("Playback")]
        public int fps = 12;
        public bool loop = true;

        [Header("Events")]
        [Tooltip("Animation events to insert at specific frames")]
        public List<AnimationEventConfig> events = new List<AnimationEventConfig>();

        [Header("Speed")]
        [Tooltip("Playback speed multiplier for this state")]
        public float speed = 1f;

        /// <summary>The part of a sheet's frames (already name-filtered and in order) this state plays.</summary>
        public List<T> SelectFrames<T>(IReadOnlyList<T> frames) => SelectFrames(frames, firstFrame, frameCount);

        /// <summary>frames[firstFrame ..] limited to frameCount (0 = through the end), clamped to what exists.</summary>
        public static List<T> SelectFrames<T>(IReadOnlyList<T> frames, int firstFrame, int frameCount)
        {
            int start = Mathf.Clamp(firstFrame, 0, frames.Count);
            int count = frameCount > 0 ? Mathf.Min(frameCount, frames.Count - start) : frames.Count - start;
            var selected = new List<T>(count);
            for (int i = 0; i < count; i++)
                selected.Add(frames[start + i]);
            return selected;
        }
    }

    /// <summary>
    /// An animation event fired at a specific frame during a clip.
    /// </summary>
    [Serializable]
    public class AnimationEventConfig
    {
        [Tooltip("Frame number (0-based) at which to fire the event")]
        public int frame;
        [Tooltip("Name of the method to call (must exist on the GameObject)")]
        public string functionName;
        [Tooltip("Optional string parameter")]
        public string stringParameter;
        [Tooltip("Optional int parameter")]
        public int intParameter;
        [Tooltip("Optional float parameter")]
        public float floatParameter;
    }

    /// <summary>
    /// A transition between two animator states, driven by parameter conditions.
    /// </summary>
    [Serializable]
    public class AnimationTransitionConfig
    {
        [Header("Connection")]
        [Tooltip("Source state name ('Any' for AnyState transitions)")]
        public string fromState;
        [Tooltip("Destination state name")]
        public string toState;

        [Header("Settings")]
        [Tooltip("Whether this transition has exit time (waits for clip to finish)")]
        public bool hasExitTime;
        [Tooltip("Exit time (0-1 normalized). Only used if hasExitTime is true.")]
        [Range(0f, 1f)]
        public float exitTime = 1f;
        [Tooltip("Transition duration in seconds")]
        public float transitionDuration = 0f;

        [Header("Conditions")]
        public List<TransitionConditionConfig> conditions = new List<TransitionConditionConfig>();
    }

    /// <summary>
    /// A single condition on a transition (e.g., "IsWalking == true").
    /// </summary>
    [Serializable]
    public class TransitionConditionConfig
    {
        public string parameterName;
        public ConditionMode mode = ConditionMode.If;
        [Tooltip("Threshold for Greater/Less comparisons")]
        public float threshold;
    }

    /// <summary>
    /// Mirrors UnityEditor.AnimatorConditionMode without requiring editor assembly.
    /// </summary>
    public enum ConditionMode
    {
        /// <summary>Bool is true / Trigger is set</summary>
        If = 1,
        /// <summary>Bool is false</summary>
        IfNot = 2,
        /// <summary>Float/Int greater than threshold</summary>
        Greater = 3,
        /// <summary>Float/Int less than threshold</summary>
        Less = 4,
        /// <summary>Int equals threshold</summary>
        Equals = 6,
        /// <summary>Int not equal to threshold</summary>
        NotEqual = 7
    }
}
