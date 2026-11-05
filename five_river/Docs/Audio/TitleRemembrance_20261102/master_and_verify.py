"""Master and verify the original title loop. numpy only; outputs PCM16 stereo.
Reads three rendered cycles to preserve the reverb tail across the loop boundary.
"""
from pathlib import Path
import struct, wave, json, hashlib, math
import numpy as np
ROOT=Path(__file__).resolve().parent
NAME='BGM_Title_Remembrance_Loop'; SR=48000; BPM=76
rng=np.random.default_rng(19501102)

def read_float_wave(path):
    with open(path,'rb') as f:
        assert f.read(4)==b'RIFF'; f.read(4); assert f.read(4)==b'WAVE'
        fmt=None
        while True:
            h=f.read(8)
            if len(h)<8: raise ValueError('No data chunk')
            tag,n=struct.unpack('<4sI',h)
            if tag==b'fmt ': fmt=struct.unpack('<HHIIHH',f.read(n)[:16])
            elif tag==b'data':
                assert fmt[0]==3 and fmt[1]==2 and fmt[2]==SR and fmt[-1]==32,fmt
                return np.frombuffer(f.read(n),dtype='<f4').reshape(-1,2).astype(np.float64)
            else: f.seek(n+n%2,1)

def write_pcm16(path,a):
    # TPDF dither remains well below the music floor.
    dither=(rng.random(a.shape)-rng.random(a.shape))/65536
    pcm=np.rint(np.clip(a+dither,-1,1)*32767).astype('<i2')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    return pcm.astype(np.float64)/32768

def db(v): return float(20*np.log10(max(float(v),1e-15)))

def truepeak4(a):
    peak=float(abs(a).max())
    idx=np.arange(-24,25)
    for phase in [.25,.5,.75]:
        kernel=np.sinc(idx-phase)*np.kaiser(49,8.6); kernel/=kernel.sum()
        for ch in range(2): peak=max(peak,float(abs(np.convolve(a[:,ch],kernel,mode='valid')).max()))
    return peak

raw=read_float_wave(ROOT/'render_three_cycles_float.wav')
cycle=128*60/BPM
start=round(cycle*SR); n=round(cycle*SR)
a=raw[start:start+n].copy(); del raw
before=a[[0,-1]].copy()
# Correct the tiny sample-phase mismatch over 10.7 ms at both ends.
# Match a natural adjacent-sample slope, rather than inserting silence/fade-outs.
edge=512
slope=((a[4]-a[0])/4+(a[-1]-a[-5])/4)/2
center=(a[0]+a[-1])/2
target_first=center+slope/2; target_last=center-slope/2
shape=.5*(1+np.cos(np.linspace(0,np.pi,edge)))
a[:edge]+=(target_first-a[0])[None,:]*shape[:,None]
a[-edge:]+=(target_last-a[-1])[None,:]*shape[::-1,None]
a-=a.mean(axis=0)
# Preserve the performed dynamics; no brickwall limiting/compression.
pre_true=truepeak4(a)
gain=10**(-3/20)/pre_true
a*=gain
out=ROOT/(NAME+'.wav')
a=write_pcm16(out,a)
# The preview includes the developing theme; tapered ends are preview-only.
preview=a[round(34*SR):round(54*SR)].copy(); fade=round(.45*SR)
preview[:fade]*=np.linspace(0,1,fade)[:,None]; preview[-fade:]*=np.linspace(1,0,fade)[:,None]
write_pcm16(ROOT/(NAME+'_Preview20s.wav'),preview)
seam=np.concatenate([a[-6*SR:],a[:6*SR]],axis=0)
write_pcm16(ROOT/(NAME+'_LoopBoundary12s.wav'),seam)
peak=float(abs(a).max()); rms=float(np.sqrt(np.mean(a*a))); tp=truepeak4(a)
steps=np.abs(np.diff(a,axis=0)); boundary=np.abs(a[0]-a[-1]); perbar=[]
for bar in range(32):
    x=a[round(bar*4*60/BPM*SR):round((bar+1)*4*60/BPM*SR)]
    perbar.append(round(db(np.sqrt(np.mean(x*x))),3))
left,right=a[:,0],a[:,1]
measurements={
    'name':NAME,'sample_rate_hz':SR,'channels':2,'pcm_bits':16,'frames':len(a),'duration_seconds':len(a)/SR,
    'bpm':BPM,'bars':32,'meter':'4/4','key':'D minor','peak_dbfs':db(peak),'rms_dbfs':db(rms),
    'true_peak_4x_estimate_dbtp':db(tp),'true_peak_method':'49-tap Kaiser-windowed sinc at 0.25/0.5/0.75 sample phases; estimate',
    'sample_clip_count':int(np.count_nonzero(abs(a)>=32767/32768)),
    'intersample_clip_estimated':bool(tp>=1),'dc_offset_L_R':a.mean(axis=0).tolist(),
    'stereo_correlation':float(np.corrcoef(left,right)[0,1]),
    'rms_dbfs_L_R':[db(np.sqrt(np.mean(left*left))),db(np.sqrt(np.mean(right*right)))],
    'loop_boundary_delta_linear_L_R':boundary.tolist(),'loop_boundary_delta_dbfs_L_R':[db(v) for v in boundary],
    'typical_adjacent_delta_99_9pct_L_R':np.quantile(steps,.999,axis=0).tolist(),
    'loop_boundary_vs_99_9pct_L_R':(boundary/np.maximum(np.quantile(steps,.999,axis=0),1e-12)).tolist(),
    'edge_correction_samples_each_end':edge,'edge_correction_ms':edge/SR*1000,
    'silence_longest_ms_below_minus80_dbfs':None,
    'per_bar_rms_dbfs':perbar,'render_master_gain_db':db(gain),
    'direct_listening_performed':False,
    'listening_note':'No direct human/auditory playback verification was available. Numeric checks do not certify musical taste or absence of every audible artifact.',
    'recommended_unity_import':{'load_type':'Decompress On Load','compression':'PCM or Vorbis quality >= 80','sample_rate':'Preserve 48000 Hz','force_to_mono':False,'loop':True,'start_fade_seconds':1.5},
    'source_sha256':hashlib.sha256((ROOT/'sources'/'GeneralUser-GS.sf2').read_bytes()).hexdigest(),
    'wav_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),
}
# Detect sustained near-silence in 10 ms windows (dither excluded by threshold).
block=480; count=len(a)//block
levels=np.max(np.abs(a[:count*block].reshape(count,block,2)),axis=(1,2))
longest=run=0
for silent in levels<1e-4:
    run=run+1 if silent else 0; longest=max(longest,run)
measurements['silence_longest_ms_below_minus80_dbfs']=longest*10
(ROOT/'audio_measurements.json').write_text(json.dumps(measurements,indent=2),encoding='utf-8')
print(json.dumps({k:measurements[k] for k in ['duration_seconds','peak_dbfs','rms_dbfs','true_peak_4x_estimate_dbtp','sample_clip_count','intersample_clip_estimated','stereo_correlation','loop_boundary_delta_dbfs_L_R','loop_boundary_vs_99_9pct_L_R','silence_longest_ms_below_minus80_dbfs','wav_sha256']},indent=2))
