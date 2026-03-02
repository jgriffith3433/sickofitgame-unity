using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Abstract base for all rewindable components. Provides:
    ///   - Self-registration with RewindTimeline (Awake/OnDestroy)
    ///   - CircularBuffer history storage
    ///   - Binary-search restore with optional interpolation
    ///
    /// Registration uses Awake/OnDestroy (NOT OnEnable/OnDisable) so that
    /// dead enemies (SetActive(false)) stay registered and can be restored.
    /// </summary>
    public abstract class BaseRewindable<TSnapshot> : MonoBehaviour, IRewindable
        where TSnapshot : struct
    {
        [SerializeField] private int bufferCapacity = 600; // 30s at 20Hz

        protected CircularBuffer<TSnapshot> history;

        public virtual bool IsGameObjectRewindable => false;

        protected virtual void Awake()
        {
            history = new CircularBuffer<TSnapshot>(bufferCapacity);
            if (RewindTimeline.Instance != null)
                RewindTimeline.Instance.Register(this);
        }

        protected virtual void OnDestroy()
        {
            if (RewindTimeline.Instance != null)
                RewindTimeline.Instance.Unregister(this);
        }

        protected virtual void Start()
        {
            // Retry registration if RewindTimeline wasn't ready during Awake
            if (RewindTimeline.Instance != null)
                RewindTimeline.Instance.Register(this);
        }

        // ─── IRewindable ──────────────────────────────────────────────

        public virtual void RecordState(float time)
        {
            var snapshot = CaptureSnapshot(time);
            history.Add(snapshot);
        }

        public void RestoreState(float time)
        {
            if (history.Count == 0) return;

            // If target time is before our earliest recording, apply pre-existence default
            float earliestTime = GetSnapshotTime(history.Oldest);
            if (time < earliestTime)
            {
                ApplyPreExistenceDefault();
                return;
            }

            // Try interpolation
            if (history.FindBracket(time, GetSnapshotTime, out int lo, out int hi, out float t))
            {
                if (lo == hi || t < 0.001f)
                    ApplySnapshot(history[lo]);
                else
                    ApplySnapshot(LerpSnapshots(history[lo], history[hi], t));
            }
            else
            {
                // Fallback: snap to nearest
                int nearest = history.FindNearestIndex(time, GetSnapshotTime);
                if (nearest >= 0)
                    ApplySnapshot(history[nearest]);
            }
        }

        public virtual void OnRewindStart() { }
        public virtual void OnRewindStop()
        {
            // Trim future entries: find the entry at current GameClock time and discard later ones
            if (GameClock.Instance == null || history.Count == 0) return;
            float currentTime = GameClock.Instance.Time;
            int keepCount = 0;
            for (int i = 0; i < history.Count; i++)
            {
                if (GetSnapshotTime(history[i]) <= currentTime + 0.001f)
                    keepCount = i + 1;
                else
                    break;
            }
            history.TrimToCount(keepCount);
        }

        // ─── Subclass contract ────────────────────────────────────────

        /// <summary>Capture current state into a snapshot struct.</summary>
        protected abstract TSnapshot CaptureSnapshot(float time);

        /// <summary>Apply a snapshot to restore state.</summary>
        protected abstract void ApplySnapshot(TSnapshot snapshot);

        /// <summary>Extract the time field from a snapshot.</summary>
        protected abstract float GetSnapshotTime(TSnapshot snapshot);

        /// <summary>
        /// Interpolate between two snapshots. Default: return nearest (a).
        /// Override for smooth interpolation (e.g., transform lerp).
        /// </summary>
        protected virtual TSnapshot LerpSnapshots(TSnapshot a, TSnapshot b, float t)
        {
            return a;
        }

        /// <summary>
        /// Called when restoring to a time before this object's first recording.
        /// Default: do nothing. RewindableGameObject overrides to deactivate.
        /// </summary>
        protected virtual void ApplyPreExistenceDefault()
        {
            // Default: do nothing (object stays in whatever state it's in)
        }
    }
}
