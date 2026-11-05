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
        [SerializeField] AnimationClip firingClip;
        [SerializeField] Quaternion reloadHandRotation=Quaternion.identity;
        [SerializeField] Vector3 clipGripOffset;
        [SerializeField] Transform loadedClip;
        [SerializeField] Transform chamberRounds;
        [SerializeField] MeshFilter sleeves;
        Vector3 boltRest, handRest;
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
        Quaternion previousView;
        Vector2 lookLag;
        float walkPhase;
        public float ThumbContactError { get; private set; }

        void Awake()
        {
            if (rifle == null || rightHand == null) { Debug.LogError("M1 presentation requires rifle and corrected hand references.", this); enabled = false; return; }
            if (bolt != null) boltRest = bolt.localPosition;
            player=rifle.GetComponent<PlayerController>();
            if(player!=null)previousView=player.CameraPivot.rotation;
            if (rightHand != null) { handRest = rightHand.localPosition; handRotation = rightHand.localRotation; }
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
            lookLag=Vector2.zero;
            if(player!=null)previousView=player.CameraPivot.rotation;
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if(firingTransform!=null)firingTransform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            rightHand.SetLocalPositionAndRotation(handRest, handRotation);
            ResetFingers();
            if (bolt != null) bolt.localPosition = boltRest;
            if (loadedClip != null) loadedClip.gameObject.SetActive(false);
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
            // Keep the established grip throughout reload; no procedural hand-loading gesture.
            float pose = 0f;
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
            transform.localPosition = new Vector3(-0.06f * pose, 0.025f * pose, 0.17f * pose)+handling;
            transform.localRotation = Quaternion.Euler(-12f * pose+lookLag.y*steadiness,lookLag.x*steadiness,-15f*pose+Mathf.Sin(walkPhase)*.3f*moving*steadiness);
            if (bolt != null)
            {
                float cycle = age >= 0f && age < 0.10f ? Mathf.Sin(age / 0.10f * Mathf.PI) : 0f;
                bool locked = rifle.Magazine != null && rifle.Magazine.InClip == 0;
                float opened = locked ? 1f : cycle;
                bolt.localPosition = boltRest + Vector3.back * (0.045f * opened);
            }
            if (rightHand != null)
            {
                rightHand.SetLocalPositionAndRotation(handRest, handRotation);
                if(rightHandVisual!=null)rightHandVisual.SetPositionAndRotation(rightHand.position,rightHand.rotation);
                UpdateSleeves();
            }
            // Ammo and reload duration still come from M1Rifle. Keep hands on the
            // stock and suppress the detached moving clip used by the old gesture.
            if(loadedClip!=null)loadedClip.gameObject.SetActive(false);
            if(chamberRounds!=null)chamberRounds.gameObject.SetActive(rifle.Magazine!=null&&rifle.Magazine.InClip>0);
            ResetFingers();ThumbContactError=0;
        }

        void ResetFingers()
        {
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(reloadFingers[f,j]!=null)reloadFingers[f,j].localRotation=fingerRest[f,j];
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
                    float spread=(finger-2.5f)*(joint==0?1.5f:0);
                    float grasp=(joint==0?14:joint==1?28:18)*carry;
                    float relaxed=(joint==0?5:joint==1?9:7)*(1f-carry);
                    delta=new Vector3((grasp+relaxed)*release,0,spread*release);
                }
                var basePose=finger==0?fingerRest[finger,joint]:Quaternion.Slerp(fingerRest[finger,joint],fingerOpen[finger,joint],release);
                bone.localRotation=basePose
                    *Quaternion.AngleAxis(delta.x,fingerHingeAxes[finger,joint])
                    *Quaternion.AngleAxis(delta.z,fingerSpreadAxes[finger,joint])
                    *Quaternion.AngleAxis(delta.y,Vector3.up);
            }
            // The four fingers stay relaxed alongside the action.
            // Do not drag the index fingertip onto the thumb's clip target: that creates a pincer.
            // Resetting before the constrained contact solve prevents accumulated twist.
            if(thumbTip==null||loadedClip==null||press<=0)return;
            var target=loadedClip.TransformPoint(new Vector3(.003f,.0475f,.0334f));
            var goal=Vector3.Lerp(thumbTip.position,target,press);
            for(int joint=0;joint<3;joint++){thumbPose[joint]=reloadFingers[0,joint].localRotation;thumbBend[joint]=0;}
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
