# T-34 running gear (2026-11-12): finds the track belt's centre line on SM_VH_T34_85 and writes what
# TankRunningGear.cs needs to turn the wheels and run the belt (Resources/Vehicles/T34_RunningGear.json).
#   1. Unity editor open on C:/team/five_river:  python ../../animation/tank_entrance/compare/unity_run.py dump_tracks.cs
#      (writes TrackL/TrackR vertices in tank-body space + triangles to C:/sk_assets/capture/gear/)
#   2. python gear.py      -> T34_RunningGear.json + TrackL_class.png / TrackR_class.png (side view: grey static,
#      green/orange wheels, blue belt, red centre line) in the same capture folder to look at
# 2026-11-14: the generated mesh is uneven, so the same run also deals with that (texture and shape at rest stay):
#   loop    the centre line is fitted a second time at twice the resolution (refine), stretch by stretch onto the
#           belt's outer face. Before, it cut across dips and bumps of up to 3.7 cm, the belt's vertices kept that
#           as their offset from the line, and those places travelled round with the belt
#   wheels  the disc that turns ends short of the belt's band (the belt is sunk up to 3 cm into the tyres where it
#           lies on them, so a disc out to TURN lost pieces to the belt and turned with notches): each wheel's disc
#           radius is taken down to where the band begins on either side (discs)
#   core    where a wheel stands on the belt the mesh has no inner face (the two are fused), so once the belt runs
#           those places are holes one can see through. The game lays a core inside the belt all the way round
#           (a box section: coreIn..coreOut out from the line, coreX0..coreX1 across, just inside the belt's own
#           faces); it shows only through such holes. Its colour is one texel of the belt's usual colour
#           (beltFill, found on the base colour texture here)
#   tyre    per side, 4 road wheels x NTYRE directions: the factor that brings the outer tyre's outline back onto a
#           circle (radii beyond TYRE_FROM are scaled by it; TankRunningGear does that to the ring that stands still)
# Wheel centres and radii (WHEELS) were fitted to the rim circles of the outer wheel faces and are fixed here.
# The belt has no repeating link pitch in its geometry (generated mesh), so the game runs it round the whole loop.
import os
os.chdir('C:/sk_assets/capture/gear')
import numpy as np, json, sys
from PIL import Image, ImageDraw
# (z, y, radius of the disc that turns, radius the belt runs on) in tank-body space, metres. Sprocket, 4 road wheels, idler.
RIM=0.497   # road wheel rim; the disc that turns stops short of it (TURN) so it never takes a piece of the belt it stands on
TURN=0.445  # (0.465 until 2026-11-14) below TYRE_FROM by the game's seam overlap, so the part that turns is never rescaled
NBELT=512; NTYRE=72
TYRE_FROM=0.457             # radius where the tyre correction starts (flat rubber side wall, so it only slides in its own plane)
TYRE_X0=1.32; TYRE_X1=1.36  # sideways: the outer tyre only (the inner one is hidden behind it)
WHEELS=[(-2.2191, 0.7912, 0.2224, 0.3026), (-1.6364, 0.5852, 0.4125, 0.496), (-0.6713, 0.5758, 0.4212, 0.5012), (0.3358, 0.5741, 0.4174, 0.5014), (1.3171, 0.5796, 0.4095, 0.4926), (2.1257, 0.7606, 0.2396, 0.3092)]   # centres and radii as measured on the team's model (gear_blender.py)
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
    for (wz,wy,wr,rr) in WHEELS: m&=np.hypot(z-wz,y-wy)>max(wr,rr if rr>0.4 else 0)+0.012
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
def fill(v):
    # missing bins take their neighbours' values (round the circle)
    n=len(v); idx=np.arange(n); ok=~np.isnan(v)
    return np.interp(idx,np.r_[idx[ok]-n,idx[ok],idx[ok]+n],np.r_[v[ok],v[ok],v[ok]])
def envelope(v,half,sigma):
    # where the surface usually is over about one link (so the grooves between links stay), then smoothed
    e=np.percentile(np.stack([np.roll(v,k) for k in range(-half,half+1)]),80,axis=0)
    g=np.exp(-0.5*(np.arange(-3*sigma,3*sigma+1)/sigma)**2); g/=g.sum(); m=len(g)
    return np.convolve(np.r_[e[-m:],e,e[:m]],g,'same')[m:-m]
def belt_off(x,s,nn,L):
    # per stretch of the loop (NBELT bins): how far the belt's outer face stands off its usual distance from the line
    belt=(nn<BELT_HALF)&(nn>-BELT_INSIDE)&(x>0.97)
    b=np.minimum((s/L*NBELT).astype(int),NBELT-1)
    top=np.full(NBELT,np.nan)
    for i in range(NBELT):
        k=belt&(b==i)&(x>1.03)&(x<1.47)
        if k.sum()>=8: top[i]=np.percentile(nn[k],92)
    top=envelope(fill(top),4,3)
    return np.clip(top-np.median(top),-0.04,0.04),belt
def refine(p,x,y,z):
    # the centre line at NBELT points, moved onto the belt stretch by stretch (three passes)
    p=resample(p,NBELT)
    for _ in range(3):
        s,nn,L=project(p,z,y)
        off=table_at(belt_off(x,s,nn,L)[0],np.arange(NBELT).astype(float))
        t=np.roll(p,-1,0)-np.roll(p,1,0); t/=np.hypot(t[:,0],t[:,1])[:,None]
        p=resample(p+np.c_[t[:,1],-t[:,0]]*off[:,None],NBELT)
    return p
def discs(loops):
    # per wheel: the radius of the disc that turns, never out into the band of either side's belt
    out=[]
    for (wz,wy,wr,rr) in WHEELS:
        reach=min(np.hypot(p[:,0]-wz,p[:,1]-wy).min() for p in loops)-BELT_INSIDE-0.004
        out.append((wz,wy,round(float(min(wr,reach)),3),rr))
    return out
TEXTURES='C:/team/five_river/Assets/_Project/Art/Models/Vehicles/T34_85/Textures/'
CORE_IN=-0.028; CORE_OUT=0.0; CORE_X0=1.0; CORE_X1=1.482    # the core: just inside the belt's faces (about -0.040, +0.025, 0.99, 1.495; the grooves between links go down to about +0.005)
def belt_fill(uvs):
    # texture coordinates of one texel that has the belt's usual colour, lies in an even patch (so it stays that
    # colour in the smaller mip levels) and is flat in the normal map. uvs: the belt's vertices
    base=np.asarray(Image.open(TEXTURES+'T_VH_T34_Body_BaseColor.png').convert('RGB'),dtype=np.float32)
    bump=np.asarray(Image.open(TEXTURES+'T_VH_T34_Body_Normal.png').convert('RGB').resize(base.shape[1::-1]),dtype=np.float32)
    H,W=base.shape[:2]
    px=np.clip((uvs[:,0]%1*W).astype(int),16,W-17); py=np.clip(((1-uvs[:,1]%1)*H).astype(int),16,H-17)
    usual=np.median(base[py,px],axis=0)
    pick=np.random.default_rng(0).choice(len(px),min(4000,len(px)),replace=False); best=None
    for i in pick:
        a=base[py[i]-16:py[i]+17,px[i]-16:px[i]+17].reshape(-1,3); b=bump[py[i]-4:py[i]+5,px[i]-4:px[i]+5].reshape(-1,3)
        score=np.abs(a.mean(0)-usual).sum()+a.std(0).sum()+0.5*np.abs(b.mean(0)-(128,128,255)).sum()
        if best is None or score<best[0]: best=(score,i,a.mean(0))
    _,i,colour=best
    return [round(float((px[i]+0.5)/W),5),round(float(1-(py[i]+0.5)/H),5)],usual,colour
def table_at(t,u):
    # the game's lookup: bin centres, straight line between them, round the circle. u in bins
    f=u-0.5; i=np.floor(f).astype(int); a=f-i; n=len(t)
    return t[i%n]*(1-a)+t[(i+1)%n]*a
def tyre_tables(x,y,z,belt,p):
    near=np.stack([np.hypot(z-w[0],y-w[1])-w[3] for w in WHEELS]).argmin(0)
    out=[]; seen=[]
    for wi in range(1,5):
        wz,wy,_,rr=WHEELS[wi]; r=np.hypot(z-wz,y-wy); th=np.degrees(np.arctan2(y-wy,z-wz))%360
        zone=(~belt)&(near==wi)&(x>TYRE_X1)&(r>0.45)&(r<0.54)
        tb=(th/(360/NTYRE)).astype(int)%NTYRE; sil=np.full(NTYRE,np.nan)
        for i in range(NTYRE):
            k=zone&(tb==i)
            if k.sum()>=10: sil[i]=np.percentile(r[k],97)
        target=np.nanmedian(sil)
        # never out into the belt where it lies on the wheel
        pr=np.hypot(p[:,0]-wz,p[:,1]-wy); pt=(np.degrees(np.arctan2(p[:,1]-wy,p[:,0]-wz))%360/(360/NTYRE)).astype(int)%NTYRE
        cap=np.full(NTYRE,9.0)
        for i in range(NTYRE):
            k=np.isin(pt,[(i-1)%NTYRE,i,(i+1)%NTYRE])
            if k.any(): cap[i]=pr[k].min()-0.034
        # where the belt lies on the wheel the tyre's rim belongs to the belt's band, so its outline cannot be read there
        sil[cap+0.034-BELT_INSIDE<target+0.025]=np.nan
        target=np.nanmedian(sil)
        want=np.minimum(target,cap)
        with np.errstate(invalid='ignore',divide='ignore'):
            k=np.where(sil>TYRE_FROM+0.006,(want-TYRE_FROM)/(sil-TYRE_FROM),np.nan)
        # bins that could not be read lean on their neighbours for 3 bins, then are left alone (factor 1)
        if np.isnan(k).all(): k=np.ones(NTYRE)
        ok=~np.isnan(k); idx=np.arange(NTYRE); far=np.array([np.min(np.minimum((i-idx[ok])%NTYRE,(idx[ok]-i)%NTYRE)) for i in idx])
        k=1+(fill(np.clip(k,0.5,3.0))-1)*np.clip(1-far/4.0,0,1); k=(np.roll(k,1)+2*k+np.roll(k,-1))/4
        out.append(k); seen.append((sil,target))
    return np.array(out),near,seen
if __name__=='__main__':
    out={}
    for n,sg in (('TrackL',-1),('TrackR',1)):
        xo,yo,zo,t=load(n,sg)
        # the team's model is light (few, large triangles): measure on points spread evenly over its surface, not on its vertices
        A=np.c_[xo,yo,zo]; e1=A[t[:,1]]-A[t[:,0]]; e2=A[t[:,2]]-A[t[:,0]]; area=0.5*np.linalg.norm(np.cross(e1,e2),axis=1)
        rng=np.random.default_rng(1); pick=rng.choice(len(t),300000,p=area/area.sum()); u=rng.random(300000); v=rng.random(300000); f=u+v>1; u[f]=1-u[f]; v[f]=1-v[f]
        S=A[t[pick,0]]+e1[pick]*u[:,None]+e2[pick]*v[:,None]; x,y,z=S[:,0],S[:,1],S[:,2]
        p=loop_of(x,y,z)
        s,nn,L=project(p,z,y); off0=belt_off(x,s,nn,L)[0]
        p=refine(p,x,y,z)
        s,nn,L=project(p,z,y)
        g=classify(xo,yo,zo,t,p)
        print(n,'loop length %.3f'%L,'tris static/wheels/belt',[(g==i).sum() for i in range(8)])
        # histogram of signed distance for points outside wheels, outer slice
        m=(x>1.4)&~spike(y,z)
        for (wz,wy,wr,rr) in WHEELS: m&=np.hypot(z-wz,y-wy)>max(wr,rr if rr>0.4 else 0)+0.012
        h,e=np.histogram(nn[m],bins=np.arange(-0.12,0.121,0.01)); print(' n hist',list(zip(np.round(e[:-1],2),h)))
        off,belt=belt_off(x,s,nn,L)
        tyre,near,seen=tyre_tables(x,y,z,belt,p)
        print(' belt outer face off the line by mm: %.0f..%.0f (std %.1f) before the second fit, %.0f..%.0f (std %.1f) after'%(1000*off0.min(),1000*off0.max(),1000*off0.std(),1000*off.min(),1000*off.max(),1000*off.std()))
        for i,(sil,target) in enumerate(seen): print(' road wheel %d tyre outline off the circle (r %.3f) by mm: %.0f..%.0f (std %.1f), scale %.2f..%.2f'%(i+1,target,1000*np.nanmin(sil-target),1000*np.nanmax(sil-target),1000*np.nanstd(sil),tyre[i].min(),tyre[i].max()))
        so,no,_=project(p,zo,yo); belto=(no<BELT_HALF)&(no>-BELT_INSIDE)&(xo>0.97)&(no>0.0)      # outer half only: the added inner belt is one flat colour
        fill_uv,usual,colour=belt_fill(np.fromfile(n+'.bin',dtype=np.float32).reshape(-1,5)[:,3:5][belto])
        print(' belt usual colour',usual.round(0),' strip texel',fill_uv,'colour there',colour.round(0))
        out[n]={'beltFill':fill_uv,'length':round(float(L),4),'loop':[round(float(v),4) for v in p.flatten()],'tyre':[round(float(v),3) for v in tyre.flatten()]}
    turning=discs([np.array(out[n]['loop']).reshape(-1,2) for n in ('TrackL','TrackR')])
    print('discs that turn, radius (m):',[w[2] for w in turning],' was',[w[2] for w in WHEELS])
    out['wheels']=[v for w in turning for v in w]; out['beltHalf']=BELT_HALF; out['beltInside']=BELT_INSIDE; out['wheelInner']=1.07; out['beltInner']=0.97
    out['tyreFrom']=TYRE_FROM; out['tyreX0']=TYRE_X0; out['tyreX1']=TYRE_X1
    out['coreIn']=CORE_IN; out['coreOut']=CORE_OUT; out['coreX0']=CORE_X0; out['coreX1']=CORE_X1
    json.dump(out,open('C:/team/five_river/Assets/_Project/Art/Resources/Vehicles/T34_RunningGear.json','w'))
