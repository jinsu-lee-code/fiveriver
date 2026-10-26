using System;

namespace FiveRivers.Combat
{
    public enum ReloadKind
    {
        None = 0,
        Empty = 1,   // 마지막 발에 클립이 튀어나간 뒤 새 클립 (ANIM_SPEC A_FP_M1_ReloadEmpty)
        Partial = 2, // 남은 클립을 빼고 새 클립 (A_FP_M1_ReloadPartial)
    }

    // M1 개런드 탄약 규칙 (GDD 7번 "8발 → 클립 배출 → 장전 → 사격").
    // 잔탄 장전 때 뺀 클립의 탄은 예비탄으로 돌아간다.
    public sealed class M1Magazine
    {
        public M1Magazine(int clipSize, int reserve, int maxReserve)
        {
            if (clipSize <= 0) throw new ArgumentOutOfRangeException(nameof(clipSize));
            ClipSize = clipSize;
            MaxReserve = maxReserve;
            InClip = clipSize;
            Reserve = Math.Min(reserve, maxReserve);
        }

        public int ClipSize { get; }
        public int MaxReserve { get; }
        public int InClip { get; private set; }
        public int Reserve { get; private set; }

        public bool CanFire => InClip > 0;
        public int Total => InClip + Reserve;

        public ReloadKind PlanReload()
        {
            if (Reserve <= 0 || InClip == ClipSize) return ReloadKind.None;
            return InClip == 0 ? ReloadKind.Empty : ReloadKind.Partial;
        }

        // 한 발 쏜다. 마지막 발이면 클립이 튀어나간다(true).
        public bool Fire(out bool clipEjected)
        {
            clipEjected = false;
            if (InClip <= 0) return false;
            InClip--;
            clipEjected = InClip == 0;
            return true;
        }

        public void CompleteReload()
        {
            Reserve += InClip;
            int take = Math.Min(ClipSize, Reserve);
            InClip = take;
            Reserve -= take;
        }

        public void Refill()
        {
            Reserve = MaxReserve;
        }

        public void Reset(int reserve)
        {
            InClip = ClipSize;
            Reserve = Math.Min(reserve, MaxReserve);
        }
    }
}
