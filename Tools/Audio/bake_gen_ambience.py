#!/usr/bin/env python3
"""Bakes Hawkes Gen audio.sfx downloads (mp3) of the place ambience into Assets/Audio/Generated/Core/<id>.ogg.

  python3 -I Tools/Audio/bake_gen_ambience.py <folder with <id>.mp3 downloads> [id ...]   (no ids: every id in floor_ambience.py found in the folder)

Beds (loop): Gen's loop take is kept when its seam is clean, else a 2 s equal-power crossfade of the tail into the head makes it seamless, RMS to -24 dB, mono ogg q4.
One-shots: leading silence cut, capped at 1.6 s with a short fade, RMS to -18 dB under a limiter. A silent download is skipped.
The loop check (what audio.analyze loop does): the step between the last and first sample against the clip's own typical
step, printed as `loop seam x`; over 6x the typical step fails. An existing .meta in git keeps its GUID (the importer settings stay).
Pure stdlib + ffmpeg.
"""
import array, math, os, re, subprocess, sys, tempfile, wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import floor_ambience as fa  # noqa: E402

OUT = os.path.join(ROOT, 'Assets', fa.FOLDER)
RATE = 44100
BED_RMS = -24.0
SHOT_RMS = -18.0
XFADE = 2.0


def decode(path, flt='anull'):
    tmp = tempfile.mktemp(suffix='.wav')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', path, '-af', flt, '-ac', '1', '-ar', str(RATE), '-c:a', 'pcm_s16le', tmp], check=True)
    with wave.open(tmp) as w:
        a = array.array('h', w.readframes(w.getnframes()))
    os.remove(tmp)
    return [x / 32768.0 for x in a]


def rms_db(x):
    if not x:
        return -91.0
    return 10 * math.log10(max(1e-12, sum(v * v for v in x) / len(x)))


def encode(x, path):
    tmp = tempfile.mktemp(suffix='.wav')
    with wave.open(tmp, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(array.array('h', [max(-32768, min(32767, int(v * 32767))) for v in x]).tobytes())
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', tmp, '-c:a', 'libvorbis', '-q:a', '4', path], check=True)
    os.remove(tmp)


def make_loop(x):
    c = int(XFADE * RATE)
    if len(x) < 4 * c:
        raise SystemExit('clip too short to loop')
    head, tail = x[:c], x[-c:]
    mid = []
    for i in range(c):
        t = i / (c - 1)
        mid.append(tail[i] * math.cos(t * math.pi / 2) + head[i] * math.sin(t * math.pi / 2))
    return x[c:-c] + mid


def seam_ratio(x):
    """Step across the loop point divided by the clip's RMS step: ~1 is seamless, a click is far over."""
    steps = [abs(x[i + 1] - x[i]) for i in range(0, len(x) - 1, 7)]
    typ = math.sqrt(sum(s * s for s in steps) / len(steps)) or 1e-9
    return abs(x[0] - x[-1]) / typ


def limit(x, ceiling=0.89):
    return [max(-ceiling, min(ceiling, v)) for v in x]


def meta_for(rel):
    r = subprocess.run(['git', '-C', ROOT, 'show', f'HEAD:{rel}.meta'], capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f'no .meta in git for {rel}: copy a sibling and give it a new guid')
    return r.stdout


def main():
    src = sys.argv[1]
    ids = sys.argv[2:] or [s for s in list(fa.BEDS) + list(fa.ONESHOTS) if os.path.exists(os.path.join(src, s + '.mp3'))]
    os.makedirs(OUT, exist_ok=True)
    bad = 0
    marked = []
    for sid in ids:
        f = os.path.join(src, sid + '.mp3')
        bed = sid in fa.BEDS
        x = decode(f, 'anull' if bed else 'silenceremove=start_periods=1:start_threshold=-50dB')
        if rms_db(x) < -60:
            print(f'skip {sid}: silent'); bad += 1; continue
        if bed:
            if seam_ratio(x) > 3:   # Gen's own loop crossfade (checked there) is kept when the seam is already clean
                x = make_loop(x)
            ratio = seam_ratio(x)
            tag = f'loop seam x{ratio:.2f}' + (' FAIL' if ratio > 6 else '')
            bad += ratio > 6
            target = BED_RMS
        else:
            n = int(1.6 * RATE)
            x = x[:n]
            fade = int(0.12 * RATE)
            for i in range(min(fade, len(x))):
                x[len(x) - 1 - i] *= i / fade
            tag = f'{len(x) / RATE:.2f} s'
            target = SHOT_RMS
        gain = 10 ** ((target - rms_db(x)) / 20)
        x = limit([v * gain for v in x])
        rel = f'Assets/{fa.FOLDER}/{sid}.ogg'
        encode(x, os.path.join(ROOT, rel))
        with open(os.path.join(ROOT, rel + '.meta'), 'w', newline='\n') as m:
            m.write(meta_for(rel))
        marked.append(sid)
        print(f'{sid}: {len(x) / RATE:.1f} s, {rms_db(x):.1f} dB rms, {tag}')
    ap = os.path.join(fa.CONFIG, 'audio.json')
    text = open(ap, encoding='utf-8').read()
    for sid in marked:   # a text edit of just this field: audio.json has hand-formatted blocks that a re-dump would reflow
        text, n = re.subn(r'("id": "%s",(?:(?!"id":).)*?"generatedFrom": )"[^"]*"' % re.escape(sid), r'\1"sfx|%s"' % sid, text, count=1, flags=re.S)
        if n != 1:
            print(f'could not mark {sid} in audio.json')
    with open(ap, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
