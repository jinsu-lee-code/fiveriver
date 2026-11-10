# 박용팔 피부(셔츠 법선 · 어깨 가중치) 수정 — 2026-11-10

사격·앞에총 자세에서 박용팔의 어깨·등·소매가 각지게 뭉개지던 것을 Blender에서 고친 자료입니다.

- 원인 1: 셔츠(서브메시 0)가 면마다 정점이 갈라져 있어(삼각형 13,708 · 정점 35,953 · 실제 위치 6,861) 법선이 위치마다 평균 18.7° 어긋남 → 몸이 접히면 구겨진 면처럼 보임
- 원인 2: 어깨 회전이 좁은 띠에 몰려 앞에총 자세에서 윗팔 소매가 깊게 접힘

## 파일
| 파일 | 내용 |
|---|---|
| `fix_park_skin.py` | Blender 5.2 백그라운드 스크립트. 셔츠 법선을 같은 위치끼리 면적 가중 평균으로 합치고, 어깨·등·팔 가중치를 UV 이음새를 묶은 채 100회 다듬음. 전후 렌더 `r_*.png`와 `report.txt` 생성 |
| `mesh.bin` · `bones.txt` · `materials.txt` | Unity 메시(`ROK_ParkYongpal_SmoothArmSkin`)를 정점 번호 그대로 덤프한 것 |
| `pose_idle/fire/carry.bin` | 평상시·사격·앞에총 순간의 스킨 행렬 |
| `weights_fixed.bin` · `normals_fixed.bin` | 결과(Unity 정점 번호 순서) |
| `unity_dump_*.cs.txt` | 위 덤프를 만든 Unity 스크립트 (script eval용) |

## 다시 만들기
1. (메시나 자세가 바뀐 경우만) Unity에서 `unity_dump_*.cs.txt`의 `<data_dir>`를 이 폴더로 바꿔 실행
2. `blender -b --python fix_park_skin.py -- <이 폴더> 100 0.5`
3. Unity 메뉴 **FiveRivers/Characters/Apply Park Skin Fix (Blender)** → `Art/SurfaceDetail20261110/ROK_ParkYongpal_SkinFix.asset`을 만들어 박용팔에 연결하고 장면 저장

되돌리기: 박용팔 `Body_Low`의 Mesh를 `SurfaceDetail20261030/ROK_ParkYongpal_SmoothArmSkin`으로 바꾸고 장면 저장.
