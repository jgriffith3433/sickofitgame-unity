using UnityEngine;
using SickOfIt.Core.Rewind;

namespace SickOfIt.Core
{
    /// <summary>
    /// Plays an AudioSource backward while the game clock rewinds, then
    /// leaves it at the matching timestamp when time runs forward again.
    /// </summary>
    public class RewindableAudioSource : BaseRewindable<RewindableAudioSource.AudioSnapshot>
    {
        [SerializeField] private AudioSource audioSource;

        private float savedPitch = 1f;
        private float savedVolume = 1f;
        private float fadeStartedAt;
        private const float FadeSeconds = 0.25f;

        public struct AudioSnapshot
        {
            public float time;
            public float playbackTime;
            public bool isPlaying;
            public float volume;
        }

        protected override void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            base.Awake();
        }

        protected override AudioSnapshot CaptureSnapshot(float time)
        {
            // A source whose clip was let go (VideoRuntime/DeathRuntime.ReleaseAudio) has no playhead: reading
            // AudioSource.time on it logs "not a clip" every step, which floods the log on WebGL.
            bool hasClip = audioSource != null && audioSource.clip != null;
            return new AudioSnapshot
            {
                time = time,
                playbackTime = hasClip ? audioSource.time : 0f,
                isPlaying = hasClip && audioSource.isPlaying,
                volume = audioSource != null ? audioSource.volume : 1f
            };
        }

        protected override void ApplySnapshot(AudioSnapshot snapshot)
        {
            if (audioSource == null || audioSource.clip == null) return;
            if (GameClock.Instance == null || !GameClock.Instance.IsRewinding) return;
            if (!snapshot.isPlaying) return;

            bool canReverse = audioSource.clip.loadType == AudioClipLoadType.DecompressOnLoad;
            if (!canReverse)
            {
                FadeOutCompressed();
                return;
            }

            if (!audioSource.isPlaying)
                audioSource.Play();

            float length = audioSource.clip.length;
            if (length <= 0.05f) return;

            float target = Mathf.Clamp(snapshot.playbackTime, 0f, length - 0.01f);
            float speed = Mathf.Max(0.01f, Mathf.Abs(GameClock.Instance.TimeScale));

            // Decompressed clips can actually play backward.
            audioSource.pitch = -speed;
            float drift = Mathf.Abs(audioSource.time - target);
            if (audioSource.loop)
                drift = Mathf.Min(drift, length - drift);
            if (drift > 0.15f)
                audioSource.time = target;
        }

        // Compressed clips ignore a negative pitch, and seeking one every few frames makes the browser or phone
        // decode from the start of a block each time. Fade it out instead (no seeks at all) and put it back at
        // the right place once, when the rewind stops.
        private void FadeOutCompressed()
        {
            float k = Mathf.Clamp01((Time.unscaledTime - fadeStartedAt) / FadeSeconds);
            audioSource.volume = Mathf.Lerp(savedVolume, 0f, k);
            if (k >= 1f && audioSource.isPlaying)
                audioSource.Pause();
        }

        private bool IsCompressed => audioSource != null && audioSource.clip != null &&
                                     audioSource.clip.loadType != AudioClipLoadType.DecompressOnLoad;

        protected override float GetSnapshotTime(AudioSnapshot snapshot) => snapshot.time;

        public override void OnRewindStart()
        {
            base.OnRewindStart();
            if (audioSource == null) return;

            savedPitch = audioSource.pitch > 0.01f ? audioSource.pitch : 1f;
            savedVolume = audioSource.volume;
            fadeStartedAt = Time.unscaledTime;
            if (!IsCompressed && !audioSource.isPlaying && audioSource.clip != null)
                audioSource.Play();
        }

        public override void OnRewindStop()
        {
            if (audioSource != null)
            {
                audioSource.pitch = savedPitch > 0.01f ? savedPitch : 1f;
                if (IsCompressed)
                {
                    // Faded out for the rewind: back to its volume, playing again (the position is set below).
                    audioSource.volume = savedVolume;
                    audioSource.UnPause();
                }
            }

            base.OnRewindStop();

            // Restore audio playback position to where it was at the current GameClock time
            if (audioSource == null || audioSource.clip == null || history.Count == 0) return;

            float currentTime = GameClock.Instance != null ? GameClock.Instance.Time : 0f;
            int nearest = history.FindNearestIndex(currentTime, SnapshotTime);
            if (nearest >= 0)
            {
                var snapshot = history[nearest];
                audioSource.time = snapshot.playbackTime;
                audioSource.volume = snapshot.volume;
                if (snapshot.isPlaying && !audioSource.isPlaying)
                    audioSource.Play();
            }
        }
    }
}
