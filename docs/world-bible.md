# Sick of It: world bible

The plan every agent builds to. It rests on [Docs/Story.md](../Docs/Story.md) (canon: names, voices, tone) and
[Docs/LevelDesign.md](../Docs/LevelDesign.md) (how a floor is built); where they disagree, they win. Tone: deadpan hero,
booming 90s announcer, real stakes played for laughs; **Hell is repetition**, never office or work jokes; slimes are villagers.

**The living-world bar (every area, every floor).** Never more than ~10 s of play without something to see, hear or do.
Every area has: *dressing* (background, props), a *landmark* seen from far, *life* (things that move and live on their own),
*ambient sound* (a bed plus one-shots), and *three or more kinds of interaction*. Things change by themselves (a clock,
the lava, a routine) and react to the hero (shots, a graze, a rewind). Story is told by something that happens, then a voiced
line; a `showText` is one line of at most ~60 characters and the last resort.

**The rhythm of ten seconds.** A floor is built on a beat grid: a *look* (a vista or landmark coming into view), a *do*
(a jump, a ledge, a shot), a *hear* (a one-shot, a voice) and a *surprise* (something moves) in rotation, so no gap passes
~10 s. Authoring tool: `ambient_finds.py` glints and souls, `bystanders.py`, `CircleAmbience`, the `incoming-warnings` HUD.

---

## Areas

Each: name, mood, look and sound, size, in and out. Sizes are in play time (a floor is 60-120 s; boss floors up to 3 min).

### The Meadow (Level 1, Floor 0, built)
- **Mood:** a nice, dull afternoon that is about to end. Warm, bright, then fire.
- **Light (Nov 11):** warm dusk grade (`meadow`) that turns raid-red when the burning bed starts; soft dusk pool and a hero
  rim so he reads on the bright sky (Docs/lighting.md).
- **Look and sound:** green hills, the village's thatch, a single path; Video1's footage as the sky layer. Bed: wind, a far
  rooster, a mill creaking. Then the Overlord's hum, lasers, burning thatch.
- **Music (Nov 11):** its own track, not the film's audio: "A nice day", a plucked D-major figure over a soft drone with a soft flute (`meadow_calm`, 84 BPM), which turns to its raid (`meadow_raid`: the same tune in D minor, 128 BPM, double kicks and alarms) the moment the sky opens (`playMusic`, 6 s in). Rewinding bends whatever is playing (slowed, low-passed) and it snaps back on release.
- **Adaptive score (Nov 12):** explore / fight / finale loops at 112 BPM, D minor (pastoral folk turned frantic), crossfading on the beat or bar with the raid; see Docs/Story.md "The adaptive score". Falls back to `meadow_calm`/`meadow_raid` until the clips are made.
- **Sound (Nov 11):** every verb has three takes that never repeat in a row (`player_shoot`, `hit_pop` + `kill_pop` layered on the enemy's own sounds, `graze_tick`, `pickup_diamond`, jump, land, dash, rewind start and stop), Music ducks 8 dB under each voice line, and the bed sits on its own Ambience bus (Docs/SoundAudit.md "Takes, buses and beds").
- **Size:** ~2 min (30 s of calm, imp raid, the portal scene ≤10 s locked, skippable).
- **In/out:** New Game (no Video1, no difficulty card) lands here; out through the fire portal into the Shaft plunge on a
  first run (Video2 stays in Rewatch Stuff).
- **First minute, as built (Nov 11):** the corner tag "PROLOGUE: THE MEADOW" (no title card); its ambient bed is the level's `ambientSound` (`ambience_meadow_calm`: wind, a far rooster, a mill creaking; the `meadowBed` entity is only a marker now) which cross-fades to `ambience_meadow_burning` (roaring fire, a village alarm bell struck out of time, a frightened rooster) when the sky opens at 5.5 s (`setAmbience`); neighbours (`meadowNeighbour`) strolling, then running when the sky opens at ~4 s; imps build 2 by 8 s and 4+ by 25 s with embers crossing the screen; thatch cinders; at ~30 s the staged first rewind (`stagedRewind`: an undodgeable ring of embers, time to 20%, the rewind button grows and pulses, the hero says "Hold R.", after the rewind "Noted."). Still missing: a hen, the mill and a cart (no art yet).
- **Falls short today:** the first 20 s are mostly the hero and sky; village life (neighbours, a cart, chickens, washing
  line) is thin. Needs the *Meadow's morning* below.

### The Shaft (Hell's map, built)
- **Mood:** vertical awe; a very tall hole and a tiny dot.
- **Look and sound:** a cross-section of ten Circles in their biome colours, Waystones on the wall, cracks that hint
  shortcuts. Bed: a deep wind-in-a-pipe drone, with each Circle's own voice rising as the marker passes it (lava hiss,
  stone hum, wind over bridges...). Yooba's portrait chatters at the side.
- **Size:** 10-25 s per visit (a first plunge ≤8 s, skippable from frame one).
- **In/out:** after Level 1 on a first run (plunge), after every floor stage and from the menu; out by Continue, Visit Yooba
  (his shop opens over it) or Back.
- **Falls short:** it is a map with a marker. Needs life on the wall (see *What happens*).

### Yooba's counter (shop, built)
- **Mood:** a market stall in the middle of the end of the world; cosy, damp, cheerful.
- **Look and sound:** Yooba's portrait, shelves of slime-made goods, lanterns. Bed: gurgle, a drip, his humming.
- **Size:** as long as the player stays. **In/out:** Visit Yooba on the Shaft or the main menu; Back.

### Circle X: the Ember Wastes (Floors 1000-901, built, the vertical slice's home)
A Circle is four stretches, each its own area. All share lava, cinders, imps and the climb.

1. **The Landing and the Slag Falls (Floors 1000-996).** *Mood:* bewildered arrival, then dread with jokes. *Look:* a
   lava river bank, sinking stones, rafts, the carved landing with the floor number, lava falls and geysers. *Light (Nov 11):*
   Floor 1000 sulphur orange, Floor 999 furnace red; lava pools flicker light onto platforms within 2 units
   (Docs/lighting.md). *Sound:* lava bubbling bed, stone creak, geyser one-shots; Floor 1000's bed is its `ambientSound`
   `ambience_floor1000` (a low lava rumble, bubbling and popping, long creaks of stone) on the Ambience bus. *Size:* 5 floors, ~8 min. *In:* Video2 drops him; *out:* the Shaft.
2. **The Spill and the Imp Run (998-997).** *Mood:* looted, wrecked. *Look:* the village's wreckage dragged through Hell,
   the baker's cart, sack imps on hauling chains. *Light (Nov 11):* Floor 998 bone grey-violet, Floor 997 obsidian with
   magenta highlights and magenta bullet lights. *Sound:* chain clank, imp chatter, a wailing cart wheel.
3. **The Foreman's Pile (996-991).** *Mood:* industrious, nasty. Ledges, chain lifts, crumbling stones, a collapsing
   bridge; ends in the Imp Foreman's climbing arena. *Out:* the Sack Lift (a 20-diamond fare) or the long way.
4. **The Depot stretch (960-951).** *Mood:* a looted village rebuilt wrong. *Look:* The Depot's stacks, the Rest of the
   Village (959), burning thatch, hot air, drips, falls, a guard post, a second lap, the Sack Pile. Boss: the Pile Keeper.
5. **The Heat Stairs (910-901).** *Mood:* ceremonial heat, a throne ahead. *Look:* ember rain, a cart run, the Count, the
   last updraft, the Door. *Sound:* a smug chuckle from ahead that grows. Out: the Slag Throne, the Magma King's arena.
6. **Stop floors (959, 957, 952).** Rest rooms: a graffiti wall, a hot bath, a waystone shrine. ~20 s of calm each.
- **Falls short:** long floors have routes and set pieces; between set pieces the climb can run past 10 s with only
  bullets. Fix: every ledge-to-ledge hop (an 8 s build target, 10 s hard ceiling) carries at least one of: a glint, a damned soul, a bystander
  routine, an ambient actor crossing the far background, a sound one-shot (the placement rule under *What happens*).

### Circle IX: the Cracked Road (Floors 900-801, built to 802)
- **Mood:** colder, hotter and more vertical: black spires with glowing seams, a road that keeps splitting.
- **Look and sound:** spires pushed up through the road, brimstone vents, hoists, a leaning spire, a quarry, a gallery of
  carved faces. Bed: a stone hum (the Spire Fiend listening) under the wind; cracks splitting as one-shots.
- **Size:** about 30 floors on the played route (~45 min). Stretches: the Cracked Road (900-891, Road Breaker), the
  Carver's Yard (860-851, the Carver), the Needle Stair (810-802, up to the Spire Fiend's door). Out: the Shaft; shortcuts
  the Spared Demon (891-860), the Hollow Spire (851-810).
- **Falls short:** the Spire Fiend's floor (801) and the long-way stand-ins (Level13-18) are first-pass.

### Circles VIII to I (designed in Story.md, not built)
Each is an area when built and must clear the bar above before it ships, in order:
**VIII the Rune Bridges** (glowing walkways over a void, Watchers), **VII the Gloomwood** (a dead forest of looping souls),
**VI the Mire** (a drowned graveyard, green lights), **V the Caverns** (teeth and drool), **IV the Dead Peaks** (moonlit
shrines), **III the Bastion** (fortress walls), **II the Keep** (courtyard, dungeon), **I the Throne Stair** (the Overlord's
palace, daylight leaking in). Shared rule: each Circle opens with a *vista* beat in its first 10 s (see *Landmarks*).

### Menus and the climb's ghosts
The main menu is the Meadow in daylight with the hero idling (a playable `PlayerController`) and **other climbers as pale
ghosts** around him (Hawkes Net; `climbingNow` chip). Out: Continue, New Run, New Game, Videos, Shop, Codex.

---

## Landmarks

One per area, readable from far, to steer by. The camera shows it early and the route passes it.

| Area | Landmark | Seen from | What it does |
|---|---|---|---|
| Meadow | The Overlord's silhouette, colossal on the hills, red eyes; the village's mill | spawn | His eyes track; the mill's sails turn until the laser |
| Shaft | The stack of Circles, the tiny glowing marker and the dark Waystones above it | whole screen | The marker pulses; each reached Waystone lights |
| Yooba's counter | A hanging green lantern shaped like his own grin | the door | Swings, brightens when a ware sells |
| Landing / Slag Falls | The carved floor number on the landing; the lava falls ahead | arrival | The numerals glow as he nears |
| Spill / Imp Run | The haul chains running up out of frame with sacks riding them | first screen | The sacks climb in lockstep forever |
| Foreman's Pile | The Imp Foreman's hoist and whip-post over the pile | 993 onward | Creaks, throws a shadow across the route |
| Depot stretch | The Depot's smokestack and the burning thatch glow on the horizon | 960 | The stack coughs; the glow flickers |
| Heat Stairs | The Magma King's throne glow at the top of every stair, his chuckle louder each floor | 910 | Pulses with his laugh |
| Slag Throne | The throne and the lava crown | the arena | He slouches until stood |
| Cracked Road | The Great Spire of Floor 801 over every floor | 900 | Grows a little each floor (the Fiend regrows it daily) |
| Carver's Yard | The Carver's half-cut statue of the hero | 860 | Gains a face feature each floor |
| Needle Stair | The spire's hum made visible: rings in the air | 810 | Rings rise in time with the hum |
| VIII-I | Per Circle: a bridge lantern, a hollow tree with eyes, a drowned crown, a mouth in a cliff, a bell tower, a gate, a chain, the Overlord's open gate with daylight leaking | each Circle's first screen | Set in Story.md |

---

## Who lives here

Routines are loops (Hell is repetition): the same few things, forever, and they change a little when the hero is there.

**The Hero.** Idles with a sigh and a glance at the sky; mutters a one-liner on a graze, a rewind, a pickup. Reactions:
flinch on a hit, a shrug on a death, a sit-down at the end. Never speeches.

**Yooba** (voice `yooba`). Rides in the gaps. *Routine:* wobbles, hums, polishes goods, hacks up a diamond, counts floors
aloud. *Reactions:* knows the next floor's gossip; remembers deaths; flags glints he can smell; bargains.

**The Announcer** (`announcer`). Calls floors, bosses and deaths. *Reaction:* louder on a graze streak, mournful on a
death, delighted at a boss.

**The Overlord** (`overlord`). Seen only at a distance until Floor 1: eight glimpses on Floors 997-806. Never answers the hero.
  *Want:* up, out, the surface. *Arc:* ahead of the hero all the way, until Floor 1, where he rewinds too. *Moment that shows it:*
  the chase below. *Three words:* bored, enormous, stuck. *Voice:* `overlord`; the silhouette itself is silent but for a low rising hum.

**The Climbers.** *Rook* (rival, always second; 996/992/955/899/858/808): shows off, cuts in, leaves a mess. *Wick*
(Circle X's lamplighter; 999/993/960/902): lights lamps on a loop, helps, repeats himself. *Pointer* (Circle IX's
fingerpost; 900/895/860/810): points the wrong way kindly. Each branches on the hero's hearts, diamonds and Grudges.

**The damned** (souls, bystanders). Stand in loops: a soul that pushes a cart to the same wall, a pair that argues the same
line, a queue that never moves. *Reactions:* flinch from shots that pass, hide at the boss's roar, wave at the hero if
he is unhurt (witness triggers, Story.md *Bystanders*).

**Imps and cinders** (Circle X). *Routine:* carry sacks, patrol ledges, sit on chains, argue over stolen goods. *Reaction:*
turn and hurl embers when the hero is near; flee when a boss is dying.

**Brimstone demons and the road's bestiary** (Circle IX). *Routine:* push a spire up through the road, then pace it. *Reaction:*
split rock to cut his way.

**The Wardens and floor bosses.** The Magma King (smug; never stands unless made to), the Spire Fiend (listens, whispers,
counts steps), the Imp Foreman, the Pile Keeper, the Road Breaker, the Carver. *Reaction:* each names the hero's progress
when it starts the fight and gets a phase for every rewind it notices.

**Far-off life** (`CircleAmbience`, up to 8 actors): birds of ash, a far imp column on a ridge, a drifting raft, a ghost
dragging a chain across a distant ledge, sparks falling like rain. They cross the background on their own.

**Other players** (ghosts): pale copies of the hero walking his floor.

---

## Characters

The named characters, each with a want, an arc, a moment the player lives, and the voice and clips that play it.

### The Hero
- Want: To get out of Hell and back to the meadow, one floor at a time.
- Arc: Sick of it, then stubborn, then something like at peace on the last floor.
- Moment: A graze streak: he flinches, mutters a one-liner, and the HUD vessels crack or glow with what happens.
- Personality: tired, dry, stubborn
- Voice: `player` (lines `vo_hero_*`)
- Animations: Idle, Walk, Talk (Idle held while a voiced `vo_hero_*` line and its subtitle play), Jump, Fall, Fire, Dash, Hurt, Victory (art/hero.json)

### Yooba
- Want: To keep the climber alive and sell him goods, and to find his Lost Things.
- Arc: A shop-slime in the gaps who starts out bargaining, remembers every death, and ends as a friend.
- Moment: He rides the Shaft and the shop, hacks up a diamond and counts floors aloud as the hero buys.
- Personality: wobbly, warm, nosy
- Voice: `yooba`
- Animations: Idle, Walk, Talk, Laugh, Cough (ui/yooba.json portrait)

### The Announcer
- Want: To make Hell a show and every floor a headline.
- Arc: Delighted at first, mournful on deaths, impressed by the end.
- Moment: He calls each new floor as the elevator dial ticks down, louder on a graze streak.
- Personality: loud, theatrical, gleeful
- Voice: `announcer`
- Animations: Idle, Walk, Talk (banner and voice only)

### The Overlord
- Want: Up and out, to the surface, ahead of the hero.
- Arc: Seen far off and never answering, until Floor 1 where he rewinds too.
- Moment: A glimpse on the far ledge: a taunt, a dropped ember, a laser sweep, then he is gone upward.
- Personality: bored, enormous, stuck
- Voice: `overlord`
- Animations: Idle, Walk, Talk (art/overlordFar.json)

### Rook
- Want: To reach the top first and be seen doing it.
- Arc: Rival who shows off, then cuts in, then leaves a mess the hero can use.
- Moment: He sprints past on Floor 996 and kicks loose a ledge the hero has to cross.
- Personality: brash, vain, sly
- Voice: `rook` (subtitles; recipe in Tools/Audio/companion_voices.json)
- Animations: Idle, Walk, Talk, Hiccup (art/rook.json)

### Wick
- Want: To keep Circle X's lamps lit, because someone must.
- Arc: Repeats himself, then helps, then lights the last lamp for the hero.
- Moment: He lights a lamp on Floor 999 as the hero passes and the dark ledge shows its path.
- Personality: patient, repetitive, kind
- Voice: `wick` (recipe in Tools/Audio/companion_voices.json)
- Animations: Idle, Walk, Talk, Hiccup (art/wick.json)

### Pointer
- Want: To show the way, even when it is the wrong one.
- Arc: Points wrong kindly, then points right once the hero has earned it.
- Moment: He points up a stairwell on Floor 900; the real way is the crack beside it.
- Personality: earnest, mistaken, gentle
- Voice: `pointer` (recipe in Tools/Audio/companion_voices.json)
- Animations: Idle, Walk, Talk, Hiccup (art/pointer.json)

---

## What happens

Per area: interactions (at least three kinds), events on their own, ambient sound, what changes.

### The Meadow
- **Interactions:** shoot imps (rockets), rewind a bad hit (R), collect diamonds, talk to a neighbour (walk to him: a
  one-line voice), knock a hay bale, pet a dog (it follows him till the raid).
- **On their own:** a hen crosses; smoke from the chimney; the mill turns; neighbours go about their routine; at ~25 s the
  sky opens, imps drop in, the village panics.
- **Ambient:** wind, a far rooster, a hammer, then alarm bells and fire.
- **Changes:** thatch burns, neighbours flee and a few hide in the cellar (they will be Hell's damned if the hero fails).

### The Shaft
- **Interactions:** Continue, Visit Yooba, hover a Waystone for its line, click a Warden silhouette (a ??? or a name).
- **On their own:** the marker breathes; embers rise; distant Wardens blink; a soul falls past; a shortcut crack opens at
  the marker's passing.
- **Ambient:** the drone; the passing Circle's tone; a Waystone's chime when lit.
- **Changes:** reached Waystones glow; taken shortcuts become dotted lines; the dead leave a ghost mark on the floor they died.

### Yooba's counter
- **Interactions:** buy, ask, haggle (turned away when broke), take a quest (Lost Things), try goods on the hero.
- **On their own:** he hums, juggles a diamond, falls asleep during a long silence, wakes with a gag.
- **Ambient:** drips, a distant gong. **Changes:** the shelves restock with the climb; the quest items he has been paid for
  hang on the wall.

### Circle X
- **Interactions (every floor):** shoot, dodge, dash/wall-jump (once unlocked), rewind, graze for style, ladders, grapple
  (after the Hook), pick up glints, weapons and Lost Things, talk to a Climber, take a shortcut.
- **On their own:** lava rises or sinks; stones sink by themselves in a fixed lap; geysers on a count; imps haul sacks
  in lockstep; the Overlord's far glimpses; ash falls; far actors cross. *Placement rule:* every 8 s of climb (the build target, inside the 10 s ceiling)
  carries a one-shot sound or a moving thing; a bare ledge is a defect.
- **Ambient:** lava bed, creaks, imp chatter; the Magma King's chuckle from 910 up.
- **Changes:** floors count down; the lava rises over the long floors; the village's things appear (a rolling pin on 997,
  a mitt on 995, a bell on 992).

### Circle IX
- **Interactions:** the same, plus ladders as the road's main climb, bounce shots, chains, risen demons.
- **On their own:** the road splits in time with the hum; spires grow a notch each floor; wind howls through cracks.
- **Ambient:** a stone hum; cracks; Pointer's creak when he points. **Changes:** the Great Spire gets taller each floor.

### Hell's cabinet HUD (inked widgets, built Nov 12)
The announcer's frame on every floor: **ember vessels** for hearts (crack on a hit, glow on a heal), a **tape reel** for
rewind juice (spins while held, drains as juice spends), a **gem** that pulses when a diamond lands, a **brass elevator dial**
that ticks the floor down. Two corners only (health+reel top-left, gem+dial top-right); on a phone the rewind button is the
big bottom-right reel (≥72 px). Same game state as before; art from `HudInkAtlas` (Gen sheet under Resources/UI). Playtest `hud-ink`.

### The chase (the Overlord escaping upward, built Nov 11)
The Overlord is always one floor ahead, and the hero only ever sees it when time goes backward. **When a rewind starts inside a
death's undo window** (the hero has died, then holds R or the rewind button), the HUD shows the Overlord's silhouette (horns,
shoulders, red eyes opening as he turns away) hauling himself up and out of the top of the screen over ~2.4 s, with a low rising
hum (`overlord_beam_charge`, pitched up). A rewind from no death shows nothing, so seeing him means "I undid a death and he got
away again". `OverlordEscape` (`Gameplay/Presentation`, made by `GameplayHud.BuildRewind`): presentation only, real time, nothing
saved. It is the quiet spine of the eight far-ledge glimpses and of the Floor 1 reveal that the loop was his first.
Playtest `overlord-chase` (run on 390x844 touch: a finger on the rewind button, 44+ px).

### Death and the Waystone loop
On death: the hero blinks, the announcer gloats, a quip, and **Again!** at once; the Shaft drops the marker to the last
Waystone only if the run ends. The run feels like the loop, not a penalty.

---

## Story beats

Each beat is lived, not read. Format: **where / when -> what happens (events, sound, voice) -> text, if any.**

1. **The nice afternoon (Meadow, 0:00-0:25).** The hero idles on the hill: a hen crosses, a neighbour waves, the mill
   turns. *Voice:* a quiet sigh (`vo_hero_*`). *Text:* none.
2. **Then the sky opened (Meadow, 0:25).** A shadow slides over the field; the Overlord's silhouette rises; a low
   boom; imps fall. *Sound:* a drop, sirens. *Voice:* the announcer's first call. *Text:* the unvoiced beat line.
3. **The raid (Meadow, 0:25-1:30).** The hero shoots imps; they steal diamonds from a cart; neighbours run. *Event
   only.* First rewind taught by an unlucky hit and the hero's line.
4. **The portal (Meadow end).** The laser drops (charge, column, burst, ring of burning turf); the hero walks to it
   scripted, leans in, says *"That's a hole."*, shouts *"Hello?"*; the echo returns deeper (`vo_portal_echo`); *"Nope."*;
   he is yanked in, then Yooba bounces in after him. *Voice* throughout. *Text:* none. Built on the beat kit
   (`docs/beats.md`, Nov 11): letterbox bars, a push-in on the hole, each line a speech bubble over its speaker (the
   hero, the hole's echo, the Overlord's *"Mm. ... Stay."* from above), 10 s or less, any tap skips to the end state,
   a second play in a session runs about 60% shorter.
5. **The plunge (Shaft, first time).** The camera falls past every Circle; each Circle's tone rises; a final slam at
   the Landing. *Voice:* announcer: *"Welcome to the bottom. Nine hundred ninety-nine to go."*; hero: *"...Great."*
6. **Arrival (Floor 1000, 0:00-0:15).** He drops onto the bank; stones sink under him; lava hisses. The announcer
   names the floor. *Text:* the warm-grey beat line.
7. **Yooba's hello (Shaft after Video3).** The slime coughs up a diamond (cough clip + sound), counts "nine hundred
   ninety-nine", the hero replies once. *Voice* both sides.
8. **The theft (Floor 998).** The launcher is taken from his hands in play (animation, a snatch sound, imps cheer);
   he gets fists. *Voice:* the hero's line. *Text:* the beat line only.
9. **The Foreman falls (Floor 991).** A voiced intro, the pile rises, the whip cracks; at the defeat, slow motion and
   a wave of diamonds; the Sack Lift offers a fare.
10. **The Overlord's glimpses (997-806).** A taunt, a dropped ember, a lift, a laser, a stolen diamond, a rewind, a
    summons, a dropped diamond: each with his one line and the hero's reply. He never answers.
11. **Wick and Rook (Circle X).** Rook cuts in, shoots the same imp, leaves a mess; Wick lights a lamp the hero needs;
    each scene branches on his hearts, diamonds and Grudges and sets a flag the later scenes read.
12. **The Magma King's knock (Floor 902).** A chuckle through the door, then a knock heard from the other side; the
    door opens; he does not stand. *Voice:* the King. The fight ends in the Waystone's light.
13. **Circle IX wakes (Shaft).** The marker climbs; the hum of the Spire Fiend rises; Pointer's first scene (900).
14. **The Fiend counts (Floor 801).** He whispers the hero's steps back; the rewind enrages him; the spire shatters.
15. **Midpoint, Floor 500.** The hero is offered a shortcut home: a lit door, daylight in it, the sound of the
    meadow. He walks past. *Voice:* hero. *Text:* none.
16. **Floor 1.** The Overlord rewinds too; the gate cracks onto the meadow; daylight pours in. The endings play through
    gameplay, then the knock from underground.

Every beat above is an event first. A beat that is only a `showText` is a defect.

---

## Bullets and hazard tells (Meadow + Floors 1000-997, Nov 12)

Hostile shots read like Returnal orbs: white-hot core, dark ink outline, cyan/magenta halo (never the sulphur/furnace
orange of the Wastes; Floor 997 prefers magenta), short fading trail. The hero's rockets stay warm amber and elongated.
Aimed shots flash the muzzle and draw a thin aim line ~0.4 s before firing; spreads flash the emitter; lava / geyser /
surge / sink tells glow a warning colour ≥ 0.5 s before they hurt. Playtests: `readability`, `bullet-look`. Hitboxes
unchanged; phone draws bullets at least 8 px across.

---

## Where the game falls short today (and what to build first)

1. **The Meadow's life.** Neighbours, a dog, a hen, a hay bale, a mill and a cart that the raid wrecks: 8 dressings and 3
   interactions before the sky opens.
2. **Dead air on long floors.** Ledge hops over ~8 s with nothing on screen but bullets: add glints, souls, actors and
   one-shots on a grid (`ambient_finds.py`, `CircleAmbience`).
3. **The Shaft is a map.** Add falling souls, Waystone chimes and the dead's marks.
4. **Circles VIII-I have a design but no area.** Build one floor to the bar first (the *vertical slice*), then widen.
5. **Every Warden's entrance.** Heard before seen, a door that opens, a room that changes.
6. **Voice.** Several lines are prompts with no clips (`pending_clips.json`); make them through Hawkes Gen.

**The vertical slice to build first:** Floor 959 *The Rest of the Village* (a stop floor in Circle X), finished to the full
bar: dressing, the Depot's stack as landmark, a soul loop, a rest fire, one Climber scene, an ambient bed, three
interaction kinds, a beat told as an event.

---

## Review fixes (fun-reviewer verdict: strong bible, thin build)

**Built vs planned.** Built: Circle X and IX routes, the Shaft map, Yooba's shop, Climbers, ghosts, stop floors. *Planned, not
built:* the Meadow's village life (hen, mill, neighbours, dog, hay bale, cellar flight), the Shaft's falling souls, Waystone
chimes and death marks, every Circle VIII-I area, beats 15-16. Anything under "interactions" for those is a promise.

**The one rule.** Ceiling: 10 s with nothing to see, hear or do. Build target: one event per 8 s.
Test: a playtest gap probe logs each area's events with timestamps and fails any gap over 10 s (to add to the `floors-*` runs).

**Circle VIII: the Rune Bridges (first slice of the middle descent).** *Mood:* hushed, vast. *Look:* blue-lit walkways over a
void, floating rune stones, a bridge lantern as landmark, Watchers (eyes) that follow the hero. *Sound:* a wind-over-void bed,
chimes when a rune lights, a low pulse when a Watcher looks. *Size:* ~100 s a floor. *In:* the Shaft after Floor 801;
*out:* the next bridge door. *Interactions:* shoot Watchers, step runes to light a path, rewind a crumbled span,
graze a gaze beam, take a lantern to light a dark span. *Life:* lantern-bearing souls crossing on far bridges, Watchers
blinking in sequence. *First 10 s:* the lantern appears over the void, a Watcher turns toward him, a rune chimes under his feet.

**Warden entrances (heard before seen).** Each Warden's floor opens with sound from ahead for ~10 s (the Magma King's chuckle,
the Spire Fiend's counting hum, the Bridge Wraith's bell of runes), then the door or wall changes (a throne rising, the road
splitting, lanterns going out) before the boss bar. No entrance is a text card.

**Ending beats 15-16 testing.** Floor 500's lit door and Floor 1's gate are checked by the playtest once built: the scenario logs
the door sound, the daylight light change, and the hero's walking past as events; no text-only check.

**Voice.** Beats 2 and 6 keep a warm-grey line, but each is paired with an event and a sound first; pending clips in
`Tools/Audio/pending_clips.json` are made through Hawkes Gen before the beats are called voiced.

**Arrival beats, Floors 1000-997 (2026-11-11).** *Story beats:* arriving on each floor is lived once: the slam on the bank (1000), a ghost flickering past under a rumble (999),
the baker's cart crashing (998), the imps' snatch and cackle (997), each with a stinger, a shake, the hero's reaction and his one voiced line; no warm-grey captions. *Who lives here:* the pale
`echoSoul` ghost of an earlier climber; the imps. *What happens:* an event first, voice second, text last (one line); skipped on a retry, never locks input. Playtest `story`.

**The menu and the counter (2026-11-11).** *Who lives here:* the hero idling on the meadow is the first screen; Yooba appears on it only
as a button, after the first diamonds. *What happens:* each new button is announced by a NEW! burst; Yooba's first visit is a whispered, glowing
pointer at the one ware the hero can buy. *Landmark:* the glowing card. Details in Docs/Story.md.


### Floor 999 dressing and the death screen's record (Nov 11)
- **Floor 999 (Only 999 To Go)** now reads differently from 1000's bare bank: a red glow from the climbing sea, tally marks scratched on the rock (the counted wall, for the soul that counts the same stone), rising ash. Props in `levels/Floor999.json` `world.props`.
- **Death screen:** always shows `BEST: FLOOR n` (kept for good, `BestFloorRecord`, "(NEW!)" on a new best) over the next unlock (cheapest good, next Waystone or the top).

### Floor arrival cards and the ember iris (Nov 12)
- Every screen change closes and opens as an **ember iris** (`ScreenTransition`: the picture shrinks to a ring of burning edge in 0.35 s, opens from it in 0.55 s; a pulse of ember light sits in the middle while a load holds it shut, so it is never plain black).
- Arriving on Floors 1000-997 slams a **stamped card** ("FLOOR 1000" over "THE LANDING", tilted, dark plate, ember rim) with the stamp thump and one announcer quip (`vo_card_1000` "Welcome to the bottom!", `vo_card_999` "Only nine ninety-nine to go!", `vo_card_998` "Mind the spill!", `vo_card_997` "Imp run!"); gone by 1.5 s while play is live. `Gameplay/Presentation/FloorCard.cs`; playtest `floor-card`.
