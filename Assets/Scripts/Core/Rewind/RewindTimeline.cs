using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Per-scene rewind coordinator. Replaces StateRecorder.
    /// 
    /// Rewindable components register/unregister themselves via BaseRewindable.Awake/OnDestroy.
    /// Object pools register themselves so shots from a discarded future can be reclaimed.
    /// 
    /// Subscribes to GameClock events to drive recording and restoring.
    /// </summary>
    public class RewindTimeline : MonoBehaviour
    {
        // Singleton — per-scene, not DontDestroyOnLoad
        private static RewindTimeline _instance;
        public static RewindTimeline Instance => _instance;

        [Header("Settings")]
        [SerializeField] private float recordInterval = 0.05f;
        [SerializeField] private float maxHistorySeconds = 30f;

        public float MaxHistorySeconds => maxHistorySeconds;

        private readonly List<IRewindable> rewindables = new List<IRewindable>();
        private readonly List<ObjectPool> trackedPools = new List<ObjectPool>();
        private float lastRecordTime;
        private bool subscribedToGameClock;

        // ─── Singleton lifecycle ──────────────────────────────────────

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            UnsubscribeFromGameClock();
        }

        // ─── Registration ─────────────────────────────────────────────

        public void Register(IRewindable r)
        {
            if (!rewindables.Contains(r))
                rewindables.Add(r);
        }

        public void Unregister(IRewindable r)
        {
            rewindables.Remove(r);
        }

        public void RegisterPool(ObjectPool pool)
        {
            if (pool != null && !trackedPools.Contains(pool))
                trackedPools.Add(pool);
        }

        public void UnregisterPool(ObjectPool pool)
        {
            trackedPools.Remove(pool);
        }

        // ─── GameClock subscription ───────────────────────────────────

        private void Update()
        {
            TrySubscribeToGameClock();
        }

        private void TrySubscribeToGameClock()
        {
            if (subscribedToGameClock || GameClock.Instance == null) return;
            GameClock.Instance.OnTimeChanged += OnClockTick;
            GameClock.Instance.OnRewindStarted += OnRewindStarted;
            GameClock.Instance.OnRewindStopped += OnRewindStopped;
            subscribedToGameClock = true;
        }

        private void UnsubscribeFromGameClock()
        {
            if (!subscribedToGameClock || GameClock.Instance == null) return;
            GameClock.Instance.OnTimeChanged -= OnClockTick;
            GameClock.Instance.OnRewindStarted -= OnRewindStarted;
            GameClock.Instance.OnRewindStopped -= OnRewindStopped;
            subscribedToGameClock = false;
        }

        // ─── Clock event handlers ─────────────────────────────────────

        private void OnClockTick(float time)
        {
            if (GameClock.Instance.IsRewinding)
            {
                RestoreAll(time);
            }
            else
            {
                // Objects rewound to before they existed come back when time reaches their birth.
                CatchUpAll(time);

                if (time - lastRecordTime >= recordInterval)
                {
                    RecordAll(time);
                    lastRecordTime = time;
                }
            }
        }

        private void OnRewindStarted()
        {
            for (int i = 0; i < rewindables.Count; i++)
                rewindables[i].OnRewindStart();
        }

        private void OnRewindStopped()
        {
            for (int i = 0; i < rewindables.Count; i++)
                rewindables[i].OnRewindStop();

            float time = GameClock.Instance != null ? GameClock.Instance.Time : 0f;

            // lastRecordTime is still up in the discarded future. Until the clock
            // caught back up to it, nothing new was saved, so the next rewind
            // had no history for the time you just replayed.
            RecordAll(time);
            lastRecordTime = time;

            for (int i = 0; i < trackedPools.Count; i++)
            {
                if (trackedPools[i] != null)
                    trackedPools[i].ReclaimDiscarded(time);
            }
        }

        // ─── Record / Restore ─────────────────────────────────────────

        private void RecordAll(float time)
        {
            for (int i = 0; i < rewindables.Count; i++)
            {
                var r = rewindables[i];

                // Skip inactive objects UNLESS they're RewindableGameObject
                // (which needs to record active state even when inactive)
                if (r is MonoBehaviour mb && !mb.gameObject.activeSelf && !r.IsGameObjectRewindable)
                    continue;

                r.RecordState(time);
            }
        }

        private void CatchUpAll(float time)
        {
            for (int i = 0; i < rewindables.Count; i++)
            {
                if (rewindables[i].IsGameObjectRewindable)
                    rewindables[i].CatchUp(time);
            }

            for (int i = 0; i < rewindables.Count; i++)
            {
                if (!rewindables[i].IsGameObjectRewindable)
                    rewindables[i].CatchUp(time);
            }
        }

        private void RestoreAll(float time)
        {
            // Phase 1: Restore RewindableGameObject first (reactivates dead enemies)
            for (int i = 0; i < rewindables.Count; i++)
            {
                if (rewindables[i].IsGameObjectRewindable)
                    rewindables[i].RestoreState(time);
            }

            // Phase 2: Restore everything else (transform, health, animator, etc.)
            for (int i = 0; i < rewindables.Count; i++)
            {
                if (!rewindables[i].IsGameObjectRewindable)
                    rewindables[i].RestoreState(time);
            }
        }

    }
}
