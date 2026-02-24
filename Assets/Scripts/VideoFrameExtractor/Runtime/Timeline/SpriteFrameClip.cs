using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// PlayableAsset for a sprite frame clip on the Timeline.
    /// Each clip references a SpriteFramesData and has a loop option.
    /// 
    /// Duration is derived from the frame count ÷ 30 FPS as a default.
    /// The user controls effective FPS by resizing the clip on the Timeline:
    /// - Non-looping (SpeedMultiplier): stretching the clip slows playback, shrinking speeds it up.
    /// - Looping: extending beyond one cycle repeats the animation.
    /// </summary>
    [System.Serializable]
    public class SpriteFrameClip : PlayableAsset, ITimelineClipAsset
    {
        private const float DefaultFPS = 30f;

        [Tooltip("SpriteFramesData asset defining the frame chunks for this clip")]
        [SerializeField] private SpriteFramesData spriteFramesData;

        [Tooltip("If true, the animation loops within the clip. " +
                 "Extend the clip on the Timeline for multiple loops.")]
        [SerializeField] private bool loop = false;

        /// <summary>
        /// The sprite frames data for this clip.
        /// </summary>
        public SpriteFramesData SpriteFramesData
        {
            get => spriteFramesData;
            set => spriteFramesData = value;
        }

        /// <summary>
        /// Whether this clip loops.
        /// </summary>
        public bool Loop
        {
            get => loop;
            set => loop = value;
        }

        /// <summary>
        /// Default clip duration based on frame count at 30 FPS.
        /// </summary>
        public override double duration
        {
            get
            {
                if (spriteFramesData != null && spriteFramesData.GetStoredTotalFrames() > 0)
                    return spriteFramesData.GetStoredTotalFrames() / (double)DefaultFPS;
                return 1.0;
            }
        }

        /// <summary>
        /// Clip capabilities:
        /// - Looping clips: ClipCaps.Looping - extending the clip on the timeline loops the animation.
        /// - Non-looping clips: ClipCaps.SpeedMultiplier - resizing changes playback speed.
        /// - Both support blending and clip-in for smooth transitions.
        /// </summary>
        public ClipCaps clipCaps => loop
            ? ClipCaps.Looping | ClipCaps.Blending | ClipCaps.ClipIn
            : ClipCaps.SpeedMultiplier | ClipCaps.Blending | ClipCaps.ClipIn;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SpriteFrameBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.spriteFramesData = spriteFramesData;
            behaviour.loop = loop;

            // Explicitly set the playable duration so GetDuration() returns a valid value
            playable.SetDuration(duration);

            return playable;
        }
    }
}
