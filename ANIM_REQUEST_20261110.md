# ANIM_REQUEST_20261110.md — 애니메이션 추가 요청서 (2026-11-10)

> **브랜치 `animator`** · 작성 2026-11-10 · 기준: 개인 저장소 최신본(49fd84d)을 팀 저장소에 반영한 상태
> `ANIM_SPEC.md`(10-25 원본 요청서)에 이어 **지금 남은 것만** 적는다. 규격은 ANIM_SPEC 1-4를 따른다: 적 · 아군은 Humanoid, **모든 이동은 제자리(Root Motion 미사용)**, 30fps 이상, **FBX 안 클립 이름 = 파일명**.
> 작업 전에 `main`을 이 브랜치로 먼저 병합하고, 기존 파일은 같은 경로 · 같은 이름으로 덮어써서 `.meta`를 유지한다.
> 커밋: `MM.DD/Anim/파일명Swap` · 검수 합격분은 `main`에서 `MM.DD/Merge/Anim`으로 병합.

## 0. 현재 상태 요약
| 구분 | 상태 |
|---|---|
| ANIM_SPEC 27종 | 27종 모두 10-30 납품 커밋 있음, `main` 병합 완료 |
| ANIM_SPEC 검수 칸 | **27칸 전부 비어 있음** → 아래 N4 |
| 11-10 반영분 | 아군 사격 클립 `A_ROK_Fire_Crouch` Blender 수정, `AC_ROK` · `AC_KPA` 컨트롤러, 1인칭 장전(검지 · 엄지 포함) Blender 원본 `source/animation/` |

## 1. 우선순위 · 목록
| 우선 | 번호 | 파일명 | 이름 | 도구 | 쓰는 구간 |
|---|---|---|---|---|---|
| A | N1 | `A_ROK_Run_PortArms.fbx` (새로) | 국군 앞에총 달리기 | VARCO 3D 애니메이션 → Humanoid 리타깃, 팔은 Blender 수정 | 고지 돌격 |
| B | N2 | 박용팔이 쓰는 대기 · 걷기 클립 | 발 뜸 수정 | Blender / Unity Foot IK | 고지 돌격 · 정상 |
| C | N3 | `source/animation/` 장전 원본 | 장전 수정 절차 인수 | 문서 · Blender | 전 구간 |
| A | N4 | ANIM_SPEC 검수 27칸 | 실행 빌드에서 합격 / 수정 요청 기록 | 검수 | 전 구간 |

## 2. 항목별 요청

### N1. 국군 앞에총 달리기 — `A_ROK_Run_PortArms` [A]
- **파일**: `A_ROK_Run_PortArms.fbx` · **위치**: `Assets/_Project/Art/Animations/ROK/`
- **문제**: 아군이 뒤처져 달릴 때 북한군 달리기 동작을 같이 써서 **다리를 크게 벌리고** 총 든 자세가 앞에총과 맞지 않는다.
- **규격**: 제자리 달리기 루프 · 0.7~0.8초 한 주기(두 걸음) · 첫 프레임과 끝 프레임 같은 자세 · 양손은 소총을 몸 앞 대각선(앞에총)으로 쥔 위치 · 보폭은 어깨너비 안쪽
- **손 위치**: 현재 앞에총 IK(`AllyRiflePose`, `AlliedRifleFit.correctCarryPose`)가 손을 소총에 붙이므로, 클립의 팔은 앞에총 근처면 된다. 팔이 크게 흔들리지 않게 한다.

프롬프트 (VARCO 3D 애니메이션 카탈로그 검색 / 텍스트):
```text
soldier running forward in place while holding a rifle diagonally across the chest with both hands (port arms), short strides, knees not spread, upper body slightly leaning forward, steady arms, loopable, 30 fps
```

검수:
- [ ] 고지 돌격에서 아군이 플레이어를 따라 뛸 때 다리가 벌어지지 않음
- [ ] 루프 이음새에서 튐 없음 (10회 반복)
- [ ] 소총이 손에서 떨어져 보이지 않음 (IK 켠 상태)
- [ ] Root Motion 없음 · 클립 이름 = 파일명
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:
- **main 연결**: `AC_ROK`의 달리기 상태 모션을 이 클립으로 바꾼다 (`main`에서)

### N2. 박용팔 발 뜸 [B]
- **문제**: 일부 장면에서 박용팔 발이 땅에서 살짝 떠 있다 (가까이 보이는 결함 — CLAUDE.md 아트 규칙 "뜬 발").
- **확인 순서**:
  1. 같은 클립을 일반 아군에게 넣었을 때도 뜨는지 → 뜨면 클립 문제(발 높이 · 루트 높이), 안 뜨면 박용팔 모델 비율(아바타 설정) 문제
  2. 클립 문제면 Blender에서 발 접지 프레임의 발 높이를 0으로 맞춰 같은 이름으로 덮어쓰기
  3. 모델 문제면 아바타의 Foot IK / 루트 높이 값을 기록하고 `main`에 전달
- 검수: [ ] 고지 돌격 · 정상 장면에서 발이 땅에 닿음 · 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### N3. 장전 Blender 원본 인수 [C]
- 1인칭 M1 장전(전탄 · 부분, 검지 · 엄지 포함)은 개인 저장소에서 Blender로 다시 만들었고 원본이 `source/animation/`에 있다.
- 수정 절차: `scripts/anim.py` · `params.json` 수정 → `blender -b --python scripts/anim.py -- both` → Unity 메뉴 `FiveRivers/Animation/Import M1 Reload Motion (Blender)`.
- 애니메이션 담당이 한 번 실행해 같은 결과가 나오는지 확인하고, `source/animation/README.md`에 막힌 점을 적는다.

### N4. ANIM_SPEC 검수 27칸 기록 [A]
- 실행 빌드에서 각 항목 "검수" 체크를 보고 결과를 기록한다. 특히 1인칭(장전 · 바주카 · 무기 전환)은 화면에 가장 가까우니 손가락 관절 · 총 가림을 가까이 본다.
- 수정 요청이 나온 항목은 이 문서 1번 표에 `N5~` 번호로 추가한다.

## 3. 범위 밖
- 분대 명령 동작 · 최만수 생전 대화 동작 · 새 무기 동작 (GDD 9번)
