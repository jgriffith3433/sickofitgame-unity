using System.Collections.Generic;

namespace SickOfIt.Data
{
    /// <summary>
    /// The <c>requires</c> gate on shop goods (player.json availableUpgrades) and Grudges (content/upgrades): a list of
    /// conditions, ALL of which must hold before the item is offered. Until then it is hidden, not greyed out.
    /// Forms: <c>flag:&lt;id&gt;</c> (an <see cref="UnlockFlags"/> id), <c>weapon:&lt;id&gt;</c> (flag "weapon:&lt;id&gt;"),
    /// <c>shop:&lt;id&gt;[:&lt;level&gt;]</c> (a shop good owned, at that level or more, default 1), <c>grudge:&lt;id&gt;[:&lt;level&gt;]</c>
    /// (a Grudge held this run), <c>level:&lt;n&gt;</c> (level n cleared). An item the hero already owns is never hidden.
    /// </summary>
    public static class Requirements
    {
        static readonly string[] Kinds = { "flag", "weapon", "shop", "grudge", "level" };

        public static bool Met(IList<string> requires)
        {
            if (requires == null)
                return true;
            foreach (string r in requires)
                if (!MetOne(r))
                    return false;
            return true;
        }

        /// <summary>Null if the condition is well-formed, else what is wrong with it (the content validator reports it).</summary>
        public static string Check(string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement))
                return "empty requirement";
            var parts = requirement.Split(':');
            if (System.Array.IndexOf(Kinds, parts[0]) < 0)
                return $"unknown requirement \"{requirement}\" (use flag:, weapon:, shop:, grudge: or level:)";
            if (parts.Length < 2 || parts[1].Length == 0)
                return $"requirement \"{requirement}\" names nothing";
            if (parts[0] == "level" && !(parts.Length == 2 && int.TryParse(parts[1], out _)))
                return $"requirement \"{requirement}\": level: takes a level number";
            if ((parts[0] == "shop" || parts[0] == "grudge") && parts.Length == 3 && !int.TryParse(parts[2], out _))
                return $"requirement \"{requirement}\": the level must be a number";
            return null;
        }

        static bool MetOne(string requirement)
        {
            if (Check(requirement) != null)
                return false;
            var parts = requirement.Split(':');
            int level = parts.Length == 3 && int.TryParse(parts[2], out int l) ? l : 1;
            switch (parts[0])
            {
                case "flag": return UnlockFlags.Has(parts[1]);
                case "weapon": return UnlockFlags.Has("weapon:" + parts[1]);
                case "shop": return RogueliteManager.Instance != null && RogueliteManager.Instance.GetUpgradeLevel(parts[1]) >= level;
                case "grudge": return UpgradeProgress.LevelOf(parts[1]) >= level;
                case "level": return GameProgressManager.Instance != null && GameProgressManager.Instance.IsLevelCompleted(int.Parse(parts[1]));
            }
            return false;
        }
    }
}
