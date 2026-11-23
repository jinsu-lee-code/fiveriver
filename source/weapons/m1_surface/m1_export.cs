var m1 = UnityEngine.Object.FindAnyObjectByType<FiveRivers.Combat.M1Rifle>(UnityEngine.FindObjectsInactive.Include);
var model = m1.Model; var sb = new System.Text.StringBuilder(); int baseIndex = 1; string log = "";
var ci = System.Globalization.CultureInfo.InvariantCulture;
foreach (var r in model.GetComponentsInChildren<UnityEngine.MeshRenderer>(true))
{
    var mf = r.GetComponent<UnityEngine.MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
    bool l0 = r.name.EndsWith("_L0"); if (!l0) continue;
    var mesh = mf.sharedMesh; var to = model.worldToLocalMatrix * r.transform.localToWorldMatrix;
    var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv;
    foreach (var p in v) { var q = to.MultiplyPoint3x4(p); sb.Append("v " + (-q.x).ToString("F6", ci) + " " + q.y.ToString("F6", ci) + " " + q.z.ToString("F6", ci) + "\n"); }
    for (int i = 0; i < v.Length; i++) { var t = i < uv.Length ? uv[i] : UnityEngine.Vector2.zero; sb.Append("vt " + t.x.ToString("F6", ci) + " " + t.y.ToString("F6", ci) + "\n"); }
    for (int i = 0; i < v.Length; i++) { var q = i < n.Length ? to.MultiplyVector(n[i]).normalized : UnityEngine.Vector3.up; sb.Append("vn " + (-q.x).ToString("F5", ci) + " " + q.y.ToString("F5", ci) + " " + q.z.ToString("F5", ci) + "\n"); }
    for (int s = 0; s < mesh.subMeshCount; s++)
    {
        var mat = s < r.sharedMaterials.Length && r.sharedMaterials[s] != null ? r.sharedMaterials[s].name : "none";
        sb.Append("o " + r.name + "__" + s + "__" + mat + "\n"); var tr = mesh.GetTriangles(s);
        for (int i = 0; i < tr.Length; i += 3) { int a = tr[i] + baseIndex, b = tr[i + 1] + baseIndex, c = tr[i + 2] + baseIndex; sb.Append("f " + a + "/" + a + "/" + a + " " + c + "/" + c + "/" + c + " " + b + "/" + b + "/" + b + "\n"); }
        log += r.name + ":" + s + ":" + mat + ":" + tr.Length / 3 + " ";
    }
    baseIndex += v.Length;
}
System.IO.File.WriteAllText("C:/sk_assets/capture/m1/m1_l0.obj", sb.ToString());
return log;
