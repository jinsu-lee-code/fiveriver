# SYSTEM_TODO_20261110.md — 시스템(main) 남은 일 (2026-11-10)

> **브랜치 `main`** · 작성 2026-11-10 · 기준: 개인 저장소 최신본(49fd84d) 반영 직후 (`114af64`)
> 파트 요청서(`ART_REQUEST_20261110.md` · `ANIM_REQUEST_20261110.md` · `SOUND_REQUEST_20261110.md`)와 짝을 이룬다. 파트가 납품하면 **연결은 main에서** 한다 (CLAUDE.md "브랜치 운영").
> 커밋: `MM.DD/항목번호/PascalCase요약` (예: `11.11/M3/VoiceLinesHookup`), 수정은 `…Fix`.
> 완료 = 테스트 통과 + 확인 방법 안내 + **실행 빌드에서 사람이 확인**.

## 0. 현재 상태
| 구분 | 상태 |
|---|---|
| Unity | **6000.6.8f1** (11-10 6000.3.25에서 올림). 처음 열 때 전체 재임포트 |
| 테스트 | EditMode 133/133 (개인 저장소, 11-10 같은 파일 기준). 팀 저장소에서 6000.6.8로는 아직 안 돌림 |
| 자동 순환 점검 | 마지막 PASS 11-08. 11-09 이후 변경(전차 한 발 사망 · 박용팔 견착 · 셔츠 수정) 뒤에는 안 돌림 |
| 실행 빌드 | 마지막 빌드 11-09 14:48 (itch.io 업로드본) — **전차 한 발 사망(15:41) · 박용팔 수정 이전** |
| 문서 | `FLOW.md` · `TASKS.md`가 11-08 이후 그대로 — **진행 순서 변경(전차 → 고지 돌격 → 정상 새벽 → 편지)이 없음** |

## 1. 목록
| 우선 | 번호 | 할 일 | 기다리는 것 |
|---|---|---|---|
| A | M1 | 팀 저장소 6000.6.8로 열기 → EditMode 테스트 · 콘솔 오류 0 확인 | — |
| A | M2 | 개발 빌드 → `FiveRivers.exe -fiverivers-smoke` 3회 자동 완주 → 사람 3회 완주 (TASKS 관문 1-7) | M1 |
| A | M3 | 대사 음성 연결: `GameText.asset` 14줄 `voice` 칸 + `SoundLibrary` + 덕킹 확인 | SOUND V1 · V2 |
| A | M4 | `FLOW.md` · `TASKS.md`를 현재 진행 순서와 11-06~11-10 작업으로 갱신 | — |
| A | M5 | 에디터 `runInBackground` 정하기 | 결정 |
| B | M6 | 일반 아군 3명 메시 교체 (`ROK_Regular_NoCap_SkinFix`) | ART R1 |
| B | M7 | `AC_ROK` 달리기 상태 → `A_ROK_Run_PortArms` | ANIM N1 |
| B | M8 | 새벽 트럭 프리팹 다시 만들기 (`FiveRivers/Setup/Build Dawn Truck Prefab`) · 하차 확인 | ART R2 |
| B | M9 | 시작 직후 소리 NullReference 원인 (10-28 미확인) | — |
| B | M10 | 빌드 경고 485건 훑어보기 (1-7 기록, 내용 미확인) | M2 |
| B | M11 | 릴리스 빌드 다시 만들고 itch.io 갱신 | M2 · 사람 확인 |
| C | M12 | 검수 결과(ART 30 · ANIM 27 · SOUND 51칸) 중 수정 요청을 TASKS에 항목으로 옮기기 | 각 파트 |

## 2. 항목별 메모

### M3. 대사 음성 연결
- 지금은 14줄 모두 `voice`가 비어 자막만 나온다. 쓰이는 곳: `NarrativeDirector`(박용팔 호출 · 만수 대사), 전차 · 조명탄 · 정찰 · 후퇴 안내, `allySupport`(20곳).
- 파일이 오면 `Setup/Audio Graybox + Sound Library`로 SoundLibrary를 채우고, GameText `voice`에 ID를 넣는다. 자막 시간 안에 끝나는지, 대사 중 효과음이 `AudioConfig.duckDb`만큼 줄어드는지 헤드폰으로 확인.
- 새 이름 5개(`VO_SQD_Tank_03`, `VO_PARK_MansuLost_01`, `VO_PARK_West_01`, `VO_MANSU_Letter_01`, `VO_MANSU_Morning_01`)는 SOUND_LIST · 데이터 생성 코드(`DataAssetsSetup`)에도 추가.

### M4. 문서 갱신
- FLOW.md: 구간 표 · 체크포인트 · 시간 상한을 현재 순서(전차 → 고지 돌격(박용팔 동행, 맨 위 바위 도착 판정) → 정상 새벽(윤태석) → 편지)로.
- TASKS.md: 11-06~11-10 항목 추가 (맵 수리 · 메가스캔 · 장전 Blender · 진행 순서 변경 · 아군 참호 넘기 · 전차 한 발 사망 · 박용팔 견착 · 셔츠 수정 · 팀 저장소 동기화). 상태는 "플레이 확인 대기"로.
- BALANCE.md: 전차 포탄 150(11-09)은 반영돼 있음.

### M5. runInBackground
- `Scripts/Editor/BuildRunner.cs:29`가 빌드할 때 `PlayerSettings.runInBackground = false`로 바꾸고 `ProjectSettings.asset`에 저장된다. 그 뒤로 **에디터에서 Play 중 Unity 창이 뒤로 가면 게임이 멈춘다.**
- 선택지: (1) 빌드 후 원래 값으로 되돌리게 고침 (추천 — 시연 빌드는 그대로, 에디터 작업은 안 멈춤) / (2) 지금처럼 둠.

### 확인 방법 (M1 · M2 공통)
- `Dabudong.unity` Play → F1 디버그 창에서 구간 이동 → 전차 포탄에 **한 발 맞으면 플레이어 사망**(측면 2발 정지 · 화재는 그대로), 고지 돌격에서 박용팔이 어깨에 총을 붙이고 셔츠에 삼각형 무늬 없음, 맨 위 바위에서 정상 판정 → 새벽 → 편지 → 처음 화면.
