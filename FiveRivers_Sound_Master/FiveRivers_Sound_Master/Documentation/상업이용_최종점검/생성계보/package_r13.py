from pathlib import Path
import json, hashlib, shutil, math, html, zipfile
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly, butter, sosfiltfilt

ROOT=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027')
OUT=ROOT/'최종19_후보비교_R13'
assert not OUT.exists(), 'New output only'
OUT.mkdir()
SR=48000
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
protected={}
def protect(p):
    p=Path(p);protected[str(p)]=sha(p);return p
for p in (ROOT/'최종합격_에셋15').rglob('*'):
    if p.is_file():protect(p)
game=Path(r'C:\Users\mbc\Documents\user\blender-ai-game-assets-guide\blender-ai-game-assets-guide')
game_manifest=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_Transfer_20261027\Source_Hashes.json')
game_files=[]
if game_manifest.exists():
    for row in json.loads(game_manifest.read_text(encoding='utf-8')):
        p=game/row['path']
        if p.exists():protect(p);game_files.append(str(p))
def read(p):
    protect(p);x,r=sf.read(p,always_2d=True)
    if r!=SR:
        g=math.gcd(r,SR);x=resample_poly(x,SR//g,r//g,axis=0)
    return x
def filt(x,f,kind='lowpass'):
    return sosfiltfilt(butter(2,f,fs=SR,btype=kind,output='sos'),x,axis=0)
def rms(x):return float(np.sqrt(np.mean(x*x)))
def fade(x,a=.0005,b=.03):
    x=x.copy();n=min(int(a*SR),len(x)//4);m=min(int(b*SR),len(x)//4)
    if n:x[:n]*=np.linspace(0,1,n)[:,None]
    if m:x[-m:]*=np.linspace(1,0,m)[:,None]
    return x
def standard_channels(x,name):
    stereo=name.startswith(('AMB_','BGM_'))
    if stereo and x.shape[1]==1:return np.repeat(x,2,axis=1),'모노를 두 채널로 복제: 공간 스테레오 아님'
    if not stereo and x.shape[1]>1:return x.mean(axis=1,keepdims=True),'채널 평균으로 모노 변환'
    return x,'채널 유지'
def write(p,x):
    p.parent.mkdir(parents=True,exist_ok=True)
    assert len(x)>0 and np.isfinite(x).all()
    peak=float(np.max(abs(resample_poly(x,4,1,axis=0))))
    gain=min(1,10**(-1.1/20)/max(peak,1e-12));x=x*gain
    rng=np.random.default_rng(2713)
    x=x+(rng.random(x.shape)-rng.random(x.shape))/65536
    sf.write(p,x,SR,subtype='PCM_16')
    y,r=sf.read(p,always_2d=True);info=sf.info(p)
    tp=float(20*np.log10(max(np.max(abs(resample_poly(y,4,1,axis=0))),1e-12)))
    assert r==SR and info.subtype=='PCM_16' and tp<=-1 and np.isfinite(y).all()
    return dict(seconds=len(y)/SR,channels=y.shape[1],sample_rate=r,subtype=info.subtype,true_peak_db=tp,gain_db=20*math.log10(gain),sha256=sha(p))
def copy(p,q):
    protect(p);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q);assert sha(p)==sha(q)
votes=json.loads((Path(__file__).parent/'User_Review.json').read_text(encoding='utf-8'))
approved=json.loads((ROOT/'최종합격_에셋15/Documentation/에셋목록.json').read_text(encoding='utf-8'))
sources=[(ROOT/'최종합격_에셋15'/r['asset'],Path(r['asset']).name,r) for r in approved]
for name in ['SFX_Bazooka_Load.wav','SFX_Enemy_RifleFire_01.wav','SFX_Tank_Move_Loop.wav','AMB_Silence_Loop.wav']:
    assert next(v for v in votes if v['file']==name)['vote']=='합격'
    p=next((ROOT/'Audio').rglob(name))
    sources.append((p,name,dict(source=str(p),approval='기존 41개 청취판 합격: 최종 묶음 누락분 복구',license_status='기존 VARCO 생성 기록의 조건 유지. 생성 시점 상업 이용 증빙 확인 필요.')))
manifest=[]
for p,name,r in sources:
    original=OUT/'Approved_Originals'/name;copy(p,original)
    x=read(p);x,channel_note=standard_channels(x,name)
    target=OUT/'Delivery/Assets/FiveRivers_ApprovedAudio'/name
    facts=write(target,x)
    manifest.append(dict(file=name,original=str(p),original_sha256=sha(p),approval=r['approval'],license_status=r['license_status'],channel_note=channel_note,**facts))
assert len(manifest)==19
docs=OUT/'Delivery/Documentation';docs.mkdir(parents=True)
(docs/'Manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
copy(ROOT/'최종합격_에셋15/Documentation/출처와_사용조건.txt',docs/'기존15_출처와_사용조건.txt')
(docs/'README.txt').write_text('19개 합격 원본에서 만든 별도 규격 복사본. WAV 48kHz PCM16, SFX 모노 / AMB 두 채널, true peak <= -1dB.\n원본 길이·속도·피치를 유지. 모노 환경음은 듀얼 모노이며 공간 스테레오 복원이 아님.\n파일명 및 길이가 SOUND_LIST 전체와 일치하는 완성 세트는 아님. 별도 변형·목록 매핑 검토 필요.\n게임 연결과 .meta를 포함하지 않음. 자동 설치하지 말고 별도 검수 후 사용.\n원본 합격은 승계했지만 채널 변환의 청감은 청취판에서 비교 가능. 출처별 조건과 미확인 사항은 Manifest.json 참조.\n',encoding='utf-8-sig')

pending=[]
def emit(name,src,edit,mode='technical',note='',extra_sources=None):
    x=read(src);x,cn=standard_channels(x,name);before=x.copy();loop='_Loop' in name
    x=filt(x,15,'highpass')
    if mode=='cannon':
        body=filt(filt(x,65,'highpass'),280)
        x=x+body*.45
        x*=rms(before)/max(rms(x),1e-12)
    elif mode=='distant':
        x=filt(x,6500);x*=min(1,10**(-18/20)/max(rms(x),1e-12))
    elif mode=='ending':
        x=filt(x,4200);x=fade(x,3,3)
    elif mode=='ricochet':
        # Keep only the first separated event; no artificial pitch sweep.
        env=np.max(abs(x),axis=1);active=np.flatnonzero(env>max(.003,float(env.max())*.08))
        start=max(0,int(active[0]) - 144) if len(active) else 0
        tail=min(len(x),start+int(.6*SR))
        x=x[start:tail]
    if not loop:
        if mode!='ending' and 'Foot_Dirt' not in name:
            active=np.flatnonzero(np.max(abs(x),axis=1)>max(.0003,float(np.max(abs(x)))*.003))
            if len(active) and active[0]>480:x=x[max(0,int(active[0])-96):]
        x=fade(x,.0005,.035) if mode!='ending' else x
    else:
        # Smooth a wrap over 30ms without changing duration or engine speed.
        n=min(1440,len(x)//20)
        t=np.linspace(0,1,n)[:,None]
        extension=x[:n]
        x[-n:]=x[-n:]*(1-t)+extension*t
        x[-96:]+=(x[0]-x[-1])*np.linspace(0,1,96)[:,None]
    bp=OUT/'Candidates/Before'/name;copy(src,bp)
    target=OUT/'Candidates/After'/name;facts=write(target,x)
    y,_=sf.read(target,always_2d=True)
    boundary=float(np.max(abs(y[0]-y[-1]))) if loop else None
    if loop:
        q=OUT/'Candidates/Loop10'/name;q.parent.mkdir(parents=True,exist_ok=True)
        sf.write(q,np.tile(y,(10,1)),SR,subtype='PCM_16')
    pending.append(dict(file=name,source=str(src),note=note,edit=edit+' / '+cn,loop=loop,boundary=boundary,status='청취 미확인',**facts))

note_map={v['file']:v['note'] for v in votes}
for p in sorted((ROOT/'재제작_R3/Audio').glob('*.wav')):
    name=p.name;mode='technical';edit='15Hz 이하 정리, 시작·끝 클릭 방지, 규격 보정. 기존 R3 메모 반영본을 기반으로 사용.'
    if name=='SFX_Tank_Ricochet_01.wav':continue
    if name=='AMB_Battle_Distant_Loop.wav':mode='distant';edit='먼 전투용 고역 완화, 과한 평균 음량 감쇠, 루프 경계 보정.'
    elif name=='BGM_Ending.wav':mode='ending';edit='R3 음악의 밝은 고역 완화, 3초 시작·끝 감쇠 유지. 밝은 화성 자체는 EQ로 바뀌지 않으므로 분위기 합격은 별도 판정 필요.'
    elif name.startswith('SFX_Foot_Dirt'):edit='요청한 2초 길이 유지, 저역 잡음·끝 경계 정리. 한 걸음 트리거용 단발은 아님.'
    elif name=='AMB_Night_Hills_Loop.wav':edit='R3의 좌우 동일 신호 유지, 루프 경계 보정. 공간 스테레오를 새로 만든 것은 아님.'
    elif name=='SFX_Flare_Burn_Loop.wav':
        p=ROOT/'실물비교_메모반영_R7/Audio/R7_Flare_Burn.wav';edit='후속 R7 연소 후보를 사용해 순환 경계·저역 정리. 점화 충격은 별도 Ignition 파일로 분리.'
    emit(name,p,edit,mode,note_map.get(name,'점화 임팩트 별도 후보'))
emit('SFX_Tank_Cannon.wav',ROOT/'추가보정_R8/Audio/R8_T34_Cannon_A.wav','R8 A의 65–280Hz 원래 성분 보강, 이전 RMS에 맞춰 음량 착시 완화. 실물 녹음 복제·일치 판정 아님.','cannon','더 무겁게 / 영상의 엔진음과 주포음을 최대한 복사해: 허가 없는 영상 음원은 혼합하지 않음')
emit('SFX_Tank_Ricochet_01.wav',ROOT/'추가보정_R8/Audio/R8_Ricochet_Alternatives.wav','연속 모사 도탄 후보에서 첫 구간을 분리하고 끝단 정리. 실물 장갑 도탄으로 검증되지 않아 추가 탐색 과제는 남음.','ricochet','전차 도탄음 더 찾아봐')
emit('VO_OSG_Hurt_Candidate.wav',ROOT/'실물비교_메모반영_R7/Audio/R7_Wounded_Trim1s.wav','앞 1초 제거된 이전 신음의 남은 길이 유지, 15Hz 이하와 끝단 정리. 0.6초 3종 납품 요구는 미충족.','technical','기존 앞 1초 제거 메모 유지. 대사가 아닌 신음 후보.')
updated={r['file'] for r in pending}
for v in votes:
    if v['vote']=='미확인' and v['file'] not in updated:
        p=next((ROOT/'Audio').rglob(v['file']))
        emit(v['file'],p,'미판정 후보: 규격·무음·끝 경계만 정리. 음색 수정 요구가 없는 항목이며 자동 합격 처리하지 않음.',note=v['note'])
(OUT/'Candidates/Manifest.json').write_text(json.dumps(pending,ensure_ascii=False,indent=2),encoding='utf-8')
for folder,file in [('실물비교_메모반영_R7','상업이용_출처.txt'),('추가보정_R8','출처와_한계.txt')]:
    copy(ROOT/folder/file,OUT/'Candidates'/('출처_'+folder+'.txt'))

def audio(path):return '<audio controls preload="none" src="'+html.escape(path)+'"></audio>'
cards=[]
for r in manifest:
    name=r['file'];cards.append('<article><h2>'+name+'</h2><p>합격 원본 → 납품 복사본 · '+r['channel_note']+'</p><div class="pair"><section><h3>합격 원본 그대로</h3>'+audio('Approved_Originals/'+name)+'</section><section><h3>48kHz · 16bit 복사본</h3>'+audio('Delivery/Assets/FiveRivers_ApprovedAudio/'+name)+'</section></div></article>')
approved_html=''.join(cards);cards=[]
for r in pending:
    name=r['file'];ref='<p><a target="_blank" href="https://www.youtube.com/watch?v=0v5zdE21gXU">기존 주포 참고 영상 열기</a> · 영상 음원은 수록하지 않음</p>' if name=='SFX_Tank_Cannon.wav' else ''
    cards.append('<article data-key="'+name+'"><h2>'+name+'</h2><p>'+html.escape(r['note'])+'</p><p>'+html.escape(r['edit'])+'</p>'+ref+'<div class="pair"><section><h3>수정 전</h3>'+audio('Candidates/Before/'+name)+'</section><section><h3>R13 수정 후보 · '+str(round(r['seconds'],3))+'초</h3>'+audio('Candidates/After/'+name)+'</section></div>'+('<details><summary>10회 반복 청취</summary>'+audio('Candidates/Loop10/'+name)+'</details>' if r['loop'] else '')+'<label>판정 <select><option>청취 미확인</option><option>합격</option><option>추가 수정 필요</option><option>부적합</option></select></label><textarea placeholder="문제 시점과 수정 메모"></textarea></article>')
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>합격 19개 · 남은 후보 R13</title><style>body{font:16px/1.65 system-ui;background:#101c25;color:#eaf1f5;max-width:1150px;margin:auto;padding:24px}a{color:#99e6cf}article{padding:22px;background:#21343f;border-radius:12px;margin:20px 0}h2{font-size:20px;overflow-wrap:anywhere}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}audio,textarea{width:100%}textarea{height:65px;margin-top:12px;box-sizing:border-box}select,button{padding:10px;font:inherit}details{margin:16px 0}.notice{padding:20px;background:#34444f}nav{position:sticky;top:0;background:#101c25;padding:12px}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style><h1>합격 19개 + 남은 후보 R13</h1><p class="notice">누락된 합격 4개를 복구했습니다. 원본 19개와 규격 복사본을 비교할 수 있습니다. 후보는 자동 합격하지 않았습니다. 파형·파일 규격 검사는 완료했지만 실물과 청감이 같다는 판정은 아닙니다. 기존 게임 연결은 변경하지 않았습니다.</p><nav><a href="#approved">합격 19개</a> · <a href="#pending">남은 후보 NUM개</a> · <a href="FiveRivers_Approved19_Delivery.zip">납품 복사본 ZIP</a></nav><h2 id="approved">합격 원본 / 규격 복사본</h2>'''+approved_html+'<h2 id="pending">후보 NUM개 · 수정 전후 비교</h2><p>주포·도탄의 실물 음색, 엔딩 음악 화성·분위기, 짧은 반복감은 최종 청취 판정이 필요합니다. 대사 신규 제작 및 B순위 미제작 항목은 이번 묶음에 포함하지 않았습니다.</p><button id="export">후보 판정·메모 내보내기</button>'+''.join(cards)+'''<script>const cards=[...document.querySelectorAll('article[data-key]')];let old={};try{old=JSON.parse(localStorage.getItem('FiveRivers_R13')||'{}')}catch{}function vals(){return Object.fromEntries(cards.map(c=>[c.dataset.key,{choice:c.querySelector('select').value,note:c.querySelector('textarea').value}]))}cards.forEach(c=>{const v=old[c.dataset.key];if(v){c.querySelector('select').value=v.choice;c.querySelector('textarea').value=v.note||''}c.oninput=()=>localStorage.setItem('FiveRivers_R13',JSON.stringify(vals()))});document.querySelectorAll('audio').forEach(a=>a.onplay=()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()}));document.getElementById('export').onclick=()=>{let a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(vals(),null,2)],{type:'application/json'}));a.download='R13_후보판정.json';a.click()};</script></html>'''
(OUT/'비교청취.html').write_text(page.replace('NUM',str(len(pending))),encoding='utf-8')
for dirname,zname in [('Delivery','FiveRivers_Approved19_Delivery.zip'),('Approved_Originals','FiveRivers_Approved19_Originals.zip')]:
    with zipfile.ZipFile(OUT/zname,'w',zipfile.ZIP_DEFLATED) as z:
        for p in (OUT/dirname).rglob('*'):
            if p.is_file():z.write(p,p.relative_to(OUT/dirname))
    with zipfile.ZipFile(OUT/zname) as z:
        assert z.testzip() is None and len([n for n in z.namelist() if n.endswith('.wav')])==19
assert all(sha(Path(p))==h for p,h in protected.items()),'Protected source changed'
(OUT/'보존검증.json').write_text(json.dumps(dict(protected_files=len(protected),all_unchanged=True,existing_game_audio_and_meta_checked=len(game_files),game_connection_written=False,approved=19,pending=len(pending)),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(output=str(OUT),approved=19,pending=len(pending),protected=len(protected),game_files=len(game_files)),ensure_ascii=False))
