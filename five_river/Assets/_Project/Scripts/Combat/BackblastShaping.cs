using UnityEngine;

namespace FiveRivers.Combat
{
    // 로켓포반(2026-11-24, 사용자 "후폭풍 연기가 탄약수를 가리는 연출은 지워"): 탄약수가 있을 때만(Bazooka.LoaderControlled)
    // 후폭풍을 발사관 뒤의 좁은 원뿔로 모은다. 탄약수는 사수 오른쪽 0.42 m · 뒤 0.8 m 에 있어 원래 효과(반구 · 구 모양 연기 반지름 0.8~1.1 m,
    // 크기 2~3.5 m 짜리 빌보드, 범위 8 m 점광)가 그를 덮었다. 탄약수가 없는 흐름은 예전 그대로.
    // 효과 프리팹(Art/VFX/VFX_Bazooka_Backblast.prefab, 2 MB 파티클 에셋)은 고치지 않고 생성 직후 이 값으로 다듬는다. 값은 화면을 보고 맞춘다.
    public static class BackblastShaping
    {
        public static bool Enabled = true;       // 점검용: false 면 예전 효과 그대로
        public static float ConeAngle = 12f;       // 연기 · 불꽃이 퍼지는 반각(도)
        public static float ConeRadius = 0.3f;     // 방출 반지름 상한(m)
        public static float LateralScale = 0.55f;  // 효과 좌우 · 상하 배율 (효과의 z 가 뒤쪽)
        public static float MaxSmokeSize = 1.2f;   // 연기 · 폭발 입자 최대 크기(m)
        public static float LightRange = 3.5f, LightScale = 0.6f;

        public static void ForLoader(GameObject effect)
        {
            if (effect == null || !Enabled) return;
            var root = effect.transform;
            root.localScale = new Vector3(LateralScale, LateralScale, 1f);
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var shape = ps.shape;
                if (shape.enabled && (shape.shapeType == ParticleSystemShapeType.Sphere || shape.shapeType == ParticleSystemShapeType.Hemisphere
                    || shape.shapeType == ParticleSystemShapeType.SphereShell || shape.shapeType == ParticleSystemShapeType.HemisphereShell))
                {
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = ConeAngle;
                    shape.radius = Mathf.Min(shape.radius, ConeRadius);
                    // 원뿔은 그 입자계의 +z 로 나간다: 효과 전체의 +z(뒤쪽)로 향하게 돌린다
                    shape.rotation = (Quaternion.Inverse(ps.transform.rotation) * root.rotation).eulerAngles;
                }
                if (ps.name.Contains("Smoke") || ps.name.Contains("Explosion"))
                {
                    var main = ps.main;
                    if (main.startSizeMultiplier > MaxSmokeSize) main.startSizeMultiplier = MaxSmokeSize;
                }
            }
            foreach (var light in effect.GetComponentsInChildren<Light>(true))
            {
                light.range = Mathf.Min(light.range, LightRange);
                light.intensity *= LightScale;
            }
        }
    }
}
