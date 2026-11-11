using System.Runtime.InteropServices;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// Tells Studio's audio probe (hawkes-audio.js) about every sound the game starts, as the probe's own
    /// <c>hawkes-sound</c> window events (<c>sound-start</c> with sample, bus, rate, gain, dur; <c>bus-gain</c> for the music duck).
    /// Unity's WebAudio sits behind a master limiter and a suspended-until-tap context, so the probe's tap of
    /// <c>createBufferSource</c> saw nothing (audio.sounds=0); the game knows better, so it says so. Presentation only:
    /// WebGL builds only, no per-frame work (one call per sound start), nothing read back.
    /// </summary>
    public static class AudioProbeBridge
    {
        /// <summary>How many starts were sent (tests, the playtest).</summary>
        public static int Sent { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void HawkesReportSoundStart(string sample, string bus, float rate, float gain, float dur);

        [DllImport("__Internal")]
        static extern void HawkesReportBusGain(string bus, float gain);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            Sfx.Played += OnPlayed;
            AudioBuses.VoiceChanged += OnVoice;
        }

        static void OnPlayed(Sfx.PlayedSound s)
        {
            float dur = 0f;
            if (s.bus == Bus.Voice)
            {
                var clip = Sfx.ClipOf(s.id);
                dur = clip != null ? clip.length : 0f;
            }
            Sound(s.id, s.bus, s.pitch, s.volume, dur);
        }

        static void OnVoice(bool speaking) => Send("music", speaking ? AudioBuses.DuckGain : 1f);

        static void Send(string bus, float gain)
        {
            try { HawkesReportBusGain(bus, gain); } catch { /* a page without the probe */ }
        }

        static void Sound(string id, Bus bus, float rate, float gain, float dur)
        {
            string busName = id != null && id.StartsWith("ui_") ? "ui" : BusName(bus);
            try { HawkesReportSoundStart(id ?? "unknown", busName, rate, gain, dur); Sent++; } catch { }
        }
#else
        static void Sound(string id, Bus bus, float rate, float gain, float dur) { }
#endif

        /// <summary>A start that doesn't go through <see cref="Sfx"/> (the music bed, the rewind whoosh loop).</summary>
        public static void Report(string id, Bus bus, float rate, float gain, float dur = 0f) => Sound(id, bus, rate, gain, dur);

        static string BusName(Bus bus)
        {
            switch (bus)
            {
                case Bus.Music: return "music";
                case Bus.Voice: return "voice";
                case Bus.Ambience: return "ambience";
                default: return "sfx";
            }
        }
    }
}
