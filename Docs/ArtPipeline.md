# Art Pipeline: from a description to an animated enemy

Making an enemy's art is **one spec file and one command**. The command generates a standing side view, the poses
its clips need, short clips between those poses, keys out the background, lines every frame up on the same point,
packs sprite sheets into `Assets/`, writes the entity's `visual.animator`, imports it, and leaves a preview to check.

```bash
node Tools/Art/art.mjs imp --start
```

**All GPU work goes through Hawkes Gen** (`C:\Projects\hawkesgen`, the shared generative-AI service; start it with
its `auto-start.bat`, it listens on http://127.0.0.1:8790). The command sends jobs to its tools with the client in
`Tools/Art/hawkesgen.mjs` (a copy of `hawkesgen/clients/js/hawkesgen.mjs`): poses are `image.edit` (Qwen-Image-Edit
2511), clips are `video.i2v` (Wan 2.2 first-last-frame), under project `sickofit` with labels `{ entity, pose }` or
`{ entity, clip }`. Hawkes Gen owns the AWS GPU fleet, the ComfyUI workflows and the tuned settings (its tool defaults
are Dose Of Destruction's `generation.json` defaults); every output also lands in its library with its recipe, and
its dashboard shows each job, its log and its cost. This repo never reads or stores AWS or API keys.

Keying, trimming and the BiRefNet cutout still come from Dose Of Destruction's Python
(`C:\Projects\doseofdestructionwebsite`, `DOD_ROOT` to move it): `_build_anims.py` and `tools/cutout.py`, run in
DoD's `.venv`. Read `hawkesgen/docs/comfyui.md` before asking for new GPU settings, and DoD's `docs/animation.md`
for what makes a good clip.

## 1. Write the spec

`Assets/GameConfig/art/<id>.json`, next to the content. The config importer ignores this folder (only the command
reads it). The entity (`content/entities/<entity>.json`) must exist first: art comes after gameplay, and the
enemy must fit [Story.md](Story.md).

```json
{
  "schemaVersion": 1,
  "entity": "imp",
  "name": "Imp",
  "description": "a small wiry hell imp, one of the Overlord's raiders: crimson leathery skin, ... a glowing ember in one clawed hand",
  "height": 0.75,
  "poses": {
    "stand": "hovering in mid-air on spread bat wings, legs dangling, the ember held up in one hand",
    "dead": "dropping out of the air, crumbling into a cloud of grey ash and dying embers"
  },
  "clips": {
    "idle":   { "from": "stand", "action": "hovers in place, flapping its bat wings steadily ...", "seconds": 2 },
    "attack": { "from": "stand", "action": "winds its arm back and hurls the glowing ember forward ...", "seconds": 1.5 },
    "hurt":   { "from": "stand", "action": "is struck, jerks backward with a yelp, then recovers", "seconds": 1 },
    "die":    { "from": "stand", "to": "dead", "action": "crumbles into grey ash and fading embers", "seconds": 1.5 }
  }
}
```

| Field | What it does |
|---|---|
| `entity` | The entity that gets the art (default: the file name). |
| `name`, `description` | Who it is and what it looks like. The same description goes into every image and clip, so be specific about colors, body and props. Say what makes it *this* Circle's creature. |
| `height` | How tall the standing figure is in the game, in world units (the screen is 5.76 tall). Imp 0.75, Cinder 0.35, Magma King 1.8. |
| `poses` | Stills the clips start and end on, each a phrase ("crouched low, ..."). `stand` is made first from the description; every other pose is made from `stand` so it keeps the same look, size and ground line. Only poses a clip uses are made. |
| `clips` | Any of `idle`, `move`, `attack`, `hurt`, `die`. `idle` is required. |
| `reference` | Optional: start `stand` from existing art (`"Images/Demon2.png"`, or an absolute path) instead of from text. |
| `style`, `background`, `negative`, `prompts`, `takes`, `frameFill` | Overrides of the defaults in `Tools/Art/art.mjs` `DEFAULTS`; `art/_defaults.json` overrides them for every enemy (the game-wide style line lives there if you change it). |
| `pixels` | Standing height in sheet pixels (default `height × 280`, 96–400). |
| `visualScale` | Set the entity's `visual.scale` too (normally the entity's own is kept). |
| `keepTint` | Keep a placeholder `visual.color` tint (normally removed: real art isn't tinted). |
| `animator` | `attackParam`, `attackEvent`, `moveParam` when the entity's abilities don't already name them. |

Per clip:

| Field | |
|---|---|
| `from`, `to` | The poses it starts and ends on (default `stand`; `to` defaults to `from`). Same pose = it loops cleanly. |
| `action` | What happens, as a verb phrase after the name ("hurls the ember forward"). In place: the game moves the entity. |
| `seconds` | Length to generate (Wan makes 16 fps). Moves read best at 1–1.5 s. |
| `loop`, `fps` | Defaults: idle/move loop; 12 fps. |
| `speed`, `start`, `end`, `autotrim`, `flip`, `tolerance`, `size`, `hit` | Same as DoD's `clips.json` (docs/animation.md "Trimming and fixes"): speed up, trim seconds, keep the whole clip, mirror, keying tolerance (default 40), size multiplier, the attack's hit frame by hand. |
| `greenKill` | Default on: also clears strongly green pixels and dark green-tinted shadows Wan paints inside the figure (out of the key's reach). Set `false` for a green creature. |
| `video` | Use this mp4 instead of generating one (see *Videos you already have*). |
| `prompt`, `negative`, `settings` | Override the clip prompt, add to the negative, or override Wan settings: any `video.i2v` param (`steps`, `split`, `motionSteps`, `cfgHigh`, `cfg`, `shift`, `loraHigh`, `loraLow`, `loraVersion`, `motionAmplitude`); others are ignored with a warning. |

Write descriptions the way the cookbook's art rules want: one creature, side view, facing right. Don't mention the
background or the camera; the prompts add that.

## 2. Run it

```bash
node Tools/Art/art.mjs imp            # everything missing or out of date, then build, apply, import
node Tools/Art/art.mjs imp --start    # also start a stopped GPU server if none is ready
node Tools/Art/art.mjs imp --status   # what exists and what's out of date
node Tools/Art/art.mjs --list         # every spec
```

| Step | What happens | Time (L40S) |
|---|---|---|
| server | Needs a running Hawkes Gen GPU server (or the fleet's auto start on). With `--start`, asks Hawkes Gen to start a stopped one (an L40S, about $2/hour; it stops itself after its idle time, 60 min by default); the jobs wait in Hawkes Gen's queue until it is ready. | 2–5 min to start |
| stand | `image.edit` (Qwen-Image-Edit 2511) paints the description on a flat green canvas (or turns `reference` side-on): 3 takes, each cut out and scored (one whole figure, inside the frame, a sensible size); the best is kept. | ~15 s a take, +1 min model load |
| poses | Each other pose, from `stand` (`image.edit`): 2 takes, best kept. | ~15 s a take |
| framing | Every pose is placed on a 640×640 green frame at the **same scale as stand, feet on the same ground line, legs on the same centre**, so all clips share one anchor. | instant |
| clips | `video.i2v` (Wan 2.2 14B first-last-frame, Hawkes Gen's default settings plus the clip's `settings`): from pose → to pose, the clip prompt, 16 fps. | ~70–90 s a clip, +1 min model switch |
| build | `Tools/Art/sheets.py`: frames at 12 fps, one-shots trimmed to the motion, loop seams removed, ≤24 frames, green keyed out (DoD `key` + `drop_specks`), the attack's hit frame found (furthest silhouette from the first), cells cropped and scaled so standing = `pixels` tall, packed into `Assets/Art/Enemies/<Name>/<Name>_<clip>.png` with a `.sheet.json` sidecar. | seconds |
| apply | Writes `visual.animator` into the entity file (states, parameters, transitions, the attack event), sets `facesLeft: false`, drops a placeholder tint. | instant |
| import | Unity batch `GameConfigCommands.Import` (skipped with `--no-unity`). Waits up to 5 minutes while another Unity (a test run, the editor) has the project; if the editor is open, focus it instead and it imports by itself. | ~1 min |

Each take is its own Hawkes Gen job with a job key (`sickofit:<id>:<pose or clip>:<recipe key>:<seed>`); a pose's or
clip's takes are queued together, so with several servers running they run side by side. Unless you pass `--seed` or
`--redo`, the seeds come from the recipe key, so running the command again after a crash or Ctrl+C sends the same job
keys and Hawkes Gen hands back the jobs it already ran instead of paying for them again. Image jobs run before video
jobs so the GPU swaps models once. Each step is skipped when its output exists and its
**recipe** (a `.json` beside every pose and clip with the prompt, seed, settings and a key of everything it was made
from) still matches the spec. Change a pose's text and that pose and the clips using it are made again; change one
clip's action and only that clip is.

**Where things go**

| What | Where |
|---|---|
| Spec | `Assets/GameConfig/art/<id>.json` |
| Work folder (outside the repo) | `<game.json characterArt.enemySourceRoot>/Generated/<id>/` = `C:\Projects\SickOfItGameEnemies\Generated\imp\` |
| Takes and chosen poses | `poses/_takes/<pose>-<seed>.png` (+ `.json` recipe, `.cut.png` cutout), `poses/<pose>.png` |
| Framed poses | `framed/<pose>.png`, `framed/frame.json` (scale, ground line, centre) |
| Clips | `clips/_takes/<clip>-<seed>.mp4`, `clips/<clip>.mp4` |
| Preview | `preview.png` (every frame of every clip, red cross = pivot, gold box = attack hit frame), `preview-<clip>.gif` |
| Sheets | `Assets/Art/Enemies/<Name>/<Name>_<clip>.png` + `.sheet.json` |

### A real run (the Imp, 2026-10-15)

Warm server, 3 stand takes + 2 dead takes + 4 clips: **about 6 minutes of GPU** (first image job 105 s with the model
load, then ~7 s per image; first video 146 s with the switch to Wan, then 20–30 s per clip). Starting a stopped
server took a few minutes more. (That run talked to the servers directly; since then the same jobs go through Hawkes
Gen, which handles waiting for a server and retrying.)

## 3. Check it

1. **Open `preview.png`** (Read it as an image). Look for: the same creature in every row; the red cross in the
   same place on the body across clips; nothing cut off at a cell edge; no green fringe; the gold box on the frame
   where the throw or hit lands.
2. **Watch the gifs** for pops at a loop seam or a clip that drifts.
3. **Play it.** Spawn it in a level (or run the bot playtest, AGENTS.md) and watch idle → attack → hurt → death.
   `Assets/Tests/PlayMode/GeneratedArtTests.cs` checks the imp end to end (sprites load, the shot waits for the Attack
   event, Hurt on a hit, Die on death); copy it for a new enemy. Run one test with an escaped filter (it's a regex):
   `-runTests -testPlatform PlayMode -testFilter 'GeneratedArtTests'` or `'LevelPlaytests\.PlayLevel\(6\)'`.
   The command prints a size check: figure height vs the collider radius. If the art is much bigger or smaller
   than the collider, change the entity's `body.radius`, not the art.

**Fixing a bad result** (no GPU unless noted):

| Problem | Fix |
|---|---|
| A pose or clip looks wrong | `--redo attack` (or `--redo stand,dead`) makes it again with a new seed (GPU). `--takes 3` for more to choose from. |
| A different take was better | `--pick stand=123456` (the seed from the file name), then the command rebuilds what depends on it. |
| Wrong creature | Rewrite `description`; everything is out of date and remade. |
| Clip too slow / long hold at the start | `"speed": 1.5`, or `"start"`/`"end"` in seconds. |
| Background left at the edges / body parts keyed out | `"tolerance"`: higher / lower than 40. |
| Hit lands on the wrong frame | `"hit": 7` on the attack clip. |
| Clip faces left | `"flip": true` (or `--redo` it). |
| Rebuild sheets only | `--build`. |

While a job waits, the command prints what Hawkes Gen says it is waiting for. If no server can start (AWS capacity),
try again in a few minutes or start one in Hawkes Gen's dashboard (Fleet tab, or `hawkesgen.cmd fleet start`); the
jobs use any ready one. If the command says it can't reach Hawkes Gen, run `C:\Projects\hawkesgen\auto-start.bat`.

## Wiring: what the animator does

The command builds the animator from the clips there are, using the parameter and event names the entity's
abilities already use (including abilities inside `states`):

| Clip | State | Plays when |
|---|---|---|
| idle | `Idle` (default) | otherwise |
| move | `Move` | the movement ability's `animBool` (e.g. `chase.animBool: "IsMoving"`) is true |
| attack | `Attack` | the attack ability's `animTrigger` is pulsed (`shoot`, `melee`, `spawner`); the event at the hit frame is its `fireOnEvent` / `hitEvent`, so the shot leaves on the throw |
| hurt | `Hurt` | the engine pulses the `Hurt` trigger on every hit that doesn't kill (`Entity.HurtParameter`); not from Attack, so a hit can't swallow a shot |
| die | `Die` | the engine plays it where the entity died, as a presentation-only effect (`Entity.DieState`, `Fx.Flipbook`), instead of the pop |

If an ability doesn't name its parameter, the command prints a note saying which field to add (for example
`"animBool": "IsMoving"` on `chase`). Entities without a `Hurt` or `Die` state behave as before.

## Videos you already have (the mp4 → frames path)

Any mp4 of the creature on a flat background (green, black or white), side view, in place, works:

- put it in the spec as a clip's `"video": "C:/path/Attack.mp4"`, or
- drop it in the work folder as `clips/<clip>.mp4` (a clip without a recipe is treated as hand-made and never
  replaced unless you `--redo` it),

then `node Tools/Art/art.mjs <id> --build`. A hand-made video is measured from its own first frame (standing height,
ground, centre). The older `SickOfIt > Regenerate Enemy Sprites` (`CharacterSpriteBaker`) still bakes the legacy
`enemies/` configs; new entities use this pipeline.

This is **not** the `VideoSpriteSheetBaker` path ([VideoPlayback.md](VideoPlayback.md)): that one bakes full-screen,
opaque story videos and backgrounds into chunked Addressables played by `SpriteFramePlayer`. Entity art needs alpha,
a shared pivot and a small sheet per clip, which is what this pipeline makes.

## How the Unity side works

- `GeneratedSheetImporter` (an `AssetPostprocessor`) slices any `X.png` that has an `X.sheet.json` beside it:
  multiple sprites named `<clip>_NN`, the sidecar's pixels per unit, and a custom pivot at the middle of the standing
  body. A changed sidecar re-slices its sheet. The pipeline never has to open Unity to set import settings.
- Pixels per unit = `pixels × visual.scale / height`, so the figure is `height` units tall at the entity's scale and
  gameplay tuning (collider, muzzle offsets) is untouched.
- Sheets are at most 2048 wide, compressed, no mipmaps; a 24-frame clip of a 200 px figure is about 2048×700.
- The content importer adds the sheets to the asset registry because the entity references them, as with any art.

## For agents: "make the X" checklist

1. Story first: the enemy is in [Story.md](Story.md)'s roster with a reason, and its entity exists with gameplay.
2. Write `Assets/GameConfig/art/<id>.json` (copy `imp.json`). Clips: what the entity does (a shooter: idle, attack,
   hurt, die; a walker adds move).
3. Make sure the entity's abilities name their animation parameters (`animTrigger` + `fireOnEvent` on shooters,
   `animBool` on movers); add them if the command's notes say so.
4. Hawkes Gen running, then `node Tools/Art/art.mjs <id> --start`. Takes ~15 minutes for five clips on a warm server.
5. Read `preview.png`; `--redo` what's wrong.
6. Check `Logs/GameConfig.log`; playtest.
7. Update the roster's *Art* column in Story.md.

The current specs: `imp` (Circle X shooter, **made**: `Assets/Art/Enemies/Imp/`), `cinder` (living spark, chaser: give its `chase` an `"animBool":
"IsMoving"` for the move clip), `magmaKing` (Warden of Circle X).
