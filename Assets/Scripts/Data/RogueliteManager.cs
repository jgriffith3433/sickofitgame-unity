using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>
    /// Central manager for all roguelite progression data.
    /// Handles cumulative health tracking, upgrade purchases, and currency.
    /// Persists via PlayerPrefs alongside GameProgressManager.
    /// </summary>
    public class RogueliteManager : MonoBehaviour
    {
        private static RogueliteManager _instance;
        public static RogueliteManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("RogueliteManager");
                    _instance = go.AddComponent<RogueliteManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // --- PlayerPrefs Keys ---
        private const string KEY_LEVEL_END_HEALTH = "Rogue_Level_{0}_EndHealth"; // int: health remaining
        private const string KEY_LEVEL_MAX_HEALTH = "Rogue_Level_{0}_MaxHealth"; // int: max health of that run
        private const string KEY_PLAYER_POINTS = "Rogue_Player_Points";
        private const string KEY_UPGRADE_PURCHASED = "Rogue_Upgrade_{0}_Purchased"; // int 0/1
        private const string KEY_EXTRA_JUMPS_PURCHASED = "Rogue_ExtraJumps_Purchased"; // int: count of extra jumps bought
        private const string KEY_MAX_HEALTH = "Rogue_MaxHealth"; // int: player's current max health

        /// <summary>
        /// The starting max health for a brand new player.
        /// </summary>
        public const int BASE_MAX_HEALTH = 3;

        /// <summary>
        /// Maximum number of extra jumps that can be purchased.
        /// </summary>
        public const int MAX_EXTRA_JUMPS = 2;

        /// <summary>
        /// Cost per extra jump upgrade (can be adjusted).
        /// </summary>
        public const int EXTRA_JUMP_COST = 100;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // =========================================================================
        // MAX HEALTH (single source of truth)
        // =========================================================================

        /// <summary>
        /// Get the player's current max health. This is the single source of truth
        /// used by PlayerHealth, all UIs, and level health tracking.
        /// </summary>
        public int GetMaxHealth()
        {
            return PlayerPrefs.GetInt(KEY_MAX_HEALTH, BASE_MAX_HEALTH);
        }

        /// <summary>
        /// Set the player's max health (e.g. after purchasing a health upgrade).
        /// </summary>
        public void SetMaxHealth(int maxHealth)
        {
            PlayerPrefs.SetInt(KEY_MAX_HEALTH, Mathf.Max(1, maxHealth));
            PlayerPrefs.Save();
            Debug.Log($"[RogueliteManager] Max health set to {maxHealth}");
        }

        /// <summary>
        /// Increase max health by an amount. Returns the new max.
        /// </summary>
        public int IncreaseMaxHealth(int amount)
        {
            int newMax = GetMaxHealth() + amount;
            SetMaxHealth(newMax);
            return newMax;
        }

        // =========================================================================
        // HEALTH TRACKING
        // =========================================================================

        /// <summary>
        /// Save the player's health at the end of a level.
        /// Called when a level is completed (not when dying).
        /// If the player improved compared to a previous completion of this level,
        /// the health improvement cascades forward to all subsequent levels
        /// that already have saved data.
        /// </summary>
        public void SaveLevelEndHealth(int levelNumber, int endHealth, int maxHealth)
        {
            int oldEndHealth = GetLevelEndHealth(levelNumber);
            int clampedEndHealth = Mathf.Min(endHealth, maxHealth);

            // Save this level's data
            PlayerPrefs.SetInt(string.Format(KEY_LEVEL_END_HEALTH, levelNumber), clampedEndHealth);
            PlayerPrefs.SetInt(string.Format(KEY_LEVEL_MAX_HEALTH, levelNumber), maxHealth);

            // Cascade improvement to subsequent levels
            if (oldEndHealth >= 0 && clampedEndHealth > oldEndHealth)
            {
                int delta = clampedEndHealth - oldEndHealth;
                CascadeHealthImprovement(levelNumber + 1, delta, maxHealth);
            }

            PlayerPrefs.Save();
            Debug.Log($"[RogueliteManager] Level {levelNumber} end health saved: {clampedEndHealth}/{maxHealth}");
        }

        /// <summary>
        /// Propagate a health improvement forward through all subsequent levels
        /// that already have saved completion data.
        /// Each level's stored end health is increased by the delta, clamped to max health.
        /// </summary>
        private void CascadeHealthImprovement(int fromLevel, int delta, int maxHealth)
        {
            // Walk forward through levels that have data and apply the delta
            for (int level = fromLevel; ; level++)
            {
                int storedHealth = GetLevelEndHealth(level);
                if (storedHealth < 0)
                    break; // No data for this level — stop cascading

                int newHealth = Mathf.Min(storedHealth + delta, maxHealth);
                PlayerPrefs.SetInt(string.Format(KEY_LEVEL_END_HEALTH, level), newHealth);
                PlayerPrefs.SetInt(string.Format(KEY_LEVEL_MAX_HEALTH, level), maxHealth);
                Debug.Log($"[RogueliteManager] Cascaded +{delta} to level {level}: {storedHealth} → {newHealth}/{maxHealth}");
            }
        }

        /// <summary>
        /// Get the health the player ended a level with.
        /// Returns -1 if the level hasn't been completed yet.
        /// </summary>
        public int GetLevelEndHealth(int levelNumber)
        {
            string key = string.Format(KEY_LEVEL_END_HEALTH, levelNumber);
            if (!PlayerPrefs.HasKey(key))
                return -1;
            return PlayerPrefs.GetInt(key);
        }

        /// <summary>
        /// Get the max health recorded for a level completion.
        /// Returns current max health if not recorded.
        /// </summary>
        public int GetLevelMaxHealth(int levelNumber)
        {
            string key = string.Format(KEY_LEVEL_MAX_HEALTH, levelNumber);
            return PlayerPrefs.GetInt(key, GetMaxHealth());
        }

        /// <summary>
        /// Returns true if we have saved health data for a level.
        /// </summary>
        public bool HasLevelHealthData(int levelNumber)
        {
            return PlayerPrefs.HasKey(string.Format(KEY_LEVEL_END_HEALTH, levelNumber));
        }

        /// <summary>
        /// Calculate the starting health for a given level based on the chain of previous level completions.
        /// Level 1 always starts with max health.
        /// Level N starts with whatever health the player ended Level N-1 with,
        /// clamped to current max health (in case max health was upgraded since).
        /// </summary>
        public int GetStartingHealthForLevel(int levelNumber)
        {
            int maxHealth = GetMaxHealth();

            if (levelNumber <= 1)
                return maxHealth;

            int previousLevelEndHealth = GetLevelEndHealth(levelNumber - 1);
            if (previousLevelEndHealth < 0)
            {
                // Previous level hasn't been completed, start with max
                return maxHealth;
            }

            // Clamp to current max (could have increased since the save)
            return Mathf.Min(previousLevelEndHealth, maxHealth);
        }

        /// <summary>
        /// Get the cumulative health — the health Yooba would start the next unplayed level with.
        /// This is the end health of the last completed level in sequence,
        /// clamped to current max health.
        /// If no levels completed, returns max health.
        /// </summary>
        public int GetCumulativeHealth(int totalLevels)
        {
            int maxHealth = GetMaxHealth();
            int health = maxHealth;

            for (int i = 1; i <= totalLevels; i++)
            {
                int endHealth = GetLevelEndHealth(i);
                if (endHealth < 0)
                {
                    // This level hasn't been completed — cumulative health is what we'd start it with
                    break;
                }
                health = Mathf.Min(endHealth, maxHealth);
            }

            return health;
        }

        /// <summary>
        /// Get a display-friendly health ratio (0.0 to 1.0) for a level.
        /// Returns -1 if no data exists.
        /// </summary>
        public float GetLevelHealthRatio(int levelNumber)
        {
            int endHealth = GetLevelEndHealth(levelNumber);
            if (endHealth < 0)
                return -1f;

            int maxHealth = GetLevelMaxHealth(levelNumber);
            return maxHealth > 0 ? (float)endHealth / maxHealth : 0f;
        }

        // =========================================================================
        // POINTS / CURRENCY
        // =========================================================================

        /// <summary>
        /// Fired whenever the player's points change. Passes the new total.
        /// </summary>
        public event System.Action<int> OnPointsChanged;

        /// <summary>
        /// Get the player's current points.
        /// </summary>
        public int GetPoints()
        {
            return PlayerPrefs.GetInt(KEY_PLAYER_POINTS, 0);
        }

        /// <summary>
        /// Add points to the player's total.
        /// </summary>
        public void AddPoints(int amount)
        {
            int current = GetPoints();
            int newTotal = current + amount;
            PlayerPrefs.SetInt(KEY_PLAYER_POINTS, newTotal);
            PlayerPrefs.Save();
            OnPointsChanged?.Invoke(newTotal);
        }

        /// <summary>
        /// Spend points. Returns true if the player had enough and the points were deducted.
        /// </summary>
        public bool SpendPoints(int amount)
        {
            int current = GetPoints();
            if (current < amount)
                return false;

            int newTotal = current - amount;
            PlayerPrefs.SetInt(KEY_PLAYER_POINTS, newTotal);
            PlayerPrefs.Save();
            OnPointsChanged?.Invoke(newTotal);
            return true;
        }

        /// <summary>
        /// Set points directly (useful for testing/debugging).
        /// </summary>
        public void SetPoints(int amount)
        {
            PlayerPrefs.SetInt(KEY_PLAYER_POINTS, amount);
            PlayerPrefs.Save();
        }

        // =========================================================================
        // UPGRADES - EXTRA JUMPS
        // =========================================================================

        /// <summary>
        /// Get the number of extra jumps the player has purchased (0 to MAX_EXTRA_JUMPS).
        /// This is the number of air jumps available. Player always has ground jump.
        /// A fresh player has 0 extra jumps (ground jump only, no air jumps).
        /// </summary>
        public int GetExtraJumpsPurchased()
        {
            return PlayerPrefs.GetInt(KEY_EXTRA_JUMPS_PURCHASED, 0);
        }

        /// <summary>
        /// Try to purchase one extra jump. Returns true if successful.
        /// </summary>
        public bool PurchaseExtraJump()
        {
            int current = GetExtraJumpsPurchased();
            if (current >= MAX_EXTRA_JUMPS)
            {
                Debug.Log("[RogueliteManager] Already at max extra jumps!");
                return false;
            }

            int cost = GetExtraJumpCost(current + 1);
            if (!SpendPoints(cost))
            {
                Debug.Log($"[RogueliteManager] Not enough points for extra jump! Need {cost}, have {GetPoints()}");
                return false;
            }

            PlayerPrefs.SetInt(KEY_EXTRA_JUMPS_PURCHASED, current + 1);
            PlayerPrefs.Save();
            Debug.Log($"[RogueliteManager] Purchased extra jump #{current + 1}! Air jumps now: {current + 1}");
            return true;
        }

        /// <summary>
        /// Get the cost of the Nth extra jump (1-indexed).
        /// Jump 1 costs EXTRA_JUMP_COST, Jump 2 costs EXTRA_JUMP_COST * 2.
        /// </summary>
        public int GetExtraJumpCost(int jumpNumber)
        {
            return EXTRA_JUMP_COST * jumpNumber;
        }

        /// <summary>
        /// Returns true if the player can afford the next extra jump.
        /// </summary>
        public bool CanAffordNextExtraJump()
        {
            int current = GetExtraJumpsPurchased();
            if (current >= MAX_EXTRA_JUMPS)
                return false;

            int cost = GetExtraJumpCost(current + 1);
            return GetPoints() >= cost;
        }

        /// <summary>
        /// Returns true if the player has purchased all available extra jumps.
        /// </summary>
        public bool HasMaxExtraJumps()
        {
            return GetExtraJumpsPurchased() >= MAX_EXTRA_JUMPS;
        }

        // =========================================================================
        // GENERIC UPGRADE TRACKING
        // =========================================================================

        /// <summary>
        /// Check if a named upgrade has been purchased.
        /// </summary>
        public bool IsUpgradePurchased(string upgradeId)
        {
            return PlayerPrefs.GetInt(string.Format(KEY_UPGRADE_PURCHASED, upgradeId), 0) == 1;
        }

        /// <summary>
        /// Mark a named upgrade as purchased.
        /// </summary>
        public void SetUpgradePurchased(string upgradeId)
        {
            PlayerPrefs.SetInt(string.Format(KEY_UPGRADE_PURCHASED, upgradeId), 1);
            PlayerPrefs.Save();
        }

        // =========================================================================
        // RESET
        // =========================================================================

        /// <summary>
        /// Reset all roguelite data. Called when starting a new game.
        /// </summary>
        public void ResetAllRogueliteData()
        {
            // Clear roguelite level health data (support up to 100 levels)
            for (int i = 1; i <= 100; i++)
            {
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_END_HEALTH, i));
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_MAX_HEALTH, i));
            }

            // Clear upgrades and max health (points are preserved across new games)
            PlayerPrefs.DeleteKey(KEY_EXTRA_JUMPS_PURCHASED);
            PlayerPrefs.DeleteKey(KEY_MAX_HEALTH);

            // Clear game progress data (level completions, video unlocks, highest level)
            for (int i = 1; i <= 100; i++)
            {
                PlayerPrefs.DeleteKey($"Level_{i}_Completed");
                PlayerPrefs.DeleteKey($"Video_{i}_Unlocked");
            }
            PlayerPrefs.DeleteKey("HighestLevelReached");
            PlayerPrefs.DeleteKey("GameInitialized");

            PlayerPrefs.Save();
            Debug.Log("[RogueliteManager] All game and roguelite data reset.");
        }
    }
}
