# FR_WindLit.shader 만들기 (2026-11-27): URP Lit.shader 를 그대로 복사해 이름을 바꾸고, 정점 단계마다 FR_Wind.hlsl 의 바람을 먼저 적용한다.
# 재질은 셰이더만 바꾸면 텍스처 · 노멀맵 · 알파 오림 등 Lit 설정이 그대로 남는다. URP 버전이 바뀌면 다시 실행한다.
# 실행: python make_windlit.py  (또는 blender -b --factory-startup --python make_windlit.py)
import glob
import io
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.abspath(os.path.join(HERE, "..", "..", "..", "five_river"))
src = glob.glob(os.path.join(PROJ, "Library", "PackageCache", "com.unity.render-pipelines.universal@*", "Shaders", "Lit.shader"))[0]
dst = os.path.join(PROJ, "Assets", "_Project", "Art", "Maps", "Dabudong_Env", "Shaders", "FR_WindLit.shader")
s = io.open(src, encoding="utf-8").read()
s = s.replace('Shader "Universal Render Pipeline/Lit"', '// 생성 파일 (source/environment/wind_20261127/make_windlit.py): URP Lit + 식생 바람(FR_Wind.hlsl)\nShader "FiveRivers/WindLit"', 1)
s = s.replace("    Properties\n    {\n", "    Properties\n    {\n"
              "        _WindHeight(\"Wind: plant height (m)\", Float) = 1.0\n"
              "        _WindStrength(\"Wind: sway at the top (m)\", Range(0, 0.6)) = 0.05\n"
              "        _WindSpeed(\"Wind: speed\", Range(0, 4)) = 1.1\n"
              "        _WindFlutter(\"Wind: leaf flutter (m)\", Range(0, 0.1)) = 0.012\n"
              "        _WindDirection(\"Wind: direction (xz)\", Vector) = (0.95, 0, 0.32, 0)\n", 1)
passes = {"LitPassVertex": "LitForwardPass.hlsl", "ShadowPassVertex": "ShadowCasterPass.hlsl", "LitGBufferPassVertex": "LitGBufferPass.hlsl",
          "DepthOnlyVertex": "DepthOnlyPass.hlsl", "DepthNormalsVertex": "LitDepthNormalsPass.hlsl"}
for fn, inc in passes.items():
    s = s.replace(f"#pragma vertex {fn}\n", f"#pragma vertex FRWind_{fn}\n", 1)
    field = "position" if fn == "DepthOnlyVertex" else "positionOS"
    line = f'#include "Packages/com.unity.render-pipelines.universal/Shaders/{inc}"\n'
    assert line in s, inc
    s = s.replace(line, line +
                  '            #include "FR_Wind.hlsl"\n'
                  f'            Varyings FRWind_{fn}(Attributes input) {{ input.{field}.xyz = FR_WindOS(input.{field}.xyz); return {fn}(input); }}\n', 1)
s = re.sub(r'\n\s*CustomEditor[^\n]*', '\n', s)   # the Lit inspector would hide the wind values
io.open(dst, "w", encoding="utf-8").write(s)
print("wrote", dst)
