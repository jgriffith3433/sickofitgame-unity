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
