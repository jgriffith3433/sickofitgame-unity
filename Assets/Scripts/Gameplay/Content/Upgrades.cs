using System;
using System.Collections.Generic;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>content/upgrades/{id}.json</summary>
    [Serializable]
    [Doc("A permanent player upgrade, offered with the offerUpgrades action (pick one of a few) or given with grantUpgrade. " +
         "Owned upgrades are saved and reset on New Game. Each level of the upgrade applies its per-level values again. File: content/upgrades/{id}.json.")]
    public class UpgradeDef
    {
        [NonSerialized] public string id;

        [Required] public string title = "";
        [Doc("Shown on the upgrade card. Be funny.")] public string description = "";
        public int maxLevel = 3;
        [Doc("How often it is offered relative to others (0 = only when listed explicitly).")] public float weight = 1f;
        [Doc("Player stat changes per level: AttackSpeed (fire rate), Damage, MoveSpeed, DamageTaken.")] public List<StatModifierDef> modifiersPerLevel = new List<StatModifierDef>();
        [Doc("Extra shots per volley, per level (fanned out).")] public int extraShotsPerLevel;
        [Doc("Seconds added to the rewind juice maximum (and filled), per level.")] public float rewindSecondsPerLevel;
        [Doc("Maximum health added per level (permanent, applied when picked).")] public int maxHealthPerLevel;
        [Doc("Card color.")] public UnityEngine.Color color = new UnityEngine.Color(0.25f, 0.6f, 1f, 1f);
    }

    /// <summary>The string names an upgrade in content/upgrades.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class UpgradeRefAttribute : Attribute { }

    [Serializable]
    [Doc("Something an entity drops when killed.")]
    public class DropDef
    {
        [Required, EntityRef] public string entity = "";
        [Doc("Chance per roll, 0-1.")] public float chance = 0.25f;
        public int count = 1;
    }

    [Serializable]
    [Doc("A labeled tick on the level progress bar.")]
    public class ProgressMarkerDef
    {
        public float seconds;
        public string label = "";
    }
}
