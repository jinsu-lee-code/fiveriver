# Right hand proportions from the user's reference photo (wrist-knuckle 86 mm, middle finger 48/31/25 mm):
# shorter palm, longer fingers. Same rule as M1HandProportion.cs in the game. Writes reload_scene_prop.blend.
import bpy
from mathutils import Vector
SP, S1, S2, S3 = .85, 1.40, 1.20, 1.15            # palm (metacarpal->knuckle), proximal, middle, distal
bpy.ops.wm.open_mainfile(filepath="C:/sk_anim/reload_scene.blend")
rig = bpy.data.objects["HandRig"]; RW = rig.matrix_world; B = rig.data.bones
skin = [o for o in bpy.data.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' and m.object == rig for m in o.modifiers)][0]

H = {b.name: RW @ b.head_local for b in B if b.name.endswith(".R")}
seg = {}      # bone -> (start, end, scale)
delta = {n: Vector() for n in H}
for k in (1, 2, 3, 4):
    m, f = "metacarpal%d.R" % k, "finger%d" % (k + 1)
    h = [H[m]] + [H["%s-%d.R" % (f, j)] for j in (1, 2, 3)]
    h.append(h[3] + (h[3] - h[2]).normalized() * .85 * (h[3] - h[2]).length)
    names = [m] + ["%s-%d.R" % (f, j) for j in (1, 2, 3)]; scales = [SP, S1, S2, S3]
    new = [h[0]]
    for i in range(4): new.append(new[i] + (h[i + 1] - h[i]) * scales[i])
    for i, n in enumerate(names):
        seg[n] = (h[i], h[i + 1], scales[i]); delta[n] = new[i] - h[i]

def mapped(name, v):
    if name not in seg: return v
    a, c, s = seg[name]; d = c - a; L = d.length; u = d / L; t = max(0., min(L, (v - a).dot(u)))
    return v + delta[name] + u * (t * (s - 1))

mw = skin.matrix_world; mi = mw.inverted(); gname = {g.index: g.name for g in skin.vertex_groups}; moved = 0
for v in skin.data.vertices:
    gs = [(gname[g.group], g.weight) for g in v.groups if gname[g.group] in seg]
    if not gs: continue
    tot = sum(g.weight for g in v.groups) or 1; p = mw @ v.co; acc = Vector(); wsum = 0
    for g in v.groups: n = gname[g.group]; acc += mapped(n, p) * g.weight; wsum += g.weight
    v.co = mi @ (acc / wsum); moved += 1
skin.data.update()

bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode='EDIT')
IW = RW.inverted()
for n, d in delta.items():
    if d.length < 1e-7: continue
    e = rig.data.edit_bones[n]; dd = IW.to_3x3() @ d; e.head += dd; e.tail += dd
bpy.ops.object.mode_set(mode='OBJECT')
for n in ("finger3-1.R", "finger3-2.R", "finger3-3.R"):
    print("PROP", n, "head moved mm %.1f" % (delta[n].length * 1000))
print("PROP palm mm %.1f -> %.1f" % (((H["finger3-1.R"] - H["wrist.R"]).length) * 1000, ((H["finger3-1.R"] + delta["finger3-1.R"] - H["wrist.R"]).length) * 1000), "verts moved", moved)
bpy.ops.wm.save_as_mainfile(filepath="C:/sk_anim/reload_scene_prop.blend")
print("PROP_OK")
