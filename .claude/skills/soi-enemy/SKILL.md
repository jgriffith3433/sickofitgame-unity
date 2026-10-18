---
name: soi-enemy
description: Make a new Sick Of It enemy (or Warden) end to end: its place in the story, the entity JSON, the art spec, generated animated sprites, a level that introduces it, validation and a bot playtest. Use for "a new enemy for floor N / Circle X", "give <enemy> real art", "a Warden for <Circle>".
---

# A Sick Of It enemy, end to end

An enemy is data: `Assets/GameConfig/content/entities/<id>.json` (gameplay) + `Assets/GameConfig/art/<id>.json` (its
art spec) + sprite sheets the art pipeline writes into `Assets/Art/Enemies/<Name>/`. No C# unless it needs a mechanic
that doesn't exist (AGENTS.md "Adding a new mechanic").

## 0. Story first (no exceptions)

Read `Docs/Story.md` ("Structure", "The Circles", "Rules for enemies", "Roster") and the canon in your brief.
- **Which Circle**: floors count down from 1,000; Circle X is 1000–901, IX 900–801 … I 100–1 (floor 40 = Circle I, the
  Throne Stair: the Overlord's personal guard). Each Circle has one biome and one family of enemies; the Story table
  already proposes them (Brimstone Demons in IX, Watchers in VIII…). Use the proposal unless the request says otherwise.
- **A reason to be there**: who it works for, what it wants. Write it into Story.md's Roster table *first*.
- **Never a slime** (slimes are villagers; Yooba is a slime). **No work or office jokes.**
- If the request doesn't fit the story (wrong Circle, a slime), say so and propose the closest thing that fits.

## 1. Gameplay: the entity

Start from the closest existing entity with `extends` (`shooter`, `chaser`, `fighter`, `imp`…) and change only what
differs: `health`, `moveSpeed`, abilities (`chase`, `shoot`, `melee`…), `loot` (diamonds only if it carries them; loop
shards from the damned), `tags`, and a design note with its reason. Every field is in `Docs/GameplayReference.md`;
recipes in `Docs/GameplayCookbook.md` ("An enemy variant", "A boss fight").
**The body must fit the figure** (`body.radius` about half the art's `height`): `extends` copies the parent's body, and
`shooter` is a solid circle with no gravity made for flyers. A ground enemy with a big solid body is a wall across a
travel stage: its chase shoves the hero off the back of the camera, the camera never advances and the floor counter
freezes (Brimstone Demon, first try). Ground walkers: a small radius, usually `solid: false` (contact damage still
hurts), spawned with their feet on `track.groundY`. Projectile hitboxes match what's drawn. Leave `visual.animator` to the art pipeline;
a placeholder `visual.color` is fine until then.

## 2. Art: spec, then the pipeline (GPU: ask first)

Write `Assets/GameConfig/art/<id>.json` (`Docs/ArtPipeline.md` §1; `art/imp.json` is the reference): one specific
`description` (colors, body, props, what makes it *this* Circle's creature), `height` in world units, the `poses` its
clips need, and `clips` (`idle` required; `attack`, `hurt`, `die`, `move` as its abilities need). Side view, facing
right, in place.

Cost before running: a warm run of the Imp was ~6 GPU-minutes (3 stand takes + 2 pose takes + 4 clips): about $0.20,
plus ~$0.12 if a server has to boot. Price yours with `hawkesgen_estimate` (project `sickofit`: `image.edit` × takes,
`video.i2v` × clips). **Without J's OK for the GPU in your brief, stop after the spec and ask** with the number.

```bash
node Tools/Art/art.mjs <id> --status     # what's missing or out of date
node Tools/Art/art.mjs <id>              # generate what's missing, build sheets, write visual.animator, import
node Tools/Art/art.mjs <id> --no-unity   # where Unity can't run (the server agent): the import happens in the build
```

- It goes through Hawkes Gen; each take has a job key, so a re-run after a failure pays for nothing twice.
- Keying uses Dose Of Destruction's Python (`DOD_ROOT`, its `.venv` with rembg). `art.mjs` checks it before sending
  anything (and that a `reference` isn't a Git LFS pointer: `git lfs pull --include <file>`); if it isn't there (the
  cloud server today), stop with the spec and entity done and say the art runs on J's PC:
  `node Tools/Art/art.mjs <id> --start`. The work folder is `SOI_ART_WORK`, else game.json's, else beside the repo.
- **Check `preview.png` yourself** (Read it): the same creature in every row, the pivot cross in the same spot, nothing
  cut off, no green fringe, the gold box on the attack's hit frame. Fix framing with the spec's per-clip fields
  (`size`, `start`, `end`, `flip`, `tolerance`) before regenerating anything.
- Size: the command prints figure height vs collider radius; change the entity's `body.radius`, not the art.

## 3. Introduce it in a level

Per Story.md it arrives **alone, with room to read it**, at most one new type per stage. Either a scripted beat in
the stage of its Circle (a phase that spawns it alone, then a breather), or the Circle's `enemies` table with
`fromFloor` (the floor generator brings it in partway). Use the `soi-level` skill for a new stage.

## 4. Check

- Validate: `studio_unity_test` (sickofit, EditMode) on the server, or locally the batch `Validate` (AGENTS.md
  "Checking your changes") and read `Logs/GameConfig.log`: zero errors.
- Copy `Assets/Tests/PlayMode/GeneratedArtTests.cs`'s imp case for the new enemy (sprites load, attack fires on its
  event, Hurt on a hit, Die on death).
- Bot playtest of the level that introduces it: `.hawkes/scenario.txt` = `level-<n>` (n = its position in game.json).
  Criteria to state: the new enemy appears alone first and is readable (its art, not a placeholder); it attacks and
  the bot can kill it; its death plays; no "placeholder art" or missing-sound problems for it; the level is still
  winnable (outcome won within its tries).
- Sounds for it (hurt, death, attack) go in `audio.json` (`soi-voice-music` skill).

Report: GPU minutes and dollars per step (stand, poses, clips) from the jobs, and the playtest verdict.
