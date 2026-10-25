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

        /// <summary>How many rewinds have ended since the game started. Not rewound itself: enemies that react to the hero's
        /// rewind (RewindSnagAbility) compare it with the value saved in their own state.</summary>
        public static int RewindsEnded { get; private set; }

        /// <summary>Real (unscaled) time the last rewind ended; -1000 before any. Not rewound; the `heroRewound` trigger reads it.</summary>
        public static float LastRewindEndedAt { get; private set; } = -1000f;

        [Header("State (Read-Only)")]
        [SerializeField] private float currentTime;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private bool isPaused;
        [SerializeField] private bool isRewinding;

        /// <summary>
        /// The longest real frame the clock follows while rewinding (s): a lag spike mid-rewind doesn't throw time
        /// (and the juice) a second back at once.
        /// </summary>
        public const float MaxRewindFrame = 0.1f;

        /// <summary>
        /// Right after a rewind stops, the next frames are clamped to this (s), and so is Unity's
        /// maximumDeltaTime, which caps how many fixed steps the simulation catches up in one frame. The stop
        /// itself is a heavy frame (every history trimmed and recorded); without the clamp the frame after it
        /// could run up to 1/3 s of shots, hits and lava unseen, and kill the hero through lag.
        /// </summary>
        public const float PostRewindMaxFrame = 1f / 30f;
        public const int PostRewindFrames = 3;

        private int postRewindFramesLeft;
        private float savedMaximumDeltaTime = -1f;

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
            EndPostRewindClamp();
        }

        private void Update()
        {
            if (isPaused) return;

            float frame = UnityEngine.Time.deltaTime;
            if (isRewinding)
            {
                frame = Mathf.Min(frame, MaxRewindFrame);
            }
            else if (postRewindFramesLeft > 0)
            {
                frame = Mathf.Min(frame, PostRewindMaxFrame);
                if (--postRewindFramesLeft == 0)
                    EndPostRewindClamp();
            }

            float delta = frame * timeScale * Direction;
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
            RewindsEnded++;
            LastRewindEndedAt = UnityEngine.Time.unscaledTime;
            BeginPostRewindClamp();
            OnRewindStopped?.Invoke();
        }

        private void BeginPostRewindClamp()
        {
            postRewindFramesLeft = PostRewindFrames;
            if (savedMaximumDeltaTime < 0f)
                savedMaximumDeltaTime = UnityEngine.Time.maximumDeltaTime;
            // Unity wants maximumDeltaTime >= fixedDeltaTime.
            UnityEngine.Time.maximumDeltaTime = Mathf.Max(PostRewindMaxFrame, UnityEngine.Time.fixedDeltaTime);
        }

        private void EndPostRewindClamp()
        {
            postRewindFramesLeft = 0;
            if (savedMaximumDeltaTime < 0f) return;
            UnityEngine.Time.maximumDeltaTime = savedMaximumDeltaTime;
            savedMaximumDeltaTime = -1f;
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
