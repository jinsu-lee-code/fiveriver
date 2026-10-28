# ART_SPEC 5~8 · 아트 납품 (2026-10-28)

`art` 브랜치에 최신 `origin/main`을 먼저 병합한 후, 최종 검수 전달본의 모델·텍스처·재질만 반영했습니다. ZIP·분할 ZIP·테스트 캐시·구버전 후보는 커밋하지 않습니다.

| 항목 | Unity 프로젝트 내 파일 |
|---|---|
| 5 · 1인칭 팔 | Assets/_Project/Art/Models/Characters/ROK_Arms/SK_CH_ROK_Arms.fbx |
| 6 · M1 개런드 | Assets/_Project/Art/Models/Weapons/M1Garand/SM_WP_M1Garand.fbx |
| 7 · 8발 클립 | Assets/_Project/Art/Models/Weapons/M1Garand/SM_WP_M1Garand_Clip.fbx |
| 8 · 빈 클립 | Assets/_Project/Art/Models/Weapons/M1Garand/SM_WP_M1Garand_ClipEmpty.fbx |

기존 모델·재질의 `.meta` 파일 전체를 보존했습니다. M1 재질은 기존 `Art/Materials/`를 사용하고, 팔은 타 병사의 재질을 바꾸지 않도록 모델 폴더의 전용 재질을 사용합니다. `ROK_Arms_Poses.json`은 검수된 M1·바주카·기본 자세 참고 데이터이며, 현재 프로젝트가 자동으로 읽는다는 뜻은 아닙니다.

원래 전달 ZIP의 런타임 연결 스크립트와 프리팹은 이전 프로토타입용이므로 이 프로젝트로 복사하지 않았습니다. Blender 원본 2개는 팀에 전달한 `FiveRivers_M1_Arms_Assets_20261027.zip`의 `Source/`에 보관하며 Git에 중복 업로드하지 않습니다. 기존 VARCO 참고 원본은 `Assets/_Project/_Source/SM_WP_M1Garand/Raw~/`에 두었습니다. `~` 폴더는 Unity 자동 임포트 대상에서 제외됩니다.

## main 통합 담당자의 확인 순서

1. Unity 6000.3.25f1 / URP 17.3.0을 유지한 채 재임포트합니다.
2. 기존 모델 GUID는 유지했지만 FBX의 하위 메시 ID는 교체 전과 다를 수 있으므로, 씬·프리팹의 MeshFilter/SkinnedMeshRenderer에 Missing이 없는지 확인합니다.
3. 모델 Importer의 Material Remap에서 `M_WP_M1_Wood`, `M_WP_M1_Metal`, `M_WP_M1_Clip`, `M_WP_M1_Brass`를 같은 이름의 `Art/Materials` 재질에 연결합니다. 기존 매핑은 그대로 보존했으며 신규 슬롯 매핑은 이 프로젝트에서 미확인입니다.
4. `ArtSpecTests` 및 기존 테스트를 실행합니다. 기본 자세·Humanoid 매핑·Walk 프레임 1~83·손가락 포즈 연결은 새 프로젝트에서 확인해야 합니다. 검수 당시 팔 프레임 0은 기본 자세였습니다.
5. 실제 M1 부품 위치에 맞게 총구·클립 소켓 및 볼트 이동축을 확인하고, 조준→8발 사격→장전→빈 클립 배출을 실행 빌드에서 확인합니다. 이전 그레이박스의 하드코딩된 소켓 위치를 그대로 합격 처리하지 않습니다.

동봉 체크리스트의 완료 표시는 독립 검수 프로젝트(6000.6.0f1 / URP 17.6.0)의 결과입니다. 이 저장소에서는 Unity를 실행하지 않았으며, 6000.3.25f1 호환성과 게임 최종 연결은 확인 대기입니다. 오른손 약 0.014mm의 미세 접촉, C2 직접 UI 확인, 병사 소매 일치 및 내부 클립 맞물림 등 기존 미확인 항목은 그대로 유지합니다.
