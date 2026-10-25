# Lore Videos: four short films for the climb

Scripts, shot lists, placement and prices for four 25-40 s videos (J's roadmap task, 2026-10-25). They are
**design only so far**: nothing here is generated or baked, because the task had no GPU budget (the prices are at the
end, for J to approve). Each tells one thing the game only hints at, in the game's tone: deadpan hero, booming arcade
announcer, absurd comedy from Hell itself, **no office or work jokes, slimes are villagers (never enemies)**, and
nothing that spoils the working answers in [Story.md](Story.md) (*The Overlord: who he is*, the Wardens' secrets).

| # | Video | File | Length | Plays | Cast |
|---|---|---|---|---|---|
| 5 | **The Count** (the Overlord) | `Video5.json` | ~30 s | after Level 21 (Floor 951, the Pile Keeper), before the King's Chimney choice's climb | Overlord, Announcer |
| 6 | **Lap 47,212** (the Shades' endless walk) | `Video6.json` | ~32 s | after the Floor 700 level (Circle VII's first floor, the Gloomwood) | Hero, Shades, Announcer |
| 7 | **Market Day** (the village and Yooba) | `Video7.json` | ~38 s | after Level 13 (Floor 959, *The Rest of the Village*) | Yooba, Hero, Announcer |
| 8 | **The Best Seat** (the Magma King's backstory) | `Video8.json` | ~35 s | after Level 31 (Floor 901), on the Shaft as Circle IX wakes | Magma King, Announcer, Hero |

## How they plug into the game

A video is `Assets/GameConfig/videos/VideoN.json` plus its name in game.json's `videos` list, a baked sprite sheet
(`Videos/VideoN.asset`), `Videos/VideoN_Audio.mp3` and a first-frame thumbnail (Docs/VideoPlayback.md). `afterLevel`
is the **level number** whose results lead to it (Video2 is `afterLevel: 1`, Video3 `2`), `nextLevel` the level it
hands on to (the one the path would have started anyway), `returnToMainMenu: false`. All four are skippable with
Escape and always watchable again from the video select. Video4 (the blue bridge) stays Circle VIII's.

Level numbers come from the *Level numbers* table in Story.md / LevelDesign.md. Video6's level (Floor 700) does not
exist yet (Circle VII is unbuilt); its `afterLevel` is filled in when it does. Video8 and Circle IX's first floor
share the Shaft after Level 31: the video plays **before** the Shaft's climb into Circle IX, so the climb is the
last thing on screen. Video7 plays where Yooba is already "met" (his hello was at Floor 999), so it deepens a
character the player likes rather than introducing one.

Voices (all existing, `Assets/GameConfig/audio.json` / the hello-hole cast): Hero `DzkD7I4tf5i3KSaB4cz5`, Announcer
`1xynpTfy7I462pPrU2kL`, Overlord `A5KBJzxznyU8QeeRqyiY` (eleven_v3, speed 0.85), Yooba `EdDvd8GSdCyZwUcsPawi`
(`eleven_flash_v2_5`), Magma King `2ERZABeWdKVXLvVAKor6` (eleven_v3, speed 0.9). The Shades have no words: a
designed hum (`audio.voice-design`, 3 previews) for the one note they share. Lines below get `audio.json` entries
(`vo_lore5_*` ...) when the videos are made.

Look: the Meadow's hand-painted comic-fantasy style with bold ink outlines (Video2, Docs/LevelDesign.md "The art of
the climb"); each film in its Circle's palette. Format 1280x704, 24 fps, a short end card (the game's name, then
"Made with Hawkes Gen · hawkesgen.com" small) on the ones played from the video select only.

---

## Video 5: The Count (the Overlord)

**Logline.** The Overlord has only ever said two things to the hero. Here he says a few more, to nobody, and we see
what a ruler of 1,000 floors does with an evening: he counts the village's diamonds, one sack at a time, and they
are never quite enough. It makes him sigh. That is the whole film.

**Why it matters / where it plays.** The imps in the Sack Pile (Floors 960-951) have been hauling the village's
sacks up the floors. The hero has just beaten the Pile Keeper. This is where the sacks go. It deepens the Overlord
without explaining him: he is bored, enormous and patient, and the diamonds are for **the gate** (already known).
It does *not* say who he was (Story.md keeps that for the late clues). Plays after Level 21.

**Tone notes.** Short lines, long pauses, every word costs him nothing. The comedy is scale and deadpan: a titan
carefully sliding single diamonds along a table the size of a field. No hurry, no jokes in the lines themselves.

**Script** (voices: Overlord, Announcer)
> *A hall of black glass. A throne's back to us, enormous. At its foot a long slab. A line of imps with sacks.*
> IMP (squeak, no words): *drops a sack, bows, retreats.*
> OVERLORD: Mm. ... One.
> *He tips the sack. Diamonds clatter onto the slab. He moves them with one finger, one by one, into a row.*
> OVERLORD: Two. ... Three.
> *Another sack. Another bow. The row grows. It runs out of slab.*
> OVERLORD: Hhhhh... *(a long, bored exhale)* ... Not enough.
> *He turns the last sack over. Nothing falls out, and on the slab one place in the row stays empty: a diamond short.*
> OVERLORD: Someone ... has been *keeping* some.
> *He looks up. Far above him, a pinpoint of light under a door. A beat. He looks at it for a very long time.*
> ANNOUNCER (distant, delighted): HE IS COUNTING AGAIN, FOLKS!
> OVERLORD: Mm. ... Quiet.
> *Cut to black. The end card.*

(The Overlord's two original lines, *"Mm."* and the exhale, return as his verbal tics. "Someone has been keeping
some" is Yooba's gag from the Prologue; the Overlord does not know the name.)

**Shot list** (24 fps; the length is set by the table read)

| # | Time | Shot | Action / camera | Sound | Recipe |
|---|---|---|---|---|---|
| 1 | 0-4 | Wide, the hall | Imps line up; slow push toward the throne's huge back; embers drift | Low drone, imp scuffle | `video.scene` |
| 2 | 4-9 | Medium on the slab | A sack tips; diamonds clatter; a finger the size of a tree trunk nudges them into a row; "One." | Clatter, Overlord line 1 | `video.keyframes` (finger lands on the diamond at 6 s) |
| 3 | 9-14 | Time-lapse feel, wide | Bows, sacks, the row stretches; "Two. Three." | Clatter montage | `video.scene` |
| 4 | 14-19 | Close, the slab's end | The row has run out of slab; a diamond left over; the exhale; "Not enough." | Exhale, line | `video.scene` |
| 5 | 19-24 | Close on the sack's bottom | The empty sack; he turns it over; nothing. "Someone has been keeping some." | Line, silence | `video.keyframes` |
| 6 | 24-30 | Low angle, his eyes, then up the shaft | Red eyes rise; far above, the pinpoint of daylight; held 3 s | Announcer, "Quiet.", drone cut | `video.scene`, `video.extend` for the hold |
| 7 | 30-32 | Black, end card | Fade | Music stinger | `video.compose` |

Music: one low drone with a slow bell, a single sting on the daylight (`audio.music`, 30 s).

**Canon check.** The Overlord: obsidian, red eyes, black horns, slow, few words. No work joke (counting treasure is
a villain's pastime, not a job); no slime enemy (the sack's missing diamond is Yooba's mischief). Announcer is
Hell's voice, loving a show.

---

## Video 6: Lap 47,212 (the Shades' endless walk)

**Logline.** Hell is repetition, and nowhere is it plainer than in the Gloomwood. A Shade takes the same walk it
has taken for ever. The film follows one lap. On the next lap it is exactly the same, including the apology.

**Where it plays.** After the first Shade floor (Floor 700). The player has just been told, by a sign, that a lap
count exists; now they see why. It is the clearest picture of what "stuck in the loop" means, and what the hero's
rewind does differently: he can walk it backward.

**Tone notes.** Polite, doomed, funny. Two Shades meet in the lane and apologise and step aside, in the same order
on every lap (Story.md, Circle VII *Comedy angle*). The hero is the only one who notices, and the only thing he says
is flat.

**Script** (voices: Shades (no words), Hero, Announcer)
> *A dead forest at dusk; torches on poles mark a lane. A faceless Shade in a coat walks the lane at a steady, careful pace.*
> *A sign nailed to a tree: LAP 47,211. A second Shade, carrying the torch, stands at the end. They meet.*
> SHADE A (a hum, one note, apologetic): Mmm.
> SHADE B (a hum, one note, higher): Mm-mm.
> *A bow. A step aside. They pass. The sign flips: LAP 47,212.*
> *Cut to the same moment from a different angle: the identical bow, the identical step, the identical pass. The sign flips: LAP 47,213.*
> *The hero, at the edge of the lane, arms folded, watches the third time.*
> HERO: ... I've seen this.
> *A fourth lap, played at double speed, the Shades' hums running into each other like a stuck record.*
> *He lifts a hand (the rewind's amber blur); the lap runs backward; the Shades walk backward, still bowing. The sign counts down: LAP 47,212. 47,211.*
> HERO: Same.
> ANNOUNCER (cheerfully): LAP FORTY-SEVEN THOUSAND, AND COUNTING!
> *The Shade bows to the camera. Cut.*

**Shot list**

| # | Time | Shot | Action / camera | Sound | Recipe |
|---|---|---|---|---|---|
| 1 | 0-5 | Wide, the lane | Torch poles, fog, one Shade pacing; sign LAP 47,211 | Footsteps, wind | `video.scene` |
| 2 | 5-11 | Two-shot, low | The two Shades meet; hum / hum; bow; step; pass | The two hums | `video.keyframes` (bow on 8 s) |
| 3 | 11-14 | Insert, the sign | Flips to 47,212 | Wooden click | `video.i2v` (sign loop) |
| 4 | 14-18 | Same shot, other angle | The identical bow and pass; LAP 47,213 | Same hums | `video.scene`, same keyframes mirrored |
| 5 | 18-22 | Medium, the hero at the lane's edge | Arms folded; "I've seen this." | Line | `video.perform` (close) |
| 6 | 22-27 | Wide, double speed | A lap at 2x; hums overlap | Hum layers | `video.interpolate` of shot 4 |
| 7 | 27-31 | Wide, reversed | The rewind blur; Shades walk back; the sign counts down | Reversed hum, rewind whoosh | `video.compose` (reverse of 4/6 + overlay) |
| 8 | 31-32 | Close, Shade A | Bows to camera; cut | Announcer, hum | `video.scene` |

Music: a choir of Shades humming one note, a big hollow drum every lap (the Hollow King's theme's cousin).

**Canon check.** Shades are the damned (never slimes). Hero's rewind is Déjà Vu: grabbing Hell's loop and dragging
it backward. No office or work joke: the gag is politeness and a counter.

---

## Video 7: Market Day (the village and Yooba)

**Logline.** Yooba remembers the village the way he tells everything: far too fast, a bit too pleased with
himself. A morning in the meadow before the Overlord: the neighbours' slimes baking, the diamonds being polished for
the harvest dance, the hero asleep through all of it. Then Yooba swallows a handful, just in case. It is a love
letter to the thing the hero is climbing back to.

**Where it plays.** After Floor 959, *The Rest of the Village*, where the hero has found what is left of it. The
player has seen the wreckage; this shows what it was. The Shaft's next line from Yooba picks up from here.

**Tone notes.** Warm, gossipy, quick. **Slimes are villagers**: the baker is a slime, the neighbours are slimes and
people and nobody is afraid of anyone. The hero is in it, asleep, in his armour, tired of everything including
the festivities. Yooba narrates as a voice-over and breaks into the picture twice. No office jokes (nobody "clocks in"):
the village is bakers, a ferryman, a woman who sings to her hens, a dance.

**Script** (voices: Yooba (narration and in-picture), Hero, Announcer)
> *The meadow at dawn, painted bright. Smoke from three chimneys. Hens. A slime in an apron pulls loaves from an oven.*
> YOOBA (V.O., very fast): Okay so this is the village, right, this is the *good* part, you have to picture it, everything is fine, it's a nice afternoon-ish, nobody has been lasered—
> *A slime child bounces after a hen. A man carries a sack of diamonds to the polishing bench, humming.*
> YOOBA (V.O.): —and these are the village's diamonds, we polish them once a year for the dance, and I'm not saying I'm the *best* polisher—
> *Yooba himself bounces into frame with a polishing rag, gleaming, pleased.*
> YOOBA: —but I'm the best polisher.
> *Pan across the meadow: the neighbours' hut, the hero's hut. Through the window: the hero asleep in his armour, a rocket launcher against the wall, a snore rattling his visor.*
> YOOBA (V.O.): That's him. Never comes to the dance. Says it's loud. It's *music*.
> *A shadow crosses the meadow. The hens look up. The sky goes red at the edge.*
> YOOBA (V.O., slower): ... And then the sky did the thing.
> *Yooba, alone at the bench with the sack, looks up. Back at the sack. Gulp: he swallows a handful of diamonds, cheeks bulging.*
> YOOBA (muffled): Hold these.
> *Cut to the Landing: Yooba on a ledge of ash, holding up a gleaming diamond to the hero, who stares at it.*
> HERO: ... You've been carrying those the whole time.
> YOOBA: ... Yes?
> ANNOUNCER (pleased): THE VILLAGE: IT WAS A VERY NICE VILLAGE!

**Shot list**

| # | Time | Shot | Action / camera | Sound | Recipe |
|---|---|---|---|---|---|
| 1 | 0-5 | Wide, the meadow at dawn | Smoke, hens, the baker slime at the oven | Birds, morning, Yooba V.O. | `video.scene` |
| 2 | 5-10 | Medium, the polishing bench | A man hums; a slime child chases a hen across frame | V.O. continues | `video.scene` |
| 3 | 10-15 | Medium, Yooba with the rag | Yooba bounces in, gleaming, pleased; the beat-line | Yooba line, rag squeak | `video.perform` |
| 4 | 15-20 | Slow pan | Neighbours' hut, the hero's hut, the hero asleep through the window; the snore | Snore, V.O. line | `video.keyframes` (snore at 17 s) |
| 5 | 20-26 | Wide, sky | The shadow crosses; the hens look up; red at the horizon | Low drone, V.O. slows | `video.scene` |
| 6 | 26-31 | Close, Yooba at the bench | Looks up, back at the sack, gulps | Gulp, "Hold these." | `video.perform` |
| 7 | 31-36 | Two-shot, the Landing | The ash ledge; Yooba holds up the diamond; the hero's look | Lines | `video.scene` (PERFORM) |
| 8 | 36-38 | Wide, the village sunny, held; end | Fade | Announcer | `video.compose` |

Music: a bright village waltz in a minor key at the end (the dance that never happened).

**Canon check.** Yooba: green slime, two eyestalks, pink square teeth, chatty, gurgling; swallowed the diamonds to
hide them. Slimes are villagers; the baker and child are slimes, not enemies. The hero sleeps in his armour with his
launcher (Teaser lore). The hut and the ash-falling aftermath match *Teasers*. No office joke.

---

## Video 8: The Best Seat (the Magma King's backstory)

**Logline.** Before there was a throne, the Magma King was a lump of slag at the bottom of a lava river, who
discovered that when something warm walks by, it stops and looks. He has been collecting audiences ever since. He
never gets up because the best seat is the one the show comes to.

**Where it plays.** After Floor 901, once the hero has beaten him (it is the Warden's backstory, not a spoiler for
the fight, and the player now knows him). It sets up the next Warden by contrast: the Spire Fiend listens, the King
performs.

**Tone notes.** Smug, slow, warm. He chuckles (*"Mm-hm-hm."*), never shouts. The comedy is his taste: he rates
every challenger like a review. It is Hell's showmanship made personal.

**Script** (voices: Magma King, Announcer, Hero)
> *A lava river, deep, slow, orange. A dull lump of slag turns over at the bottom.*
> MAGMA KING (V.O., bubbling): Mm-hm-hm. ... In the beginning I was a rock.
> *A tiny salamander skitters across the surface. The lump rises. It looks.*
> MAGMA KING (V.O.): And something *looked at me*.
> *The salamander stops, looks at the lump, then at us. A nod. It leaves.*
> MAGMA KING (V.O.): That was my first review. Four stars. Too warm.
> *A montage: a lump with a face, then shoulders, then a lava crown; imps arrive and stand in rows; a throne grows under him, stacked slag, slowly.*
> MAGMA KING (V.O.): The Wastes started coming. I never went to *them*. I never get up. Why would I? The best seat comes to the show.
> *The throne stands at the top of the Heat Stairs. A tiny figure approaches: the hero, as a silhouette, climbing.*
> MAGMA KING: Mm-hm-hm. ... Come up, then.
> *Cut: a sign carved in slag: "SEATING: ONE."*
> *The hero, at the foot of the chair, looks up at the huge amused face.*
> HERO: ... You're not even the worst I've met.
> MAGMA KING: Mm. ... *(a pleased rumble)* ... Four stars.
> ANNOUNCER: THE MAGMA KING: UNDEFEATED IN SEATING!

**Shot list**

| # | Time | Shot | Action / camera | Sound | Recipe |
|---|---|---|---|---|---|
| 1 | 0-5 | Underlit, the lava river's bed | A slag lump turns over; bubbles | Bubbling, V.O. | `video.scene` |
| 2 | 5-10 | Surface shot | The salamander skitters; the lump rises; it stops; looks at us; nods | Lines | `video.keyframes` (the nod at 8 s) |
| 3 | 10-15 | Side, the montage | The lump grows face, shoulders, crown; imps in rows | Rising drone | `video.scene` |
| 4 | 15-21 | Time-lapse, the throne | Slag stacks into the throne under him; he settles | Stone grind, line | `video.i2v` / `video.extend` |
| 5 | 21-26 | Wide, the Heat Stairs | A small silhouette climbs; "Come up, then." | Line | `video.scene` |
| 6 | 26-29 | Insert | A slag-carved sign: SEATING: ONE | Click | `video.i2v` |
| 7 | 29-35 | Low, the hero at the chair | Looks up; the line; the King's pleased rumble; "Four stars." | Lines, Announcer | `video.scene` (PERFORM, 3 voices) |

Music: his slow brass theme (the Slag Throne's) in a lounge arrangement, one chuckle-shaped bass hit.

**Canon check.** The Magma King: huge king of slag and obsidian, lava crown, slouches on a slag throne he never
gets up from, smug, amused, proud, loves an audience, chuckles "Mm-hm-hm.", never shouts; imps are his guards.
"Seating" and "reviews" are showbiz, not office jokes; a throne is not an office. The ending
nods to Floor 901's beaten state: the hero has won, and the King rates him, as a showman would.

---

## What each video costs

Prices come from `hawkesgen_estimate` (project `sickofit`, 2026-10-25, median of past jobs; GPU at $1.861/h). The
recipe per video: a table read (`audio.dialogue`, 2 reads), 8 keyframes (`image.edit`), 6 shot takes (`video.scene`)
and 2 keyframe shots (`video.keyframes`), music (`audio.music`, 2 takes) and one upscale (`video.upscale`), best of
two on the shots that matter, then `video.review` (the critic; a few cents) and `video.compose` (free).

| Video | GPU work | GPU $ | API $ (voice, review) | Total | With the 20-min idle tail |
|---|---|---|---|---|---|
| 5 The Count | 1.8 h | $5.4 | $0.5 | **$5.9** | $9.6 |
| 6 Lap 47,212 | 1.8 h | $5.4 | $0.5 | **$5.9** | $9.6 |
| 7 Market Day | 1.8 h | $5.4 | $0.5 | **$5.9** | $9.6 |
| 8 The Best Seat | 1.8 h | $5.4 | $0.5 | **$5.9** | $9.6 |
| **All four in one batch** | 8.8 h GPU incl. loads | $16.4 | $1.9 | **$18.3** | $23.3 |

A batch shares each model's load and boot, so four together ($18.3) cost less than four alone ($23.6). Plan for one
redo of the two weakest shots per video (about +$1 each) and the critic's retakes: **budget about $30 for all four,
$7.50 each**. Wall time about 66 minutes for the batch (8 servers), 30 minutes for one. The prices above are
estimates: the keyframe shots have no job history yet (the tool's own guess), so they could move by ±30%.

Not started: no server was run, no clip made, no voice generated. What remains after J approves: voices and
table reads, the keyframes, the takes, the critic loop, then baking each into `Videos/VideoN.asset` + audio +
thumbnail, `videos/VideoN.json` + the `game.json` list, and the `story-path` scenario's checks.
