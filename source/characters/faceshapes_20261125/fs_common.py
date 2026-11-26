# 공용: FBX 가져오기, 얼굴 영역, 카메라/조명/렌더
import bpy, math, os
import numpy as np
from mathutils import Vector, Matrix

HEAD_MAT_KEY = "Baked_Head"
ROOT = r"C:\Users\mbc\Documents\user\Forgotten War\fiveriver"
CHAR = os.path.join(ROOT, r"five_river\Assets\_Project\Art\Models\Characters")
WORK = os.path.join(ROOT, r"source\characters\faceshapes_20261125")

# 대상: 태그 -> (FBX, Unity에서 실제 쓰는 BaseColor 텍스처 재연결 {재질이름: png})
PROT = os.path.join(CHAR, r"ROK_Protagonist\Textures")
TARGETS = {
    "ParkYongpal": (os.path.join(CHAR, r"ROK_ParkYongpal\SK_CH_ROK_ParkYongpal.fbx"), {}),
    "Soldier": (os.path.join(CHAR, r"ROK_Soldier\SK_CH_ROK_Soldier.fbx"), {}),
    "Person1": (os.path.join(CHAR, r"ROK_Soldier\SK_CH_ROK_Soldier_Person1.fbx"), {
        "URPLab_Baked_Head": os.path.join(PROT, "Head_BaseColor.png"),
        "URPLab_Baked_Body": os.path.join(ROOT, r"five_river\Assets\_Project\Art\SurfaceDetail20261110\ROK_Protagonist_Body_BaseColor_TrouserShadowFloor.png"),
        "URPLab_Baked_cap": os.path.join(PROT, "cap_BaseColor.png"),
        "URPLab_Baked_TecticalVest": os.path.join(PROT, "TecticalVest_BaseColor.png"),
        "URPLab_Baked_Pauch": os.path.join(PROT, "Pauch_BaseColor.png"),
        "URPLab_Baked_Pauch.001": os.path.join(PROT, "Pauch_BaseColor.png"),
        "Hand_Left_Skin_FaceMatch": os.path.join(PROT, "Hand_Left_Skin_FaceMatch.png"),
        "Hand_Right_Skin_FaceMatch": os.path.join(PROT, "Hand_Right_Skin_FaceMatch.png"),
    }),
}


def load_target(tag):
    fbx, remap = TARGETS[tag]
    meshes, arm = load_fbx(fbx)
    simplify_materials(remap)
    return fbx, meshes, arm


def load_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    return meshes, arm


def simplify_materials(remap):
    """확인 렌더용: BaseColor만 연결한 무광 재질로 단순화 (금속/거칠기 맵 누락 문제 회피).
    remap에 있으면 그 png로 바꾼다."""
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        nt = m.node_tree
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            continue
        img = None
        base_in = bsdf.inputs["Base Color"]
        if base_in.links:
            src = base_in.links[0].from_node
            if src.type == "TEX_IMAGE":
                img = src.image
        if m.name in remap:
            img = bpy.data.images.load(remap[m.name], check_existing=True)
        keep_tex = None
        for n in nt.nodes:
            if n.type == "TEX_IMAGE" and img is not None and n.image == img and keep_tex is None:
                keep_tex = n.name
        bname = bsdf.name
        for nm in [n.name for n in nt.nodes]:
            n = nt.nodes[nm]
            if nm != bname and n.type != "OUTPUT_MATERIAL" and nm != keep_tex:
                nt.nodes.remove(n)
        bsdf = nt.nodes[bname]
        if img is not None:
            tn = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None)
            if tn is None:
                tn = nt.nodes.new("ShaderNodeTexImage")
                tn.image = img
            nt.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.6
        try:
            bsdf.inputs["Specular IOR Level"].default_value = 0.3
        except KeyError:
            pass
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        nt.links.new(bsdf.outputs[0], out.inputs[0])
        m.blend_method = "OPAQUE" if hasattr(m, "blend_method") else None


def body_mesh(meshes):
    for o in meshes:
        if any(m and HEAD_MAT_KEY in m.name for m in o.data.materials):
            return o
    return max(meshes, key=lambda o: len(o.data.vertices))


def arrays(o):
    me = o.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(o.matrix_world)
    wco = co @ mw[:3, :3].T + mw[:3, 3]
    return co, wco, mw


def head_weights(o):
    gi = {g.name: g.index for g in o.vertex_groups}
    hidx = gi.get("mixamorig:Head")
    w = np.zeros(len(o.data.vertices))
    for v in o.data.vertices:
        for g in v.groups:
            if g.group == hidx:
                w[v.index] = g.weight
    return w


def head_mat_verts(o):
    me = o.data
    mi = [i for i, m in enumerate(me.materials) if m and HEAD_MAT_KEY in m.name]
    mask = np.zeros(len(me.vertices), bool)
    for p in me.polygons:
        if p.material_index in mi:
            for vi in p.vertices:
                mask[vi] = True
    return mask


def set_engine(prefs):
    sc = bpy.context.scene
    for e in prefs:
        try:
            sc.render.engine = e
            return e
        except TypeError:
            pass
    sc.render.engine = "CYCLES"
    return "CYCLES"


def setup_world(strength=0.5):
    sc = bpy.context.scene
    w = sc.world or bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.32, 0.34, 0.37, 1)
    bg.inputs[1].default_value = strength
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass


def make_light(name, loc, target, energy, size=0.3):
    ld = bpy.data.lights.new(name, "AREA")
    ld.energy = energy
    ld.size = size
    lo = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(lo)
    aim(lo, loc, target)
    return lo


def make_cam(loc, target, lens=85, ortho=None, clip_start=0.01):
    cd = bpy.data.cameras.new("Cam")
    cd.lens = lens
    cd.clip_start = clip_start
    cd.clip_end = 10
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho
    co = bpy.data.objects.new("Cam", cd)
    bpy.context.scene.collection.objects.link(co)
    aim(co, loc, target)
    bpy.context.scene.camera = co
    return co


def aim(obj, loc, target, up="Y"):
    obj.location = Vector(loc)
    d = Vector(target) - Vector(loc)
    obj.rotation_euler = d.to_track_quat("-Z", up).to_euler()


def clear(types=("LIGHT", "CAMERA", "FONT")):
    for o in [o for o in bpy.data.objects if o.type in types]:
        bpy.data.objects.remove(o)


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def label(text, loc, rot, size, color=(1, 0.1, 0.1, 1)):
    cu = bpy.data.curves.new("t", "FONT")
    cu.body = text
    cu.size = size
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    ob = bpy.data.objects.new("t", cu)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    ob.color = color
    mat = bpy.data.materials.get("lbl") or bpy.data.materials.new("lbl")
    mat.diffuse_color = color
    cu.materials.append(mat)
    return ob


def draw_grid(png, W, H, u0, v0, scale, step, major, axis_u_sign=1):
    """정사영 렌더 위에 눈금선 그리기. u0,v0 = 화면 중앙의 월드 좌표(가로, 세로)."""
    img = bpy.data.images.load(png)
    px = np.array(img.pixels[:]).reshape(H, W, 4)
    ppm = W / scale  # 픽셀/미터
    def lines(c0, n, horizontal, sgn=1):
        lo = c0 - (n / 2) / ppm
        hi = c0 + (n / 2) / ppm
        k = math.ceil(lo / step)
        while k * step <= hi:
            val = k * step
            p = int(round(sgn * (val - c0) * ppm + n / 2))
            isM = (abs(round(val / major) * major - val) < 1e-6)
            col = np.array([1, 0.15, 0.15, 1]) if isM else np.array([0.2, 0.9, 1.0, 1])
            a = 0.9 if isM else 0.45
            if 0 <= p < n:
                if horizontal:
                    px[p, :, :] = px[p, :, :] * (1 - a) + col * a
                else:
                    px[:, p, :] = px[:, p, :] * (1 - a) + col * a
            k += 1
    # 세로선(가로 좌표 u), 가로선(세로 좌표 v); 이미지 행 0 = 아래
    lines(u0, W, False, axis_u_sign)
    lines(v0, H, True)
    img.pixels[:] = px.ravel()
    img.save()
