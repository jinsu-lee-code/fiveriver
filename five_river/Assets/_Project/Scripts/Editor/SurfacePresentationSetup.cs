using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    public static class SurfacePresentationSetup
    {
        const string Folder="Assets/_Project/Art/SurfaceDetail20261030";
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Apply in Edit mode.");
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var pipeline=UniversalRenderPipeline.asset;
            if(pipeline==null)throw new System.InvalidOperationException("URP required.");
            pipeline.supportsHDR=true;pipeline.renderScale=1f;pipeline.shadowDistance=90f;
            pipeline.shadowCascadeCount=4;EditorUtility.SetDirty(pipeline);
            var camera=Camera.main;
            var data=camera.GetComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality=AntialiasingQuality.High;
            data.volumeLayerMask|=1;
            var volume=Object.FindFirstObjectByType<Volume>();
            const string profilePath=Folder+"/Dabudong_Night_Profile.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
                        T Effect<T>() where T:VolumeComponent
            {
                profile.components.RemoveAll(c=>c==null);
                var effect=profile.TryGet<T>(out var existing)?existing:profile.Add<T>(true);
                if(!EditorUtility.IsPersistent(effect))AssetDatabase.AddObjectToAsset(effect,profile);
                EditorUtility.SetDirty(effect);
                return effect;
            }
            Effect<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            Effect<ColorAdjustments>().postExposure.Override(.3f);
            Effect<ColorAdjustments>().contrast.Override(0f);
            Effect<ColorAdjustments>().saturation.Override(-4f);
            Effect<Bloom>().intensity.Override(.08f);Effect<Bloom>().threshold.Override(1.4f);
            Effect<Bloom>().scatter.Override(.45f);
            Effect<Vignette>().intensity.Override(.08f);Effect<Vignette>().smoothness.Override(.5f);
            Effect<MotionBlur>().intensity.Override(0f);
            volume.sharedProfile=profile;volume.enabled=true;volume.isGlobal=true;volume.weight=1f;volume.gameObject.layer=0;
            EditorUtility.SetDirty(profile);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.20f,.235f,.31f);
            RenderSettings.ambientEquatorColor=new Color(.115f,.135f,.17f);
            RenderSettings.ambientGroundColor=new Color(.055f,.05f,.042f);
            RenderSettings.fogColor=new Color(.065f,.082f,.115f);
            RenderSettings.fogDensity=.007f;
            camera.backgroundColor=RenderSettings.fogColor;
            foreach(var filename in new[]{"qwantani_night_puresky_2k.hdr","qwantani_dawn_puresky_2k.hdr","stretch_poplin_diff_1k.png","stretch_poplin_nor_gl_1k.png","stretch_poplin_rough_1k.png","american_walnut_veneer_nor_gl_1k.png","american_walnut_veneer_rough_1k.png"})
            {
                var importer=AssetImporter.GetAtPath(Folder+"/"+filename) as TextureImporter;
                if(importer==null)continue;
                importer.textureType=filename.Contains("nor_gl")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=filename.Contains("_diff_");
                importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=4;
                importer.SaveAndReimport();
            }
            const string skyPath=Folder+"/Dabudong_NightDawnSky.mat";
            var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(sky==null){sky=new Material(Shader.Find("FiveRivers/Night Dawn Sky"));AssetDatabase.CreateAsset(sky,skyPath);}
            sky.SetTexture("_Night",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/qwantani_night_puresky_2k.hdr"));
            sky.SetTexture("_Dawn",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/qwantani_dawn_puresky_2k.hdr"));
            sky.SetFloat("_NightExposure",.06f);sky.SetFloat("_DawnExposure",.7f);sky.SetFloat("_DawnBlend",0f);
            RenderSettings.skybox=sky;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity=.7f;camera.clearFlags=CameraClearFlags.Skybox;
            DynamicGI.UpdateEnvironment();EditorUtility.SetDirty(sky);
            var moon=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.name=="Moon");
            if(moon!=null){moon.intensity=.85f;moon.color=new Color(.74f,.82f,1f);moon.shadowStrength=.85f;moon.shadowBias=.035f;moon.shadowNormalBias=.25f;}
            var fill=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.name=="M1_MoonFill");
            if(fill!=null){fill.intensity=.4f;fill.color=new Color(.82f,.88f,1f);}
            void Detail(Material material,string surface,Vector2 scale,float strength)
            {
                material.SetTexture("_DetailNormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+surface+"_DetailNormal.png"));
                material.SetTextureScale("_DetailAlbedoMap",scale);
                material.SetFloat("_DetailAlbedoMapScale",0f);
                material.SetFloat("_DetailNormalMapScale",strength);
                material.DisableKeyword("_DETAIL_MULX2");material.EnableKeyword("_DETAIL_SCALED");
            }
            // Imported FBX materials are importer-owned. Persist scene changes in derived .mat assets.
            foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var assigned=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<assigned.Length;i++)
                {
                    var source=assigned[i];if(source==null)continue;
                    string path=AssetDatabase.GetAssetPath(source);
                    if(!path.EndsWith(".fbx",System.StringComparison.OrdinalIgnoreCase))continue;
                    string model=System.IO.Path.GetFileNameWithoutExtension(path);
                    string destination=Folder+"/"+model+"_"+source.name+".mat";
                    var derived=AssetDatabase.LoadAssetAtPath<Material>(destination);
                    if(derived==null){derived=new Material(source){name=model+"_"+source.name};AssetDatabase.CreateAsset(derived,destination);}
                    assigned[i]=derived;changed=true;
                }
                if(changed){renderer.sharedMaterials=assigned;EditorUtility.SetDirty(renderer);}
            }
            var mats=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&(m.shader.name=="Universal Render Pipeline/Lit"||m.shader.name=="FiveRivers/Anatomical Skin")).Distinct().ToArray();
            foreach(var mat in mats)
            {
                var name=mat.name.ToLowerInvariant();
                if(name.Contains("pine")||name.Contains("shrub")){mat.SetFloat("_Smoothness",.35f);mat.SetFloat("_BumpScale",.45f);}
                if(name=="m_pr_blanket"){mat.SetFloat("_Smoothness",.2f);Detail(mat,"Cotton",new Vector2(5,5),.2f);}
                if(name.Contains("sandbag")){mat.SetFloat("_Smoothness",.14f);mat.SetFloat("_BumpScale",.5f);}
                if(name=="m_wp_m1_wood"){mat.SetFloat("_Smoothness",.62f);mat.SetFloat("_BumpScale",.65f);}
                if(name=="m_wp_m1_metal"){mat.SetFloat("_Smoothness",.48f);mat.SetFloat("_BumpScale",.25f);mat.SetColor("_BaseColor",new Color(.55f,.57f,.58f));}
                if(name=="m_wp_m1_clip")mat.SetFloat("_Smoothness",.6f);
                if(name=="m_wp_m1_brass")mat.SetFloat("_Smoothness",.7f);
                if((name=="refined_hand_skin"||name=="m1_adulthand_skin")){mat.SetFloat("_Smoothness",.42f);mat.SetFloat("_BumpScale",.65f);mat.SetFloat("_OcclusionStrength",.6f);}
                if(name.Contains("baked_body")||name.Contains("baked_vest")||name.Contains("baked_cap"))mat.SetFloat("_Smoothness",.18f);
                if(name.Contains("baked_head"))mat.SetFloat("_Smoothness",.3f);
                if((name=="refined_hand_skin"||name=="m1_adulthand_skin")){Detail(mat,"Skin",new Vector2(7,7),.7f);if(mat.HasProperty("_MicroRoughness"))mat.SetTexture("_MicroRoughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Skin_Roughness.png"));}
                if(name=="m_wp_m1_wood")
                {
                    mat.SetTexture("_DetailNormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/american_walnut_veneer_nor_gl_1k.png"));
                    mat.SetTextureScale("_DetailAlbedoMap",new Vector2(3f,3f));
                    mat.SetFloat("_DetailAlbedoMapScale",0f);mat.SetFloat("_DetailNormalMapScale",.3f);
                    mat.DisableKeyword("_DETAIL_MULX2");mat.EnableKeyword("_DETAIL_SCALED");
                }
                if(name=="m_wp_m1_metal"||name=="m1_aperture_parkerized")Detail(mat,"Parkerized",new Vector2(6,6),.6f);
                if(name=="m1_fp_cloth"||name=="rl_blanket_august")
                {
                    mat.SetColor("_BaseColor",new Color(.245f,.28f,.18f));
                    mat.SetTexture("_BaseMap",null);
                    mat.SetTextureScale("_BaseMap",new Vector2(3f,6f));
                    mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Metallic",0f);
                    mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/stretch_poplin_nor_gl_1k.png"));
                    mat.SetFloat("_BumpScale",.08f);mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_DetailAlbedoMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/stretch_poplin_diff_1k.png"));
                    mat.SetTextureScale("_DetailAlbedoMap",new Vector2(.75f,2f));
                    mat.SetFloat("_DetailAlbedoMapScale",.12f);
                    mat.SetTexture("_DetailNormalMap",null);mat.SetFloat("_DetailNormalMapScale",0f);
                    mat.EnableKeyword("_DETAIL_SCALED");mat.DisableKeyword("_DETAIL_MULX2");
                }
                EditorUtility.SetDirty(mat);
            }
            // The revised trench uses the same photographed dry soil as the terrain layers.
            var trench=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Maps/DabudongRefined/Source/Art/Integrated20261023/Trench/Trench_Dry_Earth.mat");
            const string soil="Assets/_Project/Art/Maps/DabudongRefined/Source/Art/SurfacePolish20261023/";
            if(trench!=null){trench.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(soil+"Dry_Soil_BaseColor.jpg"));trench.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(soil+"Dry_Soil_Normal.png"));trench.SetFloat("_BumpScale",.5f);trench.SetTexture("_OcclusionMap",null);trench.DisableKeyword("_OCCLUSIONMAP");EditorUtility.SetDirty(trench);}
            foreach(var group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                if(!group.name.Contains("Korean_Red_Pine")&&!group.name.Contains("Summer_Shrub"))continue;
                var lods=group.GetLODs();
                if(lods.Length==3){lods[0].screenRelativeTransitionHeight=.16f;lods[1].screenRelativeTransitionHeight=.035f;lods[2].screenRelativeTransitionHeight=.006f;group.SetLODs(lods);EditorUtility.SetDirty(group);}
            }
            BlanketSurfaceDetail.Apply();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);EditorSceneManager.SaveScene(camera.gameObject.scene);
        }
    }
}
