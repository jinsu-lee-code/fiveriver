var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
var T = typeof(FiveRivers.Combat.EnemyHandIK);
var sb = new System.Text.StringBuilder();
System.Func<float, string> F = x => x.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
System.Func<UnityEngine.Vector3, string> V = v => F(v.x) + " " + F(v.y) + " " + F(v.z);
int n = 0;
foreach (var e in UnityEngine.Object.FindObjectsByType<FiveRivers.Combat.EnemyAgent>(UnityEngine.FindObjectsSortMode.InstanceID))
{
    if (!e.Alive) continue;
    var ik = e.GetComponentInChildren<FiveRivers.Combat.EnemyHandIK>(); if (ik == null) continue;
    var an = e.GetComponentInChildren<UnityEngine.Animator>();
    var rifle = (UnityEngine.Transform)T.GetField("rifle", bf).GetValue(ik); if (rifle == null) continue;
    if (an.IsInTransition(0) || an.GetCurrentAnimatorClipInfo(0).Length == 0) continue;
    var at = typeof(FiveRivers.Combat.EnemyAgent);
    sb.Append("S " + e.GetInstanceID() + " " + an.GetCurrentAnimatorClipInfo(0)[0].clip.name + " " + F(ik.Shoulder) + " " + F(ik.Kneel));
    foreach (var h in new[] { UnityEngine.HumanBodyBones.RightUpperArm, UnityEngine.HumanBodyBones.RightLowerArm, UnityEngine.HumanBodyBones.RightHand, UnityEngine.HumanBodyBones.LeftUpperArm, UnityEngine.HumanBodyBones.LeftLowerArm, UnityEngine.HumanBodyBones.LeftHand, UnityEngine.HumanBodyBones.Head, UnityEngine.HumanBodyBones.Chest })
        sb.Append(" " + V(rifle.InverseTransformPoint(an.GetBoneTransform(h).position)));
    var t = an.transform;
    sb.Append(" " + V(rifle.InverseTransformDirection(t.right)) + " " + V(rifle.InverseTransformDirection(t.up)) + " " + V(rifle.InverseTransformDirection(t.forward)));
    sb.AppendLine(); n++;
}
System.IO.File.AppendAllText("C:/sk_assets/capture/grip/samples.txt", sb.ToString());
return "n=" + n;
