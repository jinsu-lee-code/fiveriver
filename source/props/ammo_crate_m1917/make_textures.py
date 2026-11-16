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


def stencil_atlas():
    W, H = 2048, 1024
    mask = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mask)
    # left half: long side of the box
    block(d, [("1344 CARTRIDGES", 1.0), ("CAL. .30", 1.15), ("BALL  M2", 1.15), ("8 RD. CLIPS IN BANDOLEERS", 0.8), ("LOT  F.A. 1157", 0.75)],
          (40, 60, 1000, 980), STENCIL)
    # top right: lid
    block(d, [("CAL. .30 BALL M2", 1.0), ("8 RD. CLIPS", 0.9)], (1060, 80, 2010, 470), STENCIL)
    # bottom right: end of the box
    block(d, [("CAL .30", 1.0), ("BALL", 1.0)], (1180, 560, 1890, 980), STENCIL)
    a = wear(np.asarray(mask), 1950)
    rgba = np.zeros((H, W, 4), np.uint8)
    rgba[..., 0:3] = (24, 22, 18)        # near-black stencil paint
    rgba[..., 3] = a
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
    shade = 0.82 + 0.10 * wale + 0.04 * warp + 0.06 * (fiber - 0.5) + 0.10 * (mottle - 0.5)
    base = np.array([150, 132, 92], np.float32)                       # khaki cotton (WWII/Korea bandoleer)
    rgb = np.clip(base[None, None, :] * shade[..., None], 0, 255).astype(np.uint8)
    Image.fromarray(rgb, "RGB").save(os.path.join(OUT, "T_PR_Bandoleer_BaseColor.png"))
    # normal from the twill height
    hgt = 0.6 * wale + 0.25 * warp + 0.15 * fiber
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
