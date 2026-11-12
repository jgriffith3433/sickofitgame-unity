#!/usr/bin/env python3
"""Bakes the Overlord's taunt clips (Assets/GameConfig/art/overlordTaunt.json) from Hawkes Gen sprite.sheet outputs.

The i2v clips came back on a blue-grey plate (about rgb 62,69,90) instead of flat 00ff00, so Gen's 00ff00 key left the
plate half opaque. This keys each 300x300 frame on the colour its own corners hold (tolerance + a 1 px edge erode, specks
dropped), refuses a frame whose figure is not opaque in the middle, drops frames whose figure touches the frame's edge
(the flee clip breaks up as he leaves the picture: the entity's drift carries him out instead), and packs the sheets the
way Tools/Art/sheets.py does (same sidecar, one scale and one pivot for every clip), so GeneratedSheetImporter slices them.

    python3 Tools/Art/overlord_taunt_bake.py idle=<sheet.png> laugh=<sheet.png> point=<sheet.png> flee=<sheet.png>

Needs Pillow and numpy (any venv). Writes Assets/Art/Enemies/OverlordTaunt/OverlordTaunt_<clip>.png + .sheet.json and
prints the per-clip frame list. art.mjs's own --build needs DoD's keying and the source mp4s; this is the same output.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Enemies", "OverlordTaunt")
CELL = 300
COLS, ROWS = 4, 3
TOL = 40           # distance in rgb from the corner colour that counts as plate
MIN_ALPHA = 160    # Gen's 00ff00 key left the plate at alpha ~60-95; the figure is 255
HEIGHT_UNITS = 1.8  # the standing figure's height in world units: ~70 px on a 390x844 phone (Travel floors show 9.96 units across)
# Per clip: frames to keep (None = every frame that passes). Flee 8+ touch the top edge or break up (cropped hem).
KEEP = {"flee": list(range(8))}


def frames_of(path):
    a = np.array(Image.open(path).convert("RGBA")).astype(np.int32)
    return [a[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL] for r in range(ROWS) for c in range(COLS)]


def erode(mask):
    m = mask.copy()
    m[1:, :] &= mask[:-1, :]
    m[:-1, :] &= mask[1:, :]
    m[:, 1:] &= mask[:, :-1]
    m[:, :-1] &= mask[:, 1:]
    return m


def largest_parts(mask):
    """Keeps connected parts at least 2% of the largest (drops specks of plate noise)."""
    h, w = mask.shape
    label = np.zeros((h, w), np.int32)
    sizes = [0]
    n = 0
    for y0 in range(h):
        for x0 in range(w):
            if not mask[y0, x0] or label[y0, x0]:
                continue
            n += 1
            label[y0, x0] = n
            stack, size = [(y0, x0)], 0
            while stack:
                y, x = stack.pop()
                size += 1
                for yy, xx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                    if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not label[yy, xx]:
                        label[yy, xx] = n
                        stack.append((yy, xx))
            sizes.append(size)
    if n == 0:
        return mask
    big = max(sizes)
    keep = np.array([s >= big * 0.02 for s in sizes])
    keep[0] = False
    return keep[label]


def outside(plate):
    """The plate reachable from the frame's border (grey details inside the figure stay)."""
    seen = np.zeros_like(plate)
    seen[0, :], seen[-1, :], seen[:, 0], seen[:, -1] = plate[0, :], plate[-1, :], plate[:, 0], plate[:, -1]
    while True:
        grown = seen.copy()
        grown[1:, :] |= seen[:-1, :]
        grown[:-1, :] |= seen[1:, :]
        grown[:, 1:] |= seen[:, :-1]
        grown[:, :-1] |= seen[:, 1:]
        grown &= plate
        if (grown == seen).all():
            return seen
        seen = grown


def key(f):
    corners = np.concatenate([f[:8, :8].reshape(-1, 4), f[:8, -8:].reshape(-1, 4), f[-8:, :8].reshape(-1, 4), f[-8:, -8:].reshape(-1, 4)])
    bg = np.median(corners[:, :3], 0)
    dist = np.sqrt(((f[..., :3] - bg) ** 2).sum(-1))
    plate = (f[..., 3] < MIN_ALPHA) | (dist < TOL)
    # Gen keyed 00ff00, so the plate kept alpha ~60-95 while the figure is 255: low alpha is plate anywhere (pockets between arm and head too).
    fg = largest_parts(erode(~outside(plate) & (f[..., 3] >= MIN_ALPHA)))
    out = f.copy()
    out[..., 3] = np.where(fg, np.maximum(f[..., 3], 0), 0)
    # Inside the figure (not on its outline) everything is fully opaque: horns, eyes and cloak must not show the floor through.
    inner = erode(erode(fg))
    out[..., 3] = np.where(inner, 255, out[..., 3])
    return out.astype(np.uint8), fg, bg


def bbox(mask):
    ys, xs = np.nonzero(mask)
    return (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1) if len(xs) else None


def main(args):
    srcs = dict(a.split("=", 1) for a in args)
    os.makedirs(OUT, exist_ok=True)
    keyed, report = {}, {}
    for name, path in srcs.items():
        kept = []
        for i, f in enumerate(frames_of(path)):
            if KEEP.get(name) is not None and i not in KEEP[name]:
                continue
            k, fg, bg = key(f)
            b = bbox(fg)
            if b is None or b[1] <= 0 or b[0] <= 0 or b[2] >= CELL:
                print(f"  {name} {i}: dropped (figure touches the frame's edge: {b})", file=sys.stderr)
                continue
            cy, cx = (b[1] + b[3]) // 2, (b[0] + b[2]) // 2
            if k[cy - 5:cy + 5, cx - 5:cx + 5, 3].min() < 255:
                print(f"  {name} {i}: dropped (see-through in the middle)", file=sys.stderr)
                continue
            kept.append((i, Image.fromarray(k, "RGBA"), b))
        keyed[name] = kept
        report[name] = [i for i, _, _ in kept]

    # One scale for every clip: the standing figure (idle's first frame) is HEIGHT_UNITS tall. Frames stay at their own resolution.
    stand = keyed["idle"][0][2]
    stand_h = stand[3] - stand[1]
    ground, center = stand[3], (stand[0] + stand[2]) / 2
    ppu = stand_h / HEIGHT_UNITS
    result = {}
    for name, kept in keyed.items():
        pad = 4
        x0 = max(0, min(b[0] for _, _, b in kept) - pad)
        y0 = max(0, min(b[1] for _, _, b in kept) - pad)
        x1 = min(CELL, max(b[2] for _, _, b in kept) + pad)
        y1 = min(CELL, max(b[3] for _, _, b in kept) + pad)
        fw, fh = x1 - x0, y1 - y0
        cells = [im.crop((x0, y0, x1, y1)) for _, im, _ in kept]
        pivot = [(center - x0) / fw, 1 - (ground - stand_h / 2 - y0) / fh]
        cols = max(1, min(len(cells), 2048 // fw))
        rows = math.ceil(len(cells) / cols)
        sheet = Image.new("RGBA", (cols * fw, rows * fh), (0, 0, 0, 0))
        sprites = []
        for j, cell in enumerate(cells):
            x, y = (j % cols) * fw, (j // cols) * fh
            sheet.paste(cell, (x, y))
            sprites.append({"name": f"{name}_{j:02d}", "x": x, "y": sheet.height - y - fh, "w": fw, "h": fh})
        png = os.path.join(OUT, f"OverlordTaunt_{name}.png")
        sheet.save(png, optimize=True)
        with open(png[:-4] + ".sheet.json", "w", encoding="utf-8", newline="\n") as fp:
            json.dump({"generatedBy": "Tools/Art/overlord_taunt_bake.py", "pixelsPerUnit": round(ppu, 4), "pivot": pivot, "sprites": sprites}, fp, indent=1)
        result[name] = {"frames": report[name], "cell": [fw, fh], "sheet": os.path.relpath(png, ROOT)}
    print(json.dumps({"pixelsPerUnit": round(ppu, 4), "standHeightPx": stand_h, "clips": result}, indent=1))


if __name__ == "__main__":
    main(sys.argv[1:])
