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
`levels/Level3.json` (the old 10-floor Spill) as the reference; a floor with a path (one level = one floor, the
layout every new floor uses) is "A floor with a path", with `levels/Floor1000.json` (The Landing) as the reference.

1. Copy an existing floor to `levels/Floor<floor>.json` (a new floor of Hell: one level = one floor, e.g. `Floor900.json`).
   Change `levelName`, `displayName`, `levelNumber`, `music`, `backgrounds`, `deathConfigName`.
2. Add `"Floor<floor>"` to the `"levels"` list in `game.json` in climb order (today the story floors are positions
   2-31, `Floor1000` to `Floor901`), before `CrackedRoad`, the old 10-floor stages (`Level*`) and the `Arcade*` levels
   (the position is the level number: Docs/Story.md, *Level numbers*). Inserting shifts every later level's number,
   so fix the `nextLevelNumber`/shortcut `nextLevel` values and the playtest's level constants that point past it. A level goes on to a video with
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

**A Warden's arena** (`levels/Floor901.json`, the Magma King, level 31; Story.md "The Magma King's fight") goes further:
- A floor stage on a **Static** camera: `floors` with `unitsPerFloor` 0 (the counter only moves by `climbFloors` /
  `setFloor`), no `floors.sign`/`door` (those need a travel track). The playtest treats it as an arena (`IsArena`).
- **The intro** is a scene: `lockInput`, `lockRewind`, `sceneUntouchable` on the player, spawn the boss and
  `setState` him into an intro state (`emerging`: a `hover` with `aroundSpawn` and `offset` that lifts him into
  place; `waiting` until a `signal`), `cameraTo`/`cameraReset`, the lines, then `bossBar` and `goToPhase` to the fight.
  His first state is the fight, so a level that just spawns him (the arcade fight) works too.
- **Phases are boss states** with `healthBelow` transitions; each attack is its own short state that cycles to the
  next with `after`. **Telegraphs:** in that state, a `shoot` of a harmless warning glow (team Neutral, `speed` 0,
  `aim` Down, `initialDelay` 0; a `line` pattern lays a row along the floor) and the real `shoot` with
  `initialDelay` 1 and `fireOnEvent` on his clip, so the shot leaves 1 s + the clip's wind-up after the glow.
- **Signals for the playtest:** `emit` from his states (`mkStand`, `mkEnrage`, ...) and from the script; the level's
  rules can answer them too (the hero's line at the first flood).
- **The ending** is an `allDead` rule → a defeat phase (lock input and rewind, `slowMotion`, lines, `climbFloors`
  ticks to the top floor, the Waystone beat), then `offerUpgrades` and a `cleared` counter as the objective. A level
  ending on a Circle's top floor reaches that Waystone when cleared (`RunFlow`); the Shaft's climb to it wakes the
  Circle above (`ui/shaft.json` band `wakeVoice`, theme `shaftWakeFormat`).

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
  `if` / `counter` (Level1.json's `portalRight`, measured from the portal's spot with `"anchor": "Camera"`). Once he
  is pulled in, the hero stays hidden at the pull point, so `"anchor": "Player"` places things at the portal (Yooba,
  the scorch) even after a `cameraTo`.
- Something on a spot painted in the level's background (Level 1's portal on the footage's fire hole): spawn it with
  `"anchor": "Camera"`. A Static level's background video is centred on the camera's starting view and as tall as it
  (2 x the ortho size, 5.6 at 2.8; 16:9 footage is 9.96 wide), so a point at fraction (u, v) of the frame from its top
  left is `[(u - 0.5) * 9.96, (0.5 - v) * 5.6]` from the camera. Do it before any `cameraTo` (the anchor follows the
  camera; the background doesn't). Level 1's fire hole, (930, 1025) of 1920x1080, is `[-0.16, -2.52]`.
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

## Bystanders ("a soul turns to the hero, flinches from shots, speaks")

The hero isn't the only one a scene can direct. Three actions work on **any entity by instance id** (the spawn `id`;
`"player"` is the hero, `"self"` is the entity running its own ability), and one trigger lets a bystander or an enemy
notice what happens near it. Demo: the damned soul `bystanderSoul` (`content/entities/bystanderSoul.json`, Story.md
"Bystanders") on the bench `content/scenes/bystanderTest.json`, played by the `bystander` playtest.

**Directing it from a scene** (level rules, phases, sequences):

```json
{ "type": "walkEntityTo", "target": "soul", "relativeTo": "player", "x": 5.2, "speed": 2.5, "emit": "soulThere" },
{ "type": "faceEntity", "target": "soul", "awayFrom": "player" },
{ "type": "playEntityAnimation", "target": "soul", "state": "Hiccup" }
```

- `walkEntityTo` walks it sideways to a world `x` (from `anchor`, or an offset from the entity named in `relativeTo`,
  negative = on its left) at `speed` world units per second (0 = its own `moveSpeed`). It overrides its own movement
  abilities while it goes, faces the way it walks (`keepFacing: true` to back away without turning), and stops on the
  spot, or after 1.5 s stuck, and `emit`s a signal. It keeps its height (kinematic or gravity-less bodies; a Dynamic
  one still falls). The hero has his own: `walkPlayerTo`.
- `faceEntity`: `direction`, or `toward` / `awayFrom` another entity. An ability that faces something every tick
  (`hover`, `chase`) turns it back, so use it on entities that stand still.
- `playEntityAnimation` plays an animator state by name (the entity must have art for it: a missing state is logged
  as a scene `error`). `playAnimation` with `"target": { "self": false, "id": "soul" }` does the same; this one
  takes a plain id and is logged as a `cutscene` event. Hero: `playPlayerAnimation`.

**Reacting on its own** (inside the entity, as `react` abilities or state transitions): the `witness` trigger.

```json
{ "type": "react", "maxTimes": 0, "when": { "type": "witness", "what": "ShotPassed", "within": 1.6 },
  "do": [ { "type": "playEntityAnimation", "target": "self", "state": "Hiccup" },
          { "type": "walkEntityTo", "target": "self", "anchor": "Self", "x": 0.5, "speed": 3, "keepFacing": true } ] }
```

- `what`: `ShotPassed` (the hero's shot, or a projectile entity of another team, comes within `within` world units),
  `AllyHurt` (another entity on the watcher's own team is hurt within `within`), `HeroHurt` (the hero is hurt within
  `within`). A hurt counts for `window` seconds (0.25) after it; a shot counts while it is that close.
- Like every trigger it fires on the rising edge: a stream of shots stays one "true" until the last leaves range.
  `maxTimes: 0` makes a `react` fire each time; the default 1 is once.
- In a level rule or sequence there is no "self": name the watcher, `"watcher": "soul"`.
- Teams matter: `AllyHurt` needs a hurtable ally (health above 0; the damned can't be hurt), and a `Neutral` watcher
  sees a `Neutral` neighbour hurt. A shot of the watcher's own team is never "a shot passing".
- Rewind-safe: the walk is saved with the entity; the world keeps the last 2 s of hurts in its saved state
  (`GameplayWorld.TryWitness`), so a rewind forgets what hasn't happened yet. Checked by `BystanderTests`.
- Limits: shots made by legacy shooter prefabs (old levels without a `script`) aren't seen; projectile entities are.

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
spent per second rewound. Defaults: 10 s at the start and as the cap (Yooba's Loop Jar x5, Bottomless Jar x2 and the in-run Déjà Vu x3 take it to 30 s,
the history window `rewindMaxHistorySeconds`). Rewards: `cleanKillRefund` juice for an enemy killed while the hero hasn't been hurt for `cleanKillSeconds` (once per
creature; the HUD floats "CLEAN KILL +0.5s"), and `survivalBonus` for living `survivalSeconds` after undoing a death with a rewind ("SURVIVED +3s"; `RewindBudget.ArmSurvival`).
Quick Unwind (`RewindSpeed` shop effect) makes the rewind itself faster. Playtest `rewind-juice`. The bottom progress bar shows the level played so far (length = script `"length"`, ticks from
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
  `{ "id": "moreHealth", "title": "Goo Plaster", "description": "...", "costsPerLevel": [10, 20, 35],
  "maxLevel": 3, "effect": "MaxHealth", "amountPerLevel": 1 }`. Effects: MaxHealth (+HP, also tops up carried health),
  ExtraJump (+air jumps), MoveSpeed / FireRate / Damage (+fraction, 0.1 = +10%), RewindJuice (+seconds), Magnet (+pickup
  range). No code needed for a new one; the shop lays out a card per entry.
- **The shop is Yooba's** (Docs/Story.md, "Yooba between floors": titles are things a village slime makes from
  diamonds and his stomach). It opens from the main menu (its own scene) and from every Shaft once Yooba has met the
  hero: **Visit Yooba** beside Continue (`ui/shaft.json` `yoobaButton`/`continueBesideYooba` and their portrait
  placements, text `shaftVisitYooba`) builds the same shop over the Shaft (`UpgradeShopManager.OpenOverShaft`, no
  scene load); its Back closes it and the Shaft's Continue goes on to the next level.
- **Gating a good or a Grudge** (`requires`, shop goods in `player.json` and `content/upgrades/*.json`): a list of
  conditions, all of which must hold before it is shown (shop) or offered (Grudge picker); until then it is simply
  absent. Forms: `flag:<id>` (an `UnlockFlags` id: `UnlockFlags.Set("...")` from C#), `weapon:<id>` (the flag
  `weapon:<id>`: the weapon system sets it when a weapon is unlocked), `shop:<id>[:level]` (a good owned at that level),
  `grudge:<id>[:level]` (held this run), `level:<n>` (level n cleared). A good already owned is never hidden, so a
  save from before the gate keeps its purchases (nothing is migrated: no flags is an empty set). The content validator
  rejects a malformed condition. Flags are cleared on New Game, kept through runs. Example:
  `"requires": ["shop:spicyFireballs:2"]` (Double-Dipped Shots).
- **Prices** (Docs/Story.md, "Yooba's prices"): `RogueliteManager.TotalPrice` sums the catalog; the playtest reports it
  as `shop.totalPrice` / `shop.basePrice`, with `level<n>.diamonds` per level. Test it with `ShopGateTests`.
- Between-level picks (content/upgrades) are separate: bigger, run-long boosts offered at the end of each level.

## Status effects (poison, slow, stun, shield)

`content/effects/{id}.json`; apply with `applyStatus`, or from `contactDamage` / `projectile` / `aura` via `"effect"`.

```json
{ "schemaVersion": 1, "duration": 2, "modifiers": [ { "stat": "MoveSpeed", "multiply": 0.4 } ], "tint": "#66AAFF88" }
```

## Bouncing shots, a blink, a front shield, a sweeping laser, predictive aim

Five mechanics, all JSON, each with a test enemy in `content/entities/` and all on the test bench
`content/scenes/mechanicsTest.json` (playtest `mechanics`). Story: Story.md, "Hell's tricks". Each is rewind-safe: every
runtime field is in `SaveState`/`LoadState`, and what they draw is read back from that state every frame.

**A bouncing shot** (`ricochetEmber`, thrown by `ricochetImp`): `projectile` takes `bounces` (how many times it
mirrors its velocity off ground, walls and platforms before `dieOnSolid` applies), `bounceKeep` (speed kept per bounce)
and `bounceSound`. It needs a body moved by velocity (the default Kinematic projectile), not a Dynamic one. Entities
(stones, platforms made of entities) and the screen's edge don't bounce it; the level's own ground and walls do.

```json
{ "type": "projectile", "damage": 1, "dieOnSolid": true, "bounces": 3, "bounceKeep": 0.95, "bounceSound": "shaft_tick" }
```

**A blink** (`blinkImp`): `teleport` picks a spot, shows it for `telegraph` seconds (the body flashes; `marker` is an
entity spawned there, give it a `lifetime` and `"team": "Neutral"` like `blinkMark`), then appears there. `to`:
`NearPlayer` (`distance` to a side, or `behind` him), `Point` (`point`) or `Random` (anywhere in view that is clear).
It stands still during the telegraph and skips the blink (tries again in a second) if the spot is inside the ground.

```json
{ "type": "teleport", "to": "NearPlayer", "distance": 3.5, "interval": 2.6, "telegraph": 0.7, "marker": "blinkMark", "sound": "banner_whoosh" }
```

**A front shield** (`bulwarkImp`): `frontShield` blocks damage that comes from the side the entity faces (`arc` is the
half-width in degrees: 90 is the whole front half). A blocked shot is spent with a spark and the `blockSound`; a hit from
behind counts as normal. Which way it faces is up to its other abilities (`patrol` and `move` face the way it walks,
`faceTarget` always faces the hero, which makes it hard to get behind). `leak` lets a share of the damage through.
It works for the hero's shots and for entity damage with a source (a blast's centre counts as where it came from).

```json
{ "type": "frontShield", "arc": 90, "blockSound": "chain_cut" }
```

**A sweeping laser** (`beamLens`): `laserSweep` shows a thin warning line for `warning` seconds (where the sweep
starts: by default `sweepDegrees` centred on the hero's place when the warning began), then a beam of `width` turns
`sweepDegrees` over `sweepTime`. The beam is a real segment from the muzzle that the first ground stops, and it hurts
the hero where it crosses his collider (once per `hitInterval`). Standing still is the thing to fix. Give it
`"shootable": false, "health": 0` for a lens that can't be shot, or health for a turret that can.

```json
{ "type": "laserSweep", "damage": 1, "interval": 5, "warning": 1.2, "sweepTime": 1.6, "sweepDegrees": 80, "length": 14 }
```

**Predictive aim** (`leadImp`): `"aim": "Predictive"` on `shoot`
aims at where the hero will be when the shot gets there, leading his current velocity at the shot's `speed` (at most two
seconds ahead; with no intercept, or a standing hero, it aims straight at him). Spreads and other patterns turn around
that aim as usual.

```json
{ "type": "shoot", "projectile": "projectileEnemy", "speed": 5.5, "aim": "Predictive", "interval": 1.8 }
```

The telemetry says what happened: `bounce`, `shieldBlock`, `laser` (warning/sweep/end), `teleport` (telegraph/blink).
Try them on the bench: `?playtest=mechanics` (the bot fights the five stations in turn; each beat is a `check` event).

## Weapons ("give him fists", "a gun with ammo", "the Floor 998 pickup")

The hero's weapons are `content/weapons/{id}.json` (fields in the generated reference, "Weapon"). He always holds the
`rocketLauncher` (his gun from player.json: same fire rate, rocket speed and sprite, no ammo) and may hold more. The
equipped one decides how Fire works: `kind: "Projectile"` fires the hero's rocket with the weapon's own `cooldown`,
`projectile.speed/lifetime/damageScale` (and `multiShot`: whether the spread Grudge applies); `kind: "Melee"` swings a
hit box in front of him instead, through a **combo** of `melee.steps` (`damage`, `reach`, `windup` before the hit lands,
`recovery` before the next swing, `knockback`, `arc`/`size` of the drawn swing). A swing that comes within
`comboWindow` seconds of the last one is the next step; later it starts over. Attack-speed upgrades shorten every time.

```json
{ "schemaVersion": 1, "displayName": "Fists", "color": "#FFD27A", "kind": "Melee",
  "aimGuide": { "show": true, "length": 0.6 }, "ammo": { "capacity": 0 },
  "melee": { "comboWindow": 0.45, "height": 1.2, "steps": [
    { "name": "jab", "damage": 1, "reach": 1.2, "windup": 0.07, "recovery": 0.13, "knockback": 2 },
    { "name": "hook", "damage": 2, "reach": 1.4, "windup": 0.12, "recovery": 0.3, "knockback": 5, "arc": 150, "size": 1.5 } ] },
  "sounds": { "use": "punch_swing", "hit": "punch_hit", "equip": "ui_click" } }
```

- **Ammo:** `ammo.capacity` above 0 gives the weapon rounds (`start`, `perShot`, `regenPerSecond`); empty, Fire clicks
  (`sounds.empty`). `giveWeapon` with `ammo` adds rounds. Rewinding gives spent ammo back.
- **Aim guide:** `aimGuide.show` / `length` (a multiple of player.json's line). The guide follows the weapon: a short line
  for a melee weapon's reach, none if `show` is false.
- **Sounds:** audio.json ids in `sounds` (`use`, `hit`, `empty`, `equip`), made like any effect.
- **Visible swing:** a melee swing draws a crescent and a fist in the weapon's `color` (no art needed, `MeleeSwingFx`).
- **Level actions:** `{ "type": "giveWeapon", "weapon": "fists", "equip": true }` (adds it to what he holds, `ammo` for ammo
  weapons) and `{ "type": "takeWeapon", "weapon": "fists" }` (the launcher can't be taken). Both pop the weapon's name over
  his head (`announce: false` to stop that). What he holds is kept between floors (`WeaponProgress`) and cleared with the
  Grudges when a run ends; a rewind to before a give/take undoes it.
- **Switching:** Q, the mouse wheel, the gamepad's top button / shoulders, and the **weapon button on the HUD** (a tap on
  touch; `hud.landscape/portrait.weapon` in theme.json). It shows the name, ammo and the key.
- **Rewind:** the held weapons, the equipped one, ammo and the swing's timers are part of the world's saved state
  (`PlayerWeapons.SaveState/LoadState`). A new field there must be saved there.
- **A new gun** is a new file; by default it shares the hero's rocket prefab (only the numbers differ).
- **Blasts and shots of their own (2026-10-25):** `projectile.pellets` / `pelletSpread` fan several rockets from one pull (the
  shotgun; the aim guide shows them all). `projectile.entity` fires a **player-team entity** instead of the rocket, spawned by
  the world so rewind saves it: `ricochetShot` (a `projectile` ability with `bounces`) and `haloShot` (`returnToHero`, a
  boomerang: out for `turnAfter` seconds, back faster, caught at the hero; with `pierce` it hits both ways). Give such a shot
  `onlyHitLiving: true` so it passes through enemy bullets instead of cancelling them. The validator wants the shot on team Player.
- **Weapons found on floors:** the weapon names its `pickup` entity (a `collect` ability whose `do` is
  `{ "type": "unlockWeapon", "weapon": "spitfire" }`), plus `banner` (default `NEW WEAPON: <NAME>`), `heroText` and `heroLine`
  for the first find. `unlockWeapon` saves it for good (`WeaponUnlocks`, kept when a run ends, cleared by New Game); every level
  starts with everything unlocked in his hands, and a rewind past the pickup can't lose it. Later finds refill the ammo. Give a
  floor's enemy the pickup by **bearer**: an `imp` variant with `drops: [{ "entity": "spitfirePickup", "chance": 1 }]`
  (`impSpitfireBearer`), or spawn the pickup in a boss's defeat sequence at `anchor: Player` so it is collected at once.
  `magnetRadius` 4.5 on the pickups pulls one dropped out of reach.
- **Taking weapons:** `takeWeapon` (the launcher too) and `takeAllWeapons` with `except` leave him only that one. They are
  *suspended* for the run (the launcher and unlocked weapons come back with `giveWeapon`, an `unlockWeapon` pickup or the next
  run); he is never left empty-handed. Floors 997-994 start with `giveWeapon fists` + `takeAllWeapons except fists`, so each is a fists
  floor however it was opened; Floor 994's landing drops the launcher back.
- **A melee floor** needs enemies that come to him: `cinder` (a chaser, one health) and `cinderBrute`, spawned with `spawnWave`
  on a section reached. The playtest bot closes in on a target when the equipped weapon is melee.

The `weapon-drops` playtest plays all of it (998's theft, the fists floors, each drop floor, a death, then a bench where each weapon
shows what it does: `content/scenes/weaponArena.json`, `targetDummy`). Try it on the bench: `?playtest=weapons` (the scene `weaponsTest`: launcher, `giveWeapon`, Q, a punched imp, a rewind,
the HUD button, `takeWeapon`; each beat is a `check` event).

## The Codex (what the hero has seen: `ui/codex.json`, entity `note`)

Story: Story.md, "The Codex". The screen (`CodexPanel`, `Gameplay/Presentation/CodexPanel.cs`) opens from the main menu and the pause menu
(`showOnMainMenu`, `showOnPauseMenu`), four tabs, a page of rows (4 in landscape, 7 in portrait: `rowsLandscape`/`rowsPortrait`, paged with < >).

- **Enemies and bosses** are the entities with a `displayName` on the enemy team (a `"boss"` tag makes it a boss). Types that share a name share
  a row. Each row: the picture (first frame of the entity's first animator state), `displayName`, the hero's `note` (new entity field: one deadpan
  line, inherited by `extends`, so give a child its own), the weakness (`hint`, the same words as the first-sight subtitle) and the tally. A new
  enemy gets a `note` next to its `displayName` and `hint`. A boss gets a `displayName`, `hint` and `note` and, if it extends a base with `hintOnce`,
  `"onSpawn": []` (bosses introduce themselves; the Codex is the only place their hint shows).
- **Weapons** are `content/weapons/*.json` (name, `heroText` as the note, `description`, the pickup entity's art); met = the launcher, or
  `UnlockFlags` `weapon:<id>`. **Characters** are listed in `ui/codex.json` `characters` (`unlock` Always / YoobaMet, optional `sheet` for the picture).
- **Met, killed**: `CodexProgress` (PlayerPrefs `Codex_Tally`, cleared by New Game) counts spawns and kills by `displayName` once per creature
  (`GameplayWorld.TryGrantOnce`, so a rewind does not count twice; silent in scenes). A type is known if `EnemyHints` (the hint set) has its name
  or the tally says met. Unmet rows are `???` in a black silhouette and show nothing else.
- Words are in `ui/codex.json`; the look is `ui/theme.json` (`card` surface, `heading`/`body`, `smallButton`, `accent`, `muted`). Playtest `codex`.

## The hero sizes them up ("a name and a weakness the first time it shows", `hintOnce`)

The first time a kind of enemy shows up in a save, the hero sizes it up: a subtitle with its name and one small hint at
its weakness, and, once the clip exists, his voice line. Story: Story.md, "The hero sizes them up". Three fields on the
entity (`content/entities/{id}.json`) and one action:

```json
{ "schemaVersion": 1, "extends": "shooter",
  "displayName": "Imp", "hint": "Slow embers. Keep moving.", "hintVoice": "vo_hero_hint_imp",
  "onSpawn": [{ "type": "hintOnce" }] }
```

- `displayName` is the type's name and its key in the save (`EnemyHints`, PlayerPrefs `Enemies_Seen`, like
  `ShortcutProgress`). Entities that `extends` another share its name and so its one hint (the bearer imps, the guards
  of a count: all "Imp" / "Imp guard"). Give a child its own `displayName` and `hint` for a hint of its own
  (`impSack`, `impSwing`). `"displayName": ""` makes a child silent (the fleeing imp, bosses: they have intros).
  `onSpawn` is a list and a child's replaces the parent's, so put `hintOnce` on the base and leave it off the children.
- `hint` is the weakness in a few words, the hero's own words, short and deadpan. The subtitle reads `IMP: Slow embers.
  Keep moving.`. `hintVoice` is an `audio.json` voice (`player`, id `vo_hero_hint_<entity>`); it can be missing or have no
  clip yet and the line shows as a subtitle only. Generate the clips with the Sound Generator (`-sounds vo_hero_hint_imp`).
- `hintOnce` with no `entity` talks about the entity it runs on (`onSpawn`, self). A level rule can name one:
  `{ "type": "hintOnce", "entity": "blinkImp" }` (say it before the first one spawns).
- **Once per save.** The set is saved at once and is not part of the rewind: a rewind past the spawn, a death, a new run
  from a Waystone and the next imp do not say it again. Only New Game (`ResetAllRogueliteData`) clears it.
- **Never over a scene.** It stays silent, and does not use the type up, while a scene (`content/scenes`) is playing,
  while a script has locked input or rewind (`lockInput`, `lockRewind`) and while rewinding; the type is then
  announced the next time it spawns where he can speak.
- The telemetry records `hint` (subject = the name, detail = the subtitle). Check it with `?playtest=enemy-hints`
  (Level 1's imps: New Game clears it, the first imp is announced once, a rewind, the second imp and a new run do not repeat it,
  the bystander scene stays silent; each beat is a `check` event).

## Replay modes ("Endless Floor", "a daily floor", "New Game+")

The story is Docs/Story.md "Again". Three main-menu buttons, each opening a card (`ReplayPanel`: the pitch, the best / today's twist and table / the cycle, then Play).

- **Endless Floor** is the level `levels/EndlessFloor.json` (listed last in game.json): `script.replay: "Endless"` and one `generateFloors` with `climbs` (every push fought off takes the
  hero up a floor), `rampPushes` (the Circle's easiest-to-hardest ramp over that many pushes), `rampStartFloor` (where the enemy tables start, so the imps come at once), `escalation`
  (past the ramp, "heat" per push: bigger pushes, more alive at once, tougher, faster, shorter breathers) and `bothSides`. The best floors climbed are saved (`ReplayModes.EndlessBest`).
- **Daily Challenge** is `levels/DailyFloor.json`: `script.replay: "Daily"`. The world's seed is `ReplayModes.DailySeed(date)` (a stable FNV hash of `yyyyMMdd`, UTC), the day's twist is
  `game.json` `replay.modifiers[seed % count]` (enemy health / attack / speed / shot speed / size, extra alive and in a push, shorter breathers), and every attempt is a line in the
  date's local table (`ReplayModes.DailyTable`, PlayerPrefs, best ten per day, the last 14 days). A twist is data: add one to `replay.modifiers`. Score = floors, kills, grazes and a bonus for finishing (`replay.score*`).
- **New Game+** is not a level: `RunFlow.NewGamePlus` (offered once the Waystone on 901 is lit, `replay.ngPlusAfterWaystone`) resets the run, the Waystones, the cleared floors and the Shaft's marker
  (`RogueliteManager.StartNewGamePlus`) but keeps held and unlocked weapons, Yooba's goods, diamonds, Codex and floor ranks, counts a cycle up (`ReplayModes.Cycle`, cleared by New Game) and starts at the Landing.
  `GameplayWorld.Scale` (`ReplayModes.ScaleForCycle`: +35% health, +12% fire rate, +5% speed per cycle, a bigger crowd every second cycle) is added to every enemy with health at `Spawn`.
- Neither replay level is a run: `RunFlow` ignores them (a death plays them again, no Waystone, no Shaft), finishing the Daily saves no level progress, and the all-levels playtest runs skip them (`IsSideFloor`).
- Playtest `replay-modes` (~20 min): Endless saves a best, the Daily is the same twice and keeps a table, New Game+ keeps a weapon and scales Hell. Tests: change the date with `ReplayModes.DateOverride` (playtest only).

## A different kind of level space

`"world"` in the script: `"camera": "Follow"` with `boundsCenter`/`boundsSize` for a wider arena, `"Scroll"` with
`scrollSpeed` for an auto-scroller, `"Travel"` for a stage that goes somewhere (next recipe), and `"playerGravity": 0.5`
for floaty jumps.

## A stage that climbs through Hell's floors ("make Floors 975-951")

Story stages are **travel stages**: the camera follows the player forward (never back), the ground runs along with
it, platforms and scenery stream in ahead, and the **floor counter** ticks down as they go (HUD `FLOOR 973`, floor
signs in the world). Between hand-written story beats the **floor generator** fills the floors from the stage's
**Circle** (`content/circles/{id}.json`). Working example: `levels/Level3.json` ("Floors 990-981: The Spill") in
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

   The generator stops at `untilFloor`, waits for its last push (at most the difficulty's `pushTimeout`, so an enemy stranded out of reach never holds the stage), then emits `floorsDone`. A beat that waits on `allDead` gets a safety rule beside it, `after` 40 s (60 on an arcade final) → `despawn` the same tag, so stragglers can't soft-lock it. While one of its pushes is
   alive the camera holds (`world.holdForPushes`), so the player fights instead of running past. Give a second
   stretch `"seed": 1` so it differs. End the stage with `{ "type": "floorReached", "floor": 951 }`.
4. Floors outside travel: `setFloor`, `climbFloors` (a shortcut, an elevator); triggers `floorReached`,
   `distanceReached`; counters `floor` and `distance`.
5. **Dress the stage** (the old stages `Level3`-`Level10` do all of these):
   - `world.props`: scenery for this stage only, drawn with the Circle's (same fields as a Circle's `props`): the
     Spill's fence posts, cart and smoking hay. A prop with `sizeVariation` 0, `chance` 1 and `spacing` =
     `unitsPerFloor` stands identical on every floor (the Loop's "same rock").
   - `floors.sign`: words carved on a big sign where the hero lands (`"IMPS ONLY"`).
   - `floors.door`: words carved over a big door at the top of the stage, standing on screen once the camera has
     stopped at `travelLength` (the Heat Stairs end at the Magma King's `"THE SLAG THRONE"`). End such a stage on
     `{ "type": "distanceReached", "units": travelLength - 0.5 }` instead of `floorReached`, so the camera has
     arrived and the door is in view when the last beat plays.
   - `world.travelSpeed`: the camera also moves on its own at least this fast, and the screen's left edge pushes the
     hero along (the Crust's sinking slag: between fights he can't stop). Generated pushes still hold it.
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

## Weather and life ("ash in the Wastes", "runes in the air", "demons far away", "make a layer drift slower than the camera")

A Circle's air is `ambience` in `content/circles/<id>.json`; it is drawn on every floor stage in that Circle (`CircleAmbience`, made by
`GameplayWorld`), behind the action, and is presentation only (no rewind, no simulation). Three lists:

- `depth`: rows of silhouettes (`shape` or `sprite`, `color`, `size`, `spacing`, `baseY`) at their own `parallax` [x, y]. 1 = stuck to the
  screen, 0 = fixed in the world, 0.9 = far away, drifting by slowly. The row repeats around the camera, so it covers any distance
  (a `spacing` y above 0 repeats it upward too, for Up-travel stages). `hanging` for stalactites.
- `weather`: bits in the air. `kind` is `Ash` (sinking flakes), `Sparks` (flickering, rising), `Cinders` (heavy, glowing, falling, turning),
  `Runes` (glyphs floating up and pulsing) or `Grit` (pebbles blown sideways). `count` alive at once, `speed`, `wind`, `sway`, `parallax`
  (negative = close and rushing past), `fromFloor` (counting down, 990 = from floor 990 up), `inFront` (draw over the hero).
- `life`: `behavior` `Flyer` (crosses at a height, flapping), `Walker` (along a far ledge) or `Chase` (a small figure and a bigger one after
  it); `shape` `Wing`, `Figure`, `Horned` or `Wisp` (built-in silhouettes, or `sprite`), `size`, `speed`, `height` (fractions of the view),
  `interval` (seconds between appearances), `parallax`. The first one shows within a few seconds of arriving.

The budget: **at most 150 weather bits** over a Circle (`CircleAmbience.MaxBits`, half on phones) and **8 actors**; everything is pooled when the stage
loads and moved in one LateUpdate (no allocation). Keep a Circle to two or three kinds of weather. Each Circle has its own set (a test fails when two
share one): Wastes = ash, sparks, cinders; Cracked Road = shed grit, sparks from the cracks, Rift ash; Rune Bridges = runes, rune dust, cold sparks.

`BackgroundLayerConfig.parallaxFactor` works the same way for a level's or Circle's background layers (`ParallaxController`): 1 is glued to the screen
(the default, and what a Travel stage always did), 0 is fixed in the world, in between a far layer. A layer that follows less than 1 must be bigger than
the view (`scale` above 1) or a tiled static sprite (`tileHorizontally`, `tileVertically`), or its edge would show; on a moving camera the controller holds
it to the screen by its own edge. Videos can scale up for room; they cannot tile.

Check it: `Tools > Run Tests` (`AmbienceTests`: the parallax rule, every Circle has weather, life and depth within the cap and none share a set) and the
`ambience` playtest (below), which also measures the frame time with the air on, off and on again.

## The hero's moves ("a dash through a bolt", "wall-jump up a shaft", "unlock a move")

Two moves on `PlayerController`, configured in player.json `moves` (`HeroMovesConfig`): **dash** and **wallJump**. Each is locked until it is unlocked.

```json
"moves": {
  "dash":     { "distance": 2.6, "duration": 0.16, "invulnerableSeconds": 0.26, "cooldown": 0.9, "airDashes": 1,
                "keys": ["LeftShift", "RightShift"], "unlockWhen": [], "firstLine": "vo_hero_dash_first" },
  "wallJump": { "slideSpeed": 1.3, "kickSpeed": 5.2, "kickUp": 9.0, "kickLockSeconds": 0.2, "wallCheckDistance": 0.12, "coyoteSeconds": 0.1,
                "unlockWhen": ["level:6"], "firstLine": "vo_hero_wall_first", "unlockLine": "vo_hero_wall_unlock" }
}
```

**Unlocking.** `HeroMoves.Arm` runs when a floor starts: a move is his if its flag (`move:dash`, `move:wallJump`, `unlockFlag`) is set, a shop good with the matching effect is owned
(`"effect": "Dash"`: the Slimed Lining, `requires level:4`), or every `unlockWhen` condition holds (the same forms as a shop good's `requires`: `level:6`, `flag:x`, `weapon:x`).
A move that just became his writes its flag, so it stays (cleared by New Game). For a new story unlock, give `unlockWhen` a condition, or add a `setFlag` action
(`move:wallJump`) to a rule on a floor and re-arm by loading the next one. The `unlockLine` / `firstLine` are audio.json voice ids (the prompt is the subtitle); they are
silent in scenes (`SceneId`).

**Input.** Dash: Shift (keys in `keys`), the gamepad's B or left stick click, the HUD's Dash chip (`hud.landscape.dash` / `hud.portrait.dash` in ui/theme.json; `GameplayHud.Dash.cs`). R is taken by rewind;
don't rebind it. Wall-jump uses Jump while clinging; pushing toward the wall is what makes him cling. The bot does not use either (it never presses Shift); the playtests press keys.

**What a dash does.** Flat burst at `distance / duration`, no gravity during it, `DashInvulnerable` for `invulnerableSeconds` (`PlayerHealth.IsInvincible` includes it, so every damage path honours it), and hostile shots
pass through him (`Entity.Phased`; the `projectile` ability skips a phased hero, so the shot flies on instead of dying on him). Cooldown `cooldown`; `airDashes` in the air, refilled on landing and by a wall kick.
**What a wall does.** Airborne, not held, pushing sideways: a box cast of the body toward that side finds a solid, non-trigger, non-one-way collider on the level layers within `wallCheckDistance`. That is a cling
(`PlayerController.WallSide`, the fall capped at `slideSpeed`); Jump while clinging (or within `coyoteSeconds` of losing the wall) kicks.

**Rewind.** The controller's own flags are not recorded, so the moves' timers live in `PlayerController.MoveState` and `GameplayWorld.SaveState` saves it through `HeroMoves.SaveState`
(times are game-clock seconds). Anything new in the moves must be added to `MoveState`, `HeroMoves.SaveState`/`LoadState` and `Get/SetMoveState`, or a rewind will not put it back. Unlocks are for good and never undone.

**Animations.** player.json states `Dash`, `WallSlide`, `WallJump` play over the Animator (`PlayAnimatorState`, like the climb poses; `PlayerController.MovePose` names the one playing). Placeholders reuse the
Walk, Grabbed and Jump sheets until the clips in `art/hero.json` are generated (GPU, priced). **Sounds:** audio.json events `playerDash`, `playerDashReady`, `playerWallCling`, `playerWallJump`.

**Level design.** A wall is any solid entity: `content/entities/wallSlab.json` (0.6 x 9, not one-way). Make a shaft with two of them about 2.4 apart; the kick crosses 2.4 comfortably. Spawn them from a path or a rule.
Telemetry `move` events: `dash`, `wallCling`, `wallJump`, `unlocked`, `line`.

**Check it:** playtests `dash`, `wall-jump` and `hero-moves` (both; ~4 min), on `content/scenes/moveTest.json` (Level 2's stage): locked, then unlocked the way a player does (Yooba's good, bought; Floor 996 cleared), a bolt dashed through unharmed,
cooldown, a shaft climbed by wall-kicks, and rewind into a burst, to before it, and back to a cling.

## The Hook and the air kit ("grapple across a gap", "wind", "a moving platform", "collapsing floor", "gravity flip")

All JSON; no C#. Story: Docs/Story.md "The Hook and the air kit". Level rules: Docs/LevelDesign.md "The air and the paths". The bench is `content/scenes/kitFloor.json`.

- **The Hook** is player.json `moves.grapple` (`GrappleConfig`: `keys` E and F, `range` 7.5, `aimCone` 50, `maxRope`, `gravity`, `swingPump`, `reelSpeed`, `pullSpeed`, ...). Locked until `unlockFlag` (`move:grapple`) or every `unlockWhen`
  condition holds (`flag:hook.found` today). Give it with the `hookPickup` entity (its `collect` writes the flag with `setFlag`), or put `"flag:..."`/`"level:N"` in `unlockWhen`.
  `PlayerController.GrappleUnlocked`, `RequestGrapple()` (a HUD chip would call it), `GrappleAim()`; the world's side is `HeroGrapple` (`GameplayWorld.Grapple`: `Attached`, `Pulling`, `AnchorSerial`, `RopeLength`, `Velocity`).
- **An anchor** is an entity with the tag `anchor` and the `grappleAnchor` ability: `"mode": "Swing"` (a rope of the length he hooked it at) or `"Pull"` (reels him in, hops him up), optional `reach` and `ropeMax`. Spawn it like any piece:
  `{ "entity": "grappleRing", "at": [0, 2.1], "id": "swingRing" }`. Controls: E throws, A / D pump, up / down reel, **Jump** lets go with the swing's speed and a hop, E again lets go. A swing anchor on a `mover` swings with it.
- **A moving platform** is an entity with `mover`: `{ "type": "mover", "waypoints": [[3, 0]], "speed": 1.6, "pause": 1.2, "mode": "PingPong" }` (waypoints are offsets from where it spawned; `Loop` goes round; `holdUntilBoarded` waits for him). Make it one-way solid like `moverSlab`.
  `waypoints` with several points make a path; for a vertical lift use `[[0, 3]]`.
- **Wind** is an entity with `wind` (a non-solid body is the zone: stretch it with the spawn's `scale`): `"push": [2.2, 0]` (x: sideways speed added to the hero, on the ground too; y: lifts or presses him), `"shotPush"` (bends every shot in the zone along the
  wind, his and Hell's, in units per second squared), `"gustPeriod"` / `"gustDuty"` / `"gustRamp"` / `"phase"` for gusts (a steady wind has no period). `windGust` and `windSteady` are examples.
- **A collapsing floor** is a row of `crumble` stones with the same `group`: the first stood on cracks, the rest follow nearest first, `chainDelay` seconds per unit; `rebuildAfter` > 0 brings them back. (`collapseSlab`.)
- **A gravity-flip zone** is an entity with `gravityFlip` (`gravityPad`; `linger` seconds after he leaves). Put a solid, not one-way, ceiling over it (`ceilingSlab`). The hero's `GravitySign` is -1 inside; jump goes down, he walks the ceiling.
- **Rewind** needs nothing: every runtime field of every piece is saved (`SaveStateCoversEveryRuntimeField`), the rope and which way gravity pulls are saved with the world, wind and gusts follow the game clock.

**Check it:** playtest `floor-kit` (~6 min, Level 2's stage with the scene `kitFloor`): swing across a 6-wide gap (with a rewind mid-swing), a pull up to a shelf, a gust ridden over a gap with a hostile bolt and his own shot bent by the wind, a mover (with a rewind mid-ride), a collapsing floor
(rewound to whole, then crossed, then rebuilt), the ceiling (rewound on it and to before the zone), and the landing. Each beat is a `check` event; `grapple` and `floorkit` events log every hook and piece.

## Ladders ("a ladder up the wall", "climb, shoot, jump off")

A ladder is an entity with the `ladder` ability (`content/entities/ladder.json`); its body is the climbable zone. Spawn it
like any floor piece and stretch it with the spawn's `scale`: the entity is 0.8 wide and 1 tall, so `"scale": [1.0, 4.0]`
is a ladder 4 units tall, centred on `at`. The test floor `content/scenes/ladderTest.json` (Level 2's stage):

```json
{ "entity": "ledge",  "at": [-4.5, -2.45], "scale": [3.0, 1], "id": "start" },
{ "entity": "ladder", "at": [-2.6, -0.2],  "scale": [1.0, 4.0], "id": "ladder1" },
{ "entity": "ledge",  "at": [0.0, 1.55],   "scale": [4.0, 1], "id": "shelf" }
```

Put the ladder's foot on a ledge's top (here -2.2) and its top level with the top of the ledge he climbs onto (1.8; ledges are
one-way, so he passes up through the shelf). The shelf must span the ladder's x or he steps out onto air.

**How it plays.**
- **Grab:** push up (W, up arrow, the left stick or the touch stick) while his body overlaps the zone (`grabReach` beside it), or
  push down while he stands on its top (he steps down onto the rungs). Not within `regrab` seconds of letting go.
- **Held:** gravity is off, he slides to the middle of the rungs (`centerSpeed`), up and down climb at `climbSpeed` times the stick,
  sideways does nothing. The Climb pose plays while he moves and ClimbIdle while he hangs still.
- **Shooting:** yes, slower: guns fire at `fireRate` (default 0.55) of their rate. A melee weapon is unchanged.
- **Leave:** Jump lets go with the ordinary jump, out sideways at `jumpOffSpeed` times the stick (0 = straight up); climbing past the top
  puts his feet a hair over it and he steps out; climbing past the bottom lets go on its foot.
- Telemetry `floorkit` events: `ladderGrab`, `ladderGrabTop`, `ladderJumpOff`, `ladderTop`, `ladderBottom`.

**Rewind.** What rewind restores of the hero is his transform (`RewindableTransform`), velocity, health and the world's state;
`PlayerController`'s own flags (held, gravity, jump buffer) are *not* recorded. So a hold can't live in the controller: like the
swinging chain, the *holder* keeps it. The ladder saves `holding` and `releasedAt`; each tick, while `holding`, it asks
`GameplayWorld.HoldPlayerClimbing`, which sets `held`, gravity 0, the climb poses, and the fire scale again. A rewind into a climb
therefore puts him back on the rung (position from the transform, hold from the ladder), and a rewind out of one drops the hold
the next tick (the world releases him when nothing holds him). His rung is his position, so nothing else is saved.
The `ladder` playtest checks both: back into the middle of a climb, and back from the shelf into the climb.

**Animator.** `player.json` `animationConfig` has three states, `Climb`, `ClimbDown` and `ClimbIdle` (generated sheets). They
have no transitions: the generated controller doesn't have them, so `PlayAnimatorState` plays them from their sheets
(`PlayerSceneClip`) and hands the sprite back to the Animator when he lets go. Three states now (`Climb` up, `ClimbDown`, `ClimbIdle`) with the generated sheets `Art/Sprites/Player/Climb*.png`
(604x340 cells, made by `Tools/Art/player_clip_sheet.py`).

**Under pressure.** Floors 999 and 998 use `ladderHaul` (a ladder with `climbSpeed` 1.7) as the way up for 5.2 units, the lava rising at 0.8 / 0.4 and,
on 998, the ember rain falling. Put the ladder's foot on a ledge's top and its top level with the next ledge's top, spanning the same x. The playtest bot
(`PlaytestBot.LadderStep`) climbs a ladder whose foot is at his feet and whose top is 1.2+ above them; the `ladder-floors` playtest plays both floors
(`ladder` events per grab and exit, a `check` per floor).

**Check it:** playtest `ladder` (~2 min): walks to the foot, grabs, hangs still, climbs up and down, shoots slower than on the ground,
rewinds in the climb and back into it from the shelf, jumps off, grabs again and exits at the top.

## A floor with a path ("climb a lava fall, cross the river on a raft, climb again")

The one-floor layout (Docs/LevelDesign.md, *A floor's path*): a level is one floor, and its way up turns. Working
examples: `levels/Floor1000.json` to `Floor996.json` (the first five floors) and `content/scenes/climbTest.json` (the
`climb` playtest plays it on Level 2's stage). A floor level is `levels/Floor<floor>.json`, its `floors` are `start`
its floor and `end` the floor above (the Shaft climbs one floor to it), and its landing beat sets the counter to `end`
with `"skipMilestones": true` before the Grudge pick.

Two layout rules the camera and the bot need: the camera only leaves a section once the hero is within the look-ahead
(`travelLead`, 1.2) of its end, so the last foothold of an `Up` section must stand within about 1.5 of the section's
top (a `Right` section's must reach to within 1.2 of its end), or the floor stops there. And don't hang a foothold
less than about 1.6 over another one: jumping up from the lower one bumps his head on it (the bot can't see that).

1. **The path**: a Travel `world` with `path` instead of `travelDirection`. Each section goes `Up`, `Right`, `Left`,
   `UpRight`, `UpLeft` for `length` units, or is an `Arena` (the camera holds until a `nextSection` action). `autoSpeed`
   moves the camera on by itself (a raft ride, a chase), `lead` overrides the look-ahead, `turn` rounds the corner into
   the next section. Entering a section emits `section:<name>`; the trigger `sectionReached` fires once the camera is in
   it (or past it). The floor counter stays on its floor (`floors.start`, `unitsPerFloor` 0); a path floor has no
   streamed ground: the hero lands on a spawned ledge.

```json
"floors": { "start": 1000, "circle": "emberWastes", "signs": false },
"world": { "camera": "Travel", "travelLead": 1.2, "holdForPushes": false, "path": [
  { "name": "climb", "direction": "Up", "length": 10, "spawns": [
      { "entity": "lavaSea", "at": [0, -6], "id": "lavaSea" },
      { "entity": "ledge", "at": [-4.5, -2.45], "scale": [2, 1] },
      { "entity": "sinkingStone", "at": [-0.8, -1.2], "scale": [0.9, 1] } ] },
  { "name": "river", "direction": "Right", "length": 18, "spawns": [
      { "entity": "lavaRiver", "at": [19.5, 7], "scale": [2.8, 1] },
      { "entity": "debrisRaft", "at": [7.6, 8.2], "scale": [1.3, 1] } ] },
  { "name": "chase", "direction": "Right", "length": 14, "autoSpeed": 1.3, "spawns": [
      { "entity": "lavaSurge", "at": [10, 10], "onEnter": true } ] } ] }
```

2. **Spawns**: each section's pieces, `at` measured from the path's origin (`anchor` `Path`, where the camera starts),
   its own start (`Section`) or the `World`. They appear when the section before starts (sections 0 and 1 at the
   start), so the next foothold is always there before he needs it; `onEnter` spawns when the section itself starts (a
   chase's wave). `scale` stretches a piece (`[2, 1]` = twice as wide). Tag footholds `foothold` (the toolkit's
   platforms already are): the playtest bot climbs from foothold to foothold.
3. **The toolkit** (entities in `content/entities`, abilities in the reference): `ledge` (solid), `sinkingStone`
   (`sink`), `crumblingStone` (`crumble`), `debrisRaft` (`raft`: `current`, `range`, bob, rock, tip, carries him),
   `slagConveyor` (`conveyor`), `swingChain` (`swingChain`: grab by jumping in, Jump lets go with the swing's speed),
   `chainLift` (`chainLift`), `counterweightLeft`/`counterweightRight` (spawn them with ids `weightLeft`/`weightRight`),
   `heatUpdraft` (`updraft`), `lavaFall` (`lavaFall`), `lavaSea` / `lavaRiver` (`lavaSea` + `scald`), `lavaSurge`
   (`surge` + `scald`), `floorLanding` (`carved`: `"{floor}"` and `words`). Variants: `"extends": "debrisRaft"` with
   its own `abilities` list (lists replace).
4. **Rising lava**: `lavaSea` rises at the counter `lavaSpeed` up to `lavaMax` units above where it spawned. Set both
   with `setCounter`; each time the speed goes from 0 to more it bubbles and rumbles 2 s first. Set `lavaSpeed` to 0 on
   a breather and back up for the next push.
5. **The landing beat**: `{ "when": { "type": "playerOn", "target": { "id": "landing" } }, "maxTimes": 1, "do": [ lines,
   { "type": "wait", "seconds": 3 }, { "type": "completeLevel" } ] }`.
6. **Generated sections**: `{ "generate": 3 }` in the path is replaced by 3 chunks from the Circle's `chunks` (a section
   with its spawns anchored to `Section`, `weight`, `fromFloor`), seeded by the level seed and the floor. A chunk's
   footing starts at its section's y 0 and a climb ends `length` higher.
7. **The world below** comes from the Circle's `sky` (`enabled`, colours, `seaLevel`, `seaParallax`, `layers` of
   shapes with `parallax` [x, y], `onSea` spires, `hanging` islands, drifting smoke, `embers`, `shimmer`).

Rewind needs nothing: every piece is an entity, so a stone that sank or a ledge that fell comes back. Check with the
`climb` playtest (or a `level-<n>` one for a real floor).

## A shortcut ("a collapsed stairwell that skips to Floor 840")

Docs/Story.md, "Shortcuts". A choice point inside a stage that skips floors for a cost. Working example: the top of
`levels/Level14.json` (*The Old Stair*, Floor 871 to 841). Write it in Story.md's Shortcuts table first.

1. **The offer** (`offerShortcut`, in a `runSequence` once the hero is at the spot): pauses the game in the Grudge
   picker's panel with two cards, card 1 `stayTitle`/`stayDescription` (keep climbing) and card 2 `title`/`description`
   with the cost line (`costText`, `{0}` = the Grudge it takes; `costGrudges` 0 = free). Never say how far it goes. It
   is offered once per play and the choice is final through rewinds. With no Grudge to pay, card 2 is stamped
   `cantPayText` and can't be picked. `toFloor` and `nextLevel` say where it lets him out and which stage comes next.
2. **The branch**: rules on `{ "type": "shortcutChosen", "shortcut": "<id>", "taken": true }` (and `false`) in the
   same phase, each `goToPhase` to its own phase (a sequence dies with the phase that started it).
3. **Taken**: lock input and rewind, the hero's line, walk him in (`walkPlayerTo`), the announcer's reaction, fade,
   `{ "type": "setFloor", "floor": <toFloor>, "skipMilestones": true }`, fade back, a line, then the objective's
   counter. No `offerUpgrades` (he paid a Grudge). The level then leads to `nextLevel` instead of `nextLevelNumber`,
   the run goes on from there, and the Shaft climbs to the stage's last floor and jumps a dotted line up to `toFloor`
   through the nearest crack in `ui/shaft.json` `shortcutHints` (`shortcutPath`, `shortcutSeconds`, `shortcutSound`).
   Taken shortcuts are saved (`ShortcutProgress`) and stay drawn on the Shaft.
4. **Kept climbing**: the stage's usual ending (clear banner, `offerUpgrades`, the counter).
5. The playtest bot presses 1 on a picker, so it keeps climbing; the `shortcut` scenario sets it to take shortcuts
   (`PlaytestBot.takeShortcuts`, presses 2). The level-n stage checks follow the branch played: a taken shortcut's
   counter ends on `toFloor` with no Grudge pick, and lines in phases never entered aren't expected.

## A secret floor ("a closet behind Floor 906's wall that pays a lore scrap")

Docs/Story.md, "Secret floors and gauntlets"; Docs/LevelDesign.md section 9. A side floor is a short room reached through a
door in the wall on a floor of the route, and it gives the climb back where it left it. **Use the generator**, not hand edits:
`Tools/Levels/side_floors.py` writes the room, the door on its host floor and the game.json entry from one table.

1. **Story first**: a row in Story.md's "Secret floors and gauntlets" table (the door, its host, its cost, the room, what it pays).
2. **The reward's content**, if it is new: a weapon (`content/weapons`, its `pickup` entity with a `collect` whose `do` is `unlockWeapon` and `setCounter secretFound 1`),
   a hidden Grudge (`content/upgrades`, **`weight: 0`** so no pick offers it; the room's script `grantUpgrade`s it), a lore flag (`setFlag` with a `lore.<name>` id, shown with a
   `popText`), diamonds (`addDiamonds`), a refill (`heal` + `addRewind`). Reuse sound ids that already have clips.
3. **The room**: a function in the generator returning the room's phases. Start with `enter(title, firstLine, firstPhase)`; fight with `wave(entity, count, points, tag)` and
   `when_dead(tag, nextPhase, name)` (with `stragglers(tag, seconds)` so nothing soft-locks); end with `outro(kind, closingLine)`, which emits `side:reward:<kind>` and `side:cleared` and sets `cleared`.
   A **gauntlet** is `gauntlet(waves, ...)` + `with_reward(...)`: a list of waves (each a list of `(entity, count, points)`), and between two waves a banner, `addRewind 10` and 3.4 s of quiet.
   Keep to 3-4 enemies at once, hovering ones at y 0.8-2.0 from the camera (the bot aims at them), no `world`.
4. **The row** in `ROOMS`: `file` (`Secret7`), `number` (its position in game.json's `levels`; the script appends it at the end), `host` (the floor whose landing it sits before),
   `door` (the card's text and `cost`: `("diamonds", n)`, `("grudge",)` or `("risk", "Risk: ...")`) and the hero's lines. Add the host's foothold to `HOST_DOORS` (the last ledge before the landing + 1.15 up;
   one door per host).
5. Run `python3 Tools/Levels/side_floors.py` (it is idempotent and checks entities, phases, wave points, that the room leads where its host does and sits on the floor its host ends on), then `--check`.
6. **The playtest**: a row in `SideRooms` (`SickOfItPlaytest.SideFloors.cs`) with what the save must hold afterwards. The bot opens a door only when `PlaytestBot.takeSideDoors` is set (a choice whose
   title begins `A door in the wall`), so the route scenarios (`circle-x-route`, `story-path`...) keep climbing past it.

The door's choice is an ordinary `offerShortcut` (once per play, final through rewinds, the cost paid once). A side floor never lights a Waystone (it sits on the floor its host ends on, not a Waystone's) and a death
in it ends the run like any floor.

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

### Waystones and runs (a death in Hell)

Docs/Story.md, "Waystones and runs". `RunFlow` (Core/Progression) does the rules, `WaystoneProgress` (Data) saves them.

- **Waystones** are ui/shaft.json's `bottomFloor` (the Landing) and each band's `lastFloor` under `topFloor`. A level
  whose `floors.start` is one reaches it when it starts; one whose `floors.end` is one reaches it when cleared (a
  Warden's arena ending on Floor 901). A run starts at a Waystone with the level whose `floors.start` is the Waystone
  or one floor above it (900 above 901), so a new Circle's first stage needs nothing else.
- **A death on a stage with floors** ends the run when the death scene starts (`DeathRuntime` → `RunFlow.EndRun` →
  `RogueliteManager.EndRun`: Grudges, their max health and the carried health reset; diamonds and shop goods kept);
  when the animation is over `ShaftHook` gets an `AfterDeath` stop and `ShaftScreen` plays mode `Drop` (from the floor
  he died on to the Waystone; first drop: `vo_shaft_first_drop` + `vo_shaft_first_drop_hero`, not skippable; later:
  `vo_shaft_drop`), then Continue loads the restart level. Levels without floors reload as before.
- **Death screen words**: `deaths/<Death>.json` `title` and `gloats` (`text` + audio.json `sound`, one per death in turn).
- **Main menu**: Continue opens `WaystoneProgress.RunLevel` (set when a floor stage starts or a level is cleared);
  New Run (`labels.newRun`, shown once a Waystone is reached) calls `RunFlow.NewRun()`.
- Playtest: `waystone`. The `full`/`story-path`/`wastes-climb` retries follow the drop and reopen the level they retry
  (a logged shortcut).

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
- **Music** is a level's `music` (its loop), a script's `playMusic` (a Warden's theme, its enrage: saved for rewind, so a
  rewind past it brings the old track back) and a Circle's `pushMusic`, which takes over on its floor stages while a generated
  push is on and cross-fades back after it (`StageMusic`). The Shaft's `plungeMusic`/`climbMusic` are audio.json sounds. Tracks
  are made with Hawkes Gen's `audio.music` and cut into seamless loops and stings by `Tools/Audio/music-mix.py` from the takes
  recorded in `Tools/Audio/music.json` (Docs/Story.md, "Music").
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

## Spawning off screen ("rain that is already falling when you see it", "enemies walk in, they don't pop in")

Hell may show up late but never out of nowhere: a hazard or enemy that appears inside the view is a cheap trick. The option is "view edge plus margin" (`ViewEdge` in `Content/Common.cs`, built on `GameplayWorld.View`, so it holds in landscape and portrait):

- `rain` pattern: `edgeMargin` (default 5) starts each shot that far ABOVE the top of the view; at speed 3.2 that is ~1.5 s of warning. `0` = the old `topInset` inside the view. The margin shrinks by itself so the shot (speed x `lifetime`) still arrives; a Neutral marker or a speed-0 drop (Slag Fall's warning glows) keeps the old start so its warning is seen.
- `spawnWave`: `edgeMargin` (default 0 = where `areaCenter`/`points` say). Above 0 it pushes each spawn out past the view edge nearest its spot: kinematic fliers enter from the side, everything else (walkers) drops in from above, with x kept inside the view so the Travel/Scroll edge barriers (0.5 outside the view's sides) never trap it.
- `generateFloors`: `edgeMargin` (default 5) does the same for every generated enemy, whatever the Circle table's `areaCenter` says (a 3.8 spot is inside a landscape view and beyond the barriers in portrait).

Floor 998's cinder waves use `edgeMargin: 5`. Playtest `spawn-edge` (Floor 998 and Level 13; run it on a landscape and a portrait device).
