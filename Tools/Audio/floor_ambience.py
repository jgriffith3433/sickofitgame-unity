#!/usr/bin/env python3
"""Place beds and positional emitters for the Meadow, the Shaft and Floors 1000-997.

  python3 -I Tools/Audio/floor_ambience.py          write audio.json, shaft.json, Level1/Floor1000-997
  python3 -I Tools/Audio/floor_ambience.py --check  exit 1 if the JSON is not what this table says

Edit the tables here, never the JSON by hand. Clips come from Tools/Audio/synth_core_sfx.py (stand-ins)
or Hawkes Gen audio.sfx (org_eb0d5c82e8da8c2d.sickofit). See docs/world-bible.md.
"""
import json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
CONFIG = os.path.join(ROOT, 'Assets/GameConfig')
FOLDER = 'Audio/Generated/Core'
MARK = 'synth|Tools/Audio/synth_core_sfx.py'

# id: (prompt, seconds, volume) — seamless Ambience-bus loops
BEDS = {
    'ambience_meadow_calm': (
        'A calm village meadow in the afternoon: soft wind through grass, a far-off rooster crowing now and then, '
        'a wooden mill creaking slowly, quiet villagers. Seamless loop, no music, no melody.',
        30, 0.8),
    'ambience_meadow_burning': (
        'The same meadow burning: roaring wind, crackling thatch fires, a village alarm bell struck out of time, '
        'a frightened far-off rooster. Seamless loop, no music, no melody.',
        30, 0.8),
    'ambience_shaft': (
        'Inside a vast vertical shaft of Hell: rushing wind in a stone pipe, distant chains rattling, a band of '
        'demons echoing far above. Seamless loop, no music, no melody.',
        30, 0.75),
    'ambience_floor1000': (
        'A lava river bank at Hell\'s Landing: bubbling lava, far screams, a lonely ticket-desk bell, long creaks of '
        'stone. Seamless loop, no music, no melody.',
        30, 0.8),
    'ambience_floor999': (
        'A furnace ledge in Hell: a furnace roar underfoot, heavy hammering on metal, hot air rushing. Seamless loop, '
        'no music, no melody.',
        30, 0.8),
    'ambience_floor998': (
        'A wrecked village dragged through Hell: slow dripping water, bone wind chimes clacking, a distant choir hum. '
        'Seamless loop, no music, no melody.',
        30, 0.75),
    'ambience_floor997': (
        'An obsidian ledge under the Overlord\'s gaze: a deep organ drone, his distant velvet laugh, chains far off. '
        'Seamless loop, no music, no melody.',
        30, 0.78),
}

# id: (prompt, seconds, volume) — positional Ambience one-shots
ONESHOTS = {
    'amb_lava_pop': ('A single wet lava bubble popping close, short and low, no music.', 1.0, 0.7),
    'amb_chain_rattle': ('Heavy iron hauling chains rattling once, short, dry, no music.', 1.2, 0.65),
    'amb_torch_crackle': ('A torch flame crackling with a few sparks, short, dry, no music.', 1.0, 0.55),
    'amb_imp_chatter': ('Two small soot imps chattering a short gibber, far, dry, no music.', 1.2, 0.5),
    'amb_bone_clatter': ('Dry bones clattering once on stone, short, dry, no music.', 1.0, 0.6),
    'amb_steam_hiss': ('A short steam vent hiss, hot air, dry, no music.', 1.1, 0.55),
    'amb_ticket_bell': ('A small brass ticket-desk service bell struck once, clear, dry, no music.', 1.4, 0.6),
    'amb_hammer_strike': ('A heavy hammer striking an anvil once, metallic ring, dry, no music.', 1.0, 0.7),
    'amb_drip': ('A single water drip in a stone cavern, short, dry, no music.', 0.8, 0.5),
    'amb_bone_chime': ('A short bone wind-chime clack of three thin tones, dry, no music.', 1.3, 0.5),
    'amb_rooster_crow': ('A far-off rooster crowing once over a meadow, dry, no music.', 1.4, 0.55),
    'amb_mill_creak': ('A wooden mill sail creaking once, slow wood squeal, dry, no music.', 1.1, 0.55),
}

# level file stem -> (bed id, emitters list)
# emitter: {sound, prop, at, minInterval, maxInterval, range, volume}
LEVELS = {
    'Level1': ('ambience_meadow_calm', [
        {'sound': 'amb_rooster_crow', 'prop': 'hen-run', 'at': [3.5, -0.5], 'minInterval': 5, 'maxInterval': 11, 'range': 12, 'volume': 0.55},
        {'sound': 'amb_mill_creak', 'prop': 'village-mill', 'at': [-2.5, 0.8], 'minInterval': 4, 'maxInterval': 10, 'range': 11, 'volume': 0.6},
        {'sound': 'amb_torch_crackle', 'prop': 'thatch-eave', 'at': [1.2, 0.2], 'minInterval': 6, 'maxInterval': 12, 'range': 9, 'volume': 0.45},
    ]),
    'Floor1000': ('ambience_floor1000', [
        {'sound': 'amb_lava_pop', 'prop': 'lava-sea', 'at': [0.0, -5.0], 'minInterval': 4, 'maxInterval': 9, 'range': 14, 'volume': 0.7},
        {'sound': 'amb_steam_hiss', 'prop': 'bank-vent', 'at': [2.5, -1.5], 'minInterval': 5, 'maxInterval': 11, 'range': 10, 'volume': 0.55},
        {'sound': 'amb_ticket_bell', 'prop': 'landing-desk', 'at': [-3.2, -2.0], 'minInterval': 7, 'maxInterval': 12, 'range': 11, 'volume': 0.6},
        {'sound': 'amb_bone_clatter', 'prop': 'sinking-stone', 'at': [1.6, -0.3], 'minInterval': 6, 'maxInterval': 12, 'range': 9, 'volume': 0.45},
    ]),
    'Floor999': ('ambience_floor999', [
        {'sound': 'amb_lava_pop', 'prop': 'lava-sea', 'at': [0.0, -5.2], 'minInterval': 4, 'maxInterval': 9, 'range': 14, 'volume': 0.65},
        {'sound': 'amb_hammer_strike', 'prop': 'counted-wall', 'at': [-2.8, 0.5], 'minInterval': 5, 'maxInterval': 10, 'range': 11, 'volume': 0.7},
        {'sound': 'amb_steam_hiss', 'prop': 'furnace-vent', 'at': [1.5, 1.0], 'minInterval': 5, 'maxInterval': 11, 'range': 10, 'volume': 0.55},
        {'sound': 'amb_torch_crackle', 'prop': 'tally-glow', 'at': [-1.2, -1.0], 'minInterval': 6, 'maxInterval': 12, 'range': 9, 'volume': 0.5},
    ]),
    'Floor998': ('ambience_floor998', [
        {'sound': 'amb_drip', 'prop': 'wreck-roof', 'at': [8.7, -0.8], 'minInterval': 4, 'maxInterval': 9, 'range': 12, 'volume': 0.55},
        {'sound': 'amb_bone_chime', 'prop': 'hay-cart', 'at': [5.7, -1.0], 'minInterval': 6, 'maxInterval': 12, 'range': 10, 'volume': 0.5},
        {'sound': 'amb_bone_clatter', 'prop': 'wreck-cart', 'at': [2.4, -1.0], 'minInterval': 5, 'maxInterval': 11, 'range': 10, 'volume': 0.55},
        {'sound': 'amb_torch_crackle', 'prop': 'burning-thatch', 'at': [13.5, 0.5], 'minInterval': 5, 'maxInterval': 10, 'range': 11, 'volume': 0.5},
    ]),
    'Floor997': ('ambience_floor997', [
        {'sound': 'amb_chain_rattle', 'prop': 'hauling-chain', 'at': [9.0, 5.4], 'minInterval': 4, 'maxInterval': 9, 'range': 12, 'volume': 0.65},
        {'sound': 'amb_imp_chatter', 'prop': 'sack-imps', 'at': [11.4, 6.6], 'minInterval': 5, 'maxInterval': 11, 'range': 11, 'volume': 0.55},
        {'sound': 'amb_steam_hiss', 'prop': 'obsidian-vent', 'at': [1.8, 0.3], 'minInterval': 6, 'maxInterval': 12, 'range': 10, 'volume': 0.5},
        {'sound': 'amb_lava_pop', 'prop': 'lava-sea', 'at': [0.0, -5.2], 'minInterval': 5, 'maxInterval': 10, 'range': 14, 'volume': 0.55},
    ]),
}


def load(path):
    return json.load(open(path, encoding='utf-8'))


def save(path, data):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(json.dumps(data, indent=2, ensure_ascii=False) + '\n')


def sound_entry(old, **fields):
    entry = dict(old) if old else {}
    base = {'id': '', 'kind': 'Effect', 'prompt': '', 'durationSeconds': 0.5, 'promptInfluence': 0.55, 'loop': False,
            'voice': '', 'voiceId': '', 'clip': '', 'takes': [], 'bus': 'Ambience', 'generatedFrom': '', 'volume': 1,
            'pitchVariation': 0.04, 'maxVoices': 3, 'cooldown': 0.05}
    out = {}
    for k in base:
        out[k] = entry.get(k, base[k])
    out.update(fields)
    order = ['id', 'kind', 'prompt', 'durationSeconds', 'promptInfluence', 'loop', 'voice', 'voiceId', 'clip', 'takes',
             'bus', 'generatedFrom', 'volume', 'pitchVariation', 'maxVoices', 'cooldown']
    return {k: out[k] for k in order}


def build_audio(audio):
    index = {s['id']: i for i, s in enumerate(audio['sounds'])}

    def put(entry):
        if entry['id'] in index:
            audio['sounds'][index[entry['id']]] = entry
        else:
            index[entry['id']] = len(audio['sounds'])
            audio['sounds'].append(entry)

    for sid, (prompt, seconds, volume) in BEDS.items():
        old = audio['sounds'][index[sid]] if sid in index else None
        made = (old or {}).get('generatedFrom') or MARK
        # Keep a Gen mark if a prior pass already replaced the stand-in.
        if not str(made).startswith('synth|') and 'gen' not in str(made).lower() and '|' in str(made):
            pass
        else:
            made = MARK
        put(sound_entry(old, id=sid, prompt=prompt, durationSeconds=seconds, promptInfluence=0.55, loop=True,
                        clip=f'{FOLDER}/{sid}.ogg', takes=[], bus='Ambience', generatedFrom=made, volume=volume,
                        pitchVariation=0, maxVoices=1, cooldown=0.1))
    for sid, (prompt, seconds, volume) in ONESHOTS.items():
        old = audio['sounds'][index[sid]] if sid in index else None
        put(sound_entry(old, id=sid, prompt=prompt, durationSeconds=seconds, promptInfluence=0.55, loop=False,
                        clip=f'{FOLDER}/{sid}.ogg', takes=[], bus='Ambience', generatedFrom=MARK, volume=volume,
                        pitchVariation=0.04, maxVoices=3, cooldown=0.2))
    return audio


def patch_level(name, bed, emitters):
    path = os.path.join(CONFIG, 'levels', name + '.json')
    data = load(path)
    # Keep field order: insert ambient after musicVolume when missing.
    if 'ambientSound' in data:
        data['ambientSound'] = f'{FOLDER}/{bed}.ogg'
        data['ambientVolume'] = BEDS[bed][2]
        data['ambientEmitters'] = emitters
    else:
        new = {}
        for k, v in data.items():
            new[k] = v
            if k == 'musicVolume':
                new['ambientSound'] = f'{FOLDER}/{bed}.ogg'
                new['ambientVolume'] = BEDS[bed][2]
                new['ambientEmitters'] = emitters
        if 'ambientSound' not in new:
            new['ambientSound'] = f'{FOLDER}/{bed}.ogg'
            new['ambientVolume'] = BEDS[bed][2]
            new['ambientEmitters'] = emitters
        data = new
    return data


def patch_shaft(data):
    data['ambientSound'] = 'ambience_shaft'
    data['ambientVolume'] = BEDS['ambience_shaft'][2]
    return data


def check(audio, levels, shaft):
    problems = []
    beds = [s for s in audio['sounds'] if s.get('bus') == 'Ambience' and s.get('loop')]
    want_beds = set(BEDS)
    got_beds = {s['id'] for s in beds}
    if got_beds < want_beds:
        problems.append(f'missing beds: {sorted(want_beds - got_beds)}')
    for sid in ONESHOTS:
        if not any(s['id'] == sid for s in audio['sounds']):
            problems.append(f'missing oneshot {sid}')
    for name, (bed, emitters) in LEVELS.items():
        level = levels[name]
        if level.get('ambientSound') != f'{FOLDER}/{bed}.ogg':
            problems.append(f'{name} ambientSound want {bed}')
        if not level.get('ambientEmitters'):
            problems.append(f'{name} has no ambientEmitters')
        elif len(level['ambientEmitters']) < 3:
            problems.append(f'{name} needs ≥3 emitters, has {len(level["ambientEmitters"])}')
    if shaft.get('ambientSound') != 'ambience_shaft':
        problems.append('shaft.json ambientSound')
    for sid in list(BEDS) + list(ONESHOTS):
        p = os.path.join(ROOT, 'Assets', FOLDER, sid + '.ogg')
        if not os.path.exists(p) or not os.path.exists(p + '.meta'):
            problems.append(f'missing clip {p}')
    return problems


def main():
    audio = build_audio(load(os.path.join(CONFIG, 'audio.json')))
    levels = {name: patch_level(name, bed, emitters) for name, (bed, emitters) in LEVELS.items()}
    shaft = patch_shaft(load(os.path.join(CONFIG, 'ui/shaft.json')))
    if '--check' in sys.argv:
        problems = check(audio, levels, shaft)
        for p in problems:
            print('AMBIENCE', p)
        sys.exit(1 if problems else 0)
    if '--gen-list' in sys.argv:
        print(f'{len(BEDS)} beds (audio.sfx loop 30s) and {len(ONESHOTS)} oneshots:')
        for sid, (prompt, seconds, _) in BEDS.items():
            print(f'  {sid} (loop, {seconds}s): {prompt[:80]}...')
        for sid, (prompt, seconds, _) in ONESHOTS.items():
            print(f'  {sid} ({seconds}s): {prompt[:80]}')
        return
    save(os.path.join(CONFIG, 'audio.json'), audio)
    for name, data in levels.items():
        save(os.path.join(CONFIG, 'levels', name + '.json'), data)
    save(os.path.join(CONFIG, 'ui/shaft.json'), shaft)
    print(f'wrote {len(BEDS)} beds, {len(ONESHOTS)} oneshots, {len(LEVELS)} levels, shaft')


if __name__ == '__main__':
    main()
