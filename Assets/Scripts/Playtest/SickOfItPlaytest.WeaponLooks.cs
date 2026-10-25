using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay;
using LevelRuntime = SickOfIt.Core.Progression.LevelRuntime;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        /// <summary>
        /// Weapon swaps and looks (art/hero.json launcherAway / equipLauncher / equipFists; weapon JSON <c>look</c>): on the bench
        /// <c>weaponLooks</c> (Level 1's stage, setup shortcut) the bot cycles the weapons with Q and fires each through real input. Switching to the fists must
        /// play EquipFists, the launcher leaving his hands LauncherTaken, and the launcher coming back EquipLauncher (the clip on screen, the prop hidden
        /// meanwhile); each of the seven weapons and the fists must show a prop of its own while held and a flash when fired (signatures all different).
        /// </summary>
        public const string WeaponLooksId = "weapon-looks";
        public const string WeaponLooksScene = "weaponLooks";
        const float WeaponLooksSeconds = 150f;

        sealed partial class Session
        {
            int looksPassed, looksFailed;

            void LooksCheck(string beat, bool ok, string detail)
            {
                if (ok)
                {
                    looksPassed++;
                    Run.Event("check", $"weapon-looks {beat}: ok ({detail})");
                }
                else
                {
                    looksFailed++;
                    Run.Event("check", $"weapon-looks {beat}: FAILED ({detail})");
                    Run.Problem(PlaytestSeverity.Bug, $"weapon-looks {beat}: {detail}");
                }
            }

            public IEnumerator WeaponLooksRun()
            {
                var content = GameContent.Current;
                if (content == null || !content.Scenes.ContainsKey(WeaponLooksScene) || !content.Weapons.ContainsKey("slagLadle"))
                {
                    Run.Problem(PlaytestSeverity.Bug, $"weapon-looks: the scene \"{WeaponLooksScene}\" or a weapon is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }
                SickOfIt.Data.WeaponProgress.Reset();
                SickOfIt.Data.WeaponUnlocks.Reset();
                Run.Event("shortcut", $"weapons reset; level {WeaponsLevel}'s stage with the scene {WeaponLooksScene} loaded directly");
                LevelRuntime.PendingScene = WeaponLooksScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(WeaponsLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(WeaponsLevel), 60f, "the weapon looks bench to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done)
                    yield break;
                StopBot();
                try
                {
                    yield return WeaponLooksSteps();
                }
                finally
                {
                    input.ReleaseAll();
                    SickOfIt.Data.WeaponProgress.Reset();
                }
                if (Run.Done)
                    yield break;
                Run.Metric("weaponLooks.passed", looksPassed);
                Run.Metric("weaponLooks.failed", looksFailed);
                Run.Progress(1f);
                End(looksFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            /// <summary>Samples every frame until the hero's sprite is the swap clip 'state' (true) or the time runs out.</summary>
            IEnumerator LooksWaitClip(string state, float seconds, System.Action<bool, bool> result)
            {
                var pc = GameplayWorld.Instance.PlayerController;
                var prop = HeldWeaponProp.For(pc);
                bool seen = false, propHidden = false;
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < until && !seen)
                {
                    if (HeroClipOnScreen(out _) == state && pc.SwapPose == state)
                        seen = true;
                    else
                        yield return null;
                }
                if (seen)
                {
                    yield return null; // the prop hides in LateUpdate, after the clip starts
                    yield return null;
                    propHidden = prop == null || prop.Shown == null;
                }
                result(seen, propHidden);
            }

            IEnumerator WeaponLooksSteps()
            {
                var world = GameplayWorld.Instance;
                var weapons = world.Weapons;
                var pc = world.PlayerController;
                var prop = HeldWeaponProp.For(pc);
                yield return WaitReal(0.6f);
                LooksCheck("prop component on the hero", prop != null, prop != null ? "HeldWeaponProp present" : "missing");
                if (prop == null)
                    yield break;
                var signatures = new Dictionary<string, string>();
                yield return Run.Until(() => pc.IsGrounded, 6f, "the hero on the ground");

                // Each weapon: take it up with Q (real input), then fire it and watch the prop's flash.
                IEnumerator ShowWeapon(string id)
                {
                    yield return WaitReal(0.7f);
                    string held = prop.Shown;
                    string sig = prop.Signature;
                    int usesBefore = prop.Uses;
                    bool flash = false;
                    input.SetAim(new Vector2(1f, 0f));
                    float until = Time.realtimeSinceStartup + 1.4f;
                    while (!Run.Done && Time.realtimeSinceStartup < until)
                    {
                        if (prop.FlashOn)
                            flash = true;
                        yield return null;
                    }
                    input.SetAim(Vector2.zero);
                    bool fired = prop.Uses > usesBefore;
                    signatures[id] = sig;
                    Run.Event("weapon-look", $"{id}: prop shown for {held ?? "none"}, fired {prop.Uses - usesBefore} times, flash seen {flash}, look {sig}");
                    LooksCheck($"{id} held and fired", held == id && sig != null && fired && flash, $"shown {held ?? "none"}, uses +{prop.Uses - usesBefore}, flash {flash}");
                }

                yield return ShowWeapon(PlayerWeapons.Launcher);
                world.Emit("look:all");
                yield return Run.Until(() => weapons.Has("slagLadle") && weapons.Has("fists"), 6f, "the weapons given");

                string[] order = { "fists", "spitfire", "ricochetPistol", "chainWhip", "shotgun", "boomerangHalo", "slagLadle" };
                foreach (string id in order)
                {
                    if (Run.Done)
                        yield break;
                    yield return Tap(Key.Q);
                    yield return Run.Until(() => weapons.CurrentId == id, 4f, "Q to equip " + id);
                    if (id == "fists")
                    {
                        bool seen = false, hidden = false;
                        yield return LooksWaitClip("EquipFists", 1.2f, (s, h) => { seen = s; hidden = h; });
                        Run.Event("weapon-swap", $"EquipFists on screen: {seen}");
                        LooksCheck("swap: fists equipping", seen && hidden, $"EquipFists on screen {seen}, prop hidden meanwhile {hidden}");
                        yield return WaitReal(1.2f);
                    }
                    else
                    {
                        LooksCheck($"swap: {id} has no swap clip", pc.SwapPose == null, $"swap pose {pc.SwapPose ?? "none"}");
                    }
                    yield return ShowWeapon(id);
                }

                // Different looks: every weapon's prop has its own shape and colours.
                var seenSigs = new HashSet<string>();
                bool allDistinct = true;
                foreach (var kv in signatures)
                    if (string.IsNullOrEmpty(kv.Value) || !seenSigs.Add(kv.Value))
                        allDistinct = false;
                LooksCheck("eight different looks", allDistinct && signatures.Count == 8, $"{seenSigs.Count} distinct looks of {signatures.Count} weapons");

                // The launcher returning to his shoulder (cycling back to it).
                yield return Tap(Key.Q);
                yield return Run.Until(() => weapons.CurrentId == PlayerWeapons.Launcher, 4f, "Q back to the launcher");
                bool back = false, backHidden = false;
                yield return LooksWaitClip("EquipLauncher", 1.2f, (s, h) => { back = s; backHidden = h; });
                Run.Event("weapon-swap", $"EquipLauncher on screen: {back}");
                LooksCheck("swap: launcher returning", back && backHidden, $"EquipLauncher on screen {back}, prop hidden meanwhile {backHidden}");
                yield return WaitReal(1.2f);

                // The launcher leaving him (Floor 998's theft): taken from his hands while he holds it.
                world.Emit("look:take");
                bool gone = false, goneHidden = false;
                yield return LooksWaitClip("LauncherTaken", 1.5f, (s, h) => { gone = s; goneHidden = h; });
                Run.Event("weapon-swap", $"LauncherTaken on screen: {gone}");
                LooksCheck("swap: launcher leaving", gone && goneHidden && !weapons.Has(PlayerWeapons.Launcher), $"LauncherTaken on screen {gone}, prop hidden meanwhile {goneHidden}, launcher held {weapons.Has(PlayerWeapons.Launcher)}");
                yield return WaitReal(1.8f);

                world.Emit("look:return");
                bool returned = false, returnedHidden = false;
                yield return LooksWaitClip("EquipLauncher", 1.5f, (s, h) => { returned = s; returnedHidden = h; });
                LooksCheck("swap: launcher back after the theft", returned && returnedHidden && weapons.CurrentId == PlayerWeapons.Launcher, $"EquipLauncher on screen {returned}, equipped {weapons.CurrentId}");
                Run.Metric("weaponLooks.swaps", pc.SwapsPlayed);
                yield return WaitReal(1.2f);
                LooksCheck("prop back after the swap", prop.Shown == PlayerWeapons.Launcher, $"prop shows {prop.Shown ?? "none"}");
            }
        }
    }
}
