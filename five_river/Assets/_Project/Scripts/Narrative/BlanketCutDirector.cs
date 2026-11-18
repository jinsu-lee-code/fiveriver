using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Staged moment of the silence (2026-11-18). The section is built on the covered body (GDD 8: "박용팔의 호출과 담요
    // 형체"), but from where the player stands it lay 14.6 m down a dark trench behind Park, a few pixels high, while
    // the only objective on screen sent him to the ammunition crate: a visitor could finish the section without
    // seeing it. As Park asks whether anyone has seen Man-su, one slow shot from the ground beyond the blanket shows
    // it, with the trench and the man calling behind it. Once per run. Space or Enter skips.
    public sealed class BlanketCutDirector : MonoBehaviour
    {
        public static bool Enabled = true;
        public static float Duration = 5.2f, HandBack = 1.0f;
        // camera in the blanket's frame: x = along the trench away from the player, y = up, z = across the trench
        public static Vector3 CameraFrom = new Vector3(2.9f, 0.8f, 0.3f), CameraTo = new Vector3(2.5f, 0.68f, 0.25f);
        public static Vector3 Look = new Vector3(-0.8f, 0.62f, 0f);
        public static float FovFrom = 48f, FovTo = 44f;

        FlowDirector director;
        PlayerController player;
        CutsceneRig rig;
        Vector3 origin, awayDir, acrossDir;
        bool playedThisRun;

        public bool Playing => rig != null && rig.Playing;

        public void Configure(FlowDirector flow, PlayerController playerController)
        {
            director = flow;
            player = playerController;
            rig = new CutsceneRig(this, flow, playerController, "Blanket") { CutBack = true };
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
            if (t.To != FlowSection.Silence) Finish();
        }

        void OnRetry(FlowCheckpoint checkpoint) => Finish();

        // False when it will not play (switched off, already shown this run, no blanket, another sequence running).
        public bool Begin(Transform blanket)
        {
            if (!Enabled || playedThisRun || Playing || blanket == null || !blanket.gameObject.activeInHierarchy) return false;
            if (director.Flow.Section != FlowSection.Silence || CutsceneRig.Running > 0) return false;
            origin = blanket.position;
            var away = origin - player.transform.position; away.y = 0f;
            if (away.sqrMagnitude < 0.36f) return false; // he is standing on the spot
            // Far away, the camera is beyond the blanket looking back down the trench, and the man who comes to the
            // body stands on the player's side of it. When the player is close (he often is: he was sent to this
            // end of the trench), that place is where he stands himself, so the whole arrangement is turned round:
            // the camera on the player's side, the man beyond the blanket.
            bool near = away.magnitude < NearLimit;
            awayDir = near ? -away.normalized : away.normalized;
            acrossDir = Vector3.Cross(Vector3.up, awayDir);
            if (!rig.Begin(Duration, HandBack)) return false;
            playedThisRun = true;
            // the player comes out of it looking at the place
            player.transform.rotation = Quaternion.LookRotation(away.normalized);
            return true;
        }

        public static float NearLimit = 4.5f;
        // Where a man stands at the body for this shot (d metres from it, on the side away from the camera), and the way he faces.
        public Vector3 StandSpot(float d) => origin - awayDir * d;
        public Quaternion StandFacing => Quaternion.LookRotation(awayDir);

        Vector3 Local(Vector3 offset)
        {
            return origin + awayDir * offset.x + Vector3.up * offset.y + acrossDir * offset.z;
        }

        void Update()
        {
            if (!Playing || !rig.Advance()) return;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Duration - HandBack, rig.Time));
            var position = Local(Vector3.Lerp(CameraFrom, CameraTo, u));
            // never under the trench floor
            var terrain = Terrain.activeTerrain;
            if (terrain != null) position.y = Mathf.Max(position.y, terrain.SampleHeight(position) + terrain.transform.position.y + 0.25f);
            var target = Local(Look);
            if (rig.Frame(position, Quaternion.LookRotation(target - position), Mathf.Lerp(FovFrom, FovTo, u))) Finish();
        }

        void Finish()
        {
            if (Playing) rig.End();
        }
    }
}
