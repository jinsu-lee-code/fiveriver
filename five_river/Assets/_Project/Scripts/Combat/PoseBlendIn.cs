using UnityEngine;

namespace FiveRivers.Combat
{
    // Eases a character out of the pose he is in when something else takes over his Animator (a PlayableGraph clip):
    // the pose is kept as it was on the frame this is attached, and for 'duration' seconds every bone is turned from
    // that pose toward what the new animation plays. Then it removes itself.
    // Made for Choi Man-su's fall (2026-11-15): the fall clip was cross-faded with a fresh copy of his controller,
    // which starts in its default standing state, so the kneeling man sprang upright before he went down.
    [DefaultExecutionOrder(-50)] // before LyingBodyRest and the hand placement
    public sealed class PoseBlendIn : MonoBehaviour
    {
        Transform[] bones;
        Quaternion[] from;
        Vector3 hipsFrom;
        Transform hips;
        float duration, time;

        public static PoseBlendIn Attach(Animator animator, float seconds, Transform skip = null)
        {
            var blend = animator.gameObject.AddComponent<PoseBlendIn>();
            var all = animator.GetComponentsInChildren<Transform>();
            var list = new System.Collections.Generic.List<Transform>(all.Length);
            foreach (var t in all)
            {
                if (t == animator.transform || (skip != null && t.IsChildOf(skip))) continue;
                list.Add(t);
            }
            blend.bones = list.ToArray();
            blend.from = new Quaternion[blend.bones.Length];
            for (int i = 0; i < blend.bones.Length; i++) blend.from[i] = blend.bones[i].localRotation;
            blend.hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (blend.hips != null) blend.hipsFrom = blend.hips.localPosition;
            blend.duration = Mathf.Max(0.01f, seconds);
            return blend;
        }

        void LateUpdate()
        {
            time += Time.deltaTime;
            float w = Mathf.SmoothStep(0f, 1f, time / duration);
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) bones[i].localRotation = Quaternion.Slerp(from[i], bones[i].localRotation, w);
            if (hips != null) hips.localPosition = Vector3.Lerp(hipsFrom, hips.localPosition, w);
            if (time >= duration) Destroy(this);
        }
    }
}
