#!/usr/bin/env python3
"""Writes the core effects' takes and the ambience beds into the game's JSON (no Unity needed).

  python3 -I Tools/Audio/core_sfx.py            write audio.json (sounds, events) and Level1/Floor1000 (ambientSound, the Meadow's setAmbience). content/entities/meadowBed.json lost its loopSound by hand (the bed is the level's ambientSound; the entity stays as the marker the playtests look for)
  python3 -I Tools/Audio/core_sfx.py --check    exit 1 if the JSON is not what this table says, or a take/bed file is missing
  python3 -I Tools/Audio/core_sfx.py --gen-list print what to make through Hawkes Gen (audio.sfx) to replace the stand-ins

The files themselves come from Tools/Audio/synth_core_sfx.py (stand-ins; Hawkes Gen refused the agent that wrote this).
Edit the table here, never the JSON by hand. See Docs/SoundAudit.md "Takes, buses and beds".
"""
import json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
CONFIG = os.path.join(ROOT, 'Assets/GameConfig')
FOLDER = 'Audio/Generated/Core'
MARK = 'synth|Tools/Audio/synth_core_sfx.py'   # generatedFrom: made by the synthesiser, not by Hawkes Gen (yet)

# id: (hawkes gen prompt, seconds, influence, volume, maxVoices, cooldown)
EFFECTS = {
    'rewind_start': ('Cassette tape rewinding fast: a high-pitched reverse whirr climbing with a warbly tape wobble. Mechanical, no music.', 1.0, 0.6, 0.7, 1, 0.04),
    'rewind_stop': ('Cassette player stop button: a short plastic click with a soft mechanical clunk and a tape winding down. One-shot, dry. '
                    'Very short, ends immediately.', 0.5, 0.65, 0.6, 3, 0.04),
}
TAKES = 3

# Made by Hawkes Gen (audio.sfx-open, Stable Audio 3, 4 prompt variants per event), baked by Tools/Audio/bake_gen_takes.py:
# id: (prompt of the first take, seconds the take is cut to, influence, volume, maxVoices, cooldown), TAKE_COUNT = takes made.
# The prompt in audio.json is what was made; generatedFrom is the Sound Generator's own key, so the editor shows them Ready.
GEN = {'graze_tick': ('A bullet whipping past very close: a thin fast whoosh with a bright glassy zip and a tiny sparkle. One-shot, dry, no music.', 0.4, 0.6, 0.45, 3, 0.05), 'player_jump': ('A person jumping: a light springy push-off, a soft rising swish and a tiny boing. One-shot, dry, no music.', 0.45, 0.6, 0.45, 3, 0.04), 'player_dash': ('A quick dash burst: a snapping cloth whoosh with a rising air swipe and a soft low puff. One-shot, dry, no music.', 0.5, 0.6, 0.55, 3, 0.1), 'pickup_diamond': ('A single bright crystal ping, clean glassy and sparkly, short, one clear pitch. One-shot, no melody, no music.', 0.5, 0.6, 0.6, 4, 0.0), 'imp_hurt': ('A small soot imp hit: a sharp high squeal over a snappy thump, crackle of embers. One-shot, dry, no music.', 0.5, 0.6, 0.48, 4, 0.08), 'cinder_hurt': ('A living ember cinder hit: a crackling snap with a hiss and a shower of tiny sparks, a dull body thud. One-shot, dry, no music.', 0.5, 0.6, 0.44, 4, 0.08), 'player_shoot': ('Arcade rocket launcher shot: a short punchy low thump with a bright snapping crack on top, tight and dry, no tail. One-shot, no music.', 0.5, 0.6, 0.55, 4, 0.05), 'hit_pop': ('A bullet landing on an enemy: a bright sharp transient slap with a meaty low body thud right behind it, tight and punchy. One-shot, dry, no music.', 0.45, 0.6, 0.55, 4, 0.03), 'kill_pop': ('An enemy bursting: a crunchy pop transient, a fat low thump body and a quick shower of sparkly bits, Bullet Kin style. One-shot, dry, no music.', 0.8, 0.6, 0.8, 4, 0.04), 'player_hurt': ('Cartoon hero taking a hit: a sharp punchy slap transient and a low painful thump with a short grunt falling in pitch. One-shot, dry, no music.', 0.7, 0.6, 0.9, 4, 0.2), 'player_land': ('Boots landing on stone ground: a short solid thud with a puff of grit. One-shot, close-up, dry, no music.', 0.45, 0.6, 0.45, 3, 0.04), 'bullet_impact': ('A bullet hitting a stone wall: a sharp dry tick of impact with a short gritty puff of rock dust, very short. One-shot, no music.', 0.5, 0.6, 0.5, 4, 0.04), 'player_die': ('Cartoon hero knocked out: a big comedic bonk, a falling slide whistle and a sad little thud. One-shot, dry, no music.', 1.6, 0.6, 0.9, 4, 0.04), 'upgrade_pick': ('Buying a good in a shop: a coin clink, a satisfying cash-register ka-ching and a bright sparkle. One-shot, no music.', 0.6, 0.6, 0.8, 4, 0.04), 'imp_die': ('A small imp bursting into cinders: a squeaky shriek, a bassy pop and falling sparks. One-shot, dry, no music.', 0.9, 0.6, 0.6, 4, 0.1), 'cinder_die': ('A glowing cinder snuffing out: a soft whoomph with a bass thump, a sizzle and a puff of ash. One-shot, dry, no music.', 0.9, 0.6, 0.55, 4, 0.1), 'impSack_die': ('A sack imp falling over: a gruff yelp, a heavy thud and a spill of clinking gems. One-shot, dry, no music.', 0.9, 0.6, 0.6, 4, 0.1), 'cinderThatch_die': ('A clump of burning thatch collapsing: a crackly crunch with a low thump and a soft pop of sparks. One-shot, dry, no music.', 0.9, 0.6, 0.55, 4, 0.1)}
TAKE_COUNT = {'player_land': 3, 'upgrade_pick': 3}

# id: (prompt, seconds, volume)
BEDS = {
    'ambience_meadow_calm': ('A calm village meadow in the afternoon: soft wind through grass, a far-off rooster crowing now and then, a wooden mill '
                             'creaking slowly. Seamless loop, no music, no melody.', 20, 0.8),
    'ambience_meadow_burning': ('The same meadow burning: roaring wind, crackling thatch fires, a village alarm bell struck out of time, a frightened '
                                'far-off rooster. Seamless loop, no music, no melody.', 20, 0.8),
    'ambience_floor1000': ('A lava river bank deep in Hell: a low lava rumble, thick bubbling and popping, a faint hiss, now and then a long creak of '
                           'stone. Seamless loop, no music, no melody.', 20, 0.8),
}

EVENTS = {'enemyHit': 'hit_pop', 'killPop': 'kill_pop', 'bulletImpact': 'bullet_impact'}


def load(path):
    return json.load(open(path, encoding='utf-8'))


def save(path, data):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(json.dumps(data, indent=2, ensure_ascii=False) + '\n')


def sound_entry(old, **fields):
    entry = dict(old) if old else {}
    base = {'id': '', 'kind': 'Effect', 'prompt': '', 'durationSeconds': 0.5, 'promptInfluence': 0.6, 'loop': False, 'voice': '', 'voiceId': '',
            'clip': '', 'takes': [], 'bus': 'Sfx', 'generatedFrom': '', 'volume': 1, 'pitchVariation': 0.06, 'maxVoices': 3, 'cooldown': 0.04}
    out = {}
    for k in base:
        out[k] = entry.get(k, base[k])
    out.update(fields)
    # keep the file's key order: id, kind, prompt, ... clip, takes, bus, generatedFrom ...
    order = ['id', 'kind', 'prompt', 'durationSeconds', 'promptInfluence', 'loop', 'voice', 'voiceId', 'clip', 'takes', 'bus', 'generatedFrom',
             'volume', 'pitchVariation', 'maxVoices', 'cooldown']
    return {k: out[k] for k in order}


def build():
    audio = load(os.path.join(CONFIG, 'audio.json'))
    index = {s['id']: i for i, s in enumerate(audio['sounds'])}

    def put(entry):
        if entry['id'] in index:
            audio['sounds'][index[entry['id']]] = entry
        else:
            index[entry['id']] = len(audio['sounds'])
            audio['sounds'].append(entry)

    table = dict(EFFECTS)
    table.update(GEN)
    for sid, (prompt, seconds, influence, volume, voices, cooldown) in table.items():
        old = audio['sounds'][index[sid]] if sid in index else None
        n_takes = TAKE_COUNT.get(sid, 4) if sid in GEN else TAKES
        made = MARK
        if sid in GEN:
            made = f"effect|{('%.2f' % seconds).rstrip('0').rstrip('.')}|{('%.2f' % influence).rstrip('0').rstrip('.')}|False|{prompt}"
        put(sound_entry(old, id=sid, prompt=prompt, durationSeconds=seconds, promptInfluence=influence, loop=False,
                        clip=f'{FOLDER}/{sid}_t1.ogg', takes=[f'{FOLDER}/{sid}_t{n}.ogg' for n in range(2, n_takes + 1)],
                        bus='Sfx', generatedFrom=made, volume=volume, pitchVariation=0.06, maxVoices=voices, cooldown=cooldown))
    for sid, (prompt, seconds, volume) in BEDS.items():
        old = audio['sounds'][index[sid]] if sid in index else None
        put(sound_entry(old, id=sid, prompt=prompt, durationSeconds=seconds, promptInfluence=0.5, loop=True, clip=f'{FOLDER}/{sid}.ogg',
                        takes=[], bus='Ambience', generatedFrom=MARK, volume=volume, pitchVariation=0, maxVoices=1, cooldown=0.1))
    # Every other sound is untouched: the new fields (takes, bus) are missing there and keep their defaults (bus Auto: Voice for lines).
    for k, v in EVENTS.items():
        audio['events'][k] = v
    return audio


def level_edits():
    out = {}
    l1 = load(os.path.join(CONFIG, 'levels/Level1.json'))
    l1['ambientSound'] = f'{FOLDER}/ambience_meadow_calm.ogg'
    l1['ambientVolume'] = 0.8
    # the burning bed comes in with the raid's music (playMusic, 5.5 s in); the actions are written once
    raid = None
    stack = [l1['script']]
    while stack:
        o = stack.pop()
        if isinstance(o, dict):
            if o.get('type') == 'playMusic' and 'meadow_raid' in str(o.get('clip')):
                raid = o
            stack.extend(o.values())
        elif isinstance(o, list):
            for i, x in enumerate(o):
                stack.append(x)
    if raid is None:
        raise SystemExit('Level1: the meadow_raid playMusic action is gone; update Tools/Audio/core_sfx.py')
    # find its list, to insert the swap right after it
    def walk(o):
        if isinstance(o, list):
            for i, x in enumerate(o):
                if x is raid:
                    yield o, i
                else:
                    yield from walk(x)
        elif isinstance(o, dict):
            for v in o.values():
                yield from walk(v)
    for lst, i in walk(l1['script']):
        if not (i + 1 < len(lst) and isinstance(lst[i + 1], dict) and lst[i + 1].get('type') == 'setAmbience'):
            lst.insert(i + 1, {'type': 'setAmbience', 'sound': 'ambience_meadow_burning', 'fadeSeconds': 2.5})
        break
    out['levels/Level1.json'] = l1

    f1 = load(os.path.join(CONFIG, 'levels/Floor1000.json'))
    new = {}
    for k, v in f1.items():
        new[k] = v
        if k == 'musicVolume':
            new['ambientSound'] = f'{FOLDER}/ambience_floor1000.ogg'
            new['ambientVolume'] = 0.8
    out['levels/Floor1000.json'] = new

    return out


def check_files():
    problems = []
    for sid in list(EFFECTS) + list(GEN):
        for n in range(1, (TAKE_COUNT.get(sid, 4) if sid in GEN else TAKES) + 1):
            p = os.path.join(ROOT, 'Assets', FOLDER, f'{sid}_t{n}.ogg')
            if not os.path.exists(p) or not os.path.exists(p + '.meta'):
                problems.append(f'missing take {p}')
    for sid in BEDS:
        p = os.path.join(ROOT, 'Assets', FOLDER, f'{sid}.ogg')
        if not os.path.exists(p) or not os.path.exists(p + '.meta'):
            problems.append(f'missing bed {p}')
    return problems


def main():
    if '--gen-list' in sys.argv:
        n = len(EFFECTS) * TAKES
        print(f'{n} takes (audio.sfx, 3 per sound, new_take for 2 and 3) and {len(BEDS)} loops, about ${n * 0.024 + len(BEDS) * 0.05:.2f}:')
        for sid, e in EFFECTS.items():
            print(f'  {sid}: {e[1]} s: {e[0]}')
        for sid, b in BEDS.items():
            print(f'  {sid} (loop, {b[1]} s): {b[0]}')
        return
    audio = build()
    levels = level_edits()
    files = {os.path.join(CONFIG, 'audio.json'): audio}
    for rel, data in levels.items():
        files[os.path.join(CONFIG, rel)] = data
    if '--check' in sys.argv:
        problems = check_files()
        for path, data in files.items():
            want = json.dumps(data, indent=2, ensure_ascii=False) + '\n'
            if open(path, encoding='utf-8', newline='').read() != want:
                problems.append(f'{os.path.relpath(path, ROOT)} is not what Tools/Audio/core_sfx.py writes (run it)')
        for p in problems:
            print('CORE-SFX', p)
        sys.exit(1 if problems else 0)
    for path, data in files.items():
        save(path, data)
        print('wrote', os.path.relpath(path, ROOT))
    for p in check_files():
        print('WARNING', p)


if __name__ == '__main__':
    main()
