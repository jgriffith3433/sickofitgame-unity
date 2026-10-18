using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// Keeps a music source at its authored volume times the player's Music setting, also when the setting
    /// changes mid-song. Set music volumes through <see cref="Set"/> instead of AudioSource.volume.
    /// </summary>
    public sealed class MusicVolume : MonoBehaviour
    {
        float authored = 1f;
        AudioSource source;

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

        void OnEnable() => GameSettings.Changed += Apply;
        void OnDisable() => GameSettings.Changed -= Apply;

        void Apply()
        {
            if (source != null)
                source.volume = Mathf.Clamp01(authored * GameSettings.MusicVolume);
        }
    }
}
