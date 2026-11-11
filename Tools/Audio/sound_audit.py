#!/usr/bin/env python3
"""Sound audit and validator for Assets/GameConfig/audio.json (no Unity needed).

  python3 Tools/Audio/sound_audit.py            list every problem, with the price of making the missing clips
  python3 Tools/Audio/sound_audit.py --report   the same as markdown (Docs/SoundAudit.md's table)
  python3 Tools/Audio/sound_audit.py --check    quiet; exit 1 on any problem (the validator)

Problems (each one fails --check):
  - a sound id in audio.json without a clip (no clip path, or the file is not in Assets/)
  - a sound id referenced by the game's config (events, entities' sounds.*Sound, weapons' sounds, hintVoice, showText
    and other *Sound / sound fields, ui/*.json lines) that audio.json does not define
  - a sound with neither a prompt nor a clip, or a voice line that names a voice audio.json does not have
  - takes and buses (Docs/SoundAudit.md "Takes, buses and beds"): a take file that is missing, a bus audio.json does not know, an
    Ambience-bus sound that does not loop, a voice line with takes or a pitch variation, a core verb with fewer than 3 takes, a level's
    ambientSound whose file is missing or that no Ambience-bus sound of audio.json describes
Prices are Hawkes Gen's median per take (hawkesgen_estimate, 2026-10-25): audio.sfx $0.024, audio.voice $0.0168.
"""
import glob, json, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
CONFIG = os.path.join(ROOT, 'Assets/GameConfig')
PRICE = {'Effect': 0.024, 'Voice': 0.0168}
BUSES = ('Auto', 'Sfx', 'Voice', 'Ambience', 'Music')
# The verbs of the player goal "every verb has a punchy sound that never grates": 3 takes each, effects on the SFX bus.
CORE_VERBS = ('player_shoot', 'hit_pop', 'kill_pop', 'player_hurt', 'graze_tick', 'pickup_diamond', 'player_jump', 'player_land',
              'player_dash', 'rewind_start', 'rewind_stop')
CORE_TAKES = 3
KEY = re.compile(r'(sound|Sound|hintVoice|heroLine|sfx)$')
# fields called "sound" that are not audio ids (theme.json's "Sound" is the settings label)
SKIP_FILES = ('audio.json',)


def refs_in(o, f, out, key=''):
    if isinstance(o, dict):
        for k, v in o.items():
            if isinstance(v, str) and KEY.search(k) and v and '/' not in v and ' ' not in v:
                out.setdefault(v, set()).add(os.path.relpath(f, CONFIG))
            elif isinstance(v, list) and KEY.search(k):
                for x in v:
                    if isinstance(x, str) and x and '/' not in x and ' ' not in x:
                        out.setdefault(x, set()).add(os.path.relpath(f, CONFIG))
            if k == 'sounds' and isinstance(v, dict):  # a weapon's or entity's sounds block
                for sk, sv in v.items():
                    if isinstance(sv, str) and sv and '/' not in sv and ' ' not in sv:
                        out.setdefault(sv, set()).add(os.path.relpath(f, CONFIG))
            refs_in(v, f, out, k)
    elif isinstance(o, list):
        for x in o:
            refs_in(x, f, out, key)


def audit():
    audio = json.load(open(os.path.join(CONFIG, 'audio.json'), encoding='utf-8'))
    sounds = {s['id']: s for s in audio['sounds']}
    # Lines with a prompt whose clip is not made yet (no voice budget): Tools/Audio/pending_clips.json. They are reported, not failed.
    pending_file = os.path.join(ROOT, 'Tools/Audio/pending_clips.json')
    pending = set(json.load(open(pending_file, encoding='utf-8'))['ids']) if os.path.exists(pending_file) else set()
    voices = {v['name'] for v in audio['voices']}
    refs = {}
    for v in audio['events'].values():
        if v:
            refs.setdefault(v, set()).add('audio.json events')
    for f in glob.glob(os.path.join(CONFIG, '**/*.json'), recursive=True):
        if os.path.basename(f) in SKIP_FILES:
            continue
        try:
            refs_in(json.load(open(f, encoding='utf-8')), f, refs)
        except ValueError:
            pass
    problems = []   # (kind, id, message)
    missing = []    # sounds that need making
    for sid, s in sounds.items():
        clip = s.get('clip')
        if not clip or not os.path.exists(os.path.join(ROOT, 'Assets', clip)):
            missing.append(s)
            if sid not in pending:
                problems.append(('no-clip', sid, 'no clip' if not clip else f'clip file missing: {clip}'))
        if not s.get('prompt') and not clip:
            problems.append(('no-prompt', sid, 'neither a prompt nor a clip'))
        if s['kind'] == 'Voice' and s.get('voice') and s['voice'] not in voices:
            problems.append(('bad-voice', sid, f'unknown voice {s["voice"]}'))
    for sid, s in sounds.items():
        for take in s.get('takes') or []:
            if not os.path.exists(os.path.join(ROOT, 'Assets', take)):
                problems.append(('no-take', sid, f'take file missing: {take}'))
        bus = s.get('bus', 'Auto')
        if bus not in BUSES:
            problems.append(('bad-bus', sid, f'unknown bus {bus} (one of {", ".join(BUSES)})'))
        if bus == 'Ambience' and not s.get('loop'):
            problems.append(('bed-not-loop', sid, 'an Ambience-bus sound is a bed: loop must be true'))
        if s['kind'] == 'Voice' and (s.get('takes') or s.get('pitchVariation', 0)):
            problems.append(('voice-varied', sid, 'a voice line has one take and never changes pitch'))
        if s['kind'] == 'Voice' and bus not in ('Auto', 'Voice'):
            problems.append(('bad-bus', sid, f'a voice line plays on the Voice bus, not {bus}'))
    for sid in CORE_VERBS:
        s = sounds.get(sid)
        if s is None:
            problems.append(('core-verb', sid, 'a core verb is missing from audio.json'))
        elif 1 + len(s.get('takes') or []) < CORE_TAKES:
            problems.append(('core-verb', sid, f'needs {CORE_TAKES} takes, has {1 + len(s.get("takes") or [])}'))
        elif s.get('pitchVariation', 0) > 0.06 + 1e-6 or s.get('maxVoices', 3) > 4:
            problems.append(('core-verb', sid, 'effects vary pitch by at most 6% (0.06) and play at most 4 copies at once'))
    beds = {os.path.splitext(os.path.basename(s['clip']))[0] for s in sounds.values() if s.get('bus') == 'Ambience' and s.get('clip')}
    for f in sorted(glob.glob(os.path.join(CONFIG, 'levels/*.json'))):
        try:
            level = json.load(open(f, encoding='utf-8'))
        except ValueError:
            continue
        amb = level.get('ambientSound')
        if amb:
            name = os.path.basename(f)
            if not os.path.exists(os.path.join(ROOT, 'Assets', amb)):
                problems.append(('no-bed', name, f'ambientSound file missing: {amb}'))
            if os.path.splitext(os.path.basename(amb))[0] not in beds:
                problems.append(('no-bed', name, f'ambientSound {amb} is not the clip of an Ambience-bus sound in audio.json'))
    for rid, where in sorted(refs.items()):
        if rid not in sounds and rid != 'Sound':
            problems.append(('undefined', rid, 'referenced but not in audio.json: ' + ', '.join(sorted(where)[:3])))
    return sounds, refs, missing, problems


def main():
    sounds, refs, missing, problems = audit()
    price = sum(PRICE[s['kind']] for s in missing)
    if '--check' in sys.argv:
        for kind, sid, msg in problems:
            print(f'SOUND {kind}: {sid}: {msg}')
        sys.exit(1 if problems else 0)
    if '--report' in sys.argv:
        print('| Sound id | Kind | Speaker | Used by | Price |')
        print('|---|---|---|---|---|')
        for s in missing:
            used = ', '.join(sorted(refs.get(s['id'], [])))[:60] or 'event / script'
            print(f"| `{s['id']}` | {s['kind']} | {s.get('voice') or '-'} | {used} | ${PRICE[s['kind']]:.4f} |")
        print(f'\n{len(missing)} clips, ${price:.2f}')
        return
    for kind, sid, msg in problems:
        print(f'{kind:10} {sid}: {msg}')
    n = {k: sum(1 for s in missing if s['kind'] == k) for k in PRICE}
    standins = [s['id'] for s in sounds.values() if str(s.get('generatedFrom', '')).startswith('synth|')]
    takes = sum(len(s.get('takes') or []) + 1 for s in sounds.values() if s.get('takes'))
    print(f'{len(standins)} sounds are synthesised stand-ins (Tools/Audio/synth_core_sfx.py), {takes} takes in multi-take sounds')
    print(f"{len(sounds)} sounds, {len(missing)} without a clip ({n['Effect']} effects, {n['Voice']} voice lines): ${price:.2f}")
    sys.exit(1 if problems else 0)


if __name__ == '__main__':
    main()
