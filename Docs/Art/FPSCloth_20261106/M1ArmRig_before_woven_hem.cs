using UnityEngine;
using System.Linq;

namespace FiveRivers.Combat
{
    // Mixamo shoulder/elbow motion, followed by wrist IK to retain contact with the M1.
    [DefaultExecutionOrder(100)]
    public sealed class M1ArmRig : MonoBehaviour
    {
        [SerializeField] M1Rifle rifle;
        [SerializeField] Camera view;
        [SerializeField] AnimationClip fireMotion;
        [SerializeField] Transform leftUpper, leftForearm, leftWrist, leftTarget;
        [SerializeField] Transform rightUpper, rightForearm, rightWrist, rightTarget;
        [SerializeField] Transform leftVisualWrist, rightVisualWrist;
        [SerializeField] Transform leftCuff, rightCuff;
        [SerializeField] Transform leftCuffAnchor, rightCuffAnchor;
        [SerializeField] Vector3[] leftCuffRim, rightCuffRim;
        [SerializeField] Vector3 leftCuffAxis=Vector3.up, rightCuffAxis=Vector3.up;
        [SerializeField] float leftCuffOverlap=.008f, rightCuffOverlap=.018f;

        [SerializeField] Vector3 leftSkinCap,rightSkinCap;
        [SerializeField] float leftSkinRadius,rightSkinRadius;
        Mesh leftSleeveMesh,rightSleeveMesh;
        Vector3[] leftSleeveVertices,rightSleeveVertices;
        const int LeftBodyRings=65;
        readonly Vector3[] sleeveCentres=new Vector3[LeftBodyRings],sleeveTangents=new Vector3[LeftBodyRings];
        readonly Vector4[] leftClothTangents=new Vector4[(LeftBodyRings+2)*33],rightClothTangents=new Vector4[35*33];
        Quaternion leftWristRest, rightWristRest;
        Quaternion leftForearmHandRest,rightForearmHandRest;
        Transform[] bones;
        Vector3[] positions;
        Quaternion[] rotations;
        float firedAt = -10f;
        public float LeftWristError { get; private set; }
        public float RightWristError { get; private set; }
        public float SourceMotionTime { get; private set; }
        public float LeftForearmTwist { get; private set; }
        public float RightForearmTwist { get; private set; }

        void Awake()
        {
            if(rifle==null || view==null || leftTarget==null || rightTarget==null || leftUpper==null || leftForearm==null || leftWrist==null || rightUpper==null || rightForearm==null || rightWrist==null)
            {
                Debug.LogError("M1 arm rig is missing a wrist/arm/camera reference.",this);enabled=false;return;
            }
            bones = GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("mixamorig:")).ToArray();
            positions = new Vector3[bones.Length];
            rotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                positions[i] = bones[i].localPosition;
                rotations[i] = bones[i].localRotation;
            }
            rifle.Fired += OnFire;
            leftWristRest=leftWrist.localRotation;
            rightWristRest=rightWrist.localRotation;
            leftForearmHandRest=Quaternion.Inverse(leftForearm.rotation)*leftTarget.rotation;
            rightForearmHandRest=Quaternion.Inverse(rightForearm.rotation)*rightTarget.rotation;
            leftSleeveMesh=Instantiate(leftCuff.GetComponent<MeshFilter>().sharedMesh);
            RefineLeftCloth(leftSleeveMesh);
            rightSleeveMesh=Instantiate(rightCuff.GetComponent<MeshFilter>().sharedMesh);
            leftCuff.GetComponent<MeshFilter>().sharedMesh=leftSleeveMesh;
            rightCuff.GetComponent<MeshFilter>().sharedMesh=rightSleeveMesh;
            leftSleeveVertices=new Vector3[leftSleeveMesh.vertexCount];rightSleeveVertices=new Vector3[rightSleeveMesh.vertexCount];
        }

        void OnDestroy() { if (rifle != null) rifle.Fired -= OnFire; if(leftSleeveMesh!=null)Destroy(leftSleeveMesh);if(rightSleeveMesh!=null)Destroy(rightSleeveMesh); }
        void OnDisable() { firedAt = -10f; }
        void OnFire() => firedAt = Time.time;

        void LateUpdate()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = positions[i];
                bones[i].localRotation = rotations[i];
            }
            transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
            float age = Time.time - firedAt;
            SourceMotionTime = fireMotion != null && age < 0.28f ? Mathf.Clamp01(age / 0.28f) * fireMotion.length : 0f;
            if (fireMotion != null && age >= 0f && age < 0.28f)
                fireMotion.SampleAnimation(gameObject, SourceMotionTime);
            Solve(leftUpper, leftForearm, leftWrist, leftTarget);
            Solve(rightUpper, rightForearm, rightWrist, rightTarget);
            // Palm rotation is shared with the forearm, rather than appearing as
            // an unconstrained swivel at the wrist. The grip target stays fixed.
            LeftForearmTwist=ShareForearmTwist(leftForearm,leftWrist,leftTarget,leftForearmHandRest);
            RightForearmTwist=ShareForearmTwist(rightForearm,rightWrist,rightTarget,rightForearmHandRest);
            leftWrist.localRotation=leftWristRest;
            rightWrist.localRotation=rightWristRest;
            if(leftVisualWrist!=null)leftVisualWrist.SetPositionAndRotation(leftTarget.position,leftTarget.rotation);
            if(rightVisualWrist!=null)rightVisualWrist.SetPositionAndRotation(rightTarget.position,rightTarget.rotation);
            FitCuff(leftCuff,leftSleeveMesh,leftSleeveVertices,leftUpper.position,leftForearm.position,leftCuffAnchor!=null?leftCuffAnchor:leftTarget,leftSkinCap,leftSkinRadius,leftCuffRim,leftCuffAxis,leftCuffOverlap,leftUpper.right,leftForearm.right);
            FitCuff(rightCuff,rightSleeveMesh,rightSleeveVertices,rightUpper.position,rightForearm.position,rightCuffAnchor!=null?rightCuffAnchor:rightTarget,rightSkinCap,rightSkinRadius,rightCuffRim,rightCuffAxis,rightCuffOverlap,rightUpper.right,rightForearm.right);
            LeftWristError = Vector3.Distance(leftWrist.position, leftTarget.position);
            RightWristError = Vector3.Distance(rightWrist.position, rightTarget.position);
        }

        // A continuous shoulder-to-wrist surface avoids open elbow tubes and overlapping skins.
        void FitCuff(Transform sleeve,Mesh mesh,Vector3[] vertices,Vector3 shoulder,Vector3 elbow,Transform hand,Vector3 localCap,float skinRadius,Vector3[] rim,Vector3 localAxis,float overlap,Vector3 upperSide,Vector3 forearmSide)
        {
            sleeve.SetPositionAndRotation(view.transform.position,view.transform.rotation);
            sleeve.localScale=Vector3.one;
            var cap=hand.TransformPoint(localCap);
            bool fittedRim=rim!=null&&rim.Length==32;
            var wristAxis=hand.TransformDirection(localAxis).normalized;
            var end=cap+wristAxis*overlap;
            var upperAxis=(elbow-shoulder).normalized;
            var lowerAxis=(cap-elbow).normalized;
            var bendAxis=(upperAxis+lowerAxis).normalized;
            // Only cover the hidden shoulder join; the old 12cm extension made
            // the narrow sleeve read as a single overlong arm in first person.
            var start=shoulder-upperAxis*.025f;
            float upperLength=Vector3.Distance(start,elbow),lowerLength=Vector3.Distance(elbow,end);
            const int count=32;
            bool leftCloth=sleeve==leftCuff;
            int bodyRings=leftCloth?LeftBodyRings:33;
            var clothTangents=leftCloth?leftClothTangents:rightClothTangents;
            var centres=sleeveCentres;
            var tangents=sleeveTangents;
            Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
            {float u=1f-t;return u*u*u*a+3f*u*u*t*b+3f*u*t*t*c+t*t*t*d;}
            for(int ring=0;ring<bodyRings;ring++)
            {
                float t=ring/(float)(bodyRings-1);
                bool upper=t<=.5f;float f=upper?t*2f:(t-.5f)*2f;
                Vector3 a=upper?start:elbow,d=upper?elbow:end;
                // The limb segments stay mostly straight. Round only the elbow
                // and cuff joins instead of bending the entire forearm like a hose.
                Vector3 b=upper?start+upperAxis*upperLength*.33f:elbow+bendAxis*lowerLength*.08f;
                Vector3 c=upper?elbow-bendAxis*upperLength*.08f:end-wristAxis*lowerLength*.08f;
                centres[ring]=Bezier(a,b,c,d,f);
                tangents[ring]=(3f*(1f-f)*(1f-f)*(b-a)+6f*(1f-f)*f*(c-b)+3f*f*f*(d-c)).normalized;
            }
            // Parallel transport from the measured wrist prevents ring flips under hand rotation.
            var side=fittedRim?hand.TransformVector(rim[0]-localCap).normalized:hand.right;var previous=wristAxis;
            for(int ring=bodyRings-1;ring>=0;ring--)
            {
                side=Quaternion.FromToRotation(previous,tangents[ring])*side;
                side=Vector3.ProjectOnPlane(side,tangents[ring]).normalized;
                float t=ring/(float)(bodyRings-1);
                // Keep the upper sleeve attached to the arm's frame and spread
                // pronation down the forearm. The measured cuff still follows the hand.
                var reference=Vector3.Lerp(upperSide,forearmSide,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.85f,t)));
                reference=Vector3.ProjectOnPlane(reference,tangents[ring]);
                if(reference.sqrMagnitude<.0001f)reference=Vector3.ProjectOnPlane(view.transform.up,tangents[ring]);
                reference.Normalize();
                float roll=Vector3.SignedAngle(reference,side,tangents[ring]);
                var frameSide=Quaternion.AngleAxis(roll*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1f,t)),tangents[ring])*reference;
                var other=Vector3.Cross(tangents[ring],frameSide).normalized;
                // Broad upper arm, narrower elbow, proximal forearm bulk and a
                // gradual wrist taper. Keep the delivered hand/cuff rim unchanged.
                float radius;
                if(t<.18f)radius=Mathf.Lerp(.050f,.052f,Mathf.SmoothStep(0,1,t/.18f));
                else if(t<.50f)radius=Mathf.Lerp(.052f,.044f,Mathf.SmoothStep(0,1,(t-.18f)/.32f));
                else if(t<.64f)radius=Mathf.Lerp(.044f,.047f,Mathf.SmoothStep(0,1,(t-.50f)/.14f));
                else radius=Mathf.Lerp(.047f,Mathf.Max(.029f,skinRadius+.003f),Mathf.SmoothStep(0,1,(t-.64f)/.36f));
                var inside=Vector3.ProjectOnPlane(-upperAxis+lowerAxis,tangents[ring]).normalized;
                float bend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(15,100,Vector3.Angle(upperAxis,lowerAxis)));
                for(int i=0;i<=count;i++)
                {
                    float angle=i*Mathf.PI*2f/count;
                    var radial=frameSide*Mathf.Cos(angle)+other*Mathf.Sin(angle);
                    float compression=Mathf.Pow(Mathf.Max(0,Vector3.Dot(radial,inside)),2)*bend;
                    // A few unequal compression folds on the inside of the bent
                    // elbow, rather than a periodic pattern wrapped around a tube.
                    float fold=-.0015f*Mathf.Exp(-Mathf.Pow((t-.46f)/.027f,2))
                        +.0011f*Mathf.Exp(-Mathf.Pow((t-.50f)/.034f,2))
                        -.0013f*Mathf.Exp(-Mathf.Pow((t-.55f)/.022f,2));
                    float foldedRadius=radius+fold*compression;
                    float depth=Mathf.Lerp(.93f,.98f,Mathf.SmoothStep(0,1,t));
                    var point=centres[ring]+(frameSide*Mathf.Cos(angle)+other*Mathf.Sin(angle)*depth)*foldedRadius;
                    if(fittedRim)
                    {
                        // The delivered support hand has a different wrist cut and scale from the
                        // hidden skeletal hand. Follow its measured rim, including during recoil.
                        var contour=hand.TransformVector(rim[i%count]-localCap);
                        var fitted=centres[ring]+contour*1.06f;
                        float blend=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.86f,1f,t));
                        point=Vector3.Lerp(point,fitted,blend);
                    }
                    // Relief belongs only to the visible cloth surface. Keep the
                    // arm centreline, nominal radius and measured wrist rim intact.
                    float clothRelief=leftCloth?ForearmClothRelief(t,angle):0f;
                    point+=(point-centres[ring]).normalized*clothRelief;
                    vertices[ring*(count+1)+i]=sleeve.InverseTransformPoint(point);
                    var tangent=sleeve.InverseTransformDirection(-frameSide*Mathf.Sin(angle)+other*Mathf.Cos(angle));
                    clothTangents[ring*(count+1)+i]=new Vector4(tangent.x,tangent.y,tangent.z,1f);
                }
                previous=tangents[ring];
            }
            // The folded lip sits inside the measured wrist, independent of forearm length.
            for(int ring=bodyRings;ring<bodyRings+2;ring++)for(int i=0;i<=count;i++)
            {
                float angle=i*Mathf.PI*2f/count;
                var centre=cap+wristAxis*(ring==bodyRings?overlap:Mathf.Max(.001f,overlap-.005f));
                var offset=fittedRim?hand.TransformVector(rim[i%count]-localCap)*(ring==bodyRings?.96f:.91f):(hand.right*Mathf.Cos(angle)-hand.forward*Mathf.Sin(angle))*(skinRadius-.003f);
                vertices[ring*(count+1)+i]=sleeve.InverseTransformPoint(centre+offset);
                var tangent=sleeve.InverseTransformDirection(-hand.right*Mathf.Sin(angle)-hand.forward*Mathf.Cos(angle));
                clothTangents[ring*(count+1)+i]=new Vector4(tangent.x,tangent.y,tangent.z,1f);
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();
            // UVs wrap at duplicated seam vertices; share their normal to avoid
            // the long dark strip that made the sleeve look split down its length.
            var normals=mesh.normals;
            for(int ring=0;ring<bodyRings+2;ring++)
            {
                int a=ring*33,b=a+32;var normal=(normals[a]+normals[b]).normalized;
                normals[a]=normals[b]=normal;
            }
            mesh.normals=normals;
            // The left folds change the angular AND lengthwise surface slope.
            // Derive their normal-map frame from the deformed surface, not a cylinder.
            if(leftCloth)mesh.RecalculateTangents();
            else mesh.tangents=clothTangents;
            mesh.RecalculateBounds();
        }
        // Give the visible left cloth enough samples for a crease to have a ridge,
        // valley and recovery. The shared source mesh and right sleeve are untouched.
        static void RefineLeftCloth(Mesh mesh)
        {
            var source=mesh.vertices;
            const int segments=32,stride=segments+1;
            if(source.Length!=35*stride)return;
            int rings=LeftBodyRings+2;
            var vertices=new Vector3[rings*stride];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[(rings-1)*segments*6];
            for(int ring=0;ring<rings;ring++)
            {
                float sourceRing=ring<LeftBodyRings?ring*32f/(LeftBodyRings-1):32f+ring-(LeftBodyRings-1);
                int a=Mathf.FloorToInt(sourceRing),b=Mathf.Min(34,a+1);
                for(int i=0;i<=segments;i++)
                {
                    int index=ring*stride+i;
                    vertices[index]=Vector3.Lerp(source[a*stride+i],source[b*stride+i],sourceRing-a);
                    uv[index]=new Vector2(i/(float)segments,sourceRing/34f);
                }
            }
            int triangle=0;
            for(int ring=0;ring<rings-1;ring++)for(int i=0;i<segments;i++)
            {
                int a=ring*stride+i,b=a+stride;
                triangles[triangle++]=a;triangles[triangle++]=a+1;triangles[triangle++]=b;
                triangles[triangle++]=a+1;triangles[triangle++]=b+1;triangles[triangle++]=b;
            }
            mesh.Clear();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        }

        // Unequal diagonal folds sit on the measured near face of the support sleeve.
        // Their angles are fixed in the cloth frame, so they cannot slide with the camera.
        // The final measured wrist ring and the folded lip are not displaced.
        static float ForearmClothRelief(float t,float angle)
        {
            if(t<=.54f || t>=.975f)return 0f;
            float Crease(float centre,float width,float amplitude,float around,float skew,float phase)
            {
                float relative=angle-around;
                float patch=Mathf.Pow(.5f+.5f*Mathf.Cos(relative),2.2f);
                float path=centre+skew*Mathf.Sin(relative)+.006f*Mathf.Sin(2f*relative+phase);
                float d=(t-path)/width;
                // A narrower compressed trough beside a softer crest gives cotton
                // a folded plane, rather than four rounded swellings of a rubber tube.
                float pair=Mathf.Exp(-d*d)-.82f*Mathf.Exp(-Mathf.Pow((d+1.22f)/.83f,2f));
                return amplitude*patch*pair;
            }
            // Near-face angle falls from 4.5 at mid-forearm to 2.4 at the cuff
            // because the cloth frame follows the delivered support-hand pronation.
            float relief=Crease(.620f,.027f,.0095f,4.55f,.028f,.34f)
                +Crease(.722f,.025f,.0105f,3.78f,-.024f,.82f)
                +Crease(.834f,.024f,.0085f,2.88f,.021f,1.44f)
                +Crease(.919f,.019f,.0065f,2.43f,-.014f,.52f);
            float envelope=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.54f,.60f,t))
                *(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.935f,.975f,t)));
            return Mathf.Clamp(relief*envelope,-.0075f,.0100f);
        }
        static float ShareForearmTwist(Transform forearm,Transform wrist,Transform target,Quaternion handRest)
        {
            var axis=(wrist.position-forearm.position).normalized;
            var delta=target.rotation*Quaternion.Inverse(forearm.rotation*handRest);
            float shared=Mathf.Clamp(TwistDegrees(delta,axis)*.7f,-70f,70f);
            forearm.rotation=Quaternion.AngleAxis(shared,axis)*forearm.rotation;
            return shared;
        }

        public static float TwistDegrees(Quaternion delta,Vector3 axis)
        {
            if(axis.sqrMagnitude<.000001f)return 0;
            axis.Normalize();float along=Vector3.Dot(new Vector3(delta.x,delta.y,delta.z),axis);
            if(along*along+delta.w*delta.w<.000001f)return 0;
            return Mathf.DeltaAngle(0,2f*Mathf.Atan2(along,delta.w)*Mathf.Rad2Deg);
        }

        public static void Solve(Transform upper, Transform forearm, Transform wrist, Transform target,bool allowShoulderTranslation=true,Vector3? elbowPosition=null)
        {
            float upperLength = Vector3.Distance(upper.position, forearm.position);
            float lowerLength = Vector3.Distance(forearm.position, wrist.position);
            var toTarget = target.position - upper.position;
            float distance = toTarget.magnitude;
            // Retain a small elbow bend at full reach. A virtually locked elbow
            // made the sleeve look stretched even though the bone lengths were fixed.
            float comfortableReach=upperLength+lowerLength-Mathf.Min(upperLength,lowerLength)*.10f;
            if (allowShoulderTranslation && distance > comfortableReach)
            {
                // The shoulder remains outside the camera; accommodate different viewmodel arm proportions.
                upper.position += toTarget.normalized * (distance-comfortableReach);
                toTarget = target.position - upper.position;
                distance = toTarget.magnitude;
            }
            var axis = toTarget.normalized;
            var elbowHint = Vector3.ProjectOnPlane((elbowPosition??forearm.position) - upper.position, axis).normalized;
            if (elbowHint.sqrMagnitude < 0.01f) elbowHint = Vector3.ProjectOnPlane(-upper.up, axis).normalized;
            distance = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            var elbow = upper.position + axis * along + elbowHint * height;
            upper.rotation = Quaternion.FromToRotation(forearm.position - upper.position, elbow - upper.position) * upper.rotation;
            forearm.rotation = Quaternion.FromToRotation(wrist.position - forearm.position, target.position - forearm.position) * forearm.rotation;
            wrist.rotation = target.rotation;
        }
    }
}
