# -*- coding: utf-8 -*-
# 안개에 잠긴 구릉 위의 수목 얼룩 (2026-11-18). 위에서 본 무늬, 사방으로 이어 붙는다. 절차 생성.
#   uv run --no-project -q --with pillow --with numpy python -I make_slope_patch_texture.py <출력.png>
# 근거 사진: US Army "Hill 518" (1950-09, 퍼블릭 도메인) https://commons.wikimedia.org/wiki/File:Hill_518.jpg
#   맨흙 비탈에 관목 · 소나무 무리가 크고 작은 덩이로 흩어져 있다. 덩이 가장자리는 잘게 풀린다.
# R = 수목 덮개(0 맨흙 .. 1 수목), 1024x1024, 한 장이 약 160 m.
import sys
import numpy as np
from PIL import Image

N = 1024

def smooth(t):
    return t * t * (3 - 2 * t)

def tile_noise(n, f, seed):
    r = np.random.RandomState(seed)
    g = r.rand(f, f)
    xs = np.arange(n) * f / float(n)
    i0 = np.floor(xs).astype(int); fr = smooth(xs - i0)
    i1 = (i0 + 1) % f; i0 = i0 % f
    a = g[i0][:, i0]; b = g[i0][:, i1]; c = g[i1][:, i0]; d = g[i1][:, i1]
    top = a + (b - a) * fr[None, :]
    bot = c + (d - c) * fr[None, :]
    return top + (bot - top) * fr[:, None]

def fbm(n, f, seed, octaves, gain=0.5):
    out = np.zeros((n, n)); amp = 1.0; total = 0.0
    for o in range(octaves):
        out += amp * tile_noise(n, f * 2 ** o, seed + o * 13)
        total += amp; amp *= gain
    return out / total

big = fbm(N, 5, 3, 3)
mid = fbm(N, 14, 7, 3)
fine = fbm(N, 64, 11, 2)
score = (big - 0.5) * 1.5 + (mid - 0.5) * 1.4 + (fine - 0.5) * 0.7
cover = np.clip(score * 4.5 + 0.42, 0, 1)
# 덩이 안의 수관 알갱이
grain = fbm(N, 180, 17, 2)
cover = cover * (0.72 + 0.28 * grain)
img = np.zeros((N, N, 3), dtype=np.uint8)
img[:, :, 0] = img[:, :, 1] = img[:, :, 2] = (np.clip(cover, 0, 1) * 255 + 0.5).astype(np.uint8)
Image.fromarray(img, 'RGB').save(sys.argv[1] if len(sys.argv) > 1 else 'T_ENV_SlopePatch.png')
print('saved', float(cover.mean()))
