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

## Video 3: Hell's Ad Break (replaces the old Video3 footage)

Added 2026-10-25 (J: *concept A, "Hell's Ad Break"*). Plays after the Landing (`afterLevel: 2`, `nextLevel: 3`), where the old talking-slime
footage was; the story is Story.md, *Video3 is Hell's Ad Break*. 25-35 s, 1280x704 (16:9), 24 fps, hand-painted comic-fantasy
with ink outlines, the Landing's lava palette; a cable-TV frame (scanlines, bezel) over everything.

**Logline.** Hell runs a cable-TV ad for its own 1,000 floors, with a slime sponsor selling diamonds, and the hero, the only
one in the picture who is actually in it, is not buying.

**Script** (voices: Announcer `1xynpTfy7I462pPrU2kL`, Yooba `EdDvd8GSdCyZwUcsPawi`, Hero `DzkD7I4tf5i3KSaB4cz5`; table read 28.7 s)
> ANNOUNCER: Are you sick of it? GOOD! Because Hell has ONE THOUSAND FLOORS!
> ANNOUNCER: Fire! Fiends! Falling rocks! All of it, forever, now with MORE floors!
> YOOBA: And diamonds! Fresh, shiny, lightly swallowed! Get yours from Yooba, the best slime in Hell!
> HERO: You're selling me the hole I'm stuck in.
> ANNOUNCER: ACT NOW, and climb the WHOLE thing!
> YOOBA: Hee hee! He said yes!
> HERO: ...Great.

| # | Time | Shot | Action / camera | Recipe |
|---|---|---|---|---|
| 1 | 0-5.5 | Logo card, MS announcer | HELL'S AD BREAK / 1,000 FLOORS slams in, his mouth works; push-in | `video.scene` |
| 2 | 5.5-11.5 | WS, the hero small in a fire reel | words slam on: FIRE! FIENDS! FALLING ROCKS!; the hero shifts his launcher, unimpressed | `video.scene` |
| 3 | 11.5-19 | MCU Yooba, sponsor card | wobbles, talks fast, holds out a diamond; push-in | `video.scene` |
| 4 | 19-21.8 | MCU hero | eyes flare, flat; slow push | `video.scene` (his line is the cut-in) |
| 5 | 21.8-24.8 | Announcer, ACT NOW flash | flashing frame, mouth | `video.scene` |
| 6 | 24.8-27 | CU Yooba | laughs | `video.scene` |
| 7 | 27-30.2 | CU hero, held | a long beat; "...Great." is the last sound | `video.scene` |

Canon check: Yooba a seller and a villager; no Overlord; no office joke; the hero's "...Great." is his catchphrase.

**Status** (2026-10-25). J approved the animatic and ~$1.55 of GPU. The **final is rendered** (`"stage": "final"`, 29.7 s, 960x544, 24 fps): three
`video.scene` PERFORM scenes at full steps and 0.5 MP (A the announcer and the hero's wide, B Yooba's spot, C the hero, the host,
Yooba, the hero), the table read's stems as voice samples, a red horned showman for the announcer (`a_9fe39c547c00c53e`) and a studio set on the
Landing's lava shore (`a_9bd1f32a05980a09`), cut by `videos/hell-ad-break/build_final.sh` (scene C's Yooba cut keyed over the set plate, a slow push
on the wide, 0.6 s held before "...Great.", one loudness pass); `edit.json` and `prompts.json` hold the assets and prompts. Critic (`video.review`):
6.2 twice (fail; story 6.4, pacing 6.3, voices 6.5, lip sync 5.4, movement 5.0, consistency 6.2, sound 6.1). What is left is in the scenes
themselves: the hero stands frozen in A's wide, Yooba's lips drift on "lightly swallowed" in B. Re-takes cost ~$0.8-0.9 each.
**Not yet in the game**: the sheets and Addressables are baked in the Unity editor (see *Putting a film into the game*), so the game still plays the old Video3.

**Cost, what it really was.** The estimate said 770 GPU s per 8-11 s scene at 0.5 MP; the jobs took 1,500-1,700 s (**$0.78-0.88 each, $2.51 for the three**,
one restart of ComfyUI on A). Price a final `video.scene` at ~$0.85 per scene, not $0.45.

### Putting a film into the game

1. `python3 Tools/Video/prepare_lore_video.py . <cut.mp4> Video3 "<display name>" --fps 12 --root <videoRoot>` (ffmpeg only): writes
   `Assets/Videos/Video3.mp4`, `Video3_Audio.mp3`, `Video3_FirstFrame.png`, a copy at the baker's source (`<videoRoot>\Video3\Output\Video3.mp4`)
   and sets `videos/Video3.json` `displayName` and `timeline.duration`. 12 fps for dialogue; 30 s is 363 frames = 13 sheets of 4x7 cells of 512x284 (the baker lays them out).
2. On the machine with Unity (editor closed): `Unity.exe -batchmode -nographics -projectPath <project> -logFile - -executeMethod SickOfIt.Tools.Editor.Generation.GenerateProjectWindow.RegenerateClipSheets -clip Video3 -fps 12`.
   It sets the asset's `playbackFPS` first (a bake reads it), then packs the sheets in place (GUIDs stay on their Addressables entries, extra old sheets are deleted).
3. Commit the sheets, `Video3.asset` and the Addressables group changes (Git LFS), run the `video3`, `yooba` and `story-path` playtests, then the deploy's Addressables build.
Agents' servers have no Unity and no `git lfs`: an agent makes the cut and the config; the bake is a build-server step.

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


---
---

# Part 2: the cinematic plan (titles, boss stings, Shaft bridges, epilogues)

Added 2026-10-25 (J's task: *extend the lore video plan with a short title cinematic per Circle, a boss intro sting for
each Warden and mid-boss, a Shaft descent/ascent bridge clip, and the epilogue for the true ending and the funny ending*).
**Design and prices only: nothing is generated, no server was started.** The memory note m_e3fc0457efb4 says GPU is approved
at any cost for Sick Of It, but this task's brief set a GPU budget of none, so the clips wait for J's go (the price is at the
end). Tone for all of it: absurd comedy from Hell itself, **Hell is repetition**, the deadpan hero, the booming announcer;
no office or work jokes; slimes are villagers. Nothing here spoils the Overlord's origin (*The Overlord: who he is*) beyond
what each ending already shows, and the website carries none of it (no finale spoilers).

| Group | Count | Length | Files | Plays |
|---|---|---|---|---|
| **Circle titles** | 10 | ~8 s | `Video9`-`Video18` (`CircleTitleX` ... `CircleTitleI`) | on the Shaft as each Circle wakes |
| **Boss stings** | 30 (10 Wardens, 20 floor bosses) | 6 s / 4 s | `Videos/Sting_<entityId>.asset` | in the boss's arena intro |
| **Shaft bridges** | 2 | 12 s / 10 s | `Video19` (Descent), `Video20` (Ascent) | after Video2; in the true ending |
| **Epilogues** | 2 | ~50 s / ~36 s | `Video21` (Knock), `Video22` (Encore) | after each ending |

Voices are the existing ones (Announcer `1xynpTfy7I462pPrU2kL`, Hero `DzkD7I4tf5i3KSaB4cz5`, Overlord, Yooba, Magma King); the
other Wardens' and floor bosses' short lines are from Story.md's Circle sections and are voiced when their Circle is built.
Look and format as in the table at the top (1280x704, 24 fps, each in its Circle's palette); the Circles' look briefs are in
Story.md. Recipes: `video.scene`, `video.keyframes`, `video.compose`, `video.review`, as in Part 1.

## Where they sit in the flow

```
Video1 > Level 1 > portal > Video2 > [Video19 Descent] > Shaft plunge > [Title X] > Floor 1000 ...
 Floor 991 / 951 ... each floor boss:  arena intro starts with its 4 s STING, then the fight
 Warden floor (901, 801 ... 101, Floor 1): arena intro starts with its 6 s STING
 Warden beaten > Waystone > Shaft > [Video8 etc. if due] > [Title of the Circle above] > Continue
 Floor 1 beaten > SHUT IT > ... Yooba on the Meadow > [Video20 Ascent] > the sit > credits > [Video21 Knock] > FLOOR 1,001
 Floor 1 beaten > SIT DOWN > the queue > credits take > [Video22 Encore] > Landing, New Game+
```

Rules: all skippable with Escape (a sting is skipped with the same key as the intro), each plays **once per save** (stings once
per boss, so a retry after a death goes straight to the fight; a rewind never replays one) and every title/bridge/epilogue is
watchable again from the video select (stings from the Codex's Bosses tab, once met). A Shaft visit plays at most one title; if a
story video is due too (Video8 after Level 31) the title plays last, so the climb is the last thing on screen. The Circle titles
for Circles II and I and the epilogues are the only ones that spoil anything, so they unlock in the video select only after the
ending. **Engine work (not built, small):** a `playClip` scene action for stings and a `Video` entry per title/bridge/epilogue
in `videos/` + game.json `videos` + `afterLevel`/`nextLevel`; the baked sheets and audio come from the clips. None of that can be
wired before the clips exist, so a `story-path` check per clip is added when they are baked.

## Circle title cinematics (10, ~8 s each)

One shape, ten times, which is the joke: *Hell is repetition, so every Circle gets the same title card, and each one is
slightly more tired of it.* 0-3 s a slow push through the Circle's biome, 3-6 s the card slams in (CIRCLE ROMAN NUMERAL, name,
floor range, in the game's HUD font), 6-8 s a Circle-specific gag, a button of music, cut. Announcer reads the card; the gag is
one hero line or one sound. Shots: `video.scene` x2 (establishing, gag) plus 3 keyframes (`image.edit`), a 10 s music sting
(`audio.music`), `video.compose` for the card. Plays on the Shaft as the Circle wakes.

| # | Circle | Biome shot | Announcer | Gag (6-8 s) | Plays after |
|---|---|---|---|---|---|
| 9 | **X, the Ember Wastes** | Lava river, imps with sacks crossing a ledge | *"CIRCLE TEN! THE EMBER WASTES! NINE HUNDRED NINETY-NINE TO GO!"* | An imp carries a sack past, stops, looks at us, carries it back the way it came. Hero: *"...Great."* | the Shaft's first plunge |
| 10 | **IX, the Cracked Road** | Black spires, glowing seams, the road splitting | *"CIRCLE NINE! THE CRACKED ROAD! KEEP TO THE ROAD!"* | The road cracks under the title card; the card falls off. | Floor 901 (with Video8 first) |
| 11 | **VIII, the Rune Bridges** | Violet void, thin bridges, floating eyes | *"CIRCLE EIGHT! THE RUNE BRIDGES! PLEASE STAY ON THEM!"* | Every eye in the void turns to the card at once, then to us. | Floor 801 |
| 12 | **VII, the Gloomwood** | Dead forest, torch lane, a Shade pacing | *"CIRCLE SEVEN! THE GLOOMWOOD! YOU'VE BEEN HERE BEFORE!"* | The same Shade crosses the card twice, identically, and the card hasn't moved. Hero: *"Same."* | Floor 701 |
| 13 | **VI, the Mire** | Black water, tilted headstones, green lights | *"CIRCLE SIX! THE MIRE! IT'S A SWAMP! IT'S NOT A SLIME!"* | A ghost light drifts in: *"Over here! ...No, other here. Sorry."* | Floor 601 |
| 14 | **V, the Caverns** | Pink-grey gum-stone, drool, breathing walls | *"CIRCLE FIVE! THE CAVERNS! HALFWAY! WELL DONE!"* | The cave inhales; the card slides toward the throat; it exhales; it slides back. | Floor 501 |
| 15 | **IV, the Dead Peaks** | Moonlit crags, candle shrines, the Bell Tower far off | *"CIRCLE FOUR! THE DEAD PEAKS! MIND THE BELL!"* | The bell tolls; the card shakes; a candle goes out; a gargoyle opens one eye. | Floor 401 |
| 16 | **III, the Bastion** | Iron-grey walls, banners, a closed gate | *"CIRCLE THREE! THE BASTION! EVERY GATE IS SHUT!"* | A portcullis drops over the card with a clang; a tiny hatch opens in it. | Floor 301 |
| 17 | **II, the Keep** | Dungeon rows, hanging chains, torches | *"CIRCLE TWO! THE KEEP! HANDS WHERE WE CAN SEE THEM!"* | The card swings on a chain; a jailer fusses through two hundred keys for the right one. | Floor 201 |
| 18 | **I, the Throne Stair** | Black glass stair, red inlay, a seam of daylight at the top | *"CIRCLE ONE! THE THRONE STAIR! ...HE'S EXPECTING YOU."* (the only one with no joke: a long hold on the seam of daylight; the draught lifts the card's edge) | | Floor 101 |

Canon check: each Circle's biome, Warden and enemy family follow *The Circles* table and its section; the hero line is one of
his stock one-liners; nothing in these asks a question the Story answers later.

## Boss intro stings (30, 4-6 s each)

A sting is a short clip that opens the boss's arena intro, before the boss bar fills: one hard shot of the boss, a line, the name
card (`NAME` and `FLOOR n` in the HUD font) and a hit of sound. It uses the boss's `art/` sheets as the keyframe (`image.edit`
for the pose), one `video.scene` of 4-6 s, the line (existing voice if the boss has one, else the announcer) and a shared
stinger (`audio.sfx`, cents). **Warden stings are 6 s** with the Warden's theme's first bar; **floor-boss stings are 4 s**. The
announcer's name call is the sting's last second and replaces the text banner so the fight starts on time. Placement: the
level's intro sequence (`bossIntro`), once per save per boss.

| Floor | Boss (entity) | Kind | Shot (the single beat) | Line |
|---|---|---|---|---|
| 991 | Imp Foreman (`impForeman`) | floor | Fat imp on his sack, whip cracking the air | Announcer: *"THE FOREMAN! HE'S VERY LARGE ABOUT IT!"* |
| 951 | Pile Keeper (`pileKeeper`) | floor | Keeper rising out of the sack pile, diamonds sliding off him | Announcer: *"THE PILE KEEPER! SOMETHING IS MISSING!"* |
| 901 | **Magma King** (`magmaKing`) | Warden | Slag throne, lava crown, he doesn't stand; one slow eye opens | King: *"Mm-hm-hm. ... Come up."* |
| 891 | Road Breaker (`roadBreaker`) | floor | A slab of road smashing down the frame | Announcer: *"THE ROAD BREAKER! IT'S BROKEN. HE BROKE IT."* |
| 851 | Carver (`carver`) | floor | A chisel carving the boss name into the wall; he stands back, pleased | Announcer: *"THE CARVER! HE'S SPELLED YOU WRONG!"* |
| 801 | **Spire Fiend** (`spireFiend`) | Warden | The great spire; a gaunt face in the stone turns toward us, listening | Fiend (whisper): *"...There you are."* |
| 791 | Keystone (`keystone`) | floor | A stone arch; a rune on its brow lights | Keystone: *"Load. ...Bearing."* |
| 751 | Lamplighter (`lamplighter`) | floor | A hunched wraith on a pole lights a rune, then puts it out | Announcer: *"THE LAMPLIGHTER! LIGHTS ON! LIGHTS OFF!"* |
| 701 | **Bridge Wraith** (`bridgeWraith`) | Warden | Hooded wraith, a lantern of blue runes, drifting over the void | Wraith: *"Mind the... oh. Do mind it."* |
| 691 | Pathfinder (`pathfinder`) | floor | Huge pilgrim with a lantern walking a ring, passing the same post | Pathfinder: *"Is this the way? ...It's always the way."* |
| 651 | Woodsman (`woodsman`) | floor | An axe falling on a tree that does not fall | Announcer: *"THE WOODSMAN! HE'S ALMOST DONE."* |
| 601 | **Hollow King** (`hollowKing`) | Warden | A dead tree; yellow eyes open in the trunk; a crown of branches | Hollow King (hollow, faint): *"Again."* |
| 591 | Gravedigger (`gravedigger`) | floor | A spade lifting earth under the camera | Gravedigger: *"Nearly done."* |
| 551 | Ferryman (`ferryman`) | floor | A barge, a pole, a stern lantern | Announcer: *"THE FERRYMAN! NO PASSENGERS!"* |
| 501 | **Drowned Queen** (`drownedQueen`) | Warden | Soaked hair parting over a weed-wound crown, green lights rising | Queen: *"Stay for the tide."* |
| 491 | Baby Maw (`babyMaw`) | floor | A cub bouncing in a cradle of ribs, mouth open | Baby Maw: *"Maaa."* |
| 451 | Grand Molar (`grandMolar`) | floor | A huge tooth grinding into frame | Announcer: *"THE GRAND MOLAR! IT'S A TOOTH! IT MOVES!"* |
| 401 | **Mother Maw** (`motherMaw`) | Warden | The cave wall opens: teeth, drool, many small eyes | Maw: *"What's the matter, dear?"* |
| 391 | Cantor (`cantor`) | floor | Gargoyle drawing breath; a bell note rings out | Announcer: *"THE CANTOR! TAKE A NOTE!"* |
| 351 | Wax Abbot (`waxAbbot`) | floor | A kind abbot of wax, a candle on his head, dripping | Abbot: *"Bless... bless..."* |
| 301 | **Bell Ringer** (`bellRinger`) | Warden | The cracked iron bell swings; candles on the monk's shoulders gutter | Announcer: *"THE BELL RINGER! THE BELL IS FOR YOU!"* |
| 291 | Drill Sergeant (`drillSergeant`) | floor | A huge soldier mid-shout, rank behind him | Sergeant: *"Aaa-TEN-tion! ...Again!"* |
| 251 | Battering Ram (`batteringRam`) | floor | The ram coming up a ramp on the shoulders of the garrison | Announcer: *"THE RAM! IT'S ALL OF THEM!"* |
| 201 | **Gatekeeper** (`gatekeeper`) | Warden | A giant built like a gate; the portcullis visor lifts | Gatekeeper (formal): *"State your business. ...Denied."* |
| 191 | Turnkey (`turnkey`) | floor | A wheel of keys swung overhead | Turnkey: *"It's one of these."* |
| 151 | Oubliette Keeper (`oublietteKeeper`) | floor | Long pale arms and a lantern on a pole over a pit | Announcer: *"THE KEEPER! WATCH THE FLOOR!"* |
| 101 | **Chain King** (`chainKing`) | Warden | Jailer king in chains, spike crown, keys at his belt, counting under his breath | Chain King: *"One. ...You're one."* |
| 91 | Captain of the Guard (`guardCaptain`) | floor | A knight in a red cloak on a balcony, the draught lifting the cloak | Captain: *"Forgive me. It is the drill."* |
| 51 | Gate Smith (`gateSmith`) | floor | A hammer falling on a door that is never finished | Announcer: *"THE GATE SMITH! HE'S ALMOST DONE!"* |
| 1 | **The Overlord** (`overlord`) | Warden | The throne's back; the red eyes open; the door behind him is white | Overlord: *"Mm. ... You."* then *"Stay."* |

Canon check: every name, floor and line matches *Floor bosses* in each Circle's section; the Overlord's two lines are the ones the
`overlord` playtest already expects at his intro; nothing is a work joke (a Foreman, a Sergeant and a Captain are Hell's
rank, not office). The Overlord's sting has no new reveal.

## Shaft bridges (2)

### Video19: Descent (~12 s, after Video2, before the Shaft's live plunge)

**Idea.** Video2 ends in the lava river; the Shaft then plunges the camera past all ten Circles. The Descent is the film of that
fall, so the plunge is the first thing the player sees *as Hell*, and every Circle gets one frame of its look and one gag: the same
fall, ten times, faster. Hero falling, arms folded, unimpressed; the Circles slide past in order of the Story's table, each for about
a second: the Meadow (grass, a hen), then Circle I down to Circle X. Hell is repetition: in every Circle the same small silhouette
waves at him as he passes, identically.
- 0-3 s: the portal's scorch mark closing, the hero tumbling in; 3-9 s: ten Circle strips streak past (keyframes of the ten biomes, 0.6 s each, `video.keyframes` between pairs); 9-12 s: the lava river rushes up; the card FLOOR 1,000 slams.
- Hero (V.O.): *"Are we there yet."* Announcer: *"WELCOME TO THE BOTTOM!"* (the Shaft then says the rest of the line live.)
- Sound: one rising whoosh, a tiny hen cluck; music cue from `audio.music`.

### Video20: Ascent (~10 s, in the true ending between the Meadow's arrival and the sit)

**Idea.** The true ending has the hero climb *through the closing light* to the Meadow. The Ascent is that climb: the Shaft in
reverse, the ten Circles falling away beneath him in daylight (Circle I's stair, the Keep's chains slack, the Bastion's gates
open, down to the Wastes' lava going dark), the Waystones going out one by one on the wall as he passes (the loop broke), and
finally the green of the grass. No jokes, a quiet hold; Yooba's voice, from above, a long way off: *"...Is that him?"*
- 0-6 s: the climb, Waystones darkening in order; 6-9 s: the daylight; 9-10 s: grass. Music: the Meadow's theme, finally in tune.
- Plays once, only after SHUT IT; the funny ending has no ascent (nobody left).

## Epilogues (2)

### Video21: Knock (the true ending, ~50 s, after the credits)

The Story's own epilogue idea ("Knock"), made into a plan. Painted like the portal scene, near-silent, nothing from Hell on screen.
Plays after the credits; leads straight to the "FLOOR 1,001" card, which is its last shot (so the post-credit sting is this
video's end, not a second clip).

**Script.** *(Narration none. Lines: the Hero's `Not today.` and the voice behind the door, `...Hello?`, the hero's own word.)*

| # | Time | Shot | Action / camera | Sound | Recipe |
|---|---|---|---|---|---|
| 1 | 0-6 | The meadow at dawn, ash on the grass | Time-lapse, the grass comes up in a green wave over the portal's scorch mark | Birds returning, wind | `video.scene` (Level 1's footage as the keyframe) |
| 2 | 6-12 | Neighbours' huts, new thatch going onto the hero's, a row of hands | Diamonds in a heap on the fence, glinting | Thatch rustle, clink | `video.scene` |
| 3 | 12-17 | The hero sitting on the grass against a fence post, eyes shut, visor up; Yooba asleep beside him, wobbling | Slow push in | Breathing, a gurgle | `video.keyframes` |
| 4 | 17-21 | Close on a blade of grass, then a pebble beside it, trembling | Rack focus | One low thump | `video.scene` |
| 5 | 21-27 | Three knocks from under the ground, a pause, a fourth, polite | Nobody moves; the hens look at the ground | Knocks (`audio.sfx`) | `video.scene` |
| 6 | 27-32 | The hero opens one eye; orange glows in the visor | He looks at the ground a long beat | Held silence | `video.keyframes` |
| 7 | 32-37 | He closes the eye; the orange goes out; Yooba burps a diamond | Hero (murmur): *"Not today."* | Burp (`audio.sfx`) | `video.scene` |
| 8 | 37-44 | Cut to black; the white outline of a door drawn in the dark; a hand knocking; title FLOOR 1,001 | Hold | Knock, once | `video.keyframes` |
| 9 | 44-50 | Black | A small tired voice: *"...Hello?"* | Voice only | audio |

Music: none until shot 7, then one low piano note. Canon check: matches *True ending* steps 4-5 and the Acts table; the hero never
rises (the funny ending is the other path); Yooba is a villager asleep, the diamonds are the village's.

### Video22: Encore (the funny ending, ~36 s, replaces the credits' last beat before the cut to the Landing)

The funny ending's credits are one long take of everyone sitting in the daylight; Encore is that take as a film, ending on the loop.
It is the *funny* answer to Knock: the same quiet, but because there is nothing left to climb.

**Script.** *(Lines: the Announcer, the hero, the Overlord, Yooba.)*

| # | Time | Shot | Action / camera | Sound | Recipe |
|---|---|---|---|---|---|
| 1 | 0-5 | The throne room, the gate shut by itself, the diamonds in a pile at its foot | Slow pull back, nobody moving | Gate thunk, a clink | `video.scene` |
| 2 | 5-12 | The hero in the throne, arms folded; the Overlord on the floor against its leg, the visor in his lap | Overlord: *"Mm. ... That's my chair."* Hero: *"Was."* | Lines | `video.keyframes` |
| 3 | 12-22 | The Wardens arrive up the stair one by one and sit: the Magma King on a slab, the Spire Fiend on a step, the Bridge Wraith apologising, the Hollow King, the Drowned Queen, the Mother Maw small and shy, the Bell Ringer, the Gatekeeper sitting *correctly*, the Chain King counting them, the Imp Foreman on his sack | Pan along the row, each a one-second beat | Footsteps, chairs of stone | `video.scene` x3 + `video.compose` |
| 4 | 22-27 | Yooba in the corner coughs a diamond into each lap, then looks at the pile | Yooba: *"Shop's open."* | Cough, clink | `video.scene` |
| 5 | 27-31 | Announcer, to nobody: *"...THE CROWD DOESN'T KNOW WHAT TO DO."* | Held wide: everyone sits | Silence | `video.keyframes` |
| 6 | 31-36 | Cut to the Landing: lava, the portal's flash | Announcer (rested, at full volume): *"WELCOME TO THE BOTTOM. NINE HUNDRED NINETY-NINE TO GO."* Hero (off-screen): *"...Great."* | The game's first lines | `video.compose` (re-uses Video2's lava footage) |

Music: the Throne's theme in a lounge arrangement, ending on the Landing's drone. It has no knock and no Floor 1,001
(the funny ending's rule), and the save then starts New Game+. Canon check: the Wardens in the queue are exactly Story.md's list.

## What it all costs (`hawkesgen_estimate`, project `sickofit`, 2026-10-25, nothing started)

Counts: 80 keyframes (`image.edit`), 58 `video.scene` shots, 12 `video.keyframes` shots, 16 `audio.music` tracks (10 title
stings, 2 bridge cues, 2 epilogue tracks, 2 retakes), 4 table reads (`audio.dialogue`), 16 upscales (titles, bridges, epilogues).

| Group | GPU work | GPU $ (before loads) | Notes |
|---|---|---|---|
| 10 Circle titles | 4.7 h | ~$8.7 | 3 keyframes + 2 shots + music + upscale each (~$0.87) |
| 10 Warden stings | 2.2 h | ~$4.1 | 1 keyframe + 1 shot each (~$0.41) |
| 20 floor-boss stings | 4.4 h | ~$8.1 | ~$0.40 each |
| 2 Shaft bridges | 0.9 h | ~$1.7 | ~$0.87 each |
| Knock | 2.1 h | ~$3.9 | 12 keyframes, 8 keyframe shots |
| Encore | 1.9 h | ~$3.6 | 10 keyframes, 4 shots + 4 keyframe shots |
| **Whole batch** | 17.2 h incl. 3,200 s of model loads and 480 s of boots | **$32.0 GPU + $1.0 API = $32.9** | **$37.9 with the 20-minute idle tail**; 8 servers, ~132 min wall |

Plan for **about $50 all in** with the critic's retakes (two shots a clip on the ones that matter) and ±30% on the keyframe
shots (`video.keyframes` has no job history: the tool's own guess). Part 1's four films are $18.3 on top, **about $68 for every
lore and cinematic clip**. The boss lines not already recorded (about 25) are voice at cents each, no GPU. If J wants less:
the 30 stings and the 10 titles are independent; the epilogues and bridges alone are ~$11 ($12 with the idle tail).


---

## Status of Videos 5, 7 and 8 (2026-10-25)

Animatics made **without GPU** (`videos/the-count`, `videos/market-day`, `videos/best-seat`, built by `videos/lore-kit/build_animatic.py`):
the game's own art (Magma King and Imp Foreman sheets, Yooba's talk/laugh sheets, the hero's Shout sheet), the **real table-read voices**
(`audio.dialogue`, stems kept) and ffmpeg moves; lines are subtitled. Mouths: Yooba's sheet cells cycle on his lines, the announcer's
drawn mouth works on his, the King's and Overlord's eyes and crown flare and the helmed hero's eye slits flare, only while each speaks.
Lengths follow the table read: The Count 26.7 s, Market Day 34.2 s, The Best Seat 45.6 s (the King's V.O. runs longer than the 35 s design).
Waiting on J's notes, then (GPU approval): Lightning scenes, finals, bake into `Videos/VideoN.asset`, `videos/VideoN.json` + game.json list
(`afterLevel` 21 / 13 / 31), and the `story-path` checks. Stand-ins until then: the Overlord is not drawn (eyes, a finger, a diamond row) and the imp is the Foreman's sheet.
