# Audio mix: buses, ducking, pause/low-health/rewind, Settings sliders

## What was wrong
The game already had software Music/Voice/SFX/Ambience buses and an −8 dB Music duck under voice, but not the full mix the brief asked for: UI bus, Ambience duck, 80 ms attack, Pause / Low-health / Rewind snapshots, a one-heart heartbeat, drag sliders that persist, or a playtest that logs ducks and snapshots.

## What changed
- **AudioBuses**: Music −8 dB + Ambience −4 dB under voice (80 ms / 400 ms); snapshots Pause (Music −10, SFX mute), LowHealth (Music −4), Rewind (SFX −6); `SnapshotChanged` for the playtest log. Master remains `AudioListener` / MasterVolume (WebGL still has no usable mixer DSP).
- **UI bus** + `SoundDef.priority` (cap/steal); `ui_click` / `ui_back` on Ui so pause menu clicks survive SFX mute.
- **HeartbeatMix**: at 1 heart plays `player_heartbeat` (procedural pulse until Gen credits allow a real `audio.sfx` loop); wired from LevelRuntime.
- **GameClock** sets Pause/Rewind snapshots on pause/rewind events.
- **Settings**: Music / Effects / Voice are drag sliders (≥48 px, touch-grown); menus playtest drags them.
- **Playtest `audio-mix`**: sliders persist, voice duck ≥6 dB, pause ≥8 dB, heartbeat at 1 heart.
- **docs/audio-mix.md**, Story / SoundAudit / world-bible notes.

## Gen / credits
Studio org credits were **1** (`audio.sfx` needs **8**). Heartbeat is procedural; loudness analyze/process not run. Listed in `.hawkes/pending-gpu.txt`. Project id used: `org_eb0d5c82e8da8c2d.sickofit`.

## Checks
- Unity compile **not run here** (no editor/dotnet Unity refs in this sandbox).
- Edited C# scanned for duplicate members / conflict markers (none).
- EditMode `SoundTakesTests` extended for 80 ms attack, ambience duck, snapshots, Ui bus (not executed here).

## Fun reviewer
**thin** — breathing/story scores high for the mix; gaps: Gen heartbeat clip, LUFS pass (credits), unrelated bare-floor one-shots (out of scope).

## Conflicts
None.

## Left
Gen `player_heartbeat` + optional LUFS pass when credits ≥ 8; then clear pending-gpu lines.
