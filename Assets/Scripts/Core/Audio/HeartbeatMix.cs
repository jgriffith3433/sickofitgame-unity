using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// Low-health mix: at one heart a looping heartbeat plays and the LowHealth snapshot (Music -4 dB) is on; it stops when
    /// he heals or dies. Prefers audio.json's <c>player_heartbeat</c> clip when Gen has made it; otherwise a tiny procedural
    /// pulse so the mix is audible without an imported file (sparse worktrees cannot always hold Audio/Generated).
    /// Presentation only; not rewound (a rewind that restores hearts turns it off through the health event).
    /// </summary>
    public static class HeartbeatMix
    {
        const string FallbackId = "player_heartbeat";
        const float AuthoredVolume = 0.55f;

        static Host host;
        static PlayerHealth bound;
        static AudioClip procedural;

        /// <summary>True while the heartbeat loop is audible.</summary>
        public static bool Playing => host != null && host.Active;

        /// <summary>Follow this health: one heart turns the loop and LowHealth snapshot on.</summary>
        public static void Bind(PlayerHealth health)
        {
            if (bound == health)
                return;
            Unbind();
            bound = health;
            if (bound == null)
                return;
            bound.onHealthChanged.AddListener(OnHealth);
            OnHealth(bound.CurrentHealth);
        }

        public static void Unbind()
        {
            if (bound != null)
                bound.onHealthChanged.RemoveListener(OnHealth);
            bound = null;
            Stop();
        }

        static void OnHealth(int hearts)
        {
            if (hearts == 1 && bound != null && bound.IsAlive)
                Start();
            else
                Stop();
        }

        static void Start()
        {
            AudioBuses.SetLowHealth(true);
            var clip = Clip();
            if (clip == null)
                return;
            Get().Play(clip);
            Sfx.Report(new Sfx.PlayedSound
            {
                id = FallbackId,
                take = 0,
                takes = 1,
                pitch = 1f,
                volume = Mathf.Clamp01(AuthoredVolume * GameSettings.SfxVolume * AudioBuses.Gain(Bus.Sfx)),
                bus = Bus.Sfx,
                time = Time.unscaledTime,
            });
        }

        static void Stop()
        {
            AudioBuses.SetLowHealth(false);
            if (host != null)
                host.Stop();
        }

        static AudioClip Clip()
        {
            var fromJson = Sfx.ClipOf(FallbackId);
            if (fromJson != null)
                return fromJson;
            if (GameDefinition.Instance?.audio != null)
            {
                string id = GameDefinition.Instance.audio.events?.lowHealthHeartbeat;
                if (!string.IsNullOrEmpty(id) && id != FallbackId)
                {
                    var named = Sfx.ClipOf(id);
                    if (named != null)
                        return named;
                }
            }
            return Procedural();
        }

        static AudioClip Procedural()
        {
            if (procedural != null)
                return procedural;
            const int sr = 22050;
            const float seconds = 1f;
            int n = Mathf.RoundToInt(sr * seconds);
            var data = new float[n];
            // Two soft thumps per second (lub-dub), dry and close — stand-in until Gen remakes player_heartbeat.
            Pulse(data, sr, 0.05f, 48f, 0.55f);
            Pulse(data, sr, 0.22f, 36f, 0.35f);
            procedural = AudioClip.Create(FallbackId, n, 1, sr, false);
            procedural.SetData(data, 0);
            return procedural;
        }

        static void Pulse(float[] data, int sr, float at, float hz, float amp)
        {
            int start = Mathf.RoundToInt(at * sr);
            int len = Mathf.RoundToInt(0.09f * sr);
            for (int i = 0; i < len && start + i < data.Length; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 38f);
                data[start + i] += amp * env * Mathf.Sin(2f * Mathf.PI * hz * t);
            }
        }

        static Host Get()
        {
            if (host == null)
            {
                var go = new GameObject("[Heartbeat]");
                Object.DontDestroyOnLoad(go);
                host = go.AddComponent<Host>();
            }
            return host;
        }

        sealed class Host : MonoBehaviour
        {
            AudioSource source;
            float authored = AuthoredVolume;

            public bool Active => source != null && source.isPlaying;

            void Awake()
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
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

            public void Play(AudioClip clip)
            {
                if (source.isPlaying && source.clip == clip)
                {
                    Apply();
                    return;
                }
                source.clip = clip;
                source.loop = true;
                source.pitch = 1f;
                Apply();
                source.Play();
            }

            public void Stop()
            {
                if (source != null && source.isPlaying)
                    source.Stop();
            }

            void Apply()
            {
                if (source == null) return;
                // Heartbeat rides SFX loudness but ignores the Pause mute (you still hear it under the pause menu if
                // somehow paused at 1 HP); Rewind still ducks it with SFX so the whoosh leads.
                float g = AudioBuses.RewindActive
                    ? AudioBuses.Gain(Bus.Sfx)
                    : Mathf.Pow(10f, AudioBuses.TrimDb(Bus.Sfx) / 20f);
                if (AudioBuses.PauseActive)
                    g = Mathf.Pow(10f, AudioBuses.TrimDb(Bus.Sfx) / 20f);
                source.volume = Mathf.Clamp01(authored * GameSettings.SfxVolume * g);
            }
        }
    }
}
