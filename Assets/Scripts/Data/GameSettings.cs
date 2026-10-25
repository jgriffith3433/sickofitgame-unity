using System;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Player preferences (sound, volumes, screen shake, hints), saved in PlayerPrefs and kept across New Game.
    /// Sound on/off, Music and Effects are the Hawkes standard sound settings: one JSON value
    /// <c>{ "on": true, "music": 0.8, "sfx": 1 }</c> (0-1) under the key <see cref="SoundKey"/>, the same in every
    /// Hawkes game. The rest keep their own "Settings_" keys.
    /// Sound on/off and master volume are applied to the AudioListener; the rest are read by whoever plays sound or shakes.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "Settings_";
        public const string SoundKey = "hawkes.sound";

        public static event Action Changed;

        [Serializable]
        sealed class Sound
        {
            public bool on = true;
            public float music = 0.8f;
            public float sfx = 1f;
        }

        static Sound sound;

        public static bool SoundOn { get => Load().on; set { Load().on = value; SaveSound(); } }
        public static float MusicVolume { get => Load().music; set { Load().music = Mathf.Clamp01(value); SaveSound(); } }
        public static float SfxVolume { get => Load().sfx; set { Load().sfx = Mathf.Clamp01(value); SaveSound(); } }

        public static float MasterVolume { get => Get("Master", 1f); set => Set("Master", value); }
        public static float VoiceVolume { get => Get("Voice", 1f); set => Set("Voice", value); }
        public static bool ScreenShake { get => Get("Shake", 1f) > 0.5f; set => Set("Shake", value ? 1f : 0f); }
        // Accessibility (settings > Accessibility). All off by default: Normal is played as designed.
        public static bool BulletOutlines { get => Get("BulletOutlines", 0f) > 0.5f; set => Set("BulletOutlines", value ? 1f : 0f); }
        public static bool HighContrastBullets { get => Get("HighContrast", 0f) > 0.5f; set => Set("HighContrast", value ? 1f : 0f); }
        public static bool ReducedFlash { get => Get("ReducedFlash", 0f) > 0.5f; set => Set("ReducedFlash", value ? 1f : 0f); }
        public static bool ReducedShake { get => Get("ReducedShake", 0f) > 0.5f; set => Set("ReducedShake", value ? 1f : 0f); }
        public static bool HitboxDot { get => Get("HitboxDot", 0f) > 0.5f; set => Set("HitboxDot", value ? 1f : 0f); }
        public static bool AimAssist { get => Get("AimAssist", 0f) > 0.5f; set => Set("AimAssist", value ? 1f : 0f); }
        public static bool Hints { get => Get("Hints", 1f) > 0.5f; set => Set("Hints", value ? 1f : 0f); }

        /// <summary>The saved hawkes.sound value, as JSON (what the settings and the playtest read back).</summary>
        public static string SoundJson => PlayerPrefs.GetString(SoundKey, "");

        /// <summary>Applies sound on/off and master volume. Called on startup and whenever a setting changes.</summary>
        public static void Apply() => AudioListener.volume = SoundOn ? MasterVolume : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStart() => Apply();

        static Sound Load()
        {
            if (sound != null)
                return sound;
            string json = PlayerPrefs.GetString(SoundKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { sound = JsonUtility.FromJson<Sound>(json); }
                catch (ArgumentException) { sound = null; }
            }
            if (sound == null)
            {
                // First run with this key: keep the volumes saved before it existed.
                sound = new Sound { music = Get("Music", 0.8f), sfx = Get("Sfx", 1f) };
            }
            sound.music = Mathf.Clamp01(sound.music);
            sound.sfx = Mathf.Clamp01(sound.sfx);
            return sound;
        }

        static void SaveSound()
        {
            PlayerPrefs.SetString(SoundKey, JsonUtility.ToJson(Load()));
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

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
