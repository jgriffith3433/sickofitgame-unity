"""The art of the climb (Docs/LevelDesign.md, "The art of the climb"): turns the chosen Hawkes Gen takes into the
sources Tools/Art/fx_sheets.py bakes, then bakes them.

Z-Image paints the "flat magenta" plate with shading (purples and pinks), and some backdrops tile, so the stills need
more than fx_sheets' colour key:

  key      magenta: alpha from how purple a pixel is (red and blue both well over green), so dark rock, orange lava and
           yellow fire all stay solid while every shade of the plate goes; the plate's tint is taken back out of the edges.
           black: light on black (smoke), alpha from brightness. none: opaque (a seamless texture).
  wrap     make it tile sideways (x) or up (y): the last strip is crossfaded into the first.
  centre   keep only the middle share of the width (a lava fall painted with stray streams at its sides).
  cropX    keep only this span of the width, as shares [from, to] (a landing's lower side steps).
  cropY    keep only this span of the height, as shares [from, to] (an updraft's flame base at the bottom).
  trim     crop to the art (plus a margin).
  top      for a platform: find its walkable top (the first row solid across the middle) and set the pivot so the
           collider's top lands on it (the entity's body height 'body' in units).

The plan is Tools/Art/climb/climb.json: one entry per piece with its takes (Hawkes Gen job, seed, asset), the prep and
the fx_sheets job fields. Usage:

    python Tools/Art/climb_art.py Tools/Art/climb/climb.json --src <folder with the takes> [--only lavaEdge,stoneMedium]

The takes are found in --src as <piece>/<file> (the names in the plan; a clip is an "image" or a "video" with
fx_sheets' fps/start/end/count/drop_last). Keyed frames go to <src>/_prep/<piece>/<clip>/, the fx_sheets jobs to
Tools/Art/climb/<piece>.json (kept: the record of what made each sheet), sheets to "out". Every clip of a piece
shares one box and pivot.
Needs ffmpeg, Pillow and numpy.
"""
import argparse
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))


def key_purple(rgb, lo=18.0, hi=60.0):
    f = rgb.astype(np.float32)
    r, g, b = f[..., 0], f[..., 1], f[..., 2]
    m = np.minimum(r, b) - g
    a = 1.0 - np.clip((m - lo) / (hi - lo), 0.0, 1.0)
    # Despill: what is left of the plate's purple in a kept pixel goes back toward grey.
    spill = np.clip(m, 0.0, None) * (1.0 - a * 0.5)
    out = f.copy()
    out[..., 0] -= np.where(r > g, spill, 0.0)
    out[..., 2] -= np.where(b > g, spill, 0.0)
    a[a < 0.05] = 0.0
    return np.dstack([np.clip(out, 0, 255), a * 255.0]).astype(np.uint8)


def key_black(rgb, gain=1.4, floor=0.06):
    f = rgb.astype(np.float32) / 255.0
    m = f.max(axis=2)
    a = np.clip((m - floor) / (1.0 - floor) * gain, 0.0, 1.0)
    col = f / np.maximum(m, 1e-3)[..., None] * np.clip(m * 1.15, 0, 1)[..., None]
    return np.dstack([np.clip(col, 0, 1) * 255.0, a * 255.0]).astype(np.uint8)


def wrap(arr, axis, share):
    """Seamless along an axis: the last 'share' crossfades into the start, and is then cut off."""
    n = arr.shape[1 if axis == "x" else 0]
    b = max(2, int(n * share))
    f = arr.astype(np.float32)
    t = np.linspace(0.0, 1.0, b, dtype=np.float32)
    if axis == "x":
        head, tail = f[:, :b], f[:, n - b:]
        f[:, :b] = head * t[None, :, None] + tail * (1.0 - t[None, :, None])
        f = f[:, :n - b]
    else:
        head, tail = f[:b], f[n - b:]
        f[:b] = head * t[:, None, None] + tail * (1.0 - t[:, None, None])
        f = f[:n - b]
    return f.astype(np.uint8)


def trim_box(alpha, pad):
    ys, xs = np.nonzero(alpha > 10)
    if len(xs) == 0:
        sys.exit("everything keyed out")
    h, w = alpha.shape
    return max(0, xs.min() - pad), max(0, ys.min() - pad), min(w, xs.max() + 1 + pad), min(h, ys.max() + 1 + pad)


def walkable_top(alpha, share=0.6, solid=0.6):
    """First row (from the top) that is solid across the middle 'share' of the art's width."""
    h, w = alpha.shape
    x0, x1 = int(w * (1 - share) / 2), int(w * (1 + share) / 2)
    for y in range(h):
        if (alpha[y, x0:x1] > 128).mean() >= solid:
            return y
    return 0


def video_frames(path, c):
    import tempfile
    folder = tempfile.mkdtemp()
    cmd = ["ffmpeg", "-v", "error", "-y"]
    if c.get("start"):
        cmd += ["-ss", str(c["start"])]
    cmd += ["-i", path]
    if c.get("end"):
        cmd += ["-t", str(c["end"] - c.get("start", 0))]
    cmd += ["-vf", f"fps={c.get('fps', 16)}", os.path.join(folder, "f_%04d.png")]
    subprocess.run(cmd, check=True)
    raw = [Image.open(os.path.join(folder, f)).convert("RGB") for f in sorted(os.listdir(folder))]
    if c.get("drop_last") and len(raw) > 1:
        raw = raw[:-1]
    if c.get("count") and len(raw) != c["count"]:
        n = c["count"]
        raw = [raw[min(len(raw) - 1, int(i * len(raw) / n))] for i in range(n)]
    return raw


def prep(piece, spec, src, out_dir):
    """Keys, wraps and trims every still and video frame of a piece with one shared box; returns (size, pivot, clips)
    with each clip a folder of RGBA frames for fx_sheets (key none)."""
    p = spec.get("prep", {})
    key = p.get("key", "magenta")
    size = None
    box = None
    keyed = {}
    for clip, c in spec["clips"].items():
        path = os.path.join(src, piece, c.get("image") or c["video"])
        ims = [Image.open(path).convert("RGB")] if "image" in c else video_frames(path, c)
        frames = []
        for im in ims:
            if size is None:
                size = tuple(p.get("resize") or im.size)
            if im.size != size:
                im = im.resize(size, Image.LANCZOS)
            rgb = np.asarray(im)
            if p.get("cropX"):
                w = rgb.shape[1]
                rgb = rgb[:, int(w * p["cropX"][0]):int(w * p["cropX"][1])]
            if p.get("cropY"):
                h = rgb.shape[0]
                rgb = rgb[int(h * p["cropY"][0]):int(h * p["cropY"][1])]
            if p.get("centre"):
                w = rgb.shape[1]
                k = int(w * (1 - p["centre"]) / 2)
                rgb = rgb[:, k:w - k]
            if p.get("wrap"):
                rgb = wrap(rgb, p["wrap"], p.get("wrapShare", 0.2))
            if key == "magenta":
                rgba = key_purple(rgb, *p.get("tolerance", [18.0, 60.0]))
            elif key == "black":
                rgba = key_black(rgb)
            else:
                rgba = np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])
            frames.append(rgba)
            if key != "none" and p.get("trim", True) and (rgba[..., 3] > 10).any():
                b = trim_box(rgba[..., 3], p.get("pad", 4))
                box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
        keyed[clip] = frames
    clips = {}
    first = None
    for clip, frames in keyed.items():
        folder = os.path.join(out_dir, piece, clip)
        os.makedirs(folder, exist_ok=True)
        for old in os.listdir(folder):
            os.remove(os.path.join(folder, old))
        for i, rgba in enumerate(frames):
            if box is not None:
                x0, y0, x1, y1 = box
                if p.get("wrap") == "x":
                    x0, x1 = 0, rgba.shape[1]
                if p.get("wrap") == "y":
                    y0, y1 = 0, rgba.shape[0]
                rgba = rgba[y0:y1, x0:x1]
            Image.fromarray(rgba, "RGBA").save(os.path.join(folder, f"{i:03d}.png"))
            if first is None:
                first = rgba
        c = {"frames": folder, "key": "none"}
        clips[clip] = c
    h, w = first.shape[:2]
    pivot = p.get("pivot", "centre")
    if pivot == "bottom":
        piv = [w / 2, h - p.get("pad", 4)]
    elif pivot == "top":
        # The collider's top on the art's walkable top: the pivot is the body's half height below it.
        top = walkable_top(first[..., 3])
        ppu = w / spec["worldWidth"]
        if spec.get("bodyWidth"):
            # The walkable top is as wide as the body: measured where it is solid, a few rows under its edge (a glow or a
            # chain above it doesn't count).
            band = first[top + 2:top + 12, :, 3] > 128
            xs = np.nonzero(band.any(axis=0))[0]
            span = xs.max() - xs.min() + 1
            ppu = span / spec["bodyWidth"]
            spec["worldWidth"] = round(w / ppu, 3)
            centre = (xs.min() + xs.max() + 1) / 2
        else:
            centre = w / 2
        piv = [centre, top + spec.get("body", 0.45) * 0.5 * ppu]
        print(f"{piece}: walkable top at row {top} of {h}, {w / ppu:.2f} units wide, {top / ppu:.2f} units of art above it")
    else:
        piv = [w / 2, h / 2]
    return [w, h], piv, clips


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("plan")
    ap.add_argument("--src", required=True)
    ap.add_argument("--only", default="")
    ap.add_argument("--preview", action="store_true")
    args = ap.parse_args()
    plan = json.load(open(args.plan, encoding="utf-8"))
    only = set(filter(None, args.only.split(",")))
    jobs_dir = os.path.dirname(os.path.abspath(args.plan))
    for piece, spec in plan["pieces"].items():
        if only and piece not in only:
            continue
        size, pivot, clips = prep(piece, spec, args.src, os.path.join(args.src, "_prep"))
        job = {
            "about": spec.get("about", ""),
            "gpu": spec.get("gpu", ""),
            "out": spec.get("out", plan["out"]),
            "name": spec["name"],
            "key": "none",
            "size": size,
            "pivot": [round(pivot[0], 1), round(pivot[1], 1)],
            "worldWidth": spec["worldWidth"],
            "widthPixels": size[0],
            "scale": spec.get("scale", 0.5),
            "clips": clips,
        }
        path = os.path.join(jobs_dir, f"{piece}.json")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(job, f, indent=2)
            f.write("\n")
        cmd = [sys.executable, os.path.join(HERE, "fx_sheets.py"), path, "--src", args.src]
        if args.preview:
            cmd += ["--preview", os.path.join(args.src, "_prep", f"{piece}_preview.png")]
        print(">", piece)
        subprocess.run(cmd, check=True)


if __name__ == "__main__":
    main()
