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

        /// <summary>The weapon-drops scenario: Floor 998's theft, the fists floors, then every weapon found on its floor, kept after a death, and what each one does.</summary>
        public const string WeaponDropsId = "weapon-drops";
        public const string WeaponArenaScene = "weaponArena";
        const float WeaponDropsSeconds = 1500f;
        /// <summary>The floors that drop a weapon and what each carries, in the order the story gives them.</summary>
        static readonly string[][] WeaponDropFloors =
        {
            new[] { "Floor994", PlayerWeapons.Launcher },
            new[] { "Floor993", "spitfire" },
            new[] { "Floor992", "ricochetPistol" },
            new[] { "Floor991", "chainWhip" },
            new[] { "Floor954", "shotgun" },
            new[] { "Floor951", "boomerangHalo" },
        };

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
                WeaponUnlocks.Reset();
                Run.Event("shortcut", $"weapons reset (nothing unlocked); level {WeaponsLevel}'s stage with the scene {WeaponsScene} loaded directly");
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
                WeaponCheck("hud plays the switch animation", hud.WeaponSwaps >= 1, $"{hud.WeaponSwaps} switch animation(s) played, the last {hud.WeaponSwapAge:0.0} s ago");
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


            // ─── weapon-drops ────────────────────────────────────────────

            readonly Dictionary<string, string> dropUnlocks = new Dictionary<string, string>();
            readonly HashSet<string> dropBanners = new HashSet<string>(), dropSubtitles = new HashSet<string>();
            bool tookLauncher, thiefSeen, launcherSeenInHand;
            int arenaBounces, dropLevel;
            float tookAt;

            void DropsTelemetry(GameplayTelemetry log, GameplayTelemetry.Event e)
            {
                WeaponsTelemetry(log, e);
                switch (e.Kind)
                {
                    case "weaponTaken":
                        if (e.Subject == PlayerWeapons.Launcher)
                        {
                            tookLauncher = true;
                            tookAt = e.Time;
                        }
                        break;
                    case "weaponUnlocked":
                        dropUnlocks[e.Subject] = e.Detail;
                        Run.Event("weapon", $"unlocked {e.Subject} ({e.Detail}), {e.Value} held");
                        break;
                    case "bounce":
                        if (e.Subject == "ricochetShot")
                            arenaBounces++;
                        break;
                }
            }

            /// <summary>Each frame of a floor: the banner and subtitle on screen, the thief, the launcher in his hand.</summary>
            IEnumerator DropsDuring(float now)
            {
                var world = GameplayWorld.Instance;
                if (world != null)
                {
                    var hud = world.Hud;
                    if (hud != null)
                    {
                        string banner = hud.BannerShown, subtitle = hud.SubtitleShown;
                        if (!string.IsNullOrEmpty(banner) && dropBanners.Add(banner))
                            Run.Event("banner", $"\"{banner}\"");
                        if (!string.IsNullOrEmpty(subtitle) && dropSubtitles.Add(subtitle))
                            Run.Event("line", $"hero: \"{subtitle}\"");
                    }
                    if (world.Weapons != null && world.Weapons.Has(PlayerWeapons.Launcher))
                        launcherSeenInHand = true;
                    if (!thiefSeen)
                    {
                        foreach (var e in world.Live)
                        {
                            if (e != null && e.IsAlive && e.DefId == "launcherThief")
                            {
                                thiefSeen = true;
                                Run.Event("weapon", "the thief imp is on screen with the launcher");
                                break;
                            }
                        }
                    }
                }
                return null;
            }

            bool SawText(HashSet<string> seen, string part)
            {
                foreach (string text in seen)
                {
                    if (text.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            }

            /// <summary>Opens a floor directly (setup shortcut: the save a hero arriving there has) and plays it with the bot until 'until' or its end; one retry after a death.</summary>
            IEnumerator DropsPlay(string levelName, float seconds, System.Func<bool> until, float from, float to)
            {
                dropLevel = GameDefinition.Instance.levels.FindIndex(l => l != null && l.levelName == levelName) + 1;
                if (dropLevel <= 0)
                {
                    WeaponCheck($"{levelName} exists", false, "no such level in game.json");
                    yield break;
                }
                for (int attempt = 1; attempt <= 2 && !Run.Done; attempt++)
                {
                    Prepare(dropLevel, true);
                    yield return OpenLevel(dropLevel, attempt);
                    if (Run.Done) yield break;
                    if (!openedLevel)
                    {
                        WeaponCheck($"{levelName} opens", false, $"never started (the game stayed in {SceneName()})");
                        yield break;
                    }
                    dropBanners.Clear();
                    dropSubtitles.Clear();
                    yield return PlayLevel(dropLevel, seconds, DropsDuring, from, to, 420f, waitOutDeath: true, until: until);
                    if (lastOutcome != "died")
                        yield break;
                    Run.Event("retry", $"{levelName}: died, try {attempt + 1}");
                }
            }

            public IEnumerator WeaponDropsRun()
            {
                var content = GameContent.Current;
                bool missing = content == null || !content.Scenes.ContainsKey(WeaponArenaScene);
                foreach (var pair in WeaponDropFloors)
                    missing |= content == null || !content.Weapons.ContainsKey(pair[1]);
                if (missing)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"weapon-drops: the scene \"{WeaponArenaScene}\" or a weapon in the drop table is missing from the content");
                    End(PlaytestOutcome.Error);
                    yield break;
                }

                // Setup shortcut: a fresh save, nothing unlocked, a fresh run.
                WeaponUnlocks.Reset();
                WeaponProgress.Reset();
                takeShortcuts = false;
                reckless = false;
                preparedFirst = 0;
                Run.Event("shortcut", "weapon-drops: nothing unlocked yet, a fresh run; each floor is opened directly with the save a hero arriving there has");
                GameplayTelemetry.Recorded += DropsTelemetry;
                try
                {
                    yield return WeaponDropsSteps();
                }
                finally
                {
                    GameplayTelemetry.Recorded -= DropsTelemetry;
                    input.ReleaseAll();
                    Time.timeScale = 1f;
                }
                if (Run.Done)
                    yield break;
                Run.Metric("weapons.passed", weaponsPassed);
                Run.Metric("weapons.failed", weaponsFailed);
                Run.Metric("weapons.unlocked", WeaponUnlocks.All().Count);
                Run.Progress(1f);
                End(weaponsFailed > 0 ? PlaytestOutcome.Error : PlaytestOutcome.Done);
            }

            IEnumerator WeaponDropsSteps()
            {
                // 1. Floor 998: the launcher is taken in a scene and he fights with fists.
                hitsSeen = 0;
                tookLauncher = thiefSeen = launcherSeenInHand = false;
                yield return DropsPlay("Floor998", 150f, () => tookLauncher && (hitsSeen >= 2 || GameClock.Instance.Time - tookAt > 75f), 0f, 0.1f);
                if (Run.Done) yield break;
                var world = GameplayWorld.Instance;
                var weapons = world != null ? world.Weapons : null;
                WeaponCheck("998: the launcher is taken", tookLauncher, tookLauncher ? "weaponTaken rocketLauncher" : "no weaponTaken for the launcher on Floor 998");
                WeaponCheck("998: a thief runs off with it", thiefSeen, thiefSeen ? "the launcherThief imp was on screen" : "no launcherThief seen");
                WeaponCheck("998: the banner and his line", SawText(dropBanners, "TAKEN YOUR GUN") && SawText(dropSubtitles, "Take the rocket launcher"),
                    $"banners: {string.Join(" | ", dropBanners)}; lines: {string.Join(" | ", dropSubtitles)}");
                WeaponCheck("998: fists in his hands, no launcher", weapons != null && !weapons.Has(PlayerWeapons.Launcher) && weapons.CurrentId == "fists" && weapons.HeldCount == 1,
                    weapons == null ? "no world" : $"holds {string.Join(", ", weapons.Held)}; equipped {weapons.CurrentId}");
                WeaponCheck("998: the fists land hits", hitsSeen >= 1, $"{hitsSeen} enemy hit(s) with the fists");

                // 2. Floors 997-995: fists only, with brawls to fight.
                foreach (string floor in new[] { "Floor997", "Floor996", "Floor995" })
                {
                    hitsSeen = 0;
                    launcherSeenInHand = false;
                    yield return DropsPlay(floor, 0f, () => hitsSeen >= 2 && GameClock.Instance.Time > 20f, 0.1f, 0.3f);
                    if (Run.Done) yield break;
                    var w = GameplayWorld.Instance != null ? GameplayWorld.Instance.Weapons : null;
                    WeaponCheck($"{floor}: fists only", !launcherSeenInHand && w != null && w.CurrentId == "fists",
                        w == null ? "no world" : $"holds {string.Join(", ", w.Held)}; launcher {(launcherSeenInHand ? "WAS" : "never")} in hand");
                    WeaponCheck($"{floor}: a brawl with the fists", hitsSeen >= 1, $"{hitsSeen} enemy hit(s)");
                }

                // 3. Floors 994-951: every weapon found on its floor, the launcher first.
                int index = 0;
                foreach (var pair in WeaponDropFloors)
                {
                    string floor = pair[0], id = pair[1];
                    var def = GameContent.Current.Weapons[id];
                    float from = 0.3f + 0.4f * index / WeaponDropFloors.Length, to = 0.3f + 0.4f * (index + 1) / WeaponDropFloors.Length;
                    index++;
                    yield return DropsPlay(floor, 0f, () => dropUnlocks.ContainsKey(id) && SawText(dropBanners, def.displayName), from, to);
                    if (Run.Done) yield break;
                    var w = GameplayWorld.Instance != null ? GameplayWorld.Instance.Weapons : null;
                    string how = dropUnlocks.TryGetValue(id, out var detail) ? detail : "never found";
                    WeaponCheck($"{floor}: {def.displayName} found", dropUnlocks.ContainsKey(id), $"unlock {how}");
                    WeaponCheck($"{floor}: {def.displayName} banner", SawText(dropBanners, def.displayName), $"banners: {string.Join(" | ", dropBanners)}");
                    WeaponCheck($"{floor}: his line", string.IsNullOrEmpty(def.heroText) || SawText(dropSubtitles, def.heroText.Substring(0, System.Math.Min(18, def.heroText.Length))),
                        $"lines: {string.Join(" | ", dropSubtitles)}");
                    WeaponCheck($"{floor}: {def.displayName} in his hands", w != null && w.Has(id), w == null ? "no world" : $"holds {string.Join(", ", w.Held)}");
                    if (id == PlayerWeapons.Launcher)
                        WeaponCheck("994: the thief gave it back", thiefSeen, thiefSeen ? "the thief imp was seen" : "no launcherThief seen");
                }

                // 4. Dying doesn't take them: a real death on Floor 999 ends the run and the next one still has them.
                int unlockedCount = WeaponUnlocks.All().Count;
                WeaponCheck("unlocks are saved", unlockedCount >= 5, $"{unlockedCount} unlocked: {string.Join(", ", WeaponUnlocks.All())}");
                reckless = true;
                int level999 = GameDefinition.Instance.levels.FindIndex(l => l != null && l.levelName == "Floor999") + 1;
                Prepare(level999, true);
                yield return OpenLevel(level999, 1);
                if (Run.Done) yield break;
                var runHeld = GameplayWorld.Instance != null ? GameplayWorld.Instance.Weapons : null;
                if (runHeld != null)
                {
                    runHeld.Give("fists", true, 0, false);
                    WeaponCheck("999: arrives with every unlocked weapon", runHeld.HeldCount >= 1 + unlockedCount, $"holds {string.Join(", ", runHeld.Held)}");
                }
                yield return PlayLevel(level999, 0f, DropsDuring, 0.7f, 0.75f, 240f, waitOutDeath: false);
                reckless = false;
                if (Run.Done) yield break;
                WeaponCheck("999: the bot died", lastOutcome == "died", $"outcome {lastOutcome}");
                yield return OpenLevel(level999, 2);
                if (Run.Done) yield break;
                var after = GameplayWorld.Instance != null ? GameplayWorld.Instance.Weapons : null;
                bool all = after != null;
                string heldNow = after != null ? string.Join(", ", after.Held) : "no world";
                if (after != null)
                {
                    foreach (string id in WeaponUnlocks.All())
                        all &= after.Has(id);
                    all &= after.Has(PlayerWeapons.Launcher) && !after.Has("fists");
                }
                WeaponCheck("after dying: the next run still has them", all && unlockedCount >= 5, $"holds {heldNow} (fists, held only for the run, are gone)");
                WeaponCheck("after dying: unlocks kept", WeaponUnlocks.All().Count == unlockedCount, $"{WeaponUnlocks.All().Count} of {unlockedCount} still unlocked");
                Run.Progress(0.75f);

                // 5. Each weapon does something of its own, on the bench (the scene weaponArena on Level 1's stage).
                yield return WeaponBench();
            }

            IEnumerator WeaponBench()
            {
                LevelRuntime.PendingScene = WeaponArenaScene;
                var old = GameplayWorld.Instance;
                var load = LoadLevel(WeaponsLevel);
                if (load != null)
                    yield return load;
                yield return Run.Until(() => GameplayWorld.Instance != old && LevelRunning(WeaponsLevel), 60f, "the weapon bench to start");
                LevelRuntime.PendingScene = null;
                if (Run.Done) yield break;
                StopBot();
                Time.timeScale = Speed;
                var world = GameplayWorld.Instance;
                var weapons = world.Weapons;
                var controller = world.PlayerController;
                var aim = new Vector2(1f, 0f);
                int distinct = 0;

                // Spitfire: a stream, many times the launcher's rate, on ammo that comes back.
                yield return EquipByKey(weapons, PlayerWeapons.Launcher);
                int v0 = controller.VolleysFired;
                input.SetAim(aim);
                yield return WaitReal(1.5f);
                input.SetAim(Vector2.zero);
                int launcherVolleys = controller.VolleysFired - v0;
                yield return EquipByKey(weapons, "spitfire");
                int ammo0 = weapons.CurrentAmmo;
                v0 = controller.VolleysFired;
                input.SetAim(aim);
                yield return WaitReal(1.5f);
                input.SetAim(Vector2.zero);
                int spitVolleys = controller.VolleysFired - v0;
                bool stream = spitVolleys >= 3 * System.Math.Max(1, launcherVolleys) && weapons.CurrentAmmo < ammo0;
                if (stream) distinct++;
                WeaponCheck("spitfire: a stream on ammo", stream, $"{spitVolleys} volleys in 1.5 s against the launcher's {launcherVolleys}; ammo {ammo0} -> {weapons.CurrentAmmo}");

                // Ricochet pistol: the bullet bounces off the ground.
                yield return EquipByKey(weapons, "ricochetPistol");
                arenaBounces = 0;
                input.SetAim(new Vector2(1f, -0.6f).normalized);
                yield return WaitReal(2.2f);
                input.SetAim(Vector2.zero);
                yield return WaitReal(1.0f);
                if (arenaBounces > 0) distinct++;
                WeaponCheck("ricochet pistol: bullets bounce", arenaBounces > 0, $"{arenaBounces} bounce(s) off the ground");

                // Fists against the chain-whip: the same dummy 2.6 away is out of the fists' reach and in the whip's.
                world.Emit("wa:d26");
                yield return Run.Until(() => FindDummy(world) != null, 6f, "a dummy 2.6 away");
                var dummy = FindDummy(world);
                yield return EquipByKey(weapons, "fists");
                int hp0 = dummy != null ? dummy.Health : 0;
                input.SetAim(aim);
                yield return WaitReal(1.5f);
                input.SetAim(Vector2.zero);
                int fistDamage = hp0 - (dummy != null ? dummy.Health : 0);
                yield return EquipByKey(weapons, "chainWhip");
                hp0 = dummy != null ? dummy.Health : 0;
                input.SetAim(aim);
                yield return WaitReal(1.5f);
                input.SetAim(Vector2.zero);
                int whipDamage = hp0 - (dummy != null ? dummy.Health : 0);
                if (fistDamage == 0 && whipDamage > 0) distinct++;
                WeaponCheck("chain-whip: reach", fistDamage == 0 && whipDamage > 0, $"the dummy 2.6 away took {fistDamage} from fists and {whipDamage} from the whip");
                world.Emit("wa:clear");
                yield return WaitReal(0.5f);

                // Shotgun: one pull, five pellets; point blank it hurts, across the room it does nothing.
                world.Emit("wa:d12");
                world.Emit("wa:far");
                yield return Run.Until(() => CountDummies(world) >= 2, 6f, "two dummies");
                var near = FindDummyNear(world, 1.2f);
                var far = FindDummyNear(world, 7.5f);
                yield return EquipByKey(weapons, "shotgun");
                yield return WaitReal(0.4f);
                int nearHp = near != null ? near.Health : 0, farHp = far != null ? far.Health : 0;
                int volley0 = controller.VolleysFired;
                input.SetAim(aim);
                float stop = Time.realtimeSinceStartup + 3f;
                while (!Run.Done && controller.VolleysFired == volley0 && Time.realtimeSinceStartup < stop)
                    yield return null;
                input.SetAim(Vector2.zero);
                int pellets = controller.LastVolley.Count;
                yield return WaitReal(1.2f);
                int nearDamage = nearHp - (near != null ? near.Health : 0), farDamage = farHp - (far != null ? far.Health : 0);
                bool blast = pellets == 5 && nearDamage >= 3 && farDamage == 0;
                if (blast) distinct++;
                WeaponCheck("shotgun: a blast", blast, $"{pellets} pellets in one volley; the dummy 1.2 away took {nearDamage}, the one 7.5 away {farDamage}");
                world.Emit("wa:clear");
                yield return WaitReal(0.5f);

                // Boomerang halo: out past the dummy, back to him, hitting both ways.
                world.Emit("wa:d26");
                yield return Run.Until(() => FindDummy(world) != null, 6f, "a dummy 2.6 away");
                dummy = FindDummy(world);
                yield return EquipByKey(weapons, "boomerangHalo");
                yield return WaitReal(0.4f);
                hp0 = dummy != null ? dummy.Health : 0;
                float maxOut = 0f, lastDistance = 99f;
                bool seen = false;
                int volleyH = controller.VolleysFired;
                input.SetAim(aim);
                float throwEnd = Time.realtimeSinceStartup + 5f;
                while (!Run.Done && Time.realtimeSinceStartup < throwEnd)
                {
                    if (controller.VolleysFired > volleyH)
                        input.SetAim(Vector2.zero);
                    Entity halo = null;
                    foreach (var e in world.Live)
                    {
                        if (e != null && e.IsAlive && e.DefId == "haloShot")
                        {
                            halo = e;
                            break;
                        }
                    }
                    if (halo != null)
                    {
                        seen = true;
                        lastDistance = Vector2.Distance(halo.Position, world.Player.Position);
                        maxOut = Mathf.Max(maxOut, lastDistance);
                    }
                    else if (seen)
                        break;
                    yield return null;
                }
                input.SetAim(Vector2.zero);
                int haloDamage = hp0 - (dummy != null ? dummy.Health : 0);
                bool back = seen && maxOut > 2.8f && lastDistance < 1.6f && haloDamage >= 2;
                if (back) distinct++;
                WeaponCheck("boomerang halo: out and back", back, $"flew {maxOut:0.0} out, caught {lastDistance:0.0} from him, the dummy took {haloDamage} (out and back)");
                world.Emit("wa:clear");

                Run.Metric("weapons.distinct", distinct);
                WeaponCheck("every weapon behaves differently", distinct == 5, $"{distinct} of 5 new weapons showed a behaviour of their own");
            }

            IEnumerator EquipByKey(PlayerWeapons weapons, string id)
            {
                for (int i = 0; i < 10 && !Run.Done && weapons.CurrentId != id; i++)
                {
                    yield return Tap(Key.Q);
                    yield return WaitReal(0.35f);
                }
                yield return WaitReal(0.35f);
            }

            static Entity FindDummy(GameplayWorld world)
            {
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && e.Tags.Contains("dummy"))
                        return e;
                }
                return null;
            }

            static int CountDummies(GameplayWorld world)
            {
                int n = 0;
                foreach (var e in world.Live)
                {
                    if (e != null && e.IsAlive && e.Tags.Contains("dummy"))
                        n++;
                }
                return n;
            }

            static Entity FindDummyNear(GameplayWorld world, float ahead)
            {
                Entity best = null;
                float bestOff = 99f;
                foreach (var e in world.Live)
                {
                    if (e == null || !e.IsAlive || !e.Tags.Contains("dummy"))
                        continue;
                    float off = Mathf.Abs(e.Position.x - world.Player.Position.x - ahead);
                    if (off < bestOff)
                    {
                        bestOff = off;
                        best = e;
                    }
                }
                return best;
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
