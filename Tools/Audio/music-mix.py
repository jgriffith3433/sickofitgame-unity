#!/usr/bin/env python3
"""Makes the game's music files from the Hawkes Gen takes picked in Tools/Audio/music.json (Docs/Story.md, "Music").

    python Tools/Audio/music-mix.py --takes <folder with the picked takes> [--only id,id] [--report]

Needs ffmpeg and numpy. Run from the repo root. Each track in music.json names its take (a file in --takes, as
saved by hawkesgen_job save_to) and how to cut it:

  loop    a seamless loop: the stretch of whole bars between 'minSeconds' and 'maxSeconds' whose end best matches its
          start (onset envelope, then the waveform), the bar after it cross-faded into its first 'crossfade' seconds,
          so the last sample runs straight into the first. Skips 'skipIntro' seconds of the take's intro.
  riser   a riser into a slam: the take's biggest hit (between 'searchFrom' and 'searchTo') is moved to 'hitAt' seconds
          (silence before it is padded or the riser cut), 'tail' seconds kept after it, faded out. 'stretchFrom' slows
          the take's riser (from there to the hit) to fill 'hitAt'; 'riserRamp' [from, to] swells its gain into the hit.
  hit     a one-shot: from just before the first strong onset, 'length' seconds, faded out over 'fade'.
  sting   a short sting: leading silence trimmed, 'length' seconds, faded out over 'fade'.

Every output is set to 'lufs' loudness with a static gain (peaks kept under -1 dBFS), 44.1 kHz stereo Ogg Vorbis.
--report prints where each cut landed (and the loop's seam match) without writing.
"""
import argparse
import json
import os
import subprocess
import sys

import numpy as np

RATE = 44100
HERE = os.path.dirname(os.path.abspath(__file__))


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(RATE), "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def encode(audio, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = np.clip(audio, -1.0, 1.0).astype(np.float32).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", "2", "-ar", str(RATE), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "5", path], input=data, check=True)


def loudness(audio):
    """Integrated loudness (LUFS) through ffmpeg's ebur128."""
    data = audio.astype(np.float32).tobytes()
    out = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-f", "f32le", "-ac", "2", "-ar", str(RATE), "-i", "-",
                          "-af", "ebur128", "-f", "null", "-"], input=data, capture_output=True).stderr.decode("utf8", "replace")
    value = None
    for line in out.splitlines():
        line = line.strip()
        if line.startswith("I:") and "LUFS" in line:
            value = float(line.split()[1])
    return value


def level(audio, lufs):
    now = loudness(audio)
    if now is None or now < -70:
        return audio, 0.0
    gain_db = lufs - now
    peak = float(np.max(np.abs(audio))) or 1.0
    gain_db = min(gain_db, -1.0 - 20 * np.log10(peak))
    return audio * (10 ** (gain_db / 20)), gain_db


def onset_envelope(mono, hop):
    frames = len(mono) // hop
    energy = np.sqrt(np.mean(mono[:frames * hop].reshape(frames, hop) ** 2, axis=1) + 1e-12)
    log = np.log(energy + 1e-4)
    flux = np.maximum(0.0, np.diff(log, prepend=log[0]))
    return energy, flux


def band_energies(mono, hop, bands=48):
    """Log energy in log-spaced bands per hop (60 Hz-8 kHz): what is playing, not just when."""
    size = 2048
    frames = (len(mono) - size) // hop
    window = np.hanning(size).astype(np.float32)
    idx = np.arange(size)[None, :] + hop * np.arange(frames)[:, None]
    spec = np.abs(np.fft.rfft(mono[idx] * window, axis=1))
    freqs = np.fft.rfftfreq(size, 1.0 / RATE)
    edges = np.geomspace(60, 8000, bands + 1)
    out = np.zeros((frames, bands), np.float32)
    for b in range(bands):
        sel = (freqs >= edges[b]) & (freqs < edges[b + 1])
        out[:, b] = np.log(spec[:, sel].sum(axis=1) + 1e-3)
    return out


def corr(a, b):
    a = a - a.mean()
    b = b - b.mean()
    return float(np.sum(a * b) / (np.sqrt(np.sum(a * a) * np.sum(b * b)) + 1e-9))


def fade(n, out=True):
    t = np.linspace(0.0, 1.0, n, endpoint=False) if n > 0 else np.zeros(0)
    curve = np.cos(t * np.pi / 2) if out else np.sin(t * np.pi / 2)
    return curve[:, None]


def make_exact(audio, spec, energy, flux, bands, fps, total):
    """A layer cut to exactly 'exactLength' seconds (its base loop's length), so the two stay in step. Starts where the
    seam matches best; a take with no room for the bar after the end fades its own end into its start instead."""
    length = int(round(spec["exactLength"] * RATE))
    xf = spec.get("crossfade", 0.25)
    k = int(xf * RATE)
    window = int(6 * fps)
    last = total - spec["exactLength"] - xf - spec.get("tailGuard", 1.0)
    best = (-2.0, 0.0)
    for start in np.arange(spec.get("skipIntro", 0.0), max(last, 0.0) + 1e-9, 0.05):
        s, e = int(start * fps), int((start + spec["exactLength"]) * fps)
        if e + window >= len(bands):
            break
        score = 0.4 * corr(flux[s:s + window], flux[e:e + window]) + 0.6 * corr(bands[s:s + window], bands[e:e + window])
        if score > best[0]:
            best = (score, float(start))
    score, start = best
    s0 = int(start * RATE)
    if last < 0:
        loop = audio[:length].copy()
        loop[-k:] = loop[-k:] * fade(k) + audio[:k] * fade(k, out=False)
        return loop, f"layer fitted to {length / RATE:.3f} s from the take's start, end faded into its start"
    loop = audio[s0:s0 + length].copy()
    loop[:k] = loop[:k] * fade(k, out=False) + audio[s0 + length:s0 + length + k] * fade(k)
    return loop, f"layer {start:.2f}-{start + length / RATE:.2f} s of {total:.1f} s: exactly {length / RATE:.3f} s, seam match {score:.2f}"


def make_loop(audio, spec):
    bpm = spec["bpm"]
    beats_per_bar = spec.get("beatsPerBar", 4)
    hop = 441  # 10 ms
    mono = audio.mean(axis=1)
    energy, flux = onset_envelope(mono, hop)
    bands = band_energies(mono, hop)
    fps = RATE / hop
    total = len(mono) / RATE
    xf = spec.get("crossfade", 0.25)
    tail_guard = spec.get("tailGuard", 4.0)  # the take's own ending (often a fade) stays out of the loop
    skip = spec.get("skipIntro", 4.0)
    window = int(6 * fps)

    if spec.get("exactLength"):
        return make_exact(audio, spec, energy, flux, bands, fps, total)

    # The take's real tempo: the beat period near the asked bpm that best repeats the onsets.
    best_period, best_score = None, -1
    for scale in np.linspace(0.97, 1.03, 61):
        period = 60.0 / (bpm * scale) * fps
        lag = int(round(period * beats_per_bar * 4))
        if lag >= len(flux) // 2:
            continue
        a, b = flux[:-lag], flux[lag:]
        score = float(np.dot(a - a.mean(), b - b.mean()) / (np.std(a) * np.std(b) * len(a) + 1e-9))
        if score > best_score:
            best_period, best_score = period, score
    bar = best_period * beats_per_bar / fps  # seconds

    best = None
    min_bars = int(np.ceil(spec["minSeconds"] / bar))
    max_bars = int(np.floor(spec["maxSeconds"] / bar))
    phrase = spec.get("phraseBars", 4)  # loops are whole phrases, so the seam falls where the music turns anyway
    for bars in range(max_bars, min_bars - 1, -1):
        if bars % phrase:
            continue
        length = bars * bar
        for start in np.arange(skip, total - length - xf - tail_guard, 0.05):
            s, e = int(start * fps), int((start + length) * fps)
            if e + window >= len(bands):
                break
            ea, eb = energy[s:s + window], energy[e:e + window]
            rhythm = corr(flux[s:s + window], flux[e:e + window])
            # Just before the seam too: what plays into the loop's end should sound like what plays into its start.
            harmony = corr(bands[s:s + window], bands[e:e + window]) * 0.5 + corr(bands[max(0, s - window):s], bands[e - window:e]) * 0.5 \
                if s >= window else corr(bands[s:s + window], bands[e:e + window])
            balance = float(min(ea.mean(), eb.mean()) / (max(ea.mean(), eb.mean()) + 1e-9))
            score = (0.4 * rhythm + 0.6 * harmony) * balance
            if best is None or score > best[0]:
                best = (score, start, length, bars)
        if best is not None and best[0] > spec.get("goodEnough", 0.8):
            break
    if best is None:
        raise SystemExit(f"{spec['id']}: the take ({total:.1f} s) is too short for a {spec['minSeconds']} s loop")
    score, start, length, bars = best

    # Sample-level: slide the end over +-20 ms to match the start's waveform.
    s0 = int(start * RATE)
    n = int(length * RATE)
    probe = int(0.08 * RATE)
    ref = mono[s0:s0 + probe]
    shift_best, corr_best = 0, -2.0
    for shift in range(-int(0.02 * RATE), int(0.02 * RATE) + 1, 4):
        seg = mono[s0 + n + shift:s0 + n + shift + probe]
        if len(seg) < probe:
            continue
        c = float(np.dot(ref, seg) / (np.linalg.norm(ref) * np.linalg.norm(seg) + 1e-9))
        if c > corr_best:
            shift_best, corr_best = shift, c
    n += shift_best

    loop = audio[s0:s0 + n].copy()
    k = int(xf * RATE)
    after = audio[s0 + n:s0 + n + k]
    # The bar after the loop's end fades into its start: sample n-1 is followed by sample 0 exactly as in the take.
    loop[:k] = loop[:k] * fade(k, out=False) + after * fade(k, out=True)
    info = (f"loop {start:.2f}-{start + n / RATE:.2f} s of {total:.1f} s: {bars} bars of {bar:.3f} s "
            f"({60 * beats_per_bar / bar:.1f} bpm), seam match {score:.2f}, waveform {corr_best:.2f}")
    return loop, info


def biggest_hit(audio, after=0.0, before=None):
    hop = 441
    mono = audio.mean(axis=1)
    energy, flux = onset_envelope(mono, hop)
    fps = RATE / hop
    # A hit: a big jump in level that stays loud for a moment.
    sustain = np.convolve(energy, np.ones(20) / 20, mode="full")[19:len(energy) + 19]
    score = flux * sustain[:len(flux)]
    first = int(after * fps)
    last = len(score) if before is None else int(before * fps)
    i = first + int(np.argmax(score[first:last]))
    return i / fps


def first_onset(audio, threshold_db=-30.0):
    hop = 441
    mono = audio.mean(axis=1)
    energy, _ = onset_envelope(mono, hop)
    peak = energy.max()
    above = np.where(20 * np.log10(energy / peak + 1e-9) > threshold_db)[0]
    return (above[0] if len(above) else 0) * hop / RATE


def stretch(audio, factor):
    """Slower (factor < 1) or faster without changing pitch: ffmpeg's atempo, chained to stay in its 0.5-2 range."""
    steps = []
    while factor < 0.5:
        steps.append(0.5)
        factor /= 0.5
    steps.append(factor)
    chain = ",".join(f"atempo={f:.5f}" for f in steps)
    raw = subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ac", "2", "-ar", str(RATE), "-i", "-", "-af", chain,
                          "-f", "f32le", "-ac", "2", "-ar", str(RATE), "-"],
                         input=audio.astype(np.float32).tobytes(), check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def make_riser(audio, spec):
    hit = biggest_hit(audio, spec.get("searchFrom", 1.0), spec.get("searchTo"))
    hit_at, tail = spec["hitAt"], spec["tail"]
    if "stretchFrom" in spec:
        # The take's riser (stretchFrom to the hit) slowed down to fill hitAt, so a short build fills a long fall.
        s, h = int(spec["stretchFrom"] * RATE), int(hit * RATE)
        riser = stretch(audio[s:h], (h - s) / RATE / hit_at)
        audio = np.concatenate([riser, audio[h:]])
        hit = len(riser) / RATE
        note = f"riser {spec['stretchFrom']:.2f}-{h / RATE:.2f} s stretched to {hit:.2f} s; "
    else:
        note = ""
    if "riserRamp" in spec:
        # A crescendo into the hit: the gain goes from the first value to the second over the riser.
        h = int(hit * RATE)
        lo, hi = spec["riserRamp"]
        audio = audio.copy()
        audio[:h] *= np.geomspace(lo, hi, h, dtype=np.float32)[:, None]
    start = hit - hit_at
    if start < 0:
        audio = np.concatenate([np.zeros((int(-start * RATE), 2), np.float32), audio])
        start = 0.0
    s, e = int(start * RATE), int((start + hit_at + tail) * RATE)
    out = audio[s:e].copy()
    k = int(spec.get("fade", 1.2) * RATE)
    out[-k:] *= fade(k)
    k = int(0.05 * RATE)
    out[:k] *= fade(k, out=False)
    return out, f"{note}hit at {hit:.2f} s, now at {hit_at:.2f} s; {len(out) / RATE:.2f} s"


def make_hit(audio, spec):
    on = max(0.0, first_onset(audio, spec.get("threshold", -24.0)) - 0.03)
    s, e = int(on * RATE), int((on + spec["length"]) * RATE)
    out = audio[s:e].copy()
    k = int(spec.get("fade", 1.5) * RATE)
    out[-k:] *= fade(k)
    k = int(0.01 * RATE)
    out[:k] *= fade(k, out=False)
    return out, f"from {on:.2f} s, {len(out) / RATE:.2f} s"


def make_sting(audio, spec):
    on = max(0.0, first_onset(audio, spec.get("threshold", -30.0)) - 0.02)
    on += spec.get("offset", 0.0)
    s, e = int(on * RATE), int((on + spec["length"]) * RATE)
    out = audio[s:e].copy()
    k = int(spec.get("fade", 0.8) * RATE)
    out[-k:] *= fade(k)
    k = int(0.01 * RATE)
    out[:k] *= fade(k, out=False)
    return out, f"from {on:.2f} s, {len(out) / RATE:.2f} s"


MAKERS = {"loop": make_loop, "riser": make_riser, "hit": make_hit, "sting": make_sting}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--takes", required=True, help="folder with the picked takes (music.json 'take' file names)")
    parser.add_argument("--only", default="", help="comma-separated track ids")
    parser.add_argument("--report", action="store_true", help="print the cuts, write nothing")
    args = parser.parse_args()
    with open(os.path.join(HERE, "music.json"), encoding="utf8") as f:
        config = json.load(f)
    only = set(filter(None, args.only.split(",")))
    for spec in config["tracks"]:
        if only and spec["id"] not in only:
            continue
        source = os.path.join(args.takes, spec["take"])
        if not os.path.exists(source):
            print(f"{spec['id']}: missing take {source}", file=sys.stderr)
            continue
        audio = decode(source)
        out, info = MAKERS[spec["mode"]](audio, spec)
        out, gain = level(out, spec.get("lufs", config.get("lufs", -16.0)))
        print(f"{spec['id']}: {info}; gain {gain:+.1f} dB -> {spec['out']}")
        if not args.report:
            encode(out, spec["out"])


if __name__ == "__main__":
    main()
