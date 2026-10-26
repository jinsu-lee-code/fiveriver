# SOUND_LIST.md — 사운드 요청서 (효과음 · 환경음 · 음악 · 음성)

> **브랜치 `sound` · 결정 (2026-10-26)**: 기존 프로토타입을 쓰지 않고 새 프로젝트로 만든다. 이 문서의 소리는 모두 표대로 VARCO Sound로 만들고, 표의 그레이박스 임시본(비프 · TTS 기본 음성)은 `main`에서 새로 만든다.
> 에셋을 넣기 전에 `main`을 이 브랜치로 먼저 병합해 그레이박스 파일과 `.meta`를 받은 뒤 같은 이름으로 덮어쓴다 (CLAUDE.md "브랜치 운영").

> 작성 2026-10-25 · 기준: GDD.md (기획안 v1.2) · 담당: C 음향(제작 · 연결), D 대사 문구 확정 · 검수: C 1차, D 2차(실제 헤드폰)
> 1순위 목표: **소리로 판단한다.** 조명탄이 뜬다 · 총알이 가깝다 · 전차에 안 먹혔다 · 전차가 멈췄다 · 누군가 없다.
> 기획안 일정 보류 순서의 1번이 '새 녹음'이다. 음성은 녹음 대신 VARCO 음성(TTS)으로 만든다.


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


### 1-4. 사운드 공통 규격
| 항목 | 규칙 |
|---|---|
| 형식 | WAV · 48kHz · 16bit (VARCO Sound 저장 형식이 다르면 변환) |
| 채널 | 효과음 · 음성 모노 / 환경음 · 음악 스테레오 |
| 음량 | 피크 −1 dBFS 이하. 소리 사이 크기 차이는 파일이 아니라 사운드 라이브러리 볼륨으로 조정 |
| 시작 | 앞 무음 없음 (10ms 이하) |
| 루프 | VARCO Sound **Loop** 기능으로 이음새 정리 → 10회 반복 재생으로 확인 |
| 변형 | `_01~_03`. VARCO Sound **Variation** 기능으로 첫 결과에서 만들면 같은 소리 계열이 유지된다 |
| 길이 | VARCO Sound 한 번 생성 최대 60초 |

### 1-5. VARCO Sound 작업 순서 (추천)
1. 아래 프롬프트를 넣고 여러 개를 한 번에 생성해 마음에 드는 것만 남긴다. 결과가 약하면 **Improve Prompt**로 문구를 다듬는다.
2. 변형이 필요한 소리는 고른 것 하나에서 **Variation**으로 `_02`, `_03`을 만든다.
3. 루프 소리는 **Loop** 기능으로 이음새를 만든다.
4. 내장 편집기로 앞 무음 자르기 · 노멀라이즈(−1 dBFS) · 효과음은 모노 변환.
5. 음성은 **Voice**에서 화자 · 속도 · 음높이를 정한다. 같은 인물은 끝까지 같은 화자 설정을 쓴다 (설정값을 `_Source/<파일명>/prompt.txt`에 기록).
6. Unity 안에서 바로 쓰는 경우에도 결과는 반드시 표의 파일명으로 저장해 그레이박스 파일을 덮어쓴다.

공통 꼬리 문구 (효과음 프롬프트 끝에 붙어 있음):
```text
dry close recording, no music, no voices, starts immediately with no silence
```
공통 제외 문구:
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

### 1-6. 재생 규칙 (사운드 라이브러리 데이터에 넣는다)
- 버스: Master / SFX / AMB / BGM / VO / UI
- **대사 중 SFX · AMB 덕킹** (값은 BALANCE.md 4번 `AudioConfig.duckDb`, 실제 헤드폰으로 조정)
- 변형은 무작위 + 같은 변형 연속 금지, 음높이 ±5%
- 3D 사운드: 적 사격 · 전차 · 조명탄. 플레이어 소리는 2D
- 정적 구간(S4): BGM 없음, AMB_Silence_Loop + 대사만

## 2. 우선순위
- **A**: 이것만으로 '소리 나는 게임'. 10/28 1차 관찰 테스트 전.
- **B**: 10/30 전. 없으면 A의 비슷한 소리로 대신.
- **C**: 여유가 있을 때만.

| 우선 | 파일명 | 이름 | 제작 도구 | 쓰는 구간 |
|---|---|---|---|---|
| A | `SFX_M1_Fire_01~03.wav` | M1 사격 (플레이어) | VARCO Sound 효과음 | 전 구간 |
| A | `SFX_M1_Ping.wav` | M1 클립 배출 '핑' | VARCO Sound 효과음 | 전 구간 |
| A | `SFX_M1_ClipInsert.wav` | M1 클립 삽입·노리쇠 닫힘 | VARCO Sound 효과음 | 전 구간 |
| A | `SFX_Bazooka_Fire.wav` | 바주카 발사 | VARCO Sound 효과음 | S5 |
| A | `SFX_Bazooka_Load.wav` | 바주카 로켓 장전 | VARCO Sound 효과음 | S5 |
| A | `SFX_Rocket_HitSide_01.wav`, `SFX_Rocket_HitSide_02.wav` | 로켓 측면 명중 (폭발) | VARCO Sound 효과음 | S5 |
| A | `SFX_Tank_Ricochet_01.wav`, `SFX_Tank_Ricochet_02.wav` | 정면 도탄 | VARCO Sound 효과음 | S5 |
| A | `SFX_Enemy_RifleFire_01~03.wav` | 적 소총 사격 (원거리) | VARCO Sound 효과음 | S2~S3 |
| A | `SFX_Bullet_Whiz_01~04.wav` | 총알 스치는 소리 | VARCO Sound 효과음 | S2~S3 |
| A | `SFX_Impact_Dirt_01~03.wav` | 흙·모래주머니 탄착 | VARCO Sound 효과음 | 전 구간 |
| A | `SFX_Player_Hit_01.wav`, `SFX_Player_Hit_02.wav` | 플레이어 피격 | VARCO Sound 효과음 | S2~S5 |
| A | `SFX_Tank_EngineIdle_Loop.wav` | 전차 엔진 공회전 | VARCO Sound 효과음 + 루프 | S5 |
| A | `SFX_Tank_Move_Loop.wav` | 전차 이동 (엔진+궤도) | VARCO Sound 효과음 + 루프 | S5 |
| A | `SFX_Tank_Cannon.wav` | 전차 주포 발사 | VARCO Sound 효과음 | S5 |
| A | `SFX_Tank_Fire_Loop.wav` | 전차 화재 (측면 2발) | VARCO Sound 효과음 + 루프 | S5~S6 |
| A | `SFX_Tank_EngineStall.wav` | 엔진 멈춤 | VARCO Sound 효과음 | S5 |
| A | `SFX_Flare_Launch.wav` | 조명탄 발사 | VARCO Sound 효과음 | S3 |
| A | `SFX_Flare_Burn_Loop.wav` | 조명탄 타는 소리 | VARCO Sound 효과음 + 루프 | S3 |
| A | `SFX_Foot_Dirt_01~04.wav` | 발걸음 (흙, 걷기) | VARCO Sound 효과음 | 전 구간 |
| A | `SFX_Ammo_Pickup.wav` | 탄약 보충 (F) | VARCO Sound 효과음 | S1, S4 |
| A | `AMB_Night_Hills_Loop.wav` | 여름밤 언덕 | VARCO Sound 효과음 + 루프 | S1~S2 |
| A | `AMB_Battle_Distant_Loop.wav` | 먼 전투 | VARCO Sound 효과음 + 루프 | S3 |
| A | `AMB_Silence_Loop.wav` | 정적 (전투 뒤) | VARCO Sound 효과음 + 루프 | S4 |
| A | `AMB_Dawn_Loop.wav` | 새벽 | VARCO Sound 효과음 + 루프 | S6 |
| A | `BGM_Ending.wav` | 새벽 + 결과 화면 (40초) | VARCO Sound 음악 | S6~S7 |
| A | `UI_Objective_Show.wav` | 목표 표시 | VARCO Sound 효과음 | 전 구간 |
| A | `VO_SQD_Scout_01.wav` | “앞에 움직임 있다! 쏴!” | VARCO Sound 음성 (한국어) | S2 |
| A | `VO_SQD_Flare_01.wav` | “조명탄이다! 엄폐하고 쏴!” | VARCO Sound 음성 (한국어) | S3 |
| A | `VO_SQD_Flare_02.wav` | “어두워진다. 지금 움직여!” | VARCO Sound 음성 (한국어) | S3 |
| A | `VO_SQD_Retreat_01.wav` | “물러간다… 탄약 챙겨!” | VARCO Sound 음성 (한국어) | S3→S4 |
| A | `VO_PARK_Call_01.wav` | “만수야.” | VARCO Sound 음성 (한국어) | S4 |
| A | `VO_PARK_Call_02.wav` | “최만수! …만수 못 봤어?” | VARCO Sound 음성 (한국어) | S4 |
| A | `VO_SQD_Tank_01.wav` | “전차다! 정면은 안 돼, 옆으로 돌아!” | VARCO Sound 음성 (한국어) | S5 |
| A | `VO_SQD_Tank_02.wav` | “옆구리! 옆구리를 노려!” | VARCO Sound 음성 (한국어) | S5 |
| A | `VO_SQD_Ally_01.wav` | “엎드려! 지원 온다!” | VARCO Sound 음성 (한국어) | S5 (아군 지원) |
| B | `SFX_M1_ClipRemove.wav` | M1 잔탄 클립 빼기 | VARCO Sound 효과음 | 전 구간 |
| B | `SFX_M1_DryFire.wav` | 빈 총 방아쇠 | VARCO Sound 효과음 | 전 구간 |
| B | `SFX_Weapon_Switch_01.wav`, `SFX_Weapon_Switch_02.wav` | 무기 전환 (1·2) | VARCO Sound 효과음 | S5 |
| B | `SFX_Enemy_SMG_01.wav`, `SFX_Enemy_SMG_02.wav` | 적 기관단총 연사 | VARCO Sound 효과음 | S3 |
| B | `SFX_Tank_Turret_Loop.wav` | 포탑 회전 | VARCO Sound 효과음 + 루프 | S5 |
| B | `SFX_Tank_MG_01.wav`, `SFX_Tank_MG_02.wav` | 전차 기관총 연사 | VARCO Sound 효과음 | S5 |
| B | `SFX_Tank_Smoke_Loop.wav` | 손상 연기 (측면 1발) | VARCO Sound 효과음 + 루프 | S5 |
| B | `SFX_Artillery_Distant_01~03.wav` | 먼 포격 | VARCO Sound 효과음 | S1~S4 |
| B | `SFX_Foot_Dirt_Run_01~04.wav` | 발걸음 (흙, 달리기) | VARCO Sound 효과음 | 전 구간 |
| B | `SFX_Stance_Change_01.wav`, `SFX_Stance_Change_02.wav` | 자세 바꾸기 (Z) | VARCO Sound 효과음 | 전 구간 |
| B | `BGM_Title_Loop.wav` | 시작 화면 | VARCO Sound 음악 + 루프 | Title |
| B | `BGM_Combat_Bed_Loop.wav` | 전투 밑깔림 | VARCO Sound 음악 + 루프 | S3 |
| B | `UI_Hint_Show.wav` | 힌트 표시 | VARCO Sound 효과음 | S1 |
| B | `UI_Menu_Click.wav` | 메뉴 선택 | VARCO Sound 효과음 | Title·Esc |
| B | `VO_KPA_Shout_01~03.wav` | 적 외침 (“앞으로!” “돌격!” “쏴라!”) | VARCO Sound 음성 (한국어) | S3 |
| B | `VO_OSG_Hurt_01~03.wav` | 플레이어 신음 (말 없음) | VARCO Sound 효과음 (음성 TTS 아님) | S2~S5 |

## 3. 항목별 요청


### 3-1. 무기


#### 1. M1 사격 (플레이어) — `SFX_M1_Fire`  [A]

- **파일**: `SFX_M1_Fire_01.wav`, `SFX_M1_Fire_02.wav`, `SFX_M1_Fire_03.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 1.5초
- **그레이박스 임시본**: 짧은 '빡' 비프

프롬프트:
```text
M1 Garand rifle single gunshot from the shooter's position, sharp powerful crack with a mechanical action clack, short outdoor echo rolling across hills at night, 1940s battle rifle, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- [ ] 한 발에 1회만 재생된다 (기획안 02)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 2. M1 클립 배출 '핑' — `SFX_M1_Ping`  [A]

- **파일**: `SFX_M1_Ping.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.8초
- **그레이박스 임시본**: 높은 비프
- **참고**: M1의 상징적인 소리. 사격음 뒤에 묻히지 않게 따로 들린다.

프롬프트:
```text
M1 Garand en-bloc clip ejecting from the rifle, bright high-pitched metallic ping of spring steel ringing briefly, followed by a small clatter of the empty clip landing on dirt, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- [ ] 사격음과 동시에 나도 '핑'이 또렷이 들린다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 3. M1 클립 삽입·노리쇠 닫힘 — `SFX_M1_ClipInsert`  [A]

- **파일**: `SFX_M1_ClipInsert.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 1.0초
- **그레이박스 임시본**: 중간 비프 2번

프롬프트:
```text
pushing an en-bloc clip of rifle cartridges down into an M1 Garand, metallic slide and firm click, then the bolt snapping forward hard, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 4. M1 잔탄 클립 빼기 — `SFX_M1_ClipRemove`  [B]

- **파일**: `SFX_M1_ClipRemove.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.8초
- **그레이박스 임시본**: 중간 비프

프롬프트:
```text
pulling back a rifle bolt and manually lifting a partly loaded metal clip out, metallic scrape and rattle of cartridges, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 5. 빈 총 방아쇠 — `SFX_M1_DryFire`  [B]

- **파일**: `SFX_M1_DryFire.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.3초
- **그레이박스 임시본**: 짧은 틱

프롬프트:
```text
rifle trigger click on an empty chamber, small dry metallic click, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 6. 무기 전환 (1·2) — `SFX_Weapon_Switch`  [B]

- **파일**: `SFX_Weapon_Switch_01.wav`, `SFX_Weapon_Switch_02.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 0.6초
- **그레이박스 임시본**: 천 스치는 노이즈

프롬프트:
```text
soldier swinging a rifle onto his back and lifting a launcher tube, canvas sling rustle, light wood and metal knock, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 7. 바주카 발사 — `SFX_Bazooka_Fire`  [A]

- **파일**: `SFX_Bazooka_Fire.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 2.0초
- **그레이박스 임시본**: 낮은 '쾅' 비프

프롬프트:
```text
bazooka rocket launch from the shoulder, loud whoosh-boom with the hiss of the rocket motor flying away, back blast blowing dust, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 8. 바주카 로켓 장전 — `SFX_Bazooka_Load`  [A]

- **파일**: `SFX_Bazooka_Load.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 1.2초
- **그레이박스 임시본**: 중간 비프 2번

프롬프트:
```text
sliding a rocket into the rear of a metal launcher tube, hollow metal slide and a latch clicking shut, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 9. 로켓 측면 명중 (폭발) — `SFX_Rocket_HitSide`  [A]

- **파일**: `SFX_Rocket_HitSide_01.wav`, `SFX_Rocket_HitSide_02.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 2.5초
- **그레이박스 임시본**: 낮은 노이즈 폭발

프롬프트:
```text
anti-tank rocket hitting the side armor of a heavy steel tank, deep metallic explosion boom with debris and ringing steel, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- [ ] 정면 도탄(SFX_Tank_Ricochet)과 소리만 듣고 구분된다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 10. 정면 도탄 — `SFX_Tank_Ricochet`  [A]

- **파일**: `SFX_Tank_Ricochet_01.wav`, `SFX_Tank_Ricochet_02.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 1.5초
- **그레이박스 임시본**: 높은 '팅' 비프

프롬프트:
```text
rocket glancing off thick sloped tank armor, loud sharp metallic clang and a whining ricochet, no explosion, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- [ ] '안 통했다'가 바로 느껴진다 (폭발처럼 들리지 않는다)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 11. 적 소총 사격 (원거리) — `SFX_Enemy_RifleFire`  [A]

- **파일**: `SFX_Enemy_RifleFire_01.wav`, `SFX_Enemy_RifleFire_02.wav`, `SFX_Enemy_RifleFire_03.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S2~S3
- **규격**: 모노 · 1.5초 · 3D 사운드
- **그레이박스 임시본**: 작은 '빡' 비프

프롬프트:
```text
bolt-action rifle shot heard from 50 meters away at night, sharp report with long echo over hills, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 12. 적 기관단총 연사 — `SFX_Enemy_SMG`  [B]

- **파일**: `SFX_Enemy_SMG_01.wav`, `SFX_Enemy_SMG_02.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S3
- **규격**: 모노 · 1.2초 · 3D 사운드
- **그레이박스 임시본**: 빠른 비프 연속

프롬프트:
```text
fast submachine gun burst of about one second heard from 50 meters away at night, rapid rattling shots with echo, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 13. 총알 스치는 소리 — `SFX_Bullet_Whiz`  [A]

- **파일**: `SFX_Bullet_Whiz_01.wav`, `SFX_Bullet_Whiz_02.wav`, `SFX_Bullet_Whiz_03.wav`, `SFX_Bullet_Whiz_04.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S2~S3
- **규격**: 모노 · 0.5초
- **그레이박스 임시본**: 짧은 쉬익

프롬프트:
```text
rifle bullet passing very close by the listener's head, supersonic snap and quick whiz, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 14. 흙·모래주머니 탄착 — `SFX_Impact_Dirt`  [A]

- **파일**: `SFX_Impact_Dirt_01.wav`, `SFX_Impact_Dirt_02.wav`, `SFX_Impact_Dirt_03.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.4초
- **그레이박스 임시본**: 짧은 퍽

프롬프트:
```text
bullet striking dirt and a burlap sandbag right next to the listener, dull thud with soil scattering, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 15. 플레이어 피격 — `SFX_Player_Hit`  [A]

- **파일**: `SFX_Player_Hit_01.wav`, `SFX_Player_Hit_02.wav`
- **위치**: `Assets/_Project/Audio/SFX/Weapon/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S2~S5
- **규격**: 모노 · 0.5초
- **그레이박스 임시본**: 낮은 퍽
- **참고**: 신음은 VO_OSG_Hurt로 따로 겹친다.

프롬프트:
```text
muffled impact of a bullet hitting a soldier, heavy dull thump with cloth, heard from inside, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-2. 전차


#### 16. 전차 엔진 공회전 — `SFX_Tank_EngineIdle_Loop`  [A]

- **파일**: `SFX_Tank_EngineIdle_Loop.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S5
- **규격**: 모노 · 루프 6초 · 3D 사운드
- **그레이박스 임시본**: 낮은 웅 소리 루프

프롬프트:
```text
1940s V12 diesel tank engine idling, rough deep rumble with uneven knocking, steady, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 17. 전차 이동 (엔진+궤도) — `SFX_Tank_Move_Loop`  [A]

- **파일**: `SFX_Tank_Move_Loop.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S5
- **규격**: 모노 · 루프 8초 · 3D 사운드
- **그레이박스 임시본**: 웅 + 철컥 루프

프롬프트:
```text
heavy tank moving slowly on a dirt road, diesel engine revving, squeaking and clanking steel tracks and road wheels, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 18. 포탑 회전 — `SFX_Tank_Turret_Loop`  [B]

- **파일**: `SFX_Tank_Turret_Loop.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S5
- **규격**: 모노 · 루프 3초
- **그레이박스 임시본**: 얇은 윙 루프

프롬프트:
```text
tank turret slowly traversing, electric motor whine with grinding gears, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 19. 전차 주포 발사 — `SFX_Tank_Cannon`  [A]

- **파일**: `SFX_Tank_Cannon.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 4초 · 3D 사운드
- **그레이박스 임시본**: 아주 낮은 쾅

프롬프트:
```text
85mm tank cannon firing nearby, massive boom with a pressure wave, long rolling echo over hills at night, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 20. 전차 기관총 연사 — `SFX_Tank_MG`  [B]

- **파일**: `SFX_Tank_MG_01.wav`, `SFX_Tank_MG_02.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 1.5초
- **그레이박스 임시본**: 빠른 비프 연속

프롬프트:
```text
tank-mounted machine gun firing a short burst, heavy rhythmic rattle with echo, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 21. 손상 연기 (측면 1발) — `SFX_Tank_Smoke_Loop`  [B]

- **파일**: `SFX_Tank_Smoke_Loop.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S5
- **규격**: 모노 · 루프 5초
- **그레이박스 임시본**: 쉬익 루프

프롬프트:
```text
smoke pouring out of a damaged tank engine deck, hissing steam and small crackles, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 22. 전차 화재 (측면 2발) — `SFX_Tank_Fire_Loop`  [A]

- **파일**: `SFX_Tank_Fire_Loop.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S5~S6
- **규격**: 모노 · 루프 8초 · 3D 사운드
- **그레이박스 임시본**: 노이즈 루프

프롬프트:
```text
burning tank, roaring fire with crackling flames and occasional small popping of ammunition, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 23. 엔진 멈춤 — `SFX_Tank_EngineStall`  [A]

- **파일**: `SFX_Tank_EngineStall.wav`
- **위치**: `Assets/_Project/Audio/SFX/Tank/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S5
- **규격**: 모노 · 3초
- **그레이박스 임시본**: 내려가는 웅

프롬프트:
```text
big diesel tank engine coughing, sputtering and dying out completely, then silence, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- [ ] '전차가 멈췄다'가 소리만으로 전달된다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-3. 조명탄·환경


#### 24. 조명탄 발사 — `SFX_Flare_Launch`  [A]

- **파일**: `SFX_Flare_Launch.wav`
- **위치**: `Assets/_Project/Audio/SFX/Env/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S3
- **규격**: 모노 · 2초
- **그레이박스 임시본**: 퐁 + 올라가는 휘파람

프롬프트:
```text
illumination flare fired from a mortar in the distance, hollow thump pop then a faint rising whistle, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- [ ] '곧 밝아진다'는 신호로 알아들을 수 있다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 25. 조명탄 타는 소리 — `SFX_Flare_Burn_Loop`  [A]

- **파일**: `SFX_Flare_Burn_Loop.wav`
- **위치**: `Assets/_Project/Audio/SFX/Env/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S3
- **규격**: 모노 · 루프 6초 · 3D 사운드
- **그레이박스 임시본**: 쉬익 루프

프롬프트:
```text
magnesium parachute flare burning high in the sky, soft steady sizzling hiss with faint crackle, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 26. 먼 포격 — `SFX_Artillery_Distant`  [B]

- **파일**: `SFX_Artillery_Distant_01.wav`, `SFX_Artillery_Distant_02.wav`, `SFX_Artillery_Distant_03.wav`
- **위치**: `Assets/_Project/Audio/SFX/Env/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S1~S4
- **규격**: 스테레오 · 4초
- **그레이박스 임시본**: 낮은 웅

프롬프트:
```text
distant artillery explosion far away at night, deep muffled rumble rolling over hills, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-4. 폴리


#### 27. 발걸음 (흙, 걷기) — `SFX_Foot_Dirt`  [A]

- **파일**: `SFX_Foot_Dirt_01.wav`, `SFX_Foot_Dirt_02.wav`, `SFX_Foot_Dirt_03.wav`, `SFX_Foot_Dirt_04.wav`
- **위치**: `Assets/_Project/Audio/SFX/Foley/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.3초
- **그레이박스 임시본**: 작은 퍽

프롬프트:
```text
single footstep of a soldier's leather boot on dry packed dirt with small pebbles, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 28. 발걸음 (흙, 달리기) — `SFX_Foot_Dirt_Run`  [B]

- **파일**: `SFX_Foot_Dirt_Run_01.wav`, `SFX_Foot_Dirt_Run_02.wav`, `SFX_Foot_Dirt_Run_03.wav`, `SFX_Foot_Dirt_Run_04.wav`
- **위치**: `Assets/_Project/Audio/SFX/Foley/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.3초
- **그레이박스 임시본**: 작은 퍽

프롬프트:
```text
single heavy running footstep of a soldier's boot on dry dirt with gear rattling slightly, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 29. 자세 바꾸기 (Z) — `SFX_Stance_Change`  [B]

- **파일**: `SFX_Stance_Change_01.wav`, `SFX_Stance_Change_02.wav`
- **위치**: `Assets/_Project/Audio/SFX/Foley/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.6초
- **그레이박스 임시본**: 천 노이즈

프롬프트:
```text
soldier crouching down quickly, canvas gear and cotton uniform rustle, small metal buckle jingle, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 30. 탄약 보충 (F) — `SFX_Ammo_Pickup`  [A]

- **파일**: `SFX_Ammo_Pickup.wav`
- **위치**: `Assets/_Project/Audio/SFX/Foley/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S1, S4
- **규격**: 모노 · 1.2초
- **그레이박스 임시본**: 찰랑 비프

프롬프트:
```text
grabbing metal rifle clips out of a wooden crate and stuffing them into a cloth bandolier, metallic jingle and cloth rustle, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-5. 환경음


#### 31. 여름밤 언덕 — `AMB_Night_Hills_Loop`  [A]

- **파일**: `AMB_Night_Hills_Loop.wav`
- **위치**: `Assets/_Project/Audio/AMB/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S1~S2
- **규격**: 스테레오 · 루프 30초
- **그레이박스 임시본**: 낮은 바람 노이즈 루프

프롬프트:
```text
summer night in the hills of Korea, crickets and insects, warm gentle wind through dry grass, wide stereo ambience, no music, no voices
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 32. 먼 전투 — `AMB_Battle_Distant_Loop`  [A]

- **파일**: `AMB_Battle_Distant_Loop.wav`
- **위치**: `Assets/_Project/Audio/AMB/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S3
- **규격**: 스테레오 · 루프 30초
- **그레이박스 임시본**: 낮은 웅 루프

프롬프트:
```text
distant battle at night across the valley, sporadic far-away rifle fire and muffled explosions, wide stereo ambience, no music, no voices
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 33. 정적 (전투 뒤) — `AMB_Silence_Loop`  [A]

- **파일**: `AMB_Silence_Loop.wav`
- **위치**: `Assets/_Project/Audio/AMB/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S4
- **규격**: 스테레오 · 루프 30초 · 음악 없음
- **그레이박스 임시본**: 아주 작은 바람 루프
- **참고**: 정적 구간에는 음악을 넣지 않는다. 대사(박용팔 호출)가 가장 크게 들려야 한다.

프롬프트:
```text
very quiet night right after a battle, faint wind, sparse crickets, occasional soft trickle of settling dirt, wide stereo ambience, no music, no voices
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 34. 새벽 — `AMB_Dawn_Loop`  [A]

- **파일**: `AMB_Dawn_Loop.wav`
- **위치**: `Assets/_Project/Audio/AMB/`
- **도구**: VARCO Sound 효과음 + 루프  ·  **쓰는 구간**: S6
- **규격**: 스테레오 · 루프 30초
- **그레이박스 임시본**: 새소리 비슷한 비프 루프

프롬프트:
```text
dawn in the hills after a battle, a few early birds chirping softly, gentle cool morning breeze, distant crackle of a dying fire, wide stereo ambience, no music, no voices
```

검수 — 공통 체크 S + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-6. 음악


#### 35. 시작 화면 — `BGM_Title_Loop`  [B]

- **파일**: `BGM_Title_Loop.wav`
- **위치**: `Assets/_Project/Audio/BGM/`
- **도구**: VARCO Sound 음악 + 루프  ·  **쓰는 구간**: Title
- **규격**: 스테레오 · 루프 60초
- **그레이박스 임시본**: 무음 60초

프롬프트:
```text
melancholic orchestral theme for a Korean War memorial game, solo cello over soft sustained strings, slow tempo around 60 bpm, D minor, sparse and restrained, no percussion, no vocals
```
제외(네거티브):
```text
heroic, epic, triumphant, drums, electronic, vocals
```

검수 — 공통 체크 S + B + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 36. 전투 밑깔림 — `BGM_Combat_Bed_Loop`  [B]

- **파일**: `BGM_Combat_Bed_Loop.wav`
- **위치**: `Assets/_Project/Audio/BGM/`
- **도구**: VARCO Sound 음악 + 루프  ·  **쓰는 구간**: S3
- **규격**: 스테레오 · 루프 60초 · 작게 깔림
- **그레이박스 임시본**: 무음 60초

프롬프트:
```text
low tense underscore for night combat, slow pulsing low drum and dark sustained strings drone, almost no melody, 80 bpm, leaves room for gunfire, no vocals
```
제외(네거티브):
```text
heroic, epic, action trailer, electronic, vocals
```

검수 — 공통 체크 S + B + O 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 37. 새벽 + 결과 화면 (40초) — `BGM_Ending`  [A]

- **파일**: `BGM_Ending.wav`
- **위치**: `Assets/_Project/Audio/BGM/`
- **도구**: VARCO Sound 음악  ·  **쓰는 구간**: S6~S7
- **규격**: 스테레오 · 정확히 40초 (새벽 20 + 결과 20) · 앞 3초 페이드인 · 끝 3초 페이드아웃
- **그레이박스 임시본**: 무음 40초
- **참고**: 8:15 강제 전환 때도 같은 40초가 그대로 재생된다.

프롬프트:
```text
gentle solo piano with distant soft strings, sorrowful yet quietly hopeful, slow, for a dawn after a battle and reading a fallen soldier's letter, 40 seconds, slow fade in, ends softly, no percussion, no vocals
```
제외(네거티브):
```text
heroic, triumphant, drums, electronic, vocals
```

검수 — 공통 체크 S + B 전부, 그리고:
- [ ] 편지(8초)가 나올 때 음악이 가장 조용하다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-7. UI


#### 38. 목표 표시 — `UI_Objective_Show`  [A]

- **파일**: `UI_Objective_Show.wav`
- **위치**: `Assets/_Project/Audio/UI/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: 전 구간
- **규격**: 모노 · 0.4초 · 아주 작게
- **그레이박스 임시본**: 부드러운 비프

프롬프트:
```text
soft subtle paper rustle cue, very short and quiet, like turning a page of a field notebook, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon, chime, reward, ding
```

검수 — 공통 체크 S 전부, 그리고:
- [ ] 보상·성공음처럼 들리지 않는다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 39. 힌트 표시 — `UI_Hint_Show`  [B]

- **파일**: `UI_Hint_Show.wav`
- **위치**: `Assets/_Project/Audio/UI/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: S1
- **규격**: 모노 · 0.3초 · 아주 작게
- **그레이박스 임시본**: 부드러운 비프

프롬프트:
```text
very soft low wooden tick, single gentle tap, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon, chime, reward, ding
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 40. 메뉴 선택 — `UI_Menu_Click`  [B]

- **파일**: `UI_Menu_Click.wav`
- **위치**: `Assets/_Project/Audio/UI/`
- **도구**: VARCO Sound 효과음  ·  **쓰는 구간**: Title·Esc
- **규격**: 모노 · 0.2초
- **그레이박스 임시본**: 짧은 틱

프롬프트:
```text
muted mechanical click of an old typewriter key, single, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
music, melody, speech, modern gun, synth, laser, cartoon
```

검수 — 공통 체크 S 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-8. 음성 · 분대장


#### 41. “앞에 움직임 있다! 쏴!” — `VO_SQD_Scout_01`  [A]

- **파일**: `VO_SQD_Scout_01.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S2
- **규격**: 모노 · 2초 이내 · 화자: 분대장(30대 남성, 낮고 거친 목소리, 다급하게 낮춘 외침)
- **그레이박스 임시본**: TTS 기본 음성

음성 문장 (D 확정 전 제안 문구):
```text
앞에 움직임 있다! 쏴!
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 42. “조명탄이다! 엄폐하고 쏴!” — `VO_SQD_Flare_01`  [A]

- **파일**: `VO_SQD_Flare_01.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S3
- **규격**: 모노 · 2초 이내 · 분대장, 큰 외침
- **그레이박스 임시본**: TTS 기본 음성
- **참고**: 조명탄 규칙(밝으면 엄폐 사격)을 가르치는 핵심 대사.

음성 문장 (D 확정 전 제안 문구):
```text
조명탄이다! 엄폐하고 쏴!
```

검수 — 공통 체크 S + R 전부, 그리고:
- [ ] 조명탄 발사음과 겹쳐도 알아듣는다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 43. “어두워진다. 지금 움직여!” — `VO_SQD_Flare_02`  [A]

- **파일**: `VO_SQD_Flare_02.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S3
- **규격**: 모노 · 2초 이내 · 분대장, 낮고 단호하게
- **그레이박스 임시본**: TTS 기본 음성
- **참고**: 어두울 때 이동·보충 규칙을 가르치는 대사.

음성 문장 (D 확정 전 제안 문구):
```text
어두워진다. 지금 움직여!
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 44. “물러간다… 탄약 챙겨!” — `VO_SQD_Retreat_01`  [A]

- **파일**: `VO_SQD_Retreat_01.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S3→S4
- **규격**: 모노 · 2.5초 이내 · 분대장, 숨 고르며
- **그레이박스 임시본**: TTS 기본 음성

음성 문장 (D 확정 전 제안 문구):
```text
물러간다… 탄약 챙겨!
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 45. “전차다! 정면은 안 돼, 옆으로 돌아!” — `VO_SQD_Tank_01`  [A]

- **파일**: `VO_SQD_Tank_01.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S5
- **규격**: 모노 · 3초 이내 · 분대장, 큰 외침
- **그레이박스 임시본**: TTS 기본 음성
- **참고**: 전차 규칙(측면 공격)을 가르치는 핵심 대사.

음성 문장 (D 확정 전 제안 문구):
```text
전차다! 정면은 안 돼, 옆으로 돌아!
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 46. “옆구리! 옆구리를 노려!” — `VO_SQD_Tank_02`  [A]

- **파일**: `VO_SQD_Tank_02.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S5
- **규격**: 모노 · 2초 이내 · 분대장, 정면 도탄 직후
- **그레이박스 임시본**: TTS 기본 음성

음성 문장 (D 확정 전 제안 문구):
```text
옆구리! 옆구리를 노려!
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 47. “엎드려! 지원 온다!” — `VO_SQD_Ally_01`  [A]

- **파일**: `VO_SQD_Ally_01.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S5 (아군 지원)
- **규격**: 모노 · 2초 이내 · 분대장, 큰 외침
- **그레이박스 임시본**: TTS 기본 음성

음성 문장 (D 확정 전 제안 문구):
```text
엎드려! 지원 온다!
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-9. 음성 · 박용팔


#### 48. “만수야.” — `VO_PARK_Call_01`  [A]

- **파일**: `VO_PARK_Call_01.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S4
- **규격**: 모노 · 1.5초 이내 · 화자: 박용팔(30대 남성, 지치고 따뜻한 목소리, 작게 부르듯)
- **그레이박스 임시본**: TTS 기본 음성

음성 문장 (D 확정 전 제안 문구):
```text
만수야.
```

검수 — 공통 체크 S + R 전부, 그리고:
- [ ] 정적 환경음 위에서 또렷이 들린다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 49. “최만수! …만수 못 봤어?” — `VO_PARK_Call_02`  [A]

- **파일**: `VO_PARK_Call_02.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S4
- **규격**: 모노 · 3초 이내 · 박용팔, 불안이 섞인 목소리
- **그레이박스 임시본**: TTS 기본 음성

음성 문장 (D 확정 전 제안 문구):
```text
최만수! …만수 못 봤어?
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-10. 음성 · 적


#### 50. 적 외침 (“앞으로!” “돌격!” “쏴라!”) — `VO_KPA_Shout`  [B]

- **파일**: `VO_KPA_Shout_01.wav`, `VO_KPA_Shout_02.wav`, `VO_KPA_Shout_03.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 음성 (한국어)  ·  **쓰는 구간**: S3
- **규격**: 모노 · 각 1.5초 이내 · 20대 남성 여러 화자, 멀리서 외치는 느낌(리버브는 게임에서)
- **그레이박스 임시본**: TTS 기본 음성
- **참고**: 화자를 3명 다르게 골라 한 사람처럼 들리지 않게 한다.

음성 문장 (D 확정 전 제안 문구):
```text
_01 앞으로! / _02 돌격! / _03 쏴라!
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-11. 음성 · 오석구


#### 51. 플레이어 신음 (말 없음) — `VO_OSG_Hurt`  [B]

- **파일**: `VO_OSG_Hurt_01.wav`, `VO_OSG_Hurt_02.wav`, `VO_OSG_Hurt_03.wav`
- **위치**: `Assets/_Project/Audio/VO/`
- **도구**: VARCO Sound 효과음 (음성 TTS 아님)  ·  **쓰는 구간**: S2~S5
- **규격**: 모노 · 0.6초 · 20세 앳된 남성
- **그레이박스 임시본**: 낮은 짧은 비프
- **참고**: TTS로는 신음이 어색하다. 효과음 생성으로 만든다.

프롬프트:
```text
young man's short pained grunt through clenched teeth, breathless, no words, dry close recording, no music, no voices, starts immediately with no silence
```
제외(네거티브):
```text
words, speech, scream, music
```

검수 — 공통 체크 S + R 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:


### 구분 체크 (헷갈리기 쉬운 짝)
| 짝 | 구분 포인트 | 확인 방법 |
|---|---|---|
| 정면 도탄 ↔ 측면 명중 | 높은 '쨍' + 튕김 ↔ 낮은 폭발 | 눈 감고 들어 구분 |
| 플레이어 사격 ↔ 적 사격 | 가깝고 기계음 ↔ 멀고 울림 | 주 공세 구간에서 확인 |
| 조명탄 발사 ↔ 먼 포격 | 퐁 + 휘파람 ↔ 둔한 웅 | 같은 구간에서 번갈아 재생 |
| 엔진 멈춤 ↔ 엔진 공회전 | 끝에 완전히 꺼짐 | 측면 2발 직후 |
| UI 목표 표시 ↔ 보상음 | 종이 소리 · 아주 작게 | '성공음'처럼 들리면 불합격 |

## 4. 받은 뒤 내가(C) 하는 일
1. `_Source/<파일명>/`에 원본 · 프롬프트(음성은 화자 설정) 보관
2. 규격(48kHz/16bit · 채널 · 피크 · 앞 무음) 맞추기
3. 그레이박스 WAV **덮어쓰기** (같은 폴더 · 같은 이름)
4. 실행 빌드에서 해당 구간 재생 확인 → D가 실제 시연 헤드폰으로 2차 확인
5. 검수판 체크 → 합격 시 TASKS.md 기록, 커밋 `MM.DD/Sound/파일명Swap`

## 5. 만들지 않는 것

- 점수·킬 카운트 표시 — 결말의 정서와 충돌 (기획안 02)
- 히트마커 그림·소리 — 같은 이유
- 처치 보상음·성공 효과음 — 같은 이유. UI 소리도 보상처럼 들리지 않게
- 정적 구간 음악 — 부재가 대사로만 전달되도록
- 새 무기·새 맵·혹한·분대 명령 관련 에셋 — 범위 제외 (GDD 9번)

## 6. 공통 체크리스트

### 공통 체크 S — 사운드 공통

- [ ] S1. 파일명·폴더가 표와 같다 (변형 _01~ 번호 포함)
- [ ] S2. WAV 48kHz 16bit, 채널이 규칙대로 (효과음 모노 / 환경음·음악 스테레오)
- [ ] S3. 피크 −1 dBFS 이하, 찢어지는 소리(클리핑) 없음
- [ ] S4. 앞에 무음 없이 바로 시작한다
- [ ] S5. 끝이 잘리지 않고 자연스럽게 줄어든다
- [ ] S6. AI 잡음(지글거림·금속성 왜곡·말소리 섞임) 없음
- [ ] S7. 1950년대 느낌 (현대 총기·전자음 느낌 없음)
- [ ] S8. 변형끼리 서로 다르지만 같은 소리로 들린다
- [ ] S9. 게임 안에서 다른 소리와 겹쳐도 구분된다
- [ ] S10. 덮어쓴 뒤 사운드 라이브러리 연결 유지

### 공통 체크 O — 루프 사운드 추가

- [ ] O1. 이음새에서 '틱' 소리·끊김 없음 (10회 반복 재생)
- [ ] O2. 시작과 끝 음량이 같다
- [ ] O3. 반복 패턴이 30초 안에 눈에 띄지 않는다

### 공통 체크 R — 음성 추가

- [ ] R1. 문장·발음이 대본과 같다
- [ ] R2. 감정·톤이 장면과 맞다
- [ ] R3. 길이가 자막 표시 시간 안에 들어간다
- [ ] R4. 1950년대 군인 말투로 어색하지 않다
- [ ] R5. 대사 중 효과음을 낮췄을 때 잘 들린다 (실제 헤드폰)

### 공통 체크 B — 음악 추가

- [ ] B1. 길이가 표와 같다
- [ ] B2. 감정 방향이 장면과 맞다 (승리감·영웅적 분위기 금지)
- [ ] B3. 대사·효과음을 덮지 않는다 (게임 안 확인)