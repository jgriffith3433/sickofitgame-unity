using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Draw order and the Scene-view gap between sorting bands.
    /// Gameplay objects stay on the Player band. These numbers only separate
    /// things that share that band, and how far apart the bands sit on Z.
    /// </summary>
    [Serializable]
    public class PresentationConfig
    {
        [Tooltip("World units between sorting bands in the Scene view. The game camera is orthographic, so this does not move anything on screen.")]
        public float layerSpacing = 2f;

        public int platformOrder;
        public int shadowOrder = 1;
        public int characterOrder = 2;
        public int pickupOrder = 3;
        public int projectileOrder = 4;
        public int healthBarOrder = 10;
        public int overlayOrder = 1000;

        public Color platformColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    }
}
