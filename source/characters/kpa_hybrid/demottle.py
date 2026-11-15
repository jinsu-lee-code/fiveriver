# Evens out the blotches in the baked uniform (2026-11-16). The generated map's islands each had their own
# brightness; baked onto large islands they stay as camouflage-like patches a hand wide. On the khaki cloth only
# (leather, boots, skin and buttons are left alone) the brightness at that scale is pulled most of the way toward
# the brightness of the wider area, measured inside the baked islands only; finer detail (seams, creases, weave)
# is kept as it is.
#   python demottle.py <basecolor.png> [strength 0..1] [patch radius px] [area radius px]
import sys
import numpy as np
from PIL import Image

path = sys.argv[1]
strength = float(sys.argv[2]) if len(sys.argv) > 2 else 0.75
r_patch = int(sys.argv[3]) if len(sys.argv) > 3 else 24
r_area = int(sys.argv[4]) if len(sys.argv) > 4 else 160


def box(a, r):
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]; pad[axis] = (r + 1, r)
        c = np.cumsum(np.pad(a, pad, mode='edge'), axis)
        a = (np.take(c, range(2 * r + 1, c.shape[axis]), axis) - np.take(c, range(0, c.shape[axis] - 2 * r - 1), axis)) / (2 * r + 1)
    return a


def masked_blur(v, m, r):
    a, b = v * m, m.astype(float)
    for _ in range(2): a, b = box(a, r), box(b, r)
    return a / np.maximum(b, 1e-4)


rgb = np.asarray(Image.open(path).convert('RGB')).astype(np.float32) / 255.0
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
cloth = (np.abs(r - g) < 0.09) & (g - b > 0.04) & (lum > 0.15) & (lum < 0.8)
patch = masked_blur(lum, cloth, r_patch)
area = masked_blur(lum, cloth, r_area)
gain = np.clip((area / np.maximum(patch, 1e-3)) ** strength, 0.7, 1.4)
gain = np.where(cloth, gain, 1.0)
# soften the edge of the cloth mask so leather and skin borders do not get a rim
soft = masked_blur(cloth.astype(np.float32), np.ones_like(lum, bool), 2)
gain = 1.0 + (gain - 1.0) * soft
out = np.clip(rgb * gain[..., None], 0, 1)
Image.fromarray((out * 255 + 0.5).astype(np.uint8)).save(path)
print('DEMOTTLED', path, 'cloth share %.2f, gain %.2f..%.2f' % (cloth.mean(), gain.min(), gain.max()))
