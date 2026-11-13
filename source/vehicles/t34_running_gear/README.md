# T-34 주행 장치 측정 (TankRunningGear.cs용)

## 2026-11-13 재측정 — 11-10 경량 모델(SM_VH_T34_85.fbx, 궤도 1만 삼각형) 기준
`gear.py` · `dump_tracks.cs`는 11-12에 옛 무거운 모델(궤도 15만 삼각형)에서 잰 것이다. 11-10에 모델이 감소(decimate)
모델로 바뀌면서 절단선이 맞지 않아 바퀴 테두리가 찢기고 띠가 날개처럼 튀었으므로, Blender에서 FBX를 직접 읽어 다시 쟀다.

| 파일 | 하는 일 |
|---|---|
| `gear_blender.py` | 측정: 바퀴 중심 · 반지름 맞춤, 고리 중심선, 띠 두께 → `T34_RunningGear.json` + 측면 분류 그림 |
| `round_wheels.py` | 바퀴 다듬기: 보기륜 4 · 기동륜 · 유동륜 테두리 정점을 축에 수직인 면에서 반지름 방향으로만 옮겨 정확한 원에 맞춤, 긴 테두리 모서리에 원 위 점 추가, 테두리 법선을 회전체 법선으로 |
| `cut_tracks.py` | 경계 분할: 게임이 나누는 선(바퀴 회전 원 · 띠 띠폭 양쪽 · x 기준면)을 따라 기존 모서리 위에만 점을 넣어 삼각형을 나눔 (모양 · UV 그대로). 4번째 인수로 다듬기 JSON을 주면 다듬기 → 분할 순서로 한 번에 처리 |

축 대응 (검증 2026-11-13): Unity 차체 공간 (x, y, z) = (-Blender x, Blender z, -Blender y).
옛 모델을 이 대응으로 읽어 gear.py 방법을 돌리면 커밋된 고리와 평균 0.03mm로 일치한다 (z를 뒤집으면 평균 2cm, 최대 18cm 어긋남).

### 다시 만들 때 순서 (백그라운드 Blender 5.2, `-b --factory-startup`)
```
B="C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"
# 1. 원본에서 측정 (다듬기 축 = 이 결과의 바퀴 중심)
$B -b --factory-startup -P dump.py -- SM_VH_T34_85.fbx new        # (dump: gear_blender.py --npz 용, 또는 Blender에서 직접 실행)
python gear_blender.py --npz new run5 run5/T34_RunningGear.json
# 2. 다듬은 메시에서 다시 측정 (중심은 다듬기 축으로 고정)
$B -b --factory-startup -P round_wheels.py -- SM_VH_T34_85.fbx run5/T34_RunningGear.json rounded.fbx
python gear_blender.py --npz rounded run6 run6/T34_RunningGear.json centres=run5/T34_RunningGear.json
# 3. 원본 → 다듬기 → 분할 → 내보내기 (ART_SPEC 내보내기 설정, reduce_and_fill.py와 같은 호출)
$B -b --factory-startup -P cut_tracks.py -- SM_VH_T34_85.fbx run6/T34_RunningGear.json final.fbx run5/T34_RunningGear.json
# 4. 확인: 분류만 다시 해서 그림과 문제 삼각형 수 보기
python gear_blender.py --npz final eval_final eval=run6/T34_RunningGear.json
```
`gear_blender.py`는 Blender 안에서 `-P gear_blender.py -- <fbx> <출력폴더> <json>`으로도 돈다 (Blender 내장 python에 numpy 있음, PIL 불필요).

### 2026-11-13 저녁 — 앞뒤 끝에서 띠가 두 가닥 · 떠 있는 리본으로 보이던 문제
**원인 (메시 쪽, 코드 · JSON은 정상)**: 게임이 옮기는 띠(고리 중심선 -0.045 ~ +0.06 안 삼각형)는 바깥 면과 옆벽은 거의
한 바퀴 이어져 있지만, **안쪽 면(바퀴 쪽)이 긴 구간에서 비어 있었다**. 데시메이트 때 안쪽 면이 유동륜 테두리 · 기동륜 ·
보기륜 타이어와 붙어 -0.045보다 깊이 들어갔기 때문이다(띠만 떼어 낸 열린 테두리: 유동륜 감는 곳 1.37 m, 위쪽 궤도 약
4~5 m, 보기륜 밑 0.4~0.8 m씩). 서 있을 때는 정지한 바퀴 테두리가 그 자리를 메우지만, 띠가 돌면 열린 단면이 위아래 궤도와
앞뒤 끝으로 옮겨 가고, Unity는 뒷면을 그리지 않으므로 그 자리가 비쳐 보여 띠가 양쪽 가장자리 두 가닥이나 떠 있는 고리처럼
보였다. 확인: 게임 코드를 그대로 옮긴 `sim.py`로 측정했다. 띠 꼭짓점의 고리 위치(국소 탐색 대 전역)는 최대 6 mm 차이로
일치하고, 측면 투영으로 보면 옮긴 띠는 띠폭 안에 그대로 있다. 그래서 고리 · 두께 분류 수치 문제는 아니다.

| 파일 | 하는 일 |
|---|---|
| `add_belt_inner.py` | 수정: 고리 전체를 따라 중심선에서 0.026 안쪽, 띠폭 x 1.005~1.48에 바퀴 쪽을 보는 띠 한 장(3 cm 간격 363고리, 궤도당 1,452삼각형)을 덧붙인다. 원래 안쪽 면(-0.030~-0.046) 뒤에 숨고, 비어 있던 곳만 막는다. 오프셋이 일정해 어디로 옮겨져도 모양이 같다. UV는 띠 평균색에 가깝고 법선이 거의 평평한 Track 텍스처 한 점. 기존 정점 · 면 · UV · 법선은 그대로 두고 뒤에 붙인다 |
| `sim.py` | TankRunningGear.cs(표 512 · 격자 국소 탐색 · 분류 · BeltJob)의 numpy 판 |
| `render_runtime.py` | 게임처럼 나누고 바퀴를 돌리고 띠를 옮긴 상태를 백그라운드 렌더 (`CULL=1` 뒷면 제거, `MODE=red` 정지 부분 빨강, `MODE=belt` 띠만) |
| `raycheck.py` | 비침 수: 왼쪽 24개 시점에서 옮긴 띠 위의 점으로 광선을 쏴, 뒷면을 건너뛴 첫 앞면이 없거나 겨눈 점보다 12 cm 넘게 뒤면 비침으로 센다 |
| `sections.py` | 고리 20 cm마다 띠 단면 모자이크(파랑 = 옮기는 띠, 회색 = 정지) |

```
$B -b --factory-startup -P add_belt_inner.py -- SM_VH_T34_85.fbx T34_RunningGear.json out.fbx T_VH_T34_Track_BaseColor.png T_VH_T34_Track_Normal.png
$B -b --factory-startup -P raycheck.py -- out.fbx T34_RunningGear.json 0,0.5,1.1,2.9,4.3
```
전후 비침 수 (광선 약 6만 개, [뒤끝, 위아래 궤도, 앞끝]):

| 주행 거리 | 수정 전 | 수정 후 |
|---|---|---|
| 0 m | [0, 1, 0] 0.00% | [12, 2, 2] 0.03% |
| 0.5 m | [801, 2145, 619] 5.30% | [25, 191, 19] 0.39% |
| 1.1 m | [414, 642, 879] 2.84% | [11, 116, 12] 0.22% |
| 2.9 m | [232, 1919, 1824] 5.86% | [3, 120, 1] 0.21% |
| 4.3 m | [120, 3603, 884] 6.88% | [0, 122, 33] 0.26% |

`gear_blender.py ... eval=`의 바퀴 · 띠 문제 수는 전후 같다(띠 꼭짓점 띠폭 밖 0, 바퀴 조각 튀어나옴 0, 정지 조각 원판 침범 0).
덧붙인 1,452삼각형은 게임 규칙으로 모두 띠로 분류되고, 정지 · 바퀴 삼각형 수는 그대로다.
- 남은 것: 위쪽 궤도 일부 단면은 바깥 면이 다른 곳보다 약 2 cm 낮고(+0.01 대 +0.035), 앞뒤 끝 단면은 약 2 cm 높다. 띠가
  돌면 이 높낮이가 함께 돌아 끝부분에서 약하게 울퉁불퉁하다(틈은 아님). 남은 비침 약 0.2%는 위아래 궤도 바깥 면 · 옆벽의 작은 구멍이다.

### 주의
- JSON의 바퀴 회전 반지름(wheels[w*4+2])은 **메시 절단 원 − 0.012**다. TankRunningGear.cs가 중심이 반지름+SeamOverlap(0.012) 안인
  삼각형을 돌리므로, 이렇게 해야 절단 원 안쪽만 정확히 돈다. 코드의 SeamOverlap을 바꾸면 JSON도 다시 만든다.
- 바퀴 목록은 좌우 공용이다(스키마). 좌우 맞춤 중심 차이는 최대 약 1cm였고, 다듬기를 공용 중심 기준으로 해서 맞췄다.
- 고리 측정에서 30삼각형 미만의 떠 있는 파편은 뺐고, 표본당 15° 넘게 꺾인 곳은 펴서(TrackR 뒤쪽 위 꺾임) 띠가 접히지 않게 했다.
