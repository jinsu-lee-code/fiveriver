# 일반 아군 피부(셔츠 · 조끼 법선 · 어깨 가중치) 수정 — 2026-11-10

ART_REQUEST_20261110 R1. 박용팔(`tools/park_skin_fix`, 11-10)과 같은 방법을 일반 아군 3명(`Ally_1~3`, 메시 `ROK_Regular_NoCap_20261105`)에 적용한 자료입니다.

- 원인: 셔츠(서브메시 0 `URPLab_Baked_Body`)와 조끼(서브메시 3 `URPLab_Baked_TecticalVest`)가 면마다 정점이 갈라져 법선이 위치마다 어긋남 — 평균 몸 14.9°, 조끼 20.8°(요청서 14.1° · 19.5°와 같은 경향, 계산 방식 차이). 팔을 들면 어깨 · 소매에 각진 그늘과 삼각형 무늬
- 결과: 셔츠 법선 평균 14.5°, 조끼 20.0° 바뀜. 어깨 · 등 · 팔 가중치 다듬기 후 심하게 찌그러진 면(면적 25% 미만) — 평상시 58 → 4, 사격 43 → 36, 앞에총 42 → 8 (`report.txt`)

## 박용팔 방식과 다른 점
| 항목 | 박용팔 | 일반 아군 |
|---|---|---|
| 법선을 다듬는 서브메시 | 0 (셔츠) | 0 (셔츠) + 3 (조끼) |
| 같은 위치 묶기 단위 | 1e-5 | 1e-7 — 메시 공간에서 키가 약 1 cm라 1e-5는 실제 이웃 정점까지 묶음 |
| 앞에총 자세 | 컨트롤러의 `Run_Rifle` | 아군 컨트롤러(`AC_ROK` 오버라이드)에 없어 같은 클립 `A_KPA_Run_Rifle`을 0.25 지점에서 샘플(`pose_carry_runrifle.bin`) |
| Unity 반영 | 메뉴 `Apply Park Skin Fix`가 에셋 생성 + 연결 + 장면 저장 | art 브랜치라 **에셋만** 만들고 연결하지 않음(코드 · 씬 · 프리팹은 main) |

## 파일
| 파일 | 내용 |
|---|---|
| `fix_regular_skin.py` | Blender 5.2 백그라운드 스크립트(`fix_park_skin.py`에 위 차이만 매개변수로) |
| `mesh.bin` · `bones.txt` · `materials.txt` · `uv_tangent.bin` | Unity 메시(Ally_1 `Body_Low`)를 정점 번호 그대로 덤프. 형식은 `park_skin_fix/unity_dump_mesh.cs.txt`와 같고, `uv_tangent.bin`은 int nv + nv×(u,v) + nv×(x,y,z,w) |
| `pose_idle/fire/carry/carry_runrifle.bin` | 평상시(Idle_Stand) · 사격(Fire_Crouch) · 걷기(Walk_Tired, 참고용) · 앞에총(Run_Rifle 클립)의 스킨 행렬 |
| `weights_fixed.bin` · `normals_fixed.bin` | 결과(Unity 정점 번호 순서) |
| `r_fire_*` · `r_carry_*` | 사격 · 앞에총 자세 전후 렌더(같은 각도 3장씩) |

## 다시 만들기
1. (메시나 자세가 바뀐 경우만) Unity에서 `park_skin_fix/unity_dump_*.cs.txt`의 대상을 `Ally_1`로, `<data_dir>`를 이 폴더로 바꿔 실행
2. `blender -b --python fix_regular_skin.py -- <이 폴더> 100 0.5`
3. Unity에서 `ROK_Regular_NoCap_20261105`를 복사해 `weights_fixed.bin` · `normals_fixed.bin`을 넣고(읽는 방법은 `Scripts/Editor/ParkSkinFix.cs`의 ReadWeights · ReadNormals와 같음), 탄젠트는 서브메시 0 · 3 정점만 새로 계산해 `Art/CharacterVariants/RegularHelmet20261105/ROK_Regular_NoCap_SkinFix.asset`으로 저장

## main 연결 (art에서는 하지 않음)
아군 3명 `[Map]/Generated/People/Ally_1~3/Model/Body_Low`의 SkinnedMeshRenderer Mesh만 `ROK_Regular_NoCap_SkinFix`로 바꾸고 장면 저장. 정점 · 서브메시 · 뼈 · bindpose · UV가 원본과 같아 다른 설정은 그대로 둔다. 되돌리기: Mesh를 `ROK_Regular_NoCap_20261105`로.
