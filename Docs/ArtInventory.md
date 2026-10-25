# Art inventory

Every visible thing in the game and whether it has real art. **Written by `python Tools/Art/art_inventory.py`** (no Unity,
no GPU; `--check` fails while a row is a placeholder or a stand-in). Don't edit the tables by hand: fix the art, then run it.
How art is made: Docs/ArtPipeline.md (creatures `node Tools/Art/art.mjs <id>`, the hero `art.mjs hero`, effects and props
`Tools/Art/fx_sheets.py`).

## Totals

| | ok | placeholder / stand-in |
|---|---|---|
| Entities in use | 367 | 61 |
| Hero clips (art/hero.json) | 6 | 84 |
| Weapon grips | 0 | 8 |
| Circle backdrops | 3 | 0 |
| Card icons (Grudges, Yooba's goods) | 22 | 31 |

## Before this pass

On beta at 4bf1506c (2026-10-25), before the full art pass: **179 entities in use** wore a placeholder (a labeled circle, a coloured box) or another creature's art tinted, **25 of 28 hero clips** were stand-ins or missing, and **all 8 weapons** were coloured block props over the launcher sheets (212 rows). The list then:

<details><summary>212 rows</summary>

- entity `afterimage`: STAND-IN, wears chaser's art tinted #D8E8FF
- entity `alphaHound`: STAND-IN, wears brimstoneDemon's art tinted #7A3A2A
- entity `baronSpillway`: STAND-IN, wears imp's art tinted #B8603A
- entity `bastionBrute`: STAND-IN, wears brimstoneDemon's art tinted #5E5668
- entity `beamLens`: STAND-IN, its own animator plays Images tinted #FF6A4A
- entity `blinkImp`: STAND-IN, wears imp's art tinted #D2A8FF
- entity `blinkMark`: STAND-IN, wears fmWarnWhip's art tinted #C58BFF
- entity `boomerangHaloPickup`: PLACEHOLDER, drawn as a labeled circle
- entity `bounceLobber`: STAND-IN, wears brimstoneDemon's art tinted #C8A070
- entity `bounceLump`: STAND-IN, wears projectileEnemy's art tinted #E0602A
- entity `brLampBolt`: STAND-IN, wears fireball's art tinted #BFEFFF
- entity `brMarkBolt`: STAND-IN, wears fireball's art tinted #E8F6FF
- entity `brRuneColumn`: PLACEHOLDER, drawn as a coloured box
- entity `brWallHit`: PLACEHOLDER, drawn as a coloured box
- entity `brWarnLamp`: STAND-IN, wears mkWarnHand's art tinted #BFEFFF
- entity `brWarnRune`: STAND-IN, wears fmWarnWhip's art tinted #BFEFFF
- entity `brWarnWall`: STAND-IN, wears fmWarnWhip's art tinted #8FE3FF
- entity `bridgeWraith`: PLACEHOLDER, drawn as a labeled circle
- entity `brimstoneLump`: STAND-IN, wears fireball's art tinted #E0602A
- entity `broodWhelp`: STAND-IN, wears brimstoneDemon's art tinted #FFB09A
- entity `brute`: STAND-IN, wears fighter's art tinted #FF8A8A
- entity `bsHit`: PLACEHOLDER, drawn as a coloured box
- entity `bsWarn`: STAND-IN, wears fmWarnWhip's art tinted #FFB060
- entity `bulwarkImp`: STAND-IN, wears imp's art tinted #9FB4C4
- entity `carver`: STAND-IN, wears brimstoneDemon's art tinted #D8D0C8
- entity `ceilingSlab`: PLACEHOLDER, drawn as a coloured box
- entity `chainBrute`: STAND-IN, wears brimstoneDemon's art tinted #908888
- entity `chainEmber`: STAND-IN, wears projectileEnemy's art tinted #B0A090
- entity `chainWhelp`: STAND-IN, wears brimstoneDemon's art tinted #C0B8B0
- entity `chainWhipPickup`: PLACEHOLDER, drawn as a labeled circle
- entity `cinderSpark`: STAND-IN, wears cinder's art tinted #FFE6B0
- entity `cinderThatch`: STAND-IN, wears cinder's art tinted #FFD08A
- entity `collapseSlab`: PLACEHOLDER, drawn as a coloured box
- entity `coronaImp`: STAND-IN, wears imp's art tinted #FFEFA0
- entity `cvHit`: PLACEHOLDER, drawn as a coloured box
- entity `cvSpotHit`: PLACEHOLDER, drawn as a coloured box
- entity `cvWarnLine`: STAND-IN, wears fmWarnWhip's art tinted #FFB060
- entity `cvWarnSpot`: STAND-IN, wears fmWarnWhip's art tinted #FFE08A
- entity `deadeyeBolt`: STAND-IN, wears projectileEnemy's art tinted #F4FBFF
- entity `deadeyeImp`: STAND-IN, wears imp's art tinted #E8F4FF
- entity `demonLobber`: STAND-IN, wears brimstoneDemon's art tinted #B0A4A8
- entity `demonWhelp`: STAND-IN, wears brimstoneDemon's art tinted #FF8A6A
- entity `drone`: STAND-IN, wears shooter's art tinted #9FFFE0
- entity `emberMine`: STAND-IN, its own animator plays Images tinted #7A2A14
- entity `fmLash`: PLACEHOLDER, drawn as a coloured box
- entity `fmWarnWhip`: STAND-IN, its own animator plays Images tinted #FFD27A
- entity `furnacePlate`: STAND-IN, wears brimstoneDemon's art tinted #C04A2A
- entity `fuseWhelp`: STAND-IN, wears brimstoneDemon's art tinted #FFD04A
- entity `geodeBit`: STAND-IN, wears cinder's art tinted #FFB070
- entity `gravityPad`: PLACEHOLDER, drawn as a coloured box
- entity `gunner`: STAND-IN, wears shooter's art tinted #FFD27A
- entity `haloEmber`: STAND-IN, wears projectileEnemy's art tinted #FFD27A
- entity `haloImp`: STAND-IN, wears imp's art tinted #FFD27A
- entity `haloShot`: STAND-IN, its own animator plays Images tinted #FFD27A
- entity `hookPickup`: PLACEHOLDER, drawn as a labeled circle
- entity `impGuard`: STAND-IN, wears imp's art tinted #E07060
- entity `impSack`: STAND-IN, wears imp's art tinted #FFE0A8
- entity `ironhideBulwark`: STAND-IN, wears imp's art tinted #6C8294
- entity `kilnMatron`: STAND-IN, wears imp's art tinted #C890E8
- entity `kmHit`: PLACEHOLDER, drawn as a coloured box
- entity `kmWarn`: STAND-IN, wears fmWarnWhip's art tinted #FFC0F0
- entity `ladder`: PLACEHOLDER, drawn as a coloured box
- entity `ladderHaul`: PLACEHOLDER, drawn as a coloured box
- entity `lamplighter`: PLACEHOLDER, drawn as a labeled circle
- entity `lanternfly`: STAND-IN, wears chaser's art tinted #7FD8FF
- entity `launcherToss`: PLACEHOLDER, drawn as a labeled circle
- entity `lavaBurst`: STAND-IN, its own animator plays Beam tinted #FF7A2A
- entity `lavaFlood`: STAND-IN, wears fireball's art tinted #FF5A1A
- entity `lavaWave`: STAND-IN, wears fireball's art tinted #FF8A2A
- entity `leadImp`: STAND-IN, wears imp's art tinted #A8D4FF
- entity `lensImp`: STAND-IN, wears imp's art tinted #FF6A6A
- entity `llFlameBolt`: STAND-IN, wears fireball's art tinted #FFD27A
- entity `llMarkBolt`: STAND-IN, wears fireball's art tinted #FFE9A8
- entity `llRuneColumn`: PLACEHOLDER, drawn as a coloured box
- entity `llWarnRune`: STAND-IN, wears fmWarnWhip's art tinted #FFE9A8
- entity `lobberGlow`: STAND-IN, wears fmWarnWhip's art tinted #FFB04A
- entity `loopWraith`: STAND-IN, wears imp's art tinted #9AD8E8C8
- entity `lostBell`: PLACEHOLDER, drawn as a labeled circle
- entity `lostMitt`: PLACEHOLDER, drawn as a labeled circle
- entity `lostRollingPin`: PLACEHOLDER, drawn as a labeled circle
- entity `magmaBoulder`: STAND-IN, wears fireball's art tinted #D2551E
- entity `magmaGeode`: STAND-IN, wears cinder's art tinted #B04A2A
- entity `mfHit`: PLACEHOLDER, drawn as a coloured box
- entity `mfWarn`: STAND-IN, wears fmWarnWhip's art tinted #FF9A6A
- entity `mineBeetle`: STAND-IN, wears brimstoneDemon's art tinted #5A3A2A
- entity `mkWarnFlood`: STAND-IN, wears overlordBeamGlow's art tinted #FF3A1A
- entity `mkWarnFloor`: STAND-IN, wears overlordBeamGlow's art tinted #FFB060
- entity `mkWarnHand`: STAND-IN, its own animator plays Images tinted #FFD27A
- entity `mkWarnMouth`: STAND-IN, its own animator plays Images tinted #FFF1B0
- entity `motherFissure`: STAND-IN, wears brimstoneDemon's art tinted #C05030
- entity `moverSlab`: PLACEHOLDER, drawn as a coloured box
- entity `overlordFar806`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar857`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar896`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar906`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar953`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar958`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar994`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar997`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `phaseRaider`: STAND-IN, wears imp's art tinted #D8A8FF
- entity `pileKeeper`: STAND-IN, wears imp's art tinted #C9907A
- entity `pkSackBurst`: PLACEHOLDER, drawn as a coloured box
- entity `pkSpear`: PLACEHOLDER, drawn as a coloured box
- entity `pkWarnSack`: STAND-IN, wears fmWarnWhip's art tinted #FFE08A
- entity `pkWarnSpear`: STAND-IN, wears fmWarnWhip's art tinted #FFB060
- entity `plateDemon`: STAND-IN, wears brimstoneDemon's art tinted #8E8CA0
- entity `plateShard`: STAND-IN, wears projectileEnemy's art tinted #D8D0F0
- entity `pointer`: STAND-IN, wears damnedSoul's art tinted #C9A27A
- entity `pyreCaller`: STAND-IN, wears imp's art tinted #8A5A4A
- entity `quickfireImp`: STAND-IN, wears imp's art tinted #FF9A5A
- entity `rbHit`: PLACEHOLDER, drawn as a coloured box
- entity `rbSpotHit`: PLACEHOLDER, drawn as a coloured box
- entity `rbWarnLine`: STAND-IN, wears fmWarnWhip's art tinted #FFB060
- entity `rbWarnSpot`: STAND-IN, wears fmWarnWhip's art tinted #FFE08A
- entity `rewindOrb`: PLACEHOLDER, drawn as a labeled circle
- entity `ricochetEmber`: STAND-IN, wears projectileEnemy's art tinted #FFC15A
- entity `ricochetImp`: STAND-IN, wears imp's art tinted #FFC890
- entity `ricochetPistolPickup`: PLACEHOLDER, drawn as a labeled circle
- entity `ricochetShot`: STAND-IN, its own animator plays Images tinted #7FE8FF
- entity `riftBomb`: STAND-IN, its own animator plays Images tinted #B0307A
- entity `riftBomber`: STAND-IN, wears imp's art tinted #FF7ABF
- entity `riftMark`: STAND-IN, wears fmWarnWhip's art tinted #FF5AA0
- entity `risenDemon`: STAND-IN, wears brimstoneDemon's art tinted #6E5A66
- entity `roadBreaker`: STAND-IN, wears brimstoneDemon's art tinted #8A6A6A
- entity `rocketLauncherPickup`: PLACEHOLDER, drawn as a labeled circle
- entity `rook`: STAND-IN, wears damnedSoul's art tinted #F2A93B
- entity `runeBolt`: STAND-IN, wears fireball's art tinted #8FE3FF
- entity `runeHound`: STAND-IN, wears brimstoneDemon's art tinted #A9BFDA
- entity `runePillar`: STAND-IN, wears ledge's art tinted #A9BFDA
- entity `seamEmber`: STAND-IN, wears projectileEnemy's art tinted #6FE0C8
- entity `seamImp`: STAND-IN, wears imp's art tinted #6FE0C8
- entity `seamMark`: STAND-IN, wears fmWarnWhip's art tinted #6FE0C8
- entity `sfNeedle`: STAND-IN, its own animator plays Climb tinted #D8C8D0
- entity `sfSpear`: STAND-IN, wears fireball's art tinted #5A4660
- entity `sfWarnField`: STAND-IN, wears fmWarnWhip's art tinted #FFF0C0
- entity `sfWarnHand`: STAND-IN, wears mkWarnHand's art tinted #FFB070
- entity `sfWarnRow`: STAND-IN, wears fmWarnWhip's art tinted #FF9A4A
- entity `sfWarnSpot`: STAND-IN, wears fmWarnWhip's art tinted #FFD08A
- entity `shard`: STAND-IN, wears fireball's art tinted #5A4A66
- entity `shardBig`: STAND-IN, wears fireball's art tinted #5A4A66
- entity `shardwright`: STAND-IN, wears brimstoneDemon's art tinted #5A5870
- entity `shotgunPickup`: PLACEHOLDER, drawn as a labeled circle
- entity `slabBolt`: STAND-IN, wears projectileEnemy's art tinted #E8F0F8
- entity `slabSniper`: STAND-IN, wears imp's art tinted #B0B8C0
- entity `slag`: STAND-IN, wears fireball's art tinted #C2461C
- entity `slagDrip`: STAND-IN, its own animator plays Images tinted #FFC86A
- entity `slagHound`: STAND-IN, wears brimstoneDemon's art tinted #3A2A28
- entity `slagLadlePickup`: PLACEHOLDER, drawn as a labeled circle
- entity `slagTortoise`: STAND-IN, wears brimstoneDemon's art tinted #8A7468
- entity `slamShockwave`: STAND-IN, wears fireball's art tinted #FFB050
- entity `sleepwalker`: STAND-IN, wears chaser's art tinted #B9C9E8
- entity `sparedWhelp`: STAND-IN, its own animator plays Images tinted #FF9A7A
- entity `spinner`: STAND-IN, wears sentinel's art tinted #7FD8FF
- entity `spireFiend`: PLACEHOLDER, drawn as a labeled circle
- entity `spireShard`: STAND-IN, wears slagDrip's art tinted #FFB070
- entity `spireShardBig`: STAND-IN, wears slagDrip's art tinted #FFB070
- entity `spitfirePickup`: PLACEHOLDER, drawn as a labeled circle
- entity `sprayer`: STAND-IN, wears shooter's art tinted #D69CFF
- entity `swHit`: PLACEHOLDER, drawn as a coloured box
- entity `swWarn`: STAND-IN, wears fmWarnWhip's art tinted #E0D8F0
- entity `targetDummy`: PLACEHOLDER, drawn as a labeled circle
- entity `tortoiseEmber`: STAND-IN, wears projectileEnemy's art tinted #C8A27A
- entity `tripleLobber`: STAND-IN, wears brimstoneDemon's art tinted #E8C898
- entity `twiceRisen`: STAND-IN, wears brimstoneDemon's art tinted #9A7A8A
- entity `twinLensImp`: STAND-IN, wears imp's art tinted #FF3A3A
- entity `veilDemon`: STAND-IN, wears brimstoneDemon's art tinted #7A6A98
- entity `veilEmber`: STAND-IN, wears projectileEnemy's art tinted #B08AFF
- entity `veilMark`: STAND-IN, wears fmWarnWhip's art tinted #B08AFF
- entity `veilMatron`: STAND-IN, wears brimstoneDemon's art tinted #A890E0
- entity `wallSlab`: PLACEHOLDER, drawn as a coloured box
- entity `watcher`: STAND-IN, its own animator plays Images tinted #CFF4FF
- entity `watcherPatrol`: STAND-IN, its own animator plays Images tinted #CFF4FF
- entity `waystoneGlow`: STAND-IN, wears overlordBeamGlow's art tinted #9EFFE6
- entity `whelpNest`: PLACEHOLDER, drawn as a coloured box
- entity `whirlEmber`: STAND-IN, wears projectileEnemy's art tinted #FF7AC8
- entity `whirlImp`: STAND-IN, wears imp's art tinted #FF7AC8
- entity `wick`: STAND-IN, wears damnedSoul's art tinted #BFE6C8
- entity `windGust`: PLACEHOLDER, drawn as a coloured box
- entity `wraithEmber`: STAND-IN, wears projectileEnemy's art tinted #9AD8E8
- hero `UnarmedIdle` (unarmedIdle): STAND-IN, plays Idle.png
- hero `UnarmedWalk` (unarmedWalk): STAND-IN, plays Walk.png
- hero `UnarmedJump` (unarmedJump): STAND-IN, plays Jump.png
- hero `UnarmedFall` (unarmedFall): STAND-IN, plays Fall.png
- hero `Punch1` (punch1): STAND-IN, plays Fire.png
- hero `Punch2` (punch2): STAND-IN, plays Fire.png
- hero `Punch3` (punch3): STAND-IN, plays Fire.png
- hero `LauncherTaken` (launcherAway): STAND-IN, plays Grabbed.png
- hero `EquipLauncher` (equipLauncher): STAND-IN, plays Curious.png
- hero `EquipFists` (equipFists): STAND-IN, plays Shout.png
- hero `Dash` (dash): STAND-IN, plays Walk.png
- hero `RunFire` (runFire): STAND-IN, plays Walk.png
- hero `WallSlide` (wallSlide): STAND-IN, plays Grabbed.png
- hero `WallJump` (wallJump): STAND-IN, plays Jump.png
- hero `GrappleThrow` (grappleThrow): MISSING, plays -
- hero `GrappleSwing` (grappleSwing): MISSING, plays -
- hero `LedgeGrab` (ledgeGrab): MISSING, plays -
- hero `LedgeClimb` (ledgeClimb): MISSING, plays -
- hero `Graze` (graze): MISSING, plays -
- hero `Hurt` (hurt): STAND-IN, plays Grabbed.png
- hero `Victory` (victory): STAND-IN, plays Shout.png
- hero `DeathBurn` (deathBurn): STAND-IN, plays Fall.png
- hero `DeathSquash` (deathSquash): STAND-IN, plays Grabbed.png
- hero `DeathSpin` (deathSpin): STAND-IN, plays Jump.png
- hero `DeathShrug` (deathShrug): STAND-IN, plays Shout.png
- weapon `boomerangHalo`: a block prop (look) is drawn over the sheets
- weapon `chainWhip`: a block prop (look) is drawn over the sheets
- weapon `fists`: a block prop (look) is drawn over the sheets
- weapon `ricochetPistol`: a block prop (look) is drawn over the sheets
- weapon `rocketLauncher`: a block prop (look) is drawn over the sheets
- weapon `shotgun`: a block prop (look) is drawn over the sheets
- weapon `slagLadle`: a block prop (look) is drawn over the sheets
- weapon `spitfire`: a block prop (look) is drawn over the sheets

</details>

## Still to do

- entity `afterimage`: STAND-IN, wears chaser's art tinted #D8E8FF
- entity `bounceLump`: STAND-IN, wears projectileEnemy's art tinted #E0602A
- entity `bridgeWraith`: PLACEHOLDER, drawn as a labeled circle
- entity `brimstoneLump`: STAND-IN, wears slag's art tinted #E0602A
- entity `chainEmber`: STAND-IN, wears projectileEnemy's art tinted #B0A090
- entity `drone`: STAND-IN, wears shooter's art tinted #9FFFE0
- entity `gunner`: STAND-IN, wears shooter's art tinted #FFD27A
- entity `haloEmber`: STAND-IN, wears projectileEnemy's art tinted #FFD27A
- entity `haloImp`: STAND-IN, wears imp's art tinted #FFD27A
- entity `haloShot`: STAND-IN, its own animator plays Images tinted #FFD27A
- entity `lavaBurst`: STAND-IN, its own animator plays Beam tinted #FF7A2A
- entity `lavaFlood`: STAND-IN, wears fireball's art tinted #FF5A1A
- entity `lavaWave`: STAND-IN, wears fireball's art tinted #FF8A2A
- entity `magmaBoulder`: STAND-IN, wears fireball's art tinted #D2551E
- entity `mkWarnFlood`: STAND-IN, wears overlordBeamGlow's art tinted #FF3A1A
- entity `mkWarnFloor`: STAND-IN, wears overlordBeamGlow's art tinted #FFB060
- entity `overlordFar806`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar857`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar896`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar906`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar953`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar958`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `overlordFar994`: STAND-IN, its own animator plays MagmaKing tinted #3B2F46
- entity `plateShard`: STAND-IN, wears projectileEnemy's art tinted #D8D0F0
- entity `quickfireImp`: STAND-IN, wears imp's art tinted #FF9A5A
- entity `ricochetEmber`: STAND-IN, wears projectileEnemy's art tinted #FFC15A
- entity `ricochetImp`: STAND-IN, wears imp's art tinted #FFC890
- entity `ricochetShot`: STAND-IN, its own animator plays Images tinted #7FE8FF
- entity `rook`: STAND-IN, wears damnedSoul's art tinted #F2A93B
- entity `runeHound`: STAND-IN, wears slagHound's art tinted #A9BFDA
- entity `seamEmber`: STAND-IN, wears projectileEnemy's art tinted #6FE0C8
- entity `seamImp`: STAND-IN, wears imp's art tinted #6FE0C8
- entity `sfNeedle`: STAND-IN, its own animator plays Climb tinted #D8C8D0
- entity `sfSpear`: STAND-IN, wears fireball's art tinted #5A4660
- entity `shard`: STAND-IN, wears slag's art tinted #5A4A66
- entity `shardBig`: STAND-IN, wears slag's art tinted #5A4A66
- entity `shardwright`: STAND-IN, wears brimstoneDemon's art tinted #5A5870
- entity `slabSniper`: STAND-IN, wears imp's art tinted #B0B8C0
- entity `slagTortoise`: STAND-IN, wears brimstoneDemon's art tinted #8A7468
- entity `slamShockwave`: STAND-IN, wears fireball's art tinted #FFB050
- entity `sleepwalker`: STAND-IN, wears chaser's art tinted #B9C9E8
- entity `sparedWhelp`: STAND-IN, its own animator plays Images tinted #FF9A7A
- entity `spinner`: STAND-IN, wears sentinel's art tinted #7FD8FF
- entity `spireFiend`: PLACEHOLDER, drawn as a labeled circle
- entity `sprayer`: STAND-IN, wears shooter's art tinted #D69CFF
- entity `targetDummy`: PLACEHOLDER, drawn as a labeled circle
- entity `tortoiseEmber`: STAND-IN, wears projectileEnemy's art tinted #C8A27A
- entity `tripleLobber`: STAND-IN, wears bounceLobber's art tinted #E8C898
- entity `twiceRisen`: STAND-IN, wears risenDemon's art tinted #9A7A8A
- entity `twinLensImp`: STAND-IN, wears lensImp's art tinted #FF3A3A
- entity `veilDemon`: STAND-IN, wears brimstoneDemon's art tinted #7A6A98
- entity `veilEmber`: STAND-IN, wears projectileEnemy's art tinted #B08AFF
- entity `veilMatron`: STAND-IN, wears brimstoneDemon's art tinted #A890E0
- entity `watcher`: STAND-IN, its own animator plays Images tinted #CFF4FF
- entity `watcherPatrol`: STAND-IN, its own animator plays Images tinted #CFF4FF
- entity `waystoneGlow`: STAND-IN, wears overlordBeamGlow's art tinted #9EFFE6
- entity `whelpNest`: PLACEHOLDER, drawn as a coloured box
- entity `whirlEmber`: STAND-IN, wears projectileEnemy's art tinted #FF7AC8
- entity `whirlImp`: STAND-IN, wears imp's art tinted #FF7AC8
- entity `wick`: STAND-IN, wears damnedSoul's art tinted #BFE6C8
- entity `wraithEmber`: STAND-IN, wears projectileEnemy's art tinted #9AD8E8
- hero `Land` (land): MISSING, plays -
- hero `AirFire` (airFire): MISSING, plays -
- hero `RunFire` (runFire): STAND-IN, plays Walk.png
- hero `EquipLauncher` (equipLauncher): STAND-IN, plays Curious.png
- hero `LauncherTaken` (launcherAway): STAND-IN, plays Grabbed.png
- hero `ShotgunIdle` (shotgunIdle): MISSING, plays -
- hero `ShotgunRun` (shotgunRun): MISSING, plays -
- hero `ShotgunJump` (shotgunJump): MISSING, plays -
- hero `ShotgunFall` (shotgunFall): MISSING, plays -
- hero `ShotgunLand` (shotgunLand): MISSING, plays -
- hero `EquipShotgun` (shotgunEquip): MISSING, plays -
- hero `ShotgunAttack` (shotgunAttack): MISSING, plays -
- hero `ShotgunRunAttack` (shotgunRunAttack): MISSING, plays -
- hero `ShotgunAirAttack` (shotgunAirAttack): MISSING, plays -
- hero `SpitfireIdle` (spitfireIdle): MISSING, plays -
- hero `SpitfireRun` (spitfireRun): MISSING, plays -
- hero `SpitfireJump` (spitfireJump): MISSING, plays -
- hero `SpitfireFall` (spitfireFall): MISSING, plays -
- hero `SpitfireLand` (spitfireLand): MISSING, plays -
- hero `EquipSpitfire` (spitfireEquip): MISSING, plays -
- hero `SpitfireAttack` (spitfireAttack): MISSING, plays -
- hero `SpitfireRunAttack` (spitfireRunAttack): MISSING, plays -
- hero `SpitfireAirAttack` (spitfireAirAttack): MISSING, plays -
- hero `PistolIdle` (ricochetPistolIdle): MISSING, plays -
- hero `PistolRun` (ricochetPistolRun): MISSING, plays -
- hero `PistolJump` (ricochetPistolJump): MISSING, plays -
- hero `PistolFall` (ricochetPistolFall): MISSING, plays -
- hero `PistolLand` (ricochetPistolLand): MISSING, plays -
- hero `EquipPistol` (ricochetPistolEquip): MISSING, plays -
- hero `PistolAttack` (ricochetPistolAttack): MISSING, plays -
- hero `PistolRunAttack` (ricochetPistolRunAttack): MISSING, plays -
- hero `PistolAirAttack` (ricochetPistolAirAttack): MISSING, plays -
- hero `LadleIdle` (slagLadleIdle): MISSING, plays -
- hero `LadleRun` (slagLadleRun): MISSING, plays -
- hero `LadleJump` (slagLadleJump): MISSING, plays -
- hero `LadleFall` (slagLadleFall): MISSING, plays -
- hero `LadleLand` (slagLadleLand): MISSING, plays -
- hero `EquipLadle` (slagLadleEquip): MISSING, plays -
- hero `LadleAttack` (slagLadleAttack): MISSING, plays -
- hero `LadleRunAttack` (slagLadleRunAttack): MISSING, plays -
- hero `LadleAirAttack` (slagLadleAirAttack): MISSING, plays -
- hero `HaloIdle` (boomerangHaloIdle): MISSING, plays -
- hero `HaloRun` (boomerangHaloRun): MISSING, plays -
- hero `HaloJump` (boomerangHaloJump): MISSING, plays -
- hero `HaloFall` (boomerangHaloFall): MISSING, plays -
- hero `HaloLand` (boomerangHaloLand): MISSING, plays -
- hero `EquipHalo` (boomerangHaloEquip): MISSING, plays -
- hero `HaloAttack` (boomerangHaloAttack): MISSING, plays -
- hero `HaloRunAttack` (boomerangHaloRunAttack): MISSING, plays -
- hero `HaloAirAttack` (boomerangHaloAirAttack): MISSING, plays -
- hero `WhipIdle` (chainWhipIdle): MISSING, plays -
- hero `WhipRun` (chainWhipRun): MISSING, plays -
- hero `WhipJump` (chainWhipJump): MISSING, plays -
- hero `WhipFall` (chainWhipFall): MISSING, plays -
- hero `WhipLand` (chainWhipLand): MISSING, plays -
- hero `EquipWhip` (chainWhipEquip): MISSING, plays -
- hero `WhipLash` (whipLash): MISSING, plays -
- hero `WhipSnap` (whipSnap): MISSING, plays -
- hero `WhipCrack` (whipCrack): MISSING, plays -
- hero `WhipRunAttack` (whipRunAttack): MISSING, plays -
- hero `WhipAirAttack` (whipAirAttack): MISSING, plays -
- hero `UnarmedIdle` (fistsIdle): STAND-IN, plays Idle.png
- hero `UnarmedWalk` (fistsRun): STAND-IN, plays Walk.png
- hero `UnarmedJump` (fistsJump): STAND-IN, plays Jump.png
- hero `UnarmedFall` (fistsFall): STAND-IN, plays Fall.png
- hero `UnarmedLand` (fistsLand): MISSING, plays -
- hero `EquipFists` (fistsEquip): STAND-IN, plays Shout.png
- hero `Punch1` (punch1): STAND-IN, plays Fire.png
- hero `Punch2` (punch2): STAND-IN, plays Fire.png
- hero `Punch3` (punch3): STAND-IN, plays Fire.png
- hero `UnarmedRunAttack` (fistsRunAttack): MISSING, plays -
- hero `UnarmedAirAttack` (fistsAirAttack): MISSING, plays -
- hero `Dash` (dash): STAND-IN, plays Walk.png
- hero `WallSlide` (wallSlide): STAND-IN, plays Grabbed.png
- hero `WallJump` (wallJump): STAND-IN, plays Jump.png
- hero `GrappleThrow` (grappleThrow): MISSING, plays -
- hero `GrappleSwing` (grappleSwing): MISSING, plays -
- hero `Graze` (graze): MISSING, plays -
- hero `Hurt` (hurt): STAND-IN, plays Grabbed.png
- hero `DeathBurn` (deathBurn): STAND-IN, plays Fall.png
- hero `DeathSquash` (deathSquash): STAND-IN, plays Grabbed.png
- hero `DeathSpin` (deathSpin): STAND-IN, plays Jump.png
- hero `DeathShrug` (deathShrug): STAND-IN, plays Shout.png
- hero `Victory` (victory): STAND-IN, plays Shout.png
- weapon `boomerangHalo`: 9 states; without a sheet of their own: HaloIdle, HaloRun, HaloJump, HaloFall, HaloLand, HaloRunAttack, HaloAirAttack, EquipHalo, HaloAttack
- weapon `chainWhip`: 11 states; without a sheet of their own: WhipIdle, WhipRun, WhipJump, WhipFall, WhipLand, WhipRunAttack, WhipAirAttack, EquipWhip, WhipLash, WhipSnap, WhipCrack
- weapon `fists`: 11 states; without a sheet of their own: UnarmedIdle, UnarmedWalk, UnarmedJump, UnarmedFall, UnarmedLand, UnarmedRunAttack, UnarmedAirAttack, EquipFists, Punch1, Punch2, Punch3
- weapon `ricochetPistol`: 9 states; without a sheet of their own: PistolIdle, PistolRun, PistolJump, PistolFall, PistolLand, PistolRunAttack, PistolAirAttack, EquipPistol, PistolAttack
- weapon `rocketLauncher`: 9 states; without a sheet of their own: Land, RunFire, AirFire, EquipLauncher
- weapon `shotgun`: 9 states; without a sheet of their own: ShotgunIdle, ShotgunRun, ShotgunJump, ShotgunFall, ShotgunLand, ShotgunRunAttack, ShotgunAirAttack, EquipShotgun, ShotgunAttack
- weapon `slagLadle`: 9 states; without a sheet of their own: LadleIdle, LadleRun, LadleJump, LadleFall, LadleLand, LadleRunAttack, LadleAirAttack, EquipLadle, LadleAttack
- weapon `spitfire`: 9 states; without a sheet of their own: SpitfireIdle, SpitfireRun, SpitfireJump, SpitfireFall, SpitfireLand, SpitfireRunAttack, SpitfireAirAttack, EquipSpitfire, SpitfireAttack
- grudge badBlood: card icon is a letter "B"
- grudge eyeForAnEye: card icon is a letter "E"
- grudge heldGrudge: card icon is a letter "G"
- grudge holdingOn: card icon is a letter "H"
- grudge keepingScore: card icon is a letter "K"
- grudge pettyCash: card icon is a letter "P"
- grudge rubbingItIn: card icon is a letter "R"
- grudge sideEye: card icon is a letter "I"
- grudge slowBurn: card icon is a letter "F"
- grudge sourGrapes: card icon is a letter "S"
- good crustyBits: card icon is a letter "P"
- good thickerCrusts: card icon is a letter "P2"
- good snotBubble: card icon is a letter "B"
- good spareBubble: card icon is a letter "B2"
- good greasedElbows: card icon is a letter "S"
- good luckyLick: card icon is a letter "C"
- good fourLeafSlime: card icon is a letter "C2"
- good meanerLick: card icon is a letter "M"
- good yoobaBurp: card icon is a letter "H"
- good hungrierBurp: card icon is a letter "H2"
- good gravyNet: card icon is a letter "N"
- good fatDiamonds: card icon is a letter "F"
- good stickyAir: card icon is a letter "G"
- good loopCrumbs: card icon is a letter "K"
- good thriftyLoop: card icon is a letter "L"
- good belchBomb: card icon is a letter "X"
- good louderBelch: card icon is a letter "X2"
- good afterglowGoo: card icon is a letter "A"
- good slipperyShots: card icon is a letter "Z"
- good floorPudding: card icon is a letter "D"
- good spareGasp: card icon is a letter "Y"

## The hero

| State | hero.json clip | Status | Sheet |
|---|---|---|---|
| AirFire | airFire | MISSING | - |
| Climb | climbUp | ok | Climb.png |
| ClimbDown | climbDown | ok | ClimbDown.png |
| ClimbIdle | climbIdle | ok | ClimbIdle.png |
| Curious | curious | ok | Curious.png |
| Dash | dash | STAND-IN | Walk.png |
| DeathBurn | deathBurn | STAND-IN | Fall.png |
| DeathShrug | deathShrug | STAND-IN | Shout.png |
| DeathSpin | deathSpin | STAND-IN | Jump.png |
| DeathSquash | deathSquash | STAND-IN | Grabbed.png |
| EquipFists | fistsEquip | STAND-IN | Shout.png |
| EquipHalo | boomerangHaloEquip | MISSING | - |
| EquipLadle | slagLadleEquip | MISSING | - |
| EquipLauncher | equipLauncher | STAND-IN | Curious.png |
| EquipPistol | ricochetPistolEquip | MISSING | - |
| EquipShotgun | shotgunEquip | MISSING | - |
| EquipSpitfire | spitfireEquip | MISSING | - |
| EquipWhip | chainWhipEquip | MISSING | - |
| Grabbed | grabbed | ok | Grabbed.png |
| GrappleSwing | grappleSwing | MISSING | - |
| GrappleThrow | grappleThrow | MISSING | - |
| Graze | graze | MISSING | - |
| HaloAirAttack | boomerangHaloAirAttack | MISSING | - |
| HaloAttack | boomerangHaloAttack | MISSING | - |
| HaloFall | boomerangHaloFall | MISSING | - |
| HaloIdle | boomerangHaloIdle | MISSING | - |
| HaloJump | boomerangHaloJump | MISSING | - |
| HaloLand | boomerangHaloLand | MISSING | - |
| HaloRun | boomerangHaloRun | MISSING | - |
| HaloRunAttack | boomerangHaloRunAttack | MISSING | - |
| Hurt | hurt | STAND-IN | Grabbed.png |
| LadleAirAttack | slagLadleAirAttack | MISSING | - |
| LadleAttack | slagLadleAttack | MISSING | - |
| LadleFall | slagLadleFall | MISSING | - |
| LadleIdle | slagLadleIdle | MISSING | - |
| LadleJump | slagLadleJump | MISSING | - |
| LadleLand | slagLadleLand | MISSING | - |
| LadleRun | slagLadleRun | MISSING | - |
| LadleRunAttack | slagLadleRunAttack | MISSING | - |
| Land | land | MISSING | - |
| LauncherTaken | launcherAway | STAND-IN | Grabbed.png |
| PistolAirAttack | ricochetPistolAirAttack | MISSING | - |
| PistolAttack | ricochetPistolAttack | MISSING | - |
| PistolFall | ricochetPistolFall | MISSING | - |
| PistolIdle | ricochetPistolIdle | MISSING | - |
| PistolJump | ricochetPistolJump | MISSING | - |
| PistolLand | ricochetPistolLand | MISSING | - |
| PistolRun | ricochetPistolRun | MISSING | - |
| PistolRunAttack | ricochetPistolRunAttack | MISSING | - |
| Punch1 | punch1 | STAND-IN | Fire.png |
| Punch2 | punch2 | STAND-IN | Fire.png |
| Punch3 | punch3 | STAND-IN | Fire.png |
| RunFire | runFire | STAND-IN | Walk.png |
| ShotgunAirAttack | shotgunAirAttack | MISSING | - |
| ShotgunAttack | shotgunAttack | MISSING | - |
| ShotgunFall | shotgunFall | MISSING | - |
| ShotgunIdle | shotgunIdle | MISSING | - |
| ShotgunJump | shotgunJump | MISSING | - |
| ShotgunLand | shotgunLand | MISSING | - |
| ShotgunRun | shotgunRun | MISSING | - |
| ShotgunRunAttack | shotgunRunAttack | MISSING | - |
| Shout | shout | ok | Shout.png |
| SpitfireAirAttack | spitfireAirAttack | MISSING | - |
| SpitfireAttack | spitfireAttack | MISSING | - |
| SpitfireFall | spitfireFall | MISSING | - |
| SpitfireIdle | spitfireIdle | MISSING | - |
| SpitfireJump | spitfireJump | MISSING | - |
| SpitfireLand | spitfireLand | MISSING | - |
| SpitfireRun | spitfireRun | MISSING | - |
| SpitfireRunAttack | spitfireRunAttack | MISSING | - |
| UnarmedAirAttack | fistsAirAttack | MISSING | - |
| UnarmedFall | fistsFall | STAND-IN | Fall.png |
| UnarmedIdle | fistsIdle | STAND-IN | Idle.png |
| UnarmedJump | fistsJump | STAND-IN | Jump.png |
| UnarmedLand | fistsLand | MISSING | - |
| UnarmedRunAttack | fistsRunAttack | MISSING | - |
| UnarmedWalk | fistsRun | STAND-IN | Walk.png |
| Victory | victory | STAND-IN | Shout.png |
| WallJump | wallJump | STAND-IN | Jump.png |
| WallSlide | wallSlide | STAND-IN | Grabbed.png |
| WhipAirAttack | whipAirAttack | MISSING | - |
| WhipCrack | whipCrack | MISSING | - |
| WhipFall | chainWhipFall | MISSING | - |
| WhipIdle | chainWhipIdle | MISSING | - |
| WhipJump | chainWhipJump | MISSING | - |
| WhipLand | chainWhipLand | MISSING | - |
| WhipLash | whipLash | MISSING | - |
| WhipRun | chainWhipRun | MISSING | - |
| WhipRunAttack | whipRunAttack | MISSING | - |
| WhipSnap | whipSnap | MISSING | - |

Ledge grab and ledge climb have no clip: the game has no ledge mechanic (nothing in PlayerController grabs a ledge).

## Weapons (grips)

| Weapon | Status | States |
|---|---|---|
| boomerangHalo | STAND-IN | 9 states; without a sheet of their own: HaloIdle, HaloRun, HaloJump, HaloFall, HaloLand, HaloRunAttack, HaloAirAttack, EquipHalo, HaloAttack |
| chainWhip | STAND-IN | 11 states; without a sheet of their own: WhipIdle, WhipRun, WhipJump, WhipFall, WhipLand, WhipRunAttack, WhipAirAttack, EquipWhip, WhipLash, WhipSnap, WhipCrack |
| fists | STAND-IN | 11 states; without a sheet of their own: UnarmedIdle, UnarmedWalk, UnarmedJump, UnarmedFall, UnarmedLand, UnarmedRunAttack, UnarmedAirAttack, EquipFists, Punch1, Punch2, Punch3 |
| ricochetPistol | STAND-IN | 9 states; without a sheet of their own: PistolIdle, PistolRun, PistolJump, PistolFall, PistolLand, PistolRunAttack, PistolAirAttack, EquipPistol, PistolAttack |
| rocketLauncher | STAND-IN | 9 states; without a sheet of their own: Land, RunFire, AirFire, EquipLauncher |
| shotgun | STAND-IN | 9 states; without a sheet of their own: ShotgunIdle, ShotgunRun, ShotgunJump, ShotgunFall, ShotgunLand, ShotgunRunAttack, ShotgunAirAttack, EquipShotgun, ShotgunAttack |
| slagLadle | STAND-IN | 9 states; without a sheet of their own: LadleIdle, LadleRun, LadleJump, LadleFall, LadleLand, LadleRunAttack, LadleAirAttack, EquipLadle, LadleAttack |
| spitfire | STAND-IN | 9 states; without a sheet of their own: SpitfireIdle, SpitfireRun, SpitfireJump, SpitfireFall, SpitfireLand, SpitfireRunAttack, SpitfireAirAttack, EquipSpitfire, SpitfireAttack |

## Circles (backdrop layers and far art)

| Circle | Status | Its art |
|---|---|---|
| crackedRoad | ok | Rift_loop.png, RoadIsland_still.png, RoadSpires_still.png |
| emberWastes | ok | FarSea_loop.png, Island_still.png, Spires_still.png |
| runeBridges | ok | RuneArches_still.png, RuneIsland_still.png, RuneVoid_loop.png |

## Card icons

| Card | Status | Icon |
|---|---|---|
| grudge badBlood | PLACEHOLDER | letter "B" |
| grudge bigDamage | ok (no badge) | no badge |
| grudge eyeForAnEye | PLACEHOLDER | letter "E" |
| grudge heldBreath | ok (no badge) | no badge |
| grudge heldGrudge | PLACEHOLDER | letter "G" |
| grudge holdingOn | PLACEHOLDER | letter "H" |
| grudge keepingScore | PLACEHOLDER | letter "K" |
| grudge notToday | ok (no badge) | no badge |
| grudge pettyCash | PLACEHOLDER | letter "P" |
| grudge rapidFire | ok (no badge) | no badge |
| grudge rubbingItIn | PLACEHOLDER | letter "R" |
| grudge sideEye | PLACEHOLDER | letter "I" |
| grudge slowBurn | PLACEHOLDER | letter "F" |
| grudge sneakers | ok (no badge) | no badge |
| grudge snoozeButton | ok (no badge) | no badge |
| grudge sourGrapes | PLACEHOLDER | letter "S" |
| grudge spreadShot | ok (no badge) | no badge |
| grudge thickSkin | ok (no badge) | no badge |
| good moreHealth | ok (no badge) | no badge |
| good extraJump | ok (no badge) | no badge |
| good comfySneakers | ok (no badge) | no badge |
| good triggerFinger | ok (no badge) | no badge |
| good spicyFireballs | ok (no badge) | no badge |
| good juiceBox | ok (no badge) | no badge |
| good bottomlessJar | ok (no badge) | no badge |
| good quickUnwind | ok (no badge) | no badge |
| good diamondMagnet | ok (no badge) | no badge |
| good doubleDip | ok (no badge) | no badge |
| good fistGoo | ok (no badge) | no badge |
| good fistReach | ok (no badge) | no badge |
| good keepWeapons | ok (no badge) | no badge |
| good crustyBits | PLACEHOLDER | letter "P" |
| good thickerCrusts | PLACEHOLDER | letter "P2" |
| good snotBubble | PLACEHOLDER | letter "B" |
| good spareBubble | PLACEHOLDER | letter "B2" |
| good greasedElbows | PLACEHOLDER | letter "S" |
| good luckyLick | PLACEHOLDER | letter "C" |
| good fourLeafSlime | PLACEHOLDER | letter "C2" |
| good meanerLick | PLACEHOLDER | letter "M" |
| good yoobaBurp | PLACEHOLDER | letter "H" |
| good hungrierBurp | PLACEHOLDER | letter "H2" |
| good gravyNet | PLACEHOLDER | letter "N" |
| good fatDiamonds | PLACEHOLDER | letter "F" |
| good stickyAir | PLACEHOLDER | letter "G" |
| good loopCrumbs | PLACEHOLDER | letter "K" |
| good thriftyLoop | PLACEHOLDER | letter "L" |
| good belchBomb | PLACEHOLDER | letter "X" |
| good louderBelch | PLACEHOLDER | letter "X2" |
| good afterglowGoo | PLACEHOLDER | letter "A" |
| good slipperyShots | PLACEHOLDER | letter "Z" |
| good floorPudding | PLACEHOLDER | letter "D" |
| good spareGasp | PLACEHOLDER | letter "Y" |
| good slimedLining | ok (no badge) | no badge |

## Entities in use

| Entity | Team | Status | How it is drawn | Art spec |
|---|---|---|---|---|
| afterimage | Enemy | STAND-IN | wears chaser's art tinted #D8E8FF | afterimage |
| alphaHound | Enemy | ok | generated: Art/Enemies/AlphaHound | alphaHound |
| baronSpillway | Enemy | ok | generated: Art/Enemies/BaronSpillway | baronSpillway |
| bastionBrute | Enemy | ok | generated: Art/Enemies/BastionBrute | bastionBrute |
| beamLens | Enemy | ok | generated: Art/Enemies/BeamLens | beamLens |
| blinkImp | Enemy | ok | generated: Art/Enemies/BlinkImp | blinkImp |
| blinkMark | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #C58BFF |  |
| boomerangHaloPickup | Neutral | ok | generated: Art/Enemies/BoomerangHaloPickup | boomerangHaloPickup |
| bounceLobber | Enemy | ok | generated: Art/Enemies/BounceLobber | bounceLobber |
| bounceLump | Enemy | STAND-IN | wears projectileEnemy's art tinted #E0602A | bounceLump |
| brLampBolt | Enemy | ok | runeBolt's effect sheet (made to be tinted), in #BFEFFF |  |
| brMarkBolt | Enemy | ok | runeBolt's effect sheet (made to be tinted), in #E8F6FF |  |
| brRuneColumn | Enemy | ok | generated: Art/Effects/RuneColumn |  |
| brWallHit | Enemy | ok | generated: Art/Effects/RuneColumn |  |
| brWarnLamp | Neutral | ok | mkWarnHand's effect sheet (made to be tinted), in #BFEFFF |  |
| brWarnRune | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #BFEFFF |  |
| brWarnWall | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #8FE3FF |  |
| bridgeWraith | Enemy | PLACEHOLDER | drawn as a labeled circle | bridgeWraith |
| brimstoneBreath | Enemy | ok | the same creature as fireball (its art, untinted) |  |
| brimstoneDemon | Enemy | ok | generated: Art/Enemies/BrimstoneDemon | brimstoneDemon |
| brimstoneLump | Enemy | STAND-IN | wears slag's art tinted #E0602A |  |
| brimstoneVent | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| brimstoneVentB | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| brimstoneVentC | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| brimstoneVentD | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| broodWhelp | Enemy | ok | generated: Art/Enemies/BroodWhelp | broodWhelp |
| brute | Enemy | ok | generated: Art/Enemies/Brute | brute |
| bsHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| bsWarn | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFB060 |  |
| bulwarkImp | Enemy | ok | generated: Art/Enemies/BulwarkImp | bulwarkImp |
| bystanderSoul | Neutral | ok | the same creature as damnedSoul (its art, untinted) |  |
| carver | Enemy | ok | generated: Art/Enemies/Carver | carver |
| ceilingSlab | Neutral | ok | generated: Art/Climb |  |
| chainBrute | Enemy | ok | generated: Art/Enemies/ChainBrute | chainBrute |
| chainEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #B0A090 |  |
| chainLift | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| chainLift991 | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| chainLift993 | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| chainLift994a | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| chainLift994b | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| chainWhelp | Enemy | ok | generated: Art/Enemies/ChainWhelp | chainWhelp |
| chainWhipPickup | Neutral | ok | generated: Art/Enemies/ChainWhipPickup | chainWhipPickup |
| cheerSoul | Neutral | ok | generated: Art/Enemies/CheerSoul | cheerSoul |
| cinder | Enemy | ok | generated: Art/Enemies/Cinder | cinder |
| cinderBrute | Enemy | ok | generated: Art/Enemies/CinderBrute | cinderBrute |
| cinderSpark | Enemy | ok | generated: Art/Enemies/CinderSpark | cinderSpark |
| cinderThatch | Enemy | ok | generated: Art/Enemies/CinderThatch | cinderThatch |
| collapseSlab | Neutral | ok | generated: Art/Climb |  |
| coronaImp | Enemy | ok | generated: Art/Enemies/CoronaImp | coronaImp |
| counterweightAL | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightAR | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightBL | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightBR | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightCL | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightCR | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightP1L | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightP1R | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightP2L | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightP2R | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightP3L | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| counterweightP3R | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| crumblingStone | Neutral | ok | generated: Art/Climb |  |
| cvHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| cvSpotHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| cvWarnLine | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFB060 |  |
| cvWarnSpot | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFE08A |  |
| damnedSoul | Neutral | ok | generated: Art/Enemies/DamnedSoul | damnedSoul |
| deadeyeBolt | Enemy | ok | generated: Art/Effects/Bolt |  |
| deadeyeImp | Enemy | ok | generated: Art/Enemies/DeadeyeImp | deadeyeImp |
| debrisRaft | Neutral | ok | generated: Art/Climb |  |
| debrisRaftHay | Neutral | ok | generated: Art/Climb |  |
| debrisRaftRoof | Neutral | ok | generated: Art/Climb |  |
| debrisRaftSign | Neutral | ok | generated: Art/Climb |  |
| debrisRaftSignBack | Neutral | ok | the same creature as debrisRaftSign (its art, untinted) |  |
| demonFirst | Enemy | ok | the same creature as brimstoneDemon (its art, untinted) |  |
| demonLobber | Enemy | ok | generated: Art/Enemies/DemonLobber | demonLobber |
| demonWhelp | Enemy | ok | generated: Art/Enemies/DemonWhelp | demonWhelp |
| diamond | Neutral | ok | painted: Images |  |
| diamondGlint | Neutral | ok | the same creature as diamond (its art, untinted) |  |
| drone | Enemy | STAND-IN | wears shooter's art tinted #9FFFE0 | drone |
| emberMine | Enemy | ok | generated: Art/Enemies/EmberMine | emberMine |
| fireball | Enemy | ok | painted: Images |  |
| floeRaft | Neutral | ok | the same creature as debrisRaft (its art, untinted) |  |
| floorLanding | Neutral | ok | generated: Art/Climb |  |
| floorLanding1000 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding702 | Neutral | ok | generated: Art/Climb |  |
| floorLanding703 | Neutral | ok | generated: Art/Climb |  |
| floorLanding704 | Neutral | ok | generated: Art/Climb |  |
| floorLanding705 | Neutral | ok | generated: Art/Climb |  |
| floorLanding706 | Neutral | ok | generated: Art/Climb |  |
| floorLanding707 | Neutral | ok | generated: Art/Climb |  |
| floorLanding708 | Neutral | ok | generated: Art/Climb |  |
| floorLanding709 | Neutral | ok | generated: Art/Climb |  |
| floorLanding710 | Neutral | ok | generated: Art/Climb |  |
| floorLanding711 | Neutral | ok | generated: Art/Climb |  |
| floorLanding712 | Neutral | ok | generated: Art/Climb |  |
| floorLanding713 | Neutral | ok | generated: Art/Climb |  |
| floorLanding714 | Neutral | ok | generated: Art/Climb |  |
| floorLanding715 | Neutral | ok | generated: Art/Climb |  |
| floorLanding716 | Neutral | ok | generated: Art/Climb |  |
| floorLanding717 | Neutral | ok | generated: Art/Climb |  |
| floorLanding718 | Neutral | ok | generated: Art/Climb |  |
| floorLanding719 | Neutral | ok | generated: Art/Climb |  |
| floorLanding720 | Neutral | ok | generated: Art/Climb |  |
| floorLanding721 | Neutral | ok | generated: Art/Climb |  |
| floorLanding722 | Neutral | ok | generated: Art/Climb |  |
| floorLanding723 | Neutral | ok | generated: Art/Climb |  |
| floorLanding724 | Neutral | ok | generated: Art/Climb |  |
| floorLanding725 | Neutral | ok | generated: Art/Climb |  |
| floorLanding726 | Neutral | ok | generated: Art/Climb |  |
| floorLanding727 | Neutral | ok | generated: Art/Climb |  |
| floorLanding728 | Neutral | ok | generated: Art/Climb |  |
| floorLanding729 | Neutral | ok | generated: Art/Climb |  |
| floorLanding730 | Neutral | ok | generated: Art/Climb |  |
| floorLanding731 | Neutral | ok | generated: Art/Climb |  |
| floorLanding732 | Neutral | ok | generated: Art/Climb |  |
| floorLanding733 | Neutral | ok | generated: Art/Climb |  |
| floorLanding734 | Neutral | ok | generated: Art/Climb |  |
| floorLanding735 | Neutral | ok | generated: Art/Climb |  |
| floorLanding736 | Neutral | ok | generated: Art/Climb |  |
| floorLanding737 | Neutral | ok | generated: Art/Climb |  |
| floorLanding738 | Neutral | ok | generated: Art/Climb |  |
| floorLanding739 | Neutral | ok | generated: Art/Climb |  |
| floorLanding740 | Neutral | ok | generated: Art/Climb |  |
| floorLanding741 | Neutral | ok | generated: Art/Climb |  |
| floorLanding742 | Neutral | ok | generated: Art/Climb |  |
| floorLanding743 | Neutral | ok | generated: Art/Climb |  |
| floorLanding744 | Neutral | ok | generated: Art/Climb |  |
| floorLanding745 | Neutral | ok | generated: Art/Climb |  |
| floorLanding746 | Neutral | ok | generated: Art/Climb |  |
| floorLanding747 | Neutral | ok | generated: Art/Climb |  |
| floorLanding748 | Neutral | ok | generated: Art/Climb |  |
| floorLanding749 | Neutral | ok | generated: Art/Climb |  |
| floorLanding750 | Neutral | ok | generated: Art/Climb |  |
| floorLanding792 | Neutral | ok | generated: Art/Climb |  |
| floorLanding793 | Neutral | ok | generated: Art/Climb |  |
| floorLanding794 | Neutral | ok | generated: Art/Climb |  |
| floorLanding795 | Neutral | ok | generated: Art/Climb |  |
| floorLanding796 | Neutral | ok | generated: Art/Climb |  |
| floorLanding797 | Neutral | ok | generated: Art/Climb |  |
| floorLanding798 | Neutral | ok | generated: Art/Climb |  |
| floorLanding799 | Neutral | ok | generated: Art/Climb |  |
| floorLanding800 | Neutral | ok | generated: Art/Climb |  |
| floorLanding802 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding803 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding804 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding805 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding806 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding807 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding808 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding809 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding810 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding851 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding852 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding853 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding854 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding855 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding856 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding857 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding858 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding859 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding860 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding891 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding892 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding893 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding894 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding895 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding896 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding897 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding898 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding899 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding900 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding902 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding903 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding904 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding905 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding906 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding907 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding908 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding909 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding910 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding951 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding952 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding953 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding953b | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding954 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding955 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding956 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding957 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding958 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding959 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding960 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding991 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding992 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding993 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding994 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding995 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding996 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding997 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding998 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| floorLanding999 | Neutral | ok | the same creature as floorLanding (its art, untinted) |  |
| fmEmber | Enemy | ok | the same creature as slag (its art, untinted) |  |
| fmLash | Enemy | ok | generated: Art/Effects/Eruption |  |
| fmSackDrip | Neutral | ok | the same creature as slagDrip (its art, untinted) |  |
| fmWarnWhip | Neutral | ok | generated: Art/Effects/Warn |  |
| furnacePlate | Enemy | ok | generated: Art/Enemies/FurnacePlate | furnacePlate |
| fuseWhelp | Enemy | ok | generated: Art/Enemies/FuseWhelp | fuseWhelp |
| geodeBit | Enemy | ok | generated: Art/Enemies/GeodeBit | geodeBit |
| giverSoul | Neutral | ok | generated: Art/Enemies/GiverSoul | giverSoul |
| grappleHook | Neutral | ok | invisible body; the grappleAnchor ability paints the anchor (art/props, AnchorRing) |  |
| grappleRing | Neutral | ok | invisible body; the grappleAnchor ability paints the anchor (art/props, AnchorRing) |  |
| gravityPad | Neutral | ok | generated: Art/Effects/Gravity |  |
| gunner | Enemy | STAND-IN | wears shooter's art tinted #FFD27A | gunner |
| haloEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #FFD27A |  |
| haloImp | Enemy | STAND-IN | wears imp's art tinted #FFD27A | haloImp |
| haloShot | Player | STAND-IN | its own animator plays Images tinted #FFD27A | haloShot |
| hawkerSoul | Neutral | ok | generated: Art/Enemies/HawkerSoul | hawkerSoul |
| heatUpdraft | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| heatUpdraftTall | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| hecklerSoul | Neutral | ok | generated: Art/Enemies/HecklerSoul | hecklerSoul |
| hidingSoul | Neutral | ok | generated: Art/Enemies/HidingSoul | hidingSoul |
| hookPickup | Neutral | ok | generated: Art/Enemies/HookPickup | hookPickup |
| imp | Enemy | ok | generated: Art/Enemies/Imp | imp |
| impCutter | Enemy | ok | generated: Art/Enemies/ImpCutter | impCutter |
| impFleeing | Neutral | ok | generated: Art/Enemies/ImpFleeing | impFleeing |
| impForeman | Enemy | ok | painted by its ability from a sheet (FloorKit art) | impForeman |
| impGuard | Enemy | ok | generated: Art/Enemies/ImpGuard | impGuard |
| impGuardCount1 | Enemy | ok | the same creature as impGuard (its art, untinted) |  |
| impGuardCount2 | Enemy | ok | the same creature as impGuard (its art, untinted) |  |
| impGuardCount3 | Enemy | ok | the same creature as impGuard (its art, untinted) |  |
| impGuardCount4 | Enemy | ok | the same creature as impGuard (its art, untinted) |  |
| impHauler | Enemy | ok | painted by its ability from a sheet (FloorKit art) |  |
| impPistolBearer | Enemy | ok | generated: Art/Enemies/ImpPistolBearer | impPistolBearer |
| impSack | Enemy | ok | generated: Art/Enemies/ImpSack | impSack |
| impShotgunBearer | Enemy | ok | generated: Art/Enemies/ImpShotgunBearer | impShotgunBearer |
| impSleeper | Neutral | ok | generated: Art/Enemies/ImpSleeper | impSleeper |
| impSleeperFall | Neutral | ok | the same creature as imp (its art, untinted) |  |
| impSpitfireBearer | Enemy | ok | generated: Art/Enemies/ImpSpitfireBearer | impSpitfireBearer |
| impSwing | Enemy | ok | painted by its ability from a sheet (FloorKit art) |  |
| impSwingB | Enemy | ok | painted by its ability from a sheet (FloorKit art) |  |
| ironhideBulwark | Enemy | ok | generated: Art/Enemies/IronhideBulwark | ironhideBulwark |
| kilnMatron | Enemy | ok | generated: Art/Enemies/KilnMatron | kilnMatron |
| kmHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| kmWarn | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFC0F0 |  |
| ladder | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| ladderHaul | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lamplighter | Enemy | ok | generated: Art/Enemies/Lamplighter | lamplighter |
| lanternfly | Enemy | ok | generated: Art/Enemies/Lanternfly | lanternfly |
| launcherThief | Neutral | ok | generated: Art/Enemies/LauncherThief | launcherThief |
| launcherToss | Neutral | ok | generated: Art/Enemies/LauncherToss | launcherToss |
| lavaBurst | Neutral | STAND-IN | its own animator plays Beam tinted #FF7A2A |  |
| lavaFall | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaFall955a | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaFall955b | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaFall955c | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaFallScenery | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaFlood | Enemy | STAND-IN | wears fireball's art tinted #FF5A1A |  |
| lavaGeyser | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaGeyserB | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaGeyserC | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaRiver | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaSea | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaSeaHall | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaSurge | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| lavaWave | Enemy | STAND-IN | wears fireball's art tinted #FF8A2A | lavaWave |
| leadImp | Enemy | ok | generated: Art/Enemies/LeadImp | leadImp |
| ledge | Neutral | ok | generated: Art/Climb |  |
| lensImp | Enemy | ok | generated: Art/Enemies/LensImp | lensImp |
| llFlameBolt | Enemy | ok | runeBolt's effect sheet (made to be tinted), in #FFD27A |  |
| llMarkBolt | Enemy | ok | runeBolt's effect sheet (made to be tinted), in #FFE9A8 |  |
| llRuneColumn | Enemy | ok | generated: Art/Effects/RuneColumn |  |
| llWarnRune | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFE9A8 |  |
| lobberGlow | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFB04A |  |
| loopCarving | Neutral | ok | the same creature as ledge (its art, untinted) |  |
| loopCarvingMeToo | Neutral | ok | the same creature as ledge (its art, untinted) |  |
| loopGeyser | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| loopSlab1 | Neutral | ok | the same creature as settleSlab (its art, untinted) |  |
| loopSlab2 | Neutral | ok | the same creature as settleSlab (its art, untinted) |  |
| loopSlab3 | Neutral | ok | the same creature as settleSlab (its art, untinted) |  |
| loopSlab4 | Neutral | ok | the same creature as settleSlab (its art, untinted) |  |
| loopStone1 | Neutral | ok | the same creature as sinkingStone (its art, untinted) |  |
| loopStone2 | Neutral | ok | the same creature as sinkingStone (its art, untinted) |  |
| loopStone3 | Neutral | ok | the same creature as sinkingStone (its art, untinted) |  |
| loopStone4 | Neutral | ok | the same creature as sinkingStone (its art, untinted) |  |
| loopStone5 | Neutral | ok | the same creature as sinkingStone (its art, untinted) |  |
| loopWraith | Enemy | ok | generated: Art/Enemies/LoopWraith | loopWraith |
| lostBell | Neutral | ok | generated: Art/Enemies/LostBell | lostBell |
| lostMitt | Neutral | ok | generated: Art/Enemies/LostMitt | lostMitt |
| lostRollingPin | Neutral | ok | generated: Art/Enemies/LostRollingPin | lostRollingPin |
| magmaBoulder | Enemy | STAND-IN | wears fireball's art tinted #D2551E | magmaBoulder |
| magmaGeode | Enemy | ok | generated: Art/Enemies/MagmaGeode | magmaGeode |
| magmaKing | Enemy | ok | generated: Art/Enemies/MagmaKing | magmaKing |
| mfHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| mfWarn | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FF9A6A |  |
| mineBeetle | Enemy | ok | generated: Art/Enemies/MineBeetle | mineBeetle |
| mkWarnFlood | Neutral | STAND-IN | wears overlordBeamGlow's art tinted #FF3A1A |  |
| mkWarnFloor | Neutral | STAND-IN | wears overlordBeamGlow's art tinted #FFB060 |  |
| mkWarnHand | Neutral | ok | generated: Art/Effects/Warn |  |
| mkWarnMouth | Neutral | ok | generated: Art/Effects/Warn |  |
| motherFissure | Enemy | ok | generated: Art/Enemies/MotherFissure | motherFissure |
| moverSlab | Neutral | ok | generated: Art/Climb |  |
| overlordBaitDiamond | Neutral | ok | painted: Images |  |
| overlordBeam | Neutral | ok | generated: Art/Effects/Beam |  |
| overlordBeamGlow | Neutral | ok | generated: Art/Effects/Beam |  |
| overlordBeamImpact | Neutral | ok | generated: Art/Effects/Beam |  |
| overlordFar806 | Neutral | STAND-IN | its own animator plays MagmaKing tinted #3B2F46 |  |
| overlordFar857 | Neutral | STAND-IN | its own animator plays MagmaKing tinted #3B2F46 |  |
| overlordFar896 | Neutral | STAND-IN | its own animator plays MagmaKing tinted #3B2F46 |  |
| overlordFar906 | Neutral | STAND-IN | its own animator plays MagmaKing tinted #3B2F46 |  |
| overlordFar953 | Neutral | STAND-IN | its own animator plays MagmaKing tinted #3B2F46 |  |
| overlordFar958 | Neutral | STAND-IN | its own animator plays MagmaKing tinted #3B2F46 |  |
| overlordFar994 | Neutral | STAND-IN | its own animator plays MagmaKing tinted #3B2F46 |  |
| overlordFar997 | Neutral | ok | generated: Art/Enemies/OverlordFar | overlordFar |
| overlordLift958 | Neutral | ok | generated: Art/Climb |  |
| phaseRaider | Enemy | ok | generated: Art/Enemies/PhaseRaider | phaseRaider |
| pileKeeper | Enemy | ok | generated: Art/Enemies/PileKeeper | pileKeeper |
| pkSackBurst | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| pkSpear | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| pkWarnSack | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFE08A |  |
| pkWarnSpear | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFB060 |  |
| plateDemon | Enemy | ok | generated: Art/Enemies/PlateDemon | plateDemon |
| plateShard | Enemy | STAND-IN | wears projectileEnemy's art tinted #D8D0F0 | plateShard |
| pointer | Neutral | ok | generated: Art/Enemies/Pointer | pointer |
| portal | Neutral | ok | generated: Art/Effects/Portal |  |
| portalScorch | Neutral | ok | generated: Art/Effects/Portal |  |
| projectileEnemy | Enemy | ok | painted: Images |  |
| projectileLaser | Enemy | ok | painted: Images |  |
| pyreCaller | Enemy | ok | generated: Art/Enemies/PyreCaller | pyreCaller |
| quickfireImp | Enemy | STAND-IN | wears imp's art tinted #FF9A5A | quickfireImp |
| rbHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| rbSpotHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| rbWarnLine | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFB060 |  |
| rbWarnSpot | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFE08A |  |
| rewindOrb | Neutral | ok | generated: Art/Effects/RewindOrb |  |
| ricochetEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #FFC15A |  |
| ricochetImp | Enemy | STAND-IN | wears imp's art tinted #FFC890 | ricochetImp |
| ricochetPistolPickup | Neutral | ok | generated: Art/Enemies/RicochetPistolPickup | ricochetPistolPickup |
| ricochetShot | Player | STAND-IN | its own animator plays Images tinted #7FE8FF | ricochetShot |
| riftBomb | Enemy | ok | generated: Art/Enemies/RiftBomb | riftBomb |
| riftBomber | Enemy | ok | generated: Art/Enemies/RiftBomber | riftBomber |
| riftMark | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FF5AA0 |  |
| risenDemon | Enemy | ok | generated: Art/Enemies/RisenDemon | risenDemon |
| risingSlab | Neutral | ok | generated: Art/Climb |  |
| risingSlabTall | Neutral | ok | the same creature as risingSlab (its art, untinted) |  |
| roadBreaker | Enemy | ok | generated: Art/Enemies/RoadBreaker | roadBreaker |
| roadCreep | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| roadCreepRight | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| roadPerch | Neutral | ok | the same creature as roadSlab (its art, untinted) |  |
| roadSlab | Neutral | ok | generated: Art/Climb |  |
| rocketLauncherPickup | Neutral | ok | generated: Art/Enemies/RocketLauncherPickup | rocketLauncherPickup |
| rook | Neutral | STAND-IN | wears damnedSoul's art tinted #F2A93B | rook |
| runeBolt | Enemy | ok | generated: Art/Effects/Rune |  |
| runeHound | Enemy | STAND-IN | wears slagHound's art tinted #A9BFDA | runeHound |
| runePillar | Neutral | ok | generated: Art/Climb |  |
| runeSpan | Neutral | ok | generated: Art/Climb |  |
| runeSpanGaze | Neutral | ok | the same creature as ledge (its art, untinted) |  |
| runeSpanHold | Neutral | ok | the same creature as ledge (its art, untinted) |  |
| runeSpanLate | Neutral | ok | the same creature as ledge (its art, untinted) |  |
| runeSpanQuick | Neutral | ok | the same creature as ledge (its art, untinted) |  |
| runeSpanUnison | Neutral | ok | the same creature as ledge (its art, untinted) |  |
| seamEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #6FE0C8 |  |
| seamImp | Enemy | STAND-IN | wears imp's art tinted #6FE0C8 | seamImp |
| seamMark | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #6FE0C8 |  |
| seesawSlab | Neutral | ok | generated: Art/Climb |  |
| seesawSlabB | Neutral | ok | the same creature as seesawSlab (its art, untinted) |  |
| settleSlab | Neutral | ok | generated: Art/Climb |  |
| sfNeedle | Enemy | STAND-IN | its own animator plays Climb tinted #D8C8D0 | sfNeedle |
| sfSpear | Enemy | STAND-IN | wears fireball's art tinted #5A4660 | sfSpear |
| sfWarnField | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFF0C0 |  |
| sfWarnHand | Neutral | ok | mkWarnHand's effect sheet (made to be tinted), in #FFB070 |  |
| sfWarnRow | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FF9A4A |  |
| sfWarnSpot | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #FFD08A |  |
| sfWhelp | Enemy | ok | the same creature as demonWhelp (its art, untinted) |  |
| shard | Enemy | STAND-IN | wears slag's art tinted #5A4A66 | shard |
| shardBig | Enemy | STAND-IN | wears slag's art tinted #5A4A66 |  |
| shardwright | Enemy | STAND-IN | wears brimstoneDemon's art tinted #5A5870 | shardwright |
| shooter | Enemy | ok | painted: Images |  |
| shotgunPickup | Neutral | ok | generated: Art/Enemies/ShotgunPickup | shotgunPickup |
| sinkingStone | Neutral | ok | generated: Art/Climb |  |
| slabBolt | Enemy | ok | generated: Art/Effects/Bolt |  |
| slabSniper | Enemy | STAND-IN | wears imp's art tinted #B0B8C0 | slabSniper |
| slag | Enemy | ok | generated: Art/Enemies/Slag | slag |
| slagConveyor | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| slagConveyorDown | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| slagDrip | Neutral | ok | generated: Art/Effects/Warn |  |
| slagHound | Enemy | ok | generated: Art/Enemies/SlagHound | slagHound |
| slagLadlePickup | Neutral | ok | generated: Art/Enemies/SlagLadlePickup | slagLadlePickup |
| slagTortoise | Enemy | STAND-IN | wears brimstoneDemon's art tinted #8A7468 | slagTortoise |
| slamShockwave | Enemy | STAND-IN | wears fireball's art tinted #FFB050 |  |
| sleepwalker | Enemy | STAND-IN | wears chaser's art tinted #B9C9E8 | sleepwalker |
| sparedWhelp | Neutral | STAND-IN | its own animator plays Images tinted #FF9A7A | sparedWhelp |
| spinner | Enemy | STAND-IN | wears sentinel's art tinted #7FD8FF | spinner |
| spireFiend | Enemy | PLACEHOLDER | drawn as a labeled circle | spireFiend |
| spireShard | Neutral | ok | slagDrip's effect sheet (made to be tinted), in #FFB070 |  |
| spireShardBig | Neutral | ok | slagDrip's effect sheet (made to be tinted), in #FFB070 |  |
| spitfirePickup | Neutral | ok | generated: Art/Enemies/SpitfirePickup | spitfirePickup |
| splitSlab | Neutral | ok | generated: Art/Climb |  |
| sprayer | Enemy | STAND-IN | wears shooter's art tinted #D69CFF | sprayer |
| swHit | Enemy | ok | the same creature as fmLash (its art, untinted) |  |
| swWarn | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #E0D8F0 |  |
| swingChain | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| swingChainB | Neutral | ok | painted by its ability from a sheet (FloorKit art) |  |
| targetDummy | Enemy | PLACEHOLDER | drawn as a labeled circle | targetDummy |
| tortoiseEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #C8A27A |  |
| tripleLobber | Enemy | STAND-IN | wears bounceLobber's art tinted #E8C898 | tripleLobber |
| twiceRisen | Enemy | STAND-IN | wears risenDemon's art tinted #9A7A8A | twiceRisen |
| twinBoltDeadeye | Enemy | ok | the same creature as deadeyeImp (its art, untinted) | twinBoltDeadeye |
| twinLensImp | Enemy | STAND-IN | wears lensImp's art tinted #FF3A3A | twinLensImp |
| veilDemon | Enemy | STAND-IN | wears brimstoneDemon's art tinted #7A6A98 | veilDemon |
| veilEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #B08AFF |  |
| veilMark | Neutral | ok | fmWarnWhip's effect sheet (made to be tinted), in #B08AFF |  |
| veilMatron | Enemy | STAND-IN | wears brimstoneDemon's art tinted #A890E0 | veilMatron |
| wallSlab | Neutral | ok | generated: Art/Effects/KitWall |  |
| watcher | Enemy | STAND-IN | its own animator plays Images tinted #CFF4FF | watcher |
| watcherPatrol | Enemy | STAND-IN | its own animator plays Images tinted #CFF4FF |  |
| waystoneGlow | Neutral | STAND-IN | wears overlordBeamGlow's art tinted #9EFFE6 |  |
| whelpDiamond | Enemy | ok | the same creature as demonWhelp (its art, untinted) |  |
| whelpNest | Enemy | PLACEHOLDER | drawn as a coloured box | whelpNest |
| whirlEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #FF7AC8 |  |
| whirlImp | Enemy | STAND-IN | wears imp's art tinted #FF7AC8 | whirlImp |
| wick | Neutral | STAND-IN | wears damnedSoul's art tinted #BFE6C8 | wick |
| windGust | Neutral | ok | generated: Art/Effects/Wind |  |
| wraithEmber | Enemy | STAND-IN | wears projectileEnemy's art tinted #9AD8E8 |  |
| wreckCart | Neutral | ok | the same creature as debrisRaft (its art, untinted) |  |
| wreckHay | Neutral | ok | the same creature as debrisRaftHay (its art, untinted) |  |
| wreckRoof | Neutral | ok | the same creature as debrisRaftRoof (its art, untinted) |  |
| wreckSign | Neutral | ok | the same creature as debrisRaftSign (its art, untinted) |  |
| yoobaProp | Neutral | ok | Yooba himself (the village slime) on Level 1: his slime sheet in his own green |  |

## Not counted: bases other entities extend, and entities nothing spawns (retired)

| Entity | Status | How it is drawn |
|---|---|---|
| chaser | ok (base only) | painted: Art/Sprites/Enemies |
| counterweightLeft | ok (base only) | generated: Art/Climb |
| counterweightRight | ok (base only) | generated: Art/Climb |
| driftSlab | ok (unused) | the same creature as seesawSlab (its art, untinted) |
| driftSlabLeft | ok (unused) | the same creature as seesawSlab (its art, untinted) |
| exploder | retired | wears chaser's art tinted #FF9966 |
| fighter | ok (base only) | painted: Images |
| sentinel | ok (base only) | painted: Images |
| slimeGlob | retired | its own animator plays Enemies tinted #9CFF7AE6 |
| slimeSplat | retired | its own animator plays Enemies tinted #7BD65A99 |
| sludge | retired | drawn as a labeled circle |
| swarmer | retired | wears chaser's art tinted #B8FF8A |
| windSteady | ok (unused) | generated: Art/Effects/Wind |
