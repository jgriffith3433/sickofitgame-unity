"""Bakes effect art (the Level 1 portal and the Overlord's beam) into sliced sprite sheets, without Unity.

Effects aren't creatures, so they don't go through art.mjs's figure framing: an effect is a set of clips that must
share one frame and one pivot (the portal's Open, Loop, Close and Scorch all sit on the same ground point), and its
background is keyed by colour, not by a cutout model.

A job file says what to bake:

    {
      "out": "Assets/Art/Effects/Portal",          sheets go here as <name>_<clip>.png + .sheet.json + .meta
      "name": "Portal",
      "key": "magenta",                            magenta: a flat #ff00ff plate (fire and grass both survive it)
                                                   black: light on black, alpha from brightness (beams, glows, bursts)
      "size": [832, 480],                          every source is scaled to this before cropping
      "pivot": [416, 262],                         the pivot in that frame (the ground point the entity stands on)
      "worldWidth": 1.5, "widthPixels": 600,       pixels per unit = widthPixels * scale / worldWidth
      "scale": 0.5,                                cells are scaled by this after cropping
      "crop": [x0, y0, x1, y1],                    optional: a fixed box in that frame instead of the art's own bounds
                                                   (every bake prints the box it used; copy it to keep a re-bake's
                                                   cell size, pivot and world size identical)
      "clips": {
        "open":   { "video": "open.mp4", "fps": 16, "start": 0, "end": 0, "drop_last": false },
        "loop":   { "video": "loop.mp4", "fps": 16, "drop_last": true, "count": 32 },   count: exactly this many frames
        "scorch": { "image": "scorch.png" },
        "swirl":  { "frames": "swirl/", "key": "none" },   a folder of PNGs; key "none" keeps their own alpha
        "charge": { "image": "glow.png", "make": "charge", "frames": 15 }
      }
    }

"make" builds a clip from one still: charge (grows in and pulses), column (a beam that crackles: scrolls and
flickers), burst (flash, grows, fades). Every clip is cropped to the union of all the job's frames, so the pivot is
the same point in every sheet.

    python fx_sheets.py <job.json> [--src <folder with the sources>] [--preview out.png]

Sources are looked up in --src (default: the job file's folder). "out" is relative to the current folder (run it
from the repo root). Extra fields ("about", "made") are notes and ignored.

Needs ffmpeg, Pillow and numpy. A sheet that already has a .meta keeps its guid.
"""
import argparse
import json
import math
import os
import random
import re
import struct
import subprocess
import sys
import tempfile
import uuid

import numpy as np
from PIL import Image

MAX_SHEET = 2048
HERE = os.path.dirname(os.path.abspath(__file__))
TEMPLATE = os.path.join(HERE, "..", "..", "Assets", "Art", "Enemies", "Imp", "Imp_idle.png.meta")
TEXT_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


# ─── keying ──────────────────────────────────────────────────────────────────

def border_color(rgb):
    edge = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]]).astype(np.float32)
    return np.median(edge, axis=0)


def key_magenta(rgb, near=70.0, far=150.0):
    """Alpha from the distance to the plate colour (sampled from the border), plate colour taken back out of the edges."""
    bg = border_color(rgb)
    f = rgb.astype(np.float32)
    d =np.sqrt(((f - bg) ** 2).sum(axis=2))
    a = np.clip((d - near) / (far - near), 0.0, 1.0)
    # Despill: what's left of the plate in a half-keyed pixel.
    safe = np.maximum(a, 1e-3)[..., None]
    col = (f - (1.0 - a[..., None]) * bg) / safe
    a[a < 0.04] = 0.0
    return np.dstack([np.clip(col, 0, 255), a * 255.0]).astype(np.uint8)


def key_black(rgb, gain=1.35, floor=0.07):
    """Light on black: alpha from brightness, colour un-premultiplied, so it looks the same over the meadow's sky."""
    f = rgb.astype(np.float32) / 255.0
    m = f.max(axis=2)
    a = np.clip((m - floor) / (1.0 - floor) * gain, 0.0, 1.0)
    col = f / np.maximum(m, 1e-3)[..., None] * np.clip(m * 1.15, 0, 1)[..., None]
    col = np.clip(col, 0, 1)
    return np.dstack([col * 255.0, a * 255.0]).astype(np.uint8)


# ─── sources ─────────────────────────────────────────────────────────────────

def video_frames(path, fps, start, end, tmp):
    folder = tempfile.mkdtemp(dir=tmp)
    cmd = ["ffmpeg", "-v", "error", "-y"]
    if start:
        cmd += ["-ss", str(start)]
    cmd += ["-i", path]
    if end:
        cmd += ["-t", str(end - (start or 0))]
    cmd += ["-vf", f"fps={fps}", "-start_number", "0", os.path.join(folder, "f_%04d.png")]
    subprocess.run(cmd, check=True)
    return [Image.open(os.path.join(folder, f)).convert("RGB") for f in sorted(os.listdir(folder))]


def made(kind, rgba, count, seed):
    """Frames made from one keyed still (RGBA float arrays in, same size out)."""
    rng = random.Random(seed)
    h, w, _ = rgba.shape
    img = Image.fromarray(rgba.astype(np.uint8), "RGBA")
    frames = []
    for i in range(count):
        t = i / max(1, count - 1)
        if kind == "charge":
            # Grows in over the first two thirds, then throbs hard just before the beam.
            grow = 0.25 + 0.75 * min(1.0, t / 0.66) ** 0.7
            throb = 1.0 + (0.08 * math.sin(i * 2.1) if t < 0.66 else 0.16 * (i % 2))
            s = grow * throb
            alpha = 0.55 + 0.45 * min(1.0, t / 0.5)
            frames.append(scaled_about(img, s, s * (0.9 + 0.1 * rng.random()), (w / 2, h), alpha))
        elif kind == "column":
            # Crackle: the beam's texture scrolls downward, its width breathes, its brightness flickers.
            arr = np.roll(rgba, int(i * h / count * 0.5), axis=0).astype(np.float32)
            sx = 0.85 + 0.3 * rng.random()
            frame = Image.fromarray(arr.astype(np.uint8), "RGBA")
            frames.append(scaled_about(frame, sx, 1.0, (w / 2, h), 0.85 + 0.15 * rng.random()))
        elif kind == "burst":
            # A white flash, then the blast grows and thins out.
            s = 0.35 + 0.85 * (1.0 - (1.0 - t) ** 2.2)
            alpha = 1.0 if t < 0.45 else max(0.0, 1.0 - (t - 0.45) / 0.55)
            f = scaled_about(img, s, s, (w / 2, h), alpha)
            if i < 2:
                arr = f.astype(np.float32)
                arr[..., :3] = arr[..., :3] + (255.0 - arr[..., :3]) * (0.6 if i == 0 else 0.3)
                f = arr
            frames.append(f)
        else:
            sys.exit(f"unknown make: {kind}")
    return frames


def scaled_about(img, sx, sy, anchor, alpha):
    """img scaled by sx, sy about anchor (x, y in pixels), same canvas, alpha multiplied."""
    w, h = img.size
    nw, nh = max(1, int(w * sx)), max(1, int(h * sy))
    small = img.resize((nw, nh), Image.BILINEAR)
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.paste(small, (int(anchor[0] - anchor[0] * sx), int(anchor[1] - anchor[1] * sy)), small)
    arr = np.asarray(canvas).astype(np.float32)
    arr[..., 3] *= alpha
    return arr


# ─── sheets ──────────────────────────────────────────────────────────────────

def internal_id(rng):
    return rng.randint(-(2 ** 63), 2 ** 63 - 1)


def sprite_id(iid):
    b = struct.pack("<q", iid).hex()
    return "".join(b[i + 1] + b[i] for i in range(0, 16, 2)) + "0800000000000000"


def existing_guid(meta_path):
    if os.path.exists(meta_path):
        m = re.search(r"^guid: (\w+)", open(meta_path, encoding="utf-8").read(), re.M)
        if m:
            return m.group(1)
    return uuid.uuid4().hex


def write_meta(meta_path, cells, ppu, pivot):
    text = open(TEMPLATE, encoding="utf-8").read()
    guid = existing_guid(meta_path)
    text = re.sub(r"^guid: \w+", f"guid: {guid}", text, count=1, flags=re.M)
    text = re.sub(r"^  spritePixelsToUnits: .*$", f"  spritePixelsToUnits: {ppu:g}", text, count=1, flags=re.M)
    rng = random.Random(f"{os.path.basename(meta_path)}:{guid}")
    ids = [internal_id(rng) for _ in cells]
    sprites = []
    for c, iid in zip(cells, ids):
        sprites.append(f"""    - serializedVersion: 2
      name: {c['name']}
      rect:
        serializedVersion: 2
        x: {c['x']}
        y: {c['y']}
        width: {c['w']}
        height: {c['h']}
      alignment: 9
      pivot: {{x: {pivot[0]:.6f}, y: {pivot[1]:.6f}}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {sprite_id(iid)}
      internalID: {iid}
      vertices: []
      indices:
      edges: []
      weights: []
""")
    start = text.index("    sprites:\n") + len("    sprites:\n")
    end = text.index("\n    outline: []\n", start) + 1
    text = text[:start] + "".join(sprites) + text[end:]
    start = text.index("    nameFileIdTable:\n") + len("    nameFileIdTable:\n")
    end = text.index("  mipmapLimitGroupName:", start)
    text = text[:start] + "".join(f"      {c['name']}: {iid}\n" for c, iid in zip(cells, ids)) + text[end:]
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def pack(frames, clip, cell_w, cell_h, out_png, ppu, pivot):
    cols = max(1, min(len(frames), MAX_SHEET // cell_w))
    rows = (len(frames) + cols - 1) // cols
    if rows * cell_h > MAX_SHEET:
        sys.exit(f"{clip}: {len(frames)} frames of {cell_w}x{cell_h} don't fit a {MAX_SHEET} sheet; lower fps or scale")
    tex_w, tex_h = cols * cell_w, rows * cell_h
    sheet = Image.new("RGBA", (tex_w, tex_h), (0, 0, 0, 0))
    cells = []
    for i, f in enumerate(frames):
        col, row = i % cols, i // cols
        sheet.paste(f, (col * cell_w, row * cell_h))
        cells.append({"name": f"{clip}_{i:02d}", "x": col * cell_w, "y": tex_h - (row + 1) * cell_h, "w": cell_w, "h": cell_h})
    sheet.save(out_png, optimize=True)
    with open(os.path.splitext(out_png)[0] + ".sheet.json", "w", encoding="utf-8", newline="\n") as f:
        json.dump({"generatedBy": "Tools/Art/fx_sheets.py", "pixelsPerUnit": round(ppu, 3), "pivot": [round(pivot[0], 6), round(pivot[1], 6)],
                   "sprites": cells}, f, indent=1)
        f.write("\n")
    write_meta(out_png + ".meta", cells, round(ppu, 3), pivot)
    side = os.path.splitext(out_png)[0] + ".sheet.json.meta"
    with open(side, "w", encoding="utf-8", newline="\n") as f:
        f.write(TEXT_META.format(guid=existing_guid(side)))
    return tex_w, tex_h


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("job")
    ap.add_argument("--src", default=None, help="folder the job's sources are in (default: the job's folder)")
    ap.add_argument("--preview", default=None, help="write a contact sheet of every clip here")
    args = ap.parse_args()
    job = json.load(open(args.job, encoding="utf-8"))
    base = os.path.abspath(args.src) if args.src else os.path.dirname(os.path.abspath(args.job))
    size = tuple(job["size"])
    keyer = {"magenta": key_magenta, "black": key_black, "none": None}[job.get("key", "magenta")]

    clips = {}
    with tempfile.TemporaryDirectory() as tmp:
        for name, c in job["clips"].items():
            src = lambda p: p if os.path.isabs(p) else os.path.join(base, p)
            clip_key = {"magenta": key_magenta, "black": key_black, "none": None}[c["key"]] if "key" in c else keyer
            keyed = (lambda im: clip_key(np.asarray(im.convert("RGB").resize(size, Image.LANCZOS)))) if clip_key else \
                (lambda im: np.asarray(im.convert("RGBA").resize(size, Image.LANCZOS)))
            if isinstance(c.get("frames"), str):
                folder = src(c["frames"])
                frames = [keyed(Image.open(os.path.join(folder, f))).astype(np.float32)
                          for f in sorted(os.listdir(folder)) if f.lower().endswith(".png")]
            elif "video" in c:
                raw = video_frames(src(c["video"]), c.get("fps", 16), c.get("start", 0), c.get("end", 0), tmp)
                if c.get("drop_last") and len(raw) > 1:
                    raw = raw[:-1]
                if c.get("reverse"):
                    raw = raw[::-1]
                if c.get("count") and len(raw) != c["count"]:
                    # Exactly count frames, picked evenly, so a clip of any length keeps the entity's frame count and timing.
                    n = c["count"]
                    raw = [raw[min(len(raw) - 1, int(i * len(raw) / n))] for i in range(n)]
                frames = [keyed(f).astype(np.float32) for f in raw]
            else:
                still = keyed(Image.open(src(c["image"]))).astype(np.float32)
                frames = made(c["make"], still, c.get("frames", 12), name) if "make" in c else [still]
            if not frames:
                sys.exit(f"{name}: no frames")
            clips[name] = frames

    # One crop for the whole effect, so the pivot is the same point in every clip.
    alpha = np.zeros(size[::-1], np.float32)
    for frames in clips.values():
        for f in frames:
            alpha = np.maximum(alpha, f[..., 3])
    ys, xs = np.nonzero(alpha > 8)
    if len(xs) == 0:
        sys.exit("everything keyed out")
    px, py = job["pivot"]
    pad = 4
    if "crop" in job:
        # A fixed box: a new take re-baked over an old one keeps its cell size, pivot and world size exactly.
        x0, y0, x1, y1 = job["crop"]
        if xs.min() < x0 or ys.min() < y0 or xs.max() >= x1 or ys.max() >= y1:
            print(f"warning: art reaches outside crop {job['crop']} (it spans {xs.min()},{ys.min()},{xs.max() + 1},{ys.max() + 1}); "
                  "the edge is cut off")
    else:
        x0, x1 = max(0, xs.min() - pad), min(size[0], xs.max() + 1 + pad)
        y0, y1 = max(0, ys.min() - pad), min(size[1], ys.max() + 1 + pad)
    print(f"crop: [{x0}, {y0}, {x1}, {y1}]")
    scale = job.get("scale", 1.0)
    cell_w, cell_h = max(1, round((x1 - x0) * scale)), max(1, round((y1 - y0) * scale))
    pivot = ((px - x0) / (x1 - x0), 1.0 - (py - y0) / (y1 - y0))
    ppu = job["widthPixels"] * scale / job["worldWidth"]

    out = job["out"] if os.path.isabs(job["out"]) else os.path.join(os.getcwd(), job["out"])
    os.makedirs(out, exist_ok=True)
    rows = []
    for name, frames in clips.items():
        cells = [Image.fromarray(np.clip(f, 0, 255).astype(np.uint8)[y0:y1, x0:x1], "RGBA").resize((cell_w, cell_h), Image.LANCZOS) for f in frames]
        png = os.path.join(out, f"{job['name']}_{name}.png")
        tw, th = pack(cells, name, cell_w, cell_h, png, ppu, pivot)
        print(f"{name}: {len(cells)} frames of {cell_w}x{cell_h} -> {png} ({tw}x{th}), {ppu:.1f} px/unit, "
              f"{cell_w / ppu:.2f}x{cell_h / ppu:.2f} units, pivot {pivot[0]:.3f},{pivot[1]:.3f}")
        rows.append(cells)

    if args.preview:
        thumb_w = min(cell_w, 160)
        th = max(1, round(cell_h * thumb_w / cell_w))
        per = max(len(r) for r in rows)
        sheet = Image.new("RGBA", (thumb_w * per, th * len(rows)), (60, 110, 200, 255))
        for r, cells in enumerate(rows):
            for i, c in enumerate(cells):
                t = c.resize((thumb_w, th), Image.BILINEAR)
                sheet.paste(t, (i * thumb_w, r * th), t)
                cx, cy = int(i * thumb_w + pivot[0] * thumb_w), int(r * th + (1 - pivot[1]) * th)
                for d in range(-3, 4):
                    for (x, y) in ((cx + d, cy), (cx, cy + d)):
                        if 0 <= x < sheet.width and 0 <= y < sheet.height:
                            sheet.putpixel((x, y), (255, 0, 0, 255))
        sheet.save(args.preview)
        print("preview:", args.preview)


if __name__ == "__main__":
    main()
