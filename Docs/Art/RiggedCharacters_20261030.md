# 리깅 캐릭터 납품본 적용

원본: 사용자가 제공한 `Character_Rigged_Models3.zip`.

| 원본 모델 | Unity 경로 (`Assets/_Project/Art/Models/Characters/` 기준) | 적용 대상 |
|---|---|---|
| SK_CH_KPA_Rifleman | KPA_Rifleman/SK_CH_KPA_Rifleman.fbx | 기존 북한군 프리팹 |
| SK_CH_ROK_ParkYongpal | ROK_ParkYongpal/SK_CH_ROK_ParkYongpal.fbx | 기존 박용팔 프리팹 |
| SK_CH_ROK_StudentSoldier | ROK_Soldier/SK_CH_ROK_Soldier.fbx | 기존 아군 병사·증원 병사 슬롯 |
| SK_CH_KPA_Rifleman_B | KPA_Rifleman_B/SK_CH_KPA_Rifleman_B.fbx | 예비 프리팹, 활성 씬 미배치 |
| SK_CH_ROK_Protagonist | ROK_Protagonist/SK_CH_ROK_Protagonist.fbx | 예비 프리팹, 기존 1인칭 팔·카메라는 유지 |

기존 세 모델의 `.meta` GUID와 게임 프리팹 GUID를 유지한다. `FiveRivers/Setup/Apply Rigged Character Models3` 메뉴가 임베디드 텍스처를 추출하고 URP 재질을 연결한 다음, 기존 프리팹의 Model을 교체한다. 게임 판정·체력·NavMeshAgent·CapsuleCollider는 유지한다. 각 게임 프리팹에서 키와 발바닥 위치를 맞추고 기존 AC_KPA 또는 AC_ROK 컨트롤러를 연결한다.

Protagonist의 `LeftHand` 자동 매핑 실패는 원본에 본이 없어서 생긴 문제가 아니다. 원본에는 `mixamorig:LeftHand`와 전체 Mixamo 골격이 존재한다. 자동 Avatar 생성에 실패한 모델에는 실제 본 이름을 명시적으로 매핑한다.

## 원본 리깅 결함과 보정 결과

FBX Cluster의 Indexes/Weights를 직접 검사한 결과다. 아래 정점은 원본 파일에 양수 스킨 가중치가 없다.

| 모델 | 전체 정점 | 가중치 미할당 정점 |
|---|---:|---:|
| KPA_Rifleman | 26,709 | 0 |
| ROK_ParkYongpal | 31,744 | 0 |
| ROK_StudentSoldier | 20,701 | 0 |
| KPA_Rifleman_B | 26,031 | 5,422 |
| ROK_Protagonist | 48,968 | 4,678 |

이후 사용자의 전체 수정 요청에 따라 두 결함을 별도 Blender 작업본에서 보정하고 Unity 6000.3.25f1에 적용했다.

- B형 얼굴·목의 미할당 5,422개는 정상 KPA 머리를 골격 기준으로 맞춘 뒤 가까운 표면 삼각형의 가중치를 보간했다.
- 주인공 양손 4,678개는 정상 학생병 양손과 정점 수·면 연결·UV가 정확히 일치함을 확인한 다음 동일 정점의 가중치를 복원했다.
- 두 모델 모두 미할당 정점은 **0개**이며 원래 정상 정점의 가중치 변화는 **0**이다. 원본 파일은 별도 보존했다.
- 수정 FBX를 다시 열어 정점 수·면 연결·UV·재질·본 이름·텍스처 바이트가 유지됨을 확인했다. 머리 회전과 팔·손목·손가락 변형도 검수했다.
- Unity에서 수정본 두 개만 `ApplyRepairedVariants()`로 갱신하고 최종 5종의 Humanoid·키·본/메시/재질 참조를 다시 확인했다. B형·주인공은 여전히 예비 프리팹이며 활성 게임에 새로 배치하지 않았다.

두 모델의 FBX는 100 MB를 초과하므로 원격 저장에는 Git LFS 등 대용량 에셋 전달 방식을 사용해야 한다. 보정 과정의 `.blend`·진단 이미지·감사 JSON은 작업 폴더의 `character_repairs/`에 보존했다.

## 확인

Unity **6000.3.25f1**에서 `CharacterImportSetup.Apply()` 실행 및 프리팹 검증을 완료했다.

| 프리팹 | 높이(m) | 발바닥 Y | 유효 Humanoid | URP 텍스처 재질 |
|---|---:|---:|---|---:|
| PF_CH_KPA_Rifleman | 1.68 | 0 | 확인 | 6/6 |
| PF_CH_KPA_Rifleman_B | 1.68 | 0 | 확인 | 6/6 |
| PF_CH_ROK_ParkYongpal | 1.72 | 0 | 확인 | 5/5 |
| PF_CH_ROK_Protagonist | 1.70 | 0 | 확인 | 8/8 |
| PF_CH_ROK_Soldier | 1.70 | 0 | 확인 | 6/6 |

5개 모두 메시·본 참조·재질 누락 0건이며 기존 애니메이션 컨트롤러 연결을 확인했다. Protagonist의 명시적 본 매핑 후 Avatar 생성도 성공했다. 가중치 보정본을 적용한 뒤에도 이 검사를 통과했으며 결과는 통합 작업의 `evidence/characters_final_validation.json`에 기록했다.

`ArtSpecTests.Characters_HeightAndSlots`는 납품본 태그가 있는 모델의 게임 프리팹에 대해 키·발 피벗·Humanoid Avatar·스킨 본·URP 재질·BaseColor 텍스처 연결을 검사한다. 다른 에셋과 기존 그레이박스 검사는 유지한다. 최종 화면·이동·사격 중 캐릭터 확인 및 전체 테스트 결과는 통합 작업에서 별도 기록한다.

## 2026-11-02 등장인물 정정
위 표는 당시 납품 적용 이력이다. Soldier FBX는 교복·학생모이고 Park FBX는 정상 철모·군복이다. 초기 비교의 좌우 설명과 학도병 전체 제외 해석은 정정한다. 사용자 최신 확인에 따라 초반 일반 아군에만 Protagonist 군복 모델을 사용하고, 새벽 보충 인원과 합류하는 학도병은 별도 증원 프리팹으로 보존한다. 박용팔 원래 모델·역할 및 기존 증원 수·동선·시점은 유지한다. 최신 구성은 five_river/Docs/Narrative/CastCorrection_20261102.md를 따른다.
