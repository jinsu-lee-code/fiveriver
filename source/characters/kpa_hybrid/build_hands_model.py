# Hands for the KPA rifleman modelled from nothing in Blender (2026-11-20, "받아둔거 말고 블렌더에서 새로 만들어").
#
# No generated or downloaded hand: a left hand is built here from a man's measurements (hand 19 cm, palm 8.5 cm
# wide; each finger's three bones and their widths), as closed pieces on its skeleton - tapered capsules for the
# finger bones, a slab for the palm, pads for the balls of the thumb and little finger, a web - fused into one skin
# (voxel remesh) and smoothed. On that the smaller forms are raised or cut: knuckles, the tendons on the back, the
# creases where the fingers bend, the finger pads, the nails. Its colour is painted per vertex from the same forms
# (lighter palm, redder knuckles, dark creases, nails) and written into a texture; it is bound to a skeleton at its
# own joints with automatic weights, and then placed on the game's rig exactly as build_hands_mh.py does (its
# section 3 is run from that file).
#
#   blender -b --factory-startup -P build_hands_model.py -- <old SK_CH_KPA_Rifleman.fbx> - <unity_bones.txt> <out.bin> [<joints.json>]
import bpy, bmesh, sys, os, struct, json
import numpy as np
from mathutils import Vector

old_fbx, _unused, bones_txt, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3, 4)]
P = 'mixamorig:'
WRIST_REACH, TEX = 0.8, 1024
HEAD_SKIN = np.array((0.465, 0.368, 0.275))   # mean skin colour of the heads' maps (sRGB)
names = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']
cm = 0.01

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=old_fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
OJ = {b.name: np.array(arm.matrix_world @ b.head_local) for b in arm.data.bones}
TJ = dict(OJ)
if len(sys.argv) > sys.argv.index('--') + 5:
    TJ.update({k: np.array(v) for k, v in json.load(open(sys.argv[sys.argv.index('--') + 5])).items()})
for o in list(bpy.data.objects): bpy.data.objects.remove(o)

# ---- 1. the skeleton, from measurements. Left hand: x toward the thumb, y toward the palm (the back faces -y),
# z along the fingers, the wrist joint at the origin.
def chain(root, direction, lengths):
    d = np.array(direction, float); d /= np.linalg.norm(d); pts = [np.array(root, float) * cm]
    for L in lengths: pts.append(pts[-1] + d * L * cm)
    return pts
# knuckles on an arc, the fingers fanned a little, at rest slightly bent toward the palm (the direction's y)
J = {
    'Index':  chain((2.7, -0.15, 9.3), (0.10, 0.10, 1), (3.9, 2.3, 2.2)),
    'Middle': chain((0.6, -0.30, 9.7), (0.02, 0.10, 1), (4.4, 2.6, 2.3)),
    'Ring':   chain((-1.4, -0.15, 9.2), (-0.07, 0.10, 1), (4.0, 2.4, 2.3)),
    'Pinky':  chain((-3.2, 0.20, 8.2), (-0.17, 0.10, 1), (3.1, 1.8, 2.0)),
}
# The thumb (remade 2026-11-20, "3D에서부터 엄지가 제대로 안되어있잖아"). As first made it was a fifth finger stuck on the
# side of the palm: in the palm's own plane, its nail on the back like the others, its two end bones bent back up
# alongside the forefinger. A thumb starts at the wrist's corner (the saddle joint), stands out in front of the palm
# as well as to the side, runs straight on from its metacarpal, and is turned about its own length so that its
# nail faces out to the side and its pad faces the fingers.
def unit(v): v = np.array(v, float); return v / np.linalg.norm(v)
tc = np.array((2.2, 0.7, 1.2))
J['Thumb'] = [tc * cm, (tc + unit((0.55, 0.30, 0.78)) * 4.6) * cm]
J['Thumb'].append(J['Thumb'][1] + unit((0.52, 0.20, 0.83)) * 3.1 * cm)
J['Thumb'].append(J['Thumb'][2] + unit((0.50, 0.18, 0.85)) * 2.7 * cm)
wrist = np.zeros(3)
# half-widths at each joint (across the finger); the fingers are a little flatter than wide
RAD = {'Index': (1.06, 0.98, 0.88, 0.74), 'Middle': (1.10, 1.02, 0.91, 0.76), 'Ring': (1.03, 0.95, 0.86, 0.72),
       'Pinky': (0.92, 0.84, 0.76, 0.64), 'Thumb': (1.50, 1.20, 1.10, 0.92)}
FLAT = 0.88
# which way each finger's back (its nail) faces: the fingers' toward the back of the hand, the thumb's out to the side
DORSAL = {n: np.array((0.0, -1.0, 0.0)) for n in names}
_tax = unit(J['Thumb'][3] - J['Thumb'][1]); _d = np.array((0.80, -0.60, 0.0)); DORSAL['Thumb'] = unit(_d - _tax * (_d @ _tax))
# (2026-11-20, against the user's photograph of his own palm) The skin is smoothed after the pieces are fused, which
# took about 2 mm off every finger: they came out as thin even rods, 1.7 cm wide on a 9 cm palm, where a man's are a
# quarter of the palm's width each and thicker at the root. So the pieces are made that much fatter than the measure.
GROW = (1.22, 1.17, 1.14, 1.12)

# ---- 2. the pieces
def ellipsoid(bm, centre, rx, ry, rz, sub=3):
    r = bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    for v in r['verts']: v.co = Vector((centre[0] + v.co.x * rx, centre[1] + v.co.y * ry, centre[2] + v.co.z * rz))
    return r['verts']
def piece(parts):
    # one closed convex piece round the given ellipsoids (centre, rx, ry, rz)
    bm = bmesh.new(); vs = []
    for c, rx, ry, rz in parts: ellipsoid(bm, c, rx, ry, rz)
    hull = bmesh.ops.convex_hull(bm, input=list(bm.verts))
    bmesh.ops.delete(bm, geom=list(set(hull['geom_interior']) | set(hull['geom_unused'])), context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('p'); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('p', me); bpy.context.collection.objects.link(o); return o
objs = []
for n in names:
    r = RAD[n]
    for i in range(3):
        if n == 'Thumb' and i == 0: continue   # its metacarpal lies in the ball of the thumb, below
        a, b = J[n][i], J[n][i + 1]
        tipback = (b - a) / np.linalg.norm(b - a) * (r[i + 1] * cm * 0.55 if i == 2 else 0)
        ga, gb = r[i] * GROW[i] * cm, r[i + 1] * GROW[i + 1] * cm
        fl = FLAT if n != 'Thumb' else 0.96   # (the thumb is turned, so it is not flattened along the palm's own depth)
        objs.append(piece([(a, ga, ga * fl, ga), (b - tipback, gb, gb * fl, gb)]))
# the palm: a slab from the wrist to the row of knuckles
palm_parts = [((0.0, 0.1 * cm, 0.8 * cm), 2.45 * cm, 1.45 * cm, 1.2 * cm), ((0.2 * cm, 0.0, 5.0 * cm), 3.6 * cm, 1.25 * cm, 2.0 * cm)]
for n in names[1:]: palm_parts.append((J[n][0] + np.array((0, 0.25, -0.5)) * cm, RAD[n][0] * cm * 1.08, 1.05 * cm, 1.0 * cm))
# the palm itself runs on a little past the knuckle joints (the fingers part about a third of the way along their
# first bones); nothing is added between the fingers beyond that edge
for n in names[1:]: palm_parts.append((J[n][0] + np.array((0, 0.15, 0.7)) * cm, RAD[n][0] * cm * 0.95, 0.80 * cm, 0.5 * cm))

objs.append(piece(palm_parts))
# the forearm's end
objs.append(piece([((0.0, 0.0, -8.0 * cm), 2.9 * cm, 2.2 * cm, 1.0 * cm), ((0.0, 0.05 * cm, 0.3 * cm), 2.75 * cm, 1.5 * cm, 1.0 * cm)]))
# the ball of the thumb round its metacarpal, and the ball of the little finger
_tm = (J['Thumb'][0] + J['Thumb'][1]) / 2
objs.append(piece([(J['Thumb'][0] + np.array((-0.4, 0.1, 0.8)) * cm, 1.35 * cm, 1.15 * cm, 1.4 * cm),   # (bigger and lower it stood out at the wrist as a knob beside the cuff: the ball of the thumb tapers to the wrist)
                    (_tm + np.array((-0.2, 0.2, 0.0)) * cm, 1.5 * cm, 1.4 * cm, 1.5 * cm),
                   (J['Thumb'][1], RAD['Thumb'][1] * GROW[1] * cm, RAD['Thumb'][1] * GROW[1] * 0.96 * cm, RAD['Thumb'][1] * GROW[1] * cm),
                   ((1.0 * cm, 0.7 * cm, 4.2 * cm), 0.8 * cm, 0.6 * cm, 1.0 * cm)]))
objs.append(piece([((-2.25 * cm, 0.8 * cm, 2.9 * cm), 1.1 * cm, 1.0 * cm, 1.9 * cm),   # (lower and further out it stood off the wrist as a square step)
                    ((-3.0 * cm, 0.7 * cm, 6.3 * cm), 0.95 * cm, 0.85 * cm, 1.5 * cm)]))
# the pads under the knuckles, and the web from the thumb to the forefinger
objs.append(piece([(J[n][0] + np.array((0, 0.75, -1.1)) * cm, 0.85 * cm, 0.55 * cm, 0.9 * cm) for n in names[1:]]))
objs.append(piece([(J['Thumb'][1] + np.array((-0.5, 0.0, 0.2)) * cm, 0.5 * cm, 0.42 * cm, 0.5 * cm), (J['Index'][0] + np.array((0.4, 0.3, -2.6)) * cm, 0.5 * cm, 0.45 * cm, 0.5 * cm),
                   ((2.6 * cm, 0.5 * cm, 4.8 * cm), 0.55 * cm, 0.5 * cm, 0.55 * cm)]))
bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]; bpy.ops.object.join(); hand = bpy.context.object; hand.name = 'hand'
def apply(m): bpy.context.view_layer.objects.active = hand; bpy.ops.object.modifier_apply(modifier=m.name)
r = hand.modifiers.new('remesh', 'REMESH'); r.mode = 'VOXEL'; r.voxel_size = 0.0011; r.adaptivity = 0.0; apply(r)
s = hand.modifiers.new('smooth', 'SMOOTH'); s.iterations = 10; s.factor = 0.6; apply(s)
# The palm, the back of the hand and the wrist are smoothed much further than the fingers (2026-11-20): the pieces
# are convex hulls, and where the big ones met they left flat facets and hard edges, like a hand cut from a board.
Vh = np.array([v.co[:] for v in hand.data.vertices]); near_f = np.full(len(Vh), 9.0)
for n in names:
    for i in range(3):
        if n == 'Thumb' and i == 0: continue
        a, b = J[n][i], J[n][i + 1]; dv = b - a
        t = np.clip((Vh - a) @ dv / (dv @ dv), 0.1 if i == 0 else 0.0, 1.2)
        near_f = np.minimum(near_f, np.linalg.norm(Vh - (a + dv[None] * t[:, None]), axis=1) / (RAD[n][i] * cm))
g = hand.vertex_groups.new(name='soft'); wsoft = np.clip((near_f - 1.15) / 0.5, 0, 1)
for vi in np.nonzero(wsoft > 0)[0]: g.add([int(vi)], float(wsoft[vi]), 'REPLACE')
s = hand.modifiers.new('smooth2', 'SMOOTH'); s.iterations = 45; s.factor = 0.6; s.vertex_group = 'soft'; apply(s)
for g_ in list(hand.vertex_groups): hand.vertex_groups.remove(g_)
# the open end up the arm is cut off (it lies inside the sleeve)
bm = bmesh.new(); bm.from_mesh(hand.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -6.5 * cm], context='VERTS'); bm.to_mesh(hand.data); bm.free()
d = hand.modifiers.new('decimate', 'DECIMATE'); d.ratio = min(1.0, 15000 / max(1, len(hand.data.polygons))); apply(d)
for p in hand.data.polygons: p.use_smooth = True
print('HAND built: %d vertices, %d faces' % (len(hand.data.vertices), len(hand.data.polygons)))

# ---- 3. the smaller forms. They are written as functions of a point on the smooth skin and its normal, so that the
# same forms can be read at every vertex (to move it) and at every texel (to paint it): painted per vertex, the nails'
# edges and the creases came out as blotches the size of the mesh's faces (2026-11-20).
def ss(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def classify(P):
    # each point's finger, the bone of it, how far along, how far to the side (in radii)
    m = len(P); nearest = np.full(m, -1); seg_i = np.zeros(m, int); seg_t = np.zeros(m); best = np.full(m, 1e9); lateral = np.zeros(m)
    for fi, n in enumerate(names):
        for i in range(3):
            a, b = J[n][i], J[n][i + 1]; dv = b - a; L2 = dv @ dv
            t = np.clip((P - a) @ dv / L2, -0.15, 1.25 if i == 2 else 1.0)
            dist = np.linalg.norm(P - (a + dv[None] * t[:, None]), axis=1)
            r_here = (RAD[n][i] + (RAD[n][i + 1] - RAD[n][i]) * np.clip(t, 0, 1)) * cm
            score = dist / r_here
            take = (score < best) & (score < 1.9)
            if n != 'Thumb' and i == 0: take &= (t >= 0.12)
            if n == 'Thumb' and i == 0: take &= False
            ax = dv / np.sqrt(L2); side = np.cross(ax, -DORSAL[n]); side /= np.linalg.norm(side)
            best[take] = score[take]; nearest[take] = fi; seg_i[take] = i; seg_t[take] = t[take]
            lateral[take] = (((P - a) @ side) / r_here)[take]
    return nearest, seg_i, seg_t, lateral
def bez(p0, p1, p2, n=30):
    t = np.linspace(0, 1, n)[:, None]; return ((1 - t) ** 2 * np.array(p0) + 2 * (1 - t) * t * np.array(p1) + t * t * np.array(p2)) * cm
CREASES = (bez((-4.0, 0, 6.5), (-0.6, 0, 6.3), (1.7, 0, 8.9)), bez((3.4, 0, 7.5), (1.0, 0, 7.0), (-2.9, 0, 4.3)), bez((3.3, 0, 7.4), (-0.6, 0, 5.6), (0.5, 0, 0.6)))
def crease_dist(P):
    # distance (in the palm's plane) to the nearest of the three creases of the palm
    d = np.full(len(P), 9.0)
    for cv in CREASES:
        for a, b in zip(cv[:-1], cv[1:]):
            dv = (b - a)[[0, 2]]; q = P[:, [0, 2]] - a[[0, 2]]; t = np.clip(q @ dv / (dv @ dv), 0, 1)
            d = np.minimum(d, np.linalg.norm(q - dv[None] * t[:, None], axis=1))
    return d
def nail_of(seg_t, lateral):
    # the nail on the back of the end bone: across (x, -1..1), along (y, 0 at the cuticle's middle to 1 at the free
    # edge), and how far inside its outline (d > 0 inside, in half-widths). The cuticle is a curve, the corners of the
    # free edge are rounded.
    x = lateral / 0.60; y = (seg_t - 0.40) / 0.58
    cut = 0.36 * x * x; free = 1.0 - 0.10 * x * x
    d = np.minimum(np.minimum(1 - np.abs(x), (y - cut) * 2.2), (free - y) * 2.2)
    return x, y, d, cut, free
def forms(P, Nn):
    f = {}
    nearest, seg_i, seg_t, lateral = classify(P)
    bd = -Nn[:, 1].copy(); th_ = nearest == 0; bd[th_] = Nn[th_] @ DORSAL['Thumb']   # how far the skin faces its finger's back
    back = np.clip(bd * 1.6, 0, 1); palmar = np.clip(-bd * 1.6, 0, 1)
    D = np.zeros(len(P))   # displacement along the normal, metres
    def bump(centre, h, rad, mask): return h * np.exp(-(np.linalg.norm(P - centre, axis=1) / rad) ** 2) * mask
    palm_ = nearest < 0
    for n in names:
        k = (0.34, 0.16, 0.08) if n != 'Thumb' else (0.0, 0.26, 0.12)
        for i in range(3):
            if k[i] > 0: D += bump(J[n][i] + DORSAL[n] * RAD[n][i] * FLAT * cm, k[i] * cm, RAD[n][i] * cm * 0.95, back)
        fi = names.index(n); mine = nearest == fi
        # creases where it bends, on the palm side: at the two outer joints and across the base of the finger
        for i, tt, depth in ((1, 0.0, 0.07), (2, 0.0, 0.055), (0, 0.50 if n != 'Thumb' else 2.0, 0.06)):
            a, b = J[n][i], J[n][i + 1]; ax = (b - a) / np.linalg.norm(b - a)
            along = (P - (a + (b - a) * tt)) @ ax
            D -= depth * cm * np.exp(-(along / (0.085 * cm)) ** 2) * palmar * mine * (np.abs(lateral) < 1.1)
        # the pad of the fingertip, and the pads of the two inner bones
        D += 0.07 * cm * palmar * mine * (seg_i == 2) * ss(0.15, 0.5, seg_t) * (1 - ss(0.85, 1.15, seg_t))
        D += 0.04 * cm * palmar * mine * (seg_i < 2) * np.sin(np.clip(seg_t, 0, 1) * np.pi) * ((n != 'Thumb') | (seg_i > 0))
        # A finger is not a tube: it is wider at its joints than between them, most of all seen from the back.
        tc_ = np.clip(seg_t, 0, 1); sidew = 0.35 + 0.65 * ss(0.3, 0.9, np.abs(lateral))
        joint = np.exp(-(tc_ / 0.17) ** 2) * (seg_i > 0) + np.exp(-((1 - tc_) / 0.17) ** 2) * (seg_i < 2)
        D += mine * sidew * (1 - palmar) * (0.040 * cm * joint - 0.035 * cm * np.sin(tc_ * np.pi) * (seg_i < 2))
        # the nail: a plate on the back of the end bone, a fold round its sides and root
        x, y, d, cut, free = nail_of(seg_t, lateral); on = back * mine * (seg_i == 2)
        D += on * (0.018 * cm * ss(0.0, 0.3, d) - 0.035 * cm * np.exp(-((d + 0.05) / 0.16) ** 2) * (y < 0.9))
    # the tendons on the back of the hand, the bone at the little-finger side of the wrist
    for n in names[1:]:
        a = np.array((J[n][0][0] * 0.35, -1.3 * cm, 1.0 * cm)); b = J[n][0] + np.array((0, -0.9 * cm, -0.8 * cm)); dv = b - a
        t = np.clip((P - a) @ dv / (dv @ dv), 0, 1); dist = np.linalg.norm(P - (a + dv[None] * t[:, None]), axis=1)
        D += 0.06 * cm * np.exp(-(dist / (0.30 * cm)) ** 2) * back * ss(0.0, 0.25, t) * palm_
    D += bump(np.array((-2.5, -1.3, -0.6)) * cm, 0.22 * cm, 0.8 * cm, back)
    # The palm is a cup, not a board: hollow in the middle, with the ball of the thumb and the ball of the little
    # finger standing up at its sides and the pads under the knuckles along its top.
    hollow = np.exp(-(np.linalg.norm((P[:, [0, 2]] - np.array((0.1, 4.9)) * cm) / (np.array((1.5, 2.1)) * cm), axis=1)) ** 2)
    D -= 0.26 * cm * hollow * palmar   # (not cut off at the thumb's own skin: stopped there, it left a torn patch on the ball of the thumb)
    D += bump((J['Thumb'][0] + J['Thumb'][1]) / 2 + np.array((-0.5, 1.0, -0.2)) * cm, 0.24 * cm, 1.7 * cm, palmar)
    D += bump(np.array((-2.8, 0.9, 3.4)) * cm, 0.14 * cm, 1.6 * cm, palmar)
    cd = crease_dist(P); cmask = palmar * palm_ * (P[:, 2] > 0.3 * cm)
    D -= 0.05 * cm * np.exp(-(cd / (0.13 * cm)) ** 2) * cmask
    f.update(bd=bd, D=D, back=back, palmar=palmar, nearest=nearest, seg_i=seg_i, seg_t=seg_t, lateral=lateral, cd=cd, cmask=cmask)
    return f
V0 = np.array([v.co[:] for v in hand.data.vertices]); hand.data.update()
N0 = np.array([v.normal[:] for v in hand.data.vertices])
fv = forms(V0, N0)
V = V0 + N0 * fv['D'][:, None]
for vi, v in enumerate(hand.data.vertices): v.co = V[vi]
hand.data.update()
N = np.array([v.normal[:] for v in hand.data.vertices])

# ---- 4. colour, painted from the same forms at every texel
base = HEAD_SKIN
def colour(P, f):
    back, palmar, nearest, seg_i, seg_t, lateral = f['back'], f['palmar'], f['nearest'], f['seg_i'], f['seg_t'], f['lateral']
    col = np.tile(base, (len(P), 1))
    col *= (1 + palmar[:, None] * np.array((0.27, 0.13, 0.14)))                      # the palm lighter and pinker
    kn = np.zeros(len(P)); wr = np.zeros(len(P)); fc = np.zeros(len(P))
    for n in names:
        fi = names.index(n); mine = nearest == fi
        for i in ((0, 1, 2) if n != 'Thumb' else (1, 2)):
            kn += np.exp(-(np.linalg.norm(P - (J[n][i] + DORSAL[n] * RAD[n][i] * cm), axis=1) / (RAD[n][i] * cm * 1.1)) ** 2) * back
        # the wrinkles over the knuckles of the fingers: fine bowed lines across each, closer at the middle joint
        for i, offs in ((1, (-0.26, -0.13, 0.0, 0.13, 0.26)), (2, (-0.10, 0.0, 0.10))):
            a = J[n][i]; ax = (J[n][i + 1] - J[n][i]); ax /= np.linalg.norm(ax); along = (P - a) @ ax + 0.035 * cm * lateral ** 2
            for off in offs:
                wr = np.maximum(wr, np.exp(-((along - off * cm) / (0.022 * cm)) ** 2) * back * mine * (np.abs(lateral) < 0.75) * (1 - 0.5 * abs(off) / 0.26))
        # the creases on the palm side of the fingers: two close lines at the middle joint, one at the end joint, two at the base
        for i, tt, offs in ((1, 0.0, (-0.07, 0.07)), (2, 0.0, (0.0,)), (0, 0.50 if n != 'Thumb' else 2.0, (-0.06, 0.08))):
            a, b = J[n][i], J[n][i + 1]; ax = (b - a) / np.linalg.norm(b - a); along = (P - (a + (b - a) * tt)) @ ax + 0.03 * cm * lateral ** 2 * (1 if i else -1)
            for off in offs:
                fc = np.maximum(fc, np.exp(-((along - off * cm) / (0.028 * cm)) ** 2) * palmar * mine * (np.abs(lateral) < 1.0))
    col *= (1 + np.clip(kn, 0, 1)[:, None] * np.array((0.04, -0.10, -0.10)))          # the knuckles redder
    pc = np.exp(-(f['cd'] / (0.045 * cm)) ** 2) * f['cmask']                           # the palm's creases, thin
    soft = np.clip(-f['D'] / (0.05 * cm), 0, 1) * palmar * (nearest >= 0)
    col *= (1 - 0.16 * wr[:, None]) * (1 - np.clip(0.20 * pc + 0.15 * fc + 0.08 * soft, 0, 0.4)[:, None] * np.array((0.85, 1.0, 1.0)))
    # the nails
    fing = nearest >= 0; endb = fing & (seg_i == 2)
    x, y, d, cut, free = nail_of(seg_t, lateral); top = ss(0.15, 0.45, f['bd']) * endb
    nail = ss(0.0, 0.07, d) * top
    plate = base * np.array((1.30, 1.12, 1.08))
    lun = ss(0.20, 0.06, y - cut) * ss(0.75, 0.45, np.abs(x))                          # the paler crescent at the root
    edge = ss(free - 0.13, free - 0.08, y)                                           # the free edge, off the finger
    ncol = plate[None] * (1 + 0.10 * lun[:, None]) * (1 - edge[:, None]) + (base * np.array((1.62, 1.60, 1.50)))[None] * edge[:, None]
    ncol *= (1 - 0.10 * np.exp(-((y - (free - 0.135)) / 0.02) ** 2))[:, None]          # the line where the nail leaves the finger
    col = col * (1 - nail[:, None]) + ncol * nail[:, None]
    fold = np.exp(-((d + 0.04) / 0.07) ** 2) * top * (y < free - 0.06)                 # the fold of skin round the sides and the root
    col *= (1 - 0.30 * fold[:, None] * np.array((0.8, 1.0, 1.0)))
    col *= (1 + 0.10 * ss(0.3, 1.0, seg_t)[:, None] * palmar[:, None] * endb[:, None] * np.array((1, 0.2, 0.2)))   # fingertips pinker
    return np.clip(col, 0, 1)

bpy.ops.object.select_all(action='DESELECT'); hand.select_set(True); bpy.context.view_layer.objects.active = hand
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.012); bpy.ops.object.mode_set(mode='OBJECT')
uvl = hand.data.uv_layers.active.data
att = np.concatenate([V0, N0], 1)                                                    # carried to the texels: the smooth skin's point and normal
tatt = np.zeros((TEX, TEX, 6), np.float32); hit = np.zeros((TEX, TEX), bool); wrote = np.zeros((TEX, TEX), bool)
for poly in hand.data.polygons:
    vs = list(poly.vertices); uv_ = np.array([uvl[li].uv[:] for li in poly.loop_indices]) * TEX
    for q in range(1, len(uv_) - 1):
        ids = (0, q, q + 1); t = uv_[list(ids)]
        lo = np.floor(t.min(0)).astype(int) - 2; hi = np.ceil(t.max(0)).astype(int) + 3
        gx, gy = np.meshgrid(np.arange(lo[0], hi[0]), np.arange(lo[1], hi[1])); p_ = np.stack([gx, gy], -1) + 0.5
        d_ = (t[1, 1] - t[2, 1]) * (t[0, 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (t[0, 1] - t[2, 1])
        if abs(d_) < 1e-9: continue
        l1 = ((t[1, 1] - t[2, 1]) * (p_[..., 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (p_[..., 1] - t[2, 1])) / d_
        l2 = ((t[2, 1] - t[0, 1]) * (p_[..., 0] - t[2, 0]) + (t[0, 0] - t[2, 0]) * (p_[..., 1] - t[2, 1])) / d_
        l3 = 1 - l1 - l2; ins = (l1 >= -0.4) & (l2 >= -0.4) & (l3 >= -0.4)   # a little past the edges: the seams' margin
        yy, xx = gy[ins].clip(0, TEX - 1), gx[ins].clip(0, TEX - 1); cc = att[[vs[i] for i in ids]]
        inside = (l1[ins] >= -0.01) & (l2[ins] >= -0.01) & (l3[ins] >= -0.01)
        val = l1[ins][:, None] * cc[0] + l2[ins][:, None] * cc[1] + l3[ins][:, None] * cc[2]   # past the edge the forms simply carry on
        keep = inside | ~hit[yy, xx]
        tatt[yy[keep], xx[keep]] = val[keep]; wrote[yy[keep], xx[keep]] = True; hit[yy[inside], xx[inside]] = True
Pt = tatt[wrote][:, :3].astype(np.float64); Nt = tatt[wrote][:, 3:].astype(np.float64)
Nt /= np.maximum(np.linalg.norm(Nt, axis=1, keepdims=True), 1e-9)
ft = forms(Pt, Nt)
tex = np.tile(base, (TEX, TEX, 1)).astype(np.float32); tex[wrote] = colour(Pt, ft)
print('NAIL texels %d, palm-crease texels %d' % (int(((nail_of(ft['seg_t'], ft['lateral'])[2] > 0.04) & (ft['nearest'] >= 0) & (ft['seg_i'] == 2) & (ft['bd'] > 0.45)).sum()), int((np.exp(-(ft['cd'] / (0.045 * cm)) ** 2) * ft['cmask'] > 0.5).sum())))
# skin grain: fine pores and a soft mottling
rng = np.random.default_rng(7)
def blur(a, r):
    for ax in (0, 1):
        c = np.cumsum(np.pad(a, [(r + 1, r) if i == ax else (0, 0) for i in range(a.ndim)], mode='wrap'), axis=ax)
        a = (np.take(c, range(2 * r + 1, c.shape[ax]), axis=ax) - np.take(c, range(0, c.shape[ax] - 2 * r - 1), axis=ax)) / (2 * r + 1)
    return a
fine = blur(rng.standard_normal((TEX, TEX)), 1); fine /= fine.std()
soft = blur(blur(rng.standard_normal((TEX, TEX)), 9), 9); soft /= soft.std()
tex = np.clip(tex * (1 + 0.035 * fine[..., None] + 0.025 * soft[..., None] * np.array((0.6, 1.0, 1.0))), 0, 1)
img = bpy.data.images.new('BaseColor', TEX, TEX, alpha=False); img.colorspace_settings.name = 'Non-Color'
px = np.ones((TEX, TEX, 4), np.float32); px[..., :3] = tex
img.pixels[:] = px.ravel(); img.filepath_raw = out + '_BaseColor.png'; img.file_format = 'PNG'; img.save()
print('TEXTURE painted: %d of %d texels on the hand' % (int(hit.sum()), TEX * TEX))
def uvmap(u): return (u[0], u[1])

if os.environ.get('HAND_PREVIEW'):   # renders of the hand alone, then stop
    exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'preview_hand.py'), encoding='utf-8').read())

# ---- 5. bound to a skeleton at its own joints (automatic weights)
ad = bpy.data.armatures.new('rig'); ar = bpy.data.objects.new('rig', ad); bpy.context.collection.objects.link(ar)
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = ar; bpy.ops.object.mode_set(mode='EDIT')
def bone(name, a, b, parent=None):
    e = ad.edit_bones.new(name); e.head = Vector(a.tolist()); e.tail = Vector(b.tolist()); e.parent = parent; return e
bf = bone('ForeArm', np.array((0, 0, -8.0)) * cm, wrist); bh = bone('Hand', wrist, J['Middle'][0], bf)
order = ['ForeArm', 'Hand']
for n in names:
    par = bh
    for i in range(3): par = bone(n + str(i + 1), J[n][i], J[n][i + 1], par); order.append(n + str(i + 1))
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT'); hand.select_set(True); ar.select_set(True); bpy.context.view_layer.objects.active = ar
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
gi = {g.index: g.name for g in hand.vertex_groups}
W = np.zeros((len(V), 17))
for v in hand.data.vertices:
    for g in v.groups:
        if gi[g.group] in order: W[v.index, order.index(gi[g.group])] += g.weight
none = W.sum(1) < 1e-6; W[none, 1] = 1.0; W /= W.sum(1, keepdims=True)
print('WEIGHTS automatic: %d vertices, %d had none' % (len(V), int(none.sum())))
for n in names:
    seg = [np.linalg.norm(J[n][i + 1] - J[n][i]) for i in range(3)]
    print('  %-6s %s  total/palm %.2f' % (n, ' '.join('%.1f' % (s_ * 1000) for s_ in seg), sum(seg) / np.linalg.norm(J['Middle'][0])))

# ---- 6. into the rig's bind pose: build_hands_mh.py's section 3, as it is
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'build_hands_mh.py'), encoding='utf-8').read()
exec(src[src.index("# ---- 3. into the rig's bind pose"):])
