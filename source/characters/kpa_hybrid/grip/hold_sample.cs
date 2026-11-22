var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var T = typeof(FiveRivers.Combat.EnemyHandIK);
int n = 0;
using (var bw = new System.IO.BinaryWriter(System.IO.File.Open("C:/sk_assets/capture/grip/holds.bin", System.IO.FileMode.Append)))
{
    var baked = new UnityEngine.Mesh();
    foreach (var e in UnityEngine.Object.FindObjectsByType<FiveRivers.Combat.EnemyAgent>(UnityEngine.FindObjectsSortMode.None))
    {
        if (!e.Alive) continue;
        var ik = e.GetComponentInChildren<FiveRivers.Combat.EnemyHandIK>(); if (ik == null) continue;
        var an = e.GetComponentInChildren<UnityEngine.Animator>();
        var rifle = (UnityEngine.Transform)T.GetField("rifle", bf).GetValue(ik); if (rifle == null) continue;
        if (an.GetCurrentAnimatorClipInfo(0).Length == 0) continue;
        UnityEngine.SkinnedMeshRenderer smr = null;
        foreach (var r in an.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>()) if (r.sharedMesh != null && r.sharedMesh.name.Contains("Hybrid")) smr = r;
        if (smr == null) continue;
        var tris = smr.sharedMesh.GetTriangles(1);
        var order = new System.Collections.Generic.List<int>(); var seen = new System.Collections.Generic.HashSet<int>();
        foreach (var t in tris) if (seen.Add(t)) order.Add(t);
        smr.BakeMesh(baked, true);
        var v = baked.vertices; var m = rifle.worldToLocalMatrix * smr.transform.localToWorldMatrix;
        var name = System.Text.Encoding.ASCII.GetBytes((an.GetCurrentAnimatorClipInfo(0)[0].clip.name + (an.IsInTransition(0) ? "~" : "")).PadRight(48).Substring(0, 48));
        bw.Write(name);
        bw.Write(ik.Shoulder); bw.Write(ik.Kneel); bw.Write(ik.TriggerWristBend); bw.Write(ik.SupportWristBend); bw.Write(ik.TriggerShort); bw.Write(ik.SupportShort);
        bw.Write(order.Count);
        foreach (var i in order) { var p = m.MultiplyPoint3x4(v[i]); bw.Write(p.x); bw.Write(p.y); bw.Write(p.z); }
        n++;
    }
    UnityEngine.Object.DestroyImmediate(baked);
}
return "n=" + n;
