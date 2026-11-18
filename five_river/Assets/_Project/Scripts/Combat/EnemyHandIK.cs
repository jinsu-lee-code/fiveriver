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
        // (2026-11-18) Where the Soviet manual for the Mosin (НСД-38, arts. 128, 131, 142) puts the support hand: the
        // rifle is not gripped but laid on the left palm, near the ball of the thumb - standing, under the magazine box;
        // kneeling and lying, under the rear sight. Measured on the Mosin mesh from the grip anchor: the magazine box
        // ends 4 cm behind it, the rear sight stands 20-27 cm ahead of it.
        public static float AimSupportAhead = -0.04f, KneelSupportAhead = 0.22f;
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
            BuildProbes();
        }

        // (2026-11-18) Points on the hands' own skin, per bone, in that bone's space: taken from the skinned mesh
        // (vertices bound mostly to one hand or finger bone), so the grip is tested against the real surface of the
        // hands and not a guessed thickness round the bones. Fingers stopped by their bones alone went 2-3 cm into the
        // wood with the generated hands, whose fingers are thicker than the old ones.
        public static int ProbesPerBone = 40;
        public static float Pad = 0.0015f;   // how far off the wood the skin stops
        readonly Dictionary<Transform, Vector3[]> probes = new Dictionary<Transform, Vector3[]>();

        void BuildProbes()
        {
            probes.Clear();
            SkinnedMeshRenderer skin = null;
            foreach (var r in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && r.sharedMesh.isReadable && (skin == null || r.sharedMesh.vertexCount > skin.sharedMesh.vertexCount)) skin = r;
            if (skin == null) return;
            var want = new HashSet<Transform> { leftHand, rightHand };
            foreach (var chains in new[] { leftFingers, rightFingers }) foreach (var c in chains) foreach (var b in c) if (b != null) want.Add(b);
            foreach (var c in new[] { leftThumb, rightThumb }) foreach (var b in c) if (b != null) want.Add(b);
            var bones = skin.bones; var binds = skin.sharedMesh.bindposes;
            var verts = skin.sharedMesh.vertices; var weights = skin.sharedMesh.boneWeights;
            var lists = new Dictionary<int, List<Vector3>>();
            for (int v = 0; v < verts.Length; v++)
            {
                var w = weights[v];
                if (w.weight0 < 0.4f || w.boneIndex0 >= bones.Length || !want.Contains(bones[w.boneIndex0])) continue;
                if (!lists.TryGetValue(w.boneIndex0, out var l)) lists[w.boneIndex0] = l = new List<Vector3>();
                l.Add(binds[w.boneIndex0].MultiplyPoint3x4(verts[v]));
            }
            foreach (var kv in lists)
            {
                var l = kv.Value; int n = Mathf.Min(ProbesPerBone, l.Count); var pick = new Vector3[n];
                for (int k = 0; k < n; k++) pick[k] = l[k * l.Count / n];
                probes[bones[kv.Key]] = pick;
            }
        }

        // the least clearance of a bone's skin from the wood (negative: inside); the bone's own point if it has no skin
        float Clearance(Transform bone, out Vector3 worst)
        {
            worst = bone.position;
            if (!probes.TryGetValue(bone, out var local)) return OutsideWood(bone.position, Vector3.zero, rifle.forward) - FingerRadius;
            float least = float.MaxValue; var m = bone.localToWorldMatrix;
            foreach (var p in local)
            {
                var w = m.MultiplyPoint3x4(p); float d = OutsideWood(w, Vector3.zero, rifle.forward);
                if (d < least) { least = d; worst = w; }
            }
            return least;
        }

        float ChainClearance(Transform[] chain)
        {
            float least = float.MaxValue;
            foreach (var b in chain) if (b != null) least = Mathf.Min(least, Clearance(b, out _));
            return least;
        }

        // the palm out of the wood: the hand is moved straight off the rifle's axis by as much as its skin is in
        // (the arm reaches for it, the hand keeps its turn)
        void PalmOut(Transform hand, Transform upper, Transform fore, Vector3 elbowHint, Transform[] thumb, Transform[][] fingers)
        {
            if (upper == null || fore == null) return;
            // the palm with the ball of the thumb and the roots of the fingers (the ball of the thumb went in first)
            var parts = new List<Transform> { hand };
            if (thumb != null && thumb[0] != null) parts.Add(thumb[0]);
            foreach (var c in fingers) if (c[0] != null) parts.Add(c[0]);
            for (int pass = 0; pass < 4; pass++)
            {
                float d = float.MaxValue; var worst = hand.position;
                foreach (var b in parts) { float e = Clearance(b, out var w); if (e < d) { d = e; worst = w; } }
                if (d >= Pad) return;
                var off = worst - WoodMiddle(worst, Vector3.zero, rifle.forward); off -= rifle.forward * Vector3.Dot(off, rifle.forward);
                if (off.sqrMagnitude < 1e-8f) off = hand.position - WoodMiddle(hand.position, Vector3.zero, rifle.forward);
                var turn = hand.rotation;
                Reach(upper, fore, hand, hand.position + off.normalized * (Pad - d), elbowHint);
                hand.rotation = turn;
            }
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
            float aimAhead = Mathf.Lerp(AimSupportAhead, KneelSupportAhead, Mathf.Clamp01(Kneel));
            var wrist = a + along * Mathf.Lerp(Mathf.Lerp(SupportAhead, aimAhead, aim), LowSupportAhead, low) + rifle.up * ForeEndAbove + rifle.TransformDirection(SupportWrist);
            Reach(leftUpper, leftFore, leftHand, Vector3.Lerp(leftHand.position, wrist, weight),
                animator.transform.TransformDirection(Vector3.Lerp(Vector3.Lerp(RunElbow, AimElbow, aim), LowElbow, low)));
            GripStockWrist(weight);
            // support hand: palm up under the fore-end, fingers wrapping across to the right
            var across = rifle.TransformDirection(new Vector3(1f, .12f, .05f));
            var support = Quaternion.LookRotation(across, Vector3.Cross(rifle.forward, across)) * Quaternion.Inverse(leftPalmFrame);
            leftHand.rotation = Quaternion.Slerp(leftHand.rotation, support, weight);
            if (measured)
            {
                // fingers straight while the palms are fitted (curled from the clip they would catch on the wood)
                foreach (var chains in new[] { leftFingers, rightFingers }) foreach (var c in chains) foreach (var b in c) if (b != null) b.localRotation = fingerRest[b];
                // the thumbs at rest too, so they do not hold the palm off
                foreach (var c in new[] { leftThumb, rightThumb }) foreach (var b in c) if (b != null) b.localRotation = fingerRest[b];
                PalmOut(leftHand, leftUpper, leftFore, animator.transform.TransformDirection(Vector3.Lerp(Vector3.Lerp(RunElbow, AimElbow, aim), LowElbow, low)), leftThumb, leftFingers);
                PalmOut(rightHand, rightUpper, rightFore, animator.transform.TransformDirection(CarryElbow), rightThumb, rightFingers);
            }
            Close(rightFingers, rightThumb, rightIndex, rightLittle, true, weight);
            Close(leftFingers, leftThumb, leftIndex, leftLittle, false, weight);
        }

        // The trigger hand round the small of the stock (2026-11-18). The rifle is placed from the clip's wrist, which
        // sat 16 cm too far forward, over the trigger guard and magazine: palm up under it (as until now) the fingers
        // came up through the guard. Now the hand is turned as on a straight-wristed stock - palm on its right side,
        // knuckles down and forward, the fingers curling under it to the left, the thumb over the top - and the arm
        // brings the knuckles to the wrist of the stock just behind the guard. Rifle space (x right, y up, z muzzle).
        public static Vector3 TriggerKnuckles = new Vector3(0.034f, -0.048f, -0.020f);
        // (2026-11-18, the Soviet manual for the Mosin, НСД-38 art. 128: the hand on the neck of the stock from the side,
        // the thumb round it from above, the forefinger through the guard with its first joint on the trigger, the other
        // fingers round it from below) the fingers run down across the stock, so they close round it and the row of knuckles lies along
        // its right side; pointed forward along it (as until then) they lay flat under the wood and the back of the
        // hand read as a shapeless lump.
        public static Vector3 TriggerFingers = new Vector3(-0.15f, -0.95f, 0.15f);   // wrist -> knuckles
        public static float ForearmShare = 0.3f;
        public static Vector3 TriggerPoint = new Vector3(0f, -0.04f, 0.045f);   // the trigger, inside the guard (rifle space): the forefinger's first joint goes on it (НСД-38 art. 128)
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
        public static float WoodRadius = 0.024f, FingersTogether = 0.55f;   // WoodRadius: only if the rifle mesh cannot be read
        public static float FingerRadius = 0.008f;   // a finger's half-thickness, where its skin is not known
        static readonly float[] JointMax = { 90f, 105f, 80f };   // how far each finger joint can bend (degrees)
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
                if (triggerHand && finger == 0)
                {
                    // the forefinger on the trigger, off the wood: pointed at it, its last two joints a little crooked
                    OnTrigger(chain, axis);
                    if (weight < 1f) { chain[0].localRotation = Quaternion.Slerp(clip0, chain[0].localRotation, weight); chain[1].localRotation = Quaternion.Slerp(clip1, chain[1].localRotation, weight); chain[2].localRotation = Quaternion.Slerp(clip2, chain[2].localRotation, weight); }
                    continue;
                }
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
                    // bent a little at a time while every point of the finger's skin stays off the wood (2026-11-18: the
                    // tip alone was tested before, so the middle of the finger went through the stock first)
                    // Closed as a hand closes round a bar, one joint after another from the knuckle out: each joint bends
                    // until the skin from it to the tip reaches the wood, then the next takes over. Bending all three
                    // together, the middle of a finger lying under the fore-end touched first and the rest of it stood
                    // out straight past the far side. (A part already lying on the wood may stay there, not go deeper.)
                    for (int joint = 0; joint < 3; joint++)
                    {
                        var rest = chain[joint].localRotation;
                        var part = joint == 0 ? chain : joint == 1 ? new[] { chain[1], chain[2] } : new[] { chain[2] };
                        float allowed = Mathf.Min(Pad, ChainClearance(part)) - 0.001f;
                        float bent = 0f;
                        for (float angle = 3f; angle <= JointMax[joint]; angle += 3f)
                        {
                            chain[joint].localRotation = rest;
                            chain[joint].rotation = Quaternion.AngleAxis(sign * angle, axis) * chain[joint].rotation;
                            if (ChainClearance(part) < allowed) break;
                            bent = angle;
                        }
                        chain[joint].localRotation = rest;
                        chain[joint].rotation = Quaternion.AngleAxis(sign * bent, axis) * chain[joint].rotation;
                    }
                }
                if (weight >= 1f) continue;
                // easing in and out of the run: part of the way from the clip's own fingers
                chain[0].localRotation = Quaternion.Slerp(clip0, chain[0].localRotation, weight);
                chain[1].localRotation = Quaternion.Slerp(clip1, chain[1].localRotation, weight);
                chain[2].localRotation = Quaternion.Slerp(clip2, chain[2].localRotation, weight);
            }
            LayThumb(thumb, stockA, stockDirection, triggerHand, weight);
        }

        // The rifle's cross-section along its length, measured on its mesh when it is bound: the outline of what a
        // hand closes round at each place (the stock axis used for the arms runs through the middle of the whole
        // rifle, which at the wrist of the stock is well below the wood). (2026-11-18) The outline is the convex hull of
        // the slice, not one averaged radius: with a round section the wood's flat sides let the fingers sink 2-3 cm in.
        const int Sections = 96;
        readonly Vector2[] sectionMiddle = new Vector2[Sections];
        readonly Vector2[][] sectionHull = new Vector2[Sections][];
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
            var slice = new List<Vector2>[Sections];
            for (int i = 0; i < Sections; i++) slice[i] = new List<Vector2>();
            foreach (var p in points)
            {
                // each point also counts for the slices either side: a slice holds few vertices of its own
                int i = Mathf.Clamp((int)((p.z - from) / sectionStep), 0, Sections - 1);
                for (int k = Mathf.Max(0, i - 1); k <= Mathf.Min(Sections - 1, i + 1); k++) slice[k].Add(new Vector2(p.x, p.y));
            }
            for (int i = 0; i < Sections; i++)
            {
                sectionHull[i] = slice[i].Count >= 3 ? Hull(slice[i]) : (i > 0 ? sectionHull[i - 1] : null);
                var m = Vector2.zero;
                if (sectionHull[i] != null) { foreach (var q in sectionHull[i]) m += q; m /= sectionHull[i].Length; }
                sectionMiddle[i] = m;
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

        // How far a point is outside the wood (negative: inside), against the section's outline where it is.
        float OutsideWood(Vector3 point, Vector3 a, Vector3 direction)
        {
            if (!measured) return Vector3.Cross(point - a, direction).magnitude - WoodRadius;
            var local = rifle.InverseTransformPoint(point);
            int i = Mathf.Clamp((int)((local.z - sectionFrom) / sectionStep), 0, Sections - 1);
            var hull = sectionHull[i];
            if (hull == null) return float.MaxValue;
            var q = new Vector2(local.x, local.y);
            // signed distance to a convex polygon (counter-clockwise): outside if beyond any edge
            float inside = float.MaxValue, outside = float.MaxValue; bool isOut = false;
            for (int k = 0; k < hull.Length; k++)
            {
                Vector2 e0 = hull[k], e1 = hull[(k + 1) % hull.Length], e = e1 - e0;
                float len = e.magnitude; if (len < 1e-6f) continue;
                float side = (e.x * (q.y - e0.y) - e.y * (q.x - e0.x)) / len;   // > 0: inner side
                if (side < 0f) isOut = true;
                inside = Mathf.Min(inside, side);
                float t = Mathf.Clamp01(Vector2.Dot(q - e0, e) / (len * len));
                outside = Mathf.Min(outside, (q - (e0 + e * t)).magnitude);
            }
            return (isOut ? outside : -inside) * rifle.lossyScale.x;
        }

        // convex hull of a section's points, counter-clockwise (monotone chain)
        static Vector2[] Hull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            foreach (var p in pts) { while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0f) h.RemoveAt(h.Count - 1); h.Add(p); }
            int lower = h.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--) { var p = pts[i]; while (h.Count >= lower && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0f) h.RemoveAt(h.Count - 1); h.Add(p); }
            h.RemoveAt(h.Count - 1);
            return h.ToArray();
        }

        // how far from a point in the wood its surface lies in a direction (the averaged radius without a measured rifle)
        float Surface(Vector3 from, Vector3 direction)
        {
            if (!measured) return WoodRadius;
            float inside = 0f, outside = 0.12f;
            for (int k = 0; k < 14; k++)
            {
                float mid = (inside + outside) * 0.5f;
                if (OutsideWood(from + direction * mid, Vector3.zero, rifle.forward) < 0f) inside = mid; else outside = mid;
            }
            return outside;
        }

        void OnTrigger(Transform[] chain, Vector3 axis)
        {
            var trigger = rifle.TransformPoint(TriggerPoint);
            var sign = Vector3.Dot(Vector3.Cross(axis, chain[1].position - chain[0].position), trigger - chain[0].position) >= 0f ? 1f : -1f;
            chain[1].rotation = Quaternion.AngleAxis(sign * 35f, axis) * chain[1].rotation;
            chain[2].rotation = Quaternion.AngleAxis(sign * 25f, axis) * chain[2].rotation;
            var tip = chain[2].position + (chain[2].position - chain[1].position) * 0.8f;
            chain[0].rotation = Quaternion.FromToRotation(tip - chain[0].position, trigger - chain[0].position) * chain[0].rotation;
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
            // Trigger hand: over the top of the small of the stock to its left side, barely forward (laid along the top
            // and 5 cm forward, as until then, it stood out as an over-long thumb); support hand: along the near side.
            // Aimed a thumb's half-thickness off the wood's real surface that way (the averaged radius, as until then,
            // lay inside the thicker small of the stock and drew the thumb into it).
            var side = triggerHand ? (rifle.up - rifle.right * 0.8f).normalized : away.normalized;
            var target = onAxis + side * (Surface(onAxis, side) + FingerRadius) + stockDirection * (triggerHand ? 0.01f : 0.05f);
            var tip = thumb[2].position + (thumb[2].position - thumb[1].position) * 0.8f;
            thumb[0].rotation = Quaternion.FromToRotation(tip - root, target - root) * thumb[0].rotation;
            // The straight line to there can still pass through the wood (the root of the thumb sits low on the side
            // of the stock): of the turns within 60 degrees of the aim, the nearest one whose skin is clear is taken
            // (the one with most room if none is).
            var aimed = thumb[0].rotation; var along = (thumb[2].position - root).normalized;
            var u = Vector3.Cross(along, stockDirection).normalized; var v = Vector3.Cross(along, u);
            Quaternion best0 = aimed; float bestScore = float.MinValue;
            for (int a = -4; a <= 4; a++)
                for (int b = -4; b <= 4; b++)
                {
                    thumb[0].rotation = Quaternion.AngleAxis(a * 15f, u) * Quaternion.AngleAxis(b * 15f, v) * aimed;
                    float clear = ChainClearance(thumb);
                    float score = (clear >= Pad ? 1f : clear * 100f) - (Mathf.Abs(a) + Mathf.Abs(b)) * 0.01f;
                    if (score > bestScore) { bestScore = score; best0 = thumb[0].rotation; }
                }
            thumb[0].rotation = best0;
            if (triggerHand && ChainClearance(thumb) >= Pad) CurlThumb(thumb, stockA, stockDirection);
            if (weight >= 1f) return;
            thumb[0].localRotation = Quaternion.Slerp(clip0, thumb[0].localRotation, weight);
            thumb[1].localRotation = Quaternion.Slerp(clip1, thumb[1].localRotation, weight);
            thumb[2].localRotation = Quaternion.Slerp(clip2, thumb[2].localRotation, weight);
        }

        // the trigger hand's thumb: its last two joints curl down onto the far side, each until its skin reaches the wood
        void CurlThumb(Transform[] thumb, Vector3 stockA, Vector3 stockDirection)
        {
            for (int j = 1; j < 3; j++)
            {
                var part = j == 1 ? new[] { thumb[1], thumb[2] } : new[] { thumb[2] };
                float allowed = Pad;
                var rest = thumb[j].localRotation; float bent = 0f;
                var toward = Vector3.Cross(thumb[j].position - thumb[j - 1].position, WoodMiddle(thumb[j].position, stockA, stockDirection) - thumb[j].position);
                if (toward.sqrMagnitude < 1e-10f) continue;
                toward.Normalize();
                for (float angle = 4f; angle <= 70f; angle += 4f)
                {
                    thumb[j].localRotation = rest; thumb[j].rotation = Quaternion.AngleAxis(angle, toward) * thumb[j].rotation;
                    if (ChainClearance(part) < allowed) break;
                    bent = angle;
                }
                thumb[j].localRotation = rest; thumb[j].rotation = Quaternion.AngleAxis(bent, toward) * thumb[j].rotation;
            }
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
