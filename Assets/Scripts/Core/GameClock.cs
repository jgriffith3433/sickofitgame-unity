using UnityEngine;
using System;

namespace SickOfIt.Core
{
    /// <summary>
    /// The master game clock. Owns time for the entire game experience.
    /// Everything that cares about game time reads from this.
    /// 
    /// Supports:
    ///   - Normal forward playback
    ///   - Pause/resume
    ///   - Rewind (negative time flow)
    ///   - Slow motion / fast forward (time scale)
    ///   - Seeking to specific times
    /// 
    /// This is what makes rewind possible. Instead of Unity's Time.time
    /// (which only goes forward), GameClock.Time can go backwards.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        // Singleton (per-scene, not DontDestroyOnLoad)
        private static GameClock _instance;
        public static GameClock Instance => _instance;

        [Header("State (Read-Only)")]
        [SerializeField] private float currentTime;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private bool isPaused;
        [SerializeField] private bool isRewinding;

        // Events
        public event Action<float> OnTimeChanged;       // fires every frame with current time
        public event Action OnRewindStarted;
        public event Action OnRewindStopped;
        public event Action OnPaused;
        public event Action OnResumed;

        // Properties
        public float Time => currentTime;
        public float TimeScale => timeScale;
        public bool IsPaused => isPaused;
        public bool IsRewinding => isRewinding;
        public float Direction => isRewinding ? -1f : 1f;

        /// <summary>
        /// Frame delta scaled by GameClock's timeScale and direction.
        /// Use this instead of Time.deltaTime for all gameplay code.
        /// Returns 0 while paused or during rewind.
        /// </summary>
        public float DeltaTime
        {
            get
            {
                if (isPaused || isRewinding) return 0f;
                return UnityEngine.Time.deltaTime * timeScale;
            }
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (isPaused) return;

            float delta = UnityEngine.Time.deltaTime * timeScale * Direction;
            currentTime += delta;

            // Don't go below 0
            if (currentTime < 0f)
            {
                currentTime = 0f;
                if (isRewinding)
                    StopRewind();
            }

            OnTimeChanged?.Invoke(currentTime);
        }

        // =========================================================================
        // PUBLIC API
        // =========================================================================

        public void SetTime(float time)
        {
            currentTime = Mathf.Max(0f, time);
            OnTimeChanged?.Invoke(currentTime);
        }

        public void Pause()
        {
            isPaused = true;
            OnPaused?.Invoke();
        }

        public void Resume()
        {
            isPaused = false;
            OnResumed?.Invoke();
        }

        public void Play()
        {
            isPaused = false;
            isRewinding = false;
            timeScale = 1f;
            OnResumed?.Invoke();
        }

        public void SetTimeScale(float scale)
        {
            timeScale = scale;
        }

        public void StartRewind(float rewindSpeed = 2f)
        {
            if (isRewinding) return;
            isRewinding = true;
            timeScale = rewindSpeed;
            OnRewindStarted?.Invoke();
        }

        public void StopRewind()
        {
            if (!isRewinding) return;
            isRewinding = false;
            timeScale = 1f;
            OnRewindStopped?.Invoke();
        }

        /// <summary>
        /// Reset clock to zero.
        /// </summary>
        public void Reset()
        {
            currentTime = 0f;
            timeScale = 1f;
            isPaused = false;
            isRewinding = false;
            OnTimeChanged?.Invoke(0f);
        }
    }
}
