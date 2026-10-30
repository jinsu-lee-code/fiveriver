# 사운드 적용 · 재생 확인 (2026-10-28)

**Unity 6000.3.25f1 / URP 17.3.0 · sound 브랜치**

| 구분 | 결과 |
|---|---|
| 게임용 음원 | 40개 / 29개 SoundLibrary ID 연결 |
| 보조·대안 | 6개 + 3개, 게임 자동 재생에서 제외 |
| 전체 파일 | 49개 원본 SHA 동일, Unity 전체 PCM 해독 성공 |
| 개별 재생 | 49/49, 샘플 진행·DSP·Source/Listener 출력 확인 |
| 게임 오디오 API | 40/40, 실제 AudioDirector와 원래 볼륨 버스 출력 |
| 게임 이벤트 표본 | 28개 클립 출력, M1 8발/핑/장전 및 바주카 발사·재장전 |
| 기존 테스트 | SoundAndTextTests 4/4 통과 |

## 실제 반영한 내용

- 요청서의 이름과 기존 `Assets/_Project/Audio/` 경로로 복사했습니다. Downloads 원본 49개는 변경하지 않았습니다.
- 기존 .meta 402개와 GUID를 보존했습니다. 추가된 음원·폴더·매핑 JSON의 .meta만 새로 생성했습니다.
- 같은 이름의 기존 임시 파일을 교체했고, 교체 대상 ID에서는 미제공 임시 변형이 섞이지 않도록 실제 제공된 클립만 선택했습니다. 파일을 복제해 새 변형인 것처럼 표시하지 않았습니다.
- `ApprovedClipMap.json`을 재생성 메뉴가 존중하도록 연결해, 메뉴 재실행 후에도 승인된 40개 연결이 그대로 유지됩니다. 나머지 22개 라이브러리 ID는 기존 음원을 유지합니다.
- 전차 접근은 이동음, 기존 Holding 상태는 공회전, 파괴 후 엔진 정지·화재로 연결했습니다. 시작/일시정지 메뉴 전환에 기존 UI_Menu_Click을 연결했습니다.
- 사용자의 게임 연결 요청에 따라 AudioDirector·TankController·AudioGraybox의 최소 연결 코드를 함께 수정했습니다. 씬·프리팹·엔진 버전은 바꾸지 않았습니다.
- 보조는 `Audio/Supplemental`, 대안은 `Audio/Alternatives`입니다. 기본 버전과 충돌하지 않는 폴더에 두고 개별 재생만 검사했습니다. 기존 루트 FiveRivers_Sound_Master 폴더는 계속 Git 제외 상태입니다.

## 검증 범위와 남은 확인

- [x] 49개 동일 원본·대상 해시, 48kHz/16bit, Unity 데이터 해독 및 연결 검사.
- [x] 49개 모두 개별 출력 확인. 시작 구간과 긴 클립의 끝부분 seek를 사용한 샘플 검사이며 전곡 청취가 아닙니다.
- [x] 게임용 40개 모두 게임 AudioDirector 경로 출력. 임시 라이브러리 복제본의 단일 클립 선택으로 검사하고 원본은 복구했습니다. 자연 이벤트 40개 전부를 발생시켰다는 의미가 아닙니다.
- [x] 실제 Input System 검수 장치로 M1 8발·핑 1·장전 1, 바주카 발사 1·장전 1을 확인했습니다. 구간 이동은 기존 DebugJump, 피격·로켓 명중은 실제 공개 처리 API를 함께 사용했습니다.
- [x] 전차 이동·Holding 공회전·정지·화재, 메뉴 전환·환경음·결말 음악의 실제 게임 경로 출력 표본을 확인했습니다.
- [x] 검수 장치·라이브러리·입력 설정·백그라운드 실행 옵션을 복구했고 장면은 저장하지 않았습니다.
- [ ] 전체 Windows 실행 빌드의 사운드 검증.
- [ ] 실제 헤드폰으로 최종 음색·밸런스·장전 동작과 소리 타이밍을 확인. M1_Reload 원본은 ClipInsert 이벤트(장전 완료)에 연결됩니다.
- [ ] 긴 루프 10회 반복과 이음새 청취.
- [ ] 초기 NullReference 오류 원인 확인. 초기 전환 이력에 약 2천 건의 예외가 남아 있습니다. 원인은 미확정이며, 이후 성공 검사 구간의 추가 오류 0/현재 콘솔 오류 0·AI Account API 경고 1과 구분합니다. 콘솔을 수동 삭제해 합격 처리하지 않았습니다.

## 49개 대응표

| 원본 이름 | 프로젝트 이름/위치 | 용도 |
|---|---|---|
| `AMB_Battle_Distant_Loop.wav` | `AMB/AMB_Battle_Distant_Loop.wav` | 게임 연결 |
| `AMB_Dawn_Loop.wav` | `AMB/AMB_Dawn_Loop.wav` | 게임 연결 |
| `AMB_Dawn_Wind.wav` | `Supplemental/AMB/AMB_Dawn_Wind_01.wav` | 보조(재생 검사) |
| `AMB_Night.wav` | `Supplemental/AMB/AMB_Night_01.wav` | 보조(재생 검사) |
| `AMB_Night_Hills_Loop.wav` | `AMB/AMB_Night_Hills_Loop.wav` | 게임 연결 |
| `AMB_Silence_Loop.wav` | `AMB/AMB_Silence_Loop.wav` | 게임 연결 |
| `BGM_Ending_R16.wav` | `BGM/BGM_Ending.wav` | 게임 연결 |
| `FOLEY_Footsteps_Dirt.wav` | `SFX/Foley/SFX_Foot_Dirt_01.wav` | 게임 연결 |
| `SFX_Ammo_Pickup.wav` | `SFX/Foley/SFX_Ammo_Pickup.wav` | 게임 연결 |
| `SFX_Bazooka_Fire.wav` | `SFX/Weapon/SFX_Bazooka_Fire.wav` | 게임 연결 |
| `SFX_Bazooka_Fire_R13.wav` | `Alternatives/R13/SFX_Bazooka_Fire.wav` | 대안(재생 검사) |
| `SFX_Bazooka_Load.wav` | `SFX/Weapon/SFX_Bazooka_Load.wav` | 게임 연결 |
| `SFX_Bullet_Pass.wav` | `SFX/Weapon/SFX_Bullet_Whiz_01.wav` | 게임 연결 |
| `SFX_Bullet_Whiz_02.wav` | `SFX/Weapon/SFX_Bullet_Whiz_02.wav` | 게임 연결 |
| `SFX_Bullet_Whiz_03.wav` | `SFX/Weapon/SFX_Bullet_Whiz_03.wav` | 게임 연결 |
| `SFX_Bullet_Whiz_04.wav` | `SFX/Weapon/SFX_Bullet_Whiz_04.wav` | 게임 연결 |
| `SFX_Click.wav` | `UI/UI_Menu_Click.wav` | 게임 연결 |
| `SFX_Enemy_RifleFire_01.wav` | `SFX/Weapon/SFX_Enemy_RifleFire_01.wav` | 게임 연결 |
| `SFX_Enemy_RifleFire_02.wav` | `SFX/Weapon/SFX_Enemy_RifleFire_02.wav` | 게임 연결 |
| `SFX_Enemy_RifleFire_03.wav` | `SFX/Weapon/SFX_Enemy_RifleFire_03.wav` | 게임 연결 |
| `SFX_Explosion_Field.wav` | `SFX/Env/SFX_Artillery_Distant_01.wav` | 게임 연결 |
| `SFX_Flare_Burn_Loop.wav` | `SFX/Env/SFX_Flare_Burn_Loop.wav` | 게임 연결 |
| `SFX_Flare_Ignition.wav` | `Supplemental/SFX/SFX_Flare_Ignition_01.wav` | 보조(재생 검사) |
| `SFX_Flare_Launch.wav` | `SFX/Env/SFX_Flare_Launch.wav` | 게임 연결 |
| `SFX_Flare_Launch_R13.wav` | `Alternatives/R13/SFX_Flare_Launch.wav` | 대안(재생 검사) |
| `SFX_Foot_Dirt_03.wav` | `SFX/Foley/SFX_Foot_Dirt_03.wav` | 게임 연결 |
| `SFX_Foot_Dirt_R16.wav` | `SFX/Foley/SFX_Foot_Dirt_04.wav` | 게임 연결 |
| `SFX_Impact_Dirt_01.wav` | `SFX/Weapon/SFX_Impact_Dirt_01.wav` | 게임 연결 |
| `SFX_Impact_Dirt_02.wav` | `SFX/Weapon/SFX_Impact_Dirt_02.wav` | 게임 연결 |
| `SFX_Impact_Dirt_03.wav` | `SFX/Weapon/SFX_Impact_Dirt_03.wav` | 게임 연결 |
| `SFX_M1_Fire.wav` | `SFX/Weapon/SFX_M1_Fire_01.wav` | 게임 연결 |
| `SFX_M1_Ping.wav` | `SFX/Weapon/SFX_M1_Ping.wav` | 게임 연결 |
| `SFX_M1_Reload.wav` | `SFX/Weapon/SFX_M1_ClipInsert.wav` | 게임 연결 |
| `SFX_Mosin_Fire.wav` | `Supplemental/SFX/SFX_Mosin_Fire_01.wav` | 보조(재생 검사) |
| `SFX_Player_Breath.wav` | `Supplemental/SFX/SFX_Player_Breath_01.wav` | 보조(재생 검사) |
| `SFX_Player_Hit_01.wav` | `SFX/Weapon/SFX_Player_Hit_01.wav` | 게임 연결 |
| `SFX_Rocket_HitSide_01.wav` | `SFX/Weapon/SFX_Rocket_HitSide_01.wav` | 게임 연결 |
| `SFX_Rocket_HitSide_02.wav` | `SFX/Weapon/SFX_Rocket_HitSide_02.wav` | 게임 연결 |
| `SFX_Tank_Cannon_R14.wav` | `SFX/Tank/SFX_Tank_Cannon.wav` | 게임 연결 |
| `SFX_Tank_EngineStall.wav` | `SFX/Tank/SFX_Tank_EngineStall.wav` | 게임 연결 |
| `SFX_Tank_Fire_Loop_R16.wav` | `SFX/Tank/SFX_Tank_Fire_Loop.wav` | 게임 연결 |
| `SFX_Tank_Move_Loop.wav` | `SFX/Tank/SFX_Tank_Move_Loop.wav` | 게임 연결 |
| `SFX_Tank_Ricochet_01.wav` | `SFX/Weapon/SFX_Tank_Ricochet_01.wav` | 게임 연결 |
| `SFX_Tank_Ricochet_02.wav` | `SFX/Weapon/SFX_Tank_Ricochet_02.wav` | 게임 연결 |
| `SFX_Tank_Ricochet_R14.wav` | `Alternatives/R14/SFX_Tank_Ricochet_01.wav` | 대안(재생 검사) |
| `UI_Objective_Show.wav` | `UI/UI_Objective_Show.wav` | 게임 연결 |
| `VEH_GMC_Engine_Loop.wav` | `Supplemental/SFX/SFX_GMC_Engine_Loop.wav` | 보조(재생 검사) |
| `VEH_T34_Engine_Loop.wav` | `SFX/Tank/SFX_Tank_EngineIdle_Loop.wav` | 게임 연결 |
| `VO_OSG_Hurt_Candidate.wav` | `VO/VO_OSG_Hurt_01.wav` | 게임 연결 |

[실측 결과](VERIFICATION.json) · [파일명·SHA·GUID 대응표](FILE_MAPPING.json)
