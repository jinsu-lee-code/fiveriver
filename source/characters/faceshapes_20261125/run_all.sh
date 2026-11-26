#!/usr/bin/env bash
# 얼굴 블렌드 셰이프 3종 모델 재생성 (Git Bash). 게임 FBX는 읽기만 한다.
# 결과: faceshapes_<tag>.json / .blend, renders/<tag>_<Shape>.png
# 렌더 없이 json·blend만: ./run_all.sh --norender
cd "$(dirname "$0")"
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
for t in ParkYongpal Soldier Person1; do
  "$B" -b --factory-startup --python "$PWD/faceshapes.py" -- "$t" "$@" > "fs_$t.log" 2>&1 &
done
wait
grep -h '@@STATS@@\|Traceback' fs_*.log | cut -c1-300
rm -rf renders/_tmp_ParkYongpal renders/_tmp_Soldier renders/_tmp_Person1
