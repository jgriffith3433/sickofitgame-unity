using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Records and restores Animator state (current state hash, normalized time, parameters).
    /// Snaps to state rather than interpolating — animation state is discrete.
    /// </summary>
    public class RewindableAnimator : BaseRewindable<RewindableAnimator.AnimatorSnapshot>
    {
        [SerializeField] private Animator animator;

        // Parameter names, hashes and types (read once in Awake)
        private int paramCount;
        private int[] paramHashes;
        private AnimatorControllerParameterType[] paramTypes;

        // Every parameter of every snapshot lives in one flat pool (paramCount floats a slot; ints and bools as
        // floats, exact for the values an Animator holds), so a record allocates nothing. Slots are handed out in a
        // ring the size of the history; slot 0 is the birth snapshot's, kept for good, the ring is slots 1..capacity.
        private float[] pool = System.Array.Empty<float>();
        private int nextSlot;

        public struct AnimatorSnapshot
        {
            public float time;
            public int stateHash;
            public float normalizedTime;
            public int slot; // where its parameters start in the pool, in slots
        }

        protected override void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            CacheParameterInfo();
            base.Awake();
        }

        private void CacheParameterInfo()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                paramCount = 0;
                return;
            }

            var parameters = animator.parameters;
            paramCount = parameters.Length;
            paramHashes = new int[paramCount];
            paramTypes = new AnimatorControllerParameterType[paramCount];
            for (int i = 0; i < paramCount; i++)
            {
                paramHashes[i] = parameters[i].nameHash;
                paramTypes[i] = parameters[i].type;
            }
        }

        // The slot for the next record: the reserved one for a life's first (its birth), the ring after that.
        private int TakeSlot()
        {
            if (!HasBirth)
                return 0;
            int slot = 1 + nextSlot;
            nextSlot = (nextSlot + 1) % BufferCapacity;
            return slot;
        }

        private void EnsurePool(int slot)
        {
            int needed = (slot + 1) * paramCount;
            if (pool.Length >= needed)
                return;
            System.Array.Resize(ref pool, Mathf.Max(needed, Mathf.Min(pool.Length * 2, (BufferCapacity + 1) * paramCount)));
        }

        protected override AnimatorSnapshot CaptureSnapshot(float time)
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return new AnimatorSnapshot { time = time };

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            int slot = paramCount > 0 ? TakeSlot() : 0;
            if (paramCount > 0)
            {
                EnsurePool(slot);
                int at = slot * paramCount;
                for (int i = 0; i < paramCount; i++)
                    pool[at + i] = ReadParam(i);
            }

            return new AnimatorSnapshot
            {
                time = time,
                stateHash = stateInfo.shortNameHash,
                normalizedTime = stateInfo.normalizedTime,
                slot = slot,
            };
        }

        private float ReadParam(int i)
        {
            switch (paramTypes[i])
            {
                case AnimatorControllerParameterType.Float: return animator.GetFloat(paramHashes[i]);
                case AnimatorControllerParameterType.Int: return animator.GetInteger(paramHashes[i]);
                case AnimatorControllerParameterType.Bool: return animator.GetBool(paramHashes[i]) ? 1f : 0f;
                default: return 0f; // triggers aren't recorded
            }
        }

        // A rewind between two recorded snapshots asks for the same one every frame: nothing to do while the
        // Animator still shows it (same state, same point in it, same parameters).
        protected override bool SkipsRepeatedRestore => true;

        protected override bool StillApplied(AnimatorSnapshot snapshot)
        {
            if (animator == null || !animator.isActiveAndEnabled || snapshot.stateHash == 0)
                return true;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != snapshot.stateHash || Mathf.Abs(info.normalizedTime - snapshot.normalizedTime) > 0.001f)
                return false;
            return ParamsMatch(snapshot);
        }

        private bool ParamsMatch(AnimatorSnapshot snapshot)
        {
            if (paramCount == 0 || (snapshot.slot + 1) * paramCount > pool.Length)
                return true;
            int at = snapshot.slot * paramCount;
            for (int i = 0; i < paramCount; i++)
            {
                if (paramTypes[i] == AnimatorControllerParameterType.Trigger)
                    continue;
                if (!Mathf.Approximately(ReadParam(i), pool[at + i]))
                    return false;
            }
            return true;
        }

        protected override void ApplySnapshot(AnimatorSnapshot snapshot)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            if (snapshot.stateHash == 0) return; // Empty snapshot

            animator.Play(snapshot.stateHash, 0, snapshot.normalizedTime);

            if (paramCount == 0 || (snapshot.slot + 1) * paramCount > pool.Length) return;
            int at = snapshot.slot * paramCount;
            for (int i = 0; i < paramCount; i++)
            {
                float value = pool[at + i];
                switch (paramTypes[i])
                {
                    case AnimatorControllerParameterType.Float:
                        if (!Mathf.Approximately(animator.GetFloat(paramHashes[i]), value))
                            animator.SetFloat(paramHashes[i], value);
                        break;
                    case AnimatorControllerParameterType.Int:
                        if (animator.GetInteger(paramHashes[i]) != (int)value)
                            animator.SetInteger(paramHashes[i], (int)value);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        if (animator.GetBool(paramHashes[i]) != (value > 0.5f))
                            animator.SetBool(paramHashes[i], value > 0.5f);
                        break;
                }
            }
        }

        // The trim at the end of a rewind dropped the newest snapshots: their slots are free again, so the ring
        // carries on after the newest one kept (otherwise it could wrap onto a kept, older one).
        public override void OnRewindStop()
        {
            base.OnRewindStop();
            if (history.Count == 0)
                nextSlot = 0;
            else
                nextSlot = history.Newest.slot % BufferCapacity;
        }

        protected override void OnLifeReset() => nextSlot = 0;

        protected override float GetSnapshotTime(AnimatorSnapshot snapshot) => snapshot.time;
    }
}
