using UnityEngine;
namespace FiveRivers.Combat
{
    // Opt-in fit for the regular uniform body. Other avatars retain their authored correction.
    [DisallowMultipleComponent]
    public sealed class AlliedRifleFit : MonoBehaviour
    {
        public static readonly Vector3 ParkRightGrip = new Vector3(.048f, -.047f, -.176f);
        public static readonly Vector3 ParkLeftGrip = new Vector3(-.065f, -.037f, .205f);
        public static readonly Vector3 ParkAimedLeftGrip = new Vector3(-.065f, -.035f, .192f);

        public static bool IsParkAvatar(Animator animator) =>
            animator != null && animator.avatar != null && animator.avatar.name == "SK_CH_ROK_ParkYongpalAvatar";

        public bool correctShoulderedHands = true;
        public bool correctCarryPose;
        // Scene opt-in for Ally_3 only: the stand clip leaves his rifle muzzle-up and chin raised.
        public bool correctStandingReady;
        public Vector3 standingReadyOffset;
        public Vector3 standingReadySupportGrip = new Vector3(-.055f, -.05f, .14f);
        // Explicitly enabled only on the three regular allies in Dabudong.
        public bool refineSupportGrip;
        // Authored only on the regular allies; other avatars keep the original gaze.
        [Range(0f, 12f)] public float shoulderedHeadPitch;
        [Range(-12f, 12f)] public float shoulderedHeadLean;
        public Vector3 shoulderOffset = new Vector3(.025f, -.015f, .025f);
        public Vector3 rightElbowDirection = new Vector3(.35f, -.4f, -.05f);
        public Vector3 leftElbowDirection = new Vector3(-.25f, -.45f, .12f);
        [Range(0,90)] public float maxForearmRoll = 45f;
        [Range(0,90)] public float maxWristSwing = 35f;
        // Wrist origins sit beside/below the wood; the palm, rather than the wrist bone, supports it.
        public Vector3 supportWristGrip = new Vector3(-.04f, -.032f, .22f);
        public Vector3 triggerWristGrip = new Vector3(.025f, -.05f, -.185f);
        // 일반 국군의 손바닥은 방아쇠울 뒤의 목재 옆면을 잡는다.
        public Vector3 ShoulderedTriggerWristGrip => refineSupportGrip
            ? triggerWristGrip + new Vector3(.006f, -.002f, -.038f)
            : triggerWristGrip;
        public Vector3 supportPalmDirection = new Vector3(1f, .35f, .1f);
        public Vector3 carryPosition = new Vector3(.04f, 1.08f, .20f);
        public Vector3 carryEuler = new Vector3(-3.2f, -31.9f, 0f);
    }
}

