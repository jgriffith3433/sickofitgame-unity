using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// Plays sounds from audio.json by id: <c>Sfx.Play(Sfx.Events.playerShoot)</c>. Each sound has its own volume,
    /// pitch variation, cooldown and voice limit; effects follow the Effects volume setting and spoken lines the
    /// Voices setting. Sounds without a clip yet (not generated) are silent. Purely presentational: nothing here
    /// touches the simulation, so it's safe from anywhere.
    /// </summary>
    public static class Sfx
    {
        const int PoolSize = 24;

        static AudioConfig config;
        static List<SoundDef> indexedList;
        static readonly Dictionary<string, SoundDef> byId = new Dictionary<string, SoundDef>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        static readonly HashSet<string> warned = new HashSet<string>();
        static AudioSource[] pool;
        static string[] poolIds;
        static int next;

        /// <summary>The event-to-sound table (never null).</summary>
        public static SoundEvents Events => Config.events ?? (Config.events = new SoundEvents());

        static AudioConfig Config
        {
            get
            {
                var definition = GameDefinition.Instance;
                var current = definition != null && definition.audio != null ? definition.audio : config ?? new AudioConfig();
                // Re-index when the config was re-imported (the editor swaps the list on import).
                if (current != config || current.sounds != indexedList)
                {
                    config = current;
                    indexedList = current.sounds;
                    byId.Clear();
                    foreach (var sound in current.sounds)
                    {
                        if (sound != null && !string.IsNullOrEmpty(sound.id))
                            byId[sound.id] = sound;
                    }
                }
                return config;
            }
        }

        /// <summary>Plays a sound by id. 'volume' and 'pitch' multiply the sound's own settings.</summary>
        public static void Play(string id, float volume = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(id) || !Application.isPlaying)
                return;
            _ = Config;
            if (!byId.TryGetValue(id, out var sound))
            {
                WarnOnce(id, $"[Sfx] No sound \"{id}\" in audio.json.");
                return;
            }
            if (sound.clip == null)
            {
                WarnOnce(id, $"[Sfx] Sound \"{id}\" has no clip yet. Generate it: SickOfIt > Audio > Sound Generator.");
                return;
            }

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < sound.cooldown)
                return;
            if (CountPlaying(id) >= Mathf.Max(1, sound.maxVoices))
                return;
            lastPlayed[id] = now;

            var source = NextSource();
            if (source == null)
                return;
            float setting = sound.kind == SoundKind.Voice ? GameSettings.VoiceVolume : GameSettings.SfxVolume;
            source.clip = sound.clip;
            source.loop = false;
            source.volume = Mathf.Clamp01(sound.volume * volume * setting);
            float variation = Mathf.Max(0f, sound.pitchVariation);
            source.pitch = Mathf.Clamp(pitch * (1f + Random.Range(-variation, variation)), 0.1f, 3f);
            source.Play();
            poolIds[System.Array.IndexOf(pool, source)] = id;
        }

        /// <summary>True when the id names a sound that has a clip (handy for "fall back to X" decisions).</summary>
        public static bool Has(string id)
        {
            _ = Config;
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var sound) && sound.clip != null;
        }

        static int CountPlaying(string id)
        {
            if (pool == null)
                return 0;
            int count = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && pool[i].isPlaying && poolIds[i] == id)
                    count++;
            }
            return count;
        }

        static AudioSource NextSource()
        {
            if (pool == null || pool[0] == null)
                CreatePool();
            // Prefer an idle source; otherwise take over the oldest one.
            for (int i = 0; i < pool.Length; i++)
            {
                int index = (next + i) % pool.Length;
                if (!pool[index].isPlaying)
                {
                    next = (index + 1) % pool.Length;
                    return pool[index];
                }
            }
            var stolen = pool[next];
            next = (next + 1) % pool.Length;
            return stolen;
        }

        static void CreatePool()
        {
            var host = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(host);
            pool = new AudioSource[PoolSize];
            poolIds = new string[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                pool[i] = source;
            }
        }

        static void WarnOnce(string id, string message)
        {
            if (Application.isEditor && warned.Add(id))
                Debug.LogWarning(message);
        }
    }
}
