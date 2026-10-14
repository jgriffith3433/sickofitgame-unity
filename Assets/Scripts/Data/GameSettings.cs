using System;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Player preferences (volumes, screen shake, hints), saved in PlayerPrefs and kept across New Game.
    /// Master volume is applied to the AudioListener; the rest are read by whoever plays sound or shakes.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "Settings_";

        public static event Action Changed;

        public static float MasterVolume { get => Get("Master", 1f); set => Set("Master", value); }
        public static float MusicVolume { get => Get("Music", 0.8f); set => Set("Music", value); }
        public static float SfxVolume { get => Get("Sfx", 1f); set => Set("Sfx", value); }
        public static float VoiceVolume { get => Get("Voice", 1f); set => Set("Voice", value); }
        public static bool ScreenShake { get => Get("Shake", 1f) > 0.5f; set => Set("Shake", value ? 1f : 0f); }
        public static bool Hints { get => Get("Hints", 1f) > 0.5f; set => Set("Hints", value ? 1f : 0f); }

        /// <summary>Applies master volume. Called on startup and whenever a setting changes.</summary>
        public static void Apply() => AudioListener.volume = MasterVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStart() => Apply();

        static float Get(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);

        static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }
    }
}
