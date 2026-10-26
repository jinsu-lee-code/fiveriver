using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Core
{
    public enum ControlMode
    {
        None = 0,     // 타이틀 · 결과 화면
        LookOnly = 1, // 아군 지원 · 새벽 (시점 회전만, GDD 4번 S6)
        Full = 2,     // S1~S5
    }

    // 게임 입력(이동 · 시점 · 사격)을 막는 곳 하나. 디버그 창 · 일시정지 메뉴가 막는다.
    // 마우스 커서도 여기서만 잠그고 푼다.
    public sealed class InputGate : MonoBehaviour
    {
        readonly HashSet<object> blockers = new HashSet<object>();
        ControlMode mode;

        public bool CanLook => blockers.Count == 0 && mode != ControlMode.None;
        public bool CanAct => blockers.Count == 0 && mode == ControlMode.Full;

        public ControlMode Mode
        {
            get => mode;
            set
            {
                mode = value;
                ApplyCursor();
            }
        }

        public static ControlMode ModeFor(FlowSection section)
        {
            switch (section)
            {
                case FlowSection.Tutorial:
                case FlowSection.Scout:
                case FlowSection.Assault:
                case FlowSection.Silence:
                case FlowSection.Tank:
                    return ControlMode.Full;
                case FlowSection.AllySupport:
                case FlowSection.Dawn:
                    return ControlMode.LookOnly;
                default:
                    return ControlMode.None;
            }
        }

        public void Block(object who)
        {
            if (blockers.Add(who)) ApplyCursor();
        }

        public void Unblock(object who)
        {
            if (blockers.Remove(who)) ApplyCursor();
        }

        void ApplyCursor()
        {
            bool locked = CanLook;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
