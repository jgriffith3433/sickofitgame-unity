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
    ///
    /// A sound with several takes (SoundDef.clip + takes) plays one at random per call, never the take it played last;
    /// effects get a random pitch of +-SoundDef.pitchVariation (6% in audio.json), voices never; at most
    /// <see cref="MaxCopies"/> copies of one effect sound at once. Every sound plays on a mixer group (AudioBuses) and
    /// each start is reported through <see cref="Played"/> (sounds played straight from a clip, such as an enemy's own death clips, bypass the buses) (the audio playtest's log).
    /// </summary>
    public static class Sfx
    {
        const int PoolSize = 24;
        /// <summary>At most this many copies of one effect sound play at once (audio.json's own maxVoices can only lower it).</summary>
        public const int MaxCopies = 4;

        /// <summary>What a sound start looked like: the id, which take (of how many), its pitch, volume and mixer group.</summary>
        public struct PlayedSound
        {
            public string id;
            public int take;
            public int takes;
            public float pitch;
            public float volume;
            public Bus bus;
            public float time;
        }

        /// <summary>Raised for every sound that really starts (effects when they begin, voice lines when the queue says them, the ambience bed).</summary>
        public static event System.Action<PlayedSound> Played;

        /// <summary>For the other players of sound in this assembly (the ambience bed): report a start the same way.</summary>
        public static void Report(PlayedSound sound) => Played?.Invoke(sound);

        static readonly Dictionary<string, AudioClip> lastTake = new Dictionary<string, AudioClip>();
        static readonly List<AudioClip> candidates = new List<AudioClip>(8);

        static AudioConfig config;
        static List<SoundDef> indexedList;
        static readonly Dictionary<string, SoundDef> byId = new Dictionary<string, SoundDef>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        static readonly HashSet<string> warned = new HashSet<string>();
        static AudioSource[] pool;
        static string[] poolIds;
        static int[] poolPriority;
        static Bus[] poolBus;
        static float[] poolAuthored; // sound.volume * call volume (before settings / buses)
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
        public static void Play(string id, float volume = 1f, float pitch = 1f, bool exactPitch = false)
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
                            Play(id, volume, pitch, exactPitch);
                    });
                    return;
                }
                WarnOnce(id, $"[Sfx] Sound \"{id}\" has no clip yet. Generate it: SickOfIt > Audio > Sound Generator.");
                return;
            }

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < sound.cooldown)
                return;
            bool isVoice = sound.kind == SoundKind.Voice;
            int cap = isVoice ? Mathf.Max(1, sound.maxVoices) : Mathf.Clamp(sound.maxVoices, 1, MaxCopies);
            if (CountPlaying(id) >= cap && !TryStealSameId(id, sound.priority))
                return;
            lastPlayed[id] = now;

            // Spoken lines take turns (a banner and its subtitle often appear together).
            if (isVoice)
            {
                VoiceQueue.Get().Enqueue(sound, volume);
                return;
            }

            var source = NextSource(sound.priority);
            if (source == null)
                return;
            var bus = AudioConfig.BusOf(sound);
            var clip = PickTake(sound, out int take, out int takeCount);
            source.clip = clip;
            source.loop = false;
            float authored = sound.volume * volume;
            source.volume = Mathf.Clamp01(authored * SettingOf(bus) * AudioBuses.Gain(bus));
            float variation = exactPitch ? 0f : Mathf.Clamp(sound.pitchVariation, 0f, 0.2f);
            source.pitch = Mathf.Clamp(pitch * (1f + Random.Range(-variation, variation)), 0.1f, 3f);
            source.Play();
            int slot = System.Array.IndexOf(pool, source);
            poolIds[slot] = id;
            poolPriority[slot] = sound.priority;
            poolBus[slot] = bus;
            poolAuthored[slot] = authored;
            Played?.Invoke(new PlayedSound { id = id, take = take, takes = takeCount, pitch = source.pitch, volume = source.volume, bus = bus, time = now });
        }

        static float SettingOf(Bus bus) =>
            bus == Bus.Voice ? GameSettings.VoiceVolume : GameSettings.SfxVolume;

        /// <summary>
        /// One of a sound's takes (its clip and 'takes' that are loaded), random but never the one picked last time for this sound.
        /// 'take' is its place in the list (0 = clip), 'count' how many there are.
        /// </summary>
        public static AudioClip PickTake(SoundDef sound, out int take, out int count)
        {
            candidates.Clear();
            if (sound.clip != null)
                candidates.Add(sound.clip);
            if (sound.takes != null)
            {
                for (int i = 0; i < sound.takes.Count; i++)
                {
                    if (sound.takes[i] != null)
                        candidates.Add(sound.takes[i]);
                }
            }
            count = candidates.Count;
            if (count == 0)
            {
                take = -1;
                return null;
            }
            int pick = 0;
            if (count > 1)
            {
                lastTake.TryGetValue(sound.id, out var previous);
                int previousIndex = previous != null ? candidates.IndexOf(previous) : -1;
                // Random among the others: a pick of 0..count-2 skips over the previous take's place.
                pick = Random.Range(0, previousIndex >= 0 ? count - 1 : count);
                if (previousIndex >= 0 && pick >= previousIndex)
                    pick++;
            }
            var chosen = candidates[pick];
            lastTake[sound.id] = chosen;
            take = pick;
            return chosen;
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
            var bus = AudioConfig.BusOf(sound);
            source.clip = sound.clip;
            source.loop = true;
            source.pitch = 1f;
            source.volume = Mathf.Clamp01(sound.volume * volume * SettingOf(bus) * AudioBuses.Gain(bus));
            source.Play();
            Played?.Invoke(new PlayedSound { id = id, take = 0, takes = 1, pitch = 1f, volume = source.volume, bus = bus, time = Time.unscaledTime });
            return true;
        }

        /// <summary>A sound's clip (null while it has none, or isn't loaded yet).</summary>
        public static AudioClip ClipOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            _ = Config;
            return byId.TryGetValue(id, out var sound) ? sound.clip : null;
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

        /// <summary>Stops the lowest-priority playing copy of this id when a higher-priority one needs the slot.</summary>
        static bool TryStealSameId(string id, int priority)
        {
            if (pool == null) return false;
            int victim = -1;
            int lowest = priority;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null || !pool[i].isPlaying || poolIds[i] != id)
                    continue;
                if (poolPriority[i] < lowest)
                {
                    lowest = poolPriority[i];
                    victim = i;
                }
            }
            if (victim < 0)
                return false;
            pool[victim].Stop();
            poolIds[victim] = null;
            return true;
        }

        static AudioSource NextSource(int priority)
        {
            if (pool == null || pool[0] == null)
                CreatePool();
            // Prefer an idle source; otherwise take over the lowest-priority playing one (or the oldest if tied).
            for (int i = 0; i < pool.Length; i++)
            {
                int index = (next + i) % pool.Length;
                if (!pool[index].isPlaying)
                {
                    next = (index + 1) % pool.Length;
                    return pool[index];
                }
            }
            int victim = next;
            int lowest = poolPriority[next];
            for (int i = 0; i < pool.Length; i++)
            {
                int index = (next + i) % pool.Length;
                if (poolPriority[index] < lowest)
                {
                    lowest = poolPriority[index];
                    victim = index;
                }
            }
            if (priority < lowest)
                return null; // every playing voice is more important
            next = (victim + 1) % pool.Length;
            return pool[victim];
        }

        static void CreatePool()
        {
            var host = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(host);
            pool = new AudioSource[PoolSize];
            poolIds = new string[PoolSize];
            poolPriority = new int[PoolSize];
            poolBus = new Bus[PoolSize];
            poolAuthored = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                pool[i] = source;
            }
            GameSettings.Changed -= ReapplyPlaying;
            AudioBuses.Changed -= ReapplyPlaying;
            GameSettings.Changed += ReapplyPlaying;
            AudioBuses.Changed += ReapplyPlaying;
        }

        /// <summary>Pause mute / rewind trim / volume sliders: keep every live one-shot at the new bus gain.</summary>
        static void ReapplyPlaying()
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null || !pool[i].isPlaying)
                    continue;
                pool[i].volume = Mathf.Clamp01(poolAuthored[i] * SettingOf(poolBus[i]) * AudioBuses.Gain(poolBus[i]));
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
                    source.volume = Mathf.Clamp01(sound.volume * volume * GameSettings.VoiceVolume * AudioBuses.Gain(Bus.Voice));
                    source.pitch = 1f;
                    source.Play();
                    freeAt = now + sound.clip.length + Gap;
                    // Music ducks for as long as the line is spoken: this start is the event that does it (AudioBuses ends it by a timer).
                    if (GameSettings.SoundOn && GameSettings.VoiceVolume > 0.01f)
                        AudioBuses.VoiceStarted(sound.clip.length);
                    Played?.Invoke(new PlayedSound { id = sound.id, take = 0, takes = 1, pitch = 1f, volume = source.volume, bus = Bus.Voice, time = now });
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
