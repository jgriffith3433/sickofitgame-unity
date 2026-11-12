using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core.Device;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// Positional ambience one-shots for a place: each config is a prop that fires its sound on a random 4–12 s gap,
    /// panned by screen x and faded by distance from the hero. On a phone (TouchDevice.IsPrimary) at most half as many
    /// play at once. Presentation only: not rewound; follows Effects volume and the Ambience bus trim.
    /// </summary>
    public static class AmbientEmitters
    {
        const int DesktopCap = 8;
        const float DefaultMin = 4f;
        const float DefaultMax = 12f;

        static Host host;

        /// <summary>How many emitter one-shots are playing right now (the playtest reads it).</summary>
        public static int PlayingCount => host != null ? host.Playing : 0;

        /// <summary>How many emitters this place has armed.</summary>
        public static int ArmedCount => host != null ? host.Armed : 0;

        /// <summary>Simultaneous cap for this device (phone = half).</summary>
        public static int Cap => TouchDevice.IsPrimary ? Mathf.Max(1, DesktopCap / 2) : DesktopCap;

        /// <summary>Last one-shot that started: sound id, pan (-1..1), volume (playtest).</summary>
        public static string LastId { get; private set; } = "";
        public static float LastPan { get; private set; }
        public static float LastVolume { get; private set; }
        public static int FiredTotal { get; private set; }

        /// <summary>Starts the place's emitters (replaces any previous set). Empty or null clears them.</summary>
        public static void Start(IList<AmbientEmitterConfig> configs)
        {
            if (!Application.isPlaying)
                return;
            FiredTotal = 0;
            LastId = "";
            LastPan = 0f;
            LastVolume = 0f;
            Get().Arm(configs);
        }

        /// <summary>Stops every emitter and frees sources.</summary>
        public static void Stop()
        {
            if (host != null)
                host.Arm(null);
            LastId = "";
            LastPan = 0f;
            LastVolume = 0f;
        }

        static Host Get()
        {
            if (host == null)
            {
                var go = new GameObject("[AmbientEmitters]");
                Object.DontDestroyOnLoad(go);
                host = go.AddComponent<Host>();
            }
            return host;
        }

        sealed class Slot
        {
            public AmbientEmitterConfig Config;
            public float NextAt;
            public AudioSource Source;
        }

        sealed class Host : MonoBehaviour
        {
            readonly List<Slot> slots = new List<Slot>(16);
            readonly System.Random rng = new System.Random(0xA3B1E77);

            public int Armed => slots.Count;
            public int Playing
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        if (slots[i].Source != null && slots[i].Source.isPlaying)
                            n++;
                    }
                    return n;
                }
            }

            public void Arm(IList<AmbientEmitterConfig> configs)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Source != null)
                    {
                        slots[i].Source.Stop();
                        Object.Destroy(slots[i].Source);
                    }
                }
                slots.Clear();
                if (configs == null)
                    return;
                float now = Time.unscaledTime;
                for (int i = 0; i < configs.Count; i++)
                {
                    var c = configs[i];
                    if (c == null || string.IsNullOrEmpty(c.sound))
                        continue;
                    float min = c.minInterval > 0f ? c.minInterval : DefaultMin;
                    float max = Mathf.Max(min, c.maxInterval > 0f ? c.maxInterval : DefaultMax);
                    var source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f; // stereo pan is set by hand from screen x
                    source.ignoreListenerPause = true;
                    slots.Add(new Slot
                    {
                        Config = c,
                        // Stagger the first fire so a place is alive within the first few seconds.
                        NextAt = now + min * (0.15f + 0.7f * (float)rng.NextDouble()) + i * 0.35f,
                        Source = source,
                    });
                }
            }

            void Update()
            {
                if (slots.Count == 0 || !GameSettings.SoundOn)
                    return;
                var clock = GameClock.Instance;
                if (clock != null && (clock.IsPaused || clock.IsRewinding))
                    return;
                float now = Time.unscaledTime;
                Vector2 hero = HeroPlace();
                Camera cam = Camera.main;
                int playing = Playing;
                int cap = Cap;
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot.Source != null && slot.Source.isPlaying)
                        continue;
                    if (now < slot.NextAt)
                        continue;
                    if (playing >= cap)
                    {
                        // Slip this one; try again soon so the place still fires 3+/min.
                        slot.NextAt = now + 0.6f + (float)rng.NextDouble();
                        continue;
                    }
                    if (TryFire(slot, hero, cam))
                        playing++;
                }
            }

            bool TryFire(Slot slot, Vector2 hero, Camera cam)
            {
                var c = slot.Config;
                float min = c.minInterval > 0f ? c.minInterval : DefaultMin;
                float max = Mathf.Max(min, c.maxInterval > 0f ? c.maxInterval : DefaultMax);
                slot.NextAt = Time.unscaledTime + min + (float)rng.NextDouble() * (max - min);

                var clip = Sfx.ClipOf(c.sound);
                if (clip == null)
                {
                    // Ask GameAssets once; the next gap will pick it up when loaded.
                    string scope = SickOfIt.Core.Addressables.GameAssets.Sound(c.sound);
                    if (SickOfIt.Core.Addressables.GameAssets.Knows(scope))
                        SickOfIt.Core.Addressables.GameAssets.Request(scope, null);
                    return false;
                }

                float range = c.range > 0.1f ? c.range : 10f;
                float dist = Vector2.Distance(hero, c.at);
                float falloff = 1f - Mathf.Clamp01(dist / range);
                if (falloff < 0.05f)
                    return false;

                float pan = 0f;
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(new Vector3(c.at.x, c.at.y, 0f));
                    pan = Mathf.Clamp(vp.x * 2f - 1f, -1f, 1f);
                }

                float authored = Mathf.Clamp01(c.volume) * falloff;
                float gain = GameSettings.SfxVolume * AudioBuses.Gain(Bus.Ambience);
                slot.Source.clip = clip;
                slot.Source.panStereo = pan;
                slot.Source.pitch = 1f + ((float)rng.NextDouble() * 2f - 1f) * 0.04f;
                slot.Source.volume = Mathf.Clamp01(authored * gain);
                slot.Source.Play();

                LastId = c.sound;
                LastPan = pan;
                LastVolume = slot.Source.volume;
                FiredTotal++;
                Sfx.Report(new Sfx.PlayedSound
                {
                    id = c.sound,
                    take = 0,
                    takes = 1,
                    pitch = slot.Source.pitch,
                    volume = slot.Source.volume,
                    bus = Bus.Ambience,
                    time = Time.unscaledTime,
                });
                return true;
            }

            static Vector2 HeroPlace()
            {
                var player = PlayerController.Active;
                if (player != null)
                    return player.transform.position;
                return Vector2.zero;
            }
        }
    }
}
