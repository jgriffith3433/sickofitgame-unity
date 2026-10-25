"""Bakes a hero clip (mp4 on a black plate) into a sliced sprite sheet, without Unity.

The same result as CharacterSpriteBaker (SickOfIt > Regenerate Player Sprites): black plate keyed out from the frame
edges (clearCutoff 28) with edge colour bled into the cleared pixels, frames packed left to right, top row first,
and a .png.meta with one sprite per frame named <State>_0000... (centre pivot, 100 pixels per unit).

Cells are always 604x340, the size of the hero's existing sheets (Idle, Walk, ...: 72 frames each). Sprite size is
world size, so a clip framed like the Idle video must use the same cell or the hero changes size. (The Unity baker
picks the cell from the frame count: re-baking a shorter clip there makes the hero bigger.)

    python player_clip_sheet.py <clip.mp4> <State> <Assets/Art/Sprites/Player> [--fps 16] [--start S] [--end S]
        [--template Assets/Art/Sprites/Player/Idle.png.meta] [--cell 604x340] [--ppu 100]

    python player_clip_sheet.py --idle-reference <Assets/Art/Sprites/Player/Idle.png> <out.png>
        writes Idle's first frame (one 604x340 cell) as a 1280x720 still on black: the reference image for hero poses (art.mjs hero --hero-ref idle).

Cells default to 604x340, centre pivot, 100 pixels per unit (CELL_W/CELL_H/PPU); --cell and --ppu exist only to
read them back in tests. Needs ffmpeg, Pillow and numpy. A sheet that already has a .meta keeps its guid.
"""
import argparse
import os
import random
import re
import struct
import subprocess
import sys
import tempfile
import uuid
from collections import deque

import numpy as np
from PIL import Image

CELL_W, CELL_H = 604, 340
CLEAR_CUTOFF = 28
MAX_SHEET = 4096
PPU = 100


def extract(mp4, fps, start, end, folder):
    cmd = ["ffmpeg", "-v", "error", "-y"]
    if start:
        cmd += ["-ss", str(start)]
    cmd += ["-i", mp4]
    if end:
        cmd += ["-t", str(end - (start or 0))]
    cmd += ["-vf", f"fps={fps}", "-start_number", "0", os.path.join(folder, "frame_%04d.png")]
    subprocess.run(cmd, check=True)
    return sorted(os.path.join(folder, f) for f in os.listdir(folder) if f.startswith("frame_"))


def key_black(rgb):
    """Alpha 0 for near-black pixels connected to the frame edge; colour bled into them (no dark fringe)."""
    h, w, _ = rgb.shape
    dark = (rgb <= CLEAR_CUTOFF).all(axis=2)
    cleared = np.zeros((h, w), bool)
    queue = deque()
    for x in range(w):
        for y in (0, h - 1):
            if dark[y, x] and not cleared[y, x]:
                cleared[y, x] = True
                queue.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if dark[y, x] and not cleared[y, x]:
                cleared[y, x] = True
                queue.append((y, x))
    while queue:
        y, x = queue.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and dark[ny, nx] and not cleared[ny, nx]:
                cleared[ny, nx] = True
                queue.append((ny, nx))

    out = rgb.astype(np.float32)
    has = ~cleared
    # Bleed: grow the kept colour into cleared pixels a few rings deep (enough for bilinear scaling).
    for _ in range(4):
        grow = np.zeros_like(out)
        count = np.zeros((h, w), np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            src_has = np.roll(has, (dy, dx), axis=(0, 1))
            src = np.roll(out, (dy, dx), axis=(0, 1))
            grow += src * src_has[..., None]
            count += src_has
        fill = ~has & (count > 0)
        out[fill] = grow[fill] / count[fill][:, None]
        has = has | fill
    alpha = np.where(cleared, 0, 255).astype(np.uint8)
    return np.dstack([np.clip(out, 0, 255).astype(np.uint8), alpha])


def internal_id(rng):
    return rng.randint(-(2 ** 63), 2 ** 63 - 1)


def sprite_id(iid):
    b = struct.pack("<q", iid).hex()
    return "".join(b[i + 1] + b[i] for i in range(0, 16, 2)) + "0800000000000000"


def write_meta(template_path, meta_path, state, rects):
    text = open(template_path, encoding="utf-8").read()
    guid = None
    if os.path.exists(meta_path):
        m = re.search(r"^guid: (\w+)", open(meta_path, encoding="utf-8").read(), re.M)
        guid = m.group(1) if m else None
    guid = guid or uuid.uuid4().hex
    text = re.sub(r"^guid: \w+", f"guid: {guid}", text, count=1, flags=re.M)
    text = re.sub(r"^  spritePixelsToUnits: [\d.]+", f"  spritePixelsToUnits: {PPU}", text, count=1, flags=re.M)

    rng = random.Random(f"{state}:{guid}")
    ids = [internal_id(rng) for _ in rects]
    sprites = []
    for i, (x, y) in enumerate(rects):
        sprites.append(
            f"""    - serializedVersion: 2
      name: {state}_{i:04d}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
        width: {CELL_W}
        height: {CELL_H}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {sprite_id(ids[i])}
      internalID: {ids[i]}
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
    table = "".join(f"      {state}_{i:04d}: {ids[i]}\n" for i in range(len(rects)))
    text = text[:start] + table + text[end:]
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def idle_reference(sheet, out):
    """Idle's first frame on a black 1280x720 plate (what the hero's videos are made at), the look every pose starts from."""
    cell = Image.open(sheet).convert("RGBA").crop((0, 0, CELL_W, CELL_H)).resize((1280, 720), Image.LANCZOS)
    plate = Image.new("RGB", (1280, 720), (0, 0, 0))
    plate.paste(cell, (0, 0), cell)
    plate.save(out)
    print(f"{out}: 1280x720 from the first {CELL_W}x{CELL_H} cell of {sheet}")


def main():
    global CELL_W, CELL_H, PPU
    if len(sys.argv) == 4 and sys.argv[1] == "--idle-reference":
        return idle_reference(sys.argv[2], sys.argv[3])
    ap = argparse.ArgumentParser()
    ap.add_argument("mp4")
    ap.add_argument("state")
    ap.add_argument("folder")
    ap.add_argument("--cell", default=f"{CELL_W}x{CELL_H}")
    ap.add_argument("--ppu", type=int, default=PPU)
    ap.add_argument("--fps", type=float, default=16)
    ap.add_argument("--start", type=float, default=0)
    ap.add_argument("--end", type=float, default=0)
    ap.add_argument("--template", default=None)
    args = ap.parse_args()

    CELL_W, CELL_H = (int(v) for v in args.cell.split("x"))
    PPU = args.ppu
    template = args.template or os.path.join(args.folder, "Idle.png.meta")
    with tempfile.TemporaryDirectory() as tmp:
        frames = extract(args.mp4, args.fps, args.start, args.end, tmp)
        if not frames:
            sys.exit("ffmpeg wrote no frames")
        cols = min(len(frames), MAX_SHEET // CELL_W)
        rows = (len(frames) + cols - 1) // cols
        if rows * CELL_H > MAX_SHEET:
            sys.exit(f"{len(frames)} frames don't fit a {MAX_SHEET} sheet; lower --fps")
        tex_w, tex_h = cols * CELL_W, rows * CELL_H
        sheet = Image.new("RGBA", (tex_w, tex_h), (0, 0, 0, 0))
        rects = []
        for i, path in enumerate(frames):
            rgba = key_black(np.asarray(Image.open(path).convert("RGB")))
            cell = Image.fromarray(rgba, "RGBA").resize((CELL_W, CELL_H), Image.BILINEAR)
            col, row = i % cols, i // cols
            sheet.paste(cell, (col * CELL_W, row * CELL_H))
            # Unity rects start at the bottom left.
            rects.append((col * CELL_W, tex_h - (row + 1) * CELL_H))
        png = os.path.join(args.folder, args.state + ".png")
        sheet.save(png, optimize=True)
        write_meta(template, png + ".meta", args.state, rects)
        print(f"{args.state}: {len(frames)} frames, {cols}x{rows} cells of {CELL_W}x{CELL_H} -> {png} ({tex_w}x{tex_h})")


if __name__ == "__main__":
    main()
