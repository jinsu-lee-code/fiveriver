using UnityEngine;

namespace FiveRivers.Combat
{
    // 납품 클립의 공통 알림 수신기. 현재 시연은 EnemyAgent/AudioDirector/NarrativeDirector가
    // 발사·음성 타이밍을 결정하므로 같은 클립 이벤트가 게임 판정이나 대사를 두 번 실행하지 않는다.
    // 캐릭터의 별도 발소리 연출을 연결할 때도 이 알림과 플레이어 발소리를 구분한다.
    public sealed class CharacterAnimationEvents : MonoBehaviour
    {
        public void Fire() { }
        public void Footstep() { }
        public void VoiceStart() { }
    }
}
