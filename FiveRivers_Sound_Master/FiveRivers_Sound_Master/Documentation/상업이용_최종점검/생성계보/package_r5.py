from pathlib import Path
import json,sys,shutil,hashlib,html,math,zipfile,subprocess
sys.path.insert(0,str(Path(__file__).parent/'deps'))
import numpy as np,soundfile as sf
from scipy.signal import resample_poly,butter,sosfiltfilt
root=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027')
old=root/'기존게임_수정비교'; final=root/'사용자합격_최종본'; rev=root/'추가수정_R5'
for p in [final/'Audio',rev/'Source',rev/'Audio',rev/'Previous',rev/'Loop_10x']:p.mkdir(parents=True,exist_ok=True)
votes=json.loads((Path(__file__).parent/'Final_User_Votes.json').read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
approved=[];pending=[]
for v in votes:
 if v['note'] or v['choice']=='추가 수정 필요':pending.append(v);continue
 folder={'원본 선호':'Before','보정본 사용 후보':'After','신규 재현 사용 후보':'New_Candidates'}.get(v['choice'])
 if not folder:continue
 p=old/folder/v['file'];d=final/'Audio'/v['file'];shutil.copy2(p,d);assert sha(p)==sha(d)
 approved.append({**v,'source':str(p),'sha256':sha(d)})
assert len(approved)==11
(final/'선택내역.json').write_text(json.dumps(approved,ensure_ascii=False,indent=2),encoding='utf-8')
(rev/'사용자수정메모.json').write_text(json.dumps(pending,ensure_ascii=False,indent=2),encoding='utf-8')
def limit(x):
 peak=np.max(abs(resample_poly(x,4,1,axis=0)));return x*min(1,10**(-1.5/20)/max(peak,1e-9))
def loop(x,n=4800):
 n=min(n,len(x)//8);r=(.5-.5*np.cos(np.linspace(0,np.pi,n)))[:,None]
 y=np.concatenate([x[n:-n],x[-n:]*(1-r)+x[:n]*r]);y[-1]=y[0];return y
name='AMB_Distant_Artillery.wav';x,sr=sf.read(old/'After'/name,always_2d=True)
# Extend using overlap-add at unchanged sample speed; cyclic seam totals exactly 10 s.
fade=int(.3*sr);r=np.linspace(0,1,fade)[:,None];y=x.copy()
while len(y)<10*sr+fade:
 y=np.concatenate([y[:-fade],y[-fade:]*(1-r)+x[:fade]*r,x[fade:]])
y=loop(y[:10*sr+fade],fade);assert len(y)==10*sr
sf.write(rev/'Audio'/name,limit(y),sr,subtype='PCM_16')
loops={'AMB_Distant_Artillery.wav','SFX_Flare_Burn.wav','SFX_T34_Engine.wav','SFX_Truck_Engine.wav'}
records=[]
for v in pending:
 name=v['file'];shutil.copy2(old/'After'/name,rev/'Previous'/name)
 p=rev/'Source'/('R5_'+name)
 if p.exists():
  x,sr=sf.read(p,always_2d=True);g=math.gcd(sr,48000);y=resample_poly(x,48000//g,sr//g,axis=0)
  y=sosfiltfilt(butter(2,15,fs=48000,btype='highpass',output='sos'),y,axis=0)
  if name in loops:y=loop(y)
  else:
   n=min(2400,len(y)//10);y[:24]*=np.linspace(0,1,24)[:,None];y[-n:]*=np.linspace(1,0,n)[:,None]
  sf.write(rev/'Audio'/name,limit(y),48000,subtype='PCM_16')
 p=rev/'Audio'/name
 if p.exists():
  y,sr=sf.read(p,always_2d=True)
  if name in loops:sf.write(rev/'Loop_10x'/name,np.tile(y,(10,1)),sr,subtype='PCM_16')
  tp=float(20*np.log10(max(np.max(abs(resample_poly(y,4,1,axis=0))),1e-12)))
  assert tp < -1.4 and np.isfinite(y).all()
  records.append({**v,'seconds':len(y)/sr,'rate':sr,'channels':y.shape[1],'true_peak_estimate_dbfs':tp,'seam':float(np.max(abs(y[0]-y[-1]))),'status':'사용자 청취 미확인','sha256':sha(p)})
(rev/'수정검사결과.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
style='<style>body{background:#13232c;color:#edf3f5;max-width:1000px;margin:30px auto;padding:20px;font:16px/1.65 system-ui}article{background:#213a47;padding:22px;margin:20px 0;border-radius:14px}h2{font-size:20px;overflow-wrap:anywhere}a{color:#98e5ca}audio{width:100%}textarea{width:98%;height:70px}select,button{padding:10px;font:inherit}.warn{color:#ffcd91}</style>'
head=lambda title:'<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>'+title+'</title>'+style+'<h1>'+title+'</h1>'
cards=''.join('<article><h2>'+v['file']+'</h2><p>'+v['choice']+' · 선택한 파일 그대로 복사</p><audio controls preload="none" src="Audio/'+v['file']+'"></audio></article>' for v in approved)
single="<script>document.querySelectorAll('audio').forEach(a=>a.onplay=()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()}));</script>"
(final/'최종본청취.html').write_text(head('사용자 선택 최종본 · 11개')+'<p>원본 선호 4개 + 보정본 선택 7개. 수정 메모가 있는 9개는 제외했습니다. 기존 게임 연결은 변경하지 않았습니다.</p><a href="../추가수정_R5/수정후보청취.html">추가 수정 후보 비교</a>'+cards+single,encoding='utf-8')
refs={'SFX_T34_Cannon.wav':('T-34 실사격 영상','https://www.youtube.com/watch?v=0v5zdE21gXU'),'SFX_T34_Engine.wav':('박물관 T-34/85 시동 영상','https://www.youtube.com/watch?v=nQM4xqCdgSw'),'SFX_Truck_Engine.wav':('GMC CCKW 복원 시동 영상','https://www.youtube.com/watch?v=ZsqW-lkQiFk'),'SFX_Mosin_Fire.wav':('Mosin 실물 자료 후보','https://www.youtube.com/watch?v=OFA5U5FwJbo'),'SFX_T34_Ricochet.wav':('9mm 강철 표적 도탄: 전차와 다른 조건','https://www.youtube.com/watch?v=5Ggd_EABmlA'),'SFX_Flare_Burn.wav':('군용 조명탄 영상: 현대 자료','https://www.dvidshub.net/video/16304/soldiers-fire-illumination-flares')}
cards=[]
for v in pending:
 name=v['file'];c='<article><h2>'+name+'</h2><p>요청: '+html.escape(v['note'])+'</p><p>이전 보정본</p><audio controls preload="none" src="Previous/'+name+'"></audio>'
 if (rev/'Audio'/name).exists():
  c+='<p>R5 새 수정 후보</p><audio controls preload="none" src="Audio/'+name+'"></audio>'
  if name in loops:c+='<details><summary>10회 연속 반복 확인</summary><audio controls preload="none" src="Loop_10x/'+name+'"></audio></details>'
 else:c+='<p class="warn">생성 대기</p>'
 if name in refs:c+='<a target="_blank" href="'+refs[name][1]+'">'+refs[name][0]+'</a><p class="warn">참고 링크: 영상 음원 직접 청취·동일성 비교 미완료. 영상 음원을 추출하거나 복제하지 않았습니다.</p>'
 if name=='SFX_Bazooka_Hit.wav':c+='<p class="warn">해당 탄종 실물 충격 녹음 확보 및 직접 청취 비교 미완료</p>'
 c+='<select><option>청취 미확인</option><option>합격</option><option>추가 수정 필요</option></select><textarea placeholder="문제 시점과 수정할 내용"></textarea></article>';cards.append(c)
js='''<script>const cards=[...document.querySelectorAll('article')];let old={};try{old=JSON.parse(localStorage.getItem('fiveRiversR5')||'{}')}catch{}function values(){return Object.fromEntries(cards.map(c=>[c.querySelector('h2').textContent,{choice:c.querySelector('select').value,note:c.querySelector('textarea').value}]))}for(const c of cards){const v=old[c.querySelector('h2').textContent];if(v){c.querySelector('select').value=v.choice;c.querySelector('textarea').value=v.note}c.oninput=()=>localStorage.setItem('fiveRiversR5',JSON.stringify(values()))}document.querySelector('#save').onclick=()=>{const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(values(),null,2)],{type:'application/json'}));a.download='R5_청취판정.json';a.click()};</script>'''
(rev/'수정후보청취.html').write_text(head('추가 수정 R5 · 최종본 제외')+'<p>먼 포성: 정확히 10초 루프로 연장. 나머지: 원래 속도·음높이로 생성한 새 후보. 루프는 연결부 크로스페이드 처리. 10회 반복 파일은 청취용이며 직접 청취 합격 판정은 아직 없습니다.</p><p><a href="../사용자합격_최종본/최종본청취.html">합격한 11개 듣기</a></p><button id="save">판정 파일 저장</button>'+''.join(cards)+single+js,encoding='utf-8')
src=Path(r'C:\Users\mbc\Documents\user\blender-ai-game-assets-guide\blender-ai-game-assets-guide')
manifest=json.loads(Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_Transfer_20261027\Source_Hashes.json').read_text(encoding='utf-8'))
changes=[r['path'] for r in manifest if sha(src/r['path'])!=r['sha256']];assert not changes
status=subprocess.run(['git','-C',str(src.parent),'status','--porcelain=v1'],capture_output=True,text=True).stdout
check={'approved_count':len(approved),'revision_ready':len(records),'protected_count':len(manifest),'changed_originals':changes,'git_status':status}
(final/'원본보존검증.json').write_text(json.dumps(check,ensure_ascii=False,indent=2),encoding='utf-8')
for folder,title in [(final,'FiveRivers_사용자합격_11개_최종본'),(rev,'FiveRivers_추가수정_R5_청취후보')]:
 dest=Path(r'C:\Users\mbc\Desktop')/(title+'.zip')
 with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED) as z:
  for p in folder.rglob('*'):
   if p.is_file():z.write(p,p.relative_to(folder))
 with zipfile.ZipFile(dest) as z:assert z.testzip() is None
print(json.dumps(check,ensure_ascii=False))
