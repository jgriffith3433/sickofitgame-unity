"""The interior plate: paints out the stray red glint on the hero's helmet in Gen's take (a_d4966297a69ccee1),
so his visor is dark while he sleeps and the glow on the line is the reveal.
Usage: python sleepfix.py <take.png> <sleep.png>"""
import sys
import numpy as np
from PIL import Image

im = np.array(Image.open(sys.argv[1]).convert('RGB')).astype(np.float32)
H, W, _ = im.shape
yy, xx = np.mgrid[0:H, 0:W]
cx, cy = 479, 492                      # the glint, 1664x928 px
d = np.hypot(xx - cx, yy - cy)
ring = (d > 14) & (d < 20)
rv = im[ring]; rv = rv[rv.sum(1) > 90]
fill = np.median(rv, axis=0)           # the helmet's gray around it
mask = (d <= 13) & ((im[..., 0] > im[..., 1] + 12) | (d <= 10))
alpha = np.clip((13.5 - d) / 2, 0, 1) * mask
noise = np.random.default_rng(1).normal(0, 2, im.shape)
im = im * (1 - alpha[..., None]) + (fill + noise) * alpha[..., None]
Image.fromarray(im.clip(0, 255).astype(np.uint8)).save(sys.argv[2])
