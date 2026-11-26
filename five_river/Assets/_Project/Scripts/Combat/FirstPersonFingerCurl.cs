using System;
using System.Linq;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Closes the first-person hands round the bazooka's two grips (2026-11-23). The bazooka clips leave the fingers
    // nearly straight, so the hands lay open against the grips with the fingers through them. After the Animator (and
    // after BazookaPresentation has moved the arms) the finger joints are turned toward the palm:
    //  - each finger closes on the grip it holds: of the ways its three joints can fold so that no bone is inside the
    //    grip and its last bones rest on it, the one nearest a natural grasp (fingerCurl) - so a thick grip leaves an
    //    open hand and a thin one a tight fist (2026-11-23 "손가락 그립 자세": before, all ten fingers took the same fixed
    //    angles whatever they held, through the grips);
    //  - the right forefinger lies on the trigger instead, the pad of its last bone on the trigger's face;
    //  - a hand off its grip (reloading) eases to half-open.
    // "Inside" and "on" are read from a field of signed distances to the launcher's surface baked from its mesh
    // (GripField), so the round fluted front grip, the flat pistol grip and the trigger guard all count as they are.
    [DefaultExecutionOrder(200)]
    public sealed class FirstPersonFingerCurl : MonoBehaviour
    {
        [Serializable]
        public sealed class Hand
        {
            public Transform hand;
            public Transform grip;            // the wrist target on the grip this hand holds
            public Transform[] index = new Transform[3], middle = new Transform[3], ring = new Transform[3], pinky = new Transform[3], thumb = new Transform[3];
            public Transform space;           // the launcher's frame the field is in
            public GripField field;           // signed distance to the launcher round this grip
            public Transform trigger;         // a point on the trigger's face (right hand only)
            public float axisDepth;           // palm centre (on the bones) to the grip's middle: held when that is inside it
            [NonSerialized] public Transform[][] chains;
            [NonSerialized] public Vector3[][] axes;
            [NonSerialized] public Transform[] tips;
            [NonSerialized] public float[] radius;      // finger thickness, per chain, in the hand's scale
            [NonSerialized] public float[][] angle;     // the angles applied last frame (smoothed)
            [NonSerialized] public Vector3[] target;    // each finger's solved angles (fingers are solved in turn)
            [NonSerialized] public Vector3 triggerAngles; // forefinger on the trigger: spread, knuckle, middle joint
            [NonSerialized] public Matrix4x4 toLocal;
            [NonSerialized] public float unit;          // world metres per weapon unit
            [NonSerialized] public int turn;
            [NonSerialized] public Quaternion[][] before, written;   // each joint before folding, and as left, last frame
            [NonSerialized] public Quaternion[][] rest;              // each joint's bind-pose local rotation (fingers straight)
            [NonSerialized] public float[][] restFlex;               // each joint's bend in that pose (bone to bone, + toward the palm)
            [NonSerialized] public Vector3 thumbLift;                // the thumb base's second axis (palmar abduction), in its own space
            [NonSerialized] public float lift, liftTarget;           // its angle now / sought (degrees, + lifts the thumb toward the palm side)
            [NonSerialized] public Vector3[] lastDir;                // each finger's last bone, its direction from its own skin (bone space)
            [NonSerialized] public int[] guarded = new int[5];       // the frame each finger was last solved again for being in the launcher
            [NonSerialized] public int triggerSearched;              // the same for the forefinger on the trigger
            [NonSerialized] public bool ready, held, wrote, first = true;
        }

        public Hand left = new Hand(), right = new Hand();
        // a natural grasp (degrees, knuckle to tip): the fold sought where the grip allows, and the fold where a finger
        // meets nothing; off the grip both are scaled by `relaxed`
        public Vector3 fingerCurl = new Vector3(48f, 70f, 42f);
        public Vector3 thumbCurl = new Vector3(30f, 42f, 34f);
        [Range(0f, 1f)] public float relaxed = 0.45f;
        public float follow = 16f;            // how fast the fingers follow a new pose (1/s)
        public int fingersPerFrame = 2;       // fingers solved again each frame (the rest keep their last answer)
        [Range(0f, 1f)] public float weight = 1f;
        // (2026-11-24) The bazooka clips key the fingers only at their first and last frames, in another pose (left forefinger's
        // middle joint 53 degrees round, bent 44 back; right thumb's last joint 51 back and wrung 60; measured from the FBX keys,
        // source/unity_staging_20261124_hands/measure/clip_joint_audit.py): between them the fingers are at rest. Folded on top of
        // the clip, the fingers flicked backward for a frame or two at every idle loop and at the start and end of firing,
        // reloading and drawing. The fingers start from their rest (bind) pose instead and are folded from there.
        public bool fingersFromRest = true;

        // Human ranges per joint (degrees, + toward the palm), knuckle (MCP), middle (PIP), last (DIP) (2026-11-24, 사용자 지시
        // "물리법칙이나 인간의 관절구조상 절대 할 수 없는 모션은 고쳐"): a knuckle bends back about 20, a middle joint not at all,
        // a last joint about 5. The first version let every joint open 25 back. Thumb: its base swings across the palm (opposition)
        // -15..50, its knuckle bends 0..55 and its last joint -10..80, each about its own hinge (before, all three swung about the
        // hand's long axis, so its last two bones turned sideways, and opened up to 60 back).
        // The ranges are of the joint's real bend, bone to bone: the rest pose's own bend is taken off (the Tripo arms rest with
        // some last joints already 9 degrees back; folded from there by the old -5 they came to -14, measured in play 2026-11-24).
        // The thumb's ranges are from its own rest (its bones' rest bend is not a straight line), its base opens back to -60 (its
        // extension and radial abduction; with -15, and with its knuckle held to 15 by the rest bend taken off, it could not
        // leave the round front grip and lay 4 cm inside it, measured in play 2026-11-24), its knuckle 10 back.
        static readonly Vector3 MaxCurl = new Vector3(90f, 105f, 85f), MinCurl = new Vector3(-20f, 0f, -5f);
        static readonly Vector3 ThumbMax = new Vector3(50f, 55f, 80f), ThumbMin = new Vector3(-60f, -10f, -10f);
        // (2026-11-24, pass 3) The thumb base is a saddle: besides swinging across the palm it lifts away from the palm's plane
        // (palmar abduction, 0..60). With the swing alone the thumb could only go through the grip or stand off it, straight up
        // (its base on the -60 stop, measured in play at armScale 1.9); lifted, it lies along the grip's side. Its swing opens back
        // to -60, its extension (with -15 / -40 the right thumb found no way round the flat pistol grip and lay 3.6 / 4.4 cm in it,
        // measured in play; the left one, lifted, now rests near its natural 25 instead of on that stop).
        const float LiftMax = 60f, LiftNatural = 25f;
        const float MaxSpread = 20f;          // the forefinger's spread to the trigger, either way (index abduction ~20)
        const float Touch = 0.004f, Allowed = -0.002f;
        const float Guard = -0.003f;          // deeper than this in the launcher after easing: solved again now (pass 4)
        static float Lo(bool thumb, int k) => thumb ? ThumbMin[k] : MinCurl[k];
        static float Hi(bool thumb, int k) => thumb ? ThumbMax[k] : MaxCurl[k];
        // the fold added on top of the rest pose that keeps the joint's real bend within range
        static float RestOf(Hand h, int c, int k) => h.restFlex != null && c != 4 ? h.restFlex[c][k] : 0f;   // (the thumb's own: from its rest)
        // (a rest pose bent back past the range is folded forward into it: the Tripo right little finger's last joint rests 21 back)
        static float Lo(Hand h, int c, int k) => Lo(c == 4, k) - RestOf(h, c, k);
        static float Hi(Hand h, int c, int k) => Hi(c == 4, k) - RestOf(h, c, k);

        void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-follow * Time.deltaTime);
            Apply(left, k); Apply(right, k);
        }

        void OnEnable() { left.first = right.first = true; }

        void Apply(Hand h, float k)
        {
            if (h.hand == null || weight <= 0f) return;
            if (!h.ready) Prepare(h);
            if (!h.ready) return;
            var chains = h.chains;
            // a joint the Animator did not write this frame (culled, no curve) still holds last frame's fold: back to
            // the pose it had before, so the folds do not pile up
            for (int c = 0; c < chains.Length; c++)
                for (int j = 0; j < 3; j++)
                {
                    var t = chains[c][j]; if (t == null) continue;
                    if (fingersFromRest && h.rest != null) t.localRotation = h.rest[c][j];
                    else if (h.wrote && t.localRotation == h.written[c][j]) t.localRotation = h.before[c][j];
                    h.before[c][j] = t.localRotation;
                }
            bool held = Held(h);
            bool fresh = h.first || held != h.held;        // just took hold or let go: everything solved now
            if (h.first) k = 1f;
            // the forefinger takes up the trigger pose from where it is
            if (held && !h.held && h.trigger != null) h.triggerAngles = new Vector3(0f, h.angle[0][0], h.angle[0][1]);
            h.first = false; h.held = held;
            for (int c = 0; c < chains.Length; c++)
            {
                if (chains[c][0] == null || chains[c][1] == null || chains[c][2] == null) continue;
                if (c == 0 && h.trigger != null && held) { Trigger(h, k, fresh); continue; }
                if (c == 0 && h.trigger != null && Mathf.Abs(h.triggerAngles.x) > 0.05f)
                {
                    // let go: the trigger finger's spread eases back too
                    h.triggerAngles.x = Mathf.Lerp(h.triggerAngles.x, 0f, k);
                    chains[0][0].rotation = Quaternion.AngleAxis(h.triggerAngles.x * weight, PalmNormal(h)) * chains[0][0].rotation;
                }
                var natural = c == 4 ? thumbCurl : fingerCurl;
                // (2026-11-25, pass 4) a hand off its grip but near it (coming back from the reload) eased its fingers half-open
                // whatever was there, and they swept through the grip for 0.1-0.2 s before it counted as held (left forefinger
                // 43 mm in, measured in play): half-open, but not in the launcher
                bool touch = held;
                var seek = held ? natural : natural * relaxed;
                float seekLift = held ? LiftNatural : LiftNatural * relaxed;
                if (!held)
                {
                    if (fresh || Due(h, c))
                    {
                        h.target[c] = seek; if (c == 4) h.liftTarget = seekLift;
                        if (Inside(h, c, seek, seekLift) < Allowed) h.target[c] = Solve(h, c, seek, seekLift, false);
                    }
                }
                else if (fresh || Due(h, c)) h.target[c] = Solve(h, c, natural, seekLift, true);
                float lift = c == 4 ? Mathf.Clamp(Mathf.Lerp(h.lift, h.liftTarget, k), 0f, LiftMax) : 0f;
                var g = Vector3.zero;
                for (int j = 0; j < 3; j++) g[j] = Mathf.Clamp(Mathf.Lerp(h.angle[c][j], h.target[c][j], k), Lo(h, c, j), Hi(h, c, j));
                // (pass 4) the fingers ease toward their answer (and only two are solved again each frame), so when the hand slid along
                // the grip (into the aim, back from the reload) they lagged and lay in it for a few frames (thumb 44 mm, middle finger
                // 41 mm, measured in play): eased into the launcher, a finger takes its answer for where the hand is now at once
                if (Inside(h, c, g, lift) < Guard)
                {
                    // the last answer still clear where the hand is now: taken as it is; else solved again (not every frame
                    // for a finger that has no clear way at all: it keeps the least-inside answer it found)
                    float tl = c == 4 ? h.liftTarget : 0f;
                    if (Inside(h, c, h.target[c], tl) < Allowed && h.guarded[c] != Time.frameCount - 1)
                    {
                        h.target[c] = Solve(h, c, seek, seekLift, touch);
                        // no clear way found: not looked for again next frame
                        h.guarded[c] = Inside(h, c, h.target[c], c == 4 ? h.liftTarget : 0f) < Allowed ? Time.frameCount : -10;
                    }
                    for (int j = 0; j < 3; j++) g[j] = Mathf.Clamp(h.target[c][j], Lo(h, c, j), Hi(h, c, j));
                    if (c == 4) lift = Mathf.Clamp(h.liftTarget, 0f, LiftMax);
                }
                if (c == 4)
                {
                    h.lift = lift;
                    chains[4][0].localRotation *= Quaternion.AngleAxis(h.lift * weight, h.thumbLift);
                }
                for (int j = 0; j < 3; j++)
                {
                    h.angle[c][j] = g[j];
                    chains[c][j].localRotation *= Quaternion.AngleAxis(h.angle[c][j] * weight, h.axes[c][j]);
                }
            }
            for (int c = 0; c < chains.Length; c++)
                for (int j = 0; j < 3; j++) if (chains[c][j] != null) h.written[c][j] = chains[c][j].localRotation;
            h.wrote = true;
            h.turn = (h.turn + Mathf.Max(1, fingersPerFrame)) % 5;
        }

        // the fingers solved again this frame
        bool Due(Hand h, int c) { int d = (c - h.turn + 5) % 5; return d < Mathf.Max(1, fingersPerFrame); }

        // holding: the grip's middle (the palm's thickness and the grip's radius out from the palm's bones) is inside it
        bool Held(Hand h)
        {
            if (h.field == null || h.space == null || h.axisDepth <= 0f) return false;
            h.toLocal = h.space.worldToLocalMatrix; h.unit = h.space.lossyScale.x;
            var centre = h.hand.position + (h.middle[0].position - h.hand.position) * 0.55f;
            // 2 mm either way before it changes, so the edge does not flicker
            float d = Sdf(h, centre + PalmNormal(h) * h.axisDepth);
            return h.held ? d < 0.002f : d < -0.002f;
        }

        float Sdf(Hand h, Vector3 world) => h.field.Sample(h.toLocal.MultiplyPoint3x4(world)) * h.unit;

        // signed distance from a point to the launcher round this hand's grip (for checks)
        public float SurfaceDistance(Hand h, Vector3 world)
        {
            if (h.field == null || h.space == null) return float.NaN;
            h.toLocal = h.space.worldToLocalMatrix; h.unit = h.space.lossyScale.x;
            return Sdf(h, world);
        }

        static Transform[][] Chains(Hand h) => new[] { h.index, h.middle, h.ring, h.pinky, h.thumb };

        // One finger as it stands before folding: its joints, its tip, each joint's folding axis (world).
        struct Finger { public Vector3 p0, p1, p2, p3, a0, a1, a2, ab; public float t, b; }

        static Finger Read(Hand h, int c)
        {
            var ch = h.chains[c];
            return new Finger
            {
                p0 = ch[0].position, p1 = ch[1].position, p2 = ch[2].position, p3 = h.tips[c].position,
                a0 = ch[0].TransformDirection(h.axes[c][0]).normalized, a1 = ch[1].TransformDirection(h.axes[c][1]).normalized, a2 = ch[2].TransformDirection(h.axes[c][2]).normalized,
                t = h.radius[c] * h.hand.lossyScale.x,
                ab = c == 4 ? ch[0].TransformDirection(h.thumbLift).normalized : Vector3.zero,
            };
        }

        // the joints after folding by g (degrees, knuckle to last joint), knuckle first
        static void Pose(in Finger f, Vector3 g, out Vector3 q1, out Vector3 q2, out Vector3 q3)
        {
            var r0 = Quaternion.AngleAxis(f.b, f.ab) * Quaternion.AngleAxis(g.x, f.a0);   // (b: the thumb's lift, 0 for the fingers)
            q1 = f.p0 + r0 * (f.p1 - f.p0); var q2a = f.p0 + r0 * (f.p2 - f.p0); var q3a = f.p0 + r0 * (f.p3 - f.p0);
            var r1 = Quaternion.AngleAxis(g.y, r0 * f.a1);
            q2 = q1 + r1 * (q2a - q1); var q3b = q1 + r1 * (q3a - q1);
            var r2 = Quaternion.AngleAxis(g.z, r1 * (r0 * f.a2));
            q3 = q2 + r2 * (q3b - q2);
        }

        // how far bone k (its end and its middle) stands off the launcher in pose g (its skin: less its thickness); < 0 in it
        float Clear(Hand h, in Finger f, Vector3 g, int k)
        {
            Pose(f, g, out var q1, out var q2, out var q3);
            Vector3 s = k == 0 ? f.p0 : k == 1 ? q1 : q2, e = k == 0 ? q1 : k == 1 ? q2 : q3;
            return Mathf.Min(Sdf(h, e), Sdf(h, (s + e) * 0.5f)) - f.t;
        }

        // How far the finger's deepest bone stands off the launcher in pose g (thumb lifted by `lift`); < 0 in it.
        // Far from the grip (or with no field) it is clear.
        float Inside(Hand h, int c, Vector3 g, float lift)
        {
            if (h.field == null || h.space == null) return float.MaxValue;
            h.toLocal = h.space.worldToLocalMatrix; h.unit = h.space.lossyScale.x;
            var f = Read(h, c); f.b = c == 4 ? lift : 0f;
            return Mathf.Min(Clear(h, f, g, 0), Mathf.Min(Clear(h, f, g, 1), Clear(h, f, g, 2)));
        }

        // The fold of one finger. For a range of knuckle folds, the next joints each fold until their own bone meets the
        // launcher (or open until out of it); of those with no bone inside, the one nearest the natural grasp, a finger
        // whose last bones touch nothing counting as far off (`touch`: a hand off its grip only keeps out of it).
        Vector3 Solve(Hand h, int c, Vector3 natural, float liftNatural = LiftNatural, bool touch = true)
        {
            var f = Read(h, c);
            bool thumb = c == 4;
            var best = natural; float bestCost = float.MaxValue, bestLift = 0f;
            // none clear (a thumb pressed into the grip): the least inside
            var least = natural; float leastIn = float.MinValue, leastLift = 0f;
            for (float lift = 0f; lift <= (thumb ? LiftMax : 0f) + 0.1f; lift += 12f)
            for (float a0 = Lo(h, c, 0); a0 <= Hi(h, c, 0); a0 += 8f)
            {
                f.b = lift;
                var g = new Vector3(a0, 0f, 0f);
                float c0 = Clear(h, f, g, 0);
                g.y = Joint(h, c, f, g, 1, natural.y, thumb);
                g.z = Joint(h, c, f, g, 2, natural.z, thumb);
                float c1 = Clear(h, f, g, 1), c2 = Clear(h, f, g, 2);
                float worst = Mathf.Min(c0, Mathf.Min(c1, c2));
                if (worst > leastIn) { leastIn = worst; least = g; leastLift = lift; }
                if (worst < Allowed) continue;
                float cost = (g.x - natural.x) * (g.x - natural.x) + (g.y - natural.y) * (g.y - natural.y) + 0.5f * (g.z - natural.z) * (g.z - natural.z)
                    + (thumb ? 0.5f * (lift - liftNatural) * (lift - liftNatural) : 0f);
                if (touch && Mathf.Min(c1, c2) > Touch) cost += 3000f;
                if (cost < bestCost) { bestCost = cost; best = g; bestLift = lift; }
            }
            if (thumb) h.liftTarget = bestCost < float.MaxValue ? bestLift : leastLift;
            return bestCost < float.MaxValue ? best : least;
        }

        // joint k's fold, the earlier ones as in g: until its bone meets the launcher; inside already, out the nearest
        // way (opening; the thumb also closing round)
        float Joint(Hand h, int c, in Finger f, Vector3 g, int k, float fallback, bool both)
        {
            const float step = 6f;
            float lo = Lo(h, c, k), hi = Hi(h, c, k);
            if (Gap(h, f, g, k, 0f) < 0f)
            {
                for (float a = step; a <= hi + 0.1f; a += step)
                {
                    if (-a >= lo && Gap(h, f, g, k, -a) >= 0f) return Edge(h, f, g, k, -a, -a + step);
                    if (both && Gap(h, f, g, k, a) >= 0f) return Edge(h, f, g, k, a, a - step);
                }
                return lo;
            }
            for (float a = step; a <= hi; a += step)
                if (Gap(h, f, g, k, a) < 0f) return Edge(h, f, g, k, a - step, a);
            return Mathf.Clamp(fallback, lo, hi);
        }

        float Gap(Hand h, in Finger f, Vector3 g, int k, float a) { g[k] = a; return Clear(h, f, g, k); }

        // between a fold that is out and one that is in, the last that is out
        float Edge(Hand h, in Finger f, Vector3 g, int k, float outside, float inside)
        {
            for (int i = 0; i < 4; i++) { float m = (outside + inside) * 0.5f; if (Gap(h, f, g, k, m) >= 0f) outside = m; else inside = m; }
            return outside;
        }

        // The right forefinger toward the trigger: spread (about the palm's normal), knuckle and middle joint (the last
        // follows the middle one), chosen so the pad of its last bone goes as far toward the trigger's face as the finger
        // reaches, resting on the launcher, no bone in it. (The first-person launcher is drawn about twice life size and
        // the hand less, so from a full grip the finger falls 3-4 cm short of the trigger, measured 2026-11-23: it lies
        // along the guard toward it, as a soldier's does until he fires.)
        void Trigger(Hand h, float k, bool search)
        {
            var ch = h.index;
            var f = Read(h, 0);
            var palm = PalmNormal(h);
            var face = h.trigger.position + h.trigger.forward * f.t;
            float reach = ((f.p1 - f.p0).magnitude + (f.p2 - f.p1).magnitude + (f.p3 - f.p2).magnitude) * 0.85f;
            var toward = face - f.p0;
            var target = toward.magnitude > reach ? f.p0 + toward.normalized * reach : face;
            void Bones(Vector3 g, out Vector3 p2, out Vector3 p3, out Vector3 pt)
            {
                var r1 = Quaternion.AngleAxis(g.x, palm) * Quaternion.AngleAxis(g.y, f.a0);
                p2 = f.p0 + r1 * (f.p1 - f.p0);
                var r2 = Quaternion.AngleAxis(g.z, r1 * f.a1) * r1;
                p3 = p2 + r2 * (f.p2 - f.p1);
                var r3 = Quaternion.AngleAxis(g.z * 0.65f, r2 * f.a2) * r2;
                pt = p3 + r3 * (f.p3 - f.p2);
            }
            float Cost(Vector3 g)
            {
                Bones(g, out var p2, out var p3, out var pt);
                var pad = Vector3.Lerp(p3, pt, 0.6f);
                float cost = (pad - target).sqrMagnitude;
                // resting on the launcher rather than in the air: the pad's skin near its surface
                float rest = Sdf(h, pad) - f.t; if (rest > 0f) cost += 0.3f * rest * rest;
                // (pass 4: the tip too - left out, it went 30 mm into the trigger and its guard past the pad, measured in play)
                cost += In(h, (f.p0 + p2) * 0.5f, f.t) + In(h, p2, f.t) + In(h, (p2 + p3) * 0.5f, f.t) + In(h, p3, f.t) + In(h, (p3 + pt) * 0.5f, f.t) + In(h, pt, f.t);
                return cost + 1e-7f * (g.x * g.x + 0.2f * g.y * g.y);
            }
            // the deepest the finger's skin goes into the launcher in pose g (m; 0 when out)
            float Depth(Vector3 g)
            {
                Bones(g, out var p2, out var p3, out var pt);
                // (the six points along the finger one by one: an array here was made twice a frame while held)
                float d = Mathf.Max(0f, f.t - Sdf(h, (f.p0 + p2) * 0.5f));
                d = Mathf.Max(d, f.t - Sdf(h, p2));
                d = Mathf.Max(d, f.t - Sdf(h, (p2 + p3) * 0.5f));
                d = Mathf.Max(d, f.t - Sdf(h, p3));
                d = Mathf.Max(d, f.t - Sdf(h, (p3 + pt) * 0.5f));
                return Mathf.Max(d, f.t - Sdf(h, pt));
            }
            // coarse on taking hold (or when lost), then a small search round last frame's answer
            Vector3 best = h.triggerAngles; float bestCost = Cost(best);
            // lost: looked for again now and then, not every frame; in the launcher (the hand slid along the grip): now
            bool inside = Depth(best) > -Guard && h.triggerSearched != Time.frameCount - 1;
            if (inside) h.triggerSearched = Time.frameCount;
            if (search || inside || (bestCost > 4e-4f && Time.frameCount % 20 == 0))
                for (float x = -MaxSpread; x <= MaxSpread; x += 5f) for (float y = Lo(h, 0, 0); y <= Hi(h, 0, 0); y += 7f) for (float z = Lo(h, 0, 1); z <= Mathf.Min(100f, Hi(h, 0, 1)); z += 8f)
                { var g = new Vector3(x, y, z); float cst = Cost(g); if (cst < bestCost) { bestCost = cst; best = g; } }
            for (float s = 4f; s >= 0.5f; s *= 0.5f)
            {
                var centre = best;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                {
                    var g = centre + new Vector3(dx, dy, dz) * s;
                    // within the joints' ranges (the last joint follows the middle at 0.65, so 0..105 keeps it within 0..85)
                    g = new Vector3(Mathf.Clamp(g.x, -MaxSpread, MaxSpread), Mathf.Clamp(g.y, Lo(h, 0, 0), Hi(h, 0, 0)), Mathf.Clamp(g.z, Lo(h, 0, 1), Hi(h, 0, 1)));
                    float cst = Cost(g); if (cst < bestCost) { bestCost = cst; best = g; }
                }
            }
            // eased toward its answer, unless easing leaves it in the launcher: then at once
            var eased = Vector3.Lerp(h.triggerAngles, best, k);
            h.triggerAngles = Depth(eased) > -Guard ? best : eased;
            var a = h.triggerAngles * weight;
            ch[0].rotation = Quaternion.AngleAxis(a.x, palm) * Quaternion.AngleAxis(a.y, f.a0) * ch[0].rotation;
            ch[1].localRotation *= Quaternion.AngleAxis(a.z, h.axes[0][1]);
            ch[2].localRotation *= Quaternion.AngleAxis(a.z * 0.65f, h.axes[0][2]);
            h.angle[0][0] = h.triggerAngles.y; h.angle[0][1] = h.triggerAngles.z; h.angle[0][2] = h.triggerAngles.z * 0.65f;
        }

        // (pass 3: x400, was x40 - at armScale 1.9 the reach cost won and the forefinger lay 13 mm inside the guard, measured in play)
        float In(Hand h, Vector3 p, float t) { float gap = Sdf(h, p) - t; return gap < 0f ? gap * gap * 400f : 0f; }

        Vector3 PalmNormal(Hand h) =>
            Vector3.Cross(h.index[0].position - h.hand.position, h.pinky[0].position - h.hand.position).normalized * (h == left ? 1f : -1f);

        // The bend axis of every joint, in its own space, so that a positive angle folds it toward the palm.
        // Palm side: (index knuckle - wrist) x (little-finger knuckle - wrist) points out of the palm of the left hand and
        // out of the back of the right one (the two hands are mirror images), measured in play 2026-11-23 against the
        // grips each palm lies on. (The first version guessed the side by the grip markers, but those are the wrist
        // targets themselves, so half the fingers folded backward.)
        void Prepare(Hand h)
        {
            var chains = h.chains = Chains(h);
            if (chains[1][0] == null || h.index[0] == null || h.pinky[0] == null) return;
            var palm = PalmNormal(h);
            h.axes = new Vector3[chains.Length][];
            h.tips = new Transform[chains.Length];
            h.angle = new float[chains.Length][];
            h.target = new Vector3[chains.Length];
            h.radius = new float[chains.Length];
            h.before = new Quaternion[chains.Length][]; h.written = new Quaternion[chains.Length][];
            for (int c = 0; c < chains.Length; c++) { h.before[c] = new Quaternion[3]; h.written[c] = new Quaternion[3]; }
            h.wrote = false;
            // the rest pose from the arm mesh's bind poses (null when a joint is not on a skinned mesh: then the clip's pose is used)
            h.rest = new Quaternion[chains.Length][];
            var skin = HandJointMath.SkinOf(this, h.hand);
            for (int c = 0; c < chains.Length && h.rest != null; c++)
            {
                h.rest[c] = new Quaternion[3];
                for (int k = 0; k < 3; k++)
                    if (chains[c][k] == null || !HandJointMath.RestLocal(skin, chains[c][k], out h.rest[c][k])) { h.rest = null; break; }
            }
            // the axes below are read off the pose the fingers will be folded from
            if (fingersFromRest && h.rest != null)
                for (int c = 0; c < chains.Length; c++) for (int k = 0; k < 3; k++) chains[c][k].localRotation = h.rest[c][k];
            for (int c = 0; c < chains.Length; c++)
            {
                h.axes[c] = new Vector3[3]; h.angle[c] = new float[3];
                var last = chains[c][2];
                h.tips[c] = last != null && last.childCount > 0 ? last.GetChild(0) : last;
                for (int k = 0; k < 3; k++)
                {
                    var j = chains[c][k]; if (j == null) continue;
                    var next = k < 2 && chains[c][k + 1] != null ? chains[c][k + 1].position : j.position + (j.position - j.parent.position);
                    var dir = (next - j.position).normalized;
                    // rotating dir about cross(dir, palm) by a positive angle turns it toward the palm
                    var axis = Vector3.Cross(dir, palm);
                    if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(dir, h.hand.up);
                    if (c == 4 && k == 0)
                    {
                        // The thumb lies along the side of the palm, so folding it about the knuckle axis did nothing
                        // visible (it stood straight out past the grip): its base opposes, swinging about the hand's length
                        // toward the palm and the fingers.
                        var length = (chains[1][0].position - h.hand.position).normalized;
                        axis = length;
                        var turned = Quaternion.AngleAxis(10f, axis) * dir;
                        if (Vector3.Dot(turned - dir, palm) < 0f) axis = -axis;
                    }
                    else if (c == 4)
                    {
                        // its knuckle and last joint are hinges (2026-11-24; turned about the hand's length, its last bones went
                        // sideways): they close the tip onto the forefinger's middle joint, over and round what the hand holds
                        // (bent toward the middle of the palm instead, the thumb went through the round front grip, 4 cm)
                        var toward = Vector3.ProjectOnPlane(chains[0][1].position - j.position, dir);
                        axis = Vector3.Cross(dir, toward);
                        if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(dir, palm);
                        var turned = Quaternion.AngleAxis(10f, axis.normalized) * dir;
                        if (Vector3.Dot(turned - dir, toward) < 0f) axis = -axis;
                    }
                    h.axes[c][k] = j.InverseTransformDirection(axis.normalized).normalized;
                }
                h.radius[c] = FingerRadius(h, chains[c]);
            }
            // the thumb base's lift: about (its direction x palm normal), + turning it toward the palm side
            {
                var t0 = chains[4][0]; var t1 = chains[4][1];
                var d0 = (t1.position - t0.position).normalized;
                var liftAxis = Vector3.Cross(d0, palm);
                if (Vector3.Dot(Quaternion.AngleAxis(10f, liftAxis.normalized) * d0 - d0, palm) < 0f) liftAxis = -liftAxis;
                h.thumbLift = t0.InverseTransformDirection(liftAxis.normalized).normalized;
            }
            // (pass 3) each finger's last bone points where its skin goes, not where the rig's end marker (…4) sits: the right
            // little finger's marker rests bent 20 back (its first bone is 8.8 mm against the left's 24, the marker 7.9 mm), so the
            // rest bend read off it was the marker's, not the finger's
            h.lastDir = new Vector3[chains.Length];
            // (tried in play 2026-11-24: the middle of a last bone's skin sits off its axis, toward the nail or the pad, and read the
            // last joints 30-40 degrees back on fingers that were not; the end marker is kept, only the little finger's is off)
            for (int c = 0; c < chains.Length; c++) h.lastDir[c] = h.tips[c] != null && h.tips[c] != chains[c][2] ? chains[c][2].InverseTransformDirection(h.tips[c].position - chains[c][2].position).normalized : SkinDirection(chains[c][2], chains[c][1]);
            // the rest pose's own bend of each joint, bone to bone about its fold axis (+ toward the palm)
            h.restFlex = new float[chains.Length][];
            for (int c = 0; c < chains.Length; c++)
            {
                h.restFlex[c] = new float[3];
                for (int k = 0; k < 3; k++)
                {
                    var j = chains[c][k]; if (j == null) continue;
                    var from = j.position - (k == 0 ? h.hand.position : chains[c][k - 1].position);
                    var nextT = k < 2 ? chains[c][k + 1] : h.tips[c];
                    var to = k < 2 ? (nextT != null ? nextT.position - j.position : Vector3.zero) : j.TransformDirection(h.lastDir[c]);
                    if (to.sqrMagnitude < 1e-10f) continue;
                    var axisW = j.TransformDirection(h.axes[c][k]).normalized;
                    h.restFlex[c][k] = Vector3.SignedAngle(Vector3.ProjectOnPlane(from, axisW), Vector3.ProjectOnPlane(to, axisW), axisW);
                }
            }
            h.ready = true;
        }

        // A bone's direction from its own skin: from the joint to the middle of the vertices bound mostly to it (bind pose), in the
        // bone's space. Falls back to the previous bone's direction.
        Vector3 SkinDirection(Transform bone, Transform previous)
        {
            var fallback = bone.InverseTransformDirection(bone.position - previous.position).normalized;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int bi = Array.IndexOf(r.bones, bone);
                var mesh = r.sharedMesh;
                if (bi < 0 || mesh == null || !mesh.isReadable) continue;
                var bind = mesh.bindposes[bi]; var v = mesh.vertices; var w = mesh.boneWeights;
                var sum = Vector3.zero; int n = 0;
                for (int i = 0; i < v.Length; i++) if (w[i].boneIndex0 == bi && w[i].weight0 >= 0.6f) { sum += bind.MultiplyPoint3x4(v[i]); n++; }
                if (n < 6) continue;
                var d = sum / n;   // bone space: the joint is the origin
                if (d.sqrMagnitude < 1e-10f) continue;
                return d.normalized;
            }
            return fallback;
        }

        // The finger's thickness from the arm mesh itself: its vertices bound mostly to the finger's middle bone, their
        // median distance from the bone, in the hand's own scale. 8 mm if the mesh cannot tell.
        float FingerRadius(Hand h, Transform[] chain)
        {
            const float fallback = 0.008f;
            var bone = chain[1]; if (bone == null || chain[2] == null) return fallback / Mathf.Max(1e-4f, h.hand.lossyScale.x);
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int bi = Array.IndexOf(r.bones, bone);
                var mesh = r.sharedMesh;
                if (bi < 0 || mesh == null || !mesh.isReadable) continue;
                var bind = mesh.bindposes[bi];
                var to = bone.InverseTransformPoint(chain[2].position); var dir = to.normalized; float len = to.magnitude;
                var v = mesh.vertices; var w = mesh.boneWeights;
                var dists = new System.Collections.Generic.List<float>();
                for (int i = 0; i < v.Length; i++)
                {
                    if (w[i].boneIndex0 != bi || w[i].weight0 < 0.6f) continue;
                    var p = bind.MultiplyPoint3x4(v[i]); float s = Vector3.Dot(p, dir);
                    if (s < 0f || s > len) continue;
                    dists.Add((p - s * dir).magnitude);
                }
                if (dists.Count < 6) continue;
                dists.Sort();
                // bone space is the bone's own scale: bring it to the hand's
                float world = dists[dists.Count / 2] * bone.lossyScale.x;
                return world / Mathf.Max(1e-4f, h.hand.lossyScale.x);
            }
            return fallback / Mathf.Max(1e-4f, h.hand.lossyScale.x);
        }
    }
}
