# A_KPA_CrouchRun 손질 (2026-11-13)

원본: `five_river/Assets/_Project/_Source/A_KPA_CrouchRun/A_KPA_CrouchRun_Mixamo.fbx` (Mixamo "Running Crouched With Rifle", 수정하지 않음)
결과: `five_river/Assets/_Project/Art/Animations/KPA/A_KPA_CrouchRun.fbx` (같은 경로 덮어쓰기, `.meta` 그대로)

## 방식
FBX를 다시 내보내지 않는다. `fbxio.py`가 원본 FBX를 바이트 그대로 읽고 쓰며(무변경 왕복 시 원본과 바이트 동일),
`kpa_crouch_run.py`는 애니메이션 커브 **값만** 바꾼다. 노드 이름 · PreRotation · 단위(cm) · 축(Y-up, +Z 정면) ·
키 시간(0~23, 30fps) · 테이크 `mixamo.com`이 원본과 같아서 Unity 아바타 · Animator 연결이 그대로 유지된다.

## 바꾼 것 (값은 `kpa_crouch_run.py`의 `CFG` 한 곳)
- 엉덩이: 평균 yaw −17.7°(오른쪽으로 돌아간 사선)를 0°로, 원래 흔들림은 80%만 유지(±4°). 3 cm 낮춤.
- 다리: 발볼(ToeBase) 경로를 다시 정함 — 디딤 구간(왼발 9~17, 오른발 21~6프레임)은 x = ±7.5 cm 직선 · 등속,
  발끝은 정면에서 4° 바깥. 무릎은 정면을 향하게 두 관절 IK로 다시 풀었다(원본 발 높이 · 발 굴림은 유지).
- 몸통: 숙임(Spine→Neck) 49°→30°(엉덩이→목 약 25°), 어깨선 −21°→−10°(오른어깨 약간 뒤, 카운터 회전 ±2°).
- 머리 · 목: 시선은 이동 방향, 6° 아래. 목이 가슴과 머리 사이를 반씩 나눔.
- 팔: 모신나강 combat idle 휴대. 오른손(개머리 손목)은 오른쪽 어깨 관절 기준 (안쪽 6, 아래 29, 앞 17) cm,
  총 방향은 위 18° · 왼쪽 24°, 왼손은 총선 위 28 cm(가슴 높이). 손 목표를 가슴(Spine2)에 붙여 걸음 따라 함께 흔들림
  + 0.8 cm 위아래. 팔꿈치는 뒤 · 아래 극으로 몸에 붙임. 손 방향은 원본의 "오른손→왼손 선" 대비 손 방향을 그대로 옮김.
- 손가락 · 어깨뼈(Shoulder) · 발가락은 원본 그대로.

## 다시 만들기 (백그라운드 Blender만, GUI 세션 사용 안 함)
```
set B="C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
%B% -b --factory-startup -P kpa_crouch_run.py -- <원본.fbx> <결과.fbx> [보고.json]
%B% -b --factory-startup -P render_sheet.py  -- <시트.png> BEFORE=<원본.fbx> AFTER=<결과.fbx>
%B% -b --factory-startup -P verify.py        -- <원본.fbx> <결과.fbx>
```
- `render_sheet.py`: 옆 · 정면 · 위 시점 8프레임 비교 시트(왼쪽 파랑, 오른쪽 빨강, 1.23 m 대용 총은 오른손→왼손 선).
- `verify.py`: FBX 트리 비교(커브 값만 달라야 함), 새 Blender 가져오기로 뼈 이름 · 레스트 포즈 · 프레임 범위 · 루프 · 위치 확인.
