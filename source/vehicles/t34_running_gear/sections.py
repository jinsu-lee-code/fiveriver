# Mosaic of belt cross-sections (sideways x across, outward up) every STEP metres round the loop:
# blue = belt triangles (move), grey = what stays still. python sections.py <prefix> <json> <tag> [TrackL]
import sys, os, json, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim, gear_blender as gb
from sim import samples
prefix, jp, tag = sys.argv[1], sys.argv[2], sys.argv[3]; name = sys.argv[4] if len(sys.argv) > 4 else 'TrackL'
J = json.load(open(jp)); STEP = 0.2
x, y, z, t = sim.load(prefix, name)
loop = np.array(J[name]['loop']).reshape(-1, 2); T = sim.table(loop); G = sim.grid(T)
g, seam = sim.classify(J, T, G, x, y, z, t)
P = np.c_[x, y, z]
def proj(S):
    s, al, ou = sim.project(T, G, S[:, 2], S[:, 1]); return s, S[:, 0], ou
Sb = samples(P, t[g == 7], 150000); Sr = samples(P, t[g != 7], 150000); Sr = Sr[Sr[:, 0] > 0.8]
B = proj(Sb); R = proj(Sr)
L = T['L']; n = int(L / STEP); cols = 11; rows = (n + cols - 1) // cols
cw, ch = 150, 110   # x 0.80..1.55 (5 px/cm... 200 px/m), outward -0.20..+0.15
img = np.zeros((rows * ch, cols * cw, 3)); img[:] = 20
for k in range(n):
    s0 = k * STEP; r, c = divmod(k, cols); oy, ox = r * ch, c * cw
    tile = img[oy:oy + ch, ox:ox + cw]; tile[:] = 32
    for (s, xx, ou), col in ((R, (140, 140, 140)), (B, (80, 150, 255))):
        d = np.abs(((s - s0 + L / 2) % L) - L / 2) < 0.01
        px = ((xx[d] - 0.80) * 200).astype(int); py = (ch / 2 - 15 - ou[d] * 400).astype(int)
        m = (px >= 0) & (px < cw) & (py >= 0) & (py < ch); tile[py[m], px[m]] = col
    for o in (J['beltHalf'], -J['beltInside']): tile[int(ch / 2 - 15 - o * 400), :] = (120, 30, 30)
    tile[:, int((J['beltInner'] - 0.80) * 200)] = (120, 30, 30)
    zc = np.interp(s0, np.arange(513) * L / 512, T['Z'])
    if zc < -1.95 or zc > 1.85: tile[:4, :] = (220, 200, 0)
    tile[:, -1] = 0; tile[-1, :] = 0
gb.png(tag + '_sections_' + name + '.png', img.astype(np.uint8))
print('tiles', n, 'step', STEP, 'L', L)
