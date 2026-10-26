using System.IO;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 편집 모드에서 지정 위치 · 방향으로 임시 카메라를 놓고 PNG로 찍는다 (그레이박스 배치 확인 · 문서용).
    // 결과: Temp/captures/<이름>.png
    static class ViewCapture
    {
        public const string Dir = "Temp/captures/";

        public static string Capture(string name, Vector3 position, Vector3 euler, float fov = 70f, int width = 1600, int height = 900)
        {
            Directory.CreateDirectory(Dir);
            var go = new GameObject("CaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = Dir + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
            return Path.GetFullPath(path);
        }

        [MenuItem("FiveRivers/Dev/Capture Map Views")]
        public static void CaptureMapViews()
        {
            Capture("trench_front", new Vector3(0f, 1.6f, -0.5f), new Vector3(2f, 0f, 0f));
            Capture("trench_to_tank", new Vector3(-16f, 1.6f, -0.5f), new Vector3(0f, 10f, 0f));
            float moundY = MapBuilder.Height(-30f, 42f);
            Capture("mound_to_tank", new Vector3(-30f, moundY + 1.6f, 42f), new Vector3(2f, 82f, 0f));
            Capture("overview", new Vector3(-60f, 70f, -50f), new Vector3(38f, 40f, 0f), 60f);
            Debug.Log("[Capture] " + Path.GetFullPath(Dir));
        }
    }
}
