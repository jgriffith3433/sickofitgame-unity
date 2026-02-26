using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;
using UnityEngine.Timeline;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// PlayableAsset for a sprite frame clip on the Timeline.
    /// Each clip references a SpriteFramesData and a playback mode.
    /// 
    /// Duration is derived from the frame count ÷ 30 FPS as a default.
    /// The user controls playback behaviour via <see cref="SpriteFrameEndBehaviour"/>:
    /// - Speed:  resizing the clip changes playback speed.
    /// - Loop:   extending beyond one cycle repeats the animation.
    /// - Hold:   plays once at native FPS, then freezes the last frame.
    /// </summary>
    [System.Serializable]
    public class SpriteFrameClip : PlayableAsset, ITimelineClipAsset
    {
        private const float DefaultFPS = 30f;

        [Tooltip("SpriteFramesData asset defining the frame chunks for this clip")]
        [SerializeField] private SpriteFramesData spriteFramesData;

        [Tooltip("How the clip behaves when it reaches the end of its frames.")]
        [FormerlySerializedAs("loop")]
        [SerializeField] private SpriteFrameEndBehaviour endBehaviour = SpriteFrameEndBehaviour.Speed;

        /// <summary>
        /// The sprite frames data for this clip.
        /// </summary>
        public SpriteFramesData SpriteFramesData
        {
            get => spriteFramesData;
            set => spriteFramesData = value;
        }

        /// <summary>
        /// How the clip behaves when playback reaches the end of its frames.
        /// </summary>
        public SpriteFrameEndBehaviour EndBehaviour
        {
            get => endBehaviour;
            set => endBehaviour = value;
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
        /// Clip capabilities depend on the chosen <see cref="SpriteFrameEndBehaviour"/>:
        /// - Speed:  SpeedMultiplier — resizing the clip changes playback speed.
        /// - Loop:   Looping — extending the clip repeats the animation.
        /// - Hold:   None — plays once at native FPS, then holds the last frame.
        /// All modes support blending and clip-in for smooth transitions.
        /// </summary>
        public ClipCaps clipCaps
        {
            get
            {
                const ClipCaps common = ClipCaps.Blending | ClipCaps.ClipIn;
                return endBehaviour switch
                {
                    SpriteFrameEndBehaviour.Loop  => ClipCaps.Looping | common,
                    SpriteFrameEndBehaviour.Hold  => common,
                    _                             => ClipCaps.SpeedMultiplier | common,
                };
            }
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SpriteFrameBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.spriteFramesData = spriteFramesData;
            behaviour.endBehaviour = endBehaviour;

            // Explicitly set the playable duration so GetDuration() returns a valid value
            playable.SetDuration(duration);

            return playable;
        }
    }
}
