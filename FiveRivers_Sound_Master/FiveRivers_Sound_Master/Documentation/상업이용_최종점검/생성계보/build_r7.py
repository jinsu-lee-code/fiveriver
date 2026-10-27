from pathlib import Path
import sys,json,hashlib,shutil,zipfile,math,html,subprocess
sys.path.insert(0,str(Path(__file__).parent/'deps'))
import numpy as np,soundfile as sf
from scipy.signal import resample_poly,butter,sosfiltfilt,fftconvolve
root=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027')
out=root/'실물비교_메모반영_R7';sr=48000
for d in ['Audio','Before','Loop_10x','Originals','Evidence']:(out/d).mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
protected={str(p):sha(p) for d in [root/'사용자합격_최종본',root/'Freesound_상업사용_후보',root/'게임용편집_R6',root/'추가수정_R5'] for p in d.rglob('*') if p.is_file()}
notes=json.loads(Path(r'C:\Users\mbc\Downloads\Freesound_후보판정.json').read_text(encoding='utf-8-sig'))
(out/'사용자메모_원문.json').write_text(json.dumps(notes,ensure_ascii=False,indent=2),encoding='utf-8')
rows=[]
def read(p):
 x,r=sf.read(p,always_2d=True);g=math.gcd(r,sr)
 return resample_poly(x,sr//g,r//g,axis=0) if r!=sr else x
def fade(x,a=.002,b=.06):
 x=x.copy();n=min(round(a*sr),len(x)//4);m=min(round(b*sr),len(x)//4)
 x[:n]*=np.linspace(0,1,n)[:,None];x[-m:]*=np.linspace(1,0,m)[:,None];return x
def put(name,x,description,source,license,loop=False):
 peak=np.max(abs(resample_poly(x,4,1,axis=0)));gain=min(1,10**(-1.5/20)/max(peak,1e-10));x*=gain
 p=out/'Audio'/name;sf.write(p,x,sr,subtype='PCM_16');y=read(p)
 if loop:sf.write(out/'Loop_10x'/name,np.tile(y,(10,1)),sr,subtype='PCM_16')
 tp=float(20*np.log10(max(np.max(abs(resample_poly(y,4,1,axis=0))),1e-10)))
 assert np.isfinite(y).all() and tp<-1.4
 rows.append(dict(file=name,seconds=len(y)/sr,edit=description,source=source,license=license,loop=loop,peak_dbfs=tp,sha256=sha(p),seam=float(abs(y[0]-y[-1]).max()) if loop else None,listening='직접 음색 판정 미완료'))
def copybefore(p,label):
 dest=out/'Before'/label;shutil.copy2(p,dest);return 'Before/'+label
src=root/'Freesound_상업사용_후보/Audition/768185.wav'
put('R7_Wounded_Trim1s.wav',fade(read(src)[sr:],.002,.025),'사용자 메모: 앞 1초 삭제, 남은 전체 구간 유지. 속도·피치 변경 없음. 양끝 2/25ms 페이드.', 'https://freesound.org/people/Valerie-Vivegnis/sounds/768185/','CC BY 4.0: 저작자·출처·라이선스·변경 표시 필요')
copybefore(src,'Wounded_full.wav')
src=root/'추가수정_R5/Audio/SFX_Mosin_Fire.wav';x=read(src);extra=round(2.5*sr)
# Preserve the original transient and add a low-level decaying, filtered reflection tail.
rng=np.random.default_rng(2707);ir=np.zeros(extra+1);idx=np.arange(round(.07*sr),len(ir));ir[idx]=rng.normal(0,1,len(idx))*np.exp(-idx/(sr*.43));ir=sosfiltfilt(butter(2,2100,fs=sr,output='sos'),ir);ir/=max(np.sum(abs(ir)),1e-10)
wet=np.stack([fftconvolve(x[:,c],ir) for c in range(x.shape[1])],axis=1);wet*=.16/max(np.max(abs(wet))/max(np.max(abs(x)),1e-10),1e-10)
y=np.pad(x,((0,extra),(0,0)))+wet
put('R7_Mosin_ExtendedTail.wav',fade(y,.0005,.16),'기존 R5 총성의 속도·피치 유지, 필터링한 감쇠 잔향을 더해 총 길이 +2.5초. 늦은 꼬리는 매우 약함. 실물 잔향 복원이 아닌 연출 후보.', 'VARCO R5 기존 후보에서 파생','VARCO 생성 당시 플랜 확인 필요; 기존 후보 원본 보존')
copybefore(src,'Mosin_R5.wav')
src=Path(r'C:\Users\mbc\Downloads\165808__sidohzen__explosion-with-debris-authentic.wav');shutil.copy2(src,out/'Originals'/src.name)
x=read(src)
put('R7_Explosion_FieldRecording.wav',fade(x[round(1.9*sr):round(7.4*sr)],.001,.2),'실제 지뢰 폐기 폭발 녹음의 1.9–7.4초 추출, 피치·속도 유지. 원본 최대 피크 포화 흔적 있음; 감쇠로 이미 발생한 왜곡은 복원되지 않음. 바주카 명중음 아님.', 'https://freesound.org/people/sidohzen/sounds/165808/','CC0 1.0: 상업 이용 허용')
copybefore(src,'Explosion_field_full.wav');copybefore(root/'게임용편집_R6/Audio/Explosion_Short.wav','Explosion_R6.wav')
for name in ['R7_T34_Cannon.wav','R7_Armor_Ricochet.wav','R7_Flare_Burn.wav']:
 x=read(out/'Generated_Source'/name);loop='Flare' in name
 if loop:
  n=12000;w=(.5-.5*np.cos(np.linspace(0,np.pi,n)))[:,None];x=np.concatenate((x[n:-n],x[-n:]*(1-w)+x[:n]*w));x[-1]=x[0]
 else:x=fade(x,.0005,.06)
 desc={'R7_T34_Cannon.wav':'벽 두드림 같은 둔탁함을 피하고 폭발성 압력 충격·짧은 저중역·야외 감쇠를 요청해 독립 생성. 실제 실사격 녹음 아님. 요청 4초, 실제 원본 2.54초.', 'R7_Armor_Ricochet.wav':'단단한 장갑 타격, 불규칙하고 짧은 튕김·금속 울림을 요청해 독립 생성. 게임 영상은 비교용이며 음원 복제·업로드 없음. 실제 T-34 장갑 실험 녹음 아님.', 'R7_Flare_Burn.wav':'도로 신호탄 원료 폐기 대신 공중 조명탄의 약한 연속 연소음을 텍스트로 새 생성. 250ms 순환 연결. 기록은 사용 맥락만 뒷받침하며 1950년 다부동 음색 고증은 미확정.'}[name]
 put(name,x,desc,'VARCO Sound / 현재 Sound Creator 플랜에서 텍스트 독립 생성','현재 Creator 플랜: 상업적 이용 가능(2026-10-27 계정 화면 확인)',loop)
copybefore(root/'게임용편집_R6/Audio/R6_T34_Cannon.wav','T34_Cannon_R6.wav')
copybefore(root/'추가수정_R5/Audio/SFX_T34_Ricochet.wav','Ricochet_R5.wav')
copybefore(root/'게임용편집_R6/Audio/Flare_Burn_10s.wav','Flare_R6.wav')
for label,r5,r6 in [('T34_Engine','SFX_T34_Engine.wav','R6_T34_Engine.wav'),('GMC_Engine','SFX_Truck_Engine.wav','R6_GMC_Engine.wav')]:
 copybefore(root/'추가수정_R5/Audio'/r5,label+'_R5.wav');copybefore(root/'게임용편집_R6/Audio'/r6,label+'_R6.wav')
 shutil.copy2(root/'게임용편집_R6/Loop_10x'/r6,out/'Loop_10x'/r6)
copybefore(root/'추가수정_R5/Audio/AMB_Distant_Artillery.wav','Distant_Artillery_keep.wav')
(out/'편집검사.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
license_text='''상업 이용 확인 / 2026-10-27
1. 현재 로그인한 VARCO Sound 플랜은 Creator이며 가격표에 상업적 이용 가능으로 명시됨. R7 신규 3개는 이 조건 확인 후 텍스트만으로 생성. R6 3개는 같은 날 같은 계정의 생성 후보로 기록되어 있으나 생성 시점 구독 영수증 자체는 별도 확보하지 않음. Free Sound는 상업적 이용 불가(3D 무료 플랜과 다름).
근거: https://www.varco.ai/home/ko/pricing?service=SOUND
계정 증빙: Sound_Creator_상업이용.png / Sound_상업이용조건.png
이용약관: https://terms.varco.ai/2b90435c2d7580279d4bd9b9dcd573ac
현재 플랜의 허용 안내이며 독점 저작권·제3자 권리 무침해 보증을 뜻하지 않음. 과거 R5 등은 생성 시점 플랜 이력 별도 확인 필요.
2. R7_Wounded_Trim1s.wav: Pain groan-man by Valerie-Vivegnis.
https://freesound.org/people/Valerie-Vivegnis/sounds/768185/
CC BY 4.0 https://creativecommons.org/licenses/by/4.0/
수정: 48kHz 변환본에서 앞 1초 제거, 양끝 2/25ms 페이드, 피치·속도 유지. 상업 게임 사용 가능, 저작자/출처/라이선스/변경 표시 필수.
3. R7_Explosion_FieldRecording.wav: Explosion with debris - authentic. 4kg TNT by sidohzen.
https://freesound.org/people/sidohzen/sounds/165808/
CC0 1.0 https://creativecommons.org/publicdomain/zero/1.0/
수정: 원본 1.9–7.4초 추출, 양끝 페이드와 피크 감쇠. 상업 이용 가능. 캄보디아 지뢰 폐기 실녹음, 바주카/전차 명중 녹음 아님.
4. YouTube 실물/게임 영상은 외부 참고 재생 전용. 재사용·추출·상업 게임 수록 허가를 확보하지 않았으므로 음원을 다운로드하거나 합성에 업로드하지 않음. 이 ZIP에 영상 음원은 없음.
5. Before 폴더는 이전 판정 비교용이다. 모든 파일을 일괄 상업 출시용으로 승인한 것이 아니다. 최종 선택 시 각 원본 라이선스를 따를 것.
'''
(out/'상업이용_출처.txt').write_text(license_text,encoding='utf-8-sig')
history='''조명탄 기록 확인
1950년 8월부터 B-29 조명탄 아래 B-26 야간 임무 기록: 미 공군 역사 자료.
https://media.defense.gov/2025/Apr/01/2003679070/-1/-1/0/AF%20IN%20SEA%20NIGHT%20OPS%20BLUE%20BOOK.PDF
한국전쟁 중 낙하산 조명탄 사용 맥락은 확인되지만 다부동 특정 진지의 기종·발사 수단·연소 음색은 이 기록만으로 확정할 수 없음. 연소음은 창작 후보이며 소리 크기도 게임 거리 조건에서 판단 필요.
장갑 도탄 게임 참고: https://www.youtube.com/watch?v=eDyNZw77TKQ
게임 효과는 실물 검증 자료가 아니며 복제·추출하지 않음.
GMC 엔진 구조 확인: Military Aviation Museum의 GMC 270 6기통 복원 기록
https://www.militaryaviationmuseum.org/cckw-fuel-truck-engine-cckw-fuel-truck-engine-reassembled-and-tested/
실물 영상은 복원/재가동 차량이며 1950년 녹음 아님. T-34 영상은 장기 보관 후 첫 시동, GMC 영상은 복원 전 시동 시도이므로 불안정한 시동음을 정상 공회전과 동일시하지 말 것.
'''
(out/'고증과_비교한계.txt').write_text(history,encoding='utf-8-sig')
def aud(src):return f'<audio controls preload="metadata" src="{src}"></audio>'
def iframe(vid,title):return f'<iframe title="{html.escape(title)}" src="https://www.youtube.com/embed/{vid}?enablejsapi=1&amp;origin=http%3A%2F%2F127.0.0.1%3A8766" allow="autoplay; encrypted-media; fullscreen" allowfullscreen referrerpolicy="strict-origin-when-cross-origin"></iframe><p><a href="https://www.youtube.com/watch?v={vid}" target="_blank" rel="noopener">영상 원문 열기 ↗</a></p>'
def decision(key):return f'<div class="decision" data-key="{key}"><label>판정 <select><option>청취 미확인</option><option>합격</option><option>추가 수정 필요</option><option>부적합</option></select></label> <label>영상 비교 구간 <input placeholder="예: 01:20–01:35"></label><textarea placeholder="음색·반복감·문제 시점 메모"></textarea></div>'
cards=[]
for label,title,vid,desc in [('T34_Engine','T-34/85 엔진','nQM4xqCdgSw','VeteransMuseum · 장기 보관 후 첫 시동. 시동 모터·기침하는 구간을 피하고 안정된 회전 구간과 비교하세요. R5는 기존 합격 표시를 보존했고 재편집하지 않았습니다.'),('GMC_Engine','GMC CCKW 엔진','ZsqW-lkQiFk','BLOWN8CYLINDER · 복원 전 시동 시도 영상. 말소리·시동 모터를 제외하고 가솔린 엔진이 유지되는 구간과 비교하세요. R5는 기존 합격 표시를 보존했습니다.')]:
 r6='R6_'+label+'.wav'
 cards.append(f'<article id="{label}"><h2>{title}</h2><div class="pair"><section><h3>실물 영상</h3>{iframe(vid,title)}<p>{desc}</p></section><section><h3>R6 비교 후보</h3>{aud("Before/"+label+"_R6.wav")}<p>R6 그대로 보존 · T-34 7.976초 / GMC 4.185초. 실제 녹음이 아닌 생성음.</p><h3>이전 R5 후보</h3>{aud("Before/"+label+"_R5.wav")}<details><summary>R6 10회 반복 확인</summary>{aud("Loop_10x/"+r6)}</details><p class="focus">비교 기준: 공회전 맥동 / 금속·팬 소음 / 가속 변화 / 같은 소리가 돌아오는 반복감. 녹음 거리·음량 차이를 먼저 감안하세요. 직접 청취 유사도 판정은 미완료입니다.</p></section></div>{decision(label)}</article>')
rowmap={r['file']:r for r in rows}
for name,title,vid,before in [('R7_T34_Cannon.wav','T-34 주포 · 벽 두드림 느낌 수정','0v5zdE21gXU','T34_Cannon_R6.wav'),('R7_Armor_Ricochet.wav','장갑 도탄 · 새 생성 후보','eDyNZw77TKQ','Ricochet_R5.wav'),('R7_Flare_Burn.wav','조명탄 연소 · 기록 참고 후 새 생성',None,'Flare_R6.wav'),('R7_Wounded_Trim1s.wav','부상 신음 · 앞 1초 삭제',None,'Wounded_full.wav'),('R7_Mosin_ExtendedTail.wav','소총 · 잔향 2.5초 연장',None,'Mosin_R5.wav'),('R7_Explosion_FieldRecording.wav','폭발 · 실제 폐기 폭발 녹음 후보',None,'Explosion_R6.wav')]:
 r=rowmap[name];ref=iframe(vid,title) if vid else '<p>원본 또는 이전 후보</p>'+aud('Before/'+before)
 note='실사격 시연 제목·설명 확인. 정확한 탄종·마이크 거리·후처리 여부 미확인.' if 'Cannon' in name else '게임 플레이 영상. 실물이 아니며 스타일 참고만 합니다. 음원 상업 사용 허가 없음.' if 'Ricochet' in name else ''
 cards.append(f'<article id="{name}"><h2>{title}</h2><div class="pair"><section>{ref}<p>{note}</p>'+('<h3>이전 후보</h3>'+aud('Before/'+before) if vid else '')+f'</section><section><h3>R7 수정 후보 · {r["seconds"]:.3f}초</h3>{aud("Audio/"+name)}<p>{r["edit"]}</p><p class="tag">{r["license"]}</p>'+ (f'<details><summary>10회 반복</summary>{aud("Loop_10x/"+name)}</details>' if r['loop'] else '')+ ('<details><summary>확보한 실녹음 전체 16.752초</summary>'+aud('Before/Explosion_field_full.wav')+'</details>' if 'Explosion' in name else '')+f'</section></div>{decision(name)}</article>')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>R7 · 실물 영상 비교와 메모 반영</title><style>body{background:#101f27;color:#eaf0f2;font:16px/1.6 system-ui;margin:0}main{max-width:1380px;margin:auto;padding:24px}article{padding:24px;background:#203640;border:1px solid #49606b;border-radius:14px;margin:24px 0;scroll-margin-top:80px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:24px}iframe{width:100%;height:310px;border:0;background:#000}audio{width:100%}a,.tag{color:#9fe4c6}.notice,.focus{padding:15px;background:#304650}.toolbar{position:sticky;top:0;background:#101f27ee;padding:12px;z-index:2}button,select,input{padding:9px;font:inherit}textarea{box-sizing:border-box;width:100%;height:75px;margin-top:10px;font:inherit}.decision{margin-top:20px;border-top:1px solid #536771;padding-top:16px}h2{margin-top:0}details{margin:18px 0}@media(max-width:850px){.pair{grid-template-columns:1fr}iframe{height:250px}}</style><main><h1>실물 영상 ↔ 게임 후보 · R7</h1><p>새 편집·생성 6개 + T-34·GMC 기존 후보 비교. 합격한 11개·기존 게임 소리·연결은 보존했습니다.</p><div class="notice">영상은 참고 재생 전용이며 게임 수록용 음원을 추출하지 않았습니다. 음색 유사도를 직접 듣고 판정한 결과는 아닙니다. 왼쪽 영상을 듣고 멈춘 다음 오른쪽 후보를 재생하세요. 음량은 자동 일치하지 않습니다. 영상 임베드가 막히면 원문 링크를 사용하세요.</div><p><a href="상업이용_출처.txt">상업 이용·크레딧</a> · <a href="Sound_상업이용조건.png">Creator 허용 증빙</a> · <a href="고증과_비교한계.txt">고증과 비교 한계</a> · <a href="편집검사.json">파일 검사</a></p><div class="toolbar"><button id="stop">전체 정지</button> <button id="save">판정·메모 저장</button> <a href="#T34_Engine">T-34</a> · <a href="#GMC_Engine">GMC</a></div>'''+''.join(cards)+'''<article><h2>먼 포성 · 메모대로 기존 유지</h2><p>‘기존으로 사용하자’ 요청 반영. 원본 파일 변경 없이 비교용 복사본만 제공.</p>'''+aud('Before/Distant_Artillery_keep.wav')+'''</article></main><script>
const ds=[...document.querySelectorAll('.decision')],as=[...document.querySelectorAll('audio')];let old={};try{old=JSON.parse(localStorage.getItem('fiveRiversR7')||'{}')}catch{}function vals(){return Object.fromEntries(ds.map(c=>[c.dataset.key,{choice:c.querySelector('select').value,time:c.querySelector('input').value,note:c.querySelector('textarea').value}]))}ds.forEach(c=>{let v=old[c.dataset.key];if(v){c.querySelector('select').value=v.choice;c.querySelector('input').value=v.time||'';c.querySelector('textarea').value=v.note||''}c.oninput=()=>localStorage.setItem('fiveRiversR7',JSON.stringify(vals()))});function stopYT(){document.querySelectorAll('iframe').forEach(f=>f.contentWindow.postMessage(JSON.stringify({event:'command',func:'pauseVideo',args:[]}),'https://www.youtube.com'))}as.forEach(a=>a.onplay=()=>{as.forEach(b=>{if(a!==b)b.pause()});stopYT()});document.querySelector('#stop').onclick=()=>{as.forEach(a=>a.pause());document.querySelectorAll('iframe').forEach(f=>f.src=f.src)};document.querySelector('#save').onclick=()=>{let a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(vals(),null,2)],{type:'application/json'}));a.download='R7_판정메모.json';a.click()};</script></html>'''
(out/'실물비교청취.html').write_text(page,encoding='utf-8')
assert all(sha(Path(p))==h for p,h in protected.items())
src=Path(r'C:\Users\mbc\Documents\user\blender-ai-game-assets-guide\blender-ai-game-assets-guide');m=json.loads(Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_Transfer_20261027\Source_Hashes.json').read_text(encoding='utf-8'));assert all(sha(src/r['path'])==r['sha256'] for r in m)
report={'new_candidates':len(rows),'protected_files_unchanged':len(protected),'game_original_files_unchanged':len(m),'git_status':subprocess.run(['git','-C',str(src.parent),'status','--porcelain=v1'],capture_output=True,text=True).stdout}
(out/'보존확인.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
with zipfile.ZipFile(Path(r'C:\Users\mbc\Desktop\FiveRivers_R7_실물비교_메모반영.zip'),'w',zipfile.ZIP_DEFLATED) as z:
 for p in out.rglob('*'):
  if p.is_file():z.write(p,p.relative_to(out))
print(json.dumps(report,ensure_ascii=False))
