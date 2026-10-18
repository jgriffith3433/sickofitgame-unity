"""Shots 5-6: the village after the Overlord (ash, embers) and the title card.
Writes raw RGB 1920x1080 frames to stdout; pipe into ffmpeg.
Usage: python render_end.py <after.png> > raw   (11 s at 30 fps: teaser 34.0-45.0)"""
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

W, H, FPS, SECONDS = 1920, 1080, 30, 11.0
FONT = '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf'
# theme.json "title" style: gradient #FFF0A0 -> #FF7A3D, outline #1E1220, shadow #1E1220E6 offset [10,-12]
TOP, BOT, OUTLINE = (255, 240, 160), (255, 122, 61), (30, 18, 32)
TITLE_AT, SUB_AT, URL_AT, FADE_AT = 4.2, 5.6, 6.8, 10.0   # seconds into this shot (teaser 34.0-45.0)

after = Image.open(sys.argv[1]).convert('RGB')
plate = after.resize((int(W * 1.16), int(H * 1.16)), Image.LANCZOS)
rng = np.random.default_rng(7)


def ease(p):
    p = min(max(p, 0.0), 1.0)
    return p * p * (3 - 2 * p)


def text_layer(text, size, track=0, outline=0.3, shadow=(10, 12), colors=None):
    """The game's title look as an RGBA image: gradient fill, thick dark outline, hard drop shadow."""
    font = ImageFont.truetype(FONT, size)
    widths = [font.getbbox(c)[2] for c in text]
    tw = sum(widths) + track * (len(text) - 1)
    ow = max(2, int(size * outline * 0.25))
    pad = ow + max(shadow) + 8
    asc, desc = font.getmetrics()
    w, h = tw + 2 * pad, asc + desc + 2 * pad
    mask = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(mask)
    x = pad
    for c, cw in zip(text, widths):
        d.text((x, pad), c, font=font, fill=255)
        x += cw + track
    stroke = mask.filter(ImageFilter.MaxFilter(2 * ow + 1))
    top_c, bot_c = colors or (TOP, BOT)
    _, gy0, _, gy1 = mask.getbbox()
    grad = np.zeros((h, w, 3), np.float32)
    t = np.clip((np.arange(h) - gy0) / max(1, gy1 - gy0), 0, 1)[:, None]
    for i in range(3):
        grad[..., i] = top_c[i] + (bot_c[i] - top_c[i]) * t
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    sh = Image.new('RGBA', (w, h), OUTLINE + (0,))
    sh.putalpha(stroke.point(lambda v: int(v * 0.9)))
    out.alpha_composite(sh, (shadow[0], shadow[1]))
    ol = Image.new('RGBA', (w, h), OUTLINE + (255,))
    ol.putalpha(stroke)
    out.alpha_composite(ol)
    fill = Image.fromarray(grad.astype(np.uint8)).convert('RGBA')
    fill.putalpha(mask)
    out.alpha_composite(fill)
    return out


title = text_layer('SICK OF IT', 290, track=8)
sub = text_layer('1,000 floors of Hell.', 76, outline=0.25, shadow=(5, 6), colors=((255, 248, 220), (255, 214, 150)))
url = text_layer('sickofitgame.com', 54, outline=0.25, shadow=(4, 5), colors=((255, 230, 109), (255, 190, 90)))
yy_, xx_ = np.mgrid[0:H, 0:W]
VIGNETTE = (1 - 0.55 * (((xx_ - W / 2) / (W / 2)) ** 2 + ((yy_ - H / 2) / (H / 2)) ** 2) / 2)[..., None].astype(np.float32)
glow = title.getchannel('A').filter(ImageFilter.GaussianBlur(40))

# particles: ash (dark flakes falling) and embers (orange sparks rising), pixel squares like the art
N_ASH, N_EMB = 220, 120
ash = np.column_stack([rng.uniform(0, W, N_ASH), rng.uniform(-H, H, N_ASH), rng.uniform(25, 70, N_ASH),
                       rng.uniform(0, 6.28, N_ASH), rng.integers(3, 8, N_ASH), rng.uniform(0.4, 1.0, N_ASH)])
emb = np.column_stack([rng.uniform(0, W, N_EMB), rng.uniform(0, 2 * H, N_EMB), rng.uniform(60, 160, N_EMB),
                       rng.uniform(0, 6.28, N_EMB), rng.integers(3, 7, N_EMB), rng.uniform(0.5, 1.0, N_EMB)])


def draw_particles(img, t, strength):
    d = ImageDraw.Draw(img, 'RGBA')
    for x, y, v, ph, s, a in ash:
        yy = (y + v * t) % (H + 40) - 20
        xx = (x + 30 * math.sin(t * 0.9 + ph) + 12 * t) % W
        g = int(40 + 50 * a)
        d.rectangle([xx, yy, xx + s, yy + s], fill=(g, g - 4, g - 6, int(220 * a * strength)))
    for x, y, v, ph, s, a in emb:
        yy = H - ((y + v * t) % (H + 60)) + 30
        xx = (x + 40 * math.sin(t * 1.3 + ph)) % W
        fl = 0.55 + 0.45 * math.sin(t * 9 + ph * 3)
        d.rectangle([xx, yy, xx + s, yy + s], fill=(255, int(120 + 90 * fl), 40, int(255 * a * fl * strength)))


import os
PREVIEW = [int(v) for v in os.environ.get('FRAMES', '').split(',') if v]
out = sys.stdout.buffer
for f in (PREVIEW or range(int(SECONDS * FPS))):
    t = f / FPS
    # camera: a boom shake at the cut, then a slow drift back
    z = 1.12 - 0.08 * ease(t / SECONDS)
    shake = 14 * math.exp(-t * 3.5)
    sx, sy = shake * math.sin(t * 61), shake * math.cos(t * 47)
    cw, ch = W * z, H * z
    x0 = min(max((plate.width - cw) / 2 + sx, 0), plate.width - cw)
    y0 = min(max((plate.height - ch) / 2 + sy, 0), plate.height - ch)
    frame = plate.resize((W, H), Image.BILINEAR, box=(x0, y0, x0 + cw, y0 + ch))
    # white flash of the boom, then the title darkens the world
    flash = max(0.0, 1 - t / 0.35)
    dim = ease((t - TITLE_AT + 0.6) / 0.8)
    arr = np.asarray(frame, np.float32)
    lum = arr.mean(2, keepdims=True)
    hell = lum * np.array([0.55, 0.22, 0.16]) + np.array([18, 4, 8])   # the meadow goes ember-dark under the title
    arr = arr * (1 - dim) + (hell * VIGNETTE) * dim
    arr = arr + (255 - arr) * flash * 0.85
    frame = Image.fromarray(arr.clip(0, 255).astype(np.uint8)).convert('RGBA')
    draw_particles(frame, t + 3, 1.0)
    if t >= TITLE_AT:
        k = (t - TITLE_AT)
        s = 1 + 0.35 * math.exp(-k * 9)               # slams in and settles
        a = ease(k / 0.12)
        tl = title.resize((int(title.width * s), int(title.height * s)), Image.BICUBIC)
        gl = glow.resize(tl.size, Image.BICUBIC)
        cx, cy = W // 2 - tl.width // 2, int(H * 0.40) - tl.height // 2
        heat = (0.55 + 0.15 * math.sin(t * 3)) * a
        gimg = Image.new('RGBA', tl.size, (255, 110, 30, 0))
        gimg.putalpha(gl.point(lambda v: int(v * heat)))
        frame.alpha_composite(gimg, (cx, cy))
        tl.putalpha(tl.getchannel('A').point(lambda v: int(v * a)))
        frame.alpha_composite(tl, (cx, cy))
    for layer, at, yc in ((sub, SUB_AT, 0.62), (url, URL_AT, 0.75)):
        if t >= at:
            a = ease((t - at) / 0.5)
            l = layer.copy()
            l.putalpha(l.getchannel('A').point(lambda v: int(v * a)))
            frame.alpha_composite(l, (W // 2 - l.width // 2, int(H * yc) - l.height // 2 + int(12 * (1 - a))))
    arr = np.asarray(frame.convert('RGB'), np.float32)
    arr *= 1 - ease((t - FADE_AT) / (SECONDS - FADE_AT))
    if PREVIEW:
        Image.fromarray(arr.clip(0, 255).astype(np.uint8)).save(f'preview_{f}.png')
    else:
        out.write(arr.clip(0, 255).astype(np.uint8).tobytes())
