# 바주카 1인칭 팔 (Tripo 리깅 국군 이등병) — 2026-11-23

사용자 지시: "바주카 손은 트리포ai에 만들어진거 사용한뒤에 퀄리티 AA급 이상으로", "트리포 ai에서 리깅하고 블랜더에서 맞춘뒤에 유니티를 넣는 방법" → "전신 리깅으로", "바주카 잡을때 소매랑 팔뚝이 안어울려서 조화롭게".

바주카를 들 때의 1인칭 팔(`VM_Bazooka/.../Arms/SK_CH_ROK_Arm_Left|Right`)을 Tripo AI의 국군 이등병 모델(소매를 팔꿈치까지 걷은 차림)의 팔로 통째로 바꾼다. 소매 · 팔뚝 · 손이 한 모델에서 나와 이음새가 없다.

## 만드는 순서
1. **Tripo** (studio.tripo3d.ai → Assets → "korean infantry private 3d model") → Export: FBX, **Mixamo** 형식, 4K, Export Skeleton 켬, 애니메이션 0개. 압축을 `raw/`에 푼다. (뼈 이름이 게임 1인칭 팔과 같은 `mixamorig:`로 나온다.)
2. **Blender** (5.2, 화면 없이):
   ```
   blender -b --python retarget_arms.py -- raw/<tripo>.fbx bazooka_arms_rig.json work
   ```
   - 몸 전체를 한 단계 세분화(Catmull-Clark)한 뒤, 팔 뼈(어깨~손가락 3마디)에 붙은 부분만 남긴다.
   - 각 뼈를 게임 1인칭 뼈대의 같은 이름 뼈 기준 자세로 옮긴다(머리 위치 · 방향 · 손바닥 기준 회전). 굵기는 양손 손가락 마디 간격 평균 배율 하나로, 소매 0.84 · 팔뚝 0.9배.
   - Blender(오른손 좌표) → Unity(왼손 좌표)라 X를 뒤집는다.
   - 결과: `work/arms_unity.json`, 텍스처, 미리보기 `work/arms_*.png`.
   - `bazooka_arms_rig.json`은 게임 1인칭 팔의 뼈대 기준 자세와 메시(Unity 메시 공간)를 Unity에서 뽑은 것이다(임시 도구 `ClaudeHandProbe.Dump`).
3. **Unity**: 메뉴 `FiveRivers/Setup/Bazooka Tripo Arms` (편집 모드, 여러 번 실행해도 결과 같음)
   - 팔 메시 교체 + URP 재질(색 · 노멀 · 금속/거칠기→금속/매끈함).
   - `FirstPersonFingerCurl`: 바주카 애니메이션이 손가락을 편 채라, 애니메이션 위에서 손가락을 손바닥 쪽으로, 엄지는 손바닥을 가로질러 감는다.
   - `BazookaPresentation`의 손목 목표 보정(`leftGripShift` · `rightGripShift`): 플레이 중 손잡이 기둥 축과 감은 손가락 고리 중심의 차이를 재서 정한 값.

`raw/`와 `work/`는 git에 올리지 않는다(.gitignore). 다시 만들 때는 1번부터.

## 확인
- 처음(타이틀)부터 새벽까지 실제 입력으로 플레이: 조작 · 전차(바주카 측면 2발) · 정찰/포격/주 공세 · 서쪽 공세/정적 · 고지/새벽.
- 손 근접: 임시 도구 `ClaudeHandProbe.CloseUps`(1인칭 시나리오 `cine fp` 안에서 허리 · 조준 · 재장전 순간).

## 손 피부 디테일 (2026-11-23)
- `bake_hand_detail.py`: Tripo 고해상도 손 스캔(`raw/tripo_hand_scan_4k.glb`, Tripo 에셋 "human hand 3d model" GLB 4K)을 병사 양손에 마디별로 맞춰 겹친 뒤, 병사 손 UV 텍셀마다 가장 가까운 스캔 표면을 찾아 넣는다.
  - 노멀: 스캔과 크게 뭉갠 스캔의 차이(주름·힘줄 같은 고주파)만 병사 손 법선에 더함 (`DETAIL` 0.6)
  - 색: 스캔 명암을 ±10%만 곱함 (짙게 하면 칼자국처럼 보였음)
  - 결과: `work/T_soldier_BaseColor_detail.png`, `work/T_soldier_Normal_detail.png`(4096), 피부 마스크 `work/T_soldier_SkinMask.png`, 반복 피부결 `work/T_skin_micro_normal.png`(512)
- 실행: `blender -b --python bake_hand_detail.py -- raw/<tripo>.fbx raw/tripo_hand_scan_4k.glb work` (약 50초), 그다음 Unity 메뉴 `FiveRivers/Setup/Bazooka Tripo Arms`
- Unity 메뉴는 `_detail` 판이 있으면 그것을 쓰고, 피부 마스크·피부결을 URP Lit 디테일 맵으로 연결한다 (반복 28, 세기 0.7 — `BazookaArmSetup.SkinMicroTiling/Strength`)
