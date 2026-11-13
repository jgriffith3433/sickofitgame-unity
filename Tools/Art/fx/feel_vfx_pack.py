#!/usr/bin/env python3
"""Pack the twelve Hawkes Gen flipbook strips into Assets/Resources/Effects/FeelVfxAtlas.png.

Each input is a sprite.sheet PNG named <kind>.png (one row of 256 px cells, the background already
keyed out; only the first FRAMES cells are used, so a strip may be longer: sprite.sheet spreads its
frames over the whole clip, and a burst that is over by frame 6 is cut with more frames and the first 10
taken). Kinds are in FeelVfx.Kind order (rows top to bottom); cells are scaled to 128, so the atlas is
1280x1536 (10 columns, 12 rows). Only ffmpeg is needed (no Pillow).

Two touch-ups, both per kind below: FADES multiplies a cell's alpha (a clip that ends holding its
subject, like the diamond gem, is faded out so no stale picture is left on screen) and RECOLOR sets every
visible pixel to one colour (the rewind ripple is dark ink on dark ground until it is made light; the
runtime tint then colours it).

  python3 -I Tools/Art/fx/feel_vfx_pack.py <strips folder> [--out Assets/Resources/Effects] [--require]
"""
import argparse, os, shutil, subprocess, sys, tempfile

KINDS = ["muzzle", "impact", "hit", "deathImp", "deathCinder", "deathSack",
         "diamond", "heroHurt", "heroDeath", "dash", "graze", "rewindRipple"]
FRAMES, CELL, SRC_CELL = 10, 128, 256

FADES = {
    "impact": [1, 1, 1, 1, 1, 1, 1, 1, 0.7, 0.4],
    "diamond": [1, 1, 1, 1, 1, 1, 0.8, 0.55, 0.3, 0.1],
    "dash": [1, 1, 1, 1, 1, 1, 1, 0.8, 0.5, 0.2],
}
RECOLOR = {"rewindRipple": (235, 255, 255)}


def row_filter(kind):
    fades = FADES.get(kind, [1] * FRAMES)
    parts = ["[0:v]format=rgba,split=%d%s" % (FRAMES, "".join("[a%d]" % i for i in range(FRAMES)))]
    for i in range(FRAMES):
        f = "[a%d]crop=%d:%d:%d:0,scale=%d:%d:flags=lanczos" % (i, SRC_CELL, SRC_CELL, i * SRC_CELL, CELL, CELL)
        if kind in RECOLOR:
            r, g, b = RECOLOR[kind]
            f += ",lutrgb=r=%d:g=%d:b=%d" % (r, g, b)
        if fades[i] != 1:
            f += ",colorchannelmixer=aa=%.3f" % fades[i]
        parts.append(f + "[c%d]" % i)
    parts.append("%shstack=inputs=%d" % ("".join("[c%d]" % i for i in range(FRAMES)), FRAMES))
    return ";".join(parts)


def ffmpeg(*args):
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", *args], check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("--out", default="Assets/Resources/Effects")
    ap.add_argument("--require", action="store_true")
    a = ap.parse_args()
    if shutil.which("ffmpeg") is None:
        sys.exit("ffmpeg is required")
    tmp = tempfile.mkdtemp(prefix="feelvfx_")
    rows, missing = [], []
    for kind in KINDS:
        p = os.path.join(a.src, kind + ".png")
        out = os.path.join(tmp, kind + ".png")
        if os.path.exists(p):
            ffmpeg("-i", p, "-filter_complex", row_filter(kind), "-frames:v", "1", "-pix_fmt", "rgba", out)
        else:
            missing.append(kind)
            ffmpeg("-f", "lavfi", "-i", "color=c=black@0:s=%dx%d,format=rgba" % (FRAMES * CELL, CELL),
                   "-frames:v", "1", "-pix_fmt", "rgba", out)
        rows.append(out)
    if missing:
        print("missing strips:", ", ".join(missing))
        if a.require:
            sys.exit(1)
    os.makedirs(a.out, exist_ok=True)
    dest = os.path.join(a.out, "FeelVfxAtlas.png")
    inputs = []
    for r in rows:
        inputs += ["-i", r]
    ffmpeg(*inputs, "-filter_complex", "vstack=inputs=%d" % len(rows), "-frames:v", "1", "-pix_fmt", "rgba", dest)
    shutil.rmtree(tmp, ignore_errors=True)
    print("wrote", dest, "%dx%d" % (FRAMES * CELL, len(KINDS) * CELL), os.path.getsize(dest) // 1024, "KB")


if __name__ == "__main__":
    main()
