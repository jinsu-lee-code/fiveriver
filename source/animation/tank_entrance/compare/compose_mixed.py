# Combined tank entrance take (2026-11-12), recorded with rec_mixed.cs at 30 fps:
#   python compose_mixed.py  -> frames for a solo video (out_mixed/), frames for a wheel close-up clip (out_wheels/),
#                               and two still sheets in the session folder
#   blender -b --python to_mp4.py -- C:/sk_assets/capture/compare/out_mixed  <전차연출_합본.mp4> 30 1920 1080
#   blender -b --python to_mp4.py -- C:/sk_assets/capture/compare/out_wheels <전차연출_합본_바퀴확대.mp4> 30 1920 1080
import glob, os
from PIL import Image, ImageDraw, ImageFont

SRC = 'C:/sk_assets/capture/compare/mixed/'
OUT = 'C:/sk_assets/capture/compare/out_mixed/'
OUT_WHEELS = 'C:/sk_assets/capture/compare/out_wheels/'
FOLDER = 'C:/Users/나민재/Documents/poject/Fiveriver/전차연출_비교/'
FPS = 30
W, H = 1920, 1080
PW, PH = 1600, 900
for folder in (OUT, OUT_WHEELS, FOLDER):
    os.makedirs(folder, exist_ok=True)
font = lambda s, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', s)
F_TITLE, F_SHOT, F_SMALL, F_TIME = font(30, True), font(25, True), font(19), font(22)
COLOR = (120, 200, 130)

# shot starts as played (TankEntranceMixedTake: the trench shots follow the moment the tank comes into view)
SHOTS = [(0.0, '① 길가 크레인 — 풀 높이에서 올라가며 맞이함 (Blender판)'), (3.4, '② 길가 낮은 자리 — 바퀴와 궤도가 지나감 (Fable판)'),
         (6.2, '③ 이동 촬영 — 전차가 따라와 지나침 (Blender판)'), (12.0, '④ 참호 — 모래주머니 뒤에서 올라오며 망원 "전차다!" (두 판)'),
         (15.5, '⑤ 포탑 뒤 — 포탑이 참호로, 발포 (Blender판)'), (17.35, '⑥ 참호 — 착탄 후 내 시점으로 (두 판)')]
EVENTS = [(0.4, '조명탄'), (14.6, '대사'), (17.2, '발포')]
WHEEL_CLIPS = [(3.5, 6.1, (0.22, 0.38, 0.72, 0.88), '② 길가 낮은 자리 — 화면 가운데를 2배로 확대'),
               (7.6, 10.4, (0.14, 0.42, 0.64, 0.92), '③ 이동 촬영 — 화면 가운데를 2배로 확대')]

frames = sorted(glob.glob(SRC + 'f_*.png'))
length = (len(frames) - 1) / FPS


def shot_at(t):
    return [s for s in SHOTS if s[0] <= t][-1]


def draw_frame(i):
    t = i / FPS
    img = Image.new('RGB', (W, H), (14, 15, 18))
    d = ImageDraw.Draw(img)
    x, y = (W - PW) // 2, 56
    d.text((x, 12), '전차 등장 연출 합본 — Fable판 · Blender판의 좋은 컷 + 카메라 흔들림 + 굴러가는 바퀴 · 궤도', font=F_TITLE, fill=COLOR)
    img.paste(Image.open(frames[i]).convert('RGB').resize((PW, PH), Image.LANCZOS), (x, y))
    d.text((x, y + PH + 10), shot_at(t)[1], font=F_SHOT, fill=(240, 240, 240))
    bx0, bx1, by = x, x + PW, y + PH + 62
    d.line((bx0, by, bx1, by), fill=(90, 90, 90), width=6)
    d.line((bx0, by, bx0 + (bx1 - bx0) * t / length, by), fill=COLOR, width=6)
    for s, _ in SHOTS:
        sx = bx0 + (bx1 - bx0) * s / length
        d.line((sx, by - 12, sx, by + 12), fill=(220, 220, 220), width=2)
    for e, label in EVENTS:
        ex = bx0 + (bx1 - bx0) * e / length
        d.ellipse((ex - 6, by + 16, ex + 6, by + 28), fill=(200, 80, 70))
        d.text((ex + 12, by + 22), label, font=F_SMALL, fill=(200, 200, 200), anchor='lm')
    d.text((bx1, y + PH + 24), '%.1f초 / %.1f초' % (t, length), font=F_TIME, fill=(200, 200, 200), anchor='rm')
    return img


for i in range(len(frames)):
    draw_frame(i).save(OUT + 'f_%04d.jpg' % i, quality=90)

# wheel close-up: each passage at real speed, then at half speed
n = 0
tiles = []
for start, stop, box, label in WHEEL_CLIPS:
    picked = range(round(start * FPS), min(round(stop * FPS), len(frames)))
    for speed, name in ((1, '실제 속도'), (2, '절반 속도')):
        for k, i in enumerate(picked):
            src = Image.open(frames[i]).convert('RGB')
            crop = src.crop((int(box[0] * src.width), int(box[1] * src.height), int(box[2] * src.width), int(box[3] * src.height))).resize((W, H), Image.LANCZOS)
            d = ImageDraw.Draw(crop)
            d.rectangle((0, 0, W, 54), fill=(14, 15, 18))
            d.text((24, 10), '%s · %s · %.2f초' % (label, name, i / FPS), font=F_SHOT, fill=COLOR)
            for _ in range(speed):
                crop.save(OUT_WHEELS + 'f_%04d.jpg' % n, quality=90)
                n += 1
            if speed == 1 and k % 3 == 0 and k < 36:
                tiles.append(crop.resize((640, 360), Image.LANCZOS))
sheet = Image.new('RGB', (4 * 640, ((len(tiles) + 3) // 4) * 360), (14, 15, 18))
for k, tile in enumerate(tiles):
    sheet.paste(tile, ((k % 4) * 640, (k // 4) * 360))
sheet.save(FOLDER + '전차연출_합본_바퀴확대.jpg', quality=88)

# still sheet: one tile every 0.8 s
times = [x * 0.8 for x in range(int(length / 0.8) + 1)]
TW, TH, COLS = 600, 338, 4
rows = (len(times) + COLS - 1) // COLS
sheet = Image.new('RGB', (COLS * (TW + 16) + 16, 70 + rows * (TH + 62)), (14, 15, 18))
d = ImageDraw.Draw(sheet)
d.text((16, 18), '전차 등장 연출 합본 — %.1f초, 0.8초 간격' % length, font=F_TITLE, fill=COLOR)
for k, t in enumerate(times):
    x, y = 16 + (k % COLS) * (TW + 16), 70 + (k // COLS) * (TH + 62)
    sheet.paste(Image.open(frames[min(round(t * FPS), len(frames) - 1)]).convert('RGB').resize((TW, TH), Image.LANCZOS), (x, y))
    d.text((x, y + TH + 4), '%.1f초  %s' % (t, shot_at(t)[1].split(' (')[0]), font=F_SMALL, fill=(210, 210, 210))
sheet.save(FOLDER + '전차연출_합본_컷.jpg', quality=88)
print('frames', len(frames), 'length', length, 'wheel frames', n)
