var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/PF_VH_T34_85.prefab");
System.IO.Directory.CreateDirectory("C:/sk_assets/capture/gear");
var sb = new System.Text.StringBuilder();
foreach (var mf in go.GetComponentsInChildren<UnityEngine.MeshFilter>(true)) {
  var m = mf.sharedMesh; var t = mf.transform;
  sb.Append(mf.name + " path=" + UnityEditor.AnimationUtility.CalculateTransformPath(t, go.transform) + " v=" + m.vertexCount + " sub=" + m.subMeshCount + " readable=" + m.isReadable + " idx=" + m.indexFormat + " lpos=" + t.localPosition + " lrot=" + t.localEulerAngles + " lscale=" + t.localScale + " bounds=" + m.bounds + " mat=" + mf.GetComponent<UnityEngine.Renderer>().sharedMaterial.name + " | ");
  if (mf.name == "TrackL" || mf.name == "TrackR") {
    var mat = go.transform.worldToLocalMatrix * t.localToWorldMatrix;
    var vs = m.vertices; var uv = m.uv;
    using (var w = new System.IO.BinaryWriter(System.IO.File.Create("C:/sk_assets/capture/gear/" + mf.name + ".bin"))) {
      for (int i = 0; i < vs.Length; i++) { var p = mat.MultiplyPoint3x4(vs[i]); w.Write(p.x); w.Write(p.y); w.Write(p.z); w.Write(uv[i].x); w.Write(uv[i].y); }
    }
    var tr = m.triangles;
    using (var w = new System.IO.BinaryWriter(System.IO.File.Create("C:/sk_assets/capture/gear/" + mf.name + ".tri"))) { foreach (var x in tr) w.Write(x); }
  }
}
return sb.ToString();
