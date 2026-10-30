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
        float blend;

        void Awake()
        {
            nightLight = moon.color;
            nightIntensity = moon.intensity;
            nightRotation = moon.transform.rotation;
            nightSky = RenderSettings.ambientSkyColor;
            nightEquator = RenderSettings.ambientEquatorColor;
            nightFog = RenderSettings.fogColor;
            nightBackground = view.backgroundColor;
        }

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Dawn) SpawnWalkers();
            // 새 회차 · 시작 화면 · (디버그) 새벽 이전 구간으로 돌아가면 밤으로
            if (t.To < FlowSection.Dawn && (blend > 0f || walkers.Count > 0))
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
            foreach (var w in walkers)
            {
                if (w == null) continue;
                w.position = Vector3.MoveTowards(w.position, w.position + w.forward, walkSpeed * Time.deltaTime);
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
        }

        void SpawnWalkers()
        {
            ClearWalkers();
            var start = points.reinforcementsStart.position;
            var end = points.reinforcementsEnd.position;
            var dir = (end - start);
            dir.y = 0f;
            for (int i = 0; i < reinforcements; i++)
            {
                var pos = start + new Vector3((i - reinforcements / 2f) * 2.2f, 0f, -i * 1.6f);
                var w = Instantiate(reinforcementPrefab, pos, Quaternion.LookRotation(dir), transform);
                if (w.TryGetComponent<AllyPose>(out var pose)) pose.Walk(walkSpeed); // A_ROK_Walk_Tired
                walkers.Add(w.transform);
            }
        }

        void ClearWalkers()
        {
            foreach (var w in walkers)
                if (w != null) Destroy(w.gameObject);
            walkers.Clear();
        }
    }
}
