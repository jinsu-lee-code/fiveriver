using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveRivers.UI
{
    // Esc 일시정지 메뉴. 게임 시간만 멈추고 회차 시계는 흐른다 (GDD 14번 9).
    // 7:00 · 8:15 강제 전환이 일시정지를 풀면 메뉴도 닫힌다.
    // 운영자 재시작: Ctrl + Shift + R (어디서든 시작 화면으로, GDD 14번 10 결정)
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] PlayerController player;
        [SerializeField] GameText text;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text resume;
        [SerializeField] TMP_Text restart;

        bool open;

        void Start()
        {
            title.text = text.pauseTitle;
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        void Update()
        {
            var flow = director.Flow;
            var kb = Keyboard.current;

            if (kb != null && kb.rKey.wasPressedThisFrame && kb.ctrlKey.isPressed && kb.shiftKey.isPressed)
            {
                Close();
                flow.AbortToTitle();
                return;
            }

            bool canPause = flow.IsRunning && flow.Section <= FlowSection.Tank;
            if (open && (!flow.Paused || !canPause)) Close();

            if (player.Controls.Pause.WasPressedThisFrame())
            {
                if (open) Close();
                else if (canPause) Open();
            }
            if (open && kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame) Close();
            }

            resume.text = $"{UiStyle.Key(player.Controls.Pause)} {text.resumeLabel}";
            restart.text = $"{UiStyle.Key("Ctrl+Shift+R")} {text.restartLabel}";
            group.alpha = Mathf.MoveTowards(group.alpha, open ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        }

        void Open()
        {
            open = true;
            director.SetPaused(true);
            player.Gate.Block(this);
        }

        void Close()
        {
            if (!open) return;
            open = false;
            if (director.Flow.Paused) director.SetPaused(false);
            player.Gate.Unblock(this);
        }
    }
}
