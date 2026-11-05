# R6 최종 검증 — 2026-11-02

- Windows x64 release 빌드: **Succeeded**, 25.163초, 오류 0.
- 빌드 경고 485개: `ConvGeneric` 460, `Hidden/Sentis/*` 25. 기존 Unity AI inference 패키지 셰이더 경고이며 이번 연출의 셰이더 오류는 없다. 패키지를 임의 삭제하거나 변경하지 않았다.
- EditMode: **114/114**, 실패·스킵 0.
- 현재 씬 통합 진행: **30/30**, 런타임 오류 0. 4배속 자동 입력/일부 단계 주입으로 실행한 검사이며 사람의 전체 플레이와 구분한다.
- 실제 LMB 바주카 검사: **8/8**. 발사 1, 로켓 1, 후폭풍 1, 장전 1. 최종 탄약 1/3. 약 0.61초 후 반동·총구 입자 0. 새 궤적 적용 후 0.03/0.10초 PNG에서 큰 흰 삼각면이 사라짐을 두 검토자가 확인했다.
- 실제 Rocket 전차 검사: **13/13**. 정면 도탄, 측면 1발 피격, 2발 격파, Dawn 전환, CP3/Title 초기화. 물리 body와 Collider의 변위 0.
- R6 실행 파일 시작 검사: 약 77초 프로세스 유지, 예외·셰이더 오류 0. D3D12 info queue interface 조회 진단 1줄이 기록되었다. 시작/로그 검사이며 실제 움직임의 시각 검증은 Editor에서 수행했다. 확인용 프로세스는 종료했다.
- 에디터: Edit, scene dirty=false, GameView 최대화 해제, 팔 없는 전장 전체 미리보기로 복구했다.
- 배포 제외 Burst 디버그 심볼은 QA 폴더에 보관했다.

실행 파일: `C:/Users/mbc/Desktop/FiveRiver_Polished_R6_20261102/FiveRivers.exe`

증거:
- `CombatFeedback_20261102/build_R6.json`, `build_R6_summary.json`
- `CombatFeedback_20261102/all_tests_presentation_final.json`
- `CombatFeedback_20261102/system_flow_presentation_final.json`
- `CombatFeedback_20261102/shot_runtime_final.json`, `shot_*_gameplay_final.png`
- `CombatFeedback_20261102/R6_startup_result.json`, `R6_player_startup.log`

독립 코드 검토에서는 현재 연결 경로의 확정 회귀 문제가 발견되지 않았다. 장시간 부스 시연과 직접 BGM 청음은 아직 수행하지 않았다. 기존 맵·모델 전체의 공개 배포 적합성을 새로 인증한 결과로 해석하지 않는다.

추가 확인: 역사 사진 적용 과정에서 R6의 표시 Terrain은 원본5레이어, Collider는 PhotoPatch6레이어로 저장된 것을 발견했다. 따라서 R6의 참호 앞 스캔 재질 표시는 완료 상태가 아니었다. R7에서는 두 컴포넌트의 SerializedObject/prefab override를 함께 저장하고 씬 재열기·Play 진입 후 같은6레이어 참조가 유지됨을 검증했다.
