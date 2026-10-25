# Sick Of It: Story Bible

The story is the design document. Every level, map, enemy, upgrade and line of dialogue should be traceable to
something on this page. If a request needs something the story doesn't explain yet, add it here first, then build
it. Agents: read this before adding or changing a level, an enemy, an upgrade or a voice line.

Status: **draft for review** (2026-10-15; Circle IX's authored floors 900-892, 860-852 and 810-802 made hotter long bullet-hell floors of ~5 minutes, 2026-10-25; Circle X's floors 997-992 and 960-952 made long bullet-hell floors of ~5 minutes, same ideas and lines, 2026-10-25; weapons found on floors and kept for good, the launcher's theft on Floor 998, 2026-10-25; Circle X's played route built to Floor 901, 2026-10-19; Circle IX's played route built to the Spire Fiend's door at 802, 2026-10-19; prologue, the Shaft and the floor layout revised 2026-10-18; Waystones and runs 2026-10-18; the Magma King's three-phase fight, 2026-10-18 (now Floor 901, level 31); Shortcuts and Circle IX's first stretch, Levels 12-18, 2026-10-18; the slime named Yooba, 2026-10-18; **one level = one floor**, 2026-10-18, see [LevelDesign.md](LevelDesign.md); On the website, 2026-10-18; Teasers, 2026-10-18; Circles VIII to IV designed, 2026-10-25; Circles III to I, the final boss and the endings designed, 2026-10-25). Names marked *(working)* are placeholders.

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
| **The Hero** | No name, on purpose (J, 2026-10-18: it goes with the absurdity); everyone, the announcer included, calls him "the hero". Thirties, unimpressed, has had enough. Talks to himself in one-liners. Never gives speeches. | `audio.json` voice `player` | Player sprites |
| **The Overlord** | Giant obsidian titan with red eyes. Ruler of Hell's 1,000 floors, as sick of Hell as the hero is of everything, and done waiting to take the surface. Never in a hurry; barely notices the hero until he can't ignore him. Says very little, and every word sounds like it cost him nothing. | `audio.json` voice `overlord`: deep, slow, enormous, barely bothered; a voice that fills a valley without being raised. Short lines, long pauses. When he lasers the portal open he barely notices the hero: *"Mm. ... Stay."* or a bored exhale. | Video1, Level1 backgrounds |
| **Yooba** | The village's green slime and its gossip, and J's own character (the same Yooba as Yooba Land; his name is final, not a working one). Swallowed a handful of the village's diamonds to hide them from the imps, got sucked through the portal with the hero, and now trades and tinkers between floors out of his own stomach. **Slimes are villagers, never enemies.** | `audio.json` voice `yooba`: friendly, chatty, wet; talks fast, gurgles between words, never stops for breath, delighted to see anyone, a bit too pleased with himself. | Video3 (the talking slime); his talking portrait (`ui/yooba.json`, see *Yooba between floors*) |
| **The Announcer** | Hell's own voice: loves a show, calls out floors, bosses and deaths like an arcade cabinet. | `announcer` | Floor titles, death screens |
| **The Wardens** | One boss per Circle: the monster that rules it. Each guards its Circle's top floor. | Per Warden | Death videos, bosses |
| **The Magma King** | Circle X's Warden: a huge king of slag and obsidian with a lava crown, slouched on a throne of slag at Floor 901. He never gets up, unless you make him: the Wastes come to him. Smug where the Overlord is bored; proud, loves an audience and takes Hell's show seriously (a challenger is the best entertainment the Wastes have had in ages). He hears everything that climbs his stairs and enjoys the wait. Making him stand is what enrages him (see *The Magma King's fight*). | `audio.json` voice `magmaKing`: very deep, thick, gurgling bass; slow, smug, amused, lazy, proud; chuckles (*"Mm-hm-hm."*), never shouts. The one line with weight is when he stands. Heard before he's seen, from ahead on the Heat Stairs. | `art/magmaKing.json`: generated sheets in `Assets/Art/Enemies/MagmaKing/` |
| **The Spire Fiend** | Circle IX's Warden: a gaunt basalt demon rooted in the heart of the great spire at Floor 801, his lower body a spire instead of legs, rock spires bristling from his head, shoulders and back, thin orange seams glowing all over him. He **grows the spires**: every spire on the Cracked Road is one he pushed up through the road (that is why the road keeps splitting), and the great spire is the one he has grown for ages so that nothing in the Circle stands higher than him. Hell's loop is his too: every night the spire sheds back to the same height, every day he grows it again. He can't bear anything above him, and a hero climbing *up* is the worst thing he can think of. Not a showman like the Magma King: he **listens** (the hum on the Needle Stair is him, listening through the rock), counts steps, and whispers. Making him lose his calm is what enrages him (see *The Spire Fiend's fight*). | `audio.json` voice `spireFiend`: a dry, hollow, rasping whisper with stone grinding under it; slow, patient, cold, precise; hums (*"Hmmmm."*) instead of laughing. The one line he raises is the enrage. Never heard on the route, only his spire's hum. | `art/spireFiend.json`: generated sheets in `Assets/Art/Enemies/SpireFiend/` |

## Prologue: how the hero gets into Hell

Level 1 is the Meadow, and it ends in a scripted scene, not a cut. In order:

1. **Video1.** A nice afternoon. The Overlord rises over the hills; his imps drop in to loot the village.
2. **Level 1, the Meadow.** The hero clears the imps and the cinders. Diamonds, a first taste of rewind.
3. **The Grudge pick** (the level's `offerUpgrades`): his first grudge. It happens before the scene, so the scene
   is never interrupted.
4. **The portal.** The Overlord's lasers come down one last time, not at the hero (he isn't worth it yet) but at
   the meadow near him. They burn a glowing hole into the meadow: a portal, lying flat on the ground.
   It is always the same spot: where the Overlord's laser hits the meadow in the background footage (Level 1's
   laser clip, a little left of centre on the ground line), and the small fire left burning there in the aftermath
   loop is this portal seen from afar. The game's beam, impact, portal and scorch sit right on top of it, wherever
   the hero happens to be standing; he walks over to it from his side, and Yooba comes from the other.
   How it looks: first a red-orange glow gathers on the grass (the beam charging), then the beam drops out of the
   sky as one burning column and bursts on the ground. The grass scorches, cracks, and tears open into a ring of
   burning turf with fire swirling down inside it: **the same fire portal the hero falls through in Video2**,
   seen side-on from the meadow, so the player recognises it when Video2 starts. It swirls the whole time he
   stands at its edge, and once it has had him (and Yooba) it flares and shrinks to a black scorch mark.
5. **The walk-over.** The hero walks over to it on his own, curious (scripted: the player's input is off), and
   leans in to peer down at it, head tilted (his `Curious` clip).
   Hero: *"That's a hole."* Beat. *"So that's where they keep coming from."*
6. **The echo.** He cups a hand and shouts down *"Hello?"* (`Shout`) Hell answers: *"...hello? ...hello? ...hello?"*, each one
   deeper than the last. It's the first hint that **Hell is repetition**: even its echo won't stop.
   The echo is his own voice coming back, slower, darker and more broken each time: the loop answering him
   (`vo_portal_echo`).
7. **"Nope."** He turns to leave. The portal grabs him: yanked off his feet backward, flailing (`Grabbed`), he
   spins and shrinks into it, and he's gone. He is still tumbling the same way down Video2's fire tunnel.
8. **Yooba.** A beat later a green village slime bounces past, wobbles at the edge, and is slurped in after him.
   Nobody remarks on it. It pays off in Video3, when the same slime turns up at the bottom of Hell, talking.
9. **Video2.** The burning door; the hero tumbling and spinning down the fire tunnel; the lava river at Floor
   1,000.
10. **The Shaft** (below): the camera plunges from the Meadow to the bottom of Hell, and the climb begins.

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
- **The path today** (re-pathed 2026-10-19): New Game → Video1 → Level 1, the Meadow → Video2 → **Floor 1000, The
  Landing** → Video3 (Yooba) → **Floors 999-991** → *the Sack Lift* → **Floors 960-951** → *the King's Chimney* →
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

- **First time, after Video2:** the camera plunges from the Meadow down the whole Shaft, past all 10 Circles, to
  FLOOR 1,000, where the hero's marker lands. Announcer: *"WELCOME TO THE BOTTOM. NINE HUNDRED NINETY-NINE TO GO."*
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

**Video3's redo (J, 2026-10-24: the old footage "doesn't fit").** The talking slime is the old game's footage, not the
Landing. The new Video3 is ~25-30 s, six shots, no talking head: (1) the Landing's lava shore, the hero face down in the
ash where Video2's tunnel spat him; (2) he sits up, deadpan, looks at the lava sea; announcer, far off: *"WELCOME TO THE
BOTTOM."*; (3) something wet slaps down behind him, a green blob with two eyestalks: Yooba, from the village; (4) Yooba
gurgles hello, the hero looks at him a long beat; (5) Yooba coughs up a diamond, holds it out, delighted; (6) the hero
takes it, looks up the dark Shaft: *"...Great."* The Shaft's hello (Floor 999) then picks up from there. Hero in the
Meadow's clothes, Landing in the game's lava palette, Yooba as his portrait (`ui/yooba.json`); no office jokes.

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
  | Goo Plaster | +1 max health | a dab of Yooba over the dents |
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
- **Yooba's prices** (2026-10-25). Hell is repetition, and so is saving up: the shop now takes about **three times as
  many floors** to empty as it did (diamond drops are unchanged; the prices went up). To buy every level of every good
  used to take **655** diamonds; now it takes **1,705**, or **1,970** with *Double-Dipped Shots* (**2,435** with the weapon goods below: Slimed Knuckles 60+120, Stretchy Wraps 45+90, Keep Weapons 150). About 2.6x the old
  prices on the old goods; with the new good on the counter, 3.0x the old total.

  | Goods | Levels | Old prices | New prices | Old total | New total |
  |---|---|---|---|---|---|
  | Goo Plaster | 5 | 10, 20, 35, 55, 80 | 25, 55, 90, 145, 210 | 200 | 525 |
  | Bouncy Soles | 2 | 15, 35 | 40, 90 | 50 | 130 |
  | Slick Heels | 3 | 12, 25, 40 | 30, 65, 105 | 77 | 200 |
  | Licked Trigger | 3 | 15, 30, 50 | 40, 80, 130 | 95 | 250 |
  | Tummy-Dipped Shots | 3 | 20, 40, 70 | 50, 105, 180 | 130 | 335 |
  | Loop Jar | 3 | 12, 25, 40 | 30, 65, 105 | 77 | 200 |
  | Shiny Sniffer | 2 | 8, 18 | 20, 45 | 26 | 65 |
  | Double-Dipped Shots *(gated)* | 2 | - | 100, 165 | - | 265 |
  | **All** | | | | **655** | **1,705** (1,970 with the gated good) |

  How many floors that is depends on how many diamonds a floor pays; the playtest reports the diamonds each level paid
  (`level<n>.diamonds`) and the shop's total (`shop.totalPrice`, `shop.basePrice`), so the ratio can be read off any
  run. Enemy `loot` chances and values are left alone on purpose (the unit of the economy is the diamond; changing
  both sides at once hides which one moved); to slow the shop further, raise prices, to speed it up, raise `loot`.
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

## Waystones and runs

Hell doesn't let you die. It sends you back to repeat the floors: the whole place runs on a loop, and the hero is
just one more thing in it. A **run** is one climb from a Waystone until Hell kills him.

- **Waystones** are the stones where Hell starts its loops over: the **Landing** at Floor 1,000 (the first, reached
  when he lands there) and the top floor of every Circle once its Warden is beaten (Floor 901 when the Magma King
  falls, then 801, ... 101). The highest one reached is saved; it is where every run starts from then on.
- **Dying on a floor of Hell** (any stage with floors) ends the run. First the death screen: the death video with
  *"YOU OBVIOUSLY DIED"* over it and Hell's announcer gloating (*"OHHH! HELL TAKES THE ROUND!"*, *"FLATTENED! WHAT A
  FINISH... FOR HELL!"*, *"AND HE'S DOWN! BACK YOU GO!"*, in turn). Then the Shaft: the hero's marker sits on the
  floor he died on, then falls, faster and faster, back down to the Waystone and lands with a slam. The run
  restarts there, with the stage that starts on (or just above) it: from the Landing, Floor 1,000 (Level 2).
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
  push track or a Warden's theme (they are intense already), and stops for a rewind. Until the layers are made, the Wastes' push track stands in
  for every Circle's.
- **Stingers.** A short fanfare when a **new weapon** is found for the first time (`sting_weapon`, with the "NEW WEAPON" banner)
  and one per **rank** on the results screen (`sting_rank_s` to `sting_rank_d`, `theme.json` grade `stingSound`): S golden and huge, A
  confident, B tidy, C a lazy shrug, D a sad comedic wah-wah. Stand-ins (the Shaft's climb sting) play until they are made.
- **The manifest** `Tools/Audio/music_manifest.json` lists every track still to make (prompt, tempo, key, takes, file, stand-in) and its
  price (`python3 Tools/Audio/music_manifest.py --price`): 34 tracks, 102 takes, ~$0.58 of GPU. Best of three takes each, picked against the
  manifest's `pick` line, then cut by `music-mix.py`. Nothing in it is generated until J approves the price.
- **The Shaft:** the plunge after Video2 is a **falling riser** that **slams** as the marker hits Floor 1,000; each
  climb gets a **short rising sting** as the marker goes up.

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
| `imp` | Imp | Meadow, X | The Overlord's raiders. They loot the village's diamonds, then guard the way down in the Wastes. Throw slow embers. Die like Hell's own fireworks: a shriek, eyes bulging, then a pop of soot and embers with the horns and the (empty) sack dropping out, because they are made of the Wastes' fire. | generated (`art/imp.json`, Docs/ArtPipeline.md) |
| `cinder` | Cinder | Meadow, X | A spark from the burning village that came alive and followed the hero through the portal. Drifts toward you and pops. | `Fireball` (exists); spec ready (`art/cinder.json`) |
| `impSack` | Sack imp | X (from Floor 997, Imp Run) | An imp hauling one of the village's diamond sacks down to the Overlord's gate. Too busy carrying to throw anything: it runs back and forth with the sack, and every hit tears the sack and spills a diamond. Diamonds from a sack: rewards that make sense. | the imp's art, tinted warm until its own sheet (an imp with a full sack) is made |
| `impSwing` | Sack imp on a swing | X (Floor 997, Imp Run, on) | A sack imp riding one of the hauling chains across a gap with the village's diamond sack (LevelDesign.md, *the hauling chains*): it swings on the chain's beat, never throws (both hands on the sack), burns on touch, and every hit tears the sack and spills a diamond. Too busy to chase anyone. | the imp's art, tinted warm, on the swing chain's links |
| `impFleeing` | A startled imp | X (Floor 1000) | The first imp he meets in Hell: a hauler on the landing who sees him, drops its ember into the lava and scrambles up its chain with its sack, which snags and drops a diamond. Harmless; it is gone in a few seconds. | the imp's art |
| `impForeman` | The Imp Foreman (floor boss) | X (Floor 991) | The fattest imp in the Wastes, on the biggest sack of the village's diamonds, with a chain whip; he drives the sack haulers up to the Sack Lift and loads it. Too heavy to fly: his wings are for show. Two phases on a climbing arena: on the loading floor he cracks his whip along the tier (a glow on the floor 1 s first) and whistles haulers onto the swings; at half health the lava takes the tier and he hauls himself up to the lift, where he swings on its chain dropping embers from his sack (a glow on the spot 1 s first). Beaten, his sack bursts: a pile of diamonds. Voice `foreman`: hoarse, nasal, wheezing, bossy. | `art/impForeman.json`: generated sheets in `Assets/Art/Enemies/ImpForeman/` (2026-10-18) |
| `impCutter` | Chain-cutter imp | X (Floor 994) | An imp at a lift's pulley hacking at the chain to drop the lift (and the hero) into the lava. Doesn't throw: the chain is its target. Shoot it and the lift holds. | the imp's art |
| `damnedSoul` | A damned soul | X (from Floor 999) | A soul stuck in Hell's loop, repeating one small thing on one ledge forever (on 999, counting the same stone). It can't fight or be hurt. When the hero passes close, his loop brushes its loop, it hiccups and a loop shard drops (once per play). The only thing that drops loop shards. | `art/damnedSoul.json`: a hunched grey soul in a shroud turning its one stone over (idle) and jolting when his loop brushes it (hiccup), drawn see-through by its `visual.color`; generated sheets in `Assets/Art/Enemies/DamnedSoul/` (2026-10-18) |
| `lavaGeyser`, `geyserVent` | Lava geyser (hazard) | X (from Floor 996, the Slag Falls) | The lava sea under pressure: a basalt vent whose crack glows and spits for a second, then blows a column of lava straight up for a moment. Fires on a fixed beat (Hell's loop), so a rewind shows the order. | `Art/Climb/Vent_*`, `Geyser_column` (made 2026-10-18) |
| `slagDrip`, `slag` | Falling slag (hazard) | X (from Floor 970, the Slag Falls) | Molten rock from the falls. A glowing drip gathers for 1.2 s (the telegraph, with a hiss), then a lump of slag falls from that spot and sets you burning. Not a creature; nothing to kill. | `Fireball`, tinted |
| `lavaBurst` | Lava burst (hazard) | X (the Loop) | A bubble swelling in a crack (1.3 s of glow), then three embers burst up out of it. In the Loop it goes off in the same crack every time. | the beam's charge glow, tinted |
| `cinderThatch`, `cinderSpark` | Thatch cinder | X (from Floor 950, Halfway Basin) | A burning clump of the village's thatch roofs that fell through with everything else and came alive like the other sparks. Bigger and slower than a cinder; shot, it bursts into two sparks (`cinderSpark`) that dart at you for two seconds and go out (they can't be shot, only dodged). | `Fireball`, scaled up and tinted straw-orange, until its own sheet |
| `impGuard` | Imp guard | X (from Floor 930, the Sack Pile) | An imp: the same family (it `extends` `imp`), one the Magma King keeps at his pile and on his stairs instead of sending it up with a sack. It carries no diamonds and doesn't run; it plants itself and throws three embers in a fan, so guards are the first imps that fight together (lines of three on the Sack Pile and the Heat Stairs). It drops nothing: `"drops": []` replaces the imp's drops (no imp drops loop shards; those come only from the damned). The only diamond it gives up is its pay from the pile, now and then (`loot`). | the imp's own art, tinted dark red, until `art/impGuard.json` (an imp in a slag helmet with a spear) is made: a tinted sprite of the same creature, which rule 5 allows |
| `emberShower` | Ember shower (hazard) | X (from Floor 940, the Crust) | Steam off the lava river raining embers two at a time; announced by the hero (*"Now it's raining fire."*) 1.5 s before the first. | `fireball` |
| `magmaKing` | The Magma King | X (Warden, Floor 901) | Rules the Ember Wastes from a throne of slag. Heard (voice `magmaKing`) on the Heat Stairs before he is met. Fights seated (boulders `magmaBoulder`, fist slam → `slamShockwave`) until half health, then stands for the only time and fights standing with his imps (ground-pound, fire breath); at a third he enrages and floods the hall (`lavaFlood`); see *The Magma King's fight*. Drops ten diamonds: his share of the stolen pile. | generated (`art/magmaKing.json`): idle, throw, slamSeat, hurt seated; rise, enrage; idleStand, slam, breath, hurtStand standing; die |
| `magmaBoulder`, `slamShockwave`, `lavaFlood` | Boulder, shockwave, lava flood | X (his arena) | The Magma King's attacks: a lump of molten slag lobbed in an arc that sets you burning, a low shockwave of slag that runs along the floor when he slams (jump it), and the flood (phase 3): a wall of lava too tall to jump that rolls most of the way across the hall (outrun it if you can; rewind if you can't). Each has its warning glow (`mkWarnHand`, `mkWarnFloor`, `mkWarnMouth`, `mkWarnFlood`), 1 s or more ahead. `lavaWave` is the older wave the arcade fight uses. | `Fireball`, scaled and tinted |
| `brimstoneDemon` | Brimstone Demon | IX | Born from the cracks in the Cracked Road: the rock splits and a demon climbs out to stop you climbing. Slow, heavy, plants itself in the road a few steps in front of you and breathes a short burst of fire (`brimstoneBreath`, sets you burning), so keep your distance. Sometimes drops a diamond: the imps' sacks leak on the road, the cracks swallow what falls, and the demons come out with it stuck in their rock. Not damned, so no loop shards. First met alone on Floor 900, the first floor of Circle IX (`levels/Floor900.json`). | `Demon1` (exists) until generated; spec ready (`art/brimstoneDemon.json`) |
| `demonWhelp` | Brimstone whelp | IX (from Floor 897, The Whelps) | The cracks' young: a small Brimstone Demon that scuttles out of a crack and charges along the ledge at him. Too small to breathe fire, not too small to bite (burns on touch); two hits. Not damned: no loop shards. | the demon's art (`Demon1`), small and tinted ember-red, until its own sheets are made (rule 5: the same creature) |
| `demonLobber` | Brimstone lobber | IX (from Floor 894, The Lobbers) | A Brimstone Demon that climbed the spires instead of the road: it squats on a spire ledge and lobs lumps of brimstone (`brimstoneLump`) in an arc at him, the landing spot glowing 1 s first (`lobberGlow`). | the demon's art, tinted ash-grey |
| `whelpNest` | Whelp nest | IX (from Floor 856, The Nest) | A crack in the rock where whelps are born: it glows and spits a whelp every few seconds until it is shot shut. A target, not a fighter: it never moves or attacks. | a glowing crack (placeholder shape) |
| `roadBreaker` | The Road Breaker (floor boss) | IX (Floor 891) | The biggest Brimstone Demon on the road, a slab of road for a club: he keeps the road broken so nothing climbs it. Two tiers: slams that run a crack along the road (a row of glows, each bursting 1.1 s later) and a lump of road kicked at the hero's spot (a glow first); at half health he breaks his own tier and the road gives way, then the same with whelps crawling out of his cracks. Voice `roadBreaker`: deep, gravelly, slow, proud of every crack. | the demon's art, scaled up and darkened, until `art/roadBreaker.json` |
| `carver` | The Carver (floor boss) | IX (Floor 851) | A gaunt demon with a chisel for a hand who carves everything on the road: the floor numbers, the rules (*KEEP TO THE ROAD*), the jokes (*THIS WAY UP*). Fussy, offended by footprints. Two tiers: chisel strikes along the tier and shards knocked loose over the hero (each after a 1.1 s glow); at half health he climbs his scaffold and whelps come out of his chiselled cracks. Voice `carver`: thin, fussy, precise, offended. | the demon's art, thin and pale, until `art/carver.json` |
| `sparedWhelp` | The spared whelp | IX (Floor 891) | A whelp pinned under the broken road after the Road Breaker falls, squealing. Harmless. Let go, it owes him: the Spared Demon shortcut. | the whelp's art |
| `roadSlab`, `settleSlab`, `splitSlab`, `risingSlab`, `seesawSlab`, `roadCreep` | The road's pieces | IX | The Cracked Road itself: solid slabs, slabs settling into the Rift, slabs splitting apart, slabs the Rift heaves up, slabs balanced on spire tips, road sliding off the spires (LevelDesign.md section 6). | `Art/Climb/Road*` (Hawkes Gen, 2026-10-19) |
| `spireShard`, `shard`, `brimstoneVent` | Spire shards, brimstone vents (hazards) | IX | The spires shedding (a glowing seam, then a shard), and the cracks breathing fire on a beat (a glowing crack, then a column). | the slag drip's and the geyser's art, tinted |
| `ricochetImp`, `ricochetEmber` | Ricochet imp | none yet (test bench) | An imp that throws Hell's habit: an ember that doesn't stop. Nothing in Hell is allowed to just end, so its ember bounces off the floor three times before it gives up (*Hell's tricks*). Throws slow, from above, so the bounce can be read. | the imp's art, tinted warm; the ember is the shared `Fireball` sheet |
| `blinkImp`, `blinkMark` | Blink imp | none yet (test bench) | An imp that slips through the seams of Hell's loop, the way the damned hiccup, but on purpose. It shows a violet mark next to the hero, stands still for 0.7 s, then is there. Not damned: no loop shards. | the imp's art, tinted violet |
| `bulwarkImp` | Bulwark imp | none yet (test bench) | An imp that carries a slab of slag in front of it (a bar of light on its facing side): shots from the front are spent on the slab. It paces; when it turns away, its back is bare. The first enemy that makes the hero move to shoot. | the imp's art, big and tinted steel |
| `leadImp` | Lead imp | none yet (test bench) | An imp that has watched the hero run and throws where he is going, not where he is. Standing still beats it; running in a line is the worst thing to do. | the imp's art, tinted ice-blue |
| `beamLens` | The Overlord's lens | none yet (test bench) | A shard of the lens the Overlord lasered the meadow with, left glowing on a ledge. A thin red line shows where it will start, 1.2 s later the beam sweeps across (a real beam: it stops at the floor and hurts where it crosses him). Not a creature: nothing to kill, only to get out of the way of. | `Fireball`, tinted and scaled |
| `fighter` → Drooler | Drooler | V | Cave beast; Death2 is it eating you. | `Fighter*` (exists) |
| `sentinel` → Lurker | Lurker | V | Crouching cave hunter; Death3. | `Level3Enemy*` (exists) |
| `chaser`, `swarmer`, `exploder`, `sludge`, `slimeGlob`, `slimeSplat` | (retired) | none | Slime enemies. Not in any level once the levels are rebuilt. Keep the files until nothing references them. | slime |
| `gunner`, `drone`, `sprayer`, `spinner`, `brute` | (to recast) | | Generic tinted variants from the arcade version. Each gets a Circle and a reason, or is retired. | tints |

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

## The HUD and menus (Hell's showmanship)

Hell loves a show, and the announcer's cabinet is the show's frame: nothing on screen just appears. This is feel, not story: no new words
(every label is an existing `ui/theme.json` text), and none of it touches the simulation or the rewind.

- **Popups** (pause, results, the Codex, settings, the Grudge picker) fade and rise in (0.28 s) and fade out (0.16 s) instead of vanishing (`PanelAnim`, `Overlay.Create`/`Overlay.Close`).
- **Numbers count**: the diamond counter, the shop's purse and the results' stats tick up or down to their total (`NumberTicker`); the results count one row after another.
- **Pickups answer**: a diamond sends a "+N" up from the counter and pops it; a heart refilled pops the heart with a green "+N"; a rewind orb sends "+Ns" from the rewind bar (`HudFloaters`).
- **Weapons**: switching drops the new name into the weapon chip with a punch of size.
- **Bosses are introduced**: when a boss bar shows, a flash lights the screen, the boss's name lands from large, and the bar fills from empty (1.3 s) (`GameplayHud.BossIntroSeconds`).
- **Between floors**: each new floor sweeps a band of lava light up the screen; a floor stage's results close on a black cover with the floor's number, and the next scene opens it (`ScreenTransition`; it never holds the player on black for more than 8 s).
- **Fit**: placed through `MenuCanvas`/`MenuResponsive`, so it holds at 1920x1080 and 1080x1920, inside the safe area (playtest `ui-polish`).

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
  - the exception is a **long bullet-hell floor** (240 to 360 seconds, many embers and shots, five set pieces; Floor 998, the
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

## Teasers

Teasers and trailers (`Marketing/`, made from the game's own art through Hawkes Gen) tell the prologue's edges, never
past it, the same way the website does. What they add to the story:

- **The hut on the main menu is the hero's.** The thatched hut at the left of the menu's meadow, with the neighbours'
  hut beside it, is where he lives. He sleeps in his armour, on a straw bed, his rocket launcher leaning on the wall.
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
