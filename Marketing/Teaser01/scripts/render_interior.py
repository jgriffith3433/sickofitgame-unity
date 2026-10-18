"""Shots 3-4: inside the hut. The hero asleep (a Wan loop of the painted interior, upscaled), a slow push to
his helmet, dust in the sunbeam, and his visor's eyes glowing orange on the line.
Raw RGB 1920x1080 frames to stdout.
Usage: python render_interior.py <loop.mp4> <still.png>
  loop.mp4  the sleeping loop (any size with the plate's 1664:928 shape; played over and over)
  still.png the plate the loop was made from (1664x928, base coordinates; template for tracking the helmet)"""
import sys, os, math, subprocess
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W, H, FPS = 1920, 1080, 30
SECONDS = 15.7              # teaser 18.3-34.0
FADE_IN = 1.5
LINE = 10.3                 # the voice take starts here (teaser 28.6): "I'm tired..." +0.1, "...of" +2.9, "sleeping" +3.6
CRACK, IGNITE = LINE + 0.15, LINE + 3.6
BASE_W, BASE_H = 1664, 928
EYES = [((511, 470.5), (522, 468.8)), ((530, 467.5), (541, 465.3))]   # the two eye slits in the visor's black band, base px
HEAD = (470, 430, 590, 510)                                    # template box for tracking the helmet

loop_path, still_path = sys.argv[1], sys.argv[2]
probe = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries', 'stream=width,height,r_frame_rate',
                        '-of', 'csv=p=0', loop_path], capture_output=True, text=True).stdout.strip().split(',')
lw, lh = int(probe[0]), int(probe[1])
num, den = probe[2].split('/'); lfps = float(num) / float(den)
# decode once to a raw file and memory-map it (the 4x loop is ~3 GB of pixels)
cache = os.path.splitext(loop_path)[0] + f'.{lw}x{lh}.rgb'
if not os.path.exists(cache):
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', loop_path, '-f', 'rawvideo', '-pix_fmt', 'rgb24', cache], check=True)
frames = np.memmap(cache, np.uint8, 'r').reshape(-1, lh, lw, 3)
# Wan's first frames settle in and its last frame doesn't match its first: play it forward and back from
# frame SKIP (breathing reads the same both ways), a seamless cycle
SKIP = int(round(5 * lfps / 16))
order = list(range(SKIP, len(frames))) + list(range(len(frames) - 2, SKIP, -1))
loop = [frames[i] for i in order]   # views into the map, no copies
n_loop = len(loop)
LS = lw / BASE_W
still = np.asarray(Image.open(still_path).convert('L').resize((BASE_W // 2, BASE_H // 2)), np.float32)


def smooth(p):
    p = min(max(p, 0.0), 1.0)
    return p * p * p * (p * (6 * p - 15) + 10)


def gblur(a, sigma):
    r = int(sigma * 3)
    x = np.arange(-r, r + 1); k = np.exp(-x * x / (2 * sigma * sigma)); k /= k.sum()
    a = np.apply_along_axis(lambda v: np.convolve(v, k, 'same'), 0, a)
    return np.apply_along_axis(lambda v: np.convolve(v, k, 'same'), 1, a)


# --- track the helmet in every loop frame (half-res SSD search, +-8 px), so the glow rides his breathing
x0, y0, x1, y1 = [v // 2 for v in HEAD]
tmpl = still[y0:y1, x0:x1]
offsets = []
M = 5   # search margin, half-res px
for fr in loop:
    crop = np.asarray(fr[int((y0 - M) * 2 * LS):int((y1 + M) * 2 * LS), int((x0 - M) * 2 * LS):int((x1 + M) * 2 * LS)])
    g = np.asarray(Image.fromarray(crop).convert('L').resize((x1 - x0 + 2 * M, y1 - y0 + 2 * M)), np.float32)
    best = (1e18, 0, 0)
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            e = ((g[M + dy:M + dy + y1 - y0, M + dx:M + dx + x1 - x0] - tmpl) ** 2).mean()
            if e < best[0]:
                best = (e, dx * 2, dy * 2)
    offsets.append(best[1:])
offsets = np.array(offsets, np.float32)
# light smoothing so the glow doesn't jitter
k = np.array([1, 2, 3, 2, 1], np.float32); k /= k.sum()
offsets = np.stack([np.convolve(np.pad(offsets[:, i], 2, mode='wrap'), k, 'valid') for i in range(2)], 1)
sys.stderr.write(f'loop {lw}x{lh} {n_loop} frames @{lfps:.1f}; helmet moves dx {offsets[:,0].min():.1f}..{offsets[:,0].max():.1f} '
                 f'dy {offsets[:,1].min():.1f}..{offsets[:,1].max():.1f}\n')


# breathing: Wan barely moves his chest, so lift it with a soft vertical warp, in on each snore (the snores
# start at teaser 19.8, 22.5, 24.8), one last breath before the line, then still. (start, peak, end) in shot time.
T0 = 18.3
BREATHS = [(-1.2, 0.0, 1.5)] + [(a - T0, b - T0, c - T0) for a, b, c in
           ((19.8, 21.6, 22.5), (22.5, 23.5, 24.8), (24.8, 26.2, 27.4), (27.4, 28.4, 29.6))]
CHEST = (650.0, 545.0, 150.0, 72.0)   # centre x, y, radii in base px
LIFT = 3.6                            # base px at the top of a breath


def breath(t):
    for a, b, c in BREATHS:
        if a <= t < b:
            return smooth((t - a) / (b - a))
        if b <= t < c:
            return 1 - smooth((t - b) / (c - b))
    return 0.0


ry0, ry1 = int((CHEST[1] - 2.2 * CHEST[3]) * LS), int((CHEST[1] + 2.2 * CHEST[3]) * LS)
rx0, rx1 = int((CHEST[0] - 2.2 * CHEST[2]) * LS), int((CHEST[0] + 2.2 * CHEST[2]) * LS)
_yy, _xx = np.mgrid[ry0:ry1, rx0:rx1].astype(np.float32)
CHEST_MASK = np.exp(-(((_xx / LS - CHEST[0]) / CHEST[2]) ** 2 + ((_yy / LS - CHEST[1]) / CHEST[3]) ** 2) * 1.4)


def breathe(img, amount):
    """Lift the chest by amount*LIFT base px (vertical warp, linear sampling)."""
    if amount < 0.01:
        return img
    roi = img[ry0:ry1, rx0:rx1].astype(np.float32)
    sy = _yy - ry0 + amount * LIFT * LS * CHEST_MASK
    y0 = np.clip(np.floor(sy).astype(int), 0, roi.shape[0] - 2)
    w = (sy - y0)[..., None]
    cols = np.arange(roi.shape[1])[None, :]
    out = roi[y0, cols] * (1 - w) + roi[y0 + 1, cols] * w
    img[ry0:ry1, rx0:rx1] = out.clip(0, 255).astype(np.uint8)
    return img


def glow_level(t):
    """0 asleep; a dim crack of light on "I'm tired...", then they blaze on "sleeping"."""
    g = 0.0
    if t >= CRACK:
        u = t - CRACK
        g = 0.48 * smooth(u / 0.25) - 0.18 * smooth((u - 0.5) / 1.0)          # flicker open, settle half-lidded
        g += 0.05 * math.sin(u * 13) * math.exp(-u * 2)
    if t >= IGNITE:
        u = t - IGNITE
        g = max(g, 0.18 + 0.82 * smooth(u / 0.18) + 0.35 * math.exp(-u * 4) * smooth(u / 0.18))
        g += 0.25 * smooth((t - (SECONDS - 0.6)) / 0.6)                          # flares into the boom
    return max(g, 0.0)


# camera: a slow push toward the helmet; a small extra push when the eyes ignite
FOCUS = (600.0, 500.0)
def camera(t):
    p = smooth(t / SECONDS) * 0.85 + 0.15 * (t / SECONDS)
    z = 1.01 + 0.62 * p + 0.14 * smooth((t - IGNITE) / 0.6)
    c = min(1.0, p * 1.1)
    cx = BASE_W / 2 + (FOCUS[0] - BASE_W / 2) * c
    cy = BASE_H / 2 + (FOCUS[1] - BASE_H / 2) * c
    return cx, cy, z


rng = np.random.default_rng(11)
N = 70   # dust turning in the sunbeam (window at upper right, beam falls down-left)
dust = np.column_stack([rng.uniform(0, 1, N), rng.uniform(-0.12, 0.12, N), rng.uniform(0, 6.28, N),
                        rng.uniform(0.004, 0.012, N), rng.integers(2, 4, N), rng.uniform(0.3, 1.0, N)])
BEAM_A, BEAM_B = np.array([1120.0, 260.0]), np.array([800.0, 640.0])

_bloom = {}
PREVIEW = [int(v) for v in os.environ.get('FRAMES', '').split(',') if v]
out = sys.stdout.buffer
for f in (PREVIEW or range(int(SECONDS * FPS))):
    t = f / FPS
    li = int(round(t * lfps)) % n_loop
    img = breathe(np.array(loop[li]), breath(t))          # one uint8 copy of the frame, edited in place
    ox, oy = offsets[li]
    # the eyes: float bloom in a box around them (8-bit blurs of a thin line band visibly), in place
    g = glow_level(t)
    if g > 0.01:
        R = int(110 * LS)
        ex, ey = int((527 + ox) * LS), int((466 + oy) * LS)
        bx0, by0 = max(ex - R, 0), max(ey - R, 0)
        key = (round(float(ox) * LS), round(float(oy) * LS))
        if key not in _bloom:
            core = Image.new('L', (2 * R, 2 * R), 0); cd = ImageDraw.Draw(core)
            for (ax, ay), (bx, by) in EYES:
                pts = [((ax + ox) * LS - bx0, (ay + oy) * LS - by0), ((bx + ox) * LS - bx0, (by + oy) * LS - by0)]
                cd.line(pts, fill=255, width=max(3, int(3.4 * LS)))
            c = np.asarray(core, np.float32) / 255
            b1, b2 = gblur(c, 5 * LS), gblur(c, 30 * LS)
            _bloom[key] = (c[..., None], (b1 / max(b1.max(), 1e-6))[..., None], (b2 / max(b2.max(), 1e-6))[..., None])
        c, b1, b2 = _bloom[key]
        roi = img[by0:by0 + 2 * R, bx0:bx0 + 2 * R].astype(np.float32)
        h_, w_ = roi.shape[:2]
        c, b1, b2 = c[:h_, :w_], b1[:h_, :w_], b2[:h_, :w_]
        hot = np.array([255, 236, 170], np.float32); orange = np.array([255, 120, 30], np.float32)
        roi += orange * (b1 * 1.9 + b2 * 1.1 * min(g, 1.3)) * g
        k = min(1.0, g * 2.6)                                  # the slits read as eyes even half-lit
        roi = roi * (1 - c * k) + hot * c * k
        img[by0:by0 + 2 * R, bx0:bx0 + 2 * R] = roi.clip(0, 255).astype(np.uint8)
    fr = Image.fromarray(img)
    # dust: drawn on the plate in base px, scaled with it
    d = ImageDraw.Draw(fr, 'RGBA')
    for s_, w_, ph, sp, sz, a in dust:
        s = (s_ + sp * t) % 1.0
        pos = BEAM_A + (BEAM_B - BEAM_A) * s
        perp = np.array([0.76, 0.65]) * (w_ * 300 + 10 * math.sin(t * 0.8 + ph))
        x, y = (pos + perp) * LS
        tw = (0.55 + 0.45 * math.sin(t * 2.2 + ph * 4)) * math.sin(math.pi * s)
        sz = sz * LS * 0.6
        d.rectangle([x, y, x + sz, y + sz], fill=(255, 236, 180, int(170 * a * tw)))
    cx, cy, z = camera(t)
    vw, vh = BASE_W / z, BASE_W / z * H / W
    bx0 = min(max(cx - vw / 2, 0), BASE_W - vw); by0 = min(max(cy - vh / 2, 0), BASE_H - vh)
    frame = fr.resize((W, H), Image.BICUBIC, box=(bx0 * LS, by0 * LS, (bx0 + vw) * LS, (by0 + vh) * LS))
    arr = np.asarray(frame, np.float32)
    arr += np.array([40, 14, 0], np.float32) * max(0, g - 0.5)    # the room warms when they blaze
    arr *= smooth(t / FADE_IN)
    arr = arr + (255 - arr) * smooth((t - (SECONDS - 0.25)) / 0.25) * 0.9      # the boom's white
    arr = arr.clip(0, 255).astype(np.uint8)
    if PREVIEW:
        Image.fromarray(arr).save(f'int_{f}.png')
    else:
        out.write(arr.tobytes())
