using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core.Input;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Applies the player's saved upgrades when a level starts, and grants new ones (from offerUpgrades / grantUpgrade).
    /// Upgrades are meta-progression: not part of the rewound simulation, and granted at most once per offer per
    /// play of the level, even if a rewind replays the moment they were offered.
    /// </summary>
    public sealed class UpgradeSystem
    {
        readonly GameplayWorld world;
        readonly PlayerController controller;
        readonly PlayerHealth health;
        int extraShots;

        public UpgradeSystem(GameplayWorld world, PlayerController controller, PlayerHealth health)
        {
            this.world = world;
            this.controller = controller;
            this.health = health;
        }

        /// <summary>Applies every owned upgrade level. Called once when the level starts.</summary>
        /// <summary>
        /// Applies the diamond-shop upgrades for this level: speed, fire rate and damage as permanent player
        /// modifiers, extra rewind juice, and pickup magnet range. (Max health and air jumps are read by the player
        /// directly from RogueliteManager.) Constant for the whole level, so rewinds and replays agree.
        /// </summary>
        public void ApplyShop()
        {
            var roguelite = RogueliteManager.Instance;
            if (roguelite == null)
                return;
            var player = world.Player;
            void Boost(Stat stat, ShopEffect effect)
            {
                float total = roguelite.GetShopTotal(effect);
                if (player != null && total > 0f)
                    player.AddModifier(stat, 1f + total, 0f, float.PositiveInfinity);
            }
            Boost(Stat.MoveSpeed, ShopEffect.MoveSpeed);
            Boost(Stat.AttackSpeed, ShopEffect.FireRate);
            Boost(Stat.Damage, ShopEffect.Damage);
            float juice = roguelite.GetShopTotal(ShopEffect.RewindJuice);
            if (juice > 0f)
                RewindBudget.Instance?.RaiseMax(juice, fill: true);
            world.PickupMagnetBonus = roguelite.GetShopTotal(ShopEffect.Magnet);
            world.Perks?.AddShop(roguelite);
            world.Perks?.Arm();
        }

        public void ApplyOwned()
        {
            foreach (var pair in UpgradeProgress.Owned().OrderBy(p => p.Key, System.StringComparer.Ordinal))
            {
                if (!world.Content.Upgrades.TryGetValue(pair.Key, out var def))
                    continue;
                for (int i = 0; i < pair.Value; i++)
                    ApplyLevel(def, picked: false);
            }
        }

        void ApplyLevel(UpgradeDef def, bool picked)
        {
            var player = world.Player;
            if (player != null)
            {
                foreach (var m in def.modifiersPerLevel)
                    player.AddModifier(m.stat, m.multiply, m.add, float.PositiveInfinity);
            }
            if (def.extraShotsPerLevel != 0)
            {
                extraShots += def.extraShotsPerLevel;
                if (controller != null)
                    controller.ExtraShots = Mathf.Max(0, extraShots);
            }
            foreach (var perk in def.perksPerLevel)
                world.Perks?.Add(perk.effect, perk.amount);
            if (def.rewindSecondsPerLevel > 0f)
                RewindBudget.Instance?.RaiseMax(def.rewindSecondsPerLevel, fill: true);
            if (def.invincibilitySecondsPerLevel > 0f)
                health?.AddInvincibilityBonus(def.invincibilitySecondsPerLevel);
            // Max health is stored by RogueliteManager, so it is added only when picked (it already persists).
            if (picked && def.maxHealthPerLevel > 0)
            {
                int max = RogueliteManager.Instance.IncreaseMaxHealth(def.maxHealthPerLevel);
                health?.SetMaxHealth(max, heal: def.maxHealthPerLevel);
            }
        }

        public bool IsMaxed(UpgradeDef def) => UpgradeProgress.LevelOf(def.id) >= def.maxLevel;

        /// <summary>Gives one level of an upgrade, once for this key.</summary>
        public void Grant(string upgradeId, string onceKey)
        {
            if (!world.TryGrantOnce(onceKey))
                return;
            GrantNow(upgradeId);
        }

        void GrantNow(string upgradeId)
        {
            if (!world.Content.Upgrades.TryGetValue(upgradeId, out var def) || IsMaxed(def))
                return;
            UpgradeProgress.Add(upgradeId);
            ApplyLevel(def, picked: true);
            world.Telemetry.Record("upgrade", upgradeId, UpgradeProgress.LevelOf(upgradeId));
            if (world.Player != null)
                world.Effects.PopText(world.Player.Position + Vector2.up * 0.6f, def.title.ToUpperInvariant() + "!", def.color, 1.2f);
        }

        /// <summary>Shows the pick-one panel (pausing the game) for an offerUpgrades action, once per play of the level.</summary>
        public void Offer(OfferUpgradesAction action, ActionContext context)
        {
            int index = context.Director != null ? context.Director.IndexOfAction(action) : 0;
            if (!world.TryGrantOnce("offer:" + index))
                return;

            var pool = (action.choices.Count > 0
                    ? action.choices.Select(id => world.Content.Upgrades.TryGetValue(id, out var d) ? d : null).Where(d => d != null)
                    : world.Content.Upgrades.Values.Where(d => d.weight > 0f))
                .Where(d => !IsMaxed(d) && Requirements.Met(d.requires))
                .OrderBy(d => d.id, System.StringComparer.Ordinal)
                .ToList();
            if (pool.Count == 0)
                return;

            // Its own random stream: offering must not shift the simulation's rolls, or a rewind replay would differ.
            var random = new System.Random(world.LevelNumber * 7919 + index);
            var picks = new List<UpgradeDef>();
            while (picks.Count < action.count && pool.Count > 0)
            {
                float total = pool.Sum(d => Mathf.Max(0.0001f, d.weight));
                float roll = (float)random.NextDouble() * total;
                int chosen = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    roll -= Mathf.Max(0.0001f, pool[i].weight);
                    if (roll < 0f) { chosen = i; break; }
                }
                picks.Add(pool[chosen]);
                pool.RemoveAt(chosen);
            }

            // Which Grudges were offered and which was taken (the analytics' choice event, PlayAnalytics).
            string offered = string.Join(",", picks.Select(d => d.id));
            world.Telemetry.Record("upgradeOffer", offered, picks.Count);
            world.Hud.ShowUpgradePicker(action.title, picks, i =>
            {
                world.Telemetry.Record("upgradePick", picks[i].id, i, offered);
                GrantNow(picks[i].id);
            });
        }
    }
}
