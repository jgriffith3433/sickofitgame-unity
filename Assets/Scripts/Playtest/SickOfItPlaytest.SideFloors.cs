using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Gameplay;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The secret-floors scenario (Docs/Story.md "Secret floors and gauntlets", cookbook "A secret floor"): the six secret floors and the
        /// two gauntlets, each reached through real input from the door in the wall on its host floor, then played to the end.
        /// `gauntlets` is the same run over just the two gauntlet floors.
        /// </summary>
        public const string SideFloorsId = "secret-floors", GauntletsId = "gauntlets";
        const float SideFloorsSeconds = 2400f, GauntletsSeconds = 900f;

        /// <summary>One side floor: the door's id (offerShortcut), its host floor and its own level (levels/*.json), what it pays and how many waves it chains.</summary>
        struct SideRoom
        {
            public string Key, Host, Side, Reward;
            public int Waves;
            public SideRoom(string key, string host, string side, string reward, int waves = 0)
            {
                Key = key; Host = host; Side = side; Reward = reward; Waves = waves;
            }
        }

        /// <summary>Tools/Levels/side_floors.py's ROOMS, in the same order; Reward is what must be in the save once the room is done.</summary>
        static readonly SideRoom[] SideRooms =
        {
            new SideRoom("tallyCloset", "Floor906", "Secret1", "flag:lore.tally"),
            new SideRoom("armoryDoor", "Floor910", "Secret2", "weapon:slagLadle"),
            new SideRoom("heldBreath", "Floor909", "Secret3", "upgrade:heldBreath"),
            new SideRoom("applauseRoom", "Floor907", "Secret4", "diamonds:20"),
            new SideRoom("restStop", "Floor905", "Secret5", "refill"),
            new SideRoom("guestBook", "Floor903", "Secret6", "flag:lore.guestbook"),
            new SideRoom("cageMatch", "Floor908", "Gauntlet1", "diamonds:12", 4),
            new SideRoom("encore", "Floor904", "Gauntlet2", "flag:gauntlet.encore", 5),
        };

        static int LevelNumberByName(string levelName)
        {
            var definition = GameDefinition.Instance;
            int levels = definition != null ? definition.TotalLevels : 0;
            for (int n = 1; n <= levels; n++)
            {
                var level = definition.GetLevel(n);
                if (level != null && level.levelName == levelName)
                    return n;
            }
            return 0;
        }

        /// <summary>The side floors and gauntlets are checked by their own scenarios, not the stage checks of the all-levels runs.</summary>
        static bool IsSideFloor(int n)
        {
            var definition = GameDefinition.Instance;
            var level = definition != null && n > 0 && n <= definition.TotalLevels ? definition.GetLevel(n) : null;
            // Endless Floor and the Daily Challenge have their own scenario (replay-modes), not the all-levels runs' stage checks.
            return level != null && level.levelName != null
                && (level.levelName.StartsWith("Secret") || level.levelName.StartsWith("Gauntlet") || ReplayModes.IsReplayLevel(n));
        }

        sealed partial class Session
        {
            int sidePassed, sideFailed;
            bool takeSideDoors;

            readonly List<string> sideSignals = new List<string>();
            readonly List<float> sideWaveJuice = new List<float>();
            string sideOffer = "", sideTaken = "", sideTakenDetail = "";
            float sideRefillJuice = -1f, sideRefillHealthFraction = -1f;

            void SideCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    sidePassed++;
                    Run.Event("check", $"secret-floors {beat}: ok ({detail})");
                }
                else
                {
                    sideFailed++;
                    Run.Event("check", $"secret-floors {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"secret-floors {beat}: {detail}");
                }
            }

            void SideTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                switch (e.Kind)
                {
                    case "shortcut-offer":
                        sideOffer = e.Subject ?? "";
                        Run.Event("side-door", $"offered \"{e.Subject}\" on FLOOR {e.Value}: {e.Detail}");
                        break;
                    case "shortcut":
                        sideTaken = e.Subject ?? "";
                        sideTakenDetail = e.Detail ?? "";
                        Run.Event("side-door", $"taken \"{e.Subject}\": {e.Detail}");
                        break;
                    case "shortcut-declined":
                        Run.Event("side-door", $"declined \"{e.Subject}\"");
                        break;
                    case "signal" when e.Subject != null && e.Subject.StartsWith("side:", System.StringComparison.Ordinal):
                    {
                        sideSignals.Add(e.Subject);
                        var budget = RewindBudget.Instance;
                        if (e.Subject.StartsWith("side:wave:", System.StringComparison.Ordinal))
                            sideWaveJuice.Add(budget != null ? budget.Juice : -1f);
                        if (e.Subject == "side:reward:refill")
                        {
                            var player = log.World.Player;
                            var health = player != null ? player.GetComponentInChildren<PlayerHealth>() : null;
                            sideRefillJuice = budget != null ? budget.Juice / Mathf.Max(0.01f, budget.Max) : -1f;
                            sideRefillHealthFraction = health != null ? health.CurrentHealth / (float)Mathf.Max(1, health.MaxHealth) : -1f;
                        }
                        Run.Event("side-floor", $"{e.Subject} at {e.Time:0.0} s" + (budget != null ? $", rewind {budget.Juice:0.0}/{budget.Max:0.0} s" : ""));
                        break;
                    }
                }
            }

            static int SideCount(List<string> signals, string prefix)
            {
                int n = 0;
                foreach (var s in signals)
                    if (s.StartsWith(prefix, System.StringComparison.Ordinal))
                        n++;
                return n;
            }

            /// <summary>Setup shortcut: enough diamonds and one Grudge in hand to pay any door (the cost itself is paid by the game, the checks read it).</summary>
            void SideSupplies()
            {
                var roguelite = RogueliteManager.Instance;
                if (roguelite != null && roguelite.GetPoints() < 60)
                {
                    roguelite.AddPoints(100);
                    Run.Event("shortcut", $"diamonds topped up to {roguelite.GetPoints()}, so any door's fare can be paid");
                }
                if (GrudgeCount() == 0)
                {
                    foreach (var id in GameContent.Current.Upgrades.Keys)
                    {
                        if (id == "heldBreath")
                            continue;
                        UpgradeProgress.Add(id);
                        Run.Event("shortcut", $"Grudge {id} given, so a Grudge door can be paid");
                        break;
                    }
                }
            }

            public IEnumerator SideFloorsRun(bool gauntletsOnly)
            {
                var rooms = new List<SideRoom>();
                foreach (var room in SideRooms)
                    if (!gauntletsOnly || room.Waves > 0)
                        rooms.Add(room);
                var content = GameContent.Current;
                float deadline = Time.realtimeSinceStartup + (gauntletsOnly ? GauntletsSeconds : SideFloorsSeconds) / 2f - 120f;
                var rewardsSeen = new List<string>();
                int reached = 0, cleared = 0;
                takeShortcuts = true;
                takeSideDoors = true;
                GameplayTelemetry.Recorded += SideTelemetry;
                try
                {
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        var room = rooms[i];
                        int host = LevelNumberByName(room.Host), side = LevelNumberByName(room.Side);
                        SideCheck($"{room.Key} levels", host > 0 && side > 0 && content != null && content.Upgrades != null,
                            $"{room.Host} is level {host}, {room.Side} is level {side}");
                        if (host <= 0 || side <= 0)
                            continue;
                        if (Time.realtimeSinceStartup > deadline)
                        {
                            Run.Event("budget", $"no time left: {room.Key} not reached");
                            continue;
                        }
                        float lo = i / (float)rooms.Count, hi = (i + 1) / (float)rooms.Count;
                        ShaftScreen.LevelFloors(side, out int sideFloor, out _);
                        ShaftScreen.LevelFloors(host, out _, out int hostEnd);

                        // 1. The door: the host floor played to its last foothold, where the door in the wall is offered; the bot opens it (2).
                        SideSupplies();
                        sideOffer = sideTaken = sideTakenDetail = "";
                        int points = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
                        int grudges = GrudgeCount();
                        Run.Event("secret-floor", $"{room.Key}: Floor {PathFloor(host)} played to its door (level {host})");
                        for (int attempt = 1; attempt <= 3; attempt++)
                        {
                            if (attempt == 1)
                            {
                                Prepare(host, i == 0);
                                var load = LoadLevel(host);
                                if (load != null)
                                    yield return load;
                            }
                            else
                            {
                                Run.Event("retry", $"{room.Host}, attempt {attempt}");
                                yield return ReachLevel(host);
                                if (Run.Done) yield break;
                            }
                            stage = null;
                            yield return PlayLevel(host, 0f, null, lo, Mathf.Lerp(lo, hi, 0.45f), 330f, waitOutDeath: true);
                            if (Run.Done) yield break;
                            if (lastOutcome != "died")
                                break;
                        }
                        SideCheck($"{room.Key} reachable", lastOutcome == "completed" && sideOffer == room.Key && sideTaken == room.Key,
                            $"{room.Host} ended {lastOutcome}; the door \"{room.Key}\" was {(sideOffer == room.Key ? "offered" : "never offered")} and " +
                            $"{(sideTaken == room.Key ? "taken" : "not taken")}");
                        if (lastOutcome != "completed" || sideTaken != room.Key)
                            continue;
                        reached++;
                        int pointsAfter = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
                        SideCheck($"{room.Key} cost", sideTakenDetail.Contains("paid") && (!sideTakenDetail.Contains("diamonds") || pointsAfter < points + 5),
                            $"{sideTakenDetail}; Grudges {grudges} -> {GrudgeCount()}, diamonds {points} -> {pointsAfter}");
                        SideCheck($"{room.Key} saved", ShortcutProgress.WasTaken(room.Key) && WaystoneProgress.RunLevel == side,
                            $"the door is in the save; the run is on level {WaystoneProgress.RunLevel} (want {side}); the Shaft's hop ends on FLOOR {hostEnd}");
                        yield return WaitReal(0.5f);

                        // 2. The side floor itself (a setup shortcut skips the results and the Shaft, which the shortcut scenario checks).
                        sideSignals.Clear();
                        sideWaveJuice.Clear();
                        sideRefillJuice = sideRefillHealthFraction = -1f;
                        int diamondsBefore = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
                        Run.Event("secret-floor", $"{room.Key}: {room.Side} (level {side}) on FLOOR {sideFloor}");
                        Prepare(side, false);
                        var loadSide = LoadLevel(side);
                        if (loadSide != null)
                            yield return loadSide;
                        stage = null;
                        yield return PlayLevel(side, 0f, null, Mathf.Lerp(lo, hi, 0.45f), hi, 420f, waitOutDeath: true);
                        if (Run.Done) yield break;
                        SideCheck($"{room.Key} played", lastOutcome == "completed" && SideCount(sideSignals, "side:enter") > 0 && SideCount(sideSignals, "side:cleared") > 0,
                            $"{room.Side} ended {lastOutcome}; {sideSignals.Count} signals: {string.Join(" ", sideSignals)}");
                        if (lastOutcome != "completed")
                            continue;
                        cleared++;

                        // 3. What it paid, from the save and the game's own state.
                        int diamondsNow = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0;
                        string paid = "";
                        bool ok = false;
                        var parts = room.Reward.Split(':');
                        switch (parts[0])
                        {
                            case "flag":
                                ok = UnlockFlags.Has(parts[1]);
                                paid = $"the lore flag {parts[1]}";
                                break;
                            case "weapon":
                                ok = WeaponUnlocks.Has(parts[1]);
                                paid = $"the weapon {parts[1]}, unlocked for good";
                                break;
                            case "upgrade":
                                ok = UpgradeProgress.LevelOf(parts[1]) >= 1;
                                paid = $"the hidden upgrade {parts[1]} (level {UpgradeProgress.LevelOf(parts[1])})";
                                break;
                            case "diamonds":
                                ok = diamondsNow - diamondsBefore >= int.Parse(parts[1]);
                                paid = $"{diamondsNow - diamondsBefore} diamonds (want {parts[1]}+)";
                                break;
                            case "refill":
                                ok = sideRefillJuice >= 0.95f && sideRefillHealthFraction >= 0.99f;
                                paid = $"rewind {sideRefillJuice * 100f:0}% and health {sideRefillHealthFraction * 100f:0}% at the reward";
                                break;
                        }
                        if (room.Waves > 0)
                        {
                            // A gauntlet also pays its flag besides the purse / the Grudge pick.
                            ok = ok && (room.Key != "encore" || UnlockFlags.Has("gauntlet.encore")) && (room.Key != "cageMatch" || UnlockFlags.Has("gauntlet.cage"));
                        }
                        SideCheck($"{room.Key} reward", ok, paid);
                        if (ok)
                            rewardsSeen.Add(room.Key + "=" + room.Reward);
                        Run.Metric($"side.{room.Key}.paid", ok ? 1 : 0);

                        if (room.Waves > 0)
                        {
                            int waves = SideCount(sideSignals, "side:wave:"), refills = SideCount(sideSignals, "side:refill:");
                            SideCheck($"{room.Key} waves", waves == room.Waves && refills == room.Waves - 1,
                                $"{waves} waves (want {room.Waves}) chained by {refills} rewind refills (want {room.Waves - 1})");
                            bool refilled = sideWaveJuice.Count == room.Waves;
                            for (int w = 1; w < sideWaveJuice.Count; w++)
                                refilled &= RewindBudget.Instance == null || sideWaveJuice[w] >= RewindBudget.Instance.Max - 0.6f;
                            SideCheck($"{room.Key} refills", refilled, "rewind juice at each wave's start: " + string.Join(", ", sideWaveJuice.ConvertAll(j => j.ToString("0.0"))) +
                                                                       (RewindBudget.Instance != null ? $" of {RewindBudget.Instance.Max:0.0}" : ""));
                        }

                        // 4. Back on the climb: the run leads on to the floor after the host.
                        int next = GameDefinition.Instance.GetLevel(host).nextLevelNumber;
                        yield return Run.Until(() => WaystoneProgress.RunLevel == next, 8f, $"the run to lead on to level {next}");
                        SideCheck($"{room.Key} back", WaystoneProgress.RunLevel == next, $"the run is on level {WaystoneProgress.RunLevel} (want {next}, the host's next)");
                    }
                }
                finally
                {
                    GameplayTelemetry.Recorded -= SideTelemetry;
                    takeShortcuts = false;
                    takeSideDoors = false;
                }
                int distinct = new HashSet<string>(rewardsSeen.ConvertAll(r => r.Substring(r.IndexOf('=') + 1))).Count;
                SideCheck("unique rewards", rewardsSeen.Count > 0 && distinct == rewardsSeen.Count,
                    $"{rewardsSeen.Count} rewards paid, {distinct} different: {string.Join(", ", rewardsSeen)}");
                Run.Metric("side.reached", reached);
                Run.Metric("side.cleared", cleared);
                Run.Metric("side.passed", sidePassed);
                Run.Metric("side.failed", sideFailed);
                Run.Progress(1f);
                End(sideFailed > 0 ? PlaytestOutcome.Done : PlaytestOutcome.Won);
            }
        }
    }
}
