using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The daily run's frame-time budgets, in milliseconds (a headless browser on the T4, so generous; a real regression is far over).
        /// Playing: the average frame of a level, and the share of frames over <see cref="FrameSpikeMs"/>. Rewinding: see rewind-perf.
        /// </summary>
        public const double FramePlayAverageMs = 40.0, FrameRewindAverageMs = 50.0, FrameSpikeMs = 100.0, FrameSpikeShare = 0.02;

        sealed partial class Session
        {
            /// <summary>What one floor cost the bot, summed over its tries (all-levels and its parts): the balance report's row.</summary>
            sealed class BalanceRow
            {
                public int Level, Tries, Deaths, Rewinds, Hits, Grazes, Frames, Spikes;
                public float ClearSeconds = -1f, LastSeconds, RewoundSeconds;
                public int Diamonds;
                public double FrameSumMs, FrameWorstMs;
                public bool Cleared;
                public double FrameAverageMs => Frames > 0 ? FrameSumMs / Frames : 0;
            }

            readonly Dictionary<int, BalanceRow> balance = new Dictionary<int, BalanceRow>();
            int balanceLevel;
            float balanceSkipUntil;

            BalanceRow BalanceOf(int level)
            {
                if (!balance.TryGetValue(level, out var row))
                    balance[level] = row = new BalanceRow { Level = level };
                return row;
            }

            /// <summary>PlayLevel starts level n: frame times count from 2 s of game time (the load's frames aren't play).</summary>
            void BalanceBegin(int n)
            {
                balanceLevel = n;
                balanceSkipUntil = 2f;
            }

            /// <summary>Once per PlayLevel frame: the frame time of this floor (game seconds <paramref name="now"/> since it began).</summary>
            void BalanceFrame(float now)
            {
                if (balanceLevel <= 0 || now < balanceSkipUntil)
                    return;
                double ms = Time.unscaledDeltaTime * 1000.0;
                var row = BalanceOf(balanceLevel);
                row.Frames++;
                row.FrameSumMs += ms;
                if (ms > row.FrameWorstMs) row.FrameWorstMs = ms;
                if (ms > FrameSpikeMs) row.Spikes++;
            }

            /// <summary>A try's telemetry summary (GameplayTelemetry.Finished): clear time, deaths, diamonds, rewind use, hits taken.</summary>
            void BalanceOnLevelEnd(GameplayTelemetry.Summary s, int diamonds)
            {
                var row = BalanceOf(s.levelNumber);
                row.Tries++;
                row.LastSeconds = s.duration;
                row.Diamonds = diamonds;
                row.Rewinds += s.rewinds;
                row.RewoundSeconds += s.rewoundSeconds;
                row.Hits += s.playerHits;
                if (s.outcome == "died") row.Deaths++;
                if (s.outcome == "completed") { row.Cleared = true; row.ClearSeconds = s.duration; }
                if (balanceLevel == s.levelNumber) balanceLevel = 0;
            }

            /// <summary>A graze (a shot passing close) from the telemetry; the graze system lands later, its events count here by themselves.</summary>
            void BalanceOnTelemetry(GameplayTelemetry.Event e, int level)
            {
                if (e.Kind == "graze")
                    BalanceOf(level).Grazes++;
            }

            /// <summary>
            /// The per-floor balance report: one event, metric set and markdown row per floor played (clear time, deaths, diamonds,
            /// rewind use, grazes, hits taken, frame time), and the frame-time budget as a check per floor. Called at the end of every
            /// all-levels run, so each part of the daily run adds its floors.
            /// </summary>
            void BalanceReport(string what, List<int> levels)
            {
                Run.Log($"balance report: {what}");
                Run.Log("| Level | Floor | Clear time | Deaths | Diamonds | Rewinds (s back) | Grazes | Hits | Frame avg / worst | Spikes |");
                Run.Log("|---|---|---|---|---|---|---|---|---|---|");
                int rows = 0, overBudget = 0;
                double totalClear = 0, totalDiamonds = 0;
                foreach (int n in levels)
                {
                    if (!balance.TryGetValue(n, out var r) || r.Tries == 0)
                        continue;
                    rows++;
                    int floor = PathFloor(n);
                    string clear = r.Cleared ? $"{r.ClearSeconds:0} s" : $"not cleared ({r.LastSeconds:0} s)";
                    string line = $"level {n}{(floor > 0 ? $" (Floor {floor})" : "")}: {clear}, {r.Deaths} death(s) in {r.Tries} {(r.Tries == 1 ? "try" : "tries")}, " +
                                  $"{r.Diamonds} diamonds, {r.Rewinds} rewind(s) ({r.RewoundSeconds:0.0} s back), {r.Grazes} graze(s), {r.Hits} hit(s) taken, " +
                                  $"frame {r.FrameAverageMs:0.0} ms avg / {r.FrameWorstMs:0} ms worst, {r.Spikes} over {FrameSpikeMs:0} ms";
                    Run.Event("balance", line);
                    Run.Log($"| {n} | {(floor > 0 ? floor.ToString() : "-")} | {clear} | {r.Deaths} | {r.Diamonds} | {r.Rewinds} ({r.RewoundSeconds:0.0}) | {r.Grazes} | {r.Hits} | " +
                            $"{r.FrameAverageMs:0.0} / {r.FrameWorstMs:0} ms | {r.Spikes} |");
                    string p = $"balance.level{n}.";
                    Run.Metric(p + "clearSeconds", r.Cleared ? r.ClearSeconds : -1f);
                    Run.Metric(p + "deaths", r.Deaths);
                    Run.Metric(p + "diamonds", r.Diamonds);
                    Run.Metric(p + "rewinds", r.Rewinds);
                    Run.Metric(p + "rewoundSeconds", r.RewoundSeconds);
                    Run.Metric(p + "grazes", r.Grazes);
                    Run.Metric(p + "hits", r.Hits);
                    Run.Metric(p + "frameAvgMs", (float)r.FrameAverageMs);
                    Run.Metric(p + "frameWorstMs", (float)r.FrameWorstMs);
                    totalClear += r.Cleared ? r.ClearSeconds : 0;
                    totalDiamonds += r.Diamonds;

                    // The daily frame-time budget, floor by floor.
                    if (r.Frames < 60)
                        continue;
                    double share = (double)r.Spikes / r.Frames;
                    bool ok = r.FrameAverageMs <= FramePlayAverageMs && share <= FrameSpikeShare;
                    string detail = $"{r.FrameAverageMs:0.0} ms average over {r.Frames} frames (budget {FramePlayAverageMs:0}), " +
                                    $"{share * 100:0.0}% of frames over {FrameSpikeMs:0} ms (budget {FrameSpikeShare * 100:0}%), worst {r.FrameWorstMs:0} ms";
                    Run.Event("check", $"frame-time level {n}: {(ok ? "ok" : "FAILED")} ({detail})");
                    if (!ok)
                    {
                        overBudget++;
                        Run.Problem(PlaytestSeverity.Warning, $"frame-time: level {n} (Floor {floor}) over budget: {detail}");
                    }
                }
                Run.Metric("balance.floors", rows);
                Run.Metric("balance.totalClearSeconds", (float)totalClear);
                Run.Metric("balance.totalDiamonds", (float)totalDiamonds);
                Run.Metric("frameTime.floorsOverBudget", overBudget);
                Run.Event("balance", $"{what}: {rows} floor(s) reported, {totalClear:0} s of clears, {totalDiamonds:0} diamonds, {overBudget} floor(s) over the frame-time budget");
            }
        }
    }
}
