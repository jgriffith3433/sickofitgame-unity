"""Level 1's portal and beam art without the GPU: the frames fx_sheets.py bakes until Hawkes Gen's are made.

The swirl inside the portal is Video2's own fire tunnel (the repainted Assets/Videos/Video2.mp4, 12-14 s), squashed
into the flat ellipse of a hole seen side-on, so it is literally the portal the hero falls through. Around it a ring
of torn, burning turf in the Meadow's pixel-art palette, flames and sparks; the beam's charge glow, column and impact
burst are painted the same way. Everything is drawn at 1x and pixelated (blocks of PIXEL) to sit with the Meadow's art.
Loops are built on a phase, so their last frame runs into their first.

    python fx_interim.py <out folder> [--video Assets/Videos/Video2.mp4]

writes <out>/portal/{open,loop,close}/NN.png, <out>/portal/scorch.png and <out>/beam/{charge,column,impact}/NN.png,
the sources of Tools/Art/fx/portal-interim.json and beam-interim.json.
"""
import argparse
import math
import os
import subprocess
import tempfile

import numpy as np
from PIL import Image

PIXEL = 3
W, H = 832, 480            # the portal's frame (fx/portal-*.json "size")
CX, CY = 416, 300          # the hole's centre = the ground point the entity stands on (the job's pivot)
RX, RY = 268, 50           # the hole's half axes: side-on, almost flat
LOOP = 32                  # loop frames (2 s at 16 fps)
OPEN = 24                  # open frames (1.5 s at 16 fps)
CLOSE = 24                 # close frames (1 s at 24 fps)

GRASS = np.array([[0x8c, 0xc0, 0x4a], [0x5a, 0x9a, 0x32], [0x3f, 0x7a, 0x2a]], np.float32)
DIRT = np.array([[0x7a, 0x4a, 0x2a], [0x5a, 0x34, 0x20]], np.float32)
CHAR = np.array([0x2a, 0x1a, 0x12], np.float32)
FIRE = [(0.0, (255, 255, 235)), (0.25, (255, 226, 120)), (0.55, (255, 150, 40)), (0.8, (225, 70, 20)), (1.0, (120, 20, 10))]


def ramp(t):
    """Fire colour for 0 (white-hot) .. 1 (deep red), any shape."""
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,), np.float32)
    for (a, ca), (b, cb) in zip(FIRE, FIRE[1:]):
        m = (t >= a) & (t <= b)
        f = ((t - a) / (b - a))[m][:, None]
        out[m] = np.array(ca, np.float32) * (1 - f) + np.array(cb, np.float32) * f
    return out


def noise1(theta, seed, octaves=4):
    """Smooth periodic noise around a circle: sum of sines with seeded phases, about -1..1."""
    rng = np.random.RandomState(seed)
    v = np.zeros_like(theta)
    for o in range(octaves):
        k = 3 * 2 ** o + rng.randint(0, 3)
        v += np.sin(theta * k + rng.uniform(0, 6.283)) / (o + 1.5)
    return v / 1.6


def grid():
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    dx, dy = (xs - CX) / RX, (ys - CY) / RY
    return xs, ys, np.sqrt(dx * dx + dy * dy), np.arctan2(dy, dx)


XS, YS, R, TH = grid()
RNG = np.random.RandomState(7)
SPECK = RNG.rand(H, W).astype(np.float32)
TEAR_IN = noise1(TH, 1) * 0.06            # the hole's ragged edge
TEAR_OUT = noise1(TH, 2) * 0.10           # the rim's outer edge
CRACK_A = RNG.uniform(0, 6.283, 9)        # crack directions across the scorch


def over(dst, rgb, a):
    """Alpha-composite rgb (H,W,3) with alpha a (H,W) over dst (H,W,4 float, straight alpha)."""
    a = np.clip(a, 0, 1)
    da = dst[..., 3]
    oa = a + da * (1 - a)
    safe = np.maximum(oa, 1e-5)[..., None]
    dst[..., :3] = (rgb * a[..., None] + dst[..., :3] * (da * (1 - a))[..., None]) / safe
    dst[..., 3] = oa
    return dst


def add_light(dst, rgb, a):
    """Light: brightens and fills alpha (fire over the dark hole)."""
    return over(dst, rgb, a)


def pixelate(img):
    small = img.resize((img.width // PIXEL, img.height // PIXEL), Image.BOX)
    return small.resize((small.width * PIXEL, small.height * PIXEL), Image.NEAREST)


def save(arr, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    pixelate(img).save(path)


# ─── the tunnel ──────────────────────────────────────────────────────────────

def tunnel_frames(video, count):
    """count frames of Video2's fire tunnel (12-14 s), as square crops round the tunnel's centre, crossfaded to loop."""
    with tempfile.TemporaryDirectory() as tmp:
        n = count + 8
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", "12.0", "-i", video, "-t", str(n / 16 + 0.1), "-vf",
                        "fps=16,crop=ih:ih:(iw-ih)/2:0,scale=512:512", "-frames:v", str(n), os.path.join(tmp, "t_%03d.png")],
                       check=True)
        raw = [np.asarray(Image.open(os.path.join(tmp, f)).convert("RGB")).astype(np.float32) for f in sorted(os.listdir(tmp))]
    frames = raw[:count]
    # Crossfade the 8 frames after the loop into its first 8, so frame count-1 runs into frame 0.
    for i in range(8):
        f = (i + 1) / 9
        frames[i] = raw[count + i] * (1 - f) + raw[i] * f
    return frames


def squash(tunnel, scale=1.0):
    """The tunnel frame mapped onto the hole's ellipse (scaled by scale), as (rgb, alpha)."""
    rx, ry = max(2, RX * scale), max(1, RY * scale)
    u = ((XS - CX) / rx * 0.5 + 0.5) * 511
    v = ((YS - CY) / ry * 0.5 + 0.5) * 511
    inside = (u >= 0) & (u <= 511) & (v >= 0) & (v <= 511)
    ui, vi = np.clip(u, 0, 511).astype(int), np.clip(v, 0, 511).astype(int)
    rgb = tunnel[vi, ui]
    # Graded to Video2's deep fire: its pale swirl mapped onto the fire ramp by brightness, keeping its structure.
    lum = rgb.mean(axis=2) / 255.0
    lo, hi = 0.55, 0.95
    rgb = ramp(1 - np.clip((lum - lo) / (hi - lo), 0, 1) ** 1.3) * 0.8 + rgb * 0.2
    r = np.sqrt(((XS - CX) / rx) ** 2 + ((YS - CY) / ry) ** 2)
    a = np.clip((1 + TEAR_IN - r) / 0.06, 0, 1) * inside
    # Hotter toward the middle (deeper down the tunnel), a dark band just under the lip.
    rgb = rgb * (0.75 + 0.35 * np.clip(1 - r, 0, 1))[..., None]
    rgb = rgb * (1 - 0.45 * np.clip((r - 0.82) / 0.18, 0, 1))[..., None]
    return np.clip(rgb, 0, 255), a


# ─── the ground around it ────────────────────────────────────────────────────

def scorch_layer(amount, glow):
    """Burnt grass round the hole (amount 0..1 how far it has spread), cracks glowing by glow 0..1."""
    reach = 0.9 + 0.38 * amount
    r = R + TEAR_OUT * 0.5
    a = np.clip((reach - r) / 0.18, 0, 1) * np.clip(amount * 3, 0, 1) * 0.8
    a = a * (0.75 + 0.25 * SPECK)
    rgb = np.broadcast_to(CHAR, (H, W, 3)).copy()
    rgb += (SPECK[..., None] - 0.5) * 22
    # Glowing cracks: thin wedges along a few directions out from the centre.
    cracks = np.zeros((H, W), np.float32)
    for k, ang in enumerate(CRACK_A):
        d = np.abs(np.angle(np.exp(1j * (TH - ang))))
        width = 0.035 * (1 - np.clip(R / reach, 0, 1)) + 0.004
        cracks = np.maximum(cracks, np.clip(1 - d / width, 0, 1) * (R < reach * (0.6 + 0.4 * ((k * 37) % 10) / 10)))
    c = cracks * glow
    rgb = rgb * (1 - c[..., None]) + ramp(0.35 + 0.4 * (1 - c))[..., :] * c[..., None]
    return rgb, np.maximum(a, c * a.clip(0.6, 1))


def rim_layer(scale, heat, front):
    """The torn turf ring round a hole of the given scale: grass on top, torn earth, hot at the inner edge.
    front=True draws the near half (in front of the fire), False the far half (behind it)."""
    if scale <= 0.02:
        return np.zeros((H, W, 3), np.float32), np.zeros((H, W), np.float32)
    rx, ry = RX * scale, RY * scale
    r = np.sqrt(((XS - CX) / rx) ** 2 + ((YS - CY) / ry) ** 2)
    inner = 1 + TEAR_IN
    outer = 1.22 + TEAR_OUT
    ring = (r >= inner - 0.01) & (r <= outer)
    half = (YS >= CY) if front else (YS < CY)
    a = ring & half
    t = np.clip((r - inner) / np.maximum(outer - inner, 1e-3), 0, 1)
    rgb = np.zeros((H, W, 3), np.float32)
    # inner -> outer: hot torn earth, charred, dirt, then grass at the lip.
    hot = ramp(0.45 + 0.5 * t)
    dirt = DIRT[(SPECK * 2).astype(int).clip(0, 1)]
    grass = GRASS[(SPECK * 3).astype(int).clip(0, 2)]
    rgb[:] = dirt
    rgb = np.where((t < 0.35)[..., None], CHAR + (SPECK[..., None] - 0.5) * 16, rgb)
    rgb = np.where((t > 0.72)[..., None], grass * 0.7 + CHAR * 0.3, rgb)   # scorched grass at the lip
    h = np.clip((0.3 - t) / 0.3, 0, 1) * heat
    rgb = rgb * (1 - h[..., None]) + hot * h[..., None]
    # The near lip has some thickness: a strip of earth under it (the torn side of the hole).
    alpha = a.astype(np.float32)
    if front:
        below = (YS > CY) & (r > outer) & (np.sqrt(((XS - CX) / rx) ** 2 + ((YS - 7 * scale - CY) / ry) ** 2) <= outer)
        rgb = np.where(below[..., None], DIRT[1] * 0.8, rgb)
        alpha = np.maximum(alpha, below.astype(np.float32))
    return rgb, alpha


# ─── fire above it ───────────────────────────────────────────────────────────

def flames(phase, scale, height):
    """Tongues of flame licking up out of the hole (phase 0..1 loops), and a glow; height multiplies their length."""
    rgb = np.zeros((H, W, 3), np.float32)
    a = np.zeros((H, W), np.float32)
    if scale <= 0.02 or height <= 0:
        return rgb, a
    rng = np.random.RandomState(11)
    for k in range(9):
        x0 = CX + (rng.uniform(-0.75, 0.75)) * RX * scale
        p = (phase * (1 + k % 2) + rng.uniform(0, 1)) % 1.0
        length = (34 + 46 * rng.uniform(0.3, 1)) * height * (0.55 + 0.45 * math.sin(p * 6.283) ** 2) * scale
        width = (34 + 30 * rng.uniform(0, 1)) * scale
        sway = 16 * math.sin(p * 6.283 * 2 + k) * scale
        y = (CY - YS) / max(length, 1)                     # 0 at the hole's centre line, 1 at the tip
        wob = sway * y * y
        d = np.abs(XS - x0 - wob) / (width * np.clip(1 - y, 0.05, 1) ** 0.7)
        m = (y > -0.25) & (y < 1) & (d < 1)
        f = np.clip(1 - d, 0, 1) * np.clip(1 - y, 0, 1) * m
        a = np.maximum(a, f)
    rgb = ramp(1 - a * 0.9 + 0.05)
    glow = np.exp(-(((XS - CX) / (RX * scale * 1.1)) ** 2 + ((YS - (CY - 30 * height)) / (70 * height + 1)) ** 2) * 2.0)
    glow = glow * (YS < CY + 4)
    a2 = np.maximum(a * 0.95, glow * 0.35 * min(1.0, height))
    rgb = np.where((a > glow * 0.35)[..., None], rgb, ramp(np.full((H, W), 0.6, np.float32)))
    return rgb, a2


def sparks(phase, scale, count=26, rise=150):
    """Embers rising out of the hole on a loop."""
    rgb = np.zeros((H, W, 3), np.float32)
    a = np.zeros((H, W), np.float32)
    if scale <= 0.02:
        return rgb, a
    rng = np.random.RandomState(5)
    for k in range(count):
        p = (phase + rng.uniform(0, 1)) % 1.0
        x = CX + rng.uniform(-0.9, 0.9) * RX * scale + 18 * math.sin((p + k) * 6.283)
        y = CY - 6 - p * rise * (0.6 + 0.6 * rng.uniform(0, 1))
        s = 2.5 + 2 * rng.uniform(0, 1)
        f = np.clip(1 - np.sqrt((XS - x) ** 2 + (YS - y) ** 2) / s, 0, 1) * (1 - p)
        a = np.maximum(a, f * 1.6)
    rgb[:] = ramp(np.full((H, W), 0.3, np.float32))
    return rgb, np.clip(a, 0, 1)


def portal_frame(tunnel, phase, hole=1.0, scorch=1.0, cracks=0.0, flame=1.0, heat=1.0, flare=0.0):
    img = np.zeros((H, W, 4), np.float32)
    srgb, sa = scorch_layer(scorch, max(cracks, 0.25 * hole))
    over(img, srgb, sa)
    brgb, ba = rim_layer(hole, heat, front=False)
    over(img, brgb, ba)
    if hole > 0.02:
        frgb, fa = squash(tunnel, hole)
        frgb = frgb + (255 - frgb) * flare * 0.4
        over(img, frgb, fa)
    lrgb, la = flames(phase, hole, flame * (1 + 1.4 * flare))
    over(img, lrgb, la)
    frgb, fa = rim_layer(hole, heat, front=True)
    over(img, frgb, fa)
    srgb, sa = sparks(phase, max(hole, 0.0), count=int(26 + 30 * flare), rise=150 + 150 * flare)
    over(img, srgb, sa)
    img[..., :3] = np.clip(img[..., :3], 0, 255)
    img[..., 3] = np.clip(img[..., 3], 0, 1) * 255
    return img


def ease_out(t):
    return 1 - (1 - t) ** 3


def ease_in(t):
    return t ** 2.2


def make_portal(out, video):
    tunnel = tunnel_frames(video, LOOP)
    for i in range(LOOP):
        save(portal_frame(tunnel[i], i / LOOP), os.path.join(out, "portal", "loop", f"{i:02d}.png"))
    # Open: the beam's scorch spreads, cracks glow, a pinhole, then the ring tears open (ends where Loop starts).
    for i in range(OPEN):
        t = i / (OPEN - 1)
        scorch = ease_out(min(1, t / 0.3))
        cracks = np.clip((t - 0.15) / 0.3, 0, 1)
        hole = 0.0 if t < 0.35 else 0.12 + 0.88 * ease_out((t - 0.35) / 0.65)
        flame = np.clip((t - 0.45) / 0.55, 0, 1)
        flare = max(0.0, 0.6 - abs(t - 0.5) * 3)          # a pop as it tears
        tun = tunnel[(i - OPEN) % LOOP]
        save(portal_frame(tun, (i - OPEN) / LOOP, hole=hole, scorch=scorch, cracks=cracks, flame=flame, flare=flare),
             os.path.join(out, "portal", "open", f"{i:02d}.png"))
    # Close: a flare, then the ring shrinks shut, leaving the scorch.
    for i in range(CLOSE):
        t = i / (CLOSE - 1)
        flare = 1.0 if t < 0.2 else max(0.0, 1 - (t - 0.2) / 0.25)
        hole = 1.0 if t < 0.2 else max(0.0, 1 - ease_in((t - 0.2) / 0.6))
        heat = 1.0 - max(0.0, (t - 0.6) / 0.4)
        cracks = max(0.0, 1 - t * 1.2)
        save(portal_frame(tunnel[i % LOOP], i / LOOP, hole=hole, cracks=cracks, flame=hole, heat=heat, flare=flare),
             os.path.join(out, "portal", "close", f"{i:02d}.png"))
    save(portal_frame(tunnel[0], 0, hole=0.0, cracks=0.0, flame=0.0), os.path.join(out, "portal", "scorch.png"))


# ─── the beam ────────────────────────────────────────────────────────────────

def beam_column(out, frames=6, w=192, h=1536):
    """The column from the sky: white-hot core, red-orange sheath, streaks crawling down, a flared foot. Pivot: bottom."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    x = (xs - w / 2)
    rng = np.random.RandomState(3)
    streak_x = rng.uniform(-40, 40, 24)
    for i in range(frames):
        p = i / frames
        breathe = 1 + 0.12 * math.sin(p * 6.283 * 2)
        foot = 1 + 1.6 * np.clip((ys - (h - 160)) / 160, 0, 1) ** 2      # widens where it hits the ground
        core = np.exp(-(x / (7 * breathe * foot)) ** 2)
        mid = np.exp(-(x / (22 * breathe * foot)) ** 2)
        outer = np.exp(-(x / (55 * breathe * foot)) ** 2)
        streaks = np.zeros_like(x)
        for k, sx in enumerate(streak_x):
            phase = (ys / h * 6 + p * 2 + k * 0.37) % 1.0         # crawls down by whole periods per loop
            streaks = np.maximum(streaks, np.exp(-((x - sx * breathe) / 4) ** 2) * (phase < 0.35) * 0.8)
        lum = np.clip(core * 1.0 + mid * 0.75 + streaks * outer * 1.2, 0, 1)
        a = np.clip(core + mid * 0.9 + outer * 0.45 + streaks * outer, 0, 1)
        fade_top = np.clip(ys / 80, 0, 1)
        a = a * fade_top
        rgb = ramp(1 - lum)
        img = np.dstack([rgb, a * 255])
        save(img, os.path.join(out, "beam", "column", f"{i:02d}.png"))


def beam_charge(out, frames=15, w=512, h=320, cy=270):
    """The charge glow on the ground (1.25 s): a flat disc that grows and throbs, rings, embers pulled in, a thin ray."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    rng = np.random.RandomState(9)
    for i in range(frames):
        t = i / (frames - 1)
        grow = 0.25 + 0.75 * ease_out(min(1, t / 0.7))
        throb = 1 + (0.06 * math.sin(i * 2.2) if t < 0.7 else 0.14 * (i % 2))
        rx, ry = 170 * grow * throb, 26 * grow * throb
        d = np.sqrt(((xs - w / 2) / rx) ** 2 + ((ys - cy) / ry) ** 2)
        disc = np.exp(-d ** 2 * 1.6)
        ring_r = (t * 3) % 1.0
        ring = np.exp(-((d - (1.3 - ring_r)) / 0.06) ** 2) * (d < 1.4) * 0.7
        ray_w = 2 + 4 * t
        ray = np.exp(-((xs - w / 2) / ray_w) ** 2) * (ys < cy) * (0.25 + 0.6 * t) * (0.8 + 0.2 * (i % 2))
        motes = np.zeros_like(xs)
        for k in range(18):
            ang = rng.uniform(0, 6.283)
            p = (t * 2 + k / 18) % 1.0
            dist = (1 - p) * 1.6
            mx, my = w / 2 + math.cos(ang) * dist * rx, cy - abs(math.sin(ang)) * dist * ry * 3
            motes = np.maximum(motes, np.clip(1 - np.sqrt((xs - mx) ** 2 + (ys - my) ** 2) / 3.5, 0, 1))
        lum = np.clip(disc * (0.6 + 0.4 * t) + ring + motes * 0.8, 0, 1)
        a = np.clip(disc * (0.5 + 0.5 * t) * 1.3 + ring + ray + motes, 0, 1)
        rgb = ramp(1 - lum * 0.9)
        rgb = np.where((ray > lum)[..., None], ramp(np.full_like(xs, 0.8)), rgb)
        save(np.dstack([rgb, a * 255]), os.path.join(out, "beam", "charge", f"{i:02d}.png"))


def beam_impact(out, frames=11, w=640, h=400, cy=340):
    """The burst where the beam lands: a white flash, a fan of fire, a shockwave along the ground, clods and sparks."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    dx, dy = xs - w / 2, cy - ys
    ang = np.arctan2(dy, dx)
    dist = np.sqrt(dx ** 2 + dy ** 2)
    rng = np.random.RandomState(4)
    rays = np.zeros_like(xs)
    for k in range(13):
        a0 = rng.uniform(0.25, math.pi - 0.25)
        rays = np.maximum(rays, np.exp(-((ang - a0) / rng.uniform(0.06, 0.14)) ** 2) * rng.uniform(0.6, 1.0))
    clods = [(rng.uniform(0.3, math.pi - 0.3), rng.uniform(160, 300)) for _ in range(10)]
    for i in range(frames):
        t = i / (frames - 1)
        reach = 60 + 260 * ease_out(t)
        fade = 1.0 if t < 0.35 else max(0.0, 1 - (t - 0.35) / 0.65)
        fan = rays * np.clip(1 - dist / reach, 0, 1) * (dy > -10)
        ball = np.exp(-(dist / (40 + 90 * ease_out(min(1, t * 2)))) ** 2) * (1 - t * 0.7)
        sw_r = 40 + 300 * ease_out(t)
        shock = np.exp(-((np.sqrt((dx / sw_r) ** 2 + (dy / (sw_r * 0.12)) ** 2) - 1) / 0.08) ** 2) * (1 - t)
        bits = np.zeros_like(xs)
        for a0, sp in clods:
            px = w / 2 + math.cos(a0) * sp * t * 1.2
            py = cy - math.sin(a0) * sp * t * 1.2 + 260 * t * t
            bits = np.maximum(bits, np.clip(1 - np.sqrt((xs - px) ** 2 + (ys - py) ** 2) / 5, 0, 1))
        lum = np.clip(ball * 1.2 + fan * 0.8 + shock * 0.7, 0, 1)
        a = np.clip((ball * 1.4 + fan * 1.1 + shock + bits) * fade, 0, 1)
        if i < 2:
            lum = np.clip(lum + (0.5 if i == 0 else 0.25) * np.exp(-(dist / 160) ** 2), 0, 1)
            a = np.clip(a + (0.6 if i == 0 else 0.3) * np.exp(-(dist / 160) ** 2), 0, 1)
        rgb = ramp(1 - lum)
        rgb = np.where((bits > lum)[..., None], DIRT[1] * 0.6, rgb)
        save(np.dstack([rgb, a * 255]), os.path.join(out, "beam", "impact", f"{i:02d}.png"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--video", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Videos", "Video2.mp4"))
    args = ap.parse_args()
    make_portal(args.out, args.video)
    beam_charge(args.out)
    beam_column(args.out)
    beam_impact(args.out)
    print("frames in", args.out)


if __name__ == "__main__":
    main()
