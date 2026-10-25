using System;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    [Serializable, JsonType("heroState")]
    [Doc("What the hero is carrying right now, for a companion scene to branch on (use it as an 'if' condition): hearts, diamonds, Grudges held, deaths so far, and 'requires' " +
         "(flag:<id>, grudge:<id>, shop:<id>, weapon:<id>, level:<n>; every one must hold). Every limit left at 0 is ignored. Reads the save and the hero, changes nothing.")]
    public sealed class HeroStateTrigger : Trigger
    {
        [Doc("True only while his hearts are at most this (0 = ignore).")] public int heartsAtMost;
        [Doc("True only while his hearts are at least this (0 = ignore).")] public int heartsAtLeast;
        [Doc("True only while he carries at least this many diamonds (0 = ignore).")] public int diamondsAtLeast;
        [Doc("True only while he carries fewer than this many diamonds (0 = ignore).")] public int diamondsBelow;
        [Doc("True only while he holds at least this many Grudges this run (0 = ignore).")] public int grudgesAtLeast;
        [Doc("True only once he has died at least this many times in the save (0 = ignore).")] public int deathsAtLeast;
        [Doc("Every condition must hold: flag:<id>, grudge:<id>[:level], shop:<id>[:level], weapon:<id>, level:<n>.")] public System.Collections.Generic.List<string> requires = new System.Collections.Generic.List<string>();

        public override bool Check(TriggerContext context)
        {
            var player = context.World.Player;
            if (heartsAtMost > 0 && (player == null || player.Health > heartsAtMost)) return false;
            if (heartsAtLeast > 0 && (player == null || player.Health < heartsAtLeast)) return false;
            var roguelite = SickOfIt.Data.RogueliteManager.Instance;
            int diamonds = roguelite != null ? roguelite.GetPoints() : 0;
            if (diamondsAtLeast > 0 && diamonds < diamondsAtLeast) return false;
            if (diamondsBelow > 0 && diamonds >= diamondsBelow) return false;
            if (grudgesAtLeast > 0)
            {
                int held = 0;
                foreach (var kv in SickOfIt.Data.UpgradeProgress.Owned()) held += kv.Value;
                if (held < grudgesAtLeast) return false;
            }
            if (deathsAtLeast > 0 && SickOfIt.Data.YoobaProgress.Deaths < deathsAtLeast) return false;
            return SickOfIt.Data.Requirements.Met(requires);
        }
    }
}
