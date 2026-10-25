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

        /// <summary>True when this level was reached through shortcut 'id' (read once, when the level starts: <see cref="ShortcutTakenTrigger"/>).</summary>
        public bool ArrivedBy(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (!arrivals.TryGetValue(id, out bool arrived))
                arrivals[id] = arrived = ShortcutProgress.ArrivedBy(id, world.LevelNumber);
            return arrived;
        }

        readonly Dictionary<string, bool> arrivals = new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>True = taken, false = passed by, null = not chosen yet.</summary>
        public bool? Choice(string id) => !string.IsNullOrEmpty(id) && choices.TryGetValue(id, out bool taken) ? taken : (bool?)null;

        public void Offer(OfferShortcutAction action, ActionContext context)
        {
            if (string.IsNullOrEmpty(action.id) || !world.TryGrantOnce("shortcut:" + action.id))
                return;
            var grudge = action.costGrudges > 0 ? GrudgeToPay() : null;
            int diamonds = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
            bool canPay = (action.costGrudges <= 0 || grudge != null) && (action.costDiamonds <= 0 || diamonds >= action.costDiamonds);
            string cost = action.costDiamonds > 0
                ? string.Format(CultureInfo.InvariantCulture, action.costText, action.costDiamonds)
                : action.costGrudges <= 0 ? (string.IsNullOrEmpty(action.costText) || action.costText.Contains("{0}") ? "Free" : action.costText) : grudge != null
                ? string.Format(CultureInfo.InvariantCulture, action.costText, grudge.title)
                : string.Format(CultureInfo.InvariantCulture, action.costText, "none left");
            world.Telemetry.Record("shortcut-offer", action.id, world.Stage.Floor, cost + (canPay ? "" : " (can't pay)")
                + (action.costDiamonds > 0 ? $"; diamonds {diamonds}" : ""));

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
                // A hidden Grudge (weight 0: Held Breath, found on a secret floor) is never the one a shortcut takes.
                .Where(p => p.Value > 0 && world.Content.Upgrades.TryGetValue(p.Key, out var held) && held.weight > 0f)
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
            if (action.costDiamonds > 0 && RogueliteManager.Instance != null && RogueliteManager.Instance.SpendPoints(action.costDiamonds))
                Paid = action.costDiamonds.ToString(CultureInfo.InvariantCulture) + " diamonds";
            Taken = action;
            TakenFromFloor = world.Stage.Floor;
            world.Telemetry.Record("shortcut", action.id, action.toFloor,
                $"from FLOOR {TakenFromFloor} to FLOOR {action.toFloor}, next level {action.nextLevel}, paid {(Paid.Length > 0 ? Paid : "nothing")}");
            if (world.Player != null)
                world.Effects.PopText(world.Player.Position + Vector2.up * 0.6f, Paid.Length > 0 ? "-" + Paid.ToUpperInvariant() : "SHORTCUT", action.color, 1.2f);
        }
    }
}
