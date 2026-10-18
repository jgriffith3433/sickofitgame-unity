using System;

namespace SickOfIt.Core.Flow
{
    /// <summary>Where the story flow is when it reaches a Shaft stop (Docs/Story.md, "The Shaft: Hell's map").</summary>
    public enum ShaftStopKind
    {
        /// <summary>A video ended and a level comes next (after Video2: the first plunge to Floor 1,000).</summary>
        AfterVideo,
        /// <summary>A level was cleared and its results closed; the next video, level or the level select comes next.</summary>
        AfterLevel,
        /// <summary>Hell ended the run (a death on a floor stage, after the death scene): the hero drops back to a Waystone and the run restarts at NextLevel.</summary>
        AfterDeath,
    }

    /// <summary>One stop on the story path, as the Shaft screen sees it.</summary>
    public struct ShaftStop
    {
        public ShaftStopKind Kind;
        /// <summary>The video that just ended (AfterVideo), else 0.</summary>
        public int Video;
        /// <summary>The level that was just cleared (AfterLevel) or died on (AfterDeath), else 0.</summary>
        public int Level;
        /// <summary>AfterDeath: the floor the hero died on, and the Waystone he drops back to.</summary>
        public int FromFloor;
        public int Waystone;
        /// <summary>The level that comes next, or 0 when the next stop is a video or the level select.</summary>
        public int NextLevel;
        /// <summary>The video that comes next, or 0.</summary>
        public int NextVideo;
    }

    /// <summary>
    /// The hook point for the Shaft, Hell's map. The flow calls it after Video2 (before Level 2) and after every
    /// cleared level, before it loads what comes next, and after the death scene of a death that ends a run (the
    /// drop back to the Waystone). With nothing registered the flow goes straight on.
    /// The Shaft screen sets <see cref="Show"/>: return true to take over and call the continuation when the
    /// player is done with it, false to let the flow go on at once. It is presentation only: it reads progress
    /// and never changes it.
    /// </summary>
    public static class ShaftHook
    {
        public static Func<ShaftStop, Action, bool> Show;

        /// <summary>Shows the Shaft for this stop if something handles it, then (or else) runs <paramref name="next"/> once.</summary>
        public static void Run(ShaftStop stop, Action next)
        {
            bool done = false;
            void Once()
            {
                if (done) return;
                done = true;
                next?.Invoke();
            }

            bool tookOver = false;
            try
            {
                tookOver = Show != null && Show(stop, Once);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            if (!tookOver)
                Once();
        }
    }
}
