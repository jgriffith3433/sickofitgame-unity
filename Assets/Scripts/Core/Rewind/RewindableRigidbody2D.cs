using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Records and restores Rigidbody2D velocity state.
    /// Position is handled by RewindableTransform — this only manages physics velocity.
    /// 
    /// On rewind start: saves body type, sets Kinematic, zeroes velocity.
    /// On rewind stop: restores body type, applies last recorded velocity.
    /// </summary>
    [RequireComponent(typeof(RewindableTransform))]
    public class RewindableRigidbody2D : BaseRewindable<RewindableRigidbody2D.RBSnapshot>
    {
        [SerializeField] private Rigidbody2D rb;

        private RigidbodyType2D savedBodyType;

        public struct RBSnapshot
        {
            public float time;
            public Vector2 velocity;
            public float angularVelocity;
        }

        protected override void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            base.Awake();
        }

        protected override RBSnapshot CaptureSnapshot(float time)
        {
            return new RBSnapshot
            {
                time = time,
                velocity = rb != null ? rb.linearVelocity : Vector2.zero,
                angularVelocity = rb != null ? rb.angularVelocity : 0f
            };
        }

        protected override void ApplySnapshot(RBSnapshot snapshot)
        {
            // During rewind, position is handled by RewindableTransform and velocity stays zero.
            // Forward catch-up (respawn after rewinding past birth) applies the birth velocity.
            if (rb == null) return;
            if (GameClock.Instance != null && GameClock.Instance.IsRewinding) return;

            rb.linearVelocity = snapshot.velocity;
            rb.angularVelocity = snapshot.angularVelocity;
        }

        protected override float GetSnapshotTime(RBSnapshot snapshot) => snapshot.time;

        public override void OnRewindStart()
        {
            base.OnRewindStart();
            if (rb == null) return;
            savedBodyType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        public override void OnRewindStop()
        {
            base.OnRewindStop();

            if (rb == null) return;
            rb.bodyType = savedBodyType;

            // Restore the velocity from the most recent snapshot after trim
            if (history.Count > 0)
            {
                var newest = history.Newest;
                rb.linearVelocity = newest.velocity;
                rb.angularVelocity = newest.angularVelocity;
            }
        }
    }
}
