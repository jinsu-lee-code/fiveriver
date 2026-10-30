# 다부동 환경 (Blender → Unity)

1950년 8월 다부동 328고지 일대 환경 맵입니다. Blender에서 만든 것을 Unity(URP)로 옮겼습니다.
사람·무기·시신·불·연기는 넣지 않았습니다.

## 씬 만들기
1. Unity에서 메뉴 **FiveRiver → 다부동 환경 씬 만들기**를 누릅니다.
2. 이 폴더 안에 `Dabudong_Env.unity`가 만들어집니다(전에 `Assets/_Project/Scenes`에 만든 적이 있으면 그 자리). 재질·프리팹·지형 데이터는 `Generated/`에 생깁니다.

다른 씬(게임 빌드 씬 포함)은 건드리지 않습니다.

## 들어 있는 것
| 경로 | 내용 |
|---|---|
| `Terrain/height_2049.raw` | 높이맵 520 m × 520 m, 16비트, 약 0.25 m 간격 |
| `Terrain/splat*_1024.raw` | 지형 텍스처 8층 가중치 |
| `Terrain/detail_*_1024.raw` | 풀·벼·잔돌·덤불 디테일 개수(칸당) |
| `Models/ENV_*.fbx` | 에셋 25종(그루터기, 적송·고사목·어린 소나무, 화강암, 모래주머니, 개인호, 벽널, 철조망, 초가, 짚더미, 옹기, 달구지, 전신주, 드럼통, 풀·벼·잔돌 등). 소나무·덤불은 `_LOD1` 포함 |
| `Models/Terrain_Far.fbx` | 원경 산(9 km), 텍스처 구움 |
| `Models/Terrain_Trench.fbx`, `Terrain_CommTrench.fbx` | 교통호·연락호 고해상도 지형 조각(지형 위에 얹음, 콜라이더 포함) |
| `Textures/` | 구운 바탕색·노멀, 지형층 텍스처 |
| `dabudong_instances.csv` | 배치 3,176개(위치·회전·크기, Unity 좌표) |
| `dabudong_scene.json`, `dabudong_assets.json` | 지형·빛·안개·카메라 정보, 에셋 목록 |

## 출처
- 지형 텍스처: [Poly Haven](https://polyhaven.com) (CC0)
- 에셋: VARCO 3D 생성물을 Blender에서 다듬음. 풀·덤불·소나무·벼·잔돌은 Blender에서 직접 만듦

## 알아 둘 점
- 참호 조각 위에는 풀 디테일이 없습니다(지형 디테일은 조각 밑 지형에만 깔림).
- 디테일 거리는 150 m입니다. 그보다 먼 논은 지형층 `Rice`의 초록색으로 보입니다.
- 다시 만들려면 Blender 쪽 `Dabudong_Map/scripts/export_unity.py`를 단계별(terrain · assets · meshes)로 실행합니다.
