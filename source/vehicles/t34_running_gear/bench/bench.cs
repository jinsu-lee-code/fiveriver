var old = UnityEngine.GameObject.Find("GearBench"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/PF_VH_T34_85.prefab");
var root = new UnityEngine.GameObject("GearBench");
var model = UnityEngine.Object.Instantiate(prefab.transform.Find("Model").gameObject, root.transform); model.name = "Model";
var gear = root.AddComponent<FiveRivers.Combat.TankRunningGear>(); gear.Build(root.transform);
if (UnityEngine.GameObject.Find("BenchLight") == null) {
  var l = new UnityEngine.GameObject("BenchLight", typeof(UnityEngine.Light)); var li = l.GetComponent<UnityEngine.Light>(); li.type = UnityEngine.LightType.Directional; li.intensity = 2.2f; li.shadows = UnityEngine.LightShadows.Soft;
  l.transform.rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(-0.75f, -0.55f, 0.35f));
  var l2 = new UnityEngine.GameObject("BenchFill", typeof(UnityEngine.Light)); var li2 = l2.GetComponent<UnityEngine.Light>(); li2.type = UnityEngine.LightType.Directional; li2.intensity = 0.7f; li2.shadows = UnityEngine.LightShadows.None;
  l2.transform.rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(-0.6f, 0.25f, -0.75f));
  UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(0.38f, 0.38f, 0.40f); UnityEngine.RenderSettings.fog = false;
}
return "bench tracks=" + gear.TrackCount + " children=" + root.GetComponentsInChildren<UnityEngine.MeshRenderer>().Length;
