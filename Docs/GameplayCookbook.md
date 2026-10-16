# Gameplay Cookbook

Worked recipes for common requests. Every field is listed in [GameplayReference.md](GameplayReference.md) (generated from the code).
All of these are plain JSON in `Assets/GameConfig/`: no C#, no Generate Project. Save the file, focus Unity (or run the
`Validate` command), and read `Logs/GameConfig.log`.

Working example: **Level 5, "Fire Rain"** (`levels/Level5.json`) uses most of the recipes below; its entities are in
`content/entities/` (`fireball`, `exploder`, `sludge`, `magmaKing`), its sequences in `content/sequences/`.

---

## Make a new level

First find the level's place in [Story.md](Story.md): which Circle and floors it covers, which enemies live there,
and its story beat. Follow the pacing rules there (quiet first 5 seconds, new enemies alone, breathers between
pushes). `levels/Level1.json` (The Meadow) is the reference: phases timed to the backdrop footage, one new enemy
at a time, a voiced line per beat, and a final phase that only checks for the clear after the last spawn. A stage
inside Hell (a stretch of floors) is a travel stage: see "A stage that climbs through Hell's floors" below, with
`levels/Level6.json` (The Landing) as the reference.

1. Copy an existing `levels/LevelN.json` to `levels/Level7.json`. Change `levelName`, `displayName`, `levelNumber`,
   `music`, `backgrounds`, `deathConfigName`.
2. Add `"Level7"` to the end of the `"levels"` list in `game.json` (the position is the level number).
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
**Circle** (`content/circles/{id}.json`). Working example: `levels/Level6.json` ("Floors 1000-976: The Landing") in
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

**Backdrops that change as you go**: a background layer's `sequenceBy` is `Time` (seconds), `Distance` (units
travelled) or `Floors` (floors climbed in this stage), so `"sequenceBy": "Floors", "sequenceStart": 3` swaps the
backdrop after three floors. `clipStart`/`clipLength` play just part of a long video (Circle X uses Video2's
lava-river shot, `clipStart` 17.6, `clipLength` 5).

Same seed, same floors: platforms, props and pushes come from the level seed, so rewinds and bot playtests repeat.

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
1920x1080 reference space, which scales to fit any screen (and stays inside phone safe areas). Button labels:
`mainMenu.json` `labels`; card texts: `captionFormat`/`playText`/`lockedText`/`clearedText` (selection screens) and
`buyFormat`/`maxedText`/`comingSoonText` (shop). Level and video cards show the level's/video's `thumbnail` sprite.

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
