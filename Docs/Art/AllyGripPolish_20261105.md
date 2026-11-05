# 아군 지지손과 흙 탄착 효과 수정 — 2026-11-05

대상 프로젝트: `five_river`, Unity 6000.3.25f1, `Assets/_Project/Scenes/Dabudong.unity`.

## 수정 범위

- `AlliedRifleFit.cs`: 기본값 false인 `refineSupportGrip` 추가.
- `AllyRiflePose.cs`: 해당 옵션을 켠 견착 자세의 왼손만 원본 손가락 자세에서 느슨하게 굽힌다. 기존 강한 주먹 굽힘과 엄지 끝 강제 접촉에서 발생하던 고리형 파지를 제거했다. 기존 공통 Grip·오른손·박용팔의 내려 든 자세는 유지한다.
- 씬의 초반 정규군 세 명만 옵션을 켜고 지지손 방향을 조정했다. 원본 메시·리그·팔 비례·뼈 위치·견착 위치·FPS 손·박용팔·새벽 학도병·맵·튜토리얼·전투 판정은 변경하지 않았다.
- Effects의 ImpactDirt 종료 시간을 0.5초에서 1초로 조정했다. 기존 0.8초 입자가 중간에 갑자기 없어지지 않고 자연스럽게 사라지도록 했다. 새 입자나 효과 에셋은 추가하지 않았다.

## 검증

Sol ultra 수정, Astra 독립 시각 검수 및 코드·VFX 검수 통과. 실제 GameView 1600×900 전후/측면, 실제 아군 발사와 반동 복귀, 나머지 두 정규군 근접 화면을 확인했다. 참고는 [미국 의회도서관 1942년 M1 파지 사진](https://www.loc.gov/item/2017878418/) 및 [다른 각도 사진](https://www.loc.gov/item/2017878398/)이며 당시 다부동의 복장 고증 근거로 해석하지 않았다.

실제 M1 입력으로 지면 명중 1회·탄약 8→7·흙 효과 1회를 확인했고, 반복 효과의 자연 소멸 및 ClearAll 정리도 확인했다. 기존 EditMode 검사 119/119 통과(3.3초). 검사한 스크립트·프리팹·씬 291개 중 의도한 위 3개 파일만 변경됐다. 최종 씬 누락 스크립트 0, 저장 상태 정상.

이는 해당 아군 자세·흙 효과의 국소 검증이다. 전체 게임 완주, 모든 모델/1인칭 손 품질, 새 실행 파일 빌드를 의미하지 않는다. 커밋·푸시는 하지 않았다.

## 기록 및 복구

외부 증거/백업 경로: `C:/Users/mbc/.codex/visualizations/2026/11/02/01a199af-6dde-73d0-a46f-1508e6f64f5d/AllyPolish_20261105`.

- `backup_before_production`: 이번 작업 직전 파일 원본.
- `FINAL_grip_comparison.png`: 같은 실제 GameView의 손/팔 영역을 나란히 배치한 비교. 생성 이미지가 아니다.
- `AFTER_actual_GameView_side.png`: 수정 후 실제 측면 GameView.
- `ASTRA_FINAL_VERDICT.md`, `ASTRA_CODE_REVIEW.md`, `VFX/ASTRA_VFX_FINAL.md`: 검수 기록.
- `editmode_test_status.json`, `preservation_after.json`: 검사 결과와 파일 보존 확인.

어깨 뒤 갈색 패치를 개머리판 관통으로 본 초기 판독은 오류였으며 철회했다. 무기를 숨겨도 남는 부분이라 견착 위치 변경은 반영하지 않았다. 검수 도구의 잘못된 임시 뼈대 저장본도 원본 백업으로 복원한 뒤 제외했다. 최종 씬 SHA-256: `46E97DFBC2EC7A996D0D8ABE642E3E3B2095753542A4810134BEAC9CEFA76EE1`.
