using System.IO;
using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 걷은 소매 — M1 1인칭 팔 (2026-11-27, 사용자 결정 "소매 걷기", 감독 지시 "방법 1"). 바주카 팔은 roll_sleeves.py →
    // Bazooka Tripo Arms 메뉴로 따로 만든다(같은 수치).
    // 하는 일(여러 번 실행해도 같음): 수치 에셋 RolledSleeveConfig 를 없을 때만 만들고, 양손 손 재질을 복사해 팔뚝 맨살 재질
    // 두 개를 만든다(바탕색 = 그 손 텍스처 피부의 중앙값에 큰 얼룩, 손 전용 노멀 · 가림 지도는 빼고 피부결 디테일은 그대로),
    // 그리고 M1ArmRig.rolledSleeve 에 연결해 씬을 저장한다. 손 메시 · 손 보정 · FBX 는 건드리지 않는다.
    // 되돌리기: Revert 메뉴(연결만 끊음 — 긴 소매 천 튜브로 돌아감).
    public static class RolledSleeves20261127
    {
        const string Dir = "Assets/_Project/Art/Models/Characters/ROK_Arms/RolledSleeve";

        [MenuItem("FiveRivers/Setup/Rolled Sleeves 20261127")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "edit mode only";
            var rig = Object.FindFirstObjectByType<M1ArmRig>(FindObjectsInactive.Include);
            if (rig == null) return "no M1ArmRig in the open scene";
            var config = AssetDatabase.LoadAssetAtPath<RolledSleeveConfig>(RolledSleeveConfig.AssetPath);
            if (config == null) { config = ScriptableObject.CreateInstance<RolledSleeveConfig>(); AssetDatabase.CreateAsset(config, RolledSleeveConfig.AssetPath); }
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Models/Characters/ROK_Arms", "RolledSleeve");
            var weapons = Object.FindFirstObjectByType<WeaponHolder>(FindObjectsInactive.Include);
            if (weapons == null) return "no WeaponHolder in the open scene";
            var model = weapons.M1.Model;
            Renderer Find(string name) => model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == name);
            var leftHand = Find("SupportHand_M1"); var rightHand = Find("M1_Anatomical_Grip_Hands");
            if (leftHand == null || rightHand == null) return "hand renderers missing";
            string log = "";
            config.leftSkin = SkinMaterial(config, leftHand.sharedMaterial, "L", ref log);
            config.rightSkin = SkinMaterial(config, rightHand.sharedMaterial, "R", ref log);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            var so = new SerializedObject(rig);
            so.FindProperty("rolledSleeve").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            EditorSceneManager.SaveScene(rig.gameObject.scene);
            return "rolled sleeves on" + log;
        }

        [MenuItem("FiveRivers/Setup/Rolled Sleeves 20261127 Revert")]
        public static string Revert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "edit mode only";
            var rig = Object.FindFirstObjectByType<M1ArmRig>(FindObjectsInactive.Include);
            if (rig == null) return "no M1ArmRig in the open scene";
            var so = new SerializedObject(rig);
            so.FindProperty("rolledSleeve").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            EditorSceneManager.SaveScene(rig.gameObject.scene);
            return "rolled sleeves off (full sleeve)";
        }

        const string PoreNormal = "Assets/_Project/Art/SurfaceDetail20261030/Skin_DetailNormal.png";   // the right hand's own pores

        static Material SkinMaterial(RolledSleeveConfig config, Material hand, string side, ref string log)
        {
            var src = hand.GetTexture("_BaseMap") as Texture2D ?? hand.mainTexture as Texture2D;
            Color skin = src != null ? SkinAt(src, config.m1SkinPercentile) : new Color(0.62f, 0.48f, 0.42f);
            float tone = side == "L" ? config.m1SkinToneLeft : config.m1SkinToneRight, rim = side == "L" ? config.m1RimToneLeft : config.m1RimToneRight;
            skin = new Color(skin.r * tone, skin.g * tone, skin.b * tone, 1f);
            var texPath = $"{Dir}/T_FP_ForearmSkin_{side}.png";
            File.WriteAllBytes(texPath, Mottled(skin, rim, config.m1WristBlend, side == "L" ? 11 : 23).EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var path = $"{Dir}/M_FP_ForearmSkin_{side}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(hand); AssetDatabase.CreateAsset(mat, path); }
            else { mat.shader = hand.shader; mat.CopyPropertiesFromMaterial(hand); }
            // the hand's own colour map and its hand-shaped normal / occlusion do not fit a forearm: a mottled skin laid along the
            // arm tube's uv (u round it, v shoulder -> wrist) that eases into the hand's colour over the last 3-4 cm, with the same
            // tint and smoothness as the hand and the right hand's skin-pore detail at the hand's pore size, half as strong
            foreach (var p in new[] { "_BaseMap", "_MainTex" }) if (mat.HasProperty(p)) { mat.SetTexture(p, tex); mat.SetTextureScale(p, Vector2.one); mat.SetTextureOffset(p, Vector2.zero); }
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null); mat.DisableKeyword("_NORMALMAP");
            if (mat.HasProperty("_OcclusionMap")) mat.SetTexture("_OcclusionMap", null); mat.DisableKeyword("_OCCLUSIONMAP");
            var pores = AssetDatabase.LoadAssetAtPath<Texture2D>(PoreNormal);
            if (pores != null && mat.HasProperty("_DetailNormalMap"))
            {
                mat.SetTexture("_DetailNormalMap", pores); mat.SetFloat("_DetailNormalMapScale", config.m1PoreStrength);
                mat.SetTextureScale("_DetailAlbedoMap", config.m1PoreTiling); mat.EnableKeyword("_DETAIL_MULX2");
                if (mat.GetTexture("_DetailAlbedoMap") == null) mat.SetFloat("_DetailAlbedoMapScale", 0f);
            }
            EditorUtility.SetDirty(mat);
            log += $" | {side}: hand {hand.name} skin median ({skin.r:0.000}, {skin.g:0.000}, {skin.b:0.000}) -> {path}";
            return mat;
        }

        // the hand texture's skin colour at a brightness percentile (opaque, not the dark cavity lines)
        static Color SkinAt(Texture2D src, float percentile)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); copy.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels32(); Object.DestroyImmediate(copy);
            var r = new System.Collections.Generic.List<byte>(); var g = new System.Collections.Generic.List<byte>(); var b = new System.Collections.Generic.List<byte>();
            for (int i = 0; i < px.Length; i += 7)
            {
                var c = px[i]; if (c.a < 128) continue;
                if (c.r + c.g + c.b < 150 || c.r < c.b) continue;   // cavity lines, background, nails' blue-grey
                r.Add(c.r); g.Add(c.g); b.Add(c.b);
            }
            if (r.Count == 0) return new Color(0.62f, 0.48f, 0.42f);
            r.Sort(); g.Sort(); b.Sort();
            int k = Mathf.Clamp(Mathf.RoundToInt((r.Count - 1) * percentile), 0, r.Count - 1);
            return new Color32(r[k], g[k], b[k], 255);
        }

        // the arm tube's skin: u round the arm (about 25 cm), v along it (shoulder 0 -> wrist ~0.94, about 60 cm). Soft blotches
        // about 5-12 cm across (a touch redder in them), round the arm seamless; over the wrist blend the colour eases to rim x skin
        static Texture2D Mottled(Color skin, float rim, Vector2 wristBlend, int seed)
        {
            const int NU = 128, NV = 512; var tex = new Texture2D(NU, NV, TextureFormat.RGBA32, true);
            var rnd = new System.Random(seed);
            float[,] Grid(int nu, int nv) { var a = new float[nv + 1, nu + 1]; for (int y = 0; y <= nv; y++) for (int x = 0; x <= nu; x++) a[y, x] = (float)(rnd.NextDouble() * 2 - 1); for (int y = 0; y <= nv; y++) a[y, nu] = a[y, 0]; return a; }
            var g1 = Grid(2, 5); var g2 = Grid(5, 12); var g3 = Grid(10, 24);
            float Sample(float[,] a, int nu, int nv, float u, float v)
            {
                float x = u * nu, y = v * nv; int x0 = Mathf.Min((int)x, nu - 1), y0 = Mathf.Min((int)y, nv - 1); float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                return Mathf.Lerp(Mathf.Lerp(a[y0, x0], a[y0, x0 + 1], fx), Mathf.Lerp(a[y0 + 1, x0], a[y0 + 1, x0 + 1], fx), fy);
            }
            var cols = new Color[NU * NV];
            for (int y = 0; y < NV; y++) for (int x = 0; x < NU; x++)
            {
                float u = x / (float)NU, v = y / (float)NV;
                float m = 0.022f * Sample(g1, 2, 5, u, v) + 0.014f * Sample(g2, 5, 12, u, v) + 0.006f * Sample(g3, 10, 24, u, v);
                float k = Mathf.Lerp(1f, rim, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(wristBlend.x, wristBlend.y, v)));
                cols[y * NU + x] = new Color(Mathf.Clamp01(skin.r * k * (1 + m * 1.15f)), Mathf.Clamp01(skin.g * k * (1 + m)), Mathf.Clamp01(skin.b * k * (1 + m * 0.9f)), 1f);
            }
            tex.SetPixels(cols); tex.Apply();
            return tex;
        }
    }
}
