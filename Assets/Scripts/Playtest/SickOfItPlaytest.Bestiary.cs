using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        /// The wastes-bestiary scenario (Docs/Story.md "The Wastes' bestiary"): the ten new Ember Wastes enemies on the test arena
        /// content/scenes/wastesBestiary.json (Level 1's stage, setup shortcut), one station each, played through real input. Before it
        /// plays: every one has a name, a hint, a hint voice and a place in the Circle's enemy table, and no two share an attack.
        /// While it plays: each one's own attack is seen (ring, spiral, blink behind, front shield, split, mines, laser sweep,
        /// summons, lunge, predictive sniper shot). One check event per creature.
        /// </summary>
        public const string BestiaryId = "wastes-bestiary";
        public const string BestiaryScene = "wastesBestiary";
        public const int BestiaryLevel = 1;
        const float BestiarySeconds = 480f;

        static readonly string[] BestiaryEnemies =
        {
            "haloImp", "whirlImp", "seamImp", "slagTortoise", "magmaGeode", "mineBeetle", "lensImp", "pyreCaller", "slagHound", "deadeyeImp",
        };

        sealed partial class Session
        {
            sealed class BestiaryWatch
            {
                public readonly HashSet<string> Passed = new HashSet<string>();
                public readonly HashSet<string> Spawned = new HashSet<string>();
                public bool Done;
                public int Failed;
                public readonly Dictionary<int, int> HaloBuckets = new Dictionary<int, int>();
                public readonly HashSet<int> WhirlAngles = new HashSet<int>();
                public int WhirlShots;
                public readonly HashSet<int> WhirlSerials = new HashSet<int>();
                public float SeamTelegraphAt = -1f;
                public int SeamBlinks;
                public int TortoiseBlocked, TortoiseHurt;
                public int GeodeBits;
                public readonly List<float> MineX = new List<float>();
                public readonly HashSet<int> MineSerials = new HashSet<int>();
                public float LaserWarnAt = -1f;
                public int CallerCinders;
                public Vector2 HoundLast;
                public float HoundLastAt = -1f;
                public float HoundMin = 99f, HoundMax;
                public readonly HashSet<int> BoltSeen = new HashSet<int>();
                public int BoltMoving, BoltGood, Bolts;
                public float BoltMaxSpeed;
            }

            BestiaryWatch bestiary;

            void BestiaryCheck(string beat, bool ok, string detail)
            {
                if (bestiary == null || bestiary.Passed.Contains(beat))
                    return;
                if (ok)
                {
                    bestiary.Passed.Add(beat);
                    Run.Event("check", $"wastes-bestiary {beat}: ok ({detail})");
                }
                else
                {
                    bestiary.Failed++;
                    Run.Event("check", $"wastes-bestiary {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"wastes-bestiary {beat}: {detail}");
                }
            }

            /// <summary>What an entity does, in words: its ability types, and for a shooter its shot, pattern and aim. Two of the ten must never match.</summary>
            static string BestiarySignature(EntityDef def)
            {
                var sb = new StringBuilder();
                foreach (var ability in def.abilities)
                {
                    if (ability == null)
                        continue;
                    string name = ability.GetType().Name;
                    if (name == "ContactDamageAbility" || name == "FaceTargetAbility")
                        continue;
                    sb.Append(name);
                    var type = ability.GetType();
                    var projectile = type.GetField("projectile");
                    var pattern = type.GetField("pattern");
                    var aim = type.GetField("aim");
                    if (projectile != null)
                        sb.Append(':').Append(projectile.GetValue(ability));
                    if (pattern != null && pattern.GetValue(ability) != null)
                        sb.Append(':').Append(pattern.GetValue(ability).GetType().Name);
                    if (aim != null)
                        sb.Append(':').Append(aim.GetValue(ability));
                    var entity = type.GetField("entity");
                    if (entity != null)
                        sb.Append(':').Append(entity.GetValue(ability));
                    sb.Append(';');
                }
                return sb.ToString();
            }

            public IEnumerator BestiaryRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(BestiaryScene) || !content.Circles.TryGetValue("emberWastes", out var circle))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"wastes-bestiary: the scene \"{BestiaryScene}\" or the circle emberWastes is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // 1. The data: a name, a hint, a hint voice, a place in the Circle's table, a different attack.
                var signatures = new Dictionary<string, string>();
                foreach (var id in BestiaryEnemies)
                {
                    if (!content.Entities.TryGetValue(id, out var def))
                    {
                        BestiaryCheck(id + " data", false, "there is no such entity");
                        continue;
                    }
                    bool inTable = false;
                    int fromFloor = 0;
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
                    BestiaryCheck(id + " data", ok && clash == null,
                        $"\"{def.displayName}\": \"{def.hint}\" (voice {def.hintVoice}), in emberWastes from floor {fromFloor}: {(inTable ? "yes" : "NO")}, attack {sig}" +
                        (clash != null ? $", the SAME as {clash}" : ""));
                }

                // 2. The arena, played by the bot.
                bestiary = new BestiaryWatch();
                Run.Event("shortcut", $"load level {BestiaryLevel}'s stage with the arena {BestiaryScene} directly");
                LevelRuntime.PendingScene = BestiaryScene;
                GameplayTelemetry.Recorded += BestiaryTelemetry;
                try
                {
                    var load = LoadLevel(BestiaryLevel);
                    if (load != null)
                        yield return load;
                    yield return PlayLevel(BestiaryLevel, BestiarySeconds, BestiaryDuring, waitOutDeath: true, until: () => bestiary.Done);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= BestiaryTelemetry;
                    LevelRuntime.PendingScene = null;
                }
                if (Run.Done)
                {
                    bestiary = null;
                    yield break;
                }

                // 3. What each one did, judged as a group at the end.
                foreach (var id in BestiaryEnemies)
                    BestiaryCheck(id + " on the arena", bestiary.Spawned.Contains(id), bestiary.Spawned.Contains(id) ? "it appeared" : "it never appeared on the arena");
                BestiaryCheck("haloImp ring", MaxBucket(bestiary.HaloBuckets) >= 8, $"{MaxBucket(bestiary.HaloBuckets)} embers left it in one volley (needs 8+)");
                BestiaryCheck("whirlImp spiral", bestiary.WhirlAngles.Count >= 6, $"{bestiary.WhirlShots} embers in {bestiary.WhirlAngles.Count} distinct directions (a spiral needs 6+)");
                BestiaryCheck("seamImp blink", bestiary.SeamBlinks > 0, $"{bestiary.SeamBlinks} blinks with a mark first");
                BestiaryCheck("slagTortoise shield", bestiary.TortoiseBlocked > 0, $"{bestiary.TortoiseBlocked} hits blocked by the shell, {bestiary.TortoiseHurt} landed on its back");
                BestiaryCheck("magmaGeode split", bestiary.GeodeBits >= 2, $"{bestiary.GeodeBits} geode bits came out of it");
                float spread = 0f;
                foreach (float x in bestiary.MineX)
                    foreach (float y in bestiary.MineX)
                        spread = Mathf.Max(spread, Mathf.Abs(x - y));
                BestiaryCheck("mineBeetle mines", bestiary.MineSerials.Count >= 2 && spread >= 0.8f, $"{bestiary.MineSerials.Count} mines laid, {spread:0.0} units apart");
                BestiaryCheck("lensImp sweep", bestiary.Passed.Contains("lensImp warning then sweep"), "the red line came before the sweep");
                BestiaryCheck("pyreCaller summons", bestiary.CallerCinders >= 2, $"{bestiary.CallerCinders} cinders called");
                BestiaryCheck("slagHound lunge", bestiary.HoundMin < 0.6f && bestiary.HoundMax >= 4f, $"crouched at {bestiary.HoundMin:0.0} u/s, then lunged at {bestiary.HoundMax:0.0} u/s");
                BestiaryCheck("deadeyeImp bolt", bestiary.Bolts > 0 && bestiary.BoltMaxSpeed >= 7f && (bestiary.BoltMoving == 0 || bestiary.BoltGood > 0),
                    $"{bestiary.Bolts} bolts, fastest {bestiary.BoltMaxSpeed:0.0} u/s, {bestiary.BoltGood} of {bestiary.BoltMoving} at the moving hero led him");

                int passed = bestiary.Passed.Count;
                Run.Metric("bestiary.passed", passed);
                Run.Metric("bestiary.failed", bestiary.Failed);
                Run.Progress(1f);
                bool allOk = bestiary.Failed == 0;
                bestiary = null;
                End(allOk ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            static int MaxBucket(Dictionary<int, int> buckets)
            {
                int max = 0;
                foreach (var pair in buckets)
                    max = Mathf.Max(max, pair.Value);
                return max;
            }

            void BestiaryTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (bestiary == null)
                    return;
                string at = $"at {e.Time:0.0} s";
                switch (e.Kind)
                {
                    case "spawn":
                        if (System.Array.IndexOf(BestiaryEnemies, e.Subject) >= 0 && bestiary.Spawned.Add(e.Subject))
                            Run.Event("bestiary", $"{at}: {e.Subject} is on the arena");
                        else if (e.Subject == "haloEmber")
                        {
                            int bucket = Mathf.RoundToInt(e.Time * 10f);
                            bestiary.HaloBuckets.TryGetValue(bucket, out int n);
                            bestiary.HaloBuckets[bucket] = n + 1;
                        }
                        else if (e.Subject == "geodeBit")
                            bestiary.GeodeBits++;
                        else if (e.Subject == "cinder")
                            bestiary.CallerCinders++;
                        break;
                    case "teleport" when e.Subject == "seamImp":
                        if (e.Detail.StartsWith("telegraph", System.StringComparison.Ordinal))
                            bestiary.SeamTelegraphAt = e.Time;
                        else if (bestiary.SeamTelegraphAt >= 0f && e.Time - bestiary.SeamTelegraphAt >= 0.5f)
                        {
                            bestiary.SeamBlinks++;
                            Run.Event("bestiary", $"{at}: seamImp blinked ({e.Detail}), {e.Time - bestiary.SeamTelegraphAt:0.00} s after its mark");
                        }
                        break;
                    case "shieldBlock" when e.Subject == "slagTortoise":
                        bestiary.TortoiseBlocked++;
                        Run.Event("bestiary", $"{at}: slagTortoise's shell blocked {e.Value} damage");
                        break;
                    case "damage" when e.Subject == "slagTortoise":
                        bestiary.TortoiseHurt++;
                        Run.Event("bestiary", $"{at}: slagTortoise took {e.Value} damage on its back");
                        break;
                    case "laser" when e.Subject == "lensImp":
                        if (e.Detail.StartsWith("warning", System.StringComparison.Ordinal))
                            bestiary.LaserWarnAt = e.Time;
                        else if (e.Detail.StartsWith("sweep", System.StringComparison.Ordinal))
                        {
                            float lead = e.Time - bestiary.LaserWarnAt;
                            BestiaryCheck("lensImp warning then sweep", bestiary.LaserWarnAt >= 0f && lead >= 0.8f, $"the beam came {lead:0.00} s after the red line");
                        }
                        break;
                    case "signal" when e.Subject.StartsWith("bestiary:", System.StringComparison.Ordinal):
                        Run.Event("bestiary", $"{at}: station {e.Subject.Substring(5)}");
                        if (e.Subject == "bestiary:done")
                            bestiary.Done = true;
                        break;
                }
            }

            /// <summary>Every frame: the geometry the telemetry doesn't carry (spiral directions, mine places, the hound's lunge, the sniper's lead).</summary>
            IEnumerator BestiaryDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world == null || bestiary == null || world.Player == null)
                    return null;
                Vector2 heroVelocity = world.PlayerVelocity;
                foreach (var e in world.Live)
                {
                    if (!e.IsAlive)
                        continue;
                    switch (e.DefId)
                    {
                        case "whirlEmber":
                            if (bestiary.WhirlSerials.Add(e.Serial) && e.Body != null && e.Body.linearVelocity.sqrMagnitude > 0.01f)
                            {
                                Vector2 v = e.Body.linearVelocity;
                                bestiary.WhirlAngles.Add(Mathf.RoundToInt(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg / 15f));
                                bestiary.WhirlShots++;
                            }
                            break;
                        case "emberMine":
                            if (bestiary.MineSerials.Add(e.Serial))
                            {
                                bestiary.MineX.Add(e.Position.x);
                                Run.Event("bestiary", $"a mine lies at x {e.Position.x:0.0} at {now:0.0} s");
                            }
                            break;
                        case "slagHound":
                            if (bestiary.HoundLastAt >= 0f && now > bestiary.HoundLastAt + 0.01f)
                            {
                                float speed = Vector2.Distance(e.Position, bestiary.HoundLast) / (now - bestiary.HoundLastAt);
                                bestiary.HoundMin = Mathf.Min(bestiary.HoundMin, speed);
                                bestiary.HoundMax = Mathf.Max(bestiary.HoundMax, speed);
                            }
                            bestiary.HoundLast = e.Position;
                            bestiary.HoundLastAt = now;
                            break;
                        case "deadeyeBolt":
                            if (bestiary.BoltSeen.Add(e.Serial) && e.Body != null && e.Body.linearVelocity.sqrMagnitude > 0.01f)
                            {
                                Vector2 v = e.Body.linearVelocity;
                                bestiary.Bolts++;
                                bestiary.BoltMaxSpeed = Mathf.Max(bestiary.BoltMaxSpeed, v.magnitude);
                                if (heroVelocity.magnitude > 0.5f)
                                {
                                    bestiary.BoltMoving++;
                                    Vector2 target = world.PlayerAimPoint;
                                    Vector2 straight = (target - e.Position).normalized;
                                    Vector2 ideal = Emission.LeadDirection(target, heroVelocity, e.Position, v.magnitude);
                                    if (Vector2.Angle(v.normalized, straight) >= 1.5f && Vector2.Angle(v.normalized, ideal) <= 10f)
                                        bestiary.BoltGood++;
                                }
                            }
                            break;
                    }
                }
                return null;
            }
        }
    }
}
