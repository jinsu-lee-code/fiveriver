using UnityEngine;

namespace FiveRivers.Data
{
    // 걷은 소매 (2026-11-27, 사용자 결정 "소매 걷기" — 1950년 8월 한여름 국군처럼 소매를 팔꿈치 근처까지 두세 번 접어 걷고
    // 팔뚝은 맨살). M1 을 들 때의 1인칭 팔(M1ArmRig 가 매 프레임 만드는 천 튜브)에 쓴다. 바주카 팔(Tripo)은 같은 수치로
    // source/characters/fp_arms_tripo_20261123/roll_sleeves.py 가 미리 만든다 — 두 무기의 걷은 높이 · 띠 · 팔뚝 모양이 같다.
    // 관 길이 t: 어깨 0 · 팔꿈치 0.5 · 손목(손 잘린 테두리) 1. 팔뚝 비율 f = 팔꿈치 0 · 손목 1 (바주카 스크립트와 같은 단위).
    // BALANCE.md 4-8.
    [CreateAssetMenu(fileName = "RolledSleeveConfig", menuName = "FiveRivers/Rolled Sleeve Config")]
    public sealed class RolledSleeveConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Data/RolledSleeveConfig.asset";

        [Header("접힌 띠 (팔뚝 비율 f, 팔꿈치 0 · 손목 1. 음수는 팔꿈치 위)")]
        [Tooltip("띠 위 끝")] public float bandTop = -0.14f;
        [Tooltip("띠 아래 끝(입술) — 이 아래는 맨살")] public float bandBottom = 0.14f;
        [Tooltip("접힌 골 위치 (띠 위 끝 · 골 · 골 · 입술)")] public float[] folds = { -0.14f, -0.05f, 0.04f, 0.14f };
        [Tooltip("띠 굵기 = 원래 소매 굵기 x")] public float bandScale = 1.08f;
        [Tooltip("접힌 겹마다 볼록한 정도(반지름 비)")] public float ridge = 0.09f;
        [Tooltip("입술에서 맨살 굵기로 줄어드는 길이(팔뚝 비율)")] public float lipLength = 0.025f;
        [Tooltip("M1: 팔꿈치를 굽힌 안쪽 면에서 띠 반지름을 줄이는 비율 (굽힘 · 안쪽 정도에 비례) — 접힌 천이 눌린 모양, 띠가 아래 팔뚝을 뚫지 않게")]
        public float bandInsideSquash = 0.5f;

        [Header("맨살 팔뚝 (손목 맨살 반지름의 배수)")]
        [Tooltip("(팔뚝 비율, 배수) — 실제 손목 둘레 약 17 cm · 팔뚝 가장 굵은 곳 약 27 cm")]
        public Vector2[] skinProfile = { new Vector2(0.14f, 1.62f), new Vector2(0.30f, 1.58f), new Vector2(0.50f, 1.42f), new Vector2(0.70f, 1.22f), new Vector2(0.86f, 1.06f), new Vector2(0.95f, 1.0f) };
        [Tooltip("손목에서 단면 가로/세로 비 ≈ 1 + 이 값 (손목 쪽으로 납작한 타원)")] public float ellipse = 0.24f;
        [Tooltip("폄근 · 위팔노근 덩이 (팔뚝 비율 중심, 폭, 각도 rad, 크기)")] public Vector4 extensors = new Vector4(0.24f, 0.15f, 0.55f, 0.07f);
        [Tooltip("굽힘근 덩이 (반대쪽, 조금 아래)")] public Vector4 flexors = new Vector4(0.34f, 0.17f, 3.4f, 0.05f);
        [Tooltip("자뼈 끝 돌기 (손목 바로 위, 아주 약하게)")] public Vector4 ulna = new Vector4(0.90f, 0.035f, 3.6f, 0.045f);

        [Header("M1 천 튜브에서의 자리 (관 길이 t, 팔꿈치 0.5)")]
        [Tooltip("이 t 부터 아래 링은 피부 재질 (띠 아래 입술 바로 뒤)")] public float m1SkinFromT = 0.585f;
        [Tooltip("손 잘린 테두리 자리에서 맨살 반지름 = 테두리 x 이 값")] public float m1SkinOverRim = 1.0f;
        [Tooltip("테두리를 지나 손 안으로 들어간 맨살 끝 반지름 = 테두리 x 이 값 (손목 반지름 약 3 cm 에서 0.95 ≈ 1.5 mm 안쪽). " +
                 "1.03 으로 손 위에 덮었을 때는 맨살 고리가 손 위로 튀어나와 밝은 선으로 보였다 (2026-11-27 감독 검수)")]
        public float m1SkinTuck = 0.95f;
        [Tooltip("팔뚝 맨살 색 = 손 텍스처 피부의 이 백분위 밝기 (손 텍스처에는 주름 · 손톱 · 그늘이 섞여 중앙값은 보이는 손보다 어둡다)")] public float m1SkinPercentile = 0.65f;
        [Tooltip("그 색에 곱하는 값 (왼손 · 오른손 따로: 두 손은 재질 · 텍스처가 다르다) — 1배속 화면에서 손등과 팔뚝 밝기를 재서 맞춘다")]
        public float m1SkinToneLeft = 1.0f, m1SkinToneRight = 1.0f;
        [Tooltip("손목 쪽 끝에서 손 색으로 넘어가는 구간 (팔 튜브 uv 의 길이 v: 어깨 0 → 손목 0.94) — 약 3~4 cm")]
        public Vector2 m1WristBlend = new Vector2(0.875f, 0.94f);
        [Tooltip("손목 끝 색 = 팔뚝 색 x (손 잘린 테두리 바로 옆 손의 밝기 / 팔뚝 밝기), 왼손 · 오른손")]
        public float m1RimToneLeft = 1.0f, m1RimToneRight = 1.0f;
        [Tooltip("팔뚝 맨살 피부결(손과 같은 Skin_DetailNormal) 세기 — 손(0.9)의 절반, 결 크기는 손등과 같게 반복 (둘레, 길이). 전에는 다른 결 지도를 굵게 써서 스타킹 같은 점무늬였다")]
        public float m1PoreStrength = 0.45f;
        public Vector2 m1PoreTiling = new Vector2(5f, 12f);

        [Header("재질 (메뉴 Rolled Sleeves 20261127 가 손 재질에서 만든다)")]
        public Material leftSkin;
        public Material rightSkin;
    }
}
