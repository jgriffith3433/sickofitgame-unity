using System;
using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Every hero state on its trigger (player.json animationConfig): the bot plays the moves bench (idle, walk, jump, fall, fire, run-and-shoot, dash, wall slide,
        /// wall jump), the weapons bench (fists locomotion, the three punches, the swaps EquipFists / EquipLauncher) and the weapon-looks bench (LauncherTaken, the launcher
        /// back), then loses four times and clears a floor (Hurt, the four deaths, Victory), all through real input. Each state is an expectation armed before its trigger and
        /// judged after: on the same frame the controller says the state is playing, the sprite on screen must be a frame of it (<see cref="PlayerController.ShowsState"/>;
        /// a state whose art is still a stand-in shows its stand-in sheet, said in the event). Every sprite the hero shows is also measured (the sheet cell's size on screen,
        /// scale included) and each state's clip is compared with Idle's: any that differs is reported (<c>hero-anim size</c> check, Warning problem, <c>animSize</c> event).
        /// </summary>
        public const string HeroAnimId = "hero-anim";
        const float HeroAnimSeconds = 1500f;
        const float HeroAnimSizeTolerance = 0.02f;

        sealed partial class Session
        {
            int animPassed, animFailed, animSizeOff;

            /// <summary>A state expected to play: armed before the trigger, judged after.</summary>
            sealed class AnimExpect
            {
                public string State;
                public Func<bool> When;
                public bool Seen;
                public string Detail = "its trigger never happened";
                public bool SpriteMissed;
            }

            /// <summary>The size of one sheet's cell on screen (world units, scale included).</summary>
            struct AnimSize
            {
                public Vector2 Min, Max;
                public int Frames;
            }

            readonly List<AnimExpect> animArmed = new List<AnimExpect>();
            readonly Dictionary<string, AnimSize> animSizes = new Dictionary<string, AnimSize>();
            readonly HashSet<string> animStatesTested = new HashSet<string>();

            void AnimCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    animPassed++;
                    Run.Event("check", $"hero-anim {beat}: ok ({detail})");
                }
                else
                {
                    animFailed++;
                    Run.Event("check", $"hero-anim {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"hero-anim {beat}: {detail}");
                }
            }

            /// <summary>The file name (no extension) of the sheet player.json gives a state, or null.</summary>
            static string AnimSheetOf(string state)
            {
                var gd = GameDefinition.Instance;
                var states = gd != null && gd.player != null && gd.player.animationConfig != null ? gd.player.animationConfig.states : null;
                if (states == null)
                    return null;
                foreach (var s in states)
                {
                    if (s != null && s.stateName == state && s.spriteSheet != null)
                        return s.spriteSheet.name;
                    if (s != null && s.stateName == state && !string.IsNullOrEmpty(s.lazySheet))
                        return System.IO.Path.GetFileNameWithoutExtension(s.lazySheet); // a weapon grip loaded on demand
                }
                return null;
            }

            static SpriteRenderer AnimRenderer(PlayerController pc)
            {
                if (pc == null)
                    return null;
                return pc.GetComponentInChildren<SpriteRenderer>();
            }

            /// <summary>
            /// Is the sprite on screen a frame of this state? ShowsState (the sprite is named State_nnnn) when the state has its own art; while it is a
            /// stand-in the sprite must be a frame of the sheet player.json gives it (the controller's own flag is what says which state it stands in for).
            /// </summary>
            static bool AnimShown(PlayerController pc, string state, out string detail)
            {
                bool exact = pc.ShowsState(state, out string sprite);
                if (exact)
                {
                    detail = sprite;
                    return true;
                }
                string sheet = AnimSheetOf(state);
                bool standIn = sheet != null && sheet != state && sprite != null && sprite.StartsWith(sheet + "_", StringComparison.Ordinal);
                detail = standIn ? $"{sprite} (stand-in: {state} wears the {sheet} sheet until its art is made)" : $"{sprite ?? "no sprite"} is not a frame of {state}";
                return standIn;
            }

            /// <summary>Called every frame before rendering: judges the armed expectations and measures the sprite on screen.</summary>
            void AnimSample()
            {
                var world = GameplayWorld.Instance;
                var pc = world != null ? world.PlayerController : null;
                if (pc == null || Run.Done)
                    return;
                foreach (var e in animArmed)
                {
                    if (e.Seen || e.When == null)
                        continue;
                    bool triggered;
                    try { triggered = e.When(); }
                    catch (Exception) { triggered = false; }
                    if (!triggered)
                        continue;
                    if (AnimShown(pc, e.State, out string detail))
                    {
                        e.Seen = true;
                        e.Detail = detail;
                    }
                    else
                    {
                        e.SpriteMissed = true;
                        e.Detail = detail;
                    }
                }
                var renderer = AnimRenderer(pc);
                if (renderer == null || renderer.sprite == null || !renderer.enabled)
                    return;
                string name = renderer.sprite.name;
                int cut = name.LastIndexOf('_');
                string sheetName = cut > 0 ? name.Substring(0, cut) : name;
                var scale = renderer.transform.lossyScale;
                var cell = renderer.sprite.rect.size / renderer.sprite.pixelsPerUnit;
                var size = new Vector2(cell.x * Mathf.Abs(scale.x), cell.y * Mathf.Abs(scale.y));
                if (!animSizes.TryGetValue(sheetName, out var rec))
                    rec = new AnimSize { Min = size, Max = size };
                rec.Min = Vector2.Min(rec.Min, size);
                rec.Max = Vector2.Max(rec.Max, size);
                rec.Frames++;
                animSizes[sheetName] = rec;
            }

            AnimExpect AnimArm(string state, Func<bool> when)
            {
                var e = new AnimExpect { State = state, When = when };
                animArmed.Add(e);
                return e;
            }

            void AnimJudge(AnimExpect e, string beat)
            {
                animStatesTested.Add(e.State);
                string label = beat == null ? e.State : $"{e.State} ({beat})";
                if (e.Seen)
                    Run.Event("anim", $"{e.State} plays: {e.Detail}");
                AnimCheck(label, e.Seen, e.Seen ? $"{e.State} on screen: {e.Detail}" : e.SpriteMissed ? $"the controller said {e.State} but the screen showed {e.Detail}" : e.Detail);
                animArmed.Remove(e);
            }

            /// <summary>Waits up to 'seconds' (real) for the expectation, so a beat doesn't outlast its trigger.</summary>
            IEnumerator AnimWait(float seconds, params AnimExpect[] all)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < until)
                {
                    bool every = true;
                    foreach (var e in all)
                        every &= e.Seen;
                    if (every)
                        break;
                    yield return null;
                }
                yield return null;
            }

            static bool AnimPlain(PlayerController pc) => HeroClipOnScreen(out _) == null && pc.MovePose == null && pc.UnarmedPose == null && pc.SwapPose == null && !pc.IsClimbing;

            public IEnumerator HeroAnimRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(MovesScene) || !content.Scenes.ContainsKey(WeaponsScene) || !content.Scenes.ContainsKey(WeaponLooksScene)
                    || !content.Entities.ContainsKey("wallSlab") || !content.Weapons.ContainsKey("fists"))
                {
                    Run.Problem(PlaytestSeverity.Bug, "hero-anim: a bench scene (moveTest, weaponsTest, weaponLooks), the entity wallSlab or the weapon fists is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                Application.onBeforeRender += AnimSample;
                try
                {
                    PlayerController.WallJumped += OnMoveKicked;
                    yield return AnimMovesStage();
                    PlayerController.WallJumped -= OnMoveKicked;
                    if (Run.Done) yield break;
                    Run.Progress(0.25f);
                    yield return AnimFistsStage();
                    if (Run.Done) yield break;
                    Run.Progress(0.4f);
                    yield return AnimLooksStage();
                    if (Run.Done) yield break;
                    Run.Progress(0.5f);
                    yield return AnimHurtDeathVictory();
                    if (Run.Done) yield break;
                    AnimSizeReport();
                }
                finally
                {
                    Application.onBeforeRender -= AnimSample;
                    PlayerController.WallJumped -= OnMoveKicked;
                    input.ReleaseAll();
                    reckless = false;
                    SickOfIt.Data.WeaponProgress.Reset();
                }
                Run.Metric("heroAnim.passed", animPassed);
                Run.Metric("heroAnim.failed", animFailed);
                Run.Metric("heroAnim.sizeOff", animSizeOff);
                Run.Metric("heroAnim.states", animStatesTested.Count);
                Run.Progress(1f);
                End(animFailed > 0 || clipsFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator AnimOpenBench(string scene, int level, string what)
            {
                Run.Event("shortcut", $"level {level}'s stage with the scene {scene} loaded directly ({what})");
                LevelRuntime.PendingScene = scene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(level);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(level), 60f, $"the {scene} bench to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                yield return Run.Until(() => GameplayWorld.Instance.PlayerController != null && GameplayWorld.Instance.PlayerController.IsGrounded, 10f, "the hero to land");
                yield return WaitReal(0.5f);
            }

            // ─── Idle, Walk, Jump, Fall, Fire, RunFire, Dash, WallSlide, WallJump ─────────

            IEnumerator AnimMovesStage()
            {
                // Setup shortcut: both moves are his (the flags a purchase and the story would write).
                UnlockFlags.Reset();
                RogueliteManager.Instance.ResetAllRogueliteData();
                UnlockFlags.Set(HeroMoves.DashFlag);
                UnlockFlags.Set(HeroMoves.WallJumpFlag);
                SickOfIt.Data.WeaponProgress.Reset();
                SickOfIt.Data.WeaponUnlocks.Reset();
                yield return AnimOpenBench(MovesScene, MovesLevel, "dash and wall-jump unlocked");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                var pc = world.PlayerController;
                var body = pc.GetComponent<Rigidbody2D>();
                AnimCheck("both moves unlocked", pc.DashUnlocked && pc.WallJumpUnlocked, $"dash {pc.DashUnlocked}, wall-jump {pc.WallJumpUnlocked}");

                // Idle: standing still.
                var idle = AnimArm("Idle", () => AnimPlain(pc) && pc.IsGrounded && Mathf.Abs(body.linearVelocity.x) < 0.1f);
                yield return AnimWait(2f, idle);
                AnimJudge(idle, "standing still");

                // Walk.
                yield return MovesWalkTo(-6f);
                var walk = AnimArm("Walk", () => AnimPlain(pc) && pc.IsGrounded && !pc.IsRunFiring && Mathf.Abs(body.linearVelocity.x) > 1f);
                input.SetKey(Key.D, true);
                yield return AnimWait(1.5f, walk);
                input.SetKey(Key.D, false);
                AnimJudge(walk, "walking");
                yield return MovesWalkTo(-6f);

                // Jump and Fall.
                var jump = AnimArm("Jump", () => AnimPlain(pc) && !pc.IsGrounded && body.linearVelocity.y > 0.5f);
                var fall = AnimArm("Fall", () => AnimPlain(pc) && !pc.IsGrounded && body.linearVelocity.y < -0.5f);
                input.SetKey(Key.Space, true);
                yield return AnimWait(3f, jump, fall);
                input.SetKey(Key.Space, false);
                AnimJudge(jump, "going up");
                AnimJudge(fall, "coming down");
                yield return Run.Until(() => pc.IsGrounded, 6f, "landing");
                yield return WaitReal(0.4f);

                // Fire: shooting standing still.
                var fire = AnimArm("Fire", () => AnimPlain(pc) && pc.IsGrounded && !pc.IsRunFiring && Mathf.Abs(body.linearVelocity.x) < 0.1f && body.linearVelocity.y > -0.1f
                    && pc.AimDirection.x > 0.5f);
                input.SetAim(Vector2.right);
                yield return AnimWait(2f, fire);
                input.SetAim(Vector2.zero);
                AnimJudge(fire, "firing standing");
                yield return WaitReal(0.4f);

                // RunFire: walking while shooting.
                var runFire = AnimArm("RunFire", () => pc.IsRunFiring);
                input.SetAim(Vector2.right);
                input.SetKey(Key.D, true);
                yield return AnimWait(1.5f, runFire);
                input.SetKey(Key.D, false);
                input.SetAim(Vector2.zero);
                AnimJudge(runFire, "walking and shooting");
                yield return MovesWalkTo(-6f);

                // Dash.
                yield return Run.Until(() => pc.DashCooldownLeft <= 0f, 3f, "the dash cooldown");
                var dash = AnimArm("Dash", () => pc.MovePose == "Dash");
                yield return PressDash(1);
                yield return AnimWait(0.8f, dash);
                AnimJudge(dash, "Shift");
                yield return WaitReal(0.8f);

                // WallSlide and WallJump: climb the shaft with real input.
                yield return MovesWalkTo(-4.5f);
                world.Emit("mv:walls");
                yield return WaitReal(0.8f);
                var slide = AnimArm("WallSlide", () => pc.MovePose == "WallSlide");
                var kick = AnimArm("WallJump", () => pc.MovePose == "WallJump");
                var hero = world.Player;
                int dir = 1, seenKicks = kickCount, kicksFrom = kickCount;
                float startY = hero.Position.y, lastPress = -1f, downAt = -1f;
                bool spaceDown = false;
                float giveUp = Time.realtimeSinceStartup + 30f;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp && hero.IsAlive && !(slide.Seen && kick.Seen))
                {
                    input.SetKey(Key.D, dir > 0);
                    input.SetKey(Key.A, dir < 0);
                    float now = Time.realtimeSinceStartup;
                    bool want = (pc.IsGrounded || pc.WallSide != 0) && now - lastPress > 0.3f;
                    if (want && !spaceDown)
                    {
                        input.SetKey(Key.Space, true);
                        spaceDown = true;
                        downAt = now;
                        lastPress = now;
                    }
                    else if (spaceDown && now - downAt > 0.08f)
                    {
                        input.SetKey(Key.Space, false);
                        spaceDown = false;
                    }
                    if (kickCount > seenKicks)
                    {
                        seenKicks = kickCount;
                        dir = -dir;
                    }
                    yield return null;
                }
                input.SetKey(Key.Space, false);
                input.SetKey(Key.D, false);
                input.SetKey(Key.A, false);
                AnimJudge(slide, "clinging to a wall");
                AnimJudge(kick, "kicking off it");
                Run.Event("anim", $"the shaft: {kickCount - kicksFrom} kicks, rose {hero.Position.y - startY:0.00}");
            }

            // ─── The fists: locomotion, three punches, the swaps ──────────────────────────

            IEnumerator AnimFistsStage()
            {
                SickOfIt.Data.WeaponProgress.Reset();
                SickOfIt.Data.WeaponUnlocks.Reset();
                yield return AnimOpenBench(WeaponsScene, WeaponsLevel, "weapons reset");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                var weapons = world.Weapons;
                var pc = world.PlayerController;
                var body = pc.GetComponent<Rigidbody2D>();

                world.Emit("wpn:give");
                yield return Run.Until(() => weapons.Has("fists"), 6f, "fists given");
                var equip = AnimArm("EquipFists", () => HeroClipOnScreen(out _) == "EquipFists" && pc.SwapPose == "EquipFists");
                yield return Tap(Key.Q);
                yield return AnimWait(1.5f, equip);
                AnimJudge(equip, "Q to the fists");
                AnimCheck("fists equipped", weapons.CurrentId == "fists", $"equipped {weapons.CurrentId}");
                yield return WaitReal(1.4f);

                var idle = AnimArm("UnarmedIdle", () => pc.UnarmedPose == "UnarmedIdle" && HeroClipOnScreen(out _) == "UnarmedIdle");
                yield return AnimWait(2f, idle);
                AnimJudge(idle, "standing with the fists");

                var walk = AnimArm("UnarmedWalk", () => pc.UnarmedPose == "UnarmedWalk" && HeroClipOnScreen(out _) == "UnarmedWalk");
                input.SetKey(Key.A, true);
                yield return AnimWait(1.5f, walk);
                input.SetKey(Key.A, false);
                AnimJudge(walk, "walking with the fists");
                yield return WaitReal(0.4f);

                var jump = AnimArm("UnarmedJump", () => pc.UnarmedPose == "UnarmedJump" && HeroClipOnScreen(out _) == "UnarmedJump");
                var fall = AnimArm("UnarmedFall", () => pc.UnarmedPose == "UnarmedFall" && HeroClipOnScreen(out _) == "UnarmedFall");
                input.SetKey(Key.Space, true);
                yield return AnimWait(3f, jump, fall);
                input.SetKey(Key.Space, false);
                AnimJudge(jump, "up with the fists");
                AnimJudge(fall, "down with the fists");
                yield return Run.Until(() => pc.IsGrounded, 6f, "landing");

                // Punches: an imp in front of him until all three combo steps have swung.
                var punches = new AnimExpect[3];
                for (int i = 0; i < 3; i++)
                {
                    string state = "Punch" + (i + 1);
                    punches[i] = AnimArm(state, () => HeroClipOnScreen(out _) == state);
                }
                for (int round = 0; round < 6 && !Run.Done && !(punches[0].Seen && punches[1].Seen && punches[2].Seen); round++)
                {
                    world.Emit("wpn:bag");
                    yield return Run.Until(() => FindBag(world) != null, 6f, "an imp to punch");
                    var bag = FindBag(world);
                    input.SetAim(Vector2.right);
                    float end = Time.realtimeSinceStartup + 6f;
                    while (!Run.Done && Time.realtimeSinceStartup < end && bag != null && bag.IsAlive)
                        yield return null;
                    input.SetAim(Vector2.zero);
                    yield return WaitReal(0.6f);
                }
                for (int i = 0; i < 3; i++)
                    AnimJudge(punches[i], $"combo step {i + 1}");

                // Q back: the launcher returns to his shoulder.
                var back = AnimArm("EquipLauncher", () => HeroClipOnScreen(out _) == "EquipLauncher" && pc.SwapPose == "EquipLauncher");
                yield return Tap(Key.Q);
                yield return AnimWait(1.5f, back);
                AnimJudge(back, "Q back to the launcher");
                yield return WaitReal(1.4f);
                // The ordinary clips again.
                var plain = AnimArm("Idle", () => AnimPlain(pc) && pc.IsGrounded && Mathf.Abs(body.linearVelocity.x) < 0.1f);
                yield return AnimWait(2f, plain);
                AnimJudge(plain, "launcher back, not the unarmed Idle");
            }

            // ─── The launcher leaving and returning (Floor 998's theft) ────────────────────

            IEnumerator AnimLooksStage()
            {
                SickOfIt.Data.WeaponProgress.Reset();
                SickOfIt.Data.WeaponUnlocks.Reset();
                yield return AnimOpenBench(WeaponLooksScene, WeaponsLevel, "weapons reset");
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                var pc = world.PlayerController;
                var taken = AnimArm("LauncherTaken", () => HeroClipOnScreen(out _) == "LauncherTaken" && pc.SwapPose == "LauncherTaken");
                world.Emit("look:take");
                yield return AnimWait(2f, taken);
                AnimJudge(taken, "the launcher is taken");
                yield return WaitReal(1.8f);
                var back = AnimArm("EquipLauncher", () => HeroClipOnScreen(out _) == "EquipLauncher" && pc.SwapPose == "EquipLauncher");
                world.Emit("look:return");
                yield return AnimWait(2f, back);
                AnimJudge(back, "the launcher comes back");
                yield return WaitReal(1.4f);
            }

            // ─── Hurt, the four deaths, Victory ─────────────────────────────────────────────

            IEnumerator AnimHurtDeathVictory()
            {
                string[] states = { "Hurt", "DeathBurn", "DeathSquash", "DeathSpin", "DeathShrug", "Victory" };
                var armed = new List<AnimExpect>();
                foreach (string state in states)
                {
                    string s = state;
                    armed.Add(AnimArm(s, () => HeroClipOnScreen(out _) == s));
                }
                yield return ToMainMenu();
                if (Run.Done) yield break;
                yield return HeroClipsSteps();
                if (Run.Done) yield break;
                string[] beats = { "the first hit", "death 1-4", "death 1-4", "death 1-4", "death 1-4", "the floor clear" };
                for (int i = 0; i < armed.Count; i++)
                    AnimJudge(armed[i], beats[i]);
            }

            // ─── Every clip is as big on screen as Idle ─────────────────────────────────────

            void AnimSizeReport()
            {
                if (!animSizes.TryGetValue("Idle", out var idle))
                {
                    AnimCheck("size: Idle measured", false, "the Idle sheet was never on screen to measure");
                    return;
                }
                Vector2 baseline = idle.Min;
                Run.Event("animSize", $"Idle: {idle.Min.x:0.000} x {idle.Min.y:0.000} world units ({idle.Frames} frames)");
                bool idleSteady = Mathf.Abs(idle.Max.x - idle.Min.x) <= baseline.x * HeroAnimSizeTolerance && Mathf.Abs(idle.Max.y - idle.Min.y) <= baseline.y * HeroAnimSizeTolerance;
                AnimCheck("size: Idle is steady", idleSteady, $"{idle.Min.x:0.000}-{idle.Max.x:0.000} x {idle.Min.y:0.000}-{idle.Max.y:0.000}");
                foreach (string state in new SortedSet<string>(animStatesTested))
                {
                    string sheet = AnimSheetOf(state);
                    // A state with its own art is named like its sheet in the sprite names; a stand-in shows its stand-in sheet's cell.
                    string key = animSizes.ContainsKey(state) ? state : sheet;
                    if (key == null || !animSizes.TryGetValue(key, out var rec))
                    {
                        Run.Event("check", $"hero-anim size {state}: not measured (its sheet {key ?? "?"} was never on screen)");
                        continue;
                    }
                    float dx = Mathf.Max(Mathf.Abs(rec.Min.x - baseline.x), Mathf.Abs(rec.Max.x - baseline.x)) / baseline.x;
                    float dy = Mathf.Max(Mathf.Abs(rec.Min.y - baseline.y), Mathf.Abs(rec.Max.y - baseline.y)) / baseline.y;
                    bool same = dx <= HeroAnimSizeTolerance && dy <= HeroAnimSizeTolerance;
                    string detail = $"{state} ({key} sheet, {rec.Frames} frames) {rec.Min.x:0.000} x {rec.Min.y:0.000} (max {rec.Max.x:0.000} x {rec.Max.y:0.000}) vs Idle {baseline.x:0.000} x {baseline.y:0.000}: width {dx * 100f:0.0}% height {dy * 100f:0.0}% off";
                    if (same)
                    {
                        Run.Event("animSize", detail);
                        Run.Event("check", $"hero-anim size {state}: ok ({detail})");
                    }
                    else
                    {
                        animSizeOff++;
                        Run.Event("animSize", "DIFFERS: " + detail);
                        Run.Event("check", $"hero-anim size {state}: DIFFERS ({detail})");
                        Run.Problem(PlaytestSeverity.Warning, $"hero-anim size {state}: the clip is not Idle's size on screen: {detail}");
                    }
                }
            }
        }
    }
}
