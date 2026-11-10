# Sound 원본 자료 (다섯개의강)
main의 게임 프로젝트에 들어간 소리를 만들 때 쓴 원본입니다. 게임에서 쓰는 최종 파일은 `five_river/Assets/_Project/Audio/`에 있습니다.

- `varco_input_running.mp4`: 게임에서 낮 고지 비탈을 6초 달리는 장면(30fps). VARCO Sound에 올린 영상
- `varco_run_tracks/`: VARCO Sound가 위 영상으로 만든 트랙 6개(플레이어 발소리·소총 다루는 소리·환경음·적 소리 3개)
- `scripts/slice_varco_run.py`: 발소리·소총 트랙에서 또렷한 순간을 골라 한 걸음씩(0.32/0.36초) 잘라 `SFX_Foot_Run_Varco_01~08`, `SFX_Gear_Run_Varco_01~06` 생성
- `scripts/make_run_foley.py`: 바르코 이전의 달리기 장비 소리·숨소리(기존 녹음을 잘라 섞음)
- `scripts/seq_to_mp4.py`: 게임 녹화 이미지 → mp4 (Blender, `SEQ_PCT`/`SEQ_CRF`로 크기 조절)
- 주의: VARCO 무료 플랜 결과물은 상업적 사용에 제한이 있을 수 있음
