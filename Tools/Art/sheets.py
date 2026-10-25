"""Image side of the art pipeline (Tools/Art/art.mjs calls this; see Docs/ArtPipeline.md).

Reuses Dose Of Destruction's keying and cutout code instead of copying it:
_build_anims.py (background key, speck cleanup, still-end trimming, loop seams)
and tools/cutout.py (BiRefNet masks). DOD_ROOT points at that checkout.

    python sheets.py blank <out.png> <size>
        A flat green canvas: Qwen's starting image when an enemy has no art yet.
    python sheets.py cutout <image> <out.png>
        The figure cut out of a still: flat-backdrop key, BiRefNet when the
        backdrop isn't flat. Prints {"box", "score", "why"} as JSON.
    python sheets.py frame <image> <out.png> <size> <fill> <frame.json>
        The standing still fitted into a size x size frame on flat green, feet
        near the bottom. Writes the framing (scale, ground line, centre) to frame.json.
    python sheets.py frame-like <image> <out.png> <frame.json>
        A pose framed like the standing still: same scale, feet on the same
        ground line, legs on the same centre, so every clip shares one anchor.
    python sheets.py build <job.json>
        Clips -> keyed, feet-aligned sprite sheets + slicing sidecars + previews.
        The job (written by art.mjs) lists the clips, output folder and sizes.
"""

import json
import math
import os
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

DOD_ROOT = Path(os.environ.get("DOD_ROOT", r"C:\Projects\doseofdestructionwebsite"))
sys.path.insert(0, str(DOD_ROOT))
sys.path.insert(0, str(DOD_ROOT / "tools"))
import _build_anims as dod  # noqa: E402  (key, drop_specks, solid_box, still_ends, same_frame, background_color)

GREEN = (0, 255, 0)
GROUND = 0.06  # Feet sit this share of the frame above its bottom edge (as DoD's prep.py frames poses).
MAX_FRAMES = 24
MAX_SHEET = 2048  # WebGL-friendly.


def flat_border(img):
    """How uniform the border is: the share of border pixels close to its median color."""
    bg = dod.background_color(img)
    w, h = img.size
    px = img.load()
    pts = [(x, y) for x in range(0, w, 5) for y in (0, h - 1)] + [(x, y) for y in range(0, h, 5) for x in (0, w - 1)]
    near = sum(1 for p in pts if max(abs(a - b) for a, b in zip(px[p], bg)) < 40)
    return near / len(pts), bg


def cutout(src):
    """RGBA cutout of a still and how it was made."""
    img = Image.open(src).convert("RGB")
    flat, bg = flat_border(img)
    if flat > 0.9:
        return dod.drop_specks(dod.key(img, bg, 40)), "key"
    try:
        import cutout as dod_cutout  # BiRefNet via rembg (DoD's .venv has it)
        import rembg  # noqa: F401
    except ImportError:
        # No rembg here (the Linux server): key by colour anyway; a take on a busy backdrop then scores low.
        return dod.drop_specks(dod.key(img, bg, 40)), "key (no rembg)"

    a = dod_cutout.mask("birefnet", "soi-" + Path(src).stem, Path(src)).resize(img.size, Image.BILINEAR)
    out = img.convert("RGBA")
    out.putalpha(a.point(lambda v: 0 if v < 10 else v))
    return dod.drop_specks(out), "birefnet"


def score(img):
    """0..1: one whole figure, inside the frame, a sensible size. And why not, when lower."""
    box = dod.solid_box(img)
    if not box:
        return 0.0, "no figure found"
    w, h = img.size
    s, why = 1.0, []
    margin = 0.01
    if box[0] <= w * margin or box[2] >= w * (1 - margin) or box[1] <= h * margin or box[3] >= h * (1 - margin):
        s -= 0.5
        why.append("touches the frame edge (cut off)")
    area = (box[2] - box[0]) * (box[3] - box[1]) / (w * h)
    if area < 0.03:
        s -= 0.4
        why.append("figure is tiny")
    if area > 0.8:
        s -= 0.3
        why.append("figure fills the frame (background not keyed?)")
    return max(0.0, s), ", ".join(why) or "ok"


def legs_center(img, box):
    """Horizontal centre of the lowest quarter of the figure: where it stands, whatever its arms do."""
    a = img.getchannel("A").point(lambda v: 255 if v > 64 else 0)
    low = a.crop((box[0], box[3] - max(1, (box[3] - box[1]) // 4), box[2], box[3]))
    lb = low.getbbox()
    return box[0] + ((lb[0] + lb[2]) / 2 if lb else (box[2] - box[0]) / 2)


def place(cut, scale, size, ground_y, center_x, out):
    box = dod.solid_box(cut)
    fig = cut.crop(box)
    fig = fig.resize((max(1, round(fig.width * scale)), max(1, round(fig.height * scale))), Image.LANCZOS)
    lc = (legs_center(cut, box) - box[0]) * scale
    canvas = Image.new("RGBA", (size, size), (*GREEN, 255))
    canvas.alpha_composite(fig, (round(center_x - lc), round(ground_y - fig.height)))
    canvas.convert("RGB").save(out)


def cmd_blank(out, size):
    Image.new("RGB", (int(size), int(size)), GREEN).save(out)


def cmd_cutout(src, out):
    cut, how = cutout(src)
    cut.save(out)
    s, why = score(cut)
    print(json.dumps({"box": dod.solid_box(cut), "score": s, "why": why, "how": how}))


def cmd_frame(src, out, size, fill, frame_json):
    size, fill = int(size), float(fill)
    cut, _ = cutout(src)
    box = dod.solid_box(cut)
    if not box:
        sys.exit(f"{src}: no figure found")
    h = box[3] - box[1]
    scale = min(size * fill / h, size * 0.9 / (box[2] - box[0]))
    ground = size * (1 - GROUND)
    place(cut, scale, size, ground, size / 2, out)
    frame = {"size": size, "scale": scale, "ground": ground, "center": size / 2, "standHeight": h * scale, "source": str(src)}
    Path(frame_json).write_text(json.dumps(frame, indent=1), encoding="utf-8")
    print(json.dumps(frame))


def cmd_frame_like(src, out, frame_json):
    f = json.loads(Path(frame_json).read_text(encoding="utf-8"))
    cut, _ = cutout(src)
    if not dod.solid_box(cut):
        sys.exit(f"{src}: no figure found")
    place(cut, f["scale"], f["size"], f["ground"], f["center"], out)
    print(json.dumps({"ok": True}))


# ------------------------------------------------------------------ sheets


def extract(video, fps, speed, start, end, tmp):
    args = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
    if start is not None:
        args += ["-ss", str(start)]
    if end is not None:
        args += ["-to", str(end)]
    retime = f"setpts=PTS/{speed}," if speed != 1 else ""
    args += ["-i", str(video), "-vf", f"{retime}fps={fps}", str(tmp / "%04d.png")]
    subprocess.run(args, check=True)
    return [Image.open(p).convert("RGB") for p in sorted(tmp.glob("*.png"))]


def kill_green(img, bg, c):
    """Wan sometimes paints a shadow or smudge of the green backdrop inside the figure, out of the key's reach:
    clear strongly green pixels. Only with a green backdrop; "greenKill": false keeps them (a green creature)."""
    if not c.get("greenKill", True) or not (bg[1] > bg[0] + 40 and bg[1] > bg[2] + 40):
        return img
    r, g, b, a = img.split()
    lead = ImageChops.subtract(g, ImageChops.lighter(r, b))  # how far green leads the other channels
    green = lead.point(lambda v: 0 if v > 28 else 255)
    # A shadow on green is near-black with green just ahead: dark and green-leaning goes too.
    dark = ImageChops.lighter(ImageChops.lighter(r, g), b).point(lambda v: 255 if v < 90 else 0)
    shadow = ImageChops.multiply(dark, lead.point(lambda v: 255 if v > 6 else 0))
    keep = ImageChops.multiply(green, ImageChops.invert(shadow))
    # Despill: a flash, fire or glow painted over the green plate keeps a green cast (yellow-green blobs). Green never
    # leads the brighter of red and blue in the kept figure, so that cast becomes the warm colour it was meant to be.
    g = ImageChops.darker(g, ImageChops.lighter(r, b))
    return Image.merge("RGBA", (r, g, b, ImageChops.multiply(a, keep)))


def hit_frame(keyed):
    """The frame whose silhouette is furthest from the first: full extension (DoD's rule)."""
    first = keyed[0].getchannel("A").resize((64, 64))
    reach = [sum(ImageChops.difference(first, k.getchannel("A").resize((64, 64))).histogram()[128:]) for k in keyed]
    return max(range(len(reach)), key=lambda i: reach[i])


def build_clip(name, c, frame, ref_height):
    loop = c.get("loop", False)
    fps = c.get("fps", 12)
    speed = max(0.25, min(4.0, float(c.get("speed", 1))))
    with tempfile.TemporaryDirectory() as tmp:
        frames = extract(c["video"], fps, speed, c.get("start"), c.get("end"), Path(tmp))
    if not frames:
        raise SystemExit(f"{c['video']}: no frames")
    # Videos not framed by this pipeline (a hand-made mp4) are scaled to the frame size first.
    size = frame["size"]
    if frames[0].size != (size, size):
        frames = [f.resize((round(f.width * size / f.height), size), Image.LANCZOS) for f in frames]
    trimmed = None
    if not loop and c.get("start") is None and c.get("end") is None and c.get("autotrim", True):
        a, b = dod.still_ends(frames)
        if a > 0 or b < len(frames) - 1:
            trimmed = [round(a / fps, 2), round(b / fps, 2)]
            frames = frames[a:b + 1]
    if loop and len(frames) > 2 and dod.same_frame(frames[0], frames[-1]):
        frames = frames[:-1]
    if len(frames) > MAX_FRAMES:
        step = len(frames) / MAX_FRAMES
        frames = [frames[int(i * step)] for i in range(MAX_FRAMES)]

    bg = dod.background_color(frames[0])
    keyed = [dod.drop_specks(kill_green(dod.key(f, bg, c.get("tolerance", 40)), bg, c)) for f in frames]
    if c.get("flip"):
        keyed = [k.transpose(Image.FLIP_LEFT_RIGHT) for k in keyed]
    boxes = [dod.solid_box(k) for k in keyed]
    real = [b for b in boxes if b]
    if not real:
        raise SystemExit(f"{c['video']}: keying removed everything; lower its tolerance")
    if c.get("measure"):
        # A hand-made video wasn't framed from the poses: its own first frame is the standing figure.
        b = real[0]
        frame = {**frame, "standHeight": b[3] - b[1], "ground": b[3], "center": legs_center(keyed[0], b)}

    # Every clip was made from poses framed the same way, so one scale and one anchor fit them all:
    # the standing figure is ref_height pixels tall, and the pivot is the middle of the standing body
    # above the shared ground point. The figure can't drift between states.
    scale = ref_height / frame["standHeight"] * float(c.get("size", 1))
    cx, cy = frame["center"], frame["ground"] - frame["standHeight"] / 2
    pad = 4
    w0, h0 = keyed[0].size
    ux0 = max(0, min(b[0] for b in real) - pad)
    uy0 = max(0, min(b[1] for b in real) - pad)
    ux1 = min(w0, max(b[2] for b in real) + pad)
    uy1 = min(h0, max(b[3] for b in real) + pad)
    fw, fh = max(1, round((ux1 - ux0) * scale)), max(1, round((uy1 - uy0) * scale))
    cells = [k.crop((ux0, uy0, ux1, uy1)).resize((fw, fh), Image.LANCZOS) for k in keyed]
    pivot = [(cx - ux0) * scale / fw, 1 - (cy - uy0) * scale / fh]

    cols = max(1, min(len(cells), MAX_SHEET // fw))
    rows = math.ceil(len(cells) / cols)
    if rows * fh > MAX_SHEET:
        raise SystemExit(f"{name}: {len(cells)} frames of {fw}x{fh} don't fit a {MAX_SHEET} sheet; lower fps or the pixel height")
    sheet = Image.new("RGBA", (cols * fw, rows * fh), (0, 0, 0, 0))
    sprites = []
    for i, cell in enumerate(cells):
        x, y = (i % cols) * fw, (i // cols) * fh
        sheet.paste(cell, (x, y))
        # Unity rects count from the bottom-left.
        sprites.append({"name": f"{name}_{i:02d}", "x": x, "y": sheet.height - y - fh, "w": fw, "h": fh})
    hit = c.get("hit")
    if hit is None and c.get("event") and len(keyed) > 2:
        hit = hit_frame(keyed)
    meta = {"clip": name, "frames": len(cells), "fps": fps, "loop": loop, "cell": [fw, fh], "pivot": pivot,
            "hit": hit, "autoTrim": trimmed, "source": str(c["video"])}
    return sheet, sprites, meta, cells


def preview(entity, metas, cells_by_clip, pivots, out_dir):
    """A contact sheet (every frame, pivot marked) and one looping gif per clip."""
    # One scale for every row, so clips compare at their real sizes.
    h = 120
    k = h / max(cells[0].height for cells in cells_by_clip.values())
    rows = []
    for name, cells in cells_by_clip.items():
        thumbs = [c.resize((max(1, round(c.width * k)), max(1, round(c.height * k)))) for c in cells]
        w = sum(t.width + 4 for t in thumbs) + 8
        card = Image.new("RGBA", (max(w, 200), h + 24), (58, 58, 64, 255))
        d = ImageDraw.Draw(card)
        m = metas[name]
        d.text((6, 4), f"{name}: {m['frames']} frames @ {m['fps']} fps{' loop' if m['loop'] else ''}"
               f"{'  hit ' + str(m['hit']) if m['hit'] is not None else ''}", fill=(255, 220, 120, 255))
        x = 4
        px, py = pivots[name]
        for i, t in enumerate(thumbs):
            top = 22 + h - t.height
            card.alpha_composite(t, (x, top))
            cx, cy = x + px * t.width, top + (1 - py) * t.height
            d.line((cx - 4, cy, cx + 4, cy), fill=(255, 60, 60, 255))
            d.line((cx, cy - 4, cx, cy + 4), fill=(255, 60, 60, 255))
            if m["hit"] == i:
                d.rectangle((x, top, x + t.width - 1, top + t.height - 1), outline=(255, 220, 120, 255))
            x += t.width + 4
        rows.append(card)
        frames = []
        for c in cells:
            f = Image.new("RGBA", c.size, (58, 58, 64, 255))
            f.alpha_composite(c)
            frames.append(f.convert("P", palette=Image.ADAPTIVE))
        frames[0].save(out_dir / f"preview-{name}.gif", save_all=True, append_images=frames[1:],
                       duration=round(1000 / m["fps"]), loop=0, disposal=2)
    width = max(r.width for r in rows)
    sheet = Image.new("RGBA", (width, sum(r.height + 4 for r in rows) + 24), (34, 34, 38, 255))
    ImageDraw.Draw(sheet).text((6, 6), f"{entity}  (red cross = pivot, gold box = attack hit frame)", fill=(230, 230, 230, 255))
    y = 24
    for r in rows:
        sheet.alpha_composite(r, (0, y))
        y += r.height + 4
    sheet.save(out_dir / "preview.png")


def cmd_build(job_path):
    job = json.loads(Path(job_path).read_text(encoding="utf-8"))
    frame = json.loads(Path(job["frame"]).read_text(encoding="utf-8"))
    out = Path(job["out"])
    out.mkdir(parents=True, exist_ok=True)
    preview_dir = Path(job["previewDir"])
    preview_dir.mkdir(parents=True, exist_ok=True)
    result, cells_by_clip, metas, pivots = {}, {}, {}, {}
    for name, c in job["clips"].items():
        sheet, sprites, meta, cells = build_clip(name, c, frame, job["refHeight"])
        png = out / f"{job['prefix']}_{name}.png"
        sheet.save(png, optimize=True)
        sidecar = {"generatedBy": "Tools/Art/art.mjs", "pixelsPerUnit": job["pixelsPerUnit"],
                   "pivot": meta["pivot"], "sprites": sprites}
        png.with_suffix(".sheet.json").write_text(json.dumps(sidecar, indent=1), encoding="utf-8")
        meta["sheet"] = png.name
        result[name] = meta
        cells_by_clip[name], metas[name], pivots[name] = cells, meta, meta["pivot"]
        print(f"  {name:8} {meta['frames']:2} frames  {meta['cell'][0]}x{meta['cell'][1]}"
              f"{'  hit ' + str(meta['hit']) if meta['hit'] is not None else ''}", file=sys.stderr)
    preview(job["entity"], metas, cells_by_clip, pivots, preview_dir)
    print(json.dumps(result))


def main():
    cmd, args = sys.argv[1], sys.argv[2:]
    {"blank": cmd_blank, "cutout": cmd_cutout, "frame": cmd_frame, "frame-like": cmd_frame_like, "build": cmd_build}[cmd](*args)


if __name__ == "__main__":
    main()
