using System;
using System.Collections;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// The mixer: six groups (Master = AudioListener; Music, Voice, SFX, Ambience, UI), each with a trim in dB, the duck of
    /// Music and Ambience under Voice, and the Pause / Low-health / Rewind snapshots. It is software on purpose. Unity's
    /// AudioMixer effects (ducking, the limiter) are not supported in WebGL, which is where the game ships, so the buses
    /// are gains that every player of sound multiplies in (<see cref="Sfx"/> for effects, voices and UI,
    /// <see cref="MusicVolume"/> for every music source, <see cref="AmbienceBed"/> for beds). The master limiter is the
    /// page's: Tools/Web/hawkes-sound.js puts a DynamicsCompressorNode in front of Unity's destination.
    ///
    /// Ducking is driven by events, never polled: <see cref="VoiceStarted"/> lowers Music by <see cref="DuckDb"/> and
    /// Ambience by <see cref="AmbienceDuckDb"/> over <see cref="AttackSeconds"/>; one timer ends the line and both come
    /// back over <see cref="ReleaseSeconds"/>. Snapshots are flags set by pause, one-heart and rewind (also events).
    /// Presentation only; nothing here touches the simulation.
    /// </summary>
    public static class AudioBuses
    {
        /// <summary>How far Music sits under a voice line (dB, negative).</summary>
        public const float DuckDb = -8f;
        /// <summary>How far Ambience sits under a voice line (dB, negative).</summary>
        public const float AmbienceDuckDb = -4f;
        public const float AttackSeconds = 0.08f;
        public const float ReleaseSeconds = 0.4f;

        public const float PauseMusicDb = -10f;
        public const float LowHealthMusicDb = -4f;
        public const float RewindSfxDb = -6f;

        static readonly float[] trimDb = new float[5]; // Music, Voice, Sfx, Ambience, Ui
        static float duck = 1f;       // linear multiplier on Music right now
        static float duckTarget = 1f;
        static float ambienceDuck = 1f;
        static float ambienceDuckTarget = 1f;
        static int voicesPlaying;
        static bool pauseOn, lowHealthOn, rewindOn;
        static Ducker ducker;

        /// <summary>A bus's gain or the duck / a snapshot changed: whoever applies it re-applies.</summary>
        public static event Action Changed;
        /// <summary>A voice line started (true) or the last one ended (false): the duck's cause, for the playtest's log.</summary>
        public static event Action<bool> VoiceChanged;
        /// <summary>A named snapshot turned on or off (Pause / LowHealth / Rewind), for the playtest's audio log.</summary>
        public static event Action<string, bool> SnapshotChanged;

        public static float MusicDuck => duck;
        public static float AmbienceDuck => ambienceDuck;
        public static float DuckGain => Mathf.Pow(10f, DuckDb / 20f);
        public static float AmbienceDuckGain => Mathf.Pow(10f, AmbienceDuckDb / 20f);
        public static bool Ducking => voicesPlaying > 0;
        public static int VoicesPlaying => voicesPlaying;
        public static bool PauseActive => pauseOn;
        public static bool LowHealthActive => lowHealthOn;
        public static bool RewindActive => rewindOn;

        /// <summary>Music bus gain in dB right now (trim + duck + snapshots), for the playtest.</summary>
        public static float MusicGainDb => ToDb(Gain(Bus.Music));
        public static float SfxGainDb => ToDb(Gain(Bus.Sfx));
        public static float AmbienceGainDb => ToDb(Gain(Bus.Ambience));

        static float ToDb(float linear) => 20f * Mathf.Log10(Mathf.Max(1e-6f, linear));

        /// <summary>Linear gain of a bus: its trim, the voice duck (Music / Ambience), and the active snapshots.</summary>
        public static float Gain(Bus bus)
        {
            if (bus == Bus.Sfx && pauseOn)
                return 0f;
            float db = trimDb[(int)bus];
            if (bus == Bus.Music)
            {
                if (pauseOn) db += PauseMusicDb;
                if (lowHealthOn) db += LowHealthMusicDb;
            }
            else if (bus == Bus.Sfx && rewindOn)
                db += RewindSfxDb;
            float g = Mathf.Pow(10f, db / 20f);
            if (bus == Bus.Music) g *= duck;
            else if (bus == Bus.Ambience) g *= ambienceDuck;
            return g;
        }

        public static float TrimDb(Bus bus) => trimDb[(int)bus];

        public static void SetTrimDb(Bus bus, float db)
        {
            trimDb[(int)bus] = Mathf.Clamp(db, -40f, 12f);
            Changed?.Invoke();
        }

        /// <summary>Pause snapshot: Music -10 dB, SFX muted. UI and Voice stay up so the pause menu still clicks and talks.</summary>
        public static void SetPause(bool on)
        {
            if (pauseOn == on) return;
            pauseOn = on;
            SnapshotChanged?.Invoke("Pause", on);
            Changed?.Invoke();
        }

        /// <summary>Low-health snapshot: Music -4 dB while the hero is at one heart (the heartbeat loop is separate).</summary>
        public static void SetLowHealth(bool on)
        {
            if (lowHealthOn == on) return;
            lowHealthOn = on;
            SnapshotChanged?.Invoke("LowHealth", on);
            Changed?.Invoke();
        }

        /// <summary>Rewind snapshot: SFX -6 dB so the rewind whoosh leads.</summary>
        public static void SetRewind(bool on)
        {
            if (rewindOn == on) return;
            rewindOn = on;
            SnapshotChanged?.Invoke("Rewind", on);
            Changed?.Invoke();
        }

        /// <summary>A voice line of this length (seconds) starts now: duck Music and Ambience for as long as it is spoken.</summary>
        public static void VoiceStarted(float seconds)
        {
            if (!Application.isPlaying)
                return;
            bool first = voicesPlaying == 0;
            voicesPlaying++;
            duckTarget = DuckGain;
            ambienceDuckTarget = AmbienceDuckGain;
            Host().Begin();
            Host().StartCoroutine(EndAfter(Mathf.Max(0.05f, seconds)));
            if (first)
                VoiceChanged?.Invoke(true);
        }

        /// <summary>The voice line ended (its timer, or a stop): once none is left, Music and Ambience come back.</summary>
        public static void VoiceEnded()
        {
            if (voicesPlaying <= 0)
                return;
            voicesPlaying--;
            if (voicesPlaying > 0)
                return;
            duckTarget = 1f;
            ambienceDuckTarget = 1f;
            Host().Begin();
            VoiceChanged?.Invoke(false);
        }

        static IEnumerator EndAfter(float seconds)
        {
            // A little past the line (the voice queue leaves 0.15 s between lines) so a chained line does not pump the music.
            yield return new WaitForSecondsRealtime(seconds + 0.2f);
            VoiceEnded();
        }

        /// <summary>For tests: back to no duck, no voices, no trims, no snapshots.</summary>
        public static void ResetForTests()
        {
            voicesPlaying = 0;
            duck = duckTarget = 1f;
            ambienceDuck = ambienceDuckTarget = 1f;
            pauseOn = lowHealthOn = rewindOn = false;
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

        /// <summary>Moves the ducks toward their targets, and only while either has somewhere to go.</summary>
        sealed class Ducker : MonoBehaviour
        {
            public void Begin() => enabled = true;

            void Update()
            {
                bool musicMoving = !Mathf.Approximately(duck, duckTarget);
                bool ambMoving = !Mathf.Approximately(ambienceDuck, ambienceDuckTarget);
                if (!musicMoving && !ambMoving)
                {
                    duck = duckTarget;
                    ambienceDuck = ambienceDuckTarget;
                    enabled = false;
                    return;
                }
                if (musicMoving)
                {
                    float span = 1f - DuckGain;
                    float rate = span / (duckTarget < duck ? AttackSeconds : ReleaseSeconds);
                    duck = Mathf.MoveTowards(duck, duckTarget, rate * Time.unscaledDeltaTime);
                }
                if (ambMoving)
                {
                    float span = 1f - AmbienceDuckGain;
                    float rate = span / (ambienceDuckTarget < ambienceDuck ? AttackSeconds : ReleaseSeconds);
                    ambienceDuck = Mathf.MoveTowards(ambienceDuck, ambienceDuckTarget, rate * Time.unscaledDeltaTime);
                }
                Changed?.Invoke();
                if (Mathf.Approximately(duck, duckTarget) && Mathf.Approximately(ambienceDuck, ambienceDuckTarget))
                {
                    duck = duckTarget;
                    ambienceDuck = ambienceDuckTarget;
                    enabled = false;
                }
            }
        }
    }
}
