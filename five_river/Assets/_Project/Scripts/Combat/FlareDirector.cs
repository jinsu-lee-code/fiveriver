using System;
using System.Collections.Generic;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 조명탄 리듬 (GDD 2번 핵심 경험 1). S2에 한 발, S3에는 일정 간격으로 쏜다.
    // Illumination(0~1)을 적 명중률 · 안내 문구가 읽는다.
    public sealed class FlareDirector : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] FlareConfig config;
        [SerializeField] Flare flarePrefab;
        [SerializeField] Effects effects;
        [SerializeField] Vector2 areaX = new Vector2(-15f, 15f);
        [SerializeField] Vector2 areaZ = new Vector2(35f, 60f);

        readonly List<Flare> flares = new List<Flare>();
        float nextLaunch = float.PositiveInfinity;
        bool repeat;
        bool wasBright;
        System.Random rng = new System.Random(1950);

        public float Illumination { get; private set; }

        public event Action Launched;
        public event Action Darkening; // 마지막으로 타던 조명탄이 꺼지기 시작할 때

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
            director.Flow.RetryRequested += OnRetry;
        }

        void OnSectionChanged(FlowTransition t)
        {
            switch (t.To)
            {
                case FlowSection.Scout:
                    Schedule(config.scoutFlareDelay, false);
                    break;
                case FlowSection.Assault:
                    Schedule(config.assaultFirstDelay, true);
                    break;
                case FlowSection.Title:
                case FlowSection.Tutorial:
                    ClearAll();
                    nextLaunch = float.PositiveInfinity;
                    break;
                default:
                    nextLaunch = float.PositiveInfinity; // 이미 뜬 조명탄은 다 타고 꺼진다
                    break;
            }
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            ClearAll();
            if (checkpoint == FlowCheckpoint.FirstContact) Schedule(config.scoutFlareDelay, false);
            else if (checkpoint == FlowCheckpoint.MainAssault) Schedule(config.assaultFirstDelay, true);
        }

        void Schedule(float delay, bool repeating)
        {
            nextLaunch = Time.time + delay;
            repeat = repeating;
        }

        void Update()
        {
            if (Time.time >= nextLaunch)
            {
                Launch();
                nextLaunch = repeat ? Time.time + config.assaultInterval : float.PositiveInfinity;
            }

            float illum = 0f;
            bool anyBurning = false;
            for (int i = flares.Count - 1; i >= 0; i--)
            {
                var f = flares[i];
                if (f == null)
                {
                    flares.RemoveAt(i);
                    continue;
                }
                illum = Mathf.Max(illum, f.Brightness);
                if (!f.Fading) anyBurning = true;
            }
            Illumination = illum;
            if (wasBright && !anyBurning && illum > 0f) Darkening?.Invoke();
            wasBright = anyBurning;
        }

        public void Launch()
        {
            float x = Mathf.Lerp(areaX.x, areaX.y, (float)rng.NextDouble());
            float z = Mathf.Lerp(areaZ.x, areaZ.y, (float)rng.NextDouble());
            var pos = new Vector3(x, config.launchHeight, z);
            var flare = effects.SpawnTracked(flarePrefab, pos, Quaternion.identity);
            flare.Ignite(config);
            flares.Add(flare);
            Launched?.Invoke();
        }

        public void ClearAll()
        {
            foreach (var f in flares)
                if (f != null) Destroy(f.gameObject);
            flares.Clear();
            Illumination = 0f;
            wasBright = false;
        }
    }
}
