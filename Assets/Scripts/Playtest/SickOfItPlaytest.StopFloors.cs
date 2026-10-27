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
        /// Stop floors (Docs/Story.md "Stop floors"): 957 The Hot Bath, 959 Hell's Graffiti, 897 Rook's Camp, 893 The Stuck Imp. Each is opened directly (a setup shortcut)
        /// and played by the bot through real input until the floor's `stop:&lt;key&gt;:done` signal. Checked: the discovery signal, no damage to the hero between the
        /// floor's `enter` and `done`, the quiet lasting 20+ s (`calm` 20 s after `enter`), the lava never rising, and per floor its own beat (the bath's heal, six carvings
        /// read and the diamond under the last, Rook's question answered, the stuck imp's same throw before and after a rewind). Tools/Levels/stop_floors.py writes the floors.
        /// </summary>
        public const string StopFloorsId = "stop-floors";
        const float StopFloorsSeconds = 480f;

        static readonly (int floor, string key)[] StopFloorList = { (957, "hotBath"), (959, "graffiti"), (897, "camp"), (893, "stuckImp") };

        sealed partial class Session
        {
            readonly Dictionary<string, float> stopSignals = new Dictionary<string, float>();
            readonly List<float> stopDamage = new List<float>();
            readonly List<float> stopThrows = new List<float>();
            float stopLavaMax;
            bool stopRewound;

            void StopTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "signal" && e.Subject.StartsWith("stop:"))
                {
                    if (!stopSignals.ContainsKey(e.Subject))
                        stopSignals[e.Subject] = e.Time;
                    Run.Event("stop-floor", $"{e.Subject} at {e.Time:0.00}");
                }
                else if (e.Kind == "damage" && e.Subject == "player")
                    stopDamage.Add(e.Time);
                else if (e.Kind == "spawn" && e.Subject == "stuckEmber")
                {
                    stopThrows.Add(e.Time);
                    Run.Event("stop-floor", $"the stuck imp throws (#{stopThrows.Count}) at {e.Time:0.00}");
                }
            }

            void StopCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"stop-floors {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"stop-floors {beat}: {detail}");
            }

            bool StopSeen(string name) => stopSignals.ContainsKey(name);

            IEnumerator StopFloorsPlay(int index, int floor, string key)
            {
                int n = LevelNumberOf("Floor" + floor);
                if (n == 0)
                {
                    StopCheck($"floor {floor} exists", false, "no such level in game.json");
                    yield break;
                }
                string done = $"stop:{key}:done";
                for (int attempt = 1; attempt <= 2 && !StopSeen(done) && !Run.Done; attempt++)
                {
                    stopSignals.Clear();
                    stopDamage.Clear();
                    stopThrows.Clear();
                    stopRewound = false;
                    stopLavaMax = 0f;
                    UnlockFlags.Reset();
                    yield return OpenLevel(n, attempt);
                    if (Run.Done || !openedLevel)
                        break;
                    int total = StopFloorList.Length;
                    yield return PlayLevel(n, 100f, now =>
                    {
                        var w = GameplayWorld.Instance;
                        if (w != null)
                            stopLavaMax = Mathf.Max(stopLavaMax, w.GetCounter("lavaSpeed"));
                        // The stuck imp: after its third throw, hold R back to before the second and watch it throw the same throw again.
                        if (key == "stuckImp" && !stopRewound && stopThrows.Count >= 3 && now >= stopThrows[2] + 0.6f)
                        {
                            stopRewound = true;
                            Run.Event("stop-floor", $"rewind: hold R back past throw 2 (at {now:0.0} s)");
                            return HoldRewind(3.0f);
                        }
                        return null;
                    }, (float)index / total, (float)(index + 1) / total, 240f, waitOutDeath: true,
                       until: () => StopSeen(done) && (key != "stuckImp" || stopRewound));
                }
            }

            public IEnumerator StopFloorsRun()
            {
                GameplayTelemetry.Recorded += StopTelemetry;
                int cleared = 0;
                try
                {
                    for (int i = 0; i < StopFloorList.Length && !Run.Done; i++)
                    {
                        var (floor, key) = StopFloorList[i];
                        yield return StopFloorsPlay(i, floor, key);
                        if (Run.Done) break;
                        string p = $"stop:{key}:";
                        string label = $"floor {floor} ({key})";
                        bool done = StopSeen(p + "done");
                        StopCheck($"{label} is played to its landing", done, done ? $"outcome {lastOutcome}" : $"outcome {lastOutcome}, signals: {string.Join(", ", stopSignals.Keys)}");
                        StopCheck($"{label} has a discovery", StopSeen(p + "discovery"), StopSeen(p + "discovery") ? $"at {stopSignals[p + "discovery"]:0.0} s" : "no discovery signal");
                        float enter = StopSeen(p + "enter") ? stopSignals[p + "enter"] : -1f;
                        float end = StopSeen(p + "done") ? stopSignals[p + "done"] : float.MaxValue;
                        int hits = 0;
                        foreach (float t in stopDamage)
                            if (t >= enter && t <= end) hits++;
                        StopCheck($"{label} is quiet (no damage to the hero)", enter >= 0f && hits == 0, $"{hits} damage events between enter and the landing");
                        StopCheck($"{label} holds the hero for 20 s", StopSeen(p + "calm") && enter >= 0f && stopSignals[p + "calm"] - enter >= 19.5f,
                            StopSeen(p + "calm") ? $"calm {stopSignals[p + "calm"] - enter:0.0} s after enter" : "the quiet never ended");
                        StopCheck($"{label} has no rising lava", stopLavaMax <= 0.001f, $"lavaSpeed peaked at {stopLavaMax:0.00}");
                        switch (key)
                        {
                            case "hotBath":
                                StopCheck($"{label} heals at the bath", StopSeen(p + "healed"), StopSeen(p + "healed") ? "the soul's lines played, then a heart" : "never stood in the spring");
                                break;
                            case "graffiti":
                                int read = 0;
                                for (int c = 1; c <= 6; c++) if (StopSeen(p + "read" + c)) read++;
                                StopCheck($"{label} reads every carving", read == 6, $"{read} of 6 carvings read");
                                StopCheck($"{label} pays the last carving's diamond", StopSeen(p + "diamond"), "diamond under 'ROOK WAS HERE. SECOND.'");
                                break;
                            case "camp":
                                bool sat = StopSeen(p + "reply:sit"), went = StopSeen(p + "reply:walkOn");
                                StopCheck($"{label} Rook asks, the hero answers once", StopSeen(p + "asked") && (sat ^ went), $"asked {StopSeen(p + "asked")}, sat {sat}, walked on {went}");
                                StopCheck($"{label} answer is paid", StopSeen(p + "done:sit") || StopSeen(p + "done:walkOn"), sat ? "sat: a heart" : went ? "walked on: two diamonds" : "no reply");
                                break;
                            case "stuckImp":
                                StopCheck($"{label} throws", stopThrows.Count >= 3, $"{stopThrows.Count} throws");
                                StopCheck($"{label} was rewound", stopRewound, stopRewound ? "R held 3 s" : "no rewind");
                                // After the rewind the clock is back before throw 2: the same throw comes again at the same game time.
                                int replays = 0;
                                for (int a = 1; a < stopThrows.Count; a++)
                                    for (int b = 0; b < a; b++)
                                        if (Mathf.Abs(stopThrows[a] - stopThrows[b]) < 0.15f) { replays++; break; }
                                StopCheck($"{label} repeats the same throw on every rewind", stopRewound && replays >= 1 && StopSeen(p + "rewound"),
                                    $"{replays} throws came again at the same game time ({string.Join(", ", stopThrows.ConvertAll(t => t.ToString("0.0")))}); the hero's line {(StopSeen(p + "rewound") ? "played" : "did not play")}");
                                break;
                        }
                        if (done) cleared++;
                    }
                    Run.Metric("stopFloors.cleared", cleared);
                    StopCheck("all four stop floors are played", cleared == StopFloorList.Length, $"{cleared} of {StopFloorList.Length}");
                }
                finally
                {
                    GameplayTelemetry.Recorded -= StopTelemetry;
                    UnlockFlags.Reset();
                }
            }
        }
    }
}
