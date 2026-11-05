using UnityEngine;
namespace FiveRivers.Combat
{
    // Opt-in fit for the regular uniform body. Other avatars retain their authored correction.
    [DisallowMultipleComponent]
    public sealed class AlliedRifleFit : MonoBehaviour
    {
        public bool correctShoulderedHands = true;
        public bool correctCarryPose;
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
        public Vector3 supportPalmDirection = new Vector3(1f, .35f, .1f);
        public Vector3 carryPosition = new Vector3(.04f, 1.08f, .20f);
        public Vector3 carryEuler = new Vector3(-3.2f, -31.9f, 0f);
    }
}

