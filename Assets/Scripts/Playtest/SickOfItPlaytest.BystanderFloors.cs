using System.Collections;
using System.Collections.Generic;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>Bystanders on the floors (Docs/Story.md "Bystanders on the floors"): every floor whose first phase spawns a "by_" bystander, played by the bot.</summary>
        public const string BystanderFloorsId = "bystander-floors";

        /// <summary>The levels whose first phase spawns an entity with an id starting "by_" (Tools/Levels/bystanders.py writes them).</summary>
        public static List<int> BystanderFloorLevels()
        {
            var list = new List<int>();
            var definition = GameDefinition.Instance;
            var content = GameContent.Current;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels && content != null; n++)
            {
                var config = definition.GetLevel(n);
                if (config == null || config.levelName == null || !content.LevelScripts.TryGetValue(config.levelName, out var script)
                    || script == null || script.phases == null || script.phases.Count == 0)
                    continue;
                foreach (var action in script.phases[0].onEnter)
                    if (action is SpawnAction spawn && spawn.id != null && spawn.id.StartsWith("by_"))
                    {
                        list.Add(n);
                        break;
                    }
            }
            return list;
        }

        sealed partial class Session
        {
            readonly HashSet<string> bystanderFloorsReacted = new HashSet<string>();
            readonly HashSet<string> bystanderFloorsKinds = new HashSet<string>();
            readonly HashSet<string> bystanderFloorsHeroActs = new HashSet<string>();
            string bystanderFloorsCurrent = "";

            void BystanderFloorsTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "levelStart")
                    bystanderFloorsCurrent = e.Subject;
                else if (e.Kind == "signal" && e.Subject.StartsWith("by:"))
                {
                    bystanderFloorsReacted.Add(bystanderFloorsCurrent);
                    bystanderFloorsKinds.Add(e.Subject);
                    // The hand-overs and the stray shot: the scripted interactions with the hero.
                    if (e.Subject == "by:giver:hand" || e.Subject == "by:hawker:sample" || e.Subject == "by:heckler:stray")
                        bystanderFloorsHeroActs.Add(e.Subject);
                    Run.Event("bystander-floor", $"{e.Subject} on {bystanderFloorsCurrent} at {e.Time:0.00}");
                }
            }

            public IEnumerator BystanderFloorsRun()
            {
                var levels = BystanderFloorLevels();
                Run.Event("bystander-floor", $"{levels.Count} floors have bystanders: {PathRange(levels)}");
                GameplayTelemetry.Recorded += BystanderFloorsTelemetry;
                try
                {
                    yield return AllLevels(levels, "the floors with bystanders", checkEvery: true);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= BystanderFloorsTelemetry;
                }
                Run.Metric("bystanderFloors.floors", levels.Count);
                Run.Metric("bystanderFloors.reacted", bystanderFloorsReacted.Count);
                Run.Metric("bystanderFloors.kinds", bystanderFloorsKinds.Count);
                Run.Metric("bystanderFloors.heroInteractions", bystanderFloorsHeroActs.Count);
                Run.Event("check", $"bystander-floors: {levels.Count} floors have bystanders; reactions seen on {bystanderFloorsReacted.Count} of them ({string.Join(", ", bystanderFloorsKinds)}); " +
                    $"scripted hero interactions seen: {bystanderFloorsHeroActs.Count} of 3 ({string.Join(", ", bystanderFloorsHeroActs)})");
            }
        }
    }
}
