using System.Runtime.InteropServices;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;
using UnityEngine;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Phone haptics: a short buzz when the hero is hit (18 ms), grazes a bullet or kills something (7 ms). Android browsers only
    /// (navigator.vibrate through Plugins/WebGL/SickOfItHaptics.jslib; iOS has none), off when Reduced Shake is on, never in a scene
    /// or a rewind. Presentation only: it listens to the telemetry and changes nothing.
    /// </summary>
    public static class Haptics
    {
        public const int HitMs = 18, GrazeMs = 7, KillMs = 7;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SickOfItHaptics_Supported();
        [DllImport("__Internal")] static extern void SickOfItHaptics_Vibrate(int ms);
        static int supported = -1;
        static bool Supported => (supported < 0 ? (supported = SickOfItHaptics_Supported()) : supported) == 1;
        static void Buzz(int ms) => SickOfItHaptics_Vibrate(ms);
#else
        static bool Supported => false;
        static void Buzz(int ms) { }
#endif

        /// <summary>Buzzes sent since the game started, and the last one's length (the playtest reads them).</summary>
        public static int Sent { get; private set; }
        public static int LastMs { get; private set; }
        /// <summary>Buzzes the game would have sent on any device (the playtest counts these where there is no vibrate).</summary>
        public static int Wanted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            Sent = 0; Wanted = 0; LastMs = 0;
            GameplayTelemetry.Recorded -= OnRecorded;
            GameplayTelemetry.Recorded += OnRecorded;
        }

        static void OnRecorded(GameplayTelemetry log, GameplayTelemetry.Event e)
        {
            var world = log != null ? log.World : null;
            if (world == null || !string.IsNullOrEmpty(world.SceneId) || GameSettings.ReducedShake)
                return;
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                return;
            int ms = 0;
            switch (e.Kind)
            {
                case "playerHit": ms = HitMs; break;
                case "graze": ms = GrazeMs; break;
                case "death":
                    var content = GameContent.Current;
                    if (content != null && !string.IsNullOrEmpty(e.Subject) && content.Entities.TryGetValue(e.Subject, out var def)
                        && def.team == Team.Enemy && !string.IsNullOrEmpty(def.displayName))
                        ms = KillMs;
                    break;
            }
            if (ms == 0)
                return;
            Wanted++;
            LastMs = ms;
            if (!Supported)
                return;
            Sent++;
            Buzz(ms);
        }
    }
}
