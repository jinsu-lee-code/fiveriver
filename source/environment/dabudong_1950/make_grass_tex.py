# Dry summer grass tufts for the Dabudong terrain (2026-11-15).
# August 1950 on the Taegu front: the worst drought in thirty years and 37 C heat (Dabudong memorial hall record).
# The blades are drawn pale and neutral so the terrain's dry / healthy tint decides the colour.
#   python make_grass_tex.py <out dir>
import sys, math, random
from PIL import Image, ImageDraw, ImageFilter

out = sys.argv[1]


def tuft(seed, n, height, spread, lean, seedheads):
    random.seed(seed)
    S = 4; W = 256 * S
    im = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    blades = []
    for i in range(n):
        x0 = W / 2 + random.gauss(0, spread * W * 0.35)
        h = W * height * random.uniform(0.45, 1.0)
        bend = random.gauss(lean, 0.22) * h
        blades.append((random.random(), x0, h, bend))
    blades.sort()
    for depth, x0, h, bend in blades:
        shade = 0.62 + 0.38 * depth                       # blades at the back are darker
        base = (int(214 * shade), int(200 * shade), int(150 * shade))
        tip = (int(236 * shade), int(222 * shade), int(176 * shade))
        w0 = random.uniform(17, 30) * S / 4
        pts = 14
        prev = None
        for k in range(pts + 1):
            t = k / pts
            x = x0 + bend * t * t + math.sin(t * 3 + depth * 9) * 2 * S * t
            y = W - 2 - h * t
            w = max(1.0, w0 * (1 - t) ** 0.8)
            c = tuple(int(base[j] + (tip[j] - base[j]) * t) for j in range(3)) + (255,)
            if prev:
                d.line([prev, (x, y)], fill=c, width=int(round(w)))
            prev = (x, y)
        if seedheads and random.random() < seedheads:      # the pale plume of a seeding grass
            for k in range(26):
                a = random.uniform(-0.9, 0.9); r = random.uniform(0, 26 * S / 4)
                px = prev[0] + math.sin(a) * r * 0.6 + bend * 0.06; py = prev[1] + abs(math.cos(a)) * r * 0.2 + random.uniform(0, 34 * S / 4)
                d.ellipse([px - 5 * S / 4, py - 11 * S / 4, px + 5 * S / 4, py + 11 * S / 4], fill=(int(238 * shade), int(226 * shade), int(190 * shade), 235))
    im = im.resize((256, 256), Image.LANCZOS)
    # bleed the colour into the transparent texels so mips do not pick up a dark fringe
    rgb = im.convert('RGB'); a = im.split()[3]
    fill = Image.new('RGB', (256, 256), (205, 192, 146))
    blurred = Image.composite(rgb, fill, a.point(lambda v: 255 if v > 8 else 0)).filter(ImageFilter.GaussianBlur(3))
    rgb = Image.composite(rgb, blurred, a.point(lambda v: 255 if v > 200 else 0))
    r, g, b = rgb.split()
    return Image.merge('RGBA', (r, g, b, a))


tuft(1950, 46, 0.86, 0.55, 0.10, 0.0).save(out + '/T_Grass_DryTuft.png')
tuft(815, 22, 0.97, 0.30, 0.16, 0.55).save(out + '/T_Grass_SeedingTall.png')
print('ok')
