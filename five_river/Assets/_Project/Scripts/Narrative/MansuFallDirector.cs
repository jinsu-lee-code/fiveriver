using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.DebugTools;
using FiveRivers.UI;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Staged moment of the western assault (2026-11-12): Choi Man-su is hit. Until now he fell at the far end of
    // the trench while the player was busy shooting the other way, and the loss the silence is built on went unseen.
    //   shot 1  along the trench beside him: he is still firing, then he is hit and goes down
    //   shot 2  low on the ground beyond him, the trench and the men still fighting behind, then back into the player's eyes — turned to his place
    // NarrativeDirector.West starts it and plays the fall when HitAt has passed. Once per run. Space or Enter skips.
    public sealed class MansuFallDirector : MonoBehaviour, IDebugPanel
    {
        public static bool Enabled = true;
        public const float HitAt = 0.9f;
        public static float Shot1 = 3.4f, Duration = 7.6f, HandBack = 1.3f;
        // camera offsets in his own frame (x = along the trench, toward the player's side; y = up; z = toward the enemy)
        public static Vector3 SideCamera = new Vector3(2.3f, 1.25f, -0.25f), SideLook = new Vector3(0f, 1.15f, 0.1f);
        public static Vector3 LowCamera = new Vector3(-2.5f, 0.5f, -1.7f), LowLook = new Vector3(0.2f, 0.3f, -0.9f);

        FlowDirector director;
        PlayerController player;
        ObjectiveDisplay objective;
        FiveRivers.Data.GameText copy;
        CutsceneRig rig;
        Transform subject;
        Animator subjectAnimator;
        Vector3 origin, look;
        Quaternion frame;
        float sideSign = 1f;
        bool playedThisRun;

        public bool Playing => rig != null && rig.Playing;
        public float Elapsed => rig != null ? rig.Time : 0f;

        public void Configure(FlowDirector flow, PlayerController playerController, ObjectiveDisplay objectiveDisplay, FiveRivers.Data.GameText text)
        {
            director = flow;
            player = playerController;
            objective = objectiveDisplay;
            copy = text;
            rig = new CutsceneRig(this, flow, playerController, "MansuFall") { CutBack = true };
        }

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
            director.Flow.RetryRequested += OnRetry;
        }

        void OnDestroy()
        {
            if (rig != null) rig.End();
            if (director == null || director.Flow == null) return;
            director.Flow.SectionChanged -= OnSectionChanged;
            director.Flow.RetryRequested -= OnRetry;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Title || t.To == FlowSection.Tutorial) playedThisRun = false;
            if (t.To != FlowSection.WestAssault) Finish();
        }

        void OnRetry(FlowCheckpoint checkpoint) => Finish();

        // Starts the sequence on him. False when it will not play (switched off, already shown this run).
        public bool Begin(Transform mansu)
        {
            if (!Enabled || playedThisRun || Playing || mansu == null || !director.Flow.IsExpanded) return false;
            subject = mansu;
            subjectAnimator = mansu.GetComponentInChildren<Animator>();
            origin = mansu.position;
            var facing = mansu.forward; facing.y = 0f;
            frame = facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            // stay on the side of him the player is on, so the cut does not jump across the line between them
            sideSign = Vector3.Dot(player.transform.position - origin, frame * Vector3.right) >= 0f ? 1f : -1f;
            look = Local(SideLook);
            if (!rig.Begin(Duration, HandBack)) return false;
            playedThisRun = true;
            objective.Hide();
            // the player comes out of it looking at his place
            var toward = origin - player.transform.position; toward.y = 0f;
            if (toward.sqrMagnitude > 1f) player.transform.rotation = Quaternion.LookRotation(toward);
            return true;
        }

        Vector3 Local(Vector3 offset) => origin + frame * new Vector3(offset.x * sideSign, offset.y, offset.z);

        void Update()
        {
            if (!Playing || !rig.Advance()) return;
            float time = rig.Time;
            Vector3 position, target; float fov;
            var chest = Chest();
            var along = frame * Vector3.right * sideSign;
            if (time < Shot1)
            {
                float u = Mathf.InverseLerp(0f, Shot1, time);
                position = Local(SideCamera) - along * (0.35f * u);
                target = Vector3.Lerp(Local(SideLook), chest, 0.8f); // follows him down as he falls
                fov = Mathf.Lerp(40f, 36f, u);
            }
            else
            {
                float u = Mathf.InverseLerp(Shot1, Duration - HandBack, time);
                position = Local(LowCamera) + along * (0.4f * u);
                target = Vector3.Lerp(Local(LowLook), chest, 0.7f);
                fov = Mathf.Lerp(40f, 36f, u);
            }
            look = time < Shot1 || time - Time.deltaTime >= Shot1 ? Vector3.Lerp(look, target, 1f - Mathf.Exp(-Time.deltaTime * 6f)) : target;
            if (rig.Frame(position, Quaternion.LookRotation(look - position), fov)) Finish();
        }

        Vector3 Chest()
        {
            if (subject == null) return origin + Vector3.up;
            var chest = subjectAnimator != null && subjectAnimator.isHuman ? subjectAnimator.GetBoneTransform(HumanBodyBones.Chest) : null;
            return chest != null ? chest.position : subject.position + Vector3.up * 1.1f;
        }

        void Finish()
        {
            if (!Playing) return;
            rig.End();
            if (director.Flow.Section == FlowSection.WestAssault && copy != null) objective.Show(copy.objectiveWest);
        }

        public string DebugTitle => "최만수 전사 연출";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label(Playing ? $"재생 중 {rig.Time:0.0}/{Duration:0.0}초 (Space · Enter 건너뛰기)" : playedThisRun ? "이번 회차에 재생함" : "서쪽 공세에서 최만수가 쓰러질 때 재생");
            Enabled = GUILayout.Toggle(Enabled, "최만수 전사 연출 켜기");
#endif
        }
    }
}
