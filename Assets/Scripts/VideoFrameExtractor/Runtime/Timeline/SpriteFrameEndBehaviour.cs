namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Controls what happens when a SpriteFrameClip's playback reaches
    /// the end of its frames within a Timeline clip.
    /// </summary>
    public enum SpriteFrameEndBehaviour
    {
        /// <summary>
        /// Speed mode: resizing the clip on the Timeline changes playback speed.
        /// Shrink = faster, stretch = slower. All frames always play exactly once.
        /// </summary>
        Speed = 0,

        /// <summary>
        /// Loop mode: extending the clip on the Timeline repeats the animation.
        /// Each cycle plays all frames once at native FPS.
        /// </summary>
        Loop = 1,

        /// <summary>
        /// Hold mode: plays the animation once at native FPS (30),
        /// then freezes on the last frame for the remaining clip duration.
        /// Extend the clip to hold the final pose longer.
        /// </summary>
        Hold = 2,
    }
}
