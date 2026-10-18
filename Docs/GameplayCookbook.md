# Gameplay Cookbook

Worked recipes for common requests. Every field is listed in [GameplayReference.md](GameplayReference.md) (generated from the code).
All of these are plain JSON in `Assets/GameConfig/`: no C#, no Generate Project. Save the file, focus Unity (or run the
`Validate` command), and read `Logs/GameConfig.log`.

Working example: **"Arcade: Fire Rain"** (`levels/ArcadeFireRain.json`, an old arcade level kept off the story path) uses most of the recipes below; its entities are in
`content/entities/` (`fireball`, `exploder`, `sludge`, `magmaKing`), its sequences in `content/sequences/`.

---

## Make a new level

First find the level's place in [Story.md](Story.md): which Circle and floors it covers, which enemies live there,
and its story beat. Follow the pacing rules there (quiet first 5 seconds, new enemies alone, breathers between
pushes). `levels/Level1.json` (The Meadow) is the reference: phases timed to the backdrop footage, one new enemy
at a time, a voiced line per beat, and a final phase that only checks for the clear after the last spawn. A stage
inside Hell (a stretch of floors) is a travel stage: see "A stage that climbs through Hell's floors" below, with
`levels/Level2.json` (The Landing) as the reference.

1. Copy an existing `levels/LevelN.json` to `levels/Level4.json` (the next story level). Change `levelName`, `displayName`, `levelNumber`,
   `music`, `backgrounds`, `deathConfigName`.
2. Add `"Level7"` to the `"levels"` list in `game.json` right after the last story level (`Level6`), before
   `CrackedRoad` and the `Arcade*` levels (the position is the level number). A level goes on to a video with
   `nextVideoNumber`, or straight to another level with `nextLevelNumber` (results, the Shaft, then that level: how
   the stages inside a Circle follow each other); with both 0 it ends at the level select. Set the previous stage's
   `nextLevelNumber` to the new one, and add the level to the `story-path` playtest's `StoryPath`
   (`SickOfItPlaytest.cs`, `(0, n)` for "no video before it") once the path reaches it.
3. Write its `"script"` (below). Set `"enemies": []` — a script replaces the enemies list.
4. Nothing to generate: the level select screen builds its buttons from the levels list at runtime. A new level unlocks after the one before it is completed.

```json
"script": {
  "objective": { "complete": { "type": "allDead", "target": { "tag": "wave" } }, "text": "Clear the arena" },
  "phases": [
    { "name": "main", "rules": [
      { "when": { "type": "atTime", "seconds": 1 },
        "do": [ { "type": "spawn", "entity": "imp", "at": [4, -2], "tags": ["wave"] } ] }
    ] }
  ]
}
```

## Rain fire from the sky

A hazard fires projectile entities from the level itself and stops when its phase ends.

```json
{ "type": "startHazard", "id": "rain", "projectile": "fireball",
  "pattern": { "type": "rain", "count": 1 }, "speed": 3, "interval": 1.2, "rampPerSecond": 0.03 }
```

`rampPerSecond` makes it intensify. Use `stopHazard` to end it early, `"persist": true` to keep it past its phase.

## Enemies in waves that get tougher

An `every` rule repeats; `multiplyPerRepeat` scales each wave (wave 3 gets `multiply + 2 × multiplyPerRepeat`).

```json
{ "name": "waves", "when": { "type": "every", "seconds": 9, "startAfter": 3, "maxTimes": 4 },
  "do": [ { "type": "spawnWave", "entity": "chaser", "count": 3, "interval": 0.6, "anchor": "Camera",
            "points": [[-5.5, -2], [5.5, -2], [5.5, 1]], "tags": ["wave"],
            "modifiers": [ { "stat": "MaxHealth", "multiply": 1, "multiplyPerRepeat": 0.25 } ] } ] }
```

For stats that should also change while enemies are alive (speed, damage), use a `modifier` action; add
`"includeFuture": true` to cover enemies spawned later.

## Only one kind of enemy in a level

Spawn only that entity in the level's script. To change every enemy already in a level, replace the `"entity"`
values in its spawn actions.

## An enemy variant ("a faster chaser")

`extends` copies another entity; objects merge, lists replace.

```json
{ "schemaVersion": 1, "extends": "chaser", "moveSpeed": 3.5, "health": 1, "visual": { "color": "#88CCFF" } }
```

## Chasers that explode near the player and leave sludge that hurts over time

`content/entities/exploder.json`:

```json
{ "schemaVersion": 1, "extends": "chaser", "moveSpeed": 2.6,
  "abilities": [
    { "type": "chase", "stopDistance": 0.3 },
    { "type": "explodeNear", "triggerRadius": 0.9, "fuse": 0.5, "blastRadius": 1.0, "damage": 1 } ],
  "onDeath": [ { "type": "spawn", "entity": "sludge", "anchor": "Self" } ] }
```

`content/entities/sludge.json` — `aura` damage ignores the player's hit invincibility, so it keeps hurting:

```json
{ "schemaVersion": 1, "team": "Enemy", "health": 0, "body": { "radius": 0.5, "solid": false },
  "visual": { "placeholderColor": "#5A8F2ACC", "placeholderLabel": false }, "healthBar": { "show": false },
  "abilities": [ { "type": "aura", "radius": 0.35, "damagePerTick": 1, "interval": 0.75 },
                 { "type": "lifetime", "seconds": 6 } ] }
```

To change the existing `chaser` itself instead of making a new type, edit `content/entities/chaser.json` the same way.

**Kamikaze on touch instead** (the stock `chaser`): `{ "type": "contactDamage", "damage": 1, "dieOnHit": true }` hits once and
dies, running `onDeath`. The chaser's `onDeath` spawns `slimeSplat` plus a few `slimeGlob`s with different `velocity`s
(Dynamic bodies with gravity) for a cosmetic green burst; spawn `sludge` there too if the puddle should hurt.

## A boss fight

A boss is an entity with a state machine; health thresholds switch its attacks. See `content/entities/magmaKing.json`:

```json
"states": [
  { "name": "fanning",
    "abilities": [ { "type": "shoot", "projectile": "fireball", "pattern": { "type": "spread", "count": 3, "angle": 30 },
                     "speed": 4, "interval": 2 } ],
    "transitions": [ { "to": "enraged", "when": { "type": "healthBelow", "fraction": 0.5 } } ] },
  { "name": "enraged",
    "onEnter": [ { "type": "runSequence", "sequence": "bossEnrage" } ],
    "abilities": [ { "type": "shoot", "projectile": "fireball", "pattern": { "type": "radial", "count": 10, "rotationPerVolley": 12 },
                     "speed": 3.5, "interval": 1.6 },
                   { "type": "spawner", "entity": "exploder", "interval": 6, "maxAlive": 2 } ] }
]
```

In the level: a `boss` phase whose `onEnter` runs an intro sequence (lock input, text, camera shake, then spawn the
boss with `"id": "boss"` and show a `bossBar`), and `"objective": { "complete": { "type": "allDead", "target": { "id": "boss" } } }`.

## A cutscene moment / unique scripted beat

Put steps in a sequence (inline `steps` or a file in `content/sequences/`); `wait` pauses between steps.

```json
{ "type": "runSequence", "steps": [
  { "type": "lockInput", "seconds": 3 },
  { "type": "slowMotion", "scale": 0.3, "duration": 1.5 },
  { "type": "showText", "text": "Something is coming...", "duration": 2 },
  { "type": "wait", "seconds": 2 },
  { "type": "playMusic", "clip": "Videos/Level3_Background1_Audio.mp3" } ] }
```

## A scene with the hero ("walk him to the portal, he gets sucked in")

A scene directs the hero himself. Lock input and rewind first (the hero can't undo a scene), then chain the beats
with signals: each move `emit`s when it is done, and a `signal` rule (or `goToPhase`) starts the next.

```json
{ "type": "lockInput" },
{ "type": "lockRewind" },
{ "type": "walkPlayerTo", "x": 2.5, "speed": 0.6, "emit": "atPortal" }
```

Then, on `atPortal`:

```json
{ "type": "runSequence", "steps": [
  { "type": "facePlayer", "direction": "Right" },
  { "type": "playPlayerAnimation", "state": "Idle" },
  { "type": "cameraTo", "at": [0, 0.9], "anchor": "Player", "size": 1.4, "duration": 1.5 },
  { "type": "wait", "seconds": 2 },
  { "type": "pullPlayerInto", "target": "portal", "duration": 1.8, "spin": 720, "endScale": 0, "emit": "gone" },
  { "type": "wait", "seconds": 2 },
  { "type": "fade", "to": "Black", "duration": 1 },
  { "type": "wait", "seconds": 1.2 },
  { "type": "playVideo", "video": 2 } ] }
```

- `walkPlayerTo` drives his own movement (Walk clip, turning), even with input locked; `x` is measured from `anchor`,
  or use `target` (a spawn `id`) to walk up to an entity. He stops on the point, or after 1.5 s stuck against a wall.
- `facePlayer`: `"direction": "Left"`/`"Right"`, or `"target"` to face an entity.
- `pullPlayerInto`: a `target` id (followed if it moves) or an `at` point. Gravity and collisions go off, he slides in
  spinning (`spin` degrees, clockwise) and shrinking (`endScale`), then he is hidden for the rest of the level.
- `playPlayerAnimation` plays a state of the player's Animator by name (`player.json` `animationConfig`). The hero's
  scene clips are `Curious` (leans in and peers down, loops), `Shout` (cups a hand and calls down, once) and `Grabbed`
  (yanked off his feet, flailing and tumbling, loops). A state isn't left by itself (no transitions out of the scene
  clips): play `Idle` before he walks again. A state in player.json that the generated controller doesn't have yet
  (Generate Project not run since it was added) still plays, straight from its sheet (`PlayerSceneClip`).
- New hero clips: generate the mp4 from an Idle frame (Hawkes Gen `video.i2v`, start = end for a loop, black
  background, same framing), bake it with `Tools/Art/player_clip_sheet.py` (604x340 cells like every hero sheet; see
  its header) and add the state to `player.json`.
- `cameraTo` / `cameraZoom` / `cameraReset` move and zoom the **Static** camera (`size` is the orthographic half-height;
  the levels use 2.8, smaller is closer); `cameraReset` goes back to the level's view. Other camera modes ignore them
  (the validator warns).
- `fade` `"Black"` holds the screen black (texts and the pause menu still show on top); `"Clear"` brings the game back.
- `unlockInput` / `unlockRewind` hand control back if the level goes on.
- All of it is saved for rewind, so rewinding through a scene (after `unlockRewind`) replays it.
- Keep the hero safe in a scene with an `invulnerable` status (`content/effects/sceneUntouchable.json`, `applyStatus`
  on `"player": true`) rather than `playerInvincible`, which makes him blink the whole time.
- Something that hums while it exists (a portal): `sounds.loopSound` on its entity, an audio.json id; it loops on
  the entity's own audio source and stops when it dies or is despawned.
- A scene that must play on either side of the hero: set a counter from `playerInZone` on entry, then branch with
  `if` / `counter` (Level1.json's `portalRight`). Once he is pulled in, the hero stays hidden at the pull point, so
  `"anchor": "Player"` places things at the portal.
- A prop with an opening, a loop and an ending (the portal): an animator whose default state is the non-looping
  opening, a transition with `hasExitTime` (no conditions) into the loop, and a `playAnimation` from the script for
  the ending. `playAnimation`, `setState` and `setAnimParam` target `self` by default and JSON only overrides the
  fields it names, so from a level script write `"target": { "self": false, "id": "portal" }`; `{ "id": "portal" }`
  alone is self + id, an import error that stops the build. If something replaces it afterward (the scorch), bake both with one pivot so it lands on the same
  spot: `Tools/Art/fx_sheets.py` crops every clip of an effect to one shared frame (Docs/ArtPipeline.md, "Effects").
- The real one is the end of Level 1 (phases `portalBeam` to `portalClose`); the `level-1` playtest checks it beat
  by beat.

**Trying a scene without its level:** put a level script in `content/scenes/<id>.json` (same fields as a level's
`script`, no `objective` needed). The playtest's `cutscene` scenario plays `content/scenes/cutsceneTest.json` on
Level 1's stage (`LevelRuntime.PendingScene`, a setup shortcut) and checks every action above.

## Something moving over a video ("the hero tumbles down the fire tunnel")

Videos, deaths and levels have a `timeline.overlays` list: pictures drawn over the scene and moved by keyframes.
Each frame the pose is **sampled from the clock time**, never tweened, so holding rewind plays it backward, a seek
lands on the right pose, and nothing needs saving. It is hidden before its first keyframe and after its last.

```json
"timeline": {
  "overlays": [
    {
      "name": "heroFall",
      "spriteSheet": "Art/Sprites/Player/Fall.png",  // or "sprite": "Images/X.png#X_0" for a still
      "fps": 12.0, "loop": true,                      // sheet frames play from the first keyframe
      "sortLayer": "Player", "sortOrder": 0,          // the video itself is Background1
      "tint": "#FFE6CC",
      "keyframes": [
        { "time": 10.45, "position": [0.9, 0.9], "rotation": 20.0, "scale": 1.25, "alpha": 0.0, "ease": "Out" },
        { "time": 14.4,  "position": [0.0, -0.18], "rotation": -1390.0, "scale": 0.03, "alpha": 0.0, "ease": "Linear" }
      ]
    }
  ]
}
```

- `position` is in world units: the camera looks at (0, 0) and a video frame is 10.24 wide and 5.76 high.
- `rotation` is degrees and is not wrapped: 1080 from 0 is three turns; negative turns clockwise.
- `ease` (`Linear`, `In`, `Out`, `InOut`, `Hold`) shapes the move from that keyframe to the next.
- The sheet must be one some config already uses (the asset registry holds its sprites); the player's sheets are.
- Keyframes must be in time order; the import report flags it, and a missing sprite or keyframe.
- Video2 uses it for the hero's fall (10.45-14.4 s). In a video, hold R or the ◄◄ button to rewind; Escape skips.
  The playtest's `video-2` scenario watches it and checks each beat.

## Branching: secret exits, alternate paths

```json
{ "when": { "type": "playerInZone", "center": [-6.5, 2], "size": [1, 1] }, "do": [ { "type": "goToLevel", "level": 4 } ] }
{ "when": { "type": "counter", "name": "kills", "atLeast": 30 }, "do": [ { "type": "playVideo", "video": 3 } ] }
{ "type": "if", "condition": { "type": "healthBelow", "target": { "player": true }, "fraction": 0.3 },
  "then": [ { "type": "heal", "target": { "player": true }, "amount": 2 } ] }
```

Counters: `kills`, `kills:{entityId}`, `spawned`, `spawned:{entityId}`, `playerHits`, plus any you set with
`setCounter` / `addCounter`. Signals: `emit` one, react with a `signal` trigger.

## The arcade loop: rewind juice, orbs, upgrades

**Rewind is limited.** `game.json` → `"rewindBudget"`: `startSeconds` of juice per level, `maxSeconds` held, `costPerSecond`
spent per second rewound. The bottom progress bar shows the level played so far (length = script `"length"`, ticks from
`"markers"`) and the juice as a teal tail behind the playhead: exactly how far back you can go.

**Orbs refill it.** Enemies drop pickups through `"drops"`; `content/entities/rewindOrb.json` is a `collect` pickup that runs
`addRewind`. A pickup pays once, even if a rewind replays it.

```json
"drops": [{ "entity": "rewindOrb", "chance": 0.25 }]
```

**Upgrades make the player stronger** (sawtooth difficulty: each level ramps up, then you get stronger, then the next
starts a bit easier). They live in `content/upgrades/{id}.json` (`modifiersPerLevel` with `AttackSpeed`, `Damage`,
`MoveSpeed`; `extraShotsPerLevel`; `rewindSecondsPerLevel`; `maxHealthPerLevel`), are saved, and reset on New Game.
Offer them at the end of a level, before completing it, so dying and retrying can't farm them:

```json
{ "when": { "type": "allDead", "target": { "tag": "wave" } },
  "do": [{ "type": "runSequence", "steps": [
    { "type": "showText", "text": "MONDAY: DEFEATED", "duration": 2 },
    { "type": "wait", "seconds": 1.5 },
    { "type": "offerUpgrades", "title": "You earned a treat" },
    { "type": "setCounter", "name": "cleared", "value": 1 } ] }] }
```

with `"objective": { "complete": { "type": "counter", "name": "cleared", "atLeast": 1 } }`.

**Tone:** frantic arcade bullet-dodging (slow, readable bullet patterns in numbers: `spread`, `radial`, `sweep`, bursts),
comedic text beats (`showText` banners and subtitles, boss intros, upgrade descriptions).

## Diamonds and the shop

- **Drops:** an enemy's `loot` rolls `count` times at `chance` when it's killed (not when it bursts on its own) and
  drops `value` pickups of `loot.entity` (empty = `diamond`, content/entities/diamond.json: bobs, magnets toward the
  player, lasts 10 s, pays +1 diamond once even across rewinds). Diamonds are kept across New Game.
- **Shop upgrades** (permanent until New Game) are `player.json` `availableUpgrades`:
  `{ "id": "moreHealth", "title": "More health, duh", "description": "...", "costsPerLevel": [10, 20, 35],
  "maxLevel": 3, "effect": "MaxHealth", "amountPerLevel": 1 }`. Effects: MaxHealth (+HP, also tops up carried health),
  ExtraJump (+air jumps), MoveSpeed / FireRate / Damage (+fraction, 0.1 = +10%), RewindJuice (+seconds), Magnet (+pickup
  range). No code needed for a new one; the shop lays out a card per entry.
- Between-level picks (content/upgrades) are separate: bigger, run-long boosts offered at the end of each level.

## Status effects (poison, slow, stun, shield)

`content/effects/{id}.json`; apply with `applyStatus`, or from `contactDamage` / `projectile` / `aura` via `"effect"`.

```json
{ "schemaVersion": 1, "duration": 2, "modifiers": [ { "stat": "MoveSpeed", "multiply": 0.4 } ], "tint": "#66AAFF88" }
```

## A different kind of level space

`"world"` in the script: `"camera": "Follow"` with `boundsCenter`/`boundsSize` for a wider arena, `"Scroll"` with
`scrollSpeed` for an auto-scroller, `"Travel"` for a stage that goes somewhere (next recipe), and `"playerGravity": 0.5`
for floaty jumps.

## A stage that climbs through Hell's floors ("make Floors 975-951")

Story stages are **travel stages**: the camera follows the player forward (never back), the ground runs along with
it, platforms and scenery stream in ahead, and the **floor counter** ticks down as they go (HUD `FLOOR 973`, floor
signs in the world). Between hand-written story beats the **floor generator** fills the floors from the stage's
**Circle** (`content/circles/{id}.json`). Working example: `levels/Level2.json` ("Floors 1000-991: The Landing") in
`content/circles/emberWastes.json` (Circle X).

1. **The circle** (one per Circle of Hell, shared by its stages; skip if it exists). Its `firstFloor`/`lastFloor`,
   `backgroundLayers` (the backdrop for its stages), `track` (ground/platform colors, `segmentLength`,
   `platformChance`, `clearStart`), `props` (scenery: `shape` Spire/Rock/Block/Glow or a `sprite`, `parallax` 0 = with
   the ground, 0.8 = far away, negative = in front), `enemies` (weighted; `fromFloor` introduces one partway up),
   `hazards` (a `warning` + `telegraph` ≥ 1 s, then `actions`), `easiest`/`hardest` difficulty (`maxAlive`,
   `pushSize`, `breather`, `hazardChance`, `healthMultiplier`; floors in between are interpolated) and `milestones`
   (a line when the counter reaches a floor, e.g. 999).
2. **The stage**: a level whose script has `floors` and a Travel `world`:

```json
"floors": { "start": 975, "end": 951, "unitsPerFloor": 3, "circle": "emberWastes" },
"world": { "camera": "Travel", "travelDirection": "Right", "travelLead": 1.5, "travelLength": 74 },
"markers": [ { "floor": 960, "label": "AMBUSH" } ]
```

   `travelLength` ≈ (start − end) × unitsPerFloor + 2 so the camera stops at the top. Leave `backgroundLayers` empty to
   use the circle's. The progress bar measures floors; `markers` take a `floor`.
3. **Phases alternate beats and generated stretches** (follow the pacing rules in Story.md):

```json
{ "name": "climb", "onEnter": [ { "type": "generateFloors", "untilFloor": 960, "firstBreather": 3 } ],
  "rules": [ { "when": { "type": "signal", "name": "floorsDone" }, "maxTimes": 1,
               "do": [ { "type": "goToPhase", "phase": "ambush" } ] } ] },
{ "name": "ambush", "onEnter": [ { "type": "lockTravel" }, { "type": "spawn", "entity": "imp", "anchor": "Camera", "at": [3.6, -1.4], "tags": ["beat"] } ],
  "rules": [ { "when": { "type": "allDead", "target": { "tag": "beat" } }, "maxTimes": 1,
               "do": [ { "type": "unlockTravel" }, { "type": "goToPhase", "phase": "climb2" } ] } ] }
```

   The generator stops at `untilFloor`, waits for its last push, then emits `floorsDone`. While one of its pushes is
   alive the camera holds (`world.holdForPushes`), so the player fights instead of running past. Give a second
   stretch `"seed": 1` so it differs. End the stage with `{ "type": "floorReached", "floor": 951 }`.
4. Floors outside travel: `setFloor`, `climbFloors` (a shortcut, an elevator); triggers `floorReached`,
   `distanceReached`; counters `floor` and `distance`.
5. **Dress the stage** (Levels 3-6 do all of these):
   - `world.props`: scenery for this stage only, drawn with the Circle's (same fields as a Circle's `props`): the
     Spill's fence posts, cart and smoking hay. A prop with `sizeVariation` 0, `chance` 1 and `spacing` =
     `unitsPerFloor` stands identical on every floor (the Loop's "same rock").
   - `floors.sign`: words carved on a big sign where the hero lands (`"IMPS ONLY"`).
   - `floors.repeat`: floors Hell makes him walk twice (`[957]`): that floor takes two floors' distance, two signs
     show its number and the counter stays on it (telemetry `floorRepeat`). Add `unitsPerFloor` to `travelLength`
     for each, and trigger the second pass with `distanceReached` (the floor doesn't change).
   - A telegraphed falling hazard: a `startHazard` whose projectile is a harmless glow (`slagDrip`, team Neutral,
     `lifetime` 1.2) with `"speed": 0`, whose `onDeath` spawns the real shot (`slag`) where it hung. Every drop is
     then warned on its own spot, not just by the hazard's subtitle.

**Backdrops that change as you go**: a background layer's `sequenceBy` is `Time` (seconds), `Distance` (units
travelled) or `Floors` (floors climbed in this stage), so `"sequenceBy": "Floors", "sequenceStart": 3` swaps the
backdrop after three floors. `clipStart`/`clipLength` play just part of a long video (Circle X uses Video2's
lava-river shot, `clipStart` 17.6, `clipLength` 5).

Same seed, same floors: platforms, props and pushes come from the level seed, so rewinds and bot playtests repeat.

## The Shaft (Hell's map between levels)

`ShaftScreen` (Core/Progression) answers `ShaftHook`: after the video in `ui/shaft.json` `plungeAfterVideo` (Video2)
it plunges from the Meadow to `bottomFloor`; after any level whose script has `floors.end` it climbs the hero's marker
to that floor. Levels without floors (the Meadow, the arcade levels) go straight on. Nothing to wire for a new stage:
give it `floors` and the Shaft follows it.

- **Circles** are `bands` in ui/shaft.json, bottom (X) to top (I): numeral, name, floors, colour, Warden. A band with
  `circle` set takes its name and floors from `content/circles/<id>.json`. The Warden is named in "next Warden" and
  drawn as ??? on the map until the hero has reached his Circle's top floor.
- **Paintings**: each band's `art` (its Circle's biome, made with Hawkes Gen's `floor-backdrop` preset, 768x960 and
  seamless so the bands stack) is scaled to cover the band and cropped (the shaft is wider in portrait); `wardenArt`
  (the Warden on a transparent background) is tinted `wardenUnmet` (a dark silhouette) until met, then shown in colour;
  `meadowArt` is the Meadow at the top and `markerArt` the hero's marker (his helmet and launcher, `markerSize` high).
  Anything without art keeps its placeholder shape. `Tools/Art/shaft_art.py` brings picked images in: list each
  slot's source (and Gen job) in `Tools/Art/shaft_art.json`; it writes `Assets/Art/Shaft/<slot>.png` + `.meta`
  (Wardens keyed off green) and the paths in ui/shaft.json. The Shaft logs `art: N of 10 Circle bands painted, ...`.
- **Words** are theme.json `texts.shaft*` (counter, floors left, next Warden, the welcome subtitles); **sounds** are
  the ids in ui/shaft.json (`shaft_fall`, `shaft_slam`, `shaft_tick`, `vo_shaft_welcome`, `vo_shaft_great`).
- **Placement**: each readout has a landscape and a portrait `MenuPlacement` in ui/shaft.json; the shaft window is a
  panel and Continue a button for the layout audit. `shortcutHints` are floors that get a wall crack or a dotted gap.
- It is presentation only: `ShaftProgress` (Data) remembers the marker's floor and whether the first plunge was seen
  (after that a tap or key skips it). New Game resets both.
- Playtests: `shaft` (the plunge, The Landing, the climb, each beat a `check`, the paintings too: `plungePaintedBands`,
  `plungeWardenSilhouettes`, `plungeHeroMarker` and the same on the climb), `menus`/`layout` open a preview
  (`ShaftScreen.Preview(from, to)`), and `full`/`story-path` press Continue on it like a player.

## Art

Entities without art draw as colored placeholders with their id, so gameplay can come first. **To make art for an
enemy, use the art pipeline** ([ArtPipeline.md](ArtPipeline.md)): one spec in `art/<id>.json` and
`node Tools/Art/art.mjs <id> --start` writes everything below for you. By hand, give the entity
a `visual.animator` with states pointing at sprite sheets (`"spriteSheet": "Images/Thing.png"`), the same shape as the
generator's AnimationConfig. Animation events (`"events": [{ "frame": 2, "functionName": "fire" }]`) drive
`shoot.fireOnEvent` and `melee.hitEvent`.

To play only part of a long sheet, give the state `"firstFrame"` and `"frameCount"` (applied after `spriteNameFilter`);
this works for entities and for the player's `animationConfig` in player.json (the player's needs **Generate Project**).

## Menus (main menu, level select, videos, shop)

Menus are built at runtime by `MenuCanvas` (`Assets/Scripts/Core/UI/Menus/`), so restyling them is JSON-only and
needs no Generate Project. Two layers:

- **`ui/theme.json`: the look.** Fonts (`font`, `titleFont`: a .ttf/.otf path), text styles (`title`, `subtitle`, `heading`,
  `body`, `caption`, `chipText`: size, color or `gradientTop`/`gradientBottom`, `outlineColor`/`outlineWidth`, `shadowColor`,
  `uppercase`, `characterSpacing`, `italic`), button styles (`button`, `backButton`, `smallButton`, `card`: `size`, a
  `surface` with `shape` Rounded/Pill/Rectangle/Circle/Burst/Sprite, `fill`, `borderColor`/`borderWidth`, `shadowColor`/
  `shadowOffset`; hover/press colors, `hoverScale`, `hoverRotation`, resting `tilt` + `alternateTilt`, `idle` motion),
  `chip` (the health/diamond pills), `accent`/`positive`/`muted` colors, `entrance` and `titleEntrance`
  (Pop/Fade/Slide*/Drop with `stagger` so lists cascade in), `hoverSound`/`clickSound`, and the `healthFormat`/`pointsFormat` strings.
- **Each screen's `layout`: what goes where.** `backdrop` (`image` cropped to fill, `tint`, a drifting `pattern`
  Stripes/Dots/Checker, `vignette`), `title`/`subtitle` (`text`, `anchor`, `rotation`, `motion`), `list` (`anchor`, `size`,
  `direction` Vertical/Horizontal/Grid, `columns` 0 = as many as fit, `itemSize`; overflow scrolls), `backAnchor`/
  `backOffset`/`backText`, `statsAnchor`/`statsOffset`, and `decorations`: free stickers with `text`, a `shape` (Burst = comic
  starburst), `fill`, `anchor`, `rotation` and a `motion` (Bob/Pulse/Wobble/Sway/Spin/Jitter).

The in-level HUD uses the same theme: banners use `title`, subtitles `subtitle`, the upgrade picker uses `card`, and the
theme's `hud` block sets the player health bar (top-left; one segment per hit point up to `maxSegments`, a flash and
drain on hits, throbbing at `lowHealthFraction`), the boss bar, the progress/rewind bar colors and the dark edge on
enemy health bars (enemies show a bar only once hurt; `healthBar.aboveBody` places it over the body). Pop text in the
world and the generated loading screen pick up the theme font and ink outline too.

Anchors are fractions of the screen (`[0,0]` bottom-left, `[1,1]` top-right); offsets and sizes are pixels in the
1920x1080 reference space, which scales to fit any screen (and stays inside phone safe areas; portrait: below). Button labels:
`mainMenu.json` `labels`; card texts: `captionFormat`/`playText`/`lockedText`/`clearedText` (selection screens) and
`buyFormat`/`maxedText`/`comingSoonText` (shop). Level and video cards show the level's/video's `thumbnail` sprite.

### Phones, portrait and touch (one layout system)

Every button, chip, bar and text the menus and the HUD place goes through `MenuResponsive` (and lists through
`MenuListLayout`), with the numbers in `ui/theme.json` `responsive`:

- **Sized on the short side.** Landscape lays out in `referenceResolution` (1920x1080); a screen taller than wide lays
  out in `responsive.portraitReferenceResolution` (1080x1920), so its width (the short side) sets the scale and a
  button is the same physical size held either way.
- **Inside the safe area, always.** Everything is kept inside its parent (the safe area, a popup, a panel) and shrunk to
  fit it (texts shrink their words). Portrait keeps `portraitMargin` from the edges; `bottomMargin` and
  `webTopMargin`/`webTopMarginPortrait` keep clear of the home bar and the web page's beta/build badge.
- **Thumb-sized.** On a portrait screen every button is at least `minTouchSize` (124 of 1080 ≈ 44 pt on a 390 pt phone);
  `touchSizeInLandscape` does the same in landscape (off, so landscape keeps its look).
- **Portrait placements.** Each `title`/`subtitle`/`decoration` has a `portrait` placement (`use`, `hide`, `anchor`,
  `offset`, `size`), each `list` a `portrait` list (`direction`, `columns`, `itemSize`: a row becomes a column, a wide
  grid two columns), and each layout `portraitBack`/`portraitStats`. With `use` off the landscape placement is used,
  fitted. The HUD's placements are `hud.landscape` and `hud.portrait` (health, diamonds, pause, rewind, floor, boss bar,
  objective, banner, subtitle, rewinding, hint, progress bar): in portrait the game is a 16:9 band in the middle, so the
  readouts stack above it and the thumb controls (rewind, the progress bar) sit below it. `responsive.screens` has the
  pause list, settings panel, results, upgrade picker and death scene's Rage Quit in portrait.

Check it with the `layout` playtest (run with device "phone", 390x844): it opens each screen and reports any button that
overlaps another button or a HUD panel, leaves the screen or is smaller than the touch size.

```json
"decorations": [ { "text": "Now with
rewind!", "shape": "Burst", "fill": "#FFC93C", "textColor": "#1E1220",
                   "borderColor": "#1E1220", "borderWidth": 6, "anchor": [0.92, 0.58], "rotation": 14,
                   "motion": { "type": "Pulse", "amount": 0.06, "speed": 1.2 } } ]
```

## Sound effects and voice lines (ElevenLabs, through Hawkes Gen)

Every sound lives in `audio.json` as a prompt; the files are generated, never hand-placed:

```json
{ "id": "player_shoot", "kind": "Effect", "prompt": "Punchy retro arcade blaster shot, short fiery whoosh and pop",
  "durationSeconds": 0.5, "promptInfluence": 0.5, "volume": 0.55, "pitchVariation": 0.08, "maxVoices": 4, "cooldown": 0.05 },
{ "id": "vo_final_wave", "kind": "Voice", "prompt": "Final wave! Probably." }
```

- **Make them:** SickOfIt > Audio > Sound Generator (Hawkes Gen must be running: `C:\Projects\hawkesgen\auto-start.bat`;
  it holds the ElevenLabs key), or the `GenerateSounds` batch command. It fills in `clip` and `generatedFrom`; editing a prompt (or duration, influence, loop,
  voice) marks the sound "Outdated" so it gets remade. Files go to `outputFolder` (Assets/Audio/Generated).
- **Effects** use ElevenLabs text-to-sound-effects: describe the sound, 0.5-30 s (the API rejects shorter) (`durationSeconds` 0 = let it decide),
  `promptInfluence` 0-1 (higher = more literal), `loop` for seamless loops. **Voices** use text-to-speech: `prompt` is
  the line, `voiceId` (or `defaultVoiceId`) the ElevenLabs voice.
- **Writing prompts that sound good** (from ElevenLabs' guidance): describe one physical sound plainly: source,
  material, action ("Gooey slime blob bursting: a juicy squelchy pop with a small wet splatter"). Add audio terms:
  *one-shot*, *impact*, *whoosh*, *close-up*, *dry*, *no reverb*. Say *no music* / *no melody* and avoid instrument
  words (trombone, brass, horn, fanfare) unless you want instruments. For two-part sounds, list them in order ("a bonk,
  then a body flopping onto the ground"). For very short sounds (UI ticks, clicks) use the API minimum of 0.5 s and say "very short" in the prompt, and use `promptInfluence` 0.6-0.7 for
  literal results.
- **Auditioning:** "3 takes" in the Sound Generator makes three variations into `Takes/`; play them and press
  **Use** on the best (each take costs a generation).
- **Voices and spoken lines:** `voices` in audio.json defines who speaks: `player` (the hero's dry, sick-of-it inner
  voice, used for subtitles) and `announcer` (the arcade announcer, used for banners). Each has a `description`; in the
  Sound Generator press **Design 3 candidates**, play them, and **Use** the right one (it's saved to your ElevenLabs
  account and its id goes into `voiceId`). Or paste any voice id from your ElevenLabs library. Lines use `eleven_v3`, so
  a line's `prompt` can carry delivery tags: `"[sighs] They brought friends. [deadpan] Rude."` (also [sarcastic],
  [annoyed], [shouting], [excited], [whispers], [laughs], [pause]). To voice on-screen text, give the `showText` action a
  `sound`: `{ "type": "showText", "text": "They brought friends. Rude.", "style": "Subtitle", "sound": "vo_l1_friends" }`
  and add that sound (`"kind": "Voice", "voice": "player"`). Lines play one at a time; a line that would start more
  than 2.5 s late is skipped.
- **When they play:** `events` maps game moments to sound ids (playerShoot, enemyDie, rewindStart, levelClear, uiClick,
  ...; empty = silent). Per enemy: `sounds.hurtSound` / `sounds.deathSound`; pickups: `collect.soundId`; scripts:
  `{ "type": "playSound", "sound": "vo_final_wave" }`. Ids are validated on import.
- **Hand-made sounds** (a clip processed from generated ones, e.g. the prologue's echo `vo_portal_echo` and
  `portal_swallow`): leave `prompt` empty and set `clip`, so the Sound Generator never remakes it, and keep the
  recipe as a script (`Tools/Audio/portal-mix.sh`, ffmpeg) to re-run when a source clip is remade.
  The portal echo is made from the hero's own "Hello?" (`vo_portal_hello_1`) by `Tools/Audio/portal-mix.sh`.
- Players control Volume / Music / Effects / Voices, screen shake and hints in Settings (main menu and pause menu).

## Game feel

Built in, no config needed: enemies pop into bits on death (`visual.fxColor` sets the color, `visual.deathPop` turns it
off), sparks on hits, muzzle flash and dust on jump/land, a red flash + shake + hit-stop when the player is hurt, the
player blinks while invincible. Screens: pause (Esc/P/Start), settings, the end-of-level results with a grade
(`ui/theme.json` `grades`: S..D thresholds and quips; score = 100 - 12 per hit - 4 per rewind), and the "undo your death"
countdown (`player.json` `deathRewindWindow`). Every on-screen word is in `ui/theme.json` `texts`.

## Checking it plays

- `Logs/GameConfig.log` after every edit (errors have file, line, field, and suggestions).
- Bot playtest of every level: `-runTests -testPlatform PlayMode -testCategory Playtest` (see AGENTS.md), then read
  `Logs/Playtest/report.md` and the per-level `*.summary.json` (kills, damage taken by source, outcome).
- Rewind is automatic for everything here: content can't break it.
