#!/usr/bin/env python3
"""Synthesises the core effect takes and the ambience beds (Python stdlib + ffmpeg, no GPU, no Hawkes Gen).

  python3 -I Tools/Audio/synth_core_sfx.py            write every take and bed under Assets/Audio/Generated/Core/
  python3 -I Tools/Audio/synth_core_sfx.py --only player_shoot ambience_meadow_calm
  python3 -I Tools/Audio/synth_core_sfx.py --list

Why this exists: Hawkes Gen's audio.sfx was not reachable from the agent that made the first version (its project
access was refused), so the three takes of every core verb and the four ambience beds are synthesised here, seeded
(the same file every run), mono, normalised, encoded to Vorbis (.ogg) with a Unity AudioImporter .meta next to each
(mono, Vorbis; loops compressed in memory). They are stand-ins in the sense of Docs/SoundAudit.md: audio.json keeps the
Hawkes Gen prompt of each sound, and `Tools/Audio/core_sfx.py --gen-list` prints what to make when the service is
available (about 33 takes + 4 loops, cents each); drop the made files over these names.
"""
import array, math, os, random, subprocess, sys, tempfile, uuid, wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(ROOT, 'Assets/Audio/Generated/Core')
SR = 44100
AMB_SR = 32000


# ─── building blocks ──────────────────────────────────────────────

def n_of(seconds, sr=SR):
    return int(seconds * sr)


def sweep(f0, f1, dur, sr=SR, shape=math.sin, power=1.0):
    """A tone gliding f0 -> f1 (exponential) over dur seconds."""
    n = n_of(dur, sr)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / max(1, n - 1)
        f = f0 * (f1 / f0) ** (t ** power)
        phase += 2 * math.pi * f / sr
        out.append(shape(phase))
    return out


def tone(f, dur, sr=SR, shape=math.sin):
    return sweep(f, f, dur, sr, shape)


def tri(p):
    return 2 / math.pi * math.asin(math.sin(p))


def saw(p):
    return ((p / (2 * math.pi)) % 1.0) * 2 - 1


def squareish(p):
    return math.tanh(3 * math.sin(p))


def noise(dur, rng, sr=SR):
    return [rng.uniform(-1, 1) for _ in range(n_of(dur, sr))]


def decay(x, tau, attack=0.002, sr=SR):
    """Exponential decay with a short linear attack (seconds)."""
    out = []
    a = max(1, int(attack * sr))
    for i, s in enumerate(x):
        t = i / sr
        g = math.exp(-t / tau)
        if i < a:
            g *= i / a
        out.append(s * g)
    return out


def scale(x, g):
    return [s * g for s in x]


def delay(x, seconds, sr=SR):
    return [0.0] * n_of(seconds, sr) + x


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, s in enumerate(p):
            out[i] += s
    return out


def lowpass(x, fc, sr=SR):
    a = 1 - math.exp(-2 * math.pi * fc / sr)
    y = 0.0
    out = []
    for s in x:
        y += a * (s - y)
        out.append(y)
    return out


def highpass(x, fc, sr=SR):
    lp = lowpass(x, fc, sr)
    return [s - l for s, l in zip(x, lp)]


def bandpass_sweep(x, f_of_t, q, sr=SR):
    """Chamberlin state-variable band-pass whose centre moves: f_of_t(0..1) -> Hz."""
    n = len(x)
    low = band = 0.0
    q1 = 1.0 / q
    out = []
    for i, s in enumerate(x):
        f = 2 * math.sin(math.pi * min(0.25, f_of_t(i / max(1, n - 1)) / sr))
        low += f * band
        high = s - low - q1 * band
        band += f * high
        out.append(band)
    return out


def peak(x):
    return max(1e-9, max(abs(s) for s in x))


def normalise(x, target=0.85):
    g = target / peak(x)
    return [s * g for s in x]


def soft_clip(x, drive=1.0):
    return [math.tanh(s * drive) for s in x]


def fade_edges(x, head=0.002, tail=0.01, sr=SR):
    n = len(x)
    h = max(1, int(head * sr))
    t = max(1, int(tail * sr))
    out = list(x)
    for i in range(min(h, n)):
        out[i] *= i / h
    for i in range(min(t, n)):
        out[n - 1 - i] *= i / t
    return out


def click(rng, dur=0.004, hp=2500):
    return highpass(decay(noise(dur + 0.004, rng), dur / 3, 0.0003), hp)


# ─── the effects: f(take) -> samples. take 0..2 shifts pitch, timbre and noise ───

PITCH = [1.0, 1.13, 0.89]


def rng_for(name, take):
    return random.Random(f'{name}:{take}')


def shoot(take):
    r, k = rng_for('shoot', take), PITCH[take]
    zap = decay(sweep(2400 * k, 420 * k, 0.16, shape=squareish, power=0.6), 0.055)
    zap = lowpass(zap, 6500)
    thump = decay(sweep(240, 70, 0.12), 0.04)
    spark = delay(decay(sweep(4600 * k, 3000 * k, 0.08), 0.03), 0.008)
    return normalise(fade_edges(mix(scale(zap, 0.9), scale(thump, 0.8), scale(spark, 0.3), scale(click(r), 0.7))), 0.9)


def hit_pop(take):
    r, k = rng_for('hit', take), PITCH[take]
    body = decay(noise(0.2, r), 0.032)
    body = bandpass_sweep(body, lambda t: 2200 * k * (1 - 0.5 * t), 1.2)
    blip = decay(sweep(950 * k, 260, 0.1, shape=tri), 0.05)
    thud = decay(sweep(170, 80, 0.09), 0.04)
    return normalise(fade_edges(mix(scale(body, 2.2), scale(blip, 0.9), scale(thud, 0.6), scale(click(r), 0.6))), 0.9)


def kill_pop(take):
    r, k = rng_for('kill', take), PITCH[take]
    boom = decay(sweep(190 * k, 44, 0.3, power=0.5), 0.13)
    crunch = lowpass(decay(noise(0.35, r), 0.07), 3200)
    pop = decay(sweep(500 * k, 1300 * k, 0.05, shape=tri), 0.035)
    ping_parts = []
    for j, f in enumerate([1568.0, 2093.0, 2637.0]):
        ping_parts.append(scale(delay(decay(tone(f * k, 0.25), 0.07), 0.045 + 0.04 * j), 0.28))
    return normalise(fade_edges(mix(scale(boom, 1.0), scale(crunch, 0.75), scale(pop, 0.55), *ping_parts, scale(click(r), 0.6))), 0.92)


def player_hurt(take):
    r, k = rng_for('hurt', take), PITCH[take]
    n = n_of(0.34)
    ph = 0.0
    voice = []
    for i in range(n):
        t = i / n
        f = (430 - 290 * t) * k * (1 + 0.04 * math.sin(2 * math.pi * 28 * i / SR))
        ph += 2 * math.pi * f / SR
        voice.append(saw(ph))
    voice = decay(lowpass(voice, 2400), 0.16)
    crash = decay(lowpass(noise(0.2, r), 3500), 0.045)
    thump = decay(sweep(150, 55, 0.12), 0.05)
    return normalise(fade_edges(mix(scale(voice, 0.7), scale(crash, 0.8), scale(thump, 0.9), scale(click(r), 0.5))), 0.9)


def graze_tick(take):
    r, k = rng_for('graze', take), PITCH[take]
    zip_ = decay(sweep(7600 * k, 4200 * k, 0.05), 0.018)
    ping = decay(tone(5200 * k, 0.08), 0.012)
    hiss = highpass(decay(noise(0.06, r), 0.008), 6000)
    return normalise(fade_edges(mix(zip_, scale(ping, 0.7), scale(hiss, 0.5)), 0.001, 0.01), 0.7)


def pickup_diamond(take):
    r, k = rng_for('pickup', take), PITCH[take]
    notes = [(1318.5, 0.0), (1975.5, 0.075)]
    parts = []
    for f, at in notes:
        f *= k
        t = mix(tone(f, 0.4), scale(tone(f * 2, 0.4), 0.35), scale(tone(f * 3, 0.4), 0.12))
        parts.append(delay(decay(t, 0.12), at))
    shimmer = delay(highpass(decay(noise(0.15, r), 0.04), 8000), 0.07)
    return normalise(fade_edges(mix(*parts, scale(shimmer, 0.25))), 0.8)


def player_jump(take):
    r, k = rng_for('jump', take), PITCH[take]
    body = decay(sweep(260 * k, 760 * k, 0.2, shape=tri, power=0.8), 0.11, 0.008)
    air = bandpass_sweep(decay(noise(0.22, r), 0.1, 0.01), lambda t: 700 + 1500 * t, 1.4)
    return normalise(fade_edges(mix(scale(body, 0.7), scale(air, 1.6))), 0.8)


def player_land(take):
    r, k = rng_for('land', take), PITCH[take]
    thud = decay(sweep(135 * k, 52, 0.12), 0.06)
    dust = decay(lowpass(noise(0.15, r), 1100), 0.04)
    return normalise(fade_edges(mix(scale(thud, 1.0), scale(dust, 0.9), scale(click(r, 0.003, 800), 0.4))), 0.85)


def player_dash(take):
    r, k = rng_for('dash', take), PITCH[take]
    n = n_of(0.3)
    air = noise(0.3, r)
    air = bandpass_sweep(air, lambda t: (500 + 3000 * t) * k, 1.6)
    shaped = [s * math.sin(math.pi * min(1.0, (i / n) ** 0.7)) ** 1.5 for i, s in enumerate(air)]
    low = decay(sweep(160 * k, 90, 0.25, shape=tri), 0.12, 0.01)
    return normalise(fade_edges(mix(scale(shaped, 2.4), scale(low, 0.35))), 0.8)


def rewind_start(take):
    r, k = rng_for('rwstart', take), PITCH[take]
    n = n_of(1.0)
    ph = 0.0
    tone_ = []
    for i in range(n):
        t = i / n
        f = 250 * k * (2400 / 250) ** (t ** 1.3)
        f *= 1 + 0.03 * math.sin(2 * math.pi * 9 * i / SR + take)
        ph += 2 * math.pi * f / SR
        g = min(1.0, t / 0.06) * (0.35 + 0.65 * t) * min(1.0, (1 - t) / 0.12)
        tone_.append(squareish(ph) * g)
    tone_ = lowpass(tone_, 5200)
    whirr = highpass(noise(1.0, r), 2500)
    whirr = [s * (0.1 + 0.4 * i / n) for i, s in enumerate(whirr)]
    return normalise(fade_edges(mix(scale(tone_, 0.8), whirr), 0.004, 0.02), 0.75)


def rewind_stop(take):
    r, k = rng_for('rwstop', take), PITCH[take]
    clk = click(r, 0.006, 1800)
    clunk = decay(sweep(430 * k, 110, 0.16, shape=tri), 0.07)
    body = decay(lowpass(noise(0.12, r), 900), 0.04)
    wind_down = decay(sweep(1800 * k, 500 * k, 0.14, shape=squareish), 0.05)
    return normalise(fade_edges(mix(scale(clk, 1.1), scale(clunk, 0.9), scale(body, 0.6), scale(wind_down, 0.25))), 0.85)


SOUNDS = {
    'player_shoot': (shoot, 3), 'hit_pop': (hit_pop, 3), 'kill_pop': (kill_pop, 3), 'player_hurt': (player_hurt, 3),
    'graze_tick': (graze_tick, 3), 'pickup_diamond': (pickup_diamond, 3), 'player_jump': (player_jump, 3),
    'player_land': (player_land, 3), 'player_dash': (player_dash, 3), 'rewind_start': (rewind_start, 3),
    'rewind_stop': (rewind_stop, 3),
}


# ─── ambience beds (seamless loops, 32 kHz) ───────────────────────

def loopable(x, fade, sr):
    """Cross-fades the tail into the head so the loop has no seam (the result is `fade` seconds shorter)."""
    f = int(fade * sr)
    body = x[:len(x) - f]
    for i in range(f):
        w = i / f
        body[i] = body[i] * w + x[len(x) - f + i] * (1 - w)
    return body


def wind(length, rng, sr, gain=1.0, rate=0.35, centre=520):
    n = n_of(length, sr)
    base = lowpass(noise(length, rng, sr), 900, sr)
    base = bandpass_sweep(base, lambda t: centre * (1 + 0.4 * math.sin(2 * math.pi * rate * t * length * 0.7)), 0.9, sr)
    out = []
    for i, s in enumerate(base):
        t = i / sr
        g = 0.55 + 0.45 * math.sin(2 * math.pi * rate * t + 1.3) * math.sin(2 * math.pi * rate * 0.37 * t)
        out.append(s * g * gain)
    return out


def place(buf, part, at, sr):
    start = int(at * sr)
    for i, s in enumerate(part):
        j = start + i
        if 0 <= j < len(buf):
            buf[j] += s


def rooster(sr, rng):
    """A far, low-passed crow: three rising 'er' notes and a long falling one, with harmonics."""
    parts = []
    notes = [(0.0, 0.18, 520, 760), (0.2, 0.18, 600, 900), (0.42, 0.22, 640, 1020), (0.7, 0.6, 980, 430)]
    out = []
    for at, dur, f0, f1 in notes:
        n = n_of(dur, sr)
        ph = 0.0
        seg = []
        for i in range(n):
            t = i / n
            f = f0 + (f1 - f0) * t
            f *= 1 + 0.02 * math.sin(2 * math.pi * 22 * i / sr)
            ph += 2 * math.pi * f / sr
            h = math.sin(ph) + 0.6 * math.sin(2 * ph) + 0.4 * math.sin(3 * ph) + 0.25 * math.sin(5 * ph)
            seg.append(h * math.sin(math.pi * t) ** 0.6)
        parts.append((at, seg))
    total = n_of(1.4, sr)
    out = [0.0] * total
    for at, seg in parts:
        place(out, seg, at, sr)
    return lowpass(out, 1700, sr)


def creak(sr, rng, f0=210, dur=0.7):
    """A wooden mill creak: a slow saw whose pitch wobbles, band-limited to the squeal."""
    n = n_of(dur, sr)
    ph = 0.0
    out = []
    for i in range(n):
        t = i / n
        f = f0 * (1 + 0.25 * math.sin(2 * math.pi * 3.1 * t) + 0.35 * t)
        ph += 2 * math.pi * f / sr
        out.append(saw(ph) * math.sin(math.pi * t) ** 1.5)
    return bandpass_sweep(out, lambda t: 700 + 500 * math.sin(6 * t), 2.5, sr)


def bell(sr, f=523.0, dur=3.0):
    """A church bell: inharmonic partials, a hard strike, a long ring."""
    parts = [(0.5, 1.0, 1.0), (1.0, 0.6, 0.9), (1.19, 0.45, 0.7), (1.5, 0.3, 0.5), (2.0, 0.5, 0.45), (2.54, 0.25, 0.3), (3.0, 0.2, 0.22)]
    out = [0.0] * n_of(dur, sr)
    for ratio, amp, ring in parts:
        fp = f * ratio * 2
        for i in range(len(out)):
            t = i / sr
            out[i] += amp * math.sin(2 * math.pi * fp * t) * math.exp(-t / (ring * 0.9))
    for i in range(int(0.004 * sr)):
        out[i] *= i / (0.004 * sr)
    return out


def crackle(length, rng, sr, density=60, gain=1.0):
    n = n_of(length, sr)
    out = [0.0] * n
    for _ in range(int(density * length)):
        at = rng.random() * length
        amp = rng.random() ** 2 * gain
        pop = decay(highpass(noise(0.03, rng, sr), 1500, sr), 0.004 + 0.008 * rng.random(), 0.0003, sr)
        place(out, scale(pop, amp), at, sr)
    return out


def meadow_calm():
    sr, L, fade = AMB_SR, 18.0, 1.5
    r = random.Random('meadow_calm')
    buf = wind(L + fade, r, sr, 1.0, 0.22, 480)
    place(buf, scale(rooster(sr, r), 0.16), 4.2, sr)
    place(buf, scale(rooster(sr, r), 0.1), 12.8, sr)
    for at in (1.2, 5.6, 9.7, 14.3):
        place(buf, scale(creak(sr, r, 190 + 30 * r.random()), 0.12), at, sr)
    # grass and a lazy cricket-less hush: a soft high bed so the loop is never silent
    buf = mix(buf, scale(highpass(noise(L + fade, r, sr), 4500, sr), 0.012))
    return normalise(loopable(buf, fade, sr), 0.6), sr


def meadow_burning():
    sr, L, fade = AMB_SR, 18.0, 1.5
    r = random.Random('meadow_burning')
    buf = wind(L + fade, r, sr, 1.5, 0.5, 700)
    roar = lowpass(noise(L + fade, r, sr), 350, sr)
    roar = [s * (0.7 + 0.3 * math.sin(2 * math.pi * 0.31 * i / sr)) for i, s in enumerate(roar)]
    buf = mix(buf, scale(roar, 1.4), crackle(L + fade, r, sr, 90, 0.9))
    # the village bell, struck out of time: an alarm
    for at, f in ((0.6, 523), (2.1, 523), (3.3, 494), (6.9, 523), (8.2, 523), (9.6, 494), (13.0, 523), (14.6, 523)):
        place(buf, scale(bell(sr, f, 2.6), 0.2), at, sr)
    place(buf, scale(rooster(sr, r), 0.1), 11.4, sr)  # one rooster, frightened, far off
    return normalise(loopable(buf, fade, sr), 0.65), sr


def floor1000():
    sr, L, fade = AMB_SR, 20.0, 1.5
    r = random.Random('floor1000')
    n = n_of(L + fade, sr)
    rumble = lowpass(noise(L + fade, r, sr), 140, sr)
    rumble = [s * (0.65 + 0.35 * math.sin(2 * math.pi * 0.12 * i / sr)) for i, s in enumerate(rumble)]
    hiss = lowpass(highpass(noise(L + fade, r, sr), 1800, sr), 6000, sr)
    hiss = [s * (0.25 + 0.15 * math.sin(2 * math.pi * 0.2 * i / sr + 2)) for i, s in enumerate(hiss)]
    buf = mix(scale(rumble, 4.0), scale(hiss, 0.5))
    # lava bubbles: low chirps with a wet pop
    for _ in range(26):
        at = r.random() * (L - 0.5)
        f0 = 90 + 120 * r.random()
        bub = decay(sweep(f0, f0 * (2.2 + r.random()), 0.14, sr), 0.07, 0.004, sr)
        place(buf, scale(bub, 0.35 + 0.4 * r.random()), at, sr)
    # stone creaks, now and then
    for at in (2.5, 7.8, 12.1, 16.9):
        place(buf, scale(creak(sr, r, 120 + 40 * r.random(), 0.9), 0.2), at, sr)
    place(buf, scale(crackle(L + fade, r, sr, 18, 0.7), 1.0), 0.0, sr)
    # far screams and a ticket-desk bell (the Landing's desk)
    for at in (5.0, 14.5):
        scream = decay(mix(sweep(380, 520, 0.35, sr), sweep(520, 300, 0.4, sr)), 0.35, 0.02, sr)
        place(buf, scale(lowpass(scream, 1200, sr), 0.1), at, sr)
    place(buf, scale(bell(sr, 990, 1.6), 0.12), 8.5, sr)
    return normalise(loopable(buf, fade, sr), 0.6), sr


def shaft_bed():
    sr, L, fade = AMB_SR, 22.0, 1.5
    r = random.Random('ambience_shaft')
    buf = wind(L + fade, r, sr, 1.8, 0.55, 220)
    pipe = lowpass(noise(L + fade, r, sr), 90, sr)
    pipe = [s * (0.7 + 0.3 * math.sin(2 * math.pi * 0.07 * i / sr)) for i, s in enumerate(pipe)]
    buf = mix(buf, scale(pipe, 2.2))
    for at in (1.4, 4.8, 9.2, 13.6, 18.1):
        ratt = decay(highpass(noise(0.35, r, sr), 800, sr), 0.12, 0.002, sr)
        place(buf, scale(ratt, 0.18), at, sr)
    # far band echo: soft filtered pulse
    for at in (3.0, 11.5, 19.0):
        echo = decay(tone(110, 1.4, sr), 0.7, 0.05, sr)
        place(buf, scale(lowpass(echo, 400, sr), 0.12), at, sr)
    return normalise(loopable(buf, fade, sr), 0.58), sr


def floor999():
    sr, L, fade = AMB_SR, 22.0, 1.5
    r = random.Random('ambience_floor999')
    roar = lowpass(noise(L + fade, r, sr), 160, sr)
    roar = [s * (0.7 + 0.3 * math.sin(2 * math.pi * 0.11 * i / sr)) for i, s in enumerate(roar)]
    buf = scale(roar, 3.2)
    buf = mix(buf, scale(wind(L + fade, r, sr, 0.8, 0.3, 500), 0.5))
    for at in (1.0, 3.5, 6.2, 9.0, 12.4, 15.8, 19.2):
        hit = decay(mix(tone(90, 0.08, sr), highpass(noise(0.05, r, sr), 2000, sr)), 0.05, 0.001, sr)
        place(buf, scale(hit, 0.35), at, sr)
    return normalise(loopable(buf, fade, sr), 0.6), sr


def floor998():
    sr, L, fade = AMB_SR, 22.0, 1.5
    r = random.Random('ambience_floor998')
    buf = wind(L + fade, r, sr, 0.7, 0.2, 900)
    drip = [0.0] * n_of(L + fade, sr)
    for _ in range(40):
        at = r.random() * (L - 0.2)
        drop = decay(tone(1400 + 600 * r.random(), 0.04, sr), 0.03, 0.001, sr)
        place(drip, scale(drop, 0.2), at, sr)
    # bone wind chimes: thin metallic
    for at in (2.0, 7.5, 14.0):
        for k, f in enumerate((880, 990, 1170, 1320)):
            place(buf, scale(decay(tone(f, 0.9, sr), 0.45, 0.002, sr), 0.08), at + 0.12 * k, sr)
    choir = [0.35 * math.sin(2 * math.pi * 220 * i / sr) * math.sin(2 * math.pi * 0.05 * i / sr)
             for i in range(n_of(L + fade, sr))]
    buf = mix(buf, drip, scale(lowpass(choir, 600, sr), 0.25))
    return normalise(loopable(buf, fade, sr), 0.55), sr


def floor997():
    sr, L, fade = AMB_SR, 22.0, 1.5
    r = random.Random('ambience_floor997')
    n = n_of(L + fade, sr)
    drone = [0.0] * n
    for partial, amp in ((55, 0.55), (82.5, 0.35), (110, 0.25), (165, 0.12)):
        for i in range(n):
            drone[i] += amp * math.sin(2 * math.pi * partial * i / sr)
    drone = [s * (0.85 + 0.15 * math.sin(2 * math.pi * 0.04 * i / sr)) for i, s in enumerate(drone)]
    buf = mix(drone, scale(wind(L + fade, r, sr, 0.5, 0.15, 300), 0.4))
    # distant Overlord laugh: low filtered burble twice per loop
    for at in (5.5, 16.0):
        laugh = decay(mix(tone(140, 0.5, sr), tone(180, 0.4, sr), lowpass(noise(0.5, r, sr), 400, sr)), 0.35, 0.04, sr)
        place(buf, scale(laugh, 0.14), at, sr)
    return normalise(loopable(buf, fade, sr), 0.58), sr


def oneshot_lava_pop(take=0):
    r = random.Random(f'amb_lava_pop{take}')
    f0 = 90 + 40 * take
    return normalise(decay(mix(sweep(f0, f0 * 2.4, 0.12, SR), highpass(noise(0.06, r), 1200, SR)), 0.08, 0.002), 0.7)


def oneshot_chain(take=0):
    r = random.Random(f'amb_chain_rattle{take}')
    parts = []
    for i in range(4 + take % 2):
        parts.append(delay(decay(highpass(noise(0.04, r), 1500, SR), 0.03, 0.001), 0.05 * i))
    return normalise(mix(*parts), 0.65)


def oneshot_torch(take=0):
    r = random.Random(f'amb_torch_crackle{take}')
    return normalise(crackle(0.45, r, SR, 80, 1.0), 0.55)


def oneshot_imp(take=0):
    r = random.Random(f'amb_imp_chatter{take}')
    parts = [scale(highpass(noise(0.35, r), 2000, SR), 0.1)]
    for k in range(3):
        f = 420 + 80 * k + 30 * take
        parts.append(delay(decay(tone(f, 0.12, SR, squareish), 0.08, 0.004), 0.1 * k))
    return normalise(mix(*parts), 0.5)


def oneshot_bone(take=0):
    r = random.Random(f'amb_bone_clatter{take}')
    hits = []
    for i in range(3):
        hits.append(delay(decay(mix(tone(600 + 100 * i, 0.05, SR), highpass(noise(0.03, r), 2500, SR)), 0.04, 0.001), 0.07 * i))
    return normalise(mix(*hits), 0.6)


def oneshot_steam(take=0):
    r = random.Random(f'amb_steam_hiss{take}')
    return normalise(decay(highpass(noise(0.55, r), 1800, SR), 0.25, 0.02), 0.5)


def oneshot_bell(take=0):
    return normalise(scale(bell(SR, 880 + 40 * take, 1.2), 0.8), 0.55)


def oneshot_hammer(take=0):
    r = random.Random(f'amb_hammer_strike{take}')
    return normalise(decay(mix(tone(70, 0.06, SR), highpass(noise(0.05, r), 900, SR)), 0.07, 0.001), 0.7)


def oneshot_drip(take=0):
    return normalise(decay(tone(1600 + 200 * take, 0.06, SR), 0.05, 0.001), 0.45)


def oneshot_chime(take=0):
    f = 990 + 110 * take
    return normalise(decay(mix(tone(f, 0.8, SR), tone(f * 1.5, 0.6, SR)), 0.4, 0.002), 0.45)


def oneshot_rooster(take=0):
    r = random.Random(f'amb_rooster_crow{take}')
    return normalise(scale(rooster(SR, r), 0.9), 0.55)


def oneshot_mill(take=0):
    r = random.Random(f'amb_mill_creak{take}')
    return normalise(scale(creak(SR, r, 180 + 20 * take, 0.75), 0.9), 0.5)


def oneshot_death_sting(take=0):
    """Low brass hit and grit, cut short: a falling minor-second pair over a thud (Hawkes Gen's version replaces it)."""
    r = random.Random('ui_death_sting')
    parts = []
    for f in (98.0, 103.8, 146.8):
        n = n_of(0.9)
        ph, buf = 0.0, []
        for i in range(n):
            t = i / n
            ph += 2 * math.pi * f * (1 - 0.12 * t) / SR
            buf.append(saw(ph))
        parts.append(scale(decay(lowpass(buf, 1800), 0.3, 0.012), 0.5))
    thud = decay(sweep(120, 42, 0.3), 0.1)
    grit = decay(lowpass(noise(0.4, r), 2600), 0.08)
    return normalise(fade_edges(mix(*parts, scale(thud, 1.0), scale(grit, 0.4)), 0.004, 0.06), 0.85)


BEDS = {
    'ambience_meadow_calm': meadow_calm,
    'ambience_meadow_burning': meadow_burning,
    'ambience_floor1000': floor1000,
    'ambience_shaft': shaft_bed,
    'ambience_floor999': floor999,
    'ambience_floor998': floor998,
    'ambience_floor997': floor997,
}

ONESHOTS = {
    'ui_death_sting': oneshot_death_sting,
    'amb_lava_pop': oneshot_lava_pop,
    'amb_chain_rattle': oneshot_chain,
    'amb_torch_crackle': oneshot_torch,
    'amb_imp_chatter': oneshot_imp,
    'amb_bone_clatter': oneshot_bone,
    'amb_steam_hiss': oneshot_steam,
    'amb_ticket_bell': oneshot_bell,
    'amb_hammer_strike': oneshot_hammer,
    'amb_drip': oneshot_drip,
    'amb_bone_chime': oneshot_chime,
    'amb_rooster_crow': oneshot_rooster,
    'amb_mill_creak': oneshot_mill,
}


# ─── output ───────────────────────────────────────────────────────

META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: {load}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: {quality}
    conversionMode: 0
    preloadAudioData: {preload}
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: {background}
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def existing_guid(meta_path):
    if os.path.exists(meta_path):
        for line in open(meta_path, encoding='utf-8'):
            if line.startswith('guid: '):
                return line.split()[1]
    # stable across runs: a name-based UUID, so a re-run never changes what references it
    return uuid.uuid5(uuid.NAMESPACE_URL, 'sickofit/' + os.path.relpath(meta_path, OUT)).hex


def write(name, samples, sr, loop):
    os.makedirs(OUT, exist_ok=True)
    wav = tempfile.NamedTemporaryFile(suffix='.wav', delete=False)
    wav.close()
    with wave.open(wav.name, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(array.array('h', [int(max(-1, min(1, s)) * 32000) for s in samples]).tobytes())
    path = os.path.join(OUT, name + '.ogg')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav.name, '-ac', '1', '-c:a', 'libvorbis',
                    '-q:a', '3' if loop else '4', '-map_metadata', '-1', path], check=True)
    os.unlink(wav.name)
    meta = path + '.meta'
    guid = existing_guid(meta)
    with open(meta, 'w', encoding='utf-8', newline='\n') as f:
        f.write(META.format(guid=guid, load=1 if loop else 0, quality='0.4' if loop else '0.5',
                            preload=0 if loop else 1, background=1 if loop else 0))
    print(f'{name}.ogg  {len(samples) / sr:.2f}s  {os.path.getsize(path) / 1024:.1f} KB')


def main():
    args = sys.argv[1:]
    only = set()
    if '--only' in args:
        only = set(args[args.index('--only') + 1:])
    if '--list' in args:
        for k, (_, n) in SOUNDS.items():
            print(k, n)
        for k in BEDS:
            print(k, 'loop')
        for k in ONESHOTS:
            print(k, 'oneshot')
        return
    for name, (fn, takes) in SOUNDS.items():
        if only and name not in only:
            continue
        for take in range(takes):
            write(f'{name}_t{take + 1}', fn(take), SR, False)
    for name, fn in BEDS.items():
        if only and name not in only:
            continue
        samples, sr = fn()
        write(name, samples, sr, True)
    for name, fn in ONESHOTS.items():
        if only and name not in only:
            continue
        write(name, fn(0), SR, False)


if __name__ == '__main__':
    main()
