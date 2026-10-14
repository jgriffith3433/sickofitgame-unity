using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// How much rewinding the player gets. Rewinding spends "juice" (seconds of game time rewound);
    /// pickups dropped by enemies refill it. Shown on the level's progress bar.
    /// </summary>
    [Serializable]
    public class RewindBudgetConfig
    {
        [Tooltip("Off = unlimited rewind, as before.")]
        public bool enabled = true;
        [Tooltip("Juice at the start of each level, in seconds of rewind.")]
        public float startSeconds = 3f;
        [Tooltip("The most juice the player can hold (upgrades can raise it).")]
        public float maxSeconds = 5f;
        [Tooltip("Juice spent per second of game time rewound.")]
        public float costPerSecond = 1f;
    }
}
