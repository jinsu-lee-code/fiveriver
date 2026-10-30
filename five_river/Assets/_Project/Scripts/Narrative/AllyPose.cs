using UnityEngine;

namespace FiveRivers.Narrative
{
    // 아군(AC_ROK) 시작 자세: 참호 아군은 Crouch → A_ROK_Idle_Trench, 새벽 보충병은 Speed → A_ROK_Walk_Tired (ANIM_SPEC 1-5).
    public class AllyPose : MonoBehaviour
    {
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int SpeedId = Animator.StringToHash("Speed");

        [SerializeField] Animator animator;
        [SerializeField] bool crouch;

        float speed;

        public void Walk(float metersPerSecond)
        {
            speed = metersPerSecond;
            Apply();
        }

        void Start() => Apply();

        void Apply()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.SetBool(CrouchId, crouch);
            animator.SetFloat(SpeedId, speed);
        }
    }
}
