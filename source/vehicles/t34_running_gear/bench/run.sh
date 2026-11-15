# usage: run.sh <tag> ; team project bench: new empty scene, play, stand the tank up, shoot at 0 / 1.48 / 5.18 m
cd "$(dirname "$0")"; U=unity_run.py; P="--project-path C:/team/five_river"
ready(){ for i in $(seq 1 90); do r=$(unity command script eval $P --timeout 20 --code "return \"$1\"==\"play\" ? (UnityEngine.Application.isPlaying?\"y\":\"n\") : (UnityEditor.EditorApplication.isCompiling||UnityEngine.Application.isPlaying?\"n\":\"y\");" 2>/dev/null | grep -o '"result":"[yn]"'); [ "$r" = '"result":"y"' ] && return 0; sleep 3; done; echo "editor not ready"; return 1; }
unity command editor.playmode stop $P >/dev/null 2>&1; sleep 3; ready edit
python $U refresh.cs; sleep 5; ready edit
python $U newscene.cs; unity command editor.playmode start $P >/dev/null 2>&1; sleep 5; ready play; sleep 2
echo "${1}0" > /c/sk_assets/capture/gear/tag.txt; python $U bench.cs sleep1 shots.cs sleep1 shots.cs
echo "${1}1" > /c/sk_assets/capture/gear/tag.txt; python $U move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 shots.cs
echo "${1}2" > /c/sk_assets/capture/gear/tag.txt; python $U move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 move.cs sleep1 shots.cs
unity command editor.playmode stop $P >/dev/null 2>&1; sleep 3; ready edit
