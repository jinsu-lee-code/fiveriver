# Headless: blender -b --factory-startup --python roll_sleeves.py -- <work dir> <out dir>
#
# 소매 걷기 (2026-11-27, 사용자 결정 "소매 걷기" — 1950년 8월 한여름 국군처럼 소매를 팔꿈치 근처까지 두세 번 접어 걷고 팔뚝은 맨살).
# Tripo 병사 팔(work/arms_unity.json)은 손목까지 긴 소매에 손목 앞 두꺼운 끝단이 있어, 1인칭에서 그 끝단이 왼손을 가렸다.
# 뼈 · 가중치 · UV · 삼각형은 그대로 두고 두 가지만 바꾼다:
#  1. 모양: 팔꿈치(위팔 · 아래팔 가중치가 갈리는 곳)~손목(아래팔 · 손이 갈리는 곳)을 축으로, 팔뚝 구간의 각 점을 축에서
#     맨살 팔뚝 굵기로 당긴다(단면 모양은 유지, 손목 맨살 반지름 기준 비율 — 실제 손목 둘레 약 17 cm, 팔뚝 가장 굵은 곳 약
#     27 cm). 팔꿈치 쪽(ROLL_TOP..ROLL_BOTTOM)은 접힌 띠: 소매 굵기를 남기고 접힌 두께 · 골을 준다. 손(t > 0.95)은 그대로.
#  2. 텍스처: 맨살 구간 삼각형이 덮는 텍셀만 피부로 칠한다(색 = 이 팔 손등 피부의 중앙값 + 낮은 주파수 얼룩, 피부 마스크 1,
#     노멀 평평, 거칠기 = 손 피부 중앙값, 금속 0). 띠는 원래 천 텍셀에 접힌 골 그늘만 곱한다. 칠한 곳은 UV 섬 밖 빈 텍셀로
#     조금 번지게 해(밉맵 가장자리) 초록 테두리가 남지 않게 한다.
# Out: <out>/arms_unity.json + T_soldier_*.png (같은 이름), 미리보기 렌더 <out>/render_*.png, 로그 <out>/roll_log.txt
import bpy, sys, os, json, math, shutil
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
src, out = args[0], args[1]
os.makedirs(out, exist_ok=True)
log = []
def say(*a):
    s = ' '.join(str(x) for x in a); print(s); log.append(s)

# ---- 모양 수치 (손목 맨살 반지름 Rw 의 배수, t = 팔꿈치 0 -> 손목 1) ----
ROLL_TOP, ROLL_BOTTOM = -0.14, 0.14          # 접힌 띠(팔꿈치 바로 위~아래)
FOLDS = [-0.14, -0.05, 0.04, 0.14]           # 접힌 골(띠 위 끝 · 두 골 · 아래 입술)
BAND_SCALE = 1.08                            # 띠 굵기 = 원래 소매 굵기 x (접혀 두꺼워짐)
RIDGE = 0.09                                 # 접힌 겹마다 볼록한 정도(반지름 비)
KEEP_SHAPE = 0.15                            # 맨살 팔뚝에 남기는 원래 단면 굴곡의 비율
# 팔뚝 모양(감독 검수 2026-11-27 "고무관처럼"): 손목 쪽 납작한 타원(엄지-새끼 방향이 넓음), 팔꿈치 아래 근육 두 덩이, 자뼈 끝 돌기
ELLIPSE = 0.24                               # 손목에서 단면 가로/세로 비 ≈ 1 + 이 값
EXTENSORS = (0.24, 0.15, 0.55, 0.07)         # (t 중심, t 폭, 각도 rad: 엄지 쪽 0, 크기) 폄근 · 위팔노근
FLEXORS = (0.34, 0.17, 3.4, 0.05)            # 굽힘근 (반대쪽, 조금 아래)
ULNA = (0.90, 0.035, 3.6, 0.045)             # 자뼈 끝 돌기 (새끼 쪽, 손목 바로 위, 아주 약하게)
MOTTLE = 0.45                                # 피부 얼룩 세기 = 손 피부 밝기 편차 x 이 값
WRIST_LINE = (0.93, 0.06, 0.85)              # 옛 끝단 자리(손목) 텍셀을 피부색 · 평평한 노멀로 섞는 (t 중심, 폭, 최대 비율)
SKIN_PROFILE = [(0.14, 1.62), (0.30, 1.58), (0.50, 1.42), (0.70, 1.22), (0.86, 1.06), (0.95, 1.0)]
HAND_FROM = 0.95                             # 이 뒤는 손: 그대로

def skin_ratio(t):
    xs = [p[0] for p in SKIN_PROFILE]; ys = [p[1] for p in SKIN_PROFILE]
    return np.interp(t, xs, ys)

def smooth(a, b, x):
    u = np.clip((x - a) / (b - a), 0, 1); return u * u * (3 - 2 * u)

data = json.load(open(os.path.join(src, 'arms_unity.json'), encoding='utf-8'))

def load_img(name):
    p = os.path.join(src, name)
    im = bpy.data.images.load(p); W, H = im.size
    a = np.empty(W * H * 4, np.float32); im.pixels.foreach_get(a)
    cs = im.colorspace_settings.name
    return im, a.reshape(H, W, 4), W, H, cs

def save_img(im, arr, name):
    im.pixels.foreach_set(arr.reshape(-1).astype(np.float32))
    im.filepath_raw = os.path.join(out, name); im.file_format = 'PNG'; im.save()

names = {'base': 'T_soldier_BaseColor_detail.png', 'base0': 'T_soldier_BaseColor.png', 'normal': 'T_soldier_Normal.png',
         'normal_d': 'T_soldier_Normal_detail.png', 'mask': 'T_soldier_SkinMask.png', 'metal': 'T_soldier_Metallic.png',
         'rough': 'T_soldier_Roughness.png'}
imgs = {k: load_img(v) for k, v in names.items() if os.path.exists(os.path.join(src, v))}
_, base, W, H, _ = imgs['base']
mask = imgs['mask'][1]
say('textures', {k: (v[2], v[3], v[4]) for k, v in imgs.items()})

def raster(uvs, tris, values=None):
    """yields (ys, xs, bary weights, tri index) for texels whose centre lies in each uv triangle"""
    for ti, (a, b, c) in enumerate(tris):
        P = uvs[[a, b, c]] * np.array([W, H])
        x0, y0 = np.floor(P.min(0)).astype(int); x1, y1 = np.ceil(P.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        v0, v1 = P[1] - P[0], P[2] - P[0]
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-12: continue
        px, py = xs - P[0][0], ys - P[0][1]
        l1 = (px * v1[1] - v1[0] * py) / den; l2 = (v0[0] * py - px * v0[1]) / den; l0 = 1 - l1 - l2
        inside = (l0 >= -1e-4) & (l1 >= -1e-4) & (l2 >= -1e-4)
        if not inside.any(): continue
        yield (ys[inside] - 0.5).astype(int), (xs[inside] - 0.5).astype(int), np.stack([l0[inside], l1[inside], l2[inside]], 1), ti

covered = np.zeros((H, W), bool)        # every texel any arm triangle uses
paint_t = np.full((H, W), np.nan, np.float32)   # t of the texels to repaint (forearm + band)
hand_t = np.full((H, W), np.nan, np.float32)    # t of the hand's texels near the wrist
paint_q = np.zeros((H, W, 3), np.float32)       # the texel's place on the arm (see Q below)
new_arms = []
skin_cols = []
for arm in data['arms']:
    V = np.array(arm['vertices'], float).reshape(-1, 3); Nn = np.array(arm['normals'], float).reshape(-1, 3)
    UV = np.array(arm['uv'], float).reshape(-1, 2) % 1.0
    T = np.array(arm['triangles'], int).reshape(-1, 3)
    bi = np.array(arm['boneIndex'], int).reshape(-1, 4); bw = np.array(arm['boneWeight'], float).reshape(-1, 4)
    nb = len(arm['bones']); Wt = np.zeros((len(V), nb))
    for k in range(4): np.add.at(Wt, (np.arange(len(V)), bi[:, k]), bw[:, k])
    wU, wF, wH = Wt[:, 1], Wt[:, 2], Wt[:, 3:].sum(1)
    def mid(a, b):
        m = np.minimum(a, b); s = m > 0.15; return (V[s] * m[s, None]).sum(0) / m[s].sum()
    E, Wr = mid(wU, wF), mid(wF, wH)
    ax = Wr - E; L = np.linalg.norm(ax); t = (V - E) @ ax / L ** 2
    foot = E + t[:, None] * ax; d = V - foot; r = np.linalg.norm(d, axis=1)
    # the wrist's bare radius: skin vertices just past the forearm/hand split
    Rw = np.median(r[(t > 0.86) & (t < 0.96)])
    # mean radius of the sleeve at each t (the cross-section kept, its size changed)
    bins = np.linspace(-0.3, 1.0, 66); rbar = np.zeros(len(V))
    idx = np.clip(np.digitize(t, bins), 1, len(bins) - 1)
    means = np.array([r[idx == k].mean() if (idx == k).any() else np.nan for k in range(len(bins))])
    means = np.where(np.isnan(means), np.nanmean(means), means)
    # smooth the profile (a fold of the old cuff should not show through)
    means = np.convolve(np.pad(means, 2, mode='edge'), np.ones(5) / 5, mode='valid')
    rbar = means[idx]
    # target radius
    target = r.copy()
    band = (t >= ROLL_TOP - 0.04) & (t < ROLL_BOTTOM + 0.02)
    bare = (t >= ROLL_BOTTOM) & (t < HAND_FROM)
    # bare forearm: to the skin profile, keeping only a quarter of the sleeve's own bumps (the old cuff's folds and its
    # inner layer would otherwise show as rings on the skin; the inner layer stays just inside the outer one)
    # (감독 검수 "고무관처럼 보임") the forearm's own shape, mild: the muscle mass below the elbow (the extensors and
    # brachioradialis toward the thumb side, the flexors on the other side a little lower), the section flattening into a
    # wide, thin oval toward the wrist, and a faint knob of the ulna's end on the little-finger side
    thumb = Wt[:, 4:7].sum(1) > 0.5
    X = V[thumb].mean(0) - Wr
    axu = ax / L; X = X - (X @ axu) * axu; X /= np.linalg.norm(X); Y = np.cross(axu, X)
    dh = d / np.maximum(r[:, None], 1e-9); cs, sn = dh @ X, dh @ Y; th = np.arctan2(sn, cs)
    q = 1 + ELLIPSE * smooth(0.35, 0.92, t)
    shape = 1 / np.sqrt(cs ** 2 / q + sn ** 2 * q)
    def lobe(tc, tw, phi, amp, power=2):
        return amp * np.exp(-((t - tc) / tw) ** 2) * np.maximum(0, np.cos(th - phi)) ** power
    shape *= 1 + lobe(*EXTENSORS) + lobe(*FLEXORS) + lobe(*ULNA, power=8)
    k_bare = np.minimum(1.0, skin_ratio(t) * Rw * shape * (1 + KEEP_SHAPE * (r / rbar - 1)) / np.maximum(r, 1e-6))
    # rolled band: the sleeve kept, folded thicker, a ridge per roll and a groove between
    ridge = np.zeros(len(V))
    for g0, g1 in zip(FOLDS[:-1], FOLDS[1:]):
        u = np.clip((t - g0) / (g1 - g0), 0, 1); ridge = np.where((t >= g0) & (t < g1), np.sin(np.pi * u), ridge)
    k_band = BAND_SCALE * (1 + RIDGE * ridge)
    # blend: band above ROLL_BOTTOM, a short lip down to skin just below it, the upper arm above ROLL_TOP unchanged
    lip = smooth(ROLL_BOTTOM, ROLL_BOTTOM + 0.025, t)
    top = smooth(ROLL_TOP - 0.04, ROLL_TOP, t)
    k = np.where(t < ROLL_BOTTOM, 1 + (k_band - 1) * top, k_bare * lip + k_band * (1 - lip))
    hand = (t >= HAND_FROM) | (wH > 0.5)
    k = np.where(hand, 1.0, k)
    # ease into the hand over the last bit so the wrist joins without a step
    k = np.where((t > 0.88) & ~hand, 1 + (k - 1) * (1 - smooth(0.88, HAND_FROM, t)), k)
    k = np.where(t < ROLL_TOP - 0.04, 1.0, k)
    V2 = foot + d * k[:, None]
    moved = np.abs(k - 1) > 1e-4
    # normals again where it moved, shared by vertices at one position (uv seams split them)
    key = np.round(V2 * 1e5).astype(np.int64); _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.reshape(-1)
    fn = np.cross(V2[T[:, 1]] - V2[T[:, 0]], V2[T[:, 2]] - V2[T[:, 0]])
    acc = np.zeros((inv.max() + 1, 3))
    for c in range(3): np.add.at(acc, inv[T[:, c]], fn)
    vn = acc[inv]; vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-12)
    # the game's handedness flips the winding: keep the old normal's side
    flip = (vn * Nn).sum(1) < 0
    vn[flip] *= -1
    N2 = np.where(moved[:, None], vn, Nn)
    say(f"{arm['name']}: forearm {L:.3f}, wrist bare radius {Rw*100:.2f}, moved {moved.sum()} of {len(V)}; "
        + ' '.join(f"t{x:+.1f}:{np.mean(r[(t>=x)&(t<x+0.1)])*100:.1f}->{np.mean(np.linalg.norm(V2-foot,axis=1)[(t>=x)&(t<x+0.1)])*100:.1f}" for x in np.arange(-0.3, 1.0, 0.1) if ((t>=x)&(t<x+0.1)).any()))
    # skin colour of this arm's hand (back of the hand and wrist, mask on)
    hv = (t > 1.0) & (t < 1.4) & (wH > 0.9)
    px = np.clip((UV[hv, 0] * W).astype(int), 0, W - 1); py = np.clip((UV[hv, 1] * H).astype(int), 0, H - 1)
    sk = mask[py, px, 0] > 0.8
    skin_cols.append(base[py[sk], px[sk], :3])
    # texels: everything the arm covers; the forearm + band triangles marked with t
    tri_t = t[T]
    # each vertex's place on the arm (along the forearm, round it): the mottling is taken from there, not from the uv
    # (2026-11-27 감독 검수: noise made in uv space broke at every uv island's edge and showed as pale squares)
    Q = np.stack([t * L, np.cos(th) * Rw * 1.5, np.sin(th) * Rw * 1.5], 1).astype(np.float32)
    for ys, xs, bary, ti in raster(UV, T):
        covered[ys, xs] = True
        tt = tri_t[ti]
        if tt.max() < ROLL_TOP - 0.06 or tt.min() > 1.25: continue
        if (wH[T[ti]] > 0.5).all():
            hand_t[ys, xs] = bary @ tt      # the hand near the wrist: only its baked cuff shadow is evened out
            continue
        paint_t[ys, xs] = bary @ tt
        paint_q[ys, xs] = bary @ Q[T[ti]]
    a2 = dict(arm)
    a2['vertices'] = V2.reshape(-1).tolist(); a2['normals'] = N2.reshape(-1).tolist()
    new_arms.append(a2)

skin = np.median(np.concatenate(skin_cols), axis=0)
say('hand skin colour (median)', skin.round(3))
# ---- repaint ----
rng = np.random.default_rng(7)
def lowfreq(scale, amp):
    """smooth value noise: a coarse random grid sampled with a smoothstep-bilinear blend (no blocks or stripes)"""
    small = rng.normal(0, 1, (H // scale + 3, W // scale + 3)).astype(np.float32)
    y = np.arange(H, dtype=np.float32) / scale; x = np.arange(W, dtype=np.float32) / scale
    y0 = y.astype(int); x0 = x.astype(int); fy = y - y0; fx = x - x0
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    a = small[y0][:, x0]; b = small[y0][:, x0 + 1]; c = small[y0 + 1][:, x0]; d_ = small[y0 + 1][:, x0 + 1]
    top_ = a + (b - a) * fx[None, :]; bot = c + (d_ - c) * fx[None, :]
    return (top_ + (bot - top_) * fy[:, None]) * amp
# skin mottling as uneven as the back of the hand: blotches at three sizes, scaled to the hand skin's own spread of brightness
hand_skin = np.concatenate(skin_cols); hand_lum = hand_skin @ np.array([0.2126, 0.7152, 0.0722])
spread = float(np.std(hand_lum) / np.mean(hand_lum))
# (the hand's spread includes its baked shading, so the blotches take under half of it, mostly in large patches)
pt = paint_t
has = ~np.isnan(pt)
def vnoise3(P, scale):
    """smooth 3-D value noise at the texels' places on the arm: continuous across uv seams"""
    p = P / scale; i = np.floor(p); f = p - i; f = f * f * (3 - 2 * f)
    def h(dx, dy, dz):
        a = (i[:, 0] + dx) * 127.1 + (i[:, 1] + dy) * 311.7 + (i[:, 2] + dz) * 74.7
        return (np.sin(a) * 43758.5453) % 1.0 * 2 - 1
    x0 = h(0, 0, 0) + (h(1, 0, 0) - h(0, 0, 0)) * f[:, 0]; x1 = h(0, 1, 0) + (h(1, 1, 0) - h(0, 1, 0)) * f[:, 0]
    x2 = h(0, 0, 1) + (h(1, 0, 1) - h(0, 0, 1)) * f[:, 0]; x3 = h(0, 1, 1) + (h(1, 1, 1) - h(0, 1, 1)) * f[:, 0]
    y0 = x0 + (x1 - x0) * f[:, 1]; y1 = x2 + (x3 - x2) * f[:, 1]
    return y0 + (y1 - y0) * f[:, 2]
Lref = 0.2   # the forearm's length in the arms' units (about 0.2 for both)
qs = paint_q[has].astype(np.float64)
v = vnoise3(qs, Lref / 2.5) + 0.6 * vnoise3(qs + 17.3, Lref / 8) + 0.25 * vnoise3(qs + 41.7, Lref / 25)
v *= MOTTLE * spread / max(float(np.std(v)), 1e-6)
vary = np.zeros((H, W), np.float32); vary[has] = v
say('hand skin brightness spread', round(spread, 3))
bare_px = has & (pt >= ROLL_BOTTOM)
band_px = has & (pt < ROLL_BOTTOM)
orig_skin = mask[..., 0] > 0.5
to_skin = bare_px & ~orig_skin
# a little shade under the band's lip on the skin
lip_shade = 1 - 0.28 * np.exp(-((np.nan_to_num(pt) - ROLL_BOTTOM) / 0.035) ** 2) * bare_px
groove = np.ones((H, W), np.float32)
for g in FOLDS:
    groove -= 0.38 * np.exp(-((np.nan_to_num(pt) - g) / 0.012) ** 2) * band_px
crest = np.zeros((H, W), np.float32)
for g0, g1 in zip(FOLDS[:-1], FOLDS[1:]):
    c = (g0 + g1) / 2; crest += 0.10 * np.exp(-((np.nan_to_num(pt) - c) / 0.03) ** 2) * band_px
# the band of texels round the wrist (forearm and hand side) where the old cuff's edge and crease were: strongest at t 0.93
ht_all = np.where(np.isnan(hand_t), pt, hand_t)
wrist_w = np.where(np.isnan(ht_all), 0.0, np.exp(-((np.nan_to_num(ht_all) - WRIST_LINE[0]) / WRIST_LINE[1]) ** 2) * WRIST_LINE[2]).astype(np.float32)
wrist_w[wrist_w < 0.02] = 0
say('wrist line texels', int((wrist_w > 0.3).sum()))
say('texels: covered', covered.sum(), 'forearm+band', has.sum(), 'repainted to skin', to_skin.sum(), 'band', band_px.sum())

# grow a mask out into texels no triangle uses (mip edges)
def grow(m, steps):
    g = m.copy()
    for _ in range(steps):
        n = g.copy()
        n[1:] |= g[:-1]; n[:-1] |= g[1:]; n[:, 1:] |= g[:, :-1]; n[:, :-1] |= g[:, 1:]
        g = np.where(covered, g, n)
    return g
skin_grow = grow(to_skin, 8)

def at_size(m, h, w):
    """a (H, W) mask at another texture size (the normal / metal / roughness maps are 2048)"""
    if (h, w) == m.shape: return m
    f = H // h
    return m.reshape(h, f, w, f).mean(axis=(1, 3)) >= 0.5

def paint(key, fn):
    if key not in imgs: return
    im, arr, w, h, cs = imgs[key]
    arr = arr.copy(); fn(arr)
    save_img(im, arr, names[key])

def paint_base(arr):
    # a touch more red in the blotches than in the rest (blood under the skin), as on the hand
    col = skin[None, None, :] * (1 + vary[..., None] * np.array([1.15, 1.0, 0.9])[None, None, :])
    a = arr[..., :3]
    # the hand's texture has the old cuff's shadow baked round the wrist: even its brightness out to the hand's own
    ht = np.where(np.isnan(hand_t), pt, hand_t)
    near = orig_skin & ~np.isnan(ht) & (ht > 0.75) & (ht < 1.2)
    if near.any():
        lum = a[near] @ np.array([0.2126, 0.7152, 0.0722]); tt_ = ht[near]
        ref = np.median(lum[(tt_ > 1.08) & (tt_ < 1.2)]) if ((tt_ > 1.08) & (tt_ < 1.2)).any() else np.median(lum)
        edges = np.linspace(0.75, 1.2, 19); fix = np.ones_like(lum)
        for lo, hi in zip(edges[:-1], edges[1:]):
            s = (tt_ >= lo) & (tt_ < hi)
            if s.sum() > 50: fix[s] = np.clip(ref / max(np.median(lum[s]), 1e-4), 0.85, 1.35)
        fade = 1 - smooth(1.05, 1.18, tt_)
        a[near] *= (1 + (fix - 1) * fade)[:, None]
        say('wrist shadow evened: bins', ' '.join(f'{lo:.2f}:{np.median(lum[(tt_>=lo)&(tt_<hi)]) if ((tt_>=lo)&(tt_<hi)).sum()>50 else 0:.3f}' for lo, hi in zip(edges[:-1], edges[1:])), 'ref', round(float(ref), 3))
    # the rolled band repainted in one sleeve colour, the cloth's own weave and creases kept as brightness only (the scan's
    # orange grime on the old sleeve read as skin in the Unity colour match and left spots under the band)
    lumw = np.array([0.2126, 0.7152, 0.0722])
    band_cloth = band_px & ~orig_skin
    sleeve = np.median(a[band_cloth], axis=0); sl = float(sleeve @ lumw)
    detail = np.clip(((a[band_px] @ lumw) / max(sl, 1e-4)) ** 0.6, 0.6, 1.4)
    band_col = sleeve[None, :] * detail[:, None] * (1 + 0.5 * vary[band_px])[:, None]
    # (2026-11-27 감독 검수: a faint green line and the old cuff's crease at the wrist, seen at dawn) the hand's texels round
    # the wrist where the old cuff sat take the painted skin, most at its middle (wrist_w; its normals are flattened too)
    wt_ = np.where(wrist_w > 0, wrist_w, 0)[..., None]
    a[:] = a * (1 - wt_) + col * wt_
    a[skin_grow] = col[skin_grow]
    # the hand's own skin near the wrist eased toward the painted skin, so no line where the two meet
    ease = orig_skin & bare_px & (pt > 0.70) & (pt < 0.97)
    wgt = (1 - smooth(0.80, 0.97, np.nan_to_num(pt)))[ease][:, None]
    a[ease] = a[ease] * (1 - wgt) + col[ease] * wgt
    a[bare_px] *= lip_shade[bare_px, None]
    a[band_px] = band_col * (groove[band_px] + crest[band_px])[:, None]
    # the grown seam texels (no triangle) take the colour of the painted texel next to them, not a flat one
    filled = has.copy(); out_ = a.copy()
    for _ in range(8):
        nxt = filled.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src_ = np.roll(filled, (dy, dx), (0, 1)); colr = np.roll(out_, (dy, dx), (0, 1))
            take = src_ & ~nxt & skin_grow & ~covered
            out_[take] = colr[take]; nxt |= take
        filled = nxt
    a[skin_grow & ~covered] = out_[skin_grow & ~covered]
    arr[..., :3] = np.clip(a, 0, 1)
paint('base', paint_base); paint('base0', paint_base)
def paint_flat_normal(arr):
    m = at_size(skin_grow, *arr.shape[:2])
    arr[m, 0] = 0.5; arr[m, 1] = 0.5; arr[m, 2] = 1.0
    h_, w_ = arr.shape[:2]; f_ = H // h_
    ww = wrist_w.reshape(h_, f_, w_, f_).mean(axis=(1, 3))[..., None] if f_ > 1 else wrist_w[..., None]
    arr[..., :3] = arr[..., :3] * (1 - ww) + np.array([0.5, 0.5, 1.0])[None, None, :] * ww
paint('normal', paint_flat_normal); paint('normal_d', paint_flat_normal)
def paint_mask(arr): arr[at_size(skin_grow | (wrist_w > 0.3), *arr.shape[:2]), :3] = 1.0
paint('mask', paint_mask)
skin_rough = np.median(imgs['rough'][1][at_size(orig_skin, *imgs['rough'][1].shape[:2])][:, 0]) if 'rough' in imgs else 0.6
def paint_rough(arr): arr[at_size(skin_grow, *arr.shape[:2]), :3] = skin_rough
paint('rough', paint_rough)
def paint_metal(arr): arr[at_size(skin_grow, *arr.shape[:2]), :3] = 0.0
paint('metal', paint_metal)
say('skin roughness', round(float(skin_rough), 3))
for f in ['T_skin_micro_normal.png']:
    if os.path.exists(os.path.join(src, f)): shutil.copy(os.path.join(src, f), os.path.join(out, f))
json.dump({'arms': new_arms}, open(os.path.join(out, 'arms_unity.json'), 'w', encoding='utf-8'))
open(os.path.join(out, 'roll_log.txt'), 'w', encoding='utf-8').write('\n'.join(log))
say('wrote', out)
