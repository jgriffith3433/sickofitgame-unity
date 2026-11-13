# Sound audit

`audio.json` is the source of every sound and voice line. `python3 Tools/Audio/sound_audit.py --check` (no Unity) is
the validator: it exits 1 for a sound id without a clip, a referenced id audio.json doesn't define, or a sound with
neither prompt nor clip. The EditMode test `SoundClipTests` runs the same rules in Unity, and a game config import logs
a warning per sound without a clip. Run `--report` for this table.

## Missing at the audit (2026-10-25), before generation

Priced with Hawkes Gen (`hawkesgen_estimate`): audio.sfx ~$0.024 a take, audio.voice ~$0.0168 a take.

| Sound id | Kind | Speaker | Used by | Price |
|---|---|---|---|---|
| `vo_yooba_buy_plaster` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_buy_soles` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_buy_heels` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_buy_trigger` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_hero_yooba_wipe` | Voice | player | event / script | $0.0168 |
| `vo_yooba_buy_dip` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_buy_jar` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_buy_sniffer` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_buy_doubledip` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_deaths_1` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_deaths_5` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_hero_yooba_deaths5` | Voice | player | event / script | $0.0168 |
| `vo_yooba_deaths_15` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_deaths_40` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_hero_yooba_deaths40` | Voice | player | event / script | $0.0168 |
| `vo_yooba_stock_rock` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_wheel` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_pebble` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_sack` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_pile` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_soot` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_king` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_spire` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_stock_thanks` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_welcome_sack` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_welcome_chimney` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_welcome_king` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_lick_1` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_lick_2` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_lick_3` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_gossip_1` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_gossip_2` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_gossip_3` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_pockets` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_humming` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_quest_offer` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_hero_yooba_quest_ask` | Voice | player | event / script | $0.0168 |
| `vo_yooba_quest_remind1` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_quest_remind2` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_quest_pin` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_quest_mitt` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_quest_bell` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_yooba_quest_done1` | Voice | yooba | ui/yooba.json | $0.0168 |
| `vo_hero_yooba_quest_done` | Voice | player | event / script | $0.0168 |
| `vo_yooba_quest_done2` | Voice | yooba | ui/yooba.json | $0.0168 |
| `pickup_lost_thing` | Effect | - | event / script | $0.0240 |
| `vo_hero_hint_imp` | Voice | player | content/entities/imp.json | $0.0168 |
| `vo_hero_hint_cinder` | Voice | player | content/entities/cinder.json | $0.0168 |
| `vo_hero_hint_impSack` | Voice | player | content/entities/impSack.json | $0.0168 |
| `vo_hero_hint_impSwing` | Voice | player | content/entities/impSwing.json | $0.0168 |
| `vo_hero_hint_impCutter` | Voice | player | content/entities/impCutter.json | $0.0168 |
| `vo_hero_hint_cinderThatch` | Voice | player | content/entities/cinderThatch.json | $0.0168 |
| `vo_hero_hint_impGuard` | Voice | player | content/entities/impGuard.json | $0.0168 |
| `vo_hero_hint_brimstoneDemon` | Voice | player | content/entities/brimstoneDemon.json | $0.0168 |
| `vo_hero_hint_demonWhelp` | Voice | player | content/entities/demonWhelp.json | $0.0168 |
| `vo_hero_hint_demonLobber` | Voice | player | content/entities/demonLobber.json | $0.0168 |
| `vo_hero_hint_whelpNest` | Voice | player | content/entities/whelpNest.json | $0.0168 |
| `vo_hero_hint_ricochetImp` | Voice | player | content/entities/ricochetImp.json | $0.0168 |
| `vo_hero_hint_blinkImp` | Voice | player | content/entities/blinkImp.json | $0.0168 |
| `vo_hero_hint_bulwarkImp` | Voice | player | content/entities/bulwarkImp.json | $0.0168 |
| `vo_hero_hint_leadImp` | Voice | player | content/entities/leadImp.json | $0.0168 |
| `vo_hero_hint_watcher` | Voice | player | content/entities/watcher.json | $0.0168 |
| `imp_hurt` | Effect | - | content/entities/imp.json | $0.0240 |
| `imp_die` | Effect | - | content/entities/imp.json | $0.0240 |
| `imp_attack` | Effect | - | content/entities/imp.json | $0.0240 |
| `cinder_hurt` | Effect | - | content/entities/cinder.json | $0.0240 |
| `cinder_die` | Effect | - | content/entities/cinder.json | $0.0240 |
| `cinderThatch_hurt` | Effect | - | content/entities/cinderThatch.json | $0.0240 |
| `cinderThatch_die` | Effect | - | content/entities/cinderThatch.json | $0.0240 |
| `impSack_hurt` | Effect | - | content/entities/impSack.json | $0.0240 |
| `impSack_die` | Effect | - | content/entities/impSack.json | $0.0240 |
| `impSack_attack` | Effect | - | content/entities/impSack.json | $0.0240 |
| `impSwing_hurt` | Effect | - | content/entities/impSwing.json | $0.0240 |
| `impSwing_die` | Effect | - | content/entities/impSwing.json | $0.0240 |
| `impSwing_attack` | Effect | - | content/entities/impSwing.json | $0.0240 |
| `impCutter_hurt` | Effect | - | content/entities/impCutter.json | $0.0240 |
| `impCutter_die` | Effect | - | content/entities/impCutter.json | $0.0240 |
| `impCutter_attack` | Effect | - | content/entities/impCutter.json | $0.0240 |
| `impGuard_hurt` | Effect | - | content/entities/impGuard.json | $0.0240 |
| `impGuard_die` | Effect | - | content/entities/impGuard.json | $0.0240 |
| `impGuard_attack` | Effect | - | content/entities/impGuard.json | $0.0240 |
| `brimstoneDemon_hurt` | Effect | - | content/entities/brimstoneDemon.json | $0.0240 |
| `brimstoneDemon_die` | Effect | - | content/entities/brimstoneDemon.json | $0.0240 |
| `brimstoneDemon_attack` | Effect | - | content/entities/brimstoneDemon.json | $0.0240 |
| `demonWhelp_hurt` | Effect | - | content/entities/demonWhelp.json | $0.0240 |
| `demonWhelp_die` | Effect | - | content/entities/demonWhelp.json | $0.0240 |
| `demonWhelp_attack` | Effect | - | content/entities/demonWhelp.json | $0.0240 |
| `demonLobber_hurt` | Effect | - | content/entities/demonLobber.json | $0.0240 |
| `demonLobber_die` | Effect | - | content/entities/demonLobber.json | $0.0240 |
| `demonLobber_attack` | Effect | - | content/entities/demonLobber.json | $0.0240 |
| `whelpNest_hurt` | Effect | - | content/entities/whelpNest.json | $0.0240 |
| `whelpNest_die` | Effect | - | content/entities/whelpNest.json | $0.0240 |
| `ricochetImp_hurt` | Effect | - | content/entities/ricochetImp.json | $0.0240 |
| `ricochetImp_die` | Effect | - | content/entities/ricochetImp.json | $0.0240 |
| `ricochetImp_attack` | Effect | - | content/entities/ricochetImp.json | $0.0240 |
| `blinkImp_hurt` | Effect | - | content/entities/blinkImp.json | $0.0240 |
| `blinkImp_die` | Effect | - | content/entities/blinkImp.json | $0.0240 |
| `blinkImp_attack` | Effect | - | content/entities/blinkImp.json | $0.0240 |
| `bulwarkImp_hurt` | Effect | - | content/entities/bulwarkImp.json | $0.0240 |
| `bulwarkImp_die` | Effect | - | content/entities/bulwarkImp.json | $0.0240 |
| `bulwarkImp_attack` | Effect | - | content/entities/bulwarkImp.json | $0.0240 |
| `leadImp_hurt` | Effect | - | content/entities/leadImp.json | $0.0240 |
| `leadImp_die` | Effect | - | content/entities/leadImp.json | $0.0240 |
| `leadImp_attack` | Effect | - | content/entities/leadImp.json | $0.0240 |
| `watcher_hurt` | Effect | - | content/entities/watcher.json | $0.0240 |
| `watcher_die` | Effect | - | content/entities/watcher.json | $0.0240 |
| `watcher_attack` | Effect | - | content/entities/watcher.json | $0.0240 |
| `impForeman_hurt` | Effect | - | content/entities/impForeman.json | $0.0240 |
| `impForeman_die` | Effect | - | content/entities/impForeman.json | $0.0240 |
| `impForeman_attack` | Effect | - | content/entities/impForeman.json | $0.0240 |
| `pileKeeper_hurt` | Effect | - | content/entities/pileKeeper.json | $0.0240 |
| `pileKeeper_die` | Effect | - | content/entities/pileKeeper.json | $0.0240 |
| `pileKeeper_attack` | Effect | - | content/entities/pileKeeper.json | $0.0240 |
| `roadBreaker_hurt` | Effect | - | content/entities/roadBreaker.json | $0.0240 |
| `roadBreaker_die` | Effect | - | content/entities/roadBreaker.json | $0.0240 |
| `roadBreaker_attack` | Effect | - | content/entities/roadBreaker.json | $0.0240 |
| `carver_hurt` | Effect | - | content/entities/carver.json | $0.0240 |
| `carver_die` | Effect | - | content/entities/carver.json | $0.0240 |
| `carver_attack` | Effect | - | content/entities/carver.json | $0.0240 |
| `magmaKing_hurt` | Effect | - | content/entities/magmaKing.json | $0.0240 |
| `magmaKing_die` | Effect | - | content/entities/magmaKing.json | $0.0240 |
| `magmaKing_attack` | Effect | - | content/entities/magmaKing.json | $0.0240 |
| `spireFiend_hurt` | Effect | - | content/entities/spireFiend.json | $0.0240 |
| `spireFiend_die` | Effect | - | content/entities/spireFiend.json | $0.0240 |
| `spireFiend_attack` | Effect | - | content/entities/spireFiend.json | $0.0240 |
| `wpn_equip_rocketLauncher` | Effect | - | content/weapons/rocketLauncher.json | $0.0240 |
| `wpn_empty_rocketLauncher` | Effect | - | content/weapons/rocketLauncher.json | $0.0240 |
| `wpn_equip_fists` | Effect | - | content/weapons/fists.json | $0.0240 |
| `wpn_equip_spitfire` | Effect | - | content/weapons/spitfire.json | $0.0240 |
| `wpn_empty_spitfire` | Effect | - | content/weapons/spitfire.json | $0.0240 |
| `wpn_equip_ricochetPistol` | Effect | - | content/weapons/ricochetPistol.json | $0.0240 |
| `wpn_empty_ricochetPistol` | Effect | - | content/weapons/ricochetPistol.json | $0.0240 |
| `wpn_equip_chainWhip` | Effect | - | content/weapons/chainWhip.json | $0.0240 |
| `wpn_empty_chainWhip` | Effect | - | content/weapons/chainWhip.json | $0.0240 |
| `wpn_equip_shotgun` | Effect | - | content/weapons/shotgun.json | $0.0240 |
| `wpn_empty_shotgun` | Effect | - | content/weapons/shotgun.json | $0.0240 |
| `wpn_equip_boomerangHalo` | Effect | - | content/weapons/boomerangHalo.json | $0.0240 |
| `wpn_empty_boomerangHalo` | Effect | - | content/weapons/boomerangHalo.json | $0.0240 |

138 clips, $2.87

## Result (2026-10-25)

All 138 clips were made with Hawkes Gen (`audio.sfx` $0.012 a second of sound, `audio.voice` about $0.024 a line) and are in
`Assets/Audio/Generated/`. Estimate $2.87, actual about **$2.14** (77 effects $0.67, 61 voice lines $1.47). After it
`sound_audit.py` reports 702 sounds, 0 without a clip.

What was added besides the Yooba and hero hint lines that had prompts but no clips:

- **Per-enemy sounds**, distinct for each of the 16 named enemies and the 6 floor bosses (`Tools/Audio/enemy_sounds.py`
  holds the table): `<id>_hurt`, `<id>_die` and, for everything that shoots, `<id>_attack`, wired as the entity's
  `sounds.hurtSound`, `deathSound` and the new `sounds.attackSound` (played when a `shoot` ability starts an attack).
  The cinders and the whelp nest have no attack: they don't shoot. The bosses' spoken lines were already voiced.
- **Per-weapon sounds**: `wpn_equip_<weapon>` (the switch, replacing the shared `ui_click`) for all 7 weapons and
  `wpn_empty_<weapon>` for the 6 that use ammo.
- `pickup_lost_thing`, Yooba's shop and quest lines, the stock/welcome/lick/gossip lines.

Not covered: the milestone banner texts (`showText` without a speaker) are silent banners by design.

## The validator

`python3 Tools/Audio/sound_audit.py --check` exits 1 on any of: a sound id in audio.json without a clip (or the file is
missing), a referenced id audio.json doesn't define, a sound with neither prompt nor clip. `SoundClipTests` (EditMode)
checks the first two in Unity, the config import warns per sound without a clip, and the `sound-audit` playtest scenario
reports them (`soundAudit.missing`) and plays one of each kind. A new sound: add its prompt, generate it, run the check.

The same check also fails (2026-11-11): a take file that is missing, a bus audio.json does not know, an Ambience-bus sound that does not
loop, a voice line with takes or a pitch change, one of the eleven core verbs with fewer than 3 takes (or a pitch variation over 6%, or more
than 4 copies), a level's `ambientSound` whose file is missing or that no Ambience-bus sound of audio.json describes. `SoundTakesTests`
(EditMode) runs the take picker, the duck maths and the same JSON rules in Unity.

## Takes, buses and beds (the player goal "every verb has a punchy sound that never grates")

Nov 11, 2026. Reference: Vlambeer's *Nuclear Throne* and Jan Willem Nijman's "The Art of Screenshake": loud, varied, layered sound on every
verb. What the code does (all presentation, nothing in the simulation, nothing rewound):

- **Takes.** `SoundDef.clip` is the first take and `SoundDef.takes` the others (audio.json `takes`: asset paths). `Sfx.Play` picks one at
  random per play and never the one it played last for that sound (`Sfx.PickTake`; a take that is not loaded yet is skipped; only effects
  have takes). Effects get a random pitch of +-`pitchVariation` (0.06 for the core verbs), **voice lines never** (the voice queue plays at
  pitch 1), and at most `Sfx.MaxCopies` = 4 copies of one effect play at once (`maxVoices` can only lower it).
- **Core verbs, 3 takes each** (`Tools/Audio/core_sfx.py` is the table, `--check` verifies the JSON): `player_shoot` (rewritten: a punchy
  bright arcade pop, the old prompt was "barely noticeable"), `hit_pop` (event `enemyHit`, plays on top of an enemy's own hurt sound
  whenever a shot lands), `kill_pop` (event `killPop`, plays with the enemy's own death sound: the hit and the kill land as layered pops in
  the same moment), `player_hurt`, `graze_tick`, `pickup_diamond`, `player_jump`, `player_land`, `player_dash`, `rewind_start`,
  `rewind_stop`. 33 takes.
- **Mixer groups** (`Core/Audio/AudioBuses.cs`): Master (AudioListener), Music, Voice, SFX, Ambience, UI — each content group with a trim
  in dB (`AudioBuses.SetTrimDb`) that `Sfx` (effects, voices, UI), `AmbienceBed` and `MusicVolume` multiply in; a sound's `bus` field picks
  its group (Auto: Voice for spoken lines, SFX for effects; menu clicks are UI). It is software on purpose: Unity's AudioMixer effects
  (ducking, limiter) are not supported in WebGL, where the game ships, and a software bus is the same on every device and testable.
  Snapshots: Pause (Music −10 dB, SFX muted), Low health (Music −4 dB + `player_heartbeat`), Rewind (SFX −6 dB). See docs/audio-mix.md.
- **Duck under voice.** When the voice queue starts a line (`AudioBuses.VoiceStarted`) Music drops **-8 dB** and Ambience **-4 dB** over
  **0.08 s**; when the line ends (one timer set at that start, `WaitForSecondsRealtime`: no polling anywhere) both come back over **0.4 s**.
  The ramp runs only while the duck moves. Back-to-back lines hold the duck.
- **Master limiter**: the page's, in `Tools/Web/hawkes-sound.js`: every AudioContext Unity makes gets a DynamicsCompressorNode
  (threshold -6 dB, knee 0, ratio 20, attack 3 ms, release 120 ms) in place of its `destination` (followed by a -3 dB trim that cancels the compressor's automatic make-up gain), so a pile of shots and
  voices peaks near -3 dBFS. Not yet verified in a WebGL build: whether Unity's "Compressed In Memory" `<audio>` clips (music, the beds) go
  through the context's destination and so the limiter. `hawkesSound.state()` carries `limiter` (how many) and `hawkesSound.limiter()` the gain reduction. The Editor and a
  standalone player have none (no WebGL, no problem).
- **Ambience beds** (`Core/Audio/AmbienceBed.cs`): `LevelConfig.ambientSound` + `ambientVolume` start with the level (`LevelRuntime`) on the
  Ambience bus, follow the Effects volume, and end with it. Seven place beds: Meadow calm/burning, Shaft (`ui/shaft.json`
  `ambientSound` → `AmbienceBed.PlaySound` while the Shaft is open), Floors 1000–997. Positional one-shots are
  `LevelConfig.ambientEmitters` → `AmbientEmitters` (random 4–12 s gaps, stereo pan by screen x, falloff by hero distance;
  phone cap = half of 8). Tables: `Tools/Audio/floor_ambience.py` (+ `synth_core_sfx.py` stand-ins). A bed is an audio.json
  Effect with `loop: true`, `bus: Ambience`; mono Vorbis, "Compressed In Memory", rides the level's scope. Not rewound: a
  rewind past the Meadow's `setAmbience` swap keeps the burning bed. Playtest `floor-ambience`.
- **Every start is reported** through `Sfx.Played` (id, take n of m, pitch, volume, bus), the audio scenario's log.

### The clips are stand-ins made by a synthesiser

Hawkes Gen's `audio.sfx` was not reachable from the agent that wrote this (its project access was refused for `sickofit`, `shared`,
`sick-of-it` and `sick-of-it-unity`), so the 33 takes and the 3 loops are **synthesised** by `Tools/Audio/synth_core_sfx.py` (Python
standard library + ffmpeg's Vorbis, seeded: the same files every run; `Assets/Audio/Generated/Core/*.ogg`, mono, with importer
`.meta`: effects "Decompress On Load", beds "Compressed In Memory"; 5-13 KB a take, 135-165 KB a loop). They are marked
`"generatedFrom": "synth|Tools/Audio/synth_core_sfx.py"` and every sound keeps its Hawkes Gen prompt. To replace them:
`python3 Tools/Audio/core_sfx.py --gen-list` prints the 33 takes (3 per sound, `new_take` for the 2nd and 3rd) and 3 loops, about
**$0.94** in all (audio.sfx; no GPU); drop the made files over the same names (`<id>_t1..3.ogg`, `<id>.ogg`) and set `generatedFrom` to the
prompt's key. Nothing else changes.

### The audio scenario

Playtest `audio` (`SickOfItPlaytest.Audio.cs`, ~7 min; run it on a desktop and at 390x844 touch): after the page's tap-to-play it loads
Level 1 directly and has the bot play 60 s of game time through real input, pressing a jump twice, a dash and a rewind at set moments (on a
phone a tap, a right-half swipe and R held), with two setup shortcuts (the graze distance widened, a diamond dropped at his feet). Every
sound that starts is a `sound` event, every voice line a `duck` event; the checks are: a sound for every verb (shot, hit, kill, graze,
pickup, jump, land, dash, rewind); no take twice in a row; effects within 6% of pitch and voices unmoved; at most 4 copies of one effect
at once; Music at least 6 dB under each voice line (the duck's own samples and the music source's volume) and back to its prior level after;
Level 1 logs an ambient bed and the Meadow's turns to its burning one; Floor 1000 logs its own and it is audible; the page's first tap
started sound and the master limiter is on the page; the main menu loaded within 20 s (WebGL). Metrics `audio.count.<verb>`,
`audio.voiceLines`, `audio.menuSeconds`.

## Hawkes Gen takes for the core verbs (Nov 12)

Eighteen events now have 3-4 takes made by Hawkes Gen (`audio.sfx-open`, Stable Audio 3, one prompt variant per take) in `Assets/Audio/Generated/Core/<id>_t<n>.ogg`:
`player_shoot`, `bullet_impact` (new, event `bulletImpact`), `hit_pop`, `kill_pop`, `player_hurt`, `player_die`, `graze_tick`, `player_dash`, `player_jump`, `player_land`,
`pickup_diamond`, `upgrade_pick` (the shop purchase), `imp_hurt`, `imp_die`, `cinder_hurt`, `cinder_die`, `impSack_die`, `cinderThatch_die`.
`Tools/Audio/bake_gen_takes.py` cuts leading silence, caps the length, levels every take to -17 dB RMS under a limiter and writes the .ogg and its .meta (a git-known .meta keeps its GUID);
`Tools/Audio/core_sfx.py` (table `GEN`) writes audio.json, with `generatedFrom` the Sound Generator's own key so the editor shows them Ready.
Ask Gen for `duration >= 1` with `trim: false`: a short request came back as a near-silent file (-70 LUFS) for graze, jump, dash, diamond and the imps' hurts.
`pickup_diamond` is exact-pitch and climbs a semitone per diamond inside 1 s (`GameplayHud.Diamond.cs`; playtest `audio`: 5 diamonds in 2 s, 5 rising pitches), maxVoices 4, cooldown 0.

## Death sting (Nov 13)

`ui_death_sting` (event `uiDeathSting`, played by `UiSounds.DeathSting` as the death screen opens, before the announcer) now has a clip: `Audio/Generated/Core/ui_death_sting.ogg`, a stand-in from `Tools/Audio/synth_core_sfx.py` (Hawkes Gen refused project `sickofit` to the agent that made it; remake it with `audio.sfx` from the prompt in audio.json and drop it over the name). The rewind whoosh (`rewind_start`/`rewind_stop`) and the hit thuds (`player_hurt`, `hit_pop`) already had takes. Playtest `score-floors` now also checks that the finale swap (`musicState finale`) fires on each of Levels 1-5.
