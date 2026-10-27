from pathlib import Path
import json,hashlib,os
os.environ['HF_HUB_OFFLINE']='1'
import numpy as np,soundfile as sf
from scipy.signal import welch,resample_poly
R=Path(r'C:\Users\mbc\Desktop\FiveRivers_Sound_A_Repair_v2_20261027');O=R/'T34_장시간루프_R12';O.mkdir(exist_ok=True)
for d in ['Audio','Long_Listen']:(O/d).mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
dirs=['사용자합격_최종본','게임용편집_R6','실물비교_메모반영_R7','추가수정_R5','추가보정_R8','T34_저음보정_R10','T34_반복보정_R11']
baseline={str(p):sha(p) for d in dirs for p in (R/d).rglob('*') if p.is_file()}
src=R/'T34_반복보정_R11/Audio/R11_T34_Engine_A.wav';x,sr=sf.read(src);assert x.ndim==1 and sr==48000
rng=np.random.default_rng(12726);N=48*sr;fade=int(.48*sr)
# Irregular segment positions sum to 48s; source segments keep original pitch and speed.
hops=rng.uniform(3.2,4.8,12);hops=hops/hops.sum()*N;hops=np.round(hops).astype(int);hops[-1]+=N-hops.sum();starts=np.r_[0,np.cumsum(hops)[:-1]]
offsets=rng.integers(0,len(x),12)
y=np.zeros(N);weights=np.zeros(N);records=[]
for i,(start,hop,offset) in enumerate(zip(starts,hops,offsets)):
 size=hop+fade;idx=(np.arange(size)+offset)%len(x);grain=x[idx].copy()
 # Modest whole-grain gain differences; no added oscillators or reference samples.
 gain_db=float(rng.uniform(-.25,.25));grain*=10**(gain_db/20)
 w=np.ones(size);ramp=.5-.5*np.cos(np.linspace(0,np.pi,fade));w[:fade]=ramp;w[-fade:]=1-ramp
 target=(start+np.arange(size))%N;y[target]+=grain*w;weights[target]+=w*w
 records.append({'start':start/sr,'hop':hop/sr,'source_offset':int(offset)/sr,'gain_db':gain_db})
y/=np.sqrt(np.maximum(weights,1e-12))
# Crossfade correlation compensation from circular 100ms-smoothed energy, capped at 1dB.
from scipy.ndimage import gaussian_filter1d
block=240;energy=np.mean(y.reshape(-1,block)**2,axis=1);smooth=np.sqrt(gaussian_filter1d(energy,20,mode='wrap'))
gain_db=np.clip(.65*20*np.log10(np.sqrt(np.mean(x*x))/np.maximum(smooth,1e-9)),-1,1)
centers=(np.arange(len(gain_db))+.5)*block;interp=np.interp(np.arange(N),np.r_[centers[-1]-N,centers,centers[0]+N],np.r_[gain_db[-1],gain_db,gain_db[0]])
y*=10**(interp/20);y*=np.sqrt(np.mean(x*x)/np.mean(y*y))
# Move the file boundary to a low-amplitude, low-step point. This is a circular shift, not an edit to the waveform.
seam_candidates=np.arange(1,int(.1*sr));seam_cost=abs(y[seam_candidates]-y[seam_candidates-1])+0.1*abs(y[seam_candidates]);shift=int(seam_candidates[np.argmin(seam_cost)]);y=np.roll(y,-shift)
sf.write(O/'Audio/R12_T34_Engine_A_Long48s.wav',y,sr,subtype='PCM_24');y,_=sf.read(O/'Audio/R12_T34_Engine_A_Long48s.wav')
before=np.resize(x,N);sf.write(O/'Audio/Before_R11_A_48s.wav',before,sr,subtype='PCM_24')
# Exactly 480 seconds of each, no time stretching.
sf.write(O/'Long_Listen/Before_R11_A_8min.wav',np.resize(x,480*sr),sr,subtype='PCM_24')
sf.write(O/'Long_Listen/R12_A_10loops_8min.wav',np.tile(y,10),sr,subtype='PCM_24')
def corr(z,lag):return float(np.corrcoef(z[:-lag],z[lag:])[0,1])
def metrics(z):
 v=np.sqrt(np.mean(z[:len(z)//12000*12000].reshape(-1,12000)**2,axis=1))
 return {'seconds':len(z)/sr,'rms_db':float(20*np.log10(np.sqrt(np.mean(z*z)))),'true_peak_db':float(20*np.log10(abs(resample_poly(z,4,1)).max())),'quarter_second_range_db':float(20*np.log10(v.max()/v.min())),'original_period_correlation':corr(z,len(x)),'boundary_jump':float(abs(z[-1]-z[0])),'step_p95':float(np.percentile(abs(np.diff(z)),95))}
report={'source':str(src),'source_sha256':sha(src),'seed':12726,'segments':records,'circular_shift_seconds':shift/sr,'crossfade_seconds':fade/sr,'before':metrics(before),'source_loop_boundary_jump':float(abs(x[-1]-x[0])),'after':metrics(y),'protected_count':len(baseline),'changed':[p for p,h in baseline.items() if sha(Path(p))!=h],'limitations':'Same short source rearranged, not new real recording. 48-second periodicity remains. Before 48s is a comparison excerpt, not a loop asset. Correlation measures repeated waveform, not perceived naturalness. No human listening.'}
assert not report['changed'];assert report['after']['true_peak_db']<-1
(O/'장시간루프검사.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:report[k] for k in ['before','after','protected_count','changed']},indent=2),flush=True)
# Actual raw-audio CLAP comparison. Same 6.72s windows and RMS-normalized inputs.
import torch
from transformers import ClapModel,ClapProcessor
torch.set_num_threads(4);torch.manual_seed(0)
m=ClapModel.from_pretrained(R/'무료로컬분석_R9/LocalModel').eval();p=ClapProcessor.from_pretrained(R/'무료로컬분석_R9/LocalModel')
def emb(a):
 a=(a/(np.sqrt(np.mean(a*a))+1e-10)*.1).astype(np.float32);np.random.seed(0)
 with torch.inference_mode():e=m.get_audio_features(**p(audio=[a],sampling_rate=sr,return_tensors='pt'))
 return e[0]/e[0].norm()
def spec(a):
 f,v=welch(a,sr,nperseg=8192);edges=[30,60,120,250,500,1000,2000,4000,8000];a=np.array([v[(f>=l)&(f<h)].sum() for l,h in zip(edges,edges[1:])]);return a/a.sum()
eb=emb(x);segments=[y[t*sr:t*sr+len(x)] for t in [0,7,14,21,28,35,41]];ee=[emb(a) for a in segments];rows=[]
for vid,t in [('0v5zdE21gXU',30),('0v5zdE21gXU',45),('nQM4xqCdgSw',155),('nQM4xqCdgSw',160)]:
 ref,s=sf.read(R/'무료로컬분석_R9/Reference_AnalysisOnly'/f'{vid}.wav');ref=ref[t*sr:t*sr+len(x)];e=emb(ref);scores=[float(a@e) for a in ee]
 rows.append({'video':vid,'start':t,'before_clap':float(eb@e),'after_clap_mean':float(np.mean(scores)),'after_clap_min':min(scores),'after_clap_max':max(scores),'after_segment_scores':scores})
(O/'실물재분석.json').write_text(json.dumps({'segment_starts':[0,7,14,21,28,35,41],'segment_length':len(x)/sr,'rows':rows,'note':'Cosines are not realism percentages. Before is one 6.72s source, after samples seven sections. No statistical significance claim.'},indent=2),encoding='utf-8');print(json.dumps(rows,indent=2),flush=True)
