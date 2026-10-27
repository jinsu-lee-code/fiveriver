from pathlib import Path
import json,hashlib,html
import numpy as np,soundfile as sf
from scipy.signal import welch,resample_poly
ROOT=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027');OUT=ROOT/'T34_저음보정_R10'
OUT.mkdir(exist_ok=True)
for sub in ['Audio','Loop_10x']:(OUT/sub).mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
old=json.loads((ROOT/'무료로컬분석_R9/보존기준.json').read_text(encoding='utf-8'))
baseline={p:sha(Path(p)) for p in old if Path(p).exists()}
src=ROOT/'추가보정_R8/Audio/R8_T34_Engine.wav';x,sr=sf.read(src,always_2d=True)
def rms(y):return np.sqrt(np.mean(y*y))
def band(y):
 f,p=welch(y.mean(axis=1),sr,nperseg=8192);return float(p[(f>=30)&(f<120)].sum()/p.sum())
def shelf(db):
 # A real, positive FFT response preserves phase and the periodic loop. No startup filter transient.
 f=np.fft.rfftfreq(len(x),1/sr);weight=1/(1+(f/140)**4);response=10**(-db*weight/20)
 y=np.fft.irfft(np.fft.rfft(x,axis=0)*response[:,None],n=len(x),axis=0)
 return y*rms(x)/rms(y)
ref=json.loads((ROOT/'무료로컬분석_R9/분석결과.json').read_text(encoding='utf-8'))
target=float(np.mean([r['metrics']['band_energy_fraction']['30-120'] for r in ref['references'] if r['video']=='0v5zdE21gXU' and r['start'] in [30,45]]))
lo,hi=0.,24.
for _ in range(40):
 mid=(lo+hi)/2
 if band(shelf(mid))>target:lo=mid
 else:hi=mid
db=(lo+hi)/2
ys=[('Before_R8_T34_Engine.wav',x,0),('R10_T34_Engine_A.wav',shelf(6),6),('R10_T34_Engine_B.wav',shelf(db),db)]
peak=max(np.abs(resample_poly(y,4,1,axis=0)).max() for _,y,_ in ys)
gain=min(1,10**(-1.5/20)/peak)
rows=[]
for name,y,cut in ys:
 y=y*gain;sf.write(OUT/'Audio'/name,y,sr,subtype='PCM_24');z,_=sf.read(OUT/'Audio'/name,always_2d=True)
 sf.write(OUT/'Loop_10x'/name,np.tile(z,(10,1)),sr,subtype='PCM_24')
 rows.append({'file':name,'low_shelf_cut_db':cut,'seconds':len(z)/sr,'low_band_percent':100*band(z),'rms_db':float(20*np.log10(rms(z))),'true_peak_db':float(20*np.log10(np.abs(resample_poly(z,4,1,axis=0)).max())),'loop_boundary_jump':float(np.max(np.abs(z[-1]-z[0]))),'sha256':sha(OUT/'Audio'/name)})
changed=[p for p,h in baseline.items() if sha(Path(p))!=h]
assert not changed
assert max(r['rms_db'] for r in rows)-min(r['rms_db'] for r in rows)<.01
report={'source':str(src),'source_sha256':sha(src),'method':'Periodic FFT low shelf, 140Hz fourth-order transition, zero phase; same RMS and common headroom gain for all comparison copies. Original preserved. No pitch/time change or reference audio mixing.','target_reference_low_band_percent':target*100,'common_gain_db':float(20*np.log10(gain)),'results':rows,'protected_count':len(baseline),'changed':changed,'limitations':'RMS matched, not perceptual loudness matched. Reference microphone, distance and engine load differ. Spectral matching is not a realism verdict. No human listening performed.'}
(OUT/'보정검사.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
cards=[]
titles=['수정 전 · R8 비교용 복사본','A · 저음 완만하게 감소','B · 참고 구간의 저음 비중에 맞춘 후보']
for title,r in zip(titles,rows):
 n=r['file'];cards.append(f'<section><h2>{title}</h2><p>30–120Hz 비중 <b>{r["low_band_percent"]:.1f}%</b> · RMS {r["rms_db"]:.2f}dBFS</p><audio controls preload="metadata" src="Audio/{n}"></audio><p><a href="Audio/{n}" download>WAV 받기</a></p><details><summary>10회 반복 듣기 · 67.2초</summary><audio controls preload="metadata" src="Loop_10x/{n}"></audio></details></section>')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>T-34 저음 보정 R10 · 전후 비교</title><style>body{font:17px/1.65 system-ui;background:#14262e;color:#edf5f5;max-width:1250px;margin:auto;padding:24px}.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:18px}section,.note{padding:20px;border-radius:12px;background:#23404b;margin:18px 0}h2{font-size:20px}audio,iframe{width:100%}iframe{height:280px;border:0}a{color:#a3ebd0}.refs{display:grid;grid-template-columns:1fr 1fr;gap:20px}details{margin-top:18px}@media(max-width:850px){.cards,.refs{grid-template-columns:1fr}}</style><h1>T-34 엔진 · 저음 보정 전후 비교</h1><p class="note">기존 R8 엔진의 저음만 줄인 별도 후보입니다. 속도·음높이·길이는 유지했습니다. 세 비교 파일의 RMS를 동일하게 맞췄으며, 청감상 음량까지 같다는 뜻은 아닙니다. 합격 음원·기존 게임 연결은 변경하지 않았습니다.</p><p>A부터 들어보세요. B는 참고 영상의 저음 비중을 목표로 더 강하게 줄였습니다. 마이크·거리·운전 상태가 달라 B가 더 사실적이라고 확정하지 않았습니다.</p><div class="cards">'''+''.join(cards)+'''</div><h2>실물 영상 비교 구간</h2><div class="refs"><section><h3>30–40초 · 저음 비중 5.4%</h3><iframe title="실물 영상 30초" src="https://www.youtube.com/embed/0v5zdE21gXU?start=30&end=40&enablejsapi=1" allow="encrypted-media; fullscreen" referrerpolicy="strict-origin-when-cross-origin"></iframe></section><section><h3>45–55초 · 저음 비중 8.2%</h3><iframe title="실물 영상 45초" src="https://www.youtube.com/embed/0v5zdE21gXU?start=45&end=55&enablejsapi=1" allow="encrypted-media; fullscreen" referrerpolicy="strict-origin-when-cross-origin"></iframe></section></div><p>영상 음원을 섞거나 복제하지 않았습니다. 측정 기반 후보이며 사람의 직접 청취 검수는 하지 않았습니다. 10회 반복 파일은 실제 후보를 10번 이어 붙인 것입니다.</p><p><a href="보정검사.json">수치·원본 보존 검사</a> · <a href="../추가보정_R8/추가보정청취.html">기존 합격 판정·메모 화면</a></p><script>document.querySelectorAll('audio').forEach(a=>a.onplay=()=>{document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()});document.querySelectorAll('iframe').forEach(f=>f.contentWindow.postMessage(JSON.stringify({event:'command',func:'pauseVideo',args:[]}),'https://www.youtube.com'))});</script></html>'''
(OUT/'수정전후청취.html').write_text(page,encoding='utf-8')
print(json.dumps(report,ensure_ascii=True,indent=2))
