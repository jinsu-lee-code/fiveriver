# Turns the three generated images into the poplar's game textures (2026-11-15).
#   poplar_branches.png  six leafy branches on black   -> T_Poplar_Branches.png (RGBA, colour bled under the alpha)
#   poplar_tree.png      the whole tree on white       -> T_Poplar_Far.png      (RGBA, for the far cross-planes)
#   poplar_bark.png      bark                          -> T_Poplar_Bark.png     (made to tile, darkened a little)
# Also writes branches.json: the box of each branch in the atlas (u0, v0, u1, v1, with v up) and where its stem
# enters (the foot of the twig), for make_poplar.py.
#   python prep_textures.py <dir with the three source images> <out dir>
import sys, json
import numpy as np
from PIL import Image, ImageFilter

src, out = sys.argv[1], sys.argv[2]


def bleed(rgb, alpha, rounds=24):
    # pushes the colour of the opaque texels outward so mips and filtering never pick up the background
    rgb = rgb.astype(np.float64).copy(); mask = alpha > 0.5
    for _ in range(rounds):
        if mask.all(): break
        acc = np.zeros_like(rgb); cnt = np.zeros(mask.shape)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            sm = np.roll(mask, (dy, dx), (0, 1)); acc += np.roll(rgb, (dy, dx), (0, 1)) * sm[..., None]; cnt += sm
        new = (~mask) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][:, None]; mask |= new
    rgb[~mask] = rgb[mask].mean(0)
    return rgb


# ---- branches: black background -> alpha
im = np.asarray(Image.open(src + '/poplar_branches.png').convert('RGB'), np.float64) / 255
lum = im.max(2)
a = np.clip((lum - 0.07) / 0.10, 0, 1)
a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float64) / 255
rgb = bleed(im, a)
# August in a drought year: the leaves are a tired, dusty green, not a spring green
rgb = rgb * np.array([0.92, 0.86, 0.74]) + np.array([0.035, 0.03, 0.01])
H, W = a.shape
Image.fromarray(np.dstack([np.clip(rgb, 0, 1), a]).__mul__(255).astype(np.uint8), 'RGBA').save(out + '/T_Poplar_Branches.png')
boxes = []
for r in range(2):
    for c in range(3):
        y0, y1, x0, x1 = r * H // 2, (r + 1) * H // 2, c * W // 3, (c + 1) * W // 3
        cell = a[y0:y1, x0:x1] > 0.3
        ys, xs = np.where(cell)
        if len(ys) == 0: continue
        bx0, bx1, by0, by1 = xs.min() + x0 - 4, xs.max() + x0 + 4, ys.min() + y0 - 4, ys.max() + y0 + 4
        # the foot of the twig: the lowest opaque texels
        low = ys > ys.max() - 6
        foot = (xs[low].mean() + x0, ys.max() + y0)
        boxes.append(dict(u0=bx0 / W, u1=bx1 / W, v0=1 - by1 / H, v1=1 - by0 / H, foot_u=foot[0] / W, foot_v=1 - foot[1] / H,
                          aspect=(bx1 - bx0) / (by1 - by0)))
json.dump(boxes, open(out + '/branches.json', 'w'), indent=1)

# ---- whole tree: white background -> alpha
im = np.asarray(Image.open(src + '/poplar_tree.png').convert('RGB'), np.float64) / 255
whiteness = im.min(2)
a = np.clip((0.93 - whiteness) / 0.10, 0, 1)
a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)), np.float64) / 255
# the far planes are matched to the near foliage, which reads darker and greener once it is lit as a volume
rgb = bleed(im, a) * np.array([0.62, 0.74, 0.52]) + np.array([0.02, 0.025, 0.01])
ys, xs = np.where(a > 0.3)
crop = (xs.min() - 6, ys.min() - 6, xs.max() + 6, ys.max() + 2)
far = Image.fromarray((np.dstack([np.clip(rgb, 0, 1), a]) * 255).astype(np.uint8), 'RGBA').crop(crop)
far = far.resize((256, 1024), Image.LANCZOS)
far.save(out + '/T_Poplar_Far.png')
json.dump(dict(far_aspect=(crop[2] - crop[0]) / (crop[3] - crop[1])), open(out + '/far.json', 'w'))

# ---- bark: made to tile by cross-fading the image with itself shifted half a tile
b = np.asarray(Image.open(src + '/poplar_bark.png').convert('RGB').resize((1024, 1024), Image.LANCZOS), np.float64) / 255
n = b.shape[0]
shift = np.roll(b, (n // 2, n // 2), (0, 1))
yy, xx = np.mgrid[0:n, 0:n] / (n - 1)
edge = np.minimum(np.minimum(xx, 1 - xx), np.minimum(yy, 1 - yy))     # 0 at the border, 0.5 in the middle
w = np.clip(edge / 0.18, 0, 1)[..., None]
t = b * w + shift * (1 - w)
g = t @ np.array([0.3, 0.59, 0.11])
# grey-brown poplar bark at a middle value: the generated picture came out near black and blue
t = np.clip((g[..., None] - g.mean()) * 0.85 + 0.36, 0, 1) * np.array([1.0, 0.93, 0.84])
Image.fromarray((t * 255).astype(np.uint8)).resize((512, 512), Image.LANCZOS).save(out + '/T_Poplar_Bark.png')
print('branches', len(boxes), 'far crop', crop)
