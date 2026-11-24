# 크기가 다른 이미지(옆 · 위 직교 / 사수 눈 원근)를 한 장으로: python loader_sheet.py out.jpg cols label1=path1 ...
import sys
from PIL import Image, ImageDraw

out, cols = sys.argv[1], int(sys.argv[2])
items = [a.split("=", 1) for a in sys.argv[3:]]
ims = [(l, Image.open(p).convert("RGB") if p != "-" else None) for l, p in items]
w = max(im.size[0] for l, im in ims if im is not None)
h = max(im.size[1] for l, im in ims if im is not None)
rows = (len(ims) + cols - 1) // cols
sheet = Image.new("RGB", (cols * w, rows * h), (20, 20, 20))
d = ImageDraw.Draw(sheet)
for i, (l, im) in enumerate(ims):
    x, y = (i % cols) * w, (i // cols) * h
    if im is not None:
        sheet.paste(im, (x + (w - im.size[0]) // 2, y + (h - im.size[1]) // 2))
    if l:
        d.rectangle((x, y, x + 7 * len(l) + 6, y + 14), fill=(0, 0, 0))
        d.text((x + 3, y + 1), l, fill=(255, 235, 60))
sheet.save(out, quality=88)
print(out, sheet.size)
