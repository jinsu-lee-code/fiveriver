# TTS 대사 제거 — 2026-10-30

사용자의 요청으로 임시 TTS 대사·적 외침 WAV 12개와 해당 meta를 게임 프로젝트에서 제거했다. `VO_SQD_*` 7개, `VO_PARK_*` 2개, `VO_KPA_Shout_*` 3개가 대상이다. SOUND_LIST 및 `tools/make_graybox_vo.ps1`의 음성 생성 목록으로 범위를 확인했다.

- SoundLibrary에서 해당 항목 10개 제거. 자막 9개의 음성 참조는 비우고 문구·표시 시간은 보존했다.
- AudioGraybox의 TTS 항목 자동 연결과 DataAssetsSetup의 기본 음성 참조를 제거했다. 적 생성 시 TTS 외침을 호출하던 구독도 제거했다.
- 자막만 표시할 때 효과음이 불필요하게 작아지지 않도록 실제 음성 재생 중에만 덕킹한다.
- 비언어 신음 `VO_OSG_Hurt_01~03`, 효과음, 음악은 유지했다.
- 기존 자막 테스트는 문구·양수 표시 시간을 검사하고, 음성이 설정된 경우에만 SoundLibrary 연결을 요구한다.

원본 파일·meta·SoundLibrary·GameText 백업과 SHA256 목록은 `C:/Users/mbc/Documents/ChatGPT/다섯개의강/Map_Import_20261030/tts_removed_backup`에 보존했다.

현재 M1 1인칭 사격·조준 개선 작업의 프로젝트는 이 TTS 제거 상태를 사용한다. 2026-10-30 재확인 결과 VO 폴더에는 비언어 신음 3개만 남아 있고, 자막 9개의 voice 필드는 모두 비어 있다. 자막 문구와 표시 시간, 총소리·환경음·음악은 유지한다.

사용자의 빌드 금지 요청에 따라 새 실행 파일은 만들지 않았다. `C:/Users/mbc/Downloads/FIveRiver/FiveRivers.exe`는 제공받은 이전 실행 파일이며, 소스 수정만으로 그 실행 파일의 음성이 바뀌지는 않는다. TTS 제거 적용 대상은 현재 Unity 프로젝트이고 검증 범위는 Editor와 소스/에셋 검사다.
