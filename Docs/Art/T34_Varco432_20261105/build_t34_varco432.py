# T-34-85 from the VARCO3D mesh (2026-11-05, user request):
#   - split the single scanned-style mesh into Hull / Turret / Gun / TrackL / TrackR (ART_SPEC names)
#   - turn the turret back to straight ahead and level the gun so the game can rotate/elevate them
#   - erase the AI-garbled turret number and bake a crisp red star + white "432" into the atlas
# Run: blender -b source/varco_tank_src.blend --python build_t34_varco432.py -- <out_dir>
# Source files in ./source are never modified.
import bpy, bmesh, sys, os, math, json
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = ARGS[0] if ARGS else os.path.join(HERE, 'out')
# Mode "scan": first VARCO mesh (panel soup, garbled number to erase, crumpled albedo to clean).
# Mode "museum": second VARCO mesh after the museum photo (one connected mesh, no markings, too toy-like -> weather it).
MODE = ARGS[2] if len(ARGS) > 2 else 'scan'
# 'weather' (4th arg): realism pass for in-game viewing (cavity AO, edge wear, dust, mottling, matte). Always on for museum.
WEATHER = MODE == 'museum' or 'weather' in ARGS[3:]
SRC = os.path.join(HERE, ARGS[1]) if len(ARGS) > 1 else os.path.join(HERE, 'source')
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, os.path.join(HERE, 'source'))
import classify
# Height bands in source units (mesh ~1 m long, ground at z=0).
if MODE == 'museum':
    classify.ZC, classify.ZHI, classify.R_T, classify.TRACK_ZMAX, classify.FACE_LEVEL = 0.29, 0.33, 0.24, 0.20, True

WIDTH = 3.0                          # ART_SPEC / ArtSpecTests: hull width 3.0 m
DECAL_W, DECAL_H = 0.22, 0.22 / 3    # decal size in source units (image is 3:1)
log = {}

for name in [n for n in bpy.data.objects.keys() if n != 'mesh_0']:
    bpy.data.objects.remove(bpy.data.objects[name])
src = bpy.data.objects['mesh_0']
bpy.context.view_layer.objects.active = src; src.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
labels, res = classify.analyse(src)
me = src.data
nf = len(me.polygons)
fc = np.empty(nf * 3); me.polygons.foreach_get('center', fc); fc = fc.reshape(-1, 3)
fn = np.empty(nf * 3); me.polygons.foreach_get('normal', fn); fn = fn.reshape(-1, 3)
ls = np.empty(nf, np.int64); me.polygons.foreach_get('loop_start', ls)
lt = np.empty(nf, np.int64); me.polygons.foreach_get('loop_total', lt)
uv = np.empty(len(me.loops) * 2); me.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)

# ---------- Turret frame (source units) ----------
d = np.array(res['gun_dir']); p0 = np.array(res['gun_point'])
f = np.array([d[0], d[1], 0.]); f /= np.linalg.norm(f)          # turret forward (horizontal)
l = np.cross([0, 0, 1.], f)                                      # turret lateral (+V side)
tur = labels == 1
U0 = (fc - p0) @ f; V0 = (fc - p0) @ l
base = tur & (fc[:, 2] < classify.ZC + 0.013)
# Ring centre: middle of the turret base footprint, slightly forward of the bustle-weighted centre.
u_mid = (np.quantile(U0[base], .02) + np.quantile(U0[base], .98)) / 2 + 0.02
v_mid = (np.quantile(V0[base], .02) + np.quantile(V0[base], .98)) / 2
pivot = p0 + f * u_mid + l * v_mid; pivot[2] = float(fc[base][:, 2].min())
rel = fc - pivot; U = rel @ f; V = rel @ l; Z = fc[:, 2]
t_axis = (fc - p0) @ d
t_trun = res['t_wall'] - 0.02
trunnion = p0 + d * t_trun
log.update(pivot=pivot.tolist(), yaw_deg=res['yaw_deg'], gun_pitch_deg=math.degrees(math.asin(d[2])),
           gun_lateral_offset=float((p0 - pivot) @ l))

# ---------- 1. Erase garbled marking from the atlas ----------
base_img = bpy.data.images.load(os.path.join(SRC, 'mat_2_base_color.png'))
W, H = base_img.size
px = np.empty(W * H * 4, np.float32); base_img.pixels.foreach_get(px); px = px.reshape(H, W, 4)
# Turret side wall planes and decal height, measured on this mesh (mid band of the turret wall, near U = 0).
wall_band = tur & (Z > np.percentile(Z[tur], 25)) & (Z < np.percentile(Z[tur], 65)) & (np.abs(U) < 0.06)
side_v = {1: float(np.percentile(V[wall_band], 97)), -1: float(np.percentile(V[wall_band], 3))}
# Halfway up the outer side wall (roof edge and cupola excluded by taking only near-vertical faces).
outer = tur & (np.abs(fn[:, 2]) < 0.5) & (np.abs(U) < 0.08) & (np.abs(V) > 0.6 * min(abs(side_v[1]), abs(side_v[-1])))
z_base, z_top = float(np.percentile(Z[outer], 5)), float(np.percentile(Z[outer], 95))
DECAL_Z = z_base + 0.5 * (z_top - z_base)
log.update(side_v=side_v, decal_z=DECAL_Z)
def uv_mask(face_sel):
    m = np.zeros((H, W), bool)
    for fi in np.nonzero(face_sel)[0]:
        p = uv[ls[fi]:ls[fi] + lt[fi]] * [W, H]
        for k in range(1, len(p) - 1):
            a, b, c = p[0], p[k], p[k + 1]
            x0, y0 = np.floor(np.minimum(np.minimum(a, b), c)).astype(int) - 1
            x1, y1 = np.ceil(np.maximum(np.maximum(a, b), c)).astype(int) + 1
            x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, W - 1), min(y1, H - 1)
            if x1 < x0 or y1 < y0: continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
            den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if abs(den) < 1e-9: continue
            w1 = ((b[1] - c[1]) * (gx - c[0]) + (c[0] - b[0]) * (gy - c[1])) / den
            w2 = ((c[1] - a[1]) * (gx - c[0]) + (a[0] - c[0]) * (gy - c[1])) / den
            m[y0:y1 + 1, x0:x1 + 1] |= (w1 >= -.02) & (w2 >= -.02) & (1 - w1 - w2 >= -.02)
    return m
if MODE == 'scan':
  zone = tur & (Z > 0.33) & (Z < 0.47) & (U > -0.20) & (U < 0.12) & (np.abs(V) > 0.06)
  mask = uv_mask(zone)
  # The -V side's garbled number sits ahead of the star and is a duller grey: erase it more aggressively.
  mask_left = uv_mask(tur & (V < -0.04) & (Z > DECAL_Z - 0.055) & (Z < DECAL_Z + 0.06) & (U > -0.03) & (U < 0.22))
  rgb = px[..., :3]
  mx, mn = rgb.max(2), rgb.min(2)
  paint_mean = float(np.median(rgb[mask].mean(1))) if mask.any() else .37
  marking = mask & (((rgb.mean(2) > paint_mean + 0.08) & (mx - mn < 0.2)) | ((rgb[..., 0] > 0.3) & (rgb[..., 0] - rgb[..., 1] > 0.12)))
  marking |= mask_left   # repaint the whole band; the decal is baked on top
  mask = mask | mask_left
  for _ in range(2):   # include anti-aliased edges
      grown = marking.copy()
      for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)): grown |= np.roll(np.roll(marking, dy, 0), dx, 1)
      marking = grown & mask
  # Diffusion fill from surrounding paint inside the same UV islands.
  known = mask & ~marking
  fill = np.where(known[..., None], rgb, 0).astype(np.float32); wgt = known.astype(np.float32)
  for _ in range(60):
      s = np.zeros_like(fill); n = np.zeros_like(wgt)
      for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
          s += np.roll(np.roll(fill * wgt[..., None], dy, 0), dx, 1); n += np.roll(np.roll(wgt, dy, 0), dx, 1)
      upd = marking & (n > 0)
      fill[upd] = s[upd] / n[upd][:, None]; wgt = np.where(upd, 1., wgt).astype(np.float32)
  rng = np.random.default_rng(432)
  noise = 1 + (rng.random((H, W, 1)).astype(np.float32) - .5) * .06
  filled = marking & (wgt > 0)   # pixels with no painted neighbour keep their original colour
  rgb[filled] = np.clip(fill[filled] * noise[filled], 0, 1)
  # Islands erased as a whole (left band) have no painted neighbour: use the turret's median paint colour.
  paint_rgb = np.median(rgb[mask & ~marking], axis=0) if (mask & ~marking).any() else np.array([.37, .36, .27], np.float32)
  empty = marking & (wgt == 0)
  rgb[empty] = np.clip(paint_rgb * noise[empty], 0, 1)
  px[..., :3] = rgb
def guided_clean(rgb, R=8, EPS=0.0025, AMOUNT=0.75):
    lum = rgb @ np.array([.299, .587, .114], np.float32)
    def box(a, r):
        c = np.cumsum(np.cumsum(np.pad(a, ((r + 1, r), (r + 1, r)), mode='edge'), 0), 1)
        return (c[2 * r + 1:, 2 * r + 1:] - c[:-2 * r - 1, 2 * r + 1:] - c[2 * r + 1:, :-2 * r - 1] + c[:-2 * r - 1, :-2 * r - 1]) / (2 * r + 1) ** 2
    m = box(lum, R); var = box(lum * lum, R) - m * m
    a = var / (var + EPS); b = m - a * m
    ratio = np.clip((box(a, R) * lum + box(b, R)) / np.maximum(lum, 1e-3), 0.6, 1.6)
    return np.clip(rgb * (1 + (ratio - 1) * AMOUNT)[..., None], 0, 1)
dust_small = None
if WEATHER:
    if MODE == 'scan': px[..., :3] = guided_clean(px[..., :3])   # remove crumple shading first, then weather on top
    # De-toy pass (user 11-05: "프라모델 느낌"): bake cavity AO and a ground-dust height mask on the real
    # geometry, then knock back saturation, add soft paint mottling, darken cavities and dust the lower hull.
    sc0 = bpy.context.scene; sc0.render.engine = 'CYCLES'; sc0.cycles.device = 'CPU'
    smat = src.data.materials[0]; snode = smat.node_tree.nodes.new('ShaderNodeTexImage')
    def bake_into(kind, size, samples):
        img = bpy.data.images.new(kind, size, size, alpha=False, float_buffer=True)
        snode.image = img; smat.node_tree.nodes.active = snode
        sc0.cycles.samples = samples
        bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); bpy.context.view_layer.objects.active = src
        bpy.ops.object.bake(type=kind, margin=4)
        a = np.empty(size * size * 4, np.float32); img.pixels.foreach_get(a)
        a = a.reshape(size, size, 4)[..., 0]
        k = W // size
        return np.repeat(np.repeat(a, k, 0), k, 1)
    sc0.world = bpy.data.worlds.new('AOW')
    ao = bake_into('AO', 1024, 32)
    # Dust mask: emission = height above ground, baked through the same UVs.
    out_n = next(n for n in smat.node_tree.nodes if n.type == 'OUTPUT_MATERIAL')
    old_link = out_n.inputs['Surface'].links[0].from_socket if out_n.inputs['Surface'].links else None
    geo_n = smat.node_tree.nodes.new('ShaderNodeNewGeometry'); sep_n = smat.node_tree.nodes.new('ShaderNodeSeparateXYZ')
    em_n = smat.node_tree.nodes.new('ShaderNodeEmission')
    smat.node_tree.links.new(geo_n.outputs['Position'], sep_n.inputs['Vector'])
    smat.node_tree.links.new(sep_n.outputs['Z'], em_n.inputs['Strength'])
    em_n.inputs['Color'].default_value = (1, 1, 1, 1)
    smat.node_tree.links.new(em_n.outputs['Emission'], out_n.inputs['Surface'])
    height = bake_into('EMIT', 1024, 1)
    smat.node_tree.links.new(geo_n.outputs['Pointiness'], em_n.inputs['Strength'])
    point = bake_into('EMIT', 1024, 1)
    if old_link: smat.node_tree.links.new(old_link, out_n.inputs['Surface'])
    rgb = px[..., :3]
    lum = (rgb @ np.array([.299, .587, .114], np.float32))[..., None]
    rgb = lum + (rgb - lum) * (0.88 if MODE == 'scan' else 0.72)       # less saturated, painted steel not plastic
    rng = np.random.default_rng(34085)
    lowf = rng.random((W // 128, H // 128)).astype(np.float32)
    lowf = np.kron(lowf, np.ones((128, 128), np.float32))
    for _ in range(3): lowf = (lowf + np.roll(lowf, 37, 0) + np.roll(lowf, 37, 1) + np.roll(lowf, -37, 0) + np.roll(lowf, -37, 1)) / 5
    rgb *= (0.94 + 0.12 * lowf)[..., None]                             # soft mottling of hand-applied paint
    rgb *= (0.5 + 0.5 * np.clip(ao, 0, 1) ** 0.9)[..., None]           # cavity / panel-gap darkening
    # Edge wear: convex edges (pointiness above the median) lose paint toward a dull grey-brown primer/steel.
    pm = float(np.median(point)); edge = np.clip((point - pm - 0.02) / 0.06, 0, 1)[..., None] * 0.45
    rgb = rgb * (1 - edge) + np.array([.36, .34, .30], np.float32) * edge
    dust = np.clip(1 - (height - 0.02) / 0.16, 0, 1)[..., None] * 0.55  # lower hull, wheels, tracks
    dust_small = dust[::W // 1024, ::W // 1024, 0]
    rgb = rgb * (1 - dust) + np.array([.40, .36, .29], np.float32) * dust * (0.85 + 0.3 * lowf[..., None])
    px[..., :3] = np.clip(rgb, 0, 1)
    smat.node_tree.nodes.remove(snode)
    log['weathered'] = True
atlas = bpy.data.images.new('T_VH_T34_Body_BaseColor', W, H, alpha=False)
atlas.pixels.foreach_set(px.ravel())
log['erased_pixels'] = int(marking.sum()) if MODE == 'scan' else 0

# ---------- 2. Bake crisp star + 432 by side projection ----------
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
keep = set(np.nonzero(tur & (Z > 0.3) & (np.abs(V) > 0.04))[0].tolist())
bmesh.ops.delete(bm, geom=[fa for fa in bm.faces if fa.index not in keep], context='FACES')
tm = bpy.data.meshes.new('BakeTarget'); bm.to_mesh(tm); bm.free()
target = bpy.data.objects.new('BakeTarget', tm); bpy.context.collection.objects.link(target)
mat = bpy.data.materials.new('DecalBake'); mat.use_nodes = True; nt = mat.node_tree; N = nt.nodes; Lk = nt.links
for n in list(N): N.remove(n)
out = N.new('ShaderNodeOutputMaterial'); emit = N.new('ShaderNodeEmission')
uvn = N.new('ShaderNodeUVMap'); src_tex = N.new('ShaderNodeTexImage'); src_tex.image = atlas; src_tex.interpolation = 'Closest'
Lk.new(uvn.outputs['UV'], src_tex.inputs['Vector'])
color = src_tex.outputs['Color']
geo = N.new('ShaderNodeNewGeometry')
for side, img_name in ((1, 'decal_star_left.png'), (-1, 'decal_star_right.png')):
    # Star sits forward (+U). Screen-right seen from outside is Z x normal: rearward on +V, forward on -V.
    centre_u = -0.0728   # star (image edge) lands at U = 0 on both sides
    centre = pivot + f * centre_u + l * side_v[side]; centre[2] = DECAL_Z
    normal = l * side
    right = np.cross([0, 0, 1.], normal); up = np.array([0, 0, 1.])
    emp = bpy.data.objects.new('DecalProj%+d' % side, None); bpy.context.collection.objects.link(emp)
    emp.matrix_world = Matrix(((right[0], up[0], normal[0], centre[0]), (right[1], up[1], normal[1], centre[1]),
                               (right[2], up[2], normal[2], centre[2]), (0, 0, 0, 1)))
    tc = N.new('ShaderNodeTexCoord'); tc.object = emp
    mp = N.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (1 / DECAL_W, 1 / DECAL_H, 1)
    Lk.new(tc.outputs['Object'], mp.inputs['Vector'])
    add = N.new('ShaderNodeVectorMath'); add.operation = 'ADD'; add.inputs[1].default_value = (.5, .5, 0)
    Lk.new(mp.outputs['Vector'], add.inputs[0])
    dec = N.new('ShaderNodeTexImage'); dec.image = bpy.data.images.load(os.path.join(HERE, 'source', img_name)); dec.extension = 'CLIP'
    dec.image.alpha_mode = 'STRAIGHT'
    Lk.new(add.outputs['Vector'], dec.inputs['Vector'])
    dot = N.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'; dot.inputs[1].default_value = tuple(normal)
    Lk.new(geo.outputs['Normal'], dot.inputs[0])
    facing = N.new('ShaderNodeMath'); facing.operation = 'GREATER_THAN'; facing.inputs[1].default_value = 0.2
    Lk.new(dot.outputs['Value'], facing.inputs[0])
    sep = N.new('ShaderNodeSeparateXYZ'); Lk.new(tc.outputs['Object'], sep.inputs['Vector'])
    absz = N.new('ShaderNodeMath'); absz.operation = 'ABSOLUTE'; Lk.new(sep.outputs['Z'], absz.inputs[0])
    near = N.new('ShaderNodeMath'); near.operation = 'LESS_THAN'; near.inputs[1].default_value = 0.12
    Lk.new(absz.outputs['Value'], near.inputs[0])
    m1 = N.new('ShaderNodeMath'); m1.operation = 'MULTIPLY'; Lk.new(dec.outputs['Alpha'], m1.inputs[0]); Lk.new(facing.outputs['Value'], m1.inputs[1])
    m2 = N.new('ShaderNodeMath'); m2.operation = 'MULTIPLY'; Lk.new(m1.outputs['Value'], m2.inputs[0]); Lk.new(near.outputs['Value'], m2.inputs[1])
    mix = N.new('ShaderNodeMix'); mix.data_type = 'RGBA'
    Lk.new(m2.outputs['Value'], mix.inputs['Factor']); Lk.new(color, mix.inputs['A']); Lk.new(dec.outputs['Color'], mix.inputs['B'])
    color = mix.outputs['Result']
Lk.new(color, emit.inputs['Color']); Lk.new(emit.outputs['Emission'], out.inputs['Surface'])
bake_node = N.new('ShaderNodeTexImage'); bake_node.image = atlas; N.active = bake_node
tm.materials.append(mat)
# Bake into a separate image so the source lookup is not read while being written.
baked = bpy.data.images.new('Baked', W, H, alpha=False); baked.pixels.foreach_set(px.ravel()); bake_node.image = baked
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 4; sc.cycles.device = 'CPU'
bpy.ops.object.select_all(action='DESELECT'); target.select_set(True); bpy.context.view_layer.objects.active = target
bpy.ops.object.bake(type='EMIT', use_clear=False, margin=3, margin_type='EXTEND')
bpy.data.objects.remove(target)
# "Clean" pass (user 11-05): the AI texture paints crumple/dent shading into the albedo. An edge-preserving
# guided filter on luminance removes that mid-frequency shading; strong edges (star, 432, panel lines) survive.
bp = np.empty(W * H * 4, np.float32); baked.pixels.foreach_get(bp); bp = bp.reshape(H, W, 4)
CLEAN = MODE == 'scan' and not WEATHER
lum = bp[..., :3] @ np.array([.299, .587, .114], np.float32)
def box(a, r):
    c = np.cumsum(np.cumsum(np.pad(a, ((r + 1, r), (r + 1, r)), mode='edge'), 0), 1)
    return (c[2 * r + 1:, 2 * r + 1:] - c[:-2 * r - 1, 2 * r + 1:] - c[2 * r + 1:, :-2 * r - 1] + c[:-2 * r - 1, :-2 * r - 1]) / (2 * r + 1) ** 2
R, EPS, AMOUNT = 8, 0.0025, 0.75
m = box(lum, R); var = box(lum * lum, R) - m * m
a = var / (var + EPS); b = m - a * m
smooth = box(a, R) * lum + box(b, R)
ratio = np.clip(smooth / np.maximum(lum, 1e-3), 0.6, 1.6)
if CLEAN: bp[..., :3] = np.clip(bp[..., :3] * (1 + (ratio - 1) * AMOUNT)[..., None], 0, 1)
baked.pixels.foreach_set(bp.ravel())
baked.filepath_raw = os.path.join(OUT, 'T_VH_T34_Body_BaseColor.png'); baked.file_format = 'PNG'; baked.save()

# ---------- 3. Split parts ----------
def part(name, mask_faces):
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    keepset = set(np.nonzero(mask_faces)[0].tolist())
    bmesh.ops.delete(bm, geom=[fa for fa in bm.faces if fa.index not in keepset], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    m = bpy.data.meshes.new(name); bm.to_mesh(m); bm.free()
    o = bpy.data.objects.new(name, m); bpy.context.collection.objects.link(o); return o
parts = {'Hull': part('Hull', labels == 0), 'Turret': part('Turret', labels == 1), 'Gun': part('Gun', labels == 2),
         'TrackL': part('TrackL', labels == 3), 'TrackR': part('TrackR', labels == 4)}
bpy.data.objects.remove(src)

# ---------- 4. Straighten turret, level gun ----------
theta = -math.pi / 2 - math.atan2(f[1], f[0])
yaw = Matrix.Translation(Vector(pivot)) @ Matrix.Rotation(theta, 4, 'Z') @ Matrix.Translation(-Vector(pivot))
for n in ('Turret', 'Gun'): parts[n].data.transform(yaw)
trun = yaw @ Vector(trunnion)
d_rot = (yaw.to_3x3() @ Vector(d)).normalized()
phi = math.asin(d_rot.z)
pitch = Matrix.Translation(trun) @ Matrix.Rotation(phi, 4, 'X') @ Matrix.Translation(-trun)
parts['Gun'].data.transform(pitch)

# ---------- 5. Real size, centred on the hull/track footprint ----------
pts = np.vstack([np.array([v.co[:] for v in parts[n].data.vertices]) for n in ('Hull', 'TrackL', 'TrackR')])
lo, hi = pts.min(0), pts.max(0)
S = WIDTH / (hi[0] - lo[0])
to_game = Matrix.Scale(S, 4) @ Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))
for o in parts.values(): o.data.transform(to_game)
pivot_g = to_game @ Vector(pivot); trun_g = to_game @ trun

# ---------- 5b. Straighten dents (user request 11-05: "찌그러진거 모두 다 펴서 깔끔하게") ----------
# Weld the panel soup so neighbouring panels move together (no cracks), then crease-preserving
# normal filtering: each face normal is averaged with neighbours within CREASE_DEG, and vertices are
# moved to agree with the filtered planes. Flat armour becomes flat; bolts, hatches and edges keep their angle.
CREASE_DEG, NORMAL_ITERS, VERTEX_ITERS = 25.0, 6, 12
def straighten(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.002)
    bm.to_mesh(o.data); bm.free()
    m = o.data; nv, nf = len(m.vertices), len(m.polygons)
    co = np.empty(nv * 3); m.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    lv = np.empty(len(m.loops), np.int64); m.loops.foreach_get('vertex_index', lv)
    fls = np.empty(nf, np.int64); m.polygons.foreach_get('loop_start', fls)
    flt = np.empty(nf, np.int64); m.polygons.foreach_get('loop_total', flt)
    lf = np.repeat(np.arange(nf), flt)                        # face of each loop
    cos_cut = math.cos(math.radians(CREASE_DEG))
    def face_geo(p):
        c = np.zeros((nf, 3)); np.add.at(c, lf, p[lv]); c /= flt[:, None]
        a = p[lv[fls]]; b = p[lv[fls + 1]]; d = p[lv[fls + 2]]
        n = np.cross(b - a, d - a); area = np.linalg.norm(n, axis=1) + 1e-12
        return c, n / area[:, None], area
    c, n, area = face_geo(co)
    nf_ = n.copy()
    for _ in range(NORMAL_ITERS):
        vs = np.zeros((nv, 3)); np.add.at(vs, lv, (nf_ * area[:, None])[lf])
        vs /= np.linalg.norm(vs, axis=1)[:, None] + 1e-12
        cand = np.zeros((nf, 3)); np.add.at(cand, lf, vs[lv]); cand /= np.linalg.norm(cand, axis=1)[:, None] + 1e-12
        keep_crease = (cand * nf_).sum(1) < cos_cut              # across a real edge: keep the face's own normal
        nf_ = np.where(keep_crease[:, None], nf_, cand)
    for _ in range(VERTEX_ITERS):
        c, _n, _a = face_geo(co)
        proj = nf_[lf] * ((c[lf] - co[lv]) * nf_[lf]).sum(1)[:, None]
        acc = np.zeros((nv, 3)); cnt = np.zeros(nv); np.add.at(acc, lv, proj); np.add.at(cnt, lv, 1)
        co = co + acc / np.maximum(cnt, 1)[:, None] * .5
    m.vertices.foreach_set('co', co.ravel()); m.update()
    return nv
log['straightened_vertices'] = {n: straighten(parts[n]) for n in ('Hull', 'Turret', 'Gun')}

# The VARCO barrel ends only ~0.3 m past the glacis; the real ZiS-S-53 overhangs far more and
# ArtSpecTests needs the muzzle beyond +3.5 m. Lengthen the plain tube section, keep muzzle and mantlet shape.
gun_tip0 = min(v.co.y for v in parts['Gun'].data.vertices)
BARREL_EXTRA = max(0.0, gun_tip0 + 3.75)   # muzzle ends ~3.75 m ahead of the hull centre
for v in parts['Gun'].data.vertices:
    if v.co.y < trun_g.y - 1.0: v.co.y -= BARREL_EXTRA

# Turret ring cover: a plate just under the hull deck closes the opening left by the original turret.
hull_m = parts['Hull'].data
hc = np.empty(len(hull_m.polygons) * 3); hull_m.polygons.foreach_get('center', hc); hc = hc.reshape(-1, 3)
hn = np.empty(len(hull_m.polygons) * 3); hull_m.polygons.foreach_get('normal', hn); hn = hn.reshape(-1, 3)
r_xy = np.linalg.norm(hc[:, :2] - np.array(pivot_g[:2]), axis=1)
deck = (r_xy > 1.0) & (r_xy < 1.6) & (hn[:, 2] > 0.9) & (hc[:, 2] > pivot_g.z - .2) & (hc[:, 2] < pivot_g.z + .25)
deck_z = float(np.median(hc[deck, 2])) if deck.any() else pivot_g.z + .05
# Find the actual opening: cast down onto the hull around the pivot; no deck hit = hole.
from mathutils.bvhtree import BVHTree
tree = BVHTree.FromPolygons([v.co[:] for v in hull_m.vertices], [p.vertices[:] for p in hull_m.polygons])
STEP = .05; G = np.arange(-1.6, 1.61, STEP); n = len(G)
hole = np.zeros((n, n), bool); hit_z = []
for i, gx in enumerate(G):
    for j, gy in enumerate(G):
        r = math.hypot(gx, gy)
        if r > 1.6 or abs(pivot_g.x + gx) > 1.15: continue
        hit = tree.ray_cast(Vector((pivot_g.x + gx, pivot_g.y + gy, deck_z + .5)), Vector((0, 0, -1)), 1.2)
        if hit[0] is None or hit[0].z < deck_z - .35: hole[i, j] = True
        elif 0.8 < r < 1.3 and hit[0].z > deck_z - .15: hit_z.append(hit[0].z)
# Keep only the opening connected to the pivot (panel seams elsewhere are not the turret hole).
c = n // 2; conn = np.zeros_like(hole); stack = [(c, c)] if hole[c, c] else []
while stack:
    i, j = stack.pop()
    if not (0 <= i < n and 0 <= j < n) or conn[i, j] or not hole[i, j]: continue
    conn[i, j] = True; stack += [(i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)]
ii, jj = np.nonzero(conn)
hole_r = float(np.hypot(G[ii], G[jj]).max()) if len(ii) else 0.0
plate_r = min(hole_r + .08, 1.05) if hole_r > 0 else 1.0
plate_z = (float(np.percentile(hit_z, 10)) if hit_z else deck_z) - 0.03
# UV of a median-brightness painted deck face so the plate reads as the same paint.
huv = np.empty(len(hull_m.loops) * 2); hull_m.uv_layers.active.data.foreach_get('uv', huv); huv = huv.reshape(-1, 2)
cand = np.nonzero(deck)[0] if deck.any() else np.array([0])
cuv = np.array([huv[hull_m.polygons[int(i)].loop_start] for i in cand])
lum = px[np.clip((cuv[:, 1] % 1 * H).astype(int), 0, H - 1), np.clip((cuv[:, 0] % 1 * W).astype(int), 0, W - 1), :3].mean(1)
paint_uv = cuv[int(np.argsort(lum)[len(lum) // 2])]
log.update(plate_radius=plate_r, plate_z=plate_z)
bm = bmesh.new(); bm.from_mesh(hull_m)
uvl = bm.loops.layers.uv.active
ring = bmesh.ops.create_circle(bm, cap_ends=True, segments=48, radius=plate_r,
                               matrix=Matrix.Translation((pivot_g.x, pivot_g.y, plate_z)))
for fa in {fa for v in ring['verts'] for fa in v.link_faces}:
    if fa.normal.z < 0: fa.normal_flip()
    for lp in fa.loops: lp[uvl].uv = paint_uv
bm.to_mesh(hull_m); bm.free()
log.update(deck_z=deck_z, barrel_extra=BARREL_EXTRA)

def set_origin(o, at):
    o.data.transform(Matrix.Translation(-Vector(at))); o.matrix_world = Matrix.Translation(Vector(at))
set_origin(parts['Hull'], (0, 0, 0))
set_origin(parts['Turret'], pivot_g)
set_origin(parts['Gun'], trun_g)
for n in ('TrackL', 'TrackR'):
    c = np.array([v.co[:] for v in parts[n].data.vertices]); set_origin(parts[n], (c.min(0) + c.max(0)) / 2)
parts['Gun'].parent = parts['Turret']; parts['Gun'].matrix_parent_inverse = parts['Turret'].matrix_world.inverted()
gun_pts = np.array([(parts['Gun'].matrix_world @ v.co)[:] for v in parts['Gun'].data.vertices])
all_pts = np.vstack([np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices]) for o in parts.values()])
log.update(scale=S, size=(all_pts.max(0) - all_pts.min(0)).tolist(), turret_pivot=list(pivot_g), trunnion=list(trun_g),
           barrel_length_from_trunnion=float(trun_g.y - gun_pts[:, 1].min()), gun_tip_y=float(gun_pts[:, 1].min()))

# ---------- 6. Material + maps (same names as the existing Unity textures) ----------
body = bpy.data.materials.new('M_VH_T34_Body'); body.use_nodes = True
bsdf = body.node_tree.nodes['Principled BSDF']
t = body.node_tree.nodes.new('ShaderNodeTexImage'); t.image = baked
body.node_tree.links.new(t.outputs['Color'], bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value = .7
for o in parts.values(): o.data.materials.clear(); o.data.materials.append(body)
# Normal map: keep panel/rivet detail but remove most of the crumpled-sheet noise (blur + 55% toward flat).
nsrc = bpy.data.images.load(os.path.join(SRC, 'mat_2_normal.png')); NW, NH = nsrc.size
npx = np.empty(NW * NH * 4, np.float32); nsrc.pixels.foreach_get(npx); npx = npx.reshape(NH, NW, 4)
blur = npx[..., :3].copy()
for _ in range(2):
    blur = (blur + np.roll(blur, 1, 0) + np.roll(blur, -1, 0) + np.roll(blur, 1, 1) + np.roll(blur, -1, 1)) / 5
flat = np.array([.5, .5, 1.], np.float32)
keep = .45 if MODE == 'scan' else .8
vec = (blur * keep + flat * (1 - keep)) * 2 - 1
vec /= np.linalg.norm(vec, axis=2)[..., None] + 1e-6
npx[..., :3] = vec * .5 + .5
nout = bpy.data.images.new('T_VH_T34_Body_Normal', NW, NH, alpha=True); nout.pixels.foreach_set(npx.ravel())
nout.filepath_raw = os.path.join(OUT, 'T_VH_T34_Body_Normal.png'); nout.file_format = 'PNG'; nout.save()
if dust_small is not None:
    # Matte painted steel; dusty lower parts even duller. R metallic, A smoothness.
    MS = 1024; msp = np.zeros((MS, MS, 4), np.float32); msp[..., 0] = .12
    msp[..., 3] = 0.24 - 0.16 * np.clip(dust_small[:MS, :MS] / .55, 0, 1)
    ms = bpy.data.images.new('T_VH_T34_Body_MetallicSmoothness', MS, MS, alpha=True); ms.pixels.foreach_set(msp.ravel())
else:
    ms = bpy.data.images.new('T_VH_T34_Body_MetallicSmoothness', 256, 256, alpha=True)
    ms.pixels.foreach_set(np.tile(np.array([.2, .2, .2, .3 if MODE == 'scan' else .18], np.float32), 256 * 256))
ms.filepath_raw = os.path.join(OUT, 'T_VH_T34_Body_MetallicSmoothness.png'); ms.file_format = 'PNG'; ms.save()

bpy.ops.object.select_all(action='DESELECT')
for o in parts.values(): o.select_set(True)
bpy.context.view_layer.objects.active = parts['Hull']
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'SM_VH_T34_85.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False,
                         use_mesh_modifiers=False, path_mode='STRIP')
json.dump(log, open(os.path.join(OUT, 'build_manifest.json'), 'w'), indent=2)
print('BUILD ' + json.dumps(log))

# ---------- 7. Review renders (studio light; turret/gun posed to prove the pivots) ----------
for n in ('DecalProj+1', 'DecalProj-1'):
    if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n])
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
sc.world = bpy.data.worlds.new('W'); sc.world.color = (.45, .45, .45); sc.render.resolution_x, sc.render.resolution_y = 1100, 700
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(35))); bpy.context.object.data.energy = 3.2
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 40
def shot(name, loc, target):
    cam.location = loc; cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, name + '.png'); bpy.ops.render.render(write_still=True)
tz = pivot_g.z + .45
shot('review_right_rest', (10.5, -4, 3.2), (0, -.6, 1.3))
shot('review_left_rest', (-10.5, -4, 3.2), (0, -.6, 1.3))
shot('review_decal_right', (5.2, pivot_g.y - .6, tz + .4), (pivot_g.x + 1, pivot_g.y - .3, tz))
shot('review_decal_left', (-5.2, pivot_g.y - .6, tz + .4), (pivot_g.x - 1, pivot_g.y - .3, tz))
shot('review_rest_top', (0, 0, 14), (0, 0, 0))
parts['Turret'].rotation_euler.z = math.radians(35); parts['Gun'].rotation_euler.x = math.radians(-15)  # -X raises the -Y barrel
shot('review_posed_turret35_gun15', (9, -9, 4.5), (0, 0, 1.3))
shot('review_posed_top', (0, 0, 14), (0, 0, 0))

# ---------- 8. Game-like look: dawn sky HDR + photo ground, eye height 1.7 m (still not Unity lighting) ----------
parts['Turret'].rotation_euler.z = math.radians(-12); parts['Gun'].rotation_euler.x = math.radians(-4)
ART = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'five_river', 'Assets', '_Project', 'Art'))
hdr = os.path.join(ART, 'SurfaceDetail20261030', 'qwantani_dawn_puresky_2k.hdr')
ground_tex = os.path.join(ART, 'PhotoGround20261102', 'rocky_trail_diff_2k.png')
if os.path.exists(hdr):
    w = bpy.data.worlds.new('Dawn'); w.use_nodes = True; wn = w.node_tree.nodes
    env = wn.new('ShaderNodeTexEnvironment'); env.image = bpy.data.images.load(hdr)
    w.node_tree.links.new(env.outputs['Color'], wn['Background'].inputs['Color']); wn['Background'].inputs['Strength'].default_value = 1.0
    sc.world = w
for o in [o for o in bpy.data.objects if o.type == 'LIGHT']: o.data.energy = 2.2
bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, 0)); gp = bpy.context.object
gm = bpy.data.materials.new('Ground'); gm.use_nodes = True
if os.path.exists(ground_tex):
    gt = gm.node_tree.nodes.new('ShaderNodeTexImage'); gt.image = bpy.data.images.load(ground_tex)
    tc = gm.node_tree.nodes.new('ShaderNodeTexCoord'); mp = gm.node_tree.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (12, 12, 1)
    gm.node_tree.links.new(tc.outputs['UV'], mp.inputs['Vector']); gm.node_tree.links.new(mp.outputs['Vector'], gt.inputs['Vector'])
    gm.node_tree.links.new(gt.outputs['Color'], gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
gm.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 1
gp.data.materials.append(gm)
cam.data.lens = 35
shot('game_front_left', (-9, -11, 1.7), (0, 0, 1.3))
shot('game_side_right', (14, -1, 1.7), (0, 0, 1.2))
shot('game_close_turret', (5.5, -4.5, 2.0), (0, -.5, 2.0))
