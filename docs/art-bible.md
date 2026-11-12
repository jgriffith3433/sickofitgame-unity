# Sick of It: art bible

How everything on screen is drawn, so the hero, the imps, the stones and the lava read as one hand-inked world.
Story and tone: [Docs/Story.md](../Docs/Story.md), [world-bible.md](world-bible.md). How art is made step by step:
[Docs/ArtPipeline.md](../Docs/ArtPipeline.md). Light and grade: [Docs/lighting.md](../Docs/lighting.md).

**Reference:** Hollow Knight. Hand-inked silhouettes with a heavy dark outline, flat cel colour, a thin rim of light, motion
that reads at any size. The joke is the hero's boredom, so poses are tired and economical, never heroic.

## 1. Line

| Thing | Rule |
|---|---|
| Outline colour | `#140B0D` (warm near-black), never pure `#000000`, never a lighter tint of the fill |
| Hero outline weight | 3 px at the 256 px cell height (about 1.2% of the figure); 4 px on the silhouette's outer edge, 2 px for inner folds |
| Enemy outline | 3 px on figures over 128 px tall, 2 px under; bosses 4 px |
| Props and stones | 2 px outline on the walkable top edge and the side, none inside |
| Line quality | one unbroken contour, slightly uneven (ink, not vector); no anti-aliased halo in the key colour; no glow bleeding through the outline |
| On phones | the outline is what keeps him readable: at 390x844 the hero is at least 48 px tall and the outline stays at least 1 px on screen (the 256 px cell is shown at ~0.19x, so never draw thinner than 3 px) |

## 2. Shading: two-tone cel plus a rim

- Each material has **one base tone and one shadow tone** (shadow is the base shifted about 25% darker and 8% toward blue-violet). No gradients, no soft airbrush, no more than 2 tones plus highlight.
- One **rim light** on the edge facing the nearest light, 2-3 px wide, in the floor's light colour (table below). Hellfire from below is the default; the hero also gets a game-side rim (`WorldLighting`), so the painted rim is subtle.
- One **glow** colour per figure for what is alive: the hero's eye slits and launcher chambers `#FF8A1F`; imps' ember hand; the Overlord's lens. Glows are the only things lighter than the rim.
- Bronze armour: base `#8A5A2B`, shadow `#4A2E1C`, brass trim `#C89A3A`, tattered cloth `#7A1F2B` (shadow `#3F0F1A`). Keep these exactly (his design is fixed).

## 3. Palette per level

Each floor is lit by its grade (Docs/lighting.md; `game.json` `lighting` profiles). Painted art stays neutral and lets the grade
and rim colour do the work, so one sheet works on every floor. The rim and ambient to keep readable against are:

| Place | Profile | Background value | Rim / light | Hero check |
|---|---|---|---|---|
| The Meadow | `meadow` | bright, `#E8C48A` dusk | warm gold `#F4C878`, raid red `#E85A2E` | dark outline carries him on the light sky |
| Floor 1000, The Landing | `floor1000` | sulphur orange `#D4A04A` | orange `#F29A3A` | outline vs mid-orange |
| Floor 999 | `floor999` | furnace red `#C43A28` | hot orange `#FF7A30` | rim must not match the red: keep it orange-yellow |
| Floor 998, The Spill | `floor998` | bone grey-violet `#8A7A9A` | cool grey `#C9B8D6` | armour is warmer than the ground, so it separates |
| Floor 997, Imp Run | `floor997` | obsidian + magenta `#C060A8` | magenta `#D870C0` | bright eyes and brass trim pop on the dark |
| Circles later (Ember Wastes `emberWastes`, `crackedRoad`, `runeBridges`) | each Circle's backdrop | see Circle def | its own | same outline, same two tones |

Rules: backgrounds never use `#140B0D` nor the hero's bronze; hazards are brighter and more saturated than platforms; the
player is the only warm bronze figure on screen (enemies are crimson, slag-black or violet).

## 4. Silhouette

- A figure must be recognisable **filled solid black** at 48 px tall. Test: threshold the sheet; the hero reads as a helm, a raised launcher and a trailing cloth, nothing else.
- Every figure has one **signature shape**: the hero's closed round helm with a launcher over one shoulder; imps' bat wings; brutes' wide shoulders. Keep it in every frame.
- No limb crosses the torso on a flat-front pose; open the gap so arms and legs show daylight.
- Weapon sits above the shoulder line, never across the face. Cloth trails behind in the direction of travel.
- Poses are broad and tired: slumped shoulders, a long sigh, never a hero stance.
- Facing: art faces **right**; the game flips for left (`use2DFlip`). Nothing asymmetrical that must read mirrored (no text on armour).

## 5. Sizes and pixels per unit

- **Hero (sheets):** 604 x 340 px cells, centre pivot, **100 pixels per unit** (Idle's). `spriteScale` 0.25 in player.json; the figure is about 0.85 units tall (85 px in the cell at the 256 px authoring height; the cell is mostly padding for weapons and effects). **Never change cell size, pivot, `colliderRadius` or the ground line**: the hitbox and feet stay where they are.
- Authoring: video frames 1280x720, keyed, trimmed and baked to the cell by `player_clip_sheet.py`. For new clips: 256 px figure height at generation, 12 fps source, baked as above.
- **Enemies:** `height` in the art spec in world units (imp 0.75, brute about 1.1, bosses 2-4); baked to cells at 100 PPU like the hero.
- **Climb art** (stones, rafts, chains, lava): `Assets/Art/Climb/`, 100 PPU; each platform's walkable top is fitted to its collider.
- **Effects and bullets:** 100 PPU; `fx_sheets.py`; light-painted (`tintable`) art is white.
- Phones (ASTC) import at most 2048 px; desktop 2048. A hero sheet is one 2040 px-wide atlas (multiples of 4).
- Hero on phones at 390x844: >= 48 px tall on screen, outline visible against every floor above.

## 6. Hero animation set and timing

Same states as `player.json` `animationConfig` (do not rename). Inked set, key 00ff00, 12 fps, 256 px authoring cells:

| State | Frames | Notes |
|---|---|---|
| Idle | 24 (loop) | bored breathing; a long sigh (shoulders drop, helm tilts down) every ~4 s, built into the loop |
| Walk (run) | 12 or 16 (loop) | planted foot moves at floor speed: **stride length = `moveSpeed` (3.8 u/s) x loop duration / 2 steps**; with 12 frames at 15 fps (0.8 s) the stride is 1.52 u per step. Set `fps` so planted feet move <= 2 px per step in footage; change fps, not `moveSpeed` |
| Jump | 8 | anticipation, launch, tuck; does not loop |
| Fall | 6 (loop) | cloth flares upward, legs trailing |
| Land | 6 | squash on frame 2, recover; never longer than 0.4 s |
| Dash | 6 (loop) | stretch forward, cloth streaming |
| Hurt | 6 | helm snaps back, one gauntlet up; no frame brighter than the flash |
| Death (burn, squash, spin, shrug) | 12 each | each reads in 1 s and ends on a still last frame |
| Rewind pose | 6 | leaning back, head turned, cloth lifted (plays while R is held) |
| Victory (shrug) | 12 | a shrug, not a cheer |

Frame picking: take every frame that sells the extremes (contact, passing, high point), drop blurry or off-model frames, keep a
consistent ground line; check the loop's last frame against the first.

## 7. How Hawkes Gen makes each kind of art here

All through Hawkes Gen under project `org_eb0d5c82e8da8c2d.sickofit` (the short name `sickofit` is refused); never hold keys in the repo.

| Kind | Tool and where |
|---|---|
| Hero and creature stills (poses) | `image.edit` (Qwen-Image-Edit) from a reference pose, flat black or `00ff00` background; `Tools/Art/art.mjs <id> --poses-only`, `--pick pose=seed` |
| Hero concepts (look-dev only) | `image.generate` x3 with the current sheet as reference; pick one before any pose |
| Moves (clips) | `video.i2v` (Wan 2.2 first-last-frame), still camera, flat 00ff00; or `video.animate` copying an existing motion (`"motion": "player:Walk"`) |
| Sheets | `sprite.sheet`-style bake: `Tools/Art/player_clip_sheet.py` (hero), `Tools/Art/sheets.py` (enemies): key, trim, align, pack to 604x340 at 100 PPU |
| Enemy art | `Assets/GameConfig/art/<id>.json` then `node Tools/Art/art.mjs <id> --start` (Docs/ArtPipeline.md) |
| Effects, portals, beams | `Tools/Art/fx_make.mjs` / `fx_sheets.py` (key magenta or black) |
| Climb art | `Tools/Art/climb_art.py` from `Tools/Art/climb/climb.json` |
| Library and takes | `hawkes.library.json` maps each folder's `poses/` and `clips/` (and `_takes/`) to Gen library slots; `art.mjs` syncs both ways; reuse before regenerating (`reuse`) |
| Cost | `hawkesgen_estimate` first; each kind of job is one batch (one model load) |

Checks without GPU: `node Tools/Art/art.mjs hero --check` (spec vs wiring, 604x340 at 100 PPU), `python3 Tools/Art/art_inventory.py`.

## 8. The hero redraw (in progress)

- Design is fixed: armoured knight, battered dark bronze plate, brass trim, closed helm with two glowing orange eye slits, tattered maroon cloth, heavy gauntlets and greaves, broad and tired. Launcher from the start. Never a man in a jacket.
- Order: concepts (3) from the current Idle sheet -> pick -> key poses (`image.edit` each) -> clips (`video.i2v`, still camera, 00ff00) -> bake -> hand-pick frames -> wire into `player.json` replacing the old sheets (same state names, same cell, same pivot) -> match walk fps to `moveSpeed`.
- Old-versus-new screenshots go in the pull request.
