# Remembrance — 다섯개의강 타이틀 BGM

- 파일: `BGM_Title_Remembrance_Loop.wav`
- 길이: 101.052625초, 32마디, 4/4, 76 BPM, D minor
- 형식: 48,000 Hz / stereo / 16-bit PCM WAV
- 구성: 절제된 피아노 주제, 느린 현악 화성, 비올라·첼로의 내성, 낮은 콘트라베이스. 보컬·타악·전투 효과음 없음.
- 곡의 선율·화성·편곡은 이번 요청을 위해 새로 작성했습니다. 다른 곡이나 기존 녹음물을 인용하지 않았습니다. 샘플 악기로 연주한 오리지널 악보를 렌더링했습니다.

## 파일

- `BGM_Title_Remembrance_Loop.wav`: 게임 연결용 완성 반복 음악.
- `BGM_Title_Remembrance_Loop.mid`: 한 주기 악보. MIDI 플레이어에 따라 음색/리버브가 다르므로 게임에는 WAV를 사용합니다.
- `BGM_Title_Remembrance_Loop_Preview20s.wav`: 34~54초 구간의 20초 미리듣기. 양끝 페이드는 이 파일에만 있습니다.
- `BGM_Title_Remembrance_Loop_LoopBoundary12s.wav`: 마지막 6초→처음 6초를 연속 배치한 경계 확인용.
- `compose_title_bgm.py`: 악보 작성 및 FluidSynth 렌더링.
- `master_and_verify.py`: 중간 반복 주기의 리버브 꼬리를 보존해 추출, 10.7 ms 경계 보정, DC 제거, true-peak 기준 선형 음량 조절, 16-bit TPDF dither 및 측정.
- `composition.json`: 전체 음표·편성·구성 기록.
- `audio_measurements.json`: 피크·RMS·스테레오·경계·해시 측정.
- `render_three_cycles.mid`, `render_three_cycles_float.wav`: 재현/검증용 중간 결과. 게임 배포에는 필요 없습니다.
- `sources/`, `tools/`: 악기 샘플과 설치 없는 렌더러. 게임 에셋에 자동으로 복사하지 않습니다.

## 확인 결과와 한계

피크 약 -3.00 dBFS, RMS 약 -20.22 dBFS. 4배 windowed-sinc 보간 true-peak 추정 약 -3.00 dBTP. 포화 샘플 0개, 추정 inter-sample clipping 없음. 스테레오 상관계수 0.742. 루프 경계의 인접 샘플 차이는 L -67.39 dBFS, R -70.31 dBFS이며 전체의 99.9 percentile 인접 차이보다 충분히 작습니다. -80 dBFS 이하 구간이 10 ms 이상 지속되는 무음은 없었습니다.

직접 청음은 수행하지 못했습니다. 수치 결과는 음악적 취향이나 모든 가청 아티팩트의 부재를 보증하지 않습니다. 위 미리듣기와 경계 파일로 실제 재생 확인이 가능합니다.

완성 WAV는 파형을 0으로 페이드아웃하지 않는 연속 루프입니다. 타이틀 첫 진입에서 1.5초 정도 페이드인하고, `loop=true`로 재생하는 사용을 권장합니다. Unity의 강제 mono 변환은 끕니다. Unity 연결을 완료했습니다. Streaming/Vorbis 품질 0.8, stereo/48 kHz로 가져왔고, SoundLibrary의 타이틀 항목만 새 WAV로 바꿨습니다. 타이틀 재생과 시간 진행, 게임 시작 시 정지를 Play mode에서 확인했습니다.

## 사용 악기와 출처

GeneralUser GS v2.0.3, S. Christian Collins.

- 공식 소개/허가 안내: https://www.schristiancollins.com/generaluser.php
- 공식 저장소: https://github.com/mrbumpy409/GeneralUser-GS
- 고정 커밋: `684543d5e5efaef08d02be50dcda8d552478fa60`
- 샘플 파일: https://github.com/mrbumpy409/GeneralUser-GS/blob/684543d5e5efaef08d02be50dcda8d552478fa60/GeneralUser-GS.sf2
- 라이선스 원문: `sources/GeneralUser-GS-LICENSE.txt`
- 라이선스는 개인/상업 음악 제작 및 제작한 녹음의 수익화를 허용합니다. 원문에는 일부 오래된 개별 샘플의 최초 출처를 저자가 100% 확정할 수 없다는 설명도 있습니다. 그 설명을 포함한 원문 전체를 보존했습니다.
- SoundFont SHA-256: `9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe` (실제 검증값은 아래 manifest 및 측정 JSON을 우선합니다.)

## 렌더러

FluidSynth v2.6.2 (LGPL), 공식 Windows x64 포터블 릴리스. 설치 및 시스템 설정 변경 없이 사용했습니다.

- 공식 다운로드: https://www.fluidsynth.org/download/
- 고정 릴리스: https://github.com/FluidSynth/fluidsynth/releases/tag/v2.6.2
- 렌더러 라이선스: `sources/FluidSynth-LICENSE.txt`
- 공식 소스: https://github.com/FluidSynth/fluidsynth/tree/v2.6.2

이 폴더의 SoundFont·렌더러는 제작/재현 도구이며 게임에 필요한 파일은 완성 WAV 한 개입니다. 작곡 단계에서는 Unity를 변경하지 않았습니다. 이후 별도 연결 작업으로 신규 WAV와 라이선스 문서를 추가하고 SoundLibrary의 타이틀 항목 하나를 교체했습니다.

## 재생성

Blender 5.2에 포함된 Python 3.13 및 numpy 2.3.4를 사용했습니다.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe' -X utf8 ./compose_title_bgm.py
& 'C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe' -X utf8 ./master_and_verify.py
```

악보의 타이밍/강약 및 출력 dither 시드는 고정했습니다. 렌더러의 내부 상태나 버전 차이 때문에 재생성 파형이 비트 단위로 동일함을 보장하지는 않습니다.
