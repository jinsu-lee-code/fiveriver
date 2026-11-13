# blender -b --factory-startup -P round_wheels.py -- <in.fbx> <gear json (centres, loop)> <out.fbx> [report.json]
# Makes the rims of the road wheels, sprocket and idler round (2026-11-13, "바퀴가 찌그러져 보임"):
# around each axle (the wheel centres of the gear JSON, shared by both tracks), vertices of the tyre and rim rings are
# moved in the plane square to the axle, along their own radius, onto exact circles. Their position along the axle and
# their angle stay; hubs and spokes (inside RIM_ZONE), the belt band and anything that is not a ring (sprocket teeth,
# bolts: no neighbours all round) do not move. Ring radius = mean-shift mode of the radii of the vertices at the same
# axial position, so a tyre keeps its profile (tread, shoulder, sidewall edge) and only loses its dents.
import bpy, bmesh, sys, os, json
import numpy as np
# radial zone that is rim / tyre (from the axle), per wheel: sprocket, 4 road wheels, idler
RIM_ZONE = [(0.18, 0.26)] + [(0.36, 0.53)] * 4 + [(0.22, 0.30)]
X_MIN = 1.05          # sign-corrected x: the wheels; the hull side and suspension are further in
DX = 0.025            # about the same axial position (the decimated rings are a few cm wide along the axle)
BW = 0.012            # radius window (dents are a few mm; the rings are 3-4 cm apart)
MAX_MOVE = 0.015
SECTORS = 12; MIN_SECTORS = 3; MIN_NEIGHBOURS = 4   # not a lone feature: neighbours at the same radius round the axle
SEG = 0.04            # ring edges longer than this are halved and the new point put on the ring's circle

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gear_blender as gb

def round_tracks(J):
    WHEELS = np.array(J['wheels']).reshape(-1, 4)
    report = {}
    for name, sign in (('TrackL', -1.0), ('TrackR', 1.0)):
        o = bpy.data.objects[name]; me = o.data
        cn = np.array([v.vector[:] for v in me.corner_normals])
        cv = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get('vertex_index', cv)
        M = np.array(o.matrix_world)
        co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
        w = co @ M[:3, :3].T + M[:3, 3]
        x, y, z = sign * -w[:, 0], w[:, 2], -w[:, 1]
        used = np.zeros(len(x), bool)
        me.calc_loop_triangles()
        tri = np.empty(len(me.loop_triangles) * 3, dtype=np.int64); me.loop_triangles.foreach_get('vertices', tri)
        used[tri] = True
        loop = np.array(J[name]['loop']).reshape(-1, 2)
        nn = gb.project(loop, z, y)[1]
        belt = (x > J['beltInner']) & (nn > -J['beltInside'] - 0.005) & (nn < J['beltHalf'] + 0.005)
        owner = np.full(len(x), -1)
        for i, (wz, wy, _, _) in enumerate(WHEELS):
            r = np.hypot(z - wz, y - wy); lo, hi = RIM_ZONE[i]
            k = used & ~belt & (x > X_MIN) & (r > lo) & (r < hi)
            owner[k & (owner == -1)] = i
            owner[k & (owner != i) & (owner != -1)] = -2        # in two wheels' zones: leave it
        ny, nz = y.copy(), z.copy()
        ring_id = np.full(len(x), -1)
        rep = []
        for i, (wz, wy, _, _) in enumerate(WHEELS):
            idx = np.where(owner == i)[0]
            if len(idx) == 0: continue
            dz, dy = z[idx] - wz, y[idx] - wy
            r0 = np.hypot(dz, dy); th = np.arctan2(dy, dz); xs = x[idx]
            sec = ((th + np.pi) / (2 * np.pi) * SECTORS).astype(int) % SECTORS
            near_x = np.abs(xs[:, None] - xs[None, :]) < DX
            r = r0.copy()
            for _ in range(40):          # mean shift of the radius among vertices at about the same axial position
                win = near_x & (np.abs(r[:, None] - r0[None, :]) < BW)
                rn = (win * r0[None, :]).sum(1) / np.maximum(win.sum(1), 1)
                if np.abs(rn - r).max() < 1e-7: r = rn; break
                r = rn
            win = near_x & (np.abs(r[:, None] - r0[None, :]) < BW)
            cover = np.array([len(set(sec[win[j]])) for j in range(len(idx))])
            move = (win.sum(1) >= MIN_NEIGHBOURS) & (cover >= MIN_SECTORS) & (np.abs(r - r0) <= MAX_MOVE)
            if move.any():               # one radius per ring: modes closer than 2 mm are the same circle
                order = np.argsort(r[move]); rv = r[move][order]
                group = np.r_[0, np.cumsum(np.diff(rv) > 0.002)]
                means = np.array([rv[group == g].mean() for g in range(group.max() + 1)])
                rm = np.empty_like(rv); rm[order] = means[group]
                r[move] = rm
                move &= np.abs(r - r0) <= MAX_MOVE
            f = np.where(move, r / r0, 1.0)
            nz[idx] = wz + dz * f; ny[idx] = wy + dy * f
            ring_id[idx[move]] = (i * 100000 + np.round(r[move] * 1000).astype(int) * 10 + 0)
            d = np.abs(r - r0)[move]
            rep.append(dict(wheel=i, zone_vertices=int(len(idx)), moved=int(move.sum()), mean_move_mm=round(float(d.mean() * 1000), 2) if len(d) else 0,
                            max_move_mm=round(float(d.max() * 1000), 2) if len(d) else 0,
                            rings_mm=sorted(set(int(round(float(v) * 1000)) for v in r[move]))))
        # write the rounded positions, then add points on long ring edges (a 16-sided ring still looks dented)
        co[:, 1] = -nz - M[1, 3]
        co[:, 2] = ny - M[2, 3]
        me.vertices.foreach_set('co', co.ravel())
        me.update()
        at = me.attributes.new('nrm_tmp', 'FLOAT_VECTOR', 'CORNER'); at.data.foreach_set('vector', cn.astype(np.float32).ravel())
        rw_at = me.attributes.new('ring_w', 'INT', 'POINT'); rw_at.data.foreach_set('value', np.where(ring_id >= 0, ring_id // 100000, -1).astype(np.int32))
        rr = np.hypot(nz - WHEELS[np.clip(ring_id // 100000, 0, 5), 0], ny - WHEELS[np.clip(ring_id // 100000, 0, 5), 1])
        rr_at = me.attributes.new('ring_r', 'FLOAT', 'POINT'); rr_at.data.foreach_set('value', np.where(ring_id >= 0, rr, 0).astype(np.float32))
        bm = bmesh.new(); bm.from_mesh(me)
        uvl = bm.loops.layers.uv['st']; nrl = bm.loops.layers.float_vector['nrm_tmp']
        rwl = bm.verts.layers.int['ring_w']; rrl = bm.verts.layers.float['ring_r']
        T = M[:3, 3]
        def tank_of(v):
            return sign * -(v.co.x + T[0]), v.co.z + T[2], -(v.co.y + T[1])
        added = 0
        for _pass in range(4):
            todo = []
            for e in bm.edges:
                va, vb = e.verts
                wa, wb = va[rwl], vb[rwl]
                if wa < 0 or wa != wb: continue
                if abs(va[rrl] - vb[rrl]) > 0.003: continue
                xa, ya, za = tank_of(va); xb, yb, zb = tank_of(vb)
                if abs(xa - xb) > DX or np.hypot(za - zb, ya - yb) < SEG: continue
                todo.append(e)
            if not todo: break
            for e in todo:
                va, vb = e.verts
                wi = va[rwl]; wz, wy = WHEELS[wi, 0], WHEELS[wi, 1]
                xa, ya, za = tank_of(va); xb, yb, zb = tank_of(vb)
                ta, tb = np.arctan2(ya - wy, za - wz), np.arctan2(yb - wy, zb - wz)
                tm = ta + ((tb - ta + np.pi) % (2 * np.pi) - np.pi) / 2
                rm = (va[rrl] + vb[rrl]) / 2; xm = (xa + xb) / 2
                zm, ym = wz + rm * np.cos(tm), wy + rm * np.sin(tm)
                ne, nv = bmesh.utils.edge_split(e, va, 0.5)
                nv.co.x = -xm * sign - T[0]; nv.co.y = -zm - T[1]; nv.co.z = ym - T[2]
                nv[rwl] = wi; nv[rrl] = rm
                for l in nv.link_loops:
                    pa, pb = l.link_loop_prev, l.link_loop_next
                    if {pa.vert, pb.vert} != {va, vb}: continue
                    l[uvl].uv = pa[uvl].uv.lerp(pb[uvl].uv, 0.5)
                    l[nrl] = pa[nrl].lerp(pb[nrl], 0.5)
                added += 1
            big = [f for f in bm.faces if len(f.verts) > 3]
            if big: bmesh.ops.triangulate(bm, faces=big, quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.to_mesh(me); bm.free()
        # normals of the rounded rings: a surface of revolution has no part along the circle; corners of the same ring
        # facing the same way (tread, shoulder, sidewall) get the same profile normal
        nverts = len(me.vertices)
        co = np.empty(nverts * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
        w = co @ M[:3, :3].T + M[:3, 3]
        x, y, z = sign * -w[:, 0], w[:, 2], -w[:, 1]
        rwv = np.empty(nverts, dtype=np.int32); me.attributes['ring_w'].data.foreach_get('value', rwv)
        rrv = np.empty(nverts, dtype=np.float32); me.attributes['ring_r'].data.foreach_get('value', rrv)
        cv = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get('vertex_index', cv)
        cn = np.empty(len(me.loops) * 3, dtype=np.float32); me.attributes['nrm_tmp'].data.foreach_get('vector', cn); cn = cn.reshape(-1, 3).astype(float)
        cn /= np.maximum(np.linalg.norm(cn, axis=1), 1e-9)[:, None]
        nx_t, ny_t, nz_t = sign * -cn[:, 0], cn[:, 2], -cn[:, 1]
        wi = rwv[cv]; rc = wi >= 0
        changed = 0
        if rc.any():
            wzc = WHEELS[np.clip(wi, 0, 5), 0]; wyc = WHEELS[np.clip(wi, 0, 5), 1]
            pz, py = z[cv] - wzc, y[cv] - wyc; pr = np.maximum(np.hypot(pz, py), 1e-9)
            ez, ey = pz / pr, py / pr
            na = nx_t; nb = nz_t * ez + ny_t * ey
            ok = rc & (np.hypot(na, nb) > 0.5)
            dbin = np.round(np.degrees(np.arctan2(nb, na)) / 30).astype(np.int64) % 12
            key = (wi.astype(np.int64) * 1000 + np.round(rrv[cv] * 1000).astype(np.int64)) * 100000 + np.round(x[cv] / DX).astype(np.int64) * 20 + dbin
            A = np.zeros(len(cv)); Bv = np.zeros(len(cv))
            for kk in np.unique(key[ok]):
                m = ok & (key == kk)
                va_, vb_ = na[m].mean(), nb[m].mean(); l = np.hypot(va_, vb_)
                A[m] = va_ / l; Bv[m] = vb_ / l
            newn = np.c_[-A * sign, -(Bv * ez), Bv * ey]
            cn[ok] = newn[ok]
            changed = int(ok.sum())
        for nm in ('nrm_tmp', 'ring_w', 'ring_r'): me.attributes.remove(me.attributes[nm])
        me.normals_split_custom_set([tuple(v) for v in cn])
        me.validate(verbose=False)
        rep.append(dict(ring_points_added=added, corner_normals_made_round=changed))
        report[name] = rep
    return report

if __name__ == '__main__' and '--' in sys.argv:
    a = sys.argv[sys.argv.index('--') + 1:]
    SRC, GEAR, DST = a[0], a[1], a[2]
    REPORT = a[3] if len(a) > 3 else None
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    report = round_tracks(json.load(open(GEAR)))
    for n, rep in report.items():
        for e in rep: print('ROUND', n, json.dumps(e))
    if REPORT: json.dump(report, open(REPORT, 'w'), indent=1)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(filepath=DST, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
                             add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=False, path_mode='STRIP')
    print('ROUND_DONE')
