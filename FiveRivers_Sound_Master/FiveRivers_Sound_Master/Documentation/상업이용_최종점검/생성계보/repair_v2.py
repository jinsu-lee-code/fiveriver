from pathlib import Path
import sys,json,hashlib,html,zipfile,shutil
sys.path.insert(0,str(Path(__file__).parent/'deps'))
import numpy as np,soundfile as sf,librosa
from scipy.signal import butter,sosfiltfilt
src=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_20261027')
dst=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027')
dst.mkdir(exist_ok=True)
rows=json.loads((src/'A_50_Status.json').read_text(encoding='utf-8'))
audit=[];cards=[];before={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in src.rglob('*.wav')}
def db(a):return float(20*np.log10(max(np.sqrt(np.mean(a*a)),1e-12)))
for row in rows:
    if row['filename'].startswith('VO_'):continue
    name=row['filename'];p=src/row['output_file'];x,sr=sf.read(p,always_2d=True);n=len(x);edits=[];loop='_Loop' in name;music=name.startswith('BGM_')
    prior=x.copy()
    if name in ('SFX_Bullet_Whiz_02.wav','SFX_Tank_Ricochet_01.wav','SFX_Bazooka_Load.wav'):
        raw,rs=sf.read(src/'Source'/name,always_2d=True);assert rs==sr
        active=np.flatnonzero(np.max(abs(raw),axis=1)>max(.005,np.max(abs(raw))*.0125))
        raw=raw[max(0,active[0]-96):min(len(raw),active[-1]+int(.02*sr))]
        rate=len(raw)/max(1,n-96)
        x=librosa.effects.time_stretch(raw.T,rate=rate,n_fft=1024,hop_length=256).T
        x=np.pad(x,((0,max(0,n-len(x))),(0,0)))[:n]
        edits.append(f'원본 이벤트 전체를 피치 유지 시간 압축({rate:.3f}배); 인위적 늘어짐/위상감 청취 확인 필요')
    dc=float(np.max(abs(x.mean(axis=0))))
    if loop:
        if dc>1e-4:x-=x.mean(axis=0);edits.append('루프 평균 DC 제거')
    elif dc>.001:
        x=sosfiltfilt(butter(2,12,fs=sr,btype='highpass',output='sos'),x,axis=0)
        edits.append('12Hz 고역통과로 DC/초저역 치우침 감소')
    if not loop and not music:
        last50=db(x[-2400:])
        if last50>-45 or name in ('SFX_Bullet_Whiz_02.wav','SFX_Tank_Ricochet_01.wav','SFX_Bazooka_Load.wav'):
            f=min(int(sr*.10),n//4);x[-f:]*=(.5+.5*np.cos(np.linspace(0,np.pi,f)))[:,None]
            edits.append(f'후단 {f/sr*1000:.0f}ms 코사인 감쇠')
        x[:24]*=np.linspace(0,1,24)[:,None]
        a=np.flatnonzero(np.max(abs(x),axis=1)>1e-3)
        trim=max(0,int(a[0])-96) if len(a) else 0
        if trim:x=np.pad(x[trim:],((0,trim),(0,0)))
        x[-1]=0
    if loop:
        x[-96:]-=np.linspace(0,1,96)[:,None]*(x[-1]-x[0])
    x*=min(1.,10**(-1.05/20)/max(np.max(abs(x)),1e-12))
    out=dst/'Audio'/row['target_folder']/name;out.parent.mkdir(parents=True,exist_ok=True);sf.write(out,x,sr,subtype='PCM_16')
    y,rr=sf.read(out,always_2d=True);info=sf.info(out);active=np.flatnonzero(np.max(abs(y),axis=1)>1e-3)
    lead=float(active[0]/sr*1000) if len(active) else 1e9
    peak=float(20*np.log10(max(np.max(abs(y)),1e-12)))
    assert rr==48000 and info.subtype=='PCM_16' and len(y)==n and peak<=-1 and np.max(abs(y))>0
    assert music or loop or lead<=10
    record={'file':name,'edits':edits,'seconds':len(y)/sr,'channels':y.shape[1],'peak_dbfs':peak,'leading_ms':lead,'before_end50_dbfs':db(prior[-2400:]),'after_end50_dbfs':db(y[-2400:]),'before_dc':float(np.max(abs(prior.mean(axis=0)))),'after_dc':float(np.max(abs(y.mean(axis=0)))),'numeric_checks_pass':True,'auditory_review':'NOT_PERFORMED','game_review':'NOT_PERFORMED','sha256':hashlib.sha256(out.read_bytes()).hexdigest()}
    repeatlink=''
    if loop:
        rep=dst/'Loop_10x'/(out.stem+'_10x.wav');rep.parent.mkdir(exist_ok=True)
        sf.write(rep,np.tile(y,(10,1)),sr,subtype='PCM_16')
        repeat,rr=sf.read(rep,always_2d=True)
        joins=[float(np.max(abs(repeat[k*n]-repeat[k*n-1]))) for k in range(1,10)]
        assert len(repeat)==10*n and all(j<1/32768 for j in joins)
        assert all(np.array_equal(repeat[k*n:(k+1)*n],y) for k in range(10))
        record.update(repeat_count=10,join_count=9,join_sample_jumps=joins,ten_repeat_listening='NOT_PERFORMED',ten_repeat_signal_check='PASS')
        repeatlink='<p>10회 반복 검수 파일</p><audio controls preload="none" src="'+str(rep.relative_to(dst)).replace('\\','/')+'"></audio>'
    audit.append(record)
    cards.append('<article><h2>'+html.escape(name)+'</h2><audio controls preload="none" src="'+str(out.relative_to(dst)).replace('\\','/')+'"></audio>'+repeatlink+'<p>'+html.escape('; '.join(edits) or '길이·피크·시작/끝 수치 재검사')+'</p><p>청취 판정: 미실시</p></article>')
assert len(audit)==41
for p,h in before.items():assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==h
(dst/'QA_41.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
(dst/'Original_Preserved.json').write_text(json.dumps(before,ensure_ascii=False,indent=2),encoding='utf-8')
(dst/'미리듣기_41개_루프10회.html').write_text('<!doctype html><html lang="ko"><meta charset="utf-8"><title>사운드 수정본 검수</title><style>body{font:16px sans-serif;background:#172028;color:#eee;max-width:1100px;margin:40px auto;padding:20px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:16px}article{padding:20px;background:#293640}h2{font-size:16px}audio{width:100%}p{line-height:1.6}</style><h1>A순위 41개 · 수정본 v2</h1><p>수치 검사와 실제 청취는 다릅니다. 이 묶음은 청취 최종 승인 전입니다. 루프는 각 10회 이어 붙인 파일을 포함합니다.</p><main>'+''.join(cards)+'</main>',encoding='utf-8')
(dst/'README_검수상태.md').write_text('''# A순위 41개 수정본 v2 — 청취 최종 승인 전

## 완료한 작업
41파일의 길이·채널·48kHz/16bit·피크·무음·끝부분·DC 재검사.
총알 스침02, 도탄01, 바주카 장전은 원본 전체 이벤트를 보존한 시간 압축으로 끝부분을 복구하고 감쇠 처리. 이 방식의 음질 변화는 청취 확인이 필요합니다.
DC가 큰 파일은 초저역 보정. 종료 직전 신호가 강한 파일은 더 긴 코사인 감쇠 적용.
루프8개는 각각10회 이어 붙인 WAV를 생성하고 총72개 접속 경계 및 반복 데이터 동일성을 검사했습니다. 이것은 10회 청취가 아닙니다.

## 미완료한 작업
이 환경에는 제가 WAV를 실제 청취하고 판단하는 도구가 없습니다. 잘못된 소리 내용, 총기 고증, 합성 티, 늘어짐, 과도한 반복감, 게임 재생/헤드폰 검수는 완료했다고 주장하지 않습니다. 최종 승인본으로 표시하지 않았습니다.
미리듣기 HTML에서41개와 루프10회 파일을 재생할 수 있습니다. QA_41.json에 각 파일 수정과 수치 결과를 기록했습니다.

## 보존 및 이동
기존 Unity 프로젝트, 기존 소리/메타/연결, 이전41개 묶음은 수정하지 않았습니다. Audio/Assets의 상대경로를 사용하되 기존파일 자동 덮어쓰기는 하지 마세요. 원본 생성 음원과 프롬프트는 이전 FiveRivers_Sound_A_20261027/Source 및 VARCO_Transcript.txt에 보존돼 있습니다.
''',encoding='utf-8-sig')
with zipfile.ZipFile(dst.with_suffix('.zip'),'w',zipfile.ZIP_DEFLATED) as z:
    for p in dst.rglob('*'):
        if p.is_file():z.write(p,p.relative_to(dst))
with zipfile.ZipFile(dst.with_suffix('.zip')) as z:assert z.testzip() is None
print(json.dumps({'files':len(audit),'specific_edits':{a['file']:a['edits'] for a in audit if a['edits']},'loop_files':sum('repeat_count' in a for a in audit),'archive_mb':round(dst.with_suffix('.zip').stat().st_size/1048576,1)},ensure_ascii=False))
