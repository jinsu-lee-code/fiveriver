# Pads the UV islands of the baked character textures (2026-11-15).
# The baked sets are many small islands on a black background with no padding, so filtering and mips pulled
# the black in along every seam: black streaks across faces and necks. The island colours are pushed out into
# the background (push-pull), which leaves every island texel as it was.
#   python pad_islands.py <backup dir> <texture> [<texture> ...]      (the original is copied to the backup dir first)
import sys, os, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

bk = sys.argv[1]


def background(rgb):
    # the background is the flat colour that covers most of the border (near black, not always pure black)
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    q = (border * 255).astype(np.int32); keys = q[:, 0] * 65536 + q[:, 1] * 256 + q[:, 2]
    vals, counts = np.unique(keys, return_counts=True); k = vals[counts.argmax()]
    flat = np.array([k // 65536, (k // 256) % 256, k % 256], np.float32) / 255
    dark = (np.abs(rgb - flat).max(2) < 0.035) & (rgb.max(2) < 0.2)
    h, w = dark.shape
    # only the dark region joined to the border is background: black boots or hair inside an island stay
    s = 4 if h >= 2048 else 2
    small = Image.fromarray((dark[::s, ::s] * 255).astype(np.uint8)).copy()   # a copy: fromarray shares a read-only buffer the fill cannot write
    W, H = small.size
    seeds = [(x, y) for x in range(0, W, 8) for y in (0, H - 1)] + [(x, y) for y in range(0, H, 8) for x in (0, W - 1)]
    for seed in seeds:
        if small.getpixel(seed) == 255: ImageDraw.floodfill(small, seed, 128)
    joined = np.asarray(small) == 128
    joined = np.repeat(np.repeat(joined, s, 0), s, 1)[:h, :w]
    return dark & joined


def pushpull(rgb, valid):
    levels = [(rgb * valid[..., None], valid.astype(np.float32))]
    while levels[-1][0].shape[0] > 1:
        c, w = levels[-1]
        h2, w2 = c.shape[0] // 2, c.shape[1] // 2
        c2 = c[:h2 * 2, :w2 * 2].reshape(h2, 2, w2, 2, 3).sum((1, 3)); ww = w[:h2 * 2, :w2 * 2].reshape(h2, 2, w2, 2).sum((1, 3))
        levels.append((c2, ww))
    fill = levels[-1][0] / np.maximum(levels[-1][1][..., None], 1e-6)
    for c, w in reversed(levels[:-1]):
        up = np.repeat(np.repeat(fill, 2, 0), 2, 1)
        if up.shape[0] < c.shape[0] or up.shape[1] < c.shape[1]:
            up = np.pad(up, ((0, c.shape[0] - up.shape[0]), (0, c.shape[1] - up.shape[1]), (0, 0)), mode='edge')
        own = c / np.maximum(w[..., None], 1e-6)
        fill = np.where(w[..., None] > 0, own, up)
    return fill


for f in sys.argv[2:]:
    name = '__'.join(f.replace(chr(92), '/').split('/'))
    orig = os.path.join(bk, name)
    if not os.path.exists(orig): shutil.copy(f, orig)
    im = Image.open(f)
    rgb = np.asarray(im.convert('RGB'), np.float32) / 255
    # the mask comes from the untouched original (some of these files were colour-corrected earlier today)
    src = np.asarray(Image.open(orig).convert('RGB'), np.float32) / 255
    bg = background(src if src.shape == rgb.shape else rgb)
    frac = bg.mean()
    if frac < 0.08 or frac > 0.97:
        print('skip  %4.0f%% %s' % (frac * 100, f)); continue
    # the rim of every island was baked half-blended with the black, so the outermost texels are dark too:
    # they are treated as background and take the colour just inside them
    rim = 5 if rgb.shape[0] >= 4096 else 3 if rgb.shape[0] >= 2048 else 2
    grown = np.asarray(Image.fromarray((bg * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * rim + 1))) > 0
    out = pushpull(rgb, ~grown)
    res = Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8))
    if im.mode == 'RGBA': res.putalpha(im.split()[3])
    res.save(f)
    print('padded %3.0f%% %s' % (frac * 100, f))
