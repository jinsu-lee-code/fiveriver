using UnityEngine;

namespace FiveRivers.Narrative
{
    // A man looks down at what lies at his feet (2026-11-18): Park stood over the covered body with his head up,
    // looking straight ahead like a sentry. The neck and head are tipped forward after the animator has posed him,
    // about the body's own right axis, so it works whatever the bones' local axes are. Presentation only.
    [DefaultExecutionOrder(200)]
    public sealed class HeadBow : MonoBehaviour
    {
        public static float Degrees = 24f, Seconds = 1.6f, StartWeight = 0.3f;

        Transform body, head, neck;
        float weight;
        // per use (2026-11-18: the squad ducking under the barrage bows deeper and faster than Park over the body)
        float degrees = -1f, seconds = -1f, releaseFor = -1f;

        public static HeadBow On(Transform root, float degrees, float seconds)
        {
            var bow = On(root);
            if (bow == null) return null;
            bow.degrees = degrees;
            bow.seconds = seconds;
            bow.releaseFor = -1f;
            return bow;
        }

        // Lifts the head again over the given time, then removes itself.
        public void Release(float over) => releaseFor = Mathf.Max(0.05f, over);

        public static HeadBow On(Transform root)
        {
            var animator = root.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return null;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return null;
            var bow = animator.GetComponent<HeadBow>();
            if (bow == null) bow = animator.gameObject.AddComponent<HeadBow>();
            bow.body = root;
            bow.head = head;
            bow.neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            bow.weight = StartWeight;
            return bow;
        }

        void LateUpdate()
        {
            if (head == null || body == null) return;
            if (releaseFor > 0f)
            {
                weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime / releaseFor);
                if (weight <= 0f) { Destroy(this); return; }
            }
            else weight = Mathf.MoveTowards(weight, 1f, Time.deltaTime / Mathf.Max(0.1f, seconds > 0f ? seconds : Seconds));
            float angle = (degrees > 0f ? degrees : Degrees) * Mathf.SmoothStep(0f, 1f, weight);
            var axis = body.right; // about his own right: positive tips the face down
            if (neck != null) neck.rotation = Quaternion.AngleAxis(angle * 0.4f, axis) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(angle * 0.6f, axis) * head.rotation;
        }
    }
}
