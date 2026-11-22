using UnityEngine;

namespace FiveRivers.Combat
{
    // Puts the KPA rifleman's hands on the rifle he is carrying. The clips hold the hands for a shorter weapon,
    // so after EnemyAgent lines the Mosin up with the hands the left palm could hover up to 10 cm off it.
    // Lives on the Animator object (OnAnimatorIK is only sent there); EnemyAgent adds it and supplies the rifle.
    //
    // 2026-11-22: the hands hold it as solved offline (EnemyGrips; source/characters/kpa_hybrid/grip). Until then
    // every frame searched for a grip from scratch - palm pushed out of the wood, each finger bent until something
    // touched - which cost up to 2 ms a man and fell apart whenever the hand's shape changed (thumb in the air,
    // fingers hanging open). Now each frame only blends between the holds of the poses he is between, the arms reach
    // for the wrists and the finger joints are set.
    public sealed class EnemyHandIK : MonoBehaviour
    {
        public static float Weight = 0.85f, Below = 0.03f;
        Animator animator;
        Transform rifle;
        Vector3 axisLocalStart, axisLocalEnd;
        Transform leftHand, rightHand, leftUpper, leftFore, rightUpper, rightFore;
        Transform[] leftJoints, rightJoints;   // thumb, index, middle, ring, little: three each
        public bool Holding { get; set; } = true;
        // where the clip put the left hand this frame, before IK (EnemyAgent aims the rifle from this)
        public Vector3 AnimatedLeftHand { get; private set; }
        public bool HasAnimatedHand { get; private set; }

        public void Bind(Animator owner, Transform weapon, Vector3 centreLocal, float behind, float ahead)
        {
            animator = owner;
            rifle = weapon;
            axisLocalStart = centreLocal + Vector3.back * behind;
            axisLocalEnd = centreLocal + Vector3.forward * ahead;
            if (!animator.isHuman) return;
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftJoints = Joints(HumanBodyBones.LeftThumbProximal);
            rightJoints = Joints(HumanBodyBones.RightThumbProximal);
        }

        // the enum runs thumb, index, middle, ring, little, proximal to distal
        Transform[] Joints(HumanBodyBones first)
        {
            var found = new Transform[15];
            for (int i = 0; i < found.Length; i++) found[i] = animator.GetBoneTransform(first + i);
            return found;
        }

        // Feet on a slope (2026-11-13, "다리 질질끌면서 내려오는 부분"): the clips lift the feet over flat ground, so
        // running down the hillside the rear foot (uphill, where the ground stands higher) went through the slope
        // and seemed to drag. Each foot is moved up or down by how much the ground under it differs from the ground
        // under the body, and turned a little to lie with the slope. EnemyAgent turns it off when he dies.
        public bool FeetOnSlope { get; set; }
        public static float FootTilt = 0.6f, MaxFootShift = 0.45f;
        public static bool SlopeFeet = true; // off = feet as the clip puts them (debug / comparison)
        // Set by EnemyAgent: how much of a kneeling state is on, and the ankle height over the sole. Kneeling, the
        // body is lowered onto the knee, so the planted foot is kept from going down into the ground.
        public float Kneel { get; set; }
        public float SoleHeight { get; set; } = 0.08f;
        Terrain ground;

        void PlaceFeet()
        {
            if (ground == null) ground = Terrain.activeTerrain;
            if (ground == null) return;
            var root = animator.transform.position;
            float under = Ground(root);
            foreach (var goal in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
            {
                var p = animator.GetIKPosition(goal);
                float shift = Mathf.Clamp(Ground(p) - under, -MaxFootShift, MaxFootShift);
                var placed = p + Vector3.up * shift;
                if (Kneel > 0f) placed.y = Mathf.Lerp(placed.y, Mathf.Max(placed.y, Ground(p) + SoleHeight), Kneel);
                animator.SetIKPosition(goal, placed);
                animator.SetIKPositionWeight(goal, 1f);
                var normal = ground.terrainData.GetInterpolatedNormal(
                    (p.x - ground.transform.position.x) / ground.terrainData.size.x, (p.z - ground.transform.position.z) / ground.terrainData.size.z);
                animator.SetIKRotation(goal, Quaternion.FromToRotation(Vector3.up, normal) * animator.GetIKRotation(goal));
                animator.SetIKRotationWeight(goal, FootTilt);
            }
        }

        float Ground(Vector3 p) => ground.SampleHeight(p) + ground.transform.position.y;

        void OnAnimatorIK(int layer)
        {
            if (animator != null)
            {
                if (FeetOnSlope && SlopeFeet) PlaceFeet();
                else
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f); animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f); animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                }
            }
            if (animator == null || rifle == null || !Holding)
            {
                if (animator != null) animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                return;
            }
            var hand = animator.GetIKPosition(AvatarIKGoal.LeftHand);
            AnimatedLeftHand = hand;
            HasAnimatedHand = true;
            var a = rifle.TransformPoint(axisLocalStart);
            var b = rifle.TransformPoint(axisLocalEnd);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(hand - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            var onStock = a + ab * t - rifle.up * Below; // palm under the stock
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, Weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, onStock);
        }

        // Off = the hands are left as the clip and the IK pass put them (how it was before 2026-11-10).
        public static bool ShapeHands = true;

        bool Shaped => ShapeHands && leftHand != null && rightHand != null && leftUpper != null && leftFore != null
            && rightUpper != null && rightFore != null && leftJoints != null && rightJoints != null;

        // Set by EnemyAgent before Shape: how far the rifle is into the shoulder.
        public float Shoulder { get; set; }
        // How much of the shouldering is over before the forefinger leaves the side of the stock for the trigger.
        public static float FingerToTriggerFrom = 0.6f;
        // Kneeling, his left shoulder moves a few centimetres from man to man and frame to frame: where the arm falls
        // short, the hand takes the fore-end that much nearer (its section is the same all along there).
        public static float SlideMost = 0.1f;
        // Between under the magazine and any hold on the fore-end the hand drops this far off the rifle on the way
        // (straight across, its fingers went 14 mm through the magazine).
        public static float MagazineClear = 0.035f;
        public float SupportWristBend { get; private set; }   // degrees between forearm and hand (check values)
        public float TriggerWristBend { get; private set; }
        public float SupportShort { get; private set; }       // how far each wrist stayed from its hold, metres (check values)
        public float TriggerShort { get; private set; }

        static readonly Unity.Profiling.ProfilerMarker ShapeMarker = new Unity.Profiling.ProfilerMarker("EnemyHandIK.Shape");

        // Call in LateUpdate, after the rifle has been placed.
        // aim: 1 when the rifle is in the aim (cover, firing), 0 while it is carried running
        // low: 1 while the rifle is carried low across the body (crouched run)
        public void Shape(float weight, float aim = 0f, float low = 0f)
        {
            using (ShapeMarker.Auto()) ShapeCore(weight, aim, low);
        }

        void ShapeCore(float weight, float aim, float low)
        {
            if (weight <= 0f || !Holding || rifle == null || !Shaped) return;
            weight = Mathf.Clamp01(weight); aim = Mathf.Clamp01(aim); low = Mathf.Clamp01(low);
            float kneel = Mathf.Clamp01(Kneel);
            float shouldered = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Shoulder));
            float onTrigger = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FingerToTriggerFrom, 1f, shouldered));
            var body = animator.transform;

            // trigger hand: carried, or in the shoulder
            EnemyGrips.Hold carried = EnemyGrips.RightCarry, aimed = EnemyGrips.RightAim;
            var rightAt = rifle.TransformPoint(Vector3.Lerp(carried.Wrist, aimed.Wrist, shouldered));
            var rightTurn = rifle.rotation * Quaternion.Slerp(carried.Turn, aimed.Turn, shouldered);
            var rightElbow = body.TransformDirection(Vector3.Slerp(carried.Elbow, aimed.Elbow, shouldered));
            TriggerShort = Place(rightUpper, rightFore, rightHand, rightAt, rightTurn, rightElbow, weight);
            for (int i = 0; i < rightJoints.Length; i++)
            {
                var joint = rightJoints[i];
                if (joint == null) continue;
                bool index = i >= 3 && i < 6;
                var turn = Quaternion.Slerp(carried.Fingers[i], aimed.Fingers[i], index ? onTrigger : shouldered);
                joint.localRotation = weight < 1f ? Quaternion.Slerp(joint.localRotation, turn, weight) : turn;
            }

            // support hand: under the magazine standing in the aim, on the fore-end kneeling and on the move
            EnemyGrips.Hold run = EnemyGrips.LeftRun, stand = EnemyGrips.LeftStand, knee = EnemyGrips.LeftKneel, across = EnemyGrips.LeftLow;
            var leftAt = rifle.TransformPoint(Vector3.Lerp(Vector3.Lerp(run.Wrist, Vector3.Lerp(stand.Wrist, knee.Wrist, kneel), aim), across.Wrist, low));
            float underMagazine = aim * (1f - kneel) * (1f - low);
            leftAt -= rifle.up * (MagazineClear * Mathf.Sqrt(Mathf.Clamp01(4f * underMagazine * (1f - underMagazine))));
            var leftTurn = rifle.rotation * Mix(run.Turn, stand.Turn, knee.Turn, across.Turn, aim, kneel, low);
            var leftElbow = body.TransformDirection(Vector3.Lerp(Vector3.Lerp(run.Elbow, Vector3.Lerp(stand.Elbow, knee.Elbow, kneel), aim), across.Elbow, low));
            SupportShort = Place(leftUpper, leftFore, leftHand, SlideIntoReach(leftAt), leftTurn, leftElbow, weight);
            for (int i = 0; i < leftJoints.Length; i++)
            {
                var joint = leftJoints[i];
                if (joint == null) continue;
                var turn = Mix(run.Fingers[i], stand.Fingers[i], knee.Fingers[i], across.Fingers[i], aim, kneel, low);
                joint.localRotation = weight < 1f ? Quaternion.Slerp(joint.localRotation, turn, weight) : turn;
            }

            TriggerWristBend = Bend(rightFore, rightHand, rightJoints[6]);
            SupportWristBend = Bend(leftFore, leftHand, leftJoints[6]);
        }

        static Quaternion Mix(Quaternion run, Quaternion stand, Quaternion knee, Quaternion across, float aim, float kneel, float low)
        {
            return Quaternion.Slerp(Quaternion.Slerp(run, Quaternion.Slerp(stand, knee, kneel), aim), across, low);
        }

        static float Place(Transform upper, Transform fore, Transform hand, Vector3 at, Quaternion turn, Vector3 elbow, float weight)
        {
            turn = Quaternion.Slerp(hand.rotation, turn, weight);
            at = Vector3.Lerp(hand.position, at, weight);
            Reach(upper, fore, hand, at, elbow);
            hand.rotation = turn;
            return Vector3.Distance(hand.position, at);
        }

        Vector3 SlideIntoReach(Vector3 at)
        {
            float arm = Vector3.Distance(leftUpper.position, leftFore.position) + Vector3.Distance(leftFore.position, leftHand.position) - 0.002f;
            var from = at - leftUpper.position;
            float over = from.sqrMagnitude - arm * arm;
            if (over <= 0f) return at;
            var along = rifle.forward;
            float b = Vector3.Dot(from, along), root = b * b - over;
            float back = root >= 0f ? b - Mathf.Sqrt(root) : b;   // (never in reach: as near as the fore-end comes)
            return at - along * Mathf.Clamp(back, 0f, SlideMost);
        }

        static float Bend(Transform fore, Transform hand, Transform middle)
        {
            return middle == null ? 0f : Vector3.Angle(hand.position - fore.position, middle.position - hand.position);
        }

        // Crouched run (2026-11-15): the clip keeps the trigger hand close in against the chest, so a rifle carried at
        // the low ready from that hand had its butt behind the shoulder, outside the arm. The trigger wrist is put
        // where it must be for the butt to pass under the forearm by the hip: 'offset' is in the agent's own axes, from
        // the shoulder joint. Call in LateUpdate before the rifle is placed from the hand.
        public static Vector3 CarryElbow = new Vector3(0.25f, -0.5f, -0.8f);
        public void ReachTrigger(Vector3 offset, float weight)
        {
            if (weight <= 0f || !Holding || rightUpper == null || rightFore == null || rightHand == null) return;
            var root = animator.transform;
            var target = rightUpper.position + root.TransformDirection(offset);
            Reach(rightUpper, rightFore, rightHand, Vector3.Lerp(rightHand.position, target, Mathf.Clamp01(weight)), root.TransformDirection(CarryElbow));
        }

        // Two-bone reach: the wrist goes to the target, the elbow bends towards the hint.
        static void Reach(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 elbowHint)
        {
            float a = Vector3.Distance(upper.position, fore.position);
            float b = Vector3.Distance(fore.position, hand.position);
            var delta = target - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            var axis = delta.normalized;
            var bend = Vector3.ProjectOnPlane(elbowHint, axis).normalized;
            if (bend.sqrMagnitude < 0.01f) bend = Vector3.ProjectOnPlane(Vector3.down, axis).normalized;
            float alongAxis = (a * a - b * b + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - alongAxis * alongAxis));
            var elbow = upper.position + axis * alongAxis + bend * height;
            upper.rotation = Quaternion.FromToRotation(fore.position - upper.position, elbow - upper.position) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, target - fore.position) * fore.rotation;
        }
    }
}
