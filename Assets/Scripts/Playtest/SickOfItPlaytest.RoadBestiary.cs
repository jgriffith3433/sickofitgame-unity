using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// The road-bestiary scenario (Docs/Story.md "The Cracked Road's bestiary"): the ten Circle IX enemies on the arena
        /// content/scenes/roadBestiary.json (Level 1's stage, setup shortcut), one station each, real input. Before it plays: each has a
        /// name, a hint, a hint voice, sounds and a row in crackedRoad's enemy table, and no two share an attack. While it plays each
        /// mechanic is seen: the plate's blocks, the bomber's blink and bomb, the bouncing lump, the chain sharing a blow, the revive,
        /// the wraith passing shots until the bot holds R (then taking damage), the veil's shield and blink, the sniper's fast bolt,
        /// the fuse whelp, the bastion's shield and revive. One check event per creature.
        /// </summary>
        public const string RoadBestiaryId = "road-bestiary";
        public const string RoadBestiaryScene = "roadBestiary";
        const float RoadBestiarySeconds = 520f;

        static readonly string[] RoadBestiaryEnemies =
        {
            "plateDemon", "riftBomber", "bounceLobber", "chainWhelp", "risenDemon", "loopWraith", "veilDemon", "slabSniper", "fuseWhelp", "bastionBrute",
        };

        sealed partial class Session
        {
            sealed class RoadWatch
            {
                public readonly HashSet<string> Passed = new HashSet<string>();
                public readonly HashSet<string> Spawned = new HashSet<string>();
                public bool Done;
                public int Failed;
                public readonly Dictionary<string, int> Count = new Dictionary<string, int>();
                public readonly HashSet<int> BoltSeen = new HashSet<int>();
                public float BoltMaxSpeed;
                public bool WraithRewound;
                public float WraithSnagAt = -1f;
                public float TeleTelegraphAt = -1f;
                public int BomberBlinks;
            }

            RoadWatch road;

            int RoadN(string key) => road != null && road.Count.TryGetValue(key, out int n) ? n : 0;

            void RoadBump(string key)
            {
                road.Count.TryGetValue(key, out int n);
                road.Count[key] = n + 1;
            }

            void RoadCheck(string beat, bool ok, string detail)
            {
                if (road == null || road.Passed.Contains(beat))
                    return;
                if (ok)
                {
                    road.Passed.Add(beat);
                    Run.Event("check", $"road-bestiary {beat}: ok ({detail})");
                }
                else
                {
                    road.Failed++;
                    Run.Event("check", $"road-bestiary {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"road-bestiary {beat}: {detail}");
                }
            }

            public IEnumerator RoadBestiaryRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(RoadBestiaryScene) || !content.Circles.TryGetValue("crackedRoad", out var circle))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"road-bestiary: the scene \"{RoadBestiaryScene}\" or the circle crackedRoad is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // 1. The data.
                road = new RoadWatch();
                var signatures = new Dictionary<string, string>();
                foreach (var id in RoadBestiaryEnemies)
                {
                    if (!content.Entities.TryGetValue(id, out var def))
                    {
                        RoadCheck(id + " data", false, "there is no such entity");
                        continue;
                    }
                    int fromFloor = 0;
                    bool inTable = false;
                    foreach (var row in circle.enemies)
                    {
                        if (row.entity == id)
                        {
                            inTable = true;
                            fromFloor = row.fromFloor;
                        }
                    }
                    bool ok = !string.IsNullOrEmpty(def.displayName) && !string.IsNullOrEmpty(def.hint) && !string.IsNullOrEmpty(def.hintVoice) && inTable
                              && def.sounds != null && !string.IsNullOrEmpty(def.sounds.hurtSound) && !string.IsNullOrEmpty(def.sounds.deathSound);
                    string sig = BestiarySignature(def);
                    string clash = null;
                    foreach (var pair in signatures)
                    {
                        if (pair.Value == sig)
                            clash = pair.Key;
                    }
                    signatures[id] = sig;
                    RoadCheck(id + " data", ok && clash == null,
                        $"\"{def.displayName}\": \"{def.hint}\" (voice {def.hintVoice}), in crackedRoad from floor {fromFloor}: {(inTable ? "yes" : "NO")}, attack {sig}" +
                        (clash != null ? $", the SAME as {clash}" : ""));
                }

                // 2. The arena, played by the bot.
                Run.Event("shortcut", $"load level {BestiaryLevel}'s stage with the arena {RoadBestiaryScene} directly");
                LevelRuntime.PendingScene = RoadBestiaryScene;
                GameplayTelemetry.Recorded += RoadTelemetry;
                try
                {
                    var load = LoadLevel(BestiaryLevel);
                    if (load != null)
                        yield return load;
                    yield return PlayLevel(BestiaryLevel, RoadBestiarySeconds, RoadDuring, waitOutDeath: true, until: () => road.Done);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= RoadTelemetry;
                    LevelRuntime.PendingScene = null;
                }
                if (Run.Done)
                {
                    road = null;
                    yield break;
                }

                // 3. What each one did.
                foreach (var id in RoadBestiaryEnemies)
                    RoadCheck(id + " on the arena", road.Spawned.Contains(id), road.Spawned.Contains(id) ? "it appeared" : "it never appeared on the arena");
                RoadCheck("plateDemon shield", RoadN("block:plateDemon") > 0, $"{RoadN("block:plateDemon")} shots spent on the plate, {RoadN("shard")} shards fired");
                RoadCheck("riftBomber blink and bomb", road.BomberBlinks > 0 && RoadN("spawn:riftBomb") > 0, $"{road.BomberBlinks} blinks after a mark, {RoadN("spawn:riftBomb")} bombs set down");
                RoadCheck("bounceLobber bounce", RoadN("bounce:bounceLump") > 0, $"{RoadN("bounce:bounceLump")} bounces of its lump");
                RoadCheck("chainWhelp chain", RoadN("chainBound") > 0 && RoadN("chainShare") > 0, $"{RoadN("chainBound")} pair bound, {RoadN("chainShare")} blows shared, {RoadN("chainDied")} shared to the death");
                RoadCheck("risenDemon revive", RoadN("revive:risenDemon") > 0 && RoadN("rise:risenDemon") > 0, $"{RoadN("revive:risenDemon")} fell, {RoadN("rise:risenDemon")} rose");
                RoadCheck("loopWraith rewind trick", RoadN("deflect:loopWraith") > 0 && RoadN("snag:loopWraith") > 0 && RoadN("hurt:loopWraith") > 0,
                    $"{RoadN("deflect:loopWraith")} shots passed through, {RoadN("snag:loopWraith")} snags after a rewind, {RoadN("hurt:loopWraith")} hits taken once snagged");
                RoadCheck("veilDemon shield and blink", RoadN("block:veilDemon") > 0 && RoadN("telegraph:veilDemon") > 0, $"{RoadN("block:veilDemon")} blocks, {RoadN("telegraph:veilDemon")} blinks marked");
                RoadCheck("slabSniper bolt", RoadN("bolt") > 0 && road.BoltMaxSpeed >= 7f && (RoadN("block:slabSniper") > 0 || RoadN("hurt:slabSniper") > 0),
                    $"{RoadN("bolt")} bolts, fastest {road.BoltMaxSpeed:0.0} u/s, {RoadN("block:slabSniper")} blocked, {RoadN("hurt:slabSniper")} landed");
                RoadCheck("fuseWhelp fuse", RoadN("spawn:fuseWhelp") > 0, "it ran in and either blew or was shot");
                RoadCheck("bastionBrute shield and revive", RoadN("block:bastionBrute") > 0 && RoadN("revive:bastionBrute") > 0, $"{RoadN("block:bastionBrute")} blocks, fell {RoadN("revive:bastionBrute")}, rose {RoadN("rise:bastionBrute")}");

                Run.Metric("roadBestiary.passed", road.Passed.Count);
                Run.Metric("roadBestiary.failed", road.Failed);
                Run.Progress(1f);
                bool allOk = road.Failed == 0;
                road = null;
                End(allOk ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            void RoadTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (road == null)
                    return;
                string at = $"at {e.Time:0.0} s";
                switch (e.Kind)
                {
                    case "spawn":
                        if (System.Array.IndexOf(RoadBestiaryEnemies, e.Subject) >= 0 && road.Spawned.Add(e.Subject))
                            Run.Event("roadBestiary", $"{at}: {e.Subject} is on the arena");
                        if (e.Subject == "riftBomb" || e.Subject == "fuseWhelp")
                            RoadBump("spawn:" + e.Subject);
                        else if (e.Subject == "plateShard")
                            RoadBump("shard");
                        break;
                    case "shieldBlock":
                        RoadBump("block:" + e.Subject);
                        Run.Event("roadBestiary", $"{at}: {e.Subject}'s shield blocked {e.Value}");
                        break;
                    case "damage":
                        if (e.Subject == "loopWraith" && road.WraithSnagAt >= 0f)
                            RoadBump("hurt:loopWraith");
                        else if (e.Subject == "slabSniper")
                            RoadBump("hurt:slabSniper");
                        break;
                    case "teleport":
                        if (e.Detail.StartsWith("telegraph", System.StringComparison.Ordinal))
                        {
                            RoadBump("telegraph:" + e.Subject);
                            if (e.Subject == "riftBomber")
                                road.TeleTelegraphAt = e.Time;
                        }
                        else if (e.Subject == "riftBomber" && road.TeleTelegraphAt >= 0f && e.Time - road.TeleTelegraphAt >= 0.5f)
                        {
                            road.BomberBlinks++;
                            Run.Event("roadBestiary", $"{at}: riftBomber blinked {e.Time - road.TeleTelegraphAt:0.00} s after its mark");
                        }
                        break;
                    case "bounce":
                        RoadBump("bounce:" + e.Subject);
                        break;
                    case "chainBound":
                        RoadBump("chainBound");
                        Run.Event("roadBestiary", $"{at}: {e.Subject} bound to {e.Detail}");
                        break;
                    case "chainShare":
                        RoadBump("chainShare");
                        if (e.Detail == "both die")
                            RoadBump("chainDied");
                        Run.Event("roadBestiary", $"{at}: a blow on {e.Subject} ({e.Value}) went down the chain: {e.Detail}");
                        break;
                    case "revive":
                    case "rise":
                        RoadBump(e.Kind + ":" + e.Subject);
                        Run.Event("roadBestiary", $"{at}: {e.Subject} {(e.Kind == "revive" ? "went down" : "rose")} ({e.Detail})");
                        break;
                    case "snagDeflect":
                        RoadBump("deflect:" + e.Subject);
                        break;
                    case "snag":
                        RoadBump("snag:" + e.Subject);
                        road.WraithSnagAt = e.Time;
                        Run.Event("roadBestiary", $"{at}: {e.Subject} snagged by the rewind ({e.Detail})");
                        break;
                    case "signal" when e.Subject.StartsWith("road:", System.StringComparison.Ordinal):
                        Run.Event("roadBestiary", $"{at}: station {e.Subject.Substring(5)}");
                        if (e.Subject == "road:done")
                            road.Done = true;
                        break;
                }
            }

            /// <summary>Every frame: the sniper's bolts, and the one hold of R that snags the wraith once shots have passed through it.</summary>
            IEnumerator RoadDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null || road == null || world.Player == null)
                    return null;
                IEnumerator hold = null;
                foreach (var e in world.Live)
                {
                    if (!e.IsAlive)
                        continue;
                    if (e.DefId == "slabBolt" && road.BoltSeen.Add(e.Serial) && e.Body != null && e.Body.linearVelocity.sqrMagnitude > 0.01f)
                    {
                        RoadBump("bolt");
                        road.BoltMaxSpeed = Mathf.Max(road.BoltMaxSpeed, e.Body.linearVelocity.magnitude);
                    }
                    else if (e.DefId == "loopWraith" && !road.WraithRewound && RoadN("deflect:loopWraith") >= 2)
                    {
                        road.WraithRewound = true;
                        hold = HoldRewind(0.8f);
                    }
                }
                return hold;
            }
        }
    }
}
