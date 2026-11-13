#!/usr/bin/env python3
"""Pack the twelve Hawkes Gen flipbook strips into Assets/Resources/Effects/FeelVfxAtlas.png.

Each input is a sprite.sheet PNG (a single row of FRAMES cells, cell 256, key 00ff00 already
keyed out) named <kind>.png in the input folder, kinds in FeelVfx.Kind order (rows top to
bottom). Cells are scaled to 128 and the atlas is 1280x1536 (10 columns, 12 rows).
Missing strips stay transparent and are listed (FeelVfxAtlas falls back to stand-ins only
when the whole atlas is missing, so run with --require to refuse a partial pack).

  python3 -I Tools/Art/fx/feel_vfx_pack.py <strips folder> [--out Assets/Resources/Effects] [--require]
"""
import argparse, os, sys
from PIL import Image

KINDS = ["muzzle", "impact", "hit", "deathImp", "deathCinder", "deathSack",
         "diamond", "heroHurt", "heroDeath", "dash", "graze", "rewindRipple"]
FRAMES, CELL = 10, 128

def cells(img, n):
    w, h = img.size
    cw = w // n
    return [img.crop((i * cw, 0, (i + 1) * cw, h)) for i in range(n)]

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("--out", default="Assets/Resources/Effects")
    ap.add_argument("--require", action="store_true")
    a = ap.parse_args()
    atlas = Image.new("RGBA", (FRAMES * CELL, len(KINDS) * CELL), (0, 0, 0, 0))
    missing = []
    for row, kind in enumerate(KINDS):
        p = os.path.join(a.src, kind + ".png")
        if not os.path.exists(p):
            missing.append(kind)
            continue
        strip = Image.open(p).convert("RGBA")
        cs = cells(strip, FRAMES)
        for col, c in enumerate(cs):
            c = c.resize((CELL, CELL), Image.LANCZOS)
            atlas.paste(c, (col * CELL, row * CELL))
    if missing:
        print("missing strips:", ", ".join(missing))
        if a.require:
            sys.exit(1)
    os.makedirs(a.out, exist_ok=True)
    dest = os.path.join(a.out, "FeelVfxAtlas.png")
    atlas.save(dest, optimize=True)
    print("wrote", dest, atlas.size, os.path.getsize(dest) // 1024, "KB")

if __name__ == "__main__":
    main()
