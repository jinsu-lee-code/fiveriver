# 전차 등장 연출 두 판 비교 (2026-11-11)

Fable판(코드로 계산한 고정 카메라 8컷)과 Blender판(Blender 키프레임, 움직이는 카메라 5컷)을 같은 조건으로 녹화해 나란히 놓은 자료입니다. **어느 판을 쓸지는 사용자 결정 보류 중** — 게임 기본값은 `TankConfig.entranceTake = 0`(연출 없음).

| 파일 | 내용 |
|---|---|
| `전차연출_나란히비교.mp4` | 왼쪽 Fable판 · 오른쪽 Blender판, 실제 속도 21초, 컷 이름 · 컷 눈금 · 조명탄/대사/발포 표시 (소리 없음) |
| `전차연출_컷비교.jpg` | 1.5초마다 두 판을 나란히 놓은 사진 표 |

## 다시 만들기 (Unity 에디터가 `C:/pf/five_river`로 열려 있어야 함)
1. 녹화 — 판마다 플레이를 새로 시작하고, 정적 구간으로 옮긴 뒤 연출을 30fps 고정 시간(`Time.captureFramerate`)으로 재생하며 2프레임마다 화면 저장(`C:/sk_assets/capture/compare/fable|blender/`). 타이틀 화면이 겹치지 않게 정적 구간에서 시작함
   ```
   python unity_run.py "cmd:editor.playmode start" sleep8 jump_silence.cs sleep6 rec_fable.cs
   python unity_run.py state.cs          # playing=False cf=0 이 되면 끝 (약 1분)
   python unity_run.py "cmd:editor.playmode stop" sleep3 "cmd:editor.playmode start" sleep8 jump_silence.cs sleep6 rec_blender.cs
   ```
2. 합치기 — `python compose.py` (프레임 · 사진 표, 경로는 파일 맨 위 상수) → `blender -b --python to_mp4.py -- C:/sk_assets/capture/compare/out <출력.mp4> 15 1920 1080`
3. 녹화 원본(`C:/sk_assets/capture/compare/`)은 확인 후 지움

## 게임에서 직접 보기
플레이 → F1 디버그 창 → "전차 등장 연출" → "Fable판 보기" / "Blender판 보기" (Space · Enter 건너뛰기). 고르면 `five_river/Assets/_Project/Data/TankConfig.asset`의 `entranceTake`를 1(Fable) 또는 2(Blender)로.

## 합본 (2026-11-12)
두 판의 좋은 컷을 섞고 카메라 흔들림을 넣은 세 번째 판(`TankEntranceMixedTake`, 디버그 창 "합본 보기", `entranceTake = 3`). 바퀴 · 궤도가 굴러가는 것(`TankRunningGear`)이 보이도록 **매 프레임(30fps)** 저장함 — 15fps로 뽑으면 바퀴살이 멈춘 것처럼 보임.
```
python unity_run.py "cmd:editor.playmode start" sleep8 jump_silence.cs sleep6 rec_mixed.cs     # 약 5분, state.cs로 끝 확인
python compose_mixed.py     # 컷 시각은 파일 위쪽 SHOTS · EVENTS (전차가 보이는 순간이 바뀌면 고칠 것)
blender -b --python to_mp4.py -- C:/sk_assets/capture/compare/out_mixed  <전차연출_합본.mp4> 30 1920 1080
blender -b --python to_mp4.py -- C:/sk_assets/capture/compare/out_wheels <전차연출_합본_바퀴확대.mp4> 30 1920 1080
```
결과물(영상 2개 · 사진 표 2장)은 용량 때문에 저장소에 넣지 않고 `Documents/poject/Fiveriver/전차연출_비교/`에 둠.
