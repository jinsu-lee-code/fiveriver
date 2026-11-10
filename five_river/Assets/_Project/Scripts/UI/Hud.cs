using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FiveRivers.UI
{
    // 전투 HUD: 오른쪽 아래 무기 · 탄약(클립 8칸), 가운데 조준선 · 상호작용 안내, 피격 테두리, 화면 어둡게.
    // 히트마커 · 점수는 없다. 피격은 붉은 테두리로만 알린다.
    public sealed class Hud : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth health;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] Resupplier resupplier;
        [SerializeField] RunCoordinator run;
        [SerializeField] GameText text;

        [Header("화면 요소")]
        [SerializeField] CanvasGroup combatGroup;
        [SerializeField] TMP_Text weaponName;
        [SerializeField] TMP_Text ammoMain;
        [SerializeField] TMP_Text ammoReserve;
        [SerializeField] Image[] clipPips;
        [SerializeField] RectTransform[] crosshairLines; // 위 · 아래 · 왼쪽 · 오른쪽
        [SerializeField] CanvasGroup crosshairGroup;
        [SerializeField] TMP_Text interactPrompt;
        [SerializeField] Image damageVignette;
        [SerializeField] Image fade;

        [Header("모양")]
        [SerializeField] float crosshairGap = 10f;
        [SerializeField] float crosshairMoveGap = 22f;
        [SerializeField] float damagePulse = 0.55f;
        [SerializeField] float damageFadeDuration = 0.8f;
        [SerializeField] float runStartFade = 1.2f;

        float damageFlash;
        float lowHealthPulseBlend;
        float lowHealthPulsePhase;
        float transitionFade;
        float gap;

        void Start()
        {
            health.Damaged += (_, __) => damageFlash = damagePulse;
            director.Flow.SectionChanged += t =>
            {
                if (t.Reason == TransitionReason.RunStart) transitionFade = 1f;
                if (t.Reason == TransitionReason.RunStart || t.To == FlowSection.Title || t.To == FlowSection.AllySupport || t.To == FlowSection.Dawn || t.To == FlowSection.Result)
                    ClearDamageFeedback();
            };
            gap = crosshairGap;
        }

        void Update()
        {
            var flow = director.Flow;
            var mode = player.Gate.Mode;
            bool combat = flow.IsRunning && flow.Section != FlowSection.Dawn && flow.Section != FlowSection.Result;
            combatGroup.alpha = Mathf.MoveTowards(combatGroup.alpha, combat ? 1f : 0f, Time.unscaledDeltaTime * 3f);

            UpdateWeapon();
            UpdateCrosshair(mode);
            UpdatePrompt();

            UpdateDamageFeedback(flow, combat);

            transitionFade = Mathf.MoveTowards(transitionFade, 0f, Time.unscaledDeltaTime / runStartFade);
            SetAlpha(fade, Mathf.Max(run.Fade, transitionFade));
        }

        void UpdateDamageFeedback(DemoFlow flow, bool combat)
        {
            if (!combat || flow.Paused || flow.CombatLocked)
            {
                ClearDamageFeedback();
                return;
            }

            damageFlash = Mathf.MoveTowards(damageFlash, 0f,
                Time.deltaTime * damagePulse / Mathf.Max(0.01f, damageFadeDuration));
            float lowHealth = (1f - health.Fraction) * 0.45f;
            // Keep the existing transparent-centre sprite; gently strengthen only its edge.
            float pulseTarget = health.Fraction <= 0.35f ? 1f : 0f;
            lowHealthPulseBlend = Mathf.MoveTowards(lowHealthPulseBlend, pulseTarget, Time.deltaTime * 2f);
            lowHealthPulsePhase = Mathf.Repeat(lowHealthPulsePhase + Time.deltaTime * (2f * Mathf.PI / 1.8f), 2f * Mathf.PI);
            float pulse = (0.5f - 0.5f * Mathf.Cos(lowHealthPulsePhase)) * 0.12f * lowHealthPulseBlend;
            SetAlpha(damageVignette, Mathf.Max(damageFlash, lowHealth + pulse));
        }

        void ClearDamageFeedback()
        {
            damageFlash = 0f;
            lowHealthPulseBlend = 0f;
            lowHealthPulsePhase = 0f;
            SetAlpha(damageVignette, 0f);
        }

        void UpdateWeapon()
        {
            if (weapons.Current == WeaponSlot.M1)
            {
                var mag = weapons.M1.Magazine;
                weaponName.text = text.m1Name;
                ammoMain.text = mag.InClip.ToString();
                ammoReserve.text = mag.Reserve.ToString();
                ammoMain.color = mag.Total == 0 ? UiStyle.Warning : UiStyle.Text;
                for (int i = 0; i < clipPips.Length; i++)
                {
                    clipPips[i].enabled = i < mag.ClipSize;
                    clipPips[i].color = i < mag.InClip ? UiStyle.Text : UiStyle.Dim * new Color(1, 1, 1, 0.35f);
                }
            }
            else
            {
                var bz = weapons.Bazooka;
                weaponName.text = text.bazookaName;
                ammoMain.text = bz.Loaded.ToString();
                ammoReserve.text = bz.Reserve.ToString();
                ammoMain.color = bz.Loaded + bz.Reserve == 0 ? UiStyle.Warning : UiStyle.Text;
                for (int i = 0; i < clipPips.Length; i++) clipPips[i].enabled = false;
            }
        }

        void UpdateCrosshair(ControlMode mode)
        {
            float aim = weapons.Current == WeaponSlot.M1 ? weapons.M1.AimBlend : weapons.Bazooka.AimBlend;
            bool show = mode == ControlMode.Full && aim < 0.5f && !weapons.Switching;
            crosshairGroup.alpha = Mathf.MoveTowards(crosshairGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            bool moving = player.MoveInput.sqrMagnitude > 0.01f;
            gap = Mathf.MoveTowards(gap, moving ? crosshairMoveGap : crosshairGap, Time.deltaTime * 60f);
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            for (int i = 0; i < crosshairLines.Length && i < dirs.Length; i++)
                crosshairLines[i].anchoredPosition = dirs[i] * gap;
        }

        void UpdatePrompt()
        {
            string prompt = "";
            var controls = player.Controls;
            if (player.Gate.CanAct)
            {
                if (resupplier.Nearby != null)
                    prompt = $"{UiStyle.Key(controls.Resupply)} {text.resupplyPrompt}";
                else if (weapons.Current == WeaponSlot.M1 && weapons.M1.Magazine.Total == 0)
                    prompt = $"<color=#EA5C4D>{text.resupplyPrompt}</color>";
            }
            interactPrompt.text = prompt;
        }

        static void SetAlpha(Image image, float a)
        {
            var c = image.color;
            c.a = a;
            image.color = c;
            image.enabled = a > 0.001f;
        }
    }
}
