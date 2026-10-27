from pathlib import Path
import json, shutil, hashlib, struct, subprocess, zipfile, html
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly
R=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027')
W=Path(__file__).parent; O=R/'최종28_수정비교_R16'; S=48000
assert not O.exists(), 'New output only'
policy=json.loads((W/'ACTIVE_AUDIO_POLICY.json').read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
baseline=json.loads((R/'주포_도탄_엔딩_R14/보존기준.json').read_text(encoding='utf-8-sig'))
for d in policy['protected_decisions']:
 assert sha(d['path'])==d['sha256'], d['path']
 baseline[d['path']]=d['sha256']
for p in (R/'주포_도탄_엔딩_R14').rglob('*'):
 if p.is_file(): baseline[str(p)]=sha(p)
for p,h in baseline.items(): assert sha(p)==h,p
for d in ['Delivery/Assets/FiveRivers_ApprovedAudio','Delivery/Documentation','Before','After','Work','Documentation','Loop10']:(O/d).mkdir(parents=True,exist_ok=True)
D=O/'Delivery/Assets/FiveRivers_ApprovedAudio'
old=R/'최종19_후보비교_R13'
shutil.copytree(old/'Delivery/Documentation',O/'Delivery/Documentation/Previous19',dirs_exist_ok=True)
manifest=[]
for p in sorted((old/'Delivery/Assets/FiveRivers_ApprovedAudio').glob('*.wav')):
 shutil.copy2(p,D/p.name);manifest.append({'file':p.name,'source':str(p),'sha256':sha(p),'approval':'既存19 / 기존 합격','rights':'Previous19 문서 참조'})
for d in policy['protected_decisions']:
 if d['choice']!='합격':continue
 p=Path(d['path']); assert not (D/p.name).exists()
 info=sf.info(p);assert info.samplerate==S and info.subtype=='PCM_16'
 shutil.copy2(p,D/p.name)
 manifest.append({**d,'file':p.name,'rights':'출처_이용조건.txt 및 원출처 기록 참조'})
assert len(manifest)==28
def dump(p,obj):p.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
dump(O/'Delivery/Documentation/Manifest.json',manifest)
dump(O/'Documentation/판정메모_보존.json',policy)
shutil.copy2(old/'Candidates/Manifest.json',O/'Delivery/Documentation/R13_원출처기록.json')
shutil.copy2(R/'주포_도탄_엔딩_R14/Documentation/출처_이용조건_한계.txt',O/'Delivery/Documentation/R14_출처_이용조건_한계.txt')
(O/'Delivery/Documentation/출처_이용조건.txt').write_text('합격 음원 28개만 포함. 게임 연결 변경 없음. 기존 M1 3개만 포함하며 미판정 M1은 제외. 도탄 R13/R14는 각각 합격한 별도 버전.\n사용자 합격은 라이선스 검증 완료와 다릅니다. 기존19 이용조건은 Previous19 참조. VARCO 생성음은 생성 당시 이용 플랜/약관 증빙을 출시 전 확인해야 합니다.\nVO_OSG_Hurt_Candidate: Valerie-Vivegnis, Freesound 768185, CC BY 4.0. 편집된 음원. https://freesound.org/people/Valerie-Vivegnis/sounds/768185/ https://creativecommons.org/licenses/by/4.0/\nSFX_Tank_Ricochet_01: aust_paul, Freesound 30932, CC0. 모사 도탄.\nSFX_Tank_Ricochet_R14: rakurka, Incoming Ricochets, Freesound 109958, CC BY 3.0. 발췌·편집·합성 혼합. https://freesound.org/people/rakurka/sounds/109958/ https://creativecommons.org/licenses/by/3.0/\n참고 영상 음원은 이 묶음에 포함하지 않았습니다.\n',encoding='utf-8')
def read(p):
 x,s=sf.read(p,always_2d=True);assert s==S;return x
def save(p,x):
 peak=float(np.max(abs(resample_poly(x,4,1,axis=0))))
 x=x*min(1,10**(-2/20)/max(peak,1e-12));sf.write(p,x,S,subtype='PCM_16')
def fade(x,a,b):
 x=x.copy();n=int(a*S);m=int(b*S)
 if n:x[:n]*=np.linspace(0,1,n)[:,None]
 if m:x[-m:]*=np.linspace(1,0,m)[:,None]
 return x
items=[('SFX_Foot_Dirt_04.wav','SFX_Foot_Dirt_R16.wav','흙 위 걷기','흙위걷는소리 로 보정','합격한 흙 발걸음을 복사해 3걸음으로 구성. 피치·속도 변형 없음.'),('SFX_Tank_Fire_Loop.wav','SFX_Tank_Fire_Loop_R16.wav','전차 화재','끝에 빨려들어가는 거빼고 불계속나는걸로 수정해','원본 앞·중간 구간만 사용하고 후반부 제외. 역재생 없이 교차 연결한 지속 화재 루프.'),('BGM_Ending_R14.wav','BGM_Ending_R16.wav','전투 후 엔딩','전쟁의 비극적인 느낌을 살려서 살아남았다는 느낌이 드는 엔딩곡으로 만들어','48 BPM 새 피아노·저음 첼로 편곡. 낮은 음역, 불협화음과 긴 쉼 뒤 작은 선율이 돌아오는 구성. 고역 조절만 한 버전이 아님.')]
for before,after,title,note,edit in items:
 src=R/'주포_도탄_엔딩_R14/Audio'/before if 'R14' in before else old/'Candidates/After'/before
 shutil.copy2(src,O/'Before'/before)
step=read(D/'FOLEY_Footsteps_Dirt.wav').mean(axis=1,keepdims=True)
step=fade(step,.002,.025);walk=np.zeros((S*2,1))
for sec,gain in [(0,.9),(.66,1),(1.32,.94)]:
 start=int(sec*S);n=min(len(step),len(walk)-start);walk[start:start+n]+=step[:n]*gain
save(O/'After/SFX_Foot_Dirt_R16.wav',walk)
# Discard the entire late source region rather than trying to EQ its suction-like tail.
src=read(R/'재제작_R3/Source/R3_SFX_Tank_Fire_Loop.wav').mean(axis=1,keepdims=True)
assert len(src)>int(4.8*S)
segments=[src[int(a*S):int(b*S)].copy() for a,b in [(.35,2.65),(1.5,3.95),(.8,3.1),(2.1,4.5)]]
target=np.sqrt(np.mean(np.concatenate(segments)**2))
segments=[x*target/max(np.sqrt(np.mean(x*x)),1e-9) for x in segments]
n=int(.28*S);theta=np.linspace(0,np.pi/2,n)[:,None]
fire=segments[0]
for x in segments[1:]:fire=np.concatenate([fire[:-n],fire[-n:]*np.cos(theta)+x[:n]*np.sin(theta),x[n:]])
# A cyclic overlap removes the final transition; rotation puts it away from the file edge.
fire=np.concatenate([fire[-n:]*np.cos(theta)+fire[:n]*np.sin(theta),fire[n:-n]])
fire=np.roll(fire,len(fire)//2,axis=0)
save(O/'After/SFX_Tank_Fire_Loop_R16.wav',fire)
sf.write(O/'Loop10/SFX_Tank_Fire_Loop_R16_10x.wav',np.tile(read(O/'After/SFX_Tank_Fire_Loop_R16.wav'),(10,1)),S,subtype='PCM_16')
# New composition, not a processing pass on the judged music.
events=[];tempo=1250000;ticks=480
def at(t,data):events.append((round(t*1000000/tempo*ticks),bytes(data)))
def note(ch,k,t,d,v):at(t,[0x90+ch,k,v]);at(t+d,[0x80+ch,k,0])
for ch,prog,vol,pan in [(0,0,90,57),(1,42,44,70)]:
 at(0,[0xc0+ch,prog]);at(0,[0xb0+ch,7,vol]);at(0,[0xb0+ch,10,pan]);at(0,[0xb0+ch,91,34])
for t,keys in [(0.5,[38,45]),(7,[43,50,58]),(13.5,[39,46,57]),(21,[38,45]),(29,[38,45]),(34,[45,62])]:
 for i,k in enumerate(keys):note(0,k,t+i*.09,4.5,32-i*3)
for t,k,d,v in [(2,62,2,36),(4.8,61,1.3,29),(8.6,58,2.8,33),(12,57,1.2,27),(15.1,63,2,31),(18,62,2.8,26),(23,57,2.2,25),(27.5,62,2,30),(30.2,65,1.5,31),(32.3,64,1.8,27),(35,62,2.4,24)]:note(0,k,t,d,v)
for t,k,d in [(7.4,43,4.5),(14,39,4),(22,38,4),(29,45,4.6)]:note(1,k,t,d,20)
def vlq(n):
 b=[n&127];n>>=7
 while n:b.insert(0,128|(n&127));n>>=7
 return bytes(b)
track=bytearray(b'\0\xff\x51\x03'+tempo.to_bytes(3,'big'));last=0
for pos,data in sorted(events,key=lambda e:e[0]):track.extend(vlq(pos-last)+data);last=pos
track.extend(vlq(round(40*1000000/tempo*ticks)-last)+b'\xff\x2f\0')
mid=O/'Work/Ending_R16.mid';mid.write_bytes(b'MThd'+struct.pack('>IHHH',6,0,1,ticks)+b'MTrk'+struct.pack('>I',len(track))+track)
mt=W/'music_tools';exe=next((mt/'fluidsynth').rglob('fluidsynth.exe'))
subprocess.run([str(exe),'-ni','-r',str(S),'-g','.8','-C','0','-R','1','-o','synth.reverb.level=0.20','-F',str(O/'Work/Ending_Render.wav'),'-T','wav','-O','float',str(mt/'GeneralUser-GS.sf2'),str(mid)],check=True,capture_output=True)
music=read(O/'Work/Ending_Render.wav')[:S*40];music*=10**(-24/20)/max(np.sqrt(np.mean(music*music)),1e-9)
save(O/'After/BGM_Ending_R16.wav',fade(music,3,3))
shutil.copy2(mt/'GeneralUser_LICENSE.txt',O/'Documentation/GeneralUser_LICENSE.txt')
metrics=[]
for p in (O/'After').glob('*.wav'):
 x=read(p);metrics.append({'file':p.name,'seconds':len(x)/S,'sample_rate':S,'channels':x.shape[1],'subtype':sf.info(p).subtype,'true_peak_db':float(20*np.log10(max(np.max(abs(resample_poly(x,4,1,axis=0))),1e-12))),'loop_boundary_jump':float(np.max(abs(x[0]-x[-1]))) if 'Loop' in p.name else None,'status':'측정 검증 / 사용자 청취 판정 대기'})
dump(O/'Documentation/수정_측정.json',{'items':[dict(zip(['before','after','title','user_note','edit'],i)) for i in items],'metrics':metrics,'listening_limit':'사람처럼 직접 청취해 합격 판정한 것이 아님. 새 후보는 사용자의 비교 청취 필요.'})
for p,h in baseline.items():assert sha(p)==h,p
for d in manifest:assert sha(D/d['file'])==d['sha256']
dump(O/'Documentation/보존검증.json',{'protected_files_checked':len(baseline),'changes':0,'approved_wavs':28,'new_candidates':3,'game_connections_modified':False,'unjudged_M1_included':False})
with zipfile.ZipFile(O/'FiveRivers_Approved28_Delivery.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in (O/'Delivery').rglob('*'):
  if p.is_file():z.write(p,p.relative_to(O/'Delivery'))
cards=[]
for before,after,title,note,edit in items:
 loop='<p>10회 연속 확인</p><audio controls preload="none" src="Loop10/SFX_Tank_Fire_Loop_R16_10x.wav"></audio>' if 'Loop' in after else ''
 cards.append(f'<article data-key="{after}"><h2>{title}</h2><blockquote>{html.escape(note)}</blockquote><p>{edit}</p><div class="pair"><div>수정 전<audio controls preload="none" src="Before/{before}"></audio></div><div>R16 수정 후보<audio controls preload="none" src="After/{after}"></audio></div></div>{loop}<select><option>미판정</option><option>합격</option><option>추가 수정 필요</option><option>부적합</option></select><textarea placeholder="수정 메모"></textarea></article>')
approved=''.join(f'<li>{html.escape(d["file"])}<audio controls preload="none" src="Delivery/Assets/FiveRivers_ApprovedAudio/{d["file"]}"></audio></li>' for d in manifest)
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>최종 28 · 수정 후보 R16</title><style>body{background:#151b24;color:#e9eef4;font:17px system-ui;max-width:1080px;margin:40px auto;padding:20px}article,details{background:#232d3b;padding:24px;margin:20px 0;border-radius:12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:22px}audio{display:block;width:100%;margin:12px 0}textarea{display:block;width:95%;height:75px;margin-top:12px}select{padding:8px}a{color:#8cd8ff}li{padding:12px}blockquote{border-left:3px solid #9ebbd8;padding-left:15px}</style><h1>최종 합격 28개 · 수정 비교 3개</h1><p>기존 19 + 새 합격 9. M1은 기존 합격본만 사용. 판정 원본·게임 연결 변경 없음.</p><p><a href="FiveRivers_Approved28_Delivery.zip">최종 합격 28개 묶음 받기</a> · 후보 3개는 이 묶음에 포함되지 않습니다.</p><p>파형·규격 검사 완료. 새 후보의 자연스러움과 분위기는 아래에서 비교해 주세요. 메모는 이 브라우저에 저장됩니다.</p>'''+''.join(cards)+'<details><summary>합격 28개 목록과 청취</summary><ol>'+approved+'</ol></details>'+'''<script>const key='FiveRivers_R16_votes';let votes={};try{votes=JSON.parse(localStorage.getItem(key)||'{}')}catch(e){}document.querySelectorAll('article').forEach(c=>{let s=c.querySelector('select'),t=c.querySelector('textarea'),v=votes[c.dataset.key];if(v){s.value=v.choice;t.value=v.note}function save(){votes[c.dataset.key]={choice:s.value,note:t.value};localStorage.setItem(key,JSON.stringify(votes))}s.onchange=save;t.oninput=save});document.addEventListener('play',e=>{document.querySelectorAll('audio').forEach(a=>{if(a!==e.target)a.pause()})},true)</script></html>'''
(O/'비교청취.html').write_text(page,encoding='utf-8')
print(json.dumps({'output':str(O),'approved':28,'metrics':metrics,'protected_unchanged':len(baseline)},ensure_ascii=False))
