---
name: soi-level
description: Design and build a Sick Of It stage (a level file): its place among Hell's 1,000 floors, the Circle, the travel script with beats and generated floors, voiced lines, then validation and a bot playtest. Use for "make Floors 975-951", "the next level", "a boss arena for <Warden>".
---

# A Sick Of It stage, end to end

A stage is `Assets/GameConfig/levels/LevelN.json` (listed in `game.json` `levels`, in order) whose `script` runs it.
Story stages inside Hell are **travel stages** that climb a stretch of floors in one Circle. References:
`levels/Level6.json` ("Floors 1000-976: The Landing") and `content/circles/emberWastes.json` (Circle X).

## 0. Where it goes

From `Docs/Story.md`: the next stretch of floors after the last stage (Level6 ends at 976, so the next is 975–951),
its Circle (biome, enemy family, Warden at x01), the act's beat, and the milestones it passes (999, 900, 666, 500, 100,
13, 2). Names are places: `displayName` "Floors 975-951: <place>". No work or office jokes; slimes are villagers.

## 1. Design before JSON

Write a short plan in your summary (a new story beat also goes into Story.md; JSON comments are lost on export, so
not there): the beats in order, which enemies (from the Circle's family;
one new type at most, introduced alone), hazards (telegraphed ≥ 1 s), breathers, the end beat, and the lines. Follow
the pacing rules: quiet first 5 s, ≤ 3 enemies on screen in Circle X (rising slowly per Circle), 60–120 s per stage.

## 2. Build it

Cookbook recipes (`Docs/GameplayCookbook.md`): "Make a new level", "A stage that climbs through Hell's floors",
"A boss fight", "Branching". Steps:
1. Copy Level6.json to the next LevelN.json; set `levelName`, `displayName`, `levelNumber`, `music`, `deathConfigName`.
2. Add `"LevelN"` to the end of `game.json` `levels`.
3. `script.floors` (`start`, `end`, `unitsPerFloor`, `circle`), `world` Travel with `travelLength` ≈ (start − end) ×
   unitsPerFloor + 2, `markers` for beats; phases alternating `generateFloors` stretches and scripted beats; end on
   `floorReached` of the last floor.
4. A new Circle: `content/circles/<id>.json` (biome backdrop, track, props, weighted enemies, hazards, difficulty
   range, milestones). Backdrops reuse footage first (Story.md "Footage to build from"); new backdrop art goes through
   Hawkes Gen and `hawkes.assets.json`, priced and approved like any GPU work.
5. Lines: the announcer's floor title and one or two hero lines in `audio.json` (`soi-voice-music`).

## 3. Check

- Validate (`studio_unity_test` sickofit EditMode, or the batch `Validate`): `Logs/GameConfig.log` has no errors.
- The PlayMode bot playtest of the level, then Studio's: `.hawkes/scenario.txt` = `level-<n>`. Criteria: the floor
  counter runs from start to end; each beat happens in order (phase events in the timeline); the new enemy (if any)
  arrives alone; every hazard is telegraphed; the bot can finish it (won within its tries); 60–120 s on a good run;
  no softlock (clock stall) or GameConfig problems.
- Rewind needs nothing: content can't break it (AGENTS.md).

Unity products build on the build server (about $0.10 a build); the beta build and its playtest run after merge.
