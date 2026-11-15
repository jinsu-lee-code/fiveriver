# A normal map with the skin's fine grain only, made from the base colour (2026-11-16).
# The generated busts came with a normal map that carried the mesh's own lumps (ridged brows, a moustache like a
# scrubbing brush, pitted cheeks); lit in the game they read as dark smudges over the face. The surface is left
# smooth here and only small detail (pores, creases, the weave of the cap) is kept: the high-pass of the colour's
# brightness, as a height, turned into a tangent-space normal (OpenGL/Unity: green points up the texture).
#   python detail_normal.py <basecolor.png> <normal out.png> [strength] [radius px]
import sys
import numpy as np
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
strength = float(sys.argv[3]) if len(sys.argv) > 3 else 2.0
radius = int(sys.argv[4]) if len(sys.argv) > 4 else 5


def blur(a, r):
    # three box blurs ~ a gaussian
    for _ in range(3):
        c = np.cumsum(np.pad(a, ((r + 1, r), (0, 0)), mode='edge'), 0); a = (c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)
        c = np.cumsum(np.pad(a, ((0, 0), (r + 1, r)), mode='edge'), 1); a = (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)
    return a


rgb = np.asarray(Image.open(src).convert('RGB')).astype(float) / 255.0
lum = rgb @ np.array([0.299, 0.587, 0.114])
height = lum - blur(lum, radius)
height = np.clip(height, -0.08, 0.08)          # strong edges (eyelids, nostrils, hair line) do not become cliffs
gx = np.gradient(height, axis=1); gy = np.gradient(height, axis=0)
n = np.dstack([-gx * strength * 10, gy * strength * 10, np.ones_like(height)])   # rows run down the image
n /= np.linalg.norm(n, axis=2, keepdims=True)
Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8)).save(dst)
print('WROTE', dst)
