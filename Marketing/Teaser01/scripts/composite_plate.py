"""The exterior plate: the outpainted left (Gen a_ab22660494cb56d9) feathered into the real menu frame."""
import sys
import numpy as np
from PIL import Image
out = np.asarray(Image.open(sys.argv[1]).convert('RGB'), np.float32)      # 1680x720 outpaint
menu = np.asarray(Image.open(sys.argv[2]).convert('RGB'), np.float32)     # 1280x720 menu frame
plate = out.copy()
x = np.arange(1680)
a = np.clip((x - 400) / 16.0, 0, 1)[None, :, None]                       # seam on the menu frame's left edge
orig = np.zeros_like(out); orig[:, 400:] = menu
plate = out * (1 - a) + orig * a
Image.fromarray(plate.clip(0, 255).astype(np.uint8)).save(sys.argv[3])
