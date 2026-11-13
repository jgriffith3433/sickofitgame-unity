#!/usr/bin/env python3
"""Measure every clip in audio.json against the mix targets and write the table into docs/audio-mix.md.

  python3 Tools/Audio/loudness_report.py            # measure (needs the real audio files, not LFS pointers) and print
  python3 Tools/Audio/loudness_report.py --write    # also replace the table between the LOUDNESS markers in docs/audio-mix.md
  python3 Tools/Audio/loudness_report.py --fix-list # print the clips that miss a target (feed them to Hawkes Gen audio.process)

Targets (docs/audio-mix.md): music -16 LUFS, SFX/UI/ambience -14, voice -16; true peak <= -1 dBTP. Tolerance +-2 LU.
Uses ffmpeg's ebur128 (integrated loudness, true peak); free, no GPU.
"""
import json, re, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGET = {"Music": -16.0, "Sfx": -14.0, "Voice": -16.0, "Ambience": -14.0, "Ui": -14.0}
TOLERANCE = 2.0
PEAK_MAX = -1.0
MARK_A, MARK_B = "<!-- LOUDNESS:begin -->", "<!-- LOUDNESS:end -->"


def bus_of(s):
    b = s.get("bus")
    if b:
        return b
    return "Voice" if s.get("kind") == "Voice" else "Sfx"


def is_pointer(p):
    with open(p, "rb") as f:
        return f.read(40).startswith(b"version https://git-lfs")


def measure(path):
    r = subprocess.run(["ffmpeg", "-nostats", "-i", str(path), "-af", "ebur128=peak=true", "-f", "null", "-"],
                       capture_output=True, text=True)
    tail = r.stderr.split("Summary:")[-1]
    i = re.search(r"I:\s+(-?[\d.]+) LUFS", tail)
    p = re.search(r"Peak:\s+(-?[\d.]+) dBFS", tail)
    return (float(i.group(1)) if i else None, float(p.group(1)) if p else None)


def main():
    audio = json.load(open(ROOT / "Assets/GameConfig/audio.json"))
    rows, missing = [], 0
    for s in audio["sounds"]:
        clips = ([s["clip"]] if s.get("clip") else []) + [t for t in (s.get("takes") or []) if isinstance(t, str)]
        for c in clips:
            p = ROOT / "Assets" / c
            if not p.exists() or is_pointer(p):
                missing += 1
                continue
            lufs, peak = measure(p)
            rows.append((bus_of(s), s["id"], c, lufs, peak))
    if not rows:
        print(f"No audio files to measure ({missing} clips are absent or Git LFS pointers): fetch LFS first.")
        return 2
    out = ["| Bus | Target LUFS | Clips | Median LUFS | Min..Max LUFS | Worst true peak | Off target |", "|---|---|---|---|---|---|---|"]
    bad = []
    for bus in sorted({r[0] for r in rows}):
        rs = [r for r in rows if r[0] == bus and r[3] is not None]
        if not rs:
            continue
        ls = sorted(r[3] for r in rs)
        off = [r for r in rs if abs(r[3] - TARGET.get(bus, -14)) > TOLERANCE or (r[4] is not None and r[4] > PEAK_MAX)]
        bad += off
        out.append(f"| {bus} | {TARGET.get(bus, -14):g} | {len(rs)} | {ls[len(ls)//2]:.1f} | {ls[0]:.1f}..{ls[-1]:.1f} | "
                   f"{max(r[4] for r in rs if r[4] is not None):.1f} dBTP | {len(off)} |")
    table = "\n".join(out) + f"\n\n{missing} clip(s) skipped (not made yet or not fetched)."
    print(table)
    if "--fix-list" in sys.argv:
        for b, i, c, l, pk in bad:
            print(f"{b}\t{i}\t{c}\t{l}\t{pk}")
    if "--write" in sys.argv:
        doc = ROOT / "docs/audio-mix.md"
        txt = doc.read_text(encoding="utf-8")
        block = f"{MARK_A}\n{table}\n{MARK_B}"
        if MARK_A in txt:
            txt = re.sub(re.escape(MARK_A) + ".*?" + re.escape(MARK_B), lambda m: block, txt, flags=re.S)
        else:
            txt += "\n## Measured loudness\n\n" + block + "\n"
        doc.write_text(txt, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
