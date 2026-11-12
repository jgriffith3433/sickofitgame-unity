#!/usr/bin/env python3
"""Bakes Hawkes Gen audio.sfx takes (mp3 in a folder) into Assets/Audio/Generated/Core/<id>_t<n>.ogg + hand-written .meta.

  python3 -I Tools/Audio/bake_gen_takes.py <folder with <id>_<n>.mp3 downloads> id[:cap_seconds] ...

Each take: leading silence cut, capped at cap_seconds (default 0.8) with a short fade, mean volume -17 dB (RMS) under a limiter, mono ogg (q4). A take with no
sound (Gen returned a near-silent file) is skipped. An existing .meta in git keeps its GUID; a new one is copied from graze_tick_t1 with a new GUID.
Mono, preload, Decompress On Load: the same importer settings as every Core take.
"""
import os, re, subprocess, sys, uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(ROOT, 'Assets/Audio/Generated/Core')
TARGET_RMS = -17.0   # dB mean volume of every baked take (the limiter holds peaks under -1 dBFS)
TEMPLATE = 'Assets/Audio/Generated/Core/graze_tick_t1.ogg.meta'


def meta_for(rel):
    git = subprocess.run(['git', '-C', ROOT, 'show', f'HEAD:{rel}.meta'], capture_output=True, text=True)
    if git.returncode == 0:
        return git.stdout
    t = subprocess.run(['git', '-C', ROOT, 'show', f'HEAD:{TEMPLATE}'], capture_output=True, text=True, check=True).stdout
    return re.sub(r'guid: [0-9a-f]{32}', 'guid: ' + uuid.uuid4().hex, t)


def loudness(path, filt='anull'):
    """Mean volume (dB RMS) of the file after `filt`: short clips are below ebur128's 400 ms gate, so RMS is the measure."""
    r = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-i', path, '-af', filt + ',volumedetect', '-f', 'null', '-'], capture_output=True, text=True)
    m = re.findall(r'mean_volume:\s+(-?[\d.]+) dB', r.stderr)
    return float(m[-1]) if m else -91.0


def main():
    src = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    for spec in sys.argv[2:]:
        sid, _, cap = spec.partition(':')
        cap = float(cap or 0.8)
        n = 0
        for i in range(1, 5):
            f = os.path.join(src, f'{sid}_{i}.mp3')
            if not os.path.exists(f):
                continue
            if loudness(f) < -60:
                print(f'skip {f}: silent')
                continue
            n += 1
            rel = f'Assets/Audio/Generated/Core/{sid}_t{n}.ogg'
            fade = min(0.08, cap / 4)
            pre = f'silenceremove=start_periods=1:start_threshold=-50dB,atrim=0:{cap},afade=t=out:st={cap - fade}:d={fade}'
            gain = max(-6.0, min(30.0, TARGET_RMS - loudness(f, pre)))
            flt = f'{pre},volume={gain:.1f}dB,alimiter=limit=0.89:level=false'
            subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', f, '-af', flt, '-ac', '1', '-ar', '44100', '-c:a', 'libvorbis', '-q:a', '4',
                            os.path.join(ROOT, rel)], check=True)
            with open(os.path.join(ROOT, rel + '.meta'), 'w', newline='\n') as m:
                m.write(meta_for(rel))
            print(f'{rel}  {loudness(os.path.join(ROOT, rel)):.1f} dB rms')
        print(f'{sid}: {n} takes')


if __name__ == '__main__':
    main()
