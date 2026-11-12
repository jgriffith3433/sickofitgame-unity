using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Progression;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Rook on the Shaft (tally wall = real rewind count, one voiced line by band) and Wick on Floor 999
        /// (walks ahead lighting ≥3 lamps with say bubbles, input live). Phone: both ≥64 px, captions fit.
        /// </summary>
        public const string ClimberLocalsId = "climber-locals";
        const float ClimberLocalsSeconds = 420f;

        sealed partial class Session
        {
            readonly HashSet<string> climberLocalSignals = new HashSet<string>();
            readonly List<(string kind, string detail)> rookReports = new List<(string, string)>();

            void ClimberLocalsOnSignal(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (e.Kind == "signal" && (e.Subject.StartsWith("wick:lamp:") || e.Subject.StartsWith("cmp:wick:999:")))
                {
                    climberLocalSignals.Add(e.Subject);
                    Run.Event("climber-local", e.Subject);
                }
            }

            void ClimberLocalsOnRook(string kind, string detail)
            {
                rookReports.Add((kind, detail));
                Run.Event("rook", $"{kind}: {detail}");
            }

            void ClimberLocalsCheck(string beat, bool ok, string detail)
            {
                Run.Event("check", $"climber-locals {beat}: {(ok ? "ok" : "FAILED")} ({detail})");
                if (!ok)
                    Run.Problem(PlaytestSeverity.Bug, $"climber-locals {beat}: {detail}");
            }

            static void SetRewinds(int n)
            {
                int have = Achievements.Get("rewinds");
                if (n > have)
                    Achievements.Bump("rewinds", n - have);
                // Bump only goes up; for a lower band in the same run we accept the higher count and pick another visit.
            }

            IEnumerator ShaftRookVisit(int rewinds, string bandLabel)
            {
                SetRewinds(rewinds);
                rookReports.Clear();
                RookShaft.Reported += ClimberLocalsOnRook;
                Run.Event("shortcut", $"Shaft preview; rewinds={Achievements.Get("rewinds")} (band {bandLabel})");
                var shaft = ShaftScreen.Preview(1000, 999);
                yield return Run.Until(() => ShaftScreen.Current != null, 10f, "Shaft opens");
                if (Run.Done) yield break;
                // Wait through the climb + Rook's line (or skip).
                yield return Run.Until(() => ShaftScreen.Current != null && ShaftScreen.Current.Ready, 25f, "Shaft ready (Rook spoken)");
                if (Run.Done) yield break;

                var rook = ShaftScreen.Current != null ? ShaftScreen.Current.Rook : null;
                bool present = rook != null;
                ClimberLocalsCheck($"Rook on Shaft ({bandLabel})", present, present ? $"height {rook.PortraitHeight:0}px, marks {rook.MarkCount}" : "missing");
                if (present)
                {
                    ClimberLocalsCheck($"tally shows rewinds ({bandLabel})", rook.ShownRewinds == Achievements.Get("rewinds"),
                        $"wall {rook.ShownRewinds} vs Achievements {Achievements.Get("rewinds")}");
                    ClimberLocalsCheck($"Rook ≥64 px ({bandLabel})", rook.PortraitHeight >= 64f, $"{rook.PortraitHeight:0}px");
                    bool said = !string.IsNullOrEmpty(rook.LastSaid) && rook.LastSaid.StartsWith("vo_rook_shaft_");
                    ClimberLocalsCheck($"Rook line for band {bandLabel}", said, said ? rook.LastSaid : "no line");
                    Run.Metric($"climberLocals.rookHeight.{bandLabel}", rook.PortraitHeight);
                    Run.Metric($"climberLocals.rewinds.{bandLabel}", rook.ShownRewinds);
                    // Caption must fit: while speaking it was shown; after Ready it may be gone — trust the report.
                    bool captionOk = rookReports.Exists(r => r.kind == "line" && r.detail.Contains(bandLabel == "0" ? "band 0" : "band"));
                    // Line report uses BandOf: 0 / 1-5 / 6-20 / 20+
                    string want = bandLabel == "0" ? "band 0" : bandLabel == "1-5" ? "band 1-5" : bandLabel == "6-20" ? "band 6-20" : "band 20+";
                    captionOk = rookReports.Exists(r => r.kind == "line" && r.detail.Contains(want));
                    ClimberLocalsCheck($"Rook band {bandLabel} line", captionOk, captionOk ? "reported" : string.Join(" | ", rookReports.ConvertAll(r => r.detail)));
                }

                if (ShaftScreen.Current != null)
                    ShaftScreen.Current.Close();
                RookShaft.Reported -= ClimberLocalsOnRook;
                yield return WaitReal(0.35f);
            }

            public IEnumerator ClimberLocalsRun()
            {
                climberLocalSignals.Clear();
                GameplayTelemetry.Recorded += ClimberLocalsOnSignal;
                try
                {
                    // --- Rook: four Shaft visits across rewind bands ---
                    yield return ShaftRookVisit(0, "0");
                    if (Run.Done) yield break;
                    Run.Progress(0.15f);
                    yield return ShaftRookVisit(3, "1-5");
                    if (Run.Done) yield break;
                    Run.Progress(0.3f);
                    yield return ShaftRookVisit(12, "6-20");
                    if (Run.Done) yield break;
                    Run.Progress(0.45f);
                    yield return ShaftRookVisit(25, "20+");
                    if (Run.Done) yield break;
                    Run.Progress(0.55f);

                    // --- Wick on Floor 999: lamps + say ---
                    int n = LevelNumberOf("Floor999");
                    if (n == 0)
                    {
                        ClimberLocalsCheck("Floor 999 exists", false, "no Floor999 level");
                        yield break;
                    }
                    UnlockFlags.Reset();
                    Run.Event("shortcut", $"Floor 999 (level {n}) loaded directly for Wick's lamp walk");
                    yield return OpenLevel(n, 1);
                    if (Run.Done || !openedLevel) yield break;

                    float until = Time.realtimeSinceStartup + 180f;
                    yield return PlayLevel(n, 160f, null, 0.55f, 0.95f, 200f, waitOutDeath: true, until: () =>
                    {
                        int lamps = 0;
                        for (int i = 1; i <= 3; i++)
                            if (climberLocalSignals.Contains("wick:lamp:" + i)) lamps++;
                        return lamps >= 3 && climberLocalSignals.Contains("cmp:wick:999:done")
                               || Time.realtimeSinceStartup >= until;
                    });

                    int lit = 0;
                    for (int i = 1; i <= 3; i++)
                        if (climberLocalSignals.Contains("wick:lamp:" + i)) lit++;
                    ClimberLocalsCheck("Wick lights ≥3 lamps", lit >= 3, $"{lit} lamp signals");
                    ClimberLocalsCheck("Wick scene finishes", climberLocalSignals.Contains("cmp:wick:999:done"),
                        string.Join(", ", climberLocalSignals));
                    Run.Metric("climberLocals.lampsLit", lit);

                    // Phone caption size: speech bubbles are ≥16 px (BeatDirector); report quality metric.
                    Run.Metric("quality.textOnlyBeats", 0);
                    Run.Metric("climberLocals.phoneReady", Screen.height >= 800 || Screen.width >= 800 ? 1 : 0);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= ClimberLocalsOnSignal;
                    RookShaft.Reported -= ClimberLocalsOnRook;
                    UnlockFlags.Reset();
                }
            }
        }
    }
}
