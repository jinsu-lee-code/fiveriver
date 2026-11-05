# 다부동 당시 사진 참고 — 도로 표면 적용

2026-11-02. 기존 맵의 높이·배치·모델 형상·튜토리얼을 보존하고 기존 전차 접근로의 표면 혼합만 조정했다. 정확한 촬영 지점의 3D 복원이나 1950년 색 복원은 아니다.

## 확인한 원본

| 자료 | 확인되는 정보 | 이번 사용 |
| --- | --- | --- |
| [AWM P00716.048](https://www.awm.gov.au/collection/C43476) | 1950년 다부동 인근의 파괴된 T-34. 정확한 월·일 불명. 항목에 Public Domain 명시 | 비포장 도로의 고른 바탕, 불규칙한 입자·잔해와 거친 가장자리 관찰 |
| [AWM P00716.047](https://www.awm.gov.au/collection/C43475) | 1950년 다부동 인근 파괴된 자주포. 월·일 불명, F. Peach 기증, Public Domain | 같은 지역의 도로와 도로변 환경 교차 확인 |
| [NARA 필름 NAID 21988](https://catalog.archives.gov/id/21988) | 미 육군 필름 `Korean War, Tabu-dong`, 8분48초. [NARA 기록관 설명](https://text-message.blogs.archives.gov/2023/05/23/where-to-lay-an-american-hero-the-burial-controversy-of-john-rice-ho-chunk/)이 1950-09-02 촬영으로 확인 | 경작지 사이 고른 도로 중앙과 거친 가장자리 참고. 개별 권리 필드는 접속 실패로 미확인, 게임 이미지로 삽입하지 않음 |
| [국사편찬위원회 SC347898](https://archive.history.go.kr/record/catalog/catalogView.do?systemId=000000902857) | 1950-09-06 다부동 아래쪽에서 쉬는 한국 경찰, CPL Buckley. AUS005_06_02V0000_396 / FEC-50-7747 | 날짜·장소 교차 조사만 수행. 사본의 비상업적 이용 조건 때문에 게임 텍스처로 사용하지 않음 |

참고 사진 원본으로 텍스처를 만들거나 게임에 사진을 삽입하지 않았다. AWM 공개본은 이 검토 문서의 출처 보관용으로만 보존했다. [AWM 이용 안내](https://www.awm.gov.au/about/organisation/corporate/copyright).

## 적용한 부분

- 기존 Mud 도로 마스크를 사용했다. 전차 시작/정지 지점을 잇는 기존 중심선 주변, 월드 z18..112 안에서만 도로 표면을 혼합했다. 새로운 도로 폭·경로·바퀴 홈을 만들지 않았다.
- 중앙은 기존 Soil을 주로 써 거칠게 갈라진 표현을 줄이고, 가장자리에만 소량의 [Poly Haven Rocky Trail 스캔](https://polyhaven.com/a/rocky_trail)을 불규칙하게 혼합했다. 해당 재료는 [CC0](https://polyhaven.com/license)이며 다부동 현장에서 촬영한 재료는 아니다.
- 사진에서 중앙/가장자리의 차이는 확인되지만 흙색·습도·정확한 입자 크기와 PBR 값은 확정할 수 없다. 현재 색과 재질 수치를 유지하고 혼합 비율만 화면 비교로 조정했다.
- 새 TerrainData `Assets/_Project/Art/HistoricalDabudong20261102/Frontline_HistoricalSurface.asset`를 생성했다. 원본 및 앞선 PhotoPatch는 보존했다.
- 도로 수정 후보는 알파맵 5226/262144 셀이며, 8bit 저장 후 실제로 달라진 셀은 5183개다. 도로 범위 밖 255057셀의 변경은 0개·최대 차이 0으로 확인했다. 참호 앞 스캔 재질 패치는 앞선 작업의 4408셀 범위를 유지한다. 이 수치는 면적의 정밀 실측이 아니라 알파맵 셀 수이다.

## 지형 참조 수정

앞선 PhotoPatch 적용 후 표시 Terrain은 원본으로, TerrainCollider는 PhotoPatch로 연결된 저장 상태를 발견했다. 높이 데이터는 같았지만 표시용 지형에 스캔 재질이 나타나지 않았다. 표시/충돌 컴포넌트 모두 SerializedObject와 prefab override로 같은 새 TerrainData에 연결하고 저장 후 씬을 다시 열어 확인했다.

`persistence.json`: persisted=true, Terrain/TerrainCollider 경로 동일, 6 layers, dirty=false.

높이 float SHA-256 전후 동일:
`454106D1CE1B7F288B4FACA626D6C1FEEE55E32839B5E404C8A81C91AC227EAE`

`application.json`: placementsUnchanged=true. 새 지형/오브젝트/모델 추가 없음.

현재 씬에서 EditMode 114/114 및 Play 통합 30/30이 통과했다. Play 종료 후에도 동일 지형 참조·6레이어·높이 해시가 유지되었다. 이번 표면 수정 뒤 W로 경계를 미는 검사를 반복하지는 않았고, 기존 경계값 z=3.2 및 관련 시스템 검증을 유지했다. `alpha_distribution.json`, `terrain_after_play.json`, `all_tests_historical_final.json`, `system_flow_historical_final.json`에 근거를 보관했다.

## 시각 검토

`before_detail.png`와 `after_detail.png`는 동일한 진단 카메라·조명에서 캡처한 엔진 화면이다. 일반 플레이 카메라와 구분한다. 주 에이전트와 독립 검토자가 모두 사진의 관찰과 변화 방향을 대조했다. 새 경계 이음새·두드러진 반복 격자·과대한 자갈은 확인하지 못했다. 측면 둔덕 시야도 `before_mound.png`/`after_mound.png`로 비교했다.

[Unity 글](https://unity.com/kr/blog/engine-platform/photogrammetry-real-world-objects-into-digital-assets)은 실제 사물의 여러 사진으로 에셋을 만드는 사진측량과 재질 레이어링을 설명한다. 이번 옛 사진은 촬영 위치·각도가 겹치는 충분한 촬영 세트가 아니므로, 정확한 3D 형상 복원은 수행하지 않았다. 사진의 환경 관찰에 기존 스캔 PBR 재료를 대응시키는 방식으로 적용했다.
