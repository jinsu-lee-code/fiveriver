using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Puts the KPA rifleman's hands on the rifle he is carrying. The clips hold the hands for a shorter weapon,
    // so after EnemyAgent lines the Mosin up with the hands the left palm could hover up to 10 cm off it.
    // Lives on the Animator object (OnAnimatorIK is only sent there); EnemyAgent adds it and supplies the rifle.
    // 2026-11-10: while he runs the hands are also shaped to hold it (Shape, called by EnemyAgent once the rifle
    // is placed). Seen close, the run clip's trigger hand lay on the receiver with a pointing finger and its
    // support hand swung open, away from the rifle. The aiming and firing clips grip correctly on their own.
    public sealed class EnemyHandIK : MonoBehaviour
    {
        public static float Weight = 0.85f, Below = 0.03f;
        // Running: where the support wrist sits from the middle of the fore-end wood (rifle space: left of it and
        // below, so the wood lies across the palm), how far ahead of the grip the support hand holds, and how far
        // the trigger palm may be turned from the clip's own pose so the wrist is never wrung round.
        public static Vector3 SupportWrist = new Vector3(-0.09f, -0.047f, 0f);
        public static float SupportAhead = 0.36f;
        // Aiming (2026-11-13, against a 1944 photo of a Mosin held in the aim): the support hand cradles the fore-end
        // about a hand's span ahead of the trigger hand, just in front of the magazine, with the elbow straight under
        // the rifle. With the running values the arm reached almost straight out along the barrel, elbow to the side.
        public static float AimSupportAhead = 0.27f;
        public static Vector3 RunElbow = new Vector3(-0.4f, -0.8f, 0.1f), AimElbow = new Vector3(-0.1f, -1f, 0.15f);
        // The stock axis runs through the middle of the whole mesh, and the butt drops: at the fore-end the wood
        // itself is this far above that axis (2026-11-11, measured on the mesh: the wood there spans 0 to 5.5 cm
        // above the axis line the support wrist used to be hung 3 cm under, which left the palm 6 cm off it).
        public static float ForeEndAbove = 0.061f;
        Animator animator;
        Transform rifle;
        Vector3 axisLocalStart, axisLocalEnd;
        Transform rightUpper, rightFore;
        Transform leftHand, rightHand, leftUpper, leftFore, leftIndex, leftLittle, rightIndex, rightLittle;
        Transform[][] leftFingers, rightFingers;
        Transform[] leftThumb, rightThumb;
        Quaternion leftPalmFrame, rightPalmFrame;
        readonly Dictionary<Transform, Quaternion> fingerRest = new Dictionary<Transform, Quaternion>();
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
            MeasureSections();
            if (!animator.isHuman) return;
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            leftLittle = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            rightIndex = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            rightLittle = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            leftFingers = AllyRiflePose.Fingers(animator, true);
            rightFingers = AllyRiflePose.Fingers(animator, false);
            leftThumb = AllyRiflePose.Thumb(animator, true);
            rightThumb = AllyRiflePose.Thumb(animator, false);
            leftPalmFrame = PalmFrame(leftHand, leftIndex, leftLittle, true);
            rightPalmFrame = PalmFrame(rightHand, rightIndex, rightLittle, false);
            var skeleton = animator.avatar.humanDescription.skeleton;
            foreach (var chain in leftFingers) AllyRiflePose.CacheRest(chain, skeleton, fingerRest);
            foreach (var chain in rightFingers) AllyRiflePose.CacheRest(chain, skeleton, fingerRest);
            AllyRiflePose.CacheRest(leftThumb, skeleton, fingerRest);
            AllyRiflePose.CacheRest(rightThumb, skeleton, fingerRest);
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
            && leftIndex != null && leftLittle != null && rightIndex != null && rightLittle != null;

        // The run clip does not hold a rifle: the trigger hand points a finger and the support hand swings open.
        // While he runs (weight = how much of the run carry is on) the support hand is brought under the
        // fore-end, both palms are turned onto the wood and the fingers closed round it. The aiming and firing
        // clips already grip correctly and are left exactly as they are (weight 0).
        // Call in LateUpdate, after the rifle has been placed.
        // aim: 1 when the rifle is in the aim (cover, firing), 0 while it is carried running
        // low: 1 while the rifle is carried low at the hip (crouched run). There the support hand takes the wood
        // just ahead of the magazine: at the running reach the arm could not get down to it and the hand stopped
        // in the air above the receiver (2026-11-15).
        public static float LowSupportAhead = 0.18f;
        public static Vector3 LowElbow = new Vector3(-0.5f, -0.6f, -0.3f);
        public void Shape(float weight, float aim = 0f, float low = 0f)
        {
            if (weight <= 0f || !Holding || rifle == null || !Shaped) return;
            weight = Mathf.Clamp01(weight);
            aim = Mathf.Clamp01(aim);
            var a = rifle.TransformPoint(axisLocalStart);
            var along = (rifle.TransformPoint(axisLocalEnd) - a).normalized;
            // the wrist goes under and to the left of the wood, so the palm (not the wrist bone) lies under it
            var wrist = a + along * Mathf.Lerp(Mathf.Lerp(SupportAhead, AimSupportAhead, aim), LowSupportAhead, low) + rifle.up * ForeEndAbove + rifle.TransformDirection(SupportWrist);
            Reach(leftUpper, leftFore, leftHand, Vector3.Lerp(leftHand.position, wrist, weight),
                animator.transform.TransformDirection(Vector3.Lerp(Vector3.Lerp(RunElbow, AimElbow, aim), LowElbow, low)));
            GripStockWrist(weight);
            // support hand: palm up under the fore-end, fingers wrapping across to the right
            var across = rifle.TransformDirection(new Vector3(1f, .12f, .05f));
            var support = Quaternion.LookRotation(across, Vector3.Cross(rifle.forward, across)) * Quaternion.Inverse(leftPalmFrame);
            leftHand.rotation = Quaternion.Slerp(leftHand.rotation, support, weight);
            Close(rightFingers, rightThumb, rightIndex, rightLittle, true, weight);
            Close(leftFingers, leftThumb, leftIndex, leftLittle, false, weight);
        }

        // The trigger hand round the small of the stock (2026-11-18). The rifle is placed from the clip's wrist, which
        // sat 16 cm too far forward, over the trigger guard and magazine: palm up under it (as until now) the fingers
        // came up through the guard. Now the hand is turned as on a straight-wristed stock - palm on its right side,
        // knuckles down and forward, the fingers curling under it to the left, the thumb over the top - and the arm
        // brings the knuckles to the wrist of the stock just behind the guard. Rifle space (x right, y up, z muzzle).
        public static Vector3 TriggerKnuckles = new Vector3(0.034f, -0.048f, -0.020f);
        public static Vector3 TriggerFingers = new Vector3(-0.1f, -0.8f, 0.55f);   // wrist -> knuckles
        public static float ForearmShare = 0.45f;
        public float TriggerWristBend { get; private set; }   // degrees between forearm and hand (check value)
        void GripStockWrist(float weight)
        {
            if (rightUpper == null || rightFore == null) return;
            var clip = rightHand.rotation;
            var fingers = rifle.TransformDirection(TriggerFingers).normalized;
            var knucklesWanted = rifle.TransformPoint(TriggerKnuckles);
            var elbow = animator.transform.TransformDirection(CarryElbow);
            var turn = clip;
            for (int pass = 0; pass < 3; pass++)   // later passes take up what the elbow's swing moved
            {
                // the hand follows the forearm part of the way: turned fully onto the wood the wrist bent ~90 degrees
                // and the sleeve came away from the hand
                var forearm = (rightHand.position - rightFore.position).normalized;
                var along = Vector3.Slerp(fingers, forearm, ForearmShare);
                turn = Quaternion.Slerp(clip, Quaternion.LookRotation(along, -rifle.right) * Quaternion.Inverse(rightPalmFrame), weight);
                rightHand.rotation = turn;
                var knuckles = (rightIndex.position + rightLittle.position) * 0.5f;
                Reach(rightUpper, rightFore, rightHand, rightHand.position + (knucklesWanted - knuckles) * weight, elbow);
            }
            rightHand.rotation = turn;
            TriggerWristBend = Vector3.Angle(rightHand.position - rightFore.position, (rightIndex.position + rightLittle.position) * 0.5f - rightHand.position);
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

        // Fingers start from the avatar's own straight joints (never a second curl on top of the clip's) and
        // bend towards the wood. The trigger finger stays long, alongside the guard.
        // The fingers close round the wood (2026-11-16). With one fixed bend per joint (as until then) they hooked
        // over the top of it like claws, spread wide as in the rest pose, with the thumb left sticking out where the
        // clip had it. Now each finger is first brought in beside the middle one, then bent - all three joints in
        // proportion - just until its tip reaches the wood's surface; the thumb is laid round the other side.
        public static float WoodRadius = 0.024f, FingersTogether = 0.6f;   // WoodRadius: only if the rifle mesh cannot be read
        static readonly Vector3 CurlShare = new Vector3(1f, 0.95f, 0.6f);
        void Close(Transform[][] fingers, Transform[] thumb, Transform index, Transform little, bool triggerHand, float weight)
        {
            var axis = (index.position - little.position).normalized;
            // the support hand closes round the fore-end wood, the trigger hand round the stock wrist
            var stockA = rifle.TransformPoint(axisLocalStart) + (triggerHand ? Vector3.zero : rifle.up * ForeEndAbove);
            var stockDirection = rifle.forward;
            var middle = fingers.Length > 1 && fingers[1][0] != null && fingers[1][1] != null ? fingers[1] : null;
            var middleWay = Vector3.zero;
            if (middle != null)
            {
                // the middle finger's rest direction, before anything is bent
                Quaternion m0 = middle[0].localRotation, m1 = middle[1].localRotation;
                middle[0].localRotation = fingerRest[middle[0]]; middle[1].localRotation = fingerRest[middle[1]];
                middleWay = (middle[1].position - middle[0].position).normalized;
                middle[0].localRotation = m0; middle[1].localRotation = m1;
            }
            for (int finger = 0; finger < fingers.Length; finger++)
            {
                var chain = fingers[finger];
                if (chain[0] == null || chain[1] == null || chain[2] == null) continue;
                Quaternion clip0 = chain[0].localRotation, clip1 = chain[1].localRotation, clip2 = chain[2].localRotation;
                foreach (var bone in chain) bone.localRotation = fingerRest[bone];
                var knuckle = chain[0].position;
                if (middle != null && chain != middle)
                {
                    // in beside the middle finger
                    var way = (chain[1].position - knuckle).normalized;
                    chain[0].rotation = Quaternion.FromToRotation(way, Vector3.Slerp(way, middleWay, FingersTogether)) * chain[0].rotation;
                }
                var inward = WoodMiddle(knuckle, stockA, stockDirection) - knuckle;
                float sign = Vector3.Dot(Vector3.Cross(axis, chain[1].position - knuckle), inward) >= 0f ? 1f : -1f;
                // every finger, the forefinger too, bends until its tip reaches the wood or the guard (a set, lighter
                // curl for the forefinger left it pointing out into the air below the rifle when he aimed)
                {
                    var rest0 = chain[0].localRotation;
                    float best = 0f, bestGap = float.MaxValue;
                    for (float angle = 10f; angle <= 110f; angle += 5f)
                    {
                        chain[0].localRotation = rest0; chain[1].localRotation = fingerRest[chain[1]]; chain[2].localRotation = fingerRest[chain[2]];
                        Bend(chain, axis, sign, CurlShare * angle);
                        var tip = chain[2].position + (chain[2].position - chain[1].position) * 0.85f;
                        float outside = OutsideWood(tip, stockA, stockDirection);
                        if (Mathf.Abs(outside) < bestGap) { bestGap = Mathf.Abs(outside); best = angle; }
                        if (outside < 0f) break;   // the tip has come round onto the wood: further only digs in
                    }
                    chain[0].localRotation = rest0; chain[1].localRotation = fingerRest[chain[1]]; chain[2].localRotation = fingerRest[chain[2]];
                    Bend(chain, axis, sign, CurlShare * best);
                }
                if (weight >= 1f) continue;
                // easing in and out of the run: part of the way from the clip's own fingers
                chain[0].localRotation = Quaternion.Slerp(clip0, chain[0].localRotation, weight);
                chain[1].localRotation = Quaternion.Slerp(clip1, chain[1].localRotation, weight);
                chain[2].localRotation = Quaternion.Slerp(clip2, chain[2].localRotation, weight);
            }
            LayThumb(thumb, stockA, stockDirection, triggerHand, weight);
        }

        static void Bend(Transform[] chain, Vector3 axis, float sign, Vector3 curl)
        {
            chain[0].rotation = Quaternion.AngleAxis(sign * curl.x, axis) * chain[0].rotation;
            chain[1].rotation = Quaternion.AngleAxis(sign * curl.y, axis) * chain[1].rotation;
            chain[2].rotation = Quaternion.AngleAxis(sign * curl.z, axis) * chain[2].rotation;
        }

        // The rifle's cross-section along its length, measured on its mesh when it is bound: the middle and the
        // half-thickness of what a hand closes round at each place (the stock axis used for the arms runs through
        // the middle of the whole rifle, which at the wrist of the stock is well below the wood).
        const int Sections = 96;
        readonly Vector2[] sectionMiddle = new Vector2[Sections];
        readonly float[] sectionRadius = new float[Sections];
        float sectionFrom, sectionStep;
        bool measured;

        void MeasureSections()
        {
            measured = false;
            if (rifle == null) return;
            var points = new List<Vector3>();
            foreach (var filter in rifle.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var toRifle = rifle.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices) points.Add(toRifle.MultiplyPoint3x4(v));
            }
            if (points.Count < 50) return;
            float from = float.MaxValue, to = float.MinValue;
            foreach (var p in points) { from = Mathf.Min(from, p.z); to = Mathf.Max(to, p.z); }
            sectionFrom = from; sectionStep = (to - from) / Sections;
            var low = new Vector2[Sections]; var high = new Vector2[Sections];
            for (int i = 0; i < Sections; i++) { low[i] = Vector2.one * float.MaxValue; high[i] = Vector2.one * float.MinValue; }
            foreach (var p in points)
            {
                int i = Mathf.Clamp((int)((p.z - from) / sectionStep), 0, Sections - 1);
                low[i] = Vector2.Min(low[i], new Vector2(p.x, p.y)); high[i] = Vector2.Max(high[i], new Vector2(p.x, p.y));
            }
            for (int i = 0; i < Sections; i++)
            {
                if (low[i].x > high[i].x) { low[i] = i > 0 ? low[i - 1] : Vector2.zero; high[i] = i > 0 ? high[i - 1] : Vector2.zero; }
                sectionMiddle[i] = (low[i] + high[i]) * 0.5f;
                sectionRadius[i] = ((high[i].x - low[i].x) + (high[i].y - low[i].y)) * 0.25f;
            }
            measured = true;
        }

        // The middle of the wood level with a point (the measured section; the arm axis if there is none).
        Vector3 WoodMiddle(Vector3 point, Vector3 a, Vector3 direction)
        {
            if (!measured) return a + direction * Vector3.Dot(point - a, direction);
            var local = rifle.InverseTransformPoint(point);
            int i = Mathf.Clamp((int)((local.z - sectionFrom) / sectionStep), 0, Sections - 1);
            return rifle.TransformPoint(new Vector3(sectionMiddle[i].x, sectionMiddle[i].y, local.z));
        }

        // How far a point is outside the wood (negative: inside), using the measured section where it is.
        float OutsideWood(Vector3 point, Vector3 a, Vector3 direction)
        {
            if (!measured) return Vector3.Cross(point - a, direction).magnitude - WoodRadius;
            var local = rifle.InverseTransformPoint(point);
            int i = Mathf.Clamp((int)((local.z - sectionFrom) / sectionStep), 0, Sections - 1);
            var offset = new Vector2(local.x, local.y) - sectionMiddle[i];
            float scale = rifle.lossyScale.x;
            return (offset.magnitude - sectionRadius[i]) * scale;
        }

        // The thumb goes round the other side of the wood from the fingers: on the stock wrist over the top of it,
        // at the fore-end along its near side, pointing a little forward.
        void LayThumb(Transform[] thumb, Vector3 stockA, Vector3 stockDirection, bool triggerHand, float weight)
        {
            if (thumb == null || thumb[0] == null || thumb[1] == null || thumb[2] == null) return;
            Quaternion clip0 = thumb[0].localRotation, clip1 = thumb[1].localRotation, clip2 = thumb[2].localRotation;
            foreach (var bone in thumb) bone.localRotation = fingerRest[bone];
            var root = thumb[0].position;
            var onAxis = WoodMiddle(root, stockA, stockDirection);
            var away = root - onAxis; away -= stockDirection * Vector3.Dot(away, stockDirection);
            var side = triggerHand ? rifle.up : away.normalized;
            var target = onAxis + side * (WoodRadius + 0.006f) + stockDirection * 0.05f;
            var tip = thumb[2].position + (thumb[2].position - thumb[1].position) * 0.8f;
            thumb[0].rotation = Quaternion.FromToRotation(tip - root, target - root) * thumb[0].rotation;
            if (weight >= 1f) return;
            thumb[0].localRotation = Quaternion.Slerp(clip0, thumb[0].localRotation, weight);
            thumb[1].localRotation = Quaternion.Slerp(clip1, thumb[1].localRotation, weight);
            thumb[2].localRotation = Quaternion.Slerp(clip2, thumb[2].localRotation, weight);
        }

        // Same construction as AllyRiflePose.PalmFrame: forward runs from the wrist to the knuckles, up is the way
        // the palm faces.
        static Quaternion PalmFrame(Transform hand, Transform index, Transform little, bool left)
        {
            if (hand == null || index == null || little == null) return Quaternion.identity;
            var along = hand.InverseTransformDirection((index.position + little.position) * .5f - hand.position);
            var across = hand.InverseTransformDirection(index.position - little.position);
            return Quaternion.LookRotation(along, left ? Vector3.Cross(across, along) : Vector3.Cross(along, across));
        }
    }
}
