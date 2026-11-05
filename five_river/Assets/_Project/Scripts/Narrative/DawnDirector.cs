using System.Collections.Generic;
using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // S6 새벽: 20초 동안 밤 → 새벽빛으로 바뀌고, 뒤쪽에서 보충병 실루엣이 지친 걸음으로 들어온다 (GDD 4번 S6).
    // 새 회차가 시작되면 밤으로 되돌린다.
    public sealed class DawnDirector : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] LevelPoints points;
        [SerializeField] Light moon;
        [SerializeField] Camera view;
        [SerializeField] GameObject reinforcementPrefab;
        [SerializeField] int reinforcements = 4;
        [SerializeField] float walkSpeed = 1.1f;

        [Header("새벽 보충병 수송")]
        [SerializeField] DawnTruckSequence truckPrefab;
        [SerializeField] Transform truckArrivalStart;
        [SerializeField] Transform truckArrivalStop;
        DawnTruckSequence truckSequence;
        [SerializeField] FiveRivers.Data.GameText.Line arrivalNarration = new FiveRivers.Data.GameText.Line
        {
            text = "동이 틀 무렵, 학도병들이 보충 인원으로 참호에 합류했다.",
            duration = 7f, voice = ""
        };

        [Header("새벽 색")]
        [SerializeField] Color dawnLight = new Color(1f, 0.78f, 0.6f);
        [SerializeField] float dawnIntensity = 1.1f;
        [SerializeField] Vector3 dawnLightEuler = new Vector3(8f, 120f, 0f); // 오른쪽 뒤에서 낮게 (실루엣)
        [SerializeField] Color dawnSky = new Color(0.55f, 0.55f, 0.62f);
        [SerializeField] Color dawnEquator = new Color(0.45f, 0.40f, 0.38f);
        [SerializeField] Color dawnFog = new Color(0.52f, 0.50f, 0.55f);

        Color nightLight, nightSky, nightEquator, nightFog, nightBackground;
        float nightIntensity;
        Quaternion nightRotation;
        readonly List<Transform> walkers = new List<Transform>();
        readonly List<Vector3> walkerDestinations = new List<Vector3>();
        readonly List<Animator> walkerAnimators = new List<Animator>();
        static readonly int SpeedId = Animator.StringToHash("Speed");
        Terrain ground;
        float blend;
        Material originalSky, runtimeSky;
        float lastEnvironmentBlend=-1f;

        void Awake()
        {
            ground = Terrain.activeTerrain;
            nightLight = moon.color;
            nightIntensity = moon.intensity;
            nightRotation = moon.transform.rotation;
            nightSky = RenderSettings.ambientSkyColor;
            nightEquator = RenderSettings.ambientEquatorColor;
            nightFog = RenderSettings.fogColor;
            nightBackground = view.backgroundColor;
            originalSky=RenderSettings.skybox;
            if(originalSky!=null && originalSky.HasProperty("_DawnBlend"))
            {
                runtimeSky=new Material(originalSky){name="NightDawnSky_Runtime"};
                runtimeSky.SetFloat("_DawnBlend",0f);
                RenderSettings.skybox=runtimeSky;
            }
        }

        void OnDestroy()
        {
            if(director!=null && director.Flow!=null)director.Flow.SectionChanged-=OnSectionChanged;
            ClearWalkers();
            if(runtimeSky!=null)
            {
                if(RenderSettings.skybox==runtimeSky)RenderSettings.skybox=originalSky;
                Destroy(runtimeSky);
            }
        }

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Dawn) SpawnWalkers();
            if (t.To == FlowSection.Result && truckSequence != null) truckSequence.StopEngine();
            // 새 회차 · 시작 화면 · (디버그) 새벽 이전 구간으로 돌아가면 밤으로
            if (t.To < FlowSection.Dawn && (blend > 0f || walkers.Count > 0 || truckSequence != null))
            {
                ClearWalkers();
                blend = 0f;
                Apply();
            }
        }

        void Update()
        {
            var flow = director.Flow;
            if (flow.Section == FlowSection.Dawn || flow.Section == FlowSection.Result)
            {
                float duration = Mathf.Max(1f, director.Config.dawnDuration);
                blend = flow.Section == FlowSection.Result ? 1f : Mathf.Clamp01((float)flow.SectionTime / duration);
                Apply();
            }
            if (truckSequence != null)
            {
                truckSequence.SetPaused(flow.Paused);
                if (!flow.Paused) truckSequence.Tick(Time.deltaTime);
            }
            for (int i=0;i<walkers.Count;i++)
            {
                var w=walkers[i];
                if (w == null) continue;
                var destination=walkerDestinations[i];
                destination.y=w.position.y;
                bool moving=(destination-w.position).sqrMagnitude>.02f;
                var position=Vector3.MoveTowards(w.position,destination,walkSpeed*Time.deltaTime);
                Ground(ref position);
                w.position=position;
                if(walkerAnimators[i]!=null)walkerAnimators[i].SetFloat(SpeedId,moving?walkSpeed:0f);
            }
        }

        void Apply()
        {
            float e = blend * blend * (3f - 2f * blend);
            moon.color = Color.Lerp(nightLight, dawnLight, e);
            moon.intensity = Mathf.Lerp(nightIntensity, dawnIntensity, e);
            moon.transform.rotation = Quaternion.Slerp(nightRotation, Quaternion.Euler(dawnLightEuler), e);
            RenderSettings.ambientSkyColor = Color.Lerp(nightSky, dawnSky, e);
            RenderSettings.ambientEquatorColor = Color.Lerp(nightEquator, dawnEquator, e);
            RenderSettings.fogColor = Color.Lerp(nightFog, dawnFog, e);
            view.backgroundColor = Color.Lerp(nightBackground, dawnFog, e);
            if(runtimeSky!=null)
            {
                runtimeSky.SetFloat("_DawnBlend",e);
                // Refresh ambient reflection at a few blend milestones, not every frame.
                if(Mathf.Abs(e-lastEnvironmentBlend)>=.24f || (e==0f&&lastEnvironmentBlend!=0f) || (e==1f&&lastEnvironmentBlend!=1f))
                { DynamicGI.UpdateEnvironment();lastEnvironmentBlend=e; }
            }
        }

        void SpawnWalkers()
        {
            ClearWalkers();
            if (truckPrefab != null)
            {
                truckSequence = Instantiate(truckPrefab, transform);
                truckSequence.Arrived += OnReinforcementsArrived;
                if (!truckSequence.Begin(reinforcementPrefab, reinforcements, points.reinforcementsEnd,
                    walkSpeed, ground, truckArrivalStart, truckArrivalStop))
                {
                    Debug.LogError("Dawn truck route or passenger sockets are incomplete.", this);
                    Destroy(truckSequence.gameObject);
                    truckSequence = null;
                }
                return;
            }
            var start = points.reinforcementsStart.position;
            var end = points.reinforcementsEnd.position;
            var dir = (end - start);
            dir.y = 0f;
            if(dir.sqrMagnitude<.001f)return;
            var rotation=Quaternion.LookRotation(dir);
            for (int i = 0; i < reinforcements; i++)
            {
                float lateral=(i-(reinforcements-1)*.5f)*2.2f;
                var pos = start + rotation*new Vector3(lateral,0f,-i*1.6f);
                Ground(ref pos);
                var w = Instantiate(reinforcementPrefab, pos, rotation, transform);
                walkers.Add(w.transform);
                walkerDestinations.Add(end+rotation*new Vector3(lateral,0f,0f));
                var animator=w.GetComponentInChildren<Animator>();
                if(animator!=null)
                {
                    animator.applyRootMotion=false;
                    animator.SetFloat(SpeedId,walkSpeed);
                    animator.Play("Walk_Tired",0,i*.23f);
                }
                walkerAnimators.Add(animator);
            }
        }

        void OnReinforcementsArrived()
        {
            var subtitles = FindFirstObjectByType<FiveRivers.UI.SubtitleDisplay>();
            if (subtitles != null) subtitles.Say(arrivalNarration);
        }

        void Ground(ref Vector3 position)
        {
            if(ground==null||ground.terrainData==null)return;
            var local=position-ground.transform.position;var size=ground.terrainData.size;
            if(local.x<0f||local.z<0f||local.x>size.x||local.z>size.z)return;
            position.y=ground.SampleHeight(position)+ground.transform.position.y+.008f;
        }

        void ClearWalkers()
        {
            if (truckSequence != null)
            {
                truckSequence.Arrived -= OnReinforcementsArrived;
                truckSequence.gameObject.SetActive(false);
                Destroy(truckSequence.gameObject);
                truckSequence = null;
            }
            foreach (var w in walkers)
                if (w != null) Destroy(w.gameObject);
            walkers.Clear();
            walkerDestinations.Clear();
            walkerAnimators.Clear();
        }
    }
}



