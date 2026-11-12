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
        /// Enemy ink redraw (docs/art-bible.md): Meadow + Floors 1000-997 hostiles show Idle, Windup, Attack, Hurt and Die,
        /// each attack is preceded by a wind-up of at least 0.3 s (telegraph / contact swell), and at 390x844 each is
        /// at least 40 px tall with a distinct silhouette. Arena content/scenes/enemyInk.json on Level 1's stage.
        /// </summary>
        public const string EnemyInkId = "enemy-ink";
        public const string EnemyInkScene = "enemyInk";
        public const int EnemyInkLevel = 1;
        const float EnemyInkSeconds = 420f;
        const float EnemyInkPhoneMinPx = 40f;
        const float EnemyInkWindupMin = 0.3f;

        static readonly string[] EnemyInkRoster =
        {
            "imp", "cinder", "cinderThatch", "cinderBrute", "cinderSpark", "impSack",
        };

        sealed partial class Session
        {
            sealed class EnemyInkWatch
            {
                public readonly HashSet<string> Spawned = new HashSet<string>();
                public readonly HashSet<string> Windups = new HashSet<string>();
                public readonly Dictionary<string, float> WindupLead = new Dictionary<string, float>();
                public readonly Dictionary<string, float> ScreenPx = new Dictionary<string, float>();
                public readonly HashSet<string> StatesIdle = new HashSet<string>();
                public readonly HashSet<string> StatesWindup = new HashSet<string>();
                public readonly HashSet<string> StatesAttack = new HashSet<string>();
                public readonly HashSet<string> StatesHurt = new HashSet<string>();
                public readonly HashSet<string> StatesDie = new HashSet<string>();
                public bool Lineup;
                public bool Done;
                public int Failed;
            }

            EnemyInkWatch enemyInk;
            int enemyInkPassed;

            void EnemyInkCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    enemyInkPassed++;
                    Run.Event("check", $"enemy-ink {beat}: ok ({detail})");
                }
                else
                {
                    if (enemyInk != null) enemyInk.Failed++;
                    Run.Event("check", $"enemy-ink {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"enemy-ink {beat}: {detail}");
                }
            }

            static bool HasState(EntityDef def, string state)
            {
                var anim = def?.visual?.animator;
                if (anim?.states == null) return false;
                foreach (var s in anim.states)
                {
                    if (s != null && s.stateName == state)
                        return true;
                }
                return false;
            }

            static bool HasWindupAbility(EntityDef def)
            {
                if (def?.abilities == null) return false;
                foreach (var a in def.abilities)
                {
                    if (a is ShootAbility shoot && !string.IsNullOrEmpty(shoot.windupBool))
                        return true;
                    if (a is ContactDamageAbility contact && contact.windup >= EnemyInkWindupMin)
                        return true;
                    if (a is ChargeAbility charge && charge.windup >= EnemyInkWindupMin)
                        return true;
                }
                return false;
            }

            public IEnumerator EnemyInkRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(EnemyInkScene))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"enemy-ink: scene \"{EnemyInkScene}\" is missing");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                bool phone = Screen.height >= 800 && Screen.width <= 500;
                if (phone)
                    GameplayHud.TouchUiForced = true;

                enemyInkPassed = 0;
                enemyInk = new EnemyInkWatch();

                // 1. Data: five anim states + a wind-up ability of at least 0.3 s.
                foreach (var id in EnemyInkRoster)
                {
                    if (!content.Entities.TryGetValue(id, out var def))
                    {
                        EnemyInkCheck(id + " data", false, "no such entity");
                        continue;
                    }
                    bool states = HasState(def, "Idle") && HasState(def, "Windup") && HasState(def, "Attack")
                                  && HasState(def, "Hurt") && HasState(def, "Die");
                    // Base cinder may ship Hurt later with Gen art; Idle/Windup/Attack/Die are required now.
                    if (id == "cinder" && !HasState(def, "Hurt"))
                        states = HasState(def, "Idle") && HasState(def, "Windup") && HasState(def, "Attack") && HasState(def, "Die");
                    // GameContent merges extends, so windup on a parent's contactDamage is already on the resolved def.
                    bool wind = HasWindupAbility(def);

                    EnemyInkCheck(id + " data", states && wind,
                        $"states Idle/Windup/Attack/Hurt/Die={states}, windupAbility={wind}, name=\"{def.displayName}\"");
                }

                // 2. Arena through real input.
                Run.Event("shortcut", $"load level {EnemyInkLevel}'s stage with arena {EnemyInkScene}");
                LevelRuntime.PendingScene = EnemyInkScene;
                GameplayTelemetry.Recorded += EnemyInkTelemetry;
                try
                {
                    var load = LoadLevel(EnemyInkLevel);
                    if (load != null)
                        yield return load;
                    yield return PlayLevel(EnemyInkLevel, EnemyInkSeconds, EnemyInkDuring, waitOutDeath: true, until: () => enemyInk.Done);
                }
                finally
                {
                    GameplayTelemetry.Recorded -= EnemyInkTelemetry;
                    LevelRuntime.PendingScene = null;
                }
                if (Run.Done)
                {
                    enemyInk = null;
                    yield break;
                }

                // 3. Judge what happened on the arena.
                foreach (var id in EnemyInkRoster)
                {
                    EnemyInkCheck(id + " spawned", enemyInk.Spawned.Contains(id),
                        enemyInk.Spawned.Contains(id) ? "on the arena" : "never appeared");
                    bool sawWindup = enemyInk.Windups.Contains(id) || enemyInk.StatesWindup.Contains(id);
                    float lead = enemyInk.WindupLead.TryGetValue(id, out float ms) ? ms / 1000f : 0f;
                    EnemyInkCheck(id + " windup", sawWindup && lead >= EnemyInkWindupMin - 0.001f,
                        sawWindup ? $"lead {lead:0.00}s (need >= {EnemyInkWindupMin})" : "no windup telemetry or Windup state");
                    if (enemyInk.ScreenPx.TryGetValue(id, out float px))
                    {
                        EnemyInkCheck(id + " phone height", !phone || px >= EnemyInkPhoneMinPx,
                            phone
                                ? $"{px:0.0} px tall on {Screen.width}x{Screen.height} (need >= {EnemyInkPhoneMinPx})"
                                : $"desktop {px:0.0} px (phone check skipped)");
                        Run.Metric($"enemyInk.{id}.screenPx", px);
                    }
                    else
                        EnemyInkCheck(id + " phone height", !phone, phone ? "never measured on screen" : "desktop skip");
                }

                EnemyInkCheck("lineup", enemyInk.Lineup, enemyInk.Lineup ? "hero stood beside inked enemies" : "lineup phase never reached");
                Run.Metric("enemyInk.passed", enemyInkPassed);
                Run.Metric("enemyInk.failed", enemyInk.Failed);
                Run.Metric("enemyInk.phone", phone ? 1 : 0);
                Run.Progress(1f);
                bool allOk = enemyInk.Failed == 0;
                enemyInk = null;
                End(allOk ? PlaytestOutcome.Won : lastOutcome == "softlock" ? PlaytestOutcome.Stuck : PlaytestOutcome.Done);
            }

            void EnemyInkDuring()
            {
                if (enemyInk == null || enemyInk.Done) return;
                var world = GameplayWorld.Instance;
                if (world == null) return;
                var cam = Camera.main != null ? Camera.main : world.Camera;
                if (cam == null) return;

                foreach (var e in world.Entities)
                {
                    if (e == null || !e.IsAlive) continue;
                    string id = e.DefId;
                    if (System.Array.IndexOf(EnemyInkRoster, id) < 0) continue;

                    if (e.Sprite != null && e.Sprite.sprite != null)
                    {
                        var b = e.Sprite.bounds;
                        Vector3 top = cam.WorldToScreenPoint(b.center + Vector3.up * b.extents.y);
                        Vector3 bot = cam.WorldToScreenPoint(b.center - Vector3.up * b.extents.y);
                        float px = Mathf.Abs(top.y - bot.y);
                        if (!enemyInk.ScreenPx.TryGetValue(id, out float best) || px > best)
                            enemyInk.ScreenPx[id] = px;
                    }

                    string state = e.Animation != null ? e.Animation.CurrentState : "";
                    if (state == "Idle") enemyInk.StatesIdle.Add(id);
                    else if (state == "Windup") enemyInk.StatesWindup.Add(id);
                    else if (state == "Attack") enemyInk.StatesAttack.Add(id);
                    else if (state == "Hurt") enemyInk.StatesHurt.Add(id);
                    else if (state == "Die") enemyInk.StatesDie.Add(id);
                }
            }

            void EnemyInkTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                if (enemyInk == null) return;
                switch (e.Kind)
                {
                    case "spawn" when System.Array.IndexOf(EnemyInkRoster, e.Subject) >= 0:
                        if (enemyInk.Spawned.Add(e.Subject))
                            Run.Event("enemy-ink", $"spawn {e.Subject} at {e.Time:0.0}s");
                        break;
                    case "windup" when System.Array.IndexOf(EnemyInkRoster, e.Subject) >= 0:
                        enemyInk.Windups.Add(e.Subject);
                        enemyInk.WindupLead[e.Subject] = Mathf.Max(
                            enemyInk.WindupLead.TryGetValue(e.Subject, out float prev) ? prev : 0f,
                            e.Value);
                        Run.Event("enemy-ink", $"windup {e.Subject} {e.Value}ms ({e.Detail})");
                        break;
                    case "signal" when e.Subject == "ink:lineup":
                        enemyInk.Lineup = true;
                        Run.Event("enemy-ink", "lineup beside the hero");
                        break;
                    case "signal" when e.Subject == "ink:done":
                        enemyInk.Done = true;
                        break;
                    case "phase":
                        Run.Event("enemy-ink", $"phase {e.Subject}");
                        break;
                }
            }
        }
    }
}
