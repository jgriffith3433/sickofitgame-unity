using System;
using System.Diagnostics;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// How smooth rewind runs, measured instead of guessed: real frame times while time runs forward, while it
    /// rewinds and on the first frames after a rewind stops, plus what the timeline's own record/restore work cost
    /// and how much it had to look after (rewindables, how many were on, the longest history). RewindTimeline feeds
    /// it; a stop writes one "[RewindPerf]" line and raises <see cref="Reported"/>; the rewind-perf playtest reads it.
    /// Presentation only: it never touches the simulation.
    /// </summary>
    public static class RewindPerf
    {
        /// <summary>Frame times for one kind of frame (forward, rewinding, just after a rewind), in milliseconds.</summary>
        public sealed class Stats
        {
            public int Frames;
            public double TotalMs, WorstMs;
            /// <summary>The timeline's own work in those frames (restore while rewinding, catch-up and record forward).</summary>
            public double WorkMs, WorstWorkMs;

            public double AverageMs => Frames > 0 ? TotalMs / Frames : 0;
            public double AverageWorkMs => Frames > 0 ? WorkMs / Frames : 0;

            public void Add(double frameMs, double workMs)
            {
                Frames++;
                TotalMs += frameMs;
                WorkMs += workMs;
                if (frameMs > WorstMs) WorstMs = frameMs;
                if (workMs > WorstWorkMs) WorstWorkMs = workMs;
            }

            public void Reset()
            {
                Frames = 0;
                TotalMs = WorstMs = WorkMs = WorstWorkMs = 0;
            }

            public override string ToString() =>
                Frames == 0 ? "no frames" : $"{Frames} frames, avg {AverageMs:0.0} ms (worst {WorstMs:0.0}), timeline work avg {AverageWorkMs:0.00} ms (worst {WorstWorkMs:0.00})";
        }

        /// <summary>Frames while time runs forward (since the last <see cref="Reset"/>).</summary>
        public static readonly Stats Forward = new Stats();
        /// <summary>Frames while rewinding (since the last <see cref="Reset"/>).</summary>
        public static readonly Stats Rewinding = new Stats();
        /// <summary>The first <see cref="AfterFrames"/> frames after each rewind stops: where a lag spike could resolve hits.</summary>
        public static readonly Stats AfterRewind = new Stats();
        /// <summary>The rewind going on (or the last one): reset when one starts.</summary>
        public static readonly Stats Current = new Stats();
        /// <summary>The first <see cref="AfterFrames"/> frames after the last rewind stopped.</summary>
        public static readonly Stats CurrentAfter = new Stats();

        public const int AfterFrames = 3;

        /// <summary>Rewinds since the last <see cref="Reset"/>.</summary>
        public static int Rewinds { get; private set; }
        /// <summary>At the last rewind's start: rewindables registered, how many were on, the longest history (snapshots).</summary>
        public static int Rewindables { get; private set; }
        public static int ActiveRewindables { get; private set; }
        public static int LongestHistory { get; private set; }
        /// <summary>The longest game-time step the clock took on a frame just after a rewind (seconds).</summary>
        public static float WorstStepAfterRewind { get; private set; }

        /// <summary>One line per finished rewind (also logged).</summary>
        public static event Action<string> Reported;

        static int afterLeft;
        static float rewindStartedAt;

        public static void Reset()
        {
            Forward.Reset();
            Rewinding.Reset();
            AfterRewind.Reset();
            Current.Reset();
            CurrentAfter.Reset();
            Rewinds = 0;
            afterLeft = 0;
            WorstStepAfterRewind = 0f;
        }

        /// <summary>A timestamp for <see cref="MillisecondsSince"/>.</summary>
        internal static long Now() => Stopwatch.GetTimestamp();

        internal static double MillisecondsSince(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

        /// <summary>A finished frame: its real length, whether it rewound, the game time the clock moved and the timeline's work in it.</summary>
        internal static void AddFrame(float frameSeconds, bool rewinding, float clockStep, double workMs)
        {
            double frameMs = frameSeconds * 1000.0;
            if (rewinding)
            {
                Rewinding.Add(frameMs, workMs);
                Current.Add(frameMs, workMs);
                return;
            }
            if (afterLeft > 0)
            {
                afterLeft--;
                AfterRewind.Add(frameMs, workMs);
                CurrentAfter.Add(frameMs, workMs);
                if (clockStep > WorstStepAfterRewind)
                    WorstStepAfterRewind = clockStep;
                if (afterLeft == 0)
                {
                    string line = $"[RewindPerf] after rewind {Rewinds}: {CurrentAfter}, longest clock step {WorstStepAfterRewind * 1000f:0.0} ms";
                    UnityEngine.Debug.Log(line);
                    Reported?.Invoke(line);
                }
                return;
            }
            Forward.Add(frameMs, workMs);
        }

        internal static void RewindStarted(float clockTime, int rewindables, int active, int longestHistory)
        {
            Rewinds++;
            Current.Reset();
            CurrentAfter.Reset();
            rewindStartedAt = clockTime;
            Rewindables = rewindables;
            ActiveRewindables = active;
            LongestHistory = longestHistory;
        }

        internal static void RewindStopped(float clockTime)
        {
            afterLeft = AfterFrames;
            string line = $"[RewindPerf] rewind {Rewinds}: {rewindStartedAt - clockTime:0.00} s of game time back; rewinding {Current}; " +
                          $"forward so far {Forward}; {Rewindables} rewindables ({ActiveRewindables} on), longest history {LongestHistory} snapshots";
            UnityEngine.Debug.Log(line);
            Reported?.Invoke(line);
        }
    }
}
