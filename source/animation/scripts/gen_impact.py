import sys
sites = [tuple(map(float, p.split(','))) for p in open('C:/sk_anim/impact_sites.txt').read().strip().split(';')]
arr = ',\n            '.join('new Vector2(%sf, %sf)' % (x, z) for x, z in sites)
code = open('C:/sk_anim/scripts/ImpactMarksForward.cs.tpl', encoding='utf-8').read().replace('__SITES__', arr)
open('C:/sk/five_river/Assets/_Project/Scripts/Editor/ImpactMarksForward20261107.cs', 'wb').write(code.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))
print('written', len(sites))
