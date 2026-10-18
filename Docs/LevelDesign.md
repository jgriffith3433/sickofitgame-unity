# Sick Of It: Level Design

How a floor of Hell is built. [Story.md](Story.md) says what Hell is and who lives where; this page says how a floor
plays, and agents build floors from it. Every floor here must fit the story; if a floor needs something the story
doesn't explain, add it to Story.md first.

Status: **design for review** (2026-10-18, J's decision m_daf635ad1287: one level = one floor, a floor boss every 10th
floor, the Warden on the Circle's top floor, shortcuts skip stretches). Nothing on this page is built yet: today's
Levels 2-11 are the old 10-floor stages (flat walks to the right) and are replaced floor by floor from this page (see
*Building it*). Names marked *(working)* are placeholders. Lines are first drafts for the voices.

## The rules in one breath

- **One level is one floor.** 60-120 seconds, always climbing up, never a walk to the right.
- **A floor boss every 10th floor** (the floors ending in 1: 991, 981, ... 911), **the Warden on the Circle's top
  floor** (901, 801, ... 101).
- **Hell's shortcuts skip stretches** of floors at a cost or a risk, so 1,000 floors stay playable in hours.
- **One new idea per floor**: one new obstacle *or* one new monster, never both. Everything else on the floor is
  something the player already knows.
- **Every floor is a place** with a name, a set piece and a story beat, its number carved into its landing.

## 1. The climb grammar

### How a floor reads

Every floor is the same sentence with different words, so the player always knows where they are in it:

1. **The ledge.** The floor opens on a ledge of black rock just above the **lava sea**, the glowing ocean at the
   bottom of every floor of the Wastes. The camera is low; the lava fills the bottom of the screen. The announcer
   calls the floor (*"FLOOR NINE HUNDRED NINETY-EIGHT! THE SPILL!"*), the hero says one line, and nothing attacks
   for the first **5 seconds** (title, line, room to move).
2. **The climb.** About **6 to 10 screens up**. The camera only goes up (it follows the hero up and never back down;
   whatever drops below the screen's bottom edge is gone, into the lava). The way up is the floor's obstacles: stones,
   rafts, lifts, updrafts, ledges. The background is the world falling away below: the lava sea, the rock walls, chains
   and the imps' lifts hanging in the dark, and the next floor's light far above.
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

## 2. The toolkit

Every floor of the Wastes is built from these pieces. Each one is introduced on one floor (the floor in the last
column) and reused after that. *Engine* says whether the game can do it today; everything new is a rewind-safe
platform or hazard type (saved in `SaveState`/`LoadState`, seeded) for the floor-building task.

| Piece | What it does | Why it's in Hell | Telegraph | First floor | Engine |
|---|---|---|---|---|---|
| **Sinking stones** | Slabs of cooled slag floating on the lava. Stand on one and it sinks (about 1.5 s to go under); step off and it bobs back up. Keep moving. | The lava sea's crust, broken into floes | The stone dips and hisses the moment he lands | 1000 | new |
| **Rising lava** | The lava sea climbs the screen, slowly, behind the hero: the push upward. Stops below each breather ledge and the landing. | The Wastes breathing in | A rumble, a line of bubbles across the screen, 2 s ahead; the announcer the first time | 999 | new |
| **Bobbing debris rafts** | Bigger floating things (the village's carts, roofs, fences) bob on their own rhythm and drift sideways. They don't sink when stood on; they tip if he stands at the edge. | Everything that fell through the portal ends up in the lava | They rock before they tip | 998 (as sinking debris), 959 (bobbing and drifting) | new |
| **Imp sack-swings** | Imps swinging across the gap on chains, carrying the village's diamond sacks. Hit the sack: diamonds. Touch the imp: a burn. Their swing is a rhythm he can ride over or shoot through. | The imps haul the diamonds up through the Wastes to the gate | The chain rattles before each swing | 997 | new (an `impSack` on a pendulum path) |
| **Lava geysers** | A vent in the rock blows a column of lava straight up. | The lava sea under pressure | The vent glows and spits for **1 s** before | 996 | partly (`lavaBurst` is the glow and burst; the column is new) |
| **Hell's loop** | A set of stones, geysers or imps that does exactly the same thing over and over. Not a new object: a new way to use the old ones. | Hell is repetition | The first repeat is called out | 995 | config (seeded patterns) |
| **Chain lifts** | A platform on a chain that rides up and down a shaft on a timer. | The imps' lifts for hauling sacks | The chain clanks and the lift jolts before it moves | 994 | new |
| **Crumbling stones** | Ledges of old slag that crack when stood on and fall after 1 s. They don't come back. | The Wastes' old floors, burned through | Cracks and dust the moment he lands | 992 | new |
| **Counterweights** | Two platforms on one chain over a pulley, one loaded with a diamond sack. Shoot the sack off and its side rises (with him on it). | The imps balance their lifts with the stolen sacks | The loaded side creaks, the sack sways | 960 | new |
| **Heat updrafts** | Shimmering columns of hot air over the lava that carry him up while he's in them (and keep carrying a jump). | The lava sea's heat | Shimmer and floating ash show the column | 957 | new |
| **Falling slag** | Lumps of molten rock dropping from the ceiling. | The floor above is melting | A glowing drip gathers for 1.2 s | 956 | yes (`slagDrip`, `slag`) |
| **Lava falls** | Curtains of lava pouring down the wall. They pour, thin to a trickle and stop on a beat, then pour again: pass while they're off. | The floor above spills over | The fall thins and sputters 1 s before it stops, and gurgles 1 s before it pours | 955 | new |
| **Ember shower** | Steam off the lava sea raining embers two at a time. | The lava sea's steam | The hero's warning line, 1.5 s before the first | 909 | yes (`emberShower`) |

Monsters the floors draw from (Circle X's one family: the Overlord's imps, and the sparks of the burning village):

| Monster | What it does | Why it's here | First floor |
|---|---|---|---|
| `impSack` (on a swing) | Swings across with a diamond sack; hit the sack, diamonds spill | Hauling the village's diamonds up to the gate | 997 |
| `cinder` | Drifts toward him and pops | A spark of the burning village that fell through and came alive | 998 |
| `imp` | Perches on a ledge above and throws slow embers down | The Overlord's raiders, guarding the way up | 993 |
| `cinderThatch` | A burning clump of thatch; shot, it bursts into two darting sparks | The village's roofs fell through too | 958 |
| `impGuard` | Plants itself on a ledge and throws three embers in a fan; drops nothing | The Magma King's own guard, keeping his pile and his stairs | 954 |

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

Each page: the place, the one new idea, the obstacles (known pieces), the monsters and why, the set piece and story
beat, the lines (announcer = Hell's show, uppercase; hero = deadpan, short), and the rough length.

---

### Stretch 1: the Landing stretch (Floors 1000-991)

Where the portal spits out everything that falls through: the lava sea, the village's wreckage floating on it, and the
imps hauling the stolen diamonds up out of it. The stretch teaches the toolkit one piece a floor and ends on the imp
who runs the hauling.

#### Floor 1000: The Landing

- **New:** sinking stones.
- **Obstacles:** a path of sinking stones up a slope of floating crust, then solid ledges; nothing else.
- **Monsters:** none to fight. At the landing an imp with a sack sees him, drops its ember in surprise (it hisses into
  the lava) and flees up a chain. It is the first imp he meets and it tells him where the diamonds went.
- **Set piece and beat:** he lands where Video2 dropped him, on a rock in the lava river. The first stone he stands on
  sinks under him: keep moving. The slope of floes is long and gentle, a lesson with no punishment but a hot bath.
  Landing carved *1000* and *WELCOME*.
- **Lines:** Announcer: *"FLOOR ONE THOUSAND! THE LANDING!"* Hero (first stone sinks): *"Even the floor wants me
  gone."* At the imp: *"Hey. That's ours."*
- **Length:** ~60 s.

#### Floor 999: Only 999 To Go

- **New:** rising lava.
- **Obstacles:** sinking stones and solid ledges, with the lava rising behind him for the first time.
- **Monsters:** none: the lava is the enemy on this floor.
- **Set piece and beat:** the milestone. Halfway up, the lava rumbles, bubbles run across the screen, and it starts to
  climb. It is slow; it stops under each breather ledge. The landing carved *999* has *ONLY* scratched in front of it.
- **Lines:** Announcer (title): *"FLOOR NINE HUNDRED NINETY-NINE! ONLY NINE HUNDRED NINETY-NINE TO GO!"* Hero:
  *"You said that already."* Lava starts: announcer *"THE LAVA'S RISING! CLIMB, LITTLE MAN!"*; hero: *"It's
  following me. Of course."*
- **Length:** ~75 s.

#### Floor 998: The Spill

- **New:** the cinder.
- **Obstacles:** the village's wreckage floating on the lava, behaving like sinking stones (fence posts, cart planks,
  the baker's cart in pieces, hay bales); slow rising lava in the last push.
- **Monsters:** cinders, the sparks of the burning village that fell through with him. The first comes alone out of
  the burning hay cart halfway up and drifts after him; later two at a time.
- **Set piece and beat:** his own village floating past. The village sign, *WELCOME TO THE VILLAGE*, sticks out of the
  lava where he lands; a burning hay cart drifts by, and the sparks in it come alive. Landing carved *998* on a
  half-sunk cart wheel.
- **Lines:** Announcer: *"FLOOR NINE HUNDRED NINETY-EIGHT! THE SPILL!"* Hero (at the sign): *"That was the baker's
  cart."* First cinder: *"The hay's following me now."*
- **Length:** ~80 s.

#### Floor 997: Imp Run

- **New:** imp sack-swings.
- **Obstacles:** a tall gap crossed by chains; sinking stones and ledges between the swings.
- **Monsters:** sack imps on swings, hauling the village's diamond sacks across the gap to the lift on the far side.
  Each hit tears the sack and spills a diamond; they don't throw (too busy carrying). The first swings alone, slowly;
  then two in rhythm.
- **Set piece and beat:** the imps' hauling route. Dropped sacks on the ledges, a carved *IMPS ONLY*, and diamonds
  raining into the lava when he hits a sack. The first floor where diamonds really pour.
- **Lines:** Announcer: *"FLOOR NINE NINETY-SEVEN! IMP RUN!"* Hero: *"Those are ours."* At the breather: *"One less
  sack for his gate."*
- **Length:** ~85 s.

#### Floor 996: The Slag Falls

- **New:** lava geysers.
- **Obstacles:** ledges along the foot of the great lava falls (scenery behind, they don't hurt here), geyser vents
  between the ledges, sinking stones over the pools.
- **Monsters:** one sack swing near the top, for the diamonds; otherwise none, the geysers are the floor.
- **Set piece and beat:** the climb beside the falls, the whole screen orange. The first vent glows for a full second
  and blows a column right in front of him with nothing else around. Landing carved *996* and *MIND THE SPOUT*.
- **Lines:** Announcer: *"FLOOR NINE NINETY-SIX! THE SLAG FALLS!"* Hero (first geyser): *"The floor sneezed."*
- **Length:** ~80 s.

#### Floor 995: The Loop

- **New:** Hell's loop (the same pattern, again and again).
- **Obstacles:** a run of sinking stones that sink by themselves in the same order, over and over, with a geyser at the
  same spot each time; the lava rises behind.
- **Monsters:** one cinder, drifting in at the same moment every lap.
- **Set piece and beat:** the floor that explains rewind. The pattern is unfair the first time (the stones sink a beat
  before he expects) and easy once he's seen it, and the floor practically asks him to drag it back. Carved on the
  rock, three times, the same words: *YOU ARE HERE. AGAIN.* Landing carved *995*.
- **Lines:** Announcer: *"FLOOR NINE NINETY-FIVE! THE LOOP!"* Hero (second lap): *"It's doing that on purpose."*,
  then *"Hell runs on a loop. So I can drag it back."* Landing, announcer: *"THE LOOP: SURVIVED. AGAIN."*
- **Length:** ~90 s.

#### Floor 994: Chain Lift

- **New:** chain lifts.
- **Obstacles:** two lifts up a sheer shaft, with sinking stones at the bottom and ledges between the lifts.
- **Monsters:** imps at the pulleys above, hacking at the chains with their claws to drop the lift (and him) into the
  lava: shoot them and the lift keeps going. They don't throw: the chain is their target. One at the first lift, two
  at the second.
- **Set piece and beat:** a long ride up a shaft of hanging chains and swinging sacks, the lava far below; the chain
  sparks where the imps cut it, the lift jolts and drops a notch when they get a cut in. Landing carved *994*.
- **Lines:** Announcer: *"FLOOR NINE NINETY-FOUR! THE CHAIN LIFT!"* Hero (first jolt): *"Don't. Don't do that."*
- **Length:** ~90 s.

#### Floor 993: Ember Ledges

- **New:** the imp that throws (plain `imp`).
- **Obstacles:** sinking stones, a chain lift, solid ledges: everything known.
- **Monsters:** imps perched on ledges above, the Overlord's raiders guarding the way up, throwing slow embers down at
  him. The first alone on a high ledge; then two, one each side; never three while the lava rises.
- **Set piece and beat:** the first imps that fight back. A ledge of their sleeping hollows and a heap of embers they
  throw from. Landing carved *993* with *GO BACK* scratched under it.
- **Lines:** Announcer: *"FLOOR NINE NINETY-THREE! EMBER LEDGES!"* Hero (first throw): *"Oh, this one throws."*
- **Length:** ~85 s.

#### Floor 992: Bad Footing

- **New:** crumbling stones.
- **Obstacles:** crumbling ledges up the wall, sinking stones at the bottom, rising lava in the second push.
- **Monsters:** a cinder pair in the first push, an imp throwing from the top.
- **Set piece and beat:** the old floor of the Wastes, burned through: the ledges crack under him and fall into the
  lava behind, so the way down is gone the moment he uses it. The floor before the boss: the hardest so far. Landing
  carved *992* with a crack through it.
- **Lines:** Announcer: *"FLOOR NINE NINETY-TWO! BAD FOOTING!"* Hero (first crumble): *"Nothing down here holds."*
- **Length:** ~90 s.

#### Floor 991: The Foreman's Pile (floor boss)

- **New:** the floor boss, **the Imp Foreman** (see *Floor bosses*).
- **Obstacles:** a climbing arena of three tiers over the lava: sinking stones, a chain lift between tiers.
- **Monsters:** the Foreman and the sack imps he whips along (at most two).
- **Set piece and beat:** the top of the hauling route, where the imps load the sacks into their lift. The Foreman
  sits on the biggest sack of all. Beat him and his sack bursts: a shower of diamonds. His lift stands open on the
  landing: **the Sack Lift** (shortcut 1). Landing carved *991*.
- **Lines:** Announcer: *"FLOOR NINE NINETY-ONE! THE IMP FOREMAN!"* Hero: *"That's the biggest sack yet. Ours too,
  I bet."* Defeat, announcer: *"THE FOREMAN IS DOWN! HIS SACK, HOWEVER, IS UP FOR GRABS!"*; hero: *"Thank you."*
- **Length:** ~120 s.

---

### Stretch 2: the Depot stretch (Floors 960-951)

Where the imps' lifts let out: their depot of chains and counterweights, the rest of the village drifting through, and
the Magma King's guard taking over from the haulers. The Sack Lift lands here; the long way comes up into 960 too.

#### Floor 960: The Depot

- **New:** counterweights.
- **Obstacles:** paired platforms on chains over pulleys, each pair balanced with a diamond sack: shoot the sack off
  and his side rises. Sinking stones at the bottom.
- **Monsters:** sack imps swinging between the pulleys (they reload the counterweights).
- **Set piece and beat:** the imps' depot, a forest of chains, sacks hanging as weights. Shooting a sack off spills it
  (diamonds) and lifts him: the floor pays for itself. Landing carved *960* and *SACKS GO UP*.
- **Lines:** Announcer: *"FLOOR NINE SIXTY! THE DEPOT!"* Hero: *"They're using our diamonds as weights."*
- **Length:** ~85 s.

#### Floor 959: The Rest of the Village

- **New:** bobbing debris rafts (that bob and drift, not sink).
- **Obstacles:** big rafts drifting sideways across the lava (a whole roof, the well's bucket and frame, a fence still
  standing on its patch of meadow), tipping at the edges; sinking stones between them.
- **Monsters:** cinders drifting off the burning roofs, two at most.
- **Set piece and beat:** more of the village than he wanted to see: the rafts drift in a slow procession, and he rides
  them up a lava river that climbs in steps. Landing carved *959* on a raft of turf with the meadow's grass still on it.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-NINE! THE REST OF THE VILLAGE!"* Hero (on the roof): *"I used to sit
  under this."*
- **Length:** ~90 s.

#### Floor 958: The Burning Thatch

- **New:** the thatch cinder.
- **Obstacles:** rafts and sinking stones, an updraft-free climb (keep it plain: the new monster is the idea).
- **Monsters:** thatch cinders, the village's roofs come alive. The first one alone, slowly, in a quiet stretch; shot,
  it bursts into two sparks that dart for two seconds. Later one thatch cinder with a plain cinder.
- **Set piece and beat:** a drift of burning roofs; one shakes itself loose and comes after him. Landing carved *958*.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-EIGHT! THE BURNING THATCH!"* Hero (first burst): *"That was somebody's
  roof."*
- **Length:** ~85 s.

#### Floor 957: Hot Air

- **New:** heat updrafts.
- **Obstacles:** shimmering columns over the lava that carry him up, with crumbling ledges between them and sinking
  stones at the foot.
- **Monsters:** one imp on a high ledge throwing into the updrafts.
- **Set piece and beat:** the floor where he flies a little. The tallest climb so far, done mostly in the air, ash
  floating up beside him. Landing carved *957*.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-SEVEN! HOT AIR!"* Hero (first updraft): *"Huh. That's actually nice."*,
  then, a beat later: *"No. It's still Hell."*
- **Length:** ~80 s.

#### Floor 956: The Drip

- **New:** falling slag.
- **Obstacles:** a climb under a melting ceiling: chain lifts and ledges, slag dripping from above; the first three
  drips come one by one while he stands still.
- **Monsters:** sack imps on swings in the second push (shoot them between the drips).
- **Set piece and beat:** the floor above is melting through. Landing carved *956* and *MIND THE DRIP*.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-SIX! THE DRIP!"* Hero: *"The ceiling's melting. Great."*
- **Length:** ~85 s.

#### Floor 955: The Falls

- **New:** lava falls (curtains that pour and stop on a beat).
- **Obstacles:** a wall of lava falls, each on its own beat, with ledges behind and between them; geysers below.
- **Monsters:** cinders that drift out of the falls (they live in the spray), one at a time.
- **Set piece and beat:** climbing up through the falls themselves, slipping through each one when it thins. Landing
  carved *955*, half hidden behind a fall that stops just as he arrives.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-FIVE! THE FALLS!"* Hero: *"Wait for it. ...Wait for it."*
- **Length:** ~90 s.

#### Floor 954: The Guard Post

- **New:** the imp guard (`impGuard`).
- **Debut:** the guard debuts here under the one-floor route; this replaces its old Floor 930 debut from the 10-floor layout.
- **Obstacles:** counterweights and solid ledges, known pieces only.
- **Monsters:** the Magma King's guard: the first stands alone on a ledge at the top of the first push, doesn't run,
  doesn't carry anything, and throws three embers in a fan. Later a guard with a plain imp. Guards drop nothing.
- **Set piece and beat:** a guard post at the edge of the King's territory: a slag helmet on a spike, a carved *THE
  KING'S WASTES*. The imps get organised from here. Landing carved *954*.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-FOUR! THE GUARD POST!"* Hero (first guard): *"This one's not carrying
  anything. Bad sign."*
- **Length:** ~85 s.

#### Floor 953: Second Lap

- **New:** a floor that repeats (he climbs Floor 953 twice: the landing says *953* and the door leads back to the
  bottom of 953).
- **Obstacles:** a loop of geysers, falls and sinking stones, the same both laps.
- **Monsters:** the same two imps on the same ledges, both laps, throwing at the same moments.
- **Set piece and beat:** Hell's loop on a bigger scale. The first lap teaches the pattern; the second is the same
  floor, and he knows it (rewind is just the small version of this). The carving on the landing has a second line on
  the second lap: *953* / *953 (AGAIN)*.
- **Lines:** Announcer, the second time: *"FLOOR NINE FIFTY-THREE! ...AGAIN!"* Hero: *"I know this one."* Landing,
  announcer: *"SECOND LAP: DONE!"*
- **Length:** ~110 s (two laps of ~55 s).

#### Floor 952: The Sack Pile

- **New:** none (the stretch's test: everything at once).
- **Obstacles:** counterweights, rafts, updrafts, falls; the lava rising through the whole last push.
- **Monsters:** a line of three guards on the pile at the top, throwing on a count; imps and cinders on the way up.
- **Set piece and beat:** the imps' camp, where they pile the sacks before hauling them up to the throne: a mountain of
  sacks under a carved *KEEP OFF THE PILE*. Clear the line and the pile spills: a heap of diamonds.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-TWO! THE SACK PILE!"* Hero (clearing the line): *"He keeps guards. So
  he's expecting someone."*
- **Length:** ~110 s.

#### Floor 951: The Keeper's Mound (floor boss)

- **New:** the floor boss, **the Pile Keeper** (see *Floor bosses*).
- **Obstacles:** a climbing arena around a mound of sacks sinking into the lava; counterweights between tiers.
- **Monsters:** the Pile Keeper and up to two imp guards in his second phase.
- **Set piece and beat:** the biggest pile in the Wastes, the King's share, slowly sinking. Beat the Keeper and the
  pile spills; behind it, roaring, the King's heat vent: **the King's Chimney** (shortcut 2). Landing carved *951*.
- **Lines:** Announcer: *"FLOOR NINE FIFTY-ONE! THE PILE KEEPER!"* Hero: *"He's sitting on it. Like a hen."* Defeat,
  announcer: *"THE PILE KEEPER IS BURIED IN HIS OWN PILE!"*; hero: *"Fitting."*
- **Length:** ~140 s.

---

### Stretch 3: the Heat Stairs (Floors 910-901)

The approach to the Slag Throne: stairs cut into the rock, the heat rising, the King's guard on the steps, and the
King's voice coming down from above. No new pieces; each floor combines the known ones into its own set piece, and the
voice builds to the door. The King's Chimney lets out at 910; the long way comes up into 910 as well.

#### Floor 910: The Stair Foot

- **New:** stairs that sink (a staircase of sinking stones, one step at a time).
- **Obstacles:** the sinking stair up out of the lava, updrafts at the turns.
- **Monsters:** one imp guard at the top.
- **Set piece and beat:** the foot of the Heat Stairs, carved *TO THE THRONE*. If he came up **the King's Chimney**
  the lava rises faster here and the King's voice comes down the stairs for the first time, amused (*"Came up my
  chimney, little spark? Rude."*); by the long way, the floor is as written. Landing carved *910*.
- **Lines:** Announcer: *"FLOOR NINE TEN! THE HEAT STAIRS! SOMEBODY'S WAITING AT THE TOP!"* Hero: *"Every step's
  hotter. Of course."*
- **Length:** ~80 s.

#### Floor 909: Ember Rain

- **New:** the ember shower.
- **Obstacles:** stairs and crumbling ledges, steam raining embers two at a time.
- **Monsters:** cinders in the steam, one at a time.
- **Set piece and beat:** the lava sea's steam boils up the stairwell and rains fire. Landing carved *909*.
- **Lines:** Announcer: *"FLOOR NINE OH NINE! EMBER RAIN!"* Hero (warning, 1.5 s before the first): *"Now it's
  raining fire."*
- **Length:** ~80 s.

#### Floor 908: The Guard Lifts

- **New:** none; set piece: chain lifts ridden past guards on the landings.
- **Obstacles:** three chain lifts in a row, each passing a guard post halfway up.
- **Monsters:** an imp guard on each landing (never more than two on screen), throwing fans at the lift.
- **Set piece and beat:** the King's stairwell has lifts for the guards; he borrows them. Landing carved *908*.
- **Lines:** Announcer: *"FLOOR NINE OH EIGHT! THE GUARD LIFTS!"* Hero: *"Going up. Unfortunately."*
- **Length:** ~90 s.

#### Floor 907: I Can Hear You

- **New:** none; set piece: the stairs rumble and the King speaks.
- **Obstacles:** crumbling stairs with geysers in the gaps.
- **Monsters:** cinders and one imp.
- **Set piece and beat:** halfway up, the whole stair shakes and the King's voice comes down it from ahead: he's
  waiting, he knows the hero is coming, and he isn't getting up. Landing carved *907*.
- **Lines:** Announcer: *"FLOOR NINE OH SEVEN!"* The King: *"I can hear you climbing, little spark."* Hero: *"Was
  that the stairs?"*
- **Length:** ~85 s.

#### Floor 906: The Loop, Upstairs

- **New:** none; set piece: Hell's loop as a puzzle he solves with rewind.
- **Obstacles:** a geyser row and falls that fire in one exact pattern; the only way through is the one gap in the
  pattern, which is hard to see the first time.
- **Monsters:** none: the loop is the floor.
- **Set piece and beat:** the stair loops round on itself; the same burst goes off in the same crack every lap.
  Rewind is the expected answer. Landing carved *906* and *YOU ARE HERE. STILL.*
- **Lines:** Announcer: *"FLOOR NINE OH SIX! THE LOOP... UPSTAIRS!"* Hero: *"Same trick. I'm getting good at
  this."*
- **Length:** ~80 s.

#### Floor 905: The Collapse

- **New:** none; set piece: a chase.
- **Obstacles:** the stair falls away behind him step by step while the lava surges up after it (fast, but
  telegraphed by the rumble and the first steps falling); crumbling stones and an updraft at the end.
- **Monsters:** none until the breather; then one imp guard.
- **Set piece and beat:** the stair gives way and he runs up it with the lava at his heels, the floor's one sprint.
  Landing carved *905*, which he reaches just as the last step goes.
- **Lines:** Announcer: *"FLOOR NINE OH FIVE! RUN!"* Hero (landing): *"I hate stairs now. Specifically."*
- **Length:** ~70 s.

#### Floor 904: The Count

- **New:** none; set piece: guards in a line throwing on a count.
- **Obstacles:** counterweights and ledges, falling slag.
- **Monsters:** a line of three imp guards across the stair, throwing their fans on a count (one, two, three): read the
  count, move between throws.
- **Set piece and beat:** the King's guard at its best, holding the stairs. Landing carved *904*.
- **Lines:** Announcer: *"FLOOR NINE OH FOUR! THEY'RE COUNTING!"* Hero: *"One, two, three. Duck."*
- **Length:** ~90 s.

#### Floor 903: The Last Updraft

- **New:** none; set piece: a long rise through falls.
- **Obstacles:** updrafts between lava falls (ride up while the fall beside him is off), ember shower at the top.
- **Monsters:** cinders in the falls.
- **Set piece and beat:** the hottest air in the Wastes, rising straight to the throne. The King again: *"Come up,
  then. I'll stay seated."* Landing carved *903*.
- **Lines:** Announcer: *"FLOOR NINE OH THREE!"* Hero (after the King): *"He knows I'm coming. Great."*
- **Length:** ~85 s.

#### Floor 902: The Door

- **New:** none; set piece: the door.
- **Obstacles:** a short climb of sinking stairs and crumbling ledges, guards on the steps, then the landing.
- **Monsters:** two imp guards, then quiet.
- **Set piece and beat:** the top of the Heat Stairs: a slab of obsidian carved *THE SLAG THRONE*. The camera stops;
  through it, one last chuckle (*"Mm-hm-hm. Knock, little spark."*), and the Grudge pick. Floor 901 opens on the other
  side. Landing carved *902*.
- **Lines:** Announcer: *"FLOOR NINE OH TWO! THE MAGMA KING AWAITS!"* Hero: *"Fine. I'll knock."*
- **Length:** ~75 s.

#### Floor 901: The Slag Throne (the Warden)

- **New:** the Warden, **the Magma King**.
- The fight is Story.md's *The Magma King's fight*, unchanged (three phases: seated; standing with his guard; enraged,
  the lava flood you rewind through), moved from the old "Floors 910 to 901" hall to **Floor 901**: one hall, the
  throne on its slag mound at the far end. It is the one floor allowed to stand still (a locked screen); the climbing
  is the nine floors of stairs before it. His title becomes *"FLOOR NINE OH ONE! THE SLAG THRONE!"*; after his defeat
  the hero walks up the broken throne's slag to the Waystone (*"WAYSTONE LIT! FLOOR NINE HUNDRED ONE!"*). Then the
  Shaft: Circle IX wakes up.
- **Length:** 2-4 minutes.

---

## 5. Floor bosses (Circle X)

Every 10th floor (the floors ending in 1) ends its stretch with a floor boss: smaller than a Warden, one of the
Circle's family (here the imps and the village's sparks), with a reason to be on its floor, and fought on a
**climbing arena**:

- **The climbing arena:** a tall locked-width screen of three tiers over the lava. The fight starts on the bottom tier;
  each phase change, the lava rises and swallows the tier he's on (rumble, 2 s of bubbles) and the camera steps up to
  the next tier, where the boss has climbed. The last tier is the landing. Three phases, one per tier; every attack
  telegraphed 1 s or more; at most 3 enemies on screen counting the boss.
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

## Building it

Today's Levels 2-11 (the 10-floor stages, `travelDirection` Right) are replaced by the floors above. The work, in the
order it can be built and played:

1. **Engine (C#, new rewind-safe types):** sinking stones, crumbling stones, rafts (bob, drift, tip), chain lifts and
   counterweights, heat updrafts, lava falls, a geyser column, and **rising lava** (a kill plane with a telegraphed rise
   that stops under marked ledges), plus a climbing floor layout (`world.camera` Travel Up with hand-placed and
   generated footholds) and the climbing arena (tiers, a camera step per phase). `offerShortcut` with a diamond cost
   (`costDiamonds`), and a trigger that a floor can read when a shortcut was taken to reach it (`shortcutTaken`, from
   `ShortcutProgress`). Grudge picks on every 5th floor and after bosses. **The damned soul** (`damnedSoul`, a new
   entity: harmless, no health, a looping idle, a proximity trigger that drops one `rewindOrb` once per play through
   `GameplayWorld.TryGrantOnce`), and the `rewindOrb` drop taken off `imp` (today `imp.json` drops one 20% of the
   time; imps aren't damned).
2. **The playtest bot learns to climb:** jump between moving footholds and keep moving off sinking stones (today's
   bot walks right; the old Slag Falls stage waited on this).
3. **Floors 1000-991 first** (the stretch J sees first), one level file per floor, in climb order in game.json, each
   leading to the next (`nextLevelNumber`); its playtest is `level-<n>` per floor and `story-path`.
4. **The Sack Lift, Floors 960-951, the King's Chimney, Floors 910-901** (Level 11's arena renumbered to Floor 901).
5. **The long way:** a generated floor per skipped floor (one level template the generator fills from the floor
   number), with the long-way floor bosses.
6. **Data that follows the renumbering:** Yooba's floor reactions (`ui/yooba.json`: the Spill moves from 985 to 998, the
   Loop from 957 to 995, Halfway's 950 stays on the long way), the Shaft's climb (one floor per level), the playtest
   scenarios that name levels by number (`wastes-climb`, `story-path`, `shortcut`, `waystone`), Circle IX's stages
   (Levels 12-18, recut into floors the same way; the Old Stair's shortcut stays at Floor 871 to 841).
7. **Art (GPU, priced):** the lava-sea backdrops (flying above the lava), the toolkit's props (stones, the village's
   rafts, lifts, counterweight sacks, vents, the carved landings), the sack-swing imp, the damned soul (a grey,
   hunched, see-through figure with a looping idle and a hiccup clip), the Imp Foreman and the Pile Keeper. Until they exist, pieces draw as the track's plain shapes and labeled placeholders.
