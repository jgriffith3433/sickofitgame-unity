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

        /// <summary>
        /// Default max health for the player (should match HealthSystem's maxHealth).
        /// </summary>
        public const int DEFAULT_MAX_HEALTH = 3;

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
        // HEALTH TRACKING
        // =========================================================================

        /// <summary>
        /// Save the player's health at the end of a level.
        /// Called when a level is completed (not when dying).
        /// </summary>
        public void SaveLevelEndHealth(int levelNumber, int endHealth, int maxHealth)
        {
            PlayerPrefs.SetInt(string.Format(KEY_LEVEL_END_HEALTH, levelNumber), endHealth);
            PlayerPrefs.SetInt(string.Format(KEY_LEVEL_MAX_HEALTH, levelNumber), maxHealth);
            PlayerPrefs.Save();
            Debug.Log($"[RogueliteManager] Level {levelNumber} end health saved: {endHealth}/{maxHealth}");
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
        /// Returns DEFAULT_MAX_HEALTH if not recorded.
        /// </summary>
        public int GetLevelMaxHealth(int levelNumber)
        {
            string key = string.Format(KEY_LEVEL_MAX_HEALTH, levelNumber);
            return PlayerPrefs.GetInt(key, DEFAULT_MAX_HEALTH);
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
        /// Level N starts with whatever health the player ended Level N-1 with.
        /// </summary>
        public int GetStartingHealthForLevel(int levelNumber)
        {
            if (levelNumber <= 1)
                return DEFAULT_MAX_HEALTH;

            int previousLevelEndHealth = GetLevelEndHealth(levelNumber - 1);
            if (previousLevelEndHealth < 0)
            {
                // Previous level hasn't been completed, start with max
                return DEFAULT_MAX_HEALTH;
            }

            return previousLevelEndHealth;
        }

        /// <summary>
        /// Get the cumulative health — the health Yooba would start the next unplayed level with.
        /// This is the end health of the last completed level in sequence.
        /// If no levels completed, returns max health.
        /// </summary>
        public int GetCumulativeHealth(int totalLevels)
        {
            // Walk the chain from level 1 forward
            int health = DEFAULT_MAX_HEALTH;

            for (int i = 1; i <= totalLevels; i++)
            {
                int endHealth = GetLevelEndHealth(i);
                if (endHealth < 0)
                {
                    // This level hasn't been completed — cumulative health is what we'd start it with
                    break;
                }
                health = endHealth;
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
            // Clear level health data (support up to 100 levels)
            for (int i = 1; i <= 100; i++)
            {
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_END_HEALTH, i));
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_MAX_HEALTH, i));
            }

            // Clear points and upgrades
            PlayerPrefs.DeleteKey(KEY_PLAYER_POINTS);
            PlayerPrefs.DeleteKey(KEY_EXTRA_JUMPS_PURCHASED);

            PlayerPrefs.Save();
            Debug.Log("[RogueliteManager] All roguelite data reset.");
        }
    }
}
