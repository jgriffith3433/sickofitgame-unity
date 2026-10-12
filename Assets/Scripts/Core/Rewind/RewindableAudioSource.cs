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
        private float nextScrubTime;

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
            return new AudioSnapshot
            {
                time = time,
                playbackTime = audioSource != null ? audioSource.time : 0f,
                isPlaying = audioSource != null && audioSource.isPlaying,
                volume = audioSource != null ? audioSource.volume : 1f
            };
        }

        protected override void ApplySnapshot(AudioSnapshot snapshot)
        {
            if (audioSource == null || audioSource.clip == null) return;
            if (GameClock.Instance == null || !GameClock.Instance.IsRewinding) return;
            if (!snapshot.isPlaying) return;

            if (!audioSource.isPlaying)
                audioSource.Play();

            float length = audioSource.clip.length;
            if (length <= 0.05f) return;

            float target = Mathf.Clamp(snapshot.playbackTime, 0f, length - 0.01f);
            bool canReverse = audioSource.clip.loadType == AudioClipLoadType.DecompressOnLoad;
            float speed = Mathf.Max(0.01f, Mathf.Abs(GameClock.Instance.TimeScale));

            if (canReverse)
            {
                // Decompressed clips can actually play backward.
                audioSource.pitch = -speed;
                float drift = Mathf.Abs(audioSource.time - target);
                if (audioSource.loop)
                    drift = Mathf.Min(drift, length - drift);
                if (drift > 0.15f)
                    audioSource.time = target;
                return;
            }

            // Compressed clips ignore a negative pitch, so step the playhead
            // backward and let each step be heard.
            audioSource.pitch = savedPitch > 0.01f ? savedPitch : 1f;
            if (Time.unscaledTime < nextScrubTime) return;
            nextScrubTime = Time.unscaledTime + 0.05f;
            audioSource.time = target;
        }

        protected override float GetSnapshotTime(AudioSnapshot snapshot) => snapshot.time;

        public override void OnRewindStart()
        {
            base.OnRewindStart();
            if (audioSource == null) return;

            savedPitch = audioSource.pitch > 0.01f ? audioSource.pitch : 1f;
            nextScrubTime = 0f;
            if (!audioSource.isPlaying && audioSource.clip != null)
                audioSource.Play();
        }

        public override void OnRewindStop()
        {
            if (audioSource != null)
                audioSource.pitch = savedPitch > 0.01f ? savedPitch : 1f;

            base.OnRewindStop();

            // Restore audio playback position to where it was at the current GameClock time
            if (audioSource == null || history.Count == 0) return;

            float currentTime = GameClock.Instance != null ? GameClock.Instance.Time : 0f;
            int nearest = history.FindNearestIndex(currentTime, GetSnapshotTime);
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
