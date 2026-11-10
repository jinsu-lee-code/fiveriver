import numpy as np, bpy
rng=np.random.default_rng(7); N=512
def tile_noise(scale,octaves=4):
    out=np.zeros((N,N))
    for o in range(octaves):
        f=scale*(2**o); g=rng.standard_normal((f,f))
        x=np.linspace(0,f,N,endpoint=False); i0=np.floor(x).astype(int); t=x-i0; i1=(i0+1)%f
        a=g[np.ix_(i0,i0)]*(1-t)[:,None]*(1-t)[None,:]+g[np.ix_(i1,i0)]*t[:,None]*(1-t)[None,:]+g[np.ix_(i0,i1)]*(1-t)[:,None]*t[None,:]+g[np.ix_(i1,i1)]*t[:,None]*t[None,:]
        out+=a/(2**o)
    return out
h=0.55*tile_noise(64,3)
lines=np.sin((np.arange(N)[None,:]*0.9+tile_noise(8,2)*40)*0.35)
h+=0.25*np.clip(lines,0.85,1.0)*-4
h=(h-h.mean())/h.std()
dx=(np.roll(h,-1,1)-np.roll(h,1,1))*0.5; dy=(np.roll(h,-1,0)-np.roll(h,1,0))*0.5
s=0.35; n=np.dstack((-dx*s,-dy*s,np.ones_like(h))); n/=np.linalg.norm(n,axis=2,keepdims=True)
def save(arr,path):
    img=bpy.data.images.new("t",N,N,alpha=True); rgba=np.dstack((arr,np.ones((N,N))))
    img.pixels.foreach_set(np.flipud(rgba).astype(np.float32).ravel()); img.filepath_raw=path; img.file_format='PNG'; img.save()
save(n*0.5+0.5,"C:/sk/five_river/Assets/_Project/Art/SurfaceDetail20261030/Skin_PoreDetail_Normal.png")
m=tile_noise(6,4); m=(m-m.mean())/m.std(); base=0.5+m*0.035
save(np.dstack((base+m*0.008,base-m*0.004,base-m*0.008)).clip(0,1),"C:/sk/five_river/Assets/_Project/Art/SurfaceDetail20261030/Skin_Mottle_Detail.png")
print("SKIN_OK")
