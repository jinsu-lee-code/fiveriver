# Tank entrance, Blender take: bakes TankEntrance.blend frame by frame to the JSON the game plays
# (TankEntranceBakedTake, Resources/Cinematics/TankEntrance_Blender).
#   blender -b TankEntrance.blend --python bake_take.py
# camera: position, forward, up (Unity world), vertical field of view      -> 10 numbers a frame
# tank:   x, z, hull yaw, turret yaw, gun pitch, shake, cut (1 = a new shot) -> 7 numbers a frame
import bpy, json, math, os
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "five_river", "Assets", "_Project", "Art", "Resources",
                                    "Cinematics", "TankEntrance_Blender.json"))
level = json.load(open(os.path.join(HERE, "level.json")))
scene = bpy.context.scene
fps = scene.render.fps


def b2u(v):
    return (v[0], v[2], v[1])


def ground(x, z):
    gx = min(max((x - level["x0"]) / level["step"], 0), level["nx"] - 1.001)
    gz = min(max((z - level["z0"]) / level["step"], 0), level["nz"] - 1.001)
    i, j = int(gx), int(gz)
    fx, fz = gx - i, gz - j
    h = level["heights"]
    n = level["nx"]
    a = h[j * n + i] * (1 - fx) + h[j * n + i + 1] * fx
    b = h[(j + 1) * n + i] * (1 - fx) + h[(j + 1) * n + i + 1] * fx
    return a * (1 - fz) + b * fz


def clear(a, b):
    steps = max(2, int((Vector(a) - Vector(b)).length))
    for i in range(1, steps):
        p = Vector(a).lerp(Vector(b), i / steps)
        if p.y < ground(p.x, p.z) + 0.2:
            return False
    return True


cuts = sorted((m.frame, m.camera, m.name) for m in scene.timeline_markers if m.camera is not None)
cues = sorted((m.frame, m.name[4:]) for m in scene.timeline_markers if m.name.startswith("cue:"))
tank = bpy.data.objects["Tank"]
turret = bpy.data.objects["Turret"]
impact = b2u(bpy.data.objects["Impact"].location)

camera_data, tank_data, hidden = [], [], {}
for frame in range(scene.frame_start, scene.frame_end + 1):
    scene.frame_set(frame)
    deps = bpy.context.evaluated_depsgraph_get()
    shot = [c for c in cuts if c[0] <= frame][-1]
    cam = shot[1].evaluated_get(deps)
    m = cam.matrix_world
    position = b2u(m.translation)
    forward = b2u((m.to_3x3() @ Vector((0, 0, -1))).normalized())
    up = b2u((m.to_3x3() @ Vector((0, 1, 0))).normalized())
    fov = math.degrees(2 * math.atan(cam.data.sensor_height / (2 * cam.data.lens)))
    camera_data += [round(v, 4) for v in (*position, *forward, *up, fov)]
    t = tank.evaluated_get(deps)
    tm = t.matrix_world
    hull_forward = tm.to_3x3() @ Vector((0, 1, 0))
    turret_forward = turret.evaluated_get(deps).matrix_world.to_3x3() @ Vector((0, 1, 0))
    tank_position = b2u(tm.translation)
    tank_data += [round(tank_position[0], 4), round(tank_position[2], 4),
                  round(math.degrees(math.atan2(hull_forward.x, hull_forward.y)), 3),
                  round(math.degrees(math.atan2(turret_forward.x, turret_forward.y)), 3),
                  round(t["gun_pitch"], 3), round(max(0.0, t["shake"]), 3), 1 if frame == shot[0] and frame > scene.frame_start else 0]
    top = (tank_position[0], ground(tank_position[0], tank_position[2]) + 1.5, tank_position[2])
    if not clear(position, top):
        hidden.setdefault(shot[2], []).append(frame)
    if position[1] < ground(position[0], position[2]) + 0.15:
        print("WARNING camera under the ground: shot", shot[2], "frame", frame)

data = {"fps": fps, "camera": camera_data, "tank": tank_data, "cues": [
    {"time": round((frame - scene.frame_start) / fps, 3), "kind": kind,
     "x": round(impact[0], 3), "y": round(impact[1], 3), "z": round(impact[2], 3)} for frame, kind in cues]}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
json.dump(data, open(OUT, "w"), separators=(",", ":"))
print("BAKE_OK", OUT, "frames", scene.frame_end - scene.frame_start + 1, "seconds", (scene.frame_end - scene.frame_start) / fps)
for name, frames in hidden.items():
    print("  tank behind the ground in shot %s: %.1f-%.1f s (%d frames)" % (name, (frames[0] - 1) / fps, (frames[-1] - 1) / fps, len(frames)))
