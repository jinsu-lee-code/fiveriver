# Side-by-side comparison of the two tank entrance takes (15 fps captures) -> frames for a video + one still sheet.
import glob, os
from PIL import Image, ImageDraw, ImageFont

SRC = 'C:/sk_assets/capture/compare/'
OUT = 'C:/sk_assets/capture/compare/out/'
SHEET = 'C:/Users/나민재/Documents/poject/Fiveriver/전차연출_비교/전차연출_컷비교.jpg'
os.makedirs(OUT, exist_ok=True)
os.makedirs(os.path.dirname(SHEET), exist_ok=True)
FPS = 15
W, H = 1920, 1080
PW, PH = 904, 508
font = lambda s, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', s)
F_TITLE, F_SHOT, F_SMALL, F_TIME = font(34, True), font(25, True), font(19), font(22)

takes = {
    'fable': {
        'name': 'Fable판 — 고정 카메라 8컷 (코드로 계산)',
        'color': (230, 170, 60),
        'shots': [(0.0, '① 길 위 망원 — 조명탄 아래 다가옴'), (3.2, '② 길가 낮은 자리 — 궤도가 지나감'),
                  (6.6, '③ 전차 앞 — 포탑이 좌우를 살핌'), (9.2, '④ 전차 뒤 — 능선 쪽으로 따라감'),
                  (12.1, '⑤ 참호 망원 — 능선을 돌아 나옴 "전차다!"'), (15.7, '⑥ 전차 앞 — 포탑이 참호로, 발포'),
                  (17.45, '⑦ 참호 — 참호 앞 착탄'), (18.95, '⑧ 내 시점으로 복귀')],
        'events': [(0.4, '조명탄'), (14.6, '대사'), (17.3, '발포')],
    },
    'blender': {
        'name': 'Blender판 — 움직이는 카메라 5컷 (Blender 키프레임)',
        'color': (90, 170, 230),
        'shots': [(0.0, 'A 크레인 — 풀 높이에서 올라가며 맞이함'), (5.5, 'B 이동 촬영 — 전차가 따라와 지나침'),
                  (11.5, 'C 참호 — 모래주머니 뒤에서 올라옴 "전차다!"'), (15.8, 'D 포탑 뒤 — 포탑이 참호로, 발포'),
                  (17.65, 'E 참호 — 착탄 후 내 시점으로')],
        'events': [(0.5, '조명탄'), (14.1, '대사'), (17.5, '발포')],
    },
}
for key, take in takes.items():
    take['frames'] = sorted(glob.glob(SRC + key + '/f_*.png'))
    take['length'] = (len(take['frames']) - 1) / FPS
n = max(len(t['frames']) for t in takes.values())
span = max(t['length'] for t in takes.values())


def shot_at(take, t):
    return [s for s in take['shots'] if s[0] <= t][-1]


def panel(take, i):
    path = take['frames'][min(i, len(take['frames']) - 1)]
    return Image.open(path).convert('RGB').resize((PW, PH), Image.LANCZOS)


def draw_frame(i):
    t = i / FPS
    img = Image.new('RGB', (W, H), (14, 15, 18))
    d = ImageDraw.Draw(img)
    d.text((W // 2, 46), '전차 등장 연출 비교 — 같은 장면, 같은 시간', font=F_TITLE, fill=(235, 235, 235), anchor='mm')
    d.text((W // 2, 88), '실제 속도 · 왼쪽 Fable판 / 오른쪽 Blender판 · 아래 막대의 눈금이 컷이 바뀌는 곳', font=F_SMALL, fill=(150, 150, 150), anchor='mm')
    for col, key in enumerate(('fable', 'blender')):
        take = takes[key]
        x = 32 + col * (PW + 48)
        y = 200
        d.rectangle((x - 3, y - 3, x + PW + 2, y + PH + 2), outline=take['color'], width=3)
        img.paste(panel(take, i), (x, y))
        d.text((x, y - 36), take['name'], font=F_SHOT, fill=take['color'])
        ended = t > take['length'] + 0.01
        shot = '끝 — 플레이어에게 조작이 넘어감' if ended else shot_at(take, t)[1]
        d.text((x, y + PH + 18), shot, font=F_SHOT, fill=(240, 240, 240))
        # timeline: cuts as ticks, events as dots, playhead
        bx0, bx1, by = x, x + PW, y + PH + 86
        d.line((bx0, by, bx0 + (bx1 - bx0) * take['length'] / span, by), fill=(90, 90, 90), width=6)
        d.line((bx0, by, bx0 + (bx1 - bx0) * min(t, take['length']) / span, by), fill=take['color'], width=6)
        for s, _ in take['shots']:
            sx = bx0 + (bx1 - bx0) * s / span
            d.line((sx, by - 13, sx, by + 13), fill=(220, 220, 220), width=2)
        for e, label in take['events']:
            ex = bx0 + (bx1 - bx0) * e / span
            d.ellipse((ex - 6, by + 22, ex + 6, by + 34), fill=(200, 80, 70))
            d.text((ex, by + 50), label, font=F_SMALL, fill=(200, 200, 200), anchor='mm')
        d.text((bx1, by - 30), '%.1f초 / %.1f초' % (min(t, take['length']), take['length']), font=F_TIME, fill=(200, 200, 200), anchor='rm')
    return img


for i in range(n):
    draw_frame(i).save(OUT + 'f_%04d.jpg' % i, quality=90)

# still: one row every 1.5 s, both takes side by side
times = [x * 1.5 for x in range(int(span / 1.5) + 1)]
TW, TH = 640, 360
sheet = Image.new('RGB', (130 + 2 * TW + 30, 70 + len(times) * (TH + 44)), (14, 15, 18))
d = ImageDraw.Draw(sheet)
for col, key in enumerate(('fable', 'blender')):
    d.text((130 + col * (TW + 30) + TW // 2, 36), takes[key]['name'].split(' — ')[0], font=F_TITLE, fill=takes[key]['color'], anchor='mm')
for row, t in enumerate(times):
    y = 70 + row * (TH + 44)
    d.text((65, y + TH // 2), '%.1f초' % t, font=F_SHOT, fill=(220, 220, 220), anchor='mm')
    for col, key in enumerate(('fable', 'blender')):
        take = takes[key]
        x = 130 + col * (TW + 30)
        i = min(round(t * FPS), len(take['frames']) - 1)
        sheet.paste(Image.open(take['frames'][i]).convert('RGB').resize((TW, TH), Image.LANCZOS), (x, y))
        d.text((x, y + TH + 8), shot_at(take, min(t, take['length']))[1], font=F_SMALL, fill=(200, 200, 200))
sheet.save(SHEET, quality=88)
print('frames', n, 'span', span, 'sheet rows', len(times))
