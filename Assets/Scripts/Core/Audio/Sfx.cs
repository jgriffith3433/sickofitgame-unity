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
                // A player build loads a clip no level or screen holds on its first play (GameAssets): an effect plays if
                // it arrives within a moment, a spoken line always.
                string scope = SickOfIt.Core.Addressables.GameAssets.Sound(id);
                if (SickOfIt.Core.Addressables.GameAssets.Knows(scope))
                {
                    float asked = Time.unscaledTime;
                    SickOfIt.Core.Addressables.GameAssets.Request(scope, () =>
                    {
                        if (sound.clip != null && (sound.kind == SoundKind.Voice || Time.unscaledTime - asked < 0.4f))
                            Play(id, volume, pitch);
                    });
                    return;
                }
                WarnOnce(id, $"[Sfx] Sound \"{id}\" has no clip yet. Generate it: SickOfIt > Audio > Sound Generator.");
                return;
            }

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < sound.cooldown)
                return;
            if (CountPlaying(id) >= Mathf.Max(1, sound.maxVoices))
                return;
            lastPlayed[id] = now;

            // Spoken lines take turns (a banner and its subtitle often appear together).
            if (sound.kind == SoundKind.Voice)
            {
                VoiceQueue.Get().Enqueue(sound, volume);
                return;
            }

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

        /// <summary>
        /// Plays a sound by id on 'source', looping, until the caller stops the source (an entity's sounds.loopSound,
        /// e.g. the portal's hum). Uses the sound's own volume and the Effects (or Voices) setting. False if it has no clip.
        /// </summary>
        public static bool PlayLoop(AudioSource source, string id, float volume = 1f)
        {
            if (source == null || string.IsNullOrEmpty(id) || !Application.isPlaying)
                return false;
            _ = Config;
            if (!byId.TryGetValue(id, out var sound))
            {
                WarnOnce(id, $"[Sfx] No sound \"{id}\" in audio.json.");
                return false;
            }
            if (sound.clip == null)
            {
                // Not loaded yet (a player build, GameAssets): the loop starts when its clip is in, if the source still wants it.
                string scope = SickOfIt.Core.Addressables.GameAssets.Sound(id);
                if (SickOfIt.Core.Addressables.GameAssets.Knows(scope))
                {
                    SickOfIt.Core.Addressables.GameAssets.Request(scope, () =>
                    {
                        if (source != null && source.isActiveAndEnabled && !source.isPlaying && sound.clip != null)
                            PlayLoop(source, id, volume);
                    });
                    return true;
                }
                WarnOnce(id, $"[Sfx] Sound \"{id}\" has no clip yet. Generate it: SickOfIt > Audio > Sound Generator.");
                return false;
            }
            float setting = sound.kind == SoundKind.Voice ? GameSettings.VoiceVolume : GameSettings.SfxVolume;
            source.clip = sound.clip;
            source.loop = true;
            source.pitch = 1f;
            source.volume = Mathf.Clamp01(sound.volume * volume * setting);
            source.Play();
            return true;
        }

        /// <summary>Length in seconds of a sound's clip, or 0 if it has none (yet).</summary>
        public static float LengthOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 0f;
            _ = Config;
            return byId.TryGetValue(id, out var sound) && sound.clip != null ? sound.clip.length : 0f;
        }

        /// <summary>The words of a voice line (its audio.json prompt without the [delivery tags]): the subtitle, available before the clip is made.</summary>
        public static string SpokenText(string id)
        {
            _ = Config;
            if (string.IsNullOrEmpty(id) || !byId.TryGetValue(id, out var sound) || sound.prompt == null)
                return "";
            return System.Text.RegularExpressions.Regex.Replace(sound.prompt, @"\[[^\]]*\]", "").Replace("  ", " ").Trim();
        }

        /// <summary>True when the id names a sound that has a clip (handy for "fall back to X" decisions).</summary>
        public static bool Has(string id)
        {
            _ = Config;
            // In a player build a clip that isn't loaded yet still exists (GameAssets loads it on its first play).
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var sound)
                && (sound.clip != null || SickOfIt.Core.Addressables.GameAssets.Knows(SickOfIt.Core.Addressables.GameAssets.Sound(id)));
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

        /// <summary>
        /// Plays queued voice lines one at a time on their own source. Lines chained in a script (a banner, then its
        /// subtitle) all get said; only a pile-up drops lines (the oldest, beyond MaxQueued) or one stale by MaxWait.
        /// A scene change clears what hasn't started, so one level's lines never spill into the next scene.
        /// </summary>
        sealed class VoiceQueue : MonoBehaviour
        {
            const float MaxWait = 8f;
            const int MaxQueued = 3;
            const float Gap = 0.15f;

            static VoiceQueue instance;
            readonly Queue<(SoundDef sound, float volume, float queuedAt)> queue = new Queue<(SoundDef, float, float)>();
            AudioSource source;
            float freeAt;

            public static VoiceQueue Get()
            {
                if (instance == null)
                {
                    var host = new GameObject("[Voices]");
                    DontDestroyOnLoad(host);
                    instance = host.AddComponent<VoiceQueue>();
                    instance.source = host.AddComponent<AudioSource>();
                    instance.source.playOnAwake = false;
                    instance.source.spatialBlend = 0f;
                    instance.source.ignoreListenerPause = true;
                    UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => instance.queue.Clear();
                }
                return instance;
            }

            public void Enqueue(SoundDef sound, float volume)
            {
                queue.Enqueue((sound, volume, Time.unscaledTime));
                while (queue.Count > MaxQueued)
                    queue.Dequeue();
            }

            void Update()
            {
                float now = Time.unscaledTime;
                if (source.isPlaying || now < freeAt)
                    return;
                while (queue.Count > 0)
                {
                    var (sound, volume, queuedAt) = queue.Dequeue();
                    if (now - queuedAt > MaxWait || sound.clip == null)
                        continue;
                    source.clip = sound.clip;
                    source.volume = Mathf.Clamp01(sound.volume * volume * GameSettings.VoiceVolume);
                    source.pitch = 1f;
                    source.Play();
                    freeAt = now + sound.clip.length + Gap;
                    break;
                }
            }
        }

        static void WarnOnce(string id, string message)
        {
            if (Application.isEditor && warned.Add(id))
                Debug.LogWarning(message);
        }
    }
}
