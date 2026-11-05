# 표면·환경 자료 출처 — 2026-10-30

추가 표면 작업을 위해 확보한 원본 자료다. 아래 자료는 외부 사진 기반 CC0 자료이며, 프로젝트에서 AI로 생성한 이미지로 분류하지 않는다. 실제 적용 및 화면 검수 결과는 최종 구현·독립 검수 기록을 따른다.

| 자료 | 제작자 | 원본 페이지 | 준비한 해상도 |
|---|---|---|---|
| Qwantani Night (Pure Sky) | Greg Zaal (촬영), Jarod Guest (처리) | https://polyhaven.com/a/qwantani_night_puresky | 2K HDR |
| Qwantani Dawn (Pure Sky) | Greg Zaal (촬영), Jarod Guest (처리) | https://polyhaven.com/a/qwantani_dawn_puresky | 2K HDR |
| Stretch Poplin | colormass (촬영), Rico Cilliers (처리) | https://polyhaven.com/a/stretch_poplin | 1K Diffuse / OpenGL Normal / Roughness |
| American Walnut Veneer | Jenelle van Heerden | https://polyhaven.com/a/american_walnut_veneer | 1K OpenGL Normal / Roughness, 원본 총 UV·색상 아틀라스를 유지하는 표면 보강용 |

Poly Haven의 [자산 라이선스](https://polyhaven.com/license)는 위 자산을 CC0로 제공한다. 2026-10-30 공식 자산 페이지와 공개 API에서 출처를 확인했다. 다운로드 파일은 API에 제시된 MD5와 대조했으며 SHA-256도 별도 기록했다. 원본 파일과 출처·해시 JSON은 `C:/Users/mbc/Documents/ChatGPT/다섯개의강/Map_Import_20261030/surface_sources/`에 있다.

기존 프로젝트의 흙·목재·금속·피부·소품 텍스처도 우선 조사했다. `evidence/local_pbr_audit.json`은 이미지 해상도와 채널, 임포트 설정을 기록한 읽기 전용 조사 결과다. 서로 다른 모델의 UV 아틀라스를 이름만 보고 바꾸지 않으며, 원본 손·총 모델과 텍스처는 보존한다.

추가로 코드에서 생성하는 미세 모공·직조·금속 표면 맵은 위 외부 촬영 자료와 구분한다. 그 생성 코드와 적용된 머티리얼의 기록을 함께 남긴다. 새 실행 파일은 사용자의 빌드 금지 지시에 따라 만들지 않았다.
