using System.Globalization;
using SickOfIt.Data.Config;

namespace SickOfIt.Data
{
    /// <summary>The three tabs of Yooba's counter.</summary>
    public enum ShopTab { Body, Rewind, Loot }

    /// <summary>
    /// What a shop card leads with (Docs/Story.md "The shop reads at a glance"): the ware's effect in plain words and numbers, made from its
    /// <see cref="ShopEffect"/> and its per-level amount ("+2 hearts", "+8% move speed", "+0.4 s of rewind per clean kill"), and which tab
    /// it sits on. The joke name and Yooba's quip come second, smaller.
    /// </summary>
    public static class ShopCardText
    {
        public const int CardsPerPage = 6;

        /// <summary>The tab a ware sits on: what rewinds, what pays and hits (Loot), the rest keeps the hero alive and moving (Body).</summary>
        public static ShopTab TabOf(ShopEffect effect)
        {
            switch (effect)
            {
                case ShopEffect.RewindJuice:
                case ShopEffect.RewindSpeed:
                case ShopEffect.CleanKillBonus:
                case ShopEffect.RewindDiscount:
                    return ShopTab.Rewind;
                case ShopEffect.Magnet:
                case ShopEffect.DiamondValue:
                case ShopEffect.KeepWeapons:
                case ShopEffect.FireRate:
                case ShopEffect.Damage:
                case ShopEffect.ShotSpeed:
                case ShopEffect.CritChance:
                case ShopEffect.CritDamage:
                case ShopEffect.BombCharges:
                case ShopEffect.BombRadius:
                case ShopEffect.MeleeDamage:
                case ShopEffect.MeleeReach:
                    return ShopTab.Loot;
                default:
                    return ShopTab.Body;
            }
        }

        /// <summary>What buying the next level of this ware does, in words and numbers.</summary>
        public static string Effect(UpgradeDefinition def)
        {
            if (def == null)
                return "";
            float a = def.amountPerLevel;
            switch (def.Effect)
            {
                case ShopEffect.MaxHealth: return Plus(a, a == 1f ? " heart" : " hearts");
                case ShopEffect.ExtraJump: return Plus(a, a == 1f ? " air jump" : " air jumps");
                case ShopEffect.MoveSpeed: return Percent(a, " move speed");
                case ShopEffect.FireRate: return Percent(a, " fire rate");
                case ShopEffect.Damage: return Percent(a, " shot damage");
                case ShopEffect.RewindJuice: return Plus(a, " s of rewind");
                case ShopEffect.Magnet: return Plus(a, " diamond pull range");
                case ShopEffect.RewindSpeed: return Percent(a, " rewind speed");
                case ShopEffect.MeleeDamage: return Percent(a, " fist damage");
                case ShopEffect.MeleeReach: return Percent(a, " fist reach");
                case ShopEffect.KeepWeapons: return "Keep 100% of your weapons";
                case ShopEffect.Dash: return "+1 dash (Shift / B)";
                case ShopEffect.Armor: return Plus(a, a == 1f ? " armour plate" : " armour plates");
                case ShopEffect.ShieldCharges: return Plus(a, a == 1f ? " bubble per floor" : " bubbles per floor");
                case ShopEffect.Dodge: return Percent(a, " chance to slip a hit");
                case ShopEffect.CritChance: return Percent(a, " crit chance");
                case ShopEffect.CritDamage: return Percent(a, " crit damage");
                case ShopEffect.HealOnKill: return Percent(a, " chance a kill heals");
                case ShopEffect.DiamondValue: return Percent(a, " diamond value");
                case ShopEffect.SlowShots: return "-" + Num(a * 100f) + "% enemy shot speed";
                case ShopEffect.CleanKillBonus: return Plus(a, " s per clean kill");
                case ShopEffect.RewindDiscount: return "-" + Num(a * 100f) + "% rewind cost";
                case ShopEffect.BombCharges: return Plus(a, a == 1f ? " bomb per floor" : " bombs per floor");
                case ShopEffect.BombRadius: return Plus(a, " bomb radius");
                case ShopEffect.Afterglow: return Plus(a, " s untouchable after a hit");
                case ShopEffect.ShotSpeed: return Percent(a, " shot speed");
                case ShopEffect.FloorHeal: return Plus(a, a == 1f ? " heart back each floor" : " hearts back each floor");
                case ShopEffect.LastGasp: return Plus(a, " last gasp per floor");
                default: return "";
            }
        }

        /// <summary>The second, smaller line: the joke name, then Yooba's quip.</summary>
        public static string NameAndQuip(UpgradeDefinition def)
        {
            if (def == null)
                return "";
            return string.IsNullOrEmpty(def.description) ? def.title : "<b>" + def.title + "</b>\n" + def.description;
        }

        static string Plus(float amount, string unit) => "+" + Num(amount) + unit;

        static string Percent(float fraction, string unit) => "+" + Num(fraction * 100f) + "%" + unit;

        static string Num(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
