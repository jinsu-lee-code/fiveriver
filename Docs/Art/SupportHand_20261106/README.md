# M1 왼쪽 지지손 — 현재 적용 기준

현재 사용 대상은 원본 VARCO 손이다. 이번 변경은 사용자가 선택한 `original_choice.jpg` 오른쪽 아래 #5의 절대 local Euler 회전 `(-12, 2, -39)`만 적용한다. 원본 메시·피부 텍스처·재질과 크기·위치는 유지한다. 회전은 매번 절대값으로 설정하므로 재실행해도 누적하지 않는다.

## 현재 활성 원본과 설정

프로젝트 루트 기준 경로:

- FBX: `five_river/Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/SM_CH_ROK_SupportHand_M1.fbx`
- 재질: `five_river/Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/M_CH_ROK_SupportHand_M1.mat`
- 피부: `five_river/Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/T_CH_ROK_SupportHand_M1_BaseColor.png`
- 설정 코드: `five_river/Assets/_Project/Scripts/Editor/SupportGripSetup.cs`
- 보존된 원본 Blender: `Docs/Art/M1FPS_20261030/SupportHandVarco/SM_CH_ROK_SupportHand_M1_Cut.blend`

값:

| 항목 | 현재 값 |
| --- | --- |
| HandEuler | `(-12, 2, -39)` — 추가 회전이 아닌 최종 local Euler |
| HandPosition | `(0, 0.026, 0.20)` |
| HandScale | `0.65` |
| 손목 측정축 | 원본 local `+Z` |
| 원래 끝점 seed | `(0, -0.09, 0)` |
| 원래 cuff overlap | `0.008` |
| 재질 tint / smoothness | 원래 white / `0.25` |

`SupportGripSetup.Apply()`가 이 원본 메시·재질과 절대 회전을 연결한다. 위치 보상이나 별도 손가락·손목·heel 변형은 사용하지 않는다. 기존 소매 천 표현과 아군 팔꿈치 수정은 별도 작업으로 유지한다.

## 이 폴더의 이전 파생 후보

`M1_LeftSupport_Editable_20261106.blend`는 앞서 만든 아군 파지 기반 파생 손 후보다. pack된 `SupportSkin_Atlas`, 총·소매·카메라 문맥이 들어 있지만 **현재 활성 원본도, 현재 회전의 편집 원본도 아니다.** 현재 게임 적용본으로 내보내거나 재반입하지 않는다.

`export_final_support.py` 역시 그 이전 파생 Blender 후보 전용 exporter다. 파일명의 final은 당시 후보 이름이며, 현재 채택 상태를 뜻하지 않는다. 이 exporter는 현재 원본 FBX를 재현하는 도구가 아니다.

이전 `VisiblePolished`·coupled·heel 변형은 현재 setup의 모델/재질/텍스처 경로에서 사용하지 않는다. 검증 전 추가 정리나 삭제는 하지 않았다.

## 비교 근거

선정 이미지: 아래 외부 QA 폴더의 `original_choice.jpg`, 오른쪽 아래 #5.

`C:/Users/mbc/.codex/visualizations/2026/11/02/01a199af-6dde-73d0-a46f-1508e6f64f5d/Expanded_20261106`

독립 이미지 비교에서는 원본 피부색·볼륨이 유지되고, 기준 자세보다 엄지 끝이 더 완전하게 읽히며 다른 각도에서 보인 큰 바깥 손가락 돌출은 두드러지지 않았다. 이 기록은 모델 변형이나 전체 플레이 밸런스 검증을 뜻하지 않으며, 최종 품질 판단은 사용자가 한다.
