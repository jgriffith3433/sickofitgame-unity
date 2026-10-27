using System;
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
    ///
    /// Rewind restores every frame, so this loop must stay cheap with a thousand rewindables (every entity,
    /// shot and floor piece has a few, and fired shots stay registered for the whole 30 s window): no
    /// allocations, no list searches, and nothing restored on an object that is switched off. What it costs
    /// is measured by <see cref="RewindPerf"/>.
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

        /// <summary>Seconds between records right now: the scene's setting, or <see cref="LowEndInterval"/> on a slow device.</summary>
        public float EffectiveRecordInterval => lowEnd ? Mathf.Max(recordInterval, LowEndInterval) : recordInterval;
        public bool LowEnd => lowEnd;

        /// <summary>10 Hz: the restore lerps between snapshots, so half as many records look the same.</summary>
        public const float LowEndInterval = 0.1f;
        // Smoothed frame time (real seconds) that switches the slow-device interval on and off, with a gap so it can't flicker.
        private const float SlowFrame = 0.030f, FastFrame = 0.022f;

        /// <summary>Playtest only: null decides by device and frame time, true/false forces the interval.</summary>
        public static bool? ForceLowEnd;

        private bool lowEnd;
        private float smoothFrame;

        private readonly List<IRewindable> rewindables = new List<IRewindable>();
        // The GameObject of each rewindable (same index), so the restore loop never casts or asks Unity for it.
        private readonly List<GameObject> objects = new List<GameObject>();
        // Where each rewindable sits in the two lists above, so Unregister is a swap-remove instead of a search.
        private readonly Dictionary<IRewindable, int> registered = new Dictionary<IRewindable, int>();
        private readonly List<ObjectPool> trackedPools = new List<ObjectPool>();
        private float lastRecordTime;
        private bool subscribedToGameClock;

        // RewindPerf: the frame being measured (its kind, the game time it moved, the timeline's work in it).
        private bool measuring;
        private bool measuredRewinding;
        private float measuredStep;
        private double measuredWork;
        private long measuredWorkAlloc, frameStartHeap;
        private float lastTickTime;

        // ─── Singleton lifecycle ──────────────────────────────────────

        private void Awake()
        {
            _instance = this;
            lowEnd = ForceLowEnd ?? SlowDevice();
        }

        private static bool SlowDevice()
        {
            return Application.isMobilePlatform || SystemInfo.processorCount <= 2 || SystemInfo.systemMemorySize <= 2048;
        }

        // Forward frames only: a device that can't keep up records less often, whatever it is.
        private void WatchFrameTime()
        {
            if (ForceLowEnd.HasValue)
            {
                lowEnd = ForceLowEnd.Value;
                return;
            }
            float frame = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            smoothFrame = smoothFrame <= 0f ? frame : Mathf.Lerp(smoothFrame, frame, 0.05f);
            if (!lowEnd && smoothFrame > SlowFrame)
                lowEnd = true;
            else if (lowEnd && smoothFrame < FastFrame && !SlowDevice())
                lowEnd = false;
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
            if (r == null || registered.ContainsKey(r))
                return;
            registered.Add(r, rewindables.Count);
            rewindables.Add(r);
            objects.Add(r is Component c ? c.gameObject : null);
        }

        public void Unregister(IRewindable r)
        {
            if (r == null || !registered.TryGetValue(r, out int i))
                return;
            registered.Remove(r);
            int last = rewindables.Count - 1;
            if (i != last)
            {
                // The last one takes the freed place (order within a restore phase doesn't matter).
                var moved = rewindables[last];
                rewindables[i] = moved;
                objects[i] = objects[last];
                registered[moved] = i;
            }
            rewindables.RemoveAt(last);
            objects.RemoveAt(last);
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
            // This tick starts a new frame: the last one is over (Time.unscaledDeltaTime is its length).
            long heapNow = RewindPerf.Heap();
            if (measuring)
                RewindPerf.AddFrame(Time.unscaledDeltaTime, measuredRewinding, measuredStep, measuredWork,
                    measuredWorkAlloc, Math.Max(0L, heapNow - frameStartHeap));
            long start = RewindPerf.Now();
            frameStartHeap = heapNow;
            measuring = true;
            measuredWork = 0;
            measuredWorkAlloc = 0;
            measuredRewinding = GameClock.Instance.IsRewinding;
            measuredStep = Mathf.Abs(time - lastTickTime);
            lastTickTime = time;

            if (measuredRewinding)
            {
                RestoreAll(time);
            }
            else
            {
                // Objects rewound to before they existed come back when time reaches their birth.
                CatchUpAll(time);
                WatchFrameTime();

                if (time - lastRecordTime >= EffectiveRecordInterval)
                {
                    RecordAll(time);
                    lastRecordTime = time;
                }
            }
            measuredWork += RewindPerf.MillisecondsSince(start);
            measuredWorkAlloc += Math.Max(0L, RewindPerf.Heap() - heapNow);
        }

        private void OnRewindStarted()
        {
            long start = RewindPerf.Now();
            int active = 0, longest = 0;
            for (int i = 0; i < rewindables.Count; i++)
            {
                rewindables[i].OnRewindStart();
                if (objects[i] != null && objects[i].activeInHierarchy)
                    active++;
                if (rewindables[i] is IRewindHistory h && h.HistoryCount > longest)
                    longest = h.HistoryCount;
            }
            RewindPerf.RewindStarted(GameClock.Instance != null ? GameClock.Instance.Time : 0f, rewindables.Count, active, longest);
            measuredWork += RewindPerf.MillisecondsSince(start);
        }

        private void OnRewindStopped()
        {
            long start = RewindPerf.Now();
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
            measuredWork += RewindPerf.MillisecondsSince(start);
            RewindPerf.RewindStopped(time);
        }

        // ─── Record / Restore ─────────────────────────────────────────

        private void RecordAll(float time)
        {
            for (int i = 0; i < rewindables.Count; i++)
            {
                var r = rewindables[i];

                // Skip inactive objects UNLESS they're RewindableGameObject
                // (which needs to record active state even when inactive)
                if (!r.IsGameObjectRewindable && objects[i] != null && !objects[i].activeSelf)
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

            // Phase 2: Restore everything else (transform, health, animator, etc.) on objects that are on.
            // Phase 1 has already switched on everything this moment needs. A switched-off object (a spent shot
            // held for the rewind window, a dead enemy) shows nothing; it gets its state the frame phase 1
            // turns it back on, and a rewind that ends with it off trims its history as before. Recording skips
            // them the same way.
            for (int i = 0; i < rewindables.Count; i++)
            {
                if (rewindables[i].IsGameObjectRewindable)
                    continue;
                if (objects[i] != null && !objects[i].activeInHierarchy)
                    continue;
                rewindables[i].RestoreState(time);
            }
        }

    }
}
