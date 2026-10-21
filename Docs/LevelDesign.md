# Sick Of It: Level Design

How a floor of Hell is built. [Story.md](Story.md) says what Hell is and who lives where; this page says how a floor
plays, and agents build floors from it. Every floor here must fit the story; if a floor needs something the story
doesn't explain, add it to Story.md first.

Status: **design for review** (2026-10-18, J's decision m_daf635ad1287: one level = one floor, a floor boss every 10th
floor, the Warden on the Circle's top floor, shortcuts skip stretches; revised the same day for J's *"some scrolling
horizontally ... as immersive as you can"*: every floor's path, set piece, surprise, rewind moment and feel). The
floor engine is built (see *Building it*); **the whole played route is built** (2026-10-18/19): Floors 1000-991 (levels
2-11), the Sack Lift, Floors 960-951 (levels 12-21), the King's Chimney, Floors 910-902 (levels 22-30) and Floor 901
(level 31), each `levels/Floor<floor>.json`. **Circle IX's played route is built too** (2026-10-19, section 6): Floors 900-891 (levels
32-41), the Spared Demon, Floors 860-851 (levels 42-51), the Hollow Spire and Floors 810-802 (levels 52-60), up to
the Spire Fiend's arena entrance. The long way isn't built as floors yet: turning a shortcut down plays the
old 10-floor stages over those floors as its stand-in (Story.md, *Level numbers*). Names marked *(working)* are placeholders. Lines are first drafts for the voices.

## The rules in one breath

- **One level is one floor.** 60-120 seconds, always going up: climbs joined by sideways runs (a raft across a lava
  river, a chase ahead of a lava wave), never a flat walk to the right from start to end.
- **A floor boss every 10th floor** (the floors ending in 1: 991, 981, ... 911), **the Warden on the Circle's top
  floor** (901, 801, ... 101).
- **Hell's shortcuts skip stretches** of floors at a cost or a risk, so 1,000 floors stay playable in hours.
- **One new idea per floor**: one new obstacle *or* one new monster, never both. Everything else on the floor is
  something the player already knows.
- **Every floor is a place** with a name, a set piece and a story beat, its number carved into its landing.
- **Every floor turns.** Its path mixes climbs with sideways runs, raft rides and chases, and it has a set piece, a
  surprise and its own feel (shake, slow motion, the lava's roar, the announcer): see *Set pieces, surprises and the
  feel*.

## 1. The climb grammar

### How a floor reads

Every floor is the same sentence with different words, so the player always knows where they are in it:

1. **The ledge.** The floor opens on a ledge of black rock just above the **lava sea**, the glowing ocean at the
   bottom of every floor of the Wastes. The camera is low; the lava fills the bottom of the screen. The announcer
   calls the floor (*"FLOOR NINE HUNDRED NINETY-EIGHT! THE SPILL!"*), the hero says one line, and nothing attacks
   for the first **5 seconds** (title, line, room to move).
2. **The climb.** About **6 to 10 screens up**, along the floor's **path** (see *A floor's path* below): sections that
   go up, sideways (left or right), diagonally, or hold still for a fight, with smooth turns between them. The camera
   only goes forward along the path (it follows the hero with a look-ahead and never back; whatever drops below the
   screen's bottom edge is gone, into the lava). The way on is the floor's obstacles: stones, rafts, lifts, chains,
   updrafts, ledges. The background is the world falling away below: the lava sea far below with its glow and haze,
   spires standing in it, smoke, islands of rock floating in the dark, embers drifting up, and the next floor's light
   far above.
3. **Pushes and breathers.** The climb is cut into 2 to 4 **pushes** (the lava rises, monsters arrive, the hazards
   speed up) with a **breather** of a few seconds between each (a solid ledge, nothing attacking, maybe a hero line).
   The last push is the hardest.
4. **The landing.** The floor ends on a wide, solid landing of carved stone with **the floor number cut into it**
   (`998`, big, glowing faintly) and sometimes a word or two the damned carved there (*KEEP MOVING*). Standing on the
   landing is safe: the lava stops rising below it.
5. **The way on.** A door or a stair at the back of the landing leads to the next floor. The hero walks into it, the
   announcer calls the floor done, the Grudge pick (on floors that give one, see *Rewards*) and the Shaft, where the
   marker climbs one floor.

### The pace

- **Quiet first 5 seconds**, always.
- **60 to 120 seconds** on a good run; floor bosses 90 to 180; the Warden separate.
- **Pushes and breathers**: 2 to 4 pushes, a breather of 3+ seconds between each.
- **At most 3 enemies on screen in Circle X**, rising slowly per Circle (4 in IX-VIII, 5 in VII-V, 6 above).
- **Every hazard telegraphed at least 1 second ahead**, on the spot it will hit (a glow, a drip, a rumble, a crack).
- **A new monster arrives alone**, in a quiet stretch, with room to read it before anything else joins.
- **The lava is the clock.** On floors where it rises it is slow and telegraphed (a rumble, a line of bubbles, the
  announcer), and it never catches a hero who keeps moving. It pushes; it doesn't ambush.
- **Always somewhere to stand.** No blind jumps: the next foothold is on screen before the hero needs it.

### Rewards

- **Diamonds** come from things carrying them: sack imps, sack swings, counterweight sacks, the floor bosses' piles.
- **Loop shards** (rewind orbs) come only from **the damned**: souls stuck in Hell's loop, repeating one floor
  forever. Never from imps (the Overlord's raiders), cinders (sparks of the village) or guards. In the Wastes a damned
  soul *(working: `damnedSoul`)* sits on a ledge doing the same small thing over and over: climbing the same three
  steps, scraping the same patch of slag, counting the same stones. It doesn't fight and can't be hurt. When the hero
  passes close by, his own loop brushes theirs, the soul's loop hiccups and a shard drops onto the ledge (once per
  play, never re-paid by a rewind). They sit on breather ledges and landings, about one or two per floor, so the
  quiet stretches are where Déjà Vu refills. The soul goes straight back to what it was doing.
- **Grudges** are picked on the landing of every **fifth floor** and after every floor boss (so about two per
  stretch of ten), not on every floor: with a floor every 90 seconds, a pick per floor would bury the climb in menus.

### The rewind loop

Hell is repetition, so Hell's hazards repeat, and rewind (Déjà Vu) is the hero's answer:

- **Some hazards loop.** A geyser row that fires in the same order, the same stones sinking in the same pattern, the
  same imp tripping on the same spot. The pattern is exact, so a player who rewinds sees it again and knows it.
- **Rewind is the answer, not a punishment.** A floor that is hard on the first try (a lava rise faster than it looks,
  a raft that tips) is fair on the second, because the loop is the same. Floors are seeded: a rewind replays the same
  future (AGENTS.md).
- **Some floors teach it.** The Loop (995) makes the hero notice; later floors (953, 906) assume it.
- **Juice is limited**, so rewind is a save, not a crutch: the loop shards the damned souls drop on the ledges refill it.

### Set pieces, surprises and the feel

J, 2026-10-18: *"Make sure there's some scrolling horizontally, etc. Be creative here! Make the gameplay and levels as
immersive as you can."* So every floor page in section 4 has, besides its one new idea:

- **A path** that mixes going up with going sideways. Written as its sections in order, with the rough length in
  screens:

  | Mark | Section | What it is | In the level file |
  |---|---|---|---|
  | `↑` | climb | straight up: a wall, a shaft, a lava fall, a chimney | `Up` |
  | `→` `←` | run | sideways along a shelf, a bridge, the foot of the falls | `Right` / `Left` |
  | `↗` `↖` | slope | up and across: a slope of floes, a stair | `UpRight` / `UpLeft` |
  | `≈` | raft ride | sideways on something the current carries; the camera keeps the current's pace | `Right`/`Left` with `autoSpeed`, `debrisRaft` |
  | `»` | chase | sideways (or up a stair) with something at his heels: the lava wave, a bridge falling away, a stair collapsing; the camera moves on by itself and stopping means a burn | `autoSpeed` + `lavaSurge` / crumbling stones |
  | `■` | arena moment | the camera holds on one screen until a short fight or a puzzle is done, then moves on | `Arena` + `nextSection` |

  No floor is a single direction. Every floor of the route has at least one sideways section, and from Floor 999 on
  most have a ride or a chase; a floor's turns are where its set piece happens.
- **A set piece:** the one moment a player tells someone about (*"the cart ride"*, *"the bridge fell behind me"*).
  It is built from the floor's new idea or from known pieces used big.
- **A surprise:** something the player didn't see coming, which is **never a new rule**. It is a known thing doing
  something unexpected (a stone that comes back as the village sign, a guard counting wrong, the door knocking back),
  a story glimpse, or a joke. A surprise that hurts is still telegraphed (1 s, on the spot); the pacing rules above
  hold through it.
- **A rewind moment** where the floor has one: a spot where Déjà Vu is the natural answer (see *Rewind as a tool*).
- **The feel:** what shakes, what slows, what the lava sounds like (see *The feel* below).

Rides aren't a new idea. Standing on something the current carries, or a camera that moves on by itself, asks
nothing new of the player; what a ride *does* to him (tipping, sinking, crumbling) is the idea, and that is
introduced on its own floor. So rides and chases start early, while the toolkit grows one piece a floor.

#### Rewind as a tool

Hell is a loop, so the floors build moments where dragging it back is the clever move, not the failure:

- **Bring it back.** A stone that sank, a ledge that crumbled, a bridge span that fell, a lift that left: all come
  back with a rewind (the `climb` playtest's sunk stone). Floors put a needed foothold where it is easy to lose
  (992's bridge, 995's sinking run).
- **See it again.** Loops (995, 953, 906) are exact: a rewind shows the same geysers in the same order, so the gap
  that was invisible at speed is plain the second time.
- **Ride it again.** A missed chain, a raft that tipped him, a cart jump he fluffed: rewind to the beat before and
  take it again, on the same rhythm (997, 959, 908).
- **Undo the hit.** Classic: the ember he didn't see. Fine, but no floor *needs* it.

The first hot bath (Floor 1000) shows the *Hold R* hint (if hints are on), and the stone comes back up with him on
it: the climb's first lesson is that the floor rewinds too.

#### The feel

All presentation (AGENTS.md: it reads the simulation, never changes it), and all through the floor's script or the
pieces themselves, so every floor page only says *when*:

- **Screen shake** (`cameraShake`, scaled by the player's Shake setting): **small** (0.05, 0.2 s) a stone sinking
  or a ledge cracking under him, an ember hitting rock near him; **medium** (0.12, 0.4 s) an **eruption** within a
  screen (a geyser, a lava fall pouring on, the lava starting to rise, a lift dropping a notch); **big** (0.25,
  0.8 s) the lava wave starting, a bridge or stair collapsing, a boss's slam, a pile bursting. A rumble before the
  wave or the King's voice is a long, low shake (0.06, 1.5 s). Never more than one big shake every few seconds.
- **Slow motion on near misses** (`slowMotion` 0.35 for 0.4 s, real time): an ember, a geyser column, a slag lump
  or the wave passing within a hair of him without a hit, or a jump off a stone or a span in its last instant before
  it goes. A heartbeat thump, the music ducks, the roar drops to a muffled rush, then everything snaps back. At most
  once every 8 s, never in the first 5 s of a floor, never while rewinding. Set pieces may also call a planned slow
  beat (the apex of the chimney toss on 957, the cart's jump on 908).
- **The lava's roar:** a looping bed under every floor of the Wastes that follows the lava. Its loudness and
  brightness rise as the lava's surface gets closer to him (the near sea rising, a wave behind him, a raft ride right
  on it) and fall away as he climbs above it; on a breather it settles to a low, far boil. Every sink is a hiss,
  every pour a gurgle, panned to where it is. On the floor's last push the roar is at its loudest.
- **The announcer calls every floor:** its number and name as the floor opens (*"FLOOR NINE NINETY-SIX! THE SLAG
  FALLS!"*, a banner and a voice), at most one call during the floor (the lava, a chase's *"RUN!"*, a boss), and a
  short call on the landing where the page has one. The floor counter on the HUD ticks as he steps onto the landing.
- **The music** is the Ember Wastes loop; a push, a chase or a ride under fire switches to its push music, a breather
  drops back (Story.md, *Music*).

#### Glimpses, glints and the damned

Three things run through every floor of the Wastes, so the climb feels like one journey:

- **The King's glow.** Far above, through the haze of the backdrop, a red light that is always straight up: his
  throne. It grows floor by floor and is never explained until it is (he is *heard* before he is seen, Story.md; the
  glimpses show the throne, never him):

  | Floors | What he sees |
  |---|---|
  | 1000-991 | a red speck at the top of the sky, easy to miss |
  | 960-955 | a glow with a shape in it, like a chair (960: the first time the hero says so) |
  | 954-951 | the glow pulses when a guard falls (he's watching) |
  | 910-904 | the throne's silhouette on its mound of slag, smoke rolling off it |
  | 903 | the throne fills the top of the sky; the light on the stairs is its light |
  | 902 | the door hides it; only its glow leaks round the edges |
- **Glints.** The imps' sacks leak. On most floors one or two of the village's diamonds glint in a nook just off the
  path, where an imp dropped them (a `diamond` placed on a ledge you leave the path to reach, a chain to ride out to,
  a raft to wait for). They are a choice, never on the way, never behind a blind jump. Yooba is never in a stage, but
  he can smell his village's diamonds: on the Shaft he tells the hero when he walked past one (Story.md, *Yooba
  between floors*), and his Shiny Sniffer makes them drift over from farther.
- **The damned.** One or two damned souls per floor sit on breather ledges and landings doing their one thing
  forever, and drop a loop shard when he passes (see *Rewards*). The first is a surprise on Floor 999.

## 2. The toolkit

Every floor of the Wastes is built from these pieces. Each one is introduced on one floor (the floor in the last
column) and reused after that. *Engine* says whether the game can do it today; everything new is a rewind-safe
platform or hazard type (saved in `SaveState`/`LoadState`, seeded) for the floor-building task.

| Piece | What it does | Why it's in Hell | Telegraph | First floor | Engine |
|---|---|---|---|---|---|
| **Sinking stones** | Slabs of cooled slag floating on the lava. Stand on one and it sinks (about 1.5 s to go under); step off and it bobs back up. Keep moving. | The lava sea's crust, broken into floes | The stone dips and hisses the moment he lands | 1000 | yes (`sinkingStone`, ability `sink`) |
| **Rising lava** | The lava sea climbs the screen, slowly, behind the hero: the push upward. Stops below each breather ledge and the landing. | The Wastes breathing in | A rumble, a line of bubbles across the screen, 2 s ahead; the announcer the first time | 999 | yes (`lavaSea`: counters `lavaSpeed`, `lavaMax`) |
| **Bobbing debris rafts** | Bigger floating things (the village's carts, roofs, fences) bob on their own rhythm and drift sideways. They don't sink when stood on; they tip if he stands at the edge. | Everything that fell through the portal ends up in the lava | They rock before they tip | 998 (as sinking debris), 959 (bobbing and drifting) | yes (`debrisRaft`, ability `raft`: drifts on a current and carries him) |
| **Imp sack-swings** | Imps swinging across the gap on chains, carrying the village's diamond sacks. Hit the sack: diamonds. Touch the imp: a burn. Their swing is a rhythm he can ride over or shoot through. | The imps haul the diamonds up through the Wastes to the gate | The chain rattles before each swing | 997 | yes (`impSwing`: a sack imp with the `swingChain` pendulum, which nobody can grab) |
| **Swinging chains** | The imps' empty chains hang over the gaps and swing. Jump into one to grab it, swing with it, jump off with its speed. | The hauling route's chains, left swinging | It swings in plain sight, the same rhythm every time | 997 (one idea with the sack-swings: *the hauling chains*, some with an imp on, some empty for him; the imp is known from the Meadow) | yes (`swingChain`) |
| **Lava geysers** | A vent in the rock blows a column of lava straight up. | The lava sea under pressure | The vent glows and spits for **1 s** before | 996 | yes (`geyserVent` + `lavaGeyser`: a `lavaFall` beat blowing upward, the vent's tell on its gurgle) |
| **Slag flows** | A crust of slag sliding along on the lava like a conveyor: with it he runs faster, against it he barely moves. | The lava sea's current, crusted over | Its glowing seams crawl the way it moves | 908 (the cart run) | yes (`slagConveyor`, ability `conveyor`) |
| **Hell's loop** | A set of stones, geysers or imps that does exactly the same thing over and over. Not a new object: a new way to use the old ones. | Hell is repetition | The first repeat is called out; each stone trembles and steams 1 s before it goes | 995 | yes (`sink` `loopPeriod`/`loopPhase`: stones that sink by themselves on the level's clock, so every lap is the same; `geyser` `worldClock`) |
| **Chain lifts** | A platform on a chain that rides up and down a shaft on a timer. | The imps' lifts for hauling sacks | The chain clanks and the lift jolts before it moves | 994 | yes (`chainLift`; `startOnStand` waits for him; `cutterTag`: imps near it hack the chain and it drops a notch every few seconds until they're shot) |
| **Crumbling stones** | Ledges of old slag that crack when stood on and fall after 1 s. They don't come back (except by rewind). | The Wastes' old floors, burned through | Cracks and dust the moment he lands | 992 | yes (`crumblingStone`, ability `crumble`) |
| **Counterweights** | Two platforms on one chain over a pulley, one loaded with a diamond sack. Shoot the sack off and its side rises (with him on it). | The imps balance their lifts with the stolen sacks | The loaded side creaks, the sack sways | 960 | partly (`counterweightLeft`/`Right`: his weight sinks one side and lifts the other; the sack is new) |
| **Heat updrafts** | Shimmering columns of hot air over the lava that carry him up while he's in them (and keep carrying a jump). | The lava sea's heat | Shimmer and floating ash show the column | 957 | yes (`heatUpdraft`) |
| **Falling slag** | Lumps of molten rock dropping from the ceiling. | The floor above is melting | A glowing drip gathers for 1.2 s | 956 | yes (`slagDrip`, `slag`) |
| **Lava falls** | Curtains of lava pouring down the wall. They pour, thin to a trickle and stop on a beat, then pour again: pass while they're off. | The floor above spills over | The fall thins and sputters 1 s before it stops, and gurgles 1 s before it pours | 955 | yes (`lavaFall`) |
| **The lava wave** | A wall of lava rolls after him sideways, never far behind the screen's edge: a run where stopping means a burn. It sinks away when the path turns up. | The lava sea surging | A rumble, the screen shakes, the announcer's *"RUN!"*, 1.5 s before it moves | 999 (the rising lava's last push turns sideways: the same lava, the same telegraph, so not a second idea); the big ones on 951 and 905 | yes (`lavaSurge`, ability `surge`, with a section's `autoSpeed`) |
| **Ember shower** | Steam off the lava sea raining embers two at a time. | The lava sea's steam | The hero's warning line, 1.5 s before the first | 909 | yes (`emberShower`) |
| **The collapsing bridge** | A span of crumbling stones across a lava river that falls away stone by stone a beat behind him, the camera running on: a chase made of the floor itself. | The Wastes' old floors, burned through | Each stone cracks the moment he lands, the far end of the span already dusting | 992 (the crumbling stones' set piece) | config (crumbling stones in a `»` section with `autoSpeed`) |
| **The cart** | The village's burning hay cart riding a slag flow down a chute: he rides in it, jumps out of it over the gaps and back in. | It fell through on Floor 998 and has been burning on the current ever since | The chute's seams glow the way it goes; a gap's lip glows 1 s ahead | 908 | partly (a `debrisRaft` carried by a `slagConveyor`; the cart's look and its jump off the chute's end are new) |
| **The dark** | A stretch where the only light is the lava's glow below, the hazards' own telegraph glows, cinders and his shots (each shot lights the way it flies). | The haze and smoke under the throne, thick enough to swallow the light | Every hazard still glows 1 s ahead: in the dark the telegraph *is* the light | 907 | no (a darkness overlay with light from the lava, glows, cinders and shots) |
| **Cooling crusts** | When a lava fall stops, its face cools into crusts of slag he can climb, up the inside of the fall; when it pours again they melt. Climb on the off beat. | The falls' own skin, the way the river crusts over | The crusts darken and crack 1 s before the fall pours again | 903 | no (crust footholds on a `lavaFall`, solid while it is off) |
| **The King's glow** | The throne far above in the backdrop, growing floor by floor (*Glimpses, glints and the damned*). Scenery, not an obstacle. | Every floor of the Wastes is under his throne | none needed | 1000 (a speck) | no (a backdrop layer sized and lit by the floor number) |

Monsters the floors draw from (Circle X's one family: the Overlord's imps, and the sparks of the burning village):

| Monster | What it does | Why it's here | First floor |
|---|---|---|---|
| `impSack` (on a swing) | Swings across with a diamond sack; hit the sack, diamonds spill | Hauling the village's diamonds up to the gate | 997 |
| `cinder` | Drifts toward him and pops | A spark of the burning village that fell through and came alive | 998 |
| `imp` | Perches on a ledge above and throws slow embers down | The Overlord's raiders, guarding the way up | 993 |
| `cinderThatch` | A burning clump of thatch; shot, it bursts into two darting sparks | The village's roofs fell through too | 958 |
| `impGuard` | Plants itself on a ledge and throws three embers in a fan; drops nothing | The Magma King's own guard, keeping his pile and his stairs | 954 |

### A floor's path

A floor is a sequence of **sections** (`world.path` in its level file): each one goes **Up**, **Right**, **Left**,
**UpRight** or **UpLeft** for some distance, or is an **Arena** (the camera holds until the fight on that tier is
over). The camera follows the hero along the current section with a look-ahead, forward only, and rounds off the turn
into the next; a section can also move on by itself (`autoSpeed`: a raft ride, a chase). Each section puts its stones,
rafts, chains and hazards on the floor a section ahead of the hero, so the next foothold is always there before he
needs it. A good floor mixes them: climb a lava fall, cross a river on a raft, run ahead of a wave, climb again.

- **The lava sea** is always below: the near sea (`lavaSea`, the one that burns and rises) under the camera, the far
  sea, spires, smoke and floating islands drawn behind (the Circle's `sky`), dropping away as he climbs.
- **Footholds are one-way.** Every stone, ledge, raft, lift and counterweight (`body.oneWay`) can be jumped up through
  from below (he lands on top) and dropped through by holding down for a moment (S, the stick or the move touch pulled well down, close to straight
  down, for player.json `dropThroughHoldSeconds`: a brush of down or a run with the stick a little low never drops him); the
  landing, the slag flows, the ground and the walls stay solid. So a foothold straight over another is a way up, and
  one straight under the piece he stands on is a way down (the bot drops for it). The `one-way` playtest shows both.
- **Lava is a hot bath, not a pit.** Touching lava burns (one hit) and throws him up out of it (`scald`), so a lost step
  costs health and time, never the whole floor.
- **Rewind restores the floor.** Every piece is rewound with everything else: a stone that sank, a ledge that fell, a
  lift that left, the lava that rose all come back. Pieces that loop (lifts, falls, chains, a raft's current) keep the
  same beat every time, so a rewind replays the same future.
- **The landing** is the last foothold: `floorLanding`, the floor number carved into it (`carved`, `{floor}`) and a word
  or two under it; standing on it plays the arrive beat (`playerOn` → lines → `completeLevel`).
- **Generated floors** (the long way): a section with `generate: N` is filled with N chunks from the Circle's
  `chunks` (each a section with its pieces, anchored to the section's start), picked by weight and `fromFloor`, seeded
  by the level seed and the floor, so the same floor is always the same floor.

**The reach rules** (the hero: moveSpeed 2, jumpForce 9, gravity 1.8, so a jump peaks 2.29 up and carries ~2 sideways at
the same height): keep each rise to the next foothold at most **1.9** (the bot's limit; 1.3 is the usual step, less off a
sinking stone, which drops while he walks), keep a same-height gap to about 2.2, keep every foothold inside the screen
while its section is on (an Up section's camera stays on its start x, ±4.98), and give every section a foothold where hero
+ `travelLead` reaches its end, or the camera never moves on. A chain he must wait for hangs off a foothold that holds
(not a crumbling or sinking one). `python3 Tools/Levels/floor_check.py [FloorN ...]` checks all of this without Unity
(jumps, the bot's chain swings, lifts at their worst cut, the screen per section, section ends, lava boxes over footholds,
stones that sink out of view). **An Up section's camera never comes back down**: it chases his jump's apex + `lead`, and
his body is only 0.6 tall, so at the floor's 1.2 a sinking stone he stands on goes under the screen's bottom before it
has sunk its 1.5 and PitCatch burns him there and throws him 15 up, past the next footholds (Floor 1000, beta). Give a
climb with sinking stones `"lead": 0.4` (its end still needs a foothold where hero + 0.4 reaches it). Don't hang a
one-way ledge straight over a stone with less than the jump's 2.29 between the stone's top and the ledge's top: a jump
from the stone rises into the ledge and falls back short of its top (Floor 1000's rest ledge over stone2; the bot now
keeps steering for the foothold it jumped for, judged from the take-off (`PlaytestBot.Climb`: no new foothold over the
jump's top, the stone it left out until it lands), but a player under it gets the same dead hop), and never hang the solid
landing (`floorLanding`, 6 wide, not one-way) over a lower foothold of the climb where its underside blocks the jump to the
next step (`Floor 998`: `floorLanding998` at y 7.3..7.9 hung over `wreckSign` at y 5.7 and blocked the jump to `spireFoot` at
y 7.05 while the camera's look-ahead had already switched to the sideways landing section; `floor_check.py` flags it, and the
bot climbs a remaining step above it when the camera rounds into a sideways section early). A lava river's box starts
past the foothold he jumps onto it from, never under the climb before it (Floor 1000's river now starts at the bank's
right edge, x 4.8: before, its untextured box hung over the slope's top-right and under the bank).
Two stones less than the hero's width (0.6) apart sink to the same height and catch him across the gap, his middle over
nothing: player.json `groundCheckWidth` is 0.3 (it was 0.1) so standing on a corner or across a gap still counts as ground
and he can jump off it (Floor 1000: wedged between stone2 and a sunk stone3, no jump for 190 s), and the bot steps on to
the stone it rests on when it isn't grounded. Thrown up by a geyser to just over a stone's top beside it (Floor 907: gapVent0 under the ledge's
left end throws him 1.4, to the corner of the crumbling stone at (14.5, 4.15)), his circle catches on the corner in the air, pushed into it and held
by friction, not grounded: the bot lets go the other way after 0.3 s without moving (`PlaytestBot.Wedged`), drops off and
climbs again (the floor sat there 160 s). A player holding into it hangs the same way until he lets go.
A lift or a counterweight level with the ledge he's on is a fine first step of a climb (956, 904, 993): the bot, which
otherwise only jumps for what is higher, steps on to a lift waiting at its bottom or the low side of a counterweight
when nothing higher is in reach. It goes for the carved landing whenever it is within a short jump (before, a ledge under
the landing won and the bot pushed into the landing's side all level: 954, 902); pinned against the landing's solid side in the air, `Wedged` lets go (above).
On the landing it stands still while the floor's ending plays: the last section mostly runs sideways, and the landing rule's
banner and wait leave him free to move, so walking on took him off the landing's far end into the pit (993, 998, 860). With
nothing in reach on a run it waits up to 4 s at the edge of what it stands on, while the camera eases into the section's
end (seconds after he gets there) and the next section's footholds start to count (997: off the far ledge before the wall).
An updraft throws him **0.7 over its column's top** (lift 5): put the foothold it carries him to at most ~0.45 above the
top and within ~1.5 of its side, and the column's foot no higher than a step above the foothold he enters from. A
counterweight he stands on is going down (and its partner up) as he jumps. Keep a timed hazard (a geyser, a fall) off the
foothold he jumps from on a climb: he can wait beside it for the beat, not on it. An Arena holds the camera on the path
point where it starts: everything the fight needs (enemies included) goes inside that screen, spawned `onEnter` if it
would shoot during the climb up to it (`floor_check.py` flags an arena enemy off that screen). A wave along the floor that
he must jump is at most **0.8 tall and 1.4 wide**: a jump keeps his feet 0.8 up for only ~0.8 s, so a bigger one at
speed 3.6-4.4 can't be cleared. A warning glow covers all the floor its attack reaches.

The test floor `content/scenes/climbTest.json` (the `climb` playtest) has one of everything: a climb of sinking and
crumbling stones, a raft ride across a lava river with a chain to swing from and an updraft under it, a chase on a slag
flow ahead of the lava wave, a climb on a chain lift with the lava rising behind, and the landing.

### The art of the climb

Everything above the lava is painted in the Meadow's look (bold ink outlines, hand-painted comic fantasy) in Video2's
lava-river palette: black basalt and charcoal slag, molten orange-yellow lava with white-hot seams, dull red smoke. The
pieces are the Wastes' own and the village's wreckage, so the climb keeps telling the story: everything that fell
through the portal ends up floating on the lava.

| Piece | What it looks like | Moves |
|---|---|---|
| **The world below** (the Circle's `sky`) | The lava sea far below, seen from high up, its currents churning; smoke and heat haze over it; black spires standing in it; floating islands of rock; the black ceiling of the Wastes glowing red at the top of the screen. All of it repeats sideways and up, so a floor can climb for two minutes without a seam. | The sea churns and drifts (a loop); smoke drifts both ways; everything moves slower than the platforms, the farther the slower |
| **The lava** (`lavaSea`, `lavaRiver`) | Its surface seen side-on: a bright wavy crest with bubbles and black crust bits, deepening to dark red under it | The crest rolls along; it flares while a rise is telegraphed |
| **Lava falls** (`lavaFall`) | A curtain of molten streams | Pours down; thins and fades on its beat, gurgles back |
| **Geysers** (`lavaGeyser`) | A low basalt mound with a glowing crack; the column blasting out of it | The crack glows and spits for 1 s (the tell), then the column (`geyser` `columnArt`) |
| **The lava wave** (`lavaSurge`) | A towering wall of lava seen side-on, its bright rolling crest on the front, churning orange to dark red behind it | Laid down the wave's body whatever its size (`surge` `art`), turned to face its run; `artAhead` lines the crest up with the edge that burns |
| **Heat updrafts** (`heatUpdraft`, `heatUpdraftTall`) | A faint column of gold heat ripples with embers and ash, fading at its sides (light on black, keyed by brightness, tinted half see-through) | Laid up the column and scrolling upward (`updraft` `art`), the drawn ash still rising over it |
| **Slag flows** (`slagConveyor`, `slagConveyorDown`) | A crust of cracked charcoal slag plates with glowing seams and lava dripping off its underside, its top flat | Fitted to the body's length (`conveyor` `art`), its top on the body's top; the drawn seams crawl over it the way it moves |
| **Stones**, three sizes | Small: brittle old slag full of fine cracks (`crumblingStone`). Medium: a floe of cooled slag, its underside glowing where it sits in the lava (`sinkingStone`). Large: a heavy basalt ledge with a hanging point (`ledge`, breathers) | Medium steams and its cracks blaze while it sinks; small cracks, breaks into chunks and falls |
| **The village's wreckage** (`debrisRaft` and its kin) | The hay cart on its side (`debrisRaft`), a slab of thatched roof (`debrisRaftRoof`), the WELCOME TO sign, half burned (`debrisRaftSign`), a hay bale burning at one end (`debrisRaftHay`) | Bob, drift and tip, each on its own rhythm |
| **The imps' ironwork** | A chain lift: an iron grate floor on four chains meeting at a ring (`chainLift`); a counterweight: planks bound in iron with a pan under them (`counterweightLeft`/`Right`); heavy black chain links; the ring and hook at a swing chain's end (`swingChain`) | Chains are laid link by link from the pulley to the piece |
| **The landing** (`floorLanding`) | Carved basalt blocks with a blank sunken panel on the front | The floor number is carved into the panel in ember orange, glowing faintly (`carved`, `onFace`) |

Every walkable piece has a flat, readable top edge, and its art is fitted so that edge is exactly the top of its body.
The art lives in `Assets/Art/Climb/`; how it was made (the Hawkes Gen takes, the keying, the fitting) is
`Tools/Art/climb/climb.json` and `Tools/Art/climb_art.py`.

## 3. The route through Circle X

About **30 played floors** in Circle X, in three stretches of ten, joined by two shortcuts. Each stretch ends on its
floor boss (or the Warden); each shortcut is the way on from a boss floor.

```
FLOOR 1000-991   THE LANDING STRETCH       10 floors played          991: the Imp Foreman
      │  SHORTCUT 1: THE SACK LIFT         (the imps' lift, 991 → 960; the Shaft's crack at 975)
      │  skips 990-961 (the long way: floor bosses 981, 971, 961)
FLOOR 960-951    THE DEPOT STRETCH         10 floors played          951: the Pile Keeper
      │  SHORTCUT 2: THE KING'S CHIMNEY    (the heat vent, 951 → 910; the Shaft's crack at 940)
      │  skips 950-911 (the long way: Halfway at 950, floor bosses 941, 931, 921, 911)
FLOOR 910-901    THE HEAT STAIRS           9 floors played + the Warden   901: THE MAGMA KING (Waystone)
```

On the Shaft every played floor climbs the marker one floor; a shortcut climbs it to the floor where he took it, then
jumps a dotted line through the crack it hinted at (975, 940: both already on the map, `ui/shaft.json`
`shortcutHints`) and lands where it let him out. The skipped floors stay dark on the map until he walks them.

**The long way.** Every shortcut is a choice (Story.md, *Shortcuts*): keep climbing and the player plays the skipped
floors. Those are floors too (one level each, 60-120 s), but they are built by the floor generator from the Circle's
toolkit (`content/circles/emberWastes.json`: the pieces this page has introduced by then, harder the closer to 901)
instead of hand-built pages, with a floor boss on every 10th (see *Floor bosses*) and the milestones on their floors
(950: *"FLOOR NINE FIFTY! HALFWAY TO NINE HUNDRED!"*). The long way is slower and richer (more diamonds, more
Grudges); the shortcut is faster and costs something. A first run should feel the pull of both.

### The shortcuts

| Shortcut | Where | Cost | Skips | Lines |
|---|---|---|---|---|
| **The Sack Lift** (`sackLift`) | Floor 991's landing, after the Imp Foreman: his lift, a cage of chains that hauls sacks up through the Wastes, *IMPS ONLY* painted on it | **Diamonds** (the imps' fare: *FARE: 20 DIAMONDS*, paid in the village's own diamonds). Without enough, the card is stamped *CAN'T AFFORD THE FARE* | Floors 990-961: out at **Floor 960**, the Depot | Hero, at the lift: *"Imps only. I'll squeeze."* Taking it: *"Paying them with our own diamonds. Lovely."* Announcer: *"HE'S RIDING THE SACK LIFT! THE CROWD IS DISGUSTED!"*, then as it lands: *"FLOOR NINE SIXTY! NO STAIRS, NO SHAME!"* Keeping on: *"I'll walk. I've got legs and spite."* |
| **The King's Chimney** (`kingsChimney`) | Floor 951's landing, after the Pile Keeper: a vent in the wall roaring with the Magma King's heat, straight up | **A harder floor after it** (risk): the chimney lets him out on the Heat Stairs, and the King heard him come up it: on Floor 910 the lava rises faster and the King says so | Floors 950-911: out at **Floor 910** | Hero, at the vent: *"It's warm. It goes up. Good enough."* Taking it: *"Hold your breath. Forever."* Announcer: *"UP THE CHIMNEY?! HE SKIPPED HALFWAY!"*, then: *"FLOOR NINE TEN! STRAIGHT TO THE STAIRS!"* Keeping on: *"No. Halfway's coming. I want to see it."* |

(Costs: the Old Stair costs a Grudge, the Sack Lift diamonds, the Chimney a harder floor: one cost each, all three
kinds from Story.md. The diamond fare needs `offerShortcut` to take a diamond cost, and Floor 910 needs to know the
Chimney was taken: see *Building it*.)

## 4. The floors

Each page: the place, the one new idea, the **path** (marks in *Set pieces, surprises and the feel*; numbers are
screens), the obstacles (known pieces), the monsters and why, the **set piece** and story beat, the **surprise**, the
**rewind** moment, the **feel** (shake, slow motion, sound), the lines (announcer = Hell's show, uppercase; hero =
deadpan, short), and the rough length.

---

### Stretch 1: the Landing stretch (Floors 1000-991)

Where the portal spits out everything that falls through: the lava sea, the village's wreckage floating on it, and the
imps hauling the stolen diamonds up out of it. The stretch teaches the toolkit one piece a floor and ends on the imp
who runs the hauling.

#### Floor 1000: The Landing

- **New:** sinking stones.
- **Path:** `↗ 3` a slope of floating crust → `≈ 2` a big floe drifting down the lava river (the current carries him,
  slowly) → `↑ 3` the bank's ledges → `→ 1` the landing.
- **Obstacles:** sinking stones up the slope and along the drift's edges, then solid ledges; nothing else. The river
  starts under the bank ledge, never under a stone of the slope: lava only takes a stone that sinks into it.
  Its two climbs (the slope, the bank) keep the camera close (`lead` 0.4, not the floor's 1.2): a sinking stone and a
  missed hop stay on screen, so the hot bath is one he sees, never a burn from under the screen that throws him past the slope.
- **Monsters:** none to fight. At the landing an imp with a sack sees him, drops its ember in surprise (it hisses into
  the lava) and flees up a chain. It is the first imp he meets and it tells him where the diamonds went.
- **Set piece and beat:** he lands where Video2 dropped him, on a rock in the lava river. The first stone he stands on
  sinks under him: keep moving. The slope of floes is long and gentle, a lesson with no punishment but a hot bath. The
  drift is the first time Hell carries him: the river turns, the far sea opens below, spires and floating islands in the
  haze. Landing carved *1000* and *WELCOME*.
- **Surprise:** the fleeing imp's sack catches on the chain and a diamond drops out of it; it glints on the landing's
  edge (the first glint: the village's own). And straight up, if he looks, a red speck at the top of the sky.
- **Rewind:** the first hot bath shows *Hold R*; the stone comes back up with him on it.
- **Feel:** no pushes, no push music. A small shake as each stone takes his weight; no slow motion (nothing to miss).
  The roar is low and wide, the whole sea, with a hiss on every sink; it drops away on the bank.
- **Lines:** Announcer: *"FLOOR ONE THOUSAND! THE LANDING!"* Hero (first stone sinks): *"Even the floor wants me
  gone."* At the imp: *"Hey. That's ours."*
- **Length:** ~60 s.

#### Floor 999: Only 999 To Go

- **New:** rising lava (and its sideways form, the wave: the same lava, the same telegraph).
- **Path:** `↑ 2` ledges → `→ 2` a shelf along the wall → `↑ 3` the climb with the lava rising behind → `» 2` the shelf
  runs out and the lava surges sideways after him, slowly → `↖ 1` up to the landing.
- **Obstacles:** sinking stones and solid ledges, the lava rising behind him for the first time, then a short, slow
  wave.
- **Monsters:** none: the lava is the enemy on this floor.
- **Set piece and beat:** the milestone. Halfway up, the lava rumbles, bubbles run across the screen, and it starts to
  climb. It is slow; it stops under each breather ledge. At the top of the climb it spills over a lip and comes along
  the shelf after him: his first chase, gentle, ending as the path turns up. The landing carved *999* has *ONLY*
  scratched in front of it.
- **Surprise:** on the breather ledge sits a grey, see-through figure counting the same stone over and over (*"nine
  hundred ninety-nine, nine hundred ninety-nine..."*, a whisper): the first damned soul. As he passes, its loop
  hiccups and a loop shard drops. It goes straight back to counting.
- **Rewind:** if the lava gets him, a rewind pulls it back down the wall with him.
- **Feel:** the rise starts with a medium shake and the long low rumble; the roar climbs with the lava, loudest on the
  chase. Slow motion the first time he leaves a stone just as the lava closes over it. Push music from the rise to the
  landing.
- **Lines:** Announcer (title): *"FLOOR NINE HUNDRED NINETY-NINE! ONLY NINE HUNDRED NINETY-NINE TO GO!"* Hero:
  *"You said that already."* Lava starts: announcer *"THE LAVA'S RISING! CLIMB, LITTLE MAN!"*; hero: *"It's
  following me. Of course."*
- **Length:** ~75 s.

#### Floor 998: The Spill

- **New:** the cinder.
- **Path:** `≈ 3` along the lava river among the village's wreckage (hop from wreck to wreck as the current takes
  them) → `↑ 3` up a jam of wreckage piled against a spire → `↗ 2` the spire's slope → the landing.
- **Obstacles:** the village's wreckage floating on the lava, behaving like sinking stones (fence posts, cart planks,
  hay bales); slow rising lava in the last push.
- **Monsters:** cinders, the sparks of the burning village that fell through with him. The first comes alone out of
  the burning hay cart halfway up and drifts after him; later two at a time.
- **Set piece and beat:** his own village floating past. The village sign, *WELCOME TO THE VILLAGE*, sticks out of the
  lava where he lands; the river carries him through a procession of fences and planks; a burning hay cart drifts by
  on the current, and the sparks in it come alive. Landing carved *998* on a half-sunk cart wheel.
- **Surprise:** the burning hay cart isn't scenery: it's where the first cinder comes from. And as he leaves the river,
  the village sign tips over and sinks out of sight (it comes back on 959).
- **Rewind:** a plank that went under too soon: rewind and step off a beat earlier.
- **Feel:** a small shake on each wreck he lands on; slow motion on the first cinder that passes within a hair; the
  roar is right under him on the river (loud, close), with the hay cart's crackle drifting past.
- **Lines:** Announcer: *"FLOOR NINE HUNDRED NINETY-EIGHT! THE SPILL!"* Hero (at the sign): *"That was the baker's
  cart."* First cinder: *"The hay's following me now."*
- **Length:** ~80 s.

#### Floor 997: Imp Run

- **New:** the hauling chains: imps swinging across the gaps with the village's sacks, and empty chains swinging
  beside them that he can ride.
- **Path:** `↑ 2` ledges up to the gap → `→ 3` across the gap by chain (empty chains between the imps' chains) →
  `↑ 2` a breather wall → `← 2` back across a higher gap, chain to chain → the landing.
- **Obstacles:** a tall gap crossed by chains; sinking stones and ledges between the swings.
- **Monsters:** sack imps on swings, hauling the village's diamond sacks across the gap to the lift on the far side.
  Each hit tears the sack and spills a diamond; they don't throw (too busy carrying). The first swings alone, slowly;
  then two in rhythm.
- **Set piece and beat:** the imps' hauling route, and the hero swinging on it: he rides an empty chain across while
  imps swing past on the chains either side, sacks and all. Dropped sacks on the ledges, a carved *IMPS ONLY*, and
  diamonds raining into the lava when he hits a sack. The first floor where diamonds really pour.
- **Surprise:** on the higher gap, the empty chain he is reaching for and an imp's chain swing in on the same beat:
  they pass face to face in midair. The imp shrieks and drops a diamond; it glints on the far ledge.
- **Rewind:** a missed chain: rewind to the beat before and take it again; the swing is the same rhythm every time.
- **Feel:** chain rattles panned to each chain; slow motion as he passes an imp in midair; no shake (the floor is
  air); the roar far below, rising whenever he drops toward it.
- **Lines:** Announcer: *"FLOOR NINE NINETY-SEVEN! IMP RUN!"* Hero: *"Those are ours."* At the breather: *"One less
  sack for his gate."*
- **Length:** ~85 s.

#### Floor 996: The Slag Falls

- **New:** lava geysers.
- **Path:** `↑ 2` ledges at the foot of the falls → `→ 3` a run along a ledge between the vents, the falls behind →
  `↑ 2` up beside the falls → `↗ 1` the landing.
- **Obstacles:** ledges along the foot of the great lava falls (scenery behind, they don't hurt here), geyser vents
  between the ledges, sinking stones over the pools.
- **Monsters:** one sack swing near the top, for the diamonds; otherwise none, the geysers are the floor.
- **Set piece and beat:** the run beside the falls, the whole screen orange, the vents going off in a row behind him
  as he passes. The first vent glows for a full second and blows a column right in front of him with nothing else
  around. Landing carved *996* and *MIND THE SPOUT*.
- **Surprise:** the last vent on the run coughs something up with its column: a diamond sack the imps lost in the
  lava. It bursts on the ledge in a spray of diamonds.
- **Rewind:** the vents fire in the same order every time: a rewind shows the order and the run becomes a rhythm.
- **Feel:** a medium shake for every eruption within a screen; slow motion when a column misses him by a hair; the
  roar swells as the falls get close and the vents gurgle a second ahead of each blow.
- **Lines:** Announcer: *"FLOOR NINE NINETY-SIX! THE SLAG FALLS!"* Hero (first geyser): *"The floor sneezed."*
- **Length:** ~80 s.

#### Floor 995: The Loop

- **New:** Hell's loop (the same pattern, again and again).
- **Path:** `→ 3` a run of sinking stones over a lava pool → `→ 3` the same run again: the same stones, the same
  geyser, the same carving (the floor visibly repeats; the camera goes on and Hell has pasted it in twice) → `↑ 3` up
  the wall → the landing.
- **Obstacles:** a run of sinking stones that sink by themselves in the same order, over and over, with a geyser at the
  same spot each time; the lava rises behind.
- **Monsters:** one cinder, drifting in at the same moment every lap.
- **Set piece and beat:** the floor that explains rewind. The pattern is unfair the first time (the stones sink a beat
  before he expects) and easy once he's seen it, and the floor practically asks him to drag it back. The key stone
  sinks under him at the run's far end: hold R and it comes back up under his feet. Carved on the rock, three times,
  the same words: *YOU ARE HERE. AGAIN.* Landing carved *995*.
- **Surprise:** under the third carving, someone scratched *(ME TOO)*: a damned soul is doing the same run beside him,
  the same three stones, forever. It drops its shard as he passes and goes round again.
- **Rewind:** the floor's whole point: the sunk stone comes back, and the loop replays exactly.
- **Feel:** a whoosh like a skipped record each time the run repeats; a medium shake on the geyser; slow motion as he
  leaves the key stone on its last instant. The roar holds steady (it's on a loop too).
- **Lines:** Announcer: *"FLOOR NINE NINETY-FIVE! THE LOOP!"* Hero (second lap): *"It's doing that on purpose."*,
  then *"Hell runs on a loop. So I can drag it back."* Landing, announcer: *"THE LOOP: SURVIVED. AGAIN."*
- **Length:** ~90 s.

#### Floor 994: Chain Lift

- **New:** chain lifts.
- **Path:** `→ 2` a ledge run into the shaft → `↑ 3` the first lift → `→ 1` across to the next shaft on a chain (known
  from 997) → `↑ 3` the second lift → the landing.
- **Obstacles:** two lifts up a sheer shaft, with sinking stones at the bottom and ledges between the lifts.
- **Monsters:** imps at the pulleys above, hacking at the chains with their claws to drop the lift (and him) into the
  lava: shoot them and the lift keeps going. They don't throw: the chain is their target. One at the first lift, two
  at the second.
- **Set piece and beat:** a long ride up a shaft of hanging chains and swinging sacks, the lava far below; the chain
  sparks where the imps cut it, the lift jolts and drops a notch when they get a cut in. Landing carved *994*.
- **Surprise:** the second lift doesn't stop at the landing: it keeps going up into the dark of the ceiling. He has to
  step off as it passes (the landing's lip glows); behind him it grinds into the rock and jams.
- **Rewind:** an imp cut the chain and he dropped: rewind and shoot that imp first.
- **Feel:** a medium shake and a slow-motion beat when a lift drops a notch; the roar recedes as he rises and the
  chains clank close; push music on the second lift.
- **Lines:** Announcer: *"FLOOR NINE NINETY-FOUR! THE CHAIN LIFT!"* Hero (first jolt): *"Don't. Don't do that."*
- **Length:** ~90 s.

#### Floor 993: Ember Ledges

- **New:** the imp that throws (plain `imp`).
- **Path:** `↑ 2` sinking stones and ledges → `↗ 2` a slope of ledges → `■` the hollows: the camera holds on the imps'
  ledge until its two imps are down → `↑ 2` a chain lift → `→ 1` the landing.
- **Obstacles:** sinking stones, a chain lift, solid ledges: everything known.
- **Monsters:** imps perched on ledges above, the Overlord's raiders guarding the way up, throwing slow embers down at
  him. The first alone on a high ledge; then two, one each side, in the hollows; never three while the lava rises.
- **Set piece and beat:** the first imps that fight back. A ledge of their sleeping hollows and a heap of embers they
  throw from. Landing carved *993* with *GO BACK* scratched under it. The imps' lift drops into the shaft with a clank
  once the hollows are clear (it isn't there during the fight, so it can't carry him off the held screen).
- **Surprise:** a third imp is asleep in the last hollow. It wakes as he lands beside it, startles, and falls off its
  own ledge into the lava.
- **Rewind:** an ember he didn't see: rewind and read the throw.
- **Feel:** slow motion on the first ember that passes close; a small shake when embers hit the rock beside him; the
  hold's push music cuts to the bare loop when the hollows are clear.
- **Lines:** Announcer: *"FLOOR NINE NINETY-THREE! EMBER LEDGES!"* Hero (first throw): *"Oh, this one throws."* The
  sleeper: *"One less."*
- **Length:** ~85 s.

#### Floor 992: Bad Footing

- **New:** crumbling stones.
- **Path:** `↑ 3` crumbling ledges up the wall → `» 3` **the collapsing bridge**: a span of crumbling stones across a
  lava river, falling away stone by stone behind him while the camera runs on → `↑ 2` the far wall → the landing.
- **Obstacles:** crumbling ledges up the wall, sinking stones at the bottom, rising lava in the second push.
- **Monsters:** a cinder pair in the first push, an imp throwing from the top.
- **Set piece and beat:** the old floor of the Wastes, burned through: the ledges crack under him and fall into the
  lava behind, so the way down is gone the moment he uses it. Then the bridge: every stone cracks as he lands on it and
  drops a beat later, the span falling into the river behind him all the way across. The floor before the boss: the
  hardest so far. Landing carved *992* with a crack through it.
- **Surprise:** the bridge's last span is already gone. An imp's chain hangs over the gap: swing it. The bridge's end, a
  heavy basalt ledge, is the one stone on it that holds, so he can wait there for the swing to come back.
- **Rewind:** a stone he needed fell before he got there: rewind and the span is whole again.
- **Feel:** a small shake on every crumble, a big one as the middle of the bridge goes; slow motion when he leaves the
  last stone as it falls; the roar rises under the bridge as the span drops into it.
- **Lines:** Announcer: *"FLOOR NINE NINETY-TWO! BAD FOOTING!"* Hero (first crumble): *"Nothing down here holds."* At
  the gap: *"Of course."*
- **Length:** ~90 s.

#### Floor 991: The Foreman's Pile (floor boss)

- **New:** the floor boss, **the Imp Foreman** (see *Floor bosses*).
- **Path:** `■` tier 1, the loading floor → `» 2` the lava takes tier 1 and he runs along the loading gantry → `↑ 2`
  a chain lift → `■` tier 2 → `↑ 1` → `■` tier 3, the lift's landing.
- **Obstacles:** a climbing arena of three tiers over the lava: sinking stones, a chain lift between tiers, the
  gantry between tiers 1 and 2.
- **Monsters:** the Foreman and the sack imps he whips along (at most two).
- **Set piece and beat:** the top of the hauling route, where the imps load the sacks into their lift. The Foreman
  sits on the biggest sack of all. Beat him and his sack bursts: a shower of diamonds. His lift stands open on the
  landing: **the Sack Lift** (shortcut 1). Landing carved *991*. (As built, the lift is offered where he falls: see below.)
- **Surprise:** on tier 3, losing, he spreads his wings and tries to fly. They're for show: he flaps, rises a hand's
  height, and drops back onto his sack.
- **Rewind:** his swing is a loop (the sack-swing, boss-sized): a rewind shows its passes.
- **As built (2026-10-18, J: "two phases with telegraphed attacks"):** two phases on two tiers. Tier 1 (the loading
  floor, a locked screen): he whips the tier (a row of glows on the floor, each cracking 1.1 s later where it glowed) and
  whistles sack imps onto the swings (two at most). At half health the lava takes tier 1 (a big shake), he hauls himself
  up out of sight and the camera runs along the gantry (`»`, the lava wave behind) to a chain lift up to tier 2 (a
  locked screen), where he swings on the lift's chain, dropping embers from his sack (a glow on the spot, the ember 1.1 s
  later) and whipping the tier below; at a quarter his wings flap for nothing (*"Hnnngh... fly... FLY..."*, hero:
  *"Those are for show, aren't they."*). Beaten: slow motion, his sack bursts into a pile of diamonds on tier 2, his
  *"My... sack..."*, the announcer's call, the hero's *"Thank you."*, then the counter ticks to 990, *"THE SACK LIFT:
  IMPS ONLY"*, the hero's *"Imps only. I'll squeeze."*, and the Sack Lift's choice (shortcut 1) comes up right there,
  about 9 s after he falls, wherever the fight ended. **The floor ends from the defeat, not from a climb** (J,
  2026-10-19): on beta v159 the bot killed him during his climb, the defeat's `nextSection` released a section that
  wasn't an Arena, tier 2's camera hold never let go and the run stood on the pile until the level timed out. So the
  defeat no longer moves the camera; it stops the lava, removes the gantry's wave and offers the lift. If the choice
  can't come up (another picker open), 2 s later the floor takes the long way by itself. And he can't be hurt while
  he climbs (`sceneUntouchable` from `fmClimb` to `fmPhase2`), so the fight always reaches tier 2. The path's `top` and
  carved `landing` sections stay in the file but the camera never reaches them now.
- **Feel:** a medium shake on each whip crack; a big shake when the lava takes a tier; slow motion as the sack bursts
  (diamonds hanging in the air); the roar swallows each tier.
- **Lines:** Announcer: *"FLOOR NINE NINETY-ONE! THE IMP FOREMAN!"* Hero: *"That's the biggest sack yet. Ours too,
  I bet."* Defeat, announcer: *"THE FOREMAN IS DOWN! HIS SACK, HOWEVER, IS UP FOR GRABS!"*; hero: *"Thank you."*
- **Length:** ~120 s.

---

### Stretch 2: the Depot stretch (Floors 960-951)

Where the imps' lifts let out: their depot of chains and counterweights, the rest of the village drifting through, and
the Magma King's guard taking over from the haulers. The Sack Lift lands here; the long way comes up into 960 too.

#### Floor 960: The Depot

- **New:** counterweights.
- **Path:** `↑ 2` sinking stones and ledges → `→ 3` along the depot's row of pulleys, pair after pair → `↑ 2` up the
  last pair → `↖ 1` the landing.
- **Obstacles:** paired platforms on chains over pulleys, each pair balanced with a diamond sack: shoot the sack off
  and his side rises. Sinking stones at the bottom.
- **Monsters:** sack imps swinging between the pulleys (they reload the counterweights).
- **Set piece and beat:** the imps' depot, a forest of chains, sacks hanging as weights. Shooting a sack off spills it
  (diamonds) and lifts him: the floor pays for itself. The last pair is a puzzle: the sack is on *his* side. He stands
  on the empty side and shoots a swinging imp's sack as it passes over the far pan: it drops in, that side sinks, his
  rises. Landing carved *960* and *SACKS GO UP*.
- **Surprise:** at the top of the pulleys the smoke parts and the red speck from Floor 1000 has a shape now: a glow
  like a chair, very far up.
- **Rewind:** shot the wrong sack and his side sank into the lava: rewind and pick again.
- **Feel:** chains creak, sacks thud into pans; a small shake as a pan drops; slow motion when a falling sack misses
  him; the roar drops away as the pans lift him.
- **Lines:** Announcer: *"FLOOR NINE SIXTY! THE DEPOT!"* Hero: *"They're using our diamonds as weights."* At the
  glow: *"Something up there's glowing. I'm going there, aren't I."*
- **Length:** ~85 s.

#### Floor 959: The Rest of the Village

- **New:** bobbing debris rafts (that bob and drift, not sink).
- **Path:** `≈ 3` a whole roof drifting along the lava river → `↑ 3` the river climbs in steps (hop raft to raft up
  the steps) → `≈ ← 2` back across on the last raft → the landing.
- **Obstacles:** big rafts drifting sideways across the lava (a whole roof, the well's bucket and frame, a fence still
  standing on its patch of meadow), tipping at the edges; sinking stones between them.
- **Monsters:** cinders drifting off the burning roofs, two at most.
- **Set piece and beat:** more of the village than he wanted to see: the rafts drift in a slow procession, and he rides
  them up a lava river that climbs in steps. Landing carved *959* on a raft of turf with the meadow's grass still on it.
- **Surprise:** the last raft is the village sign from Floor 998, come back up, half burned: *WELCOME TO...* He rides
  it across. (As built: `debrisRaftSignBack`, bobbing at the top of the steps until he stands on it, then drifting left
  with the camera to the far bank.)
- **Rewind:** a raft tipped him off its edge: rewind, stand in the middle.
- **Feel:** wood creaks, the well bucket clanks; the roar is close and steady (he is riding on it); slow motion when a
  tipping raft throws him and he lands on the next.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-NINE! THE REST OF THE VILLAGE!"* Hero (on the roof): *"I used to sit
  under this."* On the sign: *"Welcome to. Yeah. Thanks."*
- **Length:** ~90 s.

#### Floor 958: The Burning Thatch

- **New:** the thatch cinder.
- **Path:** `↗ 3` rafts and stones up a slope of the river → `≈ 2` a raft ride under a drift of burning roofs →
  `↑ 3` the bank → the landing.
- **Obstacles:** rafts and sinking stones, an updraft-free climb (keep it plain: the new monster is the idea).
- **Monsters:** thatch cinders, the village's roofs come alive. The first one alone, slowly, in a quiet stretch; shot,
  it bursts into two sparks that dart for two seconds. Later one thatch cinder with a plain cinder.
- **Set piece and beat:** the ride under the drift of burning roofs, their light flickering on the lava. Landing
  carved *958*.
- **Surprise:** the first thatch cinder isn't one of the roofs ahead of him: it's the roof he just rode past. It shakes
  itself loose behind him.
- **Rewind:** the two sparks that dart after a burst: rewind and dodge them on the same path.
- **Feel:** crackle and rustle from the roofs; slow motion when a spark darts past; the roar warm and close on the
  ride.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-EIGHT! THE BURNING THATCH!"* Hero (first burst): *"That was somebody's
  roof."*
- **Length:** ~85 s.

#### Floor 957: Hot Air

- **New:** heat updrafts.
- **Path:** `↑ 2` crumbling ledges → `→ 2` across the lava, column to column → `↑ 4` **the chimney**: one great updraft
  up a narrow rock chimney → `↗ 1` the landing.
- **Obstacles:** shimmering columns over the lava that carry him up, with crumbling ledges between them and sinking
  stones at the foot.
- **Monsters:** one imp on a high ledge throwing into the updrafts.
- **Set piece and beat:** the floor where he flies a little. Across the lava from column to column, then the chimney:
  the heat tosses him up its whole height, ash floating up beside him, and spits him out at the top. (The King's
  Chimney at 951 is this, a hundred times bigger.) Landing carved *957*. Each column's top stands at least 0.3 over the
  ledge it carries him to, so its throw lands him there.
- **Surprise:** the imp throws an ember down into the chimney's updraft, and the draft blows it straight back up into
  the imp.
- **Rewind:** knocked out of a column by an ember: rewind and wait for the throw.
- **Feel:** the roar becomes a whoosh in the columns; at the top of the chimney toss, a planned slow-motion hang (he
  floats, looks around, then drops onto the ledge); no shake.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-SEVEN! HOT AIR!"* Hero (first updraft): *"Huh. That's actually nice."*,
  then, a beat later: *"No. It's still Hell."*
- **Length:** ~80 s.

#### Floor 956: The Drip

- **New:** falling slag.
- **Path:** `↑ 3` a chain lift → `→ 3` a run under the melting ceiling → `↑ 2` ledges → the landing.
- **Obstacles:** a climb and a run under a melting ceiling: chain lifts and ledges, slag dripping from above; the first
  three drips come one by one while he stands still.
- **Monsters:** sack imps on swings in the second push (shoot them between the drips).
- **Set piece and beat:** the floor above is melting through: the run under it is a dance between the glowing drips.
  Landing carved *956* and *MIND THE DRIP*.
- **Surprise:** as he steps onto the landing, one huge drip gathers over the ledge he just left and drops a lump the
  size of a cart into the lava behind him. The splash fills the screen.
- **Rewind:** the drips fall in the same spots every time: a rewind shows the gaps.
- **Feel:** every drip hisses as it gathers; a medium shake when a lump lands near him, a big one for the last;
  slow motion on a lump that misses by a hair.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-SIX! THE DRIP!"* Hero: *"The ceiling's melting. Great."*
- **Length:** ~85 s.

#### Floor 955: The Falls

- **New:** lava falls (curtains that pour and stop on a beat).
- **Path:** `→ 3` along the wall of falls, slipping through each one when it thins → `↑ 3` up the ledges behind the
  falls → `← 1` the landing.
- **Obstacles:** a wall of lava falls, each on its own beat, with ledges behind and between them; geysers below.
- **Monsters:** cinders that drift out of the falls (they live in the spray), one at a time.
- **Set piece and beat:** running through the falls themselves, slipping through each one as it thins, then climbing
  up behind them. Landing carved *955*, half hidden behind a fall that stops just as he arrives. The climb's ledges
  stand just clear of the last fall, so he can wait on them for its beat.
- **Surprise:** when the last fall stops it shows the landing, and on it a damned soul holding out its hands under the
  fall, waiting for it to pour, forever.
- **Rewind:** a fall poured on him: rewind, count the beat, go.
- **Feel:** the roar is loud and wet, each fall's gurgle a second before it pours; a medium shake when a fall pours on
  right beside him; slow motion as he slips through a fall a hair before it pours again.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-FIVE! THE FALLS!"* Hero: *"Wait for it. ...Wait for it."*
- **Length:** ~90 s.

#### Floor 954: The Guard Post

- **New:** the imp guard (`impGuard`).
- **Debut:** the guard debuts here under the one-floor route; this replaces its old Floor 930 debut from the 10-floor layout.
- **Path:** `↑ 3` counterweights → `■` the guard post: the camera holds until the guard falls → `→ 2` along the King's
  border wall → `↑ 1` the landing.
- **Obstacles:** counterweights and solid ledges, known pieces only.
- **Monsters:** the Magma King's guard: the first stands alone on a ledge at the top of the first push, doesn't run,
  doesn't carry anything, and throws three embers in a fan. Later a guard with a plain imp. Guards drop nothing.
- **Set piece and beat:** a guard post at the edge of the King's territory: a slag helmet on a spike, a carved *THE
  KING'S WASTES*. The imps get organised from here. Landing carved *954*.
- **Surprise:** when the first guard falls, the red glow at the top of the sky pulses once. Somebody up there noticed.
- **Rewind:** a fan he jumped into: rewind and read the spread.
- **Feel:** slow motion when the middle ember of a fan passes close; a small shake on each ember into the wall; the
  border wall's run is quiet, the roar far below.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-FOUR! THE GUARD POST!"* Hero (first guard): *"This one's not carrying
  anything. Bad sign."* At the pulse: *"...Hi."*
- **Length:** ~85 s.

#### Floor 953: Second Lap

- **New:** a floor that repeats (he climbs Floor 953 twice: the landing says *953* and the door leads back to the
  bottom of 953).
- **Path (each lap):** `↑ 2` geysers and stones → `→ 2` a run past a lava fall → `↗ 2` a slope to the landing; then the
  door, and the same again.
- **Obstacles:** a loop of geysers, falls and sinking stones, the same both laps.
- **Monsters:** the same two imps on the same ledges, both laps, throwing at the same moments.
- **Set piece and beat:** Hell's loop on a bigger scale. The first lap teaches the pattern; the second is the same
  floor, and he knows it (rewind is just the small version of this). The carving on the landing has a second line on
  the second lap: *953* / *953 (AGAIN)*.
- **Surprise:** on the second lap the first imp hesitates before it throws. It remembers him too. It shrugs and
  throws on the beat anyway.
- **Rewind:** a rewind on the second lap shows the same pattern as the first lap did.
- **Feel:** the second lap's music is the same bars as the first; slow motion on a near miss only on the first lap (on
  the second he knows); the roar and every sound exactly the same both laps.
- **Lines:** Announcer, the second time: *"FLOOR NINE FIFTY-THREE! ...AGAIN!"* Hero: *"I know this one."* Landing,
  announcer: *"SECOND LAP: DONE!"*
- **Length:** ~110 s (two laps of ~55 s).

#### Floor 952: The Sack Pile

- **New:** none (the stretch's test: everything at once).
- **Path:** `≈ 2` a raft to the foot of the pile → `↑ 3` counterweights and updrafts up the side of the pile → `■` the
  line of three guards at the top: the camera holds until the line is cleared (the line stands on a ledge of sacks on
  the held screen, left of the pile's top, and steps out as he gets there) → `↑ 1` the landing.
- **Obstacles:** counterweights, rafts, updrafts, falls; the lava rising through the whole last push.
- **Monsters:** a line of three guards on the pile at the top, throwing on a count; imps and cinders on the way up.
- **Set piece and beat:** the imps' camp, where they pile the sacks before hauling them up to the throne: a mountain of
  sacks under a carved *KEEP OFF THE PILE*. Clear the line and the pile spills: a heap of diamonds.
- **Surprise:** it spills toward him. An avalanche of sacks bursts down the slope past him into the lava, diamonds
  everywhere; he grabs what he can.
- **Rewind:** the count: a rewind replays the same three throws.
- **Feel:** the lava's roar under the whole last push; a big shake as the pile spills; slow motion when a bursting sack
  sails past him.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-TWO! THE SACK PILE!"* Hero (clearing the line): *"He keeps guards. So
  he's expecting someone."*
- **Length:** ~110 s.

#### Floor 951: The Keeper's Mound (floor boss)

- **New:** the floor boss, **the Pile Keeper** (see *Floor bosses*).
- **Path:** `■` tier 1 → `↑ 1` → `■` tier 2 (the pile tilting like a raft) → `» 2` the mound sinks and a lava wave
  rolls round its shoulder after him → `■` tier 3, the top.
- **Obstacles:** a climbing arena around a mound of sacks sinking into the lava; counterweights between tiers.
- **Monsters:** the Pile Keeper and up to two imp guards in his second phase.
- **Set piece and beat:** the biggest pile in the Wastes, the King's share, slowly sinking. Beat the Keeper and the
  pile spills.
- **Surprise:** behind the spilled pile, roaring, the King's heat vent: **the King's Chimney** (shortcut 2), the
  chimney from 957 a hundred times bigger. Landing carved *951*.
- **Rewind:** his tier-3 sweeps loop: rewind and duck the same sweep.
- **Feel:** a medium shake on each sack that bursts; the big one when the pile goes; slow motion as the Keeper sinks;
  the chimney's roar drowns out the lava's.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-ONE! THE PILE KEEPER!"* Hero: *"He's sitting on it. Like a hen."* Defeat,
  announcer: *"THE PILE KEEPER IS BURIED IN HIS OWN PILE!"*; hero: *"Fitting."*
- **Length:** ~140 s.

---

### Stretch 3: the Heat Stairs (Floors 910-901)

The approach to the Slag Throne: stairs cut into the rock, the heat rising, the King's guard on the steps, and the
King's voice coming down from above. The stairs take their last pieces of the toolkit here (the slag flow, the dark,
the cooling crusts, one a floor on floors with no other new idea); otherwise each floor combines the known ones into
its own set piece, and the voice builds to the door. The King's Chimney lets out at 910; the long way comes up into
910 as well.

#### Floor 910: The Stair Foot

- **New:** stairs that sink (a staircase of sinking stones, one step at a time).
- **Path:** `↗ 4` the sinking stair up out of the lava → `→ 1` a landing on the turn → `↑ 1` an updraft at the turn →
  `↖ 3` the stair on → the landing. (As built: the turn's updraft stands with its top 0.3 under the ledge over it, so
  its throw lands him there; the rising lava stops at 5.9, under that ledge.)
- **Obstacles:** the sinking stair up out of the lava, updrafts at the turns.
- **Monsters:** one imp guard at the top.
- **Set piece and beat:** the foot of the Heat Stairs, carved *TO THE THRONE*. If he came up **the King's Chimney**
  the lava rises faster here and the King's voice comes down the stairs for the first time, amused (*"Came up my
  chimney, little spark? Rude."*); by the long way, the floor is as written. Landing carved *910*.
- **Surprise:** at the turn the haze opens and the glow has become a throne: a silhouette on a mound of slag at the top
  of the sky, smoke rolling off it. Nobody on it that he can see.
- **Rewind:** a step that sank under him: rewind and take the stair at a run.
- **Feel:** a small shake on each step; the roar right behind him all the way up the stair (louder by the Chimney);
  slow motion as he leaves a step on its last instant.
- **Lines:** Announcer: *"FLOOR NINE TEN! THE HEAT STAIRS! SOMEBODY'S WAITING AT THE TOP!"* Hero: *"Every step's
  hotter. Of course."*
- **Length:** ~80 s.

#### Floor 909: Ember Rain

- **New:** the ember shower.
- **Path:** `↖ 3` stairs and crumbling ledges → `→ 2` a shelf under an overhang → `↗ 3` the stairs on → the landing.
  (As built: the stair is three steps, all on screen, straight up onto the shelf; the soul's slab is 1.85 over the
  shelf, so he can stand on it as well as under it.)
- **Obstacles:** stairs and crumbling ledges, steam raining embers two at a time.
- **Monsters:** cinders in the steam, one at a time.
- **Set piece and beat:** the lava sea's steam boils up the stairwell and rains fire. Landing carved *909*.
- **Surprise:** on the shelf a damned soul sits in the rain holding a slab of rock over its head, the same slab,
  forever. He can stand under it with it. It drops its shard while he waits.
- **Rewind:** an ember pair he misread: rewind; the shower falls in the same pairs.
- **Feel:** embers hiss into the steps; slow motion when one falls past his face; the roar muffled by steam, the
  rain's patter over it.
- **Lines:** Announcer: *"FLOOR NINE OH NINE! EMBER RAIN!"* Hero (warning, 1.5 s before the first): *"Now it's
  raining fire."* Under the slab: *"Room for two?"*
- **Length:** ~80 s.

#### Floor 908: The Cart Run

- **New:** slag flows (the crust sliding on the lava like a conveyor).
- **Path:** `↑ 2` stairs up to the chute's head → `» 4` **the cart ride**: down a slag chute that runs along the
  stairwell, in the village's burning cart → `↑ 3` a guard lift at the chute's end → the landing.
- **Obstacles:** a slag flow down a chute along the side of the stairwell (seams glowing the way it runs), gaps in the
  chute to jump (out of the cart, over, back in), a chain lift at the end.
- **Monsters:** imp guards on the chute's landings throwing fans at the cart (never more than two on screen).
- **Set piece and beat:** the hay cart from the Spill (Floor 998) is still burning, stuck at the head of the chute
  where the current left it. He climbs in and the flow takes it: a ride along the stairwell at the flow's speed, guards
  throwing at him from the landings, gaps to jump. At the chute's end the cart flies off into a guard lift's cage and
  the lift takes him up. Landing carved *908*.
- **Surprise:** the cart. Ninety floors up, the same hay cart, still burning.
- **Rewind:** a jump off the cart he fluffed: rewind and jump on the same lip.
- **Feel:** the roar at full speed, the cart's wheels rattling on the crust; a planned slow-motion beat on the cart's
  biggest jump; a big shake as it lands in the lift's cage; push music the whole ride.
- **Lines:** Announcer: *"FLOOR NINE OH EIGHT! THE CART RUN!"* Hero (at the cart): *"Still burning. Respect."*
  Riding: *"Going up. Unfortunately."*
- **Length:** ~90 s.

#### Floor 907: I Can Hear You

- **New:** the dark (a stretch lit only by the lava's glow and his shots).
- **Path:** `↑ 2` crumbling stairs → `→ 3` the dark: a shelf under thick smoke → `↗ 2` crumbling stairs with geysers in
  the gaps → the landing.
- **Obstacles:** crumbling stairs with geysers in the gaps; the dark stretch (every hazard still glows 1 s ahead: in
  the dark the telegraph is the light).
- **Monsters:** cinders and one imp.
- **Set piece and beat:** the smoke closes over the stair and it goes dark: just the lava's glow far below, the
  cinders' light coming at him, and his own shots lighting the shelf as they fly. In the dark, the whole stair shakes
  and the King's voice comes down it from ahead: he's waiting, he knows the hero is coming, and he isn't getting up.
  Landing carved *907*.
- **Surprise:** out of the dark the only light above is the throne's glow, and it chuckles.
- **Rewind:** a step he didn't see crumble: rewind, shoot ahead to light it.
- **Feel:** a long low shake under the King's voice; the roar far and hollow in the dark, his shots loud; slow motion
  when a cinder he couldn't see till late pops past him.
- **Lines:** Announcer: *"FLOOR NINE OH SEVEN!"* The King: *"I can hear you climbing, little spark."* Hero: *"Was
  that the stairs?"*
- **Length:** ~85 s.

#### Floor 906: The Loop, Upstairs

- **New:** none; set piece: Hell's loop as a puzzle he solves with rewind.
- **Path:** `↗ 2` the stair → `→ 4` the loop: the same two screens of stair, repeated (Hell pasting it in again, the
  same crack, the same carving) → `↑ 2` out of the loop → the landing.
- **Obstacles:** a geyser row and falls that fire in one exact pattern; the only way through is the one gap in the
  pattern, which is hard to see the first time.
- **Monsters:** none: the loop is the floor.
- **Set piece and beat:** the stair loops round on itself; the same burst goes off in the same crack every lap.
  Rewind is the expected answer. Landing carved *906* and *YOU ARE HERE. STILL.*
- **Surprise:** on the last lap his own scorch marks are on the steps from the lap before: he's been here, in this
  exact spot, at least twice.
- **Rewind:** the floor's whole point: rewind, watch the pattern backward, see the gap.
- **Feel:** a skipped-record whoosh each repeat; a medium shake per burst; slow motion when he threads the gap.
- **Lines:** Announcer: *"FLOOR NINE OH SIX! THE LOOP... UPSTAIRS!"* Hero: *"Same trick. I'm getting good at
  this."*
- **Length:** ~80 s.

#### Floor 905: The Collapse

- **New:** none; set piece: a chase.
- **Path:** `» ↗ 4` up the collapsing stair, the lava surging after it → `» 2` sideways along a ledge with the wave at
  his heels → `↑ 2` an updraft out of it → the landing.
- **Obstacles:** the stair falls away behind him step by step while the lava surges up after it (fast, but
  telegraphed by the rumble and the first steps falling); crumbling stones and an updraft at the end.
- **Monsters:** none until the breather; then one imp guard.
- **Set piece and beat:** the stair gives way and he runs up it with the lava at his heels, the floor's one sprint.
  Landing carved *905*, which he reaches just as the last step goes.
- **Surprise:** the stair collapses ahead of him too: a gap opens on the ledge run, a chain hanging over it.
- **Rewind:** caught by the wave: rewind; the collapse is the same every time.
- **Feel:** a big shake as the wave starts and with each stretch of stair that goes; the roar at its loudest in the
  Wastes; slow motion on the chain over the gap; push music, then silence on the landing.
- **Lines:** Announcer: *"FLOOR NINE OH FIVE! RUN!"* Hero (landing): *"I hate stairs now. Specifically."*
- **Length:** ~70 s.

#### Floor 904: The Count

- **New:** none; set piece: guards in a line throwing on a count.
- **Path:** `↑ 3` counterweights → `■` the line on the stair: the camera holds while three guards throw on their
  count → `→ ↗ 3` along and up the stair under falling slag → the landing.
- **Obstacles:** counterweights and ledges, falling slag.
- **Monsters:** a line of three imp guards across the stair, throwing their fans on a count (one, two, three): read the
  count, move between throws. (As built: the line hangs over the right half of the held screen, x 2-4.2.)
- **Set piece and beat:** the King's guard at its best, holding the stairs. Landing carved *904*.
- **Surprise:** the third guard counts wrong. It throws on *four*, a beat late (it still winds up first), and the
  other two look at it.
- **Rewind:** the count is exact: a rewind replays it.
- **Feel:** each guard grunts its number; a small shake on every fan into the steps; slow motion when a fan's middle
  ember passes close.
- **Lines:** Announcer: *"FLOOR NINE OH FOUR! THEY'RE COUNTING!"* Hero: *"One, two, three. Duck."* After the fourth:
  *"There's always one."*
- **Length:** ~90 s.

#### Floor 903: The Last Updraft

- **New:** cooling crusts (climbing the inside of a lava fall).
- **Path:** `↑ 4` up the inside of a lava fall on cooling crusts → `→ 2` across between falls on an updraft → `↑ 2`
  the last updraft → the landing. (As built, until the crusts exist: solid steps either side of the fall, clear of it,
  so he waits on a step for the off beat and crosses; the first updraft's throw reaches the ledge beyond it; the ember
  shower at the top rumbles 1.5 s before its first pair.)
- **Obstacles:** a lava fall that stops on a beat and crusts over (climb its face while it's off), updrafts between
  falls (ride up while the fall beside him is off), ember shower at the top.
- **Monsters:** cinders in the falls.
- **Set piece and beat:** climbing up the inside of a stopped lava fall, crust to crust, before it pours again; then
  the hottest air in the Wastes, rising straight to the throne. Landing carved *903*.
- **Surprise:** at the top the throne fills the sky, close enough to see the slag dripping off it, and the King
  speaks: *"Come up, then. I'll stay seated."*
- **Rewind:** the fall poured on him mid-climb: rewind to the crust below and climb faster.
- **Feel:** the crusts crack 1 s before the pour; a medium shake when the fall pours again; slow motion on a crust he
  leaves as it melts; the roar of the falls all around.
- **Lines:** Announcer: *"FLOOR NINE OH THREE!"* Hero (on the crusts): *"Climbing a waterfall. Of lava. Sure."* After
  the King: *"He knows I'm coming. Great."*
- **Length:** ~85 s.

#### Floor 902: The Door

- **New:** none; set piece: the door.
- **Path:** `↗ 2` sinking stairs and crumbling ledges → `→ 2` along the throne's terrace past the guards → `↑ 1` →
  `■` the door.
- **Obstacles:** a short climb of sinking stairs and crumbling ledges, guards on the steps, then the landing.
- **Monsters:** two imp guards, then quiet.
- **Set piece and beat:** the top of the Heat Stairs: a slab of obsidian carved *THE SLAG THRONE*, the throne's glow
  leaking round its edges. The camera stops; through it, one last chuckle (*"Mm-hm-hm. Knock, little spark."*), and
  the Grudge pick. Floor 901 opens on the other side. Landing carved *902*.
- **Surprise:** he knocks. Three knocks come back from the other side, slow. Then the chuckle.
- **Rewind:** a guard's fan on the terrace: rewind and read it.
- **Feel:** a long low shake with each knock from the other side; the roar dies away to nothing at the door: just
  heat and the King breathing; the music drops out.
- **Lines:** Announcer: *"FLOOR NINE OH TWO! THE MAGMA KING AWAITS!"* Hero: *"Fine. I'll knock."*
- **Length:** ~75 s.

#### Floor 901: The Slag Throne (the Warden)

- **New:** the Warden, **the Magma King**.
- **Path:** `■` the hall (the one floor allowed to stand still, a locked screen) → its third phase is a run: the lava
  flood rolls across the hall and he outruns it or rewinds through it → after the defeat, `↗ 1` up the broken throne's
  slag to the Waystone.
- The fight is Story.md's *The Magma King's fight*, unchanged (three phases: seated; standing with his guard; enraged,
  the lava flood you rewind through), moved from the old "Floors 910 to 901" hall to **Floor 901**: one hall, the
  throne on its slag mound at the far end. The climbing is the nine floors of stairs before it. His title becomes
  *"FLOOR NINE OH ONE! THE SLAG THRONE!"*; after his defeat the hero walks up the broken throne's slag to the Waystone
  (*"WAYSTONE LIT! FLOOR NINE HUNDRED ONE!"*). Then the Shaft: Circle IX wakes up.
- **Set piece:** the throne rising out of the slag with him on it, the glow he has watched for a hundred floors
  finally close enough to touch.
- **Surprise:** he stands. The King who never gets up gets up, at half health, the only time he ever does
  (*"...You made me STAND."*).
- **Rewind:** the flood: outrun it or drag it back. (As built: the shockwave is 1.4 by 0.8, low enough to jump on a
  read; the flood's glow covers all the floor it reaches, up to the safe strip at the far wall.)
- **Feel:** as built for his arena (the throne rising, the big hit, his theme and its enrage variant, slow motion at
  his fall); the flood is the Wastes' last and loudest roar.
- **Length:** 2-4 minutes.

---

## 5. Floor bosses (Circle X)

Every 10th floor (the floors ending in 1) ends its stretch with a floor boss: smaller than a Warden, one of the
Circle's family (here the imps and the village's sparks), with a reason to be on its floor, and fought on a
**climbing arena**:

- **The climbing arena:** a tall locked-width screen of three tiers over the lava. The fight starts on the bottom tier;
  each phase change, the lava rises and swallows the tier he's on (rumble, 2 s of bubbles) and the camera steps up to
  the next tier, where the boss has climbed; on the played route one tier change is a short sideways run (991's
  gantry, 951's wave round the mound), so a boss floor turns too. The last tier is the landing. Three phases, one per
  tier; every attack telegraphed 1 s or more; at most 3 enemies on screen counting the boss.
- **What a floor boss gives:** a diamond burst, a Grudge pick, and often the way on (a shortcut stands on its landing).

### On the played route

#### The Imp Foreman (Floor 991)

- **Concept:** the fattest imp in the Wastes, sitting on the biggest sack of the village's diamonds with a chain whip,
  driving the sack haulers up to the lift. Too heavy to fly; his wings are for show.
- **Why he's there:** the hauling route's top, where the sacks go into the Sack Lift. He's the one who loads it.
- **Arena:** the lift's loading tiers. Bottom tier sinking stones, a lift between tiers, the lift's landing at the top.
- **Attacks:**
  1. *Tier 1, on the sack:* cracks his whip down across a tier (a glowing line 1 s before), and whistles sack imps onto
     the swings (two at most). Hitting his sack spills diamonds and makes him angry.
  2. *Tier 2, the climb:* he hauls himself up his chains to the lift, kicking loose sinking stones and throwing his
     whip's chain in an arc; the lava takes tier 1.
  3. *Tier 3, the lift:* he swings across the gap on the lift's chain with the sack, back and forth like his haulers
     (the sack-swing, boss-sized), dropping embers from the sack on each pass. Hit the sack three times: it bursts, he
     falls into the lava with it, the diamonds stay.
- **Lines:** Foreman, at the start: *"Who's this? Get back in the sack!"* Hero: *"I've never been in a sack."*

#### The Pile Keeper (Floor 951)

- **Concept:** an old imp guard, the Magma King's oldest, grown huge and slow sitting on the King's share of the
  diamonds; a helmet of slag the size of a cauldron, a spear he uses as a shovel.
- **Why he's there:** he keeps the King's pile at the top of the Depot, the last stop before the throne.
- **Arena:** the mound of sacks, sinking into the lava: each phase the pile sinks a tier and the hero climbs it.
- **Attacks:**
  1. *Tier 1, seated on the pile:* shovels diamond sacks off the pile at him in an arc (they burst on landing in a
     shower of embers; the glow of the landing spot 1 s before), and plants his spear to send a shockwave of embers
     along the tier.
  2. *Tier 2, standing:* calls two guards up the pile (a line of three with him, throwing on a count), and the pile
     tilts (the tier tips like a raft).
  3. *Tier 3, the top:* the pile sinks fast; he swings his spear in wide sweeps at the top of the mound while the lava
     rises (rewind fodder: the sweep pattern loops). Beat him and he sinks with the pile, which bursts.
- **Lines:** Keeper: *"Nobody touches the pile."* Hero: *"I'm not touching it. I'm taking it."*

### On the long way (built by the generator around a hand-made boss)

| Floor | Floor boss | Concept and why | Climbing-arena attacks |
|---|---|---|---|
| 981 | **The Bonfire** | The village's hay barn, burned through the portal in one piece and come alive: a walking bonfire of the meadow's hay, the biggest of the cinders. | Sheds cinders on each tier; rolls across the tier as a burning bale (jump it, 1 s glow); in the last phase splits into three small bonfires. |
| 971 | **The Chain Gang** | Three sack imps chained to one huge sack, hauling it up the Wastes together, arguing. | They swing as one on a triple chain; break the chains one by one and each falls free and fights alone; the sack bursts when the last goes. |
| 961 | **The Liftmaster** | The imp at the top of the Sack Lift who works its great wheel; the long way comes up beside his lift. | Drops the lift's counterweights on a tier (glowing shadow below), sends the lift down at him, hides behind the wheel's spokes (shoot between them). |
| 941 | **The Roof** | The village hall's whole thatched roof, come alive like the thatch cinders, the largest piece of the village in the Wastes. | Drifts over the tiers raining sparks; shot, it splits into thatch cinders, then into sparks; at a third it falls on the tier (telegraphed shadow) and burns. |
| 931 | **The Guard Captain** | The captain of the King's guard, who drills the guards in their lines of three on the stairs below. | Calls lines of guards that throw on his count; throws a fan of five; at a third, a charge across the tier with his spear. |
| 921 | **The Stoker** | The imp who stokes the King's lava with a shovel the size of a door, keeping the Wastes hot. | Shovels slag in arcs; opens geysers in a row (1 s glow); in the last phase makes the lava rise faster tier by tier. |
| 911 | **The Herald** | The King's herald imp, who blows the horn to announce every challenger to the throne (the King loves a show). | His horn calls geysers in a fanfare pattern (looping: rewind it), summons two guards, and the last blast is a shockwave down the stairs. His lines announce the hero, badly. |

## 6. Circle IX: the Cracked Road

Built **one level per floor** the same way as Circle X (J, 2026-10-19: *"Build Circle IX the same way Circle X was
built"*): the climb grammar (section 1), the toolkit (section 2, every piece of it now known), a played route of about
30 floors in three stretches joined by two shortcuts, a floor boss every 10th played floor. The Spire Fiend's fight on
Floor 801 is its own task (k_e16304d7a504): the route ends at his arena's entrance, on Floor 802's landing.

### What changes above the Wastes

- **The place.** The Heat Stairs come out above the Wastes' smoke onto an old road of black rock that winds up through a
  forest of black spires with glowing seams. Nobody remembers who laid it. It is the only way up, and it keeps
  splitting: pieces of it hang over the drop, float, settle, tip and break.
- **The Rift** is the hot bath of Circle IX: under every floor the road's cracks open onto a chasm of magma far below
  (the same `lavaSea` and `scald`: a hot bath, never a pit), rising on the floors where it rises. The world below is
  the Circle's `sky`: the Rift's web of glowing cracks far down, black spires standing in it, broken slabs of road
  floating in the haze, ash drifting up.
- **The family:** the **Brimstone Demons**, born from the cracks (Story.md, *Roster*): the demon who plants itself and
  breathes fire (900), the whelp that scuttles out of a crack and charges (897), the lobber that throws brimstone from a
  spire ledge (894), and the nests in the cracks that keep spitting whelps until shot (856). The imps' hauling route
  still runs up through the road (sack imps on swings, their hoist on 859): known, and the reason diamonds are here.
  The damned sit on the breather ledges as before (loop shards).
- **At most 4 enemies on screen** (section 1, *The pace*), and still at most one new idea per floor, a new monster alone.
- **The great spire.** Far above, through the haze, the tallest spire, its seams pulsing, always straight up: the
  Spire Fiend's. It grows floor by floor (the thread the King's glow was in Circle X; scenery, not drawn yet). Its
  shards are what falls on the road. He is never heard on the route (his voice and fight are k_e16304d7a504's): the
  spire hums and grinds instead.
- **The music** stays the Wastes' loop and push music until the Cracked Road has its own (Story.md, *Music*).

### The toolkit, new in Circle IX

| Piece | What it does | Why it's in Hell | Telegraph | First floor | Engine |
|---|---|---|---|---|---|
| **Road slabs** | The road itself: big flat slabs of black rock with glowing seams, hanging over the Rift. Solid. | The road | none | 900 | yes (`roadSlab`, a `ledge` with its own art) |
| **Settling slabs** | Broken slabs floating on the Rift's magma: stand on one and it settles under (sinking stones in the road's look) | The road, broken and fallen | It dips and hisses the moment he lands | 900 (known: 1000) | yes (`settleSlab`, ability `sink`) |
| **Split slabs** | A slab with a glowing seam down its middle: it splits apart under him and falls, faster than an old ledge crumbles (0.8 s). Gone, except by rewind. | The road splitting, the way it always does | The seam blazes the moment he lands | 899 | yes (`splitSlab`, ability `crumble`) |
| **Spire shards** | A shard of spire breaks off overhead and falls. | The spires shed (the great spire most of all) | The seam above glows and drips grit for 1.2 s | 898 | yes (`spireShard`: a drip that drops a `shard`) |
| **Brimstone vents** | A crack in the road belches a column of brimstone fire straight up on a beat. | The cracks the demons come out of, breathing | The crack glows and smokes for 1 s | 896 | yes (`brimstoneVent`, ability `geyser`) |
| **Rising slabs** | A slab the Rift's pressure shoves up a shaft when he stands on it, then lets back down. | The road heaving | It shudders and the crack under it glows before it moves | 895 | yes (`risingSlab`, ability `chainLift` with `startOnStand`, no chains) |
| **Seesaw slabs** | A long slab balanced on a spire's tip: it rocks, and tips if he stands at its end. | Pieces of road the spires caught | It rocks before it tips | 893 | yes (`seesawSlab`, ability `raft` with no current) |
| **Road creep** | A stretch of road sliding slowly back down toward the Rift like a belt: run with it, or against it and barely move. | The road slipping off the spires | Its seams crawl the way it slides | 860 | yes (`roadCreep`, ability `conveyor`) |
| **Whelp nests** | A crack that spits a whelp every few seconds until it is shot shut. | Where the whelps are born | It glows and chitters before each whelp | 856 | yes (`whelpNest`, ability `spawner`) |
| **Drifting slabs** | A slab of road broken off and drifting on the Rift's current: it waits at the break until he steps on, then carries him across, slow, to the next piece of road. A ride, not a new idea (959's rafts in the road's look). | The road keeps splitting; the pieces float off | It bobs at the break, waiting | 900 (known: 959) | yes (`driftSlab`/`driftSlabLeft`: `raft` with `holdUntilBoarded`, 0.5/s for 10) |
| **The toppling spire** | A spire falls across the Rift and he runs along it as it goes (a collapse chase on its back). | The spires shed whole | A long grind and a big shake, 1.5 s before | 858 (the collapse's set piece, known pieces) | config (split slabs in a `»` section) |

Every Circle X piece is known by now and comes up the road too: geysers and falls where the Rift spills into the
road, updrafts round the great spire, chain lifts and swings on the imps' hoist, the wave (the Rift surging), the
loop, the dark.

| Monster | What it does | Why it's here | First floor |
|---|---|---|---|
| `brimstoneDemon` | Plants itself a few steps in front of him and breathes a short fan of fire | Born from the cracks to keep him off the road | 900 |
| `demonWhelp` | A small demon that scuttles out of a crack and charges along the ledge; burns on touch; two hits | The cracks' young: too small to breathe fire, not too small to bite | 897 |
| `demonLobber` | Squats on a spire ledge and lobs lumps of brimstone in an arc (the landing spot glows 1 s first) | A demon that climbed the spires instead of the road | 894 |
| `whelpNest` | A crack in the rock that spits whelps until shot shut (a target, not a fighter) | Where the whelps come from | 856 |
| `impSack` on a swing | Known (997): the imps' hauling route runs on up the road | The diamonds still go up to the gate | 859 |

### The route through Circle IX

```
FLOOR 900-891    THE CRACKED ROAD          10 floors played          891: the Road Breaker
      │  SHORTCUT 1: THE SPARED DEMON      (a whelp he lets go splits the spire open, 891 → 860; the Shaft's crack at 870)
      │  skips 890-861 (the long way: the Rift, the Old Stair (its own shortcut 871 → 841), Shard Fall)
FLOOR 860-851    THE CARVER'S YARD         10 floors played          851: the Carver
      │  SHORTCUT 2: THE HOLLOW SPIRE      (up inside a spire the Carver hollowed, 851 → 810; the Shaft's crack at 830)
      │  skips 850-811 (the long way: Halfway Up the Road at 850, the Stairhead)
FLOOR 810-802    THE NEEDLE STAIR          9 floors played           801: THE SPIRE FIEND (his arena: k_e16304d7a504)
```

The same rules as Circle X's route: the Shaft climbs one floor per floor, jumps a dotted line through the crack on a
shortcut, and the skipped floors stay dark. **The long way** is played today by Circle IX's first-pass 10-floor stages
as stand-ins (Story.md, *Level numbers*), as Circle X's are: turning the Spared Demon down plays *The Rift* (890-881),
*The Old Stair* (880-871, whose collapsed stairwell is still Hell's shortcut from 871 to 841) and *Shard Fall*
(870-861), then Floor 860; turning the Hollow Spire down plays *Halfway Up the Road* (850-841) and *The Stairhead*
(840-831), then Floor 810 (830-811 have no stand-in yet: the counter jumps there until the generator builds the long
way).

### The shortcuts of Circle IX

| Shortcut | Where | Cost | Skips | Lines |
|---|---|---|---|---|
| **The Spared Demon** (`sparedDemon`) | Floor 891, after the Road Breaker: a whelp pinned under the broken road, squealing. Let it go and it owes him: it claws a crack open up the spire beside the road, straight up | **A Grudge** (letting a demon go takes some spite out of him: *Costs a Grudge: {0}*; with none, *NO GRUDGE TO GIVE*) | Floors 890-861: out at **Floor 860** | Hero, at the whelp: *"You're stuck. ...Ugh. Fine."* Taking it: *"Go on. Shoo. ...Oh, you're helping."* Announcer: *"HE SPARED A DEMON?! THE CROWD WANTS ITS MONEY BACK!"*, then: *"FLOOR EIGHT SIXTY! THE DEMON PAID HIM BACK!"* Keeping on: *"Nah. You'd bite me."* |
| **The Hollow Spire** (`hollowSpire`) | Floor 851, after the Carver: the spire he was hollowing out, a door cut in its foot, the inside a chimney of chiselled rock | **A harder floor after it** (risk, on the card: *Risk: the spire comes down behind you*): Floor 810 reads `shortcutTaken`: shards fall from the first second and the Rift rises faster, and the announcer says why | Floors 850-811: out at **Floor 810** | Hero, at the door: *"He hollowed it out. Of course he did."* Taking it: *"Up the inside. Don't touch anything."* Announcer: *"UP THE SPIRE?! HE SKIPPED HALFWAY... AGAIN!"*, then: *"FLOOR EIGHT TEN! AND THE SPIRE CAME DOWN BEHIND HIM!"* Keeping on: *"No. I'll take the road. It's the only one I trust."* |

(Costs: the Spared Demon a Grudge, the Hollow Spire a harder floor. The Old Stair, on the long way, a Grudge too.)

### The floors of Circle IX

Each page as in section 4. The landing carves the floor number and a word or two; every floor gives a Grudge pick
(the every-fifth-floor rule still waits for the long way), except where a shortcut is taken.

**The drift and the pace** (playability pass, 2026-10-19, after beta's playtest: Floors 900 and 899 played in 48 s and
17 s, the landings' numbers sat under the screen's bottom edge, and the bot fell off the road three times on 898). Every
floor below except the floor bosses now ends the same way before its landing: `≈` **the drift**, the road broken off
under him: one to three drifting slabs (`driftSlab`, 20 s each, with a slab of road or, on 899, a split slab to hop
between them) across the Rift to the far bank, a breather on the Wastes' loop music, then `↑ 1` three slabs up (the
last a copy of the step he took to the landing before) and the landing. Two drifts on most floors, one where the floor
already holds the camera for a fight or a chase (900, 898, 894, 892, 859, 858, 855, 852, 805, 804), three on 899 (Split
Ends: the road breaking up as he crosses). Every floor's climbs end where its next run or landing stands (the camera's
path no higher than a foothold + 1.0, a landing + 0.4 under its top), so the carved number is on screen when he lands
and a slip off a slab is a step, not a fall past the screen's edge (`floor_check.py` flags a carved number under the
screen). Floor 898 drops four shards (two on the flank, two on the ledge run) and spaces the rest 2.6 s apart.
Floor 808's Rift tops out under the drift and stops as it starts.

---

#### Stretch 1: the Cracked Road (Floors 900-891)

Where the road begins: the Heat Stairs let out onto it, the spires close in, and the demons climb out of its cracks.
The stretch brings the Circle's new pieces one a floor and ends on the demon who breaks the road.

#### Floor 900: The Cracked Road

- **New:** the Brimstone Demon.
- **Path:** `↗ 3` road slabs and settling slabs up out of the Rift → `→ 3` the road, a run along it → `■` the crack:
  the road splits open and the first demon hauls itself out; the camera holds until it falls → `↑ 2` → the landing.
- **Obstacles:** road slabs and settling slabs: everything else he knows from the Wastes, nothing yet.
- **Monsters:** one Brimstone Demon, alone, at the crack; nothing before it.
- **Set piece and beat:** the milestone and the first look at the Cracked Road: a hundred floors done, the spires
  closing in, the Rift glowing far below. The road splits in front of him with a big shake and the demon climbs out of
  it, slow and heavy, and plants itself. Landing carved *900* and *KEEP TO THE ROAD*.
- **Surprise:** when it falls, a diamond drops out of its rock: the imps' sacks leak on the road and the cracks keep
  what falls.
- **Rewind:** its breath fan: rewind and stand off out of its reach.
- **Feel:** a big shake as the road splits; slow motion the first time the fan passes close; the Rift's roar low and
  far below the whole floor.
- **Lines:** Announcer: *"FLOOR NINE HUNDRED! THE CRACKED ROAD!"* then *"A HUNDRED FLOORS DOWN! NINE HUNDRED TO GO!"*
  Hero: *"New Circle. Same Hell."* At the demon: *"That one came out of the road."*
- **Length:** ~75 s.

#### Floor 899: Split Ends

- **New:** split slabs.
- **Path:** `↑ 3` split slabs and road slabs up a broken wall of road → `→ 2` a shelf (the breather, a damned soul) →
  `↗ 3` split slabs and settling slabs → the landing.
- **Obstacles:** split slabs (they split under him, 0.8 s), road slabs, settling slabs.
- **Monsters:** one demon at the top of the slope.
- **Set piece and beat:** the road breaking up under his feet: every split slab goes the moment he leaves it, so the
  way down is gone as he climbs. Landing carved *899* and *WATCH YOUR STEP*.
- **Surprise:** the damned soul on the shelf is holding two halves of a split slab together, forever.
- **Rewind:** a slab he needed split before he got there: rewind and it is whole.
- **Feel:** a small shake and a crack on every split; slow motion when he leaves a slab in its last instant.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-NINE! SPLIT ENDS!"* Hero (first split): *"Even the road's falling
  apart."*
- **Length:** ~75 s.

#### Floor 898: Shard Fall

- **New:** spire shards.
- **Path:** `→ 3` along the road under the spires (the first three shards one by one) → `↑ 3` up a spire's flank on
  slabs → `← 2` a ledge run back across with shards falling → the landing.
- **Obstacles:** spire shards falling from the seams overhead; road and settling slabs.
- **Monsters:** one demon on the ledge run.
- **Set piece and beat:** the spires shed in earnest. Landing carved *898* and *HARD HATS*.
- **Surprise:** the last shard lands point-down in the road and stands there: a step up to the landing.
- **Rewind:** the shards fall in the same spots every time: a rewind shows the gaps.
- **Feel:** grit trickling before each shard; a medium shake when one lands near him.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-EIGHT! SHARD FALL!"* Hero: *"Even the scenery wants me dead."*
- **Length:** ~80 s.

#### Floor 897: The Whelps

- **New:** the demon whelp.
- **Path:** `↗ 2` slabs → `→ 3` the road, cracks along it, the first whelp alone out of one → `↑ 3` settling slabs and
  ledges with a whelp and a demon → `→ 1` the landing.
- **Obstacles:** road, split and settling slabs; known.
- **Monsters:** whelps: the first alone, in a quiet stretch; later a whelp with a demon.
- **Set piece and beat:** the cracks have young. Landing carved *897* and *DON'T FEED THEM*.
- **Surprise:** the second whelp charges so hard it runs off the end of the road into the Rift.
- **Rewind:** a whelp he didn't see coming: rewind and shoot it in the crack.
- **Feel:** chittering from the cracks before each whelp; slow motion as the first one leaps past.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-SEVEN! THE WHELPS!"* Hero: *"That one's small. And angry."*
- **Length:** ~80 s.

#### Floor 896: Brimstone Vents

- **New:** brimstone vents.
- **Path:** `↑ 2` slabs → `→ 3` the vent road, cracks in it blowing in order → `↗ 2` slabs → the landing.
- **Obstacles:** brimstone vents along the road, split slabs between them.
- **Monsters:** a whelp on the vent road, a demon at the top.
- **Set piece and beat:** the road breathing fire, the vents going off in a row as he runs. The first vent glows for a
  full second with nothing else around. Landing carved *896* and *SMELLS LIKE EGGS*.
- **Surprise:** the last vent blows a whelp out with its column; it lands on its back, kicking.
- **Rewind:** the vents fire in the same order every time.
- **Feel:** a medium shake for every blow within a screen; slow motion when a column misses him by a hair.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-SIX! BRIMSTONE VENTS!"* Hero (first vent): *"The road's breathing fire
  now."*
- **Length:** ~80 s.

#### Floor 895: The Heave

- **New:** rising slabs.
- **Path:** `→ 2` road → `↑ 4` the heave: two rising slabs up a shaft in the spires, ledges between → `↖ 2` → the
  landing.
- **Obstacles:** rising slabs, road and split slabs.
- **Monsters:** whelps on the ledges between the slabs (one, then two).
- **Set piece and beat:** the road shoving him up a shaft of spires. Landing carved *895* and *HOLD ON*.
- **Surprise:** beside the second slab a damned soul rides its own slab up and down, forever, waving.
- **Rewind:** stepped off too soon: rewind and ride it to the top.
- **Feel:** a medium shake as each slab heaves; the roar drops away as he rises.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-FIVE! THE HEAVE!"* Hero (first heave): *"Going up. Didn't ask."*
- **Length:** ~80 s.

#### Floor 894: The Lobbers

- **New:** the demon lobber.
- **Path:** `↑ 2` slabs, the first lobber alone on a spire ledge above → `■` the spire ledges: the camera holds until
  two lobbers are down → `→ 2` → `↑ 1` → the landing.
- **Obstacles:** road slabs and settling slabs; known.
- **Monsters:** lobbers: the first alone; then two, one each side, with a whelp.
- **Set piece and beat:** the demons that climbed the spires instead of the road, throwing it at him. Landing carved
  *894* and *DUCK*.
- **Surprise:** when the hold clears, a sack the imps hung on the spire splits and diamonds rain onto the ledge.
- **Rewind:** a lump he didn't read: rewind; the landing spot glows first.
- **Feel:** a small shake when a lump hits rock; slow motion on the first lump that passes close.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-FOUR! THE LOBBERS!"* Hero: *"Now they're throwing the road at me."*
- **Length:** ~85 s.

#### Floor 893: Seesaw Road

- **New:** seesaw slabs.
- **Path:** `→ 3` a row of slabs balanced on spire tips over the Rift → `↑ 3` → the landing.
- **Obstacles:** seesaw slabs (tip at the ends: stand in the middle), road slabs.
- **Monsters:** whelps on the seesaws, a lobber at the top.
- **Set piece and beat:** a road the spires caught when it fell, balanced over the drop. Landing carved *893* and
  *BALANCE*.
- **Surprise:** the last seesaw has a damned soul sitting on its far end, balancing it, forever.
- **Rewind:** tipped off the end: rewind, stand in the middle.
- **Feel:** creaks and grinding; slow motion when a tipping slab throws him onto the next.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-THREE! SEESAW ROAD!"* Hero: *"Stand in the middle. Got it."*
- **Length:** ~80 s.

#### Floor 892: Demon Row

- **New:** none; set piece: the row, then the road collapsing behind him.
- **Path:** `↑ 2` → `■` the row: three demons planted across the road, the camera holds until they fall → `» 3` the
  road gives way behind him (split slabs, the Rift surging after him) → the landing.
- **Obstacles:** split slabs in a chase, the Rift's surge, road slabs.
- **Monsters:** three demons in a row and a whelp (four at most).
- **Set piece and beat:** the demons queuing across the road under a carved *ONE AT A TIME*; when the last one falls,
  the road under the row goes too.
- **Surprise:** the road behind them gives way the moment they're down: run.
- **Rewind:** caught by the surge: rewind; the collapse is the same every time.
- **Feel:** a big shake as the road goes; the roar at its loudest in the stretch; push music on the chase.
- **Lines:** Announcer: *"FLOOR EIGHT NINETY-TWO! DEMON ROW!"* Hero: *"They're queuing now."* Chase, announcer:
  *"RUN!"*
- **Length:** ~95 s.

#### Floor 891: The Breaker's Road (floor boss)

- **New:** the floor boss, **the Road Breaker** (see *Floor bosses of Circle IX*).
- **Path:** `■` tier 1, the broken road → `» 2` the road gives way and he runs along what's left, the Rift behind →
  `↑ 2` → `■` tier 2, the top of the break.
- **Obstacles:** road and settling slabs, the Rift's surge on the run between tiers.
- **Monsters:** the Road Breaker and the whelps that crawl out of his cracks (two at most).
- **Set piece and beat:** the demon who keeps the road broken, so nothing climbs it. Beaten, he falls through his own
  crack. Under the broken road a whelp is pinned, squealing: **the Spared Demon** (shortcut 1).
- **Surprise:** losing, he slams so hard the tier under *him* breaks.
- **Rewind:** his slams crack the road in the same lines each time.
- **Feel:** a medium shake per slam; a big one when the road gives way; slow motion as he falls.
- **Lines:** see the boss. Defeat, announcer: *"THE ROAD BREAKER IS BROKEN!"*; hero: *"Road's open."*
- **Length:** ~130 s.

---

#### Stretch 2: the Carver's Yard (Floors 860-851)

The spire forest at the middle of the Circle, where every spire is carved: floor numbers, rules (*KEEP TO THE ROAD*,
*ONE AT A TIME*), and things nobody asked for. Somebody carves all of it. The Spared Demon's crack lets out here; the
long way comes up into 860 too.

#### Floor 860: The Carver's Yard

- **New:** road creep.
- **Path:** `↑ 2` up out of the crack → `→ 3` the creeping road, sliding back toward the Rift → `↑ 2` → the landing.
- **Obstacles:** road creep (run against it), road slabs, settling slabs.
- **Monsters:** a demon on the creeping road, a whelp.
- **Set piece and beat:** a road that slides off the spires as he runs up it, carved spires either side. Landing
  carved *860* and *KEEP UP*.
- **Surprise:** the *860* is freshly carved: the chips are still glowing on the landing.
- **Rewind:** carried back into the Rift: rewind and jump the slide.
- **Feel:** the grind of the creeping road under everything.
- **Lines:** Announcer: *"FLOOR EIGHT SIXTY! THE CARVER'S YARD!"* Hero: *"Who carves all these?"*
- **Length:** ~80 s.

#### Floor 859: The Hoist

- **New:** none; set piece: the imps' hoist up through the spires.
- **Path:** `→ 2` → `↑ 4` the hoist: a chain lift up between two spires → `→ 2` chain to chain across a gap → the
  landing.
- **Obstacles:** a chain lift with an imp hacking at its chain, swinging chains (known).
- **Monsters:** sack imps on swings (diamonds), a lobber throwing at the cage, the chain-cutter imp.
- **Set piece and beat:** the imps' hauling route made it up here too. Landing carved *859* and *SACKS UP*.
- **Surprise:** the lobber's lump hits the imps' sack instead: diamonds rain past him.
- **Rewind:** a cut chain dropped the lift: rewind and shoot the cutter first.
- **Feel:** chain rattles, the lift's jolts; push music on the hoist.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-NINE! THE HOIST!"* Hero: *"The imps got up here too. Of course."*
- **Length:** ~85 s.

#### Floor 858: The Leaning Spire

- **New:** none; set piece: the toppling spire.
- **Path:** `↗ 2` → `» 4` a spire topples across the Rift and he runs along its back as it goes (split slabs) → `↑ 2`
  → the landing.
- **Obstacles:** split slabs on the falling spire, settling slabs.
- **Monsters:** two whelps on the spire's back.
- **Set piece and beat:** the spire he's standing on decides to lie down. Landing carved *858* and *TIMBER*.
- **Surprise:** it falls the right way: it ends up as the bridge to the landing.
- **Rewind:** a slab split under him: rewind; the fall is the same every time.
- **Feel:** a long grind and a big shake before it goes; push music on the run.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-EIGHT! TIMBERRR!"* Hero: *"It's leaning. It's leaning. ...It's gone."*
- **Length:** ~80 s.

#### Floor 857: Seam Falls

- **New:** none; set piece: magma falls pouring from the spires' seams.
- **Path:** `→ 3` along a wall of spires pouring magma on a beat → `↑ 3` up behind the falls → the landing.
- **Obstacles:** lava falls (the seams pouring), vents below, settling slabs.
- **Monsters:** a lobber on the top.
- **Set piece and beat:** the spires bleed. Landing carved *857*, behind the last fall.
- **Surprise:** when the last fall stops, the Carver's chisel marks are behind it: he carved a door there, then gave
  up.
- **Rewind:** a fall poured on him: rewind, count the beat.
- **Feel:** loud, wet roar; slow motion when he slips through a fall.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-SEVEN! SEAM FALLS!"* Hero: *"Wait for it. ...Again."*
- **Length:** ~85 s.

#### Floor 856: The Nest

- **New:** whelp nests.
- **Path:** `↑ 2` → `→ 3` a road with nests in its cracks (the first nest alone) → `↑ 2` → the landing.
- **Obstacles:** road slabs, split slabs.
- **Monsters:** whelp nests and their whelps; a demon at the top.
- **Set piece and beat:** where the whelps come from. Shoot the crack shut and it stops. Landing carved *856* and
  *NO VACANCY*.
- **Surprise:** the last nest spits out one whelp, looks at him, and seals itself.
- **Rewind:** swarmed: rewind and shoot the nest first.
- **Feel:** chittering; a small shake as a nest seals.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-SIX! THE NEST!"* Hero: *"So that's where they come from."*
- **Length:** ~85 s.

#### Floor 855: Sign Language

- **New:** none; set piece: Hell's loop in the carvings.
- **Path:** `→ 3` a run of slabs sinking on the loop's beat past a carved *YOU ARE HERE* → `→ 3` the same run again →
  `↑ 2` → the landing.
- **Obstacles:** loop stones that settle on the level's clock, a vent on the beat.
- **Monsters:** one whelp on the second lap, at the same moment as the first.
- **Set piece and beat:** the Carver's favourite joke: *YOU ARE HERE*, three times, then *(STILL)*. Landing carved *855*.
- **Surprise:** under the last carving: *THIS WAY UP*, with an arrow pointing down.
- **Rewind:** the loop is exact.
- **Feel:** a skipped-record whoosh on the repeat.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-FIVE! SIGN LANGUAGE!"* Hero: *"I've read this one."*
- **Length:** ~85 s.

#### Floor 854: The Quarry

- **New:** none; set piece: the Carver's quarry.
- **Path:** `→ 2` cut slabs on creeping road → `↑ 4` rising slabs up the quarry face → `→ 1` the landing.
- **Obstacles:** road creep carrying cut slabs, rising slabs, split slabs.
- **Monsters:** demons in the quarry, a lobber.
- **Set piece and beat:** where the road's slabs are cut from the spires. Landing carved *854* and *CUT HERE*.
- **Surprise:** one of the cut slabs has a half-finished carving on it: a face. It looks a lot like the hero.
- **Rewind:** carried off the end: rewind and ride the rise.
- **Feel:** grinding and chiselling sounds; a medium shake as a slab heaves.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-FOUR! THE QUARRY!"* Hero: *"That's my face. Why is that my face."*
- **Length:** ~85 s.

#### Floor 853: Dust

- **New:** none; set piece: the dark of the carving dust.
- **Path:** `↑ 2` → `→ 3` the dust: a stretch where the carving dust swallows the light → `↗ 2` → the landing.
- **Obstacles:** spire shards and vents (their glows are the light), split slabs.
- **Monsters:** whelps in the dust.
- **Set piece and beat:** the dust from all that carving, thick enough to swallow the light. Landing carved *853*.
- **Surprise:** out of the dust, the great spire's seams pulse, once.
- **Rewind:** a shard he didn't see: rewind; its glow comes first.
- **Feel:** muffled roar; the shards' grit loud in the dark.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-THREE! DUST!"* Hero: *"Can't see a thing. Hell's improving."*
- **Length:** ~80 s.

#### Floor 852: The Gallery

- **New:** none; set piece: the Carver's statues.
- **Path:** `↑ 2` → `■` the gallery: carved demons along a terrace, the camera holds until the real ones fall → `→ 2`
  → the landing.
- **Obstacles:** road slabs, settling slabs.
- **Monsters:** demons and a lobber hidden among the statues (four at most).
- **Set piece and beat:** a terrace of carved demons, the Carver's best work. Some of them aren't carved. Landing
  carved *852* and *DO NOT TOUCH*.
- **Surprise:** the statue at the end wakes up last.
- **Rewind:** a statue that breathed: rewind and shoot it first.
- **Feel:** stone grinding as each one moves.
- **Lines:** Announcer: *"FLOOR EIGHT FIFTY-TWO! THE GALLERY!"* Hero: *"Nice work. Shame about the subject."*
- **Length:** ~95 s.

#### Floor 851: The Carver's Spire (floor boss)

- **New:** the floor boss, **the Carver**.
- **Path:** `■` tier 1, his workshop at a spire's foot → `↑ 2` → `■` tier 2, the scaffold up the spire → the landing
  and the Hollow Spire's door.
- **Obstacles:** road and split slabs on the tiers.
- **Monsters:** the Carver and whelps on tier 2 (two at most).
- **Set piece and beat:** the demon who carved every number the hero has read since 900, caught carving *851*. Beaten,
  he drops his chisel; the spire he was hollowing stands open: **the Hollow Spire** (shortcut 2).
- **Surprise:** before the fight he finishes the *1* of *851*. Properly.
- **Rewind:** his chisel lines glow first and come the same each time.
- **Feel:** a medium shake per chisel strike; slow motion as he falls.
- **Lines:** see the boss. Defeat, announcer: *"THE CARVER IS CHISELLED OUT!"*; hero: *"Signed it, too."*
- **Length:** ~130 s.

---

#### Stretch 3: the Needle Stair (Floors 810-802)

The road ends at the foot of the great spire and climbs it as a stair cut round its outside: shards, vents and the
whole toolkit, the spire humming louder floor by floor, up to the crack in its side where the Spire Fiend waits.

#### Floor 810: The Needle's Foot

- **New:** none; the stair begins.
- **Path:** `↗ 3` the stair round the spire → `→ 1` a turn → `↖ 3` the stair on → the landing.
- **Obstacles:** road and split slabs as stairs, settling slabs at the foot.
- **Monsters:** a demon and a whelp on the turns.
- **Set piece and beat:** the great spire close enough to touch, the stair spiralling up it. If he came up **the Hollow
  Spire**, shards fall from the first second and the Rift rises faster, and the announcer says why; by the long way the
  floor is as written. Landing carved *810* and *TO THE SPIRE*.
- **Surprise:** at the turn the spire hums, and every shard on the road trembles.
- **Rewind:** a stair slab that split: rewind and take the stair at a run.
- **Feel:** the hum under everything.
- **Lines:** Announcer: *"FLOOR EIGHT TEN! THE NEEDLE STAIR!"* Hero: *"Big spire. I'm going up it, aren't I."*
  Hollow Spire taken, announcer: *"THE SPIRE CAME DOWN BEHIND HIM! NOW EVERYTHING'S FALLING!"*
- **Length:** ~80 s.

#### Floor 809: Updraft Round the Spire

- **New:** none; set piece: the heat rising round the spire.
- **Path:** `↑ 2` → `→ 2` column to column round the spire → `↑ 3` one tall updraft → the landing.
- **Obstacles:** heat updrafts (known), split slabs between them.
- **Monsters:** a lobber throwing into the updrafts.
- **Set piece and beat:** the spire's own heat carrying him up its side. Landing carved *809*.
- **Surprise:** the lobber's lump rides the updraft straight back up into it.
- **Rewind:** knocked out of a column: rewind and wait for the throw.
- **Feel:** whoosh in the columns; a planned slow hang at the top.
- **Lines:** Announcer: *"FLOOR EIGHT OH NINE! UPDRAFT!"* Hero: *"Hot air. Familiar."*
- **Length:** ~80 s.

#### Floor 808: The Creeping Stair

- **New:** none; set piece: the stair sliding off the spire.
- **Path:** `↗ 2` → `→ 3` creeping stair against its slide → `↑ 2` → the landing.
- **Obstacles:** road creep, split slabs, rising lava (the Rift) in the last push.
- **Monsters:** whelps riding the creep down at him.
- **Set piece and beat:** the stair itself slipping off the spire. Landing carved *808* and *NOT THAT WAY*.
- **Surprise:** a whelp rides the creep all the way down into the Rift, waving.
- **Rewind:** carried back: rewind and jump the slide.
- **Feel:** grinding; the roar rising with the Rift.
- **Lines:** Announcer: *"FLOOR EIGHT OH EIGHT! THE CREEPING STAIR!"* Hero: *"Stairs going down. Me going up."*
- **Length:** ~85 s.

#### Floor 807: The Choir

- **New:** none; set piece: the vents as a choir.
- **Path:** `→ 4` a terrace of vents blowing in one exact pattern → `↑ 2` → the landing.
- **Obstacles:** brimstone vents in a loop, split slabs.
- **Monsters:** none: the vents are the floor.
- **Set piece and beat:** a row of vents round the spire that sing (blow) in the same order, every time. Landing
  carved *807* and *ENCORE*.
- **Surprise:** the last vent is a beat late, every lap.
- **Rewind:** the pattern is exact: rewind and see the gap.
- **Feel:** each vent a different note of roar.
- **Lines:** Announcer: *"FLOOR EIGHT OH SEVEN! THE CHOIR!"* Hero: *"Same song. I know the words."*
- **Length:** ~75 s.

#### Floor 806: The Overhang

- **New:** none; set piece: under the spire's overhang.
- **Path:** `↑ 2` → `← 3` under an overhang shedding shards → `↑ 2` → the landing.
- **Obstacles:** spire shards, settling slabs, split slabs.
- **Monsters:** a demon at the end of the overhang, a whelp.
- **Set piece and beat:** a lip of spire sticking out over the stair, shedding. Landing carved *806*.
- **Surprise:** the overhang's last shard is huge, and it misses.
- **Rewind:** a shard he misread: rewind.
- **Feel:** grit hissing; a big shake for the last shard.
- **Lines:** Announcer: *"FLOOR EIGHT OH SIX! THE OVERHANG!"* Hero: *"Mind your head. Mine, specifically."*
- **Length:** ~80 s.

#### Floor 805: The Breakaway

- **New:** none; set piece: a chase.
- **Path:** `» 4` a stretch of stair breaks away and the Rift surges after him → `↑ 3` a rising slab out of it → the
  landing.
- **Obstacles:** split slabs in a chase, the surge, a rising slab.
- **Monsters:** none until the breather; then a whelp.
- **Set piece and beat:** the floor's one sprint. Landing carved *805*.
- **Surprise:** the rising slab at the end takes him up just as the surge arrives.
- **Rewind:** caught: rewind; the break is the same every time.
- **Feel:** big shake as it breaks; push music; silence on the landing.
- **Lines:** Announcer: *"FLOOR EIGHT OH FIVE! RUN!"* Hero: *"I hate this spire now. Specifically."*
- **Length:** ~75 s.

#### Floor 804: The Turn

- **New:** none; set piece: demons holding the turns.
- **Path:** `↑ 2` → `■` the turn: demons and a lobber hold the stair's turn until they fall → `↗ 3` → the landing.
- **Obstacles:** road and settling slabs, shards.
- **Monsters:** two demons, a lobber and a whelp (four at most).
- **Set piece and beat:** the spire's guard at its best. Landing carved *804*.
- **Surprise:** the lobber throws, the demon below breathes into the lump, and it bursts over them both.
- **Rewind:** a fan he jumped into: rewind and read it.
- **Feel:** slow motion on the closest fan.
- **Lines:** Announcer: *"FLOOR EIGHT OH FOUR! THEY'RE HOLDING THE TURN!"* Hero: *"Excuse me. Coming through."*
- **Length:** ~90 s.

#### Floor 803: The Last Heave

- **New:** none; set piece: the last rise.
- **Path:** `↑ 5` rising slabs up the spire's face with lobbers on its ledges and shards falling → the landing.
- **Obstacles:** rising slabs, shards.
- **Monsters:** two lobbers.
- **Set piece and beat:** the spire's crown fills the top of the sky; the hum is in his teeth. Landing carved *803*.
- **Surprise:** at the top the spire's seams flare and the hum stops. Somebody up there is listening.
- **Rewind:** a lump knocked him off a slab: rewind.
- **Feel:** the hum getting louder; a medium shake as each slab heaves.
- **Lines:** Announcer: *"FLOOR EIGHT OH THREE!"* Hero: *"It stopped humming. That's worse."*
- **Length:** ~80 s.

#### Floor 802: The Spire Door

- **New:** none; set piece: the entrance.
- **Path:** `↗ 2` slabs → `→ 2` a terrace round the spire → `↑ 1` → `■` the crack in the great spire's side.
- **Obstacles:** a short climb of split slabs, a demon on the terrace, then quiet.
- **Monsters:** a demon and a whelp, then nothing.
- **Set piece and beat:** a crack in the great spire's side, tall as a door, carved *THE SPIRE*; shards trickle out of
  it and the spire grinds. The camera stops; the Grudge pick; the counter ticks to 801. Floor 801, the Spire Fiend's
  arena, is through it (k_e16304d7a504). Landing carved *802*.
- **Surprise:** a damned soul sits beside the crack, carving tally marks, forever. There are a lot of them.
- **Rewind:** a fan on the terrace: rewind and read it.
- **Feel:** the roar dies to nothing; the grind of the spire; the music drops out.
- **Lines:** Announcer: *"FLOOR EIGHT OH TWO! THE SPIRE FIEND AWAITS!"* Hero: *"Big door. Bigger spire."*
- **Length:** ~75 s.

---

### Floor bosses of Circle IX

Climbing arenas as in section 5: two tiers like the Foreman and the Keeper (phase 1 on the bottom tier, at half health
the tier gives way and the camera moves on, phase 2 on the top tier), every attack after a glow of 1 s or more on its
spot, at most four enemies counting the boss. Each gives a diamond burst and stands beside the way on.

#### The Road Breaker (Floor 891)

- **Concept:** the biggest Brimstone Demon on the road, a slab of road for a club: he keeps the road broken so nothing
  climbs it. Slow, proud of every crack.
- **Attacks:** *Tier 1:* slams the club down and a crack runs along the road (a row of glows, each bursting 1.1 s
  later where it glowed), and kicks a lump of road at the hero's spot (a glow, the lump 1.1 s later). At half health he
  slams so hard his own tier breaks, the road gives way and the camera runs on to tier 2. *Tier 2:* the same slams,
  and whelps crawl out of his cracks (two at most).
- **Lines:** Breaker: *"Road's closed."* Hero: *"I'll walk on the cracks, then."* At half: *"You broke... my road!"*
  Defeat: *"Who... fixes... roads..."* Voice `roadBreaker`: deep, gravelly, slow, proud (Story.md).
- **Art:** the demon's sheets scaled up and darkened until his own are made.

#### The Carver (Floor 851)

- **Concept:** a gaunt demon with a chisel for a hand who carves everything on the road: the floor numbers, the rules,
  the jokes. Fussy, offended by the hero's footprints.
- **Attacks:** *Tier 1:* chisel strikes along the tier (a row of glows, each bursting 1.1 s later) and a shard knocked
  loose over the hero's spot (a glow, the shard 1.1 s later). At half health he climbs his scaffold; tier 2: the same,
  and whelps from the cracks he chiselled (two at most).
- **Lines:** Carver: *"Don't. Touch. The carvings."* Hero: *"Wasn't going to."* At half: *"You SMUDGED it."*
  Defeat: *"My... masterpiece..."* Voice `carver`: thin, fussy, precise, offended (Story.md).
- **Art:** the demon's sheets, thinner and paler, until his own are made.

## Building it

The old 10-floor stages (`travelDirection` Right; once Levels 2-11, now only the long way's stand-ins, levels 39-46)
are replaced by the floors above (the played route: done 2026-10-19). The work, in the
order it can be built and played:

1. **Engine (C#, new rewind-safe types):** sinking stones, crumbling stones, rafts (bob, drift, tip), chain lifts and
   counterweights, heat updrafts, lava falls, a geyser column, and **rising lava** (a kill plane with a telegraphed rise
   that stops under marked ledges), plus a climbing floor layout (`world.camera` Travel Up with hand-placed and
   generated footholds) and the climbing arena (tiers, a camera step per phase). *Built 2026-10-18:* the path camera
   (`world.path`, Arena sections for the climbing arena's tiers), the lava sea and the world below it, every piece in
   the toolkit table marked *yes*, generated sections from the Circle's chunks, and the bot climbing a path (the `climb`
   playtest), and the geyser column (`lavaGeyser`, ability `geyser`, its painted column). Still to build: the sack on the counterweight, the imp riding a swing, a screen-space
   heat distortion shader (today the shimmer is drawn bands). `offerShortcut` with a diamond cost
   (`costDiamonds`), and a trigger that a floor can read when a shortcut was taken to reach it (`shortcutTaken`, from
   `ShortcutProgress`). Grudge picks on every 5th floor and after bosses. **The damned soul** (`damnedSoul`, a new
   entity: harmless, no health, a looping idle, a proximity trigger that drops one `rewindOrb` once per play through
   `GameplayWorld.TryGrantOnce`), and the `rewindOrb` drop taken off `imp` (today `imp.json` drops one 20% of the
   time; imps aren't damned). **For the set pieces and the feel** (*Set pieces, surprises and the feel*): the cooling
   crusts on a lava fall (903), the dark (907: a darkness overlay lit by the lava, telegraph glows, cinders and shots),
   the cart's ride on a slag flow and its jump off the chute's end (908), the King's glow (a backdrop layer sized and
   lit by the floor number), an ember carried by an updraft (957's surprise), a **near-miss trigger** for the slow
   motion (a shot, column, lump or wave passing close without a hit, or a jump off a piece in its last instant;
   presentation only, rate-limited), and **the lava's roar** (a looping bed whose loudness and brightness follow the
   distance from the hero to the lava's surface, and its sound in `audio.json`). Screen shake (`cameraShake`) and slow
   motion (`slowMotion`) already exist as script actions.
2. **The playtest bot learns to climb:** jump between moving footholds and keep moving off sinking stones (today's
   bot walks right; the old Slag Falls stage waited on this).
3. **Floors 1000-991 first** (the stretch J sees first), one level file per floor, in climb order in game.json, each
   leading to the next (`nextLevelNumber`); its playtest is `level-<n>` per floor and `story-path`. *Built 2026-10-18:
   Floors 1000-996* (`levels/Floor<floor>.json`, levels 2-6; the `landing-stretch` playtest plays them in a row).
   How a floor level is set up: `floors.start` its floor, `floors.end` the floor above (the Shaft climbs to it),
   `unitsPerFloor` 0, the landing's beat `setFloor` to the floor above with `skipMilestones` (the next floor's title
   calls a milestone itself: Floor 999 opens on *"ONLY 999 TO GO"*), and the Grudge pick before `completeLevel`. Built
   with today's pieces, so some beats are simpler than the pages above: the fleeing imp on 1000 climbs straight up out
   of sight (no chain of its own), the village sign on 998 floats as a raft (it doesn't tip over and sink), and the damned
   soul hovers in place (it doesn't climb its three steps). *Built 2026-10-18 too: Floors 995-991* (levels 7-11; the
   `foreman-stretch` playtest plays them in a row). Simplified there: The Loop's laps are the same layout placed twice
   (the stones sink on the level's clock, so both laps move together); the 994 lift that won't stop rides up past the
   landing and comes back down (no jam); the sleeping imp on 993 falls as he comes near, not as he lands; the Foreman
   fights two phases (J's request) instead of three. Floor 991 leads on to the old 10-floor stage at Floor 990 (*The
   Spill*) until the Sack Lift and the long way exist, and Grudges still come on every floor (the rule in *Rewards*, a
   pick every fifth floor, waits until the whole stretch is floors).
4. **The Sack Lift, Floors 960-951, the King's Chimney, Floors 910-901** (Level 11's arena renumbered to Floor 901).
   *Built 2026-10-19* (levels 11-31; the `circle-x-route` playtest plays Floor 991 to Floor 901 taking both shortcuts).
   Engine: `offerShortcut` `costDiamonds` (the Sack Lift's fare, `FARE: 20 DIAMONDS`, stamped *CAN'T AFFORD THE FARE*
   without it) and a risk line for a shortcut with no cost (the Chimney), the `shortcutTaken` trigger (Floor 910 reads
   it: faster lava, the King's line), and a Warden's floor (901: floors 901 to 901) that lights its Waystone and wakes
   the Circle above when cleared, never the floor below it (902). The Pile Keeper fights two tiers like the Foreman
   (tier 1: spear lines and sacks on the hero's spot, each after a 1.1 s glow; at half the mound sinks, tier 2: two
   guards and the same attacks; beaten, his pile bursts and a lava wave chases him round the mound's shoulder to the
   landing and the Chimney); he is drawn as a huge, pale imp guard (the guard's art, scaled up) until his own sheets
   are made. Built with today's pieces, so some beats are simpler than the pages above: the counterweights are pans
   that sink under him and lift their partner (no sacks to shoot off yet: 960, 954, 952, 951, 904); 953's two laps are
   the same layout placed twice with a carved landing between them (*953*, then *953 (AGAIN)*); the cart on 908 stands
   at the chute's head and he rides the slag flow itself; the dark on 907 is a fade as the smoke closes; the crusts on
   903 are crumbling stones and ledges up the face of a fall that pours on its beat; the King's glow in the backdrop
   isn't drawn. Grudges still come on every floor (the every-fifth-floor rule waits for the long way).
5. **The long way:** a generated floor per skipped floor (one level template the generator fills from the floor
   number), with the long-way floor bosses.
6. **Data that follows the renumbering:** Yooba's floor reactions (`ui/yooba.json`: the Spill moves from 985 to 998, the
   Loop from 957 to 995, Halfway's 950 stays on the long way), the Shaft's climb (one floor per level), the playtest
   scenarios that name levels by number (`wastes-climb`, `story-path`, `shortcut`, `waystone`), Circle IX's stages
   (levels 32-38, `CrackedRoad` and `Level13`-`Level18`, recut into floors the same way; the Old Stair's shortcut stays at Floor 871 to 841).
7. **Art (GPU, priced):** *Made 2026-10-18 (see "The art of the climb"):* the world below the lava (sea, haze, spires,
   islands, ceiling), the lava's surface, falls and geysers, the three stones with their steam and crumble frames, the
   village's rafts (cart, roof, sign, hay), the lift, counterweight, chains and the carved landing; *2026-10-19:* the
   lava wave, the heat updrafts and the slag flows (the last pieces that were boxes). Still to make: the
   counterweight sacks, the sack-swing imp and the Pile Keeper (the Imp Foreman's own sheets and the damned soul's
   were made 2026-10-18, `art/impForeman.json`, `art/damnedSoul.json`); for the set pieces, the hay cart's chute ride (908: the cart
   upright with him in it), the sign standing in the lava on 998 (*WELCOME TO THE VILLAGE*, whole), the throne far
   above (four distances: speck, glow, silhouette, close) and the cooling crust. Until they exist, those draw as plain
   shapes and labeled placeholders.
8. **Circle IX** (section 6). *Built 2026-10-19* (levels 32-60; the `cracked-road-route` playtest plays Floor 901 to
   Floor 802 taking both shortcuts). New data pieces, no new C#: `roadSlab`, `settleSlab`, `splitSlab`, `spireShard`
   (+ `shard`), `brimstoneVent`, `risingSlab`, `seesawSlab`, `roadCreep`, `whelpNest`, the monsters `demonWhelp` and
   `demonLobber` (+ `brimstoneLump`), the floor bosses `roadBreaker` and `carver` with their glows, and the Circle's
   `sky` in `content/circles/crackedRoad.json`. The Cracked Road's art is baked (2026-10-19) from `Tools/Art/climb/climb.json`'s
   `roadSlab` (also `risingSlab` and `seesawSlab`), `splitSlab` (crack, fall), `settleSlab` (sink; the loop slabs too),
   `rift` (the sky's `seaArt`: the Rift's crack web far below), `roadSpires` and `roadIsland` (the sky's spires and
   floating road): `Assets/Art/Climb/RoadSlab_*`, `SplitSlab_*`, `SettleSlab_*`, `Rift_loop`, `RoadSpires_still`,
   `RoadIsland_still`. Built
   with today's pieces, so some beats are simpler than the pages: the great spire isn't drawn; the toppling spire (858)
   is a collapse chase along a row of split slabs; the dust on 853 is a fade like 907's dark; 855's laps are the same
   layout placed twice (as 995); the shard that stands up on 898 and the vent's whelp on 896 are spawned pieces; the
   statues on 852 are demons standing still until the hold starts. Floor 802 leads to the level select until Floor 801
   exists.
