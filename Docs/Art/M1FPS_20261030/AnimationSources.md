# FPS 사격 애니메이션 로컬 원본·출처 조사

2026-10-30. 기존 PC Downloads 및 프로젝트 자산을 읽기 전용으로 조사했다. 새 사이트에서 애니메이션을 다운로드하지 않았다. 구현 담당자가 선택한 사격 입력 원본은 `C:/Users/mbc/Downloads/Seokgu_Mixamo_Input@Firing Rifle (1).fbx`이다. 이 문서는 원본 식별과 동작 데이터의 존재를 증명하며, 최종 Unity 파생 클립·팔 IK·사격 입력 검증을 대신하지 않는다.

## 공식 사용 안내와 확인 범위

Adobe 공식 FAQ는 Mixamo 캐릭터와 애니메이션을 개인·상업·비영리 프로젝트에서 로열티 없이 사용할 수 있으며 비디오 게임을 사용 예로 명시한다. 2026-10-30에 확인했다. [Adobe Mixamo FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html)

개별 파일의 다운로드 영수증이나 당시 계정 기록은 발견하지 못했다. 로컬 파일명, FBX의 `mixamo.com` AnimationStack, `mixamorig:*` 본 명칭, 파일 SHA-256을 출처 증거로 남긴다. 이 기록은 게임 내 활용 범위를 설명하며 원시 애니메이션 라이브러리의 별도 재배포 권한을 단정하지 않는다.

## 로컬 후보의 실제 키프레임

아래 파일은 모두 `C:/Users/mbc/Downloads/`에 있었다. FBX 7700 binary 구조를 직접 읽어 AnimationCurve의 KeyTime/KeyValueFloat를 조사했다. 움직임 곡선은 값의 최대·최소 차이가 0.00001을 초과한 곡선이며, 총 키 수는 모든 곡선의 합이다. 길이는 FBX 키 시간 범위로 Unity의 임포트 구간 자르기에 따라 달라질 수 있다.

| 원본 파일 | 바이트 | 길이(초) | 총 키 | 한 곡선 최대 샘플 | 움직임 곡선/전체 |
|---|---:|---:|---:|---:|---:|
| `Seokgu_Mixamo_Input@Firing Rifle (1).fbx` | 1,547,216 | 1.000 | 9,585 | 31 | 309/315 |
| `Seokgu_Mixamo_Input@Firing Rifle.fbx` | 1,561,984 | 1.167 | 10,815 | 36 | 300/315 |
| `Seokgu_Mixamo_Input@Reloading.fbx` | 637,680 | 3.300 | 31,734 | 178 | 315/315 |
| `Seokgu_Mixamo_Input@Rifle Aiming Idle.fbx` | 596,832 | 3.100 | 28,215 | 94 | 300/315 |
| `Seokgu_Mixamo_Input@Rifle Walk.fbx` | 365,920 | 1.300 | 8,973 | 40 | 222/315 |

각 파일에는 `mixamo.com` 스택과 Mixamo 본65개가 있다. Hips/Spine/Head, 양쪽 Shoulder/Arm/ForeArm/Hand, 손가락 및 다리/발을 포함한다. 따라서 파일명만 존재하거나 한 프레임 포즈만 가진 임시 클립은 아니다. 실제 게임에서 리타깃 결과가 올바른지는 별도 검증해야 한다.

SHA-256:

- `Seokgu_Mixamo_Input@Firing Rifle (1).fbx`: `b8909e47851c0d5545fc570720dac31a7d79ca3e749d40ecbb22a43106cd28de`
- `Seokgu_Mixamo_Input@Firing Rifle.fbx`: `42f37fe05128e7ae0ab426e5eb62f9c90b46d71ae91813633332060da68c1249`
- `Seokgu_Mixamo_Input@Reloading.fbx`: `28bebdfc88943e318326722b079d750c61bfd0e21e8cc927bedb4665e656de20`
- `Seokgu_Mixamo_Input@Rifle Aiming Idle.fbx`: `c53a7953f0bf014ae4f9e30bb24cf98314e4dce8cf517149f5957a9c124c3a02`
- `Seokgu_Mixamo_Input@Rifle Walk.fbx`: `cb44a3eeb29d90c130cac584ebd31c612e2cd0c5e8ee40bbbee037a17bba1e21`

## 채택 사격 소스의 반동·복귀 구간

`Firing Rifle (1)`은 0~0.067초에는 변화가 작고 약0.1초부터 반동이 시작된다. 오른팔의 기준자세 대비 Euler 축 변화 진단은 약0.20초에 최대, 왼팔은 약0.30초에 최대이며 0.8초 이후 복귀해 1.0초에 시작 자세 근처로 돌아온다. 0~0.25초만 잘라 사용하면 반동 최대 부근에서 끝나 복귀 구간이 사라진다.

구현 검토에는 약0.067~1.0초의 반동과 복귀를 함께 이용하고 짧은 단발 시간에 맞춰 재시간화하도록 제안했다. 이는 소스 선택 근거이며 최종 재생 시간은 구현 담당자가 게임 입력과 검증 결과에 맞춰 기록한다. 손목 접촉은 실제 무기 파지점과 IK로 확인해야 한다. 측정한 Euler 축 변화 크기는 Quaternion의 실제 회전각과 동일한 값으로 취급하지 않는다.

## 기존 프로젝트 클립과 팔 호환성

- `Art/Animations/KPA/A_KPA_Fire_Stand.fbx`: 0.267초, 최대9샘플, 움직임267/315곡선. 짧은 실제 사격 후보이며 `mixamo.com` 스택이 있다. importer는 Humanoid다.
- `Art/Animations/ROK/A_ROK_Fire_Crouch.fbx`: 1.033초, 최대32샘플, 움직임306/315곡선. 실제 동작이며 일어선 FPS 자세와의 호환은 별도다.
- `Art/Animations/FP/A_FP_M1_ReloadEmpty.fbx`: 2.5초, 최대76샘플, 움직임199/639곡선. ReloadPartial은3.2초, 최대97샘플, 움직임198/639곡선이다. 이 FP 클립들은 `Scene` 스택의 Generic 클립이며 Mixamo 다운로드 원본으로 단정하지 않는다.
- `Art/Models/Characters/ROK_Arms/SK_CH_ROK_Arms.fbx`: 왼팔·오른팔 메시2개와 Mixamo65본 전체 골격이다. importer는 Humanoid, avatarSetup1이며 본 매핑이 있다. 내장 `Walk_Validation`은 보행 검사 동작이므로 사격 소스로 혼동하지 않는다.
- 기존 FP Generic 클립의 본 이름은 위 팔 골격과 일치하지만, 루트 경로·Avatar·실제 재생은 Unity에서 확인해야 한다. 기존 정제 M1의 `wrist.*` 손 리그에 `mixamorig:*` 곡선을 그대로 붙이면 바인딩이 맞지 않는다.

검사 산출물은 `C:/Users/mbc/Documents/ChatGPT/다섯개의강/Map_Import_20261030/`의 `mixamo_animation_provenance.json`, `inspect_animation_fbx.json`, `mixamo_firing1_pulse_samples.json`, `mixamo_upper_body_rotation_ranges.json`이다. 원본/프로젝트 메시·애니메이션·코드 파일은 이 조사에서 수정하지 않았다.
