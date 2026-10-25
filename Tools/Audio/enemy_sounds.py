#!/usr/bin/env python3
"""Writes the per-enemy hurt / death / attack sounds and the per-weapon equip / empty sounds into audio.json (prompts
only: Hawkes Gen makes the clips, see sound_audit.py) and wires them into content/entities/*.json (sounds.hurtSound,
deathSound, attackSound) and content/weapons/*.json (sounds.equip, sounds.empty). Safe to run again: it only adds ids
audio.json lacks and rewrites the wiring. Edit the tables, run it, then run sound_audit.py."""
import json, os, re, sys
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
AUDIO = os.path.join(ROOT, 'Assets/GameConfig/audio.json')
ENT = os.path.join(ROOT, 'Assets/GameConfig/content/entities/')
WPN = os.path.join(ROOT, 'Assets/GameConfig/content/weapons/')
TAIL = ' One-shot, dry, no music. Very short, ends immediately.'

# id: (hurt, death, attack or None, volume)
ENEMIES = {
 'imp': ('A small imp yelping: a sharp high squeal with a crackle of embers.',
         'A small imp popping into cinders: a squeaky shriek, then a dry fiery pop and falling sparks.',
         'A small imp spitting a glowing ember: a wet hiss and a quick fizzing whoosh.', 0.6),
 'cinder': ('A living cinder hit: a crackling hiss and a shower of tiny sparks.',
            'A glowing cinder snuffing out: a soft whoomph, a sizzle and a puff of ash.', None, 0.55),
 'cinderThatch': ('Burning straw hit: a dry crackling snap with a hiss.',
                  'A clump of burning thatch collapsing: a crackly crunch, then a soft pop of sparks.', None, 0.55),
 'impSack': ('An imp grunting as a heavy sack bumps: a gruff squeak and a clink of glass gems.',
             'A sack imp falling over: a gruff yelp, a thud and a spill of clinking gems.',
             'An imp hurling a heavy sack: a grunt of effort and a creaking cloth whoosh.', 0.6),
 'impSwing': ('An imp on a chain swinging, hit: a surprised squeak with a rattle of links.',
              'A swinging imp dropped off its chain: a squeal, a metallic rattle and a faint pop.',
              'An imp lobbing an ember from a swing: a quick hiss, a creak of chain.', 0.6),
 'impCutter': ('An imp snarling as it is hit: a scratchy hiss with a tiny metal scrape.',
               'A claw-handed imp bursting: a rasping shriek and a clatter of bits of iron, then a pop.',
               'An imp snapping an ember from its claws: a sharp scratchy hiss and a crackle.', 0.6),
 'impGuard': ('A helmeted imp hit: a dull tinny clang with a grunt.',
              'A helmeted imp knocked out: a loud clang of a helmet rolling away and a small puff.',
              'Three embers spat at once: a triple fizz, quick and flat.', 0.6),
 'brimstoneDemon': ('A big demon grunting when hit: a low gravelly growl with a burst of fire.',
                    'A big demon collapsing: a rumbling roar, a thud and a long hiss of dying flames.',
                    'A demon breathing fire: a deep lungful, then a roaring whoosh of flame.', 0.75),
 'demonWhelp': ('A baby demon squealing when hit: a rough little shriek with a crackle.',
                'A baby demon bursting: a squeaky roar cut short, then a small wet pop.',
                'A baby demon spitting a spark: a snappy little spit and a fizz.', 0.6),
 'demonLobber': ('A hunched demon grunting at a hit: a thick wet cough with a growl.',
                 'A hunched demon dropping its lump of magma: a gurgling moan, a heavy splat and a hiss.',
                 'A demon lobbing a ball of magma high in the air: a thick wet spit and a rising whistle.', 0.7),
 'whelpNest': ('A nest of fire hit: a scatter of cracking stone, a flare of embers and tiny squeaks.',
               'A nest of hot stones breaking apart: a crunchy collapse with a last frightened squeak.', None, 0.7),
 'ricochetImp': ('An imp hit, with a springy ring of metal: a squeak and a twang.',
                 'A springy imp bursting: a squeaky yell, a metallic boing and a pop.',
                 'An imp flicking a bouncing ember: a sharp snap and a bright ping.', 0.6),
 'blinkImp': ('A violet imp flickering when hit: a squeak with a thin warbling whoosh.',
              'A violet imp vanishing in a flash: a squeal swallowed by a hollow pop and a fading shimmer.',
              'An imp mid-blink spitting an ember: a soft airy pop, then a sharp hiss.', 0.6),
 'bulwarkImp': ('An imp behind a metal shield hit: a heavy ringing clang and a muffled grunt.',
                'A shielded imp dropping its slab of slag: a grunt, a thundering clatter and a pop.',
                'An imp thrusting out an ember from behind a shield: a heavy grunt and a flat hiss.', 0.65),
 'leadImp': ('A cocky imp hit: a sharp sour yelp with a snort.',
             'A smug imp bursting: a high laugh turning into a shriek, then a dry pop.',
             'An imp taking careful aim and spitting an ember: a quick sniff, then a crisp hiss.', 0.6),
 'watcher': ('A giant floating eye hit: a wet slap, a high warble and a blink-flutter.',
             'A giant eye bursting: a wet pop, a long falling warble and a trickle of sparks.',
             'A huge eye opening and firing: a wet squelch, a rising hum and a sharp zap.', 0.65),
 'impForeman': ('A huge imp foreman roaring when hit: a burly angry bellow with a chain rattle.',
                'A huge imp foreman crashing down: a long defeated roar, a rattle of chains and a heavy collapse.',
                'A huge imp swinging a chain whip and a sack: a growl, a heavy whoosh and a crack.', 0.85),
 'pileKeeper': ('A mountain of treasure being struck: a loud clattering avalanche of coins and gems with a grunt.',
                'A giant pile collapsing: a thundering crash of coins, gems and armour, then a long trickle.',
                'A giant heaving a heap of coins and gems at once: a heavy grunt and a jangling roar.', 0.85),
 'roadBreaker': ('A rocky brute hit: a deep stony grunt with cracking rock.',
                 'A giant rocky brute smashed apart: a rumbling groan and a long rolling crash of boulders.',
                 'A giant slamming the road with a stone hammer: a heavy swing and a cracking boom.', 0.85),
 'carver': ('A tall blade-armed demon hit: a sharp metal scrape and a hiss of breath.',
            'A tall blade-armed demon falling apart: a ringing shatter of blades and a hollow thud.',
            'A demon slashing with huge blades: a rapid whistling whoosh and a ringing scrape.', 0.85),
 'magmaKing': ('A giant slag king struck: a deep gurgling grunt with sizzling rock.',
               'A giant king of molten slag collapsing: a deep gurgling groan, a crack of stone and a wave of hissing lava.',
               'A giant king flinging molten slag: a thick wet heave and a roaring splash of fire.', 0.9),
 'spireFiend': ('A gaunt basalt demon hit: a hollow stony grunt and a crack running through rock.',
                'A gaunt basalt demon shattering: a slow groan, spires cracking off and a long avalanche of stone.',
                'A demon pushing up a spike of rock: a low growl and a sharp cracking thrust of stone.', 0.9),
 'haloImp': ('A gold imp yelping as a ring of embers rattles: a squeak and a bright chime.',
             'A gold imp bursting: a shriek, a ring of crackling pops fading outward.',
             'An imp breathing out a ring of embers all at once: a deep puff and a bright rushing hiss.', 0.6),
 'whirlImp': ('A pink imp dizzy from a hit: a wobbly squeak and a warbling whirr.',
              'A spinning imp flying apart: a rising whirr, a shriek and a spray of fizzing pops.',
              'An imp spinning and spitting embers: a whirring spin and a quick stream of spits.', 0.6),
 'seamImp': ('A teal imp glitching when hit: a squeak with a stuttering digital-sounding crackle.',
             'A teal imp tearing out of the world: a squeal cut off by a hollow tearing pop.',
             'An imp snapping two quick embers from behind you: two sharp pops and a hiss.', 0.6),
 'slagTortoise': ('A slag shell struck: a heavy dull clang with a grunt.',
                  'A slag tortoise cracking apart: a groan, a heavy crack of stone and a hiss of cooling lava.',
                  'A tortoise spitting a slow glob of ember: a wet cough and a slow fizz.', 0.7),
 'geodeBit': ('A tiny shard of slag hit: a sharp little crack.',
              'A tiny slag shard popping: a small bright pop with a tick of sparks.', None, 0.5),
 'magmaGeode': ('A lump of hot rock cracking when hit: a dry crunch and a hiss.',
                'A geode splitting in two: a loud crack, a rush of hot air and two small glassy clinks.', None, 0.6),
 'emberMine': ('A smouldering mine nudged: a faint scrape and a hiss.',
               'A buried ember mine going off: a sharp rising fizz, then a hard flat boom and a spray of embers.', None, 0.65),
 'mineBeetle': ('A big beetle hit: a hard shell crack and a hissing chitter.',
                'A big beetle bursting: a crunching pop, a chitter cut short and a spray of embers.', None, 0.6),
 'lensImp': ('An imp hit while holding a glass shard: a squeak with a ringing glassy tink.',
             'An imp bursting with a glass shard breaking: a shriek, a glassy shatter and a pop.', None, 0.6),
 'pyreCaller': ('An old imp coughing as it is hit: a rasping cough with a crackle of sparks.',
                'An old imp falling into ash: a wheezing groan, a soft whoomph and a drift of cinders.', None, 0.6),
 'slagHound': ('A slag hound yelping when struck: a gravelly yelp with a crack of rock.',
               'A slag hound shattering: a snarl, then a crash of breaking rock and a hiss of cooling lava.', None, 0.65),
 'deadeyeImp': ('A pale imp flinching: a thin squeal and a ting of broken glass.',
                'A sniper imp popping: a thin squeal, a pop and a faint trickle of falling glass.',
                'An imp firing a thin white-hot bolt: a sharp inhale, a crack and a long whining hiss.', 0.6),
 'plateDemon': ('A demon behind a heavy iron plate hit: a deep ringing clang and a grunt.',
                'A plated demon dropping its plate: a groan, a huge clanging crash and a hiss of dying flames.',
                'A demon spitting two quick brimstone shards from behind a plate: two sharp spits and a flat hiss.', 0.7),
 'riftBomb': ('A smouldering bomb nudged: a small hollow knock and a fizz.',
              'A rift bomb going off: a rising squeal, a hard flat boom and a spray of violet sparks.', None, 0.65),
 'riftBomber': ('A pink imp hit mid-blink: a squeak with a thin warbling pop.',
                'A pink imp vanishing in a burst: a shriek, a hollow tearing pop and a fading shimmer.',
                'An imp setting down a bomb: a soft tick, a small thump and a quickening fizz.', 0.6),
 'bounceLobber': ('A hunched demon grunting as it is hit: a wet cough with a rubbery boing.',
                  'A hunched demon collapsing: a gurgling moan, a heavy splat and a last small boing.',
                  'A demon throwing a ball of magma along the ground: a thick spit and a bouncy rolling thud.', 0.7),
 'chainWhelp': ('A small demon yelping against a chain: a rough squeal and a clink of links.',
                'A small demon bursting free of its chain: a squeaky roar, a snapping chain and a wet pop.',
                None, 0.6),
 'chainBrute': ('A big plated demon hit while chained: a heavy clang, a grunt and a rattle of links.',
                'A big chained demon falling: a groaning roar, a heavy thud and a rattle of loose chain.',
                'A chained demon spitting an ember: a thick spit with a clank of chain.', 0.7),
 'risenDemon': ('A dark demon grunting when hit: a low gravelly growl with a crackle.',
                'A dark demon collapsing for good: a rumbling roar, a thud and a long hiss of dying flames.',
                'A demon breathing fire: a deep lungful, then a roaring whoosh of flame.', 0.75),
 'loopWraith': ('A pale ghost struck: a thin echoing wail and a ripple of chiming static.',
                'A pale ghost unravelling: a rising echoing wail, a glassy chime and a soft hollow pop.',
                'A ghost throwing a cold ember: a breathy whisper and a bright icy hiss.', 0.6),
 'veilDemon': ('A veiled demon behind a shield hit: a dull clang and a muffled growl.',
               'A veiled demon tearing apart: a groan, a rush of air and a hollow tearing pop.',
               'A demon firing an ember from behind a shield: a flat heavy hiss.', 0.7),
 'slabSniper': ('An imp behind a stone slab hit: a hard stony clack and a squeak.',
                'A stone-plated imp breaking: a squeal, a loud crack of stone and a pop.',
                'An imp firing a single fast bolt from behind a slab: a sharp crack and a whining hiss.', 0.6),
 'fuseWhelp': ('A baby demon with a lit fuse hit: a rough squeal and a hissing sputter.',
               'A baby demon with a fuse going off: a squealing hiss, then a sharp bang and a spray of sparks.',
               None, 0.65),
 'bastionBrute': ('A huge armoured demon hit: a deep ringing clang and a heavy grunt.',
                  'A huge armoured demon finally falling: a thundering roar, a crashing collapse of metal and stone.',
                  'A huge demon breathing fire from behind a wall of armour: a deep lungful and a roaring whoosh.', 0.8),
}
WEAPONS = {  # id: (equip, empty)
 'rocketLauncher': ('A rocket launcher shouldered: a heavy metallic clunk with a short mechanical slide.',
                    'A rocket launcher with no rocket: a dry hollow click and a faint tube rattle.'),
 'fists': ('Knuckles cracking: two quick dry pops of a fist being clenched.', None),
 'spitfire': ('A small lizard being picked up and held: a tiny scratchy chirp and a warm fizz.',
              'A small lizard that has run out of fire: a dry little cough and a weak puff.'),
 'ricochetPistol': ('A pistol drawn from a holster: a quick leather slide and a metallic click.',
                    'A pistol with an empty chamber: a short dry metallic click.'),
 'chainWhip': ('A chain whip uncoiled: a rattle of links settling and one slow clink.',
               'A chain whip with no more reach: a limp jangle of loose links.'),
 'shotgun': ('A shotgun pumped: a deep chunky pump-action rack, a shell sliding home.',
             'A shotgun with an empty magazine: a dry heavy click and a hollow clunk.'),
 'boomerangHalo': ('A glowing ring picked up: a soft ethereal chime with a gentle hum.',
                   'A glowing ring that has faded: a hollow breathy fade and a tiny dull chime.'),
}

def sfx(id, prompt, dur, vol, voices, cd, infl=0.6):
    return {"id": id, "kind": "Effect", "prompt": prompt, "durationSeconds": dur, "promptInfluence": infl, "loop": False,
            "voice": "", "voiceId": "", "clip": None, "generatedFrom": "", "volume": vol, "pitchVariation": 0.08,
            "maxVoices": voices, "cooldown": cd}

def wire(path, fields):
    """Adds fields to the file's "sounds" object by text insertion, so the rest of the file keeps its formatting."""
    raw = open(path, newline='').read()
    nl = '\r\n' if '\r\n' in raw else '\n'
    d = json.loads(raw)
    have = d.get('sounds', {})
    todo = [(k, v) for k, v in fields.items() if have.get(k) != v]
    for k, v in list(todo):
        if k in have:  # already there with another value: replace in place
            raw = re.sub(r'("sounds"\s*:\s*\{[^}]*?"%s"\s*:\s*)"[^"]*"' % k, lambda m: m.group(1) + '"%s"' % v, raw, count=1, flags=re.S)
            todo.remove((k, v))
    if not todo:
        json.loads(raw)
        open(path, 'w', newline='').write(raw)
        return
    if 'sounds' in d:
        # the first object-valued "sounds" key at the top level of the file (two-space indent) or inline
        m = re.search(r'^(\s*)"sounds"\s*:\s*\{', raw, re.M)
        ind = m.group(1)
        tail = raw[m.end():]
        if re.match(r'\s*\}', tail) or '\n' not in tail.split('}')[0]:  # empty or inline
            inline = ', '.join(f'"{k}": "{v}"' for k, v in todo)
            existing = bool(have)
            raw = raw[:m.end()] + ' ' + inline + (',' if existing else '') + raw[m.end():]
        else:
            lines = ''.join(f'{nl}{ind}  "{k}": "{v}",' for k, v in todo)
            raw = raw[:m.end()] + lines + raw[m.end():]
    else:
        end = raw.rstrip().rfind('}')
        before = raw[:end].rstrip()
        body = ', '.join(f'"{k}": "{v}"' for k, v in fields.items())
        raw = before + ',' + nl + '  "sounds": { ' + body + ' }' + nl + '}' + nl
    json.loads(raw)
    open(path, 'w', newline='').write(raw)

def main():
    raw = open(AUDIO, newline='').read()
    nl = '\r\n' if '\r\n' in raw else '\n'
    a = json.loads(raw)
    have = {s['id'] for s in a['sounds']}
    add = []
    def put(s):
        if s['id'] not in have:
            have.add(s['id']); add.append(s)
    for e, (hurt, death, attack, vol) in ENEMIES.items():
        boss = vol >= 0.85
        put(sfx(f'{e}_hurt', hurt + TAIL, 0.6 if boss else 0.5, vol * 0.8, 4, 0.08))
        put(sfx(f'{e}_die', death + TAIL, 1.5 if boss else 0.9, vol, 4, 0.1))
        if attack:
            put(sfx(f'{e}_attack', attack + TAIL, 0.9 if boss else 0.6, vol * 0.7, 3, 0.1))
    for w, (equip, empty) in WEAPONS.items():
        put(sfx(f'wpn_equip_{w}', equip + TAIL, 0.6, 0.55, 2, 0.1))
        if empty:
            put(sfx(f'wpn_empty_{w}', empty + TAIL, 0.5, 0.5, 2, 0.3))
    a['sounds'] += add
    open(AUDIO, 'w', newline='').write(json.dumps(a, indent=2, ensure_ascii=False).replace('\n', nl) + nl)
    print('added', len(add), 'sounds')
    for e, (h, d, at, v) in ENEMIES.items():
        f = {'hurtSound': f'{e}_hurt', 'deathSound': f'{e}_die'}
        if at:
            f['attackSound'] = f'{e}_attack'
        wire(ENT + e + '.json', f)
    for w, (eq, em) in WEAPONS.items():
        f = {'equip': f'wpn_equip_{w}'}
        if em:
            f['empty'] = f'wpn_empty_{w}'
        wire(WPN + w + '.json', f)

if __name__ == '__main__':
    main()
