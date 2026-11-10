# 캐릭터 삼각형 줄이기 — 2026-11-10

사용자 요청 "삼각형 너무 많은건 blender로 줄여줘"에서 "이번 아트 요청서에 있던 캐릭터만" 줄인 자료입니다. 대상은 ART_REQUEST_20261110의 R1(일반 아군)과 R3(박용팔)이고, 목표는 ART_SPEC 캐릭터 예산 1~2만 삼각형입니다.

| 메시 | 전 | 후 |
|---|---|---|
| `SurfaceDetail20261110/ROK_ParkYongpal_SkinFix.asset` (박용팔) | 62,925 삼각형 · 145,960 정점 | 19,996 삼각형 · 46,870 정점 |
| `CharacterVariants/RegularHelmet20261105/ROK_Regular_NoCap_SkinFix.asset` (일반 아군) | 94,271 삼각형 · 200,845 정점 | 19,997 삼각형 · 52,569 정점 |

서브메시별 수와 검사 값은 `park/report.txt`, `regular/report.txt`에 있습니다.

## 방법 (`reduce_character.py`, Blender 5.2 백그라운드)
1. 같은 위치의 정점을 합칩니다(서브메시별). UV는 면 모서리에 그대로 두어 UV 이음새는 이음새로 남습니다.
2. Collapse 방식으로 줄입니다. 서브메시마다 같은 비율(목표 ÷ 전체)을 적용하고, 뼈 가중치는 정점 그룹으로 따라갑니다.
3. 법선은 새로 계산하지 않습니다. 새 면의 각 모서리가 가장 가까운 원래 면의 법선(SkinFix에서 고친 값)을 받으므로 셰이딩과 각진 모서리가 그대로입니다.
4. 뼈 가중치는 정점마다 큰 것 4개를 쓰고 합을 1로 맞춥니다.

## 유지되는 것
- 에셋 파일, GUID, fileID 4300000
- 서브메시 수(일반 아군의 빈 서브메시 1 포함)
- 뼈 수와 bindpose
- 재질 슬롯
- UV 배치(텍스처 그대로 사용)
- SkinFix 법선과 가중치

탄젠트는 Unity에서 다시 계산합니다.

정점 수와 정점 번호는 바뀝니다. 그래서 `park_skin_fix` · `regular_skin_fix`의 `*_fixed.bin`(원래 정점 번호 기준)은 이 메시에 다시 적용할 수 없습니다. 그 수정은 이미 줄이기 전에 들어가 있습니다.

## 파일
| 파일 | 내용 |
|---|---|
| `<대상>/src.bin` | Unity에서 덤프한 줄이기 전 SkinFix 메시. 형식: int nv, nv×float3 위치, nv×float3 법선, nv×float4 탄젠트, nv×(int4 뼈, float4 가중치), int 8 + 채널마다 int has(+nv×float2), int hasColors, int 서브메시 수 + (int 인덱스 수 + 인덱스), int bindpose 수 + 4x4(행 우선) |
| `<대상>/out.bin` | 줄인 결과(같은 형식, 탄젠트 0) |
| `<대상>/info.txt` | 덤프 당시 메시 정보(GUID, 서브메시, UV 채널 등) |
| `<대상>/report.txt` | 서브메시별 삼각형 수와 검사 값. 원래 정점 → 새 표면 거리(메시 크기 대비 %), 원래 UV 범위 밖 UV 모서리, 뒤집힌 UV 면, 가중치 없는 정점 |
| `<대상>/r_before_*` · `r_after_*` | Blender 렌더(기본 자세, 회색, 같은 각도) |
| `<대상>/u_*` | Unity 캡처(덮어쓴 뒤) |

## 다시 만들기
1. (메시가 바뀐 경우만) Unity에서 SkinFix 메시를 위 형식의 `src.bin`으로 덤프합니다.
2. `blender -b --factory-startup --python reduce_character.py -- <대상 폴더 절대 경로> 20000`
3. Unity에서 `out.bin`을 읽어 같은 메시 에셋에 덮어씁니다.
   - `mesh.Clear()` 뒤 위치, 법선, UV0, boneWeights, bindposes, 서브메시별 삼각형을 넣습니다.
   - `RecalculateTangents` · `RecalculateBounds`를 실행합니다.
   - `SetDirty` · `SaveAssets`로 저장합니다.

되돌리기: git에서 두 `.asset`을 이 커밋 이전으로 되돌리면 됩니다.
