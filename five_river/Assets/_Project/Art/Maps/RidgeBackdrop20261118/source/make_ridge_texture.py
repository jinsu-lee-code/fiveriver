# -*- coding: utf-8 -*-
# 먼 산 띠 텍스처 (2026-11-18). 절차 생성, 사진 파일은 쓰지 않는다.
#   uv run --no-project -q --with pillow --with numpy python -I make_ridge_texture.py <출력.png>
# 근거 사진(눈으로 보고 따라 만듦): US Army, "Hill 518", 1950-09, 왜관 북쪽 (퍼블릭 도메인)
#   https://commons.wikimedia.org/wiki/File:Hill_518.jpg
#   - 능선과 윗비탈은 어두운 관목 · 소나무가 얼룩덜룩 덮고, 아랫비탈의 지릉은 밝은 맨흙이 골을 따라 드러난다
#   - 능선선 위로 큰 나무가 줄지어 솟지 않는다: 낮은 수관의 잔 요철에 가끔 한두 그루
# 배치: 4096x1024, 가로로 이어 붙는다. 위 256줄 = 능선 위 나무 윤곽(알파), 아래 768줄 = 비탈(알파 1).
#   메시는 v 0.75 를 능선선에 맞춘다. 가로 1px 약 0.15 m, 나무 띠 256px = 12 m.
import sys, io, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W, H, STRIP = 4096, 1024, 256
BODY = H - STRIP
STRIP_M = 12.0
PX_M = 0.15
rng = np.random.RandomState(19500818)


def smooth(t):
    return t * t * (3 - 2 * t)


def pnoise(w, h, fx, fy, seed):
    """값 노이즈, 가로로 주기. fx: 가로 격자 수, fy: 세로 격자 수."""
    r = np.random.RandomState(seed)
    g = r.rand(fy + 2, fx)
    xs = np.arange(w) * fx / float(w)
    ys = np.arange(h) * fy / float(h)
    x0 = np.floor(xs).astype(int); xf = smooth(xs - x0)
    y0 = np.floor(ys).astype(int); yf = smooth(ys - y0)
    x1 = (x0 + 1) % fx; x0 = x0 % fx
    a = g[y0][:, x0]; b = g[y0][:, x1]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, x1]
    top = a + (b - a) * xf[None, :]
    bot = c + (d - c) * xf[None, :]
    return top + (bot - top) * yf[:, None]


def fbm(w, h, fx, fy, seed, octaves=4, gain=0.5):
    out = np.zeros((h, w)); amp = 1.0; total = 0.0
    for o in range(octaves):
        out += amp * pnoise(w, h, fx * 2 ** o, max(1, fy * 2 ** o), seed + o * 17)
        total += amp; amp *= gain
    return out / total


def warp_x(field, shift):
    """가로로 주기적인 밀기(픽셀)."""
    h, w = field.shape
    xs = (np.arange(w)[None, :] + shift) % w
    x0 = np.floor(xs).astype(int); f = xs - x0
    x1 = (x0 + 1) % w
    rows = np.arange(h)[:, None]
    return field[rows, x0] * (1 - f) + field[rows, x1] * f


# ---------------------------------------------------------------- 비탈
t = (np.arange(BODY) / float(BODY - 1))[:, None]          # 0 능선선 .. 1 바닥
low = fbm(W, BODY, 6, 2, 11, 3)
# 침식골: 능선에서 아래로 내려오는 지릉과 골. 세로로 길고, 아래로 갈수록 깊다.
bend = (fbm(W, BODY, 10, 3, 23, 3) - 0.5) * 260.0 * (0.3 + t)
gul = np.zeros((BODY, W))
for fx, amp, seed in ((26, 1.0, 31), (58, 0.55, 37), (131, 0.28, 41)):
    n = pnoise(W, BODY, fx, 2, seed)
    ridged = 1.0 - np.abs(2.0 * n - 1.0)                  # 1 지릉 .. 0 골
    gul += amp * warp_x(ridged, bend * (26.0 / fx) ** 0.5)
gul /= 1.83
gul = gul * (0.35 + 0.65 * smooth(np.clip(t / 0.35, 0, 1))) + (1 - (0.35 + 0.65 * smooth(np.clip(t / 0.35, 0, 1)))) * 0.62
relief = smooth(np.clip((gul - 0.42) / 0.36, 0, 1))                # 0 골 바닥 .. 1 지릉 등

# 수목: 위쪽은 얼룩덜룩 덮이고 아래로는 골을 따라 내려간다 (Hill 518)
cover_n = fbm(W, BODY, 52, 11, 53, 4)
clump = fbm(W, BODY, 140, 32, 59, 3)
fine = fbm(W, BODY, 260, 60, 63, 2)
veg_score = (cover_n - 0.5) * 1.5 + (clump - 0.5) * 1.5 + (fine - 0.5) * 1.1 + (0.36 - t) * 1.25 + (0.5 - relief) * 1.1 + (low - 0.5) * 0.6
veg = np.clip(veg_score * 4.0 + 0.5, 0, 1)
# 수관 알갱이
crown = fbm(W, BODY, 420, 110, 61, 2)
crown2 = fbm(W, BODY, 190, 48, 67, 2)

earth = np.array([0.52, 0.47, 0.37])                      # 달빛 · 새벽빛에 밝게 뜨는 마사토
earth_dark = np.array([0.34, 0.29, 0.22])
tree = np.array([0.150, 0.185, 0.125])
tree_lit = np.array([0.24, 0.28, 0.17])

soil_tone = (0.30 + 0.70 * relief) * (0.86 + 0.28 * fbm(W, BODY, 60, 16, 71, 3))
soil = earth_dark[None, None, :] + (earth - earth_dark)[None, None, :] * np.clip(soil_tone, 0, 1)[:, :, None]
tree_tone = np.clip(0.25 + 0.9 * (crown * 0.6 + crown2 * 0.4) * (0.6 + 0.4 * relief), 0, 1)
wood = tree[None, None, :] + (tree_lit - tree)[None, None, :] * tree_tone[:, :, None]
body = soil * (1 - veg[:, :, None]) + wood * veg[:, :, None]

# ---------------------------------------------------------------- 능선 위 윤곽
S = 2                                                       # 슈퍼샘플
mask = Image.new('L', (W * S, STRIP * S), 0)
draw = ImageDraw.Draw(mask)
py = STRIP / STRIP_M                                        # 세로 px / m
px = 1.0 / PX_M                                             # 가로 px / m

# 낮은 수관 · 관목의 잔 요철. 민둥한 구간은 0.
n1 = pnoise(W, 1, 40, 1, 81)[0]; n2 = pnoise(W, 1, 150, 1, 83)[0]; n3 = pnoise(W, 1, 520, 1, 87)[0]
presence = np.clip((pnoise(W, 1, 14, 1, 89)[0] - 0.20) * 4.0, 0, 1)
scrub_m = presence * (0.4 + 2.2 * n1 * n1 + 1.2 * n2 * n2 + 0.6 * n3) + 0.08 + 0.5 * n3 * n2                         # 능선선에 틈이 생기지 않게 한 줄
for x in range(W):
    top = STRIP - scrub_m[x] * py
    draw.rectangle([x * S, top * S, x * S + S, STRIP * S], fill=255)

# 소나무(적송): 굽은 줄기 위에 넓적한 수관 덩이. 무리 지어 듬성하게.
trees = []
x = rng.uniform(0, 30) * px
while x < W:
    gap_m = rng.choice([rng.uniform(2.5, 7.0), rng.uniform(9.0, 26.0), rng.uniform(34.0, 80.0)], p=[0.42, 0.40, 0.18])
    x += gap_m * px
    if x >= W: break
    if presence[int(x) % W] < 0.25 and rng.rand() < 0.75: continue
    trees.append((x, rng.uniform(3.6, 7.6) * (1.0 if rng.rand() < 0.8 else 1.3)))

def blob(cx, cy, rx_m, ry_m):
    rx = rx_m * px * S; ry = ry_m * py * S
    for xx in (cx - W * S, cx, cx + W * S):               # 가로 이음
        draw.ellipse([xx - rx, cy - ry, xx + rx, cy + ry], fill=255)

for tx, h_m in trees:
    base_y = STRIP * S
    lean = rng.uniform(-0.16, 0.16)
    top_x = tx * S + lean * h_m * px * S
    top_y = base_y - h_m * py * S
    trunk_w = max(2.0, 0.26 * px * S)
    # 줄기는 수관 아래쪽까지만 보인다
    for xx in (-W * S, 0, W * S):
        draw.line([tx * S + xx, base_y, top_x + xx, top_y + 0.9 * py * S], fill=255, width=int(round(trunk_w)))
    width_m = h_m * rng.uniform(0.85, 1.25)
    # 꼭대기 덩이
    blob(top_x, top_y + 0.85 * py * S, width_m * 0.30, 0.80)
    # 그 아래 좌우로 처진 층
    layers = rng.randint(2, 4)
    for i in range(layers):
        f = (i + 1) / float(layers)
        y = top_y + (0.9 + f * h_m * 0.30) * py * S
        for side in (-1, 1):
            if rng.rand() < 0.15: continue
            off = side * width_m * rng.uniform(0.16, 0.34) * (0.6 + 0.4 * f)
            blob(top_x - lean * f * h_m * 0.3 * px * S + off * px * S, y + rng.uniform(-0.25, 0.25) * py * S,
                 width_m * rng.uniform(0.17, 0.27), rng.uniform(0.5, 0.85))
        if rng.rand() < 0.7:
            blob(top_x - lean * f * h_m * 0.3 * px * S, y - 0.3 * py * S, width_m * 0.22, 0.6)

mask = mask.filter(ImageFilter.GaussianBlur(0.8 * S)).resize((W, STRIP), Image.LANCZOS)
alpha_strip = np.asarray(mask).astype(np.float64) / 255.0
alpha_strip = np.clip((alpha_strip - 0.18) / 0.64, 0, 1)

strip_tone = 0.25 + 0.55 * fbm(W, STRIP, 300, 20, 97, 2)
strip_rgb = tree[None, None, :] + (tree_lit - tree)[None, None, :] * strip_tone[:, :, None]

# 능선선 바로 아래: 위에 수관이 있는 곳은 그 수관이 비탈로 이어진다
crest_cover = np.clip(scrub_m / 1.2, 0, 1)[None, :] * np.clip(1 - t / 0.06, 0, 1)
body = body * (1 - crest_cover[:, :, None]) + wood * crest_cover[:, :, None]

out = np.zeros((H, W, 4))
out[:STRIP, :, :3] = strip_rgb
out[:STRIP, :, 3] = alpha_strip
out[STRIP:, :, :3] = body
out[STRIP:, :, 3] = 1.0
img = Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA')
path = sys.argv[1] if len(sys.argv) > 1 else 'T_ENV_RidgeBand.png'
img.save(path)
# 눈으로 보는 미리보기: 하늘색 바탕에 올려 1/2 크기
if len(sys.argv) > 2:
    bg = Image.new('RGBA', (W, H), (150, 160, 185, 255))
    bg.alpha_composite(img)
    bg = bg.convert('RGB')
    a = bg.crop((0, 0, 2400, STRIP)).resize((2400, int(STRIP * 0.047 / 0.15)), Image.LANCZOS)
    b = bg.crop((0, STRIP, 2400, H)).resize((2400, int(BODY * 0.078 / 0.15)), Image.LANCZOS)
    pv = Image.new('RGB', (2400, a.height + b.height + 60), (150, 160, 185))
    pv.paste(a, (0, 60)); pv.paste(b, (0, 60 + a.height))
    pv.save(sys.argv[2], quality=90)
print('saved', path, len(trees), 'trees')
