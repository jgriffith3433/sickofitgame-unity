using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The Climbers (Docs/Story.md "The Climbers"): Rook (the rival), Wick (Circle X's guide) and Pointer (Circle IX's guide), fourteen scenes on fourteen floors.
        /// Every floor is opened directly twice (a setup shortcut) and played by the bot through real input until the scene's `cmp:&lt;who&gt;:&lt;floor&gt;:done` signal:
        /// once with a plain save and once with the save that the scene's other branch asks for (hearts, diamonds, deaths or the flags earlier scenes write, set as
        /// setup shortcuts). The two runs must take different branches. Tools/Levels/companions.py writes the scenes.
        /// </summary>
        public const string CompanionsId = "companions";
        const float CompanionsSeconds = 1800f;

        // floor, who, the default branch, the other branch, the save that makes it ($=diamonds d=deaths h=hearts left f=flags, comma separated)
        static readonly (int floor, string who, string plain, string other, string setup)[] CompanionScenes =
        {
            (996, "rook", "poor", "rich", "$=12"), (992, "rook", "race", "hurt", "h=1"), (955, "rook", "plain", "dead", "d=3"),
            (899, "rook", "skipped", "asked", "f=rook.asked"), (858, "rook", "plain", "rich", "$=35"), (808, "rook", "plain", "kind", "f=rook.second"),
            (999, "wick", "plain", "hurt", "h=1"), (993, "wick", "plain", "dead", "d=3"), (960, "wick", "plain", "rich", "$=25"),
            (902, "wick", "plain", "friends", "f=wick.lit,rook.asked"),
            (900, "pointer", "plain", "hurt", "h=1"), (895, "pointer", "plain", "rook", "f=rook.sideways"), (860, "pointer", "plain", "wick", "f=wick.rested"),
            (810, "pointer", "plain", "both", "f=pointer.right,rook.second"),
        };

        sealed partial class Session
        {
            readonly HashSet<string> companionSignals = new HashSet<string>();
            readonly List<string> companionOrder = new List<string>();
            int companionHurtFloor;

            void CompanionTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind != "signal" || !e.Subject.StartsWith("cmp:"))
                    return;
                if (companionSignals.Add(e.Subject))
                    companionOrder.Add(e.Subject);
                Run.Event("companion", $"{e.Subject} at {e.Time:0.00}");
            }

            void CompanionCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"companions {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"companions {beat}: {detail}");
            }

            // Setup shortcuts: the save the branch asks for. "" = a plain save.
            static void CompanionSetUp(string setup)
            {
                UnlockFlags.Reset();
                UpgradeProgress.Reset();
                YoobaProgress.Deaths = 0;
                RogueliteManager.Instance?.SetPoints(0);
                foreach (string part in setup.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string key = part.Substring(0, part.IndexOf('=')), value = part.Substring(part.IndexOf('=') + 1);
                    if (key == "$") RogueliteManager.Instance?.SetPoints(int.Parse(value));
                    else if (key == "d") YoobaProgress.Deaths = int.Parse(value);
                    else if (key == "f") foreach (string f in value.Split(',')) UnlockFlags.Set(f);
                }
            }

            // Hearts are the hero's own: once the climber has come, hurt him down to the one heart (the scene reads it after the intro lines).
            bool CompanionUntil(int floor, string who, bool hurt)
            {
                var world = GameplayWorld.Instance;
                if (hurt && companionHurtFloor != floor && companionSignals.Contains($"cmp:{who}:{floor}:enter") && world != null && world.Player != null && world.Player.Health > 1)
                {
                    companionHurtFloor = floor;
                    world.Player.DamageAs(world.Player.Health - 1, "playtest", true);
                    Run.Event("companion", $"setup shortcut: floor {floor}, hero hurt down to 1 heart");
                }
                return companionSignals.Contains($"cmp:{who}:{floor}:done");
            }

            IEnumerator CompanionPlay(int index, int floor, string who, string setup, bool hurt, int total)
            {
                string done = $"cmp:{who}:{floor}:done";
                int n = LevelNumberOf("Floor" + floor);
                if (n == 0)
                    yield break;
                for (int attempt = 1; attempt <= 2 && !companionSignals.Contains(done) && !Run.Done; attempt++)
                {
                    CompanionSetUp(setup);
                    companionHurtFloor = 0;
                    yield return OpenLevel(n, attempt);
                    if (Run.Done || !openedLevel)
                        break;
                    yield return PlayLevel(n, 120f, null, (float)index / total, (float)(index + 1) / total, 240f, waitOutDeath: true, until: () => CompanionUntil(floor, who, hurt));
                }
            }

            public IEnumerator CompanionsRun()
            {
                GameplayTelemetry.Recorded += CompanionTelemetry;
                int total = CompanionScenes.Length * 2;
                try
                {
                    var encounters = new Dictionary<string, int>();
                    var branchesSeen = new Dictionary<string, HashSet<string>>();
                    for (int i = 0; i < CompanionScenes.Length && !Run.Done; i++)
                    {
                        var (floor, who, plain, other, setup) = CompanionScenes[i];
                        string at = $"cmp:{who}:{floor}:";
                        // 1. a plain save: the default branch
                        yield return CompanionPlay(i * 2, floor, who, "", false, total);
                        bool plainOk = companionSignals.Contains(at + "branch:" + plain) && companionSignals.Contains(at + "done");
                        bool hadDone = companionSignals.Contains(at + "done");
                        // 2. the other save: the other branch (the same scene again, so forget the first run's signals)
                        foreach (string s in new List<string>(companionSignals))
                            if (s.StartsWith(at))
                                companionSignals.Remove(s);
                        yield return CompanionPlay(i * 2 + 1, floor, who, setup, setup.StartsWith("h="), total);
                        bool otherOk = companionSignals.Contains(at + "branch:" + other) && !companionSignals.Contains(at + "branch:" + plain) && companionSignals.Contains(at + "done");
                        CompanionCheck($"floor {floor} ({who}) plain save", plainOk, plainOk ? $"took \"{plain}\"" : "the default branch did not play to the end");
                        CompanionCheck($"floor {floor} ({who}) reacts to the hero ({setup})", otherOk,
                            otherOk ? $"took \"{other}\" instead of \"{plain}\"" : $"signals seen: {string.Join(", ", companionOrder.FindAll(s => s.StartsWith(at)))}");
                        if (hadDone || otherOk)
                        {
                            encounters[who] = (encounters.TryGetValue(who, out int c) ? c : 0) + 1;
                            if (!branchesSeen.TryGetValue(who, out var set)) branchesSeen[who] = set = new HashSet<string>();
                            set.Add($"{floor}:{plain}");
                            if (otherOk) set.Add($"{floor}:{other}");
                        }
                    }
                    foreach (string who in new[] { "rook", "wick", "pointer" })
                    {
                        int count = encounters.TryGetValue(who, out int c) ? c : 0;
                        int branches = branchesSeen.TryGetValue(who, out var set) ? set.Count : 0;
                        Run.Metric($"companions.{who}.encounters", count);
                        Run.Metric($"companions.{who}.branches", branches);
                        CompanionCheck($"{who} appears in 3+ encounters", count >= 3, $"{count} scenes played");
                        CompanionCheck($"{who} has branches that react to the hero", branches >= 6, $"{branches} different branches played");
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= CompanionTelemetry;
                    UnlockFlags.Reset();
                    YoobaProgress.Deaths = 0;
                }
            }
        }
    }
}
