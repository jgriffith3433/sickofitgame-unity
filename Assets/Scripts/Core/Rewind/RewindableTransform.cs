using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Records and restores transform state (position, rotation, scale).
    /// Supports smooth interpolation between keyframes.
    /// </summary>
    public class RewindableTransform : BaseRewindable<RewindableTransform.TransformSnapshot>
    {
        public struct TransformSnapshot
        {
            public float time;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        protected override TransformSnapshot CaptureSnapshot(float time)
        {
            return new TransformSnapshot
            {
                time = time,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale
            };
        }

        protected override void ApplySnapshot(TransformSnapshot snapshot)
        {
            transform.localPosition = snapshot.localPosition;
            transform.localRotation = snapshot.localRotation;
            transform.localScale = snapshot.localScale;
        }

        protected override float GetSnapshotTime(TransformSnapshot snapshot) => snapshot.time;

        protected override TransformSnapshot LerpSnapshots(TransformSnapshot a, TransformSnapshot b, float t)
        {
            return new TransformSnapshot
            {
                time = Mathf.Lerp(a.time, b.time, t),
                localPosition = Vector3.Lerp(a.localPosition, b.localPosition, t),
                localRotation = Quaternion.Slerp(a.localRotation, b.localRotation, t),
                localScale = LerpScale(a.localScale, b.localScale, t)
            };
        }

        /// <summary>
        /// Scale between two snapshots, except an axis that changed sign (a sprite flipped to face the other way): it snaps
        /// to the nearer snapshot. Lerping 1 to -1 passed through 0: rewinding over a turn drew the hero paper-thin or not at
        /// all, and a rewind that stopped there left him that way (his turns keep |scale|).
        /// </summary>
        static Vector3 LerpScale(Vector3 a, Vector3 b, float t)
        {
            var near = t < 0.5f ? a : b;
            return new Vector3(
                a.x * b.x < 0f ? near.x : Mathf.Lerp(a.x, b.x, t),
                a.y * b.y < 0f ? near.y : Mathf.Lerp(a.y, b.y, t),
                a.z * b.z < 0f ? near.z : Mathf.Lerp(a.z, b.z, t));
        }
    }
}
