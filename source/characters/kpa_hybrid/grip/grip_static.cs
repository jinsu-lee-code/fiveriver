var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
var T = typeof(FiveRivers.Combat.EnemyHandIK);
FiveRivers.Combat.EnemyAgent who = null;
foreach (var e in UnityEngine.Object.FindObjectsByType<FiveRivers.Combat.EnemyAgent>(UnityEngine.FindObjectsSortMode.InstanceID)) if (e.Alive) { who = e; break; }
if (who == null) return "none";
var ik = who.GetComponentInChildren<FiveRivers.Combat.EnemyHandIK>();
var an = who.GetComponentInChildren<UnityEngine.Animator>();
var rifle = (UnityEngine.Transform)T.GetField("rifle", bf).GetValue(ik);
var rest = new System.Collections.Generic.Dictionary<string, UnityEngine.Quaternion>(); foreach (var k in an.avatar.humanDescription.skeleton) rest[k.name] = k.rotation;
UnityEngine.SkinnedMeshRenderer smr = null;
foreach (var r in an.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) if (r.sharedMesh != null && r.sharedMesh.name.Contains("Hybrid")) smr = r;
var sb = new System.Text.StringBuilder();
System.Func<float, string> F = x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
System.Func<UnityEngine.Vector3, string> V = v => F(v.x) + " " + F(v.y) + " " + F(v.z);
System.Func<UnityEngine.Quaternion, string> Q = q => F(q.x) + " " + F(q.y) + " " + F(q.z) + " " + F(q.w);
sb.AppendLine("RIFLESCALE " + V(rifle.lossyScale));
var bones = smr.bones; var m = smr.sharedMesh; var binds = m.bindposes;
for (int i = 0; i < bones.Length; i++)
{
    var b = bones[i];
    sb.Append("BONE " + i + " " + b.name.Replace(" ", "_") + " " + System.Array.IndexOf(bones, b.parent) + " " + V(b.localPosition) + " " + Q(b.name.Contains("Hand") && !b.name.EndsWith("Hand") && rest.ContainsKey(b.name) ? rest[b.name] : b.localRotation) + " " + V(b.localScale) + " " + V(b.lossyScale));
    for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) sb.Append(" " + F(binds[i][r, c]));
    sb.AppendLine();
}
var vs = m.vertices; var ws = m.boneWeights; var tris = m.GetTriangles(1);
var ids = new System.Collections.Generic.Dictionary<int, int>();
foreach (var t in tris)
{
    if (ids.ContainsKey(t)) continue;
    ids[t] = ids.Count; var w = ws[t];
    sb.AppendLine("V " + V(vs[t]) + " " + w.boneIndex0 + " " + w.boneIndex1 + " " + w.boneIndex2 + " " + w.boneIndex3 + " " + F(w.weight0) + " " + F(w.weight1) + " " + F(w.weight2) + " " + F(w.weight3));
}
for (int t = 0; t < tris.Length; t += 3) sb.AppendLine("F " + ids[tris[t]] + " " + ids[tris[t + 1]] + " " + ids[tris[t + 2]]);
int rb = 0;
foreach (var mf in rifle.GetComponentsInChildren<UnityEngine.MeshFilter>())
{
    var rm = mf.sharedMesh; if (rm == null || !rm.isReadable) continue;
    sb.AppendLine("PART " + mf.name.Replace(" ", "_"));
    var to = rifle.worldToLocalMatrix * mf.transform.localToWorldMatrix;
    foreach (var p in rm.vertices) sb.AppendLine("RV " + V(to.MultiplyPoint3x4(p)));
    var rt = rm.triangles;
    for (int t = 0; t < rt.Length; t += 3) sb.AppendLine("RF " + (rb + rt[t]) + " " + (rb + rt[t + 1]) + " " + (rb + rt[t + 2]));
    rb += rm.vertexCount;
}
System.IO.Directory.CreateDirectory("C:/sk_assets/capture/grip");
System.IO.File.WriteAllText("C:/sk_assets/capture/grip/static.txt", sb.ToString());
return "ok bones=" + bones.Length + " handVerts=" + ids.Count + " rifleVerts=" + rb;
