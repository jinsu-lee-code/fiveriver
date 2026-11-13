using UnityEngine;

namespace FiveRivers.Combat
{
    // Cosmetic motion follows the magazine state; it never changes shot or reload gameplay timing.
    [DefaultExecutionOrder(50)]
    public sealed class M1Presentation : MonoBehaviour
    {
        [SerializeField] M1Rifle rifle;
        [SerializeField] Transform bolt;
        [SerializeField] Transform rightHand;
        [SerializeField] Transform rightHandVisual;
        [SerializeField] Transform firingTransform;
        [SerializeField] Transform animationFrame;
        [SerializeField] AnimationClip firingClip;
        [SerializeField] Quaternion reloadHandRotation=Quaternion.identity;
        [SerializeField] Vector3 clipGripOffset;
        [SerializeField] Transform loadedClip;
        [SerializeField] Transform chamberRounds;
        [SerializeField] MeshFilter sleeves;
        // Blender-authored right-hand reload; falls back to the procedural gesture below when missing.
        [SerializeField] M1ReloadMotion reloadMotion;
        Transform[] motionBones;
        Quaternion[] motionRest;
        // corrective shape that fills the cupped (grip-modelled) right palm while the reload opens the hand
        SkinnedMeshRenderer palmSkin;
        int palmShape=-1;
        Vector3 boltRest, handRest, clipRestPos;
        Quaternion clipRestRot=Quaternion.identity;
        Quaternion handFrameRest=Quaternion.identity;
        // Reload hand pose in the rifle frame (x right, y up, z toward the muzzle).
        public static Vector3 ReloadFingerDirection=new Vector3(-.15f,-.35f,1f);
        public static Vector3 ReloadPalmNormal=new Vector3(-1f,-.45f,0f);
        public static Vector3 ReloadWristFromClipTop=new Vector3(.04f,.015f,-.045f);
        // How far the rifle is presented during reload (position in metres, rotation in degrees).
        public static Vector3 ReloadPresentOffset=new Vector3(0f,0f,.05f);
        public static Vector3 ReloadPresentEuler=new Vector3(-18f,-4f,-32f);
        // Where the clip comes from and where the hand leaves to, relative to the seated clip (out of view, right of the stock).
        public static Vector3 ReloadFetchOffset=new Vector3(.2f,-.16f,-.1f);
        // Beside the receiver on the right: the clip and hand pass here on the way in and out, never through the rifle.
        public static Vector3 ReloadSideOffset=new Vector3(.08f,.06f,-.02f);
        // Thumb contact point on top of the clip, in the clip's local space, and the same point relative to the
        // clip pivot in the grip's parent space. Recomputed from the seated clip in Awake.
        Vector3 clipTop=new Vector3(.003f,.0475f,.0334f);
        Vector3 clipTopFromPivot=new Vector3(.003f,.0475f,.0334f);
        // Depth of the thumb contact below the top of the rounds' bounding box centre (metres).
        const float ClipContactAboveCentre=.0235f;
        // 0..1 while the right hand is away from the grip for the reload; M1ArmRig uses it to bring the arm up from below.
        public float ReloadArmWeight { get; private set; }
        Quaternion handRotation;
        float shotTime = -10f;
        Mesh sleeveMesh;
        Vector3[] sleeveRest, sleeveMoved;
        Vector3 wristInSleeve;
        Vector3 previousSleeveDelta;
        Quaternion previousSleeveRotation = Quaternion.identity;
        bool initialized;
        readonly Transform[,] reloadFingers=new Transform[5,3];
        readonly Quaternion[,] fingerRest=new Quaternion[5,3];
        readonly Quaternion[,] fingerOpen=new Quaternion[5,3];
        readonly Vector3[,] fingerHingeAxes=new Vector3[5,3];
        readonly Vector3[,] fingerSpreadAxes=new Vector3[5,3];
        Transform thumbTip;
        readonly Quaternion[] thumbPose=new Quaternion[3];
        readonly float[] thumbBend=new float[3];
        PlayerController player;
        WeaponHolder weapons;
        Quaternion previousView;
        Vector2 lookLag;
        float walkPhase;
        public float ThumbContactError { get; private set; }

        void Awake()
        {
            if (rifle == null || rightHand == null) { Debug.LogError("M1 presentation requires rifle and corrected hand references.", this); enabled = false; return; }
            if (bolt != null) boltRest = bolt.localPosition;
            player=rifle.GetComponent<PlayerController>();
            weapons=rifle.GetComponent<WeaponHolder>();
            if(player!=null)previousView=player.CameraPivot.rotation;
            if (rightHand != null) { handRest = rightHand.localPosition; handRotation = rightHand.localRotation; }
            if (loadedClip != null) { clipRestPos = loadedClip.localPosition; clipRestRot = loadedClip.localRotation; }
            UseSeatedClipForReload();
            var fingerBones = (rightHandVisual != null ? rightHandVisual : rightHand).GetComponentsInChildren<Transform>();
            for(int f=0;f<5;f++)for(int joint=0;joint<3;joint++)foreach(var bone in fingerBones)
                if(bone.name=="finger"+(f+1)+"-"+(joint+1)+".R"){reloadFingers[f,joint]=bone;fingerRest[f,joint]=bone.localRotation;}
            var visual=rightHandVisual!=null?rightHandVisual:rightHand;
            var forward=reloadFingers[2,0]!=null?reloadFingers[2,0].position-visual.position:visual.up;
            var across=reloadFingers[1,0]!=null&&reloadFingers[4,0]!=null?reloadFingers[1,0].position-reloadFingers[4,0].position:visual.right;
            var palmNormal=Vector3.Cross(forward,across).normalized;
            if(palmNormal.sqrMagnitude<.01f)palmNormal=visual.forward;
            for(int f=0;f<5;f++)for(int joint=0;joint<3;joint++)
            {
                var bone=reloadFingers[f,joint];if(bone==null)continue;
                var incoming=(bone.position-bone.parent.position).normalized;
                var outgoing=joint<2&&reloadFingers[f,joint+1]!=null?(reloadFingers[f,joint+1].position-bone.position).normalized:bone.up;
                var axis=Vector3.Cross(incoming,outgoing);
                if(axis.sqrMagnitude<.0001f)axis=Vector3.ProjectOnPlane(visual.right,outgoing);
                if(axis.sqrMagnitude<.0001f)axis=Vector3.ProjectOnPlane(visual.forward,outgoing);
                // Imported fingers do not share an axis sign. Positive flexion must curl
                // into the palm, never make one finger bend away while its neighbours grip.
                if(Vector3.Dot(Vector3.Cross(axis,outgoing),palmNormal)<0)axis=-axis;
                fingerHingeAxes[f,joint]=bone.InverseTransformDirection(axis.normalized);
                fingerSpreadAxes[f,joint]=bone.InverseTransformDirection(palmNormal);
            }
            // A relaxed finger pose follows the existing palm orientation and reload path.
            for(int f=1;f<5;f++)for(int j=0;j<3;j++)
            {
                var bone=reloadFingers[f,j];if(bone==null)continue;
                var axis=bone.TransformDirection(fingerHingeAxes[f,j]);
                var a=Vector3.ProjectOnPlane(bone.up,axis);var b=Vector3.ProjectOnPlane(forward,axis);
                float extension=a.sqrMagnitude>.000001f&&b.sqrMagnitude>.000001f?Vector3.SignedAngle(a,b,axis):0;
                bone.localRotation*=Quaternion.AngleAxis(extension,fingerHingeAxes[f,j]);
                fingerOpen[f,j]=bone.localRotation;
            }
            // Rest hand frame (finger direction + palm normal) in the grip's parent space, for the reload pose.
            if(rightHand.parent!=null)
            {
                var restForward=rightHand.parent.InverseTransformDirection(forward);
                var restNormal=rightHand.parent.InverseTransformDirection(palmNormal);
                if(restForward.sqrMagnitude>1e-6f&&restNormal.sqrMagnitude>1e-6f)handFrameRest=Quaternion.LookRotation(restForward,restNormal);
            }
            // fingerOpen 계산 중에 바꾼 자세가 실제 파지의 기준으로 저장되지 않게 한다.
            ResetFingers();
            BindReloadMotion(visual);
            ResetFingers();
            if(reloadFingers[0,2]!=null)
            {
                thumbTip=new GameObject("Thumb_ContactTip").transform;thumbTip.SetParent(reloadFingers[0,2],false);thumbTip.localPosition=Vector3.up*.026f;
            }
            if (sleeves != null && sleeves.sharedMesh != null)
            {
                sleeveMesh = Instantiate(sleeves.sharedMesh);
                sleeveMesh.name = "M1_RuntimeSleeves";
                sleeves.sharedMesh = sleeveMesh;
                sleeveRest = sleeveMesh.vertices;
                sleeveMoved = new Vector3[sleeveRest.Length];
                wristInSleeve = sleeves.transform.InverseTransformPoint(rightHand.position);
            }
            rifle.Fired += OnShot;
            initialized = true;
        }

        void OnDisable()
        {
            if (!initialized) return;
            shotTime = -10f;
            ReloadArmWeight = 0f;
            lookLag=Vector2.zero;
            if(player!=null)previousView=player.CameraPivot.rotation;
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if(firingTransform!=null)firingTransform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            rightHand.SetLocalPositionAndRotation(handRest, handRotation);
            ResetFingers();
            if (bolt != null) bolt.localPosition = boltRest;
            if (loadedClip != null) { loadedClip.SetLocalPositionAndRotation(clipRestPos, clipRestRot); loadedClip.gameObject.SetActive(false); }
            if(chamberRounds!=null)chamberRounds.gameObject.SetActive(true);
            UpdateSleeves();
        }

        void OnDestroy()
        {
            if (rifle != null) rifle.Fired -= OnShot;
            if (sleeveMesh != null) Destroy(sleeveMesh);
        }

        void OnShot() => shotTime = Time.time;

        void LateUpdate()
        {
            float age = Time.time - shotTime;
            if (firingTransform != null)
            {
                firingTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (firingClip != null && age >= 0f && age < firingClip.length)
                {
                    firingClip.SampleAnimation(firingTransform.gameObject, age);
                    float aimScale = Mathf.Lerp(1f, 0.58f, rifle.AimBlend);
                    firingTransform.localPosition *= aimScale;
                    firingTransform.localRotation = Quaternion.Slerp(Quaternion.identity, firingTransform.localRotation, aimScale);
                }
            }
            // Reload gesture: tilt the receiver into view, bring a fresh clip up with the right hand and thumb it in.
            // Timing follows M1Rifle.ReloadProgress, so the gameplay reload duration is unchanged.
            float reloadP = rifle.Reloading ? rifle.ReloadProgress : 0f;
            // reference-video timing: lowered at once, raised back over the last tenth
            float pose = rifle.Reloading ? Window(reloadP, .0f, .08f, .88f, .98f) : 0f;
            float motionP = MotionProgress(reloadP);
            float steadiness=Mathf.Lerp(1f,.12f,rifle.AimBlend)*(1f-pose*.8f);
            float moving=player!=null?player.MoveInput.magnitude:0;
            if(player!=null&&Time.deltaTime>0)
            {
                var delta=(Quaternion.Inverse(previousView)*player.CameraPivot.rotation).eulerAngles;
                previousView=player.CameraPivot.rotation;
                var lag=new Vector2(-Mathf.DeltaAngle(0,delta.y),Mathf.DeltaAngle(0,delta.x))*.006f/Mathf.Max(Time.deltaTime,.001f);
                lag=Vector2.ClampMagnitude(lag,1.1f);
                lookLag=Vector2.Lerp(lookLag,lag,1f-Mathf.Exp(-Time.deltaTime*14));
                walkPhase+=Time.deltaTime*(player.Sprinting?12f:8f)*moving;
            }
            float breath=Mathf.Sin(Time.time*1.45f)*.00065f;
            var handling=new Vector3(Mathf.Sin(walkPhase)*.0025f*moving,breath-Mathf.Abs(Mathf.Cos(walkPhase))*.0018f*moving,0)*steadiness;
            var presentPosition = ReloadPresentOffset * pose + handling;
            var presentRotation = Quaternion.Euler(ReloadPresentEuler.x * pose+lookLag.y*steadiness,ReloadPresentEuler.y*pose+lookLag.x*steadiness,ReloadPresentEuler.z*pose+Mathf.Sin(walkPhase)*.3f*moving*steadiness);
            if (animationFrame != null)
            {
                // 납품 곡선의 장전 동작을 더하되, 확인된 손·클립 접촉과 조준선은 기존 자세를 따른다.
                float weight = weapons != null && weapons.Switching ? 1f : rifle.Reloading ? .22f : 1f - rifle.AimBlend;
                var animatedRotation = animationFrame.localRotation;
                var retainedRotation = Quaternion.Slerp(Quaternion.identity, animatedRotation, weight);
                var correction = Quaternion.Inverse(animatedRotation);
                presentPosition = correction * (retainedRotation * presentPosition + animationFrame.localPosition * (weight - 1f));
                presentRotation = correction * retainedRotation * presentRotation;
            }
            transform.SetLocalPositionAndRotation(presentPosition, presentRotation);
            if (bolt != null)
            {
                float cycle = age >= 0f && age < 0.10f ? Mathf.Sin(age / 0.10f * Mathf.PI) : 0f;
                // An empty rifle holds the bolt open until the new clip seats (the ClipInserted cue at 66%).
                bool locked = rifle.Magazine != null && rifle.Magazine.InClip == 0 && !(rifle.Reloading && reloadP >= .66f);
                float opened = Mathf.Max(locked ? 1f : cycle, rifle.Reloading ? Window(reloadP, .06f, .16f, .70f, .80f) : 0f);
                bolt.localPosition = boltRest + Vector3.back * (0.045f * opened);
            }
            if (rightHand != null)
            {
                rightHand.SetLocalPositionAndRotation(handRest, handRotation);
                ReloadArmWeight=0f;
                if(palmShape>=0)palmSkin.SetBlendShapeWeight(palmShape,0f);
                if (rifle.Reloading && loadedClip != null)
                {
                    if (motionBones != null) PlayAuthoredReload(motionP, reloadP);
                    else PlaceReloadHandAndClip(reloadP);
                }
                if(rightHandVisual!=null)rightHandVisual.SetPositionAndRotation(rightHand.position,rightHand.rotation);
                UpdateSleeves();
            }
            // Ammo and reload duration still come from M1Rifle. The old clip leaves as the bolt opens,
            // the fresh clip is shown from the moment the hand picks it up until the reload completes.
            bool clipInHand, oldClipOut;
            if (motionBones != null)
            {
                // authored: a partial reload pulls the part-used clip out at 28% (the moving clip takes over from the
                // seated one), an empty reload shows only the fresh clip once the hand brings it back (50%)
                bool partial = rifle.ReloadKind == ReloadKind.Partial;
                clipInHand = rifle.Reloading && loadedClip != null && motionP >= (partial ? .28f : .50f);
                oldClipOut = rifle.Reloading && (!partial || motionP >= .28f);
            }
            else
            {
                clipInHand = rifle.Reloading && loadedClip != null && reloadP >= .18f;
                oldClipOut = rifle.Reloading && reloadP >= .12f;
            }
            if(loadedClip!=null)loadedClip.gameObject.SetActive(clipInHand);
            // during a partial reload the seated clip stays until the hand takes it (ammo is already counted out)
            bool showSeated = rifle.Reloading ? !clipInHand && !oldClipOut : rifle.Magazine!=null&&rifle.Magazine.InClip>0;
            if(chamberRounds!=null)chamberRounds.gameObject.SetActive(showSeated);
            if(rifle.Reloading&&motionBones!=null&&loadedClip!=null){ResetFingers();ApplyAuthoredFingers(motionP, reloadP);ThumbContactError=0;}
            else if(rifle.Reloading)AnimateFingers(reloadP);else{ResetFingers();ThumbContactError=0;}
            if (rifle.Reloading) AlignReloadFingers(motionBones != null ? motionP : reloadP, reloadP);
        }

        // The reload clip shows the same mesh and materials as the clip seated in the rifle, at exactly the seated pose,
        // so the hand carries the real clip and the hand-off at the end of the reload is seamless.
        void UseSeatedClipForReload()
        {
            if(loadedClip==null||chamberRounds==null||loadedClip.parent==null)return;
            var seatedFilter=chamberRounds.GetComponent<MeshFilter>();var seatedRenderer=chamberRounds.GetComponent<MeshRenderer>();
            var clipFilter=loadedClip.GetComponent<MeshFilter>();var clipRenderer=loadedClip.GetComponent<MeshRenderer>();
            if(seatedFilter==null||seatedFilter.sharedMesh==null||seatedRenderer==null||clipFilter==null||clipRenderer==null)
            {Debug.LogWarning("M1 reload keeps the legacy clip mesh: seated or reload clip has no mesh renderer.",this);return;}
            if(loadedClip.parent!=rightHand.parent)
            {Debug.LogWarning("M1 reload clip and right hand target must share a parent; keeping the legacy clip.",this);return;}
            if(!chamberRounds.gameObject.activeInHierarchy||!seatedRenderer.enabled)
            {Debug.LogWarning("M1 seated clip is hidden at start; reload clip contact point uses the legacy value.",this);return;}
            var parent=loadedClip.parent;
            clipRestPos=parent.InverseTransformPoint(chamberRounds.position);
            clipRestRot=Quaternion.Inverse(parent.rotation)*chamberRounds.rotation;
            var ps=parent.lossyScale;var cs=chamberRounds.lossyScale;
            if(Mathf.Abs(ps.x)<1e-6f||Mathf.Abs(ps.y)<1e-6f||Mathf.Abs(ps.z)<1e-6f)return;
            loadedClip.localScale=new Vector3(cs.x/ps.x,cs.y/ps.y,cs.z/ps.z);
            clipFilter.sharedMesh=seatedFilter.sharedMesh;
            clipRenderer.sharedMaterials=seatedRenderer.sharedMaterials;
            loadedClip.SetLocalPositionAndRotation(clipRestPos,clipRestRot);
            // The imported clip mesh has empty mesh bounds, so use the seated renderer's world bounds instead.
            var centre=parent.InverseTransformPoint(seatedRenderer.bounds.center);
            clipTopFromPivot=centre-clipRestPos+Vector3.up*ClipContactAboveCentre;
            clipTop=loadedClip.InverseTransformPoint(parent.TransformPoint(clipRestPos+clipTopFromPivot));
        }

        void BindReloadMotion(Transform visual)
        {
            if(reloadMotion==null)reloadMotion=Resources.Load<M1ReloadMotion>("M1_ReloadMotion");
            if(reloadMotion==null||!reloadMotion.IsValid||visual==null)return;
            var found=new System.Collections.Generic.Dictionary<string,Transform>();
            foreach(var t in visual.GetComponentsInChildren<Transform>(true))if(!found.ContainsKey(t.name))found.Add(t.name,t);
            var bound=new Transform[reloadMotion.bones.Length];
            for(int i=0;i<bound.Length;i++)
            {
                if(!found.TryGetValue(reloadMotion.bones[i],out bound[i]))
                {Debug.LogWarning("M1 reload motion bone missing: "+reloadMotion.bones[i]+"; using the procedural reload.",this);return;}
            }
            motionBones=bound;
            motionRest=new Quaternion[bound.Length];
            for(int i=0;i<bound.Length;i++)motionRest[i]=bound[i].localRotation;
            if(found.TryGetValue("metacarpal2.R",out var palmBone))
                foreach(var skin in transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if(System.Array.IndexOf(skin.bones,palmBone)>=0){M1HandProportion.Apply(skin);palmSkin=skin;palmShape=M1PalmFlatten.Add(skin);break;}
        }

        // The authored motion is the partial reload (take the old clip out, fetch, thumb the new one home at 66%).
        // An empty reload has no clip to take out: it starts as the hand leaves for the fresh clip, and is spread so
        // the thumb still seats it at 66% of the reload, in time with the insertion sound and the bolt.
        float MotionProgress(float p)
        {
            if (!rifle.Reloading || rifle.ReloadKind != ReloadKind.Empty) return p;
            if (p < .66f) return Mathf.Lerp(.44f, .66f, p / .66f);
            return p;
        }

        void PlayAuthoredReload(float p, float reloadP)
        {
            reloadMotion.SampleWrist(p,out var wrist,out var wristRotation,out var clipOffset);
            // 오른손이 총 위에서 떨어진 뒤 파지 쪽으로 손목부터 돌아간다.
            float positionWeight = Window(reloadP, 0f, .10f, .88f, 1f);
            float rotationWeight = Window(reloadP, 0f, .10f, .76f, .98f);
            var localRotation = Quaternion.Slerp(handRotation, wristRotation, rotationWeight);
            float release = Window(p, .73f, .81f, .86f, .94f);
            if (release > 0f)
            {
                var palmFrame = localRotation * Quaternion.Inverse(handRotation) * handFrameRest;
                var axis = palmFrame * Vector3.forward;
                var palm = Vector3.ProjectOnPlane(palmFrame * Vector3.up, axis);
                var down = player != null ? rightHand.parent.InverseTransformDirection(-player.CameraPivot.up) : Vector3.down;
                var targetPalm = Vector3.ProjectOnPlane(down, axis);
                float roll = Mathf.Clamp(Vector3.SignedAngle(palm, targetPalm, axis), -66f, 66f);
                localRotation = Quaternion.AngleAxis(roll * release, axis) * localRotation;
            }
            rightHand.SetLocalPositionAndRotation(Vector3.Lerp(handRest, wrist, positionWeight), localRotation);
            loadedClip.SetLocalPositionAndRotation(clipRestPos+clipOffset,clipRestRot);
            ReloadArmWeight=Window(p,.04f,.16f,.72f,.98f);
            // open-hand phases: carrying the clip and resting on the receiver after it is home
            if(palmShape>=0)palmSkin.SetBlendShapeWeight(palmShape,100f*Mathf.Max(Window(p,.22f,.30f,.56f,.62f),Window(p,.62f,.68f,.84f,.90f)));
        }

        void ApplyAuthoredFingers(float p, float reloadP)
        {
            for(int i=0;i<motionBones.Length;i++)motionBones[i].localRotation=reloadMotion.SampleBone(i,p);
            RelaxIndex(p);
            ShapeReloadRelease(p);

            // 접촉 구간을 지난 뒤 손가락 전체를 같은 흐름으로 파지에 돌려보낸다.
            float authoredWeight = Window(reloadP, 0f, .10f, .74f, .94f);
            if (authoredWeight >= 1f) return;
            for (int i = 0; i < motionBones.Length; i++)
            {
                var bone = motionBones[i];
                bone.localRotation = Quaternion.Slerp(motionRest[i], bone.localRotation, authoredWeight);
            }
        }

        void AlignReloadFingers(float progress, float reloadProgress)
        {
            // 클립을 꺼내고 가져와 넣는 동안 네 손가락을 중지와 나란히 유지합니다.
            // 원래 중지의 굽힘과 각 손가락의 길이는 남기고 엄지와 손목은 바꾸지 않습니다.
            float weight = Window(progress, .16f, .24f, .86f, .96f)
                * Window(reloadProgress, 0f, .10f, .86f, .96f);
            if (weight <= 0f) return;
            var middleBase = reloadFingers[2, 0];
            var middleJoint = reloadFingers[2, 1];
            var middleTip = reloadFingers[2, 2];
            if (middleBase == null || middleJoint == null || middleTip == null) return;
            var proximal = middleJoint.position - middleBase.position;
            var intermediate = middleTip.position - middleJoint.position;
            var distal = middleTip.up;
            for (int finger = 1; finger < 5; finger++)
            {
                if (finger == 2) continue;
                for (int joint = 0; joint < 3; joint++)
                {
                    var bone = reloadFingers[finger, joint];
                    if (bone == null) continue;
                    var child = joint < 2 ? reloadFingers[finger, joint + 1] : null;
                    var outgoing = child != null ? child.position - bone.position : bone.up;
                    var direction = joint == 0 ? proximal : joint == 1 ? intermediate : distal;
                    if (outgoing.sqrMagnitude < 1e-10f || direction.sqrMagnitude < 1e-10f) continue;
                    var turn = Quaternion.FromToRotation(outgoing, direction);
                    bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
                }
            }
        }

        void ShapeReloadRelease(float progress)
        {
            // 클립에서 떨어질 때 검지만 펴거나 끝 관절부터 접히지 않도록 한다.
            float weight = Window(progress, .73f, .80f, .86f, .93f);
            if (weight <= 0f) return;
            var index = reloadFingers[1, 0];
            var middle = reloadFingers[2, 0];
            var little = reloadFingers[4, 0];
            if (index == null || middle == null || little == null) return;
            for (int i = 0; i < motionBones.Length; i++)
                if (reloadMotion.bones[i].StartsWith("metacarpal", System.StringComparison.Ordinal))
                    motionBones[i].localRotation = Quaternion.Slerp(motionBones[i].localRotation, motionRest[i], .7f * weight);
            var hand = rightHandVisual != null ? rightHandVisual : rightHand;
            var forward = (middle.position - hand.position).normalized;
            var across = Vector3.ProjectOnPlane(index.position - little.position, forward).normalized;
            var palm = Vector3.Cross(forward, across);
            if (palm.sqrMagnitude < .01f) return;
            for (int finger = 1; finger < 5; finger++)
            {
                for (int joint = 0; joint < 3; joint++)
                {
                    var bone = reloadFingers[finger, joint];
                    if (bone == null) continue;
                    var child = joint < 2 ? reloadFingers[finger, joint + 1] : null;
                    var outgoing = child != null ? child.position - bone.position : bone.up;
                    float flex = joint == 0 ? 23f : joint == 1 ? 48f : 62f;
                    var direction = Quaternion.AngleAxis(flex + finger, -across) * forward
                        + across * ((2.5f - finger) * .035f);
                    var turn = Quaternion.FromToRotation(outgoing, direction);
                    bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
                }
            }
            RelaxThumb(weight);
        }

        // Once the clip is home the authored index stays almost straight (knuckle 13° against the middle finger's 74°)
        // and reads as pointing; curl it with the other fingers while the hand rests over the receiver.
        static readonly float[] IndexRelaxCurl={28f,16f,18f};
        void RelaxIndex(float p)
        {
            float w=Window(p,.64f,.70f,.84f,.92f);
            if(w<=0f)return;
            int index=System.Array.IndexOf(reloadMotion.bones,"finger2-1.R");
            int middle=System.Array.IndexOf(reloadMotion.bones,"finger3-1.R");
            int middlePalm=System.Array.IndexOf(reloadMotion.bones,"metacarpal2.R");
            if(index<0||middle<0||middlePalm<0||index+2>=motionBones.Length)return;
            // flexion axis taken from the bent middle finger, which runs alongside the index
            var knuckle=motionBones[middle];var palmDir=knuckle.position-motionBones[middlePalm].position;
            var axis=Vector3.Cross(palmDir,motionBones[middle+1].position-knuckle.position);
            if(axis.sqrMagnitude<1e-10f)return;
            axis.Normalize();
            for(int j=0;j<3;j++)
            {
                var bone=motionBones[index+j];
                bone.rotation=Quaternion.AngleAxis(IndexRelaxCurl[j]*w,axis)*bone.rotation;
            }
            RelaxThumb(w);
        }

        // In the same phase the authored thumb stands up off the back of the hand, 70 mm from the index knuckle;
        // bring it in beside the curled index so the hand rests as one relaxed shape.
        const float ThumbRelaxPull=.6f;
        void RelaxThumb(float w)
        {
            int thumb=System.Array.IndexOf(reloadMotion.bones,"finger1-1.R");
            int index=System.Array.IndexOf(reloadMotion.bones,"finger2-2.R");
            if(thumb<0||index<0||thumb+2>=motionBones.Length)return;
            var root=motionBones[thumb];var last=motionBones[thumb+2];
            var tip=last.position+(last.position-motionBones[thumb+1].position).normalized*.03f;
            var turn=Quaternion.FromToRotation(tip-root.position,motionBones[index].position-root.position);
            root.rotation=Quaternion.Slerp(Quaternion.identity,turn,ThumbRelaxPull*w)*root.rotation;
        }

        void PlaceReloadHandAndClip(float p)
        {
            // Clip path in the rifle frame: up from below the view, beside the receiver, over the opening, then pressed in.
            float toSide=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.12f,.34f,p));
            float over=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.34f,.44f,p));
            float seat=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.44f,.66f,p));
            var below=clipRestPos+ReloadFetchOffset;
            var side=clipRestPos+ReloadSideOffset;
            var above=clipRestPos+new Vector3(0f,.05f,0f);
            var carried=Vector3.Lerp(Vector3.Lerp(below,side,toSide),above,over);
            var clipPos=Vector3.Lerp(carried,clipRestPos,seat);
            loadedClip.SetLocalPositionAndRotation(clipPos,clipRestRot);
            // The hand holds the clip from the side. It fetches the clip out of view and leaves the same way, so it
            // never sweeps across the lens. The clip goes all the way in; the hand follows only part of the way and
            // the thumb finishes the push, so the fingers never sink into the receiver.
            var reloadRotation=Quaternion.LookRotation(ReloadFingerDirection,ReloadPalmNormal)*Quaternion.Inverse(handFrameRest)*handRotation;
            var grip=clipTopFromPivot+ReloadWristFromClipTop;
            var low=below+grip;
            var handSeated=Vector3.Lerp(above,clipRestPos,.15f)+grip;
            Vector3 handPos;
            if(p<.12f)handPos=Vector3.Lerp(handRest,low,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.04f,.12f,p)));
            else if(p<.70f)handPos=Vector3.Lerp(carried,clipRestPos,seat*.15f)+grip;
            else
            {
                var leave=Vector3.Lerp(handSeated,side+grip,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.70f,.78f,p)));
                leave=Vector3.Lerp(leave,low,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.78f,.90f,p)));
                handPos=Vector3.Lerp(leave,handRest,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.90f,.98f,p)));
            }
            float away=Window(p,.04f,.16f,.72f,.98f);
            ReloadArmWeight=away;
            rightHand.SetLocalPositionAndRotation(handPos,Quaternion.Slerp(handRotation,reloadRotation,away));
        }

        void ResetFingers()
        {
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(reloadFingers[f,j]!=null)reloadFingers[f,j].localRotation=fingerRest[f,j];
            // the authored reload also drives the metacarpals; put them back too
            if(motionBones!=null&&motionRest!=null)for(int i=0;i<motionBones.Length;i++)if(motionBones[i]!=null)motionBones[i].localRotation=motionRest[i];
        }

        void AnimateFingers(float p)
        {
            ResetFingers();ThumbContactError=0;
            if(!rifle.Reloading)return;
            float release=Window(p,.08f,.22f,.78f,.96f),carry=Window(p,.22f,.34f,.46f,.59f);
            float press=Window(p,.43f,.56f,.64f,.70f),clear=Window(p,.66f,.74f,.80f,.93f);
            for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++)
            {
                var bone=reloadFingers[finger,joint];if(bone==null)continue;Vector3 delta;
                if(finger==0)delta=new Vector3(-(12+joint*3)*release+(10+joint*2)*carry+(8+joint*2)*press-10*clear,joint==0?-6*release:0,joint==0?-8*release+6*carry:0);
                else
                {
                    float spread=(finger-2.5f)*(joint==0?7f:0);
                    float grasp=(joint==0?14:joint==1?28:18)*carry;
                    float relaxed=(joint==0?5:joint==1?9:7)*(1f-carry);
                    delta=new Vector3((grasp+relaxed)*release,0,spread*release);
                }
                var basePose=finger==0?fingerRest[finger,joint]:Quaternion.Slerp(fingerRest[finger,joint],fingerOpen[finger,joint],release*(1f-.6f*Mathf.Max(press,clear)));
                bone.localRotation=basePose
                    *Quaternion.AngleAxis(delta.x,fingerHingeAxes[finger,joint])
                    *Quaternion.AngleAxis(delta.z,fingerSpreadAxes[finger,joint])
                    *Quaternion.AngleAxis(delta.y,Vector3.up);
            }
            // The four fingers stay relaxed alongside the action.
            // Do not drag the index fingertip onto the thumb's clip target: that creates a pincer.
            // Resetting before the constrained contact solve prevents accumulated twist.
            if(thumbTip==null||loadedClip==null||press<=0)return;
            var target=loadedClip.TransformPoint(clipTop);
            var goal=Vector3.Lerp(thumbTip.position,target,press);
            for(int joint=0;joint<3;joint++){if(reloadFingers[0,joint]==null)return;thumbPose[joint]=reloadFingers[0,joint].localRotation;thumbBend[joint]=0;}
            for(int iteration=0;iteration<12;iteration++)for(int joint=2;joint>=0;joint--)
            {
                var bone=reloadFingers[0,joint];if(bone==null)continue;
                var a=thumbTip.position-bone.position;var b=goal-bone.position;
                if(a.sqrMagnitude<1e-8f||b.sqrMagnitude<1e-8f)continue;
                if(joint==0)
                {
                    // Opposition comes from the thumb base; its two distal joints
                    // flex on their hinge axes instead of twisting sideways.
                    var turn=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(a,b),7f);
                    var candidate=Quaternion.Inverse(bone.parent.rotation)*(turn*bone.rotation);
                    bone.localRotation=Quaternion.RotateTowards(thumbPose[joint],candidate,65f);
                }
                else
                {
                    float correction=HingeCorrection(a,b,bone.TransformDirection(fingerHingeAxes[0,joint]));
                    float range=joint==1?40f:30f;
                    thumbBend[joint]=Mathf.Clamp(thumbBend[joint]+correction,-range,range);
                    bone.localRotation=thumbPose[joint]*Quaternion.AngleAxis(thumbBend[joint],fingerHingeAxes[0,joint]);
                }
            }
            ThumbContactError=Vector3.Distance(thumbTip.position,target);
        }

        public static float HingeCorrection(Vector3 current,Vector3 desired,Vector3 hingeAxis)
        {
            if(hingeAxis.sqrMagnitude<.000001f)return 0;
            var a=Vector3.ProjectOnPlane(current,hingeAxis);var b=Vector3.ProjectOnPlane(desired,hingeAxis);
            if(a.sqrMagnitude<.00000001f||b.sqrMagnitude<.00000001f)return 0;
            return Mathf.Clamp(Vector3.SignedAngle(a,b,hingeAxis),-7f,7f);
        }

        void UpdateSleeves()
        {
            if (sleeveMesh == null) return;
            Vector3 delta = sleeves.transform.InverseTransformVector(rightHand.parent.TransformVector(rightHand.localPosition - handRest));
            var relativeFrame = Quaternion.Inverse(sleeves.transform.rotation) * rightHand.parent.rotation;
            var localDelta = rightHand.localRotation * Quaternion.Inverse(handRotation);
            var rotation = relativeFrame * localDelta * Quaternion.Inverse(relativeFrame);
            if ((delta - previousSleeveDelta).sqrMagnitude < 0.00000001f && Quaternion.Angle(rotation, previousSleeveRotation) < 0.05f) return;
            previousSleeveDelta = delta;
            previousSleeveRotation = rotation;
            for (int i = 0; i < sleeveRest.Length; i++)
            {
                var v = sleeveRest[i];
                float weight = v.x > 0f ? 1f - Mathf.SmoothStep(0f, 1f, Vector3.Distance(v, wristInSleeve) / 0.32f) : 0f;
                var rotated = wristInSleeve + rotation * (v - wristInSleeve);
                sleeveMoved[i] = Vector3.Lerp(v, rotated + delta, weight);
            }
            sleeveMesh.vertices = sleeveMoved;
            sleeveMesh.RecalculateBounds();
            sleeveMesh.RecalculateNormals();
        }

        static float Window(float p, float start, float peak, float fall, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, peak, p)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fall, end, p)));
        }
    }
}
