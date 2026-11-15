# The wreck's base colour: the tank's own texture burnt (paint gone to black scale, fire-rust where it burnt
# hottest, grey ash in soft patches).   python burnt_texture.py <T_VH_T34_V5_BaseColor.png> <out.png>
import sys
import numpy as np
from PIL import Image
a = np.asarray(Image.open(sys.argv[1]).convert('RGB'), np.float32) / 255
n = a.shape[0]; rng = np.random.default_rng(1950)
def noise(scale):
    g = rng.random((n // scale + 2, n // scale + 2)).astype(np.float32)
    return np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((n + scale, n + scale), Image.BICUBIC), np.float32)[:n, :n] / 255
big = noise(96) * 0.6 + noise(32) * 0.4; fine = noise(6); mid = noise(20)
lum = a @ np.array([0.3, 0.59, 0.11])
soot, burnt, ash = np.array([0.035, 0.032, 0.03]), np.array([0.20, 0.10, 0.055]), np.array([0.16, 0.155, 0.15])
out = soot[None, None] * (0.6 + 0.9 * lum[..., None])
r = np.clip((big - 0.48) / 0.2, 0, 1)[..., None]; out = out * (1 - 0.75 * r) + burnt[None, None] * (0.5 + 0.8 * fine[..., None]) * 0.75 * r
s = np.clip((mid - 0.62) / 0.2, 0, 1)[..., None] * np.clip((0.5 - big) / 0.2, 0, 1)[..., None]; out = out * (1 - 0.45 * s) + ash[None, None] * 0.45 * s
Image.fromarray((np.clip(out, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8)).save(sys.argv[2])
