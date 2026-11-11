using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Gameplay.Content;
using Volume = SickOfIt.Core.Audio.MusicVolume;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Which music a scripted level plays (Docs/Story.md, "Music"): the level's own track; a script's playMusic (a
    /// Warden's theme, its enrage); and on floor stages the Circle's pushMusic while a generated push is on, dropping
    /// back a moment after the floor goes quiet. Tracks cross-fade; between the level's track and the push track the
    /// playhead carries over, so the beat goes on. The script's choice is saved with the world (WorldEffects.SaveState),
    /// so a rewind past a playMusic brings the old track back, and the push track follows the generator's saved state.
    /// The Circle's intensity layer (Circle.intensityMusic) rides over the level's own track: it fades in as enemies and hostile
    /// shots crowd the world and out as it empties, and stays out under a push track or a Warden's theme.
    /// Presentation only: it reads the simulation and never changes it.
    /// </summary>
    public sealed class StageMusic : MonoBehaviour
    {
        const float FadeSeconds = 0.8f;
        // The push track stays this long after a push ends, so a short breather doesn't flip the music back and forth.
        const float PushHoldSeconds = 2f;
        // The intensity layer: how fast it comes in when the world crowds, how slowly it leaves, how often the crowd is counted.
        const float LayerRiseSeconds = 1.5f;
        const float LayerFallSeconds = 4f;
        const float CrowdEvery = 0.1f;
        // A layer of the same length as the track is kept in step with it when it drifts more than this.
        const float LayerDriftSeconds = 0.12f;

        GameplayWorld world;
        AudioSource main;
        AudioSource tail;
        AudioClip levelClip;
        float levelVolume = 1f;
        AudioClip pushClip;
        float pushVolume = 1f;
        bool circleRead;
        AudioClip layerClip;
        float layerVolume = 1f;
        float layerEnemyWeight = 1f, layerBulletWeight = 0.35f, layerLow = 3f, layerHigh = 14f;
        AudioSource layer;
        float layerLevel;
        int layerReported = -1;
        float crowd;
        float nextCrowd;
        // Every clip a playMusic asked for in this level; the saved cue is an index into it.
        readonly List<AudioClip> clips = new List<AudioClip>();

        // ─── Saved state (through WorldEffects) ───────────────────────
        int cue = -1;
        float cueVolume = 1f;

        // ─── Presentation ─────────────────────────────────────────────
        bool restart;
        bool started;
        float pushUntil = -1f;
        float volume = 1f;
        float tailVolume;
        float fadeStart = -1f;
        bool wasRewinding;

        // ─── The rewind variant (Docs/Story.md, "Music") ──────────────
        // A compressed (streaming) track can't play backward, so RewindableAudioSource fades it out for a rewind. In its place
        // plays the same track, bent: slowed like a tape being dragged, through a low-pass filter. It comes in on the clock's
        // OnRewindStarted and goes on OnRewindStopped (events, nothing polled), while the normal track is put back by the
        // rewindable source in the same frame the rewind ends.
        const float RewindVariantVolume = 0.7f;
        const float RewindVariantInSeconds = 0.12f;
        const float RewindVariantOutSeconds = 0.15f;
        const float RewindPitchStart = 0.85f;
        const float RewindPitchEnd = 0.6f;
        const float RewindPitchSeconds = 3f;
        const float RewindCutoffHz = 700f;
        AudioSource rewindSrc;
        AudioLowPassFilter rewindFilter;
        float rewindLevel;
        float rewindTarget;
        float rewindElapsed;
        GameClock subscribedClock;

        /// <summary>The bent rewind variant of the music is audible right now (playtest and tests read this).</summary>
        /// <summary>The normal track is playing and audible (not faded out or paused for a rewind).</summary>
        public bool NormalTrackAudible => main != null && main.isPlaying && main.volume > 0.02f;

        /// <summary>The clip the normal track is playing now.</summary>
        public AudioClip NormalTrackClip => main != null ? main.clip : null;

        public bool RewindVariantPlaying => rewindSrc != null && rewindSrc.isPlaying && rewindTarget > 0f;

        public void Init(GameplayWorld owner, AudioSource source)
        {
            world = owner;
            main = source;
            levelClip = source.clip;
            levelVolume = volume = Volume.AuthoredOf(source);
            Subscribe();
        }

        void Subscribe()
        {
            var clock = GameClock.Instance;
            if (clock == null || clock == subscribedClock)
                return;
            subscribedClock = clock;
            clock.OnRewindStarted += BeginRewindVariant;
            clock.OnRewindStopped += EndRewindVariant;
        }

        void OnDestroy()
        {
            if (subscribedClock != null)
            {
                subscribedClock.OnRewindStarted -= BeginRewindVariant;
                subscribedClock.OnRewindStopped -= EndRewindVariant;
            }
        }

        void BeginRewindVariant()
        {
            if (main == null || main.clip == null)
                return;
            if (rewindSrc == null)
            {
                var go = new GameObject("MusicRewind");
                go.transform.SetParent(main.transform, false);
                rewindSrc = go.AddComponent<AudioSource>();
                rewindSrc.playOnAwake = false;
                rewindSrc.spatialBlend = 0f;
                rewindSrc.dopplerLevel = 0f;
                rewindSrc.loop = true;
                rewindFilter = go.AddComponent<AudioLowPassFilter>();
                rewindFilter.cutoffFrequency = RewindCutoffHz;
                rewindFilter.lowpassResonanceQ = 1.2f;
            }
            rewindSrc.clip = main.clip;
            rewindSrc.pitch = RewindPitchStart;
            Volume.Set(rewindSrc, 0f);
            rewindSrc.Play();
            if (main.clip.length > 0.1f)
                rewindSrc.time = Mathf.Min(main.time, main.clip.length - 0.05f);
            rewindElapsed = 0f;
            rewindLevel = 0f;
            rewindTarget = 1f;
            world?.Telemetry?.Record("musicRewind", main.clip.name, 1, "variant on");
        }

        void EndRewindVariant()
        {
            if (rewindSrc == null || rewindTarget <= 0f)
                return;
            rewindTarget = 0f;
            world?.Telemetry?.Record("musicRewind", rewindSrc.clip != null ? rewindSrc.clip.name : "", 0, "variant off");
        }

        void TickRewindVariant()
        {
            if (rewindSrc == null || !rewindSrc.isPlaying)
                return;
            float dt = Time.unscaledDeltaTime;
            rewindElapsed += dt;
            float speed = dt / (rewindTarget > rewindLevel ? RewindVariantInSeconds : RewindVariantOutSeconds);
            rewindLevel = Mathf.MoveTowards(rewindLevel, rewindTarget, speed);
            rewindSrc.pitch = Mathf.Lerp(RewindPitchStart, RewindPitchEnd, Mathf.Clamp01(rewindElapsed / RewindPitchSeconds));
            Volume.Set(rewindSrc, volume * RewindVariantVolume * rewindLevel);
            if (rewindTarget <= 0f && rewindLevel <= 0f)
                rewindSrc.Stop();
        }

        /// <summary>The script's playMusic: this track from the top, until another playMusic (or a rewind past this one).</summary>
        public void Play(AudioClip clip, float clipVolume)
        {
            int index = clips.IndexOf(clip);
            if (index < 0)
            {
                clips.Add(clip);
                index = clips.Count - 1;
            }
            cue = index;
            cueVolume = clipVolume;
            restart = true;
            // The level's music may not have started yet (a phase's onEnter runs as the level begins): just queue it.
            if (!main.isPlaying)
            {
                main.clip = clip;
                main.loop = true;
                volume = clipVolume;
                Volume.Set(main, clipVolume);
                main.Play();
                restart = false;
                started = true;
                Report(clip, "script");
            }
            else
            {
                Refresh();
            }
        }

        /// <summary>The script's musicVolume: the track playing now, at this volume.</summary>
        public void SetVolume(float to)
        {
            volume = to;
            if (fadeStart < 0f)
                Volume.Set(main, to);
        }

        public void SaveState(StateWriter w)
        {
            w.Int(cue);
            w.Float(cueVolume);
        }

        public void LoadState(StateReader r)
        {
            cue = r.Int();
            cueVolume = r.Float();
            restart = false;
        }

        void Update()
        {
            if (main == null)
                return;
            if (subscribedClock == null)
                Subscribe();
            // Only moves while the variant is audible (it fades in and out on the clock's events).
            TickRewindVariant();
            var clock = GameClock.Instance;
            if (clock != null && clock.IsRewinding)
            {
                // RewindableAudioSource plays the main track backward; the fading-out one would just be noise.
                if (!wasRewinding && tail != null)
                    tail.Stop();
                if (!wasRewinding && layer != null)
                    layer.Stop();
                wasRewinding = true;
                return;
            }
            if (wasRewinding)
            {
                // The rewind put back the volume it saved, maybe from the middle of a fade: ours again.
                wasRewinding = false;
                fadeStart = -1f;
                Volume.Set(main, volume);
            }
            Refresh();
            TickFade();
        }

        void Refresh()
        {
            if (!main.isPlaying)
                return;
            if (!started)
            {
                started = true;
                Report(main.clip, "start");
            }
            if (!circleRead && world != null && world.Stage != null)
            {
                circleRead = true;
                var circle = world.Stage.Circle;
                if (circle != null)
                {
                    pushClip = circle.pushMusic;
                    pushVolume = circle.pushMusicVolume;
                    layerClip = circle.intensityMusic;
                    layerVolume = circle.intensityMusicVolume;
                    layerEnemyWeight = circle.intensityEnemyWeight;
                    layerBulletWeight = circle.intensityBulletWeight;
                    layerLow = circle.intensityLow;
                    layerHigh = Mathf.Max(circle.intensityHigh, circle.intensityLow + 0.1f);
                }
            }
            if (pushClip != null && world != null && world.Director != null && world.Director.GeneratorPushing)
                pushUntil = Time.unscaledTime + PushHoldSeconds;

            AudioClip want;
            float wantVolume;
            string why;
            if (cue >= 0 && cue < clips.Count)
            {
                want = clips[cue];
                wantVolume = cueVolume;
                why = "script";
            }
            else if (pushClip != null && Time.unscaledTime < pushUntil)
            {
                want = pushClip;
                wantVolume = pushVolume;
                why = "push";
            }
            else
            {
                want = levelClip;
                wantVolume = levelVolume;
                why = "level";
            }
            if (want != null && want != main.clip)
                Switch(want, wantVolume, restart, why);
            restart = false;
            TickLayer(want == levelClip && want == main.clip);
        }

        /// <summary>Enemies and hostile shots alive now, weighted by the Circle's numbers (counted a few times a second).</summary>
        float Crowd()
        {
            if (world == null)
                return 0f;
            float total = 0f;
            var live = world.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var e = live[i];
                if (e == null || !e.IsAlive || e.IsPlayer || e.Team != Team.Enemy)
                    continue;
                total += e.FindAbility<ProjectileAbility>() != null ? layerBulletWeight : layerEnemyWeight;
            }
            return total;
        }

        void TickLayer(bool overLevelTrack)
        {
            if (layerClip == null)
                return;
            if (Time.unscaledTime >= nextCrowd)
            {
                nextCrowd = Time.unscaledTime + CrowdEvery;
                crowd = Crowd();
            }
            float target = overLevelTrack ? Mathf.Clamp01((crowd - layerLow) / (layerHigh - layerLow)) : 0f;
            float speed = Time.unscaledDeltaTime / (target > layerLevel ? LayerRiseSeconds : LayerFallSeconds);
            layerLevel = Mathf.MoveTowards(layerLevel, target, speed);

            if (layer == null)
            {
                var go = new GameObject("MusicLayer");
                go.transform.SetParent(main.transform, false);
                layer = go.AddComponent<AudioSource>();
                layer.playOnAwake = false;
                layer.spatialBlend = 0f;
                layer.dopplerLevel = 0f;
                layer.loop = true;
                layer.clip = layerClip;
            }
            // It plays silently the whole time (so it is already in step when it comes in) and only its volume moves.
            if (!layer.isPlaying)
            {
                layer.Play();
                layer.time = main.clip != null && main.clip.length > 0.1f ? main.time % Mathf.Max(layerClip.length - 0.05f, 0.1f) : 0f;
            }
            else if (main.clip != null && Mathf.Abs(layerClip.length - main.clip.length) < 0.05f && Mathf.Abs(layer.time - main.time) > LayerDriftSeconds)
            {
                layer.time = Mathf.Min(main.time, layerClip.length - 0.05f);
            }
            Volume.Set(layer, layerVolume * layerLevel);

            int steps = Mathf.RoundToInt(layerLevel * 4f) * 25;
            if (steps != layerReported)
            {
                layerReported = steps;
                world?.Telemetry?.Record("musicLayer", layerClip.name, steps, "crowd " + crowd.ToString("0.0"));
            }
        }

        void Switch(AudioClip to, float toVolume, bool fromTop, string why)
        {
            float at = main.clip != null ? main.time : 0f;
            if (main.clip != null && main.isPlaying)
            {
                if (tail == null)
                {
                    var go = new GameObject("MusicTail");
                    go.transform.SetParent(main.transform, false);
                    tail = go.AddComponent<AudioSource>();
                    tail.playOnAwake = false;
                    tail.spatialBlend = 0f;
                    tail.dopplerLevel = 0f;
                }
                tail.clip = main.clip;
                tail.loop = false;
                tailVolume = Volume.AuthoredOf(main);
                Volume.Set(tail, tailVolume);
                tail.Play();
                tail.time = Mathf.Min(at, Mathf.Max(0f, main.clip.length - 0.05f));
            }

            main.clip = to;
            main.loop = true;
            Volume.Set(main, 0f);
            main.Play();
            if (!fromTop && to.length > 0.1f)
                main.time = at % to.length;
            volume = toVolume;
            fadeStart = Time.unscaledTime;
            Report(to, why);
        }

        void TickFade()
        {
            if (fadeStart < 0f)
                return;
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / FadeSeconds);
            Volume.Set(main, volume * t);
            if (tail != null && tail.isPlaying)
                Volume.Set(tail, tailVolume * (1f - t));
            if (t >= 1f)
            {
                fadeStart = -1f;
                if (tail != null)
                    tail.Stop();
            }
        }

        void Report(AudioClip clip, string why) => world?.Telemetry?.Record("music", clip != null ? clip.name : "", 0, why);
    }
}
