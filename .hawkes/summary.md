# Weapons: the launcher is stolen on Floor 998, five weapons drop on later floors

**Not compiled or run**: this server has no Unity or dotnet. The C# was written against the existing APIs and read back, and every JSON file parses with its references checked by script. The first Unity import and the `weapon-drops` playtest are the real test. Check `Logs/GameConfig.log` first.

## Fix in this pass (canon: Rewind orbs, m_0e7e718ff7ed)
- The canon check flagged `"entity": "rewindOrb"` in the three new bearers. Removed that 20% drop from `impPistolBearer.json`, `impShotgunBearer.json` and `impSpitfireBearer.json`; each now drops only its weapon pickup. Nothing else changed.
- Inheritance checked: `extends` replaces lists (EntityDef: "objects merge, lists replace"), so a bearer's own `drops` list is the whole list and nothing comes from `imp`. `launcherThief` extends `impFleeing` (`drops: []`), and `impForeman` has `drops: []`. `cinderBrute` extends `cinder`, not an imp.
- **For J:** the old `imp.json` base (from before this branch, untouched here) still drops `rewindOrb` at 20%, against the same canon. Every plain imp on the existing floors drops it, so fixing it changes how much juice those floors give. I left it alone because the thread said to change nothing else. It needs its own small task.
- Checked: the three files parse as JSON and their drops are as above. The `weapon-drops` playtest doesn't look at orbs. Still no Unity on this server, so no import was run.

## What it does
- **Floor 998 (The Spill)**: halfway down the river a thief imp bursts out of the hay cart and takes the rocket launcher (`launcherThief`, a spinning `launcherToss`, slow motion, "ROCKET LAUNCHER GONE"). The announcer shouts "Hell has taken your gun!" and the hero says "Oh, sure. Take the rocket launcher. Take it." and then "Fine. Personal, then." He has fists from then on.
- **Floors 998-994 are fists floors.** Each of 997-994 starts with `giveWeapon fists` and `takeAllWeapons except fists`, so it is a fists floor however it was opened. Tuned for melee: `cinder` waves and a new `cinderBrute` (3 health, needs a hook and a jab) come at him at set sections. The hazards and chain set pieces were left alone. The playtest bot now closes in on its target when it holds a melee weapon.
- **Floor 994's landing**: the thief is back and drops `rocketLauncherPickup`, with a banner and "There you are. You smell like imp." The landing's own banner now waits 4.4 s.
- **Weapons found on floors**, each a `content/weapons/*.json` with a pickup entity, a first-pickup banner (`NEW WEAPON: <NAME>`) and a hero line (voiced):
  - **Spitfire** (bearer imp on 993): a stream, 0.085 s a shot, 48 rounds that regenerate.
  - **Ricochet Pistol** (bearer imp on 992): slow, 2 damage, the bullet bounces 3 times.
  - **Chain-Whip** (the Imp Foreman, 991): melee, 2.8-3.5 reach against the fists' 1.2-1.4.
  - **Shotgun** (bearer imp on 954): 5 pellets in a fan, range about 5, 8 shells.
  - **Boomerang Halo** (the Pile Keeper, 951): flies out, comes back, hits both ways.
- **A pickup unlocks the weapon permanently.** `WeaponUnlocks` (PlayerPrefs) is kept when a run ends and after a death. Only New Game clears it (one added line in `RogueliteManager.ResetAllRogueliteData`). Every level starts with all unlocked weapons in hand. A rewind past a pickup can't lose it. Scene-taken weapons are "suspended" for the run (saved with rewind state and in `WeaponProgress`) and come back with the next run.
- **New mechanics (C#)**: `projectile.entity`, `pellets` and `pelletSpread` on weapons; a `returnToHero` ability; `unlockWeapon` and `takeAllWeapons` actions; `onlyHitLiving` on `projectile`; `Entity.IsHeroShot` (tag `heroShot`, so enemy bullets and cinders neither cancel nor are cancelled by his shots); the launcher can now be taken. The validator requires a shot entity to be on team Player.
- **Docs**: Story.md "Weapons" and "The Spill's theft", LevelDesign (998), the cookbook, AGENTS.md. The story had said weapons are lost with the run; J's request changed that, and Story.md now says so.
- **Playtest `weapon-drops`** (~20 min, named in `.hawkes/scenario.txt`), with the new `weaponArena` scene and `targetDummy`:
  1. Floor 998: weaponTaken, the thief seen, the banner and line, fists only, fist hits.
  2. 997-995 played fists-only.
  3. Each drop floor (994, 993, 992, 991, 954, 951) found, banner and line seen, weapon held.
  4. A real death on Floor 999, then the next run still holds the launcher and all five weapons. Fists, a run-only weapon, are gone.
  5. A bench with one check per weapon:
     - Spitfire: at least 3x the launcher's rate, and the ammo drops.
     - Ricochet Pistol: the bullet bounces off the ground.
     - Chain-Whip: hits a dummy 2.6 away that the fists can't reach.
     - Shotgun: 5 pellets, hurts point blank and does nothing across the room.
     - Boomerang Halo: flies out, comes back, hits both ways.
  The old `weapons` test now also resets unlocks.

## Look at
- Placeholder art: pickups are labelled circles in the weapon's colour. Shots reuse the Fireball sheet, tinted. The thief reuses the imp art. The `playtest` will list these as polish.
- Bearers are imp variants swapped in for one existing imp on 993 (`hollowImp1`, which must be killed anyway), 992 (`topImp`) and 954 (`borderImp`).
- Audio: 17 new sounds and lines generated through Hawkes Gen, about $0.25. `whip_crack` already existed and was reused.
- Conflicts: none. Small edits in shared files: `RogueliteManager.cs` (one line), `Entity.cs`, `PlayerController.cs`, `PlaytestBot.cs`, `ContentValidator.cs`. None of the other agent's listed files except the validator, where I added 5 lines.
- Left for later: the generated `Docs/GameplayReference.md` regenerates on the first import. Weapons on the Cracked Road floors and later Circles. Real art.

## GPU estimate (not run)
10 images (6 pickup icons, 2 shot sprites, the thief imp with the launcher, a brute-cinder concept): `hawkesgen_estimate` says 64 GPU seconds, about 7 min on one server, **$0.21** ($0.83 with the idle tail). Enemy-style animated sheets for the thief and the brute through `art.mjs` would be extra (clips are video, priced separately). Awaiting J's OK.

## Merge with beta (after the bystander change landed)
- `AGENTS.md`: both sides added a bullet in the same place: this branch's "Found weapons, Floors 998-951" and beta's "Bystanders". Both are kept, weapons first.
- `Assets/Scripts/Playtest/SickOfItPlaytest.cs`: two spots. The scenario list and the scenario dispatch each had this branch's `weapon-drops` and beta's `bystander` added in the same place. Both registrations and both `else if` branches are kept. Their ids live in separate partial files (`SickOfItPlaytest.Weapons.cs`, `SickOfItPlaytest.Bystander.cs`), so the two can't clash.
- These files merged without conflicts: `RogueliteManager.cs` (beta's `UnlockFlags.Reset` and Keep Weapons next to this branch's `WeaponUnlocks.Reset` on New Game), `PlayerWeapons.cs`, `ContentValidator.cs`, `Entity.cs`, `PlayerController.cs`, `audio.json`, Story.md and the cookbook. I read them back: no conflict markers, and every changed JSON file parses. There's still no Unity or dotnet here, so nothing was compiled.
