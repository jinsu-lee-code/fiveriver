// Replaces the pines that stand in the shelled ground in front of the trench with shell-broken ones
// (run in the editor: unity command script eval --code "<this file>"; saves the Dabudong scene).
//
// "Not a whole tree or a whole rock was left" (veterans of the August 1950 fighting). The pines behind the
// trench and on the far flanks are left standing; the ones in the field the attack crossed are broken.
// Each replaced pine is only switched off, not deleted, so the change can be undone by re-enabling it and
// removing the "ShelledPines_Aug1950" object.
var spots = new (float x, float z, string v, float yaw)[]
{
    (-9f, 72f, "B", 40f), (13f, 61f, "A", 200f), (30f, 83f, "C", 0f), (-28f, 44f, "A", 110f),
    (-34f, 14f, "C", 75f), (-43f, 50f, "B", 250f), (53f, 33f, "A", 300f), (55f, 43f, "C", 20f), (-44f, 26f, "A", 160f),
};
var terrain = UnityEngine.Terrain.activeTerrain;
var old = UnityEngine.GameObject.Find("ShelledPines_Aug1950");
if (old != null) UnityEngine.Object.DestroyImmediate(old);
var pines = new System.Collections.Generic.List<UnityEngine.Transform>();
foreach (var mf in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    if (mf.sharedMesh != null && mf.sharedMesh.name == "Korean_Red_Pine_LOD0") pines.Add(mf.transform.parent.parent);
if (pines.Count == 0) return "no pines found";
var group = new UnityEngine.GameObject("ShelledPines_Aug1950");
group.transform.SetParent(pines[0].parent, false);
group.transform.SetPositionAndRotation(UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
var log = new System.Text.StringBuilder();
int placed = 0;
foreach (var s in spots)
{
    UnityEngine.Transform pine = null; float best = 6f * 6f;
    foreach (var p in pines)
    {
        float d = (p.position.x - s.x) * (p.position.x - s.x) + (p.position.z - s.z) * (p.position.z - s.z);
        if (d < best) { best = d; pine = p; }
    }
    if (pine == null) { log.Append("no pine near " + s.x + "," + s.z + "; "); continue; }
    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Maps/Dabudong1950/ShelledPine/SM_ShelledPine_" + s.v + ".fbx");
    if (asset == null) { log.Append("missing model " + s.v + "; "); continue; }
    // the model keeps the rotation and scale it was imported with; a holder carries the place, heading and size
    var tree = new UnityEngine.GameObject("ShelledPine_" + s.v + "_" + placed);
    tree.transform.SetParent(group.transform, false);
    var model = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(asset, tree.transform);
    var at = pine.position;
    at.y = terrain.SampleHeight(at) + terrain.transform.position.y - 0.05f;   // the foot a little into the ground
    tree.transform.SetPositionAndRotation(at, UnityEngine.Quaternion.Euler(0f, s.yaw, 0f));
    tree.transform.localScale = pine.lossyScale;
    // what stands blocks movement and shots as the trunk did
    var was = pine.GetComponent<UnityEngine.CapsuleCollider>();
    UnityEngine.MeshRenderer trunk = null;
    foreach (var r in tree.GetComponentsInChildren<UnityEngine.MeshRenderer>()) if (r.name != "FallenTop") trunk = r;
    if (was != null && trunk != null)
    {
        var c = tree.AddComponent<UnityEngine.CapsuleCollider>();
        float h = trunk.bounds.size.y / tree.transform.lossyScale.y;
        c.direction = 1; c.radius = was.radius; c.height = UnityEngine.Mathf.Max(h, was.radius * 2f); c.center = new UnityEngine.Vector3(0f, h * 0.5f, 0f);
    }
    tree.layer = pine.gameObject.layer;
    // the fallen top lies with the slope it fell on
    UnityEngine.Transform top = null;
    foreach (var t in tree.GetComponentsInChildren<UnityEngine.Transform>()) if (t.name == "FallenTop") top = t;
    if (top != null)
    {
        var p = top.position;
        float u = (p.x - terrain.transform.position.x) / terrain.terrainData.size.x, v = (p.z - terrain.transform.position.z) / terrain.terrainData.size.z;
        var normal = terrain.terrainData.GetInterpolatedNormal(u, v);
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        top.SetPositionAndRotation(p, UnityEngine.Quaternion.FromToRotation(UnityEngine.Vector3.up, normal) * top.rotation);
    }
    foreach (var t in tree.GetComponentsInChildren<UnityEngine.Transform>()) UnityEditor.GameObjectUtility.SetStaticEditorFlags(t.gameObject, UnityEditor.GameObjectUtility.GetStaticEditorFlags(pine.gameObject));
    pine.gameObject.SetActive(false);
    placed++;
    log.Append(s.v + "@" + at.x.ToString("F0") + "," + at.z.ToString("F0") + " (pine " + pine.name + " off); ");
}
var scene = group.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "placed " + placed + ", scene saved " + saved + " :: " + log;
