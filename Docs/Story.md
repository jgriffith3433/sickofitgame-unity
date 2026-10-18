# Sick Of It: Story Bible

The story is the design document. Every level, map, enemy, upgrade and line of dialogue should be traceable to
something on this page. If a request needs something the story doesn't explain yet, add it here first, then build
it. Agents: read this before adding or changing a level, an enemy, an upgrade or a voice line.

Status: **draft for review** (2026-10-15; prologue, the Shaft and the floor layout revised 2026-10-18; Waystones and runs 2026-10-18; the Magma King's three-phase fight, Level 11, 2026-10-18; Shortcuts and Circle IX's first stretch, Levels 12-18, 2026-10-18; the slime named Yooba, 2026-10-18; **one level = one floor**, 2026-10-18, see [LevelDesign.md](LevelDesign.md); On the website, 2026-10-18; Teasers, 2026-10-18). Names marked *(working)* are placeholders.

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

## Prologue: how the hero gets into Hell

Level 1 is the Meadow, and it ends in a scripted scene, not a cut. In order:

1. **Video1.** A nice afternoon. The Overlord rises over the hills; his imps drop in to loot the village.
2. **Level 1, the Meadow.** The hero clears the imps and the cinders. Diamonds, a first taste of rewind.
3. **The Grudge pick** (the level's `offerUpgrades`): his first grudge. It happens before the scene, so the scene
   is never interrupted.
4. **The portal.** The Overlord's lasers come down one last time, not at the hero (he isn't worth it yet) but at
   the ground beside him. They burn a glowing hole into the meadow: a portal, lying flat on the ground.
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
- **Today's build** is half way between the layouts (2026-10-18): the first stretch is built one level per floor
  from LevelDesign.md (Floors 1000-991, replacing the old 10-floor Level 2; Floors 995-991 added the same day, ending on
  the first floor boss, the Imp Foreman); after them come the old 10-floor stages (a stage of 10 floors walked to the
  right), described below as they are, replaced floor by floor in turn: the Sack Lift and Floors 960-951 next, then the
  rest of Circle X, Circle IX's stages after them.
- **Level numbers:** Level *n* is position *n* in game.json's `levels` list. Level 1 is the Meadow (Floor 0). Levels
  2-11 are the floors: **Floor 1000** *The Landing*, **Floor 999** *Only 999 To Go*, **Floor 998** *The Spill*, **Floor
  997** *Imp Run*, **Floor 996** *The Slag Falls*, **Floor 995** *The Loop*, **Floor 994** *The Chain Lift*, **Floor
  993** *Ember Ledges*, **Floor 992** *Bad Footing*, **Floor 991** *The Foreman's Pile* (the Imp Foreman, the first
  floor boss); a floor's file is `levels/Floor<floor>.json` (so building the next floor never renames the ones after
  it). Levels 12-20 are the old stages, files `Level3`-`Level11` (Floors 990-901: 990-981
  The Spill, 980-971 Imp Run, 970-961 The Slag Falls, 960-951 The Loop, 950-941 Halfway Basin, 940-931 The Crust,
  930-921 The Sack Pile, 920-911 The Heat Stairs, 910-901 the Magma King); Level 21 is `CrackedRoad` (Floors 900-891),
  Levels 22-27 are `Level13`-`Level18` (Floors 890-831). The old stages keep their file names until they are replaced; the
  floors 990-961 they cover are "the long way" in LevelDesign.md, and their places' names (the Spill, Imp Run, the Slag
  Falls) now belong to Floors 998-996, where those ideas are introduced.
- **The path today** (re-pathed 2026-10-18): New Game → Video1 → Level 1, the Meadow → Video2 → **Floor 1000, The
  Landing** → Video3 (Yooba) → **Floor 999** → **Floor 998, The Spill** → **Floor 997, Imp Run** → **Floor 996, The Slag
  Falls** → **Floor 995, The Loop** → **Floor 994, The Chain Lift** → **Floor 993, Ember Ledges** → **Floor 992, Bad
  Footing** → **Floor 991, the Imp Foreman** → the old stages (the long way's stand-in until the Sack Lift is built): *Floors 990-981: The Spill* → *Floors 980-971: Imp Run* → *Floors 970-961: The Slag Falls* →
  *Floors 960-951: The Loop* → *Floors 950-941: Halfway Basin* → *Floors 940-931: The Crust* → *Floors 930-921: The Sack
  Pile* → *Floors 920-911: The Heat Stairs* (ending at the Magma King's door) → *Floors 910-901: The Slag Throne* (his
  arena; built 2026-10-18) → the Shaft (Circle IX wakes up) → the level select, until Circle IX's stages are rebuilt.
  Inside a Circle the levels run straight on from one to the next (a level's `nextLevelNumber`): results, the Shaft's
  climb, the next level, with no video between them. After them in the list, off the path
  (reachable from the level select only, once unlocked): Circle IX's first stretch, `CrackedRoad` (*Floors 900-891*,
  cut to 10 floors; its beats are still the first layout's, for the Circle IX task to rebuild)
  and `Level13`-`Level18` (*Floors 890-831*, first pass, see *Circle IX stage outline*; they run straight on from the Cracked
  Road, and the Old Stair's shortcut can skip three of them), and after those the old
  arcade levels `ArcadeCrossfire`, `ArcadeSpinCycle`, `ArcadeEverything` and `ArcadeFireRain` (titled "Arcade:
  ...", outside the Circles, their slime enemies swapped for imps and cinders), kept to be recast: their
  enemies and patterns reused inside Circle stages, Fire Rain's Magma King fight as the reference for the Floor 901 arena. Their
  `levelNumber` still names their source footage (`Level2_Background1` is Crossfire's). Video4 (the blue bridge and
  the forest) is off the early path: it is watched from the video select and belongs to Circles VIII-VII.
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

### Acts and story beats

| Act | Where | What happens | Media |
|---|---|---|---|
| Prologue | The Meadow (Level 1) | A nice day. The Overlord rises over the hills; his imps drop in to loot the village; he lasers it; his imps take the diamonds. His lasers burn a portal into the ground beside the hero, who goes to look, shouts down it, hears Hell echo back, says "Nope" and is sucked in. Yooba is slurped in after him. | Video1 → Level1 → portal scene → Video2 → the Shaft |
| Act 1 | Circles X–VIII | The portal drops the hero at Floor 1,000. Yooba turns up (the slime slurped in after him), coughs up a diamond, and starts helping. First Warden. The hero learns Hell repeats, and that he can rewind it. | Video2, Video3 (Yooba) |
| Act 2 | Circles VII–IV | Deeper strangeness: souls stuck in loops (Shades) show what Hell does to people. Hints the Overlord is just as stuck. Midpoint at Floor 500: the hero could take a shortcut home and doesn't. | new |
| Act 3 | Circles III–I | The Overlord's own guard. The gate is almost open; daylight leaks down the stairs. The hero climbs toward it. | new |
| Finale | Floor 1 | The Overlord at his gate. Phase 1: he doesn't take the hero seriously. Phase 2: he rewinds too (the loop was his first). Phase 3: the gate cracks open on the meadow behind him. | new |
| Ending | The Meadow | The hero shoves the Overlord back down and the gate shuts on him. Home, the meadow regrowing, Yooba spitting the diamonds back out. The hero sits down for the first time in 1,000 floors. Something knocks from under the ground. (Post-credit: "Floor 1,001".) | new |

### Circle X stage outline: the Ember Wastes (the old 10-floor stages; Floors 1000-991 are now one level per floor, LevelDesign.md)

**Being replaced.** Circle X's design is now one level per floor: the played route (Floors 1000-991, the Sack Lift,
Floors 960-951, the King's Chimney, Floors 910-901), every floor's page and the floor bosses (the Imp Foreman at 991,
the Pile Keeper at 951) are in [LevelDesign.md](LevelDesign.md). The beats below move onto single floors there (the
Landing → 1000, *ONLY 999 TO GO* → 999, the Spill → 998, Imp Run → 997, the Slag Falls → 996, the Loop → 995, the Sack
Pile → 952, the Heat Stairs → 910-902, the Slag Throne → 901; Halfway Basin's 950 and the Crust go to the long way).
This table is what today's build plays until then.

Ten stages of ten floors. Each one is a place with one beat; the enemy column follows the rules below (one new
enemy at a time, alone; at most 3 on screen in Circle X). Lines are first drafts for the voices.

| Level | Floors | Place | New | The beat |
|---|---|---|---|---|
| 2 | 1000-991 | **The Landing** | `imp` | The lava river where the portal spits everything out. Quiet start; one imp, alone, sees the hero and drops its ember in surprise. Floor 999 milestone: *"ONLY NINE HUNDRED NINETY-NINE TO GO!"* Hero: *"You said that already."* |
| 3 | 990-981 | **The Spill** | `cinder` | Debris from the village that fell through with him: broken fence posts, the baker's cart in pieces, smoking hay, and the village's own sign, *WELCOME TO THE VILLAGE*, standing where he lands. The sparks in it come alive and drift after him: one cinder, alone. Hero: *"That was the baker's cart."* |
| 4 | 980-971 | **Imp Run** | `impSack` (imps with sacks) | A trail of imps hauling the village's diamond sacks down, dropped sacks along the path, a carved *IMPS ONLY*. A sack imp doesn't throw: it runs back and forth with its sack, and every hit tears it and spills a diamond (three hits, three diamonds, two more when it pops): the first stage where diamonds really pour. The first one comes alone. Hero: *"Those are ours."*; at the breather, *"One less sack for his gate."* |
| 5 | 970-961 | **The Slag Falls** | hazard: falling slag | The path along the foot of the lava falls, glowing falls behind and cooling ledges of slag (*MIND THE DRIP*). Slag drops from above, always with a glowing drip a second before (`slagDrip`, then `slag`); the first three drip one by one while he stands still, then they fall through the whole stage. No new enemy: the stage is the falls. Hero: *"It's raining rock. Hot rock."*, then *"The ceiling's melting."* (the slag hazard's warning). It is walked to the right for now; a real climb up the wall (`travelDirection` Up) waits for a bot that can climb ledges. |
| 6 | 960-951 | **The Loop** | (none) | Hell visibly repeats: the same rock and the same lava crack on every floor, a carved *YOU ARE HERE. AGAIN.*, and Floor 957 twice (`floors.repeat`: two signs say 957 and the counter stays on it). At each 957 the announcer calls *"FLOOR 957!"*, the same lava burst goes off in the same crack and the same imp trips on the same spot. After the second: *"It's doing that on purpose."* and *"Hell runs on a loop. So I can drag it back."* The stage that explains why rewind works. Clear: *"THE LOOP: SURVIVED. AGAIN."* |
| 7 | 950-941 | **Halfway Basin** | `cinderThatch` (cinders that split) | Floor 950, halfway to 900, opens the stage: after the title the announcer calls *"FLOOR 950! HALFWAY TO NINE HUNDRED!"* and the hero answers *"Halfway through the first one."* A wide basin under a carved *HALFWAY! (TO 900)*, where the village's burning thatch fell: the first thatch cinder comes alone and bursts into two sparks when shot (*"That was somebody's roof."*). Then the first real mixed pushes of imps, cinders and sack imps; at the breather, *"Fifty floors. Then him."* |
| 8 | 940-931 | **The Crust** | hazard: ember shower | The lava river has a skin of cooling slag (*DON'T STOP*). It holds only while he keeps moving and sinks back into the lava behind him: between fights the screen keeps pushing him on (`travelSpeed`), imps throw from the far bank. More fire from above: the river's steam rains embers, two at a time (`emberShower`), first on its own with its warning, *"Now it's raining fire."* Hero: *"It sinks if I stop. So I don't stop."* |
| 9 | 930-921 | **The Sack Pile** | `impGuard` (the Magma King's guard) | The imps' camp under a carved *KEEP OFF THE PILE*, where they pile the stolen sacks before carrying them up to the throne. The imps get organised: the Magma King's own guard doesn't carry sacks or run; it stands its ground and throws three embers at once. The first guard comes alone (*"This one's not carrying anything. Bad sign."*); later three of them hold the pile in a line, throwing on a count. Clear the line and the pile spills: a heap of diamonds (*"He keeps guards. So he's expecting someone."*). Heaviest imp fight of the Circle. |
| 10 | 920-911 | **The Heat Stairs** | (none) | The approach to the throne under a carved *TO THE THRONE*: the heat rising, the hardest floors of the Circle, embers from above and his guard on the steps. Announcer builds it up (*"FLOOR 920! THE HEAT STAIRS! SOMEBODY'S WAITING AT THE TOP!"*); the hero doesn't (*"Every step's hotter. Of course."*). Twice the stairs rumble and the Magma King's voice comes down them from ahead: he's waiting, he knows the hero is coming, and he isn't getting up (*"I can hear you climbing, little spark."* / hero: *"Was that the stairs?"*; *"Come up, then. I'll stay seated."* / *"He knows I'm coming. Great."*). The stage ends at his door, a slab of obsidian carved *THE SLAG THRONE* (`floors.door`), where the camera stops; through it, one last chuckle (*"Mm-hm-hm. Knock, little spark."*), the announcer's *"THE HEAT STAIRS: CLIMBED! THE MAGMA KING AWAITS!"* and the Grudge pick. Level 11 opens on the other side of that door. |
| 11 | 910-901 | **The Slag Throne** | Warden: `magmaKing` | The Magma King's arena (a locked screen: the one kind of stage allowed to stand still): one hall nine floors high, the throne on a slag mound at Floor 901. Three phases (seated; standing with his imps; enraged, the lava flood you rewind through). Beat him and the counter ticks to 901, the Waystone lights and his name fills in on the Shaft, where Circle IX wakes up. The fight is below (*The Magma King's fight*). Announcer: *"THE MAGMA KING HAS FALLEN!"* Hero: *"He got up for me. I'm touched."* |

Built so far (2026-10-18): Levels 2-10, The Landing to The Heat Stairs, and Level 11, the Slag Throne (the Warden's
arena, below), so Circle X is complete. Levels 2-10 each have their title, lines, carved sign, beats,
generated floors between them, a breather and a Grudge pick at the end. Levels 7-10 climb toward the Warden: thatch
cinders that split, the ember shower from Floor 940, the Magma King's guard from Floor 930, and his voice on the
stairs; the generator's floors get harder the closer they are to 901. The village debris and the Loop's rock are
drawn as the track's plain shapes until their art is made (a cart, the village sign, hay bales, a sack imp with a
full sack: Hawkes Gen, priced, waiting for a GPU budget).

#### The Magma King's fight

In the one-floor layout the arena is **Floor 901, *The Slag Throne***, through the door at the top of the Heat
Stairs (Floor 902), and the fight below stays as it is; the hall is no longer counted as nine floors (LevelDesign.md,
Floor 901). Today's build: the Floor 901 arena is **Level 11, *Floors 910-901: The Slag Throne*** (built 2026-10-18; `ArcadeFireRain` keeps the
older two-phase fight as an off-path reference). Through the door at the top of the Heat Stairs is one hall, nine
floors high: the hero comes in on its floor at **Floor 910**, and the throne sits at the far end on top of a mound of
cooling slag that reaches **Floor 901**, the top of the Wastes. It is the one stage that stands still: a locked screen,
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
   *"He got up for me. I'm touched."* Then the hero walks up the broken throne's slag and the counter ticks the hall's
   floors away, 910 to **901**, and at the top a **Waystone lights** (a standing stone glowing awake): *"WAYSTONE LIT!
   FLOOR NINE HUNDRED ONE!"*, the hero: *"Great. Now Hell starts me here."* From now on a run starts at 901 (see
   *Waystones and runs*). The Grudge pick, the results, then the Shaft.
8. **The Shaft after him.** The marker climbs 911 to 901, the Magma King's figure on the map fills in (beaten), the
   Waystone at 901 lights, and the band above, **Circle IX, the Cracked Road, wakes up**: its painting brightens out of
   the dark from the bottom up, its seams glowing, and the announcer calls it (*"CIRCLE NINE! THE CRACKED ROAD!"*).
   Next Warden: the Spire Fiend. Circle IX's stages are still to be rebuilt in the 10-floor layout, so for now the
   level select follows.

Video3 (Yooba, talking) plays after **The Landing** (Floor 1000, and the Shaft's climb to 999): the slime from the meadow turns up at the bottom of Hell,
pleased to see a familiar face, and coughs up a diamond. From then on Yooba is the shop.

### Circle IX stage outline: the Cracked Road (Levels 12-18, first pass)

Black spires with glowing seams over a road of cooling rock. The road is the only way up, and it keeps splitting: the
**Brimstone Demons** climb out of the cracks to keep him off it, and the spires shed shards from Floor 890. At most 3
on screen, like Circle X (rising later in the Circle). Built 2026-10-18 as a first pass so Hell's first shortcut has
floors to skip: each stage has its title, one or two hero lines, a carved sign, generated floors from `crackedRoad`
with a breather and a Grudge pick. No new enemy after the demon (the Circle's family is still one creature); their
beats are for the Circle IX task to deepen. Levels 19-21 (Floors 830-801, up to the Spire Fiend) are still to come.
In the one-floor layout (LevelDesign.md) Circle IX is recut into floors like Circle X, with a floor boss every 10th
floor and the Spire Fiend on 801; the Old Stair's shortcut stays where it is (Floor 871, out at 841).

| Level | Floors | Place | New | The beat |
|---|---|---|---|---|
| 12 | 900-891 | **The Cracked Road** | `brimstoneDemon` | The first demon climbs out of the road, alone (`levels/CrackedRoad.json`, the first layout's beats). |
| 13 | 890-881 | **The Rift** | (none) | The road cracks wide open under a carved *KEEP TO THE ROAD*; demons come out in twos. Hero: *"The road's coming apart. Can't blame it."* |
| 14 | 880-871 | **The Old Stair** | **Shortcut** | A road past a staircase someone carved into the spires long ago, fallen in. At the top it ends at the stairwell's mouth, a slab carved *STAIRS (CLOSED)* (`floors.door`). Hero: *"Stairs. Fallen in. Still going up, though."* The first shortcut (below). |
| 15 | 870-861 | **Shard Fall** | (none) | The spires shed in earnest under a carved *HARD HATS*. Hero: *"Even the scenery wants me dead."* |
| 16 | 860-851 | **Demon Row** | (none) | Three demons planted in a row across the road under *ONE AT A TIME*. Hero: *"They're queuing now."* |
| 17 | 850-841 | **Halfway Up the Road** | (none) | Floor 850, the Circle's milestone (*"FLOOR 850: HALFWAY UP THE ROAD"*). Hero: *"Halfway. Again."* |
| 18 | 840-831 | **The Stairhead** | (none) | Where the Old Stair comes out: rubble spilling from a crack in a spire, the road and the stair meeting under *MIND THE RUBBLE*. Hero: *"So that's where the stairs come out."* (true whichever way he came). For now the level select follows. |

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
  | Loop Jar | +1 s of Déjà Vu (rewind) | a loop shard one of the damned dropped; he only keeps it in a jar |
  | Shiny Sniffer | diamonds come from farther away | a wobbly bit of Yooba that sniffs out diamonds |
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
| **The Sack Lift** (`sackLift`, designed) | Floor 991's landing, after the Imp Foreman: the imps' sack lift; the Shaft's crack at 975 | diamonds (the imps' fare, paid in the village's own diamonds) | Floors 990-961: out at **Floor 960** | Taking it: *"Paying them with our own diamonds. Lovely."* Announcer: *"HE'S RIDING THE SACK LIFT! THE CROWD IS DISGUSTED!"* (all lines: LevelDesign.md) |
| **The King's Chimney** (`kingsChimney`, designed) | Floor 951's landing, after the Pile Keeper: the Magma King's heat vent; the Shaft's crack at 940 | a harder floor after it (the King heard him come up: Floor 910's lava rises faster) | Floors 950-911: out at **Floor 910** | Taking it: *"Hold your breath. Forever."* Announcer: *"UP THE CHIMNEY?! HE SKIPPED HALFWAY!"* |
| **The Collapsed Stairwell** (`oldStair`) | Floor 871, the top of the Old Stair (`levels/Level14.json`, level 23), behind *STAIRS (CLOSED)*; the Shaft's crack at 870 | a Grudge | Floors 870-841 (levels 24-26): out at **Floor 841**, level 27 next | Hero, at the top: *"Stairs. Fallen in. Still going up, though."* Taking it: *"Fine. One less thing to hate."* Announcer, as he climbs in: *"WHAT'S THIS? A SHORTCUT?! THE CROWD HATES IT!"*, then, as the counter lands: *"FLOOR EIGHT FORTY-ONE! NOBODY TAKES THE STAIRS!"* Hero, coming out: *"That went further than stairs should."* Keeping on: *"Nah. Hell would love that."* |

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
- **The Magma King** (Level 11): the Wastes' groove dies as his throne rises, and **one big hit** lands as he
  appears. The fight starts on his theme: swaggering doom metal and organ, a king who loves an audience. When he
  loses his temper (the **enrage**) it turns frantic: blast beats, a screaming solo, sirens. When he falls, the
  music sinks under the slow motion.
- **The Shaft:** the plunge after Video2 is a **falling riser** that **slams** as the marker hits Floor 1,000; each
  climb gets a **short rising sting** as the marker goes up.

## The systems, in story terms

Mechanics keep working as they do; their names and reasons come from here. Rename UI text (`ui/theme.json`
`texts`) and upgrade titles to match when those systems are next touched.

| System | In the story | Notes for design |
|---|---|---|
| **Rewind** (hold R) | **Déjà Vu.** Hell runs everything on a loop; the hero grabs the loop and pulls it backward. The floor is part of the loop too: a stone that sank comes back up, a ledge that fell is back in place, the lava drops to where it was. | The juice meter is "Déjà Vu". **Rewind orbs** are *loop shards* that drop only from the damned: souls stuck in Hell's loop, repeating a floor forever (in the Wastes, a damned soul on a ledge whose loop hiccups as the hero passes; deeper down, the Shades). Never from imps, cinders or guards. In a **scene** something bigger has hold of the loop (the portal's pull, a Warden's entrance), so he can't grab it: rewind is locked and the button says "Not now". |
| **Death and runs** | Hell doesn't let you die; it sends you back to repeat the floors. "YOU OBVIOUSLY DIED." | A run restarts at the highest **Waystone** reached (the Landing, then the top of each Circle once its Warden is beaten); see *Waystones and runs*. Death videos are Hell's announcer gloating. |
| **Between-level picks** (`content/upgrades`) | **Grudges.** Every floor gives the hero something new to be annoyed about, and spite is fuel. Lost when a run ends. | Titles are grudges: "Spite Rounds", "Short Fuse", "Not Today", "Déjà Vu". |
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
| `damnedSoul` | A damned soul | X (from Floor 999) | A soul stuck in Hell's loop, repeating one small thing on one ledge forever (on 999, counting the same stone). It can't fight or be hurt. When the hero passes close, his loop brushes its loop, it hiccups and a loop shard drops (once per play). The only thing that drops loop shards. | a grey see-through placeholder until `art/damnedSoul.json` is made (GPU, priced) |
| `lavaGeyser`, `geyserVent` | Lava geyser (hazard) | X (from Floor 996, the Slag Falls) | The lava sea under pressure: a basalt vent whose crack glows and spits for a second, then blows a column of lava straight up for a moment. Fires on a fixed beat (Hell's loop), so a rewind shows the order. | `Art/Climb/Vent_*`, `Geyser_column` (made 2026-10-18) |
| `slagDrip`, `slag` | Falling slag (hazard) | X (from Floor 970, the Slag Falls) | Molten rock from the falls. A glowing drip gathers for 1.2 s (the telegraph, with a hiss), then a lump of slag falls from that spot and sets you burning. Not a creature; nothing to kill. | `Fireball`, tinted |
| `lavaBurst` | Lava burst (hazard) | X (the Loop) | A bubble swelling in a crack (1.3 s of glow), then three embers burst up out of it. In the Loop it goes off in the same crack every time. | the beam's charge glow, tinted |
| `cinderThatch`, `cinderSpark` | Thatch cinder | X (from Floor 950, Halfway Basin) | A burning clump of the village's thatch roofs that fell through with everything else and came alive like the other sparks. Bigger and slower than a cinder; shot, it bursts into two sparks (`cinderSpark`) that dart at you for two seconds and go out (they can't be shot, only dodged). | `Fireball`, scaled up and tinted straw-orange, until its own sheet |
| `impGuard` | Imp guard | X (from Floor 930, the Sack Pile) | An imp: the same family (it `extends` `imp`), one the Magma King keeps at his pile and on his stairs instead of sending it up with a sack. It carries no diamonds and doesn't run; it plants itself and throws three embers in a fan, so guards are the first imps that fight together (lines of three on the Sack Pile and the Heat Stairs). It drops nothing: `"drops": []` replaces the imp's drops (no imp drops loop shards; those come only from the damned). The only diamond it gives up is its pay from the pile, now and then (`loot`). | the imp's own art, tinted dark red, until `art/impGuard.json` (an imp in a slag helmet with a spear) is made: a tinted sprite of the same creature, which rule 5 allows |
| `emberShower` | Ember shower (hazard) | X (from Floor 940, the Crust) | Steam off the lava river raining embers two at a time; announced by the hero (*"Now it's raining fire."*) 1.5 s before the first. | `fireball` |
| `magmaKing` | The Magma King | X (Warden, Floor 901) | Rules the Ember Wastes from a throne of slag. Heard (voice `magmaKing`) on the Heat Stairs before he is met. Fights seated (boulders `magmaBoulder`, fist slam → `slamShockwave`) until half health, then stands for the only time and fights standing with his imps (ground-pound, fire breath); at a third he enrages and floods the hall (`lavaFlood`); see *The Magma King's fight*. Drops ten diamonds: his share of the stolen pile. | generated (`art/magmaKing.json`): idle, throw, slamSeat, hurt seated; rise, enrage; idleStand, slam, breath, hurtStand standing; die |
| `magmaBoulder`, `slamShockwave`, `lavaFlood` | Boulder, shockwave, lava flood | X (his arena) | The Magma King's attacks: a lump of molten slag lobbed in an arc that sets you burning, a low shockwave of slag that runs along the floor when he slams (jump it), and the flood (phase 3): a wall of lava too tall to jump that rolls most of the way across the hall (outrun it if you can; rewind if you can't). Each has its warning glow (`mkWarnHand`, `mkWarnFloor`, `mkWarnMouth`, `mkWarnFlood`), 1 s or more ahead. `lavaWave` is the older wave the arcade fight uses. | `Fireball`, scaled and tinted |
| `brimstoneDemon` | Brimstone Demon | IX | Born from the cracks in the Cracked Road: the rock splits and a demon climbs out to stop you climbing. Slow, heavy, plants itself in the road a few steps in front of you and breathes a short burst of fire (`brimstoneBreath`, sets you burning), so keep your distance. Sometimes drops a diamond: the imps' sacks leak on the road, the cracks swallow what falls, and the demons come out with it stuck in their rock. Not damned, so no loop shards. First met alone at the start of Circle IX (`levels/CrackedRoad.json` today; Level 12 in the 10-floor layout). | `Demon1` (exists) until generated; spec ready (`art/brimstoneDemon.json`) |
| `fighter` → Drooler | Drooler | V | Cave beast; Death2 is it eating you. | `Fighter*` (exists) |
| `sentinel` → Lurker | Lurker | V | Crouching cave hunter; Death3. | `Level3Enemy*` (exists) |
| `chaser`, `swarmer`, `exploder`, `sludge`, `slimeGlob`, `slimeSplat` | (retired) | none | Slime enemies. Not in any level once the levels are rebuilt. Keep the files until nothing references them. | slime |
| `gunner`, `drone`, `sprayer`, `spinner`, `brute` | (to recast) | | Generic tinted variants from the arcade version. Each gets a Circle and a reason, or is retired. | tints |

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
   rewinds and playtests repeat. Circle X is `emberWastes`; its stages are Levels 2-10, "Floors 1000-991:
   The Landing" to "Floors 920-911: The Heat Stairs", and Level 11, "Floors 910-901: The Slag Throne" (the Magma
   King's arena: a locked screen whose counter only moves by `climbFloors`). Circle IX is `crackedRoad`; its first
   stage is `CrackedRoad`, "Floors 900-891: The Cracked Road" (off the path until Circle IX is rebuilt).

   *Stage dressing* (2026-10-18, with Levels 3-6): a stage's own scenery on top of its Circle's (`world.props`:
   the Spill's debris, the Loop's same rock), a carved sign where the hero lands (`floors.sign`), floors Hell makes
   him walk twice (`floors.repeat`, the Loop's 957), and stages that lead straight on to the next one through the
   Shaft (`nextLevelNumber`). With Levels 7-10: a door carved with words at the top of a stage, where the camera
   stops (`floors.door`, the Magma King's *THE SLAG THRONE*).

Note: the Landing was recut from the first layout's 25-floor stage to Floors 1000-991 (2026-10-18), and its
cinders moved up to the Spill, where the Story puts them. `CrackedRoad` was cut the same way to Floors
900-891 (Level 12's floors); its beats are still the first layout's until the Circle IX task rebuilds it.

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
   first one is the Old Stair's (Level 14, Floor 871 to 841). Playtest: `shortcut`.

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
