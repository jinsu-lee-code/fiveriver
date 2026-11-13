using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.DebugTools;
using FiveRivers.UI;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Staged moment of the summit (2026-11-12): the crest is taken and the day breaks.
    //   shot 1  from the crest: along the dark ridge, round to the valley the squad came up out of, where the sky
    //           is turning light (the dawn runs faster while the view is on it)
    //   shot 2  down in the valley: the student soldiers who came on the truck, standing by the road, from in front
    // then back into the player's eyes, turned toward the truck (the objective that follows asks him to look at it).
    // Plays when the Dawn section opens on the expanded run, once per run. Space or Enter skips.
    public sealed class SummitDawnDirector : MonoBehaviour, IDebugPanel
    {
        public static bool Enabled = true;
        public static float Shot1 = 4.2f, Duration = 10.2f, HandBack = 1.4f;
        public static float PanFromPitch = -5f, PanToPitch = 7f, DawnRate = 3f, TruckSlide = 0.8f;
        // camera and its aim in the truck's own frame (x = to its right, y = above the ground, z = ahead of it);
        // the student soldiers stand about 5 m to its right and 13 m behind it
        public static Vector3 TruckCamera = new Vector3(2.6f, 1.35f, -6.8f), TruckLook = new Vector3(5.4f, 1.3f, -13f);

        FlowDirector director;
        PlayerController player;
        DawnDirector dawn;
        ObjectiveDisplay objective;
        CutsceneRig rig;
        bool playedThisRun, pending;
        Vector3 toSun, downhill, eye, truckFocus, truckCamera, truckSide;
        bool truckCameraSet;

        public bool Playing => rig != null && rig.Playing;
        public float Elapsed => rig != null ? rig.Time : 0f;

        public void Configure(FlowDirector flow, PlayerController playerController, DawnDirector dawnDirector, ObjectiveDisplay objectiveDisplay)
        {
            director = flow;
            player = playerController;
            dawn = dawnDirector;
            objective = objectiveDisplay;
            // the last shot is ~100 m down in the valley: a cut under the bars, not a flight back up the hill (2026-11-13)
            rig = new CutsceneRig(this, flow, playerController, "SummitDawn") { CutBack = true };
        }

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
        }

        void OnDestroy()
        {
            if (rig != null) rig.End();
            if (director != null && director.Flow != null) director.Flow.SectionChanged -= OnSectionChanged;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Title || t.To == FlowSection.Tutorial) playedThisRun = false;
            pending = t.To == FlowSection.Dawn && Enabled && !playedThisRun && director.Flow.IsExpanded;
            if (t.To != FlowSection.Dawn && Playing) Finish();
        }

        void Update()
        {
            if (pending)
            {
                pending = false;
                Begin();
            }
            if (!Playing || !rig.Advance()) return;
            float time = rig.Time;
            Vector3 position; Quaternion rotation; float fov;
            if (time < Shot1)
            {
                // from the crest: along the dark ridge, round to the valley the squad came up out of, where the sky is light
                float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, Shot1, time));
                position = eye + Vector3.up * 0.25f;
                var from = Quaternion.LookRotation(toSun) * Quaternion.Euler(PanFromPitch, 0f, 0f);
                var to = Quaternion.LookRotation(downhill) * Quaternion.Euler(PanToPitch, 0f, 0f);
                rotation = Quaternion.Slerp(from, to, u);
                fov = Mathf.Lerp(50f, 44f, u);
            }
            else
            {
                // down in the valley: the student soldiers by the road, seen from the truck's side
                float u = Mathf.InverseLerp(Shot1, Duration - HandBack, time);
                var truck = dawn.Truck;
                if (truck != null && !truckCameraSet)
                {
                    truckCameraSet = true;
                    if (dawn.TruckSequence != null) dawn.TruckSequence.RevealNow();
                    var forward = truck.forward; forward.y = 0f; forward.Normalize();
                    truckSide = Vector3.Cross(Vector3.up, forward);
                    truckFocus = OnGround(truck.position + forward * TruckLook.z + truckSide * TruckLook.x, TruckLook.y);
                    truckCamera = OnGround(truck.position + forward * TruckCamera.z + truckSide * TruckCamera.x, TruckCamera.y);
                }
                if (truckCameraSet)
                {
                    position = truckCamera + truckSide * (TruckSlide * u);
                    rotation = Quaternion.LookRotation(truckFocus - position);
                    fov = Mathf.Lerp(34f, 28f, u);
                }
                else
                {
                    // no truck in this scene: stay on the sky
                    position = eye + Vector3.up * 0.25f;
                    rotation = Quaternion.LookRotation(downhill) * Quaternion.Euler(PanToPitch, 0f, 0f);
                    fov = 44f;
                }
            }
            if (rig.Frame(position, rotation, fov)) Finish();
        }

        Vector3 OnGround(Vector3 p, float above)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null) p.y = Mathf.Max(p.y, terrain.SampleHeight(p) + terrain.transform.position.y) + above;
            return p;
        }

        void Finish()
        {
            dawn.DawnRate = 1f;
            rig.End();
        }

        void Begin()
        {
            var camera = player.CameraPivot.GetComponentInChildren<Camera>();
            if (camera == null) return;
            eye = camera.transform.position;
            // where the dawn light comes from, flattened to the horizon
            toSun = -(Quaternion.Euler(0f, dawn.DawnLightEuler.y, 0f) * Vector3.forward);
            // the way the squad came up: from the crest toward the trench
            downhill = (dawn.TruckStop != null ? dawn.TruckStop.position : eye - toSun * 50f) - eye;
            downhill.y = 0f; downhill.Normalize();
            truckCameraSet = false;
            var truck = dawn.Truck;
            if (!rig.Begin(truck != null ? Duration : Shot1 + HandBack, HandBack)) return;
            playedThisRun = true;
            dawn.DawnRate = DawnRate; // the sky turns while the view is on it
            objective.Hide();
            // the player comes out of it looking down at where the truck stops
            if (truck != null)
            {
                var stop = dawn.TruckStop != null ? dawn.TruckStop.position : truck.position;
                var toward = stop - player.transform.position; toward.y = 0f;
                if (toward.sqrMagnitude > 1f) player.transform.rotation = Quaternion.LookRotation(toward);
            }
        }

        public string DebugTitle => "정상 새벽 연출";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label(Playing ? $"재생 중 {rig.Time:0.0}/{rig.Duration:0.0}초 (Space · Enter 건너뛰기)" : playedThisRun ? "이번 회차에 재생함" : "정상에 올라 새벽 구간이 열릴 때 재생");
            Enabled = GUILayout.Toggle(Enabled, "정상 새벽 연출 켜기");
#endif
        }
    }
}
