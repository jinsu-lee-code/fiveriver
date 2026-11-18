# 여러 이미지를 한 장으로 붙인다: python sheet.py out.jpg cols label1=path1 label2=path2 ...
import sys, io
from PIL import Image, ImageDraw
out, cols = sys.argv[1], int(sys.argv[2])
items = [a.split("=", 1) for a in sys.argv[3:]]
first = next(Image.open(p) for l, p in items if p != "-")
w, h = first.size
ims = [(l, Image.open(p).convert("RGB") if p != "-" else Image.new("RGB", (w, h), (20, 20, 20))) for l, p in items]
rows = (len(ims) + cols - 1) // cols
sheet = Image.new("RGB", (cols * w, rows * h), (20, 20, 20))
d = ImageDraw.Draw(sheet)
for i, (l, im) in enumerate(ims):
    x, y = (i % cols) * w, (i // cols) * h
    sheet.paste(im, (x, y))
    d.rectangle((x, y, x + 8 * len(l) + 6, y + 14), fill=(0, 0, 0))
    d.text((x + 3, y + 1), l, fill=(255, 235, 60))
sheet.save(out, quality=88)
print(out, sheet.size)
