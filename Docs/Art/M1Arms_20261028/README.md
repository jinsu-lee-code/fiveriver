# ART_SPEC 5~8 · 아트 납품 및 Unity 확인

2026-10-28 · `fiveriver/art` · Unity 6000.3.25f1 / URP 17.3.0

[현재 체크리스트](ART_SPEC_작업완료_체크리스트.md) · [시각 체크리스트](ART_SPEC_작업완료_체크리스트.html) · [실측 JSON](UNITY_VERIFICATION.json)

4모델을 ART_SPEC 이름·폴더에 반영했고, 지정 Unity 버전에서 임포트·정적 이미지와 기존 참조를 검사했습니다. ArtSpecTests 15/15 통과, 검사한 기존 19프리팹 및 Dabudong 씬 참조 Missing 0입니다. 모델·재질의 기존 GUID는 유지했습니다. 장전 클립 FBX만 Unity Importer에서 금속·황동 재질 remap 2개를 추가했습니다. 다른 기존 .meta는 변경하지 않았습니다.

모델 경로는 `five_river/Assets/_Project/Art/Models/Characters/ROK_Arms/SK_CH_ROK_Arms.fbx`와 `Weapons/M1Garand/SM_WP_M1Garand{,_Clip,_ClipEmpty}.fbx`입니다. 무기 재질은 `Art/Materials/`, 팔 전용 재질은 팔 모델 폴더에 있습니다. `ROK_Arms_Poses.json`은 검수된 포즈의 참고 데이터이며 현재 게임이 자동으로 읽는다는 뜻이 아닙니다.

## main 통합 담당자에게 남은 일

1. 현재 씬에는 팔이 없으므로 팔 리그와 게임의 FirstPersonAnimatorBridge를 연결합니다. 이전 프로토타입용 연결 스크립트·프리팹은 이번 art 납품에 복사하지 않았습니다.
2. VM_M1의 임시 RearSight·FrontSight와 기존 하드코딩 소켓 위치를 최종 모델 기준으로 정리합니다. 이번 확인에서 게임 코드·씬·프리팹은 수정하지 않았습니다.
3. Configure Avatar의 초록 UI와 실제 Walk 83프레임 변형, 소매 일치를 확인합니다.
4. 실행 빌드에서 파지→ADS→8발 사격→장전→빈 클립 배출과 야간 가시성을 확인합니다. 이번 정적 임포트 확인과 이전 6000.6 검수는 최종 게임 통합을 대체하지 않습니다.

콘솔은 검사 시 오류 0 / 경고 2입니다. 팔 애니메이션 임포트 경고와 Unity AI Account API 경고를 숨기거나 지우지 않았습니다.

VARCO 참고 원본은 `Assets/_Project/_Source/SM_WP_M1Garand/Raw~/`에 보관합니다. Blender 원본 2개는 팀에 이미 보낸 `FiveRivers_M1_Arms_Assets_20261027.zip`의 `Source/`에 있으며 중복 ZIP·중간 모델·검증 캐시는 커밋하지 않습니다.
