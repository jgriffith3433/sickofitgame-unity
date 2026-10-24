# Work for Sick Of It

You are fixing a problem Hawkes Studio's automated playtest found in Sick Of It (A 2D arcade bullet-dodger (Unity WebGL) with rewind: a sarcastic nobody chases the Overlord up 1,000 floors of Hell. Hell is repetition, so rewind is the hero's power. Slimes are villagers, never enemies. No office or work jokes.).


The website (home page, blog, privacy page, the game page) is a separate repo, sickofitgamewebsite, with its own beta. This change
works in the game's source only; if the request is really about those pages, say so in the summary (a task with target
"site" works there).

## What J asked for

Make the video "Hello? (how the hero ended up in Hell)" (short: vertical 1080x1920, 15-60 s, for TikTok, YouTube Shorts and Reels). The video skill (below) is how videos are made here:
the story and a beat sheet first, then an animatic (a rough draft: the real voices, the art and the staging, timed) that J approves or steers, then the final: a table read of the whole scene first (audio.dialogue), then whole scenes generated from the characters'
references (video.scene) by default, with several takes of the important scenes picked by the critic (video.review),
and Blender and ComfyUI for shots that need more, within the GPU budget.

## J's script
The meadow village at dusk, still smoking. Imps lie scattered and beaten. The Overlord, a giant obsidian titan with red eyes, turns away without hurrying. His lasers rake the ground behind him and burn a perfect glowing circle into the meadow. He's gone.

The Hero (thirties, unimpressed) walks over and looks down into it.
HERO: That's a hole.
(A beat. An imp's horn rolls past his foot and drops in. Silence. Then, far below, a tiny clink.)
HERO: So that's where they keep coming from.
(He leans over the edge.)
HERO: Hello?
HELL (echoing back, three times, each one deeper and closer): Hello? ... Hello? ... HELLO.
(He looks at the camera.)
HERO: Nope.
(The hole inhales. He's sucked in mid-step, arms out, still annoyed.)
(Silence. Then Yooba, the village's little green slime, bounces into frame, cheeks bulging with diamonds, peering into the hole.)
YOOBA: (muffled, mouth full) Mmf. Wait for meee—
(SHLURP. Yooba is slurped in after him. The circle seals shut. One diamond rolls out and stops.)
ANNOUNCER (booming 90s arcade voice): FLOOR ONE THOUSAND!
END CARD: Sick Of It. Made with Hawkes Gen · hawkesgen.com

## Direction
A comedy beat built on timing: the hero's deadpan against Hell's absurd echo. Let the silences land (the clink, the beat before "Nope."). Slimes are villagers, never enemies; Yooba is chatty and wet. No office or work jokes. The house style is the game's: dark warm pixel-art Hell glow meets the green meadow.

## J's controls (follow them)
- Length: about 30 s.
- Voices: Hero: the game's hero voice, dry. Overlord: no lines. Yooba: his voice (yooba, EdDvd8GSdCyZwUcsPawi). Hell's echo: deepening each time. Announcer: Hell's booming 90s arcade voice.
- Quality: standard (about 0.5 MP, upscaled to the format).
- Music: Silence for the first half; a low Hell drone swells only after "Nope."

Keep J's lines as written (fix only obvious typos). Cast each character with their own voice. Leave the finished cut's
Gen asset and its edit in .hawkes/video.json; J screens it in Studio and his notes come back to you as revisions.

Work out what that means in this project (read the story doc and AGENTS.md), make it well, and keep it consistent with
the product's tone and style. If it's too big for one change, do a solid first part and say what's left in the summary.

## GPU budget: $6.03 (approved)

You may generate through Hawkes Gen up to that: queue each kind of job as one batch (one model load), reuse what
the library already has (`reuse`, asset manifests), and report the GPU minutes and dollars spent (`hawkesgen_jobs`).
Never spend past it: if it won't fit, make the most important part and say what the rest would cost, or ask (a
question) for more.

You can't start GPU servers yourself (that needs Gen's owner role), and you don't need to: when no GPU server is
running, Studio has started a stopped one for you as this change began (it takes a few minutes to boot, and stops
itself after Gen's idle time). Ignore Gen's "only J can start one" note (Studio already did; don't ask J), and never
launch a server.

**Don't wait for GPU jobs in your session.** Do the parts that need no GPU first, then:
1. Queue your Gen jobs (`hawkesgen_run` with `wait_for_server: true`; one batch per kind of job). Gen holds them
   until a server is ready.
2. Commit any work that doesn't need their outputs.
3. Write `.hawkes/waiting-jobs.txt`: one Gen job id per line, each followed by a note of what to do with its output
   (e.g. `j_abc123 portal open clip: judge, then bake into Assets/Art/Portal`).
4. End your session (a short `.hawkes/summary.md` of what's done so far).

Studio watches those jobs (they may take hours if servers are busy) and resumes this same session with their results
(ids, statuses, asset ids) once every one has finished; then you judge, bake and commit. That isn't a failure and
costs no try.

If you ship stand-in or interim art, or fallback code, because the GPU art isn't made yet, list what is still to make
in `.hawkes/pending-gpu.txt` (one line each: the piece, what it needs, and its Hawkes Gen job ids). Then the task doesn't
count as done: Studio runs it again (not as a try) once a GPU server is up, until the real art lands. Delete that file
when nothing is left to make.

## This is a video (J screens it)

Follow the video skill below (every harness gets it here; Claude Code can also load it as `video`). No playtest and
no beta check judge this: J watches each version in Studio's Screening tab and either approves it or sends notes, which
come back to you here as a revision of this same work.

- Keep the video's source in this repo under `videos/<slug>/`: `script.md` (J's script, as written), `video.json`
  (cast and voices, shots, the cut's timeline, every Gen job and asset id). Commit it each pass.
- Render with Hawkes Gen (`video.compose` for the cut). Leave the finished cut in `.hawkes/video.json`:
  `{ "asset": "a_...", "poster": "a_...", "seconds": 31.5, "summary": "what changed this version", "caption": "post text with hashtags", "stage": "animatic" or "final", "story": "the logline, wants, turn and payoff", "beats": [the beat sheet], "edit": { the cut as video.compose's timeline with Gen asset ids } }`.
  Your first pass is the animatic unless the request says J turned it off; the final comes after J approves it.
  **Always hand over the `edit`** (the skill's "The edit"): it's how J's timeline notes find your shots and how Studio makes
  his quick edits (cut, speed, zoom) without you.
  If you can't put the cut into Gen yourself, give its path in this worktree instead (`"file": "videos/<slug>/out/cut.mp4"`,
  `"poster": "videos/<slug>/out/poster.png"`): Studio uploads it. Without one of them there is nothing for J to watch.
- On a revision: answer every note, change only what the notes are about, and say per note what you did in `summary`.
  J's notes come from the Screening timeline: `[v3 · 0:12.4-0:15.0 · edit clips 4, 5 · beats 6 "ots_mech" (...)]` names
  exactly which version, seconds, clips of your edit and beats he means, then what he asked for and his words. "Studio made
  a quick edit" lines are edits J already made: take them into your edit (don't undo them).
- Versions so far: v1 a_713a62150b34140d (Animatic v1 with the low Hell drone added: silent until after "Nope." (swells in from 21.5 s, ducked under the voices). ); v2 a_dcd4459c54299d84 (v2 restage answering Studio's review: Overlord now walks away (2.5 s) then the ring burns into an empty meadow with beat); v3 a_a172d9aa62beed08 (v3 answers all 7 notes of Studio's v2 review. 1) Echoes: three distinct escalating shots (OTS hero at the rim with a cal); v4 a_a1986393ab101a28 (FINAL: J approved animatic v3, so story, beats, staging, voices and timing are unchanged. Every clip was upscaled (Real-); v5 a_189e28309c51a505 (v6, answering Studio's review of v4. Cause of the audio slip: the upscaled clips still carried the sound baked into the ).

### The video skill: Making a video

J writes the script. You direct it: a **table read** first (the whole scene voiced in one take, section 1), then two
passes:

1. **The animatic** (your first pass, unless the request says J turned it off): the story, the beat sheet, the real
   voices, the characters' art in the house style and the staging, timed, as a rough cut: a Lightning-mode scene
   (section 5) or on-model stills held with moves. J, 2026-10-22: "I like the workflow of a rough draft first and the go
   ahead etc. This helps with also changing the voice, checking the art etc." So it must show the real voices and the real
   art, not placeholders: that's what he checks. Mark it `"stage": "animatic"`.
2. **The final**, once J approves it ("yes, go ahead, but with this guidance": his guidance and notes come with the
   go-ahead): every beat rendered at full quality with the strongest tool Hawkes Gen has, keeping the animatic's story,
   beats, staging and timing, and doing what the guidance says.

**J's controls** come in the request ("J's controls (follow them)"): length, look, voices, kind (the route, section 0),
method (which scene model, performances, his own performance video, or shot by shot), quality (draft / standard /
high), music, captions, end card. Follow them; what he leaves open is yours.

Studio reviews each version before J sees it and sends it back to you if it falls short. The reviewer is **the critic**
(Gen `video.review`: Gemini watches the cut with its sound and scores story, pacing, voices, lip sync, movement,
consistency and sound 1-10, with timecoded notes that land on J's timeline). J screens what passes; his notes come back
to you as revisions of the same work. Run the critic yourself before you hand anything over (section 7).

#### Why the videos weren't selling yet (J, 2026-10-22)

"Pacing, voices, lipsync, movement and script. Other tools on the market have already got this down completely where
little editing is needed. I wish that our pipeline could generate videos that are already good." The critic's review of
The Board v6 named it exactly: lip sync 1/10 ("dialogue plays over static keyframes with stationary mouths"), movement
2.5/10 ("completely motionless still frames"), story 7.5 and consistency 8.5. So: **nothing ships with still frames under
a voice, mouths that don't move, or lines read one after another with robotic gaps.** Every route below exists to fix
those five things.

#### 0. Pick the route (by kind of video)

The request may name the kind; otherwise pick it from the script. Every route starts with the table read and ends with
the critic.

| Kind | The route |
|---|---|
| **Dialogue scene** (2-3 characters talking) | Table read (`audio.dialogue`) → `video.scene` in PERFORM mode (each character's voice sample, the lines in the prompt; the model acts the turn-taking) for each scene, 2-3 takes of the scenes that matter, the critic picks → `video.extend` to carry a moment past 15 s → `video.keyframes` only where a beat must land at an exact second. TRACK mode (the table read locked) when J's delivery must not change. |
| **Monologue** (one character, to camera or alone) | Table read of the one voice → `video.perform` (the character speaks and moves in their own cloned voice, face and body acted together) for the close shots; `video.lipsync` when the line is locked; `video.scene` for the wide that places them. |
| **Action / fight** | Beat sheet by physical time (wind-up, impact, reaction, aftermath) → `video.keyframes` with the impact pinned to its frame, or Blender blocking + `video.control` for choreography (section 6); `video.extend` to carry the action on; SFX on every hit. Lines, if any, go through the table read. |
| **Music video** | The song first (`audio.music`, or J's track): its sections and beats set the cut, but don't cut on every beat. Sung lines: `video.lipsync` on the vocal stem or `video.scene` TRACK mode; the rest is performance and story, not illustration of the lyrics. |
| **Trailer** | A shape, not a montage: a quiet open that sets the world, a turn, a rising run of short shots, a held last image, then the title and end card. Lines from the table read; `video.scene` per moment; one line of dialogue can carry a whole trailer. |
| **Gameplay ad** | Real gameplay footage (Studio's playtest recordings, the game's own capture) as the spine: a hook in the first second, the one thing that's fun, the payoff. Characters can introduce it (`video.perform`), or footage repaints through `video.control` (section 6). Never fake gameplay that the game doesn't have. |
| **Performance capture** (J acted it; the request names his Gen asset) | His video drives everything: `video.replace` (our character takes over the person in it, with every move, hesitation and glance; split the video into ~10 s pieces), then `audio.voice-change` per speaker (his delivery in the cast voice, beat for beat, so the lips stay right), then the cut. Don't re-time his performance. |

#### 0b. Pacing: lived, not assembled

An interesting video is not a sequence of attractive shots; it is a sequence of meaningful changes in attention,
expectation, emotion and time. Model the experience of living through the moment, not the appearance of it.

- **Every moment changes something**: what the viewer knows, expects, feels, notices, or how they read what comes next.
  The change can be small (a second glance at the phone means more than the first). It doesn't have to advance the plot.
- **Track four things per beat** in the beat sheet (`attention`, `expect`, `change`, `residue`): where the viewer looks,
  what they think will happen, what actually changes, what carries into the next beat.
- **Five clocks**: physical time (actions take as long as they take: don't summarise a gesture), psychological time (how
  long it feels to the character), social time (turn-taking, a beat before a hard answer, the listener's face after the
  line), environmental time (the world keeps moving: light, steam, traffic, a curtain), editorial time (what you keep,
  skip, hold).
- **A pulse, not a tempo**: alternate density and simplicity, movement and stillness, close and far. Quiet stretches
  make the peaks matter. Let important actions breathe, compress routine, hold on reactions, sometimes cut before an
  action finishes and sometimes stay after it.
- **Hold a shot until the viewer has experienced what matters**, not merely until the information is delivered. Give a
  reason for every duration (`why` in the beat sheet); there is no fixed number of seconds per shot.
- **People behave causally**: they hesitate, look away, react a fraction late, carry the last beat's emotion into the
  next one; objects stay where they were put. Keep a world model between shots (where everyone and everything is, what
  they want, what they feel, how much time has passed, what the viewer knows).
- **Avoid**: constant escalation (no baseline, no surprise), illustrating the script literally, decorative B-roll that
  changes nothing, mechanical cutting (every 2 s, on every beat, on every sentence end), overdirected on-cue acting, and
  slowness mistaken for realism. Never add random pauses, shake or "imperfection" to look real: variation must have a
  cause.
- **Check the sequence, not the shots**: does it make the viewer feel something no single shot could? If removing a
  shot changes nothing, it goes.

#### What J wants (his words, 2026-10-22)

"Where's the close up shots? Where's the facial reactions? Where's the different camera angles? Where's the physical
reactions and VFX? Where's the unique bespoke animations that fit the style of the character? ... When making a video we
should have the agent go beat by beat second by second describing what should happen and using all of the tools at its
disposal to do so. And it just so happens, one of the best ways to do that is blender and comfy."

The first video out of this pipeline was keyed fighter clips standing on a stage, punching the air: one wide shot,
small characters, no faces, no reactions, no camera. That is the floor, not the bar. Never ship that.

And on The Board v1: "for the exit punk origins it went from one style to the next and that was a little weird",
"the characters needed a little bit more expression, and as far as them interacting they felt kinda disconnected? Like
it was just two characters from random other movies stitched together to form a barely coherent sentence. And on that
note, the story it put together made no sense either. I couldn't follow along with the point of the story, why they are
doing what they are doing." Story, togetherness and one consistent look come before any of the craft below.

Also from J: lore and characters, not feature montages; each character in their own fitting voice; pacing cut to the
voice; only shots that look good; something happens in the first second; a short end card (the game's name, then "Made
with Hawkes Gen · hawkesgen.com" small) is the only selling.

#### 1. Read and cast

1. Read J's script as written and keep his lines (fix only obvious typos). Read the product's story doc and the
   characters' canon (`memory_search` kind canon) for who they are and how they move. If a line contradicts canon, ask
   (`.hawkes/question.md`).
2. Voices: each character's own. Dose Of Destruction `content/voice.json` (`voices`, model `eleven_v4`, `cast` says why);
   Sick Of It: Yooba `EdDvd8GSdCyZwUcsPawi` with `eleven_flash_v2_5`, others in `Assets/GameConfig/audio.json`; PH Origins
   `locales/voices.json`. No voice yet: design one (`audio.voice-design`, three previews), save the best
   (`audio.voice-save`), write it into the product's voice file, and note it in `.hawkes/learned.md`.
3. **The table read** (always first): the whole scene voiced in one take with `audio.dialogue` (`lines: [{ who, text }]`
   in order, `cast: { who: voiceId }`, `stems: true`, `count: 2` for a second read). The speakers take turns and react
   like people, which separate `audio.voice` lines read one at a time never do. Put the delivery in the text with tags
   (`[sighs]` `[interrupting]` `[under his breath]` `[laughs]`), including the pauses and overlaps the scene needs. It
   gives the dialogue track, each line as its own clip (stems) and when each line starts and ends: **those times set the
   beats.** Keep a scene under about 2,000 characters; split longer scripts into scenes. Listen to it (or run the
   critic on it with a still) before anything renders: if it sounds recited, re-read it with better tags. Use
   `audio.voice` only for a single line added later. A voice that only exists on a performance J recorded goes on with
   `audio.voice-change`.

#### 2. The story (before anything else)

Write `videos/<slug>/story.md`, short, and put the same text in `.hawkes/video.json` `story`:
- **Logline**: one sentence a stranger would understand: who, wants what, what gets in the way, what changes.
- **Each character**: what they want in this scene, how they feel about the other one, their attitude (in one line each).
- **The turn**: the moment the scene changes (a reveal, a decision, a reversal), and **the payoff**: how it lands (the
  joke, the feeling, the consequence).
- **Why it matters**: what it shows about the world or the character, in the game's canon.
- **Who it's for**: someone who has never seen the game. They must be able to follow it.

If J's script leaves the point unclear, find the strongest reading that fits his lines and canon, and say it in the
logline. Don't add lines; make the staging, reactions and cuts carry the meaning. If you can't find a reading that makes
sense, ask J (`.hawkes/question.md`) rather than shooting confusion. Every beat in the sheet should serve this story.

#### 3. Find every form of every character

Characters exist in different forms across products: rigged 3D models, 3D models without a rig, 2D animated clips,
sprites, still art, LoRAs. Yooba is a rigged Blender model (`yoobaland/yooba`), DoD clips (`doseofdestruction/yooba`) and
Sick Of It pixel art (`sickofit/gloob`). Search `hawkesgen_entities` per project and `hawkesgen_library` by name, and the
repos (`assets/models/*.glb`, Blender files). Record them in `video.json` `cast[]` with each form's source. A form only
on J's PC can't be used here: say in the summary which file should go into the library.

Look at what you find (frames, renders): you need to know what each character looks like from the front, in close-up,
and how they move, before you plan shots.

**One look per character, from their canonical reference.** Every shot's keyframe of a character starts from the same
reference (DoD: the fighter's `reference` and `side` slots on `doseofdestruction/<id>`; others: their entity's
`concept`/`reference`), never a look you invent for that shot. Make a small sheet first (front, three-quarter, close-up
portrait, two or three expressions) from that reference with `image.edit` / `image.angles`, check it's on-model, and draw
every shot of them from the sheet. The Board v1 made The Mechanic a 3D man in the wides and a flat 2D cartoon in the
close-ups: that fails review.

**One house style per video.** Characters can be drawn in different styles in canon: in Dose Of Destruction, Exit
Punk is a 3D, Pixar-like render and The Mechanic a flat 2D cartoon. A scene with both needs one style, or it looks like
two films stitched together. Use the style J's direction names; without one, the style of the video's lead character.
Redraw the others in it with `image.edit` (their reference as image 1, the lead's as image 2: "redraw the person from
image 1 in exactly the rendering style of image 2, keep his design exactly: ..."), check the result is still them
(face, hair, outfit, colours, props), and keep it in their kit as their look in that style. The Board bake-off redrew The
Mechanic in 3D this way, on-model in one try (Gen asset `a_deb66dcd567fb43f`).

**Character kits (made once, reused by every video).** Keep each character's kit in the product repo under
`videos/cast/<character-id>/kit.json`: the canonical reference, the sheet (front, three-quarter, portrait) and an
expression set (neutral, smug, shocked, angry, deadpan, hurt, delighted) as Gen asset ids, their voice, their 3D form or
Blender proxy (height, proportions), a motion style in a sentence ("servo-stiff: holds still, then moves one joint at a
time"), motion clips made for them, and a LoRA if one was trained. Look for the kit first; extend it when a video needs
an expression or move it lacks; build it when it doesn't exist. Commit it, so the next video starts from it.

#### 4. The beat sheet (before you render anything)

Write `videos/<slug>/beats.json`: an array, one entry per shot, timed to the voice lines:

```json
{ "t": 2.4, "seconds": 1.2, "shot": "CU", "angle": "low", "camera": "push-in",
  "who": ["Exit Punk"], "line": "That's me. Hi.",
  "action": "kicks the board up into his hand, grins", "expression": "cocky half-smile, eyebrows up",
  "sfx": "board clack", "vfx": null,
  "method": "video.scene PERFORM, scene 1 (take 2 of 3, picked by the critic), CUT 2",
  "attention": "his grin, then the board", "expect": "a trick", "change": "he's cocky, not nervous", "residue": "will the trick land?",
  "why": "his entrance: sell the attitude before the line lands; held 1.2 s so the grin registers" }
```

`shot` is `EWS` `WS` `MS` `MCU` `CU` `ECU` `OTS` `insert` or `POV`; `angle` `eye` `low` `high` `dutch` `top`;
`camera` `static` `push-in` `pull-out` `orbit` `pan` `truck` `crane-up` `crane-down` `follow` `whip-pan` `handheld`.
Write it like a storyboard artist and an editor, second by second, timed to the table read. `attention`, `expect`,
`change` and `residue` are section 0b's four things; a beat where `change` is empty is a beat to cut or merge.

**The craft bar** (every video meets it unless J's direction says otherwise; the critic checks it):
- **A hook in the first second**: motion, a face or a line. No title card or slow establishing opener.
- **Pacing by purpose** (section 0b): each shot as long as its moment needs, with a reason. Enough setups (shot size ×
  angle) that the scene reads and breathes; a long hold is fine when something is happening in it (a reaction landing,
  a silence building), never a still frame with a voice over it.
- **Variety in dialogue**: never a ping-pong of the same two static close-ups. Mix over-the-shoulders, two-shots, a
  change of angle or side, a slow push on the line that matters, a cutaway to hands or a prop.
- **Consistency**: each character looks the same in every shot (one model and style, from their kit).
- **Together, not stitched**: the characters share one set, one light and one geography. Hold screen direction (the
  180-degree line: if A looks right in the wide, A looks right in A's close-up), match eyelines, and use over-the-shoulders
  and two-shots that show both. They react to each other's words, look at each other, hand things over, touch the same
  props. Stage the scene once in Blender (or one set plate) and take every shot from that scene, so geography and light
  hold. Never intercut portraits made on separate backgrounds.
- **Expression**: faces act. Each beat names the expression, and it changes with the beat (a smirk dropping, eyes
  narrowing, a beat of disbelief). Use the kit's expression set for keyframes and prompt the micro-motion.
- **Faces**: every spoken line has the speaker in MCU or closer, lip-synced, or plays over a reaction on purpose.
- **Movement**: bodies and faces act in every shot (weight, gesture, breath, a glance, cause and effect). A still image
  with a slow zoom under dialogue is the failure the critic scored 2.5/10; even the animatic moves where a line is
  spoken (a Lightning `video.scene` or `video.perform`, not a held still).
- **Reactions**: every punchline, insult, hit or reveal cuts to someone's face reacting (CU or ECU), held a beat.
- **Physical action reads**: a wide or medium to show what happens, the impact in close-up, a physical reaction
  (recoil, stagger, a shake, a dent, a wobble), **VFX** on impacts and powers (flash, sparks, dust, speed lines, a
  shockwave, steam), and an SFX for every action.
- **Default method**: scenes from references (`video.scene`, section 5); single shots only where a scene needs more.
- **Bespoke animation**: each character moves like themselves, made for this beat (a skater's flow, a robot's servo
  stiffness, a slime's wobble). Stock idle or attack clips only for background extras, never the hero beats.
- **A motivated camera**: at least one move that serves the story (a push-in on a realization, a whip-pan to the
  reaction, a low angle for menace).
- **Sound**: voices on top, SFX on every action, music or room tone under.

#### 5. Scenes, not shots: the default method (`video.scene`)

J, 2026-10-22, after the bake-off: whole scenes generated from references are the director's default. One generation
makes a whole scene (5-15 s, several cuts) with every character on-model in one room and one light, acting to the
voices, with native sound. That is what fixes "two characters from random movies stitched together": shots made one
at a time drift apart in look, light and space; a scene made in one pass doesn't.

**`video.scene`** (MiniMax H3, open weights, our GPU fleet), per scene:
- **Inputs:** `image` and `image2` (and `image3` for a third character): the characters' kit references in the house
  style (full body, plain background); `set`: the location with no people in it (generate it, then remove any figure
  that sneaks in with `image.edit`); `audio`, `audio2`, `audio3`: see the two modes (optional inputs go in order).
- **PERFORM mode (the default for dialogue):** `audio`/`audio2`/`audio3` are each speaker's clean voice sample (3-10 s
  of that character, e.g. their stem from the table read), and the lines are in the prompt: "<Subject 1> speaks in the
  voice of <Audio 1>: '...'". The model acts the dialogue itself, with its own timing, breaths, overlaps and reactions,
  which is what makes it feel alive. Check the words came out right (the critic listens).
- **TRACK mode:** `audio` is the finished voice track (the table read, or its lines for this scene) and the prompt says
  "Use <Audio 1> exactly as it is: X speaks first, then Y; each speaker's lips match his own voice." For a locked
  delivery: J's chosen read, a song, a voice-over.
- **Prompt:** style first, then each reference's job ("<Picture 1> is Exit Punk, a skater kid (blond spiky hair...)",
  "<Picture 3> is the foundry: every shot happens in this one room"), "Both stay exactly on-model in every shot.", then
  the shots as `CUT 1 (0 to 1.7 s): ...` with framing, camera, action, expression, the line in quotes, and what the
  listener does while the other speaks. Your beat sheet becomes this list. Write behaviour, not poses: "he looks at the
  bolt before he answers", "she starts to reply, stops".
- **Params:** `seconds` = the scene's length (5-15). `aspect` `16:9 (Widescreen)` or `9:16 (Portrait)`.
  `megapixels` 0.4-0.6 (0.5 is 960x544); upscale the finished scene with `video.upscale`.
- **Takes, picked by the critic:** for the scenes that carry the video (the turn, the payoff, a hard line), make 2-3
  takes (different seeds) and run `video.review` on them together (`video`: up to 4 takes, `brief`: the scene's beats
  and lines, `focus`: what must land). It ranks them and names the `best`; keep that one, note the ranking in
  `video.json`. One take is fine for simple scenes. Quality `high` means takes for every scene. Price them first.
- **Longer moments:** `video.extend` carries a shot on from its last frame (same style words) past 15 s, for a moment
  that must continue without a cut. `video.keyframes` pins up to three frames to exact seconds (`at1`..`at3`: the reveal,
  the hit, the last look) when a beat must land at a time; `video.scene` when the performance should find its own timing.
- **Longer videos** are several scenes, each its own generation with the same references, cut together. Cut between
  scenes on a change of place, time or beat, never mid-line.
- **An animatic** (only when J asked for one) is the same call made cheap: `enable_lightning_lora: true` (4 steps),
  `megapixels` 0.3. It still moves and speaks. The final re-runs the approved prompts at full steps and size.
- **Check** the result as always: frames once a second next to the beat sheet, the audio against the table read, and the
  critic (section 7). The Board bake-off scene (11.5 s, 5 cuts) took 17 minutes on one GPU.

**Close performances:** `video.perform` (LTX-2.3 ID-LoRA): one character speaks and moves in one pass in their own
cloned voice (`audio`: 3-10 s of them speaking; prompt `[VISUAL]: ... [SPEECH]: the exact words [SOUNDS]: ...`): for
monologues, a close-up line, a talking head that must act. `video.lipsync`: the character says exactly the given audio
(a table-read stem, a sung line) with natural head and body motion, when the delivery is locked.

**J's own performance:** `video.replace` (SCAIL-2) puts the character from `image` into the person in J's `video` with
all of his movement; `replacement_mode` keeps his background and camera, off lets the prompt set a new one. About 10 s
per run: split longer videos. Then `audio.voice-change` (`video` in, `voice`: the cast voice id) puts the cast voice on
his delivery word for word, so the lips stay right. One speaker at a time.

Paid alternatives (ComfyUI partner nodes billed to the Comfy.org account, only when J has added the key and wants them):
`video.seedance` (Seedance 2.5: the same inputs, up to 30 s) and `video.kling` (Kling O3: one reference sheet; Kling
voices the characters itself, so not with our cast voices).

#### 6. Shots that need more: Blender and ComfyUI

When a scene needs exact control the references can't give (precise blocking, a specific camera move, choreography, a
character performing a particular motion, an effect on one character), make that shot this way and cut it into the
scene. Price it first (`hawkesgen_estimate`, or `film.plan`, which prices a whole recipe) and stay inside the brief's
GPU budget; batch jobs of one kind together (one model load).

1. **Stage it in Blender** (`blender.render`; hawkesgen `docs/blender.md` has the format). Use the characters' 3D forms,
   or stand-ins for 2D-only characters (a mannequin or rig proxy of the right size and proportions, or a card with their
   art). Block it with `beats` (walk to, turn, `lookAt`), props in hands (`attachments`), the set (PBR ground and walls,
   an `image.panorama` sky, stage art, a `splat.build` scene), effects, and the camera from your beat sheet (`move`,
   `lens`: 24 wide, 35, 50, 85 close; `target`, `distance`, `height`, `angle`, `amount`, `handheld`, `dof`).
   Run `film.gate` on it before any repaint.
2. **Bespoke motion.** Rigged characters: the CC0 motion library (`motion:ual1/...`, search `hawkesgen_library`
   "motion") as a base, `motion.from-text` for the specific action ("skates in, ollies, catches the board"), or keyed
   poses (`blender.character` ops). 2D characters: animate a rig proxy in Blender with that motion, render its `pose`
   pass, and drive the character with it: `video.motion` or `video.animate` with an on-model keyframe of the character.
3. **The look.** Repaint Blender's guide passes (`depth`, `pose`, `edges`; `video.control-mix` lays them together) with
   `video.control` (LTX, with sound) or `video.control-wan`, from a first frame made on-model (`image.edit` from the
   character's reference sheet, best of 3, `image.relight` to the set's light). For 2D-style characters the first frame
   is the character drawn in their own style on the set, so the repaint keeps their look. Or Blender's own render when
   the models already look right.
4. **Faces and lines.** Close-ups: an on-model portrait in the shot's light (a Blender portrait render, or `image.edit`
   from the reference with the expression the beat asks for: "eyes wide, jaw dropped"), then `video.talk` with the line.
   Silent reactions: `video.i2v` from the expression still, prompted with the micro-motion ("blinks twice, slowly
   narrows eyes").
5. **VFX.** Render the characters on `layers` in Blender, run the effect through the video model on one character's
   matte, and put it back with `video.composite` (glow and bloom inside the matte, never over the other face). Or
   `film.plan` recipe `vfx`. Impact frames, flashes and shakes can also be done in the cut.
6. **Video to video.** Any footage (a gameplay recording, an existing clip, your own rough animatic) repaints through
   `video.control` with `estimateDepth: true`.
7. **Finish.** `video.upscale` then `video.interpolate` on shots that need it; `image.restore` on stills.

**Longer shots and joins.** Don't chain a clip from the last frame of the one before and hope: the style drifts and the
join shows. For a line longer than one clip, size `video.talk`'s `frames` to the whole line (it joins its two passes
itself and keeps the lips on the voice; size from the line's real length) or cut to a reaction or another angle
mid-line, which reads better anyway. Generate each shot from the same kit keyframe and the same set, never from the
previous clip's last frame. Check every join for a jump in look, light or lip sync.

`film.plan` builds these chains as priced `asset.make` recipes: `cinema` (gate, render, on-model keyframe, relight,
repaint, upscale), `dialogue` (a lip-synced close-up), `vfx`, `trailer`.

**Judge every generated shot hard.** These models morph, drift off-model and invent things. Pull frames and look. A
shot that looks wrong gets redone (another seed, a better keyframe, a tighter guide) or replaced, never kept because it
was paid for.

#### 7. The cut

`video.compose`: shots in order with their durations from the beat sheet, cuts on action, voice lines on their shots
(`dialogue: true`), SFX placed on the frames they belong to, music ducked under the voices, captions burned in for shorts
(large, in the middle band: TikTok's buttons cover the right edge and the bottom 20%). Short = 1080 × 1920, wide =
1920 × 1080. The end card last, 1.5 s, cut straight to it (no long fade to black after).

Then watch it as J will: pull a frame per second and look at all of them in order next to `beats.json`. Every beat there?
Faces on-model? The hook in the first second? Any dead air? Then run **the critic** on the cut: `video.review` with
`video` = the cut and `brief` = J's script, your story, the beat sheet and J's notes so far (about 1¢; it watches and
listens). Fix what it finds (its notes are timecoded, with the ask: lipsync, redo, shorter...) and run it again, until
it passes or what's left needs J's call; say in `summary` what it scored (`scores`) and what you left and why. A
reference clip J likes can go in as `reference` to hold the cut to it. Studio runs the same critic on what you hand over.

#### 7b. The edit (always)

Make the cut from an **edit**: `video.compose`'s timeline with Gen asset ids where the tool takes input indexes, and hand
it over as `.hawkes/video.json` `edit` (also commit it as `videos/<slug>/edit.json`). Render the cut by running
`video.compose` from it (clips and audio as `asset:<id>` inputs in the same order as the indexes). This is what makes the
Screening timeline work: J selects a range, Studio knows which of your clips and beats it is, and plain edits (cut a range
out, speed it up, punch or push in on a point) are made by Studio in seconds without you.

```json
{ "width": 1080, "height": 1920, "fps": 24, "burn": true,
  "clips": [ { "asset": "a_...", "in": 0, "out": 2.4, "beat": "hook", "label": "WS Exit Punk skates in" },
             { "asset": "a_...", "in": 3.1, "out": 4.6, "beat": "ep_hi", "label": "CU Exit Punk: That's me. Hi.", "zoom": { "from": 1, "to": 1.15, "x": 0.5, "y": 0.3 } } ],
  "audio": [ { "asset": "a_...", "start": 2.5, "seconds": 1.4, "dialogue": true, "label": "EXIT PUNK: That's me. Hi." },
             { "asset": "a_...", "start": 0, "seconds": 24, "volume": 0.3, "duck": true, "label": "music" } ],
  "captions": [ { "text": "That's me. Hi.", "start": 2.5, "end": 3.9 } ] }
```

Rules: every clip has its `out` (its length must be known); `beat` is the beat sheet's `key` for that shot; every audio
track and caption has an absolute `start` on the cut (never relative to a clip) and every voice line its `seconds`;
voice lines are `dialogue: true`. A whole generated scene can be one clip, or several clips cut from it (one per CUT),
which lets J's notes and quick edits work shot by shot: prefer that. A punch-in on a close-up is a `zoom` on that clip,
not a new render.

#### 8. Hand it over

- Commit `videos/<slug>/`: `script.md` (J's script), `story.md`, `beats.json`, `video.json` (cast and forms, every shot's
  method, Gen jobs and asset ids), any helper scripts; and the kits you made or extended (`videos/cast/`).
- Write `.hawkes/video.json` (`asset` and `poster` are Gen asset ids; if you can't put the files into Gen yourself,
  give their paths in your worktree instead, `"file": "videos/<slug>/out/cut.mp4"` and `"poster": "videos/<slug>/out/poster.png"`,
  and Studio uploads them):
  `{ "asset": "<the cut>", "poster": "<a frame>", "seconds": 24.5, "summary": "what this version is / what changed", "caption": "post text: one line that makes people watch, 3-6 hashtags, hawkesgen.com", "beats": <the same array as beats.json>, "story": "<story.md>", "stage": "animatic" or "final", "edit": <edit.json> }`
- `.hawkes/summary.md` (what you made, what it cost) and `.hawkes/learned.md` (casting, a character's motion style, what
  worked or didn't, as durable notes).

#### Revisions

Notes come from J's Screening timeline or Talk, and from Studio's review (the critic: timecoded notes with their ask,
and its scores for story, pacing, voices, lip sync, movement, consistency and sound). A timeline note says exactly where: `[v3 · 0:12.4-0:15.0 · edit clips 4, 5 · beats 6 "ots_mech" (OTS The Mechanic
"Eleven seconds...")] Make this shorter: ... J: "drag less here"`. A review note reads the same way, ending in
`Studio's review: "..."`. That range is all it's about: change those clips and
beats, keep everything else as it is (same assets, same timing elsewhere), re-render only what changes, and re-cut from
your edit. Answer every note; in `summary`, say per note what you did.

What each ask means (the first words after the brackets):
- **Make this shorter / Let this breathe**: the timeline first (trim the clip's in/out, tighten the gap between lines,
  drop a beat); a new take only if the line itself drags (a faster delivery).
- **Redo this**: the same beat again, better: a new seed, a better keyframe or prompt; for a scene, regenerate that scene
  with the same references, voice track and CUT list, and fix what looked wrong.
- **Fix the lip sync**: the speaker's mouth on the voice line: re-run that shot with the line (`video.lipsync` or
  `video.perform` for one speaker, the scene in TRACK mode with the table read, or `video.talk` sized to the line's real
  length), and check the mouth against the line frame by frame. A still frame under a line is never the fix.
- **Closer / Wider / A different angle / Move the camera**: a new setup for that beat: a tighter or wider framing, an
  over-the-shoulder or a two-shot, a motivated move. A simple closer can be a `zoom` on the clip; a new angle is a render.
- **Add a reaction / More expression**: cut to the listener's face (an expression from the kit, `video.i2v` micro-motion),
  or redo the shot with the expression named in the prompt.
- **Change the voice**: a new take (delivery tags, emphasis) or another voice; re-time the beats to the new line length,
  and save a recast in the product's voice file. Re-read the scene with `audio.dialogue` rather than one line alone, so
  the turn-taking still flows.
- **Fix the script here**: a line that doesn't earn its place, an unclear point, a missing turn or payoff. J's lines are
  his: propose the change in `summary` (or ask), and carry the point with staging, a reaction or a held beat instead.
- **Fix the look**: back on-model, in the house style and the scene's light (from the kit references), nothing melting.
- **More physical action and VFX / Fix the sound / Fix the captions / Cut this out**: as they say.
- **Studio made a quick edit**: J already cut, sped up or zoomed part of the cut in Studio; its new edit is in the line.
  Make it your edit (then apply the other notes on top of it), never undo it.
- **J approved the animatic ... J's guidance**: make the final with the guidance; his notes on the animatic come with it.

If a note is unclear, ask (`.hawkes/question.md`) instead of guessing big.

## The thread (J, Talk and agents on this work; newest last)

- J (script), 2026-10-23 04:07: Script (short):
The meadow village at dusk, still smoking. Imps lie scattered and beaten. The Overlord, a giant obsidian titan with red eyes, turns away without hurrying. His lasers rake the ground behind him and burn a perfect glowing circle into the meadow. He's gone.

The Hero (thirties, unimpressed) walks over and looks down into it.
HERO: That's a hole.
(A beat. An imp's horn rolls past his foot and drops in. Silence. Then, far below, a tiny clink.)
HERO: So that's where they keep coming from.
(He leans over the edge.)
HERO: Hello?
HELL (echoing back, three times, each one deeper and closer): Hello? ... Hello? ... HELLO.
(He looks at the camera.)
HERO: Nope.
(The hole inhales. He's sucked in mid-step, arms out, still annoyed.)
(Silence. Then Yooba, the village's little green slime, bounces into frame, cheeks bulging with diamonds, peering into the hole.)
YOOBA: (muffled, mouth full) Mmf. Wait for meee—
(SHLURP. Yooba is slurped in after him. The circle seals shut. One diamond rolls out and stops.)
ANNOUNCER (booming 90s arcade voice): FLOOR ONE THOUSAND!
END CARD: Sick Of It. Made with Hawkes Gen · hawkesgen.com
- Studio (review) (review), 2026-10-23 18:25: Studio's review of v1 (4/10): The gag skeleton (hole, "Hello?", escalating echo, "Nope.", sucked in, FLOOR ONE THOUSAND) is readable, and the hero's deadpan CUs have a voice. But the opener contradicts the script and doesn't match the rest, the suck-in and Yooba beats don't show what the script says, and the payoff cavern has no hero. It would need a restage before J sees it. (Contact sheets: the critic couldn't watch it: Gemini quota: {
  "error": {
    "code": 429,
    "message": "You exceeded your current quota, please check your plan and billing details. For more information on this error, head to: https://ai.googl.)
- [0:01] Opener 0-4 s breaks the setup. Only the first frame shows the Overlord; then the hero stands inside the laser circle in a bright-green overhead view, in a different style and light from the dusk meadow (smoke, sunset, thatched huts) that follows. The three overhead frames look like the lasers are aimed at the hero, not like the titan leaving. Fix: hold the low WS on the Overlord turning away for about 2.5 s, then a high WS of the circle burning into the empty dusk meadow, with beaten imps in the
- [0:21] The suck-in doesn't read. The hero strides into the fire, then lies in it smiling, with no wind, pull or arms-out annoyance. Fix: a WS where he leans, the flame inhales toward the hole, grass and embers streak into it and he is dragged mid-step with arms out. Then cut to a CU of his annoyed face for about 0.5 s. Add the inhale SFX at 20.0 s.
- [0:23] Yooba never bounces in. He is already sitting in the hole at 23 s, drawn as a smooth cartoon blob that doesn't match the pixel style. His cheeks aren't bulging with diamonds (the gems sit on his sides), and his mouth is never shown, so "Mmf. Wait for meee—" has no speaker. Fix: show the empty meadow first, then Yooba bounces in from the left at MS with full cheeks and eager eyes, peers over the edge for the line, then gets slurped. Redraw him in the same pixel style. The caption should land when
- [0:13] The echo is one static high WS held for 5 s, and the caption is a single block, "Hello? ... Hello? ... HELLO.", for the whole stretch. It never escalates visually. The hero is not in frame, and nobody reacts to the echo. Fix: three distinct beats, each caption line on its own and growing in size. The flame grows and the camera pushes in each time. Cut to the hero's face (eyes widening a little, then flat again) between the echoes. The caption should not spill over into his CU at 17-18 s.
- [0:19] Lip sync is poor. At 19 s the word "Nope" is burned into the mouth region as a tiny label, an artifact, and on the "So that's where they keep coming from" CU the mouth shows white bars and flaps without matching the syllables. Regenerate these CUs with real lip-sync so the final inherits clean mouths. The "Nope." CU should be on the hero's eyes, then look to camera, a beat, then the line.
- [0:28] Payoff is weak. The cavern is a static, empty wide with an arcade cabinet and no hero landing or reacting. "FLOOR ONE THOUSAND!" explains nothing visually. Fix: push-in on the cavern and have the hero drop into frame, or show his deadpan face reacting to the lava. Hold on that for the announcer line, then end card for about 1.5 s. Make sure it's a short end card, not the same shot with the text overlaid.
- [0:26] The circle never seals. The hole stays an open black pit and the diamond is a tiny blue dot at the bottom corner. The imps also vanish between frames. Fix: show the ring closing with a flash, grass rolling back over it, then a MS or ECU on the diamond rolling and stopping, and hold it for about 1 s of quiet before the announcer.
- [0:05] The "That's a hole." shot never shows the imp horn rolling in or the clink, and the hero has a match or cigarette in his mouth that disappears afterward. Add an insert of the horn rolling past his boot and dropping in, then a hold on his deadpan CU. Without it the "beat of silence" reads as dead air. Across the whole cut, almost every shot is static and there is no OTS or two-shot. Add at least one motivated move, such as a push in on the hole at the echo.
- Studio (review) (review), 2026-10-23 22:46: Studio's review of v2 (5/10): The gag is clear and the beats are in order: the hole, the horn, the three echoes, "Nope", the slurp, the diamond and Floor 1000. It is a big step up from the earlier cuts. But the staging doesn't sell the turn or the pairing: the echoes don't escalate on screen, the hole keeps changing look, the hero never shares a frame with the Overlord or Yooba, and the hero's face barely acts. At 34 s it is also long and mostly static. (Contact sheets: the critic couldn't watch it: Gemini quota: {
  "error": {
    "code": 429,
    "message": "You exceeded your current quota, please check your plan and billing details. For more information on this error, head to: https://ai.googl.)
- [0:10] The echo escalation doesn't read. Echo 1 (10-11 s) is a wide of the ring with the caption 'Hello?' and no hero in frame. Echo 2 (13 s) is an over-the-shoulder with a small fire in the crater. Echo 3 (15 s) is the fire column, held about 1 s, and then straight into the eyes. Nothing shows 'deeper and closer' between echo 1 and 2, and the hero's own 'Hello?' and Hell's reply have the same caption, so the viewer can't tell whose line is whose. Fix: caption Hell's echoes in a different colour or siz
- [0:20] The suck-in doesn't read as being sucked in. At 19 the hero stands with hands in his pockets and a flame swirl curls around him. At 20 he is jumping with arms out, stamping on a fire pit, like a dance. Fix: a wide where the pull is visible: his boots slide toward the hole, his coat and the imps lean inward, dust and sparks stream into the ring, and he is dragged off his feet. Keep the 'still annoyed' look on his face. Add wind SFX and a sharp inhale before the snap.
- [0:22] Hero and Yooba never share a frame, and Yooba is introduced only after the hero is gone, so there is no friendship and no 'wait for meee' motive. In the wide at 22-24 Yooba is already standing on the hole instead of peering into it, and his cheeks aren't visibly full of diamonds. Fix: plant Yooba earlier. Put a small green slime in the background or at the edge of frame at 9-14 s, peeking from behind a hut or following the hero. Then bounce him in over the hole with bulging cheeks, as an MS with
- [0:00] The hook is weak. The first 3 s are three nearly identical static silhouette wides of an anonymous black giant, with no face, no line and no sound event. The Overlord is never in the same frame as the hero, so the viewer doesn't learn who he is or why the hole matters. Fix: open on motion, such as a laser slicing the grass with the ring igniting, or a fast push-in on the glowing red eyes, then cut straight to the hero already looking down. Cut the Overlord to about 1.5 s and give him one camera 
- [0:08] The hero's face doesn't act, and the dialogue setups are ping-pong. 'That's a hole' (4 s), 'So that's where…' (7-10 s) and the CU at 12 and 14 are almost the same angry, flat eyebrows and similar static eye-level CU. The planned 'eyes widen' and 'flat again' reactions don't show. Fix: change the expression per beat: a blank stare on 'hole', a dry half-smile on 'where they keep coming from', a slight lean and raised brow on 'Hello?', then a real micro-flinch or widened eyes at echo 3. Vary the an
- [0:13] The hole is drawn three different ways: a black void in a glowing ring (3, 10, 21, 26), a crater full of fire (13, 19, 22-24) and a flat rune circle (27). Hero scale also jumps between the small figure in the wides and the blocky, large-pixel CUs. This breaks the shared geography and the style, so the hero looks like he is in a different place from the hole. Fix: lock one hole design (black void, burning rim, steady smoke wisps) and only animate its intensity. Use a consistent pixel scale for th
- [0:28] The payoff is slower than it needs to be. The diamond is two near-identical blurred ECUs held about 2 s (28-29), the hole's seal isn't a clear action (26-27), and the hero doesn't fall into Hell. At 30-31 he is already crouched on lava with no arrival, then 32 is a fire-faced portrait that hasn't been set up. Fix: show the seal as one clear flash and snap. Show a diamond roll-and-stop with a clink at about 1 s in a single ECU, and give the announcer line its own beat with a punch-in. Add a fall 
- [0:33] The end card is dark and cluttered. 'SICK OF IT / Made with Hawkes Gen · hawkesgen.com' sits as one wrapped block over a murky background, with 'Hawkes / Gen' split across two lines, and it follows a fire-faced portrait that is a style jump. Fix: put the title and 'Made with Hawkes Gen · hawkesgen.com' on a clean dark card, with the URL on one line, in the middle band, held about 1.5 s with no fade. Cut or shorten the portrait at 32 s so the sequence is lava → hero's deadpan → card.
- J (note), 2026-10-24 11:07: Approved the animatic v3: make the final.
- Studio (review) (review), 2026-10-24 11:30: Studio's review of v4 (3.8/10; lipsync 2.5, voices 3, sound 3, pacing 4, story 4.5, movement 6.5, consistency 7.5): The dialogue track slipped severely in the second half: Hell's echo plays over 'Nope.', the hero's voice plays over the slime, Yooba's voice plays in Hell, and the final announcer line is cut in half.
- [0:16-0:17] Hell's third echo ('HELLO') is delayed and plays directly over the hero's 'Nope.' closeup, bumping the hero's line out of position. Shift Hell's third roar earlier to 00:13 when the fire column erupts so the hero can deliver 'Nope.' at 00:16.
- [0:21-0:23] Due to the audio slip, the hero's voice line ('Nope.') plays over Yooba the slime while the caption reads 'Mmf. Wait for meee—'. Restore Yooba's line here so the character audio matches the subtitle and mouth flap.
- [0:27-0:28] Yooba's 'Wait for meee!' line plays inside Hell over the hero landing while the screen reads 'FLOOR ONE THOUSAND!'. Replace with the booming arcade announcer line.
- [0:29-0:30] The Announcer's line 'Floor...' is cut off mid-word as the title card appears. Give the announcer track room to fully pronounce 'FLOOR ONE THOUSAND!' before the end title.
- [0:17-0:19] The suction animation of the hero being pulled into the hole looks like an unnatural, stepped slide rather than an explosive vacuum. Smooth out the frames and add a quick downward stretch.
- Studio (review) (review), 2026-10-24 11:39: Studio's review of v5 (3.5/10; sound 2, voices 4, pacing 5, lipsync 5, movement 5, consistency 6, story 7): Severe audio bleed persists throughout: orphaned dialogue lines play over other characters, punchlines overlap, and the announcer repeats and cuts off abruptly at the end card.
- [0:27-0:29] Yooba's voice ('Wait for meee!') plays under the Announcer shouting 'FLOOR ONE THOUSAND!', causing a loud, messy collision. Strip the lingering Yooba stem from this segment.
- [0:21-0:23] A duplicate hero voice saying 'Nope.' plays directly over Yooba's spoken line ('Mmf. Wait for meee—'). Mute the baked hero audio track so only Yooba speaks.
- [0:29-0:29] The announcer voice starts repeating ('Floor w—') and cuts off abruptly right as the end card appears. Trim the audio tail cleanly so the line finishes before the transition.
- [0:15-0:17] Hell's prolonged echo overlaps the hero's 'Nope.', ruining the deadpan comedic beat. Cut the echo before the hero's line so the refusal hits in total silence.
- [0:17-0:19] The suction animation feels floaty and stationary instead of an intense downward vacuum drag. Accelerate the downward movement into the pit to give the suction real weight.

Follow what J said there; where it differs from the request, J's latest word wins.





## Notes on the last attempt (J's or the PM's)

Studio's review on v5 (make a new version that answers every one; change only what they're about):
- [v5 · 0:27.0-0:29.0 · edit clips 21, 22 · beats 21 "fall" (); 22 "lava" ()] Fix the sound here: the SFX, the music level, the mix. Studio's review: "Yooba's voice ('Wait for meee!') plays under the Announcer shouting 'FLOOR ONE THOUSAND!', causing a loud, messy collision. Strip the lingering Yooba stem from this segment."
- [v5 · 0:21.5-0:23.5 · edit clip 17 · beat 17 "yooba" ()] Fix the sound here: the SFX, the music level, the mix. Studio's review: "A duplicate hero voice saying 'Nope.' plays directly over Yooba's spoken line ('Mmf. Wait for meee—'). Mute the baked hero audio track so only Yooba speaks."
- [v5 · 0:29.0-0:29.9 · edit clips 22, 23 · beat 22 "lava" ()] Cut this out. Studio's review: "The announcer voice starts repeating ('Floor w—') and cuts off abruptly right as the end card appears. Trim the audio tail cleanly so the line finishes before the transition."
- [v5 · 0:15.8-0:17.0 · edit clips 12, 13 · beats 12 "eyes" (); 13 "nope" ()] Change the voice or the delivery here: a new take (emphasis, timing, emotion) or a different voice. Studio's review: "Hell's prolonged echo overlaps the hero's 'Nope.', ruining the deadpan comedic beat. Cut the echo before the hero's line so the refusal hits in total silence."
- [v5 · 0:17.2-0:19.2 · edit clips 13, 14 · beats 13 "nope" (); 14 "suck" ()] More physical action and VFX here: the impact, a physical reaction, sparks or a shake, and the SFX. Studio's review: "The suction animation feels floaty and stationary instead of an intense downward vacuum drag. Accelerate the downward movement into the pit to give the suction real weight."

## Other agents on Sick Of It

Up to 2 agents work on Sick Of It at once, each on its own branch from beta. Another agent may merge into beta
while you work, so keep your change narrow: touch only the files your task needs, and don't reformat, rename or move
shared files. None is running beside you right now, but one may start while you work.

## Working in Unity (several agents share this repo)

- **Prefabs first.** Put new or changed content in prefabs (or the project's data files and runtime-built content,
  where it already works that way), not straight into a scene. Keep scene edits small and rare: a scene is one file
  every agent wants, and Studio runs only one change at a time that touches one.
- **Never hand-merge binary files** (textures, audio, models such as FBX/GLB, binary `.asset`s, anything in Git LFS),
  and never regenerate or re-export one someone else made. On a conflict, keep beta's version and redo your own edit on
  top (if you need a different version, add it as a new file with its own .meta and point your work at it).
- **Unity YAML** (`.unity`, `.prefab`, `.asset`, `.mat`, `.meta`...): keep every GUID and fileID as it is; never
  duplicate, renumber or invent them, and keep each asset's paired `.meta` (add, move and delete them together).
  After resolving one, check it still parses: no conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`) and every
  `--- !u!<class> &<fileID>` document header intact.
- **Say in your summary** which conflicts happened and how you resolved each one (or that there were none).

## What the studio already knows (shared memory)

Earlier agents, J and Talk learned these; follow decisions and preferences, respect canon, and use the lessons
(unconfirmed ones are an earlier agent's notes: check them). More: memory_search, if you have the tool.
- [decision] Phone notifications only for what went wrong, J-only decisions and milestones: Merges, starts, answers and progress go to Talk only. (m_0754baa8d780)
- [decision] Unity work: use prefabs as much as possible (fewer merge conflicts, good practice); several agents may work on one Unity product at once: 2026-10-19. In Unity projects (Sick Of It, Yooba Land, The Place Department), put new or changed content in prefabs, not straight into scenes. Keep scene edits small and rare; prefer runtime-built content and data files where a project already does. J raised agents per product to 3, so agents must expect another agent working on the same repo. They must check which files the others touch, avoid h… (m_0cbc53095180)
- [decision] PM may start up to 5000 agents a day; keep working through the roadmap without stopping: 2026-10-19: J raised the PM's maxStartsPerDay from 100 to 5000 after Yooba Land's roadmap tasks and bugs sat idle at the 100-start cap. He also said "Just.. don't stop working there's a lot to do": keep the roadmap and the backlog bugs moving continuously, and don't let work idle on caps. Max-plan usage (not billed) is the real limit. (m_10400c353706)
- [decision] Build a reusable Babylon performance & leak root-cause monitor and attach its diagnostics to beta bug reports: Build a shared Babylon.js performance and leak root-cause monitor (usable across all Babylon projects: I Can Be, PH Origins, Dose Of Destruction) that diagnoses leaks, high-poly meshes, draw calls, render passes, unfrozen meshes/materials, and per-frame shader dirtying, and include its diagnostic snapshot automatically when submitting bug reports. (m_1140e13dc447)
- [decision] Reviews run on Sonnet: STUDIO_REVIEW_MODEL; a review is a few cents. Keep them there. (m_1bfcd733d45d)
- [decision] Studio must run the roadmap and bugs to done from J adding items alone; catch the roadmap up to what's already on beta: 2026-10-19. Work already on beta counts as done, never "planned". A dropped dependency never blocks. Held ideas are released when the item they waited on lands on beta. Tasks whose tries all found the work already done are done, not failed. Any remaining stall gets one Inbox question. Today's cleanup: Circle IX levels k_2bbf6b413a9f released (one level = one floor), Spire Fiend k_e16304d7a504 now… (m_1d389f0d68a1)
- [decision] DoD Season 2 and PH Origins level 7 directions (picked by Talk under J's delegation): 2026-10-19. DoD Season 2 "The card is yours" (i_b4cf57094da9): Exit Punk is the champion defending the card against 4 walk-ons not used in Season 1, reusing existing stages (Rainbow Full Pipe gets a chapter); a new finale stage is a later item. PH Origins level 7 "The Train" (i_0968e068740d): Pierce walks the cars toward his old, untransformed body, which gets off at a stop and doesn't look back,… (m_2015488b4339)
- [decision] Approvals move from typed words to an Approve / Deny button (m_286ae9f4cd83)
- [decision] Hawkes Analytics is the only analytics in every project, websites included: 2026-10-18. Every product's game AND website pages (including the Unity games' separate website repos: sickofitgamewebsite, YoobaLandGame, theplacedepartmentwebsite) use Hawkes Analytics (Gen's SDK). Remove GoatCounter, Umami, gtag or any other tracker wherever found, and never add another. Extends m_e4959acc8abd. (m_3000e802bd85)

## How to work

1. Read `AGENTS.md` (and any docs it points to) before changing anything. Follow its rules.
2. Look at the screenshots to see the problem. Find the cause in the code or data; prefer the smallest correct fix.
3. Keep to this finding. Don't refactor unrelated code, don't touch generated output or build folders, don't change
   secrets, CI, or deploy scripts.
4. Run the project's checks:
- (none listed: at least make sure it builds or loads)
5. Commit your change with a clear message (`Fix: ...`). Don't push; Studio does. Studio stops a pass after
   180 minutes: commit as you go (after each working piece or finished batch), so a stop loses nothing.
6. Write `.hawkes/summary.md`: what was wrong, what you changed (files), how you checked it, and anything J should
   look at. If you couldn't fix it, explain why there instead, and don't commit.
   Two approaches, then stop: when two different approaches haven't worked (the checks still fail, or you can't find
   the cause), don't keep iterating. Write `.hawkes/stuck.md` (what you tried, what happened, where you think the cause
   is) and commit nothing that doesn't work: Studio gives the next attempt a bigger model, with your notes.
7. Write `.hawkes/try.md`: how J tries it on the beta site, in two to five short steps a player would follow (where to
   go, what to press, what he should see).
8. If you can't go on without a decision that the request, the criteria and the thread don't settle (a real direction
   choice, not a detail you can decide well yourself), write ONE clear question to `.hawkes/question.md` (the options
   you see and the one you'd pick), commit nothing, and stop. Studio gets you the answer and you continue in this same
   session.
9. Before you finish, write `.hawkes/learned.md`: what the next agent working on this project should know that
   isn't obvious from the code (at most 5 lines, only real knowledge: a trap you hit and how you got past it, how a part of
   the codebase really works, a check that matters). One per line, in this form:
   - lesson: <short title> — <what broke or misled you, and the fix>
   - convention: <short title> — <how this codebase does it>
   Write nothing there if you learned nothing new. Studio keeps these in the shared memory for the next agent.

## The product's story (for tone and intent)

--- sickofitgame-unity/Docs/Story.md
# Sick Of It: Story Bible

The story is the design document. Every level, map, enemy, upgrade and line of dialogue should be traceable to
something on this page. If a request needs something the story doesn't explain yet, add it here first, then build
it. Agents: read this before adding or changing a level, an enemy, an upgrade or a voice line.

Status: **draft for review** (2026-10-15; Circle X's played route built to Floor 901, 2026-10-19; Circle IX's played route built to the Spire Fiend's door at 802, 2026-10-19; prologue, the Shaft and the floor layout revised 2026-10-18; Waystones and runs 2026-10-18; the Magma King's three-phase fight, 2026-10-18 (now Floor 901, level 31); Shortcuts and Circle IX's first stretch, Levels 12-18, 2026-10-18; the slime named Yooba, 2026-10-18; **one level = one floor**, 2026-10-18, see [LevelDesign.md](LevelDesign.md); On the website, 2026-10-18; Teasers, 2026-10-18). Names marked *(working)* are placeholders.

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
| **The Spire Fiend** | Circle IX's Warden: a gaunt basalt demon rooted in the heart of the great spire at Floor 801, his lower body a spire instead of legs, rock spires bristling from his head, shoulders and back, thin orange seams glowing all over him. He **grows the spires**: every spire on the Cracked Road is one he pushed up through the road (that is why the road keeps splitting), and the great spire is the one he has grown for ages so that nothing in the Circle stands higher than him. Hell's loop is his too: every night the spire sheds back to the same height, every day he grows it again. He can't bear anything above him, and a hero climbing *up* is the worst thing he can think of. Not a showman like t
