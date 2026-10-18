"""Shot 1: the village. A slow push from the main menu's own framing into the hut's dark doorway.
Plate: the outpainted menu frame (1680x720 base coords; the menu itself is x 400-1680) upscaled 4x by
Real-ESRGAN. Raw RGB 1920x1080 frames to stdout. Usage: python render_exterior.py <plate_4x.png>"""
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw

W, H, FPS, SECONDS = 1920, 1080, 30, 17.5
BASE_W, BASE_H = 1680, 720
# camera keys (t, centre x, centre y, zoom); zoom 1 at x 1040 is exactly the main menu's view, and the
# last key sits inside the doorway's dark (x 582-643, y 565-684)
KEYS = [(0.0, 1040, 360, 1.0), (2.0, 1020, 368, 1.04), (6.0, 780, 440, 1.45), (9.5, 630, 520, 2.5),
        (13.0, 616, 590, 4.8), (15.6, 613, 618, 9.0), (17.5, 612, 628, 17.0)]
FADE = (15.6, 17.5)

big = Image.open(sys.argv[1]).convert('RGB')
S = big.width / BASE_W
levels = [(S, big)]
while levels[-1][0] > 1.01:
    s, im = levels[-1]
    levels.append((s / 2, im.resize((im.width // 2, im.height // 2), Image.LANCZOS)))
levels.reverse()   # smallest first

def pchip(xs, ys):
    """Monotone cubic interpolation (Fritsch-Carlson): smooth, no overshoot between keys."""
    xs, ys = np.asarray(xs, float), np.asarray(ys, float)
    h = np.diff(xs); dl = np.diff(ys) / h
    m = np.zeros_like(ys)
    m[0], m[-1] = dl[0] * 0.5, dl[-1]
    for i in range(1, len(xs) - 1):
        if dl[i - 1] * dl[i] > 0:
            w1, w2 = 2 * h[i] + h[i - 1], h[i] + 2 * h[i - 1]
            m[i] = (w1 + w2) / (w1 / dl[i - 1] + w2 / dl[i])
    m[0] = 0.0   # start from rest
    def f(x):
        i = int(np.clip(np.searchsorted(xs, x) - 1, 0, len(h) - 1))
        u = (x - xs[i]) / h[i]
        h00, h10, h01, h11 = 2*u**3 - 3*u**2 + 1, u**3 - 2*u**2 + u, -2*u**3 + 3*u**2, u**3 - u**2
        return h00 * ys[i] + h10 * h[i] * m[i] + h01 * ys[i + 1] + h11 * h[i] * m[i + 1]
    return f

kt = [k[0] for k in KEYS]
cam_x, cam_y = pchip(kt, [k[1] for k in KEYS]), pchip(kt, [k[2] for k in KEYS])
cam_z = pchip(kt, [math.log(k[3]) for k in KEYS])


def smooth(p):
    p = min(max(p, 0.0), 1.0)
    return p * p * p * (p * (6 * p - 15) + 10)

rng = np.random.default_rng(3)
N = 46   # pollen and seed fluff drifting across the meadow
motes = np.column_stack([rng.uniform(0, W, N), rng.uniform(0, H, N), rng.uniform(8, 30, N),
                         rng.uniform(0, 6.28, N), rng.integers(3, 6, N), rng.uniform(0.35, 0.9, N)])

PREVIEW = [int(v) for v in os.environ.get('FRAMES', '').split(',') if v]
out = sys.stdout.buffer
total = int(SECONDS * FPS)
for f in (PREVIEW or range(total)):
    t = f / FPS
    p = t / SECONDS
    zoom, cx, cy = math.exp(cam_z(t)), cam_x(t), cam_y(t)
    vw, vh = 1280 / zoom, 720 / zoom
    x0 = min(max(cx - vw / 2, 0), BASE_W - vw)
    y0 = min(max(cy - vh / 2, 0), BASE_H - vh)
    scale, src = next(((sc, im) for sc, im in levels if vw * sc >= W * 0.95), levels[-1])
    box = (x0 * scale, y0 * scale, (x0 + vw) * scale, (y0 + vh) * scale)
    frame = src.resize((W, H), Image.BICUBIC if vw * scale < W else Image.LANCZOS, box=box).convert('RGBA')
    # drifting motes, fading out as we reach the doorway
    d = ImageDraw.Draw(frame, 'RGBA')
    vis = 1 - smooth((p - 0.5) / 0.2)
    for x, y, v, ph, sz, a in motes:
        xx = (x + v * t * 1.6 + 25 * math.sin(t * 0.7 + ph)) % W
        yy = (y - v * t * 0.4 + 18 * math.sin(t * 1.1 + ph * 2)) % H
        tw = 0.6 + 0.4 * math.sin(t * 3 + ph * 5)
        d.rectangle([xx, yy, xx + sz, yy + sz], fill=(255, 250, 215, int(200 * a * tw * vis)))
    arr = np.asarray(frame.convert('RGB'), np.float32)
    arr *= 1 - smooth((t - FADE[0]) / (FADE[1] - FADE[0]))
    arr = arr.clip(0, 255).astype(np.uint8)
    if PREVIEW:
        Image.fromarray(arr).save(f'ext_{f}.png')
    else:
        out.write(arr.tobytes())
