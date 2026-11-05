using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.Combat
{
    // World-space wood dust and a small surface mark. No HUD hit confirmation or reward sound.
    public static class M1SurfaceImpact
    {
        static Material dustMaterial;
        static Material markMaterial;
        static Texture2D impactTexture;
        static readonly Queue<GameObject> Marks = new Queue<GameObject>();
        static readonly Queue<GameObject> Dust = new Queue<GameObject>();

        public static void Clear()
        {
            while(Marks.Count>0){var mark=Marks.Dequeue();if(mark!=null)Object.Destroy(mark);}
            while(Dust.Count>0){var dust=Dust.Dequeue();if(dust!=null)Object.Destroy(dust);}
            if(dustMaterial!=null)Object.Destroy(dustMaterial);
            if(markMaterial!=null)Object.Destroy(markMaterial);
            if(impactTexture!=null)Object.Destroy(impactTexture);
            dustMaterial=null;markMaterial=null;impactTexture=null;
        }

        public static void Wood(RaycastHit hit)
        {
            EnsureMaterials();
            var go = new GameObject("M1_WoodImpact");
            Dust.Enqueue(go);
            while(Dust.Count>16){var old=Dust.Dequeue();if(old!=null)Object.Destroy(old);}
            go.transform.SetPositionAndRotation(hit.point + hit.normal * 0.006f, Quaternion.LookRotation(hit.normal));
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.duration = 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f);
            main.startColor = new Color(0.55f, 0.40f, 0.25f, 0.75f);
            main.gravityModifier = 0.7f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 14;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.008f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            particles.Play();
            Object.Destroy(go, 0.6f);

            var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = "M1_WoodStrike";
            mark.layer = 2;
            Object.Destroy(mark.GetComponent<Collider>());
            mark.transform.SetPositionAndRotation(hit.point + hit.normal * 0.0015f, Quaternion.LookRotation(-hit.normal));
            mark.transform.localScale = Vector3.one * 0.024f;
            mark.transform.SetParent(hit.collider.transform, true);
            var markRenderer = mark.GetComponent<MeshRenderer>();
            markRenderer.sharedMaterial = markMaterial;
            markRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Marks.Enqueue(mark);
            while (Marks.Count > 16) { var old = Marks.Dequeue(); if (old != null) Object.Destroy(old); }
        }

        static void EnsureMaterials()
        {
            if (dustMaterial != null) return;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            impactTexture=texture;
            texture.name = "M1_ImpactSoftCircle";
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 16f) / 16f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, radius)));
                }
            texture.Apply(false, true);
            Material Make(Color color,bool particle=false)
            {
                // Players strip shaders not referenced by a scene material; fall back instead of throwing.
                var shader = Shader.Find(particle?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                var material = new Material(shader);
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                return material;
            }
            dustMaterial = Make(Color.white,true);
            markMaterial = Make(new Color(0.12f, 0.075f, 0.035f, 0.95f));
        }
    }
}
