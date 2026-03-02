using SickOfIt.Core;
using UnityEngine;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Rewind input handler. Hold the rewind button to rewind time.
    /// 
    /// The rewind button is wired at generation time directly from
    /// the MobileController prefab — no runtime lookups needed.
    /// The actual state restore is handled by RewindTimeline.
    /// The actual time manipulation is handled by GameClock.
    /// </summary>
    public class RewindController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float rewindSpeed = 3f;
        [Tooltip("Minimum time (seconds) that must have passed before rewind is available")]
        [SerializeField] private float minimumTimeBeforeRewind = 2f;

        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;

        [Header("Mobile Controls (wired by generator from MobileController prefab)")]
        [Tooltip("Hold-to-rewind button.")]
        [SerializeField] private MobileControllerFramework.Button rewindButton;

        private bool isRewinding;

        private void OnEnable()
        {
            if (rewindButton != null)
            {
                rewindButton.TapStartEvent.AddListener(StartRewind);
                rewindButton.TapEndEvent.AddListener(StopRewind);
            }
        }

        private void OnDisable()
        {
            if (rewindButton != null)
            {
                rewindButton.TapStartEvent.RemoveListener(StartRewind);
                rewindButton.TapEndEvent.RemoveListener(StopRewind);
            }
        }

        public void StartRewind()
        {
            if (isRewinding) return;
            if (GameClock.Instance == null) return;
            if (GameClock.Instance.Time < minimumTimeBeforeRewind) return;

            isRewinding = true;
            GameClock.Instance.StartRewind(rewindSpeed);

            // Mute game music during rewind
            if (musicSource != null)
                musicSource.Pause();

            Debug.Log("[Rewind] Started");
        }

        public void StopRewind()
        {
            if (!isRewinding) return;

            isRewinding = false;
            if (GameClock.Instance != null)
                GameClock.Instance.StopRewind();

            // Resume game music
            if (musicSource != null)
                musicSource.UnPause();

            Debug.Log($"[Rewind] Stopped at t={GameClock.Instance?.Time:F1}s");
        }
    }
}
