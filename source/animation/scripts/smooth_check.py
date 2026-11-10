# Wrist speed and jerk per frame of the baked reload: stop-and-go spots and sudden jumps
import json, math, sys
d=json.load(open(sys.argv[1] if len(sys.argv)>1 else "C:/sk_anim/reload_partial_baked.json")); F=d["frames"]; fps=d["fps"]
P=[(m[12],m[13],m[14]) for m in d["wrist"]]
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def n(a): return math.sqrt(sum(x*x for x in a))
v=[n(sub(P[i+1],P[i]))*fps for i in range(F)]
acc=[abs(v[i+1]-v[i])*fps for i in range(F-1)]
stops=[i for i in range(1,F-1) if v[i]<0.05 and v[i-1]>0.15 or (v[i]<0.05 and v[i+1]>0.15)]
print("SMOOTH peak_speed %.2f m/s  peak_accel %.1f m/s2  mean_accel %.1f"%(max(v),max(acc),sum(acc)/len(acc)))
print("SMOOTH stop_frames", [round(i/F,2) for i in range(1,F-1) if v[i]<0.03 and 0.1<i/F<0.9])
print("SMOOTH top_accel", sorted([(round(a,1),round(i/F,2)) for i,a in enumerate(acc)],reverse=True)[:6])
