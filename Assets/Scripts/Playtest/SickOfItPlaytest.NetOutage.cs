using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Core.Flow;
using SickOfIt.Core.Progression;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The net-outage scenario: the Hawkes Net server is down (a setup shortcut makes every attempt fail with 502, then 410),
        /// the game warns once per retry and never errors, retries on a growing timer, plays a level meanwhile, shows the quiet offline chip,
        /// stops on 410 and reconnects (without a reload) when the server comes back.</summary>
        public const string NetOutageId = "net-outage";
        const float NetOutageSeconds = 240f;

        sealed partial class Session
        {
            int netFailed, netErrors, netWarnings;

            void NetCheck(string beat, bool ok, string detail)
            {
                if (ok) { Run.Event("check", $"net-outage {beat}: ok ({detail})"); return; }
                netFailed++;
                Run.Event("check", $"net-outage {beat}: FAILED ({detail})");
                Run.Problem(PlaytestSeverity.Bug, $"net-outage {beat}: {detail}");
            }

            void NetLog(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    netErrors++;
                else if (type == LogType.Warning && message.StartsWith("Hawkes Net:", System.StringComparison.Ordinal))
                    netWarnings++;
            }

            public IEnumerator NetOutageRun()
            {
                bool live = GhostLobby.Status != "off"; // a lobby exists only in a WebGL build (and not with ?ghosts=0)
                if (!live)
                    Run.Event("check", "net-outage: no lobby here (not WebGL): the single-player and no-error checks still run");
                yield return ToMainMenu();
                if (Run.Done) yield break;
                Application.logMessageReceived += NetLog;
                int attempts0 = GhostLobby.Attempts;
                GhostLobby.SimulateOutage("502");
                // Take the connection down now (setup shortcut): the current one is dropped by the next attempt's failure.
                Run.Event("shortcut", "the server is down: every connection attempt fails with 502");

                // 1. Menu with the server down: a warning per retry, growing waits, the quiet chip, no errors.
                if (live)
                {
                    yield return Run.Until(() => GhostLobby.Attempts - attempts0 >= 4, 90f, "four retries on the backoff timer");
                    int tries = GhostLobby.Attempts - attempts0;
                    NetCheck("warnings", netWarnings <= tries, $"{netWarnings} warnings for {tries} attempts (one each at most)");
                    NetCheck("backoff", GhostLobby.Status == "offline" && GhostLobby.NextRetryIn > 3f, $"status {GhostLobby.Status}, next retry in {GhostLobby.NextRetryIn:0.0} s (2, 4, 8 s ... steps)");
                    NetCheck("chip", GhostLobby.Offline, "the menu shows its quiet offline chip while the server is down");
                }
                NetCheck("no errors", netErrors == 0, $"{netErrors} errors logged while the server was down");

                // 2. Single-player through a level with the server down.
                SceneTransitionManager.Instance.LoadLevel(1);
                float before = ClockTime;
                yield return PlayLevel(1, 25f);
                if (Run.Done) yield break;
                NetCheck("single-player", ClockTime > before + 10f || ClockTime < before, $"level 1 played with the server down (clock {ClockTime:0.0} s)");
                NetCheck("no errors in level", netErrors == 0, $"{netErrors} errors logged");

                // 3. A 410 stops retrying for the session.
                if (live)
                {
                    GhostLobby.SimulateOutage("410");
                    yield return Run.Until(() => GhostLobby.Status == "halted", 90f, "the 410 to halt the retries");
                    int halted = GhostLobby.Attempts;
                    yield return WaitReal(15f);
                    NetCheck("410 stops", GhostLobby.Attempts == halted, $"no attempt in 15 s after the 410 ({GhostLobby.Attempts - halted} made)");

                    // 4. The server comes back: the next scene load asks again and the game is online with no reload.
                    GhostLobby.SimulateOutage(null);
                    yield return ToMainMenu();
                    yield return Run.Until(() => GhostLobby.Online, 60f, "the game to reconnect by itself");
                    NetCheck("reconnect", GhostLobby.Online, $"status {GhostLobby.Status} without a reload");
                }
                Application.logMessageReceived -= NetLog;
                Run.Metric("netOutage.warnings", netWarnings);
                Run.Metric("netOutage.errors", netErrors);
                Run.Progress(1f);
                End(netFailed == 0 ? PlaytestOutcome.Won : PlaytestOutcome.Done);
            }
        }
    }
}
