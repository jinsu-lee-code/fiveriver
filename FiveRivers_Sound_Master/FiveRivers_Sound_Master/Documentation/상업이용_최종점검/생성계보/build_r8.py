from pathlib import Path
import sys,json,hashlib,shutil,math,zipfile,subprocess,html
sys.path.insert(0,str(Path(__file__).parent/'deps'))
import numpy as np,soundfile as sf
from scipy.signal import butter,sosfiltfilt,resample_poly,find_peaks
root=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027');out=root/'추가보정_R8';sr=48000
for d in ['Audio','Before','Loop_10x','Originals']:(out/d).mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
protected={str(p):sha(p) for d in [root/'사용자합격_최종본',root/'게임용편집_R6',root/'실물비교_메모반영_R7',root/'추가수정_R5'] for p in d.rglob('*') if p.is_file()}
def read(p):
 x,r=sf.read(p,always_2d=True);g=math.gcd(r,sr);return resample_poly(x,sr//g,r//g,axis=0) if r!=sr else x
def filt(x,f,b='lowpass'):return sosfiltfilt(butter(2,f,fs=sr,btype=b,output='sos'),x,axis=0)
def fade(x,a=.001,b=.06):
 y=x.copy();n=min(round(a*sr),len(y)//4);m=min(round(b*sr),len(y)//4);y[:n]*=np.linspace(0,1,n)[:,None];y[-m:]*=np.linspace(1,0,m)[:,None];return y
def rms(x):return float(np.sqrt(np.mean(x*x)))
def spread(x):
 n=sr//4;r=np.array([rms(x[i:i+n]) for i in range(0,len(x)-n+1,n)]);return float(20*np.log10(r.max()/max(r.min(),1e-10)))
rows=[]
def emit(name,x,before,desc,loop=False):
 x=x.copy();peak=np.max(abs(resample_poly(x,4,1,axis=0)));x*=min(1,10**(-1.5/20)/max(peak,1e-10));sf.write(out/'Audio'/name,x,sr,subtype='PCM_16');y=read(out/'Audio'/name)
 bp=out/'Before'/before.name
 if not bp.exists():shutil.copy2(before,bp)
 if loop:
  sf.write(out/'Loop_10x'/name,np.tile(y,(10,1)),sr,subtype='PCM_16')
  sf.write(out/'Loop_10x'/('Before_'+name),np.tile(read(before),(10,1)),sr,subtype='PCM_16')
 peak=float(20*np.log10(max(np.max(abs(resample_poly(y,4,1,axis=0))),1e-12)));assert peak<-1.4
 rows.append(dict(file=name,before=before.name,seconds=len(y)/sr,edit=desc,loop=loop,peak=peak,rms_range_db=spread(y),before_rms_range_db=spread(read(before)),boundary=float(abs(y[-1]-y[0]).max()) if loop else None,sha256=sha(out/'Audio'/name)))
for model,a,b in [('T34',.4,7.3),('GMC',.3,3.9)]:
 raw=read(root/f'게임용편집_R6/Generated_Source/R6_{model}_Engine.wav');x=filt(raw,20,'highpass')[round(a*sr):round(b*sr)];f=round(.18*sr)
 w=(.5-.5*np.cos(np.linspace(0,np.pi,f)))[:,None];u=x[-f:];v=x[:f];corr=float(np.sum(u*v)/np.sqrt(np.sum(u*u)*np.sum(v*v)));den=np.sqrt((1-w)**2+w*w+2*corr*w*(1-w));blend=(u*(1-w)+v*w)/np.maximum(den,.7)
 y=np.concatenate([x[f:-f],blend]);y[-1]=y[0]
 before=root/f'게임용편집_R6/Audio/R6_{model}_Engine.wav';y*=rms(read(before))/max(rms(y),1e-10)
 emit(f'R8_{model}_Engine.wav',y,before,f'생성 원본 {a}–{b}초만 사용해 끝의 음량 감쇠 제외. 180ms 에너지 보정 순환 연결. 이전 후보와 전체 RMS 일치, 속도·피치 유지. 더 짧은 루프이므로 반복감 감소는 별도 청취 확인 필요. 실물 음색 일치 보정 아님.',True)
src=root/'실물비교_메모반영_R7/Audio/R7_T34_Cannon.wav';x=read(src);low=filt(filt(x,65,'highpass'),430);y=x+.9*low
y*=rms(x[:sr])/max(rms(y[:sr]),1e-10)
emit('R8_T34_Cannon_A.wav',fade(y),src,'메모: 더 무겁게. 65–430Hz 원래 성분 보강, 최초 1초 RMS를 R7과 맞춰 단순 음량 증가 효과를 줄임. 피치 유지. 실물 녹음과 직접 음색 검증한 결과 아님.')
field=read(root/'실물비교_메모반영_R7/Originals/165808__sidohzen__explosion-with-debris-authentic.wav');env=np.max(abs(field),axis=1);on=int(np.flatnonzero(env>.12)[0]);body=field[max(0,on-240):max(0,on-240)+round(1.3*sr)];body=fade(filt(filt(body,65,'highpass'),380),.005,.4)
if body.shape[1]!=y.shape[1]:body=body.mean(axis=1,keepdims=True)
body*=rms(low[:sr])*.9/max(rms(body[:sr]),1e-10);z=y.copy();n=min(len(body),len(z)-round(.04*sr));z[round(.04*sr):round(.04*sr)+n]+=body[:n];z*=rms(x[:sr])/max(rms(z[:sr]),1e-10)
emit('R8_T34_Cannon_B.wav',fade(z),src,'메모: 더 무겁게. A에 CC0 실제 폐기 폭발의 65–380Hz 짧은 충격층을 낮게 혼합, 최초 1초 RMS 일치. 디자인 합성 후보이며 T-34 주포 실녹음 아님. 원료의 포화 왜곡은 복원되지 않음.')
start=max(0,on-round(.008*sr));e=field[start:start+round(2.8*sr)];emit('R8_Explosion_Tight.wav',fade(e,.0005,.35),root/'실물비교_메모반영_R7/Audio/R7_Explosion_FieldRecording.wav','수정 필요 판정에 상세 메모 없음: 폭발 전 대기 구간 제거, 충격 직전 8ms부터 2.8초 유지, 350ms 꼬리 페이드. 피치 유지. 기존 녹음 포화는 복구하지 않았음.')
src=Path(r'C:\Users\mbc\Downloads\30932__aust_paul__bullet-ricochet.wav');shutil.copy2(src,out/'Originals'/src.name);r=read(src)
emit('R8_Ricochet_Alternatives.wav',fade(r,.001,.02),root/'실물비교_메모반영_R7/Audio/R7_Armor_Ricochet.wav','더 찾아봐: aust_paul의 CC0 모사 도탄 4종 연속 원본을 새 비교 후보로 추가. 속도·피치 유지, 48kHz 및 양끝 페이드만 적용. 실물 전차 장갑 녹음 아님. 선택 전에는 단발 게임 에셋으로 쓰지 말 것.')
(out/'검사결과.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
license='''R8 편집 출처 / 2026-10-27
R8_T34_Engine / GMC_Engine / Cannon_A: 기존 R6·R7 VARCO 생성물 파생. 현재 Sound Creator 상업 이용 허용 증빙은 R7 폴더에 보존. R6 생성 당시 계정 플랜 이력 자체는 별도 미확보.
Cannon_B: 위 생성물 + sidohzen의 CC0 실녹음 일부 혼합.
Explosion_Tight: Explosion with debris - authentic. 4kg TNT by sidohzen, CC0 1.0.
https://freesound.org/people/sidohzen/sounds/165808/
Ricochet_Alternatives: bullet ricochet.wav by aust_paul, CC0 1.0.
https://freesound.org/people/aust_paul/sounds/30932/
https://creativecommons.org/publicdomain/zero/1.0/
CC0 음원은 상업 이용 허용. 변경내역은 검사결과.json 참조.
실물 영상 음원은 추출/다운로드/합성 업로드하지 않음. 영상 상업 재사용 권리 미확보; 비교 재생 전용.
직접 음색을 듣고 판정하지 못함. 실물 유사도 수치·일치 판정 없음.
'''
(out/'출처와_한계.txt').write_text(license,encoding='utf-8-sig')
def audio(s):return '<audio controls preload="metadata" src="'+s+'"></audio>'
cards=[]
for r in rows:
 name=r['file'];vid='nQM4xqCdgSw' if 'T34_Engine' in name else 'ZsqW-lkQiFk' if 'GMC_Engine' in name else '0v5zdE21gXU' if 'Cannon' in name else None
 ref=f'<iframe title="실물 참고 영상" src="https://www.youtube.com/embed/{vid}?enablejsapi=1" allow="autoplay; encrypted-media; fullscreen" allowfullscreen referrerpolicy="strict-origin-when-cross-origin"></iframe><p><a href="https://www.youtube.com/watch?v={vid}" target="_blank">영상 원문</a></p>' if vid else '<p>실물 음색 검증이 아닌 추가 후보입니다.</p>'
 cards.append(f'<article data-key="{name}"><h2>{name} · {r["seconds"]:.3f}초</h2><div class="pair"><section>{ref}<h3>수정 전</h3>{audio("Before/"+r["before"])}</section><section><h3>R8 수정 후보</h3>{audio("Audio/"+name)}<p>{r["edit"]}</p>'+ (f'<p>250ms 구간 음량 범위: {r["before_rms_range_db"]:.2f} → {r["rms_range_db"]:.2f}dB. 범위 감소는 음색 유사성을 뜻하지 않습니다.</p><details><summary>수정 전 10회</summary>{audio("Loop_10x/Before_"+name)}</details><details><summary>수정 후 10회</summary>{audio("Loop_10x/"+name)}</details>' if r['loop'] else '')+'</section></div><select><option>청취 미확인</option><option>합격</option><option>추가 수정 필요</option><option>부적합</option></select><textarea placeholder="문제 시점·음색·반복감 메모"></textarea></article>')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>R8 · 메모 반영 추가 보정</title><style>body{background:#10232d;color:#eef4f5;font:16px/1.6 system-ui;max-width:1380px;margin:auto;padding:24px}article{padding:24px;border:1px solid #49616d;background:#213b48;margin:24px 0;border-radius:14px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:24px}audio,iframe{width:100%}iframe{height:290px;border:0}a{color:#9ce3c7}textarea{box-sizing:border-box;width:100%;height:75px;margin-top:15px;font:inherit}select,button{font:inherit;padding:10px}.note{padding:16px;background:#394755}h2{font-size:22px}details{margin:14px 0}@media(max-width:850px){.pair{grid-template-columns:1fr}}</style><h1>R8 · 실물 참고 / 수정 전 / 수정 후</h1><p class="note">직접 청취 비교는 수행하지 못했습니다. 메모와 파형에 근거한 수정 후보입니다. 엔진은 끝의 감쇠와 순환 연결을 수정했으며, 영상과 음색이 일치한다고 판정한 것은 아닙니다. 합격 11개·R7 합격 소총·기존 게임 파일은 보존했습니다.</p><p><a href="출처와_한계.txt">출처·이용 조건</a> · <a href="사용자메모.json">반영한 메모</a> · <a href="검사결과.json">파일 검사</a> · <a href="../실물비교_메모반영_R7/실물비교청취.html">R7 전체 청취</a></p><button id="save">판정·메모 저장</button>'''+''.join(cards)+'''<script>let cs=[...document.querySelectorAll('article')],old={};try{old=JSON.parse(localStorage.getItem('fiveRiversR8')||'{}')}catch{}function vals(){return Object.fromEntries(cs.map(c=>[c.dataset.key,{choice:c.querySelector('select').value,note:c.querySelector('textarea').value}]))}cs.forEach(c=>{let v=old[c.dataset.key];if(v){c.querySelector('select').value=v.choice;c.querySelector('textarea').value=v.note||''}c.oninput=()=>localStorage.setItem('fiveRiversR8',JSON.stringify(vals()))});document.querySelectorAll('audio').forEach(a=>a.onplay=()=>{document.querySelectorAll('audio').forEach(b=>{if(b!==a)b.pause()});document.querySelectorAll('iframe').forEach(f=>f.contentWindow.postMessage(JSON.stringify({event:'command',func:'pauseVideo',args:[]}),'https://www.youtube.com'))});document.querySelector('#save').onclick=()=>{let a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(vals(),null,2)],{type:'application/json'}));a.download='R8_판정메모.json';a.click()};</script></html>'''
(out/'추가보정청취.html').write_text(page,encoding='utf-8')
assert all(sha(Path(p))==h for p,h in protected.items())
src=Path(r'C:\Users\mbc\Documents\user\blender-ai-game-assets-guide\blender-ai-game-assets-guide');m=json.loads(Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_Transfer_20261027\Source_Hashes.json').read_text(encoding='utf-8'));assert all(sha(src/r['path'])==r['sha256'] for r in m)
report=dict(protected_unchanged=len(protected),game_originals_unchanged=len(m),git_status=subprocess.run(['git','-C',str(src.parent),'status','--porcelain=v1'],capture_output=True,text=True).stdout)
(out/'보존확인.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report));print([(r['file'],round(r['before_rms_range_db'],2),round(r['rms_range_db'],2)) for r in rows[:2]])
with zipfile.ZipFile(r'C:\Users\mbc\Desktop\FiveRivers_R8_추가보정.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in out.rglob('*'):
  if p.is_file():z.write(p,p.relative_to(out))
