# Sick Of It: Story Bible

The story is the design document. Every level, map, enemy, upgrade and line of dialogue should be traceable to
something on this page. If a request needs something the story doesn't explain yet, add it here first, then build
it. Agents: read this before adding or changing a level, an enemy, an upgrade or a voice line.

Status: **draft for review** (2026-10-15; prologue, the Shaft and the floor layout revised 2026-10-18). Names marked *(working)* are placeholders.

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
| **The Hero** *(name working)* | Thirties, unimpressed, has had enough. Talks to himself in one-liners. Never gives speeches. | `audio.json` voice `player` | Player sprites |
| **The Overlord** | Giant obsidian titan with red eyes. Ruler of Hell's 1,000 floors, as sick of Hell as the hero is of everything, and done waiting to take the surface. Never in a hurry; barely notices the hero until he can't ignore him. Says very little, and every word sounds like it cost him nothing. | `audio.json` voice `overlord`: deep, slow, enormous, barely bothered; a voice that fills a valley without being raised. Short lines, long pauses. When he lasers the portal open he barely notices the hero: *"Mm. ... Stay."* or a bored exhale. | Video1, Level1 backgrounds |
| **Gloob** *(working)* | The village's green slime and its gossip. Swallowed a handful of the village's diamonds to hide them from the imps, got sucked through the portal with the hero, and now trades and tinkers between floors out of his own stomach. **Slimes are villagers, never enemies.** | `audio.json` voice `gloob` *(to add)*: friendly, chatty, wet; talks fast, gurgles between words, never stops for breath, delighted to see anyone. | Video3 (the talking slime) |
| **The Announcer** | Hell's own voice: loves a show, calls out floors, bosses and deaths like an arcade cabinet. | `announcer` | Floor titles, death screens |
| **The Wardens** | One boss per Circle: the monster that rules it. Each guards its Circle's top floor. | Per Warden | Death videos, bosses |
| **The Magma King** | Circle X's Warden: a huge king of slag and obsidian with a lava crown, slouched on a throne of slag at Floor 901. He never gets up: the Wastes come to him. Smug where the Overlord is bored; he hears everything that climbs his stairs and enjoys the wait. | `audio.json` voice `magmaKing`: very deep, thick, gurgling bass; slow, smug, amused, lazy; chuckles (*"Mm-hm-hm."*), never shouts. Heard before he's seen, from ahead on the Heat Stairs. | `art/magmaKing.json` (spec) |

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
   stands at its edge, and once it has had him (and Gloob) it flares and shrinks to a black scorch mark.
5. **The walk-over.** The hero walks over to it on his own, curious (scripted: the player's input is off), and
   leans in to peer down at it, head tilted (his `Curious` clip).
   Hero: *"That's a hole."* Beat. *"So that's where they keep coming from."*
6. **The echo.** He cups a hand and shouts down *"Hello?"* (`Shout`) Hell answers: *"...hello? ...hello? ...hello?"*, each one
   deeper than the last. It's the first hint that **Hell is repetition**: even its echo won't stop.
   The echo is his own voice coming back, slower, darker and more broken each time: the loop answering him
   (`vo_portal_echo`).
7. **"Nope."** He turns to leave. The portal grabs him: yanked off his feet backward, flailing (`Grabbed`), he
   spins and shrinks into it, and he's gone. He is still tumbling the same way down Video2's fire tunnel.
8. **Gloob.** A beat later a green village slime bounces past, wobbles at the edge, and is slurped in after him.
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
- **A level (stage) is 10 floors. A Circle is 10 levels. The 10th level of each Circle is its Warden's arena**,
  Floors x10 to x01 (Circle X: Floors 910-901, the Magma King). So the whole climb is 100 levels plus the Meadow.
- **Level numbers:** Level 1 is the Meadow (Floor 0). Level 2 is Floors 1000-991, *The Landing*; level *n* is
  Floors `1000 - 10(n-2)` to `991 - 10(n-2)`; Level 11 is Floors 910-901, the Magma King; Level 12 starts Circle IX
  at Floor 900. Level *n* is position *n* in game.json's `levels` list, and the file is `levels/Level<n>.json`.
- **The path today** (re-pathed 2026-10-18): New Game → Video1 → Level 1, the Meadow → Video2 → Level 2, *Floors
  1000-991: The Landing* → Video3 (Gloob) → Level 3, *Floors 990-981: The Spill* → Level 4, *Floors 980-971: Imp
  Run* → Level 5, *Floors 970-961: The Slag Falls* → Level 6, *Floors 960-951: The Loop* → Level 7, *Floors 950-941:
  Halfway Basin* → Level 8, *Floors 940-931: The Crust* → Level 9, *Floors 930-921: The Sack Pile* → Level 10, *Floors
  920-911: The Heat Stairs* (ending at the Magma King's door) → the level select, until Level 11, his arena, is built. Inside a Circle the stages run straight on from one to the next (a level's
  `nextLevelNumber`): results, the Shaft's climb, the next stage, with no video between them. After them in the list, off the path
  (reachable from the level select only, once unlocked): `CrackedRoad` (*Floors 900-891*, cut to 10 floors and
  numbered for Level 12; its beats are still the first layout's, for the Circle IX task to rebuild) and the old
  arcade levels `ArcadeCrossfire`, `ArcadeSpinCycle`, `ArcadeEverything` and `ArcadeFireRain` (titled "Arcade:
  ...", outside the Circles, their slime enemies swapped for imps and cinders), kept to be recast: their
  enemies and patterns reused inside Circle stages, Fire Rain's Magma King fight as the reference for the Floor 901 arena. Their
  `levelNumber` still names their source footage (`Level2_Background1` is Crossfire's). Video4 (the blue bridge and
  the forest) is off the early path: it is watched from the video select and belongs to Circles VIII-VII.
- The **floor number is always on screen** (HUD: `FLOOR 973`), and painted into the world as signs, carvings and
  doors, so the player feels how deep they are. Milestones get a beat: 999 ("only nine hundred ninety-nine to
  go"), 950 ("halfway through the Wastes"), 900, 666, 500, 100, 13, 2.
- A **stage** (one level file) is a stretch of 10 floors, not a single screen. The player physically travels through
  it: climbing, walking right, riding something up. Floors tick by as they go (see *Engine* below).
- Pacing target: a Circle takes about 8 to 10 minutes on a good run, the whole climb about 90 minutes. Most
  floors are short stretches inside a stage; Hell's **shortcuts** (elevators, collapsed stairwells, a demon who
  you spared) skip floors at a risk, which is a rogue choice and keeps 1,000 floors reachable.

### The Shaft: Hell's map

Hell is a hole, so its map is a **vertical shaft**: a tall cross-section with the Meadow (FLOOR 0) at the top,
the 10 Circles stacked below it, each in its own biome colour, and FLOOR 1,000 at the bottom.

- **First time, after Video2:** the camera plunges from the Meadow down the whole Shaft, past all 10 Circles, to
  FLOOR 1,000, where the hero's marker lands. Announcer: *"WELCOME TO THE BOTTOM. NINE HUNDRED NINETY-NINE TO GO."*
  Hero: *"...Great."*
- **After every level:** the Shaft comes back and the hero's marker climbs to the floor he reached (Level 2 ends
  at Floor 991, so the marker rises from 1,000 to 991). It is how the player feels the climb: a tiny dot, a very
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
- **Shortcuts are hinted, not explained:** cracks in the shaft wall and dotted gaps between floors that the player
  can't read yet. They become Hell's shortcuts (see *Engine*, Shortcuts) once that system is built.
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
| Prologue | The Meadow (Level 1) | A nice day. The Overlord rises over the hills; his imps drop in to loot the village; he lasers it; his imps take the diamonds. His lasers burn a portal into the ground beside the hero, who goes to look, shouts down it, hears Hell echo back, says "Nope" and is sucked in. Gloob is slurped in after him. | Video1 → Level1 → portal scene → Video2 → the Shaft |
| Act 1 | Circles X–VIII | The portal drops the hero at Floor 1,000. Gloob turns up (the slime slurped in after him), coughs up a diamond, and starts helping. First Warden. The hero learns Hell repeats, and that he can rewind it. | Video2, Video3 (Gloob) |
| Act 2 | Circles VII–IV | Deeper strangeness: souls stuck in loops (Shades) show what Hell does to people. Hints the Overlord is just as stuck. Midpoint at Floor 500: the hero could take a shortcut home and doesn't. | new |
| Act 3 | Circles III–I | The Overlord's own guard. The gate is almost open; daylight leaks down the stairs. The hero climbs toward it. | new |
| Finale | Floor 1 | The Overlord at his gate. Phase 1: he doesn't take the hero seriously. Phase 2: he rewinds too (the loop was his first). Phase 3: the gate cracks open on the meadow behind him. | new |
| Ending | The Meadow | The hero shoves the Overlord back down and the gate shuts on him. Home, the meadow regrowing, Gloob spitting the diamonds back out. The hero sits down for the first time in 1,000 floors. Something knocks from under the ground. (Post-credit: "Floor 1,001".) | new |

### Circle X stage outline: the Ember Wastes (Levels 2-11)

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
| 11 | 910-901 | **The Slag Throne** | Warden: `magmaKing` | The Magma King's arena (a locked screen: the one kind of stage allowed to stand still). Beat him and his name fills in on the Shaft; Floor 901 is the Circle's Waystone. Announcer: *"THE MAGMA KING HAS FALLEN!"* Hero: *"He was sitting down the whole time."* |

Built so far (2026-10-18): Levels 2-10, The Landing to The Heat Stairs, each with its title, lines, carved sign, beats,
generated floors between them, a breather and a Grudge pick at the end. Levels 7-10 climb toward the Warden: thatch
cinders that split, the ember shower from Floor 940, the Magma King's guard from Floor 930, and his voice on the
stairs; the generator's floors get harder the closer they are to 901. The village debris and the Loop's rock are
drawn as the track's plain shapes until their art is made (a cart, the village sign, hay bales, a sack imp with a
full sack: Hawkes Gen, priced, waiting for a GPU budget).

Video3 (Gloob, talking) plays after **The Landing**: the slime from the meadow turns up at the bottom of Hell,
pleased to see a familiar face, and coughs up a diamond. From then on Gloob is the shop.

## The systems, in story terms

Mechanics keep working as they do; their names and reasons come from here. Rename UI text (`ui/theme.json`
`texts`) and upgrade titles to match when those systems are next touched.

| System | In the story | Notes for design |
|---|---|---|
| **Rewind** (hold R) | **Déjà Vu.** Hell runs everything on a loop; the hero grabs the loop and pulls it backward. | The juice meter is "Déjà Vu". **Rewind orbs** are *loop shards* that drop from the damned (Shades, imps). In a **scene** something bigger has hold of the loop (the portal's pull, a Warden's entrance), so he can't grab it: rewind is locked and the button says "Not now". |
| **Death and runs** | Hell doesn't let you die; it sends you back to repeat the floors. "YOU OBVIOUSLY DIED." | A run restarts at the last **Waystone** (top of each Circle once its Warden is beaten). Death videos are Hell's announcer gloating. |
| **Between-level picks** (`content/upgrades`) | **Grudges.** Every floor gives the hero something new to be annoyed about, and spite is fuel. Lost when a run ends. | Titles are grudges: "Spite Rounds", "Short Fuse", "Not Today", "Déjà Vu". |
| **Diamonds and the shop** | The village's diamonds, the portal fuel the imps stole; enemies drop them back. **Gloob** works them into the hero's gear between floors. Permanent. | Shop = Gloob. Shop upgrade names are things a village slime can make. |
| **Floor counter and progress bar** | How far up you are. | Progress bar = floors left in the stage; markers = Warden, shortcut, milestone. |

## Rules for enemies

1. **Every enemy has a reason to be on its floor**: who it works for, what it wants, why it's in the hero's way.
   Write the reason in the entity's design note (and in this table) before giving it stats.
2. **One Circle, one family.** An enemy belongs to the Circle whose biome it fits; at most it shows up again in
   the next Circle up, promoted (tougher, recolored, renamed).
3. **Introduce one new enemy at a time, alone**, with room to read it before it's mixed with others.
4. **Slimes are friends.** Gloob is a slime; the village's slimes are villagers. No slime enemies.
5. The look comes from the art pipeline (see *Art*), never from tinting a sprite of a different creature.
   Placeholder tints are fine while art is being made, and the table here says which art is pending.

### Roster

| Entity id | Name | Circle | Why it's there | Art |
|---|---|---|---|---|
| `imp` | Imp | Meadow, X | The Overlord's raiders. They loot the village's diamonds, then guard the way down in the Wastes. Throw slow embers. Die like Hell's own fireworks: a shriek, eyes bulging, then a pop of soot and embers with the horns and the (empty) sack dropping out, because they are made of the Wastes' fire. | generated (`art/imp.json`, Docs/ArtPipeline.md) |
| `cinder` | Cinder | Meadow, X | A spark from the burning village that came alive and followed the hero through the portal. Drifts toward you and pops. | `Fireball` (exists); spec ready (`art/cinder.json`) |
| `impSack` | Sack imp | X (from Floor 980, Imp Run) | An imp hauling one of the village's diamond sacks down to the Overlord's gate. Too busy carrying to throw anything: it runs back and forth with the sack, and every hit tears the sack and spills a diamond. Diamonds from a sack: rewards that make sense. | the imp's art, tinted warm until its own sheet (an imp with a full sack) is made |
| `slagDrip`, `slag` | Falling slag (hazard) | X (from Floor 970, the Slag Falls) | Molten rock from the falls. A glowing drip gathers for 1.2 s (the telegraph, with a hiss), then a lump of slag falls from that spot and sets you burning. Not a creature; nothing to kill. | `Fireball`, tinted |
| `lavaBurst` | Lava burst (hazard) | X (the Loop) | A bubble swelling in a crack (1.3 s of glow), then three embers burst up out of it. In the Loop it goes off in the same crack every time. | the beam's charge glow, tinted |
| `cinderThatch`, `cinderSpark` | Thatch cinder | X (from Floor 950, Halfway Basin) | A burning clump of the village's thatch roofs that fell through with everything else and came alive like the other sparks. Bigger and slower than a cinder; shot, it bursts into two sparks (`cinderSpark`) that dart at you for two seconds and go out (they can't be shot, only dodged). | `Fireball`, scaled up and tinted straw-orange, until its own sheet |
| `impGuard` | Imp guard | X (from Floor 930, the Sack Pile) | The Magma King's own guard: imps he keeps at his throne instead of sending them up with sacks. They don't carry or run; they plant themselves and throw three embers in a fan, so they're the first imps that fight together (lines of three on the Sack Pile and the Heat Stairs). Their pay is in the pile: a diamond now and then. | the imp's art, tinted dark red until its own sheet (an imp in a slag helmet with a spear) is made |
| `emberShower` | Ember shower (hazard) | X (from Floor 940, the Crust) | Steam off the lava river raining embers two at a time; announced by the hero (*"Now it's raining fire."*) 1.5 s before the first. | `fireball` |
| `magmaKing` | The Magma King | X (Warden, Floor 901) | Rules the Ember Wastes from a throne of slag. Heard (voice `magmaKing`) on the Heat Stairs before he is met. | spec ready (`art/magmaKing.json`); run the art pipeline |
| `brimstoneDemon` | Brimstone Demon | IX | Born from the cracks in the Cracked Road: the rock splits and a demon climbs out to stop you climbing. Slow, heavy, plants itself in the road a few steps in front of you and breathes a short burst of fire (`brimstoneBreath`, sets you burning), so keep your distance. Sometimes drops a diamond: the imps' sacks leak on the road, the cracks swallow what falls, and the demons come out with it stuck in their rock. Not damned, so no loop shards. First met alone at the start of Circle IX (`levels/CrackedRoad.json` today; Level 12 in the 10-floor layout). | `Demon1` (exists) until generated; spec ready (`art/brimstoneDemon.json`) |
| `fighter` → Drooler | Drooler | V | Cave beast; Death2 is it eating you. | `Fighter*` (exists) |
| `sentinel` → Lurker | Lurker | V | Crouching cave hunter; Death3. | `Level3Enemy*` (exists) |
| `chaser`, `swarmer`, `exploder`, `sludge`, `slimeGlob`, `slimeSplat` | (retired) | none | Slime enemies. Not in any level once the levels are rebuilt. Keep the files until nothing references them. | slime |
| `gunner`, `drone`, `sprayer`, `spinner`, `brute` | (to recast) | | Generic tinted variants from the arcade version. Each gets a Circle and a reason, or is retired. | tints |

## Rules for levels

- **A level is a place in the story**, named after the place. `displayName` is the place ("The Meadow",
  "Floors 1000–991: The Landing").
- **No work or office jokes** anywhere in the game: titles, lines, upgrades, results quips.
- **It goes somewhere.** The player moves through a space and the floor counter moves. A single locked screen is
  only for boss arenas.
- **Pacing (arcade, but fair):**
  - the first 5 seconds of a level are quiet: title, one line, room to move;
  - a new enemy type arrives alone, at most one new type per stage;
  - no more than 3 enemies on screen in Circle X, rising slowly per Circle;
  - every hazard is telegraphed at least 1 second ahead;
  - a stage has a breather (a few seconds with nothing attacking) between each push;
  - target stage length 60 to 120 seconds; bosses separate.
- **Story beats are voiced**: one title line from the announcer, one or two lines from the hero, a beat at the
  end. Lines are written for the character's voice (see `audio.json` voices) and are short.
- **Rewards make sense**: diamonds drop from things that were carrying them; loop shards from the damned.

## Engine work this story needs

Built (2026-10-15; recipe: GameplayCookbook "A stage that climbs through Hell's floors"):

1. **Floor counter**: script counter `floor` (`floors.start`/`end` in the level script), HUD readout `FLOOR 973`,
   floor signs in the world, `setFloor`/`climbFloors` actions, `floorReached` trigger, and Circle `milestones`
   (lines that play when the counter reaches a floor).
2. **Travelling stages**: `world.camera: "Travel"` (Right walks, Up climbs): forward-only camera, ground that runs
   along, platforms and parallax props streamed in ahead and removed behind, backgrounds keyed by `Time`,
   `Distance` or `Floors` (`sequenceBy`) and able to play part of a clip (`clipStart`/`clipLength`).
3. **Floor generator**: `content/circles/<id>.json` (backdrop, track, props, enemy table, hazards, easiest/hardest
   difficulty, milestones) and the `generateFloors` action that fills floors between story beats, seeded, so
   rewinds and playtests repeat. Circle X is `emberWastes`; its stages so far are Levels 2-10, "Floors 1000-991:
   The Landing" to "Floors 920-911: The Heat Stairs". Circle IX is `crackedRoad`; its first stage is `CrackedRoad`,
   "Floors 900-891: The Cracked Road" (off the path for now: the Magma King's arena, Floors 910-901, is still to
   build).

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
   shrink), Gloob bounces past and is slurped in. Rewind is off during the scene. The hero gets new animation
   clips for it (look down, shout, turn, spin into the portal).
   *Built so far* (2026-10-18; recipe: GameplayCookbook "A scene with the hero"): the scene actions a level script
   uses for it: walk the hero somewhere by himself, turn him, play one of his clips, pull him into a point
   (spinning and shrinking until he is gone), move and zoom the camera, fade to black, and lock rewind while it
   plays. `content/scenes/cutsceneTest.json` runs every one of them on the Meadow for the playtest.
   *The scene itself* (2026-10-18): Level1.json's last phases, `portalBeam` → `portalLook` → `portalGloob` →
   `portalClose`. After the Grudge pick: input and rewind lock, the hero can't be hurt (`sceneUntouchable`) and
   the music drops. The Overlord's beam is telegraphed by a red glow on the ground 2.5 units from the hero, on
   the side toward the middle of the meadow, for 1.25 s, then strikes (flash, shake, a column of beam, *"Mm. ...
   Stay."*) and the ground burns open into the `portal` (humming, drawn behind the hero). The camera closes in a
   little, he walks to its edge, the lines and the echo play, a "?" pops over him, *"Nope."*, he turns and takes
   one step, *"Oh, come ON-"* and he is pulled in (1.2 s, 2.5 turns). A beat later `gloobProp` (Gloob: the
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
7. **Voices**: `overlord` and `gloob` in `audio.json` `voices`, and the prologue, Shaft and Circle X lines.

Not built yet:

8. **Waystones and runs**: save the highest Waystone reached; New Run starts there; Grudges reset, diamonds and
   Gloob's upgrades persist.
9. **Shortcuts**: a choice point that skips floors for a cost or a risk.

## Art

Port the Dose Of Destruction pipeline (`C:\Projects\doseofdestructionwebsite`, see its `docs/comfyui.md`,
`docs/animation.md`, `docs/maps.md`):

- **Characters:** one still per enemy → side-view poses (Qwen-Image-Edit on the ComfyUI GPU server) → short
  clips between poses (Wan 2.2 first-last-frame) → background keyed (BiRefNet / white key) → frames aligned on the
  feet → sprite sheet → the entity's `visual.animator`. Clip list per enemy kind: idle, move, attack, hurt, die.
- **Maps:** each Circle gets a painted backdrop and props (Qwen, two passes) plus the existing video loops.
- **Agent-first:** a request like "make the Lurker" should be one config entry (description, poses, clips) and a
  command that queues the GPU jobs, builds sheets and writes the entity's animator, with a preview to check.
