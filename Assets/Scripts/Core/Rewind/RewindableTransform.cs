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
                localScale = Vector3.Lerp(a.localScale, b.localScale, t)
            };
        }
    }
}
