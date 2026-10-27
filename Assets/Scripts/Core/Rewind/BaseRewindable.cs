using System;
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
    public abstract class BaseRewindable<TSnapshot> : MonoBehaviour, IRewindable, IRewindHistory
        where TSnapshot : struct
    {
        [SerializeField] private int bufferCapacity = 600; // 30s at 20Hz: the most it will hold, not what it allocates up front

        /// <summary>The most snapshots the history keeps (its array grows toward this as the object lives).</summary>
        protected int BufferCapacity => Mathf.Max(1, bufferCapacity);

        protected CircularBuffer<TSnapshot> history;

        // GetSnapshotTime as a delegate, made once: passing the method group to the buffer's searches
        // allocated a new delegate per rewindable per frame while rewinding.
        private Func<TSnapshot, float> snapshotTime;
        protected Func<TSnapshot, float> SnapshotTime => snapshotTime ?? (snapshotTime = GetSnapshotTime);

        // The snapshot RestoreState applied last (index from the oldest), for SkipsRepeatedRestore.
        private int appliedIndex = -1;

        public int HistoryCount => history != null ? history.Count : 0;

        // First moment this object existed. Survives a rewind that trims every snapshot.
        private bool hasBirth;
        private TSnapshot birthSnapshot;
        private bool birthPresented;

        protected bool HasBirth => hasBirth;
        protected float BornAt { get; private set; }
        protected TSnapshot BirthSnapshot => birthSnapshot;

        public bool HasRecordedBirth => hasBirth;
        public float BirthTime => BornAt;

        public virtual bool IsGameObjectRewindable => false;

        /// <summary>
        /// When true, forward playback turns the object back on once time reaches its birth.
        /// Pooled shots and pickups set this false so a discarded future does not replay.
        /// </summary>
        public virtual bool ReplaysBirth => true;

        protected virtual void Awake()
        {
            history = new CircularBuffer<TSnapshot>(BufferCapacity);
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

            // Capture birth immediately. Waiting for the next record tick makes
            // "before the first snapshot" look like the object never existed.
            if (!gameObject.activeInHierarchy) return;
            if (GameClock.Instance == null || GameClock.Instance.IsRewinding) return;
            RecordState(GameClock.Instance.Time);
        }

        // ─── IRewindable ──────────────────────────────────────────────

        public virtual void RecordState(float time)
        {
            Remember(CaptureSnapshot(time));
        }

        /// <summary>
        /// Store a snapshot. The first one is the object's birth and is kept even
        /// when a rewind trims every later sample.
        /// </summary>
        protected void Remember(TSnapshot snapshot)
        {
            float time = GetSnapshotTime(snapshot);
            if (hasBirth && time < BornAt - 0.0001f)
                return;

            if (!hasBirth)
            {
                hasBirth = true;
                BornAt = time;
                birthSnapshot = snapshot;
            }

            history.Add(snapshot);
            appliedIndex = -1;
        }

        public virtual void CatchUp(float time)
        {
            if (!hasBirth)
                return;

            if (time < BornAt)
            {
                birthPresented = false;
                return;
            }

            if (birthPresented)
                return;

            birthPresented = true;

            // History was trimmed away because we branched before this object existed.
            // Put it back in the birth state, then let recording take over.
            if (history.Count == 0)
            {
                appliedIndex = -1;
                ApplySnapshot(birthSnapshot);
            }
        }

        public void RestoreState(float time)
        {
            if (history.Count == 0) return;

            // If target time is before our earliest recording, apply pre-existence default
            float earliestTime = GetSnapshotTime(history.Oldest);
            if (time < earliestTime)
            {
                // It existed then, but the ring buffer has dropped its oldest snapshots (a minute of play): hold the
                // oldest one kept. Treated as "not born yet", the hero's graphics were switched off and stayed off while
                // his body still took hits.
                if (hasBirth && time >= BornAt - 0.0001f)
                {
                    ApplyAt(0);
                    return;
                }
                appliedIndex = -1;
                ApplyPreExistenceDefault();
                return;
            }

            // Try interpolation
            if (history.FindBracket(time, SnapshotTime, out int lo, out int hi, out float t))
            {
                if (lo == hi || t < 0.001f)
                    ApplyAt(lo);
                else
                {
                    appliedIndex = -1;
                    ApplySnapshot(LerpSnapshots(history[lo], history[hi], t));
                }
            }
            else
            {
                // Fallback: snap to nearest
                int nearest = history.FindNearestIndex(time, SnapshotTime);
                if (nearest >= 0)
                    ApplyAt(nearest);
            }
        }

        private void ApplyAt(int index)
        {
            var snapshot = history[index];
            if (SkipsRepeatedRestore && index == appliedIndex && StillApplied(snapshot))
                return;
            appliedIndex = index;
            ApplySnapshot(snapshot);
        }

        public void BeginNewLife()
        {
            hasBirth = false;
            birthPresented = false;
            appliedIndex = -1;
            history?.Clear();
            OnLifeReset();
        }

        public void CaptureBirth()
        {
            if (!isActiveAndEnabled) return;
            if (GameClock.Instance == null || GameClock.Instance.IsRewinding) return;
            RecordState(GameClock.Instance.Time);
        }

        protected virtual void OnLifeReset() { }

        public virtual void OnRewindStart()
        {
            birthPresented = false;
            appliedIndex = -1;
        }
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
            appliedIndex = -1;
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
        /// Restoring the same recorded snapshot as last frame (a rewind sits between two of them for a while)
        /// does nothing when this is true and <see cref="StillApplied"/> agrees. Only for state nothing else
        /// changes during a rewind; the snapshot is applied again after any record, trim or new rewind.
        /// </summary>
        protected virtual bool SkipsRepeatedRestore => false;

        /// <summary>Cheap check that the last applied snapshot still shows (see <see cref="SkipsRepeatedRestore"/>).</summary>
        protected virtual bool StillApplied(TSnapshot snapshot) => true;

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
