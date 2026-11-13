using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FiveRivers.Narrative
{
    // What every staged moment after the tank entrance shares (2026-11-12): its own camera above the player's,
    // the two black bars, and the player taken out of play while it runs — no control, cannot be hurt, the run
    // clock standing still, the HUD and the first-person body hidden. The owner moves the camera every frame
    // through Frame(); the last HandBack seconds ease it into the player's own eyes.
    public sealed class CutsceneRig
    {
        public static int Running { get; private set; }
        public const float BarFade = 0.4f, BarHeight = 0.105f, SkipAfter = 0.6f;
        // after the view comes back the player stays unhurt a moment longer, so the first thing seen is not a hit
        public const float Grace = 1.5f;
        // when true the hand-back is a cut into the player's eyes under the bars, not a move (for cameras with a wall between)
        public bool CutBack { get; set; }

        readonly MonoBehaviour owner;
        readonly FlowDirector director;
        readonly PlayerController player;
        readonly string name;
        Hud hud;
        Camera sequenceCamera;
        Renderer[] playerRenderers;
        PlayerHealth playerHealth;
        bool invulnerableBefore;
        CanvasGroup bars;
        float clockBefore;

        public Camera PlayerCamera { get; private set; }
        public bool Playing { get; private set; }
        public float Time { get; private set; }
        public float Duration { get; private set; }
        public float HandBack { get; private set; }

        public CutsceneRig(MonoBehaviour owner, FlowDirector director, PlayerController player, string name)
        {
            this.owner = owner;
            this.director = director;
            this.player = player;
            this.name = name;
        }

        public bool Begin(float duration, float handBack)
        {
            if (Playing) return false;
            PlayerCamera = player.CameraPivot.GetComponentInChildren<Camera>();
            if (PlayerCamera == null) return false;
            if (hud == null) hud = Object.FindFirstObjectByType<Hud>();
            Duration = duration;
            HandBack = handBack;
            Time = 0f;
            EnsureCamera();
            EnsureBars();
            Playing = true;
            Running++;
            director.InputGate.Block(owner);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            clockBefore = director.ClockScale;
            director.ClockScale = 0f;
            if (hud != null) hud.Cinematic = true;
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                invulnerableBefore = playerHealth.Invulnerable;
                playerHealth.Invulnerable = true;
            }
            playerRenderers = HidePlayer(player);
            cutDone = false;
            PlayerCamera.enabled = false;
            sequenceCamera.enabled = true;
            bars.gameObject.SetActive(true);
            bars.alpha = 0f;
            return true;
        }

        // Advances the clock; false while the game is paused. Space or Enter jumps to the hand-back.
        public bool Advance()
        {
            if (!Playing || director.Flow.Paused) return false;
            Time += UnityEngine.Time.deltaTime;
            var keys = Keyboard.current;
            if (keys != null && Time > SkipAfter && Time < Duration - HandBack
                && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
                Time = Duration - HandBack;
            return true;
        }

        // Places the camera for this frame. Returns true when the sequence has run out (the owner then ends it).
        public bool Frame(Vector3 position, Quaternion rotation, float fov)
        {
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Duration - HandBack, Duration, Time));
            if (CutBack)
            {
                back = Time >= Duration - HandBack * .5f ? 1f : 0f;
                // the cut goes to the player's own camera, rifle and hands included, so nothing pops in when the
                // bars lift (2026-11-13)
                if (back >= 1f && !cutDone)
                {
                    cutDone = true;
                    ShowPlayer(player, playerRenderers);
                    sequenceCamera.enabled = false;
                    PlayerCamera.enabled = true;
                }
            }
            var own = PlayerCamera.transform;
            sequenceCamera.transform.SetPositionAndRotation(Vector3.Lerp(position, own.position, back), Quaternion.Slerp(rotation, own.rotation, back));
            sequenceCamera.fieldOfView = Mathf.Lerp(fov, PlayerCamera.fieldOfView, back);
            bars.alpha = Mathf.Min(Mathf.InverseLerp(0f, BarFade, Time), Mathf.InverseLerp(Duration, Duration - BarFade, Time));
            return Time >= Duration;
        }

        public void End()
        {
            if (!Playing) return;
            Playing = false;
            Running = Mathf.Max(0, Running - 1);
            director.InputGate.Unblock(owner);
            director.ClockScale = clockBefore;
            if (hud != null) hud.Cinematic = false;
            if (playerHealth != null)
            {
                if (invulnerableBefore || !owner.isActiveAndEnabled) playerHealth.Invulnerable = invulnerableBefore;
                else owner.StartCoroutine(ReleaseAfterGrace(playerHealth));
            }
            ShowPlayer(player, playerRenderers);
            if (sequenceCamera != null) sequenceCamera.enabled = false;
            if (PlayerCamera != null) PlayerCamera.enabled = true;
            if (bars != null) bars.gameObject.SetActive(false);
        }

        bool cutDone;

        // The first-person arms and weapons are not part of a staged shot. Shared by every sequence.
        public static Renderer[] HidePlayer(PlayerController player)
        {
            var renderers = player.GetComponentsInChildren<Renderer>(false);
            foreach (var r in renderers) r.forceRenderingOff = true;
            var weapons = player.GetComponentInChildren<WeaponHolder>(true);
            if (weapons != null) weapons.CutsceneHidden = true;
            return renderers;
        }

        public static void ShowPlayer(PlayerController player, Renderer[] renderers)
        {
            if (renderers != null) foreach (var r in renderers) if (r != null) r.forceRenderingOff = false;
            var weapons = player != null ? player.GetComponentInChildren<WeaponHolder>(true) : null;
            if (weapons != null) weapons.CutsceneHidden = false;
        }

        System.Collections.IEnumerator ReleaseAfterGrace(PlayerHealth health)
        {
            yield return new WaitForSeconds(Grace);
            if (health != null && !Playing) health.Invulnerable = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Running = 0;

        void EnsureCamera()
        {
            if (sequenceCamera != null) return;
            var holder = new GameObject(name + "Camera");
            holder.transform.SetParent(owner.transform, false);
            sequenceCamera = holder.AddComponent<Camera>();
            sequenceCamera.CopyFrom(PlayerCamera);
            sequenceCamera.depth = PlayerCamera.depth + 1f;
            var from = PlayerCamera.GetUniversalAdditionalCameraData();
            var to = sequenceCamera.GetUniversalAdditionalCameraData();
            to.renderPostProcessing = from.renderPostProcessing;
            to.antialiasing = from.antialiasing;
            to.antialiasingQuality = from.antialiasingQuality;
            to.volumeLayerMask = from.volumeLayerMask;
            to.renderShadows = from.renderShadows;
            sequenceCamera.enabled = false;
        }

        void EnsureBars()
        {
            if (bars != null) return;
            var holder = new GameObject(name + "Bars", typeof(Canvas), typeof(CanvasGroup));
            holder.transform.SetParent(owner.transform, false);
            var canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;
            bars = holder.GetComponent<CanvasGroup>();
            bars.interactable = false;
            bars.blocksRaycasts = false;
            for (int i = 0; i < 2; i++)
            {
                var bar = new GameObject(i == 0 ? "Top" : "Bottom", typeof(Image)).GetComponent<Image>();
                bar.transform.SetParent(holder.transform, false);
                bar.color = Color.black;
                bar.raycastTarget = false;
                var rect = bar.rectTransform;
                rect.anchorMin = new Vector2(0f, i == 0 ? 1f - BarHeight : 0f);
                rect.anchorMax = new Vector2(1f, i == 0 ? 1f : BarHeight);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            holder.SetActive(false);
        }
    }
}
