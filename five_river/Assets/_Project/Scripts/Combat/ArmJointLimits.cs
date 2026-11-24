using System;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Keeps the bazooka's first-person wrists and forearms within what a human arm can do (2026-11-24, 사용자 지시
    // "물리법칙이나 인간의 관절구조상 절대 할 수 없는 모션은 고쳐").
    // BazookaPresentation places each hand on its grip (or on the rocket while reloading) and turns the hand bone to the grip's
    // rotation outright (M1ArmRig.Solve: wrist.rotation = target.rotation): the whole turn between the forearm and the grip sat in
    // the one wrist joint - its roll (the Tripo forearm has no twist bones) and its bend, unbounded. After it (and before the
    // fingers close, FirstPersonFingerCurl), for each arm:
    //  1. the forearm takes `twistShare` of the hand's roll about it (pronation / supination happen in the forearm), at most
    //     `maxForearmTwist` either way (M1ArmRig.ShareForearmTwist does the same for the M1 arms, 0.7 of it to 70 degrees);
    //  2. a wrist bent past flexion `maxFlexion` / extension `maxExtension` / radial `maxRadial` / ulnar `maxUlnar` is brought back
    //     to the limit by moving the elbow, not the hand: the forearm turns toward the hand's own line, the elbow follows, and the
    //     off-screen shoulder slides so the bone lengths still meet (as M1ArmRig.AlignLeftForearm does). The hand stays on its grip.
    // The neutral wrist is the rig's own bind pose (straight wrist), read from the arm mesh.
    [DefaultExecutionOrder(150)]
    public sealed class ArmJointLimits : MonoBehaviour
    {
        [Serializable]
        public sealed class Arm
        {
            public Transform upper, forearm, hand, target;
            public Transform index1, middle1, pinky1;
            public bool left;
            [NonSerialized] public bool ready;
            [NonSerialized] public Quaternion restRel;      // hand in the forearm's frame at rest
            [NonSerialized] public Vector2 rest;             // the wrist angles of the bind pose (subtracted: neutral = 0)
            [NonSerialized] public Quaternion written, before; [NonSerialized] public bool wrote;
            // check values, last frame (degrees): measured before the limit
            [NonSerialized] public float twist, forearmShare, handGaveWay; [NonSerialized] public Vector2 wrist, limited;
        }

        public Arm left = new Arm { left = true }, right = new Arm();
        public float maxFlexion = 80f, maxExtension = 70f, maxRadial = 20f, maxUlnar = 35f;
        // (2026-11-24, pass 2) Clamped only, the wrists sat on a limit in 40 % of the bazooka frames (the grips' turns with
        // BazookaPresentation's fixed elbow hint put the right wrist 65-166 degrees back): every frame the forearm is first turned
        // toward a natural grip - the wrist straight, the pistol hand 10 degrees toward the little finger as a fist on a pistol
        // grip is - by `preferWeight` of the way, then held within the limits.
        public Vector2 preferLeft = new Vector2(0f, 0f), preferRight = new Vector2(0f, -10f);
        [Range(0f, 1f)] public float preferWeight = 0.85f;
        // The forearm takes the whole roll (pronation in the forearm, the rest of a big turn in the shoulder, off screen): with 0.7
        // of it to 85 degrees, 34-94 degrees stayed in the wrist on reloading (measured in play 2026-11-24).
        public float maxForearmTwist = 170f;
        [Range(0f, 1f)] public float twistShare = 1f;
        [Tooltip("the shoulder stays below this height in the camera's frame (metres): it must not come into view")]
        public float shoulderBelow = -0.18f;
        public Transform cameraFrame;

        void LateUpdate()
        {
            Limit(left); Limit(right);
        }

        void Prepare(Arm a)
        {
            a.ready = false;
            if (a.upper == null || a.forearm == null || a.hand == null || a.target == null || a.middle1 == null || a.index1 == null || a.pinky1 == null) return;
            var skin = HandJointMath.SkinOf(this, a.hand);
            if (!HandJointMath.RestWorld(skin, a.forearm, out var fore) || !HandJointMath.RestWorld(skin, a.hand, out var hand)
                || !HandJointMath.RestWorld(skin, a.middle1, out var mid) || !HandJointMath.RestWorld(skin, a.index1, out var idx)
                || !HandJointMath.RestWorld(skin, a.pinky1, out var pk)) return;
            a.restRel = Quaternion.Inverse(fore.rotation) * hand.rotation;
            Vector3 P(Matrix4x4 m) => m.GetColumn(3);
            var f = HandJointMath.Frame(P(hand), P(mid), P(idx), P(pk), a.left);
            a.rest = HandJointMath.Wrist(f, P(hand) - P(fore));
            a.ready = true;
        }

        void Limit(Arm a)
        {
            if (!a.ready) Prepare(a);
            if (!a.ready) return;
            // a forearm the Animator did not write this frame still holds last frame's roll: back to what it had
            if (a.wrote && a.forearm.localRotation == a.written) a.forearm.localRotation = a.before;
            a.before = a.forearm.localRotation;
            // the bend first (it does not depend on the forearm's roll), then the roll
            var f = HandJointMath.Frame(a.hand.position, a.middle1.position, a.index1.position, a.pinky1.position, a.left);
            var w = HandJointMath.Wrist(f, a.hand.position - a.forearm.position) - a.rest;
            a.wrist = w;
            var goal = Vector2.Lerp(w, a.left ? preferLeft : preferRight, preferWeight);
            var c = new Vector2(Mathf.Clamp(goal.x, -maxExtension, maxFlexion), Mathf.Clamp(goal.y, -maxUlnar, maxRadial));
            a.limited = c;
            if ((c - w).sqrMagnitude > 0.25f)
            {
                var wristPos = a.hand.position; var handRot = a.hand.rotation;
                float fore = Vector3.Distance(a.forearm.position, wristPos), up = Vector3.Distance(a.upper.position, a.forearm.position);
                var shoulder0 = a.upper.position;
                // as much of the correction as keeps the shoulder out of view (all of it, half, a quarter)
                for (float k = 1f; k >= 0.24f; k *= 0.5f)
                {
                    var want = HandJointMath.ForearmFor(f, Vector2.Lerp(w, c, k) + a.rest);
                    var elbow = wristPos - want * fore;
                    var shoulder = elbow + (shoulder0 - elbow).normalized * up;
                    if (cameraFrame != null && cameraFrame.InverseTransformPoint(shoulder).y > shoulderBelow && k > 0.25f) continue;
                    // (pass 3) nor the elbow and its sleeve (6 cm round it) into the 70-degree, 16:9 view: moved toward the hand's line
                    // with the longer arms of armScale 1.9, the left elbow came up into the lower left when shouldered
                    if (cameraFrame != null && InView(cameraFrame.InverseTransformPoint(elbow), 0.06f) && k > 0.25f) continue;
                    a.upper.position = shoulder;
                    M1ArmRig.Solve(a.upper, a.forearm, a.hand, a.target, false, elbow);
                    break;
                }
                a.hand.rotation = handRot;
                // (2026-11-24, pass 3) Where the shoulder may not move far enough (drawing, reloading: the right wrist stayed 110
                // back and 99 to the side, measured in play), the hand gives way instead: it turns about the wrist until the wrist is
                // within its range, leaving the grip a little for that moment - better than a wrist bent past what one can do.
                var f2 = HandJointMath.Frame(a.hand.position, a.middle1.position, a.index1.position, a.pinky1.position, a.left);
                var forearmDir = (a.hand.position - a.forearm.position).normalized;
                var w2 = HandJointMath.Wrist(f2, forearmDir) - a.rest;
                var c2 = new Vector2(Mathf.Clamp(w2.x, -maxExtension, maxFlexion), Mathf.Clamp(w2.y, -maxUlnar, maxRadial));
                if ((c2 - w2).sqrMagnitude > 0.25f)
                {
                    // the turn that carries the forearm direction the limit wants (in the hand's frame) onto the forearm as it is
                    var wanted = HandJointMath.ForearmFor(f2, c2 + a.rest);
                    a.hand.rotation = Quaternion.FromToRotation(wanted, forearmDir) * a.hand.rotation;
                    a.handGaveWay = Vector2.Distance(c2, w2);
                }
                else a.handGaveWay = 0f;
            }
            else a.handGaveWay = 0f;
            ShareTwist(a);
            a.written = a.forearm.localRotation; a.wrote = true;
        }

        static bool InView(Vector3 p, float margin)
        {
            const float TanHalfV = 0.7002f, TanHalfH = 1.2449f;   // tan 35 deg, and the matching half width at 16:9
            if (p.z < 0.01f) return false;
            return p.y > -(TanHalfV * p.z + margin) && Mathf.Abs(p.x) < TanHalfH * p.z + margin;
        }

        // the forearm rolls with the hand (up to the limit), the hand keeps its turn on the grip
        void ShareTwist(Arm a)
        {
            var axis = (a.hand.position - a.forearm.position).normalized;
            var handRot = a.hand.rotation;
            var delta = handRot * Quaternion.Inverse(a.forearm.rotation * a.restRel);
            float tw = M1ArmRig.TwistDegrees(delta, axis);
            float share = Mathf.Clamp(tw * twistShare, -maxForearmTwist, maxForearmTwist);
            a.forearm.rotation = Quaternion.AngleAxis(share, axis) * a.forearm.rotation;
            a.hand.rotation = handRot;
            a.twist = tw; a.forearmShare = share;
        }
    }
}
