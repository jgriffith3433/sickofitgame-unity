using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The elites scenario (Docs/Story.md "Elites and mid-bosses"). Data first: twelve elite variants (a name, a hint, a hero voice line with
        /// a clip, the elite tag) and four mid-bosses (phases, a boss tag). Then the arena content/scenes/eliteArena.json on Level 1's stage
        /// (setup shortcut), one station each, played by the bot through real input: every elite and every mid-boss appears, and the
        /// mid-bosses' wave and rage phases are seen. Then the four floors with a mid-boss (955, 905, 895, 855) are played from the start of
        /// the floor with the hero's hints reset: the floor's elites appear (a "hint" event is the hero introducing each), the mid-boss's
        /// intro, fight and defeat signals come in order. One check event each.
        /// </summary>
        public const string ElitesId = "elites";
        public const string EliteScene = "eliteArena";
        const float ElitesSeconds = 2500f;

        static readonly string[] EliteIds =
        {
            "quickfireImp", "coronaImp", "ironhideBulwark", "alphaHound", "twinBoltDeadeye", "twinLensImp",
            "phaseRaider", "broodWhelp", "furnacePlate", "tripleLobber", "twiceRisen", "veilMatron",
        };

        static readonly string[] MidBossIds = { "baronSpillway", "kilnMatron", "motherFissure", "shardwright" };
        static readonly string[] MidBossPre = { "bs", "km", "mf", "sw" };
        static readonly int[] MidBossLevels = { 17, 27, 37, 47 };
        static readonly string[][] MidBossFloorElites =
        {
            new[] { "ironhideBulwark", "alphaHound" },
            new[] { "twinBoltDeadeye", "twinLensImp", "phaseRaider" },
            new[] { "broodWhelp", "furnacePlate" },
            new[] { "tripleLobber", "twiceRisen" },
        };

        sealed partial class Session
        {
            sealed class EliteWatch
            {
                public readonly HashSet<string> Passed = new HashSet<string>();
                public readonly HashSet<string> Spawned = new HashSet<string>();
                public readonly HashSet<string> Signals = new HashSet<string>();
                public readonly HashSet<string> Hints = new HashSet<string>();
                public readonly HashSet<string> Died = new HashSet<string>();
                public float DefeatedAt = -1f;
                public string Pre = "";
                public bool Done;
                public int Failed;
            }

            EliteWatch elites;

            void EliteCheck(string beat, bool ok, string detail)
            {
                if (elites == null || elites.Passed.Contains(beat))
                    return;
                if (ok)
                {
                    elites.Passed.Add(beat);
                    Run.Event("check", $"elites {beat}: ok ({detail})");
                }
                else
                {
                    elites.Failed++;
                    Run.Event("check", $"elites {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"elites {beat}: {detail}");
                }
            }

            public IEnumerator ElitesRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(EliteScene))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"elites: the scene \"{EliteScene}\" is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                elites = new EliteWatch();

                // 1. The data.
                foreach (var id in EliteIds)
                {
                    if (!content.Entities.TryGetValue(id, out var def))
                    {
                        EliteCheck(id + " data", false, "there is no such entity");
                        continue;
                    }
                    bool voiced = !string.IsNullOrEmpty(def.hintVoice) && SickOfIt.Core.Audio.Sfx.Has(def.hintVoice);
                    bool tagged = def.tags != null && def.tags.Contains("elite");
                    EliteCheck(id + " data", !string.IsNullOrEmpty(def.displayName) && !string.IsNullOrEmpty(def.hint) && voiced && tagged,
                        $"\"{def.displayName}\": \"{def.hint}\" (hero voice {def.hintVoice}: {(voiced ? "has a clip" : "NO CLIP")}), elite tag: {(tagged ? "yes" : "NO")}");
                }
                for (int i = 0; i < MidBossIds.Length; i++)
                {
                    if (!content.Entities.TryGetValue(MidBossIds[i], out var def))
                    {
                        EliteCheck(MidBossIds[i] + " data", false, "there is no such entity");
                        continue;
                    }
                    bool tagged = def.tags != null && def.tags.Contains("midBoss");
                    EliteCheck(MidBossIds[i] + " data", !string.IsNullOrEmpty(def.displayName) && tagged && def.states != null && def.states.Count >= 10,
                        $"\"{def.displayName}\": {(def.states != null ? def.states.Count : 0)} states (needs 10+), midBoss tag: {(tagged ? "yes" : "NO")}");
                }

                // 2. The arena, played by the bot.
                Run.Event("shortcut", $"load level 1's stage with the arena {EliteScene} directly");
                LevelRuntime.PendingScene = EliteScene;
                GameplayTelemetry.Recorded += EliteTelemetry;
                try
                {
                    var load = LoadLevel(1);
                    if (load != null)
                        yield return load;
                    yield return PlayLevel(1, 780f, null, waitOutDeath: true, until: () => elites.Done);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= EliteTelemetry;
                    LevelRuntime.PendingScene = null;
                }
                if (Run.Done)
                {
                    elites = null;
                    yield break;
                }
                foreach (var id in EliteIds)
                    EliteCheck(id + " on the arena", elites.Spawned.Contains(id), elites.Spawned.Contains(id) ? "it appeared" : "it never appeared on the arena");
                for (int i = 0; i < MidBossIds.Length; i++)
                {
                    string id = MidBossIds[i], pre = MidBossPre[i];
                    EliteCheck(id + " on the arena", elites.Spawned.Contains(id), elites.Spawned.Contains(id) ? "it appeared" : "it never appeared on the arena");
                    EliteCheck(id + " arena phases", elites.Signals.Contains(pre + "Wave") && elites.Signals.Contains(pre + "Rage"),
                        $"wave phase: {(elites.Signals.Contains(pre + "Wave") ? "seen" : "NOT seen")}, rage phase: {(elites.Signals.Contains(pre + "Rage") ? "seen" : "NOT seen")}");
                }

                // 3. The four floors, from their start, the hero's hints forgotten.
                for (int i = 0; i < MidBossIds.Length && !Run.Done; i++)
                {
                    int level = MidBossLevels[i];
                    string pre = MidBossPre[i];
                    elites.Spawned.Clear();
                    elites.Signals.Clear();
                    elites.Hints.Clear();
                    elites.DefeatedAt = -1f;
                    elites.Pre = pre;
                    SickOfIt.Data.EnemyHints.Reset();
                    Prepare(level, true);
                    GameplayTelemetry.Recorded += EliteTelemetry;
                    try
                    {
                        var load = LoadLevel(level);
                        if (load != null)
                            yield return load;
                        yield return PlayLevel(level, 420f, null, waitOutDeath: true,
                            until: () => elites.DefeatedAt >= 0f && GameClock.Instance != null && GameClock.Instance.Time - elites.DefeatedAt > 30f);
                    }
                    finally
                    {
                        GameplayTelemetry.Recorded -= EliteTelemetry;
                    }
                    if (Run.Done)
                        break;
                    string floorName = $"level {level} ({MidBossIds[i]})";
                    foreach (var id in MidBossFloorElites[i])
                        EliteCheck($"{floorName} elite {id}", elites.Spawned.Contains(id),
                            elites.Spawned.Contains(id) ? $"it came onto the floor" : "it never appeared on the floor");
                    EliteCheck($"{floorName} hero introduces", elites.Hints.Count > 0, elites.Hints.Count > 0 ? "hints: " + string.Join(", ", elites.Hints) : "the hero never sized an elite up");
                    EliteCheck($"{floorName} mid-boss", elites.Signals.Contains(pre + "Intro") && elites.Signals.Contains(pre + "Fight"),
                        $"intro: {(elites.Signals.Contains(pre + "Intro") ? "seen" : "NOT seen")}, fight: {(elites.Signals.Contains(pre + "Fight") ? "seen" : "NOT seen")}");
                    EliteCheck($"{floorName} phases", elites.Signals.Contains(pre + "Wave") && elites.Signals.Contains(pre + "Rage") && elites.Signals.Contains(pre + "Defeat"),
                        $"wave: {(elites.Signals.Contains(pre + "Wave") ? "seen" : "NOT seen")}, rage: {(elites.Signals.Contains(pre + "Rage") ? "seen" : "NOT seen")}, defeat: {(elites.Signals.Contains(pre + "Defeat") ? "seen" : "NOT seen")}");
                }

                Run.Metric("elites.passed", elites.Passed.Count);
                Run.Metric("elites.failed", elites.Failed);
                Run.Progress(1f);
                bool allOk = elites.Failed == 0;
                elites = null;
                End(allOk ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            void EliteTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (elites == null)
                    return;
                string at = $"at {e.Time:0.0} s";
                switch (e.Kind)
                {
                    case "spawn":
                        if (System.Array.IndexOf(EliteIds, e.Subject) >= 0 || System.Array.IndexOf(MidBossIds, e.Subject) >= 0)
                        {
                            if (elites.Spawned.Add(e.Subject))
                                Run.Event("elite", $"{at}: {e.Subject} is on the field");
                        }
                        break;
                    case "death":
                        if ((System.Array.IndexOf(EliteIds, e.Subject) >= 0 || System.Array.IndexOf(MidBossIds, e.Subject) >= 0) && elites.Died.Add(e.Subject))
                            Run.Event("elite", $"{at}: {e.Subject} died");
                        break;
                    case "hint":
                        if (elites.Hints.Add(e.Subject))
                            Run.Event("hint", $"{e.Subject} {at}: \"{e.Detail}\" (the hero introduces it)");
                        break;
                    case "signal":
                        if (e.Subject.StartsWith("elite:", System.StringComparison.Ordinal))
                        {
                            Run.Event("elite", $"{at}: station {e.Subject.Substring(6)}");
                            if (e.Subject == "elite:done")
                                elites.Done = true;
                        }
                        else if (e.Subject.Length > 2 && System.Array.IndexOf(MidBossPre, e.Subject.Substring(0, 2)) >= 0
                                 && (e.Subject.EndsWith("Intro") || e.Subject.EndsWith("Fight") || e.Subject.EndsWith("Wave") || e.Subject.EndsWith("Rage") || e.Subject.EndsWith("Defeat")))
                        {
                            if (elites.Signals.Add(e.Subject))
                                Run.Event("midboss", $"{at}: {e.Subject}");
                            if (e.Subject.EndsWith("Defeat") && elites.DefeatedAt < 0f)
                                elites.DefeatedAt = e.Time;
                        }
                        break;
                }
            }
        }
    }
}
