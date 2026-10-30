using System;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 적 · 아군 · 1인칭 클립의 애니메이션 이벤트(ANIM_SPEC 1-6)를 받는다. Animator와 같은 오브젝트에 둔다.
    // 소리 · 대사는 지금 코드가 시간으로 처리하므로 여기서는 알리기만 한다 (받는 곳이 없으면 Unity가 오류를 낸다).
    public sealed class AnimationEventSink : MonoBehaviour
    {
        public event Action<string> Raised;

        void Footstep() => Raised?.Invoke(nameof(Footstep));
        void Fire() => Raised?.Invoke(nameof(Fire));
        void VoiceStart() => Raised?.Invoke(nameof(VoiceStart));
        void ClipEject() => Raised?.Invoke(nameof(ClipEject));
        void ClipRemove() => Raised?.Invoke(nameof(ClipRemove));
        void ClipInsert() => Raised?.Invoke(nameof(ClipInsert));
        void BoltRelease() => Raised?.Invoke(nameof(BoltRelease));
        void ReloadDone() => Raised?.Invoke(nameof(ReloadDone));
        void RocketInsert() => Raised?.Invoke(nameof(RocketInsert));
        void EquipDone() => Raised?.Invoke(nameof(EquipDone));
        void UnequipDone() => Raised?.Invoke(nameof(UnequipDone));
        void AmmoAdded() => Raised?.Invoke(nameof(AmmoAdded));
    }
}
