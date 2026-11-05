# PC 정제 다부동 맵 이관·통합 기록

사용자가 지정한 PC의 다듬은 완성 작업본을 선택했다. 이전 GitHub 초기 맵 `Art/Maps/Dabudong/SM_ENV_Dabudong_Map.prefab`은 현행 적용 대상에서 제외하고 보존한다. 이전 납품 기록과 이미지가 현재 맵을 설명하는 것으로 혼동되지 않도록 이 문서를 현행 기록으로 둔다.

## 출처와 이관 범위

- 원본 프로젝트: `C:/Users/mbc/Documents/user/Forgotten War/다섯개의강`.
- 원본 씬: `Assets/_Project/Scenes/Missions/M01_Dabudong.unity`.
- 근거 작업 기록: `Terrain_Ridges_20261023/STATUS.md`, `Historical_Position_20261023/STATUS.md`, `Environment_Characters_20261023/STATUS_20261023.md`.
- 적용 프리팹: `five_river/Assets/_Project/Art/Maps/DabudongRefined/SM_ENV_Dabudong_Refined.prefab`.
- 포함 루트: `M01_Greybox`, `M01_Environment`, `M01_Art_Aug1950`, 비활성 상태의 `Vegetation`.
- 카메라·조명·후처리·게임 MonoBehaviour·마커·경로·엔딩 연출은 제외했다. 무기·캐릭터·오디오는 맵 납품에 포함하지 않았다. `Blanket_Body`는 원본 담요 환경 소품이다.
- 원본을 읽기 전용으로 복사한 격리 프로젝트에서 Unity 6000.6.7f1로 추출했다. 실제 게임 프로젝트는 6000.3.25f1을 유지한다.

AssetDatabase 의존성을 따라 87개 에셋, 추출 시 합계 115,883,573바이트를 선별했다. 62개를 새로 설치하고, 파일 및 meta 바이트가 이미 동일한 25개는 GUID로 기존 경로를 재사용했다. GUID 충돌 0이며 기존 파일 삭제·덮어쓰기는 없다. 재사용 에셋 일부가 이전 `Dabudong/Source`에 있으므로 그 폴더를 일괄 삭제하면 안 된다. 전체 제작 후보나 다른 모델 묶음을 복사하지 않았다.

추출 상태는 오브젝트 2,705개, Renderer 2,260개, Terrain 1개, Collider 731개, LODGroup 279개다. 이 숫자는 비활성 오브젝트와 모든 LOD를 포함한 자산 구성 수이며 한 프레임에 동시에 렌더링되는 수가 아니다. 누락 스크립트·메시·재질은 0이다.

## 초기 맵과 실제 정제 작업본의 차이

| 항목 | 이전 초기 맵 | 현재 정제 작업본 |
|---|---|---|
| 지형 | Realistic/M01_TerrainData | TerrainRidges20261023/M01_Continuous_Ridges_Reviewed |
| Terrain 크기 | 135×30×180 | 135×64×180 |
| 지형 원점 | 기존 초기 배치 | (-60,-0.65,-60) |
| 참호 | 고정 바닥 충돌체 y=0.02 사용 | 연속 Terrain을 파낸 바닥, 중심 y=-0.370686 |
| 배경 | 기존 Apron | 기존 Apron 비활성, Ridges_Backdrop 사용 |
| 정제 배치 | 최신 아트 루트 없음 | M01_Art_Aug1950 및 다듬은 소나무·관목·모래주머니·흙 진지 포함 |

원본 월드 배치·메시·LOD·머티리얼을 유지했고, 프리팹 루트는 위치0·회전0·크기1로 적용했다. 원본 `A_Floor`는 Renderer와 Collider가 모두 꺼져 있다. `Cover/CP01..18`의 숨긴 Renderer와 활성 엄폐 BoxCollider는 보존했다. 이전 목재 벽·Apron·Vegetation을 임의로 다시 켜지 않았다.

## Unity 6000.3 통합 검증

새 TerrainData는 높이·알파맵·구멍·디테일 밀도·레이어 참조·프로토타입을 중립 파일로 내보내고 6000.3.25f1에서 다시 생성했다. 최초 재구성의 저장 전 메모리 검사에서는 높이/알파맵 최대 오차0, 구멍/디테일 불일치0이었다. 이후 저장된 알파맵이 잔디 레이어100%로 초기화된 문제를 발견했으며, 아래 PDF 대조 및 수정 기록을 따른다. 크기135×64×180, 높이513×513, 알파512×512×4, 디테일512×512(프로토타입1, 밀도 합476,249), 나무0, 지형 구멍0을 유지한다. `.meta` GUID를 보존해 프리팹 참조를 복구했다.

`ImportedMapSetup`은 정제 프리팹을 선택하고 이전 인스턴스의 실제 프리팹 경로가 다르면 교체한다. 플레이어·동료·보급 위치에 남아 있던 고정 참호 y값을 제거하고 Terrain 높이+0.04로 정렬했다. 적/엄폐/퇴각 포인트는 새 지형의 NavMesh에 정렬한다.

저장된 `unity_6000_3_map_validation.json` 결과: 적용 인스턴스1, 이전 생성 지오메트리0, Terrain1, 스크립트·참조·메시·재질 누락0, 경로98/98 완료. 플레이어 바닥은 새 Terrain이며 여유 높이0.0399998724m다. 전차 각도는 정면19.8563°/체크포인트35.0717°/측면 둔덕55.9532°로, 측면 판정 기준50°를 충족한다.

통합 담당자가 Play 프레임1275→3725 진행을 확인했다. `map_applied_game.png`는 이 단계의 실제 Game 화면이다. 이 이미지는 맵 적용 확인용이며 후속 M1 수정·최종 빌드 상태를 대신하지 않는다. 독립 빌드 FPS나 전체 엔딩 통과 결과는 이 맵 문서에서 주장하지 않는다.

## 보수적 최적화 감사

| 측정 항목 | 검사 전 | 검사 후 | 처리 |
|---|---:|---:|---|
| CPU Read/Write가 켜진 텍스처 | 0/35 | 0/35 | 이미 꺼져 있어 추가 재임포트 없음 |
| 실제 환경용 instancing 설정 | 15/15 on | 15/15 on | 기존 설정 유지 |
| 숨긴 graybox용 재질 | 3개 off | 3개 off | 비렌더링 대상이므로 효과 없는 변경 생략 |
| LODGroup | 279 | 279 | 정점·LOD 임계값·LOD 개수 변경 없음 |
| 모델 importer Read/Write | 4개 중3개 on | 동일 | runtime NavMesh/Collider CPU 메시 접근 보호 |

GPU instancing 플래그가 켜져 있다는 사실만으로 실제 draw call 감소를 주장하지 않는다. SRP Batcher·활성 LOD·메시/재질 조합에 따른 실제 배치 수는 별도 프레임 프로파일링 대상이다. 이번 감사로 추가 절감한 CPU 메모리는 0이며 추가 FPS 개선값은 측정하지 않았다. 기존 최적화가 이미 적용된 항목을 다시 변경하지 않았다. 상세 importer·재질 값은 `refined_static_optimization_audit.json`에 있다.

## 현재 게임 포인트

저장된 `Assets/_Project/Scenes/Dabudong.unity`의 LevelPoints를 읽어 월드 위치를 기록했다. 부모 Transform의 identity 회전/크기를 확인한 정적 스냅샷이며, 적 관련 y는 NavMesh 정렬 후 값이다. 원본 마커 값과 혼동하지 않는다. 전체 회전도 `refined_mapped_points.json`에 보존했다.

| 필드 / 이름 | 월드 x, y, z |
|---|---|
| playerSpawn / PlayerSpawn | 0.000000, -0.330686, -0.500000 |
| tankCheckpoint / TankCheckpoint | 17.000000, 0.009178, -1.000000 |
| moundTop / MoundTop | 28.000000, 1.569618, -6.000000 |
| enemySpawns / EnemySpawn_0 | -20.000000, 19.224546, 99.000000 |
| enemySpawns / EnemySpawn_1 | -10.000000, 18.606690, 101.000000 |
| enemySpawns / EnemySpawn_2 | 0.000000, 16.731860, 99.000000 |
| enemySpawns / EnemySpawn_3 | 17.000000, 10.145257, 101.000000 |
| enemySpawns / EnemySpawn_4 | 25.000000, 7.964951, 99.000000 |
| scoutCovers / ScoutCover_0 | -22.000000, 9.052322, 64.000000 |
| scoutCovers / ScoutCover_1 | -4.000000, 10.077608, 68.000000 |
| assaultCovers / AssaultCover_0 | -18.000000, 6.635059, 50.000000 |
| assaultCovers / AssaultCover_1 | 3.000000, 6.830806, 54.000000 |
| assaultCovers / AssaultCover_2 | 20.000000, 6.236785, 52.000000 |
| assaultCovers / AssaultCover_3 | -20.000000, 5.135263, 38.000000 |
| assaultCovers / AssaultCover_4 | -2.000000, 4.905627, 40.000000 |
| assaultCovers / AssaultCover_5 | 26.000000, 4.499410, 40.000000 |
| assaultCovers / AssaultCover_6 | -16.000000, 3.680865, 29.000000 |
| assaultCovers / AssaultCover_7 | 0.000000, 3.130793, 27.999998 |
| assaultCovers / AssaultCover_8 | 21.000000, 2.474003, 30.000000 |
| assaultCovers / AssaultCover_9 | 24.000000, 9.110737, 66.000000 |
| retreatPoints / Retreat_0 | -20.000000, 21.377995, 107.000000 |
| retreatPoints / Retreat_1 | 0.000000, 19.477470, 108.000000 |
| retreatPoints / Retreat_2 | 25.000000, 6.372414, 107.000000 |
| tankStart / TankStart | 44.766666, 0.009178, 105.000000 |
| tankHold / TankHold | 40.940000, 0.009178, 23.000000 |
| ammoCrates / AmmoCrate_Trench | 5.000000, -0.061442, -1.600000 |
| ammoCrates / AmmoCrate_Left | 17.500000, 0.009178, -1.600000 |
| target / Target | 4.000000, 0.733239, 18.000000 |
| parkSpot / ParkSpot | -6.500000, 0.009178, -1.200000 |
| blanketSpot / BlanketSpot | -9.500000, 0.009178, -1.500000 |
| allySpots / AllySpot_1 | -14.000000, -0.284080, -0.400000 |
| allySpots / AllySpot_2 | 11.000000, 0.009178, -0.400000 |
| allySpots / AllySpot_3 | 16.000000, 0.009178, -0.400000 |
| reinforcementsStart / ReinforcementsStart | 27.000000, 0.987276, -35.000000 |
| reinforcementsEnd / ReinforcementsEnd | 23.000000, 0.009178, -1.000000 |

## PDF 이미지 원본 추적 및 알파맵 저장 문제 수정

제출 PDF 6페이지 왼쪽 맵 이미지의 정확한 원본은 `C:/Users/mbc/Documents/ChatGPT/다섯개의강/Terrain_Ridges_20261023/Evidence/Editor_Overview_After.png`이다. `verify_submission_ai_effects.py`의 box(7) 이미지 삽입 코드에서 확인했다. 이미지 파일명만으로 다른 맵을 추측하지 않고 원본 씬·배치·실제 표면을 대조했다.

- PC 원본 씬과 추출에 사용한 복사본 씬의 SHA-256은 모두 `cfa11db5623540de33b3314c7dee094626c27cc2dec3f670ed463f2c342d2ec0`이다.
- M01_Art_Aug1950 직속 프리팹279개를 이름·좌표로 매칭했고 누락0, 최대 좌표차0.000004357m였다. 세부 결과는 `pdf_map_source_identity.json`에 있다.
- 원본/대상 Ridges_Backdrop.asset의 SHA-256은 모두 `c53bbe8e6ba3150409122fcf3a7b83890d96b8e4900d406f9818b8723b231821`이다.
- 지형 레이어4개, 맵 텍스처35개, 재질18개의 파일과 meta는 원본과 바이트가 동일했다. portable 레이어 GUID가 옛 맵의 다른 레이어로 충돌한 것이 아니었다.

같은 카메라 위치(-35,27,-30), 바라보는 위치(0,0,30), FOV60과 원본의 주간 검사 조명으로 렌더하자 형상은 같지만 지표면만 녹색 위장 무늬였다. 원인은 조명만이 아니었다. 저장 전 메모리에서 검증한 알파맵이 TerrainData 자산 생성/저장 뒤 잔디100% `(1,0,0,0)`로 바뀌었다. 원본의 평균 비율은 잔디14.2021%·마른 흙76.5003%·바위7.7172%·흙1.5836%다.

이미 영속화된 TerrainData에 원본 알파맵을 다시 설정하고 저장·강제 재임포트했다. 수정 전 최대 알파 오차1에서 수정 후0이 되었으며, 샘플(256,256)이 원본 `(0.1882353,0.8117648,0,0)`과 같아졌다. 재렌더한 `pdf_map_same_camera_after_alpha_restore.png`에서 갈색 지형 복원을 확인했다. 형상·메시·UV를 수정하거나 새로운 지면을 만든 작업이 아니다.

변환 helper는 이제 최종 저장·재임포트 후 높이/알파/holes/detail 전체 대조를 수행하도록 보완했다. 별도 `ValidatePersistedRefinedTerrain.cs.txt`는 현재 자산을 수정하지 않고 portable 원본과 대조하고 `Temp/refined_terrain_persisted_validation.json`을 출력한다. 최종 저장 후 검사 수치는 이 독립 검사 결과로 판단한다. 앞선 저장 전 검증을 저장 후 검증으로 혼동하지 않는다.

PDF 구도 비교 이미지는 원본 조명으로 진단하기 위해 임시 렌더한 것이다. 실제 게임의 밤→새벽 조명 연출을 모두 주간으로 바꿨다는 뜻은 아니다.

최종 저장 후 독립 대조도 완료했다. `refined_terrain_persisted_validation.json`의 결과는 높이 최대 오차0·알파 최대 오차0·구멍 불일치0·디테일 불일치0·레이어 참조 불일치0이며, 디테일 밀도 합476,249와 네 레이어 가중치 합도 원본과 일치한다. 이 결과는 최초 저장 전 검사를 대신하는 최종 지형 데이터 검증이다.
