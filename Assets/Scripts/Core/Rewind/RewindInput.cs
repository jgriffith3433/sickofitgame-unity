using System;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Lets any rewind control (the R key, the HUD's rewind button) start and stop a rewind without knowing who
    /// performs it. The level's RewindController registers itself here.
    /// </summary>
    public static class RewindInput
    {
        static Action begin;
        static Action end;

        public static bool Available => begin != null;

        public static void Register(Action onBegin, Action onEnd)
        {
            begin = onBegin;
            end = onEnd;
        }

        public static void Unregister(Action onBegin)
        {
            if (begin == onBegin)
                begin = end = null;
        }

        public static void Begin() => begin?.Invoke();
        public static void End() => end?.Invoke();
    }
}
