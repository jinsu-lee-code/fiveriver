# T-34 running gear (2026-11-12): finds the track belt's centre line on SM_VH_T34_85 and writes what
# TankRunningGear.cs needs to turn the wheels and run the belt (Resources/Vehicles/T34_RunningGear.json).
#   1. Unity editor open on C:/pf/five_river:  python ../../animation/tank_entrance/compare/unity_run.py dump_tracks.cs
#      (writes TrackL/TrackR vertices in tank-body space + triangles to C:/sk_assets/capture/gear/)
#   2. python gear.py      -> T34_RunningGear.json + TrackL_class.png / TrackR_class.png (side view: grey static,
#      green/orange wheels, blue belt, red centre line) in the same capture folder to look at
# Wheel centres and radii (WHEELS) were fitted to the rim circles of the outer wheel faces and are fixed here.
# The belt has no repeating link pitch in its geometry (generated mesh), so the game runs it round the whole loop.
import os
os.chdir('C:/sk_assets/capture/gear')
import numpy as np, json, sys
from PIL import Image, ImageDraw
# (z, y, radius of the disc that turns, radius the belt runs on) in tank-body space, metres. Sprocket, 4 road wheels, idler.
RIM=0.497   # road wheel rim; the disc that turns stops short of it (TURN) so it never takes a piece of the belt it stands on
TURN=0.465
WHEELS=[(-2.24,0.80,0.215,0.30),(-1.646,0.568,TURN,RIM),(-0.672,0.568,TURN,RIM),(0.334,0.570,TURN,RIM),(1.318,0.569,TURN,RIM),(2.13,0.765,0.285,0.34)]
NS=256
def load(n,sg):
    a=np.fromfile(n+'.bin',dtype=np.float32).reshape(-1,5)
    t=np.fromfile(n+'.tri',dtype=np.int32).reshape(-1,3)
    return sg*a[:,0],a[:,1],a[:,2],t
def spike(y,z): return (z>2.2)&(y>1.19-1.38*(z-2.3))
def resample(p,n):
    q=np.vstack([p,p[:1]]); seg=np.hypot(*(q[1:]-q[:-1]).T); c=np.r_[0,np.cumsum(seg)]
    u=np.linspace(0,c[-1],n,endpoint=False)
    return np.c_[np.interp(u,c,q[:,0]),np.interp(u,c,q[:,1])]
def smooth(p,k):
    for _ in range(k): p=(np.roll(p,1,0)+2*p+np.roll(p,-1,0))/4
    return p
def project(p,z,y):
    # nearest point on the closed polyline p: arc length s, signed distance n (+ = outward), per point
    q=np.vstack([p,p[:1]]); d=q[1:]-q[:-1]; L=np.hypot(d[:,0],d[:,1]); c=np.r_[0,np.cumsum(L)]
    best=np.full(len(z),1e9); s=np.zeros(len(z)); nn=np.zeros(len(z))
    for i in range(len(p)):
        rz=z-q[i,0]; ry=y-q[i,1]
        u=np.clip((rz*d[i,0]+ry*d[i,1])/(L[i]**2),0,1)
        ez=rz-u*d[i,0]; ey=ry-u*d[i,1]; dist=np.hypot(ez,ey)
        k=dist<best
        best[k]=dist[k]; s[k]=c[i]+u[k]*L[i]
        nn[k]=(ez[k]*d[i,1]-ey[k]*d[i,0])/L[i]     # cross: sign fixed below
    return s,nn,c[-1]
def loop_of(x,y,z):
    m=(x>1.40)&~spike(y,z)
    for (wz,wy,wr,rr) in WHEELS: m&=np.hypot(z-wz,y-wy)>max(wr,rr if wr==TURN else 0)+0.012
    C=(0.0,0.6); ang=np.arctan2(y-C[1],z-C[0]); rad=np.hypot(z-C[0],y-C[1])
    NB=360; b=((ang+np.pi)/(2*np.pi)*NB).astype(int)%NB
    r=np.full(NB,np.nan)
    for i in range(NB):
        k=m&(b==i)
        if k.sum()<6: continue
        ro=np.percentile(rad[k],95); kk=k&(rad>ro-0.07)
        r[i]=(np.percentile(rad[kk],5)+ro)/2
    idx=np.arange(NB); ok=~np.isnan(r)
    r=np.interp(idx,np.r_[idx[ok]-NB,idx[ok],idx[ok]+NB],np.r_[r[ok],r[ok],r[ok]])
    a=-np.pi+(idx+0.5)/NB*2*np.pi
    p=np.c_[C[0]+r*np.cos(a),C[1]+r*np.sin(a)]      # counter-clockwise seen with z right, y up
    p=smooth(resample(p,NS),3)
    for it in range(3):
        s,n,L=project(p,z[m],y[m])
        k=np.abs(n)<0.07
        bins=(s[k]/L*NS).astype(int)%NS
        off=np.zeros(NS)
        for i in range(NS):
            v=n[k][bins==i]
            if len(v)>=6: off[i]=(np.percentile(v,5)+np.percentile(v,95))/2
        off=smooth(off[:,None],2)[:,0]
        t=np.roll(p,-1,0)-np.roll(p,1,0); t/=np.hypot(t[:,0],t[:,1])[:,None]
        p=p+np.c_[t[:,1],-t[:,0]]*off[:,None]
        p=smooth(resample(p,NS),2)
    return p
def classify(x,y,z,t,p):
    cx=x[t].mean(1); cy=y[t].mean(1); cz=z[t].mean(1)
    g=np.zeros(len(t),int)          # 0 static, 1..6 wheel, 7 belt
    s,n,L=project(p,cz,cy)
    for i,(wz,wy,wr,_) in enumerate(WHEELS):
        g[(np.hypot(cz-wz,cy-wy)<wr)&(cx>1.07)]=i+1
    g[(n<BELT_HALF)&(n>-BELT_INSIDE)&(cx>0.97)]=7      # the belt wins where a wheel stands on it
    return g
BELT_HALF=0.055
BELT_INSIDE=0.047
if __name__=='__main__':
    out={}
    for n,sg in (('TrackL',-1),('TrackR',1)):
        x,y,z,t=load(n,sg)
        p=loop_of(x,y,z)
        s,nn,L=project(p,z,y)
        g=classify(x,y,z,t,p)
        print(n,'loop length %.3f'%L,'tris static/wheels/belt',[(g==i).sum() for i in range(8)])
        # histogram of signed distance for points outside wheels, outer slice
        m=(x>1.4)&~spike(y,z)
        for (wz,wy,wr,rr) in WHEELS: m&=np.hypot(z-wz,y-wy)>max(wr,rr if wr==TURN else 0)+0.012
        h,e=np.histogram(nn[m],bins=np.arange(-0.12,0.121,0.01)); print(' n hist',list(zip(np.round(e[:-1],2),h)))
        out[n]={'length':round(float(L),4),'loop':[round(float(v),4) for v in p.flatten()]}
        S=400;W=int(5.3*S);H=int(1.3*S)
        im=Image.new('RGB',(W,H));pix=np.zeros((H,W,3),np.uint8)
        col=np.array([(90,90,90)]+[(60,200,60),(230,160,40)]*3+[(70,140,255)])
        order=np.argsort(x[t].mean(1))
        for k in order[::1]:
            for v in t[k]:
                pix[H-1-int(y[v]*S),int((z[v]+2.65)*S)]=col[g[k]]
        im=Image.fromarray(pix);d=ImageDraw.Draw(im)
        for q in p: px=(q[0]+2.65)*S;py=H-1-q[1]*S; d.point((px,py),fill=(255,0,0))
        im.save(n+'_class.png')
    out['wheels']=[v for w in WHEELS for v in w]; out['beltHalf']=BELT_HALF; out['beltInside']=BELT_INSIDE; out['wheelInner']=1.07; out['beltInner']=0.97
    json.dump(out,open('C:/pf/five_river/Assets/_Project/Art/Resources/Vehicles/T34_RunningGear.json','w'))
