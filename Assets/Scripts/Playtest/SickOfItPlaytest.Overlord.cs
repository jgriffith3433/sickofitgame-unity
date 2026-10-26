using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The Overlord's eight glimpses (Docs/Story.md "The Overlord on the far ledge"): each floor opened directly (setup shortcut) and played by the bot
        /// through real input until its scene's `ovl:&lt;floor&gt;:done` signal. Floor 896's scene is rewound with a real hold of R. Tools/Levels/overlord_cameos.py writes them.
        /// </summary>
        public const string OverlordCameosId = "overlord-cameos";
        const float OverlordCameosSeconds = 1500f;

        // floor, the signal that proves its own interaction
        static readonly (int floor, string kind, string proof)[] OverlordScenes =
        {
            (997, "a taunt", "taunt"), (994, "a dropped ember", "drop"), (958, "a flight on a lift", "lift"), (953, "a laser at the floor", "laser"),
            (906, "a stolen diamond", "stolen"), (896, "rewound by the hero", "rewound"), (857, "a summons", "summon"), (806, "a dropped diamond", "drop"),
        };

        sealed partial class Session
        {
            readonly HashSet<string> overlordSignals = new HashSet<string>();
            readonly List<string> overlordOrder = new List<string>();
            float overlordWalkAt = -1f;
            int overlordRewindStage;
            float overlordRewindEnd, overlordClockBefore, overlordClockAfter;

            void OverlordTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind != "signal" || !e.Subject.StartsWith("ovl:"))
                    return;
                if (overlordSignals.Add(e.Subject))
                    overlordOrder.Add(e.Subject);
                if (e.Subject == "ovl:896:walk")
                    overlordWalkAt = e.Time;
                Run.Event("overlord", $"{e.Subject} at {e.Time:0.00}");
            }

            void OverlordCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"overlord-cameos {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"overlord-cameos {beat}: {detail}");
            }

            // Floor 896: once he is walking, hold R for a second (setup shortcut: the juice topped up), as the hero's line says.
            bool OverlordUntil(int floor)
            {
                if (overlordSignals.Contains($"ovl:{floor}:done"))
                    return true;
                var clock = GameClock.Instance;
                if (floor != 896 || clock == null || overlordWalkAt < 0f)
                    return false;
                if (overlordRewindStage == 0 && clock.Time > overlordWalkAt + 2.5f)
                {
                    overlordRewindStage = 1;
                    RewindBudget.Instance?.Add(30f);
                    overlordClockBefore = clock.Time;
                    overlordRewindEnd = Time.realtimeSinceStartup + 1.2f;
                    Run.Event("rewind", $"floor 896: hold R while the Overlord walks his ledge (clock {clock.Time:0.0})");
                    input.SetKey(Key.R, true);
                }
                else if (overlordRewindStage == 1 && Time.realtimeSinceStartup >= overlordRewindEnd)
                {
                    overlordRewindStage = 2;
                    input.SetKey(Key.R, false);
                    overlordClockAfter = clock.Time;
                }
                return false;
            }

            public IEnumerator OverlordCameosRun()
            {
                GameplayTelemetry.Recorded += OverlordTelemetry;
                try
                {
                    for (int i = 0; i < OverlordScenes.Length && !Run.Done; i++)
                    {
                        // A floor with no progress for minutes ended as "stalled" (its stuck event names the place): the run ends here, stuck.
                        if (lastOutcome == "stalled")
                            break;
                        var (floor, kind, proof) = OverlordScenes[i];
                        int n = LevelNumberOf("Floor" + floor);
                        if (n == 0)
                        {
                            OverlordCheck($"floor {floor}", false, "no such level in game.json");
                            continue;
                        }
                        string done = $"ovl:{floor}:done";
                        for (int attempt = 1; attempt <= 2 && !overlordSignals.Contains(done) && !Run.Done; attempt++)
                        {
                            overlordWalkAt = -1f;
                            overlordRewindStage = 0;
                            yield return OpenLevel(n, attempt);
                            if (Run.Done || !openedLevel)
                                break;
                            yield return PlayLevel(n, 240f, null, (float)i / OverlordScenes.Length, (float)(i + 1) / OverlordScenes.Length, 300f, waitOutDeath: true,
                                until: () => OverlordUntil(floor));
                            input.SetKey(Key.R, false);
                            // Only a death earns a second try: a floor that stalled or ran out of time is reported (the stuck event names the place) and the next one starts.
                            if (lastOutcome != "died")
                                break;
                        }
                        bool all = overlordSignals.Contains($"ovl:{floor}:enter") && overlordSignals.Contains($"ovl:{floor}:line")
                                   && overlordSignals.Contains($"ovl:{floor}:{proof}") && overlordSignals.Contains(done);
                        OverlordCheck($"floor {floor} ({kind})", all,
                            all ? "he came, spoke, did it, the hero answered and he left" : $"signals seen: {string.Join(", ", overlordOrder.FindAll(s => s.StartsWith($"ovl:{floor}:")))}");
                    }
                    if (overlordRewindStage == 2)
                        OverlordCheck("rewind during the scene", overlordClockAfter < overlordClockBefore - 0.5f && overlordSignals.Contains("ovl:896:rewound"),
                            $"clock {overlordClockBefore:0.0} -> {overlordClockAfter:0.0}, his \"Again\" {(overlordSignals.Contains("ovl:896:rewound") ? "heard" : "not heard")}");
                    else
                        OverlordCheck("rewind during the scene", false, "floor 896's scene never got to the rewind");
                    int floors = 0;
                    foreach (var s in OverlordScenes)
                        if (overlordSignals.Contains($"ovl:{s.floor}:done"))
                            floors++;
                    Run.Metric("overlord.floors", floors);
                    OverlordCheck("eight appearances on distinct floors", floors >= 8, $"{floors} of {OverlordScenes.Length} scenes played to the end");
                    if (lastOutcome == "stalled" && !Run.Done)
                    {
                        Run.Progress(1f);
                        End(PlaytestOutcome.Stuck);
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= OverlordTelemetry;
                    input.SetKey(Key.R, false);
                }
            }
        }
    }
}
