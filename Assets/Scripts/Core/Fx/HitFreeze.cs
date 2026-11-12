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
        static float cooldownSeconds;
        static float staleScale = -1f;
        /// <summary>No freeze or slow motion may outlast this, in real seconds, whatever asked for it.</summary>
        public const float MaxSeconds = 3f;

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
            seconds = Mathf.Min(seconds, MaxSeconds);
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding || clock.IsPaused)
                return false;
            float now = Time.unscaledTime;
            if (Active)
            {
                // Already still: a second strong hit lengthens it (up to its own length), never stacks. A request that
                // outlasts the running one also sets the scale: the hero's 0.08 s hurt freeze (x0.02) always starts first, and the
                // Floor 1000 salvo's 600 s slow-motion (x0.2, a lesson he has to watch) was held at x0.02, a near stop, instead.
                if (now + seconds > endsAt)
                {
                    endsAt = now + seconds;
                    float wanted = baseScale * Mathf.Clamp(scale, 0.001f, 1f);
                    if (!Mathf.Approximately(wanted, frozenScale) && Mathf.Approximately(Time.timeScale, frozenScale))
                    {
                        frozenScale = wanted;
                        Time.timeScale = wanted;
                    }
                }
                return false;
            }
            if (now < readyAt || Time.timeScale <= 0f)
                return false;
            baseScale = Time.timeScale;
            frozenScale = baseScale * Mathf.Clamp(scale, 0.001f, 1f);
            Time.timeScale = frozenScale;
            endsAt = now + seconds;
            cooldownSeconds = Mathf.Max(0f, cooldown);
            readyAt = now + seconds + cooldownSeconds;
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
                Time.timeScale = RestoreScale;
            else if (Time.timeScale <= 0f)
                staleScale = frozenScale; // A pause holds it at 0 and will hand the frozen value back: the safety net in Tick undoes that.
            endsAt = -1f;
            frozenScale = -1f;
            // A freeze cut short (a rewind, a pause, a scene change) hands its cooldown back: 'readyAt' was set for the full
            // length. The Floor 1000 lesson's 600 s slow-motion otherwise left every later freeze (the hurt hit-stop, the next
            // try's lesson) refused for ten minutes after its rewind.
            readyAt = Mathf.Min(readyAt, Time.unscaledTime + cooldownSeconds);
        }

        static float RestoreScale => baseScale > 0f ? baseScale : 1f;

        static void Tick()
        {
            if (!Active)
            {
                // Safety net: a freeze that ended while a pause/picker held the scale at 0 must not come back when they restore
                // the value they saved (the frozen one). No freeze is running, so a scale equal to the old frozen one is stale.
                if (staleScale > 0f && Time.timeScale > 0f)
                {
                    if (Mathf.Approximately(Time.timeScale, staleScale))
                        Time.timeScale = RestoreScale;
                    staleScale = -1f;
                }
                return;
            }
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
