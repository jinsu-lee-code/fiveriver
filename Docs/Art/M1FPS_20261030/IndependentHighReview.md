# Astra High 독립 검수 — Claude 인계 상태

최종 상태 정리: 2026-10-30 15:24 KST. 사용자의 Claude 인계문서 요청에 따라 새 확장 작업 없이 검증된 상태와 미확인 부분을 고정한다. **전체 출품 품질 최종 합격은 선언하지 않는다.** 아래의 통과는 명시한 자산 버전과 검수 범위에 한정한다. 새 빌드를 만들지 않았으며 기존 배포 실행 파일이 수정되었다는 뜻이 아니다.

## 인계 시점 통과 항목

- **손/소매:** 14:26 승인된 파생 손의 비율·두께, 손톱, 정상 smooth shading, 소매 개구부 해결을 hip/ADS/장전 네 단계에서 확인했다. `evidence/HighAccepted_Hands_20261030_1426/`에 고정 사본과 해시가 있다. 피부/천이 실물과 구분 불가하다는 판정은 아니다.
- **M1 입력:** 실제 InputSystem 입력으로 ADS/hip 발사, 표적 명중, 장전 완료를 확인했다. 별도 캡처 없는 1,246프레임에서 반동 중 새 look 입력이 회복 후에도 유지됨을 확인했다. `HighAccepted_Input_20261030_1416` 및 위 손 14:26 JSON이 근거다. 단, 아래의 새 M1 총 자산 복구 후 회귀 검증을 대신하지 않는다.
- **아군:** 실제 Fire_Crouch, 총구 효과, 3D M1 소리, 전투 자막 중 발사, 일시정지/Silence 중지와 재시작 초기화를 확인했다. 파지·견착·시선의 큰 분리와 Idle의 약12cm 부유를 해결했다. Idle sole gap 8.24~8.50mm, 원본 Fire 접지 유지, 실제 Call 중 얼굴 관통 없음. `HighAccepted_AllySubtitle_20261030_1419` 및 `HighAccepted_AllyPose_20261030_1441` 참조.
- **TTS/저장:** 삭제 대상 12 WAV GUID의 Assets 참조 0, 9개 voice 필드 공란, 비언어 Hurt 3개 유지. 씬 재열기 후 Volume 5개 효과/sky/손/아군 파생 자산 참조 지속성도 확인했다. `HighAccepted_Persistence_20261030_1437` 참조.
- **맵 보존 범위:** 15:22:42에 107개 runtime/asmdef·ProjectSettings·Packages·Assets/Settings 파일을 직접 SHA-256 재계산해 14:27 baseline 대비 변경0을 확인했다. `ClaudeMapIntegration_20261030/system_high_handoff.json` 참조. 원본 TerrainData 파일 해시도 보존된다. Unity import 결과의 보호170,011셀 float mismatch=0, renderer/TerrainCollider 같은 파생 데이터 연결을 확인했다.
- **1차 자동 진행:** `evidence/CurrentProjectFlow_145150.json`의 16/16 checks, 관측구간 오류0, 보급→전차 전면도탄1/측면2→Dawn→Result→Title→새 회차 night0을 독립 확인했다. 목표/적 피해 주입과 synthetic real Rocket을 사용한 통합 시뮬레이션이며 사람 플레이·난이도·최종 수정 후 재검증은 아니다.

## Claude가 이어서 확인해야 하는 항목

1. **기존 사용자 수정 M1 복구:** 현재 FP는 다른 art SM_WP 총을 사용했던 것이 확인되었다. 사용자 기존 `M1_Reviewed.prefab`/M1ExteriorRefine·Photo 재질의 동일 화면 비교에서 금속·목재 정보가 더 명확했다(`evidence/M1_UserCorrectedCompare`). 복구 선택에는 동의했으나 High가 확인한 증거는 hip 비교뿐이다. 구현 담당 최종 회신상 **임시 clone 비교만 했고 복구 Apply는 아직 하지 않았다.** 따라서 저장 씬은 기존 art 총 상태이며, 새 총의 ADS 조준선/실제 ray 명중/장전 clip 접촉/손 관통/저장 재열기를 다시 확인해야 한다. 손 승인본을 새로 만들 필요는 없다.
2. **맵 sandbag 실제 렌더:** 보호 CP 물리 경계는 유지한다. 파생 CPU 메시의 축 상관/Blender 렌더는 4단 직립벽을 입증하지만, 마지막 독립 확인 Unity 근접 PNG `ClaudeMapFinal_150029/01_sandbags.png`는 매트처럼 보였다. 후속으로 Edit에서 27개 파생 메시의 동일 vertices/normals/tangents/triangles 재할당 및 UploadMeshData(false) 후 fresh Play의 M1 비교 배경에는 직립벽이 보인다. High도 해당 배경 변화를 확인했지만 최종 근접/측면 증거가 없어 최종 시각 통과는 보류한다. CPU 기하가 정상이고 동일 데이터 재업로드로 바뀌어 GPU stale 가설이 강하다. 재업로드 보강은 외부 Tools/ClaudeMapGrounding.cs에만 있고 프로젝트 helper에는 아직 복사하지 않았다는 담당 회신이다. 기하를 추정으로 다시 변형하지 않는다.
3. **충돌 검증의 한계:** `cover_surface_ray_audit.json`의9개 CP/108ray에서 상단 위54개 모두 miss, 아래54개 모두 hit, boolean 불일치0으로 상단을 넘는 보이지 않는 벽은 해결되었다. 그러나 물리 Box 진입과 rounded mesh 표면 거리차는 평균33.4cm/최대51.8cm다. 정밀 표면 일치라고 보고하지 않는다. 대표 실제 M1 타격 효과가 앞쪽 공중에 보이는지 확인한 뒤 필요성을 판단한다. Rock은 원래 visual footprint를 복구했고 기존 단순 collider보다 큰 차이는 원래부터 존재한다.
4. **환경 최종 품질:** 새 pine의 수직/alpha 잎, 지형 층 구분, 모래주머니 접지 개선은 확인했으나 전체 맵 합격은 아니다. 실제 활성 참호 흙벽/판자 재질 배선, 매끈한 담요 돔과 중앙 seam, 야간/조명탄/새벽 화면, 최종 렌더 부하가 남아 있다. `RL_Earth/RL_Sandbag` 새 재질은 scene 참조0이므로 모든 참호 재질 적용으로 주장하지 않는다. 기존 Varco sandbag PBR을 유지한 부분은 의도된 범위다.
5. **Dawn 보충병:** 실제 걷기/발 접지 시각은 미검증이다. 정적으로는 `DawnDirector`가 Y 고정 수평 이동을 하며, 연결된 PF_CH_ROK_Soldier의 AC_ROK는 Speed0/Idle_Stand 기본이고 Walk는 Speed>.1 조건이다. 현재 코드에서 아군 Speed 설정 경로를 찾지 못했다. 실제 슬라이딩/부유를 먼저 관측하고, 시스템 유지 범위에서 시각 자산으로 해결할 수 있는지 판단한다. 추정만으로 합격 또는 결함 확정하지 않는다.
6. **최종 통합/Console:** 1차 Flow 기록의 errors0은 harness 구독 이후만 포함한다. 이전 `COL_Granite_Boulder` unreadable/NavMeshBootstrap 오류가 있었으므로 fresh Play 시작부터 전체 Console 오류0을 별도로 확인해야 한다. 최종 기존 테스트77개, 98경로, 최신 총/맵 변경 후 흐름·정상 씬 저장·QA 임시 설정 복구 완료 여부를 구현 담당 증거와 대조한다. High는 이 최종 묶음의 완료 증거를 아직 받지 못했다. 구현 담당 최종 회신상 98경로/최종 perf/DawnVisualQA/전체 Console 감사는 미실행이다. 다만 안전 정리는 완료했다: Edit 상태, compiling=false, Dabudong 저장 dirty=false, QA stop callback 호출, timeScale=1, runInBackground=false, virtual input device 없음, focus 기본값 및 GameView 원래 index0 복구. 이 정리 상태는 담당 회신이며 High가 Unity를 직접 제어해 재확인한 것은 아니다.

아래는 발견·수정·재검증의 시간순 기록이다. 과거 불합격은 이후 명시적으로 해결된 경우 현재 상태로 읽지 않는다. 증거 경로 기준: `C:/Users/mbc/Documents/ChatGPT/다섯개의강/Map_Import_20261030/`.

---
# Astra High 독립 검수 — 2026-10-30

검수 담당은 구현 코드를 수정하거나 Unity를 제어하지 않았다. 구현 담당의 파일과 실제 Editor 입력 실행에서 생성한 1600×900 카메라 이미지를 별도로 읽었다. 새 빌드는 만들지 않는 사용자 지시를 유지한다. 아래 판정은 Windows 배포 실행 파일의 합격을 뜻하지 않는다.

## 13:20 KST 중간 판정

**소매의 팔꿈치 개구부/분리 문제만 통과. 전체 출품 품질은 검수 진행 중이다.**

- 이전 13:14 장전 이미지에는 오른팔 팔꿈치의 큰 검은 환형 개구부, 양팔의 구형 cap/관 분리가 보였다.
- 13:20:05 `evidence/FPS_ActualInput/10_reload_contact_camera1600.png`에서는 단일 연속 소매 실루엣으로 바뀌었으며 해당 검은 개구부와 분리가 사라졌다. hip/ADS 이미지도 함께 확인했다. 해당 장전 PNG SHA-256: `904DF895254CE6706E78747B0FF72D102A5A53922AF21AEA11DA760412438AB9`.
- 검수 이미지의 손목에는 큰 노출 틈이 보이지 않는다. 단, 한 장전 시점으로 전체 애니메이션의 무결성을 단정하지 않는다. 20%/80% 전후의 전환 자세도 추가 확인할 필요가 있다.
- `M1Presentation.OnDisable`에서 오른손 thumbBase/thumbMiddle/thumbEnd 세 본의 원래 회전을 모두 복원함을 정적 확인했다.
- 최신 입력 JSON은 controllersEnabled=true이고 ADS와 hip 발사 두 번 모두 TutorialTarget에 명중했다. AimBlend=1인 첫 발, AimBlend=0인 두 번째 발과 탄약 7→6을 기록한다. 최대 recoil은 1.565079도였다.
- 캡처 04/05가 같은 frame 77이므로 두 이미지로 반동 회복 시간차를 증명할 수 없다. NoCapture 연속 입력 기록의 독립 확인은 아직 남았다. `captureMetadata.shotAge`는 두 번째 발 이후에도 첫 발 기준이므로 hip 발사 나이는 shots[1] 시간을 이용해야 한다.

## 아직 합격하지 않은 항목

- 군복은 매끈한 초록 관처럼 보이며 천의 구조/주름이 부족하다. 피부는 균일한 왁스처럼 보인다. 금속의 넓고 균일한 하이라이트, 목재의 큰 얼룩 중심 표현도 근접 출품 품질로 판단하기 어렵다. 후처리로 가리지 않고 재질과 표면 정보의 실제 개선이 필요하다.
- 사용자가 추가로 지적한 얇은 손은 손바닥 두께·손가락 굵기·관절·손목 비율을 수정한 뒤 파지/장전 접촉을 다시 확인해야 한다. 원본 보존, 파생 메시 수정이 기준이다.
- 아군 사격의 대상 선정, 사격 모션, 총구 효과, 비언어 SFX, 비전투/일시정지/재시작 정리와 기존 전투 진행 보존은 구현 후 별도 검수한다.
- 통합 테스트, 마지막 정상 씬 저장, QA 임시 설정 복구 및 최종 플레이 화면 확인은 아직 남았다.

증거 경로의 기준은 `C:/Users/mbc/Documents/ChatGPT/다섯개의강/Map_Import_20261030/`이다. 같은 이름의 증거가 재생성될 수 있으므로 시간과 해시를 함께 기록했다.

## 13:29 KST 손 수정 회귀 검수 — 고정 사본

`evidence/HighReviewed_20261030_1329/`에 검수한 hip/ADS/장전 PNG와 입력 JSON을 복사했다. 폴더 이름은 검수 기록이며 전체 품질 합격을 뜻하지 않는다.

- 소매의 검은 개구부/구형 cap/관 분리 해결은 유지된다.
- 손바닥과 엄지의 부피 증가는 실제 화면에서 확인되며, 이 세 자세에서 새 큰 관통이나 손목 틈은 발견하지 못했다. 원본에서 매번 파생 메시를 생성하므로 누적 팽창하지 않는 구현도 확인했다.
- 인체 사실감은 아직 보류한다. 길고 매끈한 손가락에 관절/손톱/피부 색 단서가 부족하다. 첫 재질 변경의 천 격자 무늬는 과하며 야간 암부가 막혔다. 구현 담당자가 이를 인지하고 수정 중이다.
- 정적 검수에서 발견한 목재 먼지의 일반 URP/Unlit 사용 문제는 Particles/Unlit 분리로 수정되었다. 일반 Unlit에는 COLOR 입력이 없어 입자의 startColor/수명 페이드가 적용되지 않았던 문제다. 실제 수정 효과의 동작 확인은 후속이다.

고정 증거 SHA-256:

| 파일 | SHA-256 |
| --- | --- |
| 01_hip_target_camera1600.png | `10472398BFA2F2160B4D7BF8644FCDB4639F421E343C602C77F32B95FC2D8F19` |
| 02_ads_before_camera1600.png | `37B78AFBD4D7BC830AF11E068133BB39DF2A80E6E4BF3DA611A463293911F2E0` |
| 10_reload_contact_camera1600.png | `62C0B3ED8DF4D2A56B262A7069D25EB4317D90D348876F5E57A758344B716FB2` |
| input_review.json | `2D3D27DB91207526105E64944D6B1041BE5525669596CDA1D1A49FFB8FE359EE` |

## 아군 1차 독립 수치 검토

`ally_qa_Scout_Running_132947.json`의 280샘플/17.9765초를 별도로 파싱했다. 아군 네 명이 각각 5~6회 추가 발사했으며 실제 M1 AudioSource 재생을 포함한 샘플이 235개였다. 재생된 총소리의 spatialBlend는 모두 1이었다. 총구와 목표 방향 최대 차이는 0.06255726도, 적 수는 4로 유지되었다. 실제 Fire_Crouch 상태도 네 명 모두 기록되었다.

다만 왼손 본과 총축의 거리는 Fire 구간에서도 최대 약10~11.8cm, 대기 구간에는 약10.6~12.5cm였다. 이 수치는 본 위치와 손 표면의 차이도 포함하므로 파지 실패를 단독 확정하지 않지만, 실제 근접 이미지에서 파지/전환을 확인하기 전에는 아군 화면을 합격 처리하지 않는다. 일시정지/정적 구간/재시작 동작도 후속 확인한다.

## 13:34 KST 아군 화면과 중지/복구 검수

고정 사본: `evidence/HighReviewed_Allies_20261030_1334/`.

**발사/정지 연결은 수치상 통과했으나 아군 파지 화면은 P1 불합격이다.** `ally_firing_Ally_1_133401.png`와 `ally_firing_ParkYongpal_133401.png`를 직접 확인했다. 왼손이 총의 아래 공중에 있고 개머리판은 견착되지 않는다. 박용팔 왼손목의 검은 개구부도 보인다. 정확한 목표 방향 수치만으로 화면이 올바르다고 판단하지 않았다. 구현 담당자에게 양손 파지, 어깨/얼굴과의 위치 정합을 재수정하도록 전달했다.

- Assault 일시정지 285샘플에서 합산 발사수 210→210, pending=0, FirePose=0. 첫 샘플에만 이전 M1 소리 꼬리가 있었고 후속 새 재생은 없었다. scaled time은 216.339035로 고정되었다.
- Silence 282샘플/17.979217초에서 발사수 210→210, pending=0, FirePose=0, 총소리 재생 샘플=0.
- 재시도 전 `[16,14,14,14]`였던 카운터가 retry와 fresh에서 각각 `[0,0,0,0]`으로 초기화된 기록을 확인했다.

| 고정 파일 | SHA-256 |
| --- | --- |
| ally_firing_Ally_1_133401.png | `031214A3FCE9A835D3D113E806006B362DF2D369C317472BBCE75294E6CCDE2E` |
| ally_firing_ParkYongpal_133401.png | `0F0CF0895E6A1BD002C851006D9FCF4C4C9860B668382180EA9CCF29B449DD3B` |
| ally_qa_Assault_Paused_133219.json | `91ACDB453D3835DD8F0006A19D34E145F1B27D67F1CBDAB93DD6C2CE8F25C135` |
| ally_qa_resets.json | `0166F2E1C7B10EACD8AEED9077ED4798B5D5FD0E244F8D0D299344D7A450ED92` |
| ally_qa_Scout_Running_132947.json | `550F46BD85AFA57690759386110E88D94445833C8A7288F04BAC86BBC3474AB5` |
| ally_qa_Silence_Running_133253.json | `FBC5F9E3C5CDFB86D9FA695FDAEB69804AE793184BBEC39FB2474046CE709505` |

## 저장 지속성 결함 발견

13:36 전후 정적 검수에서 `SurfacePresentationSetup`의 `VolumeProfile.Add<T>(true)`로 만든 효과가 subasset으로 등록되지 않은 문제를 발견했다. 실제 저장된 `Dabudong_Night_Profile.asset`의 components 다섯 개가 모두 `{fileID: 0}`였다. 따라서 그 시점의 라이브 톤매핑/노출 결과는 Editor 재열기 후 유지된다고 볼 수 없다. 구현 담당자에게 AddObjectToAsset/dirty 저장과 실제 nonzero 참조·서브에셋 블록 확인을 요청했다. 최종 통과에는 저장 후 씬을 다시 열어 확인한 증거가 필요하다.

후속 파일 검수에서 AddObjectToAsset 및 각 효과의 dirty 처리가 반영되었고, 저장 YAML에 다섯 개 nonzero 참조와 Vignette/Bloom/ColorAdjustments/Tonemapping/MotionBlur 서브에셋 블록이 존재함을 독립 확인했다. 저장 구조 결함은 수정되었다. 마지막 씬 재열기 검증은 별도다. 새 DawnDirector의 sky material은 런타임 복제본을 사용하며 종료 시 원본을 복구하고 새 회차에서 blend를 0으로 되돌리는 경로도 정적 확인했다.

## TTS 제거 유지 독립 확인

구현 작업 중 백업의 삭제 대상 `*.wav.meta` 12개에서 GUID를 직접 추출해 현재 Assets 전체를 검색했다. 해당 GUID 참조는 0개였다. 현재 GameText의 voice 필드 9개는 모두 비어 있고, VO 디렉터리에는 비언어 Hurt WAV 세 개만 남아 있다. 아군 추가 사격은 비언어 M1 효과음을 사용한다. TTS 제거 상태 유지는 통과다.

## 아군 후속 원인 구분

13:42 견착/양팔 IK 후 일반 아군의 큰 손-총 분리는 사라졌다. 박용팔의 거친 전완과 소매 구획을 새 IK에 의한 찢김으로 의심했으나, `ally_park_original_clip_135244.png`(IK 없이 동일 원본 Fire clip)와 `ally_park_twist_preview_134918.png`를 직접 비교한 결과 큰 말린 소매와 갈색 피부 구획은 원본에도 존재했다. **새 IK가 팔을 절단했다는 해석은 철회한다.** 남은 원본의 전완 면 분할/거친 cuff 외관은 표면 품질 문제로 구분하며 normal 수정 후 재검수한다. 머리의 과도한 옆 기울기는 13:49 미리보기에서 개선되었다. 이 미리보기는 실제 플레이 사격 회귀 검증을 대신하지 않는다.

13:34/13:42 아군 이미지들은 임시 카메라에 Camera.CopyFrom만 사용하여 URP post-processing 설정이 복사되지 않았다. 따라서 기하 검수에는 사용하되 이 이미지의 밝기/반사만으로 최종 재질 품질을 확정하지 않는다. 최종 환경 평가는 실제 플레이어 카메라로 수행해야 한다.

## 14:06–14:07 실제 플레이어 카메라 재검수

`evidence/FPS_Anatomy_1405`의 hip/ADS/장전 접촉 및 장전 상승·clip 접근·복귀 이미지를 직접 확인했다. 소매의 큰 개구부 해결은 유지된다. 약지·소지의 굽힘과 손바닥 부피는 이전보다 사람 손에 가깝고, clip 접근 이미지 13에는 황동 탄환의 분리된 원통 형태가 읽힌다. 하지만 열린 손바닥의 삼각 면 음영, 아직 반영되지 않은 손톱 mask 수정, 지나치게 선명한 소매 직조 무늬 때문에 인체/재질 최종 통과는 보류한다. 구현 담당자에게 source 대비 normal 재계산 회귀를 분리해 확인하도록 요청했다.

`evidence/Environment_140727`의 5개 이미지와 metadata를 직접 읽었다. 실제 main camera, postProcessing=true, controllerEnabled=true인 Scout 화면이다. 시각 검수를 위해 플레이어 위치만 임시 이동했으며 재질·조명·포즈 override는 없다는 기록이다.

- `01_sandbags.png` 가까운 모래주머니와 우측 멀리 있는 모래주머니에는 밑면 아래 빈 공간과 떨어진 그림자가 보인다. `04_shrub.png` 배경 두 곳에서도 같은 현상이 보인다. 원본 배치 여부와 별개로 접지 P1을 수정해야 한다. 실제 mesh 바닥과 Terrain 높이의 차이를 요청했다.
- `02_rock.png`의 지면·바위·나무껍질에는 표면 정보가 읽힌다. 이 샷에서 큰 지면 틈이나 분홍 오류 shader는 발견하지 못했다.
- `03_pine.png`의 잎 덩어리는 불투명한 접힌 판처럼 보이므로 실제 alpha texture/clip 배선을 확인해야 한다.
- `05_imported_blanket.png`는 여전히 매끈한 올리브 돔과 중앙 세로 seam으로 읽힌다. 실제 활성 오브젝트는 imported `Trench_Visuals/Blanket_Body`이며 별도 비활성 서사 인물 FBX와 구분한다. 삭제할 대상이 아니라 천 형상/표면을 개선할 대상이다.

따라서 이 시점의 전체 환경 품질은 통과하지 않았다. 야간/새벽 전환, 조명탄, 아군 2–5m 실제 거리, 새벽 보충병 발 접지와 걷기 상태의 최종 증거도 남아 있다.

아군의 자막 표시 중 발사 차단은 TTS 제거 후 전투 자막 동안 적만 발사하게 할 수 있어 불필요한 gate로 판단했다. 구현 담당자는 해당 자막 gate를 제거하고 section/CombatLocked/일시정지/사망/LOS 조건을 유지하기로 했다. 최종 fresh Scout에서는 자막 표시 중 실제 발사·효과·소리가 함께 증가하는지 확인해야 한다.

## 14:16 입력 중 반동 회복 — 통과

고정 증거: `evidence/HighAccepted_Input_20261030_1416/input_review.json` SHA-256 `BDA171A0E97AFC04E01934398FE02A3FC8AA9674E455D4F3FB69748FDCF62494`. 실행 스크립트 사본 `FPSActualInputNoCapture.cs.txt` SHA-256 `3FB36B48D87CD76CA571464AC77BAEAF923B87C16758876A2BD6B247E518F163`.

PNG 캡처 없이 실제 InputSystem Aim/Fire/Look/Reload를 실행한 1,246개 고유 프레임/7.8005초 기록을 독립 파싱했다. 모든 샘플에서 PlayerController와 WeaponHolder가 활성 상태였다. 첫 발 +0.1040초에 (3,−2) look을 주입하자 pitch가 −1.70656371→−1.59856367도, yaw가 +0.162도 변했다. 이는 기본 감도 0.09와 ADS 배율 0.6의 계산과 일치한다. 첫 반동 최대 1.56768548도, +0.603초에는 0.00001204도로 회복되었고 새 pitch는 최종 장전 완료 후에도 그대로 유지되었다. **반동 회복이 사용자의 새로운 시점 입력을 덮지 않는 동작은 통과**다. ADS FOV 50, 해제 FOV 70, 장전 종료 탄수 8도 확인했다.

이 실행의 첫 ADS는 TutorialTarget, 두 번째 hip은 Terrain에 맞았다. 시점 이동과 hip 분산 이후 발사이므로 이 실행을 두 발 모두 표적 명중한 증거로 제시하지 않는다. 앞선 별도 표적 명중 검증과 구분한다. 자동 입력이며 사람이 직접 플레이한 기록이 아니다.

## Claude 맵 통합 추가 검수 범위

사용자 요청 문서와 Claude HANDOFF를 읽었다. 실제 대상은 현재 `fiveriver/five_river`이며 문서의 예전 프로젝트 경로는 사용하지 않는다. 최신 사용자 지시에 따라 맵 통합은 시각 데이터만 다루며 기존 런타임 시스템·전투·이동·AI·진행·UI·오디오 및 URP/Graphics/Quality 설정을 바꾸지 않는다. 현 M1/아군 승인 작업과 맵 통합의 변경 범위를 분리한다.

현재 지형을 새로 내보내 파생 terrain에 적용해야 한다. 원본 Claude 높이 파일을 덮어쓰지 않는다. 원본 roundtrip의 보호 높이 bit동일 검사는 r16끼리의 비교이며, 전체 float→ushort→float 변환은 원본 Unity 높이까지 비트 동일함을 보장하지 않는다. 변경 없는 셀에는 원본 float 높이를 그대로 보존하도록 구현 담당자에게 전달했다. 기존 pine는 RGB 불투명 실체 메시이며 alpha 손실로 확정할 수 없다는 원인 정정도 반영한다. Claude 새 nature의 alpha 카드는 별도로 alpha clip/양면 배선을 검수한다.

## 14:19 아군 전투 자막 중 사격 — 기능 통과, 접지 화면 미통과

고정 수치 증거 `evidence/HighAccepted_AllySubtitle_20261030_1419/ally_qa_Scout_Running_141907.json`, SHA-256 `715BACB6AA3E7A6CCB14582E9A02D8B3B34CE3ACAD15E29CE9FD65F310BB59B8`를 독립 파싱했다. 246샘플의 게임시간 범위는 11.4105초다. Speaking=true인 39샘플 동안 네 아군 모두 발사수 0→1, 총구효과/소리요청 카운터도 같은 수로 증가했다. 36샘플에서 실제 총소리 AudioSource가 재생되었고 spatialBlend는 모두 1이다. 전체 마지막 카운터는 3/3/3/4발, 적 수는 2로 유지되었다. **TTS 없는 전투 자막이 아군 사격을 막던 회귀는 통과**다.

target이 있고 실제 FirePose인 218샘플에서 왼손 파지 타깃 오차 최대 0.1267mm, 오른손 0.0023mm, 머리 목표각 최대 0.028도였다. 견착 오차 최대 18.68mm는 발사 반동 구간을 포함한다. 반면 target 최초 획득 후 standing→crouch 전환 약 0.25초 동안 왼손 타깃 오차가 최대 15.8cm였으므로 이 순간을 정상 발사 파지 통계에 섞지 않는다.

`evidence/AllyMainCamera_141933/01_park.png`와 `02_student.png`는 실제 플레이어 카메라 약 2.5m 거리에서 확인했다. 총의 큰 양손 분리, 얼굴 앞을 가로지르는 파지 및 머리 과도한 기울기는 해결되었다. 하지만 두 캐릭터의 발·무릎이 지면 위에 떠 있는 인상이 명확하여 실제 skin/foot와 Terrain 높이 차이 검증을 요청했다. 특히 학생 아군 앞발 아래 틈과 떨어진 그림자가 보인다. 새 맵 통합의 시스템 동결을 존중하여 원본 배치/포즈의 원인부터 확인해야 한다. 아군 전체 시각 품질은 이 시점에 아직 통과하지 않았다.

손바닥 삼각 면 음영의 원인 격리도 확인했다. `FPS_SkinNormalOff_1416/12_reload_rise_camera1600.png`에서는 normal texture를 꺼도 결함이 유지되었으나, `FPS_SourceNormals_1418/12_reload_rise_camera1600.png`에서 같은 파생 정점에 원본 artist normals/tangents만 복원하자 큰 삼각 명암 경계가 사라졌다. source의 겹친 정점 그룹 3,086개에 대한 구현 담당자 audit은 파생 정점 위치 gap=0 및 normal seam 각도=0이었다. 따라서 정점 틈보다는 RecalculateNormals가 원래의 smooth shading 정보를 잃은 회귀로 판단한다. 정상 normal texture를 다시 적용한 저장 상태의 최종 이미지를 확인한 뒤 해당 결함을 통과 처리한다.

## 14:26 손 비율·표면 회귀 수정 — 통과 범위 확정

`evidence/FPS_Reviewed_1426`의 hip, ADS, 장전 접촉, 손 상승, clip 접근, 복귀 여섯 장을 직접 확인했다. fresh Apply 후 정상 normal texture를 다시 사용한 상태에서도 큰 손바닥 삼각 명암 경계가 사라졌다. 손바닥과 엄지두덩의 부피, 손가락 단면/끝 테이퍼, 손톱 패치가 읽히며 원래의 지나치게 가는 손 인상이 개선되었다. 해당 여섯 자세에서 큰 관통·손목 틈·소매 팔꿈치 개구부는 발견하지 못했다. 과했던 천 격자도 제거되었다. **얇은 손 교정과 손 기하/명암 회귀 해결은 통과**다. 실물과 구분 불가한 피부나 군복이라고 평가한 것은 아니다.

고정 사본: `evidence/HighAccepted_Hands_20261030_1426/`의 여섯 PNG, `input_review.json`, `SHA256.json`.

| 파일 | SHA-256 |
| --- | --- |
| 01_hip_target_camera1600.png | `D6D4CA229C7AD9D76BAD93787C403DFF200AE4ECF745BC4475B51DF7534556BD` |
| 02_ads_before_camera1600.png | `C6E8F1D288D6CBF2C22E195D081D86848AF7A1ACC7B9E494703DCE6846D462B2` |
| 10_reload_contact_camera1600.png | `E1C13261F9D3CAD3B43722047C1270A06B4D558179A7657CA0A1638E8DB49F07` |
| 12_reload_rise_camera1600.png | `3495D8D7ECC965767BF0A2CE1F831C6736460C8C3646A6631FC6E4270BBD84DA` |
| 13_reload_clip_approach_camera1600.png | `7B1388378F38A37BA2BC47A223AC384286BFBD811D06689571CED1381F06B4CB` |
| 14_reload_return_camera1600.png | `35BACB9DDB5E991CC39469961E67DB921AB3D8E627C275C2436EA4C8B2C92B23` |
| input_review.json | `4F3F229B142C9FAEB8908EFA5432A72DC19316ECD7412A179894A0D4D00D7190` |

## Claude 맵 Blender 중간 검수

`ClaudeMapIntegration_20261030/ArtSource/Map`의 input/output r16와 protection_mask.u8를 독립 파싱했다. 263,169셀 중 88,855셀이 변경되었고, 보호 셀 170,011개의 r16 차이는 0이었다. 실제 높이 범위 64m를 적용한 변화 최대치는 +0.250003815/−0.349614710m로 작업 결과와 일치한다. aerial/trench 미리보기에서 기존 능선/중앙 전투 영역의 구조와 갈색 길/풀 분포를 확인했다. 이 이미지는 Blender 참고 렌더이며 흰 소품과 조명은 최종 Unity 화면의 증거가 아니다. 원경 구덩이 군집과 이미 떠 있던 모래주머니는 남아 있다.

Editor import 코드에서 원본 float에 delta만 더하고 변경 없는 셀은 유지하며, 실제 GetHeights의 보호 셀 차이를 검사하는 경로를 확인했다. terrain renderer와 TerrainCollider는 같은 파생 TerrainData를 사용해야 하며, 기존 콜라이더 설정/물리 재질은 유지한다. 필요 시 같은 설정의 파생 NavMesh 재베이크만 허용하고 실제 경로 회귀를 확인한다는 부모의 최신 범위 정정을 반영한다. Unity import 후 float 비교, 접지, 시스템 해시 및 실제 플레이 화면은 아직 후속이다.

아군 접지 후속 수치로 시각 추정을 정정한다. `evidence/AllyGroundContact_ActualFire.json`을 직접 읽었으며 실제 Fire_Crouch 진입 시 CPU skinned sole 최저점과 지형 차이는 Park −4.53cm, 학생 +2.08~2.78cm였다. 따라서 앞선 사격 사진의 그림자 분리만으로 사격 자세까지 12cm 떠 있다고 단정하지 않는다. 구현 담당자 계측의 Idle +11.7~13.0cm는 별개의 확정 결함이다. 전체 모델을 12cm 내리는 방식은 사격 자세를 묻을 수 있어 보류하고, idle animation asset의 접지 설정을 검토한다.

Blender v2 aerial도 직접 확인했다. 구덩이를 85개로 줄이고 그을음을 완화한 결과 오른쪽 능선/좌상단의 과밀한 검은 군집은 v1보다 줄었다. 기존 능선과 중앙 보호 영역은 유지되어 보인다. 최종 Unity fresh Play에서는 `COL_Granite_Boulder` unreadable 메시로 발생했던 NavMeshBootstrap 오류가 모델 import 설정 수정 후 사라졌는지도 반드시 확인한다.

## 14:37 저장 후 재열기와 14:41 아군 자세 — 통과

`SceneReopenPersistence_1437.json`을 독립 확인했다. 실제 scene 재열기 후 Tonemapping/ColorAdjustments/Bloom/Vignette/MotionBlur 다섯 효과가 persistent=true이며 night/dawn sky, 파생 손 메시, 아군 네 override controller 참조가 유지되었다. 앞선 nonzero fileID/subasset 정적 검수와 합쳐 저장 지속성 결함 수정은 통과다. 고정 `evidence/HighAccepted_Persistence_20261030_1437/SceneReopenPersistence_1437.json` SHA-256 `507FA3342BB2ECDB11CEE99188971819B0046E6530944B94B3B1E7575F2DA18C`.

`AllyGroundedIdleSetup`은 원본을 보존한 Idle/Call 파생 clip의 RootT.y만 보정하며 actor root, marker, collider, 상태 타이밍, Fire clip을 바꾸지 않는다. 실제 CorrectedIdle 수치를 독립 확인한 결과 네 아군 sole gap은 8.24~8.50mm다. CorrectedFire는 Park −4.536cm, 학생 +2.079~2.779cm로 수정 전 Fire와 거의 같아 일괄 모델 하강으로 생길 지면 관통 회귀가 없다.

`AllyCall_Verified_144058/01_park_call.png`와 metadata를 직접 확인했다. 실제 `A_ROK_Call_Grounded_ParkYongpal` normalizedTime=0.500504732인 Silence 자세로, 발이 지면에 닿고 총이 얼굴/머리를 관통하지 않는다. 플레이어 controls를 임시 정지해 카메라를 유지했지만 actor pose override는 하지 않았다. 이는 아군 시각 증거이며 실제 입력 검증으로 제시하지 않는다. 기존 사격 화면의 파지/견착/시선 개선과 합쳐 **아군의 지적된 파지·시선·Idle 접지·Call 회귀는 통과**다. 최신 맵 통합 후 전체 플레이 회귀는 별도로 남는다.

고정 사본 `evidence/HighAccepted_AllyPose_20261030_1441/`에 Call PNG/metadata, 이전 실제 사격 PNG 두 장, corrected idle/fire JSON 및 모든 SHA-256 목록을 보존했다. Call PNG SHA-256 `BD07F94D5EA607F80AD0586B41B43BC0654EB50D11981170B2EF4BB03A92FCAC`, corrected idle JSON `817D0DECC55E93EE86EF7C333C950ED5A6B1D899A11700DEE492B75D2F95E315`, corrected fire JSON `931B414215CDD76333726505B1521EA53B09A301A7F1E329842B320C2B0678ED`.

최신 14:26 손 고정 JSON도 추가로 읽었다. ADS/hip 모두 TutorialTarget에 맞았으며 장전 완료 시 탄수 8이었다. 서로 다른 reload frame 437/487/560/688에 상승/clip 접근/접촉/복귀가 기록되었다. 별도 14:16 NoCapture의 두 번째 Terrain 명중과 혼동하지 않는다.

## 14:45 맵 통합 후 시스템 파일 독립 대조 — 통과

부모가 만든 14:27:43 baseline의 107개 runtime C#/asmdef, ProjectSettings, Packages, Assets/Settings 파일을 14:45:34에 각각 디스크에서 다시 SHA-256 계산했다. 변경 0, allMatch=true였다. 결과는 `ClaudeMapIntegration_20261030/system_high_independent.json`에 기록했다. 이 범위의 시스템 보존은 통과다. 기존 승인된 M1/아군 작업은 baseline에 포함되어 있으며 맵 Editor 도구와 시각 자산은 비교 범위에서 제외된다. Scene 기능 필드와 실제 플레이 흐름은 별도의 확인이 필요하다.

원본 TerrainData 파일도 export 시 SHA-256 `8F1A647EF14EF307D920B80CC66A4E2E8D5C335DDDDC1BE0A39E40FC33BA120E`와 동일함을 직접 확인했다. Unity import 결과는 v2 변경 셀 88,826, 보호 셀 170,011, 보호 float mismatch=0, renderer/TerrainCollider 동일 파생 데이터라고 기록한다.

추가 정적 검수에서 material exact-name 매칭은 현재 August/Trench_Dry_* 이름을 포함하지 않는다는 점을 전달했다. 새 Claude RL_Earth/RL_Sandbag GUID의 scene 참조는 0이었다. 담당자는 활성 Varco sandbag의 기존 baked PBR을 generic RL 재질로 덮지 않은 것이 의도이며 모든 재질 적용으로 주장하지 않겠다고 설명했다. 실제 활성 renderer 재질 경로 audit으로 최종 적용 범위를 확인한다. 새 nature의 축 차이, 기존 collider와 시각 모델 경계 정합, LOD를 끈 뒤 실제 렌더 부하도 최종 Unity 시각/기능 검수 조건이다.


