using UnityEngine.Playables;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Carries per-clip data for the SpriteFrame Timeline track.
    /// Populated by SpriteFrameClip.CreatePlayable().
    /// </summary>
    public class SpriteFrameBehaviour : PlayableBehaviour
    {
        /// <summary>
        /// The sprite frames data asset for this clip.
        /// </summary>
        public SpriteFramesData spriteFramesData;

        /// <summary>
        /// If true, the animation loops within the clip duration.
        /// When the clip is extended beyond one cycle on the timeline, it repeats.
        /// </summary>
        public bool loop;
    }
}
