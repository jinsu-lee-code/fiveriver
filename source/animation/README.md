# animation 원본 자료 (다섯개의강) — M1 장전 애니메이션
게임에 들어간 결과는 `five_river/Assets/_Project/Art/Resources/M1_ReloadMotion.asset`(Unity 메뉴 FiveRivers/Animation/Import M1 Reload Motion (Blender)로 가져옴)입니다.

- `scripts/build_scene.py`: Unity 1인칭 화면(export/)을 Blender 5.2에 재구성 → `reload_scene.blend`
- `scripts/anim.py` + `scripts/params.json`: 손목 경로·손가락 IK로 부분 장전 제작 → `reload_anim.blend`, 프레임 데이터 `reload_partial_baked.json`
  - 참고 영상: youtube.com/shorts/8jNDx8IP1fE (8.2~11.4초, 3.2초 = 게임 부분 장전 시간). 남은 클립 잡기 28%, 뽑아서 화면 밖 28~47%, 새 클립 50~58%, 눌러 넣기 58~66%, 손 빠짐 66~82%, 쥔 자세 복귀 88~97%
  - 실행: `blender -b --python scripts/anim.py -- both` (렌더+내보내기), `-- export` (내보내기만)
- `reload_partial_baked.json.b8`, `.b11`: 이전 버전
- `export/`, `tex/`: 재구성에 쓰는 메쉬·텍스처, `render/`: 확인용 렌더 모음
- 경로는 원래 PC 기준 `C:/sk_anim/...`, `C:/sk/five_river/...`로 적혀 있음 → 다른 위치면 스크립트 안 경로를 바꿔서 실행

## tank_entrance — 전차 등장 연출 Blender판 (2026-11-11)
게임에 들어간 결과는 `five_river/Assets/_Project/Art/Resources/Cinematics/TankEntrance_Blender.json`(631프레임, 30fps, 21.0초)이고 `TankEntranceBakedTake.cs`가 읽습니다. 보는 법: 플레이 → F1 → "전차 등장 연출" → "Blender판 보기".

- `level.json`: Unity에서 내보낸 지형 높이(1m 격자) · LevelPoints · 모래주머니/나무/바위 범위
- `build_take.py`: `level.json`으로 `TankEntrance.blend` 생성(지형 · 전차 대역, 카메라 5대 + Track-To 대상, 컷 마커, `cue:*` 마커)
- `bake_take.py`: 프레임마다 구워 게임 JSON으로 저장
  - 실행 (이 폴더에서, Blender 5.2): `blender -b --python build_take.py` → `blender -b TankEntrance.blend --python bake_take.py`
- 좌표: Unity (x, y, z) = Blender (x, z, y). 전차 위치 키는 선형이어야 함(가감속이면 카메라 겨냥보다 뒤처짐)
