# M1 Garand surface maps from the baked masks (bake_masks.py) (2026-11-23, "사실적인 질감으로").
# Until now the steel was one even blue-black with no wear and the walnut an even brown: both read as plastic.
#   steel: parkerized (grey-green manganese phosphate, matte), worn through to bright steel on the edges and where
#          hands and the sling rub, oil and dust dark in the corners, fine scratches along the rifle, a little rust
#   walnut: the grain of the old map kept; oil-darkened where it is handled, the finish rubbed pale on the edges,
#          dirt in the recesses, dents and scratches, a soft sheen that breaks up
# Out: <name>_BaseColor.png (sRGB) and <name>_Mask.png (R metallic, G occlusion, A smoothness - URP Lit).
# The normal maps are not touched (they carry the receiver's lettering).
#
#   python compose_maps.py <mask dir> <old texture dir list...> <out dir>
import sys, os
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
masks, weapons, out = sys.argv[1:4]
OLD = {'Metal': ('M1Refined/Source/Art/M1ExteriorRefine20261023/Metal_BaseColor.png', 'M1Handling20261102/Surface20261106/M1_View_Metal_SurfaceNormal.png'),
       'Hardware': ('M1Refined/Source/Art/M1ExteriorRefine20261023/Hardware_BaseColor.png', 'M1Refined/Source/Art/M1ExteriorRefine20261023/Hardware_Normal.png'),
       'Wood': ('M1Refined/Source/Art/M1ExteriorRefine20261023/Wood_BaseColor.png', 'M1Handling20261102/Surface20261106/M1_View_Wood_SurfaceNormal.png')}


def load(path, size, mode='RGB'):
    im = Image.open(path).convert(mode)
    if im.size != (size, size): im = im.resize((size, size), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255.0


def ss(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def to_srgb(lin): return np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(np.clip(lin, 0, 1), 1 / 2.4) - 0.055)
def to_lin(s): return np.where(s <= 0.04045, s / 12.92, np.power((s + 0.055) / 1.055, 2.4))
def mix(a, b, t): return a * (1 - t[..., None]) + b * t[..., None]
def col(*rgb): return np.array(rgb, np.float32)


def stats(name, a): print('   %-10s mean %.3f  min %.3f  max %.3f' % (name, a.mean(), a.min(), a.max()))


for name, size in (('Metal', 4096), ('Hardware', 2048), ('Wood', 4096)):
    m = lambda k: load(os.path.join(masks, 'mask_%s_%s.png' % (name, k)), size)
    ao = m('ao')[..., 0]; edge = m('edge'); nA = m('noiseA'); nB = m('noiseB'); place = m('place')
    old_base, old_normal = (load(os.path.join(weapons, p), size) for p in OLD[name])
    used = old_base.sum(-1) > 0.02                                    # texels some face uses
    fine, mid, broad = edge[..., 0], edge[..., 1], edge[..., 2]
    cloud, blotch, grain = nA[..., 0], nA[..., 1], nA[..., 2]
    longs, cross, specks = nB[..., 0], nB[..., 1], nB[..., 2]
    cavity = np.clip((1 - ao) * 1.6, 0, 1)
    engraved = np.clip((1 - old_normal[..., 2]) * 6.0, 0, 1)             # lettering and cut lines of the normal map
    along = place[..., 0]
    print(name, size); stats('ao', ao); stats('edge', fine); stats('cloud', cloud)

    if name != 'Wood':
        # ---- parkerized steel (linear colours)
        park = col(0.046, 0.052, 0.046) * (0.80 + 0.45 * cloud[..., None]) * (0.92 + 0.16 * blotch[..., None])
        park = park * (1 + (grain[..., None] - 0.5) * 0.25)
        # worn through: on the edges first, then in patches where it is carried (noise decides how far it has gone)
        wear = ss(0.10, 0.42, fine * (0.45 + 1.1 * blotch)) + 0.5 * ss(0.25, 0.7, mid * (0.3 + 1.2 * cloud))
        rub = ss(0.62, 0.80, cloud * 0.6 + blotch * 0.5) * 0.35 * (1 - cavity)        # handling polish on the flats
        scratch = ss(0.70, 0.78, longs) * ss(0.35, 0.6, blotch) * 0.6 + ss(0.76, 0.82, cross) * ss(0.5, 0.7, cloud) * 0.4
        bare = np.clip(wear + scratch * (1 - cavity), 0, 1)
        steel = col(0.50, 0.50, 0.48) * (0.85 + 0.3 * grain[..., None])
        base = mix(park, col(0.105, 0.108, 0.10) * (0.9 + 0.2 * grain[..., None]), np.clip(rub, 0, 1))   # thinned coating: greyer
        base = mix(base, steel, bare)
        # oil and dust in the corners and the lettering, a little rust where the coating is gone and damp sits
        grime = np.clip(cavity * (0.6 + 0.6 * blotch) + engraved * 0.5, 0, 1)
        base = mix(base, col(0.022, 0.020, 0.017), grime * 0.8)
        rust = ss(0.80, 0.88, blotch * 0.55 + cloud * 0.35 + cavity * 0.25) * ss(0.2, 0.5, specks) * (1 - bare) * 0.7
        base = mix(base, col(0.16, 0.065, 0.028), rust)
        metallic = np.clip(0.78 + 0.22 * bare - 0.45 * rust - 0.25 * grime, 0, 1)
        smooth = 0.34 + 0.10 * (cloud - 0.5) + 0.20 * rub + 0.34 * bare - 0.16 * grime - 0.22 * rust + (grain - 0.5) * 0.08
    else:
        # ---- oiled walnut: the old map's grain, re-toned
        grainmap = to_lin(old_base)
        lum = grainmap.mean(-1, keepdims=True); mean = lum[used].mean()
        tone = col(0.092, 0.040, 0.018) * (lum / mean) ** 1.2              # deeper contrast in the figure
        tone = tone * (0.90 + 0.22 * cloud[..., None]) * (0.96 + 0.08 * blotch[..., None])
        handled = ss(0.50, 0.78, cloud * 0.55 + blotch * 0.55) * (1 - cavity)   # hands' oil: darker, redder, shinier
        tone = mix(tone, tone * col(0.70, 0.60, 0.52), handled * 0.6)
        worn = np.clip(ss(0.15, 0.6, fine * (0.4 + 1.2 * blotch)) * 0.9 + ss(0.35, 0.85, mid * (0.3 + cloud)) * 0.35, 0, 1)
        tone = mix(tone, col(0.20, 0.115, 0.058) * (lum / mean) ** 0.6, worn * 0.75)          # finish rubbed off: pale, dry
        dents = ss(0.10, 0.03, specks) * ss(0.45, 0.6, blotch)
        scr = ss(0.74, 0.80, longs) * ss(0.45, 0.65, cloud) * 0.5 + ss(0.78, 0.83, cross) * ss(0.5, 0.7, blotch) * 0.5
        tone = mix(tone, col(0.035, 0.018, 0.010), np.clip(dents * 0.8 + cavity * (0.5 + 0.5 * blotch) * 0.85, 0, 1))
        tone = mix(tone, col(0.22, 0.13, 0.07), scr * 0.45)
        base = tone
        metallic = np.zeros_like(ao)
        smooth = 0.36 + 0.10 * (cloud - 0.5) + 0.12 * handled - 0.20 * worn - 0.22 * cavity - 0.15 * scr - 0.12 * dents + (grain - 0.5) * 0.06
    occlusion = np.clip(0.35 + 0.65 * ao, 0, 1) * (1 - 0.35 * engraved)
    smooth = np.clip(smooth, 0.04, 0.92)
    base = np.clip(to_srgb(np.clip(base, 0, 1)), 0, 1)
    base[~used] = base[used].mean(0)                                   # (no black between the islands for the mips)
    stats('smooth', smooth[used]); stats('metallic', metallic[used]); print('   base mean', base[used].mean(0).round(3))
    Image.fromarray((base * 255 + 0.5).astype(np.uint8)).save(os.path.join(out, 'T_WP_M1_%s_BaseColor.png' % name), optimize=True)
    msize = 2048
    pack = np.stack([metallic, occlusion, np.zeros_like(ao), smooth], -1)
    im = Image.fromarray((pack * 255 + 0.5).astype(np.uint8), 'RGBA')
    if size != msize: im = im.resize((msize, msize), Image.LANCZOS)
    im.save(os.path.join(out, 'T_WP_M1_%s_Mask.png' % name), optimize=True)
print('MAPS written', out)
