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
    /// Presentation only: it reads the simulation and never changes it.
    /// </summary>
    public sealed class StageMusic : MonoBehaviour
    {
        const float FadeSeconds = 0.8f;
        // The push track stays this long after a push ends, so a short breather doesn't flip the music back and forth.
        const float PushHoldSeconds = 2f;

        GameplayWorld world;
        AudioSource main;
        AudioSource tail;
        AudioClip levelClip;
        float levelVolume = 1f;
        AudioClip pushClip;
        float pushVolume = 1f;
        bool circleRead;
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

        public void Init(GameplayWorld owner, AudioSource source)
        {
            world = owner;
            main = source;
            levelClip = source.clip;
            levelVolume = volume = Volume.AuthoredOf(source);
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
            var clock = GameClock.Instance;
            if (clock != null && clock.IsRewinding)
            {
                // RewindableAudioSource plays the main track backward; the fading-out one would just be noise.
                if (!wasRewinding && tail != null)
                    tail.Stop();
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
        }

        void Switch(AudioClip to, float toVolume, bool fromTop, string why)
        {
            float at = main.time;
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
