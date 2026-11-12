# Lighting and post (Meadow + Floors 1000–997)

Built-in renderer (no URP): a camera fullscreen grade pass (`SickOfIt/LevelGrade`) plus additive light sprites over a
multiply darkness layer (`WorldLighting`). Tuned in `game.json` `lighting`. Presentation only — never saved or rewound.

Reference mood: Dead Cells' torchlight and glowing pools, each biome with its own grade.

## Grades

| Place | Profile id | Dominant grade | Notes |
|---|---|---|---|
| The Meadow (Level 1) | `meadow` | warm dusk `#E8C48A` → raid red `#E85A2E` | Raid kicks in when `AmbienceBed` is the burning bed (sky opens). |
| Floor 1000, The Landing | `floor1000` | sulphur orange `#D4A04A` | Lava sea and stones. |
| Floor 999 | `floor999` | furnace red `#C43A28` | Rising heat. |
| Floor 998, The Spill | `floor998` | bone grey-violet `#8A7A9A` | Wreckage, ash. |
| Floor 997, Imp Run | `floor997` | obsidian with magenta `#C060A8` | Dark walls, magenta rim and bullet lights. |

Each profile also sets darkness multiply, bloom (threshold ≈ 1), vignette, grain (desktop), hero rim, lava / torch / bullet light colours and flicker.

## Lights

- **Lava:** additive pool, reach `lightReach` (2 world units), slow flicker. Platforms within that radius sit in the glow.
- **Torches / lamps:** warm falloff, faster flicker (entity id or tag contains `torch` / `lamp` / `lantern`).
- **Hero rim:** soft duplicate sprite + a rim light so he separates from every backdrop.
- **Enemy bullets:** small additive lights (on top of the readability rims).
- **Meadow calm:** a soft dusk pool above the hero when there is no lava yet.

## Quality tiers

Chosen by touch primary **and** short screen side ≤ `phoneShortSide` (500), or `GameplayHud.TouchUiForced` in playtests.

| Tier | Bloom | Grain | Max lights |
|---|---|---|---|
| Desktop | full res | on | 24 |
| Phone | half res | off | 8 |

Reduced Flash softens bloom and grain further.

## Rewind

While rewinding, the world desaturates in presentation (`RewindWorldLook`) with the hero and bullets kept in full colour; gold time-ripple, optional rim scanlines and sand/ember motes sit only on the outer ~8% frame (letterbox / screen edge) so the central playfield stays clear. On release, colour snaps back in 0.2 s with a ripple from the hero.

## Code

- `Data/Config/LightingConfig.cs` — profiles and quality
- `Gameplay/Presentation/WorldLighting.cs` — darkness + lights + rim
- `Gameplay/Presentation/LevelGradePass.cs` — `OnRenderImage` grade
- `Resources/Feel/LevelGrade.shader`, `SpriteMultiply.shader`
- Playtest `lighting` (`SickOfItPlaytest.Lighting.cs`)
