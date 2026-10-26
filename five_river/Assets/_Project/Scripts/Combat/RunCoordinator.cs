using System;
using System.Collections;
using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 회차 시작 · 사망 · 체크포인트 복구 때 플레이어를 되돌린다 (FLOW.md 4 · 5번의 플레이어 칸).
    // 적 · 전차 · 조명탄 · 효과는 각자 RetryRequested · SectionChanged를 받아 정리한다.
    public sealed class RunCoordinator : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] LevelPoints points;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth health;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] Effects effects;
        [SerializeField] float deathFadeTime = 1.2f;

        bool retried;
        Coroutine deathRoutine;

        public bool Dying => deathRoutine != null;

        // 화면 어둡게 (0~1). UI가 그린다.
        public float Fade { get; private set; }

        public event Action PlayerRestored;

        void Start()
        {
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.RetryRequested += OnRetry;
            health.Died += OnDied;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.Reason == TransitionReason.RunStart)
            {
                StopDeath();
                effects.ClearAll();
                Restore(points.playerSpawn, bazooka: false);
                player.ResetTutorialCounters();
            }
            else if (t.Reason == TransitionReason.DebugJump)
            {
                StopDeath();
                Restore(t.To >= FlowSection.Tank ? points.tankCheckpoint : points.playerSpawn, bazooka: t.To >= FlowSection.Tank);
            }
            else if (t.To == FlowSection.Tank && t.From != FlowSection.Tank)
            {
                weapons.GiveBazooka();
            }
            else if (t.To == FlowSection.Title)
            {
                StopDeath();
                effects.ClearAll();
            }
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            retried = true;
            effects.ClearAll();
            bool tank = checkpoint == FlowCheckpoint.Tank;
            Restore(tank ? points.tankCheckpoint : points.playerSpawn, bazooka: tank);
        }

        void OnDied()
        {
            if (deathRoutine == null) deathRoutine = StartCoroutine(DeathSequence());
        }

        IEnumerator DeathSequence()
        {
            for (float t = 0f; t < deathFadeTime; t += Time.unscaledDeltaTime)
            {
                Fade = t / deathFadeTime;
                yield return null;
            }
            Fade = 1f;
            retried = false;
            director.Flow.ReportPlayerDied();
            // 8:15 이후 · 체크포인트 없는 구간이면 복구가 오지 않는다 → 그 자리에서 일으킨다
            if (!retried) health.Restore();
            for (float t = 0f; t < deathFadeTime * 0.6f; t += Time.unscaledDeltaTime)
            {
                Fade = 1f - t / (deathFadeTime * 0.6f);
                yield return null;
            }
            Fade = 0f;
            deathRoutine = null;
        }

        void StopDeath()
        {
            if (deathRoutine != null) StopCoroutine(deathRoutine);
            deathRoutine = null;
            Fade = 0f;
        }

        void Restore(Transform at, bool bazooka)
        {
            player.Teleport(at);
            health.Restore();
            weapons.ResetLoadout(bazooka, WeaponSlot.M1);
            PlayerRestored?.Invoke();
        }
    }
}
