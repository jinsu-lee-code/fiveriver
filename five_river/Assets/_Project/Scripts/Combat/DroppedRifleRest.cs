using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 쓰러진 적이 놓친 소총을 언제 · 어떻게 굳히는가 (2026-11-27, 검수 캡처 hitdir/front_head_0.7 · 2.0: 땅에 비스듬히 선 채 굳음).
    // 순수 계산만 둔다 (MonoBehaviour 없음, 단위 테스트 DroppedRifleRestTests). 엔진 쪽은 Combat/DroppedRifle.
    //  - 소총 강체가 잠들거나, 정한 속도 · 회전 아래로 정한 시간 머물거나, 떨어진 뒤 상한 시간이 지나면 굳힌다.
    //    (예전: 시신이 굳을 때 EnemyAgent.Settle 이 그 순간 강체를 지워, 기대거나 구르던 자세 그대로 얼었다.)
    //  - 굳힐 때 긴 축이 지면에서 maxTilt 보다 서 있으면 지면 법선에 맞춰 가장 작은 회전으로 눕힌다.
    // 수치는 BodyPhysicsConfig(BALANCE 2-1d "떨어진 소총").
    public static class DroppedRifleRest
    {
        public struct Rule
        {
            public float restSpeed, restSpin, restHold, most, maxTilt, laySeconds;

            public static Rule From(BodyPhysicsConfig config)
            {
                if (config == null) return Fallback;
                return new Rule
                {
                    restSpeed = config.rifleRestSpeed, restSpin = config.rifleRestSpin, restHold = config.rifleRestHold,
                    most = config.rifleMost, maxTilt = config.rifleMaxTilt, laySeconds = config.rifleLaySeconds,
                };
            }

            // 래그돌 설정이 없는 몸(그레이박스)에서만 쓴다. BodyPhysicsConfig 의 기본값과 같다.
            public static Rule Fallback => new Rule { restSpeed = 0.05f, restSpin = 0.25f, restHold = 0.4f, most = 6f, maxTilt = 25f, laySeconds = 0.3f };
        }

        // 정지해 있는 시간: 둘 다 문턱 아래면 늘고, 하나라도 넘으면 0으로.
        public static float StillFor(float stillFor, float speed, float spin, float dt, in Rule rule)
            => speed < rule.restSpeed && spin < rule.restSpin ? stillFor + dt : 0f;

        public static bool ReadyToFreeze(float sinceDrop, bool sleeping, float stillFor, in Rule rule)
            => sinceDrop >= rule.most || sleeping || stillFor >= rule.restHold;

        // 긴 축이 지면(법선 normal 인 평면)에서 서 있는 각도 (0 = 누움, 90 = 똑바로 섬)
        public static float Tilt(Vector3 longAxis, Vector3 normal)
        {
            if (longAxis.sqrMagnitude < 1e-8f || normal.sqrMagnitude < 1e-8f) return 0f;
            return Mathf.Abs(90f - Vector3.Angle(longAxis, normal));
        }

        public static bool NeedsLay(float tilt, in Rule rule) => tilt > rule.maxTilt;

        // 긴 축(localLong)을 지면에 눕히는 회전. 축이 기운 쪽으로 넘어뜨리고(가장 작은 회전), 그 방향을 법선 둘레로
        // yaw 도 돌린 것도 고를 수 있다(눕힌 자리가 막혔을 때). 똑바로 서 있으면 소총의 위쪽(local up)이 향한 쪽으로.
        public static Quaternion Lay(Quaternion rotation, Vector3 localLong, Vector3 normal, float yaw = 0f)
        {
            normal = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            var along = rotation * localLong;
            var flat = Vector3.ProjectOnPlane(along, normal);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.ProjectOnPlane(rotation * Vector3.up, normal);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.ProjectOnPlane(Vector3.forward, normal);
            flat = Quaternion.AngleAxis(yaw, normal) * flat.normalized;
            return Quaternion.FromToRotation(along, flat) * rotation;
        }

        // 눕히기 진행 (0..1, 부드럽게)
        public static float LayProgress(float since, in Rule rule)
            => rule.laySeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(since / rule.laySeconds));
    }
}
