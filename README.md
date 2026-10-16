# Sick Of It

A 2D side-view arcade bullet-dodger about a hero who is completely sick of everything. Dodge waves of slimes, demons
and fire rain, rewind time when it goes wrong (if you have the juice), collect diamonds, buy upgrades, and survive
five levels of increasingly bad days. Built with Unity 6.3 (6000.3.7f1), shipped to WebGL.

## Getting started

1. Open the project in Unity **6000.3.7f1** (Unity Hub).
2. Run **SickOfIt > Generate Project** once (and again after changing generator code). It builds the prefabs and
   scenes from the config.
3. Open `Assets/Scenes/MainMenu.unity` and press Play.

Controls: move with WASD/arrows or the left stick, aim and fire with the mouse (or right stick), jump with Space,
**hold R** (or the on-screen ◄◄ button) to rewind, Esc/P to pause.

## How the game is made

The whole game is defined in JSON under **`Assets/GameConfig/`**: levels and their scripted events, enemies and
bosses, upgrades, menus and their look, every sound and voice line. The editor imports the JSON automatically when it
changes; most changes need no code and no regeneration.

| What | Where |
|---|---|
| Levels (waves, phases, banners, boss fights) | `levels/*.json` (`script` field) |
| Enemies, bosses, projectiles, pickups | `content/entities/*.json` |
| Between-level upgrades | `content/upgrades/*.json` |
| Shop upgrades (bought with diamonds) | `player.json` → `availableUpgrades` |
| Menus, HUD look, all on-screen words | `ui/theme.json`, `ui/*.json` |
| Sound effects, voice lines, voices | `audio.json` |

Guides:
- **[AGENTS.md](AGENTS.md)**: how the project works and the rules for changing it (for people and coding agents).
- **[Docs/GameplayCookbook.md](Docs/GameplayCookbook.md)**: recipes ("make a boss fight", "add a voice line",
  "add a shop upgrade", "restyle the menu").
- **[Docs/GameplayReference.md](Docs/GameplayReference.md)**: every content field (generated).
- **[ARCHITECTURE.md](ARCHITECTURE.md)** and **[CONVENTIONS.md](CONVENTIONS.md)**: code structure and C# style.

## Sound and voices (Hawkes Gen / ElevenLabs)

Every sound in `audio.json` is a text prompt; the audio files are generated with
[ElevenLabs](https://elevenlabs.io) through Hawkes Gen (`C:\Projects\hawkesgen`, which holds the key):

1. Start Hawkes Gen (`C:\Projects\hawkesgen\auto-start.bat`).
2. Open **SickOfIt > Audio > Sound Generator (Hawkes Gen)**.
3. **Voices**: design the `player` and `announcer` voices from their descriptions ("Design 3 candidates", play, Use).
4. **Generate** makes every sound that's missing or whose prompt changed. **3 takes** makes variations to pick from.

Generation uses ElevenLabs credits (Hawkes Gen's dashboard shows the usage). Generated files live in
`Assets/Audio/Generated/`, and every take is also kept in Hawkes Gen's library.

## Testing

With the editor closed (batch mode; see AGENTS.md for the full commands):

- Config check: `-executeMethod SickOfIt.Tools.Editor.GameConfig.GameConfigCommands.Validate`
- Unit tests: `-runTests -testPlatform EditMode` and `-runTests -testPlatform PlayMode`
- Bot playtest of every level: `-runTests -testPlatform PlayMode -testCategory Playtest`, then read
  `Logs/Playtest/report.md`. Every play in the editor also writes a log to `Logs/Playtest/`.

Config errors and warnings (with file, line and a suggested fix) are written to `Logs/GameConfig.log` on every import.

## Building

WebGL builds use Addressables for scenes and video frames. Build from Unity's Build Profiles as usual; the config is
imported and validated before every build, and a build with config errors is refused. Gameplay content and level
scripts can also ship without a rebuild via a remote content bundle (`remoteContentUrl` in `game.json`, then
**Build Remote Content Bundle**).
