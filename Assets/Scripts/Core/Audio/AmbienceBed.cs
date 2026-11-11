using System.Collections;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// The ambient bed under a place: one looping clip (wind, a far rooster and a mill for the Meadow; lava and stone creaks
    /// for Floor 1000) on the Ambience bus, started when the level loads (LevelConfig.ambientSound / ambientVolume) and swapped
    /// by the level script's <c>setAmbience</c> action (the Meadow's burning bed with the bells). A swap cross-fades over its
    /// own time (a coroutine, no polling). It follows the Effects volume and the Ambience bus trim and is reported through
    /// <see cref="Sfx.Played"/> like any sound start. Presentation only: not rewound (a rewind past a swap keeps the new bed).
    /// </summary>
    public static class AmbienceBed
    {
        static Host host;

        /// <summary>The id (file name) of the bed that is playing now, or "" for none.</summary>
        public static string Current { get; private set; } = "";

        /// <summary>The bed's current volume at the speaker (0 when none plays): the playtest reads it.</summary>
        public static float Volume => host != null ? host.Level : 0f;

        public static bool Playing => host != null && host.Active;

        /// <summary>Starts (or cross-fades to) this clip. 'authoredVolume' is LevelConfig.ambientVolume.</summary>
        public static void Play(AudioClip clip, float authoredVolume, float fadeSeconds = 1.5f)
        {
            if (clip == null || !Application.isPlaying)
                return;
            if (Current == clip.name && host != null && host.Active)
                return;
            Current = clip.name;
            Get().Switch(clip, authoredVolume, fadeSeconds);
            Sfx.Report(new Sfx.PlayedSound { id = clip.name, take = 0, takes = 1, pitch = 1f, volume = Mathf.Clamp01(authoredVolume * GameSettings.SfxVolume * AudioBuses.Gain(Bus.Ambience)), bus = Bus.Ambience, time = Time.unscaledTime });
        }

        /// <summary>Starts the bed that is an audio.json sound (kind Effect, loop true, bus Ambience): the level script's setAmbience.</summary>
        public static void PlaySound(string soundId, float fadeSeconds)
        {
            var clip = Sfx.ClipOf(soundId);
            if (clip == null)
                return;
            float volume = 1f;
            var def = GameDefinition.Instance != null && GameDefinition.Instance.audio != null ? GameDefinition.Instance.audio.Find(soundId) : null;
            if (def != null)
                volume = def.volume;
            Play(clip, volume, fadeSeconds);
        }

        /// <summary>Fades the bed out and lets go of it (a level ends).</summary>
        public static void Stop(float fadeSeconds = 0.6f)
        {
            Current = "";
            if (host != null)
                host.FadeOut(fadeSeconds);
        }

        static Host Get()
        {
            if (host == null)
            {
                var go = new GameObject("[Ambience]");
                Object.DontDestroyOnLoad(go);
                host = go.AddComponent<Host>();
            }
            return host;
        }

        sealed class Host : MonoBehaviour
        {
            readonly AudioSource[] source = new AudioSource[2];
            readonly float[] authored = new float[2];
            readonly float[] weight = new float[2];
            int active;
            Coroutine fade;

            public bool Active => source[active] != null && source[active].isPlaying;
            public float Level => source[active] != null ? source[active].volume : 0f;

            void Awake()
            {
                for (int i = 0; i < 2; i++)
                {
                    source[i] = gameObject.AddComponent<AudioSource>();
                    source[i].playOnAwake = false;
                    source[i].loop = true;
                    source[i].spatialBlend = 0f;
                }
            }

            void OnEnable()
            {
                GameSettings.Changed += Apply;
                AudioBuses.Changed += Apply;
            }

            void OnDisable()
            {
                GameSettings.Changed -= Apply;
                AudioBuses.Changed -= Apply;
            }

            public void Switch(AudioClip clip, float authoredVolume, float seconds)
            {
                int from = active;
                int to = 1 - active;
                active = to;
                source[to].clip = clip;
                source[to].loop = true;
                authored[to] = authoredVolume;
                weight[to] = 0f;
                source[to].Play();
                if (fade != null)
                    StopCoroutine(fade);
                fade = StartCoroutine(Fade(from, to, seconds));
            }

            public void FadeOut(float seconds)
            {
                if (fade != null)
                    StopCoroutine(fade);
                fade = StartCoroutine(Fade(active, -1, seconds));
            }

            IEnumerator Fade(int from, int to, float seconds)
            {
                float fromStart = weight[from];
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / Mathf.Max(0.01f, seconds));
                    weight[from] = fromStart * (1f - k);
                    if (to >= 0)
                        weight[to] = k;
                    Apply();
                    yield return null;
                }
                weight[from] = 0f;
                if (to >= 0)
                    weight[to] = 1f;
                Apply();
                if (source[from] != null)
                    source[from].Stop();
                if (to < 0)
                    Current = "";
                fade = null;
            }

            void Apply()
            {
                float k = GameSettings.SfxVolume * AudioBuses.Gain(Bus.Ambience);
                for (int i = 0; i < 2; i++)
                {
                    if (source[i] != null)
                        source[i].volume = Mathf.Clamp01(authored[i] * weight[i] * k);
                }
            }
        }
    }
}
