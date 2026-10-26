# ART_SPEC.md — 아트 에셋 요청서 (3D 모델 · VFX · UI)

> **브랜치 `art` · 결정 (2026-10-26)**: 기존 프로토타입을 쓰지 않고 새 프로젝트로 만든다. 이 문서의 '기존 재사용 우선' · '기존 프로토타입 팔' · '기존 프로토타입 총' 같은 항목은 그다음 방법(VARCO · Blender)으로 바로 만들고, 그레이박스 임시본은 `main`에서 새로 만든다.
> 에셋을 넣기 전에 `main`을 이 브랜치로 먼저 병합해 그레이박스 파일과 `.meta`를 받은 뒤 같은 이름으로 덮어쓴다 (CLAUDE.md "브랜치 운영").

> 작성 2026-10-25 · 기준: GDD.md (기획안 v1.2) · 담당: C 아트와 연출 · 검수: C 1차, D 2차
> 1순위 목표: **어두운 화면에서도 실루엣만으로 무엇인지 알 수 있다.** 텍스처 디테일보다 형태 · 재질 · 발광.
> 기존 프로토타입에 있는 에셋은 **먼저 재사용**하고(기획안 03), 이 목록은 새로 필요하거나 교체할 것 기준이다.
> 이 문서의 표는 `assets_data.py` 한 곳에서 만들어진다. 이름을 바꿀 때는 그 파일을 고치고 다시 생성한다.


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


### 1-4. 모델 공통 규격
| 항목 | 규칙 |
|---|---|
| 단위 · 축 | 1 unit = 1m, Unity 기준 위 +Y · 앞 +Z |
| 내보내기 (Blender) | FBX · Apply Transform · Forward −Z, Up Y · Scale 1.0 · Apply Scalings: FBX All |
| 피벗 | 캐릭터 · 소품 = 바닥 중앙 / 무기 = 손잡이 / 움직이는 부품 = 회전축 |
| 삼각형 예산 | 1인칭 무기 0.8~1.5만 · 캐릭터 1~2만 · 전차 3~6만 · 소품 0.05~0.4만 |
| 텍스처 | 1인칭 무기 · 전차 2K, 캐릭터 1K, 소품 512~1K, PNG |
| 재질 슬롯 | 표에 적은 이름 그대로, 모델당 2개 이하 (전차 2개) |
| 콜라이더 | FBX에 넣지 않는다. 프리팹에서 단순 도형으로 |
| LOD | 이번 데모에서는 만들지 않는다 |

### 1-5. VARCO 3D 작업 순서 (추천)
1. **텍스트 → 이미지**로 컨셉 이미지를 먼저 만든다. 3~4장 뽑아 C가 한 장 고른다. (바로 텍스트 → 3D도 되지만, 이미지를 먼저 고르면 결과가 덜 흔들린다.)
2. 고른 이미지를 **이미지 → 3D**에 넣는다. 캐릭터는 정면 A-포즈, 무기는 정확한 옆면, 전차는 3/4 정면 이미지를 쓴다.
3. 생성 결과의 폴리곤 수를 예산에 맞게 조정한다. 형태가 무너질 만큼 줄이지 않는다.
4. 캐릭터는 **자동 리깅** → Unity에서 Humanoid로 가져온다.
5. 받은 FBX는 Blender에서 축 · 크기 · 피벗 · 부품 분리 · 이름을 정리해 위 규칙으로 내보낸다.
6. 같은 계열(국군 3종, 북한군 2종)은 **첫 결과 이미지를 참고 이미지로 넣어** 다음 것을 만든다. 같은 부대처럼 보인다.

공통 스타일 문구 (모든 3D 프롬프트 끝에 붙어 있음):
```text
realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
캐릭터 자세 문구:
```text
full body, A-pose with arms 45 degrees down, front view, standing straight, feet shoulder-width apart, empty open hands, no weapon, no loose hanging straps, symmetrical
```
> 영어 프롬프트를 기본으로 적었다. 한국어로도 시험해 보고 결과가 나은 쪽을 쓴다.

## 2. 우선순위
- **A**: 이것만 있어도 처음부터 결말까지 '보이는 게임'이 된다. 10/28 1차 관찰 테스트 전 목표.
- **B**: 10/30 기능 추가 종료 전까지. 늦으면 그레이박스 또는 기존 에셋으로 출시.
- **C**: 여유가 있을 때만. 일정이 밀리면 가장 먼저 보류.

| 우선 | 파일명 | 이름 | 제작 도구 | 쓰는 구간 |
|---|---|---|---|---|
| A | `SK_CH_ROK_Soldier.fbx` | 국군 병사 (아군 공용) | VARCO 3D | S2~S6 |
| A | `SK_CH_KPA_Rifleman.fbx` | 북한군 보병 (적 공용) | VARCO 3D | S2~S3 |
| A | `SK_CH_ROK_Arms.fbx` | 1인칭 팔 (오석구) | 기존 재사용 우선 → 없으면 ROK_Soldier에서 잘라내기(Blender) | 전 구간 |
| A | `SM_WP_M1Garand.fbx` | M1 개런드 | VARCO 3D + Blender 정리 | 전 구간 |
| A | `SM_WP_M1Garand_Clip.fbx` | M1 클립 (장전, 8발) | VARCO 3D 또는 Blender 직접 | 전 구간 |
| A | `SM_WP_M1Garand_ClipEmpty.fbx` | M1 빈 클립 (배출용) | Blender (Clip에서 탄 제거) | 전 구간 |
| A | `SM_WP_Bazooka.fbx` | 바주카 (M20 3.5인치 추천) | VARCO 3D + Blender 정리 | S5 |
| A | `SM_WP_BazookaRocket.fbx` | 바주카 로켓탄 | VARCO 3D | S5 |
| A | `SM_VH_T34_85.fbx` | T-34-85 | VARCO 3D + Blender 부품 분리 | S5 |
| A | `SM_PR_AmmoCrate.fbx` | 탄약 상자 (F 보충) | VARCO 3D | S1, S4 |
| A | `SM_PR_Target.fbx` | 튜토리얼 표적 | VARCO 3D | S1 |
| A | `SM_PR_BlanketBody.fbx` | 담요 덮인 형체 (만수) | VARCO 3D | S4 |
| A | `VFX_MuzzleFlash_M1.prefab` | M1 총구 화염 | Unity 파티클 | 전 구간 |
| A | `VFX_Bazooka_Backblast.prefab` | 바주카 발사 화염·후폭풍 | Unity 파티클 | S5 |
| A | `VFX_RocketTrail.prefab` | 로켓 궤적 | Unity 파티클 (Trail) | S5 |
| A | `VFX_Flare.prefab` | 조명탄 (낙하산) | Unity 파티클 + 점광원 | S3 |
| A | `VFX_Impact_Dirt.prefab` | 흙·모래주머니 탄착 | Unity 파티클 | 전 구간 |
| A | `VFX_Tank_Ricochet.prefab` | 전차 정면 도탄 불꽃 | Unity 파티클 | S5 |
| A | `VFX_Tank_HitExplosion.prefab` | 전차 측면 명중 폭발 | Unity 파티클 | S5 |
| A | `VFX_Tank_Smoke.prefab` | 전차 연기 (측면 1발) | Unity 파티클 (루프) | S5 |
| A | `VFX_Tank_Fire.prefab` | 전차 화재 (측면 2발·정지) | Unity 파티클 (루프) + 점광원 | S5~S6 |
| A | `FONT_UI_Sans.ttf` | UI 글꼴 (고딕) | 무료 글꼴 (예: Noto Sans KR, OFL) | 전 구간 |
| B | `SK_CH_ROK_ParkYongpal.fbx` | 박용팔 | VARCO 3D | S4 |
| B | `SM_WP_Mosin.fbx` | 적 소총 (모신나강) | VARCO 3D | S2~S3 |
| B | `SM_PR_SandbagWall.fbx` | 모래주머니 벽 (2m 모듈) | 기존 재사용 우선 → VARCO 3D | 전 구간 |
| B | `T_UI_LetterPaper.png` | 편지지 배경 (결과 화면) | VARCO 3D 이미지 노드 (없으면 수작업) | S7 |
| B | `FONT_Letter_Serif.ttf` | 편지 글꼴 (명조) | 무료 글꼴 (예: Noto Serif KR, OFL) | S7 |
| C | `SK_CH_KPA_Rifleman_B.fbx` | 북한군 보병 변형 (철모) | VARCO 3D | S3 |
| C | `SM_WP_PPSh41.fbx` | 적 기관단총 (PPSh-41) | VARCO 3D | S3 |
| C | `T_UI_TitleBG.png` | 시작 화면 배경 | VARCO 3D 이미지 노드 또는 게임 화면 캡처 | Title |

## 3. 항목별 요청


### 3-1. 캐릭터


#### 1. 국군 병사 (아군 공용) — `SK_CH_ROK_Soldier`  [A]

- **파일**: `SK_CH_ROK_Soldier.fbx`
- **위치**: `Assets/_Project/Art/Models/Characters/ROK_Soldier/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S2~S6
- **규격**: 키 1.70m · 1~2만 삼각형 · 텍스처 1K · 슬롯 M_CH_ROK_Body, M_CH_ROK_Gear · 피벗 발 중앙
- **그레이박스 임시본**: 회색 캡슐 1.70m (같은 이름 FBX)
- **참고**: 참호 아군, 새벽 보충병 실루엣에 같이 쓴다. 1인칭 팔(SK_CH_ROK_Arms)도 이 모델에서 잘라 만든다.

프롬프트:
```text
Korean War 1950 South Korean army infantry soldier, young man in his early twenties, M1 steel helmet with net, faded khaki cotton field uniform with rolled sleeves, canvas cartridge belt with pouches, canvas leggings, worn brown leather boots, dust and sweat stains, full body, A-pose with arms 45 degrees down, front view, standing straight, feet shoulder-width apart, empty open hands, no weapon, no loose hanging straps, symmetrical, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern equipment, camouflage pattern, body armor, zipper, text, logo, weapon in hands, cropped, multiple objects, blurry, cartoon
```

검수 — 공통 체크 M + C 전부, 그리고:
- [ ] 새벽 역광에서 실루엣이 '국군 병사'로 읽힌다 (철모 형태)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 2. 박용팔 — `SK_CH_ROK_ParkYongpal`  [B]

- **파일**: `SK_CH_ROK_ParkYongpal.fbx`
- **위치**: `Assets/_Project/Art/Models/Characters/ROK_ParkYongpal/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S4
- **규격**: 키 1.72m · 1~2만 삼각형 · 텍스처 1K · 슬롯 ROK_Soldier와 같게
- **그레이박스 임시본**: SK_CH_ROK_Soldier 복사본 (색만 다르게)
- **참고**: 일정이 밀리면 ROK_Soldier에 수건만 붙여 대체한다.

프롬프트:
```text
Korean War 1950 South Korean army corporal, man in his thirties, weathered tanned face with stubble, tired eyes, M1 steel helmet pushed back, small towel around the neck, faded khaki cotton field uniform, canvas cartridge belt, worn leather boots, full body, A-pose with arms 45 degrees down, front view, standing straight, feet shoulder-width apart, empty open hands, no weapon, no loose hanging straps, symmetrical, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern equipment, camouflage pattern, body armor, zipper, text, logo, weapon in hands, cropped, multiple objects, blurry, cartoon
```

검수 — 공통 체크 M + C 전부, 그리고:
- [ ] ROK_Soldier와 나란히 세웠을 때 다른 인물로 구분된다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 3. 북한군 보병 (적 공용) — `SK_CH_KPA_Rifleman`  [A]

- **파일**: `SK_CH_KPA_Rifleman.fbx`
- **위치**: `Assets/_Project/Art/Models/Characters/KPA_Rifleman/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S2~S3
- **규격**: 키 1.68m · 1~2만 삼각형 · 텍스처 1K · 슬롯 M_CH_KPA_Body, M_CH_KPA_Gear · 피벗 발 중앙
- **그레이박스 임시본**: 빨간 캡슐 1.68m (같은 이름 FBX)
- **참고**: 정찰병 2명·주 공세 7명 모두 이 모델. 고증(군모·장구)은 D가 자료로 확인.

프롬프트:
```text
Korean War 1950 North Korean People's Army infantry soldier, summer uniform, khaki-mustard cotton tunic with stand-up collar, soft side cap, canvas ammunition pouches on a leather belt, wrapped cloth puttees, canvas shoes, dusty, full body, A-pose with arms 45 degrees down, front view, standing straight, feet shoulder-width apart, empty open hands, no weapon, no loose hanging straps, symmetrical, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern equipment, camouflage pattern, body armor, zipper, text, logo, weapon in hands, cropped, multiple objects, blurry, cartoon, helmet with net
```

검수 — 공통 체크 M + C 전부, 그리고:
- [ ] 조명탄 아래에서 국군과 실루엣(군모 vs 철모)으로 구분된다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 4. 북한군 보병 변형 (철모) — `SK_CH_KPA_Rifleman_B`  [C]

- **파일**: `SK_CH_KPA_Rifleman_B.fbx`
- **위치**: `Assets/_Project/Art/Models/Characters/KPA_Rifleman/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S3
- **규격**: KPA_Rifleman과 같은 뼈대·슬롯 이름
- **그레이박스 임시본**: KPA_Rifleman 복사본
- **참고**: KPA_Rifleman 컨셉 이미지를 참고 이미지로 넣어 같은 사람처럼 만든다.

프롬프트:
```text
same North Korean People's Army 1950 infantry soldier as reference image, but wearing a Soviet SSh-40 steel helmet instead of the side cap, full body, A-pose with arms 45 degrees down, front view, standing straight, feet shoulder-width apart, empty open hands, no weapon, no loose hanging straps, symmetrical, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern equipment, camouflage pattern, body armor, zipper, text, logo, weapon in hands, cropped, multiple objects, blurry, cartoon
```

검수 — 공통 체크 M + C 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 5. 1인칭 팔 (오석구) — `SK_CH_ROK_Arms`  [A]

- **파일**: `SK_CH_ROK_Arms.fbx`
- **위치**: `Assets/_Project/Art/Models/Characters/ROK_Arms/`
- **도구**: 기존 재사용 우선 → 없으면 ROK_Soldier에서 잘라내기(Blender)  ·  **쓰는 구간**: 전 구간
- **규격**: 팔꿈치 위까지 · 5천~1만 삼각형 · 텍스처 2K · 손가락 뼈 필수 · 피벗 카메라 위치
- **그레이박스 임시본**: 기존 프로토타입 팔 그대로
- **참고**: AI로 1인칭 팔만 따로 뽑으면 무기 파지가 맞지 않는 경우가 많다. 기존 팔을 먼저 쓰고 손 위치 오류만 고친다(기획안 02).

검수 — 공통 체크 M + C 전부, 그리고:
- [ ] M1·바주카를 쥘 때 손가락이 총을 뚫지 않는다
- [ ] 소매가 ROK_Soldier와 같은 색·재질
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-2. 무기


#### 6. M1 개런드 — `SM_WP_M1Garand`  [A]

- **파일**: `SM_WP_M1Garand.fbx`
- **위치**: `Assets/_Project/Art/Models/Weapons/M1Garand/`
- **도구**: VARCO 3D + Blender 정리  ·  **쓰는 구간**: 전 구간
- **규격**: 길이 1.10m · 8천~1.5만 삼각형 · 텍스처 2K · 부품 Body / Bolt / ClipSlot(빈 오브젝트) · 슬롯 M_WP_M1_Wood, M_WP_M1_Metal · 피벗 손잡이 · 총구 +Z
- **그레이박스 임시본**: 1.10m 막대 + 손잡이 박스 (같은 부품 이름)
- **참고**: 가늠자·가늠쇠는 AI가 비틀기 쉽다. 생성 후 Blender에서 조준선 높이를 맞춘다. 기존 프로토타입 총이 있으면 비교해 나은 쪽을 쓴다.

프롬프트:
```text
M1 Garand semi-automatic rifle, exact side view, walnut wood stock and handguard, dark grey parkerized metal, rear aperture sight and front sight post clearly visible, no sling, accurate real proportions, length 1.1 meters, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
scope, optic, rail, magazine, sling, bayonet, modern rifle, plastic, text, cartoon
```

검수 — 공통 체크 M + W 전부, 그리고:
- [ ] Bolt 부품이 따로 분리돼 앞뒤로 움직일 수 있다
- [ ] 8발 클립이 ClipSlot에 정확히 들어간다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 7. M1 클립 (장전, 8발) — `SM_WP_M1Garand_Clip`  [A]

- **파일**: `SM_WP_M1Garand_Clip.fbx`
- **위치**: `Assets/_Project/Art/Models/Weapons/M1Garand/`
- **도구**: VARCO 3D 또는 Blender 직접  ·  **쓰는 구간**: 전 구간
- **규격**: 길이 약 6cm · 1천 삼각형 이하 · 슬롯 M_WP_M1_Clip, M_WP_M1_Brass · 피벗 클립 아래 중앙
- **그레이박스 임시본**: 작은 금색 박스

프롬프트:
```text
M1 Garand en-bloc clip loaded with eight .30-06 brass rifle cartridges stacked in two staggered rows, dark steel clip, close-up product view, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
magazine, belt, loose bullets, text
```

검수 — 공통 체크 M 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 8. M1 빈 클립 (배출용) — `SM_WP_M1Garand_ClipEmpty`  [A]

- **파일**: `SM_WP_M1Garand_ClipEmpty.fbx`
- **위치**: `Assets/_Project/Art/Models/Weapons/M1Garand/`
- **도구**: Blender (Clip에서 탄 제거)  ·  **쓰는 구간**: 전 구간
- **규격**: Clip과 같은 크기·피벗 · 500 삼각형 이하
- **그레이박스 임시본**: 작은 회색 박스
- **참고**: 장전 클립에서 탄만 지워 만든다. 배출될 때 날아가는 물체.

검수 — 공통 체크 M 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 9. 바주카 (M20 3.5인치 추천) — `SM_WP_Bazooka`  [A]

- **파일**: `SM_WP_Bazooka.fbx`
- **위치**: `Assets/_Project/Art/Models/Weapons/Bazooka/`
- **도구**: VARCO 3D + Blender 정리  ·  **쓰는 구간**: S5
- **규격**: 길이 1.52m · 8천~1.2만 삼각형 · 텍스처 2K · 부품 Body / RocketSlot(빈 오브젝트) · 슬롯 M_WP_Bazooka · 피벗 손잡이 · 발사 방향 +Z
- **그레이박스 임시본**: 1.52m 원통 + 손잡이 박스
- **참고**: ⬜ D 확정: 1950년 8월에는 2.36인치 M9A1과 3.5인치 M20이 함께 쓰였다. 전차를 멈추는 장면이라 M20을 추천.

프롬프트:
```text
M20 3.5 inch super bazooka anti-tank rocket launcher, exact side view, long olive drab aluminum tube, shoulder rest, pistol grip with trigger, front grip, simple folding sight, length 1.5 meters, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
RPG, modern launcher, scope, text, cartoon
```

검수 — 공통 체크 M + W 전부, 그리고:
- [ ] 어깨에 멘 1인칭 화면에서 조준기가 보인다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 10. 바주카 로켓탄 — `SM_WP_BazookaRocket`  [A]

- **파일**: `SM_WP_BazookaRocket.fbx`
- **위치**: `Assets/_Project/Art/Models/Weapons/Bazooka/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S5
- **규격**: 길이 약 0.6m · 1천 삼각형 이하 · 피벗 꼬리 중앙 · 앞 +Z
- **그레이박스 임시본**: 0.6m 원통

프롬프트:
```text
3.5 inch M28 bazooka rocket, olive drab cylindrical body with pointed nose and tail fins, side view, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
missile with wings, modern, text
```

검수 — 공통 체크 M 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 11. 적 소총 (모신나강) — `SM_WP_Mosin`  [B]

- **파일**: `SM_WP_Mosin.fbx`
- **위치**: `Assets/_Project/Art/Models/Weapons/Mosin/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S2~S3
- **규격**: 길이 1.23m · 3천~6천 삼각형 · 텍스처 1K · 피벗 손잡이
- **그레이박스 임시본**: 1.23m 막대
- **참고**: 적 손에 붙는 3인칭 무기라 예산을 낮게 잡는다.

프롬프트:
```text
Mosin-Nagant M1891/30 bolt-action rifle, exact side view, worn wooden stock, dark steel barrel and bolt handle, no bayonet, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
scope, sling, bayonet, modern, text
```

검수 — 공통 체크 M 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 12. 적 기관단총 (PPSh-41) — `SM_WP_PPSh41`  [C]

- **파일**: `SM_WP_PPSh41.fbx`
- **위치**: `Assets/_Project/Art/Models/Weapons/PPSh41/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S3
- **규격**: 길이 0.84m · 3천~6천 삼각형
- **그레이박스 임시본**: 0.84m 막대 + 원통

프롬프트:
```text
PPSh-41 submachine gun with drum magazine, exact side view, wooden stock, perforated barrel shroud, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern, text
```

검수 — 공통 체크 M 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-3. 전차


#### 13. T-34-85 — `SM_VH_T34_85`  [A]

- **파일**: `SM_VH_T34_85.fbx`
- **위치**: `Assets/_Project/Art/Models/Vehicles/T34_85/`
- **도구**: VARCO 3D + Blender 부품 분리  ·  **쓰는 구간**: S5
- **규격**: 길이 8.1m(포 포함) · 폭 3.0m · 높이 2.7m · 3~6만 삼각형 · 텍스처 2K · 부품 Hull / Turret(피벗=포탑 링 중앙) / Gun(피벗=포 회전축) / TrackL / TrackR · 슬롯 M_VH_T34_Body, M_VH_T34_Track · 피벗 바닥 중앙 · 포 방향 +Z
- **그레이박스 임시본**: 박스 3개(차체·포탑·포) 실측 크기, 같은 부품 이름
- **참고**: VARCO 결과는 한 덩어리로 나오는 경우가 많다. Blender에서 포탑·포·궤도를 떼어 이름을 붙인다. 연기·화재 위치와 측면 판정 콜라이더는 FBX가 아니라 프리팹(PF_VH_T34_85)에 둔다.

프롬프트:
```text
Soviet T-34-85 medium tank used by the North Korean army in 1950, three-quarter front view, dark olive green paint, worn and dusty, mud on the tracks and road wheels, sloped front armor, 85mm long gun, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern tank, reactive armor, camouflage net, star insignia text, cartoon
```

검수 — 공통 체크 M 전부, 그리고:
- [ ] Turret이 따로 돌고 Gun이 따로 위아래로 움직인다
- [ ] TrackL/TrackR가 별도 재질이라 텍스처 스크롤이 가능하다
- [ ] 정면 경사 장갑이 화면에서 '비스듬해서 튕긴다'로 읽힌다
- [ ] 측면 둔덕에서 본 옆면이 넓게 보인다 (측면 판정과 일치)
- [ ] 불탄 상태 재질(M_VH_T34_Burnt)로 바꿔도 깨지지 않는다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-4. 소품


#### 14. 탄약 상자 (F 보충) — `SM_PR_AmmoCrate`  [A]

- **파일**: `SM_PR_AmmoCrate.fbx`
- **위치**: `Assets/_Project/Art/Models/Props/AmmoCrate/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S1, S4
- **규격**: 0.70×0.40×0.30m · 1천~2천 삼각형 · 텍스처 512~1K · 피벗 바닥 중앙
- **그레이박스 임시본**: 0.7m 노란 박스
- **참고**: 어두운 구간에서 찾을 수 있어야 한다(기획안 02). 밝은 나무색 또는 흰 띠를 유지한다.

프롬프트:
```text
wooden military ammunition crate from the 1950s, rope handles, lid open showing cloth bandoliers of rifle clips inside, pale stencil paint marks without readable letters, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern plastic case, readable text, logo
```

검수 — 공통 체크 M 전부, 그리고:
- [ ] 야간 조명에서 10m 거리에서도 찾을 수 있다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 15. 튜토리얼 표적 — `SM_PR_Target`  [A]

- **파일**: `SM_PR_Target.fbx`
- **위치**: `Assets/_Project/Art/Models/Props/Target/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S1
- **규격**: 폭 0.5m · 높이 1.7m · 500~1천 삼각형 · 피벗 바닥 중앙
- **그레이박스 임시본**: 0.5×1.7m 판

프롬프트:
```text
old wooden training target board nailed to a stake, simple painted human torso silhouette, a few bullet holes, weathered planks, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
paper target with numbers, text, modern
```

검수 — 공통 체크 M 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 16. 담요 덮인 형체 (만수) — `SM_PR_BlanketBody`  [A]

- **파일**: `SM_PR_BlanketBody.fbx`
- **위치**: `Assets/_Project/Art/Models/Props/BlanketBody/`
- **도구**: VARCO 3D  ·  **쓰는 구간**: S4
- **규격**: 1.80×0.60×0.35m · 2천~4천 삼각형 · 슬롯 M_PR_Blanket, M_PR_Boot · 피벗 바닥 중앙
- **그레이박스 임시본**: 1.8m 낮은 박스 (담요색)
- **참고**: 만수의 부재는 '호출 + 담요 형체'로만 전달한다(기획안 02). 피·신체 노출 없이.

프롬프트:
```text
an olive drab army wool blanket draped over the outline of a person lying on the ground, soft heavy folds, only the shape visible, one worn leather boot showing at the end, dirt on the blanket, quiet and respectful, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
blood, gore, exposed body, face, hands, text
```

검수 — 공통 체크 M 전부, 그리고:
- [ ] 보자마자 '누가 덮여 있다'는 것을 안다
- [ ] 피·상처·신체 노출이 없다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 17. 모래주머니 벽 (2m 모듈) — `SM_PR_SandbagWall`  [B]

- **파일**: `SM_PR_SandbagWall.fbx`
- **위치**: `Assets/_Project/Art/Models/Props/Sandbag/`
- **도구**: 기존 재사용 우선 → VARCO 3D  ·  **쓰는 구간**: 전 구간
- **규격**: 2.0×0.6×1.0m · 2천~4천 삼각형 · 피벗 바닥 중앙 · 좌우 끝이 이어 붙도록
- **그레이박스 임시본**: 2m 갈색 박스

프롬프트:
```text
low wall of stacked burlap sandbags, three rows, sagging and dusty, straight module 2 meters long, realistic, Korean War 1950 summer, worn and dusty, muted earth tones, game-ready 3D asset, PBR materials, neutral even studio lighting, plain light grey background, single object centered, whole object visible, no text, no logo, no watermark
```
제외(네거티브):
```text
modern hesco barrier, text
```

검수 — 공통 체크 M 전부, 그리고:
- [ ] 두 개를 이어 붙였을 때 틈·겹침 깜빡임이 없다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-5. VFX


#### 18. M1 총구 화염 — `VFX_MuzzleFlash_M1`  [A]

- **파일**: `VFX_MuzzleFlash_M1.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클  ·  **쓰는 구간**: 전 구간
- **규격**: 0.05초 · 점광원 1개(0.05초) · 한 발에 1회
- **그레이박스 임시본**: 노란 구 0.05초

검수 — 공통 체크 V 전부, 그리고:
- [ ] 한 발에 정확히 1회 (기획안 02)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 19. 바주카 발사 화염·후폭풍 — `VFX_Bazooka_Backblast`  [A]

- **파일**: `VFX_Bazooka_Backblast.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클  ·  **쓰는 구간**: S5
- **규격**: 앞 화염 0.1초 + 뒤 연기 1.5초
- **그레이박스 임시본**: 흰 구

검수 — 공통 체크 V 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 20. 로켓 궤적 — `VFX_RocketTrail`  [A]

- **파일**: `VFX_RocketTrail.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클 (Trail)  ·  **쓰는 구간**: S5
- **규격**: 연기 꼬리 1초
- **그레이박스 임시본**: Trail Renderer 흰 선

검수 — 공통 체크 V 전부, 그리고:
- [ ] 로켓이 어디로 갔는지 눈으로 따라갈 수 있다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 21. 조명탄 (낙하산) — `VFX_Flare`  [A]

- **파일**: `VFX_Flare.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클 + 점광원  ·  **쓰는 구간**: S3
- **규격**: 밝은 점 + 연기 꼬리 + 점광원(그림자) · 천천히 내려오며 흔들림 · 약 15~20초
- **그레이박스 임시본**: 흰 구 + 점광원

검수 — 공통 체크 V 전부, 그리고:
- [ ] 조명탄 아래 적 윤곽과 엄폐물이 분리돼 보인다 (기획안 02)
- [ ] 꺼질 때 '이제 어두워진다'가 느껴진다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 22. 흙·모래주머니 탄착 — `VFX_Impact_Dirt`  [A]

- **파일**: `VFX_Impact_Dirt.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클  ·  **쓰는 구간**: 전 구간
- **규격**: 흙 튐 0.5초
- **그레이박스 임시본**: 갈색 점 몇 개

검수 — 공통 체크 V 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 23. 전차 정면 도탄 불꽃 — `VFX_Tank_Ricochet`  [A]

- **파일**: `VFX_Tank_Ricochet.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클  ·  **쓰는 구간**: S5
- **규격**: 불꽃 튐 0.3초, 폭발 없음
- **그레이박스 임시본**: 노란 점

검수 — 공통 체크 V 전부, 그리고:
- [ ] 측면 명중(연기·폭발)과 한눈에 다르다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 24. 전차 측면 명중 폭발 — `VFX_Tank_HitExplosion`  [A]

- **파일**: `VFX_Tank_HitExplosion.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클  ·  **쓰는 구간**: S5
- **규격**: 섬광 + 파편 1초
- **그레이박스 임시본**: 주황 구

검수 — 공통 체크 V 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 25. 전차 연기 (측면 1발) — `VFX_Tank_Smoke`  [A]

- **파일**: `VFX_Tank_Smoke.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클 (루프)  ·  **쓰는 구간**: S5
- **규격**: 검회색 연기 기둥, 루프
- **그레이박스 임시본**: 회색 구 반복

검수 — 공통 체크 V 전부, 그리고:
- [ ] '한 발 더 맞히면 된다'가 읽힌다 (화재와 구분)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 26. 전차 화재 (측면 2발·정지) — `VFX_Tank_Fire`  [A]

- **파일**: `VFX_Tank_Fire.prefab`
- **위치**: `Assets/_Project/Art/VFX/`
- **도구**: Unity 파티클 (루프) + 점광원  ·  **쓰는 구간**: S5~S6
- **규격**: 불꽃 + 짙은 연기 + 깜빡이는 점광원, 루프
- **그레이박스 임시본**: 주황 구 반복

검수 — 공통 체크 V 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

### 3-6. UI


#### 27. 편지지 배경 (결과 화면) — `T_UI_LetterPaper`  [B]

- **파일**: `T_UI_LetterPaper.png`
- **위치**: `Assets/_Project/Art/UI/`
- **도구**: VARCO 3D 이미지 노드 (없으면 수작업)  ·  **쓰는 구간**: S7
- **규격**: 1920×1080 PNG · 가운데 글이 올라갈 빈 공간
- **그레이박스 임시본**: 누런 단색 사각형

프롬프트:
```text
old Korean letter paper from 1950, yellowed thin hanji paper with faint vertical red ruled lines, creases from being folded in four, soft water stains and worn edges, flat top view, evenly lit, completely blank with no writing, 16:9
```
제외(네거티브):
```text
text, handwriting, calligraphy, signature, stamp, modern paper
```

검수 — 공통 체크 U 전부, 그리고:
- [ ] 편지 글(8초)이 배경 무늬에 묻히지 않는다
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 28. 시작 화면 배경 — `T_UI_TitleBG`  [C]

- **파일**: `T_UI_TitleBG.png`
- **위치**: `Assets/_Project/Art/UI/`
- **도구**: VARCO 3D 이미지 노드 또는 게임 화면 캡처  ·  **쓰는 구간**: Title
- **규격**: 1920×1080 PNG · 왼쪽 1/3 어둡게(메뉴 자리)
- **그레이박스 임시본**: 어두운 남색 단색
- **참고**: 게임 안 새벽 장면을 캡처해 쓰는 쪽이 더 잘 맞을 수 있다.

프롬프트:
```text
dawn mist over low Korean hills after a night battle, 1950, silhouettes of a trench line and a few soldiers far away, muted desaturated blue and amber, quiet and somber, cinematic wide shot, no text, 16:9
```
제외(네거티브):
```text
text, logo, modern buildings, bright colors
```

검수 — 공통 체크 U 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 29. UI 글꼴 (고딕) — `FONT_UI_Sans`  [A]

- **파일**: `FONT_UI_Sans.ttf`
- **위치**: `Assets/_Project/Art/Fonts/`
- **도구**: 무료 글꼴 (예: Noto Sans KR, OFL)  ·  **쓰는 구간**: 전 구간
- **규격**: Regular·Bold 2종
- **그레이박스 임시본**: 기본 글꼴

검수 — 공통 체크 F 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:

#### 30. 편지 글꼴 (명조) — `FONT_Letter_Serif`  [B]

- **파일**: `FONT_Letter_Serif.ttf`
- **위치**: `Assets/_Project/Art/Fonts/`
- **도구**: 무료 글꼴 (예: Noto Serif KR, OFL)  ·  **쓰는 구간**: S7
- **규격**: Regular 1종
- **그레이박스 임시본**: 기본 글꼴
- **참고**: 윈도우 시스템 글꼴(맑은 고딕 등)은 배포에 쓰지 않는다.

검수 — 공통 체크 F 전부, 그리고:
- (개별 항목 없음)
- 결과: ⬜ 합격 / ⬜ 수정 요청 — 메모:


### 구분 체크 (헷갈리기 쉬운 짝)
| 짝 | 구분 포인트 | 확인 방법 |
|---|---|---|
| 국군 ↔ 북한군 | 철모(그물) ↔ 부드러운 군모, 군복 색 | 조명탄 아래 20m에서 실루엣만 보고 구분 |
| 국군 병사 ↔ 박용팔 | 목의 수건 · 뒤로 젖힌 철모 · 나이 | 나란히 세워 캡처 |
| 전차 정면 도탄 ↔ 측면 1발 ↔ 측면 2발 | 불꽃 튐 ↔ 연기 ↔ 정지 + 화재 | 세 상태를 같은 각도에서 캡처해 비교 |
| 장전 클립 ↔ 빈 클립 | 황동 탄 유무 | 1인칭 화면에서 확인 |
| 탄약 상자 ↔ 주변 상자 · 흙 | 밝은 나무색 · 열린 뚜껑 | 어두운 구간 10m |


## 4. 게임이 모델에 덧입히는 것 (모델에 넣지 않는다)
- 총구 화염 · 연기 · 화재 · 도탄 불꽃 → `VFX_` 프리팹 (3-5)
- 전차 손상 상태 → 재질 교체(`M_VH_T34_Burnt`) + VFX. 모델은 하나
- 전차 궤도 움직임 → 텍스처 스크롤(코드), 포탑 회전 → 코드
- 측면 판정 범위 → 프리팹 콜라이더 (`HitZone_SideL`, `HitZone_SideR`, `HitZone_Front`)
- 소켓 → 프리팹의 빈 오브젝트: `Socket_Muzzle`, `Socket_ClipEject`, `Socket_Smoke`, `Socket_Fire`, `Socket_RocketSpawn`

## 5. 받은 뒤 내가(C) 하는 일
1. `_Source/<파일명>/`에 원본 · 프롬프트 보관
2. Blender에서 축 · 크기 · 피벗 · 부품 분리 · 이름 정리 → 규칙대로 FBX 내보내기
3. 그레이박스 파일 **덮어쓰기** (같은 폴더 · 같은 이름)
4. Unity에서 재질을 엔진 표준 재질로 연결, 분홍 없는지 확인
5. 아래 공통 체크 + 항목별 체크를 검수판에서 체크
6. 야간 씬에 놓고 실루엣 확인 캡처 → 검수판 메모에 첨부 경로 기록
7. 합격 → TASKS.md 해당 항목에 `에셋 교체 (날짜): 파일명` 기록, 커밋 `MM.DD/Art/파일명Swap`

## 6. 만들지 않는 것

- 점수·킬 카운트 표시 — 결말의 정서와 충돌 (기획안 02)
- 히트마커 그림·소리 — 같은 이유
- 처치 보상음·성공 효과음 — 같은 이유. UI 소리도 보상처럼 들리지 않게
- 정적 구간 음악 — 부재가 대사로만 전달되도록
- 새 무기·새 맵·혹한·분대 명령 관련 에셋 — 범위 제외 (GDD 9번)

## 7. 공통 체크리스트

### 공통 체크 M — 모델 공통

- [ ] M1. 파일명·폴더가 표와 정확히 같다 (대소문자 포함)
- [ ] M2. VARCO 원본 다운로드를 _Source 폴더에 보관했다
- [ ] M3. 크기가 실측 기준 ±5% 이내 (Unity에서 1m 큐브 옆에 두고 확인)
- [ ] M4. 피벗이 규칙 위치에 있다 (캐릭터·소품=바닥 중앙, 무기=손잡이)
- [ ] M5. 회전 0으로 놓았을 때 앞이 +Z, 위가 +Y
- [ ] M6. Transform 적용됨 (스케일 1, 회전 0)
- [ ] M7. 삼각형 수가 예산 이내 (Unity Stats)
- [ ] M8. 메시 찢어짐·구멍·떠다니는 조각 없음
- [ ] M9. 녹아내린 듯 울퉁불퉁한 표면 없음
- [ ] M10. 컨셉 이미지와 형태·색이 같다
- [ ] M11. 분홍(깨진 재질) 없음, 텍스처가 모두 연결됨
- [ ] M12. 재질 슬롯 이름이 규칙대로
- [ ] M13. 글자·로고·워터마크 없음
- [ ] M14. 야간 씬 조명에서 실루엣만으로 무엇인지 알 수 있다
- [ ] M15. 그레이박스 파일을 덮어쓴 뒤 프리팹 참조 유지 (Missing 0)
- [ ] M16. 콘솔 오류·경고 0

### 공통 체크 C — 캐릭터 추가

- [ ] C1. A-포즈, 손에 아무것도 없다
- [ ] C2. Rig → Humanoid, Configure Avatar 매핑이 모두 초록색
- [ ] C3. 손가락 뼈가 있다 (총 쥐는 동작용)
- [ ] C4. 얼굴이 뭉개지지 않고 손가락이 붙어 있지 않다
- [ ] C5. 현대 장비(방탄조끼·무늬 군복·지퍼·운동화) 없음 — 고증 D 확인
- [ ] C6. 테스트 애니메이션(걷기) 적용 시 옷이 몸을 뚫지 않는다

### 공통 체크 W — 1인칭 무기 추가

- [ ] W1. 1인칭 근거리에서 텍스처가 뭉개지지 않는다 (2K)
- [ ] W2. 분리 부품 이름이 규칙대로, 피벗이 회전·이동 축
- [ ] W3. 실물 비율과 같다 (총열·개머리판·손잡이 길이)

### 공통 체크 V — VFX 공통

- [ ] V1. 프리팹 이름이 표와 같다
- [ ] V2. HDR 발광 + 블룸에서 과하게 하얗게 날아가지 않는다
- [ ] V3. 해당 구간 최저 fps가 기준선 대비 10% 이상 떨어지지 않는다
- [ ] V4. 재생이 끝나면 스스로 사라진다 (Stop Action: Destroy/Disable)
- [ ] V5. 회차 초기화·체크포인트 복구 시 남아 있지 않다

### 공통 체크 U — UI 이미지 공통

- [ ] U1. 파일명·폴더가 표와 같다
- [ ] U2. 1920×1080에서 늘어나거나 흐려지지 않는다
- [ ] U3. 글자·서명·워터마크가 들어가 있지 않다 (글자는 게임이 올린다)
- [ ] U4. 그 위에 올린 자막·편지 글이 잘 읽힌다 (명암 대비)

### 공통 체크 F — 글꼴 공통

- [ ] F1. 라이선스가 배포 가능(OFL 등)이고 라이선스 파일을 함께 넣었다
- [ ] F2. 게임에 쓰는 모든 한글·기호가 나온다 (빈 네모 없음)
- [ ] F3. 1920×1080 기준 28~32px에서 읽힌다