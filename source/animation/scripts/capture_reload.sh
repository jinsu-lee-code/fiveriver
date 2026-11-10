#!/bin/bash
# usage: capture_reload.sh PREFIX phase... ; plays, partial reload at 0.04x, screenshots into C:/sk_anim/cap
P="--project-path C:/sk/five_river"; D=C:/sk_anim/cap; mkdir -p /c/sk_anim/cap; pre=$1; shift
ev(){ unity command script eval $P --timeout 90 --code "$1" 2>/dev/null | tail -1 | sed -E 's/.*"result":"([^"]*)".*/\1/'; }
unity command editor.playmode start $P >/dev/null 2>&1
for i in $(seq 1 30); do r=$(ev 'return UnityEngine.Application.isPlaying && UnityEngine.Object.FindAnyObjectByType<FiveRivers.Core.FlowDirector>()!=null ? "y":"n";'); [ "$r" = y ] && break; sleep 3; done
ev 'var d=UnityEngine.Object.FindAnyObjectByType<FiveRivers.Core.FlowDirector>(); d.Flow.StartRun(); d.Flow.ReportGoalMet(FiveRivers.Core.FlowSection.Tutorial); d.ClockScale=0f; foreach(var e in UnityEngine.Object.FindObjectsByType<FiveRivers.Combat.EnemyAgent>(UnityEngine.FindObjectsSortMode.None)) e.enabled=false; return "ok";' >/dev/null; sleep 3
ev 'var r=UnityEngine.Object.FindAnyObjectByType<FiveRivers.Combat.M1Rifle>(); r.SendMessage("Shoot",false); return "c";' >/dev/null; sleep 2
ev 'var r=UnityEngine.Object.FindAnyObjectByType<FiveRivers.Combat.M1Rifle>(); UnityEngine.Time.timeScale=0.03f; r.TryReload(); return "rel="+r.Reloading;' >/dev/null
for t in "$@"; do while :; do p=$(ev 'UnityEngine.Time.timeScale=0.03f; var r=UnityEngine.Object.FindAnyObjectByType<FiveRivers.Combat.M1Rifle>(); return (r.Reloading?r.ReloadProgress:1f).ToString("F3");'); awk -v p="$p" -v t="0.$t" 'BEGIN{exit !(p>=t)}' && break; done; ev 'UnityEngine.ScreenCapture.CaptureScreenshot("'$D'/'$pre'_'$t'.png"); return "ok";' >/dev/null; echo -n "$t@$p "; done; echo
ev 'UnityEngine.Time.timeScale=1f; return "ok";' >/dev/null
unity command editor.playmode stop $P >/dev/null 2>&1
