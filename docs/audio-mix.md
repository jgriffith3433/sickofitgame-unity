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
| UI | Menu kit via `UiSounds` (`ui_hover`/`ui_click`/`ui_back`/`ui_toggle`/`ui_panel_open`/`ui_shop_open`/`ui_purchase`/`ui_cant_afford`/`ui_unlock`/`ui_rank_stamp`/`ui_new_best`/`ui_death_sting`, plus `level_clear`) | Follows Effects; **not** muted on Pause |

## Dynamics (script gains)

| Moment | Music | Ambience | SFX | Attack / release |
|---|---|---|---|---|
| Voice line | −8 dB | −4 dB | — | 80 ms / 400 ms |
| Pause | −10 dB | — | muted | instant snapshot |
| Low health (1 heart) | −4 dB + `player_heartbeat` loop | — | — | snapshot + loop |
| Rewind | — | — | −6 dB | snapshot |

Cap: at most 4 simultaneous copies of one effect (`Sfx.MaxCopies`). `SoundDef.priority` steals a lower-priority copy when the cap or pool is full.

## Loudness targets

Bring clips to these with Hawkes Gen `audio.process` / `audio.analyze` (both free, CPU). `Tools/Audio/loudness_report.py`
measures every clip in `audio.json` with ffmpeg (ebur128: integrated LUFS, true peak) and writes the per-bus table below
(`--write`); `--fix-list` names the clips outside target +-2 LU or above -1 dBTP, to feed to `audio.process`. It needs the real
audio files: a sparse checkout holds only Git LFS pointers (this pass), so the table is filled on a machine with LFS.

| Kind | Integrated | True peak |
|---|---|---|
| Music | −16 LUFS | ≤ −1 dBTP |
| SFX | −14 LUFS | ≤ −1 dBTP |
| Voice | −16 LUFS | ≤ −1 dBTP |
| Heartbeat (`player_heartbeat`) | −14 LUFS (loop) | ≤ −1 dBTP |

`player_heartbeat` is a Hawkes Gen clip (`audio.sfx`, Stable Audio 3, 2 s loop, job j_4c88fa89eada, `audio.process` to -18 LUFS / -2.2 dBTP, `Assets/Audio/Generated/Core/player_heartbeat.ogg`, wired in `audio.json`). `HeartbeatMix` keeps a procedural pulse only as a fallback for a checkout without the file. Gen's loop check notes a head/tail level difference: the clip is two thumps, so the tail is naturally quieter; the seam itself has no click. Page master limiter: `Tools/Web/hawkes-sound.js` (−6 dB threshold, 20:1).

## Playtest

Scenario `audio-mix` (`SickOfItPlaytest.AudioMix.cs`): Settings slider height + persist, Level 1 voice duck, pause muffling, one-heart heartbeat. Logs `duck` and `snapshot` events.
