from pathlib import Path
import sys,json,math,csv,hashlib
sys.path.insert(0,str(Path(__file__).parent/'deps'))
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly
root=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_20261027')
jobs=json.loads((root/'A_50_Status.json').read_text(encoding='utf-8'))
mapping=json.loads((root/'Source_Mapping.json').read_text(encoding='utf-8')) if (root/'Source_Mapping.json').exists() else {'SFX_M1_Fire_01.wav':'M1_shot_original.wav'}
def extend_loop(a,n,overlap):
    base=a.copy();ramp=np.linspace(0,1,overlap)[:,None]
    while len(a)<n:
        a=np.concatenate((a[:-overlap],a[-overlap:]*(1-ramp)+base[:overlap]*ramp,base[overlap:]))
    return a[:n]
for r in jobs:
    name=r['filename']
    if name not in mapping:continue
    src=root/'Source'/mapping[name]
    x,rate=sf.read(src,always_2d=True)
    import re
    seconds=float(re.search(r'(\d+(?:\.\d+)?)초',r['specification']).group(1))
    channels=2 if '스테레오' in r['specification'] else 1
    stereo_note=''
    if channels==1:x=x.mean(axis=1,keepdims=True)
    if channels==2 and x.shape[1]!=2:
        side=root/'Source'/name.replace('_Loop.wav','_Side.wav')
        if name.startswith('AMB_') and side.exists():
            y,rr=sf.read(side,always_2d=True);y=y.mean(axis=1,keepdims=True)
            gg=math.gcd(rr,rate);y=resample_poly(y,rate//gg,rr//gg,axis=0)
            size=int((seconds+.5)*rate)
            a=extend_loop(x,size,min(int(.5*rate),len(x)//5));b=extend_loop(y,size,min(int(.5*rate),len(y)//5))
            x=np.column_stack((.8*a[:,0]+.2*b[:,0],.2*a[:,0]+.8*b[:,0]))
            stereo_note='독립 생성 후보1·2를 L/R 교차 배치한 스테레오 편집; 단일 실녹음 스테레오 아님'
        else:
            r['status']='원본 확보 / 실제 스테레오 제작 필요';continue
    g=math.gcd(rate,48000);x=resample_poly(x,48000//g,rate//g,axis=0)
    edits=[stereo_note] if stereo_note else []
    # Assemble the three separated handling transients into one compact reload cue.
    if name=='SFX_M1_ClipInsert.wav':
        pieces=[]
        for a,b in [(2.98,3.28),(4.57,4.88),(4.91,5.30)]:
            z=x[int(a*48000):int(b*48000)].copy()
            f=min(240,len(z)//10);z[:f]*=np.linspace(0,1,f)[:,None];z[-f:]*=np.linspace(1,0,f)[:,None]
            pieces.append(z)
        x=np.concatenate(pieces);edits.append('분리된 장전 동작 3구간 결합, 무음 간격 축소; 동작 순서 청취 검수 필요')
    # Extract one event from generated multi-event takes without altering pitch.
    if name.startswith(('SFX_Bullet_Whiz','SFX_Impact_Dirt','SFX_Player_Hit','SFX_Foot_Dirt')):
        frame=240
        env=np.array([np.sqrt(np.mean(x[i:i+frame]**2)) for i in range(0,len(x),frame)])
        hits=np.flatnonzero(env>max(.012,float(env.max())*.12))
        if len(hits):x=x[max(0,int(hits[0]*frame)-96):]
        edits.append('반복 테이크에서 첫 단발 이벤트 추출, 후단 5ms fade')
    # Trim only low-level leading silence; preserve 2 ms before detected onset.
    active=np.flatnonzero(np.max(np.abs(x),axis=1)>10**(-60/20))
    if not len(active):r['status']='무음 결과 거부';continue
    if '_Loop' not in name and not name.startswith('BGM_'):x=x[max(0,int(active[0])-96):]
    n=int(round(seconds*48000))
    if '_Loop' in name:
        overlap=min(24000,len(x)//5)
        if len(x)<n+overlap:
            unit=x.copy()
            while len(x)<n+overlap:
                ramp=np.linspace(0,1,overlap)[:,None]
                x=np.concatenate((x[:-overlap],x[-overlap:]*(1-ramp)+unit[:overlap]*ramp,unit[overlap:]))
            edits.append('원본 루프를 겹쳐 요청 길이 구성')
        x=x[:n+overlap].copy();head=x[:overlap].copy();x=x[overlap:]
        ramp=np.linspace(0,1,overlap)[:,None];x[-overlap:]=x[-overlap:]*(1-ramp)+head*ramp
        edits.append('순환 경계 크로스페이드; 10회 반복 청취 필요')
    if len(x)<n:
        if '_Loop' in name or name.startswith(('AMB_','BGM_')):
            r['status']='원본 확보 / 요청 길이보다 짧음';continue
        # A one-shot may finish before its delivery window. Pad only the end.
        x=np.pad(x,((0,n-len(x)),(0,0)))
    elif '_Loop' not in name and not name.startswith('BGM_'):
        tail=x[n:]
        if len(tail) and np.max(np.abs(tail))>10**(-30/20):
            edits.append('요청 길이로 편집; 후단 잘림·잔향 청취 검수 필요')
    x=x[:n].copy()
    if '_Loop' in name:
        seam=96
        x[-seam:]-=np.linspace(0,1,seam)[:,None]*(x[-1]-x[0])
        edits.append('마지막 2ms 경계 보정')
    if name=='BGM_Ending.wav':
        f=3*48000;x[:f]*=np.linspace(0,1,f)[:,None];x[-f:]*=np.linspace(1,0,f)[:,None]
        edits.append('앞뒤 3초 fade; 편지 구간 음량은 엔진에서 조정')
    if name=='SFX_Tank_EngineStall.wav':
        f=int(.6*48000);x[-f:]*=np.linspace(1,0,f)[:,None]**2
        edits.append('마지막 0.6초 감쇠로 완전 정지')
    if '_Loop' not in name:
        fade=min(240,len(x)//20);x[-fade:]*=np.linspace(1,0,fade)[:,None]
        if not name.startswith('BGM_'):
            active2=np.flatnonzero(np.max(np.abs(x),axis=1)>10**(-60/20))
            trim=max(0,int(active2[0])-96) if len(active2) else 0
            if trim:x=np.pad(x[trim:],((0,trim),(0,0)))
    x*=min(1.,10**(-1.01/20)/max(np.max(np.abs(x)),1e-12))
    dest=root/'Review_Audio'/r['target_folder']/name;dest.parent.mkdir(parents=True,exist_ok=True)
    sf.write(dest,x,48000,subtype='PCM_16')
    y,sr=sf.read(dest,always_2d=True);info=sf.info(dest)
    peak=20*np.log10(max(np.max(np.abs(y)),1e-12))
    assert sr==48000 and info.subtype=='PCM_16' and info.channels==channels and abs(info.duration-seconds)<1/48000 and peak<=-1
    r.update(status='규격 검증 / 청취·게임 검수 대기',edits='; '.join(edits),source_file=str(src.relative_to(root)),output_file=str(dest.relative_to(root)),sample_peak_dbfs=round(float(peak),4),sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),final_audio_approved=False)
(root/'A_50_Status.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2),encoding='utf-8')
keys=list(dict.fromkeys(k for row in jobs for k in row))
with (root/'A_50_Status.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=keys);w.writeheader();w.writerows(jobs)
print(json.dumps({r['filename']:r['status'] for r in jobs if r['filename'] in mapping},ensure_ascii=False))
