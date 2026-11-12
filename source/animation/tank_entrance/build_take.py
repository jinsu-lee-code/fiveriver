# Tank entrance, Blender take (2026-11-11): builds TankEntrance.blend from level.json.
#   blender -b --python build_take.py
# level.json is written from Unity (terrain heights on a 1 m grid, LevelPoints, bounds of sandbags, trees and rocks).
# Everything is keyframed here in Unity world coordinates, mapped to Blender as (x, y, z) -> (x, z, y), so the
# file can be opened and the keys moved by hand; bake_take.py then writes what the game plays.
#
# The take: five shots, each a moving camera on Bezier keys (the code-written "Fable take" cuts between fixed set-ups).
#   A  0.0  crane: from the grass at the roadside up to head height while the tank comes on under a flare
#   B  5.5  dolly: lets the tank catch up, pass and pull ahead, swinging from its front quarter to its rear quarter
#   C 11.5  trench: rises from behind the sandbags as it comes round the spur; the squad leader calls it
#   D 15.8  over the turret: the gun swings to the trench and fires
#   E 17.65 trench: the round lands in front of the wall, then round to the tank and into the player's eyes
# The tank keeps its road speed (3.5 m/s) on its own road the whole time, so the fight starts where it would have.
import bpy, json, math, os
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
level = json.load(open(os.path.join(HERE, "level.json")))
FPS = 30
SPEED = 3.5
EYE = Vector((17.0, 1.61, -1.0))          # the player's eyes at the tank checkpoint (Unity)
CUTS = {"A": 0.0, "B": 5.5, "C": 11.5, "D": 15.8, "E": 17.65}
END = 21.0
FIRE = 17.5
SEEN = 13.7                               # the tank comes out from behind the spur, seen from the trench (measured in play)


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


def u2b(v):
    return Vector((v[0], v[2], v[1]))


def on_ground(x, z, above=0.0):
    return Vector((x, ground(x, z) + above, z))


start = Vector(level["points"]["tankStart"][:3])
hold = Vector(level["points"]["tankHold"][:3])
route = Vector((hold.x - start.x, 0, hold.z - start.z))
length = route.length
road = route / length
side = Vector((road.z, 0, -road.x))       # to the road's right; the trench is on this side when trench_side is +1
trench_side = 1.0 if (EYE - start).dot(side) >= 0 else -1.0
hull_yaw = math.degrees(math.atan2(road.x, road.z))


def tank_at(t):
    p = start + road * min(SPEED * t, length)
    return Vector((p.x, 0, p.z))


def hull(t, above=1.5):
    p = tank_at(t)
    return on_ground(p.x, p.z, above)


def along(s, off=0.0, above=0.0):
    p = start + road * s + side * (off * trench_side)
    return on_ground(p.x, p.z, above)


def f(t):
    return round(t * FPS) + 1


# ---- scene ----
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = FPS
scene.frame_start = 1
scene.frame_end = f(END)
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
prefs = bpy.context.preferences.edit


def link(obj, parent=None):
    scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def box(name, size, location, parent=None, color=(0.5, 0.5, 0.5, 1)):
    mesh = bpy.data.meshes.new(name)
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    verts = [(x, y, z) for x in (-sx, sx) for y in (-sy, sy) for z in (-sz, sz)]
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    mesh.from_pydata(verts, [], faces)
    obj = link(bpy.data.objects.new(name, mesh), parent)
    obj.location = location
    obj.color = color
    return obj


# terrain stand-in
nx, nz, step = level["nx"], level["nz"], level["step"]
verts = [(level["x0"] + i * step, level["z0"] + j * step, level["heights"][j * nx + i]) for j in range(nz) for i in range(nx)]
faces = [(j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i) for j in range(nz - 1) for i in range(nx - 1)]
terrain = bpy.data.meshes.new("Terrain")
terrain.from_pydata(verts, [], faces)
link(bpy.data.objects.new("Terrain", terrain)).color = (0.35, 0.3, 0.2, 1)
colors = {"sandbag": (0.3, 0.26, 0.2, 1), "tree": (0.15, 0.3, 0.15, 1), "rock": (0.45, 0.45, 0.45, 1)}
props = link(bpy.data.objects.new("LevelProps", None))
for kind, cx, cy, cz, sx, sy, sz in level["boxes"]:
    box(kind, (sx, sz, sy), (cx, cz, cy), props, colors[kind])
for name, p in level["points"].items():
    e = link(bpy.data.objects.new("Point_" + name, None), props)
    e.empty_display_size = 0.5
    e.location = u2b(p[:3])
player = link(bpy.data.objects.new("PlayerEye", None))
player.empty_display_type = 'SPHERE'
player.empty_display_size = 0.15
player.location = u2b(EYE)

# ---- tank: one continuous drive under all the shots ----
tank = link(bpy.data.objects.new("Tank", None))
tank.empty_display_type = 'ARROWS'
tank.rotation_euler = (0, 0, -math.radians(hull_yaw))   # its own +Y is forward
box("Hull", (3.0, 6.1, 1.2), (0, 0, 0.95), tank, (0.25, 0.3, 0.2, 1))
turret = link(bpy.data.objects.new("Turret", None), tank)
turret.location = (0, 0.3, 1.6)
box("TurretBody", (2.2, 2.6, 0.8), (0, 0, 0.35), turret, (0.25, 0.3, 0.2, 1))
box("Gun", (0.16, 4.2, 0.16), (0, 3.2, 0.35), turret, (0.2, 0.2, 0.2, 1))
hull_target = link(bpy.data.objects.new("HullTarget", None), tank)
hull_target.location = (0, 0, 1.6)
tank["shake"] = 0.0
tank["gun_pitch"] = 0.0

prefs.keyframe_new_interpolation_type = 'LINEAR'
for t in (0.0, END):
    p = tank_at(t)
    tank.location = (p.x, p.z, ground(p.x, p.z))
    tank.keyframe_insert("location", frame=f(t))



def make_linear(obj):
    # the preference above is not always honoured for new keys: set every key of this object to linear by hand,
    # or the tank eases out of its start and into its end instead of holding its road speed
    action = obj.animation_data.action
    curves = []
    if hasattr(action, "layers"):
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    curves += list(bag.fcurves)
    else:
        curves = list(action.fcurves)
    for curve in curves:
        if curve.data_path == "location":
            for point in curve.keyframe_points:
                point.interpolation = 'LINEAR'
            curve.update()
    return len(curves)


print("tank curves", make_linear(tank))

# where the round lands: in the open in front of the trench, on the far side of the wall from the player
from_tank = EYE - tank_at(FIRE)
from_tank.y = 0
from_tank.normalize()
impact = EYE - from_tank * 11.0 + Vector((from_tank.z, 0, -from_tank.x)) * 3.0
impact = on_ground(impact.x, impact.z)
impact_obj = link(bpy.data.objects.new("Impact", None))
impact_obj.empty_display_type = 'SPHERE'
impact_obj.location = u2b(impact)

prefs.keyframe_new_interpolation_type = 'BEZIER'


def turret_yaw_to(point, t):
    d = point - tank_at(t)
    return math.degrees(math.atan2(d.x, d.z)) - hull_yaw


def key_turret(t, relative_yaw):
    turret.rotation_euler = (0, 0, -math.radians(relative_yaw))
    turret.keyframe_insert("rotation_euler", frame=f(t))


def key_prop(name, t, value):
    tank[name] = float(value)
    tank.keyframe_insert('["%s"]' % name, frame=f(t))


# the turret: straight on the march, a look out to the open side, a look in towards the hill, then onto the trench
key_turret(0.0, 0)
key_turret(6.2, 0)
key_turret(7.6, -24)
key_turret(9.0, 0)
key_turret(10.4, 18)
key_turret(11.6, 0)
key_turret(15.9, 0)
to_trench = (turret_yaw_to(impact, FIRE) + 180) % 360 - 180
key_turret(17.0, to_trench)
key_turret(END, to_trench)
key_prop("gun_pitch", 15.9, 0)
key_prop("gun_pitch", 17.1, -1.5)

# ---- cameras ----
cameras = {}


def camera(name):
    data = bpy.data.cameras.new("Cam" + name)
    data.sensor_fit = 'VERTICAL'
    data.sensor_height = 24.0
    data.clip_start, data.clip_end = 0.1, 600
    cam = link(bpy.data.objects.new("Cam" + name, data))
    target = link(bpy.data.objects.new("Target" + name, None))
    target.empty_display_size = 0.3
    track = cam.constraints.new('TRACK_TO')
    track.target = target
    track.track_axis = 'TRACK_NEGATIVE_Z'
    track.up_axis = 'UP_Y'
    cameras[name] = (cam, target)
    marker = scene.timeline_markers.new(name, frame=f(CUTS[name]))
    marker.camera = cam
    return cam, target


def key(name, t, position, look_at, fov):
    cam, target = cameras[name]
    cam.location = u2b(position)
    cam.keyframe_insert("location", frame=f(t))
    target.location = u2b(look_at)
    target.keyframe_insert("location", frame=f(t))
    cam.data.lens = 12.0 / math.tan(math.radians(fov) / 2)   # vertical field of view on a 24 mm tall gate
    cam.data.keyframe_insert("lens", frame=f(t))


# A: crane at the roadside, far down the road
camera("A")
key("A", 0.0, along(47.0, 2.4, 0.32), hull(0.0, 1.9), 15.0)
key("A", 2.6, along(46.0, 2.3, 0.9), hull(2.6, 1.8), 14.0)
key("A", 5.5, along(43.5, 2.0, 2.3), hull(5.5, 1.6), 12.0)

# B: dolly on the trench side of the road; the tank catches up, passes and pulls ahead
camera("B")
# (abreast at 8.9 s it stands well back, or only the gun and a track are in the frame)
for t, s, off, above, look_ahead, fov in ((5.5, 29.0, 5.0, 0.85, 0.0, 38.0), (7.4, 30.2, 6.6, 1.0, 0.5, 38.0),
                                          (8.9, 31.2, 7.6, 1.3, 1.0, 40.0), (10.2, 32.2, 6.0, 2.0, 2.5, 42.0),
                                          (11.5, 33.2, 4.2, 2.8, 4.0, 43.0)):
    aim = hull(t, 1.5) + road * look_ahead
    key("B", t, along(s, off, above), aim, fov)
for t, v in ((5.5, 0.15), (8.3, 0.75), (9.6, 0.35), (11.4, 0.2)):
    key_prop("shake", t, v)

# C: from the trench: up from behind the sandbags, onto the place where it comes round, then with it
camera("C")
emerge = hull(SEEN + 0.5)
to_spur = Vector((emerge.x - EYE.x, 0, emerge.z - EYE.z)).normalized()
key("C", 11.5, EYE - to_spur * 1.1 + Vector((0, -0.42, 0)), EYE + to_spur * 6.0 + Vector((0, -0.25, 0)), 32.0)
key("C", 13.2, EYE - to_spur * 0.5 + Vector((0, 0.05, 0)), emerge, 24.0)
key("C", 14.4, EYE - to_spur * 0.2 + Vector((0, 0.22, 0)), hull(14.4), 15.0)
key("C", 15.8, EYE + Vector((0, 0.3, 0)), hull(15.8), 12.0)
key_prop("shake", 11.5, 0.0)
key_prop("shake", 15.7, 0.0)

# D: over the turret, on the far side from the trench; the gun comes round and fires
camera("D")
for t in (15.8, 16.8, 17.65):
    p = tank_at(t) - road * (5.6 - 0.4 * (t - 15.8)) - side * (1.9 * trench_side)
    aim = impact.lerp(hull(t, 2.2), 0.25)
    key("D", t, on_ground(p.x, p.z, 3.3 + 0.15 * (t - 15.8)), aim, 40.0)
key_prop("shake", 15.8, 0.25)
key_prop("shake", FIRE - 0.04, 0.25)
key_prop("shake", FIRE, 1.0)

# E: the round lands; then round to the tank and home into the player's eyes
camera("E")
key("E", 17.65, EYE + Vector((0, 0.1, 0)), impact + Vector((0, 1.2, 0)), 50.0)
key("E", 19.2, EYE + Vector((0, 0.1, 0)), impact + Vector((0, 1.4, 0)), 50.0)
key("E", 20.4, EYE + Vector((0, 0.04, 0)), hull(20.4), 58.0)
key("E", END, EYE, hull(END), 60.0)
key_prop("shake", 17.66, 0.8)
key_prop("shake", 19.0, 0.1)
key_prop("shake", 20.0, 0.0)

# cues for the game
for name, t in (("cue:flare", 0.5), ("cue:line", SEEN + 0.4), ("cue:fire", FIRE)):
    scene.timeline_markers.new(name, frame=f(t))
scene.camera = cameras["A"][0]
scene["unity_note"] = "Unity (x, y, z) = Blender (x, z, y). Cut markers carry the shot cameras; cue:* markers are game cues."

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "TankEntrance.blend"))
print("BUILD_OK frames", scene.frame_end, "impact", tuple(round(c, 2) for c in impact), "turret to trench", round(to_trench, 1))
