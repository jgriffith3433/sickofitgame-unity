using SickOfIt.Core;
using UnityEngine;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Rewind input handler and visual effects controller.
    /// Uses MobileControllerFramework Button for input (same as all other game controls).
    /// 
    /// Hold the rewind button to rewind. Release to resume.
    /// 
    /// Visual effects during rewind:
    ///   - VHS scanline overlay
    ///   - Rewind icon indicator
    /// 
    /// The actual state restore is handled by StateRecorder.
    /// The actual time manipulation is handled by GameClock.
    /// </summary>
    public class RewindController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("MobileControllerFramework Button for rewind")]
        [SerializeField] private MobileControllerFramework.Button rewindButton;

        [Header("Rewind Settings")]
        [SerializeField] private float rewindSpeed = 3f;
        [Tooltip("Minimum time (seconds) that must have passed before rewind is available")]
        [SerializeField] private float minimumTimeBeforeRewind = 2f;

        [Header("Visual Effects")]
        [Tooltip("Overlay sprite for VHS scanline effect during rewind")]
        [SerializeField] private GameObject rewindOverlay;
        [Tooltip("Rewind icon/indicator UI")]
        [SerializeField] private GameObject rewindIndicator;
        [Tooltip("Color tint during rewind")]
        [SerializeField] private Color rewindTint = new Color(0.8f, 0.9f, 1f, 0.3f);

        [Header("Audio")]
        [SerializeField] private AudioClip rewindStartSound;
        [SerializeField] private AudioClip rewindLoopSound;
        [SerializeField] private AudioClip rewindStopSound;

        private AudioSource audioSource;
        private bool isRewinding;
        private SpriteRenderer overlayRenderer;

        private void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            if (rewindOverlay != null)
            {
                rewindOverlay.SetActive(false);
                overlayRenderer = rewindOverlay.GetComponent<SpriteRenderer>();
            }

            if (rewindIndicator != null)
                rewindIndicator.SetActive(false);

            // Auto-discover rewind button if not assigned
            if (rewindButton == null)
            {
                var buttons = Object.FindObjectsByType<MobileControllerFramework.Button>(FindObjectsSortMode.None);
                foreach (var btn in buttons)
                {
                    if (btn.gameObject.name.ToLower().Contains("rewind"))
                    {
                        rewindButton = btn;
                        break;
                    }
                }
            }

            if (rewindButton != null)
            {
                rewindButton.TapStartEvent.AddListener(StartRewind);
                rewindButton.TapEndEvent.AddListener(StopRewind);
            }
        }

        private void OnDestroy()
        {
            if (rewindButton != null)
            {
                rewindButton.TapStartEvent.RemoveListener(StartRewind);
                rewindButton.TapEndEvent.RemoveListener(StopRewind);
            }
        }

        private void Update()
        {
            if (isRewinding)
                UpdateRewindVisuals();
        }

        public void StartRewind()
        {
            if (isRewinding) return;
            if (GameClock.Instance == null) return;
            if (GameClock.Instance.Time < minimumTimeBeforeRewind) return;

            isRewinding = true;
            GameClock.Instance.StartRewind(rewindSpeed);

            // Visuals
            if (rewindOverlay != null)
                rewindOverlay.SetActive(true);
            if (rewindIndicator != null)
                rewindIndicator.SetActive(true);

            // Audio
            if (rewindStartSound != null)
                audioSource.PlayOneShot(rewindStartSound);
            if (rewindLoopSound != null)
            {
                audioSource.clip = rewindLoopSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            // Mute game music during rewind
            var musicSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var src in musicSources)
            {
                if (src != audioSource && src.gameObject.name.Contains("Music"))
                    src.Pause();
            }

            Debug.Log("[Rewind] Started");
        }

        public void StopRewind()
        {
            if (!isRewinding) return;

            isRewinding = false;
            if (GameClock.Instance != null)
                GameClock.Instance.StopRewind();

            // Visuals
            if (rewindOverlay != null)
                rewindOverlay.SetActive(false);
            if (rewindIndicator != null)
                rewindIndicator.SetActive(false);

            // Audio
            audioSource.Stop();
            if (rewindStopSound != null)
                audioSource.PlayOneShot(rewindStopSound);

            // Resume game music
            var musicSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var src in musicSources)
            {
                if (src != audioSource && src.gameObject.name.Contains("Music"))
                    src.UnPause();
            }

            Debug.Log($"[Rewind] Stopped at t={GameClock.Instance?.Time:F1}s");
        }

        private void UpdateRewindVisuals()
        {
            // Pulse the overlay opacity for VHS effect
            if (overlayRenderer != null)
            {
                float alpha = 0.1f + Mathf.PingPong(Time.unscaledTime * 2f, 0.15f);
                var c = rewindTint;
                c.a = alpha;
                overlayRenderer.color = c;
            }
        }
    }
}
