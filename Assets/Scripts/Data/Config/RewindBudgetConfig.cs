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
        public float startSeconds = 10f;
        [Tooltip("The most juice the player can hold (upgrades can raise it).")]
        public float maxSeconds = 10f;
        [Tooltip("Juice spent per second of game time rewound.")]
        public float costPerSecond = 1f;
        [Tooltip("Juice paid back for an enemy killed cleanly (the hero not hurt for cleanKillSeconds). 0 = off.")]
        public float cleanKillRefund = 0.5f;
        [Tooltip("A kill is clean when the hero has not been hurt for this many seconds of game time.")]
        public float cleanKillSeconds = 3f;
        [Tooltip("After undoing a death with a rewind, living this many seconds pays survivalBonus. 0 = off.")]
        public float survivalSeconds = 6f;
        [Tooltip("Juice paid for surviving after a death-rewind.")]
        public float survivalBonus = 3f;
    }
}
