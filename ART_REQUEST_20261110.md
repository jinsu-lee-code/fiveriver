# ART_REQUEST_20261110.md — 아트 추가 요청서 (2026-11-10)

> **브랜치 `art`** · 작성 2026-11-10 · 기준: 개인 저장소 최신본(49fd84d)을 팀 저장소에 반영한 상태
> 이 문서는 `ART_SPEC.md`(10-25 원본 요청서)에 이어 **지금 남은 것만** 적는다. 이름 · 규격 · 내보내기 규칙은 ART_SPEC 1번(공통 규칙)을 그대로 따른다.
> 작업 전에 `main`을 이 브랜치로 먼저 병합하고, 기존 파일은 **같은 경로 · 같은 이름으로 덮어써서** `.meta`(GUID)를 유지한다 (CLAUDE.md "브랜치 운영").
> 커밋: `MM.DD/Art/파일명Swap` · 검수 합격분은 `main`에서 `MM.DD/Merge/Art`로 병합.

## 0. 현재 상태 요약
| 구분 | 상태 |
|---|---|
| ART_SPEC 22종 | 21종 납품 커밋 있음. `T_VH_T34_Body_BaseColor`만 커밋 없음 (전차를 VARCO 432 모델로 바꾸며 텍스처를 모델에 구움 → 아래 R5) |
| ART_SPEC 검수 칸 | **30칸 전부 비어 있음** (합격 / 수정 요청 표시 없음) → 아래 R6 |
| 11-10 반영분 | 맵 수리(MapRepair20261106) · 메가스캔 소품(Megascans20261107) · M1 재질 · 탄약 상자 스케일 수정 · 박용팔 셔츠 수정(SurfaceDetail20261110) |

## 1. 우선순위 · 목록
| 우선 | 번호 | 대상 파일 | 할 일 | 도구 | 쓰는 구간 |
|---|---|---|---|---|---|
| A | R1 | `ROK_Regular_NoCap_20261105.asset` (일반 아군 3명) | 셔츠 · 조끼 조각 노멀 수정 | Blender (`tools/park_skin_fix` 방식) | 참호 · 고지 돌격 |
| A | R2 | `GMC_Truck_Open.fbx` (새벽 트럭) | 짐칸 왼쪽 안쪽 벽 구멍 메우기 | Blender | 정상 새벽 |
| B | R3 | `SK_CH_ROK_ParkYongpal` 몸 재질 | 셔츠 노멀 맵 다시 굽기 (매끈한 메시 기준) | Blender / Substance | 고지 돌격 |
| B | R4 | `SM_WP_Bazooka.fbx` | M20(3.5인치) / M9A1(2.36인치) 확정 후 형태 확인 | 기획(D) 결정 → Blender | 전차전 |
| C | R5 | `ART_SPEC.md` 전차 항목 | `T_VH_T34_Body_BaseColor` · `M_VH_T34_Body` 항목을 실제 납품 형태로 고치기 | 문서 | — |
| A | R6 | ART_SPEC 검수 30칸 | 실행 빌드에서 보고 합격 / 수정 요청 기록 | 검수 | 전 구간 |

## 2. 항목별 요청

### R1. 일반 아군 셔츠 · 조끼 조각 노멀 — `ROK_Regular_NoCap_20261105` [A]
- **위치**: `Assets/_Project/Art/CharacterVariants/RegularHelmet20261105/`
- **문제**: 셔츠 · 조끼 면이 삼각형마다 따로 빛을 받는다(노멀이 면 단위로 나뉨). 평균 편차 몸 14.1° · 조끼 19.5°. 팔을 들면 어깨 · 소매에 각진 그늘과 삼각형 무늬가 생긴다. 박용팔은 11-10에 고쳤고, 일반 아군 3명이 남았다.
- **방법**: `tools/park_skin_fix/README.md` 순서 그대로 한다.
  1. Unity에서 메시 · 포즈 덤프(`unity_dump_mesh.cs.txt`, `unity_dump_pose.cs.txt`의 대상 이름만 바꿈)
  2. Blender 백그라운드로 `fix_park_skin.py`를 실행해 같은 위치 정점끼리 노멀을 면적 가중 평균하고 어깨 웨이트를 다듬는다(셔츠 · 조끼 서브메시)
  3. 결과를 **새 파일** `ROK_Regular_NoCap_SkinFix.asset`으로 저장. 원본 메시는 덮어쓰지 않는다
- **산출물**: `Art/CharacterVariants/RegularHelmet20261105/ROK_Regular_NoCap_SkinFix.asset` + Blender 자료 `tools/regular_skin_fix/`
- **main 연결**: 아군 3명의 SkinnedMeshRenderer 메시만 바꾼다 (연결은 `main`에서, CLAUDE.md 규칙)

검수:
- [ ] 사격 · 앞에총 자세에서 어깨 · 소매에 삼각형 무늬 없음 (수정 전과 같은 각도 캡처 비교)
- [ ] 정점 수 · 서브메시 수 · 뼈 수가 원본과 같음
- [ ] 노멀 맵 세기를 낮췄다면 재질 값 기록
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### R2. 새벽 트럭 짐칸 구멍 — `GMC_Truck_Open.fbx` [A]
- **위치**: `Assets/_Project/Art/DawnTruck20261102/`
- **문제**: 짐칸 왼쪽 안쪽 벽에 작은 검은 삼각형 구멍 몇 개(위 · 뒤에서 보임). TASKS "새벽 트럭 하차 연출" 남은 결함.
- **방법**: Blender에서 구멍 난 면을 메우고 노멀 방향 통일 → ART_SPEC 1-4 규칙으로 같은 이름 내보내기. 부품 이름 · 재질 슬롯 유지.

검수:
- [ ] 위 · 뒤 · 안쪽에서 구멍 없음
- [ ] 하차 경사로(`DawnTruck_RearRamp`) 끝이 땅에 닿고 통로를 막지 않음
- [ ] 메뉴 `FiveRivers/Setup/Build Dawn Truck Prefab` 다시 실행 후 새벽 구간에서 학생병 4명 하차 정상
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### R3. 박용팔 셔츠 노멀 맵 다시 굽기 [B]
- **위치**: `Assets/_Project/Art/SurfaceDetail20261030/` (`SK_CH_ROK_ParkYongpal_URPLab_Baked_Body.mat`이 쓰는 노멀 맵)
- **문제**: 노멀 맵이 조각난 메시 기준으로 구워져 있어, 메시를 매끈하게 고친 뒤 세기를 1→0.3으로 낮춰 임시로 막았다. 천 주름이 약해졌다.
- **방법**: 수정 메시(`SurfaceDetail20261110/ROK_ParkYongpal_SkinFix.asset`) 기준으로 노멀 맵을 다시 굽고, 같은 이름으로 덮어쓴 뒤 세기를 1로 되돌린다.

검수:
- [ ] 세기 1에서도 삼각형 무늬 없음, 천 주름은 보임
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### R4. 바주카 형식 확정 — `SM_WP_Bazooka.fbx` [B]
- ART_SPEC 291행 "⬜ D 확정"이 아직 비어 있다. 1950년 8월에는 M9A1(2.36인치)과 M20(3.5인치)을 함께 썼다. 전차를 멈추는 장면이라 **M20 추천**.
- D가 정하면 날짜와 이유를 ART_SPEC에 적고, 현재 모델이 그 형식과 맞는지(포신 지름 · 길이 · 손잡이 위치)만 확인한다.
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### R5. 전차 요청서 정리 [C]
- 11-05에 전차를 `SM_VH_T34_85.fbx`(Hull / Turret / Gun / TrackL / TrackR, 빨간 별 + 흰 432를 구워 넣음)로 바꿨다. 재현 스크립트 `Docs/Art/T34_Varco432_20261105/build_t34_varco432.py`.
- ART_SPEC의 `T_VH_T34_Body_BaseColor` · `M_VH_T34_Body` 항목을 실제 파일 이름과 슬롯에 맞게 고치고 검수 칸을 채운다.

### R6. ART_SPEC 검수 30칸 기록 [A]
- 실행 빌드(개발 빌드)에서 각 항목의 "검수" 체크를 보고 `결과: ✅ 합격` 또는 `🔁 수정 요청 — 메모`로 바꾼다.
- 어두운 구간(정찰 · 주 공세), 조명탄 아래, 새벽 역광 세 조건에서 실루엣을 본다 (ART_SPEC 1순위 목표).
- 수정 요청이 나온 항목은 이 문서 1번 표에 `R7~` 번호로 추가한다.

## 3. 범위 밖 (이번에 만들지 않음)
- 논 · 마을 맵 (GDD 9번), 메가스캔 "수정 후 사용" 12종, 새 무기 · 새 맵
