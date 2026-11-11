using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// Keeps a music source at its authored volume times the player's Music setting and the Music bus (its trim and the
    /// duck under voice lines, <see cref="AudioBuses"/>), also when either changes mid-song. Set music volumes through
    /// <see cref="Set"/> instead of AudioSource.volume.
    /// </summary>
    public sealed class MusicVolume : MonoBehaviour
    {
        float authored = 1f;
        AudioSource source;

        /// <summary>The source this binder keeps (the playtest reads its volume to check the duck).</summary>
        public AudioSource Source => source;

        public static void Set(AudioSource source, float authoredVolume)
        {
            if (source == null)
                return;
            if (!source.TryGetComponent<MusicVolume>(out var binder))
                binder = source.gameObject.AddComponent<MusicVolume>();
            binder.source = source;
            binder.authored = authoredVolume;
            binder.Apply();
        }

        /// <summary>The volume last given to <see cref="Set"/> (the source's own volume if it never was).</summary>
        public static float AuthoredOf(AudioSource source)
        {
            if (source == null)
                return 0f;
            return source.TryGetComponent<MusicVolume>(out var binder) ? binder.authored : source.volume;
        }

        /// <summary>The volume a music source plays at right now: authored x the Music setting x the Music bus (duck included).</summary>
        public static float Effective(float authoredVolume) => Mathf.Clamp01(authoredVolume * GameSettings.MusicVolume * AudioBuses.Gain(Bus.Music));

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            AudioBuses.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            GameSettings.Changed -= Apply;
            AudioBuses.Changed -= Apply;
        }

        void Apply()
        {
            if (source != null)
                source.volume = Effective(authored);
        }
    }
}
