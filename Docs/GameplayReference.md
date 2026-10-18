# Gameplay Reference

> Generated from the code by `GameplayReferenceGenerator` — don't edit by hand. See AGENTS.md for the workflow and Docs/GameplayCookbook.md for worked examples.

Gameplay is data. **Entities** (enemies, bosses, projectiles, hazards) live in `Assets/GameConfig/content/entities/{id}.json`, reusable **sequences** in `content/sequences/{id}.json`, **status effects** in `content/effects/{id}.json`, the **Circles of Hell** (biome, enemy table, hazards, difficulty) in `content/circles/{id}.json`, **scenes** (a level script played on a level's stage, for scripted scenes and their tests) in `content/scenes/{id}.json`, and each level's **script** in the `"script"` field of `levels/{name}.json`. Every content file starts with `"schemaVersion": 1`; the id is the file name. Polymorphic things (abilities, triggers, actions, patterns) are objects with a `"type"`.

Missing fields use the defaults below; unknown fields are errors. Assets are paths under `Assets/` (e.g. `"Images/Fireball.png"`).

## Contents
- [Entity](#entity)
- [Abilities](#abilities)
- [Triggers](#triggers)
- [Actions](#actions)
- [Patterns](#patterns)
- [Level script](#level-script)
- [Circle](#circle)
- [Sequence](#sequence)
- [Status effect](#status-effect)
- [Shared types](#shared-types)
- [Enums](#enums)

## Entity

### EntityDef

Anything that exists in a level: an enemy, a boss, a projectile, a hazard. File: content/entities/{id}.json. Use "extends": "otherId" to start from another entity and change only some fields (objects merge, lists replace).

| Field | Type | Default | Notes |
|---|---|---|---|
| `team` | [Team](#team) | `"Enemy"` | Which side it is on. |
| `health` | integer | `3` | Starting health. 0 = can't be damaged (most projectiles and hazards). |
| `invincibilityTime` | number | `0.0` | Seconds it can't be hurt again after a hit. |
| `moveSpeed` | number | `2.0` | Base movement speed (world units per second) used by movement abilities that don't set their own. |
| `tags` | list of string | `[]` | Labels for selecting it from scripts, e.g. "boss", "wave". |
| `shootable` | true/false | `true` | The player's shots can hit it. Only applies to team Enemy with health above 0. |
| `activationRange` | number | `0.0` | Abilities only run while the player is closer than this (0 = always). |
| `body` | [BodyDef](#bodydef) | (object) |  |
| `visual` | [VisualDef](#visualdef) | (object) |  |
| `healthBar` | [HealthBarDef](#healthbardef) | (object) |  |
| `sounds` | [SoundsDef](#soundsdef) | (object) |  |
| `abilities` | list of [Ability](#abilities) | `[]` | Abilities active in every state. |
| `states` | list of [StateDef](#statedef) | `[]` | Optional state machine. Each state adds its own abilities; transitions move between states. The first state is where it starts, unless initialStates is set. |
| `initialStates` | list of string | `[]` | Pick the starting state at random from these (deterministic per spawn). |
| `onSpawn` | list of [Action](#actions) | `[]` | Actions run when it spawns (self = this entity). |
| `onDeath` | list of [Action](#actions) | `[]` | Actions run when it dies from damage (self = this entity, still at its death position). |
| `drops` | list of [DropDef](#dropdef) | `[]` | Entities dropped when it is killed (rewind orbs, power-ups), each rolled separately. |
| `loot` | [LootDef](#lootdef) | (object) | Points dropped on death. |

### BodyDef

Physics body and collider.

| Field | Type | Default | Notes |
|---|---|---|---|
| `kind` | [BodyKind](#bodykind) | `"Kinematic"` |  |
| `shape` | [ShapeKind](#shapekind) | `"Circle"` |  |
| `radius` | number | `1.0` | Circle radius, in the entity's local units (scaled with the visual scale, as before). |
| `size` | [x, y] | `[1.0,1.0]` | Box size, in local units. |
| `offset` | [x, y] | `[0.0,0.0]` |  |
| `solid` | true/false | `true` | Blocks the player and other solid bodies. False = passes through and only detects overlaps (projectiles, hazards, auras). |
| `gravity` | number | `1.0` | Gravity multiplier for Dynamic bodies. |
| `freezeRotation` | true/false | `true` | Never rotates. |
| `rotateToVelocity` | true/false | `false` | Turns to face its velocity (arrows, fireballs). |

### VisualDef

How it looks. Without art it is drawn as a colored placeholder with its id, so gameplay can be built before the art exists.

| Field | Type | Default | Notes |
|---|---|---|---|
| `scale` | number | `1.0` | Scale applied to the whole entity (sprite and collider). |
| `sprite` | asset (Sprite) | — | A single still sprite, used when there is no animator. |
| `animator` | [AnimatorDef](#animatordef) | (object) | Animation graph: states play sprite-sheet clips; parameters and transitions switch between them, like Unity's Animator. |
| `color` | color | `"#FFFFFF"` | Tint multiplied into the sprite. |
| `placeholderColor` | color | `[0.9,0.3,0.3,1.0]` | Placeholder color when there is no art. |
| `placeholderSize` | number | `0.6` | Placeholder diameter in world units. |
| `placeholderLabel` | true/false | `true` | Show the id on the placeholder. |
| `facesLeft` | true/false | `false` | The art faces left when not flipped (most sprites face right). |
| `layer` | [SpriteLayer](#spritelayer) | `"Player"` | Draw order band. |
| `sortingOrder` | integer | `-1` | Draw order within the band (-1 = the standard character order). |
| `hurtFlash` | true/false | `true` | Flash white when hurt. |
| `fxColor` | color | `"#00000000"` | Color of the bits that fly off when it's hit or dies. Transparent = cream. |
| `deathPop` | true/false | `true` | Pop into bits when it dies (enemies with health). |
| `deathPunch` | number | `0.0` | Death clip: squash-and-stretch pop (0 = none, 0.3 = a clear pop), then it shrinks and floats up as it fades. |
| `deathFlash` | true/false | `false` | Death clip: a bright flash of fxColor at the moment of death. |
| `deathBits` | integer | `0` | Death clip: how many bits fly off (0 = the default 6). |
| `deathFade` | number | `0.0` | Death clip: seconds the last frame takes to fade out (0 = the default 0.25). |

### AnimatorDef

Animation graph, same shape as the generator's AnimationConfig: parameters, states, transitions, defaultStateName.

| Field | Type | Default | Notes |
|---|---|---|---|
| `parameters` | list of [AnimParameterDef](#animparameterdef) | `[]` |  |
| `states` | list of [AnimStateDef](#animstatedef) | `[]` |  |
| `transitions` | list of [AnimTransitionDef](#animtransitiondef) | `[]` |  |
| `defaultStateName` | string | `""` |  |

### AnimParameterDef

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |
| `type` | [AnimParameterKind](#animparameterkind) | `"Bool"` |  |
| `defaultValue` | number | `0.0` |  |

### AnimStateDef

One animation clip made from a sprite sheet.

| Field | Type | Default | Notes |
|---|---|---|---|
| `stateName` | string | `""` | **Required.** |
| `spriteSheet` | asset (Texture2D) | — | Sprite sheet texture (sliced into sprites). Frames play in sprite-name order. |
| `spriteNameFilter` | string | `""` | Only sprites whose names start with this. |
| `firstFrame` | integer | `0` | Skip this many frames (after the name filter), e.g. to play only part of a long sheet. |
| `frameCount` | integer | `0` | How many frames to play from firstFrame. 0 = through the last frame. |
| `fps` | integer | `12` |  |
| `loop` | true/false | `true` |  |
| `speed` | number | `1.0` |  |
| `events` | list of [AnimEventDef](#animeventdef) | `[]` | Events fired when a frame is reached; abilities react to them by name (e.g. a shot on the frame the arm swings). |

### AnimEventDef

| Field | Type | Default | Notes |
|---|---|---|---|
| `frame` | integer | `0` |  |
| `functionName` | string | `""` | **Required.** Event name abilities listen for, e.g. "fire". |

### AnimTransitionDef

Switches animation state when its conditions are all true (and the exit time is reached, if hasExitTime). fromState "Any" applies from every state.

| Field | Type | Default | Notes |
|---|---|---|---|
| `fromState` | string | `""` | **Required.** |
| `toState` | string | `""` | **Required.** |
| `hasExitTime` | true/false | `false` |  |
| `exitTime` | number | `1.0` | Fraction of the clip that must have played (1 = the end). |
| `conditions` | list of [AnimConditionDef](#animconditiondef) | `[]` |  |

### AnimConditionDef

| Field | Type | Default | Notes |
|---|---|---|---|
| `parameterName` | string | `""` | **Required.** |
| `mode` | [AnimConditionMode](#animconditionmode) | `"If"` |  |
| `threshold` | number | `0.0` |  |

### HealthBarDef

Floating health bar.

| Field | Type | Default | Notes |
|---|---|---|---|
| `show` | true/false | `true` |  |
| `aboveBody` | true/false | `true` | Sit just above the top of the entity's body (collider); 'offset' then nudges it from there. Off = 'offset' is from the entity's position. |
| `offset` | [x, y] | `[0.0,0.1]` | Offset in world units (from the body's top with aboveBody, else from the entity's position). |
| `width` | number | `0.42` | Width in world units. |
| `height` | number | `0.055` |  |
| `color` | color | `[1.0,0.36,0.23,1.0]` |  |
| `trackColor` | color | `[0.12,0.07,0.13,0.8]` |  |

### SoundsDef

| Field | Type | Default | Notes |
|---|---|---|---|
| `spawn` | list of asset (AudioClip) | `[]` |  |
| `hurt` | list of asset (AudioClip) | `[]` |  |
| `death` | list of asset (AudioClip) | `[]` |  |
| `hurtThreshold` | number | `1.0` | Hurt sounds only play at or below this fraction of health. |
| `hurtSound` | string | `""` | audio.json sound for hits, used when 'hurt' has no clips. Empty = the game's enemyHurt sound (enemies only). |
| `deathSound` | string | `""` | audio.json sound for deaths, used when 'death' has no clips. Empty = the game's enemyDie sound (enemies only). |
| `loopSound` | string | `""` | audio.json sound played on a loop while it is alive (a portal's hum, an engine). Stops when it dies or despawns. |
| `pitchVariation` | number | `0.1` |  |
| `volume` | number | `1.0` |  |

### LootDef

Point pickups dropped on death.

| Field | Type | Default | Notes |
|---|---|---|---|
| `chance` | number | `0.0` | Chance for each drop, 0-1. |
| `count` | integer | `1` |  |
| `value` | integer | `1` | Diamonds per successful drop (each is its own pickup). |
| `entity` | string | `""` | The pickup entity dropped. Empty = the "diamond" entity (or the old generated diamond prefab if there's no such entity). (entity id) |

### StateDef

A state of the entity's state machine.

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |
| `abilities` | list of [Ability](#abilities) | `[]` | Abilities active only in this state (added to the entity's always-on abilities). |
| `onEnter` | list of [Action](#actions) | `[]` | Actions run on entering the state (self = this entity). |
| `onExit` | list of [Action](#actions) | `[]` | Actions run on leaving the state. |
| `animation` | string | `""` | Animation state to play on entering, if any. |
| `transitions` | list of [TransitionDef](#transitiondef) | `[]` | Checked in order every tick; the first that triggers wins. |

### TransitionDef

| Field | Type | Default | Notes |
|---|---|---|---|
| `to` | string | `""` | **Required.** |
| `when` | [Trigger](#triggers) | — | When to switch. Time-based triggers (after, every, randomAfter) count from entering the state. |

## Abilities

A piece of entity behavior: movement, attacks, reactions. Combine several on one entity.

[`animParam`](#animparam) · [`aura`](#aura) · [`charge`](#charge) · [`chase`](#chase) · [`collect`](#collect) · [`contactDamage`](#contactdamage) · [`explodeNear`](#explodenear) · [`faceTarget`](#facetarget) · [`hover`](#hover) · [`lifetime`](#lifetime) · [`melee`](#melee) · [`move`](#move) · [`onAnimationEvent`](#onanimationevent) · [`patrol`](#patrol) · [`projectile`](#projectile) · [`react`](#react) · [`shoot`](#shoot) · [`spawner`](#spawner) · [`spin`](#spin) · [`wander`](#wander)

### animParam

Keeps an animator bool equal to a condition, e.g. "IsCrouching" while the player is far away.

| Field | Type | Default | Notes |
|---|---|---|---|
| `param` | string | `""` | **Required.** |
| `when` | [Trigger](#triggers) | — |  |

### aura

Every 'interval' seconds, hurts (and/or applies a status to) everything within 'radius'. Ignores hit invincibility, so it works as damage over time (sludge, fire, poison clouds).

| Field | Type | Default | Notes |
|---|---|---|---|
| `radius` | number | `0.5` |  |
| `damagePerTick` | integer | `1` |  |
| `interval` | number | `0.5` |  |
| `effect` | string | `""` | (status effect id) |
| `affects` | [AuraTargets](#auratargets) | `"Hostile"` | Who it affects. |

### charge

Every 'interval' seconds (when the player is within 'range'), stops for 'windup' seconds, then dashes at the player's position at 'speed' for 'duration' seconds.

| Field | Type | Default | Notes |
|---|---|---|---|
| `interval` | number | `3.0` |  |
| `initialDelay` | number | `1.0` |  |
| `windup` | number | `0.5` |  |
| `duration` | number | `0.5` |  |
| `speed` | number | `8.0` |  |
| `range` | number | `8.0` |  |
| `windupBool` | string | `""` | Animator bool set while winding up. |
| `chargeBool` | string | `""` | Animator bool set while dashing. |

### chase

Moves toward the player, stopping at 'stopDistance'.

| Field | Type | Default | Notes |
|---|---|---|---|
| `speed` | number | `0.0` | Speed; 0 = the entity's moveSpeed. |
| `stopDistance` | number | `0.5` |  |
| `horizontalOnly` | true/false | `false` | Only move left and right. |
| `range` | number | `0.0` | Only chase when the player is closer than this (0 = always). |
| `face` | true/false | `true` | Turn to face the way it moves. |
| `animBool` | string | `""` | Animator bool set true while moving. |

### collect

Makes it a pickup: drifts to the player when close, and when touched runs its actions (self = the pickup) and disappears. Each pickup pays out once, even if a rewind brings it back.

| Field | Type | Default | Notes |
|---|---|---|---|
| `magnetRadius` | number | `1.6` | Starts drifting toward the player inside this distance (0 = never). |
| `magnetSpeed` | number | `6.0` |  |
| `pickupRadius` | number | `0.25` | Collected inside this distance (added to the player's size). |
| `bobHeight` | number | `0.06` | Bob up and down while waiting. |
| `do` | list of [Action](#actions) | `[]` |  |
| `sound` | asset (AudioClip) | — |  |
| `soundId` | string | `""` | audio.json sound to play when collected (used when 'sound' isn't set). |

### contactDamage

Hurts hostile things it touches.

| Field | Type | Default | Notes |
|---|---|---|---|
| `damage` | integer | `1` |  |
| `interval` | number | `0.0` | Seconds between hits while touching. 0 = only when contact starts. |
| `effect` | string | `""` | Status effect applied on hit. (status effect id) |
| `knockback` | number | `0.0` | Push the target away with this impulse. |
| `dieOnHit` | true/false | `false` | Burst on touching something hostile: hits it, then dies (running onDeath, e.g. to splatter; no loot, not a kill). |

### explodeNear

When the player comes within 'triggerRadius', it arms; after 'fuse' seconds it explodes, hurting everything hostile within 'blastRadius', then dies (running onDeath, e.g. to leave sludge).

| Field | Type | Default | Notes |
|---|---|---|---|
| `triggerRadius` | number | `1.0` |  |
| `fuse` | number | `0.6` |  |
| `blastRadius` | number | `1.2` |  |
| `damage` | integer | `2` |  |
| `stopWhenArmed` | true/false | `true` | Stand still while the fuse burns. |
| `flash` | true/false | `true` | Flash while the fuse burns. |
| `effect` | string | `""` | (status effect id) |
| `animBool` | string | `""` | Animator bool set while armed. |

### faceTarget

Turns to face the player.

No fields.

### hover

Flies toward a point 'offset' from the player (or from where it spawned), bobbing up and down. Good for flying enemies and bosses.

| Field | Type | Default | Notes |
|---|---|---|---|
| `speed` | number | `0.0` |  |
| `offset` | [x, y] | `[0.0,1.5]` |  |
| `aroundSpawn` | true/false | `false` | Hover around the spawn point instead of the player. |
| `keepSide` | true/false | `true` | Mirror offset.x to stay on the side of the player it is already on. |
| `bobHeight` | number | `0.15` |  |
| `bobSpeed` | number | `2.0` |  |

### lifetime

Dies after this many seconds (running onDeath, so a fireball can leave something behind).

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `5.0` |  |

### melee

A close-range hit. With autoAttack it attacks whenever the player is in range and the cooldown is ready; the hit lands on 'hitEvent' (an animation event) or immediately.

| Field | Type | Default | Notes |
|---|---|---|---|
| `range` | number | `1.5` |  |
| `damage` | integer | `1` |  |
| `cooldown` | number | `1.0` | Seconds between attacks. 0 = keep attacking while the player is in range. |
| `window` | number | `0.35` | Seconds an attack lasts (it stands still meanwhile). |
| `autoAttack` | true/false | `true` | Start attacks by itself. False = only hit on 'hitEvent' (another ability or the animator drives the attack). |
| `hitEvent` | string | `""` | Animation event that deals the hit. |
| `animBool` | string | `""` | Animator bool set while attacking. |
| `animTrigger` | string | `""` | Animator parameter pulsed when an attack starts. |
| `effect` | string | `""` | (status effect id) |
| `knockback` | number | `0.0` |  |

### move

Keeps moving in a direction (or the direction it was launched in), optionally curving toward the player.

| Field | Type | Default | Notes |
|---|---|---|---|
| `speed` | number | `0.0` | Speed; 0 = keep the launch speed. |
| `direction` | [x, y] | `[0.0,0.0]` | Direction; zero = keep the launch direction. |
| `homing` | number | `0.0` | Degrees per second it turns toward the player (homing). 0 = straight. |
| `acceleration` | number | `0.0` | Speed added per second. |

### onAnimationEvent

Runs actions when an animation event happens.

| Field | Type | Default | Notes |
|---|---|---|---|
| `event` | string | `""` | **Required.** Event name. |
| `do` | list of [Action](#actions) | `[]` |  |

### patrol

Walks back and forth 'distance' either side of where it spawned, pausing at each end.

| Field | Type | Default | Notes |
|---|---|---|---|
| `speed` | number | `0.0` |  |
| `distance` | number | `2.0` |  |
| `pause` | number | `0.5` |  |
| `animBool` | string | `""` |  |

### projectile

Makes it a projectile: hurts the first hostile thing it touches, then disappears.

| Field | Type | Default | Notes |
|---|---|---|---|
| `damage` | integer | `1` |  |
| `pierce` | integer | `0` | Extra targets it can pass through. |
| `dieOnSolid` | true/false | `false` | Disappear when hitting ground, walls, or platforms. |
| `effect` | string | `""` | (status effect id) |
| `knockback` | number | `0.0` |  |
| `hitSound` | asset (AudioClip) | — |  |

### react

Runs actions when a trigger fires, e.g. enrage below half health. 'maxTimes' 0 = every time.

| Field | Type | Default | Notes |
|---|---|---|---|
| `when` | [Trigger](#triggers) | — |  |
| `do` | list of [Action](#actions) | `[]` | Actions to run (self = this entity). |
| `maxTimes` | integer | `1` |  |

### shoot

Fires projectile entities in a pattern. With 'interval' it attacks on a timer; with 'fireOnEvent' the shot happens on that animation frame (the timer then just starts the animation).

| Field | Type | Default | Notes |
|---|---|---|---|
| `projectile` | string | `""` | **Required.** Entity id of the projectile. (entity id) |
| `pattern` | [Pattern](#patterns) | — | Volley shape. Default: one shot. |
| `speed` | number | `5.0` |  |
| `aim` | [Aim](#aim) | `"Player"` |  |
| `direction` | [x, y] | `[1.0,0.0]` |  |
| `muzzle` | [x, y] | `[0.0,0.0]` | Where shots start, relative to the entity in world units; x is mirrored when facing left. |
| `interval` | number | `2.0` | Seconds between attacks. 0 = only when 'fireOnEvent' happens. |
| `initialDelay` | number | `1.0` | Seconds before the first attack, counted from spawning or from entering the state that has this ability. |
| `burst` | integer | `1` | Volleys per attack. |
| `burstInterval` | number | `0.1` |  |
| `range` | number | `0.0` | Only attack when the player is closer than this (0 = any distance). |
| `fireOnEvent` | string | `""` | Animation event that releases the shot. |
| `animTrigger` | string | `""` | Animator parameter pulsed when an attack starts (a trigger, or a bool set for one tick). |
| `damage` | integer | `0` | Damage per shot; 0 = the projectile's own. |

### spawner

Spawns other entities every 'interval' seconds, keeping at most 'maxAlive' of them.

| Field | Type | Default | Notes |
|---|---|---|---|
| `entity` | string | `""` | **Required.**  (entity id) |
| `interval` | number | `3.0` |  |
| `initialDelay` | number | `1.0` |  |
| `maxAlive` | integer | `3` |  |
| `offset` | [x, y] | `[0.0,0.0]` |  |
| `animTrigger` | string | `""` | Animator parameter pulsed when spawning. |

### spin

Rotates continuously (cosmetic).

| Field | Type | Default | Notes |
|---|---|---|---|
| `degreesPerSecond` | number | `180.0` |  |

### wander

Picks random points within 'radius' of where it spawned and walks to them, pausing between.

| Field | Type | Default | Notes |
|---|---|---|---|
| `speed` | number | `0.0` |  |
| `radius` | number | `2.0` |  |
| `horizontalOnly` | true/false | `true` |  |
| `pauseMin` | number | `0.5` |  |
| `pauseMax` | number | `1.5` |  |
| `animBool` | string | `""` |  |

## Triggers

When something happens. In a rule it fires the actions once each time it becomes true; in a state transition it switches state.

[`after`](#after) · [`allDead`](#alldead) · [`allOf`](#allof) · [`always`](#always) · [`animationDone`](#animationdone) · [`anyOf`](#anyof) · [`atTime`](#attime) · [`chance`](#chance) · [`counter`](#counter) · [`distanceReached`](#distancereached) · [`distanceToPlayer`](#distancetoplayer) · [`every`](#every) · [`floorReached`](#floorreached) · [`healthBelow`](#healthbelow) · [`not`](#not) · [`phaseStarted`](#phasestarted) · [`playerInZone`](#playerinzone) · [`randomAfter`](#randomafter) · [`signal`](#signal)

### after

This many seconds after the phase (or state) started.

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `0.0` |  |

### allDead

Everything the target selects is dead, after at least one of them was alive (so it doesn't fire before they spawn).

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |

### allOf

True when all of the listed triggers are true.

| Field | Type | Default | Notes |
|---|---|---|---|
| `triggers` | list of [Trigger](#triggers) | `[]` |  |

### always

Always true. Useful for a transition that should happen immediately.

No fields.

### animationDone

Inside an entity: the current non-looping animation has played to the end.

No fields.

### anyOf

True when any of the listed triggers is true.

| Field | Type | Default | Notes |
|---|---|---|---|
| `triggers` | list of [Trigger](#triggers) | `[]` |  |

### atTime

This many seconds after the level started (absolute level time).

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `0.0` |  |

### chance

Checked every 'every' seconds; true with this probability (0-1) each check. Deterministic.

| Field | Type | Default | Notes |
|---|---|---|---|
| `probability` | number | `0.5` |  |
| `every` | number | `1.0` |  |

### counter

A counter is at least 'atLeast' (and below 'below', if set). Built-in counters: kills, kills:{entityId}, spawned, spawned:{entityId}, playerHits, floor (the floor counter), distance (travel stages).

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |
| `atLeast` | number | `1.0` |  |
| `below` | number | `0.0` | Optional upper bound (exclusive). 0 = none. |

### distanceReached

Travel stages: the camera has travelled at least 'units' world units from the start (the counter "distance").

| Field | Type | Default | Notes |
|---|---|---|---|
| `units` | number | `10.0` |  |

### distanceToPlayer

Inside an entity: the player is closer than 'below' and/or farther than 'above' (0 = no limit).

| Field | Type | Default | Notes |
|---|---|---|---|
| `below` | number | `0.0` |  |
| `above` | number | `0.0` |  |

### every

Repeatedly: first after 'startAfter' seconds, then every 'seconds'. 'maxTimes' 0 = forever.

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `1.0` |  |
| `startAfter` | number | `0.0` |  |
| `maxTimes` | integer | `0` |  |

### floorReached

The floor counter is at 'floor' or higher up (a lower number). Use it for milestones and to end a generated stretch.

| Field | Type | Default | Notes |
|---|---|---|---|
| `floor` | integer | `1` |  |

### healthBelow

A target's health is below a fraction of its maximum (e.g. 0.5), or below 'amount' if set. Defaults to self inside entities.

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `fraction` | number | `0.5` |  |
| `amount` | integer | `0` | Use an absolute amount instead of a fraction (0 = use fraction). |

### not

True when the inner trigger is false.

| Field | Type | Default | Notes |
|---|---|---|---|
| `trigger` | [Trigger](#triggers) | — |  |

### phaseStarted

The phase (or state) just started. Fires once per entry.

No fields.

### playerInZone

The player is inside a box: 'center' and 'size' in world units, 'center' measured from 'anchor'.

| Field | Type | Default | Notes |
|---|---|---|---|
| `center` | [x, y] | `[0.0,0.0]` |  |
| `size` | [x, y] | `[1.0,1.0]` |  |
| `anchor` | [Anchor](#anchor) | `"World"` | World, or Camera: from the camera's center (a spot painted on a level's background, which is placed on the camera). |

### randomAfter

A random time between min and max seconds after the phase (or state) started. Rolled on entry, deterministic.

| Field | Type | Default | Notes |
|---|---|---|---|
| `min` | number | `1.0` |  |
| `max` | number | `2.0` |  |

### signal

Another rule, sequence, or entity emitted this signal (action 'emit').

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |

## Actions

Something that happens: spawn, change phase, show text, apply a status...

[`addCounter`](#addcounter) · [`addDiamonds`](#adddiamonds) · [`addRewind`](#addrewind) · [`applyStatus`](#applystatus) · [`bossBar`](#bossbar) · [`cameraReset`](#camerareset) · [`cameraShake`](#camerashake) · [`cameraTo`](#camerato) · [`cameraZoom`](#camerazoom) · [`climbFloors`](#climbfloors) · [`completeLevel`](#completelevel) · [`damage`](#damage) · [`despawn`](#despawn) · [`emit`](#emit) · [`facePlayer`](#faceplayer) · [`fade`](#fade) · [`failLevel`](#faillevel) · [`flash`](#flash) · [`generateFloors`](#generatefloors) · [`goToLevel`](#gotolevel) · [`goToPhase`](#gotophase) · [`grantUpgrade`](#grantupgrade) · [`heal`](#heal) · [`hideBossBar`](#hidebossbar) · [`hideText`](#hidetext) · [`if`](#if) · [`kill`](#kill) · [`lockInput`](#lockinput) · [`lockRewind`](#lockrewind) · [`lockTravel`](#locktravel) · [`modifier`](#modifier) · [`musicVolume`](#musicvolume) · [`objectiveText`](#objectivetext) · [`offerUpgrades`](#offerupgrades) · [`playAnimation`](#playanimation) · [`playMusic`](#playmusic) · [`playPlayerAnimation`](#playplayeranimation) · [`playSound`](#playsound) · [`playVideo`](#playvideo) · [`playerInvincible`](#playerinvincible) · [`popText`](#poptext) · [`pullPlayerInto`](#pullplayerinto) · [`random`](#random) · [`removeStatus`](#removestatus) · [`returnToMenu`](#returntomenu) · [`runSequence`](#runsequence) · [`setAnimParam`](#setanimparam) · [`setCounter`](#setcounter) · [`setFloor`](#setfloor) · [`setState`](#setstate) · [`showText`](#showtext) · [`slowMotion`](#slowmotion) · [`spawn`](#spawn) · [`spawnWave`](#spawnwave) · [`startHazard`](#starthazard) · [`stopGenerator`](#stopgenerator) · [`stopHazard`](#stophazard) · [`unlockInput`](#unlockinput) · [`unlockRewind`](#unlockrewind) · [`unlockTravel`](#unlocktravel) · [`wait`](#wait) · [`walkPlayerTo`](#walkplayerto)

### addCounter

Adds to a named counter (negative to subtract).

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |
| `amount` | number | `1.0` |  |

### addDiamonds

Gives the player diamonds (the shop currency). Use it in a pickup's collect actions, which pay out once even if rewound.

| Field | Type | Default | Notes |
|---|---|---|---|
| `amount` | integer | `1` |  |

### addRewind

Adds seconds of rewind juice (up to the maximum) and shows it.

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `1.0` |  |
| `text` | string | `"+{0}s REWIND"` | Text floated at the pickup ('' = none). {0} = the seconds. |

### applyStatus

Applies a status effect (content/effects) to entities or the player.

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `effect` | string | `""` | **Required.**  (status effect id) |

### bossBar

Shows a big health bar for an entity.

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `label` | string | `""` |  |

### cameraReset

Moves and zooms the camera back to the level's own view after cameraTo / cameraZoom.

| Field | Type | Default | Notes |
|---|---|---|---|
| `duration` | number | `1.0` |  |
| `ease` | [Ease](#ease) | `"InOut"` |  |

### cameraShake

Something that happens: spawn, change phase, show text, apply a status...

| Field | Type | Default | Notes |
|---|---|---|---|
| `intensity` | number | `0.2` |  |
| `duration` | number | `0.3` |  |

### cameraTo

Moves the camera to a point (and zooms, with 'size') over 'duration' seconds. Static camera only (world.camera); cameraReset goes back.

| Field | Type | Default | Notes |
|---|---|---|---|
| `at` | [x, y] | `[0.0,0.0]` | The new center, measured from 'anchor'. |
| `anchor` | [Anchor](#anchor) | `"World"` |  |
| `size` | number | `0.0` | Half the visible height in world units (the level's is 2.8; smaller = closer). 0 = keep the current zoom. |
| `duration` | number | `1.0` |  |
| `ease` | [Ease](#ease) | `"InOut"` |  |

### cameraZoom

Zooms the camera where it is to 'size' over 'duration' seconds. Static camera only; cameraReset goes back.

| Field | Type | Default | Notes |
|---|---|---|---|
| `size` | number | `2.0` | Half the visible height in world units (the level's is 2.8; smaller = closer). |
| `duration` | number | `1.0` |  |
| `ease` | [Ease](#ease) | `"InOut"` |  |

### climbFloors

The hero goes up this many floors (the counter goes down; negative goes back down).

| Field | Type | Default | Notes |
|---|---|---|---|
| `count` | integer | `1` |  |

### completeLevel

Completes the level now (the level's completionDelay and next video still apply).

No fields.

### damage

Damages entities (or the player).

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `amount` | integer | `1` |  |
| `ignoreInvincibility` | true/false | `false` | Ignore hit invincibility. |

### despawn

Removes entities without them dying (no onDeath, no loot, not counted as kills).

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |

### emit

Emits a signal that 'signal' triggers react to (next tick).

| Field | Type | Default | Notes |
|---|---|---|---|
| `signal` | string | `""` | **Required.** |

### facePlayer

Turns the player left or right, or toward an entity. Moving or firing turns him again.

| Field | Type | Default | Notes |
|---|---|---|---|
| `direction` | [FaceDirection](#facedirection) | `"Right"` |  |
| `target` | string | `""` | Instance id of an entity to face instead of 'direction'. |

### fade

Fades the screen to black and holds it, or back to clear (to: Clear). Lines (showText) and the pause menu still show over it.

| Field | Type | Default | Notes |
|---|---|---|---|
| `to` | [FadeTo](#fadeto) | `"Black"` |  |
| `duration` | number | `1.0` |  |
| `color` | color | `"#000000"` | The color it fades to. |

### failLevel

Fails the level: plays the level's death scene.

No fields.

### flash

Flashes the screen a color.

| Field | Type | Default | Notes |
|---|---|---|---|
| `color` | color | `"#FFFFFF"` |  |
| `duration` | number | `0.2` |  |

### generateFloors

Fills the stage with floors built from a Circle (content/circles): pushes of enemies from its table and telegraphed hazards, with breathers between them, harder the higher the floor. Runs until the floor counter reaches 'untilFloor', its phase ends, or stopGenerator; hand-written story beats go in the phases around it. Seeded: same seed, same floors, so rewinds and playtests repeat. Generated enemies get the tags in 'tags'.

| Field | Type | Default | Notes |
|---|---|---|---|
| `circle` | string | `""` | Circle to build from (empty = the script's floors.circle). (circle id) |
| `untilFloor` | integer | `0` | Stop starting pushes once the floor counter reaches this floor (0 = run until the phase ends). |
| `firstBreather` | number | `2.0` | Quiet seconds before the first push. |
| `tags` | list of string | `["generated"]` | Tags for every generated enemy (target them with allDead etc.). |
| `seed` | integer | `0` | Added to the level seed, so two generated stretches in one stage differ. |
| `persist` | true/false | `false` | Keep running after the phase that started it ends. |
| `doneSignal` | string | `"floorsDone"` | Signal emitted when it stops at untilFloor and its last push is over. |

### goToLevel

Leaves for another level (by number) instead of the normal next step. For branching paths and secret exits.

| Field | Type | Default | Notes |
|---|---|---|---|
| `level` | integer | `1` |  |

### goToPhase

Ends the current phase (stopping its hazards, sequences, and timed modifiers) and starts another.

| Field | Type | Default | Notes |
|---|---|---|---|
| `phase` | string | `""` | **Required.**  (phase name) |

### grantUpgrade

Gives the player one level of an upgrade (once per level, even across rewinds).

| Field | Type | Default | Notes |
|---|---|---|---|
| `upgrade` | string | `""` | **Required.** |

### heal

Restores health (up to the maximum).

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `amount` | integer | `1` |  |

### hideBossBar

Something that happens: spawn, change phase, show text, apply a status...

No fields.

### hideText

Something that happens: spawn, change phase, show text, apply a status...

| Field | Type | Default | Notes |
|---|---|---|---|
| `style` | [TextStyle](#textstyle) | `"Banner"` |  |

### if

Runs 'then' if the condition is true right now, otherwise 'else'.

| Field | Type | Default | Notes |
|---|---|---|---|
| `condition` | [Trigger](#triggers) | — |  |
| `then` | list of [Action](#actions) | `[]` |  |
| `else` | list of [Action](#actions) | `[]` |  |

### kill

Kills entities (onDeath runs; counts as a kill).

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |

### lockInput

Stops the player from moving and shooting for 'seconds' (0 = until unlockInput).

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `0.0` |  |

### lockRewind

No rewind until unlockRewind (a scene the hero can't undo): R and the HUD button do nothing, and the button says so.

No fields.

### lockTravel

Travel stages: the camera stops moving forward (an arena moment, a boss, a cutscene) until unlockTravel.

No fields.

### modifier

Changes stats of entities. With 'includeFuture' it also applies to matching entities spawned later (until it expires or its phase ends).

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `modifiers` | list of [StatModifierDef](#statmodifierdef) | `[]` |  |
| `duration` | number | `0.0` | Seconds it lasts. 0 = permanent. |
| `includeFuture` | true/false | `false` | Also apply to matching entities spawned later. |

### musicVolume

Something that happens: spawn, change phase, show text, apply a status...

| Field | Type | Default | Notes |
|---|---|---|---|
| `volume` | number | `1.0` |  |

### objectiveText

Sets the objective line in the corner ('' hides it).

| Field | Type | Default | Notes |
|---|---|---|---|
| `text` | string | `""` |  |

### offerUpgrades

Pauses the game and lets the player pick one of 'count' upgrades. Offered once per level, even across rewinds. Picks from 'choices' if set, otherwise from every upgrade not yet maxed (weighted).

| Field | Type | Default | Notes |
|---|---|---|---|
| `count` | integer | `3` |  |
| `choices` | list of string | `[]` |  |
| `title` | string | `"Pick an upgrade"` |  |

### playAnimation

Plays an animation state on entities.

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `state` | string | `""` | **Required.** |

### playMusic

Switches the level music.

| Field | Type | Default | Notes |
|---|---|---|---|
| `clip` | asset (AudioClip) | — |  |
| `volume` | number | `1.0` |  |

### playPlayerAnimation

Plays a state of the player's Animator by name (player.json animationConfig: Idle, Walk, Jump, Fall, Fire, and scene clips added there). His own movement takes over again through the Animator's transitions.

| Field | Type | Default | Notes |
|---|---|---|---|
| `state` | string | `""` | **Required.** |
| `crossFade` | number | `0.0` | Seconds to blend into it (0 = cut). |

### playSound

Plays a sound: an audio.json sound id (announcer lines, stings) or a clip.

| Field | Type | Default | Notes |
|---|---|---|---|
| `sound` | string | `""` | audio.json sound id (e.g. an announcer line). Used when 'clip' isn't set. |
| `clip` | asset (AudioClip) | — |  |
| `volume` | number | `1.0` |  |

### playVideo

Leaves for a video (by number).

| Field | Type | Default | Notes |
|---|---|---|---|
| `video` | integer | `1` |  |

### playerInvincible

Something that happens: spawn, change phase, show text, apply a status...

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `2.0` |  |

### popText

Floats a short text up from a point (damage numbers, jokes, "NICE").

| Field | Type | Default | Notes |
|---|---|---|---|
| `text` | string | `""` |  |
| `color` | color | `"#FFFFFF"` |  |
| `at` | [x, y] | `[0.0,0.0]` |  |
| `anchor` | [Anchor](#anchor) | `"Self"` |  |
| `size` | number | `1.0` |  |

### pullPlayerInto

Pulls the player into a point (a portal): gravity and collisions off, he slides into it spinning and shrinking, then is gone (hidden, nothing can hit him) for the rest of the level. 'emit' fires when he disappears.

| Field | Type | Default | Notes |
|---|---|---|---|
| `at` | [x, y] | `[0.0,0.0]` | The point, measured from 'anchor' (used when 'target' is empty). |
| `anchor` | [Anchor](#anchor) | `"World"` |  |
| `target` | string | `""` | Instance id of an entity to be pulled into (followed if it moves). |
| `duration` | number | `1.5` | Seconds from the first tug to gone. |
| `spin` | number | `720.0` | Degrees he turns on the way in (positive = clockwise; 720 = two turns). |
| `endScale` | number | `0.0` | His size at the end, as a fraction of normal (0 = shrinks to nothing). |
| `ease` | [Ease](#ease) | `"In"` | In = slow first tug, fast swallow. |
| `emit` | string | `""` | Signal emitted when he is gone. |

### random

Runs one of the choices, picked by weight (deterministic).

| Field | Type | Default | Notes |
|---|---|---|---|
| `choices` | list of [WeightedActions](#weightedactions) | `[]` |  |

### removeStatus

Removes a status effect.

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `effect` | string | `""` | **Required.**  (status effect id) |

### returnToMenu

Something that happens: spawn, change phase, show text, apply a status...

No fields.

### runSequence

Runs a list of actions in order, with 'wait' steps between them. Use a named sequence (content/sequences) or list the steps here.

| Field | Type | Default | Notes |
|---|---|---|---|
| `sequence` | string | `""` | Named sequence id. (sequence id) |
| `steps` | list of [Action](#actions) | `[]` | Inline steps, used when 'sequence' is empty. |
| `persist` | true/false | `false` | Keep running after the phase that started it ends. |

### setAnimParam

Sets an animator parameter on entities (bools use value 0/1; triggers ignore value).

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `param` | string | `""` | **Required.** |
| `value` | number | `1.0` |  |

### setCounter

Sets a named counter.

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |
| `value` | number | `0.0` |  |

### setFloor

Sets the floor counter (the HUD's FLOOR readout), e.g. after a shortcut or a cutscene that skips floors.

| Field | Type | Default | Notes |
|---|---|---|---|
| `floor` | integer | `1000` |  |

### setState

Moves entities to a state of their state machine.

| Field | Type | Default | Notes |
|---|---|---|---|
| `target` | [TargetSelector](#targetselector) | (object) |  |
| `state` | string | `""` | **Required.** |

### showText

Shows text on screen.

| Field | Type | Default | Notes |
|---|---|---|---|
| `text` | string | `""` |  |
| `duration` | number | `2.0` | Seconds (0 = until replaced or hideText). |
| `style` | [TextStyle](#textstyle) | `"Banner"` |  |
| `color` | color | `"#FFFFFF"` |  |
| `sound` | string | `""` | audio.json sound spoken (or played) with it, e.g. the player's voice line for this subtitle. |

### slowMotion

Slows game time to 'scale' for 'duration' seconds of real time.

| Field | Type | Default | Notes |
|---|---|---|---|
| `scale` | number | `0.3` |  |
| `duration` | number | `1.0` |  |

### spawn

Spawns one or more entities.

| Field | Type | Default | Notes |
|---|---|---|---|
| `entity` | string | `""` | **Required.**  (entity id) |
| `at` | [x, y] | `[0.0,0.0]` | Position, measured from 'anchor'. |
| `anchor` | [Anchor](#anchor) | `"World"` |  |
| `count` | integer | `1` |  |
| `spacing` | [x, y] | `[0.5,0.0]` | Offset between each of 'count' entities. |
| `id` | string | `""` | Instance id for targeting it later (count must be 1). |
| `tags` | list of string | `[]` | Tags for targeting it later. |
| `health` | integer | `0` | Overrides the entity's starting health (0 = keep). |
| `modifiers` | list of [StatModifierDef](#statmodifierdef) | `[]` |  |
| `velocity` | [x, y] | `[0.0,0.0]` | Initial velocity. |
| `facePlayer` | true/false | `true` | Face the player when spawned. |

### spawnWave

Spawns 'count' entities spread over an area or a list of points, optionally one every 'interval' seconds. Use modifiers with multiplyPerRepeat to make each wave of an 'every' rule stronger.

| Field | Type | Default | Notes |
|---|---|---|---|
| `entity` | string | `""` | **Required.**  (entity id) |
| `count` | integer | `3` |  |
| `points` | list of [x, y] | `[]` | Spawn points (measured from 'anchor'). If fewer than count, the rest spawn in the area. |
| `areaCenter` | [x, y] | `[0.0,0.0]` | Center of the spawn area, measured from 'anchor'. |
| `areaSize` | [x, y] | `[10.0,0.0]` | Size of the spawn area. Zero = all at areaCenter. |
| `anchor` | [Anchor](#anchor) | `"Camera"` |  |
| `interval` | number | `0.0` | Seconds between each spawn (0 = all at once). |
| `tags` | list of string | `[]` |  |
| `health` | integer | `0` |  |
| `modifiers` | list of [StatModifierDef](#statmodifierdef) | `[]` |  |
| `facePlayer` | true/false | `true` |  |

### startHazard

Starts firing projectile entities from the level itself (fire rain, turrets, sweeping lasers). Stops when its phase ends, after 'duration', or on stopHazard.

| Field | Type | Default | Notes |
|---|---|---|---|
| `id` | string | `""` | **Required.** Name for stopHazard. |
| `projectile` | string | `""` | **Required.**  (entity id) |
| `pattern` | [Pattern](#patterns) | — |  |
| `speed` | number | `4.0` |  |
| `aim` | [Aim](#aim) | `"Down"` |  |
| `direction` | [x, y] | `[0.0,-1.0]` |  |
| `origin` | [x, y] | `[0.0,0.0]` | Where shots come from, measured from 'anchor' (rain patterns use the top of the screen instead). |
| `anchor` | [Anchor](#anchor) | `"Camera"` |  |
| `interval` | number | `1.0` | Seconds between volleys. |
| `initialDelay` | number | `0.0` |  |
| `duration` | number | `0.0` | Seconds it runs (0 = until its phase ends or stopHazard). |
| `persist` | true/false | `false` | Keep running after its phase ends. |
| `damage` | integer | `0` | Damage per shot; 0 = the projectile's own. |
| `rampPerSecond` | number | `0.0` | Volleys per second added each second it runs (it intensifies). Negative slows it down. |

### stopGenerator

Stops the floor generator (enemies already spawned stay).

No fields.

### stopHazard

Stops a hazard started with startHazard.

| Field | Type | Default | Notes |
|---|---|---|---|
| `id` | string | `""` | **Required.** |

### unlockInput

Something that happens: spawn, change phase, show text, apply a status...

No fields.

### unlockRewind

Allows rewinding again after lockRewind.

No fields.

### unlockTravel

Travel stages: the camera moves forward again after lockTravel.

No fields.

### wait

Inside a sequence: pause before the next step.

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `1.0` |  |

### walkPlayerTo

Walks the player to a point by himself, with his own Walk clip and turning, even while input is locked (lockInput). He stops there (or after 1.5 s without getting closer, e.g. against a wall) and 'emit' fires.

| Field | Type | Default | Notes |
|---|---|---|---|
| `x` | number | `0.0` | Where to stop (world x), measured from 'anchor'; with 'target', an offset from that entity. |
| `anchor` | [Anchor](#anchor) | `"World"` |  |
| `target` | string | `""` | Instance id (spawn 'id') of an entity to walk up to, instead of a fixed x. |
| `speed` | number | `0.6` | Fraction of his normal running speed (1 = full speed, 0.5 = a stroll). |
| `emit` | string | `""` | Signal emitted when he arrives. |


## Patterns

The shape of one volley of shots.

[`line`](#line) · [`radial`](#radial) · [`rain`](#rain) · [`scatter`](#scatter) · [`single`](#single) · [`spread`](#spread) · [`sweep`](#sweep)

### line

'count' parallel shots side by side, 'spacing' apart.

| Field | Type | Default | Notes |
|---|---|---|---|
| `count` | integer | `3` |  |
| `spacing` | number | `0.3` |  |

### radial

'count' shots evenly around a full circle, turned by 'rotationPerVolley' degrees each volley (spirals).

| Field | Type | Default | Notes |
|---|---|---|---|
| `count` | integer | `8` |  |
| `rotationPerVolley` | number | `0.0` |  |

### rain

'count' shots from random points along the top of the screen (or across 'width' around the origin if width > 0), falling down. Ignores the aim.

| Field | Type | Default | Notes |
|---|---|---|---|
| `count` | integer | `1` |  |
| `width` | number | `0.0` | 0 = the whole screen width. |
| `angleJitter` | number | `5.0` | Random tilt, degrees either side of straight down. |
| `topInset` | number | `0.2` | Start this far below the top edge of the screen. |

### scatter

'count' shots in random directions within 'angle' degrees of the aim.

| Field | Type | Default | Notes |
|---|---|---|---|
| `count` | integer | `5` |  |
| `angle` | number | `60.0` |  |

### single

One shot along the aim.

No fields.

### spread

A fan of 'count' shots spanning 'angle' degrees, centered on the aim.

| Field | Type | Default | Notes |
|---|---|---|---|
| `count` | integer | `3` |  |
| `angle` | number | `30.0` |  |

### sweep

One shot per volley, turning from 'fromAngle' to 'toAngle' (degrees from the aim) over 'steps' volleys, then repeating.

| Field | Type | Default | Notes |
|---|---|---|---|
| `fromAngle` | number | `-45.0` |  |
| `toAngle` | number | `45.0` |  |
| `steps` | integer | `8` |  |
| `pingPong` | true/false | `true` | Go back and forth instead of restarting. |

## Level script

### LevelScript

A level's gameplay, written as the "script" object in levels/{name}.json. Rules fire actions when their trigger fires; global rules run the whole level, phase rules only while their phase is active. The first phase starts the level.

| Field | Type | Default | Notes |
|---|---|---|---|
| `seed` | integer | `0` | Random seed (0 = derived from the level number). Same seed, same level, same choices. |
| `world` | [WorldDef](#worlddef) | (object) |  |
| `floors` | [FloorsDef](#floorsdef) | (object) |  |
| `objective` | [ObjectiveDef](#objectivedef) | `{"complete":null,"fail":null,"text":""}` |  |
| `length` | number | `0.0` | Expected length in seconds, for the progress bar (0 = the level's duration). |
| `markers` | list of [ProgressMarkerDef](#progressmarkerdef) | `[]` | Labeled ticks on the progress bar, e.g. where the boss arrives. |
| `counters` | list of [CounterDef](#counterdef) | `[]` | Counters set when the level starts. |
| `rules` | list of [RuleDef](#ruledef) | `[]` | Rules active for the whole level. |
| `phases` | list of [PhaseDef](#phasedef) | `[]` |  |

### PhaseDef

A stage of the level, like 'waves' then 'boss'. Effects started in a phase (hazards, sequences, modifiers) end when it ends unless marked persist.

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |
| `onEnter` | list of [Action](#actions) | `[]` |  |
| `onExit` | list of [Action](#actions) | `[]` |  |
| `rules` | list of [RuleDef](#ruledef) | `[]` |  |

### RuleDef

When 'when' fires, run 'do'.

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | Optional name, for reports. |
| `when` | [Trigger](#triggers) | — |  |
| `do` | list of [Action](#actions) | `[]` |  |
| `maxTimes` | integer | `0` | Fire at most this many times (0 = no limit). |

### ObjectiveDef

How the level is won and lost.

| Field | Type | Default | Notes |
|---|---|---|---|
| `complete` | [Trigger](#triggers) | — | Win when this fires. Leave empty to win only through a completeLevel action. |
| `fail` | [Trigger](#triggers) | — | Lose when this fires (in addition to the player dying). |
| `text` | string | `""` | Objective line shown in the corner. |

### WorldDef

The space the level happens in.

| Field | Type | Default | Notes |
|---|---|---|---|
| `camera` | [CameraMode](#cameramode) | `"Static"` |  |
| `boundsCenter` | [x, y] | `[0.0,0.0]` | Follow/Scroll: the area the camera may show, as center and size. Zero size = unlimited. |
| `boundsSize` | [x, y] | `[0.0,0.0]` |  |
| `scrollSpeed` | [x, y] | `[1.0,0.0]` | Scroll: camera velocity in world units per second. |
| `followSharpness` | number | `5.0` | Follow: how quickly the camera catches up (higher = snappier). |
| `playerGravity` | number | `1.0` | Multiplies the player's gravity (0.5 = floaty). |
| `travelDirection` | [TravelDirection](#traveldirection) | `"Right"` | Travel: which way the stage goes. |
| `travelLead` | number | `1.5` | Travel: how far ahead of the player the camera looks (world units). |
| `travelSpeed` | number | `0.0` | Travel: the camera also moves forward on its own at least this fast (0 = only as the player goes). |
| `travelLength` | number | `0.0` | Travel: the camera stops after this distance (0 = no end). |
| `holdForPushes` | true/false | `true` | Travel: the camera waits while enemies of a generated push are alive, so the player fights instead of running past. |
| `props` | list of [PropDef](#propdef) | `[]` | Travel: scenery for this stage only (the village debris in The Spill), drawn with its Circle's props. |

### FloorsDef

The floor counter: which floors of Hell a stage covers. Floors count down as the hero climbs (1000 is the bottom, 1 the top). The counter is the script counter "floor"; the HUD shows it (ui/theme.json texts.floorFormat) and the progress bar measures floors.

| Field | Type | Default | Notes |
|---|---|---|---|
| `start` | integer | `0` | Floor the stage starts on, e.g. 1000. 0 = no floor counter. |
| `end` | integer | `0` | Floor the stage ends on, e.g. 976 (the progress bar runs from start to here). 0 = no end. |
| `unitsPerFloor` | number | `0.0` | Travel stages: world units travelled per floor. The counter goes up a floor each time the camera covers this distance, and a floor sign stands where it happens. 0 = only actions (setFloor, climbFloors) change the floor. |
| `circle` | string | `""` | The Circle of Hell (content/circles) the stage is in: track, props, backdrop, generated floors and milestone lines. (circle id) |
| `signs` | true/false | `true` | Paint floor numbers into the world as signs on travel stages. |
| `sign` | string | `""` | Travel stages: words carved on a big sign at the start of the stage (the place's own sign, e.g. "IMPS ONLY"). '' = none. |
| `door` | string | `""` | Travel stages: words carved over a big door standing at the top of the stage, where the camera stops (the Heat Stairs end at the Magma King's door, "THE SLAG THRONE"). '' = none. |
| `repeat` | list of integer | `[]` | Floors Hell makes the hero walk twice (The Loop): each one takes two floors' distance, its number stays on the counter and two signs show it. Add unitsPerFloor per repeat to travelLength. |

### CounterDef

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | **Required.** |
| `value` | number | `0.0` |  |

### ProgressMarkerDef

A labeled tick on the level progress bar.

| Field | Type | Default | Notes |
|---|---|---|---|
| `seconds` | number | `0.0` |  |
| `floor` | integer | `0` | On a stage with floors.end set the bar measures floors: put the tick at this floor instead of at 'seconds' (0 = use seconds). |
| `label` | string | `""` |  |

## Circle

### CircleDef

A Circle of Hell: a run of floors with one biome, one family of enemies and one Warden (see Docs/Story.md). Travel stages in the circle take their ground, platforms, passing props, backdrop and generated floors (generateFloors) from it. File: content/circles/{id}.json.

| Field | Type | Default | Notes |
|---|---|---|---|
| `displayName` | string | `""` | Name shown to the player, e.g. "The Ember Wastes". |
| `firstFloor` | integer | `1000` | Bottom floor of the circle (where the climb through it starts), e.g. 1000. |
| `lastFloor` | integer | `901` | Top floor (the Warden's), e.g. 901. Difficulty goes from 'easiest' at firstFloor to 'hardest' here. |
| `backgroundLayers` | list of [BackgroundLayerConfig](#backgroundlayerconfig) | `[]` | Backdrop for stages in this circle whose level file has no backgroundLayers (same fields as a level's backgroundLayers). |
| `track` | [TrackDef](#trackdef) | (object) |  |
| `props` | list of [PropDef](#propdef) | `[]` | Scenery that passes by on travel stages: spires, rocks, chains. Drawn only; never collides. |
| `enemies` | list of [CircleEnemyDef](#circleenemydef) | `[]` | Who the generator sends, picked by weight. Use fromFloor to introduce a new enemy partway up (one new enemy at a time). |
| `hazards` | list of [CircleHazardDef](#circlehazarddef) | `[]` | Hazards the generator can use instead of an enemy push. Each is telegraphed first. |
| `easiest` | [DifficultyDef](#difficultydef) | (object) | Generator settings at the circle's first (bottom) floor. |
| `hardest` | [DifficultyDef](#difficultydef) | (object) | Generator settings at its last (top) floor; floors in between are interpolated. |
| `milestones` | list of [MilestoneDef](#milestonedef) | `[]` | Lines that play when the floor counter reaches a floor in this circle (any stage), e.g. 999: "Only nine hundred ninety-nine to go." |

### TrackDef

The ground and platforms of a travel stage. The ground runs the whole way; platforms are streamed in ahead and removed behind, laid out from the level seed (same seed, same platforms).

| Field | Type | Default | Notes |
|---|---|---|---|
| `groundY` | number | `-2.5` | Height of the top of the ground (world units). |
| `groundColor` | color | `[0.12,0.06,0.05,1.0]` |  |
| `groundEdgeColor` | color | `[1.0,0.45,0.15,1.0]` | Thin strip along the top of the ground (alpha 0 = none). |
| `segmentLength` | number | `6.0` | Length of one streamed segment along the travel direction. Up: the height between ledges. |
| `platformChance` | number | `0.5` | Chance (0-1) that a segment has a platform. |
| `platformHeight` | [x, y] | `[1.1,1.8]` | Platform height above the ground: [min, max]. |
| `platformWidth` | [x, y] | `[1.4,2.6]` | Platform width: [min, max]. |
| `platformColor` | color | `"#00000000"` | Platform tint (alpha 0 = the game's platform color). |
| `clearStart` | number | `8.0` | Distance from the start kept flat, so the opening has room to move. |

### PropDef

A kind of scenery repeated along a travel stage. 'parallax' sets its depth: 0 moves with the ground, 0.8 is far away and drifts by slowly, negative is close to the camera and rushes past in front.

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | For reports. |
| `sprite` | asset (Sprite) | — | Art (optional). Without it the prop is drawn as 'shape' in 'color'. |
| `shape` | [PropShape](#propshape) | `"Spire"` |  |
| `color` | color | `[0.08,0.04,0.05,1.0]` |  |
| `size` | [x, y] | `[1.0,2.5]` | Width and height in world units. |
| `sizeVariation` | number | `0.3` | Random size variation as a fraction (0.3 = 70% to 130%). |
| `across` | number | `-2.5` | Where its base sits across the travel direction: the world y for Right travel, the x offset from the camera for Up travel. |
| `parallax` | number | `0.5` | Depth: 0 = with the ground, 1 = stuck to the sky, negative = in front of the player. |
| `spacing` | number | `4.0` | Average distance between two of them. |
| `chance` | number | `0.8` | Chance (0-1) each slot gets one. |
| `layer` | [SpriteLayer](#spritelayer) | `"Background2"` | Draw band: Background2 behind the player, Foreground1 in front. |
| `sortingOrder` | integer | `0` |  |

### CircleEnemyDef

An enemy the floor generator can send.

| Field | Type | Default | Notes |
|---|---|---|---|
| `entity` | string | `""` | **Required.**  (entity id) |
| `weight` | number | `1.0` | How often it is picked relative to the others. |
| `fromFloor` | integer | `0` | It only appears once the floor counter is at or above this floor (counting down: 990 means from floor 990 up). 0 = from the start. |
| `areaCenter` | [x, y] | `[3.6,-1.2]` | Spawn area center, measured from the camera (x is ahead of it on Right travel). |
| `areaSize` | [x, y] | `[1.2,2.0]` | Spawn area size; each spawn picks a point inside it. |

### CircleHazardDef

A hazard the floor generator can run instead of an enemy push: a warning (the telegraph), then its actions.

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `""` | For reports. |
| `weight` | number | `1.0` |  |
| `fromFloor` | integer | `0` | Only at or above this floor (counting down). 0 = from the start. |
| `warning` | string | `""` | Warning text shown when it starts (subtitle). |
| `warningSound` | string | `""` | Sound played with the warning. |
| `telegraph` | number | `1.2` | Seconds between the warning and the actions (at least 1, so it is fair). |
| `actions` | list of [Action](#actions) | `[]` | What happens, usually a startHazard with a duration. |
| `duration` | number | `6.0` | Seconds the push lasts before the next breather. |

### DifficultyDef

How hard the generated floors are. A circle sets one for its bottom floor and one for its top floor.

| Field | Type | Default | Notes |
|---|---|---|---|
| `maxAlive` | integer | `2` | Most generated enemies alive at once. |
| `pushSize` | integer | `2` | Enemies in one push. |
| `spawnInterval` | number | `1.2` | Seconds between spawns inside a push. |
| `breather` | number | `4.0` | Quiet seconds between pushes. |
| `hazardChance` | number | `0.2` | Chance (0-1) a push is a hazard instead of enemies. |
| `healthMultiplier` | number | `1.0` | Multiplies generated enemies' health. |
| `pushTimeout` | number | `25.0` | A push ends after this many seconds even if enemies are left (they keep fighting). |

### MilestoneDef

A line for reaching a floor.

| Field | Type | Default | Notes |
|---|---|---|---|
| `floor` | integer | `0` |  |
| `text` | string | `""` | **Required.** |
| `sound` | string | `""` | Voice line or sting played with it. |
| `style` | [TextStyle](#textstyle) | `"Banner"` |  |
| `color` | color | `[1.0,0.82,0.48,1.0]` |  |
| `duration` | number | `2.5` | Seconds on screen. |

### BackgroundLayerConfig

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | `"Background"` | Name for this layer (for debugging/editing) |
| `spriteFramesData` | asset (ScriptableObject) | — | Animated sprite frame sequence (for video-style or scrolling backgrounds) |
| `staticSprite` | asset (Sprite) | — | Static sprite (for simple image backgrounds) |
| `solidColor` | color | `"#00000000"` | Solid color fill (used when no sprite or frames assigned) |
| `offset` | [x, y] | `[0.0,0.0]` | Position offset from camera center |
| `scale` | [x, y] | `[1.0,1.0]` | Scale multiplier |
| `layer` | [SpriteLayer](#spritelayer) | `"Background1"` | Draw band. Background1 is furthest back. Foreground bands cover the player. |
| `sortingOrder` | integer | `0` | Order inside the chosen band. Higher draws in front of other sprites on that same band. |
| `parallaxFactor` | number | `0.0` | Parallax multiplier (0 = static background, 1 = moves with camera) |
| `tileHorizontally` | true/false | `false` | If true, tile this layer horizontally for infinite scroll |
| `sequenceStart` | number | `-1.0` | When this layer appears, in seconds on the game clock (the same clock as the music). Negative means it plays from the start, stacked with the other layers. |
| `sequenceDuration` | number | `0.0` | How long this layer stays up. The clip loops or holds inside that window. 0 means until the next sequenced layer, or for the rest of the level if it is the last one. |
| `sequenceLoop` | true/false | `true` | Repeat the clip until the window ends. Off plays it once and holds the last frame. |
| `sequenceBy` | [BackgroundSequenceKey](#backgroundsequencekey) | `"Time"` | What sequenceStart and sequenceDuration measure: Time (seconds), Distance (world units travelled on a Travel stage) or Floors (floors climbed since the stage's first floor). The clip itself still plays on the game clock. |
| `clipStart` | number | `0.0` | Play only part of the clip: start here (seconds into the clip). Lets one long video supply a backdrop, e.g. just its lava-river shot. |
| `clipLength` | number | `0.0` | Length of that part in seconds (0 = the whole clip). It loops inside this window when the layer loops. |

## Sequence

### SequenceDef

A reusable list of steps (actions and waits) such as a boss intro. File: content/sequences/{id}.json.

| Field | Type | Default | Notes |
|---|---|---|---|
| `steps` | list of [Action](#actions) | `[]` |  |

## Status effect

### StatusEffectDef

A timed condition on an entity or the player: poison, burning, slow, stun, shield. File: content/effects/{id}.json.

| Field | Type | Default | Notes |
|---|---|---|---|
| `duration` | number | `3.0` |  |
| `tickInterval` | number | `0.5` | Seconds between damage ticks. |
| `damagePerTick` | integer | `0` | Damage each tick (ignores hit invincibility). |
| `modifiers` | list of [StatModifierDef](#statmodifierdef) | `[]` |  |
| `stun` | true/false | `false` | Can't move or attack. |
| `invulnerable` | true/false | `false` | Can't be damaged. |
| `tint` | color | `"#FFFFFF00"` | Tint while active (alpha 0 = none). |
| `stacking` | [Stacking](#stacking) | `"Refresh"` |  |
| `maxStacks` | integer | `3` |  |

## Shared types

### TargetSelector

Picks entities. Set exactly one of: player, self, id, tag, entity, all.

| Field | Type | Default | Notes |
|---|---|---|---|
| `player` | true/false | `false` | The player. |
| `self` | true/false | `false` | The entity running this (only inside entity abilities, states, and onDeath). |
| `id` | string | `""` | The entity spawned with this instance id. |
| `tag` | string | `""` | Every live entity with this tag. |
| `entity` | string | `""` | Every live entity of this entity type. (entity id) |
| `all` | true/false | `false` | Every live entity except the player. |

### StatModifierDef

Changes a stat: value * multiply + add.

| Field | Type | Default | Notes |
|---|---|---|---|
| `stat` | [Stat](#stat) | `"MoveSpeed"` |  |
| `multiply` | number | `1.0` | Multiplier (1 = unchanged). |
| `add` | number | `0.0` | Added after multiplying. |
| `multiplyPerRepeat` | number | `0.0` | Added to 'multiply' for each previous repeat of the rule that applied this (wave scaling): 0.2 makes wave 3 get multiply + 0.4. |

### WeightedActions

| Field | Type | Default | Notes |
|---|---|---|---|
| `weight` | number | `1.0` |  |
| `do` | list of [Action](#actions) | `[]` |  |

## Enums

### Aim

Which way shots go before the pattern spreads them.

- `Player` — At the player's body.
- `Facing` — The way the shooter faces (left or right).
- `Direction` — The 'direction' field.
- `Down`
- `Up`
- `Left`
- `Right`
- `Random` — A random direction each volley (deterministic).

### Anchor

Where a position is measured from.

- `World` — World coordinates.
- `Self` — Relative to the entity running the action.
- `Player` — Relative to the player.
- `Camera` — Relative to the camera center.

### AnimConditionMode

How an animator condition compares its parameter.

- `If`
- `IfNot`
- `Greater`
- `Less`
- `Equals`
- `NotEqual`

### AnimParameterKind

Animator parameter type.

- `Float`
- `Int`
- `Bool`
- `Trigger`

### AuraTargets

- `Hostile`
- `Friendly`
- `All`

### BackgroundSequenceKey

- `Time`
- `Distance`
- `Floors`

### BodyKind

How the body moves and collides.

- `Kinematic` — Moved only by its abilities; ignores gravity and pushes (enemies, most projectiles).
- `Dynamic` — Moved by physics: falls with gravity, bounces off ground.

### CameraMode

How the camera behaves.

- `Static` — Fixed on the level's starting view (the classic arena).
- `Follow` — Follows the player inside 'bounds'.
- `Scroll` — Moves at 'scrollSpeed'; the player is kept on screen.
- `Travel` — Travels through the stage in 'travelDirection': follows the player forward, never back. The ground runs along with it, platforms and props stream in ahead and are removed behind. The counter "distance" is how far it has gone.

### Ease

How a scripted move speeds up and slows down over its duration.

- `Linear` — Constant speed.
- `In` — Starts slow, ends fast (being sucked in).
- `Out` — Starts fast, ends slow (settling).
- `InOut` — Slow at both ends (a camera move).

### FaceDirection

Which way the player faces.

- `Left`
- `Right`

### FadeTo

What a fade does.

- `Black` — Fades the screen to 'color' (black) and holds it there.
- `Clear` — Fades back to the game.

### PropShape

A built-in silhouette for props without a sprite.

- `Spire` — A tall pointed rock.
- `Rock` — A low lumpy boulder.
- `Block` — A plain rectangle.
- `Glow` — A flat glowing patch on the ground (lava cracks, puddles).

### ShapeKind

Collider shape.

- `Circle`
- `Box`

### SpriteLayer

- `Background1`
- `Background2`
- `Player`
- `Foreground1`
- `Foreground2`

### Stacking

What happens when a status is applied again while it is active.

- `Refresh` — Restart its duration.
- `Stack` — Add a stack (up to maxStacks); damage and modifiers scale with stacks.
- `Ignore` — Keep the existing one.

### Stat

A number on an entity that modifiers and status effects can change.

- `MoveSpeed` — Multiplies movement speed.
- `Damage` — Multiplies damage dealt.
- `DamageTaken` — Multiplies damage taken (0 = immune).
- `AttackSpeed` — Multiplies how often attacks happen (2 = twice as often).
- `ProjectileSpeed` — Multiplies projectile speed.
- `MaxHealth` — Multiplies maximum health (applies when spawned).
- `Size` — Multiplies sprite and collider size (applies when spawned).

### Team

Which side something is on. Entities hurt entities of other teams; Neutral hurts and is hurt by no one unless an ability says so.

- `Enemy`
- `Player`
- `Neutral`

### TextStyle

- `Banner` — Big, centered.
- `Subtitle` — Smaller, lower on screen.

### TravelDirection

Which way a travel stage goes.

- `Right` — Walk to the right.
- `Up` — Climb upward on ledges.

