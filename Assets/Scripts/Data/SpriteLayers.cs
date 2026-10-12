using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Draw bands, back to front. Pick one on a SpriteRenderer when placing level art.
    /// Generated gameplay (player, enemies, platforms, pickups, projectiles) stays on
    /// <see cref="Player"/>, so a Foreground1 sprite covers the whole gameplay plane.
    /// </summary>
    public enum SpriteLayer
    {
        Background1,
        Background2,
        Player,
        Foreground1,
        Foreground2
    }

    /// <summary>
    /// Applies <see cref="SpriteLayer"/>, the order used inside the Player band,
    /// and a Z so the Scene view shows each band as its own plane.
    /// The game camera is orthographic, so this Z does not change what the player sees.
    /// Sorting layer still decides draw order.
    /// </summary>
    public static class SpriteLayers
    {
        /// <summary>Distance between neighboring bands. Player sits at 0.</summary>
        public const float Spacing = 2f;

        public const int Platform = 0;
        public const int Shadow = 1;
        public const int Character = 2;
        public const int Pickup = 3;
        public const int Projectile = 4;
        public const int HealthBar = 10;

        /// <summary>Full-screen effects on Foreground2, in front of other foreground art.</summary>
        public const int Overlay = 1000;

        /// <summary>
        /// World/local Z for a band. Positive is farther from the camera (camera sits at z = -10).
        /// Background1 is +4, Player is 0, Foreground2 is -4.
        /// </summary>
        public static float Depth(SpriteLayer layer)
        {
            return ((int)SpriteLayer.Player - (int)layer) * Spacing;
        }

        public static void Apply(SpriteRenderer renderer, SpriteLayer layer, int order = 0)
        {
            if (renderer == null)
                return;

            renderer.sortingLayerName = layer.ToString();
            renderer.sortingOrder = order;

            var transform = renderer.transform;
            var position = transform.localPosition;
            position.z = Depth(layer);
            transform.localPosition = position;
        }
    }
}
