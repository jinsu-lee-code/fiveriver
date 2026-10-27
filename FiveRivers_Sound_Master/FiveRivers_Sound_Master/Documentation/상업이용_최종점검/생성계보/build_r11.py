from pathlib import Path
import json,hashlib
import numpy as np,soundfile as sf
from scipy.ndimage import gaussian_filter1d
from scipy.signal import resample_poly
R=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027');O=R/'T34_반복보정_R11'
for d in ['Audio','Loop_10x']:(O/d).mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
protected={str(p):sha(p) for base in [R/'사용자합격_최종본',R/'추가보정_R8',R/'실물비교_메모반영_R7',R/'T34_저음보정_R10'] for p in base.rglob('*') if p.is_file()}
def metrics(x):
 v=np.array([np.sqrt(np.mean(x[i:i+12000]**2)) for i in range(0,len(x)-12000+1,12000)])
 return {'rms_db':float(20*np.log10(np.sqrt(np.mean(x*x)))),'quarter_second_range_db':float(20*np.log10(v.max()/v.min())),'boundary_jump':float(abs(x[-1]-x[0])),'internal_step_p95':float(np.percentile(abs(np.diff(x)),95)),'true_peak_db':float(20*np.log10(abs(resample_poly(x,4,1)).max()))}
rows=[]
for k in ['A','B']:
 src=R/'T34_저음보정_R10/Audio'/f'R10_T34_Engine_{k}.wav';x,sr=sf.read(src)
 # Periodic energy envelope; gently reduce slow variation, without changing speed or pitch.
 size=240;power=np.mean(x.reshape(-1,size)**2,axis=1);env=np.sqrt(gaussian_filter1d(power,20,mode='wrap'))
 db=np.clip(.5*20*np.log10(np.sqrt(np.mean(x*x))/np.maximum(env,1e-9)),-.5,.5)
 centers=(np.arange(len(db))+.5)*size
 gain_db=np.interp(np.arange(len(x)),np.r_[centers[-1]-len(x),centers,centers[0]+len(x)],np.r_[db[-1],db,db[0]])
 y=x*10**(gain_db/20);y*=np.sqrt(np.mean(x*x)/np.mean(y*y))
 name=f'R11_T34_Engine_{k}.wav';sf.write(O/'Audio'/name,y,sr,subtype='PCM_24');z,_=sf.read(O/'Audio'/name);sf.write(O/'Loop_10x'/name,np.tile(z,10),sr,subtype='PCM_24')
 rows.append({'variant':k,'file':name,'before':metrics(x),'after':metrics(z),'gain_limit_db':.5,'sha256':sha(O/'Audio'/name)})
changed=[p for p,h in protected.items() if sha(Path(p))!=h];assert not changed
(O/'반복보정검사.json').write_text(json.dumps({'rows':rows,'protected_count':len(protected),'changed':changed,'method':'Circular smoothed envelope, half-strength leveling capped at +/-0.5dB, RMS restored. No seam crossfade or time/pitch changes. Ten repeats are 67.2 seconds. Short-loop motif repetition remains.'},indent=2),encoding='utf-8')
comparisons=json.loads((O/'AB_분석.json').read_text(encoding='utf-8'))
tr=''.join(f'<tr><td>{a["video"]} {a["start"]}–{a["end"]}초</td><td>{a["A"]["clap_cosine"]:.3f}</td><td>{a["B"]["clap_cosine"]:.3f}</td><td>{a["A"]["spectral_shape_error_db"]:.2f} / {a["B"]["spectral_shape_error_db"]:.2f}</td></tr>' for a in comparisons)
cards=[]
for row in rows:
 k=row['variant'];cards.append(f'''<article><h2>{k} · 반복 음량 보정</h2><p>250ms 구간 음량 범위 {row['before']['quarter_second_range_db']:.2f} → {row['after']['quarter_second_range_db']:.2f}dB. 음높이·속도·6.72초 길이 유지.</p><div class="pair"><section><h3>수정 전 R10 {k}</h3><audio controls preload="metadata" src="../T34_저음보정_R10/Audio/R10_T34_Engine_{k}.wav"></audio><details><summary>수정 전 10회 반복</summary><audio controls preload="metadata" src="../T34_저음보정_R10/Loop_10x/R10_T34_Engine_{k}.wav"></audio></details></section><section><h3>수정 후 R11 {k}</h3><audio controls preload="metadata" src="Audio/{row['file']}"></audio><details><summary>수정 후 10회 반복</summary><audio controls preload="metadata" src="Loop_10x/{row['file']}"></audio></details><a href="Audio/{row['file']}" download>수정 후보 WAV</a></section></div></article>''')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>T-34 A/B 재분석 · R11 반복 보정</title><style>body{font:17px/1.7 system-ui;background:#132830;color:#edf4f4;max-width:1200px;margin:auto;padding:24px}article,.note{background:#23424d;padding:20px;border-radius:12px;margin:20px 0}.pair{display:grid;grid-template-columns:1fr 1fr;gap:24px}audio,iframe{width:100%}iframe{height:280px;border:0}a{color:#a4e9cf}td,th{padding:10px;border-bottom:1px solid #5b747c}table{width:100%;border-collapse:collapse}details{margin:12px 0}@media(max-width:800px){.pair{grid-template-columns:1fr}}</style><h1>T-34 A/B 재분석 · 반복 보정 R11</h1><p class="note">실제 오디오를 로컬 CLAP에 입력하고 주파수를 측정했습니다. 사람의 직접 청취 판정은 아닙니다. A/B 각각 6.72초와 실물의 동일 길이 구간을 비교했고, 모델 입력의 RMS를 맞췄습니다.</p><h2>어느 쪽이 가까운가?</h2><p>지정 실사격 영상에서는 <b>A가 오디오 특징 점수에서 우세</b>합니다. 다만 <b>B는 네 구간 모두 주파수 분포 오차가 작고</b>, 다른 T-34 영상에서는 특징 점수도 높았습니다. 따라서 A를 먼저 비교 청취하되, 실제 음색이 더 같다고 확정할 수 없습니다. 이 비교 구간들은 서로 다른 운전 상태와 녹음 조건을 포함합니다.</p><table><tr><th>실물 구간</th><th>A 특징 점수 ↑</th><th>B 특징 점수 ↑</th><th>주파수 오차 A/B dB ↓</th></tr>'''+tr+'''</table><p>점수는 유사도 백분율이 아니며, 작은 점수 차이에 통계적 유의성이 확인된 것은 아닙니다. 분석표는 R10 A/B 결과입니다.</p><h2>반복 보정</h2><p>경계 변화는 내부 샘플 변화의 95백분위 값보다 작아 큰 경계 수술은 하지 않았습니다. 순환 방식의 부드러운 음량 보정을 ±0.5dB 범위로 제한하고 평균 음량을 다시 맞췄습니다. 6.72초의 같은 패턴이 반복되는 점은 남아 있습니다. 10회 파일은 측정·청취용이며 직접 10회 청취했다는 뜻이 아닙니다.</p>'''+''.join(cards)+'''<h2>실물과 다시 들어보기</h2><div class="pair"><iframe title="지정 영상 45초" src="https://www.youtube.com/embed/0v5zdE21gXU?start=45&end=52&enablejsapi=1" referrerpolicy="strict-origin-when-cross-origin" allow="encrypted-media; fullscreen"></iframe><iframe title="다른 T34 영상 155초" src="https://www.youtube.com/embed/nQM4xqCdgSw?start=155&end=162&enablejsapi=1" referrerpolicy="strict-origin-when-cross-origin" allow="encrypted-media; fullscreen"></iframe></div><p>실물 음원 혼합 없음. 합격 음원·게임 연결 변경 없음. 기존 파일 '''+str(len(protected))+'''개 해시 확인: 변경 0개.</p><p><a href="AB_분석.json">A/B 분석 원자료</a> · <a href="반복보정검사.json">반복·보존 검사</a></p><script>document.querySelectorAll('audio').forEach(a=>a.onplay=()=>{document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()});document.querySelectorAll('iframe').forEach(f=>f.contentWindow.postMessage(JSON.stringify({event:'command',func:'pauseVideo',args:[]}),'https://www.youtube.com'))});</script></html>'''
(O/'AB재분석_반복청취.html').write_text(page,encoding='utf-8');print(json.dumps(rows,indent=2));print('PRESERVED',len(protected),'CHANGED',changed)
