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
        /// How the clip behaves when playback reaches the end of its frames.
        /// </summary>
        public SpriteFrameEndBehaviour endBehaviour;
    }
}
