# ANIM_SPEC.md — 애니메이션 요청서 (적 · 아군 · 1인칭)

> **브랜치 `animator` · 결정 (2026-10-26)**: 기존 프로토타입을 쓰지 않고 새 프로젝트로 만든다. 이 문서의 '기존 재사용 우선' · '기존 클립' · '기존 Idle/Walk/Run' 같은 항목은 그다음 방법(VARCO · Blender)으로 바로 만들고, 그레이박스 임시본은 `main`에서 새로 만든다.
> 에셋을 넣기 전에 `main`을 이 브랜치로 먼저 병합해 그레이박스 파일과 `.meta`를 받은 뒤 같은 이름으로 덮어쓴다 (CLAUDE.md "브랜치 운영").

> 작성 2026-10-25 · 기준: GDD.md (기획안 v1.2) · 담당: C 아트와 연출(제작), B 전투개발(Animator 연결) · 검수: C 1차, B 2차
> 1순위 목표: **동작이 판단을 돕는다.** 적이 쏘는지 · 숨는지 · 쓰러졌는지, 내 총이 장전 중인지가 바로 보인다.
> 이 문서의 표는 `assets_data.py` 한 곳에서 만들어진다.


## 1. 공통 규칙 — 그레이박스에서 교체만 하면 되도록

### 1-1. 교체 원칙 (가장 중요)
- 개발 중에는 **최종 파일명과 같은 이름의 임시 파일(그레이박스)**을 먼저 프로젝트에 넣고, 프리팹·Animator·사운드 라이브러리를 그 파일에 연결해 둔다.
- 에셋이 오면 **같은 폴더에 같은 이름으로 덮어쓴다.** Unity는 `.meta` 파일의 GUID를 유지하므로 연결이 그대로 살아 있다.
  - Unity 에디터를 열어 둔 채 탐색기에서 덮어쓰거나, 에디터를 닫고 덮어쓴다. **Unity 안에서 기존 파일을 지우고 새 파일을 끌어다 넣지 않는다** (GUID가 바뀌어 연결이 끊긴다).
  - `.meta` 파일은 절대 지우거나 덮어쓰지 않는다.
- 확장자가 같아야 한다 (임시 FBX → 최종 FBX, 임시 WAV → 최종 WAV).
- 모델은 **부품 이름 · 재질 슬롯 이름**도 임시본과 같아야 한다. 코드가 붙는 위치(총구·연기·화재·판정 콜라이더)는 FBX가 아니라 **프리팹(`PF_`) 안의 빈 오브젝트**로 둔다. 그러면 모델이 바뀌어도 위치만 다시 맞추면 된다.
- 애니메이션은 **FBX 안 클립 이름 = 파일명**이어야 Animator 연결이 유지된다.

### 1-2. 이름 규칙
| 접두어 | 종류 | 예 |
|---|---|---|
| `SK_` | 뼈대 있는 모델 (캐릭터) | `SK_CH_KPA_Rifleman.fbx` |
| `SM_` | 고정 모델 (무기·전차·소품) | `SM_VH_T34_85.fbx` |
| `A_` | 애니메이션 클립 FBX | `A_KPA_Run_Rifle.fbx` |
| `AC_` | Animator Controller | `AC_KPA.controller` |
| `PF_` | 프리팹 (코드·콜라이더·소켓 포함) | `PF_VH_T34_85.prefab` |
| `M_` | 재질 | `M_VH_T34_Body.mat` |
| `T_` | 텍스처 · UI 이미지 | `T_VH_T34_Body_BaseColor.png` |
| `VFX_` | 파티클 프리팹 | `VFX_Tank_Fire.prefab` |
| `SFX_` `AMB_` `BGM_` `VO_` `UI_` | 효과음 · 환경음 · 음악 · 음성 · UI 소리 | `SFX_M1_Ping.wav` |
| `FONT_` | 글꼴 | `FONT_UI_Sans.ttf` |

- 단어는 PascalCase, 구분은 `_`. 한글·공백·괄호 금지.
- 분류 코드: `CH` 캐릭터 · `WP` 무기 · `VH` 차량 · `PR` 소품 · `ROK` 국군 · `KPA` 북한군 · `FP` 1인칭.
- 변형은 `_01`, `_02`, `_03` (두 자리). 루프는 이름 끝에 `_Loop` (변형 번호보다 앞: `SFX_Tank_Fire_Loop.wav`).
- 텍스처 접미어: `_BaseColor`, `_Normal`, `_Metallic`, `_Roughness`, `_AO`, `_Emission` (VARCO 출력 그대로 받고, 렌더 파이프라인에 맞는 합본은 C가 Unity에서 만든다).
- 루트 폴더 `Assets/_Project`는 CLAUDE.md의 실제 프로젝트 구조가 정해지면 그대로 바꾼다.

### 1-3. 원본 보관
- VARCO에서 받은 원본(컨셉 이미지 · 생성 결과 · 프롬프트)은 `Assets/_Project/_Source/<파일명>/`에 그대로 둔다. 게임 폴더에는 손본 결과만 넣는다.
- 프롬프트를 바꿔 다시 만들었으면 원본 폴더에 `prompt.txt`로 남긴다. 다음 재생성 때 같은 결과 방향을 유지할 수 있다.


### 1-4. 애니메이션 공통 규격
| 항목 | 규칙 |
|---|---|
| 형식 | FBX, 파일 하나에 클립 하나, 클립 이름 = 파일명 |
| 뼈대 | 적 · 아군: VARCO 자동 리깅 → Unity **Humanoid** (서로 리타깃) / 1인칭: 기존 팔 뼈대 (Generic) |
| 이동 | 모든 이동 동작은 **제자리**. 실제 이동은 NavMesh · 코드가 한다 (Root Motion 미사용) |
| 프레임 | 30fps 이상 |
| 루프 | 루프 클립은 첫 · 마지막 프레임 자세가 같고, Unity에서 Loop Time 체크 |
| 이벤트 | 아래 1-6 이름만 쓴다. 소리 · 탄약 · 진행이 이 이벤트에 붙는다 |

### 1-5. Animator 매개변수 (변경 금지 — 그레이박스와 최종이 같은 이름을 쓴다)
| Controller | 매개변수 |
|---|---|
| `AC_KPA` (적) | `Speed` float(m/s) · `Crouch` bool · `Aim` bool · `Fire` trigger · `Hit` trigger · `Die` trigger · `DieIndex` int(0~2) · `Retreat` bool |
| `AC_ROK` (아군) | `Speed` float · `Crouch` bool · `Fire` trigger · `Call` trigger |
| `AC_FP_M1` | `Fire` trigger · `ReloadEmpty` trigger · `ReloadPartial` trigger · `Sprint` bool · `Equip` trigger · `Unequip` trigger · `Resupply` trigger |
| `AC_FP_Bazooka` | `Fire` trigger · `Reload` trigger · `Equip` trigger · `Unequip` trigger |
| 전차 | Animator 없음. 포탑 회전 · 궤도 스크롤 · 정지는 코드 |

### 1-6. 애니메이션 이벤트 이름
`Fire` · `ClipEject` · `ClipRemove` · `ClipInsert` · `BoltRelease` · `ReloadDone` · `RocketInsert` · `EquipDone` · `UnequipDone` · `AmmoAdded` · `VoiceStart` · `Footstep`

### 1-7. 코드로 처리해 클립을 만들지 않는 것 (1인칭)
- 조준(ADS) 전환, 사격 반동, 시점 흔들림(sway), 걷기 흔들림(bob), 자세(Z) 높이 변화 → 코드(절차적)
- 클립으로 만드는 것은 **장전 · 꺼내기 · 집어넣기 · 보충 · 바주카 발사** 뿐이다. 4명 · 11일 일정에서 손으로 만들 양을 줄이기 위해서다.

### 1-8. VARCO 3D 애니메이션 작업 순서 (적 · 아군)
1. 리깅이 끝난 `SK_CH_KPA_Rifleman` / `SK_CH_ROK_Soldier`를 VARCO에서 연다.
2. 애니메이션 카탈로그에서 아래 표의 **검색어**와 가장 가까운 동작을 고른다.
   > 카탈로그의 실제 동작 키는 VARCO 플로우를 연 상태에서만 확인할 수 있어 아직 표에 넣지 않았다. 플로우를 열어 두면 Claude가 목록을 읽어 키를 채워 줄 수 있다.
3. 총을 드는 동작은 **소총 든 자세(rifle)** 계열을 고른다. 맨손 동작에 총을 붙이면 손이 어긋난다.
4. 같은 동작을 국군 · 북한군 양쪽에 리타깃해 쓴다 (예: 앉아 사격).
5. FBX로 받아 클립 이름을 파일명으로 바꾸고, 그레이박스 클립을 덮어쓴다.
6. 1인칭 동작은 VARCO 카탈로그로 만들기 어렵다(1인칭 팔 · 무기 전용). **기존 프로토타입 클립을 먼저 쓰고**, 없는 것만 Blender에서 손으로 만든다.

## 2. 우선순위
- **A**: 10/28 1차 관찰 테스트 전. 없으면 판단이 안 되는 동작.
- **B**: 10/30 전. 없으면 A 동작으로 대신한다.
- **C**: 여유가 있을 때만.

| 우선 | 파일명 | 이름 | 제작 도구 | 쓰는 구간 |
|---|---|---|---|---|
| A | `A_KPA_Idle_Aim.fbx` | 서서 조준 대기 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S2~S3 |
| A | `A_KPA_Walk_Rifle.fbx` | 소총 들고 걷기 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S2 |
| A | `A_KPA_Run_Rifle.fbx` | 소총 들고 달리기 (돌격·철수 공용) | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S3 |
| A | `A_KPA_Crouch_Aim.fbx` | 엄폐해 앉아 조준 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S3 |
| A | `A_KPA_Fire_Stand.fbx` | 서서 사격 1발 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S2~S3 |
| A | `A_KPA_Death_01.fbx` | 쓰러짐 1 (뒤로) | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S2~S3 |
| A | `A_ROK_Idle_Trench.fbx` | 참호에 몸 낮춰 대기 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S1~S4 |
| A | `A_ROK_Call.fbx` | 박용팔 — 두리번거리며 부르기 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S4 |
| A | `A_ROK_Walk_Tired.fbx` | 지친 걸음 (새벽 보충병) | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S6 |
| A | `A_FP_M1_Idle.fbx` | M1 대기 | 기존 재사용 우선 / Blender 수작업 | 전 구간 |
| A | `A_FP_M1_ReloadEmpty.fbx` | 빈 클립 장전 (핑 → 새 클립) | 기존 재사용 우선 / Blender 수작업 | 전 구간 |
| A | `A_FP_M1_ReloadPartial.fbx` | 잔탄 장전 (남은 클립 빼고 넣기) | 기존 재사용 우선 / Blender 수작업 | 전 구간 |
| A | `A_FP_M1_Equip.fbx` | M1 꺼내기 | 기존 재사용 우선 / Blender 수작업 | S5 |
| A | `A_FP_M1_Unequip.fbx` | M1 집어넣기 | 기존 재사용 우선 / Blender 수작업 | S5 |
| A | `A_FP_Bazooka_Idle.fbx` | 바주카 어깨 견착 대기 | 기존 재사용 우선 / Blender 수작업 | S5 |
| A | `A_FP_Bazooka_Equip.fbx` | 바주카 꺼내기 | 기존 재사용 우선 / Blender 수작업 | S5 |
| A | `A_FP_Bazooka_Fire.fbx` | 바주카 발사 반동 | 기존 재사용 우선 / Blender 수작업 | S5 |
| A | `A_FP_Bazooka_Reload.fbx` | 바주카 재장전 | Blender 수작업 | S5 |
| B | `A_KPA_CrouchRun.fbx` | 몸 낮춰 달리기 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S3 |
| B | `A_KPA_Fire_Crouch.fbx` | 앉아 사격 1발 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S3 |
| B | `A_KPA_HitReact.fbx` | 피격 움찔 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S2~S3 |
| B | `A_KPA_Death_02.fbx` | 쓰러짐 2 (앞으로) | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S2~S3 |
| B | `A_ROK_Fire_Crouch.fbx` | 참호에서 사격 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S3 |
| B | `A_ROK_Idle_Stand.fbx` | 서서 대기 | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S6 |
| B | `A_FP_M1_Sprint.fbx` | M1 들고 달리기 | 기존 재사용 우선 / Blender 수작업 | 전 구간 |
| B | `A_FP_Resupply.fbx` | 탄약 상자에서 보충 (F) | Blender 수작업 | S1, S4 |
| C | `A_KPA_Death_03.fbx` | 쓰러짐 3 (무릎 꿇으며) | VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃 | S2~S3 |

## 3. 항목별 요청


### 3-1. 적 (AC_KPA)


#### 1. 서서 조준 대기 — `A_KPA_Idle_Aim`  [A]

- **파일**: `A_KPA_Idle_Aim.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S2~S3
- **규격**: 루프 2~3초 · 소총 든 자세
- **그레이박스 임시본**: 기존 Idle 클립
- **참고**: 카탈로그 검색어

VARCO 카탈로그 검색어:
```text
rifle idle aim / standing aim
```

검수 — 공통 체크 N + L 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 2. 소총 들고 걷기 — `A_KPA_Walk_Rifle`  [A]

- **파일**: `A_KPA_Walk_Rifle.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S2
- **규격**: 루프 1초 · 제자리
- **그레이박스 임시본**: 기존 Walk

VARCO 카탈로그 검색어:
```text
rifle walk
```

검수 — 공통 체크 N + L 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 3. 소총 들고 달리기 (돌격·철수 공용) — `A_KPA_Run_Rifle`  [A]

- **파일**: `A_KPA_Run_Rifle.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S3
- **규격**: 루프 0.7초 · 제자리
- **그레이박스 임시본**: 기존 Run
- **참고**: 철수도 같은 클립을 쓰고 방향만 돌린다.

VARCO 카탈로그 검색어:
```text
rifle run / sprint with rifle
```

검수 — 공통 체크 N + L 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 4. 몸 낮춰 달리기 — `A_KPA_CrouchRun`  [B]

- **파일**: `A_KPA_CrouchRun.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S3
- **규격**: 루프 0.8초 · 제자리
- **그레이박스 임시본**: A_KPA_Run_Rifle 복사

VARCO 카탈로그 검색어:
```text
crouch run / crouched sprint
```

검수 — 공통 체크 N + L 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 5. 엄폐해 앉아 조준 — `A_KPA_Crouch_Aim`  [A]

- **파일**: `A_KPA_Crouch_Aim.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S3
- **규격**: 루프 2초
- **그레이박스 임시본**: A_KPA_Idle_Aim 복사

VARCO 카탈로그 검색어:
```text
crouch aim / kneeling aim rifle
```

검수 — 공통 체크 N + L 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 6. 서서 사격 1발 — `A_KPA_Fire_Stand`  [A]

- **파일**: `A_KPA_Fire_Stand.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S2~S3
- **규격**: 0.5~0.8초 · 이벤트 Fire
- **그레이박스 임시본**: Idle 복사

VARCO 카탈로그 검색어:
```text
rifle fire / shoot standing
```

검수 — 공통 체크 N 전부, 그리고:
- [ ] 이벤트 Fire 시점에 총구가 앞을 향한다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 7. 앉아 사격 1발 — `A_KPA_Fire_Crouch`  [B]

- **파일**: `A_KPA_Fire_Crouch.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S3
- **규격**: 0.5~0.8초 · 이벤트 Fire
- **그레이박스 임시본**: A_KPA_Fire_Stand 복사

VARCO 카탈로그 검색어:
```text
crouch fire / kneeling shoot
```

검수 — 공통 체크 N 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 8. 피격 움찔 — `A_KPA_HitReact`  [B]

- **파일**: `A_KPA_HitReact.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S2~S3
- **규격**: 0.3~0.5초
- **그레이박스 임시본**: 없음(생략)

VARCO 카탈로그 검색어:
```text
hit reaction / flinch
```

검수 — 공통 체크 N 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 9. 쓰러짐 1 (뒤로) — `A_KPA_Death_01`  [A]

- **파일**: `A_KPA_Death_01.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S2~S3
- **규격**: 1.0~1.5초 · 마지막 프레임 정지
- **그레이박스 임시본**: 캡슐 눕히기 클립

VARCO 카탈로그 검색어:
```text
death backward / fall back
```

검수 — 공통 체크 N 전부, 그리고:
- [ ] 쓰러진 뒤 몸이 땅을 뚫거나 떠 있지 않다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 10. 쓰러짐 2 (앞으로) — `A_KPA_Death_02`  [B]

- **파일**: `A_KPA_Death_02.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S2~S3
- **규격**: 1.0~1.5초
- **그레이박스 임시본**: A_KPA_Death_01 복사

VARCO 카탈로그 검색어:
```text
death forward / fall forward
```

검수 — 공통 체크 N 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 11. 쓰러짐 3 (무릎 꿇으며) — `A_KPA_Death_03`  [C]

- **파일**: `A_KPA_Death_03.fbx`
- **위치**: `Assets/_Project/Art/Animations/KPA/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S2~S3
- **규격**: 1.0~1.5초
- **그레이박스 임시본**: A_KPA_Death_01 복사

VARCO 카탈로그 검색어:
```text
death kneel / collapse
```

검수 — 공통 체크 N 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-2. 아군 (AC_ROK)


#### 12. 참호에 몸 낮춰 대기 — `A_ROK_Idle_Trench`  [A]

- **파일**: `A_ROK_Idle_Trench.fbx`
- **위치**: `Assets/_Project/Art/Animations/ROK/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S1~S4
- **규격**: 루프 3초
- **그레이박스 임시본**: 기존 Idle

VARCO 카탈로그 검색어:
```text
crouch idle / kneeling idle
```

검수 — 공통 체크 N + L 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 13. 참호에서 사격 — `A_ROK_Fire_Crouch`  [B]

- **파일**: `A_ROK_Fire_Crouch.fbx`
- **위치**: `Assets/_Project/Art/Animations/ROK/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S3
- **규격**: 0.5~0.8초 · 이벤트 Fire
- **그레이박스 임시본**: A_ROK_Idle_Trench 복사

VARCO 카탈로그 검색어:
```text
crouch fire / kneeling shoot
```

검수 — 공통 체크 N 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 14. 박용팔 — 두리번거리며 부르기 — `A_ROK_Call`  [A]

- **파일**: `A_ROK_Call.fbx`
- **위치**: `Assets/_Project/Art/Animations/ROK/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S4
- **규격**: 3~4초 · 이벤트 VoiceStart
- **그레이박스 임시본**: Idle 복사
- **참고**: 카탈로그에 맞는 동작이 없으면 '두리번거리기' 대기 동작 + 고개 돌림으로 대신한다.

VARCO 카탈로그 검색어:
```text
look around / searching / shout call
```

검수 — 공통 체크 N 전부, 그리고:
- [ ] 대사 '만수야'와 입·고개 움직임이 어긋나지 않는다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 15. 지친 걸음 (새벽 보충병) — `A_ROK_Walk_Tired`  [A]

- **파일**: `A_ROK_Walk_Tired.fbx`
- **위치**: `Assets/_Project/Art/Animations/ROK/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S6
- **규격**: 루프 1.2초 · 제자리
- **그레이박스 임시본**: 기존 Walk

VARCO 카탈로그 검색어:
```text
tired walk / injured walk / slow walk
```

검수 — 공통 체크 N + L 전부, 그리고:
- [ ] 새벽 역광 실루엣으로 봤을 때 '지친 병사'가 읽힌다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 16. 서서 대기 — `A_ROK_Idle_Stand`  [B]

- **파일**: `A_ROK_Idle_Stand.fbx`
- **위치**: `Assets/_Project/Art/Animations/ROK/`
- **도구**: VARCO 3D 애니메이션 (카탈로그) → Humanoid 리타깃  ·  **쓰는 구간**: S6
- **규격**: 루프 3초
- **그레이박스 임시본**: 기존 Idle

VARCO 카탈로그 검색어:
```text
idle standing
```

검수 — 공통 체크 N + L 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-3. 1인칭 M1 (AC_FP_M1)


#### 17. M1 대기 — `A_FP_M1_Idle`  [A]

- **파일**: `A_FP_M1_Idle.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: 전 구간
- **규격**: 루프 2초 · 미세한 호흡
- **그레이박스 임시본**: 기존 클립

검수 — 공통 체크 N + L + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 18. 빈 클립 장전 (핑 → 새 클립) — `A_FP_M1_ReloadEmpty`  [A]

- **파일**: `A_FP_M1_ReloadEmpty.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: 전 구간
- **규격**: 약 2.5초 · 이벤트 ClipInsert, BoltRelease, ReloadDone
- **그레이박스 임시본**: 기존 클립
- **참고**: 8발째 사격 순간 빈 클립이 튀어나가는 것은 사격 이벤트(ClipEject)로 코드에서 처리하고, 이 클립은 새 클립을 넣는 동작부터 시작한다.

검수 — 공통 체크 N + P 전부, 그리고:
- [ ] ClipInsert 시점에 클립이 총 안으로 들어간다
- [ ] ReloadDone 전에 사격이 되지 않는다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 19. 잔탄 장전 (남은 클립 빼고 넣기) — `A_FP_M1_ReloadPartial`  [A]

- **파일**: `A_FP_M1_ReloadPartial.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: 전 구간
- **규격**: 약 3.2초 · 이벤트 ClipRemove, ClipInsert, BoltRelease, ReloadDone
- **그레이박스 임시본**: A_FP_M1_ReloadEmpty 복사

검수 — 공통 체크 N + P 전부, 그리고:
- [ ] 빼낸 클립이 손에서 사라지는 시점이 자연스럽다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 20. M1 꺼내기 — `A_FP_M1_Equip`  [A]

- **파일**: `A_FP_M1_Equip.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: S5
- **규격**: 0.5초 · 이벤트 EquipDone
- **그레이박스 임시본**: 기존 클립

검수 — 공통 체크 N + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 21. M1 집어넣기 — `A_FP_M1_Unequip`  [A]

- **파일**: `A_FP_M1_Unequip.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: S5
- **규격**: 0.4초 · 이벤트 UnequipDone
- **그레이박스 임시본**: 기존 클립

검수 — 공통 체크 N + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 22. M1 들고 달리기 — `A_FP_M1_Sprint`  [B]

- **파일**: `A_FP_M1_Sprint.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: 전 구간
- **규격**: 루프 0.7초
- **그레이박스 임시본**: A_FP_M1_Idle 복사

검수 — 공통 체크 N + L + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 23. 탄약 상자에서 보충 (F) — `A_FP_Resupply`  [B]

- **파일**: `A_FP_Resupply.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: Blender 수작업  ·  **쓰는 구간**: S1, S4
- **규격**: 0.8~1.0초 · 이벤트 AmmoAdded
- **그레이박스 임시본**: A_FP_M1_Idle 복사
- **참고**: 없으면 화면 아래로 손을 내렸다 올리는 짧은 동작으로 대신한다.

검수 — 공통 체크 N + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-4. 1인칭 바주카 (AC_FP_Bazooka)


#### 24. 바주카 어깨 견착 대기 — `A_FP_Bazooka_Idle`  [A]

- **파일**: `A_FP_Bazooka_Idle.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: S5
- **규격**: 루프 2초
- **그레이박스 임시본**: 기존 클립

검수 — 공통 체크 N + L + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 25. 바주카 꺼내기 — `A_FP_Bazooka_Equip`  [A]

- **파일**: `A_FP_Bazooka_Equip.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: S5
- **규격**: 0.7초 · 이벤트 EquipDone
- **그레이박스 임시본**: 기존 클립

검수 — 공통 체크 N + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 26. 바주카 발사 반동 — `A_FP_Bazooka_Fire`  [A]

- **파일**: `A_FP_Bazooka_Fire.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: 기존 재사용 우선 / Blender 수작업  ·  **쓰는 구간**: S5
- **규격**: 0.6초 · 이벤트 Fire(0프레임)
- **그레이박스 임시본**: 기존 클립

검수 — 공통 체크 N + P 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 27. 바주카 재장전 — `A_FP_Bazooka_Reload`  [A]

- **파일**: `A_FP_Bazooka_Reload.fbx`
- **위치**: `Assets/_Project/Art/Animations/FP/`
- **도구**: Blender 수작업  ·  **쓰는 구간**: S5
- **규격**: 2.0~2.5초 · 이벤트 RocketInsert, ReloadDone
- **그레이박스 임시본**: A_FP_Bazooka_Idle 복사
- **참고**: 전차를 멈추려면 2발이 필요하므로 재장전은 필수.

검수 — 공통 체크 N + P 전부, 그리고:
- [ ] RocketInsert 시점에 로켓이 관 뒤쪽으로 들어간다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:


### 구분 체크
| 짝 | 구분 포인트 |
|---|---|
| 적 서서 조준 ↔ 앉아 조준 | 엄폐물 위로 머리만 보이는지 |
| 적 돌격 ↔ 철수 | 같은 달리기, 몸 방향이 반대 (철수 시 등이 보인다) |
| 빈 클립 장전 ↔ 잔탄 장전 | 잔탄은 클립을 먼저 빼는 동작이 있다 · 시간이 더 길다 |

## 4. 받은 뒤 내가(C → B) 하는 일
1. `_Source/<파일명>/`에 원본 보관
2. Unity Import: Rig 설정(Humanoid/Generic), Animation 탭에서 클립 이름 = 파일명, Loop Time, 이벤트 추가
3. 그레이박스 클립 **덮어쓰기**
4. B가 Animator에서 상태 연결 유지 확인 → 실행 빌드에서 동작
5. 검수판 체크 → 합격 시 TASKS.md 기록, 커밋 `MM.DD/Anim/파일명Swap`

## 5. 공통 체크리스트

### 공통 체크 N — 애니메이션 공통

- [ ] N1. 파일명·폴더가 표와 같다
- [ ] N2. FBX 안 클립 이름 = 파일명 (Animation 탭에서 확인)
- [ ] N3. 제자리 동작 (루트가 앞으로 이동하지 않음, Root Motion 미사용)
- [ ] N4. SK_ 모델에 리타깃했을 때 관절 꺾임·뒤틀림 없음
- [ ] N5. 발이 바닥에서 미끄러지거나 뜨지 않는다
- [ ] N6. 손이 무기에서 떨어지지 않는다 (무기 드는 동작)
- [ ] N7. 30fps 이상, 길이가 표 기준 ±20% 이내
- [ ] N8. 덮어쓴 뒤 Animator 상태의 Motion 연결이 유지된다
- [ ] N9. 콘솔 오류·경고 0

### 공통 체크 L — 루프 동작 추가

- [ ] L1. Loop Time 체크, 이음새에서 튀지 않는다 (10회 반복 재생)

### 공통 체크 P — 1인칭 동작 추가

- [ ] P1. 카메라 기준 화면에서 손·무기가 화면 밖으로 잘리지 않는다
- [ ] P2. 가늠자·가늠쇠가 조준(ADS) 위치에서 일직선
- [ ] P3. 애니메이션 이벤트 이름이 표와 같고 시점이 맞다