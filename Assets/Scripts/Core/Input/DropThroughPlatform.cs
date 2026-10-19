using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// A one-way platform: the hero jumps up through it from below and lands on top, and holding down while standing
    /// on it drops him through (<see cref="PlayerController"/> ignores the collision with that one piece until he is
    /// below it). On the generated Platform prefab; climb footholds get the same through <see cref="MakeOneWay"/>
    /// (EntityFactory, an entity body's "oneWay"). Ground and walls are never one-way.
    /// </summary>
    public class DropThroughPlatform : MonoBehaviour
    {
        [SerializeField] private Collider2D platformCollider;

        private void Awake()
        {
            if (platformCollider == null) platformCollider = GetComponent<Collider2D>();
            if (platformCollider != null)
                MakeOneWay(platformCollider);
        }

        /// <summary>Makes a collider one-way (solid from above only), or solid all round again with oneWay false.</summary>
        public static void MakeOneWay(Collider2D collider, bool oneWay = true)
        {
            if (collider == null)
                return;
            if (!collider.TryGetComponent<PlatformEffector2D>(out var effector))
            {
                if (!oneWay)
                    return;
                effector = collider.gameObject.AddComponent<PlatformEffector2D>();
            }
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 180f;
            effector.useColliderMask = false;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            effector.enabled = oneWay;
            collider.usedByEffector = oneWay;
        }

        /// <summary>True for a collider he can jump up through and drop down through.</summary>
        public static bool IsOneWay(Collider2D collider)
        {
            return collider != null && collider.usedByEffector
                && collider.TryGetComponent<PlatformEffector2D>(out var effector) && effector.enabled && effector.useOneWay;
        }
    }
}
