// 식생 바람 (2026-11-27, 감독 세션 전달 — 사용자 지시 "나무가 흔들림이 없어서 바람이 없는 것 같다 … 사실적인 디테일은 놓치지 말고").
// 1950년 8월 다부동 한여름 밤~새벽의 약한 바람(1~3 m/s, 가끔 돌풍): 줄기 · 밑동은 거의 그대로(높이의 제곱), 가지 끝 · 잎이
// 흔들리고, 포기 · 나무마다 박자가 달라 숲 전체가 한꺼번에 같이 움직이지 않는다. 돌풍은 바람 방향을 따라 지나간다.
// 방향은 연기 · 안개 · 미루나무와 같은 쪽(동쪽, 조금 북쪽 — 메뉴 FiveRivers/Setup/Vegetation Wind 20261127 이 맞춘다).
// FR_WindLit.shader(URP Lit 사본)와 같이 쓴다. 재질 값: _WindHeight(m, 식물 높이) · _WindStrength(m, 꼭대기 흔들림) · _WindSpeed.
#ifndef FR_WIND_INCLUDED
#define FR_WIND_INCLUDED

float _WindStrength, _WindSpeed, _WindHeight, _WindFlutter;
float4 _WindDirection;

float3 FR_WindOS(float3 positionOS)
{
    if (_WindStrength <= 0.0) return positionOS;
    float3 ws = TransformObjectToWorld(positionOS);
    float3 origin = TransformObjectToWorld(float3(0, 0, 0));
    float scaleY = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
    float h = saturate(positionOS.y * scaleY / max(_WindHeight * max(scaleY, 0.01), 0.01));
    float t = _Time.y * _WindSpeed;
    float2 dir = normalize(_WindDirection.xz + 1e-4);
    // the gust travels along the wind: places further downwind get it a little later
    float along = dot(origin.xz, dir);
    float gust = saturate(sin(t * 0.33 - along * 0.035) * 0.5 + 0.5); gust *= gust;
    float phase = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831;   // own beat per plant
    float sway = sin(t + phase) * 0.6 + sin(t * 2.17 + phase * 1.7) * 0.25;
    float lean = (0.35 + 0.65 * gust);                                                         // leans with the wind, comes back
    float amount = _WindStrength * h * h * (lean * 0.55 + sway * (0.45 + 0.4 * gust));
    // small leaves and needle tips flutter faster, by their own position
    float flutter = _WindFlutter * h * sin(t * 6.3 + ws.x * 3.7 + ws.z * 2.9 + ws.y * 4.1) * (0.4 + 0.6 * gust);
    float3 side = float3(-dir.y, 0, dir.x);
    ws += float3(dir.x, 0, dir.y) * amount + side * flutter * 0.6 + float3(0, flutter * 0.4 - abs(amount) * 0.15, 0);
    return TransformWorldToObject(ws);
}
#endif
