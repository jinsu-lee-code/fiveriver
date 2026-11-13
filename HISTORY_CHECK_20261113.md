# HISTORY_CHECK_20261113.md — 다부동(1950년 8월) 고증 대조

> 작성 2026-11-13 (Claude, 사용자 지시 "6.25당시 자료및 사료들을 살펴보고 우리나라 논문이나 외국논문 사진 자료 등 모든걸 다긁어와서 오류가 없게끔 만들어놔").
> 조사 방법: 국내 · 해외 자료를 하위 에이전트 둘이 웹으로 나눠 조사, 동작 참고 영상은 직접 열어 캡처. 원전(군사편찬연구소 『6·25전쟁사』 · 『낙동강선방어작전』(2008) · 『다부동전투』(1981), Appleman *South to the Naktong* CMH 원본 PDF)은 웹에서 본문을 열지 못했다 → 아래 판정은 2차 자료와 Appleman 19장 전재본 기준. **원전 대조는 사람이 해야 한다.**
> 표기: **확인** = 두 출처 이상 일치 / **불일치** = 출처마다 다름 / **추정** = 출처 하나 또는 맥락 / **미확인** = 찾지 못함

## 1. 게임 내용과 대조

| 게임 내용 | 판정 | 근거 | 조치 |
|---|---|---|---|
| 1950년 8월 · 다부동, 국군 1사단 야간 방어 | 확인 | 국군 1사단(백선엽) 8월 13일 다부동 배치, 8월 18일부터 북한군 야간 전면 공격(전북일보 2023, 한국민족문화대백과, Appleman 19장) | 그대로 |
| 북한군 소총 = 모신나강 | 확인(주력) | M1891/30이 1950년 미 극동군 정보 자료에 북한군 표준 소총으로 올라 있음(American Rifleman). M1944 기병총의 1950년 사용 근거는 못 찾음 | 그대로 (`SM_WP_Mosin`) |
| 북한군 PPSh-41 | 확인 | "거의 어디에나" 있던 기관단총(American Rifleman), 북한제 49식 | 그대로 |
| 북한군 여름 군복(카키 면 상의, 천 모자) | 추정 | 호주전쟁기념관 소장 북한군 여름 야전 셔츠: 카키 면 능직, 소련식(AWM C104125, 1950~51년경). 철모 착용은 미확인 | 그대로 |
| 조명탄 아래 전투 | 확인(미군 조명탄) · 미확인(국내 자료) | 볼링장 전투에서 미 포병 조명탄으로 도로 위 북한군 차량 19대 식별(Wikipedia "Battle of the Bowling Alley"), 북한군은 녹색 신호탄으로 공격 지점 표시(Appleman 19장), 조명용 트립 플레어 설치. 국내 자료에는 조명탄 언급 없음 | 그대로. 북한군이 조명탄을 쏘는 것처럼 보이는 연출이 있다면 '아군/미군 조명탄'으로 읽히게 하는 편이 사료에 가깝다(현재 대사 "조명탄이다! 엄폐하고 쏴!"는 누가 쐈는지 말하지 않음) |
| 북한군 T-34-85 투입 | 확인 | 전차 투입은 여러 출처 일치. 대수는 "약 20대"(withnews, Daily NK) — 정확한 수는 출처마다 다름 | 그대로 (게임은 1대) |
| 전차 정면 불가 · 옆면을 노림 | 확인 | 2.36인치는 측 · 후면 외에는 약했다는 평가(koreanwaronline, The Armory Life), 국군에 대전차 무기가 부족했다(백선엽 인터뷰) | 그대로 |
| **바주카 = M20(3.5인치)** | 근거 있음(단일 1차 계열) · 불일치 있음 | **교차 검증 (11-13)**: Appleman 19장 원문(전재본에서 직접 확인) "During the day, two enemy tanks came through the ROK 1st Division lines twelve miles north of Taegu at Tabu-dong, but ROK 3.5-inch bazooka teams knocked out both of them."(8월 16일). 같은 장에 미 27연대 F중대 3.5인치 팀의 8월 18 · 21일 격파 기록. 반면 HistoryNet 27연대 기사는 구경 없이 "rocket"으로만 쓰고 국군 1사단의 전차 격파를 인정하지 않음. koreanwaronline 바주카 페이지는 국군 3.5인치 사용 기록을 싣지 않음. 국군 지급 시점 · 수량, 백선엽 회고록 구절은 미확인. 2.36인치를 다부동에서 국군이 썼다는 기록은 못 찾음 | **M20 유지** (사료상 더 맞는 쪽). 원전(Appleman 원서 19장, 군사편찬연구소 『낙동강선방어작전』)으로 최종 확인 권장 |
| **전차 포탑 "432" 숫자와 붉은 별** | **사진으로 반증 → 고침** | 1950년 북한군 T-34-85 사진 5장 확인: 위키미디어 공용 "Tank Action in the Bowling Alley"(다부동 현장), "Captured KPA T-34-85 tanks"(호주전쟁기념관 P00716.064), "T-34-85-korea", "North Korean T-34 tank"(컬러 복원), "3RAR jeep passes a wrecked T34-85 … Songju to Taegu"(호주전쟁기념관 P01813.436). **모두 포탑에 숫자 · 별 없음(민짜)** | **11-13 텍스처에서 지움**: `T_VH_T34_Body_BaseColor.png`의 포탑 양옆 "432"+별 두 곳과 다른 UV 조각에 걸친 별 조각 두 곳(모두 4곳, 78,296픽셀)을 주변 포탑 색 · 결로 메움. 그 밖의 픽셀은 바뀌지 않음(검사함). 원본 백업: scratchpad `decal\T_VH_T34_Body_BaseColor_backup_20261113.png`, 처리 스크립트 `decal_fix_all.py`. 게임 화면에서 확인. 앞판의 돋을새김 무늬는 칠이 아니라 모델 형태(예비 궤도판으로 보임)라 그대로 둠 |
| 학도병 합류(새벽, 윤태석 포항 출신) | 확인(참전) · 미확인(복장) | 국군 1사단에 학도병 약 500명(Daily NK, withnews). 교복 차림 · 무기 지급은 자료로 확인 못함 | 그대로. 학도병 복장은 원전 확인 필요 |
| 북한군 공격 방식(야간 침투, 함성) | 추정 | 야간 공격은 확인, "역 Y자 포위"는 6~7월 전선 기술. 함성 · 나팔은 다부동 기록 미확인 | 그대로 (함성 녹음은 정치 구호 없이 짧은 외침) |

## 2. 쓰지 말아야 할 것 (출처가 엇갈리거나 혼동되기 쉬운 것)
- 정확한 전차 대수("T-34 33대"는 7월 5일 오산 스미스 부대 기록이다).
- 다부동 전투 기간: 8월 1일~9월 24일 / 8월 3일~9월 22일 / 8월 5~20일 등 기관마다 다름 → 화면에는 "1950년 8월"만.
- 328고지 주인이 바뀐 횟수(15번 / 16번).
- 북한군 사단 구성(3·13·15사단 / 1·13·15사단).

## 3. 동작 참고 자료 (2026-11-13 확인)
- 모신나강 3인칭 동작: Brassfire 1896 — https://www.youtube.com/watch?v=_EtsuYGZdC0 (전투 대기 · 조준 대기 · 무릎 조준 · 사격 · 장전). 전투 대기 자세(개머리를 옆구리에, 총구 앞쪽 위)를 적 숙여 달리기 동작에 반영
- 모신나강 조준 사진: 1944년 저격수 샤니나 · 발라예프(위키미디어 공용, 공개 저작물) — 왼손 탄창 앞 총몸 아래, 팔꿈치 총 아래 → 적 조준 손 위치에 반영
- 소총 숙여 달리기: https://www.youtube.com/watch?v=6Ue3q-c1kxk (게임 애니메이션 작업 영상)
- 소련군 재현(영국 284소총사단 재현 부대): https://www.youtube.com/watch?v=lvZNjYCLbFA — 전진하는 병사는 거의 서서 뛰고 소총은 몸 앞에 낮게 든다
- Enlisted 소련군 돌격: https://www.youtube.com/watch?v=u_3qctiymRg — 대부분 1인칭이라 동작 참고는 어려움

## 4. 주요 출처
- 한국민족문화대백과 '다부동전투' https://encykorea.aks.ac.kr/Article/E0013458
- 전북일보 2023 낙동강 방어선 https://www.jjan.kr/article/20230313580184
- Daily NK 백선엽 인터뷰(2009) https://www.dailynk.com/english/dabudong-battle-the-saviour-of-the/
- 국가기록원 1950 낙동강 작전기록 소개 https://theme.archives.go.kr/next/pages/new_newsletter/2016/06/sub03_1.html
- Appleman 19장(전재본) https://koreanwaronline.com/history/Appleman/Chapter19.htm
- Wikipedia "Battle of the Bowling Alley" https://en.wikipedia.org/wiki/Battle_of_the_Bowling_Alley
- American Rifleman "Communist Small Arms of the Korean War" https://www.americanrifleman.org/content/communist-small-arms-of-the-korean-war
- 호주전쟁기념관 북한군 여름 셔츠 https://www.awm.gov.au/collection/C104125
- The Armory Life "T-34-85 in the Korean War" https://www.thearmorylife.com/t-34-85-in-the-korean-war/
- koreanwaronline 바주카 https://koreanwaronline.com/arms/bazooka.htm
