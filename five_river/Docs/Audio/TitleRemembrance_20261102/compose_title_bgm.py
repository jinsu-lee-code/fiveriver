"""Original title score: Remembrance (101.05 s / 32 bars / D minor / 76 BPM).
No borrowed melodies. GeneralUser GS sampled instruments, rendered by FluidSynth.
Run with Blender 5.2 bundled Python (requires numpy only).
"""
from pathlib import Path
import struct, math, json, random, subprocess, hashlib, wave
import numpy as np

ROOT = Path(__file__).resolve().parent
NAME = 'BGM_Title_Remembrance_Loop'
SR, BPM, PPQ = 48000, 76, 960
BEATS = 128
SECONDS = BEATS * 60 / BPM
RNG = random.Random(19501102)
TRACKS = [[] for _ in range(7)]
NOTE_LOG = []

def varlen(n):
    b = [n & 127]; n >>= 7
    while n: b.insert(0, 128 | (n & 127)); n >>= 7
    return bytes(b)

def event(track, beat, data, order=1):
    TRACKS[track].append((max(0, round(beat*PPQ)), order, bytes(data)))

def meta(track, beat, kind, body):
    event(track, beat, b'\xff'+bytes([kind])+varlen(len(body))+body, -3)

def cc(ch, beat, ctrl, val): event(ch+1, beat, [0xB0+ch,ctrl,max(0,min(127,round(val)))], -1)

def note(ch, beat, pitch, duration, velocity, human=True):
    # The same performed cycle repeats, including its subtle timing/velocity nuances.
    jitter = RNG.uniform(-.014,.014) if human else 0
    vel = max(1,min(118,round(velocity+(RNG.uniform(-3,3) if human else 0))))
    beat = max(0, beat+jitter)
    dur = duration * (RNG.uniform(.97,1.025) if human else 1)
    NOTE_LOG.append(dict(channel=ch,beat=round(beat,5),pitch=pitch,duration=round(dur,5),velocity=vel))
    for repeat in range(3):
        b = beat + repeat*BEATS
        event(ch+1,b,[0x90+ch,pitch,vel],2)
        event(ch+1,b+dur,[0x80+ch,pitch,0],0)

meta(0,0,0x03,b'Remembrance - original title score')
meta(0,0,0x51,round(60_000_000/BPM).to_bytes(3,'big'))
meta(0,0,0x58,bytes([4,2,24,8]))
meta(0,0,0x59,bytes([255,1])) # D minor
# 0-based GM programs: grand piano, grand piano, strings, viola, cello, contrabass.
channels=[('Piano - melody',0,89,60,37),('Piano - sparse left hand',0,77,51,32),
          ('Strings - breathing harmony',48,62,76,53),('Viola - inner voice',41,46,38,48),
          ('Cello - restrained counterline',42,67,53,47),('Contrabass - low foundation',43,44,65,40)]
for ch,(label,program,vol,pan,rev) in enumerate(channels):
    meta(ch+1,0,3,label.encode())
    event(ch+1,0,[0xC0+ch,program],-2)
    for c,v in [(7,vol),(10,pan),(91,rev),(93,0),(11,85)]: cc(ch,0,c,v)

# Root, open piano color, high string voicing. Slow harmonic rhythm; no percussion.
chords=[
(38,[50,57,64,65],[57,62,65,69]), # Dm(add9)
(34,[46,53,57,62],[57,62,65,69]), # Bbmaj7
(33,[45,53,60,67],[57,60,65,67]), # F(add9)/A
(36,[48,55,62,64],[55,60,62,67]), # Cadd9
(31,[43,50,57,58],[57,62,65,70]), # Gm9
(41,[53,57,62,64],[57,62,65,69]), # Dm/F
(34,[46,53,60,62],[58,60,65,69]), # Bbadd9
(33,[45,52,55,62],[57,62,64,67]), # A7sus
(38,[50,57,64,65],[57,62,65,69]),
(34,[46,53,57,62],[57,62,65,70]),
(33,[45,53,60,67],[57,60,65,69]),
(36,[48,55,62,64],[55,60,64,67]),
(31,[43,50,57,58],[57,62,65,70]),
(41,[53,57,62,64],[57,62,65,69]),
(40,[52,58,62,67],[58,62,64,67]), # Em7b5
(33,[45,52,55,61],[57,61,64,67]), # A7
(34,[46,53,57,62],[58,62,65,69]),
(33,[45,53,60,65],[57,60,65,69]),
(31,[43,50,57,58],[58,62,65,69]),
(41,[53,57,62,64],[57,62,65,69]),
(39,[51,58,62,67],[58,62,65,67]), # Ebmaj9: distant, quiet shadow
(38,[50,53,58,65],[58,62,65,69]), # Bb/D
(31,[43,50,57,58],[57,62,65,70]),
(33,[45,52,55,62],[57,62,64,67]),
(38,[50,57,64,65],[57,62,65,69]),
(34,[46,53,57,62],[57,62,65,69]),
(33,[45,53,60,67],[57,60,65,67]),
(36,[48,55,62,64],[55,60,62,67]),
(31,[43,50,57,58],[57,62,65,70]),
(33,[45,50,57,65],[57,62,65,69]), # Dm/A
(34,[46,53,60,62],[58,62,65,69]),
(33,[45,52,55,62],[57,62,64,67]),
]
melody=[
[(.65,69,.9),(2.05,74,1.5)],[(.15,72,1.05),(1.55,69,.8),(2.65,65,1.1)],
[(.4,69,1.25),(1.95,67,.75),(3.05,64,.75)],[(.4,67,1.6),(2.5,64,.7),(3.4,62,.5)],
[(.6,65,.7),(1.6,69,1.1),(3.1,70,.65)],[(.2,69,1.9),(2.65,65,1.05)],
[(.45,67,1.35),(2.3,65,.6),(3.1,62,.65)],[(.5,64,2.1),(3.2,61,.55)],
[(.45,69,.8),(1.65,74,1.05),(3.1,76,.65)],[(.35,77,1.3),(2.1,74,.85),(3.25,72,.55)],
[(.3,72,1.3),(2.0,69,1.5)],[(.7,67,.9),(2.15,64,1.1)],
[(.35,65,.7),(1.45,69,.9),(2.85,70,.9)],[(.4,69,1.5),(2.45,65,1.2)],
[(.45,67,1.1),(2.1,64,.75),(3.15,62,.55)],[(.45,61,.8),(1.8,64,1.6)],
[(.55,74,1.1),(2.2,77,1.25)],[(.35,76,1.35),(2.2,72,1.15)],
[(.4,74,1.15),(2.05,70,.7),(3.05,69,.65)],[(.5,69,1.3),(2.4,65,1.15)],
[(.5,67,1.2),(2.25,65,.65),(3.2,62,.55)],[(.6,65,1.6),(2.7,62,.9)],
[(.5,62,.9),(1.9,65,.9),(3.2,69,.55)],[(.5,67,1.3),(2.6,64,1.0)],
[(.65,69,.9),(2.05,74,1.4)],[(.35,72,1.05),(1.8,69,.8),(2.95,65,.7)],
[(.5,69,1.2),(2.1,67,.7),(3.2,64,.6)],[(.6,67,1.6),(2.8,64,.8)],
[(.6,65,1.05),(2.2,62,1.3)],[(.55,65,1.0),(2.1,64,.8),(3.2,62,.55)],
[(.5,65,1.35),(2.4,62,1.0)],[(.8,64,1.8)],
]
phrase=[.76]*8+[.88]*8+[1.0]*8+[.74,.72,.70,.68,.67,.65,.63,.61]
for bar,(root,keys,voicing) in enumerate(chords):
    b=bar*4; energy=phrase[bar]
    # Spacious three/four-note piano accompaniment with alternate patterns.
    patterns=[[(0,0,2.4),(1.45,1,1.5),(2.65,3,1.15)],
              [(.1,0,2.2),(1.25,2,1.8),(2.8,1,1.0)],
              [(0,0,2.0),(1.1,1,1.4),(2.5,2,1.2)],
              [(.15,0,2.2),(1.8,1,1.2),(3.0,3,.8)]]
    for off,idx,dur in patterns[bar%4]: note(1,b+off,keys[idx],dur,46*energy)
    for off,pitch,dur in melody[bar]: note(0,b+off,pitch,dur,63*energy)
    for repeat in range(3):
        bb=b+repeat*BEATS
        for ch in [0,1]:
            cc(ch,bb+.04,64,70 if ch==0 else 55); cc(ch,bb+3.83,64,0)
    # Ensemble bows: modest slow onset; upper voice enters only after 2 measures.
    if bar >= 2:
        for i,pitch in enumerate(voicing): note(2,b+.06+i*.018,pitch,3.68,42*energy,human=False)
    # Inner viola appears in the developing second half, never an ostinato.
    if 12 <= bar < 28:
        note(3,b+.32,voicing[1]-12,3.2,40*energy,human=False)
    # Bowed bass has long rests at the beginning/end of phrases.
    if bar%4 != 3: note(5,b+.08,root-12 if root>=38 else root,3.55,48*energy,human=False)
    # Cello is a countermelody, introduced as the title lingers.
    if 8 <= bar < 24:
        p=keys[0] if keys[0]>=43 else keys[0]+12
        note(4,b+.35,p,1.95,53*energy)
        if bar%2==0: note(4,b+2.75,keys[1],.95,47*energy)
    elif bar>=24 and bar%2==0: note(4,b+.45,keys[0],3.05,40*energy)
    # Smooth bow envelope through MIDI expression rather than volume jumps.
    for step in range(9):
        frac=step/8
        breath=math.sin(math.pi*frac)**.8
        for repeat in range(3):
            bb=b+frac*4+repeat*BEATS
            for ch,base,depth in [(2,39,32),(3,46,24),(4,60,19),(5,50,16)]:
                cc(ch,bb,11,(base+depth*breath)*(.85+.15*energy))

for tr in range(7):
    meta(tr,BEATS*3+4,0x2F,b'')
chunks=[]
for events in TRACKS:
    events.sort(key=lambda x:(x[0],x[1]))
    pos=0; data=bytearray()
    for tick,_,body in events:
        data.extend(varlen(tick-pos)); data.extend(body); pos=tick
    chunks.append(b'MTrk'+struct.pack('>I',len(data))+data)
midi=ROOT/(NAME+'.mid')
midi.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(chunks),PPQ)+b''.join(chunks))
(ROOT/'composition.json').write_text(json.dumps(dict(title=NAME,bpm=BPM,meter='4/4',key='D minor',bars=32,seconds=SECONDS,channels=channels,notes_one_cycle=NOTE_LOG,concept='Quiet remembrance, unresolved tension, original theme; no vocals/percussion/quoted melodies.'),indent=2),encoding='utf-8')

synth=next((ROOT/'tools').rglob('fluidsynth.exe'))
sf=ROOT/'sources'/'GeneralUser-GS.sf2'
raw=ROOT/'render_three_cycles_float.wav'
args=[str(synth),'-ni','-q','-r',str(SR),'-g','0.32','-C','0','-R','1',
      '-o','synth.reverb.room-size=0.67','-o','synth.reverb.damp=0.52',
      '-o','synth.reverb.width=72','-o','synth.reverb.level=0.28',
      '-o','synth.cpu-cores=1','-o','synth.polyphony=256',
      '-T','wav','-O','float','-F',str(raw),str(sf),str(midi)]
print('Rendering original sampled score...',flush=True)
subprocess.run(args,check=True)
print('MIDI and raw rendering complete.',flush=True)

# Keep the rendering performance and a convenient one-cycle MIDI separately.
(ROOT/'render_three_cycles.mid').write_bytes(midi.read_bytes())
one_chunks=[]
for tr,events in enumerate(TRACKS):
    ev=[e for e in events if e[0] < BEATS*PPQ and not e[2].startswith(b'\xff\x2f')]
    ev.append((BEATS*PPQ,3,b'\xff\x2f\x00'))
    ev.sort(key=lambda x:(x[0],x[1])); pos=0; data=bytearray()
    for tick,_,body in ev: data.extend(varlen(tick-pos)); data.extend(body); pos=tick
    one_chunks.append(b'MTrk'+struct.pack('>I',len(data))+data)
midi.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(one_chunks),PPQ)+b''.join(one_chunks))
print('One-cycle MIDI saved.',flush=True)
