using System;
using System.Collections;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// The mixer: four buses (Music, Voice, SFX, Ambience), each with a trim in dB, and the duck of Music under Voice.
    /// It is software on purpose. Unity's AudioMixer effects (ducking, the limiter) are not supported in WebGL, which is
    /// where the game ships, so the buses are gains that every player of sound multiplies in (<see cref="Sfx"/> for
    /// effects, voices and the ambience bed, <see cref="MusicVolume"/> for every music source). The master limiter is the
    /// page's: Tools/Web/hawkes-sound.js puts a DynamicsCompressorNode in front of Unity's destination (-6 dB threshold,
    /// 20:1), the one place a limiter works in a browser.
    ///
    /// Ducking is driven by events, never polled: <see cref="VoiceStarted"/> (the voice queue starts a line) lowers Music by
    /// <see cref="DuckDb"/> over <see cref="AttackSeconds"/>; one timer (a coroutine, set up by that call) calls the end of
    /// the line after its length and Music comes back over <see cref="ReleaseSeconds"/>. The ramp runs only while the
    /// duck is moving. Presentation only; nothing here touches the simulation.
    /// </summary>
    public static class AudioBuses
    {
        /// <summary>How far Music sits under a voice line (dB, negative).</summary>
        public const float DuckDb = -8f;
        public const float AttackSeconds = 0.1f;
        public const float ReleaseSeconds = 0.4f;

        static readonly float[] trimDb = new float[4];
        static float duck = 1f;       // linear multiplier on Music right now
        static float duckTarget = 1f; // where it is going
        static int voicesPlaying;
        static Ducker ducker;

        /// <summary>A bus's gain or the duck changed: whoever applies it (the music sources, the ambience bed) re-applies.</summary>
        public static event Action Changed;
        /// <summary>A voice line started (true) or the last one ended (false): the duck's cause, for the playtest's log.</summary>
        public static event Action<bool> VoiceChanged;

        /// <summary>The linear gain of the duck (1 = none, <see cref="DuckGain"/> at full duck).</summary>
        public static float MusicDuck => duck;
        public static float DuckGain => Mathf.Pow(10f, DuckDb / 20f);
        public static bool Ducking => voicesPlaying > 0;
        public static int VoicesPlaying => voicesPlaying;

        /// <summary>Linear gain of a bus: its trim, and for Music the duck.</summary>
        public static float Gain(Bus bus)
        {
            float g = Mathf.Pow(10f, trimDb[(int)bus] / 20f);
            return bus == Bus.Music ? g * duck : g;
        }

        public static float TrimDb(Bus bus) => trimDb[(int)bus];

        public static void SetTrimDb(Bus bus, float db)
        {
            trimDb[(int)bus] = Mathf.Clamp(db, -40f, 12f);
            Changed?.Invoke();
        }

        /// <summary>A voice line of this length (seconds) starts now: duck the music for as long as it is spoken.</summary>
        public static void VoiceStarted(float seconds)
        {
            if (!Application.isPlaying)
                return;
            bool first = voicesPlaying == 0;
            voicesPlaying++;
            duckTarget = DuckGain;
            Host().Begin();
            Host().StartCoroutine(EndAfter(Mathf.Max(0.05f, seconds)));
            if (first)
                VoiceChanged?.Invoke(true);
        }

        /// <summary>The voice line ended (its timer, or a stop): once none is left, the music comes back.</summary>
        public static void VoiceEnded()
        {
            if (voicesPlaying <= 0)
                return;
            voicesPlaying--;
            if (voicesPlaying > 0)
                return;
            duckTarget = 1f;
            Host().Begin();
            VoiceChanged?.Invoke(false);
        }

        static IEnumerator EndAfter(float seconds)
        {
            // A little past the line (the voice queue leaves 0.15 s between lines) so a chained line does not pump the music.
            yield return new WaitForSecondsRealtime(seconds + 0.2f);
            VoiceEnded();
        }

        /// <summary>For tests: back to no duck, no voices, no trims.</summary>
        public static void ResetForTests()
        {
            voicesPlaying = 0;
            duck = duckTarget = 1f;
            for (int i = 0; i < trimDb.Length; i++)
                trimDb[i] = 0f;
        }

        static Ducker Host()
        {
            if (ducker == null)
            {
                var go = new GameObject("[AudioBuses]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                ducker = go.AddComponent<Ducker>();
                ducker.enabled = false;
            }
            return ducker;
        }

        /// <summary>Moves the duck toward its target, and only while it has somewhere to go (it switches itself off on arrival).</summary>
        sealed class Ducker : MonoBehaviour
        {
            public void Begin() => enabled = true;

            void Update()
            {
                if (Mathf.Approximately(duck, duckTarget))
                {
                    duck = duckTarget;
                    enabled = false;
                    return;
                }
                float span = 1f - DuckGain;
                float rate = span / (duckTarget < duck ? AttackSeconds : ReleaseSeconds);
                duck = Mathf.MoveTowards(duck, duckTarget, rate * Time.unscaledDeltaTime);
                Changed?.Invoke();
                if (Mathf.Approximately(duck, duckTarget))
                {
                    duck = duckTarget;
                    enabled = false;
                }
            }
        }
    }
}
