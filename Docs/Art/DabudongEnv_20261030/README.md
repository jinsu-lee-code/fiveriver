# 다부동 환경 맵 (Blender 제작) 아트 납품

1950년 8월 다부동 328고지 일대를 Blender에서 새로 만든 환경 맵입니다. 사람·무기·시신·불·연기는 넣지 않았습니다.
같은 날 들어온 `Art/Maps/Dabudong/`(예전 M01 장면을 옮긴 맵)과는 별개이며, 경로가 겹치지 않습니다.

- 자료 폴더: `five_river/Assets/_Project/Art/Maps/Dabudong_Env/`
- 씬 조립 스크립트: 같은 폴더의 `Editor/DabudongEnvImporter.cs` (메뉴 **FiveRiver → 다부동 환경 씬 만들기**)
- 확인용 렌더: `before_after.png`(왼쪽 예전 장면, 오른쪽 이번 맵), `view_valley.png`, `view_trench.png`, `view_village.png` (Blender 렌더)

## 들어 있는 것
| 경로 | 내용 |
|---|---|
| `Terrain/height_2049.raw` | 높이맵 520 m × 520 m, 16비트, 약 0.25 m 간격 |
| `Terrain/splat*_1024.raw` | 지형 텍스처 8층 가중치 |
| `Terrain/detail_*_1024.raw` | 풀·벼·잔돌·덤불 디테일(칸당 개수) |
| `Models/ENV_*.fbx` | 에셋 25종(그루터기, 적송·고사목·어린 소나무, 화강암, 모래주머니, 개인호, 벽널, 철조망, 초가, 짚더미, 옹기, 달구지, 전신주, 드럼통, 풀·벼·잔돌). 소나무·덤불은 `_LOD1` 포함 |
| `Models/Terrain_Far.fbx` | 원경 산(9 km) |
| `Models/Terrain_Trench.fbx`, `Terrain_CommTrench.fbx` | 교통호·연락호 고해상도 지형 조각(콜라이더 포함) |
| `Textures/` | 구운 바탕색·노멀, 지형층 텍스처 |
| `dabudong_instances.csv` | 배치 3,176개(위치·회전·크기) |
| `dabudong_scene.json`, `dabudong_assets.json` | 지형·빛·안개·카메라, 에셋 목록 |

파일 113개, 약 170 MB. 가장 큰 파일은 20 MB 미만이며, 기존 맵 납품처럼 Git LFS 없이 실제 파일입니다.

## 씬 만드는 법
1. Unity에서 메뉴 **FiveRiver → 다부동 환경 씬 만들기**(또는 **Tools → Dabudong → 다부동 환경 씬 만들기**)를 누릅니다.
2. `Art/Maps/Dabudong_Env/Generated/`에 재질·프리팹·지형 데이터가, `Art/Maps/Dabudong_Env/Dabudong_Env.unity`에 씬이 생깁니다.
   현행 `Scenes/Dabudong.unity`에는 손대지 않습니다.
3. 씬에는 지형(Terrain 1개), 원경·참호 메시, 배치 3,176개(`Props_*` 그룹), 해·하늘·안개·후처리, 카메라 6개(CAM_Valley만 켜짐)가 들어갑니다.

## 확인 결과와 미확인
- 높이맵·스플랫·디테일은 이미지로 되살려 방향과 내용을 확인했습니다.
- 씬 조립 스크립트는 이 프로젝트의 URP 17.3 런타임 어셈블리를 참조해 컴파일 오류 0건을 확인했습니다.
- **이 프로젝트 기준 Unity 6000.3.25f1에서 메뉴를 실행해 본 확인은 하지 않았습니다.** 같은 스크립트로 Unity 6000.6에서 씬을 만든 적은 있습니다.
- 현재 게임의 이동 경로·전차 경로·체크포인트 연결과 실행 빌드 플레이 검증은 하지 않았습니다(아트 납품 단계).

## 출처
- 지형 텍스처: Poly Haven (CC0)
- 에셋: VARCO 3D 생성물을 Blender에서 다듬음. 풀·덤불·소나무·벼·잔돌은 Blender에서 직접 만듦
- 다시 만들기: Blender 쪽 `Dabudong_Map/scripts/export_unity.py`(terrain · assets · meshes 단계)
