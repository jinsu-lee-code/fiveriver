using UnityEngine;

namespace FiveRivers.Combat
{
    // Anatomical joint angles of the first-person hands (2026-11-24, 사용자 지시 "물리법칙이나 인간의 관절구조상 절대 할 수 없는
    // 모션은 고쳐"). Shared by ArmJointLimits (the limit at run time) and the check tool.
    //  - wrist: the forearm's direction seen from the hand. In the hand's frame (h along the hand to the middle knuckle, p out of
    //    the palm, r toward the thumb) a wrist flexed by f has the forearm tilted toward the back of the hand: flex = atan2(-a.p, a.h);
    //    deviated toward the thumb by d: radial = atan2(-a.r, a.h). Extension and ulnar deviation are the negative sides.
    //  - human range used (the coordinator's figures): flexion 80, extension 70, radial 20, ulnar 35, forearm turn +-85 degrees.
    public static class HandJointMath
    {
        public struct HandFrame { public Vector3 h, p, r; }

        // The hand's frame from its bones. Palm side as FirstPersonFingerCurl measured it in play 2026-11-23: (index knuckle -
        // wrist) x (little knuckle - wrist) points out of the palm of the left hand and out of the back of the right one.
        public static HandFrame Frame(Vector3 wrist, Vector3 middle1, Vector3 index1, Vector3 pinky1, bool left)
        {
            var h = (middle1 - wrist).normalized;
            var p = Vector3.Cross(index1 - wrist, pinky1 - wrist).normalized * (left ? 1f : -1f);
            p = Vector3.ProjectOnPlane(p, h).normalized;
            // toward the thumb: the index side of the knuckle line
            var r = Vector3.ProjectOnPlane(index1 - pinky1, h);
            r = Vector3.ProjectOnPlane(r, p).normalized;
            return new HandFrame { h = h, p = p, r = r };
        }

        // (flexion +, extension -), (radial +, ulnar -), degrees; forearm = elbow -> wrist
        public static Vector2 Wrist(in HandFrame f, Vector3 forearm)
        {
            var a = forearm.normalized;
            float ah = Vector3.Dot(a, f.h), ap = Vector3.Dot(a, f.p), ar = Vector3.Dot(a, f.r);
            return new Vector2(Mathf.Atan2(-ap, ah) * Mathf.Rad2Deg, Mathf.Atan2(-ar, ah) * Mathf.Rad2Deg);
        }

        // the forearm direction that gives these wrist angles (|angles| < 85)
        public static Vector3 ForearmFor(in HandFrame f, Vector2 angles)
        {
            float tf = Mathf.Tan(Mathf.Clamp(angles.x, -85f, 85f) * Mathf.Deg2Rad), td = Mathf.Tan(Mathf.Clamp(angles.y, -85f, 85f) * Mathf.Deg2Rad);
            return (f.h - f.p * tf - f.r * td).normalized;
        }

        // A finger joint's turn from its rest (bind-pose) local rotation, in the rest frame: bend toward the palm about `flexAxis`
        // (+ = toward the palm), sideways about the third axis, twist about the bone. Axes in the joint's own (rest) space.
        public static Vector3 FingerAngles(Quaternion restLocal, Quaternion local, Vector3 boneAxis, Vector3 flexAxis)
        {
            var delta = Quaternion.Inverse(restLocal) * local;          // in the joint's rest frame
            float twist = M1ArmRig.TwistDegrees(delta, boneAxis);
            var swing = delta * Quaternion.Inverse(Quaternion.AngleAxis(twist, boneAxis));
            var moved = swing * boneAxis;
            var side = Vector3.Cross(boneAxis, flexAxis).normalized;    // the plane the joint bends in is (boneAxis, side)
            // bend: the bone turned about flexAxis (toward -side when positive, as AngleAxis turns boneAxis)
            var turnedByFlex = Quaternion.AngleAxis(10f, flexAxis) * boneAxis;
            float sign = Vector3.Dot(turnedByFlex - boneAxis, side) >= 0f ? 1f : -1f;
            float flex = Mathf.Atan2(sign * Vector3.Dot(moved, side), Vector3.Dot(moved, boneAxis)) * Mathf.Rad2Deg;
            float abd = Mathf.Asin(Mathf.Clamp(Vector3.Dot(moved, flexAxis), -1f, 1f)) * Mathf.Rad2Deg;
            return new Vector3(flex, abd, twist);
        }

        // rest (bind pose) local rotation of a bone in a skinned renderer: parent^-1 * bone, both from the mesh's bind poses
        public static bool RestLocal(SkinnedMeshRenderer skin, Transform bone, out Quaternion rest)
        {
            rest = Quaternion.identity;
            if (skin == null || skin.sharedMesh == null || bone == null || bone.parent == null) return false;
            var bones = skin.bones; var bind = skin.sharedMesh.bindposes;
            int i = System.Array.IndexOf(bones, bone), p = System.Array.IndexOf(bones, bone.parent);
            if (i < 0 || p < 0 || i >= bind.Length || p >= bind.Length) return false;
            rest = (bind[p] * bind[i].inverse).rotation;     // (restWorld_p)^-1 * restWorld_i, restWorld = bind^-1
            return true;
        }

        public static bool RestWorld(SkinnedMeshRenderer skin, Transform bone, out Matrix4x4 rest)
        {
            rest = Matrix4x4.identity;
            if (skin == null || skin.sharedMesh == null || bone == null) return false;
            int i = System.Array.IndexOf(skin.bones, bone);
            if (i < 0 || i >= skin.sharedMesh.bindposes.Length) return false;
            rest = skin.sharedMesh.bindposes[i].inverse; return true;
        }

        public static SkinnedMeshRenderer SkinOf(Component root, Transform bone)
        {
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && System.Array.IndexOf(r.bones, bone) >= 0) return r;
            return null;
        }
    }
}
