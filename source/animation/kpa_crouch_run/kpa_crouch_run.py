"""Re-author A_KPA_CrouchRun (Mixamo "Running Crouched With Rifle") into a straight-ahead WWII
crouch run with a Mosin-Nagant combat-idle style carry.

Run (background Blender only, never the GUI session):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P kpa_crouch_run.py -- SRC.fbx DST.fbx [report.json]

The source FBX is read with fbxio (byte-exact reader/writer). Only AnimationCurve key values of the
edited bones change; node names, PreRotations, units (cm), axes, key times and the take "mixamo.com"
stay exactly as in the Mixamo file, so the Unity .meta / avatar / Animator links are unaffected.

FBX space: cm, +Y up, +Z = character forward (direction of travel), +X = character left.
"""
import sys, os, math, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fbxio, rig
from mathutils import Matrix, Vector, Quaternion, Euler

P = 'mixamorig:'
NFR, PER = rig.NFR, rig.PERIOD
UP = Vector((0, 1, 0))

# ---------------------------------------------------------------- tuning (all in one place)
CFG = dict(
    hips_drop=3.0,          # cm lower pelvis -> deeper knee bend
    hips_yaw_target=0.0,    # deg, mean pelvis yaw vs travel (+ = toward character's left)
    hips_yaw_osc=0.8,       # keep this fraction of the original pelvis yaw oscillation
    chest_yaw_target=-10.0, # deg, mean shoulder-line yaw (right shoulder slightly back for the carry)
    chest_yaw_osc=0.6,      # fraction of original shoulder counter-rotation kept
    lean_target=30.0,       # deg, torso line Spine->Neck from vertical (side view)
    spine_weights=(0.35, 0.65, 1.0),   # cumulative share of the spine correction on Spine/Spine1/Spine2
    head_pitch=6.0,         # deg face direction below horizontal (looking ahead)
    head_yaw=0.0,
    neck_share=0.5,
    stance={'Left': (9, 17), 'Right': (21, 6)},   # constant-speed contact frames (inclusive, wrapping)
    stance_speed=None,      # cm/frame of planted foot; None = measured from source
    foot_track_x=7.5,       # cm, |x| of the ball of the foot while planted
    swing_x_keep=0.5,
    foot_toe_out=4.0,       # deg, toe-out of the planted foot
    knee_out=0.12,          # knee pole: lateral share (outward)
    # rifle carry (Mosin combat idle): values in the world frame of the average pose
    r_hand_from_r_shoulder=(6.0, -29.0, 17.0),  # x(left) y(up) z(fwd) cm from RightArm joint (stock wrist)
    rifle_pitch=18.0,       # deg muzzle up from horizontal
    rifle_yaw=24.0,         # deg muzzle toward character's left (across the body)
    rifle_cant=-15.0,       # deg roll of the rifle frame around its axis (hand orientation only)
    grip_sep=28.0,          # cm right hand -> left hand along the rifle
    l_hand_below=0.0,       # cm left hand under the rifle line (supporting)
    r_elbow_pole=(-0.15, -0.5, -1.0),
    l_elbow_pole=(0.25, -1.0, -0.35),
    hand_bounce=0.8,        # cm extra vertical hand lag bounce (2 per cycle)
)


def norm(v):
    v = Vector(v)
    return v.normalized() if v.length > 1e-9 else v


def yaw_of(v):
    return math.degrees(math.atan2(v.x, v.z))


def Ry(deg):
    return Matrix.Rotation(math.radians(deg), 3, 'Y')


def Rx(deg):
    return Matrix.Rotation(math.radians(deg), 3, 'X')


def frame(v1, v2):
    e1 = norm(v1)
    e2 = norm(Vector(v2) - Vector(v2).dot(e1) * e1)
    e3 = e1.cross(e2)
    return Matrix((e1, e2, e3)).transposed()


def two_vec_rot(local_dir, local_axis, world_dir, world_axis):
    return frame(world_dir, world_axis) @ frame(local_dir, local_axis).inverted()


def quat_mean(qs):
    # NB: mathutils 4D Vector.normalized() leaves the 4th component untouched -> normalise by hand
    q0 = qs[0]
    acc = [0.0, 0.0, 0.0, 0.0]
    for q in qs:
        s = 1.0 if q.dot(q0) >= 0 else -1.0
        for i in range(4):
            acc[i] += s * q[i]
    ln = math.sqrt(sum(a * a for a in acc))
    return Quaternion([a / ln for a in acc])


def two_bone(hip, target, l1, l2, pole, max_ratio=0.995):
    d = target - hip
    dist = d.length
    reach = dist / (l1 + l2)
    dist = min(max(dist, abs(l1 - l2) + 1e-3), (l1 + l2) * max_ratio)
    u = d.normalized()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0))
    pp = norm(pole - pole.dot(u) * u)
    knee = hip + a * u + h * pp
    end = hip + dist * u
    return knee, end, reach


def in_stance(f, s):
    a, b = s
    return (a <= f <= b) if a <= b else (f >= a or f <= b)


def stance_frames(s):
    a, b = s
    out, f = [], a
    while True:
        out.append(f)
        if f == b:
            break
        f = (f + 1) % PER
    return out


def regularize(orig, s, stance_vals):
    """orig: list over PER frames; inside stance use stance_vals[f]; swing keeps the original shape plus
    an offset that blends smoothly between the stance-end and next-stance-start offsets (cyclic)."""
    sf = stance_frames(s)
    off = {f: stance_vals[f] - orig[f] for f in sf}
    last, first = sf[-1], sf[0]
    span = (first - last) % PER
    out = list(orig)
    for f in range(PER):
        if f in off:
            out[f] = stance_vals[f]
            continue
        k = (f - last) % PER
        w = k / span
        w = w * w * (3 - 2 * w)
        out[f] = orig[f] + (1 - w) * off[last] + w * off[first]
    return out


class Builder:
    def __init__(self, src):
        self.fbx = fbxio.FbxFile(src)
        self.R = rig.Rig(self.fbx)
        R = self.R
        self.G0 = [R.fk(f) for f in range(NFR)]
        self.rest = R.rest()
        self.Lr0 = {n: [rig.eul_mat(R.lrot[n][f]) for f in range(NFR)] for n in R.order}
        self.measure_source()

    # ------------------------------------------------------------ measurements of the source
    def pos0(self, n, f):
        return self.G0[f][P + n].translation

    def measure_source(self):
        m = {}
        hy, cy, lean = [], [], []
        for f in range(PER):
            side = self.pos0('LeftUpLeg', f) - self.pos0('RightUpLeg', f); side.y = 0
            hy.append(yaw_of(side.cross(UP)))
            sh = self.pos0('LeftArm', f) - self.pos0('RightArm', f); sh.y = 0
            cy.append(yaw_of(sh.cross(UP)))
            t = self.pos0('Neck', f) - self.pos0('Spine', f)
            lean.append(math.degrees(math.atan2(t.z, t.y)))
        self.src_hips_yaw = hy
        self.src_chest_yaw = cy
        m['hips_yaw_mean'] = sum(hy) / PER
        m['hips_yaw_range'] = (min(hy), max(hy))
        m['chest_yaw_mean'] = sum(cy) / PER
        m['lean_mean'] = sum(lean) / PER
        # feet
        self.toe0, self.footyaw = {}, {}
        speeds = []
        for s in ('Left', 'Right'):
            toes = [self.pos0(s + 'ToeBase', f).copy() for f in range(PER)]
            self.toe0[s] = toes
            sf = stance_frames(CFG['stance'][s])
            ys = []
            for f in sf:
                d = self.pos0(s + 'ToeBase', f) - self.pos0(s + 'Foot', f)
                ys.append(yaw_of(Vector((d.x, 0, d.z))))
            self.footyaw[s] = sum(ys) / len(ys)
            # inner-stance speed (exclude the first/last contact frame)
            inner = sf[1:-1]
            sp = [(toes[inner[i]].z - toes[inner[i + 1]].z) for i in range(len(inner) - 1)]
            speeds.append(sum(sp) / len(sp))
            m['%s_stance' % s] = sf
            m['%s_foot_yaw' % s] = self.footyaw[s]
            m['%s_stance_speed_cm_per_frame' % s] = speeds[-1]
            m['%s_toe_x_stance' % s] = sum(toes[f].x for f in sf) / len(sf)
            m['%s_toe_z_stance_span' % s] = (toes[sf[0]].z, toes[sf[-1]].z)
        self.src_speed = sum(speeds) / 2
        m['stance_speed_cm_per_frame'] = self.src_speed
        m['hips_height_range'] = (min(self.pos0('Hips', f).y for f in range(PER)), max(self.pos0('Hips', f).y for f in range(PER)))
        self.src_meas = m

    # ------------------------------------------------------------ target paths
    def foot_targets(self):
        v = CFG['stance_speed'] or self.src_speed
        self.speed = v
        out = {}
        for s, side in (('Left', 1.0), ('Right', -1.0)):
            st = CFG['stance'][s]
            sf = stance_frames(st)
            toes = self.toe0[s]
            zmean = sum(toes[f].z for f in sf) / len(sf)
            mid = (len(sf) - 1) / 2
            zs = {f: zmean - v * (i - mid) for i, f in enumerate(sf)}
            xs = {f: side * CFG['foot_track_x'] for f in sf}
            xmean = sum(toes[f].x for f in sf) / len(sf)
            xo = [side * CFG['foot_track_x'] + CFG['swing_x_keep'] * (toes[f].x - xmean) for f in range(PER)]
            z = regularize([t.z for t in toes], st, zs)
            x = regularize(xo, st, xs)
            y = [t.y for t in toes]
            out[s] = [Vector((x[f], y[f], z[f])) for f in range(PER)]
        return out

    # ------------------------------------------------------------ build one pass
    def build(self, pitch_corr, yaw_corr, arms=None):
        R, C = self.R, CFG
        hy_mean = self.src_meas['hips_yaw_mean']
        cy_mean = self.src_meas['chest_yaw_mean']
        toes = self.foot_targets()
        rest = self.rest
        frames = []
        for f in range(PER):
            G0 = self.G0[f]
            Lr = {n: self.Lr0[n][f].copy() for n in R.order}
            Lt = {n: R.ltrans[n][f].copy() for n in R.order}
            Lt[P + 'Hips'] = Lt[P + 'Hips'] + Vector((0, -C['hips_drop'], 0))
            G = {}
            # pelvis yaw: remove the diagonal, keep part of the oscillation
            hy = self.src_hips_yaw[f]
            hips_corr = C['hips_yaw_target'] + C['hips_yaw_osc'] * (hy - hy_mean) - hy
            Rh = Ry(hips_corr)
            # spine correction (world): yaw so the shoulder line ends near the target, pitch for lean
            cy = self.src_chest_yaw[f]
            chest_after_hips = cy + hips_corr
            want_c = C['chest_yaw_target'] + C['chest_yaw_osc'] * (cy - cy_mean)
            Q = (Ry(want_c - chest_after_hips + yaw_corr) @ Rx(pitch_corr)).to_quaternion()
            setters = {}
            setters[P + 'Hips'] = lambda n, Gp, pos: Rh @ G0[n].to_3x3()
            for i, sn in enumerate(('Spine', 'Spine1', 'Spine2')):
                w = C['spine_weights'][i]
                Qi = Quaternion().slerp(Q, w).to_matrix()
                setters[P + sn] = (lambda Qi: (lambda n, Gp, pos: Qi @ Rh @ G0[n].to_3x3()))(Qi)
            # head / neck
            head_target = Ry(C['head_yaw']) @ Rx(C['head_pitch']) @ rest[P + 'Head'].to_3x3()

            def neck_set(n, Gp, pos):
                follow = Gp.to_3x3() @ R.rpre[n] @ self.Lr0[n][f]
                full = head_target @ (rest[P + 'Head'].to_3x3().inverted() @ rest[n].to_3x3())
                return follow.to_quaternion().slerp(full.to_quaternion(), C['neck_share']).to_matrix()
            setters[P + 'Neck'] = neck_set
            setters[P + 'Head'] = lambda n, Gp, pos: head_target
            # legs
            leg = {}
            for s, side in (('Left', 1.0), ('Right', -1.0)):
                fy = self.footyaw[s]
                want = side * C['foot_toe_out']
                Rf = Ry(want - fy) @ G0[P + s + 'Foot'].to_3x3()
                toe_t = toes[s][f]
                ankle_t = toe_t - Rf @ R.t[P + s + 'ToeBase']
                leg[s] = (Rf, ankle_t, side)

            def upleg_set(n, Gp, pos, s=None):
                Rf, ankle_t, side = leg[s]
                l1 = R.t[P + s + 'Leg'].length; l2 = R.t[P + s + 'Foot'].length
                pole = Vector((side * C['knee_out'], 0, 1.0))
                knee, end, reach = two_bone(pos, ankle_t, l1, l2, pole)
                self._reach.append(reach)
                leg[s + '_knee'] = knee; leg[s + '_end'] = end
                nrm = norm((knee - pos).cross(end - knee))
                return two_vec_rot(norm(R.t[P + s + 'Leg']), self.bend_local[P + s + 'UpLeg'], knee - pos, nrm)

            def lleg_set(n, Gp, pos, s=None):
                knee, end = leg[s + '_knee'], leg[s + '_end']
                up = G[P + s + 'UpLeg'].translation
                nrm = norm((knee - up).cross(end - knee))
                return two_vec_rot(norm(R.t[P + s + 'Foot']), self.bend_local[P + s + 'Leg'], end - knee, nrm)
            for s in ('Left', 'Right'):
                setters[P + s + 'UpLeg'] = (lambda s: (lambda n, Gp, pos: upleg_set(n, Gp, pos, s)))(s)
                setters[P + s + 'Leg'] = (lambda s: (lambda n, Gp, pos: lleg_set(n, Gp, pos, s)))(s)
                setters[P + s + 'Foot'] = (lambda s: (lambda n, Gp, pos: leg[s][0]))(s)
            # arms
            if arms is not None:
                arm = {}

                def hand_targets():
                    if 'R' in arm:
                        return
                    S2 = G[P + 'Spine2']
                    arm['R'] = S2 @ arms['pR']
                    arm['L'] = S2 @ arms['pL']
                    bounce = C['hand_bounce'] * math.cos(2 * math.pi * (2 * f / PER) - 1.2)
                    arm['R'] = arm['R'] + Vector((0, bounce, 0))
                    arm['L'] = arm['L'] + Vector((0, bounce, 0))
                    arm['F'] = S2.to_3x3() @ arms['F_local']
                    arm['pole_R'] = S2.to_3x3() @ arms['poleR']
                    arm['pole_L'] = S2.to_3x3() @ arms['poleL']

                def uparm_set(n, Gp, pos, s=None):
                    hand_targets()
                    l1 = R.t[P + s + 'ForeArm'].length; l2 = R.t[P + s + 'Hand'].length
                    k = s[0]
                    elbow, end, reach = two_bone(pos, arm[k], l1, l2, arm['pole_' + k], 0.985)
                    self._arm_reach[k].append(reach)
                    arm[k + '_elbow'] = elbow; arm[k + '_end'] = end
                    nrm = norm((elbow - pos).cross(end - elbow))
                    return two_vec_rot(norm(R.t[P + s + 'ForeArm']), self.bend_local[P + s + 'Arm'], elbow - pos, nrm)

                def fore_set(n, Gp, pos, s=None):
                    k = s[0]
                    elbow, end = arm[k + '_elbow'], arm[k + '_end']
                    sh = G[P + s + 'Arm'].translation
                    nrm = norm((elbow - sh).cross(end - elbow))
                    return two_vec_rot(norm(R.t[P + s + 'Hand']), self.bend_local[P + s + 'ForeArm'], end - elbow, nrm)

                def hand_set(n, Gp, pos, s=None):
                    return arm['F'] @ arms['hand_rel'][s]
                for s in ('Left', 'Right'):
                    setters[P + s + 'Arm'] = (lambda s: (lambda n, Gp, pos: uparm_set(n, Gp, pos, s)))(s)
                    setters[P + s + 'ForeArm'] = (lambda s: (lambda n, Gp, pos: fore_set(n, Gp, pos, s)))(s)
                    setters[P + s + 'Hand'] = (lambda s: (lambda n, Gp, pos: hand_set(n, Gp, pos, s)))(s)
            # ---- solve in hierarchy order
            changed = set()
            for n in R.order:
                p = R.parent.get(n)
                Gp = G[p] if p else Matrix.Identity(4)
                base = Gp @ Matrix.Translation(Lt[n])
                pos = base.translation.copy()
                if n in setters:
                    grot = setters[n](n, Gp, pos)
                    Lr[n] = R.rpre[n].inverted() @ (Gp.to_3x3().inverted() @ grot)
                    changed.add(n)
                L = (R.rpre[n] @ Lr[n]).to_4x4()
                L.translation = Lt[n]
                G[n] = Gp @ L
            frames.append(dict(G=G, Lr=Lr, Lt=Lt, changed=changed))
        return frames

    def prepare_bend_axes(self):
        """bend axis (hinge normal) of knees/elbows in each bone's local space, averaged over the source."""
        R = self.R
        self.bend_local = {}
        chains = []
        for s in ('Left', 'Right'):
            chains.append((s + 'UpLeg', s + 'Leg', s + 'Foot'))
            chains.append((s + 'Arm', s + 'ForeArm', s + 'Hand'))
        for a, b, c in chains:
            va, vb = [], []
            for f in range(PER):
                G = self.G0[f]
                A, B, Cc = G[P + a].translation, G[P + b].translation, G[P + c].translation
                n = (B - A).cross(Cc - B)
                if n.length < 1e-3:
                    continue
                n.normalize()
                va.append(G[P + a].to_3x3().inverted() @ n)
                vb.append(G[P + b].to_3x3().inverted() @ n)
            self.bend_local[P + a] = norm(sum(va, Vector()))
            self.bend_local[P + b] = norm(sum(vb, Vector()))

    def measure(self, frames):
        hy, cy, lean = [], [], []
        for fr in frames:
            G = fr['G']
            pos = lambda n: G[P + n].translation
            side = pos('LeftUpLeg') - pos('RightUpLeg'); side.y = 0
            hy.append(yaw_of(side.cross(UP)))
            sh = pos('LeftArm') - pos('RightArm'); sh.y = 0
            cy.append(yaw_of(sh.cross(UP)))
            t = pos('Neck') - pos('Spine')
            lean.append(math.degrees(math.atan2(t.z, t.y)))
        return sum(hy) / len(hy), sum(cy) / len(cy), sum(lean) / len(lean), hy, cy, lean

    def solve(self):
        self.prepare_bend_axes()
        pc, yc = 0.0, 0.0
        for it in range(6):
            self._reach = []
            fr = self.build(pc, yc)
            h, c, l, *_ = self.measure(fr)
            pc += (CFG['lean_target'] - l) / CFG['spine_weights'][0] * 0.6
            yc += (CFG['chest_yaw_target'] - c)
        self.pitch_corr, self.yaw_corr = pc, yc
        self._reach = []
        base = self.build(pc, yc)
        # ---- design the carry at the average pose, store it in Spine2 space
        C = CFG
        S2q = quat_mean([fr['G'][P + 'Spine2'].to_quaternion() for fr in base])
        S2p = sum((fr['G'][P + 'Spine2'].translation for fr in base), Vector()) / PER
        S2 = S2q.to_matrix().to_4x4(); S2.translation = S2p
        S2inv = S2.inverted()
        shR = sum((fr['G'][P + 'RightArm'].translation for fr in base), Vector()) / PER
        pR = shR + Vector(C['r_hand_from_r_shoulder'])
        p, y = math.radians(C['rifle_pitch']), math.radians(C['rifle_yaw'])
        d = Vector((math.sin(y) * math.cos(p), math.sin(p), math.cos(y) * math.cos(p)))
        F = frame(d, UP)                     # columns: rifle axis, up, side
        F = F @ Matrix.Rotation(math.radians(C['rifle_cant']), 3, 'X')   # roll about the rifle axis
        up_r = F.col[1]
        pL = pR + C['grip_sep'] * d - C['l_hand_below'] * up_r
        # source hand orientation relative to the source "rifle" (right hand -> left hand line)
        hand_rel = {}
        for s in ('Left', 'Right'):
            qs = []
            for f in range(PER):
                G = self.G0[f]
                d0 = G[P + 'LeftHand'].translation - G[P + 'RightHand'].translation
                F0 = frame(d0, UP)
                qs.append((F0.inverted() @ G[P + s + 'Hand'].to_3x3()).to_quaternion())
            hand_rel[s] = quat_mean(qs).to_matrix()
        arms = dict(pR=S2inv @ pR, pL=S2inv @ pL, F_local=S2q.to_matrix().inverted() @ F,
                    poleR=S2q.to_matrix().inverted() @ norm(C['r_elbow_pole']),
                    poleL=S2q.to_matrix().inverted() @ norm(C['l_elbow_pole']),
                    hand_rel=hand_rel)
        self._arm_reach = {'R': [], 'L': []}
        self._reach = []
        frames = self.build(pc, yc, arms)
        self.design = dict(pR=list(pR), pL=list(pL), dir=list(d))
        return frames

    # ------------------------------------------------------------ write curves
    def write(self, frames, dst):
        R = self.R
        frames = frames + [frames[0]]          # key 23 == key 0
        changed = set().union(*[fr['changed'] for fr in frames])
        written = []
        for n in R.order:
            if n not in changed:
                continue
            keys = [(n, 'R', a) for a in range(3)]
            if not all(k in R.curves for k in keys):
                raise RuntimeError('bone without rotation curves: ' + n)
            prev = Euler([math.radians(v) for v in R.lrot[n][0]], 'XYZ')
            vals = []
            for f in range(NFR):
                e = frames[f]['Lr'][n].to_euler('XYZ', prev)
                prev = e
                vals.append([math.degrees(x) for x in e])
            vals[-1] = list(vals[0])
            for a in range(3):
                kv = fbxio.curve_arrays(R.curves[(n, 'R', a)])[1]
                assert len(kv.v) == NFR
                kv.set_array([v[a] for v in vals])
            written.append(n)
        n = P + 'Hips'
        for a in range(3):
            kv = fbxio.curve_arrays(R.curves[(n, 'T', a)])[1]
            kv.set_array([frames[f]['Lt'][n][a] for f in range(NFR)])
        self.fbx.write(dst)
        return written


def report(b, frames):
    h, c, l, hy, cy, lean = b.measure(frames)
    out = dict(source=b.src_meas)
    toes = {s: [fr['G'][P + s + 'ToeBase'].translation.copy() for fr in frames] for s in ('Left', 'Right')}
    new = dict(hips_yaw_mean=h, hips_yaw_range=(min(hy), max(hy)), chest_yaw_mean=c, chest_yaw_range=(min(cy), max(cy)),
               lean_mean=l, lean_range=(min(lean), max(lean)), stance_speed_cm_per_frame=b.speed,
               planted_speed_m_per_s=b.speed * 30 / 100, cycle_s=PER / 30,
               ground_travel_per_cycle_m=b.speed * PER / 100,
               leg_reach_max=max(b._reach), arm_reach_max={k: max(v) for k, v in b._arm_reach.items()},
               pitch_corr=b.pitch_corr, yaw_corr=b.yaw_corr, design=b.design)
    for s in ('Left', 'Right'):
        sf = stance_frames(CFG['stance'][s])
        t = toes[s]
        new[s + '_stance_toe'] = [(f, round(t[f].x, 2), round(t[f].y, 2), round(t[f].z, 2)) for f in sf]
        d = [frames[f]['G'][P + s + 'ToeBase'].translation - frames[f]['G'][P + s + 'Foot'].translation for f in sf]
        new[s + '_foot_yaw'] = sum(yaw_of(Vector((v.x, 0, v.z))) for v in d) / len(d)
        # planted ball drift relative to the ground (ground moves backwards at `speed` per frame)
        drift = []
        for i in range(len(sf) - 1):
            a, c = t[sf[i]], t[sf[i + 1]]
            drift.append(math.sqrt((c.x - a.x) ** 2 + (c.y - a.y) ** 2 + (c.z - a.z + b.speed) ** 2))
        new[s + '_planted_drift_cm_per_frame_max'] = max(drift)
    gap = min(toes['Left'][f].x - toes['Right'][f].x for f in range(PER))
    new['min_lateral_gap_between_balls_cm'] = gap
    out['new'] = new
    out['quality'] = quality(b, frames)
    return out


def quality(b, frames):
    """largest frame-to-frame local rotation step per bone (source vs new, incl. the 22->0 loop seam)
    and largest local rotation from rest of hands/forearms (twist sanity for the Unity humanoid)."""
    R = b.R
    q = {}
    for n in sorted(set().union(*[fr['changed'] for fr in frames])):
        def steps(get):
            out = []
            for f in range(PER):
                a = get(f).to_quaternion(); c = get((f + 1) % PER).to_quaternion()
                out.append(math.degrees(a.rotation_difference(c).angle))
            return max(out), out[PER - 1]
        s_max, s_seam = steps(lambda f: b.Lr0[n][f])
        n_max, n_seam = steps(lambda f: frames[f]['Lr'][n])
        q[n.split(':')[1]] = dict(src_max_step=round(s_max, 1), new_max_step=round(n_max, 1), new_seam_step=round(n_seam, 1))
    for n in ('LeftHand', 'RightHand', 'LeftForeArm', 'RightForeArm', 'Head', 'Neck'):
        src = max(math.degrees(b.Lr0[P + n][f].to_quaternion().angle) for f in range(PER))
        new = max(math.degrees(frames[f]['Lr'][P + n].to_quaternion().angle) for f in range(PER))
        # twist about the bone axis (local Y) of the local rotation
        def twist(m):
            qq = m.to_quaternion()
            sw, tw = qq.to_swing_twist('Y')
            return math.degrees(tw)
        q[n]['src_max_local_angle'] = round(src, 1)
        q[n]['new_max_local_angle'] = round(new, 1)
        q[n]['src_twist_range'] = (round(min(twist(b.Lr0[P + n][f]) for f in range(PER)), 1), round(max(twist(b.Lr0[P + n][f]) for f in range(PER)), 1))
        q[n]['new_twist_range'] = (round(min(twist(frames[f]['Lr'][P + n]) for f in range(PER)), 1), round(max(twist(frames[f]['Lr'][P + n]) for f in range(PER)), 1))
    return q


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    src, dst = args[0], args[1]
    b = Builder(src)
    frames = b.solve()
    written = b.write(frames, dst)
    rep = report(b, frames)
    rep['written_bones'] = written
    txt = json.dumps(rep, indent=1, default=lambda o: list(o))
    print(txt)
    if len(args) > 2:
        open(args[2], 'w').write(txt)


if __name__ == '__main__':
    main()
