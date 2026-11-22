# The game's own skinned hands against the rifle's distance field.
import struct, collections
import numpy as np
from grip_model import D, Hand, Field

hs = {s: Hand(s) for s in ('Left', 'Right')}
field = Field()
keep = {s: h.vids[h.in_hand > 0.6] for s, h in hs.items()}
b = open(D + 'holds.bin', 'rb').read(); o = 0
rows = collections.defaultdict(list)
while o < len(b):
    name = b[o:o + 48].decode('ascii').strip(); o += 48
    sh, kn, br, bl, sr, sl = struct.unpack_from('<6f', b, o); o += 24
    n = struct.unpack_from('<i', b, o)[0]; o += 4
    P = np.frombuffer(b, '<f4', n * 3, o).reshape(n, 3).astype(float); o += 12 * n
    steady = '~' not in name and (sh > 0.99 or sh < 0.01)
    r = [br, bl, sr * 1000, sl * 1000]
    for s in ('Right', 'Left'):
        r.append(max(0.0, -field(P[keep[s]]).min()) * 1000)
    rows[(name.rstrip('~') if steady else 'between poses', 'shouldered' if sh > 0.99 else 'carried' if sh < 0.01 else '')
         if steady else ('between poses', '')].append(r)
print('%-40s %5s | wrist R / L deg | short of hold R / L mm | in rifle R med/max, L med/max mm' % ('pose', 'n'))
for k, v in sorted(rows.items()):
    a = np.array(v); med = np.median(a, 0); mx = a.max(0)
    print('%-40s %5d | %3.0f / %3.0f | %5.1f / %5.1f | %4.1f/%4.1f  %4.1f/%4.1f'
          % (' '.join(k), len(a), mx[0], mx[1], mx[2], mx[3], med[4], mx[4], med[5], mx[5]))
