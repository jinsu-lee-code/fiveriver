# Unity CLI helper (Unity editor must be open on C:/team/five_river).
#   python unity_run.py "cmd:editor.playmode start" sleep8 jump_silence.cs sleep6 rec_fable.cs
# items: cmd:<unity command ...>, sleepN, or a .cs snippet (run with script eval; must end with return "...";)
import sys; sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import subprocess,os,re,sys,time
for f in sys.argv[1:]:
    if f.startswith('sleep'): time.sleep(float(f[5:])); continue
    if f.startswith('cmd:'):
        r=subprocess.run(['unity','command']+f[4:].split()+['--project-path','C:/team/five_river'],capture_output=True,text=True,encoding='utf-8'); print(r.stdout.strip().splitlines()[-1:] ); continue
    c=open(f if os.path.exists(f) else os.environ['TEMP']+'/'+f, encoding='utf-8').read()
    r=subprocess.run(['unity','command','script','eval','--project-path','C:/team/five_river','--code',c],capture_output=True,text=True,encoding='utf-8')
    m=re.search(r'"result":"(.*?)"\}',r.stdout,re.S);print(m.group(1).replace('\n','\n') if m else r.stdout[-800:]+r.stderr[-400:])
