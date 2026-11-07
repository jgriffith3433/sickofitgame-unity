using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Where the HUD's touch move stick takes fingers, in screen pixels (null when it isn't shown). A finger that goes
    /// down inside it steers the hero, so <see cref="GlobalTapJump"/> must not also jump for it. Also the stick's maths,
    /// kept free of Unity objects so a test can run it.
    /// </summary>
    public static class TouchStickZone
    {
        /// <summary>The zone on screen right now, or null while the stick is hidden.</summary>
        public static Rect? Zone;

        /// <summary>Fraction of the radius inside which the stick reads as nothing.</summary>
        public const float DeadFraction = 0.18f;
        /// <summary>Fraction of the radius past which a push up is a jump flick.</summary>
        public const float JumpFraction = 0.8f;

        public static bool Contains(Vector2 screenPosition) => Zone.HasValue && Zone.Value.Contains(screenPosition);

        /// <summary>The move axis (-1..1) for a finger <paramref name="dx"/> pixels right of where it went down.</summary>
        public static float Axis(float dx, float radius)
        {
            if (radius <= 0f)
                return 0f;
            float t = Mathf.Clamp(dx / radius, -1f, 1f);
            float a = Mathf.Abs(t);
            if (a < DeadFraction)
                return 0f;
            return Mathf.Sign(t) * Mathf.Clamp01((a - DeadFraction) / (1f - DeadFraction));
        }

        /// <summary>True when the finger is pushed far enough up to count as a jump.</summary>
        public static bool IsJumpFlick(float dy, float radius) => radius > 0f && dy / radius >= JumpFraction;
    }
}
