using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-27 (아트 검수 "폭발 · 연기 · 피격 효과가 밤 화면과 따로 놂"):
    //  - the rocket's explosion on the tank is one flip-book cloud (Explosion FREE 1, unlit) that stayed a bright orange-red for
    //    a whole second: under the night grade (saturation -60) a pink candy-floss ball. It is now drawn by its brightness only
    //    (FR_ParticleFlipbookValue.shader) and coloured by its colour over lifetime: a cream-white flash for the first twentieth
    //    of its life, a dim brown cloud by an eighth, then smoke. The light of the explosion goes out in 0.3 s instead of
    //    0.8 / 1.2 s (FlashEffect.duration).
    //  - the tank's smoke, the smoke of its destruction and the puff of a bullet in the dirt were unlit and pale (WarFX): white
    //    clouds by night (the night picture is a dark scene exposed up, so an unlit 0.3 grey is eight times the ground behind it)
    //    and the same under a flare. They go onto a lit particle material (URP Particles/Simple Lit, the soft puff of
    //    M_VFX_Smoke): about as bright as the land behind them by moonlight, and they take the light of a flare, of the
    //    explosion or of the fire below them. (Tried between: the unlit smoke of the shell bursts in dark greys 0.13-0.17 — still
    //    72 of 255 by night against 12; and the lit material at an albedo of 0.2 — a black dome against the moonlit land.)
    //  - the clods a burst throws up were unlit dark squares up to 26 cm across: black flat polygons against a lit slope, pale ones
    //    against a dark one. Lit, small, and drawn stretched along their flight (ShellingSetup builds those prefabs: changed there).
    // No particle counts go up. Numbers: BALANCE.md 4-7. Changes prefabs under Art/VFX and two new materials; the scene is not
    // touched. Re-running gives the same result. Run FiveRivers/Setup/Shelling after it for the shell-burst prefabs.
    public static class BattleVfxLook20261127
    {
        const string Dir = "Assets/_Project/Art/VFX/";
        const string SmokeSource = "Assets/_Project/Art/Materials/M_VFX_Smoke.mat";
        public const string SmokeLitPath = "Assets/_Project/Art/Materials/M_VFX_SmokeLit.mat";
        public const string DebrisLitPath = "Assets/_Project/Art/Materials/M_VFX_DebrisLit.mat";
        const string ExplosionSource = "Assets/_Project/Art/Mirza Beig/Cinematic Explosions FREE/Materials/Explosions/Explosion FREE 1.mat";
        const string ExplosionPath = "Assets/_Project/Art/Materials/M_VFX_TankExplosion.mat";
        // The flip-book is drawn by its brightness only (shader FiveRivers/ParticleFlipbookValue): its own flame is a pure
        // orange-red (247, 128, 12), salmon pink under the night grade (saturation -60), and no multiplying tint cures that — with
        // green lifted eight times (2.2, 8.0, 3.4) the bright parts clipped to pink and the dimmer puffs came out green
        // (vfx/final_hit2_eyes_0.10.png). The hue is now the colour curve's alone; this is its brightness (HDR).
        public const string ExplosionShader = "FiveRivers/ParticleFlipbookValue";
        public static readonly Color ExplosionBase = new Color(2.0f, 2.0f, 2.0f, 1f);
        // where the sheet is black smoke: this colour, whatever the colour curve (0 was a black cloud at 0.1-0.3 s: 14 of 255 on 24)
        public static readonly Color ExplosionSmoke = new Color(0.075f, 0.07f, 0.065f, 1f);
        public const float ExplosionKeepHue = 0f, ExplosionSoftFade = 0.5f, ExplosionCameraNear = 1f, ExplosionCameraFar = 3f;
        const string DebrisTexture = "Assets/_Project/Art/JMO Assets/WarFX/Desktop/Textures/Misc/WFX_T_Debris Generic.tga";

        // ---- numbers (BALANCE.md 4-7): albedo of the lit particles (alpha as the effect had it)
        public static readonly Color OilSmoke = new Color(0.42f, 0.41f, 0.40f);      // the burning tank
        public static readonly Color BlastSmoke = new Color(0.34f, 0.33f, 0.32f);    // the cloud of its destruction
        public static readonly Color Dust = new Color(0.42f, 0.34f, 0.24f);          // a bullet in dry soil
        public static readonly Color Clod = new Color(0.29f, 0.23f, 0.17f);          // thrown earth
        // the fireball: time (share of the particle's life), colour of the sheet's bright parts (x ExplosionBase; sRGB numbers,
        // so 0.12 is 0.013 in linear light: the fire is out by a fifth of the life and only ExplosionSmoke is left)
        static readonly (float t, Color c)[] Fireball =
        {
            // a cream-white flash, a pale straw glow by 0.07, dim brown embers by 0.12, gone from 0.2 (kept low in saturation:
            // the night grade turns a strong orange to salmon)
            (0.00f, new Color(1.00f, 0.93f, 0.76f)), (0.04f, new Color(1.00f, 0.93f, 0.76f)), (0.07f, new Color(0.80f, 0.66f, 0.46f)), (0.12f, new Color(0.34f, 0.26f, 0.19f)),
            (0.20f, new Color(0.16f, 0.13f, 0.105f)), (0.35f, new Color(0.12f, 0.105f, 0.09f)), (1.00f, new Color(0.11f, 0.10f, 0.09f)),
        };
        public const float FlashSeconds = 0.3f;   // the explosion's light (was 0.8 s on a hit, 1.2 s on the destruction)
        public const float ClodSizeMin = 0.012f, ClodSizeMax = 0.03f, ClodStretch = 2.0f, ClodVelocityStretch = 0.004f;

        public static Material SmokeLit() => Lit(SmokeLitPath, null);
        public static Material DebrisLit() => Lit(DebrisLitPath, AssetDatabase.LoadAssetAtPath<Texture2D>(DebrisTexture));

        // a lit, alpha-blended particle material: the unlit smoke material's own settings on URP Particles/Simple Lit
        static Material Lit(string path, Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            var source = AssetDatabase.LoadAssetAtPath<Material>(SmokeSource);
            if (shader == null || source == null) throw new System.InvalidOperationException("없음: Particles/Simple Lit 셰이더 또는 " + SmokeSource);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(source); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", Color.white);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_Smoothness", 0f); m.SetColor("_SpecColor", Color.black);
            m.SetFloat("_ReceiveShadows", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("FiveRivers/Setup/Battle VFX Look 20261127")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var smoke = SmokeLit(); var debris = DebrisLit();
            var sb = new StringBuilder();
            Edit("VFX_Tank_HitExplosion", sb, root => { Explosion(root, sb); });
            Edit("VFX_Tank_DestroyExplosion", sb, root =>
            {
                Explosion(root, sb);
                LitSmoke(root, "SmokeBurst", smoke, BlastSmoke, sb); LitSmoke(root, "Smoke", smoke, BlastSmoke, sb);
            });
            Edit("VFX_Tank_Smoke", sb, root => { foreach (var ps in Systems(root, "Smoke")) LitSmoke(ps, smoke, OilSmoke, sb); });
            Edit("VFX_Tank_Fire", sb, root => { foreach (var ps in Systems(root, "Smoke")) LitSmoke(ps, smoke, OilSmoke, sb); });
            Edit("VFX_Impact_Dirt", sb, root =>
            {
                LitSmoke(root, "Impact", smoke, Dust, sb); LitSmoke(root, "Directional Smoke", smoke, Dust, sb);
                foreach (var ps in Systems(root, "Dirt"))
                {
                    Clods(ps, debris, ClodSizeMin, ClodSizeMax);
                    sb.AppendLine($"    {ps.name}: lit clods {ClodSizeMin}-{ClodSizeMax} m, stretched");
                }
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[BattleVfxLook]\n" + sb);
            return sb.ToString();
        }

        static void Edit(string prefab, StringBuilder sb, System.Action<GameObject> change)
        {
            string path = Dir + prefab + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { sb.AppendLine("  missing " + path); return; }
            sb.AppendLine("  " + prefab);
            using (var scope = new PrefabUtility.EditPrefabContentsScope(path)) change(scope.prefabContentsRoot);
        }

        static ParticleSystem[] Systems(GameObject root, string name) => root.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name == name).ToArray();

        // The flip-book's own material draws it as emission (base colour black, emission 2): the particle's colour only reached its
        // alpha, so no colour curve could darken it. Ours draws the same sheet's brightness times the particle colour.
        static Material ExplosionMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(ExplosionSource);
            var shader = Shader.Find(ExplosionShader);
            if (source == null || shader == null) throw new System.InvalidOperationException("없음: " + ExplosionSource + " 또는 셰이더 " + ExplosionShader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(ExplosionPath);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, ExplosionPath); }
            m.shader = shader; m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            m.SetColor("_BaseColor", ExplosionBase);
            m.SetFloat("_KeepHue", ExplosionKeepHue); m.SetColor("_SmokeColor", ExplosionSmoke); m.SetFloat("_SoftFade", ExplosionSoftFade);
            m.SetFloat("_CameraNear", ExplosionCameraNear); m.SetFloat("_CameraFar", ExplosionCameraFar);
            m.renderQueue = source.renderQueue;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Explosion(GameObject root, StringBuilder sb)
        {
            var material = ExplosionMaterial();
            foreach (var ps in Systems(root, "Explosion"))
            {
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(Fireball.Select(k => new GradientColorKey(k.c, k.t)).ToArray(),
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
                var main = ps.main; main.startColor = Color.white;
                sb.AppendLine($"    {ps.name}: fireball colour curve (white-yellow -> dark brown by a tenth of its life -> smoke)");
            }
            foreach (var fx in root.GetComponentsInChildren<FiveRivers.Combat.FlashEffect>(true))
            {
                var so = new SerializedObject(fx); var d = so.FindProperty("duration");
                if (!Mathf.Approximately(d.floatValue, FlashSeconds)) sb.AppendLine($"    light goes out in {d.floatValue:0.##} -> {FlashSeconds} s");
                d.floatValue = FlashSeconds; so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void LitSmoke(GameObject root, string name, Material lit, Color albedo, StringBuilder sb) { foreach (var ps in Systems(root, name)) LitSmoke(ps, lit, albedo, sb); }

        static void LitSmoke(ParticleSystem ps, Material lit, Color albedo, StringBuilder sb)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            var main = ps.main; float alpha = main.startColor.mode == ParticleSystemGradientMode.Color ? main.startColor.color.a : 0.5f;
            r.sharedMaterial = lit; r.normalDirection = 0f;   // rounded normals: a puff takes light from above too, not only from the lens
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            main.startColor = new Color(albedo.r, albedo.g, albedo.b, alpha);
            // (the colour over its life keeps its alpha curve; its colour keys are white)
            var col = ps.colorOverLifetime;
            if (col.enabled && col.color.mode == ParticleSystemGradientMode.Gradient && col.color.gradient != null)
            {
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, col.color.gradient.alphaKeys);
                col.color = new ParticleSystem.MinMaxGradient(g);
            }
            sb.AppendLine($"    {ps.name}: lit smoke, albedo ({albedo.r:0.00}, {albedo.g:0.00}, {albedo.b:0.00}), alpha {alpha:0.00}");
        }

        // thrown earth: lit, small, drawn stretched along its flight (also used by ShellingSetup)
        public static void Clods(ParticleSystem ps, Material lit, float sizeMin, float sizeMax)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = lit; r.normalDirection = 0f;
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = ClodStretch; r.velocityScale = ClodVelocityStretch; r.cameraVelocityScale = 0f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new Color(Clod.r, Clod.g, Clod.b, 1f);
        }
    }
}
