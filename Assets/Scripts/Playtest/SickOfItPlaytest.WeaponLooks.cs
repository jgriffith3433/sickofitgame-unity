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
        /// Weapon grips and swaps (content/weapons <c>hands</c>, art/hero.json): on the bench <c>weaponLooks</c> (Level 1's stage, setup shortcut)
        /// the bot cycles the weapons with Q and fires each through real input. Every weapon has its own animation set: switching to it plays its
        /// equip clip, standing he shows its idle sheet, firing or swinging its attack sheet (each a frame of that weapon's own state, never another
        /// weapon's sheet and never a drawn prop), and the launcher leaving and coming back play LauncherTaken and EquipLauncher.
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

            /// <summary>Samples every frame until the hero's sprite is a frame of one of 'states' (true) or the time runs out.</summary>
            IEnumerator LooksWaitState(string[] states, float seconds, System.Action<bool, string> result)
            {
                var pc = GameplayWorld.Instance.PlayerController;
                string seen = null;
                float until = Time.realtimeSinceStartup + seconds;
                while (!Run.Done && Time.realtimeSinceStartup < until && seen == null)
                {
                    foreach (var s in states)
                        if (!string.IsNullOrEmpty(s) && pc.ShowsState(s, out _))
                            seen = s;
                    if (seen == null)
                        yield return null;
                }
                pc.ShowsState("", out string sprite);
                result(seen != null, seen ?? sprite ?? "none");
            }

            IEnumerator WeaponLooksSteps()
            {
                var world = GameplayWorld.Instance;
                var weapons = world.Weapons;
                var pc = world.PlayerController;
                yield return WaitReal(0.6f);
                yield return Run.Until(() => pc.IsGrounded, 6f, "the hero on the ground");
                var sheets = new Dictionary<string, string>();

                // Each weapon: its idle sheet standing, then its attack sheet while firing (real input), each its own state.
                IEnumerator ShowWeapon(string id)
                {
                    var hands = pc.CurrentHands;
                    LooksCheck($"{id} has a grip", hands != null, hands != null ? $"idle {hands.idle}, attack {string.Join("/", hands.attack)}" : "no hands in its JSON");
                    if (hands == null)
                        yield break;
                    yield return WaitReal(0.6f);
                    string idleState = hands.animator ? "Idle" : hands.idle;
                    bool idleSeen = false; string idleSprite = null;
                    yield return LooksWaitState(new[] { idleState }, 2.5f, (s, sprite) => { idleSeen = s; idleSprite = sprite; });
                    LooksCheck($"{id} standing shows {idleState}", idleSeen, $"sprite {idleSprite}");
                    input.SetAim(new Vector2(-1f, 0f));
                    var attackStates = hands.animator ? new[] { "Fire" } : hands.attack;
                    bool attackSeen = false; string attackSprite = null;
                    yield return LooksWaitState(attackStates, 2.5f, (s, sprite) => { attackSeen = s; attackSprite = sprite; });
                    input.SetAim(Vector2.zero);
                    LooksCheck($"{id} attacking shows its attack", attackSeen, $"sprite {attackSprite} (wanted {string.Join("/", attackStates)})");
                    sheets[id] = idleState;
                    Run.Event("weapon-look", $"{id}: idle {idleState} seen {idleSeen}, attack seen {attackSeen} ({attackSprite})");
                    yield return WaitReal(0.5f);
                }

                yield return ShowWeapon(PlayerWeapons.Launcher);
                world.Emit("look:all");
                yield return Run.Until(() => weapons.Has("slagLadle") && weapons.Has("fists"), 6f, "the weapons given");

                string[] order = { "fists", "spitfire", "ricochetPistol", "chainWhip", "shotgun", "boomerangHalo", "slagLadle" };

                // The found weapons' grip sheets aren't loaded at boot (beta v246's out-of-memory abort, "Halting program."):
                // they come in once the weapons are in his hands (LazySheets).
                var lazyIdles = new List<string>();
                foreach (string id in order)
                {
                    var hands = GameContent.Current.Weapons.TryGetValue(id, out var def) ? def.hands.Built(def) : null;
                    string sheet = hands != null ? SickOfIt.Core.Addressables.LazySheets.SheetOf(hands.idle) : null;
                    if (sheet != null)
                        lazyIdles.Add(sheet);
                }
                float askedAt = Time.realtimeSinceStartup;
                while (!Run.Done && Time.realtimeSinceStartup - askedAt < 20f && lazyIdles.Exists(SickOfIt.Core.Addressables.LazySheets.Loading))
                    yield return null;
                int loadedIdles = lazyIdles.FindAll(s => SickOfIt.Core.Addressables.LazySheets.Sprites(s) != null).Count;
                LooksCheck("grip sheets loaded on demand, not at boot", lazyIdles.Count > 0 && loadedIdles == lazyIdles.Count,
                    $"{loadedIdles} of {lazyIdles.Count} found weapons' idle sheets in {Time.realtimeSinceStartup - askedAt:0.0} s");

                foreach (string id in order)
                {
                    if (Run.Done)
                        yield break;
                    yield return Tap(Key.Q);
                    yield return Run.Until(() => weapons.CurrentId == id, 4f, "Q to equip " + id);
                    string equip = pc.CurrentHands?.equip;
                    bool seen = false; string sprite = null;
                    yield return LooksWaitState(new[] { equip }, 1.2f, (s, sp) => { seen = s; sprite = sp; });
                    LooksCheck($"swap: {id} equip clip", seen && pc.SwapPose == equip, $"{equip} on screen {seen} (sprite {sprite}), swap pose {pc.SwapPose ?? "none"}");
                    yield return WaitReal(1.2f);
                    yield return ShowWeapon(id);
                }

                var distinct = new HashSet<string>(sheets.Values);
                LooksCheck("eight grips, eight idle sheets", distinct.Count == 8 && sheets.Count == 8, $"{distinct.Count} distinct idle states of {sheets.Count} weapons");

                // The launcher returning to his shoulder (cycling back to it).
                yield return Tap(Key.Q);
                yield return Run.Until(() => weapons.CurrentId == PlayerWeapons.Launcher, 4f, "Q back to the launcher");
                bool back = false; string backSprite = null;
                yield return LooksWaitState(new[] { "EquipLauncher" }, 1.2f, (s, sp) => { back = s; backSprite = sp; });
                LooksCheck("swap: launcher returning", back, $"EquipLauncher on screen {back} (sprite {backSprite})");
                yield return WaitReal(1.2f);

                // The launcher leaving him (Floor 998's theft): taken from his hands while he holds it.
                world.Emit("look:take");
                bool gone = false; string goneSprite = null;
                yield return LooksWaitState(new[] { "LauncherTaken" }, 1.5f, (s, sp) => { gone = s; goneSprite = sp; });
                LooksCheck("swap: launcher leaving", gone && !weapons.Has(PlayerWeapons.Launcher), $"LauncherTaken on screen {gone} (sprite {goneSprite}), launcher held {weapons.Has(PlayerWeapons.Launcher)}");
                yield return WaitReal(1.8f);

                world.Emit("look:return");
                bool returned = false; string retSprite = null;
                yield return LooksWaitState(new[] { "EquipLauncher" }, 1.5f, (s, sp) => { returned = s; retSprite = sp; });
                LooksCheck("swap: launcher back after the theft", returned && weapons.CurrentId == PlayerWeapons.Launcher, $"EquipLauncher on screen {returned} (sprite {retSprite}), equipped {weapons.CurrentId}");
                Run.Metric("weaponLooks.swaps", pc.SwapsPlayed);
                yield return WaitReal(1.2f);
            }
        }
    }
}
