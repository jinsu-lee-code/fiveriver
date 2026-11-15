var r = UnityEngine.GameObject.Find("GearBench"); r.transform.position += new UnityEngine.Vector3(0f, 0f, 0.37f) - new UnityEngine.Vector3(0f, 0f, 0f);
return "moved z=" + r.transform.position.z;
