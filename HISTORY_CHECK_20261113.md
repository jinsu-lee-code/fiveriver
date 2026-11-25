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

## 5. 2026-11-15 추가 대조 (Claude, 사용자 지시 "모두 전투기록에 적힌대로 그대로 만들어")

Appleman 19장 전재본(koreanwaronline)을 다시 읽어 그날 넣은 것들을 대조했다. 인용은 전재본 원문.

| 게임 내용 | 판정 | 근거 | 조치 |
|---|---|---|---|
| 전차 길 양쪽의 미루나무 | 확인 | "the poplar-lined Taegu-Sangju road ran northward on a level course in the narrow mountain valley" | 11-15 추가 (`ValleyRoad_Aug1950`) |
| 길 서쪽의 개울 | 확인 | "A stream on the west closely paralleled it." | 11-15 추가. 서쪽(-x)에 배치. 물은 웅덩이만 남김(가뭄 기록에 따른 해석, 추정) |
| 풀 색 | **불일치 → 고침** | "as they rumbled down the narrow, green valley". 11-15 오전에는 30년 만의 가뭄 기록(다부동전적기념관)만 보고 짚색으로 만들었다 | 푸른 쪽으로 고침: 풀 healthy 0.34,0.44,0.21 / dry 0.54,0.53,0.29, 풀밭 바닥 색조 0.93,0.97,0.72. 가뭄은 군데군데 마른 색과 드러난 흙으로 남김 |
| 북한군 녹색 신호탄 | 확인 → **추가** | "The North Koreans used a system of flares to signal various actions and to co-ordinate them ... green flares were used to signal an attack on a given area." | `Combat/KpaSignalFlare.cs`: 주 공세 · 서쪽 공세 시작 때 적 쪽 능선 뒤에서 녹색 신호탄 1발. 조명 판정에는 관여하지 않음 |
| 달밤 | 확인 | "A bright moon silhouetted enemy tanks against the dark flanking mountains" | 그대로 (달빛 방향광 있음) |
| 전차 · 자주포가 앞서고 보병이 길 양쪽으로 따름 | 확인(기록) · 게임은 단순화 | "Nine tanks and several SP guns supported the enemy troops in the valley", "moved forward on both sides of the road" | 게임은 전차 1대. 바꾸지 않음(설계 범위) |
| 지뢰 · 인계철선 조명탄 · 미군 M26 전차 | 확인(기록) · 게임에 없음 | "antipersonnel mines, and trip flares across the road and stream bed", "Pershing M26 tanks of C Company, 73d Tank Battalion" | 넣지 않음. 이들은 미 27연대 진지 것이고 게임은 국군 1사단 참호 |
| 국군 1사단 위치 | 확인 | "the ROK 1st Division held the high ground on either side of the 27th Infantry positions" | 그대로. 다만 게임 참호는 골짜기 길에서 40 m 거리라 능선보다는 낮다(지형 단순화) |
| 포격에 부서진 나무 | 확인(증언) | "온전한 나무도 온전한 바위도 없었다"(참전자, 매일신문 2023-06-22) | 11-15 추가 (`ShelledPines_Aug1950`) |
| 인민군 모습(얼굴 · 군복) | 추정 | 카키 면 여름 군복 · 천 모자(§1). 얼굴은 사료가 아니라 생성 이미지 | 11-15 새 몸 · 새 머리 3종. 각반과 가죽 탄입대는 생성 모델의 것으로, 1950년 8월 북한군 지급품과의 일치는 **미확인** |
| 전차 형태 | 확인(차종) | T-34-85, 포탑에 숫자 · 별 없음(§1) | 11-15 새 차체도 민짜. 높이를 9% 늘린 것은 사료와 무관한 게임 쪽 타협 |

### 원전 대조 (2026-11-15 저녁)

Appleman 원서를 미 육군 군사사센터 공식 사이트에서 받아 직접 확인했다 (CMH Pub 20-2, `history.army.mil/portals/143/Images/Publications/catalog/20-2.pdf`, 852쪽, 사용자가 브라우저에서 저장; 사본 `C:/sk_assets/sources/appleman_cmh/20-2.pdf`). 위 표의 인용은 **모두 원서에 그대로 있다**. 책 쪽수(PDF 쪽수 - 33):

| 내용 | 원서 쪽 | 원문 |
|---|---|---|
| 국군 3.5인치 로켓포 | 353 | "two enemy tanks came through the ROK 1st Division lines twelve miles north of Taegu at Tabu-dong, but ROK 3.5-inch bazooka teams knocked out both of them" (8월 16일) |
| 국군 1사단은 양쪽 고지 | 355 | "The ROK 1st Division held the high ground on either side of the 27th Infantry positions." |
| 미루나무 길 · 서쪽 개울 | 355 | "the poplar-lined Taegu-Sangju road ran northward on a level course in the narrow mountain valley. A stream on the west closely paralleled it. The road was nearly straight on a north-south axis" |
| 유학산 · 가산 높이 | 356 | "Rising abruptly from the valley on the west side was the Yuhak-san mountain mass which swept up to a height of 2,700 feet. On the east, a similar mountain mass rose to a height of 2,400 feet, culminating ... in towering Ka-san, more than 2,900 feet high at its walled summit" |
| 전차 · 자주포가 앞서고 보병이 따름 | 356 | "Two enemy tanks and a self-propelled gun moved out of the village of Sinjumak ... Infantry followed them, some in trucks and others on foot." (8월 18일 밤, 일곱 번의 야간 공격 중 첫 번째) |
| 북한군 녹색 신호탄 | 357 | "The North Koreans used a system of flares to signal various actions and to co-ordinate them. ... green flares were used to signal an attack on a given area." 이어서 미 27연대가 같은 녹색 신호탄을 쏴 적을 속였다고 적음 |
| 지뢰와 조명 | 357 | "The use of mines in front of the defensive positions in the narrow valley became a nightly feature of the battles. ... At such times flares illuminated the scene" |
| 달밤 · 푸른 골짜기 | 358 | "A bright moon silhouetted enemy tanks against the dark flanking mountains as they rumbled down the narrow, green valley, leading another attack." (8월 20일 밤) |

주의: 이 장면들은 **미 27연대가 지킨 골짜기 바닥(볼링장)** 의 기록이다. 게임의 국군 1사단 참호는 그 양쪽 고지여야 맞고, 원서는 국군 고지 전투를 자세히 적지 않는다. 국군 쪽 세부(수류탄전, 야간 기습)는 국내 자료(§1)에 따른다. 군사편찬연구소 간행물은 여전히 열지 못했다.

## 6. 2026-11-15 한국 측 공간사 대조 (Claude)

출처: 국방부 군사편찬연구소, 『6·25전쟁사 5권 낙동강선 방어작전』(2008), 제3장 제2절 "제11연대의 다부동지역 전투". 군사편찬연구소 누리집 간행물 소개에서 공개 PDF. 사본 `C:/sk_assets/sources/imhc_naktong/naktong_v5.pdf`, 본문 추출 `pages.json`(책 쪽수 = PDF 쪽수 − 10).

| 항목 | 기록 (책 쪽수) | 게임 | 판정 |
|---|---|---|---|
| 국군이 길목 골짜기를 지킨 때 | 제11연대 제1대대가 신주막–다부동 골짜기(복곡)를 지키다 8월 18일 미 27연대에 넘기고 400~500 m 능선으로 올라감(167). 8월 26일 03:45 제11연대가 미 27연대 지역을 다시 인수(176) | 참호가 길 옆 낮은 곳 | **근거 있음.** 8월 13~17일 또는 26일 이후의 제11연대라면 길목이 맞다. 참호를 옮길 필요 없음 |
| 미군이 있던 동안의 배치 | 미 27연대는 5번 도로 중심 1,000 m 안팎, 좌우 감제고지는 제11연대(166). 8월 21일 도로 좌우 고지를 제11연대 1대대와 제15연대 2대대가 장악(168) | — | Appleman 355쪽과 일치 |
| 국군의 대전차 무기 | "대전차 무기는 최근 지급받은 3.5인치 로켓포가 전부"(160) | 로켓으로 전차 격파 | 일치 |
| 전차가 길로 옴 | 8월 15일 아침 전차 7대와 장갑차 5대가 제1대대 진지를 뚫고 신주막을 거쳐 수정으로(162). 신주막–다부동은 "적 전차부대의 기동로"(160) | 전차가 길로 내려옴 | 일치 |
| 조명탄 | 8월 21일 밤 미 27연대가 적 접근 보고를 받고 즉각 조명탄 발사(169) | 조명탄 장면 | 일치 (쏜 쪽은 미군) |
| 근거리 유인과 수류탄 | 8월 20일 밤 제15연대 2대대가 적을 진전 50 m까지 유인한 뒤 기습사격과 수류탄(168) | 근거리 교전 | 일치 |
| 능선을 따라 오는 보병 | 8월 21일 밤 접근로 양측 능선으로 각 1개 대대 규모가 은밀히 접근(169) | 서쪽 공세 | 일치 |

- 결정: 참호 위치와 화면 문구("1950년 8월 · 다부동")는 그대로 둔다. 화면에 부대 이름이나 날짜를 더 넣지 않으므로 어긋나는 곳이 없다. 설정을 물으면 "제11연대, 미군 도착 전(8월 13~17일)의 신주막–다부동 길목"으로 답한다.
- 주의: 미루나무 길과 녹색 신호탄은 이 책에 나오지 않는다(Appleman 기록). 학도병 복장은 이 책에서 확인하지 못했다.


## 7. 2026-11-17 한국 측 공간사 원문 재대조 (Claude, 사용자 지시 "한국측 기록 다운받아져있는거 가져다가 기록이랑 같은지 확인해")

방법: 내려받아 둔 `C:/sk_assets/sources/imhc_naktong/naktong_v5.pdf`(808쪽)에서 본문을 새로 뽑아(PyMuPDF, 11-15의 `pages.json`은 쓰지 않음) §6 표의 7개 항목을 인용 쪽의 원문과 한 줄씩 맞춰 봤다. 쪽 번호는 본문에 찍힌 숫자로 확인(책 쪽수 = PDF 쪽수 − 10).

| §6 항목 | 원문 (책 쪽수) | 결과 |
|---|---|---|
| 길목 골짜기 인계 · 능선 진출 | "제11연대 제1대대는 미군에게 신주막-다부동 골짜기인 복곡(卜谷) 방어지대를 인계한 후 … 높이 400～500m의 능선으로 진출"(167), "제11연대는 26일 03:45분에 미 제27연대의 방어지역을 인수"(176) | 같음 |
| 미군 정면 1,000 m, 좌우 고지는 제11연대 | "5번 도로를 중심으로 1,000m 안팎의 협소한 정면 … 그 좌우의 감제고지는 제11연대가 담당"(166), "다부동 접근로의 좌우 양측방 감제고지를 장악"(168) | 같음 |
| 대전차 무기 | "대전차 무기는 최근 지급받은 3.5인치 로켓포가 전부였다"(160) | 같음 (글자 그대로) |
| 전차가 길로 옴 | "8월 15일 아침에 전차 7대와 장갑차 5대가 신주막을 거쳐 수정(水亭)으로 진출"(162), "대구에 이르는 적 전차부대의 기동로"(160) | 같음 |
| 조명탄 | "미 제27연대는 … 적 접근 보고를 받고 즉각 조명탄을 발사"(169) | 같음 |
| 50 m 유인 · 수류탄 | "이날(20일) 밤 02:00～03:00시 사이 … 진전 50m까지 바싹 유인한 다음 기습사격을 가하고 수류탄으로"(168) | 같음 |
| 양측 능선으로 접근 | "접근로의 양측 능선을 따라 각각 1개 대대규모의 북한군이 은밀히 접근"(169) | 같음 |

- §6 "주의" 확인: 책 전체에서 "미루나무" 0번, 150~200쪽에서 "신호탄" · "학도" · "함성/나팔" 0번. 적어 둔 대로다.
- **고칠 곳 1 (§6 결정의 날짜)**: "미군 도착 전(8월 13~17일)의 길목"은 원문보다 넓게 적었다. 원문 순서는 8월 13일 제1대대가 **208고지-하판동 일대**에 배치(160) → 15일 아침 그 진지가 뚫려 **진목정으로** 빠져나옴(162) → 17일 **수정의 좌측 능선** 점령(163) → 18일 복곡 방어지대를 미군에 인계(167). 책은 복곡을 지킨 시작 날짜를 적지 않았다. 설정을 물으면 "제11연대 제1대대, **8월 15~18일**(15일 진목정으로 물러난 뒤 18일 미군에 넘기기까지)의 신주막-다부동 길목, 또는 8월 26일 이후"로 답한다. 게임 화면에는 날짜 · 부대 이름이 없어 바꿀 것이 없다.
- **덧붙일 것 2 (§1 전차 대수)**: 이 책은 신주막-다부동 접근로의 북한군 전차연대(제13사단 배속)가 "전차 14대를 재보급"받았다고 적는다(166). §1의 "약 20대"(withnews, Daily NK)와 다르다 → §2 "쓰지 말아야 할 것"(정확한 전차 대수)에 그대로 해당. 게임은 1대라 영향 없음.
- **덧붙일 것 3 (§1 "8월 18일부터 야간 전면 공격")**: 이 책에는 야간 공격이 더 일찍 나온다 — 8월 14일 17:00 공격 시작, "야음을 이용해 제1대대 방어진지를 뚫고"(162). 18일 저녁은 전차 5대를 포함한 보전협동 공격(167), 21일 밤이 대규모 야간 공격(169). 게임의 "야간 방어전"과는 맞는다.
- 책 안의 엇갈림(참고만): 제15연대 제2대대의 356고지를 167쪽은 "5번 도로 우측", 168쪽은 "좌측"으로 적었다. 우리 기록은 방향을 쓰지 않았다.
- 결론: §6 표 7개 항목은 원문과 같다. 틀리게 옮긴 숫자 · 날짜 · 쪽수는 없다. 위 1의 날짜 범위만 좁혀 읽는다. 게임 파일 변경 없음.

## 8. 2026-11-26 시작 컷씬 날짜 · 고증 점검 도구 반영 (Claude, 사용자 지시 "처음에 컷씬 하나 만들고 다부동 전투날짜 하나 검은색 배경에 넣어서")

- **화면에 넣은 날짜: "1950년 8월 21일 밤"** (`GameText.introDate`). 『6·25전쟁사 5권 낙동강선 방어작전』 원문을 다시 읽고 게임 장면과 가장 많이 겹치는 밤을 골랐다.
  - 168쪽: 8월 21일 아침 제11연대 제1대대가 356고지를 탈취해 제15연대 제2대대와 함께 "다부동 접근로의 좌우 양측방 감제고지를 장악". 미 제27연대는 "대대적인 야간공격"을 예상.
  - 169쪽: 그날 밤 "신주막–다부동 접근로의 양측 능선을 따라 각각 1개 대대규모의 북한군이 은밀히 접근", 5번 도로로 보전협동부대, 미 제27연대가 "즉각 조명탄을 발사", 전차 7대 · 자주포 3문.
  - 게임과 겹치는 것: 밤, 조명탄(쏜 쪽은 미군 — §1과 같음), 능선을 따라 오는 보병(서쪽 공세), 길로 오는 전차, 국군 1사단이 골짜기 양쪽 고지를 지킴.
  - 다른 후보와 버린 이유: 8월 14일 밤(162쪽)은 제1대대 진지가 뚫려 15일 아침 물러난 밤이라 결말과 맞지 않는다. 8월 18일 저녁(167쪽)은 전차를 3.5인치로 잡은 것이 미 27연대 F중대다.
  - 남는 차이(그대로 둠): 게임 참호는 길에서 40 m로 능선보다 낮다(§5 지형 단순화). 21일 밤 전차를 잡은 것은 미군 전차와 F중대 로켓포(169쪽)다. 국군 3.5인치 팀의 격파 기록은 8월 16일 낮(Appleman 353쪽). 게임의 전차 격파는 이 둘을 합친 단순화.
- 장소 "경상북도 칠곡군 다부동", 설명 "대구로 가는 길목. 국군 제1사단이 골짜기 양쪽 능선을 지키고 있었다." — 160쪽("대구에 이르는 적 전차부대의 기동로"), 168쪽(좌우 감제고지 장악).
- 같은 날 고증 점검 도구(`Documents\poject\Fiveriver\history_check`, Claude API)로 화면 문구 88개를 대조해 고친 것: `tankArrives` "정면은 안 돼" → "정면에 서지 마"(정면 불가 근거가 2.36인치 평가였음), M1 장전 힌트(남은 클립을 빼고 새 클립), 국군 기록 용어 "3.5인치 로켓포"(바주카 → 로켓포 · `bazookaName` "M20 3.5인치 로켓포"), `m1Name` "M1 소총", 만수 사투리 "몰라가꼬예", 탄약수 "사수, 이쪽입니다!".
- 확인 못 한 것(도구 보고서 "확인이 필요한 것"): 1950년 8월 국군 1사단 주 소총이 M1이었는지, 진지의 모래주머니, 포항 출신 보충병 · 트럭 합류, 편지의 지명(군사우편 검열).
