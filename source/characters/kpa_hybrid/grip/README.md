# 적군이 모신나강을 쥐는 자세 (2026-11-22)

게임은 매 프레임 쥐는 모양을 찾지 않고 `EnemyGrips.cs`의 여섯 자세를 섞기만 한다
(오른손: 들고 이동 · 견착, 왼손: 낮게 들고 달리기 · 서서 달리기 · 무릎쏴 · 서서 쏴).
손 모델, 총 모델, 관절이 바뀌면 값을 손으로 고치지 말고 다시 푼다.

1. 플레이 모드 주 공세에서 `grip_static.cs`(총 · 손 메시)를 한 번, `grip_sample.cs`(어깨 위치)를 여러 번 eval
   → `C:/sk_assets/capture/grip/static.txt`, `samples.txt`
2. `blender -b --factory-startup -P grip_sdf.py` : 총 표면까지의 거리 지도
3. `python grip_solve.py 24` : 자세 풀이 (numpy만 필요, 약 3분). 목표 지점은 `SPECS`
4. `blender -b --factory-startup -P grip_render.py -- RightAim ...` : 총과 함께 눈으로 확인
5. `python grip_export.py` : `EnemyGrips.cs` 생성 → 컴파일
6. `hold_shots.cs` · `hold_sample.cs` + `python hold_check.py` : 게임 안 확인
   (`C:/sk_assets/capture/ad/hold_run.sh`)

시간을 멈추는 스크립트는 플레이를 끄기 전에 `Time.timeScale = 1`로 되돌릴 것.
기본값 0이 TimeManager에 저장된 적이 있다.
