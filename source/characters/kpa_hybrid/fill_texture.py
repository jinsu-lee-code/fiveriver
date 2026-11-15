# Fills the empty (black) texels of a baked map from the baked texels around them (push-pull: averaged down a
# pyramid of the valid texels, then pulled back up into the holes), so that mip levels and filtering at island
# borders never pull black into the face. Normal maps are filled the same way.
#   python fill_texture.py <png> [<png> ...]
import sys
import numpy as np
from PIL import Image


def push_pull(colour, valid):
    if min(colour.shape[:2]) <= 1 or valid.all():
        return colour
    h, w = colour.shape[:2]
    h2, w2 = (h + 1) // 2, (w + 1) // 2
    c = np.zeros((h2 * 2, w2 * 2, 3)); v = np.zeros((h2 * 2, w2 * 2))
    c[:h, :w] = colour * valid[..., None]; v[:h, :w] = valid
    cs = c.reshape(h2, 2, w2, 2, 3).sum((1, 3)); vs = v.reshape(h2, 2, w2, 2).sum((1, 3))
    small = np.where(vs[..., None] > 0, cs / np.maximum(vs, 1)[..., None], 0)
    small = push_pull(small, vs > 0)
    up = np.repeat(np.repeat(small, 2, 0), 2, 1)[:h, :w]
    return np.where(valid[..., None], colour, up)


for path in sys.argv[1:]:
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    valid = a.sum(2) >= 6
    out = push_pull(a, valid)
    Image.fromarray(np.clip(out + 0.5, 0, 255).astype(np.uint8)).save(path)
    print('FILLED', path, 'empty share %.3f' % (1 - valid.mean()))
