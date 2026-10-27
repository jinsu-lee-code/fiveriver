from pathlib import Path
import sys,json,hashlib,shutil,math,html,zipfile,subprocess
sys.path.insert(0,str(Path(__file__).parent/'deps'))
import numpy as np,soundfile as sf
from scipy.signal import butter,sosfiltfilt,resample_poly
root=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027');source=root/'Freesound_상업사용_후보';out=root/'게임용편집_R6'
for d in ['Audio','Before','Previous_R5','Loop_10x','Generated_Source']:(out/d).mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
protected={str(p):sha(p) for d in [root/'사용자합격_최종본',root/'Freesound_상업사용_후보/Originals'] for p in d.rglob('*') if p.is_file()}
meta=json.loads((source/'음원목록.json').read_text(encoding='utf-8'));meta={r['id']:r for r in meta}
SR=48000;rows=[]
def load(sid):return sf.read(source/'Audition'/(sid+'.wav'),always_2d=True)[0]
def fade(x,msin=3,msout=35):
 x=x.copy();a=min(round(msin*48),len(x)//5);b=min(round(msout*48),len(x)//5)
 x[:a]*=np.linspace(0,1,a)[:,None];x[-b:]*=np.linspace(1,0,b)[:,None];return x
def make_loop(x,seconds,start=0,cross=.25):
 n=round(seconds*SR);f=round(cross*SR);s=round(start*SR);x=x[s:s+n+f].copy()
 if len(x)<n+f:raise ValueError('source too short; no hidden repetition')
 w=(.5-.5*np.cos(np.linspace(0,np.pi,f)))[:,None]
 y=np.concatenate([x[f:-f],x[-f:]*(1-w)+x[:f]*w]);y[-1]=y[0];return y
def emit(name,x,sid,desc,loop=False,r5=None):
 peak=float(np.max(abs(resample_poly(x,4,1,axis=0))));gain=min(1,10**(-1.5/20)/max(peak,1e-12));x=x*gain
 p=out/'Audio'/name;sf.write(p,x,SR,subtype='PCM_16');y,_=sf.read(p,always_2d=True)
 if loop:sf.write(out/'Loop_10x'/name,np.tile(y,(10,1)),SR,subtype='PCM_16')
 before=(sid+'.wav') if sid in meta else sid
 if sid in meta:shutil.copy2(source/'Audition'/before,out/'Before'/before)
 else:shutil.copy2(out/'Generated_Source'/sid,out/'Before'/sid)
 if r5:shutil.copy2(root/'추가수정_R5/Audio'/r5,out/'Previous_R5'/r5)
 tp=float(20*np.log10(max(np.max(abs(resample_poly(y,4,1,axis=0))),1e-12)))
 assert np.isfinite(y).all() and tp < -1.4
 rows.append({'file':name,'seconds':len(y)/SR,'source':sid,'before':before,'r5':r5,'loop':loop,'edit':desc+f' 최종 피크 감쇠 {20*np.log10(gain):.2f}dB.','true_peak_est_dbfs':tp,'dc':float(abs(y.mean(axis=0)).max()),'boundary_difference':float(abs(y[-1]-y[0]).max()) if loop else None,'sha256':sha(p),'listening':'직접 청취 미완료 / 사용자 판정 대기'})
for name,a,b in [('Wounded_A',.65,1.95),('Wounded_B',2.4,4.85)]:
 emit(name+'.wav',fade(load('768185')[round(a*SR):round(b*SR)],5,60),'768185',f'신음 구간 {a}–{b}초 추출. 앞 5ms·뒤 60ms 페이드, 속도·피치 유지.','', 'SFX_Wounded.wav')
x=load('317834');x=sosfiltfilt(butter(2,80,fs=SR,btype='highpass',output='sos'),x,axis=0)
emit('Flare_Burn_10s.wav',make_loop(x,10,2,.25),'317834','2초부터 10.25초 분량 사용. 80Hz 저역 제거, 250ms 순환 크로스페이드. 도로 신호탄 기반, 공중 군용 실물 아님.',True,'SFX_Flare_Burn.wav')
for label,a,b in [('A',0,.35),('B',.72,1.15),('C',3.85,4.32)]:
 emit('Ricochet_'+label+'.wav',fade(load('109958')[round(a*SR):round(b*SR)],1,20),'109958',f'{a}–{b}초 짧은 이벤트 후보 추출. 앞 1ms·뒤 20ms 페이드. 탄종·표적 미확인.',False,'SFX_T34_Ricochet.wav')
x=load('95129');x=sosfiltfilt(butter(2,45,fs=SR,btype='highpass',output='sos'),x,axis=0)
for sec in [10,30]:emit(f'Distant_Artillery_{sec}s.wav',make_loop(x,sec,15,.5),'95129',f'15초부터 원래 녹음 {sec+.5}초 사용. 45Hz 저역 제거 및 500ms 순환 연결. 예포 녹음이며 시간 압축·동일 구간 복제 없음.',True,'AMB_Distant_Artillery.wav')
emit('Explosion_Short.wav',fade(load('231765')[:round(3.5*SR)],.5,180),'231765','실질적인 감쇠 뒤의 긴 무음 꼬리 제거, 3.5초 단발, 뒤 180ms 감쇠. 합성 폭발이며 바주카 피탄 실녹음 아님.',False,'SFX_Bazooka_Hit.wav')
for name,r5,isloop in [('R6_T34_Engine.wav','SFX_T34_Engine.wav',True),('R6_GMC_Engine.wav','SFX_Truck_Engine.wav',True),('R6_T34_Cannon.wav','SFX_T34_Cannon.wav',False)]:
 p=out/'Generated_Source'/name
 if not p.exists():continue
 x,sr=sf.read(p,always_2d=True);g=math.gcd(sr,SR);x=resample_poly(x,SR//g,sr//g,axis=0);x=sosfiltfilt(butter(2,20,fs=SR,btype='highpass',output='sos'),x,axis=0)
 if isloop:
  duration=(len(x)-12000)/SR;x=make_loop(x,duration,0,.25);desc='텍스트만으로 VARCO 신규 생성. 실제 장비 녹음 아님. 소스 자연 길이 유지 후 250ms 순환 연결(0.25초 감소). 12초 요청보다 짧으면 억지로 늘리지 않음.'
 else:x=fade(x,.5,80);desc='텍스트만으로 VARCO 신규 생성. 실제 T-34 실사격 녹음 아님. 자연 길이 유지, 양끝 페이드.'
 emit(name,x,name,desc,isloop,r5)
credits=[]
for sid in sorted({r['source'] for r in rows if r['source'] in meta}):
 m=meta[sid];edits='\n'.join(r['file']+': '+r['edit'] for r in rows if r['source']==sid)
 credits.append(f'{m["title"]} by {m["author"]}\n{m["url"]}\n{m["license"]}: {m["license_url"]}\nModifications / 수정 내역:\n{edits}\n')
(out/'CREDITS.txt').write_text('\n'.join(credits)+'\nVARCO 3개: 텍스트 독립 생성. 외부 수집 음원 업로드 없음. 계정 요금제 및 생성물 상업 이용 조건 확인은 별도 필요.\n',encoding='utf-8-sig')
(out/'편집검사결과.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
cards=[]
for r in rows:
 name=r['file'];c=f'<article data-file="{name}"><h2>{name}</h2><p class="tag">{r["seconds"]:.3f}초 · 48kHz PCM16 · '+('루프' if r['loop'] else '단발')+'</p><p>'+html.escape(r['edit'])+'</p><div class="pair">'
 for title,src in [('A 원료 / 생성 원본','Before/'+r['before']),('B 게임용 편집 후보','Audio/'+name),('C 이전 R5 후보','Previous_R5/'+r['r5'])]:c+=f'<section><h3>{title}</h3><audio controls preload="metadata" src="{src}"></audio></section>'
 c+='</div>'
 if r['loop']:c+=f'<details><summary>10회 반복 ({r["seconds"]*10:.2f}초)</summary><audio controls preload="none" src="Loop_10x/{name}"></audio></details>'
 c+='<select><option>청취 미확인</option><option>합격</option><option>추가 수정 필요</option><option>부적합</option></select><textarea placeholder="문제 시점·반복감·수정 요청"></textarea></article>';cards.append(c)
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>R6 · 게임용 길이와 루프 비교</title><style>body{max-width:1350px;margin:24px auto;padding:20px;background:#12222b;color:#e7f0f4;font:16px/1.6 system-ui}article{background:#213a47;border:1px solid #426274;border-radius:14px;padding:24px;margin:24px 0}h2{font-size:21px;overflow-wrap:anywhere}h3{font-size:16px}.pair{display:grid;grid-template-columns:repeat(3,1fr);gap:18px}audio{width:100%}a,.tag{color:#93e3c3}.notice{padding:18px;background:#32404a;color:#f3d1a2}textarea{box-sizing:border-box;width:100%;height:70px;margin-top:12px;font:inherit;padding:10px}select,button{font:inherit;padding:10px}details{margin:15px 0}@media(max-width:950px){.pair{grid-template-columns:1fr}}</style><h1>게임용 편집 R6 · 원료 / 편집본 / 이전 후보</h1><p>합격한 11개·기존 게임 소리·연결은 보존했습니다. 여기 있는 음원은 새 비교 후보이며 게임에 적용하지 않았습니다.</p><div class="notice">길이·페이드·반복 경계는 수치 검사했습니다. 제가 직접 듣고 자연스러움을 판정한 결과는 아닙니다. 실녹음의 장비 차이와 합성음 여부를 각 항목에 표시했습니다. 특정 모델의 실물 음색과 같다는 보장은 없습니다.<br>모신/마우저 혼합 녹음과 차종 불명 전차 엔진은 정확한 대체음으로 편집하지 않고 기존 수집실에 참고용으로 보존했습니다.</div><p><a href="CREDITS.txt">출처·수정 크레딧</a> · <a href="편집검사결과.json">길이·반복 경계 검사</a></p><button id="save">판정·메모 저장</button>'''+''.join(cards)+'''<script>const cs=[...document.querySelectorAll('article')];let old={};try{old=JSON.parse(localStorage.getItem('fiveRiversR6')||'{}')}catch{}function values(){return Object.fromEntries(cs.map(c=>[c.dataset.file,{choice:c.querySelector('select').value,note:c.querySelector('textarea').value}]))}cs.forEach(c=>{const v=old[c.dataset.file];if(v){c.querySelector('select').value=v.choice;c.querySelector('textarea').value=v.note}c.oninput=()=>localStorage.setItem('fiveRiversR6',JSON.stringify(values()))});document.querySelectorAll('audio').forEach(a=>a.onplay=()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()}));document.querySelector('#save').onclick=()=>{const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(values(),null,2)],{type:'application/json'}));a.download='R6_판정메모.json';a.click()};</script></html>'''
(out/'편집비교청취.html').write_text(page,encoding='utf-8')
assert all(sha(Path(p))==h for p,h in protected.items())
src=Path(r'C:\Users\mbc\Documents\user\blender-ai-game-assets-guide\blender-ai-game-assets-guide');m=json.loads(Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_Transfer_20261027\Source_Hashes.json').read_text(encoding='utf-8'));assert all(sha(src/r['path'])==r['sha256'] for r in m)
report={'outputs':len(rows),'loops':sum(r['loop']==True for r in rows),'protected_unchanged':len(protected),'original_manifest_unchanged':len(m),'git_status':subprocess.run(['git','-C',str(src.parent),'status','--porcelain=v1'],capture_output=True,text=True).stdout}
(out/'보존확인.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
zpath=Path(r'C:\Users\mbc\Desktop\FiveRivers_게임용편집_R6_비교후보.zip')
with zipfile.ZipFile(zpath,'w',zipfile.ZIP_DEFLATED) as z:
 for p in out.rglob('*'):
  if p.is_file():z.write(p,p.relative_to(out))
with zipfile.ZipFile(zpath) as z:assert z.testzip() is None
print(json.dumps(report,ensure_ascii=False))
