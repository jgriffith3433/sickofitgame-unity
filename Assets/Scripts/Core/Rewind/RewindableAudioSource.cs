using UnityEngine;
using SickOfIt.Core.Rewind;

namespace SickOfIt.Core
{
    /// <summary>
    /// Records and restores AudioSource playback position during rewind.
    /// When rewinding, the music scrubs backward to the correct timestamp.
    /// 
    /// Attach to the same GameObject as the level music AudioSource.
    /// RewindController already pauses music during rewind — this component
    /// ensures the playback position is correct when rewind stops.
    /// </summary>
    public class RewindableAudioSource : BaseRewindable<RewindableAudioSource.AudioSnapshot>
    {
        [SerializeField] private AudioSource audioSource;

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
            if (audioSource == null) return;
            // Don't play during rewind — just track the position.
            // RewindController handles muting. We restore position on stop.
        }

        protected override float GetSnapshotTime(AudioSnapshot snapshot) => snapshot.time;

        public override void OnRewindStop()
        {
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
