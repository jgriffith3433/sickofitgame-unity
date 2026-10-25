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
Prices are Hawkes Gen's median per take (hawkesgen_estimate, 2026-10-25): audio.sfx $0.024, audio.voice $0.0168.
"""
import glob, json, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
CONFIG = os.path.join(ROOT, 'Assets/GameConfig')
PRICE = {'Effect': 0.024, 'Voice': 0.0168}
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
            problems.append(('no-clip', sid, 'no clip' if not clip else f'clip file missing: {clip}'))
        if not s.get('prompt') and not clip:
            problems.append(('no-prompt', sid, 'neither a prompt nor a clip'))
        if s['kind'] == 'Voice' and s.get('voice') and s['voice'] not in voices:
            problems.append(('bad-voice', sid, f'unknown voice {s["voice"]}'))
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
    print(f"{len(sounds)} sounds, {len(missing)} without a clip ({n['Effect']} effects, {n['Voice']} voice lines): ${price:.2f}")
    sys.exit(1 if problems else 0)


if __name__ == '__main__':
    main()
