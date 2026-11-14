#!/usr/bin/env python3
"""Sidecars for the tilesets and story props (Docs/LevelDesign.md "Tilesets and story props").

Every PNG under Assets/Art/Tiles/<set>/ and Assets/Art/Props/<set>/ gets a `.sheet.json` beside it
(Assets/Scripts/Tools/Editor/Generation/GeneratedSheetImporter.cs slices the sheet from it):
  * a prop's cell size is its `_idle.png` size; `_react.png` and `_done.png` are grids of that cell (row-major,
    empty cells are skipped), `brazier_idle` is a 4-frame loop;
  * a tile keeps the Ledge piece's numbers (pixelsPerUnit 202.236, pivot 0.500601/0.691185) so a variant lines up
    with the body exactly as the old Ledge did; the Meadow's sod strips hang from the top edge of the ground.
The pixels-per-unit of a prop is its world height (table PROP_HEIGHT, in world units) over its idle height, so the
table is the only place a size is chosen. `--check` verifies every sidecar is current (no Unity needed).

  python3 Tools/Art/story_art.py          write sidecars
  python3 Tools/Art/story_art.py --check  fail when a sidecar is missing or stale
"""
import json
import os
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROPS = os.path.join(ROOT, "Assets", "Art", "Props")
TILES = os.path.join(ROOT, "Assets", "Art", "Tiles")

LEDGE_PPU = 202.236
LEDGE_PIVOT = [0.500601, 0.691185]
SOD_PPU = 202.0          # a 404 px strip is 2.0 world units wide: one body-width of the Meadow's ground
SOD_PIVOT = [0.5, 0.94]  # the grass lip sits on the ground's top edge

# The base of each prop is its pivot: the sheet's pivot y is the first frame's empty rows under it.
# World height of each prop's idle frame (units; the hero is about 1.3 tall, a ledge is 2 wide).
PROP_HEIGHT = {
    # Meadow
    "hen": 0.55, "mill": 3.1, "cart": 0.95, "hayBale": 0.75, "fenceA": 0.85, "well": 1.05, "scarecrow": 1.45, "roosterPerch": 1.1,
    # Floor 1000
    "welcomeDesk": 1.0, "numberDispenser": 1.0, "floorSign": 1.1, "cageStanding": 1.35, "gibbet": 1.7, "hatShelf": 1.2,
    "luggage": 0.8, "queueRope": 0.8, "serviceBell": 0.45,
    # Floor 999
    "furnacePipes": 1.9, "gaugePost": 1.75, "gaugePipe": 1.1, "valveWheel": 0.95, "steamVent": 1.6, "anvil": 0.75,
    "coalBucket": 0.6, "furnaceDoor": 1.5, "tallyWall": 1.4,
    # Floor 998
    "bonePile": 0.95, "boneHeap": 0.5, "boneChime": 1.35, "brokenPew": 0.75, "candleStubs": 0.5, "choirLoft": 1.9,
    "hymnBoard": 1.4, "pulpit": 1.2, "stoneFont": 0.85,
    # Floor 997
    "bannerTall": 2.0, "bannerShort": 1.5, "bannerStand": 1.65, "ovlPortrait": 1.55, "brazier": 1.15, "halberds": 1.45,
    "pedestal": 0.95, "throneCrate": 1.2, "wallHook": 1.3,
}


def read_png(path):
    """(width, height, rows of RGBA bytes) of an 8-bit RGBA/RGB/gray+alpha PNG."""
    with open(path, "rb") as f:
        data = f.read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", path
    pos, idat, w = 8, b"", 0
    while pos < len(data):
        n, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        if kind == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
            assert depth == 8 and interlace == 0 and ctype in (2, 4, 6), f"{path}: unsupported PNG ({depth}, {ctype}, {interlace})"
            bpp = {2: 3, 4: 2, 6: 4}[ctype]
        elif kind == b"IDAT":
            idat += body
        pos += 12 + n
    raw = zlib.decompress(idat)
    stride = w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        ft = raw[i]
        line = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if ft == 1:
                line[x] = (line[x] + a) & 255
            elif ft == 2:
                line[x] = (line[x] + b) & 255
            elif ft == 3:
                line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif ft == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        rows.append(line)
        prev = line
    return w, h, rows, bpp


def cell_filled(rows, bpp, x0, y0, cw, ch):
    """True when the cell has any visible pixel (alpha above 8)."""
    if bpp == 3:
        return True
    off = bpp - 1
    for y in range(y0, y0 + ch):
        line = rows[y]
        for x in range(x0, x0 + cw):
            if line[x * bpp + off] > 8:
                return True
    return False


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    return struct.unpack(">II", head[16:24])


def sidecar_for(path):
    """The sidecar dict for one PNG, or None when it is not one of ours."""
    rel = os.path.relpath(path, os.path.join(ROOT, "Assets", "Art")).replace(os.sep, "/")
    kind, group, name = rel.split("/")
    stem = name[:-4]
    if kind == "Tiles":
        if group == "Meadow":
            return {"pixelsPerUnit": SOD_PPU, "pivot": SOD_PIVOT, "sprites": [{"name": stem + "_00", "x": 0, "y": 0, "w": png_size(path)[0], "h": png_size(path)[1]}]}
        w, h = png_size(path)
        return {"pixelsPerUnit": LEDGE_PPU, "pivot": LEDGE_PIVOT, "sprites": [{"name": stem + "_00", "x": 0, "y": 0, "w": w, "h": h}]}
    prop, _, clip = stem.partition("_")
    iw, ih = png_size(os.path.join(os.path.dirname(path), prop + "_idle.png"))
    w, h = png_size(path)
    cw = 250 if (prop == "brazier" and clip == "idle") else iw
    if prop == "brazier":
        cw = 250
    cols, rows_n = w // cw, h // ih
    assert cols * cw == w and rows_n * ih == h, f"{path}: {w}x{h} is not a grid of {cw}x{ih}"
    _, _, rows, bpp = read_png(path)
    sprites = []
    n = 0
    for r in range(rows_n):
        for c in range(cols):
            if cell_filled(rows, bpp, c * cw, r * ih, cw, ih):
                # PNG rows run top to bottom, Unity rects from the bottom.
                sprites.append({"name": f"{clip}_{n:02d}", "x": c * cw, "y": h - (r + 1) * ih, "w": cw, "h": ih})
                n += 1
    ppu = round(ih / PROP_HEIGHT[prop], 3)
    # The pivot is the lowest visible row of the sheet's first frame, so what he sees touches the entity's place
    # (the art keeps 2-5 px of empty rows under its feet).
    first = sprites[0]
    pad = 0
    top_row = h - first["y"] - ih
    for y in range(top_row + ih - 1, top_row - 1, -1):
        if any(rows[y][x * bpp + bpp - 1] > 8 for x in range(first["x"], first["x"] + cw)) or bpp == 3:
            pad = top_row + ih - 1 - y
            break
    return {"pixelsPerUnit": ppu, "pivot": [0.5, round(pad / ih, 6)], "sprites": sprites}


def all_pngs():
    for base in (TILES, PROPS):
        for dirpath, _, files in sorted(os.walk(base)):
            for f in sorted(files):
                if f.endswith(".png"):
                    yield os.path.join(dirpath, f)


def main():
    check = "--check" in sys.argv
    bad = 0
    for png in all_pngs():
        want = json.dumps(sidecar_for(png), indent=1) + "\n"
        side = png[:-4] + ".sheet.json"
        have = open(side).read() if os.path.exists(side) else None
        if have == want:
            continue
        if check:
            print("stale or missing:", os.path.relpath(side, ROOT))
            bad += 1
        else:
            with open(side, "w", newline="\n") as f:
                f.write(want)
    if check:
        print("story_art: OK" if not bad else f"story_art: {bad} sidecars out of date")
        sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
