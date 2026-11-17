"""M1917 ammunition packing box: stencil decal atlas and bandoleer cloth texture (2026-11-16).

Run: uv run --no-project --with pillow --with numpy python -I source/props/ammo_crate_m1917/make_textures.py
Writes into five_river/Assets/_Project/Art/Models/Props/AmmoCrateM1917/Textures/. Deterministic (fixed seeds).

Stencil text follows period markings of .30 cal M2 ball in 8-round M1 rifle clips packed in bandoleers
(dealer photos of WWII/Korea-era crates: "CAL .30 BALL M2", "8 RD CLIPS IN BANDOLEERS", round count, lot).
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "five_river", "Assets", "_Project", "Art", "Models", "Props", "AmmoCrateM1917", "Textures")
os.makedirs(OUT, exist_ok=True)
STENCIL = "C:/Windows/Fonts/STENCIL.TTF"


def wear(alpha, seed, amount=0.35):
    """Ragged sprayed edges and scuffed patches on a stencil alpha mask (0..255)."""
    rng = np.random.default_rng(seed)
    a = alpha.astype(np.float32) / 255.0
    h, w = a.shape
    # spray: blur then threshold against noise -> rough edges
    blur = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.2)), np.float32) / 255.0
    grain = rng.random((h, w)).astype(np.float32)
    edge = np.clip((blur - 0.35 - (grain - 0.5) * 0.35) * 4.0, 0.0, 1.0)
    # scuffs: low-frequency noise removes paint in patches
    low = rng.random((h // 32 + 1, w // 32 + 1)).astype(np.float32)
    low = np.asarray(Image.fromarray((low * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32) / 255.0
    fine = rng.random((h // 4 + 1, w // 4 + 1)).astype(np.float32)
    fine = np.asarray(Image.fromarray((fine * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR), np.float32) / 255.0
    keep = np.clip(1.0 - np.clip((low - (1.0 - amount)) * 3.0, 0, 1) - np.clip((fine - 0.92) * 6.0, 0, 1), 0, 1)
    return (np.clip(edge * keep, 0, 1) * 255).astype(np.uint8)


def block(draw, lines, box, font_path):
    """Lines centred in box (x0,y0,x1,y1), each line as large as fits its share of the height."""
    x0, y0, x1, y1 = box
    n = len(lines)
    row = (y1 - y0) / n
    for i, (text, scale) in enumerate(lines):
        size = int(row * 0.78 * scale)
        font = ImageFont.truetype(font_path, size)
        while draw.textlength(text, font=font) > (x1 - x0) * 0.94 and size > 8:
            size -= 2
            font = ImageFont.truetype(font_path, size)
        tw = draw.textlength(text, font=font)
        cy = y0 + row * (i + 0.5)
        draw.text(((x0 + x1 - tw) / 2, cy), text, font=font, fill=255, anchor="lm")


def clip_symbol(d, box, lw):
    """The packing silhouette for clips: a rounded frame with eight cartridge heads in two rows (fig. 12.6)."""
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, radius=(y1 - y0) * 0.22, outline=255, width=lw)
    r = (y1 - y0) * 0.13
    for row in range(2):
        for c in range(4):
            cx = x0 + (x1 - x0) * (0.17 + 0.22 * c)
            cy = y0 + (y1 - y0) * (0.32 + 0.36 * row)
            d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)


def flaming_bomb(d, cx, cy, r):
    """Ordnance insignia: a shell with a flame."""
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    d.rectangle((cx - r * 0.28, cy - r * 1.35, cx + r * 0.28, cy - r * 0.8), fill=255)
    for k, (dx, h) in enumerate(((-0.42, 1.3), (-0.14, 1.9), (0.14, 2.3), (0.42, 1.5))):
        d.polygon([(cx + dx * r - r * 0.2, cy - r * 1.35), (cx + dx * r + r * 0.2, cy - r * 1.35),
                   (cx + dx * r + r * 0.25, cy - r * (1.35 + h * 0.5)), (cx + dx * r - r * 0.05, cy - r * (1.35 + h))], fill=255)


K = 2  # 2026-11-17: the atlas is drawn at twice the old vertical resolution so the small lines stay legible


def text(d, xy, s, size, anchor="lm", bold=0):
    """Stencil lettering at layout scale K; `bold` thickens the strokes of the small lines (px at K = 1)."""
    d.text((xy[0] * K, xy[1] * K), s, font=ImageFont.truetype(STENCIL, size * K), fill=255, anchor=anchor,
           stroke_width=int(round(bold * K)), stroke_fill=255)


def stencil_atlas():
    """4096 x 2048: [long side | end | bandoleer reference card], the same UV cells as the 4096 x 1024 atlas it
    replaces. Layout after TM 9-1990 (C2, 1943) fig. 12.6, with the code and nomenclature of ball M2 in 8-round
    clips in bandoleers (T1EHA, as on the surviving box). 2026-11-17: twice the rows (5.9 px/mm up the box side),
    the small lines (weight, cube, lot, packing) a little heavier, and less paint scuffed off them: at night they
    had broken up below the alpha cut-off."""
    AW, AH = 4096, 1024 * K
    # long side, drawn 1276 x 1024 layout units (0.43 x 0.345 m) then fitted into its atlas cell
    side = Image.new("L", (1276 * K, 1024 * K), 0)
    d = ImageDraw.Draw(side)
    text(d, (638, 92), "1344 CARTRIDGES CAL. .30", 88, "mm")
    text(d, (638, 186), "8 RD. CLIPS BANDOLEERS", 54, "mm", bold=1.2)
    text(d, (70, 330), "T1EHA", 122)
    clip_symbol(d, (560 * K, 268 * K, 800 * K, 420 * K), 13 * K)
    clip_symbol(d, (560 * K, 470 * K, 800 * K, 622 * K), 13 * K)
    text(d, (70, 640), "BALL M2", 58, bold=1.0)
    text(d, (70, 830), "111 LB.", 56, bold=1.6)
    text(d, (70, 910), "1.5 CU. FT.", 56, bold=1.6)
    flaming_bomb(d, 1090 * K, 520 * K, 52 * K)
    text(d, (1210, 700), "REPACKED LOT", 52, "rm", bold=1.6)
    text(d, (1210, 780), "R.A. C65467", 66, "rm", bold=1.0)
    # end, drawn 594 x 1024 (0.20 x 0.345 m): two lines above the hand-hold, the rest below it
    end = Image.new("L", (594 * K, 1024 * K), 0)
    d = ImageDraw.Draw(end)
    text(d, (297, 34), "1344 CARTRIDGES CAL..30", 38, "mm", bold=1.4)
    text(d, (297, 86), "T1EHA", 58, "mm", bold=0.8)
    text(d, (297, 300), "BALL M2", 54, "mm", bold=0.8)
    text(d, (297, 372), "8RD CLIPS BANDOLEERS", 40, "mm", bold=1.4)
    clip_symbol(d, (190 * K, 520 * K, 404 * K, 660 * K), 11 * K)
    text(d, (297, 770), "REPACKED LOT", 42, "mm", bold=1.4)
    text(d, (297, 846), "R.A. C65467", 56, "mm", bold=0.8)
    # reference card (par. 30), printed black: 400 x 600
    card = Image.new("L", (400 * K, 600 * K), 0)
    d = ImageDraw.Draw(card)
    f = "C:/Windows/Fonts/arialbd.ttf"
    for i, (s_, sz) in enumerate((("AMMUNITION LOT", 30), ("R.A. 65467", 44), ("48 CARTRIDGES", 28), ("BALL, CAL. .30, M2", 28), ("8-RD. CLIPS", 28),
                                  ("REMINGTON ARMS CO.", 22), ("POWDER  IMR 4895", 22), ("LOT 20713", 22))):
        d.text((200 * K, (70 + i * 64) * K), s_, font=ImageFont.truetype(f, sz * K), fill=255, anchor="mm")
    rgba = np.zeros((AH, AW, 4), np.uint8)
    # chrome-yellow marking paint (par. 23: "marked in yellow"), a step lighter than before so it holds against the
    # chocolate stain under moonlight
    yellow, black = (216, 178, 60), (26, 24, 22)

    def put(img, x0, y0, w, h, colour, seed, amount):
        a = wear(np.asarray(img.resize((w, h), Image.LANCZOS)), seed, amount)
        rgba[y0:y0 + h, x0:x0 + w, 0:3] = colour
        rgba[y0:y0 + h, x0:x0 + w, 3] = a
    # colour everywhere (not only inside the cells) so the mips of the cut-out do not bleed black into the edges
    rgba[:, :3044, 0:3] = yellow
    rgba[:, 3044:, 0:3] = black
    put(side, 4, 2 * K, 2032, 1020 * K, yellow, 1950, 0.10)
    put(end, 2064, 2 * K, 942, 1020 * K, yellow, 1951, 0.10)
    put(card, 3044, 2 * K, 1044, 636 * K, black, 1952, 0.08)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, "T_PR_M1917_Stencil.png"))


def bandoleer_cloth():
    """Khaki cotton twill (diagonal wale), with slight dye mottling; tileable 1024."""
    S = 1024
    rng = np.random.default_rng(48)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    wale = 0.5 + 0.5 * np.sin((x + y) * (2 * np.pi / 6.0))            # 2/1 twill diagonal, 6 px period
    warp = 0.5 + 0.5 * np.sin(x * (2 * np.pi / 3.0))
    fiber = rng.random((S, S)).astype(np.float32)
    fiber = np.asarray(Image.fromarray((fiber * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255.0

    def tile_noise(cells, seed):
        r = np.random.default_rng(seed).random((cells, cells)).astype(np.float32)
        r = np.concatenate([r, r[:, :1]], 1); r = np.concatenate([r, r[:1, :]], 0)
        return np.asarray(Image.fromarray((r * 255).astype(np.uint8)).resize((S + S // cells, S + S // cells), Image.BICUBIC), np.float32)[:S, :S] / 255.0

    mottle = tile_noise(8, 3) * 0.6 + tile_noise(32, 4) * 0.4
    # 2026-11-17: what reads as cloth from standing height is not the weave (1 mm) but the handling: creases, a faded
    # sun side, grime rubbed into the folds. The tile is 22 cm, so cells of 6..24 give marks of 1..4 cm.
    crumple = tile_noise(6, 11) * 0.5 + tile_noise(14, 12) * 0.3 + tile_noise(28, 13) * 0.2
    ridge = 1.0 - np.abs(tile_noise(10, 21) * 2.0 - 1.0)             # thin crease lines where the noise crosses 0.5
    ridge = np.clip((ridge - 0.86) / 0.14, 0, 1)
    fade = tile_noise(4, 31)
    grime = np.clip((tile_noise(5, 41) * 0.6 + tile_noise(20, 42) * 0.4 - 0.52) * 3.0, 0, 1)
    shade = (0.84 + 0.10 * wale + 0.04 * warp + 0.06 * (fiber - 0.5) + 0.10 * (mottle - 0.5)
             + 0.11 * (crumple - 0.5) + 0.07 * (fade - 0.5) - 0.12 * ridge - 0.07 * grime)
    base = np.array([128, 124, 84], np.float32)                       # olive-drab cotton (TM 9-1990 par. 30)
    rgb = base[None, None, :] * shade[..., None]
    rgb = rgb * (1.0 - 0.18 * grime[..., None]) + np.array([70, 58, 40], np.float32)[None, None, :] * (0.18 * grime[..., None])
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    Image.fromarray(rgb, "RGB").save(os.path.join(OUT, "T_PR_Bandoleer_BaseColor.png"))
    # normal from the twill height
    hgt = 0.6 * wale + 0.25 * warp + 0.15 * fiber + 14.0 * crumple - 3.0 * ridge
    gx = np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)
    gy = np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)
    k = 1.2
    n = np.stack([-gx * k, gy * k, np.ones_like(gx)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, "T_PR_Bandoleer_Normal.png"))


if __name__ == "__main__":
    stencil_atlas()
    bandoleer_cloth()
    print("textures ->", OUT)
