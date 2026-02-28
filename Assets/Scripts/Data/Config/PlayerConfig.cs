using System;
using System.Collections.Generic;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// All player-related configuration.
    /// TopDownController and UpgradeShopManager read from this at runtime.
    /// </summary>
    [Serializable]
    public class PlayerConfig
    {
        [Header("Movement")]
        public float moveSpeed = 2f;
        public float jumpForce = 15f;
        public float gravityScale = 1.8f;
        public float groundCheckDistance = 0.2f;
        public float groundCheckWidth = 0.1f;

        [Header("Combat")]
        public bool enableShooting = true;
        public float fireRate = 0.25f;
        public float projectileSpeed = 10f;
        public float projectileLifetime = 5f;
        public string projectileAddress = "Projectile_Player";

        [Header("Roguelite")]
        public int baseMaxHealth = 5;
        public int maxExtraJumps = 2;
        public int extraJumpCost = 100;

        [Header("Upgrades")]
        public List<UpgradeDefinition> availableUpgrades = new List<UpgradeDefinition>();
    }

    /// <summary>
    /// Defines one purchasable upgrade in the shop.
    /// </summary>
    [Serializable]
    public class UpgradeDefinition
    {
        public string upgradeId;
        public string displayName;
        public string description;
        public int cost;
        public int maxPurchases = 1;
    }
}
