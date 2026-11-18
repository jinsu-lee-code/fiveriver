# VARCO 3D 국군 대기 · 경계 동작 원본 (2026-11-18)

참호의 아군이 싸우지 않을 때 대기 클립 하나만 반복하는 문제를 풀기 위해 받아 둔 **원본**이다.
게임(`five_river/Assets`)에는 아직 넣지 않았다. 이 폴더는 보관용이며 Unity가 읽지 않는다.

- 받은 날: 2026-11-18
- 개수 · 크기: FBX 21개, 합계 약 647MB (파일 하나 약 30.8MB — 모델 · 텍스처가 함께 들어 있다)
- 쓴 포인트: **0** (Animate 21회, 실행 응답의 예상 포인트가 두 번 모두 0)
- 못 받은 키: 없음 (21개 모두 `isPlus` 표시가 있었지만 CREATOR 등급에서 실행됨)
- **주의: 이 폴더는 `.gitignore`에 들어 있지 않다.** 647MB가 커밋에 섞이지 않게 한다.

## 1. VARCO 구성

| 항목 | 내용 |
|---|---|
| 플로우 | "다섯개의강" `90f424df-da07-4631-b782-f31de5e868c7` |
| 캐릭터 | "국군 신병"(1950년 8월 다부동, T포즈, 무기 없음) — 2026-10-23 "국군 인물 다시 만들기" 묶음 |
| 노드 흐름 | GenerateImage `48948655` → EditImage `b5f69443` → Generate3D `903dccfd`(5만 면, quad, tPose=1, 4096 PBR) → **Rig `d427368e-803c-4e48-8f86-cde87f146e9d`**(humanoid-fingers) → Animate |
| 새로 만든 것 | Animate 노드 21개(`inPlace=1`, `count=1`), 캔버스 x 6400~8800 · y 20900~22000 빈 자리에 7열 3줄. Rig `d427368e`의 `out` → 각 노드 `mesh` 연결 21개 |
| 기존 내용 | 바꾸지 않음. 작업 전후 그래프 비교: 기존 노드 396개 · 연결 280개 그대로, 노드 417개 · 연결 301개가 됨 |
| 내려받기 | `get_output_downloads`(fbx) 주소를 curl로 받음 |

같은 플로우에 리깅된 국군이 더 있다: 분대장 Rig `66a86445`, BAR 사수 Rig `8eecab66`(둘 다 humanoid-fingers), 주인공 오석구 Rig `6c227e0a`(옛 방식), 출처 이미지뿐인 Rig `61794acc`(humanoid, 기존 `kneel_rifle_idle_1_looping` · `rifle_shot_looping` 등이 붙어 있음). Humanoid로 리타깃해 쓸 것이므로 어느 캐릭터로 받아도 동작은 같다.

## 2. 키 ↔ 파일 ↔ 쓰임새 ↔ 제안 파일명

`_looping` 변형이 있는 키는 그쪽을 받았다. 파일명은 요청대로 **기본 키 이름**이다. "받은 키" 열이 실제로 적용한 키다.
제안 파일명은 `ANIM_SPEC.md` 1-2 규칙(`A_ROK_…`, PascalCase, 변형 `_01`)을 따른 **제안**이며 아직 요청서에 올리지 않았다.

| 파일 (`raw/`) | 받은 키 | 쓰임새 | 제안 게임 파일명 | 노드 id 앞 8자 |
|---|---|---|---|---|
| kneel_rifle_idle_1.fbx | kneel_rifle_idle_1_looping | 무릎 대기 변화 | A_ROK_Trench_Idle_01.fbx | e54d6f8d |
| kneel_rifle_idle_2.fbx | kneel_rifle_idle_2_looping | 무릎 대기 변화 | A_ROK_Trench_Idle_02.fbx | 834cc92d |
| kneel_rifle_idle_3.fbx | kneel_rifle_idle_3_looping | 무릎 대기 변화 | A_ROK_Trench_Idle_03.fbx | 4d473fdc |
| kneel_combat_idle.fbx | kneel_combat_idle_looping | 무릎 대기(긴장) | A_ROK_Trench_Idle_Alert.fbx | 74f2e0ea |
| kneel_look_guard.fbx | kneel_look_guard_looping | 들판 살피기 | A_ROK_Trench_Scan_01.fbx | 37a99e6f |
| kneel_rifle_guard.fbx | kneel_rifle_guard_looping | 들판 살피기(총 겨눈 채) | A_ROK_Trench_Scan_02.fbx | c8f927f0 |
| kneel_clockwise_guard.fbx | kneel_clockwise_guard (반복 변형 없음) | 오른쪽으로 돌며 살피기 | A_ROK_Trench_Scan_TurnRight.fbx | 6d5e6fce |
| kneel_counter_guard.fbx | kneel_counter_guard_looping | 왼쪽으로 돌며 살피기 | A_ROK_Trench_Scan_TurnLeft.fbx | 63d736af |
| kneel_muzzle_raise_1.fbx | kneel_muzzle_raise_1_looping | 흉벽 너머 내다보기 | A_ROK_Trench_LookOver_01.fbx | 0aa56de5 |
| kneel_muzzle_raise_2.fbx | kneel_muzzle_raise_2_looping | 흉벽 너머 내다보기 | A_ROK_Trench_LookOver_02.fbx | 75a049f9 |
| kneel_rifle_raise.fbx | kneel_rifle_raise_looping | 흉벽 너머 내다보기(총 올림) | A_ROK_Trench_LookOver_03.fbx | cb8ed803 |
| rifle_stand_idle_1.fbx | rifle_stand_idle_1_looping | 서서 대기 | A_ROK_Stand_Idle_01.fbx | 08b1e58b |
| rifle_stand_idle_2.fbx | rifle_stand_idle_2_looping | 서서 대기 | A_ROK_Stand_Idle_02.fbx | 575912c1 |
| rifle_guard_idle_1.fbx | rifle_guard_idle_1_looping | 서서 경계 | A_ROK_Stand_Guard.fbx | db0c2d73 |
| rifle_look_idle.fbx | rifle_look_idle_looping | 서서 둘러보기 | A_ROK_Stand_Look.fbx | 7febb70c |
| rifle_hand_idle_1.fbx | rifle_hand_idle_1_looping | 서서 총 고쳐 잡기 | A_ROK_Stand_HandAdjust.fbx | 201bee28 |
| stand_to_kneel_1.fbx | stand_to_kneel_1 (반복 변형 없음) | 서 있다가 무릎 꿇기 | A_ROK_StandToKneel_01.fbx | 72d5cb23 |
| stand_to_kneel_2.fbx | stand_to_kneel_2 (반복 변형 없음) | 서 있다가 무릎 꿇기 | A_ROK_StandToKneel_02.fbx | dab016c9 |
| crouch_kneel_idle.fbx | crouch_kneel_idle_looping | 쪼그려 무릎 대기 | A_ROK_Crouch_Idle.fbx | ec12a084 |
| startled_stagger.fbx | startled_stagger (반복 변형 없음) | 움찔하며 비틀 | A_ROK_Flinch_Stagger.fbx | a9160cf9 |
| surprise.fbx | surprise (반복 변형이 있지만 한 번짜리로 쓸 것이라 기본 키) | 움찔 | A_ROK_Flinch.fbx | 9467d3cb |

`crouch_kneel_idle`은 같은 리그에 기본 키로 만든 기존 노드(`6a066d36`)가 이미 있다. 여기서는 반복 변형을 새로 받았다.

## 3. 검사 수치

백그라운드 Blender 5.2.2(`blender -b --factory-startup --python`)로 FBX를 읽어 잰 값이다. Unity로는 확인하지 않았다.

공통: 뼈 53개(21개 모두 이름 · 순서 같음), 30fps, 뼈대 오브젝트 스케일 1 · 회전 0, 액션 이름 `Armature|Take 001|BaseLayer`, 메시 1개(정점 78,868) 포함, 엉덩이 수평 이동은 모든 프레임에서 0(제자리).
길이 단위는 FBX 그대로이며, 이 모델은 섰을 때 키가 약 1.0이다(실제 키 아님 — 아래 5번).

| 파일 | 프레임 | 길이(초) | 엉덩이 높이 시작→끝 | 엉덩이 높이 변동폭 | 첫 · 끝 자세 차(도) | 반복 가능 |
|---|---|---|---|---|---|---|
| kneel_rifle_idle_1 | 358 | 11.90 | 0.247→0.247 | 0.008 | 0 | 예 |
| kneel_rifle_idle_2 | 336 | 11.17 | 0.242→0.242 | 0.005 | 0 | 예 |
| kneel_rifle_idle_3 | 227 | 7.53 | 0.236→0.236 | 0.001 | 0 | 예 |
| kneel_combat_idle | 298 | 9.90 | 0.250→0.250 | 0.004 | 0 | 예 |
| kneel_look_guard | 392 | 13.03 | 0.220→0.220 | 0.004 | 0 | 예 |
| kneel_rifle_guard | 301 | 10.00 | 0.304→0.304 | 0.001 | 0 | 예 |
| kneel_clockwise_guard | 234 | 7.77 | 0.237→0.226 | 0.221 | 44.6 | **아니오** (끝 자세가 다름) |
| kneel_counter_guard | 341 | 11.33 | 0.238→0.238 | 0.226 | 0 | 예 (중간에 몸이 크게 오르내림) |
| kneel_muzzle_raise_1 | 324 | 10.77 | 0.255→0.255 | 0.003 | 0 | 예 |
| kneel_muzzle_raise_2 | 321 | 10.67 | 0.237→0.237 | 0.007 | 0 | 예 |
| kneel_rifle_raise | 334 | 11.10 | 0.238→0.238 | 0.007 | 0.04 | 예 |
| rifle_stand_idle_1 | 204 | 6.77 | 0.521→0.521 | 0.005 | 0.04 | 예 |
| rifle_stand_idle_2 | 308 | 10.23 | 0.534→0.534 | 0.014 | 0 | 예 |
| rifle_guard_idle_1 | 406 | 13.50 | 0.528→0.528 | 0.004 | 0 | 예 |
| rifle_look_idle | 371 | 12.33 | 0.540→0.540 | 0.010 | 0 | 예 |
| rifle_hand_idle_1 | 214 | 7.10 | 0.540→0.540 | 0.008 | 0 | 예 |
| stand_to_kneel_1 | 90 | 2.97 | 0.568→0.123 | 0.453 | 144.0 | 아니오 (전환 동작) |
| stand_to_kneel_2 | 95 | 3.13 | 0.568→0.129 | 0.439 | 143.4 | 아니오 (전환 동작) |
| crouch_kneel_idle | 321 | 10.67 | 0.272→0.272 | 0.017 | 0.06 | 예 |
| startled_stagger | 416 | 13.83 | 0.511→0.503 | 0.193 | 54.2 | 아니오 (한 번짜리, 길다) |
| surprise | 68 | 2.23 | 0.552→0.553 | 0.038 | 15.1 | 아니오 (한 번짜리) |

"첫 · 끝 자세 차"는 첫 프레임과 마지막 프레임에서 뼈 회전이 가장 크게 다른 뼈의 각도다. 0이면 이음매 없이 반복된다.
엉덩이 높이 변동폭은 약 40프레임 간격으로 뽑아 잰 값이라 실제 최대치보다 조금 작을 수 있다.

### 기존 클립과 비교 (`A_ROK_Idle_Trench.fbx`)

| 항목 | 기존 `A_ROK_Idle_Trench` | 이번에 받은 것 |
|---|---|---|
| 뼈 개수 | 65 | 53 |
| 뼈 이름 | `mixamorig:Hips` … (믹사모 이름, 손가락 4마디, `HeadTop_End` · `Toe_End` 있음) | `Root` → `Hips` … (접두어 없음), 손가락 `HandLoRA_Bip01_L_Finger0~42`(3마디), 끝 뼈 없음 |
| 최상위 뼈 | `mixamorig:Hips` | `Root` |
| 뼈대 스케일 · 회전 | 0.01 · X 90도 | 1 · 0 |
| 메시 | 없음 (0.4MB) | 있음 (30.8MB) |
| 길이 | 91프레임 · 3.0초 · 30fps | 68~416프레임 · 30fps |
| 엉덩이 높이 ÷ 선 자세 엉덩이 높이 | 0.464 ÷ 0.962 = 0.48 | 무릎 대기 0.247 ÷ 0.53 ≈ 0.47 |

**뼈대가 다르다.** 그대로 기존 Avatar에 얹을 수 없고 Humanoid 리타깃이 필요하다. 무릎 대기의 몸 높이 비율은 기존 클립과 거의 같다(0.48 대 0.47).

## 4. 못 받은 것

없음. 요청한 21개 키를 모두 받았다.

## 5. Unity에 넣을 때 주의할 점

1. **Humanoid 리타깃**: Rig를 Humanoid로 하고, 이 뼈대로 Avatar를 한 번 만들어(Create From This Model) 나머지 20개는 그 Avatar를 복사해 쓴다. 손가락 뼈 이름(`HandLoRA_Bip01_*`)은 자동 매핑이 안 될 수 있다 — 손가락은 선택 항목이므로 비워 두어도 되지만, 총 잡은 손 모양이 필요하면 Configure에서 직접 맞춘다. `Root`는 매핑하지 않는다.
2. **메시 · 재질 떼기**: 파일마다 모델 · 텍스처가 들어 있어 30MB다. 게임 폴더에는 Blender에서 뼈대와 동작만 남겨 다시 내보낸 것(기존 클립처럼 0.5MB 안팎)을 넣는다. 그대로 넣으면 21개 × 30MB와 중복 재질이 프로젝트에 들어간다.
3. **클립 이름**: 지금은 전부 `Take 001`이다. `ANIM_SPEC.md` 1-1 규칙(클립 이름 = 파일명)에 맞춰 바꾼다.
4. **스케일**: 이 모델은 선 키가 약 1.0 단위다. Humanoid 리타깃은 비율로 옮기므로 문제없지만, Generic으로 쓰면 크기가 맞지 않는다.
5. **루프 설정**: 3번 표에서 "예"인 클립만 Loop Time을 켠다. Loop Pose는 첫 · 끝 자세가 같으므로 필요 없다. `kneel_clockwise_guard` · `stand_to_kneel_*` · `startled_stagger` · `surprise`는 한 번 재생하고 대기로 돌아가게 한다.
6. **루트 모션 끄기**: Animator의 Apply Root Motion을 끄고, 클립 Root Transform Rotation · Position(Y) · Position(XZ)을 모두 Bake Into Pose로 둔다. 엉덩이 수평 이동은 이미 0이지만 `kneel_clockwise_guard` · `kneel_counter_guard`는 몸을 돌리는 동작이라 회전이 루트로 새지 않게 한다.
7. **발 미끄러짐**: VARCO의 제자리 처리는 엉덩이 수평 위치를 완전히 고정한다. 몸을 돌리거나 비틀거리는 동작(`kneel_*_guard` 회전 2종, `startled_stagger`)은 발이 바닥에서 미끄러져 보일 수 있다 — 눈으로 확인 필요.
8. **기존 `A_ROK_Idle_Trench`와 이어지는지**: 무릎 계열 11개의 몸 높이는 기존 대기와 거의 같아 짧은 전환(0.25~0.4초)으로 섞일 것으로 본다. 다만 어느 무릎을 꿇었는지 · 총 든 방향이 같은지는 수치로 확인하지 않았다. 다르면 전환 중 다리가 엇갈린다.
   - `kneel_rifle_guard`는 엉덩이가 더 높다(0.304) — 반쯤 일어선 자세일 수 있다.
   - `kneel_counter_guard` · `kneel_clockwise_guard`는 중간에 엉덩이가 0.22만큼 오르내린다(선 키의 약 20%) — 참호 흉벽 위로 머리가 나올 수 있다.
   - `stand_to_kneel_*`는 끝 높이가 0.12~0.13으로 무릎 대기(0.24)보다 훨씬 낮다 — 주저앉거나 양 무릎을 꿇는 자세로 끝나는 듯하다. 무릎 대기로 바로 잇기 전에 끝 자세를 확인한다.
   - `startled_stagger`는 13.8초로 길다. 움찔하는 앞부분만 잘라 쓴다.
9. **총**: 동작은 소총을 든 자세지만 FBX에 총은 없다. 손과 M1이 맞는지는 프리팹의 손잡이 위치(`Grip_Left` 등)로 확인한다.
10. **Animator**: `AC_ROK` 매개변수는 변경 금지(`ANIM_SPEC.md` 1-5)다. 대기 변화를 넣으려면 매개변수 추가 여부를 먼저 정해야 한다.

## 7. 게임 적용 (2026-11-18 오후)

- 이 폴더의 `strip_to_anim_fbx.py`가 `raw/`에서 뼈대와 동작만 남겨 `five_river/Assets/_Project/Art/Animations/ROK/A_ROK_*.fbx`로 내보냈다(21개 + `A_ROK_Varco_TPose.fbx`, 합계 8.4MB). `raw/`는 그 뒤 `.gitignore`에 넣었다(위의 "들어 있지 않다"는 그 전 상태).
- Unity 쪽 설정과 재생 목록은 메뉴 `FiveRivers/Setup/ROK Idle Clips`. 21개 중 7개를 쓰고 14개는 뺐다. 쓴 것 · 뺀 것과 이유, 재생 방식은 `TASKS.md`의 "VARCO 대기 동작 적용 · 2026-11-18".
- 아래 6번 가운데 Unity 임포트 · Humanoid 매핑은 확인했다(자동 매핑이 손가락까지 52개). 클립을 대기 자세에 통째로 섞는 것은 맞지 않았다(선 방향 · 발 간격 · 소총 든 모양이 다름).

## 6. 확인하지 못한 것

- 동작을 **눈으로 보지 않았다**(렌더 · 재생 없음). 키 이름과 수치로만 판단했다.
- Unity 임포트 · Humanoid 매핑 결과 · 기존 클립과의 전환은 확인하지 않았다(Unity를 건드리지 않는 조건).
- 발 미끄러짐, 손과 총의 맞물림, 꿇은 무릎의 좌우.
