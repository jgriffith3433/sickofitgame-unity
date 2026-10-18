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
            SickOfIt.Core.Rewind.RewindInput.Register(StartRewind, StopRewind);
            if (rewindButton != null)
            {
                rewindButton.TapStartEvent.AddListener(StartRewind);
                rewindButton.TapEndEvent.AddListener(StopRewind);
            }
        }

        private void OnDisable()
        {
            SickOfIt.Core.Rewind.RewindInput.Unregister(StartRewind);
            if (rewindButton != null)
            {
                rewindButton.TapStartEvent.RemoveListener(StartRewind);
                rewindButton.TapEndEvent.RemoveListener(StopRewind);
            }
        }

        public void StartRewind()
        {
            if (GameClock.Instance == null) return;
            // The clock can stop itself at time zero. Trust that, not a stale local flag.
            if (GameClock.Instance.IsRewinding) return;
            if (GameClock.Instance.Time < minimumTimeBeforeRewind) return;
            if (GameClock.Instance.IsPaused) return;
            // A scripted scene holds the loop (lockRewind): nothing happens until it lets go.
            if (SickOfIt.Core.Rewind.RewindInput.Locked) return;
            // Out of rewind juice: nothing happens until a pickup refills it.
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            if (budget != null && !budget.CanRewind) return;

            isRewinding = true;
            GameClock.Instance.StartRewind(rewindSpeed);

            // The song has to stay running so RewindableAudioSource can play it backward.
            if (musicSource != null && musicSource.GetComponent<RewindableAudioSource>() == null)
                musicSource.gameObject.AddComponent<RewindableAudioSource>();
        }

        private void Update()
        {
            // Hold R to rewind (the HUD's rewind button does the same with mouse or touch). Holding counts, not just
            // the press, so R held before rewind is allowed (first seconds, empty juice) starts it as soon as it is.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return;
            // One rewind per hold: if it stops by itself (out of juice, back at the start), wait for a new press.
            bool held = keyboard.rKey.isPressed;
            if (held && !heldLastFrame)
                rewoundThisHold = false;
            if (held && !rewoundThisHold && GameClock.Instance != null && !GameClock.Instance.IsRewinding)
            {
                StartRewind();
                rewoundThisHold = GameClock.Instance.IsRewinding;
            }
            if (!held && heldLastFrame)
                StopRewind();
            heldLastFrame = held;
        }

        private bool heldLastFrame;
        private bool rewoundThisHold;

        public void StopRewind()
        {
            isRewinding = false;
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                GameClock.Instance.StopRewind();
        }
    }
}
