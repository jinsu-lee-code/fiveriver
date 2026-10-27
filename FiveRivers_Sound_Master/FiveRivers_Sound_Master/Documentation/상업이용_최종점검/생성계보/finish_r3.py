from pathlib import Path
import sys,json,hashlib,html,shutil,math
sys.path.insert(0,str(Path(__file__).parent/'deps'))
import numpy as np,soundfile as sf
from scipy.signal import butter,sosfiltfilt,resample_poly
root=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027');out=root/'재제작_R3'
for folder in ('Audio','Loop_10x','Previous'):(out/folder).mkdir(exist_ok=True)
votes=json.loads((Path(__file__).parent/'User_Review.json').read_text(encoding='utf-8'))
old={r['file']:r for r in json.loads((root/'QA_41.json').read_text(encoding='utf-8'))}
rows=[];cards=[]
for p in sorted((out/'Source').glob('R3_*.wav')):
 name=p.name[3:];x,sr=sf.read(p,always_2d=True);rawseconds=len(x)/sr;edits=[]
 if sr!=48000:
  g=math.gcd(sr,48000);x=resample_poly(x,48000//g,sr//g,axis=0);sr=48000;edits.append('48kHz 변환')
 seconds=1 if 'Ignition' in name else 2 if 'Foot_Dirt' in name else old[name]['seconds'];n=int(seconds*sr)
 loop='_Loop' in name;bgm=name.startswith('BGM');amb=name.startswith('AMB')
 x=sosfiltfilt(butter(2,15,fs=sr,btype='highpass',output='sos'),x,axis=0);edits.append('15Hz 초저역/DC 정리, 속도·피치 변경 없음')
 if amb:
  x=np.repeat(x.mean(axis=1,keepdims=True),2,axis=1);edits.append('좌우 불일치 제거: 동일 모노를 두 채널에 배치(공간 스테레오 아님)')
 elif not bgm:x=x.mean(axis=1,keepdims=True)
 if loop:
  fade=min(24000,len(x)//5);base=x.copy();r=np.linspace(0,1,fade)[:,None]
  while len(x)<n+fade:x=np.concatenate([x[:-fade],x[-fade:]*(1-r)+base[:fade]*r,base[fade:]])
  head=x[:fade].copy();x=x[fade:fade+n].copy();x[-fade:]=x[-fade:]*(1-r)+head*r
  # The final 2ms converge to the first sample, avoiding a discontinuity at wrap.
  f=96;x[-f:]+=(x[0]-x[-1])*np.linspace(0,1,f)[:,None]
  edits.append('생성 길이 '+str(round(rawseconds,3))+'초를 교차 페이드 반복 편집; 반복 패턴 청취 확인 필요')
 else:
  if not bgm:
   a=np.flatnonzero(np.max(abs(x),axis=1)>.001)
   if len(a):x=x[max(0,int(a[0])-96):]
  x=x[:n].copy();f=min(144000 if bgm else 2400,len(x)//4)
  x[-f:]*=(.5+.5*np.cos(np.linspace(0,np.pi,f)))[:,None]
  start=min(144000 if bgm else 24,len(x)//4);x[:start]*=np.linspace(0,1,start)[:,None]
  x=np.pad(x,((0,max(0,n-len(x))),(0,0)))
  edits.append('BGM 3초 시작/끝 감쇠' if bgm else '시작 무음 정리, 끝 50ms 이내 감쇠, 필요한 후단 무음 채움')
 # Conservative peaks for ambience/music; avoid boosting quiet ambience to weapon levels.
 limit=10**((-12 if amb else -8 if bgm else -2)/20);x*=min(1,limit/max(np.max(abs(x)),1e-10))
 target=out/'Audio'/name;sf.write(target,x,sr,subtype='PCM_16');z,_=sf.read(target,always_2d=True)
 if loop:sf.write(out/'Loop_10x'/name,np.tile(z,(10,1)),sr,subtype='PCM_16')
 previous=next((root/'Audio').rglob(name),None)
 if previous:shutil.copy2(previous,out/'Previous'/name)
 note=next((v['note'] for v in votes if v['file']==name),'조명탄 점화 임팩트 보강용 추가 단발')
 row={'file':name,'user_note':note,'seconds':len(z)/sr,'channels':z.shape[1],'source_seconds':rawseconds,'peak_dbfs':float(20*np.log10(max(np.max(abs(z)),1e-12))),'loop_join_delta':float(np.max(abs(z[0]-z[-1]))) if loop else None,'edits':edits,'auditory_review':'새 버전 청취 대기','sha256':hashlib.sha256(target.read_bytes()).hexdigest()};rows.append(row)
 card='<article><h2>'+name+'</h2><p>'+html.escape(note)+'</p>'
 if previous:card+='<p>이전 버전</p><audio controls preload="none" src="Previous/'+name+'"></audio>'
 card+='<p>새 재제작본 · '+str(seconds)+'초</p><audio controls preload="none" src="Audio/'+name+'"></audio>'
 if loop:card+='<p>새 버전 10회 반복</p><audio controls preload="none" src="Loop_10x/'+name+'"></audio>'
 card+='<details><summary>편집 내역과 확인할 점</summary>'+html.escape(' / '.join(edits))+'</details><label><input type="checkbox"> 이번 버전 합격</label><textarea placeholder="남은 문제와 시점"></textarea></article>';cards.append(card)
(out/'검사결과.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(Path(__file__).parent/'User_Review.json',out/'이전버전_사용자판정.json')
page='''<!doctype html><meta charset="utf-8"><title>재제작 R3 비교 청취</title><style>body{max-width:1000px;margin:30px auto;padding:20px;background:#14212b;color:#e5edf5;font:16px/1.6 system-ui}article{background:#213744;padding:20px;margin:18px 0;border-radius:12px}audio,textarea{width:100%}h2{font-size:18px}a{color:#91e4da}textarea{height:65px}button{padding:12px}</style><h1>재제작 R3 · 이전 버전과 비교</h1><p>재제작 요청 11개 + 조명탄 점화음 1개. 기존 음원·게임 연결 보존. 사용자 메모를 반영해 새로 생성했으며 새 버전의 청취 합격은 아직 미확인입니다.</p><p>발소리는 요청하신 2초 보행입니다. 걸음별 호출에 직접 교체하면 겹칠 수 있습니다. 환경음은 좌우 균형을 우선한 듀얼 모노입니다. 짧게 생성된 루프를 반복 편집했으므로 반복감 확인이 필요합니다.</p><p><a href="../기존게임_점검/체크리스트.html">기존 게임 20개 체크리스트</a> · <a href="../청취실.html">이전 전체 41개</a></p><button id="export">새 버전 판정 저장</button>'''+''.join(cards)+'''<script>const cards=[...document.querySelectorAll('article')];let old={};try{old=JSON.parse(localStorage.getItem('r3AudioQA')||'{}')}catch{}function values(){return Object.fromEntries(cards.map(c=>[c.querySelector('h2').textContent,{accepted:c.querySelector('input').checked,note:c.querySelector('textarea').value}]))}for(const c of cards){let v=old[c.querySelector('h2').textContent];if(v){c.querySelector('input').checked=v.accepted;c.querySelector('textarea').value=v.note}c.oninput=()=>localStorage.setItem('r3AudioQA',JSON.stringify(values()))}document.querySelectorAll('audio').forEach(a=>a.onplay=()=>document.querySelectorAll('audio').forEach(b=>{if(b!==a)b.pause()}));document.querySelector('#export').onclick=()=>{let a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(values(),null,2)],{type:'application/json'}));a.download='R3_청취판정.json';a.click()};</script>'''
(out/'비교청취.html').write_text(page,encoding='utf-8')
print(json.dumps({'count':len(rows),'unique_hashes':len(set(r['sha256'] for r in rows)),'peak_pass':all(r['peak_dbfs']<=-1 for r in rows),'loop_seams':[r['loop_join_delta'] for r in rows if r['loop_join_delta'] is not None]}))
