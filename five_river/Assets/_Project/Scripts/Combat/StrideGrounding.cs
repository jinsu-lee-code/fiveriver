using UnityEngine;

namespace FiveRivers.Combat
{
    // The riflemen's moving clips (Run_Rifle, CrouchRun_Rifle) carry the hips higher than their legs reach on these
    // rigs: measured 2026-11-17 frame by frame, the foot on the ground stayed 6-12 cm above the trench floor for the
    // whole run (toe joint 0.11-0.17 m up against 0.05 m standing), so the men ran on air. While he is moving, the
    // lowest point either toe reaches in a stride is followed, and the hips are set down (or lifted, on a rising
    // slope where the leading foot went under the ground) by the difference to the height the toe has when he
    // stands. Vertical only: stride length and foot speed are untouched. Runs before AllyCombatant.LateUpdate so
    // the rifle follows the lowered hands. Presentation only.
    [DefaultExecutionOrder(-60)]
    public sealed class StrideGrounding : MonoBehaviour
    {
        // seconds of running looked at (a stride of either foot is 0.35 s), how fast the offset moves (m/s), its limits
        // (the first look after he steps off is shorter, so he is not left in the air for his first strides)
        const float Window = 0.42f, FirstWindow = 0.24f, Rate = 0.8f, MaxDown = 0.22f, MaxUp = 0.10f;
        const float RestLow = 0.03f, RestHigh = 0.07f;
        // Coming up out of the trench (0.9 m in 0.8 s) the window still held what the ramp had taught (lift him: the
        // leading toe went into the rising ground), and on the level he ran 15-20 cm over the field for half a second
        // with his knees up (filmed 2026-11-17 evening). When the body's height has been changing faster than
        // SlopeRate and then runs level for LevelAfter, the window starts afresh (short), from the offset last
        // learned on level ground.
        const float SlopeRate = 0.3f, LevelAfter = 0.08f;

        // Set by whoever moves him (HillAllies): true while he is stepping.
        public bool Active { get; set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float Offset => offset;
#endif

        Transform body, hips, leftToes, rightToes;
        float rest = 0.05f, offset, target, learned, windowLow = float.MaxValue, windowTime, window = FirstWindow;
        bool hasLearned, wasActive;
        float lastBodyY, levelTime, levelLearned;
        bool onSlope, hasLevel;
        Vector3 written;
        bool wrote;

        public static StrideGrounding Attach(Animator animator, Transform bodyRoot)
        {
            if (animator == null || !animator.isHuman) return null;
            var stride = animator.GetComponent<StrideGrounding>();
            if (stride == null) stride = animator.gameObject.AddComponent<StrideGrounding>();
            stride.body = bodyRoot;
            stride.hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            stride.leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            stride.rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (stride.leftToes == null) stride.leftToes = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (stride.rightToes == null) stride.rightToes = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (stride.hips == null || stride.leftToes == null || stride.rightToes == null) { stride.enabled = false; return stride; }
            // how high the toe joint sits when the foot is on the ground: he is standing or kneeling at his post now
            float low = Mathf.Min(stride.Gap(stride.leftToes.position), stride.Gap(stride.rightToes.position));
            stride.rest = Mathf.Clamp(low, RestLow, RestHigh);
            stride.enabled = true;
            return stride;
        }

        void LateUpdate()
        {
            if (hips == null || body == null) return;
            // The animator did not pose him this frame (culled, paused): the bones still hold what was written last
            // frame, so writing again would add up.
            if (wrote && hips.localPosition == written) return;
            wrote = false;
            if (Active)
            {
                if (!wasActive)
                {
                    windowLow = float.MaxValue; windowTime = 0f; window = FirstWindow; target = hasLearned ? learned : 0f;
                    lastBodyY = body.position.y; onSlope = false; levelTime = 0f;
                }
                float climb = Time.deltaTime > 0f ? Mathf.Abs(body.position.y - lastBodyY) / Time.deltaTime : 0f;
                lastBodyY = body.position.y;
                if (climb > SlopeRate) { onSlope = true; levelTime = 0f; }
                else if (onSlope && (levelTime += Time.deltaTime) >= LevelAfter)
                {
                    onSlope = false;
                    windowLow = float.MaxValue; windowTime = 0f; window = FirstWindow;
                    target = learned = hasLevel ? levelLearned : 0f;
                }
                windowLow = Mathf.Min(windowLow, Mathf.Min(Gap(leftToes.position), Gap(rightToes.position)));
                windowTime += Time.deltaTime;
                if (windowTime >= window)
                {
                    window = Window;
                    target = learned = Mathf.Clamp(rest - windowLow, -MaxDown, MaxUp);
                    hasLearned = true;
                    if (!onSlope) { levelLearned = learned; hasLevel = true; }
                    windowLow = float.MaxValue;
                    windowTime = 0f;
                }
            }
            else target = 0f;
            wasActive = Active;
            offset = Mathf.MoveTowards(offset, target, Rate * Time.deltaTime);
            if (Mathf.Abs(offset) < 0.0005f) return;
            hips.position += Vector3.up * offset;
            written = hips.localPosition;
            wrote = true;
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        // Height of a point over the ground he stands on: of the surfaces under it, the one nearest his own feet
        // (a sandbag wall or a truck beside him is not the ground).
        float Gap(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.7f, Vector3.down, hits, 2.2f, FiveRivers.Core.WorldQuery.Solid, QueryTriggerInteraction.Ignore);
            float best = body.position.y, bestGap = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(body) || c.attachedRigidbody != null || c.GetComponentInParent<CharacterController>() != null
                    || c.GetComponentInParent<AllyCombatant>() != null || c.GetComponentInParent<EnemyAgent>() != null) continue;
                float gap = Mathf.Abs(hits[i].point.y - body.position.y);
                if (gap < bestGap) { bestGap = gap; best = hits[i].point.y; }
            }
            return p.y - best;
        }
    }
}
