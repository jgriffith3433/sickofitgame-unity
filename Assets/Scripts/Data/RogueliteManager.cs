using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data.Config;

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
        private static bool _applicationIsQuitting;

        public static RogueliteManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;
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
        private const string KEY_SHOP_LEVEL = "Rogue_Shop_{0}"; // int: levels bought of a shop upgrade

        /// <summary>
        /// Fallback starting max health (used if GameDefinition is unavailable).
        /// Runtime values come from GameDefinition.player.
        /// </summary>
        public const int BASE_MAX_HEALTH_FALLBACK = 5;

        /// <summary>
        /// Fallback max extra jumps (used if GameDefinition is unavailable).
        /// </summary>
        public const int MAX_EXTRA_JUMPS_FALLBACK = 2;

        /// <summary>
        /// Fallback cost per extra jump (used if GameDefinition is unavailable).
        /// </summary>
        public const int EXTRA_JUMP_COST_FALLBACK = 100;

        /// <summary>Read from GameDefinition.player at runtime.</summary>
        public int BaseMaxHealth
        {
            get
            {
                var gd = GameDefinition.Instance;
                return gd != null ? gd.player.baseMaxHealth : BASE_MAX_HEALTH_FALLBACK;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _applicationIsQuitting = false;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
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
            return PlayerPrefs.GetInt(KEY_MAX_HEALTH, BaseMaxHealth);
        }

        /// <summary>
        /// Set the player's max health (e.g. after purchasing a health upgrade).
        /// </summary>
        public void SetMaxHealth(int maxHealth)
        {
            PlayerPrefs.SetInt(KEY_MAX_HEALTH, Mathf.Max(1, maxHealth));
            PlayerPrefs.Save();
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
            ////Debug.Log($"[RogueliteManager] Level {levelNumber} end health saved: {clampedEndHealth}/{maxHealth}");
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
                ////Debug.Log($"[RogueliteManager] Cascaded +{delta} to level {level}: {storedHealth} → {newHealth}/{maxHealth}");
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
        // SHOP UPGRADES (bought with diamonds, kept until New Game)
        // =========================================================================

        /// <summary>
        /// What the shop sells: player.availableUpgrades, or upgradeShopConfig.upgrades when that's empty.
        /// </summary>
        public static IList<UpgradeDefinition> ShopCatalog
        {
            get
            {
                var gd = GameDefinition.Instance;
                if (gd == null)
                    return new UpgradeDefinition[0];
                if (gd.player != null && gd.player.availableUpgrades != null && gd.player.availableUpgrades.Count > 0)
                    return gd.player.availableUpgrades;
                return gd.upgradeShopConfig != null && gd.upgradeShopConfig.upgrades != null
                    ? (IList<UpgradeDefinition>)gd.upgradeShopConfig.upgrades
                    : new UpgradeDefinition[0];
            }
        }

        static string LevelKey(string id) => id == "extraJump" ? KEY_EXTRA_JUMPS_PURCHASED : string.Format(KEY_SHOP_LEVEL, id);

        /// <summary>How many levels of a shop upgrade the player owns.</summary>
        public int GetUpgradeLevel(string id) => string.IsNullOrEmpty(id) ? 0 : PlayerPrefs.GetInt(LevelKey(id), 0);

        /// <summary>Price of the next level, or -1 when maxed.</summary>
        public int GetNextCost(UpgradeDefinition upgrade)
        {
            int level = GetUpgradeLevel(upgrade.id);
            if (level >= upgrade.maxLevel)
                return -1;
            if (upgrade.costsPerLevel == null || upgrade.costsPerLevel.Length == 0)
                return 0;
            return upgrade.costsPerLevel[Mathf.Clamp(level, 0, upgrade.costsPerLevel.Length - 1)];
        }

        public bool CanAfford(UpgradeDefinition upgrade)
        {
            int cost = GetNextCost(upgrade);
            return cost >= 0 && GetPoints() >= cost;
        }

        /// <summary>
        /// Buys the next level: spends the diamonds, saves the level, and applies what persists outside levels
        /// (max health, which also tops up the health carried between levels). Returns false if maxed or too poor.
        /// </summary>
        public bool TryPurchase(UpgradeDefinition upgrade)
        {
            int cost = GetNextCost(upgrade);
            if (cost < 0 || !SpendPoints(cost))
                return false;
            PlayerPrefs.SetInt(LevelKey(upgrade.id), GetUpgradeLevel(upgrade.id) + 1);
            if (upgrade.Effect == ShopEffect.MaxHealth)
            {
                int amount = Mathf.Max(1, Mathf.RoundToInt(upgrade.amountPerLevel));
                int max = IncreaseMaxHealth(amount);
                int totalLevels = GameDefinition.Instance != null ? GameDefinition.Instance.TotalLevels : 0;
                for (int i = 1; i <= totalLevels; i++)
                {
                    if (!HasLevelHealthData(i))
                        continue;
                    string key = string.Format(KEY_LEVEL_END_HEALTH, i);
                    PlayerPrefs.SetInt(key, Mathf.Min(max, PlayerPrefs.GetInt(key) + amount));
                }
            }
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>The summed effect of every owned shop upgrade of one kind (e.g. extra jumps, +% fire rate).</summary>
        public float GetShopTotal(ShopEffect effect)
        {
            float total = 0f;
            foreach (var upgrade in ShopCatalog)
            {
                if (upgrade != null && upgrade.Effect == effect)
                    total += GetUpgradeLevel(upgrade.id) * upgrade.amountPerLevel;
            }
            return total;
        }

        /// <summary>Air jumps bought in the shop. The player always has the ground jump.</summary>
        public int GetExtraJumpsPurchased() => Mathf.RoundToInt(GetShopTotal(ShopEffect.ExtraJump));

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
        // RUNS (Docs/Story.md, "Waystones and runs")
        // =========================================================================

        /// <summary>
        /// A run is over (Hell killed the hero and sends him back to a Waystone): his Grudges go, and the health he
        /// carried from level to level, so the next run starts fresh. Diamonds, Gloob's goods (shop upgrades and the
        /// max health they gave), level unlocks and the Waystones are kept.
        /// </summary>
        public void EndRun()
        {
            int totalLevels = GameDefinition.Instance?.TotalLevels ?? 100;
            for (int i = 1; i <= totalLevels; i++)
            {
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_END_HEALTH, i));
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_MAX_HEALTH, i));
            }
            UpgradeProgress.Reset();

            // Max health is one number for both: a Grudge (thickSkin) raised it too. Back to the base plus Gloob's goods.
            int fromShop = 0;
            foreach (var upgrade in ShopCatalog)
            {
                if (upgrade != null && upgrade.Effect == ShopEffect.MaxHealth)
                    fromShop += GetUpgradeLevel(upgrade.id) * Mathf.Max(1, Mathf.RoundToInt(upgrade.amountPerLevel));
            }
            SetMaxHealth(BaseMaxHealth + fromShop);
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
            int totalLevels = GameDefinition.Instance?.TotalLevels ?? 100;

            // Clear roguelite level health data
            for (int i = 1; i <= totalLevels; i++)
            {
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_END_HEALTH, i));
                PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL_MAX_HEALTH, i));
            }

            // Clear upgrades and max health (points are preserved across new games)
            PlayerPrefs.DeleteKey(KEY_EXTRA_JUMPS_PURCHASED);
            foreach (var upgrade in ShopCatalog)
            {
                if (upgrade != null && !string.IsNullOrEmpty(upgrade.id))
                    PlayerPrefs.DeleteKey(LevelKey(upgrade.id));
            }
            PlayerPrefs.DeleteKey(KEY_MAX_HEALTH);
            UpgradeProgress.Reset();

            // Clear game progress data (level completions, video unlocks, highest level)
            for (int i = 1; i <= totalLevels; i++)
            {
                PlayerPrefs.DeleteKey($"Level_{i}_Completed");
                PlayerPrefs.DeleteKey($"Video_{i}_Unlocked");
            }
            PlayerPrefs.DeleteKey("HighestLevelReached");
            PlayerPrefs.DeleteKey("GameInitialized");
            ShaftProgress.Reset();
            ShortcutProgress.Reset();
            GloobProgress.Reset();
            WaystoneProgress.Reset();

            PlayerPrefs.Save();
            ////Debug.Log("[RogueliteManager] All game and roguelite data reset.");
        }
    }
}
