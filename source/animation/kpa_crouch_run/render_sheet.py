"""Contact sheet: rows = (label, view) for each FBX, columns = frames. Workbench, stick-figure proxies
(left limbs blue, right limbs red) and a 1.23 m stand-in rifle placed like the game does it
(grip at the right hand, pointing through the left hand).

blender -b --factory-startup -P render_sheet.py -- OUT.png LABEL1=file1.fbx LABEL2=file2.fbx ...
"""
import sys, os, math
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index('--') + 1:]
OUT = args[0]
INPUTS = [a.split('=', 1) for a in args[1:]]
FRAMES = [0, 3, 6, 9, 12, 15, 18, 21]
TW, TH = 260, 360
RIFLE_LEN, GRIP_FROM_BUTT = 1.23, 0.28
VIEWS = ['side', 'front', 'top']

COL = {'L': (0.25, 0.45, 0.95, 1), 'R': (0.95, 0.3, 0.25, 1), 'C': (0.75, 0.75, 0.72, 1)}
RAD = {'Hips': 9, 'Spine': 9, 'Spine1': 10, 'Spine2': 11, 'Neck': 4, 'Head': 9, 'Shoulder': 3.5, 'Arm': 4,
       'ForeArm': 3.5, 'Hand': 3, 'UpLeg': 6.5, 'Leg': 5, 'Foot': 4, 'ToeBase': 3}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def cyl(name, length, r, color):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=r, radius2=r * 0.85, depth=length)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(-math.pi / 2, 3, 'X'))
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, length / 2, 0))
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.color = color
    bpy.context.scene.collection.objects.link(ob)
    return ob


def build_proxies(arm):
    sc = bpy.context.scene
    sc.frame_set(0)
    bpy.context.view_layer.update()
    for pb in arm.pose.bones:
        short = pb.name.split(':')[-1]
        key = None
        for k in ('ForeArm', 'Shoulder', 'Arm', 'Hand', 'UpLeg', 'Leg', 'Foot', 'ToeBase', 'Spine2', 'Spine1', 'Spine', 'Hips', 'Neck', 'Head'):
            if short.endswith(k) or short == k:
                key = k; break
        if key is None or ('Hand' in short and short not in ('LeftHand', 'RightHand')) or short.endswith('_End'):
            continue
        side = 'L' if short.startswith('Left') else ('R' if short.startswith('Right') else 'C')
        length = pb.bone.length
        r = RAD[key]
        if key == 'Head':
            length *= 1.0
        ob = cyl('px_' + short, length, r, COL[side])
        ob.parent = arm
        ob.parent_type = 'BONE'
        ob.parent_bone = pb.name
        bpy.context.view_layer.update()
        ob.matrix_world = arm.matrix_world @ pb.matrix
    # rifle
    rifle = cyl('rifle', 100.0, 1.6, (0.15, 0.12, 0.08, 1))
    rifle.scale = (0.01, 0.01, 0.01)
    muzzle = cyl('muzzle', 6.0, 3.0, (1.0, 0.8, 0.1, 1))
    muzzle.scale = (0.01, 0.01, 0.01)
    return rifle, muzzle


def place_rifle(arm, rifle, muzzle):
    mw = arm.matrix_world
    R = mw @ arm.pose.bones['mixamorig:RightHand'].head
    L = mw @ arm.pose.bones['mixamorig:LeftHand'].head
    d = (L - R).normalized()
    butt = R - d * GRIP_FROM_BUTT
    rot = d.to_track_quat('Y', 'Z').to_matrix().to_4x4()
    m = Matrix.Translation(butt) @ rot @ Matrix.Diagonal((RIFLE_LEN / 100, RIFLE_LEN / 100, RIFLE_LEN / 100, 1))
    rifle.matrix_world = m
    m2 = Matrix.Translation(butt + d * (RIFLE_LEN - 0.06)) @ rot @ Matrix.Diagonal((0.01, 0.01, 0.01, 1))
    muzzle.matrix_world = m2
    return R, L, d


def setup_render():
    sc = bpy.context.scene
    try:
        sc.render.engine = 'BLENDER_WORKBENCH'
    except TypeError:
        pass
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'OBJECT'
    sc.display.shading.show_shadows = False
    sc.display.shading.show_cavity = False
    sc.render.resolution_x, sc.render.resolution_y = TW, TH
    sc.render.film_transparent = False
    sc.world = bpy.data.worlds.new('w') if sc.world is None else sc.world
    sc.render.image_settings.file_format = 'PNG'
    # ground + 10 cm grid stripes (static: the clip is in place, planted feet must slide backwards uniformly)
    me = bpy.data.meshes.new('ground')
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.5)
    bm.to_mesh(me); bm.free()
    g = bpy.data.objects.new('ground', me); g.color = (0.35, 0.33, 0.3, 1)
    sc.collection.objects.link(g)
    for i in range(-15, 16):
        s = cyl('line%d' % i, 3.0, 0.002, (0.2, 0.2, 0.2, 1))
        s.matrix_world = Matrix.Translation((-1.5, i * 0.1, 0.0005)) @ Matrix.Rotation(-math.pi / 2, 4, 'Z')
    # travel arrow (+ character forward = -Y in Blender)
    a = cyl('travel', 0.6, 0.012, (0.1, 0.8, 0.2, 1))
    a.matrix_world = Matrix.Translation((0, -0.2, 0.003)) @ Matrix.Rotation(math.pi, 4, 'Z')
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    cam.data.type = 'ORTHO'
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def set_view(cam, view):
    if view == 'side':     # from the character's right (-X in Blender), forward (-Y) points left on screen? -> right
        cam.location = (-5, -0.15, 0.85)
        cam.rotation_euler = (math.pi / 2, 0, -math.pi / 2)
        cam.data.ortho_scale = 2.1
    elif view == 'front':  # from ahead of the character (-Y), looking back at him
        cam.location = (0, -5, 0.85)
        cam.rotation_euler = (math.pi / 2, 0, 0)
        cam.data.ortho_scale = 2.1
    else:                  # top
        cam.location = (0, -0.15, 5)
        cam.rotation_euler = (0, 0, 0)
        cam.data.ortho_scale = 2.1


def render_rows(label, path, tmpdir):
    clear()
    try:
        bpy.ops.import_scene.fbx(filepath=path, anim_offset=0)
    except TypeError:
        bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    cam = setup_render()
    rifle, muzzle = build_proxies(arm)
    sc = bpy.context.scene
    rows = []
    info = []
    for view in VIEWS:
        set_view(cam, view)
        tiles = []
        for f in FRAMES:
            sc.frame_set(f)
            bpy.context.view_layer.update()
            R, L, d = place_rifle(arm, rifle, muzzle)
            if view == 'side':
                pitch = math.degrees(math.asin(max(-1, min(1, d.z))))
                info.append((f, round(pitch, 1)))
            fn = os.path.join(tmpdir, '%s_%s_%02d.png' % (label, view, f))
            sc.render.filepath = fn
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(fn)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(TH, TW, 4)
            bpy.data.images.remove(img)
            px[-14:, :, :3] *= 0.4      # darker strip for readability
            tiles.append(px)
        rows.append((label + ' ' + view, np.concatenate(tiles, axis=1)))
    print('RIFLE_PITCH', label, info)
    return rows


def main():
    tmpdir = os.path.join(os.path.dirname(OUT), 'tiles')
    os.makedirs(tmpdir, exist_ok=True)
    allrows = []
    for label, path in INPUTS:
        allrows += render_rows(label, path, tmpdir)
    # order: side rows of all inputs, then front, then top
    ordered = []
    for v in VIEWS:
        ordered += [r for r in allrows if r[0].endswith(v)]
    sheet = np.concatenate([r[1] for r in ordered[::-1]], axis=0)   # pixels are bottom-up
    H, W = sheet.shape[:2]
    img = bpy.data.images.new('sheet', W, H, alpha=True)
    img.pixels = sheet.ravel()
    img.filepath_raw = OUT
    img.file_format = 'PNG'
    img.save()
    print('SHEET', OUT, W, H, [r[0] for r in ordered], 'frames', FRAMES)


main()
