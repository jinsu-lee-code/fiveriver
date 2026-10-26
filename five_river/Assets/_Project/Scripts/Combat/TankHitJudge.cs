using UnityEngine;

namespace FiveRivers.Combat
{
    public enum TankHitResult
    {
        Ricochet = 0, // 정면 도탄 (효과 없음)
        Side = 1,     // 측면 유효 명중
    }

    // 전차 측면 판정 (GDD 5-5, BALANCE 2-3). 쏜 사람이 전차 정면에서 몇 도 옆에 있었는지로 정한다.
    // 화면에서 옆면이 보이는 위치 = 유효 판정 위치가 되도록 수평 각도만 본다.
    public static class TankHitJudge
    {
        public static float AngleFromFront(Vector3 tankPosition, Vector3 tankForward, Vector3 shooterPosition)
        {
            var forward = new Vector3(tankForward.x, 0f, tankForward.z);
            var toShooter = new Vector3(shooterPosition.x - tankPosition.x, 0f, shooterPosition.z - tankPosition.z);
            if (forward.sqrMagnitude < 1e-6f || toShooter.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.Angle(forward, toShooter);
        }

        public static TankHitResult Judge(Vector3 tankPosition, Vector3 tankForward, Vector3 shooterPosition, float sideAngle)
        {
            return AngleFromFront(tankPosition, tankForward, shooterPosition) >= sideAngle ? TankHitResult.Side : TankHitResult.Ricochet;
        }
    }
}
