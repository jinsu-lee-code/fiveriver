# 미리보기 렌더 도우미 (Workbench, 직교 카메라)
import math
import os

import bpy
from mathutils import Vector

VIEWS = {
    # 이름: (카메라가 있는 방향(대상에서 본), 숨길 오브젝트)
    "side": (Vector((1.0, 0.0, 0.0)), ()),                    # 캐릭터 왼쪽에서 (참호 안에서 옆모습, 앞은 화면 왼쪽)
    "sideR": (Vector((-1.0, 0.0, 0.0)), ()),                  # 캐릭터 오른쪽에서
    "front": (Vector((0.0, -1.0, 0.12)), ("FrontWall", "Parapet")),
    "q": (Vector((0.75, 0.9, 0.55)), ()),                     # 뒤 왼쪽 위에서 3/4
    "qf": (Vector((-0.8, -0.75, 0.35)), ("FrontWall", "Parapet")),   # 앞 오른쪽에서 3/4
    "top": (Vector((0.0, 0.0001, 1.0)), ()),
    "topx": (Vector((0.0005, 0.0, 1.0)), ()),                # 위에서, 몸이 화면 가로로 눕게
}


def setup(scene, res=(360, 420)):
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    sh.cavity_type = "BOTH"
    sh.show_shadows = False
    sh.show_object_outline = True
    scene.display.render_aa = "8"
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.55, 0.60, 0.66)
    sh.background_type = "WORLD"
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.clip_start = 0.05
    cam.data.clip_end = 50
    scene.camera = cam
    return cam


def aim(cam, view, target=(0.0, -0.05, 0.75), scale=1.9, dist=6.0):
    direction, hidden = VIEWS[view]
    t = Vector(target)
    cam.location = t + direction.normalized() * dist
    cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = scale
    for name in ("FrontWall", "Parapet", "BackWall", "Floor", "ParapetLine"):
        o = bpy.data.objects.get(name)
        if o:
            o.hide_render = name in hidden
    return cam


def shot(scene, cam, view, path, **kw):
    aim(cam, view, **kw)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
