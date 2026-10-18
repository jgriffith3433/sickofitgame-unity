using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Hell's shortcuts (Docs/Story.md, "Shortcuts"): a choice point in a stage that skips floors for a cost. Like an
    /// upgrade offer it is meta-progression, outside the rewound simulation: offered once per play, chosen once, and the
    /// choice is kept through rewinds (<see cref="ShortcutChosenTrigger"/> reads it), so a replay takes the same branch
    /// and the cost is never paid twice. A taken shortcut changes where the stage leads (<see cref="Taken"/>, read by
    /// LevelRuntime) and is saved for the Shaft (<see cref="ShortcutProgress"/>) once the level is cleared.
    /// </summary>
    public sealed class ShortcutSystem
    {
        readonly GameplayWorld world;
        readonly PlayerHealth health;
        readonly Dictionary<string, bool> choices = new Dictionary<string, bool>(StringComparer.Ordinal);

        public ShortcutSystem(GameplayWorld world, PlayerHealth health)
        {
            this.world = world;
            this.health = health;
        }

        /// <summary>The shortcut taken in this play of the level, or null.</summary>
        public OfferShortcutAction Taken { get; private set; }
        /// <summary>The floor the counter was on when it was taken.</summary>
        public int TakenFromFloor { get; private set; }
        /// <summary>The Grudge it cost (its title), or "".</summary>
        public string Paid { get; private set; } = "";

        /// <summary>True = taken, false = passed by, null = not chosen yet.</summary>
        public bool? Choice(string id) => !string.IsNullOrEmpty(id) && choices.TryGetValue(id, out bool taken) ? taken : (bool?)null;

        public void Offer(OfferShortcutAction action, ActionContext context)
        {
            if (string.IsNullOrEmpty(action.id) || !world.TryGrantOnce("shortcut:" + action.id))
                return;
            var grudge = action.costGrudges > 0 ? GrudgeToPay() : null;
            bool canPay = action.costGrudges <= 0 || grudge != null;
            string cost = action.costGrudges <= 0 ? "Free" : grudge != null
                ? string.Format(CultureInfo.InvariantCulture, action.costText, grudge.title)
                : string.Format(CultureInfo.InvariantCulture, action.costText, "none left");
            world.Telemetry.Record("shortcut-offer", action.id, world.Stage.Floor, cost + (canPay ? "" : " (can't pay)"));

            if (world.Hud == null)
            {
                Choose(action, false, null);
                return;
            }
            var stay = new MenuCardData { Badge = "1", Title = action.stayTitle, Description = action.stayDescription, Status = "Keep climbing" };
            var take = new MenuCardData { Badge = "2", Caption = cost, Title = action.title, Description = action.description, Status = "Take it", StatusColor = action.color };
            world.Hud.ShowShortcutChoice(action.prompt, stay, take, action.color, cost, canPay ? null : action.cantPayText,
                i => Choose(action, i == 1 && canPay, grudge));
        }

        /// <summary>The Grudge a shortcut takes: the one he has most levels of (ties by id), so the cost is always the same for the same run.</summary>
        UpgradeDef GrudgeToPay()
        {
            var owned = UpgradeProgress.Owned()
                .Where(p => p.Value > 0 && world.Content.Upgrades.ContainsKey(p.Key))
                .OrderByDescending(p => p.Value)
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .ToList();
            return owned.Count > 0 ? world.Content.Upgrades[owned[0].Key] : null;
        }

        void Choose(OfferShortcutAction action, bool take, UpgradeDef grudge)
        {
            if (choices.ContainsKey(action.id))
                return;
            choices[action.id] = take;
            if (!take)
            {
                world.Telemetry.Record("shortcut-declined", action.id, world.Stage.Floor);
                return;
            }
            if (grudge != null && UpgradeProgress.Remove(grudge.id))
            {
                Paid = grudge.title;
                // A Grudge that gave max health takes it back with it.
                if (grudge.maxHealthPerLevel > 0 && RogueliteManager.Instance != null)
                {
                    int max = RogueliteManager.Instance.IncreaseMaxHealth(-grudge.maxHealthPerLevel);
                    health?.SetMaxHealth(max, 0);
                }
            }
            Taken = action;
            TakenFromFloor = world.Stage.Floor;
            world.Telemetry.Record("shortcut", action.id, action.toFloor,
                $"from FLOOR {TakenFromFloor} to FLOOR {action.toFloor}, next level {action.nextLevel}, paid {(Paid.Length > 0 ? Paid : "nothing")}");
            if (world.Player != null)
                world.Effects.PopText(world.Player.Position + Vector2.up * 0.6f, Paid.Length > 0 ? "-" + Paid.ToUpperInvariant() : "SHORTCUT", action.color, 1.2f);
        }
    }
}
