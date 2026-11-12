# Sick of It — audio mix

Software buses (Unity WebGL ignores AudioMixer DSP). Master is `AudioListener` via `GameSettings.MasterVolume`.
Content groups live in `AudioBuses` / `Sfx` / `MusicVolume` / `AmbienceBed` / `HeartbeatMix`.

## Buses

| Group | Role | Player control |
|---|---|---|
| Master | Listener gain | Settings → Master (+/−) |
| Music | Stage / score beds | Settings → Music (drag slider) |
| SFX | Combat one-shots, heartbeat | Settings → Effects (drag slider) |
| Voice | Spoken lines | Settings → Voice (drag slider) |
| Ambience | Level beds | Follows Effects |
| UI | Menu clicks (`ui_click`, `ui_back`) | Follows Effects; **not** muted on Pause |

## Dynamics (script gains)

| Moment | Music | Ambience | SFX | Attack / release |
|---|---|---|---|---|
| Voice line | −8 dB | −4 dB | — | 80 ms / 400 ms |
| Pause | −10 dB | — | muted | instant snapshot |
| Low health (1 heart) | −4 dB + `player_heartbeat` loop | — | — | snapshot + loop |
| Rewind | — | — | −6 dB | snapshot |

Cap: at most 4 simultaneous copies of one effect (`Sfx.MaxCopies`). `SoundDef.priority` steals a lower-priority copy when the cap or pool is full.

## Loudness targets

Bring clips to these with Hawkes Gen `audio.process` / `audio.analyze` when org credits allow (blocked this pass: studio had 1 credit, `audio.sfx` needs 8).

| Kind | Integrated | True peak |
|---|---|---|
| Music | −16 LUFS | ≤ −1 dBTP |
| SFX | −14 LUFS | ≤ −1 dBTP |
| Voice | −16 LUFS | ≤ −1 dBTP |
| Heartbeat (`player_heartbeat`) | −14 LUFS (loop) | ≤ −1 dBTP |

Until Gen remakes it, `HeartbeatMix` plays a procedural pulse so Low Health is always audible. Page master limiter: `Tools/Web/hawkes-sound.js` (−6 dB threshold, 20:1).

## Playtest

Scenario `audio-mix` (`SickOfItPlaytest.AudioMix.cs`): Settings slider height + persist, Level 1 voice duck, pause muffling, one-heart heartbeat. Logs `duck` and `snapshot` events.
