using UnityEngine;
using SickOfIt.Data.Config;

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
        static PresentationConfig Presentation =>
            GameDefinition.Instance != null ? GameDefinition.Instance.presentation : null;

        /// <summary>Distance between neighboring bands. Player sits at 0.</summary>
        public static float Spacing => Presentation != null ? Presentation.layerSpacing : 2f;

        public static int Platform => Presentation != null ? Presentation.platformOrder : 0;
        public static int Shadow => Presentation != null ? Presentation.shadowOrder : 1;
        public static int Character => Presentation != null ? Presentation.characterOrder : 2;
        public static int Pickup => Presentation != null ? Presentation.pickupOrder : 3;
        public static int Projectile => Presentation != null ? Presentation.projectileOrder : 4;
        public static int HealthBar => Presentation != null ? Presentation.healthBarOrder : 10;

        /// <summary>Full-screen effects on Foreground2, in front of other foreground art.</summary>
        public static int Overlay => Presentation != null ? Presentation.overlayOrder : 1000;

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
