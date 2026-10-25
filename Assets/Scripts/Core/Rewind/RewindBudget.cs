using System;
using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// The player's rewind "juice": seconds of game time they can still rewind in this level. Rewinding spends it,
    /// pickups add to it. It is deliberately NOT part of the rewound state, otherwise rewinding would refund itself.
    /// One per level scene; without one, rewind is unlimited (as before).
    /// </summary>
    public sealed class RewindBudget : MonoBehaviour
    {
        public static RewindBudget Instance { get; private set; }

        float juice;
        float max = 5f;
        float costPerSecond = 1f;
        float lastClockTime;
        bool draining;

        public float Juice => juice;
        public float Max => max;
        public float Fraction => max > 0f ? juice / max : 0f;
        /// <summary>Rewinding is allowed (some juice left).</summary>
        public bool CanRewind => juice > 0.01f;

        /// <summary>Fires when juice is added (amount added).</summary>
        public event Action<float> Gained;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        float survivalSeconds;
        float survivalBonus;
        bool survivalArmed;
        float survivalSince;

        /// <summary>Fires when a reward pays juice (amount actually added, label for the HUD). Not rewound, like the juice.</summary>
        public event Action<float, string> Rewarded;

        public void ConfigureRewards(float survivalAfterSeconds, float bonus)
        {
            survivalSeconds = Mathf.Max(0f, survivalAfterSeconds);
            survivalBonus = Mathf.Max(0f, bonus);
        }

        /// <summary>Pays juice as a reward and tells the HUD. Returns the amount added (0 when full).</summary>
        public float Reward(float seconds, string label)
        {
            if (seconds <= 0f)
                return 0f;
            float before = juice;
            juice = Mathf.Min(max, juice + seconds);
            float added = juice - before;
            if (added > 0.001f)
                Rewarded?.Invoke(added, label);
            return added;
        }

        /// <summary>A death was undone by a rewind: living on for a while pays the survival bonus.</summary>
        public void ArmSurvival()
        {
            if (survivalSeconds <= 0f || survivalBonus <= 0f)
                return;
            survivalArmed = true;
            survivalSince = -1f;
        }

        public void DisarmSurvival() => survivalArmed = false;

        public void Configure(float start, float maxSeconds, float cost)
        {
            max = Mathf.Max(0.1f, maxSeconds);
            juice = Mathf.Clamp(start, 0f, max);
            costPerSecond = Mathf.Max(0f, cost);
        }

        public void RaiseMax(float seconds, bool fill)
        {
            max += seconds;
            if (fill)
                juice = Mathf.Min(max, juice + seconds);
        }

        public void Add(float seconds)
        {
            if (seconds <= 0f)
                return;
            float before = juice;
            juice = Mathf.Min(max, juice + seconds);
            Gained?.Invoke(juice - before);
        }

        void LateUpdate()
        {
            var clock = GameClock.Instance;
            if (clock == null)
                return;

            if (!clock.IsRewinding)
            {
                draining = false;
                if (survivalArmed && !clock.IsPaused)
                {
                    if (survivalSince < 0f)
                        survivalSince = clock.Time;
                    else if (clock.Time - survivalSince >= survivalSeconds)
                    {
                        survivalArmed = false;
                        Reward(survivalBonus, "SURVIVED");
                    }
                }
                return;
            }
            survivalArmed = false;

            if (!draining)
            {
                draining = true;
                lastClockTime = clock.Time;
                return;
            }

            float rewound = Mathf.Max(0f, lastClockTime - clock.Time);
            lastClockTime = clock.Time;
            juice = Mathf.Max(0f, juice - rewound * costPerSecond);
            if (juice <= 0f)
                clock.StopRewind();
        }
    }
}
