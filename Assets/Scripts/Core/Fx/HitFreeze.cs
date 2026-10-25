using UnityEngine;

namespace SickOfIt.Core.Fx
{
    /// <summary>
    /// The hit-freeze (game.json feel.freeze): a strong hit stops the world for a few real milliseconds. It lowers Unity's
    /// Time.timeScale, so FixedUpdate simply comes later: every simulation tick, its dt and its random numbers are the same as
    /// without the freeze, and the rewind history records the same moments. Never during a rewind or a pause; a rewind starting
    /// mid-freeze ends it at once. Presentation only.
    /// </summary>
    public static class HitFreeze
    {
        static float endsAt = -1f;
        static float readyAt;
        static float baseScale = 1f;
        static float frozenScale = -1f;

        /// <summary>Freezes so far this session, and the real seconds of the last one (for the playtest).</summary>
        public static int Count { get; private set; }
        public static float LastSeconds { get; private set; }
        public static string LastReason { get; private set; } = "";
        public static bool Active => endsAt >= 0f;

        /// <summary>Freezes for 'seconds' of real time at 'scale', unless one ran less than 'cooldown' ago. Returns true if it froze.</summary>
        public static bool Freeze(float seconds, float scale, float cooldown, string reason)
        {
            if (seconds <= 0f || !Application.isPlaying)
                return false;
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding || clock.IsPaused)
                return false;
            float now = Time.unscaledTime;
            if (Active)
            {
                // Already still: a second strong hit lengthens it (up to its own length), never stacks.
                endsAt = Mathf.Max(endsAt, now + seconds);
                return false;
            }
            if (now < readyAt || Time.timeScale <= 0f)
                return false;
            baseScale = Time.timeScale;
            frozenScale = baseScale * Mathf.Clamp(scale, 0.001f, 1f);
            Time.timeScale = frozenScale;
            endsAt = now + seconds;
            readyAt = now + seconds + Mathf.Max(0f, cooldown);
            Count++;
            LastSeconds = seconds;
            LastReason = reason ?? "";
            Runner.Ensure();
            return true;
        }

        /// <summary>Ends a freeze now, putting Unity's time scale back (pause and the results call this before they read it).</summary>
        public static void Cancel()
        {
            if (!Active)
                return;
            // Only put it back if nobody else changed it meanwhile (a pause, the results screen, the playtest's speed).
            if (Mathf.Approximately(Time.timeScale, frozenScale))
                Time.timeScale = baseScale;
            endsAt = -1f;
            frozenScale = -1f;
        }

        static void Tick()
        {
            if (!Active)
                return;
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding || Time.unscaledTime >= endsAt)
                Cancel();
        }

        /// <summary>Ends the freeze on time, in real time.</summary>
        sealed class Runner : MonoBehaviour
        {
            static Runner instance;

            public static void Ensure()
            {
                if (instance != null)
                    return;
                var go = new GameObject("[HitFreeze]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Runner>();
            }

            void Update() => Tick();

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                    Cancel();
                }
            }
        }
    }
}
