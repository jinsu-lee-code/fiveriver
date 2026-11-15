using UnityEngine;

namespace FiveRivers.Narrative
{
    // Several copies of one character standing in a row (the four student soldiers at dawn) moved in perfect
    // unison: same frame of the same idle, same height. Each copy plays its idle at a slightly
    // different speed and carries its head a little differently (the spawner already staggers where each starts) (2026-11-15).
    public sealed class IdleVariation : MonoBehaviour
    {
        [SerializeField] float speed = 1f;
        [SerializeField] Vector3 headTurn;   // degrees, added to the idle's head pose
        Animator animator;
        Transform head;

        public void Set(float playSpeed, Vector3 turn)
        {
            speed = playSpeed; headTurn = turn;
            if (animator != null) animator.speed = speed;
        }

        void OnEnable()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            animator.speed = speed;
        }

        void LateUpdate()
        {
            if (head != null) head.localRotation *= Quaternion.Euler(headTurn);
        }
    }
}
