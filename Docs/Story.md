# Sick Of It: Story Bible

The story is the design document. Every level, map, enemy, upgrade and line of dialogue should be traceable to
something on this page. If a request needs something the story doesn't explain yet, add it here first, then build
it. Agents: read this before adding or changing a level, an enemy, an upgrade or a voice line.

Status: **draft for review** (2026-10-15; the Climbers, a rival and a guide per Circle, 2026-10-25; the Overlord on the far ledge, eight glimpses on Circles X and IX, 2026-10-25; elites and mid-bosses on the x5 floors, 2026-10-25; the hero's moves, a dash and a wall-jump, 2026-10-25; the six bosses' second look: more phases, minions, longer fights, 2026-10-25; secret floors and gauntlets behind doors on the Heat Stairs, 2026-10-25; Circle IX's authored floors 900-892, 860-852 and 810-802 made hotter long bullet-hell floors of ~5 minutes, 2026-10-25; Circle X's floors 997-992 and 960-952 made long bullet-hell floors of ~5 minutes, same ideas and lines, 2026-10-25; weapons found on floors and kept for good, the launcher's theft on Floor 998, 2026-10-25; Circle X's played route built to Floor 901, 2026-10-19; Circle IX's played route built to the Spire Fiend's door at 802, 2026-10-19; prologue, the Shaft and the floor layout revised 2026-10-18; Waystones and runs 2026-10-18 (Return for 10 diamonds on the death screen, 2026-10-27); the Magma King's three-phase fight, 2026-10-18 (now Floor 901, level 31); Shortcuts and Circle IX's first stretch, Levels 12-18, 2026-10-18; the slime named Yooba, 2026-10-18; **one level = one floor**, 2026-10-18, see [LevelDesign.md](LevelDesign.md); On the website, 2026-10-18; Teasers, 2026-10-18; Circles VIII to IV designed, 2026-10-25; Circles III to I, the final boss and the endings designed, 2026-10-25). Names marked *(working)* are placeholders.

## The pitch

A tired, sarcastic nobody watches a giant Overlord laser his village flat on a nice afternoon. The Overlord rules
Hell, all 1,000 floors of it, and Hell is dark, loud and the same forever. He wants the surface. He burns the
meadow empty, has his imps steal the village's diamonds (they're what holds a portal open), and steps back
through the portal to gather his army.

The hero doesn't follow him on purpose. He's just curious about the wrong hole: the Overlord's lasers come down
one last time and burn a portal into the ground right next to him, and it swallows him (see *Prologue* below). The
portal spits him out at the very bottom of Hell, **Floor 1,000**. The Overlord
sits at the top, Floor 1, right under the meadow, feeding the diamonds into a gate that will tear Hell open onto
the surface for good. The whole game is the climb back up: **1,000 floors, counting down**, to stop him before the
gate opens.

The joke that holds it together: **Hell is repetition.** Everyone down there does the same thing forever. The
hero is already sick of everything, so Hell can't break him. He learns to grab Hell's loop and drag it backward
(rewind), and every time Hell kills him and sends him back, he comes back more annoyed (the roguelite loop).

Tone: deadpan hero, booming 90s arcade announcer, real stakes played for laughs. The humor comes from the hero's
attitude and from Hell itself (its endless repetition, its showmanship, its monsters), **never from office or work
jokes** (no Mondays, inboxes, meetings, coffee, bosses-as-managers). Dark, but never edgy for its own sake.

## Cast

| Who | What | Voice / look | Footage |
|---|---|---|---|
| **The Hero** | No name, on purpose (J, 2026-10-18: it goes with the absurdity); everyone, the announcer included, calls him "the hero". Thirties, unimpressed, has had enough. Talks to himself in one-liners. Never gives speeches. | `audio.json` voice `player` | Player sprites: an armoured knight, dark battered plate with orange seams, a crested helm with glowing eye slits, the glowing launcher on his shoulder (never a jacket) |
| **The Overlord** | Giant obsidian titan with red eyes. Ruler of Hell's 1,000 floors, as sick of Hell as the hero is of everything, and done waiting to take the surface. Never in a hurry; barely notices the hero until he can't ignore him. Says very little, and every word sounds like it cost him nothing. | `audio.json` voice `overlord`: deep, slow, enormous, barely bothered; a voice that fills a valley without being raised. Short lines, long pauses. When he lasers the portal open he barely notices the hero: *"Mm. ... Stay."* or a bored exhale. | Video1, Level1 backgrounds |
| **Yooba** | The village's green slime and its gossip, and J's own character (the same Yooba as Yooba Land; his name is final, not a working one). Swallowed a handful of the village's diamonds to hide them from the imps, got sucked through the portal with the hero, and now trades and tinkers between floors out of his own stomach. **Slimes are villagers, never enemies.** | `audio.json` voice `yooba`: friendly, chatty, wet; talks fast, gurgles between words, never stops for breath, delighted to see anyone, a bit too pleased with himself. | Video3 (the talking slime); his talking portrait (`ui/yooba.json`, see *Yooba between floors*) |
| **The Announcer** | Hell's own voice: loves a show, calls out floors, bosses and deaths like an arcade cabinet. | `announcer` | Floor titles, death screens |
| **The Climbers** | **Rook** (the rival, always second), **Wick** (Circle X's lamplighter guide) and **Pointer** (Circle IX's fingerpost guide): damned souls met again and again on the floors, with scenes that branch on the hero. See *The Climbers*. | `rook`, `wick`, `pointer` (subtitles until cast) | `art/rook.json`, `wick.json`, `pointer.json` |
| **The Wardens** | One boss per Circle: the monster that rules it. Each guards its Circle's top floor. | Per Warden | Death videos, bosses |
| **The Magma King** | Circle X's Warden: a huge king of slag and obsidian with a lava crown, slouched on a throne of slag at Floor 901. He never gets up, unless you make him: the Wastes come to him. Smug where the Overlord is bored; proud, loves an audience and takes Hell's show seriously (a challenger is the best entertainment the Wastes have had in ages). He hears everything that climbs his stairs and enjoys the wait. Making him stand is what enrages him (see *The Magma King's fight*). | `audio.json` voice `magmaKing`: very deep, thick, gurgling bass; slow, smug, amused, lazy, proud; chuckles (*"Mm-hm-hm."*), never shouts. The one line with weight is when he stands. Heard before he's seen, from ahead on the Heat Stairs. | `art/magmaKing.json`: generated sheets in `Assets/Art/Enemies/MagmaKing/` |
| **The Spire Fiend** | Circle IX's Warden: a gaunt basalt demon rooted in the heart of the great spire at Floor 801, his lower body a spire instead of legs, rock spires bristling from his head, shoulders and back, thin orange seams glowing all over him. He **grows the spires**: every spire on the Cracked Road is one he pushed up through the road (that is why the road keeps splitting), and the great spire is the one he has grown for ages so that nothing in the Circle stands higher than him. Hell's loop is his too: every night the spire sheds back to the same height, every day he grows it again. He can't bear anything above him, and a hero climbing *up* is the worst thing he can think of. Not a showman like the Magma King: he **listens** (the hum on the Needle Stair is him, listening through the rock), counts steps, and whispers. Making him lose his calm is what enrages him (see *The Spire Fiend's fight*). | `audio.json` voice `spireFiend`: a dry, hollow, rasping whisper with stone grinding under it; slow, patient, cold, precise; hums (*"Hmmmm."*) instead of laughing. The one line he raises is the enrage. Never heard on the route, only his spire's hum. | `art/spireFiend.json`: generated sheets in `Assets/Art/Enemies/SpireFiend/` |

## First five minutes

A brand-new player who presses **New Game** (after one "Tap to play" and nothing else on that card) lands on **Floor 1000** itself (the Meadow stays in Pick a Level); the first staged rewind ("Hold R.") comes 8 s in. Nobody is left watching for more than about
10 s before it (2026-11-11). New Game starts at **Normal** with no Pick Your Hell card (that card lives in
Settings). After three deaths inside five real minutes the death screen offers **Try Assist** once. Level 1's portal
scene stays under 10 s of locked input (That's a hole / Hello? echo / Nope / the grab); a tap or key skips the rest.
On a first run Video2 (the fire tunnel) is skipped and stays in Rewatch Stuff; the Shaft plunge is at most 8 s and
skippable from its first frame. The first Grudge pick is after Floor 1000, not after the Meadow. Playtest `first-five`.

## Prologue: how the hero gets into Hell

Level 1 is the Meadow, and it ends in a scripted scene, not a cut. In order:

1. **Video1.** A nice afternoon. The Overlord rises over the hills; his imps drop in to loot the village. Since 2026-11-08 New
   Game skips it and opens Level 1, whose first seconds tell the same beat in play (the sky opens, the imps drop in);
   Video1 is watchable from Videos.
2. **Level 1, the Meadow.** The hero clears the imps and the cinders. Diamonds, a first taste of rewind.
3. **The portal.** The Overlord's lasers come down one last time, not at the hero (he isn't worth it yet) but at
   the meadow near him. They burn a glowing hole into the meadow: a portal, lying flat on the ground. The first Grudge
   pick waits until after Floor 1000 (see *First five minutes*).
   It is always the same spot: where the Overlord's laser hits the meadow in the background footage (Level 1's
   laser clip, a little left of centre on the ground line), and the small fire left burning there in the aftermath
   loop is this portal seen from afar. The game's beam, impact, portal and scorch sit right on top of it, wherever
   the hero happens to be standing; he walks over to it from his side, and Yooba comes from the other.
   How it looks: first a red-orange glow gathers on the grass (the beam charging), then the beam drops out of the
   sky as one burning column and bursts on the ground. The grass scorches, cracks, and tears open into a ring of
   burning turf with fire swirling down inside it: **the same fire portal the hero falls through in Video2**,
   seen side-on from the meadow, so the player recognises it when Video2 starts. It swirls the whole time he
   stands at its edge, and once it has had him (and Yooba) it flares and shrinks to a black scorch mark.
4. **The walk-over.** The hero walks over to it on his own, curious (scripted: the player's input is off), and
   leans in to peer down at it, head tilted (his `Curious` clip).
   Hero: *"That's a hole."*
5. **The echo.** He cups a hand and shouts down *"Hello?"* (`Shout`) Hell answers: *"...hello? ...hello? ...hello?"*, each one
   deeper than the last. It's the first hint that **Hell is repetition**: even its echo won't stop.
   The echo is his own voice coming back, slower, darker and more broken each time: the loop answering him
   (`vo_portal_echo`).
6. **"Nope."** The portal grabs him: yanked off his feet backward, flailing (`Grabbed`), he
   spins and shrinks into it, and he's gone. A tap or key skips the rest of the scene once the portal is open.
7. **Yooba.** A beat later a green village slime bounces past, wobbles at the edge, and is slurped in after him.
   Nobody remarks on it. It pays off in Video3, when the same slime turns up at the bottom of Hell, talking.
8. **Video2** (Rewatch Stuff; skipped on a first run): the burning door; the hero tumbling and spinning down the fire
   tunnel; the lava river at Floor 1,000.
9. **The Shaft** (below): the camera plunges from the Meadow to the bottom of Hell (at most 8 s, skippable from the
   first frame), and the climb begins.

The hero never chose to go. That matters to his voice: he isn't a hero on a quest, he's a man who fell down a
hole and is now walking home the long way. He wants to stop the Overlord mostly because the Overlord is in the way.

## Structure: 1,000 floors

```
THE MEADOW (Floor 0, the surface)          Prologue: the Overlord burns the village
        │  sucked through the portal       Video2: the fire tunnel
        ▼
FLOOR 1,000  ── bottom of Hell ──          "WELCOME TO THE BOTTOM. NINE HUNDRED NINETY-NINE TO GO."
   Circle X ... Circle I                   climb: floors count DOWN toward 1
FLOOR 1  ── the top: the Overlord's door   Final boss
        │
        ▼
THE MEADOW again                           Ending
```

- Hell is **10 Circles of 100 floors**. Each Circle has one biome, one family of enemies and one Warden at its
  top floor (x01: Floor 901, 801, ... 101). Floor 1 is the Overlord.
- **One level is one floor** (J, 2026-10-18; it replaces "a level is 10 floors"). Each floor of Hell is its own
  level: 1 to 2 minutes, always **climbing upward** (from a ledge just above the lava sea, up through the floor's
  obstacles, to a landing with the floor number carved in it and a door to the next floor), with its own obstacles,
  its own monsters and its own set piece. How a floor is built, the toolkit every floor draws from and every played
  floor of Circle X are in **[LevelDesign.md](LevelDesign.md)**; build floors from there.
- **A floor boss every 10th floor** (the floors ending in 1: 991, 981, ... 911), on a climbing arena, one of its
  Circle's family. **The Warden every 100 floors**, on its Circle's top floor (901, 801, ... 101); Floor 1 is the
  Overlord.
- **Hell's shortcuts skip stretches** of floors (see *Shortcuts*): a lift, a chimney, a fallen stair. A first climb
  plays about **30 floors per Circle** (three stretches of ten, each ending on its floor boss or the Warden, joined by
  two shortcuts); turning a shortcut down plays the floors it skips (the long way). On the Shaft the skipped floors are
  the dotted jump.
- **Today's build** (2026-10-19): Circle X's whole played route is built one level per floor from LevelDesign.md:
  Floors 1000-991 (ending on the first floor boss, the Imp Foreman), **the Sack Lift** (991 to 960), Floors 960-951
  (ending on the Pile Keeper), **the King's Chimney** (951 to 910), Floors 910-902 (the Heat Stairs, up to the Magma
  King's door) and **Floor 901**, his arena. The long way (the floors the two shortcuts skip) isn't built as floors yet:
  until it is, turning a shortcut down plays the old 10-floor stages over those floors as its stand-in, off the story
  path. **Circle IX's played route** is built the same way (2026-10-19, LevelDesign.md section 6): Floors 900-891
  (ending on the Road Breaker), **the Spared Demon** (891 to 860), Floors 860-851 (ending on the Carver), **the Hollow
  Spire** (851 to 810) and Floors 810-802, the Needle Stair up to the door of the Spire Fiend's arena on Floor 801 (his
  fight is its own task, k_e16304d7a504). Its long way is played by Circle IX's first-pass 10-floor stages as stand-ins.
- **Level numbers:** Level *n* is position *n* in game.json's `levels` list. Level 1 is the Meadow (Floor 0). Levels
  2-11 are **Floor 1000** *The Landing*, **999** *Only 999 To Go*, **998** *The Spill*, **997** *Imp Run*, **996** *The
  Slag Falls*, **995** *The Loop*, **994** *The Chain Lift*, **993** *Ember Ledges*, **992** *Bad Footing*, **991** *The
  Foreman's Pile* (the Imp Foreman). Levels 12-21 are **Floor 960** *The Depot*, **959** *The Rest of the Village*, **958**
  *The Burning Thatch*, **957** *Hot Air*, **956** *The Drip*, **955** *The Falls*, **954** *The Guard Post*, **953**
  *Second Lap*, **952** *The Sack Pile*, **951** *The Keeper's Mound* (the Pile Keeper). Levels 22-31 are **Floor 910**
  *The Heat Stairs*, **909** *Ember Rain*, **908** *The Cart Run*, **907** *I Can Hear You*, **906** *The Loop, Upstairs*,
  **905** *The Collapse*, **904** *The Count*, **903** *The Last Updraft*, **902** *The Door* and **901** *The Slag Throne*
  (the Magma King). Levels 32-41 are **Floor 900** *The Cracked Road*, **899** *Split Ends*, **898** *Shard Fall*,
  **897** *The Whelps*, **896** *Brimstone Vents*, **895** *The Heave*, **894** *The Lobbers*, **893** *Seesaw Road*,
  **892** *Demon Row*, **891** *The Breaker's Road* (the Road Breaker). Levels 42-51 are **Floor 860** *The Carver's
  Yard*, **859** *The Hoist*, **858** *The Leaning Spire*, **857** *Seam Falls*, **856** *The Nest*, **855** *Sign
  Language*, **854** *The Quarry*, **853** *Dust*, **852** *The Gallery*, **851** *The Carver's Spire* (the Carver).
  Levels 52-60 are **Floor 810** *The Needle's Foot*, **809** *Updraft Round the Spire*, **808** *The Creeping Stair*,
  **807** *The Choir*, **806** *The Overhang*, **805** *The Breakaway*, **804** *The Turn*, **803** *The Last Heave* and
  **802** *The Spire Door*. A floor's file is `levels/Floor<floor>.json` (Floor 901's was `Level11.json`). Levels 61-66
  are Circle IX's first-pass stages `Level13`-`Level18` (Floors 890-831; the old `CrackedRoad` over 900-891 is gone,
  replaced by Floors 900-891), kept as the long way's stand-ins: Floor 891's way on when the Spared Demon is turned down
  (`Level13`-`Level15`, Floors 890-861, then Floor 860; `Level14`, *The Old Stair*, still offers its stairwell to 841,
  then `Level18`) and Floor 851's when the Hollow Spire is (`Level17`-`Level18`, Floors 850-831, then Floor 810;
  `Level16`, *Demon Row* over 860-851, is played only from the level select). Levels 67-74 are the old
  10-floor stages, files `Level3`-`Level10` (*Floors 990-981: The Spill* to *Floors 920-911: The Lower Stairs*), kept as
  the long way's stand-ins: Floor 991's way on when the Sack Lift is turned down (`Level3`-`Level5`, Floors 990-961, then
  Floor 960) and Floor 951's when the Chimney is (`Level7`-`Level10`, Floors 950-911, then Floor 910; `Level6`, the old
  Loop over 960-951, is played only from the level select). Their places' names now belong to the floors where those
  ideas are introduced.
- **The path today** (re-pathed 2026-10-19; New Game goes straight to Level 1 since 2026-11-08, Video1 stays in Videos;
  first run skips Video2 since 2026-11-11, see *First five minutes*): New Game → Level 1, the Meadow → the Shaft plunge →
  **Floor 1000, The Landing** (first Grudge) → Video3 (Yooba) → **Floors 999-991** → *the Sack Lift* → **Floors 960-951** → *the King's Chimney* →
  **Floors 910-902** → **Floor 901, the Slag Throne** (his Waystone) → the Shaft (Circle IX wakes up) → **Floors
  900-891** → *the Spared Demon* → **Floors 860-851** → *the Hollow Spire* → **Floors 810-802** → the level select,
  until Floor 801 (the Spire Fiend) is built. Inside a Circle the levels run straight on from one to the next (a
  level's `nextLevelNumber`, or a taken shortcut's `nextLevel`): results, the Shaft's climb, the next level, with no
  video between them. After them in the list, off the path (reachable from the level select only, once unlocked):
  Circle IX's first-pass stages `Level13`-`Level18` (*Floors 890-831*, see *Circle IX's long way*), Circle X's long
  way's stand-ins above, and after those the old arcade levels `ArcadeCrossfire`, `ArcadeSpinCycle`,
  `ArcadeEverything` and `ArcadeFireRain` (titled "Arcade: ...", outside the Circles, their slime enemies swapped for
  imps and cinders), kept to be recast: their enemies and patterns reused inside Circle stages. Their `levelNumber`
  still names their source footage (`Level2_Background1` is Crossfire's). Video4 (the blue bridge and the forest) is
  off the early path: it is watched from the video select and belongs to Circles VIII-VII.
- **A floor's counter:** a floor level's `floors` are `start` = its floor and `end` = the floor above (Floor 998:
  998 to 997). The HUD reads its floor through the climb and ticks to the next as he steps onto the landing (without
  replaying a milestone: the next floor's own title calls it), and the Shaft climbs the marker one floor, to `end`.
- The **floor number is always on screen** (HUD: `FLOOR 973`), and painted into the world as signs, carvings and
  doors, so the player feels how deep they are. Milestones get a beat: 999 ("only nine hundred ninety-nine to
  go"), 950 ("halfway through the Wastes"), 900, 666, 500, 100, 13, 2.
- A **floor** (one level file) is a climb, not a single screen: the player physically goes up through it, and the
  counter ticks to the next floor at its landing. Only Wardens' arenas stand still.
- Pacing target: a floor takes 60 to 120 seconds (floor bosses up to 3 minutes), so a Circle's played route (about 30
  floors) takes 45 to 60 minutes and the whole climb, shortcuts taken, about 8 to 10 hours. Hell's
  **shortcuts** (the imps' lifts, chimneys, collapsed stairwells, a demon you spared) skip stretches at a cost or a
  risk, which is a rogue choice and keeps 1,000 floors reachable.

### The Shaft: Hell's map

Hell is a hole, so its map is a **vertical shaft**: a tall cross-section with the Meadow (FLOOR 0) at the top,
the 10 Circles stacked below it, each in its own biome colour, and FLOOR 1,000 at the bottom.

- **First time** (after Level 1 on a first run, or after Video2 from Rewatch): the camera plunges from the Meadow down
  the whole Shaft, past all 10 Circles, to FLOOR 1,000, where the hero's marker lands (at most 8 s; a tap skips from
  the first frame). Announcer: *"WELCOME TO THE BOTTOM. NINE HUNDRED NINETY-NINE TO GO."*
  Hero: *"...Great."*
- **After every level:** the Shaft comes back and the hero's marker climbs to the floor he reached: one floor per
  floor (Floor 1000 done, the marker rises from 1,000 to 999); the old 10-floor stages still climb ten. It is how the player feels the climb: a tiny dot, a very
  tall hole.
- **Wardens** sit at the top of each Circle as dark silhouettes marked **???** until the hero has met them; then
  their name and portrait fill in.
- **The Wardens' shapes** *(working looks, for the silhouettes; each gets its full design when its Circle is built)*:
  the Magma King (`art/magmaKing.json`: slag and obsidian, lava crown, slouched); the Spire Fiend (gaunt basalt
  demon bristling with rock spires, glowing seams); the Bridge Wraith (floating hooded wraith, a lantern of blue
  runes, no legs); the Hollow King (a dead hollow tree with yellow eyes in its trunk and a crown of branches); the
  Drowned Queen (soaked hair over her face, waterlogged gown, a weed-wound crown, green ghost lights); the Mother Maw
  (a cave beast that is mostly mouth: teeth, drool, many small eyes); the Bell Ringer (a hooded gargoyle monk with a
  cracked iron bell on his back and candles melting on his shoulders); the Gatekeeper (an armoured giant built like a
  gate, portcullis visor, tower shield); the Chain King (a jailer king wrapped in chains, an iron spike crown, keys at
  his belt); the Overlord (canon: giant obsidian titan, red eyes, a crown of black horns).
- **Waystones** stand on the Shaft's wall at the bottom (the Landing, FLOOR 1,000) and at the top of every Circle
  (901, 801, ... 101): a small standing stone with *WAYSTONE* beside it and a line across the shaft, glowing once
  reached, dark stone above. When Hell ends a run the marker **drops** back down to one (see *Waystones and runs*).
- **Shortcuts are hinted, not explained:** cracks in the shaft wall and dotted gaps between floors that the player
  can't read yet. Some of them are Hell's shortcuts (see *Shortcuts*): once the hero has taken one, the Shaft shows
  it as a **dotted jump**: the marker leaves the shaft through the crack it hinted at and comes back in floors higher
  up, and the dotted line stays on the map from then on.
- The Shaft is a presentation screen: it reads progress, it never changes it.

### The Circles

Biomes follow the footage we already have, from the bottom up. Wardens, enemies and maps are proposals to
build toward; the first two Circles reuse current art.

| Circle | Floors | Biome | Footage to build from | Enemies (who and why) | Warden |
|---|---|---|---|---|---|
| X | 1000–901 | **The Ember Wastes:** lava rivers where the portal spits out everything that falls through | Video2 (lava river), Level5 fire rain | **Imps:** the Overlord's raiders, carrying the stolen diamonds down and throwing embers at anyone who follows. **Cinders:** sparks from the burning meadow that fell through with you and came alive | **The Magma King**, the Wastes' fire-throne (exists: `magmaKing`) |
| IX | 900–801 | **The Cracked Road:** black spires with glowing seams | Level2_Background1 (path) | **Brimstone Demons:** born from the cracks; they split the rock to stop you climbing (`Demon1` art, fire breath) | The Spire Fiend *(working)* |
| VIII | 800–701 | **The Rune Bridges:** glowing stone walkways over a void | Level2_Background1 end, Video4 (blue bridge) | **Watchers:** floating eyes that guard the bridges | The Bridge Wraith |
| VII | 700–601 | **The Gloomwood:** dead forest, torches, shadow blobs with yellow eyes | Level3_Background1, Video4 forest | **Shades:** souls stuck repeating the same walk; they only hurt you if you stand in their loop | The Hollow King |
| VI | 600–501 | **The Mire:** drowned graveyard, green ghost lights | Video2 (swamp frames) | **Wisps**, **Bog Hands** | The Drowned Queen |
| V | 500–401 | **The Caverns:** drool and teeth | Death2 (pale beast), Death3 (blue beast) | **Droolers** (`Fighter` art), **Lurkers** (`Level3Enemy` art). Both already have death videos | The Mother Maw |
| IV | 400–301 | **The Dead Peaks:** moonlit mountains, candle shrines | Video2 (peaks) | **Gargoyles**, **Candle Monks** | The Bell Ringer |
| III | 300–201 | **The Bastion:** fortress walls and gates | Video2 (castle gate) | **Garrison**, **Archers** | The Gatekeeper |
| II | 200–101 | **The Keep:** courtyard, dungeon, torches | Video2 (dungeon) | **Jailers**, **Chains** | The Chain King |
| I | 100–1 | **The Throne Stair:** the Overlord's palace and his half-built gate, daylight leaking through at the top | to make | Everything, at its strongest: the Overlord's personal guard | **The Overlord** (Floor 1) |

Circles VIII to IV are designed in full (looks, Wardens, floor bosses, enemies and weaknesses, signature mechanics, shortcuts, music) in *Circles VIII to IV: the middle descent*, below; the table above is the short form.
Circles III to I, the Overlord and the ending are designed the same way in *Circles III to I and the top of Hell: the final descent*, below. (Circle I has no Warden of its own: its Warden is the Overlord on Floor 1.)

### Acts and story beats

| Act | Where | What happens | Media |
|---|---|---|---|
| Prologue | The Meadow (Level 1) | A nice day. The Overlord rises over the hills; his imps drop in to loot the village; he lasers it; his imps take the diamonds. His lasers burn a portal into the ground near the hero (the fire hole in the footage), who goes to look, shouts down it, hears Hell echo back, says "Nope" and is sucked in. Yooba is slurped in after him. | Video1 → Level1 → portal scene → Video2 → the Shaft |
| Act 1 | Circles X–VIII | The portal drops the hero at Floor 1,000. Yooba turns up (the slime slurped in after him), coughs up a diamond, and starts helping. First Warden. The hero learns Hell repeats, and that he can rewind it. | Video2, Video3 (Yooba) |
| Act 2 | Circles VII–IV | Deeper strangeness: souls stuck in loops (Shades) show what Hell does to people. Hints the Overlord is just as stuck. Midpoint at Floor 500: the hero could take a shortcut home and doesn't. | new |
| Act 3 | Circles III–I | The Overlord's own guard. The gate is almost open; daylight leaks down the stairs. The hero climbs toward it. | new; designed in *Circles III to I and the top of Hell* |
| Finale | Floor 1 | The Overlord at his gate. Phase 1: he doesn't take the hero seriously. Phase 2: he rewinds too (the loop was his first). Phase 3: the gate cracks open on the meadow behind him. | new; designed in *Circles III to I and the top of Hell* |
| Ending | The Meadow | The hero shoves the Overlord back down and the gate shuts on him. Home, the meadow regrowing, Yooba spitting the diamonds back out. The hero sits down for the first time in 1,000 floors. Something knocks from under the ground. (Post-credit: "Floor 1,001".) | new; designed in *Circles III to I and the top of Hell* |

### Circle X: the Ember Wastes (one level per floor)

Circle X is built **one level per floor** (J's decision, 2026-10-18): the played route, every floor's page and the
floor bosses are in [LevelDesign.md](LevelDesign.md) (*The route through Circle X*, *The floors*), and *Level numbers*
above says which level is which floor. In short: Level 2 is **Floor 1000, The Landing** (`levels/Floor1000.json`),
Levels 3-11 are Floors 999-991 (the Imp Foreman on 991), the Sack Lift goes to Levels 12-21 (Floors 960-951, the Pile
Keeper on 951), the King's Chimney to Levels 22-30 (Floors 910-902, the Heat Stairs up to his door) and Level 31 is
**Floor 901, *The Slag Throne*** (`levels/Floor901.json`), the Magma King's arena (*The Magma King's fight*, below).

The old plan, ten stages of ten floors each, is gone; its beats moved onto single floors (the Landing → 1000, *ONLY 999
TO GO* → 999, the Spill → 998, Imp Run → 997, the Slag Falls → 996, the Loop → 995, the Sack Pile → 952, the Heat
Stairs → 910-902, the Slag Throne → 901). Eight of the old stages are still in game.json, off the story path, as **the
long way's stand-ins** until the generator builds those floors: what plays when a shortcut is turned down.

| Level | File | Floors | Stand-in for |
|---|---|---|---|
| 39-41 | `Level3`-`Level5` | 990-961 | the floors the Sack Lift skips: *The Spill*, *Imp Run*, *The Slag Falls*; then Floor 960 |
| 42 | `Level6` | 960-951 | nothing: the old *Loop*, played only from the level select |
| 43-46 | `Level7`-`Level10` | 950-911 | the floors the King's Chimney skips: *Halfway Basin* (the Floor 950 milestone), *The Crust*, *The Sack Pile*, *The Lower Stairs*; then Floor 910 |

#### The Magma King's fight

The arena is **Floor 901, *The Slag Throne*** (`levels/Floor901.json`, level 31; renumbered 2026-10-19 from the old
"Floors 910-901" hall, Level 11), through the door at the top of the Heat Stairs (Floor 902), and the fight below
stays as it is; the hall is one floor, its counter on 901 from the start (floors 901 to 901), standing over the lava
sea (the near sea glows under its floor, the lava river behind it). `ArcadeFireRain` keeps the older two-phase fight as
an off-path reference. The throne sits at the far end on top of a mound of cooling slag, the top of the Wastes. It is the one stage that stands still: a locked screen,
the throne at the right, the hero on the floor in front of it. The fight lives in the `magmaKing` entity (its states)
and the `bossStand`/`bossEnrage` sequences; the level runs the intro and the ending. He is big: seated on the throne
he stands about 2.4 units tall (over two heroes), and standing more.

Every attack is telegraphed a second or more ahead, on the spot it will hit: a glow in his hand before a boulder, a
glowing strip along the floor before a slam's shockwave, a glowing drip before slag falls, a glow at his mouth before
the fire, and the whole stretch of floor the flood will cover glowing red before it rolls.

1. **Intro** (input and rewind off, the hero can't be hurt). The title (*"FLOORS 910 TO 901! THE SLAG THRONE!"*),
   then the hall rumbles and the camera pans up to the throne as it **rises out of the slag** with him already on it,
   slouched and delighted (he doesn't get up for this: the throne brings him). The announcer calls it like an arcade
   cabinet: *"FLOOR 901! THE WARDEN OF THE WASTES!"* and *"THE... MAGMA... KING!"*. His taunt: *"Mm-hm-hm. An
   audience, at last. Welcome to the Slag Throne, little spark."* The camera comes back down; the hero: *"He's not
   getting up, is he."* The boss bar fills, control comes back: *"Dance for me, little spark. Hell loves a show."*
2. **Phase 1: from the throne** (full health to half). He doesn't get up for this. He scoops molten boulders from
   beside the throne and lobs them in an arc (`magmaBoulder`), and slams his fist on the throne's arm to send a low
   **shockwave** of slag along the floor (`slamShockwave`: jump it). The first real hit: *"Mm. That one tickled."*
3. **Half health: he stands** (`bossStand`). The only time he ever gets up: he pushes himself off the throne, rock
   grinding. With weight, not a shout: *"...You made me STAND."*
4. **Phase 2: standing, with his guard** (half to a third). He ground-pounds with both fists (a shockwave and slag
   dripping from the ceiling) and breathes a fan of fire, and he calls his imps down off the slag mound to play to
   the crowd: *"Guards. [chuckles] Make some noise for our guest."* (`impGuard`, his own guard from the Sack Pile and
   the stairs, at most two at a time, so with him never more than three on screen). The imps come **only in this
   phase**, and like every guard they carry nothing and drop nothing.
5. **A third: the enrage** (`bossEnrage`). He loses his temper: he roars, his cracks flare white-hot, the screen
   flashes, and what's left of his guard burns up in the heat. *"Now. Let's give them a real show."*
6. **Phase 3: the flood** (a third to the end). Faster slams and fire, slag from the ceiling, and his worst: both
   fists into the floor and a **flood of lava** (`lavaFlood`) rolls from the throne, a wall too tall to jump. The floor
   glows where it will reach; it stops short of the far wall, so there is a strip of safe floor at the hall's far
   end, but from where the hero usually stands it's too far to make in time. That is what Déjà Vu is for: caught,
   he drags the loop back and is already running the second time. The first flood, the hero: *"Can't jump that.
   ...Can do it again, though."* Near the end, the King: *"You're ruining... my show."*
7. **Defeat.** Slow motion; he cracks apart and collapses into slag at the foot of his throne, and the throne splits
   down the middle. His last words: *"The show... must go... on..."* Announcer: *"THE MAGMA KING HAS FALLEN!"* Hero:
   *"He got up for me. I'm touched."* Then the hero walks up the broken throne's slag, and at the top of it, on Floor
   901, a **Waystone lights** (a standing stone glowing awake): *"WAYSTONE LIT!
   FLOOR NINE HUNDRED ONE!"*, the hero: *"Great. Now Hell starts me here."* From now on a run starts at 901 (see
   *Waystones and runs*). The Grudge pick, the results, then the Shaft.
8. **The Shaft after him.** The marker stands on 901 (Floor 902 climbed it there; the floor below never lights the Waystone, only his defeat does), the Magma King's figure on the map fills in (beaten), the
   Waystone at 901 lights, and the band above, **Circle IX, the Cracked Road, wakes up**: its painting brightens out of
   the dark from the bottom up, its seams glowing, and the announcer calls it (*"CIRCLE NINE! THE CRACKED ROAD!"*).
   Next Warden: the Spire Fiend. Continue goes on to **Floor 900, the Cracked Road** (LevelDesign.md section 6).

Video3 (Yooba, talking) plays after **The Landing** (Floor 1000, and the Shaft's climb to 999): the slime from the meadow turns up at the bottom of Hell,
pleased to see a familiar face, and coughs up a diamond. From then on Yooba is the shop.

**Video3 is Hell's Ad Break (J, 2026-10-25, replacing the 2026-10-24 redo: the old footage "doesn't fit").** The talking-slime
footage is the old game's, not the Landing. The new Video3 is a ~30 s **cable-TV ad break for "1,000 FLOORS"**, played
after the Landing: Hell advertises itself like a late-night shop. (1) the **announcer** (Hell's own voice, booming) over a
logo card, HELL'S AD BREAK: *"Are you sick of it? GOOD! Because Hell has ONE THOUSAND FLOORS!"*; (2) fire, fiends, falling
rocks, the hero small in the picture: *"All of it, forever, now with MORE floors!"*; (3) a **sponsor spot**: **Yooba**, the
village's slime, hawks diamonds like a salesman, delighted: *"And diamonds! Fresh, shiny, lightly swallowed! Get yours from
Yooba, the best slime in Hell!"* (the joke is his own: he swallowed them in the Prologue); (4) the **hero cuts in**, deadpan,
the only one not buying it: *"You're selling me the hole I'm stuck in."*; (5) *"ACT NOW, and climb the WHOLE thing!"*;
(6) Yooba: *"Hee hee! He said yes!"*; (7) the hero, a long beat, then the last line, which the video ends on: *"...Great."*
No office or work jokes (the register is a shopping channel, never a job); Yooba is a villager and a seller, never an
enemy; the Overlord does not appear. Yooba's own hello stays on the Shaft at Floor 999 and picks up after it (he is not
introduced here: the ad is how he *sells*, the Shaft is how he *greets*). Hero in the Meadow's armour as in `player.json`,
Yooba as his portrait (`ui/yooba.json`). Voices are the existing `player`, `announcer` and `yooba`. Recipe and prices:
[LoreVideos.md](LoreVideos.md) "Video 3: Hell's Ad Break"; files in `videos/hell-ad-break/`.

### Circle IX: the Cracked Road (one level per floor)

Black spires with glowing seams over a road of cooling rock, the Rift's magma glowing in its cracks far below. The road
is the only way up, and it keeps splitting: the **Brimstone Demons** climb out of the cracks to keep him off it, and the
spires shed shards. Built **one level per floor** like Circle X (J, 2026-10-19): the route, every floor's page and the
floor bosses are in [LevelDesign.md](LevelDesign.md), section 6. In short: Floors 900-891 (*the Cracked Road*, the
Brimstone Demon met alone on Floor 900, its whelps, lobbers and the road's new pieces one a floor), ending on **the Road
Breaker** (891), the biggest demon on the road, who keeps it broken; **the Spared Demon** shortcut (891 to 860); Floors
860-851 (*the Carver's Yard*, the spire forest where everything is carved), ending on **the Carver** (851), the demon
who carves every floor number and rule on the road; **the Hollow Spire** shortcut (851 to 810); Floors 810-802 (*the
Needle Stair*, round the outside of the great spire) up to the crack in its side on Floor 802's landing, and through it
**Floor 801, *The Spire's Heart***, **the Spire Fiend**'s arena (*The Spire Fiend's fight*, below). At most 4 enemies
on screen.

**The heat (2026-10-25).** Above the Wastes Hell stops being polite: every authored floor of the Cracked Road that is not a
floor boss (900-892, 860-852, 810-802) runs about five minutes, like Circle X's long floors, and is hotter than any of them. The
cracks are where the Brimstone family is born, and past Floor 900 they are all open at once: his own opening stays as it was
(its idea, its first monster alone, its lines), then the road goes on in five set pieces and the demons, whelps, nests and
lobbers come out of it in numbers, with the imps' tricks that came up from the Wastes (ricochet, blink and lead imps, the
guards of the hauling route, the Overlord's lens from the last pieces on) and mixed waves of cinders, sparks and brutes from
the Rift. The climbs include ladders (the demons' own ways up the spires, bolted where they climb every day). The three
floor bosses (891, 851, 801) stay fights, not floors of this kind. Built by `Tools/Levels/cracked_floors.py`; recipe in
LevelDesign.md, *A long bullet-hell floor*, point 10.

The **great spire** is Circle IX's glimpse: always straight up through the haze, the Spire Fiend's, its seams pulsing,
growing floor by floor. The Spire Fiend is never heard on the route; his spire hums and grinds. (The hum is him
listening: on 803 it stops, because he has heard the hero.)

#### The Spire Fiend's fight

The arena is **Floor 801, *The Spire's Heart*** (`levels/Floor801.json`, level 61), through the crack in the great spire's
side on Floor 802's landing: the hollow inside of the spire, a round hall of black basalt whose seams glow, its floor a
crust over the Rift (the Rift's magma glows under it), shards hanging from the dark far overhead. It stands still like
the Slag Throne: a locked screen, the counter on 801 from the start (floors 801 to 801), the hero on the left, and at the
right a tall black spire standing on its own in the floor, its seams dim. That spire is him. He is about 2.6 units tall
risen out of it (over two heroes), and he never leaves it: he is rooted, the spire is his legs. The fight lives in the
`spireFiend` entity (its states) and the `sfShed`/`sfEnrage` sequences; the level runs the intro and the ending.

Every attack is telegraphed a second or more ahead, on the spot it will hit: a glow at his shoulder before he throws a
spire, a row of glowing cracks running along the floor before the needles burst up out of them, a glowing seam
overhead before a shard drops, and, for his worst, the whole floor glowing except the strip he leaves himself.

1. **Intro** (input and rewind off, the hero can't be hurt). The title (*"FLOOR 801: THE SPIRE'S HEART"*), then the hum
   comes back, louder than it ever was on the stair; the camera moves to the lone spire as its seams light up one by
   one, it cracks open and **he unfolds out of it** (*rise*). The announcer: *"FLOOR 801! THE WARDEN OF THE ROAD!"* and
   *"THE... SPIRE... FIEND!"*. His greeting, a whisper: *"Hmmmm. There you are. I heard every step. A hundred floors of
   them."* The camera comes back; the hero: *"He's been listening. Course he has."* The boss bar fills, control comes
   back: *"Climb, then. Nothing climbs higher than me."*
2. **Phase 1: rooted** (full health to half). He drives his claws into the floor and a row of cracks glows along it
   from his spire toward the hero; 1.1 s later each crack bursts into a **needle** of rock (`sfNeedle`: get off the row,
   or onto the ledges). And he snaps a spire off his own back and hurls it at the hero like a spear (`sfSpear`, the glow
   at his shoulder first). The first real hit: *"Hm. You chipped me."*
3. **Half health: the spire sheds** (`sfShed`). He shrieks without a mouth, the whole hall shakes and the spire starts
   shedding on them both. A whisper, cold: *"You don't climb MY spire."*
4. **Phase 2: the spire sheds, the cracks open** (half to a third). The same needles and spears, now with shards
   dropping from the dark over the hero's spot (`spireShard`: the seam glows 1.2 s, then the shard), and his cracks
   breed: **whelps** crawl out of the floor where his needles went in (*"Up. Out of the cracks."*; `demonWhelp`, at most
   two, so with him never more than three on screen). They come **only in this phase**, and like every guard they drop
   nothing.
5. **A third: the enrage** (`sfEnrage`). He loses the calm he is proud of: he rears up to his full height, the spires on
   his back grow longer, every seam flares white-hot, the screen flashes and his whelps crumble back into the rock.
   The one line he raises: *"ENOUGH. Everything here comes DOWN."*
6. **Phase 3: the needle field** (a third to the end). Faster needle rows, shards from overhead, and his worst: claws in
   to the wrist, and **the whole floor glows**, all of it but a strip against the far left wall; 1.6 s later the hall
   is a forest of needles (`sfNeedleField`). From where the hero usually stands the strip is too far to reach in time.
   That is what Déjà Vu is for: caught, he drags the loop back and is already running for the wall. The first field, the
   hero: *"Floor's all spikes. ...Let's try that again."* Near the end, the Fiend: *"Stop. Climbing."*
7. **Defeat.** Slow motion; his seams flicker and go dark one by one and he crumbles into a heap of black shards around
   the broken stump of his spire. His last words, a whisper: *"Nothing... higher... than... me..."* Announcer: *"THE SPIRE
   FIEND HAS FALLEN!"* Hero: *"He never got off that rock. I almost respect it."* The hum stops for good. Then the hero
   climbs the broken stump, and at the top of it, on Floor 801, a **Waystone lights**: *"WAYSTONE LIT! FLOOR EIGHT
   HUNDRED ONE!"*, the hero: *"Good. Fewer stairs next time."* From now on a run starts at 801. The Grudge pick, the
   results, then the Shaft.
8. **The Shaft after him.** The marker stands on 801 (Floor 802 climbed it there), the Spire Fiend's figure fills in
   (beaten), the Waystone at 801 lights, and the band above, **Circle VIII, the Rune Bridges, wakes up** (*"CIRCLE
   EIGHT! THE RUNE BRIDGES!"*). Next Warden: the Bridge Wraith. Continue goes to the level select until Circle VIII is
   built.

**Music:** the Cracked Road's loop and the Spire Fiend's theme are designed (*Music*) and in the manifest but not yet made; the hall is
silent but for the hum through the intro, and the fight plays the Wastes' push music until his theme is made.

#### Circle IX's long way

The first-pass stages (built 2026-10-18, 10 floors each, generated from `crackedRoad`) stay as the long way's
stand-ins, as Circle X's do. The first, `CrackedRoad` (900-891), is gone: Floors 900-891 replace it.

| Level | Floors | Place | The beat |
|---|---|---|---|
| 61 (`Level13`) | 890-881 | **The Rift** | The road cracks wide open under a carved *KEEP TO THE ROAD*; demons come out in twos. Hero: *"The road's coming apart. Can't blame it."* |
| 62 (`Level14`) | 880-871 | **The Old Stair** | A road past a staircase someone carved into the spires long ago, fallen in. At the top, the stairwell's mouth, *STAIRS (CLOSED)*: the Old Stair's shortcut (below) to 841. |
| 63 (`Level15`) | 870-861 | **Shard Fall** | The spires shed in earnest under a carved *HARD HATS*. Then Floor 860. |
| 64 (`Level16`) | 860-851 | **Demon Row** | Off the path (the level select only): Floors 860-851 are played now. |
| 65 (`Level17`) | 850-841 | **Halfway Up the Road** | Floor 850, the Circle's milestone (*"FLOOR 850: HALFWAY UP THE ROAD"*). |
| 66 (`Level18`) | 840-831 | **The Stairhead** | Where the Old Stair comes out, under *MIND THE RUBBLE*. Then Floor 810 (830-811 have no stand-in yet). |

## Circles VIII to IV: the middle descent *(working)*

Designed 2026-10-25 (J: *"Design Circles VIII, VII, VI, V and IV"*), before any of it is built. Everything here is a
**design, not content**: no entity, level or sound for these Circles exists yet, and **every name marked *(working)*
is a placeholder** (the five Warden names are canon, already on the Shaft). Entity ids are the ids the files will
take. It is Act 2 (see *Acts and story beats*): Hell gets stranger, the damned show what Hell does to people, and the
Overlord turns out to be as stuck as the hero.

**What every Circle below shares**

- **100 floors each**, the Warden on the top floor (701, 601, 501, 401, 301; each lights a Waystone, so the Shaft's
  Waystones sit at 701, 601, 501, 401 and 301 as before).
- **The played route is the Circle X/IX shape**: about 30 floors in three stretches (`x00-x91`, `x60-x51`, `x10-x01`),
  a floor boss at the end of the first two (`x91`, `x51`), a shortcut after each (`x91` to `x60`, `x51` to `x10`), the
  last stretch ending at the Warden's door (`x02`) and his arena (`x01`). One level = one floor. The route sketches are
  in [LevelDesign.md](LevelDesign.md), section 7.
- **Slimes are villagers, never enemies.** The Mire and the Caverns are wet; nothing in them is slime, and Yooba
  (who runs the shop on the Shaft) says so himself when he sees them.
- **One new idea per floor**, a new monster always alone first, at most 4 enemies on screen (section 1 of
  LevelDesign.md). **Each Circle has one signature mechanic** that the player must learn: it gets its own floor with
  nothing else on it (the Circle's first), is used in a harder way every few floors and is the heart of the Warden's fight.
- **Every enemy has a weakness** the player can read and use (rule 1 of *Rules for enemies*). Damned souls (the
  Shades, the Drowned, the Votives) are the only things that drop loop shards.
- **The comedy is Hell's**: repetition, showmanship, creatures that are very bad at being scary. Never office or work
  jokes. The hero stays deadpan, the announcer booms.
- **The Overlord thread.** One clue per Circle that he is as stuck as the hero (the quiet spine of Act 2).
- **Yooba** has one reaction line per Circle on the Shaft (`ui/yooba.json`, as for Circle X), kept in his voice.
- **Rewind** is the answer to each Circle's hazard in a different way; the floors may not require it, only reward it.

### Circle VIII: the Rune Bridges (Floors 800-701)

**Theme and look.** After the Road's red and black, cold: a void the colour of a bruise with no bottom, and across it
thin stone bridges and arches carved with glowing blue runes, far apart, strung like a thought nobody finished. Blue
fog, glass-clear light, floating eyes that follow the hero's every step. The runes are the Circle's lights; there is
no other. *(Look brief for the art pass: ink-violet void, cyan runes with a soft halo, pale grey masonry, mist bands
at three depths, distant bridges as silhouettes; the Road's embers are gone, replaced by drifting rune dust.)* Falling
off is not death but a drop to the span's start and a heart (`PitCatch`, as on the Wastes' path floors).

**Signature mechanic: Rune Spans (`runeSpan`, working).** A bridge piece is solid only while its rune is lit. A rune
brightens 1 s before the span appears (a hum) and dims 1 s before it goes out, on a fixed beat, so a row of spans
is a rhythm to read and cross (Hell's loop again: a rewind brings a vanished span back). The first floor teaches it
with one lit span and nothing else; later floors chain them, run them out of step, and make them follow the
Watchers' gaze.

**Comedy angle.** The runes only ever say what the thing in front of them is: *BRIDGE. THIS IS THE BRIDGE. STILL THE
BRIDGE. BRIDGE (AHEAD).* A Watcher has watched one bridge forever, has never once seen anyone cross it, and doesn't
know what to do with its face. Hero, the first time one blinks at him: *"Please stop looking at me."*

**The Overlord thread.** On the Circle's last stretch, a huge handprint is worn into a parapet, and below it a rune in
a hand much larger than the others: *UP*. Written, rubbed out, written again. Hero: *"He was trying to get out of here
too."*

**The Warden: the Bridge Wraith** (`bridgeWraith`, level `Floor701`). A hooded wraith with no legs, floating at the
middle of the Great Span with a lantern of blue runes, the keeper of every bridge in the Circle: she lights a rune
when someone needs to cross and puts it out behind them, and nobody has ever needed to for ages, so she is very
excited and very afraid of doing it wrong. *Personality:* hushed, courteous, anxious; apologises before every
terrible thing (*"Sorry. Sorry. Mind the gap. ...There isn't one. Sorry."*). Hates being rushed. Never raises her
voice; the lantern does it for her. *Voice* `bridgeWraith`: a dry, hollow whisper with a long echo tail, as if from the
far end of a hall; politely worried; a little breath-catch before her attacks; the lantern chimes when she speaks
loudest. *Fight idea (working):* the arena is the **Great Span**, a ring of bridges round her. Phase 1, *The Lantern*:
she sweeps the lantern and lights runes in a pattern: the pattern is the floor, so the hero must cross where she lights
and shoot the lantern's shutters when they open; her bolts come from the runes she lit. At half health, phase 2, *Snuffed*:
she shuts the lantern and the Great Span goes dark except for the runes the hero shoots: a shot rune holds its span for 5 s,
so he writes his own way across while her afterimages (`afterimage`) trace his path. At a third, phase 3, *Unwritten*:
she drops the lantern, the bridges unwrite themselves behind him on the beat and she floats after him, apologising.
Beaten, she puts her lantern down and the runes read, for the first time, something else: *THANK YOU*.
*Weakness:* the lantern's shutters (open for 2 s each time she lights something).

**Floor bosses** *(working)*
- **The Keystone** (`keystone`, Floor 791). The arch-stone every bridge hangs from: a stone golem who holds the first
  bridge up by standing under it and refuses to move. Slams that send a ring of broken span along the bridge
  (a glow 1 s first) and drops chunks of arch. *Weakness:* the lit rune on his brow, open only when he slams; shot
  out, the arch stays up by itself. At half, he steps aside and the bridge sways. Voice: slow stone, one word at a
  time: *"Load. ...Bearing."*
- **The Lamplighter** (`lamplighter`, Floor 751). The Wraith's chief carrier: a hunched wraith on a long pole lighting
  runes under the hero and a second later putting them out. *Weakness:* his pole's flame, shot out (he wanders blind
  and off the bridge in a gag the announcer calls). Two tiers: one bridge, then a double bridge with Watchers added.

**Enemies** *(entity ids working; all new)*

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `watcher` | Watcher | 800 | Floating eye that guards one bridge forever; sweeps a gaze cone, and a hero in it takes a bolt. | Closes its lid against shots; opens it to fire: shoot as it opens. |
| `lensWatcher` | Lens Watcher | 780 | A Watcher with a rune lens in front, firing a long beam along a bridge. | Break the lens (the bright disc); the eye behind is harmless. |
| `glyphScrawler` | Glyph Scrawler | 770 | A small hooded thing that scribbles runes on bridges because it can't stop. Its false runes look like spans but flicker wrong and aren't there. | Shoot it while it scribbles (it can't dodge mid-word); false runes vanish with it. |
| `lanternfly` | Lanternfly | 765 | Blue lantern-bugs that follow the lit runes and drift into the hero. | They die in the dark: shoot a lit rune off (or run past a dimming span) and they fall. |
| `edgeBiter` | Edge Biter | 760 | Something in the void that bites the edge of bridges. | Only its jaw shows; shoot it open-mouthed; it takes a bite out of the span and leaves it a gap. |
| `afterimage` | Afterimage | 745 | The bridges remember who crossed: a pale copy that walks the hero's route from 3 s ago. | Lead it into a Watcher's beam or off the edge; a rewind erases it. |
| `runeHound` | Rune Hound | 735 | Stone hound statues set by the spans: they wake when a lit rune is stepped on. | Shoot the rune on its collar; it sleeps again. Slow to turn. |
| `sleepwalker` | Sleepwalker | 725 | A wraith asleep on its feet that drifts along the bridge exactly one way. | Harmless until woken; a shot sets it off, so don't: wait for it to pass. |
| `keystone`, `lamplighter`, `bridgeWraith` | the floor bosses and the Warden | 791, 751, 701 | above | above |

**Shortcuts** *(working)*
- **The Watcher's Favour** (`watchersFavour`), Floor 791 to **Floor 760**: the Watcher that has watched the Keystone's
  bridge for ever is so thrilled someone finally crossed that it flies him over the next stretch in its gaze. *Cost:*
  30 diamonds ("It wants a keepsake"). Hero: *"It's... crying. Okay."*
- **The Unwritten Span** (`unwrittenSpan`), Floor 751 to **Floor 710**: a span with a blank rune over the void, a
  single stone with nothing written on it; shoot it and write a way up. *Cost (risk):* Floor 710 reads
  `shortcutTaken`: its runes are scribbled over by the Scrawler's false ones from the first second.

**Music.** Glassy: FM bells and a slow arpeggio in E-flat minor at 112 BPM that "writes" a new note every time a rune
lights, over a thin choir. The loop is one phrase that plays itself backward after four bars (the bridge is read, then
unread). Push music: the arpeggio in sixteenths with synth-brass stabs. The Wraith's theme: a whispered choir over a
funeral march in 3/4 and a single bell; the enrage drops the bell and plays the march twice as fast.

**Built: the first nine floors (2026-10-25, Floors 800-792, `levels/Floor800.json` to `Floor792.json`).** Each is a long
bullet-hell floor of about five minutes: a quiet opening, then five set pieces along one bridge, each with its own idea. The
Circle is `content/circles/runeBridges.json`.
- **Rune Spans are `runeSpan`** (data plus one ability, `RuneSpan.cs`). A rune hums for 1 s (cyan, flickering, not yet solid),
  the span is solid for 5.5 s (its last second blinks), then it is a faint outline until the next beat; a rewind brings a
  vanished span back. A row is a wave (each span lights a moment after the one before: `runeSpan`), all on one beat
  (`runeSpanUnison`) with a row a third of a beat behind it (`runeSpanLate`), a quick beat (`runeSpanQuick`, 6 s), spans
  that wait for him (`runeSpanHold`: lit while he stands on it), or spans that follow a Watcher's gaze (`runeSpanGaze`).
  A pier (`runePillar`) is the bridge's own stone and always there; falling off is the Circle's pit (a heart, thrown back
  up). Rune bolts (`runeBolt`) are cold, die on a lit span and pass through a dark one.
- **The Watcher** (`watcher`, first met on Floor 797) is a floating eye: lid shut for 2.6 s (shots from every side are
  blocked), lid open for 2.2 s (a slow fan of three bolts, and it can be shot): *shoot as it opens.* On Floor 793 a
  Watcher patrols above the bridge (`watcherPatrol`) and the spans under its gaze light.
- **The safe intro is Floor 800, The Bridge Foot.** No enemy at all: the first stretch is spans that wait for him (he
  cannot be dropped by a rune he is standing on), then spans on a slow wave, then rows with piers between, then a thin
  rain of bolts. Everything the Circle asks of him is learnt there.
- **The nine floors and their one new idea each.** 800 The Bridge Foot (the span alone); 799 Two In A Row (the beat, the
  first imp); 798 Three In Time (ricochet imps bounce their embers off the bridge); 797 The First Watcher; 796 Out Of
  Step (rows a third of a beat apart, bulwark imps); 795 The Stagger (blink imps); 794 The Hum And The Glow (lead imps
  that aim where he will be); 793 Glance Away (spans that follow a gaze); 792 Still The Bridge (everything, ahead of
  the Keystone on 791). The runes only ever say what the thing in front of them is: each landing is carved *BRIDGE*,
  *STILL THE BRIDGE*, *BRIDGE (AHEAD)*, *THIS IS THE BRIDGE*, *BRIDGE (BEHIND)*, *STILL BRIDGE*, *BRIDGE, LIT*,
  *BRIDGE (WATCHED)*, *BRIDGE. MORE BRIDGE.*
- **Not built yet:** the Keystone (791), the Watcher's Favour, Floors 760-751 and the Lamplighter, the Unwritten Span,
  the Circle's lower floors and the Bridge Wraith, the Lens Watcher, the Glyph Scrawler and Lanternfly, and the rune a
  shot holds lit for 4 s. Continue after Floor 792 returns to the level select until the Keystone is built.
- **The Shaft wakes Circle VIII** as before (*"CIRCLE EIGHT! THE RUNE BRIDGES!"*): the band now belongs to the Circle, so
  its floors-left and next Warden read from it, and Continue after Floor 801 goes on to Floor 800.

**Built: the lower half (2026-10-25, Floors 751-701, `levels/Floor751.json` to `Floor701.json`, written by `Tools/Levels/rune_lower.py`).** Floors 791-752
(the Keystone, the Watcher's Favour, the Circle's middle) are still to build; the lower half stands on its own and is played from the level select, and
the Lamplighter's floor stays 751 as above. Continue after Floor 702 goes to the Bridge Wraith, and after Floor 701 to the level select until Circle VII is built.
- **The Lamplighter (Floor 751, the mid-boss).** The Wraith's chief carrier, on one held bridge (a Travel camera on an Arena section, like the Pile Keeper's
  mound). Three phases and a last stand: *one bridge* (a glow, a second later a column of rune where the hero stands, then a fan of flame from the pole),
  *two bridges* at two thirds (`llTier2`: Watchers, imps and lead imps come up to four of a kind), *blind* at a third (`llBlindSignal`: his flame is nearly
  out, up to six minions) and a last stand at an eighth (`llLast`). Every attack is a glow first. *Weakness:* the flame on his pole, so the hero's hint is
  *"The Lamplighter. His whole job is the flame on his pole. So I shoot the flame."* Beaten, he walks off the bridge (*"Oh. ...Is this the edge?"*,
  the announcer: *"THE LAMPLIGHTER HAS WALKED OFF THE BRIDGE!"*) and the counter ticks to 750. His voice, `lamplighter`: thin, creaky, fussy.
  The Unwritten Span shortcut (751 to 710) is not built; this stretch has none.
- **Floors 750-702 are short crossings** (forty-nine floors, about 110 s each: a foot, three set pieces, a far end; the Circle's first nine were long for
  the lesson, the lower half is long in number): the same Rune Spans, one idea at a time, a Watcher's gaze, a carved landing that says what it is
  (*BRIDGE. HALFWAY.*, *MIND THE GAP*, *NO GAP (SORRY)*, *DO NOT WAKE IT*, *UP* below the Overlord's handprint on 708, *SORRY. SORRY.* on 702).
  Floor 750, *Halfway Across*, is the Circle's milestone. Three new creatures, each met alone first, built from existing mechanics:
  the **Afterimage** (745: a pale copy of the hero that follows slowly, and is shot from range), the **Rune Hound** (735: a stone hound that wakes only when he
  comes near, then crouches and lunges) and the **Sleepwalker** (725: drifts one way and is harmless until a shot wakes it, then it sprays bolts, so the hero
  waits). The runes' comedy is the Circle's. The title of every floor is voiced by the announcer and the hero says one line from a shared set of sixteen
  (*"Hell loves a repeat"* is a joke the floors make themselves). The Circle's table (`runeBridges`) adds the three creatures from their floors.
- **The Overlord's thread** is on 708-706: a huge handprint on a parapet and *UP*, rubbed out and written again (*"He was trying too."*).

#### The Bridge Wraith's fight (Floor 701, `bridgeWraith`, level `Floor701`)

The Great Span, a Static screen with a ring of four bridges lit on the beat above the floor, the Wraith floating at the middle with her lantern.
Voice `bridgeWraith` (hushed, courteous, anxious, an echo tail). She apologises before every terrible thing; every attack is a glow 1+ s first.
1. **Intro.** *"FLOOR 701: THE GREAT SPAN"*, the hum, the camera on her as she rises, *"FLOOR 701! THE WARDEN OF THE RUNE BRIDGES!"*, *"THE BRIDGE WRAITH"*, her whisper
   (*"Oh. Oh, hello. Someone needs to cross. ...Sorry. It has been so long."*), the hero (*"She's already sorry. That's worse."*), the boss bar.
2. **Phase 1, The Lantern** (to 75%, then her carriers come: `brWave`, imps, ricochet imps and blink imps). She lights something (a fan of rune bolts from
   the lantern after a glow, or a rune column under the hero after a glow) and the lantern is open; then it shutters and shots are blocked for a moment. Shoot it
   open. The hero's hint: *"The Bridge Wraith. The lantern opens when she lights something. So I shoot it then."*
3. **Phase 2, Snuffed** (half health, `brSnuff`, `brPhase2`). She shuts the lantern and the Great Span goes dark: the ring is gone and three spans light only while the
   hero stands on them. Her little lights come (`lanternfly`, the only add in this phase), with Bulwark, lead imps, Afterimages and Watchers up to six of a kind;
   predictive bursts and wider fans.
4. **Phase 3, Unwritten** (a third, `brEnrage`, `brUnwrite`). *"Unwritten. Unwritten. Please, do not rush me."* The ring comes back on a quick beat and walls of runes
   fall across the floor after their glow. The hero: *"The bridge is unwriting itself. Behind me. Of course it is."* A last stand at 15% (`brLast`): up to eight of
   six kinds at once, with the Rune Hound, and all her attacks in turn.
5. **Defeat.** Slow motion; she puts the lantern down and the runes read, for the first time, *THANK YOU*. Her last line: *"Oh. ...Thank you for crossing."* Announcer:
   *"THE BRIDGE WRAITH HAS FALLEN!"* Hero: *"She said thank you. Hell, first time."* Then the Waystone lights on Floor 701 (*"WAYSTONE LIT: FLOOR SEVEN HUNDRED ONE!"*,
   the hero: *"Good. Fewer bridges next time."*). A run starts at 701 from now on.
6. **The Shaft after her.** The marker stands on 701, the Wraith's figure fills in, the Waystone at 701 lights and **Circle VII, the Gloomwood, wakes up**
   (*"CIRCLE SEVEN! THE GLOOMWOOD!"*, `vo_shaft_wake_vii`). Next Warden: the Hollow King.
*Art:* the Wraith, the Lamplighter and the three creatures are placeholders (specs `Assets/GameConfig/art/*.json`; the GPU run is priced, not approved).
*Playtest:* `bridge-descent` (a sample of the lower half and then the Wraith with the Shaft), `bridge-wraith`, `level-N` and `rune-bridges-3` onward.

### Circle VII: the Gloomwood (Floors 700-601)

**Theme and look.** A dead forest at no time of day: black trees with their bark peeling in sheets, grey fog at knee
height, torches on posts burning a sick yellow, paths worn bare in loops and a faint footprint on every one. Shadow
blobs with yellow eyes in the gaps between trunks. Nothing here is lit well. *(Look brief: charcoal trunks against
bruised-green sky, torchlight as the only warm colour, fog banks that scroll, a lane of worn dirt marked by torches on
the floors with Shades.)*

**Signature mechanic: Loop Lanes (`loopLane`, working).** A **lane** is a torch-marked circuit on the floor along
which Shades walk the same walk on a fixed beat, forever. A Shade only hurts the hero if he is standing in its lane
when it passes; he can cross a lane in a gap, stand behind a Shade and walk with it, or jump over. Lanes never change
(they are loops already; a rewind replays them to the frame), and in the Circle's **Long Walk** floors the *same floor
is placed three times in a row*: the way up is the one Shade walking the wrong way. The first floor has one lane, one
Shade and nothing else.

**Shades** are the Circle's damned: souls stuck repeating the same walk. Shot, a Shade stumbles and sits out a lap
(it cannot be killed), and its loop shard drops once. They are never enemies in a way the hero resents: they are what
Hell does to people. Each is a person with a walk: one carrying a bucket, one who is always late and never
arrives, one who stops and checks his pockets in the same place on every lap.

**Comedy angle.** The Shades are very polite: when two meet in a lane they apologise and step aside, in the same
order, on every lap, for ever. A sign counting laps (*LAP 47,212*) is tallied by a Shade who is not at all
bothered. One of them waves at the hero at the same point on every lap, and the hero waves back on the third. Hero:
*"Hell: now with scenery."*

**The Overlord thread.** A very large lane, many times the size of the others, its footprints deep and each the
same: an enormous Shade-like walker at the far edge of the forest, walking a circuit round a tree. Never reached.
Hero: *"...That one's tall."* Announcer, quietly, the only time: *"DO NOT FOLLOW THAT ONE."*

**The Warden: the Hollow King** (`hollowKing`, level `Floor601`). A dead hollow tree with yellow eyes in its trunk and
a crown of branches: the oldest tree in the Gloomwood, a king whose kingdom is its walkers. Anyone who walks into his
hollow comes out a Shade; he loves company, has had a lot of it, and remembers every guest. *Personality:* wide,
welcoming, lonely, creaking; hospitable to a fault and quite unable to let anyone leave; repeats the ends of his own
sentences, softly, as if the forest answered. *Voice* `hollowKing`: a big wooden barrel of a voice, deep and kind,
with an echo tail and creaks between the words; sad when he's being kind, never raised. *Fight idea (working):* the
arena is **his clearing**, three ring lanes round the trunk with Shades walking each at a different speed. Phase 1,
*Company*: the hero must stand in the safe gaps of the lanes and shoot the yellow eyes in the trunk when a branch lifts
away; roots erupt after a glow where he stands too long. At half health, phase 2, *The Hollow*: the trunk opens and
shows a crowd of Shades standing in it, politely, waving; he sucks the hero toward the hollow (a pull to the trunk) while
the Shades walk the ring lanes faster. At a third, phase 3, *The Long Walk*: he steps out of his roots and walks his own
lap round the clearing, every attack replaying in the same order each lap, which a rewind rewards the player for
learning: a pattern seen once is a pattern solved. *Weakness:* the yellow eyes, open only when he speaks.

**Floor bosses** *(working)*
- **The Pathfinder** (`pathfinder`, Floor 691). The Shade who has been walking the longest, grown huge: a hunched
  pilgrim with a lantern, walking a circuit round the arena's middle that the hero has to cross. Each lap he drops his
  lantern's embers (a glow on the lane first). *Weakness:* a shot to the lantern makes him stumble for a lap; three
  stumbles and he sits down, finally, and says "Oh," and the lane stops. Voice: faint, thoughtful: *"Is this the way?
  ...It's always the way."*
- **The Woodsman** (`woodsman`, Floor 651). A hulking Shade with an axe who has been chopping the same tree for as long
  as the forest has been dead. The tree never falls. The axe swings on a fixed beat (a glow on the floor under the
  blade 1 s ahead); the tree sheds the trunk-eyed Gloomlings. *Weakness:* the tree: shoot it and it finally falls, he
  stares at it, and the hero walks past him. Two tiers: the clearing, then the log run.

**Enemies** *(entity ids working; `shade` and its variants are all damned and drop loop shards)*

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `shade` | Shade | 700 | A soul stuck repeating one walk. Hurts only inside its lane as it passes. | Can't be killed. A shot makes it stumble and sit out a lap; a loop shard drops once. |
| `shadeTorch` | Torchbearer | 685 | A Shade whose walk is carrying the torch that marks the lane. | Shoot the torch: the lane's Shade lays its walk down, and the lane is safe. |
| `gloomling` | Gloomling | 680 | The shadow blobs with yellow eyes that live in the gaps between trunks and pounce on whoever stands in shade. | Torchlight pops them: stand in a torch's pool, or shoot a torch down onto them. |
| `grabBranch` | Grasping Branch | 670 | A dead limb that lashes at passers-by on a beat. | Its knot, a yellow eye in the wood; shot, it goes limp. |
| `rootSnare` | Root Snare | 665 | Roots that come up through the loop's footprints. | A glow 1 s first, then a snare. Can't be shot, only left. They are weakest where the hero keeps moving. |
| `carrionCrow` | Carrion Crow | 660 | Crows that fly the same loop every lap. | Its loop is exact: shoot where it will be at its apex. |
| `hollowStag` | Hollow Stag | 640 | An antlered husk, charging down the path it has charged for ever. | Its antlers lodge in a trunk at the end of its charge, stunning it: shoot it then. |
| `stumpMimic` | Stump Mimic | 635 | A stump that looks like a seat; it wakes when you stand on it. | Torchlight and a shot to its hollow when it gapes. |
| `pathfinder`, `woodsman`, `hollowKing` | the floor bosses and the Warden | 691, 651, 601 | above | above |

**Shortcuts** *(working)*
- **The Wrong Way Round** (`wrongWay`), Floor 691 to **Floor 660**: after the Pathfinder sits down, one Shade is
  still walking, the wrong way, out of the loop. Follow it a full lap without shooting (it leaves the lane at the
  end). *Cost:* a Grudge. Hero: *"Where are you going?"* The Shade turns, shrugs, and is gone.
- **The Hollow Way** (`hollowWay`), Floor 651 to **Floor 610**: a door in a trunk, the King's roots from the inside.
  *Cost (risk):* Floor 610 reads `shortcutTaken`: roots erupt from the first second, and the announcer says why.

**Music.** Spare and dry: a lone detuned guitar, wooden blocks and a low drone in A minor at 92 BPM; the loop is one
bar repeated, audibly, and the longer you stand in the forest the more the bar thickens by one layer. Push music:
drums and wolves under the same bar. The Hollow King's theme: a choir of Shades humming one note, a big hollow drum,
a bowed saw; the enrage is the humming in unison at double time and the drum beaten on the trunk.

### Circle VI: the Mire (Floors 600-501)

**Theme and look.** A drowned graveyard: black water to the ankle and in places the waist, tilted headstones and
stone crosses standing up out of it, reeds, ghost lights green and pale hovering where nothing should be, mist,
rain that doesn't wet. Everything is cold and wet and quiet. **Nothing here is slime**: the water is clear, black
and cold, the mud is peat, and Yooba says so (*"That's a swamp. I'm a villager."*). *(Look brief: green-black water
with a hard surface line, tombstones as platforms at the tide's different heights, reeds as parallax, green lights
glowing as the only colour, a pale moon through the mist.)*

**Signature mechanic: The Tide (`mireTide`, working).** The water rises and falls on a beat (the tide comes in on
the dot, Hell's loop). In the water the hero is slowed and **Bog Hands** can grab; out of it (on tombstones, mausoleum
roofs, the path) he moves freely. Tide marks on the stones show the rise 2 s ahead. It is the Wastes' rising lava
turned into a clock you read: a hot bath, never a pit. A rewind sends the tide back. The first floor teaches it with one
slow tide and a lone tombstone; later the tide is mixed with the Wisps' lures, the Epitaphs' text and the Pallbearers' lanes.

**Comedy angle.** The ghost lights are terrible at luring: *"Over here! ...No, other here. Sorry."* The epitaphs
are the jokes: *HERE LIES SOMEONE (AGAIN). BELOVED BY THE ONE WHO DUG THIS. GONE TO A BETTER FLOOR (THIS ONE).* The Bog
Hands only ever grab one thing, a single boot, which gets passed from hand to hand across the whole Circle. Hero: *"Everyone
down here is waiting for something."*

**The Overlord thread.** A statue of the Overlord, as big as a house, drowned to its chest at the Mire's lowest point
and rimed with moss: put there by the Drowned Queen, who once waited for him. The inscription: *HE NEVER CAME BACK FOR US.* Hero:
*"He wasn't a good date, then."* *(Working: confirm with J whether the Queen's story is hers or the Overlord's before building.)*

**The Warden: the Drowned Queen** (`drownedQueen`, level `Floor501`). Soaked hair over her face, a waterlogged gown
that never dries, a crown of wound weed, green ghost lights hovering where her crown's jewels were. She is a
queen who is sure the wedding is today: she has set a ballroom in the flooded crypt for guests that never came, and
every guest the hero kills is a guest she never got. *Personality:* grand, grieving, theatrical, soaked, with sudden
bright cheer that turns on a word; sulks magnificently. Calls the hero "guest", then "late". Takes the water personally.
*Voice* `drownedQueen`: bubbling, watery, slow and mournful with a bright, brittle cheer that keeps breaking through; drip
and gurgle between phrases; sobs that become laughter. *Fight idea (working):* the arena is the **flooded ballroom**,
the tide as her mood. Phase 1, *Tears*: she weeps and the tide climbs; her tears rain in lines (a glow on the surface
1 s first); the hero crosses tombstone tables and shoots the green lights of her crown to dry her. At half health, phase 2,
*The Waltz*: the water drops to the floor and her gown sweeps round the hall in a slow, wide wave to jump (the glow
first), with Bog Hands as dancing partners. At a third, phase 3, *Drained*: she pulls the plug on the ballroom, the
water rushes toward the middle in a current that drags the hero, and she calls every wisp to herself; the hero
fights toward the centre against the current to put out her last lights. *Weakness:* the green lights in her crown
(each one shot dries a patch of her gown and slows the tide).

**Floor bosses** *(working)*
- **The Gravedigger** (`gravedigger`, Floor 591). A stooped drowned digger who digs graves wherever the hero stands,
  and never finishes one. A glow under the hero's feet for 1 s, then a pit with a lid of earth (jump out of it, a heart
  if he's caught). *Weakness:* his spade hits stone at the end of every third dig and rings: a shot to the
  blade then stuns him. He is the Mire's simplest soul and never says why. Voice: low, plain, soft: *"Nearly done."*
- **The Ferryman** (`ferryman`, Floor 551). A drowned boatman poling a barge through the tide lanes; the hero fights
  on the barge deck while the tide rises and the barge drifts. He won't take passengers. Strikes with the pole (a
  swing across the deck, a glow first), drops the barge's moorings. *Weakness:* his lantern at the stern; shot, the barge
  runs aground on a tombstone and tips him in. Two tiers: the barge, then the half-sunk wreck.

**Enemies** *(entity ids working)*

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `wisp` | Wisp | 600 | A green ghost light that lures walkers into the water. It drifts toward the hero and pops cold. | It hides in the reeds; hit it when it drifts out. Two hits, and it hates loud noises: it dims when a shot passes near. |
| `bogHand` | Bog Hand | 595 | A hand reaching up out of the water and the peat, grabbing ankles. | Grabs only if the hero stands still on its spot 1 s: keep moving. Shot at the wrist it lets go. |
| `drownedMourner` | Drowned Mourner | 585 | A drowned figure in black with a wreath, weeping. Throws wreaths in arcs. | Sobs (stunned and open) every three throws: shoot then. |
| `graveEel` | Grave Eel | 575 | A long eel in the tide, snapping at tombstone edges. | Surfaces to bite and is stranded at low tide: shoot it then. |
| `epitaph` | Epitaph | 570 | A headstone that spits its own inscription, letter by letter, as shots. | Shoot the date at the bottom; the letters stop. |
| `pallbearer` | Pallbearer | 560 | Four drowned bearers carrying a coffin along a lane. | Kill one and the coffin tips, opens and blocks the lane (a diamond inside); the rest drop it. |
| `mistHeron` | Mist Heron | 550 | A tall bird that stalks the tide line and spears at the platforms. | Rooted while striking: shoot between strikes. |
| `bogBloom` | Bog Bloom | 540 | A water lily that opens and spits spores slowly. | Closes if shot from the front: shoot as it opens. |
| `gravedigger`, `ferryman`, `drownedQueen` | the floor bosses and the Warden | 591, 551, 501 | above | above |

**Shortcuts** *(working)*
- **The Plug** (`mirePlug`), Floor 591 to **Floor 560**: a stone plug in the mire; pull it and the tide drains for 30
  seconds and the hero walks across the Mire's bed, past everything the water was hiding. *Cost:* 25 diamonds
  ("The Mire keeps what it's owed"). Hero: *"Why is there a plug."*
- **The Widow's Coffin** (`widowsCoffin`), Floor 551 to **Floor 510**: a coffin that floats up the flooded crypt's
  stairs. *Cost (risk):* Floor 510 reads `shortcutTaken`: the ballroom's water is already in the room.

**Music.** A dirge on a waterlogged piano and a bowed cello in F-sharp minor at 76 BPM, with a pizzicato ping for the
wisps and bubbling for percussion. Push music: rain and cello stabs, the tide turning up. The Queen's theme: a
slow waltz on a drowned piano and a wet choir that sounds like it is singing through a bath; the enrage is the waltz
at double time falling apart.

### Circle V: the Caverns (Floors 500-401)

**Theme and look.** Warm, pink-grey, wet, and breathing: caves of stone that look like gums, stalactites like teeth,
walls that swell and settle, drool in ribbons from the ceiling, pale light from glowing cysts. After the cold, the
heat of a mouth. The Caverns are the Mother Maw's, and she is huge; the further up, the more it is her mouth. *(Look
brief: fleshy rose-grey stone, tooth-white stalactites and ridges, glossy drool, deep red throat openings, glowing
cysts as the light; the camera sits low and the ceiling hangs.)*

**Signature mechanic: The Gulp (`gulp`, working).** The Caverns breathe: on a fixed beat the cave inhales (a wet wind
that drags everything toward the throat, a sliding pull on the hero), then exhales (a push the other way). **Drool**
pools slow him and slick the floor. A floor teaches it with one gulp and the drool alone; later floors put ledges
the gulp sweeps, **peristaltic floors** that move a stretch of floor like a conveyor on the beat, and tooth-ledges that
close on the exhale. A rewind runs the breath backward.

**Midpoint: Floor 500, *The Way Home*.** The first floor of the Caverns, and the halfway point of the game. A door in
the cave wall with daylight under it, a real meadow smell. Yooba (on the Shaft the
floor before): *"That's... home. That's the way home."* It is the hero's chance to leave (Act 2's midpoint). The
Overlord's gate will open anyway; the village will not be saved. He opens the door, looks at the village whole,
sees the sky behind it is already Hell-red, says *"...Yeah. No,"* and closes it. The card always ends there: the
Door is a **shortcut he doesn't take** (it appears on the Shaft as a dotted line that goes up and back down, then
the climb on). *(Working: confirm with J that a card whose only real option is "no" is what he wants.)*

**Comedy angle.** Everything here is hungry and polite about it: the Droolers say grace; the Lurkers sulk when spotted;
every creature calls the Mother Maw "Mum", including the ones who are clearly not her kids. A sign: *PLEASE DO NOT FEED
THE FLOOR* (the floor eats it). The echoes in the cave repeat the hero's lines back wrong. The same half-eaten things come round on the
Gulp again and again. Hero: *"It's like being inside a sneeze."*

**The Overlord thread.** On the Way Home's floor, the door has been opened many times: the handle is worn, and the
hand that wore it was enormous. The grass under the step is dead, trodden down. Hero: *"He's tried this door.
...A lot."*

**The Warden: the Mother Maw** (`motherMaw`, level `Floor401`). A cave beast that is mostly mouth: teeth, drool, many
small eyes along her brow, and tiny forelimbs for fussing. Hell's greatest mother: she feeds everything, never
stops feeding, and cannot understand why the hero keeps running. *Personality:* warm, smothering, insistent,
mortified by manners (hers and others'); coos and complains with her mouth full. Calls the hero "love" and "dear" and
"you're skin and bones". *Voice* `motherMaw`: huge, wet and cooing, a lullaby-baritone with slurps and swallows between
words, tender in a way that makes it worse; booms only when she says "EAT." *Fight idea (working):* the arena is
**her mouth**. Phase 1, *Open Wide*: the floor is her tongue, lashing along the arena on a beat (a glow first, jump it);
her teeth hang from the ceiling and bite down on a toll (stalactite glow, 1 s); the hero shoots the sore tooth she
hides behind her tongue. At half health, phase 2, *Swallow*: she gulps and the arena becomes the **throat**, a
climbing arena down which a swallow pulls him on the beat; he climbs it against the gulp, with drool slowing every
ledge. At a third, phase 3, *Sulk*: all her small eyes open at once along her brow, each a turret (glow, then beam);
shoot them out one by one while she sobs *"After everything I've done for you."* *Weakness:* a cracked tooth at the back.

**Floor bosses** *(working)*
- **The Baby Maw** (`babyMaw`, Floor 491). The Mother's cub, a toddler all mouth and no sense, in a nursery of gnawed
  bones and a cradle of ribs. Throws tantrums: bounces across the arena (a shadow on the floor first), spits drool-bombs,
  cries (a shockwave from its mouth). *Weakness:* the pacifier-shaped lump in its mouth: shoot it and it lets out a
  wail that stuns the cub. At half health it calls for Mum (a distant boom, the Warden's voice, the first time the
  hero hears her: *"What's the matter, dear?"*). Voice: a high, wet whine, "Maaa".
- **The Grand Molar** (`grandMolar`, Floor 451). A huge tooth with a mind of its own, grinding across a tier of the
  gum. Rolls and grinds (a trail glow first), drops its roots. *Weakness:* the cavity (a dark spot on its crown): shoot it
  and the tooth wobbles. Two tiers: the lower jaw, then the upper, where the roof bites down.

**Enemies** *(entity ids working; the Drooler and the Lurker use art that exists)*

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `fighter` → `drooler` | Drooler | 500 | Cave beast, the Mother's loudest child, forever drooling (`Fighter` art; Death2 is it eating you). | Slobbers a slick behind it: it slips on its own drool and is open when it falls; the drool sack on its chin. |
| `sentinel` → `lurker` | Lurker | 497 | Crouching cave hunter that waits in alcoves for the hero to pass, then pounces (`Level3Enemy` art; Death3). | Freezes when watched: face it and it holds still; shoot it then. |
| `gumGrub` | Gum Grub | 490 | Larvae in the gum that wriggle out of the wall in a stream. | Stick in the drool and can't turn: shoot them in a line. |
| `molar` | Molar | 485 | A rolling tooth that tumbles down the slope. | The cavity on its crown; a hit splits it. |
| `stalactooth` | Stalactooth | 480 | A hanging tooth that drops when the hero's shadow passes. | Fall to the floor and stick in it; the second one is slower. |
| `tongueLasher` | Tongue Lasher | 470 | A tongue that reaches along the floor from a wall hole and lashes. | The tip is tender: shoot it and it retreats. |
| `belcher` | Belcher | 460 | A cave creature that burps gas bubbles that rise slowly. | The bubbles pop when shot (harmless); touched, they burst. |
| `leftover` | Leftover | 455 | Something the Maw ate long ago (an imp's helm, a demon's horn, a bridge's lamp) that got up again. | Half-digested: one hit. |
| `babyMaw`, `grandMolar`, `motherMaw` | the floor bosses and the Warden | 491, 451, 401 | above | above |

**Shortcuts** *(working)*
- **The Way Home** (Floor 500): above; the shortcut he never takes.
- **The Swallow** (`swallow`), Floor 491 to **Floor 460**: let a Drooler eat him and ride the gulp up the throat.
  *Cost:* his Déjà Vu meter is emptied on arrival (rewind juice to zero, a sacrifice). Hero: *"I'm going to
  regret this. ...I do regret this."*
- **The Hiccup** (`hiccup`), Floor 451 to **Floor 410**: the Caverns hiccup and shoot everything on the floor up the
  throat; he holds on. *Cost (risk):* Floor 410 reads `shortcutTaken`: the Gulp pulls twice as hard, because the Mother noticed.

**Music.** Wet, slow industrial in C minor at 100 BPM: a bass that is a heartbeat under a squelching sub, gurgles for
percussion, a distant moan in the pad. The loop is a heartbeat and a breath, audibly in step with the Gulp. Push
music: the heartbeat doubles and a tuba joins. The Mother Maw's theme: a lullaby played on a tuba choir over wet bass; the
enrage is the lullaby at double time and screamed.

### Circle IV: the Dead Peaks (Floors 400-301)

**Theme and look.** After the Maw's heat, cold: moonlit mountains with nothing living on them, black crags edged in
blue-white, dead pines, snow that falls upward in the wind, and in every ledge a candle shrine with a flame that
shouldn't still be burning. Rope bridges, monastery walls cut into the cliff, bells, wind. Down the middle of the sky,
the **Bell Tower**, tall and cracked, far above. *(Look brief: silver moon, ink crags in layers, ice-blue light, warm
candle-yellow as the only warmth, rope bridges and prayer flags, mist in the valleys, the tower in every sky.)*

**Signature mechanic: The Toll (`toll`, working).** A great bell tolls across the whole Circle on a fixed beat. The
candles of the shrines flicker one beat before it; on the toll a ring of force runs along the ledges and rope bridges
(jump it) and loose stones shake free; in the quiet between tolls he is free. **Candles** light safe pools:
**Gargoyles** are stone while their shrine's candle burns and wake when it goes out, so shooting a candle is a
weapon *and* a risk. The first floor teaches the toll alone with one ledge and no monster; later floors pair it with the
candles, the wind and the rope bridges. A rewind runs the toll back, and a bell that has not yet rung can be avoided.

**Comedy angle.** The monks are deeply devoted to a service that never arrives: they rehearse the same service, light
the same candles, put them out and say *"Again."* The gargoyles are very bad at being statues: they hold the pose
when watched, and strain. A sign beside the first great bell: *DO NOT RING*; the bell has been rung every time. Hell is
repetition: ring, ring, ring. Hero: *"I'd say it's a bit much. But here we are."*

**The Overlord thread.** The tower's toll is a **countdown**: the Bell Ringer rings once for each floor the gate has
left to open; the hero's climb has been moving the number, and the count on the bell's wall matches the Shaft's
marker. The shrine's candle that is the biggest has the Overlord's face in its wax. Hero: *"It's a countdown. For him."* The
Announcer: *"HE IS RUNNING OUT OF TIME. SO ARE YOU. IT'S A DRAW."* *(Working.)*

**The Warden: the Bell Ringer** (`bellRinger`, level `Floor301`). A hooded gargoyle monk with a cracked iron bell
on his back and candles melting on his shoulders, grey stone and old wax. He rings the toll that counts the
gate down; the bell is cracked, never rings true, and it drives him quietly mad. *Personality:* devout, tired,
patient to the point of menace; a perfectionist about one note he can never make; everything is "almost". Never hurries;
hums between phrases. *Voice* `bellRinger`: a low, flat monotone chant, hushed and devout, with a dead clang
under each phrase; when he's really speaking the bell answers. *Fight idea (working):* the arena is the **belfry**,
bells hung round a platform in a ring. Phase 1, *The Toll*: he swings the bell on his back and the toll sends a ring
along the floor (a glow, then the ring: jump it) with falling plaster; the hero shoots his shoulder candles out, each of which
silences one of the tolls in the rhythm. At half health, phase 2, *The Peal*: he rings the belfry's bells in a sequence,
readable on the glints, the rhythm being the attack, with the wax from his candles slowing the floor. At a third, phase 3,
*Cracked*: the bell splits and his toll becomes a whole-screen ring (jump on the beat, the screen shakes) and he does
nothing else; the hero drops a bell on him from the rope by shooting it free. *Weakness:* the candles on his shoulders; with
all out he can't ring.

**Floor bosses** *(working)*
- **The Cantor** (`cantor`, Floor 391). A gargoyle who sings in bell notes, perched over a chapel ledge: each note
  a ring of force that wakes the statues round him. *Weakness:* his open mouth, shot between notes silences him a
  bar. Two tiers: the ledge, then the choir loft where the statues wake.
- **The Wax Abbot** (`waxAbbot`, Floor 351). An abbot made of candle wax who melts when shot and reforms from the puddles
  (a glow on each puddle 1 s before the piece stands up). *Weakness:* the cold: lead him into the open window and his wax sets
  where he stands; a shot then shatters it. Voice: soft, kind, going out: *"Bless... bless..."*

**Enemies** *(entity ids working)*

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `gargoyle` | Gargoyle | 400 | Stone guardians on the ledges. | Stone while its shrine's candle burns; wakes and dives when it goes out. Hit it mid-dive (eyes), not perched. |
| `candleMonk` | Candle Monk | 395 | A robed monk who keeps one candle lit and drips wax pools (slowing) wherever he goes. | The candle: shot, he crumples to ash, and the pool dries. |
| `moonMoth` | Moon Moth | 385 | Pale moths drawn to candle flames; they spiral into the hero. | They go for flames: shoot a candle out and they flutter off. |
| `prayerWheel` | Prayer Wheel | 380 | A rolling wheel on a rail that fires a petal on each turn. | Jam the axle with a shot and it stops. |
| `rimeHound` | Rime Hound | 370 | A ghost wolf that howls on the toll. | Drowns out the toll: dies to a toll it can't hear. |
| `screeSnake` | Scree Snake | 365 | A slide of rock that comes down slopes like a snake. | Soft belly: jump it and shoot from behind. |
| `votive` | Votive | 350 | The damned: tiny flames, each carrying a bell, that walk a procession round the Peaks. | Pop one and the next pops (a chain); loop shards drop from the damned. |
| `clapper` | Clapper | 340 | A bell's clapper that got out and swings on a chain. | Hit the bell it hangs from. |
| `cantor`, `waxAbbot`, `bellRinger` | the floor bosses and the Warden | 391, 351, 301 | above | above |

**Shortcuts** *(working)*
- **The Ringer's Rope** (`ringersRope`), Floor 391 to **Floor 360**: a bell rope that rings a counter-toll and
  rocks a stone staircase loose. *Cost:* 30 diamonds. Hero: *"Please don't be loud."*
- **The Pilgrim's Lift** (`pilgrimsLift`), Floor 351 to **Floor 310**: a cable car in the open wind. *Cost (risk):*
  Floor 310 reads `shortcutTaken`: a storm, with the tolls closer together.

**Music.** Choral and cold: a men's choir, tubular bells and a low church-organ drone in D minor at 84 BPM, with the toll
as the loop's downbeat (the bell *is* the metronome). Push music: war drums and the choir in full, the toll doubled. The
Bell Ringer's theme: a cracked bell slow on the downbeat over a drone and a plainsong choir; the enrage breaks the beat
into a flurry of bells and a distorted organ.

## Circles III to I and the top of Hell: the final descent *(working)*

Designed 2026-10-25 (J: *"Same as the Circles VIII to IV atlas task but for Circles III, II and I, plus the top of Hell:
the final floors up to Floor 1, the Overlord's throne and the ending options"*), before any of it is built. Like the
middle descent, this is a **design, not content**: no entity, level, scene or sound exists yet and **every name marked
*(working)* is a placeholder** (the Gatekeeper, the Chain King and the Overlord are canon, already on the Shaft).
Everything in *Circles VIII to IV* about the shared shape of a Circle holds here too: 100 floors each, the played route
in three stretches, floor bosses, two shortcuts, one signature mechanic, a weakness for every enemy, the comedy being
Hell's, and Yooba's one line per Circle. This is Act 3 (see *Acts and story beats*): the Overlord's own guard, the gate
almost open, daylight leaking down the stairs, and then the one man at the top.

**What changes in the last three Circles**

- **Numbers.** Circle III is Floors 300-201 (Warden on 201), Circle II 200-101 (Warden on 101), Circle I 100-1. The
  Waystones at 201 and 101 light when the Gatekeeper and the Chain King fall (as 301 does for the Bell Ringer). Circle I
  has no Waystone of its own: **a death above 101 restarts the run at 101**, so the whole top of Hell, the Overlord
  included, is climbed from the Chain King's floor in one run.
- **Circle I is the Overlord's, and its played route is shorter:** stretches 100-91 (floor boss on 91), 60-51 (floor boss
  on 51) and 10-2 (the door on 2), then Floor 1, the throne. Two shortcuts join them, as below.
- **Each Circle is the one below promoted** (rule 2 of *Rules for enemies*): the Bastion's garrison are the Peaks'
  gargoyles turned into soldiers, the Keep's jailers hold what the soldiers didn't catch, and the Throne Stair's guard is
  the best of everything in a uniform. All five of **Hell's tricks** (*Hell's tricks*) finally get their reason: they are
  the Overlord's personal guard's drill. Circle I is where they appear, at their strongest, one new idea a floor. (The
  front shield also belongs to the Gatekeeper's tower shield.)
- **The comedy gets quieter as the climb gets higher.** The last Circle has the fewest jokes and the hero's driest
  lines; the funniest thing at the top is a man very tired of a chair.
- **The Overlord thread pays off.** One clue a Circle, as before, then the answer at the top (*The Overlord: who he is*).

### Circle III: the Bastion (Floors 300-201)

**Theme and look.** After the Peaks' moonlit nothing, stone made by hands: a fortress built into the shaft's wall, grey
curtain walls, towers, barracks, gatehouses, a flat iron-grey sky and banners that don't move. Torchlight, drums, the
sound of marching: the Circle is **loud and in step**, the opposite of the Peaks. The banners carry the Overlord's sign (a
crown of black horns). Every wall has a gate, every gate is shut. *(Look brief: iron-grey stone in layers, torch orange
against blue-grey sky, oxblood banners, portcullis teeth in the foreground, a drumbeat of repeated towers; the far wall
always shows the same gate, closed, a little closer each stretch.)*

**Signature mechanic: The Portcullis (`portcullis`, working).** Gates drop and rise on a drum beat: a raised portcullis is
a doorway, a dropped one is a wall (and a platform on its top). The drum is the clock: **a glint on the gate's teeth at
least 1 s before it drops**, and a gate never drops on the hero (it jams on a hero standing in it and pushes him out, one
heart). Enemies hold gates too: a Whistler's blast drops the next one. The first floor teaches it with one gate and
nothing else; later floors chain gates in the drum's order, run them out of step, and put them between the hero and an
archer's line. A rewind raises the gate that had dropped. *(Data like the Wastes' slabs; the whistle trigger is the
only new piece.)*

**Comedy angle.** The garrison marches in step, and one soldier is always a half-beat out of it and is told so, for ever,
by the one next to him (*"LEFT. LEFT. LEFT. ...YOUR OTHER LEFT."*). Archers carry a single arrow each, loose it, and go and
fetch it, together, in a queue. Every gate carries a sign: *CLOSED*, *CLOSED (STILL)*, *CLOSED (SEE OTHER GATE)*. The
garrison has never seen a fight and is thrilled. Hero, at the first drop on the beat: *"Of course it's a drum."* On the
third: *"I'm not going to get used to this."*

**The Overlord thread.** All over the Bastion, a standing order painted at every post in one huge hand: *HOLD*. Hold the
gate, hold the wall, hold the stair. The order is old: paint over paint over paint, the same word in the same hand. The
garrison don't know who wrote it; it was there when they were. Hero: *"Who tells a wall to hold?"* Announcer, quietly:
*"THEY'VE BEEN HOLDING FOR A VERY LONG TIME."*

**The Warden: the Gatekeeper** (`gatekeeper`, level `Floor201`). An armoured giant built like a gate: a portcullis visor
(the bars drop and rise over a dark face), a tower shield as tall as the hero's jump, hinges at the shoulders. He holds the
Hall Gate, the last of the Bastion, and has never opened it, because nobody has ever come with the right word, and he has
taken that very seriously. *Personality:* stern, literal, slow, dutiful; speaks in orders and repeats the end of them; the
more he is hurt, the more correct he becomes. Not cruel, only certain he is right, and quietly, desperately bored.
Hero: *"Going up."* Gatekeeper: *"Halt. ...Halt."* *Voice* `gatekeeper`: a deep iron-barrel baritone, slow and measured, a
metal clang on the stressed words, no menace in it at all. *Fight idea (working):* the arena is **the Hall Gate**, one
static screen, the great gate in the far wall under a hung portcullis, banners, a drum on a platform. Phase 1, *Halt*: he
marches the width of the floor behind his tower shield (a `frontShield`: shots from the front are spent) and slams it down
at the end of each march (a glow on the floor 1 s before, then a shockwave: jump it); the hero climbs to the gate's ledge
and shoots his back. At half health, phase 2, *The Gate Shuts*: he raises the shield overhead, lowers his visor and drops
three gates across the arena on the beat (a glint first), driving the hero into the lanes between them while archers on
the battlements fire along the lanes (a glow on each line). At a third, phase 3, *Open*: he drops the shield to free both
hands, swings a wild shieldless mace, and the drum goes on without him; the hero shoots the hinges of his shoulders.
*Weakness:* his back and the visor, open for 2 s after each slam. Beaten, he kneels, raises the bars of his visor, and says
the word he never said: *"...Pass."* The Hall Gate opens by itself, the first gate in Hell to. The Waystone at 201 lights;
Circle II wakes.

**Floor bosses** *(working)*
- **The Drill Sergeant** (`drillSergeant`, Floor 291). A huge soldier shouting the garrison through a drill, on a parade
  ground; two tiers (the yard, then the wall-walk). He calls a drill (a drum, then a rank of garrison marches in on the
  beat: each rank is a wall with one gap) and between drills blasts the whistle that drops a gate (glint first).
  *Weakness:* his whistle, shot out of his mouth; the garrison stand there confused and he has no more orders. Voice: a
  parade-ground bark that cracks: *"Aaa-TEN-tion! ...Again!"*
- **The Ram** (`batteringRam`, Floor 251). A siege ram the size of a house, carried by a team of garrison up a long ramp,
  charging the hero through the ramp's gates (a glint, then the charge). Two tiers: the ramp, then the ram stuck in the
  gate with the garrison climbing its beam. *Weakness:* the ram's head-iron, a bright band exposed when it is held at a
  gate; the garrison drop it, one by one, in a queue.

**Enemies** *(entity ids working; the played floors meet some of them again, promoted, as in Circle VIII)*

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `garrison` | Garrison | 300 | The Overlord's foot soldier, a grey stone man with a spear and a drill. Fights in step. | Marches on a fixed beat; every few steps one turns. Hit it as it turns. |
| `archer` | Archer | 285 | One arrow each, loosed along a glowing line, then goes and fetches it. | A glow on its line 1 s ahead; shot while fetching, it drops the arrow and the line. |
| `whistler` | Whistler | 280 | A garrison with a whistle that drops the next gate on the beat. | Shoot the whistle before the blast. |
| `shieldWall` | Shield Wall | 270 | Three garrison with tower shields (`frontShield`), moving as one. | Shots from the front are spent; get behind or above (a gate's top). |
| `siegeBird` | Siege Bird | 265 | A heraldic iron bird that dives on whoever crosses a gate. | Dives along a fixed line; hit it mid-dive. |
| `standardBearer` | Standard Bearer | 262 | Carries the banner the garrison keep in step to; where it goes they go. | Shoot the banner down: the rank halts, out of step, and starts again. |
| `ironHorse` | Iron Horse | 255 | A statue of a horse that charges the length of a rampart when a gate opens in front of it. | Leave the gate in front of it shut. |
| `deserter` | The Deserter | 240 | The damned of the Bastion: a soldier stuck running from the same battle, through the gates. | Can't be killed; a shot makes him trip and sit out a lap; a loop shard drops once. |
| `drillSergeant`, `batteringRam`, `gatekeeper` | the floor bosses and the Warden | 291, 251, 201 | above | above |

**Shortcuts** *(working)*
- **The Postern** (`postern`), Floor 291 to **Floor 260**: after the Sergeant's whistle falls, a small door in the wall,
  the garrison's own way out when nobody's looking, with one soldier standing at it holding it open, ashamed. *Cost:* a
  Grudge. Hero: *"After you."* The soldier doesn't move. Hero: *"Fine."*
- **The Drawbridge Chain** (`drawbridgeChain`), Floor 251 to **Floor 210**: grab the counterweight chain as the Ram falls
  and the drawbridge swings him over the next wall. *Cost (risk):* Floor 210 reads `shortcutTaken`: the whole garrison has
  been told, and the gates drop on the beat from the first second.

**Music.** A snare-and-brass march in C minor at 100 BPM, tight and mechanical, with one bar of silence every eight (the
gate drop); the loop is the same eight bars, marching in place. Push music: the march doubled, timpani and low brass
stabs. The Gatekeeper's theme: a slow processional with a tolling anvil and a men's chorus chanting one word, *"Hold."*;
the enrage cuts the chorus and drops a single great drum, a gate closing, on every bar.

### Circle II: the Keep (Floors 200-101)

**Theme and look.** Under the fortress, the place the fortress keeps what it takes: a courtyard, a hall of cells, a deep
dungeon, torches that light the stone but never the corners. Iron: bars, rings, chains hanging from every ceiling and
swinging a little when nothing moves them. Damp, close, orange. *(Look brief: dark wet stone in warm torch-orange, hanging
chains at two depths, iron doors in rows, a wall of cell numbers; the cells get fewer and bigger as the Circle rises.)*

**Signature mechanic: The Tether (`tether`, working).** A jailer's chain clips to the hero and ties him to a ring in the
floor for a few seconds, with a maximum range: he can still move, jump and shoot, but no farther than the chain's length
from its ring (the taut line shows the slack and glows on a short chain). A tether is **broken** by shooting its ring or a
link (a link takes two shots), or by killing the jailer; a rewind unclips it. Chains also **swing** as hazards (a ball and
chain on a beat) and **lift** as platforms (a taut chain is a rail, a loose one a rope). The first floor has one chain and
one ring and no jailer; later floors have jailers who clip him at the worst moment. A tether is never a trap: the ring is
always shootable from inside its range. *(A range limit on the hero: new C#, rewind-safe, like the Caverns' Gulp.)*

**Comedy angle.** The Keep is run by people who love their keys. A jailer's ring has two hundred keys; he knows which by
touch and is never right. Every key opens the same cell. A wall counts the cells: *CELL 4, CELL 4, CELL 4*. Prisoners (the
damned) call out their names, always the same one: *"I'm Dennis!"* from every cell. A sign, scratched through and written
in again: *PLEASE DO NOT FEED THE PRISONERS*. Hero: *"If I'd known Hell had a basement..."* A floor later: *"There's a
basement under the basement."*

**The Overlord thread.** On the Keep's lowest floors there is one very large **empty cell**, the biggest in the dungeon,
its door open, the lock worn smooth from the inside. A chain on its wall has pulled a groove through its ring: one
prisoner's whole life of pulling. On the wall, scratched with something that wasn't made for it: four small drawings, a
hill, a hut, a field, a door. Hero: *"Whoever he was, he got out."* The cell number is **1**.

**The Warden: the Chain King** (`chainKing`, level `Floor101`). A jailer king wrapped in chains, an iron spike crown, a
belt of keys the size of the hero's head. He keeps every prisoner in Hell and has lost exactly one, which he does not
discuss, and the loss has made him a *collector* who counts what he has, again and again. *Personality:* possessive,
proud, soft-spoken, deeply attached; tender with the damned in a way that is horrible. He doesn't hate the hero: he wants
to **keep** him (*"Oh, you're new. You'll like it here. Everyone does."*) and is genuinely hurt when refused. Counts aloud.
*Voice* `chainKing`: a rich, low, warm voice, slow and affectionate, with a chain's dry rattle in every pause; never
shouts; the one raised line is when he hears his first prisoner got out. *Fight idea (working):* the arena is **the
Counting Hall**, one static screen, a high dungeon with cells in a ring and chains hanging in the middle. Phase 1,
*Keeping*: he tethers the hero (a ring in the floor, a glow first) and sweeps his chain-flail across the floor (a line of
glow before each sweep); the hero breaks the ring and a link of the flail, then shoots the key on his belt that opens a
cell and lets out a Convict who runs across the hall (harmless, but it draws his attention). At half health, phase 2,
*Collecting*: he opens the cells and **the damned come out for the count**, walking a line across the floor on a beat (they
hurt only if you stand in their line: the Shades' rule), while chains lower cages over spots (a glow, 1 s) that hold the
hero if he stays. At a third, phase 3, *Mine*: he drops the keys and takes the flail in both hands; every chain in the
hall comes alive and tethers the hero at once, and he can move only by breaking them in turn (a rewind re-clips).
*Weakness:* the spikes of his crown, one knocked loose per cage dropped; with all the spikes out he can't tether. Beaten,
he sits among his cells and counts, gets it right for the first time, and lets the last cell open: *"...One gone. One
fewer. ...One."* The Waystone at 101 lights; Circle I wakes. Hero: *"Mine was a hole in a field."*

**Floor bosses** *(working)*
- **The Turnkey** (`turnkey`, Floor 191). A huge jailer with a wing's keys on a ring the size of a wheel, which he whirls as
  a flail (a glow on the floor, then the sweep). Two tiers: the courtyard, then the cell row, where he opens cells to send
  Convicts at the hero. *Weakness:* the ring: each key shot off makes the flail shorter, until it is a keyring in his hand.
  He can't pick the right one: *"It's one of these."*
- **The Oubliette Keeper** (`oublietteKeeper`, Floor 151). A pale, long-armed jailer who keeps the pit in the middle of the
  dungeon floor and drops the floor on a beat (a glow under the hero's feet, 1.2 s, then the trapdoor). Two tiers: the
  dungeon, then the pit's rim with the damned climbing the sides. *Weakness:* the lantern on his pole, the only light in the
  pit; shot, he can't see where his floor is.

**Enemies** *(entity ids working)*

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `jailer` | Jailer | 200 | A guard who tethers the hero and drags him by the chain. | The ring in the floor; shot, the tether snaps and he staggers. |
| `ballChain` | Ball and Chain | 190 | A prisoner's iron ball, rolling on a long chain on a fixed beat. | The chain's anchor; shot, the ball stops where it is. |
| `keyBat` | Key Bat | 180 | A bat carrying a key that opens or locks a door in front of the hero. | Shot, it drops the key; a dropped key holds its door open for 6 s. |
| `convict` | Convict | 175 | The damned: a prisoner stuck running the same stretch between two cells. | Can't be killed; a shot makes him sit out a lap; a loop shard drops once. |
| `dungeonHound` | Dungeon Hound | 165 | A chained hound that lunges to the end of its chain and no farther. | Out of its reach it is harmless; the collar is the weak point. |
| `manacleHand` | Manacle Hand | 160 | A hand reaching out of a wall to clamp on a passer-by. | Its wrist-cuff glints before it grabs; shot, it opens. |
| `cageDrop` | Cage Drop | 155 | A cage on a chain that falls on whoever stands under it. | A glow on the floor 1.2 s before; cut the chain and the empty cage becomes a platform. |
| `spikeRoller` | Spike Roller | 140 | A drum of spikes that rolls down a corridor on a rail. | Jam its axle with a shot and it stops. |
| `turnkey`, `oublietteKeeper`, `chainKing` | the floor bosses and the Warden | 191, 151, 101 | above | above |

**Shortcuts** *(working)*
- **The Dumbwaiter** (`dumbwaiter`), Floor 191 to **Floor 160**: a food lift from the Turnkey's wing up to the kitchens,
  built for plates. *Cost:* 40 diamonds (*"It wants paying in something shiny"*). Hero: *"I'm riding with the plates?"*
- **The Escape Tunnel** (`escapeTunnel`), Floor 151 to **Floor 110**: a hole in the oubliette's wall someone dug with a
  spoon, ages ago, leading up. *Cost (risk):* Floor 110 reads `shortcutTaken`: the tunnel comes out in the Keeper's own
  cell, the jailers have been woken, and they tether him from the first second.

**Music.** Slow and clanking: anvil hits and chain-rattle percussion, a harpsichord and a low string drone in F-sharp
minor at 88 BPM; the loop is one figure played slower each time a key turns, then reset (a lock opening). Push music:
the chain percussion doubled under distorted organ. The Chain King's theme: a music-box lullaby on a pipe organ with a dry
rattle, in 6/8; the enrage plays the lullaby faster until it breaks, then hammers it out of tune.

### Circle I: the Throne Stair (Floors 100-1)

**Theme and look.** The Overlord's palace, which is mostly a staircase: one very long obsidian stair, up and up, black
glass and red inlay, torches that burn the same height, banners in his sign, no windows until the top. Every imp in Hell has
been carrying the village's diamonds here, on lifts and chains and belts, to a **hopper** at the stair's head that feeds the
gate. The muck of the deeper Circles is gone; this one is clean, quiet and expensive. And at the top, daylight **leaks
down**: one pale seam under a door on Floor 60, a stripe of sky on the wall at 30, grass dust drifting on the draught, then,
on Floor 2, a white door. *(Look brief: black glass and red inlay, long perspectives of stairs and balustrades, columns in
layers, the glow of the hopper far above, and on the last floors a grey-gold light and wind, the one new colour in the
game. The meadow's green is saved for the very top.)*

**Signature mechanic: The Draw (`gateDraw`, working).** The half-open gate is **drawing the air of Hell up the stair
toward the surface**. A draught blows up the Circle: it carries loose things (embers, ash, banners, the hero's shots)
upward, makes his jump longer and floatier, and slows him walking against it (on floors where it blows sideways). Gusts
come on a beat (a banner snaps one beat before), and **shafts of daylight** light the stair in lanes: in a daylight lane
the Overlord's guard flinch and their shots curve with the draught. The first floor teaches the draught alone, a hall
with a gust and a few embers; later floors turn it sideways, set gusts against jumps, and use it to carry the guard's
shots. A rewind runs the gusts back. *(C#: a rewind-safe force on the hero and projectiles, like the Caverns' Gulp.)*

**Comedy angle.** Almost none; the Circle is the end of the joke. What is left is the guard, who stand at attention under
banners the draught has blown into their faces for ages, and are very polite when they shoot you. A sign at the foot of
the stair: *PLEASE KEEP TO THE LEFT OF THE STAIR (THE LEFT HAS BEEN REMOVED)*. On Floor 13 the announcer, with real feeling:
*"FLOOR THIRTEEN! UNLUCKY FOR SOMEONE!"* The hero, a floor later, at a patch of daylight: *"...Is that grass?"* The
announcer, off the beat for the only time: *"...Don't."*

**The Overlord thread.** The footprints. The stair's treads are worn in the middle by one pair of feet, the same pair on
every tread, an enormous stride, and from Floor 50 on, beside them, a second line: a man's. At the foot of the stair, set
against the wall like something put down on the way past, the rusted barrel of an old rocket launcher. On the arm of the
throne at the top (it faces away from the room), a **visor**, small, rusted, its two eye-slits the exact cut of the hero's own.
Hero: *"...Huh."* (See *The Overlord: who he is*.)

**The floor guard** *(the Overlord's personal guard, entity ids working)*. Everything at its strongest, one new idea a
floor, four enemies at a time at most, a new one alone first. They are **obsidian**: the Overlord's own stone, carved by
the Circle's one sculptor, who also made the throne.

| Entity | Name | First met | Why it's there | Weakness / how it plays |
|---|---|---|---|---|
| `obsidianGuard` | Obsidian Guard | 100 | The Overlord's elite: obsidian plate and a spear. Fights in pairs, one a half-step behind the other. | A seam of red at the neck; shoot from either side. |
| `gateImp` | Gate Imp | 95 | A sack imp promoted (rule 2): a fuel-bearer in a sash, carrying one of the village's sacks up the stair to the hopper. | Cut the lift's belt and it drops the sack (diamonds). |
| `lensSentry` | Lens Sentry | 93 | A column with the Overlord's lens at its head (`laserSweep`). | Break the lens; the column is harmless. |
| `ricochetGuard` | Ricochet Guard | 60 | Hell's trick, promoted: its shot bounces off the stair three times. | The ember arcs low: wait for the bounce. |
| `blinkGuard` | Blink Guard | 57 | Hell's trick: it slips through the loop and is next to you. | A violet mark 0.7 s first; shoot where it stands. |
| `bulwarkGuard` | Bulwark Guard | 54 | Hell's trick: an obsidian plate held in front (`frontShield`). | From behind or above, with the draught's help. |
| `leadGuard` | Lead Guard | 52 | Hell's trick: it has watched you run for ninety floors. | Stand still; shoot from a daylight lane, where it misjudges the curve. |
| `echoGuard` | Echo Guard | 9 | The Afterimage promoted: a pale copy of the hero's last three seconds. | Lead it into a lens; a rewind erases it. |
| `guardCaptain`, `gateSmith`, `overlord` | the floor bosses and the Overlord | 91, 51, 1 | above | above |

**Floor bosses** *(working)*
- **The Captain of the Guard** (`guardCaptain`, Floor 91). A tall obsidian knight in a red cloak who calls the guard's drill
  from a balcony; two tiers (the stair hall, then the balcony, with the draught blowing his cloak). He fights with the guard's
  tricks in order, each telegraphed: ricochet, blink, front shield. *Weakness:* the cloak: the draught blows it across his
  visor and he fights blind for 3 s, which is when to shoot. Voice: crisp, low, courteous, a little sorry: *"Forgive me. It
  is the drill."*
- **The Gate Smith** (`gateSmith`, Floor 51). The sculptor who carves the guard and the gate: a stooped obsidian craftsman
  at a half-built forge, hammering a door that never gets finished. Two tiers (the forge, then the unfinished gate itself,
  lit from within): hammered sparks run along the floor (a glow first) and guards are struck out of the stone (a glow on the
  anvil 1.2 s before each stands). *Weakness:* the rivets of the unfinished gate: shoot them all out and the door falls and he
  looks at it. He doesn't speak to the hero, only, softly, to the door: *"Nearly. Nearly."* A voice of wet stone and tiny
  hammering.

**Shortcuts** *(working)*
- **The Fuel Line** (`fuelLine`), Floor 91 to **Floor 60**: the belt that carries the village's diamonds up the stair to the
  hopper; ride it, standing on a sack. *Cost:* a Grudge. Hero: *"Riding our own diamonds. Again."* Announcer: *"HE'S ON THE
  FUEL LINE! THE LINE HAS NO COMMENT!"*
- **The Daylight Draught** (`daylightDraught`), Floor 51 to **Floor 10**: a crack in the stair's wall where the draught
  is strong enough to carry a man. *Cost (risk):* Floor 10 reads `shortcutTaken`: the Overlord, for the first time, turns
  his head (a huge red-eyed profile in the window above, 2 s) and the guard are on the floor from the first second. The
  announcer says why. Hero: *"Please don't look."*

**Music.** A grand, clean, low orchestra in D minor at 76 BPM, slow for the first time: a theme assembled from the earlier
Circles' motifs one by one (the Wastes' groove as the bass line; the Road's riff, the Bridges' arpeggio, the Gloomwood's
lone guitar bar, the Mire's piano, the Maw's heartbeat, the Peaks' bell, the Bastion's march, the Keep's lullaby), so the
player hears the whole climb played back as he climbs it. The loop is the same sixteen bars, a little brighter every ten
floors, the key rising a semitone each time, with a single sustained major chord entering at Floor 13 and growing. Push
music: the full orchestra and distorted guitars on the same tune.

### The Overlord: who he is *(working)*

This is the answer the Act 2 and Act 3 clues have been circling, and it is **one decision for J** (see the summary): the
Overlord is **the one who climbed first**. A long time ago, before there were 1,000 floors, a man in a visor, tired of
everything, fell down a hole. He climbed. There was no Shaft, no counter, no Waystones, no Yooba; there was only up, and Hell,
which was not yet very large, noticed, and made itself bigger above him floor by floor to meet him as he went (that is why it
has 1,000: it is the length of one climb). He reached the top, and the top was a door in the ground, the **gate**, shut, with
the meadow's light under it, that opens only from above, or from below with the village's **diamonds** burning in its lock.
So he climbed the whole of it again, and again (**the loop was his first**: he found Déjà Vu on the way and dragged it
backward, and learned what the hero will, that rewinding undoes nothing and only gets you another go), and somewhere
between the hundredth climb and the thousandth he stopped being a man and became the thing the Circles had been making, the
obsidian titan, **ruler of a place he was only trying to leave**. The imps are his errand; the village is the only fuel
that opens the gate; the portal he lasers open in Video1 is his way of fishing. *"Mm. ... Stay."* was a man seeing, for
the first time in an age, another one fall down the hole, and telling him, as one does, not to try it.

- **What he wants** is what the hero wants: to go home. He has just done it for much longer, and gone strange with it.
  He no longer knows whether the surface is a place or a habit. *Taking* it is what the plan has become.
- **Why he barely notices the hero:** he has seen ten thousand souls on their way down; this one is going the wrong way.
- **The canon is unchanged:** he is Hell's ruler, enormous, obsidian, red-eyed, crowned in black horns, slow, says very
  little, and his voice fills a valley. Every clue fits: *UP* (VIII), the footprint lane (VII), the drowned statue (VI),
  the Way Home's door at Floor 500 (V: the same daylight under it), the countdown candle with his face (IV: the bell
  counts toward his gate), *HOLD* (III), Cell 1 and its four drawings (II), the man's footprints and the visor (I).
  Nothing in the cast table is retold.
- **The Announcer** never lets on. He is Hell's voice and knows exactly who the Overlord was, and says nothing, because it
  would spoil the show. In the true ending he says it, once.

### Floor 1: the Overlord (the final boss)

**Floor 1, *The Throne*** (`levels/Floor001.json`, working; the last level), through the white door on Floor 2. One static
screen (the Warden shape): a round gate-room at the top of the stair, the **half-built gate** filling the far wall (an
enormous ring of black glass, two thirds shut, a seam of the meadow's light along its unfinished top), the **hopper** above
it with the village's diamonds burning down a chute into the gate's lock, and before the gate the **throne**, which faces
the gate with its back to the room. The Overlord sits in it. He doesn't turn. He is bigger than the Magma King: seated, he
fills the top of the screen, and the screen shows his back, his crowned head over the throne, one huge hand on its arm.

Every attack is telegraphed a second or more ahead, on the spot it will hit, as with every boss. He has **three phases**, as
the Acts table says, and the first two are about *attention*.

1. **Intro** (input and rewind off, the hero can't be hurt). The title (*"FLOOR ONE! THE OVERLORD!"*). The room is silent,
   then the gate hums. The hero walks in. Overlord, not turning: *"Mm. ... You."* A pause. *"Stay."* Announcer, low: *"HE DID
   NOT TURN AROUND."* The boss bar fills.
2. **Phase 1: *Not Worth It*.** He fights without turning, with one hand: his **shoulder lenses** (`laserSweep`: a thin red
   line, then the beam sweeps), a slap of the hand along the floor (a glowing strip, then a shockwave: jump it), and the
   guard dropping in from above (never more than two). Nothing he does is quick, and none of it is meant to kill. *The hero
   cannot hurt him from here:* his back is armour. To make him turn, the hero shoots **the hopper's straps**: three straps hold
   the chute, and each one cut drops a little fuel and turns his head (a red eye in the dark) for 3 s, the only time he can
   be hurt. Three straps, three turns. Overlord: *"...Don't."* (to half health)
3. **Phase 2: *The Loop Was His*.** He **rewinds**. The screen desaturates for 1 s (the tell: a hum dropping), then **the last
   three seconds of the fight run backward**: the hero's shots return to the gun, his position is pulled back, and *the
   Overlord's attacks replay* over the hero's old position. The hero has Déjà Vu too: **holding R in the tell's second
   cancels his** (a rewind answering a rewind: the screen holds, tears, and settles on the hero's side). A rewind he doesn't
   answer repeats an attack the hero already dodged, so the tell is a trap *and* a chance. The lenses fire where the hero was;
   the guard come back from where they died. Overlord, flat: *"I know every second."* In this phase he stands.
4. **Phase 3: *The Gate Cracks*** (a third of health). The gate splits and **daylight pours in**: a white column from the seam
   down the room, the real meadow's light, a smell of grass in the draught, which pulls everything toward the crack. He
   **stops rewinding** (the light won't let the loop close) and his attacks become slow, huge and terrible: both hands slam (a
   glow the width of the floor, 1.5 s: stand in the lane of daylight, the only safe place), the crown's black horns shed and
   spear the floor (a glow under each), and the fuel pours down in a burning stream (shoot the lock it runs into). The hero
   fights **in the light**, and for the first time the music loses its minor chord. *Weakness:* the red eyes (open now
   whenever he looks at the hero, which is always) and the crown's horns, shed one at a time.
5. **Defeat.** Slow motion. His last word isn't for the announcer. He sinks to one knee, one hand on the floor; the beams
   die; the room goes quiet. The gate is still open, the fuel still burning, the light on his back. He looks at the visor on
   the throne's arm. *The fight is over; the choice isn't* (the ending, next).

**Runs.** A death in the fight restarts at the Waystone at 101: Floors 100-2 are climbed again, minus any shortcut taken
that run. The usual rewind works throughout; phase 2's tell is the one place it matters twice.

**The Announcer** calls it like a cabinet that knows it is about to lose its star: *"FLOOR ONE! THE OVERLORD!"*, *"HE DID NOT
TURN AROUND."* (intro); *"HE'S TURNING! HE'S... LOOKING!"* (each turn); *"HE'S REWINDING?! THAT'S... THAT'S YOURS!"* (phase
2's tell); *"THE GATE! IT'S OPENING! IT'S... DAYLIGHT?!"* (phase 3); *"THE OVERLORD HAS FALLEN. ...HELL WILL NEED A
MOMENT."* (the defeat).

### The ending: what happens at the gate *(working)*

After the Overlord falls the hero is alone in the gate-room with a kneeling titan, an open gate drawing the room up toward
a meadow, and the fuel still burning. A short scene (input and rewind off: something bigger has hold of the loop) takes him
to the throne's arm, where the old visor lies. He picks it up, looks at it, and looks at his own. Overlord, very quietly,
in the voice of a man much smaller than the one who fought: *"...Home. ... Was it nice?"* Hero: *"It was a nice
afternoon."* (Video1's words, and the only speech he gives.) Then the **choice**, two cards like a Grudge pick, final (Déjà Vu
can't undo it, for the last time):

| Card | What it is | Ending |
|---|---|---|
| **SHUT IT** *(first card)* | Close the gate. | **The true ending: "Last Time"** |
| **SIT DOWN** *(the other card; the joke)* | Take the chair. | **The funny ending: "Encore"** |

Two more options were designed and cut, kept here so no one invents them again: **TAKE THE FUEL** (the hero pockets the
gate's diamonds and walks out through the open gate, leaving it open: Hell would reach the surface, which the pitch rules
out) and **LET HIM OUT** (he opens the gate for the Overlord: a forgiveness, and a threat to the village; it is the ending
a sequel would need, which is why the true ending's knock stays).

#### True ending: "Last Time"

*In order: the portal fuel, the loop, rewind's meaning and Yooba's role.*

1. **The fuel.** The gate is held open by the village's diamonds burning in its lock. A shove won't shut it. The hero
   **rewinds the gate.** For this one scene the meter holds everything he has saved (a bar the length of every loop shard he
   took from the damned) and he holds R; the gate's own loop runs backward, the burning diamonds leap out of the lock and
   up the chute, out of the hopper and back along the belts, the imps' whole hauling run **played in reverse** over the
   climb (the Throne Stair, the Pile, the Depot, the sacks flying home the way they came), a streak of light up the shaft
   and through the crack into the meadow. *The village's diamonds go home: not burned, not kept, not sold, but returned,
   by the one trick that returns things.*
2. **The shove.** The gate is shutting with the Overlord kneeling in its door, half in the light, half out. He doesn't resist.
   The hero puts a hand on his shoulder (the size of a cart) and shoves him back off the threshold onto Hell's side. Hero:
   *"Stay."* (The Overlord's own word, now a mercy, because the surface is not where he can go.) The gate closes on the
   titan, and the Shaft's plunge runs for a moment in reverse, floors flashing past, down, the counter running 1, 2, 3...
   The room goes dark and quiet. The visor is gone from the throne's arm: he took it with him.
3. **The loop.** The loop **breaks**: the Waystones go dark one by one on the Shaft's wall, because there is nothing left to
   restart. A death in Hell now just ends. *Rewind's meaning:* the Overlord used it to look back, and the hero used it to put
   something right. **Déjà Vu is repetition with a choice: again, on purpose.** Hell's loop was a punishment because it
   had none. The announcer says so once, the only time he speaks without a show: *"HE WAS A HERO ONCE, TOO. ...WE DON'T TALK
   ABOUT IT."*
4. **Yooba.** The hero climbs through the closing light to **the Meadow**. It is not Hell, so the rule that Yooba never appears
   in a stage doesn't apply (this is a scene, not a floor). He is at the portal's old scorch mark on the grass with the
   diamonds the gate sent back in a heap, and he is **coughing up the ones he swallowed**, one by one, a very long line.
   Yooba: *"I kept them SAFE. Mostly in my— anyway. They're warm."* The neighbours come out to look. His last line is a
   goods list: *"I have a Goo Plaster for that, if you want one."* Hero: *"No."* Yooba: *"It's free."* Hero: *"...Fine."* (The
   shop's first good, the last.)
5. **Home.** The meadow is regrowing, the hut is a pile of straw, the neighbours' huts are fine. The hero sits down on the
   ground for the first time in 1,000 floors. The announcer is gone. Something **knocks** from under the ground: three knocks,
   a pause, one more, politely. The hero doesn't get up. The orange of his visor's eyes lights, and goes out again. Fade.
   **Post-credit: "FLOOR 1,001"** (the canonical sting): the Landing's lava, still, and below it in the dark the white outline
   of a door, knocking, and behind it a man's voice, hoarse and tired and almost cheerful: *"...Hello?"* (the hero's own word at
   the portal, answered at last).

The true ending **keeps the Acts table**: the hero shoves the Overlord back down, the gate shuts on him, Yooba spits the
diamonds out, the hero sits for the first time, something knocks, and Floor 1,001 is the sting.

#### Funny ending: "Encore"

The hero, over the kneeling Overlord, looks at the throne, the only chair in 1,000 floors, and picks **SIT DOWN**.

1. **Sitting.** He walks to the throne and sits in it, heavily, with a long breath out. Announcer, after a beat: *"...HE'S
   SITTING. THE CROWD DOESN'T KNOW WHAT TO DO."* Overlord, looking up from the floor, very slowly: *"Mm. ... That's my chair."*
   Hero: *"Was."*
2. **The show stalls.** Hell has no rule for a hero who sits. The announcer tries to call it (*"WHAT A... MOVE?"*) and gives up.
   The gate, with nobody fighting, nobody feeding it and nobody watching, **shuts by itself out of sheer boredom**; the
   diamonds go dark in the lock and drop, with a clink, into a pile at its foot.
3. **The queue.** One by one up the stair the Wardens arrive and sit: the Magma King on a slab of slag, the Spire Fiend on a
   step (listening), the Bridge Wraith, apologising, the Hollow King, the Drowned Queen, the Mother Maw (small and shy, this
   once), the Bell Ringer, the Gatekeeper (who sits down slowly, *correctly*), the Chain King (who counts them), the Imp Foreman on
   his sack. Nobody speaks. They are all, it turns out, as sick of Hell as he is. The Overlord sits on the floor against the
   throne's leg with the visor in his lap. Yooba, in a corner, coughs up a diamond into each of their laps.
4. **Yooba's role** is the joke's last step: he has swallowed **all** the gate's diamonds again, to keep them safe, and refuses
   to give them back. Yooba: *"SHOP'S OPEN."* The credits roll over everyone sitting in the daylight, nobody talking, in one
   long take.
5. **The loop.** Because Hell is repetition and nobody got out, the credits end on a **cut to the Landing**: Floor 1,000, the
   lava, the portal's flash, and the announcer, rested, at full volume: *"WELCOME TO THE BOTTOM. NINE HUNDRED NINETY-NINE TO
   GO."* Hero, off-screen: *"...Great."* (the game's first lines, repeated). The save starts a **New Game+** with the
   Waystones and Yooba's goods kept. *Rewind's meaning here:* the hero took a seat and the loop didn't care; it started
   again anyway.

The funny ending has **no knock and no Floor 1,001**: it ends on the loop. Both endings keep the tone: deadpan hero, booming
announcer, no office jokes. Its gag is the hero sitting down, which the true ending ends on too, in grass.

#### Epilogue video idea: "Knock" *(working; not made)*

About **50 seconds**, painted like the portal scene, near-silent; it plays after the true ending's credits (the funny ending
has its stinger instead). It is the true ending's last beat as a film, so it shows only what the Meadow shows: no Hell, no
Overlord on screen.

| # | Shot | Seconds | Beat |
|---|---|---|---|
| 1 | The meadow at dawn, ash on the grass; a time-lapse as the grass comes up in a green wave over the scorch mark. | 6 | The village after, healing. |
| 2 | The neighbours' huts and new thatch going onto the hero's, a row of hands. Diamonds in a heap on the fence, glinting. | 6 | The fuel is home. |
| 3 | The hero in his armour, sitting on the grass with his back to a fence post, eyes shut, visor up. Yooba beside him, wobbling, asleep. | 5 | Nobody asks him for anything. |
| 4 | A close-up of a blade of grass, then a pebble beside it, trembling. | 4 | |
| 5 | Three knocks from under the ground, a pause, a fourth, polite. Nobody moves. | 6 | The hook. |
| 6 | The hero opens one eye. Orange glows in the visor. He looks at the ground for a long beat. | 5 | |
| 7 | He closes the eye. The orange goes out. Yooba, asleep, burps a diamond. | 5 | His one murmured line: *"Not today."* |
| 8 | Cut to black. A white outline of a door, drawn in the dark, and a hand in the door, knocking. Title: FLOOR 1,001. | 6 | The sequel hook. |
| 9 | Black. A small, tired voice behind the door: *"...Hello?"* | 3 | The echo, answered. |

Notes: the meadow is the game's own (Level 1's footage, the website's `QuietVillageAfter`); the knock and the *"Hello?"* are
voice and sound lines (cents); marketing may not use it (the website carries no finale spoilers, and teasers never go past
the prologue). Priced in LevelDesign.md section 8.

### Yooba on the Shaft: Circles III to I

One reaction per Circle (the moment/floor model of *Yooba between floors*, in his voice, said once per save; working lines):

- **III (the Bastion, Floor 300):** *"They've all got the same face! I counted. Three hundred and eight. I can do the face,
  look."* Hero: *"Don't."*
- **II (the Keep, Floor 200):** *"There's a lot of chains. I'm not scared of chains. Anything with that many gaps is
  basically fine for a slime."*
- **I (the Throne Stair, Floor 100):** *"That's the way home. I can smell it. It smells like hay. And a little like us."* Hero:
  *"Yeah."*
- **The ending:** above. His shop gets no new goods (the list is final), only one new line at Floor 101: he gives the hero a
  free *Last Plaster*, a callback to the first thing he ever sold.

### The shape of the climb, from the top

```
FLOOR 300  THE BASTION       the drum, the gates, HOLD               Warden: the Gatekeeper (201)  "...Pass."
FLOOR 200  THE KEEP          chains, keys, an empty Cell 1           Warden: the Chain King (101)  "...One."
FLOOR 100  THE THRONE STAIR  the draw, the guard, daylight leaking   Floor 91: Captain, Floor 51: Smith
FLOOR 2    THE DOOR          a white door, grass dust
FLOOR 1    THE THRONE        the Overlord, three phases, the choice  SHUT IT / SIT DOWN
FLOOR 0    THE MEADOW        the diamonds home, Yooba, a knock       true: "Last Time" / funny: "Encore"
```

### Yooba between floors

Yooba rides along in the gaps: on the Shaft between stages and behind the shop counter. He never fights and never
appears inside a stage; he is the one friendly voice in Hell, and he talks far too much. He is drawn as a **talking
portrait** (`ui/yooba.json`), made from Video3's own slime: the green blob with two eyestalks and big pink square
teeth. Four loops: **idle** (a slow wobble), **talk**, **laugh**, and **cough** (he hacks up a diamond, the one trick he
is proud of). His voice is `yooba` (designed for him: fast, wet, gurgling, a bit too pleased with himself).

- **Hello** (the Shaft after Video3, at Floor 999, before Floor 999 is climbed): he says hello, repeats the announcer's
  *"nine hundred ninety-nine"* back at the hero (he counted), and coughs up his first diamond. The hero answers with
  one line. From then on Yooba is "met" (New Game forgets it).
- **Between floors** (every Shaft climb after that): one line, picked by what just happened, first match wins: the
  climb passed a milestone floor (999, 950, the Spill's 998 with the baker's cart), the Magma King is close (within
  30 floors; at his door, a whisper), the hero died on the way, a new Grudge was picked, the hero walked past a
  diamond glinting off the path (he can smell his village's diamonds from the Shaft: *"You left one on nine-ninety-six.
  I can smell it from here."*; LevelDesign.md, *Glimpses, glints and the damned*); else idle gossip. Each
  reaction is said once per save, then the next one up gets its turn. Some get a one-line reply from the hero.
- **The shop** is Yooba's counter, **Yooba's Goods**: he welcomes the hero in, comments on a buy (often coughing it
  up, still warm), turns him away when he can't afford it (*no tabs, no pockets*), and says goodbye when he leaves.
  It is reached from every Shaft after his hello (**Visit Yooba**, beside Continue: the shop opens over the map, and
  leaving it puts the hero back on the Shaft, Continue on to the next stage) and from the main menu. Paid in the
  village's diamonds; what he makes is kept (New Game forgets it, like everything else).
- **His goods** are things a village slime can make out of diamonds and his own stomach, each with a plain effect:

  | Goods | Effect | What it is |
  |---|---|---|
  | Goo Plaster | +2 max health per level, 10 levels (5 hearts grow to 25) | a dab of Yooba over the dents, ten times |
  | Bouncy Soles | an extra jump in the air | slime in the boots |
  | Slick Heels | runs 8% faster | slime on the heels |
  | Licked Trigger | fires 10% faster | he licked it |
  | Tummy-Dipped Shots | 15% more damage | every shot dipped in his stomach |
  | Loop Jar | +2 s of Déjà Vu (rewind) per level, 5 levels (the hero starts with 10 s) | a loop shard one of the damned dropped; he only keeps it in a jar |
  | Bottomless Jar *(gated)* | +2 s more per level, 2 levels (needs Loop Jar 3) | Yooba's stomach, but for loops |
  | Quick Unwind | rewinds 25% faster per level, 3 levels | Yooba greased the loop |
  | Shiny Sniffer | diamonds come from farther away | a wobbly bit of Yooba that sniffs out diamonds |
  | Double-Dipped Shots *(gated)* | a further 20% more damage | he only offers it to someone who has had the first dip twice (*Tummy-Dipped Shots*, level 2); back in the stomach they go |
  | Slimed Knuckles *(gated)* | punches (fists) hit 50% harder per level, 2 levels | a coat of slime on both fists |
  | Stretchy Wraps *(gated)* | punches reach 15% farther per level, 2 levels | wraps of stretched slime |
  | Keep Weapons *(gated)* | the weapons he holds survive a run's end | the hero will not put them down |
- **The shop is alive** (2026-10-25; `ui/yooba.json`, `YoobaShelf`, `UpgradeShopManager`). Yooba's counter is a room, and it changes as the hero climbs:
  - **The shelf.** Behind the counter stands a shelf of ten slots. It starts with one jar (*Spare Goo*) and nine dusty gaps. Each time the hero clears a
    floor Yooba cares about, a jar from that floor turns up: a Licked Rock (Floor 999), a Cart Wheel off the baker's cart (998), a Loop Pebble (995),
    a Sack Scrap (991, the Foreman's), a Pile Crumb (951), a Soot Tin from the King's Chimney (910), a jar of Magma King (901) and a Spire Splinter (Circle IX).
    They are not for sale; they are trophies, and he points each new one out once (`ShopStocked`). The tenth is the Thank-You Jar, for the quest below.
  - **He reacts.** To what the hero buys (a line made for each good: the plaster, the soles, the licked trigger...), and to his deaths: Yooba keeps count and
    says something at 1, 5, 15 and 40 (`ShopDeaths`; the line for the highest count reached, the lower ones are old news). The welcome also changes after the Foreman, the Chimney and the Magma King.
  - **Running gags across Circles**, said to himself when the shop is quiet (`ShopIdle`): the *lick count* (three rocks, then forty, then two hundred and the Magma King's throne tastes of gravy), the
    *baker's daughter and the blacksmith* (a secret, then a letter, then engaged), *he still has no pockets*, and his humming. A new stage of a gag turns up once the hero has climbed far enough.
  - **Idle life.** While he is quiet Yooba is never still: he blinks, looks about, hops, hums and sniffs on his own every few seconds.
  - **The Lost Things** (a short quest). Once Floor 999 is cleared Yooba asks, in a whisper, for three things that fell out of the village when the portal took them:
    **the baker's rolling pin** (Floor 997), **the blacksmith's mitt** (Floor 995) and **the bell's clapper** (Floor 992). Each lies on a ledge off the climb (`lostRollingPin`,
    `lostMitt`, `lostBell`; touching one writes the flag `lost.pin`/`lost.mitt`/`lost.bell`, kept for good). On the next visit Yooba has a line for each find, a
    ledger chip (LOST THINGS 1/3) counts them, and when all three are in he pays **60 diamonds once** and adds the Thank-You Jar to the shelf. Never an office joke: they are a village's things,
    and Yooba is a gossip who wants his village's lost-and-found back.
  New lines: 40 Yooba lines and 6 hero replies as `audio.json` voice prompts (ElevenLabs, ~0.78 USD to make; the clips are not made yet, so they show as subtitles with the line's timing until the Sound Generator runs).
  Not built: the shelf and idle fidgets are drawn from menu shapes (a painted shelf and idle clips would need GPU art; priced in the pull request); the shop room on the Shaft's Visit Yooba overlay is the same screen.
- **Weapon goods** (2026-10-25). Yooba does not make weapons and has never seen one: the fists (and whatever comes next) are
  Hell's business, not his. The three weapon goods have `requires: ["weapon:fists"]`, so they are not on the counter until the
  hero has held the fists once (`PlayerWeapons.Give` writes the unlock flag `weapon:<id>`; it outlives runs, New Game forgets it).
  *Keep Weapons* is the one thing that changes the run rule: a death normally clears his weapons with the Grudges
  (`WeaponProgress`); with it bought they stay in his hands (and the equipped one stays equipped). He pays for these goods
  like any other, but Yooba hasn't a clue what he sold: buying a weapon good makes him **confused and then offended**, and he
  accuses the hero of stealing from his stomach (`ShopBuyWeapon` lines: *"I don't make fists. I don't HAVE fists. You've been in
  my tummy, haven't you? THIEF!"*). He still takes the diamonds, and the good still works. Never an office joke; he is a gossip
  who is sure the world is rummaging in him.
- **Goods that aren't on the counter yet** (2026-10-25): a good can carry a `requires` (`player.json`): until the hero
  has what Yooba wants to see (a weapon unlocked, a story flag, another good bought to a level, a Grudge held, a level
  cleared) it is *not on the counter at all*, not greyed out: Yooba doesn't show off what you can't use. The moment it
  is met, the card appears. A hero who already owns a good always sees it (an older save loses nothing).
- **Yooba's prices** (2026-10-27, J: "make the upgrades so they don't cost as much diamonds ... cap higher"). **This supersedes the 2026-10-25 price rise** (2.6x the old
  prices on the older goods, 3.0x with the gated good: 1,705 diamonds for the older counter, 3,565 with the weapon goods, Slimed Lining and rewind goods). Hell is repetition, and so is saving up, but not *that* much of it: **a
  Circle of climbing should buy something that matters.** The older goods now cost about half per level and reach further.
  Measured first (no playtest report was at hand, so from the loot tables: `loot` chance x count x value per kill, ~40 kills on a five-minute floor, a Circle's ~30 played
  floors): **Circle X pays about 15 diamonds a floor, ~450 a Circle** (imps 0.15-0.6 a kill, the sack imps 2); **Circle IX about 25 a floor, ~600-750** (the Road's wraiths 3, bastion brutes 1.6). The real numbers are the
  playtest's `level<n>.diamonds`, in the `all-levels` balance report; read those and adjust the table below. Enemy `loot` is untouched.
  - **Goo Plaster is the headline: +2 max health a level, 10 levels, 10 diamonds rising ~1.3x a level, 433 in all: one Circle of climbing buys the whole line, and the hero goes from 5 hearts to 25.** It stops at 25
    on purpose. J's example was 100 HP, but every hit costs one heart; 100 would need hits that scale with it. The health bar draws segments up to 12 (`hud.maxSegments`) and a smooth bar with the "25/25" count beyond that,
    so 25 stays readable in landscape (440 wide) and portrait (330 wide).
  - The other older goods got 4-5 levels where they had 2-3 (rewind goods stay at their cap: the history holds 30 s), at roughly half the old price per level. Yooba's 21 later goods (the ones with a card tag) are
    untouched, on their own curve below.

  | Goods | Levels | Prices | Total |
  |---|---|---|---|
  | Goo Plaster | 10 (+2 hearts each) | 10, 13, 17, 22, 29, 38, 49, 64, 83, 108 | 433 |
  | Bouncy Soles | 2 | 20, 45 | 65 |
  | Slick Heels | 5 | 12, 20, 32, 48, 70 | 182 |
  | Licked Trigger | 5 | 15, 25, 40, 60, 85 | 225 |
  | Tummy-Dipped Shots | 5 | 20, 35, 55, 80, 115 | 305 |
  | Loop Jar | 5 | 12, 20, 32, 45, 60 | 169 |
  | Bottomless Jar *(gated)* | 2 | 70, 100 | 170 |
  | Quick Unwind | 5 | 18, 30, 48, 70, 95 | 261 |
  | Shiny Sniffer | 3 | 8, 18, 30 | 56 |
  | Double-Dipped Shots *(gated)* | 4 | 35, 60, 90, 125 | 310 |
  | Slimed Knuckles *(gated)* | 4 | 25, 45, 70, 100 | 240 |
  | Stretchy Wraps *(gated)* | 4 | 18, 32, 50, 70 | 170 |
  | Keep Weapons *(gated)* | 1 | 60 | 60 |
  | Slimed Lining *(gated)* | 1 | 40 | 40 |
  | **All older goods** | | | **2,686** (was 3,565); 1,696 without the gated ones |

  The playtest reports the diamonds each level paid (`level<n>.diamonds`) and the shop's total (`shop.totalPrice`, `shop.basePrice`); `shop-balance` buys the whole health line with real clicks.

- **Yooba's new goods and the new Grudges** (2026-10-25; `Gameplay/Core/HeroPerks.cs`, `ShopEffect` Armor..LastGasp, `UpgradeDef.perksPerLevel`). More things a slime can make out of his stomach, 21 goods (7 of them gated) and 10 Grudges, all small
  effects with something to see: a *POP!*, *CLANK!*, *SLIPPED!*, *CRIT!*, *+1 BURP*, *BELCH!* or *LAST GASP!* floats over the hero. Totals are constant for a floor; what is spent (bubbles, bombs, gasps, plates) is saved state; every chance is a hash of the seed and the moment, so a rewind replays the same.
  Prices follow the curve Goo Plaster set: the first level's price times 1, 2.2, 3.6, 5.8, 8.4, rounded to 5 (`ShopGoodsTests`). The new goods add **7,125** diamonds (9,811 for the whole counter; the older goods are 2,686 since 2026-10-27, the Slimed Lining included).

  | Good (card tag) | Levels | Prices | What it does |
  |---|---|---|---|
  | Crusty Bits (P) | 3 | 55, 120, 200 | Armour plates: each blocks a hit, grows back 20 s later |
  | Thicker Crusts (P2) *needs Crusty Bits 3* | 2 | 120, 265 | +1 plate per level |
  | Snot Bubble (B) | 3 | 50, 110, 180 | A bubble at the start of each floor; pops on the first hit |
  | Spare Bubble (B2) *needs Snot Bubble 3* | 2 | 110, 240 | +1 bubble per level |
  | Greased Elbows (S) | 3 | 40, 90, 145 | 8% of hits slip off |
  | Lucky Lick (C) | 3 | 45, 100, 160 | 7% of volleys crit (x2) |
  | Four-Leaf Slime (C2) *needs Lucky Lick 3* | 2 | 100, 220 | +8% crit |
  | Meaner Lick (M) *needs Lucky Lick 1* | 3 | 60, 130, 215 | Crits +50% harder |
  | Yooba's Burp (H) | 3 | 50, 110, 180 | 7% of kills heal a heart |
  | Hungrier Burp (H2) *needs Burp 3* | 2 | 110, 240 | +7% |
  | Gravy Net (N) *needs Shiny Sniffer 2* | 3 | 25, 55, 90 | Bigger magnet (+1.5) |
  | Fat Diamonds (F) | 3 | 70, 155, 250 | 15% of diamonds are worth one more |
  | Sticky Air (G) | 3 | 65, 145, 235 | Enemy shots 6% slower |
  | Loop Crumbs (K) | 3 | 45, 100, 160 | Clean kills pay 0.4 s more Déjà Vu |
  | Thrifty Loop (L) | 3 | 55, 120, 200 | Rewinds cost 8% less juice |
  | Belch Bomb (X) | 2 | 85, 185 | A hit that lands pops the shots near him and hurts what's close (one a floor per level) |
  | Louder Belch (X2) *needs Belch Bomb 1* | 3 | 60, 130, 215 | Bomb radius +0.6 |
  | Afterglow Goo (A) | 3 | 55, 120, 200 | +0.15 s untouchable after a hit |
  | Slippery Shots (Z) | 3 | 35, 75, 125 | His shots fly 10% faster |
  | Floor Pudding (D) | 2 | 75, 165 | Heals a heart as each floor starts |
  | Spare Gasp (Y) | 2 | 120, 265 | A killing hit leaves one heart, once a floor per level |

  The Grudges (`content/upgrades`, perks per level held, gone when the run ends): **Held Grudge** (a bubble), **Sour Grapes** (+10% crit), **Bad Blood** (+8% heal on kill), **Petty Cash** (+25% fat diamonds),
  **Slow Burn** (enemy shots 8% slower), **Eye For An Eye** (a belch bomb), **Holding On** (a last gasp), **Side Eye** (+10% slip), **Keeping Score** (clean kills +0.5 s), **Rubbing It In** (crits +50%).
  Card art is a one or two letter tag until the icons are painted (31 icons, priced in the pull request).
- He gossips, he counts, he is proud of his stomach. He is never scared for long and never mean. Slimes are
  villagers: nobody in the game ever fights him or his kind.

## Shortcuts

Hell is repetition, so it hates a shortcut, and it leaves them lying around anyway: a stairwell that fell in, an
elevator nobody trusts, a demon who owes you for sparing it. A **shortcut** is a choice point inside a stage that skips
floors for a cost or a risk. It is the rogue choice that keeps 1,000 floors reachable in hours, not days.

- **The choice** stops the stage (like a Grudge pick) and shows two cards: **keep climbing** (the long way: the stage
  ends as usual, with its Grudge pick) or **the shortcut**, with **its cost on the card**. The player is never told how
  far a shortcut goes: the card says what it is, not where it ends. The choice is made once per play and is final;
  Déjà Vu can't undo it (Hell's loop runs the same way again), and the cost is paid once.
- **Costs** come from the story: a Grudge (spite is what keeps him climbing; the easy way takes some out of him), a
  harder stage after it, or the village's diamonds. One cost per shortcut. A Grudge cost takes the one he has most of
  (and the max health it gave); with no Grudge to give, the card is stamped *NO GRUDGE TO GIVE* and he climbs on.
- **Taking it** jumps the floor counter and the story path past the stages it skips: the stage ends where the
  shortcut lets him out, with no Grudge pick (he just paid one), and the next stage is the one after the skipped ones.
  On the Shaft the marker climbs to where he took it, then jumps a dotted line up through the crack it hinted at.
- Every shortcut gets a voiced hero line when he takes it and the **announcer's reaction**: Hell's show hates a cheat
  and loves a twist.
- Where they are: hinted on the Shaft (`ui/shaft.json` `shortcutHints`) long before he can take them. Act 2's midpoint
  (Floor 500) is the one he doesn't take: a way home, and he climbs on.

| Shortcut | Where | Cost | Skips | Lines |
|---|---|---|---|---|
| **The Sack Lift** (`sackLift`, built 2026-10-19, `levels/Floor991.json`) | Floor 991's landing, after the Imp Foreman: the imps' sack lift; the Shaft's crack at 975 | diamonds: *FARE: 20 DIAMONDS* (`costDiamonds`), paid in the village's own diamonds; with fewer the card is stamped *CAN'T AFFORD THE FARE* and he walks | Floors 990-961: out at **Floor 960** | Taking it: *"Paying them with our own diamonds. Lovely."* Announcer: *"HE'S RIDING THE SACK LIFT! THE CROWD IS DISGUSTED!"* (all lines: LevelDesign.md) |
| **The King's Chimney** (`kingsChimney`, built 2026-10-19, `levels/Floor951.json`) | Floor 951's landing, after the Pile Keeper: the Magma King's heat vent; the Shaft's crack at 940 | a harder floor after it (a risk, on the card: *Risk: the King hears you coming*): Floor 910 reads `shortcutTaken`, its lava rises faster with the push music on, and the King says so: *"Came up my chimney, little spark? Rude."* | Floors 950-911: out at **Floor 910** | Taking it: *"Hold your breath. Forever."* Announcer: *"UP THE CHIMNEY?! HE SKIPPED HALFWAY!"* |
| **The Collapsed Stairwell** (`oldStair`) | Floor 871, the top of the Old Stair (`levels/Level14.json`, level 62, on Circle IX's long way), behind *STAIRS (CLOSED)*; the Shaft's crack at 870 | a Grudge | Floors 870-841 (levels 63-65): out at **Floor 841**, level 66 next | Hero, at the top: *"Stairs. Fallen in. Still going up, though."* Taking it: *"Fine. One less thing to hate."* Announcer, as he climbs in: *"WHAT'S THIS? A SHORTCUT?! THE CROWD HATES IT!"*, then, as the counter lands: *"FLOOR EIGHT FORTY-ONE! NOBODY TAKES THE STAIRS!"* Hero, coming out: *"That went further than stairs should."* Keeping on: *"Nah. Hell would love that."* |
| **The Spared Demon** (`sparedDemon`, built 2026-10-19, `levels/Floor891.json`) | Floor 891, after the Road Breaker: a whelp pinned under the broken road; let go, it claws a crack up the spire; the Shaft's crack at 870 | a Grudge | Floors 890-861: out at **Floor 860** | Taking it: *"Go on. Shoo. ...Oh, you're helping."* Announcer: *"HE SPARED A DEMON?! THE CROWD WANTS ITS MONEY BACK!"* (all lines: LevelDesign.md section 6) |
| **The Hollow Spire** (`hollowSpire`, built 2026-10-19, `levels/Floor851.json`) | Floor 851, after the Carver: the spire he was hollowing out; the Shaft's crack at 830 | a harder floor after it (a risk, on the card: *Risk: the spire comes down behind you*): Floor 810 reads `shortcutTaken`, its shards fall from the start and the Rift rises faster | Floors 850-811: out at **Floor 810** | Taking it: *"Up the inside. Don't touch anything."* Announcer: *"UP THE SPIRE?! HE SKIPPED HALFWAY... AGAIN!"* |

## Secret floors and gauntlets

*(built 2026-10-25; generator `Tools/Levels/side_floors.py`, floors in LevelDesign.md section 9, recipe in the cookbook "A secret floor")*

Hell is repetition, so it has a floor between every two floors that nobody numbered: a closet, a rack, a room where the
steam is too thick. The hero finds them the way he finds a shortcut, by being the one who looks at a wall too long.
A **secret floor** is a short stage (one room, about a minute and a half) on **"Floor N and a half"**; a **gauntlet** is a
room that sends him waves, one after the other.

- **The door in the wall.** On the last foothold before a floor's landing, a seam in the stone offers a choice with the same
  two cards as a shortcut (`offerShortcut`; the title always begins *A door in the wall*): *leave it* (the floor ends as usual,
  with its Grudge pick) or *go in*, with its **cost on the card**: diamonds, a Grudge, or a risk the card names. Taken, the
  floor ends there with no Grudge pick (the door cost something), the side floor plays, and the climb goes on **exactly
  where it was**: the side floor leads to the floor after its host, and nothing on the route is skipped. The Shaft draws it
  as a small dotted hop through a crack, like a shortcut.
- **Waystone rules, unchanged.** A side floor sits on the floor its host ends on, never on a Waystone's, so it lights nothing. A death
  in it ends the run like any floor of Hell: the death screen, the drop to the highest Waystone, Grudges reset, diamonds and goods
  kept. What it pays (a weapon, a lore flag) is kept the way those are kept everywhere else; a Grudge it pays (Held Breath) is a
  Grudge like the rest and goes with the run. Held Breath is never the Grudge a shortcut or a door takes.
- **No sound of its own yet.** The lines are subtitles in the announcer's and the hero's voices; the voice clips are still to be made
  (`audio.json`, cents each; nothing else depends on them).

| Door | On | Cost | The room | Pays |
|---|---|---|---|---|
| **The Tally Closet** (`tallyCloset`, `Secret1`) | Floor 906, the Loop upstairs | 5 diamonds | Two imp keepers, then a wall of fives, every group crossed out, a big hand; the last mark: *AGAIN.* | **Lore**: flag `lore.tally`. The Overlord counts the floors too, and is as stuck as the hero. *"He counts them too. Great. Now I can't even hate him properly."* |
| **The Armory Nobody Locked** (`armoryDoor`, `Secret2`) | Floor 910, the foot of the Heat Stairs | a Grudge | Two imp guards, then a rack with one thing on it. | **A weapon**: the **Slag Ladle**, unlocked for good (slow, heavy, 10 scoops that come back). *"I'm armed with a soup utensil. It's fine."* |
| **The Held Breath** (`heldBreath`, `Secret3`) | Floor 909, a vent as wide as a door | 15 diamonds | Steam: fire rain and cinders for twenty seconds, nothing to do but not get hit. | **A hidden upgrade**: the Grudge **Held Breath** (+2 max health, 2 s more rewind), weight 0, so no Grudge pick ever offers it. |
| **The Applause Room** (`applauseRoom`, `Secret4`) | Floor 907, the King's ear | free; *Risk: Hell is watching.* | A crowd of damned souls, the announcer overjoyed that he is standing, one imp as "the act". | **A funny set piece** that tips: 20 diamonds. *"It's an imp."* |
| **The Rest Stop** (`restStop`, `Secret5`) | Floor 905, a bench in a crack | 10 diamonds | A bench, a soul who hands over a loop shard, the crowd not knowing what to do about a man sitting. | **A refill**: full health and full rewind. *"Ten seconds of nothing. Hell hates this."* |
| **The Guest Book** (`guestBook`, `Secret6`) | Floor 903, under the throne's glow | a Grudge | Three cinders "see him out", then the Magma King's book: CAME. SAW. LEFT. in one hand, hundreds of times; his last line is wet: LITTLE SPARK. | **Lore**: flag `lore.guestbook`. The King wrote the hero's name before he got there. |
| **The Cage Match** (`cageMatch`, `Gauntlet1`) | Floor 908, the lift's cage | *Risk: four waves, no way out.* | Sand on the floor. Four waves (3 imps; 2 guards; 3 cinders and an imp; 2 guards and 2 imps), **a full rewind refill between every two**. | A purse: 12 diamonds and the flag `gauntlet.cage`. |
| **The Encore** (`encore`, `Gauntlet2`) | Floor 904, a curtain painted on the wall | 10 diamonds | The same show five times, each a little bigger (2 imps; 3; 2 and a guard; 3 cinders and an imp; 3 guards and an imp), **a rewind refill between every two**. | The flag `gauntlet.encore`. |

Tone: the announcer loves a secret ("A SECRET DOOR?! HELL HAS BEEN KEEPING SECRETS!"), the hero has no patience for one. No office
or work jokes. A secret floor is never required: the long way and the shortcuts reach the Magma King without any of them.

## Waystones and runs

Hell doesn't let you die. It sends you back to repeat the floors: the whole place runs on a loop, and the hero is
just one more thing in it. A **run** is one climb from a Waystone until Hell kills him.

- **Waystones** are the stones where Hell starts its loops over: the **Landing** at Floor 1,000 (the first, reached
  when he lands there) and the top floor of every Circle once its Warden is beaten (Floor 901 when the Magma King
  falls, then 801, ... 101). The highest one reached is saved; it is where every run starts from then on.
- **Dying on a floor of Hell** (any stage with floors) ends the run, unless he pays to *Return* (below). First the death screen: the death video with
  *"YOU OBVIOUSLY DIED"* over it and Hell's announcer gloating (*"OHHH! HELL TAKES THE ROUND!"*, *"FLATTENED! WHAT A
  FINISH... FOR HELL!"*, *"AND HE'S DOWN! BACK YOU GO!"*, in turn). Then the Shaft: the hero's marker sits on the
  floor he died on, then falls, faster and faster, back down to the Waystone and lands with a slam. The run
  restarts there, with the stage that starts on (or just above) it: from the Landing, Floor 1,000 (Level 2).
- **Return** (J, 2026-10-27): Hell sells second chances, and it loves the sale. Once the rewind window has closed
  (hold R beats everything) and the death screen is up, a second button sits beside *Rage Quit*: **"Return for 10
  diamonds"**, the announcer's offer, in the village's own coin. Only with the diamonds in his pockets (under 10 it
  isn't there). The Shaft's drop stays the default: do nothing, skip the animation, or Rage Quit, and the run ends as
  above. Pay, and the run **doesn't end**: no drop, no Waystone. The floor he died on starts again from its first
  moment with the Grudges and the health he carried in, **1.5 seconds** in which nothing can hurt him, and the
  rewind juice of any fresh floor. (A floor is one level, so "the floor he died on" is exactly where it restarts; a
  stage of several floors starts over at its first.) The 10 diamonds are spent for good, never refunded by a rewind.
  The announcer pockets them without shame. Not on the Meadow or the replay floors: they play again anyway.
- **The first time** the announcer explains it, in story terms: *"HELL DOESN'T LET YOU DIE. IT LETS YOU DO IT
  AGAIN."* Hero: *"Of course it does."* That first drop can't be skipped. After that, one jeer: *"BACK TO THE
  WAYSTONE! DO IT AGAIN!"* (Yooba, on the next climb, has his own line about it: *"Back again? Hell does that."*)
- **What Hell takes and what it can't:** the run's **Grudges** go (and the max health a Grudge gave), and the
  health he carried from stage to stage, so a new run starts fresh. The **village's diamonds** and **Yooba's goods**
  stay: they're real things, in his pockets and his boots, not spite. Yooba's shop is open on the drop too.
- **The Meadow** is not Hell: a death there (and on the arcade levels, outside the Circles) just plays the level again.
- **Main menu:** **Continue** goes on with the current run (the stage it is on); **New Run** (once a Waystone is
  reached, *"New Run (Floor 1,000)"*) throws the current run away and starts a fresh one at the highest Waystone,
  Grudges reset; **New Game** forgets everything and starts at the Meadow.

## Music: Hell's arcade soundtrack

Hell puts on a show, so it has a soundtrack: a 90s arcade cabinet played through a furnace. Distorted guitars, FM
synth leads, gated drums, a low choir of the damned. Every Circle gets its own loop (Hell is repetition: the loop
*is* the joke) and every Warden a theme. Made with Hawkes Gen's `audio.music` (ACE-Step), `floor-music` preset;
the picks and the trims are recorded in `Tools/Audio/music.json` and made by `Tools/Audio/music-mix.py`.

- **The Ember Wastes** (Circle X, its floors below the Warden): a groove in D minor at 128 BPM that loops without a seam (each
  stage's `music`). When Hell sends a **push** (a generated wave of enemies, or a hazard) the Circle's `pushMusic`,
  the same tune turned up (double kicks, shredding, alarms), takes over and drops back once the floor goes quiet.
- **The Magma King** (Floor 901, level 31): the Wastes' groove dies as his throne rises, and **one big hit** lands as he
  appears. The fight starts on his theme: swaggering doom metal and organ, a king who loves an audience. When he
  loses his temper (the **enrage**) it turns frantic: blast beats, a screaming solo, sirens. When he falls, the
  music sinks under the slow motion.
- **Every Circle has a loop and a Warden a theme** (designed, 2026-10-25; the *Music* paragraphs of each Circle above are the
  brief). Circle IX (the Cracked Road) had none: its loop is a **grinding half-time stone riff** in C-sharp minor at
  120 BPM (down-tuned guitar, slabs cracking for percussion, a low brass drone); the Spire Fiend's theme is **patient
  doom** at 84 BPM (a sparse stone drone, a hollow whispered choir holding single notes, one dry bass pulse on the
  downbeat: it adds one note at a time, like someone counting steps). The Overlord (Circle I's, Floor 1) gets the
  full orchestra and distorted organ on the same grand slow tune as Circle I's loop, enormous and in no hurry (the major
  chord of his third phase is made later). Circle X's loop and the Magma King's theme exist; the rest are in the manifest.
- **The intensity layer.** Hell gets louder when it gets crowded. Each Circle has a **layer**: a stem the same length, tempo and key
  as its loop (the rhythm section turned up: Wastes' double kicks and alarms, the Bridges' sixteenth arpeggio, the Maw's doubled
  heartbeat, the Keep's chains), played silently in step with the loop all the time and **faded in as enemies and hostile shots
  crowd the screen** (`CircleDef.intensityMusic`, `StageMusic`): each live enemy counts 1, each hostile shot 0.35, the layer starts at a
  crowd of 3 and is fully in at 14; it comes in over 1.5 s and leaves over 4 s so a single volley doesn't flicker it. It is not used under a
  push track or a Warden's theme (they are intense already), and stops for a rewind. The three built Circles (Ember Wastes, Cracked Road, Rune Bridges) each play their own layer; deeper Circles keep the
  Wastes' push track until theirs are wired.
- **The Meadow's own music** (Level 1): not the old background film's sound but a track of its own: **"A nice day"**, a warm plucked
  figure over a soft drone in D major (`meadow_calm`, level `music`), which plays while the village goes about its morning. When the sky
  opens (the rumble, the shake, the neighbours running) the music **turns**: `playMusic` brings in `meadow_raid`, the same tune in D minor at 128 BPM
  with double kicks, shredding and alarms. Both are Hawkes Gen `audio.music` takes (best of three each, cut into seamless loops by `music-mix.py`;
  `meadow_calm` is 84 BPM in D major with plucked guitar and a soft flute). The nice day lasts 6 s before the sky opens.
- **Rewinding bends the music.** A held rewind fades the normal track out and brings in **the same track, slowed like a tape dragged backward and
  low-passed** (`StageMusic`: pitch 0.85 sliding to 0.6, a 700 Hz low-pass, 0.12 s in, 0.15 s out), started and stopped by the clock's
  `OnRewindStarted` / `OnRewindStopped` events (telemetry `musicRewind`). The normal track is back the moment the rewind ends.
- **A Warden's theme starts as his arena locks:** on Floor 901 the Wastes' loop is cut as the throne rises, his one big hit lands and **the theme
  comes in under the intro** (0.5), rising to full (0.9) when the fight begins.
- **Stingers.** A short fanfare when a **new weapon** is found for the first time (`sting_weapon`, with the "NEW WEAPON" banner)
  and one per **rank** on the results screen (`sting_rank_s` to `sting_rank_d`, `theme.json` grade `stingSound`): S golden and huge, A
  confident, B tidy, C a lazy shrug, D a sad comedic wah-wah. Stand-ins (the Shaft's climb sting) play until they are made.
- **The manifest** `Tools/Audio/music_manifest.json` lists every track still to make (prompt, tempo, key, takes, file, stand-in) and its
  price (`python3 Tools/Audio/music_manifest.py --price`): 34 tracks, 102 takes, ~$0.58 of GPU. Best of three takes each, picked against the
  manifest's `pick` line, then cut by `music-mix.py`. Nothing in it is generated until J approves the price.
- **The Shaft:** the plunge after Video2 is a **falling riser** that **slams** as the marker hits Floor 1,000; each
  climb gets a **short rising sting** as the marker goes up.

### The adaptive score (Meadow, Floors 1000-997)

Each of the first five levels has three loops at one tempo and key: **explore** (sparse, the floor's motif), **fight** (full drums,
distorted guitar, choir stabs) and **finale** (the set piece at the floor's end, its own cue, never the fight loop). The Meadow is
pastoral folk turned frantic (112 BPM, D minor); 1000 sulphur and brass (120, A minor); 999 furnace industrial (124, E minor);
998 bone-percussion and choir (108, F# minor); 997 the Overlord's organ (116, C minor). `LevelConfig.score` (`ScoreConfig`), played by
`StageMusic` (the one music player; `StageMusic.Score.cs` is its director): the three loops play on one playhead and only their gains
move. Fight comes in on the next beat when an enemy is alive or two hostile shots are on screen, leaves on the next bar a second after
the wave's last enemy is down; the floor's finale signal (`floors:<n>:finale`, the Meadow's `portal:telegraph`) takes the finale cue.
A rewind tape-stops the music (pitch down over 0.3 s) and the three loops come back on one playhead when it ends; a death cuts to a
1 s sting, then explore. The cue table is `Tools/Audio/adaptive_score.py` (`--manifest`, `--wire`, `--check`); the existing per-level
tracks are the fallback for a floor with no `score`. Playtest `score-floors`.

## The systems, in story terms

Mechanics keep working as they do; their names and reasons come from here. Rename UI text (`ui/theme.json`
`texts`) and upgrade titles to match when those systems are next touched.

| System | In the story | Notes for design |
|---|---|---|
| **Rewind** (hold R) | **Déjà Vu.** Hell runs everything on a loop; the hero grabs the loop and pulls it backward. The floor is part of the loop too: a stone that sank comes back up, a ledge that fell is back in place, the lava drops to where it was. | The juice meter is "Déjà Vu". **Rewind orbs** are *loop shards* that drop only from the damned: souls stuck in Hell's loop, repeating a floor forever (in the Wastes, a damned soul on a ledge whose loop hiccups as the hero passes; deeper down, the Shades). Never from imps, cinders or guards. In a **scene** something bigger has hold of the loop (the portal's pull, a Warden's entrance), so he can't grab it: rewind is locked and the button says "Not now". |
| **Death and runs** | Hell doesn't let you die; it sends you back to repeat the floors. "YOU OBVIOUSLY DIED." | A run restarts at the highest **Waystone** reached (the Landing, then the top of each Circle once its Warden is beaten); see *Waystones and runs*. Death videos are Hell's announcer gloating. |
| **Between-level picks** (`content/upgrades`) | **Grudges.** Every floor gives the hero something new to be annoyed about, and spite is fuel. Lost when a run ends. A Grudge can carry a `requires` too (not offered until met). | Titles are grudges: "Spite Rounds", "Short Fuse", "Not Today", "Déjà Vu". **Not Today** (`notToday`, built 2026-10-25): whatever hit him, he decides it is not today; after a hit he is untouchable for 0.5 s longer per level (2 levels). It is the Magma King's murmured *"Not today."*, taken as a motto. |
| **Diamonds and the shop** | The village's diamonds, the portal fuel the imps stole; enemies drop them back. **Yooba** works them into the hero's gear between floors. Permanent. | Shop = Yooba. Shop upgrade names are things a village slime can make. |
| **Floor counter and progress bar** | How far up you are. | Progress bar = floors left in the stage; markers = Warden, shortcut, milestone. |

## Rules for enemies

1. **Every enemy has a reason to be on its floor**: who it works for, what it wants, why it's in the hero's way.
   Write the reason in the entity's design note (and in this table) before giving it stats.
2. **One Circle, one family.** An enemy belongs to the Circle whose biome it fits; at most it shows up again in
   the next Circle up, promoted (tougher, recolored, renamed).
3. **Introduce one new enemy at a time, alone**, with room to read it before it's mixed with others.
4. **Slimes are friends.** Yooba is a slime; the village's slimes are villagers. No slime enemies.
5. The look comes from the art pipeline (see *Art*), never from tinting a sprite of a different creature.
   Placeholder tints are fine while art is being made, and the table here says which art is pending.

### Roster

| Entity id | Name | Circle | Why it's there | Art |
|---|---|---|---|---|
| `imp` | Imp | Meadow, X | The Overlord's raiders. They loot the village's diamonds, then guard the way down in the Wastes. Throw slow embers. Die like Hell's own fireworks: a shriek, eyes bulging, then a pop of soot and embers with the horns and the (empty) sack dropping out, because they are made of the Wastes' fire. | generated (`art/imp.json`, `Art/Enemies/Imp/`) |
| `cinder` | Cinder | Meadow, X | A spark from the burning village that came alive and followed the hero through the portal. Drifts toward you and pops. | generated (`art/cinder.json`, `Art/Enemies/Cinder/`) |
| `impSack` | Sack imp | X (from Floor 997, Imp Run) | An imp hauling one of the village's diamond sacks down to the Overlord's gate. Too busy carrying to throw anything: it runs back and forth with the sack, and every hit tears the sack and spills a diamond. Diamonds from a sack: rewards that make sense. | generated (`art/impSack.json`, `Art/Enemies/ImpSack/`) |
| `impSwing` | Sack imp on a swing | X (Floor 997, Imp Run, on) | A sack imp riding one of the hauling chains across a gap with the village's diamond sack (LevelDesign.md, *the hauling chains*): it swings on the chain's beat, never throws (both hands on the sack), burns on touch, and every hit tears the sack and spills a diamond. Too busy to chase anyone. | the imp's art, tinted warm, on the swing chain's links |
| `impFleeing` | A startled imp | X (Floor 1000) | The first imp he meets in Hell: a hauler on the landing who sees him, drops its ember into the lava and scrambles up its chain with its sack, which snags and drops a diamond. Harmless; it is gone in a few seconds. | generated (`art/impFleeing.json`, `Art/Enemies/ImpFleeing/`) |
| `impForeman` | The Imp Foreman (floor boss) | X (Floor 991) | The fattest imp in the Wastes, on the biggest sack of the village's diamonds, with a chain whip; he drives the sack haulers up to the Sack Lift and loads it. Too heavy to fly: his wings are for show. Two phases on a climbing arena: on the loading floor he cracks his whip along the tier (a glow on the floor 1 s first) and whistles haulers onto the swings; at half health the lava takes the tier and he hauls himself up to the lift, where he swings on its chain dropping embers from his sack (a glow on the spot 1 s first). Beaten, his sack bursts: a pile of diamonds. Voice `foreman`: hoarse, nasal, wheezing, bossy. | generated (`art/impForeman.json`, `Art/Enemies/ImpForeman/`) |
| `impCutter` | Chain-cutter imp | X (Floor 994) | An imp at a lift's pulley hacking at the chain to drop the lift (and the hero) into the lava. Doesn't throw: the chain is its target. Shoot it and the lift holds. | generated (`art/impCutter.json`, `Art/Enemies/ImpCutter/`) |
| `damnedSoul` | A damned soul | X (from Floor 999) | A soul stuck in Hell's loop, repeating one small thing on one ledge forever (on 999, counting the same stone). It can't fight or be hurt. When the hero passes close, his loop brushes its loop, it hiccups and a loop shard drops (once per play). The only thing that drops loop shards. | generated (`art/damnedSoul.json`, `Art/Enemies/DamnedSoul/`) |
| `lavaGeyser`, `geyserVent` | Lava geyser (hazard) | X (from Floor 996, the Slag Falls) | The lava sea under pressure: a basalt vent whose crack glows and spits for a second, then blows a column of lava straight up for a moment. Fires on a fixed beat (Hell's loop), so a rewind shows the order. | generated (`Art/Climb/`) |
| `slagDrip`, `slag` | Falling slag (hazard) | X (from Floor 970, the Slag Falls) | Molten rock from the falls. A glowing drip gathers for 1.2 s (the telegraph, with a hiss), then a lump of slag falls from that spot and sets you burning. Not a creature; nothing to kill. | generated (`Art/Effects/Warn/`) |
| `lavaBurst` | Lava burst (hazard) | X (the Loop) | A bubble swelling in a crack (1.3 s of glow), then three embers burst up out of it. In the Loop it goes off in the same crack every time. | generated (`Art/Effects/Warn/`) |
| `cinderThatch`, `cinderSpark` | Thatch cinder | X (from Floor 950, Halfway Basin) | A burning clump of the village's thatch roofs that fell through with everything else and came alive like the other sparks. Bigger and slower than a cinder; shot, it bursts into two sparks (`cinderSpark`) that dart at you for two seconds and go out (they can't be shot, only dodged). | generated (`art/cinderThatch.json`, `Art/Enemies/CinderThatch/`) |
| `impGuard` | Imp guard | X (from Floor 930, the Sack Pile) | An imp: the same family (it `extends` `imp`), one the Magma King keeps at his pile and on his stairs instead of sending it up with a sack. It carries no diamonds and doesn't run; it plants itself and throws three embers in a fan, so guards are the first imps that fight together (lines of three on the Sack Pile and the Heat Stairs). It drops nothing: `"drops": []` replaces the imp's drops (no imp drops loop shards; those come only from the damned). The only diamond it gives up is its pay from the pile, now and then (`loot`). | generated (`art/impGuard.json`, `Art/Enemies/ImpGuard/`) |
| `emberShower` | Ember shower (hazard) | X (from Floor 940, the Crust) | Steam off the lava river raining embers two at a time; announced by the hero (*"Now it's raining fire."*) 1.5 s before the first. | `fireball` |
| `magmaKing` | The Magma King | X (Warden, Floor 901) | Rules the Ember Wastes from a throne of slag. Heard (voice `magmaKing`) on the Heat Stairs before he is met. Fights seated (boulders `magmaBoulder`, fist slam → `slamShockwave`) until half health, then stands for the only time and fights standing with his imps (ground-pound, fire breath); at a third he enrages and floods the hall (`lavaFlood`); see *The Magma King's fight*. Drops ten diamonds: his share of the stolen pile. | generated (`art/magmaKing.json`, `Art/Enemies/MagmaKing/`) |
| `magmaBoulder`, `slamShockwave`, `lavaFlood` | Boulder, shockwave, lava flood | X (his arena) | The Magma King's attacks: a lump of molten slag lobbed in an arc that sets you burning, a low shockwave of slag that runs along the floor when he slams (jump it), and the flood (phase 3): a wall of lava too tall to jump that rolls most of the way across the hall (outrun it if you can; rewind if you can't). Each has its warning glow (`mkWarnHand`, `mkWarnFloor`, `mkWarnMouth`, `mkWarnFlood`), 1 s or more ahead. `lavaWave` is the older wave the arcade fight uses. | generated (`art/magmaBoulder.json`, `Art/Enemies/MagmaBoulder/`) |
| `brimstoneDemon` | Brimstone Demon | IX | Born from the cracks in the Cracked Road: the rock splits and a demon climbs out to stop you climbing. Slow, heavy, plants itself in the road a few steps in front of you and breathes a short burst of fire (`brimstoneBreath`, sets you burning), so keep your distance. Sometimes drops a diamond: the imps' sacks leak on the road, the cracks swallow what falls, and the demons come out with it stuck in their rock. Not damned, so no loop shards. First met alone on Floor 900, the first floor of Circle IX (`levels/Floor900.json`). | generated (`art/brimstoneDemon.json`, `Art/Enemies/BrimstoneDemon/`) |
| `demonWhelp` | Brimstone whelp | IX (from Floor 897, The Whelps) | The cracks' young: a small Brimstone Demon that scuttles out of a crack and charges along the ledge at him. Too small to breathe fire, not too small to bite (burns on touch); two hits. Not damned: no loop shards. | generated (`art/demonWhelp.json`, `Art/Enemies/DemonWhelp/`) |
| `demonLobber` | Brimstone lobber | IX (from Floor 894, The Lobbers) | A Brimstone Demon that climbed the spires instead of the road: it squats on a spire ledge and lobs lumps of brimstone (`brimstoneLump`) in an arc at him, the landing spot glowing 1 s first (`lobberGlow`). | generated (`art/demonLobber.json`, `Art/Enemies/DemonLobber/`) |
| `whelpNest` | Whelp nest | IX (from Floor 856, The Nest) | A crack in the rock where whelps are born: it glows and spits a whelp every few seconds until it is shot shut. A target, not a fighter: it never moves or attacks. | generated (`art/whelpNest.json`, `Art/Enemies/WhelpNest/`) |
| `roadBreaker` | The Road Breaker (floor boss) | IX (Floor 891) | The biggest Brimstone Demon on the road, a slab of road for a club: he keeps the road broken so nothing climbs it. Two tiers: slams that run a crack along the road (a row of glows, each bursting 1.1 s later) and a lump of road kicked at the hero's spot (a glow first); at half health he breaks his own tier and the road gives way, then the same with whelps crawling out of his cracks. Voice `roadBreaker`: deep, gravelly, slow, proud of every crack. | generated (`art/roadBreaker.json`, `Art/Enemies/RoadBreaker/`) |
| `carver` | The Carver (floor boss) | IX (Floor 851) | A gaunt demon with a chisel for a hand who carves everything on the road: the floor numbers, the rules (*KEEP TO THE ROAD*), the jokes (*THIS WAY UP*). Fussy, offended by footprints. Two tiers: chisel strikes along the tier and shards knocked loose over the hero (each after a 1.1 s glow); at half health he climbs his scaffold and whelps come out of his chiselled cracks. Voice `carver`: thin, fussy, precise, offended. | generated (`art/carver.json`, `Art/Enemies/Carver/`) |
| `sparedWhelp` | The spared whelp | IX (Floor 891) | A whelp pinned under the broken road after the Road Breaker falls, squealing. Harmless. Let go, it owes him: the Spared Demon shortcut. | generated (`art/sparedWhelp.json`, `Art/Enemies/SparedWhelp/`) |
| `roadSlab`, `settleSlab`, `splitSlab`, `risingSlab`, `seesawSlab`, `roadCreep` | The road's pieces | IX | The Cracked Road itself: solid slabs, slabs settling into the Rift, slabs splitting apart, slabs the Rift heaves up, slabs balanced on spire tips, road sliding off the spires (LevelDesign.md section 6). | generated (`Art/Climb/`) |
| `spireShard`, `shard`, `brimstoneVent` | Spire shards, brimstone vents (hazards) | IX | The spires shedding (a glowing seam, then a shard), and the cracks breathing fire on a beat (a glowing crack, then a column). | the slag drip's and the geyser's art, tinted |
| `haloImp`, `haloEmber` | Halo imp | X (from Floor 925, the Lower Stairs on the long way) | An imp that has worked out that one ember is easy to dodge and ten are a fence. It hangs in the air and breathes out a ring of slow embers all at once (`radial`), then goes quiet for four seconds. The hero's way through is the gap between two embers, not around them. | generated (`art/haloImp.json`, `Art/Enemies/HaloImp/`) |
| `whirlImp`, `whirlEmber` | Whirl imp | X (from Floor 920) | An imp that spins in place as it spits, the way the damned in the Wastes turn their one stone: two arms of embers that wind outwards in a spiral (`radial` with `rotationPerVolley`). Nothing aims at the hero; he has to read the spin and stay between the arms. | generated (`art/whirlImp.json`, `Art/Enemies/WhirlImp/`) |
| `seamImp`, `seamEmber`, `seamMark` | Seam imp | X (from Floor 935) | An imp that slips through the seam in Hell's loop, like the Blink imp, but it comes out **behind** the hero (`teleport`, `behind`): a teal mark first, 0.7 s, then it is there and spits two quick embers into his back. The Blink imp lands beside him; this one wants him to turn around. Not damned: no loop shards. | generated (`art/seamImp.json`, `Art/Enemies/SeamImp/`) |
| `slagTortoise`, `tortoiseEmber` | Slag tortoise | X (from Floor 915) | The Magma King's tollkeepers: a slow beast with a cooled shell of slag on its front half, that plods back and forth along the stairs spitting slow embers ahead of it. Shots from the front (`frontShield`) are spent on the shell; its back is bare. The Bulwark imp carries a slab; this one *is* the slab, and it can only be got round by running past it. | generated (`art/slagTortoise.json`, `Art/Enemies/SlagTortoise/`) |
| `magmaGeode`, `geodeBit` | Magma geode | X (from Floor 945) | A lump of cooled slag with a lava core that drifts at the hero and, when it is shot open, splits into two **small geode bits** that dart at him on their own (`onDeath` spawns). Unlike the Thatch cinder's sparks, the bits can be shot, one hit each. Shoot it early and far away. | generated (`art/magmaGeode.json`, `Art/Enemies/MagmaGeode/`) |
| `mineBeetle`, `emberMine` | Mine beetle | X (from Floor 940) | A beetle the size of a dog that crawls along the floor leaving smouldering eggs behind it (`spawner`): `emberMine`, which sits still, arms when the hero comes close and goes off after 0.7 s (`explodeNear`). The mines fade by themselves after fifteen seconds, and a mine can be shot from a distance. Rewards the player who keeps off the floor and shoots the mines first. | generated (`art/mineBeetle.json`, `Art/Enemies/MineBeetle/`) |
| `lensImp` | Lens imp | X (from Floor 905, the Heat Stairs) | An imp that picked up a splinter of the lens the Overlord lasered the meadow with and holds it out like a lantern. A thin red line shows 1.2 s ahead where the beam will start, then the beam sweeps (`laserSweep`) across the stairs. The hero's answer is a jump or a duck below the line's end, never a long stand. | generated (`art/lensImp.json`, `Art/Enemies/LensImp/`) |
| `pyreCaller` | Pyre caller | X (from Floor 910) | A hunched imp elder that burns incense of the Wastes' own cinders: it holds up a fist of fire and a living cinder crawls out of it every five seconds (`spawner`, at most two alive). It never throws. Shoot it first or be drowned in sparks. | generated (`art/pyreCaller.json`, `Art/Enemies/PyreCaller/`) |
| `slagHound` | Slag hound | X (from Floor 930) | The Magma King's hunting dog: a lean hound of black slag with a glowing back, that circles the hero slowly until it crouches (0.7 s, the telegraph) and then lunges in a straight line (`charge`). It does not turn in the lunge. Sidestep it or jump it. | generated (`art/slagHound.json`, `Art/Enemies/SlagHound/`) |
| `deadeyeImp`, `deadeyeBolt` | Deadeye imp | X (from Floor 950, Halfway Basin) | An imp that sits far off and takes its time. One thin white-hot bolt every four and a half seconds, **aimed where the hero will be** (`Aim.Predictive`) and fast. The Lead imp throws lazily at a run; this one is a sniper and punishes a straight line. Zigzag, or stand still and let it miss. | generated (`art/deadeyeImp.json`, `Art/Enemies/DeadeyeImp/`) |
| `plateDemon`, `plateShard` | Plate demon | IX (from Floor 886) | A Brimstone Demon that tore a slab of the Road's iron plating up and carries it in front of it, pacing back and forth behind it. Shots from the front are spent on the plate (`frontShield`); from behind it is bare. It spits two quick shards in a narrow fan over the top. The Bulwark imp carries a slab; this one is a wall that shoots. Sometimes drops a diamond. | generated (`art/plateDemon.json`, `Art/Enemies/PlateDemon/`) |
| `riftBomber`, `riftMark`, `riftBomb` | Rift bomber | IX (from Floor 880) | An imp that learned the Rift's trick from the cracks: it blinks to a violet-pink mark beside the hero (`teleport`, 0.8 s warning) and sets down smouldering bombs (`spawner`, at most two). A bomb arms when he comes within a step and goes off 0.9 s later (`explodeNear`); it can be shot from a distance. The bomber is the teleport mechanic with a payload. | generated (`art/riftBomber.json`, `Art/Enemies/RiftBomber/`) |
| `bounceLobber`, `bounceLump` | Bounce lobber | IX (from Floor 874) | A Brimstone lobber that stopped aiming for the spot and started throwing for the floor: its lump of magma bounces four times along the road (`projectile` `bounces`), keeping most of its speed, before it gives up. It squats high, so the lump comes down and then runs at him in hops. | generated (`art/bounceLobber.json`, `Art/Enemies/BounceLobber/`) |
| `chainWhelp`, `chainBrute`, `chainEmber` | Chained pair | IX (from Floor 868) | A whelp and a brute shackled to either end of one chain, spawned together. They share one life: a hit on either is dealt to both (`chainLink`), and when one dies the other dies with it. The brute is the plate-armoured end (`frontShield`) and spits slow embers; the whelp runs at him. Shoot the exposed end. The chain is drawn between them. | generated (`art/chainWhelp.json`, `Art/Enemies/ChainWhelp/`) |
| `risenDemon` | Risen demon | IX (from Floor 862) | A Brimstone Demon that has died on the Road before and found that the Road keeps. Killed, it lies still and untouchable for 2.2 s (`revive`, the `downed` effect), then gets up with 60% of its health, once. Breathes a short fan of fire like the rest. | generated (`art/risenDemon.json`, `Art/Enemies/RisenDemon/`) |
| `fuseWhelp` | Fuse whelp | IX (from Floor 855) | A whelp with a stick of Road-rock strapped on and a fuse lit. It runs at the hero as fast as the Road allows and goes off when it gets close (`explodeNear`, 0.55 s). Shoot it early; it never shoots. | generated (`art/fuseWhelp.json`, `Art/Enemies/FuseWhelp/`) |
| `veilDemon`, `veilMark`, `veilEmber` | Veil demon | IX (from Floor 845) | A slim demon behind a shimmering veil and a small shield (`frontShield`), pacing. Every five seconds it blinks to the hero's side after a violet mark (`teleport`); the shield faces the way it walks, so the blink is what lets him get behind it. Spits embers forward. | generated (`art/veilDemon.json`, `Art/Enemies/VeilDemon/`) |
| `slabSniper`, `slabBolt` | Slab sniper | IX (from Floor 835) | An imp crouched behind a narrow upright slab on a high ledge, one eye in the slit. A thin white bolt every 4.2 s aimed where the hero will be (`Aim.Predictive`, fast). The slab (`frontShield`, a narrow arc of 50) swallows shots that come level; a shot from steeply below gets past, so he has to come under it. | generated (`art/slabSniper.json`, `Art/Enemies/SlabSniper/`) |
| `loopWraith`, `wraithEmber` | Loop wraith | IX (from Floor 825) | A shroud of Hell's loop with the loop showing through. Shots go through it (`rewindSnag`: the shot is spent, a chime says so). Only a rewind catches it: when the hero lets go of R it is snagged in the loop for 4 s (a bar over its head counts down, it glows) and takes damage like anything else. The only enemy that asks for the hero's one power. Pays well (three diamonds). | generated (`art/loopWraith.json`, `Art/Enemies/LoopWraith/`) |
| `bastionBrute` | Bastion brute | IX (from Floor 815) | The biggest wall on the Road: black plates riveted over cracked rock, a wide shield (`frontShield`, arc 120) and a short fan of fire. When it falls it gets back up once (`revive`, 50%, 2.4 s down). Patience, and a way round the shield, twice. Pays two diamonds. | generated (`art/bastionBrute.json`, `Art/Enemies/BastionBrute/`) |
| `ricochetImp`, `ricochetEmber` | Ricochet imp | X (from Floor 918) | An imp that throws Hell's habit: an ember that doesn't stop. Nothing in Hell is allowed to just end, so its ember bounces off the floor three times before it gives up (*Hell's tricks*). Throws slow, from above, so the bounce can be read. | generated (`art/ricochetImp.json`, `Art/Enemies/RicochetImp/`) |
| `blinkImp`, `blinkMark` | Blink imp | none yet (test bench) | An imp that slips through the seams of Hell's loop, the way the damned hiccup, but on purpose. It shows a violet mark next to the hero, stands still for 0.7 s, then is there. Not damned: no loop shards. | generated (`art/blinkImp.json`, `Art/Enemies/BlinkImp/`) |
| `bulwarkImp` | Bulwark imp | none yet (test bench) | An imp that carries a slab of slag in front of it (a bar of light on its facing side): shots from the front are spent on the slab. It paces; when it turns away, its back is bare. The first enemy that makes the hero move to shoot. | generated (`art/bulwarkImp.json`, `Art/Enemies/BulwarkImp/`) |
| `leadImp` | Lead imp | none yet (test bench) | An imp that has watched the hero run and throws where he is going, not where he is. Standing still beats it; running in a line is the worst thing to do. | generated (`art/leadImp.json`, `Art/Enemies/LeadImp/`) |
| `beamLens` | The Overlord's lens | none yet (test bench) | A shard of the lens the Overlord lasered the meadow with, left glowing on a ledge. A thin red line shows where it will start, 1.2 s later the beam sweeps across (a real beam: it stops at the floor and hurts where it crosses him). Not a creature: nothing to kill, only to get out of the way of. | generated (`art/beamLens.json`, `Art/Enemies/BeamLens/`) |
| `fighter` → Drooler | Drooler | V | Cave beast; Death2 is it eating you. | `Fighter*` (exists) |
| `sentinel` → Lurker | Lurker | V | Crouching cave hunter; Death3. | `Level3Enemy*` (exists) |
| `chaser`, `swarmer`, `exploder`, `sludge`, `slimeGlob`, `slimeSplat` | (retired) | none | Slime enemies. Not in any level once the levels are rebuilt. Keep the files until nothing references them. | slime |
| `gunner`, `drone`, `sprayer`, `spinner`, `brute` | (to recast) | | Generic tinted variants from the arcade version. Each gets a Circle and a reason, or is retired. | generated (`art/gunner.json`, `Art/Enemies/Gunner/`) |

### The hero sizes them up (built 2026-10-25)

Hell has a lot of things in it and the hero has no time to learn them properly, so he does what a tired person does:
he looks at it once, says what it is and what it can't do, and moves on. **The first time a kind of enemy appears in a
save**, the hero says its name and one small hint at its weakness, in one short line (the subtitle shows the same
words, `IMP: Slow embers. Keep moving.`). He never repeats himself: not for the second imp, not after he dies, not after
a rewind, not on the next run from a Waystone; only a New Game makes everything new to him again. He says nothing during
a scene (when something bigger than him has hold of the loop) and nothing about the Wardens and floor bosses, who
introduce themselves. Hazards are not creatures and get no line. Voice `player` (`vo_hero_hint_<entity>`), deadpan,
under ten words of hint, never a joke about work. Recipe: GameplayCookbook "The hero sizes them up".

| Entity | Name and hint |
|---|---|
| `imp` (and every plain imp that extends it) | Imp: Slow embers. Keep moving. |
| `cinder` | Cinder: Drifts at you and pops. Shoot it early. |
| `impSack` | Sack imp: Too busy to throw. Shoot the sack. |
| `impSwing` | Swinging sack imp: Rides the chain's beat. Time the swing. |
| `impCutter` | Chain-cutter imp: Hacks the chain. Shoot it first. |
| `cinderThatch` | Thatch cinder: Bursts into sparks. Dodge them. |
| `impGuard` | Imp guard: Fans three embers. Find the gap. |
| `brimstoneDemon` (and `demonFirst`) | Brimstone demon: Fire breath, short range. Keep your distance. |
| `demonWhelp` | Brimstone whelp: Small, fast, two hits. Back up and shoot. |
| `demonLobber` | Brimstone lobber: Lobs brimstone. The glow shows where. |
| `whelpNest` | Whelp nest: Spits whelps until shot shut. |
| `ricochetImp` | Ricochet imp: Its ember bounces. Count the bounces. |
| `blinkImp` | Blink imp: Watch the violet mark. It lands there. |
| `bulwarkImp` | Bulwark imp: Slag shield in front. Shoot it from behind. |
| `leadImp` | Lead imp: Throws where you're going. Stand still. |
| `watcher` | Watcher: Shoot as its lid opens. |
| `haloImp` | Halo imp: Ring of embers. Find the gap. |
| `whirlImp` | Whirl imp: Spiral embers. Weave with the spin. |
| `seamImp` | Seam imp: Blinks behind you. Watch the mark. |
| `slagTortoise` | Slag tortoise: Armored front. Shoot its back. |
| `magmaGeode` | Magma geode: Splits in two. Shoot the bits. |
| `mineBeetle` | Mine beetle: Leaves mines. Shoot them early. |
| `lensImp` | Lens imp: Red line, then a sweep. Move. |
| `pyreCaller` | Pyre caller: Calls cinders. Shoot it first. |
| `slagHound` | Slag hound: Crouches, then lunges. Sidestep. |
| `deadeyeImp` | Deadeye imp: Leads its shot. Zigzag. |
| `plateDemon` | Plate demon: Plate in front. Get behind it. |
| `riftBomber` | Rift bomber: Blinks in, drops a bomb. Step off. |
| `bounceLobber` | Bounce lobber: Its lump bounces. Wait it out. |
| `chainWhelp` (and `chainBrute`) | Chained pair: Linked. Hurt one, hurt both. |
| `risenDemon` | Risen demon: Gets up once. Shoot it twice. |
| `loopWraith` | Loop wraith: Shots pass through. Rewind snags it. |
| `veilDemon` | Veil demon: Shielded, and it blinks. Hit its back. |
| `slabSniper` | Slab sniper: Slab in front. Shoot from below. |
| `fuseWhelp` | Fuse whelp: Runs, then blows. Shoot it early. |
| `bastionBrute` | Bastion brute: Wide shield, gets back up. Patience. |

Not named yet: the retired slime-era arcade enemies (`chaser`, `swarmer`, `exploder`) and the arcade variants still
to be recast (`gunner`, `drone`, `sprayer`, `spinner`, `brute`, `fighter`, `sentinel`); they get a line when they get a
Circle. New enemies come with a `displayName`, a `hint` and a line from now on (`soi-enemy`).

### The Codex (what the hero has seen, built 2026-10-25)

The hero keeps no diary, but the Shaft remembers for him: **the Codex** is the list of everything he has laid eyes on,
opened from the main menu and the pause menu. Four tabs: **Enemies**, **Bosses** (the Wardens and floor bosses),
**Weapons** and **Characters**. A page holds a few entries, each with a picture, its name, the hero's note (one deadpan
line, never a joke about work), the weakness (the same hint he says the first time it shows) and the tally: **times met,
times killed**. A weapon shows whether he has it. What he has not met is a **black silhouette called "???"** with no name,
note, weakness or numbers: the Codex never tells him what is coming. Met is the same memory as "The hero sizes them up"
(`EnemyHints`): an enemy he has named is in the Codex, and he can meet one in a scene without a subtitle and still find it
there. The tallies are kept with the save like the hints (a death or a rewind does not take them back; one rewound kill is
counted once); only a New Game empties the Codex. The Wardens do not introduce themselves with a hint line, but they are
named here, with a note.

Characters: the hero (always), the Overlord (the meadow's laser, always), the Announcer (always) and Yooba (once met).

### Achievements (what Hell lets him keep, built 2026-10-25)

Hell keeps score, and the Announcer reads the score out whether the hero wants it or not. **Achievements** are the
things Hell has noticed him do: 76 of them, in a fifth Codex tab (**Achievements**), each with a Hell-flavoured name,
what it asks for (one deadpan line, never a joke about work), a **title** and a count. Unlocking one drops a small
card from the top of the screen ("ACHIEVEMENT UNLOCKED", its name, "New title: ...") over anything he is doing, then lifts away; they queue if
several come at once. The titles are his to wear: tapping an unlocked row in the Codex puts its title on the hero (the
Codex's Characters page then reads *The Hero, Bullet Hugger*); tapping again takes it off. He wears one at a time.

The list covers: demons killed (1 to 2,000, in total and on a single floor), different kinds killed, a run of named
enemies (the imps by the hundred, the hounds, tortoises, beetles, bombers, deadeyes ...), every Warden and floor boss
(the Imp Foreman, Pile Keeper, Magma King, Road Breaker, Carver, Spire Fiend), floors cleared without a hit (and a boss
the same way), rewinds (the first, fifty, five minutes' worth, and **undoing a death**), deaths (the first, 25, 100, and
being killed by an imp), grazes and an S rank, floors climbed, the secret rooms and gauntlets behind the doors in the
wall, shortcuts taken and declined, Yooba's shop (the first buy, 100 and 1,000 diamonds spent, ten different goods,
diamonds collected), Grudges, weapons found (and the shotgun, the chain-whip, the fists by name), and the moves
(the dash, the wall-jump, the Hook). A few are **secret**: they show as ??? with one hint until he has them.

Rules: nothing is paid out (no diamonds, no juice: they are a record and a title, not a reward); rewind never counts
a kill, hit or graze twice and never takes an unlock back; a scene or test bench counts nothing; the endless and daily
floors do not count as a floor cleared. The record is kept with the save like the Codex (a death or a new run keeps it);
**only New Game empties it**, titles included. In the data: `ui/codex.json` `achievements` (id, name, description, title,
`stat`, `target`, `secret`, `hint`); the counters are fed by `AchievementTracker` (from the level's telemetry), the shop and the
diamond purse, and saved by `Data/Achievements.cs`. A new achievement is a row in the file naming one of those counters
(`kill:<enemy display name>`, `weapon:<id>`, `weaponHeld:<id>` or a counter listed in `AchievementTracker.KnownStats`).

## Style: grazes, the meter and the floor's rank (built 2026-10-25)

Hell is a show and the show keeps score. A shot that goes past the hero close enough to ruffle his coat without touching him is a **graze**, and Hell's
audience loves a graze: the announcer notices, the crowd gasps, and the hero gets paid for nerve. This is feel and a reward, not story: no new place,
no new character (the crowd is Hell's audience, never named, never seen), and **no office or work jokes** in any line.

- **A graze**: a hostile shot passes within 0.35 world units of the hero's body without hitting him. It counts once the shot has gone by its closest point
  (a shot that goes on to hit him is not a graze). A spark where it passed, a short ticking sound that climbs in pitch with the streak. Nothing else is noisy.
- **The style meter** (bottom of the screen, above the progress bar) fills with every graze (more for a longer streak) and drains by itself after a short
  grace. Being hit empties it. Crossing a tier pays once: **COOL** (a little rewind juice), **HOT** (1 diamond, more juice), **SICK OF IT** (2 diamonds,
  more juice). The payout is paid once per play: a rewind that undoes the graze takes the credit away (meter, streak and graze count are rewound like
  everything else), and replaying it does not pay twice.
- **Lines**: the announcer on streaks of 5, 10 and 20 and on SICK OF IT, the crowd gasping and roaring with him; a no-hit floor gets the announcer and the
  crowd on the rank screen. Subtitle first (the line's text is its audio.json prompt), voice when its clip exists; one line at a time, never twice for the
  same graze, never more often than every 6 s.
- **The rank screen** (the floor's results): time against the floor's **par** (its expected length), hits taken (a **no-hit floor** is flagged), grazes,
  kills, rewind use and diamonds, and a rank **S, A, B or C** (points: 30 for par time, 30 for no hits, 20 for grazes up to a goal of 20, 20 for no
  rewinds). The best rank per floor is kept for good (until New Game) and the screen says when a floor is a new best.
- Data: `game.json` `style` (distances, meter, tiers and their payouts, the lines, the rank points), lines' words and voices in `audio.json`
  (`vo_style_*` announcer, `vo_crowd_*` crowd; the `graze_tick` effect), words on the screen in `ui/theme.json` `texts`.

## Again: Endless Floor, the Daily Challenge and New Game Plus (built 2026-10-25)

Hell is repetition, so Hell has a rerun. Once the hero has made it through the first Warden he understands what the place is: not a climb with an end but a
show that runs again. Three ways to go round, all on the main menu, all in the hero's and the announcer's voices, and **no office or work jokes** anywhere in them.

- **Endless Floor** (*"ENDLESS FLOOR"*): the Ember Wastes with no top. Hell's loop sends the pushes one after another and the counter climbs a floor
  for every push fought off; the pushes get bigger and meaner (the Circle's own ramp over the first dozen, then Hell simply turns it up each push). The
  announcer: *"THE FLOOR NEVER ENDS! NEITHER DO THEY!"* Hell keeps score: **the best floor climbed is saved** (a new best is called out when he passes
  the old one). A death plays it again from Floor 1,000; the loop is the point.
- **Daily Challenge** (*"DAILY CHALLENGE"*): one floor a day, the same for everyone who plays it that day. The date is the seed: same date, same pushes
  in the same order, same **modifier** (Hell's twist of the day, picked from the list in `game.json` `replay.modifiers`: thick skin, trigger-happy, a
  crowd, fast hell, short breath, big ones). The floor is ten pushes long (1,000 to 990); it scores floors climbed, kills, grazes and a bonus for finishing.
  The **local table** keeps the day's best scores on this device (nothing is sent anywhere). Announcer: *"TODAY'S SHOW! SAME FOR EVERYONE!"* Hero: *"Same
  as yesterday, then."*
- **New Game Plus** (*"NEW GAME+"*, offered once the Magma King has fallen, i.e. the Waystone on Floor 901 is lit): Hell takes the climb back and gives it again.
  The hero starts again at the Landing, Floor 1,000, with the Waystones, floors and Grudges reset, but **keeps what he made and found**: every
  weapon he unlocked, Yooba's goods, the diamonds, what the Codex knows and his best floor ranks. Everything in Hell hits harder, shoots faster and has
  more health for every cycle (+35% health, +12% fire rate, +5% speed per cycle, a bigger crowd every second cycle, up to cycle 9). Announcer: *"FLOOR
  1,000! AGAIN! NOW WITH MORE HELL!"* Hero: *"Again. Fine."* The first climb is cycle 1; the New Game+ card says which cycle he is on and which he would start (*"Again (Cycle 3)"*).
- These are the Ember Wastes' floors made to repeat (`generateFloors` with a ramp, `climbs`, and an `escalation` past it) with the level seed taken from the
  date; every roll comes from the seeded streams, so a rewind replays the same floor. Data: `game.json` `replay`, the levels `EndlessFloor` and
  `DailyFloor`, the best/table/cycle in the save (`ReplayModes`). A death in Endless or the Daily is **not a run's end**: no Shaft, no Waystone, it plays again.

## The HUD and menus (Hell's showmanship)

Hell loves a show, and the announcer's cabinet is the show's frame: nothing on screen just appears. This is feel, not story: no new words
(every label is an existing `ui/theme.json` text), and none of it touches the simulation or the rewind.

- **Inked widgets (Nov 12)**: the runtime TMP chips are gone. Hearts are **ember vessels** that crack apart over ~0.3 s on a hit and refill with a glow on heal; rewind juice is a **tape reel** that spins and drains while R is held and clicks when juice returns; diamonds sit beside a **gem** that pulses within 0.2 s of a pickup; the floor counter is a **brass elevator dial** whose needle ticks down each new floor. Grouped in two safe-area corners (health + reel top-left, gem + dial top-right). On a phone the rewind button is the one big bottom-right control (≥72 px) wearing the same reel art. Atlas: `Resources/UI/HudInkAtlas` (Gen sheet, with procedural stand-ins until it lands). Playtest `hud-ink`.
- **Popups** (pause, results, the Codex, settings, the Grudge picker) fade and rise in (0.28 s) and fade out (0.16 s) instead of vanishing (`PanelAnim`, `Overlay.Create`/`Overlay.Close`).
- **Numbers count**: the diamond counter, the shop's purse and the results' stats tick up or down to their total (`NumberTicker`); the results count one row after another.
- **Pickups and bonuses answer**: a diamond sends a "+N" up from the counter and pops it; a heart refilled pops the vessel with a "+N"; a rewind orb or clean kill sends "+Ns" / "CLEAN KILL +0.5s" from the rewind bar (`HudFloaters`), and in-world bonuses and penalties rise on `WorldEffects.PopText`. Every floating popup and the HUD `REWIND` readout wear a heavy dark plum outline + drop shadow over a semi-opaque dark plum backing pill with a warm-grey rim (`#C9B8A6` subtitle family), pop in larger for the first ~0.3 s before easing to normal, hold full opacity for 0.9 s before fading, lift dark colours so they read clearly on bright meadow sky/grass and black slag alike, and stack into separate vertical slots so successive popups never overlap (playtest `rewind-juice`).
- **Weapons**: switching drops the new name into the weapon chip with a punch of size.
- **Bosses are introduced**: when a boss bar shows, a flash lights the screen, the boss's name lands from large, and the bar fills from empty (1.3 s) (`GameplayHud.BossIntroSeconds`).
- **Between floors**: each new floor sweeps a band of lava light up the screen; a floor stage's results close on a black cover with the floor's number, and the next scene opens it (`ScreenTransition`; it never holds the player on black for more than 8 s).
- **Fit**: placed through `MenuCanvas`/`MenuResponsive`, so it holds at 1920x1080 and 1080x1920, inside the safe area; on a 390x844 phone the two HUD corners cover at most 12% of the playfield (playtests `ui-polish`, `hud-ink`).

## Incoming warnings (Hell gives you a heads-up, quietly)

Hell shows up late but never out of nowhere (hazards and enemies start just off screen), and the cabinet does the decent thing: when a fireball or a creature
is about to come in, a faint tick glows on the edge where it will cross, about a second and a half ahead. It is not a siren. No sound, no flash, no
shake, no words: the hero is sick of it, not blind to it (2026-10-27; playtest `incoming-warnings`).

- A marker is a soft, see-through tick just inside the edge at the crossing point, orange for a shot, rose for a creature, from 25% opacity at 1.5 s out to 70% at 0.3 s, a touch deeper as it nears. It fades in over a quarter-second and out in a tenth the moment the thing is in view.
- Only hostile things that will really come in get one (position and velocity, worked out fresh every frame): never what moves away, passes beside the view, stands still, is a prop, a pickup or Neutral. A crowd shares a marker (Floor 998's rain is one soft band across the top, not a wall of arrows); at most four show, soonest arrival first.
- A rewind clears them while it runs and they come back from the replayed floor; a pause freezes them. Nothing is saved.
- Data: `game.json` `incomingWarnings` (lookahead, cap, merge, opacities, size, tints). The player's switch is **Settings > Accessibility > Incoming warnings** (on by default). Code: `IncomingSet` (the maths) and `GameplayHud.Incoming.cs` (a pool of Images behind the HUD's widgets, nudged off any button).

## The feel of a hit (Hell's cabinet hits back)

Hell is an arcade cabinet, and a cabinet sells every blow: the world stops for a beat when something lands hard, monsters
flinch, the hero's gun kicks, and Hell's fire actually shines. This is feel, not story: no new words or sounds, none of it
touches the simulation or the rewind, and all of it is tuned in `game.json` `feel` (2026-10-25; playtest `game-feel` / `vfx-kit`).

- **The hit-freeze**: a strong blow (2+ damage: the fists' hook, the whip's last lash, a crit), a kill, an explosion or the hero
  getting hurt stops the world for 45-80 ms; a Warden's killing blow holds it for about a fifth of a second. It is Unity's clock
  that slows, never Hell's: every tick of the floor is the same, only a moment later, so a rewind replays exactly what happened.
  Never two freezes within 0.3 s, so a crowd never stutters; a rewind or a pause ends one at once.
- **Monsters flinch**: a hit flashes the monster white and squashes it, springing back, pushed a little away from the blow
  (the sprite moves, never its body). Bosses flinch less; they are big and proud about it.
- **The hero's kick**: every shot throws his sprite back a few hundredths of a unit from his aim and squeezes it; a swing of the
  fists leans him into it. He doesn't comment. He has been shot at before.
- **Hell's fire shines**: fireballs and embers glow their own color, lava falls and geysers light the air around them, the lava
  sea lights the bottom of the screen, the Magma King smoulders, and explosions and death pops flare. Soft additive sprites
  (`Glow`), not 2D lights: the game runs the built-in renderer, and on WebGL a few dozen batched quads cost far less than a lighting
  pass. At most 48 on screen at once.
- **Hand-made flipbook bursts** (2026-11-12, Dead Cells-style): every shot, impact, enemy hit, family death (imp soot, cinder ash,
  sack/Floor 997 threats), diamond pickup, hero hurt, hero death, dash smear, graze streak and rewind-start ripple plays a short
  inked flipbook from one atlas (`FeelVfx` / `Resources/Effects/FeelVfxAtlas`, pooled — never Instantiate in play). Diamonds arc to
  the HUD gem within 0.6 s. Phones draw the same sheets smaller and cap the pool lower so 390x844 holds 30+ fps. Gen sheets replace
  the procedural stand-ins; playtest `vfx-kit`.

## Every floor has its own light (built 2026-11-11)

Hell is not one red cave. The Meadow and Floors 1000–997 each wear their own mood of light (Docs/lighting.md; `game.json`
`lighting`; playtest `lighting`), in the Dead Cells sense: torchlight and glowing pools on hand-painted walls, a grade per
place.

| Place | Grade |
|---|---|
| The Meadow | Warm dusk that turns red when the sky opens |
| Floor 1000 | Sulphur orange |
| Floor 999 | Furnace red |
| Floor 998 | Bone grey-violet |
| Floor 997 | Obsidian with magenta highlights |

The built-in renderer has no URP volumes: a camera fullscreen grade (bloom on emissive at threshold ≈ 1, vignette, colour
grade, film grain on desktop) sits on top of a multiply darkness layer, and additive light sprites (lava flicker, warm
torches, a rim on the hero, a small light on enemy bullets) punch through it within about 2 units. A phone tier (touch +
short side ≤ 500 px) runs bloom at half resolution, drops grain, and caps lights at 8 so 390×844 holds 30+ fps. Rewind's
scanlines and glitch stay on the letterbox / frame, off the playfield.

## Sound that never grates (takes, ducking, beds) (built 2026-11-11)

Every verb sounds like something: a shot, a hit, a kill, a graze, a diamond, a jump, a landing, a dash, a rewind going and stopping. Each
has three takes that never play the same one twice in a row and a pitch that moves about 6% (voices never move), a shot and a kill land as
layered pops in the same moment (a short `hit_pop` / `kill_pop` over the enemy's own hurt and death sounds) and at most four copies of
one effect sound at once. Voices always sit over the music: Music ducks 8 dB (Ambience 4 dB) in 80 ms for as long as a line is spoken.
Pause muffles Music −10 dB and mutes SFX; at one heart a heartbeat loop plays under Music −4 dB; rewind ducks SFX −6 dB so the whoosh
leads. Every place has a bed of its own: the Meadow's wind, a far rooster and a mill that turn to fire and a village alarm bell when the
sky opens (it is the same field, burning), Floor 1000's lava and creaking stone. The design and the numbers are in Docs/SoundAudit.md
"Takes, buses and beds" and docs/audio-mix.md.

## Readability is fairness (art direction and bullets)

In a dodger every death must be the player's. So a hostile shot is never lost in the picture: each wears two thin rims, a pale one
beside it (the level's or Circle's `shotRim`; the Ember Wastes' is a hot cream) and an ink one outside it, so it reads on a bright
meadow and on black slag alike. Settings > Accessibility still adds the heavier outlines and the one-colour high-contrast shots on top.
Every level has at least two layers of depth: the Circle's `ambience` (silhouettes and weather at their own parallax) or, on a level
without a Circle like the Meadow, the script's own `ambience`. Palette rule: backdrops stay dark and low in saturation, hazards and shots
are the only hot orange/yellow on screen. Playtest `readability` (levels 1-5). Still to do: per-bullet-family silhouettes (a ring, a
shard, a spiral) and a second pass on the Cracked Road and Rune Bridges once their art is regenerated (GPU).

**Enemy wind-up (Meadow and Floors 1000-997).** Every hostile that attacks on those floors holds a Cuphead-style wind-up of at
least 0.3 s before the shot or lunge: the imp coils its ember-hand back with a glow, cinders (and thatch / brute / spark) swell
and flare, sack imps glow before a bump. Specs and the art bible (`docs/art-bible.md` §9); playtest `enemy-ink`.

## Hell's air (weather and the life far away)

Hell is repetition, so even its air repeats: every Circle has its own weather and its own things going about their business a long way off, and
none of it has anything to do with the hero. It is there so the climb looks like a place. (2026-10-25; `content/circles/*.json` `ambience`.)

| Circle | Weather | Far away |
|---|---|---|
| X, the Ember Wastes | grey ash sinking, sparks flicking up off the lava, cinders falling (from Floor 990) | cinder bats crossing, imps trudging a far ridge, a small soul running from something bigger (from Floor 996) |
| IX, the Cracked Road | grit shed by the spires and blown along the road, sparks from the cracks, ash from the Rift | shard-wings over the Rift, a demon walking the far road, a whelp chased by a bigger demon |
| VIII, the Rune Bridges | runes lifting off the walls, pale rune dust, cold sparks | watchers drifting in the void, a statue walking a far bridge, a wisp chased by a hound |

The deeper Circles get theirs when they are built: a new Circle's `ambience` is part of building it (two or three kinds of weather, at least two kinds
of life, a depth layer or two, a set no other Circle has). The chases are never explained and never resolved: they cross, they leave, they come back.

### Living screens (Meadow, Shaft, Floors 1000-997)

Every early screen of Hell moves on its own so the world feels alive (2026-11-12; Ori-style drifting air). A level's own
`script.ambience` overrides the Circle when set. Each pack has `liveWeather` (players see the bits; Circle defaults stay
quiet so deeper floors don't return to "far too many particles"), ≥3 weather kinds, timed `events` every ~10–15 s, and a
**foreground silhouette** row on `Foreground1` at α ≤ 0.6 (grass/fence in the Meadow; chains, cages, stalactites below)
that parallaxes faster over the play area without hiding bullets. Phone tier: half the bits (`Quality` 0.5), no heat
shimmer. Playtest `living-air`.

| Place | Particles | Timed events | Foreground |
|---|---|---|---|
| Meadow | pollen, motes, raid embers | birds scatter + smoke when the sky opens (`meadow:raid`); chimney steam | grass and fence posts |
| Shaft | wind streaks, falling debris, ash | debris fall every 10–15 s | chain silhouettes on the window |
| Floor 1000 | rising embers, ash, heat dust + heat shimmer (desktop) | glow pulse, steam, ember spit | hanging chains |
| Floor 999 | furnace sparks, steam, slag grit | steam vents, furnace belch, spark burst | cages |
| Floor 998 | ash snow, soul wisps, bone dust | soul drift, ash cascade, choir glow | hanging cages |
| Floor 997 | obsidian dust, magenta sparks, ash | background lava geyser every 10–15 s, spit, dust fall | obsidian stalactites |

## Hell's tricks (five mechanics)

Hell is repetition, so its monsters have a few tricks and use them forever. Five are built as JSON abilities so floors
can mix them (2026-10-25). They live on the **mechanics test bench** (`content/scenes/mechanicsTest.json`, the
`mechanics` playtest) until a Circle is given a reason to use them; rule 1 of *Rules for enemies* still holds: no
floor gets one without a reason written here first.

| Trick | In the story | Ability / option |
|---|---|---|
| **The ember that bounces** | Nothing in Hell is allowed to just end. A shot that hits the floor goes on, a few more times. | `projectile` `bounces` (Ricochet imp's ember) |
| **The blink** | The loop hiccups and the imp is somewhere else. It always shows where first: a mark, a flash, 0.7 s. | `teleport` (Blink imp) |
| **The front shield** | An imp with a slab of slag held out front. The hero beats it by being behind it, which means running, which Hell would rather he didn't. | `frontShield` (Bulwark imp) |
| **The Overlord's lens** | His lasers, small: a warning line, then a sweep. Where the beam crosses the hero it hurts; the floor stops it. | `laserSweep` (the lens) |
| **The lead** | An imp that has watched the hero run. | `shoot` `aim: "Predictive"` (Lead imp) |

Tone: the announcer calls them out in the bench's banners (*"HELL'S EMBERS DON'T STOP. THEY BOUNCE."*), the hero says
nothing, and no joke is about work.

### The Wastes' bestiary (built 2026-10-25)

The tricks have a reason now: the Magma King's Wastes grew ten more kinds of monster for the climb, each built on one trick or
one old habit of Hell, each with its reason in the *Roster* (the Halo, Whirl, Seam, Slag tortoise, Magma geode, Mine beetle, Lens,
Pyre caller, Slag hound and Deadeye). They join Circle X's enemy table (`emberWastes`) at staggered floors from 950 down to 905,
one at a time, so the long way's generated floors and the Heat Stairs bring them in alone. The Ricochet imp joins them from 920.
The test arena is `content/scenes/wastesBestiary.json` (playtest `wastes-bestiary`): ten stations, one creature each, the
announcer naming the idea. Nothing in the bestiary is a slime and nothing in it is a joke about work. Art: specs in
`Assets/GameConfig/art/`; until the sheets are generated they wear the imp's or the demon's art, tinted (rule 5).

### The Cracked Road's bestiary (built 2026-10-25)

Circle IX's Road grew ten more kinds of monster, and where the Wastes' ten each had one trick, these **combine two**: a shield that
shoots, a bomber that blinks, a lump that bounces, two bodies on one chain, a demon that gets up, a ghost that only a rewind can touch.
Hell is repetition, and the Road is where Hell starts repeating *itself*. They join `crackedRoad`'s enemy table at staggered floors from
886 down to 815, one new kind at a time (the table is used by generated floors and the long way; the authored floors 900-892, 860-852 and
810-802 keep their own scripts).

| Idea | In the story | Kinds |
|---|---|---|
| Shielded shooters | The Road is paved in iron, and some of it walks. | Plate demon, Veil demon (+ blink), Slab sniper (+ predictive shot) |
| Teleporting bomber | The Rift's trick, with a payload. | Rift bomber |
| Bouncing lobber | The Road gives everything back, a few times. | Bounce lobber |
| Chain-linked pair | Two shackled to one life. | Chained pair (whelp + brute) |
| Revive once | The Road keeps what it kills, once. | Risen demon, Bastion brute (+ shield) |
| Dies only to a rewind trick | Hell's loop with the loop showing: the hero's own power is the key. | Loop wraith |
| Charge + bomb | A whelp with a fuse. | Fuse whelp |

Three new JSON abilities carry the new ideas (`Gameplay/Content/RosterMechanics.cs`, all saved for rewind): `revive`, `chainLink`
and `rewindSnag` (the snag window opens off `GameClock.RewindsEnded`, a counter that is not itself rewound, compared with the value
each wraith saved). The test arena is `content/scenes/roadBestiary.json` (playtest `road-bestiary`): ten stations, one creature each;
the Loop wraith's station gives the hero 10 s of rewind juice and the bot holds R once shots have passed through it. Each kind has a
hint, a hero voice line (`vo_hero_hint_<id>`), hurt/death (and attack) sounds, loot and a table row. Nothing in it is a slime and nothing is
a joke about work. Art: specs in `Assets/GameConfig/art/` (`plateDemon`, `riftBomber`, `bounceLobber`, `chainWhelp`, `chainBrute`, `risenDemon`,
`loopWraith`, `veilDemon`, `slabSniper`, `fuseWhelp`, `bastionBrute`); until the sheets are generated they wear the Brimstone demon's or the imp's
art, tinted (rule 5). Pricing for the art (`hawkesgen_estimate`: 99 poses + 132 clips): about $9.63 of GPU, $14.60 with the idle tail.

## Weapons (the hero's hands)

The hero starts with one thing: the **rocket launcher**, the gun he has fired since the Meadow. On Floor 998 Hell takes it
(see *The Spill's theft*), and from Floor 994 on Hell hands him things instead. A weapon is a JSON file in
`content/weapons`; a floor gives or takes one with `giveWeapon` / `takeWeapon`, and a **pickup** unlocks one for good.

**What stays and what goes (J, 2026-10-25: "a pickup unlocks the weapon permanently").** A weapon found on a floor is
*his*: it is saved, he has it again after Hell kills him and in every later run, and only a New Game gives it back.
That is the one thing the roguelite loop does not take from him, and the story says why: Hell is repetition, and a
weapon he has fired is a weapon Hell has now seen him fire. What a *scene* gives (the fists on Floor 998) is for the run, like a
Grudge, and goes when the run ends. A scene can also take unlocked weapons away again for a while (Floors 998-994 take
everything but the fists, every run); they come back with the launcher.

| Weapon | In the story | How it plays | Found |
|---|---|---|---|
| **Rocket launcher** | His own gun. No ammo; it never runs dry because he never stops being annoyed. | Fires along the aim at 0.3 s; the spread Grudge fans it out. | His; taken on 998, back on 994 |
| **Fists** | What a man has when Hell has taken everything else. Two jabs and a hook, and each lands like he has been meaning to do it for a long time. No jokes about work: the joke is how personal it is. | Melee, no ammo, 1.2 reach: jab, cross, then a hook for double damage. He has to be right there. | Given on 998, for the run |
| **Spitfire** | A newt the imps use to light their lamps. Squeeze it and it spits embers. He holds a lizard; the lizard is the weapon. | A stream: a shot every 0.085 s (3.5x the launcher), 48 rounds that come back on their own (7 a second). One damage a shot; it empties in four seconds. | A bearer imp on 993 |
| **Ricochet Pistol** | Built by someone who was never happy with a straight line. | Slow (0.45 s), 2 damage, no ammo, and each bullet bounces off ground, walls and ledges three times: it hits what is round the corner. | A bearer imp on 992 |
| **Chain-Whip** | The Foreman's own whip, off the Foreman. | Melee with 2.8-3.5 reach (fists: 1.2-1.4): lash, snap, then a crack for double damage. Thin and long: it hits what is in a line, not what is above him. | The Imp Foreman, 991 |
| **Shotgun** | A slag-barrel blunderbuss off a guard post. One trigger, five opinions. | Five pellets in a 9 degree fan, range about 5, 0.8 s between shots, 8 shells that come back at one every 2 s. Point blank it is five hits. | A bearer imp on 954 |
| **Boomerang Halo** | A halo off something that fell a long way, one of the things the Pile Keeper keeps in his pile. He throws it; it comes back. Unlike everything else he has thrown. | Flies out, hangs, returns faster to him and is caught; goes through everything and hits it both ways. 0.75 s between throws. | The Pile Keeper, 951 |

Rules: the announcer says nothing about a weapon being *found*: a banner (*NEW WEAPON: SPITFIRE*) and the hero's own line
carry it, once, the first time. (He does shout when Hell *takes* the launcher: that is a show.) The hero's lines are deadpan
and about the thing in his hand, never about work. A finder is a **bearer**: an ordinary enemy of its floor that carries the
weapon and drops its pickup when it dies, or a boss that drops it as it falls; nothing is found lying about, so a weapon
is always something he took off someone. Picking one up again later only refills its ammo. A floor that gives or takes a
weapon says why in its own script. Switching: Q, the mouse wheel or the gamepad's top button; a button on the HUD on touch.

### In his hands (swaps and looks)

What he holds is **seen**, in every frame: each weapon has its own animation set (weapon JSON `hands`, made 2026-10-25 from his own
Idle frame and launcher videos, `art/hero.json`): he carries it in the same raised hand that carried the launcher. The launcher on his
shoulder; the Spitfire, a live orange newt held up by its belly strap; the Ricochet Pistol, a pale pistol with a blue eye; the Chain-Whip,
its grip raised and the chain hanging; the Shotgun, a slag-barrel gun on his shoulder; the Boomerang Halo, a gold ring held up like a discus;
the Slag Ladle, a long handle over his shoulder and a bowl of slag; the fists, a boxer's guard. Each walks, jumps, falls and lands its own
way, and attacks its own way standing, running and in the air. Every swap gets a clip: the launcher **leaving** him (Floor 998: yanked off
his shoulder, he looks at his empty hands, unimpressed), the launcher **coming back** (swung up onto the shoulder and checked), the **fists**
(knuckles cracked, guard up) and every other weapon taken out and checked. What he does with no weapon in particular (the dash, the walls,
the Hook, a graze, a hit, the deaths, the victory, the ladder) he does empty-handed.

### The Spill's theft (Floor 998 to 994)

On Floor 998, as the river carries him past the hay cart, an imp (the **thief**, a launcher-sized sack on his back)
bursts out of the hay, snatches the rocket launcher out of his hands and runs off down the river with it. The announcer
makes a show of it (*"HELL HAS TAKEN YOUR GUN! USE YOUR HANDS!"*). The hero: *"Oh, sure. Take the rocket launcher. Take it."*
and, fists up: *"Fine. Personal, then."* He fights with fists for **five floors**, 998 to 994, and Hell tunes them for it: the
sparks of the burning village keep coming up after him (cinders, one punch each) and, bigger, **brutes** (a cinder that
takes a hook and a jab), more of them every time he climbs; the chain imps and lava stay where they were, because
those floors were never about a gun. At the top of Floor 994 the thief is back on the landing, drops the launcher at
his feet and leaves in a hurry. *"There you are. You smell like imp."* From there the weapons come off Hell's own.

## Bystanders (the damned notice)

Hell is repetition, and the damned are its most repetitive people, but a loop that is brushed hiccups, and a soul whose
loop hiccups *notices* (2026-10-25). Until now they sat and did their one thing; a bystander is a damned soul that
also **watches the hero**: it turns to him when he comes close, flinches back from shots that go past it and from
anything that hurts him, and says one line about it. It can't be hurt and can't fight; it only reacts, and then goes
back to its place and its loop (`walkEntityTo`, `faceEntity`, `playEntityAnimation` and the `witness` trigger,
cookbook "Bystanders"). The same tools let an imp look up when a shot passes, or a guard step back when an ally goes
down, but nothing in a Circle does that yet: each use needs its reason written here first (*Rules for enemies*).

Voice: a soul speaks the way Hell makes everyone speak: it says the thing, then says it again, flatter, because that
is how a loop works. Short, never a joke about work. *"Oh. A visitor. ...A visitor."* when he comes near; *"That looked
like it hurt. ...That looked like it hurt."* when he is hurt next to it. Subtitles only for now (grey-blue, like the
soul); no voice is cast for the damned yet.

| Entity | Where | What it does |
|---|---|---|
| `bystanderSoul` (extends `damnedSoul`) | the test bench `content/scenes/bystanderTest.json`, none on a floor yet | Stands looking away. Hero within 4.5: turns to him, speaks. A shot passes within 1.6: hiccups, steps back 0.5, settles. Hero hurt within 6: hiccups, steps back, speaks (once). Drops nothing: the shard belongs to the plain `damnedSoul`. |

### Bystanders on the floors (2026-10-25)

The damned who sit on a ledge doing their one thing now include **five kinds that notice him**, each on its own floors
(`Tools/Levels/bystanders.py` writes them into the first phase of 21 floors: 999, 997-992, 960, 958, 955, 953, 910, 908, 905,
904, 900, 896, 894, 860, 856, 854; one or two a floor, floating ahead of where he starts, never in a Warden's arena).
All are damned souls: they can't be hurt or fight, they say a line and then say it again, flatter (the loop), and what
they hand him is whatever their loop was holding. No human villagers: nobody from the village is in Hell but Yooba, so
no hiding villager. Hell has its own cowards, a **hiding soul**. Voices (cast through Hawkes Gen, clips
`vo_by_*`): `soul` (flat, thin), `hawker` (hoarse, sing-song), `heckler` (nasal, sneering).

| Entity | Reacts to | Lines |
|---|---|---|
| `hawkerSoul` | hero within 4.5: pitches. Within 2.6: hands him a **free sample** (a diamond, once). A shot past the stall: hiccups, backs off, "Not the stall!". Hero hurt: shrugs. | *"Cinders! Fresh cinders! ...Fresh cinders!"*, *"Free sample. It's the same cinder."*, *"No refunds."* |
| `hecklerSoul` | hero within 5: jeers. **A stray shot** passes within 0.9: hiccups, steps back, "Aim at someone else!" (it can't be hit; the shot goes through). Hero hurt: cackles. | *"Is that all? ...Is that all?"*, *"Hey! Aim at someone else!"*, *"Ha! Do it again."* |
| `giverSoul` | hero within 3.2: **hands him a loop shard** (a rewind orb) with a hiccup, once. | *"Here. It's not mine. ...It's not mine."* |
| `hidingSoul` | hero within 5: turns its back and whispers. Hero hurt within 8 or a shot within 3: backs away, "I'm not here." | *"Shh."*, *"I'm not here. ...I'm not here."* |
| `cheerSoul` | hero within 4.5: encourages. A shot passes: hiccups and cheers. | *"Go on up. ...Go on up."*, *"Again! ...Again!"* |

Taunting or flinching imps are not placed yet: each needs its reason written here first, and a floor's enemy rules
(one new type, at most three at once) say where. Every reaction emits a `by:<kind>:<what>` signal for the playtest (`bystander-floors`).

## Ladders (the damned built ways up)

Hell is repetition, and so are its stairs: the imps and the damned who haul, mine and keep watch have bolted ladders to
every wall they had to go up more than once (2026-10-25). A **ladder** is a way up that asks for no jump: the hero stands
at its foot, pushes up and holds on. Held, he doesn't fall; he climbs and descends at the pace of a man who has done
this before and would like it noted, and lets go with a jump, or just climbs off the top. His gun stays in his hand
and comes out slower, because it is hard to aim while holding on with the other one (about half the rate). Rewind
takes it back like everything else in Hell: the rung he was on and whether he held it.

Two played floors have one (2026-10-25), both because the way on really is straight up a wall and Hell's haulers had already
bolted something to it: **Floor 999** (*Only 999 To Go*), where the lava starts to rise and the last ledges of the climb are
replaced by the ladder the imps hauled sacks up (one ladder, 5 units, no jump needed: the lava is the clock); and **Floor 998**
(*The Spill*), the jam, where the wrecked village is piled up against the spire's foot and the damned bolted a ladder up the
pile: the ember rain and the cinders fall on him all the way up, and he has only fists (the launcher was stolen on this floor),
so he can't shoot back at all. These two are `ladderHaul` (`content/entities/ladderHaul.json`, a ladder that climbs at 1.7
instead of 2.6: the hauling ladders are old and he takes his time on them, which is the point of the pressure). Other uses are
for floors where the way on is straight up a wall (a Gloomwood trunk, the Caverns' mine shafts, the Keep's watchtowers), each
with its reason written here first (*Rules for levels*). The test floor is `content/scenes/ladderTest.json` (the `ladder`
playtest); the played ones are the `ladder-floors` playtest.

The hero's lines (subtitles; no clip is cast yet): *"A ladder. Hell finally built something that goes up on purpose."*
The announcer, once a floor has one: *"A LADDER! HOW CONVENIENT! HOW SUSPICIOUS!"* (a banner on Floor 998; on Floor 999 the hero's line
plays instead; no clips are cast for either).

**The hero's climb on the ladder** is three clips (generated, `art/hero.json`): `ClimbIdle` (hanging on the rungs, bored), `Climb`
(going up, hand over hand) and `ClimbDown` (the same, going down).

## The hero's moves (a shove and a kick)

Hell is repetition, and by Floor 998 the hero has been hit by the same bolt often enough to have opinions about it. Two things he
learns to do about it, both small, both his own (2026-10-25). Neither makes him a hero; they make him a man who is slightly harder to hit and
slightly better at leaving.

**Quicker, from the first second (2026-11-11).** A man who has fallen through ten Circles has been running from worse than imps. He now runs
at 3.8 units a second (a screen in about 2.7 s), eases into a run and out of it in a blink, and a short tap on Jump is a hop. The coat
already slides on Level 1 (the dash is his from the start; "Oh. It slides. Nobody said it slides." is the first time), and a hit stings
rather than excuses him: 1.2 s of grace on Normal (Assist 2.0, Hard 0.9, Hell Loop 0.7), not 5. Yooba's Slimed Lining is now the upgrade: a
second dash in the air and a coat that dries in 0.6 s. Nothing is drawn on him, no ring and no dot (J took the hitbox dot out, 2026-11-12). On a touch screen a swipe on the right half dashes; on Android a phone buzzes a hair for a hit, a graze or a kill.

**The dash (Yooba's Slimed Lining).** The slime does the work. Once the hero has lost the launcher on Floor 998 and has nothing left to
hide behind, Yooba puts a slick of himself in the inside of the coat: shove off the floor and the coat carries him a few steps (2.6 units, a
sixth of a second) with nothing able to touch him while it does, bolts included: they pass through and fly on. Then it needs a moment to dry (0.9
seconds). One dash in the air before he lands. It is sold in Yooba's shop for 90 diamonds once Floor 998 is cleared (`requires level:4`). He pushes
it with **Shift**, the **Dash** button on touch (a chip under the weapon chip; it says READY or the seconds to wait) or the gamepad's B; the
way the stick points, or the way he faces. The hero's line the first time: *"Oh. It slides. Nobody said it slides."* Yooba's when it is bought:
*"Slimed Lining! Shove off and whoosh! Mind the coat. It's still a bit me."*

**The wall-jump (a story moment).** Nobody sells this one: he works it out. The moment Floor 996 is cleared (lava falls, geysers, walls that
look worse than they are) the next floor's start gives it to him with a line, *"Walls. Hell has plenty. I could kick off one."* From then on,
in the air, pushing into a solid wall (the level's walls, a solid slab; never a one-way ledge) makes him cling and slide down it slowly (1.3 units
a second at most, the hands are not made for it), and Jump kicks him off, up and away from the wall (up 9, away 5.2, the stick ignored for a fifth of
a second so he leaves it); a kick refills his air dash. Kicking from wall to wall climbs a shaft. First cling: *"A hot wall. Sure. I'll hold on to that."*

Both moves are unlocked **for good** (`UnlockFlags` `move:dash`, `move:wallJump`, cleared with New Game; a rewind never takes them back, like a found weapon).
What rewind restores of them is their state: the dash's burst, its untouchable moment and its cooldown, the air charge, the wall he clings to and the kick's push
(`GameplayWorld` saves `PlayerController.MoveState`, because the controller's own flags are not recorded). The announcer has nothing to say about either:
he is busy, and neither is a boss. Where the moves are *needed* (a shaft floor, a gap that only a dash crosses) is written per floor, with its reason here first; so far they
are optional, and the bench `content/scenes/moveTest.json` (playtests `dash`, `wall-jump`, `hero-moves`) is the only place with a shaft.

Art: the hero's `Dash`, `WallSlide` and `WallJump` clips are specified in `Assets/GameConfig/art/hero.json` (not generated; priced, GPU not approved). Until then they play
placeholders from the existing sheets (`Walk`, `Grabbed`, `Jump`). Sounds are made: `player_dash`, `player_dash_ready`, `player_wall_cling`, `player_wall_jump`.

## The Hook and the air kit (a hook, a gust, a ceiling) (built 2026-10-25)

Hell is repetition, and the imps who haul its sacks on chains have done the same lift a million times, which is why every hauler's belt still carries
the thing they climb with: a **hook** on a length of chain. The hero finds one lying on the first ledge of the kit floor, next to a hauler
nobody has bothered to bury (**the Hook**, a pickup, `hookPickup`; it sets the flag `hook.found` for good, kept through runs, cleared with New Game).
*"Somebody's hook. Somebody's not using it."* From then on **E** (or F, the gamepad's left shoulder or Y) throws it.

**How the hook works.** It throws up and ahead: along the right stick, the mouse (once he has used it), the move stick leaning up (sideways = up and that way),
else up and the way he faces. It takes the **anchor** (a ring in a wall, a hanging hook, a stump of chain; `grappleRing`, `grappleHook`) nearest his aim inside a
50 degree cone and 7.5 units. A **swing** anchor holds a rope of the length he hooked it at: he swings on it like a damned soul on a chain, A / D pump the swing, up reels
in and down lets out, **Jump** lets go with the swing's speed and a hop (the way to cross a gap), E again lets go without one. A **pull** anchor reels him straight in
and hops him up onto the ledge it sits on. The rope is drawn from his hand; the anchor glows when the hook would take it. It never works on the floor's footholds, only on anchors:
Hell leaves them where it wants a climber to go. The hero's line is the pickup's text and nothing else: he is not impressed by a hook, only by what it hooks.

**The air and the paths.** Four more pieces for floors that want more than ledges (Docs/LevelDesign.md "The air and the paths"):

- **A moving platform on a path** (`mover`): a slab that travels waypoints and waits at each, carrying whoever stands on it. The Wastes' haulers ride these between the
  lava and the stairs; the damned have long stopped asking where they go.
- **Wind** (`wind`): the Wastes breathe. A zone that blows the hero sideways (walking into it is slow, standing in it is not an option), lifts him or presses him down, and bends
  every shot in it, his and Hell's. Some blow in gusts a hero can time. The hero's line, if he ever gets one: *"It's breathing on me."*
- **Collapsing floors** (`crumble` with a `group`): old slag that gives way one stone after the next in a wave he can outrun, and is rebuilt a little later because Hell
  likes things to be where they were. The same stone as the crumbling stone of Floor 992, now in rows.
- **Gravity-flip zones** (`gravityFlip`): pockets where Hell's loop runs the other way. In one, gravity pulls up: he falls to the ceiling and walks it upside down, jumps down
  from it, and when he leaves (a moment later, so he can cross) the floor is the floor again. Put a ceiling over one, or he falls out of the top of the view.

**Rewind.** All of it is rewound with everything else: the rope (anchor, length, swing speed), the platform's place on its path, a stone that cracked and fell and came back,
a gust (they follow the game clock), and which way gravity pulls. Dragging Hell's loop backward off the ceiling puts him back on the floor he stood on.

**Where it is built.** So far on the bench `content/scenes/kitFloor.json` (the playtest `floor-kit`: swing, pull, gust, mover, collapse, ceiling, in that order, on Level 2's stage). Where the
pieces are *needed* on the played route (a Circle whose floors cross a gap only a hook crosses) is written per floor, with its reason here first; none is yet.

Art: the anchors, the slab and the zones are drawn from plain shapes (rings, streaks, arrows) until painted; the hero's `Grapple` / `Hang` clips are not specified yet. Sounds: the hook
takes hold with `player_land`; new sounds (a throw, a creak, a gust) go through Hawkes Gen later. Priced: nothing here needs the GPU until the art is painted.

## The bosses' second look (six fights, 2-4 minutes, a crowd)

The first fights were a boss and a pattern. Hell, though, loves a crowd, and the hero has by now been through nine Circles' worth of
imps, so every Warden and floor boss brings the crowd with him. Built 2026-10-25 for the Imp Foreman (991), the Pile Keeper (951), the Magma
King (901), the Road Breaker (891), the Carver (851) and the Spire Fiend (801); data in their entity files, written by
`Tools/Levels/boss_rework.py` (edit its tables and run it, never the states by hand).

- **More phases.** A floor boss has four: alone on the first tier (the glowing whip, spear or road line, the rain of embers or the spot under
  the hero), the **surge** below 70% (`xxSurge`, the first wave), the climb at half health (the tier changes, the wave goes with it), tier 2
  (`xxPhase2`, a second wave and the hero's chain/ledge fight) and the **rage** at a quarter (`xxPhase3`, the biggest wave). A Warden
  has five: the first fight, the **wave** below 75% (`mkWave`, `sfWave`), the half-health stand/shed with his guard, the enrage and the **last
  stand** below 15% (`mkLast`, `sfLast`).
- **Denser, telegraphed.** Every attack is a glow first, 1+ s ahead. Lines get wider (5 to 7), and each fight gains two attacks that use
  Hell's tricks: a **fan of bouncing embers** (`ricochetEmber`, `bounces`) and a **burst of bolts thrown where the hero is going**
  (`Aim.Predictive`), each behind a `mkWarnHand` glow.
- **Minions from mixed types, tied to phases, 4 to 8 alive.** Each boss's spawners share one cap (a spawner counts everything its owner spawned):
  4 in the surge/wave, 6 in the second wave, 8 in the rage/last stand. The Wastes bosses mix the roster of the Ember Wastes (Foreman: sack imps,
  halo, deadeye, ricochet, whirl, lead and blink imps, slag hounds; Keeper and King: guards, bulwark, lens, slab, lead imps); the Cracked Road's
  mix the road's (Breaker, Carver, Spire Fiend: whelps, fuse whelps, bounce lobbers, plate, veil and risen demons, slab snipers). A phase's
  wave is cleared when the next begins (the climb, the enrage), so each phase is its own crowd. No rift bombers (their bombs count as enemies).
- **Longer.** Health about doubled and more (Foreman 40 to 105, Pile Keeper 36 to 100, Road Breaker and Carver 34 to 100, Magma King and Spire
  Fiend 80 to 170): the fight, from its fight signal to its defeat signal, should take the bot 2-4 minutes. Tune health, not timers, if it doesn't.
- **The hero sizes each one up.** A voiced one-liner a few seconds into the fight, in his deadpan, saying the weakness the Codex also lists
  (`vo_hero_hint_<boss>`): the Foreman's *whip glows before it cracks*, the Keeper's *guards first*, the Breaker's *the road glows before it
  bursts*, the Carver's *shards fall after the glow*, the King's *dodge the breath, don't wake him early*, the Fiend's *he counts my steps, stay calm*.
  (A boss's `hintOnce` stays silent in his locked intro, so the level says it once the fight starts.)

Playtest: `boss-fights` (all six, ~50 min), and every stage run that reaches one (`floors-*`, `all-levels-*`) gets the same `bossFight.*` checks.

## Elites and mid-bosses (Hell promotes its own, built 2026-10-25)

Hell is repetition, and so is its ladder: the ones who survive a Circle's floors long enough get a name, a bigger body and a trick the common kind
doesn't have. The hero has met a thousand imps by now; these are the ones that stayed. Twelve of them turn up on the x5 floors, and four stand in
the way as **mid-bosses**: not Wardens, not floor bosses, just the nastiest thing on their floor. Data written by `Tools/Levels/elites.py`
(edit its tables and run it, never the files by hand; `--check` runs with no Unity).

**Elites** (entity tag `elite`, gold health bar, a bigger body, more loot and a better chance at a rewind orb). Each is a named variant of an
existing enemy with one twist, and the hero sizes each up the first time one spawns in a save (`hintOnce`: a name, a weakness, a voiced line):

| Elite | Of | The twist | Floors |
|---|---|---|---|
| Quickfire Imp | imp | faster, two shots a volley | 995 |
| Corona Imp | halo imp | 14-ember ring and a burning aura | 995 |
| Ironhide Bulwark | bulwark imp | armored: a front shield 150 degrees wide, and it shoots now | 955 |
| Alpha Hound | slag hound | faster lunge; bursts into three cinders when it dies | 955 |
| Twin-Bolt Deadeye | deadeye imp | two led bolts a volley | 905 |
| Twin Lens | lens imp | a second beam sweeping the other way | 905 |
| Phase Raider | blink imp | blinks in behind him, two shots | 905 |
| Brood Whelp | brimstone whelp | bursts into two more whelps | 895, 805 |
| Furnace Plate | plate demon | hot armor (150 degree shield) and a burning aura | 895 |
| Triple Lobber | bounce lobber | three bouncing lumps at once | 855 |
| Twice-Risen | risen demon | gets up twice | 855 |
| Veil Matron | veil demon | shield, faster blink, burning aura | 805 |

They use Hell's tricks (`frontShield`, `teleport`, `Aim.Predictive`, `laserSweep`, `bounces`, `revive`) plus the `aura` and `onDeath` spawns. Floors 995,
955, 905, 895, 855 and 805 are authored floors, so the elites are rules in their scripts: an `every` rule in a section's phase (three times, 24 s apart),
or a one-off when a section is reached.

**Mid-boss fights.** On Floors 955 (Circle X, the Falls), 905 (the Heat Stairs), 895 and 855 (the Cracked Road) the last stretch of the floor stops the camera
(`lockTravel`), the floor's own hazards stop, and the announcer calls him; his line, the hero's line (voiced: he says what he has seen and what he will do
about it), the boss bar, the fight. Four phases: **alone** (a glow on the floor under the hero, a fan of bouncing embers, bolts thrown where he is going,
every attack a glow 1+ s ahead), the **wave** below 70% (3 minions of the floor's mix at most, a camera shake and a line), the **rage** below 35% (5 at most,
the wave is gone, a wider fan, a special: the elites join the crowd) and the defeat (slow motion, diamonds, the announcer's banner, the hero's closing line).
The landing waits for the counter `midboss`. He hovers near the hero; killing the minions is never the way (they keep coming), he is.

| Mid-boss | Floor | Who he is | His trick | Minions | Hero |
|---|---|---|---|---|---|
| Baron Spillway | 955 | An imp noble in a slag cape who keeps the Falls and charges a toll on everything that falls | bouncing embers and spots; bolts in the rage | halo imps, slag hounds, deadeye imps; then ricochet, whirl, Corona Imps | *"Baron. Embers bounce, marks glow first. I can read that."* |
| The Kiln Matron | 905 | An old imp crone who bakes the Heat Stairs; they are still hot | blinks in beside him (`teleport`), a laser sweep in the rage | guards, bulwark, lens imps; then Twin Lens, Phase Raiders | *"She blinks in, then the beam. I leave first."* |
| Mother Fissure | 895 | The crack that breeds the Road's whelps | bouncing lumps in threes, then fives | whelps, fuse whelps, bounce lobbers; then Brood Whelps, Triple Lobbers, plate demons | *"Whelps keep coming. I hit the mother."* |
| The Shardwright | 855 | The Carver's apprentice: the master carves the rules, he carves the visitors | blinks behind him, bolts in fives | plate demons, slab snipers; then Veil Matrons, Furnace Plates, Twice-Risen | *"Bolts aimed ahead of me. So I stop being ahead."* |

Voice: the hero's lines and the announcer's call are made (`vo_hero_hint_<elite>`, `vo_mb_<bs|km|mf|sw>_title|hero|end`); the mid-bosses' own lines are subtitles
until their voices are made. Art: specs in `Assets/GameConfig/art/<id>.json` for all sixteen, not generated (GPU priced, not approved); until then each wears its
base creature's art, tinted and larger (rule 5). Test arena `content/scenes/eliteArena.json`, playtest `elites` (~40 min: the arena, then the four floors).

## The chase, seen on a rewind (2026-11-11)

Not only on the far ledges: when the hero dies and undoes it, rewinding past the death, the Overlord's silhouette climbs out of
the top of the screen, eyes red, a low hum rising. He is always ahead; time backward is the only way to see his back. A rewind
that undoes no death shows nothing. (`OverlordEscape`; world bible "The chase".)

## Chapter One closes on Floor 997 (built 2026-11-12)

Floors 1000–997 are the **opening climb**: one new threat each, taught then pushed into a finale. After Floor 997's Imp Run
finale (the Lift Stop), the hero **catches up** with the Overlord for the first time — not a Warden fight, a 20–40 s playable
face-off on the landing ledge.

**Canon holds.** The Overlord still never answers the hero and never turns to him. He talks to the air (or past the hero);
the hero's lines are deadpan replies. Dying here and holding R still shows his back for a moment (the chase / Flee clip).
His signature is the **lens**: a thin warning line for 0.5 s, then a `laserSweep` in three escalating phrases (wider, faster).
Survivable on Assist and Normal: every phrase telegraphs.

**Lines (voice `overlord` / `player`, made as one dialogue then split to stems):**

| # | Who | Line |
|---|---|---|
| 1 | Overlord | *"Mm. ... Caught up."* |
| 2 | Hero | *"Been looking for you."* |
| 3 | Overlord | *"Mm. ... Watch."* |
| 4 | Hero | *"I always do."* |
| 5 | Overlord | *"Mm. ... Not far enough."* |

He escapes **up the Shaft** laughing; music resolves into a short chapter sting. A **CHAPTER ONE** complete card shows time,
deaths, rewinds and diamonds, then one voiced hook that foreshadows Floor 500's door with daylight: hero *"Daylight's still
up there. Somewhere."* Continue leads on as today (Grudge pick, Shaft to 996). Flag `chapter1.seen` (kept until New Game):
a replay offers Skip. Written by `Tools/Levels/chapter_one.py`; playtest `chapter-one`. This is the player-facing close of
the opening climb, not the end of Act 1 (Circles X–VIII in the act table above).

## The Overlord watches the climb (Floors 1000-997, built 2026-11-12)

The first four floors gave him no face; now he has one. On Floors 1000-997 the Overlord is on screen again and again, small and far on the
upper edge, and he **answers what the hero just did**, in his own voice (`audio.json` voice `overlord`, unchanged: bored, deep, velvety, barely
bothered) and in his own few words: *"Mm. ... "* then one short line. He still never listens to the hero and never turns to him (canon: he is always
ahead; the hero only sees his back by rewinding). 30 lines, each said once per session, at most one every 20 s:

| Moment | Lines | Figure |
|---|---|---|
| Floor entered (one per floor) | 1000 *The bottom. Cozy, isn't it?* / 999 *Rising, are we?* / 998 *Your village is floating by.* / 997 *Higher. How tiresome.* | hovers (Idle) |
| The hero dies | by what hurt him last: an imp, a shot, a hazard (lava, geyser, cinder fire), a fall; 3 each | laughs |
| A rewind (no death behind it) | 4: *Again with the clock.*, *I remember that trick.*... | climbs away when it ends |
| A rewind that undoes a death | 4, spoken at once, whatever the cooldown: *Dead, then not. Tiresome.* | the silhouette climbs out of the top (see "The chase") |
| 30 s without being hit | 3: *Untouched. It won't last.* | points |
| The finale (`floors:<n>:finale`) | 3: *Now it gets loud.* | points |

The figure (`overlordTaunt`, art spec `art/overlordTaunt.json`: Idle, Laugh, Point, Flee) stays on the upper edge, never over the touch stick or the
rewind button, and his caption is one line. Code: `OverlordTaunts` (Gameplay/Presentation), lines from `Tools/Levels/overlord_taunts.py`.

## The Overlord on the far ledge (eight glimpses, Circles X and IX, built 2026-10-25)

J (2026-10-25): *"Give the Overlord (2 voice lines today) at least 8 mid-floor appearances across Circles X and IX."* Until now he is in Video1, the
portal scene and (one day) the Floor 1 throne room. These are the glimpses between: the hero climbing a floor looks up and the Overlord is **somewhere
far above, on a ledge**, busy with Hell. They keep the cast table intact:

- **He never answers the hero, never turns to him and never looks down at him.** He is talking to the air, his back or his shoulder to the screen, as he
  is until Floor 10 (*Shortcuts*, Circle I). The hero talks to *him*; the Overlord's lines are to nobody. (This is the whole joke of the glimpses.)
- **His voice is the table's:** deep, slow, enormous, barely bothered, at most six words, each a `Mm.` and a long pause first. No speeches, no threats,
  no office or work jokes; nothing he says is about the hero.
- **The hero's replies are deadpan one-liners**, said once the Overlord has finished. The hero never says he *wants* to catch him; he is just in the way.
- **They are scenes** (the scene engine's actions on a floor's own rule, `sectionReached`, once a play): the camera holds for the few seconds it lasts,
  nothing new is introduced, the scene never ends a floor and the floor goes on afterwards. **Rewinding through one is part of the design**: the scene is
  world state, so he steps back, the line is said again and the hero answers again (Hell is repetition). Only the dropped ember can hurt, once, 1 heart,
  after a glow that tells the hero where. Nothing it does touches the diamonds he is carrying or the save: what he "steals" is on the floor, not in the shop.
- **Look:** a huge obsidian silhouette with red eyes, a crown of black horns, far off and small on the screen (`overlordFar`; placeholder until the art
  is made: spec `Assets/GameConfig/art/overlordFar.json`, priced, not approved). Voice: `overlord` (audio.json `vo_ovl_*`, hero `vo_hero_ovl_*`).

| # | Floor, section | What he does | Overlord | Hero | Why here |
|---|---|---|---|---|---|
| 1 | 997, *higher* | **Taunt:** sits on a far ledge, looking up, hand on a horn | *"Mm. ... Up. They always try up."* | *"Don't mind me."* | The first floor with the hauling chains: the imps work for him. |
| 2 | 994, *cross* | **Drops a hazard:** a glow on the stone ahead, then an ember falls from his fingers | *"Mm. ... Catch."* | *"Great. A present."* | The floor where the thief throws things back. |
| 3 | 958, *bank* | **Flees upward on a lift:** steps onto a chain lift that rises out of the screen | *"Mm. ... Higher."* | *"He has a lift. Of course he has a lift."* | Hell's burning village floors; the hero has none. |
| 4 | 953, *lap2up* | **Lasers the floor:** a glow, then a column of his beam into the stone far ahead; the screen shakes, nothing is hit | *"Mm. ... Floor."* | *"Missed. Or he didn't care."* | The Loop's floor: the beam is the one from Video1, again. |
| 5 | 906, *loop1* | **Steals a diamond:** one hangs ahead; he marks it with the beam, and takes it if the hero doesn't first | *"Mm. ... Mine."* | *"It was never yours either."* | The Heat Stairs, where the diamonds are the point. |
| 6 | 896, *slope* | **Rewound by the hero:** he strolls along the ledge. The hero (*"Let's see what R does to him."*) holds R: he walks back the way he came | on the rewind: *"Hhh. ... Again."* | *"Yeah. Same here."* | The first Circle IX floor of breathing vents; Hell breathes in and out, and so does he. First clue he knows the loop (*The Overlord: who he is*). |
| 7 | 857, *behind* | **Summons:** points down the road; two whelps drop in | *"Mm. ... Go."* | *"He sends the help."* | The Carver's road: Hell's errands. |
| 8 | 806, *under* | **Loses a diamond:** crossing the ledge with a sack, one falls out and drops by the hero's path | *"Mm. ... Heavy."* | *"Not picking that up for him. ... Fine. I am."* | The Spire's last stretch: the sack is nearly full; he is nearly up. |

Eight floors, eight different interactions (a taunt, a hazard, a lift, a laser, a theft, a rewind, a summons, a drop). His voice is **made once**: the
two lines the game had (*Stay.* and the exhale, the portal) are joined by these nine (`vo_ovl_997_up` ... `vo_ovl_806_heavy`, plus `vo_ovl_896_again`) and
the hero's ten replies (`vo_hero_ovl_*`). Data written by `Tools/Levels/overlord_cameos.py` (edit its table and run it, never the files by hand;
`--check` runs with no Unity); two small mechanics make them possible, `screenAnchor` (an entity held at a place on the screen, drifting) and the
`heroRewound` trigger (true for a moment after a rewind ends). Playtest `overlord-cameos`.

## The Climbers: a rival and a guide for each Circle (built 2026-10-25)

J (2026-10-25): *"Invent a rival climber and one recurring guide character per built Circle."* The Overlord is far away and says nothing to the hero; the
bystanders say one line and are gone. The climb needs people who are met **again**, who remember the last floor, and who are funny to him. These three
are damned souls like the rest of Hell's loop-keepers, but each has a loop that *almost* lets him leave it, and the hero is the reason it almost does. They
share the bystanders' rules: they can't be hurt, can't fight and are never an enemy; they never speak of work, bosses-as-managers, offices or coffee;
nobody from the village is in Hell but Yooba, so none of them is a villager; none is a slime.

| Who | Where | Why they are in Hell | Look | Voice (`audio.json` voice, cast through Hawkes Gen) |
|---|---|---|---|---|
| **Rook**, the Other Climber (the rival) | Circle X: Floors 996, 992, 955. Circle IX: 899, 858, 808 | In life he came **second at everything**, by a hair, and was sure he would have won with one more try. Hell's sentence for that is the loop: he climbs the Wastes, and on every floor he arrives exactly one step behind whoever is ahead. He has been the runner-up of the whole climb since before the hero fell. Now there is someone ahead of him who is *not even trying*, and it is the best and worst thing that ever happened to him. | Tall, lean, a soul in a patched mountaineer's coat and a scarf that burns without burning down, one glove, a coiled rope that goes nowhere. Amber glow (`#F2A93B` in subtitles). A medal on a ribbon around his neck that says **2**. He is always mid-stretch, as if someone has just taken his photograph. | `rook`: bright, boastful, a little too loud, a game-show host who is not winning; cracks on the "2". Third-person when he is hurt (*"Rook will be fine."*). |
| **Wick**, the Lamplighter (Circle X's guide) | Circle X: Floors 999, 993, 960, 902 | He lights **the one lamp** on a ledge so travellers can see the safe stone; it goes out the moment anyone passes, so he lights it again, for a different traveller, for ever. He wants one traveller to get to the top, so the lamp stays lit once. | Small, bent, a candle for a head that is always lit and never shorter, a long pole with a wick on the end. Pale green flame (`#BFE6C8`). | `wick`: a soft wheeze, very kind, in no hurry, speaks as if every sentence were a match that might not catch. Hums. |
| **Pointer**, the Fingerpost (Circle IX's guide) | Circle IX: Floors 900, 895, 860, 810 | A soul fused to a road sign when the Cracked Road split under him: one arm is stuck out straight, **he can only point**, and he is right about where the road holds, always, to nobody's thanks. He used to say *"the road is cracked"* to every passer-by; they thought it was a sign. | Planks nailed to a body, one arm a long signpost arm with a hand at the end, a name board on his chest that reads UP. Dusty tan (`#C9A27A`). | `pointer`: gruff, flat, as few words as possible, directions only. Says *"Left."*, *"Up."*, *"Your other left."* The only soul in Hell who is annoyed at being right. |

### What a climber is, in the game

A climber is a **scene** on a floor (the scene engine, like the Overlord's glimpses): when the hero reaches a section, the climber is there, ahead, the camera
holds a few seconds, the lines run, a **small reward** is paid once, a **flag** is written (for good, kept until New Game) and the climber goes on its way; the floor
then goes on. Nothing in a scene ends a floor, introduces an enemy or takes anything from him. **Every scene branches on the hero** (`heroState`, a new trigger that
reads his hearts, diamonds, Grudges, deaths and the flags earlier scenes wrote), so the same scene says something else to a hero who is bleeding, rich, hurt
often, or who has met the same person before. Rewinding through a scene replays it (Hell is repetition); the reward is keyed to the floor (`TryGrantOnce` through the
spawned pickup's own collect), so a rewind never pays twice, and the flags are `setFlag`.

The rewards are small on purpose: a diamond or three, a rewind orb, a heart when he is down to one. They are never the floor's best prize, and none is required.

### Rook, the rival (six scenes, Circles X and IX)

Rook is always **one step ahead and one step behind**: he is ahead on the screen, and he loses. The hero never races him on purpose and never says he wants to win.
Rook thinks R is cheating, so he keeps a tally, and the tally is his arc (flags `rook.met`, `rook.dared`, `rook.asked`, `rook.sideways`, `rook.second`, `rook.done`).

**On every Shaft stop** he also stands beside a chalk wall of tally marks that shows the hero's **real total rewind count** (`Achievements` stat `rewinds`, bundles of five; new marks scratch in when the count rises since the last visit). He speaks **one voiced line per visit**, picked by the count band (0 / 1–5 / 6–20 / 20+), never the same line twice in a row (16 lines in `ui/rook.json`). Presentation only (`RookShaft`): walking past him needs no button; Continue still ends the stop. Voice stays the cast `rook` id (bright, boastful — not a new design).

| # | Floor, section | What happens | Branches on the hero | Reward | Flag |
|---|---|---|---|---|---|
| 1 | 996, *foot* | He is stretching on a ledge ahead. *"First! ...Oh. You're a person."* He announces the race the hero hasn't entered. | **Diamonds 10+:** *"Look at the pockets on you. Cheating."* **Otherwise:** *"Slow and poor. I like it."* The hero: *"I'm just going up."* | Diamonds 10+: nothing (he's offended). Otherwise he tosses a diamond: *"For the effort."* | `rook.met` |
| 2 | 992, *wall* | A dare: *"Race you to the bridge. Go!"* He runs ahead and the cracking stone drops under him; he scrambles back, angry. | **Hearts 1:** *"You're one hit from the floor. Even I won't race that."* and he gives up the dare. **Hearts 2+:** *"Third floor in a row I'm second!"* | Hearts 1: a heart (a shard of his scarf's ember heals him). Else: a diamond he drops. | `rook.dared` |
| 3 | 955, *falls* | He hides behind the falls to watch the hero's R. *"How does it work? Asking for a friend. The friend is me."* | **Deaths 3+:** *"You've died more than I have, and you're still ahead?"* **Grudges 2+:** *"You collect those like medals."* **Otherwise:** *"Show me. Slowly."* | A rewind orb he "found". | `rook.asked` |
| 4 | 899, *wall* | He tried R on his own. *"It doesn't go backward for me. It goes... sideways."* He steps sideways. | **Flag `rook.asked`:** he says the trick he learned from the hero is "bring your own annoyance". **Else (he skipped 955):** *"I heard there's a button."* | Diamonds 2. | `rook.sideways` |
| 5 | 858, *foot* | The road leans and he gets there first, then the road drops him behind the hero again. *"Second. ...Second. It's a number, at least."* | **Hearts 1:** he gives up the lead and walks beside him, warning the hero where the road tips. **Diamonds 30+:** *"Spend those. You'll get tired of carrying the first place."* | A heart if hearts are 1; else a rewind orb. | `rook.second` |
| 6 | 808, *foot* | The last scene. He sits on the bottom step with his medal in his hand. *"I don't need to be first. I need to see you be."* He gives the hero the rope that goes nowhere: *"It goes where you go."* | **Flag `rook.second`:** the kindest line he has. **Deaths 5+:** *"Die less. It's embarrassing for both of us."* **Otherwise:** *"Don't die, I'd have to carry on."* | Three diamonds and a rewind orb. | `rook.done` |

The hero's side of it is all deadpan: *"I'm not racing."*, *"You're ahead."* *"Then why am I behind?"*, *"It's a button."*, *"Fine. Second's a good place."*

### Wick, the Lamplighter (Circle X's guide, four scenes)

Wick teaches the Wastes by what he lights. The lamp on each of his ledges is lit **once** when the hero reaches it and **stays lit** (the one lamp in Hell that does).
His arc: he is a stranger who wants a traveller to arrive; by the Heat Stairs he is a friend who says he can stop now. Flags `wick.met`, `wick.lit`, `wick.rested`.

**Floor 999 (his first meeting)** is the full lamp walk: he appears ahead, walks the ledges lighting **at least three** `wickLamp` posts (each flares with a light flash and `sfx_wick_lamp` and stays as `wickLampLit`), speaks with BeatDirector `say` bubbles so **input stays live**, and leaves eight voiced lines across greeting, warnings before the rising lava, and farewell (`vo_wick999_*` / `vo_wick_lamp_*`). The hero can keep climbing while Wick lights; no button required to pass him.

| # | Floor, section | What happens | Branches on the hero | Reward | Flag |
|---|---|---|---|---|---|
| 1 | 999, *ledges* | Wick walks ahead lighting three lamps; greeting, lava warnings, farewell (see above). | **Hearts 1:** *"You're running on a wick."* he heals at a lamp. **Otherwise:** tip that the lava rises. | Hearts 1: a heart. Otherwise a rewind orb. | `wick.met` |
| 2 | 993, *ledges* | *"They throw things here. ...Don't catch them."* He lights the lamp where the hollows are. | **Deaths 3+:** *"You've been up here before. ...So have I."* **Flag `rook.met`:** *"A loud one went by. He said he's winning."* | A rewind orb (two if deaths 3+). | `wick.lit` |
| 3 | 960, *stones* | He can't keep the lamp lit; the pulleys are loud. *"It keeps going out. ...I'm not crying, it's the smoke."* | **Diamonds 20+:** he asks for one for the oil and gives back two orbs ("I'll owe you"; the diamonds are *not* taken). **Grudges 2+:** *"Hold on to those. They'll get heavy."* | Two rewind orbs, or one. | none |
| 4 | 902, *stairs* | The last lamp before the Magma King's door. *"I can stop now. ...Can I stop now?"* | **Flag `wick.lit` and `rook.asked`:** *"You talk to everyone, don't you."* **Hearts 1:** he heals him. **Otherwise:** *"Go quiet. He hears everything."* | A heart if low; otherwise three diamonds. | `wick.rested` |

### Pointer, the Fingerpost (Circle IX's guide, four scenes)

Pointer is the opposite of Wick: he does not speak to be kind, he points. The pointing is the **hint**: the glow follows his arm to the next crack that holds. His arc is
that he is fed up with being right, and by the Spire he points at something other than the road. Flags `pointer.met`, `pointer.right`, `pointer.up`.

| # | Floor, section | What happens | Branches on the hero | Reward | Flag |
|---|---|---|---|---|---|
| 1 | 900, *rise* | *"Left."* The hero goes right. *"Your other left."* A glow marks the ledge that holds. | **Hearts 1:** *"Up. Fast. Please."* he points at an orb. **Otherwise:** the diamond sits where his arm points. | Diamond (or an orb at hearts 1). | `pointer.met` |
| 2 | 895, *road* | The road heaves. *"Up."* Then *"Not that up."* | **Deaths 2+:** *"I've pointed you here before."* **Flag `rook.sideways`:** *"A man went sideways. I don't recommend it."* | A rewind orb. | `pointer.right` |
| 3 | 860, *crack* | Under the Carver's yard. *"Don't look at the statue."* | **Grudges 2+:** *"You're carrying a lot of promises."* **Flag `wick.rested`:** *"Little glowing one sent regards."* | Two diamonds. | none |
| 4 | 810, *stair* | At the foot of the Spire's last flight. His arm droops for the first time and points up at the Needle. *"That's all I know. ...Up."* | **Hearts 1:** he heals him. **Flag `pointer.right` and `rook.second`:** *"I was right about all of you."* | A heart and two diamonds. | `pointer.up` |

### How they meet each other

Wick mentions Rook on Floor 993 if the hero has met him; Pointer mentions Rook (895) and Wick (860) the same way. Rook never mentions them, because he is
busy. The three are never on screen at once; the hero is the only one who has met all three, which is why each one asks the same thing: *"Are you the one going up?"*

### Voices and subtitles

All three are **voiced** (2026-10-25): Rook, Wick and Pointer each have a voice designed in Hawkes Gen (`audio.voice-design`, `audio.voice-save`; audio.json `voices` `rook`, `wick`, `pointer`,
each with its own `voiceId`, so no two cast members share a voice: Rook bright and too loud, Wick a soft wheeze, Pointer gruff and flat). Every line they say is an audio.json sound `vo_<who><floor>_<tag>`
(68 clips, `eleven_v3`, cents each) and every `showText` of a scene carries it as `sound`; the hero's 31 replies are `vo_hero_cmp_<line>` in the `player` voice. Only Pointer's silent "..." stays a bare subtitle.
Recipes and the line list: `Tools/Audio/companion_voices.json`. Art is a spec each in `Assets/GameConfig/art/` (`rook`, `wick`, `pointer`: side view, idle / talk / walk / react clips; the Overlord's far
glimpses `overlordFar`; the five bystander types `hawkerSoul`, `hecklerSoul`, `cheerSoul`, `giverSoul`, `hidingSoul`), priced, not generated (GPU not approved); until `art.mjs` runs, the climbers wear the
damned soul's sheet tinted in their colour and the bystanders the damned soul's sheet, and the Overlord's glimpses the Magma King's idle tinted obsidian. Data is written by `Tools/Levels/companions.py` (edit its table and run it, never the floors by hand;
`--check` runs with no Unity). New C# is the `heroState` trigger (`Gameplay/Content/HeroState.cs`). Playtest `companions`.

## Stop floors

Hell never lets up, so the times it does are remembered. A **stop floor** (2026-10-27) is a floor where nothing attacks: the lava sea sits still below, no hazard is
started, no enemy can reach the ledge, and the hero has about **20 seconds** of quiet with one thing to find (a scene, a soul, a carving or a climber), a heal where it fits, and
nothing to learn. The landing waits for the quiet to run out (the hero says so if he is early). It still ends with the floor's Grudge pick. They take the place of long floors
(same numbers, same level slots), one new idea each. The trial was four floors; the rest followed the same day (J: "every fourth floor of Circle X and IX"), eight in all:

| Floor | Name | Circle | The discovery | Quiet |
|---|---|---|---|---|
| 959 | **Hell's Graffiti** | X | Six carvings from earlier climbers, one deadpan line each, the last signed by Rook (*"ROOK WAS HERE. SECOND."*). A diamond lies under it. | no hazard, no enemy |
| 957 | **The Hot Bath** | X | A spring that cooled in the first century. Three damned souls soak in it and are nice about it. One tells the hero to sit in it; it heals a heart. *"It's the one thing down here that isn't trying to burn you."* They have not told the Overlord. | no hazard, no enemy |
| 952 | **The Waystone Shrine** | X | A stone by the road with a keeper soul (*"Kneel. Resting is free. ...Mending costs."*). A card: **rest** (a heart, keeps every Grudge) or **fill every heart** for a Grudge (`offerShortcut` with `costGrudges` 1; with none to give the card says so and he can only rest). Paying skips the floor's Grudge pick: the shrine already took one. Sits just under the Pile Keeper's mound, which is why it is there. | no hazard, no enemy |
| 897 | **Rook's Camp** | IX | Rook got here first and lit a fire out of fire. His camp is on a ledge under the road: *drop down and sit* (a heart, and Rook counts it as a win) or *walk on* (two diamonds, and Rook says he is right behind). The hero's reply is where he stands. Sets `rook.camp`. | no hazard, no enemy |
| 893 | **The Stuck Imp** | IX | An imp on a ledge below keeps lobbing the same ember in the same direction. It fizzles over the lava short of everything, every 3.2 s, forever. Rewind and it throws the same throw: the hero notices. The ember can't reach the ledge (it dies after 0.8 s, 2.4 units out); the imp can't be hurt and drops nothing. | one harmless ember a few seconds, nowhere near him |
| 856 | **The Sleeping Demon** | IX | A demon asleep on a ledge under the road, snoring in time with the lava. **Stealth:** it stays asleep unless a shot of the hero's passes within 3 units of it or he lands on its back; then it wakes (*"Mmh. ...Mm?"*), shows a `!`, and 1.4 s later fires slow spreads of three for 8 s before it lies back down. Walk past without waking it and three diamonds lie on the road beyond (`QUIET`). The demon is Neutral and cannot be shot, so a bot that shoots whatever is hostile walks by it. | asleep: none; woken: a telegraphed volley |
| 853 | **The Damned Parade** | IX | Souls repeat a village fair on the road: a hawker at a stall (*"Step right up! ...Step right up!"*) and three walkers who go to the far end and back. The loop is exactly the same every time (the hero says so). **On the third loop one turns to watch the hero** (*"...You're not in the line."* / *"I'm leaving."* / *"That's what the last one said."*) and gives him a rewind orb. Done with `walkEntityTo`, `faceEntity` and a long sequence (a rewind replays it). | no hazard, no enemy |
| 809 | **The Loop Room** | IX | The road dips four ledges into a room (a carved *THE LOOP ROOM. JUMP. REWIND.*) with a soul in it who says *"Jump. Then rewind. ...I'll do it for you."* A jump in the room sets the permanent flag `loopRoom.jumped`; a rewind while he is in the room afterwards makes a pale **echo** (`echoSoul`, a `mover` hop) jump where he stands, a diamond falls and the hero says *"It jumps worse than me."* (`heroRewound` + `heroState`.) | no hazard, no enemy |

Lines are short and the hero's are one-liners. Souls are never the joke's target and nobody mentions work. The floors are written by `Tools/Levels/stop_floors.py`; the long-floor
generators (`long_floors.py`, `cracked_floors.py`) skip them (`STOP_FLOORS`). Their lines are audio.json prompts without clips yet (priced, not made: `Tools/Audio/pending_clips.json`), so they
play as subtitles until the clips exist (`stop_floors.py --wire` then adds their `sound` fields). The spring, the camp fire, the shrine stone, the fair stall and the loop sign are tinted placeholder boxes; the stuck imp and the sleeping demon wear the imp's sheet; every soul wears the damned soul's. Playtest `stop-floors` (all eight).

**Where the stop floors are, and are not.** Every fourth floor of the authored stretches where a floor can give way: Circle X 959, 957, 952; Circle IX 897, 893, 856, 853, 809. The
opening floors (1000-991: the tutorial, the launcher's theft, the weapons found, the lost things), Floors 910-902 (the side doors, the mid-boss and the Door) and the floors that carry a
Climber's scene, an Overlord glimpse, an elite or a weapon pickup are not replaced: those scenes live in the floor's own script. Circle VIII (the Rune Bridges, 800-701) has none yet.

**Small finds on the long floors** (`Tools/Levels/ambient_finds.py`, run after the floor writers): each long floor of Circles X and IX gets two **diamond glints** (`diamondGlint`, one diamond) and,
when it has no damned soul yet, one **soul** hovering over a ledge of its own route (a soul within 1.6 units pays a rewind orb). Spawns carry the id prefix `find_`.

## Why he is sick of it: the opening and the level beats (2026-11-06)

**The frame, in one breath.** He was sick of it all before Hell: the same afternoon, the same chores, the same nothing
happening. Then the sky opened, and Hell turned out to be the same thing, louder. Every beat below is a small step on that one
idea: *Hell is repetition, and he has had enough of repetition.* (No office or work jokes; the loop is Hell's.)

- **The opening** is Video1 (*Intro*), played on a fresh save after New Game, **skippable with Escape or a tap**. It is the only
  opening; it says why he is sick of it by showing the afternoon he already was, then the Overlord.
  "First launch" means a new player's first play: a fresh save offers only New Game, which starts Video1 (the main menu never skips it); Continue/New Run come only after a run exists. The owner may later ask to start Video1 on the very first boot instead.
- **A beat** is one line of narration at the start of a level, a `Subtitle` in warm grey (`#C9B8A6`, so it never reads as the
  hero's voice or the announcer's gold title), unvoiced, 3.6 s, after the title and the hero's line. **One beat per level,
  and no beat is ever used twice.** A floor that has no beat yet has none (never a copy).

| Level | Beat |
|---|---|
| 1, The Meadow | Then the sky opened. (its own task: k_e34466068097) |
| Floor 1000 | The bottom of Hell. He is already sick of it. (now an arrival beat, below) |
| Floor 999 | One floor up. Hell is counting along. (arrival beat) |
| Floor 998 | They took the village. Now they take the gun. (arrival beat) |
| Floor 997 | Imps haul his village's diamonds. Same sack, every loop. (arrival beat) |

### Arrival beats (Floors 1000-997)

*Story is played, not read.* The warm-grey captions of Floors 1000-997 are gone: each floor's arrival is a short moment the player lives while still
moving (Portal's first chamber: the voice and the room do the telling). Each is a `storyBeat` action in phase 0's `onEnter` (`id` `arrive<floor>`), lived **once per save**
(`Data/StoryBeats`, PlayerPrefs `Story_Beats`, cleared only by New Game): a death, a retry or a new run from the Waystone does not replay it; the banner still shows. It never locks input.
Every beat = a stinger + a camera shake (and a small flash) + a hero reaction clip (Land, Curious, then back to Idle) + one event + **one** voiced hero caption (the line is its own subtitle, one line, under 60 characters).

| Floor | The moment | Stinger | Hero's line (landed clip) |
|---|---|---|---|
| 1000 | He hits the bank: the slam, the screen shudders and flashes ember-orange, he lands. | `shaft_slam` | *"Floor one thousand. Of course it's a thousand."* |
| 999 | The ground rumbles; a pale ghost of an earlier climber flickers past (`echoSoul`, the Hell-keeps-count of those who came before). He looks. | `magma_rumble` | *"You said that already."* |
| 998 | The baker's cart wreck crashes into the spill: a thud of sacks, a flash. He looks. | `impSack_die` | *"That was the baker's cart."* |
| 997 | An imp snatches (the launcher goes; fists come), cackling; he looks at the sack line. | `thief_snatch` | *"Those are ours."* (`vo_imprun_ours`) |

Playtest `story` (`SickOfItPlaytest.Story.cs`, touch, ~5 min) plays them and reports `quality.textOnlyBeats` (a caption with no voice and no beat around it); `opening-beats` still checks that each floor's voiced line shows.

## Rules for levels

- **A level is a place in the story**, named after the place. `displayName` is the place ("The Meadow",
  "Floors 1000–991: The Landing").
- **No work or office jokes** anywhere in the game: titles, lines, upgrades, results quips.
- **A level is one floor, and it goes up.** The player climbs from the lava sea to the floor's landing and the
  counter moves on; never a flat walk to the right. A floor's way up turns (J, 2026-10-18: "some scrolling
  horizontally"): up a lava fall, sideways across a lava river on a raft or ahead of a lava wave, up again, always
  forward, the whole floor hanging over the lava sea far below. A single locked screen is only for a Warden's arena
  (floor bosses fight on climbing arenas). The climb grammar, the path and the toolkit are in
  [LevelDesign.md](LevelDesign.md).
- **Every floor is a ride** (J, 2026-10-18: *"Be creative here! Make the gameplay and levels as immersive as you
  can."*): each one mixes climbs with sideways runs, raft rides on the lava's currents and chases, and has a set piece
  (the bridge that falls behind him, the village's burning cart down a slag chute), a surprise that is never a new
  rule, a spot where rewind is the clever move, and its own feel: the screen shaking on eruptions, slow motion on near
  misses, the lava's roar rising with the lava, the announcer calling the floor. The throne glows far above every floor
  of the Wastes and gets closer as he climbs. All of it per floor in [LevelDesign.md](LevelDesign.md).
- **Pacing (arcade, but fair):**
  - the first 5 seconds of a level are quiet: title, one line, room to move;
  - one new idea per floor: a new obstacle or a new enemy type, never both; a new enemy arrives alone;
  - no more than 3 enemies on screen in Circle X, rising slowly per Circle;
  - every hazard is telegraphed at least 1 second ahead;
  - a floor has a breather (a few seconds with nothing attacking) between each push;
  - target floor length 60 to 120 seconds; floor bosses up to 3 minutes; Wardens separate.
  - the exception is a **long bullet-hell floor** (240 to 360 seconds; Floor 998 and Circle X's long floors now run ~150-195 s on the camera's pace, with no timer on the landing; Floor 998's ember rain falls from **Mother Cinder**, a huge cinder in the background, the burning village grown up, many embers and shots, five set pieces; Floor 998, the
    Spill, is the first): Hell showing off. Recipe in LevelDesign.md.
- **Story beats are voiced**: one title line from the announcer, one or two lines from the hero, a beat at the
  end. Lines are written for the character's voice (see `audio.json` voices) and are short.
- **Rewards make sense**: diamonds drop from things that were carrying them; loop shards from the damned.

## Engine work this story needs

The one-floor layout needs new rewind-safe pieces (sinking and crumbling stones, rafts, lifts, counterweights,
updrafts, lava falls, rising lava, the climbing arena), a climbing playtest bot and a diamond-cost shortcut: the list
is LevelDesign.md's *Building it*. The floor toolkit and the path camera are built (2026-10-18, LevelDesign.md
*The toolkit* and *A floor's path*; recipe: GameplayCookbook "A floor with a path"); the floors themselves are next.
What exists today:

Built (2026-10-15; recipe: GameplayCookbook "A stage that climbs through Hell's floors"):

1. **Floor counter**: script counter `floor` (`floors.start`/`end` in the level script), HUD readout `FLOOR 973`,
   floor signs in the world, `setFloor`/`climbFloors` actions, `floorReached` trigger, and Circle `milestones`
   (lines that play when the counter reaches a floor).
2. **Travelling stages**: `world.camera: "Travel"` (Right walks, Up climbs): forward-only camera, ground that runs
   along, platforms and parallax props streamed in ahead and removed behind, backgrounds keyed by `Time`,
   `Distance` or `Floors` (`sequenceBy`) and able to play part of a clip (`clipStart`/`clipLength`).
3. **Floor generator**: `content/circles/<id>.json` (backdrop, track, props, enemy table, hazards, easiest/hardest
   difficulty, milestones) and the `generateFloors` action that fills floors between story beats, seeded, so
   rewinds and playtests repeat. Circle X is `emberWastes`; its levels are one floor each, Levels 2-31, "Floor 1000:
   The Landing" to "Floor 901: The Slag Throne" (the Magma King's arena: a locked screen whose counter stays on 901),
   with the old 10-floor stages (Levels 67-74) as the long way's stand-ins. Circle IX is `crackedRoad`; its levels
   are one floor each too, Levels 32-60, "Floor 900: The Cracked Road" to "Floor 802: The Spire Door".

   *Stage dressing* (2026-10-18, with the old stages `Level3`-`Level6`, now levels 67-70): a stage's own scenery on top of its Circle's (`world.props`:
   the Spill's debris, the Loop's same rock), a carved sign where the hero lands (`floors.sign`), floors Hell makes
   him walk twice (`floors.repeat`, the Loop's 957), and stages that lead straight on to the next one through the
   Shaft (`nextLevelNumber`). With `Level7`-`Level10` (now levels 71-74): a door carved with words at the top of a stage, where the camera
   stops (`floors.door`, the Magma King's *THE SLAG THRONE*).

Note: the Landing was recut from the first layout's 25-floor stage to Floors 1000-991 (2026-10-18), then to the one
floor it is now (Floor 1000, `levels/Floor1000.json`), and its cinders moved up to the Spill, where the Story puts them. `CrackedRoad` was cut the same way to Floors
900-891, then replaced by the one-floor levels Floors 900-891 (2026-10-19).

This milestone (*Into Hell*, approved 2026-10-18) builds:

4. **Scripted scenes**: the end of Level 1 (after the Grudge pick): the Overlord's lasers burn the portal into the
   ground, the hero is walked over with input off, the lines and the echo play, the portal pulls him in (spin and
   shrink), Yooba bounces past and is slurped in. Rewind is off during the scene. The hero gets new animation
   clips for it (look down, shout, turn, spin into the portal).
   *Built so far* (2026-10-18; recipe: GameplayCookbook "A scene with the hero"): the scene actions a level script
   uses for it: walk the hero somewhere by himself, turn him, play one of his clips, pull him into a point
   (spinning and shrinking until he is gone), move and zoom the camera, fade to black, and lock rewind while it
   plays. `content/scenes/cutsceneTest.json` runs every one of them on the Meadow for the playtest.
   *The scene itself* (2026-10-18): Level1.json's last phases, `portalBeam` → `portalLook` → `portalYooba` →
   `portalClose`. After the Grudge pick: input and rewind lock, the hero can't be hurt (`sceneUntouchable`) and
   the music drops. The Overlord's beam is telegraphed by a red glow on the ground 2.5 units from the hero, on
   the side toward the middle of the meadow, for 1.25 s, then strikes (flash, shake, a column of beam, *"Mm. ...
   Stay."*) and the ground burns open into the `portal` (humming, drawn behind the hero). The camera closes in a
   little, he walks to its edge, the lines and the echo play, a "?" pops over him, *"Nope."*, he turns and takes
   one step, *"Oh, come ON-"* and he is pulled in (1.2 s, 2.5 turns). A beat later `yoobaProp` (Yooba: the
   village slime, slime art, harmless) hops in from the far side and is slurped in. The portal flares and
   closes to a scorch, the screen fades to black, and the level ends: results, then Video2. The hero no
   longer says *"Fine. I'll go get them."*: he never chose to go. Still to come: the hero's scene clips (look
   down, shout, turn); today he uses Idle and Walk.
   *Portal and beam art* (2026-10-18): baked by `Tools/Art/fx_sheets.py` into `Assets/Art/Effects/`. Today's is
   the no-GPU first pass (`Tools/Art/fx_interim.py`): the swirl inside the portal is Video2's own fire tunnel
   squashed into the hole, in a torn turf rim in the Meadow's palette. The Hawkes Gen version (Qwen paintings
   styled on the Meadow and Video2, Wan clips between them) is queued and replaces the same sheets. The
   `portal` opens (scorch → crack → ring tears open), loops its swirl while the hero is there, and plays its
   closing flare down to the scorch, which `portalScorch` then holds. The beam is three entities:
   `overlordBeamGlow` (the charge glow, 1.25 s), `overlordBeam` (one column from the sky) and
   `overlordBeamImpact` (the burst where it lands).
5. **Video overlays**: Video2 shows the hero tumbling and spinning down the fire tunnel (about 10.5-14.5 s), as an
   animated sprite drawn over the baked video.
6. **The Shaft**: (hook point: `ShaftHook` in `Core/Flow`, called after every video before its level and after
   every cleared level before what comes next) the vertical Hell map after Video2 (the plunge from FLOOR 0 to FLOOR 1,000 with the welcome
   lines) and after every level (the marker climbs to the floor reached), Wardens as ??? silhouettes until met,
   cracks and dotted gaps where shortcuts will be.
   *Built* (2026-10-18, first pass; recipe: GameplayCookbook "The Shaft"): `ShaftScreen` answers the hook with a
   map configured in `ui/shaft.json` (art, 2026-10-18: each Circle a painted band of its biome stacked bottom to
   top, the Meadow's daylight hole at the very top, each Warden's own figure kept black against its Circle's glow
   until met, and the hero's marker his own helmet and rocket launcher; placeholder shapes only where a painting
   is still missing), its words
   in theme.json `texts`. After Video2 the view falls from the Meadow's burned hole past the 10 Circle bands, the
   counter racing, and slams onto FLOOR 1,000 (shake, flash, *"WELCOME TO THE BOTTOM. NINE HUNDRED NINETY-NINE TO
   GO."*, *"...Great."*). After every stage with a floor range the marker climbs to the stage's last floor, the
   counter ticking a floor at a time, and the screen says how many floors are left to the top (Floor 1) and which
   Warden is next and how far: *"THE MAGMA KING: 90 FLOORS UP"*. The next Warden is always named there (Hell's
   announcer loves a show and tells you who is waiting); on the map itself the Warden stays a **???** silhouette
   until the hero has beaten him (reached the top floor of his Circle). The Meadow and the arcade levels have no
   floors, so no Shaft follows them. The first Shaft can't be skipped; every one after it can (a tap or a key).
   It remembers the floor its marker was last shown on; the deepest floor reached is the lowest last floor of the
   cleared levels. New Game resets it.
7. **Voices**: `overlord` and `yooba` in `audio.json` `voices`, and the prologue, Shaft and Circle X lines.

8. **Waystones and runs** (built 2026-10-18; see *Waystones and runs*): `RunFlow` (Core/Progression) and
   `WaystoneProgress` (the save: highest Waystone, the run's level, runs ended). Waystones are `ui/shaft.json`'s
   bottom floor and each band's top floor under the Overlord's. A level that starts on a Waystone reaches it (the
   Landing), one that ends on one reaches it (a Warden's arena). The death scene of a death on a floor stage ends the
   run (`RogueliteManager.EndRun`: Grudges, their max health and the carried health go) and its end hands the
   Shaft an `AfterDeath` stop: `ShaftScreen` mode `Drop`, from the floor he died on to the Waystone, the lines in
   theme.json `texts` (`shaftFirstDrop`, `shaftDrop`, ...) and `ui/shaft.json` (`firstDropVoice`, ...), the stones on
   the map (`waystoneLit` / `waystoneDark`). The death screen's title and gloats are `deaths/*.json` `title` and
   `gloats`. Main menu Continue opens the run's level; New Run (`mainMenu.json` `labels.newRun`) starts one at the
   highest Waystone. Playtest: `waystone`.

9. **Shortcuts** (built 2026-10-18; see *Shortcuts*; recipe: GameplayCookbook "A shortcut"): the level action
   `offerShortcut` (the two cards, the cost, paid once, final through rewinds) and the trigger `shortcutChosen` the
   script branches on; a taken shortcut's `nextLevel` replaces the stage's `nextLevelNumber`, the run goes on from it,
   and `ShortcutProgress` (Data) keeps it for the Shaft's dotted jump (`ShaftStop.ShortcutFrom`/`ShortcutTo`). The
   first one is the Old Stair's (`Level14.json`, level 62, Floor 871 to 841). Playtest: `shortcut`.

10. **Hell's tricks** (built 2026-10-25; see *Hell's tricks*; recipe: GameplayCookbook "Bouncing shots, a blink, a front shield, a sweeping laser, predictive aim"):
   `projectile.bounces`, the `teleport`, `frontShield` and `laserSweep` abilities (`Gameplay/Content/Mechanics.cs`) and `Aim.Predictive`.
   One test enemy each (`ricochetImp`, `blinkImp`, `bulwarkImp`, `beamLens`, `leadImp`), all on the `mechanicsTest` bench. Playtest: `mechanics`.
   Not built: a shield that sends shots back (it only spends them), and their own art (the imps are tinted).

## On the website

The website's lore (`Tools/Web/site`, installed by the deploy; built 2026-10-18) is this page told for visitors, the
way the hero got it: *the Descent* (`blog/lore.html`) falls from the Meadow (Floor 0) through Circles I to X to Floor
1,000, like the Shaft's first plunge, then a page per character, resident, hazard, place and rule. What it shows
follows what the game has revealed:

- **Wardens** are black figures with **???** until the hero has beaten them in the game; hovering or tapping one shows
  only a hint from its working look (a cracked bell, a lantern of blue runes), never its name. Exceptions, as on the
  Shaft: the Magma King (beaten, named, painted), the Spire Fiend (the next Warden, so named on hover) and the Overlord
  (known from the prologue).
- **Circles not built yet** show their biome, and their enemy families marked *(rumoured)*: this table is still a
  proposal.
- **One level = one floor** (LevelDesign.md): the site describes Hell's floors as the new design has them (every
  floor its own climb, a floor boss every 10th floor, places on the floors LevelDesign.md gives them: the Spill 998,
  the Loop 995, the Guard Post 954), not today's 10-floor stages; only the engine post says what today's build does.
- **No finale spoilers:** the Overlord's fight at Floor 1 and the ending stay off the site ("Hell hasn't shown anyone yet").
- **The early builds:** Bork, Slobber and Sintinel (the arcade version's monsters) keep their old pages, marked *from
  the early builds* and not on the climb. The old pages' Yooba-as-avenger, "chose to go" and "World 1/World 2" are
  corrected on the pages themselves, each in a *What changed* note: Yooba is the village slime, nobody chose the portal,
  and Hell is 1,000 floors.

## Lore videos

Four short films (~25-40 s each) tell what the game only hints at: the Overlord counting the village's diamonds (after
Level 21), the Shades' endless walk (after Circle VII's first floor), the village and Yooba before the portal (after
Level 13) and the Magma King's backstory (after Level 31). Scripts, shot lists, placement in the level/Shaft flow and
prices are in [LoreVideos.md](LoreVideos.md); none is made yet. They keep the canon above: no Overlord origin (the
late clues keep that), slimes are villagers, no office jokes.

**The cinematic plan** (LoreVideos.md, Part 2, 2026-10-25) adds 10 Circle title cinematics (~8 s, on the Shaft as each Circle
wakes), 30 boss intro stings (6 s for each Warden and the Overlord, 4 s for each floor boss, once per save in the arena
intro), two Shaft bridges (the Descent after Video2, the Ascent in the true ending) and the two ending epilogues (*Knock* after
the true ending, *Encore* closing the funny one). Scripts, shot lists, placement and prices (about $33, $38 with the idle tail)
are there; none is made, the GPU waits for J.

## Teasers

Teasers and trailers (`Marketing/`, made from the game's own art through Hawkes Gen) tell the prologue's edges, never
past it, the same way the website does. What they add to the story:

- **The hut in the teasers is the hero's.** The thatched hut (Video1 / Teaser01's meadow, not the live main menu — the menu is the
  lava ledge diorama) is where he lives. He sleeps in his armour, on a straw bed, his rocket launcher leaning on the wall.
- **The nice afternoon** (Video1) starts with him asleep. Sunbeam, dust, a candle, a snore that rattles his visor. He
  is already tired of everything, including sleeping.
- **His eyes glow when he's had enough.** The visor's orange eyes (his sprite has them) are dark while he sleeps
  and light up when he wakes into a grudge. Teaser01's line: *"I'm tired... of sleeping..."*
- **The quiet village after:** the meadow with his hut gone and ash falling (the website's `QuietVillageAfter`)
  is what the Overlord's afternoon leaves; teasers may cut to it as a flash-forward. They never show the portal, the
  Shaft or a Warden the game hasn't revealed.

Teaser01 (YouTube, 2026-10-18): the menu's meadow, a push into his doorway, the hut inside, the line, a boom, the
village after, the title. Its shot list is `Marketing/Teaser01/README.md`.

## Art

Port the Dose Of Destruction pipeline (`C:\Projects\doseofdestructionwebsite`, see its `docs/comfyui.md`,
`docs/animation.md`, `docs/maps.md`):

- **Characters:** one still per enemy → side-view poses (Qwen-Image-Edit on the ComfyUI GPU server) → short
  clips between poses (Wan 2.2 first-last-frame) → background keyed (BiRefNet / white key) → frames aligned on the
  feet → sprite sheet → the entity's `visual.animator`. Clip list per enemy kind: idle, move, attack, hurt, die.
- **Maps:** each Circle gets a painted backdrop and props (Qwen, two passes) plus the existing video loops.
- **Agent-first:** a request like "make the Lurker" should be one config entry (description, poses, clips) and a
  command that queues the GPU jobs, builds sheets and writes the entity's animator, with a preview to check.

## How sick of it are you? (difficulty and accessibility)

New Game starts at **Normal** with no card (2026-11-11; see *First five minutes*). *Pick Your Hell* lives in Settings:
**Assist**, **Normal**, **Hard**, **Hell Loop**. It is the hero's mood, not a story fork: the same Hell, the same lines. Normal is the bullet hell as
designed and changes nothing. The pick scales four things for a whole play (`Data/Difficulty.cs`; saved in PlayerPrefs `Difficulty`): how fast hostile
shots fly (x0.8 / 1 / 1.15 / 1.3), how often enemies shoot (density x0.75 / 1 / 1.25 / 1.5, on `shoot` abilities), how hard a hit lands on the hero
(x0.5 / 1 / 1.5 / 2, a whole number of hearts, the fraction paid by a roll that belongs to the shot, so a rewind replays it; pits and lava are not
scaled) and rewind juice held and earned (x1.5 / 1 / 0.75 / 0.5). Assist also turns Aim Assist on. After three deaths inside five real minutes of a New
Game the death screen offers **Try Assist** once (`FtueProgress`). Settings > Accessibility holds five toggles (plus incoming warnings and touch buttons), each saved at once: bullet outlines,
high-contrast bullets (one hot colour with an ink rim), reduced flash, reduced shake, aim assist (a shot aimed within
9 degrees of an enemy bends most of the way onto it). `BulletReadability` (`Gameplay/Presentation/AccessibilityVisuals.cs`) is presentation
only. Nothing is drawn on the hero (the hitbox ring and dot were removed 2026-11-12). Playtest `difficulty`.

## The hero's own flinch, deaths and victory

The hero is on screen when Hell hits him, kills him and when a floor falls. A hit makes him flinch (state `Hurt`, a short clip, never only a red flash). Hell kills him four ways, which come round in turn,
one per death: `DeathBurn` (lit up like a firework, a puff of smoke), `DeathSquash` (looks up, sighs, flattened), `DeathSpin` (launched, waves goodbye at the top) and `DeathShrug` (shrugs and lies down to quit).
The death scene (the baked video) shows the same death over it. Clearing a floor plays `Victory`: a nod and the smallest gauntlet pump, a flat "fine". All deadpan; specs in `Assets/GameConfig/art/hero.json`.
All of them are generated clips of his own (`art/hero.json`).

## Other climbers: the ghosts (built 2026-10-26)

Hell is repetition, and the hero is not the only one stuck in it. Everyone else playing Sick Of It right now climbs the same floor beside him as a
faint, pale ghost of the hero: running, jumping and falling through their own attempt. They cannot touch him, help him or hurt him; a rewind
turns back his own time, never theirs (they are other people, now). A floor with ghosts on it is a crowd of nobodies all sick of the same thing.
In the game: `Core/Progression/GhostLobby.cs` on Hawkes Net (one room for everyone, `lobby` on sickofitgame.com, `t-lobby` on beta and in playtests, through Hawkes Net's standard `HawkesNetLobby`),
WebGL only, `?ghosts=0` turns it off. Presentation only, never the simulation.

The main menu is already haunted. Before he climbs, the hero stands on the lava ledge (see *The main menu is a living diorama*) with everyone
else who is on the menu right now: their ghosts idle and wander the same ledge (wherever their own hero walks, spread so a crowd of idlers
doesn't stand in one spot), and a chip in the corner counts the ones already climbing ("12 climbing right now"). Nobody is ever alone in being
sick of it. In the game: the menu is its own band in the same room (z 0; a level is its number x 1000, nowhere -1000), `GhostLobby.MenuOpen`
while `MainMenuManager` is up, the chip's words in mainMenu.json `labels.climbingNow`.

## The main menu is a living diorama (built 2026-11-12)

The first screen is Hell, not the meadow: the hero idles bored on a painted basalt ledge over a lava sea (ash-to-ember sky, far cliff teeth, cyan
rewind seam that sparks under his feet, drifting embers, an occasional lava burst) while the theme (or the Landing's lava bed until the dedicated
theme lands) plays. A pale damned soul sits further along the ledge — it looks up when the Overlord hums and turns when the hero walks near. Far
above, the Overlord's black-obsidian silhouette — long horns, red eyes, never looking down — climbs past every 15–20 seconds with a low hum and
sheds one ember toward the ledge; the hero faces him and mutters "Still climbing. Of course." The camera slowly pushes in 3% over 20 s and back.
The title **Sick Of It** slams in over half a second on first show, then breathes with a soft pulse. The buttons sit on illustrated basalt-and-ember
plates with hover and press states; the staged first-visit menu (Play / Settings / Rage Quit) and its button count stay the same. New art loads
after the buttons are up so a phone still reaches the menu inside 20 s. In the game: `MenuLavaBackdrop` (painted ledge, soul, lava bursts, Overlord
climb + hum + dropped ember, seam spark, camera breathe), `MenuInkPlates` (button plates), `MainMenuManager` (logo slam, face+mutter, voiced sigh,
bed after the buttons, click harden). Playtest `living-menu`.

## The main theme and the UI kit (k_aea2f5751795)

Hell greets you with its own song: a **sardonic surf-metal** main theme in E minor at 120 BPM, distorted surf guitars under a
**whistled lead motif** that floor cues can quote later. The menu plays an intro then a seamless loop (`menu_theme` →
`menu_theme_loop` via Hawkes Gen `audio.music` + `audio.music-loop` intro-loop); until those clips land the ledge keeps
the Landing's lava bed so it is never silent. Theme starts within a second of the menu appearing (after the page's tap).

Every button, purchase, unlock and clear answers with a crafted one-shot on the **UI bus** through one helper
(`UiSounds`, never a per-button AudioSource): hover, press, back, toggle, panel open, Yooba's shop open (door bell +
slime squelch), purchase (cash register + slurp), can't afford (deflating buzz), unlock fanfare (NEW! sticker),
level-clear fanfare (2–5 s while the summary appears), rank stamp, new-best jingle and death sting. Pause leaves the UI
bus up so menus still click. Playtest `ui-sounds` (also at 390x844: press ≤100 ms after touch-up). Clips are Gen-made
when org credits allow; ids sit in `Tools/Audio/pending_clips.json` until then.

## Rewind that feels amazing (RewindFx)

Rewind is the hook, so holding R turns the picture into **time pouring backwards** (Prince of Persia: The Sands of Time): the world desaturates to about 20% colour while the hero and every bullet stay full-colour; the hero leaves fading ghost copies of his recent places (8 on desktop, 4 on a phone); every bullet draws a short bright streak in its reverse direction; sand and ember motes stream backwards across the screen; and the screen edges (the outer ~8%, never the play area) carry a gold time-ripple. Scanlines, if any, stay only on that outer rim — never over the central playfield (critic feel fix). On release: a 0.2 s colour snap-back and a gold ripple out from the hero (`FeelVfx.RewindRipple`). A reversed tape whirr still loops (`rewind_start` played backwards) with its pitch climbing; Reduced Flash halves the look and stops the wobble. `Gameplay/Presentation/RewindFx.cs` + `RewindWorldLook.cs`, presentation only; playtest `rewind-fx`.

## A hit that doesn't hide the fight

Getting hurt flashes the **hero sprite** white for 0.10–0.15 s (never a full-screen white wash that eats the bullets), shoves him at least 0.3 world units away from the shot, and pulses a red edge vignette. Screen shakes on a hit last 0.3 s or less. The phone rewind button wears a fill ring that drains while held. Playtest `rewind-fx` (phone pass at 390x844).

## The first rewind (Level 1)

The Meadow teaches rewind by happening to the hero: the sky opens at about 4 s, imps build steadily, and at about 30 s a ring of embers closes on him that nobody can dodge (`stagedRewind`). Time drops to a fifth, the rewind button swells and pulses, and he mutters "Hold R." (`vo_hero_cmp_hold_r`). Holding it undoes the hit; he says "Noted." The replay then throws a single dodgeable ember. One caption at a time, six words at most. The title card is a small corner tag.

## The menu only shows what you have met, and the shop reads at a glance (built 2026-11-11)

**Why.** A stranger on the first menu used to meet a wall of nine buttons and a counter of thirty-five goods with joke names. Now the
first menu is the hero idling on the meadow with Play, Settings and Rage Quit (in a browser Rage Quit only appears after a real rage quit, as before). Everything else arrives with the thing that earns it, wearing a one-time NEW! burst, the same comic sticker as
"Now with rewind!": **Visit Yooba** after the first diamonds (the slime he is: he turns up when there is something to spend), **Pick a Level**,
**Rewatch Stuff** and the **Codex** once Floor 1000 is cleared, **Endless Floor**, the **Daily Challenge** and **New Game+** after the first
Waystone. Play is a New Game on a brand-new save; after that the button is Continue, with New Game beside it. A reveal is kept for good
(`MenuReveal`, prefs `Menu_Unlocked`/`Menu_Seen`): New Game wipes the climb, never what he has unlocked; a save from before this change gets
every button it qualifies for and no stickers.

**The counter.** Yooba's wares sit on three tabs, Body (hearts, jumps, speed, plates, bubbles, dodge, burps), Rewind (jars, unwinding, crumbs,
thrift) and Loot (diamond pull and value, shot damage and speed, crits, bombs, fists, Keep Weapons), six cards a page. Each card leads with
what it does in numbers, made from the ware's effect and per-level amount ("+2 hearts", "+8% move speed", "+0.4 s per clean kill"); the
joke name and his quip come second, smaller. A ware the hero can pay for says "CAN BUY" in green and puts a ♦ on its tab. On the first visit
with something to spend and nothing owned, Yooba whispers "Psst! The glowing one. You can afford it. Start there." (`vo_yooba_shop_pick`, clip
pending in `Tools/Audio/pending_clips.json`) and the first affordable ware's card glows; buying it, or his saying it, ends that for good.
Canon: Yooba never says what a ware costs him; the card says it plainly because the hero is sick of guessing.



## The first four floors teach

Hell's first four floors are a lesson the hero is too tired to enjoy. Each one hands him one new way to die, shows it to him
alone and slow (the glow he has learned to read, a sound he will learn to dread), lets him make friends with it, then ends with
the whole floor leaning on it: the music turns, the screen heats up and the Show cheers him through. 1000 is the floor itself
(sinking stones), 999 the lava that follows him, 998 the village's sparks, 997 the imps who carry what he lost. No new creatures:
only what the bestiary already holds, taught in order. Table and design: Docs/LevelDesign.md "The first four floors: teach, then finale".
