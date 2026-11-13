"""FBX-space skeleton model of the Mixamo clip (cm, Y up, Z = character forward, X = character left).

Global(bone) = Global(parent) * T(LclTranslation) * PreRotation * R(LclRotation)   (Euler XYZ, degrees)
Exactly what the FBX SDK / Unity evaluate for these nodes (no pivots, offsets, post-rotation, scale = 1).
"""
import math
from mathutils import Matrix, Vector, Euler, Quaternion
import fbxio

NFR = 24          # keys 0..23, key 23 == key 0
PERIOD = 23       # unique frames in the loop (0.767 s at 30 fps)


def eul_mat(deg):
    return Euler([math.radians(v) for v in deg], 'XYZ').to_matrix()


class Rig:
    def __init__(self, fbx):
        self.fbx = fbx
        objs = fbx.top('Objects').children
        by_id = {o.props[0].v: o for o in objs}
        self.names = []
        self.parent = {}
        self.t = {}
        self.rpre = {}
        self.rstatic = {}
        id2name = {}
        for o in objs:
            if o.name != 'Model':
                continue
            n = fbxio.sval(o.props[1])
            id2name[o.props[0].v] = n
            p = fbxio.props70(o)
            self.names.append(n)
            self.t[n] = Vector(p.get('Lcl Translation', [0, 0, 0]))
            self.rpre[n] = eul_mat(p.get('PreRotation', [0, 0, 0]))
            self.rstatic[n] = list(p.get('Lcl Rotation', [0, 0, 0]))
        for c in fbx.top('Connections').children:
            if fbxio.sval(c.props[0]) == 'OO' and c.props[1].v in id2name:
                self.parent[id2name[c.props[1].v]] = id2name.get(c.props[2].v)
        # topological order
        order, seen = [], set()

        def visit(n):
            if n in seen:
                return
            p = self.parent.get(n)
            if p:
                visit(p)
            seen.add(n); order.append(n)
        for n in self.names:
            visit(n)
        self.order = order
        self.children = {n: [c for c in order if self.parent.get(c) == n] for n in order}
        _, self.curves = fbxio.anim_index(fbx)
        # per-frame local channels
        self.lrot = {n: [list(self.rstatic[n]) for _ in range(NFR)] for n in order}
        self.ltrans = {n: [self.t[n].copy() for _ in range(NFR)] for n in order}
        for (n, ch, ax), cnode in self.curves.items():
            kt, kv = fbxio.curve_arrays(cnode)
            vals = kv.v
            for f in range(NFR):
                v = vals[f] if len(vals) == NFR else vals[0]
                if ch == 'R':
                    self.lrot[n][f][ax] = v
                elif ch == 'T':
                    self.ltrans[n][f][ax] = v

    # ---- forward kinematics
    def local(self, n, f, lrot=None, ltrans=None):
        r = (lrot or self.lrot)[n][f]
        t = (ltrans or self.ltrans)[n][f]
        m = (self.rpre[n] @ eul_mat(r)).to_4x4()
        m.translation = t
        return m

    def fk(self, f, lrot=None, ltrans=None):
        g = {}
        for n in self.order:
            p = self.parent.get(n)
            L = self.local(n, f, lrot, ltrans)
            g[n] = (g[p] @ L) if p else L
        return g

    def rest(self):
        g = {}
        for n in self.order:
            p = self.parent.get(n)
            L = (self.rpre[n] @ eul_mat(self.rstatic[n])).to_4x4()
            L.translation = self.t[n]
            g[n] = (g[p] @ L) if p else L
        return g

    def local_rot_from_global(self, n, gparent_rot, grot):
        """Euler-free local rotation matrix R such that Gp * Rpre * R = G (rotation parts)."""
        return self.rpre[n].inverted() @ (gparent_rot.inverted() @ grot)
