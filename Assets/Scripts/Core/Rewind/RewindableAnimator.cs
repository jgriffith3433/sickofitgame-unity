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

        // Pre-allocated parameter arrays (sized in Awake)
        private int paramCount;
        private int[] paramHashes;
        private AnimatorControllerParameterType[] paramTypes;

        public struct AnimatorSnapshot
        {
            public float time;
            public int stateHash;
            public float normalizedTime;
            public float[] floatParams;
            public int[] intParams;
            public bool[] boolParams;
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

            int floatCount = 0, intCount = 0, boolCount = 0;
            for (int i = 0; i < paramCount; i++)
            {
                paramHashes[i] = parameters[i].nameHash;
                paramTypes[i] = parameters[i].type;
                switch (paramTypes[i])
                {
                    case AnimatorControllerParameterType.Float: floatCount++; break;
                    case AnimatorControllerParameterType.Int: intCount++; break;
                    case AnimatorControllerParameterType.Bool: boolCount++; break;
                }
            }

            // Pre-allocate reusable arrays
            reusableFloats = floatCount > 0 ? new float[floatCount] : null;
            reusableInts = intCount > 0 ? new int[intCount] : null;
            reusableBools = boolCount > 0 ? new bool[boolCount] : null;
        }

        // Pre-allocated snapshot arrays (sized once in CacheParameterInfo)
        private float[] reusableFloats;
        private int[] reusableInts;
        private bool[] reusableBools;

        protected override AnimatorSnapshot CaptureSnapshot(float time)
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return new AnimatorSnapshot { time = time };

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            var snapshot = new AnimatorSnapshot
            {
                time = time,
                stateHash = stateInfo.shortNameHash,
                normalizedTime = stateInfo.normalizedTime,
                floatParams = reusableFloats != null ? (float[])reusableFloats.Clone() : null,
                intParams = reusableInts != null ? (int[])reusableInts.Clone() : null,
                boolParams = reusableBools != null ? (bool[])reusableBools.Clone() : null,
            };

            int fi = 0, ii = 0, bi = 0;
            for (int i = 0; i < paramCount; i++)
            {
                switch (paramTypes[i])
                {
                    case AnimatorControllerParameterType.Float:
                        snapshot.floatParams[fi++] = animator.GetFloat(paramHashes[i]);
                        break;
                    case AnimatorControllerParameterType.Int:
                        snapshot.intParams[ii++] = animator.GetInteger(paramHashes[i]);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        snapshot.boolParams[bi++] = animator.GetBool(paramHashes[i]);
                        break;
                }
            }

            return snapshot;
        }

        protected override void ApplySnapshot(AnimatorSnapshot snapshot)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            if (snapshot.stateHash == 0) return; // Empty snapshot

            animator.Play(snapshot.stateHash, 0, snapshot.normalizedTime);

            int fi = 0, ii = 0, bi = 0;
            for (int i = 0; i < paramCount; i++)
            {
                switch (paramTypes[i])
                {
                    case AnimatorControllerParameterType.Float:
                        if (snapshot.floatParams != null && fi < snapshot.floatParams.Length)
                            animator.SetFloat(paramHashes[i], snapshot.floatParams[fi++]);
                        break;
                    case AnimatorControllerParameterType.Int:
                        if (snapshot.intParams != null && ii < snapshot.intParams.Length)
                            animator.SetInteger(paramHashes[i], snapshot.intParams[ii++]);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        if (snapshot.boolParams != null && bi < snapshot.boolParams.Length)
                            animator.SetBool(paramHashes[i], snapshot.boolParams[bi++]);
                        break;
                }
            }
        }

        protected override float GetSnapshotTime(AnimatorSnapshot snapshot) => snapshot.time;
    }
}
