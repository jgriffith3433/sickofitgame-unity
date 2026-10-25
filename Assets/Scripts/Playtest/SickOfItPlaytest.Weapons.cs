using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;
using SickOfIt.Data;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>The weapons scenario: the scene content/scenes/weaponsTest.json on Level 1's stage (setup shortcut).</summary>
        public const string WeaponsId = "weapons";
        public const string WeaponsScene = "weaponsTest";
        public const int WeaponsLevel = 1;
        const float WeaponsSeconds = 120f;

        sealed partial class Session
        {
            int weaponsPassed, weaponsFailed;
            int swingsSeen, hitsSeen;

            void WeaponCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    weaponsPassed++;
                    Run.Event("check", $"weapons {beat}: ok ({detail})");
                }
                else
                {
                    weaponsFailed++;
                    Run.Event("check", $"weapons {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"weapons {beat}: {detail}");
                }
            }

            void WeaponsTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                switch (e.Kind)
                {
                    case "weapon":
                        Run.Event("weapon", $"equipped {e.Subject} ({e.Detail}), ammo {e.Value}");
                        break;
                    case "weaponGiven":
                        Run.Event("weapon", $"given {e.Subject} ({e.Value} held{(string.IsNullOrEmpty(e.Detail) ? "" : ", " + e.Detail)})");
                        break;
                    case "weaponTaken":
                        Run.Event("weapon", $"taken {e.Subject} ({e.Value} held)");
                        break;
                    case "swing":
                        swingsSeen++;
                        Run.Event("weapon", $"swing {e.Subject}: {e.Detail} (step {e.Value})");
                        break;
                    case "weaponHit":
                        hitsSeen += e.Value;
                        Run.Event("weapon", $"{e.Subject} {e.Detail} hit {e.Value} enemy(ies)");
                        break;
                }
            }

            IEnumerator WeaponsClick(Rect rect)
            {
                input.MoveMouse(rect.center);
                yield return WaitReal(0.25f);
                input.SetMouseButton(true);
                yield return null;
                yield return null;
                input.SetMouseButton(false);
                yield return WaitReal(0.25f);
            }

            public IEnumerator WeaponsRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(WeaponsScene) || !content.Weapons.ContainsKey(PlayerWeapons.Launcher))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"weapons: the scene \"{WeaponsScene}\" or the weapon \"{PlayerWeapons.Launcher}\" is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // Setup shortcut: a hero who holds only the launcher, as a new run does.
                WeaponProgress.Reset();
                Run.Event("shortcut", $"weapons reset; level {WeaponsLevel}'s stage with the scene {WeaponsScene} loaded directly");
                LevelRuntime.PendingScene = WeaponsScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(WeaponsLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(WeaponsLevel), 60f, "the weapons bench to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                GameplayTelemetry.Recorded += WeaponsTelemetry;
                try
                {
                    yield return WeaponsSteps();
                }
                finally
                {
                    GameplayTelemetry.Recorded -= WeaponsTelemetry;
                    input.ReleaseAll();
                    WeaponProgress.Reset();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("weapons.passed", weaponsPassed);
                Run.Metric("weapons.failed", weaponsFailed);
                Run.Metric("weapons.swings", swingsSeen);
                Run.Progress(1f);
                End(weaponsFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator WeaponsSteps()
            {
                var world = GameplayWorld.Instance;
                var weapons = world.Weapons;
                var controller = world.PlayerController;
                var hud = world.Hud;
                var guide = world.AimGuide;
                var clock = GameClock.Instance;
                var aim = new Vector2(1f, 0f);

                // 1. The launcher is the default and behaves as before.
                WeaponCheck("launcher is the default", weapons != null && weapons.CurrentId == PlayerWeapons.Launcher && weapons.HeldCount == 1,
                    $"equipped {weapons?.CurrentId}, {weapons?.HeldCount} held");
                yield return WaitReal(0.4f);
                WeaponCheck("hud shows the launcher", hud.WeaponButtonShown && hud.WeaponText.ToUpperInvariant().Contains("ROCKET LAUNCHER"),
                    $"weapon button {(hud.WeaponButtonShown ? "shown" : "NOT shown")}: \"{hud.WeaponText}\"");
                int volleys0 = controller.VolleysFired;
                float t0 = clock.Time;
                input.SetAim(aim);
                yield return WaitReal(0.5f);
                float launcherGuide = guide != null && guide.Visible ? guide.Length : 0f;
                var dirs = new List<Vector2>(controller.LastVolley);
                yield return Run.Until(() => clock.Time - t0 >= 3f, 20f, "3 s of the launcher firing");
                input.SetAim(Vector2.zero);
                int fired = controller.VolleysFired - volleys0;
                float span = clock.Time - t0;
                float expected = span / GameDefinition.Instance.player.fireRate;
                WeaponCheck("launcher fire rate unchanged", fired >= expected * 0.7f && fired <= expected * 1.1f + 1f,
                    $"{fired} volleys in {span:0.0} s; player.json fireRate {GameDefinition.Instance.player.fireRate} allows about {expected:0.#}");
                bool aimed = dirs.Count == 1 && Vector2.Angle(dirs[0], aim) < 1f;
                WeaponCheck("launcher aim unchanged", aimed, dirs.Count == 0 ? "no volley seen" : $"first volley at {Mathf.Atan2(dirs[0].y, dirs[0].x) * Mathf.Rad2Deg:0.0}° for an aim at 0°");
                WeaponCheck("launcher aim guide", launcherGuide > 0f, $"guide length {launcherGuide:0.00}");
                Run.Progress(0.2f);

                // 2. A level action gives a weapon (kept in the bag, not equipped).
                world.Emit("wpn:give");
                yield return Run.Until(() => weapons.Has("fists"), 6f, "giveWeapon to put fists in his hands");
                WeaponCheck("giveWeapon", weapons.Has("fists") && weapons.CurrentId == PlayerWeapons.Launcher && weapons.HeldCount == 2,
                    $"holds {string.Join(", ", weapons.Held)}; equipped {weapons.CurrentId}");
                yield return WaitReal(0.4f);

                // 3. Switch on the keyboard: Q. The HUD shows it and the guide follows.
                yield return Tap(Key.Q);
                yield return WaitReal(0.4f);
                float switched1 = clock.Time;
                WeaponCheck("keyboard switch", weapons.CurrentId == "fists", $"equipped {weapons.CurrentId} after Q");
                WeaponCheck("hud shows fists", hud.WeaponText.ToUpperInvariant().Contains("FISTS"), $"the weapon button reads \"{hud.WeaponText}\"");
                input.SetAim(aim);
                yield return WaitReal(0.3f);
                float fistGuide = guide != null && guide.Visible ? guide.Length : 0f;
                input.SetAim(Vector2.zero);
                WeaponCheck("aim guide follows the weapon", fistGuide > 0f && launcherGuide > 0f && fistGuide < launcherGuide * 0.8f,
                    $"fists' guide {fistGuide:0.00} against the launcher's {launcherGuide:0.00}");
                Run.Progress(0.4f);

                // 4. Fists punch an enemy, with a visible swing.
                world.Emit("wpn:bag");
                yield return Run.Until(() => FindBag(world) != null, 6f, "an imp to punch");
                var bag = FindBag(world);
                int hitsBefore = hitsSeen, swingsBefore = swingsSeen;
                bool swingShown = false;
                int volleysBefore = controller.VolleysFired;
                input.SetAim(aim);
                float punchEnd = Time.realtimeSinceStartup + 8f;
                while (!Run.Done && Time.realtimeSinceStartup < punchEnd && bag != null && bag.IsAlive)
                {
                    if (world.SwingFx != null && world.SwingFx.Visible)
                        swingShown = true;
                    yield return null;
                }
                input.SetAim(Vector2.zero);
                if (Run.Done) yield break;
                WeaponCheck("fists swing", swingsSeen > swingsBefore && controller.VolleysFired > volleysBefore, $"{swingsSeen - swingsBefore} swing(s)");
                WeaponCheck("swing is visible", swingShown, swingShown ? $"the crescent and fist drew (peak alpha {world.SwingFx.PeakAlpha:0.00})" : "no swing was drawn");
                WeaponCheck("fists hit", hitsSeen > hitsBefore, $"{hitsSeen - hitsBefore} enemy hit(s)");
                WeaponCheck("fists kill an imp", bag == null || !bag.IsAlive, bag == null || !bag.IsAlive ? "the imp died to punches" : "the imp is still alive");
                Run.Progress(0.6f);

                // 5. Rewind restores the equipped weapon: Q back to the launcher, wait, then rewind to before that.
                yield return WaitReal(1.5f);
                float before = clock.Time;
                yield return Tap(Key.Q);
                yield return WaitReal(0.3f);
                float switched2 = clock.Time;
                WeaponCheck("switch back", weapons.CurrentId == PlayerWeapons.Launcher, $"equipped {weapons.CurrentId}");
                yield return WaitReal(1.5f);
                var budget = RewindBudget.Instance;
                if (budget != null)
                    budget.Add(5f);
                Run.Event("rewind", $"hold R back to before the switch (switched at {switched2:0.0}, now {clock.Time:0.0})");
                input.SetKey(Key.R, true);
                float giveUp = Time.realtimeSinceStartup + 5f;
                while (!Run.Done && Time.realtimeSinceStartup < giveUp && clock.Time > switched2 - 0.5f)
                    yield return null;
                input.SetKey(Key.R, false);
                yield return WaitReal(0.3f);
                bool wasRewound = clock.Time < switched2;
                WeaponCheck("rewind restores the weapon", wasRewound && weapons.CurrentId == "fists",
                    $"clock {clock.Time:0.0} (switch at {switched2:0.0}): equipped {weapons.CurrentId}, hud \"{hud.WeaponText}\"");
                Run.Progress(0.75f);

                // 6. Touch: the HUD's weapon button (the mouse clicks it the way a finger taps it).
                yield return WaitReal(0.8f);
                var rect = hud.WeaponButtonScreenRect;
                WeaponCheck("weapon button on screen", rect.width > 20f && rect.height > 20f && rect.xMin >= 0f && rect.xMax <= Screen.width && rect.yMin >= 0f && rect.yMax <= Screen.height,
                    $"button {rect.width:0}x{rect.height:0} px at {rect.center.x:0},{rect.center.y:0} of a {Screen.width}x{Screen.height} screen");
                string was = weapons.CurrentId;
                yield return WeaponsClick(rect);
                WeaponCheck("button switch", weapons.CurrentId != was, $"{was} -> {weapons.CurrentId} after tapping the HUD button");
                if (weapons.CurrentId != PlayerWeapons.Launcher)
                {
                    yield return WaitReal(0.4f);
                    yield return WeaponsClick(rect);
                }

                // 7. A level action takes the weapon away again.
                world.Emit("wpn:take");
                yield return Run.Until(() => !weapons.Has("fists"), 6f, "takeWeapon to take the fists away");
                yield return WaitReal(0.4f);
                WeaponCheck("takeWeapon", !weapons.Has("fists") && weapons.CurrentId == PlayerWeapons.Launcher && weapons.HeldCount == 1,
                    $"holds {string.Join(", ", weapons.Held)}; equipped {weapons.CurrentId}; hud \"{hud.WeaponText}\"");
            }

            static Entity FindBag(GameplayWorld world)
            {
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && !e.IsPlayer && e.Tags.Contains("bag"))
                        return e;
                }
                return null;
            }
        }
    }
}
