using System;
using System.Text.RegularExpressions;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// How the game gets its Hawkes Net connection back (GhostLobby): capped exponential backoff with jitter
    /// (2 s, 4 s, 8 s ... 60 s, each +-25%), scheduled from the failure itself, never polled. A 410 Gone, or the
    /// third 4xx in a row (other than 408/429, which are only "slow down"), ends the session's retrying: the game
    /// asks again on the next scene load or when an online feature opens. Pure logic, no Unity objects (EditMode tests).
    /// </summary>
    public sealed class NetRetry
    {
        public const float FirstDelay = 2f, MaxDelay = 60f, Jitter = 0.25f;
        public const int StopAfterClientErrors = 3;

        static readonly Regex StatusCode = new Regex(@"\b([45]\d\d)\b", RegexOptions.Compiled);
        readonly Random random;
        int failures, clientErrors;

        public NetRetry(Random random = null) { this.random = random ?? new Random(); }

        /// <summary>Failures since the last success (the backoff step).</summary>
        public int Failures => failures;

        /// <summary>The HTTP status named in a problem's code or detail ("ticket: 403", "WebSocket 502"), or 0.</summary>
        public static int StatusOf(string problem)
        {
            if (string.IsNullOrEmpty(problem)) return 0;
            if (problem.IndexOf("gone", StringComparison.OrdinalIgnoreCase) >= 0) return 410;
            var m = StatusCode.Match(problem);
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        /// <summary>A connection was made: the next failure starts the backoff over.</summary>
        public void Succeeded() { failures = 0; clientErrors = 0; }

        /// <summary>The next failure: the seconds to wait before trying again, or -1 to stop retrying this session.</summary>
        public float Failed(string problem)
        {
            int status = StatusOf(problem);
            failures++;
            if (status == 410) return -1f;
            if (status >= 400 && status < 500 && status != 408 && status != 429)
            {
                if (++clientErrors >= StopAfterClientErrors) return -1f;
            }
            else clientErrors = 0;
            return DelayFor(failures);
        }

        /// <summary>Delay before attempt number `failure` (1 = after the first failure): 2, 4, 8 ... 60, jittered.</summary>
        public float DelayFor(int failure)
        {
            float basis = Math.Min(MaxDelay, FirstDelay * (float)Math.Pow(2, Math.Max(0, failure - 1)));
            float spread = 1f + Jitter * (2f * (float)random.NextDouble() - 1f);
            return basis * spread;
        }
    }
}
