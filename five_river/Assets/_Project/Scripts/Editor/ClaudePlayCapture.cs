using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 임시 점검 도구 (2026-10-30 Claude). 플레이 모드에서 손 자세 후보와 아군 사격을 찍는다. 씬은 저장하지 않는다. 작업이 끝나면 지운다.
    [InitializeOnLoad]
    public static class ClaudePlayCapture
    {
        const string Out = "Logs/claude_probe/";
        const string Key = "ClaudePlayCapture";
        static double startAt;
        static int step;
        static readonly StringBuilder log = new StringBuilder();

        static ClaudePlayCapture()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (startAt == 0) { startAt = EditorApplication.timeSinceStartup; return; }
            double t = EditorApplication.timeSinceStartup - startAt;
            try
            {
                if (step == 0 && t > 3)
                {
                    Object.FindFirstObjectByType<FlowDirector>().Flow.StartRun();
                    log.AppendLine("StartRun at " + t.ToString("F1"));
                    step = 1;
                }
                else if (step == 1 && t > 7)
                {
                    if (Variants()) { step = 2; startAt = EditorApplication.timeSinceStartup - 12; }
                }
                else if (step == 2 && t > 16)
                {
                    var flow = Object.FindFirstObjectByType<FlowDirector>().Flow;
                    log.AppendLine("section " + flow.Section);
                    foreach (var a in Object.FindObjectsByType<AllyCombatant>(FindObjectsSortMode.None))
                        log.AppendLine($"{a.name} shots={a.ShotsFired} flashes={a.MuzzleEffectsSpawned}");
                    AllyView();
                    step = 3;
                }
                else if (step == 3 && t > 18)
                {
                    Finish();
                }
            }
            catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); Finish(); }
        }

        static Transform grip;
        static Vector3 p0;
        static Quaternion r0;
        static string[] lines;
        static int index, setFrame = -1;
        static Dictionary<Transform, Quaternion> fingerRest;
        static object[] hipRest, handRest;

        // One variant per few frames: set the target, let skinning update, then render.
        static bool Variants()
        {
            if (grip == null)
            {
                grip = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == "Grip_Left");
                p0 = grip.localPosition; r0 = grip.localRotation;
                log.AppendLine("Grip_Left parent " + grip.parent.name + " local " + p0.ToString("F4") + " euler " + grip.localEulerAngles.ToString("F1"));
                lines = (File.Exists(Out + "variants.txt") ? File.ReadAllLines(Out + "variants.txt") : new[] { "base 0 0 0 0 0 0" })
                    .Where(l => l.Trim().Length > 0 && !l.StartsWith("#")).ToArray();
                // Rifle parts in rifle space (M1_ShotMotion) to place the hand against the real stock.
                foreach (var r in grip.parent.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy))
                {
                    var b = r.localBounds; var m = grip.parent.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) * 2 - 1, (c & 2) - 1, ((c & 4) >> 1) - 1)));
                        min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                    }
                    if (max.z - min.z > 0.08f) log.AppendLine($"part {r.name} min {min:F3} max {max:F3}");
                }
                var wrist = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == "wrist.L");
                fingerRest = wrist.GetComponentsInChildren<Transform>().Where(b => b.name.StartsWith("finger")).ToDictionary(b => b, b => b.localRotation);
            }
            if (index >= lines.Length) { grip.localPosition = p0; grip.localRotation = r0; return true; }
            var f = lines[index].Split(' ');
            float F(int i) => i < f.Length ? float.Parse(f[i], CultureInfo.InvariantCulture) : 0f;
            if (setFrame < 0)
            {
                // Names starting with "ads" render the aimed pose: the hip pose fields temporarily take the aim values.
                var rifle = Object.FindFirstObjectByType<M1Rifle>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var hipPos = typeof(M1Rifle).GetField("hipPosition", flags);
                var hipEuler = typeof(M1Rifle).GetField("hipEuler", flags);
                if (hipRest == null) hipRest = new object[] { hipPos.GetValue(rifle), hipEuler.GetValue(rifle) };
                bool ads = f[0].StartsWith("ads");
                hipPos.SetValue(rifle, ads ? typeof(M1Rifle).GetField("aimPosition", flags).GetValue(rifle) : hipRest[0]);
                hipEuler.SetValue(rifle, ads ? Vector3.zero : hipRest[1]);
                if (f.Length >= 10)
                {
                    // Absolute grip from reference: wrist position, finger direction and palm normal in rifle space.
                    var wrist = fingerRest.Keys.First().parent;
                    while (wrist.name != "wrist.L") wrist = wrist.parent;
                    Transform B(string n) => fingerRest.Keys.First(b => b.name == n);
                    grip.localRotation = r0;
                    var lf = Quaternion.Inverse(wrist.rotation) * (B("finger3-1.L").position - wrist.position);
                    var la = Quaternion.Inverse(wrist.rotation) * (B("finger2-1.L").position - B("finger5-1.L").position);
                    var lp = Vector3.Cross(lf, la).normalized;
                    var lt = Quaternion.Inverse(wrist.rotation) * (B("finger1-2.L").position - wrist.position);
                    log.AppendLine($"{f[0]} local fingers {lf.normalized:F2} palm {lp:F2} thumb {lt.normalized:F2} thumb.palm {Vector3.Dot(lt.normalized, lp):F2}");
                    grip.localPosition = new Vector3(F(1), F(2), F(3));
                    grip.localRotation = Quaternion.LookRotation(new Vector3(F(7), F(8), F(9)).normalized, new Vector3(F(4), F(5), F(6)).normalized)
                        * Quaternion.Inverse(Quaternion.LookRotation(lp, lf.normalized));
                    f = new[] { f[0], "0", "0", "0", "0", "0", "0", f.Length > 10 ? f[10] : "0", f.Length > 11 ? f[11] : "0" };
                }
                else if (f[0].StartsWith("h_") || f[0].StartsWith("ads_h_"))
                {
                    // Move the Varco support hand (and the sleeve end with it) relative to its saved pose.
                    var hand = grip.parent.Find("SupportHand_M1");
                    if (handRest == null) handRest = new object[] { hand.localPosition, hand.localRotation, hand.localScale };
                    hand.localPosition = (Vector3)handRest[0] + new Vector3(F(1), F(2), F(3));
                    hand.localRotation = (Quaternion)handRest[1] * Quaternion.Euler(F(4), F(5), F(6));
                    hand.localScale = (Vector3)handRest[2] * (F(7) > 0f ? F(7) : 1f);
                    grip.SetPositionAndRotation(hand.TransformPoint(new Vector3(0f, F(8) > 0f ? -F(8) : -0.10f, 0f)), hand.rotation);
                    f = new[] { f[0], "0", "0", "0", "0", "0", "0", "0", "0" };
                }
                else
                {
                    grip.localPosition = p0 + new Vector3(F(1), F(2), F(3));
                    grip.localRotation = r0 * Quaternion.Euler(F(4), F(5), F(6));
                }
                // Optional extra curl (degrees) for index/middle (7) and ring/little (8), all joints but the base.
                foreach (var kv in fingerRest)
                {
                    string n = kv.Key.name;
                    float curl = n.StartsWith("finger1") || n.Contains("-1.") ? 0f
                        : n.StartsWith("finger2") || n.StartsWith("finger3") ? F(7) : F(8);
                    kv.Key.localRotation = kv.Value * Quaternion.Euler(curl, 0f, 0f);
                }
                setFrame = Time.frameCount;
            }
            else if (Time.frameCount > setFrame + 3)
            {
                {
                    Transform B(string n) => fingerRest.Keys.First(b => b.name == n);
                    var w = B("finger3-1.L").parent; while (w.name != "wrist.L") w = w.parent;
                    var sp = grip.parent;
                    var fd = sp.InverseTransformDirection(B("finger3-1.L").position - w.position).normalized;
                    var tip = sp.InverseTransformPoint(B("finger3-3.L").position);
                    var th = sp.InverseTransformPoint(B("finger1-3.L").position);
                    log.AppendLine($"{f[0]} measured wrist {sp.InverseTransformPoint(w.position):F3} grip {grip.localPosition:F3} fingerDir {fd:F2} middleTip {tip:F3} thumbTip {th:F3}");
                }
                Shot(Camera.main, "hand_" + f[0]);
                // Side view from the left of the rifle, like the reference photos.
                var side = new GameObject("ClaudeSideCam").AddComponent<Camera>();
                side.nearClipPlane = 0.01f; side.fieldOfView = 40f;
                var rifleSpace = grip.parent;
                var eye = grip.position + rifleSpace.TransformDirection(new Vector3(-0.45f, 0.12f, 0.05f));
                side.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(grip.position + rifleSpace.TransformDirection(new Vector3(0f, 0.03f, 0.04f)) - eye, rifleSpace.up));
                Shot(side, "side_" + f[0]);
                // Same angle as the reference photo: from the shooter's right front.
                var photoEye = grip.position + rifleSpace.TransformDirection(new Vector3(0.40f, 0.10f, 0.35f));
                side.transform.SetPositionAndRotation(photoEye, Quaternion.LookRotation(grip.position + rifleSpace.TransformDirection(new Vector3(0f, 0.03f, 0.04f)) - photoEye, rifleSpace.up));
                Shot(side, "front_" + f[0]);
                Object.Destroy(side.gameObject);
                index++;
                setFrame = -1;
            }
            return false;
        }

        static void AllyView()
        {
            var ally = GameObject.Find("Ally_2");
            if (ally == null) return;
            var go = new GameObject("ClaudeAllyCam");
            var c = go.AddComponent<Camera>();
            var p = ally.transform.position;
            c.transform.SetPositionAndRotation(p + new Vector3(-4.5f, 1.6f, -3.5f), Quaternion.LookRotation(p + new Vector3(2f, 0.6f, 6f) - (p + new Vector3(-4.5f, 1.6f, -3.5f))));
            c.fieldOfView = 55f;
            Shot(c, "ally_view");
            Object.Destroy(go);
        }

        static void Shot(Camera c, string name)
        {
            var rt = new RenderTexture(1600, 900, 24);
            var old = c.targetTexture;
            c.targetTexture = rt;
            c.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Out + name + ".png", tex.EncodeToPNG());
            c.targetTexture = old;
            RenderTexture.active = null;
            Object.Destroy(tex);
            rt.Release();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            File.WriteAllText(Out + "play.txt", log.ToString());
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
